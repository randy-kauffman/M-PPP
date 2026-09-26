using System;
using System.Globalization;
using Microsoft.Xrm.Sdk;
using Sanic.Mppp.Plugins.Datos;
using Sanic.Mppp.Plugins.Dominio;
using Sanic.Mppp.Plugins.Steps;

namespace Sanic.Mppp.Plugins.Api
{
    /// <summary>
    /// El envoltorio `IPlugin` de `sanic_mppp_capi_registrarresultadorpa` (diseno/03 §3): traduce parámetros y arma
    /// las piezas. NO tiene lógica de negocio: eso es <see cref="RegistrarResultadoRpa"/>. Contrato (lo fijan las
    /// pruebas `RegistrarResultadoRpaApiAceptacion`):
    ///  - <see cref="PuertaDelRpa.Exigir"/> es la PRIMERA línea de lógica (D-9), antes de leer `resultados`;
    ///  - `resultados` (String, JSON): el arreglo del lote. Ausente se trata como lote vacío (`MPPP-RPA-009`, vía el
    ///    dominio: no hay un chequeo aparte acá). NOTA DE DISEÑO: el contrato (diseno/03 §3) no fija la forma
    ///    exacta del parámetro de ENTRADA del lote —solo muestra la forma de la RESPUESTA
    ///    (`{"resultados":[...],"procesadas":N,"conerror":M}") — así que "un solo `String` con un arreglo JSON,
    ///    simétrico a la salida" es una inferencia de esta pieza, no algo que el contrato zanje; se deja explícito
    ///    para que quien registre la Custom API (punto 3, fuera de este alcance) lo confirme o lo ajuste;
    ///  - salidas `resultados` (String, JSON), `procesadas` (Integer), `conerror` (Integer);
    ///  - el servicio se crea con SYSTEM; quien llama es `InitiatingUserId` (ya lo validó la puerta);
    ///  - `rpa.horas.tolerancia` y `rpa.puedeaprobar` DEGRADAN a su fallback si faltan o están mal cargados;
    ///  - `rpa.lote.maximo` y `rpa.codigos` son obligatorios: si faltan, es un error real de la llamada ENTERA (no
    ///    de un ítem), igual que un parámetro de plantilla faltante en `ValidarSolicitud`;
    ///  - un lote vacío o más grande que `rpa.lote.maximo` rechaza LA LLAMADA ENTERA (<see cref="ArgumentException"/>
    ///    del dominio, con su código `MPPP-RPA-008`/`009`), nunca un ítem individual;
    ///  - la fecha de referencia sale de `OperationCreatedOn` como UTC;
    ///  - una <see cref="InvalidPluginExecutionException"/> de adentro se propaga tal cual; una
    ///    <see cref="ArgumentException"/> del dominio (lote inaceptable) se relanza con su mismo mensaje; cualquier
    ///    otra excepción se traza completa y se relanza con un mensaje genérico.
    /// </summary>
    public sealed class RegistrarResultadoRpaApi : IPlugin
    {
        public const string ParametroResultados = "resultados";
        public const string SalidaResultados = "resultados";
        public const string SalidaProcesadas = "procesadas";
        public const string SalidaConError = "conerror";

        public const string ParametroToleranciaHoras = "rpa.horas.tolerancia";

        /// <summary>Lo único que se le dice al bot cuando algo se rompe de verdad (para la llamada entera). El detalle va al trace.</summary>
        public const string MensajeDeErrorGenerico = "No se pudieron registrar los resultados del RPA. El equipo tecnico ya tiene el detalle.";

        public void Execute(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new InvalidPluginExecutionException("No se recibió el proveedor de servicios del plugin.");
            }

            var contexto = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var fabrica = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var trace = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            if (contexto == null || fabrica == null || trace == null)
            {
                throw new InvalidPluginExecutionException("Faltan servicios de la plataforma para ejecutar la Custom API.");
            }

            // SYSTEM, no quien llama (diseno/04 §1).
            var servicio = fabrica.CreateOrganizationService(null);

            // Primera línea de lógica del plugin (diseno/03 encabezado, D-9): antes de leer 'resultados'.
            PuertaDelRpa.Exigir(contexto, servicio);

            var loteJson = LeerResultados(contexto);

            try
            {
                trace.Trace("RegistrarResultadoRpa: empieza.");

                var catalogos = new CatalogosDataverse(servicio);
                var toleranciaHoras = LeerToleranciaHoras(catalogos);
                var puedeAprobar = PlomeriaDeFila.LeerRpaPuedeAprobar(servicio);

                var core = new RegistrarResultadoRpa(new FilasDataverse(servicio), catalogos);
                var ahoraUtc = DateTime.SpecifyKind(contexto.OperationCreatedOn, DateTimeKind.Utc);
                var resultado = core.Ejecutar(loteJson, contexto.InitiatingUserId, ahoraUtc, toleranciaHoras, puedeAprobar);

                contexto.OutputParameters[SalidaResultados] = resultado.ResultadosJson;
                contexto.OutputParameters[SalidaProcesadas] = resultado.Procesadas;
                contexto.OutputParameters[SalidaConError] = resultado.ConError;

                trace.Trace($"RegistrarResultadoRpa: termina. procesadas={resultado.Procesadas}, conerror={resultado.ConError}.");
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (ArgumentException ex)
            {
                // El lote entero es inaceptable (MPPP-RPA-008/009, diseno/03 §3): no es un ítem, rechaza la llamada.
                throw new InvalidPluginExecutionException(ex.Message);
            }
            catch (Exception ex)
            {
                trace.Trace(ex.ToString());
                throw new InvalidPluginExecutionException(MensajeDeErrorGenerico);
            }
        }

        private static string LeerResultados(IPluginExecutionContext contexto)
        {
            if (!contexto.InputParameters.Contains(ParametroResultados))
            {
                return string.Empty;
            }

            return contexto.InputParameters[ParametroResultados] as string ?? string.Empty;
        }

        /// <summary>`rpa.horas.tolerancia`: ausente o no numérico DEGRADA al fallback quemado.</summary>
        private static int LeerToleranciaHoras(CatalogosDataverse catalogos)
        {
            var parametro = catalogos.Parametro(ParametroToleranciaHoras);
            if (parametro == null || !int.TryParse(parametro.Valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tolerancia) || tolerancia <= 0)
            {
                return ReglasDelRpa.ToleranciaHorasPorDefecto;
            }

            return tolerancia;
        }
    }
}
