using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Sanic.Mppp.Plugins.Catalogos;
using Sanic.Mppp.Plugins.Dominio;
using Sanic.Mppp.Plugins.Plantilla;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Unitarias.Catalogos
{
    /// <summary>
    /// Comportamientos internos del planificador del cambio de estado (DN-04, Excel 2) que las pruebas de aceptación no fijan:
    /// redacción de los motivos, cuándo se advierte un efecto, contratos de inmutabilidad y de cultura.
    /// </summary>
    public class CambioDeEstadoTests
    {
        private static Guid G(int n) => new Guid(n, 0, 0, new byte[8]);

        private static readonly Guid Sol = G(1);
        private static readonly Guid Luna = G(2);
        private static readonly Guid Dormido = G(3);
        private static readonly Guid Ana = G(10);
        private static readonly Guid Juan = G(30);
        private static readonly Guid Maria = G(31);

        // Sol activo con DOS autorizaciones activas (Juan, Pedro); Dormido inactivo con UNA autorización activa; Luna activo con una inactiva;
        // María inactiva con una autorización activa sobre Sol.
        private static EstadoDeCatalogos Foto() => new EstadoDeCatalogos(
            new[]
            {
                new ClienteExistente(Sol, "000000123", "ABC123456001", "Ferretería Sol", Ana, true),
                new ClienteExistente(Luna, "000000456", "XYZ987654002", "Distribuidora Luna", Ana, true),
                new ClienteExistente(Dormido, "000000789", "INA000000003", "Cliente Dormido", Ana, false),
            },
            new[] { new PlanExistente(G(20), "0A12", Sol, TipoDeFormatoDelPlan._06, Moneda.COR, true) },
            new[]
            {
                new AutorizadoExistente(Juan, "juan.perez@empresa.com", "Juan Pérez", true),
                new AutorizadoExistente(Maria, "maria.lopez@empresa.com", "María López", false),
                new AutorizadoExistente(G(32), "pedro@empresa.com", "Pedro", true),
            },
            new[]
            {
                new AutorizacionClienteExistente(G(40), Juan, Sol, null, null, true),
                new AutorizacionClienteExistente(G(41), Juan, Luna, null, null, false),
                new AutorizacionClienteExistente(G(42), G(32), Sol, null, null, true),
                new AutorizacionClienteExistente(G(43), Maria, Sol, null, null, true),
                new AutorizacionClienteExistente(G(44), G(32), Dormido, null, null, true),
            },
            new UsuarioCandidato[0]);

        private static FilaPlantilla F(int numero, string tipo, string accion, string cifBac = null, string cifCom = null, string plan = null, string correo = null)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["A"] = tipo, ["B"] = accion, ["C"] = cifBac, ["D"] = cifCom, ["E"] = plan, ["F"] = correo,
            };
            return new FilaPlantilla { NumeroFilaExcel = numero, NumeroOrden = numero - 1, Valores = d };
        }

        private static PlanDeCambioDeEstado Planificar(params FilaPlantilla[] filas) => PlanificadorDeCambioDeEstado.Planificar(filas, Foto());

        // ------------------------------------------------------------------------------------------ claves y tipos
        [Fact]
        public void Una_celda_que_el_tipo_no_usa_da_solo_el_error_de_sobrante_y_no_el_de_formato()
        {
            var fila = Planificar(F(2, "CLIENTE", "ACTIVAR", "123", plan: "ABCDE")).Filas[0];
            var error = Assert.Single(fila.Errores);
            Assert.StartsWith("Codigo plan: ", error);
            Assert.Contains("CLIENTE", error);
        }

        [Fact]
        public void Un_cliente_con_los_dos_CIF_que_coinciden_y_uno_sobrante_dice_cual_sobra()
        {
            var fila = Planificar(F(2, "PLAN", "ACTIVAR", "123", "ABC123456001", "0A12")).Filas[0];
            Assert.Contains(fila.Errores, e => e.StartsWith("CIF BAC: ", StringComparison.Ordinal));
            Assert.Contains(fila.Errores, e => e.StartsWith("CIF COM: ", StringComparison.Ordinal));
            Assert.Equal(2, fila.Errores.Count);
        }

        [Fact]
        public void Un_correo_de_mas_de_320_caracteres_dice_el_largo_y_no_se_cita()
        {
            var enorme = new string('a', 400) + "@empresa.com";
            var error = Assert.Single(Planificar(F(2, "CORREO", "ACTIVAR", correo: enorme)).Filas[0].Errores);
            Assert.StartsWith("Correo: tiene 412 caracteres y el máximo es 320.", error);
        }

        [Fact]
        public void Un_cliente_inexistente_dice_por_cual_CIF_se_busco()
        {
            var porBac = Assert.Single(Planificar(F(2, "CLIENTE", "ACTIVAR", "999")).Filas[0].Errores);
            Assert.StartsWith("CIF BAC / CIF COM: ", porBac);
            Assert.Contains("000000999", porBac);

            var porCom = Assert.Single(Planificar(F(2, "CLIENTE", "ACTIVAR", cifCom: "NOE000000999")).Filas[0].Errores);
            Assert.Contains("NOE000000999", porCom);
        }

        [Fact]
        public void Una_autorizacion_que_no_existe_como_par_nombra_el_correo_y_el_cliente()
        {
            var error = Assert.Single(Planificar(F(2, "AUTORIZACION_CLIENTE", "ACTIVAR", "789", correo: "juan.perez@empresa.com")).Filas[0].Errores);
            Assert.StartsWith("Correo: ", error);
            Assert.Contains("juan.perez@empresa.com", error);
            Assert.Contains("000000789", error);
        }

        [Fact]
        public void Una_autorizacion_con_cliente_y_correo_inexistentes_reporta_los_dos_errores()
        {
            var fila = Planificar(F(2, "AUTORIZACION_CLIENTE", "ACTIVAR", "999", correo: "nadie@empresa.com")).Filas[0];
            Assert.Contains(fila.Errores, e => e.StartsWith("CIF BAC / CIF COM: ", StringComparison.Ordinal));
            Assert.Contains(fila.Errores, e => e.StartsWith("Correo: ", StringComparison.Ordinal));
        }

        // ------------------------------------------------------------------------------------------ repetidos
        [Fact]
        public void Un_cliente_nombrado_por_CIF_BAC_en_una_fila_y_por_CIF_COM_en_otra_es_el_mismo_registro()
        {
            var plan = Planificar(F(2, "CLIENTE", "ACTIVAR", "789"), F(3, "CLIENTE", "ACTIVAR", cifCom: "INA000000003"), F(4, "CLIENTE", "ACTIVAR", "789", "INA000000003"));
            Assert.False(plan.TieneErrores);
            Assert.Single(plan.Cambios);
            Assert.StartsWith("Activado", plan.Filas[0].Texto);
            Assert.StartsWith("Sin cambios: lo pide una fila anterior", plan.Filas[1].Texto);
            Assert.StartsWith("Sin cambios: lo pide una fila anterior", plan.Filas[2].Texto);
            Assert.Equal(2, plan.SinCambios);
        }

        [Fact]
        public void La_repetida_que_ya_estaba_en_el_estado_pedido_no_cuenta_el_cambio_dos_veces()
        {
            var plan = Planificar(F(2, "CLIENTE", "ACTIVAR", "123"), F(3, "CLIENTE", "ACTIVAR", "123"));
            Assert.False(plan.TieneErrores);
            Assert.Empty(plan.Cambios);
            Assert.Equal("Sin cambios: ya estaba activo", plan.Filas[0].Texto);
            Assert.StartsWith("Sin cambios: lo pide una fila anterior", plan.Filas[1].Texto);
            Assert.Equal(2, plan.SinCambios);
        }

        [Fact]
        public void La_advertencia_de_repetida_nombra_la_fila_que_lo_aplica()
        {
            var plan = Planificar(F(9, "CLIENTE", "ACTIVAR", "789"), F(4, "CLIENTE", "ACTIVAR", "789"), F(6, "CLIENTE", "ACTIVAR", "789"));
            var advertencia = Assert.Single(plan.Filas[0].Advertencias, a => a.StartsWith("Accion: ", StringComparison.Ordinal));
            Assert.Matches(@"\b4\b", advertencia);
            Assert.Equal(4, Assert.Single(plan.Cambios).FilaOrigen);
        }

        [Fact]
        public void Con_tres_filas_y_dos_acciones_cada_fila_nombra_a_las_que_piden_lo_contrario()
        {
            var plan = Planificar(
                F(2, "PLAN", "ACTIVAR", plan: "0A12"),
                F(3, "PLAN", "DESACTIVAR", plan: "0A12"),
                F(4, "PLAN", "DESACTIVAR", plan: "0A12"));
            Assert.True(plan.TieneErrores);
            var primera = Assert.Single(plan.Filas[0].Errores);
            Assert.Matches(@"\b3\b", primera);
            Assert.Matches(@"\b4\b", primera);
            var segunda = Assert.Single(plan.Filas[1].Errores);
            Assert.Matches(@"\b2\b", segunda);
            Assert.DoesNotMatch(@"\b4\b", segunda);
            Assert.Empty(plan.Cambios);
        }

        // ------------------------------------------------------------------------------------------ advertencias de efecto
        [Fact]
        public void La_advertencia_cuenta_en_singular_y_en_plural()
        {
            var plural = Planificar(F(2, "CLIENTE", "DESACTIVAR", "123")).Filas[0].Advertencias.Single();
            Assert.StartsWith("CIF BAC / CIF COM: ", plural);
            Assert.Contains("3 autorizaciones por cliente activas", plural); // Juan, Pedro y María sobre Sol

            var singular = Planificar(F(2, "CORREO", "DESACTIVAR", correo: "juan.perez@empresa.com")).Filas[0].Advertencias.Single();
            Assert.StartsWith("Correo: ", singular);
            Assert.Contains("1 autorización por cliente activa", singular);
        }

        [Fact]
        public void Desactivar_algo_que_ya_estaba_inactivo_no_advierte_aunque_tenga_autorizaciones_activas()
        {
            // Dormido ya está inactivo: sus autorizaciones ya no valen. Advertir "deja sin efecto" sería decir algo falso.
            var fila = Planificar(F(2, "CLIENTE", "DESACTIVAR", "789")).Filas[0];
            Assert.Empty(fila.Advertencias);
            Assert.Equal("Sin cambios: ya estaba inactivo", fila.Texto);
        }

        [Fact]
        public void Activar_un_cliente_o_un_correo_nunca_advierte_efecto()
        {
            Assert.Empty(Planificar(F(2, "CLIENTE", "ACTIVAR", "789")).Filas[0].Advertencias);
            Assert.Empty(Planificar(F(2, "CORREO", "ACTIVAR", correo: "maria.lopez@empresa.com")).Filas[0].Advertencias);
        }

        [Fact]
        public void Activar_una_autorizacion_con_cliente_y_correo_inactivos_advierte_las_dos_cosas()
        {
            var foto = new EstadoDeCatalogos(
                new[] { new ClienteExistente(Dormido, "000000789", "INA000000003", "Cliente Dormido", Ana, false) },
                new PlanExistente[0],
                new[] { new AutorizadoExistente(Maria, "maria.lopez@empresa.com", "María López", false) },
                new[] { new AutorizacionClienteExistente(G(50), Maria, Dormido, null, null, false) },
                new UsuarioCandidato[0]);
            var fila = PlanificadorDeCambioDeEstado.Planificar(new[] { F(2, "AUTORIZACION_CLIENTE", "ACTIVAR", "789", correo: "maria.lopez@empresa.com") }, foto).Filas[0];
            Assert.Contains(fila.Advertencias, a => a.StartsWith("CIF BAC / CIF COM: ", StringComparison.Ordinal));
            Assert.Contains(fila.Advertencias, a => a.StartsWith("Correo: ", StringComparison.Ordinal));
        }

        [Fact]
        public void Desactivar_una_autorizacion_no_advierte_efecto()
        {
            Assert.Empty(Planificar(F(2, "AUTORIZACION_CLIENTE", "DESACTIVAR", "123", correo: "juan.perez@empresa.com")).Filas[0].Advertencias);
        }

        [Fact]
        public void Activar_una_autorizacion_ya_activa_no_cambia_pero_advierte_si_su_correo_o_su_cliente_estan_inactivos()
        {
            // Decisión del arquitecto (2026-10-01, revisión): quien pide ACTIVAR cree que la autorización opera, cambie o no el estado.
            var correoInactivo = Planificar(F(2, "AUTORIZACION_CLIENTE", "ACTIVAR", "123", correo: "maria.lopez@empresa.com")).Filas[0];
            Assert.StartsWith("Sin cambios: ya estaba activo", correoInactivo.Texto);
            Assert.Contains(correoInactivo.Advertencias, a => a.StartsWith("Correo: ", StringComparison.Ordinal));

            var clienteInactivo = Planificar(F(2, "AUTORIZACION_CLIENTE", "ACTIVAR", "789", correo: "pedro@empresa.com")).Filas[0];
            Assert.StartsWith("Sin cambios: ya estaba activo", clienteInactivo.Texto);
            Assert.Contains(clienteInactivo.Advertencias, a => a.StartsWith("CIF BAC / CIF COM: ", StringComparison.Ordinal));
        }

        [Fact]
        public void Activar_una_autorizacion_ya_activa_con_todo_activo_no_advierte_nada()
        {
            var fila = Planificar(F(2, "AUTORIZACION_CLIENTE", "ACTIVAR", "123", correo: "juan.perez@empresa.com")).Filas[0];
            Assert.Equal("Sin cambios: ya estaba activo", fila.Texto);
        }

        [Fact]
        public void El_efecto_cuenta_la_autorizacion_que_el_archivo_activa()
        {
            // Juan→Luna está inactiva en la foto; el archivo la activa y desactiva a Luna: Luna deja sin efecto esa autorización.
            var plan = Planificar(
                F(2, "CLIENTE", "DESACTIVAR", "456"),
                F(3, "AUTORIZACION_CLIENTE", "ACTIVAR", "456", correo: "juan.perez@empresa.com"));
            Assert.False(plan.TieneErrores);
            Assert.Contains("1 autorización por cliente activa", Assert.Single(plan.Filas[0].Advertencias));
        }

        // ------------------------------------------------------------------------------------------ texto, orden y contratos
        [Fact]
        public void Una_fila_sana_en_un_archivo_con_errores_conserva_su_advertencia()
        {
            var plan = Planificar(F(2, "CLIENTE", "DESACTIVAR", "123"), F(3, "PLAN", "ACTIVAR", plan: "ZZZZ"));
            var sana = plan.Filas[0];
            Assert.Equal("Sin errores — no se grabó: el archivo tiene errores en otras filas | Advertencia: " + sana.Advertencias.Single(), sana.Texto);
        }

        [Fact]
        public void Los_cambios_salen_ordenados_por_fila_de_origen_y_son_de_solo_lectura()
        {
            var plan = Planificar(F(9, "CLIENTE", "ACTIVAR", "789"), F(4, "PLAN", "DESACTIVAR", plan: "0A12"));
            Assert.Equal(new[] { 4, 9 }, plan.Cambios.Select(c => c.FilaOrigen).ToArray());
            Assert.Equal(new[] { 9, 4 }, plan.Filas.Select(f => f.NumeroFilaExcel).ToArray());
            Assert.Throws<NotSupportedException>(() => ((ICollection<ResultadoDeFilaDeCambio>)plan.Filas).Clear());
            Assert.Throws<NotSupportedException>(() => ((ICollection<string>)plan.Filas[0].Errores).Add("x"));
            Assert.Throws<NotSupportedException>(() => ((ICollection<string>)plan.Filas[0].Advertencias).Add("x"));
        }

        [Fact]
        public void El_plan_no_cambia_si_quien_llamo_modifica_despues_sus_filas()
        {
            var fila = F(2, "CLIENTE", "ACTIVAR", "789");
            var lista = new List<FilaPlantilla> { fila };
            var plan = PlanificadorDeCambioDeEstado.Planificar(lista, Foto());
            fila.Valores["A"] = "PLAN";
            lista.Clear();
            Assert.Equal(TipoDeCatalogo.Cliente, Assert.Single(plan.Cambios).Tipo);
        }

        [Fact]
        public void Las_claves_no_se_pueden_modificar_desde_afuera()
        {
            var claves = ClavesDeCambioDeEstado.Extraer(new[] { F(2, "CLIENTE", "ACTIVAR", "1", "ABC123456001", "1", "a@b.com") });
            Assert.Throws<NotSupportedException>(() => ((ICollection<string>)claves.CifsBac).Add("x"));
            Assert.Throws<NotSupportedException>(() => ((ICollection<string>)claves.CifsCom).Add("x"));
            Assert.Throws<NotSupportedException>(() => ((ICollection<string>)claves.CodigosDePlan).Add("x"));
            Assert.Throws<NotSupportedException>(() => ((ICollection<string>)claves.Correos).Add("x"));
        }

        [Fact]
        public void Las_claves_incluyen_las_celdas_que_el_tipo_no_usa_si_estan_bien_formadas_y_rechazan_nulos()
        {
            // La foto se arma con todo lo que el archivo nombra; una celda sobrante igual da error en su fila, así que traerla no daña.
            var claves = ClavesDeCambioDeEstado.Extraer(new[] { F(2, "CLIENTE", "ACTIVAR", "123", plan: "0A12") });
            Assert.Equal(new[] { "0A12" }, claves.CodigosDePlan.ToArray());
            Assert.ThrowsAny<ArgumentException>(() => ClavesDeCambioDeEstado.Extraer(new[] { F(2, "CLIENTE", "ACTIVAR", "1"), null }));
        }

        [Fact]
        public void El_resultado_es_el_mismo_con_otra_cultura_del_hilo()
        {
            FilaPlantilla[] Archivo() => new[]
            {
                F(2, "cliente", " desactivar ", "123"),
                F(3, "CORREO", "ACTIVAR", correo: "MARIA.LOPEZ@empresa.com"),
                F(4, "plan", "activar", plan: "a12"),
                F(5, "AUTORIZACION_CLIENTE", "ACTIVAR", "123", correo: "maria.lopez@empresa.com"),
            };

            string[] Textos(string cultura)
            {
                var original = CultureInfo.CurrentCulture;
                try
                {
                    CultureInfo.CurrentCulture = new CultureInfo(cultura);
                    return PlanificadorDeCambioDeEstado.Planificar(Archivo(), Foto()).Filas.Select(f => f.Texto).ToArray();
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
        public void Ningun_motivo_de_tipo_o_accion_cita_el_valor_entero()
        {
            var enorme = new string('x', 5000);
            var fila = Planificar(F(2, enorme, enorme, "123")).Filas[0];
            Assert.All(fila.Errores, e => Assert.True(e.Length < 400, $"motivo de {e.Length} caracteres"));
            Assert.Contains(fila.Errores, e => e.StartsWith("Tipo: ", StringComparison.Ordinal));
            Assert.Contains(fila.Errores, e => e.StartsWith("Accion: ", StringComparison.Ordinal));
        }

        [Fact]
        public void El_motivo_de_tipo_o_accion_dice_cuales_son_los_valores_validos()
        {
            var tipo = Planificar(F(2, "CLIENTES", "ACTIVAR", "123")).Filas[0].Errores.Single();
            foreach (var valido in new[] { "CLIENTE", "PLAN", "CORREO", "AUTORIZACION_CLIENTE" }) Assert.Contains(valido, tipo);
            var accion = Planificar(F(2, "CLIENTE", "BORRAR", "123")).Filas[0].Errores.Single();
            Assert.Contains("ACTIVAR", accion);
            Assert.Contains("DESACTIVAR", accion);
        }

        [Fact]
        public void Sin_tipo_ni_accion_el_motivo_dice_que_faltan()
        {
            var fila = Planificar(F(2, null, null, "123")).Filas[0];
            Assert.Matches(new Regex("^Tipo: .*(falta|vac)", RegexOptions.IgnoreCase), fila.Errores.First(e => e.StartsWith("Tipo: ", StringComparison.Ordinal)));
            Assert.Contains(fila.Errores, e => e.StartsWith("Accion: ", StringComparison.Ordinal));
        }

        // ------------------------------------------------------------------------------------------ segunda vuelta (revisión)
        [Fact]
        public void Una_repetida_con_otro_error_tambien_lleva_la_advertencia_de_repetida()
        {
            var plan = Planificar(F(2, "CLIENTE", "ACTIVAR", "789"), F(3, "CLIENTE", "ACTIVAR", "789", plan: "0A12"));
            Assert.True(plan.TieneErrores);
            Assert.Contains(plan.Filas[1].Errores, e => e.StartsWith("Codigo plan: ", StringComparison.Ordinal));
            var advertencia = Assert.Single(plan.Filas[1].Advertencias, a => a.StartsWith("Accion: ", StringComparison.Ordinal));
            Assert.Matches(@"\b2\b", advertencia);
        }

        [Fact]
        public void El_estado_final_no_cuenta_las_filas_con_error()
        {
            // La fila 3 desactivaría Juan→Sol, pero tiene una celda de más: no entra al estado final. Sol sigue con sus 3 activas.
            var plan = Planificar(
                F(2, "CLIENTE", "DESACTIVAR", "123"),
                F(3, "AUTORIZACION_CLIENTE", "DESACTIVAR", "123", plan: "0A12", correo: "juan.perez@empresa.com"));
            Assert.True(plan.TieneErrores);
            Assert.Contains("3 autorizaciones por cliente activas", Assert.Single(plan.Filas[0].Advertencias));
        }

        [Fact]
        public void Opuestas_con_otro_error_en_una_de_las_filas_no_rompen_el_planificador_ni_inventan_cambios()
        {
            var plan = Planificar(
                F(2, "PLAN", "ACTIVAR", plan: "0A12"),
                F(3, "PLAN", "DESACTIVAR", plan: "0A12", correo: "juan.perez@empresa.com"),
                F(4, "CORREO", "DESACTIVAR", correo: "juan.perez@empresa.com"));
            Assert.True(plan.TieneErrores);
            Assert.Empty(plan.Cambios);
            Assert.Equal(0, plan.SinCambios);
            Assert.Equal(2, plan.Filas[1].Errores.Count);
        }
    }
}
