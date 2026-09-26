using System;
using System.Collections.Generic;
using System.Linq;
using Sanic.Mppp.Plugins.Dominio;
using Sanic.Mppp.Plugins.Plantilla;
using Sanic.Mppp.Plugins.Validacion;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Aceptacion
{
    /// <summary>
    /// DN-01 (diseno/DIRECTRICES-NUEVAS.md, aprobador 2026-09-26): `AUTORIZACION_CORREO_PLAN` pasa a tener DOS vías —
    /// `AutorizacionPlan` (la de hoy) y `AutorizacionCliente` (nueva) — elegidas por un parámetro con FALLBACK QUEMADO
    /// a Plan. El código de la regla, su orden, su nivel y su efecto NO cambian (eso ya lo fija
    /// <see cref="ReglasDeRegistroAceptacion"/>): acá se prueba (1) el fallback del parámetro `autorizacion.modo`,
    /// (2) a DÓNDE mira <see cref="CatalogosDeValidacion.EstaAutorizado"/> según el modo, y (3) lo NO NEGOCIABLE: el
    /// mensaje al cliente es EXACTAMENTE el mismo en los dos modos.
    /// </summary>
    public class AutorizacionPorClienteAceptacion
    {
        private static readonly Guid Cliente1 = Guid.Parse("00000000-0000-0000-0000-0000000000c1");
        private static readonly Guid Cliente2 = Guid.Parse("00000000-0000-0000-0000-0000000000c2");
        private static readonly Guid PlanId = Guid.Parse("00000000-0000-0000-0000-000000000042");

        // Texto unificado del 2026-09-24 (diseno/03-contratos-custom-api.md §... y DIRECTRICES-NUEVAS.md DN-01 punto 5),
        // el mismo que trae hoy `sanic_mensajecliente` de PLAN_EXISTE y de AUTORIZACION_CORREO_PLAN (playbooks/datos/reglas.md).
        private const string PlantillaUnificada = "El plan {plan} no existe o usted no está autorizado sobre él.";

        private static readonly Dictionary<string, string> Columnas = CamposDeFila.Todos()
            .Select((campo, i) => (campo, columna: ((char)('C' + i)).ToString()))
            .ToDictionary(x => x.campo, x => x.columna);

        private static ConfiguracionPlantilla Estructura()
        {
            var campos = CamposDeFila.Todos().Select(c => new CampoPlantilla { Nombre = c, Columna = Columnas[c], EncabezadoEsperado = c }).ToList();
            return new ConfiguracionPlantilla { Hoja = "Datos", FilaEncabezado = 12, PrimeraFila = 13, CantidadFilas = 100, Campos = campos };
        }

        private const string Listas = @"{
            ""gestion"":[{""valor"":""Inclusion""}],
            ""clasificacion"":[{""valor"":""ACH""}],
            ""tipoIdentificacion"":[{""valor"":""CNA""}],
            ""moneda"":[{""valor"":""USD""}],
            ""banco"":[{""valor"":""BAC"",""codigo"":""102""}]
        }";

        private static PlanDelCatalogo Plan(Guid? clienteId) =>
            new PlanDelCatalogo(PlanId, "0042", TipoDeFormatoDelPlan._06, Moneda.USD, clienteId);

        private static CatalogosDeValidacion Catalogos(
            ModoDeAutorizacion modo,
            IEnumerable<Guid> planesAutorizados = null,
            IEnumerable<Guid> clientesAutorizados = null,
            Guid? clienteDelPlan = null)
        {
            return new CatalogosDeValidacion(
                Estructura(),
                ListasPlantilla.DesdeJson(Listas),
                ObligatoriedadPlantilla.DesdeJson(ParametrosDePlantillaAceptacion.ObligatoriedadInicial),
                new[] { Plan(clienteDelPlan) },
                planesAutorizados ?? new Guid[0],
                modo,
                clientesAutorizados);
        }

        private static FilaEnValidacion Fila(CatalogosDeValidacion catalogos)
        {
            var valores = new Dictionary<string, string>
            {
                [Columnas["gestion"]] = "Inclusion",
                [Columnas["clasificacion"]] = "ACH",
                [Columnas["tipoIdentificacion"]] = "CNA",
                [Columnas["moneda"]] = "USD",
                [Columnas["banco"]] = "BAC",
                [Columnas["numeroPlan"]] = "42",
                [Columnas["nombreBeneficiario"]] = "Juan Perez",
                [Columnas["numeroIdentificacion"]] = "0011234567890A",
                [Columnas["numeroCuenta"]] = "123456789",
            };
            return new FilaEnValidacion(new FilaPlantilla { NumeroFilaExcel = 15, NumeroOrden = 1, Valores = valores }, catalogos);
        }

        // ------------------------------------------------------------------ autorizacion.modo: fallback quemado (DN-01)
        [Theory]
        [InlineData(null, ModoDeAutorizacion.Plan)]
        [InlineData("", ModoDeAutorizacion.Plan)]
        [InlineData("   ", ModoDeAutorizacion.Plan)]
        [InlineData("plan", ModoDeAutorizacion.Plan)]
        [InlineData("PLAN", ModoDeAutorizacion.Plan)]
        [InlineData("cliente", ModoDeAutorizacion.Cliente)]
        [InlineData("Cliente", ModoDeAutorizacion.Cliente)]
        [InlineData("  CLIENTE  ", ModoDeAutorizacion.Cliente)]
        [InlineData("clienteX", ModoDeAutorizacion.Plan)] // basura: degrada, nunca tumba la validación
        [InlineData("por-cliente", ModoDeAutorizacion.Plan)]
        [InlineData("0", ModoDeAutorizacion.Plan)]
        public void El_parametro_autorizacion_modo_degrada_a_plan_si_falta_o_trae_basura(string valorCrudo, ModoDeAutorizacion esperado)
        {
            Assert.Equal(esperado, ModoDeAutorizacionParametro.Resolver(valorCrudo));
        }

        [Fact]
        public void El_nombre_del_parametro_es_autorizacion_punto_modo()
        {
            Assert.Equal("autorizacion.modo", ModoDeAutorizacionParametro.Nombre);
        }

        // ------------------------------------------------------------------ EstaAutorizado: a dónde mira según el modo
        [Fact]
        public void En_modo_cliente_autoriza_por_el_cliente_dueno_del_plan_sin_mirar_autorizacionplan()
        {
            // El correo NO tiene el plan en AutorizacionPlan, pero SÍ tiene a Cliente1 en AutorizacionCliente, y el
            // plan es de Cliente1: en modo cliente tiene que autorizar.
            var catalogos = Catalogos(ModoDeAutorizacion.Cliente, planesAutorizados: new Guid[0], clientesAutorizados: new[] { Cliente1 }, clienteDelPlan: Cliente1);
            Assert.True(catalogos.EstaAutorizado(PlanId));
        }

        [Fact]
        public void En_modo_cliente_una_autorizacion_por_plan_no_alcanza_esa_via_se_conserva_pero_no_se_mira()
        {
            // DN-01: "En modo cliente, las filas de AutorizacionPlan SE CONSERVAN y NO SE MIRAN."
            var catalogos = Catalogos(ModoDeAutorizacion.Cliente, planesAutorizados: new[] { PlanId }, clientesAutorizados: new Guid[0], clienteDelPlan: Cliente1);
            Assert.False(catalogos.EstaAutorizado(PlanId));
        }

        [Fact]
        public void En_modo_cliente_estar_autorizado_sobre_otro_cliente_no_autoriza_este_plan()
        {
            var catalogos = Catalogos(ModoDeAutorizacion.Cliente, clientesAutorizados: new[] { Cliente2 }, clienteDelPlan: Cliente1);
            Assert.False(catalogos.EstaAutorizado(PlanId));
        }

        [Fact]
        public void En_modo_cliente_un_plan_sin_cliente_dueno_conocido_nunca_se_da_por_autorizado()
        {
            // Nunca se inventa un dueño: mismo criterio de "nunca se adivina" que el resto del catálogo.
            var catalogos = Catalogos(ModoDeAutorizacion.Cliente, clientesAutorizados: new[] { Cliente1 }, clienteDelPlan: null);
            Assert.False(catalogos.EstaAutorizado(PlanId));
        }

        [Fact]
        public void En_modo_cliente_sin_clientes_autorizados_del_remitente_nunca_lanza_y_nunca_autoriza()
        {
            // Ausente (null) se trata como vacío: degrada sin excepción, mismo criterio que el resto de D-18/D-42.
            var catalogos = Catalogos(ModoDeAutorizacion.Cliente, clientesAutorizados: null, clienteDelPlan: Cliente1);
            Assert.False(catalogos.EstaAutorizado(PlanId));
        }

        [Fact]
        public void En_modo_plan_la_via_cliente_no_se_mira_aunque_el_cliente_dueno_este_autorizado()
        {
            var catalogos = Catalogos(ModoDeAutorizacion.Plan, planesAutorizados: new Guid[0], clientesAutorizados: new[] { Cliente1 }, clienteDelPlan: Cliente1);
            Assert.False(catalogos.EstaAutorizado(PlanId));
        }

        [Fact]
        public void El_modo_por_defecto_del_constructor_es_plan_el_comportamiento_de_hoy()
        {
            // Sin pasar modo ni clientesAutorizados: se comporta exactamente como antes de DN-01 (backward compatible
            // con los 5 argumentos posicionales que ya usan ValidarSolicitud.cs y ReglasDeRegistroAceptacion.cs).
            var catalogos = new CatalogosDeValidacion(
                Estructura(),
                ListasPlantilla.DesdeJson(Listas),
                ObligatoriedadPlantilla.DesdeJson(ParametrosDePlantillaAceptacion.ObligatoriedadInicial),
                new[] { Plan(Cliente1) },
                new[] { PlanId });
            Assert.True(catalogos.EstaAutorizado(PlanId));
        }

        // ------------------------------------------------------------------ el mensaje al cliente: NO NEGOCIABLE
        [Fact]
        public void El_mensaje_al_cliente_es_exactamente_el_mismo_en_modo_plan_y_en_modo_cliente()
        {
            // El 2026-09-24 se unificó a propósito el mensaje de PLAN_EXISTE y AUTORIZACION_CORREO_PLAN para cerrar
            // una fuga de información: un texto que distinga "el plan no existe" de "no estás autorizado sobre él"
            // es un ORÁCULO que le dice a un atacante qué códigos de plan existen (100 filas por correo = 100
            // sondeos, y los códigos siguen convenciones, así que adivinarlos es barato). DN-01 punto 5 (no
            // negociable) extiende esa misma razón a los DOS MODOS de esta directriz: si "no autorizado en modo
            // cliente" y "no autorizado en modo plan" se redactaran distinto, la diferencia filtraría información
            // (qué modo está activo, o cómo se agrupan los planes por cliente). Por eso el texto tiene que ser
            // BYTE A BYTE igual entre los dos modos, no solo "parecido".
            string MensajeDe(ModoDeAutorizacion modo, IEnumerable<Guid> planesAutorizados, IEnumerable<Guid> clientesAutorizados, Guid? clienteDelPlan)
            {
                var catalogos = Catalogos(modo, planesAutorizados, clientesAutorizados, clienteDelPlan);
                var fila = Fila(catalogos);
                var veredicto = ReglasDeRegistro.Evaluadores().Single(e => e.Codigo == ReglasDeRegistro.AutorizacionCorreoPlan).Evaluar(fila);
                Assert.False(veredicto.Cumple);
                return MensajeAlCliente.Componer(PlantillaUnificada, veredicto, ReglasDeRegistro.AutorizacionCorreoPlan);
            }

            var mensajeModoPlan = MensajeDe(ModoDeAutorizacion.Plan, planesAutorizados: new Guid[0], clientesAutorizados: null, clienteDelPlan: Cliente1);
            var mensajeModoCliente = MensajeDe(ModoDeAutorizacion.Cliente, planesAutorizados: null, clientesAutorizados: new Guid[0], clienteDelPlan: Cliente1);

            Assert.Equal("El plan 0042 no existe o usted no está autorizado sobre él.", mensajeModoPlan);
            Assert.Equal(mensajeModoPlan, mensajeModoCliente); // EL PUNTO CENTRAL: byte a byte igual entre los dos modos
        }
    }
}
