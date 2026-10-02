using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Mppp.Plugins.Catalogos;
using Sanic.Mppp.Plugins.Datos;
using Sanic.Mppp.Plugins.Exportacion;
using Sanic.Mppp.Plugins.Plantilla;

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

        /// <summary>"MPPP-CAR-001" a "MPPP-CAR-011".</summary>
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
    ///    inicio de la llamada); presupuesto agotado → 008; una escritura que falla → 009 con su fila ("No se grabó nada"),
    ///    salvo que su <c>ErrorCode</c> sea de plataforma (sobrecarga, límites, tiempo agotado) → 011 sin culpar a la fila (D-75).
    ///    Las tres dejan que la excepción suba: la transacción de la Custom API deshace lo escrito (D-72).
    /// </summary>
    public sealed class CatalogosMasivos
    {
        public const string ParametroFilasMaximo = "carga.filas.maximo";
        public const int FilasMaximoPorDefecto = 300;
        /// <summary>120 s del límite síncrono menos 20 de margen, como exportarparadigitar (diseno/03 §1b).</summary>
        public const int PresupuestoSegundos = 100;

        /// <summary>Máximo de filas de una hoja de Excel menos la del encabezado: un valor mayor en el parámetro no cabe en la hoja.</summary>
        private const int FilasMaximoQueCabenEnLaHoja = 1048575;

        private const string ParametroLimites = "lectura.limites";

        /// <summary>Holgura sobre 4*ceil(tope/3) para no rechazar por el relleno ni por un salto de línea suelto.</summary>
        private const int MargenDeCaracteresDeBase64 = 16;

        /// <summary>Lo más que se muestra del motivo de Dataverse en el 009; el detalle completo va a la traza.</summary>
        private const int MaximoDeCaracteresDelMotivo = 300;

        private readonly IOrganizationService _usuario;
        private readonly IOrganizationService _sistema;
        private readonly Func<TimeSpan> _transcurrido;
        private readonly DateTime _hoy;

        /// <param name="hoy">La fecha de hoy en Nicaragua (solo fecha): la calcula el envoltorio.</param>
        public CatalogosMasivos(IOrganizationService usuario, IOrganizationService sistema, Func<TimeSpan> transcurrido, DateTime hoy)
        {
            _usuario = usuario ?? throw new ArgumentNullException(nameof(usuario));
            _sistema = sistema ?? throw new ArgumentNullException(nameof(sistema));
            _transcurrido = transcurrido ?? throw new ArgumentNullException(nameof(transcurrido));
            _hoy = DateTime.SpecifyKind(hoy.Date, DateTimeKind.Unspecified);
        }

        /// <summary>La plantilla vacía de esa estructura, en base64. No consulta nada.</summary>
        public string Plantilla(EstructuraDeExcelDeCatalogo estructura)
        {
            if (estructura == null) throw new ArgumentNullException(nameof(estructura));
            return Convert.ToBase64String(ArmadorDeExcelDeCatalogos.Plantilla(estructura));
        }

        public ResultadoDeCargaMasiva Cargar(string archivoBase64)
        {
            var estructura = EstructuraDeExcelDeCatalogo.Carga;
            var filas = LeerArchivo(archivoBase64, estructura, out var avisos);

            var foto = new FotoDeCatalogosDataverse(_usuario, _sistema).ParaCarga(ClavesDeCarga.Extraer(filas));
            var plan = PlanificadorDeCarga.Planificar(filas, foto, _hoy, avisos);

            var resultado = Convert.ToBase64String(ArmadorDeExcelDeCatalogos.Resultado(
                estructura, filas, plan.Filas.ToDictionary(f => f.NumeroFilaExcel, f => f.Texto)));
            var errores = plan.Filas.Count(f => f.Errores.Count > 0);
            var advertencias = plan.Filas.Count(f => f.Advertencias.Count > 0);

            // Con errores en alguna fila no se escribe NADA (todo o nada): el plan trae las listas de creación vacías.
            if (plan.TieneErrores)
                return new ResultadoDeCargaMasiva(false, resultado, filas.Count, errores, advertencias, 0, 0, 0, 0);

            // Sin nada que escribir no hay nada que perder por el tiempo: se devuelve el resultado, sin mirar el presupuesto.
            if (plan.Clientes.Count + plan.Planes.Count + plan.Autorizados.Count + plan.Autorizaciones.Count > 0)
                Escribir(escritor => escritor.Aplicar(plan));
            return new ResultadoDeCargaMasiva(true, resultado, filas.Count, errores, advertencias,
                plan.Clientes.Count, plan.Planes.Count, plan.Autorizados.Count, plan.Autorizaciones.Count);
        }

        public ResultadoDeCambioMasivo CambiarEstado(string archivoBase64)
        {
            var estructura = EstructuraDeExcelDeCatalogo.CambioDeEstado;
            var filas = LeerArchivo(archivoBase64, estructura, out var avisos);

            var foto = new FotoDeCatalogosDataverse(_usuario, _sistema).ParaCambioDeEstado(ClavesDeCambioDeEstado.Extraer(filas));
            var plan = PlanificadorDeCambioDeEstado.Planificar(filas, foto, avisos);

            var resultado = Convert.ToBase64String(ArmadorDeExcelDeCatalogos.Resultado(
                estructura, filas, plan.Filas.ToDictionary(f => f.NumeroFilaExcel, f => f.Texto)));
            var errores = plan.Filas.Count(f => f.Errores.Count > 0);
            var advertencias = plan.Filas.Count(f => f.Advertencias.Count > 0);

            if (plan.TieneErrores)
                return new ResultadoDeCambioMasivo(false, resultado, filas.Count, errores, advertencias, 0, 0, 0);

            if (plan.Cambios.Count > 0)
                Escribir(escritor => escritor.Aplicar(plan));
            return new ResultadoDeCambioMasivo(true, resultado, filas.Count, errores, advertencias, plan.Activados, plan.Desactivados, plan.SinCambios);
        }

        // ============================================================================================ lectura del archivo (MPPP-CAR-001 a 007)

        /// <summary>
        /// Devuelve las filas no vacías y, aparte, los avisos del lector (un #REF!, un número que pudo redondearse) para que el
        /// planificador los vuelva errores de su fila. Los avisos de la columna Resultado se descartan: es solo de salida y nunca
        /// se lee (un archivo de resultado corregido y vuelto a subir puede traer cualquier cosa ahí).
        /// </summary>
        private IReadOnlyList<FilaPlantilla> LeerArchivo(string archivoBase64, EstructuraDeExcelDeCatalogo estructura, out IReadOnlyList<AdvertenciaLectura> avisos)
        {
            if (string.IsNullOrWhiteSpace(archivoBase64))
                throw ArchivoInvalido();

            // Los parámetros se leen con SYSTEM: el Administrador de planes no tiene privilegio sobre Parámetro (diseno/04 §2).
            // Primero, porque el tope de peso se mira ANTES de decodificar: un texto enorme no se convierte en bytes para después rechazarlo.
            var parametros = new CatalogosDataverse(_sistema);
            var maximo = FilasMaximoVigente(parametros.Parametro(ParametroFilasMaximo));
            var limites = LimitesLectura.DesdeJson(parametros.Parametro(ParametroLimites)?.Valor);

            // 4 caracteres de base64 por cada 3 bytes, más un margen chico: si el texto ya trae más que eso, los bytes pasarían el tope.
            var maximoDeCaracteres = 4 * ((limites.TamanoMaximoBytesEntrada + 2) / 3) + MargenDeCaracteresDeBase64;
            if (archivoBase64.Length > maximoDeCaracteres)
            {
                throw new CargaMasivaRechazadaException("MPPP-CAR-003",
                    "El archivo pesa más de lo permitido. Quitale filas o partilo en varios archivos y subí cada uno por separado.",
                    new InvalidDataException("El texto del archivo trae " + archivoBase64.Length.ToString(CultureInfo.InvariantCulture)
                        + " caracteres: con el tope de " + limites.TamanoMaximoBytesEntrada.ToString(CultureInfo.InvariantCulture) + " bytes no puede ser un archivo válido. No se decodificó."));
            }

            var bytes = Decodificar(archivoBase64);

            var lectura = new LectorOpenXml(limites).Leer(bytes, estructura.ConfiguracionDeLectura(maximo));
            if (!lectura.EsValido)
                throw RechazoDelLector(lectura, estructura, maximo);

            if (lectura.Filas.Count == 0)
            {
                throw new CargaMasivaRechazadaException("MPPP-CAR-007",
                    "El archivo no tiene ninguna fila con datos. Completá al menos una fila debajo de los encabezados y volvé a subirlo.");
            }

            var letraDeResultado = estructura.ColumnaResultado.Letra;
            avisos = lectura.Advertencias.Where(a => a == null || !string.Equals(a.Columna, letraDeResultado, StringComparison.Ordinal)).ToList();
            return lectura.Filas.ToList();
        }

        private static CargaMasivaRechazadaException ArchivoInvalido()
        {
            return new CargaMasivaRechazadaException("MPPP-CAR-001",
                "No llegó un archivo válido. Elegí un archivo de Excel (.xlsx) y volvé a intentarlo.");
        }

        private static byte[] Decodificar(string archivoBase64)
        {
            byte[] bytes = null;
            try
            {
                bytes = Convert.FromBase64String(archivoBase64);
            }
            catch (FormatException)
            {
                // No es base64: se rechaza abajo con el mismo código que un archivo vacío.
            }

            if (bytes == null || bytes.Length == 0)
                throw ArchivoInvalido();

            return bytes;
        }

        /// <summary>Ausente, no entero o menor que 1: el valor quemado. Mayor que lo que cabe en la hoja: lo que cabe.</summary>
        private static int FilasMaximoVigente(ParametroLeido parametro)
        {
            if (parametro != null
                && int.TryParse(parametro.Valor == null ? null : parametro.Valor.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var maximo)
                && maximo >= 1)
            {
                return Math.Min(maximo, FilasMaximoQueCabenEnLaHoja);
            }

            return FilasMaximoPorDefecto;
        }

        /// <summary>
        /// El texto es nuestro, en español y sin jerga de la librería. Lo que dijo el lector (el detalle técnico, y qué celda) va como
        /// excepción interna: el envoltorio la deja en la traza y el usuario nunca la ve.
        /// </summary>
        private static CargaMasivaRechazadaException RechazoDelLector(ResultadoLecturaPlantilla lectura, EstructuraDeExcelDeCatalogo estructura, int maximo)
        {
            var detalle = new InvalidDataException("El lector rechazó el archivo (" + lectura.Rechazo + "): " + string.Join(" | ", lectura.Errores)
                + (string.IsNullOrWhiteSpace(lectura.DetalleTecnico) ? string.Empty : ". Detalle técnico: " + lectura.DetalleTecnico));

            switch (lectura.Rechazo)
            {
                case TipoDeRechazoLectura.PesoExcedido:
                    return new CargaMasivaRechazadaException("MPPP-CAR-003",
                        "El archivo pesa más de lo permitido. Quitale filas o partilo en varios archivos y subí cada uno por separado.", detalle);

                case TipoDeRechazoLectura.HojaInexistente:
                    return new CargaMasivaRechazadaException("MPPP-CAR-004",
                        "No existe la hoja '" + estructura.Hoja + "' en el archivo. Descargá la plantilla con la opción \"Descargar plantilla\" y no le cambies el nombre a la hoja.", detalle);

                case TipoDeRechazoLectura.Encabezado:
                    return new CargaMasivaRechazadaException("MPPP-CAR-005",
                        "Los encabezados del archivo no coinciden con la plantilla (" + string.Join("; ", lectura.Errores) + "). "
                        + "Descargá la plantilla y no cambies, agregues ni quites columnas.", detalle);

                case TipoDeRechazoLectura.ColumnaDeMas:
                    return new CargaMasivaRechazadaException("MPPP-CAR-005",
                        "El archivo tiene datos en columnas que la plantilla no tiene. " + string.Join(" ", lectura.Errores), detalle);

                case TipoDeRechazoLectura.FilaDeMas:
                    return new CargaMasivaRechazadaException("MPPP-CAR-006",
                        "El archivo trae más de " + maximo.ToString(CultureInfo.InvariantCulture) + " filas con datos, que es el máximo por archivo. "
                        + "Partilo en archivos más chicos y subí cada uno por separado.", detalle);

                default: // ArchivoIlegible, CeldaIlegible, Desorden, SinClasificar
                    return new CargaMasivaRechazadaException("MPPP-CAR-002",
                        "El archivo no se pudo leer como un Excel (.xlsx): puede estar dañado o ser de otro formato. "
                        + "Descargá la plantilla, pasá tus datos ahí y volvé a subirlo.", detalle);
            }
        }

        // ============================================================================================ escritura (MPPP-CAR-008, 009 y 011)

        /// <summary>
        /// Cuenta con el presupuesto ANTES de escribir (no solo dentro del escritor): si la pasada 1 ya se comió los 100 s no se
        /// empieza. Las dos excepciones dejan subir la falla para que la transacción de la Custom API deshaga lo escrito (D-72), y
        /// después de atraparlas no se hace NINGUNA llamada más al servicio.
        /// </summary>
        private void Escribir(Action<EscritorDeCatalogos> aplicar)
        {
            var presupuesto = TimeSpan.FromSeconds(PresupuestoSegundos);
            if (_transcurrido() >= presupuesto)
                throw TiempoAgotado(null);

            try
            {
                aplicar(new EscritorDeCatalogos(_usuario, _transcurrido, presupuesto));
            }
            catch (PresupuestoDeEscrituraAgotadoException ex)
            {
                throw TiempoAgotado(ex);
            }
            catch (EscrituraDeCatalogoFallidaException ex)
            {
                // Una falla de PLATAFORMA (sobrecarga, límites) no es culpa de la fila: se clasifica por ErrorCode, nunca por el texto (D-75).
                if (FallasDePlataforma.EsDePlataforma(ex.CodigoDeError))
                    throw new CargaMasivaRechazadaException("MPPP-CAR-011",
                        "Dataverse estaba sobrecargado y no pudo terminar de grabar. No se grabó nada. "
                        + "Esperá unos minutos y volvé a subir el mismo archivo; si vuelve a pasar, partilo en archivos más chicos.", ex);

                var motivo = Recortar((ex.Motivo ?? string.Empty).Trim());
                throw new CargaMasivaRechazadaException("MPPP-CAR-009",
                    "No se pudo grabar la fila " + ex.Fila.ToString(CultureInfo.InvariantCulture) + ": " + motivo + (motivo.EndsWith("\u2026", StringComparison.Ordinal) ? " " : ". ")
                    + "No se grabó nada. Corregí esa fila y volvé a subir el archivo.", ex);
            }
        }

        /// <summary>El motivo de Dataverse puede ser larguísimo (un error con su detalle entero): se muestran unos 300 caracteres, sin partir un par subrogado.</summary>
        private static string Recortar(string motivo)
        {
            if (motivo.Length <= MaximoDeCaracteresDelMotivo) return motivo.TrimEnd('.');

            var largo = MaximoDeCaracteresDelMotivo - 1;
            if (char.IsHighSurrogate(motivo[largo - 1])) largo--;
            return motivo.Substring(0, largo) + "\u2026";
        }

        private static CargaMasivaRechazadaException TiempoAgotado(Exception interna)
        {
            return new CargaMasivaRechazadaException("MPPP-CAR-008",
                "Se agotó el tiempo máximo de procesamiento (" + PresupuestoSegundos.ToString(CultureInfo.InvariantCulture) + " segundos) antes de terminar de grabar. "
                + "No se grabó nada. Partí el archivo en uno más chico y volvé a subirlo.", interna);
        }
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

        /// <summary>
        /// Seam SOLO para pruebas (requiere <c>InternalsVisibleTo</c>): si no es null, se usa como reloj en vez del cronómetro real,
        /// para probar el presupuesto sin esperar 100 s. Producción NUNCA la toca: queda en null.
        /// </summary>
        internal static Func<TimeSpan> RelojParaPruebas;

        public void Execute(IServiceProvider serviceProvider)
            => EjecucionDeCatalogosMasivos.Ejecutar(serviceProvider, "CargarCatalogos", RelojParaPruebas, (nucleo, archivo, plantilla, salidas) =>
            {
                if (plantilla)
                {
                    SalidasDeCarga(salidas, new ResultadoDeCargaMasiva(false, nucleo.Plantilla(EstructuraDeExcelDeCatalogo.Carga), 0, 0, 0, 0, 0, 0, 0));
                    return "plantilla entregada";
                }

                var r = nucleo.Cargar(archivo);
                SalidasDeCarga(salidas, r);
                return "grabado=" + r.Grabado.ToString() + ", filas=" + r.Filas.ToString(CultureInfo.InvariantCulture)
                    + ", errores=" + r.Errores.ToString(CultureInfo.InvariantCulture) + ", advertencias=" + r.Advertencias.ToString(CultureInfo.InvariantCulture);
            });

        private static void SalidasDeCarga(ParameterCollection salidas, ResultadoDeCargaMasiva r)
        {
            salidas[SalidaGrabado] = r.Grabado;
            salidas[SalidaResultado] = r.ResultadoBase64;
            salidas[SalidaFilas] = r.Filas;
            salidas[SalidaErrores] = r.Errores;
            salidas[SalidaAdvertencias] = r.Advertencias;
            salidas[SalidaClientesCreados] = r.ClientesCreados;
            salidas[SalidaPlanesCreados] = r.PlanesCreados;
            salidas[SalidaCorreosCreados] = r.CorreosCreados;
            salidas[SalidaAutorizacionesCreadas] = r.AutorizacionesCreadas;
        }
    }

    /// <summary>Envoltorio de sanic_mppp_capi_cambiarestadocatalogos. Mismo contrato que <see cref="CargarCatalogosApi"/>.</summary>
    public sealed class CambiarEstadoCatalogosApi : IPlugin
    {
        public const string SalidaActivados = "activados";
        public const string SalidaDesactivados = "desactivados";
        public const string SalidaSinCambios = "sincambios";

        /// <summary>Seam SOLO para pruebas: igual que <see cref="CargarCatalogosApi.RelojParaPruebas"/>.</summary>
        internal static Func<TimeSpan> RelojParaPruebas;

        public void Execute(IServiceProvider serviceProvider)
            => EjecucionDeCatalogosMasivos.Ejecutar(serviceProvider, "CambiarEstadoCatalogos", RelojParaPruebas, (nucleo, archivo, plantilla, salidas) =>
            {
                if (plantilla)
                {
                    SalidasDeCambio(salidas, new ResultadoDeCambioMasivo(false, nucleo.Plantilla(EstructuraDeExcelDeCatalogo.CambioDeEstado), 0, 0, 0, 0, 0, 0));
                    return "plantilla entregada";
                }

                var r = nucleo.CambiarEstado(archivo);
                SalidasDeCambio(salidas, r);
                return "grabado=" + r.Grabado.ToString() + ", filas=" + r.Filas.ToString(CultureInfo.InvariantCulture)
                    + ", errores=" + r.Errores.ToString(CultureInfo.InvariantCulture) + ", advertencias=" + r.Advertencias.ToString(CultureInfo.InvariantCulture);
            });

        private static void SalidasDeCambio(ParameterCollection salidas, ResultadoDeCambioMasivo r)
        {
            salidas[CargarCatalogosApi.SalidaGrabado] = r.Grabado;
            salidas[CargarCatalogosApi.SalidaResultado] = r.ResultadoBase64;
            salidas[CargarCatalogosApi.SalidaFilas] = r.Filas;
            salidas[CargarCatalogosApi.SalidaErrores] = r.Errores;
            salidas[CargarCatalogosApi.SalidaAdvertencias] = r.Advertencias;
            salidas[SalidaActivados] = r.Activados;
            salidas[SalidaDesactivados] = r.Desactivados;
            salidas[SalidaSinCambios] = r.SinCambios;
        }
    }

    /// <summary>
    /// Lo común a los dos envoltorios: proveedor de servicios, reloj del presupuesto (arranca al entrar), "hoy" en Nicaragua, los
    /// dos servicios (el del usuario que llama para los catálogos y SYSTEM para parámetros, usuarios y roles), MPPP-CAR-010 y la
    /// traducción de excepciones. Un <see cref="CargaMasivaRechazadaException"/> sale con su mensaje tal cual y con su detalle técnico
    /// en la traza; cualquier otra excepción va a la traza completa y sale con el mensaje genérico.
    /// </summary>
    internal static class EjecucionDeCatalogosMasivos
    {
        internal static void Ejecutar(IServiceProvider serviceProvider, string nombre, Func<TimeSpan> relojParaPruebas,
            Func<CatalogosMasivos, string, bool, ParameterCollection, string> trabajo)
        {
            // El presupuesto de tiempo corre desde que entra la llamada (diseno/03 §1b).
            var cronometro = Stopwatch.StartNew();

            if (serviceProvider == null)
                throw new InvalidPluginExecutionException("No se recibió el proveedor de servicios del plugin.");

            var contexto = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var fabrica = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var trace = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            if (contexto == null || fabrica == null || trace == null)
                throw new InvalidPluginExecutionException("Faltan servicios de la plataforma para ejecutar la Custom API.");

            try
            {
                var archivo = contexto.InputParameters.Contains(CargarCatalogosApi.ParametroArchivo)
                    ? contexto.InputParameters[CargarCatalogosApi.ParametroArchivo] as string
                    : null;
                var plantilla = contexto.InputParameters.Contains(CargarCatalogosApi.ParametroPlantilla)
                    && contexto.InputParameters[CargarCatalogosApi.ParametroPlantilla] is bool pedida && pedida;

                // Un archivo vacío no es un archivo: con plantilla = true y archivo en blanco se entrega la plantilla.
                if (plantilla && !string.IsNullOrWhiteSpace(archivo))
                {
                    throw new CargaMasivaRechazadaException("MPPP-CAR-010",
                        "Llegaron a la vez el pedido de la plantilla y un archivo, y no se puede adivinar cuál de las dos cosas querés. Pedí una sola por vez.");
                }

                trace.Trace("{0}", nombre + ": empieza. plantilla=" + plantilla.ToString() + ".");

                // Nicaragua es UTC-6 todo el año, sin horario de verano (DN-04): "hoy" es esa fecha, no la del servidor.
                var hoy = DateTime.SpecifyKind(DateTime.UtcNow.AddHours(-6).Date, DateTimeKind.Unspecified);
                var reloj = relojParaPruebas; // una sola lectura: evita una carrera si una prueba lo cambia a mitad de camino
                var nucleo = new CatalogosMasivos(
                    fabrica.CreateOrganizationService(contexto.UserId),
                    fabrica.CreateOrganizationService(null),
                    reloj ?? (() => cronometro.Elapsed),
                    hoy);

                var resumen = trabajo(nucleo, archivo, plantilla, contexto.OutputParameters);
                trace.Trace("{0}", nombre + ": termina. " + resumen + ".");
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (CargaMasivaRechazadaException ex)
            {
                // El texto es para el usuario; el detalle técnico (qué dijo el lector o Dataverse) queda para la traza.
                trace.Trace("{0}", nombre + ": rechazo " + ex.Codigo + (ex.InnerException == null ? "." : ". Detalle: " + ex.InnerException));
                throw new InvalidPluginExecutionException(ex.Message);
            }
            catch (Exception ex)
            {
                trace.Trace("{0}", ex.ToString());
                throw new InvalidPluginExecutionException(CargarCatalogosApi.MensajeDeErrorGenerico);
            }
        }
    }
}
