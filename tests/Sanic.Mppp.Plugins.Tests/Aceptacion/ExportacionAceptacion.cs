using System;
using System.Collections.Generic;
using System.Linq;
using Sanic.Mppp.Plugins.Exportacion;
using Sanic.Mppp.Plugins.Tests.Apoyo;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Aceptacion
{
    /// <summary>
    /// DN-02 (diseno/DIRECTRICES-NUEVAS.md): parámetro `exportacion.columnas` y el armado del .xlsx de exportación.
    /// Alcance de ESTA etapa: solo el dominio — leer el parámetro y armar el archivo a partir de filas ya resueltas.
    /// Nada de Dataverse, Custom API, ribbon ni DN-01 acá.
    /// </summary>
    public class ExportacionAceptacion
    {
        // ------------------------------------------------------------------ helpers de armado de JSON/filas para los tests
        private static string JsonDeColumnas(params (string campo, string encabezado)[] columnas)
        {
            var items = columnas.Select(c => $@"{{""campo"":""{c.campo}"",""encabezado"":""{c.encabezado}""}}");
            return @"{""columnas"":[" + string.Join(",", items) + "]}";
        }

        private static ConfiguracionExportacion Config(params (string campo, string encabezado)[] columnas)
        {
            return ConfiguracionExportacion.DesdeJson(JsonDeColumnas(columnas));
        }

        private static FilaParaExportar Fila(params (string campo, string valor)[] valores)
        {
            var diccionario = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (campo, valor) in valores) diccionario[campo] = valor;
            return new FilaParaExportar { Valores = diccionario };
        }

        // ------------------------------------------------------------------ exportacion.columnas: forma y orden
        [Fact]
        public void El_orden_del_arreglo_es_el_orden_de_las_columnas_y_cada_una_trae_campo_y_encabezado()
        {
            var config = Config(("numeroPlan", "No. Plan"), ("nombreBeneficiario", "Nombre"), ("numeroCuenta", "Cuenta"));

            Assert.Equal(3, config.Columnas.Count);
            Assert.Equal("numeroPlan", config.Columnas[0].Campo);
            Assert.Equal("No. Plan", config.Columnas[0].Encabezado);
            Assert.Equal("nombreBeneficiario", config.Columnas[1].Campo);
            Assert.Equal("Nombre", config.Columnas[1].Encabezado);
            Assert.Equal("numeroCuenta", config.Columnas[2].Campo);
            Assert.Equal("Cuenta", config.Columnas[2].Encabezado);
        }

        [Fact]
        public void Cualquier_nombre_de_campo_es_valido_el_codigo_no_tiene_una_lista_propia_de_campos()
        {
            // DN-02: "Qué columnas salen lo decide ese parámetro, no el código". No hay whitelist ni blacklist acá:
            // ni siquiera CamposDeFila.Todos() se consulta.
            var config = Config(("unCampoQueNoExisteEnNingunLado", "Rara"));
            Assert.Equal("unCampoQueNoExisteEnNingunLado", config.Columnas[0].Campo);
        }

        public static IEnumerable<object[]> ColumnasMalCargadas()
        {
            yield return new object[] { "nulo", null };
            yield return new object[] { "en blanco", "   " };
            yield return new object[] { "no es JSON", "{columnas:" };
            yield return new object[] { "es un arreglo en la raiz, no un objeto", "[]" };
            yield return new object[] { "objeto vacio, sin columnas", "{}" };
            yield return new object[] { "columnas vacia", @"{""columnas"":[]}" };
            yield return new object[] { "elemento nulo", @"{""columnas"":[null]}" };
            yield return new object[] { "campo ausente", @"{""columnas"":[{""encabezado"":""X""}]}" };
            yield return new object[] { "campo vacio", @"{""columnas"":[{""campo"":"""",""encabezado"":""X""}]}" };
            yield return new object[] { "campo en blanco", @"{""columnas"":[{""campo"":""   "",""encabezado"":""X""}]}" };
            yield return new object[] { "encabezado ausente", @"{""columnas"":[{""campo"":""numeroPlan""}]}" };
            yield return new object[] { "encabezado vacio", @"{""columnas"":[{""campo"":""numeroPlan"",""encabezado"":""""}]}" };
            yield return new object[] { "encabezado en blanco", @"{""columnas"":[{""campo"":""numeroPlan"",""encabezado"":""   ""}]}" };
            yield return new object[] { "campo repetido", @"{""columnas"":[{""campo"":""numeroPlan"",""encabezado"":""A""},{""campo"":""numeroPlan"",""encabezado"":""B""}]}" };
            yield return new object[] { "campo repetido con otra mayuscula", @"{""columnas"":[{""campo"":""numeroPlan"",""encabezado"":""A""},{""campo"":""NUMEROPLAN"",""encabezado"":""B""}]}" };
        }

        [Theory]
        [MemberData(nameof(ColumnasMalCargadas))]
        public void Una_configuracion_de_exportacion_mal_cargada_revienta_con_el_motivo(string caso, string json)
        {
            var ex = Assert.Throws<FormatException>(() => ConfiguracionExportacion.DesdeJson(json));
            Assert.False(string.IsNullOrWhiteSpace(ex.Message), caso);
        }

        // ------------------------------------------------------------------ armado del .xlsx
        [Fact]
        public void El_archivo_tiene_una_sola_hoja_con_nombre_fijo()
        {
            var config = Config(("numeroPlan", "Plan"));
            var armador = new ArmadorExportacionOpenXml();

            byte[] xlsx = armador.Armar(new List<FilaParaExportar>(), config);

            Assert.Equal(new[] { ArmadorExportacionOpenXml.NombreDeHoja }, LectorCrudoDeExportacion.NombresDeHojas(xlsx));
        }

        [Fact]
        public void El_encabezado_sale_en_la_primera_fila_en_el_orden_de_la_configuracion()
        {
            var config = Config(("numeroPlan", "No. Plan"), ("nombreBeneficiario", "Nombre"), ("numeroCuenta", "Cuenta"));
            var armador = new ArmadorExportacionOpenXml();

            byte[] xlsx = armador.Armar(new List<FilaParaExportar>(), config);
            var filas = LectorCrudoDeExportacion.LeerFilas(xlsx, ArmadorExportacionOpenXml.NombreDeHoja);

            Assert.Single(filas); // sin filas de datos: solo el encabezado
            Assert.Equal("No. Plan", filas[0].Celdas[0].Texto);
            Assert.Equal("Nombre", filas[0].Celdas[1].Texto);
            Assert.Equal("Cuenta", filas[0].Celdas[2].Texto);
        }

        [Fact]
        public void Cada_fila_trae_los_valores_en_el_mismo_orden_de_columnas_que_el_encabezado_y_en_el_orden_de_la_lista()
        {
            var config = Config(("numeroPlan", "No. Plan"), ("nombreBeneficiario", "Nombre"));
            var filas = new List<FilaParaExportar>
            {
                Fila(("numeroPlan", "123"), ("nombreBeneficiario", "Ana Perez")),
                Fila(("numeroPlan", "456"), ("nombreBeneficiario", "Beto Ruiz")),
            };
            var armador = new ArmadorExportacionOpenXml();

            byte[] xlsx = armador.Armar(filas, config);
            var leidas = LectorCrudoDeExportacion.LeerFilas(xlsx, ArmadorExportacionOpenXml.NombreDeHoja);

            Assert.Equal(3, leidas.Count); // encabezado + 2 filas de datos
            Assert.Equal("123", leidas[1].Celdas[0].Texto);
            Assert.Equal("Ana Perez", leidas[1].Celdas[1].Texto);
            Assert.Equal("456", leidas[2].Celdas[0].Texto);
            Assert.Equal("Beto Ruiz", leidas[2].Celdas[1].Texto);
        }

        [Fact]
        public void El_orden_de_columnas_lo_decide_la_configuracion_no_el_orden_del_diccionario_de_la_fila()
        {
            var config = Config(("numeroCuenta", "Cuenta"), ("numeroPlan", "Plan"));
            // El diccionario se arma en el orden CONTRARIO al de la configuración: no puede importar cómo está armado.
            var fila = new FilaParaExportar
            {
                Valores = new Dictionary<string, string> { ["numeroPlan"] = "999", ["numeroCuenta"] = "111" }
            };
            var armador = new ArmadorExportacionOpenXml();

            byte[] xlsx = armador.Armar(new List<FilaParaExportar> { fila }, config);
            var leidas = LectorCrudoDeExportacion.LeerFilas(xlsx, ArmadorExportacionOpenXml.NombreDeHoja);

            Assert.Equal("111", leidas[1].Celdas[0].Texto); // columna A = numeroCuenta
            Assert.Equal("999", leidas[1].Celdas[1].Texto); // columna B = numeroPlan
        }

        [Fact]
        public void Un_numero_de_cuenta_con_ceros_a_la_izquierda_se_escribe_tal_cual_como_texto_nunca_como_numero()
        {
            var config = Config(("numeroCuenta", "Cuenta"));
            var fila = Fila(("numeroCuenta", "0001234567890123"));
            var armador = new ArmadorExportacionOpenXml();

            byte[] xlsx = armador.Armar(new List<FilaParaExportar> { fila }, config);
            var leidas = LectorCrudoDeExportacion.LeerFilas(xlsx, ArmadorExportacionOpenXml.NombreDeHoja);

            Assert.True(leidas[1].Celdas[0].EsTexto, "la celda no puede quedar como numero: se pierden los ceros a la izquierda");
            Assert.Equal("0001234567890123", leidas[1].Celdas[0].Texto);
        }

        [Fact]
        public void Un_valor_nulo_en_un_campo_que_si_existe_como_clave_es_una_celda_vacia_no_un_error()
        {
            var config = Config(("referencia", "Ref"));
            var fila = new FilaParaExportar
            {
                Valores = new Dictionary<string, string> { ["referencia"] = null }
            };
            var armador = new ArmadorExportacionOpenXml();

            byte[] xlsx = armador.Armar(new List<FilaParaExportar> { fila }, config);
            var leidas = LectorCrudoDeExportacion.LeerFilas(xlsx, ArmadorExportacionOpenXml.NombreDeHoja);

            Assert.True(string.IsNullOrEmpty(leidas[1].Celdas.ElementAtOrDefault(0)?.Texto));
        }

        [Fact]
        public void Un_campo_que_el_parametro_nombra_y_la_fila_no_trae_como_clave_es_un_error_de_configuracion_ruidoso()
        {
            var config = Config(("numeroPlan", "Plan"), ("campoQueNoTraeLaFila", "Rara"));
            var fila = Fila(("numeroPlan", "123")); // no trae "campoQueNoTraeLaFila" como clave
            var armador = new ArmadorExportacionOpenXml();

            var ex = Assert.Throws<ArgumentException>(() => armador.Armar(new List<FilaParaExportar> { fila }, config));
            Assert.Contains("campoQueNoTraeLaFila", ex.Message);
        }

        [Fact]
        public void Una_fila_con_diccionario_de_valores_nulo_es_un_error_de_configuracion_ruidoso()
        {
            var config = Config(("numeroPlan", "Plan"));
            var fila = new FilaParaExportar { Valores = null };
            var armador = new ArmadorExportacionOpenXml();

            Assert.Throws<ArgumentException>(() => armador.Armar(new List<FilaParaExportar> { fila }, config));
        }

        [Fact]
        public void Filas_o_configuracion_nulas_son_un_error_de_programacion()
        {
            var config = Config(("numeroPlan", "Plan"));
            var armador = new ArmadorExportacionOpenXml();

            Assert.Throws<ArgumentNullException>(() => armador.Armar(null, config));
            Assert.Throws<ArgumentNullException>(() => armador.Armar(new List<FilaParaExportar>(), null));
        }

        [Fact]
        public void Una_configuracion_armada_a_mano_sin_columnas_es_un_error_de_programacion()
        {
            var config = new ConfiguracionExportacion { Columnas = new List<ColumnaExportacion>() };
            var armador = new ArmadorExportacionOpenXml();

            Assert.Throws<ArgumentException>(() => armador.Armar(new List<FilaParaExportar>(), config));
        }
    }
}
