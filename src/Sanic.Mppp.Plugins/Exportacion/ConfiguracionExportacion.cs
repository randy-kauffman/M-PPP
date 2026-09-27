using System;
using System.Collections.Generic;

namespace Sanic.Mppp.Plugins.Exportacion
{
    /// <summary>
    /// El orden de columnas de la exportación a Excel (parámetro `exportacion.columnas`, diseno/DIRECTRICES-NUEVAS.md DN-02).
    /// Hermana de `plantilla.estructura` (<see cref="Plantilla.ConfiguracionPlantilla"/>) pero de SALIDA: acá el código NO
    /// decide qué columnas se pueden pedir. Cualquier nombre de campo vale; este dominio no tiene una lista propia de
    /// campos permitidos ni prohibidos (DN-02: "Qué columnas salen lo decide ese parámetro, no el código").
    /// </summary>
    public sealed class ConfiguracionExportacion
    {
        /// <summary>Las columnas, en el ORDEN en que salen en el Excel: la primera es la columna A, la segunda la B, etc.</summary>
        public IList<ColumnaExportacion> Columnas { get; set; }

        /// <summary>
        /// Lee el JSON del parámetro. Forma:
        /// {"columnas":[{"campo":"numeroPlan","encabezado":"No. Plan"},{"campo":"nombreBeneficiario","encabezado":"Nombre"}]}
        /// El ORDEN del arreglo "columnas" ES el orden de las columnas del Excel (columna A = primer elemento, B = segundo, ...).
        /// Un JSON que no se puede leer, o con valores que no forman una configuración válida, es <see cref="FormatException"/>
        /// con el motivo: un parámetro mal cargado no puede terminar en una exportación "vacía pero válida" (mismo criterio
        /// que <see cref="Plantilla.ConfiguracionPlantilla.DesdeJson"/>).
        /// NO usa System.Text.Json (Microsoft advierte no depender de él en un plugin): DataContractJsonSerializer.
        /// </summary>
        public static ConfiguracionExportacion DesdeJson(string json)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// ÚNICA validación de una <see cref="ConfiguracionExportacion"/> ya armada, la use quien la use: <see cref="DesdeJson"/>
        /// (la traduce a <see cref="FormatException"/>) o <see cref="ArmadorExportacionOpenXml.Armar"/> (la traduce a
        /// <see cref="ArgumentException"/>, para una configuración armada a mano). Devuelve el motivo del primer problema
        /// encontrado, o null si la configuración es válida.
        /// </summary>
        internal static string Invalidez(ConfiguracionExportacion configuracion)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>Una columna de la exportación: qué campo trae y con qué texto de encabezado.</summary>
    public sealed class ColumnaExportacion
    {
        /// <summary>Nombre del campo: la clave que se busca en <see cref="FilaParaExportar.Valores"/> de cada fila.</summary>
        public string Campo { get; set; }

        /// <summary>Texto de la fila de encabezado del Excel, en la misma columna.</summary>
        public string Encabezado { get; set; }
    }
}
