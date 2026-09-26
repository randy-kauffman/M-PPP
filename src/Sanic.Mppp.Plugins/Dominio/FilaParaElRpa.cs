using System;

namespace Sanic.Mppp.Plugins.Dominio
{
    /// <summary>Una fila ya resuelta, lista para serializar al bot (diseno/03 §2).</summary>
    public sealed class FilaParaElRpa
    {
        public Guid FilaId { get; set; }
        public string Solicitud { get; set; }
        public int NumeroFila { get; set; }
        public EstadoDeLaFila Estado { get; set; }
        public string Plan { get; set; }
        public string TipoFormato { get; set; }
        public string Gestion { get; set; }
        public string Clasificacion { get; set; }
        public string NombreBeneficiario { get; set; }
        public string TipoIdentificacion { get; set; }
        public string NumeroIdentificacion { get; set; }
        public string NumeroCuenta { get; set; }
        public string Moneda { get; set; }
        public string Banco { get; set; }

        /// <summary>Los 3 dígitos del banco (DD-10). El bot no tiene por qué saber que BANPRO es 008.</summary>
        public string BancoCodigo { get; set; }

        /// <summary>La que RIGE: en formato 11 es la construida; en los demás, la recibida.</summary>
        public string Referencia { get; set; }

        /// <summary>Tal cual vino en el Excel. **Si vino vacía, va vacía**, también en formato 11.</summary>
        public string ReferenciaRecibida { get; set; }

        public DateTime FechaValidada { get; set; }
    }

    /// <summary>
    /// Arma el JSON que recibe el bot (diseno/03 §2, revisado con el aprobador el 2026-09-25).
    ///
    /// **Por qué está separado del plugin.** El contrato con el bot es lo que más caro sale cambiar: del otro lado
    /// hay un proceso de Automation Anywhere que alguien más mantiene. Un cambio silencioso acá le rompe el día a
    /// otro equipo, así que vive en su propia clase, con sus propias pruebas, y se toca a propósito.
    ///
    /// **Dos decisiones que el JSON lleva adentro:**
    ///  - los choices viajan como **texto canónico** (`Inclusion`, `ACH`, `RUC`, `COR`, `BANPRO`, `validada`), nunca
    ///    como el número de la opción: un `159460001` no le dice nada a un bot, y son los valores que ya declara
    ///    `plantilla.listas` y los que el bot tipea en AS400;
    ///  - todas las fechas con **offset explícito** (`2026-10-05T14:52:07Z`).
    /// </summary>
    public static class ArmadorJsonDelRpa
    {
        /// <summary>El arreglo completo. Vacío si no hay filas: `[]`, nunca `null` ni vacío de verdad.</summary>
        public static string Serializar(System.Collections.Generic.IEnumerable<FilaParaElRpa> filas)
        {
            throw new NotImplementedException("8.x: lo implementa el constructor contra ArmadorJsonDelRpaAceptacion.");
        }

        /// <summary>El estado, como lo lee el bot: `validada` o `digitada`.</summary>
        public static string EstadoParaElBot(EstadoDeLaFila estado)
        {
            throw new NotImplementedException("8.x: lo implementa el constructor contra ArmadorJsonDelRpaAceptacion.");
        }
    }
}
