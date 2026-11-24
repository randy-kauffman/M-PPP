using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Mppp.Plugins.Api;
using Sanic.Mppp.Plugins.Catalogos;
using Sanic.Mppp.Plugins.Datos;
using Sanic.Mppp.Plugins.Dominio;
using Sanic.Mppp.Plugins.Exportacion;
using Sanic.Mppp.Plugins.Plantilla;
using Sanic.Mppp.Plugins.Tests.Dobles;
using Sanic.Mppp.Plugins.Validacion;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Aceptacion
{
    /// <summary>
    /// DN-06 (aprobador, 2026-11-24; diseno/DIRECTRICES-NUEVAS.md): Canal y Requiere pre-registro del plan.
    ///  - Regla nueva PLAN_REQUIERE_PREREGISTRO ("Este plan no requiere pre-registro."), que depende de la autorización; las
    ///    cinco reglas que dependían de la autorización pasan a depender de ella.
    ///  - La obligatoriedad se elige por el canal del plan: Banca en Linea usa `plantilla.obligatoriedad`, Conectividad usa
    ///    `plantilla.obligatoriedad.conectividad`, exigido SOLO cuando una fila lo necesita.
    ///  - Lectura tolerante del plan: canal vacío = Banca en Linea, pre-registro vacío = Sí.
    ///  - Carga masiva: L "Canal", M "Requiere preregistro", Resultado en N.
    /// PRUEBAS DEL ARQUITECTO: el constructor no las toca.
    /// </summary>
    public class Dn06Aceptacion
    {
        // ================================================================== el catálogo de reglas destino
        /// <summary>El catálogo de registro DESTINO de DN-06, en orden.</summary>
        internal static readonly (string codigo, string[] dependeDe)[] SemillaRegistro =
        {
            (ReglasDeRegistro.ListasValidas, new string[0]),
            (ReglasDeRegistro.LargosYFormato, new string[0]),
            (ReglasDeRegistro.CaracteresPermitidos, new string[0]),
            (ReglasDeRegistro.PlanExiste, new string[0]),
            (ReglasDeRegistro.AutorizacionCorreoPlan, new[] { ReglasDeRegistro.PlanExiste }),
            (ReglasDeRegistro.PlanRequierePreregistro, new[] { ReglasDeRegistro.AutorizacionCorreoPlan }),
            (ReglasDeRegistro.GestionDelFormato, new[] { ReglasDeRegistro.PlanRequierePreregistro, ReglasDeRegistro.ListasValidas }),
            (ReglasDeRegistro.ClasificacionDelFormato, new[] { ReglasDeRegistro.PlanRequierePreregistro, ReglasDeRegistro.ListasValidas }),
            (ReglasDeRegistro.BancoDelFormato, new[] { ReglasDeRegistro.PlanRequierePreregistro, ReglasDeRegistro.ListasValidas }),
            (ReglasDeRegistro.Obligatoriedad, new[] { ReglasDeRegistro.PlanRequierePreregistro, ReglasDeRegistro.ListasValidas }),
            (ReglasDeRegistro.MonedaDelPlan, new[] { ReglasDeRegistro.PlanRequierePreregistro, ReglasDeRegistro.ListasValidas }),
            (ReglasDeRegistro.ReferenciaFormato11, new[] { ReglasDeRegistro.ClasificacionDelFormato, ReglasDeRegistro.Obligatoriedad }),
            (ReglasDeRegistro.CuentaDelBanco, new[] { ReglasDeRegistro.BancoDelFormato }),
        };

        /// <summary>
        /// Una matriz de Conectividad DISTINTA de la v3 a propósito (la real es una copia; acá tiene que notarse cuál se usó):
        /// además de lo de la v3, tipo y número de identificación son opcionales para todos.
        /// </summary>
        internal static readonly string ObligatoriedadConectividadDePrueba = Dn05Aceptacion.ObligatoriedadV3.Replace(
            @"""opcionales"": [{""campo"":""referencia""}, {""campo"":""moneda""}]",
            @"""opcionales"": [{""campo"":""referencia""}, {""campo"":""moneda""}, {""campo"":""tipoIdentificacion""}, {""campo"":""numeroIdentificacion""}]");

        // ================================================================== el mundo de las reglas
        private const string Listas = @"{
            ""gestion"":[{""valor"":""Inclusion""},{""valor"":""Exclusion""},{""valor"":""Modificacion""}],
            ""clasificacion"":[{""valor"":""BAC""},{""valor"":""ACH""},{""valor"":""CK""}],
            ""tipoIdentificacion"":[{""valor"":""CNA""},{""valor"":""RUC""},{""valor"":""PAS""}],
            ""moneda"":[{""valor"":""COR""},{""valor"":""USD""}],
            ""banco"":[{""valor"":""LAFISE"",""codigo"":""006""},{""valor"":""BAC"",""codigo"":""007""},{""valor"":""BANPRO"",""codigo"":""008""}]
        }";

        private static readonly Guid PBanca = Guid.Parse("00000000-0000-0000-0000-000000000011");
        private static readonly Guid PConectividad = Guid.Parse("00000000-0000-0000-0000-000000000012");
        private static readonly Guid PSinPreregistro = Guid.Parse("00000000-0000-0000-0000-000000000013");
        private static readonly Guid PAjenoSinPreregistro = Guid.Parse("00000000-0000-0000-0000-000000000099");

        private static readonly Dictionary<string, string> Columnas = CamposDeFila.Todos()
            .Select((campo, i) => (campo, columna: ((char)('C' + i)).ToString()))
            .ToDictionary(x => x.campo, x => x.columna);

        private static readonly Dictionary<string, string> Encabezados = new Dictionary<string, string>
        {
            ["gestion"] = "Gestion", ["clasificacion"] = "Clasificacion", ["tipoIdentificacion"] = "Tipo ID", ["moneda"] = "Moneda", ["banco"] = "Banco",
            ["numeroPlan"] = "No. Plan", ["nombreBeneficiario"] = "Nombre", ["numeroIdentificacion"] = "No. Identificacion",
            ["numeroCuenta"] = "No. Cuenta", ["referencia"] = "Referencia",
        };

        private static ConfiguracionPlantilla Estructura()
        {
            var campos = CamposDeFila.Todos().Select(c => new CampoPlantilla { Nombre = c, Columna = Columnas[c], EncabezadoEsperado = Encabezados[c] }).ToList();
            return new ConfiguracionPlantilla { Hoja = "Datos", FilaEncabezado = 12, PrimeraFila = 13, CantidadFilas = 100, Campos = campos };
        }

        /// <summary>
        /// Cuatro planes de formato 11 en USD: 0011 Banca en Linea, 0012 Conectividad, 0013 sin pre-registro (autorizados) y
        /// 0099 sin pre-registro (NO autorizado).
        /// </summary>
        private static CatalogosDeValidacion Catalogos(bool conConectividad = true)
        {
            return new CatalogosDeValidacion(
                Estructura(),
                ListasPlantilla.DesdeJson(Listas),
                ObligatoriedadPlantilla.DesdeJson(Dn05Aceptacion.ObligatoriedadV3),
                new List<PlanDelCatalogo>
                {
                    new PlanDelCatalogo(PBanca, "0011", TipoDeFormatoDelPlan._11, Moneda.USD, null, Canal.BancaEnLinea, true),
                    new PlanDelCatalogo(PConectividad, "0012", TipoDeFormatoDelPlan._11, Moneda.USD, null, Canal.Conectividad, true),
                    new PlanDelCatalogo(PSinPreregistro, "0013", TipoDeFormatoDelPlan._11, Moneda.USD, null, Canal.BancaEnLinea, false),
                    new PlanDelCatalogo(PAjenoSinPreregistro, "0099", TipoDeFormatoDelPlan._11, Moneda.USD, null, Canal.BancaEnLinea, false),
                },
                new[] { PBanca, PConectividad, PSinPreregistro },
                formatos: FormatosDelPlan.DesdeJson(Dn05Aceptacion.FormatosDn05),
                largosDeCuenta: LargosDeCuenta.DesdeJson(Dn05Aceptacion.LargosDn05),
                obligatoriedadConectividad: conConectividad ? ObligatoriedadPlantilla.DesdeJson(ObligatoriedadConectividadDePrueba) : null);
        }

        /// <summary>Una Inclusión de formato 11 buena para el plan pedido, con los cambios de cada prueba (nulo = la celda no vino).</summary>
        private static FilaEnValidacion Fila(CatalogosDeValidacion catalogos, string plan, params (string campo, string valor)[] cambios)
        {
            var porCampo = new Dictionary<string, string>
            {
                ["gestion"] = "Inclusion", ["clasificacion"] = "ACH", ["tipoIdentificacion"] = "CNA", ["moneda"] = "USD", ["banco"] = "LAFISE",
                ["numeroPlan"] = plan, ["nombreBeneficiario"] = "Juan Perez", ["numeroIdentificacion"] = "0011234567890A", ["numeroCuenta"] = "123456789",
                ["referencia"] = "REF1",
            };
            foreach (var (campo, valor) in cambios)
            {
                porCampo[campo] = valor;
            }

            var valores = porCampo.Where(p => p.Value != null).ToDictionary(p => Columnas[p.Key], p => p.Value);
            return new FilaEnValidacion(new FilaPlantilla { NumeroFilaExcel = 15, NumeroOrden = 3, Valores = valores }, catalogos);
        }

        private static FilaEnValidacion Fila(string plan, params (string campo, string valor)[] cambios) => Fila(Catalogos(), plan, cambios);

        private static Veredicto Suelta(string codigo, FilaEnValidacion fila) => ReglasDeRegistro.Evaluadores().Single(e => e.Codigo == codigo).Evaluar(fila);

        private static IList<ResultadoDeRegla> Correr(FilaEnValidacion fila)
        {
            var reglas = SemillaRegistro.Select((r, i) => new DefinicionDeRegla
            {
                Codigo = r.codigo, Orden = (i + 1) * 10, DependeDe = r.dependeDe, Efecto = EfectoDeLaRegla.Rechaza,
            }).ToList();
            return new MotorDeReglas<FilaEnValidacion>(ReglasDeRegistro.Evaluadores()).Evaluar(reglas, fila);
        }

        private static ResultadoDeLaRegla Resultado(IEnumerable<ResultadoDeRegla> rs, string codigo) => rs.Single(r => r.Codigo == codigo).Resultado;

        // ================================================================== el plan del catálogo
        [Fact]
        public void Un_plan_armado_sin_canal_ni_preregistro_es_banca_en_linea_y_requiere_preregistro()
        {
            var plan = new PlanDelCatalogo(PBanca, "0011", TipoDeFormatoDelPlan._11, Moneda.USD);
            Assert.Equal(Canal.BancaEnLinea, plan.Canal);
            Assert.True(plan.RequierePreregistro);
        }

        [Fact]
        public void Un_plan_guarda_su_canal_y_su_preregistro()
        {
            var plan = new PlanDelCatalogo(PConectividad, "0012", TipoDeFormatoDelPlan._11, Moneda.USD, null, Canal.Conectividad, false);
            Assert.Equal((Canal.Conectividad, false), (plan.Canal, plan.RequierePreregistro));
        }

        // ================================================================== PLAN_REQUIERE_PREREGISTRO
        [Fact]
        public void La_regla_nueva_tiene_su_codigo_y_un_solo_evaluador()
        {
            Assert.Equal("PLAN_REQUIERE_PREREGISTRO", ReglasDeRegistro.PlanRequierePreregistro);
            Assert.Single(ReglasDeRegistro.Evaluadores(), e => e.Codigo == ReglasDeRegistro.PlanRequierePreregistro);
            var codigos = ReglasDeRegistro.Evaluadores().Select(e => e.Codigo).ToList();
            Assert.Equal(codigos.Count, codigos.Distinct(StringComparer.Ordinal).Count());
        }

        [Theory]
        [InlineData("11")]
        [InlineData("12")]
        public void Un_plan_que_requiere_preregistro_cumple(string plan)
        {
            Assert.Equal(ResultadoDeLaRegla.Cumplida, Suelta(ReglasDeRegistro.PlanRequierePreregistro, Fila(plan)).Resultado);
        }

        [Fact]
        public void Un_plan_que_no_requiere_preregistro_falla_con_el_texto_del_aprobador_sin_marcadores()
        {
            var v = Suelta(ReglasDeRegistro.PlanRequierePreregistro, Fila("13"));

            Assert.Equal(ResultadoDeLaRegla.NoCumplida, v.Resultado);
            Assert.Equal("Este plan no requiere pre-registro.", v.Razon);
            Assert.True(v.Marcadores == null || v.Marcadores.Count == 0, "sin marcadores: una plantilla del catálogo con {plan} no se completa (D-19)");
            Assert.DoesNotContain("0013", v.Razon);
        }

        [Fact]
        public void Sin_plan_no_da_por_bueno_el_preregistro()
        {
            Assert.Equal(ResultadoDeLaRegla.NoCumplida, Suelta(ReglasDeRegistro.PlanRequierePreregistro, Fila("8888")).Resultado);
        }

        [Fact]
        public void Con_el_catalogo_destino_un_plan_sin_preregistro_recibe_solo_ese_motivo()
        {
            // Además le falta el nombre: si OBLIGATORIEDAD se evaluara, diría "Faltan datos obligatorios".
            var rs = Correr(Fila("13", ("nombreBeneficiario", null)));

            Assert.Equal(ResultadoDeLaRegla.Cumplida, Resultado(rs, ReglasDeRegistro.AutorizacionCorreoPlan));
            Assert.Equal(ResultadoDeLaRegla.NoCumplida, Resultado(rs, ReglasDeRegistro.PlanRequierePreregistro));
            foreach (var codigo in new[]
                     {
                         ReglasDeRegistro.GestionDelFormato, ReglasDeRegistro.ClasificacionDelFormato, ReglasDeRegistro.BancoDelFormato,
                         ReglasDeRegistro.Obligatoriedad, ReglasDeRegistro.MonedaDelPlan,
                     })
            {
                Assert.Equal(ResultadoDeLaRegla.Omitida, Resultado(rs, codigo));
            }

            var fallidas = rs.Where(r => r.Resultado == ResultadoDeLaRegla.NoCumplida).Select(r => r.Codigo).ToList();
            Assert.Equal(new[] { ReglasDeRegistro.PlanRequierePreregistro }, fallidas);
        }

        [Fact]
        public void A_un_remitente_no_autorizado_no_se_le_dice_nada_del_preregistro()
        {
            // D-41: sobre un plan ajeno solo se dice que no está autorizado.
            var rs = Correr(Fila("99"));

            Assert.Equal(ResultadoDeLaRegla.NoCumplida, Resultado(rs, ReglasDeRegistro.AutorizacionCorreoPlan));
            Assert.Equal(ResultadoDeLaRegla.Omitida, Resultado(rs, ReglasDeRegistro.PlanRequierePreregistro));
        }

        [Fact]
        public void Con_el_catalogo_destino_un_plan_que_requiere_preregistro_se_valida_como_antes()
        {
            var rs = Correr(Fila("11"));
            Assert.All(rs, r => Assert.Equal(ResultadoDeLaRegla.Cumplida, r.Resultado));
        }

        // ================================================================== obligatoriedad por canal
        [Fact]
        public void Los_nombres_del_parametro_de_conectividad()
        {
            Assert.Equal("plantilla.obligatoriedad.conectividad", ObligatoriedadPlantilla.NombreParametroConectividad);
            Assert.Equal(ObligatoriedadPlantilla.NombreParametroConectividad, ValidarSolicitud.ParametroObligatoriedadConectividad);
        }

        [Fact]
        public void El_catalogo_entrega_la_matriz_de_cada_canal()
        {
            var c = Catalogos();
            Assert.Same(c.Obligatoriedad, c.ObligatoriedadPara(Canal.BancaEnLinea));
            Assert.Same(c.ObligatoriedadConectividad, c.ObligatoriedadPara(Canal.Conectividad));
            Assert.NotNull(c.ObligatoriedadConectividad);
        }

        [Fact]
        public void Sin_la_matriz_de_conectividad_pedirla_es_un_error_que_nombra_el_parametro()
        {
            var c = Catalogos(conConectividad: false);
            Assert.Null(c.ObligatoriedadConectividad);
            Assert.Same(c.Obligatoriedad, c.ObligatoriedadPara(Canal.BancaEnLinea));
            var ex = Assert.Throws<InvalidOperationException>(() => c.ObligatoriedadPara(Canal.Conectividad));
            Assert.Contains("plantilla.obligatoriedad.conectividad", ex.Message);
        }

        [Fact]
        public void Una_fila_de_un_plan_de_conectividad_usa_la_matriz_de_conectividad()
        {
            var v = Suelta(ReglasDeRegistro.Obligatoriedad, Fila("12", ("tipoIdentificacion", null), ("numeroIdentificacion", null)));
            Assert.Equal(ResultadoDeLaRegla.Cumplida, v.Resultado);
        }

        [Fact]
        public void La_misma_fila_de_un_plan_de_banca_en_linea_usa_la_matriz_de_siempre()
        {
            var v = Suelta(ReglasDeRegistro.Obligatoriedad, Fila("11", ("tipoIdentificacion", null), ("numeroIdentificacion", null)));
            Assert.Equal(ResultadoDeLaRegla.NoCumplida, v.Resultado);
            Assert.Equal("Faltan datos obligatorios: Tipo ID, No. Identificacion.", v.Razon);
        }

        [Fact]
        public void Una_fila_de_conectividad_sin_la_matriz_cargada_es_un_error_real_que_nombra_el_parametro()
        {
            var fila = Fila(Catalogos(conConectividad: false), "12");
            var ex = Assert.Throws<InvalidOperationException>(() => Suelta(ReglasDeRegistro.Obligatoriedad, fila));
            Assert.Contains("plantilla.obligatoriedad.conectividad", ex.Message);
        }

        [Fact]
        public void Sin_la_matriz_de_conectividad_una_fila_de_banca_en_linea_se_valida_igual()
        {
            var fila = Fila(Catalogos(conConectividad: false), "11");
            Assert.Equal(ResultadoDeLaRegla.Cumplida, Suelta(ReglasDeRegistro.Obligatoriedad, fila).Resultado);
        }

        // ================================================================== lectura tolerante de los planes (capa de datos)
        private static Entity PlanEntidad(string codigo, params (string atributo, object valor)[] extra)
        {
            var e = new Entity(Tablas.Plan)
            {
                ["statecode"] = new OptionSetValue(Tablas.Activo), ["sanic_codigo"] = codigo,
                ["sanic_tipoformato"] = new OptionSetValue((int)TipoDeFormatoDelPlan._11), ["sanic_moneda"] = new OptionSetValue((int)Moneda.USD),
            };
            foreach (var (a, v) in extra)
            {
                e[a] = v;
            }

            return e;
        }

        [Fact]
        public void Los_planes_activos_traen_canal_y_preregistro_y_lo_vacio_se_lee_con_los_valores_por_defecto_en_una_consulta()
        {
            var svc = new OrganizationServiceEnMemoria();
            var conectividad = svc.Sembrar(PlanEntidad("0012", ("sanic_canal", new OptionSetValue((int)Canal.Conectividad)), ("sanic_requierepreregistro", false)));
            var vacio = svc.Sembrar(PlanEntidad("0011"));
            var nulos = svc.Sembrar(PlanEntidad("0014", ("sanic_canal", null), ("sanic_requierepreregistro", null)));

            var planes = new CatalogosDataverse(svc).PlanesActivosPorCodigo(new[] { "0011", "0012", "0014" });

            Assert.Equal(1, svc.Llamadas.Count);
            Assert.Equal((Canal.Conectividad, false), planes.Where(p => p.Id == conectividad).Select(p => (p.Canal, p.RequierePreregistro)).Single());
            Assert.Equal((Canal.BancaEnLinea, true), planes.Where(p => p.Id == vacio).Select(p => (p.Canal, p.RequierePreregistro)).Single());
            Assert.Equal((Canal.BancaEnLinea, true), planes.Where(p => p.Id == nulos).Select(p => (p.Canal, p.RequierePreregistro)).Single());
        }

        [Fact]
        public void Un_canal_fuera_del_choice_es_un_error_real_que_nombra_la_columna()
        {
            var svc = new OrganizationServiceEnMemoria();
            var id = svc.Sembrar(PlanEntidad("0011", ("sanic_canal", new OptionSetValue(999))));
            var ex = Assert.Throws<InvalidOperationException>(() => new CatalogosDataverse(svc).PlanesActivosPorCodigo(new[] { "0011" }));
            Assert.Contains("sanic_canal", ex.Message);
            Assert.Contains(id.ToString(), ex.Message);
        }

        // ================================================================== carga masiva: columnas
        [Fact]
        public void La_carga_suma_canal_en_L_y_requiere_preregistro_en_M_al_final()
        {
            var reales = ColumnasDeCarga.Todas.Select(c => (c.Letra, c.Campo, c.Encabezado)).ToArray();
            Assert.Equal(13, reales.Length);
            Assert.Equal(("L", "canal", "Canal"), reales[11]);
            Assert.Equal(("M", "requierePreregistro", "Requiere preregistro"), reales[12]);
            Assert.Equal(("K", "comentarios", "Comentarios"), reales[10]); // las de antes no se mueven
            Assert.Equal("canal", ColumnasDeCarga.Canal);
            Assert.Equal("requierePreregistro", ColumnasDeCarga.RequierePreregistro);
        }

        [Fact]
        public void El_resultado_pasa_a_la_columna_N()
        {
            Assert.Equal("N", EstructuraDeExcelDeCatalogo.Carga.ColumnaResultado.Letra);
            Assert.Equal(14, EstructuraDeExcelDeCatalogo.Carga.Columnas.Count);
        }

        // ================================================================== carga masiva: planificador
        private static readonly DateTime Hoy = new DateTime(2026, 11, 24);
        private static readonly Guid ClienteSol = new Guid(1, 0, 0, new byte[8]);
        private static readonly Guid Ana = new Guid(10, 0, 0, new byte[8]);
        private static readonly Guid PlanSol = new Guid(20, 0, 0, new byte[8]);      // 0A12, Banca en Linea, Sí
        private static readonly Guid PlanSolCon = new Guid(21, 0, 0, new byte[8]);   // 0A13, Conectividad, No
        private static readonly Guid PlanSolViejo = new Guid(22, 0, 0, new byte[8]); // 0A14, armado sin canal ni preregistro (valores por defecto)

        private static EstadoDeCatalogos Foto()
        {
            return new EstadoDeCatalogos(
                new[] { new ClienteExistente(ClienteSol, "000000123", "ABC123456001", "Ferretería Sol", Ana, true) },
                new[]
                {
                    new PlanExistente(PlanSol, "0A12", ClienteSol, TipoDeFormatoDelPlan._06, Moneda.COR, true, Canal.BancaEnLinea, true),
                    new PlanExistente(PlanSolCon, "0A13", ClienteSol, TipoDeFormatoDelPlan._06, Moneda.COR, true, Canal.Conectividad, false),
                    new PlanExistente(PlanSolViejo, "0A14", ClienteSol, TipoDeFormatoDelPlan._06, Moneda.COR, true),
                },
                new AutorizadoExistente[0],
                new AutorizacionClienteExistente[0],
                new[] { new UsuarioCandidato(Ana, "ana@banco.com", true, true) });
        }

        private static FilaPlantilla F(int numero, params (string letra, string valor)[] valores)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (letra, valor) in valores) d[letra] = valor;
            return new FilaPlantilla { NumeroFilaExcel = numero, NumeroOrden = numero - 1, Valores = d };
        }

        /// <summary>Plan nuevo 00N1 del cliente existente Sol, formato 06 en COR, con lo que se pase en L y M.</summary>
        private static FilaPlantilla PlanNuevo(int numero, string canal, string preregistro = null, string codigo = "N1")
        {
            var valores = new List<(string, string)> { ("A", "123"), ("E", codigo), ("F", "06"), ("G", "COR") };
            if (canal != null) valores.Add(("L", canal));
            if (preregistro != null) valores.Add(("M", preregistro));
            return F(numero, valores.ToArray());
        }

        private static PlanDeCarga Planificar(params FilaPlantilla[] filas) => PlanificadorDeCarga.Planificar(filas, Foto(), Hoy);

        private static void SinErrores(PlanDeCarga plan) => Assert.False(plan.TieneErrores, string.Join(" || ", plan.Filas.SelectMany(f => f.Errores)));

        private static void UnErrorQueEmpiezaCon(PlanDeCarga plan, string prefijo)
        {
            Assert.True(plan.TieneErrores);
            Assert.Contains(plan.Filas.SelectMany(f => f.Errores), e => e.StartsWith(prefijo, StringComparison.Ordinal));
        }

        [Fact]
        public void Un_plan_nuevo_se_crea_con_el_canal_y_el_preregistro_de_la_fila()
        {
            var plan = Planificar(PlanNuevo(2, "Conectividad", "No"));
            SinErrores(plan);
            var nuevo = Assert.Single(plan.Planes);
            Assert.Equal(("00N1", Canal.Conectividad, false), (nuevo.Codigo, nuevo.Canal, nuevo.RequierePreregistro));
        }

        [Theory]
        [InlineData("Banca en Linea", null, Canal.BancaEnLinea, true)]
        [InlineData("  banca en línea ", "", Canal.BancaEnLinea, true)]
        [InlineData("BANCA EN LINEA", "si", Canal.BancaEnLinea, true)]
        [InlineData("conectividad", "Sí", Canal.Conectividad, true)]
        [InlineData("Conectividad", "NO", Canal.Conectividad, false)]
        public void Canal_y_preregistro_se_leen_sin_distinguir_caja_ni_tildes_y_preregistro_vacio_es_si(string canal, string preregistro, Canal esperado, bool requiere)
        {
            var plan = Planificar(PlanNuevo(2, canal, preregistro));
            SinErrores(plan);
            var nuevo = Assert.Single(plan.Planes);
            Assert.Equal((esperado, requiere), (nuevo.Canal, nuevo.RequierePreregistro));
        }

        [Fact]
        public void Un_plan_nuevo_sin_canal_no_se_crea()
        {
            var plan = Planificar(PlanNuevo(2, null, "Si"));
            UnErrorQueEmpiezaCon(plan, "Canal: hace falta para crear el plan 00N1.");
            Assert.Empty(plan.Planes);
        }

        [Fact]
        public void Un_canal_que_no_existe_es_error_de_la_fila()
        {
            UnErrorQueEmpiezaCon(Planificar(PlanNuevo(2, "Satelite")), "Canal: \"Satelite\" no es un canal válido.");
        }

        [Fact]
        public void Un_preregistro_que_no_es_si_ni_no_es_error_de_la_fila()
        {
            UnErrorQueEmpiezaCon(Planificar(PlanNuevo(2, "Conectividad", "Tal vez")), "Requiere preregistro: \"Tal vez\" no es un valor válido.");
        }

        [Fact]
        public void Canal_o_preregistro_sin_codigo_de_plan_es_error_de_la_fila()
        {
            UnErrorQueEmpiezaCon(Planificar(F(2, ("A", "123"), ("L", "Conectividad"))), "Canal: viene sin Codigo plan");
            UnErrorQueEmpiezaCon(Planificar(F(2, ("A", "123"), ("M", "No"))), "Requiere preregistro: viene sin Codigo plan");
        }

        [Fact]
        public void Un_plan_nuevo_con_dos_canales_distintos_en_el_archivo_es_error()
        {
            var plan = Planificar(PlanNuevo(2, "Conectividad"), PlanNuevo(3, "Banca en Linea"));
            UnErrorQueEmpiezaCon(plan, "Canal: el plan 00N1 aparece con otro canal en");
            Assert.Empty(plan.Planes);
        }

        [Fact]
        public void Un_plan_nuevo_con_dos_preregistros_distintos_en_el_archivo_es_error()
        {
            var plan = Planificar(PlanNuevo(2, "Conectividad", "No"), PlanNuevo(3, "Conectividad", "Si"));
            UnErrorQueEmpiezaCon(plan, "Requiere preregistro: el plan 00N1 aparece con otro valor en");
            Assert.Empty(plan.Planes);
        }

        [Theory]
        [InlineData("0A12", null, null)]
        [InlineData("0A12", "Banca en Linea", "Si")]
        [InlineData("0A13", "Conectividad", "No")]
        [InlineData("0A14", "Banca en Linea", "Si")] // grabado sin valores: se lee Banca en Linea y Sí
        public void Sobre_un_plan_existente_lo_vacio_o_lo_grabado_pasa(string codigo, string canal, string preregistro)
        {
            var valores = new List<(string, string)> { ("A", "123"), ("E", codigo) };
            if (canal != null) valores.Add(("L", canal));
            if (preregistro != null) valores.Add(("M", preregistro));
            var plan = Planificar(F(2, valores.ToArray()));
            SinErrores(plan);
            Assert.Empty(plan.Planes);
        }

        [Fact]
        public void La_carga_no_cambia_el_canal_de_un_plan_existente()
        {
            UnErrorQueEmpiezaCon(Planificar(F(2, ("A", "123"), ("E", "0A12"), ("L", "Conectividad"))),
                "Canal: el plan 0A12 está grabado con canal Banca en Linea, no Conectividad. La carga no modifica planes que ya existen");
        }

        [Fact]
        public void La_carga_no_cambia_el_preregistro_de_un_plan_existente()
        {
            UnErrorQueEmpiezaCon(Planificar(F(2, ("A", "123"), ("E", "0A13"), ("M", "Si"))),
                "Requiere preregistro: el plan 0A13 está grabado con No, no Si. La carga no modifica planes que ya existen");
        }

        // ================================================================== carga masiva: datos
        [Fact]
        public void El_escritor_graba_canal_y_preregistro_del_plan_nuevo()
        {
            var plan = Planificar(PlanNuevo(2, "Conectividad", "No"), PlanNuevo(3, "Banca en Linea", null, "N2"));
            SinErrores(plan);
            var svc = new OrganizationServiceEnMemoria();

            new EscritorDeCatalogos(svc, () => TimeSpan.Zero, TimeSpan.FromSeconds(100)).Aplicar(plan);

            var planes = svc.Registros(Tablas.Plan);
            var n1 = planes.Single(p => (string)p["sanic_codigo"] == "00N1");
            Assert.Equal((int)Canal.Conectividad, ((OptionSetValue)n1["sanic_canal"]).Value);
            Assert.False((bool)n1["sanic_requierepreregistro"]);
            var n2 = planes.Single(p => (string)p["sanic_codigo"] == "00N2");
            Assert.Equal((int)Canal.BancaEnLinea, ((OptionSetValue)n2["sanic_canal"]).Value);
            Assert.True((bool)n2["sanic_requierepreregistro"]);
        }

        [Fact]
        public void La_foto_de_la_carga_lee_canal_y_preregistro_con_los_valores_por_defecto_para_lo_vacio()
        {
            var cat = new OrganizationServiceEnMemoria();
            var sol = cat.Sembrar(new Entity(Tablas.Cliente)
            {
                ["statecode"] = new OptionSetValue(Tablas.Activo), ["sanic_cifbac"] = "000000123", ["sanic_cifcom"] = "ABC123456001",
                ["sanic_nombre"] = "Ferretería Sol", ["ownerid"] = new EntityReference("systemuser", Ana),
            });
            Entity Plan(string codigo, params (string, object)[] extra)
            {
                var e = PlanEntidad(codigo, extra);
                e["sanic_clienteid"] = new EntityReference(Tablas.Cliente, sol);
                return e;
            }

            var con = cat.Sembrar(Plan("0A13", ("sanic_canal", new OptionSetValue((int)Canal.Conectividad)), ("sanic_requierepreregistro", false)));
            var viejo = cat.Sembrar(Plan("0A14"));

            var claves = ClavesDeCarga.Extraer(new[] { F(2, ("A", "123"), ("E", "A13")), F(3, ("A", "123"), ("E", "A14")) });
            var foto = new FotoDeCatalogosDataverse(cat, new OrganizationServiceEnMemoria()).ParaCarga(claves);

            Assert.Equal((Canal.Conectividad, false), foto.Planes.Where(p => p.Id == con).Select(p => (p.Canal, p.RequierePreregistro)).Single());
            Assert.Equal((Canal.BancaEnLinea, true), foto.Planes.Where(p => p.Id == viejo).Select(p => (p.Canal, p.RequierePreregistro)).Single());
        }
    }
}
