using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Mppp.Plugins.Api;
using Sanic.Mppp.Plugins.Datos;
using Sanic.Mppp.Plugins.Dominio;
using Sanic.Mppp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Aceptacion
{
    /// <summary>
    /// El plugin de `sanic_mppp_capi_obtenerfilaspendientes` (diseno/03 §2), sin el envoltorio `IPlugin`. Orquesta
    /// piezas ya probadas por separado (<see cref="SeleccionParaElRpa"/>, <see cref="ReglasDelRpa"/>,
    /// <see cref="ArmadorJsonDelRpa"/>): estas pruebas verifican el ENSAMBLAJE, no vuelven a probar cada regla.
    /// </summary>
    public class ObtenerFilasPendientesAceptacion
    {
        private static readonly DateTime Ahora = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
        private static readonly Guid ElBot = Guid.NewGuid();

        private const string ListasJson =
            "{\"gestion\":[{\"valor\":\"Inclusion\"}],\"clasificacion\":[{\"valor\":\"ACH\"}]," +
            "\"tipoIdentificacion\":[{\"valor\":\"CNA\"}],\"moneda\":[{\"valor\":\"COR\"}]," +
            "\"banco\":[{\"valor\":\"BANPRO\",\"codigo\":\"008\"}]}";

        private static (OrganizationServiceEnMemoria svc, Guid planId, Guid solicitudId) ArmarBase()
        {
            var svc = new OrganizationServiceEnMemoria();
            svc.Sembrar(new Entity(Tablas.Parametro) { ["statecode"] = new OptionSetValue(Tablas.Activo), ["sanic_nombre"] = "plantilla.listas", ["sanic_version"] = 1, ["sanic_valor"] = ListasJson });
            var planId = svc.Sembrar(new Entity(Tablas.Plan) { ["statecode"] = new OptionSetValue(Tablas.Activo), ["sanic_tipoformato"] = new OptionSetValue((int)TipoDeFormatoDelPlan._06) });
            var solicitudId = svc.Sembrar(new Entity(TablasHistorico.Solicitud) { ["sanic_nombre"] = "MPPP-00000123" });
            return (svc, planId, solicitudId);
        }

        private static Guid SembrarFila(OrganizationServiceEnMemoria svc, Guid planId, Guid solicitudId, EstadoDeLaFila estado, DateTime fechaValidada, int numeroFila, Guid? digitadaPor = null)
        {
            var e = new Entity(TablasHistorico.Fila)
            {
                ["sanic_estado"] = new OptionSetValue((int)estado),
                ["sanic_fechavalidada"] = fechaValidada,
                ["sanic_numerofila"] = numeroFila,
                ["sanic_requiereatencionmanual"] = false,
                ["sanic_solicitudid"] = new EntityReference(TablasHistorico.Solicitud, solicitudId),
                ["sanic_planid"] = new EntityReference(Tablas.Plan, planId),
                ["sanic_numeroplan"] = "0001",
                ["sanic_gestion"] = new OptionSetValue((int)Gestion.Inclusion),
                ["sanic_clasificacion"] = new OptionSetValue((int)Clasificacion.ACH),
                ["sanic_nombrebeneficiario"] = "Juan Perez",
                ["sanic_tipoidentificacion"] = new OptionSetValue((int)TipoDeIdentificacion.CNA),
                ["sanic_numeroidentificacion"] = "001-000000-0000A",
                ["sanic_numerocuenta"] = "123456",
                ["sanic_moneda"] = new OptionSetValue((int)Moneda.COR),
                ["sanic_banco"] = new OptionSetValue((int)Banco.BANPRO),
                ["sanic_referencia"] = "008" + "12345678901234567".Substring(0, 14),
                ["sanic_referenciarecibida"] = "12345",
            };
            if (digitadaPor.HasValue)
            {
                e["sanic_digitadapor"] = new EntityReference(TablasNativas.Usuario, digitadaPor.Value);
            }

            return svc.Sembrar(e);
        }

        [Fact]
        public void Entrega_las_filas_elegibles_en_orden_FIFO_y_refresca_el_claim()
        {
            var (svc, planId, solicitudId) = ArmarBase();
            var primera = SembrarFila(svc, planId, solicitudId, EstadoDeLaFila.Validada, Ahora.AddMinutes(-30), 1);
            var segunda = SembrarFila(svc, planId, solicitudId, EstadoDeLaFila.Validada, Ahora.AddMinutes(-10), 2);

            var core = new ObtenerFilasPendientes(new FilasDataverse(svc), new CatalogosDataverse(svc));
            var resultado = core.Ejecutar(maximoPedido: 100, claimMinutosPedido: 10, claimMinutosTope: 60, rpaPuedeAprobar: false, quienLlama: ElBot, ahoraUtc: Ahora);

            Assert.Equal(2, resultado.Entregadas);
            Assert.Equal(0, resultado.Pendientes);
            Assert.Contains("\"filaid\":\"" + primera + "\"", resultado.FilasJson);
            Assert.Contains("\"filaid\":\"" + segunda + "\"", resultado.FilasJson);
            Assert.True(resultado.FilasJson.IndexOf(primera.ToString(), StringComparison.Ordinal) < resultado.FilasJson.IndexOf(segunda.ToString(), StringComparison.Ordinal));

            foreach (var filaId in new[] { primera, segunda })
            {
                var registro = svc.Registros(TablasHistorico.Fila).Single(f => f.Id == filaId);
                Assert.Equal(Ahora.AddMinutes(10), registro["sanic_claimhasta"]);
            }
        }

        [Fact]
        public void El_JSON_trae_el_banco_partido_en_texto_y_codigo_de_3_digitos()
        {
            var (svc, planId, solicitudId) = ArmarBase();
            SembrarFila(svc, planId, solicitudId, EstadoDeLaFila.Validada, Ahora, 1);

            var core = new ObtenerFilasPendientes(new FilasDataverse(svc), new CatalogosDataverse(svc));
            var resultado = core.Ejecutar(100, 10, 60, false, ElBot, Ahora);

            Assert.Contains("\"banco\":\"BANPRO\"", resultado.FilasJson);
            Assert.Contains("\"bancocodigo\":\"008\"", resultado.FilasJson);
            Assert.Contains("\"tipoformato\":\"06\"", resultado.FilasJson);
            Assert.Contains("\"solicitud\":\"MPPP-00000123\"", resultado.FilasJson);
        }

        [Fact]
        public void Recorta_a_maximo_pero_pendientes_cuenta_TODAS_las_elegibles_restantes()
        {
            var (svc, planId, solicitudId) = ArmarBase();
            for (var i = 1; i <= 5; i++)
            {
                SembrarFila(svc, planId, solicitudId, EstadoDeLaFila.Validada, Ahora.AddMinutes(-i), i);
            }

            var core = new ObtenerFilasPendientes(new FilasDataverse(svc), new CatalogosDataverse(svc));
            var resultado = core.Ejecutar(maximoPedido: 2, claimMinutosPedido: 10, claimMinutosTope: 60, rpaPuedeAprobar: false, quienLlama: ElBot, ahoraUtc: Ahora);

            Assert.Equal(2, resultado.Entregadas);
            Assert.Equal(3, resultado.Pendientes);
        }

        [Fact]
        public void No_entrega_filas_marcadas_para_atencion_manual_y_el_claim_no_las_excluye_pero_tampoco_las_entrega()
        {
            var (svc, planId, solicitudId) = ArmarBase();
            var conAtencion = SembrarFila(svc, planId, solicitudId, EstadoDeLaFila.Validada, Ahora, 1);
            svc.Update(new Entity(TablasHistorico.Fila, conAtencion) { ["sanic_requiereatencionmanual"] = true });

            var core = new ObtenerFilasPendientes(new FilasDataverse(svc), new CatalogosDataverse(svc));
            var resultado = core.Ejecutar(100, 10, 60, false, ElBot, Ahora);

            Assert.Equal(0, resultado.Entregadas);
            Assert.Equal(0, resultado.Pendientes);
        }

        [Fact]
        public void Sin_filas_elegibles_devuelve_un_arreglo_JSON_vacio_sin_tocar_ninguna_fila()
        {
            var (svc, _, _) = ArmarBase();

            var core = new ObtenerFilasPendientes(new FilasDataverse(svc), new CatalogosDataverse(svc));
            var resultado = core.Ejecutar(100, 10, 60, false, ElBot, Ahora);

            Assert.Equal("[]", resultado.FilasJson);
            Assert.Equal(0, resultado.Entregadas);
            Assert.Equal(0, resultado.Pendientes);
        }

        [Fact]
        public void Una_Digitada_por_el_propio_bot_entra_para_que_la_apruebe_si_puedeAprobar()
        {
            var (svc, planId, solicitudId) = ArmarBase();
            SembrarFila(svc, planId, solicitudId, EstadoDeLaFila.Digitada, Ahora, 1, digitadaPor: ElBot);

            var core = new ObtenerFilasPendientes(new FilasDataverse(svc), new CatalogosDataverse(svc));
            var resultado = core.Ejecutar(100, 10, 60, rpaPuedeAprobar: true, quienLlama: ElBot, ahoraUtc: Ahora);

            Assert.Equal(1, resultado.Entregadas);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Maximo_cero_o_negativo_es_un_error(int maximo)
        {
            var (svc, _, _) = ArmarBase();
            var core = new ObtenerFilasPendientes(new FilasDataverse(svc), new CatalogosDataverse(svc));
            Assert.Throws<ArgumentException>(() => core.Ejecutar(maximo, 10, 60, false, ElBot, Ahora));
        }

        [Fact]
        public void Sin_el_parametro_plantilla_listas_es_un_error_real_si_hay_algo_que_entregar()
        {
            var svc = new OrganizationServiceEnMemoria();
            var planId = svc.Sembrar(new Entity(Tablas.Plan) { ["statecode"] = new OptionSetValue(Tablas.Activo), ["sanic_tipoformato"] = new OptionSetValue((int)TipoDeFormatoDelPlan._06) });
            var solicitudId = svc.Sembrar(new Entity(TablasHistorico.Solicitud) { ["sanic_nombre"] = "MPPP-00000123" });
            SembrarFila(svc, planId, solicitudId, EstadoDeLaFila.Validada, Ahora, 1);

            var core = new ObtenerFilasPendientes(new FilasDataverse(svc), new CatalogosDataverse(svc));
            Assert.Throws<InvalidOperationException>(() => core.Ejecutar(100, 10, 60, false, ElBot, Ahora));
        }
    }
}
