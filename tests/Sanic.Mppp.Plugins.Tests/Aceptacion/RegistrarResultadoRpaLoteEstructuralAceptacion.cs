using System;
using System.Globalization;
using System.Linq;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Mppp.Plugins.Api;
using Sanic.Mppp.Plugins.Datos;
using Sanic.Mppp.Plugins.Dominio;
using Sanic.Mppp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Aceptacion
{
    /// <summary>
    /// Regresión del defecto BLOQUEANTE de 2026-09-25 contra el entorno real: un lote de 3 ítems (uno bueno, uno con
    /// `codigo` inexistente) tumbó LA LLAMADA ENTERA con HTTP 400 "ISV code reduced the open transaction count.
    /// Custom plug-ins should not catch exceptions from OrganizationService calls and continue processing."
    ///
    /// Causa (diseno/03-contratos-custom-api.md §3, "Se reporta por lote"): el diseño exige resultado POR ÍTEM,
    /// nunca todo-o-nada. Se implementó como "por cada ítem: escribir, y si revienta, atrapar y seguir" — pero
    /// cuando una llamada a `OrganizationService` falla DENTRO de la transacción del plugin, la plataforma YA
    /// decrementó el contador de transacción; atraparla y seguir corrompe el estado y voltea la llamada completa.
    ///
    /// Ninguna de las 967 pruebas existentes lo vio porque <see cref="OrganizationServiceEnMemoria"/> no simula el
    /// modelo transaccional de Dataverse (no decrementa ningún contador cuando una llamada suya falla y se atrapa).
    /// Por eso esta prueba es ESTRUCTURAL, sobre el invariante que el arreglo tiene que sostener, no una
    /// reproducción literal del HTTP 400:
    ///
    ///   con un lote de 3 ítems donde el del medio es inválido, el servicio NO recibe ni una escritura por ese
    ///   ítem, las escrituras de los otros dos SÍ se hacen, y la llamada devuelve 200 con el error por ítem
    ///   (nunca una excepción que tumbe el lote entero).
    ///
    /// Se cuentan las llamadas con <see cref="ServicioEspiaDeEscrituras"/> (un espía propio de este archivo, nunca
    /// se toca <see cref="OrganizationServiceEnMemoria"/> ni ninguna de las 967 pruebas existentes). Si el arreglo
    /// se revierte —si alguien reintroduce un `try/catch` alrededor de una llamada a `OrganizationService` que deja
    /// escribir de más, o que deja de escribir de menos—, la cuenta de escrituras cambia y esta prueba se pone roja.
    /// </summary>
    public class RegistrarResultadoRpaLoteEstructuralAceptacion
    {
        private static readonly DateTime Ahora = new DateTime(2026, 10, 5, 15, 0, 0, DateTimeKind.Utc);
        private static readonly Guid ElBot = Guid.NewGuid();

        private const string Codigos =
            "{\"OK\":{\"cliente\":null,\"nota\":\"AS400 acepto la operacion\"}," +
            "\"RECH-GEN\":{\"cliente\":\"Su operacion no pudo completarse.\",\"nota\":\"Rechazo generico\"}}";

        private static OrganizationServiceEnMemoria ArmarServicioBase(int loteMaximo = 50)
        {
            var svc = new OrganizationServiceEnMemoria();
            svc.Sembrar(new Entity(Tablas.Parametro) { ["statecode"] = new OptionSetValue(Tablas.Activo), ["sanic_nombre"] = "rpa.lote.maximo", ["sanic_version"] = 1, ["sanic_valor"] = loteMaximo.ToString(CultureInfo.InvariantCulture) });
            svc.Sembrar(new Entity(Tablas.Parametro) { ["statecode"] = new OptionSetValue(Tablas.Activo), ["sanic_nombre"] = "rpa.codigos", ["sanic_version"] = 1, ["sanic_valor"] = Codigos });
            return svc;
        }

        private static ServicioEspiaDeEscrituras ArmarServicioEspiado(int loteMaximo = 50) =>
            new ServicioEspiaDeEscrituras(ArmarServicioBase(loteMaximo));

        private static Guid SembrarFila(OrganizationServiceEnMemoria svc, EstadoDeLaFila estado, Guid? digitadaPor = null)
        {
            var e = new Entity(TablasHistorico.Fila)
            {
                ["sanic_estado"] = new OptionSetValue((int)estado),
                ["sanic_requiereatencionmanual"] = false,
            };
            if (digitadaPor.HasValue)
            {
                e["sanic_digitadapor"] = new EntityReference(TablasNativas.Usuario, digitadaPor.Value);
            }

            return svc.Sembrar(e);
        }

        private static RegistrarResultadoRpa Core(IOrganizationService svc) =>
            new RegistrarResultadoRpa(new FilasDataverse(svc), new CatalogosDataverse(svc));

        private static string Item(Guid filaId, string resultado, string codigo = "OK", string mensaje = null, DateTime? fecha = null) =>
            "{\"filaid\":\"" + filaId + "\",\"resultado\":\"" + resultado + "\"," +
            (mensaje != null ? "\"mensaje\":\"" + mensaje + "\"," : "") +
            "\"codigo\":\"" + codigo + "\"," +
            "\"fechaproceso\":\"" + (fecha ?? Ahora).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) + "\"}";

        private static string Lote(params string[] items) => "[" + string.Join(",", items) + "]";

        private static int EstadoDe(OrganizationServiceEnMemoria svc, Guid filaId) =>
            ((OptionSetValue)svc.Registros(TablasHistorico.Fila).Single(f => f.Id == filaId)["sanic_estado"]).Value;

        // ---------- el invariante, por cada motivo de invalidez que ninguna prueba anterior contaba escrituras ----------

        [Fact]
        public void Codigo_inexistente_en_el_item_del_medio_no_escribe_nada_y_el_resto_del_lote_se_aplica()
        {
            var svc = ArmarServicioEspiado();
            var buena1 = SembrarFila(svc.Interno, EstadoDeLaFila.Validada);
            var invalida = SembrarFila(svc.Interno, EstadoDeLaFila.Validada);
            var buena2 = SembrarFila(svc.Interno, EstadoDeLaFila.Validada);

            var resultado = Core(svc).Ejecutar(
                Lote(Item(buena1, "digitada"), Item(invalida, "digitada", codigo: "NO-MAPEADO"), Item(buena2, "digitada")),
                ElBot, Ahora, 24, false);

            Assert.Equal(2, resultado.Procesadas);
            Assert.Equal(1, resultado.ConError);
            Assert.Contains("MPPP-RPA-005", resultado.ResultadosJson);
            Assert.Equal(2, svc.Escrituras); // exactamente las de buena1 y buena2: CERO para el ítem inválido
            Assert.Equal((int)EstadoDeLaFila.Digitada, EstadoDe(svc.Interno, buena1));
            Assert.Equal((int)EstadoDeLaFila.Digitada, EstadoDe(svc.Interno, buena2));
            Assert.Equal((int)EstadoDeLaFila.Validada, EstadoDe(svc.Interno, invalida));
        }

        [Fact]
        public void Fecha_sin_offset_en_el_item_del_medio_no_escribe_nada_y_el_resto_del_lote_se_aplica()
        {
            var svc = ArmarServicioEspiado();
            var buena1 = SembrarFila(svc.Interno, EstadoDeLaFila.Validada);
            var invalida = SembrarFila(svc.Interno, EstadoDeLaFila.Validada);
            var buena2 = SembrarFila(svc.Interno, EstadoDeLaFila.Validada);
            var itemInvalido = "{\"filaid\":\"" + invalida + "\",\"resultado\":\"digitada\",\"codigo\":\"OK\",\"fechaproceso\":\"2026-10-05T15:00:00\"}";

            var resultado = Core(svc).Ejecutar(
                Lote(Item(buena1, "digitada"), itemInvalido, Item(buena2, "digitada")),
                ElBot, Ahora, 24, false);

            Assert.Equal(2, resultado.Procesadas);
            Assert.Equal(1, resultado.ConError);
            Assert.Contains(ReglasDelRpa.CodigoFecha, resultado.ResultadosJson);
            Assert.Equal(2, svc.Escrituras);
            Assert.Equal((int)EstadoDeLaFila.Digitada, EstadoDe(svc.Interno, buena1));
            Assert.Equal((int)EstadoDeLaFila.Digitada, EstadoDe(svc.Interno, buena2));
            Assert.Equal((int)EstadoDeLaFila.Validada, EstadoDe(svc.Interno, invalida));
        }

        [Fact]
        public void Fecha_fuera_de_tolerancia_en_el_item_del_medio_no_escribe_nada_y_el_resto_del_lote_se_aplica()
        {
            var svc = ArmarServicioEspiado();
            var buena1 = SembrarFila(svc.Interno, EstadoDeLaFila.Validada);
            var invalida = SembrarFila(svc.Interno, EstadoDeLaFila.Validada);
            var buena2 = SembrarFila(svc.Interno, EstadoDeLaFila.Validada);

            var resultado = Core(svc).Ejecutar(
                Lote(Item(buena1, "digitada"), Item(invalida, "digitada", fecha: Ahora.AddHours(-48)), Item(buena2, "digitada")),
                ElBot, Ahora, 24, false);

            Assert.Equal(2, resultado.Procesadas);
            Assert.Equal(1, resultado.ConError);
            Assert.Contains(ReglasDelRpa.CodigoFecha, resultado.ResultadosJson);
            Assert.Equal(2, svc.Escrituras);
            Assert.Equal((int)EstadoDeLaFila.Digitada, EstadoDe(svc.Interno, buena1));
            Assert.Equal((int)EstadoDeLaFila.Digitada, EstadoDe(svc.Interno, buena2));
            Assert.Equal((int)EstadoDeLaFila.Validada, EstadoDe(svc.Interno, invalida));
        }

        [Fact]
        public void Resultado_desconocido_en_el_item_del_medio_no_escribe_nada_y_el_resto_del_lote_se_aplica()
        {
            var svc = ArmarServicioEspiado();
            var buena1 = SembrarFila(svc.Interno, EstadoDeLaFila.Validada);
            var invalida = SembrarFila(svc.Interno, EstadoDeLaFila.Validada);
            var buena2 = SembrarFila(svc.Interno, EstadoDeLaFila.Validada);

            var resultado = Core(svc).Ejecutar(
                Lote(Item(buena1, "digitada"), Item(invalida, "digitando"), Item(buena2, "digitada")),
                ElBot, Ahora, 24, false);

            Assert.Equal(2, resultado.Procesadas);
            Assert.Equal(1, resultado.ConError);
            Assert.Contains("MPPP-RPA-002", resultado.ResultadosJson);
            Assert.Equal(2, svc.Escrituras);
            Assert.Equal((int)EstadoDeLaFila.Digitada, EstadoDe(svc.Interno, buena1));
            Assert.Equal((int)EstadoDeLaFila.Digitada, EstadoDe(svc.Interno, buena2));
            Assert.Equal((int)EstadoDeLaFila.Validada, EstadoDe(svc.Interno, invalida));
        }

        [Fact]
        public void Transicion_ilegal_en_el_item_del_medio_no_escribe_nada_y_el_resto_del_lote_se_aplica()
        {
            var svc = ArmarServicioEspiado();
            var buena1 = SembrarFila(svc.Interno, EstadoDeLaFila.Validada);
            var otroUsuario = Guid.NewGuid();
            var invalida = SembrarFila(svc.Interno, EstadoDeLaFila.Digitada, digitadaPor: otroUsuario);
            var buena2 = SembrarFila(svc.Interno, EstadoDeLaFila.Validada);

            // El bot pide aprobar lo que digitó OTRO (persona u otro bot): TransicionesDeFila la rechaza por
            // segregación de funciones, sin código MPPP-RPA-0xx propio (el contrato no le asigna uno).
            var resultado = Core(svc).Ejecutar(
                Lote(Item(buena1, "digitada"), Item(invalida, "aprobada"), Item(buena2, "digitada")),
                ElBot, Ahora, 24, rpaPuedeAprobar: true);

            Assert.Equal(2, resultado.Procesadas);
            Assert.Equal(1, resultado.ConError);
            Assert.Equal(2, svc.Escrituras);
            Assert.Equal((int)EstadoDeLaFila.Digitada, EstadoDe(svc.Interno, buena1));
            Assert.Equal((int)EstadoDeLaFila.Digitada, EstadoDe(svc.Interno, buena2));
            Assert.Equal((int)EstadoDeLaFila.Digitada, EstadoDe(svc.Interno, invalida)); // sigue igual: NO pasó a Aprobada
        }

        /// <summary>
        /// LA prueba que ninguna de las 967 anteriores podía escribir: un `filaid` que no existe se resuelve HOY
        /// con un `Retrieve` por id (<see cref="FilasDataverse.LeerParaRegistro"/>), que en Dataverse real (y en
        /// este doble: contrato de <see cref="OrganizationServiceEnMemoria"/>, "un registro que no existe es
        /// FaultException, como en Dataverse") TIRA una excepción — y hoy el plugin la atrapa
        /// (<c>RegistrarResultadoRpa.LeerFilaActual</c>) para seguir con el resto del lote. ESE catch alrededor de
        /// una llamada a `OrganizationService` es exactamente el patrón que el defecto de 2026-09-25 prohíbe.
        ///
        /// El arreglo tiene que resolver la existencia con una LECTURA que devuelve vacío (`RetrieveMultiple`), no
        /// con una excepción atrapada. Por eso esta prueba no se conforma con contar escrituras: además afirma que
        /// NINGÚN `Retrieve` del servicio real haya fallado durante el procesamiento del lote.
        /// </summary>
        [Fact]
        public void Filaid_que_no_existe_en_el_item_del_medio_no_dispara_ni_atrapa_una_falla_del_servicio_y_no_escribe_nada()
        {
            var svc = ArmarServicioEspiado();
            var buena1 = SembrarFila(svc.Interno, EstadoDeLaFila.Validada);
            var inexistente = Guid.NewGuid(); // nunca sembrada: no existe en el servicio
            var buena2 = SembrarFila(svc.Interno, EstadoDeLaFila.Validada);

            var resultado = Core(svc).Ejecutar(
                Lote(Item(buena1, "digitada"), Item(inexistente, "digitada"), Item(buena2, "digitada")),
                ElBot, Ahora, 24, false);

            Assert.Equal(2, resultado.Procesadas);
            Assert.Equal(1, resultado.ConError);
            Assert.Contains("MPPP-RPA-001", resultado.ResultadosJson);
            Assert.Equal(2, svc.Escrituras);
            Assert.Equal((int)EstadoDeLaFila.Digitada, EstadoDe(svc.Interno, buena1));
            Assert.Equal((int)EstadoDeLaFila.Digitada, EstadoDe(svc.Interno, buena2));

            // El invariante nuevo, y la lección del día: nunca un try/catch alrededor de una llamada a
            // OrganizationService. Hoy este assert está ROJO porque LeerParaRegistro usa Retrieve-por-id, que
            // FALLA para un filaid inexistente y esa falla se atrapa adentro del plugin.
            Assert.Equal(0, svc.RetrievesQueFallaron);
        }
    }

    /// <summary>
    /// Espía de <see cref="IOrganizationService"/> propio de este archivo (no se toca
    /// <see cref="OrganizationServiceEnMemoria"/>, que ya tiene su propio contrato y sus propias pruebas): cuenta
    /// las escrituras reales que le llegaron (`Create`/`Update`/`Delete`) y si algún `Retrieve` llegó a FALLAR — la
    /// única forma de comprobar, desde AFUERA del plugin, que nunca se atrapó una excepción de una llamada real a
    /// `OrganizationService` (diseno/03 §3; defecto real de 2026-09-25, "ISV code reduced the open transaction
    /// count").
    /// </summary>
    internal sealed class ServicioEspiaDeEscrituras : IOrganizationService
    {
        internal ServicioEspiaDeEscrituras(OrganizationServiceEnMemoria interno)
        {
            Interno = interno ?? throw new ArgumentNullException(nameof(interno));
        }

        internal OrganizationServiceEnMemoria Interno { get; }

        /// <summary>Cuántas llamadas de escritura (`Create`/`Update`/`Delete`) recibió el servicio real.</summary>
        public int Escrituras { get; private set; }

        /// <summary>Cuántas veces un `Retrieve` del servicio real terminó en `FaultException` (nunca tendría que pasar de acá para afuera sin que el plugin lo evite).</summary>
        public int RetrievesQueFallaron { get; private set; }

        public Guid Create(Entity entity)
        {
            Escrituras++;
            return Interno.Create(entity);
        }

        public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
        {
            try
            {
                return Interno.Retrieve(entityName, id, columnSet);
            }
            catch (FaultException<OrganizationServiceFault>)
            {
                RetrievesQueFallaron++;
                throw;
            }
        }

        public void Update(Entity entity)
        {
            Escrituras++;
            Interno.Update(entity);
        }

        public void Delete(string entityName, Guid id)
        {
            Escrituras++;
            Interno.Delete(entityName, id);
        }

        public OrganizationResponse Execute(OrganizationRequest request) => Interno.Execute(request);

        public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) =>
            Interno.Associate(entityName, entityId, relationship, relatedEntities);

        public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) =>
            Interno.Disassociate(entityName, entityId, relationship, relatedEntities);

        public EntityCollection RetrieveMultiple(QueryBase query) => Interno.RetrieveMultiple(query);
    }
}
