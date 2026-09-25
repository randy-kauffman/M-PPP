using System;
using Microsoft.Xrm.Sdk;
using Sanic.Mppp.Plugins.Datos;
using Sanic.Mppp.Plugins.Steps;
using Sanic.Mppp.Plugins.Tests.Apoyo;
using Sanic.Mppp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Aceptacion
{
    /// <summary>
    /// 7.x: la puerta de las dos Custom API del RPA (diseno/03, D-9 y D-44).
    ///
    /// **Estas pruebas SON la condición con la que se cerró D-9**: "una prueba de aceptación por API que verifique el
    /// rechazo de un llamador no autorizado". Sin ellas la decisión no está completa, y así quedó escrito en `03`.
    ///
    /// Lo que importa acá no es que el bot pase —eso ya se verificó contra el entorno real con
    /// `herramientas/pruebas/como_rpa.py`— sino que **todos los demás NO pasen**. Que la puerta se abra para quien debe
    /// es la mitad fácil; la que protege al banco es la otra.
    /// </summary>
    public class PuertaDelRpaAceptacion
    {
        private static readonly DateTime Ahora = new DateTime(2026, 9, 25, 18, 0, 0, DateTimeKind.Utc);

        private static (IPluginExecutionContext ctx, IOrganizationService svc) Armar(
            bool identidadDeAplicacion,
            bool conElRol,
            bool elRolExisteEnElEntorno = true,
            string nombreDelRol = TablasNativas.RolRpa)
        {
            var svc = new OrganizationServiceEnMemoria();
            var ctx = new ContextoDePluginSimulado(svc, Ahora);

            var usuario = new Entity(TablasNativas.Usuario) { ["fullname"] = "Quien llama" };
            if (identidadDeAplicacion)
            {
                usuario["applicationid"] = Guid.NewGuid();
            }

            var usuarioId = svc.Sembrar(usuario);
            ctx.Contexto.InitiatingUserId = usuarioId;

            if (elRolExisteEnElEntorno)
            {
                var rolId = svc.Sembrar(new Entity(TablasNativas.Rol) { ["name"] = nombreDelRol });
                if (conElRol)
                {
                    svc.Sembrar(new Entity(TablasNativas.UsuarioRol)
                    {
                        ["systemuserid"] = usuarioId,
                        ["roleid"] = rolId
                    });
                }
            }

            return (ctx.Contexto, svc);
        }

        private static string Rechazo(bool identidadDeAplicacion, bool conElRol,
                                      bool elRolExisteEnElEntorno = true, string nombreDelRol = TablasNativas.RolRpa)
        {
            var (ctx, svc) = Armar(identidadDeAplicacion, conElRol, elRolExisteEnElEntorno, nombreDelRol);
            var e = Assert.Throws<InvalidPluginExecutionException>(() => PuertaDelRpa.Exigir(ctx, svc));
            return e.Message;
        }

        [Fact]
        public void El_bot_pasa()
        {
            var (ctx, svc) = Armar(identidadDeAplicacion: true, conElRol: true);
            PuertaDelRpa.Exigir(ctx, svc); // no lanza
        }

        [Fact]
        public void Una_persona_NO_pasa_aunque_le_hayan_asignado_el_rol()
        {
            // El caso que hace innecesaria la tabla marcadora que recomienda Learn: un humano no tiene
            // `applicationid`, nunca. Ni con el rol asignado por error pasa.
            Assert.Contains("MPPP-RPA-006", Rechazo(identidadDeAplicacion: false, conElRol: true));
        }

        [Fact]
        public void Una_persona_sin_el_rol_tampoco()
        {
            Assert.Contains("MPPP-RPA-006", Rechazo(identidadDeAplicacion: false, conElRol: false));
        }

        [Fact]
        public void Otra_identidad_de_aplicacion_sin_el_rol_NO_pasa()
        {
            // Cualquier otro service principal del tenant: tener `applicationid` no alcanza.
            Assert.Contains("MPPP-RPA-006", Rechazo(identidadDeAplicacion: true, conElRol: false));
        }

        [Fact]
        public void Si_el_rol_NO_esta_creado_el_error_lo_dice_y_no_culpa_al_usuario()
        {
            // D-44: sin esto, el bot recibiría un "no autorizado" y quien lo lea saldría a revisar permisos
            // cuando el problema es que nadie creó un rol.
            var mensaje = Rechazo(identidadDeAplicacion: true, conElRol: true, elRolExisteEnElEntorno: false);
            Assert.Contains("MPPP-RPA-007", mensaje);
            Assert.Contains("6.7", mensaje);
        }

        [Fact]
        public void El_rol_se_resuelve_por_nombre_sin_distinguir_caja()
        {
            var (ctx, svc) = Armar(identidadDeAplicacion: true, conElRol: true, nombreDelRol: "sr - mppp - rpa");
            PuertaDelRpa.Exigir(ctx, svc); // no lanza
        }

        [Fact]
        public void Un_rol_con_nombre_PARECIDO_no_sirve()
        {
            // "Renombrarlo apaga el bot" no es una frase de color: es este caso.
            Assert.Contains("MPPP-RPA-007", Rechazo(identidadDeAplicacion: true, conElRol: true,
                                                    nombreDelRol: "SR - MPPP - RPA (viejo)"));
        }

        [Fact]
        public void El_mensaje_de_rechazo_no_filtra_quien_llamo()
        {
            // Un mensaje de autorización que nombra al usuario es un oráculo barato para quien prueba credenciales.
            var mensaje = Rechazo(identidadDeAplicacion: true, conElRol: false);
            Assert.DoesNotContain("Quien llama", mensaje);
        }
    }
}
