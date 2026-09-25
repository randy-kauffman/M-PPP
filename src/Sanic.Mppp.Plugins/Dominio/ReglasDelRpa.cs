using System;

namespace Sanic.Mppp.Plugins.Dominio
{
    /// <summary>
    /// Las reglas de borde de las dos Custom API del RPA (diseno/03 §2 y §3), sin Dataverse de por medio:
    /// cuánto dura una reserva, si sigue vigente, si una fecha de AS400 es creíble y si un lote es aceptable.
    ///
    /// Están acá y no adentro de los plugins porque **son decisiones de negocio, no de plomería**, y porque son
    /// exactamente el tipo de cosa que hay que poder probar sin levantar medio entorno.
    ///
    /// Los códigos `MPPP-RPA-00x` viajan al bot dentro del mensaje: el bot decide por el prefijo si reintentar o no
    /// (`03` §3). Un error sin código es transitorio y SÍ se reintenta; uno con código, nunca.
    /// </summary>
    public static class ReglasDelRpa
    {
        /// <summary>Fallback quemado de `rpa.horas.tolerancia`: que falte el parámetro degrada el control, no tumba al bot.</summary>
        public const int ToleranciaHorasPorDefecto = 24;

        /// <summary>Fallback quemado de `rpa.minutos.claimmaximo`, por el mismo motivo que el de arriba.</summary>
        public const int ClaimMaximoMinutosPorDefecto = 60;

        public const string CodigoFecha = "MPPP-RPA-003";
        public const string CodigoLoteGrande = "MPPP-RPA-008";
        public const string CodigoLoteVacio = "MPPP-RPA-009";

        /// <summary>
        /// Minutos que va a durar la reserva. El bot pide; el tope manda.
        ///
        /// Se **recorta** al tope en vez de rechazar, igual que `maximo` en `03` §2: pedir una reserva más larga de la
        /// permitida no es un error del bot, es una preferencia que no se le puede conceder. Cero o negativo sí es un
        /// error: una reserva que nace vencida no reserva nada.
        /// </summary>
        public static int MinutosDeClaim(int pedidos, int tope)
        {
            if (pedidos <= 0)
            {
                throw new ArgumentException("Una reserva de cero o menos minutos nace vencida: no reserva nada.", nameof(pedidos));
            }

            // Un tope mal cargado (`rpa.minutos.claimmaximo` lo escribe una persona) haria que el recorte
            // devolviera 0: una reserva que nace vencida, o sea NINGUNA reserva, con el bot colisionando
            // contra los ejecutivos sin que falle nada. Degrada al fallback, como la tolerancia de fecha.
            var topeEfectivo = tope > 0 ? tope : ClaimMaximoMinutosPorDefecto;

            return pedidos > topeEfectivo ? topeEfectivo : pedidos;
        }

        /// <summary>`true` si la reserva sigue en pie. Sin valor = nunca se reservó = libre.</summary>
        public static bool ClaimVigente(DateTime? claimHasta, DateTime ahora)
        {
            if (claimHasta == null)
            {
                return false;
            }

            // "Hasta" no incluye el instante: en el borde exacto la fila ya está libre.
            return claimHasta.Value > ahora;
        }

        /// <summary>
        /// Valida `fechaproceso`. Lanza <see cref="ArgumentException"/> con <see cref="CodigoFecha"/> si no sirve.
        ///
        /// `sanic_fechadigitada` y `sanic_fechaaprobada` son el rastro de los cuatro ojos: un bot con el reloj o la
        /// zona horaria mal los envenena **en silencio**, y nadie se entera hasta que alguien audite.
        /// </summary>
        public static void ExigirFechaCreible(DateTime fechaProceso, DateTime ahora, int toleranciaHoras)
        {
            // Una tolerancia mal cargada (0 o negativa) degrada el control al fallback, no tumba al bot.
            int toleranciaEfectiva = toleranciaHoras > 0 ? toleranciaHoras : ToleranciaHorasPorDefecto;

            if (fechaProceso > ahora)
            {
                throw new ArgumentException(CodigoFecha + ": la fecha de proceso está en el futuro; el reloj del bot está adelantado.", nameof(fechaProceso));
            }

            DateTime limiteAntiguedad = ahora.AddHours(-toleranciaEfectiva);
            if (fechaProceso < limiteAntiguedad)
            {
                throw new ArgumentException(CodigoFecha + ": la fecha de proceso es más vieja que la tolerancia permitida.", nameof(fechaProceso));
            }
        }

        /// <summary>
        /// Valida el tamaño del lote. Lanza <see cref="ArgumentException"/> con su código si no sirve.
        ///
        /// **Un lote grande se rechaza ENTERO, nunca se recorta** — al revés que `maximo`, y a propósito: `maximo`
        /// pide trabajo y darle menos no pierde nada, pero un lote son **hechos que ya ocurrieron en AS400**, y
        /// procesar solo los primeros X perdería el resto en silencio. Quien "unifique" los dos comportamientos va a
        /// romper esto.
        /// </summary>
        public static void ExigirLoteAceptable(int cantidad, int tope)
        {
            if (cantidad <= 0)
            {
                throw new ArgumentException(CodigoLoteVacio + ": el lote llegó vacío; no hay nada que procesar.", nameof(cantidad));
            }

            if (cantidad > tope)
            {
                // Se rechaza ENTERO: a diferencia de MinutosDeClaim, recortar perdería en silencio hechos ya ocurridos en AS400.
                throw new ArgumentException(CodigoLoteGrande + ": el lote supera el tope permitido y se rechaza completo.", nameof(cantidad));
            }
        }
    }
}
