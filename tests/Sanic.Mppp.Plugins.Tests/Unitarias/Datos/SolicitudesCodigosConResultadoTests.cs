using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Mppp.Plugins.Datos;
using Sanic.Mppp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Unitarias.Datos
{
    /// <summary>
    /// `SolicitudesDataverse.CodigosConResultado` (D-77): el insumo de la idempotencia de ClasificarCorreo. Valida el id,
    /// filtra por Solicitud y devuelve solo los códigos de ESA Solicitud, sin repetidos.
    /// </summary>
    public class SolicitudesCodigosConResultadoTests
    {
        private static Entity Resultado(Guid solicitudId, string codigo)
            => new Entity(TablasHistorico.ResultadoRegla)
            {
                ["sanic_solicitudid"] = new EntityReference(TablasHistorico.Solicitud, solicitudId),
                ["sanic_reglacodigo"] = codigo,
            };

        private static readonly string[] Pedidos = { "A", "B" };

        [Fact]
        public void Un_id_vacio_se_rechaza_antes_de_tocar_el_servicio()
        {
            var svc = new OrganizationServiceEnMemoria();
            Assert.Throws<ArgumentException>(() => new SolicitudesDataverse(svc).CodigosConResultado(Guid.Empty, Pedidos));
        }

        [Fact]
        public void Codigos_nulos_se_rechazan()
        {
            var svc = new OrganizationServiceEnMemoria();
            Assert.Throws<ArgumentNullException>(() => new SolicitudesDataverse(svc).CodigosConResultado(Guid.NewGuid(), null));
        }

        [Fact]
        public void Sin_codigos_pedidos_devuelve_vacio_sin_llamar_al_servicio()
        {
            // Un servicio que lanza en cualquier llamada.
            Assert.Empty(new SolicitudesDataverse(new SinLlamadas()).CodigosConResultado(Guid.NewGuid(), new string[0]));
        }

        [Fact]
        public void Sin_resultados_guardados_devuelve_vacio()
        {
            var svc = new OrganizationServiceEnMemoria();
            Assert.Empty(new SolicitudesDataverse(svc).CodigosConResultado(Guid.NewGuid(), Pedidos));
        }

        [Fact]
        public void Devuelve_solo_los_codigos_pedidos_de_esa_solicitud_sin_repetidos()
        {
            var svc = new OrganizationServiceEnMemoria();
            var mia = Guid.NewGuid();
            var otra = Guid.NewGuid();
            svc.Create(Resultado(mia, "A"));
            svc.Create(Resultado(mia, "B"));
            svc.Create(Resultado(mia, "B"));
            svc.Create(Resultado(mia, "Z")); // de la misma solicitud, pero no se pidió
            svc.Create(Resultado(otra, "A"));

            var codigos = new SolicitudesDataverse(svc).CodigosConResultado(mia, Pedidos);

            Assert.Equal(new[] { "A", "B" }, codigos.OrderBy(c => c, StringComparer.Ordinal));
        }

        private sealed class SinLlamadas : IOrganizationService
        {
            public Guid Create(Entity entity) => throw new InvalidOperationException("no debía llamarse");
            public Entity Retrieve(string entityName, Guid id, Microsoft.Xrm.Sdk.Query.ColumnSet columnSet) => throw new InvalidOperationException("no debía llamarse");
            public void Update(Entity entity) => throw new InvalidOperationException("no debía llamarse");
            public void Delete(string entityName, Guid id) => throw new InvalidOperationException("no debía llamarse");
            public OrganizationResponse Execute(OrganizationRequest request) => throw new InvalidOperationException("no debía llamarse");
            public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) => throw new InvalidOperationException("no debía llamarse");
            public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) => throw new InvalidOperationException("no debía llamarse");
            public EntityCollection RetrieveMultiple(Microsoft.Xrm.Sdk.Query.QueryBase query) => throw new InvalidOperationException("no debía llamarse");
        }
    }
}
