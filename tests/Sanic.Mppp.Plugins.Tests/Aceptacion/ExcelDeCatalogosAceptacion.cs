using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Sanic.Mppp.Plugins.Catalogos;
using Sanic.Mppp.Plugins.Exportacion;
using Sanic.Mppp.Plugins.Plantilla;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Aceptacion
{
    /// <summary>
    /// DN-04: la forma de los dos Excel de catálogos y su armado (plantilla vacía y archivo de resultado). La garantía central:
    /// lo que se arma, el lector estricto lo lee sin rechazo (bajar, corregir y volver a subir). Pruebas del ARQUITECTO.
    /// </summary>
    public class ExcelDeCatalogosAceptacion
    {
        public static IEnumerable<object[]> Estructuras()
        {
            yield return new object[] { "carga" };
            yield return new object[] { "cambio" };
        }

        private static EstructuraDeExcelDeCatalogo De(string cual) => cual == "carga" ? EstructuraDeExcelDeCatalogo.Carga : EstructuraDeExcelDeCatalogo.CambioDeEstado;

        private static FilaPlantilla Fila(int numero, params (string letra, string valor)[] valores)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (l, v) in valores) d[l] = v;
            return new FilaPlantilla { NumeroFilaExcel = numero, NumeroOrden = numero - 1, Valores = d };
        }

        private static ResultadoLecturaPlantilla Releer(byte[] archivo, EstructuraDeExcelDeCatalogo e, int maximo = 300)
            => new LectorOpenXml().Leer(archivo, e.ConfiguracionDeLectura(maximo));

        /// <summary>Todas las celdas de la hoja, por referencia, con su tipo y si traen fórmula.</summary>
        private static Dictionary<string, (string texto, CellValues? tipo, bool formula)> Celdas(byte[] archivo, string hoja)
        {
            var resultado = new Dictionary<string, (string, CellValues?, bool)>(StringComparer.Ordinal);
            using (var ms = new MemoryStream(archivo))
            using (var doc = SpreadsheetDocument.Open(ms, false))
            {
                var wb = doc.WorkbookPart;
                var sheet = wb.Workbook.Descendants<Sheet>().Single(s => s.Name == hoja);
                var ws = (WorksheetPart)wb.GetPartById(sheet.Id);
                var compartidos = wb.SharedStringTablePart?.SharedStringTable.Elements<SharedStringItem>().Select(i => i.InnerText).ToArray() ?? new string[0];
                foreach (var c in ws.Worksheet.Descendants<Cell>())
                {
                    var tipo = c.DataType?.Value;
                    string texto = tipo == CellValues.SharedString ? compartidos[int.Parse(c.CellValue.Text)]
                        : tipo == CellValues.InlineString ? c.InlineString.InnerText
                        : c.CellValue?.Text;
                    resultado[c.CellReference.Value] = (texto, tipo, c.CellFormula != null);
                }
            }

            return resultado;
        }

        // ================================================================================================ estructuras
        [Fact]
        public void La_estructura_de_la_carga_son_las_once_columnas_mas_Resultado_en_L()
        {
            var e = EstructuraDeExcelDeCatalogo.Carga;
            Assert.Equal("CargaCatalogos", e.Hoja);
            Assert.Equal(ColumnasDeCarga.Todas.Select(c => (c.Letra, c.Campo, c.Encabezado)).Concat(new[] { ("L", "resultado", "Resultado") }),
                e.Columnas.Select(c => (c.Letra, c.Campo, c.Encabezado)));
            Assert.Equal("L", e.ColumnaResultado.Letra);
            Assert.Throws<NotSupportedException>(() => ((ICollection<ColumnaDeCatalogo>)e.Columnas).Clear());
        }

        [Fact]
        public void La_estructura_del_cambio_de_estado_son_las_seis_columnas_mas_Resultado_en_G()
        {
            var e = EstructuraDeExcelDeCatalogo.CambioDeEstado;
            Assert.Equal("ActivarDesactivar", e.Hoja);
            Assert.Equal(ColumnasDeCambioDeEstado.Todas.Select(c => (c.Letra, c.Campo, c.Encabezado)).Concat(new[] { ("G", "resultado", "Resultado") }),
                e.Columnas.Select(c => (c.Letra, c.Campo, c.Encabezado)));
            Assert.Equal("G", e.ColumnaResultado.Letra);
        }

        [Theory]
        [MemberData(nameof(Estructuras))]
        public void La_configuracion_de_lectura_es_estricta_desde_la_fila_2_y_nueva_en_cada_llamada(string cual)
        {
            var e = De(cual);
            var c = e.ConfiguracionDeLectura(300);
            Assert.Equal((e.Hoja, 1, 2, 300, true), (c.Hoja, c.FilaEncabezado, c.PrimeraFila, c.CantidadFilas, c.EstructuraEstricta));
            Assert.Equal(e.Columnas.Select(x => (x.Letra, x.Encabezado)), c.Campos.Select(x => (x.Columna, x.EncabezadoEsperado)));

            c.CantidadFilas = 1;
            c.Campos.Clear();
            var otra = e.ConfiguracionDeLectura(300);
            Assert.Equal(300, otra.CantidadFilas);
            Assert.Equal(e.Columnas.Count, otra.Campos.Count);

            Assert.Throws<ArgumentOutOfRangeException>(() => e.ConfiguracionDeLectura(0));
        }

        // ================================================================================================ plantilla vacía
        [Theory]
        [MemberData(nameof(Estructuras))]
        public void La_plantilla_vacia_trae_solo_los_encabezados_y_el_lector_estricto_la_acepta_sin_filas(string cual)
        {
            var e = De(cual);
            var archivo = ArmadorDeExcelDeCatalogos.Plantilla(e);

            var lectura = Releer(archivo, e);
            Assert.True(lectura.EsValido, string.Join(" | ", lectura.Errores));
            Assert.Empty(lectura.Filas);

            var celdas = Celdas(archivo, e.Hoja);
            Assert.Equal(e.Columnas.Select(c => (c.Letra + "1", c.Encabezado)), celdas.Select(kv => (kv.Key, kv.Value.texto)));
        }

        // ================================================================================================ resultado
        [Fact]
        public void El_resultado_va_en_la_misma_fila_con_los_valores_leidos_y_se_puede_volver_a_subir()
        {
            var e = EstructuraDeExcelDeCatalogo.Carga;
            var filas = new[]
            {
                Fila(5, ("A", "123"), ("H", "juan@empresa.com"), ("L", "un resultado viejo que se reemplaza")),
                Fila(2, ("A", "000123"), ("C", "=SUMA(A1)"), ("K", "  ")),
                Fila(3, ("B", "ABC123456001"), ("E", "0A12")),
            };
            var textos = new Dictionary<int, string>
            {
                [2] = "Creado: cliente", [3] = "Error: Codigo plan: algo", [5] = "Sin errores — no se grabó: el archivo tiene errores en otras filas",
            };
            var archivo = ArmadorDeExcelDeCatalogos.Resultado(e, filas, textos);

            var celdas = Celdas(archivo, e.Hoja);
            Assert.Equal("000123", celdas["A2"].texto);   // los ceros a la izquierda se conservan: es texto, no número
            Assert.Equal("=SUMA(A1)", celdas["C2"].texto);
            Assert.All(celdas.Values, c => Assert.False(c.formula, "ninguna celda puede ser fórmula"));
            Assert.All(celdas.Values, c => Assert.True(c.tipo == CellValues.SharedString || c.tipo == CellValues.InlineString, "toda celda es texto"));
            Assert.False(celdas.ContainsKey("K2"), "una celda en blanco no se escribe");
            Assert.Equal("Creado: cliente", celdas["L2"].texto);
            Assert.Equal("Error: Codigo plan: algo", celdas["L3"].texto);
            Assert.StartsWith("Sin errores", celdas["L5"].texto);
            Assert.Equal("juan@empresa.com", celdas["H5"].texto);

            // Bajar, corregir y volver a subir: el lector estricto lo acepta y lee los mismos valores en las mismas filas.
            var lectura = Releer(archivo, e);
            Assert.True(lectura.EsValido, string.Join(" | ", lectura.Errores));
            Assert.Equal(new[] { 2, 3, 5 }, lectura.Filas.Select(f => f.NumeroFilaExcel).ToArray());
            Assert.Equal("000123", lectura.Filas[0].Valores["A"]);
            Assert.Equal("=SUMA(A1)", lectura.Filas[0].Valores["C"]);
            Assert.Equal("0A12", lectura.Filas[1].Valores["E"]);
        }

        [Fact]
        public void El_resultado_del_cambio_de_estado_tambien_se_puede_volver_a_subir()
        {
            var e = EstructuraDeExcelDeCatalogo.CambioDeEstado;
            var archivo = ArmadorDeExcelDeCatalogos.Resultado(e,
                new[] { Fila(2, ("A", "CLIENTE"), ("B", "DESACTIVAR"), ("C", "123")) },
                new Dictionary<int, string> { [2] = "Desactivado" });
            Assert.Equal("Desactivado", Celdas(archivo, e.Hoja)["G2"].texto);
            var lectura = Releer(archivo, e);
            Assert.True(lectura.EsValido, string.Join(" | ", lectura.Errores));
            Assert.Equal("CLIENTE", Assert.Single(lectura.Filas).Valores["A"]);
        }

        [Fact]
        public void Filas_y_resultados_tienen_que_corresponderse_uno_a_uno()
        {
            var e = EstructuraDeExcelDeCatalogo.Carga;
            var dos = new[] { Fila(2, ("A", "1")) };
            Assert.ThrowsAny<ArgumentException>(() => ArmadorDeExcelDeCatalogos.Resultado(e, dos, new Dictionary<int, string>()));
            Assert.ThrowsAny<ArgumentException>(() => ArmadorDeExcelDeCatalogos.Resultado(e, dos, new Dictionary<int, string> { [2] = "x", [3] = "y" }));
            Assert.ThrowsAny<ArgumentException>(() => ArmadorDeExcelDeCatalogos.Resultado(e, new[] { Fila(2, ("A", "1")), Fila(2, ("A", "2")) }, new Dictionary<int, string> { [2] = "x" }));
            Assert.ThrowsAny<ArgumentException>(() => ArmadorDeExcelDeCatalogos.Resultado(e, new[] { Fila(1, ("A", "1")) }, new Dictionary<int, string> { [1] = "x" }));
            Assert.ThrowsAny<ArgumentException>(() => ArmadorDeExcelDeCatalogos.Resultado(e, new[] { Fila(2, ("A", "1")), null }, new Dictionary<int, string> { [2] = "x" }));
            Assert.Throws<ArgumentNullException>(() => ArmadorDeExcelDeCatalogos.Resultado(null, dos, new Dictionary<int, string> { [2] = "x" }));
            Assert.Throws<ArgumentNullException>(() => ArmadorDeExcelDeCatalogos.Resultado(e, null, new Dictionary<int, string>()));
            Assert.Throws<ArgumentNullException>(() => ArmadorDeExcelDeCatalogos.Resultado(e, dos, null));
            Assert.Throws<ArgumentNullException>(() => ArmadorDeExcelDeCatalogos.Plantilla(null));
        }

        [Fact]
        public void Un_texto_de_mas_de_32767_caracteres_es_error_y_uno_de_32767_no()
        {
            // Literal (Learn, "Excel specifications and limits"), no la constante del código; y aparte se afirma la constante.
            Assert.Equal(32767, ArmadorDeExcelDeCatalogos.MaximoDeCaracteresPorCelda);
            var e = EstructuraDeExcelDeCatalogo.Carga;
            var filas = new[] { Fila(2, ("A", "1")) };
            ArmadorDeExcelDeCatalogos.Resultado(e, filas, new Dictionary<int, string> { [2] = new string('r', 32767) });
            Assert.ThrowsAny<ArgumentException>(() => ArmadorDeExcelDeCatalogos.Resultado(e, filas, new Dictionary<int, string> { [2] = new string('r', 32768) }));
        }

        [Fact]
        public void Un_valor_del_usuario_de_mas_de_32767_caracteres_se_escribe_acortado_en_el_eco_sin_fallar()
        {
            // Revisión 4b (2026-10-01): el lector limita bytes, no celdas; una celda hostil enorme llega hasta acá. Su fila ya tiene
            // error en el planificador y el eco es solo para que el usuario la vea (nunca se graba): se acorta, no tira la API.
            var e = EstructuraDeExcelDeCatalogo.Carga;
            var archivo = ArmadorDeExcelDeCatalogos.Resultado(e, new[] { Fila(2, ("C", new string('c', 40000))) }, new Dictionary<int, string> { [2] = "Error: Razon social: tiene 40000 caracteres" });
            var c2 = Celdas(archivo, e.Hoja)["C2"].texto;
            Assert.Equal(32767, c2.Length);
            Assert.EndsWith("…", c2);
            Assert.True(Releer(archivo, e).EsValido);
        }

        [Fact]
        public void Un_caracter_que_XML_no_admite_se_reemplaza_y_nunca_tira_el_armado()
        {
            // Revisión 4b: un valor leído nunca lo trae (el lector rechaza el archivo antes), pero un mensaje armado con datos de
            // Dataverse sí podría. Se reemplaza por U+FFFD; los pares subrogados válidos (un emoji) se conservan.
            var e = EstructuraDeExcelDeCatalogo.Carga;
            var archivo = ArmadorDeExcelDeCatalogos.Resultado(e, new[] { Fila(2, ("C", "a\u0001b 😀")) }, new Dictionary<int, string> { [2] = "Error: x\u0002y \uD800 z" });
            var celdas = Celdas(archivo, e.Hoja);
            Assert.Equal("a\uFFFDb 😀", celdas["C2"].texto);
            Assert.Equal("Error: x\uFFFDy \uFFFD z", celdas["L2"].texto);
            Assert.True(Releer(archivo, e).EsValido);
        }

        [Fact]
        public void Cinco_mil_filas_se_arman_y_se_releen_rapido()
        {
            var e = EstructuraDeExcelDeCatalogo.Carga;
            var filas = Enumerable.Range(0, 5000).Select(i => Fila(i + 2, ("A", (100000 + i).ToString()), ("C", "Cliente " + i), ("H", $"c{i}@empresa.com"))).ToArray();
            var textos = filas.ToDictionary(f => f.NumeroFilaExcel, f => "Creado: cliente, correo, autorización");
            var reloj = Stopwatch.StartNew();
            var archivo = ArmadorDeExcelDeCatalogos.Resultado(e, filas, textos);
            reloj.Stop();
            Assert.True(reloj.Elapsed < TimeSpan.FromSeconds(5), $"armar tardó {reloj.Elapsed}");
            var lectura = Releer(archivo, e, 5000);
            Assert.True(lectura.EsValido, string.Join(" | ", lectura.Errores));
            Assert.Equal(5000, lectura.Filas.Count);
        }
    }
}
