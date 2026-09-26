using System;
using System.Globalization;
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
    /// La otra mitad del invariante del defecto BLOQUEANTE de 2026-09-25 (ver también
    /// <see cref="RegistrarResultadoRpaLoteEstructuralAceptacion"/>): esa clase prueba que un DATO MALO del bot
    /// (filaid inexistente, código no mapeado, fecha corrida, transición ilegal...) nunca tumba el lote — se
    /// responde por ítem. Esta prueba cubre la asimetría OPUESTA, la que el `catch (Exception)` genérico de
    /// `RegistrarResultadoRpa.ProcesarItem` tapaba: cuando la escritura misma (`RegistrarDigitacion` y compañía)
    /// falla porque la PLATAFORMA (AS400 vía Dataverse) no responde, eso NO es un dato malo del bot — es una
    /// CONDICIÓN EXCEPCIONAL, y la excepción tiene que propagarse y tumbar LA LLAMADA ENTERA. Taparla convirtiéndola
    /// en un error por ítem dejaría a Dataverse mintiendo sobre qué pasó realmente en AS400.
    ///
    /// Escrita en rojo ANTES del arreglo (2026-09-25): hoy `ProcesarItem` atrapa cualquier `Exception` que no sea
    /// `ResultadoRpaException` y devuelve "Error interno al procesar este ítem", así que `Ejecutar` NO tira y esta
    /// prueba falla. El arreglo saca ese `catch (Exception)` entero (no lo afina): una vez afuera, cualquier falla
    /// real de `OrganizationService` durante una escritura sube tal cual.
    /// </summary>
    public class RegistrarResultadoRpaPropagacionAceptacion
    {
        private static readonly DateTime Ahora = new DateTime(2026, 10, 5, 15, 0, 0, DateTimeKind.Utc);
        private static readonly Guid ElBot = Guid.NewGuid();

        private const string Codigos = "{\"OK\":{\"cliente\":null,\"nota\":\"AS400 acepto la operacion\"}}";

        private static OrganizationServiceEnMemoria ArmarServicioBase()
        {
            var svc = new OrganizationServiceEnMemoria();
            svc.Sembrar(new Entity(Tablas.Parametro) { ["statecode"] = new OptionSetValue(Tablas.Activo), ["sanic_nombre"] = "rpa.lote.maximo", ["sanic_version"] = 1, ["sanic_valor"] = "50" });
            svc.Sembrar(new Entity(Tablas.Parametro) { ["statecode"] = new OptionSetValue(Tablas.Activo), ["sanic_nombre"] = "rpa.codigos", ["sanic_version"] = 1, ["sanic_valor"] = Codigos });
            return svc;
        }

        private static Guid SembrarFila(OrganizationServiceEnMemoria svc, EstadoDeLaFila estado)
        {
            var e = new Entity(TablasHistorico.Fila)
            {
                ["sanic_estado"] = new OptionSetValue((int)estado),
                ["sanic_requiereatencionmanual"] = false,
            };
            return svc.Sembrar(e);
        }

        private static RegistrarResultadoRpa Core(IOrganizationService svc) =>
            new RegistrarResultadoRpa(new FilasDataverse(svc), new CatalogosDataverse(svc));

        private static string Item(Guid filaId, string resultado) =>
            "{\"filaid\":\"" + filaId + "\",\"resultado\":\"" + resultado + "\",\"codigo\":\"OK\"," +
            "\"fechaproceso\":\"" + Ahora.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) + "\"}";

        private static string Lote(params string[] items) => "[" + string.Join(",", items) + "]";

        [Fact]
        public void Escritura_que_falla_por_plataforma_tumba_la_llamada_entera_no_se_convierte_en_error_por_item()
        {
            var basico = ArmarServicioBase();
            var filaId = SembrarFila(basico, EstadoDeLaFila.Validada);
            var svc = new ServicioQueFallaAlEscribir(basico);

            // El invariante: una falla de OrganizationService durante una escritura SUBE tal cual. Nunca
            // "Error interno al procesar este ítem" ni un `resultado.ConError` — eso sería el mismo defecto
            // de 2026-09-25 disfrazado de "más robusto".
            Assert.Throws<InvalidOperationException>(() => Core(svc).Ejecutar(
                Lote(Item(filaId, "digitada")), ElBot, Ahora, 24, false));
        }
    }

    /// <summary>
    /// Doble propio de este archivo (mismo criterio que <see cref="ServicioEspiaDeEscrituras"/> en
    /// <see cref="RegistrarResultadoRpaLoteEstructuralAceptacion"/>: nunca se toca <see cref="OrganizationServiceEnMemoria"/>):
    /// simula que AS400/la plataforma rechaza CUALQUIER escritura, para comprobar desde AFUERA del plugin que esa
    /// falla se propaga en vez de atraparse y disfrazarse de error por ítem.
    /// </summary>
    internal sealed class ServicioQueFallaAlEscribir : IOrganizationService
    {
        private readonly OrganizationServiceEnMemoria _interno;

        internal ServicioQueFallaAlEscribir(OrganizationServiceEnMemoria interno)
        {
            _interno = interno ?? throw new ArgumentNullException(nameof(interno));
        }

        public Guid Create(Entity entity) => _interno.Create(entity);

        public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet) => _interno.Retrieve(entityName, id, columnSet);

        public void Update(Entity entity) =>
            throw new InvalidOperationException(
                "AS400 no respondió (simulado): una falla de escritura de plataforma tiene que tumbar la llamada entera, nunca atraparse por ítem.");

        public void Delete(string entityName, Guid id) => _interno.Delete(entityName, id);

        public OrganizationResponse Execute(OrganizationRequest request) => _interno.Execute(request);

        public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) =>
            _interno.Associate(entityName, entityId, relationship, relatedEntities);

        public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) =>
            _interno.Disassociate(entityName, entityId, relationship, relatedEntities);

        public EntityCollection RetrieveMultiple(QueryBase query) => _interno.RetrieveMultiple(query);
    }
}
