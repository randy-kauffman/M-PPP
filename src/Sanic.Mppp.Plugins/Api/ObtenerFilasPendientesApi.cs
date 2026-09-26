using System;
using System.Globalization;
using Microsoft.Xrm.Sdk;
using Sanic.Mppp.Plugins.Datos;
using Sanic.Mppp.Plugins.Dominio;
using Sanic.Mppp.Plugins.Steps;

namespace Sanic.Mppp.Plugins.Api
{
    /// <summary>
    /// El envoltorio `IPlugin` de `sanic_mppp_capi_obtenerfilaspendientes` (diseno/03 §2): traduce parámetros y arma
    /// las piezas. NO tiene lógica de negocio: eso es <see cref="ObtenerFilasPendientes"/>. Contrato (lo fijan las
    /// pruebas `ObtenerFilasPendientesApiAceptacion`):
    ///  - <see cref="PuertaDelRpa.Exigir"/> es la PRIMERA línea de lógica (diseno/03 encabezado, D-9): antes de leer
    ///    cualquier parámetro de entrada, para no gastar nada en un llamador no autorizado;
    ///  - `maximo` (Integer, OPCIONAL): ausente → defecto 100; presente y no es entero, o es 0 o negativo →
    ///    <see cref="InvalidPluginExecutionException"/>; el recorte al tope (500) lo hace el dominio;
    ///  - `claimminutos` (Integer, REQUERIDO): ausente, no es entero, 0 o negativo → <see cref="InvalidPluginExecutionException"/>;
    ///  - salidas `filas` (String, JSON), `entregadas` (Integer), `pendientes` (Integer), con esos nombres exactos;
    ///  - el servicio se crea con SYSTEM (`CreateOrganizationService(null)`, diseno/04 §1); quien llama es
    ///    `IPluginExecutionContext.InitiatingUserId` (ya lo validó la puerta: es el usuario de aplicación del RPA);
    ///  - `rpa.minutos.claimmaximo` y `rpa.puedeaprobar` se leen del parámetro; ausentes o mal cargados DEGRADAN al
    ///    fallback (mismo criterio que <see cref="ReglasDelRpa"/> en todos lados: un dato de configuración que falta
    ///    no puede tumbar al bot), nunca tumban la llamada;
    ///  - la fecha sale de `OperationCreatedOn` como UTC; el código nunca lee el reloj;
    ///  - deja rastro en el trace al empezar y al terminar (entregadas/pendientes), sin volcar datos de las filas;
    ///  - una <see cref="InvalidPluginExecutionException"/> de adentro se propaga; cualquier otra excepción se traza
    ///    completa y se vuelve a lanzar con un mensaje genérico.
    /// </summary>
    public sealed class ObtenerFilasPendientesApi : IPlugin
    {
        public const string ParametroMaximo = "maximo";
        public const string ParametroClaimMinutos = "claimminutos";
        public const string SalidaFilas = "filas";
        public const string SalidaEntregadas = "entregadas";
        public const string SalidaPendientes = "pendientes";

        public const string ParametroClaimMaximo = "rpa.minutos.claimmaximo";

        /// <summary>Lo único que se le dice al bot cuando algo se rompe de verdad. El detalle va al trace.</summary>
        public const string MensajeDeErrorGenerico = "No se pudieron obtener las filas pendientes. El equipo tecnico ya tiene el detalle.";

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

            // SYSTEM, no quien llama (diseno/04 §1): el rol del RPA no tiene privilegios sobre tablas MPPP, a propósito.
            var servicio = fabrica.CreateOrganizationService(null);

            // Primera línea de lógica del plugin (diseno/03 encabezado, D-9): antes de leer ningún parámetro.
            PuertaDelRpa.Exigir(contexto, servicio);

            var maximo = LeerMaximo(contexto);
            var claimMinutos = LeerClaimMinutos(contexto);

            try
            {
                trace.Trace($"ObtenerFilasPendientes: empieza. maximo={maximo}, claimminutos={claimMinutos}.");

                var catalogos = new CatalogosDataverse(servicio);
                var claimTope = LeerClaimTope(catalogos);
                var puedeAprobar = PlomeriaDeFila.LeerRpaPuedeAprobar(servicio);

                var core = new ObtenerFilasPendientes(new FilasDataverse(servicio), catalogos);
                var ahoraUtc = DateTime.SpecifyKind(contexto.OperationCreatedOn, DateTimeKind.Utc);
                var resultado = core.Ejecutar(maximo, claimMinutos, claimTope, puedeAprobar, contexto.InitiatingUserId, ahoraUtc);

                contexto.OutputParameters[SalidaFilas] = resultado.FilasJson;
                contexto.OutputParameters[SalidaEntregadas] = resultado.Entregadas;
                contexto.OutputParameters[SalidaPendientes] = resultado.Pendientes;

                trace.Trace($"ObtenerFilasPendientes: termina. entregadas={resultado.Entregadas}, pendientes={resultado.Pendientes}.");
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                trace.Trace(ex.ToString());
                throw new InvalidPluginExecutionException(MensajeDeErrorGenerico);
            }
        }

        /// <summary>`maximo`: OPCIONAL. Ausente → 100 (diseno/03 §2). Presente y no es un entero positivo → error.</summary>
        private static int LeerMaximo(IPluginExecutionContext contexto)
        {
            if (!contexto.InputParameters.Contains(ParametroMaximo))
            {
                return ReglasDelRpa.MaximoPorDefecto;
            }

            if (!(contexto.InputParameters[ParametroMaximo] is int maximo) || maximo <= 0)
            {
                throw new InvalidPluginExecutionException($"El parámetro '{ParametroMaximo}' tiene que ser un entero mayor que cero.");
            }

            return maximo;
        }

        /// <summary>`claimminutos`: REQUERIDO (diseno/03 §2 no le da defecto, a diferencia de `maximo`).</summary>
        private static int LeerClaimMinutos(IPluginExecutionContext contexto)
        {
            if (!contexto.InputParameters.Contains(ParametroClaimMinutos) || !(contexto.InputParameters[ParametroClaimMinutos] is int claimMinutos) || claimMinutos <= 0)
            {
                throw new InvalidPluginExecutionException($"El parámetro '{ParametroClaimMinutos}' tiene que ser un entero mayor que cero.");
            }

            return claimMinutos;
        }

        /// <summary>`rpa.minutos.claimmaximo`: ausente o no numérico DEGRADA al fallback quemado (mismo criterio que en todo `ReglasDelRpa`).</summary>
        private static int LeerClaimTope(CatalogosDataverse catalogos)
        {
            var parametro = catalogos.Parametro(ParametroClaimMaximo);
            if (parametro == null || !int.TryParse(parametro.Valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tope) || tope <= 0)
            {
                return ReglasDelRpa.ClaimMaximoMinutosPorDefecto;
            }

            return tope;
        }
    }
}
