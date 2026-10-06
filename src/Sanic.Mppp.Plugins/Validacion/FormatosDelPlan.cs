using System;
using System.Collections.Generic;
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

        private FormatosDelPlan()
        {
        }

        public static FormatosDelPlan DesdeJson(string json)
        {
            throw new NotImplementedException();
        }

        /// <summary>Lo que admite ese formato. Nunca nulo: los tres formatos son obligatorios en el parámetro.</summary>
        public ReglasDelFormato Para(TipoDeFormatoDelPlan formato)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>Lo que admite UN formato de plan (DN-05). Inmutable: las listas se exponen como copias de solo lectura.
    /// FIRMA DEL ARQUITECTO.</summary>
    public sealed class ReglasDelFormato
    {
        private ReglasDelFormato()
        {
        }

        /// <summary>Las clasificaciones admitidas, en el orden del parámetro. Nunca vacía.</summary>
        public IReadOnlyList<Clasificacion> Clasificaciones => throw new NotImplementedException();

        /// <summary>Las gestiones admitidas, en el orden del parámetro. Nunca vacía.</summary>
        public IReadOnlyList<Gestion> Gestiones => throw new NotImplementedException();

        /// <summary>Los bancos admitidos, en el orden del parámetro. NULO = cualquier banco (la clave no vino).</summary>
        public IReadOnlyList<Banco> Bancos => throw new NotImplementedException();
    }
}
