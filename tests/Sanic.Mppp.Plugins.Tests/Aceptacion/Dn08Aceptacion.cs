using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Mppp.Plugins.Catalogos;
using Sanic.Mppp.Plugins.Datos;
using Sanic.Mppp.Plugins.Dominio;
using Sanic.Mppp.Plugins.Plantilla;
using Sanic.Mppp.Plugins.Steps;
using Sanic.Mppp.Plugins.Tests.Apoyo;
using Sanic.Mppp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Aceptacion
{
    /// <summary>
    /// DN-08 (diseno/DIRECTRICES-NUEVAS.md): un rol de MPPP heredado por un equipo (de propietario o de grupo de Entra) vale
    /// igual que uno asignado directo, en las cuatro consultas que preguntan "¿tiene el rol?". Pruebas del ARQUITECTO: el
    /// constructor no las cambia.
    ///
    /// Las tres tablas (`systemuserroles`, `teammembership`, `teamroles`) son intersect entities: en Dataverse real sus columnas
    /// llegan como `Guid` crudo. Se siembran así, salvo en la prueba que fija que también se acepta `EntityReference`.
    /// </summary>
    public class Dn08Aceptacion
    {
        private static readonly DateTime Ahora = new DateTime(2026, 11, 27, 18, 0, 0, DateTimeKind.Utc);

        // ================================================================================================ armado común

        private static Guid Rol(OrganizationServiceEnMemoria svc, string nombre) => svc.Sembrar(new Entity(TablasNativas.Rol) { ["name"] = nombre });

        private static Guid Usuario(OrganizationServiceEnMemoria svc, string nombre, bool deshabilitado = false, bool esAplicacion = false, string correo = null)
        {
            var u = new Entity(TablasNativas.Usuario) { ["fullname"] = nombre, ["isdisabled"] = deshabilitado };
            if (esAplicacion) u["applicationid"] = Guid.NewGuid();
            if (correo != null) u["internalemailaddress"] = correo;
            return svc.Sembrar(u);
        }

        private static void Directo(OrganizationServiceEnMemoria svc, Guid usuario, Guid rol)
            => svc.Sembrar(new Entity(TablasNativas.UsuarioRol) { ["systemuserid"] = usuario, ["roleid"] = rol });

        /// <summary>Un equipo con esos roles. El `team` se siembra para que el mundo se parezca al real; el código no lo necesita.</summary>
        private static Guid Equipo(OrganizationServiceEnMemoria svc, params Guid[] roles)
        {
            var equipo = svc.Sembrar(new Entity("team") { ["name"] = "Equipo " + Guid.NewGuid().ToString("N").Substring(0, 6) });
            foreach (var rol in roles) svc.Sembrar(new Entity(TablasNativas.RolDeEquipo) { ["teamid"] = equipo, ["roleid"] = rol });
            return equipo;
        }

        private static void Miembro(OrganizationServiceEnMemoria svc, Guid usuario, Guid equipo)
            => svc.Sembrar(new Entity(TablasNativas.MiembroDeEquipo) { ["systemuserid"] = usuario, ["teamid"] = equipo });

        private static int Consultas(OrganizationServiceEnMemoria svc) => svc.Llamadas.Count(l => l.Operacion == "RetrieveMultiple");

        private static int ConsultasA(OrganizationServiceEnMemoria svc, string tabla) =>
            svc.Llamadas.Count(l => l.Operacion == "RetrieveMultiple" && l.Entidad == tabla);

        // ================================================================================================ RolesDeMppp.IdsPorNombre

        [Fact]
        public void IdsPorNombre_trae_todos_los_ids_de_cada_nombre_uno_por_unidad_de_negocio()
        {
            var svc = new OrganizationServiceEnMemoria();
            var ejeRaiz = Rol(svc, TablasNativas.RolEjecutivo);
            var ejeOtraUnidad = Rol(svc, TablasNativas.RolEjecutivo);
            var sup = Rol(svc, TablasNativas.RolSupervisor);
            Rol(svc, "System Administrator");

            var ids = RolesDeMppp.IdsPorNombre(svc, new[] { TablasNativas.RolEjecutivo, TablasNativas.RolSupervisor });

            Assert.Equal(new[] { ejeRaiz, ejeOtraUnidad, sup }.OrderBy(g => g), ids.Keys.OrderBy(g => g));
            Assert.Equal(TablasNativas.RolEjecutivo, ids[ejeOtraUnidad]);
            Assert.Equal(TablasNativas.RolSupervisor, ids[sup]);
            Assert.Equal(1, Consultas(svc));
        }

        [Fact]
        public void IdsPorNombre_sin_ningun_rol_instalado_devuelve_vacio_y_no_lanza()
        {
            var svc = new OrganizationServiceEnMemoria();
            Rol(svc, "System Administrator");

            Assert.Empty(RolesDeMppp.IdsPorNombre(svc, new[] { TablasNativas.RolEjecutivo }));
        }

        // ================================================================================================ RolesDeMppp.RolesDelUsuario

        [Fact]
        public void RolesDelUsuario_cuenta_el_rol_asignado_directo()
        {
            var svc = new OrganizationServiceEnMemoria();
            var eje = Rol(svc, TablasNativas.RolEjecutivo);
            var ana = Usuario(svc, "Ana");
            Directo(svc, ana, eje);

            Assert.Equal(new[] { eje }, RolesDeMppp.RolesDelUsuario(svc, ana, new[] { eje }));
        }

        [Fact]
        public void RolesDelUsuario_cuenta_el_rol_heredado_por_un_equipo()
        {
            var svc = new OrganizationServiceEnMemoria();
            var eje = Rol(svc, TablasNativas.RolEjecutivo);
            var ana = Usuario(svc, "Ana");
            Miembro(svc, ana, Equipo(svc, eje));

            Assert.Equal(new[] { eje }, RolesDeMppp.RolesDelUsuario(svc, ana, new[] { eje }));
        }

        [Fact]
        public void RolesDelUsuario_no_cuenta_un_equipo_sin_el_rol_ni_el_rol_de_un_equipo_ajeno()
        {
            var svc = new OrganizationServiceEnMemoria();
            var eje = Rol(svc, TablasNativas.RolEjecutivo);
            var otro = Rol(svc, "System Customizer");
            var ana = Usuario(svc, "Ana");
            var beto = Usuario(svc, "Beto");
            Miembro(svc, ana, Equipo(svc, otro));       // Ana: en un equipo que NO tiene el rol
            Miembro(svc, beto, Equipo(svc, eje));       // el rol está en el equipo de Beto, no en el de Ana

            Assert.Empty(RolesDeMppp.RolesDelUsuario(svc, ana, new[] { eje }));
        }

        [Fact]
        public void RolesDelUsuario_solo_devuelve_roles_de_la_lista_pedida()
        {
            var svc = new OrganizationServiceEnMemoria();
            var eje = Rol(svc, TablasNativas.RolEjecutivo);
            var sup = Rol(svc, TablasNativas.RolSupervisor);
            var ana = Usuario(svc, "Ana");
            Miembro(svc, ana, Equipo(svc, sup));

            Assert.Empty(RolesDeMppp.RolesDelUsuario(svc, ana, new[] { eje }));
        }

        [Fact]
        public void RolesDelUsuario_une_directo_y_equipos_sin_repetidos()
        {
            var svc = new OrganizationServiceEnMemoria();
            var eje = Rol(svc, TablasNativas.RolEjecutivo);
            var sup = Rol(svc, TablasNativas.RolSupervisor);
            var ana = Usuario(svc, "Ana");
            Directo(svc, ana, eje);
            Miembro(svc, ana, Equipo(svc, eje));
            Miembro(svc, ana, Equipo(svc, eje, sup));

            var roles = RolesDeMppp.RolesDelUsuario(svc, ana, new[] { eje, sup });

            Assert.Equal(new[] { eje, sup }.OrderBy(g => g), roles.OrderBy(g => g));
        }

        [Fact]
        public void RolesDelUsuario_hace_un_numero_fijo_de_consultas_aunque_el_usuario_este_en_muchos_equipos()
        {
            var svc = new OrganizationServiceEnMemoria();
            var eje = Rol(svc, TablasNativas.RolEjecutivo);
            var ana = Usuario(svc, "Ana");
            for (var i = 0; i < 8; i++) Miembro(svc, ana, Equipo(svc, i == 7 ? eje : Rol(svc, "Otro " + i)));
            var antes = Consultas(svc);

            Assert.Single(RolesDeMppp.RolesDelUsuario(svc, ana, new[] { eje }));

            Assert.True(Consultas(svc) - antes <= 3, $"hizo {Consultas(svc) - antes} consultas; el contrato es a lo sumo 3");
            Assert.Equal(1, ConsultasA(svc, TablasNativas.RolDeEquipo));
        }

        [Fact]
        public void RolesDelUsuario_sin_equipos_no_consulta_los_roles_de_equipo()
        {
            var svc = new OrganizationServiceEnMemoria();
            var eje = Rol(svc, TablasNativas.RolEjecutivo);
            var ana = Usuario(svc, "Ana");
            Directo(svc, ana, eje);

            RolesDeMppp.RolesDelUsuario(svc, ana, new[] { eje });

            Assert.Equal(0, ConsultasA(svc, TablasNativas.RolDeEquipo));
        }

        [Fact]
        public void RolesDelUsuario_con_la_lista_de_roles_vacia_devuelve_vacio_sin_consultar()
        {
            var svc = new OrganizationServiceEnMemoria();
            var ana = Usuario(svc, "Ana");

            Assert.Empty(RolesDeMppp.RolesDelUsuario(svc, ana, new Guid[0]));
            Assert.Equal(0, Consultas(svc));
        }

        [Fact]
        public void RolesDelUsuario_acepta_las_columnas_como_EntityReference_ademas_de_Guid()
        {
            var svc = new OrganizationServiceEnMemoria();
            var eje = Rol(svc, TablasNativas.RolEjecutivo);
            var ana = Usuario(svc, "Ana");
            var equipo = svc.Sembrar(new Entity("team") { ["name"] = "Equipo" });
            svc.Sembrar(new Entity(TablasNativas.RolDeEquipo) { ["teamid"] = new EntityReference("team", equipo), ["roleid"] = new EntityReference(TablasNativas.Rol, eje) });
            svc.Sembrar(new Entity(TablasNativas.MiembroDeEquipo) { ["systemuserid"] = new EntityReference(TablasNativas.Usuario, ana), ["teamid"] = new EntityReference("team", equipo) });

            Assert.Equal(new[] { eje }, RolesDeMppp.RolesDelUsuario(svc, ana, new[] { eje }));
        }

        // ================================================================================================ RolesDeMppp.UsuariosConElRol

        [Fact]
        public void UsuariosConElRol_une_directos_y_miembros_de_equipos_con_el_rol_sin_repetidos()
        {
            var svc = new OrganizationServiceEnMemoria();
            var eje = Rol(svc, TablasNativas.RolEjecutivo);
            var otro = Rol(svc, "System Customizer");
            var ana = Usuario(svc, "Ana");        // directo y por dos equipos
            var beto = Usuario(svc, "Beto");      // solo por equipo
            var carla = Usuario(svc, "Carla");    // en un equipo sin el rol
            var dani = Usuario(svc, "Dani");      // nada
            Directo(svc, ana, eje);
            var equipoUno = Equipo(svc, eje);
            var equipoDos = Equipo(svc, eje, otro);
            Miembro(svc, ana, equipoUno);
            Miembro(svc, ana, equipoDos);
            Miembro(svc, beto, equipoDos);
            Miembro(svc, carla, Equipo(svc, otro));

            var usuarios = RolesDeMppp.UsuariosConElRol(svc, new[] { eje });

            Assert.Equal(new[] { ana, beto }.OrderBy(g => g), usuarios.OrderBy(g => g));
            Assert.DoesNotContain(carla, usuarios);
            Assert.DoesNotContain(dani, usuarios);
        }

        [Fact]
        public void UsuariosConElRol_con_filtro_solo_considera_esos_usuarios()
        {
            var svc = new OrganizationServiceEnMemoria();
            var eje = Rol(svc, TablasNativas.RolEjecutivo);
            var ana = Usuario(svc, "Ana");
            var beto = Usuario(svc, "Beto");
            var equipo = Equipo(svc, eje);
            Miembro(svc, ana, equipo);
            Miembro(svc, beto, equipo);

            Assert.Equal(new[] { beto }, RolesDeMppp.UsuariosConElRol(svc, new[] { eje }, new[] { beto }));
        }

        [Fact]
        public void UsuariosConElRol_con_filtro_vacio_o_sin_roles_devuelve_vacio_sin_consultar()
        {
            var svc = new OrganizationServiceEnMemoria();
            var eje = Rol(svc, TablasNativas.RolEjecutivo);
            Directo(svc, Usuario(svc, "Ana"), eje);
            var antes = Consultas(svc);

            Assert.Empty(RolesDeMppp.UsuariosConElRol(svc, new[] { eje }, new Guid[0]));
            Assert.Empty(RolesDeMppp.UsuariosConElRol(svc, new Guid[0]));
            Assert.Equal(antes, Consultas(svc));
        }

        [Fact]
        public void UsuariosConElRol_hace_un_numero_fijo_de_consultas_con_muchos_equipos_y_usuarios()
        {
            var svc = new OrganizationServiceEnMemoria();
            var eje = Rol(svc, TablasNativas.RolEjecutivo);
            for (var e = 0; e < 5; e++)
            {
                var equipo = Equipo(svc, eje);
                for (var u = 0; u < 4; u++) Miembro(svc, Usuario(svc, $"U{e}{u}"), equipo);
            }

            var antes = Consultas(svc);
            Assert.Equal(20, RolesDeMppp.UsuariosConElRol(svc, new[] { eje }).Count);
            Assert.True(Consultas(svc) - antes <= 3, $"hizo {Consultas(svc) - antes} consultas; el contrato es a lo sumo 3");
        }

        // ================================================================================================ paginación (revisión DN-08)

        /// <summary>
        /// Envuelve el doble y devuelve, para UNA tabla, los resultados en páginas de <c>tamano</c>, como Dataverse a partir de
        /// 5000: `MoreRecords` + `PagingCookie`, y respeta `PageInfo.PageNumber` del pedido. El doble no simula páginas, así
        /// que acá se le saca el `PageInfo` antes de delegar y se corta el resultado completo. La primera página es la que se
        /// pide SIN `PageInfo` (o con `PageNumber` 0 o 1).
        /// </summary>
        private sealed class PaginadoEn : IOrganizationService
        {
            private readonly OrganizationServiceEnMemoria _real;
            private readonly string _tabla;
            private readonly int _tamano;

            public PaginadoEn(OrganizationServiceEnMemoria real, string tabla, int tamano) { _real = real; _tabla = tabla; _tamano = tamano; }

            public List<int> PaginasPedidas { get; } = new List<int>();

            public EntityCollection RetrieveMultiple(QueryBase query)
            {
                var q = (QueryExpression)query;
                if (q.EntityName != _tabla) return _real.RetrieveMultiple(q);

                var pagina = Math.Max(1, q.PageInfo?.PageNumber ?? 1);
                if (pagina > 1) Assert.False(string.IsNullOrEmpty(q.PageInfo.PagingCookie), "la página siguiente se pide con la cookie de la anterior");
                PaginasPedidas.Add(pagina);

                var sinPaginas = new QueryExpression(q.EntityName) { ColumnSet = q.ColumnSet, Criteria = q.Criteria };
                var todo = _real.RetrieveMultiple(sinPaginas).Entities.ToList();
                var trozo = todo.Skip((pagina - 1) * _tamano).Take(_tamano).ToList();
                return new EntityCollection(trozo)
                {
                    EntityName = q.EntityName,
                    MoreRecords = pagina * _tamano < todo.Count,
                    PagingCookie = "<cookie page=\"" + pagina + "\" />",
                };
            }

            public Guid Create(Entity entity) => _real.Create(entity);
            public void Update(Entity entity) => _real.Update(entity);
            public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet) => _real.Retrieve(entityName, id, columnSet);
            public void Delete(string entityName, Guid id) => _real.Delete(entityName, id);
            public OrganizationResponse Execute(OrganizationRequest request) => _real.Execute(request);
            public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) => _real.Associate(entityName, entityId, relationship, relatedEntities);
            public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) => _real.Disassociate(entityName, entityId, relationship, relatedEntities);
        }

        [Theory]
        [InlineData("teammembership")]
        [InlineData("teamroles")]
        [InlineData("systemuserroles")]
        public void UsuariosConElRol_lee_todas_las_paginas(string tabla)
        {
            var svc = new OrganizationServiceEnMemoria();
            var eje = Rol(svc, TablasNativas.RolEjecutivo);
            var esperados = new List<Guid>();
            for (var e = 0; e < 3; e++)
            {
                var equipo = Equipo(svc, eje);
                for (var u = 0; u < 3; u++)
                {
                    var id = Usuario(svc, $"Equipo{e}-{u}");
                    Miembro(svc, id, equipo);
                    esperados.Add(id);
                }
            }

            for (var d = 0; d < 5; d++)
            {
                var id = Usuario(svc, $"Directo{d}");
                Directo(svc, id, eje);
                esperados.Add(id);
            }

            var paginado = new PaginadoEn(svc, tabla, tamano: 2);

            var usuarios = RolesDeMppp.UsuariosConElRol(paginado, new[] { eje });

            Assert.Equal(esperados.OrderBy(g => g), usuarios.OrderBy(g => g));
            Assert.True(paginado.PaginasPedidas.Count > 1, "con páginas de 2 tuvo que pedir más de una");
            Assert.Equal(Enumerable.Range(1, paginado.PaginasPedidas.Count), paginado.PaginasPedidas);
        }

        [Theory]
        [InlineData("teammembership")]
        [InlineData("teamroles")]
        public void RolesDelUsuario_lee_todas_las_paginas(string tabla)
        {
            var svc = new OrganizationServiceEnMemoria();
            var eje = Rol(svc, TablasNativas.RolEjecutivo);
            var ana = Usuario(svc, "Ana");
            // Ana está en 5 equipos; solo el ÚLTIMO tiene el rol (y los otros cuatro tienen otros roles pedidos que no son Ejecutivo).
            var otros = Enumerable.Range(0, 4).Select(i => Rol(svc, "Otro " + i)).ToList();
            foreach (var otro in otros) Miembro(svc, ana, Equipo(svc, otro));
            Miembro(svc, ana, Equipo(svc, eje));
            var pedidos = otros.Concat(new[] { eje }).ToList();

            var roles = RolesDeMppp.RolesDelUsuario(new PaginadoEn(svc, tabla, tamano: 1), ana, pedidos);

            Assert.Equal(pedidos.OrderBy(g => g), roles.OrderBy(g => g));
        }

        [Fact]
        public void IdsPorNombre_lee_todas_las_paginas()
        {
            var svc = new OrganizationServiceEnMemoria();
            var ids = Enumerable.Range(0, 5).Select(_ => Rol(svc, TablasNativas.RolEjecutivo)).ToList();

            var leidos = RolesDeMppp.IdsPorNombre(new PaginadoEn(svc, TablasNativas.Rol, tamano: 2), new[] { TablasNativas.RolEjecutivo });

            Assert.Equal(ids.OrderBy(g => g), leidos.Keys.OrderBy(g => g));
        }

        [Fact]
        public void El_aviso_llega_a_todos_los_miembros_aunque_el_equipo_ocupe_varias_paginas()
        {
            var svc = new OrganizationServiceEnMemoria();
            var eje = Rol(svc, TablasNativas.RolEjecutivo);
            var equipo = Equipo(svc, eje);
            var miembros = Enumerable.Range(0, 7).Select(i => Usuario(svc, "M" + i)).ToList();
            foreach (var m in miembros) Miembro(svc, m, equipo);

            new AvisosDataverse(new PaginadoEn(svc, TablasNativas.MiembroDeEquipo, tamano: 3))
                .AvisarAEjecutivos(Guid.NewGuid(), "Correo sin procesar", "Motivo: es una respuesta.");

            var duenos = svc.Registros(TablasNativas.Notificacion).Select(n => n.GetAttributeValue<EntityReference>("ownerid").Id);
            Assert.Equal(miembros.OrderBy(g => g), duenos.OrderBy(g => g));
        }

        [Fact]
        public void En_la_carga_masiva_los_roles_en_varias_paginas_no_fallan_y_la_foto_queda_completa()
        {
            // La consulta de asignaciones va filtrada por los usuarios de la carga: para que ocupe varias páginas, Ana tiene el
            // rol en tres unidades de negocio (tres ids con el mismo nombre) y páginas de 1.
            var sistema = new OrganizationServiceEnMemoria();
            var ana = Usuario(sistema, "Ana", correo: "ana@banco.com");
            for (var i = 0; i < 3; i++) Directo(sistema, ana, Rol(sistema, TablasNativas.RolEjecutivo));

            var filas = new[]
            {
                new FilaPlantilla { NumeroFilaExcel = 2, NumeroOrden = 1, Valores = new Dictionary<string, string>(StringComparer.Ordinal) { ["D"] = "ana@banco.com" } },
            };
            var foto = new FotoDeCatalogosDataverse(new OrganizationServiceEnMemoria(), new PaginadoEn(sistema, TablasNativas.UsuarioRol, tamano: 1))
                .ParaCarga(ClavesDeCarga.Extraer(filas));

            Assert.True(foto.Usuarios.Single(u => u.Id == ana).EsEjecutivo);
        }

        // ================================================================================================ transiciones de Fila

        /// <summary>Lo mínimo para correr <see cref="TransicionDeFilaStep"/> como en <c>StepsDeFilaAceptacion</c>, pero con
        /// usuarios que tienen los roles SOLO por equipo.</summary>
        private sealed class MundoDeFila
        {
            public readonly OrganizationServiceEnMemoria Svc = new OrganizationServiceEnMemoria();
            public readonly ContextoDePluginSimulado Ctx;
            public readonly Guid RolEjecutivo, RolSupervisor;
            public Guid SolicitudId, FilaId;

            public MundoDeFila(EstadoDeLaFila estado = EstadoDeLaFila.Validada, Guid? digitadaPor = null)
            {
                RolEjecutivo = Rol(Svc, TablasNativas.RolEjecutivo);
                RolSupervisor = Rol(Svc, TablasNativas.RolSupervisor);
                Rol(Svc, TablasNativas.RolRpa);
                Svc.Sembrar(new Entity(Tablas.Parametro) { ["statecode"] = new OptionSetValue(0), ["sanic_nombre"] = TransicionDeFilaStep.ParametroRpaPuedeAprobar, ["sanic_version"] = 1, ["sanic_valor"] = "no" });
                SolicitudId = Svc.Sembrar(new Entity(TablasHistorico.Solicitud)
                {
                    ["sanic_nombre"] = "MPPP-00000123", ["sanic_estadoprocesamiento"] = new OptionSetValue((int)EstadoDeLaSolicitud.EnProceso),
                    ["sanic_remitente"] = "ana@acme.com", ["sanic_fecharecibido"] = Ahora, ["sanic_messageid"] = "<m@x>",
                });
                var fila = new Entity(TablasHistorico.Fila)
                {
                    ["sanic_numerofila"] = 1, ["sanic_estado"] = new OptionSetValue((int)estado),
                    ["sanic_solicitudid"] = new EntityReference(TablasHistorico.Solicitud, SolicitudId),
                    ["sanic_numerocuenta"] = "123456789", ["sanic_nombrebeneficiario"] = "Juan Perez", ["sanic_numeroplan"] = "0042",
                };
                if (digitadaPor.HasValue) fila["sanic_digitadapor"] = new EntityReference(TablasNativas.Usuario, digitadaPor.Value);
                FilaId = Svc.Sembrar(fila);
                Ctx = new ContextoDePluginSimulado(Svc, Ahora);
                Ctx.Contexto.MessageName = "Update";
                Ctx.Contexto.PrimaryEntityName = TablasHistorico.Fila;
            }

            public Entity Correr(Guid quienLlama, EstadoDeLaFila hacia)
            {
                var actual = Svc.Retrieve(TablasHistorico.Fila, FilaId, new ColumnSet(true));
                var target = new Entity(TablasHistorico.Fila, FilaId) { ["sanic_estado"] = new OptionSetValue((int)hacia) };
                var pre = new Entity(TablasHistorico.Fila, FilaId)
                {
                    ["sanic_estado"] = actual["sanic_estado"],
                    ["sanic_solicitudid"] = new EntityReference(TablasHistorico.Solicitud, SolicitudId),
                    ["sanic_numerofila"] = actual["sanic_numerofila"],
                };
                var digitador = actual.GetAttributeValue<EntityReference>("sanic_digitadapor");
                if (digitador != null) pre["sanic_digitadapor"] = digitador;

                Ctx.Contexto.InitiatingUserId = quienLlama;
                Ctx.Contexto.UserId = quienLlama;
                Ctx.Contexto.InputParameters["Target"] = target;
                Ctx.Contexto.PreEntityImages["PreImage"] = pre;
                new TransicionDeFilaStep().Execute(Ctx);
                return target;
            }
        }

        [Fact]
        public void Un_ejecutivo_por_equipo_digita_una_fila_validada()
        {
            var m = new MundoDeFila();
            var ana = Usuario(m.Svc, "Ana");
            Miembro(m.Svc, ana, Equipo(m.Svc, m.RolEjecutivo));

            var target = m.Correr(ana, EstadoDeLaFila.Digitada);

            Assert.Equal(ana, target.GetAttributeValue<EntityReference>("sanic_digitadapor").Id);
        }

        [Fact]
        public void Un_miembro_de_un_equipo_sin_el_rol_no_digita()
        {
            var m = new MundoDeFila();
            var ana = Usuario(m.Svc, "Ana");
            Miembro(m.Svc, ana, Equipo(m.Svc, Rol(m.Svc, "System Customizer")));

            var ex = Assert.Throws<InvalidPluginExecutionException>(() => m.Correr(ana, EstadoDeLaFila.Digitada));

            Assert.Contains("El rol de quien llama", ex.Message);
        }

        [Fact]
        public void Un_supervisor_por_equipo_aprueba_lo_que_digito_otro()
        {
            var digitador = Guid.NewGuid();
            var m = new MundoDeFila(EstadoDeLaFila.Digitada, digitador);
            var sara = Usuario(m.Svc, "Sara");
            Miembro(m.Svc, sara, Equipo(m.Svc, m.RolSupervisor));

            var target = m.Correr(sara, EstadoDeLaFila.Aprobada);

            Assert.Equal(sara, target.GetAttributeValue<EntityReference>("sanic_aprobadapor").Id);
        }

        [Theory]
        [InlineData(true, false)]    // Ejecutivo directo, Supervisor por equipo
        [InlineData(false, true)]    // Supervisor directo, Ejecutivo por equipo
        [InlineData(false, false)]   // los dos por equipos distintos
        public void Ejecutivo_y_supervisor_juntando_directo_y_equipos_es_D25(bool ejecutivoDirecto, bool supervisorDirecto)
        {
            var m = new MundoDeFila();
            var ana = Usuario(m.Svc, "Ana");
            if (ejecutivoDirecto) Directo(m.Svc, ana, m.RolEjecutivo); else Miembro(m.Svc, ana, Equipo(m.Svc, m.RolEjecutivo));
            if (supervisorDirecto) Directo(m.Svc, ana, m.RolSupervisor); else Miembro(m.Svc, ana, Equipo(m.Svc, m.RolSupervisor));

            var ex = Assert.Throws<InvalidPluginExecutionException>(() => m.Correr(ana, EstadoDeLaFila.Digitada));

            Assert.Contains("D-25", ex.Message);
        }

        // ================================================================================================ avisos

        [Fact]
        public void El_aviso_a_ejecutivos_llega_a_los_del_equipo_una_sola_vez_y_solo_a_los_habilitados()
        {
            var svc = new OrganizationServiceEnMemoria();
            var eje = Rol(svc, TablasNativas.RolEjecutivo);
            var otro = Rol(svc, "System Customizer");
            var ana = Usuario(svc, "Ana");                         // directo y por equipo
            var beto = Usuario(svc, "Beto");                       // solo por dos equipos
            var carla = Usuario(svc, "Carla", deshabilitado: true); // por equipo, deshabilitada
            var dani = Usuario(svc, "Dani");                       // en un equipo sin el rol
            Directo(svc, ana, eje);
            var uno = Equipo(svc, eje);
            var dos = Equipo(svc, eje);
            Miembro(svc, ana, uno);
            Miembro(svc, beto, uno);
            Miembro(svc, beto, dos);
            Miembro(svc, carla, dos);
            Miembro(svc, dani, Equipo(svc, otro));
            var antes = Consultas(svc);

            new AvisosDataverse(svc).AvisarAEjecutivos(Guid.NewGuid(), "Correo sin procesar", "Motivo: es una respuesta.");

            var duenos = svc.Registros(TablasNativas.Notificacion).Select(n => n.GetAttributeValue<EntityReference>("ownerid").Id).ToList();
            Assert.Equal(new[] { ana, beto }.OrderBy(g => g), duenos.OrderBy(g => g));
            Assert.True(Consultas(svc) - antes <= 5, $"hizo {Consultas(svc) - antes} consultas; el contrato es a lo sumo 5");
        }

        [Fact]
        public void El_aviso_de_filas_por_aprobar_llega_al_supervisor_por_equipo()
        {
            var svc = new OrganizationServiceEnMemoria();
            var sup = Rol(svc, TablasNativas.RolSupervisor);
            Rol(svc, TablasNativas.RolEjecutivo);
            var sara = Usuario(svc, "Sara");
            Miembro(svc, sara, Equipo(svc, sup));

            new AvisosDataverse(svc).AvisarFilasPorAprobar(Guid.NewGuid(), "Filas por aprobar", "La solicitud tiene filas por aprobar.");

            var aviso = Assert.Single(svc.Registros(TablasNativas.Notificacion));
            Assert.Equal(sara, aviso.GetAttributeValue<EntityReference>("ownerid").Id);
        }

        // ================================================================================================ carga masiva (DN-04)

        [Fact]
        public void En_la_carga_masiva_un_ejecutivo_por_equipo_es_ejecutivo()
        {
            var sistema = new OrganizationServiceEnMemoria();
            var eje = Rol(sistema, TablasNativas.RolEjecutivo);
            var otro = Rol(sistema, TablasNativas.RolSupervisor);
            var ana = Usuario(sistema, "Ana", correo: "ana@banco.com");
            var beto = Usuario(sistema, "Beto", correo: "beto@banco.com");
            Miembro(sistema, ana, Equipo(sistema, eje));
            Miembro(sistema, beto, Equipo(sistema, otro));

            var filas = new[]
            {
                new FilaPlantilla { NumeroFilaExcel = 2, NumeroOrden = 1, Valores = new Dictionary<string, string>(StringComparer.Ordinal) { ["D"] = "ana@banco.com" } },
                new FilaPlantilla { NumeroFilaExcel = 3, NumeroOrden = 2, Valores = new Dictionary<string, string>(StringComparer.Ordinal) { ["D"] = "beto@banco.com" } },
            };
            var foto = new FotoDeCatalogosDataverse(new OrganizationServiceEnMemoria(), sistema).ParaCarga(ClavesDeCarga.Extraer(filas));

            Assert.True(foto.Usuarios.Single(u => u.Id == ana).EsEjecutivo);
            Assert.False(foto.Usuarios.Single(u => u.Id == beto).EsEjecutivo);
        }

        // ================================================================================================ puerta del RPA

        private static (IPluginExecutionContext ctx, OrganizationServiceEnMemoria svc, Guid usuario, Guid rol) MundoDelRpa(bool esAplicacion)
        {
            var svc = new OrganizationServiceEnMemoria();
            var ctx = new ContextoDePluginSimulado(svc, Ahora);
            var rol = Rol(svc, TablasNativas.RolRpa);
            var usuario = Usuario(svc, "Quien llama", esAplicacion: esAplicacion);
            ctx.Contexto.InitiatingUserId = usuario;
            return (ctx.Contexto, svc, usuario, rol);
        }

        [Fact]
        public void El_bot_con_el_rol_RPA_por_equipo_pasa_la_puerta()
        {
            var (ctx, svc, bot, rol) = MundoDelRpa(esAplicacion: true);
            Miembro(svc, bot, Equipo(svc, rol));

            PuertaDelRpa.Exigir(ctx, svc); // no lanza
        }

        [Fact]
        public void El_bot_con_el_rol_RPA_de_otra_unidad_de_negocio_pasa_la_puerta()
        {
            var (ctx, svc, bot, _) = MundoDelRpa(esAplicacion: true);
            var rolDeOtraUnidad = Rol(svc, TablasNativas.RolRpa);   // mismo nombre, otro id: el de otra unidad de negocio
            Directo(svc, bot, rolDeOtraUnidad);

            PuertaDelRpa.Exigir(ctx, svc); // no lanza
        }

        [Fact]
        public void Una_persona_en_un_equipo_con_el_rol_RPA_no_pasa_la_puerta()
        {
            var (ctx, svc, persona, rol) = MundoDelRpa(esAplicacion: false);
            Miembro(svc, persona, Equipo(svc, rol));

            var ex = Assert.Throws<InvalidPluginExecutionException>(() => PuertaDelRpa.Exigir(ctx, svc));

            Assert.Contains("MPPP-RPA-006", ex.Message);
        }

        [Fact]
        public void El_bot_en_un_equipo_sin_el_rol_RPA_no_pasa_la_puerta()
        {
            var (ctx, svc, bot, _) = MundoDelRpa(esAplicacion: true);
            Miembro(svc, bot, Equipo(svc, Rol(svc, "System Customizer")));

            var ex = Assert.Throws<InvalidPluginExecutionException>(() => PuertaDelRpa.Exigir(ctx, svc));

            Assert.Contains("MPPP-RPA-006", ex.Message);
        }
    }
}
