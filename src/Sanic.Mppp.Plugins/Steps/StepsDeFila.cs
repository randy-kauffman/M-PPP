using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Mppp.Plugins.Datos;
using Sanic.Mppp.Plugins.Dominio;
using Sanic.Mppp.Plugins.Respuesta;

namespace Sanic.Mppp.Plugins.Steps
{
    /// <summary>
    /// Plomería común a los tres steps de esta pieza (diseno/03 §4 y §5): leer la imagen previa que trae la columna que
    /// hace falta, resolver los roles de negocio del actor con un número FIJO de consultas, leer `rpa.puedeaprobar` y el
    /// nombre de quien actuó para la Bitácora (D-21, `sanic_actortexto`).
    /// </summary>
    internal static class PlomeriaDeFila
    {
        // Los tres nombres viven en `TablasNativas` y en ningún otro lado: tenerlos duplicados acá
        // fue lo que permitió que dos de ellos quedaran con un valor que el entorno no conoce.

        /// <summary>La primera pre-image que trae `columna` (03 §4: "el estado de origen sale de la PRE-IMAGE, nunca del Target", sea cual sea la clave con la que la registraron).</summary>
        internal static Entity ImagenQueTrae(IPluginExecutionContext contexto, string columna)
        {
            foreach (var imagen in contexto.PreEntityImages.Values)
            {
                if (imagen != null && imagen.Contains(columna))
                {
                    return imagen;
                }
            }

            throw new InvalidPluginExecutionException($"Falta la imagen previa con la columna '{columna}'.");
        }

        /// <summary>
        /// Los roles de negocio del actor (Ejecutivo, Supervisor, RPA), con un número FIJO de consultas (diseno/04 §2 y §3;
        /// DN-08): los tres roles por `name` y, vía <see cref="RolesDeMppp"/>, los que el actor tiene asignados directo o por
        /// un equipo (de propietario o de grupo de Entra). Las banderas de todas las fuentes se unen: Ejecutivo directo +
        /// Supervisor por un grupo es "los dos roles" (D-25).
        /// </summary>
        internal static RolDeActor DeterminarRoles(IOrganizationService servicio, Guid actorId)
        {
            var nombrePorRoleId = RolesDeMppp.IdsPorNombre(
                servicio, new[] { TablasNativas.RolEjecutivo, TablasNativas.RolSupervisor, TablasNativas.RolRpa });
            if (nombrePorRoleId.Count == 0)
            {
                // Que NINGUNO de los tres roles exista en el entorno no es "este usuario no tiene rol":
                // es la solución mal instalada, y lo arregla un administrador en minutos. Se dice, en vez
                // de devolver `Ninguno` y dejar que el mensaje culpe al usuario. Confundir las dos cosas
                // fue lo que escondió el defecto de los nombres durante todo el 2026-09-23.
                throw new InvalidPluginExecutionException(
                    $"Los roles de MPPP no están instalados en este entorno: no existe ninguno llamado "
                    + $"'{TablasNativas.RolEjecutivo}', '{TablasNativas.RolSupervisor}' ni '{TablasNativas.RolRpa}'. "
                    + "Sin ellos no se puede autorizar ninguna transición. Avisá al administrador del entorno.");
            }

            var resultado = RolDeActor.Ninguno;
            foreach (var roleId in RolesDeMppp.RolesDelUsuario(servicio, actorId, nombrePorRoleId.Keys.ToList()))
            {
                if (nombrePorRoleId.TryGetValue(roleId, out var nombre))
                {
                    resultado |= NombreARol(nombre);
                }
            }

            return resultado;
        }

        private static RolDeActor NombreARol(string nombre)
        {
            if (string.Equals(nombre, TablasNativas.RolEjecutivo, StringComparison.OrdinalIgnoreCase))
            {
                return RolDeActor.Ejecutivo;
            }

            if (string.Equals(nombre, TablasNativas.RolSupervisor, StringComparison.OrdinalIgnoreCase))
            {
                return RolDeActor.Supervisor;
            }

            if (string.Equals(nombre, TablasNativas.RolRpa, StringComparison.OrdinalIgnoreCase))
            {
                return RolDeActor.Rpa;
            }

            return RolDeActor.Ninguno;
        }

        /// <summary>`rpa.puedeaprobar` vale `si` (sin distinguir mayúsculas) para habilitar la excepción; ausente o cualquier otro valor, no (diseno/03 §4).</summary>
        internal static bool LeerRpaPuedeAprobar(IOrganizationService servicio)
        {
            var parametro = new CatalogosDataverse(servicio).Parametro(TransicionDeFilaStep.ParametroRpaPuedeAprobar);
            return parametro != null && string.Equals(parametro.Valor, "si", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Toma el bloqueo exclusivo de la Solicitud antes de mirar sus filas.
        ///
        /// **Por qué.** Este step decide si cierra la Solicitud mirando el estado de TODAS sus filas.
        /// Cuando dos filas de la misma Solicitud se aprueban al mismo tiempo —el ribbon las manda en
        /// paralelo, y dos supervisores pueden hacerlo desde dos máquinas— cada transacción lee las
        /// filas ANTES de que la otra confirme, las dos ven "todavía queda alguna abierta" y **ninguna
        /// cierra**. Pasó en Dev el 2026-09-23 con MPPP-00001008: tres filas aprobadas, Solicitud en
        /// proceso, y nadie atrás para darse cuenta.
        ///
        /// **Cómo.** Es el patrón que Microsoft documenta como *"Approach 3: Pre-lock in a plug-in
        /// transaction"* (Learn, Scalable Customization Design, ejemplo de autonumeración): se escribe
        /// un valor de marcador en una columna técnica de la fila compartida, lo que **pide y toma un
        /// lock exclusivo de escritura sobre esa fila**. La segunda transacción espera a que la primera
        /// confirme, y entonces sí lee el estado real. El VALOR escrito no lo lee nadie: lo único que
        /// importa es el lock.
        ///
        /// **Qué cuesta.** Serializa las transiciones de filas de LA MISMA Solicitud. Filas de
        /// solicitudes distintas no se estorban. Learn avisa que el pre-lock se paga con contención y
        /// recomienda tomarlo lo más tarde posible dentro de la transacción: por eso va acá, en el
        /// PostOperation y justo antes de la lectura, y no al principio del pipeline.
        ///
        /// Las dos alternativas que se descartaron: serializar en el cliente (no resuelve dos usuarios
        /// a la vez) y cerrar por barrido periódico (difiere el cierre siempre). El barrido queda como
        /// red de seguridad, no como el mecanismo.
        /// </summary>
        internal static void TomarElBloqueoDeLaSolicitud(IOrganizationService servicio, Guid solicitudId, IPluginExecutionContext contexto)
        {
            servicio.Update(new Entity(TablasHistorico.Solicitud, solicitudId)
            {
                ["sanic_bloqueodecierre"] = AhoraUtc(contexto),
            });
        }

        /// <summary>
        /// La fecha de la operación, marcada como UTC.
        ///
        /// `OperationCreatedOn` VIENE en UTC, pero el SDK lo entrega con `Kind = Unspecified`, y
        /// `SolicitudesDataverse` exige `Kind = Utc` a propósito: una fecha con otro Kind se
        /// guardaría corrida en una columna de hora local, en silencio. Las dos Custom API ya
        /// hacían este `SpecifyKind`; los steps de Fila usaban el valor crudo y reventaban con
        /// "La fecha tiene que venir en UTC" la primera vez que una transición llegó a ejecutarse
        /// de verdad en el entorno (2026-09-23). Ahora sale de un solo lugar.
        /// </summary>
        internal static DateTime AhoraUtc(IPluginExecutionContext contexto) =>
            DateTime.SpecifyKind(contexto.OperationCreatedOn, DateTimeKind.Utc);

        /// <summary>`sanic_actortexto` (D-21): el `fullname` de quien inicia, con una consulta.</summary>
        internal static string NombreDeQuienActua(IOrganizationService servicio, Guid usuarioId)
        {
            var usuario = servicio.Retrieve(TablasNativas.Usuario, usuarioId, new ColumnSet("fullname"));
            return usuario.GetAttributeValue<string>("fullname");
        }

        /// <summary>
        /// `sanic_claimhasta` ACTUAL de la fila (diseno/03 §2, "El claim"). No viaja en la pre-image registrada
        /// (`sanic_estado, sanic_digitadapor, sanic_solicitudid`): ampliarla es tocar el registro de steps, fuera
        /// de este alcance. Se lee con un `Retrieve` propio de UNA sola columna.
        ///
        /// Se SALTEA para el código de servidor: el claim nunca lo frena a él (03 §2), así que preguntarlo sería
        /// gastar una consulta que nadie va a mirar.
        ///
        /// **Costo, dicho de frente** (revisión del aprobador, 2026-09-25): el ribbon transiciona filas en LOTE,
        /// así que esto es una consulta extra POR FILA que transiciona una PERSONA. Se acepta por ser de una sola
        /// columna; no convertirla en una consulta de dos.
        /// </summary>
        internal static DateTime? LeerClaimHasta(IOrganizationService servicio, Guid filaId, bool esCodigoDeServidor)
        {
            if (esCodigoDeServidor)
            {
                return null;
            }

            var fila = servicio.Retrieve(TablasHistorico.Fila, filaId, new ColumnSet("sanic_claimhasta"));
            return fila.Contains("sanic_claimhasta") ? (DateTime?)fila["sanic_claimhasta"] : null;
        }

        /// <summary>
        /// El usuario cuyos roles de negocio hay que resolver para autorizar la transición (revisión de código,
        /// 2026-09-21, spike C-05 parte B): si NO es código de servidor, o si es código de servidor sin anidar
        /// (identidad de aplicación en profundidad 1: es quien llama de verdad), es `InitiatingUserId`. Si es código
        /// de servidor ANIDADO (profundidad mayor que 1: la Custom API del RPA escribiendo como SYSTEM, donde
        /// `InitiatingUserId` vale SYSTEM en todos los niveles), se confía en la identidad que la Custom API ya
        /// grabó: `sanic_aprobadapor` del `Target` si la transición va a Aprobada, `sanic_digitadapor` del `Target`
        /// si va a Digitada. Para cualquier OTRO caso no hay ninguna columna del `Target` que guarde una identidad
        /// (no existe `sanic_rechazadopor`): se devuelve `null` a propósito — ver
        /// <see cref="RolDeServidorSinIdentidad"/>, que es quien decide el rol para ese hueco, en vez de adivinar
        /// una identidad que no existe.
        /// </summary>
        internal static Guid? ResolverIdentidadDelActor(IPluginExecutionContext contexto, bool esCodigoDeServidor, Entity target, Entity preImagen, EstadoDeLaFila hacia)
        {
            if (!esCodigoDeServidor || contexto.Depth <= 1)
            {
                return contexto.InitiatingUserId;
            }

            if (hacia == EstadoDeLaFila.Aprobada)
            {
                return target.GetAttributeValue<EntityReference>("sanic_aprobadapor")?.Id;
            }

            if (hacia == EstadoDeLaFila.Digitada)
            {
                return target.GetAttributeValue<EntityReference>("sanic_digitadapor")?.Id;
            }

            return null;
        }

        /// <summary>
        /// D-60 (2026-09-28). El rol a usar cuando <see cref="ResolverIdentidadDelActor"/> no pudo resolver ninguna
        /// identidad para código de servidor ANIDADO (`esCodigoDeServidor &amp;&amp; Depth &gt; 1`) — hoy, en la
        /// práctica, solo pasa para `Hacia == RechazadaEnAS400`.
        ///
        /// **El defecto que esto reemplaza.** Hasta acá, ese hueco se llenaba leyendo `sanic_digitadapor` de la
        /// PRE-IMAGE, como proxy de "quién está actuando". Esa columna es `null` cuando la fila nunca pasó por
        /// Digitada — el camino MÁS COMÚN del RPA: intenta directo desde Validada y AS400 la rechaza (diseno/03 §4,
        /// fila "Validada o Digitada → Rechazada en AS400") — así que el actor resolvía a `RolDeActor.Ninguno` y
        /// `AutorizarQuienDigita` (`Dominio/TransicionesDeFila.cs`) rechazaba SIEMPRE la transición, aunque quien
        /// llama ya hubiera pasado <see cref="PuertaDelRpa.Exigir"/> con el rol RPA confirmado. Confirmado leyendo
        /// el `plugintracelog` real de Dev (2026-09-28), en `messageblock` (NO en `exceptiondetails`, que la
        /// corrección de seguridad del proyecto genericiza a propósito):
        ///
        ///   RegistrarResultadoRpa: empieza.
        ///   System.ServiceModel.FaultException`1[Microsoft.Xrm.Sdk.OrganizationServiceFault]: El rol de quien
        ///   llama no puede hacer esta transición.
        ///
        /// **Por qué no hay una identidad real que leer.** No existe una columna `sanic_rechazadopor`: nadie
        /// registró nunca "quién rechazó" como dato propio (a diferencia de `sanic_digitadapor`/`sanic_aprobadapor`).
        /// Agregar una es un cambio de esquema de Dataverse, fuera del alcance de este arreglo. Y no hay forma de
        /// preguntarle a la plataforma quién originó la llamada real: el spike C-05 parte B ya probó que ni
        /// `context.ParentContext` ni `SharedVariables` sobreviven el `Update` anidado con SYSTEM
        /// (`spikes/c05-parseo-excel/CONCLUSIONES-B.md`, "Ninguna de las dos vías propuestas por el diseño sirve").
        ///
        /// **Por qué confiar el rol es correcto acá, y no un debilitamiento de la seguridad.** `AutorizarQuienDigita`
        /// (la única autorización que usa esta transición) NUNCA compara identidades — solo mira el ROL
        /// (`p.Actor.Roles == Ejecutivo || p.Actor.Roles == Rpa`), a diferencia de `AutorizarAprobar`, que sí exige
        /// segregación de funciones por identidad. Y la identidad REAL de quien llama a la Custom API del RPA YA se
        /// verificó, con consulta a Dataverse, ANTES de llegar acá: <see cref="PuertaDelRpa.Exigir"/> corre en
        /// `RegistrarResultadoRpaApi.Execute` (D-9) sobre el `InitiatingUserId` de ESE nivel — el mismo que, según
        /// documenta `diseno/03-contratos-custom-api.md` §4, "en su nivel, antes de elevar, sí es el usuario de
        /// aplicación del bot". Confiar acá es el MISMO criterio "a SYSTEM se le cree" que ya gobierna
        /// `BaseDeStep.EsCodigoDeServidor` (ver su propio comentario) y el bypass del claim en
        /// `TransicionesDeFila.Evaluar` — no una excepción nueva.
        ///
        /// **Por qué no generaliza a cualquier transición.** Se falla CERRADO (`RolDeActor.Ninguno`) para cualquier
        /// `hacia` que no sea `RechazadaEnAS400`: hoy Anular nunca lo hace el RPA (03 §4, "el RPA nunca anula") y
        /// Devolver solo lo hace un Supervisor persona (nunca código de servidor), así que ninguna otra combinación
        /// debería llegar acá. Si el día de mañana otra transición empieza a escribirse desde código de servidor
        /// anidado, tiene que agregarse ACÁ a propósito — nunca heredar un "sí" que nadie pidió.
        /// </summary>
        internal static RolDeActor RolDeServidorSinIdentidad(bool esCodigoDeServidor, int depth, EstadoDeLaFila hacia)
        {
            return esCodigoDeServidor && depth > 1 && hacia == EstadoDeLaFila.RechazadaEnAS400
                ? RolDeActor.Rpa
                : RolDeActor.Ninguno;
        }
    }

    /// <summary>
    /// 7.9, transición de estado de Fila: `Update` de Fila, PreOperation, síncrono, filtro `sanic_estado`, con pre-image
    /// (`sanic_estado`, `sanic_digitadapor`, `sanic_solicitudid`), orden 1 (diseno/03 §4, D-25). Contrato (lo fijan las pruebas
    /// `StepsDeFilaAceptacion`):
    ///  - el estado de origen sale de la PRE-IMAGE, nunca del `Target` (nadie puede decir de dónde venía);
    ///  - **quién actúa**: si escribe una persona, `sanic_digitadapor`, `sanic_aprobadapor` y sus fechas que vengan en el `Target`
    ///    se IGNORAN y se pisan con `InitiatingUserId` y la fecha del contexto; si escribe código de servidor, se confía en el
    ///    `Target` (es la Custom API del RPA, que ya puso al usuario del bot; `03` §4);
    ///  - **de quién se leen los roles** (revisión de código, 2026-09-21): si escribe una persona, de `InitiatingUserId`. Si
    ///    escribe código de servidor ANIDADO (profundidad mayor que 1, o sea la Custom API del RPA escribiendo como SYSTEM), de
    ///    `InitiatingUserId` NO se puede: el spike C-05 parte B probó que ahí vale SYSTEM en todos los niveles. Se leen de la
    ///    identidad que la Custom API ya grabó en el `Target`: `sanic_aprobadapor` para Aprobada, `sanic_digitadapor` para
    ///    Digitada, y para el resto el `sanic_digitadapor` de la pre-image (quien venía trabajando la fila). Si ahí tampoco hay
    ///    nadie, el actor queda sin roles y la transición se rechaza: nunca se asume un rol;
    ///  - los roles salen de los security roles de ese usuario, con un número fijo de consultas;
    ///  - la transición se evalúa SIEMPRE con <see cref="Dominio.TransicionesDeFila"/>, venga de quien venga, y si no está
    ///    permitida el guardado se rechaza con su motivo;
    ///  - `rpa.puedeaprobar` se lee del parámetro (`si` habilita la excepción; ausente o cualquier otra cosa = no);
    ///  - los efectos se escriben en el `Target`: Digitada pone `sanic_digitadapor`/`sanic_fechadigitada`; Aprobada pone
    ///    `sanic_aprobadapor`/`sanic_fechaaprobada`; Devolver limpia `sanic_digitadapor`/`sanic_fechadigitada`;
    ///  - un `Update` que no cambia el estado (el `Target` no trae `sanic_estado`) no hace nada;
    ///  - si la transición aterriza en un estado que ya NO es "abierto" (<see cref="Dominio.TransicionesDeFila.EstaAbierta"/>:
    ///    hoy Aprobada, Rechazada en AS400 o Anulada), se libera `sanic_claimhasta` (diseno/03 §2, "Cuándo se libera"); en
    ///    Validada → Digitada, que sigue "abierta", el claim NO se toca.
    /// </summary>
    public sealed class TransicionDeFilaStep : IPlugin
    {
        public const string ParametroRpaPuedeAprobar = "rpa.puedeaprobar";

        public void Execute(IServiceProvider serviceProvider)
        {
            if (!PlomeriaDePlataforma.Preparar(serviceProvider, out var contexto, out var servicio, out var target))
            {
                return;
            }

            if (!string.Equals(target.LogicalName, TablasHistorico.Fila, StringComparison.Ordinal) || !target.Contains("sanic_estado"))
            {
                // Filtering attributes = sanic_estado (03 §4): un Update que no cambia el estado no hace nada.
                return;
            }

            var preImagen = PlomeriaDeFila.ImagenQueTrae(contexto, "sanic_estado");
            var desde = (EstadoDeLaFila)preImagen.GetAttributeValue<OptionSetValue>("sanic_estado").Value;
            var hacia = (EstadoDeLaFila)target.GetAttributeValue<OptionSetValue>("sanic_estado").Value;
            var digitadaPorPrevio = preImagen.GetAttributeValue<EntityReference>("sanic_digitadapor")?.Id;

            var esCodigoDeServidor = BaseDeStep.EsCodigoDeServidor(contexto, servicio);
            // El actor para roles y para la segregación de funciones es el mismo usuario confiado, nunca SYSTEM
            // (revisión de código, 2026-09-21): ver el contrato arriba y PlomeriaDeFila.ResolverIdentidadDelActor.
            var actorId = PlomeriaDeFila.ResolverIdentidadDelActor(contexto, esCodigoDeServidor, target, preImagen, hacia);
            // D-60 (2026-09-28): sin identidad resoluble (código de servidor anidado, "cualquier otro caso" —
            // hoy solo Rechazada en AS400), el rol NO sale de una consulta: ver PlomeriaDeFila.RolDeServidorSinIdentidad.
            var roles = actorId.HasValue
                ? PlomeriaDeFila.DeterminarRoles(servicio, actorId.Value)
                : PlomeriaDeFila.RolDeServidorSinIdentidad(esCodigoDeServidor, contexto.Depth, hacia);
            var rpaPuedeAprobar = PlomeriaDeFila.LeerRpaPuedeAprobar(servicio);
            var ahora = PlomeriaDeFila.AhoraUtc(contexto);
            var claimHasta = PlomeriaDeFila.LeerClaimHasta(servicio, target.Id, esCodigoDeServidor);

            var pedido = new PedidoDeTransicion
            {
                Desde = desde,
                Hacia = hacia,
                Actor = new Actor(actorId ?? Guid.Empty, roles),
                Mensaje = target.Contains("sanic_mensaje") ? target["sanic_mensaje"] as string : null,
                // La nota de la devolución viaja en su propia columna y NUNCA sale en el correo al cliente
                // (2026-09-24). Ver `PedidoDeTransicion.NotaInterna`.
                NotaInterna = target.Contains("sanic_notainterna") ? target["sanic_notainterna"] as string : null,
                DigitadaPor = digitadaPorPrevio,
                RpaPuedeAprobar = rpaPuedeAprobar,
                ClaimHasta = claimHasta,
                Ahora = ahora,
                EsCodigoDeServidor = esCodigoDeServidor,
            };

            var resultado = TransicionesDeFila.Evaluar(pedido);
            if (!resultado.Permitida)
            {
                throw new InvalidPluginExecutionException(resultado.Motivo);
            }

            AplicarEfecto(target, resultado.Efecto, contexto, esCodigoDeServidor);

            // diseno/03 §3, "Atención manual: una MARCA, no un estado": "Se apaga sola. La marca la limpia
            // CUALQUIER transición exitosa de la fila". Sin enumerar casos: no importa el Efecto, el Hacia, ni
            // quién hizo la transición (persona o bot) — si llegamos acá, se permitió, y se apaga.
            target["sanic_requiereatencionmanual"] = false;

            // diseno/03 §2, "Cuándo se libera": "Al vencer, y al llegar la fila a un estado terminal (Aprobada,
            // Rechazada en AS400, Anulada) ... En Validada → Digitada el claim NO se libera". Se reusa
            // `EstaAbierta` (Validada y Digitada son los únicos estados "abiertos" hoy) en vez de enumerar los
            // terminales a mano: la lista de terminales de hoy es la de hoy, y en fase 3 puede crecer sin que
            // haga falta tocar esta línea. Por qué Validada → Digitada NO libera aunque el destino sea distinto
            // del origen: al bot le queda trabajo sobre esa fila —aprobarla—, y soltarle la reserva ahí lo
            // dejaría peleando contra un ejecutivo justo en el medio de su propio trabajo.
            if (!TransicionesDeFila.EstaAbierta(hacia))
            {
                target["sanic_claimhasta"] = null;
            }
        }

        /// <summary>Escribe quién y cuándo según el efecto (03 §4). A una persona nunca se le cree lo que mandó; al código de servidor se le confía lo que ya trae, y se completa solo lo que falte.</summary>
        private static void AplicarEfecto(Entity target, EfectoDeTransicion efecto, IPluginExecutionContext contexto, bool esCodigoDeServidor)
        {
            switch (efecto)
            {
                case EfectoDeTransicion.RegistrarDigitacion:
                    EscribirQuienYCuando(target, "sanic_digitadapor", "sanic_fechadigitada", contexto, esCodigoDeServidor);
                    break;
                case EfectoDeTransicion.RegistrarAprobacion:
                    EscribirQuienYCuando(target, "sanic_aprobadapor", "sanic_fechaaprobada", contexto, esCodigoDeServidor);
                    break;
                case EfectoDeTransicion.LimpiarDigitacion:
                    // Devolver: limpia explícitamente en el Target, poniendo null (no omitiendo la columna).
                    target["sanic_digitadapor"] = null;
                    target["sanic_fechadigitada"] = null;
                    break;
            }
        }

        private static void EscribirQuienYCuando(Entity target, string columnaUsuario, string columnaFecha, IPluginExecutionContext contexto, bool esCodigoDeServidor)
        {
            if (esCodigoDeServidor)
            {
                // 03 §4: "si quien llama ES SYSTEM ... el step confía en ese Target"; solo se completa lo que no vino.
                if (!target.Contains(columnaUsuario))
                {
                    target[columnaUsuario] = new EntityReference(TablasNativas.Usuario, contexto.InitiatingUserId);
                }

                if (!target.Contains(columnaFecha))
                {
                    target[columnaFecha] = PlomeriaDeFila.AhoraUtc(contexto);
                }

                return;
            }

            // "Si quien llama NO es SYSTEM ... se ignoran y se pisan con InitiatingUserId y la hora actual" (03 §4).
            target[columnaUsuario] = new EntityReference(TablasNativas.Usuario, contexto.InitiatingUserId);
            target[columnaFecha] = PlomeriaDeFila.AhoraUtc(contexto);
        }
    }

    /// <summary>
    /// 7.9, post-transición: `Update` de Fila, PostOperation, filtro `sanic_estado`, con pre-image (`sanic_estado`,
    /// `sanic_solicitudid`, `sanic_numerofila`) (diseno/03 §4 "Cierre"). Contrato:
    ///  - escribe la Bitácora del evento que corresponde a la transición (Fila digitada, aprobada, devuelta, rechazada en AS400,
    ///    anulada), con el número de fila y el nombre de quien actuó en texto;
    ///  - después mira las filas de esa Solicitud: si la Solicitud sigue En proceso y ya no queda ninguna en Validada ni en
    ///    Digitada (<see cref="Dominio.CierreDeSolicitud"/>), la pasa a **Procesada**, pone `sanic_fechaprocesada` y arma
    ///    `sanic_respuestafinalcontenido` con <see cref="Respuesta.ArmadorRespuesta.RespuestaFinal"/>. El paso a Cerrada NO es de
    ///    este plugin (lo hace `MPPP-ENV` al enviar);
    ///  - si la Solicitud no está En proceso, o todavía quedan filas abiertas, solo queda la Bitácora;
    ///  - todo con un número fijo de consultas: las filas de la Solicitud en UNA, y la Solicitud en otra.
    /// </summary>
    public sealed class PostTransicionDeFilaStep : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            if (!PlomeriaDePlataforma.Preparar(serviceProvider, out var contexto, out var servicio, out var target))
            {
                return;
            }

            if (!string.Equals(target.LogicalName, TablasHistorico.Fila, StringComparison.Ordinal) || !target.Contains("sanic_estado"))
            {
                return;
            }

            var preImagen = PlomeriaDeFila.ImagenQueTrae(contexto, "sanic_estado");
            var desde = (EstadoDeLaFila)preImagen.GetAttributeValue<OptionSetValue>("sanic_estado").Value;
            var hacia = (EstadoDeLaFila)target.GetAttributeValue<OptionSetValue>("sanic_estado").Value;
            var solicitudId = preImagen.GetAttributeValue<EntityReference>("sanic_solicitudid").Id;
            var numeroFila = preImagen.GetAttributeValue<int>("sanic_numerofila");

            // El evento de Bitácora es un dato fijo del par (Desde, Hacia): la pre-operación ya autorizó esta misma
            // transición, así que acá no hace falta resolver roles ni leer `rpa.puedeaprobar` de nuevo (revisión de
            // código, 2026-09-21; Dominio.TransicionesDeFila.EventoPara mira la misma tabla que Evaluar).
            var evento = TransicionesDeFila.EventoPara(desde, hacia);
            if (evento == null)
            {
                // La pre-operación ya validó esta misma transición: llegar acá sin evento es un error de programación.
                throw new InvalidPluginExecutionException("No se pudo determinar el evento de Bitácora de la transición.");
            }

            var solicitudes = new SolicitudesDataverse(servicio);
            var actorTexto = PlomeriaDeFila.NombreDeQuienActua(servicio, contexto.InitiatingUserId);
            // DN-09 C: el "por qué" de esta acción (solo lo que trajo ESTE Update) queda en la Bitácora, porque la acción siguiente lo pisa en la fila.
            var detalle = TransicionesDeFila.DetalleDeBitacora(
                hacia,
                target.GetAttributeValue<string>("sanic_mensaje"),
                target.GetAttributeValue<string>("sanic_notainterna"));
            solicitudes.RegistrarEvento(solicitudId, PlomeriaDeFila.AhoraUtc(contexto), evento.Value, OrigenDelEvento.Plugin, numeroFila, actorTexto, detalle);

            PlomeriaDeFila.TomarElBloqueoDeLaSolicitud(servicio, solicitudId, contexto);

            // Consulta 1 (filas de la Solicitud): la fila que se acaba de transicionar todavía figura en la base con su
            // estado ANTERIOR en el doble de pruebas (no vuelve a aplicar el Update); se refleja el nuevo estado acá,
            // como ya lo tiene la plataforma real en PostOperation (diseno/03 §8).
            var filas = LeerFilasDeLaSolicitud(servicio, solicitudId, target.Id, hacia);

            var solicitud = solicitudes.Leer(solicitudId); // Consulta 2 (la Solicitud), ya bajo el bloqueo.

            // D-64 (diseno/05 DA-08): "Tenés filas por aprobar", a los supervisores, la PRIMERA vez que esta
            // Solicitud pasa a tener alguna fila Digitada — nunca una por fila. `filas` ya refleja el estado
            // Digitada de la que se acaba de transicionar (la sustitución de arriba); si es la ÚNICA Digitada de
            // la Solicitud, es porque antes de este Update no había ninguna: recién ahora "hay filas por aprobar".
            // Si ya había otra (el ribbon o el RPA transicionando varias filas de la misma tanda), ya se avisó.
            if (hacia == EstadoDeLaFila.Digitada && filas.Count(f => f.Estado == EstadoDeLaFila.Digitada) == 1)
            {
                new AvisosDataverse(servicio).AvisarFilasPorAprobar(solicitudId, TituloAvisoPorAprobar(solicitud.Numero), CuerpoAvisoPorAprobar(solicitud.Numero));
            }

            if (!CierreDeSolicitud.CorrespondeProcesar(solicitud.Estado, filas.Select(f => f.Estado)))
            {
                return;
            }

            var respuesta = ArmadorRespuesta.RespuestaFinal(new SolicitudParaRespuesta
            {
                Numero = solicitud.Numero,
                Estado = EstadoDeLaSolicitud.Procesada,
                FechaRecibidoTexto = null,
                MotivosDelSobre = new List<string>(),
                Filas = filas.Select(ConvertirParaRespuesta).ToList(),
            });

            var entidadSolicitud = new Entity(TablasHistorico.Solicitud, solicitudId);
            entidadSolicitud["sanic_estadoprocesamiento"] = new OptionSetValue((int)EstadoDeLaSolicitud.Procesada);
            entidadSolicitud["sanic_fechaprocesada"] = PlomeriaDeFila.AhoraUtc(contexto);
            entidadSolicitud["sanic_respuestafinalcontenido"] = respuesta;
            servicio.Update(entidadSolicitud);

            solicitudes.RegistrarEvento(solicitudId, PlomeriaDeFila.AhoraUtc(contexto), EventoDeBitacora.Procesada, OrigenDelEvento.Plugin, 0, null, null);
        }

        private static string TituloAvisoPorAprobar(string numeroSolicitud) => $"Tenés filas por aprobar: solicitud {numeroSolicitud}";

        private static string CuerpoAvisoPorAprobar(string numeroSolicitud) => $"La solicitud {numeroSolicitud} tiene filas por aprobar.";

        private static IList<FilaParaCierre> LeerFilasDeLaSolicitud(IOrganizationService servicio, Guid solicitudId, Guid filaQueTransiciono, EstadoDeLaFila haciaLaFilaQueTransiciono)
        {
            var consulta = new QueryExpression(TablasHistorico.Fila)
            {
                ColumnSet = new ColumnSet("sanic_numerofila", "sanic_numeroplan", "sanic_nombrebeneficiario", "sanic_numerocuenta", "sanic_numeroidentificacion", "sanic_estado", "sanic_mensaje"),
            };
            consulta.Criteria.AddCondition("sanic_solicitudid", ConditionOperator.Equal, solicitudId);

            return servicio.RetrieveMultiple(consulta).Entities
                .Select(fila => new FilaParaCierre
                {
                    NumeroFila = fila.GetAttributeValue<int>("sanic_numerofila"),
                    NumeroPlan = fila.GetAttributeValue<string>("sanic_numeroplan"),
                    NombreBeneficiario = fila.GetAttributeValue<string>("sanic_nombrebeneficiario"),
                    NumeroCuenta = fila.GetAttributeValue<string>("sanic_numerocuenta"),
                    NumeroIdentificacion = fila.GetAttributeValue<string>("sanic_numeroidentificacion"),
                    // La plataforma ya escribió este cambio en la base cuando el step corre en PostOperation (diseno/03 §8: el
                    // doble de pruebas no vuelve a aplicar el Update, así que acá se refleja igual que en Dev).
                    Estado = fila.Id == filaQueTransiciono ? haciaLaFilaQueTransiciono : (EstadoDeLaFila)fila.GetAttributeValue<OptionSetValue>("sanic_estado").Value,
                    Mensaje = fila.GetAttributeValue<string>("sanic_mensaje"),
                })
                .ToList();
        }

        private static FilaParaRespuesta ConvertirParaRespuesta(FilaParaCierre fila) => new FilaParaRespuesta
        {
            NumeroFila = fila.NumeroFila,
            NumeroPlan = fila.NumeroPlan,
            NombreBeneficiario = fila.NombreBeneficiario,
            NumeroCuenta = fila.NumeroCuenta,
            NumeroIdentificacion = fila.NumeroIdentificacion,
            Estado = fila.Estado,
            Mensaje = fila.Mensaje,
        };

        /// <summary>Lo mínimo de una Fila para decidir el cierre y armar la respuesta final. Sin SDK hacia afuera.</summary>
        private sealed class FilaParaCierre
        {
            public int NumeroFila { get; set; }

            public string NumeroPlan { get; set; }

            public string NombreBeneficiario { get; set; }

            public string NumeroCuenta { get; set; }

            public string NumeroIdentificacion { get; set; }

            public EstadoDeLaFila Estado { get; set; }

            public string Mensaje { get; set; }
        }
    }

    /// <summary>
    /// 7.12, atender un correo por clasificar: `Update` de Solicitud, PreOperation, filtro `sanic_estadoprocesamiento`
    /// (diseno/03 §5; diseno/07 DF-09). Contrato:
    ///  - solo se admite salir de **No reconocida** o **No es correo nuevo**, y solo hacia **Cerrada** o **Descartada**;
    ///  - cualquier otro cambio de estado hecho por una PERSONA se rechaza con su motivo (el código de servidor no pasa por acá:
    ///    la Custom API y `MPPP-VIG` mueven los estados que les tocan);
    ///  - no completa ninguna columna propia: quién atendió y cuándo queda en la Bitácora, con `sanic_actortexto` (D-21).
    /// </summary>
    public sealed class AtenderPorClasificarStep : IPlugin
    {
        private static readonly HashSet<EstadoDeLaSolicitud> DesdeValidos = new HashSet<EstadoDeLaSolicitud>
        {
            EstadoDeLaSolicitud.NoReconocida,
            EstadoDeLaSolicitud.NoEsCorreoNuevo,
        };

        private static readonly HashSet<EstadoDeLaSolicitud> HaciaValidos = new HashSet<EstadoDeLaSolicitud>
        {
            EstadoDeLaSolicitud.Cerrada,
            EstadoDeLaSolicitud.Descartada,
        };

        public void Execute(IServiceProvider serviceProvider)
        {
            if (!PlomeriaDePlataforma.Preparar(serviceProvider, out var contexto, out var servicio, out var target))
            {
                return;
            }

            if (!string.Equals(target.LogicalName, TablasHistorico.Solicitud, StringComparison.Ordinal) || !target.Contains("sanic_estadoprocesamiento"))
            {
                return;
            }

            if (BaseDeStep.EsCodigoDeServidor(contexto, servicio))
            {
                // 03 §5: la Custom API y MPPP-VIG mueven los estados que les tocan; este control no es asunto suyo.
                return;
            }

            var preImagen = PlomeriaDeFila.ImagenQueTrae(contexto, "sanic_estadoprocesamiento");
            var desde = (EstadoDeLaSolicitud)preImagen.GetAttributeValue<OptionSetValue>("sanic_estadoprocesamiento").Value;
            var hacia = (EstadoDeLaSolicitud)target.GetAttributeValue<OptionSetValue>("sanic_estadoprocesamiento").Value;

            if (!DesdeValidos.Contains(desde) || !HaciaValidos.Contains(hacia))
            {
                throw new InvalidPluginExecutionException(
                    $"No se puede cambiar el estado de '{desde}' a '{hacia}': solo se puede cerrar o descartar lo que está por clasificar.");
            }

            // "No reconocida atendida" cubre el cierre de los dos estados de origen (07 §6: "No reconocida → Cerrada (atendida)");
            // Descartada es el evento propio de esa transición.
            var evento = hacia == EstadoDeLaSolicitud.Cerrada ? EventoDeBitacora.NoReconocidaAtendida : EventoDeBitacora.Descartada;
            var actorTexto = PlomeriaDeFila.NombreDeQuienActua(servicio, contexto.InitiatingUserId);
            new SolicitudesDataverse(servicio).RegistrarEvento(target.Id, PlomeriaDeFila.AhoraUtc(contexto), evento, OrigenDelEvento.Plugin, 0, actorTexto, null);
        }
    }

    /// <summary>
    /// El boton "Revisado" (`05` §5): cuando una persona apaga `sanic_requiererevision`, este step deja el
    /// motivo en la Bitacora y limpia la columna. `Update` de Solicitud, PreOperation, filtro
    /// `sanic_requiererevision`, con pre-imagen de la marca y del motivo.
    ///
    /// **De donde sale el motivo.** `sanic_motivorevision` NO la escribe la persona: la escriben los flujos,
    /// para explicar por que hizo falta revisar ("Validacion fallida 3 veces", "Envio iniciado sin
    /// confirmar"). Es lo que el operador LEE en la vista "Para revisar". Por eso el boton no pide texto: si
    /// lo pidiera, la nota pisaria el diagnostico justo cuando se lo va a atender.
    ///
    /// Hasta el 2026-09-23 el comando SI pedia texto y lo escribia en esa columna, y no funcionaba: la lista
    /// blanca no admite `sanic_motivorevision` de una persona ("No se puede guardar: la(s) columna(s)
    /// sanic_motivorevision no esta(n) permitida(s)"). Ademas, el plugin que segun `05` §5 y el plan de
    /// pruebas "pasa la nota a la Bitacora" NO EXISTIA: este es. Se resolvio como el mockup aprobado el
    /// 2026-09-20, que no pide texto.
    ///
    /// La columna se limpia agregandola al `Target`, no con una escritura aparte: la lista blanca corre en
    /// orden 0 y ya termino, asi que agregar una columna despues no la esquiva — la persona sigue sin poder
    /// mandarla ella.
    /// </summary>
    public sealed class RevisionAtendidaStep : IPlugin
    {
        public const string Marca = "sanic_requiererevision";
        public const string Motivo = "sanic_motivorevision";

        public void Execute(IServiceProvider serviceProvider)
        {
            if (!PlomeriaDePlataforma.Preparar(serviceProvider, out var contexto, out var servicio, out var target))
            {
                return;
            }

            if (!string.Equals(target.LogicalName, TablasHistorico.Solicitud, StringComparison.Ordinal) || !target.Contains(Marca))
            {
                return;
            }

            var preImagen = PlomeriaDeFila.ImagenQueTrae(contexto, Marca);
            var estaba = preImagen.GetAttributeValue<bool>(Marca);
            var queda = target.GetAttributeValue<bool>(Marca);

            // Solo el apagado. Prenderla es asunto de los flujos, que ya escriben su propio evento.
            if (!estaba || queda)
            {
                return;
            }

            var motivo = preImagen.GetAttributeValue<string>(Motivo);
            var actorTexto = PlomeriaDeFila.NombreDeQuienActua(servicio, contexto.InitiatingUserId);
            new SolicitudesDataverse(servicio).RegistrarEvento(
                target.Id, PlomeriaDeFila.AhoraUtc(contexto), EventoDeBitacora.RevisionAtendida,
                OrigenDelEvento.Plugin, 0, actorTexto, motivo);

            // Se limpia para que la proxima vez que la marca se prenda, el motivo que se lea sea el nuevo y
            // no el de la vez anterior.
            target[Motivo] = null;
        }
    }
}
