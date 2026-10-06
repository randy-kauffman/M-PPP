using System;
using System.Collections.Generic;
using System.Linq;

namespace Sanic.Mppp.Plugins.Validacion
{
    /// <summary>
    /// DN-05: el miembro de un enum cuyo nombre coincide con un texto de parámetro, comparado sin distinguir caja ni tildes
    /// (<see cref="ListasPlantilla.Normalizar"/>, la misma forma que `porGestion`; no una segunda). Lo comparten `plan.formatos`
    /// y `cuenta.largos`. El nombre es el del miembro (`Inclusion`, `BAC`), nunca su valor numérico: "159460001" no resuelve.
    /// </summary>
    internal static class EnumPorNombre<T> where T : struct
    {
        /// <summary>Nombre normalizado → miembro, armado una vez por tipo.</summary>
        private static readonly IDictionary<string, T> PorNombreNormalizado = Enum.GetNames(typeof(T))
            .ToDictionary(nombre => ListasPlantilla.Normalizar(nombre), nombre => (T)Enum.Parse(typeof(T), nombre), StringComparer.Ordinal);

        public static bool TryResolver(string texto, out T miembro)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                miembro = default(T);
                return false;
            }

            return PorNombreNormalizado.TryGetValue(ListasPlantilla.Normalizar(texto), out miembro);
        }
    }
}
