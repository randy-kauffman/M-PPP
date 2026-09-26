// PRUEBAS DE ACEPTACIÓN nuevas (2026-09-25): punto 4 de diseno/FASE2-ESTADO.md — el claim frena a las
// PERSONAS, nunca al bot. Ver diseno/03-contratos-custom-api.md §2, sección "El claim: reserva con
// vencimiento". Función pura, sin Dataverse: reusa Dominio.ReglasDelRpa.ClaimVigente (ya existe) desde
// TransicionesDeFila.Evaluar. NO se toca TransicionesDeFilaAceptacion.cs (archivo existente).
using System;
using Sanic.Mppp.Plugins.Dominio;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Aceptacion
{
    /// <summary>
    /// diseno/03 §2: "Qué frena el claim. Mientras `sanic_claimhasta` esté en el futuro, una persona no puede
    /// transicionar esa fila: el step de transición la rechaza nombrando la causa y la hora en que se libera."
    /// Y más abajo: "el claim NO frena al bot cuando reporta. El claim protege tomar, no reportar."
    ///
    /// **Corrección del aprobador (2026-09-25)**: el mensaje NO dice una hora — `Ahora`/`ClaimHasta` llegan en
    /// UTC, y convertir a la zona del ejecutivo (Nicaragua, UTC-6) exigiría infraestructura que el plugin no
    /// tiene de forma confiable. Dice CUÁNTO FALTA (una duración), que es correcto sin importar la zona horaria
    /// de quien lo lee.
    /// </summary>
    public class ClaimVigenciaEnTransicionAceptacion
    {
        private static readonly Guid Ana = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        private static readonly Guid Bot = Guid.Parse("cccccccc-0000-0000-0000-000000000003");

        private static ResultadoDeTransicion EvaluarDigitar(RolDeActor rol, bool esCodigoDeServidor, DateTime? claimHasta, DateTime ahora, Guid? quien = null)
        {
            return TransicionesDeFila.Evaluar(new PedidoDeTransicion
            {
                Desde = EstadoDeLaFila.Validada,
                Hacia = EstadoDeLaFila.Digitada,
                Actor = new Actor(quien ?? Ana, rol),
                ClaimHasta = claimHasta,
                Ahora = ahora,
                EsCodigoDeServidor = esCodigoDeServidor,
            });
        }

        [Fact]
        public void Persona_con_claim_vigente_no_transiciona_y_el_mensaje_dice_cuanto_falta_para_liberarse()
        {
            var ahora = new DateTime(2026, 9, 25, 20, 0, 0, DateTimeKind.Utc);
            var claimHasta = ahora.AddMinutes(15); // exacto: falta 15,0 minutos

            var r = EvaluarDigitar(RolDeActor.Ejecutivo, esCodigoDeServidor: false, claimHasta: claimHasta, ahora: ahora);

            Assert.False(r.Permitida);
            Assert.False(string.IsNullOrWhiteSpace(r.Motivo));
            Assert.Equal(EfectoDeTransicion.Ninguno, r.Efecto);
            Assert.Null(r.Evento);
            // Nombra la causa...
            Assert.Contains("reserv", r.Motivo, StringComparison.OrdinalIgnoreCase);
            // ...y CUÁNTO FALTA, nunca una hora.
            Assert.Contains("en 15 minutos", r.Motivo);
        }

        /// <summary>Redondea siempre HACIA ARRIBA: 90 segundos son 1,5 minutos, y nunca se dice menos de lo que falta.</summary>
        [Fact]
        public void El_minuto_restante_se_redondea_hacia_arriba()
        {
            var ahora = new DateTime(2026, 9, 25, 20, 0, 0, DateTimeKind.Utc);
            var claimHasta = ahora.AddSeconds(90); // 1,5 minutos -> redondea a 2

            var r = EvaluarDigitar(RolDeActor.Ejecutivo, esCodigoDeServidor: false, claimHasta: claimHasta, ahora: ahora);

            Assert.Contains("en 2 minutos", r.Motivo);
        }

        /// <summary>Un minuto exacto es SINGULAR: "1 minuto", no "1 minutos".</summary>
        [Fact]
        public void Un_minuto_exacto_usa_singular()
        {
            var ahora = new DateTime(2026, 9, 25, 20, 0, 0, DateTimeKind.Utc);
            var claimHasta = ahora.AddSeconds(60);

            var r = EvaluarDigitar(RolDeActor.Ejecutivo, esCodigoDeServidor: false, claimHasta: claimHasta, ahora: ahora);

            Assert.Contains("en 1 minuto", r.Motivo);
            Assert.DoesNotContain("1 minutos", r.Motivo);
        }

        /// <summary>Nunca "0 minutos": por debajo del minuto se dice "menos de un minuto" (aprobador, 2026-09-25).</summary>
        [Fact]
        public void Menos_de_un_minuto_nunca_dice_cero_minutos()
        {
            var ahora = new DateTime(2026, 9, 25, 20, 0, 0, DateTimeKind.Utc);
            var claimHasta = ahora.AddSeconds(30);

            var r = EvaluarDigitar(RolDeActor.Ejecutivo, esCodigoDeServidor: false, claimHasta: claimHasta, ahora: ahora);

            Assert.Contains("menos de un minuto", r.Motivo);
            Assert.DoesNotContain("0 minutos", r.Motivo);
        }

        [Fact]
        public void Persona_con_claim_ya_vencido_transiciona_normalmente()
        {
            var ahora = new DateTime(2026, 9, 25, 20, 0, 0, DateTimeKind.Utc);
            var claimHasta = ahora.AddMinutes(-1); // venció hace un minuto

            var r = EvaluarDigitar(RolDeActor.Ejecutivo, esCodigoDeServidor: false, claimHasta: claimHasta, ahora: ahora);

            Assert.True(r.Permitida, r.Motivo);
            Assert.Equal(EfectoDeTransicion.RegistrarDigitacion, r.Efecto);
        }

        [Fact]
        public void Persona_sin_ningun_claim_transiciona_normalmente()
        {
            var ahora = new DateTime(2026, 9, 25, 20, 0, 0, DateTimeKind.Utc);

            var r = EvaluarDigitar(RolDeActor.Ejecutivo, esCodigoDeServidor: false, claimHasta: null, ahora: ahora);

            Assert.True(r.Permitida, r.Motivo);
        }

        [Theory]
        [InlineData(15)]   // claim todavía vigente
        [InlineData(-15)]  // claim ya vencido
        public void El_bot_codigo_de_servidor_transiciona_siempre_tenga_o_no_vigente_el_claim(int minutosDesdeAhora)
        {
            var ahora = new DateTime(2026, 9, 25, 20, 0, 0, DateTimeKind.Utc);
            var claimHasta = ahora.AddMinutes(minutosDesdeAhora);

            var r = EvaluarDigitar(RolDeActor.Rpa, esCodigoDeServidor: true, claimHasta: claimHasta, ahora: ahora, quien: Bot);

            Assert.True(r.Permitida, r.Motivo);
            Assert.Equal(EfectoDeTransicion.RegistrarDigitacion, r.Efecto);
        }

        /// <summary>
        /// El claim frena "esa fila", no una transición puntual: no hace falta que sea Digitar para que
        /// bloquee. Se prueba con Anular, que exige además su propio mensaje al cliente.
        /// </summary>
        [Fact]
        public void El_claim_vigente_frena_a_una_persona_en_cualquier_transicion_no_solo_en_digitar()
        {
            var ahora = new DateTime(2026, 9, 25, 20, 0, 0, DateTimeKind.Utc);
            var claimHasta = ahora.AddMinutes(5);

            var r = TransicionesDeFila.Evaluar(new PedidoDeTransicion
            {
                Desde = EstadoDeLaFila.Validada,
                Hacia = EstadoDeLaFila.Anulada,
                Actor = new Actor(Ana, RolDeActor.Ejecutivo),
                Mensaje = "El cliente mandó mal la cuenta",
                ClaimHasta = claimHasta,
                Ahora = ahora,
                EsCodigoDeServidor = false,
            });

            Assert.False(r.Permitida);
            Assert.Contains("en 5 minutos", r.Motivo);
        }

        /// <summary>
        /// D-25 (mala configuración) siempre gana sobre el claim (estado temporal): confirmado por el aprobador,
        /// 2026-09-25. Un usuario con los dos roles no transiciona NUNCA, tenga o no vigente el claim, y el
        /// mensaje es el de D-25, no el del claim.
        /// </summary>
        [Fact]
        public void D25_ambos_roles_gana_sobre_el_claim_vigente()
        {
            var ahora = new DateTime(2026, 9, 25, 20, 0, 0, DateTimeKind.Utc);
            var claimHasta = ahora.AddMinutes(5);
            var ambos = RolDeActor.Ejecutivo | RolDeActor.Supervisor;

            var r = EvaluarDigitar(ambos, esCodigoDeServidor: false, claimHasta: claimHasta, ahora: ahora);

            Assert.False(r.Permitida);
            Assert.Contains("rol", r.Motivo, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("reserv", r.Motivo, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>El instante exacto del vencimiento ya está libre (mismo borde que <see cref="ReglasDelRpa.ClaimVigente"/>).</summary>
        [Fact]
        public void El_instante_exacto_del_vencimiento_ya_deja_pasar_a_la_persona()
        {
            var ahora = new DateTime(2026, 9, 25, 20, 0, 0, DateTimeKind.Utc);

            var r = EvaluarDigitar(RolDeActor.Ejecutivo, esCodigoDeServidor: false, claimHasta: ahora, ahora: ahora);

            Assert.True(r.Permitida, r.Motivo);
        }
    }
}
