// PRUEBAS DE ACEPTACIÓN nuevas (2026-09-26): defecto REAL confirmado en Dev — una Fila llegó a Anulada
// con sanic_claimhasta todavía puesto (no se liberó). Ver diseno/03-contratos-custom-api.md §2, "Cuándo se
// libera": "Al vencer, y al llegar la fila a un estado terminal (Aprobada, Rechazada en AS400, Anulada) o al
// marcarse para atención manual. En Validada → Digitada el claim NO se libera."
//
// "atencionmanual libera el claim" YA está cubierto y en VERDE en FilasDataverseAceptacion.cs
// (MarcarAtencionManual_no_toca_el_estado_pero_libera_el_claim, y los casos de RegistrarAprobacion /
// RegistrarRechazoEnAS400 en el mismo archivo) — esa vía es la del bot (RegistrarResultadoRpa), que ya
// limpia sanic_claimhasta en FilasDataverse. Por eso NO se duplica acá.
//
// Lo que SÍ falta: cuando una PERSONA transiciona la Fila directamente (TransicionDeFilaStep, PreOperation
// de Update), el step nunca toca sanic_claimhasta — ni al aterrizar en un estado terminal (que debería
// liberarlo) ni en Validada → Digitada (que correctamente no lo toca, pero hoy "no lo toca" es un
// accidente: el step no toca esa columna en NINGÚN caso, no una decisión).
//
// NO se toca PasosClaimYAtencionManualAceptacion.cs ni ningún otro archivo existente: esta es una clase de
// pruebas nueva y separada, con su propio mundo mínimo (mismo patrón que esa clase).
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
    public class ClaimSeLiberaAlTransicionarAceptacion
    {
        private static readonly DateTime Ahora = new DateTime(2026, 9, 26, 6, 0, 0, DateTimeKind.Utc);

        private sealed class Mundo
        {
            public readonly OrganizationServiceEnMemoria Svc = new OrganizationServiceEnMemoria();
            public readonly ContextoDePluginSimulado Ctx;
            public Guid SolicitudId, FilaId, Ejecutivo, Supervisor, Rpa;

            public Mundo(EstadoDeLaFila estadoActual, Guid? digitadaPor = null, DateTime? claimHasta = null, string rpaPuedeAprobar = "si")
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
                    ["sanic_nombre"] = "MPPP-00000456",
                    ["sanic_estadoprocesamiento"] = new OptionSetValue((int)EstadoDeLaSolicitud.EnProceso),
                    ["sanic_remitente"] = "ana@acme.com",
                    ["sanic_fecharecibido"] = Ahora,
                    ["sanic_messageid"] = "<m2@x>",
                });

                var fila = new Entity(TablasHistorico.Fila)
                {
                    ["sanic_numerofila"] = 1,
                    ["sanic_estado"] = new OptionSetValue((int)estadoActual),
                    ["sanic_solicitudid"] = new EntityReference(TablasHistorico.Solicitud, SolicitudId),
                    ["sanic_numerocuenta"] = "123456789",
                    ["sanic_nombrebeneficiario"] = "Juan Perez",
                    ["sanic_numeroplan"] = "0042",
                    ["sanic_requiereatencionmanual"] = false,
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

        // ------------------------------------------------------------------ "terminal libera" (03 §2)

        /// <summary>
        /// El defecto REAL visto en Dev el 2026-09-26: MPPP-00001039-F01 llegó a Anulada con
        /// sanic_claimhasta todavía en el futuro. El claim vencido acá (no vigente) es a propósito: no
        /// queremos confundir "el gate de vigencia frena" con "la transición libera el claim al terminar";
        /// son dos reglas distintas y esta prueba aísla la segunda.
        /// </summary>
        [Fact]
        public void Anular_una_fila_con_claim_puesto_libera_el_claim()
        {
            var m = new Mundo(EstadoDeLaFila.Validada, claimHasta: Ahora.AddMinutes(-5));

            var target = m.Correr(m.Ejecutivo, EstadoDeLaFila.Validada, EstadoDeLaFila.Anulada,
                mensaje: "El cliente mandó mal la cuenta");

            Assert.True(target.Contains("sanic_claimhasta"));
            Assert.Null(target["sanic_claimhasta"]);
        }

        [Fact]
        public void Rechazar_en_as400_con_claim_puesto_libera_el_claim()
        {
            var m = new Mundo(EstadoDeLaFila.Validada, claimHasta: Ahora.AddMinutes(-5));

            var target = m.Correr(m.Ejecutivo, EstadoDeLaFila.Validada, EstadoDeLaFila.RechazadaEnAS400,
                mensaje: "Cuenta cerrada en AS400");

            Assert.True(target.Contains("sanic_claimhasta"));
            Assert.Null(target["sanic_claimhasta"]);
        }

        [Fact]
        public void Aprobar_con_claim_puesto_libera_el_claim()
        {
            var otroDigitador = Guid.NewGuid();
            var m = new Mundo(EstadoDeLaFila.Digitada, digitadaPor: otroDigitador, claimHasta: Ahora.AddMinutes(-5));

            var target = m.Correr(m.Supervisor, EstadoDeLaFila.Digitada, EstadoDeLaFila.Aprobada,
                digitadaPorEnPreImagen: otroDigitador);

            Assert.True(target.Contains("sanic_claimhasta"));
            Assert.Null(target["sanic_claimhasta"]);
        }

        /// <summary>El bot llega a estos mismos estados terminales por el mismo step (Depth &gt; 1, nested):
        /// también tiene que quedar liberado, no solo cuando actúa una persona.</summary>
        [Fact]
        public void El_bot_aprobando_lo_que_el_mismo_digito_tambien_libera_el_claim()
        {
            var m = new Mundo(EstadoDeLaFila.Digitada, digitadaPor: null, claimHasta: Ahora.AddMinutes(-5), rpaPuedeAprobar: "si");
            // El bot digitó esta fila: DigitadaPor en la pre-imagen es el propio Rpa.
            var target = m.Correr(m.Rpa, EstadoDeLaFila.Digitada, EstadoDeLaFila.Aprobada, digitadaPorEnPreImagen: m.Rpa);

            Assert.True(target.Contains("sanic_claimhasta"));
            Assert.Null(target["sanic_claimhasta"]);
        }

        // ------------------------------------------------------------------ "Validada → Digitada NO libera" (03 §2)

        /// <summary>
        /// "En Validada → Digitada el claim NO se libera: al bot le queda trabajo sobre esa fila, aprobarla."
        /// Prueba de regresión: hoy pasa (el step no toca la columna en ningún caso), pero pasa por
        /// accidente, no por una regla explícita — por eso se dej a fijada acá.
        /// </summary>
        /// <summary>
        /// El `Target` de un `Update` real solo trae las columnas que ESE mensaje cambia (igual que la
        /// plataforma: un `Update` parcial no trae las columnas que no tocó). Que el step NO libere el
        /// claim en Digitar se prueba, entonces, como que el step NUNCA agrega `sanic_claimhasta` al
        /// `Target` para esta transición — si lo agregara (aunque fuera con el mismo valor), el `Update`
        /// real lo escribiría igual, y listo: el valor real en Dataverse queda intacto solo si el step no
        /// toca la columna.
        /// </summary>
        [Fact]
        public void Digitar_no_libera_el_claim()
        {
            var m = new Mundo(EstadoDeLaFila.Validada, claimHasta: Ahora.AddMinutes(-5));

            var target = m.Correr(m.Ejecutivo, EstadoDeLaFila.Validada, EstadoDeLaFila.Digitada);

            Assert.False(target.Contains("sanic_claimhasta"));
        }

        /// <summary>Devolver (Digitada → Validada) tampoco es un estado terminal: sigue "abierta", así que
        /// tampoco debería liberar el claim (mismo criterio que Digitar, y misma forma de probarlo: el step
        /// no tiene que tocar la columna en el `Target`).</summary>
        [Fact]
        public void Devolver_no_libera_el_claim()
        {
            var otroDigitador = Guid.NewGuid();
            var m = new Mundo(EstadoDeLaFila.Digitada, digitadaPor: otroDigitador, claimHasta: Ahora.AddMinutes(-5));

            var target = m.Correr(m.Supervisor, EstadoDeLaFila.Digitada, EstadoDeLaFila.Validada,
                notaInterna: "Falta el nombre completo", digitadaPorEnPreImagen: otroDigitador);

            Assert.False(target.Contains("sanic_claimhasta"));
        }
    }
}
