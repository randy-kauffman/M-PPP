// PRUEBAS DE ACEPTACIÓN nuevas (2026-09-25): puntos 4 y 5 de diseno/FASE2-ESTADO.md, a nivel del STEP real
// (TransicionDeFilaStep), con los dobles de Dataverse. Ver diseno/03-contratos-custom-api.md §2 ("El claim")
// y §3 ("Atención manual: una MARCA, no un estado"). NO se toca StepsDeFilaAceptacion.cs (archivo existente):
// esta es una clase de pruebas nueva y separada, con su propio mundo mínimo.
using System;
using Microsoft.Xrm.Sdk;
using Sanic.Mppp.Plugins.Datos;
using Sanic.Mppp.Plugins.Dominio;
using Sanic.Mppp.Plugins.Steps;
using Sanic.Mppp.Plugins.Tests.Apoyo;
using Sanic.Mppp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Aceptacion
{
    public class PasosClaimYAtencionManualAceptacion
    {
        private static readonly DateTime Ahora = new DateTime(2026, 9, 25, 19, 0, 0, DateTimeKind.Utc);

        private sealed class Mundo
        {
            public readonly OrganizationServiceEnMemoria Svc = new OrganizationServiceEnMemoria();
            public readonly ContextoDePluginSimulado Ctx;
            public Guid SolicitudId, FilaId, Ejecutivo, Supervisor, Rpa;

            public Mundo(EstadoDeLaFila estadoActual, Guid? digitadaPor = null, DateTime? claimHasta = null,
                         bool marcaAtencionManual = false, string rpaPuedeAprobar = "no")
            {
                Guid Rol(string nombre) => Svc.Sembrar(new Entity(TablasNativas.Rol) { ["name"] = nombre });
                var rolEjecutivo = Rol(TablasNativas.RolEjecutivo);
                var rolSupervisor = Rol(TablasNativas.RolSupervisor);
                var rolRpa = Rol(TablasNativas.RolRpa);

                Ejecutivo = Usuario("Eje Cutivo", rolEjecutivo);
                Supervisor = Usuario("Super Visor", rolSupervisor);
                Rpa = Usuario("Bot RPA", rolRpa, esAplicacion: true);

                Svc.Sembrar(new Entity(Tablas.Parametro)
                {
                    ["statecode"] = new OptionSetValue(0),
                    ["sanic_nombre"] = TransicionDeFilaStep.ParametroRpaPuedeAprobar,
                    ["sanic_version"] = 1,
                    ["sanic_valor"] = rpaPuedeAprobar,
                });

                SolicitudId = Svc.Sembrar(new Entity(TablasHistorico.Solicitud)
                {
                    ["sanic_nombre"] = "MPPP-00000123",
                    ["sanic_estadoprocesamiento"] = new OptionSetValue((int)EstadoDeLaSolicitud.EnProceso),
                    ["sanic_remitente"] = "ana@acme.com",
                    ["sanic_fecharecibido"] = Ahora,
                    ["sanic_messageid"] = "<m@x>",
                });

                var fila = new Entity(TablasHistorico.Fila)
                {
                    ["sanic_numerofila"] = 1,
                    ["sanic_estado"] = new OptionSetValue((int)estadoActual),
                    ["sanic_solicitudid"] = new EntityReference(TablasHistorico.Solicitud, SolicitudId),
                    ["sanic_numerocuenta"] = "123456789",
                    ["sanic_nombrebeneficiario"] = "Juan Perez",
                    ["sanic_numeroplan"] = "0042",
                    ["sanic_requiereatencionmanual"] = marcaAtencionManual,
                };
                if (digitadaPor.HasValue)
                {
                    fila["sanic_digitadapor"] = new EntityReference(TablasNativas.Usuario, digitadaPor.Value);
                }

                if (claimHasta.HasValue)
                {
                    fila["sanic_claimhasta"] = claimHasta.Value;
                }

                FilaId = Svc.Sembrar(fila);
                Ctx = new ContextoDePluginSimulado(Svc, Ahora);
                Ctx.Contexto.MessageName = "Update";
                Ctx.Contexto.PrimaryEntityName = TablasHistorico.Fila;
            }

            public Guid Usuario(string nombre, Guid rol, bool esAplicacion = false)
            {
                var u = new Entity(TablasNativas.Usuario) { ["fullname"] = nombre, ["isdisabled"] = false };
                if (esAplicacion)
                {
                    u["applicationid"] = Guid.NewGuid();
                }

                var id = Svc.Sembrar(u);
                Svc.Sembrar(new Entity(TablasNativas.UsuarioRol) { ["systemuserid"] = id, ["roleid"] = rol });
                return id;
            }

            /// <summary>Arma Target + PreImage como la plataforma y corre <see cref="TransicionDeFilaStep"/>.</summary>
            public Entity Correr(Guid quienLlama, EstadoDeLaFila desde, EstadoDeLaFila hacia, string mensaje = null,
                                  string notaInterna = null, Guid? digitadaPorEnPreImagen = null)
            {
                var target = new Entity(TablasHistorico.Fila, FilaId) { ["sanic_estado"] = new OptionSetValue((int)hacia) };
                if (mensaje != null)
                {
                    target["sanic_mensaje"] = mensaje;
                }

                if (notaInterna != null)
                {
                    target["sanic_notainterna"] = notaInterna;
                }

                var pre = new Entity(TablasHistorico.Fila, FilaId)
                {
                    ["sanic_estado"] = new OptionSetValue((int)desde),
                    ["sanic_solicitudid"] = new EntityReference(TablasHistorico.Solicitud, SolicitudId),
                    ["sanic_numerofila"] = 1,
                };
                if (digitadaPorEnPreImagen.HasValue)
                {
                    pre["sanic_digitadapor"] = new EntityReference(TablasNativas.Usuario, digitadaPorEnPreImagen.Value);
                }

                Ctx.Contexto.InitiatingUserId = quienLlama;
                Ctx.Contexto.UserId = quienLlama;
                Ctx.Contexto.InputParameters["Target"] = target;
                Ctx.Contexto.PreEntityImages["PreImage"] = pre;
                new TransicionDeFilaStep().Execute(Ctx);
                return target;
            }
        }

        // ------------------------------------------------------------------ punto 4: el claim frena a las PERSONAS

        [Fact]
        public void El_step_rechaza_a_una_persona_mientras_el_claim_este_vigente_y_dice_cuanto_falta_para_liberarse()
        {
            // El mensaje dice una DURACIÓN, no una hora (corrección del aprobador, 2026-09-25): `Retrieve`
            // devuelve `sanic_claimhasta` en UTC, y Nicaragua es UTC-6 — una hora sin convertir le mentiría al
            // ejecutivo por seis horas.
            var claimHasta = Ahora.AddMinutes(10);
            var m = new Mundo(EstadoDeLaFila.Validada, claimHasta: claimHasta);

            var ex = Assert.Throws<InvalidPluginExecutionException>(
                () => m.Correr(m.Ejecutivo, EstadoDeLaFila.Validada, EstadoDeLaFila.Digitada));

            Assert.Contains("en 10 minutos", ex.Message);
        }

        [Fact]
        public void El_step_deja_pasar_a_una_persona_cuando_el_claim_ya_vencio()
        {
            var claimHasta = Ahora.AddMinutes(-10);
            var m = new Mundo(EstadoDeLaFila.Validada, claimHasta: claimHasta);

            var target = m.Correr(m.Ejecutivo, EstadoDeLaFila.Validada, EstadoDeLaFila.Digitada);

            Assert.Equal(m.Ejecutivo, target.GetAttributeValue<EntityReference>("sanic_digitadapor").Id);
        }

        [Fact]
        public void El_step_deja_pasar_a_una_persona_cuando_la_fila_no_tiene_ningun_claim()
        {
            var m = new Mundo(EstadoDeLaFila.Validada); // sin claimHasta

            var target = m.Correr(m.Ejecutivo, EstadoDeLaFila.Validada, EstadoDeLaFila.Digitada);

            Assert.Equal(m.Ejecutivo, target.GetAttributeValue<EntityReference>("sanic_digitadapor").Id);
        }

        [Theory]
        [InlineData(10)]   // claim vigente
        [InlineData(-10)]  // claim vencido
        public void El_bot_transiciona_siempre_sin_que_el_claim_lo_frene(int minutosDesdeAhora)
        {
            var claimHasta = Ahora.AddMinutes(minutosDesdeAhora);
            var m = new Mundo(EstadoDeLaFila.Validada, claimHasta: claimHasta);

            var target = m.Correr(m.Rpa, EstadoDeLaFila.Validada, EstadoDeLaFila.Digitada);

            Assert.Equal(m.Rpa, target.GetAttributeValue<EntityReference>("sanic_digitadapor").Id);
        }

        // ------------------------------------------------------------------ punto 5: la marca se limpia sola

        [Fact]
        public void Digitar_exitosamente_apaga_la_marca_de_atencion_manual()
        {
            var m = new Mundo(EstadoDeLaFila.Validada, marcaAtencionManual: true);

            var target = m.Correr(m.Ejecutivo, EstadoDeLaFila.Validada, EstadoDeLaFila.Digitada);

            Assert.True(target.Contains("sanic_requiereatencionmanual"));
            Assert.False((bool)target["sanic_requiereatencionmanual"]);
        }

        [Fact]
        public void Rechazar_en_as400_exitosamente_apaga_la_marca_de_atencion_manual()
        {
            var m = new Mundo(EstadoDeLaFila.Validada, marcaAtencionManual: true);

            var target = m.Correr(m.Ejecutivo, EstadoDeLaFila.Validada, EstadoDeLaFila.RechazadaEnAS400, mensaje: "Cuenta cerrada en AS400");

            Assert.True(target.Contains("sanic_requiereatencionmanual"));
            Assert.False((bool)target["sanic_requiereatencionmanual"]);
        }

        [Fact]
        public void Anular_exitosamente_apaga_la_marca_de_atencion_manual()
        {
            var m = new Mundo(EstadoDeLaFila.Validada, marcaAtencionManual: true);

            var target = m.Correr(m.Ejecutivo, EstadoDeLaFila.Validada, EstadoDeLaFila.Anulada, mensaje: "El cliente mandó mal la cuenta");

            Assert.True(target.Contains("sanic_requiereatencionmanual"));
            Assert.False((bool)target["sanic_requiereatencionmanual"]);
        }

        [Fact]
        public void Aprobar_exitosamente_apaga_la_marca_de_atencion_manual()
        {
            var otroDigitador = Guid.NewGuid();
            var m = new Mundo(EstadoDeLaFila.Digitada, digitadaPor: otroDigitador, marcaAtencionManual: true);

            var target = m.Correr(m.Supervisor, EstadoDeLaFila.Digitada, EstadoDeLaFila.Aprobada, digitadaPorEnPreImagen: otroDigitador);

            Assert.True(target.Contains("sanic_requiereatencionmanual"));
            Assert.False((bool)target["sanic_requiereatencionmanual"]);
        }

        [Fact]
        public void Devolver_exitosamente_apaga_la_marca_de_atencion_manual()
        {
            var otroDigitador = Guid.NewGuid();
            var m = new Mundo(EstadoDeLaFila.Digitada, digitadaPor: otroDigitador, marcaAtencionManual: true);

            var target = m.Correr(m.Supervisor, EstadoDeLaFila.Digitada, EstadoDeLaFila.Validada,
                notaInterna: "Falta el nombre completo", digitadaPorEnPreImagen: otroDigitador);

            Assert.True(target.Contains("sanic_requiereatencionmanual"));
            Assert.False((bool)target["sanic_requiereatencionmanual"]);
        }

        /// <summary>La regla no distingue actor: también corre para el bot, sin que nadie tenga que acordarse.</summary>
        [Fact]
        public void El_bot_transicionando_tambien_apaga_la_marca_de_atencion_manual()
        {
            var m = new Mundo(EstadoDeLaFila.Validada, marcaAtencionManual: true);

            var target = m.Correr(m.Rpa, EstadoDeLaFila.Validada, EstadoDeLaFila.Digitada);

            Assert.True(target.Contains("sanic_requiereatencionmanual"));
            Assert.False((bool)target["sanic_requiereatencionmanual"]);
        }

        [Fact]
        public void Una_transicion_rechazada_no_apaga_la_marca_de_atencion_manual()
        {
            // El ejecutivo no puede aprobar (eso es del supervisor): la transición se rechaza.
            var m = new Mundo(EstadoDeLaFila.Digitada, digitadaPor: Guid.NewGuid(), marcaAtencionManual: true);

            var target = new Entity(TablasHistorico.Fila, m.FilaId) { ["sanic_estado"] = new OptionSetValue((int)EstadoDeLaFila.Aprobada) };
            var pre = new Entity(TablasHistorico.Fila, m.FilaId)
            {
                ["sanic_estado"] = new OptionSetValue((int)EstadoDeLaFila.Digitada),
                ["sanic_solicitudid"] = new EntityReference(TablasHistorico.Solicitud, m.SolicitudId),
                ["sanic_numerofila"] = 1,
            };
            m.Ctx.Contexto.InitiatingUserId = m.Ejecutivo;
            m.Ctx.Contexto.UserId = m.Ejecutivo;
            m.Ctx.Contexto.InputParameters["Target"] = target;
            m.Ctx.Contexto.PreEntityImages["PreImage"] = pre;

            Assert.Throws<InvalidPluginExecutionException>(() => new TransicionDeFilaStep().Execute(m.Ctx));

            Assert.False(target.Contains("sanic_requiereatencionmanual"));
        }

        [Fact]
        public void Una_transicion_rechazada_por_el_claim_tampoco_apaga_la_marca_de_atencion_manual()
        {
            var claimHasta = Ahora.AddMinutes(10);
            var m = new Mundo(EstadoDeLaFila.Validada, claimHasta: claimHasta, marcaAtencionManual: true);

            var target = new Entity(TablasHistorico.Fila, m.FilaId) { ["sanic_estado"] = new OptionSetValue((int)EstadoDeLaFila.Digitada) };
            var pre = new Entity(TablasHistorico.Fila, m.FilaId)
            {
                ["sanic_estado"] = new OptionSetValue((int)EstadoDeLaFila.Validada),
                ["sanic_solicitudid"] = new EntityReference(TablasHistorico.Solicitud, m.SolicitudId),
                ["sanic_numerofila"] = 1,
            };
            m.Ctx.Contexto.InitiatingUserId = m.Ejecutivo;
            m.Ctx.Contexto.UserId = m.Ejecutivo;
            m.Ctx.Contexto.InputParameters["Target"] = target;
            m.Ctx.Contexto.PreEntityImages["PreImage"] = pre;

            Assert.Throws<InvalidPluginExecutionException>(() => new TransicionDeFilaStep().Execute(m.Ctx));

            Assert.False(target.Contains("sanic_requiereatencionmanual"));
        }
    }
}
