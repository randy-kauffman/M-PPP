using System;
using System.Globalization;
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
    /// El plugin de `sanic_mppp_capi_registrarresultadorpa` (diseno/03 §3), sin el envoltorio `IPlugin`. Reusa
    /// <see cref="TransicionesDeFila"/> (la máquina del step de §4) para decidir cada transición puntual del RPA;
    /// estas pruebas verifican el ENSAMBLAJE del lote (por ítem, nunca todo o nada) y las reglas propias de esta
    /// Custom API (idempotencia, los dos textos, atención manual), no vuelven a probar la máquina de estados entera.
    /// </summary>
    public class RegistrarResultadoRpaAceptacion
    {
        private static readonly DateTime Ahora = new DateTime(2026, 10, 5, 15, 0, 0, DateTimeKind.Utc);
        private static readonly Guid ElBot = Guid.NewGuid();

        private const string Codigos =
            "{\"OK\":{\"cliente\":null,\"nota\":\"AS400 acepto la operacion\"}," +
            "\"RECH-GEN\":{\"cliente\":\"Su operacion no pudo completarse.\",\"nota\":\"Rechazo generico\"}}";

        private static OrganizationServiceEnMemoria ArmarServicio(int loteMaximo = 50)
        {
            var svc = new OrganizationServiceEnMemoria();
            svc.Sembrar(new Entity(Tablas.Parametro) { ["statecode"] = new OptionSetValue(Tablas.Activo), ["sanic_nombre"] = "rpa.lote.maximo", ["sanic_version"] = 1, ["sanic_valor"] = loteMaximo.ToString(CultureInfo.InvariantCulture) });
            svc.Sembrar(new Entity(Tablas.Parametro) { ["statecode"] = new OptionSetValue(Tablas.Activo), ["sanic_nombre"] = "rpa.codigos", ["sanic_version"] = 1, ["sanic_valor"] = Codigos });
            return svc;
        }

        private static Guid SembrarFila(OrganizationServiceEnMemoria svc, EstadoDeLaFila estado, Guid? digitadaPor = null, bool atencionManual = false)
        {
            var e = new Entity(TablasHistorico.Fila)
            {
                ["sanic_estado"] = new OptionSetValue((int)estado),
                ["sanic_requiereatencionmanual"] = atencionManual,
            };
            if (digitadaPor.HasValue)
            {
                e["sanic_digitadapor"] = new EntityReference(TablasNativas.Usuario, digitadaPor.Value);
            }

            return svc.Sembrar(e);
        }

        private static RegistrarResultadoRpa Core(OrganizationServiceEnMemoria svc) =>
            new RegistrarResultadoRpa(new FilasDataverse(svc), new CatalogosDataverse(svc));

        private static string Item(Guid filaId, string resultado, string codigo = "OK", string mensaje = null, DateTime? fecha = null) =>
            "{\"filaid\":\"" + filaId + "\",\"resultado\":\"" + resultado + "\"," +
            (mensaje != null ? "\"mensaje\":\"" + mensaje + "\"," : "") +
            "\"codigo\":\"" + codigo + "\"," +
            "\"fechaproceso\":\"" + (fecha ?? Ahora).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) + "\"}";

        private static string Lote(params string[] items) => "[" + string.Join(",", items) + "]";

        private Entity Registro(OrganizationServiceEnMemoria svc, Guid filaId) => svc.Registros(TablasHistorico.Fila).Single(f => f.Id == filaId);

        // ---------- digitada ----------

        [Fact]
        public void Digitada_mueve_Validada_a_Digitada_y_pone_digitadapor_y_fecha()
        {
            var svc = ArmarServicio();
            var filaId = SembrarFila(svc, EstadoDeLaFila.Validada);

            var resultado = Core(svc).Ejecutar(Lote(Item(filaId, "digitada")), ElBot, Ahora, 24, false);

            Assert.Equal(1, resultado.Procesadas);
            Assert.Equal(0, resultado.ConError);
            Assert.Contains("\"estadofila\":\"digitada\"", resultado.ResultadosJson);
            Assert.Contains("\"yaregistrado\":false", resultado.ResultadosJson);

            var registro = Registro(svc, filaId);
            Assert.Equal((int)EstadoDeLaFila.Digitada, ((OptionSetValue)registro["sanic_estado"]).Value);
            Assert.Equal(ElBot, ((EntityReference)registro["sanic_digitadapor"]).Id);
        }

        [Fact]
        public void Repetir_digitada_sobre_una_fila_YA_digitada_es_idempotente()
        {
            var svc = ArmarServicio();
            var filaId = SembrarFila(svc, EstadoDeLaFila.Digitada, digitadaPor: ElBot);

            var resultado = Core(svc).Ejecutar(Lote(Item(filaId, "digitada")), ElBot, Ahora, 24, false);

            Assert.Equal(1, resultado.Procesadas);
            Assert.Contains("\"yaregistrado\":true", resultado.ResultadosJson);
            Assert.DoesNotContain("\"error\"", resultado.ResultadosJson);
        }

        [Fact]
        public void Si_una_persona_ya_actuo_no_es_error_y_estadofila_dice_la_verdad()
        {
            var svc = ArmarServicio();
            var filaId = SembrarFila(svc, EstadoDeLaFila.Anulada);

            var resultado = Core(svc).Ejecutar(Lote(Item(filaId, "digitada")), ElBot, Ahora, 24, false);

            Assert.Equal(1, resultado.Procesadas);
            Assert.Equal(0, resultado.ConError);
            Assert.Contains("\"estadofila\":\"anulada\"", resultado.ResultadosJson);
            Assert.Contains("\"yaregistrado\":true", resultado.ResultadosJson);
        }

        // ---------- aprobada ----------

        [Fact]
        public void Aprobada_mueve_Digitada_a_Aprobada_cuando_puedeAprobar_y_es_el_mismo_bot_y_libera_el_claim()
        {
            var svc = ArmarServicio();
            var filaId = SembrarFila(svc, EstadoDeLaFila.Digitada, digitadaPor: ElBot);
            svc.Update(new Entity(TablasHistorico.Fila, filaId) { ["sanic_claimhasta"] = Ahora.AddMinutes(5) });

            var resultado = Core(svc).Ejecutar(Lote(Item(filaId, "aprobada")), ElBot, Ahora, 24, rpaPuedeAprobar: true);

            Assert.Equal(0, resultado.ConError);
            Assert.Contains("\"estadofila\":\"aprobada\"", resultado.ResultadosJson);
            var registro = Registro(svc, filaId);
            Assert.Equal((int)EstadoDeLaFila.Aprobada, ((OptionSetValue)registro["sanic_estado"]).Value);
            Assert.Null(registro["sanic_claimhasta"]);
        }

        [Fact]
        public void Aprobada_con_rpa_puedeaprobar_en_NO_devuelve_error_y_la_fila_queda_Digitada()
        {
            var svc = ArmarServicio();
            var filaId = SembrarFila(svc, EstadoDeLaFila.Digitada, digitadaPor: ElBot);

            var resultado = Core(svc).Ejecutar(Lote(Item(filaId, "aprobada")), ElBot, Ahora, 24, rpaPuedeAprobar: false);

            Assert.Equal(0, resultado.Procesadas);
            Assert.Equal(1, resultado.ConError);
            Assert.Contains("\"error\"", resultado.ResultadosJson);
            var registro = Registro(svc, filaId);
            Assert.Equal((int)EstadoDeLaFila.Digitada, ((OptionSetValue)registro["sanic_estado"]).Value);
        }

        [Fact]
        public void Aprobada_de_lo_que_digito_OTRO_bot_o_una_persona_devuelve_error_segregacion()
        {
            var svc = ArmarServicio();
            var otroUsuario = Guid.NewGuid();
            var filaId = SembrarFila(svc, EstadoDeLaFila.Digitada, digitadaPor: otroUsuario);

            var resultado = Core(svc).Ejecutar(Lote(Item(filaId, "aprobada")), ElBot, Ahora, 24, rpaPuedeAprobar: true);

            Assert.Equal(1, resultado.ConError);
        }

        // ---------- rechazada ----------

        [Fact]
        public void Rechazada_escribe_el_texto_traducido_al_cliente_y_el_crudo_a_notainterna_por_separado()
        {
            var svc = ArmarServicio();
            var filaId = SembrarFila(svc, EstadoDeLaFila.Validada);

            var resultado = Core(svc).Ejecutar(
                Lote(Item(filaId, "rechazada", codigo: "RECH-GEN", mensaje: "CUENTA INACTIVA")), ElBot, Ahora, 24, false);

            Assert.Equal(0, resultado.ConError);
            var registro = Registro(svc, filaId);
            Assert.Equal((int)EstadoDeLaFila.RechazadaEnAS400, ((OptionSetValue)registro["sanic_estado"]).Value);
            Assert.Equal("Su operacion no pudo completarse.", registro["sanic_mensaje"]);
            Assert.Equal("CUENTA INACTIVA", registro["sanic_notainterna"]);
            Assert.Null(registro["sanic_claimhasta"]);
        }

        [Fact]
        public void Rechazada_sin_mensaje_es_MPPP_RPA_004()
        {
            var svc = ArmarServicio();
            var filaId = SembrarFila(svc, EstadoDeLaFila.Validada);

            var resultado = Core(svc).Ejecutar(Lote(Item(filaId, "rechazada", codigo: "RECH-GEN")), ElBot, Ahora, 24, false);

            Assert.Equal(1, resultado.ConError);
            Assert.Contains("MPPP-RPA-004", resultado.ResultadosJson);
        }

        // ---------- atencionmanual ----------

        [Fact]
        public void AtencionManual_prende_la_marca_deja_la_nota_y_libera_el_claim_sin_cambiar_el_estado()
        {
            var svc = ArmarServicio();
            var filaId = SembrarFila(svc, EstadoDeLaFila.Validada);
            svc.Update(new Entity(TablasHistorico.Fila, filaId) { ["sanic_claimhasta"] = Ahora.AddMinutes(5) });

            var resultado = Core(svc).Ejecutar(
                Lote(Item(filaId, "atencionmanual", codigo: "RECH-GEN", mensaje: "condicion no mapeada XYZ")), ElBot, Ahora, 24, false);

            Assert.Equal(0, resultado.ConError);
            Assert.Contains("\"estadofila\":\"validada\"", resultado.ResultadosJson);
            var registro = Registro(svc, filaId);
            Assert.Equal((int)EstadoDeLaFila.Validada, ((OptionSetValue)registro["sanic_estado"]).Value);
            Assert.True((bool)registro["sanic_requiereatencionmanual"]);
            Assert.Equal("condicion no mapeada XYZ", registro["sanic_notainterna"]);
            Assert.Null(registro["sanic_claimhasta"]);
        }

        [Fact]
        public void AtencionManual_tambien_vale_sobre_una_fila_Digitada()
        {
            var svc = ArmarServicio();
            var filaId = SembrarFila(svc, EstadoDeLaFila.Digitada, digitadaPor: ElBot);

            var resultado = Core(svc).Ejecutar(
                Lote(Item(filaId, "atencionmanual", codigo: "RECH-GEN", mensaje: "no se puede aprobar")), ElBot, Ahora, 24, false);

            Assert.Equal(0, resultado.ConError);
            var registro = Registro(svc, filaId);
            Assert.Equal((int)EstadoDeLaFila.Digitada, ((OptionSetValue)registro["sanic_estado"]).Value); // no cambia
            Assert.True((bool)registro["sanic_requiereatencionmanual"]);
        }

        [Fact]
        public void AtencionManual_sin_mensaje_es_MPPP_RPA_004()
        {
            var svc = ArmarServicio();
            var filaId = SembrarFila(svc, EstadoDeLaFila.Validada);

            var resultado = Core(svc).Ejecutar(Lote(Item(filaId, "atencionmanual", codigo: "RECH-GEN")), ElBot, Ahora, 24, false);

            Assert.Equal(1, resultado.ConError);
            Assert.Contains("MPPP-RPA-004", resultado.ResultadosJson);
        }

        // ---------- validaciones por ítem ----------

        [Fact]
        public void Filaid_que_no_existe_es_MPPP_RPA_001_y_estadofila_es_nulo()
        {
            var svc = ArmarServicio();
            var inexistente = Guid.NewGuid();

            var resultado = Core(svc).Ejecutar(Lote(Item(inexistente, "digitada")), ElBot, Ahora, 24, false);

            Assert.Equal(1, resultado.ConError);
            Assert.Contains("MPPP-RPA-001", resultado.ResultadosJson);
            Assert.Contains("\"estadofila\":null", resultado.ResultadosJson);
        }

        [Fact]
        public void Resultado_invalido_es_MPPP_RPA_002()
        {
            var svc = ArmarServicio();
            var filaId = SembrarFila(svc, EstadoDeLaFila.Validada);

            var resultado = Core(svc).Ejecutar(Lote(Item(filaId, "digitando")), ElBot, Ahora, 24, false);

            Assert.Equal(1, resultado.ConError);
            Assert.Contains("MPPP-RPA-002", resultado.ResultadosJson);
        }

        [Fact]
        public void Fecha_sin_offset_es_el_codigo_de_fecha()
        {
            var svc = ArmarServicio();
            var filaId = SembrarFila(svc, EstadoDeLaFila.Validada);
            var item = "{\"filaid\":\"" + filaId + "\",\"resultado\":\"digitada\",\"codigo\":\"OK\",\"fechaproceso\":\"2026-10-05T15:00:00\"}";

            var resultado = Core(svc).Ejecutar(Lote(item), ElBot, Ahora, 24, false);

            Assert.Equal(1, resultado.ConError);
            Assert.Contains(ReglasDelRpa.CodigoFecha, resultado.ResultadosJson);
        }

        [Fact]
        public void Fecha_en_el_futuro_es_el_codigo_de_fecha()
        {
            var svc = ArmarServicio();
            var filaId = SembrarFila(svc, EstadoDeLaFila.Validada);

            var resultado = Core(svc).Ejecutar(Lote(Item(filaId, "digitada", fecha: Ahora.AddHours(2))), ElBot, Ahora, 24, false);

            Assert.Equal(1, resultado.ConError);
            Assert.Contains(ReglasDelRpa.CodigoFecha, resultado.ResultadosJson);
        }

        [Fact]
        public void Codigo_que_no_esta_en_el_catalogo_es_MPPP_RPA_005()
        {
            var svc = ArmarServicio();
            var filaId = SembrarFila(svc, EstadoDeLaFila.Validada);

            var resultado = Core(svc).Ejecutar(Lote(Item(filaId, "digitada", codigo: "NO-MAPEADO")), ElBot, Ahora, 24, false);

            Assert.Equal(1, resultado.ConError);
            Assert.Contains("MPPP-RPA-005", resultado.ResultadosJson);
        }

        // ---------- el lote: nunca todo o nada ----------

        [Fact]
        public void Un_item_con_error_NO_revierte_los_demas_del_mismo_lote()
        {
            var svc = ArmarServicio();
            var buena1 = SembrarFila(svc, EstadoDeLaFila.Validada);
            var mala = SembrarFila(svc, EstadoDeLaFila.Validada);
            var buena2 = SembrarFila(svc, EstadoDeLaFila.Validada);

            var resultado = Core(svc).Ejecutar(
                Lote(Item(buena1, "digitada"), Item(mala, "digitada", codigo: "NO-MAPEADO"), Item(buena2, "digitada")),
                ElBot, Ahora, 24, false);

            Assert.Equal(2, resultado.Procesadas);
            Assert.Equal(1, resultado.ConError);
            Assert.Equal((int)EstadoDeLaFila.Digitada, ((OptionSetValue)Registro(svc, buena1)["sanic_estado"]).Value);
            Assert.Equal((int)EstadoDeLaFila.Digitada, ((OptionSetValue)Registro(svc, buena2)["sanic_estado"]).Value);
            Assert.Equal((int)EstadoDeLaFila.Validada, ((OptionSetValue)Registro(svc, mala)["sanic_estado"]).Value);
        }

        // ---------- el lote entero ----------

        [Fact]
        public void Un_lote_vacio_se_rechaza_ENTERO()
        {
            var svc = ArmarServicio();
            Assert.Throws<ArgumentException>(() => Core(svc).Ejecutar("[]", ElBot, Ahora, 24, false));
        }

        [Fact]
        public void Un_lote_mas_grande_que_rpa_lote_maximo_se_rechaza_ENTERO()
        {
            var svc = ArmarServicio(loteMaximo: 2);
            var filas = Enumerable.Range(0, 3).Select(_ => SembrarFila(svc, EstadoDeLaFila.Validada)).ToArray();
            var items = filas.Select(f => Item(f, "digitada")).ToArray();

            var e = Assert.Throws<ArgumentException>(() => Core(svc).Ejecutar(Lote(items), ElBot, Ahora, 24, false));
            Assert.Contains(ReglasDelRpa.CodigoLoteGrande, e.Message);

            // Nada se procesó: ninguna fila cambió de estado.
            foreach (var f in filas)
            {
                Assert.Equal((int)EstadoDeLaFila.Validada, ((OptionSetValue)Registro(svc, f)["sanic_estado"]).Value);
            }
        }

        [Fact]
        public void Sin_el_parametro_rpa_lote_maximo_es_un_error_real()
        {
            var svc = new OrganizationServiceEnMemoria();
            svc.Sembrar(new Entity(Tablas.Parametro) { ["statecode"] = new OptionSetValue(Tablas.Activo), ["sanic_nombre"] = "rpa.codigos", ["sanic_version"] = 1, ["sanic_valor"] = Codigos });
            var filaId = SembrarFila(svc, EstadoDeLaFila.Validada);

            Assert.Throws<InvalidOperationException>(() => Core(svc).Ejecutar(Lote(Item(filaId, "digitada")), ElBot, Ahora, 24, false));
        }

        [Fact]
        public void Sin_el_parametro_rpa_codigos_es_un_error_real()
        {
            var svc = new OrganizationServiceEnMemoria();
            svc.Sembrar(new Entity(Tablas.Parametro) { ["statecode"] = new OptionSetValue(Tablas.Activo), ["sanic_nombre"] = "rpa.lote.maximo", ["sanic_version"] = 1, ["sanic_valor"] = "50" });
            var filaId = SembrarFila(svc, EstadoDeLaFila.Validada);

            Assert.Throws<InvalidOperationException>(() => Core(svc).Ejecutar(Lote(Item(filaId, "digitada")), ElBot, Ahora, 24, false));
        }
    }
}
