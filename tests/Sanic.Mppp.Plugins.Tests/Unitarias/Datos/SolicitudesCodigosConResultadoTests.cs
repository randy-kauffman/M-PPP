using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
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

        [Fact]
        public void La_comparacion_no_distingue_mayusculas_como_la_clave_alternativa()
        {
            var svc = new OrganizationServiceEnMemoria();
            var id = Guid.NewGuid();
            svc.Create(Resultado(id, "es_correo_nuevo"));
            svc.Create(Resultado(id, "ES_CORREO_NUEVO"));

            var codigos = new SolicitudesDataverse(svc).CodigosConResultado(id, new[] { "Es_Correo_Nuevo" });

            Assert.Single(codigos);
            Assert.Equal("es_correo_nuevo", codigos.Single(), StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void Una_consulta_que_trae_MoreRecords_es_un_error_real()
        {
            var svc = new SiempreTruncado(new OrganizationServiceEnMemoria());
            Assert.Throws<InvalidOperationException>(() => new SolicitudesDataverse(svc).CodigosConResultado(Guid.NewGuid(), Pedidos));
        }

        private sealed class SiempreTruncado : IOrganizationService
        {
            private readonly IOrganizationService _real;

            public SiempreTruncado(IOrganizationService real) { _real = real; }

            public EntityCollection RetrieveMultiple(QueryBase query)
            {
                var r = _real.RetrieveMultiple(query);
                r.MoreRecords = true;
                return r;
            }

            public Guid Create(Entity entity) => _real.Create(entity);
            public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet) => _real.Retrieve(entityName, id, columnSet);
            public void Update(Entity entity) => _real.Update(entity);
            public void Delete(string entityName, Guid id) => _real.Delete(entityName, id);
            public OrganizationResponse Execute(OrganizationRequest request) => _real.Execute(request);
            public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) => _real.Associate(entityName, entityId, relationship, relatedEntities);
            public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) => _real.Disassociate(entityName, entityId, relationship, relatedEntities);
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
