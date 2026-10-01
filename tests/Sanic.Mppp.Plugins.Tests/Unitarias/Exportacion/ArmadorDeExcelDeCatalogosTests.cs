using System;
using System.Collections.Generic;
using System.Linq;
using Sanic.Mppp.Plugins.Exportacion;
using Sanic.Mppp.Plugins.Plantilla;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Unitarias.Exportacion
{
    /// <summary>Bordes del armado de los Excel de catálogos que la aceptación no cubre (DN-04, pieza 4b).</summary>
    public class ArmadorDeExcelDeCatalogosTests
    {
        private static FilaPlantilla Fila(int numero, IDictionary<string, string> valores)
            => new FilaPlantilla { NumeroFilaExcel = numero, NumeroOrden = numero - 1, Valores = valores };

        private static IReadOnlyDictionary<int, string> Textos(params (int fila, string texto)[] t)
            => t.ToDictionary(x => x.fila, x => x.texto);

        [Fact]
        public void Una_fila_sin_Valores_es_error()
        {
            var e = EstructuraDeExcelDeCatalogo.Carga;
            Assert.Throws<ArgumentException>(() => ArmadorDeExcelDeCatalogos.Resultado(e, new[] { Fila(2, null) }, Textos((2, "x"))));
        }

        [Fact]
        public void Una_letra_que_no_es_columna_de_la_estructura_es_error_y_no_se_descarta_en_silencio()
        {
            var e = EstructuraDeExcelDeCatalogo.CambioDeEstado; // A–G: la H no existe
            var valores = new Dictionary<string, string> { ["A"] = "CLIENTE", ["H"] = "perdido" };
            Assert.Throws<ArgumentException>(() => ArmadorDeExcelDeCatalogos.Resultado(e, new[] { Fila(2, valores) }, Textos((2, "x"))));
        }

        private static Dictionary<string, string> CeldasPorReferencia(byte[] archivo, string hoja)
        {
            var r = new Dictionary<string, string>(StringComparer.Ordinal);
            using (var ms = new System.IO.MemoryStream(archivo))
            using (var doc = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(ms, false))
            {
                var wb = doc.WorkbookPart;
                var sheet = wb.Workbook.Descendants<DocumentFormat.OpenXml.Spreadsheet.Sheet>().Single(x => x.Name == hoja);
                var ws = (DocumentFormat.OpenXml.Packaging.WorksheetPart)wb.GetPartById(sheet.Id);
                var compartidos = wb.SharedStringTablePart.SharedStringTable.Elements<DocumentFormat.OpenXml.Spreadsheet.SharedStringItem>().Select(i => i.InnerText).ToArray();
                foreach (var c in ws.Worksheet.Descendants<DocumentFormat.OpenXml.Spreadsheet.Cell>())
                    r[c.CellReference.Value] = compartidos[int.Parse(c.CellValue.Text)];
            }

            return r;
        }

        [Fact]
        public void Un_caracter_que_XML_no_admite_se_reemplaza_por_U_FFFD_y_no_tira_el_armado()
        {
            var e = EstructuraDeExcelDeCatalogo.Carga;
            var valores = new Dictionary<string, string> { ["C"] = "Cliente\u0001" };
            var archivo = ArmadorDeExcelDeCatalogos.Resultado(e, new[] { Fila(2, valores) }, Textos((2, "mal\u0000")));
            var celdas = CeldasPorReferencia(archivo, e.Hoja);
            Assert.Equal("Cliente\uFFFD", celdas["C2"]);
            Assert.Equal("mal\uFFFD", celdas["L2"]);
        }

        [Fact]
        public void El_acortado_del_eco_no_parte_un_par_subrogado()
        {
            var e = EstructuraDeExcelDeCatalogo.Carga;
            // El emoji (2 chars) queda justo en las posiciones 32765-32766: cortar en 32766 dejaría solo su mitad alta.
            string largo = new string('x', 32765) + "😀" + new string('y', 100);
            var archivo = ArmadorDeExcelDeCatalogos.Resultado(e, new[] { Fila(2, new Dictionary<string, string> { ["C"] = largo }) }, Textos((2, "x")));
            string c2 = CeldasPorReferencia(archivo, e.Hoja)["C2"];
            Assert.True(c2.Length <= 32767);
            Assert.EndsWith("…", c2);
            Assert.False(char.IsHighSurrogate(c2[c2.Length - 2]), "no puede quedar media pareja antes de los puntos suspensivos");
            Assert.Equal(new string('x', 32765) + "…", c2);
        }

        [Fact]
        public void Un_resultado_de_mas_de_32767_caracteres_sigue_siendo_error_aunque_el_eco_se_acorte()
        {
            var e = EstructuraDeExcelDeCatalogo.Carga;
            var valores = new Dictionary<string, string> { ["C"] = new string('c', 40000) };
            Assert.Throws<ArgumentException>(() => ArmadorDeExcelDeCatalogos.Resultado(e, new[] { Fila(2, valores) }, Textos((2, new string('r', 32768)))));
        }

        [Fact]
        public void Un_resultado_nulo_o_en_blanco_deja_la_celda_sin_escribir_y_la_fila_se_puede_releer()
        {
            var e = EstructuraDeExcelDeCatalogo.Carga;
            var archivo = ArmadorDeExcelDeCatalogos.Resultado(
                e,
                new[] { Fila(2, new Dictionary<string, string> { ["A"] = "1", ["B"] = null }) },
                new Dictionary<int, string> { [2] = null });
            var lectura = new LectorOpenXml().Leer(archivo, e.ConfiguracionDeLectura(10));
            Assert.True(lectura.EsValido, string.Join(" | ", lectura.Errores));
            Assert.Equal("1", Assert.Single(lectura.Filas).Valores["A"]);
        }

        [Fact]
        public void Un_numero_de_fila_que_no_cabe_en_una_hoja_de_Excel_es_error()
        {
            var e = EstructuraDeExcelDeCatalogo.Carga;
            var fila = Fila(1048577, new Dictionary<string, string> { ["A"] = "1" });
            Assert.Throws<ArgumentException>(() => ArmadorDeExcelDeCatalogos.Resultado(e, new[] { fila }, Textos((1048577, "x"))));
        }
    }
}
