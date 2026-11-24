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
    /// DN-07 (aprobador, 2026-11-24; diseno/DIRECTRICES-NUEVAS.md): el número de identificación se valida según su tipo, siempre
    /// que venga escrito. CNA: 13 dígitos + letra, fecha DDMMAA real y letra del Módulo 23. RUC: 14 caracteres; con forma de
    /// cédula se valida como cédula; con J, J + 13 dígitos. PAS y PEX: 6 a 9 letras o dígitos. CRE: sin cambio.
    /// Todas las cédulas de estas pruebas son SINTÉTICAS (calculadas con el algoritmo); nunca datos reales.
    /// PRUEBAS DEL ARQUITECTO: el constructor no las toca.
    /// </summary>
    public class Dn07Aceptacion
    {
        // ------------------------------------------------------------------ cédulas sintéticas
        private const string CedulaBuena = "0010101800000R";        // municipio 001, 01/01/80, letra correcta
        private const string CedulaBuena2 = "2812502920001F";       // 25/02/92
        private const string CedulaBuena3 = "4410802000123J";       // 08/02/00
        private const string Cedula29FebBisiesto = "0012902000005Y"; // 29/02/00: 2000 es bisiesto
        private const string Cedula31Dic = "9993112990009R";        // 31/12/99
        private const string Cedula29FebNoBisiesto = "0012902010005T"; // 29/02/01: no existe ni en 1901 ni en 2001 (letra correcta)
        private const string CedulaMes13 = "0013213800000M";        // 32/13: no existe (letra correcta)
        private const string CedulaDia31Abril = "0013104800000B";   // 31/04: no existe (letra correcta)
        private const string CedulaDiaCero = "0010000800000H";      // 00/00: no existe (letra correcta)
        private const string CedulaLetraMal = "0010101800000S";     // la correcta es R

        private const string MotivoCedulaForma = "El número de identificación no es una cédula válida: debe tener 13 dígitos y una letra al final, sin guiones.";
        private const string MotivoCedulaFecha = "El número de identificación no es una cédula válida: la fecha de nacimiento no existe.";
        private const string MotivoCedulaLetra = "El número de identificación no es una cédula válida: la letra final no corresponde al número.";
        private const string MotivoRucLargo = "El número de identificación no es un RUC válido: debe tener 14 caracteres.";
        private const string MotivoRucFecha = "El número de identificación no es un RUC válido: la fecha de nacimiento no existe.";
        private const string MotivoRucLetra = "El número de identificación no es un RUC válido: la letra final no corresponde al número.";
        private const string MotivoRucJuridico = "El número de identificación no es un RUC válido: después de la J debe tener 13 dígitos.";
        private const string MotivoPasaporte = "El número de identificación no es un pasaporte válido: debe tener entre 6 y 9 letras o números.";

        private static string M(TipoDeIdentificacion tipo, string numero) => IdentificacionPorTipo.Motivo(tipo, numero);

        // ================================================================== CNA
        [Theory]
        [InlineData(CedulaBuena)]
        [InlineData(CedulaBuena2)]
        [InlineData(CedulaBuena3)]
        [InlineData(Cedula29FebBisiesto)]
        [InlineData(Cedula31Dic)]
        public void Una_cedula_bien_formada_es_valida(string cedula)
        {
            Assert.Null(M(TipoDeIdentificacion.CNA, cedula));
        }

        [Fact]
        public void La_letra_en_minuscula_vale_igual()
        {
            Assert.Null(M(TipoDeIdentificacion.CNA, "0010101800000r"));
        }

        [Theory]
        [InlineData("001010180000R")]     // 12 dígitos + letra
        [InlineData("00101018000000R")]   // 14 dígitos + letra
        [InlineData("00101018000000")]    // 14 dígitos, sin letra
        [InlineData("0010101800000")]     // 13 dígitos, sin letra
        [InlineData("001010180000AR")]    // una letra en el lugar de un dígito
        [InlineData("R0010101800000")]    // la letra adelante
        public void Una_cedula_sin_13_digitos_y_letra_falla_por_forma(string cedula)
        {
            Assert.Equal(MotivoCedulaForma, M(TipoDeIdentificacion.CNA, cedula));
        }

        [Theory]
        [InlineData(Cedula29FebNoBisiesto)]
        [InlineData(CedulaMes13)]
        [InlineData(CedulaDia31Abril)]
        [InlineData(CedulaDiaCero)]
        public void Una_cedula_con_una_fecha_que_no_existe_falla_por_fecha(string cedula)
        {
            Assert.Equal(MotivoCedulaFecha, M(TipoDeIdentificacion.CNA, cedula));
        }

        [Fact]
        public void Una_cedula_con_la_letra_que_no_corresponde_falla_por_letra()
        {
            Assert.Equal(MotivoCedulaLetra, M(TipoDeIdentificacion.CNA, CedulaLetraMal));
        }

        [Fact]
        public void Las_23_letras_del_modulo_se_reconocen_y_ninguna_otra()
        {
            // Para cada residuo posible, un número de 13 dígitos con ese residuo y fecha válida; su letra tiene que ser la del alfabeto
            // oficial en esa posición, y cualquier otra letra tiene que fallar.
            const string alfabeto = "ABCDEFGHJKLMNPQRSTUVWXY";
            var hechos = new HashSet<long>();
            for (long consecutivo = 0; hechos.Count < 23; consecutivo++)
            {
                var trece = "001010180" + consecutivo.ToString("0000");
                var residuo = long.Parse(trece) % 23;
                if (!hechos.Add(residuo))
                {
                    continue;
                }

                Assert.Null(M(TipoDeIdentificacion.CNA, trece + alfabeto[(int)residuo]));
                foreach (var otra in "ABCDEFGHIJKLMNOPQRSTUVWXYZ".Where(l => l != alfabeto[(int)residuo]))
                {
                    Assert.Equal(MotivoCedulaLetra, M(TipoDeIdentificacion.CNA, trece + otra));
                }
            }
        }

        [Fact]
        public void La_fecha_se_revisa_antes_que_la_letra()
        {
            // 29/02/01 con una letra cualquiera: el motivo es la fecha, no la letra.
            Assert.Equal(MotivoCedulaFecha, M(TipoDeIdentificacion.CNA, "0012902010005A"));
        }

        // ================================================================== RUC
        [Theory]
        [InlineData(CedulaBuena)]          // persona natural: su cédula
        [InlineData("J0310000001234")]     // persona jurídica
        [InlineData("j0310000001234")]     // la J en minúscula
        [InlineData("E0000000012345")]     // otro contribuyente: solo el largo
        [InlineData("R12AB34CD56EF7")]
        [InlineData("N0000000000001")]
        [InlineData("0ABCDEFGHIJKLM")]     // empieza con 0 pero no tiene forma de cédula: solo el largo
        public void Un_ruc_valido_pasa(string ruc)
        {
            Assert.Null(M(TipoDeIdentificacion.RUC, ruc));
        }

        [Theory]
        [InlineData("J031000000123")]      // 13
        [InlineData("J03100000012345")]    // 15
        [InlineData("E000000001234")]
        public void Un_ruc_que_no_tiene_14_caracteres_falla_por_largo(string ruc)
        {
            Assert.Equal(MotivoRucLargo, M(TipoDeIdentificacion.RUC, ruc));
        }

        [Fact]
        public void Un_ruc_con_forma_de_cedula_se_valida_como_cedula_y_lo_dice_como_ruc()
        {
            Assert.Equal(MotivoRucLetra, M(TipoDeIdentificacion.RUC, CedulaLetraMal));
            Assert.Equal(MotivoRucFecha, M(TipoDeIdentificacion.RUC, CedulaMes13));
        }

        [Theory]
        [InlineData("J03100000012A4")]
        [InlineData("JJ310000001234")]
        public void Un_ruc_juridico_sin_13_digitos_despues_de_la_J_falla(string ruc)
        {
            Assert.Equal(MotivoRucJuridico, M(TipoDeIdentificacion.RUC, ruc));
        }

        // ================================================================== PAS y PEX
        [Theory]
        [InlineData(TipoDeIdentificacion.PAS, "C01X00T4")]
        [InlineData(TipoDeIdentificacion.PAS, "123456")]
        [InlineData(TipoDeIdentificacion.PAS, "ABC123456")]
        [InlineData(TipoDeIdentificacion.PEX, "a1234567")]
        [InlineData(TipoDeIdentificacion.PEX, "XYZ987654")]
        public void Un_pasaporte_de_6_a_9_letras_o_digitos_es_valido(TipoDeIdentificacion tipo, string pasaporte)
        {
            Assert.Null(M(tipo, pasaporte));
        }

        [Theory]
        [InlineData(TipoDeIdentificacion.PAS, "12345")]
        [InlineData(TipoDeIdentificacion.PAS, "ABC1234567")]
        [InlineData(TipoDeIdentificacion.PEX, "A1")]
        [InlineData(TipoDeIdentificacion.PEX, "0011234567890A")]
        public void Un_pasaporte_fuera_de_6_a_9_falla(TipoDeIdentificacion tipo, string pasaporte)
        {
            Assert.Equal(MotivoPasaporte, M(tipo, pasaporte));
        }

        // ================================================================== lo que no se mira
        [Theory]
        [InlineData("1")]
        [InlineData("cualquier123")]
        [InlineData("0011234567890A")]
        public void CRE_no_tiene_formato_y_pasa_siempre(string numero)
        {
            Assert.Null(M(TipoDeIdentificacion.CRE, numero));
        }

        [Theory]
        [InlineData(TipoDeIdentificacion.CNA)]
        [InlineData(TipoDeIdentificacion.RUC)]
        [InlineData(TipoDeIdentificacion.PAS)]
        public void Vacio_o_nulo_no_se_mira(TipoDeIdentificacion tipo)
        {
            Assert.Null(M(tipo, null));
            Assert.Null(M(tipo, ""));
            Assert.Null(M(tipo, "   "));
        }

        [Theory]
        [InlineData(TipoDeIdentificacion.CNA, "001-010180-0000R")]
        [InlineData(TipoDeIdentificacion.CNA, "001 010180 0000R")]
        [InlineData(TipoDeIdentificacion.RUC, "J-0310000001234")]
        [InlineData(TipoDeIdentificacion.PAS, "AB.12345")]
        [InlineData(TipoDeIdentificacion.CNA, "00101018000０0R")] // un dígito de ancho completo: no es ASCII
        public void Con_caracteres_que_no_son_letras_o_digitos_ASCII_no_se_mira(TipoDeIdentificacion tipo, string numero)
        {
            // Lo rechaza CARACTERES_PERMITIDOS: el cliente no recibe dos motivos por lo mismo.
            Assert.Null(M(tipo, numero));
        }

        [Fact]
        public void Se_recortan_los_espacios_de_los_lados()
        {
            Assert.Null(M(TipoDeIdentificacion.CNA, "  " + CedulaBuena + " "));
        }

        // ================================================================== la regla
        private const string Listas = @"{
            ""gestion"":[{""valor"":""Inclusion""},{""valor"":""Exclusion""},{""valor"":""Modificacion""}],
            ""clasificacion"":[{""valor"":""BAC""},{""valor"":""ACH""},{""valor"":""CK""}],
            ""tipoIdentificacion"":[{""valor"":""CNA""},{""valor"":""CRE""},{""valor"":""PAS""},{""valor"":""PEX""},{""valor"":""RUC""}],
            ""moneda"":[{""valor"":""COR""},{""valor"":""USD""}],
            ""banco"":[{""valor"":""LAFISE"",""codigo"":""006""},{""valor"":""BAC"",""codigo"":""007""}]
        }";

        private static readonly Dictionary<string, string> Columnas = CamposDeFila.Todos()
            .Select((campo, i) => (campo, columna: ((char)('C' + i)).ToString()))
            .ToDictionary(x => x.campo, x => x.columna);

        private static CatalogosDeValidacion Catalogos()
        {
            var campos = CamposDeFila.Todos().Select(c => new CampoPlantilla { Nombre = c, Columna = Columnas[c], EncabezadoEsperado = c }).ToList();
            var estructura = new ConfiguracionPlantilla { Hoja = "Datos", FilaEncabezado = 12, PrimeraFila = 13, CantidadFilas = 100, Campos = campos };
            var plan = Guid.Parse("00000000-0000-0000-0000-000000000011");
            return new CatalogosDeValidacion(estructura, ListasPlantilla.DesdeJson(Listas),
                ObligatoriedadPlantilla.DesdeJson(Dn05Aceptacion.ObligatoriedadV3),
                new List<PlanDelCatalogo> { new PlanDelCatalogo(plan, "0011", TipoDeFormatoDelPlan._11, Moneda.USD) }, new[] { plan });
        }

        private static FilaEnValidacion Fila(string tipo, string numero, string gestion = "Inclusion")
        {
            var porCampo = new Dictionary<string, string>
            {
                ["gestion"] = gestion, ["clasificacion"] = "ACH", ["tipoIdentificacion"] = tipo, ["moneda"] = "USD", ["banco"] = "LAFISE",
                ["numeroPlan"] = "11", ["nombreBeneficiario"] = "Juan Perez", ["numeroIdentificacion"] = numero, ["numeroCuenta"] = "123456789",
                ["referencia"] = "REF1",
            };
            var valores = porCampo.Where(p => p.Value != null).ToDictionary(p => Columnas[p.Key], p => p.Value);
            return new FilaEnValidacion(new FilaPlantilla { NumeroFilaExcel = 15, NumeroOrden = 3, Valores = valores }, Catalogos());
        }

        private static Veredicto Suelta(FilaEnValidacion fila) =>
            ReglasDeRegistro.Evaluadores().Single(e => e.Codigo == ReglasDeRegistro.IdentificacionDelTipo).Evaluar(fila);

        [Fact]
        public void La_regla_tiene_su_codigo_y_un_solo_evaluador()
        {
            Assert.Equal("IDENTIFICACION_DEL_TIPO", ReglasDeRegistro.IdentificacionDelTipo);
            Assert.Single(ReglasDeRegistro.Evaluadores(), e => e.Codigo == ReglasDeRegistro.IdentificacionDelTipo);
            Assert.Throws<ArgumentNullException>(() => Suelta(null));
        }

        [Fact]
        public void Una_fila_con_una_cedula_buena_cumple()
        {
            Assert.Equal(ResultadoDeLaRegla.Cumplida, Suelta(Fila("CNA", CedulaBuena)).Resultado);
        }

        [Fact]
        public void Una_fila_con_una_cedula_mala_falla_con_el_motivo_sin_marcadores_ni_el_numero()
        {
            var v = Suelta(Fila("CNA", CedulaLetraMal));
            Assert.Equal(ResultadoDeLaRegla.NoCumplida, v.Resultado);
            Assert.Equal(MotivoCedulaLetra, v.Razon);
            Assert.True(v.Marcadores == null || v.Marcadores.Count == 0);
            Assert.DoesNotContain(CedulaLetraMal, v.Razon);
        }

        [Theory]
        [InlineData("Modificacion")]
        [InlineData("Exclusion")]
        public void Se_valida_en_cualquier_gestion_si_viene_escrita(string gestion)
        {
            Assert.Equal(ResultadoDeLaRegla.NoCumplida, Suelta(Fila("CNA", CedulaLetraMal, gestion)).Resultado);
        }

        [Fact]
        public void El_tipo_escrito_en_minuscula_o_con_variante_de_la_lista_se_resuelve()
        {
            Assert.Equal(ResultadoDeLaRegla.NoCumplida, Suelta(Fila("cna", CedulaLetraMal)).Resultado);
        }

        [Theory]
        [InlineData(null, CedulaLetraMal)]   // sin tipo: no hay contra qué validar
        [InlineData("CNA", null)]            // sin número: lo decide OBLIGATORIEDAD
        [InlineData("XYZ", CedulaLetraMal)]  // tipo fuera de la lista: lo dice LISTAS_VALIDAS
        [InlineData("CRE", "1")]
        public void Sin_tipo_o_sin_numero_la_regla_se_cumple(string tipo, string numero)
        {
            Assert.Equal(ResultadoDeLaRegla.Cumplida, Suelta(Fila(tipo, numero)).Resultado);
        }

        [Fact]
        public void En_el_motor_depende_de_listas_validas()
        {
            var reglas = new List<DefinicionDeRegla>
            {
                new DefinicionDeRegla { Codigo = ReglasDeRegistro.ListasValidas, Orden = 10, DependeDe = new string[0], Efecto = EfectoDeLaRegla.Rechaza },
                new DefinicionDeRegla { Codigo = ReglasDeRegistro.IdentificacionDelTipo, Orden = 27, DependeDe = new[] { ReglasDeRegistro.ListasValidas }, Efecto = EfectoDeLaRegla.Rechaza },
            };
            var motor = new MotorDeReglas<FilaEnValidacion>(ReglasDeRegistro.Evaluadores());

            var conTipoMalo = motor.Evaluar(reglas, Fila("XYZ", CedulaLetraMal));
            Assert.Equal(ResultadoDeLaRegla.Omitida, conTipoMalo.Single(r => r.Codigo == ReglasDeRegistro.IdentificacionDelTipo).Resultado);

            var conCedulaMala = motor.Evaluar(reglas, Fila("CNA", CedulaLetraMal));
            var r27 = conCedulaMala.Single(r => r.Codigo == ReglasDeRegistro.IdentificacionDelTipo);
            Assert.Equal((ResultadoDeLaRegla.NoCumplida, MotivoCedulaLetra), (r27.Resultado, r27.Razon));
            Assert.Equal(EstadoDeLaFila.RechazadaEnValidacion, EstadosPorReglas.DeLaFila(conCedulaMala));
        }
    }
}
