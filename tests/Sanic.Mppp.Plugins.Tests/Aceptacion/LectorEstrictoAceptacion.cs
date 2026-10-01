// PRUEBAS DE ACEPTACIÓN del modo estricto del lector (DN-04, diseno/DIRECTRICES-NUEVAS.md, "Lectura del archivo") y del tipo de
// rechazo que la Custom API de catálogos traduce a su código MPPP-CAR. EL CONSTRUCTOR NO MODIFICA ESTE ARCHIVO.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Sanic.Mppp.Plugins.Plantilla;
using Xunit;
using static Sanic.Mppp.Plugins.Tests.Apoyo.XlsxCrudo;

namespace Sanic.Mppp.Plugins.Tests.Aceptacion
{
    public class LectorEstrictoAceptacion
    {
        private const string Hoja = "Plantilla";

        /// <summary>Encabezado en la fila 5; datos en las filas 6, 7 y 8; campos en C y D (igual que LectorVentanaYOrdenAceptacion).</summary>
        private static ConfiguracionPlantilla Ventana(bool estricta)
        {
            return new ConfiguracionPlantilla
            {
                Hoja = Hoja, FilaEncabezado = 5, PrimeraFila = 6, CantidadFilas = 3, EstructuraEstricta = estricta,
                Campos = new List<CampoPlantilla>
                {
                    new CampoPlantilla { Nombre = "uno", Columna = "C", EncabezadoEsperado = "Campo Uno" },
                    new CampoPlantilla { Nombre = "dos", Columna = "D", EncabezadoEsperado = "Campo Dos" },
                },
            };
        }

        private static string Encabezado(string uno = "Campo Uno", string dos = "Campo Dos", params string[] extra)
            => Fila(5, new[] { Celda("C5", uno), Celda("D5", dos) }.Concat(extra).ToArray());

        private static string Datos(int fila, params string[] extra)
            => Fila(fila, new[] { Celda($"C{fila}", $"c{fila}"), Celda($"D{fila}", $"d{fila}") }.Concat(extra).ToArray());

        /// <summary>Celda con formato y sin valor: Excel las escribe todo el tiempo y NO es un dato.</summary>
        private static string SoloFormato(string referencia) => $"<c r=\"{referencia}\" s=\"1\"/>";

        private static ResultadoLecturaPlantilla Leer(bool estricta, params string[] filas)
            => new LectorOpenXml().Leer(ConFilas(Hoja, filas), Ventana(estricta));

        // ============================================================================== apagado = como siempre (no regresión)
        [Fact]
        public void Apagado_es_el_valor_por_defecto()
        {
            Assert.False(new ConfiguracionPlantilla().EstructuraEstricta);
        }

        [Fact]
        public void El_parametro_JSON_no_puede_encender_el_modo_estricto()
        {
            var config = ConfiguracionPlantilla.DesdeJson(
                @"{""hoja"":""Plantilla"",""filaEncabezado"":5,""primeraFila"":6,""cantidadFilas"":3,""estructuraEstricta"":true,""EstructuraEstricta"":true,"
                + @"""campos"":[{""nombre"":""uno"",""columna"":""C"",""encabezado"":""Campo Uno""}]}");
            Assert.False(config.EstructuraEstricta);
        }

        [Fact]
        public void Apagado_una_fila_despues_de_la_ventana_corta_sin_invalidar()
        {
            var r = Leer(false, Encabezado(), Datos(6), Datos(12));
            Assert.True(r.EsValido);
            Assert.Equal(TipoDeRechazoLectura.Ninguno, r.Rechazo);
            Assert.Equal(new[] { 6 }, r.Filas.Select(f => f.NumeroFilaExcel).ToArray());
        }

        [Fact]
        public void Apagado_un_valor_despues_de_la_ultima_columna_se_ignora()
        {
            var r = Leer(false, Encabezado(extra: Celda("E5", "Extra")), Datos(6, Celda("F6", "x")));
            Assert.True(r.EsValido);
            Assert.Single(r.Filas);
        }

        [Fact]
        public void Apagado_el_encabezado_se_compara_exacto()
        {
            var r = Leer(false, Encabezado(uno: "campo uno"), Datos(6));
            Assert.False(r.EsValido);
            Assert.Equal(TipoDeRechazoLectura.Encabezado, r.Rechazo);
        }

        // ============================================================================== encendido: filas de más
        [Fact]
        public void Estricto_una_fila_con_datos_despues_de_la_ventana_rechaza_el_archivo_nombrando_la_fila_y_el_limite()
        {
            var r = Leer(true, Encabezado(), Datos(6), Datos(12));
            Assert.False(r.EsValido);
            Assert.Equal(TipoDeRechazoLectura.FilaDeMas, r.Rechazo);
            var motivo = Assert.Single(r.Errores);
            Assert.Contains("12", motivo);   // la fila que sobra
            Assert.Contains("8", motivo);    // la última fila admitida
        }

        [Fact]
        public void Estricto_una_fila_despues_de_la_ventana_con_un_valor_en_cualquier_columna_rechaza()
        {
            var r = Leer(true, Encabezado(), Datos(6), Fila(20, Celda("Z20", "nota al pie")));
            Assert.Equal(TipoDeRechazoLectura.FilaDeMas, r.Rechazo);
        }

        [Fact]
        public void Estricto_filas_despues_de_la_ventana_solo_con_formato_o_en_blanco_no_son_datos()
        {
            var r = Leer(true, Encabezado(), Datos(6),
                Fila(9, SoloFormato("C9"), SoloFormato("D9")),
                Fila(10, Celda("C10", ""), Celda("D10", "   ")),
                Fila(500, SoloFormato("A500")));
            Assert.True(r.EsValido, string.Join(" | ", r.Errores));
            Assert.Equal(new[] { 6 }, r.Filas.Select(f => f.NumeroFilaExcel).ToArray());
        }

        [Fact]
        public void Estricto_sigue_exigiendo_el_orden_en_lo_que_lee_despues_de_la_ventana()
        {
            var r = Leer(true, Encabezado(), Datos(6), Fila(12, SoloFormato("C12")), Fila(10, SoloFormato("C10")));
            Assert.Equal(TipoDeRechazoLectura.Desorden, r.Rechazo);
        }

        [Fact]
        public void Estricto_cincuenta_mil_filas_solo_con_formato_despues_de_la_ventana_se_recorren_rapido()
        {
            var filas = new List<string> { Encabezado(), Datos(6) };
            for (var i = 9; i < 50009; i++) filas.Add(Fila(i, SoloFormato($"C{i}")));
            var archivo = ConFilas(Hoja, filas.ToArray());
            var reloj = Stopwatch.StartNew();
            var r = new LectorOpenXml().Leer(archivo, Ventana(true));
            reloj.Stop();
            Assert.True(r.EsValido, string.Join(" | ", r.Errores));
            Assert.True(reloj.Elapsed < TimeSpan.FromSeconds(5), $"tardó {reloj.Elapsed}");
        }

        // ============================================================================== encendido: columnas de más
        [Fact]
        public void Estricto_un_encabezado_de_mas_rechaza_nombrando_la_celda()
        {
            var r = Leer(true, Encabezado(extra: Celda("E5", "Extra")), Datos(6));
            Assert.False(r.EsValido);
            Assert.Equal(TipoDeRechazoLectura.ColumnaDeMas, r.Rechazo);
            Assert.Contains(r.Errores, e => e.Contains("E5"));
        }

        [Fact]
        public void Estricto_un_valor_despues_de_la_ultima_columna_en_una_fila_de_datos_rechaza_nombrando_la_celda()
        {
            var r = Leer(true, Encabezado(), Datos(6), Datos(7, Celda("F7", "x")));
            Assert.Equal(TipoDeRechazoLectura.ColumnaDeMas, r.Rechazo);
            Assert.Contains(r.Errores, e => e.Contains("F7"));
        }

        [Fact]
        public void Estricto_celdas_de_mas_solo_con_formato_o_en_blanco_no_son_datos()
        {
            var r = Leer(true, Encabezado(extra: new[] { SoloFormato("E5"), Celda("F5", " ") }), Datos(6, SoloFormato("G6")));
            Assert.True(r.EsValido, string.Join(" | ", r.Errores));
        }

        // ============================================================================== encendido: encabezados sin formato
        [Theory]
        [InlineData("campo uno", "CAMPO DOS")]
        [InlineData("  Campo Uno  ", "Campo Dos")]
        [InlineData("Cámpo Úno", "Campo Dós")]
        public void Estricto_compara_encabezados_sin_mayusculas_tildes_ni_espacios_a_los_lados(string uno, string dos)
        {
            var r = Leer(true, Encabezado(uno, dos), Datos(6));
            Assert.True(r.EsValido, string.Join(" | ", r.Errores));
        }

        [Theory]
        [InlineData("Campo  Uno")]   // espacio de más EN MEDIO: es otro texto
        [InlineData("Campo Uno 2")]
        [InlineData("")]
        public void Estricto_un_encabezado_distinto_sigue_siendo_error(string uno)
        {
            var r = Leer(true, Encabezado(uno: uno), Datos(6));
            Assert.Equal(TipoDeRechazoLectura.Encabezado, r.Rechazo);
        }

        // ============================================================================== tipo de rechazo, encendido o apagado
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Cada_rechazo_del_lector_trae_su_tipo_y_nunca_SinClasificar(bool estricta)
        {
            var lector = new LectorOpenXml();

            Assert.Equal(TipoDeRechazoLectura.ArchivoIlegible, lector.Leer(Encoding.UTF8.GetBytes("no soy un zip"), Ventana(estricta)).Rechazo);
            Assert.Equal(TipoDeRechazoLectura.HojaInexistente, lector.Leer(ConFilas("OtraHoja", Encabezado(), Datos(6)), Ventana(estricta)).Rechazo);
            Assert.Equal(TipoDeRechazoLectura.Encabezado, lector.Leer(ConFilas(Hoja, Datos(6)), Ventana(estricta)).Rechazo);               // sin fila 5
            Assert.Equal(TipoDeRechazoLectura.Encabezado, lector.Leer(ConFilas(Hoja, Encabezado(dos: "Otro"), Datos(6)), Ventana(estricta)).Rechazo);
            Assert.Equal(TipoDeRechazoLectura.Desorden, lector.Leer(ConFilas(Hoja, Encabezado(), Datos(7), Datos(6)), Ventana(estricta)).Rechazo);
            Assert.Equal(TipoDeRechazoLectura.Desorden, lector.Leer(ConFilas(Hoja, Encabezado(), Fila(6, Celda("D6", "d"), Celda("C6", "c"))), Ventana(estricta)).Rechazo);
            Assert.Equal(TipoDeRechazoLectura.CeldaIlegible, lector.Leer(ConFilas(Hoja, Encabezado(), Fila(6, CeldaDanada("C6"))), Ventana(estricta)).Rechazo);

            var archivo = ConFilas(Hoja, Encabezado(), Datos(6));
            var chico = new LectorOpenXml(new LimitesLectura { TamanoMaximoBytesEntrada = 10, TamanoMaximoBytesDescomprimidos = 20L * 1024 * 1024 });
            Assert.Equal(TipoDeRechazoLectura.PesoExcedido, chico.Leer(archivo, Ventana(estricta)).Rechazo);
            var inflado = new LectorOpenXml(new LimitesLectura { TamanoMaximoBytesEntrada = 2L * 1024 * 1024, TamanoMaximoBytesDescomprimidos = 10 });
            Assert.Equal(TipoDeRechazoLectura.PesoExcedido, inflado.Leer(archivo, Ventana(estricta)).Rechazo);

            var valido = lector.Leer(archivo, Ventana(estricta));
            Assert.True(valido.EsValido);
            Assert.Equal(TipoDeRechazoLectura.Ninguno, valido.Rechazo);
        }

        [Fact]
        public void El_tipo_viaja_tambien_cuando_se_lee_desde_un_stream()
        {
            using (var stream = new System.IO.MemoryStream(ConFilas("OtraHoja", Encabezado(), Datos(6))))
                Assert.Equal(TipoDeRechazoLectura.HojaInexistente, new LectorOpenXml().Leer(stream, Ventana(true)).Rechazo);
        }

        [Fact]
        public void Las_fabricas_sin_tipo_quedan_SinClasificar_y_las_con_tipo_lo_conservan()
        {
            Assert.Equal(TipoDeRechazoLectura.SinClasificar, ResultadoLecturaPlantilla.ConError("x").Rechazo);
            Assert.Equal(TipoDeRechazoLectura.SinClasificar, ResultadoLecturaPlantilla.ConErrores(new List<string> { "x" }).Rechazo);
            Assert.Equal(TipoDeRechazoLectura.ArchivoIlegible, ResultadoLecturaPlantilla.ConErrorTecnico("x", "detalle").Rechazo);
            Assert.Equal(TipoDeRechazoLectura.FilaDeMas, ResultadoLecturaPlantilla.ConError(TipoDeRechazoLectura.FilaDeMas, "x").Rechazo);
            Assert.Equal(TipoDeRechazoLectura.Encabezado, ResultadoLecturaPlantilla.ConErrores(TipoDeRechazoLectura.Encabezado, new List<string> { "x", "y" }).Rechazo);
            Assert.Equal(TipoDeRechazoLectura.Ninguno, ResultadoLecturaPlantilla.ConFilas(new List<FilaPlantilla>(), new List<AdvertenciaLectura>()).Rechazo);
        }

        [Fact]
        public void Una_fabrica_con_tipo_no_acepta_Ninguno_porque_un_rechazo_valido_no_existe()
        {
            Assert.ThrowsAny<ArgumentException>(() => ResultadoLecturaPlantilla.ConError(TipoDeRechazoLectura.Ninguno, "x"));
            Assert.ThrowsAny<ArgumentException>(() => ResultadoLecturaPlantilla.ConErrores(TipoDeRechazoLectura.Ninguno, new List<string> { "x" }));
        }
    }
}
