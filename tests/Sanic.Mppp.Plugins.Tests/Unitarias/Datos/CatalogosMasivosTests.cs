using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Mppp.Plugins.Catalogos;
using Sanic.Mppp.Plugins.Datos;
using Sanic.Mppp.Plugins.Dominio;
using Sanic.Mppp.Plugins.Plantilla;
using Sanic.Mppp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Unitarias.Datos
{
    /// <summary>
    /// Comportamientos de la frontera con Dataverse de DN-04 que las pruebas de aceptación no fijan: forma de las consultas
    /// (nunca una condición In vacía, columnas acotadas, sin joins), una consulta de más por id para los dueños de planes,
    /// roles repetidos por unidad de negocio, y el contrato fino del escritor (qué se convierte, qué no, cuándo se mira el tiempo).
    /// </summary>
    public class CatalogosMasivosTests
    {
        private static readonly Guid Ana = new Guid("00000000-0000-0000-0000-0000000000a1");

        // ------------------------------------------------------------------------------------------ armado
        private static Entity E(string tabla, int estado, params (string atributo, object valor)[] atributos)
        {
            var e = new Entity(tabla) { ["statecode"] = new OptionSetValue(estado) };
            foreach (var (a, v) in atributos) e[a] = v;
            return e;
        }

        private static Guid Cliente(OrganizationServiceEnMemoria svc, string bac, string com, string nombre)
            => svc.Sembrar(E(Tablas.Cliente, 0, ("sanic_cifbac", bac), ("sanic_cifcom", com), ("sanic_nombre", nombre), ("ownerid", new EntityReference("systemuser", Ana))));

        private static Guid Plan(OrganizationServiceEnMemoria svc, string codigo, Guid cliente)
            => svc.Sembrar(E(Tablas.Plan, 0, ("sanic_codigo", codigo), ("sanic_clienteid", new EntityReference(Tablas.Cliente, cliente)),
                ("sanic_tipoformato", new OptionSetValue((int)TipoDeFormatoDelPlan._10)), ("sanic_moneda", new OptionSetValue((int)Moneda.USD))));

        private static FilaPlantilla Fila(int numero, params (string letra, string valor)[] valores)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (l, v) in valores) d[l] = v;
            return new FilaPlantilla { NumeroFilaExcel = numero, NumeroOrden = numero - 1, Valores = d };
        }

        private static OrganizationServiceEnMemoria SistemaVacio() => new OrganizationServiceEnMemoria();

        /// <summary>Delega en un servicio real y guarda lo que le pidieron, para afirmar sobre la FORMA de las consultas y de las escrituras.</summary>
        private sealed class ServicioEspia : IOrganizationService
        {
            private readonly IOrganizationService _real;

            public ServicioEspia(IOrganizationService real) { _real = real; }

            public List<QueryExpression> Consultas { get; } = new List<QueryExpression>();
            public List<Entity> Creadas { get; } = new List<Entity>();
            public List<Entity> Actualizadas { get; } = new List<Entity>();

            public EntityCollection RetrieveMultiple(QueryBase query)
            {
                Consultas.Add((QueryExpression)query);
                return _real.RetrieveMultiple(query);
            }

            public Guid Create(Entity entity) { Creadas.Add(entity); return _real.Create(entity); }
            public void Update(Entity entity) { Actualizadas.Add(entity); _real.Update(entity); }
            public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet) => _real.Retrieve(entityName, id, columnSet);
            public void Delete(string entityName, Guid id) => _real.Delete(entityName, id);
            public OrganizationResponse Execute(OrganizationRequest request) => _real.Execute(request);
            public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) => _real.Associate(entityName, entityId, relationship, relatedEntities);
            public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) => _real.Disassociate(entityName, entityId, relationship, relatedEntities);
        }

        private static IEnumerable<ConditionExpression> Condiciones(FilterExpression filtro)
        {
            foreach (var c in filtro.Conditions) yield return c;
            foreach (var f in filtro.Filters)
            {
                foreach (var c in Condiciones(f)) yield return c;
            }
        }

        // ------------------------------------------------------------------------------------------ forma de las consultas
        [Fact]
        public void Ninguna_consulta_manda_una_condicion_In_vacia_ni_pide_todas_las_columnas_ni_usa_joins_o_paginas()
        {
            var cat = new ServicioEspia(Sembrada());
            var sistema = new ServicioEspia(SistemaConEjecutiva());

            // Solo CIF BAC y un correo de ejecutivo: no hay CIF COM, códigos ni correos de autorizado.
            new FotoDeCatalogosDataverse(cat, sistema).ParaCarga(ClavesDeCarga.Extraer(new[] { Fila(2, ("A", "123"), ("D", "ana@banco.com")) }));
            new FotoDeCatalogosDataverse(cat, sistema).ParaCambioDeEstado(ClavesDeCambioDeEstado.Extraer(new[] { Fila(2, ("A", "CLIENTE"), ("B", "ACTIVAR"), ("C", "123")) }));

            var todas = cat.Consultas.Concat(sistema.Consultas).ToList();
            Assert.NotEmpty(todas);
            foreach (var q in todas)
            {
                Assert.False(q.ColumnSet.AllColumns, "consulta sobre " + q.EntityName + " con todas las columnas");
                Assert.Empty(q.LinkEntities);
                Assert.Null(q.PageInfo?.PagingCookie);
                foreach (var c in Condiciones(q.Criteria).Where(c => c.Operator == ConditionOperator.In))
                    Assert.NotEmpty(c.Values);
            }
        }

        [Fact]
        public void Con_solo_CIF_COM_la_consulta_de_clientes_no_nombra_el_CIF_BAC()
        {
            var cat = new ServicioEspia(Sembrada());
            new FotoDeCatalogosDataverse(cat, SistemaVacio()).ParaCarga(ClavesDeCarga.Extraer(new[] { Fila(2, ("B", "ABC123456001")) }));
            var q = cat.Consultas.Single(c => c.EntityName == Tablas.Cliente);
            Assert.DoesNotContain(Condiciones(q.Criteria), c => c.AttributeName == "sanic_cifbac");
        }

        private static OrganizationServiceEnMemoria Sembrada()
        {
            var cat = new OrganizationServiceEnMemoria();
            Cliente(cat, "000000123", "ABC123456001", "Sol");
            return cat;
        }

        private static OrganizationServiceEnMemoria SistemaConEjecutiva()
        {
            var s = new OrganizationServiceEnMemoria();
            var rol = s.Sembrar(new Entity("role") { ["name"] = "SR - MPPP - Ejecutivo" });
            s.Sembrar(new Entity("systemuser", Ana) { ["internalemailaddress"] = "ana@banco.com", ["isdisabled"] = false });
            s.Sembrar(new Entity("systemuserroles") { ["systemuserid"] = Ana, ["roleid"] = rol });
            return s;
        }

        // ------------------------------------------------------------------------------------------ dueños de los planes
        [Fact]
        public void El_dueno_de_un_plan_se_trae_con_UNA_consulta_mas_y_solo_si_falta()
        {
            var cat = new OrganizationServiceEnMemoria();
            var sol = Cliente(cat, "000000123", "ABC123456001", "Sol");
            var luna = Cliente(cat, "000000456", "XYZ987654002", "Luna");
            Plan(cat, "0A12", sol);
            Plan(cat, "0B34", luna);
            Plan(cat, "0B35", luna);

            // El plan de Sol: su dueño ya está (el archivo nombra a Sol). No hay consulta de más.
            var espia1 = new ServicioEspia(cat);
            new FotoDeCatalogosDataverse(espia1, SistemaVacio()).ParaCarga(ClavesDeCarga.Extraer(new[] { Fila(2, ("A", "123"), ("E", "0A12")) }));
            Assert.Single(espia1.Consultas, q => q.EntityName == Tablas.Cliente);

            // Dos planes de Luna, que el archivo no nombra: UNA consulta más (por id), no una por plan.
            var espia2 = new ServicioEspia(cat);
            var foto = new FotoDeCatalogosDataverse(espia2, SistemaVacio()).ParaCarga(ClavesDeCarga.Extraer(new[] { Fila(2, ("A", "123"), ("E", "0B34")), Fila(3, ("A", "123"), ("E", "0B35")) }));
            Assert.Equal(2, espia2.Consultas.Count(q => q.EntityName == Tablas.Cliente));
            Assert.Contains(foto.Clientes, c => c.Id == luna);
            Assert.Equal(2, foto.Planes.Count);
        }

        [Fact]
        public void Un_plan_cuyo_dueno_ya_no_existe_no_rompe_la_foto()
        {
            var cat = new OrganizationServiceEnMemoria();
            Plan(cat, "0B34", Guid.NewGuid());
            var foto = new FotoDeCatalogosDataverse(cat, SistemaVacio()).ParaCarga(ClavesDeCarga.Extraer(new[] { Fila(2, ("E", "0B34")) }));
            Assert.Single(foto.Planes);
            Assert.Empty(foto.Clientes);
        }

        [Fact]
        public void La_foto_del_cambio_de_estado_no_trae_los_duenos_de_los_planes()
        {
            var cat = new OrganizationServiceEnMemoria();
            var luna = Cliente(cat, "000000456", "XYZ987654002", "Luna");
            Plan(cat, "0B34", luna);
            var espia = new ServicioEspia(cat);
            var foto = new FotoDeCatalogosDataverse(espia, SistemaVacio()).ParaCambioDeEstado(ClavesDeCambioDeEstado.Extraer(new[] { Fila(2, ("A", "PLAN"), ("B", "ACTIVAR"), ("E", "0B34")) }));
            Assert.Single(foto.Planes);
            Assert.Empty(foto.Clientes);
            Assert.DoesNotContain(espia.Consultas, q => q.EntityName == Tablas.Cliente);
        }

        // ------------------------------------------------------------------------------------------ tabla incompleta: nombra cuál
        [Theory]
        [InlineData("sanic_mppp_tbl_plan")]
        [InlineData("sanic_mppp_tbl_autorizado")]
        [InlineData("sanic_mppp_tbl_autorizacioncliente")]
        public void Si_cualquier_consulta_de_catalogos_trae_mas_registros_falla_nombrando_la_tabla(string tabla)
        {
            var cat = new OrganizationServiceEnMemoria();
            var sol = Cliente(cat, "000000123", "ABC123456001", "Sol");
            Plan(cat, "0A12", sol);
            var juan = cat.Sembrar(E(Tablas.Autorizado, 0, ("sanic_nombre", "juan@empresa.com"), ("sanic_nombrecontacto", "Juan")));
            cat.Sembrar(E(Tablas.AutorizacionCliente, 0, ("sanic_autorizadoid", new EntityReference(Tablas.Autorizado, juan)), ("sanic_clienteid", new EntityReference(Tablas.Cliente, sol))));
            var truncado = new TruncadoEn(cat, tabla);

            var ex = Assert.Throws<InvalidOperationException>(() =>
                new FotoDeCatalogosDataverse(truncado, SistemaVacio()).ParaCarga(
                    ClavesDeCarga.Extraer(new[] { Fila(2, ("A", "123"), ("E", "0A12"), ("H", "juan@empresa.com")) })));
            Assert.Contains(tabla, ex.Message);
        }

        // DN-08: `role` y `systemuserroles` ya no se prueban acá. Las lee `RolesDeMppp`, que PAGINA (lee todas las páginas) en
        // vez de fallar: la foto queda completa igual, que es lo que esta prueba protegía. Lo fija `Dn08Aceptacion`
        // (`..._lee_todas_las_paginas`). `systemuser` sigue siendo de esta clase y sigue fallando.
        [Theory]
        [InlineData("systemuser")]
        public void Si_una_consulta_de_usuarios_o_roles_trae_mas_registros_falla_nombrando_la_tabla(string tabla)
        {
            var truncado = new TruncadoEn(SistemaConEjecutiva(), tabla);
            var ex = Assert.Throws<InvalidOperationException>(() =>
                new FotoDeCatalogosDataverse(new OrganizationServiceEnMemoria(), truncado).ParaCarga(ClavesDeCarga.Extraer(new[] { Fila(2, ("D", "ana@banco.com")) })));
            Assert.Contains(tabla, ex.Message);
        }

        [Fact]
        public void La_foto_del_cambio_de_estado_tambien_falla_si_le_falta_una_pagina()
        {
            var cat = new OrganizationServiceEnMemoria();
            var sol = Cliente(cat, "000000123", "ABC123456001", "Sol");
            var juan = cat.Sembrar(E(Tablas.Autorizado, 0, ("sanic_nombre", "juan@empresa.com"), ("sanic_nombrecontacto", "Juan")));
            cat.Sembrar(E(Tablas.AutorizacionCliente, 0, ("sanic_autorizadoid", new EntityReference(Tablas.Autorizado, juan)), ("sanic_clienteid", new EntityReference(Tablas.Cliente, sol))));
            var ex = Assert.Throws<InvalidOperationException>(() =>
                new FotoDeCatalogosDataverse(new TruncadoEn(cat, Tablas.AutorizacionCliente), SistemaVacio())
                    .ParaCambioDeEstado(ClavesDeCambioDeEstado.Extraer(new[] { Fila(2, ("A", "CLIENTE"), ("B", "DESACTIVAR"), ("C", "123")) })));
            Assert.Contains(Tablas.AutorizacionCliente, ex.Message);
        }

        // ------------------------------------------------------------------------------------------ usuarios y roles
        [Fact]
        public void El_mismo_rol_en_varias_unidades_de_negocio_cuenta_y_un_usuario_sin_roles_no_es_ejecutivo()
        {
            var s = new OrganizationServiceEnMemoria();
            var rolUno = s.Sembrar(new Entity("role") { ["name"] = "SR - MPPP - Ejecutivo" });
            var rolDos = s.Sembrar(new Entity("role") { ["name"] = "SR - MPPP - Ejecutivo" }); // otra unidad de negocio
            var enDos = Guid.NewGuid();
            var sinRoles = Guid.NewGuid();
            s.Sembrar(new Entity("systemuser", enDos) { ["internalemailaddress"] = "dos@banco.com", ["isdisabled"] = false });
            s.Sembrar(new Entity("systemuser", sinRoles) { ["internalemailaddress"] = "nada@banco.com", ["isdisabled"] = false });
            s.Sembrar(new Entity("systemuserroles") { ["systemuserid"] = enDos, ["roleid"] = rolDos });
            Assert.NotEqual(rolUno, rolDos);

            var foto = new FotoDeCatalogosDataverse(new OrganizationServiceEnMemoria(), s)
                .ParaCarga(ClavesDeCarga.Extraer(new[] { Fila(2, ("D", "dos@banco.com")), Fila(3, ("D", "nada@banco.com")) }));
            Assert.True(foto.Usuarios.Single(u => u.Id == enDos).EsEjecutivo);
            Assert.False(foto.Usuarios.Single(u => u.Id == sinRoles).EsEjecutivo);
        }

        [Fact]
        public void Si_el_rol_no_existe_aunque_ningun_usuario_coincida_es_un_error_de_configuracion_que_lo_nombra()
        {
            // Un rol que falta en el entorno no es "este usuario no es ejecutivo": es la causa de TODAS las filas, y no se esconde.
            var ex = Assert.Throws<InvalidOperationException>(() =>
                new FotoDeCatalogosDataverse(new OrganizationServiceEnMemoria(), new OrganizationServiceEnMemoria())
                    .ParaCarga(ClavesDeCarga.Extraer(new[] { Fila(2, ("D", "nadie@banco.com")) })));
            Assert.Contains("SR - MPPP - Ejecutivo", ex.Message);
        }

        [Fact]
        public void Sin_correos_de_ejecutivo_no_se_consulta_ni_el_rol()
        {
            var espia = new ServicioEspia(new OrganizationServiceEnMemoria());
            new FotoDeCatalogosDataverse(new OrganizationServiceEnMemoria(), espia).ParaCarga(ClavesDeCarga.Extraer(new[] { Fila(2, ("A", "123")) }));
            Assert.Empty(espia.Consultas);
        }

        [Fact]
        public void Con_el_rol_pero_sin_asignaciones_nadie_es_ejecutivo_y_no_falla()
        {
            var s = new OrganizationServiceEnMemoria();
            s.Sembrar(new Entity("role") { ["name"] = "SR - MPPP - Ejecutivo" });
            s.Sembrar(new Entity("systemuser", Ana) { ["internalemailaddress"] = "ana@banco.com", ["isdisabled"] = false });
            var foto = new FotoDeCatalogosDataverse(new OrganizationServiceEnMemoria(), s).ParaCarga(ClavesDeCarga.Extraer(new[] { Fila(2, ("D", "ana@banco.com")) }));
            Assert.False(Assert.Single(foto.Usuarios).EsEjecutivo);
        }

        [Fact]
        public void Un_usuario_con_isdisabled_nulo_tampoco_cuenta_como_habilitado()
        {
            var s = SistemaConEjecutiva();
            s.Sembrar(new Entity("systemuser", Guid.NewGuid()) { ["internalemailaddress"] = "raro@banco.com", ["isdisabled"] = null });
            var foto = new FotoDeCatalogosDataverse(new OrganizationServiceEnMemoria(), s).ParaCarga(ClavesDeCarga.Extraer(new[] { Fila(2, ("D", "raro@banco.com")) }));
            Assert.False(Assert.Single(foto.Usuarios).Habilitado);
        }

        // ------------------------------------------------------------------------------------------ columnas
        [Fact]
        public void Un_cliente_sin_dueno_es_un_error_que_nombra_tabla_y_columna()
        {
            var cat = new OrganizationServiceEnMemoria();
            cat.Sembrar(E(Tablas.Cliente, 0, ("sanic_cifbac", "000000123"), ("sanic_cifcom", "ABC123456001"), ("sanic_nombre", "Sol")));
            var ex = Assert.Throws<InvalidOperationException>(() =>
                new FotoDeCatalogosDataverse(cat, SistemaVacio()).ParaCarga(ClavesDeCarga.Extraer(new[] { Fila(2, ("A", "123")) })));
            Assert.Contains(Tablas.Cliente, ex.Message);
            Assert.Contains("ownerid", ex.Message);
        }

        [Fact]
        public void Un_correo_sin_nombre_de_contacto_se_lee_igual_porque_la_columna_se_agrego_despues()
        {
            // diseno/02 §2.3: sanic_nombrecontacto "agregada el 2026-09-24": los correos anteriores pueden no tenerla.
            var cat = new OrganizationServiceEnMemoria();
            cat.Sembrar(E(Tablas.Autorizado, 0, ("sanic_nombre", "viejo@empresa.com")));
            var foto = new FotoDeCatalogosDataverse(cat, SistemaVacio()).ParaCarga(ClavesDeCarga.Extraer(new[] { Fila(2, ("A", "1"), ("H", "viejo@empresa.com")) }));
            Assert.Equal("viejo@empresa.com", Assert.Single(foto.Autorizados).Correo);
        }

        [Fact]
        public void Un_registro_sin_estado_es_un_error_que_nombra_la_columna()
        {
            var cat = new OrganizationServiceEnMemoria();
            cat.Sembrar(new Entity(Tablas.Cliente) { ["sanic_cifbac"] = "000000123", ["sanic_cifcom"] = "ABC123456001", ["sanic_nombre"] = "Sol", ["ownerid"] = new EntityReference("systemuser", Ana) });
            var ex = Assert.Throws<InvalidOperationException>(() =>
                new FotoDeCatalogosDataverse(cat, SistemaVacio()).ParaCarga(ClavesDeCarga.Extraer(new[] { Fila(2, ("A", "123")) })));
            Assert.Contains("statecode", ex.Message);
        }

        // ------------------------------------------------------------------------------------------ escritor
        private static PlanDeCarga PlanUnClienteConAutorizacion(string fecha)
        {
            var foto = new EstadoDeCatalogos(new ClienteExistente[0], new PlanExistente[0], new AutorizadoExistente[0], new AutorizacionClienteExistente[0],
                new[] { new UsuarioCandidato(Ana, "ana@banco.com", true, true) });
            var plan = PlanificadorDeCarga.Planificar(new[]
            {
                Fila(2, ("A", "1"), ("B", "NUE000000001"), ("C", "Nueva"), ("D", "ana@banco.com"), ("H", "n@empresa.com"), ("I", "N"), ("J", fecha)),
            }, foto, new DateTime(2026, 10, 1));
            Assert.False(plan.TieneErrores, string.Join(" | ", plan.Filas.SelectMany(f => f.Errores)));
            return plan;
        }

        [Fact]
        public void La_fecha_se_escribe_tal_cual_sale_del_plan_sin_hora_y_sin_Kind()
        {
            var espia = new ServicioEspia(new OrganizationServiceEnMemoria());
            new EscritorDeCatalogos(espia, () => TimeSpan.Zero, TimeSpan.FromSeconds(100)).Aplicar(PlanUnClienteConAutorizacion("15/09/2024"));
            var aut = espia.Creadas.Single(e => e.LogicalName == Tablas.AutorizacionCliente);
            var fecha = (DateTime)aut["sanic_fechadocumento"];
            Assert.Equal(new DateTime(2024, 9, 15), fecha);
            Assert.Equal(DateTimeKind.Unspecified, fecha.Kind);
            Assert.False(aut.Contains("sanic_comentarios"));
        }

        [Fact]
        public void El_escritor_no_hace_ninguna_lectura()
        {
            var espia = new ServicioEspia(new OrganizationServiceEnMemoria());
            new EscritorDeCatalogos(espia, () => TimeSpan.Zero, TimeSpan.FromSeconds(100)).Aplicar(PlanUnClienteConAutorizacion("15/09/2024"));
            Assert.Empty(espia.Consultas);
            Assert.Equal(3, espia.Creadas.Count); // cliente, correo, autorización
        }

        [Fact]
        public void Un_fallo_sin_Detail_usa_el_mensaje_de_la_excepcion()
        {
            var servicio = new ServicioQueLanza(new FaultException<OrganizationServiceFault>(null, new FaultReason("clave duplicada")));
            var ex = Assert.Throws<EscrituraDeCatalogoFallidaException>(() =>
                new EscritorDeCatalogos(servicio, () => TimeSpan.Zero, TimeSpan.FromSeconds(100)).Aplicar(PlanUnClienteConAutorizacion("15/09/2024")));
            Assert.Contains("clave duplicada", ex.Motivo);
            Assert.Equal(2, ex.Fila);
        }

        [Fact]
        public void Una_excepcion_que_no_es_un_fallo_de_Dataverse_sube_tal_cual_y_sin_mas_llamadas()
        {
            var servicio = new ServicioQueLanza(new TimeoutException("se cortó la red"));
            Assert.Throws<TimeoutException>(() =>
                new EscritorDeCatalogos(servicio, () => TimeSpan.Zero, TimeSpan.FromSeconds(100)).Aplicar(PlanUnClienteConAutorizacion("15/09/2024")));
            Assert.Equal(1, servicio.Llamadas);
        }

        [Fact]
        public void El_presupuesto_se_mira_antes_de_la_primera_escritura_y_alcanzarlo_ya_es_agotarlo()
        {
            var svc = new OrganizationServiceEnMemoria();
            var ex = Assert.Throws<PresupuestoDeEscrituraAgotadoException>(() =>
                new EscritorDeCatalogos(svc, () => TimeSpan.FromSeconds(100), TimeSpan.FromSeconds(100)).Aplicar(PlanUnClienteConAutorizacion("15/09/2024")));
            Assert.Equal((0, 3), (ex.Escritas, ex.Total));
            Assert.Empty(svc.Llamadas);
        }

        [Fact]
        public void Un_plan_que_no_escribe_nada_no_mira_el_reloj_ni_falla_por_tiempo()
        {
            var foto = new EstadoDeCatalogos(new ClienteExistente[0], new PlanExistente[0], new AutorizadoExistente[0], new AutorizacionClienteExistente[0], new UsuarioCandidato[0]);
            var vacio = PlanificadorDeCarga.Planificar(new FilaPlantilla[0], foto, new DateTime(2026, 10, 1));
            var svc = new OrganizationServiceEnMemoria();
            new EscritorDeCatalogos(svc, () => TimeSpan.FromHours(1), TimeSpan.FromSeconds(1)).Aplicar(vacio);
            Assert.Empty(svc.Llamadas);
        }

        [Fact]
        public void El_cambio_de_estado_escribe_solo_estado_y_motivo_de_estado_y_respeta_el_presupuesto()
        {
            var cat = new OrganizationServiceEnMemoria();
            var sol = Cliente(cat, "000000123", "ABC123456001", "Sol");
            var luna = Cliente(cat, "000000456", "XYZ987654002", "Luna");
            var filas = new[]
            {
                Fila(2, ("A", "CLIENTE"), ("B", "DESACTIVAR"), ("C", "123")),
                Fila(3, ("A", "CLIENTE"), ("B", "DESACTIVAR"), ("C", "456")),
            };
            var plan = PlanificadorDeCambioDeEstado.Planificar(filas, new FotoDeCatalogosDataverse(cat, SistemaVacio()).ParaCambioDeEstado(ClavesDeCambioDeEstado.Extraer(filas)));

            var espia = new ServicioEspia(cat);
            new EscritorDeCatalogos(espia, () => TimeSpan.Zero, TimeSpan.FromSeconds(100)).Aplicar(plan);
            Assert.Equal(2, espia.Actualizadas.Count);
            Assert.All(espia.Actualizadas, u =>
            {
                Assert.Equal(new[] { "statecode", "statuscode" }, u.Attributes.Keys.OrderBy(k => k).ToArray());
                Assert.Equal(Tablas.Cliente, u.LogicalName);
            });
            Assert.Equal(new[] { sol, luna }, espia.Actualizadas.Select(u => u.Id).ToArray());

            var cat2 = new OrganizationServiceEnMemoria();
            var ex = Assert.Throws<PresupuestoDeEscrituraAgotadoException>(() =>
                new EscritorDeCatalogos(cat2, () => TimeSpan.FromSeconds(100), TimeSpan.FromSeconds(100)).Aplicar(plan));
            Assert.Equal((0, 2), (ex.Escritas, ex.Total));
        }

        [Fact]
        public void La_falla_viaja_como_InnerException_aunque_no_tenga_Detail()
        {
            var original = new FaultException<OrganizationServiceFault>(null, new FaultReason("clave duplicada"));
            var ex = Assert.Throws<EscrituraDeCatalogoFallidaException>(() =>
                new EscritorDeCatalogos(new ServicioQueLanza(original), () => TimeSpan.Zero, TimeSpan.FromSeconds(100)).Aplicar(PlanUnClienteConAutorizacion("15/09/2024")));
            Assert.Same(original, ex.InnerException);
        }

        [Fact]
        public void La_excepcion_con_causa_conserva_fila_motivo_y_mensaje_y_la_de_siempre_no_trae_causa()
        {
            var causa = new InvalidOperationException("x");
            var conCausa = new EscrituraDeCatalogoFallidaException(7, "duplicado", causa);
            Assert.Equal((7, "duplicado", "Fila 7: duplicado"), (conCausa.Fila, conCausa.Motivo, conCausa.Message));
            Assert.Same(causa, conCausa.InnerException);

            var sinCausa = new EscrituraDeCatalogoFallidaException(7, "duplicado");
            Assert.Equal("Fila 7: duplicado", sinCausa.Message);
            Assert.Null(sinCausa.InnerException);
        }

        [Theory]
        [InlineData(99, 1)]
        [InlineData(0, 1)]
        [InlineData(1, 99)]
        [InlineData(2, 0)]
        public void Un_tipo_o_una_accion_fuera_del_enum_falla_antes_de_escribir_nada(int tipo, int accion)
        {
            var validos = new CambioDeEstadoACrear(TipoDeCatalogo.Cliente, Guid.NewGuid(), AccionDeEstado.Desactivar, 2);
            var roto = new CambioDeEstadoACrear((TipoDeCatalogo)tipo, Guid.NewGuid(), (AccionDeEstado)accion, 3);
            // El roto va DESPUÉS de uno válido: si se validara sobre la marcha, el primero ya estaría escrito.
            var plan = new PlanDeCambioDeEstado(new List<ResultadoDeFilaDeCambio>(), false, new List<CambioDeEstadoACrear> { validos, roto }, 0);
            var svc = new OrganizationServiceEnMemoria();
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => new EscritorDeCatalogos(svc, () => TimeSpan.Zero, TimeSpan.FromSeconds(100)).Aplicar(plan));
            Assert.Empty(svc.Llamadas);
        }

        // ------------------------------------------------------------------------------------------ dobles
        private sealed class TruncadoEn : IOrganizationService
        {
            private readonly IOrganizationService _real;
            private readonly string _tabla;

            public TruncadoEn(IOrganizationService real, string tabla) { _real = real; _tabla = tabla; }

            public EntityCollection RetrieveMultiple(QueryBase query)
            {
                var r = _real.RetrieveMultiple(query);
                if (query is QueryExpression q && q.EntityName == _tabla) r.MoreRecords = true;
                return r;
            }

            public Guid Create(Entity entity) => _real.Create(entity);
            public void Update(Entity entity) => _real.Update(entity);
            public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet) => _real.Retrieve(entityName, id, columnSet);
            public void Delete(string entityName, Guid id) => _real.Delete(entityName, id);
            public OrganizationResponse Execute(OrganizationRequest request) => _real.Execute(request);
            public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) => _real.Associate(entityName, entityId, relationship, relatedEntities);
            public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) => _real.Disassociate(entityName, entityId, relationship, relatedEntities);
        }

        private sealed class ServicioQueLanza : IOrganizationService
        {
            private readonly Exception _excepcion;

            public ServicioQueLanza(Exception excepcion) { _excepcion = excepcion; }

            public int Llamadas { get; private set; }

            public Guid Create(Entity entity) { Llamadas++; throw _excepcion; }
            public void Update(Entity entity) { Llamadas++; throw _excepcion; }
            public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet) { Llamadas++; throw _excepcion; }
            public void Delete(string entityName, Guid id) { Llamadas++; throw _excepcion; }
            public OrganizationResponse Execute(OrganizationRequest request) { Llamadas++; throw _excepcion; }
            public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) { Llamadas++; throw _excepcion; }
            public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) { Llamadas++; throw _excepcion; }
            public EntityCollection RetrieveMultiple(QueryBase query) { Llamadas++; throw _excepcion; }
        }
    }
}
