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
    /// Las tres decisiones del aprobador (2026-09-26) que fija esta clase: (1) un `filaid` que no vuelve REVIENTA
    /// la llamada entera, nombrando cuáles faltaron; (2) `exportacion.maximo` (sembrado en 500, PROVISIONAL) rechaza
    /// LA LLAMADA ENTERA si se supera, nunca recorta en silencio; (3) el vocabulario de campos incluye, además de
    /// los diez de `CamposDeFila`, el número de Solicitud y el número de fila — sin eso el archivo no tiene
    /// identidad para que el ejecutivo vuelva a marcar lo que digitó.
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

        private static void SembrarMaximo(OrganizationServiceEnMemoria svc, string valor)
        {
            svc.Sembrar(new Entity(Tablas.Parametro)
            {
                ["statecode"] = new OptionSetValue(Tablas.Activo),
                ["sanic_nombre"] = ExportarParaDigitar.ParametroMaximo,
                ["sanic_version"] = 1,
                ["sanic_valor"] = valor,
            });
        }

        /// <summary>Siembra los dos parámetros que `Ejecutar` necesita siempre, con un tope alto por defecto para no interferir con pruebas que no lo están probando.</summary>
        private static void SembrarParametros(OrganizationServiceEnMemoria svc, params (string campo, string encabezado)[] columnas)
        {
            SembrarMaximo(svc, "500");
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
            SembrarMaximo(svc, "500"); // sin esto revienta por el tope, no por lo que se quiere probar
            var filaId = svc.Sembrar(Fila(svc, "0001", "Ana Perez", "001-1", "111"));

            var ex = Assert.Throws<InvalidOperationException>(() => Core(svc).Ejecutar(new[] { filaId }));
            Assert.Contains(ExportarParaDigitar.ParametroColumnas, ex.Message);
        }

        [Fact]
        public void Un_parametro_de_columnas_mal_formado_propaga_el_FormatException_del_dominio()
        {
            var svc = new OrganizationServiceEnMemoria();
            SembrarMaximo(svc, "500");
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

        [Fact]
        public void Falta_el_parametro_exportacion_maximo_es_un_error_de_configuracion()
        {
            var svc = new OrganizationServiceEnMemoria();
            SembrarColumnas(svc, ("numeroPlan", "Plan"));
            var filaId = svc.Sembrar(Fila(svc, "0001", "Ana Perez", "001-1", "111"));

            var ex = Assert.Throws<InvalidOperationException>(() => Core(svc).Ejecutar(new[] { filaId }));
            Assert.Contains(ExportarParaDigitar.ParametroMaximo, ex.Message);
        }

        public static System.Collections.Generic.IEnumerable<object[]> MaximosInvalidos()
        {
            yield return new object[] { "0" };
            yield return new object[] { "-1" };
            yield return new object[] { "no soy un numero" };
        }

        [Theory]
        [MemberData(nameof(MaximosInvalidos))]
        public void Exportacion_maximo_invalido_es_un_error_de_configuracion(string valor)
        {
            var svc = new OrganizationServiceEnMemoria();
            SembrarColumnas(svc, ("numeroPlan", "Plan"));
            SembrarMaximo(svc, valor);

            var ex = Assert.Throws<InvalidOperationException>(() => Core(svc).Ejecutar(new Guid[0]));
            Assert.Contains(ExportarParaDigitar.ParametroMaximo, ex.Message);
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

        // ---------- Decision 2 (aprobador, 2026-09-26): tope `exportacion.maximo`, rechaza entero, no recorta ----------

        [Fact]
        public void El_lote_que_supera_el_maximo_revienta_nombrando_pedidas_y_tope_sin_consultar_Dataverse()
        {
            var svc = new OrganizationServiceEnMemoria();
            SembrarColumnas(svc, ("numeroPlan", "Plan"));
            SembrarMaximo(svc, "2");
            var pedidos = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() }; // 3 > 2, ninguno existe

            var ex = Assert.Throws<LoteDeExportacionExcedidoException>(() => Core(svc).Ejecutar(pedidos));

            Assert.Equal(3, ex.Pedidas);
            Assert.Equal(2, ex.Tope);
            // Se rechaza ANTES de tocar Fila: ni siquiera se gasta una consulta en algo que ya se sabe que se va a rechazar.
            Assert.DoesNotContain(svc.Llamadas, l => l.Entidad == TablasHistorico.Fila);
        }

        [Fact]
        public void El_lote_exactamente_igual_al_maximo_no_revienta()
        {
            var svc = new OrganizationServiceEnMemoria();
            SembrarParametros(svc, ("numeroPlan", "Plan"));
            SembrarMaximo(svc, "1");
            var filaId = svc.Sembrar(Fila(svc, "0001", "Ana Perez", "001-1", "111"));

            var resultado = Core(svc).Ejecutar(new[] { filaId }); // 1 == 1, no supera

            Assert.Equal(1, resultado.Exportadas);
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
