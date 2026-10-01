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

        [Fact]
        public void Un_caracter_que_XML_no_admite_es_error_antes_de_tocar_el_SDK()
        {
            var e = EstructuraDeExcelDeCatalogo.Carga;
            var valores = new Dictionary<string, string> { ["C"] = "Cliente\u0001" };
            Assert.Throws<ArgumentException>(() => ArmadorDeExcelDeCatalogos.Resultado(e, new[] { Fila(2, valores) }, Textos((2, "x"))));
            Assert.Throws<ArgumentException>(() => ArmadorDeExcelDeCatalogos.Resultado(e, new[] { Fila(2, new Dictionary<string, string>()) }, Textos((2, "mal\u0000"))));
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
