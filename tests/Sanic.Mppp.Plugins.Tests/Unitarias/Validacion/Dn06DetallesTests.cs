using System;
using Sanic.Mppp.Plugins.Dominio;
using Sanic.Mppp.Plugins.Validacion;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Unitarias.Validacion
{
    /// <summary>DN-06: detalles que las pruebas de aceptación no fijan (el veredicto como resultado de regla).</summary>
    public class Dn06DetallesTests
    {
        [Fact]
        public void El_veredicto_se_expone_como_cumplida_o_no_cumplida_nunca_omitida()
        {
            Assert.Equal(ResultadoDeLaRegla.Cumplida, Veredicto.Cumplida().Resultado);
            Assert.Equal(ResultadoDeLaRegla.NoCumplida, Veredicto.NoCumplida("motivo").Resultado);
        }

        [Fact]
        public void Un_plan_armado_por_posicion_con_canal_y_preregistro_los_conserva()
        {
            var plan = new PlanDelCatalogo(Guid.NewGuid(), "0011", TipoDeFormatoDelPlan._06, Moneda.COR, canal: Canal.Conectividad, requierePreregistro: false);
            Assert.Null(plan.ClienteId);
            Assert.Equal((Canal.Conectividad, false), (plan.Canal, plan.RequierePreregistro));
        }
    }
}
