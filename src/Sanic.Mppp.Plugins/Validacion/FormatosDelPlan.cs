using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Sanic.Mppp.Plugins.Dominio;

namespace Sanic.Mppp.Plugins.Validacion
{
    /// <summary>
    /// DN-05 (aprobador, 2026-10-06): qué admite cada tipo de formato de plan, leído del parámetro `plan.formatos`. Lo leen
    /// tres reglas de registro: `CLASIFICACION_DEL_FORMATO`, `GESTION_DEL_FORMATO` y `BANCO_DEL_FORMATO`. Inmutable.
    /// FIRMA DEL ARQUITECTO.
    /// </summary>
    /// <remarks>
    /// Forma del JSON (las tres claves de formato, exactas: "06", "10" y "11"):
    /// <code>
    /// {
    ///   "06": {"clasificaciones": ["BAC"],      "gestiones": ["Inclusion","Exclusion","Modificacion"], "bancos": ["BAC"]},
    ///   "10": {"clasificaciones": ["BAC","CK"], "gestiones": ["Inclusion","Exclusion","Modificacion"], "bancos": ["BAC"]},
    ///   "11": {"clasificaciones": ["ACH"],      "gestiones": ["Inclusion","Exclusion"]}
    /// }
    /// </code>
    /// <see cref="FormatException"/> (parámetro mal cargado; ante la duda se falla cerrado, nunca se adivina):
    /// - JSON nulo, vacío, en blanco o inválido; la raíz no es un objeto.
    /// - Falta alguno de los tres formatos, sobra una clave que no es "06", "10" ni "11", o un formato aparece dos veces.
    /// - La entrada de un formato no es un objeto.
    /// - Falta `clasificaciones` o `gestiones`, o viene vacía, o no es un arreglo de textos.
    /// - `bancos` viene presente pero vacío, o no es un arreglo de textos. AUSENTE (o JSON `null`) = cualquier banco.
    /// - Un valor que no es el nombre de un miembro de <see cref="Clasificacion"/>, <see cref="Gestion"/> o
    ///   <see cref="Banco"/>. Se compara sin distinguir caja ni tildes (<see cref="ListasPlantilla.Normalizar"/>), igual
    ///   que `porGestion` (DN-03). Un valor repetido dentro de la misma lista también es error.
    /// </remarks>
    public sealed class FormatosDelPlan
    {
        /// <summary>`sanic_nombre` del parámetro en `sanic_mppp_tbl_parametro`.</summary>
        public const string NombreParametro = "plan.formatos";

        private const string Prefijo = "El parámetro 'plan.formatos' no es válido";

        /// <summary>Las tres claves de formato, exactas, y el miembro al que corresponde cada una.</summary>
        private static readonly KeyValuePair<string, TipoDeFormatoDelPlan>[] Formatos =
        {
            new KeyValuePair<string, TipoDeFormatoDelPlan>("06", TipoDeFormatoDelPlan._06),
            new KeyValuePair<string, TipoDeFormatoDelPlan>("10", TipoDeFormatoDelPlan._10),
            new KeyValuePair<string, TipoDeFormatoDelPlan>("11", TipoDeFormatoDelPlan._11),
        };

        private readonly IDictionary<TipoDeFormatoDelPlan, ReglasDelFormato> _porFormato;

        private FormatosDelPlan(IDictionary<TipoDeFormatoDelPlan, ReglasDelFormato> porFormato)
        {
            _porFormato = porFormato;
        }

        public static FormatosDelPlan DesdeJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new FormatException($"{Prefijo}: está vacío.");
            }

            var documento = AnalizadorJson.LeerEstricto(json, Prefijo);
            if (!(documento is IDictionary<string, object> raiz))
            {
                throw new FormatException($"{Prefijo}: la raíz tiene que ser un objeto con los formatos '06', '10' y '11'.");
            }

            // El parser ya rechazó las claves repetidas. Acá: ni una de menos ni una de más, y por su texto EXACTO ("6" no es "06").
            foreach (var clave in raiz.Keys)
            {
                if (!Array.Exists(Formatos, f => string.Equals(f.Key, clave, StringComparison.Ordinal)))
                {
                    throw new FormatException($"{Prefijo}: trae un formato desconocido ('{clave}'); solo existen '06', '10' y '11'.");
                }
            }

            var porFormato = new Dictionary<TipoDeFormatoDelPlan, ReglasDelFormato>();
            foreach (var formato in Formatos)
            {
                if (!raiz.TryGetValue(formato.Key, out var entrada))
                {
                    throw new FormatException($"{Prefijo}: falta el formato '{formato.Key}'.");
                }

                porFormato[formato.Value] = LeerFormato(formato.Key, entrada);
            }

            return new FormatosDelPlan(porFormato);
        }

        /// <summary>Lo que admite ese formato. Nunca nulo: los tres formatos son obligatorios en el parámetro.</summary>
        public ReglasDelFormato Para(TipoDeFormatoDelPlan formato)
        {
            if (!_porFormato.TryGetValue(formato, out var reglas))
            {
                throw new ArgumentOutOfRangeException(nameof(formato), formato, "El formato no es uno de los de plan.formatos ('06', '10' y '11').");
            }

            return reglas;
        }

        private static ReglasDelFormato LeerFormato(string formato, object entrada)
        {
            if (!(entrada is IDictionary<string, object> objeto))
            {
                throw new FormatException($"{Prefijo}: la entrada del formato '{formato}' no es un objeto.");
            }

            // Una clave que no es de las tres se rechaza: `banco` en vez de `bancos` NO puede leerse como "cualquier banco"
            // (aflojar en silencio lo que el negocio creyó restringir).
            foreach (var clave in objeto.Keys)
            {
                if (clave != "clasificaciones" && clave != "gestiones" && clave != "bancos")
                {
                    throw new FormatException($"{Prefijo}: el formato '{formato}' trae una clave desconocida ('{clave}'); solo existen 'clasificaciones', 'gestiones' y 'bancos'.");
                }
            }

            var clasificaciones = LeerLista<Clasificacion>(formato, objeto, "clasificaciones", obligatoria: true);
            var gestiones = LeerLista<Gestion>(formato, objeto, "gestiones", obligatoria: true);
            var bancos = LeerLista<Banco>(formato, objeto, "bancos", obligatoria: false);
            return new ReglasDelFormato(clasificaciones, gestiones, bancos);
        }

        /// <summary>Una lista de nombres de miembros de <typeparamref name="T"/>, en el orden del parámetro. Ausente o `null`:
        /// error si es obligatoria, nulo si no. Presente: no vacía, solo textos, cada uno un miembro, ninguno repetido.</summary>
        private static List<T> LeerLista<T>(string formato, IDictionary<string, object> entrada, string clave, bool obligatoria) where T : struct
        {
            if (!entrada.TryGetValue(clave, out var crudo) || crudo == null)
            {
                if (obligatoria)
                {
                    throw new FormatException($"{Prefijo}: al formato '{formato}' le falta '{clave}'.");
                }

                return null;
            }

            if (!(crudo is IList<object> lista))
            {
                throw new FormatException($"{Prefijo}: '{clave}' del formato '{formato}' no es un arreglo.");
            }

            if (lista.Count == 0)
            {
                throw new FormatException($"{Prefijo}: '{clave}' del formato '{formato}' está vacío.");
            }

            var resultado = new List<T>(lista.Count);
            foreach (var elemento in lista)
            {
                if (!(elemento is string texto))
                {
                    throw new FormatException($"{Prefijo}: '{clave}' del formato '{formato}' trae un elemento que no es un texto.");
                }

                if (!EnumPorNombre<T>.TryResolver(texto, out var miembro))
                {
                    throw new FormatException($"{Prefijo}: '{clave}' del formato '{formato}' trae un valor desconocido ('{texto}').");
                }

                if (resultado.Contains(miembro))
                {
                    throw new FormatException($"{Prefijo}: '{clave}' del formato '{formato}' repite el valor '{miembro}'.");
                }

                resultado.Add(miembro);
            }

            return resultado;
        }
    }

    /// <summary>Lo que admite UN formato de plan (DN-05). Inmutable: las listas se exponen como copias de solo lectura.
    /// FIRMA DEL ARQUITECTO.</summary>
    public sealed class ReglasDelFormato
    {
        private readonly ReadOnlyCollection<Clasificacion> _clasificaciones;
        private readonly ReadOnlyCollection<Gestion> _gestiones;
        private readonly ReadOnlyCollection<Banco> _bancos;

        /// <summary>Copia las listas que recibe: quien las armó no puede mutarlas después. Interno: solo lo arma
        /// <see cref="FormatosDelPlan.DesdeJson"/>, que ya las validó.</summary>
        internal ReglasDelFormato(IList<Clasificacion> clasificaciones, IList<Gestion> gestiones, IList<Banco> bancos)
        {
            _clasificaciones = new ReadOnlyCollection<Clasificacion>(new List<Clasificacion>(clasificaciones));
            _gestiones = new ReadOnlyCollection<Gestion>(new List<Gestion>(gestiones));
            _bancos = bancos == null ? null : new ReadOnlyCollection<Banco>(new List<Banco>(bancos));
        }

        /// <summary>Las clasificaciones admitidas, en el orden del parámetro. Nunca vacía.</summary>
        public IReadOnlyList<Clasificacion> Clasificaciones => _clasificaciones;

        /// <summary>Las gestiones admitidas, en el orden del parámetro. Nunca vacía.</summary>
        public IReadOnlyList<Gestion> Gestiones => _gestiones;

        /// <summary>Los bancos admitidos, en el orden del parámetro. NULO = cualquier banco (la clave no vino).</summary>
        public IReadOnlyList<Banco> Bancos => _bancos;
    }
}
