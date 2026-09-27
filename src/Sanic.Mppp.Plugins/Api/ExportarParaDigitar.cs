using System;
using System.Collections.Generic;
using Sanic.Mppp.Plugins.Datos;
using Sanic.Mppp.Plugins.Exportacion;

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
    /// El plugin de `sanic_mppp_capi_exportarparadigitar` (DN-02), sin el envoltorio `IPlugin` (que es
    /// <see cref="ExportarParaDigitarApi"/>). Orquesta piezas que YA EXISTEN y están en verde
    /// (<see cref="ConfiguracionExportacion"/>, <see cref="Exportacion.ArmadorExportacionOpenXml"/>): no decide nada
    /// nuevo sobre el dominio de exportación en sí. Contrato (lo fijan las pruebas `ExportarParaDigitarAceptacion`):
    ///
    ///  1. lee las Filas pedidas por id (<see cref="Datos.FilasDataverse.PorIds"/>) — un id que no existe simplemente
    ///     no vuelve en la respuesta, NUNCA una excepción de plataforma atrapada para seguir procesando (mismo
    ///     criterio que <see cref="Datos.FilasDataverse.LeerParaRegistro"/>: "una consulta que no encuentra nada
    ///     devuelve vacío", nunca un `try/catch` alrededor de una llamada a `OrganizationService`);
    ///  2. lee `exportacion.columnas` y lo resuelve con <see cref="ConfiguracionExportacion.DesdeJson"/>: ausente es
    ///     <see cref="InvalidOperationException"/> (mismo criterio que
    ///     <see cref="ObtenerFilasPendientes"/>.ParametroObligatorio), mal formado propaga el
    ///     <see cref="FormatException"/> del dominio tal cual;
    ///  3. arma una <see cref="Exportacion.FilaParaExportar"/> por Fila encontrada, con los valores que este plugin
    ///     SABE resolver: el vocabulario de <see cref="Validacion.CamposDeFila"/>, el mismo de `plantilla.estructura`
    ///     (DN-02: "`exportacion.columnas`... hermano de `plantilla.estructura`, pero de SALIDA"). Un campo que el
    ///     parámetro pida FUERA de ese vocabulario no es un error de ESTA clase: lo revienta
    ///     <see cref="Exportacion.ArmadorExportacionOpenXml.Armar"/> con su propio <see cref="ArgumentException"/>
    ///     ruidoso, porque la fila no lo trae como clave (DN-02, DIRECTRICES-NUEVAS.md: "el código no tiene una
    ///     lista propia de campos permitidos ni prohibidos" — la restricción NO nace acá tampoco);
    ///  4. el orden de las filas en el archivo es el orden de <c>filaIds</c> (el orden en que se pidieron), no el
    ///     que devuelva Dataverse (que no está garantizado);
    ///  5. arma el `.xlsx` con <see cref="Exportacion.IArmadorExportacion.Armar"/> y lo devuelve en base64.
    ///
    /// **PENDIENTE DE DECISIÓN DEL APROBADOR** (reportado en la Etapa 1, a propósito NO implementado ni testeado
    /// acá todavía — ver el reporte de esa etapa):
    ///  - qué pasa con un id de <c>filaIds</c> que no existe, o que existe pero no está en un estado exportable:
    ///    ¿se omite del archivo, o revienta toda la llamada?;
    ///  - si hace falta un tope de filas por llamada (BP-PP-053: el presupuesto es 2 minutos y la ESCRITURA del
    ///    Excel está SIN MEDIR, a diferencia de la LECTURA que sí midió el spike C-05).
    /// Mientras eso no se decida, esta clase NO filtra por estado ni por cantidad: ese es exactamente el hueco que
    /// separa esta Etapa 1 (roja) de la Etapa 2.
    /// </summary>
    public sealed class ExportarParaDigitar
    {
        /// <summary>El parámetro de configuración (DN-02), hermano de `plantilla.estructura` pero de SALIDA.</summary>
        public const string ParametroColumnas = "exportacion.columnas";

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
        /// El ORDEN de esta lista es el orden en que salen las filas en el archivo (ver punto 4 de la clase). Vacía
        /// → archivo válido con cero filas de datos (solo el encabezado), no es un error.
        /// </param>
        public ResultadoDeExportacion Ejecutar(IList<Guid> filaIds)
        {
            throw new NotImplementedException("DN-02: lo implementa el constructor contra ExportarParaDigitarAceptacion, Etapa 2.");
        }
    }
}
