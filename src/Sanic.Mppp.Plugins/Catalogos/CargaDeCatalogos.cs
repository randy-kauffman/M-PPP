using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using Sanic.Mppp.Plugins.Dominio;
using Sanic.Mppp.Plugins.Plantilla;

namespace Sanic.Mppp.Plugins.Catalogos
{
    // =====================================================================================================================
    // DN-04 (diseno/DIRECTRICES-NUEVAS.md), Excel 1: carga masiva de Clientes, Planes, Correos y Autorizaciones por cliente.
    // Este archivo es el DOMINIO, puro: no conoce Dataverse. Recibe las filas ya leídas y una foto de lo que existe, y
    // devuelve qué le pasa a cada fila y qué hay que crear. La Custom API (03 §1c) arma la foto, llama a esto y escribe.
    // FIRMAS DEL ARQUITECTO: no se cambian sin consultarlo. Las pruebas de aceptación están en
    // tests/.../Aceptacion/CargaDeCatalogosAceptacion.cs.
    // =====================================================================================================================

    /// <summary>Una columna del Excel de catálogos: letra (clave en <see cref="FilaPlantilla.Valores"/>, como la guarda el lector), nombre de campo y encabezado.</summary>
    public sealed class ColumnaDeCatalogo
    {
        public ColumnaDeCatalogo(string letra, string campo, string encabezado)
        {
            Letra = letra;
            Campo = campo;
            Encabezado = encabezado;
        }

        public string Letra { get; }

        public string Campo { get; }

        /// <summary>Texto exacto del encabezado en la plantilla, y prefijo de todo motivo de error sobre esa columna.</summary>
        public string Encabezado { get; }
    }

    /// <summary>
    /// La estructura del Excel 1, FIJA en el código (DN-04: excepción a LP-08). Una sola fuente: de acá salen la
    /// configuración del lector, la plantilla vacía y el prefijo de cada mensaje. Las claves de
    /// <see cref="FilaPlantilla.Valores"/> son las LETRAS (A a K), igual que las deja <c>LectorOpenXml</c>.
    /// </summary>
    public static class ColumnasDeCarga
    {
        public const string Hoja = "CargaCatalogos";

        public const string CifBac = "cifBac";
        public const string CifCom = "cifCom";
        public const string RazonSocial = "razonSocial";
        public const string CorreoEjecutivo = "correoEjecutivo";
        public const string CodigoPlan = "codigoPlan";
        public const string TipoFormato = "tipoFormato";
        public const string Moneda = "moneda";
        public const string Correo = "correo";
        public const string NombreContacto = "nombreContacto";
        public const string FechaDocumento = "fechaDocumento";
        public const string Comentarios = "comentarios";

        private static readonly ReadOnlyCollection<ColumnaDeCatalogo> TodasLasColumnas = new ReadOnlyCollection<ColumnaDeCatalogo>(new[]
        {
            new ColumnaDeCatalogo("A", CifBac, "CIF BAC"),
            new ColumnaDeCatalogo("B", CifCom, "CIF COM"),
            new ColumnaDeCatalogo("C", RazonSocial, "Razon social"),
            new ColumnaDeCatalogo("D", CorreoEjecutivo, "Correo ejecutivo"),
            new ColumnaDeCatalogo("E", CodigoPlan, "Codigo plan"),
            new ColumnaDeCatalogo("F", TipoFormato, "Tipo formato"),
            new ColumnaDeCatalogo("G", Moneda, "Moneda"),
            new ColumnaDeCatalogo("H", Correo, "Correo"),
            new ColumnaDeCatalogo("I", NombreContacto, "Nombre contacto"),
            new ColumnaDeCatalogo("J", FechaDocumento, "Fecha documento"),
            new ColumnaDeCatalogo("K", Comentarios, "Comentarios"),
        });

        /// <summary>Las 11 columnas, A a K, en orden (DN-04 §"Excel 1"). Lista de solo lectura por tipo.</summary>
        public static IReadOnlyList<ColumnaDeCatalogo> Todas => TodasLasColumnas;

        internal static string LetraDe(string campo) => Buscar(campo).Letra;

        internal static string EncabezadoDe(string campo) => Buscar(campo).Encabezado;

        private static ColumnaDeCatalogo Buscar(string campo)
        {
            foreach (var columna in TodasLasColumnas)
            {
                if (string.Equals(columna.Campo, campo, StringComparison.Ordinal)) return columna;
            }

            throw new ArgumentException("No hay una columna de carga llamada '" + campo + "'.", nameof(campo));
        }
    }

    // --------------------------------------------------------------------------------------------- la foto de lo que existe

    public sealed class ClienteExistente
    {
        public ClienteExistente(Guid id, string cifBac, string cifCom, string razonSocial, Guid duenoId, bool activo)
        {
            Id = id; CifBac = cifBac; CifCom = cifCom; RazonSocial = razonSocial; DuenoId = duenoId; Activo = activo;
        }

        public Guid Id { get; }
        /// <summary>Ya normalizado como lo guarda el step (9 dígitos).</summary>
        public string CifBac { get; }
        /// <summary>Ya normalizado como lo guarda el step (12 caracteres, mayúscula).</summary>
        public string CifCom { get; }
        public string RazonSocial { get; }
        public Guid DuenoId { get; }
        public bool Activo { get; }
    }

    public sealed class PlanExistente
    {
        public PlanExistente(Guid id, string codigo, Guid clienteId, TipoDeFormatoDelPlan formato, Moneda moneda, bool activo)
        {
            Id = id; Codigo = codigo; ClienteId = clienteId; Formato = formato; Moneda = moneda; Activo = activo;
        }

        public Guid Id { get; }
        /// <summary>Ya normalizado (4 caracteres, mayúscula).</summary>
        public string Codigo { get; }
        public Guid ClienteId { get; }
        public TipoDeFormatoDelPlan Formato { get; }
        public Moneda Moneda { get; }
        public bool Activo { get; }
    }

    public sealed class AutorizadoExistente
    {
        public AutorizadoExistente(Guid id, string correo, string nombreContacto, bool activo)
        {
            Id = id; Correo = correo; NombreContacto = nombreContacto; Activo = activo;
        }

        public Guid Id { get; }
        /// <summary>Ya normalizado (minúscula, sin espacios).</summary>
        public string Correo { get; }
        public string NombreContacto { get; }
        public bool Activo { get; }
    }

    public sealed class AutorizacionClienteExistente
    {
        public AutorizacionClienteExistente(Guid id, Guid autorizadoId, Guid clienteId, DateTime? fechaDocumento, string comentarios, bool activo)
        {
            Id = id; AutorizadoId = autorizadoId; ClienteId = clienteId; FechaDocumento = fechaDocumento; Comentarios = comentarios; Activo = activo;
        }

        public Guid Id { get; }
        public Guid AutorizadoId { get; }
        public Guid ClienteId { get; }
        /// <summary>Solo fecha (la hora se ignora al comparar).</summary>
        public DateTime? FechaDocumento { get; }
        public string Comentarios { get; }
        public bool Activo { get; }
    }

    /// <summary>Un usuario del entorno cuyo correo coincide con algún "Correo ejecutivo" del archivo.</summary>
    public sealed class UsuarioCandidato
    {
        public UsuarioCandidato(Guid id, string correo, bool habilitado, bool esEjecutivo)
        {
            Id = id; Correo = correo; Habilitado = habilitado; EsEjecutivo = esEjecutivo;
        }

        public Guid Id { get; }
        /// <summary>Tal como viene del usuario; se compara sin distinguir mayúsculas ni espacios a los lados.</summary>
        public string Correo { get; }
        public bool Habilitado { get; }
        /// <summary>Tiene el rol `SR - MPPP - Ejecutivo`.</summary>
        public bool EsEjecutivo { get; }
    }

    /// <summary>
    /// Lo que existe hoy, para las claves que trae el archivo. Copia las colecciones al construirse: quien la armó no
    /// puede cambiarla después. Ninguna colección puede ser nula ni traer elementos nulos (<see cref="ArgumentException"/>).
    /// <para>
    /// PRECONDICIÓN de quien arma la foto: los CIF BAC, CIF COM, códigos de plan y correos de autorizado llegan YA normalizados como
    /// los guarda el step (<c>Steps.Normalizacion</c>: CIF BAC de 9 dígitos, CIF COM de 12 en mayúscula, código de 4 en mayúscula,
    /// correo en minúscula y sin espacios). Cliente y plan se buscan por igualdad exacta: un valor sin normalizar nunca se encuentra,
    /// y el archivo daría "no existe" para algo que sí existe. Los correos de usuario sí se comparan sin distinguir mayúsculas.
    /// </para>
    /// </summary>
    public sealed class EstadoDeCatalogos
    {
        public EstadoDeCatalogos(
            IEnumerable<ClienteExistente> clientes,
            IEnumerable<PlanExistente> planes,
            IEnumerable<AutorizadoExistente> autorizados,
            IEnumerable<AutorizacionClienteExistente> autorizaciones,
            IEnumerable<UsuarioCandidato> usuarios)
        {
            Clientes = Copiar(clientes, nameof(clientes));
            Planes = Copiar(planes, nameof(planes));
            Autorizados = Copiar(autorizados, nameof(autorizados));
            Autorizaciones = Copiar(autorizaciones, nameof(autorizaciones));
            Usuarios = Copiar(usuarios, nameof(usuarios));
        }

        public IReadOnlyList<ClienteExistente> Clientes { get; }
        public IReadOnlyList<PlanExistente> Planes { get; }
        public IReadOnlyList<AutorizadoExistente> Autorizados { get; }
        public IReadOnlyList<AutorizacionClienteExistente> Autorizaciones { get; }
        public IReadOnlyList<UsuarioCandidato> Usuarios { get; }

        // Copia, y de solo lectura por tipo: quien armó la foto no puede cambiarla después, y quien la lee no puede cambiarla nunca.
        private static ReadOnlyCollection<T> Copiar<T>(IEnumerable<T> origen, string nombre) where T : class
        {
            if (origen == null)
                throw new ArgumentNullException(nombre, "La foto de catálogos no admite una colección nula.");

            var copia = new List<T>();
            foreach (var elemento in origen)
            {
                if (elemento == null)
                    throw new ArgumentException("La foto de catálogos no admite elementos nulos.", nombre);
                copia.Add(elemento);
            }

            return new ReadOnlyCollection<T>(copia);
        }
    }

    /// <summary>
    /// Las claves del archivo, YA NORMALIZADAS con las funciones de <c>Steps.Normalizacion</c>, para que la Custom API
    /// arme la foto con una consulta por tabla (condición In). Un valor con formato inválido no entra (va a ser error de
    /// su fila). Sin repetidos. Los correos de ejecutivo, en minúscula y sin espacios a los lados.
    /// </summary>
    public sealed class ClavesDeCarga
    {
        private ClavesDeCarga(IReadOnlyCollection<string> cifsBac, IReadOnlyCollection<string> cifsCom, IReadOnlyCollection<string> codigosDePlan,
            IReadOnlyCollection<string> correos, IReadOnlyCollection<string> correosDeEjecutivo)
        {
            CifsBac = cifsBac;
            CifsCom = cifsCom;
            CodigosDePlan = codigosDePlan;
            Correos = correos;
            CorreosDeEjecutivo = correosDeEjecutivo;
        }

        public IReadOnlyCollection<string> CifsBac { get; }
        public IReadOnlyCollection<string> CifsCom { get; }
        public IReadOnlyCollection<string> CodigosDePlan { get; }
        public IReadOnlyCollection<string> Correos { get; }
        public IReadOnlyCollection<string> CorreosDeEjecutivo { get; }

        public static ClavesDeCarga Extraer(IEnumerable<FilaPlantilla> filas)
        {
            if (filas == null) throw new ArgumentNullException(nameof(filas));

            var cifsBac = new ConjuntoOrdenado();
            var cifsCom = new ConjuntoOrdenado();
            var codigos = new ConjuntoOrdenado();
            var correos = new ConjuntoOrdenado();
            var ejecutivos = new ConjuntoOrdenado();

            var posicion = 0;
            foreach (var fila in filas)
            {
                if (fila == null)
                    throw new ArgumentException("Las filas no pueden traer elementos nulos.", nameof(filas));

                // Se lee con el mismo lector que valida: lo que ahí es inválido no sale como clave (y será error de su fila).
                // La fecha no importa para las claves: se le da un "hoy" que nada supera, para no depender del reloj.
                var leida = LectorDeFilaDeCarga.Leer(fila, posicion++, DateTime.MaxValue);
                if (leida.CifBac.Usable) cifsBac.Agregar(leida.CifBac.Valor);
                if (leida.CifCom.Usable) cifsCom.Agregar(leida.CifCom.Valor);
                if (leida.CodigoPlan.Usable) codigos.Agregar(leida.CodigoPlan.Valor);
                if (leida.Correo.Usable) correos.Agregar(leida.Correo.Valor);
                if (leida.CorreoEjecutivo.Usable) ejecutivos.Agregar(leida.CorreoEjecutivo.Valor);
            }

            return new ClavesDeCarga(cifsBac.ComoLista(), cifsCom.ComoLista(), codigos.ComoLista(), correos.ComoLista(), ejecutivos.ComoLista());
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

    // --------------------------------------------------------------------------------------------- el resultado

    /// <summary>
    /// A qué cliente apunta algo que se va a crear: uno que ya existe (<see cref="IdExistente"/>) o uno que crea este
    /// mismo archivo (<see cref="CifBacNuevo"/>, la clave del <see cref="ClienteACrear"/>). Exactamente uno de los dos.
    /// </summary>
    public sealed class ReferenciaACliente
    {
        public ReferenciaACliente(Guid? idExistente, string cifBacNuevo)
        {
            IdExistente = idExistente; CifBacNuevo = cifBacNuevo;
        }

        public Guid? IdExistente { get; }
        public string CifBacNuevo { get; }
    }

    /// <summary>Igual que <see cref="ReferenciaACliente"/>, para el correo: existente por id, o nuevo por su correo normalizado.</summary>
    public sealed class ReferenciaAAutorizado
    {
        public ReferenciaAAutorizado(Guid? idExistente, string correoNuevo)
        {
            IdExistente = idExistente; CorreoNuevo = correoNuevo;
        }

        public Guid? IdExistente { get; }
        public string CorreoNuevo { get; }
    }

    public sealed class ClienteACrear
    {
        public ClienteACrear(string cifBac, string cifCom, string razonSocial, Guid duenoId, int filaOrigen)
        {
            CifBac = cifBac; CifCom = cifCom; RazonSocial = razonSocial; DuenoId = duenoId; FilaOrigen = filaOrigen;
        }

        public string CifBac { get; }
        public string CifCom { get; }
        public string RazonSocial { get; }
        public Guid DuenoId { get; }
        /// <summary>Primera fila (menor número de fila de Excel) que lo nombra: la que dice "Creado: cliente".</summary>
        public int FilaOrigen { get; }
    }

    public sealed class PlanACrear
    {
        public PlanACrear(string codigo, TipoDeFormatoDelPlan formato, Moneda moneda, ReferenciaACliente cliente, int filaOrigen)
        {
            Codigo = codigo; Formato = formato; Moneda = moneda; Cliente = cliente; FilaOrigen = filaOrigen;
        }

        public string Codigo { get; }
        public TipoDeFormatoDelPlan Formato { get; }
        public Moneda Moneda { get; }
        public ReferenciaACliente Cliente { get; }
        public int FilaOrigen { get; }
    }

    public sealed class AutorizadoACrear
    {
        public AutorizadoACrear(string correo, string nombreContacto, int filaOrigen)
        {
            Correo = correo; NombreContacto = nombreContacto; FilaOrigen = filaOrigen;
        }

        public string Correo { get; }
        public string NombreContacto { get; }
        public int FilaOrigen { get; }
    }

    public sealed class AutorizacionACrear
    {
        public AutorizacionACrear(ReferenciaAAutorizado autorizado, ReferenciaACliente cliente, DateTime? fechaDocumento, string comentarios, int filaOrigen)
        {
            Autorizado = autorizado; Cliente = cliente; FechaDocumento = fechaDocumento; Comentarios = comentarios; FilaOrigen = filaOrigen;
        }

        public ReferenciaAAutorizado Autorizado { get; }
        public ReferenciaACliente Cliente { get; }
        /// <summary>Solo fecha: <see cref="DateTimeKind.Unspecified"/>, hora 00:00.</summary>
        public DateTime? FechaDocumento { get; }
        public string Comentarios { get; }
        public int FilaOrigen { get; }
    }

    /// <summary>Lo que le pasó a una fila del archivo. Es lo que va a la columna "Resultado" del Excel que se devuelve.</summary>
    public sealed class ResultadoDeFilaDeCarga
    {
        internal ResultadoDeFilaDeCarga(int numeroFilaExcel, List<string> errores, List<string> advertencias, List<string> creados, bool elArchivoTieneErrores)
        {
            NumeroFilaExcel = numeroFilaExcel;
            Errores = new ReadOnlyCollection<string>(errores);
            Advertencias = new ReadOnlyCollection<string>(advertencias);
            Creados = new ReadOnlyCollection<string>(creados);
            Texto = ArmarTexto(errores, advertencias, creados, elArchivoTieneErrores);
        }

        private static string ArmarTexto(List<string> errores, List<string> advertencias, List<string> creados, bool elArchivoTieneErrores)
        {
            var segmentos = new List<string>();
            foreach (var error in errores)
                segmentos.Add("Error: " + error);

            if (errores.Count == 0)
            {
                if (elArchivoTieneErrores)
                    segmentos.Add("Sin errores — no se grabó: el archivo tiene errores en otras filas");
                else if (creados.Count > 0)
                    segmentos.Add("Creado: " + string.Join(", ", creados));
                else
                    segmentos.Add("Sin cambios: todo ya existía o lo crea una fila anterior");
            }

            foreach (var advertencia in advertencias)
                segmentos.Add("Advertencia: " + advertencia);

            return string.Join(" | ", segmentos);
        }

        public int NumeroFilaExcel { get; }

        /// <summary>Cada uno empieza con "&lt;Encabezado&gt;: " (o "CIF BAC / CIF COM: " cuando son las dos), sin el prefijo "Error: ".</summary>
        public IReadOnlyList<string> Errores { get; }

        /// <summary>Mismo formato que <see cref="Errores"/>, sin el prefijo "Advertencia: ".</summary>
        public IReadOnlyList<string> Advertencias { get; }

        /// <summary>
        /// Lo que crea ESTA fila, en este orden y con estas palabras: "cliente", "plan", "correo", "autorización". Vacío
        /// si el archivo tiene errores (no se crea nada). Lo que aparece en varias filas lo crea la de menor número.
        /// </summary>
        public IReadOnlyList<string> Creados { get; }

        /// <summary>
        /// El texto de la columna "Resultado", segmentos unidos por " | ", en este orden:
        /// 1. cada error como "Error: &lt;motivo&gt;";
        /// 2. si la fila no tiene errores: "Creado: cliente, plan, …" (los de <see cref="Creados"/>), o
        ///    "Sin cambios: todo ya existía o lo crea una fila anterior" si no crea nada, o
        ///    "Sin errores — no se grabó: el archivo tiene errores en otras filas" si el ARCHIVO tiene errores;
        /// 3. cada advertencia como "Advertencia: &lt;motivo&gt;".
        /// </summary>
        public string Texto { get; }
    }

    public sealed class PlanDeCarga
    {
        internal PlanDeCarga(List<ResultadoDeFilaDeCarga> filas, bool tieneErrores, List<ClienteACrear> clientes, List<PlanACrear> planes,
            List<AutorizadoACrear> autorizados, List<AutorizacionACrear> autorizaciones)
        {
            Filas = new ReadOnlyCollection<ResultadoDeFilaDeCarga>(filas);
            TieneErrores = tieneErrores;
            Clientes = new ReadOnlyCollection<ClienteACrear>(clientes);
            Planes = new ReadOnlyCollection<PlanACrear>(planes);
            Autorizados = new ReadOnlyCollection<AutorizadoACrear>(autorizados);
            Autorizaciones = new ReadOnlyCollection<AutorizacionACrear>(autorizaciones);
        }

        /// <summary>Una por fila recibida, en el mismo orden en que se recibieron.</summary>
        public IReadOnlyList<ResultadoDeFilaDeCarga> Filas { get; }

        /// <summary>Alguna fila tiene al menos un error. Entonces las cuatro listas de creación están VACÍAS.</summary>
        public bool TieneErrores { get; }

        /// <summary>Orden de escritura (DN-04): Clientes, Planes, Autorizados, Autorizaciones. Cada entidad, una sola vez.</summary>
        public IReadOnlyList<ClienteACrear> Clientes { get; }
        public IReadOnlyList<PlanACrear> Planes { get; }
        public IReadOnlyList<AutorizadoACrear> Autorizados { get; }
        public IReadOnlyList<AutorizacionACrear> Autorizaciones { get; }
    }

    /// <summary>
    /// Pasada 1 de DN-04: valida todo y decide qué crear, sin escribir nada. Reglas por fila 1 a 16 de DN-04 §"Excel 1".
    /// Lineal en la cantidad de filas (nada de buscar con un recorrido por cada fila).
    /// </summary>
    public static class PlanificadorDeCarga
    {
        /// <param name="filas">Las filas no vacías que devolvió el lector, con <see cref="FilaPlantilla.Valores"/> por LETRA
        /// de columna (A a K, <see cref="ColumnasDeCarga.Todas"/>). Una clave que falta, o un valor nulo, equivale a celda vacía.
        /// <see cref="FilaPlantilla.Valores"/> nulo equivale a fila sin valores. Nula, o con un elemento nulo, o con
        /// dos filas con el mismo número de fila de Excel → <see cref="ArgumentException"/> (es un error de quien llama).</param>
        /// <param name="estado">La foto de lo que existe. Nula → <see cref="ArgumentNullException"/>.</param>
        /// <param name="hoy">La fecha de hoy (solo fecha) en la hora del negocio: una "Fecha documento" posterior es error.</param>
        /// <summary>
        /// Como la otra sobrecarga, más los avisos del lector (revisión 4c, 2026-10-01; LP-07 "nada se pierde callado"): cada
        /// <see cref="AdvertenciaLectura"/> (un error de Excel como #REF!, un número que pudo redondearse) es un ERROR de su fila,
        /// con el encabezado de su columna como prefijo y el motivo del lector. En un catálogo eso nunca es un dato válido.
        /// <paramref name="avisos"/> nulo → <see cref="ArgumentNullException"/>; un aviso nulo, de una fila que no está en
        /// <paramref name="filas"/> o de una columna que no es de la estructura → <see cref="ArgumentException"/>.
        /// FIRMA DEL ARQUITECTO.
        /// </summary>
        public static PlanDeCarga Planificar(IReadOnlyList<FilaPlantilla> filas, EstadoDeCatalogos estado, DateTime hoy, IReadOnlyList<AdvertenciaLectura> avisos)
        {
            if (filas == null) throw new ArgumentNullException(nameof(filas));
            if (estado == null) throw new ArgumentNullException(nameof(estado));
            if (avisos == null) throw new ArgumentNullException(nameof(avisos));

            // Se lee todo antes de planificar: de acá en adelante no se vuelve a tocar nada de lo que entregó quien llamó.
            var leidas = new List<FilaDeTrabajo>(filas.Count);
            var numerosVistos = new HashSet<int>();
            foreach (var fila in filas)
            {
                if (fila == null)
                    throw new ArgumentException("Las filas no pueden traer elementos nulos.", nameof(filas));
                if (!numerosVistos.Add(fila.NumeroFilaExcel))
                    throw new ArgumentException("Hay dos filas con el mismo número de fila de Excel (" + fila.NumeroFilaExcel.ToString(CultureInfo.InvariantCulture) + ").", nameof(filas));

                leidas.Add(LectorDeFilaDeCarga.Leer(fila, leidas.Count, hoy));
            }

            AvisosDelLector.Aplicar(avisos, ColumnasDeCarga.Todas, leidas);
            return new Planificacion(leidas, estado).Ejecutar();
        }

        public static PlanDeCarga Planificar(IReadOnlyList<FilaPlantilla> filas, EstadoDeCatalogos estado, DateTime hoy)
            => Planificar(filas, estado, hoy, new AdvertenciaLectura[0]);
    }
}
