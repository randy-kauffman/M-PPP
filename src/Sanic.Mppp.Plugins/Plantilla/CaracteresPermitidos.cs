using System;

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
        /// <summary>
        /// <see cref="ArgumentException"/> si no permite NADA (las tres en <c>false</c> y <c>otros</c> nulo o vacío): un
        /// campo en el que ningún valor es posible es un parámetro mal cargado, no una regla. <c>otros</c> nulo = vacío.
        /// </summary>
        public CaracteresPermitidos(bool letras, bool digitos, bool espacio, string otros)
        {
            throw new NotImplementedException();
        }

        /// <summary>A-Z y a-z, ASCII.</summary>
        public bool Letras => throw new NotImplementedException();

        /// <summary>0-9, ASCII.</summary>
        public bool Digitos => throw new NotImplementedException();

        /// <summary>Solo el espacio común (U+0020). Un tabulador o un espacio duro (U+00A0) NO es un espacio.</summary>
        public bool Espacio => throw new NotImplementedException();

        /// <summary>Los caracteres agregados, tal cual vinieron. Nunca nulo.</summary>
        public string Otros => throw new NotImplementedException();

        /// <summary>True si <paramref name="c"/> está permitido.</summary>
        public bool Permite(char c)
        {
            throw new NotImplementedException();
        }
    }
}
