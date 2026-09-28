using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xrm.Sdk;
using Sanic.Mppp.Plugins.Api;
using Sanic.Mppp.Plugins.Datos;
using Sanic.Mppp.Plugins.Dominio;
using Sanic.Mppp.Plugins.Plantilla;
using Sanic.Mppp.Plugins.Tests.Apoyo;
using Sanic.Mppp.Plugins.Tests.Dobles;
using Sanic.Mppp.Plugins.Validacion;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Aceptacion
{
    /// <summary>
    /// DN-01, "El modo cambia TODO, no una parte" (diseno/DIRECTRICES-NUEVAS.md, corrección del aprobador, 2026-09-26).
    /// Antes de esta corrección, el diseño dejaba a `REMITENTE_RECONOCIDO` (nivel Correo, en <see cref="ClasificarCorreo"/>)
    /// afuera del modo: con `autorizacion.modo = cliente`, un correo autorizado SOLO por cliente (cero filas en
    /// `AutorizacionPlan`) pasaba `AUTORIZACION_CORREO_PLAN` (nivel Registro) pero quedaba rechazado en
    /// `REMITENTE_RECONOCIDO` — y como una "No reconocida" NO recibe respuesta, el cliente mandaba su solicitud y
    /// nunca se enteraba de nada. La corrección consolida el modo en UN SOLO punto
    /// (<see cref="CatalogosDataverse.PlanesAutorizadosDe"/>), con <see cref="ClasificarCorreo"/> y
    /// <see cref="ValidarSolicitud"/> como únicos llamadores, NINGUNO consciente del modo. Esta prueba corre los DOS
    /// niveles juntos, sobre el MISMO correo y el MISMO servicio: si alguien vuelve a partir el modo en dos, se pone
    /// roja (es la red que sostiene la corrección del aprobador).
    /// </summary>
    public class ModoDeAutorizacionWiringAceptacion
    {
        private static readonly DateTime Ahora = new DateTime(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);
        private const string Hoja = "Datos";
        private static readonly string[] Columnas = { "C", "D", "E", "F", "G", "H", "I", "J", "K", "L" };

        private static readonly Dictionary<string, string> Encabezados = new Dictionary<string, string>
        {
            ["gestion"] = "Gestion", ["clasificacion"] = "Clasificacion", ["tipoIdentificacion"] = "Tipo de identificacion", ["moneda"] = "Moneda",
            ["banco"] = "Banco", ["numeroPlan"] = "No. Plan", ["nombreBeneficiario"] = "Nombre del beneficiario",
            ["numeroIdentificacion"] = "No. Identificacion", ["numeroCuenta"] = "No. Cuenta", ["referencia"] = "Referencia",
        };

        private static Entity Activo(string tabla, params (string atributo, object valor)[] atributos)
        {
            var e = new Entity(tabla) { ["statecode"] = new OptionSetValue(0) };
            foreach (var (atributo, valor) in atributos)
            {
                e[atributo] = valor;
            }

            return e;
        }

        private static string Estructura()
        {
            var campos = CamposDeFila.Todos().Select((c, i) => $@"{{""nombre"":""{c}"",""columna"":""{Columnas[i]}"",""encabezado"":""{Encabezados[c]}""}}");
            return @"{""hoja"":""" + Hoja + @""",""filaEncabezado"":12,""primeraFila"":13,""cantidadFilas"":100,""campos"":[" + string.Join(",", campos) + "]}";
        }

        /// <summary>Una plantilla con UNA sola fila, en el orden de <see cref="CamposDeFila.Todos"/>.</summary>
        private static byte[] Excel(params string[] fila)
        {
            var b = new ConstructorXlsxDePrueba(Hoja);
            foreach (var campo in CamposDeFila.Todos().Select((c, i) => (c, i)))
            {
                b.ConTexto($"{Columnas[campo.i]}12", Encabezados[campo.c]);
            }

            for (var c = 0; c < fila.Length; c++)
            {
                if (!string.IsNullOrEmpty(fila[c]))
                {
                    b.ConTexto($"{Columnas[c]}13", fila[c]);
                }
            }

            return b.Bytes();
        }

        private sealed class ArchivosDeMemoria : IArchivos
        {
            public readonly Dictionary<(string tabla, Guid id, string columna), byte[]> Contenido = new Dictionary<(string, Guid, string), byte[]>();

            public byte[] Descargar(string tabla, Guid id, string columna, long maximoBytes) => Contenido[(tabla, id, columna)];

            public byte[] DescargarInicio(string tabla, Guid id, string columna, int maximoBytes) => Contenido[(tabla, id, columna)].Take(maximoBytes).ToArray();
        }

        private sealed class AvisosSimulados : IAvisos
        {
            public void AvisarAEjecutivos(Guid solicitudId, string titulo, string cuerpo)
            {
            }

            public void AvisarFilasPorDigitar(Guid solicitudId, Guid clienteId, string titulo, string cuerpo)
            {
            }

            public void AvisarFilasPorAprobar(Guid solicitudId, string titulo, string cuerpo)
            {
            }
        }

        [Fact]
        public void En_modo_cliente_un_correo_autorizado_solo_por_cliente_pasa_remitente_reconocido_y_valida_sus_filas()
        {
            var svc = new OrganizationServiceEnMemoria();
            var archivos = new ArchivosDeMemoria();

            // El modo global: cliente.
            svc.Sembrar(Activo(Tablas.Parametro, ("sanic_nombre", ModoDeAutorizacionParametro.Nombre), ("sanic_version", 1), ("sanic_valor", "cliente")));

            // Cliente y su plan; el correo NO tiene NINGUNA fila en AutorizacionPlan — cero filas, a propósito
            // (DN-01: la única vía posible acá es AutorizacionCliente).
            var cliente = svc.Sembrar(Activo(Tablas.Cliente, ("sanic_nombre", "ACME")));
            var planId = svc.Sembrar(Activo(
                Tablas.Plan, ("sanic_codigo", "0042"), ("sanic_tipoformato", new OptionSetValue((int)TipoDeFormatoDelPlan._06)),
                ("sanic_moneda", new OptionSetValue((int)Moneda.USD)), ("sanic_clienteid", new EntityReference(Tablas.Cliente, cliente))));
            var autorizado = svc.Sembrar(Activo(Tablas.Autorizado, ("sanic_nombre", "ana@acme.com")));
            svc.Sembrar(Activo(
                Tablas.AutorizacionCliente, ("sanic_autorizadoid", new EntityReference(Tablas.Autorizado, autorizado)),
                ("sanic_clienteid", new EntityReference(Tablas.Cliente, cliente))));

            // Reglas de nivel Correo (ClasificarCorreo).
            svc.Sembrar(Activo(
                Tablas.Regla, ("sanic_codigo", ReglasDelCorreo.EsCorreoNuevo), ("sanic_nivel", new OptionSetValue((int)NivelDeLaRegla.Correo)),
                ("sanic_orden", 10), ("sanic_efecto", new OptionSetValue((int)EfectoDeLaRegla.EnviaARevision))));
            svc.Sembrar(Activo(
                Tablas.Regla, ("sanic_codigo", ReglasDelCorreo.RemitenteReconocido), ("sanic_nivel", new OptionSetValue((int)NivelDeLaRegla.Correo)),
                ("sanic_orden", 20), ("sanic_dependede", ReglasDelCorreo.EsCorreoNuevo), ("sanic_efecto", new OptionSetValue((int)EfectoDeLaRegla.EnviaARevision))));
            svc.Sembrar(Activo(Tablas.Parametro, ("sanic_nombre", ClasificarCorreo.ParametroPrefijosDeReenvio), ("sanic_version", 1), ("sanic_valor", @"[""FW:""]")));

            // Reglas de nivel Solicitud (el sobre) y de nivel Registro (ValidarSolicitud), igual que ValidarSolicitudAceptacion.
            var sobre = new[] { ReglasDelSobre.TraeAdjunto, ReglasDelSobre.AdjuntoEsExcel, ReglasDelSobre.UnSoloExcel, ReglasDelSobre.EstructuraPlantilla, ReglasDelSobre.TieneFilas };
            for (var i = 0; i < sobre.Length; i++)
            {
                svc.Sembrar(Activo(
                    Tablas.Regla, ("sanic_codigo", sobre[i]), ("sanic_nivel", new OptionSetValue((int)NivelDeLaRegla.Solicitud)), ("sanic_orden", (i + 1) * 10),
                    ("sanic_dependede", i == 0 ? null : sobre[i - 1]), ("sanic_efecto", new OptionSetValue((int)EfectoDeLaRegla.Rechaza))));
            }

            var registro = new (string codigo, string dependeDe)[]
            {
                (ReglasDeRegistro.ListasValidas, null), (ReglasDeRegistro.LargosYFormato, null), (ReglasDeRegistro.PlanExiste, null),
                (ReglasDeRegistro.Formato11SoloAch, ReglasDeRegistro.PlanExiste + "," + ReglasDeRegistro.ListasValidas),
                (ReglasDeRegistro.Obligatoriedad, ReglasDeRegistro.PlanExiste + "," + ReglasDeRegistro.ListasValidas),
                (ReglasDeRegistro.ReferenciaFormato11, ReglasDeRegistro.Formato11SoloAch + "," + ReglasDeRegistro.Obligatoriedad),
                (ReglasDeRegistro.MonedaDelPlan, ReglasDeRegistro.PlanExiste + "," + ReglasDeRegistro.ListasValidas),
                (ReglasDeRegistro.AutorizacionCorreoPlan, ReglasDeRegistro.PlanExiste),
            };
            for (var i = 0; i < registro.Length; i++)
            {
                svc.Sembrar(Activo(
                    Tablas.Regla, ("sanic_codigo", registro[i].codigo), ("sanic_nivel", new OptionSetValue((int)NivelDeLaRegla.Registro)), ("sanic_orden", (i + 1) * 10),
                    ("sanic_dependede", registro[i].dependeDe), ("sanic_efecto", new OptionSetValue((int)EfectoDeLaRegla.Rechaza))));
            }

            svc.Sembrar(Activo(Tablas.Parametro, ("sanic_nombre", ValidarSolicitud.ParametroEstructura), ("sanic_version", 1), ("sanic_valor", Estructura())));
            svc.Sembrar(Activo(Tablas.Parametro, ("sanic_nombre", ValidarSolicitud.ParametroListas), ("sanic_version", 1), ("sanic_valor", ParametrosDePlantillaAceptacion.ListasValidas)));
            svc.Sembrar(Activo(Tablas.Parametro, ("sanic_nombre", ValidarSolicitud.ParametroObligatoriedad), ("sanic_version", 1), ("sanic_valor", ParametrosDePlantillaAceptacion.ObligatoriedadInicial)));
            svc.Sembrar(Activo(Tablas.Parametro, ("sanic_nombre", ValidarSolicitud.ParametroLimites), ("sanic_version", 1), ("sanic_valor", @"{""maximoBytesComprimido"":2097152,""maximoBytesDescomprimido"":20971520}")));

            var solicitudId = svc.Sembrar(new Entity(TablasHistorico.Solicitud)
            {
                ["sanic_nombre"] = "MPPP-00000999", ["sanic_estadoprocesamiento"] = new OptionSetValue((int)EstadoDeLaSolicitud.Ingresada), ["sanic_remitente"] = "ana@acme.com",
                ["sanic_asunto"] = "Inclusiones", ["sanic_fecharecibido"] = Ahora, ["sanic_messageid"] = "<m@x>",
                ["sanic_cantidadadjuntos"] = 1, ["sanic_cantidadexcel"] = 1,
            });

            archivos.Contenido[(TablasHistorico.Solicitud, solicitudId, "sanic_correocrudo")] = Encoding.UTF8.GetBytes("Subject: Inclusiones\r\n\r\ncuerpo");
            archivos.Contenido[(TablasHistorico.Solicitud, solicitudId, ValidarSolicitud.ColumnaExcel)] =
                Excel("Inclusion", "ACH", "CNA", "USD", "BAC", "0042", "Juan Perez", "0011234567890", "123456789", string.Empty);

            var catalogos = new CatalogosDataverse(svc);

            // ------------------------------------------------------------ nivel Correo: el correo tiene que ENTRAR.
            var clasificacion = new ClasificarCorreo(new SolicitudesDataverse(svc), catalogos, archivos, new AvisosSimulados()).Ejecutar(solicitudId, Ahora);

            Assert.True(clasificacion.Procesar);
            Assert.NotEqual(Clasificaciones.RemitenteNoReconocido, clasificacion.Clasificacion);

            // ------------------------------------------------------------ nivel Registro: la MISMA autorización por
            // cliente tiene que validar la fila (recién acá se abre el Excel).
            var resultado = new ValidarSolicitud(new SolicitudesDataverse(svc), catalogos, archivos, limites => new LectorOpenXml(limites), new AvisosSimulados()).Ejecutar(solicitudId, Ahora);

            Assert.Equal((EstadoDeLaSolicitud.EnProceso, 1, 1, 0), (resultado.Estado, resultado.FilasTotales, resultado.FilasValidas, resultado.FilasRechazadas));
            var fila = Assert.Single(svc.Registros(TablasHistorico.Fila));
            Assert.Equal((int)EstadoDeLaFila.Validada, fila.GetAttributeValue<OptionSetValue>("sanic_estado").Value);
            Assert.Equal(planId, fila.GetAttributeValue<EntityReference>("sanic_planid").Id);
        }
    }
}
