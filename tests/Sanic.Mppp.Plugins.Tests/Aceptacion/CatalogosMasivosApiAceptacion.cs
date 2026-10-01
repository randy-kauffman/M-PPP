using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Mppp.Plugins.Api;
using Sanic.Mppp.Plugins.Datos;
using Sanic.Mppp.Plugins.Exportacion;
using Sanic.Mppp.Plugins.Plantilla;
using Sanic.Mppp.Plugins.Tests.Apoyo;
using Sanic.Mppp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Aceptacion
{
    /// <summary>
    /// DN-04, las dos Custom API de catálogos (diseno/03 §1c y §1d): el núcleo <see cref="CatalogosMasivos"/> y sus envoltorios
    /// IPlugin. Une las piezas ya probadas; acá se prueba el cableado: códigos MPPP-CAR, quién escribe, parámetros, todo o nada.
    /// Pruebas del ARQUITECTO: el constructor no las cambia.
    /// </summary>
    public class CatalogosMasivosApiAceptacion
    {
        private static readonly DateTime Hoy = new DateTime(2026, 10, 1);
        private static readonly Guid Ana = new Guid("00000000-0000-0000-0000-0000000000a1");
        private static readonly Guid UsuarioQueLlama = new Guid("00000000-0000-0000-0000-00000000cafe");

        // ================================================================================================ armado
        /// <summary>
        /// "Es la plantilla de esa estructura", comparando CONTENIDO: el generador de .xlsx no es determinista (hora del zip e Id
        /// de relación aleatorio), así que dos plantillas armadas por separado nunca son iguales byte a byte. Corregido el
        /// 2026-10-01: la primera versión de estas pruebas comparaba base64 y era imposible de cumplir (lo detectó el constructor).
        /// </summary>
        private static void EsLaPlantillaDe(EstructuraDeExcelDeCatalogo esperada, EstructuraDeExcelDeCatalogo otra, string base64)
        {
            var bytes = Convert.FromBase64String(base64);
            var propia = new LectorOpenXml().Leer(bytes, esperada.ConfiguracionDeLectura(300));
            Assert.True(propia.EsValido, string.Join(" | ", propia.Errores));
            Assert.Empty(propia.Filas);
            Assert.Equal(TipoDeRechazoLectura.HojaInexistente, new LectorOpenXml().Leer(bytes, otra.ConfiguracionDeLectura(300)).Rechazo);
        }

        private static OrganizationServiceEnMemoria Sistema(string filasMaximo = null, string limites = null)
        {
            var s = new OrganizationServiceEnMemoria();
            var rol = s.Sembrar(new Entity("role") { ["name"] = "SR - MPPP - Ejecutivo" });
            s.Sembrar(new Entity("systemuser", Ana) { ["internalemailaddress"] = "ana@banco.com", ["isdisabled"] = false });
            s.Sembrar(new Entity("systemuserroles") { ["systemuserid"] = Ana, ["roleid"] = rol });
            if (filasMaximo != null) s.Sembrar(Parametro("carga.filas.maximo", filasMaximo));
            if (limites != null) s.Sembrar(Parametro("lectura.limites", limites));
            return s;
        }

        private static Entity Parametro(string nombre, string valor) => new Entity(Tablas.Parametro)
        {
            ["sanic_nombre"] = nombre, ["sanic_version"] = 1, ["sanic_valor"] = valor, ["statecode"] = new OptionSetValue(Tablas.Activo),
        };

        private static OrganizationServiceEnMemoria Catalogos(out Guid sol)
        {
            var c = new OrganizationServiceEnMemoria();
            sol = c.Sembrar(new Entity(Tablas.Cliente)
            {
                ["sanic_cifbac"] = "000000123", ["sanic_cifcom"] = "ABC123456001", ["sanic_nombre"] = "Ferretería Sol",
                ["ownerid"] = new EntityReference("systemuser", Ana), ["statecode"] = new OptionSetValue(Tablas.Activo),
            });
            return c;
        }

        private static FilaPlantilla Fila(int numero, params (string letra, string valor)[] valores)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (l, v) in valores) d[l] = v;
            return new FilaPlantilla { NumeroFilaExcel = numero, NumeroOrden = numero - 1, Valores = d };
        }

        /// <summary>Un archivo VÁLIDO de esa estructura (lo arma el mismo armador que usa la API, con la columna Resultado vacía).</summary>
        private static string Archivo(EstructuraDeExcelDeCatalogo e, params FilaPlantilla[] filas)
            => Convert.ToBase64String(ArmadorDeExcelDeCatalogos.Resultado(e, filas, filas.ToDictionary(f => f.NumeroFilaExcel, f => (string)null)));

        /// <summary>Un archivo armado a mano: hoja y encabezados a elección (para los rechazos de estructura).</summary>
        private static string ArchivoCrudo(string hoja, IEnumerable<string> encabezados, params string[] filasExtra)
        {
            var letras = "ABCDEFGHIJKLMNOP";
            var celdas = encabezados.Select((t, i) => XlsxCrudo.Celda($"{letras[i]}1", t)).ToArray();
            return Convert.ToBase64String(XlsxCrudo.ConFilas(hoja, new[] { XlsxCrudo.Fila(1, celdas) }.Concat(filasExtra).ToArray()));
        }

        private static IEnumerable<string> EncabezadosCarga() => EstructuraDeExcelDeCatalogo.Carga.Columnas.Select(c => c.Encabezado);

        private static CatalogosMasivos Nucleo(IOrganizationService usuario, IOrganizationService sistema, Func<TimeSpan> reloj = null, DateTime? hoy = null)
            => new CatalogosMasivos(usuario, sistema, reloj ?? (() => TimeSpan.Zero), hoy ?? Hoy);

        private static string Codigo(Action accion) => Assert.Throws<CargaMasivaRechazadaException>(accion).Codigo;

        private static IEnumerable<LlamadaRegistrada> Escrituras(OrganizationServiceEnMemoria s) => s.Llamadas.Where(l => l.Operacion == "Create" || l.Operacion == "Update");

        // ================================================================================================ núcleo: argumentos
        [Fact]
        public void El_nucleo_exige_sus_servicios_y_su_reloj()
        {
            var s = new OrganizationServiceEnMemoria();
            Assert.Throws<ArgumentNullException>(() => new CatalogosMasivos(null, s, () => TimeSpan.Zero, Hoy));
            Assert.Throws<ArgumentNullException>(() => new CatalogosMasivos(s, null, () => TimeSpan.Zero, Hoy));
            Assert.Throws<ArgumentNullException>(() => new CatalogosMasivos(s, s, null, Hoy));
        }

        // ================================================================================================ rechazos del archivo
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("esto no es base64 !!")]
        public void Archivo_vacio_o_base64_invalido_es_MPPP_CAR_001(string archivo)
        {
            var cat = Catalogos(out _);
            Assert.Equal("MPPP-CAR-001", Codigo(() => Nucleo(cat, Sistema()).Cargar(archivo)));
            Assert.Equal("MPPP-CAR-001", Codigo(() => Nucleo(cat, Sistema()).CambiarEstado(archivo)));
            Assert.Empty(cat.Llamadas);
        }

        [Fact]
        public void Los_rechazos_del_lector_se_traducen_a_su_codigo_sin_escribir_nada()
        {
            var cat = Catalogos(out _);
            var n = Nucleo(cat, Sistema(filasMaximo: "2"));
            Assert.Equal("MPPP-CAR-002", Codigo(() => n.Cargar(Convert.ToBase64String(new byte[] { 1, 2, 3, 4 }))));
            Assert.Equal("MPPP-CAR-004", Codigo(() => n.Cargar(ArchivoCrudo("OtraHoja", EncabezadosCarga()))));
            Assert.Equal("MPPP-CAR-005", Codigo(() => n.Cargar(ArchivoCrudo("CargaCatalogos", EncabezadosCarga().Select(t => t == "Moneda" ? "Divisa" : t)))));
            Assert.Equal("MPPP-CAR-005", Codigo(() => n.Cargar(ArchivoCrudo("CargaCatalogos", EncabezadosCarga().Concat(new[] { "Extra" })))));
            Assert.Equal("MPPP-CAR-006", Codigo(() => n.Cargar(Archivo(EstructuraDeExcelDeCatalogo.Carga, Fila(2, ("A", "123")), Fila(3, ("A", "123")), Fila(4, ("A", "123"))))));
            Assert.Equal("MPPP-CAR-007", Codigo(() => n.Cargar(Convert.ToBase64String(ArmadorDeExcelDeCatalogos.Plantilla(EstructuraDeExcelDeCatalogo.Carga)))));
            Assert.Equal("MPPP-CAR-004", Codigo(() => n.CambiarEstado(Archivo(EstructuraDeExcelDeCatalogo.Carga, Fila(2, ("A", "123"))))));
            Assert.Empty(Escrituras(cat));
        }

        [Fact]
        public void Un_archivo_que_supera_lectura_limites_es_MPPP_CAR_003()
        {
            var n = Nucleo(Catalogos(out _), Sistema(limites: @"{""maximoBytesComprimido"":10,""maximoBytesDescomprimido"":20000000}"));
            Assert.Equal("MPPP-CAR-003", Codigo(() => n.Cargar(Archivo(EstructuraDeExcelDeCatalogo.Carga, Fila(2, ("A", "123"))))));
        }

        [Fact]
        public void Los_mensajes_de_rechazo_empiezan_con_su_codigo_y_no_traen_jerga_de_la_libreria()
        {
            var ex = Assert.Throws<CargaMasivaRechazadaException>(() => Nucleo(Catalogos(out _), Sistema()).Cargar(Convert.ToBase64String(new byte[] { 1, 2, 3 })));
            Assert.StartsWith("MPPP-CAR-002: ", ex.Message);
            Assert.DoesNotContain("OpenXml", ex.Message);
            Assert.DoesNotContain("Exception", ex.Message);
        }

        // ================================================================================================ parámetros
        [Theory]
        [InlineData(null)]       // ausente
        [InlineData("abc")]      // no es entero
        [InlineData("0")]        // menor que 1
        public void Sin_un_carga_filas_maximo_usable_el_tope_es_300(string valor)
        {
            var filas300 = Enumerable.Range(2, 300).Select(n => Fila(n, ("A", "123"))).ToArray();
            var filas301 = Enumerable.Range(2, 301).Select(n => Fila(n, ("A", "123"))).ToArray();
            var cat = Catalogos(out _);
            Assert.True(Nucleo(cat, Sistema(filasMaximo: valor)).Cargar(Archivo(EstructuraDeExcelDeCatalogo.Carga, filas300)).Grabado);
            Assert.Equal("MPPP-CAR-006", Codigo(() => Nucleo(cat, Sistema(filasMaximo: valor)).Cargar(Archivo(EstructuraDeExcelDeCatalogo.Carga, filas301))));
        }

        [Fact]
        public void Los_parametros_se_leen_con_SYSTEM_nunca_con_el_usuario()
        {
            var cat = Catalogos(out _);
            var sistema = Sistema(filasMaximo: "300");
            Nucleo(cat, sistema).Cargar(Archivo(EstructuraDeExcelDeCatalogo.Carga, Fila(2, ("A", "123"))));
            Assert.DoesNotContain(cat.Llamadas, l => l.Entidad == Tablas.Parametro);
            Assert.Contains(sistema.Llamadas, l => l.Entidad == Tablas.Parametro);
        }

        // ================================================================================================ carga: todo o nada
        [Fact]
        public void Con_errores_de_fila_devuelve_el_resultado_sin_escribir_nada()
        {
            var cat = Catalogos(out _);
            var r = Nucleo(cat, Sistema()).Cargar(Archivo(EstructuraDeExcelDeCatalogo.Carga,
                Fila(2, ("A", "555"), ("B", "NUE000000555"), ("C", "Comercial Nueva"), ("D", "ana@banco.com")),
                Fila(3, ("A", "123"), ("E", "ZZZZZ"))));
            Assert.False(r.Grabado);
            Assert.Empty(Escrituras(cat));
            Assert.Equal((2, 1, 0, 0, 0, 0), (r.Filas, r.Errores, r.ClientesCreados, r.PlanesCreados, r.CorreosCreados, r.AutorizacionesCreadas));

            var lectura = new LectorOpenXml().Leer(Convert.FromBase64String(r.ResultadoBase64), EstructuraDeExcelDeCatalogo.Carga.ConfiguracionDeLectura(300));
            Assert.True(lectura.EsValido, string.Join(" | ", lectura.Errores));
            Assert.StartsWith("Sin errores — no se grabó", lectura.Filas[0].Valores["L"]);
            Assert.StartsWith("Error: Codigo plan: ", lectura.Filas[1].Valores["L"]);
        }

        [Fact]
        public void Sin_errores_escribe_con_el_servicio_del_usuario_y_devuelve_lo_creado()
        {
            var cat = Catalogos(out _);
            var sistema = Sistema();
            var r = Nucleo(cat, sistema).Cargar(Archivo(EstructuraDeExcelDeCatalogo.Carga,
                Fila(2, ("A", "555"), ("B", "NUE000000555"), ("C", "Comercial Nueva"), ("D", "ana@banco.com"),
                    ("E", "N1"), ("F", "06"), ("G", "COR"), ("H", "nuevo@empresa.com"), ("I", "Nuevo"), ("K", "Ok"))));
            Assert.True(r.Grabado);
            Assert.Equal((1, 0, 1, 1, 1, 1), (r.Filas, r.Errores, r.ClientesCreados, r.PlanesCreados, r.CorreosCreados, r.AutorizacionesCreadas));
            Assert.Equal(4, Escrituras(cat).Count());
            Assert.Empty(Escrituras(sistema));
            var lectura = new LectorOpenXml().Leer(Convert.FromBase64String(r.ResultadoBase64), EstructuraDeExcelDeCatalogo.Carga.ConfiguracionDeLectura(300));
            Assert.Equal("Creado: cliente, plan, correo, autorización", Assert.Single(lectura.Filas).Valores["L"]);
        }

        [Fact]
        public void La_fecha_de_hoy_la_decide_quien_construye_el_nucleo()
        {
            var archivo = Archivo(EstructuraDeExcelDeCatalogo.Carga, Fila(2, ("A", "123"), ("H", "n@empresa.com"), ("I", "N"), ("J", "02/10/2026")));
            Assert.False(Nucleo(Catalogos(out _), Sistema(), hoy: new DateTime(2026, 10, 1)).Cargar(archivo).Grabado);
            Assert.True(Nucleo(Catalogos(out _), Sistema(), hoy: new DateTime(2026, 10, 2)).Cargar(archivo).Grabado);
        }

        [Fact]
        public void Presupuesto_agotado_es_MPPP_CAR_008()
        {
            var cat = Catalogos(out _);
            var n = Nucleo(cat, Sistema(), reloj: () => TimeSpan.FromSeconds(CatalogosMasivos.PresupuestoSegundos));
            Assert.Equal(100, CatalogosMasivos.PresupuestoSegundos);
            var ex = Assert.Throws<CargaMasivaRechazadaException>(() => n.Cargar(Archivo(EstructuraDeExcelDeCatalogo.Carga,
                Fila(2, ("A", "555"), ("B", "NUE000000555"), ("C", "Comercial Nueva"), ("D", "ana@banco.com")))));
            Assert.Equal("MPPP-CAR-008", ex.Codigo);
            Assert.Contains("No se grabó nada", ex.Message);
        }

        [Fact]
        public void Una_escritura_que_falla_es_MPPP_CAR_009_con_su_fila_y_sube_para_que_la_transaccion_deshaga_todo()
        {
            var cat = Catalogos(out _);
            var falla = new ServicioQueFallaAlEscribir(cat, "Entity Key violated");
            var ex = Assert.Throws<CargaMasivaRechazadaException>(() => Nucleo(falla, Sistema()).Cargar(Archivo(EstructuraDeExcelDeCatalogo.Carga,
                Fila(7, ("A", "555"), ("B", "NUE000000555"), ("C", "Comercial Nueva"), ("D", "ana@banco.com")))));
            Assert.Equal("MPPP-CAR-009", ex.Codigo);
            Assert.Contains("7", ex.Message);
            Assert.Contains("Entity Key violated", ex.Message);
            Assert.Contains("No se grabó nada", ex.Message);
            Assert.IsType<EscrituraDeCatalogoFallidaException>(ex.InnerException);
        }

        // ================================================================================================ cambio de estado
        [Fact]
        public void El_cambio_de_estado_escribe_con_el_usuario_y_cuenta_lo_que_cambio()
        {
            var cat = Catalogos(out var sol);
            var r = Nucleo(cat, Sistema()).CambiarEstado(Archivo(EstructuraDeExcelDeCatalogo.CambioDeEstado, Fila(2, ("A", "CLIENTE"), ("B", "DESACTIVAR"), ("C", "123"))));
            Assert.True(r.Grabado);
            Assert.Equal((1, 0, 0, 1, 0), (r.Filas, r.Errores, r.Activados, r.Desactivados, r.SinCambios));
            Assert.Equal(1, ((OptionSetValue)cat.Registros(Tablas.Cliente).Single(x => x.Id == sol)["statecode"]).Value);

            var conError = Nucleo(cat, Sistema()).CambiarEstado(Archivo(EstructuraDeExcelDeCatalogo.CambioDeEstado,
                Fila(2, ("A", "CLIENTE"), ("B", "ACTIVAR"), ("C", "123")), Fila(3, ("A", "PLAN"), ("B", "ACTIVAR"), ("E", "ZZZZ"))));
            Assert.False(conError.Grabado);
            Assert.Equal(1, ((OptionSetValue)cat.Registros(Tablas.Cliente).Single(x => x.Id == sol)["statecode"]).Value);
        }

        // ================================================================================================ plantilla
        [Fact]
        public void La_plantilla_es_la_del_armador_y_no_consulta_nada()
        {
            var cat = new OrganizationServiceEnMemoria();
            var sistema = new OrganizationServiceEnMemoria();
            var n = Nucleo(cat, sistema);
            EsLaPlantillaDe(EstructuraDeExcelDeCatalogo.Carga, EstructuraDeExcelDeCatalogo.CambioDeEstado, n.Plantilla(EstructuraDeExcelDeCatalogo.Carga));
            EsLaPlantillaDe(EstructuraDeExcelDeCatalogo.CambioDeEstado, EstructuraDeExcelDeCatalogo.Carga, n.Plantilla(EstructuraDeExcelDeCatalogo.CambioDeEstado));
            Assert.Empty(cat.Llamadas);
            Assert.Empty(sistema.Llamadas);
        }

        // ================================================================================================ envoltorios IPlugin
        private static (FabricaPorUsuario fabrica, ContextoDeEjecucionSimulado contexto) Envoltorio(OrganizationServiceEnMemoria usuario, OrganizationServiceEnMemoria sistema, params (string, object)[] entradas)
        {
            var contexto = new ContextoDeEjecucionSimulado { UserId = UsuarioQueLlama, InitiatingUserId = UsuarioQueLlama, OperationCreatedOn = DateTime.UtcNow };
            foreach (var (k, v) in entradas) contexto.InputParameters[k] = v;
            return (new FabricaPorUsuario(contexto, usuario, sistema), contexto);
        }

        [Fact]
        public void Con_plantilla_y_sin_archivo_devuelve_la_plantilla_vacia_sin_escribir()
        {
            var (fabrica, contexto) = Envoltorio(new OrganizationServiceEnMemoria(), new OrganizationServiceEnMemoria(), ("plantilla", true));
            new CargarCatalogosApi().Execute(fabrica);
            EsLaPlantillaDe(EstructuraDeExcelDeCatalogo.Carga, EstructuraDeExcelDeCatalogo.CambioDeEstado, (string)contexto.OutputParameters["resultado"]);
            Assert.Equal(false, contexto.OutputParameters["grabado"]);
            Assert.Equal(0, contexto.OutputParameters["filas"]);

            var (fabrica2, contexto2) = Envoltorio(new OrganizationServiceEnMemoria(), new OrganizationServiceEnMemoria(), ("plantilla", true));
            new CambiarEstadoCatalogosApi().Execute(fabrica2);
            EsLaPlantillaDe(EstructuraDeExcelDeCatalogo.CambioDeEstado, EstructuraDeExcelDeCatalogo.Carga, (string)contexto2.OutputParameters["resultado"]);
        }

        [Fact]
        public void Plantilla_y_archivo_juntos_es_MPPP_CAR_010_y_ninguno_es_MPPP_CAR_001()
        {
            var archivo = Archivo(EstructuraDeExcelDeCatalogo.Carga, Fila(2, ("A", "123")));
            var (f1, _) = Envoltorio(new OrganizationServiceEnMemoria(), Sistema(), ("plantilla", true), ("archivo", archivo));
            Assert.StartsWith("MPPP-CAR-010: ", Assert.Throws<InvalidPluginExecutionException>(() => new CargarCatalogosApi().Execute(f1)).Message);
            var (f2, _) = Envoltorio(new OrganizationServiceEnMemoria(), Sistema());
            Assert.StartsWith("MPPP-CAR-001: ", Assert.Throws<InvalidPluginExecutionException>(() => new CargarCatalogosApi().Execute(f2)).Message);
            var (f3, _) = Envoltorio(new OrganizationServiceEnMemoria(), Sistema(), ("plantilla", false));
            Assert.StartsWith("MPPP-CAR-001: ", Assert.Throws<InvalidPluginExecutionException>(() => new CambiarEstadoCatalogosApi().Execute(f3)).Message);
        }

        [Fact]
        public void El_envoltorio_escribe_con_el_usuario_que_llama_y_llena_todas_las_salidas()
        {
            var cat = Catalogos(out _);
            var sistema = Sistema();
            var (fabrica, contexto) = Envoltorio(cat, sistema, ("archivo", Archivo(EstructuraDeExcelDeCatalogo.Carga,
                Fila(2, ("A", "555"), ("B", "NUE000000555"), ("C", "Comercial Nueva"), ("D", "ana@banco.com"), ("H", "nuevo@empresa.com"), ("I", "Nuevo")))));
            new CargarCatalogosApi().Execute(fabrica);

            Assert.Contains(UsuarioQueLlama, fabrica.Pedidos);
            Assert.Contains(null, fabrica.Pedidos);
            Assert.Empty(Escrituras(sistema));
            Assert.Equal(3, Escrituras(cat).Count());
            Assert.Equal(true, contexto.OutputParameters["grabado"]);
            Assert.Equal((1, 0, 0, 1, 0, 1, 1), ((int)contexto.OutputParameters["filas"], (int)contexto.OutputParameters["errores"], (int)contexto.OutputParameters["advertencias"],
                (int)contexto.OutputParameters["clientescreados"], (int)contexto.OutputParameters["planescreados"], (int)contexto.OutputParameters["correoscreados"],
                (int)contexto.OutputParameters["autorizacionescreadas"]));
            Assert.False(string.IsNullOrEmpty((string)contexto.OutputParameters["resultado"]));
        }

        [Fact]
        public void El_envoltorio_del_cambio_de_estado_llena_sus_salidas()
        {
            var cat = Catalogos(out _);
            var (fabrica, contexto) = Envoltorio(cat, Sistema(), ("archivo", Archivo(EstructuraDeExcelDeCatalogo.CambioDeEstado, Fila(2, ("A", "CLIENTE"), ("B", "DESACTIVAR"), ("C", "123")))));
            new CambiarEstadoCatalogosApi().Execute(fabrica);
            Assert.Equal((true, 0, 1, 0), ((bool)contexto.OutputParameters["grabado"], (int)contexto.OutputParameters["activados"],
                (int)contexto.OutputParameters["desactivados"], (int)contexto.OutputParameters["sincambios"]));
        }

        [Fact]
        public void Un_rechazo_sale_con_su_mensaje_y_un_error_inesperado_con_el_generico_y_la_traza()
        {
            var (f1, _) = Envoltorio(Catalogos(out _), Sistema(), ("archivo", ArchivoCrudo("OtraHoja", EncabezadosCarga())));
            Assert.StartsWith("MPPP-CAR-004: ", Assert.Throws<InvalidPluginExecutionException>(() => new CargarCatalogosApi().Execute(f1)).Message);

            var roto = new OrganizationServiceEnMemoria();   // sin rol de ejecutivo: la foto falla con InvalidOperationException
            var (f2, _) = Envoltorio(Catalogos(out _), roto, ("archivo", Archivo(EstructuraDeExcelDeCatalogo.Carga,
                Fila(2, ("A", "555"), ("B", "NUE000000555"), ("C", "Comercial Nueva"), ("D", "ana@banco.com")))));
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => new CargarCatalogosApi().Execute(f2));
            Assert.Equal(CargarCatalogosApi.MensajeDeErrorGenerico, ex.Message);
            Assert.Contains("SR - MPPP - Ejecutivo", f2.Traza.Todo);
        }

        // ================================================================================================ dobles de esta prueba
        /// <summary>Fábrica que da un servicio por usuario: null → SYSTEM; cualquier id → el del usuario. Registra lo pedido.</summary>
        private sealed class FabricaPorUsuario : IServiceProvider, IOrganizationServiceFactory
        {
            private readonly ContextoDeEjecucionSimulado _contexto;
            private readonly IOrganizationService _usuario;
            private readonly IOrganizationService _sistema;

            public FabricaPorUsuario(ContextoDeEjecucionSimulado contexto, IOrganizationService usuario, IOrganizationService sistema)
            {
                _contexto = contexto; _usuario = usuario; _sistema = sistema;
            }

            public List<Guid?> Pedidos { get; } = new List<Guid?>();

            public TraceSimulado Traza { get; } = new TraceSimulado();

            public object GetService(Type serviceType)
            {
                if (serviceType == typeof(IPluginExecutionContext)) return _contexto;
                if (serviceType == typeof(IOrganizationServiceFactory)) return this;
                if (serviceType == typeof(ITracingService)) return Traza;
                return null;
            }

            public IOrganizationService CreateOrganizationService(Guid? userId)
            {
                Pedidos.Add(userId);
                return userId == null ? _sistema : _usuario;
            }
        }

        /// <summary>Decorador: la primera escritura falla como Dataverse.</summary>
        private sealed class ServicioQueFallaAlEscribir : IOrganizationService
        {
            private readonly IOrganizationService _real;
            private readonly string _mensaje;

            public ServicioQueFallaAlEscribir(IOrganizationService real, string mensaje) { _real = real; _mensaje = mensaje; }

            private FaultException<OrganizationServiceFault> Falla() => new FaultException<OrganizationServiceFault>(new OrganizationServiceFault { Message = _mensaje }, new FaultReason(_mensaje));

            public Guid Create(Entity entity) => throw Falla();
            public void Update(Entity entity) => throw Falla();
            public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet) => _real.Retrieve(entityName, id, columnSet);
            public void Delete(string entityName, Guid id) => _real.Delete(entityName, id);
            public OrganizationResponse Execute(OrganizationRequest request) => _real.Execute(request);
            public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) => _real.Associate(entityName, entityId, relationship, relatedEntities);
            public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) => _real.Disassociate(entityName, entityId, relationship, relatedEntities);
            public EntityCollection RetrieveMultiple(QueryBase query) => _real.RetrieveMultiple(query);
        }
    }
}
