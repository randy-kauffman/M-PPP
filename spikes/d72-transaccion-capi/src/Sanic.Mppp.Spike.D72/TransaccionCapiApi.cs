using System;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;

namespace Sanic.Mppp.Spike.D72
{
    /// <summary>
    /// SPIKE D-72 (se descarta). Operacion principal de la Custom API sanic_mppp_capi_zzspiked72.
    /// Hace varios Create sobre sanic_mppp_tbl_autorizado y, segun `modo`, lanza una excepcion.
    /// </summary>
    public sealed class TransaccionCapiApi : IPlugin
    {
        private const string Tabla = "sanic_mppp_tbl_autorizado";

        public void Execute(IServiceProvider serviceProvider)
        {
            var contexto = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var traza = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            var fabrica = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));

            var modo = (string)contexto.InputParameters["modo"];
            var marca = (string)contexto.InputParameters["marca"];

            var usuario = fabrica.CreateOrganizationService(contexto.UserId);
            var sistema = fabrica.CreateOrganizationService(null);

            int creados = 0;

            switch (modo)
            {
                case "exito":
                    for (int n = 1; n <= 3; n++) { Crear(usuario, marca, n); creados++; }
                    break;

                case "excepcion":
                    for (int n = 1; n <= 3; n++) { Crear(usuario, marca, n); creados++; }
                    Lanzar(contexto, traza, modo, creados, null);
                    break;

                case "fallo_escritura":
                    Crear(usuario, marca, 1); creados++;
                    Crear(usuario, marca, 2); creados++;
                    try
                    {
                        // Mismo correo que el 1.o: choca con la clave sanic_mppp_key_autorizado_correo.
                        Crear(usuario, marca, 1);
                        creados++;
                    }
                    catch (FaultException<OrganizationServiceFault> ex)
                    {
                        // Sin ninguna otra llamada al servicio: solo traza (no es el servicio) y lanzar.
                        Lanzar(contexto, traza, modo, creados, "D72-FALLO: " + ex.Message);
                    }
                    break;

                case "sistema":
                    for (int n = 1; n <= 3; n++) { Crear(sistema, marca, n); creados++; }
                    Lanzar(contexto, traza, modo, creados, null);
                    break;

                case "mixto":
                    Crear(usuario, marca, 1); creados++;
                    Crear(usuario, marca, 2); creados++;
                    Crear(sistema, marca, 3); creados++;
                    Crear(sistema, marca, 4); creados++;
                    Lanzar(contexto, traza, modo, creados, null);
                    break;

                default:
                    throw new InvalidPluginExecutionException("D72-MODO-DESCONOCIDO: " + modo);
            }

            Salidas(contexto, creados);
            traza.Trace(Resumen(contexto, modo, creados));
        }

        private static void Crear(IOrganizationService servicio, string marca, int n)
        {
            var fila = new Entity(Tabla);
            fila["sanic_nombre"] = "zz-d72-" + marca + "-" + n + "@spike.invalid";
            fila["sanic_nombrecontacto"] = "ZZ spike D-72";
            servicio.Create(fila);
        }

        private static void Salidas(IPluginExecutionContext contexto, int creados)
        {
            contexto.OutputParameters["entransaccion"] = contexto.IsInTransaction;
            contexto.OutputParameters["etapa"] = contexto.Stage;
            contexto.OutputParameters["profundidad"] = contexto.Depth;
            contexto.OutputParameters["creados"] = creados;
        }

        private static string Resumen(IPluginExecutionContext contexto, string modo, int creados)
        {
            return "D72 modo=" + modo
                + " entransaccion=" + contexto.IsInTransaction
                + " etapa=" + contexto.Stage
                + " profundidad=" + contexto.Depth
                + " creados=" + creados;
        }

        // Escribe los mismos valores en la traza ANTES de lanzar, y lanza.
        private static void Lanzar(IPluginExecutionContext contexto, ITracingService traza, string modo, int creados, string mensaje)
        {
            traza.Trace(Resumen(contexto, modo, creados));
            throw new InvalidPluginExecutionException(mensaje ?? "D72-EXCEPCION");
        }
    }
}
