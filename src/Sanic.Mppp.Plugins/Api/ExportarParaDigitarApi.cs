using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xrm.Sdk;
using Sanic.Mppp.Plugins.Datos;
using Sanic.Mppp.Plugins.Exportacion;

namespace Sanic.Mppp.Plugins.Api
{
    /// <summary>
    /// El envoltorio `IPlugin` de `sanic_mppp_capi_exportarparadigitar` (DN-02, diseno/DIRECTRICES-NUEVAS.md): traduce
    /// parámetros y arma las piezas. NO tiene lógica de negocio: eso es <see cref="ExportarParaDigitar"/>. Contrato
    /// (lo fijan las pruebas `ExportarParaDigitarApiAceptacion`):
    ///
    ///  - **Quién puede llamarla**: a diferencia de las dos Custom API de fase 2 (RPA), a ÉSTA la llama una PERSONA
    ///    desde la app (un Ejecutivo o un Supervisor, DN-02), no un bot. El control NO es una puerta en el código
    ///    como <see cref="Steps.PuertaDelRpa"/> — esa puerta existe porque el RPA no tiene un privilegio propio (D-9).
    ///    Acá el control es el que usan las dos Custom API de fase 1 (`clasificarcorreo`, `validarsolicitud`,
    ///    `03-contratos-custom-api.md` encabezado, D-9): `ExecutePrivilegeName` de la plataforma, sobre un privilegio
    ///    que YA existe y que solo tienen los roles que deben poder exportar. Por eso ESTA clase, igual que
    ///    <see cref="ValidarSolicitudApi"/> y <see cref="ClasificarCorreoApi"/>, NO trae una primera línea de
    ///    autorización. **Confirmado por el aprobador, 2026-09-26**: `ExecutePrivilegeName = prvReadsanic_mppp_tbl_fila`
    ///    — Ejecutivo y Supervisor lo tienen (`04-matriz-privilegios.md` §2: `R W Ap:O` sobre Fila), Administrador de
    ///    planes y Administrador técnico no tienen nada sobre Fila, así que quedan afuera solos. El registro real de
    ///    ese privilegio en la Custom API sigue fuera de alcance de este plugin (es una tarea de `pac`/Dev);
    ///  - `filaids` (StringArray, REQUERIDO): ausente, o presente pero no es un arreglo de texto →
    ///    <see cref="InvalidPluginExecutionException"/>; un elemento que no se puede leer como <see cref="Guid"/> →
    ///    <see cref="InvalidPluginExecutionException"/> que nombra la posición. Un arreglo VACÍO no es un error: se
    ///    exporta un archivo válido con cero filas de datos (mismo criterio que <see cref="ExportarParaDigitar"/>);
    ///  - salidas `archivo` (String, el `.xlsx` en base64) y `exportadas` (Integer), con esos nombres exactos;
    ///  - el servicio se crea con SYSTEM (`CreateOrganizationService(null)`, diseno/04 §1): igual que las demás,
    ///    nunca con los privilegios de quien llama;
    ///  - deja rastro en el trace al empezar y al terminar (cuántas filas), sin volcar valores de ninguna fila;
    ///  - una <see cref="InvalidPluginExecutionException"/> de adentro se propaga. Los rechazos de negocio (decisión
    ///    del aprobador, 2026-09-26) llegan a quien llama con su mensaje TAL CUAL, sin genericizar, porque el
    ///    ejecutivo necesita esa información para actuar: <see cref="FilasNoEncontradasException"/> (una fila
    ///    pedida no existe — "diez segundos: volver a seleccionar y bajar de nuevo") y
    ///    <see cref="PresupuestoDeExportacionAgotandoseException"/> (el plugin cortó solo antes de que la
    ///    plataforma reviente por el presupuesto de 2 minutos — "volvé a seleccionar con menos filas"). Cualquier
    ///    OTRA excepción se traza completa y se vuelve a lanzar con un mensaje genérico (mismo criterio que las
    ///    demás Custom API): esas dos no son "un error real" del sistema, son un rechazo esperado que el propio
    ///    dominio ya redactó para que lo lea una persona. Criterio general del proyecto: se genericiza lo que
    ///    revela cómo funciona el sistema por dentro; se deja pasar lo que le dice a la persona qué tiene que hacer.
    /// </summary>
    public sealed class ExportarParaDigitarApi : IPlugin
    {
        public const string ParametroFilaIds = "filaids";
        public const string SalidaArchivo = "archivo";
        public const string SalidaExportadas = "exportadas";

        /// <summary>Lo único que se le dice a quien llama cuando algo se rompe de verdad. El detalle va al trace.</summary>
        public const string MensajeDeErrorGenerico = "No se pudo armar el archivo de exportación. El equipo tecnico ya tiene el detalle.";

        /// <summary>
        /// Seam SOLO para pruebas (requiere <c>InternalsVisibleTo</c> hacia el proyecto de pruebas): si no es null,
        /// se usa como reloj en vez de un cronómetro real. Sin esto, una prueba de este wrapper que quisiera
        /// ejercitar <see cref="PresupuestoDeExportacionAgotandoseException"/> tendría que esperar casi 2 minutos
        /// de verdad. Producción NUNCA la toca: queda en null siempre, y en null usa el cronómetro real de
        /// <see cref="ExportarParaDigitar"/>.
        /// </summary>
        internal static Func<TimeSpan> RelojParaPruebas;

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

            // SYSTEM, no quien llama (diseno/04 §1): igual que las demás Custom API de este proyecto.
            var servicio = fabrica.CreateOrganizationService(null);

            var filaIds = LeerFilaIds(contexto);

            try
            {
                trace.Trace($"ExportarParaDigitar: empieza. filas pedidas={filaIds.Count.ToString(CultureInfo.InvariantCulture)}.");

                var core = new ExportarParaDigitar(new FilasDataverse(servicio), new CatalogosDataverse(servicio), new ArmadorExportacionOpenXml());
                var reloj = RelojParaPruebas; // una sola lectura: evita una carrera si una prueba lo cambia a mitad de camino
                var resultado = reloj != null ? core.Ejecutar(filaIds, reloj) : core.Ejecutar(filaIds);

                contexto.OutputParameters[SalidaArchivo] = resultado.ArchivoBase64;
                contexto.OutputParameters[SalidaExportadas] = resultado.Exportadas;

                trace.Trace($"ExportarParaDigitar: termina. exportadas={resultado.Exportadas.ToString(CultureInfo.InvariantCulture)}.");
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (FilasNoEncontradasException ex)
            {
                // Decisión del aprobador (2026-09-26): este mensaje SÍ llega tal cual, no es un error interno.
                throw new InvalidPluginExecutionException(ex.Message);
            }
            catch (PresupuestoDeExportacionAgotandoseException ex)
            {
                // Misma decisión: el ejecutivo necesita saber qué hacer (seleccionar menos), no un error de plataforma.
                throw new InvalidPluginExecutionException(ex.Message);
            }
            catch (Exception ex)
            {
                trace.Trace(ex.ToString());
                throw new InvalidPluginExecutionException(MensajeDeErrorGenerico);
            }
        }

        /// <summary>`filaids`: REQUERIDO, StringArray (calza con `SelectedControlSelectedItemIds` del ribbon sin JSON de por medio). Cada elemento tiene que parsear como Guid.</summary>
        private static IList<Guid> LeerFilaIds(IPluginExecutionContext contexto)
        {
            if (!contexto.InputParameters.Contains(ParametroFilaIds))
            {
                throw new InvalidPluginExecutionException($"El parámetro '{ParametroFilaIds}' es requerido.");
            }

            if (!(contexto.InputParameters[ParametroFilaIds] is string[] valores))
            {
                throw new InvalidPluginExecutionException($"El parámetro '{ParametroFilaIds}' tiene que ser un arreglo de texto (StringArray).");
            }

            var ids = new List<Guid>(valores.Length);
            for (var i = 0; i < valores.Length; i++)
            {
                if (!Guid.TryParse(valores[i], out var id))
                {
                    throw new InvalidPluginExecutionException(
                        $"El parámetro '{ParametroFilaIds}' trae un valor que no es un identificador válido en la posición {(i + 1).ToString(CultureInfo.InvariantCulture)}.");
                }

                ids.Add(id);
            }

            return ids;
        }
    }
}
