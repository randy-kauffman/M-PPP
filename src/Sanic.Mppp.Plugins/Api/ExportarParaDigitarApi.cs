using System;
using Microsoft.Xrm.Sdk;

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
    ///    autorización: el registro de la Custom API (`ExecutePrivilegeName`, fuera de alcance de esta etapa) es el
    ///    control completo. Ver el reporte de la Etapa 1 para el privilegio recomendado;
    ///  - `filaids` (StringArray, REQUERIDO): ausente, o presente pero no es un arreglo de texto →
    ///    <see cref="InvalidPluginExecutionException"/>; un elemento que no se puede leer como <see cref="Guid"/> →
    ///    <see cref="InvalidPluginExecutionException"/> que nombra la posición. Un arreglo VACÍO no es un error: se
    ///    exporta un archivo válido con cero filas de datos (mismo criterio que <see cref="ExportarParaDigitar"/>);
    ///  - salidas `archivo` (String, el `.xlsx` en base64) y `exportadas` (Integer), con esos nombres exactos;
    ///  - el servicio se crea con SYSTEM (`CreateOrganizationService(null)`, diseno/04 §1): igual que las demás,
    ///    nunca con los privilegios de quien llama;
    ///  - deja rastro en el trace al empezar y al terminar (cuántas filas), sin volcar valores de ninguna fila;
    ///  - una <see cref="InvalidPluginExecutionException"/> de adentro se propaga; cualquier otra excepción se traza
    ///    completa y se vuelve a lanzar con un mensaje genérico (mismo criterio que las demás Custom API).
    /// </summary>
    public sealed class ExportarParaDigitarApi : IPlugin
    {
        public const string ParametroFilaIds = "filaids";
        public const string SalidaArchivo = "archivo";
        public const string SalidaExportadas = "exportadas";

        /// <summary>Lo único que se le dice a quien llama cuando algo se rompe de verdad. El detalle va al trace.</summary>
        public const string MensajeDeErrorGenerico = "No se pudo armar el archivo de exportación. El equipo tecnico ya tiene el detalle.";

        public void Execute(IServiceProvider serviceProvider)
        {
            throw new NotImplementedException("DN-02: lo implementa el constructor contra ExportarParaDigitarApiAceptacion, Etapa 2.");
        }
    }
}
