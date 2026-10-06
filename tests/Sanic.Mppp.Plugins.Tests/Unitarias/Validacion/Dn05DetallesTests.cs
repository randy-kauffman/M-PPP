using System;
using System.Collections.Generic;
using System.Linq;
using Sanic.Mppp.Plugins.Dominio;
using Sanic.Mppp.Plugins.Plantilla;
using Sanic.Mppp.Plugins.Validacion;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Unitarias.Validacion
{
    /// <summary>DN-05: detalles internos que las pruebas de aceptación no fijan (el parser compartido, las claves desconocidas,
    /// el tope exacto de caracteres, los sustitutos sueltos, el largo en el borde).</summary>
    public class Dn05DetallesTests
    {
        private const string Prefijo = "P";

        // ------------------------------------------------------------------ AnalizadorJson
        [Fact]
        public void El_modo_de_siempre_no_entiende_numeros_ni_booleanos_y_pisa_la_clave_repetida()
        {
            Assert.Throws<FormatException>(() => AnalizadorJson.Leer("{\"a\": 1}", Prefijo));
            Assert.Throws<FormatException>(() => AnalizadorJson.Leer("{\"a\": true}", Prefijo));

            var objeto = (IDictionary<string, object>)AnalizadorJson.Leer("{\"a\": \"uno\", \"a\": \"dos\"}", Prefijo);
            Assert.Equal("dos", objeto["a"]);

            // Y no exige que el documento termine: lo de siempre (lo validó el deserializador antes).
            Assert.NotNull(AnalizadorJson.Leer("{} basura", Prefijo));
        }

        [Fact]
        public void El_modo_estricto_lee_enteros_decimales_y_booleanos_con_su_tipo()
        {
            var o = (IDictionary<string, object>)AnalizadorJson.LeerEstricto(
                "{\"i\": 17, \"n\": -3, \"d\": 6.5, \"e\": 1e2, \"t\": true, \"f\": false, \"z\": null, \"g\": 12345678901234}", Prefijo);

            Assert.Equal(17L, o["i"]);
            Assert.Equal(-3L, o["n"]);
            Assert.Equal(6.5, o["d"]);
            Assert.Equal(100.0, o["e"]); // con exponente es decimal, aunque valga 100
            Assert.Equal(true, o["t"]);
            Assert.Equal(false, o["f"]);
            Assert.Null(o["z"]);
            Assert.Equal(12345678901234L, o["g"]);
        }

        [Theory]
        [InlineData("{\"a\": 1, \"a\": 2}")] // repetida
        [InlineData("{} {}")] // texto después del final
        [InlineData("{\"a\": 01}")] // cero a la izquierda
        [InlineData("{\"a\": -}")]
        [InlineData("{\"a\": 1.}")]
        [InlineData("{\"a\": 1e}")]
        [InlineData("{\"a\": +1}")]
        [InlineData("{\"a\": tru}")]
        public void El_modo_estricto_rechaza_lo_que_no_es_json_de_verdad(string json)
        {
            Assert.Throws<FormatException>(() => AnalizadorJson.LeerEstricto(json, Prefijo));
        }

        [Fact]
        public void Un_entero_que_no_cabe_en_long_llega_como_decimal_y_no_como_entero()
        {
            var o = (IDictionary<string, object>)AnalizadorJson.LeerEstricto("{\"a\": 99999999999999999999999}", Prefijo);
            Assert.IsType<double>(o["a"]);
        }

        [Fact]
        public void El_anidamiento_esta_acotado_y_no_desborda_la_pila()
        {
            var hondo = string.Concat(Enumerable.Repeat("[", 5000)) + string.Concat(Enumerable.Repeat("]", 5000));
            Assert.Throws<FormatException>(() => AnalizadorJson.LeerEstricto(hondo, Prefijo));
            Assert.Throws<FormatException>(() => AnalizadorJson.Leer(hondo, Prefijo));

            var justo = string.Concat(Enumerable.Repeat("[", AnalizadorJson.ProfundidadMaxima)) + string.Concat(Enumerable.Repeat("]", AnalizadorJson.ProfundidadMaxima));
            Assert.NotNull(AnalizadorJson.LeerEstricto(justo, Prefijo));
        }

        [Fact]
        public void El_mensaje_de_error_abre_con_el_prefijo_recibido()
        {
            var ex = Assert.Throws<FormatException>(() => AnalizadorJson.Leer("{\"a\": 1}", "El parámetro X"));
            Assert.StartsWith("El parámetro X: ", ex.Message);
        }

        // ------------------------------------------------------------------ plan.formatos y cuenta.largos: claves que sobran
        private const string F06 = "\"06\": {\"clasificaciones\": [\"BAC\"], \"gestiones\": [\"Inclusion\"]}";
        private const string F10 = "\"10\": {\"clasificaciones\": [\"BAC\"], \"gestiones\": [\"Inclusion\"]}";
        private const string F11 = "\"11\": {\"clasificaciones\": [\"ACH\"], \"gestiones\": [\"Inclusion\"]}";

        [Theory]
        [InlineData("\"06\": {\"clasificaciones\": [\"BAC\"], \"gestiones\": [\"Inclusion\"], \"banco\": [\"BAC\"]}")] // `banco` por `bancos`: no puede quedar como "cualquier banco"
        [InlineData("\"06\": {\"clasificaciones\": [\"BAC\"], \"gestiones\": [\"Inclusion\"], \"bancos\": [\"BAC\"], \"x\": 1}")]
        public void Formatos_una_clave_desconocida_en_un_formato_es_error(string f06)
        {
            Assert.Throws<FormatException>(() => FormatosDelPlan.DesdeJson("{" + f06 + "," + F10 + "," + F11 + "}"));
        }

        [Fact]
        public void Formatos_lee_los_tres_aunque_vengan_en_otro_orden_y_acepta_bancos_null()
        {
            var f = FormatosDelPlan.DesdeJson("{" + F11 + "," + F06 + "," + F10 + "}");
            Assert.Equal(new[] { Clasificacion.ACH }, f.Para(TipoDeFormatoDelPlan._11).Clasificaciones);
            Assert.Null(f.Para(TipoDeFormatoDelPlan._06).Bancos);
        }

        [Theory]
        [InlineData("{\"porDefecto\": {\"minimo\": 6, \"maximo\": 17}, \"porBancos\": {}}")] // `porBancos`: no puede quedar como "todos por defecto"
        [InlineData("{\"porDefecto\": {\"minimo\": 6, \"maximo\": 17, \"largo\": 3}}")]
        [InlineData("{\"porDefecto\": {\"minimo\": 6, \"maximo\": 17}, \"porBanco\": {\"BAC\": null}}")]
        [InlineData("{\"porDefecto\": {\"minimo\": 6, \"maximo\": true}}")]
        [InlineData("{\"porDefecto\": {\"minimo\": 6, \"maximo\": 9999999999}}")] // no cabe en int
        [InlineData("{\"porDefecto\": {\"minimo\": 6, \"maximo\": 17}} x")]
        public void Largos_formas_que_no_se_adivinan_son_error(string json)
        {
            Assert.Throws<FormatException>(() => LargosDeCuenta.DesdeJson(json));
        }

        [Fact]
        public void Largos_acepta_un_rango_de_un_solo_valor_y_un_banco_en_otra_caja()
        {
            var l = LargosDeCuenta.DesdeJson("{\"porDefecto\": {\"minimo\": 7, \"maximo\": 7}, \"porBanco\": {\"Ficohsa\": {\"minimo\": 4, \"maximo\": 16}}}");
            Assert.Equal((7, 7), (l.Para(Banco.BAC).Minimo, l.Para(Banco.BAC).Maximo));
            Assert.Equal((4, 16), (l.Para(Banco.FICOHSA).Minimo, l.Para(Banco.FICOHSA).Maximo));
        }

        // ------------------------------------------------------------------ CaracteresPermitidos: otros con un carácter repetido y en el borde
        [Fact]
        public void Caracteres_otros_se_conserva_tal_cual_aunque_repita()
        {
            var c = new CaracteresPermitidos(false, false, false, "--ab");
            Assert.Equal("--ab", c.Otros);
            Assert.True(c.Permite('-') && c.Permite('a') && c.Permite('b'));
            Assert.False(c.Permite('c') || c.Permite('A') || c.Permite(' '));
        }

        // ------------------------------------------------------------------ CARACTERES_PERMITIDOS y CUENTA_DEL_BANCO sobre una fila armada a mano
        private static readonly Guid PlanId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");

        private static FilaEnValidacion Fila(params (string campo, string valor)[] cambios)
        {
            var campos = CamposDeFila.Todos()
                .Select((c, i) => new CampoPlantilla { Nombre = c, Columna = ((char)('C' + i)).ToString(), EncabezadoEsperado = "H-" + c })
                .ToList();
            campos.Single(c => c.Nombre == "referencia").Caracteres = new CaracteresPermitidos(true, true, false, null);
            campos.Single(c => c.Nombre == "nombreBeneficiario").Caracteres = new CaracteresPermitidos(true, false, true, "😀");
            const string listas = "{\"gestion\":[{\"valor\":\"Inclusion\"}],\"clasificacion\":[{\"valor\":\"BAC\"}],\"tipoIdentificacion\":[{\"valor\":\"CNA\"}],"
                + "\"moneda\":[{\"valor\":\"USD\"}],\"banco\":[{\"valor\":\"BAC\",\"codigo\":\"007\"}]}";
            var catalogos = new CatalogosDeValidacion(
                new ConfiguracionPlantilla { Hoja = "H", FilaEncabezado = 1, PrimeraFila = 2, CantidadFilas = 5, Campos = campos },
                ListasPlantilla.DesdeJson(listas),
                ObligatoriedadPlantilla.DesdeJson("{\"porDefecto\":\"obligatorio\"}"),
                new List<PlanDelCatalogo> { new PlanDelCatalogo(PlanId, "00A1", TipoDeFormatoDelPlan._06, Moneda.USD) },
                new[] { PlanId },
                formatos: FormatosDelPlan.DesdeJson("{" + F06.Replace("\"gestiones\": [\"Inclusion\"]", "\"gestiones\": [\"Inclusion\"], \"bancos\": [\"BAC\"]") + "," + F10 + "," + F11 + "}"),
                largosDeCuenta: LargosDeCuenta.DesdeJson("{\"porDefecto\": {\"minimo\": 6, \"maximo\": 17}, \"porBanco\": {\"BAC\": {\"minimo\": 9, \"maximo\": 16}}}"));

            var valores = new Dictionary<string, string> { ["numeroPlan"] = "A1", ["numeroCuenta"] = "123456789" };
            foreach (var (campo, valor) in cambios)
            {
                valores[campo] = valor;
            }

            return new FilaEnValidacion(
                new FilaPlantilla { NumeroFilaExcel = 2, NumeroOrden = 1, Valores = valores.ToDictionary(p => campos.Single(c => c.Nombre == p.Key).Columna, p => p.Value) },
                catalogos);
        }

        private static Veredicto Suelta(string codigo, FilaEnValidacion fila) => ReglasDeRegistro.Evaluadores().Single(e => e.Codigo == codigo).Evaluar(fila);

        [Theory]
        [InlineData(10, false)] // exactamente el tope: no hay "…"
        [InlineData(11, true)]
        public void El_tope_de_diez_caracteres_agrega_los_puntos_solo_si_hay_mas_distintos(int distintos, bool conPuntos)
        {
            const string todos = "!\"#$%&'()*+";
            var valor = todos.Substring(0, distintos) + "!!!"; // los repetidos no cuentan
            var mostrados = string.Join(" ", todos.Take(Math.Min(distintos, 10)).Select(c => c.ToString())) + (conPuntos ? " …" : string.Empty);

            var razon = Suelta(ReglasDeRegistro.CaracteresPermitidos, Fila(("referencia", valor))).Razon;

            Assert.Equal($"Caracteres no permitidos: H-referencia ({mostrados}).", razon);
        }

        [Fact]
        public void Un_sustituto_suelto_es_invisible_y_un_par_permitido_en_otros_se_acepta()
        {
            // La mitad alta sola (un Excel corrupto) no se muestra: se nombra.
            Assert.Equal("Caracteres no permitidos: H-referencia ((carácter invisible)).",
                Suelta(ReglasDeRegistro.CaracteresPermitidos, Fila(("referencia", "AB\uD83D"))).Razon);
            Assert.Equal("Caracteres no permitidos: H-referencia ((carácter invisible)).",
                Suelta(ReglasDeRegistro.CaracteresPermitidos, Fila(("referencia", "\uDE00AB"))).Razon);

            // El emoji está en `otros` del nombre: el par entero se acepta; en la referencia (sin `otros`) se muestra entero.
            Assert.True(Suelta(ReglasDeRegistro.CaracteresPermitidos, Fila(("nombreBeneficiario", "Juan😀"))).Cumple);
            Assert.Equal("Caracteres no permitidos: H-referencia (😀).", Suelta(ReglasDeRegistro.CaracteresPermitidos, Fila(("referencia", "A😀B😀"))).Razon);
        }

        [Fact]
        public void Un_valor_larguisimo_de_un_solo_caracter_prohibido_se_resuelve_sin_acumular()
        {
            var razon = Suelta(ReglasDeRegistro.CaracteresPermitidos, Fila(("referencia", new string('-', 200000)))).Razon;
            Assert.Equal("Caracteres no permitidos: H-referencia (-).", razon);
        }

        [Theory]
        [InlineData(8, false)]
        [InlineData(9, true)]
        [InlineData(16, true)]
        [InlineData(17, false)]
        public void El_largo_de_la_cuenta_se_mide_en_los_bordes_del_banco_efectivo(int largo, bool cumple)
        {
            // Banco vacío en un plan 06 = BAC (9-16): el borde de arriba es 16, no el 17 del por defecto.
            Assert.Equal(cumple, Suelta(ReglasDeRegistro.CuentaDelBanco, Fila(("numeroCuenta", new string('7', largo)))).Cumple);
        }
    }
}
