using System;
using System.Collections.Generic;
using Sanic.Mppp.Plugins.Dominio;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Aceptacion
{
    /// <summary>
    /// 8.x: el JSON que recibe el bot (diseno/03 §2, revisado con el aprobador el 2026-09-25).
    ///
    /// Este es **el contrato con otro equipo**. Del otro lado hay un proceso de Automation Anywhere que alguien más
    /// mantiene: un cambio silencioso acá le rompe el día. Por eso se prueba el TEXTO, no el objeto.
    /// </summary>
    public class ArmadorJsonDelRpaAceptacion
    {
        private static readonly DateTime Validada =
            new DateTime(2026, 10, 5, 14, 52, 7, DateTimeKind.Utc);

        private static FilaParaElRpa Fila() => new FilaParaElRpa
        {
            FilaId = Guid.Parse("5f0c9a21-7b3e-4d18-9c42-0a1b2c3d4e5f"),
            Solicitud = "MPPP-00002001",
            NumeroFila = 3,
            Estado = EstadoDeLaFila.Validada,
            Plan = "0742",
            TipoFormato = "Formato 11",
            Gestion = "Inclusion",
            Clasificacion = "ACH",
            NombreBeneficiario = "NOMBRE DE PRUEBA UNO",
            TipoIdentificacion = "CNA",
            NumeroIdentificacion = "0010203040005X",
            NumeroCuenta = "9990001112223",
            Moneda = "COR",
            Banco = "BANPRO",
            BancoCodigo = "008",
            Referencia = "0080742000000000003",
            ReferenciaRecibida = "",
            FechaValidada = Validada
        };

        private static string Uno(FilaParaElRpa fila) => ArmadorJsonDelRpa.Serializar(new[] { fila });

        [Fact]
        public void Sin_filas_devuelve_un_arreglo_VACIO_y_no_nulo()
        {
            // El bot parsea siempre; que no haya trabajo no puede obligarlo a un caso especial.
            Assert.Equal("[]", ArmadorJsonDelRpa.Serializar(new List<FilaParaElRpa>()));
        }

        [Fact]
        public void Estan_los_18_campos_con_su_nombre_exacto()
        {
            var json = Uno(Fila());
            foreach (var campo in new[]
            {
                "filaid", "solicitud", "numerofila", "estado", "plan", "tipoformato", "gestion",
                "clasificacion", "nombrebeneficiario", "tipoidentificacion", "numeroidentificacion",
                "numerocuenta", "moneda", "banco", "bancocodigo", "referencia", "referenciarecibida",
                "fechavalidada"
            })
            {
                Assert.Contains("\"" + campo + "\"", json);
            }
        }

        [Fact]
        public void Los_choices_viajan_como_TEXTO_y_nunca_como_numero()
        {
            // Un 159460001 no le dice nada a un bot de Automation Anywhere.
            var json = Uno(Fila());
            Assert.Contains("\"Inclusion\"", json);
            Assert.Contains("\"ACH\"", json);
            Assert.Contains("\"validada\"", json);
            Assert.DoesNotContain("159460", json);
        }

        [Fact]
        public void El_banco_viaja_PARTIDO_en_nombre_y_codigo()
        {
            var json = Uno(Fila());
            Assert.Contains("\"banco\": \"BANPRO\"".Replace(" ", ""), json.Replace(" ", ""));
            Assert.Contains("\"bancocodigo\": \"008\"".Replace(" ", ""), json.Replace(" ", ""));
        }

        [Fact]
        public void La_fecha_lleva_OFFSET_explicito()
        {
            Assert.Contains("2026-10-05T14:52:07Z", Uno(Fila()));
        }

        [Fact]
        public void Una_referencia_recibida_VACIA_viaja_vacia_y_no_como_la_que_rige()
        {
            // Formato 11: `referencia` es la CONSTRUIDA y `referenciarecibida` vino vacia. Completar una con la
            // otra le haria creer al bot que el cliente mando algo que nunca mando.
            var json = Uno(Fila()).Replace(" ", "");
            Assert.Contains("\"referenciarecibida\":\"\"", json);
            Assert.Contains("\"referencia\":\"0080742000000000003\"", json);
        }

        [Fact]
        public void Una_Digitada_dice_digitada()
        {
            Assert.Equal("digitada", ArmadorJsonDelRpa.EstadoParaElBot(EstadoDeLaFila.Digitada));
            Assert.Equal("validada", ArmadorJsonDelRpa.EstadoParaElBot(EstadoDeLaFila.Validada));
        }

        [Theory]
        [InlineData(EstadoDeLaFila.Aprobada)]
        [InlineData(EstadoDeLaFila.Anulada)]
        public void Un_estado_que_el_bot_NUNCA_deberia_ver_revienta_en_vez_de_inventarse_un_texto(EstadoDeLaFila estado)
        {
            // `SeleccionParaElRpa` ya garantiza que no llegan. Si llegara, es un defecto nuestro y tiene que
            // gritar acá, no viajarle al bot como una cadena rara que el va a ignorar en silencio.
            Assert.Throws<ArgumentOutOfRangeException>(() => ArmadorJsonDelRpa.EstadoParaElBot(estado));
        }

        [Fact]
        public void El_texto_del_cliente_se_ESCAPA_y_no_rompe_el_JSON()
        {
            // El nombre del beneficiario viene de un Excel que manda un cliente: es texto hostil hasta que se
            // demuestre lo contrario (03 §7). Una comilla suya no puede partir el contrato con el bot.
            var fila = Fila();
            fila.NombreBeneficiario = "JUAN \"EL\" PEREZ\\";
            var json = Uno(fila);
            Assert.Contains("\\\"EL\\\"", json);
            Assert.DoesNotContain("PEREZ\\\"", json);
        }

        [Fact]
        public void Dos_filas_son_dos_elementos()
        {
            var json = ArmadorJsonDelRpa.Serializar(new[] { Fila(), Fila() });
            Assert.StartsWith("[", json.TrimStart());
            Assert.EndsWith("]", json.TrimEnd());
            Assert.Equal(2, json.Split(new[] { "\"filaid\"" }, StringSplitOptions.None).Length - 1);
        }
    }
}
