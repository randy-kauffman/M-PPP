using System;
using Microsoft.Xrm.Sdk;
using Sanic.Mppp.Plugins.Exportacion;

namespace Sanic.Mppp.Plugins.Api
{
    // =====================================================================================================================
    // DN-04 (diseno/DIRECTRICES-NUEVAS.md; diseno/03 §1c y §1d): el núcleo y los dos envoltorios IPlugin de
    // sanic_mppp_capi_cargarcatalogos y sanic_mppp_capi_cambiarestadocatalogos. Une lo ya construido: lector estricto →
    // planificador → foto → escritor → armador. FIRMAS DEL ARQUITECTO. Pruebas: tests/.../Aceptacion/CatalogosMasivosApiAceptacion.cs.
    // =====================================================================================================================

    /// <summary>Lo que devuelve la carga (Excel 1). Con <see cref="Grabado"/> = false no se escribió nada.</summary>
    public sealed class ResultadoDeCargaMasiva
    {
        public ResultadoDeCargaMasiva(bool grabado, string resultadoBase64, int filas, int errores, int advertencias,
            int clientesCreados, int planesCreados, int correosCreados, int autorizacionesCreadas)
        {
            Grabado = grabado; ResultadoBase64 = resultadoBase64; Filas = filas; Errores = errores; Advertencias = advertencias;
            ClientesCreados = clientesCreados; PlanesCreados = planesCreados; CorreosCreados = correosCreados; AutorizacionesCreadas = autorizacionesCreadas;
        }

        public bool Grabado { get; }
        public string ResultadoBase64 { get; }
        /// <summary>Filas no vacías leídas.</summary>
        public int Filas { get; }
        /// <summary>Filas con al menos un error.</summary>
        public int Errores { get; }
        /// <summary>Filas con al menos una advertencia.</summary>
        public int Advertencias { get; }
        public int ClientesCreados { get; }
        public int PlanesCreados { get; }
        public int CorreosCreados { get; }
        public int AutorizacionesCreadas { get; }
    }

    /// <summary>Lo que devuelve el cambio de estado (Excel 2). Con <see cref="Grabado"/> = false no se escribió nada.</summary>
    public sealed class ResultadoDeCambioMasivo
    {
        public ResultadoDeCambioMasivo(bool grabado, string resultadoBase64, int filas, int errores, int advertencias, int activados, int desactivados, int sinCambios)
        {
            Grabado = grabado; ResultadoBase64 = resultadoBase64; Filas = filas; Errores = errores; Advertencias = advertencias;
            Activados = activados; Desactivados = desactivados; SinCambios = sinCambios;
        }

        public bool Grabado { get; }
        public string ResultadoBase64 { get; }
        public int Filas { get; }
        public int Errores { get; }
        public int Advertencias { get; }
        public int Activados { get; }
        public int Desactivados { get; }
        public int SinCambios { get; }
    }

    /// <summary>
    /// Un rechazo con código (DN-04 §"Contratos", tabla MPPP-CAR). <see cref="Exception.Message"/> = "&lt;Codigo&gt;: &lt;texto&gt;",
    /// en español y sin jerga de librerías: es lo que el JS le muestra al usuario tal cual.
    /// </summary>
    public sealed class CargaMasivaRechazadaException : Exception
    {
        public CargaMasivaRechazadaException(string codigo, string texto, Exception interna = null)
            : base(codigo + ": " + texto, interna)
        {
            Codigo = codigo;
        }

        /// <summary>"MPPP-CAR-001" a "MPPP-CAR-010".</summary>
        public string Codigo { get; }
    }

    /// <summary>
    /// El núcleo de las dos Custom API, sin IPlugin (se prueba con dobles). Reglas:
    ///  - <c>usuario</c> lee y escribe los CATÁLOGOS (auditoría: queda quién lo hizo); <c>sistema</c> lee PARÁMETROS, usuarios y
    ///    roles (el Administrador de planes no tiene privilegio sobre Parámetro, diseno/04 §2).
    ///  - Parámetros: <see cref="ParametroFilasMaximo"/> (si falta, no es entero o es menor que 1: <see cref="FilasMaximoPorDefecto"/>)
    ///    y `lectura.limites` (como la ingesta: si falta, los valores por defecto de <c>LimitesLectura</c>).
    ///  - Rechazos del archivo, por <c>TipoDeRechazoLectura</c>: vacío o base64 inválido → 001; ArchivoIlegible, CeldaIlegible,
    ///    Desorden o SinClasificar → 002; PesoExcedido → 003; HojaInexistente → 004; Encabezado o ColumnaDeMas → 005;
    ///    FilaDeMas → 006; ninguna fila de datos → 007. Siempre como <see cref="CargaMasivaRechazadaException"/>, nada escrito.
    ///  - Con errores de fila: archivo de resultado y Grabado = false, NINGUNA escritura.
    ///  - Sin errores: escribe con el presupuesto (<see cref="PresupuestoSegundos"/>, medido con <c>transcurrido</c> desde el
    ///    inicio de la llamada); presupuesto agotado → 008; una escritura que falla → 009 con su fila ("No se grabó nada").
    ///    Las dos dejan que la excepción suba: la transacción de la Custom API deshace lo escrito (D-72).
    /// </summary>
    public sealed class CatalogosMasivos
    {
        public const string ParametroFilasMaximo = "carga.filas.maximo";
        public const int FilasMaximoPorDefecto = 300;
        /// <summary>120 s del límite síncrono menos 20 de margen, como exportarparadigitar (diseno/03 §1b).</summary>
        public const int PresupuestoSegundos = 100;

        /// <param name="hoy">La fecha de hoy en Nicaragua (solo fecha): la calcula el envoltorio.</param>
        public CatalogosMasivos(IOrganizationService usuario, IOrganizationService sistema, Func<TimeSpan> transcurrido, DateTime hoy)
            => throw new NotImplementedException();

        /// <summary>La plantilla vacía de esa estructura, en base64. No consulta nada.</summary>
        public string Plantilla(EstructuraDeExcelDeCatalogo estructura) => throw new NotImplementedException();

        public ResultadoDeCargaMasiva Cargar(string archivoBase64) => throw new NotImplementedException();

        public ResultadoDeCambioMasivo CambiarEstado(string archivoBase64) => throw new NotImplementedException();
    }

    /// <summary>
    /// Envoltorio de sanic_mppp_capi_cargarcatalogos. Entradas: <see cref="ParametroArchivo"/> (String) y
    /// <see cref="ParametroPlantilla"/> (Boolean, opcional). Con plantilla = true y sin archivo devuelve la plantilla vacía en
    /// <see cref="SalidaResultado"/> y todo lo demás en false/0; con los dos → MPPP-CAR-010; sin ninguno → MPPP-CAR-001.
    /// Servicios: el del usuario que llama (<c>UserId</c> del contexto) y SYSTEM (null). Hoy = fecha UTC menos 6 horas
    /// (Nicaragua, sin horario de verano). Un <see cref="CargaMasivaRechazadaException"/> sale como
    /// InvalidPluginExecutionException con su mensaje tal cual; cualquier otra excepción va a la traza y sale con
    /// <see cref="MensajeDeErrorGenerico"/>.
    /// </summary>
    public sealed class CargarCatalogosApi : IPlugin
    {
        public const string ParametroArchivo = "archivo";
        public const string ParametroPlantilla = "plantilla";
        public const string SalidaGrabado = "grabado";
        public const string SalidaResultado = "resultado";
        public const string SalidaFilas = "filas";
        public const string SalidaErrores = "errores";
        public const string SalidaAdvertencias = "advertencias";
        public const string SalidaClientesCreados = "clientescreados";
        public const string SalidaPlanesCreados = "planescreados";
        public const string SalidaCorreosCreados = "correoscreados";
        public const string SalidaAutorizacionesCreadas = "autorizacionescreadas";
        public const string MensajeDeErrorGenerico = "No se pudo procesar el archivo. No se grabó nada. El equipo técnico ya tiene el detalle.";

        public void Execute(IServiceProvider serviceProvider) => throw new NotImplementedException();
    }

    /// <summary>Envoltorio de sanic_mppp_capi_cambiarestadocatalogos. Mismo contrato que <see cref="CargarCatalogosApi"/>.</summary>
    public sealed class CambiarEstadoCatalogosApi : IPlugin
    {
        public const string SalidaActivados = "activados";
        public const string SalidaDesactivados = "desactivados";
        public const string SalidaSinCambios = "sincambios";

        public void Execute(IServiceProvider serviceProvider) => throw new NotImplementedException();
    }
}
