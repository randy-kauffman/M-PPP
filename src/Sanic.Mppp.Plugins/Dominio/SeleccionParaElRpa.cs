using System;

namespace Sanic.Mppp.Plugins.Dominio
{
    /// <summary>Lo que hay que saber de una Fila para decidir si el bot se la lleva (diseno/03 §2).</summary>
    public sealed class FilaCandidata
    {
        public EstadoDeLaFila Estado { get; set; }

        /// <summary>Quién la digitó. `null` si todavía nadie.</summary>
        public Guid? DigitadaPor { get; set; }

        /// <summary>`sanic_requiereatencionmanual`: si está prendida, la fila ya es de un ejecutivo.</summary>
        public bool RequiereAtencionManual { get; set; }

        /// <summary>`sanic_claimhasta`. Ver <see cref="ReglasDelRpa.ClaimVigente"/>.</summary>
        public DateTime? ClaimHasta { get; set; }
    }

    /// <summary>
    /// Qué fila entra en el lote que `obtenerfilaspendientes` le entrega al bot (diseno/03 §2, decisiones del
    /// 2026-09-25). Separado del plugin a propósito: son reglas de negocio, y se prueban sin levantar nada.
    ///
    /// El ORDEN de las filas no se decide acá (es FIFO por `sanic_fechavalidada` y número de fila, y lo resuelve la
    /// consulta): acá se decide la PERTENENCIA.
    /// </summary>
    public static class SeleccionParaElRpa
    {
        /// <param name="quienLlama">El usuario de aplicación que está pidiendo el lote.</param>
        /// <param name="puedeAprobar">`rpa.puedeaprobar`. En `false` no se entrega ninguna Digitada.</param>
        public static bool EntraEnElLote(FilaCandidata fila, Guid quienLlama, bool puedeAprobar, DateTime ahora)
        {
            // La atencion manual la saca del circuito automatico entero: gana incluso sobre una Digitada
            // propia, porque ya es trabajo de un ejecutivo, no del bot.
            if (fila.RequiereAtencionManual)
            {
                return false;
            }

            if (fila.Estado == EstadoDeLaFila.Validada)
            {
                // El claim NO excluye: es un candado contra personas, no contra el bot que la pide de nuevo.
                return true;
            }

            if (fila.Estado == EstadoDeLaFila.Digitada)
            {
                if (!puedeAprobar)
                {
                    return false;
                }

                // Solo entra si LE CONSTA que la digitacion se hizo bien: la hizo el mismo que llama ahora.
                return fila.DigitadaPor == quienLlama;
            }

            // Cualquier otro estado (Aprobada, Anulada, RechazadaEnAS400, RechazadaEnValidacion,
            // SinAutorizacion) es terminal: ya salio del circuito del RPA.
            return false;
        }
    }
}
