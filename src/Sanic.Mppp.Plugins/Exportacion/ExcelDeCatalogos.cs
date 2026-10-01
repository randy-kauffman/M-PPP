using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Xml;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Sanic.Mppp.Plugins.Catalogos;
using Sanic.Mppp.Plugins.Plantilla;

namespace Sanic.Mppp.Plugins.Exportacion
{
    // =====================================================================================================================
    // DN-04 (diseno/DIRECTRICES-NUEVAS.md, "Excel de resultado" y su precisión del 2026-10-01): la forma de los dos Excel de
    // catálogos y su armado. UNA sola fuente para la plantilla vacía, el archivo de resultado y la lectura al subir, así no
    // se pueden despegar. FIRMAS DEL ARQUITECTO. Pruebas: tests/.../Aceptacion/ExcelDeCatalogosAceptacion.cs.
    // =====================================================================================================================

    /// <summary>
    /// La forma de uno de los dos Excel: hoja y columnas, con <see cref="EncabezadoResultado"/> como ÚLTIMA columna (solo de
    /// salida: se exige su encabezado al leer, su contenido nunca se usa). Inmutable.
    /// </summary>
    public sealed class EstructuraDeExcelDeCatalogo
    {
        public const string EncabezadoResultado = "Resultado";

        private static readonly EstructuraDeExcelDeCatalogo EstructuraCarga =
            new EstructuraDeExcelDeCatalogo(ColumnasDeCarga.Hoja, ColumnasDeCarga.Todas);

        private static readonly EstructuraDeExcelDeCatalogo EstructuraCambio =
            new EstructuraDeExcelDeCatalogo(ColumnasDeCambioDeEstado.Hoja, ColumnasDeCambioDeEstado.Todas);

        private readonly ReadOnlyCollection<ColumnaDeCatalogo> _columnas;

        // La estructura se ARMA a partir de las columnas del dominio (una sola fuente, DN-04): las suyas, en su orden, y
        // Resultado en la letra que sigue a la última.
        private EstructuraDeExcelDeCatalogo(string hoja, IReadOnlyList<ColumnaDeCatalogo> columnasDeDominio)
        {
            Hoja = hoja;
            var todas = new List<ColumnaDeCatalogo>(columnasDeDominio);
            todas.Add(new ColumnaDeCatalogo(LetraDeColumna(todas.Count + 1), "resultado", EncabezadoResultado));
            _columnas = new ReadOnlyCollection<ColumnaDeCatalogo>(todas);
        }

        // Base 26 de Excel (A=1 … Z=26, AA=27), la misma aritmética que LectorOpenXml (LP-06).
        internal static string LetraDeColumna(int numero)
        {
            var letras = new System.Text.StringBuilder();
            while (numero > 0)
            {
                int resto = (numero - 1) % 26;
                letras.Insert(0, (char)('A' + resto));
                numero = (numero - 1) / 26;
            }

            return letras.ToString();
        }

        /// <summary>Excel 1: hoja <see cref="ColumnasDeCarga.Hoja"/>, columnas <see cref="ColumnasDeCarga.Todas"/> (A–K) y Resultado en L.</summary>
        public static EstructuraDeExcelDeCatalogo Carga => EstructuraCarga;

        /// <summary>Excel 2: hoja <see cref="ColumnasDeCambioDeEstado.Hoja"/>, columnas A–F y Resultado en G.</summary>
        public static EstructuraDeExcelDeCatalogo CambioDeEstado => EstructuraCambio;

        public string Hoja { get; }

        /// <summary>Todas, en orden, INCLUIDA la de Resultado al final (campo "resultado"). Solo lectura por tipo.</summary>
        public IReadOnlyList<ColumnaDeCatalogo> Columnas => _columnas;

        /// <summary>La última de <see cref="Columnas"/>.</summary>
        public ColumnaDeCatalogo ColumnaResultado => _columnas[_columnas.Count - 1];

        /// <summary>
        /// Una configuración NUEVA en cada llamada (quien la reciba puede mutarla sin afectar a la siguiente): hoja, encabezado en
        /// la fila 1, datos desde la 2, <paramref name="maximoFilas"/> filas, un campo por columna (incluida Resultado) con su
        /// encabezado exacto, y <see cref="ConfiguracionPlantilla.EstructuraEstricta"/> encendido. Menos de 1 →
        /// <see cref="ArgumentOutOfRangeException"/>.
        /// </summary>
        public ConfiguracionPlantilla ConfiguracionDeLectura(int maximoFilas)
        {
            if (maximoFilas < 1)
                throw new ArgumentOutOfRangeException(nameof(maximoFilas), "El máximo de filas tiene que ser 1 o mayor.");

            var campos = new List<CampoPlantilla>(_columnas.Count);
            foreach (ColumnaDeCatalogo columna in _columnas)
                campos.Add(new CampoPlantilla { Nombre = columna.Campo, Columna = columna.Letra, EncabezadoEsperado = columna.Encabezado });

            return new ConfiguracionPlantilla
            {
                Hoja = Hoja,
                FilaEncabezado = 1,
                PrimeraFila = 2,
                CantidadFilas = maximoFilas,
                Campos = campos,
                EstructuraEstricta = true
            };
        }
    }

    /// <summary>
    /// Arma los .xlsx con Open XML SDK (la misma librería que lee, como <see cref="ArmadorExportacionOpenXml"/>). Toda celda se
    /// escribe como TEXTO, nunca como fórmula ni número: "=SUMA(A1)" queda como texto y "000123" conserva sus ceros. Las celdas
    /// vacías o en blanco no se escriben. El archivo armado lo puede leer <see cref="LectorOpenXml"/> con
    /// <see cref="EstructuraDeExcelDeCatalogo.ConfiguracionDeLectura"/> sin rechazo.
    /// </summary>
    public static class ArmadorDeExcelDeCatalogos
    {
        /// <summary>Tope de texto de una celda de Excel (Learn, "Excel specifications and limits": 32.767 caracteres).</summary>
        public const int MaximoDeCaracteresPorCelda = 32767;

        /// <summary>La plantilla vacía: la hoja con la fila 1 de encabezados (incluido Resultado) y nada más.</summary>
        public static byte[] Plantilla(EstructuraDeExcelDeCatalogo estructura)
        {
            if (estructura == null) throw new ArgumentNullException(nameof(estructura));
            return ArmarXlsx(estructura, new List<KeyValuePair<int, string[]>>());
        }

        /// <summary>
        /// El archivo de resultado: la fila 1 de encabezados, y cada fila recibida EN SU MISMO número de fila de Excel (para que
        /// "fila 7" en un mensaje sea la fila 7 que ve el usuario), con sus valores por letra tal como se leyeron y, en la columna
        /// Resultado, el texto de <paramref name="resultadoPorFila"/> para ese número de fila (lo que la fila traía en esa
        /// columna se reemplaza). Las filas se escriben en orden ascendente aunque lleguen desordenadas.
        /// <see cref="ArgumentException"/>: una fila sin resultado, un resultado sin fila, dos filas con el mismo número, una
        /// fila con número menor que 2, o un texto de más de <see cref="MaximoDeCaracteresPorCelda"/>. Nulos →
        /// <see cref="ArgumentNullException"/>.
        /// </summary>
        public static byte[] Resultado(EstructuraDeExcelDeCatalogo estructura, IReadOnlyList<FilaPlantilla> filas, IReadOnlyDictionary<int, string> resultadoPorFila)
        {
            if (estructura == null) throw new ArgumentNullException(nameof(estructura));
            if (filas == null) throw new ArgumentNullException(nameof(filas));
            if (resultadoPorFila == null) throw new ArgumentNullException(nameof(resultadoPorFila));

            // TODO el contenido se valida y se resuelve a texto ANTES de tocar el SDK: un argumento malo es error de quien
            // llama, no un archivo a medio armar (mismo criterio que ArmadorExportacionOpenXml).
            IReadOnlyList<ColumnaDeCatalogo> columnas = estructura.Columnas;
            int posicionResultado = columnas.Count - 1;
            var indicePorLetra = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < columnas.Count; i++) indicePorLetra[columnas[i].Letra] = i;

            var filasResueltas = new List<KeyValuePair<int, string[]>>(filas.Count);
            var vistas = new HashSet<int>();
            for (int posicion = 0; posicion < filas.Count; posicion++)
            {
                FilaPlantilla fila = filas[posicion];
                string donde = "La fila en la posición " + (posicion + 1).ToString(CultureInfo.InvariantCulture);
                if (fila == null || fila.Valores == null)
                    throw new ArgumentException(donde + " no trae valores.", nameof(filas));

                int numero = fila.NumeroFilaExcel;
                if (numero < PrimeraFilaDeDatos || numero > UltimaFilaDeExcel)
                    throw new ArgumentException(donde + " tiene el número de fila " + numero.ToString(CultureInfo.InvariantCulture) + ": tiene que estar entre " + PrimeraFilaDeDatos.ToString(CultureInfo.InvariantCulture) + " (la 1 es el encabezado) y " + UltimaFilaDeExcel.ToString(CultureInfo.InvariantCulture) + ".", nameof(filas));

                if (!vistas.Add(numero))
                    throw new ArgumentException("Hay dos filas con el número de fila " + numero.ToString(CultureInfo.InvariantCulture) + ".", nameof(filas));

                if (!resultadoPorFila.TryGetValue(numero, out string resultado))
                    throw new ArgumentException("La fila " + numero.ToString(CultureInfo.InvariantCulture) + " no tiene resultado.", nameof(resultadoPorFila));

                var celdas = new string[columnas.Count];
                foreach (KeyValuePair<string, string> par in fila.Valores)
                {
                    // Una letra fuera de la estructura es un cableado mal hecho: descartarla sería perder un dato en silencio.
                    if (par.Key == null || !indicePorLetra.TryGetValue(par.Key, out int indice))
                        throw new ArgumentException("La fila " + numero.ToString(CultureInfo.InvariantCulture) + " trae la columna '" + par.Key + "', que no es de esta estructura.", nameof(filas));

                    // La columna Resultado de la fila se reemplaza, así que su contenido viejo no se valida ni se escribe.
                    if (indice == posicionResultado) continue;

                    ValidarTexto(par.Value, "La columna " + par.Key + " de la fila " + numero.ToString(CultureInfo.InvariantCulture), nameof(filas));
                    celdas[indice] = par.Value;
                }

                ValidarTexto(resultado, "El resultado de la fila " + numero.ToString(CultureInfo.InvariantCulture), nameof(resultadoPorFila));
                celdas[posicionResultado] = resultado;
                filasResueltas.Add(new KeyValuePair<int, string[]>(numero, celdas));
            }

            // Una clave sin fila: las filas ya son únicas y cada una encontró la suya, así que sobra si hay más claves que filas.
            if (resultadoPorFila.Count != filasResueltas.Count)
                throw new ArgumentException("Hay resultados para filas que no se recibieron.", nameof(resultadoPorFila));

            filasResueltas.Sort((a, b) => a.Key.CompareTo(b.Key));
            return ArmarXlsx(estructura, filasResueltas);
        }

        private const int PrimeraFilaDeDatos = 2;

        // Filas máximas de una hoja de Excel (Learn, "Excel specifications and limits").
        private const int UltimaFilaDeExcel = 1048576;

        private static void ValidarTexto(string texto, string donde, string parametro)
        {
            if (string.IsNullOrEmpty(texto)) return;

            if (texto.Length > MaximoDeCaracteresPorCelda)
                throw new ArgumentException(donde + " tiene " + texto.Length.ToString(CultureInfo.InvariantCulture) + " caracteres: el máximo de una celda es " + MaximoDeCaracteresPorCelda.ToString(CultureInfo.InvariantCulture) + ".", parametro);

            // XML 1.0 no admite ciertos caracteres de control ni sustitutos sueltos: el SDK fallaría recién al guardar, con el
            // archivo a medio armar. Se rechaza antes.
            for (int i = 0; i < texto.Length; i++)
            {
                char c = texto[i];
                if (XmlConvert.IsXmlChar(c)) continue;
                if (i + 1 < texto.Length && XmlConvert.IsXmlSurrogatePair(texto[i + 1], c)) { i++; continue; }
                throw new ArgumentException(donde + " trae un carácter que un archivo Excel no admite (posición " + i.ToString(CultureInfo.InvariantCulture) + ").", parametro);
            }
        }

        private static byte[] ArmarXlsx(EstructuraDeExcelDeCatalogo estructura, IList<KeyValuePair<int, string[]>> filas)
        {
            IReadOnlyList<ColumnaDeCatalogo> columnas = estructura.Columnas;
            using (var stream = new MemoryStream())
            {
                using (var documento = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
                {
                    WorkbookPart workbookPart = documento.AddWorkbookPart();
                    workbookPart.Workbook = new Workbook();

                    var sharedStringPart = workbookPart.AddNewPart<SharedStringTablePart>();
                    sharedStringPart.SharedStringTable = new SharedStringTable();
                    var indicesCompartidos = new Dictionary<string, int>(StringComparer.Ordinal);

                    var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                    var sheetData = new SheetData();
                    worksheetPart.Worksheet = new Worksheet(sheetData);

                    // Filas en orden ascendente y celdas de izquierda a derecha: LP-05 del lector rechaza lo contrario.
                    var encabezado = new Row { RowIndex = 1 };
                    foreach (ColumnaDeCatalogo columna in columnas)
                        encabezado.AppendChild(CeldaDeTexto(columna.Letra + "1", columna.Encabezado, sharedStringPart, indicesCompartidos));
                    sheetData.AppendChild(encabezado);

                    foreach (KeyValuePair<int, string[]> fila in filas)
                    {
                        string numero = fila.Key.ToString(CultureInfo.InvariantCulture);
                        var filaExcel = new Row { RowIndex = (uint)fila.Key };
                        for (int i = 0; i < columnas.Count; i++)
                        {
                            // Una celda vacía o en blanco no se escribe (el lector las trata igual que una ausente).
                            if (string.IsNullOrWhiteSpace(fila.Value[i])) continue;
                            filaExcel.AppendChild(CeldaDeTexto(columnas[i].Letra + numero, fila.Value[i], sharedStringPart, indicesCompartidos));
                        }

                        sheetData.AppendChild(filaExcel);
                    }

                    var sheets = workbookPart.Workbook.AppendChild(new Sheets());
                    sheets.AppendChild(new Sheet { Id = workbookPart.GetIdOfPart(worksheetPart), SheetId = 1, Name = estructura.Hoja });
                    workbookPart.Workbook.Save();
                }

                return stream.ToArray();
            }
        }

        // Texto compartido, NUNCA número ni fórmula (DN-04): "000123" conserva sus ceros y "=SUMA(A1)" queda como texto. Misma
        // razón y mismo patrón que ArmadorExportacionOpenXml.CeldaDeTexto, que ya corrió en el sandbox.
        private static Cell CeldaDeTexto(string referencia, string texto, SharedStringTablePart sharedStringPart, Dictionary<string, int> indicesCompartidos)
        {
            if (!indicesCompartidos.TryGetValue(texto, out int indice))
            {
                indice = indicesCompartidos.Count;
                indicesCompartidos[texto] = indice;
                var elemento = new Text(texto);
                if (char.IsWhiteSpace(texto[0]) || char.IsWhiteSpace(texto[texto.Length - 1]))
                    elemento.Space = SpaceProcessingModeValues.Preserve;
                sharedStringPart.SharedStringTable.AppendChild(new SharedStringItem(elemento));
            }

            return new Cell
            {
                CellReference = referencia,
                DataType = CellValues.SharedString,
                CellValue = new CellValue(indice.ToString(CultureInfo.InvariantCulture))
            };
        }
    }
}
