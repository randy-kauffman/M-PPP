using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Sanic.Mppp.Plugins.Tests.Apoyo
{
    /// <summary>
    /// Lee de vuelta, celda por celda, un .xlsx que devolvió ArmadorExportacionOpenXml — SOLO para que los tests puedan
    /// verificar lo que se escribió. No duplica el lector de producción (LectorOpenXml valida una VENTANA configurada
    /// con encabezado esperado; acá se necesita leer TODA la hoja tal cual salió, sin esa validación).
    /// </summary>
    internal static class LectorCrudoDeExportacion
    {
        /// <summary>Todas las filas de la hoja pedida, cada una como lista de celdas en orden de columna físico.</summary>
        public static IList<FilaCruda> LeerFilas(byte[] xlsx, string nombreHoja)
        {
            using (var stream = new MemoryStream(xlsx))
            using (var documento = SpreadsheetDocument.Open(stream, isEditable: false))
            {
                WorkbookPart workbookPart = documento.WorkbookPart;
                Sheet hoja = workbookPart.Workbook.Descendants<Sheet>()
                    .First(s => s.Name != null && s.Name.Value == nombreHoja);
                var worksheetPart = (WorksheetPart)workbookPart.GetPartById(hoja.Id);

                string[] compartidas = workbookPart.SharedStringTablePart != null
                    ? workbookPart.SharedStringTablePart.SharedStringTable.Elements<SharedStringItem>()
                        .Select(item => item.InnerText).ToArray()
                    : new string[0];

                var filas = new List<FilaCruda>();
                foreach (Row fila in worksheetPart.Worksheet.Descendants<Row>())
                {
                    var celdas = new List<CeldaCruda>();
                    foreach (Cell celda in fila.Elements<Cell>())
                        celdas.Add(Leer(celda, compartidas));

                    filas.Add(new FilaCruda { Celdas = celdas });
                }

                return filas;
            }
        }

        /// <summary>Nombres de todas las hojas del archivo, en el orden en que aparecen.</summary>
        public static IList<string> NombresDeHojas(byte[] xlsx)
        {
            using (var stream = new MemoryStream(xlsx))
            using (var documento = SpreadsheetDocument.Open(stream, isEditable: false))
            {
                return documento.WorkbookPart.Workbook.Descendants<Sheet>()
                    .Select(s => s.Name != null ? s.Name.Value : null)
                    .ToList();
            }
        }

        private static CeldaCruda Leer(Cell celda, string[] compartidas)
        {
            string referencia = celda.CellReference != null ? celda.CellReference.Value : null;
            string texto;
            bool esTexto;

            if (celda.DataType != null && celda.DataType.Value == CellValues.SharedString)
            {
                int indice = int.Parse(celda.CellValue.Text, NumberStyles.Integer, CultureInfo.InvariantCulture);
                texto = compartidas[indice];
                esTexto = true;
            }
            else if (celda.DataType != null && celda.DataType.Value == CellValues.InlineString)
            {
                texto = celda.InlineString != null
                    ? (celda.InlineString.Text != null ? celda.InlineString.Text.Text : celda.InlineString.InnerText)
                    : null;
                esTexto = true;
            }
            else if (celda.DataType != null && celda.DataType.Value == CellValues.String)
            {
                texto = celda.CellValue != null ? celda.CellValue.Text : null;
                esTexto = true;
            }
            else
            {
                // Sin t="s"/"str"/"inlineStr": Excel lo interpreta como número.
                texto = celda.CellValue != null ? celda.CellValue.Text : null;
                esTexto = false;
            }

            return new CeldaCruda { Referencia = referencia, Texto = texto, EsTexto = esTexto };
        }

        public sealed class FilaCruda
        {
            public IList<CeldaCruda> Celdas { get; set; }
        }

        public sealed class CeldaCruda
        {
            public string Referencia { get; set; }
            public string Texto { get; set; }
            public bool EsTexto { get; set; }
        }
    }
}
