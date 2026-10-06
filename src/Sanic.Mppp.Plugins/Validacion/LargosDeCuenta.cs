using System;
using System.Collections.Generic;
using Sanic.Mppp.Plugins.Dominio;

namespace Sanic.Mppp.Plugins.Validacion
{
    /// <summary>
    /// DN-05 (aprobador, 2026-10-06, escenario 20): largo válido del número de cuenta según el banco, leído del parámetro
    /// `cuenta.largos`. Lo lee la regla `CUENTA_DEL_BANCO`. Inmutable. FIRMA DEL ARQUITECTO.
    /// </summary>
    /// <remarks>
    /// Forma del JSON:
    /// <code>
    /// {
    ///   "porDefecto": {"minimo": 6, "maximo": 17},
    ///   "porBanco": {"BANPRO": {"minimo": 6, "maximo": 16}, "BAC": {"minimo": 9, "maximo": 16}}
    /// }
    /// </code>
    /// <see cref="FormatException"/> (parámetro mal cargado):
    /// - JSON nulo, vacío, en blanco o inválido; la raíz no es un objeto.
    /// - Falta `porDefecto`. (`porBanco` puede faltar o ser JSON `null`: entonces todos usan `porDefecto`.)
    /// - Un rango sin `minimo` o sin `maximo`, con un valor que no es un entero, con `minimo` menor que 1, o con `minimo`
    ///   mayor que `maximo`.
    /// - Una clave de `porBanco` que no es el nombre de un miembro de <see cref="Banco"/> (sin distinguir caja ni tildes,
    ///   <see cref="ListasPlantilla.Normalizar"/>), o dos claves que normalizan al mismo banco.
    /// </remarks>
    public sealed class LargosDeCuenta
    {
        /// <summary>`sanic_nombre` del parámetro en `sanic_mppp_tbl_parametro`.</summary>
        public const string NombreParametro = "cuenta.largos";

        private const string Prefijo = "El parámetro 'cuenta.largos' no es válido";

        private readonly RangoDeLargo _porDefecto;
        private readonly IDictionary<Banco, RangoDeLargo> _porBanco;

        private LargosDeCuenta(RangoDeLargo porDefecto, IDictionary<Banco, RangoDeLargo> porBanco)
        {
            _porDefecto = porDefecto;
            _porBanco = porBanco;
        }

        public static LargosDeCuenta DesdeJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new FormatException($"{Prefijo}: está vacío.");
            }

            var documento = AnalizadorJson.LeerEstricto(json, Prefijo);
            if (!(documento is IDictionary<string, object> raiz))
            {
                throw new FormatException($"{Prefijo}: la raíz tiene que ser un objeto con 'porDefecto' y, si hace falta, 'porBanco'.");
            }

            // Una clave que no es ninguna de las dos se rechaza: un `porBancos` mal escrito NO puede quedar como "todos usan
            // el rango por defecto" (aflojar en silencio lo que el negocio creyó restringir).
            foreach (var clave in raiz.Keys)
            {
                if (clave != "porDefecto" && clave != "porBanco")
                {
                    throw new FormatException($"{Prefijo}: trae una clave desconocida ('{clave}'); solo existen 'porDefecto' y 'porBanco'.");
                }
            }

            if (!raiz.TryGetValue("porDefecto", out var crudoPorDefecto) || crudoPorDefecto == null)
            {
                throw new FormatException($"{Prefijo}: falta 'porDefecto'.");
            }

            var porDefecto = LeerRango("porDefecto", crudoPorDefecto);
            var porBanco = new Dictionary<Banco, RangoDeLargo>();

            if (raiz.TryGetValue("porBanco", out var crudoPorBanco) && crudoPorBanco != null)
            {
                if (!(crudoPorBanco is IDictionary<string, object> bancos))
                {
                    throw new FormatException($"{Prefijo}: 'porBanco' no es un objeto.");
                }

                // Banco → clave tal cual vino, solo para citarla: dos claves que normalizan al mismo banco son una
                // ambigüedad que revienta (como en `porGestion`), no "gana la última".
                var clavesVistas = new Dictionary<Banco, string>();
                foreach (var par in bancos)
                {
                    if (!EnumPorNombre<Banco>.TryResolver(par.Key, out var banco))
                    {
                        throw new FormatException($"{Prefijo}: 'porBanco' trae un banco desconocido ('{par.Key}').");
                    }

                    if (clavesVistas.TryGetValue(banco, out var anterior))
                    {
                        throw new FormatException($"{Prefijo}: hay una ambigüedad en 'porBanco': '{anterior}' y '{par.Key}' son el mismo banco ({banco}).");
                    }

                    clavesVistas[banco] = par.Key;
                    porBanco[banco] = LeerRango($"porBanco.{par.Key}", par.Value);
                }
            }

            return new LargosDeCuenta(porDefecto, porBanco);
        }

        /// <summary>El rango de ese banco; el de `porDefecto` si el banco no tiene entrada propia o es nulo (banco
        /// desconocido: caso de una exclusión en formato 11 sin Banco, DN-05 decisión 10).</summary>
        public RangoDeLargo Para(Banco? banco)
        {
            if (banco.HasValue && _porBanco.TryGetValue(banco.Value, out var propio))
            {
                return propio;
            }

            return _porDefecto;
        }

        private static RangoDeLargo LeerRango(string donde, object crudo)
        {
            if (!(crudo is IDictionary<string, object> objeto))
            {
                throw new FormatException($"{Prefijo}: '{donde}' no es un objeto con 'minimo' y 'maximo'.");
            }

            foreach (var clave in objeto.Keys)
            {
                if (clave != "minimo" && clave != "maximo")
                {
                    throw new FormatException($"{Prefijo}: '{donde}' trae una clave desconocida ('{clave}'); solo existen 'minimo' y 'maximo'.");
                }
            }

            var minimo = LeerEntero(donde, objeto, "minimo");
            var maximo = LeerEntero(donde, objeto, "maximo");

            if (minimo < 1)
            {
                throw new FormatException($"{Prefijo}: el 'minimo' de '{donde}' tiene que ser 1 o mayor.");
            }

            if (minimo > maximo)
            {
                throw new FormatException($"{Prefijo}: el 'minimo' de '{donde}' es mayor que su 'maximo'.");
            }

            return new RangoDeLargo(minimo, maximo);
        }

        /// <summary>Un entero de JSON (el parser entrega <c>long</c> solo para un número sin fracción ni exponente). Un texto
        /// ("6"), un decimal (6.5 o 6.0), un booleano, `null` o un entero que no cabe en <c>int</c>: error.</summary>
        private static int LeerEntero(string donde, IDictionary<string, object> objeto, string clave)
        {
            if (!objeto.TryGetValue(clave, out var crudo) || crudo == null)
            {
                throw new FormatException($"{Prefijo}: a '{donde}' le falta '{clave}'.");
            }

            if (!(crudo is long entero) || entero < int.MinValue || entero > int.MaxValue)
            {
                throw new FormatException($"{Prefijo}: '{clave}' de '{donde}' no es un entero.");
            }

            return (int)entero;
        }
    }

    /// <summary>Un rango de largo, ambos extremos INCLUIDOS. Inmutable. FIRMA DEL ARQUITECTO.</summary>
    public sealed class RangoDeLargo
    {
        public RangoDeLargo(int minimo, int maximo)
        {
            Minimo = minimo;
            Maximo = maximo;
        }

        public int Minimo { get; }

        public int Maximo { get; }
    }
}
