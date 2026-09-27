using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Sanic.Mppp.Plugins.Exportacion
{
    /// <summary>
    /// Arma el .xlsx con Open XML SDK 3.1.0 (DN-02: el Excel se arma en C# con Open XML SDK — la misma librería que
    /// <see cref="Plantilla.LectorOpenXml"/> usa para leer; escribir es la otra mitad de la misma librería, no una
    /// dependencia nueva).
    ///
    /// Rendimiento de ESCRITURA: SIN MEDIR. El spike C-05 (spikes/c05-parseo-excel) midió LECTURA en el sandbox real
    /// de Dataverse (parte B, 6.5 MB descomprimidos); que la librería ya esté no garantiza que armar cientos de filas
    /// entre en el límite de 2 minutos del plugin. Esa medición queda pendiente para cuando exista la Custom API con
    /// volumen real (mismo criterio con el que se midió `rpa.lote.maximo`) — fuera del alcance de este dominio.
    /// </summary>
    public sealed class ArmadorExportacionOpenXml : IArmadorExportacion
    {
        /// <summary>
        /// Nombre de la única hoja del archivo generado. Es una decisión de este código, no del parámetro: DN-02 solo
        /// deja el ORDEN de columnas en manos del negocio, no el nombre de la hoja.
        /// </summary>
        public const string NombreDeHoja = "Filas";

        public byte[] Armar(IList<FilaParaExportar> filas, ConfiguracionExportacion configuracion)
        {
            if (filas == null) throw new ArgumentNullException(nameof(filas));
            if (configuracion == null) throw new ArgumentNullException(nameof(configuracion));

            string motivoConfig = ConfiguracionExportacion.Invalidez(configuracion);
            if (motivoConfig != null)
                throw new ArgumentException($"La configuración de exportación {motivoConfig}", nameof(configuracion));

            // Resuelve el texto de cada celda ANTES de tocar el SDK: a diferencia de LectorOpenXml (que lee un
            // archivo hostil de fuera del banco), acá quien llama a este dominio YA resolvió las filas — una fila sin
            // el campo que pide la configuración es un error de programación/wiring, no un archivo inválido, así que
            // se valida por fuera de cualquier manejo de excepciones del SDK.
            var textoPorFila = new List<string[]>(filas.Count);
            for (int indiceFila = 0; indiceFila < filas.Count; indiceFila++)
            {
                FilaParaExportar fila = filas[indiceFila];
                int numeroDeFila = indiceFila + 1; // 1-based para el mensaje

                if (fila == null || fila.Valores == null)
                    throw new ArgumentException(
                        $"La fila en la posición {numeroDeFila.ToString(CultureInfo.InvariantCulture)} no trae valores.",
                        nameof(filas));

                var valoresDeLaFila = new string[configuracion.Columnas.Count];
                for (int indiceColumna = 0; indiceColumna < configuracion.Columnas.Count; indiceColumna++)
                {
                    string campo = configuracion.Columnas[indiceColumna].Campo;

                    // Un campo que el parámetro nombra y esta fila no trae como CLAVE es un error de configuración
                    // ruidoso (DN-02): nunca una columna vacía en silencio. Un valor presente pero nulo o vacío sí es
                    // una celda vacía legítima (se resuelve más abajo).
                    if (!fila.Valores.TryGetValue(campo, out string valor))
                        throw new ArgumentException(
                            $"La fila en la posición {numeroDeFila.ToString(CultureInfo.InvariantCulture)} no trae el campo '{campo}' que pide la configuración de exportación.",
                            nameof(filas));

                    valoresDeLaFila[indiceColumna] = valor ?? string.Empty;
                }

                textoPorFila.Add(valoresDeLaFila);
            }

            return ArmarXlsx(configuracion, textoPorFila);
        }

        private static byte[] ArmarXlsx(ConfiguracionExportacion configuracion, IList<string[]> textoPorFila)
        {
            using (var stream = new MemoryStream())
            {
                using (var documento = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
                {
                    WorkbookPart workbookPart = documento.AddWorkbookPart();
                    workbookPart.Workbook = new Workbook();

                    var sharedStringPart = workbookPart.AddNewPart<SharedStringTablePart>();
                    sharedStringPart.SharedStringTable = new SharedStringTable();
                    var indicesCompartidos = new Dictionary<string, int>(StringComparer.Ordinal);

                    var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                    var sheetData = new SheetData();
                    worksheetPart.Worksheet = new Worksheet(sheetData);

                    var filaEncabezado = new Row { RowIndex = 1 };
                    for (int indiceColumna = 0; indiceColumna < configuracion.Columnas.Count; indiceColumna++)
                    {
                        string referencia = NumeroAColumna(indiceColumna + 1) + "1";
                        filaEncabezado.AppendChild(CeldaDeTexto(
                            referencia, configuracion.Columnas[indiceColumna].Encabezado, sharedStringPart, indicesCompartidos));
                    }
                    sheetData.AppendChild(filaEncabezado);

                    for (int indiceFila = 0; indiceFila < textoPorFila.Count; indiceFila++)
                    {
                        int numeroFilaExcel = indiceFila + 2; // la 1 es el encabezado
                        var filaExcel = new Row { RowIndex = (uint)numeroFilaExcel };
                        string[] valores = textoPorFila[indiceFila];

                        for (int indiceColumna = 0; indiceColumna < valores.Length; indiceColumna++)
                        {
                            string referencia = NumeroAColumna(indiceColumna + 1) + numeroFilaExcel.ToString(CultureInfo.InvariantCulture);
                            filaExcel.AppendChild(CeldaDeTexto(referencia, valores[indiceColumna], sharedStringPart, indicesCompartidos));
                        }

                        sheetData.AppendChild(filaExcel);
                    }

                    var sheets = workbookPart.Workbook.AppendChild(new Sheets());
                    sheets.AppendChild(new Sheet
                    {
                        Id = workbookPart.GetIdOfPart(worksheetPart),
                        SheetId = 1,
                        Name = NombreDeHoja
                    });

                    workbookPart.Workbook.Save();
                }

                return stream.ToArray();
            }
        }

        /// <summary>
        /// Arma una celda de texto (t="s", shared string) — NUNCA una celda numérica. Esto no es una preferencia de
        /// formato: es lo único que hace que el archivo sirva para lo que se hizo. Una referencia de un plan formato
        /// 11 puede traer HASTA 20 DÍGITOS (ej. "00700012345678901234"). Si esa celda quedara como número, Excel la
        /// muestra en notación científica y le COME LOS ÚLTIMOS DÍGITOS por el límite de precisión de un double
        /// (~15-17 dígitos significativos) — sin que nada falle. El ejecutivo copia esa referencia ya rota a AS400 y
        /// digita algo que no existe, y nadie se entera hasta que el cliente reclama. Lo mismo pasa con
        /// numeroCuenta y sus ceros a la izquierda: un valor NUMÉRICO en Excel nunca los conserva. Un shared string,
        /// en cambio, es exactamente la secuencia de caracteres que se le dio: Excel no la reinterpreta nunca. Que
        /// nadie "mejore" esto a celdas numéricas.
        /// </summary>
        private static Cell CeldaDeTexto(string referencia, string valor, SharedStringTablePart sharedStringPart, Dictionary<string, int> indicesCompartidos)
        {
            string texto = valor ?? string.Empty;

            if (!indicesCompartidos.TryGetValue(texto, out int indice))
            {
                indice = indicesCompartidos.Count;
                indicesCompartidos[texto] = indice;
                var elementoDeTexto = new Text(texto);
                if (TieneEspaciosAlBordeOEstaVacio(texto))
                    elementoDeTexto.Space = SpaceProcessingModeValues.Preserve;

                sharedStringPart.SharedStringTable.AppendChild(new SharedStringItem(elementoDeTexto));
            }

            return new Cell
            {
                CellReference = referencia,
                DataType = CellValues.SharedString,
                CellValue = new CellValue(indice.ToString(CultureInfo.InvariantCulture))
            };
        }

        private static bool TieneEspaciosAlBordeOEstaVacio(string texto)
        {
            return texto.Length == 0 || char.IsWhiteSpace(texto[0]) || char.IsWhiteSpace(texto[texto.Length - 1]);
        }

        // LP-06 de LectorOpenXml, misma aritmética base-26 de columnas de Excel (A=1, B=2, ..., Z=26, AA=27...), pero
        // acá NUNCA se lee una columna: siempre se GENERA a partir de la posición en la configuración.
        private static string NumeroAColumna(int numero)
        {
            var letras = new System.Text.StringBuilder();
            while (numero > 0)
            {
                int resto = (numero - 1) % 26;
                letras.Insert(0, (char)('A' + resto));
                numero = (numero - 1) / 26;
            }
            return letras.ToString();
        }
    }
}
