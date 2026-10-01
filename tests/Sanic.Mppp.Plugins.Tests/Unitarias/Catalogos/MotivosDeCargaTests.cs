using System;
using Sanic.Mppp.Plugins.Catalogos;
using Sanic.Mppp.Plugins.Steps;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Unitarias.Catalogos
{
    public class MotivosDeCargaTests
    {
        [Fact]
        public void El_motivo_de_un_ArgumentException_no_arrastra_el_nombre_del_parametro()
        {
            // En .NET moderno el Message agrega " (Parameter 'valor')"; en .NET Framework, "Parameter name: valor". Ninguno le sirve a quien carga el archivo.
            var motivo = MotivosDeCarga.DeArgumento(new ArgumentException("El CIF BAC solo admite dígitos.", "valor"));
            Assert.Equal("El CIF BAC solo admite dígitos.", motivo);
        }

        [Fact]
        public void El_motivo_con_el_formato_literal_de_NET_Framework_sale_limpio()
        {
            // Es el formato que produce el sandbox de Dataverse (net462): el Message es "<motivo>" + CRLF + "Parameter name: <nombre>".
            // En .NET moderno el Message lo arma distinto, así que la prueba lo reproduce a mano con una excepción que lo devuelve tal cual.
            var motivo = MotivosDeCarga.DeArgumento(new ExcepcionConMensaje("El CIF BAC solo admite dígitos.\r\nParameter name: valor", "valor"));
            Assert.Equal("El CIF BAC solo admite dígitos.", motivo);
        }

        [Fact]
        public void El_motivo_con_el_formato_de_NET_Framework_sin_retorno_de_carro_sale_limpio()
        {
            var motivo = MotivosDeCarga.DeArgumento(new ExcepcionConMensaje("Solo dígitos.\nParameter name: valor", "valor"));
            Assert.Equal("Solo dígitos.", motivo);
        }

        private sealed class ExcepcionConMensaje : ArgumentException
        {
            private readonly string _mensaje;

            public ExcepcionConMensaje(string mensaje, string parametro) : base(mensaje, parametro) { _mensaje = mensaje; }

            public override string Message => _mensaje;
        }

        [Fact]
        public void El_motivo_de_un_ArgumentException_sin_parametro_queda_igual()
        {
            Assert.Equal("Texto del motivo.", MotivosDeCarga.DeArgumento(new ArgumentException("Texto del motivo.")));
        }

        [Fact]
        public void El_motivo_real_de_cada_normalizacion_sale_limpio()
        {
            var ex = Assert.Throws<ArgumentException>(() => Normalizacion.CifBac("12A"));
            var motivo = MotivosDeCarga.DeArgumento(ex);
            Assert.DoesNotContain("Parameter", motivo);
            Assert.DoesNotContain("valor", motivo);
            Assert.EndsWith(".", motivo);
        }

        [Theory]
        [InlineData(new[] { 9 }, "la fila 9")]
        [InlineData(new[] { 9, 3 }, "las filas 3 y 9")]
        [InlineData(new[] { 9, 3, 5 }, "las filas 3, 5 y 9")]
        [InlineData(new[] { 8, 7, 6, 5, 4, 3, 2 }, "las filas 2, 3, 4, 5, 6 y otras 2")]
        public void Las_filas_se_nombran_ordenadas_y_con_tope(int[] numeros, string esperado)
        {
            Assert.Equal(esperado, MotivosDeCarga.Filas(numeros));
        }

        [Fact]
        public void Un_valor_largo_se_acorta_para_el_mensaje()
        {
            Assert.Equal("abc", MotivosDeCarga.Mostrar("abc"));
            var largo = MotivosDeCarga.Mostrar(new string('x', 500));
            Assert.Equal(60, largo.Length);
            Assert.EndsWith("…", largo);
        }
    }
}
