using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Mppp.Plugins.Api;
using Sanic.Mppp.Plugins.Datos;
using Sanic.Mppp.Plugins.Dominio;
using Sanic.Mppp.Plugins.Exportacion;
using Sanic.Mppp.Plugins.Tests.Apoyo;
using Sanic.Mppp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Aceptacion
{
    /// <summary>
    /// El plugin de `sanic_mppp_capi_exportarparadigitar` (DN-02, diseno/DIRECTRICES-NUEVAS.md), sin el envoltorio
    /// `IPlugin` (que es <see cref="ExportarParaDigitarApi"/>). Orquesta piezas YA probadas por separado
    /// (<see cref="FilasDataverse.PorIds"/>, <see cref="ConfiguracionExportacion"/>, <see cref="ArmadorExportacionOpenXml"/>):
    /// estas pruebas verifican el ENSAMBLAJE, no vuelven a probar cada pieza.
    ///
    /// Las decisiones del aprobador que fija esta clase: (1, 2026-09-26) un `filaid` que no vuelve REVIENTA
    /// la llamada entera, nombrando cuáles faltaron; (2, 2026-09-26) el vocabulario de campos incluye, además de
    /// los diez de `CamposDeFila`, el número de Solicitud y el número de fila — sin eso el archivo no tiene
    /// identidad para que el ejecutivo vuelva a marcar lo que digitó; (3, 2026-09-26 — reemplaza la decisión
    /// original de `exportacion.maximo`) la Custom API pasó a Action y SE ELIMINÓ el tope de filas por llamada: el
    /// único límite que queda es el presupuesto síncrono de 2 minutos (BP-PP-053), y el plugin se cuida solo,
    /// cortando ANTES con un mensaje que le dice a una persona qué hacer (cuántas alcanzó a procesar, que
    /// seleccione menos), nunca dejando que la plataforma reviente con un error genérico.
    /// </summary>
    public class ExportarParaDigitarAceptacion
    {
        private static readonly DateTime Base = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

        private static Entity Fila(
            OrganizationServiceEnMemoria svc,
            string numeroPlan, string nombreBeneficiario, string numeroIdentificacion, string numeroCuenta,
            string numeroSolicitud = "MPPP-00000001", int numeroFila = 1)
        {
            var solicitudId = svc.Sembrar(new Entity(TablasHistorico.Solicitud) { ["sanic_nombre"] = numeroSolicitud });

            return new Entity(TablasHistorico.Fila)
            {
                ["sanic_estado"] = new OptionSetValue((int)EstadoDeLaFila.Validada),
                ["sanic_fechavalidada"] = Base,
                ["sanic_numerofila"] = numeroFila,
                ["sanic_requiereatencionmanual"] = false,
                ["sanic_solicitudid"] = new EntityReference(TablasHistorico.Solicitud, solicitudId),
                ["sanic_numeroplan"] = numeroPlan,
                ["sanic_gestion"] = new OptionSetValue((int)Gestion.Inclusion),
                ["sanic_clasificacion"] = new OptionSetValue((int)Clasificacion.ACH),
                ["sanic_nombrebeneficiario"] = nombreBeneficiario,
                ["sanic_tipoidentificacion"] = new OptionSetValue((int)TipoDeIdentificacion.CNA),
                ["sanic_numeroidentificacion"] = numeroIdentificacion,
                ["sanic_numerocuenta"] = numeroCuenta,
                ["sanic_moneda"] = new OptionSetValue((int)Moneda.COR),
                ["sanic_banco"] = new OptionSetValue((int)Banco.BANPRO),
                ["sanic_referencia"] = "REF-1",
                ["sanic_referenciarecibida"] = "REF-1",
            };
        }

        private static string JsonDeColumnas(params (string campo, string encabezado)[] columnas)
        {
            var items = columnas.Select(c => $@"{{""campo"":""{c.campo}"",""encabezado"":""{c.encabezado}""}}");
            return @"{""columnas"":[" + string.Join(",", items) + "]}";
        }

        private static void SembrarColumnas(OrganizationServiceEnMemoria svc, params (string campo, string encabezado)[] columnas)
        {
            svc.Sembrar(new Entity(Tablas.Parametro)
            {
                ["statecode"] = new OptionSetValue(Tablas.Activo),
                ["sanic_nombre"] = ExportarParaDigitar.ParametroColumnas,
                ["sanic_version"] = 1,
                ["sanic_valor"] = JsonDeColumnas(columnas),
            });
        }

        /// <summary>
        /// Siembra el único parámetro que `Ejecutar` necesita siempre. Se llama igual que antes de la decisión del
        /// aprobador (2026-09-26) de eliminar `exportacion.maximo` para no tocar las pruebas que no están probando
        /// esa decisión: hoy es un alias liso de <see cref="SembrarColumnas"/>.
        /// </summary>
        private static void SembrarParametros(OrganizationServiceEnMemoria svc, params (string campo, string encabezado)[] columnas)
        {
            SembrarColumnas(svc, columnas);
        }

        private static ExportarParaDigitar Core(OrganizationServiceEnMemoria svc)
        {
            return new ExportarParaDigitar(new FilasDataverse(svc), new CatalogosDataverse(svc), new ArmadorExportacionOpenXml());
        }

        [Fact]
        public void Arma_el_archivo_con_las_filas_pedidas_en_el_orden_pedido_y_cuenta_exportadas()
        {
            var svc = new OrganizationServiceEnMemoria();
            SembrarParametros(svc, ("numeroPlan", "Plan"), ("nombreBeneficiario", "Nombre"));
            var primera = svc.Sembrar(Fila(svc, "0001", "Ana Perez", "001-1", "111"));
            var segunda = svc.Sembrar(Fila(svc, "0002", "Beto Ruiz", "001-2", "222"));

            // Se piden en el orden CONTRARIO al que se sembraron: el archivo tiene que respetar el orden pedido.
            var resultado = Core(svc).Ejecutar(new[] { segunda, primera });

            Assert.Equal(2, resultado.Exportadas);
            var xlsx = Convert.FromBase64String(resultado.ArchivoBase64);
            var filas = LectorCrudoDeExportacion.LeerFilas(xlsx, ArmadorExportacionOpenXml.NombreDeHoja);
            Assert.Equal(3, filas.Count); // encabezado + 2
            Assert.Equal("0002", filas[1].Celdas[0].Texto);
            Assert.Equal("Beto Ruiz", filas[1].Celdas[1].Texto);
            Assert.Equal("0001", filas[2].Celdas[0].Texto);
            Assert.Equal("Ana Perez", filas[2].Celdas[1].Texto);
        }

        [Fact]
        public void FilaIds_vacio_no_es_un_error_arma_un_archivo_con_solo_encabezado()
        {
            var svc = new OrganizationServiceEnMemoria();
            SembrarParametros(svc, ("numeroPlan", "Plan"));

            var resultado = Core(svc).Ejecutar(new Guid[0]);

            Assert.Equal(0, resultado.Exportadas);
            var xlsx = Convert.FromBase64String(resultado.ArchivoBase64);
            var filas = LectorCrudoDeExportacion.LeerFilas(xlsx, ArmadorExportacionOpenXml.NombreDeHoja);
            Assert.Single(filas); // solo el encabezado
        }

        [Fact]
        public void Resuelve_los_campos_protegidos_numeroidentificacion_y_numerocuenta_por_nombre()
        {
            var svc = new OrganizationServiceEnMemoria();
            SembrarParametros(svc, ("numeroIdentificacion", "Identificacion"), ("numeroCuenta", "Cuenta"));
            var filaId = svc.Sembrar(Fila(svc, "0001", "Ana Perez", "001-000000-0000A", "0001234567890123"));

            var resultado = Core(svc).Ejecutar(new[] { filaId });

            var xlsx = Convert.FromBase64String(resultado.ArchivoBase64);
            var filas = LectorCrudoDeExportacion.LeerFilas(xlsx, ArmadorExportacionOpenXml.NombreDeHoja);
            Assert.Equal("001-000000-0000A", filas[1].Celdas[0].Texto);
            Assert.Equal("0001234567890123", filas[1].Celdas[1].Texto);
            Assert.True(filas[1].Celdas[1].EsTexto, "el numero de cuenta no puede salir como celda numerica: pierde los ceros a la izquierda");
        }

        [Fact]
        public void Resuelve_los_campos_de_choice_como_su_texto_canonico()
        {
            var svc = new OrganizationServiceEnMemoria();
            SembrarParametros(svc, ("gestion", "Gestion"), ("clasificacion", "Clasificacion"), ("tipoIdentificacion", "TipoId"), ("moneda", "Moneda"), ("banco", "Banco"));
            var filaId = svc.Sembrar(Fila(svc, "0001", "Ana Perez", "001-1", "111"));

            var resultado = Core(svc).Ejecutar(new[] { filaId });

            var xlsx = Convert.FromBase64String(resultado.ArchivoBase64);
            var filas = LectorCrudoDeExportacion.LeerFilas(xlsx, ArmadorExportacionOpenXml.NombreDeHoja);
            Assert.Equal("Inclusion", filas[1].Celdas[0].Texto);
            Assert.Equal("ACH", filas[1].Celdas[1].Texto);
            Assert.Equal("CNA", filas[1].Celdas[2].Texto);
            Assert.Equal("COR", filas[1].Celdas[3].Texto);
            Assert.Equal("BANPRO", filas[1].Celdas[4].Texto);
        }

        [Fact]
        public void Resuelve_numeroSolicitud_y_numeroFila_decision_del_aprobador_2026_09_26()
        {
            var svc = new OrganizationServiceEnMemoria();
            SembrarParametros(svc,
                (ExportarParaDigitar.CampoNumeroSolicitud, "Solicitud"),
                (ExportarParaDigitar.CampoNumeroFila, "NroFila"));
            var filaId = svc.Sembrar(Fila(svc, "0001", "Ana Perez", "001-1", "111", numeroSolicitud: "MPPP-00000042", numeroFila: 7));

            var resultado = Core(svc).Ejecutar(new[] { filaId });

            var xlsx = Convert.FromBase64String(resultado.ArchivoBase64);
            var filas = LectorCrudoDeExportacion.LeerFilas(xlsx, ArmadorExportacionOpenXml.NombreDeHoja);
            Assert.Equal("MPPP-00000042", filas[1].Celdas[0].Texto);
            Assert.Equal("7", filas[1].Celdas[1].Texto);
        }

        [Fact]
        public void Un_campo_pedido_fuera_del_vocabulario_conocido_revienta_con_el_motivo()
        {
            var svc = new OrganizationServiceEnMemoria();
            SembrarParametros(svc, ("campoInventado", "Rara"));
            var filaId = svc.Sembrar(Fila(svc, "0001", "Ana Perez", "001-1", "111"));

            var ex = Assert.Throws<ArgumentException>(() => Core(svc).Ejecutar(new[] { filaId }));
            Assert.Contains("campoInventado", ex.Message);
        }

        [Fact]
        public void Falta_el_parametro_exportacion_columnas_es_un_error_de_configuracion()
        {
            var svc = new OrganizationServiceEnMemoria();
            var filaId = svc.Sembrar(Fila(svc, "0001", "Ana Perez", "001-1", "111"));

            var ex = Assert.Throws<InvalidOperationException>(() => Core(svc).Ejecutar(new[] { filaId }));
            Assert.Contains(ExportarParaDigitar.ParametroColumnas, ex.Message);
        }

        [Fact]
        public void Un_parametro_de_columnas_mal_formado_propaga_el_FormatException_del_dominio()
        {
            var svc = new OrganizationServiceEnMemoria();
            svc.Sembrar(new Entity(Tablas.Parametro)
            {
                ["statecode"] = new OptionSetValue(Tablas.Activo),
                ["sanic_nombre"] = ExportarParaDigitar.ParametroColumnas,
                ["sanic_version"] = 1,
                ["sanic_valor"] = "esto no es JSON",
            });
            var filaId = svc.Sembrar(Fila(svc, "0001", "Ana Perez", "001-1", "111"));

            Assert.Throws<FormatException>(() => Core(svc).Ejecutar(new[] { filaId }));
        }

        // ---------- Decision 1 (aprobador, 2026-09-26): un filaid que no vuelve REVIENTA nombrando cuales ----------

        [Fact]
        public void Un_filaid_inexistente_revienta_la_llamada_entera_nombrando_cual_falta()
        {
            var svc = new OrganizationServiceEnMemoria();
            SembrarParametros(svc, ("numeroPlan", "Plan"));
            var existe = svc.Sembrar(Fila(svc, "0001", "Ana Perez", "001-1", "111"));
            var noExiste = Guid.NewGuid();

            var ex = Assert.Throws<FilasNoEncontradasException>(() => Core(svc).Ejecutar(new[] { existe, noExiste }));

            Assert.Single(ex.Faltantes);
            Assert.Equal(noExiste, ex.Faltantes[0]);
            Assert.Contains(noExiste.ToString(), ex.Message);
        }

        [Fact]
        public void Varios_filaid_inexistentes_se_nombran_todos()
        {
            var svc = new OrganizationServiceEnMemoria();
            SembrarParametros(svc, ("numeroPlan", "Plan"));
            var faltanteUno = Guid.NewGuid();
            var faltanteDos = Guid.NewGuid();

            var ex = Assert.Throws<FilasNoEncontradasException>(() => Core(svc).Ejecutar(new[] { faltanteUno, faltanteDos }));

            Assert.Equal(2, ex.Faltantes.Count);
            Assert.Contains(faltanteUno, ex.Faltantes);
            Assert.Contains(faltanteDos, ex.Faltantes);
        }

        // ---------- Decision del aprobador (2026-09-26): se elimina `exportacion.maximo` (BP-PP-053, Custom API
        // pasada a Action). El unico limite que queda es el presupuesto de 2 minutos, mas abajo. ----------

        [Fact]
        public void Ya_no_existe_ningun_tope_de_filas_por_llamada_un_lote_de_1000_no_revienta_por_eso()
        {
            var svc = new OrganizationServiceEnMemoria();
            SembrarColumnas(svc, ("numeroPlan", "Plan")); // sin sembrar exportacion.maximo: ya no se lee
            var pedidos = Enumerable.Range(0, 1000).Select(_ => Guid.NewGuid()).ToList(); // ninguno existe

            // Antes de la decision, 1000 > exportacion.maximo (500) hubiera reventado ANTES de tocar Fila con
            // LoteDeExportacionExcedidoException (tipo que ya no existe). Ahora sigue de largo hasta la
            // verificacion real de que las filas existan, y revienta por ESO, no por un tope de cantidad.
            var ex = Assert.Throws<FilasNoEncontradasException>(() => Core(svc).Ejecutar(pedidos));
            Assert.Equal(1000, ex.Faltantes.Count);
        }

        [Fact]
        public void Sin_exportacion_maximo_en_el_entorno_no_hay_ningun_error_de_configuracion_por_eso()
        {
            var svc = new OrganizationServiceEnMemoria();
            SembrarColumnas(svc, ("numeroPlan", "Plan")); // exportacion.maximo NUNCA se siembra en esta prueba
            var filaId = svc.Sembrar(Fila(svc, "0001", "Ana Perez", "001-1", "111"));

            var resultado = Core(svc).Ejecutar(new[] { filaId });

            Assert.Equal(1, resultado.Exportadas);
        }

        // ---------- Decision del aprobador (2026-09-26): sin tope de filas, el unico limite es el presupuesto
        // sincrono de 2 minutos (BP-PP-053). El plugin se cuida solo: si se acerca, CORTA con un mensaje que le
        // dice a una persona que hacer (cuantas alcanzo a procesar, que seleccione menos), nunca un error tecnico
        // generico de la plataforma. ----------

        private static Func<TimeSpan> RelojQueYaEstaSobreElUmbral()
        {
            return () => TimeSpan.FromMinutes(5); // cualquier valor >= al umbral de corte
        }

        [Fact]
        public void Si_ya_esta_sobre_el_umbral_antes_de_armar_una_sola_fila_corta_con_cero_procesadas()
        {
            var svc = new OrganizationServiceEnMemoria();
            SembrarColumnas(svc, ("numeroPlan", "Plan"));
            var filaId = svc.Sembrar(Fila(svc, "0001", "Ana Perez", "001-1", "111"));

            var ex = Assert.Throws<PresupuestoDeExportacionAgotandoseException>(
                () => Core(svc).Ejecutar(new[] { filaId }, RelojQueYaEstaSobreElUmbral()));

            Assert.Equal(0, ex.Procesadas);
            Assert.Equal(1, ex.Pedidas);
        }

        [Fact]
        public void Corta_a_mitad_de_camino_e_informa_cuantas_alcanzo_a_procesar_de_cuantas_se_pidieron()
        {
            var svc = new OrganizationServiceEnMemoria();
            SembrarColumnas(svc, ("numeroPlan", "Plan"));
            var idUno = svc.Sembrar(Fila(svc, "0001", "Uno", "001-1", "111"));
            var idDos = svc.Sembrar(Fila(svc, "0002", "Dos", "001-2", "222"));
            var idTres = svc.Sembrar(Fila(svc, "0003", "Tres", "001-3", "333"));
            var llamados = 0;
            // Las dos primeras filas alcanzan a armarse (tiempo bajo); al llegar a la tercera ya paso el umbral.
            TimeSpan Reloj() { llamados++; return llamados <= 2 ? TimeSpan.Zero : TimeSpan.FromMinutes(5); }

            var ex = Assert.Throws<PresupuestoDeExportacionAgotandoseException>(
                () => Core(svc).Ejecutar(new[] { idUno, idDos, idTres }, Reloj));

            Assert.Equal(2, ex.Procesadas);
            Assert.Equal(3, ex.Pedidas);
        }

        [Fact]
        public void El_mensaje_del_corte_por_tiempo_dice_que_hacer_y_no_expone_detalles_tecnicos()
        {
            var ex = new PresupuestoDeExportacionAgotandoseException(procesadas: 2, pedidas: 5);

            Assert.Contains("2", ex.Message);
            Assert.Contains("5", ex.Message);
            Assert.Contains("menos filas", ex.Message, StringComparison.OrdinalIgnoreCase);
            // Nunca un error tecnico: nada de nombres de excepcion, milisegundos, ni codigos HTTP.
            Assert.DoesNotContain("Exception", ex.Message);
            Assert.DoesNotContain("ms", ex.Message);
            Assert.DoesNotContain("HTTP", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Si_nunca_se_acerca_al_umbral_no_corta_y_arma_el_archivo_completo()
        {
            var svc = new OrganizationServiceEnMemoria();
            SembrarColumnas(svc, ("numeroPlan", "Plan"));
            var idUno = svc.Sembrar(Fila(svc, "0001", "Uno", "001-1", "111"));
            var idDos = svc.Sembrar(Fila(svc, "0002", "Dos", "001-2", "222"));

            var resultado = Core(svc).Ejecutar(new[] { idUno, idDos }, () => TimeSpan.Zero);

            Assert.Equal(2, resultado.Exportadas);
        }

        [Fact]
        public void El_reloj_inyectado_nulo_es_un_error_de_programacion()
        {
            var svc = new OrganizationServiceEnMemoria();
            Assert.Throws<ArgumentNullException>(() => Core(svc).Ejecutar(new Guid[0], null));
        }

        [Fact]
        public void FilaIds_nulo_es_un_error_de_programacion()
        {
            var svc = new OrganizationServiceEnMemoria();
            Assert.Throws<ArgumentNullException>(() => Core(svc).Ejecutar(null));
        }

        [Fact]
        public void Dependencias_nulas_son_un_error_de_programacion()
        {
            var svc = new OrganizationServiceEnMemoria();
            var filas = new FilasDataverse(svc);
            var catalogos = new CatalogosDataverse(svc);
            var armador = new ArmadorExportacionOpenXml();

            Assert.Throws<ArgumentNullException>(() => new ExportarParaDigitar(null, catalogos, armador));
            Assert.Throws<ArgumentNullException>(() => new ExportarParaDigitar(filas, null, armador));
            Assert.Throws<ArgumentNullException>(() => new ExportarParaDigitar(filas, catalogos, null));
        }
    }
}
