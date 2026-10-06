using System.IO;
using System.Linq;
using Sanic.Mppp.Plugins.Dominio;
using Sanic.Mppp.Plugins.Plantilla;
using Sanic.Mppp.Plugins.Validacion;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Aceptacion
{
    /// <summary>DN-05: los valores EXACTOS que se siembran en Dev (2026-10-06) se leen con el código nuevo.</summary>
    public class Dn05SembradoAceptacion
    {
        internal const string EstructuraV2 = "{\"hoja\":\"Datos\",\"filaEncabezado\":12,\"primeraFila\":13,\"cantidadFilas\":100,\"campos\":[{\"nombre\":\"gestion\",\"columna\":\"B\",\"encabezado\":\"Gesti\u00f3n\",\"formato\":\"texto\",\"largoMinimo\":1,\"largoMaximo\":20},{\"nombre\":\"clasificacion\",\"columna\":\"C\",\"encabezado\":\"Clasificaci\u00f3n\",\"formato\":\"texto\",\"largoMinimo\":1,\"largoMaximo\":10},{\"nombre\":\"numeroPlan\",\"columna\":\"D\",\"encabezado\":\"No. Plan\",\"formato\":\"alfanumerico\",\"largoMinimo\":1,\"largoMaximo\":4},{\"nombre\":\"nombreBeneficiario\",\"columna\":\"E\",\"encabezado\":\"Nombre Colaborador / Proveedor\",\"formato\":\"texto\",\"largoMinimo\":1,\"largoMaximo\":44,\"caracteres\":{\"letras\":true,\"espacio\":true,\"otros\":\"-.,\"}},{\"nombre\":\"tipoIdentificacion\",\"columna\":\"F\",\"encabezado\":\"Tipo ID\",\"formato\":\"texto\",\"largoMinimo\":1,\"largoMaximo\":10},{\"nombre\":\"numeroIdentificacion\",\"columna\":\"G\",\"encabezado\":\"No. Identificaci\u00f3n\",\"formato\":\"texto\",\"largoMinimo\":1,\"largoMaximo\":16,\"caracteres\":{\"letras\":true,\"digitos\":true}},{\"nombre\":\"referencia\",\"columna\":\"H\",\"encabezado\":\"Referencia\",\"formato\":\"texto\",\"largoMinimo\":1,\"largoMaximo\":20,\"caracteres\":{\"letras\":true,\"digitos\":true}},{\"nombre\":\"numeroCuenta\",\"columna\":\"I\",\"encabezado\":\"No. Cuenta\",\"formato\":\"texto\",\"largoMinimo\":1,\"largoMaximo\":17,\"caracteres\":{\"digitos\":true}},{\"nombre\":\"moneda\",\"columna\":\"J\",\"encabezado\":\"Moneda\",\"formato\":\"texto\",\"largoMinimo\":1,\"largoMaximo\":10},{\"nombre\":\"banco\",\"columna\":\"K\",\"encabezado\":\"Banco\",\"formato\":\"texto\",\"largoMinimo\":1,\"largoMaximo\":20}]}";

        [Fact]
        public void La_estructura_sembrada_trae_los_caracteres_de_la_decision_7_y_la_cuenta_a_17()
        {
            var e = ConfiguracionPlantilla.DesdeJson(EstructuraV2);
            Assert.Equal(17, e.Campos.Single(c => c.Nombre == "numeroCuenta").LargoMaximo);
            Assert.Equal("-.,", e.Campos.Single(c => c.Nombre == "nombreBeneficiario").Caracteres.Otros);
            Assert.True(e.Campos.Single(c => c.Nombre == "numeroCuenta").Caracteres.Digitos);
            Assert.False(e.Campos.Single(c => c.Nombre == "numeroCuenta").Caracteres.Letras);
            Assert.True(e.Campos.Single(c => c.Nombre == "referencia").Caracteres.Letras);
            Assert.Null(e.Campos.Single(c => c.Nombre == "moneda").Caracteres);
        }

        [Fact]
        public void Los_otros_tres_parametros_sembrados_se_leen()
        {
            Assert.NotNull(ObligatoriedadPlantilla.DesdeJson(Dn05Aceptacion.ObligatoriedadV3));
            Assert.Equal(new[] { Clasificacion.ACH }, FormatosDelPlan.DesdeJson(Dn05Aceptacion.FormatosDn05).Para(TipoDeFormatoDelPlan._11).Clasificaciones);
            Assert.Equal(9, LargosDeCuenta.DesdeJson(Dn05Aceptacion.LargosDn05).Para(Banco.BAC).Minimo);
        }
    }
}
