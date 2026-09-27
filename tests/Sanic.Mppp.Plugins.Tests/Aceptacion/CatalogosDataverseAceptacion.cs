using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Mppp.Plugins.Datos;
using Sanic.Mppp.Plugins.Dominio;
using Sanic.Mppp.Plugins.Tests.Dobles;
using Sanic.Mppp.Plugins.Validacion;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Aceptacion
{
    /// <summary>
    /// 7.6 Datos, lectores de catálogos: número fijo de consultas, solo lo activo, tipos del dominio, y falla ruidosa ante un
    /// dato que no se puede traducir. Los nombres de columnas son los de diseno/02.
    /// </summary>
    public class CatalogosDataverseAceptacion
    {
        private const int Inactivo = 1;

        private static Entity Entidad(string tabla, int estado = Tablas.Activo, params (string atributo, object valor)[] atributos)
        {
            var e = new Entity(tabla) { ["statecode"] = new OptionSetValue(estado) };
            foreach (var (atributo, valor) in atributos)
            {
                e[atributo] = valor;
            }

            return e;
        }

        private static int Consultas(OrganizationServiceEnMemoria svc) => svc.Llamadas.Count;

        // ------------------------------------------------------------------ parámetros
        [Fact]
        public void El_parametro_es_la_version_activa_mas_alta_en_una_sola_consulta()
        {
            var svc = new OrganizationServiceEnMemoria();
            svc.Sembrar(Entidad(Tablas.Parametro, Tablas.Activo, ("sanic_nombre", "plantilla.listas"), ("sanic_version", 1), ("sanic_valor", "v1")));
            svc.Sembrar(Entidad(Tablas.Parametro, Tablas.Activo, ("sanic_nombre", "plantilla.listas"), ("sanic_version", 3), ("sanic_valor", "v3")));
            svc.Sembrar(Entidad(Tablas.Parametro, Inactivo, ("sanic_nombre", "plantilla.listas"), ("sanic_version", 7), ("sanic_valor", "v7 inactiva")));
            svc.Sembrar(Entidad(Tablas.Parametro, Tablas.Activo, ("sanic_nombre", "plantilla.listas.otra"), ("sanic_version", 9), ("sanic_valor", "otra")));

            var p = new CatalogosDataverse(svc).Parametro("plantilla.listas");

            Assert.Equal(("plantilla.listas", 3, "v3"), (p.Nombre, p.Version, p.Valor));
            Assert.Equal(1, Consultas(svc));
        }

        [Fact]
        public void Un_parametro_sin_version_activa_es_nulo_y_el_nombre_se_compara_como_dataverse_sin_distinguir_mayusculas()
        {
            var svc = new OrganizationServiceEnMemoria();
            svc.Sembrar(Entidad(Tablas.Parametro, Inactivo, ("sanic_nombre", "lectura.limites"), ("sanic_version", 1), ("sanic_valor", "x")));
            var catalogos = new CatalogosDataverse(svc);
            Assert.Null(catalogos.Parametro("lectura.limites"));
            // Re-revisión, 2026-09-21: Dataverse compara texto sin distinguir mayúsculas; el nombre va en minúscula por convención (02 §2.5).
            svc.Sembrar(Entidad(Tablas.Parametro, Tablas.Activo, ("sanic_nombre", "lectura.limites"), ("sanic_version", 2), ("sanic_valor", "y")));
            Assert.Equal("y", catalogos.Parametro("Lectura.Limites").Valor);
            Assert.Null(catalogos.Parametro("lectura.limites.otra"));
            Assert.Throws<ArgumentException>(() => catalogos.Parametro(" "));
            Assert.Throws<ArgumentException>(() => catalogos.Parametro(null));
        }

        [Fact]
        public void Un_parametro_activo_sin_version_o_sin_valor_es_un_error_real_que_nombra_el_registro()
        {
            var svc = new OrganizationServiceEnMemoria();
            var id = svc.Sembrar(Entidad(Tablas.Parametro, Tablas.Activo, ("sanic_nombre", "plantilla.listas"), ("sanic_version", 2)));
            var ex = Assert.Throws<InvalidOperationException>(() => new CatalogosDataverse(svc).Parametro("plantilla.listas"));
            Assert.Contains("sanic_valor", ex.Message);
            Assert.Contains(id.ToString(), ex.Message);
        }

        // ------------------------------------------------------------------ reglas
        private static Entity Regla(string codigo, NivelDeLaRegla nivel, int orden, string dependeDe = null, EfectoDeLaRegla efecto = EfectoDeLaRegla.Rechaza, string mensaje = null, int estado = Tablas.Activo)
        {
            return Entidad(Tablas.Regla, estado, ("sanic_codigo", codigo), ("sanic_nivel", new OptionSetValue((int)nivel)), ("sanic_orden", orden),
                ("sanic_dependede", dependeDe), ("sanic_efecto", new OptionSetValue((int)efecto)), ("sanic_mensajecliente", mensaje));
        }

        [Fact]
        public void Las_reglas_activas_de_un_nivel_salen_como_definiciones_en_una_sola_consulta()
        {
            var svc = new OrganizationServiceEnMemoria();
            svc.Sembrar(Regla("TRAE_ADJUNTO", NivelDeLaRegla.Solicitud, 10, mensaje: "Adjunte la plantilla."));
            svc.Sembrar(Regla("ADJUNTO_ES_EXCEL", NivelDeLaRegla.Solicitud, 20, "TRAE_ADJUNTO"));
            svc.Sembrar(Regla("VIEJA", NivelDeLaRegla.Solicitud, 5, estado: Inactivo));
            svc.Sembrar(Regla("LISTAS_VALIDAS", NivelDeLaRegla.Registro, 10));
            svc.Sembrar(Regla("AVISO", NivelDeLaRegla.Solicitud, 30, " TRAE_ADJUNTO , ADJUNTO_ES_EXCEL,, ", EfectoDeLaRegla.Advierte));

            var reglas = new CatalogosDataverse(svc).ReglasActivas(NivelDeLaRegla.Solicitud);

            Assert.Equal(1, Consultas(svc));
            Assert.Equal(new[] { "ADJUNTO_ES_EXCEL", "AVISO", "TRAE_ADJUNTO" }, reglas.Select(r => r.Codigo).OrderBy(c => c, StringComparer.Ordinal));
            var trae = reglas.Single(r => r.Codigo == "TRAE_ADJUNTO");
            Assert.Equal((10, EfectoDeLaRegla.Rechaza, "Adjunte la plantilla."), (trae.Orden, trae.Efecto, trae.MensajeCliente));
            Assert.Empty(trae.DependeDe);
            Assert.Equal(new[] { "TRAE_ADJUNTO" }, reglas.Single(r => r.Codigo == "ADJUNTO_ES_EXCEL").DependeDe);
            var aviso = reglas.Single(r => r.Codigo == "AVISO");
            Assert.Equal(new[] { "TRAE_ADJUNTO", "ADJUNTO_ES_EXCEL" }, aviso.DependeDe); // recortadas, sin vacíos
            Assert.Equal(EfectoDeLaRegla.Advierte, aviso.Efecto);
            Assert.Null(aviso.MensajeCliente);
            Assert.Empty(new CatalogosDataverse(svc).ReglasActivas(NivelDeLaRegla.Correo));
        }

        [Fact]
        public void Las_reglas_tambien_se_pueden_leer_con_su_id_en_la_misma_consulta()
        {
            var svc = new OrganizationServiceEnMemoria();
            var id = svc.Sembrar(Regla("TRAE_ADJUNTO", NivelDeLaRegla.Solicitud, 10));
            var catalogos = new CatalogosDataverse(svc);

            var conId = catalogos.ReglasActivasConId(NivelDeLaRegla.Solicitud);

            var regla = Assert.Single(conId);
            Assert.Equal(id, regla.Id);
            Assert.Equal("TRAE_ADJUNTO", regla.Definicion.Codigo);
            Assert.Equal(1, Consultas(svc));
            Assert.Equal(conId.Select(r => r.Definicion.Codigo), catalogos.ReglasActivas(NivelDeLaRegla.Solicitud).Select(d => d.Codigo));
        }

        [Theory]
        [InlineData("sanic_codigo")]
        [InlineData("sanic_orden")]
        [InlineData("sanic_efecto")]
        public void Una_regla_activa_con_una_columna_requerida_en_nulo_es_un_error_real(string columna)
        {
            var svc = new OrganizationServiceEnMemoria();
            var regla = Regla("X", NivelDeLaRegla.Registro, 1);
            regla[columna] = null;
            var id = svc.Sembrar(regla);
            var ex = Assert.Throws<InvalidOperationException>(() => new CatalogosDataverse(svc).ReglasActivas(NivelDeLaRegla.Registro));
            Assert.Contains(columna, ex.Message);
            Assert.Contains(id.ToString(), ex.Message);
        }

        [Fact]
        public void Un_efecto_que_no_es_del_choice_es_un_error_real_no_un_efecto_por_defecto()
        {
            var svc = new OrganizationServiceEnMemoria();
            var regla = Regla("X", NivelDeLaRegla.Registro, 1);
            regla["sanic_efecto"] = new OptionSetValue(999);
            svc.Sembrar(regla);
            Assert.Throws<InvalidOperationException>(() => new CatalogosDataverse(svc).ReglasActivas(NivelDeLaRegla.Registro));
        }

        // ------------------------------------------------------------------ planes
        private static Guid Plan(OrganizationServiceEnMemoria svc, string codigo, TipoDeFormatoDelPlan formato, Moneda moneda, Guid clienteId, int estado = Tablas.Activo)
        {
            return svc.Sembrar(Entidad(Tablas.Plan, estado, ("sanic_codigo", codigo), ("sanic_tipoformato", new OptionSetValue((int)formato)),
                ("sanic_moneda", new OptionSetValue((int)moneda)), ("sanic_clienteid", new EntityReference(Tablas.Cliente, clienteId))));
        }

        [Fact]
        public void Los_planes_activos_por_codigo_salen_de_una_sola_consulta_con_la_lista_limpia()
        {
            var svc = new OrganizationServiceEnMemoria();
            var cliente = Guid.NewGuid();
            var id42 = Plan(svc, "0042", TipoDeFormatoDelPlan._11, Moneda.USD, cliente);
            Plan(svc, "0006", TipoDeFormatoDelPlan._06, Moneda.COR, cliente);
            Plan(svc, "00A1", TipoDeFormatoDelPlan._10, Moneda.USD, cliente, Inactivo);

            var planes = new CatalogosDataverse(svc).PlanesActivosPorCodigo(new[] { "0042", "0042", "00A1", " ", null, "9999" });

            Assert.Equal(1, Consultas(svc));
            var plan = Assert.Single(planes);
            Assert.Equal((id42, "0042", TipoDeFormatoDelPlan._11, Moneda.USD), (plan.Id, plan.Codigo, plan.TipoFormato, plan.Moneda));
        }

        [Fact]
        public void Sin_codigos_no_hay_consulta()
        {
            var svc = new OrganizationServiceEnMemoria();
            var catalogos = new CatalogosDataverse(svc);
            Assert.Empty(catalogos.PlanesActivosPorCodigo(new string[0]));
            Assert.Empty(catalogos.PlanesActivosPorCodigo(new[] { " ", null }));
            Assert.Equal(0, Consultas(svc));
            Assert.Throws<ArgumentNullException>(() => catalogos.PlanesActivosPorCodigo(null));
        }

        [Theory]
        [InlineData("sanic_tipoformato")] // un plan sin sanic_codigo no puede calzar con el In: no llega a leerse, ni acá ni en Dataverse
        [InlineData("sanic_moneda")]
        public void Un_plan_activo_con_una_columna_requerida_en_nulo_es_un_error_real(string columna)
        {
            var svc = new OrganizationServiceEnMemoria();
            var e = Entidad(Tablas.Plan, Tablas.Activo, ("sanic_codigo", "0042"), ("sanic_tipoformato", new OptionSetValue((int)TipoDeFormatoDelPlan._11)), ("sanic_moneda", new OptionSetValue((int)Moneda.USD)));
            e[columna] = null;
            var id = svc.Sembrar(e);
            var ex = Assert.Throws<InvalidOperationException>(() => new CatalogosDataverse(svc).PlanesActivosPorCodigo(new[] { "0042" }));
            Assert.Contains(columna, ex.Message);
            Assert.Contains(id.ToString(), ex.Message);
        }

        [Fact]
        public void Un_plan_activo_con_moneda_o_formato_fuera_del_choice_es_un_error_real()
        {
            var svc = new OrganizationServiceEnMemoria();
            var e = Entidad(Tablas.Plan, Tablas.Activo, ("sanic_codigo", "0042"), ("sanic_tipoformato", new OptionSetValue(999)), ("sanic_moneda", new OptionSetValue((int)Moneda.USD)));
            var id = svc.Sembrar(e);
            var ex = Assert.Throws<InvalidOperationException>(() => new CatalogosDataverse(svc).PlanesActivosPorCodigo(new[] { "0042" }));
            Assert.Contains("sanic_tipoformato", ex.Message);
            Assert.Contains(id.ToString(), ex.Message);
        }

        // ------------------------------------------------------------------ autorizaciones (todo activo, D-42)
        private sealed class Mundo
        {
            public OrganizationServiceEnMemoria Svc = new OrganizationServiceEnMemoria();
            public Guid ClienteActivo, ClienteInactivo, PlanA, PlanB, PlanInactivo, PlanDeClienteInactivo, PlanSinAutorizacion;

            public Mundo()
            {
                ClienteActivo = Svc.Sembrar(Entidad(Tablas.Cliente, Tablas.Activo, ("sanic_nombre", "ACME")));
                ClienteInactivo = Svc.Sembrar(Entidad(Tablas.Cliente, Inactivo, ("sanic_nombre", "VIEJA SA")));
                PlanA = Plan(Svc, "000A", TipoDeFormatoDelPlan._11, Moneda.USD, ClienteActivo);
                PlanB = Plan(Svc, "000B", TipoDeFormatoDelPlan._06, Moneda.COR, ClienteActivo);
                PlanInactivo = Plan(Svc, "000C", TipoDeFormatoDelPlan._06, Moneda.COR, ClienteActivo, Inactivo);
                PlanDeClienteInactivo = Plan(Svc, "000D", TipoDeFormatoDelPlan._06, Moneda.COR, ClienteInactivo);
                PlanSinAutorizacion = Plan(Svc, "000E", TipoDeFormatoDelPlan._06, Moneda.COR, ClienteActivo);
            }

            public Guid Autorizado(string correo, Guid cliente, int estado = Tablas.Activo)
            {
                return Svc.Sembrar(Entidad(Tablas.Autorizado, estado, ("sanic_nombre", correo), ("sanic_clienteid", new EntityReference(Tablas.Cliente, cliente))));
            }

            public void Autorizacion(Guid autorizado, Guid plan, int estado = Tablas.Activo, bool conEvidencia = false)
            {
                var e = Entidad(Tablas.AutorizacionPlan, estado, ("sanic_autorizadoid", new EntityReference(Tablas.Autorizado, autorizado)), ("sanic_planid", new EntityReference(Tablas.Plan, plan)));
                if (conEvidencia)
                {
                    e["sanic_documentofirmado"] = Guid.NewGuid().ToString();
                }

                Svc.Sembrar(e);
            }
        }

        [Fact]
        public void Un_plan_esta_autorizado_solo_si_todo_esta_activo_y_la_evidencia_no_cuenta()
        {
            var m = new Mundo();
            var yo = m.Autorizado("ana@acme.com", m.ClienteActivo);
            m.Autorizacion(yo, m.PlanA, conEvidencia: true);
            m.Autorizacion(yo, m.PlanB); // sin evidencia: vale igual (D-42)
            m.Autorizacion(yo, m.PlanInactivo);
            m.Autorizacion(yo, m.PlanDeClienteInactivo);
            var yoInactivo = m.Autorizado("ana@acme.com", m.ClienteInactivo, Inactivo);
            m.Autorizacion(yoInactivo, m.PlanSinAutorizacion);
            var otro = m.Autorizado("otro@acme.com", m.ClienteActivo);
            m.Autorizacion(otro, m.PlanSinAutorizacion);
            m.Autorizacion(yo, m.PlanSinAutorizacion, Inactivo); // autorización inactiva

            var planes = new CatalogosDataverse(m.Svc).PlanesAutorizadosDe("ana@acme.com");

            Assert.Equal(new[] { m.PlanA, m.PlanB }.OrderBy(g => g), planes.OrderBy(g => g));
            // Eran 4 (a lo sumo) hasta el 2026-09-26. Ese día DN-01 consolidó el modo de autorización en un solo
            // punto (esta misma función) y eso agregó una QUINTA consulta, fija: leer el parámetro
            // `autorizacion.modo` (tabla Parametro) para saber a cuál de las dos vías mirar — AutorizacionPlan o
            // AutorizacionCliente. No hay forma de evitarla: sin leer el parámetro no se sabe qué tabla consultar.
            // Medido (no supuesto): con 1, 40 y 500 autorizaciones en modo Plan, y con 1, 20 y 500 clientes
            // autorizados en modo Cliente, siempre dieron 5 consultas — nunca más. Por eso el rango sube a 5, y NO
            // más que eso: lo que esta prueba protege es que el número no CREZCA con los datos (nombre de la
            // prueba de abajo). Si algún día el conteo real supera 5, NO se amplía el rango de nuevo sin medir
            // primero si es un costo fijo nuevo (como este) o un N+1 metido por accidente — en ese segundo caso se
            // arregla la consulta, no la prueba.
            Assert.InRange(Consultas(m.Svc), 1, 5);
        }

        [Fact]
        public void El_correo_se_busca_como_lo_guarda_dataverse_sin_espacios_y_en_minuscula()
        {
            var m = new Mundo();
            var yo = m.Autorizado("ana@acme.com", m.ClienteActivo);
            m.Autorizacion(yo, m.PlanA);
            var catalogos = new CatalogosDataverse(m.Svc);
            Assert.Equal(new[] { m.PlanA }, catalogos.PlanesAutorizadosDe("  Ana@ACME.com  "));
            Assert.Empty(catalogos.PlanesAutorizadosDe("nadie@acme.com"));
        }

        [Fact]
        public void Un_mismo_correo_bajo_dos_empresas_suma_los_planes_de_las_dos()
        {
            var m = new Mundo();
            var otroCliente = m.Svc.Sembrar(Entidad(Tablas.Cliente, Tablas.Activo, ("sanic_nombre", "BETA")));
            var planBeta = Plan(m.Svc, "000F", TipoDeFormatoDelPlan._10, Moneda.USD, otroCliente);
            m.Autorizacion(m.Autorizado("ana@acme.com", m.ClienteActivo), m.PlanA);
            m.Autorizacion(m.Autorizado("ana@acme.com", otroCliente), planBeta);

            var planes = new CatalogosDataverse(m.Svc).PlanesAutorizadosDe("ana@acme.com");
            Assert.Equal(new[] { m.PlanA, planBeta }.OrderBy(g => g), planes.OrderBy(g => g));
        }

        [Fact]
        public void Sin_correo_no_hay_consulta_y_el_conjunto_es_nuevo_cada_vez()
        {
            var m = new Mundo();
            var catalogos = new CatalogosDataverse(m.Svc);
            Assert.Empty(catalogos.PlanesAutorizadosDe(null));
            Assert.Empty(catalogos.PlanesAutorizadosDe("   "));
            Assert.Equal(0, Consultas(m.Svc));

            var yo = m.Autorizado("ana@acme.com", m.ClienteActivo);
            m.Autorizacion(yo, m.PlanA);
            catalogos.PlanesAutorizadosDe("ana@acme.com").Add(Guid.NewGuid());
            Assert.Single(catalogos.PlanesAutorizadosDe("ana@acme.com"));
        }

        [Fact]
        public void El_numero_de_consultas_no_crece_con_la_cantidad_de_autorizaciones()
        {
            var m = new Mundo();
            var yo = m.Autorizado("ana@acme.com", m.ClienteActivo);
            for (var i = 0; i < 40; i++)
            {
                m.Autorizacion(yo, Plan(m.Svc, $"P{i:D3}", TipoDeFormatoDelPlan._06, Moneda.COR, m.ClienteActivo));
            }

            var planes = new CatalogosDataverse(m.Svc).PlanesAutorizadosDe("ana@acme.com");
            Assert.Equal(40, planes.Count);
            // Eran 4 (a lo sumo) hasta el 2026-09-26. Ese día DN-01 consolidó el modo de autorización en un solo
            // punto (esta misma función) y eso agregó una QUINTA consulta, fija: leer el parámetro
            // `autorizacion.modo` (tabla Parametro) para saber a cuál de las dos vías mirar — AutorizacionPlan o
            // AutorizacionCliente. No hay forma de evitarla: sin leer el parámetro no se sabe qué tabla consultar.
            // Medido (no supuesto): con 1, 40 y 500 autorizaciones en modo Plan, y con 1, 20 y 500 clientes
            // autorizados en modo Cliente, siempre dieron 5 consultas — nunca más, así crezca la cantidad de
            // autorizaciones (que es justo lo que el nombre de esta prueba dice que protege). Por eso el rango
            // sube a 5, y NO más que eso. Si el conteo real algún día supera 5, NO se amplía el rango sin medir
            // primero si es un costo fijo nuevo (como este) o un N+1 metido por accidente — en ese segundo caso se
            // arregla la consulta, no la prueba.
            Assert.InRange(Consultas(m.Svc), 1, 5);
        }

        [Fact]
        public void Un_servicio_nulo_es_un_error_de_programacion()
        {
            Assert.Throws<ArgumentNullException>(() => new CatalogosDataverse(null));
        }

        // ------------------------------------------------------------------ DN-01 (wiring, punto 4): el nombre lógico de la tabla nueva
        [Fact]
        public void La_tabla_autorizacioncliente_tiene_su_nombre_logico()
        {
            Assert.Equal("sanic_mppp_tbl_autorizacioncliente", Tablas.AutorizacionCliente);
        }

        // ------------------------------------------------------------------ DN-01 (wiring): PlanesAutorizadosDe segun `autorizacion.modo`
        // Corrección del aprobador, 2026-09-26 (diseno/DIRECTRICES-NUEVAS.md, "El modo cambia TODO, no una parte"):
        // hay UN SOLO punto donde se consulta la autorización — PlanesAutorizadosDe — y el modo se resuelve AHÍ
        // adentro. Por eso estas pruebas NO llaman a ningún método de "resolver modo" ni de "clientes autorizados"
        // por separado (esa era la forma VIEJA, de dos métodos, que el commit eb61127 dejó pendiente de rehacer):
        // solo miran lo que PlanesAutorizadosDe devuelve, para que ningún llamador tenga que enterarse del modo.
        private sealed class MundoModo
        {
            public OrganizationServiceEnMemoria Svc = new OrganizationServiceEnMemoria();
            public Guid ClienteActivo, ClienteInactivo;

            public MundoModo()
            {
                ClienteActivo = Svc.Sembrar(Entidad(Tablas.Cliente, Tablas.Activo, ("sanic_nombre", "ACME")));
                ClienteInactivo = Svc.Sembrar(Entidad(Tablas.Cliente, Inactivo, ("sanic_nombre", "VIEJA SA")));
            }

            public Guid Autorizado(string correo, int estado = Tablas.Activo) =>
                Svc.Sembrar(Entidad(Tablas.Autorizado, estado, ("sanic_nombre", correo)));

            public Guid PlanDe(Guid cliente, string codigo, int estado = Tablas.Activo) =>
                Plan(Svc, codigo, TipoDeFormatoDelPlan._06, Moneda.COR, cliente, estado);

            public void AutorizacionPlan(Guid autorizado, Guid plan, int estado = Tablas.Activo)
            {
                Svc.Sembrar(Entidad(Tablas.AutorizacionPlan, estado,
                    ("sanic_autorizadoid", new EntityReference(Tablas.Autorizado, autorizado)),
                    ("sanic_planid", new EntityReference(Tablas.Plan, plan))));
            }

            public void AutorizacionCliente(Guid autorizado, Guid cliente, int estado = Tablas.Activo)
            {
                Svc.Sembrar(Entidad(Tablas.AutorizacionCliente, estado,
                    ("sanic_autorizadoid", new EntityReference(Tablas.Autorizado, autorizado)),
                    ("sanic_clienteid", new EntityReference(Tablas.Cliente, cliente))));
            }

            public void ParametroModo(string valor)
            {
                Svc.Sembrar(Entidad(Tablas.Parametro, Tablas.Activo, ("sanic_nombre", ModoDeAutorizacionParametro.Nombre), ("sanic_version", 1), ("sanic_valor", valor)));
            }
        }

        [Fact]
        public void En_modo_cliente_planes_autorizados_de_trae_todos_los_planes_del_cliente_sin_enumerarlos()
        {
            var m = new MundoModo();
            m.ParametroModo("cliente");
            var planUno = m.PlanDe(m.ClienteActivo, "000A");
            var planDos = m.PlanDe(m.ClienteActivo, "000B");
            var yo = m.Autorizado("ana@acme.com");
            m.AutorizacionCliente(yo, m.ClienteActivo);

            var planes = new CatalogosDataverse(m.Svc).PlanesAutorizadosDe("ana@acme.com");

            Assert.Equal(new[] { planUno, planDos }.OrderBy(g => g), planes.OrderBy(g => g));
        }

        [Fact]
        public void En_modo_cliente_una_autorizacionplan_propia_no_alcanza_esa_via_queda_muda()
        {
            // DN-01 punto 4: "En modo cliente, las filas de AutorizacionPlan se conservan y no se miran."
            var m = new MundoModo();
            m.ParametroModo("cliente");
            var plan = m.PlanDe(m.ClienteActivo, "000A");
            var yo = m.Autorizado("ana@acme.com");
            m.AutorizacionPlan(yo, plan); // SIN AutorizacionCliente

            var planes = new CatalogosDataverse(m.Svc).PlanesAutorizadosDe("ana@acme.com");

            Assert.Empty(planes);
        }

        [Fact]
        public void En_modo_cliente_solo_cuentan_autorizacioncliente_y_cliente_activos()
        {
            var m = new MundoModo();
            m.ParametroModo("cliente");
            var planActivo = m.PlanDe(m.ClienteActivo, "000A");
            m.PlanDe(m.ClienteInactivo, "000C"); // cliente inactivo: nunca puede aparecer
            var yo = m.Autorizado("ana@acme.com");
            m.AutorizacionCliente(yo, m.ClienteActivo);
            m.AutorizacionCliente(yo, m.ClienteInactivo); // cliente inactivo: no cuenta
            var yoInactivo = m.Autorizado("otro@acme.com", Inactivo);
            m.AutorizacionCliente(yoInactivo, m.ClienteActivo); // autorizado inactivo: no cuenta

            var planes = new CatalogosDataverse(m.Svc).PlanesAutorizadosDe("ana@acme.com");

            Assert.Equal(new[] { planActivo }, planes);
        }

        [Fact]
        public void Con_el_parametro_en_basura_planes_autorizados_de_degrada_a_plan_y_no_mira_autorizacioncliente()
        {
            var m = new MundoModo();
            m.ParametroModo("clienteX"); // basura: degrada al modo conocido (fallback quemado, DN-01)
            var planPorCliente = m.PlanDe(m.ClienteActivo, "000A"); // el correo SOLO tiene AutorizacionCliente sobre este
            var otroCliente = m.Svc.Sembrar(Entidad(Tablas.Cliente, Tablas.Activo, ("sanic_nombre", "BETA")));
            var planPorPlan = m.PlanDe(otroCliente, "000B"); // el correo SOLO tiene AutorizacionPlan sobre este
            var yo = m.Autorizado("ana@acme.com");
            m.AutorizacionCliente(yo, m.ClienteActivo);
            m.AutorizacionPlan(yo, planPorPlan);

            var planes = new CatalogosDataverse(m.Svc).PlanesAutorizadosDe("ana@acme.com");

            Assert.Equal(new[] { planPorPlan }, planes); // solo el de AutorizacionPlan: la vía cliente quedó muda
            Assert.DoesNotContain(planPorCliente, planes);
        }

        [Fact]
        public void En_modo_cliente_el_correo_se_busca_como_lo_guarda_dataverse_sin_espacios_y_en_minuscula()
        {
            var m = new MundoModo();
            m.ParametroModo("cliente");
            var plan = m.PlanDe(m.ClienteActivo, "000A");
            var yo = m.Autorizado("ana@acme.com");
            m.AutorizacionCliente(yo, m.ClienteActivo);
            var catalogos = new CatalogosDataverse(m.Svc);

            Assert.Equal(new[] { plan }, catalogos.PlanesAutorizadosDe("  Ana@ACME.com  "));
            Assert.Empty(catalogos.PlanesAutorizadosDe("nadie@acme.com"));
        }

        [Fact]
        public void En_modo_cliente_sin_correo_no_hay_consulta()
        {
            var m = new MundoModo();
            m.ParametroModo("cliente");
            var catalogos = new CatalogosDataverse(m.Svc);
            var consultasAntes = Consultas(m.Svc);

            Assert.Empty(catalogos.PlanesAutorizadosDe(null));
            Assert.Empty(catalogos.PlanesAutorizadosDe("   "));

            Assert.Equal(consultasAntes, Consultas(m.Svc));
        }

        [Fact]
        public void En_modo_cliente_el_numero_de_consultas_no_crece_con_la_cantidad_de_planes()
        {
            var m = new MundoModo();
            m.ParametroModo("cliente");
            var yo = m.Autorizado("ana@acme.com");
            m.AutorizacionCliente(yo, m.ClienteActivo);
            for (var i = 0; i < 20; i++)
            {
                m.PlanDe(m.ClienteActivo, $"P{i:D3}");
            }

            var planes = new CatalogosDataverse(m.Svc).PlanesAutorizadosDe("ana@acme.com");

            Assert.Equal(20, planes.Count);
            Assert.InRange(Consultas(m.Svc), 1, 6);
        }

        // ------------------------------------------------------------------ DN-01 (wiring): PlanesActivosPorCodigo trae el cliente dueño
        [Fact]
        public void Los_planes_activos_traen_el_cliente_dueno_del_plan_para_el_modo_cliente_de_dn01()
        {
            var svc = new OrganizationServiceEnMemoria();
            var cliente = Guid.NewGuid();
            Plan(svc, "0042", TipoDeFormatoDelPlan._11, Moneda.USD, cliente);

            var plan = Assert.Single(new CatalogosDataverse(svc).PlanesActivosPorCodigo(new[] { "0042" }));

            Assert.Equal(cliente, plan.ClienteId);
        }

        [Fact]
        public void Un_plan_activo_sin_cliente_dueno_se_carga_sin_problema_con_clienteid_nulo()
        {
            // Corrección del aprobador, 2026-09-26: esta prueba primero pedía InvalidOperationException acá.
            // Estaba mal — PlanesActivosPorCodigo la usan LOS DOS MODOS de DN-01, y en modo Plan el dueño del plan
            // NO SE MIRA para nada (es el comportamiento de hoy, el que está activo en TODOS los entornos). Exigir
            // `sanic_clienteid` acá rompería el modo Plan para proteger un modo (Cliente) que todavía nadie usa:
            // exactamente al revés de "un dato de configuración incompleto degrada el control, nunca voltea la
            // operación" (mismo criterio que `servicio.cuenta`, `rpa.horas.tolerancia`, `rpa.minutos.claimmaximo` y
            // `autorizacion.modo`). Por eso un plan sin dueño se carga SIN PROBLEMA, con ClienteId nulo; ver
            // `En_modo_cliente_un_plan_sin_cliente_dueno_conocido_nunca_aparece_aunque_el_correo_este_autorizado`
            // para el otro lado: en modo Cliente eso significa fallar cerrado (nunca autoriza).
            var svc = new OrganizationServiceEnMemoria();
            var e = Entidad(Tablas.Plan, Tablas.Activo, ("sanic_codigo", "0042"), ("sanic_tipoformato", new OptionSetValue((int)TipoDeFormatoDelPlan._11)), ("sanic_moneda", new OptionSetValue((int)Moneda.USD)));
            svc.Sembrar(e);

            var plan = Assert.Single(new CatalogosDataverse(svc).PlanesActivosPorCodigo(new[] { "0042" }));

            Assert.Null(plan.ClienteId);
        }

        [Fact]
        public void En_modo_cliente_un_plan_sin_cliente_dueno_conocido_nunca_aparece_aunque_el_correo_este_autorizado()
        {
            // El otro lado de la prueba de arriba: un plan sin dueño conocido se carga sin problema, pero en modo
            // Cliente eso significa fallar cerrado — nunca se inventa un dueño, así que nunca queda autorizado,
            // aunque el correo tenga autorización activa sobre el ÚNICO cliente que existe en el mundo.
            var m = new MundoModo();
            m.ParametroModo("cliente");
            var planConDueno = m.PlanDe(m.ClienteActivo, "000A");
            var planSinDueno = m.Svc.Sembrar(Entidad(Tablas.Plan, Tablas.Activo, ("sanic_codigo", "000B"), ("sanic_tipoformato", new OptionSetValue((int)TipoDeFormatoDelPlan._06)), ("sanic_moneda", new OptionSetValue((int)Moneda.COR))));
            var yo = m.Autorizado("ana@acme.com");
            m.AutorizacionCliente(yo, m.ClienteActivo);

            var planes = new CatalogosDataverse(m.Svc).PlanesAutorizadosDe("ana@acme.com");

            Assert.Equal(new[] { planConDueno }, planes);
            Assert.DoesNotContain(planSinDueno, planes);
        }
    }
}
