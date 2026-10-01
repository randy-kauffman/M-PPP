using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Mppp.Plugins.Api;
using Sanic.Mppp.Plugins.Datos;
using Sanic.Mppp.Plugins.Exportacion;
using Sanic.Mppp.Plugins.Plantilla;
using Sanic.Mppp.Plugins.Tests.Apoyo;
using Sanic.Mppp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Unitarias.Api
{
    /// <summary>
    /// Lo que las pruebas de aceptación de las dos Custom API de catálogos (DN-04, pieza 4c) no fijan: el presupuesto de tiempo
    /// del envoltorio con el reloj de pruebas, la traza (el detalle técnico NUNCA llega al usuario, va a la traza), los textos de
    /// los MPPP-CAR que nombran un dato vigente, la versión activa más alta de los parámetros y "hoy" en Nicaragua.
    /// </summary>
    [Collection(ColeccionesDePrueba.RelojDeApi)]
    public class CatalogosMasivosApiTests
    {
        private static readonly DateTime Hoy = new DateTime(2026, 10, 1);
        private static readonly Guid Ana = new Guid("00000000-0000-0000-0000-0000000000a1");
        private static readonly Guid UsuarioQueLlama = new Guid("00000000-0000-0000-0000-00000000cafe");

        // ------------------------------------------------------------------------------------------ armado
        private static OrganizationServiceEnMemoria Sistema(params Entity[] parametros)
        {
            var s = new OrganizationServiceEnMemoria();
            var rol = s.Sembrar(new Entity("role") { ["name"] = "SR - MPPP - Ejecutivo" });
            s.Sembrar(new Entity("systemuser", Ana) { ["internalemailaddress"] = "ana@banco.com", ["isdisabled"] = false });
            s.Sembrar(new Entity("systemuserroles") { ["systemuserid"] = Ana, ["roleid"] = rol });
            foreach (var p in parametros) s.Sembrar(p);
            return s;
        }

        private static Entity Parametro(string nombre, int version, string valor, int estado = Tablas.Activo) => new Entity(Tablas.Parametro)
        {
            ["sanic_nombre"] = nombre, ["sanic_version"] = version, ["sanic_valor"] = valor, ["statecode"] = new OptionSetValue(estado),
        };

        private static OrganizationServiceEnMemoria Catalogos()
        {
            var c = new OrganizationServiceEnMemoria();
            c.Sembrar(new Entity(Tablas.Cliente)
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

        private static string Archivo(EstructuraDeExcelDeCatalogo e, params FilaPlantilla[] filas)
            => Convert.ToBase64String(ArmadorDeExcelDeCatalogos.Resultado(e, filas, filas.ToDictionary(f => f.NumeroFilaExcel, f => (string)null)));

        private static string ArchivoDeUnaFilaQueCrea() => Archivo(EstructuraDeExcelDeCatalogo.Carga,
            Fila(2, ("A", "555"), ("B", "NUE000000555"), ("C", "Comercial Nueva"), ("D", "ana@banco.com")));

        private static IEnumerable<LlamadaRegistrada> Escrituras(OrganizationServiceEnMemoria s) => s.Llamadas.Where(l => l.Operacion == "Create" || l.Operacion == "Update");

        private static CatalogosMasivos Nucleo(IOrganizationService usuario, IOrganizationService sistema, Func<TimeSpan> reloj = null)
            => new CatalogosMasivos(usuario, sistema, reloj ?? (() => TimeSpan.Zero), Hoy);

        private static (Fabrica fabrica, ContextoDeEjecucionSimulado contexto) Envoltorio(OrganizationServiceEnMemoria usuario, OrganizationServiceEnMemoria sistema, params (string, object)[] entradas)
        {
            var contexto = new ContextoDeEjecucionSimulado { UserId = UsuarioQueLlama, InitiatingUserId = UsuarioQueLlama, OperationCreatedOn = DateTime.UtcNow };
            foreach (var (k, v) in entradas) contexto.InputParameters[k] = v;
            return (new Fabrica(contexto, usuario, sistema), contexto);
        }

        // ------------------------------------------------------------------------------------------ presupuesto del envoltorio
        [Fact]
        public void El_envoltorio_de_la_carga_con_el_presupuesto_agotado_responde_MPPP_CAR_008_sin_escribir()
        {
            var cat = Catalogos();
            var (fabrica, contexto) = Envoltorio(cat, Sistema(), ("archivo", ArchivoDeUnaFilaQueCrea()));
            CargarCatalogosApi.RelojParaPruebas = () => TimeSpan.FromSeconds(CatalogosMasivos.PresupuestoSegundos);
            try
            {
                var ex = Assert.Throws<InvalidPluginExecutionException>(() => new CargarCatalogosApi().Execute(fabrica));
                Assert.StartsWith("MPPP-CAR-008: ", ex.Message);
                Assert.Contains("No se grabó nada", ex.Message);
            }
            finally
            {
                CargarCatalogosApi.RelojParaPruebas = null; // nunca se puede filtrar a otra prueba
            }

            Assert.Empty(Escrituras(cat));
            Assert.False(contexto.OutputParameters.Contains("grabado"));
        }

        [Fact]
        public void El_envoltorio_del_cambio_de_estado_con_el_presupuesto_agotado_responde_MPPP_CAR_008_sin_escribir()
        {
            var cat = Catalogos();
            var (fabrica, _) = Envoltorio(cat, Sistema(), ("archivo", Archivo(EstructuraDeExcelDeCatalogo.CambioDeEstado,
                Fila(2, ("A", "CLIENTE"), ("B", "DESACTIVAR"), ("C", "123")))));
            CambiarEstadoCatalogosApi.RelojParaPruebas = () => TimeSpan.FromSeconds(CatalogosMasivos.PresupuestoSegundos);
            try
            {
                var ex = Assert.Throws<InvalidPluginExecutionException>(() => new CambiarEstadoCatalogosApi().Execute(fabrica));
                Assert.StartsWith("MPPP-CAR-008: ", ex.Message);
                Assert.Contains("No se grabó nada", ex.Message);
            }
            finally
            {
                CambiarEstadoCatalogosApi.RelojParaPruebas = null;
            }

            Assert.Empty(Escrituras(cat));
        }

        [Fact]
        public void El_reloj_de_pruebas_es_solo_de_pruebas_y_arranca_apagado()
        {
            Assert.Null(CargarCatalogosApi.RelojParaPruebas);
            Assert.Null(CambiarEstadoCatalogosApi.RelojParaPruebas);
        }

        [Fact]
        public void Con_la_pasada_1_ya_pasada_del_presupuesto_y_algo_que_escribir_responde_008_sin_escribir()
        {
            // Cuenta con el presupuesto ANTES de escribir, no solo dentro del escritor: el 008 sale sin una sola escritura.
            var cat = Catalogos();
            var n = Nucleo(cat, Sistema(), () => TimeSpan.FromSeconds(CatalogosMasivos.PresupuestoSegundos));
            var ex = Assert.Throws<CargaMasivaRechazadaException>(() => n.Cargar(ArchivoDeUnaFilaQueCrea()));
            Assert.Equal("MPPP-CAR-008", ex.Codigo);
            Assert.Empty(Escrituras(cat));
        }

        [Fact]
        public void Si_no_hay_nada_que_escribir_el_presupuesto_no_se_mira_ni_en_el_cambio_de_estado()
        {
            // Revisión 4c: sin escrituras no hay nada que perder por el tiempo, así que se devuelve el resultado.
            var cat = Catalogos();
            var n = Nucleo(cat, Sistema(), () => TimeSpan.FromMinutes(5));
            var r = n.CambiarEstado(Archivo(EstructuraDeExcelDeCatalogo.CambioDeEstado, Fila(2, ("A", "CLIENTE"), ("B", "ACTIVAR"), ("C", "123"))));   // ya estaba activo
            Assert.True(r.Grabado);
            Assert.Equal((1, 0, 0, 0, 1), (r.Filas, r.Errores, r.Activados, r.Desactivados, r.SinCambios));
            Assert.Empty(Escrituras(cat));
        }

        [Fact]
        public void Un_instante_antes_del_presupuesto_todavia_graba()
        {
            var cat = Catalogos();
            var r = Nucleo(cat, Sistema(), () => TimeSpan.FromSeconds(CatalogosMasivos.PresupuestoSegundos).Subtract(TimeSpan.FromMilliseconds(1)))
                .Cargar(ArchivoDeUnaFilaQueCrea());
            Assert.True(r.Grabado);
            Assert.NotEmpty(Escrituras(cat));
        }

        [Fact]
        public void El_mensaje_del_008_pide_partir_el_archivo()
        {
            var ex = Assert.Throws<CargaMasivaRechazadaException>(() => Nucleo(Catalogos(), Sistema(), () => TimeSpan.FromMinutes(5)).Cargar(ArchivoDeUnaFilaQueCrea()));
            Assert.Contains("Partí", ex.Message);
        }

        // ------------------------------------------------------------------------------------------ traza
        [Fact]
        public void Un_error_inesperado_deja_toda_la_excepcion_en_la_traza_y_nada_de_ella_en_el_mensaje()
        {
            var roto = new OrganizationServiceEnMemoria(); // sin el rol de ejecutivo: la foto falla con InvalidOperationException
            var (fabrica, _) = Envoltorio(Catalogos(), roto, ("archivo", ArchivoDeUnaFilaQueCrea()));
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => new CargarCatalogosApi().Execute(fabrica));
            Assert.Equal(CargarCatalogosApi.MensajeDeErrorGenerico, ex.Message);
            Assert.Contains("InvalidOperationException", fabrica.Traza.Todo);
            Assert.DoesNotContain("SR - MPPP - Ejecutivo", ex.Message);
        }

        [Fact]
        public void Un_error_que_no_es_de_la_API_en_el_cambio_de_estado_tambien_sale_generico()
        {
            var (fabrica, _) = Envoltorio(Catalogos(), Sistema(Parametro("lectura.limites", 1, "esto no es json {")),
                ("archivo", Archivo(EstructuraDeExcelDeCatalogo.CambioDeEstado, Fila(2, ("A", "CLIENTE"), ("B", "DESACTIVAR"), ("C", "123")))));
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => new CambiarEstadoCatalogosApi().Execute(fabrica));
            Assert.Equal(CargarCatalogosApi.MensajeDeErrorGenerico, ex.Message);
            Assert.Contains("FormatException", fabrica.Traza.Todo);
        }

        [Fact]
        public void El_detalle_tecnico_del_lector_va_a_la_traza_y_el_mensaje_no_lo_trae()
        {
            var basura = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 });
            var lectura = new LectorOpenXml().Leer(Convert.FromBase64String(basura), EstructuraDeExcelDeCatalogo.Carga.ConfiguracionDeLectura(300));
            Assert.Equal(TipoDeRechazoLectura.ArchivoIlegible, lectura.Rechazo);
            Assert.False(string.IsNullOrWhiteSpace(lectura.DetalleTecnico));

            var (fabrica, _) = Envoltorio(Catalogos(), Sistema(), ("archivo", basura));
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => new CargarCatalogosApi().Execute(fabrica));
            Assert.StartsWith("MPPP-CAR-002: ", ex.Message);
            Assert.DoesNotContain(lectura.DetalleTecnico, ex.Message);
            Assert.Contains(lectura.DetalleTecnico, fabrica.Traza.Todo);
        }

        [Fact]
        public void Un_rechazo_con_codigo_deja_su_codigo_en_la_traza()
        {
            var (fabrica, _) = Envoltorio(Catalogos(), Sistema(), ("plantilla", true), ("archivo", ArchivoDeUnaFilaQueCrea()));
            Assert.Throws<InvalidPluginExecutionException>(() => new CargarCatalogosApi().Execute(fabrica));
            Assert.Contains("MPPP-CAR-010", fabrica.Traza.Todo);
        }

        [Fact]
        public void Una_escritura_que_falla_deja_la_falla_original_en_la_traza_y_el_mensaje_nombra_la_fila_y_el_motivo()
        {
            var cat = Catalogos();
            var falla = new ServicioQueFalla(cat, "Entity Key violated");
            var (fabrica, _) = Envoltorio(null, Sistema(), ("archivo", Archivo(EstructuraDeExcelDeCatalogo.Carga,
                Fila(7, ("A", "555"), ("B", "NUE000000555"), ("C", "Comercial Nueva"), ("D", "ana@banco.com")))));
            fabrica.UsuarioPersonalizado = falla;
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => new CargarCatalogosApi().Execute(fabrica));
            Assert.StartsWith("MPPP-CAR-009: ", ex.Message);
            Assert.Contains("fila 7", ex.Message);
            Assert.Contains("Entity Key violated", ex.Message);
            Assert.Contains("No se grabó nada", ex.Message);
            Assert.Contains("FaultException", fabrica.Traza.Todo);
        }

        // ------------------------------------------------------------------------------------------ textos que nombran un dato vigente
        [Theory]
        [InlineData("2")]
        [InlineData("7")]
        public void El_006_nombra_el_maximo_de_filas_vigente(string maximo)
        {
            var filas = Enumerable.Range(2, int.Parse(maximo, CultureInfo.InvariantCulture) + 1).Select(n => Fila(n, ("A", "123"))).ToArray();
            var ex = Assert.Throws<CargaMasivaRechazadaException>(() => Nucleo(Catalogos(), Sistema(Parametro("carga.filas.maximo", 1, maximo)))
                .Cargar(Archivo(EstructuraDeExcelDeCatalogo.Carga, filas)));
            Assert.Equal("MPPP-CAR-006", ex.Codigo);
            Assert.Contains(maximo + " filas", ex.Message);
        }

        [Fact]
        public void El_006_sin_parametro_nombra_el_300_por_defecto()
        {
            var filas = Enumerable.Range(2, 301).Select(n => Fila(n, ("A", "123"))).ToArray();
            var ex = Assert.Throws<CargaMasivaRechazadaException>(() => Nucleo(Catalogos(), Sistema()).Cargar(Archivo(EstructuraDeExcelDeCatalogo.Carga, filas)));
            Assert.Contains("300 filas", ex.Message);
        }

        [Fact]
        public void El_004_nombra_la_hoja_esperada()
        {
            var ex = Assert.Throws<CargaMasivaRechazadaException>(() => Nucleo(Catalogos(), Sistema())
                .CambiarEstado(Archivo(EstructuraDeExcelDeCatalogo.Carga, Fila(2, ("A", "123")))));
            Assert.Equal("MPPP-CAR-004", ex.Codigo);
            Assert.Contains("ActivarDesactivar", ex.Message);
        }

        [Fact]
        public void El_005_nombra_lo_que_encontro_el_lector()
        {
            var encabezados = EstructuraDeExcelDeCatalogo.Carga.Columnas.Select(c => c.Encabezado == "Moneda" ? "Divisa" : c.Encabezado).ToArray();
            var letras = "ABCDEFGHIJKLMNOP";
            var crudo = Convert.ToBase64String(XlsxCrudo.ConFilas("CargaCatalogos",
                new[] { XlsxCrudo.Fila(1, encabezados.Select((t, i) => XlsxCrudo.Celda(letras[i] + "1", t)).ToArray()) }));
            var ex = Assert.Throws<CargaMasivaRechazadaException>(() => Nucleo(Catalogos(), Sistema()).Cargar(crudo));
            Assert.Equal("MPPP-CAR-005", ex.Codigo);
            Assert.Contains("Moneda", ex.Message);
            Assert.Contains("Divisa", ex.Message);
        }

        [Fact]
        public void Todos_los_rechazos_de_archivo_empiezan_con_su_codigo_hablan_en_voseo_y_no_traen_jerga()
        {
            var cat = Catalogos();
            var n = Nucleo(cat, Sistema(Parametro("carga.filas.maximo", 1, "1"), Parametro("lectura.limites", 1, @"{""maximoBytesComprimido"":10}")));
            var casos = new Dictionary<string, Action>
            {
                ["MPPP-CAR-001"] = () => n.Cargar(""),
                ["MPPP-CAR-002"] = () => Nucleo(cat, Sistema()).Cargar(Convert.ToBase64String(new byte[] { 1, 2, 3, 4 })),
                ["MPPP-CAR-003"] = () => n.Cargar(Archivo(EstructuraDeExcelDeCatalogo.Carga, Fila(2, ("A", "123")))),
                ["MPPP-CAR-004"] = () => Nucleo(cat, Sistema()).Cargar(Archivo(EstructuraDeExcelDeCatalogo.CambioDeEstado, Fila(2, ("A", "CLIENTE")))),
                ["MPPP-CAR-006"] = () => Nucleo(cat, Sistema(Parametro("carga.filas.maximo", 1, "1"))).Cargar(Archivo(EstructuraDeExcelDeCatalogo.Carga, Fila(2, ("A", "123")), Fila(3, ("A", "123")))),
                ["MPPP-CAR-007"] = () => Nucleo(cat, Sistema()).Cargar(Convert.ToBase64String(ArmadorDeExcelDeCatalogos.Plantilla(EstructuraDeExcelDeCatalogo.Carga))),
                ["MPPP-CAR-008"] = () => Nucleo(cat, Sistema(), () => TimeSpan.FromMinutes(5)).Cargar(ArchivoDeUnaFilaQueCrea()),
            };
            foreach (var caso in casos)
            {
                var ex = Assert.Throws<CargaMasivaRechazadaException>(caso.Value);
                Assert.Equal(caso.Key, ex.Codigo);
                Assert.StartsWith(caso.Key + ": ", ex.Message);
                Assert.True(ex.Message.Length > caso.Key.Length + 20, ex.Message);
                foreach (var jerga in new[] { "OpenXml", "Exception", "System.", "SDK", "ZIP", "Stack" })
                    Assert.DoesNotContain(jerga, ex.Message, StringComparison.OrdinalIgnoreCase);
            }
        }

        // ------------------------------------------------------------------------------------------ parámetros
        [Fact]
        public void El_tope_de_filas_sale_de_la_version_activa_mas_alta()
        {
            var filas = Enumerable.Range(2, 3).Select(n => Fila(n, ("A", "123"))).ToArray();
            var sistema = Sistema(
                Parametro("carga.filas.maximo", 1, "1"),
                Parametro("carga.filas.maximo", 2, "3"),
                Parametro("carga.filas.maximo", 3, "1", 1));
            Assert.True(Nucleo(Catalogos(), sistema).Cargar(Archivo(EstructuraDeExcelDeCatalogo.Carga, filas)).Grabado);
        }

        [Fact]
        public void Un_lectura_limites_mal_formado_no_se_traga_es_un_error_de_configuracion()
        {
            var n = Nucleo(Catalogos(), Sistema(Parametro("lectura.limites", 1, "esto no es json {")));
            Assert.Throws<FormatException>(() => n.Cargar(ArchivoDeUnaFilaQueCrea()));
        }

        [Fact]
        public void El_cambio_de_estado_tambien_respeta_el_tope_de_filas()
        {
            var n = Nucleo(Catalogos(), Sistema(Parametro("carga.filas.maximo", 1, "1")));
            var ex = Assert.Throws<CargaMasivaRechazadaException>(() => n.CambiarEstado(Archivo(EstructuraDeExcelDeCatalogo.CambioDeEstado,
                Fila(2, ("A", "CLIENTE"), ("B", "DESACTIVAR"), ("C", "123")), Fila(3, ("A", "CLIENTE"), ("B", "ACTIVAR"), ("C", "123")))));
            Assert.Equal("MPPP-CAR-006", ex.Codigo);
        }

        // ------------------------------------------------------------------------------------------ envoltorio: entradas y hoy
        [Fact]
        public void Un_archivo_vacio_junto_con_plantilla_true_es_pedir_la_plantilla()
        {
            var (fabrica, contexto) = Envoltorio(new OrganizationServiceEnMemoria(), new OrganizationServiceEnMemoria(), ("plantilla", true), ("archivo", ""));
            new CargarCatalogosApi().Execute(fabrica);
            Assert.False(string.IsNullOrEmpty((string)contexto.OutputParameters["resultado"]));
            Assert.Equal(false, contexto.OutputParameters["grabado"]);
        }

        [Fact]
        public void La_plantilla_deja_todas_las_salidas_en_cero()
        {
            var (fabrica, contexto) = Envoltorio(new OrganizationServiceEnMemoria(), new OrganizationServiceEnMemoria(), ("plantilla", true));
            new CargarCatalogosApi().Execute(fabrica);
            foreach (var salida in new[] { "filas", "errores", "advertencias", "clientescreados", "planescreados", "correoscreados", "autorizacionescreadas" })
                Assert.Equal(0, contexto.OutputParameters[salida]);

            var (fabrica2, contexto2) = Envoltorio(new OrganizationServiceEnMemoria(), new OrganizationServiceEnMemoria(), ("plantilla", true));
            new CambiarEstadoCatalogosApi().Execute(fabrica2);
            foreach (var salida in new[] { "filas", "errores", "advertencias", "activados", "desactivados", "sincambios" })
                Assert.Equal(0, contexto2.OutputParameters[salida]);
            Assert.Equal(false, contexto2.OutputParameters["grabado"]);
        }

        [Fact]
        public void El_envoltorio_calcula_hoy_en_Nicaragua()
        {
            string conFecha(DateTime fecha) => Archivo(EstructuraDeExcelDeCatalogo.Carga,
                Fila(2, ("A", "123"), ("H", "n@empresa.com"), ("I", "N"), ("J", fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture))));
            var nicaragua = DateTime.UtcNow.AddHours(-6).Date;

            var (f1, c1) = Envoltorio(Catalogos(), Sistema(), ("archivo", conFecha(nicaragua.AddDays(-1))));
            new CargarCatalogosApi().Execute(f1);
            Assert.Equal(true, c1.OutputParameters["grabado"]);

            var (f2, c2) = Envoltorio(Catalogos(), Sistema(), ("archivo", conFecha(nicaragua.AddDays(2))));
            new CargarCatalogosApi().Execute(f2);
            Assert.Equal(false, c2.OutputParameters["grabado"]);
            Assert.Equal(1, c2.OutputParameters["errores"]);
        }

        [Fact]
        public void Sin_proveedor_de_servicios_el_envoltorio_lo_dice()
        {
            Assert.Throws<InvalidPluginExecutionException>(() => new CargarCatalogosApi().Execute(null));
            Assert.Throws<InvalidPluginExecutionException>(() => new CambiarEstadoCatalogosApi().Execute(null));
        }

        // ------------------------------------------------------------------------------------------ revisión 4c
        [Fact]
        public void El_tope_de_peso_justo_no_da_003_y_un_byte_menos_si()
        {
            var archivo = Archivo(EstructuraDeExcelDeCatalogo.Carga, Fila(2, ("A", "123")));
            var bytes = Convert.FromBase64String(archivo).Length;

            string limites(long comprimido) => @"{""maximoBytesComprimido"":" + comprimido.ToString(CultureInfo.InvariantCulture) + @",""maximoBytesDescomprimido"":20000000}";
            Assert.True(Nucleo(Catalogos(), Sistema(Parametro("lectura.limites", 1, limites(bytes)))).Cargar(archivo).Grabado);
            var ex = Assert.Throws<CargaMasivaRechazadaException>(() => Nucleo(Catalogos(), Sistema(Parametro("lectura.limites", 1, limites(bytes - 1)))).Cargar(archivo));
            Assert.Equal("MPPP-CAR-003", ex.Codigo);
        }

        [Fact]
        public void Un_texto_enorme_e_invalido_da_003_y_uno_chico_e_invalido_001_sin_decodificar_el_enorme()
        {
            var sistema = Sistema(Parametro("lectura.limites", 1, @"{""maximoBytesComprimido"":300}"));
            Assert.Equal("MPPP-CAR-003", Assert.Throws<CargaMasivaRechazadaException>(() => Nucleo(Catalogos(), sistema).Cargar(new string('A', 5000) + "!")).Codigo);
            Assert.Equal("MPPP-CAR-001", Assert.Throws<CargaMasivaRechazadaException>(() => Nucleo(Catalogos(), sistema).Cargar("AAAA!")).Codigo);
        }

        [Fact]
        public void El_motivo_del_009_se_recorta_sin_partir_un_par_subrogado_y_la_traza_lo_trae_entero()
        {
            var motivo = new string('x', 298) + "\U0001F600" + new string('y', 200);   // el par subrogado cae justo en el corte
            var falla = new ServicioQueFalla(Catalogos(), motivo);
            var (fabrica, _) = Envoltorio(null, Sistema(), ("archivo", ArchivoDeUnaFilaQueCrea()));
            fabrica.UsuarioPersonalizado = falla;
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => new CargarCatalogosApi().Execute(fabrica));

            Assert.StartsWith("MPPP-CAR-009: ", ex.Message);
            Assert.Contains("\u2026", ex.Message);
            Assert.Contains("No se grabó nada", ex.Message);
            Assert.True(ex.Message.Length < 600, ex.Message.Length.ToString(CultureInfo.InvariantCulture));
            for (var i = 0; i < ex.Message.Length; i++)
            {
                if (char.IsHighSurrogate(ex.Message[i])) Assert.True(i + 1 < ex.Message.Length && char.IsLowSurrogate(ex.Message[i + 1]), "par subrogado partido");
                if (char.IsLowSurrogate(ex.Message[i])) Assert.True(i > 0 && char.IsHighSurrogate(ex.Message[i - 1]), "par subrogado partido");
            }

            Assert.Contains(motivo, fabrica.Traza.Todo);   // el detalle completo queda en la traza
        }

        [Fact]
        public void Un_motivo_corto_del_009_no_se_toca()
        {
            var ex = Assert.Throws<CargaMasivaRechazadaException>(() => Nucleo(new ServicioQueFalla(Catalogos(), "Entity Key violated."), Sistema()).Cargar(ArchivoDeUnaFilaQueCrea()));
            Assert.Contains("Entity Key violated. No se grabó nada", ex.Message);
            Assert.DoesNotContain("\u2026", ex.Message);
        }

        [Fact]
        public void Un_error_de_Excel_en_la_columna_Resultado_no_se_lee_ni_rechaza_el_archivo()
        {
            // La columna Resultado es solo de salida (DN-04): un archivo de resultado que el usuario corrigió y vuelve a subir puede
            // traer cualquier cosa ahí, y el aviso del lector sobre esa columna no es un error de la fila.
            var e = EstructuraDeExcelDeCatalogo.Carga;
            var archivo = Convert.ToBase64String(XlsxCrudo.ConFilas(e.Hoja,
                XlsxCrudo.Fila(1, e.Columnas.Select(c => XlsxCrudo.Celda(c.Letra + "1", c.Encabezado)).ToArray()),
                XlsxCrudo.Fila(2, XlsxCrudo.Celda("A2", "123"), "<c r=\"" + e.ColumnaResultado.Letra + "2\" t=\"e\"><v>#REF!</v></c>")));
            var r = Nucleo(Catalogos(), Sistema()).Cargar(archivo);
            Assert.Equal(0, r.Errores);
            Assert.True(r.Grabado);
        }

        [Fact]
        public void Un_error_de_Excel_en_el_cambio_de_estado_no_se_graba_en_silencio()
        {
            var e = EstructuraDeExcelDeCatalogo.CambioDeEstado;
            var archivo = Convert.ToBase64String(XlsxCrudo.ConFilas(e.Hoja,
                XlsxCrudo.Fila(1, e.Columnas.Select(c => XlsxCrudo.Celda(c.Letra + "1", c.Encabezado)).ToArray()),
                XlsxCrudo.Fila(2, XlsxCrudo.Celda("A2", "CLIENTE"), XlsxCrudo.Celda("B2", "DESACTIVAR"), XlsxCrudo.Celda("C2", "123"),
                    "<c r=\"F2\" t=\"e\"><v>#N/A</v></c>")));
            var cat = Catalogos();
            var r = Nucleo(cat, Sistema()).CambiarEstado(archivo);
            Assert.False(r.Grabado);
            Assert.Equal(1, r.Errores);
            Assert.Empty(Escrituras(cat));
        }

        // ------------------------------------------------------------------------------------------ dobles
        private sealed class Fabrica : IServiceProvider, IOrganizationServiceFactory
        {
            private readonly ContextoDeEjecucionSimulado _contexto;
            private readonly IOrganizationService _usuario;
            private readonly IOrganizationService _sistema;

            public Fabrica(ContextoDeEjecucionSimulado contexto, IOrganizationService usuario, IOrganizationService sistema)
            {
                _contexto = contexto; _usuario = usuario; _sistema = sistema;
            }

            public IOrganizationService UsuarioPersonalizado { get; set; }

            public TraceSimulado Traza { get; } = new TraceSimulado();

            public object GetService(Type serviceType)
            {
                if (serviceType == typeof(IPluginExecutionContext)) return _contexto;
                if (serviceType == typeof(IOrganizationServiceFactory)) return this;
                if (serviceType == typeof(ITracingService)) return Traza;
                return null;
            }

            public IOrganizationService CreateOrganizationService(Guid? userId)
                => userId == null ? _sistema : UsuarioPersonalizado ?? _usuario;
        }

        private sealed class ServicioQueFalla : IOrganizationService
        {
            private readonly IOrganizationService _real;
            private readonly string _mensaje;

            public ServicioQueFalla(IOrganizationService real, string mensaje) { _real = real; _mensaje = mensaje; }

            private System.ServiceModel.FaultException<OrganizationServiceFault> Falla()
                => new System.ServiceModel.FaultException<OrganizationServiceFault>(new OrganizationServiceFault { Message = _mensaje }, new System.ServiceModel.FaultReason(_mensaje));

            public Guid Create(Entity entity) => throw Falla();
            public void Update(Entity entity) => throw Falla();
            public Entity Retrieve(string entityName, Guid id, Microsoft.Xrm.Sdk.Query.ColumnSet columnSet) => _real.Retrieve(entityName, id, columnSet);
            public void Delete(string entityName, Guid id) => _real.Delete(entityName, id);
            public OrganizationResponse Execute(OrganizationRequest request) => _real.Execute(request);
            public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) => _real.Associate(entityName, entityId, relationship, relatedEntities);
            public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) => _real.Disassociate(entityName, entityId, relationship, relatedEntities);
            public EntityCollection RetrieveMultiple(Microsoft.Xrm.Sdk.Query.QueryBase query) => _real.RetrieveMultiple(query);
        }
    }
}
