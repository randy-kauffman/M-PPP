using System;
using System.Collections.Generic;
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
    /// El pedido supera `exportacion.maximo`. Decisión del aprobador (2026-09-26): se rechaza LA LLAMADA ENTERA
    /// nombrando el tope, nunca se recorta en silencio — recortar entregaría un archivo incompleto sin avisar, el
    /// mismo pecado que omitir una fila faltante (<see cref="FilasNoEncontradasException"/>).
    /// </summary>
    public sealed class LoteDeExportacionExcedidoException : Exception
    {
        public LoteDeExportacionExcedidoException(int pedidas, int tope)
            : base($"Se pidieron {pedidas.ToString(CultureInfo.InvariantCulture)} filas; el máximo por llamada es " +
                   $"{tope.ToString(CultureInfo.InvariantCulture)} (parámetro '{ExportarParaDigitar.ParametroMaximo}', " +
                   "PROVISIONAL: conservador, sin medir la escritura real del Excel — BP-PP-053). Hacé la descarga en lotes más chicos.")
        {
            Pedidas = pedidas;
            Tope = tope;
        }

        public int Pedidas { get; }

        public int Tope { get; }
    }

    /// <summary>
    /// El plugin de `sanic_mppp_capi_exportarparadigitar` (DN-02), sin el envoltorio `IPlugin` (que es
    /// <see cref="ExportarParaDigitarApi"/>). Orquesta piezas que YA EXISTEN y están en verde
    /// (<see cref="ConfiguracionExportacion"/>, <see cref="Exportacion.ArmadorExportacionOpenXml"/>): no decide nada
    /// nuevo sobre el dominio de exportación en sí. Contrato (lo fijan las pruebas `ExportarParaDigitarAceptacion`):
    ///
    ///  1. lee `exportacion.maximo` (REQUERIDO, entero positivo) y rechaza LA LLAMADA ENTERA si <c>filaIds</c> la
    ///     supera (<see cref="LoteDeExportacionExcedidoException"/>) — decisión del aprobador, 2026-09-26: sembrado
    ///     en 500, PROVISIONAL pendiente de medir la escritura real del Excel (BP-PP-053, la lectura la midió el
    ///     spike C-05 pero la escritura no);
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

        /// <summary>El tope de filas por llamada (decisión del aprobador, 2026-09-26). Sembrado en 500, PROVISIONAL.</summary>
        public const string ParametroMaximo = "exportacion.maximo";

        /// <summary>El número de Solicitud (`sanic_nombre`, ej. "MPPP-00000123"), agregado al vocabulario el 2026-09-26 para que el archivo tenga identidad de fila.</summary>
        public const string CampoNumeroSolicitud = "numeroSolicitud";

        /// <summary>`sanic_numerofila`, agregado al vocabulario el 2026-09-26 por el mismo motivo que <see cref="CampoNumeroSolicitud"/>.</summary>
        public const string CampoNumeroFila = "numeroFila";

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
            if (filaIds == null)
            {
                throw new ArgumentNullException(nameof(filaIds));
            }

            var tope = ParametroMaximoLeido();
            if (filaIds.Count > tope)
            {
                throw new LoteDeExportacionExcedidoException(filaIds.Count, tope);
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

            var filasParaExportar = filaIds.Select(id => ValoresDeFila(porId[id], numerosDeSolicitud)).ToList();
            var xlsx = _armador.Armar(filasParaExportar, configuracion);

            return new ResultadoDeExportacion(Convert.ToBase64String(xlsx), filasParaExportar.Count);
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

        private int ParametroMaximoLeido()
        {
            var parametro = ParametroObligatorio(ParametroMaximo);
            if (!int.TryParse(parametro.Valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor) || valor <= 0)
            {
                throw new InvalidOperationException($"El parámetro '{ParametroMaximo}' tiene que ser un entero positivo.");
            }

            return valor;
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
