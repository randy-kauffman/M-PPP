using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Sanic.Mppp.Plugins.Datos;
using Sanic.Mppp.Plugins.Exportacion;
using Sanic.Mppp.Plugins.Validacion;

namespace Sanic.Mppp.Plugins.Api
{
    /// <summary>Las salidas de `sanic_mppp_capi_exportarparadigitar` (diseno/DIRECTRICES-NUEVAS.md DN-02).</summary>
    public sealed class ResultadoDeExportacion
    {
        public ResultadoDeExportacion(string archivoBase64, int exportadas)
        {
            ArchivoBase64 = archivoBase64;
            Exportadas = exportadas;
        }

        /// <summary>El `.xlsx` armado por <see cref="Exportacion.ArmadorExportacionOpenXml"/>, en base64.</summary>
        public string ArchivoBase64 { get; }

        /// <summary>Cuántas filas quedaron efectivamente en el archivo.</summary>
        public int Exportadas { get; }
    }

    /// <summary>
    /// Faltó una o más Filas pedidas: no existen (o ya no). Decisión del aprobador (2026-09-26): esto REVIENTA la
    /// llamada ENTERA nombrando cuáles, nunca se omiten en silencio. La intuición tira para el otro lado, pero un
    /// archivo con una fila menos, entregado como si estuviera completo, es una fila que el ejecutivo NUNCA digita
    /// y NADIE se entera hasta que el cliente reclama semanas después. Un rechazo ruidoso cuesta diez segundos:
    /// volver a seleccionar y descargar de nuevo.
    /// </summary>
    public sealed class FilasNoEncontradasException : Exception
    {
        public FilasNoEncontradasException(IReadOnlyList<Guid> faltantes)
            : base(Mensaje(faltantes))
        {
            Faltantes = faltantes;
        }

        /// <summary>Los `filaid` pedidos que NO volvieron de <see cref="Datos.FilasDataverse.PorIds"/>.</summary>
        public IReadOnlyList<Guid> Faltantes { get; }

        private static string Mensaje(IReadOnlyList<Guid> faltantes)
        {
            var lista = string.Join(", ", faltantes);
            return $"No se pudieron exportar {faltantes.Count.ToString(CultureInfo.InvariantCulture)} fila(s): no existen o ya no están disponibles (filaid: {lista}). Volvé a seleccionar y descargar de nuevo.";
        }
    }

    /// <summary>
    /// El plugin se acercó al presupuesto síncrono real de Dataverse (BP-PP-053) antes de terminar de armar el
    /// archivo. Decisión del aprobador (2026-09-26, reemplaza el tope fijo `exportacion.maximo` que tenía esta
    /// Custom API cuando era Function): sin tope de filas, tarde o temprano alguien selecciona un lote tan grande
    /// que el tiempo no alcanza. En vez de dejar que la PLATAFORMA reviente con un error genérico e ininteligible,
    /// el plugin lleva la cuenta del tiempo transcurrido y CORTA antes, con un mensaje que le dice a una persona
    /// qué hacer: cuántas filas alcanzó a preparar y que vuelva a seleccionar menos. Nunca se entrega un archivo
    /// parcial sin avisar (mismo criterio que <see cref="FilasNoEncontradasException"/>): se rechaza LA LLAMADA
    /// ENTERA, porque un archivo con menos filas que las seleccionadas, entregado como si fuera completo, es
    /// exactamente el mismo riesgo que este proyecto ya eliminó una vez (filas que el ejecutivo nunca digitó en
    /// AS400 pero que el sistema puede llegar a dar por digitadas).
    /// </summary>
    public sealed class PresupuestoDeExportacionAgotandoseException : Exception
    {
        public PresupuestoDeExportacionAgotandoseException(int procesadas, int pedidas)
            : base(
                $"La descarga no alcanzó a terminar con las {pedidas.ToString(CultureInfo.InvariantCulture)} fila(s) " +
                $"que seleccionaste: se llegaron a preparar {procesadas.ToString(CultureInfo.InvariantCulture)} antes " +
                "de frenar a tiempo. Volvé a seleccionar con menos filas y descargá de nuevo en lotes más chicos.")
        {
            Procesadas = procesadas;
            Pedidas = pedidas;
        }

        /// <summary>Cuántas filas se alcanzaron a preparar ANTES de cortar (nunca se llegan a entregar: la llamada se rechaza entera).</summary>
        public int Procesadas { get; }

        /// <summary>Cuántas filas se habían pedido en total.</summary>
        public int Pedidas { get; }
    }

    /// <summary>
    /// El plugin de `sanic_mppp_capi_exportarparadigitar` (DN-02), sin el envoltorio `IPlugin` (que es
    /// <see cref="ExportarParaDigitarApi"/>). Orquesta piezas que YA EXISTEN y están en verde
    /// (<see cref="ConfiguracionExportacion"/>, <see cref="Exportacion.ArmadorExportacionOpenXml"/>): no decide nada
    /// nuevo sobre el dominio de exportación en sí. Contrato (lo fijan las pruebas `ExportarParaDigitarAceptacion`):
    ///
    ///  1. YA NO hay tope de filas por llamada (decisión del aprobador, 2026-09-26: se eliminó `exportacion.maximo`
    ///     al pasar la Custom API de Function a Action). El único límite que queda es el presupuesto síncrono real
    ///     de Dataverse (BP-PP-053): <c>Ejecutar</c> lleva la cuenta del tiempo transcurrido mientras arma cada fila
    ///     y, si se acerca al presupuesto, CORTA rechazando LA LLAMADA ENTERA con
    ///     <see cref="PresupuestoDeExportacionAgotandoseException"/> (nombra cuántas alcanzó a preparar) en vez de
    ///     dejar que la plataforma reviente con un error genérico;
    ///  2. lee `exportacion.columnas` y lo resuelve con <see cref="ConfiguracionExportacion.DesdeJson"/>: ausente es
    ///     <see cref="InvalidOperationException"/> (mismo criterio que
    ///     <see cref="RegistrarResultadoRpa"/>.ParametroObligatorio), mal formado propaga el
    ///     <see cref="FormatException"/> del dominio tal cual;
    ///  3. lee las Filas pedidas por id (<see cref="Datos.FilasDataverse.PorIds"/>) — un id que no existe simplemente
    ///     no vuelve en la respuesta, NUNCA una excepción de plataforma atrapada para seguir procesando (mismo
    ///     criterio que <see cref="Datos.FilasDataverse.LeerParaRegistro"/>). Si falta AUNQUE SEA UNA, revienta la
    ///     llamada ENTERA nombrando cuáles (<see cref="FilasNoEncontradasException"/>, decisión del aprobador,
    ///     2026-09-26: ver el XML doc de esa excepción para el porqué);
    ///  4. arma una <see cref="Exportacion.FilaParaExportar"/> por Fila, con los valores que este plugin SABE
    ///     resolver: el vocabulario de <see cref="Validacion.CamposDeFila"/> (el mismo de `plantilla.estructura`,
    ///     DN-02 los llama hermanos) MÁS <see cref="CampoNumeroSolicitud"/> y <see cref="CampoNumeroFila"/> —
    ///     agregados por decisión del aprobador, 2026-09-26: sin identidad de fila el Excel es una lista de datos
    ///     que el ejecutivo no puede volver a marcar como digitada. Un campo que el parámetro pida FUERA de ese
    ///     vocabulario no es un error de ESTA clase: lo revienta <see cref="Exportacion.ArmadorExportacionOpenXml.Armar"/>
    ///     con su propio <see cref="ArgumentException"/> ruidoso, porque la fila no lo trae como clave (DN-02: "el
    ///     código no tiene una lista propia de campos permitidos ni prohibidos" — la restricción NO nace acá);
    ///  5. el orden de las filas en el archivo es el orden de <c>filaIds</c> (el orden en que se pidieron), no el
    ///     que devuelva Dataverse (que no está garantizado);
    ///  6. arma el `.xlsx` con <see cref="Exportacion.IArmadorExportacion.Armar"/> y lo devuelve en base64.
    /// </summary>
    public sealed class ExportarParaDigitar
    {
        /// <summary>El parámetro de configuración (DN-02), hermano de `plantilla.estructura` pero de SALIDA.</summary>
        public const string ParametroColumnas = "exportacion.columnas";

        /// <summary>El número de Solicitud (`sanic_nombre`, ej. "MPPP-00000123"), agregado al vocabulario el 2026-09-26 para que el archivo tenga identidad de fila.</summary>
        public const string CampoNumeroSolicitud = "numeroSolicitud";

        /// <summary>`sanic_numerofila`, agregado al vocabulario el 2026-09-26 por el mismo motivo que <see cref="CampoNumeroSolicitud"/>.</summary>
        public const string CampoNumeroFila = "numeroFila";

        /// <summary>
        /// Presupuesto síncrono real de un plugin de Dataverse (BP-PP-053, `dataverse-async`). Nunca sale de un
        /// parámetro configurable: un valor mal puesto ahí apuntaría el corte a un número que la plataforma no
        /// respeta, y eso es peor que no cortar.
        /// </summary>
        private static readonly TimeSpan PresupuestoSincrono = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Margen que se deja SIN procesar más filas, para alcanzar a armar el `.xlsx` de lo ya preparado y volver
        /// (aunque termine en <see cref="PresupuestoDeExportacionAgotandoseException"/>) antes de que la
        /// PLATAFORMA corte con un error genérico e ininteligible.
        /// </summary>
        private static readonly TimeSpan MargenParaTerminar = TimeSpan.FromSeconds(20);

        private static readonly TimeSpan UmbralDeCorte = PresupuestoSincrono - MargenParaTerminar;

        private readonly FilasDataverse _filas;
        private readonly CatalogosDataverse _catalogos;
        private readonly IArmadorExportacion _armador;

        public ExportarParaDigitar(FilasDataverse filas, CatalogosDataverse catalogos, IArmadorExportacion armador)
        {
            _filas = filas ?? throw new ArgumentNullException(nameof(filas));
            _catalogos = catalogos ?? throw new ArgumentNullException(nameof(catalogos));
            _armador = armador ?? throw new ArgumentNullException(nameof(armador));
        }

        /// <param name="filaIds">
        /// Los `filaid` seleccionados, YA VALIDADOS como Guid por quien llama (<see cref="ExportarParaDigitarApi"/>).
        /// El ORDEN de esta lista es el orden en que salen las filas en el archivo (ver punto 5 de la clase). Vacía
        /// → archivo válido con cero filas de datos (solo el encabezado), no es un error.
        /// </param>
        public ResultadoDeExportacion Ejecutar(IList<Guid> filaIds)
        {
            return Ejecutar(filaIds, RelojDeSistema());
        }

        /// <summary>
        /// Igual que <see cref="Ejecutar(IList{Guid})"/>, con el reloj inyectado: es lo que le permite a una
        /// prueba ejercitar el corte de <see cref="PresupuestoDeExportacionAgotandoseException"/> (BP-PP-053) sin
        /// tener que esperar de verdad casi 2 minutos. Producción SIEMPRE pasa por el overload de un solo
        /// parámetro, que arranca un cronómetro real.
        /// </summary>
        public ResultadoDeExportacion Ejecutar(IList<Guid> filaIds, Func<TimeSpan> transcurridoDesdeElInicio)
        {
            if (filaIds == null)
            {
                throw new ArgumentNullException(nameof(filaIds));
            }

            if (transcurridoDesdeElInicio == null)
            {
                throw new ArgumentNullException(nameof(transcurridoDesdeElInicio));
            }

            var configuracion = ConfiguracionExportacion.DesdeJson(ParametroObligatorio(ParametroColumnas).Valor);

            var encontradas = _filas.PorIds(filaIds);
            var porId = encontradas.ToDictionary(f => f.FilaId);

            var faltantes = filaIds.Where(id => !porId.ContainsKey(id)).Distinct().ToList();
            if (faltantes.Count > 0)
            {
                throw new FilasNoEncontradasException(faltantes);
            }

            var numerosDeSolicitud = _filas.NumerosDeSolicitud(encontradas.Select(f => f.SolicitudId).Distinct());

            // Sin tope de filas (decisión del aprobador, 2026-09-26), un lote lo bastante grande puede acercarse al
            // presupuesto síncrono real de Dataverse. Se arma fila por fila, chequeando el tiempo transcurrido ANTES
            // de cada una: si ya se cruzó el umbral, se corta acá — nunca se entrega un archivo parcial sin avisar.
            var filasParaExportar = new List<FilaParaExportar>(filaIds.Count);
            foreach (var id in filaIds)
            {
                if (transcurridoDesdeElInicio() >= UmbralDeCorte)
                {
                    throw new PresupuestoDeExportacionAgotandoseException(filasParaExportar.Count, filaIds.Count);
                }

                filasParaExportar.Add(ValoresDeFila(porId[id], numerosDeSolicitud));
            }

            var xlsx = _armador.Armar(filasParaExportar, configuracion);

            return new ResultadoDeExportacion(Convert.ToBase64String(xlsx), filasParaExportar.Count);
        }

        private static Func<TimeSpan> RelojDeSistema()
        {
            var cronometro = Stopwatch.StartNew();
            return () => cronometro.Elapsed;
        }

        private static FilaParaExportar ValoresDeFila(FilaPendienteLeida fila, IDictionary<Guid, string> numerosDeSolicitud)
        {
            if (!numerosDeSolicitud.TryGetValue(fila.SolicitudId, out var numeroDeSolicitud))
            {
                throw new InvalidOperationException($"La fila {fila.FilaId} no tiene una solicitud resoluble: no se le puede armar la exportación.");
            }

            return new FilaParaExportar
            {
                Valores = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [CamposDeFila.NumeroPlan] = fila.NumeroPlan,
                    [CamposDeFila.NombreBeneficiario] = fila.NombreBeneficiario,
                    [CamposDeFila.NumeroIdentificacion] = fila.NumeroIdentificacion,
                    [CamposDeFila.NumeroCuenta] = fila.NumeroCuenta,
                    [CamposDeFila.Referencia] = fila.Referencia,
                    [CamposDeFila.Gestion] = fila.Gestion?.ToString(),
                    [CamposDeFila.Clasificacion] = fila.Clasificacion?.ToString(),
                    [CamposDeFila.TipoIdentificacion] = fila.TipoIdentificacion?.ToString(),
                    [CamposDeFila.Moneda] = fila.Moneda?.ToString(),
                    [CamposDeFila.Banco] = fila.Banco?.ToString(),
                    [CampoNumeroSolicitud] = numeroDeSolicitud,
                    [CampoNumeroFila] = fila.NumeroFila.ToString(CultureInfo.InvariantCulture),
                },
            };
        }

        private ParametroLeido ParametroObligatorio(string nombre)
        {
            var parametro = _catalogos.Parametro(nombre);
            if (parametro == null)
            {
                throw new InvalidOperationException($"Falta el parámetro '{nombre}', necesario para exportar filas a Excel.");
            }

            return parametro;
        }
    }
}
