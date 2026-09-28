using System;
using Microsoft.Xrm.Sdk;
using Sanic.Mppp.Plugins.Api;
using Sanic.Mppp.Plugins.Datos;
using Sanic.Mppp.Plugins.Tests.Apoyo;
using Sanic.Mppp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Aceptacion
{
    /// <summary>El envoltorio `IPlugin` de `sanic_mppp_capi_obtenerfilaspendientes` (diseno/03 §2). Traduce parámetros; no decide nada del negocio.</summary>
    public class ObtenerFilasPendientesApiAceptacion
    {
        private static readonly DateTime Ahora = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

        private const string ListasJson =
            "{\"gestion\":[{\"valor\":\"Inclusion\"}],\"clasificacion\":[{\"valor\":\"ACH\"}]," +
            "\"tipoIdentificacion\":[{\"valor\":\"CNA\"}],\"moneda\":[{\"valor\":\"COR\"}]," +
            "\"banco\":[{\"valor\":\"BANPRO\",\"codigo\":\"008\"}]}";

        /// <summary>Un llamador que SÍ pasa la puerta (identidad de aplicación con el rol del RPA), listo para llamar a la API.</summary>
        private static (ContextoDePluginSimulado ctx, OrganizationServiceEnMemoria svc, Guid bot) ArmarBot(bool conRol = true)
        {
            var svc = new OrganizationServiceEnMemoria();
            var bot = svc.Sembrar(new Entity(TablasNativas.Usuario) { ["applicationid"] = Guid.NewGuid() });
            var rolId = svc.Sembrar(new Entity(TablasNativas.Rol) { ["name"] = TablasNativas.RolRpa });
            if (conRol)
            {
                svc.Sembrar(new Entity(TablasNativas.UsuarioRol) { ["systemuserid"] = bot, ["roleid"] = rolId });
            }

            svc.Sembrar(new Entity(Tablas.Parametro) { ["statecode"] = new OptionSetValue(Tablas.Activo), ["sanic_nombre"] = "plantilla.listas", ["sanic_version"] = 1, ["sanic_valor"] = ListasJson });

            var ctx = new ContextoDePluginSimulado(svc, Ahora);
            ctx.Contexto.InitiatingUserId = bot;
            ctx.Contexto.MessageName = "sanic_mppp_capi_obtenerfilaspendientes";
            ctx.Contexto.InputParameters[ObtenerFilasPendientesApi.ParametroClaimMinutos] = 10;
            return (ctx, svc, bot);
        }

        [Fact]
        public void Un_llamador_no_autorizado_no_pasa_de_la_primera_linea()
        {
            var (ctx, svc, _) = ArmarBot(conRol: false);

            Assert.Throws<InvalidPluginExecutionException>(() => new ObtenerFilasPendientesApi().Execute(ctx));

            // La puerta se evalúa ANTES de leer 'claimminutos' o tocar ninguna Fila.
            Assert.DoesNotContain(svc.Llamadas, l => l.Entidad == TablasHistorico.Fila);
        }

        [Fact]
        public void Sin_filas_pendientes_devuelve_las_tres_salidas_con_sus_nombres_exactos()
        {
            var (ctx, _, _) = ArmarBot();

            new ObtenerFilasPendientesApi().Execute(ctx);

            var salidas = ctx.Contexto.OutputParameters;
            Assert.Equal("[]", salidas[ObtenerFilasPendientesApi.SalidaFilas]);
            Assert.Equal(0, salidas[ObtenerFilasPendientesApi.SalidaEntregadas]);
            Assert.Equal(0, salidas[ObtenerFilasPendientesApi.SalidaPendientes]);
            Assert.Equal(3, salidas.Count);
            Assert.Null(ctx.UsuarioDelServicio); // SYSTEM
            Assert.True(ctx.Trace.Lineas.Count >= 2);
        }

        [Fact]
        public void Maximo_ausente_usa_el_defecto_de_100_sin_lanzar()
        {
            var (ctx, _, _) = ArmarBot();
            ctx.Contexto.InputParameters.Remove(ObtenerFilasPendientesApi.ParametroMaximo);

            new ObtenerFilasPendientesApi().Execute(ctx); // no lanza
        }

        /// <summary>
        /// D-61, corregido `diseno/03-contratos-custom-api.md` §2 (nota fechada 2026-09-28): en una Function,
        /// Dataverse pone la clave en `InputParameters` con el DEFAULT del tipo (`default(int) == 0`) también
        /// cuando el bot omite el parámetro — la clave queda PRESENTE, nunca ausente. Ausente y 0 son
        /// indistinguibles, así que un bot que sigue el contrato al pie de la letra y no manda `maximo` hoy
        /// recibe HTTP 400 real. El fixture reproduce eso (la clave presente con 0), no la remueve: removerla
        /// modelaría una plataforma que no existe (lección de D-59). Decisión del aprobador: 0 pasa a significar
        /// "el defecto" (100). Se prueba con 2 filas sembradas para distinguir "usa el defecto" de "cero filas":
        /// si el código tratara 0 como límite literal, `entregadas` daría 0, no 2.
        /// </summary>
        [Fact]
        public void Maximo_en_cero_se_trata_como_ausente_y_usa_el_defecto()
        {
            var (ctx, svc, _) = ArmarBot();
            ctx.Contexto.InputParameters[ObtenerFilasPendientesApi.ParametroMaximo] = 0;

            var planId = svc.Sembrar(new Entity(Tablas.Plan) { ["statecode"] = new OptionSetValue(Tablas.Activo), ["sanic_tipoformato"] = new OptionSetValue((int)Sanic.Mppp.Plugins.Dominio.TipoDeFormatoDelPlan._06) });
            var solicitudId = svc.Sembrar(new Entity(TablasHistorico.Solicitud) { ["sanic_nombre"] = "MPPP-00000123" });
            for (var i = 1; i <= 2; i++)
            {
                svc.Sembrar(new Entity(TablasHistorico.Fila)
                {
                    ["sanic_estado"] = new OptionSetValue((int)Sanic.Mppp.Plugins.Dominio.EstadoDeLaFila.Validada),
                    ["sanic_fechavalidada"] = Ahora,
                    ["sanic_numerofila"] = i,
                    ["sanic_requiereatencionmanual"] = false,
                    ["sanic_solicitudid"] = new EntityReference(TablasHistorico.Solicitud, solicitudId),
                    ["sanic_planid"] = new EntityReference(Tablas.Plan, planId),
                });
            }

            new ObtenerFilasPendientesApi().Execute(ctx); // no lanza: 0 es "el defecto", no "cero filas"

            var salidas = ctx.Contexto.OutputParameters;
            Assert.Equal(2, salidas[ObtenerFilasPendientesApi.SalidaEntregadas]);
            Assert.Equal(0, salidas[ObtenerFilasPendientesApi.SalidaPendientes]);
        }

        /// <summary>
        /// `-1` sigue siendo error: a diferencia de 0, un negativo NUNCA es el default de un `int` omitido
        /// (`default(int) == 0`), así que es distinguible de la ausencia — es un valor que alguien mandó a propósito.
        /// </summary>
        [Theory]
        [InlineData(-1)]
        [InlineData("no soy un entero")]
        public void Maximo_presente_pero_invalido_es_un_error_de_entrada(object valor)
        {
            var (ctx, svc, _) = ArmarBot();
            ctx.Contexto.InputParameters[ObtenerFilasPendientesApi.ParametroMaximo] = valor;

            var ex = Assert.Throws<InvalidPluginExecutionException>(() => new ObtenerFilasPendientesApi().Execute(ctx));
            Assert.Contains(ObtenerFilasPendientesApi.ParametroMaximo, ex.Message);
            Assert.Empty(ctx.Contexto.OutputParameters);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        [InlineData(-1)]
        public void Claimminutos_ausente_o_invalido_es_un_error_de_entrada(object valor)
        {
            var (ctx, _, _) = ArmarBot();
            ctx.Contexto.InputParameters.Remove(ObtenerFilasPendientesApi.ParametroClaimMinutos);
            if (valor != null)
            {
                ctx.Contexto.InputParameters[ObtenerFilasPendientesApi.ParametroClaimMinutos] = valor;
            }

            var ex = Assert.Throws<InvalidPluginExecutionException>(() => new ObtenerFilasPendientesApi().Execute(ctx));
            Assert.Contains(ObtenerFilasPendientesApi.ParametroClaimMinutos, ex.Message);
        }

        [Fact]
        public void Un_error_real_no_le_cuenta_nada_a_quien_llama_y_el_detalle_va_al_trace()
        {
            var (ctx, svc, _) = ArmarBot();
            // Sin 'plantilla.listas' seria un problema, pero acá directamente rompemos la tabla de parámetros
            // dejando el parámetro con un valor de listas mal formado, para forzar un error real al armar el JSON.
            foreach (var p in svc.Registros(Tablas.Parametro))
            {
                svc.Update(new Entity(Tablas.Parametro, p.Id) { ["sanic_valor"] = "esto no es JSON" });
            }

            var planId = svc.Sembrar(new Entity(Tablas.Plan) { ["statecode"] = new OptionSetValue(Tablas.Activo), ["sanic_tipoformato"] = new OptionSetValue((int)Sanic.Mppp.Plugins.Dominio.TipoDeFormatoDelPlan._06) });
            var solicitudId = svc.Sembrar(new Entity(TablasHistorico.Solicitud) { ["sanic_nombre"] = "MPPP-00000123" });
            svc.Sembrar(new Entity(TablasHistorico.Fila)
            {
                ["sanic_estado"] = new OptionSetValue((int)Sanic.Mppp.Plugins.Dominio.EstadoDeLaFila.Validada),
                ["sanic_fechavalidada"] = Ahora,
                ["sanic_numerofila"] = 1,
                ["sanic_requiereatencionmanual"] = false,
                ["sanic_solicitudid"] = new EntityReference(TablasHistorico.Solicitud, solicitudId),
                ["sanic_planid"] = new EntityReference(Tablas.Plan, planId),
            });

            var ex = Assert.Throws<InvalidPluginExecutionException>(() => new ObtenerFilasPendientesApi().Execute(ctx));

            Assert.Equal(ObtenerFilasPendientesApi.MensajeDeErrorGenerico, ex.Message);
            Assert.NotEmpty(ctx.Trace.Todo);
            Assert.Empty(ctx.Contexto.OutputParameters);
        }

        [Fact]
        public void Un_proveedor_incompleto_no_revienta_con_una_referencia_nula()
        {
            Assert.Throws<InvalidPluginExecutionException>(() => new ObtenerFilasPendientesApi().Execute(null));
            foreach (var quitar in new[] { "contexto", "fabrica", "trace" })
            {
                var (ctx, _, _) = ArmarBot();
                ctx.DaContexto = quitar != "contexto";
                ctx.DaFabrica = quitar != "fabrica";
                ctx.DaTrace = quitar != "trace";
                Assert.Throws<InvalidPluginExecutionException>(() => new ObtenerFilasPendientesApi().Execute(ctx));
            }
        }
    }
}
