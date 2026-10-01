using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Sanic.Mppp.Plugins.Plantilla;

namespace Sanic.Mppp.Plugins.Catalogos
{
    // =====================================================================================================================
    // DN-04 (diseno/DIRECTRICES-NUEVAS.md), Excel 2: activar o desactivar Clientes, Planes, Correos y Autorizaciones por
    // cliente, registro por registro. DOMINIO puro, como CargaDeCatalogos.cs: filas leídas + foto → qué le pasa a cada fila
    // y qué estados cambian. Reutiliza la foto (EstadoDeCatalogos; acá no se usan los usuarios) y Steps.Normalizacion.
    // FIRMAS DEL ARQUITECTO: no se cambian sin consultarlo. Pruebas: tests/.../Aceptacion/CambioDeEstadoAceptacion.cs.
    // =====================================================================================================================

    /// <summary>La estructura del Excel 2, FIJA en el código (DN-04). Claves de <see cref="FilaPlantilla.Valores"/>: las LETRAS A a F.</summary>
    public static class ColumnasDeCambioDeEstado
    {
        public const string Hoja = "ActivarDesactivar";

        public const string Tipo = "tipo";
        public const string Accion = "accion";
        public const string CifBac = "cifBac";
        public const string CifCom = "cifCom";
        public const string CodigoPlan = "codigoPlan";
        public const string Correo = "correo";

        /// <summary>Las 6 columnas, A a F: Tipo, Accion, CIF BAC, CIF COM, Codigo plan, Correo. Lista de solo lectura por tipo.</summary>
        public static IReadOnlyList<ColumnaDeCatalogo> Todas => TodasLasColumnas;

        private static readonly ReadOnlyCollection<ColumnaDeCatalogo> TodasLasColumnas = new ReadOnlyCollection<ColumnaDeCatalogo>(new[]
        {
            new ColumnaDeCatalogo("A", Tipo, "Tipo"),
            new ColumnaDeCatalogo("B", Accion, "Accion"),
            new ColumnaDeCatalogo("C", CifBac, "CIF BAC"),
            new ColumnaDeCatalogo("D", CifCom, "CIF COM"),
            new ColumnaDeCatalogo("E", CodigoPlan, "Codigo plan"),
            new ColumnaDeCatalogo("F", Correo, "Correo"),
        });

        internal static string LetraDe(string campo) => Buscar(campo).Letra;

        internal static string EncabezadoDe(string campo) => Buscar(campo).Encabezado;

        private static ColumnaDeCatalogo Buscar(string campo)
        {
            foreach (var columna in TodasLasColumnas)
            {
                if (string.Equals(columna.Campo, campo, StringComparison.Ordinal)) return columna;
            }

            throw new ArgumentException("No hay una columna de cambio de estado llamada '" + campo + "'.", nameof(campo));
        }
    }

    /// <summary>Columna A. En el archivo: CLIENTE, PLAN, CORREO, AUTORIZACION_CLIENTE (sin distinguir mayúsculas ni espacios a los lados).</summary>
    public enum TipoDeCatalogo
    {
        Cliente = 1,
        Plan = 2,
        Correo = 3,
        AutorizacionCliente = 4,
    }

    /// <summary>Columna B. En el archivo: ACTIVAR, DESACTIVAR (sin distinguir mayúsculas ni espacios a los lados).</summary>
    public enum AccionDeEstado
    {
        Activar = 1,
        Desactivar = 2,
    }

    /// <summary>Un cambio de estado nativo (statecode) a aplicar en la pasada 2. Se aplica una vez por registro.</summary>
    public sealed class CambioDeEstadoACrear
    {
        public CambioDeEstadoACrear(TipoDeCatalogo tipo, Guid id, AccionDeEstado accion, int filaOrigen)
        {
            Tipo = tipo; Id = id; Accion = accion; FilaOrigen = filaOrigen;
        }

        public TipoDeCatalogo Tipo { get; }
        /// <summary>Id del registro existente (cliente, plan, autorizado o autorización por cliente, según <see cref="Tipo"/>).</summary>
        public Guid Id { get; }
        public AccionDeEstado Accion { get; }
        /// <summary>Primera fila (menor número de fila de Excel) que lo pide.</summary>
        public int FilaOrigen { get; }
    }

    /// <summary>Las claves del archivo, normalizadas, para armar la foto con una consulta por tabla. Sin repetidos ni inválidas.</summary>
    public sealed class ClavesDeCambioDeEstado
    {
        private ClavesDeCambioDeEstado(IReadOnlyCollection<string> cifsBac, IReadOnlyCollection<string> cifsCom, IReadOnlyCollection<string> codigosDePlan, IReadOnlyCollection<string> correos)
        {
            CifsBac = cifsBac;
            CifsCom = cifsCom;
            CodigosDePlan = codigosDePlan;
            Correos = correos;
        }

        public IReadOnlyCollection<string> CifsBac { get; }
        public IReadOnlyCollection<string> CifsCom { get; }
        public IReadOnlyCollection<string> CodigosDePlan { get; }
        public IReadOnlyCollection<string> Correos { get; }

        public static ClavesDeCambioDeEstado Extraer(IEnumerable<FilaPlantilla> filas)
        {
            if (filas == null) throw new ArgumentNullException(nameof(filas));

            var cifsBac = new ConjuntoOrdenado();
            var cifsCom = new ConjuntoOrdenado();
            var codigos = new ConjuntoOrdenado();
            var correos = new ConjuntoOrdenado();

            foreach (var fila in filas)
            {
                if (fila == null)
                    throw new ArgumentException("Las filas no pueden traer elementos nulos.", nameof(filas));

                // Se leen todas las celdas, sin mirar el tipo: la foto tiene que traer lo que el archivo nombre; una celda que el tipo no
                // usa igual da error en su fila, así que traer su clave de más no daña.
                var leida = LectorDeFilaDeCambio.LeerCeldas(fila, 0);
                if (leida.CifBac.Usable) cifsBac.Agregar(leida.CifBac.Valor);
                if (leida.CifCom.Usable) cifsCom.Agregar(leida.CifCom.Valor);
                if (leida.CodigoPlan.Usable) codigos.Agregar(leida.CodigoPlan.Valor);
                if (leida.Correo.Usable) correos.Agregar(leida.Correo.Valor);
            }

            return new ClavesDeCambioDeEstado(cifsBac.ComoLista(), cifsCom.ComoLista(), codigos.ComoLista(), correos.ComoLista());
        }

        /// <summary>Sin repetidos y en el orden en que aparecieron.</summary>
        private sealed class ConjuntoOrdenado
        {
            private readonly HashSet<string> _vistos = new HashSet<string>(StringComparer.Ordinal);
            private readonly List<string> _orden = new List<string>();

            public void Agregar(string valor)
            {
                if (_vistos.Add(valor)) _orden.Add(valor);
            }

            public IReadOnlyCollection<string> ComoLista() => new ReadOnlyCollection<string>(_orden);
        }
    }

    public sealed class ResultadoDeFilaDeCambio
    {
        internal ResultadoDeFilaDeCambio(int numeroFilaExcel, List<string> errores, List<string> advertencias, string accion, bool elArchivoTieneErrores)
        {
            NumeroFilaExcel = numeroFilaExcel;
            Errores = new ReadOnlyCollection<string>(errores);
            Advertencias = new ReadOnlyCollection<string>(advertencias);
            Texto = ArmarTexto(errores, advertencias, accion, elArchivoTieneErrores);
        }

        private static string ArmarTexto(List<string> errores, List<string> advertencias, string accion, bool elArchivoTieneErrores)
        {
            var segmentos = new List<string>();
            foreach (var error in errores)
                segmentos.Add("Error: " + error);

            if (errores.Count == 0)
                segmentos.Add(elArchivoTieneErrores ? "Sin errores — no se grabó: el archivo tiene errores en otras filas" : accion);

            foreach (var advertencia in advertencias)
                segmentos.Add("Advertencia: " + advertencia);

            return string.Join(" | ", segmentos);
        }

        public int NumeroFilaExcel { get; }

        /// <summary>Cada uno empieza con "&lt;Encabezado&gt;: " de la columna culpable, sin el prefijo "Error: ".</summary>
        public IReadOnlyList<string> Errores { get; }

        /// <summary>Mismo formato, sin el prefijo "Advertencia: ".</summary>
        public IReadOnlyList<string> Advertencias { get; }

        /// <summary>
        /// La columna "Resultado", segmentos unidos por " | ", en este orden:
        /// 1. cada error como "Error: &lt;motivo&gt;";
        /// 2. si la fila no tiene errores: "Activado" o "Desactivado" (la fila que lo aplica), o "Sin cambios: ya estaba activo",
        ///    "Sin cambios: ya estaba inactivo", "Sin cambios: lo pide una fila anterior" (misma acción repetida), o
        ///    "Sin errores — no se grabó: el archivo tiene errores en otras filas" si el ARCHIVO tiene errores;
        /// 3. cada advertencia como "Advertencia: &lt;motivo&gt;".
        /// </summary>
        public string Texto { get; }
    }

    public sealed class PlanDeCambioDeEstado
    {
        internal PlanDeCambioDeEstado(List<ResultadoDeFilaDeCambio> filas, bool tieneErrores, List<CambioDeEstadoACrear> cambios, int sinCambios)
        {
            Filas = new ReadOnlyCollection<ResultadoDeFilaDeCambio>(filas);
            TieneErrores = tieneErrores;
            Cambios = new ReadOnlyCollection<CambioDeEstadoACrear>(cambios);
            foreach (var cambio in cambios)
            {
                if (cambio.Accion == AccionDeEstado.Activar) Activados++;
                else Desactivados++;
            }

            SinCambios = sinCambios;
        }

        /// <summary>Una por fila recibida, en el orden recibido.</summary>
        public IReadOnlyList<ResultadoDeFilaDeCambio> Filas { get; }

        /// <summary>Alguna fila tiene error. Entonces <see cref="Cambios"/> está VACÍA y los tres contadores valen 0.</summary>
        public bool TieneErrores { get; }

        /// <summary>Solo lo que cambia de verdad (lo que ya estaba en el estado pedido no entra). Un registro, una vez.</summary>
        public IReadOnlyList<CambioDeEstadoACrear> Cambios { get; }

        public int Activados { get; }
        public int Desactivados { get; }
        /// <summary>Filas sin errores que no cambian nada (ya estaba, o repetida).</summary>
        public int SinCambios { get; }
    }

    /// <summary>
    /// Pasada 1 del Excel 2 (DN-04 §"Excel 2", reglas 1 a 7): valida todo y decide qué estados cambian, sin escribir.
    /// Las advertencias de efecto (regla 6) se calculan sobre el estado FINAL, con todo el archivo aplicado. Lineal.
    /// </summary>
    public static class PlanificadorDeCambioDeEstado
    {
        /// <param name="filas">Filas no vacías del lector, por LETRA (A a F). Nula, con un elemento nulo o con números de
        /// fila repetidos → <see cref="ArgumentException"/>. Clave ausente, valor nulo o en blanco = celda vacía.</param>
        /// <param name="estado">La foto (normalizada como la guarda el step). Debe traer las autorizaciones por cliente de
        /// los clientes y correos nombrados, para contar el efecto de desactivarlos. Nula → <see cref="ArgumentNullException"/>.</param>
        /// <summary>Como la otra sobrecarga, más los avisos del lector: cada uno es un ERROR de su fila (mismo contrato que
        /// <see cref="PlanificadorDeCarga.Planificar(IReadOnlyList{FilaPlantilla}, EstadoDeCatalogos, DateTime, IReadOnlyList{AdvertenciaLectura})"/>).
        /// FIRMA DEL ARQUITECTO.</summary>
        public static PlanDeCambioDeEstado Planificar(IReadOnlyList<FilaPlantilla> filas, EstadoDeCatalogos estado, IReadOnlyList<AdvertenciaLectura> avisos)
            => throw new NotImplementedException();

        public static PlanDeCambioDeEstado Planificar(IReadOnlyList<FilaPlantilla> filas, EstadoDeCatalogos estado)
        {
            if (filas == null) throw new ArgumentNullException(nameof(filas));
            if (estado == null) throw new ArgumentNullException(nameof(estado));

            // Se lee todo antes de planificar: de acá en adelante no se vuelve a tocar nada de lo que entregó quien llamó.
            var leidas = new List<FilaDeCambio>(filas.Count);
            var numerosVistos = new HashSet<int>();
            foreach (var fila in filas)
            {
                if (fila == null)
                    throw new ArgumentException("Las filas no pueden traer elementos nulos.", nameof(filas));
                if (!numerosVistos.Add(fila.NumeroFilaExcel))
                    throw new ArgumentException("Hay dos filas con el mismo número de fila de Excel (" + fila.NumeroFilaExcel.ToString(System.Globalization.CultureInfo.InvariantCulture) + ").", nameof(filas));

                leidas.Add(LectorDeFilaDeCambio.Leer(fila, leidas.Count));
            }

            return new PlanificacionDeCambio(leidas, estado).Ejecutar();
        }
    }
}
