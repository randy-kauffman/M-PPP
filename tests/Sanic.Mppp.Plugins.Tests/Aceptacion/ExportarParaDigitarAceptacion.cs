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
    /// A PROPÓSITO no hay pruebas acá de: (a) un `filaId` que no existe o que no está en un estado exportable —
    /// pendiente de decisión del aprobador (ver el reporte de la Etapa 1); (b) un tope de filas por llamada —
    /// también pendiente (BP-PP-053, la escritura del Excel está sin medir).
    /// </summary>
    public class ExportarParaDigitarAceptacion
    {
        private static readonly DateTime Base = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

        private static Entity Fila(string numeroPlan, string nombreBeneficiario, string numeroIdentificacion, string numeroCuenta)
        {
            return new Entity(TablasHistorico.Fila)
            {
                ["sanic_estado"] = new OptionSetValue((int)EstadoDeLaFila.Validada),
                ["sanic_fechavalidada"] = Base,
                ["sanic_numerofila"] = 1,
                ["sanic_requiereatencionmanual"] = false,
                ["sanic_solicitudid"] = new EntityReference(TablasHistorico.Solicitud, Guid.NewGuid()),
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

        private static ExportarParaDigitar Core(OrganizationServiceEnMemoria svc)
        {
            return new ExportarParaDigitar(new FilasDataverse(svc), new CatalogosDataverse(svc), new ArmadorExportacionOpenXml());
        }

        [Fact]
        public void Arma_el_archivo_con_las_filas_pedidas_en_el_orden_pedido_y_cuenta_exportadas()
        {
            var svc = new OrganizationServiceEnMemoria();
            SembrarColumnas(svc, ("numeroPlan", "Plan"), ("nombreBeneficiario", "Nombre"));
            var primera = svc.Sembrar(Fila("0001", "Ana Perez", "001-1", "111"));
            var segunda = svc.Sembrar(Fila("0002", "Beto Ruiz", "001-2", "222"));

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
            SembrarColumnas(svc, ("numeroPlan", "Plan"));

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
            SembrarColumnas(svc, ("numeroIdentificacion", "Identificacion"), ("numeroCuenta", "Cuenta"));
            var filaId = svc.Sembrar(Fila("0001", "Ana Perez", "001-000000-0000A", "0001234567890123"));

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
            SembrarColumnas(svc, ("gestion", "Gestion"), ("clasificacion", "Clasificacion"), ("tipoIdentificacion", "TipoId"), ("moneda", "Moneda"), ("banco", "Banco"));
            var filaId = svc.Sembrar(Fila("0001", "Ana Perez", "001-1", "111"));

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
        public void Un_campo_pedido_fuera_del_vocabulario_conocido_revienta_con_el_motivo()
        {
            var svc = new OrganizationServiceEnMemoria();
            SembrarColumnas(svc, ("campoInventado", "Rara"));
            var filaId = svc.Sembrar(Fila("0001", "Ana Perez", "001-1", "111"));

            var ex = Assert.Throws<ArgumentException>(() => Core(svc).Ejecutar(new[] { filaId }));
            Assert.Contains("campoInventado", ex.Message);
        }

        [Fact]
        public void Falta_el_parametro_exportacion_columnas_es_un_error_de_configuracion()
        {
            var svc = new OrganizationServiceEnMemoria();
            var filaId = svc.Sembrar(Fila("0001", "Ana Perez", "001-1", "111"));

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
            var filaId = svc.Sembrar(Fila("0001", "Ana Perez", "001-1", "111"));

            Assert.Throws<FormatException>(() => Core(svc).Ejecutar(new[] { filaId }));
        }

        [Fact]
        public void FilaIds_nulo_es_un_error_de_programacion()
        {
            var svc = new OrganizationServiceEnMemoria();
            SembrarColumnas(svc, ("numeroPlan", "Plan"));
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
