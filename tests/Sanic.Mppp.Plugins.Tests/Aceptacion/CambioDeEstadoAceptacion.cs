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
    /// DN-04 (diseno/DIRECTRICES-NUEVAS.md), Excel 2 "ActivarDesactivar": el planificador del cambio de estado (pasada 1).
    /// Pruebas del ARQUITECTO: el constructor no las cambia; si una le parece imposible o contradictoria, para y reporta.
    /// </summary>
    public class CambioDeEstadoAceptacion
    {
        // ================================================================================================ armado
        private static Guid G(int n) => new Guid(n, 0, 0, new byte[8]);

        private static readonly Guid Sol = G(1);        // 000000123 / ABC123456001, activo
        private static readonly Guid Luna = G(2);       // 000000456 / XYZ987654002, activo
        private static readonly Guid Dormido = G(3);    // 000000789 / INA000000003, inactivo
        private static readonly Guid Ana = G(10);
        private static readonly Guid PlanSol = G(20);   // 0A12, activo
        private static readonly Guid PlanDormido = G(22); // 0C56, inactivo
        private static readonly Guid Juan = G(30);      // activo
        private static readonly Guid Maria = G(31);     // INACTIVO
        private static readonly Guid Pedro = G(32);     // activo
        private static readonly Guid JuanSol = G(40);   // activa
        private static readonly Guid JuanLuna = G(41);  // INACTIVA
        private static readonly Guid PedroSol = G(42);  // activa
        private static readonly Guid MariaLuna = G(43); // activa (correo inactivo)
        private static readonly Guid MariaSol = G(44);  // INACTIVA (correo inactivo)

        private static EstadoDeCatalogos Foto() => new EstadoDeCatalogos(
            new[]
            {
                new ClienteExistente(Sol, "000000123", "ABC123456001", "Ferretería Sol", Ana, true),
                new ClienteExistente(Luna, "000000456", "XYZ987654002", "Distribuidora Luna", Ana, true),
                new ClienteExistente(Dormido, "000000789", "INA000000003", "Cliente Dormido", Ana, false),
            },
            new[]
            {
                new PlanExistente(PlanSol, "0A12", Sol, TipoDeFormatoDelPlan._06, Moneda.COR, true),
                new PlanExistente(PlanDormido, "0C56", Sol, TipoDeFormatoDelPlan._11, Moneda.USD, false),
            },
            new[]
            {
                new AutorizadoExistente(Juan, "juan.perez@empresa.com", "Juan Pérez", true),
                new AutorizadoExistente(Maria, "maria.lopez@empresa.com", "María López", false),
                new AutorizadoExistente(Pedro, "pedro@empresa.com", "Pedro", true),
            },
            new[]
            {
                new AutorizacionClienteExistente(JuanSol, Juan, Sol, null, null, true),
                new AutorizacionClienteExistente(JuanLuna, Juan, Luna, null, null, false),
                new AutorizacionClienteExistente(PedroSol, Pedro, Sol, null, null, true),
                new AutorizacionClienteExistente(MariaLuna, Maria, Luna, null, null, true),
                new AutorizacionClienteExistente(MariaSol, Maria, Sol, null, null, false),
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

        private static ResultadoDeFilaDeCambio Unica(PlanDeCambioDeEstado plan) => Assert.Single(plan.Filas);

        private static void Error(ResultadoDeFilaDeCambio fila, string prefijo)
            => Assert.Contains(fila.Errores, e => e.StartsWith(prefijo, StringComparison.Ordinal));

        private static void SinErrores(PlanDeCambioDeEstado plan)
            => Assert.False(plan.TieneErrores, string.Join(" || ", plan.Filas.SelectMany(f => f.Errores)));

        private static CambioDeEstadoACrear UnCambio(PlanDeCambioDeEstado plan) => Assert.Single(plan.Cambios);

        // ================================================================================================ estructura fija
        [Fact]
        public void Las_seis_columnas_A_a_F_con_sus_encabezados_exactos()
        {
            var esperado = new[]
            {
                ("A", "tipo", "Tipo"), ("B", "accion", "Accion"), ("C", "cifBac", "CIF BAC"),
                ("D", "cifCom", "CIF COM"), ("E", "codigoPlan", "Codigo plan"), ("F", "correo", "Correo"),
            };
            Assert.Equal(esperado, ColumnasDeCambioDeEstado.Todas.Select(c => (c.Letra, c.Campo, c.Encabezado)).ToArray());
            Assert.Equal("ActivarDesactivar", ColumnasDeCambioDeEstado.Hoja);
            Assert.Throws<NotSupportedException>(() => ((ICollection<ColumnaDeCatalogo>)ColumnasDeCambioDeEstado.Todas).Clear());
        }

        // ================================================================================================ argumentos
        [Fact]
        public void Nulos_y_numeros_de_fila_repetidos_son_error_de_quien_llama()
        {
            Assert.Throws<ArgumentNullException>(() => PlanificadorDeCambioDeEstado.Planificar(null, Foto()));
            Assert.Throws<ArgumentNullException>(() => PlanificadorDeCambioDeEstado.Planificar(new FilaPlantilla[0], null));
            Assert.ThrowsAny<ArgumentException>(() => Planificar(F(2, "CLIENTE", "ACTIVAR", "123"), null, F(3, "CLIENTE", "ACTIVAR", "456")));
            Assert.ThrowsAny<ArgumentException>(() => Planificar(null, F(2, "CLIENTE", "ACTIVAR", "123")));
            Assert.ThrowsAny<ArgumentException>(() => Planificar(F(2, "CLIENTE", "ACTIVAR", "123"), F(2, "CLIENTE", "ACTIVAR", "456")));
        }

        [Fact]
        public void Cero_filas_no_cambia_nada()
        {
            var plan = Planificar();
            Assert.False(plan.TieneErrores);
            Assert.Empty(plan.Filas);
            Assert.Empty(plan.Cambios);
        }

        // ================================================================================================ tipo y acción (regla 1)
        [Theory]
        [InlineData("CLIENTES")]
        [InlineData("AUTORIZACION_PLAN")] // DN-04: solo autorizaciones por cliente, ni en la carga ni en el cambio de estado
        [InlineData("AUTORIZACIÓN_CLIENTE")]
        [InlineData("")]
        public void Tipo_fuera_de_su_lista_es_error(string tipo)
        {
            Error(Unica(Planificar(F(2, tipo, "ACTIVAR", "123"))), "Tipo: ");
        }

        [Theory]
        [InlineData("BORRAR")]
        [InlineData("activo")]
        [InlineData("")]
        public void Accion_fuera_de_su_lista_es_error(string accion)
        {
            Error(Unica(Planificar(F(2, "CLIENTE", accion, "123"))), "Accion: ");
        }

        [Fact]
        public void Tipo_y_accion_se_comparan_sin_mayusculas_ni_espacios_a_los_lados()
        {
            var plan = Planificar(F(2, "  cliente ", " Desactivar  ", "123"));
            SinErrores(plan);
            var cambio = UnCambio(plan);
            Assert.Equal(TipoDeCatalogo.Cliente, cambio.Tipo);
            Assert.Equal(AccionDeEstado.Desactivar, cambio.Accion);
        }

        // ================================================================================================ claves por tipo (reglas 2 y 3)
        [Fact]
        public void Cliente_por_CIF_BAC_por_CIF_COM_o_por_los_dos_si_coinciden()
        {
            Assert.Equal(Sol, UnCambio(Planificar(F(2, "CLIENTE", "DESACTIVAR", " 123 "))).Id);
            Assert.Equal(Sol, UnCambio(Planificar(F(2, "CLIENTE", "DESACTIVAR", cifCom: "abc123456001"))).Id);
            Assert.Equal(Sol, UnCambio(Planificar(F(2, "CLIENTE", "DESACTIVAR", "123", "ABC123456001"))).Id);
            Assert.Equal("Desactivado", Unica(Planificar(F(2, "CLIENTE", "DESACTIVAR", "123"))).Texto.Split(new[] { " | " }, StringSplitOptions.None)[0]);
        }

        [Fact]
        public void Cliente_con_CIF_que_no_coinciden_inexistente_o_sin_CIF_es_error()
        {
            Error(Unica(Planificar(F(2, "CLIENTE", "DESACTIVAR", "123", "XYZ987654002"))), "CIF BAC / CIF COM: ");
            Error(Unica(Planificar(F(2, "CLIENTE", "DESACTIVAR", "123", "NOE000000999"))), "CIF BAC / CIF COM: ");
            Error(Unica(Planificar(F(2, "CLIENTE", "DESACTIVAR", "999999"))), "CIF BAC / CIF COM: ");
            Error(Unica(Planificar(F(2, "CLIENTE", "DESACTIVAR"))), "CIF BAC / CIF COM: ");
            Error(Unica(Planificar(F(2, "CLIENTE", "DESACTIVAR", "12A"))), "CIF BAC: ");
            Error(Unica(Planificar(F(2, "CLIENTE", "DESACTIVAR", cifCom: "MALO"))), "CIF COM: ");
        }

        [Fact]
        public void Cada_tipo_exige_sus_claves_y_ninguna_otra()
        {
            // Una celda que no se usa es una intención que no se cumple (DN-04, Excel 2, regla 2).
            Error(Unica(Planificar(F(2, "CLIENTE", "DESACTIVAR", "123", plan: "0A12"))), "Codigo plan: ");
            Error(Unica(Planificar(F(2, "CLIENTE", "DESACTIVAR", "123", correo: "juan.perez@empresa.com"))), "Correo: ");

            Error(Unica(Planificar(F(2, "PLAN", "DESACTIVAR", "123", plan: "0A12"))), "CIF BAC: ");
            Error(Unica(Planificar(F(2, "PLAN", "DESACTIVAR", cifCom: "ABC123456001", plan: "0A12"))), "CIF COM: ");
            Error(Unica(Planificar(F(2, "PLAN", "DESACTIVAR", plan: "0A12", correo: "juan.perez@empresa.com"))), "Correo: ");
            Error(Unica(Planificar(F(2, "PLAN", "DESACTIVAR"))), "Codigo plan: ");

            Error(Unica(Planificar(F(2, "CORREO", "DESACTIVAR", "123", correo: "juan.perez@empresa.com"))), "CIF BAC: ");
            Error(Unica(Planificar(F(2, "CORREO", "DESACTIVAR", plan: "0A12", correo: "juan.perez@empresa.com"))), "Codigo plan: ");
            Error(Unica(Planificar(F(2, "CORREO", "DESACTIVAR"))), "Correo: ");

            Error(Unica(Planificar(F(2, "AUTORIZACION_CLIENTE", "DESACTIVAR", "123", plan: "0A12", correo: "juan.perez@empresa.com"))), "Codigo plan: ");
            Error(Unica(Planificar(F(2, "AUTORIZACION_CLIENTE", "DESACTIVAR", "123"))), "Correo: ");
            Error(Unica(Planificar(F(2, "AUTORIZACION_CLIENTE", "DESACTIVAR", correo: "juan.perez@empresa.com"))), "CIF BAC / CIF COM: ");
        }

        [Fact]
        public void Plan_por_codigo_normalizado_inexistente_o_invalido()
        {
            var plan = Planificar(F(2, "plan", "desactivar", plan: "a12"));
            SinErrores(plan);
            Assert.Equal((TipoDeCatalogo.Plan, PlanSol), (UnCambio(plan).Tipo, UnCambio(plan).Id));
            Error(Unica(Planificar(F(2, "PLAN", "DESACTIVAR", plan: "ZZZZ"))), "Codigo plan: ");
            Error(Unica(Planificar(F(2, "PLAN", "DESACTIVAR", plan: "ABCDE"))), "Codigo plan: ");
        }

        [Fact]
        public void Correo_por_correo_normalizado_inexistente_o_invalido()
        {
            var plan = Planificar(F(2, "CORREO", "DESACTIVAR", correo: "  Pedro@Empresa.COM "));
            SinErrores(plan);
            Assert.Equal((TipoDeCatalogo.Correo, Pedro), (UnCambio(plan).Tipo, UnCambio(plan).Id));
            Error(Unica(Planificar(F(2, "CORREO", "DESACTIVAR", correo: "nadie@empresa.com"))), "Correo: ");
            Error(Unica(Planificar(F(2, "CORREO", "DESACTIVAR", correo: "no-es-correo"))), "Correo: ");
        }

        [Fact]
        public void Autorizacion_por_cliente_es_el_par_correo_cliente_y_tiene_que_existir_como_par()
        {
            var plan = Planificar(F(2, "AUTORIZACION_CLIENTE", "DESACTIVAR", cifCom: "ABC123456001", correo: "PEDRO@empresa.com"));
            SinErrores(plan);
            Assert.Equal((TipoDeCatalogo.AutorizacionCliente, PedroSol), (UnCambio(plan).Tipo, UnCambio(plan).Id));

            // Pedro y Luna existen, pero Pedro no está autorizado sobre Luna: que existan por separado no alcanza.
            Error(Unica(Planificar(F(2, "AUTORIZACION_CLIENTE", "DESACTIVAR", "456", correo: "pedro@empresa.com"))), "Correo: ");
            Error(Unica(Planificar(F(2, "AUTORIZACION_CLIENTE", "DESACTIVAR", "999999", correo: "pedro@empresa.com"))), "CIF BAC / CIF COM: ");
            Error(Unica(Planificar(F(2, "AUTORIZACION_CLIENTE", "DESACTIVAR", "123", correo: "nadie@empresa.com"))), "Correo: ");
        }

        // ================================================================================================ ya en el estado (regla 4)
        [Fact]
        public void Lo_que_ya_esta_en_el_estado_pedido_no_cambia_y_no_es_error()
        {
            var plan = Planificar(
                F(2, "CLIENTE", "DESACTIVAR", "789"),
                F(3, "CLIENTE", "ACTIVAR", "123"),
                F(4, "PLAN", "DESACTIVAR", plan: "0C56"),
                F(5, "AUTORIZACION_CLIENTE", "ACTIVAR", "123", correo: "juan.perez@empresa.com"));
            SinErrores(plan);
            Assert.Empty(plan.Cambios);
            Assert.Equal(4, plan.SinCambios);
            Assert.Equal("Sin cambios: ya estaba inactivo", plan.Filas[0].Texto);
            Assert.Equal("Sin cambios: ya estaba activo", plan.Filas[1].Texto);
            Assert.Equal("Sin cambios: ya estaba inactivo", plan.Filas[2].Texto);
            Assert.Equal("Sin cambios: ya estaba activo", plan.Filas[3].Texto);
        }

        // ================================================================================================ repetidos (regla 5)
        [Fact]
        public void El_mismo_registro_con_la_misma_accion_se_aplica_una_vez_y_la_segunda_fila_lo_advierte()
        {
            var plan = Planificar(F(5, "CLIENTE", "ACTIVAR", "789"), F(2, "CLIENTE", "ACTIVAR", cifCom: "INA000000003"));
            SinErrores(plan);
            var cambio = UnCambio(plan);
            Assert.Equal(Dormido, cambio.Id);
            Assert.Equal(2, cambio.FilaOrigen);
            Assert.Equal(1, plan.Activados);
            Assert.Equal(1, plan.SinCambios);
            Assert.StartsWith("Sin cambios: lo pide una fila anterior", plan.Filas[0].Texto);
            Assert.Contains(plan.Filas[0].Advertencias, a => Regex.IsMatch(a, @"\b2\b"));
            Assert.StartsWith("Activado", plan.Filas[1].Texto);
        }

        [Fact]
        public void El_mismo_registro_con_acciones_opuestas_es_error_en_las_dos_filas_nombrando_a_la_otra()
        {
            var plan = Planificar(F(2, "CORREO", "ACTIVAR", correo: "pedro@empresa.com"), F(7, "CORREO", "DESACTIVAR", correo: "PEDRO@empresa.com"));
            Assert.True(plan.TieneErrores);
            Assert.Contains(plan.Filas[0].Errores, e => e.StartsWith("Accion: ", StringComparison.Ordinal) && Regex.IsMatch(e, @"\b7\b"));
            Assert.Contains(plan.Filas[1].Errores, e => e.StartsWith("Accion: ", StringComparison.Ordinal) && Regex.IsMatch(e, @"\b2\b"));
        }

        // ================================================================================================ advertencias de efecto (regla 6)
        [Fact]
        public void Desactivar_un_cliente_o_un_correo_advierte_cuantas_autorizaciones_activas_deja_sin_efecto()
        {
            var cliente = Unica(Planificar(F(2, "CLIENTE", "DESACTIVAR", "123")));            // Juan→Sol y Pedro→Sol
            Assert.Contains(cliente.Advertencias, a => Regex.IsMatch(a, @"\b2\b"));
            var correo = Unica(Planificar(F(2, "CORREO", "DESACTIVAR", correo: "juan.perez@empresa.com"))); // solo Juan→Sol (Juan→Luna está inactiva)
            Assert.Contains(correo.Advertencias, a => Regex.IsMatch(a, @"\b1\b"));
            Assert.Contains(" | Advertencia: ", correo.Texto);
        }

        [Fact]
        public void El_efecto_se_cuenta_sobre_el_estado_final_del_archivo()
        {
            // El mismo archivo desactiva Juan→Sol: al desactivar a Juan ya no queda ninguna autorización activa suya.
            var plan = Planificar(
                F(2, "AUTORIZACION_CLIENTE", "DESACTIVAR", "123", correo: "juan.perez@empresa.com"),
                F(3, "CORREO", "DESACTIVAR", correo: "juan.perez@empresa.com"));
            SinErrores(plan);
            Assert.Empty(plan.Filas[1].Advertencias);

            // Lo mismo con un cliente: Luna tiene una sola activa (María→Luna); si el archivo la desactiva, Luna no advierte nada.
            Assert.Contains(Unica(Planificar(F(2, "CLIENTE", "DESACTIVAR", "456"))).Advertencias, a => Regex.IsMatch(a, @"\b1\b"));
            var luna = Planificar(
                F(2, "CLIENTE", "DESACTIVAR", "456"),
                F(3, "AUTORIZACION_CLIENTE", "DESACTIVAR", "456", correo: "maria.lopez@empresa.com"));
            SinErrores(luna);
            Assert.Empty(luna.Filas[0].Advertencias);
        }

        [Fact]
        public void Activar_una_autorizacion_cuyo_cliente_o_correo_queda_inactivo_advierte_que_no_va_a_estar_vigente()
        {
            // María está inactiva: activar María→Sol advierte.
            var correoInactivo = Planificar(F(2, "AUTORIZACION_CLIENTE", "ACTIVAR", "123", correo: "maria.lopez@empresa.com"));
            SinErrores(correoInactivo);
            Assert.NotEmpty(Unica(correoInactivo).Advertencias);

            // Si el mismo archivo activa a María, ya no.
            var conCorreo = Planificar(
                F(2, "AUTORIZACION_CLIENTE", "ACTIVAR", "123", correo: "maria.lopez@empresa.com"),
                F(3, "CORREO", "ACTIVAR", correo: "maria.lopez@empresa.com"));
            SinErrores(conCorreo);
            Assert.Empty(conCorreo.Filas[0].Advertencias);

            // Activar Juan→Luna mientras el mismo archivo desactiva a Luna: advierte en la fila de la autorización.
            var clienteQueSeApaga = Planificar(
                F(2, "AUTORIZACION_CLIENTE", "ACTIVAR", "456", correo: "juan.perez@empresa.com"),
                F(3, "CLIENTE", "DESACTIVAR", "456"));
            SinErrores(clienteQueSeApaga);
            Assert.NotEmpty(clienteQueSeApaga.Filas[0].Advertencias);

            // Activar Juan→Luna con todo activo: sin advertencias.
            Assert.Empty(Unica(Planificar(F(2, "AUTORIZACION_CLIENTE", "ACTIVAR", "456", correo: "juan.perez@empresa.com"))).Advertencias);
        }

        // ================================================================================================ huecos de la revisión (2026-10-01)
        [Fact]
        public void Pedir_activar_una_autorizacion_que_ya_esta_activa_pero_no_vigente_tambien_advierte()
        {
            // Decisión del arquitecto: quien pide ACTIVAR cree que la autorización va a operar. Si su correo o su cliente queda
            // inactivo, no opera, cambie o no el estado de la fila. María→Luna está activa, María está inactiva.
            var plan = Planificar(F(2, "AUTORIZACION_CLIENTE", "ACTIVAR", "456", correo: "maria.lopez@empresa.com"));
            SinErrores(plan);
            Assert.Empty(plan.Cambios);
            var fila = Unica(plan);
            Assert.StartsWith("Sin cambios: ya estaba activo", fila.Texto);
            Assert.Contains(fila.Advertencias, a => a.StartsWith("Correo: ", StringComparison.Ordinal));
        }

        [Fact]
        public void Opuestas_y_repetidas_se_detectan_aunque_una_de_las_filas_tenga_otro_error()
        {
            // "Reportar todo de una vez" (DN-04): que la fila 3 tenga una celda de más no esconde que pide lo contrario que la 2.
            var plan = Planificar(F(2, "CLIENTE", "ACTIVAR", "123"), F(3, "CLIENTE", "DESACTIVAR", "123", plan: "0A12"));
            Assert.True(plan.TieneErrores);
            Assert.Contains(plan.Filas[0].Errores, e => e.StartsWith("Accion: ", StringComparison.Ordinal) && Regex.IsMatch(e, @"\b3\b"));
            Assert.Contains(plan.Filas[1].Errores, e => e.StartsWith("Accion: ", StringComparison.Ordinal) && Regex.IsMatch(e, @"\b2\b"));
            Assert.Contains(plan.Filas[1].Errores, e => e.StartsWith("Codigo plan: ", StringComparison.Ordinal));
        }

        [Fact]
        public void Una_repetida_de_tres_filas_que_nombran_el_registro_de_formas_distintas_se_aplica_una_vez()
        {
            var plan = Planificar(
                F(9, "CLIENTE", "ACTIVAR", "789"),
                F(4, "CLIENTE", "ACTIVAR", cifCom: "ina000000003"),
                F(6, "CLIENTE", "ACTIVAR", "000000789", "INA000000003"));
            SinErrores(plan);
            Assert.Equal(4, UnCambio(plan).FilaOrigen);
            Assert.Equal(1, plan.Activados);
            Assert.Equal(2, plan.SinCambios);
            Assert.StartsWith("Activado", plan.Filas[1].Texto);
        }

        [Fact]
        public void Las_claves_de_celdas_que_sobran_tambien_entran_a_la_foto()
        {
            // Decisión 9 del constructor, fijada: traer una clave de más solo cuesta una consulta, y la fila da error igual.
            var claves = ClavesDeCambioDeEstado.Extraer(new[] { F(2, "CLIENTE", "ACTIVAR", "123", plan: "0A12", correo: "pedro@empresa.com") });
            Assert.Equal(new[] { "0A12" }, claves.CodigosDePlan.ToArray());
            Assert.Equal(new[] { "pedro@empresa.com" }, claves.Correos.ToArray());
        }

        // ================================================================================================ todo o nada, texto y contadores
        [Fact]
        public void Con_un_error_en_cualquier_fila_no_cambia_nada()
        {
            var plan = Planificar(F(2, "CLIENTE", "DESACTIVAR", "123"), F(3, "PLAN", "DESACTIVAR", plan: "ZZZZ"));
            Assert.True(plan.TieneErrores);
            Assert.Empty(plan.Cambios);
            Assert.Equal(0, plan.Activados); Assert.Equal(0, plan.Desactivados); Assert.Equal(0, plan.SinCambios);
            Assert.StartsWith("Sin errores — no se grabó: el archivo tiene errores en otras filas", plan.Filas[0].Texto);
            Assert.StartsWith("Error: Codigo plan: ", plan.Filas[1].Texto);
        }

        [Fact]
        public void Una_fila_reporta_todos_sus_errores_y_el_texto_respeta_el_orden()
        {
            var fila = Unica(Planificar(F(2, "CLIENTES", "BORRAR", "12A", plan: "0A12")));
            Error(fila, "Tipo: ");
            Error(fila, "Accion: ");
            Error(fila, "CIF BAC: ");
            Assert.Equal(string.Join(" | ", fila.Errores.Select(e => "Error: " + e).Concat(fila.Advertencias.Select(a => "Advertencia: " + a))), fila.Texto);
        }

        [Fact]
        public void Los_contadores_cuentan_cambios_reales()
        {
            var plan = Planificar(
                F(2, "CLIENTE", "DESACTIVAR", "123"),
                F(3, "CLIENTE", "ACTIVAR", "789"),
                F(4, "AUTORIZACION_CLIENTE", "ACTIVAR", "456", correo: "juan.perez@empresa.com"),
                F(5, "PLAN", "DESACTIVAR", plan: "0C56"));
            SinErrores(plan);
            Assert.Equal(2, plan.Activados);
            Assert.Equal(1, plan.Desactivados);
            Assert.Equal(1, plan.SinCambios);
            Assert.Equal(3, plan.Cambios.Count);
            Assert.Throws<NotSupportedException>(() => ((ICollection<CambioDeEstadoACrear>)plan.Cambios).Clear());
        }

        [Fact]
        public void Ningun_valor_enorme_en_ninguna_columna_produce_un_Resultado_enorme()
        {
            var enorme = new string('x', 30000);
            var bases = new[]
            {
                F(2, enorme, "ACTIVAR", "123"),
                F(2, "CLIENTE", enorme, "123"),
                F(2, "CLIENTE", "ACTIVAR", enorme),
                F(2, "CLIENTE", "ACTIVAR", cifCom: enorme),
                F(2, "PLAN", "ACTIVAR", plan: enorme),
                F(2, "CORREO", "ACTIVAR", correo: enorme + "@empresa.com"),
                F(2, "CLIENTE", "ACTIVAR", "123", plan: enorme),
            };
            foreach (var fila in bases)
            {
                var resultado = Unica(Planificar(fila));
                Assert.NotEmpty(resultado.Errores);
                Assert.True(resultado.Texto.Length < 2000, $"Resultado de {resultado.Texto.Length} caracteres");
            }
        }

        // ================================================================================================ claves y costo
        [Fact]
        public void Las_claves_salen_normalizadas_sin_repetidos_y_sin_las_invalidas()
        {
            var claves = ClavesDeCambioDeEstado.Extraer(new[]
            {
                F(2, "CLIENTE", "ACTIVAR", "123", "abc123456001"),
                F(3, "AUTORIZACION_CLIENTE", "ACTIVAR", "000000123", correo: "Juan.Perez@Empresa.com"),
                F(4, "PLAN", "ACTIVAR", plan: "a12"),
                F(5, "CORREO", "ACTIVAR", "12A", "MALO", "ABCDE", "malo"),
            });
            Assert.Equal(new[] { "000000123" }, claves.CifsBac.ToArray());
            Assert.Equal(new[] { "ABC123456001" }, claves.CifsCom.ToArray());
            Assert.Equal(new[] { "0A12" }, claves.CodigosDePlan.ToArray());
            Assert.Equal(new[] { "juan.perez@empresa.com" }, claves.Correos.ToArray());
            Assert.Throws<ArgumentNullException>(() => ClavesDeCambioDeEstado.Extraer(null));
        }

        [Fact]
        public void Cinco_mil_filas_se_planifican_en_tiempo_lineal()
        {
            var clientes = Enumerable.Range(0, 5000).Select(i =>
                new ClienteExistente(G(1000 + i), (100000 + i).ToString("D9", CultureInfo.InvariantCulture), $"NUE{i:D6}001", $"Cliente {i}", Ana, true)).ToList();
            var autorizaciones = Enumerable.Range(0, 5000).Select(i => new AutorizacionClienteExistente(G(20000 + i), Juan, G(1000 + i), null, null, true)).ToList();
            var foto = new EstadoDeCatalogos(clientes, new PlanExistente[0],
                new[] { new AutorizadoExistente(Juan, "juan.perez@empresa.com", "Juan Pérez", true) }, autorizaciones, new UsuarioCandidato[0]);
            var filas = Enumerable.Range(0, 5000)
                .Select(i => F(i + 2, "CLIENTE", "DESACTIVAR", (100000 + i).ToString(CultureInfo.InvariantCulture)))
                .Concat(new[] { F(5002, "CORREO", "DESACTIVAR", correo: "juan.perez@empresa.com") })
                .ToArray();
            var reloj = Stopwatch.StartNew();
            var plan = PlanificadorDeCambioDeEstado.Planificar(filas, foto);
            reloj.Stop();
            SinErrores(plan);
            Assert.Equal(5001, plan.Desactivados);
            Assert.True(reloj.Elapsed < TimeSpan.FromSeconds(3), $"tardó {reloj.Elapsed}");
        }
    }
}
