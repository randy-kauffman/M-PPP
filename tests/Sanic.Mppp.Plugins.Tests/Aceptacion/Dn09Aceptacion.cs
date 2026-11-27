using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Mppp.Plugins.Datos;
using Sanic.Mppp.Plugins.Dominio;
using Sanic.Mppp.Plugins.Steps;
using Sanic.Mppp.Plugins.Tests.Apoyo;
using Sanic.Mppp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Aceptacion
{
    /// <summary>
    /// DN-09 C (diseno/DIRECTRICES-NUEVAS.md): la Bitácora de cada transición de Fila guarda el "por qué" en `sanic_detalle`, porque
    /// en la fila la acción siguiente lo pisa. Pruebas del ARQUITECTO: el constructor no las cambia.
    /// </summary>
    public class Dn09Aceptacion
    {
        private static readonly DateTime Ahora = new DateTime(2026, 11, 27, 18, 0, 0, DateTimeKind.Utc);

        // ================================================================================================ función pura

        [Fact]
        public void Rechazada_en_AS400_guarda_el_mensaje_al_cliente()
        {
            Assert.Equal("Mensaje al cliente: La cuenta está cerrada.",
                TransicionesDeFila.DetalleDeBitacora(EstadoDeLaFila.RechazadaEnAS400, "La cuenta está cerrada.", null));
        }

        [Fact]
        public void Rechazada_por_el_RPA_guarda_el_mensaje_y_la_nota_en_dos_lineas()
        {
            Assert.Equal("Mensaje al cliente: La cuenta está cerrada.\nNota interna: AS400 E-114 cuenta cancelada",
                TransicionesDeFila.DetalleDeBitacora(EstadoDeLaFila.RechazadaEnAS400, "La cuenta está cerrada.", "AS400 E-114 cuenta cancelada"));
        }

        [Fact]
        public void Anulada_guarda_el_mensaje_al_cliente()
        {
            Assert.Equal("Mensaje al cliente: El cliente pidió anularla.",
                TransicionesDeFila.DetalleDeBitacora(EstadoDeLaFila.Anulada, "El cliente pidió anularla.", null));
        }

        [Fact]
        public void La_devolucion_guarda_solo_la_nota_interna_aunque_venga_un_mensaje()
        {
            Assert.Equal("Nota interna: Falta el banco.",
                TransicionesDeFila.DetalleDeBitacora(EstadoDeLaFila.Validada, "texto que no corresponde", "Falta el banco."));
        }

        [Theory]
        [InlineData(EstadoDeLaFila.Digitada)]
        [InlineData(EstadoDeLaFila.Aprobada)]
        public void Digitada_y_aprobada_no_llevan_detalle_aunque_vengan_textos(EstadoDeLaFila hacia)
        {
            Assert.Null(TransicionesDeFila.DetalleDeBitacora(hacia, "algo", "otra cosa"));
        }

        [Fact]
        public void Los_textos_se_recortan_y_los_vacios_no_generan_linea()
        {
            Assert.Equal("Mensaje al cliente: La cuenta está cerrada.",
                TransicionesDeFila.DetalleDeBitacora(EstadoDeLaFila.RechazadaEnAS400, "  La cuenta está cerrada.  ", "   "));
            Assert.Equal("Nota interna: solo la nota",
                TransicionesDeFila.DetalleDeBitacora(EstadoDeLaFila.Anulada, " ", " solo la nota "));
            Assert.Null(TransicionesDeFila.DetalleDeBitacora(EstadoDeLaFila.Anulada, null, ""));
            Assert.Null(TransicionesDeFila.DetalleDeBitacora(EstadoDeLaFila.Validada, "mensaje", null));
        }

        // ================================================================================================ el step escribe el detalle

        /// <summary>Lo mínimo para correr <see cref="PostTransicionDeFilaStep"/> como en <c>StepsDeFilaAceptacion</c>.</summary>
        private sealed class Mundo
        {
            public readonly OrganizationServiceEnMemoria Svc = new OrganizationServiceEnMemoria();
            public readonly ContextoDePluginSimulado Ctx;
            public readonly Guid Ejecutivo, Supervisor;
            public Guid SolicitudId, FilaId;

            public Mundo(EstadoDeLaFila estado, Guid? digitadaPor = null)
            {
                var rolEjecutivo = Svc.Sembrar(new Entity(TablasNativas.Rol) { ["name"] = TablasNativas.RolEjecutivo });
                var rolSupervisor = Svc.Sembrar(new Entity(TablasNativas.Rol) { ["name"] = TablasNativas.RolSupervisor });
                Svc.Sembrar(new Entity(TablasNativas.Rol) { ["name"] = TablasNativas.RolRpa });
                Ejecutivo = Usuario("Eje Cutivo", rolEjecutivo);
                Supervisor = Usuario("Super Visor", rolSupervisor);
                Svc.Sembrar(new Entity(Tablas.Parametro) { ["statecode"] = new OptionSetValue(0), ["sanic_nombre"] = TransicionDeFilaStep.ParametroRpaPuedeAprobar, ["sanic_version"] = 1, ["sanic_valor"] = "no" });
                SolicitudId = Svc.Sembrar(new Entity(TablasHistorico.Solicitud)
                {
                    ["sanic_nombre"] = "MPPP-00000123", ["sanic_estadoprocesamiento"] = new OptionSetValue((int)EstadoDeLaSolicitud.EnProceso),
                    ["sanic_remitente"] = "ana@acme.com", ["sanic_fecharecibido"] = Ahora, ["sanic_messageid"] = "<m@x>",
                });
                var fila = new Entity(TablasHistorico.Fila)
                {
                    ["sanic_numerofila"] = 3, ["sanic_estado"] = new OptionSetValue((int)estado),
                    ["sanic_solicitudid"] = new EntityReference(TablasHistorico.Solicitud, SolicitudId),
                    ["sanic_numerocuenta"] = "123456789", ["sanic_nombrebeneficiario"] = "Juan Perez", ["sanic_numeroplan"] = "0042",
                };
                if (digitadaPor.HasValue) fila["sanic_digitadapor"] = new EntityReference(TablasNativas.Usuario, digitadaPor.Value);
                FilaId = Svc.Sembrar(fila);
                // Otra fila abierta: así la solicitud no se cierra y el step no arma la respuesta final (no es lo que se prueba acá).
                Svc.Sembrar(new Entity(TablasHistorico.Fila)
                {
                    ["sanic_numerofila"] = 9, ["sanic_estado"] = new OptionSetValue((int)EstadoDeLaFila.Validada),
                    ["sanic_solicitudid"] = new EntityReference(TablasHistorico.Solicitud, SolicitudId),
                    ["sanic_numerocuenta"] = "987654321", ["sanic_nombrebeneficiario"] = "Otra", ["sanic_numeroplan"] = "0042",
                });
                Ctx = new ContextoDePluginSimulado(Svc, Ahora);
                Ctx.Contexto.MessageName = "Update";
                Ctx.Contexto.PrimaryEntityName = TablasHistorico.Fila;
            }

            private Guid Usuario(string nombre, Guid rol)
            {
                var id = Svc.Sembrar(new Entity(TablasNativas.Usuario) { ["fullname"] = nombre, ["isdisabled"] = false });
                Svc.Sembrar(new Entity(TablasNativas.UsuarioRol) { ["systemuserid"] = id, ["roleid"] = rol });
                return id;
            }

            /// <summary>Corre el step de post-transición con el `Target` y la pre-image como los manda la plataforma.</summary>
            public Entity BitacoraDe(Guid quienLlama, EstadoDeLaFila hacia, string mensaje = null, string nota = null)
            {
                var actual = Svc.Retrieve(TablasHistorico.Fila, FilaId, new ColumnSet(true));
                var target = new Entity(TablasHistorico.Fila, FilaId) { ["sanic_estado"] = new OptionSetValue((int)hacia) };
                if (mensaje != null) target["sanic_mensaje"] = mensaje;
                if (nota != null) target["sanic_notainterna"] = nota;
                var pre = new Entity(TablasHistorico.Fila, FilaId)
                {
                    ["sanic_estado"] = actual["sanic_estado"],
                    ["sanic_solicitudid"] = new EntityReference(TablasHistorico.Solicitud, SolicitudId),
                    ["sanic_numerofila"] = actual["sanic_numerofila"],
                };
                var digitador = actual.GetAttributeValue<EntityReference>("sanic_digitadapor");
                if (digitador != null) pre["sanic_digitadapor"] = digitador;

                Ctx.Contexto.InitiatingUserId = quienLlama;
                Ctx.Contexto.UserId = quienLlama;
                Ctx.Contexto.InputParameters["Target"] = target;
                Ctx.Contexto.PreEntityImages["PreImage"] = pre;
                new PostTransicionDeFilaStep().Execute(Ctx);
                return Assert.Single(Svc.Registros(TablasHistorico.Bitacora).Where(b => b.GetAttributeValue<int>("sanic_numerofila") == 3));
            }
        }

        [Fact]
        public void El_rechazo_en_AS400_deja_el_mensaje_en_la_bitacora()
        {
            var m = new Mundo(EstadoDeLaFila.Validada);

            var b = m.BitacoraDe(m.Ejecutivo, EstadoDeLaFila.RechazadaEnAS400, mensaje: "La cuenta está cerrada.");

            Assert.Equal((int)EventoDeBitacora.FilaRechazadaEnAS400, b.GetAttributeValue<OptionSetValue>("sanic_evento").Value);
            Assert.Equal("Mensaje al cliente: La cuenta está cerrada.", b.GetAttributeValue<string>("sanic_detalle"));
            Assert.Contains("Eje", b.GetAttributeValue<string>("sanic_actortexto"));
        }

        [Fact]
        public void La_devolucion_deja_la_nota_interna_en_la_bitacora()
        {
            var digitador = Guid.NewGuid();
            var m = new Mundo(EstadoDeLaFila.Digitada, digitador);

            var b = m.BitacoraDe(m.Supervisor, EstadoDeLaFila.Validada, nota: "Falta el banco.");

            Assert.Equal((int)EventoDeBitacora.FilaDevuelta, b.GetAttributeValue<OptionSetValue>("sanic_evento").Value);
            Assert.Equal("Nota interna: Falta el banco.", b.GetAttributeValue<string>("sanic_detalle"));
        }

        [Fact]
        public void La_anulacion_deja_el_mensaje_en_la_bitacora()
        {
            var m = new Mundo(EstadoDeLaFila.Validada);

            var b = m.BitacoraDe(m.Ejecutivo, EstadoDeLaFila.Anulada, mensaje: "El cliente pidió anularla.");

            Assert.Equal("Mensaje al cliente: El cliente pidió anularla.", b.GetAttributeValue<string>("sanic_detalle"));
        }

        [Fact]
        public void Digitar_no_deja_detalle()
        {
            var m = new Mundo(EstadoDeLaFila.Validada);

            var b = m.BitacoraDe(m.Ejecutivo, EstadoDeLaFila.Digitada);

            Assert.Null(b.GetAttributeValue<string>("sanic_detalle"));
        }

        [Fact]
        public void Un_texto_de_mas_de_10000_caracteres_se_recorta_al_escribir_la_bitacora()
        {
            var m = new Mundo(EstadoDeLaFila.Validada);

            var b = m.BitacoraDe(m.Ejecutivo, EstadoDeLaFila.RechazadaEnAS400, mensaje: new string('x', 12000));

            Assert.Equal(TablasHistorico.LargoDetalle, b.GetAttributeValue<string>("sanic_detalle").Length);
        }
    }
}
