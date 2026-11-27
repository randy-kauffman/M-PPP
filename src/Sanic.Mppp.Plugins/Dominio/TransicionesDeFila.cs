using System;
using System.Collections.Generic;

namespace Sanic.Mppp.Plugins.Dominio
{
    /// <summary>Todo lo que hace falta para decidir un cambio de estado de una Fila (diseno/03 §4). Sin SDK.</summary>
    public sealed class PedidoDeTransicion
    {
        public EstadoDeLaFila Desde { get; set; }

        public EstadoDeLaFila Hacia { get; set; }

        public Actor Actor { get; set; }

        /// <summary>`sanic_mensaje`: lo que va a LEER EL CLIENTE en su correo. Obligatorio en rechazar y anular.</summary>
        public string Mensaje { get; set; }

        /// <summary>
        /// `sanic_notainterna`: lo que el supervisor le escribe al ejecutivo al devolver una fila. Obligatorio
        /// en devolver, y NUNCA sale en el correo al cliente.
        ///
        /// Existe desde el 2026-09-24 porque hasta entonces la devolución escribía en `sanic_mensaje`, que es
        /// la columna que el correo muestra como "Motivos". Consecuencia real, vista en correos ya enviados:
        /// una fila que terminó APROBADA le llegó al cliente con el motivo "Devuelta". Una nota de trabajo
        /// entre dos personas del banco no es asunto del cliente.
        /// </summary>
        public string NotaInterna { get; set; }

        /// <summary>`sanic_digitadapor` de la fila ANTES del cambio. Decide la segregación de funciones al aprobar.</summary>
        public Guid? DigitadaPor { get; set; }

        /// <summary>Parámetro `rpa.puedeaprobar` (por defecto, no).</summary>
        public bool RpaPuedeAprobar { get; set; }

        /// <summary>
        /// `sanic_claimhasta` de la fila ANTES de esta transición (diseno/03 §2, "El claim: reserva con
        /// vencimiento"). `null` = nunca se reservó = libre. El claim frena a las PERSONAS, nunca al bot.
        /// </summary>
        public DateTime? ClaimHasta { get; set; }

        /// <summary>El "ahora" del pipeline (mismo instante que <c>PlomeriaDeFila.AhoraUtc</c>), para decidir con <see cref="ReglasDelRpa.ClaimVigente"/> si el claim sigue vigente.</summary>
        public DateTime Ahora { get; set; }

        /// <summary>
        /// `true` si quien pide la transición es código de servidor (el bot, vía SYSTEM anidado o identidad de
        /// aplicación — <c>Steps.BaseDeStep.EsCodigoDeServidor</c>). El claim nunca frena a quien reporta como
        /// código de servidor (diseno/03 §2: "el claim NO frena al bot cuando reporta").
        /// </summary>
        public bool EsCodigoDeServidor { get; set; }
    }

    /// <summary>Lo que el step tiene que hacer con las columnas de quién y cuándo, si la transición se permite.</summary>
    public enum EfectoDeTransicion
    {
        Ninguno = 0,
        /// <summary>`digitadapor` = quien llama, `fechadigitada` = ahora.</summary>
        RegistrarDigitacion = 1,
        /// <summary>`aprobadapor` = quien llama, `fechaaprobada` = ahora.</summary>
        RegistrarAprobacion = 2,
        /// <summary>Limpia `digitadapor` y `fechadigitada` (devolución).</summary>
        LimpiarDigitacion = 3,
    }

    public sealed class ResultadoDeTransicion
    {
        public ResultadoDeTransicion(bool permitida, string motivo, EfectoDeTransicion efecto, EventoDeBitacora? evento)
        {
            Permitida = permitida;
            Motivo = motivo;
            Efecto = efecto;
            Evento = evento;
        }

        public bool Permitida { get; }

        /// <summary>Por qué NO se permite, en texto para el usuario. `null` si se permite.</summary>
        public string Motivo { get; }

        public EfectoDeTransicion Efecto { get; }

        /// <summary>Evento a escribir en la Bitácora. `null` si no se permite.</summary>
        public EventoDeBitacora? Evento { get; }
    }

    /// <summary>La máquina de estados de la Fila. Función pura: no lee reloj, no toca Dataverse.</summary>
    public static class TransicionesDeFila
    {
        /// <summary>Texto exacto de diseno/03 §4 para todo lo que no está en la tabla.</summary>
        public const string NoPermitida = "Transición no permitida";

        private const string MensajeRolNoAutorizado = "El rol de quien llama no puede hacer esta transición.";
        private const string MensajeFaltaMensaje = "Esta transición exige un mensaje que explique el motivo.";
        private const string MensajeFaltaNota = "Devolver exige una nota que le explique al ejecutivo qué corregir.";
        private const string MensajeAmbosRoles = "Un usuario con los roles de Ejecutivo y de Supervisor no puede hacer ninguna transición (D-25).";
        private const string MensajeSinDigitador = "No se puede aprobar una fila sin saber quién la digitó.";
        private const string MensajeMismoDigitador = "Quien digitó la fila no puede aprobarla: falta la segregación de funciones.";

        /// <summary>
        /// El mensaje de rechazo cuando el claim está vigente (diseno/03 §2). Dice CUÁNTO FALTA, nunca UNA HORA
        /// (decisión del aprobador, 2026-09-25).
        ///
        /// Por qué. `Ahora` y `ClaimHasta` llegan siempre en UTC (el plugin los lee con `Retrieve` y con el reloj
        /// del contexto). Nicaragua es UTC-6: un claim que vence a las 15:30 UTC son las 09:30 en la pantalla del
        /// ejecutivo, y decirle "se libera a las 15:30" le miente por seis horas — le va a parecer que falta toda
        /// la tarde cuando en realidad falta un rato. El plugin NO tiene forma confiable de saber la zona horaria
        /// de quien lo lee (no hay sesión de usuario que consultar desde acá, y meter esa infraestructura para
        /// esto es desproporcionado), así que en vez de convertir una hora que puede estar mal, se evita mostrar
        /// una hora: la DURACIÓN restante es correcta sin importar en qué zona horaria esté quien la lee.
        ///
        /// Si alguien "mejora" esto a un timestamp más adelante, va a reintroducir el mismo bug: dejarlo escrito
        /// acá para que no haga falta redescubrirlo.
        /// </summary>
        private static string MensajeClaimVigente(PedidoDeTransicion pedido)
        {
            var restante = pedido.ClaimHasta.Value - pedido.Ahora;
            return $"La fila está reservada por el proceso automático; se libera {TextoDeCuantoFalta(restante)}.";
        }

        /// <summary>
        /// "En N minutos", redondeando SIEMPRE hacia arriba (nunca se le dice a alguien que ya se liberó cuando
        /// todavía falta una fracción), y nunca "0 minutos": una fila que dice "se libera en 0 minutos" y no se
        /// libera es exactamente el tipo de detalle que hace desconfiar de todo el sistema. Por debajo del minuto
        /// se dice "menos de un minuto" en vez de redondear a cero.
        /// </summary>
        private static string TextoDeCuantoFalta(TimeSpan restante)
        {
            if (restante.TotalMinutes < 1)
            {
                return "en menos de un minuto";
            }

            var minutos = (int)Math.Ceiling(restante.TotalMinutes);
            return minutos == 1 ? "en 1 minuto" : $"en {minutos} minutos";
        }

        /// <summary>Una fila de la tabla de diseno/03 §4: desde, hacia (la clave), quién autoriza, si exige mensaje, efecto y evento.</summary>
        /// <summary>Qué texto exige la transición, y por lo tanto QUIÉN lo va a leer.</summary>
        public enum TextoExigido
        {
            Ninguno = 0,
            /// <summary>`sanic_mensaje`: lo lee el cliente en su correo.</summary>
            MensajeAlCliente = 1,
            /// <summary>`sanic_notainterna`: lo lee el ejecutivo. No sale del banco.</summary>
            NotaInterna = 2,
        }

        private sealed class DefinicionDeTransicion
        {
            public TextoExigido Exige;

            public EfectoDeTransicion Efecto;

            public EventoDeBitacora Evento;

            /// <summary>Devuelve `null` si el actor puede hacer la transición, o el motivo por el que no.</summary>
            public Func<PedidoDeTransicion, string> Autorizar;
        }

        private static bool EsSoloEsteRol(RolDeActor roles, RolDeActor esperado) => roles == esperado;

        /// <summary>Digitar y Rechazar en AS400 los hace "quien digita": Ejecutivo, o el usuario de aplicación del RPA (03 §4).</summary>
        private static string AutorizarQuienDigita(PedidoDeTransicion p) =>
            EsSoloEsteRol(p.Actor.Roles, RolDeActor.Ejecutivo) || EsSoloEsteRol(p.Actor.Roles, RolDeActor.Rpa)
                ? null
                : MensajeRolNoAutorizado;

        /// <summary>Anular: "Solo Ejecutivo (...); el RPA nunca anula" (03 §4).</summary>
        private static string AutorizarAnular(PedidoDeTransicion p) =>
            EsSoloEsteRol(p.Actor.Roles, RolDeActor.Ejecutivo) ? null : MensajeRolNoAutorizado;

        /// <summary>Devolver: solo el Supervisor, con mensaje (03 §4, DD-07).</summary>
        private static string AutorizarDevolver(PedidoDeTransicion p) =>
            EsSoloEsteRol(p.Actor.Roles, RolDeActor.Supervisor) ? null : MensajeRolNoAutorizado;

        /// <summary>
        /// Aprobar: Supervisor distinto de quien digitó (segregación), o el RPA aprobando lo que él mismo
        /// digitó, solo si `rpa.puedeaprobar` lo habilita (03 §4, la excepción del RPA).
        /// </summary>
        private static string AutorizarAprobar(PedidoDeTransicion p)
        {
            if (EsSoloEsteRol(p.Actor.Roles, RolDeActor.Supervisor))
            {
                if (p.DigitadaPor == null)
                {
                    return MensajeSinDigitador;
                }

                return p.DigitadaPor == p.Actor.UsuarioId ? MensajeMismoDigitador : null;
            }

            if (EsSoloEsteRol(p.Actor.Roles, RolDeActor.Rpa))
            {
                return p.RpaPuedeAprobar && p.DigitadaPor == p.Actor.UsuarioId ? null : MensajeRolNoAutorizado;
            }

            return MensajeRolNoAutorizado;
        }

        /// <summary>La tabla De / A / Quién / Condición / Efecto de diseno/03 §4, una fila por transición.</summary>
        private static readonly Dictionary<(EstadoDeLaFila Desde, EstadoDeLaFila Hacia), DefinicionDeTransicion> Tabla =
            new Dictionary<(EstadoDeLaFila, EstadoDeLaFila), DefinicionDeTransicion>
            {
                [(EstadoDeLaFila.Validada, EstadoDeLaFila.Digitada)] = new DefinicionDeTransicion
                {
                    Exige = TextoExigido.Ninguno,
                    Efecto = EfectoDeTransicion.RegistrarDigitacion,
                    Evento = EventoDeBitacora.FilaDigitada,
                    Autorizar = AutorizarQuienDigita,
                },
                [(EstadoDeLaFila.Validada, EstadoDeLaFila.RechazadaEnAS400)] = new DefinicionDeTransicion
                {
                    Exige = TextoExigido.MensajeAlCliente,
                    Efecto = EfectoDeTransicion.Ninguno,
                    Evento = EventoDeBitacora.FilaRechazadaEnAS400,
                    Autorizar = AutorizarQuienDigita,
                },
                [(EstadoDeLaFila.Digitada, EstadoDeLaFila.RechazadaEnAS400)] = new DefinicionDeTransicion
                {
                    Exige = TextoExigido.MensajeAlCliente,
                    Efecto = EfectoDeTransicion.Ninguno,
                    Evento = EventoDeBitacora.FilaRechazadaEnAS400,
                    Autorizar = AutorizarQuienDigita,
                },
                [(EstadoDeLaFila.Validada, EstadoDeLaFila.Anulada)] = new DefinicionDeTransicion
                {
                    Exige = TextoExigido.MensajeAlCliente,
                    Efecto = EfectoDeTransicion.Ninguno,
                    Evento = EventoDeBitacora.FilaAnulada,
                    Autorizar = AutorizarAnular,
                },
                [(EstadoDeLaFila.Digitada, EstadoDeLaFila.Anulada)] = new DefinicionDeTransicion
                {
                    Exige = TextoExigido.MensajeAlCliente,
                    Efecto = EfectoDeTransicion.Ninguno,
                    Evento = EventoDeBitacora.FilaAnulada,
                    Autorizar = AutorizarAnular,
                },
                [(EstadoDeLaFila.Digitada, EstadoDeLaFila.Aprobada)] = new DefinicionDeTransicion
                {
                    Exige = TextoExigido.Ninguno,
                    Efecto = EfectoDeTransicion.RegistrarAprobacion,
                    Evento = EventoDeBitacora.FilaAprobada,
                    Autorizar = AutorizarAprobar,
                },
                [(EstadoDeLaFila.Digitada, EstadoDeLaFila.Validada)] = new DefinicionDeTransicion
                {
                    Exige = TextoExigido.NotaInterna,
                    Efecto = EfectoDeTransicion.LimpiarDigitacion,
                    Evento = EventoDeBitacora.FilaDevuelta,
                    Autorizar = AutorizarDevolver,
                },
            };

        /// <summary>
        /// El evento de Bitácora de esa transición, o nulo si no existe. Es un dato fijo del par (Desde, Hacia): no depende del
        /// actor ni de ningún parámetro, así que quien solo necesita el evento no tiene que resolver roles (revisión de código,
        /// 2026-09-21: el step PostOperation gastaba tres consultas para averiguarlo).
        /// </summary>
        public static EventoDeBitacora? EventoPara(EstadoDeLaFila desde, EstadoDeLaFila hacia) =>
            Tabla.TryGetValue((desde, hacia), out var definicion) ? (EventoDeBitacora?)definicion.Evento : null;

        public static ResultadoDeTransicion Evaluar(PedidoDeTransicion pedido)
        {
            if (pedido == null)
            {
                throw new ArgumentNullException(nameof(pedido));
            }

            if (pedido.Actor == null)
            {
                throw new ArgumentNullException(nameof(pedido.Actor));
            }

            if (!Tabla.TryGetValue((pedido.Desde, pedido.Hacia), out var definicion))
            {
                return new ResultadoDeTransicion(false, NoPermitida, EfectoDeTransicion.Ninguno, null);
            }

            // D-25 (04 §2): Ejecutivo y Supervisor son mutuamente excluyentes por usuario, para cualquier transición.
            // Va PRIMERO porque es una MALA CONFIGURACIÓN, y una mala configuración siempre gana: hay que
            // arreglarla ya, no esperar a que se destrabe sola (revisión del aprobador, 2026-09-25).
            if (pedido.Actor.Roles.HasFlag(RolDeActor.Ejecutivo) && pedido.Actor.Roles.HasFlag(RolDeActor.Supervisor))
            {
                return new ResultadoDeTransicion(false, MensajeAmbosRoles, EfectoDeTransicion.Ninguno, null);
            }

            // El claim (diseno/03 §2): mientras esté vigente, frena a cualquier PERSONA para CUALQUIER transición
            // de esta fila — no es una regla de una transición puntual, es un candado sobre la fila entera. Va
            // ACÁ, después de D-25 y antes de `Autorizar`, porque a diferencia de D-25 es un estado TEMPORAL: un
            // "no autorizado" genérico sería menos verdadero (y menos útil) que decir que la fila está, de hecho,
            // reservada y por cuánto más lo sigue estando. Nunca frena al código de servidor (el bot, que puede
            // reportar aunque el claim ya venció o siga vivo: `03` §2, "el claim NO frena al bot cuando reporta").
            if (!pedido.EsCodigoDeServidor && ReglasDelRpa.ClaimVigente(pedido.ClaimHasta, pedido.Ahora))
            {
                return new ResultadoDeTransicion(false, MensajeClaimVigente(pedido), EfectoDeTransicion.Ninguno, null);
            }

            var motivoDeRechazo = definicion.Autorizar(pedido);
            if (motivoDeRechazo != null)
            {
                return new ResultadoDeTransicion(false, motivoDeRechazo, EfectoDeTransicion.Ninguno, null);
            }

            if (definicion.Exige == TextoExigido.MensajeAlCliente && string.IsNullOrWhiteSpace(pedido.Mensaje))
            {
                return new ResultadoDeTransicion(false, MensajeFaltaMensaje, EfectoDeTransicion.Ninguno, null);
            }

            if (definicion.Exige == TextoExigido.NotaInterna && string.IsNullOrWhiteSpace(pedido.NotaInterna))
            {
                return new ResultadoDeTransicion(false, MensajeFaltaNota, EfectoDeTransicion.Ninguno, null);
            }

            return new ResultadoDeTransicion(true, null, definicion.Efecto, definicion.Evento);
        }

        /// <summary>Una fila "abierta" todavía espera trabajo en AS400: Validada o Digitada.</summary>
        public static bool EstaAbierta(EstadoDeLaFila estado) =>
            estado == EstadoDeLaFila.Validada || estado == EstadoDeLaFila.Digitada;

        /// <summary>DN-09 C: prefijo de la línea del texto que lee el cliente en el `sanic_detalle` de la Bitácora.</summary>
        public const string PrefijoMensajeAlCliente = "Mensaje al cliente: ";

        /// <summary>DN-09 C: prefijo de la línea de la nota interna en el `sanic_detalle` de la Bitácora.</summary>
        public const string PrefijoNotaInterna = "Nota interna: ";

        /// <summary>
        /// DN-09 C (diseno/DIRECTRICES-NUEVAS.md): el "por qué" de una transición, para el `sanic_detalle` de su Bitácora. La fila
        /// guarda el texto en `sanic_mensaje` / `sanic_notainterna`, y la acción siguiente lo pisa; la Bitácora es la que no se pisa.
        ///  - hacia **Rechazada en AS400** o **Anulada**: <see cref="PrefijoMensajeAlCliente"/> + mensaje y, si vino (el RPA manda
        ///    los dos), una segunda línea (`\n`) <see cref="PrefijoNotaInterna"/> + nota;
        ///  - hacia **Validada** (la devolución): solo <see cref="PrefijoNotaInterna"/> + nota; un mensaje que viniera se ignora;
        ///  - hacia **Digitada** o **Aprobada**: siempre `null` (no piden texto);
        ///  - los textos se recortan de espacios; uno vacío o solo espacios no genera línea; sin ninguna línea → `null`.
        /// Función pura. El largo lo recorta quien escribe la Bitácora (10000), no esta función.
        /// </summary>
        public static string DetalleDeBitacora(EstadoDeLaFila hacia, string mensaje, string notaInterna)
        {
            var mensajeLimpio = (mensaje ?? string.Empty).Trim();
            var notaLimpia = (notaInterna ?? string.Empty).Trim();

            switch (hacia)
            {
                case EstadoDeLaFila.RechazadaEnAS400:
                case EstadoDeLaFila.Anulada:
                    var lineas = new System.Collections.Generic.List<string>();
                    if (mensajeLimpio.Length > 0) lineas.Add(PrefijoMensajeAlCliente + mensajeLimpio);
                    if (notaLimpia.Length > 0) lineas.Add(PrefijoNotaInterna + notaLimpia);
                    return lineas.Count == 0 ? null : string.Join("\n", lineas);

                case EstadoDeLaFila.Validada:
                    return notaLimpia.Length == 0 ? null : PrefijoNotaInterna + notaLimpia;

                default:
                    return null;
            }
        }
    }
}
