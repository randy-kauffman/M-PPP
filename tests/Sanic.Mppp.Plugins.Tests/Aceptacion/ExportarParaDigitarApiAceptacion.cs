using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Sanic.Mppp.Plugins.Api;
using Sanic.Mppp.Plugins.Datos;
using Sanic.Mppp.Plugins.Dominio;
using Sanic.Mppp.Plugins.Tests.Apoyo;
using Sanic.Mppp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Aceptacion
{
    /// <summary>
    /// El envoltorio `IPlugin` de `sanic_mppp_capi_exportarparadigitar` (DN-02, diseno/DIRECTRICES-NUEVAS.md). Traduce
    /// parámetros; no decide nada del negocio. A diferencia de las Custom API del RPA, ÉSTA no lleva una puerta en
    /// código (ver el XML doc de <see cref="ExportarParaDigitarApi"/>, confirmado por el aprobador el 2026-09-26):
    /// por eso, a diferencia de `ObtenerFilasPendientesApiAceptacion`, acá NO hay una prueba de "llamador no
    /// autorizado".
    /// </summary>
    public class ExportarParaDigitarApiAceptacion
    {
        private static readonly DateTime Ahora = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

        private static Entity Fila(OrganizationServiceEnMemoria svc, string numeroPlan, string nombreBeneficiario)
        {
            var solicitudId = svc.Sembrar(new Entity(TablasHistorico.Solicitud) { ["sanic_nombre"] = "MPPP-00000001" });

            return new Entity(TablasHistorico.Fila)
            {
                ["sanic_estado"] = new OptionSetValue((int)EstadoDeLaFila.Validada),
                ["sanic_fechavalidada"] = Ahora,
                ["sanic_numerofila"] = 1,
                ["sanic_requiereatencionmanual"] = false,
                ["sanic_solicitudid"] = new EntityReference(TablasHistorico.Solicitud, solicitudId),
                ["sanic_numeroplan"] = numeroPlan,
                ["sanic_gestion"] = new OptionSetValue((int)Gestion.Inclusion),
                ["sanic_clasificacion"] = new OptionSetValue((int)Clasificacion.ACH),
                ["sanic_nombrebeneficiario"] = nombreBeneficiario,
                ["sanic_tipoidentificacion"] = new OptionSetValue((int)TipoDeIdentificacion.CNA),
                ["sanic_numeroidentificacion"] = "001-1",
                ["sanic_numerocuenta"] = "111",
                ["sanic_moneda"] = new OptionSetValue((int)Moneda.COR),
                ["sanic_banco"] = new OptionSetValue((int)Banco.BANPRO),
                ["sanic_referencia"] = "REF-1",
                ["sanic_referenciarecibida"] = "REF-1",
            };
        }

        /// <summary>Un llamador con `exportacion.columnas` y `exportacion.maximo` cargados y una Fila lista, listo para llamar a la API.</summary>
        private static (ContextoDePluginSimulado ctx, OrganizationServiceEnMemoria svc, Guid filaId) Armar()
        {
            var svc = new OrganizationServiceEnMemoria();
            svc.Sembrar(new Entity(Tablas.Parametro)
            {
                ["statecode"] = new OptionSetValue(Tablas.Activo),
                ["sanic_nombre"] = ExportarParaDigitar.ParametroColumnas,
                ["sanic_version"] = 1,
                ["sanic_valor"] = @"{""columnas"":[{""campo"":""numeroPlan"",""encabezado"":""Plan""},{""campo"":""nombreBeneficiario"",""encabezado"":""Nombre""}]}",
            });
            svc.Sembrar(new Entity(Tablas.Parametro)
            {
                ["statecode"] = new OptionSetValue(Tablas.Activo),
                ["sanic_nombre"] = ExportarParaDigitar.ParametroMaximo,
                ["sanic_version"] = 1,
                ["sanic_valor"] = "500",
            });
            var filaId = svc.Sembrar(Fila(svc, "0001", "Ana Perez"));

            var ctx = new ContextoDePluginSimulado(svc, Ahora);
            ctx.Contexto.MessageName = "sanic_mppp_capi_exportarparadigitar";
            ctx.Contexto.InputParameters[ExportarParaDigitarApi.ParametroFilaIds] = new[] { filaId.ToString() };
            return (ctx, svc, filaId);
        }

        [Fact]
        public void Devuelve_las_dos_salidas_con_sus_nombres_exactos_y_trabaja_como_system()
        {
            var (ctx, _, _) = Armar();

            new ExportarParaDigitarApi().Execute(ctx);

            var salidas = ctx.Contexto.OutputParameters;
            Assert.Equal(1, salidas[ExportarParaDigitarApi.SalidaExportadas]);
            var archivo = Assert.IsType<string>(salidas[ExportarParaDigitarApi.SalidaArchivo]);
            Assert.False(string.IsNullOrWhiteSpace(archivo));
            Convert.FromBase64String(archivo); // no revienta: es base64 válido
            Assert.Equal(2, salidas.Count);
            Assert.Null(ctx.UsuarioDelServicio); // SYSTEM
            Assert.True(ctx.Trace.Lineas.Count >= 2);
        }

        [Fact]
        public void Filaids_vacio_no_lanza_y_exporta_cero_filas()
        {
            var (ctx, _, _) = Armar();
            ctx.Contexto.InputParameters[ExportarParaDigitarApi.ParametroFilaIds] = new string[0];

            new ExportarParaDigitarApi().Execute(ctx);

            Assert.Equal(0, ctx.Contexto.OutputParameters[ExportarParaDigitarApi.SalidaExportadas]);
        }

        [Fact]
        public void Filaids_ausente_es_un_error_de_entrada()
        {
            var (ctx, svc, _) = Armar();
            ctx.Contexto.InputParameters.Remove(ExportarParaDigitarApi.ParametroFilaIds);

            var ex = Assert.Throws<InvalidPluginExecutionException>(() => new ExportarParaDigitarApi().Execute(ctx));
            Assert.Contains(ExportarParaDigitarApi.ParametroFilaIds, ex.Message);
            Assert.Empty(ctx.Contexto.OutputParameters);
        }

        public static IEnumerable<object[]> FilaidsQueNoSonUnArregloDeTexto()
        {
            yield return new object[] { "un-solo-string-no-un-arreglo" };
            yield return new object[] { 123 };
            yield return new object[] { new Guid[] { Guid.NewGuid() } }; // Guid[], no string[]
        }

        [Theory]
        [MemberData(nameof(FilaidsQueNoSonUnArregloDeTexto))]
        public void Filaids_que_no_es_un_arreglo_de_texto_es_un_error_de_entrada(object valor)
        {
            var (ctx, _, _) = Armar();
            ctx.Contexto.InputParameters[ExportarParaDigitarApi.ParametroFilaIds] = valor;

            var ex = Assert.Throws<InvalidPluginExecutionException>(() => new ExportarParaDigitarApi().Execute(ctx));
            Assert.Contains(ExportarParaDigitarApi.ParametroFilaIds, ex.Message);
        }

        [Theory]
        [InlineData("no soy un guid")]
        [InlineData("")]
        [InlineData(null)]
        public void Un_elemento_de_filaids_que_no_es_un_guid_valido_es_un_error_de_entrada(string valorInvalido)
        {
            var (ctx, _, filaId) = Armar();
            ctx.Contexto.InputParameters[ExportarParaDigitarApi.ParametroFilaIds] = new[] { filaId.ToString(), valorInvalido };

            var ex = Assert.Throws<InvalidPluginExecutionException>(() => new ExportarParaDigitarApi().Execute(ctx));
            Assert.Contains(ExportarParaDigitarApi.ParametroFilaIds, ex.Message);
            Assert.Empty(ctx.Contexto.OutputParameters);
        }

        // ---------- Decision 1 (aprobador, 2026-09-26): revienta nombrando cuales filaid no volvieron ----------

        [Fact]
        public void Un_filaid_que_no_existe_revienta_con_un_mensaje_especifico_no_generico()
        {
            var (ctx, _, filaId) = Armar();
            var noExiste = Guid.NewGuid();
            ctx.Contexto.InputParameters[ExportarParaDigitarApi.ParametroFilaIds] = new[] { filaId.ToString(), noExiste.ToString() };

            var ex = Assert.Throws<InvalidPluginExecutionException>(() => new ExportarParaDigitarApi().Execute(ctx));

            // A diferencia de un error interno, este mensaje SÍ tiene que llegar tal cual a quien llama: el
            // ejecutivo necesita saber cuál fila no se exportó para volver a seleccionarla (decision del aprobador).
            Assert.NotEqual(ExportarParaDigitarApi.MensajeDeErrorGenerico, ex.Message);
            Assert.Contains(noExiste.ToString(), ex.Message);
            Assert.Empty(ctx.Contexto.OutputParameters);
        }

        // ---------- Decision 2 (aprobador, 2026-09-26): el tope revienta con un mensaje especifico ----------

        [Fact]
        public void El_lote_que_supera_exportacion_maximo_revienta_con_un_mensaje_especifico_no_generico()
        {
            var (ctx, svc, filaId) = Armar();
            foreach (var p in svc.Registros(Tablas.Parametro))
            {
                if ((string)p["sanic_nombre"] == ExportarParaDigitar.ParametroMaximo)
                {
                    svc.Update(new Entity(Tablas.Parametro, p.Id) { ["sanic_valor"] = "1" });
                }
            }
            ctx.Contexto.InputParameters[ExportarParaDigitarApi.ParametroFilaIds] = new[] { filaId.ToString(), Guid.NewGuid().ToString() };

            var ex = Assert.Throws<InvalidPluginExecutionException>(() => new ExportarParaDigitarApi().Execute(ctx));

            Assert.NotEqual(ExportarParaDigitarApi.MensajeDeErrorGenerico, ex.Message);
            Assert.Contains(ExportarParaDigitar.ParametroMaximo, ex.Message);
            Assert.Empty(ctx.Contexto.OutputParameters);
        }

        [Fact]
        public void Un_error_real_no_le_cuenta_nada_a_quien_llama_y_el_detalle_va_al_trace()
        {
            var (ctx, svc, _) = Armar();
            // Rompemos la config real dejando el parametro de columnas con un valor mal formado (el de maximo
            // queda intacto: si tambien se corrompiera, revienta por el tope, no por lo que se quiere probar).
            foreach (var p in svc.Registros(Tablas.Parametro))
            {
                if ((string)p["sanic_nombre"] == ExportarParaDigitar.ParametroColumnas)
                {
                    svc.Update(new Entity(Tablas.Parametro, p.Id) { ["sanic_valor"] = "esto no es JSON" });
                }
            }

            var ex = Assert.Throws<InvalidPluginExecutionException>(() => new ExportarParaDigitarApi().Execute(ctx));

            Assert.Equal(ExportarParaDigitarApi.MensajeDeErrorGenerico, ex.Message);
            Assert.NotEmpty(ctx.Trace.Todo);
            Assert.Empty(ctx.Contexto.OutputParameters);
        }

        [Fact]
        public void Un_proveedor_incompleto_no_revienta_con_una_referencia_nula()
        {
            Assert.Throws<InvalidPluginExecutionException>(() => new ExportarParaDigitarApi().Execute(null));
            foreach (var quitar in new[] { "contexto", "fabrica", "trace" })
            {
                var (ctx, _, _) = Armar();
                ctx.DaContexto = quitar != "contexto";
                ctx.DaFabrica = quitar != "fabrica";
                ctx.DaTrace = quitar != "trace";
                Assert.Throws<InvalidPluginExecutionException>(() => new ExportarParaDigitarApi().Execute(ctx));
            }
        }
    }
}
