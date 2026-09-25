using System;
using Sanic.Mppp.Plugins.Dominio;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Aceptacion
{
    /// <summary>
    /// 8.x: qué fila entra en el lote de `obtenerfilaspendientes` (diseno/03 §2, decisiones del 2026-09-25).
    /// Funciones puras: sin Dataverse, sin dobles.
    /// </summary>
    public class SeleccionParaElRpaAceptacion
    {
        private static readonly DateTime Ahora = new DateTime(2026, 10, 5, 15, 0, 0, DateTimeKind.Utc);
        private static readonly Guid ElBot = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid OtroBot = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid UnEjecutivo = Guid.Parse("33333333-3333-3333-3333-333333333333");

        private static bool Entra(FilaCandidata fila, bool puedeAprobar = true, Guid? quienLlama = null)
        {
            return SeleccionParaElRpa.EntraEnElLote(fila, quienLlama ?? ElBot, puedeAprobar, Ahora);
        }

        private static FilaCandidata Validada() => new FilaCandidata { Estado = EstadoDeLaFila.Validada };

        private static FilaCandidata DigitadaPor(Guid quien) =>
            new FilaCandidata { Estado = EstadoDeLaFila.Digitada, DigitadaPor = quien };

        // ---------- Lo que SÍ entra ----------

        [Fact]
        public void Una_Validada_libre_entra()
        {
            Assert.True(Entra(Validada()));
        }

        [Fact]
        public void Una_Digitada_POR_EL_MISMO_BOT_entra_para_que_la_apruebe()
        {
            Assert.True(Entra(DigitadaPor(ElBot)));
        }

        [Fact]
        public void Una_fila_con_el_claim_VENCIDO_vuelve_a_entrar()
        {
            var fila = Validada();
            fila.ClaimHasta = Ahora.AddMinutes(-1);
            Assert.True(Entra(fila));
        }

        [Fact]
        public void Una_fila_que_el_BOT_YA_TIENE_TOMADA_vuelve_a_entrar()
        {
            // El claim es un candado contra las PERSONAS, no contra el bot. Si el bot se cae y reinicia,
            // perdio su lista en memoria: con exclusion recibiria CERO filas y quedaria trabado hasta el
            // vencimiento. Asi, pide, recupera lo suyo y sigue (razon del aprobador, 2026-09-25).
            var fila = Validada();
            fila.ClaimHasta = Ahora.AddMinutes(30);
            Assert.True(Entra(fila));
        }

        // ---------- Lo que NO entra ----------

        [Fact]
        public void Una_Digitada_POR_OTRO_no_entra_aunque_sea_otro_bot()
        {
            // Al bot B no le consta lo que hizo el bot A, igual que no le consta lo de un ejecutivo.
            Assert.False(Entra(DigitadaPor(OtroBot)));
        }

        [Fact]
        public void Una_Digitada_POR_UN_EJECUTIVO_no_entra()
        {
            // El bot aprueba porque LE CONSTA que la digitacion se hizo bien: la hizo el. De lo que hizo
            // una persona no le consta nada, y aprobarlo seria firmar un trabajo que no vio.
            Assert.False(Entra(DigitadaPor(UnEjecutivo)));
        }

        [Fact]
        public void Con_rpa_puedeaprobar_en_NO_no_entra_ninguna_Digitada()
        {
            // Sin el aval de C-03 esas filas son de un supervisor. Entregarselas seria darle trabajo que le
            // vamos a prohibir: intentaria aprobar, fallaria, y repetiria en cada corrida DURANTE MESES,
            // enterrando el primer error de verdad abajo de diez mil falsos.
            Assert.False(Entra(DigitadaPor(ElBot), puedeAprobar: false));
        }

        [Fact]
        public void Con_rpa_puedeaprobar_en_NO_las_Validadas_SIGUEN_entrando()
        {
            // El dia que llegue el aval se cambia UN parametro y las Digitadas aparecen solas.
            Assert.True(Entra(Validada(), puedeAprobar: false));
        }

        [Fact]
        public void Una_fila_marcada_para_ATENCION_MANUAL_no_entra()
        {
            var fila = Validada();
            fila.RequiereAtencionManual = true;
            Assert.False(Entra(fila));
        }

        [Fact]
        public void La_marca_de_atencion_manual_gana_incluso_sobre_una_Digitada_propia()
        {
            var fila = DigitadaPor(ElBot);
            fila.RequiereAtencionManual = true;
            Assert.False(Entra(fila));
        }

        [Fact]
        public void Una_Digitada_SIN_AUTOR_no_entra()
        {
            // No deberia existir —el plugin siempre escribe `digitadapor`— pero una fila corrupta, una
            // migracion o una edicion a mano podrian producirla. Sin autor no se puede afirmar que la digito
            // el bot, y afirmarlo por omision seria dejar que apruebe trabajo ajeno. Falla cerrado.
            Assert.False(Entra(new FilaCandidata { Estado = EstadoDeLaFila.Digitada, DigitadaPor = null }));
        }

        [Theory]
        [InlineData(EstadoDeLaFila.Aprobada)]
        [InlineData(EstadoDeLaFila.Anulada)]
        [InlineData(EstadoDeLaFila.RechazadaEnAS400)]
        [InlineData(EstadoDeLaFila.RechazadaEnValidacion)]
        [InlineData(EstadoDeLaFila.SinAutorizacion)]
        public void Ningun_estado_terminal_entra(EstadoDeLaFila estado)
        {
            Assert.False(Entra(new FilaCandidata { Estado = estado, DigitadaPor = ElBot }));
        }
    }
}
