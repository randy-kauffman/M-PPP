using System;
using System.Globalization;
using Sanic.Mppp.Plugins.Catalogos;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Unitarias.Catalogos
{
    /// <summary>DN-04, Excel 1, columna J: celda de fecha de Excel (serial, con fracción descartada) o texto d/m/aaaa.</summary>
    public class FechaDeCargaTests
    {
        [Theory]
        [InlineData("45566", 2024, 10, 1)]
        [InlineData("45566.75", 2024, 10, 1)]      // la hora se descarta
        [InlineData("45566.999999", 2024, 10, 1)]
        [InlineData(" 45566 ", 2024, 10, 1)]
        [InlineData("1/9/2024", 2024, 9, 1)]
        [InlineData("01/09/2024", 2024, 9, 1)]
        [InlineData("29/02/2024", 2024, 2, 29)]
        [InlineData("31/12/9999", 9999, 12, 31)]
        public void Interpreta_serial_de_Excel_y_texto_d_m_aaaa(string valor, int anio, int mes, int dia)
        {
            Assert.True(FechaDeCarga.TryInterpretar(valor, out var fecha));
            Assert.Equal(new DateTime(anio, mes, dia), fecha);
            Assert.Equal(DateTimeKind.Unspecified, fecha.Kind);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("-5")]
        [InlineData("0")]
        [InlineData("0.5")]
        [InlineData("4.5566E4")]        // notación científica: no se reconstruye con certeza
        [InlineData("45566,5")]         // coma decimal: no es lo que escribe el lector
        [InlineData("3000000")]         // fuera del rango de fechas
        [InlineData("1/9/24")]          // año de 2 dígitos
        [InlineData("1/9/20244")]
        [InlineData("001/9/2024")]
        [InlineData("1/009/2024")]
        [InlineData("29/02/2023")]      // 2023 no es bisiesto
        [InlineData("31/04/2024")]
        [InlineData("0/9/2024")]
        [InlineData("1/13/2024")]
        [InlineData("1/9/0000")]
        [InlineData("1-9-2024")]
        [InlineData("2024/09/01")]
        [InlineData("1/9")]
        [InlineData("1/9/2024/1")]
        [InlineData("a/b/cccc")]
        [InlineData("١/٩/٢٠٢٤")]       // dígitos arábigos: no son ASCII
        public void Todo_lo_demas_es_ilegible(string valor)
        {
            Assert.False(FechaDeCarga.TryInterpretar(valor, out var fecha));
            Assert.Equal(default(DateTime), fecha);
        }

        [Fact]
        public void No_depende_de_la_cultura_del_hilo()
        {
            var original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("fr-FR"); // coma decimal y otro calendario de formato
                Assert.True(FechaDeCarga.TryInterpretar("45566.5", out var fecha));
                Assert.Equal(new DateTime(2024, 10, 1), fecha);
                Assert.False(FechaDeCarga.TryInterpretar("45566,5", out _));
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }
    }
}
