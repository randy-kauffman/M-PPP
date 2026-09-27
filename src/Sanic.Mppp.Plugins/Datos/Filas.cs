using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Mppp.Plugins.Dominio;

namespace Sanic.Mppp.Plugins.Datos
{
    /// <summary>Una Fila candidata para el lote de `obtenerfilaspendientes` (diseno/03 §2), con todo lo necesario
    /// para decidir su pertenencia (<see cref="Dominio.SeleccionParaElRpa"/>) y para armar su JSON
    /// (<see cref="Dominio.ArmadorJsonDelRpa"/>) sin volver a leer Dataverse.</summary>
    public sealed class FilaPendienteLeida
    {
        public Guid FilaId { get; set; }

        public int NumeroFila { get; set; }

        public EstadoDeLaFila Estado { get; set; }

        /// <summary>`sanic_digitadapor`. `null` si todavía nadie.</summary>
        public Guid? DigitadaPor { get; set; }

        public bool RequiereAtencionManual { get; set; }

        public Guid SolicitudId { get; set; }

        /// <summary>`sanic_planid`. Debería venir siempre (una Validada/Digitada ya pasó `PLAN_EXISTE`), pero se
        /// trata como opcional: un dato corrupto no puede tumbar el lote entero de otras filas sanas.</summary>
        public Guid? PlanId { get; set; }

        /// <summary>`sanic_numeroplan`, ya normalizado (el código que rige, DD-10 no aplica acá).</summary>
        public string NumeroPlan { get; set; }

        public Gestion? Gestion { get; set; }

        public Clasificacion? Clasificacion { get; set; }

        public string NombreBeneficiario { get; set; }

        public TipoDeIdentificacion? TipoIdentificacion { get; set; }

        public string NumeroIdentificacion { get; set; }

        public string NumeroCuenta { get; set; }

        public Moneda? Moneda { get; set; }

        public Banco? Banco { get; set; }

        public string Referencia { get; set; }

        public string ReferenciaRecibida { get; set; }

        public DateTime FechaValidada { get; set; }
    }

    /// <summary>Lo mínimo de una Fila para decidir y aplicar un resultado de `registrarresultadorpa` (diseno/03 §3): su estado actual y quién la digitó.</summary>
    public sealed class FilaParaRegistro
    {
        public EstadoDeLaFila Estado { get; set; }

        public Guid? DigitadaPor { get; set; }

        public bool RequiereAtencionManual { get; set; }
    }

    /// <summary>
    /// Lectura y escritura de Fila para las dos Custom API del RPA (diseno/03 §2 y §3). Reglas (las fijan las
    /// pruebas `FilasDataverseAceptacion`):
    ///  - <see cref="Candidatas"/> filtra SOLO por `sanic_estado` (Validada o Digitada) y ordena FIFO
    ///    (`sanic_fechavalidada`, `sanic_numerofila`); la PERTENENCIA (atención manual, quién digitó, claim) la
    ///    decide el dominio (<see cref="Dominio.SeleccionParaElRpa"/>), no esta consulta — así ningún matiz de "qué
    ///    entra" queda escondido en un filtro de Dataverse que el motor en memoria no pueda ejercitar igual que la
    ///    plataforma real;
    ///  - todos los métodos de traducción/actualización graban con las mismas reglas de <see cref="SolicitudesDataverse"/>
    ///    (recorte de texto, columnas nulas no se mandan, fechas EXIGEN UTC);
    ///  - `RefrescarClaim` / `RegistrarDigitacion` / `RegistrarAprobacion` / `RegistrarRechazoEnAS400` /
    ///    `MarcarAtencionManual` son UN `Update` cada una; nunca se agrupan en lote (Microsoft Learn, "Don't use
    ///    batch request types in plug-ins": mismo criterio que <see cref="SolicitudesDataverse.GuardarFilas"/>);
    ///  - los estados terminales para el RPA (Aprobada, Rechazada en AS400) y la marca de atención manual LIBERAN
    ///    el claim (`sanic_claimhasta = null`); Digitada NO lo libera (diseno/03 §2, "Cuándo se libera").
    /// Servicio nulo: <see cref="ArgumentNullException"/>. Un `filaId` vacío es <see cref="ArgumentException"/>.
    /// </summary>
    public sealed class FilasDataverse
    {
        private static readonly string[] ColumnasCandidatas =
        {
            "sanic_estado", "sanic_digitadapor", "sanic_requiereatencionmanual", "sanic_numerofila",
            "sanic_fechavalidada", "sanic_solicitudid", "sanic_planid", "sanic_numeroplan", "sanic_gestion",
            "sanic_clasificacion", "sanic_nombrebeneficiario", "sanic_tipoidentificacion", "sanic_numeroidentificacion",
            "sanic_numerocuenta", "sanic_moneda", "sanic_banco", "sanic_referencia", "sanic_referenciarecibida",
        };

        private readonly IOrganizationService _servicio;

        public FilasDataverse(IOrganizationService servicio)
        {
            _servicio = servicio ?? throw new ArgumentNullException(nameof(servicio));
        }

        /// <summary>
        /// Las Filas en estado Validada o Digitada, en orden FIFO (`sanic_fechavalidada`, `sanic_numerofila`
        /// ascendente). UNA consulta. La pertenencia (atención manual, quién digitó, claim) no se filtra acá: la
        /// decide <see cref="Dominio.SeleccionParaElRpa"/> sobre lo que esto devuelve.
        /// </summary>
        public IList<FilaPendienteLeida> Candidatas()
        {
            var consulta = new QueryExpression(TablasHistorico.Fila) { ColumnSet = new ColumnSet(ColumnasCandidatas) };
            consulta.Criteria.AddCondition("sanic_estado", ConditionOperator.In, (int)EstadoDeLaFila.Validada, (int)EstadoDeLaFila.Digitada);
            consulta.AddOrder("sanic_fechavalidada", OrderType.Ascending);
            consulta.AddOrder("sanic_numerofila", OrderType.Ascending);

            return _servicio.RetrieveMultiple(consulta).Entities.Select(Convertir).ToList();
        }

        /// <summary>
        /// Las Filas pedidas por id, para `sanic_mppp_capi_exportarparadigitar` (DN-02, diseno/DIRECTRICES-NUEVAS.md).
        /// UNA sola consulta con `In`, SIN filtrar por `sanic_estado`: a diferencia de <see cref="Candidatas"/>, acá
        /// quién puede pedir qué fila lo decide quien llama (una persona, desde la app), no este método. Un
        /// `filaId` que no existe simplemente NO vuelve en la lista — nunca una excepción de plataforma atrapada
        /// para seguir procesando el resto (mismo criterio que <see cref="LeerParaRegistro"/>, documentado ahí con
        /// el incidente real del 2026-09-25: "ISV code reduced the open transaction count..."). El ORDEN de lo que
        /// devuelve NO está garantizado: si el orden importa, lo decide quien llama sobre <paramref name="filaIds"/>.
        /// `filaIds` vacío → sin consulta, lista vacía.
        /// </summary>
        public IList<FilaPendienteLeida> PorIds(IEnumerable<Guid> filaIds)
        {
            throw new NotImplementedException("DN-02: lo implementa el constructor contra FilasDataverseAceptacion, Etapa 2.");
        }

        /// <summary>El tipo de formato de esos planes, por id, SIN filtrar por activo: una Fila ya validada tiene que
        /// poder reportarse al bot aunque el Plan se haya desactivado después (diseno/03 §2). UNA consulta con `In`;
        /// vacío → sin consulta y diccionario vacío.</summary>
        public IDictionary<Guid, TipoDeFormatoDelPlan> TiposDeFormato(IEnumerable<Guid> planIds)
        {
            if (planIds == null)
            {
                throw new ArgumentNullException(nameof(planIds));
            }

            var ids = planIds.Distinct().ToList();
            if (ids.Count == 0)
            {
                return new Dictionary<Guid, TipoDeFormatoDelPlan>();
            }

            var consulta = new QueryExpression(Tablas.Plan) { ColumnSet = new ColumnSet("sanic_tipoformato") };
            consulta.Criteria.AddCondition(Tablas.Plan + "id", ConditionOperator.In, ids.Cast<object>().ToArray());

            return _servicio.RetrieveMultiple(consulta).Entities
                .ToDictionary(e => e.Id, e => (TipoDeFormatoDelPlan)e.GetAttributeValue<OptionSetValue>("sanic_tipoformato").Value);
        }

        /// <summary>El `sanic_nombre` (número) de esas Solicitudes, por id. UNA consulta con `In`; vacío → sin consulta.</summary>
        public IDictionary<Guid, string> NumerosDeSolicitud(IEnumerable<Guid> solicitudIds)
        {
            if (solicitudIds == null)
            {
                throw new ArgumentNullException(nameof(solicitudIds));
            }

            var ids = solicitudIds.Distinct().ToList();
            if (ids.Count == 0)
            {
                return new Dictionary<Guid, string>();
            }

            var consulta = new QueryExpression(TablasHistorico.Solicitud) { ColumnSet = new ColumnSet("sanic_nombre") };
            consulta.Criteria.AddCondition(TablasHistorico.Solicitud + "id", ConditionOperator.In, ids.Cast<object>().ToArray());

            return _servicio.RetrieveMultiple(consulta).Entities
                .ToDictionary(e => e.Id, e => e.GetAttributeValue<string>("sanic_nombre"));
        }

        /// <summary>Pone o refresca `sanic_claimhasta` (diseno/03 §2, "El claim"). UN `Update`.</summary>
        public void RefrescarClaim(Guid filaId, DateTime claimHasta)
        {
            ValidarFilaId(filaId);
            ValidarUtc(claimHasta, nameof(claimHasta));

            _servicio.Update(new Entity(TablasHistorico.Fila, filaId) { ["sanic_claimhasta"] = claimHasta });
        }

        private static readonly string[] ColumnasParaRegistro = { "sanic_estado", "sanic_digitadapor", "sanic_requiereatencionmanual" };

        /// <summary>
        /// Lo mínimo de la Fila para decidir un resultado de `registrarresultadorpa`. Un `filaId` que no existe
        /// devuelve `null`, NUNCA propaga una falla del servicio: usa `RetrieveMultiple` con `QueryExpression`
        /// filtrando por id (una consulta que no encuentra nada devuelve vacío) en vez de `Retrieve` por id (que
        /// TIRA `FaultException` si el id no existe).
        ///
        /// Hasta el 2026-09-25 este método usaba `Retrieve` por id y SÍ propagaba esa excepción — era una decisión
        /// implícita, no explícita: era simplemente lo que hacía `Retrieve`. Ese día, `herramientas/pruebas/probar_rpa.py
        /// --confirmar` contra el ENTORNO REAL (no las pruebas: <see cref="OrganizationServiceEnMemoria"/> no simula
        /// el modelo transaccional de Dataverse) mandó un lote de 3 ítems con uno de `filaid` inexistente y la
        /// plataforma tumbó LA LLAMADA ENTERA con "ISV code reduced the open transaction count. Custom plug-ins
        /// should not catch exceptions from OrganizationService calls and continue processing." — porque
        /// `RegistrarResultadoRpa` atrapaba esa excepción para seguir con el resto del lote, y atrapar una excepción
        /// de `OrganizationService` DENTRO de la transacción del plugin corrompe el contador que la plataforma ya
        /// decrementó. Un `filaid` que no existe es un DATO MALO del bot, no una condición excepcional de
        /// plataforma: se decide con una lectura que puede no encontrar nada, no con una excepción que hay que
        /// atrapar. Por eso cambió a `RetrieveMultiple` — que NUNCA tira por esto — y NUNCA debería volver a
        /// `Retrieve` por id así quede "más simple": eso reabriría el mismo defecto.
        /// </summary>
        public FilaParaRegistro LeerParaRegistro(Guid filaId)
        {
            ValidarFilaId(filaId);

            var consulta = new QueryExpression(TablasHistorico.Fila) { ColumnSet = new ColumnSet(ColumnasParaRegistro) };
            consulta.Criteria.AddCondition(TablasHistorico.Fila + "id", ConditionOperator.Equal, filaId);

            var registro = _servicio.RetrieveMultiple(consulta).Entities.FirstOrDefault();
            return registro == null ? null : ConvertirParaRegistro(registro);
        }

        private static FilaParaRegistro ConvertirParaRegistro(Entity registro)
        {
            return new FilaParaRegistro
            {
                Estado = (EstadoDeLaFila)registro.GetAttributeValue<OptionSetValue>("sanic_estado").Value,
                DigitadaPor = registro.GetAttributeValue<EntityReference>("sanic_digitadapor")?.Id,
                RequiereAtencionManual = registro.GetAttributeValue<bool>("sanic_requiereatencionmanual"),
            };
        }

        /// <summary>`Validada -> Digitada` (diseno/03 §3). NO libera el claim: al bot le queda trabajo sobre la fila.</summary>
        public void RegistrarDigitacion(Guid filaId, Guid usuarioDelRpa, DateTime fechaProcesoUtc)
        {
            ValidarFilaId(filaId);
            ValidarUtc(fechaProcesoUtc, nameof(fechaProcesoUtc));

            var entidad = new Entity(TablasHistorico.Fila, filaId)
            {
                ["sanic_estado"] = new OptionSetValue((int)EstadoDeLaFila.Digitada),
                ["sanic_digitadapor"] = new EntityReference(TablasNativas.Usuario, usuarioDelRpa),
                ["sanic_fechadigitada"] = fechaProcesoUtc,
            };
            _servicio.Update(entidad);
        }

        /// <summary>`Digitada -> Aprobada` (diseno/03 §3). Estado terminal: libera el claim.</summary>
        public void RegistrarAprobacion(Guid filaId, Guid usuarioDelRpa, DateTime fechaProcesoUtc)
        {
            ValidarFilaId(filaId);
            ValidarUtc(fechaProcesoUtc, nameof(fechaProcesoUtc));

            var entidad = new Entity(TablasHistorico.Fila, filaId)
            {
                ["sanic_estado"] = new OptionSetValue((int)EstadoDeLaFila.Aprobada),
                ["sanic_aprobadapor"] = new EntityReference(TablasNativas.Usuario, usuarioDelRpa),
                ["sanic_fechaaprobada"] = fechaProcesoUtc,
                ["sanic_claimhasta"] = null,
            };
            _servicio.Update(entidad);
        }

        /// <summary>`Validada|Digitada -> Rechazada en AS400` (diseno/03 §3). Estado terminal: libera el claim.
        /// `mensajeCliente` es el texto YA TRADUCIDO del catálogo `rpa.codigos` (`sanic_mensaje`); `notaInterna` es
        /// el texto crudo de AS400 que mandó el bot (`sanic_notainterna`, nunca sale del banco).</summary>
        public void RegistrarRechazoEnAS400(Guid filaId, string mensajeCliente, string notaInterna)
        {
            ValidarFilaId(filaId);

            var entidad = new Entity(TablasHistorico.Fila, filaId)
            {
                ["sanic_estado"] = new OptionSetValue((int)EstadoDeLaFila.RechazadaEnAS400),
                ["sanic_mensaje"] = Recortar(mensajeCliente, TablasHistorico.LargoMensaje),
                ["sanic_notainterna"] = notaInterna,
                ["sanic_claimhasta"] = null,
            };
            _servicio.Update(entidad);
        }

        /// <summary>Prende `sanic_requiereatencionmanual`, deja el porqué en `sanic_notainterna` y libera el claim
        /// (diseno/03 §3, "Atención manual"). NO toca `sanic_estado`: la fila sigue siendo lo que era.</summary>
        public void MarcarAtencionManual(Guid filaId, string notaInterna)
        {
            ValidarFilaId(filaId);

            var entidad = new Entity(TablasHistorico.Fila, filaId)
            {
                ["sanic_requiereatencionmanual"] = true,
                ["sanic_notainterna"] = notaInterna,
                ["sanic_claimhasta"] = null,
            };
            _servicio.Update(entidad);
        }

        private static FilaPendienteLeida Convertir(Entity registro)
        {
            return new FilaPendienteLeida
            {
                FilaId = registro.Id,
                NumeroFila = registro.GetAttributeValue<int>("sanic_numerofila"),
                Estado = (EstadoDeLaFila)registro.GetAttributeValue<OptionSetValue>("sanic_estado").Value,
                DigitadaPor = registro.GetAttributeValue<EntityReference>("sanic_digitadapor")?.Id,
                RequiereAtencionManual = registro.GetAttributeValue<bool>("sanic_requiereatencionmanual"),
                SolicitudId = registro.GetAttributeValue<EntityReference>("sanic_solicitudid")?.Id ?? Guid.Empty,
                PlanId = registro.GetAttributeValue<EntityReference>("sanic_planid")?.Id,
                NumeroPlan = registro.GetAttributeValue<string>("sanic_numeroplan"),
                Gestion = (Gestion?)registro.GetAttributeValue<OptionSetValue>("sanic_gestion")?.Value,
                Clasificacion = (Clasificacion?)registro.GetAttributeValue<OptionSetValue>("sanic_clasificacion")?.Value,
                NombreBeneficiario = registro.GetAttributeValue<string>("sanic_nombrebeneficiario"),
                TipoIdentificacion = (TipoDeIdentificacion?)registro.GetAttributeValue<OptionSetValue>("sanic_tipoidentificacion")?.Value,
                NumeroIdentificacion = registro.GetAttributeValue<string>("sanic_numeroidentificacion"),
                NumeroCuenta = registro.GetAttributeValue<string>("sanic_numerocuenta"),
                Moneda = (Moneda?)registro.GetAttributeValue<OptionSetValue>("sanic_moneda")?.Value,
                Banco = (Banco?)registro.GetAttributeValue<OptionSetValue>("sanic_banco")?.Value,
                Referencia = registro.GetAttributeValue<string>("sanic_referencia"),
                ReferenciaRecibida = registro.GetAttributeValue<string>("sanic_referenciarecibida"),
                FechaValidada = registro.GetAttributeValue<DateTime>("sanic_fechavalidada"),
            };
        }

        private static void ValidarFilaId(Guid filaId)
        {
            if (filaId == Guid.Empty)
            {
                throw new ArgumentException("El id de la fila no puede estar vacío.", nameof(filaId));
            }
        }

        private static void ValidarUtc(DateTime fecha, string nombreParametro)
        {
            if (fecha.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("La fecha tiene que venir en UTC (Kind = DateTimeKind.Utc); otro Kind se guardaría corrido en una columna de hora local.", nombreParametro);
            }
        }

        private static string Recortar(string texto, int largoMaximo)
        {
            if (texto == null || texto.Length <= largoMaximo)
            {
                return texto;
            }

            var largo = largoMaximo;
            if (largo > 0 && char.IsHighSurrogate(texto[largo - 1]))
            {
                largo--;
            }

            return texto.Substring(0, largo);
        }
    }
}
