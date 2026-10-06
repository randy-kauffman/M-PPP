using System;
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

        private LargosDeCuenta()
        {
        }

        public static LargosDeCuenta DesdeJson(string json)
        {
            throw new NotImplementedException();
        }

        /// <summary>El rango de ese banco; el de `porDefecto` si el banco no tiene entrada propia o es nulo (banco
        /// desconocido: caso de una exclusión en formato 11 sin Banco, DN-05 decisión 10).</summary>
        public RangoDeLargo Para(Banco? banco)
        {
            throw new NotImplementedException();
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
