using System;
using System.Collections.Generic;
using System.Globalization;
using Sanic.Mppp.Plugins.Dominio;
using Sanic.Mppp.Plugins.Plantilla;
using Sanic.Mppp.Plugins.Steps;

namespace Sanic.Mppp.Plugins.Catalogos
{
    /// <summary>Los encabezados tal como los dice la plantilla: prefijo de todo motivo sobre esa columna. Una sola fuente, <see cref="ColumnasDeCarga"/>.</summary>
    internal static class Encabezados
    {
        internal static readonly string CifBac = ColumnasDeCarga.EncabezadoDe(ColumnasDeCarga.CifBac);
        internal static readonly string CifCom = ColumnasDeCarga.EncabezadoDe(ColumnasDeCarga.CifCom);
        internal static readonly string RazonSocial = ColumnasDeCarga.EncabezadoDe(ColumnasDeCarga.RazonSocial);
        internal static readonly string CorreoEjecutivo = ColumnasDeCarga.EncabezadoDe(ColumnasDeCarga.CorreoEjecutivo);
        internal static readonly string CodigoPlan = ColumnasDeCarga.EncabezadoDe(ColumnasDeCarga.CodigoPlan);
        internal static readonly string TipoFormato = ColumnasDeCarga.EncabezadoDe(ColumnasDeCarga.TipoFormato);
        internal static readonly string Moneda = ColumnasDeCarga.EncabezadoDe(ColumnasDeCarga.Moneda);
        internal static readonly string Correo = ColumnasDeCarga.EncabezadoDe(ColumnasDeCarga.Correo);
        internal static readonly string NombreContacto = ColumnasDeCarga.EncabezadoDe(ColumnasDeCarga.NombreContacto);
        internal static readonly string FechaDocumento = ColumnasDeCarga.EncabezadoDe(ColumnasDeCarga.FechaDocumento);
        internal static readonly string Comentarios = ColumnasDeCarga.EncabezadoDe(ColumnasDeCarga.Comentarios);

        /// <summary>Cuando el problema es del cliente en conjunto y no de una de sus dos claves (DN-04, regla 1 y siguientes).</summary>
        internal const string Cliente = "CIF BAC / CIF COM";
    }

    /// <summary>
    /// Lee UNA fila: celda por celda, normaliza y valida el formato, y deja dicho (en la propia fila) cada error de formato.
    /// Lo que depende de otras filas o de lo que existe lo resuelve el planificador. DN-04, Excel 1, "Reglas por fila".
    /// </summary>
    internal static class LectorDeFilaDeCarga
    {
        // Largos de las columnas de destino (diseno/02): razón social T(200), correo T(320), nombre de contacto T(100), comentarios T(50).
        private const int MaximoRazonSocial = 200;
        private const int MaximoCorreo = 320;
        private const int MaximoNombreContacto = 100;
        private const int MaximoComentarios = 50;

        private static readonly DateTime PrimeraFechaValida = new DateTime(1900, 1, 1);

        internal static FilaDeTrabajo Leer(FilaPlantilla fila, int posicion, DateTime hoy)
        {
            var f = new FilaDeTrabajo(posicion, fila.NumeroFilaExcel);
            var valores = fila.Valores;

            // Cliente (reglas 1 y 2 miran los dos CIF juntos; acá solo el formato de cada uno).
            var cifBac = Celda(valores, ColumnasDeCarga.CifBac);
            var cifCom = Celda(valores, ColumnasDeCarga.CifCom);
            f.CifBac = Normalizar(f, Encabezados.CifBac, cifBac, Normalizacion.CifBac);
            f.CifCom = Normalizar(f, Encabezados.CifCom, cifCom, Normalizacion.CifCom);
            if (cifBac == null && cifCom == null)
                f.Error(Encabezados.Cliente + ": la fila no tiene cliente. Completá el CIF BAC, el CIF COM o los dos.");

            var razon = Celda(valores, ColumnasDeCarga.RazonSocial);
            f.Razon = Texto(f, Encabezados.RazonSocial, razon, MaximoRazonSocial);
            f.CorreoEjecutivo = LeerCorreo(f, Encabezados.CorreoEjecutivo, Celda(valores, ColumnasDeCarga.CorreoEjecutivo));

            // Plan (regla 5): tipo de formato y moneda son datos de un plan, y sin código no hay plan.
            var codigo = Celda(valores, ColumnasDeCarga.CodigoPlan);
            var formato = Celda(valores, ColumnasDeCarga.TipoFormato);
            var moneda = Celda(valores, ColumnasDeCarga.Moneda);
            f.CodigoPlan = Normalizar(f, Encabezados.CodigoPlan, codigo, Normalizacion.CodigoDePlan);
            if (codigo == null)
            {
                f.Formato = SinPlan(f, Encabezados.TipoFormato, formato, new Campo<TipoDeFormatoDelPlan>());
                f.Moneda = SinPlan(f, Encabezados.Moneda, moneda, new Campo<Moneda>());
            }
            else
            {
                f.Formato = LeerFormato(f, formato);
                f.Moneda = LeerMoneda(f, moneda);
            }

            // Correo y autorización (regla 8): nombre, fecha y comentarios son datos de una autorización, y sin correo no hay autorización.
            var correo = Celda(valores, ColumnasDeCarga.Correo);
            var nombre = Celda(valores, ColumnasDeCarga.NombreContacto);
            var fecha = Celda(valores, ColumnasDeCarga.FechaDocumento);
            var comentarios = Celda(valores, ColumnasDeCarga.Comentarios);
            f.Correo = LeerCorreo(f, Encabezados.Correo, correo);
            if (correo == null)
            {
                f.Nombre = SinCorreo(f, Encabezados.NombreContacto, nombre, new Campo<string>());
                f.Fecha = SinCorreo(f, Encabezados.FechaDocumento, fecha, new Campo<DateTime>());
                f.Comentarios = SinCorreo(f, Encabezados.Comentarios, comentarios, new Campo<string>());
            }
            else
            {
                f.Nombre = Texto(f, Encabezados.NombreContacto, nombre, MaximoNombreContacto);
                f.Fecha = LeerFecha(f, fecha, hoy);
                f.Comentarios = Texto(f, Encabezados.Comentarios, comentarios, MaximoComentarios);
            }

            return f;
        }

        /// <summary>El valor de la celda por LETRA de columna, sin espacios a los lados; nulo si falta la clave, el valor es nulo o está en blanco (DN-04).</summary>
        private static string Celda(IDictionary<string, string> valores, string campo)
        {
            if (valores == null) return null;
            string valor;
            if (!valores.TryGetValue(ColumnasDeCarga.LetraDe(campo), out valor) || string.IsNullOrWhiteSpace(valor)) return null;
            return valor.Trim();
        }

        /// <summary>
        /// DN-04 exige que "qué es un CIF o un correo válido" sea EXACTAMENTE lo que dice el step: se usa su misma función. Lanza
        /// <see cref="ArgumentException"/> con un motivo legible; se atrapa solo acá (código propio, no un SDK) y pasa a ser
        /// el error de esta fila con el prefijo de la columna.
        /// </summary>
        private static Campo<string> Normalizar(FilaDeTrabajo f, string encabezado, string crudo, Func<string, string> normalizar)
        {
            var campo = new Campo<string>();
            if (crudo == null) return campo;

            campo.Presente = true;
            try
            {
                campo.Valor = normalizar(crudo);
                campo.Valido = campo.Valor != null;
            }
            catch (ArgumentException ex)
            {
                f.Error(encabezado + ": \"" + MotivosDeCarga.Mostrar(crudo) + "\" no es válido. " + MotivosDeCarga.DeArgumento(ex) + " Corregilo.");
            }

            return campo;
        }

        // Los dos correos del archivo (D, el del ejecutivo, y H, el autorizado) caben en una columna de correo T(320) (diseno/02 §2.3).
        private static Campo<string> LeerCorreo(FilaDeTrabajo f, string encabezado, string crudo)
        {
            var campo = Normalizar(f, encabezado, crudo, Normalizacion.CorreoAutorizado);
            if (campo.Usable && campo.Valor.Length > MaximoCorreo)
            {
                campo.Valido = false;
                f.Error(LargoExcedido(encabezado, campo.Valor.Length, MaximoCorreo));
            }

            return campo;
        }

        /// <summary>Texto libre: sin espacios a los lados, con tope de largo.</summary>
        private static Campo<string> Texto(FilaDeTrabajo f, string encabezado, string crudo, int maximo)
        {
            var campo = new Campo<string>();
            if (crudo == null) return campo;

            campo.Presente = true;
            if (crudo.Length > maximo)
            {
                f.Error(LargoExcedido(encabezado, crudo.Length, maximo));
                return campo;
            }

            campo.Valor = crudo;
            campo.Valido = true;
            return campo;
        }

        private static string LargoExcedido(string encabezado, int largo, int maximo)
            => encabezado + ": tiene " + largo.ToString(CultureInfo.InvariantCulture) + " caracteres y el máximo es " + maximo.ToString(CultureInfo.InvariantCulture) + ". Acortalo.";

        private static Campo<TipoDeFormatoDelPlan> LeerFormato(FilaDeTrabajo f, string crudo)
        {
            var campo = new Campo<TipoDeFormatoDelPlan>();
            if (crudo == null) return campo;

            campo.Presente = true;
            // "6" se acepta como "06" (DN-04): Excel guarda el 06 como número y pierde el cero.
            if (crudo == "06" || crudo == "6") campo.Valor = TipoDeFormatoDelPlan._06;
            else if (crudo == "10") campo.Valor = TipoDeFormatoDelPlan._10;
            else if (crudo == "11") campo.Valor = TipoDeFormatoDelPlan._11;
            else
            {
                f.Error(Encabezados.TipoFormato + ": \"" + MotivosDeCarga.Mostrar(crudo) + "\" no es un tipo de formato válido. Usá 06, 10 u 11 (también se acepta 6). Corregilo.");
                return campo;
            }

            campo.Valido = true;
            return campo;
        }

        private static Campo<Moneda> LeerMoneda(FilaDeTrabajo f, string crudo)
        {
            var campo = new Campo<Moneda>();
            if (crudo == null) return campo;

            campo.Presente = true;
            if (string.Equals(crudo, "COR", StringComparison.OrdinalIgnoreCase)) campo.Valor = Moneda.COR;
            else if (string.Equals(crudo, "USD", StringComparison.OrdinalIgnoreCase)) campo.Valor = Moneda.USD;
            else
            {
                f.Error(Encabezados.Moneda + ": \"" + MotivosDeCarga.Mostrar(crudo) + "\" no es una moneda válida. Usá COR o USD. Corregila.");
                return campo;
            }

            campo.Valido = true;
            return campo;
        }

        private static Campo<DateTime> LeerFecha(FilaDeTrabajo f, string crudo, DateTime hoy)
        {
            var campo = new Campo<DateTime>();
            if (crudo == null) return campo;

            campo.Presente = true;
            DateTime fecha;
            if (!FechaDeCarga.TryInterpretar(crudo, out fecha))
            {
                f.Error(Encabezados.FechaDocumento + ": \"" + MotivosDeCarga.Mostrar(crudo)
                        + "\" no es una fecha válida. Usá una celda de fecha de Excel o el formato d/m/aaaa (por ejemplo, 15/09/2024). Corregila.");
                return campo;
            }

            // Dataverse no admite fechas anteriores a 1753, y una que pase acá reventaría la pasada 2 entera sin decir qué fila: un
            // documento firmado anterior a 1900 no existe en este negocio (DN-04, "Lectura del archivo", Fechas).
            if (fecha < PrimeraFechaValida)
            {
                f.Error(Encabezados.FechaDocumento + ": " + Dia(fecha) + " es anterior al " + Dia(PrimeraFechaValida) + ", que es la primera fecha que se acepta. Corregila.");
                return campo;
            }

            // Un documento firmado no puede tener fecha de mañana (DN-04, "Fechas").
            if (fecha > hoy.Date)
            {
                f.Error(Encabezados.FechaDocumento + ": " + Dia(fecha) + " es posterior a hoy (" + Dia(hoy) + "). Un documento firmado no puede tener fecha futura. Corregila.");
                return campo;
            }

            campo.Valor = fecha;
            campo.Valido = true;
            return campo;
        }

        internal static string Dia(DateTime fecha) => fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        // Una celda que trae un dato de un plan o de una autorización que la fila no nombra: error y fuera de toda comparación posterior.
        private static Campo<T> SinPlan<T>(FilaDeTrabajo f, string encabezado, string crudo, Campo<T> campo)
        {
            if (crudo == null) return campo;
            campo.Presente = true;
            f.Error(encabezado + ": viene sin " + Encabezados.CodigoPlan + ", y es un dato de un plan que no se nombró. Completá el código del plan o borrá la celda.");
            return campo;
        }

        private static Campo<T> SinCorreo<T>(FilaDeTrabajo f, string encabezado, string crudo, Campo<T> campo)
        {
            if (crudo == null) return campo;
            campo.Presente = true;
            f.Error(encabezado + ": viene sin " + Encabezados.Correo + ", y es un dato de una autorización sin correo. Completá el correo o borrá la celda.");
            return campo;
        }
    }
}
