using System.Collections.Generic;

namespace Sanic.Mppp.Plugins.Exportacion
{
    /// <summary>
    /// Una fila YA RESUELTA que hay que exportar (DN-02): el resto del sistema (fuera de este dominio, fuera de alcance
    /// acá) ya resolvió sus valores desde Dataverse. Este dominio no sabe de Dataverse, de Custom API ni de dónde salió
    /// cada valor.
    /// </summary>
    public sealed class FilaParaExportar
    {
        /// <summary>
        /// Valor de cada campo, por NOMBRE DE CAMPO (la misma clave que <see cref="ColumnaExportacion.Campo"/>). Un campo
        /// que la configuración pide y esta fila no trae como CLAVE es un error de configuración ruidoso (ver
        /// <see cref="IArmadorExportacion"/>): nunca una columna vacía en silencio. Un valor presente pero nulo o vacío sí
        /// es una celda vacía legítima.
        /// </summary>
        public IDictionary<string, string> Valores { get; set; }
    }
}
