using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Sanic.Mppp.Plugins.Catalogos;
using Sanic.Mppp.Plugins.Dominio;
using Sanic.Mppp.Plugins.Plantilla;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Aceptacion
{
    /// <summary>
    /// DN-04 (diseno/DIRECTRICES-NUEVAS.md), Excel 1 "CargaCatalogos": el planificador de la carga masiva (pasada 1).
    /// Dominio puro: filas ya leídas + foto de lo que existe → qué le pasa a cada fila y qué se crea.
    /// Pruebas del ARQUITECTO: el constructor no las cambia; si una le parece imposible o contradictoria, para y reporta.
    /// </summary>
    public class CargaDeCatalogosAceptacion
    {
        // ================================================================================================ armado
        private static readonly DateTime Hoy = new DateTime(2026, 10, 1);

        private static Guid G(int n) => new Guid(n, 0, 0, new byte[8]);

        // Ids fijos de la foto
        private static readonly Guid ClienteSol = G(1);      // 000000123 / ABC123456001 / Ferretería Sol, dueño Ana, activo
        private static readonly Guid ClienteLuna = G(2);     // 000000456 / XYZ987654002 / Distribuidora Luna, dueño Ana, activo
        private static readonly Guid ClienteInactivo = G(3); // 000000789 / INA000000003 / Cliente Dormido, inactivo
        private static readonly Guid Ana = G(10);            // ana@banco.com, habilitada, ejecutiva
        private static readonly Guid Beto = G(11);           // beto@banco.com, habilitado, ejecutivo
        private static readonly Guid PlanSol = G(20);        // 0A12 de Sol, 06, COR, activo
        private static readonly Guid PlanLuna = G(21);       // 0B34 de Luna, 10, USD, activo
        private static readonly Guid PlanInactivo = G(22);   // 0C56 de Sol, 11, USD, inactivo
        private static readonly Guid Juan = G(30);           // juan.perez@empresa.com "Juan Pérez", activo
        private static readonly Guid Maria = G(31);          // maria.lopez@empresa.com "María López", inactivo
        private static readonly Guid AutJuanSol = G(40);     // Juan → Sol, activa, 2024-10-01, "Firmado en sucursal"
        private static readonly Guid AutJuanLuna = G(41);    // Juan → Luna, INACTIVA

        private static EstadoDeCatalogos Foto(IEnumerable<UsuarioCandidato> usuariosExtra = null)
        {
            var usuarios = new List<UsuarioCandidato>
            {
                new UsuarioCandidato(Ana, "ana@banco.com", true, true),
                new UsuarioCandidato(Beto, "Beto@Banco.com", true, true),
                new UsuarioCandidato(G(12), "doble@banco.com", true, true),
                new UsuarioCandidato(G(13), "doble@banco.com", true, true),
                new UsuarioCandidato(G(14), "baja@banco.com", false, true),
                new UsuarioCandidato(G(15), "sinrol@banco.com", true, false),
            };
            if (usuariosExtra != null) usuarios.AddRange(usuariosExtra);
            return new EstadoDeCatalogos(
                new[]
                {
                    new ClienteExistente(ClienteSol, "000000123", "ABC123456001", "Ferretería Sol", Ana, true),
                    new ClienteExistente(ClienteLuna, "000000456", "XYZ987654002", "Distribuidora Luna", Ana, true),
                    new ClienteExistente(ClienteInactivo, "000000789", "INA000000003", "Cliente Dormido", Ana, false),
                },
                new[]
                {
                    new PlanExistente(PlanSol, "0A12", ClienteSol, TipoDeFormatoDelPlan._06, Moneda.COR, true),
                    new PlanExistente(PlanLuna, "0B34", ClienteLuna, TipoDeFormatoDelPlan._10, Moneda.USD, true),
                    new PlanExistente(PlanInactivo, "0C56", ClienteSol, TipoDeFormatoDelPlan._11, Moneda.USD, false),
                },
                new[]
                {
                    new AutorizadoExistente(Juan, "juan.perez@empresa.com", "Juan Pérez", true),
                    new AutorizadoExistente(Maria, "maria.lopez@empresa.com", "María López", false),
                },
                new[]
                {
                    new AutorizacionClienteExistente(AutJuanSol, Juan, ClienteSol, new DateTime(2024, 10, 1), "Firmado en sucursal", true),
                    new AutorizacionClienteExistente(AutJuanLuna, Juan, ClienteLuna, null, null, false),
                },
                usuarios);
        }

        /// <summary>Una fila con valores por LETRA de columna, como los deja LectorOpenXml.</summary>
        private static FilaPlantilla F(int numero, params (string letra, string valor)[] valores)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (letra, valor) in valores) d[letra] = valor;
            return new FilaPlantilla { NumeroFilaExcel = numero, NumeroOrden = numero - 1, Valores = d };
        }

        // Atajos de columnas
        private static (string, string) A(string v) => ("A", v);   // CIF BAC
        private static (string, string) B(string v) => ("B", v);   // CIF COM
        private static (string, string) C(string v) => ("C", v);   // Razon social
        private static (string, string) D(string v) => ("D", v);   // Correo ejecutivo
        private static (string, string) E(string v) => ("E", v);   // Codigo plan
        private static (string, string) Fo(string v) => ("F", v);  // Tipo formato
        private static (string, string) Mo(string v) => ("G", v);  // Moneda
        private static (string, string) H(string v) => ("H", v);   // Correo
        private static (string, string) I(string v) => ("I", v);   // Nombre contacto
        private static (string, string) J(string v) => ("J", v);   // Fecha documento
        private static (string, string) K(string v) => ("K", v);   // Comentarios
        private static (string, string) Ca(string v) => ("L", v);  // Canal (DN-06)
        private static (string, string) Pr(string v) => ("M", v);  // Requiere preregistro (DN-06)

        /// <summary>Cliente nuevo completo (A, B, C, D) con CIF BAC = <paramref name="cifBac"/>.</summary>
        private static (string, string)[] ClienteNuevo(string cifBac = "555", string cifCom = "NUE000000555", string razon = "Comercial Nueva", string ejecutivo = "ana@banco.com")
            => new[] { A(cifBac), B(cifCom), C(razon), D(ejecutivo) };

        private static PlanDeCarga Planificar(params FilaPlantilla[] filas) => PlanificadorDeCarga.Planificar(filas, Foto(), Hoy);

        private static ResultadoDeFilaDeCarga Unica(PlanDeCarga plan) => Assert.Single(plan.Filas);

        private static void UnErrorQueEmpiezaCon(ResultadoDeFilaDeCarga fila, string prefijo)
        {
            Assert.Contains(fila.Errores, e => e.StartsWith(prefijo, StringComparison.Ordinal));
        }

        private static void SinErrores(PlanDeCarga plan)
        {
            Assert.False(plan.TieneErrores, string.Join(" || ", plan.Filas.SelectMany(f => f.Errores)));
        }

        private static string Serial(DateTime fecha) => fecha.ToOADate().ToString(CultureInfo.InvariantCulture);

        // ================================================================================================ estructura fija
        [Fact]
        public void Las_trece_columnas_A_a_M_con_sus_encabezados_exactos()
        {
            var esperado = new[]
            {
                ("A", "cifBac", "CIF BAC"), ("B", "cifCom", "CIF COM"), ("C", "razonSocial", "Razon social"),
                ("D", "correoEjecutivo", "Correo ejecutivo"), ("E", "codigoPlan", "Codigo plan"), ("F", "tipoFormato", "Tipo formato"),
                ("G", "moneda", "Moneda"), ("H", "correo", "Correo"), ("I", "nombreContacto", "Nombre contacto"),
                ("J", "fechaDocumento", "Fecha documento"), ("K", "comentarios", "Comentarios"),
                ("L", "canal", "Canal"), ("M", "requierePreregistro", "Requiere preregistro"), // DN-06
            };
            var reales = ColumnasDeCarga.Todas.Select(c => (c.Letra, c.Campo, c.Encabezado)).ToArray();
            Assert.Equal(esperado, reales);
            Assert.Equal("CargaCatalogos", ColumnasDeCarga.Hoja);
        }

        [Fact]
        public void La_lista_de_columnas_no_se_puede_modificar_desde_afuera()
        {
            var lista = ColumnasDeCarga.Todas;
            Assert.Throws<NotSupportedException>(() => ((ICollection<ColumnaDeCatalogo>)lista).Add(new ColumnaDeCatalogo("N", "x", "X")));
            Assert.Equal(13, ColumnasDeCarga.Todas.Count);
        }

        // ================================================================================================ argumentos
        [Fact]
        public void Filas_o_estado_nulos_son_error_de_quien_llama()
        {
            Assert.Throws<ArgumentNullException>(() => PlanificadorDeCarga.Planificar(null, Foto(), Hoy));
            Assert.Throws<ArgumentNullException>(() => PlanificadorDeCarga.Planificar(new FilaPlantilla[0], null, Hoy));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void Una_fila_nula_en_cualquier_posicion_de_varias_es_error_de_quien_llama(int posicion)
        {
            var filas = new List<FilaPlantilla> { F(2, A("123")), F(3, A("456")) };
            filas.Insert(posicion, null);
            Assert.Throws<ArgumentException>(() => PlanificadorDeCarga.Planificar(filas, Foto(), Hoy));
        }

        [Fact]
        public void Dos_filas_con_el_mismo_numero_de_fila_de_Excel_es_error_de_quien_llama()
        {
            Assert.Throws<ArgumentException>(() => Planificar(F(2, A("123")), F(3, A("456")), F(2, A("456"))));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void La_foto_no_admite_colecciones_nulas(int cual)
        {
            Assert.ThrowsAny<ArgumentException>(() => new EstadoDeCatalogos(
                cual == 0 ? null : new ClienteExistente[0],
                cual == 1 ? null : new PlanExistente[0],
                cual == 2 ? null : new AutorizadoExistente[0],
                cual == 3 ? null : new AutorizacionClienteExistente[0],
                cual == 4 ? null : new UsuarioCandidato[0]));
        }

        [Fact]
        public void La_foto_no_admite_elementos_nulos_en_medio_de_varios()
        {
            var c1 = new ClienteExistente(G(91), "000000001", "AAA000000001", "Uno", Ana, true);
            var c2 = new ClienteExistente(G(92), "000000002", "AAA000000002", "Dos", Ana, true);
            Assert.ThrowsAny<ArgumentException>(() => new EstadoDeCatalogos(
                new[] { c1, null, c2 }, new PlanExistente[0], new AutorizadoExistente[0], new AutorizacionClienteExistente[0], new UsuarioCandidato[0]));
            var u1 = new UsuarioCandidato(G(93), "x@b.com", true, true);
            var u2 = new UsuarioCandidato(G(94), "y@b.com", true, true);
            Assert.ThrowsAny<ArgumentException>(() => new EstadoDeCatalogos(
                new ClienteExistente[0], new PlanExistente[0], new AutorizadoExistente[0], new AutorizacionClienteExistente[0], new[] { u1, null, u2 }));
        }

        [Fact]
        public void La_foto_copia_las_colecciones_y_no_cambia_si_cambia_la_lista_original()
        {
            var clientes = new List<ClienteExistente> { new ClienteExistente(G(91), "000000001", "AAA000000001", "Uno", Ana, true) };
            var foto = new EstadoDeCatalogos(clientes, new PlanExistente[0], new AutorizadoExistente[0], new AutorizacionClienteExistente[0], new UsuarioCandidato[0]);
            clientes.Clear();
            Assert.Single(foto.Clientes);
            Assert.Throws<NotSupportedException>(() => ((ICollection<ClienteExistente>)foto.Clientes).Clear());
        }

        [Fact]
        public void Cero_filas_no_es_error_del_planificador_y_no_crea_nada()
        {
            var plan = Planificar();
            Assert.False(plan.TieneErrores);
            Assert.Empty(plan.Filas);
            Assert.Empty(plan.Clientes); Assert.Empty(plan.Planes); Assert.Empty(plan.Autorizados); Assert.Empty(plan.Autorizaciones);
        }

        [Fact]
        public void Clave_ausente_valor_nulo_blanco_y_Valores_nulo_equivalen_a_celda_vacia()
        {
            var sinValores = new FilaPlantilla { NumeroFilaExcel = 2, NumeroOrden = 1, Valores = null };
            var conNulos = F(3, A(null), B("   "), C(""));
            var plan = Planificar(sinValores, conNulos);
            UnErrorQueEmpiezaCon(plan.Filas[0], "CIF BAC / CIF COM: ");
            UnErrorQueEmpiezaCon(plan.Filas[1], "CIF BAC / CIF COM: ");
        }

        // ================================================================================================ cliente
        [Fact]
        public void Sin_CIF_BAC_ni_CIF_COM_la_fila_es_error()
        {
            var fila = Unica(Planificar(F(2, E("0A12"), H("juan.perez@empresa.com"))));
            UnErrorQueEmpiezaCon(fila, "CIF BAC / CIF COM: ");
        }

        [Fact]
        public void El_cliente_se_encuentra_por_CIF_BAC_normalizado_y_se_autoriza_el_correo_existente()
        {
            var plan = Planificar(F(2, A(" 123 "), H("JUAN.PEREZ@empresa.com "), K("x")));
            // Juan → Sol ya existe ACTIVA, pero K "x" difiere del comentario grabado: error, no se pisa.
            UnErrorQueEmpiezaCon(Unica(plan), "Comentarios: ");

            var plan2 = Planificar(F(2, A("456"), H("nuevo@empresa.com"), I("Nuevo Contacto")));
            SinErrores(plan2);
            var aut = Assert.Single(plan2.Autorizaciones);
            Assert.Equal(ClienteLuna, aut.Cliente.IdExistente);
            Assert.Null(aut.Cliente.CifBacNuevo);
            Assert.Equal("nuevo@empresa.com", aut.Autorizado.CorreoNuevo);
        }

        [Fact]
        public void El_cliente_se_encuentra_por_CIF_COM_en_minuscula()
        {
            var plan = Planificar(F(2, B("xyz987654002"), H("nuevo@empresa.com"), I("Nuevo")));
            SinErrores(plan);
            Assert.Equal(ClienteLuna, Assert.Single(plan.Autorizaciones).Cliente.IdExistente);
            Assert.Empty(plan.Clientes);
        }

        [Fact]
        public void CIF_BAC_y_CIF_COM_de_dos_clientes_distintos_es_error_que_nombra_a_los_dos()
        {
            var fila = Unica(Planificar(F(2, A("123"), B("XYZ987654002"))));
            var error = Assert.Single(fila.Errores, e => e.StartsWith("CIF BAC / CIF COM: ", StringComparison.Ordinal));
            Assert.Contains("Ferretería Sol", error);
            Assert.Contains("Distribuidora Luna", error);
        }

        [Fact]
        public void Un_CIF_encuentra_un_cliente_y_el_otro_no_existe_es_error()
        {
            UnErrorQueEmpiezaCon(Unica(Planificar(F(2, A("123"), B("NOE000000999")))), "CIF BAC / CIF COM: ");
            UnErrorQueEmpiezaCon(Unica(Planificar(F(2, A("999999"), B("ABC123456001")))), "CIF BAC / CIF COM: ");
        }

        [Theory]
        [InlineData("12A")]
        [InlineData("1234567890")]
        public void CIF_BAC_con_formato_invalido_es_error_de_esa_columna(string valor)
        {
            UnErrorQueEmpiezaCon(Unica(Planificar(F(2, A(valor)))), "CIF BAC: ");
        }

        [Theory]
        [InlineData("ABC12")]
        [InlineData("ABC12345600X")]
        public void CIF_COM_con_formato_invalido_es_error_de_esa_columna(string valor)
        {
            UnErrorQueEmpiezaCon(Unica(Planificar(F(2, B(valor)))), "CIF COM: ");
        }

        [Fact]
        public void Cliente_nuevo_completo_se_crea_normalizado_con_el_ejecutivo_como_dueno()
        {
            var plan = Planificar(F(2, A("555"), B("nue000000555"), C("  Comercial Nueva  "), D("  ANA@banco.com ")));
            SinErrores(plan);
            var nuevo = Assert.Single(plan.Clientes);
            Assert.Equal("000000555", nuevo.CifBac);
            Assert.Equal("NUE000000555", nuevo.CifCom);
            Assert.Equal("Comercial Nueva", nuevo.RazonSocial);
            Assert.Equal(Ana, nuevo.DuenoId);
            Assert.Equal(2, nuevo.FilaOrigen);
            Assert.Equal(new[] { "cliente" }, Unica(plan).Creados);
            Assert.Equal("Creado: cliente", Unica(plan).Texto);
        }

        [Fact]
        public void Cliente_nuevo_incompleto_nombra_cada_columna_que_falta()
        {
            UnErrorQueEmpiezaCon(Unica(Planificar(F(2, A("555"), B("NUE000000555"), D("ana@banco.com")))), "Razon social: ");
            UnErrorQueEmpiezaCon(Unica(Planificar(F(2, A("555"), B("NUE000000555"), C("Comercial Nueva")))), "Correo ejecutivo: ");
            UnErrorQueEmpiezaCon(Unica(Planificar(F(2, A("555"), C("Comercial Nueva"), D("ana@banco.com")))), "CIF COM: ");
            UnErrorQueEmpiezaCon(Unica(Planificar(F(2, B("NUE000000555"), C("Comercial Nueva"), D("ana@banco.com")))), "CIF BAC: ");

            var todo = Unica(Planificar(F(2, A("555"))));
            UnErrorQueEmpiezaCon(todo, "CIF COM: ");
            UnErrorQueEmpiezaCon(todo, "Razon social: ");
            UnErrorQueEmpiezaCon(todo, "Correo ejecutivo: ");
        }

        [Fact]
        public void El_ejecutivo_tiene_que_ser_exactamente_un_usuario_habilitado_con_el_rol_y_cada_caso_tiene_su_motivo()
        {
            string Motivo(string correo)
            {
                var fila = Unica(Planificar(F(2, ClienteNuevo(ejecutivo: correo))));
                return Assert.Single(fila.Errores, e => e.StartsWith("Correo ejecutivo: ", StringComparison.Ordinal));
            }

            var noExiste = Motivo("nadie@banco.com");
            var varios = Motivo("doble@banco.com");
            var deshabilitado = Motivo("baja@banco.com");
            var sinRol = Motivo("sinrol@banco.com");
            Assert.Equal(4, new[] { noExiste, varios, deshabilitado, sinRol }.Distinct().Count());

            var invalido = Unica(Planificar(F(2, ClienteNuevo(ejecutivo: "no-es-correo"))));
            UnErrorQueEmpiezaCon(invalido, "Correo ejecutivo: ");
        }

        [Fact]
        public void El_correo_del_ejecutivo_se_compara_sin_mayusculas_ni_espacios_a_los_lados()
        {
            var plan = Planificar(F(2, ClienteNuevo(ejecutivo: "  beto@BANCO.com ")));
            SinErrores(plan);
            Assert.Equal(Beto, Assert.Single(plan.Clientes).DuenoId);
        }

        [Fact]
        public void Cliente_existente_con_otra_razon_social_es_error_y_no_se_pisa()
        {
            UnErrorQueEmpiezaCon(Unica(Planificar(F(2, A("123"), C("Ferreteria Sol")))), "Razon social: "); // sin tilde: distinta
            SinErrores(Planificar(F(2, A("123"), C("  FERRETERÍA SOL "))));                                  // mayúsculas y espacios: igual
        }

        [Fact]
        public void Cliente_existente_con_otro_ejecutivo_es_error_y_con_el_mismo_no()
        {
            UnErrorQueEmpiezaCon(Unica(Planificar(F(2, A("123"), D("beto@banco.com")))), "Correo ejecutivo: ");
            UnErrorQueEmpiezaCon(Unica(Planificar(F(2, A("123"), D("nadie@banco.com")))), "Correo ejecutivo: ");
            SinErrores(Planificar(F(2, A("123"), D("ana@banco.com"))));
        }

        [Fact]
        public void Cliente_existente_inactivo_es_advertencia_no_error()
        {
            var plan = Planificar(F(2, A("789"), H("nuevo@empresa.com"), I("Nuevo")));
            SinErrores(plan);
            Assert.NotEmpty(Unica(plan).Advertencias);
            Assert.Contains(" | Advertencia: ", Unica(plan).Texto);
        }

        [Fact]
        public void Razon_social_de_mas_de_200_es_error_y_de_200_no()
        {
            var de200 = new string('R', 200);
            var de201 = new string('R', 201);
            SinErrores(Planificar(F(2, ClienteNuevo(razon: de200))));
            UnErrorQueEmpiezaCon(Unica(Planificar(F(2, ClienteNuevo(razon: de201)))), "Razon social: ");
        }

        // ================================================================================================ plan
        [Fact]
        public void Tipo_formato_o_moneda_sin_codigo_de_plan_es_error()
        {
            UnErrorQueEmpiezaCon(Unica(Planificar(F(2, A("123"), Fo("06")))), "Tipo formato: ");
            UnErrorQueEmpiezaCon(Unica(Planificar(F(2, A("123"), Mo("COR")))), "Moneda: ");
        }

        [Fact]
        public void Plan_nuevo_de_cliente_existente_se_crea_normalizado()
        {
            var plan = Planificar(F(2, A("123"), E("a1"), Fo("6"), Mo(" usd ")));
            SinErrores(plan);
            var p = Assert.Single(plan.Planes);
            Assert.Equal("00A1", p.Codigo);
            Assert.Equal(TipoDeFormatoDelPlan._06, p.Formato);
            Assert.Equal(Moneda.USD, p.Moneda);
            Assert.Equal(ClienteSol, p.Cliente.IdExistente);
            Assert.Equal(new[] { "plan" }, Unica(plan).Creados);
        }

        [Theory]
        [InlineData("06", TipoDeFormatoDelPlan._06)]
        [InlineData("6", TipoDeFormatoDelPlan._06)]
        [InlineData(" 10 ", TipoDeFormatoDelPlan._10)]
        [InlineData("11", TipoDeFormatoDelPlan._11)]
        public void Tipo_formato_acepta_06_10_11_y_6(string valor, TipoDeFormatoDelPlan esperado)
        {
            var plan = Planificar(F(2, A("123"), E("ZZ1"), Fo(valor), Mo("COR")));
            SinErrores(plan);
            Assert.Equal(esperado, Assert.Single(plan.Planes).Formato);
        }

        [Fact]
        public void Formato_11_no_tiene_regla_de_moneda_ni_de_clasificacion_al_crear_el_plan()
        {
            // FORMATO_11_SOLO_ACH es una regla de Registro sobre la Fila, NO del alta del plan (DN-04, corrección del 2026-10-01).
            SinErrores(Planificar(F(2, A("123"), E("ZZ1"), Fo("11"), Mo("COR"))));
        }

        [Theory]
        [InlineData("07")]
        [InlineData("1")]
        [InlineData("006")]
        [InlineData("diez")]
        public void Tipo_formato_invalido_es_error(string valor)
        {
            UnErrorQueEmpiezaCon(Unica(Planificar(F(2, A("123"), E("ZZ1"), Fo(valor), Mo("COR")))), "Tipo formato: ");
        }

        [Theory]
        [InlineData("EUR")]
        [InlineData("C0R")]
        [InlineData("dolares")]
        public void Moneda_invalida_es_error(string valor)
        {
            UnErrorQueEmpiezaCon(Unica(Planificar(F(2, A("123"), E("ZZ1"), Fo("06"), Mo(valor)))), "Moneda: ");
        }

        [Fact]
        public void Plan_nuevo_incompleto_nombra_lo_que_falta()
        {
            UnErrorQueEmpiezaCon(Unica(Planificar(F(2, A("123"), E("ZZ1"), Mo("COR")))), "Tipo formato: ");
            UnErrorQueEmpiezaCon(Unica(Planificar(F(2, A("123"), E("ZZ1"), Fo("06")))), "Moneda: ");
        }

        [Theory]
        [InlineData("ABCDE")]
        [InlineData("A-1")]
        public void Codigo_de_plan_invalido_es_error(string valor)
        {
            UnErrorQueEmpiezaCon(Unica(Planificar(F(2, A("123"), E(valor), Fo("06"), Mo("COR")))), "Codigo plan: ");
        }

        [Fact]
        public void Plan_existente_del_mismo_cliente_no_se_crea_y_sus_datos_tienen_que_coincidir()
        {
            var igual = Planificar(F(2, A("123"), E("a12"), Fo("06"), Mo("COR")));
            SinErrores(igual);
            Assert.Empty(igual.Planes);
            Assert.Empty(Unica(igual).Creados);
            Assert.Equal("Sin cambios: todo ya existía o lo crea una fila anterior", Unica(igual).Texto);

            UnErrorQueEmpiezaCon(Unica(Planificar(F(2, A("123"), E("0A12"), Fo("10")))), "Tipo formato: ");
            UnErrorQueEmpiezaCon(Unica(Planificar(F(2, A("123"), E("0A12"), Mo("USD")))), "Moneda: ");
        }

        [Fact]
        public void Plan_existente_de_otro_cliente_es_error_que_nombra_al_cliente_real()
        {
            var fila = Unica(Planificar(F(2, A("123"), E("0B34"))));
            var error = Assert.Single(fila.Errores, e => e.StartsWith("Codigo plan: ", StringComparison.Ordinal));
            Assert.Contains("Distribuidora Luna", error);
        }

        [Fact]
        public void Plan_existente_con_un_cliente_nuevo_en_la_fila_es_error()
        {
            UnErrorQueEmpiezaCon(Unica(Planificar(F(2, ClienteNuevo().Concat(new[] { E("0A12") }).ToArray()))), "Codigo plan: ");
        }

        [Fact]
        public void Plan_existente_inactivo_es_advertencia()
        {
            var plan = Planificar(F(2, A("123"), E("0C56")));
            SinErrores(plan);
            Assert.NotEmpty(Unica(plan).Advertencias);
        }

        [Fact]
        public void Plan_nuevo_de_un_cliente_nuevo_apunta_al_cliente_por_su_CIF_BAC()
        {
            var plan = Planificar(F(2, ClienteNuevo().Concat(new[] { E("N1"), Fo("10"), Mo("USD") }).ToArray()));
            SinErrores(plan);
            var p = Assert.Single(plan.Planes);
            Assert.Null(p.Cliente.IdExistente);
            Assert.Equal("000000555", p.Cliente.CifBacNuevo);
            Assert.Equal(new[] { "cliente", "plan" }, Unica(plan).Creados);
        }

        // ================================================================================================ correo y autorización
        [Fact]
        public void Nombre_fecha_o_comentarios_sin_correo_es_error()
        {
            UnErrorQueEmpiezaCon(Unica(Planificar(F(2, A("123"), I("Alguien")))), "Nombre contacto: ");
            UnErrorQueEmpiezaCon(Unica(Planificar(F(2, A("123"), J("01/10/2024")))), "Fecha documento: ");
            UnErrorQueEmpiezaCon(Unica(Planificar(F(2, A("123"), K("hola")))), "Comentarios: ");
        }

        [Theory]
        [InlineData("sin-arroba.com")]
        [InlineData("a@b")]
        [InlineData("a b@c.com")]
        public void Correo_con_formato_invalido_es_error(string valor)
        {
            UnErrorQueEmpiezaCon(Unica(Planificar(F(2, A("123"), H(valor), I("X")))), "Correo: ");
        }

        [Fact]
        public void Correo_nuevo_crea_el_correo_y_la_autorizacion_normalizados()
        {
            var plan = Planificar(F(2, A("123"), H("  Nuevo@Empresa.COM "), I("  Nuevo Contacto "), J("15/09/2024"), K("  Ok  ")));
            SinErrores(plan);
            var autorizado = Assert.Single(plan.Autorizados);
            Assert.Equal("nuevo@empresa.com", autorizado.Correo);
            Assert.Equal("Nuevo Contacto", autorizado.NombreContacto);
            var aut = Assert.Single(plan.Autorizaciones);
            Assert.Null(aut.Autorizado.IdExistente);
            Assert.Equal("nuevo@empresa.com", aut.Autorizado.CorreoNuevo);
            Assert.Equal(ClienteSol, aut.Cliente.IdExistente);
            Assert.Equal(new DateTime(2024, 9, 15), aut.FechaDocumento);
            Assert.Equal(DateTimeKind.Unspecified, aut.FechaDocumento.Value.Kind);
            Assert.Equal("Ok", aut.Comentarios);
            Assert.Equal(new[] { "correo", "autorización" }, Unica(plan).Creados);
            Assert.Equal("Creado: correo, autorización", Unica(plan).Texto);
        }

        [Fact]
        public void Correo_nuevo_sin_nombre_de_contacto_es_error()
        {
            UnErrorQueEmpiezaCon(Unica(Planificar(F(2, A("123"), H("nuevo@empresa.com")))), "Nombre contacto: ");
        }

        [Fact]
        public void Correo_existente_con_otro_nombre_es_error_y_con_el_mismo_no()
        {
            UnErrorQueEmpiezaCon(Unica(Planificar(F(2, A("456"), H("juan.perez@empresa.com"), I("Juan P.")))), "Nombre contacto: ");
            var plan = Planificar(F(2, A("123"), E("ZZ1"), Fo("06"), Mo("COR"), H("juan.perez@empresa.com"), I(" JUAN PÉREZ ")));
            SinErrores(plan);
        }

        [Fact]
        public void Correo_existente_se_autoriza_sobre_otro_cliente_usando_su_id()
        {
            var plan = Planificar(F(2, ClienteNuevo().Concat(new[] { H("juan.perez@empresa.com") }).ToArray()));
            SinErrores(plan);
            Assert.Empty(plan.Autorizados);
            var aut = Assert.Single(plan.Autorizaciones);
            Assert.Equal(Juan, aut.Autorizado.IdExistente);
            Assert.Null(aut.Autorizado.CorreoNuevo);
            Assert.Null(aut.Cliente.IdExistente);
            Assert.Equal("000000555", aut.Cliente.CifBacNuevo);
        }

        [Fact]
        public void Correo_existente_inactivo_es_advertencia()
        {
            var plan = Planificar(F(2, A("123"), H("maria.lopez@empresa.com")));
            SinErrores(plan);
            Assert.NotEmpty(Unica(plan).Advertencias);
            Assert.Single(plan.Autorizaciones);
        }

        [Fact]
        public void Autorizacion_existente_activa_queda_ya_existia_y_sus_datos_tienen_que_coincidir()
        {
            var igual = Planificar(F(2, A("123"), H("juan.perez@empresa.com"), J("01/10/2024"), K("FIRMADO EN SUCURSAL ")));
            SinErrores(igual);
            Assert.Empty(igual.Autorizaciones);
            Assert.Equal("Sin cambios: todo ya existía o lo crea una fila anterior", Unica(igual).Texto);

            SinErrores(Planificar(F(2, A("123"), H("juan.perez@empresa.com"))));
            UnErrorQueEmpiezaCon(Unica(Planificar(F(2, A("123"), H("juan.perez@empresa.com"), J("02/10/2024")))), "Fecha documento: ");
            UnErrorQueEmpiezaCon(Unica(Planificar(F(2, A("123"), H("juan.perez@empresa.com"), K("Otro")))), "Comentarios: ");
        }

        [Fact]
        public void Autorizacion_existente_inactiva_es_error_se_reactiva_con_el_otro_boton()
        {
            UnErrorQueEmpiezaCon(Unica(Planificar(F(2, A("456"), H("juan.perez@empresa.com")))), "Correo: ");
        }

        [Fact]
        public void Largos_maximos_de_correo_nombre_y_comentarios()
        {
            // Literales, no constantes del código: correo T(320), nombre contacto T(100), comentarios T(50) (diseno/02).
            var correo320 = new string('a', 320 - "@empresa.com".Length) + "@empresa.com";
            var correo321 = "a" + correo320;
            SinErrores(Planificar(F(2, A("123"), H(correo320), I("X"))));
            UnErrorQueEmpiezaCon(Unica(Planificar(F(2, A("123"), H(correo321), I("X")))), "Correo: ");

            SinErrores(Planificar(F(2, A("123"), H("n@empresa.com"), I(new string('n', 100)))));
            UnErrorQueEmpiezaCon(Unica(Planificar(F(2, A("123"), H("n@empresa.com"), I(new string('n', 101))))), "Nombre contacto: ");

            SinErrores(Planificar(F(2, A("123"), H("n@empresa.com"), I("X"), K(new string('k', 50)))));
            UnErrorQueEmpiezaCon(Unica(Planificar(F(2, A("123"), H("n@empresa.com"), I("X"), K(new string('k', 51))))), "Comentarios: ");
        }

        // ================================================================================================ fecha del documento
        public static IEnumerable<object[]> FechasValidas()
        {
            yield return new object[] { Serial(new DateTime(2024, 10, 1)), new DateTime(2024, 10, 1) };
            yield return new object[] { Serial(new DateTime(2024, 10, 1, 18, 0, 0)), new DateTime(2024, 10, 1) };
            yield return new object[] { "01/10/2024", new DateTime(2024, 10, 1) };
            yield return new object[] { "1/9/2024", new DateTime(2024, 9, 1) };
            yield return new object[] { " 29/02/2024 ", new DateTime(2024, 2, 29) };
            yield return new object[] { "01/10/2026", new DateTime(2026, 10, 1) }; // hoy: vale
        }

        [Theory]
        [MemberData(nameof(FechasValidas))]
        public void Fecha_documento_acepta_celda_de_fecha_y_texto_dd_mm_aaaa_sin_hora(string valor, DateTime esperada)
        {
            var plan = Planificar(F(2, A("123"), H("n@empresa.com"), I("X"), J(valor)));
            SinErrores(plan);
            var fecha = Assert.Single(plan.Autorizaciones).FechaDocumento.Value;
            Assert.Equal(esperada, fecha);
            Assert.Equal(TimeSpan.Zero, fecha.TimeOfDay);
            Assert.Equal(DateTimeKind.Unspecified, fecha.Kind);
        }

        [Theory]
        [InlineData("02/10/2026")]  // mañana
        [InlineData("2024-10-01")]  // otro formato
        [InlineData("31/02/2024")]  // no existe
        [InlineData("10/2024")]
        [InlineData("ayer")]
        [InlineData("-5")]
        public void Fecha_documento_futura_o_ilegible_es_error(string valor)
        {
            UnErrorQueEmpiezaCon(Unica(Planificar(F(2, A("123"), H("n@empresa.com"), I("X"), J(valor)))), "Fecha documento: ");
        }

        // ================================================================================================ coherencia entre filas
        [Fact]
        public void Archivo_desnormalizado_tipico_crea_cada_cosa_una_sola_vez_y_la_atribuye_a_la_primera_fila()
        {
            var plan = Planificar(
                F(2, ClienteNuevo().Concat(new[] { E("N1"), Fo("06"), Mo("COR"), H("nuevo@empresa.com"), I("Nuevo") }).ToArray()),
                F(3, A("555"), E("N2"), Fo("10"), Mo("USD"), H("nuevo@empresa.com")),
                F(4, B("NUE000000555"), E("N3"), Fo("11"), Mo("USD"), H("nuevo@empresa.com")));
            SinErrores(plan);
            Assert.Single(plan.Clientes);
            Assert.Equal(3, plan.Planes.Count);
            Assert.Single(plan.Autorizados);
            Assert.Single(plan.Autorizaciones);
            Assert.Equal(new[] { 2, 3, 4 }, plan.Planes.Select(p => p.FilaOrigen).OrderBy(n => n).ToArray());
            Assert.All(plan.Planes, p => Assert.Equal("000000555", p.Cliente.CifBacNuevo));
            Assert.Equal(new[] { "cliente", "plan", "correo", "autorización" }, plan.Filas[0].Creados);
            Assert.Equal(new[] { "plan" }, plan.Filas[1].Creados);
            Assert.Equal(new[] { "plan" }, plan.Filas[2].Creados);
            Assert.Equal("Creado: cliente, plan, correo, autorización", plan.Filas[0].Texto);
        }

        [Fact]
        public void Los_resultados_salen_en_el_orden_recibido_y_lo_crea_la_fila_de_menor_numero()
        {
            var plan = Planificar(
                F(5, A("555"), H("nuevo@empresa.com")),
                F(2, ClienteNuevo().Concat(new[] { H("nuevo@empresa.com"), I("Nuevo") }).ToArray()));
            SinErrores(plan);
            Assert.Equal(new[] { 5, 2 }, plan.Filas.Select(f => f.NumeroFilaExcel).ToArray());
            Assert.Equal(2, Assert.Single(plan.Clientes).FilaOrigen);
            Assert.Equal(2, Assert.Single(plan.Autorizaciones).FilaOrigen);
            Assert.Empty(plan.Filas[0].Creados);
            Assert.Equal(new[] { "cliente", "correo", "autorización" }, plan.Filas[1].Creados);
        }

        [Fact]
        public void Los_datos_de_un_cliente_nuevo_pueden_repartirse_entre_filas_si_no_se_contradicen()
        {
            var plan = Planificar(
                F(2, A("555"), B("NUE000000555"), C("Comercial Nueva")),
                F(3, A("555"), D("ana@banco.com")));
            SinErrores(plan);
            Assert.Equal(Ana, Assert.Single(plan.Clientes).DuenoId);
        }

        [Fact]
        public void Si_a_un_cliente_nuevo_le_falta_un_dato_en_todo_el_archivo_el_error_va_en_todas_sus_filas()
        {
            var plan = Planificar(F(2, A("555"), B("NUE000000555"), C("Comercial Nueva")), F(3, A("555"), E("N1"), Fo("06"), Mo("COR")));
            Assert.All(plan.Filas, f => UnErrorQueEmpiezaCon(f, "Correo ejecutivo: "));
            Assert.Empty(plan.Clientes);
            Assert.Empty(plan.Planes);
        }

        private static void NombraLaFila(ResultadoDeFilaDeCarga fila, string prefijo, int otra)
        {
            Assert.Contains(fila.Errores, e => e.StartsWith(prefijo, StringComparison.Ordinal) && Regex.IsMatch(e, $@"\b{otra}\b"));
        }

        [Fact]
        public void La_misma_clave_con_datos_distintos_es_error_en_todas_las_filas_y_nombra_a_las_otras()
        {
            var razon = Planificar(F(2, ClienteNuevo()), F(9, ClienteNuevo(razon: "Otra Razon")));
            NombraLaFila(razon.Filas[0], "Razon social: ", 9);
            NombraLaFila(razon.Filas[1], "Razon social: ", 2);

            var ejecutivo = Planificar(F(2, ClienteNuevo()), F(3, ClienteNuevo(ejecutivo: "beto@banco.com")));
            NombraLaFila(ejecutivo.Filas[0], "Correo ejecutivo: ", 3);

            var formato = Planificar(F(2, A("123"), E("N1"), Fo("06"), Mo("COR")), F(3, A("123"), E("N1"), Fo("10"), Mo("COR")));
            NombraLaFila(formato.Filas[0], "Tipo formato: ", 3);
            NombraLaFila(formato.Filas[1], "Tipo formato: ", 2);

            var moneda = Planificar(F(2, A("123"), E("N1"), Fo("06"), Mo("COR")), F(3, A("123"), E("N1"), Mo("USD")));
            NombraLaFila(moneda.Filas[1], "Moneda: ", 2);

            var nombre = Planificar(F(2, A("123"), H("n@empresa.com"), I("Uno")), F(3, A("456"), H("n@empresa.com"), I("Dos")));
            NombraLaFila(nombre.Filas[0], "Nombre contacto: ", 3);

            var comentario = Planificar(F(2, A("123"), H("n@empresa.com"), I("X"), K("a")), F(3, A("123"), E("0A12"), H("n@empresa.com"), K("b")));
            NombraLaFila(comentario.Filas[0], "Comentarios: ", 3);

            var fecha = Planificar(F(2, A("123"), H("n@empresa.com"), I("X"), J("01/09/2024")), F(3, A("123"), H("n@empresa.com"), J("02/09/2024")));
            NombraLaFila(fecha.Filas[1], "Fecha documento: ", 2);
        }

        [Fact]
        public void El_mismo_plan_nuevo_bajo_dos_clientes_es_error_en_las_dos_filas()
        {
            var plan = Planificar(F(2, A("123"), E("N1"), Fo("06"), Mo("COR")), F(3, A("456"), E("N1"), Fo("06"), Mo("COR")));
            NombraLaFila(plan.Filas[0], "Codigo plan: ", 3);
            NombraLaFila(plan.Filas[1], "Codigo plan: ", 2);
        }

        [Fact]
        public void Un_CIF_BAC_con_dos_CIF_COM_en_el_archivo_o_al_reves_es_error()
        {
            var bac = Planificar(F(2, ClienteNuevo()), F(3, A("555"), B("OTR000000555")));
            NombraLaFila(bac.Filas[0], "CIF BAC / CIF COM: ", 3);
            NombraLaFila(bac.Filas[1], "CIF BAC / CIF COM: ", 2);

            var com = Planificar(F(2, ClienteNuevo()), F(3, A("666"), B("NUE000000555"), C("Comercial Nueva"), D("ana@banco.com")));
            NombraLaFila(com.Filas[0], "CIF BAC / CIF COM: ", 3);
            NombraLaFila(com.Filas[1], "CIF BAC / CIF COM: ", 2);
        }

        [Fact]
        public void La_misma_autorizacion_en_varias_filas_se_crea_una_vez_y_las_celdas_vacias_no_contradicen()
        {
            var plan = Planificar(
                F(2, A("123"), E("N1"), Fo("06"), Mo("COR"), H("n@empresa.com"), I("X"), K("Firmado")),
                F(3, A("123"), E("N2"), Fo("06"), Mo("COR"), H("n@empresa.com")));
            SinErrores(plan);
            var aut = Assert.Single(plan.Autorizaciones);
            Assert.Equal("Firmado", aut.Comentarios);
            Assert.Equal(2, aut.FilaOrigen);
        }

        // ================================================================================================ todo o nada y texto
        [Fact]
        public void Con_un_error_en_cualquier_fila_no_se_crea_nada_y_las_filas_sanas_lo_dicen()
        {
            var plan = Planificar(
                F(2, ClienteNuevo().Concat(new[] { E("N1"), Fo("06"), Mo("COR"), H("nuevo@empresa.com"), I("Nuevo") }).ToArray()),
                F(3, A("123"), E("ZZZZZ")));
            Assert.True(plan.TieneErrores);
            Assert.Empty(plan.Clientes); Assert.Empty(plan.Planes); Assert.Empty(plan.Autorizados); Assert.Empty(plan.Autorizaciones);
            Assert.All(plan.Filas, f => Assert.Empty(f.Creados));
            Assert.Equal("Sin errores — no se grabó: el archivo tiene errores en otras filas", plan.Filas[0].Texto);
            Assert.StartsWith("Error: Codigo plan: ", plan.Filas[1].Texto);
        }

        [Fact]
        public void Una_fila_reporta_todos_sus_errores_no_solo_el_primero()
        {
            var fila = Unica(Planificar(F(2, A("12A"), E("ZZ1"), Fo("06"), Mo("EUR"), H("malo"), I("X"))));
            UnErrorQueEmpiezaCon(fila, "CIF BAC: ");
            UnErrorQueEmpiezaCon(fila, "Moneda: ");
            UnErrorQueEmpiezaCon(fila, "Correo: ");
            Assert.Equal(string.Join(" | ", fila.Errores.Select(e => "Error: " + e).Concat(fila.Advertencias.Select(a => "Advertencia: " + a))), fila.Texto);
        }

        [Fact]
        public void El_texto_lleva_errores_luego_la_accion_y_al_final_las_advertencias()
        {
            var plan = Planificar(F(2, A("789"), E("N1"), Fo("06"), Mo("COR")));
            SinErrores(plan);
            var fila = Unica(plan);
            Assert.Equal("Creado: plan" + string.Concat(fila.Advertencias.Select(a => " | Advertencia: " + a)), fila.Texto);
        }

        // ================================================================================================ huecos de la revisión (2026-10-01)
        [Fact]
        public void Correo_del_ejecutivo_de_mas_de_320_es_error_corto_y_no_entra_a_las_claves()
        {
            // Literal: un correo admite como máximo 320 caracteres (el mismo tope que la columna Correo, diseno/02 §2.3).
            var de320 = new string('a', 320 - "@banco.com".Length) + "@banco.com";
            var enorme = new string('a', 30000) + "@banco.com";

            var conDe320 = Unica(Planificar(F(2, ClienteNuevo(ejecutivo: de320))));
            Assert.DoesNotContain(conDe320.Errores, e => e.Contains("caracteres y el máximo"));  // 320 no es "demasiado largo"

            foreach (var plan in new[]
            {
                Planificar(F(2, ClienteNuevo(ejecutivo: enorme))),     // cliente nuevo
                Planificar(F(2, A("123"), D(enorme))),                 // cliente existente
            })
            {
                var fila = Unica(plan);
                UnErrorQueEmpiezaCon(fila, "Correo ejecutivo: ");
                Assert.All(fila.Errores, e => Assert.True(e.Length < 400, $"motivo de {e.Length} caracteres"));
                Assert.True(fila.Texto.Length < 1000, $"Resultado de {fila.Texto.Length} caracteres");
            }

            var claves = ClavesDeCarga.Extraer(new[] { F(2, ClienteNuevo(ejecutivo: enorme)) });
            Assert.Empty(claves.CorreosDeEjecutivo);
        }

        [Fact]
        public void Ningun_valor_enorme_en_ninguna_columna_produce_un_Resultado_enorme()
        {
            // Una celda de Excel admite 32.767 caracteres; el Resultado no puede acercarse. Cada columna, una por una.
            var enorme = new string('x', 30000);
            foreach (var letra in new[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M" })
            {
                var valores = ClienteNuevo().Concat(new[] { E("N1"), Fo("06"), Mo("COR"), Ca("Banca en Linea"), H("n@empresa.com"), I("X") })
                    .Where(v => v.Item1 != letra).Concat(new[] { (letra, enorme) }).ToArray();
                var fila = Unica(Planificar(F(2, valores)));
                Assert.NotEmpty(fila.Errores);
                Assert.True(fila.Texto.Length < 2000, $"columna {letra}: Resultado de {fila.Texto.Length} caracteres");
            }
        }

        [Theory]
        [InlineData("31/12/1899")]
        [InlineData("01/01/1000")]
        [InlineData("01/01/0001")]
        [InlineData("1")]   // serial 1 = 31/12/1899
        public void Fecha_documento_anterior_a_1900_es_error_en_la_pasada_1(string valor)
        {
            // Dataverse no admite fechas anteriores a 1753; un documento firmado anterior a 1900 no existe en este negocio.
            // Se rechaza en la fila, no en la escritura (que deshace toda la carga sin decir qué fila).
            UnErrorQueEmpiezaCon(Unica(Planificar(F(2, A("123"), H("n@empresa.com"), I("X"), J(valor)))), "Fecha documento: ");
        }

        [Fact]
        public void Fecha_documento_desde_el_1_de_enero_de_1900_vale()
        {
            SinErrores(Planificar(F(2, A("123"), H("n@empresa.com"), I("X"), J("01/01/1900"))));
            SinErrores(Planificar(F(2, A("123"), H("n@empresa.com"), I("X"), J(Serial(new DateTime(1900, 1, 1))))));
        }

        [Fact]
        public void Sobre_algo_que_ya_existe_lleva_error_solo_la_fila_que_contradice_lo_grabado()
        {
            // Decisión del arquitecto (revisión 2026-10-01, decisión 1 del constructor): lo existente se compara contra la foto,
            // no fila contra fila. La fila que coincide con lo grabado está bien; la otra lleva el error.
            var plan = Planificar(F(2, A("123"), C("Ferretería Sol")), F(3, A("123"), C("Otra Razon")));
            Assert.True(plan.TieneErrores);
            Assert.Empty(plan.Filas[0].Errores);
            UnErrorQueEmpiezaCon(plan.Filas[1], "Razon social: ");
        }

        [Fact]
        public void Un_CIF_BAC_solo_y_un_CIF_COM_solo_son_el_mismo_cliente_nuevo_si_otra_fila_trae_los_dos()
        {
            var plan = Planificar(
                F(2, A("555"), E("N1"), Fo("06"), Mo("COR")),
                F(3, B("NUE000000555"), E("N2"), Fo("06"), Mo("cor")),
                F(4, ClienteNuevo()));
            SinErrores(plan);
            var cliente = Assert.Single(plan.Clientes);
            Assert.Equal(2, cliente.FilaOrigen);
            Assert.All(plan.Planes, p => Assert.Equal("000000555", p.Cliente.CifBacNuevo));
            Assert.All(plan.Planes, p => Assert.Equal(Moneda.COR, p.Moneda));
        }

        [Fact]
        public void Trescientas_filas_en_conflicto_dentro_de_un_mismo_grupo_no_se_vuelven_lentas_ni_enormes()
        {
            var filas = Enumerable.Range(0, 300).Select(i => F(i + 2, ClienteNuevo(razon: "Razon " + (i % 150).ToString(CultureInfo.InvariantCulture)))).ToArray();
            var reloj = Stopwatch.StartNew();
            var plan = PlanificadorDeCarga.Planificar(filas, Foto(), Hoy);
            reloj.Stop();
            Assert.True(plan.TieneErrores);
            Assert.All(plan.Filas, f => UnErrorQueEmpiezaCon(f, "Razon social: "));
            Assert.All(plan.Filas, f => Assert.True(f.Texto.Length < 1000, $"fila {f.NumeroFilaExcel}: {f.Texto.Length} caracteres"));
            Assert.True(reloj.Elapsed < TimeSpan.FromSeconds(2), $"tardó {reloj.Elapsed}");
        }

        // ================================================================================================ lo grabado vacío (revisión 4a)
        [Fact]
        public void Un_correo_grabado_sin_nombre_de_contacto_lo_dice_y_no_cita_comillas_vacias()
        {
            // 6 de los 9 correos de Dev no tienen nombre (son anteriores a la columna, 2026-09-24). La carga no lo completa.
            var foto = new EstadoDeCatalogos(
                new[] { new ClienteExistente(G(1), "000000123", "ABC123456001", "Ferretería Sol", Ana, true) },
                new PlanExistente[0],
                new[] { new AutorizadoExistente(G(30), "viejo@empresa.com", null, true) },
                new[] { new AutorizacionClienteExistente(G(40), G(30), G(1), null, null, true) },
                new UsuarioCandidato[0]);
            var fila = Assert.Single(PlanificadorDeCarga.Planificar(new[] { F(2, A("123"), H("viejo@empresa.com"), I("Viejo"), K("algo")) }, foto, Hoy).Filas);
            var nombre = Assert.Single(fila.Errores, e => e.StartsWith("Nombre contacto: ", StringComparison.Ordinal));
            Assert.Contains("sin nombre de contacto", nombre);
            var comentario = Assert.Single(fila.Errores, e => e.StartsWith("Comentarios: ", StringComparison.Ordinal));
            Assert.Contains("sin comentario", comentario);
            Assert.DoesNotContain("\"\"", nombre + comentario);

            // Sin I ni K, ese mismo correo se usa sin problema.
            Assert.False(PlanificadorDeCarga.Planificar(new[] { F(2, A("123"), H("viejo@empresa.com")) }, foto, Hoy).TieneErrores);
        }

        // ================================================================================================ avisos del lector (revisión 4c)
        [Fact]
        public void Un_aviso_del_lector_es_error_de_su_fila_con_el_encabezado_de_su_columna()
        {
            var filas = new[] { F(2, A("123"), H("n@empresa.com"), I("X"), K("#REF!")), F(3, A("456")) };
            var avisos = new[] { new AdvertenciaLectura { NumeroFilaExcel = 2, Columna = "K", Motivo = "celda K2: tiene un valor de error de Excel ('#REF!')" } };
            var plan = PlanificadorDeCarga.Planificar(filas, Foto(), Hoy, avisos);
            Assert.True(plan.TieneErrores);
            Assert.Empty(plan.Autorizaciones);
            var error = Assert.Single(plan.Filas[0].Errores, e => e.StartsWith("Comentarios: ", StringComparison.Ordinal));
            Assert.Contains("#REF!", error);
            Assert.Empty(plan.Filas[1].Errores);

            // Sin avisos, las dos sobrecargas dicen lo mismo.
            Assert.Equal(PlanificadorDeCarga.Planificar(filas, Foto(), Hoy).Filas.Select(f => f.Texto),
                PlanificadorDeCarga.Planificar(filas, Foto(), Hoy, new AdvertenciaLectura[0]).Filas.Select(f => f.Texto));
        }

        [Fact]
        public void Un_aviso_mal_armado_es_error_de_quien_llama()
        {
            var filas = new[] { F(2, A("123")) };
            Assert.Throws<ArgumentNullException>(() => PlanificadorDeCarga.Planificar(filas, Foto(), Hoy, null));
            Assert.ThrowsAny<ArgumentException>(() => PlanificadorDeCarga.Planificar(filas, Foto(), Hoy, new AdvertenciaLectura[] { null }));
            Assert.ThrowsAny<ArgumentException>(() => PlanificadorDeCarga.Planificar(filas, Foto(), Hoy, new[] { new AdvertenciaLectura { NumeroFilaExcel = 9, Columna = "A", Motivo = "x" } }));
            Assert.ThrowsAny<ArgumentException>(() => PlanificadorDeCarga.Planificar(filas, Foto(), Hoy, new[] { new AdvertenciaLectura { NumeroFilaExcel = 2, Columna = "Z", Motivo = "x" } }));
        }

        // ================================================================================================ claves para la foto
        [Fact]
        public void Las_claves_salen_normalizadas_sin_repetidos_y_sin_las_invalidas()
        {
            var claves = ClavesDeCarga.Extraer(new[]
            {
                F(2, A("123"), B("abc123456001"), D(" ANA@banco.com "), E("a1"), H("Juan.Perez@Empresa.com")),
                F(3, A("000000123"), B("ABC123456001"), D("ana@banco.com"), E("00A1"), H("juan.perez@empresa.com ")),
                F(4, A("12A"), B("MALO"), D(""), E("ABCDE"), H("malo")),
            });
            Assert.Equal(new[] { "000000123" }, claves.CifsBac.ToArray());
            Assert.Equal(new[] { "ABC123456001" }, claves.CifsCom.ToArray());
            Assert.Equal(new[] { "00A1" }, claves.CodigosDePlan.ToArray());
            Assert.Equal(new[] { "juan.perez@empresa.com" }, claves.Correos.ToArray());
            Assert.Equal(new[] { "ana@banco.com" }, claves.CorreosDeEjecutivo.ToArray());
        }

        [Fact]
        public void Extraer_claves_rechaza_nulos_de_quien_llama()
        {
            Assert.Throws<ArgumentNullException>(() => ClavesDeCarga.Extraer(null));
            Assert.ThrowsAny<ArgumentException>(() => ClavesDeCarga.Extraer(new[] { F(2, A("1")), null, F(3, A("2")) }));
        }

        // ================================================================================================ costo acotado
        [Fact]
        public void Cinco_mil_filas_distintas_se_planifican_en_tiempo_lineal()
        {
            // Mucho más que el tope de 300 (carga.filas.maximo): detecta un recorrido por fila (cuadrático), no mide la plataforma.
            var filas = Enumerable.Range(0, 5000).Select(i =>
                F(i + 2, A((100000 + i).ToString(CultureInfo.InvariantCulture)), B($"NUE{i:D6}001"), C($"Cliente {i}"), D("ana@banco.com"),
                  // 0x2000 en adelante: "2000".."3387", lejos de los planes de la foto (0A12, 0B34, 0C56 caerían en el rango 0..4999).
                  E((i + 0x2000).ToString("X4", CultureInfo.InvariantCulture)), Fo("06"), Mo("COR"), H($"c{i}@empresa.com"), I($"Contacto {i}")))
                .ToArray();
            var reloj = Stopwatch.StartNew();
            var plan = PlanificadorDeCarga.Planificar(filas, Foto(), Hoy);
            reloj.Stop();
            SinErrores(plan);
            Assert.Equal(5000, plan.Clientes.Count);
            Assert.True(reloj.Elapsed < TimeSpan.FromSeconds(3), $"tardó {reloj.Elapsed}");
        }
    }
}
