using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using Sanic.Mppp.Plugins.Datos;
using Sanic.Mppp.Plugins.Dominio;
using Sanic.Mppp.Plugins.Plantilla;

namespace Sanic.Mppp.Plugins.Api
{
    /// <summary>Un ítem del lote de `registrarresultadorpa`, ya resuelto, listo para escribir.</summary>
    public sealed class ItemRegistrado
    {
        public ItemRegistrado(Guid? filaId, string estadoFila, bool yaRegistrado, string error)
        {
            FilaId = filaId;
            EstadoFila = estadoFila;
            YaRegistrado = yaRegistrado;
            Error = error;
        }

        public Guid? FilaId { get; }

        /// <summary>`null` SOLO cuando `filaid` ni siquiera resolvió a una Fila real (MPPP-RPA-001): ahí no hay estado que reportar.</summary>
        public string EstadoFila { get; }

        public bool YaRegistrado { get; }

        /// <summary>`null` si el ítem se procesó sin error.</summary>
        public string Error { get; }
    }

    /// <summary>Las salidas de `sanic_mppp_capi_registrarresultadorpa` (diseno/03 §3, "Se reporta por lote").</summary>
    public sealed class ResultadoDeRegistroRpa
    {
        public ResultadoDeRegistroRpa(string resultadosJson, int procesadas, int conError)
        {
            ResultadosJson = resultadosJson;
            Procesadas = procesadas;
            ConError = conError;
        }

        public string ResultadosJson { get; }

        public int Procesadas { get; }

        public int ConError { get; }
    }

    /// <summary>Un fallo de UN ítem del lote, con o sin código MPPP-RPA-0xx (diseno/03 §3). NUNCA se deja subir fuera
    /// del procesamiento de su propio ítem: "el plugin no puede tirar excepción por un ítem del lote".</summary>
    public sealed class ResultadoRpaException : Exception
    {
        public ResultadoRpaException(string codigo, string mensaje)
            : base(codigo == null ? mensaje : $"{codigo}: {mensaje}")
        {
            Codigo = codigo;
        }

        /// <summary>`null` cuando el motivo no tiene un código MPPP-RPA-0xx asignado en el contrato (por ejemplo, una
        /// transición que <see cref="TransicionesDeFila"/> rechaza por segregación de funciones o por
        /// `rpa.puedeaprobar` en `no`: diseno/03 §3 dice "devuelve error" pero no le asigna un código propio).</summary>
        public string Codigo { get; }
    }

    /// <summary>
    /// El plugin de `sanic_mppp_capi_registrarresultadorpa` (diseno/03 §3), sin el envoltorio `IPlugin` (que es
    /// <see cref="RegistrarResultadoRpaApi"/>). Contrato (lo fijan las pruebas `RegistrarResultadoRpaAceptacion`):
    ///  - el lote es de 1 a `rpa.lote.maximo` ítems (<see cref="ReglasDelRpa.ExigirLoteAceptable"/>); fuera de ese
    ///    rango se rechaza LA LLAMADA ENTERA (no es un ítem, es <see cref="ArgumentException"/> con
    ///    <see cref="ReglasDelRpa.CodigoLoteGrande"/> o <see cref="ReglasDelRpa.CodigoLoteVacio"/>);
    ///  - CADA ítem se procesa en su propio `try`, pero SOLO para un dato MAL CLASIFICADO por el bot (filaid
    ///    inexistente, resultado desconocido, fecha inválida, código no mapeado, transición no permitida): eso NUNCA
    ///    sale de acá como excepción, se atrapa y se anota en su `error` (<see cref="ResultadoRpaException"/>). Una
    ///    escritura que falla por un motivo REAL de plataforma (concurrencia, otro plugin, un límite) es harina de
    ///    otro costal: SÍ sale como excepción y tumba la llamada entera — nunca se atrapa un `catch (Exception)`
    ///    genérico ni un `try/catch` alrededor de una llamada a `OrganizationService` (defecto real, 2026-09-25:
    ///    ver el comentario grande en <c>ProcesarItem</c>);
    ///  - por ítem: `filaid` tiene que resolver a una Fila real (si no, `MPPP-RPA-001`); `resultado` tiene que ser
    ///    `digitada`, `aprobada`, `rechazada` o `atencionmanual` (si no, `MPPP-RPA-002` — el catálogo de errores del
    ///    contrato no lista `atencionmanual` entre los tres valores válidos de `resultado`, pero la tabla de
    ///    transiciones y el propio encargo de esta pieza lo exigen: se documenta como discrepancia del contrato, no
    ///    como decisión propia); `fechaproceso` exige offset explícito y ventana de tolerancia
    ///    (<see cref="ReglasDelRpa.ParsearFechaConOffset"/> + <see cref="ReglasDelRpa.ExigirFechaCreible"/>, código
    ///    <see cref="ReglasDelRpa.CodigoFecha"/>); `mensaje` es obligatorio en `rechazada` Y en `atencionmanual`
    ///    (`MPPP-RPA-004`: el contrato solo lo dice de manera explícita para `rechazada`, pero `atencionmanual`
    ///    también escribe `sanic_notainterna` y necesita contenido — mismo motivo, misma exigencia); `codigo` tiene
    ///    que estar en `rpa.codigos` (`MPPP-RPA-005`);
    ///  - la TRANSICIÓN concreta la decide <see cref="TransicionesDeFila"/> (la misma máquina de estados del step
    ///    de §4, reusada acá en proceso — no se reinventa una segunda): el actor es el rol Rpa, con
    ///    `RpaPuedeAprobar` = `rpa.puedeaprobar`. Si la fila YA está en el estado de destino, o en cualquier otro
    ///    (una persona actuó antes), NO es un error: `yaregistrado = true` y `estadofila` dice la verdad, sin
    ///    excepción ni escritura (diseno/03 §3, "El bot decide por estadofila, NUNCA por yaregistrado");
    ///  - `rechazada` escribe `sanic_mensaje` con el texto TRADUCIDO del `codigo` (lo lee el cliente) y
    ///    `sanic_notainterna` con el `mensaje` CRUDO del bot (nunca sale del banco: diseno/03 §3 "Los dos textos");
    ///  - `atencionmanual` es válido mientras la fila esté ABIERTA (<see cref="TransicionesDeFila.EstaAbierta"/>:
    ///    Validada o Digitada), no solo desde Validada como muestra literalmente la fila de la tabla de §3: se
    ///    interpreta así porque la marca es independiente del estado y restringirla a Validada dejaría sin forma de
    ///    reportar un problema no clasificable mientras el bot está APROBANDO — se documenta como interpretación,
    ///    no como algo que el contrato zanje;
    ///  - los estados terminales (Aprobada, Rechazada en AS400) y la marca de atención manual LIBERAN el claim.
    /// </summary>
    public sealed class RegistrarResultadoRpa
    {
        public const string ParametroCodigos = "rpa.codigos";
        public const string ParametroLoteMaximo = "rpa.lote.maximo";

        public const string CodigoFilaInexistente = "MPPP-RPA-001";
        public const string CodigoResultadoInvalido = "MPPP-RPA-002";
        public const string CodigoFaltaMensaje = "MPPP-RPA-004";
        public const string CodigoCodigoNoMapeado = "MPPP-RPA-005";

        private static readonly HashSet<string> ResultadosValidos =
            new HashSet<string>(StringComparer.Ordinal) { "digitada", "aprobada", "rechazada", "atencionmanual" };

        private readonly FilasDataverse _filas;
        private readonly CatalogosDataverse _catalogos;

        public RegistrarResultadoRpa(FilasDataverse filas, CatalogosDataverse catalogos)
        {
            _filas = filas ?? throw new ArgumentNullException(nameof(filas));
            _catalogos = catalogos ?? throw new ArgumentNullException(nameof(catalogos));
        }

        public ResultadoDeRegistroRpa Ejecutar(string loteJson, Guid quienLlama, DateTime ahoraUtc, int toleranciaHoras, bool rpaPuedeAprobar)
        {
            if (quienLlama == Guid.Empty)
            {
                throw new ArgumentException("El id de quien llama no puede estar vacío.", nameof(quienLlama));
            }

            if (ahoraUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("La fecha tiene que venir en UTC (Kind = DateTimeKind.Utc).", nameof(ahoraUtc));
            }

            var crudos = ParsearLote(loteJson);

            var loteMaximo = ParametroEnteroObligatorio(ParametroLoteMaximo);
            ReglasDelRpa.ExigirLoteAceptable(crudos.Count, loteMaximo);

            var codigos = CatalogoDeCodigosRpa.DesdeJson(ParametroObligatorio(ParametroCodigos).Valor);

            var items = new List<ItemRegistrado>(crudos.Count);
            foreach (var crudo in crudos)
            {
                items.Add(ProcesarItem(crudo, codigos, quienLlama, ahoraUtc, toleranciaHoras, rpaPuedeAprobar));
            }

            var conError = items.Count(i => i.Error != null);
            var json = SerializarResultados(items);
            return new ResultadoDeRegistroRpa(json, items.Count - conError, conError);
        }

        /// <summary>
        /// Procesa UN ítem. Un dato MAL CLASIFICADO por el bot (filaid inexistente, resultado desconocido, fecha
        /// sin offset o fuera de tolerancia, código no mapeado, transición no permitida) se decide con funciones
        /// PURAS antes de escribir nada — <see cref="ReglasDelRpa"/>, <see cref="CatalogoDeCodigosRpa"/>,
        /// <see cref="TransicionesDeFila.Evaluar"/> — y ACÁ se atrapa, pero SOLO como
        /// <see cref="ResultadoRpaException"/>: nunca tumba el lote, se reporta en el `error` de ese ítem
        /// (diseno/03 §3, "Se reporta por lote").
        ///
        /// NUNCA agregues acá un `catch (Exception)` genérico, ni un `try/catch` alrededor de NINGUNA llamada a
        /// `OrganizationService` (`_filas.Registrar*`, `_filas.BuscarParaRegistro`, etc.), aunque parezca "más
        /// robusto". El 2026-09-25, `herramientas/pruebas/probar_rpa.py --confirmar` contra el ENTORNO REAL (no las
        /// pruebas: el doble no simula el modelo transaccional de Dataverse) mandó un lote de 3 ítems y la
        /// plataforma rechazó LA LLAMADA ENTERA con:
        ///
        ///   "ISV code reduced the open transaction count. Custom plug-ins should not catch exceptions from
        ///   OrganizationService calls and continue processing."
        ///
        /// Causa: cuando una llamada a `OrganizationService` falla DENTRO de la transacción del plugin, la
        /// plataforma YA decrementó el contador de transacción; atraparla y seguir corrompe ese estado y voltea la
        /// llamada completa. Antes de este arreglo, este mismo método tenía un `catch (Exception)` que hacía
        /// exactamente eso.
        ///
        /// La asimetría es a propósito, no una inconsistencia: un ítem mal clasificado es un DATO MALO del bot y se
        /// responde por ítem; una escritura que FALLA de verdad (concurrencia, otro plugin, un límite de la
        /// plataforma) es una condición EXCEPCIONAL de verdad y tiene que tumbar la llamada entera — taparla
        /// dejaría a Dataverse mintiendo sobre lo que en realidad pasó en AS400. Por eso esta excepción se deja
        /// propagar tal cual, sin atraparla.
        /// </summary>
        private ItemRegistrado ProcesarItem(ItemCrudoDto crudo, CatalogoDeCodigosRpa codigos, Guid quienLlama, DateTime ahoraUtc, int toleranciaHoras, bool rpaPuedeAprobar)
        {
            Guid? filaId = null;
            FilaParaRegistro actual = null;

            try
            {
                filaId = LeerFilaId(crudo);
                actual = ExigirFilaExistente(filaId.Value);

                var resultado = LeerResultado(crudo);
                var fechaProcesoUtc = LeerFechaProceso(crudo, ahoraUtc, toleranciaHoras);
                ExigirMensajeSiHaceFalta(resultado, crudo.Mensaje);
                var codigoResuelto = ResolverCodigo(codigos, crudo.Codigo);

                switch (resultado)
                {
                    case "digitada":
                        return AplicarDigitada(filaId.Value, actual, quienLlama, fechaProcesoUtc);
                    case "aprobada":
                        return AplicarAprobada(filaId.Value, actual, quienLlama, fechaProcesoUtc, rpaPuedeAprobar);
                    case "rechazada":
                        return AplicarRechazada(filaId.Value, actual, quienLlama, codigoResuelto, crudo.Mensaje);
                    default: // "atencionmanual", ya validado arriba
                        return AplicarAtencionManual(filaId.Value, actual, crudo.Mensaje);
                }
            }
            catch (ResultadoRpaException ex)
            {
                return new ItemRegistrado(filaId, actual != null ? TextoDeEstado(actual.Estado) : null, true, ex.Message);
            }
        }

        /// <summary>`ReglasDelRpa.ParsearFechaConOffset`/`ExigirFechaCreible` tiran `ArgumentException` con el código
        /// YA incluido en el mensaje (`MPPP-RPA-003: ...`): acá se traduce a <see cref="ResultadoRpaException"/> sin
        /// duplicar el prefijo (por eso el código va `null`, no <see cref="ReglasDelRpa.CodigoFecha"/> otra vez).</summary>
        private static DateTime LeerFechaProceso(ItemCrudoDto crudo, DateTime ahoraUtc, int toleranciaHoras)
        {
            try
            {
                var fecha = ReglasDelRpa.ParsearFechaConOffset(crudo.FechaProceso);
                ReglasDelRpa.ExigirFechaCreible(fecha, ahoraUtc, toleranciaHoras);
                return fecha;
            }
            catch (ArgumentException ex)
            {
                throw new ResultadoRpaException(null, ex.Message);
            }
        }

        private Guid LeerFilaId(ItemCrudoDto crudo)
        {
            if (string.IsNullOrWhiteSpace(crudo.FilaId) || !Guid.TryParse(crudo.FilaId, out var filaId) || filaId == Guid.Empty)
            {
                throw new ResultadoRpaException(CodigoFilaInexistente, "El identificador de la fila no es válido.");
            }

            return filaId;
        }

        /// <summary>
        /// `filaid` inexistente es un DATO MALO del bot, no una falla del servicio: por eso usa
        /// <see cref="FilasDataverse.BuscarParaRegistro"/> (una lectura que devuelve `null` cuando no encuentra
        /// nada) en vez de <see cref="FilasDataverse.LeerParaRegistro"/> (que propaga la falla del servicio tal
        /// cual). Acá NO hay ningún `try/catch` alrededor de la llamada a `OrganizationService` — ver el porqué en
        /// el comentario grande de <see cref="ProcesarItem"/>.
        /// </summary>
        private FilaParaRegistro ExigirFilaExistente(Guid filaId)
        {
            var actual = _filas.BuscarParaRegistro(filaId);
            if (actual == null)
            {
                throw new ResultadoRpaException(CodigoFilaInexistente, "La fila no existe.");
            }

            return actual;
        }

        private static string LeerResultado(ItemCrudoDto crudo)
        {
            var resultado = crudo.Resultado?.Trim();
            if (resultado == null || !ResultadosValidos.Contains(resultado))
            {
                throw new ResultadoRpaException(CodigoResultadoInvalido, $"El resultado '{crudo.Resultado}' no es válido.");
            }

            return resultado;
        }

        private static void ExigirMensajeSiHaceFalta(string resultado, string mensaje)
        {
            var requiereMensaje = resultado == "rechazada" || resultado == "atencionmanual";
            if (requiereMensaje && string.IsNullOrWhiteSpace(mensaje))
            {
                throw new ResultadoRpaException(CodigoFaltaMensaje, $"Falta 'mensaje', obligatorio cuando el resultado es '{resultado}'.");
            }
        }

        private static CodigoRpa ResolverCodigo(CatalogoDeCodigosRpa codigos, string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo))
            {
                throw new ResultadoRpaException(CodigoCodigoNoMapeado, "Falta 'codigo'.");
            }

            var resuelto = codigos.Resolver(codigo);
            if (resuelto == null)
            {
                throw new ResultadoRpaException(CodigoCodigoNoMapeado, $"El código '{codigo}' no está en el catálogo 'rpa.codigos'.");
            }

            return resuelto;
        }

        private ItemRegistrado AplicarDigitada(Guid filaId, FilaParaRegistro actual, Guid quienLlama, DateTime fechaProcesoUtc)
        {
            if (actual.Estado == EstadoDeLaFila.Digitada)
            {
                return SinCambios(filaId, actual.Estado);
            }

            if (actual.Estado != EstadoDeLaFila.Validada)
            {
                return SinCambios(filaId, actual.Estado);
            }

            EvaluarOFallar(EstadoDeLaFila.Validada, EstadoDeLaFila.Digitada, quienLlama, actual.DigitadaPor, mensajeAlCliente: null, rpaPuedeAprobar: false);
            _filas.RegistrarDigitacion(filaId, quienLlama, fechaProcesoUtc);
            return new ItemRegistrado(filaId, TextoDeEstado(EstadoDeLaFila.Digitada), false, null);
        }

        private ItemRegistrado AplicarAprobada(Guid filaId, FilaParaRegistro actual, Guid quienLlama, DateTime fechaProcesoUtc, bool rpaPuedeAprobar)
        {
            if (actual.Estado == EstadoDeLaFila.Aprobada)
            {
                return SinCambios(filaId, actual.Estado);
            }

            if (actual.Estado != EstadoDeLaFila.Digitada)
            {
                return SinCambios(filaId, actual.Estado);
            }

            EvaluarOFallar(EstadoDeLaFila.Digitada, EstadoDeLaFila.Aprobada, quienLlama, actual.DigitadaPor, mensajeAlCliente: null, rpaPuedeAprobar: rpaPuedeAprobar);
            _filas.RegistrarAprobacion(filaId, quienLlama, fechaProcesoUtc);
            return new ItemRegistrado(filaId, TextoDeEstado(EstadoDeLaFila.Aprobada), false, null);
        }

        private ItemRegistrado AplicarRechazada(Guid filaId, FilaParaRegistro actual, Guid quienLlama, CodigoRpa codigoResuelto, string mensajeCrudo)
        {
            if (actual.Estado == EstadoDeLaFila.RechazadaEnAS400)
            {
                return SinCambios(filaId, actual.Estado);
            }

            if (actual.Estado != EstadoDeLaFila.Validada && actual.Estado != EstadoDeLaFila.Digitada)
            {
                return SinCambios(filaId, actual.Estado);
            }

            EvaluarOFallar(actual.Estado, EstadoDeLaFila.RechazadaEnAS400, quienLlama, actual.DigitadaPor, mensajeAlCliente: codigoResuelto.Cliente, rpaPuedeAprobar: false);
            _filas.RegistrarRechazoEnAS400(filaId, codigoResuelto.Cliente, mensajeCrudo);
            return new ItemRegistrado(filaId, TextoDeEstado(EstadoDeLaFila.RechazadaEnAS400), false, null);
        }

        private ItemRegistrado AplicarAtencionManual(Guid filaId, FilaParaRegistro actual, string mensajeCrudo)
        {
            if (!TransicionesDeFila.EstaAbierta(actual.Estado))
            {
                return SinCambios(filaId, actual.Estado);
            }

            if (actual.RequiereAtencionManual)
            {
                return SinCambios(filaId, actual.Estado);
            }

            _filas.MarcarAtencionManual(filaId, mensajeCrudo);
            return new ItemRegistrado(filaId, TextoDeEstado(actual.Estado), false, null);
        }

        /// <summary>
        /// Corre <see cref="TransicionesDeFila.Evaluar"/> (la MISMA máquina del step de §4) para la transición
        /// puntual del RPA; si no la permite, la convierte en <see cref="ResultadoRpaException"/> SIN código (el
        /// contrato no le asigna uno a un rechazo de segregación o de `rpa.puedeaprobar`).
        /// </summary>
        private static void EvaluarOFallar(
            EstadoDeLaFila desde, EstadoDeLaFila hacia, Guid quienLlama, Guid? digitadaPor, string mensajeAlCliente, bool rpaPuedeAprobar)
        {
            var pedido = new PedidoDeTransicion
            {
                Desde = desde,
                Hacia = hacia,
                Actor = new Actor(quienLlama, RolDeActor.Rpa),
                Mensaje = mensajeAlCliente,
                DigitadaPor = digitadaPor,
                RpaPuedeAprobar = rpaPuedeAprobar,
            };

            var resultado = TransicionesDeFila.Evaluar(pedido);
            if (!resultado.Permitida)
            {
                throw new ResultadoRpaException(null, resultado.Motivo);
            }
        }

        private static ItemRegistrado SinCambios(Guid filaId, EstadoDeLaFila estadoActual) =>
            new ItemRegistrado(filaId, TextoDeEstado(estadoActual), true, null);

        /// <summary>El estado, tal como lo lee el bot: minúsculas, sin separadores (diseno/03 §3).</summary>
        private static string TextoDeEstado(EstadoDeLaFila estado)
        {
            switch (estado)
            {
                case EstadoDeLaFila.Validada: return "validada";
                case EstadoDeLaFila.Digitada: return "digitada";
                case EstadoDeLaFila.Aprobada: return "aprobada";
                case EstadoDeLaFila.RechazadaEnAS400: return "rechazadaas400";
                case EstadoDeLaFila.Anulada: return "anulada";
                case EstadoDeLaFila.RechazadaEnValidacion: return "rechazadaenvalidacion";
                case EstadoDeLaFila.SinAutorizacion: return "sinautorizacion";
                default:
                    throw new ArgumentOutOfRangeException(nameof(estado), estado, "Estado de fila desconocido.");
            }
        }

        private static IList<ItemCrudoDto> ParsearLote(string loteJson)
        {
            if (string.IsNullOrWhiteSpace(loteJson))
            {
                return new List<ItemCrudoDto>();
            }

            try
            {
                return LimitesLectura.DeserializarJson<List<ItemCrudoDto>>(loteJson) ?? new List<ItemCrudoDto>();
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException || ex is StackOverflowException || ex is System.Threading.ThreadAbortException))
            {
                throw new FormatException($"El lote de resultados no es un JSON válido: {ex.Message}", ex);
            }
        }

        private ParametroLeido ParametroObligatorio(string nombre)
        {
            var parametro = _catalogos.Parametro(nombre);
            if (parametro == null)
            {
                throw new InvalidOperationException($"Falta el parámetro '{nombre}', necesario para registrar resultados del RPA.");
            }

            return parametro;
        }

        private int ParametroEnteroObligatorio(string nombre)
        {
            var parametro = ParametroObligatorio(nombre);
            if (!int.TryParse(parametro.Valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor) || valor <= 0)
            {
                throw new InvalidOperationException($"El parámetro '{nombre}' tiene que ser un entero positivo.");
            }

            return valor;
        }

        private static string SerializarResultados(IList<ItemRegistrado> items)
        {
            var sb = new StringBuilder();
            sb.Append('[');
            for (var i = 0; i < items.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                EscribirItem(sb, items[i]);
            }

            sb.Append(']');
            return sb.ToString();
        }

        private static void EscribirItem(StringBuilder sb, ItemRegistrado item)
        {
            sb.Append('{');
            sb.Append("\"filaid\":");
            sb.Append(item.FilaId.HasValue ? $"\"{item.FilaId.Value}\"" : "null");

            sb.Append(",\"estadofila\":");
            sb.Append(item.EstadoFila != null ? $"\"{EscaparJson(item.EstadoFila)}\"" : "null");

            sb.Append(",\"yaregistrado\":").Append(item.YaRegistrado ? "true" : "false");

            if (item.Error != null)
            {
                sb.Append(",\"error\":\"").Append(EscaparJson(item.Error)).Append('"');
            }

            sb.Append('}');
        }

        private static string EscaparJson(string valor)
        {
            var sb = new StringBuilder(valor.Length);
            foreach (var c in valor)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }

                        break;
                }
            }

            return sb.ToString();
        }

        [DataContract]
        internal sealed class ItemCrudoDto
        {
            [DataMember(Name = "filaid")]
            public string FilaId { get; set; }

            [DataMember(Name = "resultado")]
            public string Resultado { get; set; }

            [DataMember(Name = "mensaje")]
            public string Mensaje { get; set; }

            [DataMember(Name = "codigo")]
            public string Codigo { get; set; }

            [DataMember(Name = "fechaproceso")]
            public string FechaProceso { get; set; }
        }
    }
}
