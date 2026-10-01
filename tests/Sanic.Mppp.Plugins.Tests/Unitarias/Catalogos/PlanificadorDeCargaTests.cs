using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Sanic.Mppp.Plugins.Catalogos;
using Sanic.Mppp.Plugins.Dominio;
using Sanic.Mppp.Plugins.Plantilla;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Unitarias.Catalogos
{
    /// <summary>
    /// Comportamientos internos del planificador de DN-04 que las pruebas de aceptación no fijan: contratos de
    /// inmutabilidad, independencia de la cultura, redacción de los motivos y casos de borde de la foto.
    /// </summary>
    public class PlanificadorDeCargaTests
    {
        private static readonly DateTime Hoy = new DateTime(2026, 10, 1);

        private static Guid G(int n) => new Guid(n, 0, 0, new byte[8]);

        private static readonly Guid Sol = G(1);
        private static readonly Guid Luna = G(2);
        private static readonly Guid Ana = G(10);

        private static EstadoDeCatalogos Foto(bool conLunaEnLaFoto = true, params UsuarioCandidato[] usuariosExtra)
        {
            var clientes = new List<ClienteExistente> { new ClienteExistente(Sol, "000000123", "ABC123456001", "Ferretería Sol", Ana, true) };
            if (conLunaEnLaFoto) clientes.Add(new ClienteExistente(Luna, "000000456", "XYZ987654002", "Distribuidora Luna", Ana, true));
            var usuarios = new List<UsuarioCandidato> { new UsuarioCandidato(Ana, "ana@banco.com", true, true) };
            usuarios.AddRange(usuariosExtra);
            return new EstadoDeCatalogos(
                clientes,
                new[] { new PlanExistente(G(20), "0B34", Luna, TipoDeFormatoDelPlan._10, Moneda.USD, true) },
                new[] { new AutorizadoExistente(G(30), "juan.perez@empresa.com", "Juan Pérez", true) },
                new AutorizacionClienteExistente[0],
                usuarios);
        }

        private static FilaPlantilla F(int numero, params (string letra, string valor)[] valores)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (letra, valor) in valores) d[letra] = valor;
            return new FilaPlantilla { NumeroFilaExcel = numero, NumeroOrden = numero - 1, Valores = d };
        }

        private static PlanDeCarga Planificar(EstadoDeCatalogos foto, params FilaPlantilla[] filas) => PlanificadorDeCarga.Planificar(filas, foto, Hoy);

        // ------------------------------------------------------------------------------------------ inmutabilidad
        [Fact]
        public void Nada_de_lo_que_devuelve_el_plan_se_puede_modificar_desde_afuera()
        {
            var plan = Planificar(Foto(), F(2, ("A", "555"), ("B", "NUE000000555"), ("C", "Nueva"), ("D", "ana@banco.com"), ("H", "juan.perez@empresa.com")));
            Assert.Throws<NotSupportedException>(() => ((ICollection<ResultadoDeFilaDeCarga>)plan.Filas).Clear());
            Assert.Throws<NotSupportedException>(() => ((ICollection<ClienteACrear>)plan.Clientes).Clear());
            Assert.Throws<NotSupportedException>(() => ((ICollection<PlanACrear>)plan.Planes).Clear());
            Assert.Throws<NotSupportedException>(() => ((ICollection<AutorizadoACrear>)plan.Autorizados).Clear());
            Assert.Throws<NotSupportedException>(() => ((ICollection<AutorizacionACrear>)plan.Autorizaciones).Clear());
            var fila = plan.Filas[0];
            Assert.Throws<NotSupportedException>(() => ((ICollection<string>)fila.Errores).Add("x"));
            Assert.Throws<NotSupportedException>(() => ((ICollection<string>)fila.Advertencias).Add("x"));
            Assert.Throws<NotSupportedException>(() => ((ICollection<string>)fila.Creados).Add("x"));
        }

        [Fact]
        public void El_plan_no_cambia_si_quien_llamo_modifica_despues_la_lista_o_los_valores_de_sus_filas()
        {
            var fila = F(2, ("A", "555"), ("B", "NUE000000555"), ("C", "Nueva"), ("D", "ana@banco.com"));
            var filas = new List<FilaPlantilla> { fila };
            var plan = PlanificadorDeCarga.Planificar(filas, Foto(), Hoy);

            fila.Valores["C"] = "Otra cosa";
            filas.Clear();

            Assert.Single(plan.Filas);
            Assert.Equal("Nueva", Assert.Single(plan.Clientes).RazonSocial);
        }

        [Fact]
        public void Las_claves_no_se_pueden_modificar_desde_afuera()
        {
            var claves = ClavesDeCarga.Extraer(new[] { F(2, ("A", "1"), ("B", "ABC123456001"), ("D", "ana@banco.com"), ("E", "1"), ("H", "a@b.com")) });
            Assert.Throws<NotSupportedException>(() => ((ICollection<string>)claves.CifsBac).Add("x"));
            Assert.Throws<NotSupportedException>(() => ((ICollection<string>)claves.CifsCom).Add("x"));
            Assert.Throws<NotSupportedException>(() => ((ICollection<string>)claves.CodigosDePlan).Add("x"));
            Assert.Throws<NotSupportedException>(() => ((ICollection<string>)claves.Correos).Add("x"));
            Assert.Throws<NotSupportedException>(() => ((ICollection<string>)claves.CorreosDeEjecutivo).Add("x"));
        }

        [Fact]
        public void La_foto_copia_cada_una_de_sus_cinco_colecciones()
        {
            var clientes = new List<ClienteExistente>();
            var planes = new List<PlanExistente> { new PlanExistente(G(20), "0B34", Luna, TipoDeFormatoDelPlan._10, Moneda.USD, true) };
            var autorizados = new List<AutorizadoExistente> { new AutorizadoExistente(G(30), "a@b.com", "A", true) };
            var autorizaciones = new List<AutorizacionClienteExistente> { new AutorizacionClienteExistente(G(40), G(30), Sol, null, null, true) };
            var usuarios = new List<UsuarioCandidato> { new UsuarioCandidato(Ana, "ana@banco.com", true, true) };
            var foto = new EstadoDeCatalogos(clientes, planes, autorizados, autorizaciones, usuarios);

            planes.Clear(); autorizados.Clear(); autorizaciones.Clear(); usuarios.Clear();

            Assert.Single(foto.Planes);
            Assert.Single(foto.Autorizados);
            Assert.Single(foto.Autorizaciones);
            Assert.Single(foto.Usuarios);
            Assert.Throws<NotSupportedException>(() => ((ICollection<PlanExistente>)foto.Planes).Clear());
            Assert.Throws<NotSupportedException>(() => ((ICollection<AutorizadoExistente>)foto.Autorizados).Clear());
            Assert.Throws<NotSupportedException>(() => ((ICollection<AutorizacionClienteExistente>)foto.Autorizaciones).Clear());
            Assert.Throws<NotSupportedException>(() => ((ICollection<UsuarioCandidato>)foto.Usuarios).Clear());
        }

        // ------------------------------------------------------------------------------------------ cultura y reloj
        [Fact]
        public void El_resultado_es_el_mismo_con_otra_cultura_del_hilo()
        {
            FilaPlantilla[] Archivo() => new[]
            {
                F(2, ("A", "555"), ("B", "nue000000555"), ("C", "Iglesia Íntima"), ("D", "ANA@banco.com"), ("E", "i1"), ("F", "6"), ("G", "usd"), ("H", "INES@Empresa.com"), ("I", "Inés"), ("J", "1/9/2024")),
                F(3, ("A", "123"), ("C", "FERRETERÍA SOL"), ("E", "0b34"), ("G", "cor")),
                F(4, ("A", "123"), ("H", "juan.perez@empresa.com"), ("I", "JUAN PÉREZ"), ("J", "45566.5")),
            };

            string[] Textos(string cultura)
            {
                var original = CultureInfo.CurrentCulture;
                try
                {
                    CultureInfo.CurrentCulture = new CultureInfo(cultura);
                    return Planificar(Foto(), Archivo()).Filas.Select(f => f.Texto).ToArray();
                }
                finally
                {
                    CultureInfo.CurrentCulture = original;
                }
            }

            var invariante = Textos("");
            Assert.Equal(invariante, Textos("tr-TR"));
            Assert.Equal(invariante, Textos("fr-FR"));
        }

        [Fact]
        public void Hoy_con_hora_igual_cuenta_como_hoy()
        {
            var fila = F(2, ("A", "123"), ("H", "n@empresa.com"), ("I", "X"), ("J", "01/10/2026"));
            var plan = PlanificadorDeCarga.Planificar(new[] { fila }, Foto(), new DateTime(2026, 10, 1, 18, 30, 0));
            Assert.False(plan.TieneErrores);
        }

        [Fact]
        public void La_fecha_futura_dice_cual_es_y_cual_es_hoy()
        {
            var plan = Planificar(Foto(), F(2, ("A", "123"), ("H", "n@empresa.com"), ("I", "X"), ("J", "02/10/2026")));
            var error = Assert.Single(plan.Filas[0].Errores);
            Assert.StartsWith("Fecha documento: ", error);
            Assert.Contains("02/10/2026", error);
            Assert.Contains("01/10/2026", error);
        }

        // ------------------------------------------------------------------------------------------ redacción de motivos
        [Fact]
        public void Los_motivos_de_formato_no_arrastran_el_nombre_del_parametro_del_framework()
        {
            var plan = Planificar(Foto(), F(2, ("A", "12A"), ("B", "ABC12"), ("E", "ABCDE"), ("F", "06"), ("G", "COR"), ("H", "malo"), ("I", "X")));
            Assert.All(plan.Filas[0].Errores, e =>
            {
                Assert.DoesNotContain("Parameter", e);
                Assert.DoesNotContain("valor)", e);
                Assert.EndsWith(".", e);
            });
        }

        [Fact]
        public void Un_valor_enorme_se_acorta_en_el_motivo()
        {
            var plan = Planificar(Foto(), F(2, ("A", new string('9', 2000))));
            Assert.True(plan.Filas[0].Errores.All(e => e.Length < 400));
        }

        [Fact]
        public void Una_advertencia_repetida_en_la_misma_fila_se_dice_una_sola_vez()
        {
            var foto = new EstadoDeCatalogos(
                new[] { new ClienteExistente(Sol, "000000123", "ABC123456001", "Ferretería Sol", Ana, false) },
                new PlanExistente[0], new AutorizadoExistente[0], new AutorizacionClienteExistente[0],
                new[] { new UsuarioCandidato(Ana, "ana@banco.com", true, true) });
            var plan = Planificar(foto, F(2, ("A", "123"), ("B", "ABC123456001")));
            Assert.Single(plan.Filas[0].Advertencias);
        }

        [Fact]
        public void Con_errores_en_el_archivo_una_fila_sana_con_advertencia_dice_las_dos_cosas()
        {
            var foto = new EstadoDeCatalogos(
                new[] { new ClienteExistente(Sol, "000000123", "ABC123456001", "Ferretería Sol", Ana, false) },
                new PlanExistente[0], new AutorizadoExistente[0], new AutorizacionClienteExistente[0],
                new[] { new UsuarioCandidato(Ana, "ana@banco.com", true, true) });
            var plan = Planificar(foto, F(2, ("A", "123")), F(3, ("A", "12A")));
            var sana = plan.Filas[0];
            Assert.Equal("Sin errores — no se grabó: el archivo tiene errores en otras filas | Advertencia: " + sana.Advertencias.Single(), sana.Texto);
        }

        [Fact]
        public void Con_mas_de_cinco_filas_en_conflicto_se_nombran_las_primeras_y_se_cuenta_el_resto()
        {
            var filas = Enumerable.Range(2, 8).Select(n => F(n, ("A", "555"), ("B", "NUE000000555"), ("C", "Razon " + n), ("D", "ana@banco.com"))).ToArray();
            var plan = Planificar(Foto(), filas);
            var error = Assert.Single(plan.Filas[0].Errores, e => e.StartsWith("Razon social: ", StringComparison.Ordinal));
            Assert.Contains("otras 2", error);
        }

        // ------------------------------------------------------------------------------------------ bordes de la foto
        [Fact]
        public void Un_plan_de_un_cliente_que_no_esta_en_la_foto_se_denuncia_igual_sin_nombrarlo()
        {
            var plan = Planificar(Foto(conLunaEnLaFoto: false), F(2, ("A", "123"), ("E", "0B34")));
            var error = Assert.Single(plan.Filas[0].Errores, e => e.StartsWith("Codigo plan: ", StringComparison.Ordinal));
            Assert.Contains("0B34", error);
        }

        [Fact]
        public void Dos_usuarios_con_el_mismo_correo_donde_uno_es_el_dueno_no_objetan_a_un_cliente_existente()
        {
            var otraAna = new UsuarioCandidato(G(99), "ANA@banco.com", true, true);
            var plan = Planificar(Foto(true, otraAna), F(2, ("A", "123"), ("D", "ana@banco.com")));
            Assert.False(plan.TieneErrores);
        }

        [Fact]
        public void Un_usuario_con_correo_nulo_en_la_foto_no_rompe_la_busqueda()
        {
            var sinCorreo = new UsuarioCandidato(G(98), null, true, true);
            var plan = Planificar(Foto(true, sinCorreo), F(2, ("A", "555"), ("B", "NUE000000555"), ("C", "Nueva"), ("D", "ana@banco.com")));
            Assert.False(plan.TieneErrores);
        }

        [Fact]
        public void Una_fila_con_clave_invalida_no_inventa_el_error_de_cliente_ausente()
        {
            var plan = Planificar(Foto(), F(2, ("A", "12A")));
            Assert.DoesNotContain(plan.Filas[0].Errores, e => e.StartsWith("CIF BAC / CIF COM: ", StringComparison.Ordinal));
            Assert.Single(plan.Filas[0].Errores);
        }

        [Fact]
        public void Un_plan_que_se_crea_en_una_fila_y_se_nombra_en_otra_de_un_cliente_nuevo_apunta_al_mismo_cliente()
        {
            var plan = Planificar(Foto(),
                F(2, ("A", "555"), ("B", "NUE000000555"), ("C", "Nueva"), ("D", "ana@banco.com"), ("E", "p1"), ("F", "06"), ("G", "COR")),
                F(3, ("B", "NUE000000555"), ("E", "p1")));
            Assert.False(plan.TieneErrores);
            var p = Assert.Single(plan.Planes);
            Assert.Equal("000000555", p.Cliente.CifBacNuevo);
            Assert.Equal(2, p.FilaOrigen);
        }

        [Fact]
        public void Las_listas_de_creacion_salen_ordenadas_por_fila_de_origen()
        {
            var plan = Planificar(Foto(),
                F(9, ("A", "999"), ("B", "NUE000000999"), ("C", "Nueve"), ("D", "ana@banco.com")),
                F(4, ("A", "444"), ("B", "NUE000000444"), ("C", "Cuatro"), ("D", "ana@banco.com")));
            Assert.Equal(new[] { 4, 9 }, plan.Clientes.Select(c => c.FilaOrigen).ToArray());
        }

        // ------------------------------------------------------------------------------------------ segunda vuelta (revisión)
        [Fact]
        public void El_correo_del_ejecutivo_de_mas_de_320_dice_el_largo_y_no_se_cita_entero()
        {
            var enorme = new string('a', 400) + "@banco.com";
            var plan = Planificar(Foto(), F(2, ("A", "555"), ("B", "NUE000000555"), ("C", "Nueva"), ("D", enorme)));
            var error = Assert.Single(plan.Filas[0].Errores);
            Assert.StartsWith("Correo ejecutivo: tiene 410 caracteres y el máximo es 320.", error);
        }

        [Fact]
        public void Un_correo_de_ejecutivo_de_exactamente_320_es_valido()
        {
            var de320 = new string('a', 320 - "@banco.com".Length) + "@banco.com";
            var foto = Foto(true, new UsuarioCandidato(G(77), de320, true, true));
            var plan = Planificar(foto, F(2, ("A", "555"), ("B", "NUE000000555"), ("C", "Nueva"), ("D", de320)));
            Assert.False(plan.TieneErrores);
            Assert.Equal(G(77), Assert.Single(plan.Clientes).DuenoId);
        }

        [Fact]
        public void Ningun_motivo_cita_un_correo_de_320_caracteres_entero()
        {
            var largo = new string('a', 320 - "@banco.com".Length) + "@banco.com";
            var foto = new EstadoDeCatalogos(
                new[] { new ClienteExistente(Sol, "000000123", "ABC123456001", "Ferretería Sol", Ana, false) },
                new PlanExistente[0],
                new[] { new AutorizadoExistente(G(30), largo, "Nombre grabado", false) },
                new[] { new AutorizacionClienteExistente(G(40), G(30), Sol, null, "grabado", true) },
                new[] { new UsuarioCandidato(Ana, "ana@banco.com", true, true) });

            var plan = Planificar(foto,
                F(2, ("A", "123"), ("D", largo)),                                                   // ejecutivo que no es el dueño, cliente inactivo
                F(3, ("A", "123"), ("H", largo), ("I", "Otro nombre"), ("K", "otro")),              // correo y autorización existentes, datos distintos
                F(4, ("A", "999"), ("B", "NUE000000999"), ("C", "N"), ("D", largo), ("H", "x" + largo.Substring(1))),
                F(5, ("A", "999"), ("D", "x" + largo.Substring(1))));                               // ejecutivo contradictorio entre filas, correo nuevo sin nombre
            var mensajes = plan.Filas.SelectMany(f => f.Errores.Concat(f.Advertencias)).ToList();
            Assert.NotEmpty(mensajes);
            Assert.All(mensajes, m => Assert.True(m.Length < 400, $"motivo de {m.Length} caracteres: {m.Substring(0, 80)}"));
        }

        [Theory]
        [InlineData("31/12/1899", 1899)]
        [InlineData("01/01/0001", 1)]
        public void La_fecha_anterior_a_1900_dice_cual_es_y_desde_cuando_vale(string valor, int anio)
        {
            Assert.True(anio < 1900);
            var plan = Planificar(Foto(), F(2, ("A", "123"), ("H", "n@empresa.com"), ("I", "X"), ("J", valor)));
            var error = Assert.Single(plan.Filas[0].Errores);
            Assert.StartsWith("Fecha documento: ", error);
            Assert.Contains(valor, error);
            Assert.Contains("01/01/1900", error);
        }

        [Fact]
        public void El_serial_1_es_31_12_1899_y_es_error_pero_el_serial_2_es_el_1_de_enero_de_1900_y_vale()
        {
            Assert.Single(Planificar(Foto(), F(2, ("A", "123"), ("H", "n@empresa.com"), ("I", "X"), ("J", "1"))).Filas[0].Errores);
            Assert.False(Planificar(Foto(), F(2, ("A", "123"), ("H", "n@empresa.com"), ("I", "X"), ("J", "2"))).TieneErrores);
        }

        // ------------------------------------------------------------------------------------------ lo grabado vacío (revisión 4a)
        private static EstadoDeCatalogos FotoConAutorizacion(DateTime? fecha, string comentarios, string nombreContacto)
            => new EstadoDeCatalogos(
                new[] { new ClienteExistente(Sol, "000000123", "ABC123456001", "Ferretería Sol", Ana, true) },
                new PlanExistente[0],
                new[] { new AutorizadoExistente(G(30), "juan.perez@empresa.com", nombreContacto, true) },
                new[] { new AutorizacionClienteExistente(G(40), G(30), Sol, fecha, comentarios, true) },
                new[] { new UsuarioCandidato(Ana, "ana@banco.com", true, true) });

        [Fact]
        public void La_fecha_grabada_vacia_se_dice_sin_fecha_y_no_con_sin_fecha()
        {
            var foto = FotoConAutorizacion(null, "c", "Juan");
            var error = Assert.Single(PlanificadorDeCarga.Planificar(new[] { F(2, ("A", "123"), ("H", "juan.perez@empresa.com"), ("J", "01/10/2024")) }, foto, Hoy).Filas[0].Errores);
            Assert.StartsWith("Fecha documento: ", error);
            Assert.Contains("ya existe sin fecha", error);
            Assert.DoesNotContain("con sin", error);
            Assert.Contains("01/10/2024", error);
        }

        [Fact]
        public void La_fecha_grabada_se_cita_con_su_dia_y_la_del_archivo_tambien()
        {
            var foto = FotoConAutorizacion(new DateTime(2024, 9, 1), "c", "Juan");
            var error = Assert.Single(PlanificadorDeCarga.Planificar(new[] { F(2, ("A", "123"), ("H", "juan.perez@empresa.com"), ("J", "02/10/2024")) }, foto, Hoy).Filas[0].Errores);
            Assert.Contains("ya existe con fecha 01/09/2024, no con fecha 02/10/2024", error);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void El_comentario_y_el_nombre_grabados_vacios_se_dicen_sin_citar_comillas_vacias(string vacio)
        {
            var foto = FotoConAutorizacion(null, vacio, vacio);
            var fila = PlanificadorDeCarga.Planificar(new[] { F(2, ("A", "123"), ("H", "juan.perez@empresa.com"), ("I", "Juan"), ("K", "algo")) }, foto, Hoy).Filas[0];
            Assert.Contains("ya existe sin nombre de contacto", Assert.Single(fila.Errores, e => e.StartsWith("Nombre contacto: ", StringComparison.Ordinal)));
            Assert.Contains("ya existe sin comentario", Assert.Single(fila.Errores, e => e.StartsWith("Comentarios: ", StringComparison.Ordinal)));
            Assert.All(fila.Errores, e => Assert.DoesNotContain("\"\"", e));
        }

        [Fact]
        public void El_comentario_y_el_nombre_grabados_se_citan_y_los_del_archivo_tambien()
        {
            var foto = FotoConAutorizacion(null, "Firmado", "Juan Pérez");
            var fila = PlanificadorDeCarga.Planificar(new[] { F(2, ("A", "123"), ("H", "juan.perez@empresa.com"), ("I", "Juan P."), ("K", "Otro")) }, foto, Hoy).Filas[0];
            Assert.Contains("ya existe con el nombre \"Juan Pérez\", no con el nombre \"Juan P.\"", fila.Errores.Single(e => e.StartsWith("Nombre contacto: ", StringComparison.Ordinal)));
            Assert.Contains("ya existe con el comentario \"Firmado\", no con el comentario \"Otro\"", fila.Errores.Single(e => e.StartsWith("Comentarios: ", StringComparison.Ordinal)));
        }
    }
}
