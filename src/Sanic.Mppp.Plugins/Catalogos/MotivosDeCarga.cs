using System;
using System.Collections.Generic;
using System.Linq;
using Sanic.Mppp.Plugins.Steps;

namespace Sanic.Mppp.Plugins.Catalogos
{
    /// <summary>Piezas de redacción de los motivos de error y advertencia de la carga (DN-04 §"Formato de los motivos").</summary>
    internal static class MotivosDeCarga
    {
        private const int MaximoDeFilasNombradas = 5;
        private const int MaximoDeCaracteresMostrados = 60;

        /// <summary>
        /// El motivo de una validación de <c>Steps.Normalizacion</c>, sin el nombre del parámetro que el framework le pega al
        /// <see cref="Exception.Message"/> (DN-04: se reutiliza la validación del step, pero el texto lo lee quien carga el archivo).
        /// Se corta donde empieza el nombre del parámetro y no por una frase fija, porque esa frase depende de la versión del
        /// framework y de su idioma.
        /// </summary>
        internal static string DeArgumento(ArgumentException excepcion)
        {
            var mensaje = excepcion.Message ?? string.Empty;
            var parametro = excepcion.ParamName;
            if (!string.IsNullOrEmpty(parametro))
            {
                var posicion = mensaje.LastIndexOf(parametro, StringComparison.Ordinal);
                if (posicion > 0)
                {
                    var antes = mensaje.Substring(0, posicion);
                    var corte = Math.Max(antes.LastIndexOf('\n'), antes.LastIndexOf(" (", StringComparison.Ordinal));
                    if (corte > 0)
                        mensaje = mensaje.Substring(0, corte);
                }
            }

            return mensaje.Trim();
        }

        /// <summary>"la fila 9", "las filas 3 y 9", "las filas 3, 5 y 9"; con más de cinco, nombra las primeras y cuenta el resto.</summary>
        internal static string Filas(IReadOnlyList<int> numeros)
        {
            var ordenados = numeros.OrderBy(n => n).ToList();
            if (ordenados.Count == 1)
                return "la fila " + ordenados[0].ToString(System.Globalization.CultureInfo.InvariantCulture);

            var texto = ordenados.Select(n => n.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList();
            if (texto.Count <= MaximoDeFilasNombradas)
                return "las filas " + string.Join(", ", texto.Take(texto.Count - 1)) + " y " + texto[texto.Count - 1];

            var resto = texto.Count - MaximoDeFilasNombradas;
            return "las filas " + string.Join(", ", texto.Take(MaximoDeFilasNombradas)) + " y otras " + resto.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>El valor de la celda tal como se cita en un mensaje: acortado, para que una celda enorme no desborde el Resultado.</summary>
        internal static string Mostrar(string valor)
        {
            if (valor == null) return string.Empty;
            if (valor.Length <= MaximoDeCaracteresMostrados) return valor;
            return TextoAscii.Recortar(valor, MaximoDeCaracteresMostrados - 1) + "…";
        }
    }
}
