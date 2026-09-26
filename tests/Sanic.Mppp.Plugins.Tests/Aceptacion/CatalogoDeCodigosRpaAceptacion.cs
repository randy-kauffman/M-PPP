using System;
using Sanic.Mppp.Plugins.Dominio;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Aceptacion
{
    /// <summary>Parámetro `rpa.codigos` (diseno/03 §3, "Los dos textos"). Función pura: sin Dataverse.</summary>
    public class CatalogoDeCodigosRpaAceptacion
    {
        private const string Json = "{\"OK\":{\"cliente\":null,\"nota\":\"AS400 acepto la operacion\"}," +
                                     "\"RECH-GEN\":{\"cliente\":\"Su operacion no pudo completarse.\",\"nota\":\"Rechazo generico\"}}";

        [Fact]
        public void Resuelve_un_codigo_de_exito_con_cliente_nulo()
        {
            var catalogo = CatalogoDeCodigosRpa.DesdeJson(Json);
            var ok = catalogo.Resolver("OK");
            Assert.Null(ok.Cliente);
            Assert.Equal("AS400 acepto la operacion", ok.Nota);
        }

        [Fact]
        public void Resuelve_un_codigo_de_rechazo_con_texto_para_el_cliente()
        {
            var catalogo = CatalogoDeCodigosRpa.DesdeJson(Json);
            var rechazo = catalogo.Resolver("RECH-GEN");
            Assert.Equal("Su operacion no pudo completarse.", rechazo.Cliente);
        }

        [Fact]
        public void Un_codigo_que_no_esta_en_el_catalogo_devuelve_nulo()
        {
            var catalogo = CatalogoDeCodigosRpa.DesdeJson(Json);
            Assert.Null(catalogo.Resolver("NO-EXISTE"));
        }

        [Fact]
        public void La_comparacion_de_codigos_es_EXACTA_no_normalizada()
        {
            // A diferencia de plantilla.listas: estos codigos los manda el bot tal cual, no un cliente escribiendo un Excel.
            var catalogo = CatalogoDeCodigosRpa.DesdeJson(Json);
            Assert.Null(catalogo.Resolver("ok"));
            Assert.Null(catalogo.Resolver(" OK"));
        }

        [Fact]
        public void Un_codigo_nulo_devuelve_nulo_sin_lanzar()
        {
            var catalogo = CatalogoDeCodigosRpa.DesdeJson(Json);
            Assert.Null(catalogo.Resolver(null));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("no es json")]
        [InlineData("{\"OK\":{\"cliente\":null}}")] // falta 'nota'
        [InlineData("{\"OK\":{\"nota\":\"x\"}}")] // falta 'cliente'
        [InlineData("[]")] // no es un objeto
        public void Un_parametro_mal_cargado_es_FormatException(string json)
        {
            Assert.Throws<FormatException>(() => CatalogoDeCodigosRpa.DesdeJson(json));
        }

        [Fact]
        public void Un_catalogo_vacio_pero_bien_formado_no_resuelve_nada()
        {
            var catalogo = CatalogoDeCodigosRpa.DesdeJson("{}");
            Assert.Null(catalogo.Resolver("OK"));
        }
    }
}
