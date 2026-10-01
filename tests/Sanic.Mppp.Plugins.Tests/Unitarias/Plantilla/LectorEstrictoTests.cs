using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sanic.Mppp.Plugins.Plantilla;
using Xunit;
using static Sanic.Mppp.Plugins.Tests.Apoyo.XlsxCrudo;

namespace Sanic.Mppp.Plugins.Tests.Unitarias.Plantilla
{
    /// <summary>DN-04: detalles internos del modo estricto que las pruebas de aceptación no fijan (qué cuenta como valor, prioridades, mensajes).</summary>
    public class LectorEstrictoTests
    {
        private const string Hoja = "Plantilla";

        private static ConfiguracionPlantilla Ventana(bool estricta) => new ConfiguracionPlantilla
        {
            Hoja = Hoja, FilaEncabezado = 5, PrimeraFila = 6, CantidadFilas = 3, EstructuraEstricta = estricta,
            Campos = new List<CampoPlantilla>
            {
                new CampoPlantilla { Nombre = "uno", Columna = "C", EncabezadoEsperado = "Campo Uno" },
                new CampoPlantilla { Nombre = "dos", Columna = "D", EncabezadoEsperado = "Campo Dos" },
            },
        };

        private static string Encabezado(string uno = "Campo Uno", params string[] extra)
            => Fila(5, new[] { Celda("C5", uno), Celda("D5", "Campo Dos") }.Concat(extra).ToArray());

        private static string Datos(int fila, params string[] extra)
            => Fila(fila, new[] { Celda($"C{fila}", "c"), Celda($"D{fila}", "d") }.Concat(extra).ToArray());

        private static ResultadoLecturaPlantilla Leer(bool estricta, params string[] filas)
            => new LectorOpenXml().Leer(ConFilas(Hoja, filas), Ventana(estricta));

        [Theory]
        [InlineData("<c r=\"F7\"><v>0</v></c>")]                 // un cero es un valor
        [InlineData("<c r=\"F7\" t=\"b\"><v>0</v></c>")]         // un booleano falso es un valor
        [InlineData("<c r=\"F7\" t=\"e\"><v>#REF!</v></c>")]     // un error de Excel es un valor
        [InlineData("<c r=\"F7\" t=\"b\"><v>x</v></c>")]         // algo que no se puede interpretar: algo trae
        [InlineData("<c r=\"F7\" t=\"s\"><v>999</v></c>")]       // texto compartido inexistente
        public void Estricto_cualquier_celda_de_mas_con_algo_adentro_cuenta_como_valor(string celdaDeMas)
        {
            var r = Leer(true, Encabezado(), Datos(6), Datos(7, celdaDeMas));
            Assert.False(r.EsValido);
            Assert.Contains(r.Rechazo, new[] { TipoDeRechazoLectura.ColumnaDeMas, TipoDeRechazoLectura.CeldaIlegible });
        }

        [Fact]
        public void Estricto_una_celda_de_mas_con_texto_en_blanco_seguida_de_otra_con_valor_rechaza_por_la_segunda()
        {
            var r = Leer(true, Encabezado(), Datos(6, Celda("E6", "  "), Celda("F6", "x")));
            Assert.Equal(TipoDeRechazoLectura.ColumnaDeMas, r.Rechazo);
            Assert.Contains("F6", Assert.Single(r.Errores));
        }

        [Fact]
        public void Estricto_el_mensaje_de_columna_nombra_la_celda_y_la_ultima_columna_admitida()
        {
            var r = Leer(true, Encabezado(extra: Celda("E5", "Extra")), Datos(6));
            var motivo = Assert.Single(r.Errores);
            Assert.Contains("E5", motivo);
            Assert.Contains("hasta la D", motivo);
        }

        [Fact]
        public void Estricto_el_mensaje_de_fila_nombra_la_fila_la_celda_y_el_ultimo_limite()
        {
            var r = Leer(true, Encabezado(), Datos(6), Fila(40, Celda("B40", "x")));
            var motivo = Assert.Single(r.Errores);
            Assert.Contains("fila 40", motivo);
            Assert.Contains("B40", motivo);
            Assert.Contains("hasta la 8", motivo);
        }

        [Fact]
        public void Estricto_una_celda_desordenada_despues_de_la_ventana_es_Desorden()
        {
            var r = Leer(true, Encabezado(), Datos(6), Fila(12, Celda("F12", "x"), Celda("E12", "y")));
            Assert.Equal(TipoDeRechazoLectura.Desorden, r.Rechazo);
        }

        [Fact]
        public void Estricto_una_celda_desordenada_mas_alla_de_la_ultima_columna_es_Desorden()
        {
            var r = Leer(true, Encabezado(), Datos(6, SoloFormato("G6"), SoloFormato("F6")));
            Assert.Equal(TipoDeRechazoLectura.Desorden, r.Rechazo);
        }

        [Fact]
        public void Apagado_una_celda_desordenada_mas_alla_de_la_ultima_columna_no_se_mira()
        {
            var r = Leer(false, Encabezado(), Datos(6, SoloFormato("G6"), SoloFormato("F6")));
            Assert.True(r.EsValido);
        }

        [Fact]
        public void Estricto_el_encabezado_distinto_gana_sobre_lo_de_mas()
        {
            var r = Leer(true, Encabezado(uno: "Otro", extra: Celda("E5", "Extra")), Datos(6, Celda("F6", "x")), Fila(12, Celda("A12", "z")));
            Assert.Equal(TipoDeRechazoLectura.Encabezado, r.Rechazo);
        }

        [Fact]
        public void Estricto_la_columna_de_mas_gana_sobre_la_fila_de_mas()
        {
            var r = Leer(true, Encabezado(), Datos(6, Celda("F6", "x")), Fila(12, Celda("A12", "z")));
            Assert.Equal(TipoDeRechazoLectura.ColumnaDeMas, r.Rechazo);
        }

        [Fact]
        public void Estricto_una_fila_de_mas_sin_atributo_r_se_ubica_por_su_posicion()
        {
            // Filas 5, 6, 7, 8 explícitas; la siguiente sin r vale 9 (LP-06) y trae un valor.
            var r = Leer(true, Encabezado(), Datos(6), Datos(7), Datos(8), "<row><c r=\"A9\" t=\"inlineStr\"><is><t>x</t></is></c></row>");
            Assert.Equal(TipoDeRechazoLectura.FilaDeMas, r.Rechazo);
            Assert.Contains("fila 9", Assert.Single(r.Errores));
        }

        [Fact]
        public void Estricto_una_ventana_completa_y_sin_nada_de_mas_es_valida_y_trae_sus_filas()
        {
            var r = Leer(true, Encabezado(), Datos(6), Datos(7), Datos(8));
            Assert.True(r.EsValido, string.Join(" | ", r.Errores));
            Assert.Equal(TipoDeRechazoLectura.Ninguno, r.Rechazo);
            Assert.Equal(new[] { 6, 7, 8 }, r.Filas.Select(f => f.NumeroFilaExcel).ToArray());
        }

        [Fact]
        public void Estricto_el_espacio_interno_cuenta_pero_la_mayuscula_no()
        {
            // "Campo Uno" esperado: sin tilde ni mayúscula pasa; con otro carácter base no.
            Assert.True(Leer(true, Encabezado(uno: "CAMPO UNO"), Datos(6)).EsValido);
            Assert.Equal(TipoDeRechazoLectura.Encabezado, Leer(true, Encabezado(uno: "CampoUno"), Datos(6)).Rechazo);
        }

        [Fact]
        public void Estricto_la_tilde_del_encabezado_esperado_tambien_se_descarta()
        {
            var config = Ventana(true);
            config.Campos[0].EncabezadoEsperado = "Campo Único";
            var r = new LectorOpenXml().Leer(ConFilas(Hoja, Encabezado(uno: "CAMPO UNICO"), Datos(6)), config);
            Assert.True(r.EsValido, string.Join(" | ", r.Errores));
        }

        [Fact]
        public void Estricto_el_tipo_viaja_en_el_Stream_para_fila_y_columna_de_mas()
        {
            using (var s1 = new MemoryStream(ConFilas(Hoja, Encabezado(), Datos(6), Datos(12))))
                Assert.Equal(TipoDeRechazoLectura.FilaDeMas, new LectorOpenXml().Leer(s1, Ventana(true)).Rechazo);
            using (var s2 = new MemoryStream(ConFilas(Hoja, Encabezado(extra: Celda("E5", "x")), Datos(6))))
                Assert.Equal(TipoDeRechazoLectura.ColumnaDeMas, new LectorOpenXml().Leer(s2, Ventana(true)).Rechazo);
        }

        [Fact]
        public void Un_stream_que_supera_el_tope_de_entrada_es_PesoExcedido()
        {
            var lector = new LectorOpenXml(new LimitesLectura { TamanoMaximoBytesEntrada = 10, TamanoMaximoBytesDescomprimidos = 20L * 1024 * 1024 });
            using (var s = new MemoryStream(ConFilas(Hoja, Encabezado(), Datos(6))))
                Assert.Equal(TipoDeRechazoLectura.PesoExcedido, lector.Leer(s, Ventana(false)).Rechazo);
        }

        [Fact]
        public void Un_archivo_que_no_es_un_zip_es_ArchivoIlegible_y_trae_detalle_tecnico()
        {
            var r = new LectorOpenXml().Leer(System.Text.Encoding.UTF8.GetBytes("no soy un zip"), Ventana(true));
            Assert.Equal(TipoDeRechazoLectura.ArchivoIlegible, r.Rechazo);
            Assert.NotNull(r.DetalleTecnico);
        }

        private static string SoloFormato(string referencia) => $"<c r=\"{referencia}\" s=\"1\"/>";
    }
}
