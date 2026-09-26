using System;
using System.Globalization;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Mppp.Plugins.Api;
using Sanic.Mppp.Plugins.Datos;
using Sanic.Mppp.Plugins.Dominio;
using Sanic.Mppp.Plugins.Tests.Apoyo;
using Sanic.Mppp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Aceptacion
{
    /// <summary>El envoltorio `IPlugin` de `sanic_mppp_capi_registrarresultadorpa` (diseno/03 §3). Traduce parámetros; no decide nada del negocio.</summary>
    public class RegistrarResultadoRpaApiAceptacion
    {
        private static readonly DateTime Ahora = new DateTime(2026, 10, 5, 15, 0, 0, DateTimeKind.Utc);

        private const string Codigos = "{\"OK\":{\"cliente\":null,\"nota\":\"AS400 acepto la operacion\"}}";

        private static (ContextoDePluginSimulado ctx, OrganizationServiceEnMemoria svc, Guid bot) ArmarBot(bool conRol = true, int loteMaximo = 50)
        {
            var svc = new OrganizationServiceEnMemoria();
            var bot = svc.Sembrar(new Entity(TablasNativas.Usuario) { ["applicationid"] = Guid.NewGuid() });
            var rolId = svc.Sembrar(new Entity(TablasNativas.Rol) { ["name"] = TablasNativas.RolRpa });
            if (conRol)
            {
                svc.Sembrar(new Entity(TablasNativas.UsuarioRol) { ["systemuserid"] = bot, ["roleid"] = rolId });
            }

            svc.Sembrar(new Entity(Tablas.Parametro) { ["statecode"] = new OptionSetValue(Tablas.Activo), ["sanic_nombre"] = "rpa.lote.maximo", ["sanic_version"] = 1, ["sanic_valor"] = loteMaximo.ToString(CultureInfo.InvariantCulture) });
            svc.Sembrar(new Entity(Tablas.Parametro) { ["statecode"] = new OptionSetValue(Tablas.Activo), ["sanic_nombre"] = "rpa.codigos", ["sanic_version"] = 1, ["sanic_valor"] = Codigos });

            var ctx = new ContextoDePluginSimulado(svc, Ahora);
            ctx.Contexto.InitiatingUserId = bot;
            ctx.Contexto.MessageName = "sanic_mppp_capi_registrarresultadorpa";
            return (ctx, svc, bot);
        }

        private static Guid SembrarFila(OrganizationServiceEnMemoria svc, EstadoDeLaFila estado) =>
            svc.Sembrar(new Entity(TablasHistorico.Fila) { ["sanic_estado"] = new OptionSetValue((int)estado), ["sanic_requiereatencionmanual"] = false });

        private static string Item(Guid filaId, string resultado, string codigo = "OK") =>
            "{\"filaid\":\"" + filaId + "\",\"resultado\":\"" + resultado + "\",\"codigo\":\"" + codigo + "\"," +
            "\"fechaproceso\":\"" + Ahora.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) + "\"}";

        [Fact]
        public void Un_llamador_no_autorizado_no_pasa_de_la_primera_linea()
        {
            var (ctx, svc, _) = ArmarBot(conRol: false);
            ctx.Contexto.InputParameters[RegistrarResultadoRpaApi.ParametroResultados] = "[]";

            Assert.Throws<InvalidPluginExecutionException>(() => new RegistrarResultadoRpaApi().Execute(ctx));

            Assert.DoesNotContain(svc.Llamadas, l => l.Entidad == TablasHistorico.Fila);
        }

        [Fact]
        public void Un_lote_valido_devuelve_las_tres_salidas_con_sus_nombres_exactos_y_usa_SYSTEM()
        {
            var (ctx, svc, bot) = ArmarBot();
            var filaId = SembrarFila(svc, EstadoDeLaFila.Validada);
            ctx.Contexto.InputParameters[RegistrarResultadoRpaApi.ParametroResultados] = "[" + Item(filaId, "digitada") + "]";

            new RegistrarResultadoRpaApi().Execute(ctx);

            var salidas = ctx.Contexto.OutputParameters;
            Assert.Contains("\"estadofila\":\"digitada\"", (string)salidas[RegistrarResultadoRpaApi.SalidaResultados]);
            Assert.Equal(1, salidas[RegistrarResultadoRpaApi.SalidaProcesadas]);
            Assert.Equal(0, salidas[RegistrarResultadoRpaApi.SalidaConError]);
            Assert.Equal(3, salidas.Count);
            Assert.Null(ctx.UsuarioDelServicio);
        }

        [Fact]
        public void El_parametro_resultados_ausente_se_trata_como_lote_vacio_y_rechaza_la_llamada()
        {
            var (ctx, _, _) = ArmarBot();

            var ex = Assert.Throws<InvalidPluginExecutionException>(() => new RegistrarResultadoRpaApi().Execute(ctx));
            Assert.Contains(ReglasDelRpa.CodigoLoteVacio, ex.Message);
        }

        [Fact]
        public void Un_lote_mas_grande_que_el_tope_rechaza_la_llamada_ENTERA_con_su_codigo()
        {
            var (ctx, svc, _) = ArmarBot(loteMaximo: 1);
            var filaA = SembrarFila(svc, EstadoDeLaFila.Validada);
            var filaB = SembrarFila(svc, EstadoDeLaFila.Validada);
            ctx.Contexto.InputParameters[RegistrarResultadoRpaApi.ParametroResultados] =
                "[" + Item(filaA, "digitada") + "," + Item(filaB, "digitada") + "]";

            var ex = Assert.Throws<InvalidPluginExecutionException>(() => new RegistrarResultadoRpaApi().Execute(ctx));
            Assert.Contains(ReglasDelRpa.CodigoLoteGrande, ex.Message);
        }

        [Fact]
        public void Un_error_real_no_le_cuenta_nada_a_quien_llama_y_el_detalle_va_al_trace()
        {
            var svc = new OrganizationServiceEnMemoria();
            var bot = svc.Sembrar(new Entity(TablasNativas.Usuario) { ["applicationid"] = Guid.NewGuid() });
            var rolId = svc.Sembrar(new Entity(TablasNativas.Rol) { ["name"] = TablasNativas.RolRpa });
            svc.Sembrar(new Entity(TablasNativas.UsuarioRol) { ["systemuserid"] = bot, ["roleid"] = rolId });
            // Sin 'rpa.lote.maximo' ni 'rpa.codigos': error real de configuración.
            var ctx = new ContextoDePluginSimulado(svc, Ahora);
            ctx.Contexto.InitiatingUserId = bot;
            ctx.Contexto.InputParameters[RegistrarResultadoRpaApi.ParametroResultados] = "[" + Item(Guid.NewGuid(), "digitada") + "]";

            var ex = Assert.Throws<InvalidPluginExecutionException>(() => new RegistrarResultadoRpaApi().Execute(ctx));

            Assert.Equal(RegistrarResultadoRpaApi.MensajeDeErrorGenerico, ex.Message);
            Assert.NotEmpty(ctx.Trace.Todo);
            Assert.Empty(ctx.Contexto.OutputParameters);
        }

        [Fact]
        public void Un_proveedor_incompleto_no_revienta_con_una_referencia_nula()
        {
            Assert.Throws<InvalidPluginExecutionException>(() => new RegistrarResultadoRpaApi().Execute(null));
            foreach (var quitar in new[] { "contexto", "fabrica", "trace" })
            {
                var (ctx, _, _) = ArmarBot();
                ctx.Contexto.InputParameters[RegistrarResultadoRpaApi.ParametroResultados] = "[]";
                ctx.DaContexto = quitar != "contexto";
                ctx.DaFabrica = quitar != "fabrica";
                ctx.DaTrace = quitar != "trace";
                Assert.Throws<InvalidPluginExecutionException>(() => new RegistrarResultadoRpaApi().Execute(ctx));
            }
        }
    }
}
