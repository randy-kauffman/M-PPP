using System;
using System.Collections.Generic;

namespace Sanic.Mppp.Plugins.Plantilla
{
    /// <summary>
    /// DN-05 (aprobador, 2026-10-06, escenario 21): qué caracteres admite un campo. Viene en `plantilla.estructura`, en
    /// `campos[].caracteres`; un campo sin esa clave no se revisa (<see cref="CampoPlantilla.Caracteres"/> nulo). Lo usa la
    /// regla `CARACTERES_PERMITIDOS`. Inmutable. FIRMA DEL ARQUITECTO.
    /// </summary>
    /// <remarks>
    /// JSON: <c>{"letras": true, "digitos": true, "espacio": true, "otros": "-.,"}</c>. Una clave que falta vale
    /// <c>false</c> (o vacío, en <c>otros</c>). Las letras son SOLO ASCII (A-Z, a-z): sin Ñ ni tildes (decisión 7). Si el
    /// negocio quiere una letra más, va en <c>otros</c>. Todo lo que no se permite expresamente está prohibido, incluidos
    /// los caracteres invisibles y los de control.
    /// </remarks>
    public sealed class CaracteresPermitidos
    {
        private readonly HashSet<char> _otros;

        /// <summary>
        /// <see cref="ArgumentException"/> si no permite NADA (las tres en <c>false</c> y <c>otros</c> nulo o vacío): un
        /// campo en el que ningún valor es posible es un parámetro mal cargado, no una regla. <c>otros</c> nulo = vacío.
        /// </summary>
        public CaracteresPermitidos(bool letras, bool digitos, bool espacio, string otros)
        {
            Otros = otros ?? string.Empty;
            if (!letras && !digitos && !espacio && Otros.Length == 0)
            {
                throw new ArgumentException("Un campo sin ningún carácter permitido no admite ningún valor: es un parámetro mal cargado.", nameof(otros));
            }

            Letras = letras;
            Digitos = digitos;
            Espacio = espacio;
            // Conjunto armado una vez: Permite() es O(1) por carácter, y el costo de revisar un valor queda atado a su largo.
            _otros = new HashSet<char>(Otros);
        }

        /// <summary>A-Z y a-z, ASCII.</summary>
        public bool Letras { get; }

        /// <summary>0-9, ASCII.</summary>
        public bool Digitos { get; }

        /// <summary>Solo el espacio común (U+0020). Un tabulador o un espacio duro (U+00A0) NO es un espacio.</summary>
        public bool Espacio { get; }

        /// <summary>Los caracteres agregados, tal cual vinieron. Nunca nulo.</summary>
        public string Otros { get; }

        /// <summary>True si <paramref name="c"/> está permitido. Letra y dígito se comparan por rango de <see cref="char"/>,
        /// nunca con `char.IsLetter`/`char.IsDigit` (que aceptan letras y dígitos de otros alfabetos).</summary>
        public bool Permite(char c)
        {
            if (Letras && ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')))
            {
                return true;
            }

            if (Digitos && c >= '0' && c <= '9')
            {
                return true;
            }

            if (Espacio && c == ' ')
            {
                return true;
            }

            return _otros.Contains(c);
        }
    }
}
