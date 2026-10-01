using System.Collections.Generic;

namespace Sanic.Mppp.Plugins.Plantilla
{
    /// <summary>Una fila de datos de la ventana que trae al menos un valor. Las filas totalmente vacías no se devuelven.</summary>
    public sealed class FilaPlantilla
    {
        /// <summary>Número de fila en el Excel (la 12, la 13…).</summary>
        public int NumeroFilaExcel { get; set; }

        /// <summary>Posición dentro de la ventana, empezando en 1: la primera fila de datos configurada es la 1, tenga o no valores.</summary>
        public int NumeroOrden { get; set; }

        /// <summary>Valor de cada campo configurado, por LETRA DE COLUMNA. Un campo sin celda trae texto vacío.</summary>
        public IDictionary<string, string> Valores { get; set; }
    }

    /// <summary>Algo que el lector leyó pero no pudo interpretar con seguridad (LP-07: nada se pierde callado).</summary>
    public sealed class AdvertenciaLectura
    {
        public int NumeroFilaExcel { get; set; }

        public string Columna { get; set; }

        public string Motivo { get; set; }
    }

    /// <summary>
    /// Por qué el lector rechazó el archivo (DN-04: la Custom API de catálogos lo traduce a su código MPPP-CAR sin mirar el texto).
    /// FIRMA DEL ARQUITECTO.
    /// </summary>
    public enum TipoDeRechazoLectura
    {
        /// <summary>El archivo es válido.</summary>
        Ninguno = 0,
        /// <summary>Rechazo armado con las fábricas sin tipo (<see cref="ResultadoLecturaPlantilla.ConError(string)"/>,
        /// <see cref="ResultadoLecturaPlantilla.ConErrores(IList{string})"/>). <c>LectorOpenXml</c> NUNCA devuelve este valor.</summary>
        SinClasificar,
        /// <summary>No se pudo abrir o recorrer como .xlsx (LP-01), o le falta el libro o la hoja de cálculo.</summary>
        ArchivoIlegible,
        /// <summary>Supera el tope de bytes de entrada (LP-02) o descomprimidos (LP-03).</summary>
        PesoExcedido,
        /// <summary>No existe la hoja configurada.</summary>
        HojaInexistente,
        /// <summary>Falta la fila de encabezado, o algún encabezado no es el esperado.</summary>
        Encabezado,
        /// <summary>Filas o celdas que retroceden o se repiten (LP-05).</summary>
        Desorden,
        /// <summary>Una celda que se interpreta no se puede leer (texto compartido inexistente, booleano inválido…).</summary>
        CeldaIlegible,
        /// <summary>Solo con <see cref="ConfiguracionPlantilla.EstructuraEstricta"/>: un valor en una columna posterior a la última configurada.</summary>
        ColumnaDeMas,
        /// <summary>Solo con <see cref="ConfiguracionPlantilla.EstructuraEstricta"/>: un valor en una fila posterior a la ventana.</summary>
        FilaDeMas,
    }

    public sealed class ResultadoLecturaPlantilla
    {
        private ResultadoLecturaPlantilla(bool esValido, IList<string> errores, IList<FilaPlantilla> filas, IList<AdvertenciaLectura> advertencias, TipoDeRechazoLectura rechazo, string detalleTecnico = null)
        {
            EsValido = esValido;
            Rechazo = rechazo;
            Errores = errores;
            Filas = filas;
            Advertencias = advertencias;
            DetalleTecnico = detalleTecnico;
        }

        public bool EsValido { get; }

        /// <summary><see cref="TipoDeRechazoLectura.Ninguno"/> si es válido. FIRMA DEL ARQUITECTO.</summary>
        public TipoDeRechazoLectura Rechazo { get; }

        /// <summary>Motivos por los que el archivo es inválido, en texto para el cliente. Vacío si es válido.</summary>
        public IList<string> Errores { get; }

        public IList<FilaPlantilla> Filas { get; }

        public IList<AdvertenciaLectura> Advertencias { get; }

        /// <summary>
        /// Lo que dijo la librería que lee el Excel cuando no pudo abrirlo. Es para la traza del plugin (ITracingService), NUNCA para el cliente:
        /// <see cref="Errores"/> termina en un correo a una persona de fuera del banco y no lleva jerga de un SDK de terceros. `null` si no aplica.
        /// </summary>
        public string DetalleTecnico { get; }

        public static ResultadoLecturaPlantilla ConFilas(IList<FilaPlantilla> filas, IList<AdvertenciaLectura> advertencias)
        {
            return new ResultadoLecturaPlantilla(true, new List<string>(), filas, advertencias, TipoDeRechazoLectura.Ninguno);
        }

        public static ResultadoLecturaPlantilla ConError(string motivo)
        {
            return new ResultadoLecturaPlantilla(false, new List<string> { motivo }, new List<FilaPlantilla>(), new List<AdvertenciaLectura>(), TipoDeRechazoLectura.SinClasificar);
        }

        /// <summary>El archivo no se pudo abrir o recorrer: al cliente, un texto nuestro; a la traza, lo que dijo la librería.</summary>
        public static ResultadoLecturaPlantilla ConErrorTecnico(string motivoParaElCliente, string detalleTecnico)
        {
            return new ResultadoLecturaPlantilla(false, new List<string> { motivoParaElCliente }, new List<FilaPlantilla>(), new List<AdvertenciaLectura>(), TipoDeRechazoLectura.ArchivoIlegible, detalleTecnico);
        }

        /// <summary>Rechazo con su tipo. FIRMA DEL ARQUITECTO.</summary>
        public static ResultadoLecturaPlantilla ConError(TipoDeRechazoLectura rechazo, string motivo)
        {
            ExigirTipoDeRechazo(rechazo);
            return new ResultadoLecturaPlantilla(false, new List<string> { motivo }, new List<FilaPlantilla>(), new List<AdvertenciaLectura>(), rechazo);
        }

        /// <summary>Rechazo con su tipo y varios motivos. FIRMA DEL ARQUITECTO.</summary>
        public static ResultadoLecturaPlantilla ConErrores(TipoDeRechazoLectura rechazo, IList<string> motivos)
        {
            ExigirTipoDeRechazo(rechazo);
            return new ResultadoLecturaPlantilla(false, motivos, new List<FilaPlantilla>(), new List<AdvertenciaLectura>(), rechazo);
        }

        // Un resultado inválido con tipo Ninguno sería contradictorio (EsValido = false pero "no hay rechazo"): es un error de programación.
        private static void ExigirTipoDeRechazo(TipoDeRechazoLectura rechazo)
        {
            if (rechazo == TipoDeRechazoLectura.Ninguno)
                throw new System.ArgumentException("Un rechazo no puede tener el tipo Ninguno: ese valor es solo del archivo válido.", nameof(rechazo));
        }

        public static ResultadoLecturaPlantilla ConErrores(IList<string> motivos)
        {
            return new ResultadoLecturaPlantilla(false, motivos, new List<FilaPlantilla>(), new List<AdvertenciaLectura>(), TipoDeRechazoLectura.SinClasificar);
        }
    }
}
