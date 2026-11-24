using System;
using System.Globalization;
using Sanic.Mppp.Plugins.Dominio;

namespace Sanic.Mppp.Plugins.Validacion
{
    /// <summary>
    /// DN-07 (aprobador, 2026-11-24): valida el número de identificación según su tipo. CNA: 13 dígitos + letra, fecha DDMMAA
    /// real y letra del Módulo 23. RUC: 14 caracteres; con forma de cédula se valida como cédula; con J, J + 13 dígitos.
    /// PAS y PEX: 6 a 9 letras o dígitos. CRE: sin formato conocido, no se mira. Función pura: ningún motivo repite el número (DD-08).
    /// </summary>
    public static class IdentificacionPorTipo
    {
        private const string AlfabetoModulo23 = "ABCDEFGHJKLMNPQRSTUVWXY";

        /// <summary>
        /// El motivo de rechazo, o null si el número es válido o no se mira (vacío, tipo CRE, o con algún caracter que no sea
        /// letra o dígito ASCII: ese caso lo rechaza CARACTERES_PERMITIDOS y el cliente no recibe dos motivos por lo mismo).
        /// </summary>
        public static string Motivo(TipoDeIdentificacion tipo, string numero)
        {
            if (tipo == TipoDeIdentificacion.CRE || string.IsNullOrWhiteSpace(numero))
            {
                return null;
            }

            var valor = numero.Trim().ToUpperInvariant();
            foreach (var c in valor)
            {
                if (!EsLetraAscii(c) && !EsDigitoAscii(c))
                {
                    return null;
                }
            }

            switch (tipo)
            {
                case TipoDeIdentificacion.CNA:
                    return EsFormaDeCedula(valor)
                        ? MotivoDeFechaYLetra(valor, "una cédula")
                        : "El número de identificación no es una cédula válida: debe tener 13 dígitos y una letra al final, sin guiones.";
                case TipoDeIdentificacion.RUC:
                    return MotivoRuc(valor);
                case TipoDeIdentificacion.PAS:
                case TipoDeIdentificacion.PEX:
                    return valor.Length >= 6 && valor.Length <= 9
                        ? null
                        : "El número de identificación no es un pasaporte válido: debe tener entre 6 y 9 letras o números.";
                default:
                    return null;
            }
        }

        private static string MotivoRuc(string valor)
        {
            if (valor.Length != 14)
            {
                return "El número de identificación no es un RUC válido: debe tener 14 caracteres.";
            }

            if (EsFormaDeCedula(valor))
            {
                return MotivoDeFechaYLetra(valor, "un RUC");
            }

            if (valor[0] == 'J')
            {
                for (var i = 1; i < valor.Length; i++)
                {
                    if (!EsDigitoAscii(valor[i]))
                    {
                        return "El número de identificación no es un RUC válido: después de la J debe tener 13 dígitos.";
                    }
                }
            }

            return null;
        }

        /// <summary>Fecha y luego letra de una cédula (o de un RUC con forma de cédula); el texto cambia solo en cómo se nombra.</summary>
        private static string MotivoDeFechaYLetra(string valor, string como)
        {
            var nombre = como == "un RUC" ? "un RUC válido" : "una cédula válida";
            if (!FechaExiste(valor))
            {
                return $"El número de identificación no es {nombre}: la fecha de nacimiento no existe.";
            }

            var residuo = (int)(long.Parse(valor.Substring(0, 13), CultureInfo.InvariantCulture) % 23);
            return valor[13] == AlfabetoModulo23[residuo]
                ? null
                : $"El número de identificación no es {nombre}: la letra final no corresponde al número.";
        }

        /// <summary>DDMMAA en las posiciones 4 a 9: vale si existe con el año en 1900+AA o en 2000+AA.</summary>
        private static bool FechaExiste(string valor)
        {
            var dia = int.Parse(valor.Substring(3, 2), CultureInfo.InvariantCulture);
            var mes = int.Parse(valor.Substring(5, 2), CultureInfo.InvariantCulture);
            var aa = int.Parse(valor.Substring(7, 2), CultureInfo.InvariantCulture);
            if (mes < 1 || mes > 12 || dia < 1)
            {
                return false;
            }

            return dia <= DateTime.DaysInMonth(1900 + aa, mes) || dia <= DateTime.DaysInMonth(2000 + aa, mes);
        }

        private static bool EsFormaDeCedula(string valor)
        {
            if (valor.Length != 14 || !EsLetraAscii(valor[13]))
            {
                return false;
            }

            for (var i = 0; i < 13; i++)
            {
                if (!EsDigitoAscii(valor[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool EsDigitoAscii(char c) => c >= '0' && c <= '9';

        private static bool EsLetraAscii(char c) => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
    }
}
