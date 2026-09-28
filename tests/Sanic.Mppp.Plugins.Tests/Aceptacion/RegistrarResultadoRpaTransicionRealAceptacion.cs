using System;
using System.Globalization;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Mppp.Plugins.Api;
using Sanic.Mppp.Plugins.Datos;
using Sanic.Mppp.Plugins.Dominio;
using Sanic.Mppp.Plugins.Steps;
using Sanic.Mppp.Plugins.Tests.Apoyo;
using Sanic.Mppp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Aceptacion
{
    /// <summary>
    /// D-60 (2026-09-28): `registrarresultadorpa` con `resultado: "rechazada"` revienta SIEMPRE en Dev, incluso con
    /// un solo ítem válido, con el mensaje genérico de <see cref="RegistrarResultadoRpaApi.MensajeDeErrorGenerico"/>.
    ///
    /// **Por qué `RegistrarResultadoRpaAceptacion` (y las demás suites de esta pieza) no lo vieron nunca.**
    /// `OrganizationServiceEnMemoria` es un almacén puro: un `Update` ahí NUNCA dispara ningún plugin registrado.
    /// La plataforma real, en cambio, aplica UN SEGUNDO control sobre el mismo `Update` de Fila —
    /// <see cref="TransicionDeFilaStep"/> (diseno/03 §4), que vuelve a evaluar <see cref="TransicionesDeFila"/> con
    /// los roles reales del actor — y ese segundo control es el que rechaza la transición. Esto es la MISMA
    /// categoría de lección que ya dejaron D-59 y el propio doble sobre los `In` sin aplanar: "un doble más
    /// permisivo que la plataforma es peor que no tener doble: da confianza falsa". El fixture de esta clase
    /// (<see cref="ServicioConTransicionDeFilaReal"/>) ENVUELVE el doble para que un `Update` de Fila corra los dos
    /// plugins, uno arriba del otro, tal como corre Dataverse — recién ahí un rojo sobre `rechazada` significa algo.
    ///
    /// **La causa real, confirmada leyendo el `plugintracelog` de Dev (no `exceptiondetails`, que viene genericizado
    /// a propósito por la corrección de seguridad del proyecto — mismo criterio que destrabó D-59):**
    ///
    ///   RegistrarResultadoRpa: empieza.
    ///   System.ServiceModel.FaultException`1[Microsoft.Xrm.Sdk.OrganizationServiceFault]: El rol de quien llama no
    ///   puede hacer esta transición. (...)
    ///
    /// `PlomeriaDeFila.ResolverIdentidadDelActor` (`Steps/StepsDeFila.cs`), para código de servidor ANIDADO
    /// (`esCodigoDeServidor &amp;&amp; Depth &gt; 1`) con `hacia` distinto de Aprobada y de Digitada — el único caso real
    /// hoy es Rechazada en AS400 — resolvía la identidad del actor leyendo `sanic_digitadapor` de la PRE-IMAGE. Esa
    /// columna es NULL cuando la fila nunca pasó por Digitada (el camino más común: el bot intenta directo desde
    /// Validada y AS400 la rechaza), así que el actor resolvía a `RolDeActor.Ninguno` y la transición SIEMPRE se
    /// rechazaba — incluso viniendo del bot, ya autenticado y con el rol RPA verificado por
    /// <see cref="PuertaDelRpa"/> en su propio nivel. No hay ninguna columna `sanic_rechazadopor` que guarde una
    /// identidad real para este caso, y el spike C-05 parte B ya probó que ni `ParentContext` ni `SharedVariables`
    /// sirven para preguntárselo a la plataforma (`spikes/c05-parseo-excel/CONCLUSIONES-B.md`). El arreglo confía el
    /// rol directamente en RPA para esta combinación puntual (el mismo criterio "a SYSTEM se le cree" que ya rige
    /// todo este step, diseno/03 §4) y falla cerrado para cualquier otra combinación no anticipada.
    ///
    /// **Efecto colateral que esta misma suite fija (B-17, "nunca todo o nada"):** antes del arreglo, la excepción
    /// REAL de plataforma que tira `TransicionDeFilaStep` escapaba sin atrapar de `RegistrarResultadoRpa.Ejecutar`
    /// (el contrato prohíbe a propósito un `catch` genérico alrededor de una llamada a `OrganizationService`, ver
    /// el comentario grande en `RegistrarResultadoRpa.ProcesarItem`) y tumbaba LA LLAMADA ENTERA — perdiendo
    /// también cualquier ítem bueno (`digitada`) ya procesado en el mismo lote. Arreglado D-60, ese lote mixto
    /// tiene que aplicar los dos.
    /// </summary>
    public class RegistrarResultadoRpaTransicionRealAceptacion
    {
        private static readonly DateTime Ahora = new DateTime(2026, 10, 5, 15, 0, 0, DateTimeKind.Utc);
        private static readonly Guid ElBot = Guid.NewGuid();

        private const string Codigos =
            "{\"OK\":{\"cliente\":null,\"nota\":\"AS400 acepto la operacion\"}," +
            "\"RECH-GEN\":{\"cliente\":\"Su operacion no pudo completarse.\",\"nota\":\"Rechazo generico\"}}";

        private static OrganizationServiceEnMemoria ArmarServicio()
        {
            var svc = new OrganizationServiceEnMemoria();
            svc.Sembrar(new Entity(Tablas.Parametro) { ["statecode"] = new OptionSetValue(Tablas.Activo), ["sanic_nombre"] = "rpa.lote.maximo", ["sanic_version"] = 1, ["sanic_valor"] = "50" });
            svc.Sembrar(new Entity(Tablas.Parametro) { ["statecode"] = new OptionSetValue(Tablas.Activo), ["sanic_nombre"] = "rpa.codigos", ["sanic_version"] = 1, ["sanic_valor"] = Codigos });
            svc.Sembrar(new Entity(Tablas.Parametro) { ["statecode"] = new OptionSetValue(Tablas.Activo), ["sanic_nombre"] = TransicionDeFilaStep.ParametroRpaPuedeAprobar, ["sanic_version"] = 1, ["sanic_valor"] = "no" });

            // Los tres roles de negocio tienen que existir para que TransicionDeFilaStep pueda resolver roles
            // (Steps/StepsDeFila.cs, PlomeriaDeFila.DeterminarRoles) — si no existe NINGUNO, el step lo trata como
            // "solución mal instalada", no como "sin rol", y tira ANTES de llegar a la autorización real.
            var rolRpa = svc.Sembrar(new Entity(TablasNativas.Rol) { ["name"] = TablasNativas.RolRpa });
            svc.Sembrar(new Entity(TablasNativas.Rol) { ["name"] = TablasNativas.RolEjecutivo });
            svc.Sembrar(new Entity(TablasNativas.Rol) { ["name"] = TablasNativas.RolSupervisor });

            // `ElBot` (quienLlama de todo `Core(svc).Ejecutar(...)` en esta suite) tiene que estar dado de alta con
            // el rol Rpa: `systemuserroles` es una tabla intersect (columnas `Guid` crudo, no `EntityReference`) —
            // mismo criterio que `StepsDeFilaAceptacion.Mundo.Usuario` y el propio comentario de
            // `PlomeriaDeFila.IdDeLaAsignacion` sobre el defecto real que tapaba justamente esto.
            svc.Sembrar(new Entity(TablasNativas.Usuario, ElBot) { ["fullname"] = "Bot RPA", ["applicationid"] = Guid.NewGuid() });
            svc.Sembrar(new Entity(TablasNativas.UsuarioRol) { ["systemuserid"] = ElBot, ["roleid"] = rolRpa });

            return svc;
        }

        private static Guid SembrarFilaValidada(OrganizationServiceEnMemoria svc)
        {
            // A propósito SIN `sanic_digitadapor`: es el camino más común del bot (intenta directo desde Validada)
            // y el que el defecto rompía siempre.
            return svc.Sembrar(new Entity(TablasHistorico.Fila)
            {
                ["sanic_estado"] = new OptionSetValue((int)EstadoDeLaFila.Validada),
                ["sanic_requiereatencionmanual"] = false,
            });
        }

        private static string Item(Guid filaId, string resultado, string codigo = "OK", string mensaje = null) =>
            "{\"filaid\":\"" + filaId + "\",\"resultado\":\"" + resultado + "\"," +
            (mensaje != null ? "\"mensaje\":\"" + mensaje + "\"," : "") +
            "\"codigo\":\"" + codigo + "\"," +
            "\"fechaproceso\":\"" + Ahora.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) + "\"}";

        private static string Lote(params string[] items) => "[" + string.Join(",", items) + "]";

        private static Entity Registro(OrganizationServiceEnMemoria svc, Guid filaId) =>
            svc.Registros(TablasHistorico.Fila).Single(f => f.Id == filaId);

        [Fact]
        public void Rechazada_sola_sobre_una_fila_Validada_nunca_digitada_se_acepta_con_la_transicion_real_de_plataforma()
        {
            var basico = ArmarServicio();
            var filaId = SembrarFilaValidada(basico);
            var svc = new ServicioConTransicionDeFilaReal(basico, Ahora);

            var resultado = new RegistrarResultadoRpa(new FilasDataverse(svc), new CatalogosDataverse(svc)).Ejecutar(
                Lote(Item(filaId, "rechazada", codigo: "RECH-GEN", mensaje: "CUENTA INACTIVA")), ElBot, Ahora, 24, false);

            Assert.Equal(1, resultado.Procesadas);
            Assert.Equal(0, resultado.ConError);
            var registro = Registro(basico, filaId);
            Assert.Equal((int)EstadoDeLaFila.RechazadaEnAS400, ((OptionSetValue)registro["sanic_estado"]).Value);
            Assert.Equal("Su operacion no pudo completarse.", registro["sanic_mensaje"]);
            Assert.Equal("CUENTA INACTIVA", registro["sanic_notainterna"]);
        }

        [Fact]
        public void Un_lote_mixto_digitada_y_rechazada_no_pierde_el_item_bueno_con_la_transicion_real_de_plataforma()
        {
            var basico = ArmarServicio();
            var filaDigitada = SembrarFilaValidada(basico);
            var filaRechazada = SembrarFilaValidada(basico);
            var svc = new ServicioConTransicionDeFilaReal(basico, Ahora);

            var lote = Lote(
                Item(filaDigitada, "digitada"),
                Item(filaRechazada, "rechazada", codigo: "RECH-GEN", mensaje: "prueba B-17"));

            var resultado = new RegistrarResultadoRpa(new FilasDataverse(svc), new CatalogosDataverse(svc))
                .Ejecutar(lote, ElBot, Ahora, 24, false);

            Assert.Equal(2, resultado.Procesadas);
            Assert.Equal(0, resultado.ConError);
            Assert.Equal((int)EstadoDeLaFila.Digitada, ((OptionSetValue)Registro(basico, filaDigitada)["sanic_estado"]).Value);
            Assert.Equal((int)EstadoDeLaFila.RechazadaEnAS400, ((OptionSetValue)Registro(basico, filaRechazada)["sanic_estado"]).Value);
        }

        [Fact]
        public void Digitada_sola_sigue_funcionando_con_la_transicion_real_de_plataforma_no_regresion()
        {
            // Repro mínima del set B ("digitada sola funciona"): no debería dejar de funcionar con el arreglo.
            var basico = ArmarServicio();
            var filaId = SembrarFilaValidada(basico);
            var svc = new ServicioConTransicionDeFilaReal(basico, Ahora);

            var resultado = new RegistrarResultadoRpa(new FilasDataverse(svc), new CatalogosDataverse(svc))
                .Ejecutar(Lote(Item(filaId, "digitada")), ElBot, Ahora, 24, false);

            Assert.Equal(1, resultado.Procesadas);
            Assert.Equal(0, resultado.ConError);
            Assert.Equal((int)EstadoDeLaFila.Digitada, ((OptionSetValue)Registro(basico, filaId)["sanic_estado"]).Value);
        }
    }

    /// <summary>
    /// Doble que ENVUELVE <see cref="OrganizationServiceEnMemoria"/> y, para cada `Update` de Fila que trae
    /// `sanic_estado`, corre <see cref="TransicionDeFilaStep"/> ANTES de persistir — el mismo control PreOperation
    /// que la plataforma real aplica sobre ese mismo `Update` (diseno/03 §4, paso 16 de la lista cerrada de steps).
    /// `Depth = 2` reproduce la condición real: `RegistrarResultadoRpaApi.Execute` crea su propio servicio con
    /// `fabrica.CreateOrganizationService(null)` (SYSTEM) y llama `_servicio.Update(...)` DESDE ADENTRO de su propia
    /// ejecución de plugin — un `Update` anidado, exactamente lo que hace que `BaseDeStep.EsCodigoDeServidor` (que
    /// solo mira `Depth &gt; 1`) devuelva `true` sin necesitar consultar nada más.
    ///
    /// No se simula <see cref="PostTransicionDeFilaStep"/> (Bitácora y cierre de Solicitud): esta suite prueba la
    /// autorización de la transición, no el cierre; ninguna de las pruebas de acá necesita la Bitácora.
    /// </summary>
    internal sealed class ServicioConTransicionDeFilaReal : IOrganizationService
    {
        private readonly OrganizationServiceEnMemoria _interno;
        private readonly DateTime _ahoraUtc;

        public ServicioConTransicionDeFilaReal(OrganizationServiceEnMemoria interno, DateTime ahoraUtc)
        {
            _interno = interno ?? throw new ArgumentNullException(nameof(interno));
            _ahoraUtc = ahoraUtc;
        }

        public Guid Create(Entity entity) => _interno.Create(entity);

        public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet) => _interno.Retrieve(entityName, id, columnSet);

        public void Delete(string entityName, Guid id) => _interno.Delete(entityName, id);

        public OrganizationResponse Execute(OrganizationRequest request) => _interno.Execute(request);

        public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) =>
            _interno.Associate(entityName, entityId, relationship, relatedEntities);

        public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) =>
            _interno.Disassociate(entityName, entityId, relationship, relatedEntities);

        public EntityCollection RetrieveMultiple(QueryBase query) => _interno.RetrieveMultiple(query);

        public void Update(Entity entity)
        {
            if (string.Equals(entity.LogicalName, TablasHistorico.Fila, StringComparison.Ordinal) && entity.Contains("sanic_estado"))
            {
                CorrerTransicionDeFilaStep(entity);
            }

            _interno.Update(entity);
        }

        private void CorrerTransicionDeFilaStep(Entity target)
        {
            var preImagen = _interno.Retrieve(TablasHistorico.Fila, target.Id, new ColumnSet("sanic_estado", "sanic_digitadapor", "sanic_solicitudid"));

            var ctx = new ContextoDePluginSimulado(_interno, _ahoraUtc);
            ctx.Contexto.Depth = 2; // código de servidor anidado (ver el docstring de la clase).
            ctx.Contexto.MessageName = "Update";
            ctx.Contexto.PrimaryEntityName = TablasHistorico.Fila;
            ctx.Contexto.InputParameters["Target"] = target;
            ctx.Contexto.PreEntityImages["PreImage"] = preImagen;

            // Tira InvalidPluginExecutionException si la plataforma real rechazaría esta transición — esa
            // excepción sube tal cual, igual que en Dataverse (nunca se atrapa acá).
            new TransicionDeFilaStep().Execute(ctx);
        }
    }
}
