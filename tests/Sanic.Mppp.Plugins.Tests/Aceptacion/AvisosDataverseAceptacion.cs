using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Mppp.Plugins.Datos;
using Sanic.Mppp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Aceptacion
{
    /// <summary>7.6b: los avisos dentro de la app (diseno/05 DA-08). Que nadie tenga el rol no puede tumbar la clasificación de un correo.</summary>
    public class AvisosDataverseAceptacion
    {
        private static readonly Guid Solicitud = Guid.NewGuid();

        private sealed class Mundo
        {
            public readonly OrganizationServiceEnMemoria Svc = new OrganizationServiceEnMemoria();
            public Guid Rol;

            public Mundo(bool conRol = true)
            {
                if (conRol)
                {
                    Rol = Svc.Sembrar(new Entity(TablasNativas.Rol) { ["name"] = TablasNativas.RolEjecutivo });
                }

                Svc.Sembrar(new Entity(TablasNativas.Rol) { ["name"] = "sr_mppp_supervisor" });
            }

            public Guid Usuario(string nombre, bool deshabilitado = false, bool conRol = true, bool otroRol = false)
            {
                var id = Svc.Sembrar(new Entity(TablasNativas.Usuario) { ["fullname"] = nombre, ["isdisabled"] = deshabilitado });
                if (conRol)
                {
                    Asignar(id, Rol);
                }

                if (otroRol)
                {
                    Asignar(id, Svc.Registros(TablasNativas.Rol).Single(r => (string)r["name"] == "sr_mppp_supervisor").Id);
                }

                return id;
            }

            public void Asignar(Guid usuario, Guid rol)
            {
                // `systemuserroles` es una intersect entity: en Dataverse REAL sus columnas son `Uniqueidentifier`
                // crudos, no lookups (confirmado en el `plugintracelog` de Dev, D-59; mismo hallazgo que
                // `StepsDeFilaAceptacion.cs` para `roleid`). Sembrar acá un `EntityReference` es lo que escondió
                // D-59 durante toda la construcción: el doble modelaba algo distinto de la plataforma.
                Svc.Sembrar(new Entity(TablasNativas.UsuarioRol) { ["systemuserid"] = usuario, ["roleid"] = rol });
            }

            public void Avisar(string titulo = "Correo sin procesar: solicitud MPPP-00000123", string cuerpo = "Motivo: es una respuesta.")
            {
                new AvisosDataverse(Svc).AvisarAEjecutivos(Solicitud, titulo, cuerpo);
            }
        }

        /// <summary>Un mundo aparte para el rol Supervisor: el de <see cref="Mundo"/> siembra un rol Ejecutivo y un rol
        /// "sr_mppp_supervisor" a propósito MAL escrito (para representar "otro rol, cualquiera"), así que no sirve
        /// para probar el rol Supervisor de verdad.</summary>
        private sealed class MundoDeSupervisores
        {
            public readonly OrganizationServiceEnMemoria Svc = new OrganizationServiceEnMemoria();
            public Guid Rol;

            public MundoDeSupervisores(bool conRol = true)
            {
                if (conRol)
                {
                    Rol = Svc.Sembrar(new Entity(TablasNativas.Rol) { ["name"] = TablasNativas.RolSupervisor });
                }

                Svc.Sembrar(new Entity(TablasNativas.Rol) { ["name"] = TablasNativas.RolEjecutivo });
            }

            public Guid Usuario(string nombre, bool deshabilitado = false, bool conRol = true)
            {
                var id = Svc.Sembrar(new Entity(TablasNativas.Usuario) { ["fullname"] = nombre, ["isdisabled"] = deshabilitado });
                if (conRol)
                {
                    Svc.Sembrar(new Entity(TablasNativas.UsuarioRol) { ["systemuserid"] = id, ["roleid"] = Rol });
                }

                return id;
            }

            public void Avisar(Guid solicitudId, string titulo = "Tenés filas por aprobar: solicitud MPPP-00000123", string cuerpo = "La solicitud MPPP-00000123 tiene filas por aprobar.")
            {
                new AvisosDataverse(Svc).AvisarFilasPorAprobar(solicitudId, titulo, cuerpo);
            }
        }

        /// <summary>Un mundo para el aviso al ejecutivo DUEÑO del cliente (D-64): distinto de los dos de arriba porque el
        /// destinatario no sale de un rol, sale del `ownerid` de la tabla Cliente.</summary>
        private sealed class MundoDelCliente
        {
            public readonly OrganizationServiceEnMemoria Svc = new OrganizationServiceEnMemoria();

            public Guid Usuario(string nombre, bool deshabilitado = false)
            {
                return Svc.Sembrar(new Entity(TablasNativas.Usuario) { ["fullname"] = nombre, ["isdisabled"] = deshabilitado });
            }

            public Guid Cliente(Guid? ownerId, string nombre = "ACME")
            {
                var e = new Entity(Tablas.Cliente) { ["sanic_nombre"] = nombre };
                if (ownerId.HasValue)
                {
                    e["ownerid"] = new EntityReference(TablasNativas.Usuario, ownerId.Value);
                }

                return Svc.Sembrar(e);
            }

            public void Avisar(Guid clienteId, string titulo = "Tenés filas por digitar: solicitud MPPP-00000123", string cuerpo = "La solicitud MPPP-00000123 tiene filas por digitar.")
            {
                new AvisosDataverse(Svc).AvisarFilasPorDigitar(Solicitud, clienteId, titulo, cuerpo);
            }
        }

        [Fact]
        public void Un_aviso_por_cada_ejecutivo_habilitado_con_numero_fijo_de_consultas()
        {
            var m = new Mundo();
            var ana = m.Usuario("Ana");
            var beto = m.Usuario("Beto", otroRol: true);
            m.Usuario("Carla", deshabilitado: true); // deshabilitada: no recibe
            m.Usuario("Dario", conRol: false, otroRol: true); // supervisor, no ejecutivo
            m.Asignar(ana, m.Rol); // el rol asignado dos veces: un solo aviso

            m.Avisar();

            var avisos = m.Svc.Registros(TablasNativas.Notificacion);
            Assert.Equal(2, avisos.Count);
            Assert.Equal(new[] { ana, beto }.OrderBy(g => g), avisos.Select(a => a.GetAttributeValue<EntityReference>("ownerid").Id).OrderBy(g => g));
            Assert.All(avisos, a => Assert.Equal(TablasNativas.Usuario, a.GetAttributeValue<EntityReference>("ownerid").LogicalName));
            Assert.All(avisos, a => Assert.Equal("Correo sin procesar: solicitud MPPP-00000123", a["title"]));
            Assert.All(avisos, a => Assert.Equal("Motivo: es una respuesta.", a["body"]));
            var consultas = m.Svc.Llamadas.Count(l => l.Operacion == "RetrieveMultiple");
            Assert.InRange(consultas, 1, 5); // DN-08: rol, directas, equipos con el rol, miembros, habilitados
        }

        [Theory]
        [InlineData(true)] // no existe el rol
        [InlineData(false)] // existe pero nadie lo tiene
        public void Sin_destinatarios_no_se_crea_nada_y_no_se_lanza(bool sinRol)
        {
            var m = new Mundo(conRol: !sinRol);
            if (!sinRol)
            {
                m.Usuario("Carla", deshabilitado: true);
            }

            m.Avisar();

            Assert.Empty(m.Svc.Registros(TablasNativas.Notificacion));
        }

        [Fact]
        public void Los_textos_se_recortan_al_largo_de_su_columna()
        {
            var m = new Mundo();
            m.Usuario("Ana");
            m.Avisar(new string('t', 1000), new string('c', 5000));
            var aviso = m.Svc.Registros(TablasNativas.Notificacion).Single();

            // Los largos REALES de `appnotification` (Microsoft Learn), escritos acá como literales a propósito: comparar el
            // recorte contra la misma constante que se está probando no puede detectar que la constante esté mal (lo estuvo:
            // `body` decía 2000 y son 500; revisión de código, 2026-09-21).
            Assert.Equal(256, ((string)aviso["title"]).Length);
            Assert.Equal(500, ((string)aviso["body"]).Length);
            Assert.Equal((256, 500), (TablasNativas.LargoTitulo, TablasNativas.LargoCuerpo));
        }

        [Fact]
        public void Un_aviso_sin_texto_o_sin_solicitud_es_un_error_de_programacion()
        {
            var m = new Mundo();
            m.Usuario("Ana");
            var avisos = new AvisosDataverse(m.Svc);
            Assert.Throws<ArgumentException>(() => avisos.AvisarAEjecutivos(Solicitud, " ", "cuerpo"));
            Assert.Throws<ArgumentException>(() => avisos.AvisarAEjecutivos(Solicitud, "titulo", null));
            Assert.Throws<ArgumentException>(() => avisos.AvisarAEjecutivos(Guid.Empty, "titulo", "cuerpo"));
            Assert.Empty(m.Svc.Llamadas);
            Assert.Throws<ArgumentNullException>(() => new AvisosDataverse(null));
        }

        // ------------------------------------------------------------------ D-64: "Tenés filas por aprobar" (a los supervisores)
        [Fact]
        public void Filas_por_aprobar_avisa_a_cada_supervisor_habilitado()
        {
            var m = new MundoDeSupervisores();
            var sole = m.Usuario("Sole");
            var beto = m.Usuario("Beto");
            m.Usuario("Carla", deshabilitado: true); // deshabilitada: no recibe
            m.Usuario("Dario", conRol: false); // sin el rol: no recibe

            m.Avisar(Solicitud);

            var avisos = m.Svc.Registros(TablasNativas.Notificacion);
            Assert.Equal(2, avisos.Count);
            Assert.Equal(new[] { sole, beto }.OrderBy(g => g), avisos.Select(a => a.GetAttributeValue<EntityReference>("ownerid").Id).OrderBy(g => g));
            Assert.All(avisos, a => Assert.Equal("Tenés filas por aprobar: solicitud MPPP-00000123", a["title"]));
        }

        [Fact]
        public void Filas_por_aprobar_sin_ningun_supervisor_no_crea_nada_y_no_lanza()
        {
            var m = new MundoDeSupervisores(conRol: false);
            m.Avisar(Solicitud);
            Assert.Empty(m.Svc.Registros(TablasNativas.Notificacion));
        }

        // ------------------------------------------------------------------ D-64: "Tenés filas por digitar" (al ejecutivo dueño del cliente)
        [Fact]
        public void Filas_por_digitar_avisa_solo_al_dueno_del_cliente()
        {
            var m = new MundoDelCliente();
            var ejecutivo = m.Usuario("Eje Cutivo");
            m.Usuario("Otro Ejecutivo"); // no es dueño de este cliente: no recibe nada
            var clienteId = m.Cliente(ejecutivo);

            m.Avisar(clienteId);

            var aviso = Assert.Single(m.Svc.Registros(TablasNativas.Notificacion));
            Assert.Equal(ejecutivo, aviso.GetAttributeValue<EntityReference>("ownerid").Id);
            Assert.Equal(TablasNativas.Usuario, aviso.GetAttributeValue<EntityReference>("ownerid").LogicalName);
            Assert.Equal("Tenés filas por digitar: solicitud MPPP-00000123", aviso["title"]);
        }

        [Fact]
        public void Filas_por_digitar_con_el_dueno_deshabilitado_no_crea_nada_y_no_lanza()
        {
            var m = new MundoDelCliente();
            var ejecutivo = m.Usuario("Eje Cutivo", deshabilitado: true);
            var clienteId = m.Cliente(ejecutivo);

            m.Avisar(clienteId);

            Assert.Empty(m.Svc.Registros(TablasNativas.Notificacion));
        }

        [Fact]
        public void Filas_por_digitar_de_un_cliente_sin_dueno_no_crea_nada_y_no_lanza()
        {
            var m = new MundoDelCliente();
            var clienteId = m.Cliente(ownerId: null);

            m.Avisar(clienteId);

            Assert.Empty(m.Svc.Registros(TablasNativas.Notificacion));
        }

        [Fact]
        public void Filas_por_digitar_o_por_aprobar_sin_argumentos_validos_es_un_error_de_programacion()
        {
            var m = new MundoDelCliente();
            var avisos = new AvisosDataverse(m.Svc);
            Assert.Throws<ArgumentException>(() => avisos.AvisarFilasPorDigitar(Guid.Empty, Guid.NewGuid(), "titulo", "cuerpo"));
            Assert.Throws<ArgumentException>(() => avisos.AvisarFilasPorDigitar(Solicitud, Guid.Empty, "titulo", "cuerpo"));
            Assert.Throws<ArgumentException>(() => avisos.AvisarFilasPorDigitar(Solicitud, Guid.NewGuid(), " ", "cuerpo"));
            Assert.Throws<ArgumentException>(() => avisos.AvisarFilasPorDigitar(Solicitud, Guid.NewGuid(), "titulo", null));
            Assert.Throws<ArgumentException>(() => avisos.AvisarFilasPorAprobar(Guid.Empty, "titulo", "cuerpo"));
            Assert.Throws<ArgumentException>(() => avisos.AvisarFilasPorAprobar(Solicitud, " ", "cuerpo"));
            Assert.Throws<ArgumentException>(() => avisos.AvisarFilasPorAprobar(Solicitud, "titulo", null));
            Assert.Empty(m.Svc.Llamadas);
        }
    }
}
