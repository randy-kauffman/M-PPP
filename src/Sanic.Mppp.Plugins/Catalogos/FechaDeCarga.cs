using System;
using System.Globalization;
using Sanic.Mppp.Plugins.Steps;

namespace Sanic.Mppp.Plugins.Catalogos
{
    /// <summary>
    /// La "Fecha documento" del Excel de carga (DN-04, Excel 1, columna J): una celda de fecha de Excel (número serial,
    /// con la fracción de día descartada) o texto d/m/aaaa. Cualquier otra forma es ilegible.
    /// Sin cultura: el comportamiento no puede cambiar según la configuración regional del servidor.
    /// </summary>
    internal static class FechaDeCarga
    {
        private const double UltimoSerialValido = 2958465; // 31/12/9999, el tope de DateTime.FromOADate.

        /// <summary>El resultado es solo fecha, <see cref="DateTimeKind.Unspecified"/>. Si no se pudo leer, <paramref name="fecha"/> queda en su valor por defecto.</summary>
        internal static bool TryInterpretar(string valor, out DateTime fecha)
        {
            fecha = default(DateTime);
            if (string.IsNullOrWhiteSpace(valor)) return false;

            var texto = valor.Trim();
            return texto.IndexOf('/') >= 0 ? TryTexto(texto, out fecha) : TrySerial(texto, out fecha);
        }

        private static bool TrySerial(string texto, out DateTime fecha)
        {
            fecha = default(DateTime);

            // Solo dígitos y punto decimal: nada de signo ni de notación científica (DN-04: "1.23E+08" no se reconstruye con certeza).
            if (!double.TryParse(texto, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var serial))
                return false;

            var dia = Math.Floor(serial);
            if (dia < 1 || dia > UltimoSerialValido) return false;

            fecha = DateTime.FromOADate(dia).Date;
            return true;
        }

        private static bool TryTexto(string texto, out DateTime fecha)
        {
            fecha = default(DateTime);

            var partes = texto.Split('/');
            if (partes.Length != 3) return false;
            if (!EsNumeroAscii(partes[0], 1, 2) || !EsNumeroAscii(partes[1], 1, 2) || !EsNumeroAscii(partes[2], 4, 4)) return false;

            var dia = Convertir(partes[0]);
            var mes = Convertir(partes[1]);
            var anio = Convertir(partes[2]);
            if (anio < 1 || mes < 1 || mes > 12) return false;
            if (dia < 1 || dia > DateTime.DaysInMonth(anio, mes)) return false;

            fecha = new DateTime(anio, mes, dia);
            return true;
        }

        private static bool EsNumeroAscii(string texto, int minimo, int maximo)
        {
            if (texto.Length < minimo || texto.Length > maximo) return false;
            foreach (var c in texto)
            {
                if (!TextoAscii.EsDigito(c)) return false;
            }

            return true;
        }

        // Ya se comprobó que son 1 a 4 dígitos ASCII: se arma a mano, sin Parse sobre contenido del archivo.
        private static int Convertir(string digitos)
        {
            var numero = 0;
            foreach (var c in digitos)
                numero = numero * 10 + (c - '0');
            return numero;
        }
    }
}
