using System;
using System.Collections.Generic;
using System.Linq;
using Sanic.Mppp.Plugins.Dominio;
using Sanic.Mppp.Plugins.Plantilla;
using Sanic.Mppp.Plugins.Validacion;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Aceptacion
{
    /// <summary>
    /// DN-05 (aprobador, 2026-10-06; diseno/DIRECTRICES-NUEVAS.md): escenarios de prueba del negocio y matriz de
    /// obligatoriedad. Fija `plan.formatos` (clasificación, gestión y banco por formato), `cuenta.largos`, los caracteres
    /// permitidos por campo, la referencia de formato 11 solo en Inclusión, y la matriz completa como
    /// `plantilla.obligatoriedad` versión 3. PRUEBAS DEL ARQUITECTO: el constructor no las toca.
    /// </summary>
    public class Dn05Aceptacion
    {
        // ------------------------------------------------------------------ los parámetros, tal cual se van a sembrar
        /// <summary>`plan.formatos` de DN-05 §B, exacto.</summary>
        internal const string FormatosDn05 = @"{
            ""06"": {""clasificaciones"": [""BAC""],      ""gestiones"": [""Inclusion"",""Exclusion"",""Modificacion""], ""bancos"": [""BAC""]},
            ""10"": {""clasificaciones"": [""BAC"",""CK""], ""gestiones"": [""Inclusion"",""Exclusion"",""Modificacion""], ""bancos"": [""BAC""]},
            ""11"": {""clasificaciones"": [""ACH""],      ""gestiones"": [""Inclusion"",""Exclusion""]}
        }";

        /// <summary>`cuenta.largos` de DN-05 §F, con los datos del aprobador (escenario 20), exacto.</summary>
        internal const string LargosDn05 = @"{
            ""porDefecto"": {""minimo"": 6, ""maximo"": 17},
            ""porBanco"": {
                ""BANPRO"":  {""minimo"": 6,  ""maximo"": 16},
                ""BDF"":     {""minimo"": 10, ""maximo"": 16},
                ""LAFISE"":  {""minimo"": 9,  ""maximo"": 16},
                ""FICOHSA"": {""minimo"": 4,  ""maximo"": 16},
                ""AVANZ"":   {""minimo"": 10, ""maximo"": 16},
                ""BAC"":     {""minimo"": 9,  ""maximo"": 16}
            }
        }";

        /// <summary>`plantilla.obligatoriedad` versión 3 de DN-05 §A, exacta: la matriz del negocio.</summary>
        internal const string ObligatoriedadV3 = @"{
            ""porDefecto"": ""obligatorio"",
            ""opcionales"": [{""campo"":""referencia""}, {""campo"":""moneda""}],
            ""reglas"": [
                {""gestion"":""Inclusion"",""clasificacion"":""*"",""formato"":""06"",""opcionales"":[""clasificacion"",""tipoIdentificacion"",""banco""]},
                {""gestion"":""Inclusion"",""clasificacion"":""*"",""formato"":""10"",""opcionales"":[""banco""]},
                {""gestion"":""Inclusion"",""clasificacion"":""*"",""formato"":""11"",""opcionales"":[""clasificacion""]}
            ],
            ""porGestion"": {
                ""Exclusion"":    {""opcionales"":[{""campo"":""clasificacion""},{""campo"":""nombreBeneficiario""},{""campo"":""tipoIdentificacion""},{""campo"":""numeroIdentificacion""},{""campo"":""banco""}],""obligatorios"":[{""campo"":""referencia""}]},
                ""Modificacion"": {""opcionales"":[{""campo"":""clasificacion""},{""campo"":""nombreBeneficiario""},{""campo"":""tipoIdentificacion""},{""campo"":""numeroIdentificacion""},{""campo"":""banco""}],""obligatorios"":[{""campo"":""referencia""}]}
            }
        }";

        /// <summary>Las cinco reglas nuevas de nivel Registro.</summary>
        internal static readonly string[] CodigosNuevos =
        {
            ReglasDeRegistro.ClasificacionDelFormato, ReglasDeRegistro.GestionDelFormato, ReglasDeRegistro.BancoDelFormato,
            ReglasDeRegistro.CaracteresPermitidos, ReglasDeRegistro.CuentaDelBanco,
        };

        /// <summary>
        /// El catálogo de registro DESTINO de DN-05, en orden: FORMATO_11_SOLO_ACH ya no está (inactiva) y las tres reglas por
        /// formato dependen de la autorización, igual que la que reemplazan: sobre un plan ajeno quedan Omitidas y no dicen nada.
        /// </summary>
        internal static readonly (string codigo, string[] dependeDe)[] SemillaRegistro =
        {
            (ReglasDeRegistro.ListasValidas, new string[0]),
            (ReglasDeRegistro.LargosYFormato, new string[0]),
            (ReglasDeRegistro.CaracteresPermitidos, new string[0]),
            (ReglasDeRegistro.PlanExiste, new string[0]),
            (ReglasDeRegistro.AutorizacionCorreoPlan, new[] { ReglasDeRegistro.PlanExiste }),
            (ReglasDeRegistro.GestionDelFormato, new[] { ReglasDeRegistro.AutorizacionCorreoPlan, ReglasDeRegistro.ListasValidas }),
            (ReglasDeRegistro.ClasificacionDelFormato, new[] { ReglasDeRegistro.AutorizacionCorreoPlan, ReglasDeRegistro.ListasValidas }),
            (ReglasDeRegistro.BancoDelFormato, new[] { ReglasDeRegistro.AutorizacionCorreoPlan, ReglasDeRegistro.ListasValidas }),
            (ReglasDeRegistro.Obligatoriedad, new[] { ReglasDeRegistro.AutorizacionCorreoPlan, ReglasDeRegistro.ListasValidas }),
            (ReglasDeRegistro.MonedaDelPlan, new[] { ReglasDeRegistro.AutorizacionCorreoPlan, ReglasDeRegistro.ListasValidas }),
            (ReglasDeRegistro.ReferenciaFormato11, new[] { ReglasDeRegistro.ClasificacionDelFormato, ReglasDeRegistro.Obligatoriedad }),
            (ReglasDeRegistro.CuentaDelBanco, new[] { ReglasDeRegistro.BancoDelFormato }),
        };

        // ------------------------------------------------------------------ el mundo de estas pruebas
        /// <summary>Listas con los ocho bancos reales y sus códigos (playbooks/datos/parametros.md).</summary>
        private const string Listas = @"{
            ""gestion"":[{""valor"":""Inclusion""},{""valor"":""Exclusion""},{""valor"":""Modificacion""}],
            ""clasificacion"":[{""valor"":""BAC""},{""valor"":""ACH""},{""valor"":""CK""}],
            ""tipoIdentificacion"":[{""valor"":""CNA""},{""valor"":""RUC""},{""valor"":""PAS""}],
            ""moneda"":[{""valor"":""COR""},{""valor"":""USD""}],
            ""banco"":[{""valor"":""LAFISE"",""codigo"":""006""},{""valor"":""BAC"",""codigo"":""007""},{""valor"":""BANPRO"",""codigo"":""008""},
                       {""valor"":""FICOHSA"",""codigo"":""009""},{""valor"":""BDF"",""codigo"":""012""},{""valor"":""AVANZ"",""codigo"":""019""},
                       {""valor"":""PRODUZCAMOS"",""codigo"":""023""},{""valor"":""ATLANTIDA"",""codigo"":""037""}]
        }";

        private static readonly Guid P06 = Guid.Parse("00000000-0000-0000-0000-000000000006");
        private static readonly Guid P10 = Guid.Parse("00000000-0000-0000-0000-000000000010");
        private static readonly Guid P11 = Guid.Parse("00000000-0000-0000-0000-000000000011");
        private static readonly Guid PAjeno = Guid.Parse("00000000-0000-0000-0000-000000000099");

        private static readonly Dictionary<string, string> Columnas = CamposDeFila.Todos()
            .Select((campo, i) => (campo, columna: ((char)('C' + i)).ToString()))
            .ToDictionary(x => x.campo, x => x.columna);

        private static readonly Dictionary<string, string> Encabezados = new Dictionary<string, string>
        {
            ["gestion"] = "Gestion", ["clasificacion"] = "Clasificacion", ["tipoIdentificacion"] = "Tipo ID", ["moneda"] = "Moneda", ["banco"] = "Banco",
            ["numeroPlan"] = "No. Plan", ["nombreBeneficiario"] = "Nombre", ["numeroIdentificacion"] = "No. Identificacion",
            ["numeroCuenta"] = "No. Cuenta", ["referencia"] = "Referencia",
        };

        /// <summary>La estructura con los caracteres de DN-05 §E (decisión 7). Largos amplios a propósito: LARGOS_Y_FORMATO no es de esta prueba.</summary>
        private static ConfiguracionPlantilla Estructura()
        {
            var campos = CamposDeFila.Todos().Select(c => new CampoPlantilla { Nombre = c, Columna = Columnas[c], EncabezadoEsperado = Encabezados[c] }).ToList();
            campos.Single(c => c.Nombre == "nombreBeneficiario").Caracteres = new CaracteresPermitidos(true, false, true, "-.,");
            campos.Single(c => c.Nombre == "referencia").Caracteres = new CaracteresPermitidos(true, true, false, null);
            campos.Single(c => c.Nombre == "numeroIdentificacion").Caracteres = new CaracteresPermitidos(true, true, false, null);
            campos.Single(c => c.Nombre == "numeroCuenta").Caracteres = new CaracteresPermitidos(false, true, false, null);
            return new ConfiguracionPlantilla { Hoja = "Datos", FilaEncabezado = 12, PrimeraFila = 13, CantidadFilas = 100, Campos = campos };
        }

        /// <summary>El remitente: autorizado sobre 0006, 0010 y 0011; NO sobre 0099.</summary>
        private static CatalogosDeValidacion Catalogos(bool conFormatos = true, bool conLargos = true, string obligatoriedad = ObligatoriedadV3, string formatos = FormatosDn05)
        {
            return new CatalogosDeValidacion(
                Estructura(),
                ListasPlantilla.DesdeJson(Listas),
                ObligatoriedadPlantilla.DesdeJson(obligatoriedad),
                new List<PlanDelCatalogo>
                {
                    new PlanDelCatalogo(P06, "0006", TipoDeFormatoDelPlan._06, Moneda.COR),
                    new PlanDelCatalogo(P10, "0010", TipoDeFormatoDelPlan._10, Moneda.USD),
                    new PlanDelCatalogo(P11, "0011", TipoDeFormatoDelPlan._11, Moneda.USD),
                    new PlanDelCatalogo(PAjeno, "0099", TipoDeFormatoDelPlan._11, Moneda.USD),
                },
                new[] { P06, P10, P11 },
                formatos: conFormatos ? FormatosDelPlan.DesdeJson(formatos) : null,
                largosDeCuenta: conLargos ? LargosDeCuenta.DesdeJson(LargosDn05) : null);
        }

        /// <summary>
        /// Una fila BUENA para el plan pedido (Inclusión, con todo lo que su formato admite), con los cambios de cada prueba.
        /// Un valor nulo = la celda no vino. `plan` es el código tal cual se escribe: "6", "10", "11" o "99".
        /// </summary>
        private static FilaEnValidacion Fila(CatalogosDeValidacion catalogos, string plan, params (string campo, string valor)[] cambios)
        {
            var porFormato = new Dictionary<string, (string clasificacion, string moneda, string banco)>
            {
                ["6"] = ("BAC", "COR", "BAC"), ["10"] = ("CK", "USD", "BAC"), ["11"] = ("ACH", "USD", "LAFISE"), ["99"] = ("ACH", "USD", "LAFISE"),
            };
            var (clasificacion, moneda, banco) = porFormato[plan];
            var porCampo = new Dictionary<string, string>
            {
                ["gestion"] = "Inclusion", ["clasificacion"] = clasificacion, ["tipoIdentificacion"] = "CNA", ["moneda"] = moneda, ["banco"] = banco,
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

        // ================================================================== plan.formatos
        [Fact]
        public void Formatos_lee_lo_que_admite_cada_formato_en_el_orden_del_parametro()
        {
            var f = FormatosDelPlan.DesdeJson(FormatosDn05);

            Assert.Equal(new[] { Clasificacion.BAC }, f.Para(TipoDeFormatoDelPlan._06).Clasificaciones);
            Assert.Equal(new[] { Clasificacion.BAC, Clasificacion.CK }, f.Para(TipoDeFormatoDelPlan._10).Clasificaciones);
            Assert.Equal(new[] { Clasificacion.ACH }, f.Para(TipoDeFormatoDelPlan._11).Clasificaciones);
            Assert.Equal(new[] { Gestion.Inclusion, Gestion.Exclusion, Gestion.Modificacion }, f.Para(TipoDeFormatoDelPlan._06).Gestiones);
            Assert.Equal(new[] { Gestion.Inclusion, Gestion.Exclusion }, f.Para(TipoDeFormatoDelPlan._11).Gestiones);
            Assert.Equal(new[] { Banco.BAC }, f.Para(TipoDeFormatoDelPlan._06).Bancos);
            Assert.Equal(new[] { Banco.BAC }, f.Para(TipoDeFormatoDelPlan._10).Bancos);
            Assert.Null(f.Para(TipoDeFormatoDelPlan._11).Bancos); // sin la clave: cualquier banco
            Assert.Equal("plan.formatos", FormatosDelPlan.NombreParametro);
        }

        [Fact]
        public void Formatos_compara_los_nombres_sin_distinguir_caja_ni_tildes_y_bancos_null_es_cualquiera()
        {
            var f = FormatosDelPlan.DesdeJson(@"{
                ""06"": {""clasificaciones"": [""bac""], ""gestiones"": [""INCLUSIÓN"",""exclusión""], ""bancos"": [""Bac""]},
                ""10"": {""clasificaciones"": [""ck""], ""gestiones"": [""modificación""], ""bancos"": null},
                ""11"": {""clasificaciones"": [""Ach""], ""gestiones"": [""Inclusion""]}
            }");

            Assert.Equal(new[] { Clasificacion.BAC }, f.Para(TipoDeFormatoDelPlan._06).Clasificaciones);
            Assert.Equal(new[] { Gestion.Inclusion, Gestion.Exclusion }, f.Para(TipoDeFormatoDelPlan._06).Gestiones);
            Assert.Equal(new[] { Banco.BAC }, f.Para(TipoDeFormatoDelPlan._06).Bancos);
            Assert.Equal(new[] { Gestion.Modificacion }, f.Para(TipoDeFormatoDelPlan._10).Gestiones);
            Assert.Null(f.Para(TipoDeFormatoDelPlan._10).Bancos);
        }

        private const string F10 = @"""10"": {""clasificaciones"": [""BAC""], ""gestiones"": [""Inclusion""]}";
        private const string F11 = @"""11"": {""clasificaciones"": [""ACH""], ""gestiones"": [""Inclusion""]}";

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("{no es json")]
        [InlineData("[]")]
        [InlineData("\"06\"")]
        [InlineData("{" + F10 + "," + F11 + "}")] // falta el 06
        [InlineData(@"{""06"": {""clasificaciones"": [""BAC""], ""gestiones"": [""Inclusion""]}," + F10 + "," + F11 + @",""07"": {""clasificaciones"": [""BAC""], ""gestiones"": [""Inclusion""]}}")] // sobra un formato
        [InlineData(@"{""06"": {""clasificaciones"": [""BAC""], ""gestiones"": [""Inclusion""]},""06"": {""clasificaciones"": [""BAC""], ""gestiones"": [""Inclusion""]}," + F10 + "," + F11 + "}")] // repetido
        [InlineData(@"{""6"": {""clasificaciones"": [""BAC""], ""gestiones"": [""Inclusion""]}," + F10 + "," + F11 + "}")] // "6" no es "06"
        [InlineData(@"{""06"": [""BAC""]," + F10 + "," + F11 + "}")] // la entrada no es un objeto
        [InlineData(@"{""06"": {""gestiones"": [""Inclusion""]}," + F10 + "," + F11 + "}")] // sin clasificaciones
        [InlineData(@"{""06"": {""clasificaciones"": [], ""gestiones"": [""Inclusion""]}," + F10 + "," + F11 + "}")] // clasificaciones vacía
        [InlineData(@"{""06"": {""clasificaciones"": [""BAC""]}," + F10 + "," + F11 + "}")] // sin gestiones
        [InlineData(@"{""06"": {""clasificaciones"": [""BAC""], ""gestiones"": []}," + F10 + "," + F11 + "}")] // gestiones vacía
        [InlineData(@"{""06"": {""clasificaciones"": [""BAC""], ""gestiones"": [""Inclusion""], ""bancos"": []}," + F10 + "," + F11 + "}")] // bancos presente y vacía
        [InlineData(@"{""06"": {""clasificaciones"": [""XYZ""], ""gestiones"": [""Inclusion""]}," + F10 + "," + F11 + "}")] // clasificación desconocida
        [InlineData(@"{""06"": {""clasificaciones"": [""BAC""], ""gestiones"": [""Alta""]}," + F10 + "," + F11 + "}")] // gestión desconocida
        [InlineData(@"{""06"": {""clasificaciones"": [""BAC""], ""gestiones"": [""Inclusion""], ""bancos"": [""CITI""]}," + F10 + "," + F11 + "}")] // banco desconocido
        [InlineData(@"{""06"": {""clasificaciones"": [""BAC"",""bac""], ""gestiones"": [""Inclusion""]}," + F10 + "," + F11 + "}")] // repetida en la lista
        [InlineData(@"{""06"": {""clasificaciones"": [1], ""gestiones"": [""Inclusion""]}," + F10 + "," + F11 + "}")] // no es texto
        [InlineData(@"{""06"": {""clasificaciones"": ""BAC"", ""gestiones"": [""Inclusion""]}," + F10 + "," + F11 + "}")] // no es arreglo
        [InlineData(@"{""06"": {""clasificaciones"": [null], ""gestiones"": [""Inclusion""]}," + F10 + "," + F11 + "}")] // elemento nulo
        public void Formatos_mal_cargado_es_FormatException(string json)
        {
            Assert.Throws<FormatException>(() => FormatosDelPlan.DesdeJson(json));
        }

        [Fact]
        public void Formatos_no_se_puede_mutar_desde_afuera()
        {
            var f = FormatosDelPlan.DesdeJson(FormatosDn05);
            var reglas = f.Para(TipoDeFormatoDelPlan._06);

            if (reglas.Clasificaciones is IList<Clasificacion> c)
            {
                try { c[0] = Clasificacion.ACH; } catch (NotSupportedException) { }
            }

            if (reglas.Bancos is IList<Banco> b)
            {
                try { b[0] = Banco.LAFISE; } catch (NotSupportedException) { }
            }

            Assert.Equal(new[] { Clasificacion.BAC }, f.Para(TipoDeFormatoDelPlan._06).Clasificaciones);
            Assert.Equal(new[] { Banco.BAC }, f.Para(TipoDeFormatoDelPlan._06).Bancos);
        }

        // ================================================================== cuenta.largos
        [Theory]
        [InlineData(Banco.BANPRO, 6, 16)]
        [InlineData(Banco.BDF, 10, 16)]
        [InlineData(Banco.LAFISE, 9, 16)]
        [InlineData(Banco.FICOHSA, 4, 16)]
        [InlineData(Banco.AVANZ, 10, 16)]
        [InlineData(Banco.BAC, 9, 16)]
        [InlineData(Banco.PRODUZCAMOS, 6, 17)] // sin entrada propia: porDefecto
        [InlineData(Banco.ATLANTIDA, 6, 17)]
        public void Largos_de_cada_banco_son_los_del_aprobador(Banco banco, int minimo, int maximo)
        {
            var rango = LargosDeCuenta.DesdeJson(LargosDn05).Para(banco);
            Assert.Equal((minimo, maximo), (rango.Minimo, rango.Maximo));
        }

        [Fact]
        public void Largos_sin_banco_usa_el_por_defecto_y_sin_porBanco_todos_usan_el_por_defecto()
        {
            var sinBanco = LargosDeCuenta.DesdeJson(LargosDn05).Para(null);
            Assert.Equal((6, 17), (sinBanco.Minimo, sinBanco.Maximo));

            var soloDefecto = LargosDeCuenta.DesdeJson(@"{""porDefecto"": {""minimo"": 3, ""maximo"": 5}}");
            Assert.Equal((3, 5), (soloDefecto.Para(Banco.BAC).Minimo, soloDefecto.Para(Banco.BAC).Maximo));

            var porBancoNulo = LargosDeCuenta.DesdeJson(@"{""porDefecto"": {""minimo"": 3, ""maximo"": 5}, ""porBanco"": null}");
            Assert.Equal(5, porBancoNulo.Para(Banco.LAFISE).Maximo);

            var normalizado = LargosDeCuenta.DesdeJson(@"{""porDefecto"": {""minimo"": 6, ""maximo"": 17}, ""porBanco"": {""bac"": {""minimo"": 9, ""maximo"": 9}}}");
            Assert.Equal((9, 9), (normalizado.Para(Banco.BAC).Minimo, normalizado.Para(Banco.BAC).Maximo));
            Assert.Equal("cuenta.largos", LargosDeCuenta.NombreParametro);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData("{no es json")]
        [InlineData("[]")]
        [InlineData(@"{""porBanco"": {""BAC"": {""minimo"": 9, ""maximo"": 16}}}")] // sin porDefecto
        [InlineData(@"{""porDefecto"": null}")]
        [InlineData(@"{""porDefecto"": {""maximo"": 17}}")] // sin mínimo
        [InlineData(@"{""porDefecto"": {""minimo"": 6}}")] // sin máximo
        [InlineData(@"{""porDefecto"": {""minimo"": 0, ""maximo"": 17}}")] // mínimo < 1
        [InlineData(@"{""porDefecto"": {""minimo"": 18, ""maximo"": 17}}")] // mínimo > máximo
        [InlineData(@"{""porDefecto"": {""minimo"": ""6"", ""maximo"": 17}}")] // no es entero
        [InlineData(@"{""porDefecto"": {""minimo"": 6.5, ""maximo"": 17}}")] // no es entero
        [InlineData(@"{""porDefecto"": {""minimo"": 6, ""maximo"": 17}, ""porBanco"": {""CITI"": {""minimo"": 6, ""maximo"": 16}}}")] // banco desconocido
        [InlineData(@"{""porDefecto"": {""minimo"": 6, ""maximo"": 17}, ""porBanco"": {""BAC"": {""minimo"": 9, ""maximo"": 16}, ""bac"": {""minimo"": 9, ""maximo"": 16}}}")] // ambiguo
        [InlineData(@"{""porDefecto"": {""minimo"": 6, ""maximo"": 17}, ""porBanco"": {""BAC"": {""minimo"": 17, ""maximo"": 16}}}")] // rango de banco inválido
        [InlineData(@"{""porDefecto"": {""minimo"": 6, ""maximo"": 17}, ""porBanco"": [""BAC""]}")] // porBanco no es objeto
        public void Largos_mal_cargado_es_FormatException(string json)
        {
            Assert.Throws<FormatException>(() => LargosDeCuenta.DesdeJson(json));
        }

        // ================================================================== CaracteresPermitidos
        [Theory]
        [InlineData('a', true)]
        [InlineData('Z', true)]
        [InlineData(' ', true)]
        [InlineData('-', true)]
        [InlineData('.', true)]
        [InlineData(',', true)]
        [InlineData('1', false)] // el nombre no lleva dígitos
        [InlineData('ñ', false)] // decisión 7: sin Ñ
        [InlineData('Ñ', false)]
        [InlineData('é', false)] // ni tildes
        [InlineData('&', false)]
        [InlineData('\t', false)] // solo el espacio común
        [InlineData(' ', false)] // espacio duro
        [InlineData('​', false)] // espacio de ancho cero
        [InlineData('Ａ', false)] // A de ancho completo: no es ASCII
        public void Caracteres_del_nombre_son_los_de_la_decision_7(char c, bool permitido)
        {
            Assert.Equal(permitido, new CaracteresPermitidos(true, false, true, "-.,").Permite(c));
        }

        [Fact]
        public void Caracteres_sin_nada_permitido_es_un_parametro_mal_cargado_y_otros_nulo_es_vacio()
        {
            Assert.Throws<ArgumentException>(() => new CaracteresPermitidos(false, false, false, null));
            Assert.Throws<ArgumentException>(() => new CaracteresPermitidos(false, false, false, string.Empty));

            var soloDigitos = new CaracteresPermitidos(false, true, false, null);
            Assert.Equal(string.Empty, soloDigitos.Otros);
            Assert.True(soloDigitos.Permite('0') && soloDigitos.Permite('9'));
            Assert.False(soloDigitos.Permite('a') || soloDigitos.Permite(' ') || soloDigitos.Permite('٣')); // dígito arábigo: no ASCII
            Assert.True(new CaracteresPermitidos(false, false, false, "#").Permite('#'));
        }

        /// <summary>`plantilla.estructura` mínima válida (la forma sembrada), con un `caracteres` opcional en el nombre.</summary>
        private static string EstructuraJson(string caracteresDelNombre)
        {
            var campos = CamposDeFila.Todos().Select((c, i) =>
            {
                var extra = c == "nombreBeneficiario" && caracteresDelNombre != null ? @",""caracteres"":" + caracteresDelNombre : string.Empty;
                return $@"{{""nombre"":""{c}"",""columna"":""{(char)('B' + i)}"",""encabezado"":""{Encabezados[c]}"",""formato"":""texto""{extra}}}";
            });
            return @"{""hoja"":""Datos"",""filaEncabezado"":12,""primeraFila"":13,""cantidadFilas"":100,""campos"":[" + string.Join(",", campos) + "]}";
        }

        [Fact]
        public void La_estructura_lee_caracteres_por_campo_y_un_campo_sin_la_clave_no_se_revisa()
        {
            var e = ConfiguracionPlantilla.DesdeJson(EstructuraJson(@"{""letras"":true,""espacio"":true,""otros"":""-.,""}"));
            var nombre = e.Campos.Single(c => c.Nombre == "nombreBeneficiario").Caracteres;

            Assert.NotNull(nombre);
            Assert.True(nombre.Letras && nombre.Espacio && !nombre.Digitos);
            Assert.Equal("-.,", nombre.Otros);
            Assert.All(e.Campos.Where(c => c.Nombre != "nombreBeneficiario"), c => Assert.Null(c.Caracteres));
            Assert.Null(ConfiguracionPlantilla.DesdeJson(EstructuraJson(@"null")).Campos.Single(c => c.Nombre == "nombreBeneficiario").Caracteres);
        }

        [Theory]
        [InlineData(@"{}")] // nada permitido
        [InlineData(@"{""letras"":false,""digitos"":false,""espacio"":false,""otros"":""""}")]
        [InlineData(@"{""letras"":""si""}")] // no es booleano
        [InlineData(@"{""otros"":5}")] // no es texto
        [InlineData(@"""letras""")] // no es objeto
        [InlineData(@"{""letras"":1}")] // revisión DN-05, hallazgo 5: tipos exactos
        [InlineData(@"{""letras"":""true""}")]
        [InlineData(@"{""letras"":true,""tildes"":true}")] // clave desconocida: no se ignora en silencio
        [InlineData(@"{""letras"":null}")]
        [InlineData(@"{""letras"":true,""digitos"":null}")] // el nulo solo: no lo tapa un "todo en false"
        public void La_estructura_con_caracteres_mal_cargados_es_FormatException(string caracteres)
        {
            Assert.Throws<FormatException>(() => ConfiguracionPlantilla.DesdeJson(EstructuraJson(caracteres)));
        }

        [Fact]
        public void El_catalogo_conserva_los_caracteres_en_la_copia_de_cada_campo()
        {
            var nombre = Catalogos().Campo("nombreBeneficiario");
            Assert.NotNull(nombre.Caracteres);
            Assert.True(nombre.Caracteres.Permite('-'));
            Assert.False(nombre.Caracteres.Permite('1'));
        }

        // ================================================================== la matriz completa (OBLIGATORIEDAD con la v3)
        /// <summary>La matriz del negocio con las correcciones del aprobador (DN-05): lo que es obligatorio por gestión y formato.</summary>
        private static readonly Dictionary<(string gestion, string plan), string[]> Obligatorios = new Dictionary<(string, string), string[]>
        {
            [("Inclusion", "6")] = new[] { "gestion", "numeroPlan", "nombreBeneficiario", "numeroIdentificacion", "numeroCuenta" },
            [("Inclusion", "10")] = new[] { "gestion", "clasificacion", "numeroPlan", "nombreBeneficiario", "tipoIdentificacion", "numeroIdentificacion", "numeroCuenta" },
            [("Inclusion", "11")] = new[] { "gestion", "numeroPlan", "nombreBeneficiario", "tipoIdentificacion", "numeroIdentificacion", "numeroCuenta", "banco" },
            [("Exclusion", "6")] = new[] { "gestion", "numeroPlan", "referencia", "numeroCuenta" },
            [("Exclusion", "10")] = new[] { "gestion", "numeroPlan", "referencia", "numeroCuenta" },
            [("Exclusion", "11")] = new[] { "gestion", "numeroPlan", "referencia", "numeroCuenta" }, // decisión 10: el banco no hace falta
            [("Modificacion", "6")] = new[] { "gestion", "numeroPlan", "referencia", "numeroCuenta" },
            [("Modificacion", "10")] = new[] { "gestion", "numeroPlan", "referencia", "numeroCuenta" },
        };

        public static IEnumerable<object[]> CeldasDeLaMatriz()
        {
            return Obligatorios.SelectMany(p => CamposDeFila.Todos().Select(campo => new object[] { p.Key.gestion, p.Key.plan, campo, p.Value.Contains(campo) }));
        }

        [Theory]
        [MemberData(nameof(CeldasDeLaMatriz))]
        public void Con_la_v3_cada_celda_de_la_matriz_es_obligatoria_u_opcional_segun_el_negocio(string gestion, string plan, string campo, bool obligatorio)
        {
            var fila = Fila(plan, ("gestion", gestion), (campo, null));
            Assert.Equal(!obligatorio, Suelta(ReglasDeRegistro.Obligatoriedad, fila).Cumple);
        }

        [Theory]
        [InlineData("Inclusion", "6")]
        [InlineData("Inclusion", "10")]
        [InlineData("Inclusion", "11")]
        [InlineData("Exclusion", "6")]
        [InlineData("Exclusion", "10")]
        [InlineData("Exclusion", "11")]
        [InlineData("Modificacion", "6")]
        [InlineData("Modificacion", "10")]
        public void Con_la_v3_una_fila_solo_con_los_obligatorios_pasa_obligatoriedad(string gestion, string plan)
        {
            var cambios = CamposDeFila.Todos().Where(c => !Obligatorios[(gestion, plan)].Contains(c)).Select(c => (c, (string)null)).ToList();
            cambios.Add(("gestion", gestion));
            Assert.True(Suelta(ReglasDeRegistro.Obligatoriedad, Fila(plan, cambios.ToArray())).Cumple);
        }

        // ================================================================== CLASIFICACION_DEL_FORMATO
        [Theory]
        [InlineData("6", "BAC", true)]
        [InlineData("6", "ACH", false)] // escenario 9: otro canal en un plan solo BAC
        [InlineData("6", "CK", false)] // escenario 10: cheques en un plan que no los admite
        [InlineData("6", "", true)]
        [InlineData("10", "BAC", true)]
        [InlineData("10", "CK", true)]
        [InlineData("10", "ACH", false)]
        [InlineData("10", "", true)] // que sea obligatoria lo dice OBLIGATORIEDAD, no esta regla
        [InlineData("11", "ACH", true)]
        [InlineData("11", "BAC", false)]
        [InlineData("11", "CK", false)]
        [InlineData("11", "", true)]
        [InlineData("10", "XYZ", false)] // inválida y suelta: no se da por buena
        public void La_clasificacion_tiene_que_ser_una_de_las_del_formato(string plan, string clasificacion, bool cumple)
        {
            Assert.Equal(cumple, Suelta(ReglasDeRegistro.ClasificacionDelFormato, Fila(plan, ("clasificacion", clasificacion))).Cumple);
        }

        [Fact]
        public void La_clasificacion_del_formato_nombra_al_plan_y_las_permitidas()
        {
            var once = Suelta(ReglasDeRegistro.ClasificacionDelFormato, Fila("11", ("clasificacion", "BAC")));
            Assert.Equal("El plan 0011 solo admite la clasificación ACH.", once.Razon); // el mismo texto que FORMATO_11_SOLO_ACH
            Assert.Equal("0011", once.Marcadores["plan"]);

            var diez = Suelta(ReglasDeRegistro.ClasificacionDelFormato, Fila("10", ("clasificacion", "ACH")));
            Assert.Equal("El plan 0010 solo admite la clasificación BAC o CK.", diez.Razon);
        }

        [Fact]
        public void La_clasificacion_del_formato_sin_plan_no_se_cumple_y_sin_parametro_es_error_de_configuracion()
        {
            Assert.False(Suelta(ReglasDeRegistro.ClasificacionDelFormato, Fila("6", ("numeroPlan", "7777"))).Cumple);
            Assert.Throws<InvalidOperationException>(() => Suelta(ReglasDeRegistro.ClasificacionDelFormato, Fila(Catalogos(conFormatos: false), "6")));
        }

        [Theory]
        [InlineData("6", "", Clasificacion.BAC)] // una sola posible: se completa
        [InlineData("11", "", Clasificacion.ACH)]
        [InlineData("11", "  ", Clasificacion.ACH)] // solo espacios = vacía
        [InlineData("10", "", null)] // dos posibles: no se adivina
        [InlineData("6", "ACH", Clasificacion.ACH)] // un valor recibido NUNCA se reemplaza (lo rechaza la regla)
        [InlineData("6", "XYZ", null)] // inválido: queda vacío (DD-01), no se completa
        public void La_clasificacion_efectiva_completa_solo_cuando_el_formato_admite_una(string plan, string recibida, Clasificacion? esperada)
        {
            Assert.Equal(esperada, Fila(plan, ("clasificacion", recibida)).ClasificacionEfectiva);
        }

        [Fact]
        public void La_clasificacion_efectiva_no_se_completa_sin_plan_ni_sin_parametro_y_nunca_lanza()
        {
            Assert.Null(Fila("6", ("clasificacion", ""), ("numeroPlan", "7777")).ClasificacionEfectiva);
            Assert.Null(Fila(Catalogos(conFormatos: false), "6", ("clasificacion", "")).ClasificacionEfectiva);
            Assert.Equal(Clasificacion.BAC, Fila(Catalogos(conFormatos: false), "6").ClasificacionEfectiva);
        }

        // ================================================================== GESTION_DEL_FORMATO
        [Theory]
        [InlineData("11", "Modificacion", false)] // decisión 4
        [InlineData("11", "Inclusion", true)]
        [InlineData("11", "Exclusion", true)]
        [InlineData("6", "Modificacion", true)]
        [InlineData("10", "Modificacion", true)]
        [InlineData("11", "", true)] // vacía: es de OBLIGATORIEDAD
        [InlineData("11", "Alta", false)] // inválida y suelta: no se da por buena
        public void La_gestion_tiene_que_ser_una_de_las_del_formato(string plan, string gestion, bool cumple)
        {
            Assert.Equal(cumple, Suelta(ReglasDeRegistro.GestionDelFormato, Fila(plan, ("gestion", gestion))).Cumple);
        }

        [Fact]
        public void La_gestion_del_formato_nombra_al_plan_y_la_gestion()
        {
            var v = Suelta(ReglasDeRegistro.GestionDelFormato, Fila("11", ("gestion", "modificación")));
            Assert.Equal("El plan 0011 no admite la gestión Modificacion.", v.Razon);
            Assert.Equal("0011", v.Marcadores["plan"]);
        }

        [Fact]
        public void La_gestion_del_formato_sin_plan_no_se_cumple_y_sin_parametro_es_error_de_configuracion()
        {
            Assert.False(Suelta(ReglasDeRegistro.GestionDelFormato, Fila("11", ("numeroPlan", "7777"))).Cumple);
            Assert.Throws<InvalidOperationException>(() => Suelta(ReglasDeRegistro.GestionDelFormato, Fila(Catalogos(conFormatos: false), "11")));
        }

        // ================================================================== BANCO_DEL_FORMATO
        [Theory]
        [InlineData("6", "BAC", true)]
        [InlineData("6", "LAFISE", false)] // decisión 10: 06 y 10 solo BAC
        [InlineData("6", "", true)]
        [InlineData("10", "BAC", true)]
        [InlineData("10", "BANPRO", false)]
        [InlineData("10", "", true)]
        [InlineData("11", "LAFISE", true)] // el 11 no restringe bancos
        [InlineData("11", "BAC", true)]
        [InlineData("11", "", true)]
        [InlineData("6", "CITI", false)] // inválido y suelto: no se da por bueno
        public void El_banco_tiene_que_ser_uno_de_los_del_formato(string plan, string banco, bool cumple)
        {
            Assert.Equal(cumple, Suelta(ReglasDeRegistro.BancoDelFormato, Fila(plan, ("banco", banco))).Cumple);
        }

        [Fact]
        public void El_banco_del_formato_nombra_al_plan_y_los_permitidos()
        {
            var v = Suelta(ReglasDeRegistro.BancoDelFormato, Fila("6", ("banco", "LAFISE")));
            Assert.Equal("El plan 0006 solo admite cuentas de BAC.", v.Razon);
            Assert.Equal("0006", v.Marcadores["plan"]);
        }

        [Fact]
        public void El_banco_del_formato_sin_plan_no_se_cumple_y_sin_parametro_es_error_de_configuracion()
        {
            Assert.False(Suelta(ReglasDeRegistro.BancoDelFormato, Fila("6", ("numeroPlan", "7777"))).Cumple);
            Assert.Throws<InvalidOperationException>(() => Suelta(ReglasDeRegistro.BancoDelFormato, Fila(Catalogos(conFormatos: false), "6")));
        }

        [Theory]
        [InlineData("6", "", Banco.BAC)] // decisión 10: vacío en 06 y 10 = BAC
        [InlineData("10", "", Banco.BAC)]
        [InlineData("11", "", null)] // el 11 admite cualquiera: no se adivina
        [InlineData("6", "LAFISE", Banco.LAFISE)] // un valor recibido NUNCA se reemplaza
        [InlineData("6", "CITI", null)]
        public void El_banco_efectivo_completa_solo_cuando_el_formato_admite_uno(string plan, string recibido, Banco? esperado)
        {
            Assert.Equal(esperado, Fila(plan, ("banco", recibido)).BancoEfectivo);
        }

        // ================================================================== CARACTERES_PERMITIDOS (escenario 21)
        [Theory]
        [InlineData("nombreBeneficiario", "Distribuidora del Norte, S.A.", true)]
        [InlineData("nombreBeneficiario", "Juan-Perez", true)]
        [InlineData("nombreBeneficiario", "Peña", false)]
        [InlineData("nombreBeneficiario", "José", false)]
        [InlineData("nombreBeneficiario", "A&B", false)]
        [InlineData("referencia", "AB12", true)]
        [InlineData("referencia", "AB-12", false)]
        [InlineData("referencia", "AB 12", false)]
        [InlineData("numeroIdentificacion", "0011234567890A", true)]
        [InlineData("numeroIdentificacion", "001-123456-0000A", false)] // sin guiones: solo letras y dígitos
        [InlineData("numeroCuenta", "123456789", true)]
        [InlineData("numeroCuenta", "1234 5678", false)]
        [InlineData("numeroCuenta", "12.345678", false)]
        [InlineData("numeroCuenta", "", true)] // vacío: es de OBLIGATORIEDAD
        [InlineData("moneda", "US$", true)] // sin `caracteres`: no se revisa (LISTAS_VALIDAS se encarga)
        public void Cada_campo_solo_admite_sus_caracteres(string campo, string valor, bool cumple)
        {
            Assert.Equal(cumple, Suelta(ReglasDeRegistro.CaracteresPermitidos, Fila("6", (campo, valor))).Cumple);
        }

        [Fact]
        public void Los_caracteres_no_permitidos_se_nombran_por_campo_sin_repetir_y_en_el_orden_de_la_fila()
        {
            var v = Suelta(ReglasDeRegistro.CaracteresPermitidos, Fila("6", ("referencia", "A-B-1"), ("nombreBeneficiario", "Peña #ñ1")));

            // Orden de los campos: el de CamposDeFila.Todos() (nombre antes que referencia). Orden de los caracteres: el de
            // su primera aparición. Sin repetir.
            Assert.Equal("Caracteres no permitidos: Nombre (ñ # 1); Referencia (-).", v.Razon);
            Assert.Equal("Nombre, Referencia", v.Marcadores["campo"]);
        }

        [Fact]
        public void Los_caracteres_invisibles_y_el_espacio_se_nombran_sin_mostrarse()
        {
            Assert.Equal("Caracteres no permitidos: Referencia ((espacio)).",
                Suelta(ReglasDeRegistro.CaracteresPermitidos, Fila("6", ("referencia", "AB 12"))).Razon);

            // Tabulador, espacio de ancho cero y espacio duro: todos se muestran igual, y por eso una sola vez.
            Assert.Equal("Caracteres no permitidos: Nombre ((carácter invisible)).",
                Suelta(ReglasDeRegistro.CaracteresPermitidos, Fila("6", ("nombreBeneficiario", "Juan\tPe​re z"))).Razon);
        }

        [Fact]
        public void Un_caracter_fuera_del_plano_basico_se_muestra_entero_una_sola_vez()
        {
            Assert.Equal("Caracteres no permitidos: Nombre (😀).",
                Suelta(ReglasDeRegistro.CaracteresPermitidos, Fila("6", ("nombreBeneficiario", "Juan😀😀"))).Razon);
        }

        [Fact]
        public void Se_muestran_a_lo_sumo_diez_caracteres_por_campo()
        {
            var v = Suelta(ReglasDeRegistro.CaracteresPermitidos, Fila("6", ("referencia", "!\"#$%&'()*+/")));
            Assert.Equal("Caracteres no permitidos: Referencia (! \" # $ % & ' ( ) * …).", v.Razon);
        }

        [Fact]
        public void El_mensaje_de_caracteres_nunca_cita_el_valor_de_la_cuenta_ni_de_la_identificacion()
        {
            var v = Suelta(ReglasDeRegistro.CaracteresPermitidos, Fila("6", ("numeroCuenta", "12.345678"), ("numeroIdentificacion", "001-123456-0000A")));
            Assert.DoesNotContain("12.345678", v.Razon);
            Assert.DoesNotContain("001-123456-0000A", v.Razon);
            Assert.Equal("Caracteres no permitidos: No. Identificacion (-); No. Cuenta (.).", v.Razon);
        }

        // ================================================================== CUENTA_DEL_BANCO (escenario 20)
        [Theory]
        [InlineData("11", "LAFISE", 8, false)]
        [InlineData("11", "LAFISE", 9, true)]
        [InlineData("11", "LAFISE", 16, true)]
        [InlineData("11", "LAFISE", 17, false)]
        [InlineData("11", "BANPRO", 5, false)]
        [InlineData("11", "BANPRO", 6, true)]
        [InlineData("11", "FICOHSA", 4, true)]
        [InlineData("11", "ATLANTIDA", 17, true)] // sin entrada propia: 6-17
        [InlineData("11", "ATLANTIDA", 18, false)]
        [InlineData("6", "", 8, false)] // banco vacío en 06 = BAC: 9-16
        [InlineData("6", "", 9, true)]
        [InlineData("10", "", 16, true)]
        [InlineData("10", "", 17, false)]
        public void El_largo_de_la_cuenta_depende_del_banco(string plan, string banco, int largo, bool cumple)
        {
            var fila = Fila(plan, ("banco", banco), ("numeroCuenta", new string('1', largo)));
            Assert.Equal(cumple, Suelta(ReglasDeRegistro.CuentaDelBanco, fila).Cumple);
        }

        [Fact]
        public void Los_ceros_a_la_izquierda_cuentan_para_el_largo()
        {
            Assert.True(Suelta(ReglasDeRegistro.CuentaDelBanco, Fila("6", ("numeroCuenta", "000000001"))).Cumple); // 9
            Assert.False(Suelta(ReglasDeRegistro.CuentaDelBanco, Fila("6", ("numeroCuenta", "00000001"))).Cumple); // 8
            Assert.True(Suelta(ReglasDeRegistro.CuentaDelBanco, Fila("6", ("numeroCuenta", "  000000001  "))).Cumple); // los espacios a los lados no
        }

        [Fact]
        public void Una_exclusion_en_formato_11_sin_banco_usa_el_largo_por_defecto()
        {
            Assert.True(Suelta(ReglasDeRegistro.CuentaDelBanco, Fila("11", ("gestion", "Exclusion"), ("banco", ""), ("numeroCuenta", new string('1', 17)))).Cumple);
            Assert.True(Suelta(ReglasDeRegistro.CuentaDelBanco, Fila("11", ("gestion", "Exclusion"), ("banco", ""), ("numeroCuenta", new string('1', 6)))).Cumple);
            var v = Suelta(ReglasDeRegistro.CuentaDelBanco, Fila("11", ("gestion", "Exclusion"), ("banco", ""), ("numeroCuenta", new string('1', 18))));
            Assert.False(v.Cumple);
            Assert.Equal("El largo de la cuenta no es válido.", v.Razon);
        }

        [Fact]
        public void El_mensaje_del_largo_nombra_el_banco_y_nunca_la_cuenta_ni_el_largo()
        {
            var v = Suelta(ReglasDeRegistro.CuentaDelBanco, Fila("11", ("banco", "LAFISE"), ("numeroCuenta", "12345678")));
            Assert.Equal("El largo de la cuenta no corresponde al banco LAFISE.", v.Razon);
            Assert.DoesNotContain("12345678", v.Razon);

            var deBac = Suelta(ReglasDeRegistro.CuentaDelBanco, Fila("6", ("banco", ""), ("numeroCuenta", "12345678")));
            Assert.Equal("El largo de la cuenta no corresponde al banco BAC.", deBac.Razon); // el efectivo
        }

        [Fact]
        public void La_cuenta_vacia_no_se_mide_y_sin_parametro_es_error_de_configuracion()
        {
            Assert.True(Suelta(ReglasDeRegistro.CuentaDelBanco, Fila("6", ("numeroCuenta", null))).Cumple);
            Assert.Throws<InvalidOperationException>(() => Suelta(ReglasDeRegistro.CuentaDelBanco, Fila(Catalogos(conLargos: false), "6")));
        }

        // ================================================================== REFERENCIA_FORMATO_11 solo en Inclusión (decisión 3)
        [Fact]
        public void En_una_inclusion_de_formato_11_la_referencia_se_arma_con_banco_y_cuenta()
        {
            var fila = Fila("11", ("referencia", "IGNORADA"));
            Assert.Equal("006" + "123456789".PadLeft(17, '0'), fila.ReferenciaConstruida);
            Assert.Equal("006" + "123456789".PadLeft(17, '0'), fila.ReferenciaQueRige);
            Assert.True(Suelta(ReglasDeRegistro.ReferenciaFormato11, fila).Cumple);
            Assert.False(Suelta(ReglasDeRegistro.ReferenciaFormato11, Fila("11", ("banco", ""))).Cumple); // en la inclusión el banco hace falta
        }

        [Theory]
        [InlineData("")]
        [InlineData("LAFISE")]
        public void En_una_exclusion_de_formato_11_rige_la_referencia_que_mando_el_cliente(string banco)
        {
            var fila = Fila("11", ("gestion", "Exclusion"), ("banco", banco), ("referencia", "  REFX1 "));
            Assert.Null(fila.ReferenciaConstruida);
            Assert.Equal("REFX1", fila.ReferenciaQueRige);
            Assert.Equal("  REFX1 ", fila.ReferenciaRecibida);
            Assert.True(Suelta(ReglasDeRegistro.ReferenciaFormato11, fila).Cumple);
        }

        [Fact]
        public void Con_la_gestion_vacia_o_invalida_en_formato_11_no_se_arma_nada_y_rige_la_recibida()
        {
            foreach (var gestion in new[] { "", "Alta" })
            {
                var fila = Fila("11", ("gestion", gestion), ("referencia", "REFX1"));
                Assert.Null(fila.ReferenciaConstruida);
                Assert.Equal("REFX1", fila.ReferenciaQueRige);
                Assert.True(Suelta(ReglasDeRegistro.ReferenciaFormato11, fila).Cumple); // la fila ya la rechazan LISTAS u OBLIGATORIEDAD
            }
        }

        // ================================================================== DN-10: la referencia cuando no viene
        // (aprobador, 2026-11-27, corregido el 2026-11-28): si la referencia viene vacía, en una Inclusión
        //  - de formato 06 rige el NÚMERO DE IDENTIFICACIÓN, tal cual vino, sin espacios a los lados (sin pasar a mayúsculas);
        //  - de formato 10 con clasificación BAC rige el NÚMERO DE CUENTA, tal cual vino, sin espacios a los lados ni relleno.
        // Si el cliente escribió una referencia, se respeta. El resto (Exclusión, Modificación, formato 10 CK, formato 11) no cambia.
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void En_una_inclusion_de_formato_06_sin_referencia_rige_la_identificacion(string referencia)
        {
            var fila = Fila("6", ("referencia", referencia), ("numeroIdentificacion", "  0011234567890a "), ("numeroCuenta", "123456789"));
            Assert.Equal("0011234567890a", fila.ReferenciaQueRige);
            Assert.Equal(referencia, fila.ReferenciaRecibida);
            Assert.Null(fila.ReferenciaConstruida);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void En_una_inclusion_de_formato_10_BAC_sin_referencia_rige_la_cuenta(string referencia)
        {
            var fila = Fila("10", ("clasificacion", "BAC"), ("referencia", referencia), ("numeroCuenta", "  123456789 "));
            Assert.Equal("123456789", fila.ReferenciaQueRige);
            Assert.Equal(referencia, fila.ReferenciaRecibida);
            Assert.Null(fila.ReferenciaConstruida);
        }

        [Theory]
        [InlineData("6", "BAC")]
        [InlineData("10", "BAC")]
        public void La_referencia_que_escribio_el_cliente_se_respeta(string plan, string clasificacion)
        {
            Assert.Equal("REF9", Fila(plan, ("clasificacion", clasificacion), ("referencia", "  REF9 ")).ReferenciaQueRige);
        }

        [Fact]
        public void Sin_referencia_y_sin_el_dato_que_la_reemplaza_rige_vacia()
        {
            Assert.Equal(string.Empty, Fila("6", ("referencia", ""), ("numeroIdentificacion", "")).ReferenciaQueRige);
            Assert.Equal(string.Empty, Fila("10", ("clasificacion", "BAC"), ("referencia", ""), ("numeroCuenta", "")).ReferenciaQueRige);
        }

        [Theory]
        [InlineData("6", "Exclusion", "BAC")]
        [InlineData("6", "Modificacion", "BAC")]
        [InlineData("6", "", "BAC")]
        [InlineData("10", "Inclusion", "CK")]
        [InlineData("10", "Inclusion", "")]
        [InlineData("10", "Exclusion", "BAC")]
        [InlineData("10", "Modificacion", "BAC")]
        public void Fuera_de_esos_dos_casos_la_referencia_vacia_sigue_vacia(string plan, string gestion, string clasificacion)
        {
            Assert.Equal(string.Empty, Fila(plan, ("gestion", gestion), ("clasificacion", clasificacion), ("referencia", "")).ReferenciaQueRige);
        }

        // ================================================================== el catálogo destino, por el motor
        [Theory]
        [InlineData("6")]
        [InlineData("10")]
        [InlineData("11")]
        public void Una_fila_buena_de_cada_formato_cumple_todo_el_catalogo_destino(string plan)
        {
            var rs = Correr(Fila(plan));
            Assert.All(rs, r => Assert.Equal(ResultadoDeLaRegla.Cumplida, r.Resultado));
        }

        [Fact]
        public void Una_exclusion_minima_en_formato_11_cumple_todo_el_catalogo_destino()
        {
            var rs = Correr(Fila("11", ("gestion", "Exclusion"), ("clasificacion", null), ("tipoIdentificacion", null), ("moneda", null), ("banco", null),
                ("nombreBeneficiario", null), ("numeroIdentificacion", null), ("referencia", "REFX1")));
            Assert.All(rs, r => Assert.Equal(ResultadoDeLaRegla.Cumplida, r.Resultado));
        }

        [Fact]
        public void Una_modificacion_en_formato_11_solo_la_rechaza_la_gestion_del_formato()
        {
            var rs = Correr(Fila("11", ("gestion", "Modificacion")));
            Assert.Equal(ResultadoDeLaRegla.NoCumplida, Resultado(rs, ReglasDeRegistro.GestionDelFormato));
            Assert.All(rs.Where(r => r.Codigo != ReglasDeRegistro.GestionDelFormato), r => Assert.Equal(ResultadoDeLaRegla.Cumplida, r.Resultado));
        }

        [Fact]
        public void Sobre_un_plan_ajeno_las_reglas_por_formato_quedan_omitidas_y_no_dicen_nada_del_plan()
        {
            // 0099 es formato 11 y el remitente no tiene permiso: si las reglas por formato hablaran, la fila diría qué
            // admite un plan ajeno (la fuga cerrada el 2026-09-24).
            var rs = Correr(Fila("99", ("gestion", "Modificacion"), ("clasificacion", "BAC"), ("numeroCuenta", "12345")));

            Assert.Equal(ResultadoDeLaRegla.NoCumplida, Resultado(rs, ReglasDeRegistro.AutorizacionCorreoPlan));
            foreach (var codigo in new[]
            {
                ReglasDeRegistro.GestionDelFormato, ReglasDeRegistro.ClasificacionDelFormato, ReglasDeRegistro.BancoDelFormato,
                ReglasDeRegistro.Obligatoriedad, ReglasDeRegistro.MonedaDelPlan, ReglasDeRegistro.ReferenciaFormato11, ReglasDeRegistro.CuentaDelBanco,
            })
            {
                Assert.Equal(ResultadoDeLaRegla.Omitida, Resultado(rs, codigo));
            }
        }

        [Fact]
        public void Los_escenarios_9_y_10_se_rechazan_en_la_fila_por_el_motor()
        {
            var nueve = Correr(Fila("6", ("clasificacion", "ACH")));
            Assert.Equal(ResultadoDeLaRegla.NoCumplida, Resultado(nueve, ReglasDeRegistro.ClasificacionDelFormato));

            var diez = Correr(Fila("11", ("clasificacion", "CK")));
            Assert.Equal(ResultadoDeLaRegla.NoCumplida, Resultado(diez, ReglasDeRegistro.ClasificacionDelFormato));
            // REFERENCIA_FORMATO_11 depende de la clasificación: queda Omitida, no suma un segundo motivo.
            Assert.Equal(ResultadoDeLaRegla.Omitida, Resultado(diez, ReglasDeRegistro.ReferenciaFormato11));
        }

        // ================================================================== revisión DN-05: lo que las mutaciones no detectaban
        [Fact]
        public void Obligatoriedad_mira_el_banco_y_la_clasificacion_efectivos()
        {
            // Con la obligatoriedad inicial (todo obligatorio salvo referencia), Banco y Clasificación vacíos en un plan 06 NO
            // faltan: el formato los completa (M17, y M16 por la clasificación).
            var catalogos = Catalogos(obligatoriedad: ParametrosDePlantillaAceptacion.ObligatoriedadInicial);
            Assert.True(Suelta(ReglasDeRegistro.Obligatoriedad, Fila(catalogos, "6", ("banco", ""), ("clasificacion", ""), ("moneda", "COR"))).Cumple);
            Assert.False(Suelta(ReglasDeRegistro.Obligatoriedad, Fila(catalogos, "11", ("banco", ""))).Cumple); // el 11 no completa banco
        }

        [Fact]
        public void Una_regla_de_obligatoriedad_por_clasificacion_calza_con_la_clasificacion_efectiva()
        {
            // M16: plan 11 con la clasificación vacía es ACH efectiva, y la regla de ACH afloja el Tipo ID.
            const string conReglaAch = @"{""porDefecto"":""obligatorio"",""opcionales"":[{""campo"":""referencia""}],""reglas"":[
                {""gestion"":""*"",""clasificacion"":""ACH"",""formato"":""11"",""opcionales"":[""tipoIdentificacion""]}]}";
            var catalogos = Catalogos(obligatoriedad: conReglaAch);
            Assert.True(Suelta(ReglasDeRegistro.Obligatoriedad, Fila(catalogos, "11", ("clasificacion", ""), ("tipoIdentificacion", ""))).Cumple);
        }

        [Fact]
        public void Con_varios_bancos_o_clasificaciones_admitidos_nada_se_completa()
        {
            // M37: completar exige UNA sola opción, también en bancos.
            const string varios = @"{
                ""06"": {""clasificaciones"": [""BAC"",""CK""], ""gestiones"": [""Inclusion""], ""bancos"": [""BAC"",""LAFISE""]},
                ""10"": {""clasificaciones"": [""BAC""], ""gestiones"": [""Inclusion""]},
                ""11"": {""clasificaciones"": [""ACH""], ""gestiones"": [""Inclusion""]}
            }";
            var fila = Fila(Catalogos(formatos: varios), "6", ("banco", ""), ("clasificacion", ""));
            Assert.Null(fila.BancoEfectivo);
            Assert.Null(fila.ClasificacionEfectiva);
        }

        [Theory]
        [InlineData("{\"06\": {\"clasificaciones\": [\"B\\u 41 C\"], \"gestiones\": [\"Inclusion\"]}, \"10\": {\"clasificaciones\": [\"BAC\"], \"gestiones\": [\"Inclusion\"]}, \"11\": {\"clasificaciones\": [\"ACH\"], \"gestiones\": [\"Inclusion\"]}}")]
        public void El_json_estricto_exige_escapes_de_cuatro_hexadecimales(string json)
        {
            // Revisión DN-05, hallazgo 6: "\u 41 " (con espacios) no es un escape válido, aunque int.Parse lo acepte y dé "BAC".
            // (El control crudo dentro de una cadena no se prueba acá: en estos dos parámetros todo texto se valida contra
            // un enum, así que no es observable.)
            Assert.Throws<FormatException>(() => FormatosDelPlan.DesdeJson(json));
        }

        [Theory]
        [InlineData("\u3164")] // relleno Hangul
        [InlineData("\u115F")] // relleno inicial Hangul
        [InlineData("\u2800")] // braille en blanco
        [InlineData("\uFE0F")] // selector de variación
        [InlineData("\u0301")] // marca combinante (Mn): la tilde de una "é" descompuesta
        [InlineData("\U000E0100")] // selector de variación fuera del plano básico: un par sustituto válido que no se ve
        public void Los_caracteres_que_no_se_ven_se_nombran_como_invisibles(string invisible)
        {
            // Revisión DN-05, hallazgo 7: relleno, braille en blanco, selectores de variación y marcas combinantes (Mn, Me)
            // tampoco se muestran crudos.
            var v = Suelta(ReglasDeRegistro.CaracteresPermitidos, Fila("6", ("nombreBeneficiario", "Jose" + invisible)));
            Assert.Equal("Caracteres no permitidos: Nombre ((carácter invisible)).", v.Razon);
        }
}
}
