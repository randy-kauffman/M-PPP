using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization;
using Sanic.Mppp.Plugins.Plantilla;

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
            // Igual que ConfiguracionPlantilla.DesdeJson: el orden de columnas es obligatorio, no hay "valor por
            // defecto" razonable para qué campos exportar. Un parámetro ausente o vacío es tan inválido como uno
            // mal escrito.
            if (string.IsNullOrWhiteSpace(json))
                throw new FormatException("El parámetro de columnas de la exportación está vacío.");

            ConfiguracionExportacionDto dto;
            try
            {
                dto = LimitesLectura.DeserializarJson<ConfiguracionExportacionDto>(json);
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException || ex is StackOverflowException || ex is System.Threading.ThreadAbortException))
            {
                throw new FormatException($"El parámetro de columnas de la exportación no es un JSON válido: {ex.Message}", ex);
            }

            if (dto == null)
                throw new FormatException("El parámetro de columnas de la exportación no es un JSON válido.");

            // Se arman "en crudo" (sin adivinar nada) y se validan con Invalidez, la MISMA función que usa
            // ArmadorExportacionOpenXml.Armar para una configuración armada a mano.
            var columnas = new List<ColumnaExportacion>();
            if (dto.Columnas != null)
            {
                foreach (ColumnaExportacionDto columnaDto in dto.Columnas)
                {
                    columnas.Add(columnaDto == null
                        ? null
                        : new ColumnaExportacion { Campo = columnaDto.Campo, Encabezado = columnaDto.Encabezado });
                }
            }

            var configuracion = new ConfiguracionExportacion { Columnas = columnas };

            string motivo = Invalidez(configuracion);
            if (motivo != null)
                throw new FormatException($"El parámetro de columnas de la exportación {motivo}");

            return configuracion;
        }

        /// <summary>
        /// ÚNICA validación de una <see cref="ConfiguracionExportacion"/> ya armada, la use quien la use: <see cref="DesdeJson"/>
        /// (la traduce a <see cref="FormatException"/>) o <see cref="ArmadorExportacionOpenXml.Armar"/> (la traduce a
        /// <see cref="ArgumentException"/>, para una configuración armada a mano). Devuelve el motivo del primer problema
        /// encontrado, o null si la configuración es válida. A propósito NO valida que <see cref="ColumnaExportacion.Campo"/>
        /// sea uno de un conjunto conocido (DN-02: el código no tiene una lista propia de campos permitidos ni prohibidos):
        /// solo mira la FORMA de la configuración, nunca el CONTENIDO del nombre del campo.
        /// </summary>
        internal static string Invalidez(ConfiguracionExportacion configuracion)
        {
            if (configuracion.Columnas == null || configuracion.Columnas.Count == 0)
                return "no trae ninguna columna.";

            var camposVistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int indice = 0; indice < configuracion.Columnas.Count; indice++)
            {
                ColumnaExportacion columna = configuracion.Columnas[indice];
                int posicion = indice + 1; // 1-based: todo motivo que se refiera a una columna dice cuál es.

                if (columna == null)
                    return $"trae una columna nula en la posición {posicion.ToString(CultureInfo.InvariantCulture)}.";

                if (string.IsNullOrWhiteSpace(columna.Campo))
                    return $"trae una columna sin nombre de campo en la posición {posicion.ToString(CultureInfo.InvariantCulture)}.";

                if (!camposVistos.Add(columna.Campo))
                    return $"repite el campo '{columna.Campo}'.";

                if (string.IsNullOrWhiteSpace(columna.Encabezado))
                    return $"trae el campo '{columna.Campo}' sin encabezado.";
            }

            return null;
        }

        [DataContract]
        private sealed class ConfiguracionExportacionDto
        {
            [DataMember(Name = "columnas")]
            public List<ColumnaExportacionDto> Columnas { get; set; }
        }

        [DataContract]
        private sealed class ColumnaExportacionDto
        {
            [DataMember(Name = "campo")]
            public string Campo { get; set; }

            [DataMember(Name = "encabezado")]
            public string Encabezado { get; set; }
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
