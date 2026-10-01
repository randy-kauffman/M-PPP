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

namespace Sanic.Mppp.Plugins.Tests.Aceptacion
{
    /// <summary>
    /// DN-04, frontera con Dataverse de la carga y el cambio de estado masivos (diseno/03 §1c/§1d): la foto (pasada 1) y el
    /// escritor (pasada 2, regla del spike D-72). Pruebas del ARQUITECTO: el constructor no las cambia.
    /// </summary>
    public class CatalogosMasivosAceptacion
    {
        private const int Inactivo = 1;
        private static readonly Guid Ana = new Guid("00000000-0000-0000-0000-0000000000a1");
        private static readonly Guid Beto = new Guid("00000000-0000-0000-0000-0000000000b2");

        // ================================================================================================ armado
        private static Entity E(string tabla, int estado, params (string atributo, object valor)[] atributos)
        {
            var e = new Entity(tabla) { ["statecode"] = new OptionSetValue(estado) };
            foreach (var (a, v) in atributos) e[a] = v;
            return e;
        }

        private static Guid Cliente(OrganizationServiceEnMemoria svc, string bac, string com, string nombre, int estado = Tablas.Activo)
            => svc.Sembrar(E(Tablas.Cliente, estado, ("sanic_cifbac", bac), ("sanic_cifcom", com), ("sanic_nombre", nombre), ("ownerid", new EntityReference("systemuser", Ana))));

        private static Guid Plan(OrganizationServiceEnMemoria svc, string codigo, Guid cliente, int estado = Tablas.Activo)
            => svc.Sembrar(E(Tablas.Plan, estado, ("sanic_codigo", codigo), ("sanic_clienteid", new EntityReference(Tablas.Cliente, cliente)),
                ("sanic_tipoformato", new OptionSetValue((int)TipoDeFormatoDelPlan._10)), ("sanic_moneda", new OptionSetValue((int)Moneda.USD))));

        private static Guid Autorizado(OrganizationServiceEnMemoria svc, string correo, string nombre, int estado = Tablas.Activo)
            => svc.Sembrar(E(Tablas.Autorizado, estado, ("sanic_nombre", correo), ("sanic_nombrecontacto", nombre)));

        private static Guid Autorizacion(OrganizationServiceEnMemoria svc, Guid autorizado, Guid cliente, int estado = Tablas.Activo, DateTime? fecha = null, string comentarios = null)
            => svc.Sembrar(E(Tablas.AutorizacionCliente, estado, ("sanic_autorizadoid", new EntityReference(Tablas.Autorizado, autorizado)),
                ("sanic_clienteid", new EntityReference(Tablas.Cliente, cliente)), ("sanic_fechadocumento", fecha), ("sanic_comentarios", comentarios)));

        private static FilaPlantilla Fila(int numero, params (string letra, string valor)[] valores)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (l, v) in valores) d[l] = v;
            return new FilaPlantilla { NumeroFilaExcel = numero, NumeroOrden = numero - 1, Valores = d };
        }

        /// <summary>Usuarios y roles, en el servicio SYSTEM: Ana ejecutiva, Beto con otro rol, Carla deshabilitada y ejecutiva.</summary>
        private static OrganizationServiceEnMemoria Sistema()
        {
            var svc = new OrganizationServiceEnMemoria();
            var rolEjecutivo = svc.Sembrar(new Entity("role") { ["name"] = "SR - MPPP - Ejecutivo" });
            var rolOtro = svc.Sembrar(new Entity("role") { ["name"] = "SR - MPPP - Supervisor" });
            svc.Sembrar(new Entity("systemuser", Ana) { ["internalemailaddress"] = "ana@banco.com", ["isdisabled"] = false });
            svc.Sembrar(new Entity("systemuser", Beto) { ["internalemailaddress"] = "beto@banco.com", ["isdisabled"] = false });
            var carla = svc.Sembrar(new Entity("systemuser") { ["internalemailaddress"] = "carla@banco.com", ["isdisabled"] = true });
            svc.Sembrar(new Entity("systemuserroles") { ["systemuserid"] = Ana, ["roleid"] = rolEjecutivo });
            svc.Sembrar(new Entity("systemuserroles") { ["systemuserid"] = Beto, ["roleid"] = rolOtro });
            svc.Sembrar(new Entity("systemuserroles") { ["systemuserid"] = carla, ["roleid"] = rolEjecutivo });
            return svc;
        }

        // ================================================================================================ foto: argumentos
        [Fact]
        public void La_foto_exige_los_dos_servicios_y_las_claves()
        {
            var s = new OrganizationServiceEnMemoria();
            Assert.Throws<ArgumentNullException>(() => new FotoDeCatalogosDataverse(null, s));
            Assert.Throws<ArgumentNullException>(() => new FotoDeCatalogosDataverse(s, null));
            Assert.Throws<ArgumentNullException>(() => new FotoDeCatalogosDataverse(s, s).ParaCarga(null));
            Assert.Throws<ArgumentNullException>(() => new FotoDeCatalogosDataverse(s, s).ParaCambioDeEstado(null));
        }

        // ================================================================================================ foto de la carga
        [Fact]
        public void La_foto_de_la_carga_trae_lo_nombrado_activo_e_inactivo_y_nada_mas()
        {
            var cat = new OrganizationServiceEnMemoria();
            var sol = Cliente(cat, "000000123", "ABC123456001", "Ferretería Sol");
            var dormido = Cliente(cat, "000000789", "INA000000003", "Cliente Dormido", Inactivo);
            Cliente(cat, "000000999", "NOM000000999", "No nombrado");
            var juan = Autorizado(cat, "juan@empresa.com", "Juan", Inactivo);
            Autorizado(cat, "otro@empresa.com", "Otro");
            var aut = Autorizacion(cat, juan, sol, Tablas.Activo, new DateTime(2024, 10, 1), "Firmado");
            Autorizacion(cat, juan, Cliente(cat, "000000555", "NOE000000555", "Otro cliente"));

            var claves = ClavesDeCarga.Extraer(new[]
            {
                Fila(2, ("A", "123"), ("H", "juan@empresa.com")),
                Fila(3, ("B", "INA000000003")),
            });
            var foto = new FotoDeCatalogosDataverse(cat, Sistema()).ParaCarga(claves);

            Assert.Equal(new[] { sol, dormido }.OrderBy(g => g), foto.Clientes.Select(c => c.Id).OrderBy(g => g));
            var cSol = foto.Clientes.Single(c => c.Id == sol);
            Assert.Equal(("000000123", "ABC123456001", "Ferretería Sol", Ana, true), (cSol.CifBac, cSol.CifCom, cSol.RazonSocial, cSol.DuenoId, cSol.Activo));
            Assert.False(foto.Clientes.Single(c => c.Id == dormido).Activo);

            var a = Assert.Single(foto.Autorizados);
            Assert.Equal((juan, "juan@empresa.com", "Juan", false), (a.Id, a.Correo, a.NombreContacto, a.Activo));

            // Solo el par (correo encontrado × cliente encontrado); la de Juan sobre un cliente no nombrado no entra.
            var au = Assert.Single(foto.Autorizaciones);
            Assert.Equal((aut, juan, sol, (DateTime?)new DateTime(2024, 10, 1), "Firmado", true), (au.Id, au.AutorizadoId, au.ClienteId, au.FechaDocumento, au.Comentarios, au.Activo));
        }

        [Fact]
        public void La_foto_de_la_carga_trae_al_dueno_de_un_plan_aunque_el_archivo_no_lo_nombre()
        {
            var cat = new OrganizationServiceEnMemoria();
            var luna = Cliente(cat, "000000456", "XYZ987654002", "Distribuidora Luna");
            var plan = Plan(cat, "0B34", luna, Inactivo);

            var foto = new FotoDeCatalogosDataverse(cat, Sistema()).ParaCarga(ClavesDeCarga.Extraer(new[] { Fila(2, ("A", "123"), ("E", "b34")) }));

            var p = Assert.Single(foto.Planes);
            Assert.Equal((plan, "0B34", luna, TipoDeFormatoDelPlan._10, Moneda.USD, false), (p.Id, p.Codigo, p.ClienteId, p.Formato, p.Moneda, p.Activo));
            Assert.Contains(foto.Clientes, c => c.Id == luna && c.RazonSocial == "Distribuidora Luna");
        }

        [Fact]
        public void Los_usuarios_se_leen_con_SYSTEM_y_dicen_si_estan_habilitados_y_si_son_ejecutivos()
        {
            var cat = new OrganizationServiceEnMemoria();
            var sistema = Sistema();
            var claves = ClavesDeCarga.Extraer(new[]
            {
                Fila(2, ("D", "ana@banco.com")), Fila(3, ("D", "BETO@banco.com")), Fila(4, ("D", "carla@banco.com")), Fila(5, ("D", "nadie@banco.com")),
            });
            var foto = new FotoDeCatalogosDataverse(cat, sistema).ParaCarga(claves);

            Assert.Equal(3, foto.Usuarios.Count);
            var ana = foto.Usuarios.Single(u => u.Id == Ana);
            Assert.True(ana.Habilitado); Assert.True(ana.EsEjecutivo);
            var beto = foto.Usuarios.Single(u => u.Id == Beto);
            Assert.True(beto.Habilitado); Assert.False(beto.EsEjecutivo);
            var carla = foto.Usuarios.Single(u => u.Correo.Equals("carla@banco.com", StringComparison.OrdinalIgnoreCase));
            Assert.False(carla.Habilitado); Assert.True(carla.EsEjecutivo);

            Assert.Empty(cat.Llamadas.Where(l => l.Entidad == "systemuser" || l.Entidad == "role" || l.Entidad == "systemuserroles"));
            Assert.NotEmpty(sistema.Llamadas);
        }

        [Fact]
        public void Sin_claves_no_hay_ninguna_consulta()
        {
            var cat = new OrganizationServiceEnMemoria();
            var sistema = Sistema();
            var foto = new FotoDeCatalogosDataverse(cat, sistema);
            var vacia = foto.ParaCarga(ClavesDeCarga.Extraer(new[] { Fila(2, ("C", "solo razón social")) }));
            Assert.Empty(vacia.Clientes); Assert.Empty(vacia.Usuarios);
            foto.ParaCambioDeEstado(ClavesDeCambioDeEstado.Extraer(new[] { Fila(2, ("A", "CLIENTE"), ("B", "ACTIVAR")) }));
            Assert.Empty(cat.Llamadas);
            Assert.Empty(sistema.Llamadas);
        }

        [Fact]
        public void La_cantidad_de_consultas_no_depende_de_la_cantidad_de_claves()
        {
            int Consultas(int n)
            {
                var cat = new OrganizationServiceEnMemoria();
                var sistema = Sistema();
                var filas = Enumerable.Range(0, n).Select(i => Fila(i + 2,
                    ("A", (100 + i).ToString()), ("B", $"NUE{i:D6}001"), ("D", "ana@banco.com"), ("E", (i + 0x2000).ToString("X4")), ("H", $"c{i}@empresa.com"))).ToArray();
                for (var i = 0; i < n; i++)
                {
                    var c = Cliente(cat, (100 + i).ToString("D9"), $"NUE{i:D6}001", $"C{i}");
                    var a = Autorizado(cat, $"c{i}@empresa.com", $"N{i}");
                    Plan(cat, (i + 0x2000).ToString("X4"), c);
                    Autorizacion(cat, a, c);
                }

                new FotoDeCatalogosDataverse(cat, sistema).ParaCarga(ClavesDeCarga.Extraer(filas));
                return cat.Llamadas.Count + sistema.Llamadas.Count;
            }

            var una = Consultas(1);
            Assert.Equal(una, Consultas(50));
            Assert.True(una <= 8, $"{una} consultas");
        }

        [Fact]
        public void Una_consulta_con_mas_registros_de_los_que_trajo_falla_en_vez_de_trabajar_con_una_foto_truncada()
        {
            var cat = new OrganizationServiceEnMemoria();
            Cliente(cat, "000000123", "ABC123456001", "Sol");
            var truncado = new ServicioConMasRegistros(cat, Tablas.Cliente);
            var ex = Assert.Throws<InvalidOperationException>(() =>
                new FotoDeCatalogosDataverse(truncado, Sistema()).ParaCarga(ClavesDeCarga.Extraer(new[] { Fila(2, ("A", "123")) })));
            Assert.Contains(Tablas.Cliente, ex.Message);
        }

        [Fact]
        public void Un_choice_fuera_del_enum_es_un_error_que_nombra_la_columna()
        {
            var cat = new OrganizationServiceEnMemoria();
            var c = Cliente(cat, "000000123", "ABC123456001", "Sol");
            cat.Sembrar(E(Tablas.Plan, Tablas.Activo, ("sanic_codigo", "0A12"), ("sanic_clienteid", new EntityReference(Tablas.Cliente, c)),
                ("sanic_tipoformato", new OptionSetValue(999)), ("sanic_moneda", new OptionSetValue((int)Moneda.COR))));
            var ex = Assert.Throws<InvalidOperationException>(() =>
                new FotoDeCatalogosDataverse(cat, Sistema()).ParaCarga(ClavesDeCarga.Extraer(new[] { Fila(2, ("E", "0A12")) })));
            Assert.Contains("sanic_tipoformato", ex.Message);
        }

        // ================================================================================================ foto del cambio de estado
        [Fact]
        public void La_foto_del_cambio_de_estado_trae_todas_las_autorizaciones_de_los_clientes_y_correos_nombrados_y_ningun_usuario()
        {
            var cat = new OrganizationServiceEnMemoria();
            var sistema = Sistema();
            var sol = Cliente(cat, "000000123", "ABC123456001", "Sol");
            var otro = Cliente(cat, "000000999", "NOM000000999", "Otro");
            var juan = Autorizado(cat, "juan@empresa.com", "Juan");
            var pedro = Autorizado(cat, "pedro@empresa.com", "Pedro");
            var juanOtro = Autorizacion(cat, juan, otro);              // por el correo nombrado
            var pedroSol = Autorizacion(cat, pedro, sol, Inactivo);    // por el cliente nombrado
            Autorizacion(cat, pedro, otro);                            // ni cliente ni correo nombrados

            var claves = ClavesDeCambioDeEstado.Extraer(new[]
            {
                Fila(2, ("A", "CLIENTE"), ("B", "DESACTIVAR"), ("C", "123")),
                Fila(3, ("A", "CORREO"), ("B", "DESACTIVAR"), ("F", "juan@empresa.com")),
            });
            var foto = new FotoDeCatalogosDataverse(cat, sistema).ParaCambioDeEstado(claves);

            Assert.Equal(new[] { juanOtro, pedroSol }.OrderBy(g => g), foto.Autorizaciones.Select(a => a.Id).OrderBy(g => g));
            Assert.False(foto.Autorizaciones.Single(a => a.Id == pedroSol).Activo);
            Assert.Empty(foto.Usuarios);
            Assert.Empty(sistema.Llamadas);
        }

        // ================================================================================================ escritor: carga
        private static PlanDeCarga PlanDeCargaValido(out Guid sol, out Guid juan)
        {
            sol = Guid.NewGuid();
            juan = Guid.NewGuid();
            var foto = new EstadoDeCatalogos(
                new[] { new ClienteExistente(sol, "000000123", "ABC123456001", "Ferretería Sol", Ana, true) },
                new PlanExistente[0],
                new[] { new AutorizadoExistente(juan, "juan@empresa.com", "Juan", true) },
                new AutorizacionClienteExistente[0],
                new[] { new UsuarioCandidato(Beto, "beto@banco.com", true, true) });
            var plan = PlanificadorDeCarga.Planificar(new[]
            {
                // Cliente nuevo con su plan nuevo y un correo nuevo autorizado sobre él (con fecha y comentario).
                Fila(2, ("A", "555"), ("B", "NUE000000555"), ("C", "Comercial Nueva"), ("D", "beto@banco.com"),
                    ("E", "N1"), ("F", "11"), ("G", "USD"), ("H", "nuevo@empresa.com"), ("I", "Nuevo"), ("J", "15/09/2024"), ("K", "Ok")),
                // Plan nuevo de un cliente existente, y un correo existente autorizado sobre el cliente nuevo (sin fecha ni comentario).
                Fila(3, ("A", "123"), ("E", "N2"), ("F", "06"), ("G", "COR")),
                Fila(4, ("A", "555"), ("H", "juan@empresa.com")),
            }, foto, new DateTime(2026, 10, 1));
            Assert.False(plan.TieneErrores, string.Join(" | ", plan.Filas.SelectMany(f => f.Errores)));
            return plan;
        }

        [Fact]
        public void El_escritor_crea_en_orden_con_las_columnas_exactas_y_resuelve_las_referencias_a_lo_nuevo()
        {
            var plan = PlanDeCargaValido(out var sol, out var juan);
            var svc = new OrganizationServiceEnMemoria();
            new EscritorDeCatalogos(svc, () => TimeSpan.Zero, TimeSpan.FromSeconds(100)).Aplicar(plan);

            // Solo Create, en el orden de escritura, uno por entidad: 1 cliente, 2 planes, 1 correo, 2 autorizaciones.
            Assert.Equal(
                new[] { Tablas.Cliente, Tablas.Plan, Tablas.Plan, Tablas.Autorizado, Tablas.AutorizacionCliente, Tablas.AutorizacionCliente },
                svc.Llamadas.Select(l => (l.Operacion, l.Entidad)).Select(x => { Assert.Equal("Create", x.Operacion); return x.Entidad; }).ToArray());

            var cliente = Assert.Single(svc.Registros(Tablas.Cliente));
            Assert.Equal("Comercial Nueva", cliente["sanic_nombre"]);
            Assert.Equal("000000555", cliente["sanic_cifbac"]);
            Assert.Equal("NUE000000555", cliente["sanic_cifcom"]);
            var dueno = (EntityReference)cliente["ownerid"];
            Assert.Equal(("systemuser", Beto), (dueno.LogicalName, dueno.Id));

            var planes = svc.Registros(Tablas.Plan);
            var n1 = planes.Single(p => (string)p["sanic_codigo"] == "00N1");
            Assert.Equal((int)TipoDeFormatoDelPlan._11, ((OptionSetValue)n1["sanic_tipoformato"]).Value);
            Assert.Equal((int)Moneda.USD, ((OptionSetValue)n1["sanic_moneda"]).Value);
            Assert.Equal(cliente.Id, ((EntityReference)n1["sanic_clienteid"]).Id);
            var n2 = planes.Single(p => (string)p["sanic_codigo"] == "00N2");
            Assert.Equal(sol, ((EntityReference)n2["sanic_clienteid"]).Id);
            Assert.All(planes, p => Assert.False(p.Contains("sanic_nombre"), "el nombre del plan lo calcula el step"));

            var autorizado = Assert.Single(svc.Registros(Tablas.Autorizado));
            Assert.Equal(("nuevo@empresa.com", "Nuevo"), ((string)autorizado["sanic_nombre"], (string)autorizado["sanic_nombrecontacto"]));

            var auts = svc.Registros(Tablas.AutorizacionCliente);
            var conDatos = auts.Single(a => ((EntityReference)a["sanic_autorizadoid"]).Id == autorizado.Id);
            Assert.Equal(cliente.Id, ((EntityReference)conDatos["sanic_clienteid"]).Id);
            Assert.Equal(new DateTime(2024, 9, 15), (DateTime)conDatos["sanic_fechadocumento"]);
            Assert.Equal("Ok", conDatos["sanic_comentarios"]);
            var deJuan = auts.Single(a => ((EntityReference)a["sanic_autorizadoid"]).Id == juan);
            Assert.Equal(cliente.Id, ((EntityReference)deJuan["sanic_clienteid"]).Id);
            Assert.False(deJuan.Contains("sanic_fechadocumento"));
            Assert.False(deJuan.Contains("sanic_comentarios"));
            Assert.All(auts, a => Assert.False(a.Contains("sanic_nombre"), "el nombre de la autorización lo calcula el step"));
        }

        [Fact]
        public void El_escritor_rechaza_un_plan_con_errores_o_nulo_sin_tocar_el_servicio()
        {
            var svc = new OrganizationServiceEnMemoria();
            var escritor = new EscritorDeCatalogos(svc, () => TimeSpan.Zero, TimeSpan.FromSeconds(100));
            var conErrores = PlanificadorDeCarga.Planificar(new[] { Fila(2, ("E", "0A12")) },
                new EstadoDeCatalogos(new ClienteExistente[0], new PlanExistente[0], new AutorizadoExistente[0], new AutorizacionClienteExistente[0], new UsuarioCandidato[0]),
                new DateTime(2026, 10, 1));
            Assert.True(conErrores.TieneErrores);
            Assert.ThrowsAny<ArgumentException>(() => escritor.Aplicar(conErrores));
            Assert.ThrowsAny<ArgumentException>(() => escritor.Aplicar((PlanDeCarga)null));
            Assert.ThrowsAny<ArgumentException>(() => escritor.Aplicar((PlanDeCambioDeEstado)null));
            Assert.Empty(svc.Llamadas);
            Assert.Throws<ArgumentNullException>(() => new EscritorDeCatalogos(null, () => TimeSpan.Zero, TimeSpan.FromSeconds(1)));
            Assert.Throws<ArgumentNullException>(() => new EscritorDeCatalogos(svc, null, TimeSpan.FromSeconds(1)));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(6)]
        public void Si_una_escritura_falla_lanza_con_la_fila_de_origen_y_no_hace_ninguna_llamada_mas(int cualFalla)
        {
            var plan = PlanDeCargaValido(out _, out _);
            var svc = new ServicioQueFallaEn(new OrganizationServiceEnMemoria(), cualFalla, "Entity Key violated");
            var ex = Assert.Throws<EscrituraDeCatalogoFallidaException>(() =>
                new EscritorDeCatalogos(svc, () => TimeSpan.Zero, TimeSpan.FromSeconds(100)).Aplicar(plan));
            Assert.Contains("Entity Key violated", ex.Motivo);
            // Fila de origen de la escritura número `cualFalla`: cliente(2), plan N1(2), plan N2(3), correo(2), aut(2), aut(4).
            var filas = new[] { 2, 2, 3, 2, 2, 4 };
            Assert.Equal(filas[cualFalla - 1], ex.Fila);
            Assert.Equal(cualFalla, svc.Llamadas);
        }

        [Fact]
        public void Con_el_presupuesto_agotado_no_escribe_mas_y_dice_cuantas_alcanzo()
        {
            var plan = PlanDeCargaValido(out _, out _);
            var svc = new OrganizationServiceEnMemoria();
            var ex = Assert.Throws<PresupuestoDeEscrituraAgotadoException>(() =>
                new EscritorDeCatalogos(svc, () => svc.Llamadas.Count >= 2 ? TimeSpan.FromSeconds(100) : TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(100)).Aplicar(plan));
            Assert.Equal((2, 6), (ex.Escritas, ex.Total));
            Assert.Equal(2, svc.Llamadas.Count);
        }

        // ================================================================================================ escritor: cambio de estado
        [Fact]
        public void El_escritor_cambia_el_estado_nativo_en_la_tabla_de_cada_tipo()
        {
            var cat = new OrganizationServiceEnMemoria();
            var sol = Cliente(cat, "000000123", "ABC123456001", "Sol");
            var dormido = Cliente(cat, "000000789", "INA000000003", "Dormido", Inactivo);
            var plan0 = Plan(cat, "0A12", sol);
            var juan = Autorizado(cat, "juan@empresa.com", "Juan");
            var aut = Autorizacion(cat, juan, dormido, Inactivo);
            var filas = new[]
            {
                Fila(2, ("A", "CLIENTE"), ("B", "DESACTIVAR"), ("C", "123")),
                Fila(3, ("A", "CLIENTE"), ("B", "ACTIVAR"), ("C", "789")),
                Fila(4, ("A", "PLAN"), ("B", "DESACTIVAR"), ("E", "0A12")),
                Fila(5, ("A", "CORREO"), ("B", "DESACTIVAR"), ("F", "juan@empresa.com")),
                Fila(6, ("A", "AUTORIZACION_CLIENTE"), ("B", "ACTIVAR"), ("C", "789"), ("F", "juan@empresa.com")),
            };
            var foto = new FotoDeCatalogosDataverse(cat, new OrganizationServiceEnMemoria()).ParaCambioDeEstado(ClavesDeCambioDeEstado.Extraer(filas));
            var plan = PlanificadorDeCambioDeEstado.Planificar(filas, foto);
            Assert.False(plan.TieneErrores, string.Join(" | ", plan.Filas.SelectMany(f => f.Errores)));

            var antes = cat.Llamadas.Count;
            new EscritorDeCatalogos(cat, () => TimeSpan.Zero, TimeSpan.FromSeconds(100)).Aplicar(plan);
            Assert.All(cat.Llamadas.Skip(antes), l => Assert.Equal("Update", l.Operacion));
            Assert.Equal(5, cat.Llamadas.Count - antes);

            (int, int) Estado(string tabla, Guid id)
            {
                var r = cat.Registros(tabla).Single(x => x.Id == id);
                return (((OptionSetValue)r["statecode"]).Value, ((OptionSetValue)r["statuscode"]).Value);
            }

            Assert.Equal((1, 2), Estado(Tablas.Cliente, sol));
            Assert.Equal((0, 1), Estado(Tablas.Cliente, dormido));
            Assert.Equal((1, 2), Estado(Tablas.Plan, plan0));
            Assert.Equal((1, 2), Estado(Tablas.Autorizado, juan));
            Assert.Equal((0, 1), Estado(Tablas.AutorizacionCliente, aut));
        }

        [Fact]
        public void Si_un_cambio_de_estado_falla_lanza_con_su_fila_y_no_sigue()
        {
            var cat = new OrganizationServiceEnMemoria();
            Cliente(cat, "000000123", "ABC123456001", "Sol");
            Cliente(cat, "000000456", "XYZ987654002", "Luna");
            var filas = new[]
            {
                Fila(2, ("A", "CLIENTE"), ("B", "DESACTIVAR"), ("C", "123")),
                Fila(7, ("A", "CLIENTE"), ("B", "DESACTIVAR"), ("C", "456")),
            };
            var plan = PlanificadorDeCambioDeEstado.Planificar(filas,
                new FotoDeCatalogosDataverse(cat, new OrganizationServiceEnMemoria()).ParaCambioDeEstado(ClavesDeCambioDeEstado.Extraer(filas)));
            var svc = new ServicioQueFallaEn(cat, 1, "sin privilegio");
            var ex = Assert.Throws<EscrituraDeCatalogoFallidaException>(() =>
                new EscritorDeCatalogos(svc, () => TimeSpan.Zero, TimeSpan.FromSeconds(100)).Aplicar(plan));
            Assert.Equal(2, ex.Fila);
            Assert.Equal(1, svc.Llamadas);
        }

        // ================================================================================================ revisión 2026-10-01: fallar cerrado
        [Fact]
        public void Un_usuario_sin_isdisabled_en_la_respuesta_no_cuenta_como_habilitado()
        {
            var sistema = new OrganizationServiceEnMemoria();
            var rol = sistema.Sembrar(new Entity("role") { ["name"] = "SR - MPPP - Ejecutivo" });
            sistema.Sembrar(new Entity("systemuser", Ana) { ["internalemailaddress"] = "ana@banco.com" });
            sistema.Sembrar(new Entity("systemuserroles") { ["systemuserid"] = Ana, ["roleid"] = rol });
            var foto = new FotoDeCatalogosDataverse(new OrganizationServiceEnMemoria(), sistema)
                .ParaCarga(ClavesDeCarga.Extraer(new[] { Fila(2, ("D", "ana@banco.com")) }));
            Assert.False(Assert.Single(foto.Usuarios).Habilitado);
        }

        [Fact]
        public void Si_el_rol_de_ejecutivo_no_existe_en_el_entorno_es_un_error_de_configuracion_que_lo_nombra()
        {
            var sistema = new OrganizationServiceEnMemoria();
            sistema.Sembrar(new Entity("systemuser", Ana) { ["internalemailaddress"] = "ana@banco.com", ["isdisabled"] = false });
            var ex = Assert.Throws<InvalidOperationException>(() => new FotoDeCatalogosDataverse(new OrganizationServiceEnMemoria(), sistema)
                .ParaCarga(ClavesDeCarga.Extraer(new[] { Fila(2, ("D", "ana@banco.com")) })));
            Assert.Contains("SR - MPPP - Ejecutivo", ex.Message);
        }

        [Fact]
        public void La_falla_de_Dataverse_viaja_como_InnerException()
        {
            var plan = PlanDeCargaValido(out _, out _);
            var svc = new ServicioQueFallaEn(new OrganizationServiceEnMemoria(), 1, "Entity Key violated");
            var ex = Assert.Throws<EscrituraDeCatalogoFallidaException>(() =>
                new EscritorDeCatalogos(svc, () => TimeSpan.Zero, TimeSpan.FromSeconds(100)).Aplicar(plan));
            Assert.IsType<FaultException<OrganizationServiceFault>>(ex.InnerException);
        }

        // ================================================================================================ dobles de esta prueba
        /// <summary>Decorador: la escritura número N (Create o Update) falla como Dataverse; cuenta TODAS las llamadas.</summary>
        private sealed class ServicioQueFallaEn : IOrganizationService
        {
            private readonly IOrganizationService _real;
            private readonly int _falla;
            private readonly string _mensaje;
            private int _escrituras;

            public ServicioQueFallaEn(IOrganizationService real, int falla, string mensaje) { _real = real; _falla = falla; _mensaje = mensaje; }

            public int Llamadas { get; private set; }

            private void Escritura()
            {
                Llamadas++;
                if (++_escrituras == _falla)
                    throw new FaultException<OrganizationServiceFault>(new OrganizationServiceFault { Message = _mensaje }, new FaultReason(_mensaje));
            }

            public Guid Create(Entity entity) { Escritura(); return _real.Create(entity); }
            public void Update(Entity entity) { Escritura(); _real.Update(entity); }
            public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet) { Llamadas++; return _real.Retrieve(entityName, id, columnSet); }
            public void Delete(string entityName, Guid id) { Llamadas++; _real.Delete(entityName, id); }
            public OrganizationResponse Execute(OrganizationRequest request) { Llamadas++; return _real.Execute(request); }
            public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) { Llamadas++; _real.Associate(entityName, entityId, relationship, relatedEntities); }
            public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) { Llamadas++; _real.Disassociate(entityName, entityId, relationship, relatedEntities); }
            public EntityCollection RetrieveMultiple(QueryBase query) { Llamadas++; return _real.RetrieveMultiple(query); }
        }

        /// <summary>Decorador: las consultas sobre esa tabla dicen que hay más registros de los que trajeron.</summary>
        private sealed class ServicioConMasRegistros : IOrganizationService
        {
            private readonly IOrganizationService _real;
            private readonly string _tabla;

            public ServicioConMasRegistros(IOrganizationService real, string tabla) { _real = real; _tabla = tabla; }

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
    }
}
