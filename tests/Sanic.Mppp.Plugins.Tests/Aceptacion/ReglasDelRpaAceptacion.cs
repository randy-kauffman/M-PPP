using System;
using Sanic.Mppp.Plugins.Dominio;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Aceptacion
{
    /// <summary>
    /// 7.x: las reglas de borde de las dos Custom API del RPA (diseno/03 §2 y §3, decisiones del 2026-09-25).
    /// Funciones puras: no hay Dataverse, no hay dobles, no hay contexto. Si algo de esto se rompe, se ve acá y no
    /// en un entorno a las tres de la mañana.
    /// </summary>
    public class ReglasDelRpaAceptacion
    {
        private static readonly DateTime Ahora = new DateTime(2026, 10, 5, 15, 0, 0, DateTimeKind.Utc);

        // ---------- MinutosDeClaim: el bot pide, el tope manda ----------

        [Theory]
        [InlineData(10, 60, 10)]   // pide menos que el tope: se le da lo que pidió
        [InlineData(60, 60, 60)]   // justo el tope
        [InlineData(999, 60, 60)]  // pide de más: se RECORTA, no es un error del bot
        public void Los_minutos_del_claim_se_recortan_al_tope(int pedidos, int tope, int esperado)
        {
            Assert.Equal(esperado, ReglasDelRpa.MinutosDeClaim(pedidos, tope));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Una_reserva_que_nace_vencida_es_un_error(int pedidos)
        {
            // Cero o negativo no es "una preferencia que no se puede conceder": no reserva nada.
            Assert.Throws<ArgumentException>(() => ReglasDelRpa.MinutosDeClaim(pedidos, 60));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void Un_TOPE_mal_cargado_cae_al_fallback_en_vez_de_dar_una_reserva_vencida(int tope)
        {
            // `rpa.minutos.claimmaximo` lo carga una persona, y con tope 0 el recorte devolveria 0: una reserva
            // que nace vencida, o sea NINGUNA reserva, y el bot colisionando con los ejecutivos sin que falle nada.
            // Mismo criterio que la tolerancia de ExigirFechaCreible: un dato de configuracion mal cargado DEGRADA
            // el control, nunca lo apaga en silencio.
            Assert.Equal(10, ReglasDelRpa.MinutosDeClaim(10, tope));
            Assert.Equal(ReglasDelRpa.ClaimMaximoMinutosPorDefecto,
                         ReglasDelRpa.MinutosDeClaim(99999, tope));
        }

        // ---------- ClaimVigente ----------

        [Fact]
        public void Sin_reserva_la_fila_esta_libre()
        {
            Assert.False(ReglasDelRpa.ClaimVigente(null, Ahora));
        }

        [Fact]
        public void Una_reserva_que_vence_despues_esta_vigente()
        {
            Assert.True(ReglasDelRpa.ClaimVigente(Ahora.AddMinutes(1), Ahora));
        }

        [Fact]
        public void Una_reserva_vencida_ya_no_frena_a_nadie()
        {
            Assert.False(ReglasDelRpa.ClaimVigente(Ahora.AddMinutes(-1), Ahora));
        }

        [Fact]
        public void El_instante_exacto_del_vencimiento_ya_esta_libre()
        {
            // El borde se define a propósito: "hasta" no incluye el instante. Si no se fija acá, cada quien
            // lo resuelve distinto y aparece una fila trabada un segundo de mas.
            Assert.False(ReglasDelRpa.ClaimVigente(Ahora, Ahora));
        }

        // ---------- ExigirFechaCreible ----------

        [Fact]
        public void Una_fecha_de_hace_un_rato_es_creible()
        {
            ReglasDelRpa.ExigirFechaCreible(Ahora.AddHours(-2), Ahora, 24); // no lanza
        }

        [Fact]
        public void Una_fecha_en_el_FUTURO_se_rechaza()
        {
            // El reloj del bot adelantado envenena el rastro de los cuatro ojos, en silencio.
            var e = Assert.Throws<ArgumentException>(() => ReglasDelRpa.ExigirFechaCreible(Ahora.AddMinutes(1), Ahora, 24));
            Assert.Contains(ReglasDelRpa.CodigoFecha, e.Message);
        }

        [Fact]
        public void Una_fecha_mas_vieja_que_la_tolerancia_se_rechaza()
        {
            var e = Assert.Throws<ArgumentException>(() => ReglasDelRpa.ExigirFechaCreible(Ahora.AddHours(-25), Ahora, 24));
            Assert.Contains(ReglasDelRpa.CodigoFecha, e.Message);
        }

        [Fact]
        public void El_borde_de_la_tolerancia_todavia_sirve()
        {
            ReglasDelRpa.ExigirFechaCreible(Ahora.AddHours(-24), Ahora, 24); // no lanza
        }

        [Fact]
        public void Una_tolerancia_invalida_cae_al_fallback_en_vez_de_tumbar_al_bot()
        {
            // Mismo criterio que `servicio.cuenta`: un dato de configuracion mal cargado DEGRADA el control,
            // nunca voltea la operacion. Con tolerancia 0 se usa el fallback quemado (24 h).
            ReglasDelRpa.ExigirFechaCreible(Ahora.AddHours(-23), Ahora, 0);   // no lanza
            Assert.Throws<ArgumentException>(() => ReglasDelRpa.ExigirFechaCreible(Ahora.AddHours(-25), Ahora, 0));
        }

        // ---------- ExigirLoteAceptable ----------

        [Fact]
        public void Un_lote_normal_pasa()
        {
            ReglasDelRpa.ExigirLoteAceptable(50, 50); // no lanza
        }

        [Fact]
        public void Un_lote_VACIO_se_rechaza()
        {
            var e = Assert.Throws<ArgumentException>(() => ReglasDelRpa.ExigirLoteAceptable(0, 50));
            Assert.Contains(ReglasDelRpa.CodigoLoteVacio, e.Message);
        }

        [Fact]
        public void Un_lote_MAS_GRANDE_QUE_EL_TOPE_se_rechaza_ENTERO_y_NO_se_recorta()
        {
            // La asimetria con MinutosDeClaim es a proposito: un lote son hechos que YA ocurrieron en AS400, y
            // procesar solo los primeros X perderia el resto en silencio. Dataverse quedaria mintiendo sobre el core.
            var e = Assert.Throws<ArgumentException>(() => ReglasDelRpa.ExigirLoteAceptable(51, 50));
            Assert.Contains(ReglasDelRpa.CodigoLoteGrande, e.Message);
        }

        [Fact]
        public void Los_dos_codigos_de_lote_son_DISTINTOS()
        {
            // El bot ramifica por el codigo: "partilo" y "mandame algo" no son la misma instruccion.
            Assert.NotEqual(ReglasDelRpa.CodigoLoteVacio, ReglasDelRpa.CodigoLoteGrande);
        }

        // ---------- CantidadAEntregar: el bot pide, el tope (500) manda ----------

        [Theory]
        [InlineData(10, 10)]
        [InlineData(100, 100)]
        [InlineData(500, 500)]
        [InlineData(999, 500)] // pide de mas: se RECORTA, no es un error del bot
        public void La_cantidad_a_entregar_se_recorta_al_tope_fijo_de_500(int pedido, int esperado)
        {
            Assert.Equal(esperado, ReglasDelRpa.CantidadAEntregar(pedido));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Pedir_cero_o_menos_filas_es_un_error(int pedido)
        {
            Assert.Throws<ArgumentException>(() => ReglasDelRpa.CantidadAEntregar(pedido));
        }

        // ---------- ParsearFechaConOffset ----------

        [Fact]
        public void Una_fecha_con_Z_se_interpreta_como_UTC()
        {
            var fecha = ReglasDelRpa.ParsearFechaConOffset("2026-10-05T15:04:12Z");
            Assert.Equal(new DateTime(2026, 10, 5, 15, 4, 12, DateTimeKind.Utc), fecha);
            Assert.Equal(DateTimeKind.Utc, fecha.Kind);
        }

        [Fact]
        public void Una_fecha_con_offset_negativo_se_convierte_a_UTC()
        {
            var fecha = ReglasDelRpa.ParsearFechaConOffset("2026-10-05T09:04:12-06:00");
            Assert.Equal(new DateTime(2026, 10, 5, 15, 4, 12, DateTimeKind.Utc), fecha);
        }

        [Theory]
        [InlineData("2026-10-05T15:04:12")] // sin offset: se rechaza, no se asume la hora local del servidor
        [InlineData("")]
        [InlineData(null)]
        [InlineData("no es una fecha")]
        public void Una_fecha_sin_offset_explicito_o_ilegible_se_rechaza(string texto)
        {
            var e = Assert.Throws<ArgumentException>(() => ReglasDelRpa.ParsearFechaConOffset(texto));
            Assert.Contains(ReglasDelRpa.CodigoFecha, e.Message);
        }
    }
}
