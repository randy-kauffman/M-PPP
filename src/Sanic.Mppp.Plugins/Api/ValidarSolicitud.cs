using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Sanic.Mppp.Plugins.Datos;
using Sanic.Mppp.Plugins.Dominio;
using Sanic.Mppp.Plugins.Plantilla;
using Sanic.Mppp.Plugins.Respuesta;
using Sanic.Mppp.Plugins.Steps;
using Sanic.Mppp.Plugins.Validacion;

namespace Sanic.Mppp.Plugins.Api
{
    /// <summary>Las salidas de `sanic_mppp_capi_validarsolicitud` (diseno/03 §1).</summary>
    public sealed class ResultadoDeValidacion
    {
        public ResultadoDeValidacion(EstadoDeLaSolicitud estado, bool yaProcesada, int filasTotales, int filasValidas, int filasRechazadas, string resumen)
        {
            Estado = estado;
            YaProcesada = yaProcesada;
            FilasTotales = filasTotales;
            FilasValidas = filasValidas;
            FilasRechazadas = filasRechazadas;
            Resumen = resumen;
        }

        public EstadoDeLaSolicitud Estado { get; }

        public bool YaProcesada { get; }

        public int FilasTotales { get; }

        public int FilasValidas { get; }

        public int FilasRechazadas { get; }

        /// <summary>Una línea para el run history del flujo. Nunca trae datos del cliente.</summary>
        public string Resumen { get; }
    }

    /// <summary>
    /// El plugin grande: abre la plantilla y valida la Solicitud entera (diseno/03 §1), sin el envoltorio `IPlugin` (que es
    /// <see cref="ValidarSolicitudApi"/>). Contrato (lo fijan las pruebas `ValidarSolicitudAceptacion`):
    ///  1. lee la Solicitud; si no está en Ingresada → `YaProcesada`, con los contadores y el estado ya guardados, sin tocar nada;
    ///  2. carga UNA vez los parámetros `plantilla.estructura`, `plantilla.listas`, `plantilla.obligatoriedad`, `lectura.limites` y,
    ///     desde DN-05, `plan.formatos` y `cuenta.largos` (la versión activa más alta de cada uno); un parámetro que falta o está mal cargado es un error REAL, no una regla
    ///     fallida: sin él no se puede validar nada. `sanic_versionparametros` queda como `estructura=2;listas=3;obligatoriedad=1;formatos=1;largos=1`
    ///     (en ese orden, con la versión de cada uno; `lectura.limites` no va: no cambia cómo se valida, solo cuánto se lee);
    ///  3. evalúa las reglas de nivel Solicitud con <see cref="ReglasDelSobre"/> y guarda un `ResultadoRegla` por cada una. El
    ///     Excel se abre UNA sola vez y solo si alguna regla lo necesita (el sobre corta antes si no hay adjunto);
    ///  4. si el sobre no deja seguir (alguna Rechaza no cumplida, o una que Envía a revisión: D-39), NO se leen filas: la
    ///     Solicitud queda Rechazada (o No reconocida), con el acuse armado, los contadores en cero y la Bitácora;
    ///  5. si el sobre pasa, por cada fila de la plantilla evalúa las reglas de nivel Registro con <see cref="ReglasDeRegistro"/>,
    ///     arma su estado y su `sanic_mensaje`, y guarda las filas. Los catálogos se cargan UNA vez: los planes de todos los
    ///     números de plan de la plantilla en una consulta, y las autorizaciones del remitente aparte (diseno/03 §1 paso 6);
    ///  6. estado final: Rechazada si ninguna fila quedó Validada (DD-09), si no En proceso. Escribe el acuse
    ///     (<see cref="Respuesta.ArmadorRespuesta"/>), los contadores, `sanic_fechavalidada` y la Bitácora "Validación terminada";
    ///  7. ningún camino termina sin Bitácora, y en ninguno se le responde al cliente desde acá (eso es `MPPP-ENV`).
    /// Nada lee el reloj: la fecha llega por parámetro y tiene que ser UTC. Una excepción real se propaga tal cual.
    /// </summary>
    public sealed class ValidarSolicitud
    {
        public const string ParametroEstructura = "plantilla.estructura";
        public const string ParametroListas = "plantilla.listas";
        public const string ParametroObligatoriedad = "plantilla.obligatoriedad";
        public const string ParametroLimites = "lectura.limites";

        /// <summary>DN-05: obligatorios como los de arriba (falta → error real que nombra el parámetro). Entran en
        /// `sanic_versionparametros` como `;formatos={v};largos={v}`, después de los tres de hoy. FIRMA DEL ARQUITECTO.</summary>
        public const string ParametroFormatos = FormatosDelPlan.NombreParametro;
        public const string ParametroLargosDeCuenta = LargosDeCuenta.NombreParametro;

        /// <summary>`sanic_exceloriginal`, la columna de archivo de la que se lee la plantilla.</summary>
        public const string ColumnaExcel = "sanic_exceloriginal";

        private readonly SolicitudesDataverse _solicitudes;
        private readonly CatalogosDataverse _catalogos;
        private readonly IArchivos _archivos;
        private readonly Func<LimitesLectura, ILectorPlantilla> _lector;
        private readonly IAvisos _avisos;

        /// <summary>
        /// El lector llega como FÁBRICA, no ya armado: sus topes salen de `lectura.limites`, que recién se conoce adentro
        /// de <see cref="Ejecutar"/>. Armarlo afuera dejaría el tope de bytes descomprimidos (la defensa contra un zip
        /// bomba, LP-02) clavado en su valor por defecto, ignorando el parámetro.
        /// </summary>
        public ValidarSolicitud(SolicitudesDataverse solicitudes, CatalogosDataverse catalogos, IArchivos archivos, Func<LimitesLectura, ILectorPlantilla> lector, IAvisos avisos)
        {
            _solicitudes = solicitudes ?? throw new ArgumentNullException(nameof(solicitudes));
            _catalogos = catalogos ?? throw new ArgumentNullException(nameof(catalogos));
            _archivos = archivos ?? throw new ArgumentNullException(nameof(archivos));
            _lector = lector ?? throw new ArgumentNullException(nameof(lector));
            _avisos = avisos ?? throw new ArgumentNullException(nameof(avisos));
        }

        public ResultadoDeValidacion Ejecutar(Guid solicitudId, DateTime ahoraUtc)
        {
            if (solicitudId == Guid.Empty)
            {
                throw new ArgumentException("El id de la solicitud no puede estar vacío.", nameof(solicitudId));
            }

            if (ahoraUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("La fecha tiene que venir en UTC (Kind = DateTimeKind.Utc).", nameof(ahoraUtc));
            }

            var solicitud = _solicitudes.Leer(solicitudId);

            // Paso 1 (diseno/03 §1): idempotencia. Fuera de Ingresada se devuelve lo guardado, sin tocar nada más: UNA
            // sola llamada a Dataverse en todo el camino (la Leer() de arriba, que ahora trae también los tres
            // contadores ya guardados — commit a63ed82).
            if (solicitud.Estado != EstadoDeLaSolicitud.Ingresada)
            {
                return new ResultadoDeValidacion(solicitud.Estado, true, solicitud.FilasTotales, solicitud.FilasValidas, solicitud.FilasRechazadas,
                    string.Format(CultureInfo.InvariantCulture, "Solicitud {0}: ya procesada (estado {1}).", solicitud.Numero, solicitud.Estado));
            }

            // Paso 2 (diseno/03 §1 paso 3): los parámetros (4 de siempre y 2 de DN-05), versión activa más alta. El que falta es un error REAL
            // (no una regla fallida) cuyo mensaje nombra el parámetro.
            var pEstructura = ParametroObligatorio(ParametroEstructura);
            var pListas = ParametroObligatorio(ParametroListas);
            var pObligatoriedad = ParametroObligatorio(ParametroObligatoriedad);
            var pLimites = ParametroObligatorio(ParametroLimites);
            // DN-05: `plan.formatos` y `cuenta.largos` son obligatorios igual que los de arriba. Si faltan, sin ellos no se
            // puede validar nada (error real que nombra el parámetro, no una regla fallida).
            var pFormatos = ParametroObligatorio(ParametroFormatos);
            var pLargos = ParametroObligatorio(ParametroLargosDeCuenta);

            var estructura = ConfiguracionPlantilla.DesdeJson(pEstructura.Valor);
            var listas = ListasPlantilla.DesdeJson(pListas.Valor);
            var obligatoriedad = ObligatoriedadPlantilla.DesdeJson(pObligatoriedad.Valor);
            var limites = LimitesLectura.DesdeJson(pLimites.Valor);
            var formatos = FormatosDelPlan.DesdeJson(pFormatos.Valor);
            var largosDeCuenta = LargosDeCuenta.DesdeJson(pLargos.Valor);

            // Revisión DN-05, hallazgo 4: lo que `plan.formatos` completa o admite tiene que existir en `plantilla.listas`; si no,
            // se guardaría un valor que ninguna fila podría escribir. Error real ANTES de escribir nada. Los bancos de
            // `cuenta.largos` NO se cruzan: uno sin entrada en la lista simplemente nunca se alcanza.
            CruzarFormatosConListas(formatos, listas);

            // `lectura.limites` no entra en sanic_versionparametros: no cambia cómo se valida, solo cuánto se lee (diseno/03 §1 paso 3).
            var versionParametros = string.Format(
                CultureInfo.InvariantCulture, "estructura={0};listas={1};obligatoriedad={2};formatos={3};largos={4}",
                pEstructura.Version, pListas.Version, pObligatoriedad.Version, pFormatos.Version, pLargos.Version);

            // Paso 3 (diseno/03 §1 pasos 2 y 4): el sobre, con la plantilla leída A DEMANDA y una sola vez.
            var lector = _lector(limites) ?? throw new InvalidOperationException("La fábrica de lector de plantilla devolvió nulo.");
            var sobre = new SobreEnValidacion(solicitud.CantidadAdjuntos, solicitud.CantidadExcel,
                () => LeerPlantilla(solicitudId, limites, estructura, lector));

            var reglasSolicitud = _catalogos.ReglasActivasConId(NivelDeLaRegla.Solicitud);
            var idsSolicitudPorCodigo = reglasSolicitud.ToDictionary(r => r.Definicion.Codigo, r => r.Id, StringComparer.Ordinal);
            var motorSobre = new MotorDeReglas<SobreEnValidacion>(ReglasDelSobre.Evaluadores());
            var resultadosSolicitud = motorSobre.Evaluar(reglasSolicitud.Select(r => r.Definicion), sobre);

            // Paso 4: si alguna regla de solicitud No cumplida tiene efecto Rechaza o Envía a revisión, el sobre no deja
            // seguir: no se leen filas. Advierte no bloquea (diseno/02 §1 "Efecto de regla").
            var sobreDejaSeguir = !resultadosSolicitud.Any(r =>
                r.Resultado == ResultadoDeLaRegla.NoCumplida && r.EfectoAplicado != EfectoDeLaRegla.Advierte);

            var filasParaGuardar = new List<FilaParaGuardar>();
            var filasParaRespuesta = new List<FilaParaRespuesta>();
            var estadosDeFilas = new List<EstadoDeLaFila>();
            Guid? clienteConFilasPorDigitar = null;

            if (sobreDejaSeguir)
            {
                clienteConFilasPorDigitar = ProcesarFilas(sobre, estructura, listas, obligatoriedad, formatos, largosDeCuenta, solicitud.Numero, solicitud.Remitente, ahoraUtc, filasParaGuardar, filasParaRespuesta, estadosDeFilas);
            }

            var estadoFinal = EstadosPorReglas.DeLaSolicitud(resultadosSolicitud, estadosDeFilas);

            // El historial del sobre se guarda SIEMPRE, pase lo que pase (diseno/03 §1 paso 4: "en los tres finales posibles").
            _solicitudes.GuardarResultados(solicitudId, resultadosSolicitud, idsSolicitudPorCodigo, ahoraUtc);

            if (filasParaGuardar.Count > 0)
            {
                _solicitudes.GuardarFilas(solicitudId, filasParaGuardar);
            }

            var filasValidas = estadosDeFilas.Count(e => e == EstadoDeLaFila.Validada);
            var filasRechazadas = filasParaGuardar.Count - filasValidas;

            var acuse = ArmarAcuse(solicitud, estadoFinal, resultadosSolicitud, filasParaRespuesta);

            _solicitudes.CerrarValidacion(solicitudId, new CierreDeValidacion
            {
                Estado = estadoFinal,
                FechaValidada = ahoraUtc,
                FilasTotales = filasParaGuardar.Count,
                FilasValidas = filasValidas,
                FilasRechazadas = filasRechazadas,
                AcuseContenido = acuse,
                VersionParametros = versionParametros,
            });

            var resumen = ArmarResumen(solicitud.Numero, estadoFinal, filasParaGuardar.Count, filasValidas, filasRechazadas);

            // Ningún camino termina sin Bitácora (diseno/03 §1 paso 7).
            _solicitudes.RegistrarEvento(solicitudId, ahoraUtc, EventoDeBitacora.ValidacionTerminada, OrigenDelEvento.CustomAPI, 0, null, resumen);

            // D-64 (diseno/05 DA-08): la solicitud dejó filas por digitar (al menos una Validada) -> UN aviso al
            // ejecutivo DUEÑO del cliente, nunca uno por fila. `clienteConFilasPorDigitar` ya resolvió CUÁL cliente
            // (el de la primera fila Validada con plan conocido) sin ninguna consulta extra.
            if (clienteConFilasPorDigitar.HasValue)
            {
                _avisos.AvisarFilasPorDigitar(solicitudId, clienteConFilasPorDigitar.Value, TituloAvisoPorDigitar(solicitud.Numero), CuerpoAvisoPorDigitar(solicitud.Numero));
            }

            return new ResultadoDeValidacion(estadoFinal, false, filasParaGuardar.Count, filasValidas, filasRechazadas, resumen);
        }

        private static string TituloAvisoPorDigitar(string numeroSolicitud) => $"Tenés filas por digitar: solicitud {numeroSolicitud}";

        private static string CuerpoAvisoPorDigitar(string numeroSolicitud) => $"La solicitud {numeroSolicitud} tiene filas por digitar.";

        /// <summary>Un parámetro ausente es un error real que nombra su código (diseno/03 §1 paso 3), nunca una regla fallida.</summary>
        /// <summary>Revisión DN-05, hallazgo 4: toda clasificación, gestión y banco de `plan.formatos` está en la lista que le
        /// corresponde de `plantilla.listas`. Si no, <see cref="InvalidOperationException"/> que nombra el parámetro, el formato y el valor.</summary>
        private static void CruzarFormatosConListas(FormatosDelPlan formatos, ListasPlantilla listas)
        {
            foreach (var formato in new[] { TipoDeFormatoDelPlan._06, TipoDeFormatoDelPlan._10, TipoDeFormatoDelPlan._11 })
            {
                var reglas = formatos.Para(formato);
                foreach (var v in reglas.Clasificaciones)
                    ExigirEnLista(listas, CamposDeFila.Clasificacion, v.ToString(), formato);
                foreach (var v in reglas.Gestiones)
                    ExigirEnLista(listas, CamposDeFila.Gestion, v.ToString(), formato);
                // `Bancos` nulo = cualquier banco (la clave no vino): no hay nada que cruzar.
                foreach (var v in reglas.Bancos ?? (IReadOnlyList<Banco>)new Banco[0])
                    ExigirEnLista(listas, CamposDeFila.Banco, v.ToString(), formato);
            }
        }

        private static void ExigirEnLista(ListasPlantilla listas, string lista, string valor, TipoDeFormatoDelPlan formato)
        {
            if (!listas.Acepta(lista, valor))
            {
                throw new InvalidOperationException(string.Format(
                    CultureInfo.InvariantCulture,
                    "El parámetro '{0}' trae '{1}' en el formato {2}, y el parámetro '{3}' no lo acepta en la lista '{4}'.",
                    ParametroFormatos, valor, formato, ParametroListas, lista));
            }
        }

        private ParametroLeido ParametroObligatorio(string nombre)
        {
            var parametro = _catalogos.Parametro(nombre);
            if (parametro == null)
            {
                throw new InvalidOperationException($"Falta el parámetro '{nombre}', necesario para validar la solicitud.");
            }

            return parametro;
        }

        /// <summary>
        /// Baja el Excel de `sanic_exceloriginal` y lo lee. LP-02: un archivo más pesado que el límite NO revienta acá:
        /// vuelve como un resultado de lectura inválido, para que sea la regla ESTRUCTURA_PLANTILLA la que no se cumpla,
        /// con un motivo para el cliente (diseno/03 §7). Aprobador, 2026-11-24: el cliente recibe SOLO la frase del peso, con
        /// el máximo configurado (<see cref="MensajeDePesoExcedido"/>), sea que el tope roto sea el comprimido (acá) o el
        /// descomprimido LP-03 (el lector lo rechaza con un texto técnico, que se reemplaza por este mismo mensaje).
        /// </summary>
        private ResultadoLecturaPlantilla LeerPlantilla(Guid solicitudId, LimitesLectura limites, ConfiguracionPlantilla estructura, ILectorPlantilla lector)
        {
            byte[] excel;
            try
            {
                excel = _archivos.Descargar(TablasHistorico.Solicitud, solicitudId, ColumnaExcel, limites.TamanoMaximoBytesEntrada);
            }
            catch (ArchivoExcedeElMaximoException)
            {
                return ResultadoLecturaPlantilla.ConError(
                    TipoDeRechazoLectura.PesoExcedido, MensajeDePesoExcedido(limites.TamanoMaximoBytesEntrada));
            }

            var lectura = lector.Leer(excel, estructura);
            if (lectura.Rechazo == TipoDeRechazoLectura.PesoExcedido)
            {
                return ResultadoLecturaPlantilla.ConError(
                    TipoDeRechazoLectura.PesoExcedido, MensajeDePesoExcedido(limites.TamanoMaximoBytesEntrada));
            }

            return lectura;
        }

        /// <summary>
        /// Texto EXACTO para el cliente cuando el Excel pesa más que el tope (aprobador, 2026-11-24). El máximo va en megas:
        /// entero si es exacto ("2"), si no con un decimal y coma ("2,5"). Formato independiente de la cultura del hilo.
        /// </summary>
        public static string MensajeDePesoExcedido(long maximoBytes)
        {
            const double BytesPorMega = 1048576d;
            var formato = new NumberFormatInfo { NumberDecimalSeparator = "," };
            var megas = maximoBytes % 1048576L == 0
                ? (maximoBytes / 1048576L).ToString("0", formato)
                : (maximoBytes / BytesPorMega).ToString("0.0", formato);
            return "El archivo de Excel pesa más de lo permitido (máximo " + megas + " MB). Por favor, envíe un archivo más liviano.";
        }

        /// <summary>
        /// Paso 5 y 6 (diseno/03 §1): evalúa las reglas de registro fila por fila. Los catálogos se cargan UNA vez: los
        /// planes de todos los números de plan de la plantilla en una sola consulta `In`, y las autorizaciones del
        /// remitente aparte. Devuelve el cliente dueño de la PRIMERA fila que quedó Validada (con plan conocido), o
        /// nulo si ninguna quedó Validada: es a ese cliente al que D-64 avisa "filas por digitar" (una vez, no por fila).
        /// </summary>
        private Guid? ProcesarFilas(
            SobreEnValidacion sobre, ConfiguracionPlantilla estructura, ListasPlantilla listas, ObligatoriedadPlantilla obligatoriedad,
            FormatosDelPlan formatos, LargosDeCuenta largosDeCuenta, string numeroDeSolicitud, string remitente, DateTime ahoraUtc,
            List<FilaParaGuardar> filasParaGuardar, List<FilaParaRespuesta> filasParaRespuesta, List<EstadoDeLaFila> estadosDeFilas)
        {
            var filasDeLaVentana = sobre.Lectura.Filas;
            if (filasDeLaVentana == null || filasDeLaVentana.Count == 0)
            {
                return null;
            }

            // Catálogo "vacío" solo para normalizar los números de plan (no hace falta la lista de planes para eso):
            // sirve para pedir los planes de una sola vez, con una única consulta `In` (diseno/03 §1 paso 6). Normalizar
            // dos veces es barato (el orden lo confirma la tarea): se vuelve a armar la fila con el catálogo real abajo.
            var catalogosParaNormalizar = new CatalogosDeValidacion(estructura, listas, obligatoriedad, new List<PlanDelCatalogo>(), new List<Guid>());
            var codigosDePlan = filasDeLaVentana
                .Select(f => new FilaEnValidacion(f, catalogosParaNormalizar).NumeroPlanNormalizado)
                .Where(codigo => codigo != null)
                .Distinct(StringComparer.Ordinal);

            var planesActivos = _catalogos.PlanesActivosPorCodigo(codigosDePlan);
            var planesAutorizados = _catalogos.PlanesAutorizadosDe(remitente);
            var catalogos = new CatalogosDeValidacion(estructura, listas, obligatoriedad, planesActivos, planesAutorizados, formatos: formatos, largosDeCuenta: largosDeCuenta);

            var reglasRegistro = _catalogos.ReglasActivasConId(NivelDeLaRegla.Registro);
            var definicionesRegistro = reglasRegistro.Select(r => r.Definicion).ToList();
            var motorFila = new MotorDeReglas<FilaEnValidacion>(ReglasDeRegistro.Evaluadores());
            Guid? clienteConFilasPorDigitar = null;

            foreach (var filaPlantilla in filasDeLaVentana)
            {
                var fila = new FilaEnValidacion(filaPlantilla, catalogos);
                var resultadosFila = motorFila.Evaluar(definicionesRegistro, fila);
                var estadoFila = EstadosPorReglas.DeLaFila(resultadosFila);
                var motivos = EstadosPorReglas.MotivosDeLaFila(resultadosFila);
                var mensaje = motivos.Count > 0 ? string.Join(" ", motivos) : null;

                estadosDeFilas.Add(estadoFila);

                if (estadoFila == EstadoDeLaFila.Validada && clienteConFilasPorDigitar == null && fila.Plan?.ClienteId != null)
                {
                    clienteConFilasPorDigitar = fila.Plan.ClienteId;
                }

                var numeroCuenta = fila.Recibido(CamposDeFila.NumeroCuenta);
                var numeroIdentificacion = fila.Recibido(CamposDeFila.NumeroIdentificacion);
                var numeroPlan = fila.NumeroPlanNormalizado;

                filasParaGuardar.Add(new FilaParaGuardar
                {
                    NumeroFila = fila.NumeroFila,
                    // Se calcula acá, con el número de solicitud ya en memoria: así el step no vuelve a leer la
                    // Solicitud por cada fila del Create (03 §1 paso 6, "nunca una consulta por fila").
                    Nombre = NombreCalculado.DeFila(numeroDeSolicitud, fila.NumeroFila),
                    Gestion = fila.Gestion,
                    // DN-05: lo que se guarda es lo EFECTIVO (la clasificación y el banco que el formato del plan completó solos).
                    Clasificacion = fila.ClasificacionEfectiva,
                    Moneda = fila.Moneda,
                    TipoIdentificacion = fila.TipoIdentificacion,
                    Banco = fila.BancoEfectivo,
                    NumeroPlan = numeroPlan,
                    PlanId = fila.Plan?.Id,
                    NombreBeneficiario = fila.Recibido(CamposDeFila.NombreBeneficiario),
                    NumeroIdentificacion = numeroIdentificacion,
                    NumeroCuenta = numeroCuenta,
                    Referencia = fila.ReferenciaQueRige,
                    ReferenciaRecibida = fila.ReferenciaRecibida,
                    Estado = estadoFila,
                    Mensaje = mensaje,
                    FechaValidada = ahoraUtc,
                });

                filasParaRespuesta.Add(new FilaParaRespuesta
                {
                    NumeroFila = fila.NumeroFila,
                    NumeroPlan = numeroPlan ?? fila.Recibido(CamposDeFila.NumeroPlan),
                    NombreBeneficiario = fila.Recibido(CamposDeFila.NombreBeneficiario),
                    NumeroCuenta = numeroCuenta,
                    NumeroIdentificacion = numeroIdentificacion,
                    Estado = estadoFila,
                    Mensaje = mensaje,
                });
            }

            return clienteConFilasPorDigitar;
        }

        /// <summary>Arma el acuse (RF-04, DD-09). No reconocida no tiene acuse: no se le responde al cliente sin que la mire una persona (D-39).</summary>
        private static string ArmarAcuse(SolicitudLeida solicitud, EstadoDeLaSolicitud estado, IList<ResultadoDeRegla> resultadosSolicitud, IList<FilaParaRespuesta> filas)
        {
            if (estado == EstadoDeLaSolicitud.NoReconocida)
            {
                return null;
            }

            var motivosDelSobre = resultadosSolicitud
                .Where(r => r.Resultado == ResultadoDeLaRegla.NoCumplida)
                .OrderBy(r => r.Orden)
                .Select(r => r.Razon)
                .ToList();

            return ArmadorRespuesta.Acuse(new SolicitudParaRespuesta
            {
                Numero = solicitud.Numero,
                FechaRecibidoTexto = solicitud.FechaRecibido.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
                Estado = estado,
                MotivosDelSobre = motivosDelSobre,
                Filas = filas,
            });
        }

        /// <summary>Una línea para el run history (`Resumen`), sin datos del cliente (diseno/03 §1 "resumen").</summary>
        private static string ArmarResumen(string numero, EstadoDeLaSolicitud estado, int totales, int validas, int rechazadas)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "Solicitud {0}: {1}. Filas totales: {2}, válidas: {3}, rechazadas: {4}.",
                numero, estado, totales, validas, rechazadas);
        }
    }
}
