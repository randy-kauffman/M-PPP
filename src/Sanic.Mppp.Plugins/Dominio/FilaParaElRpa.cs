using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

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
        public static string Serializar(IEnumerable<FilaParaElRpa> filas)
        {
            if (filas == null)
            {
                throw new ArgumentNullException(nameof(filas));
            }

            var sb = new StringBuilder();
            sb.Append('[');

            var primera = true;
            foreach (var fila in filas)
            {
                if (fila == null)
                {
                    // Una fila nula acá es un error de armado de quien llama, no algo que el bot deba ver.
                    throw new ArgumentException("Una fila nula es un error de armado.", nameof(filas));
                }

                if (!primera)
                {
                    sb.Append(',');
                }

                primera = false;
                EscribirFila(sb, fila);
            }

            sb.Append(']');
            return sb.ToString();
        }

        /// <summary>El estado, como lo lee el bot: `validada` o `digitada`.</summary>
        public static string EstadoParaElBot(EstadoDeLaFila estado)
        {
            switch (estado)
            {
                case EstadoDeLaFila.Validada:
                    return "validada";
                case EstadoDeLaFila.Digitada:
                    return "digitada";
                default:
                    // `SeleccionParaElRpa` ya garantiza que no llegan otros estados. Si llegara uno, es un defecto
                    // nuestro y tiene que gritar acá (ArgumentOutOfRangeException), no viajarle al bot como texto inventado.
                    throw new ArgumentOutOfRangeException(nameof(estado), estado,
                        $"El bot nunca debería ver una fila en estado {estado}.");
            }
        }

        // Los 18 campos, en el orden fijo del contrato (diseno/03 §2). Nombres en minúscula y sin separadores,
        // exactamente como los verifica `Estan_los_18_campos_con_su_nombre_exacto`.
        private static void EscribirFila(StringBuilder sb, FilaParaElRpa fila)
        {
            sb.Append('{');
            EscribirCampoTexto(sb, "filaid", fila.FilaId.ToString());
            sb.Append(',');
            EscribirCampoTexto(sb, "solicitud", fila.Solicitud);
            sb.Append(',');
            EscribirCampoNumero(sb, "numerofila", fila.NumeroFila);
            sb.Append(',');
            EscribirCampoTexto(sb, "estado", EstadoParaElBot(fila.Estado));
            sb.Append(',');
            EscribirCampoTexto(sb, "plan", fila.Plan);
            sb.Append(',');
            EscribirCampoTexto(sb, "tipoformato", fila.TipoFormato);
            sb.Append(',');
            EscribirCampoTexto(sb, "gestion", fila.Gestion);
            sb.Append(',');
            EscribirCampoTexto(sb, "clasificacion", fila.Clasificacion);
            sb.Append(',');
            EscribirCampoTexto(sb, "nombrebeneficiario", fila.NombreBeneficiario);
            sb.Append(',');
            EscribirCampoTexto(sb, "tipoidentificacion", fila.TipoIdentificacion);
            sb.Append(',');
            EscribirCampoTexto(sb, "numeroidentificacion", fila.NumeroIdentificacion);
            sb.Append(',');
            EscribirCampoTexto(sb, "numerocuenta", fila.NumeroCuenta);
            sb.Append(',');
            EscribirCampoTexto(sb, "moneda", fila.Moneda);
            sb.Append(',');
            EscribirCampoTexto(sb, "banco", fila.Banco);
            sb.Append(',');
            EscribirCampoTexto(sb, "bancocodigo", fila.BancoCodigo);
            sb.Append(',');
            EscribirCampoTexto(sb, "referencia", fila.Referencia);
            sb.Append(',');
            // Tal cual vino: NUNCA se completa con Referencia aunque haya llegado vacía (contrato, formato 11).
            EscribirCampoTexto(sb, "referenciarecibida", fila.ReferenciaRecibida);
            sb.Append(',');
            EscribirCampoTexto(sb, "fechavalidada", FormatearFecha(fila.FechaValidada));
            sb.Append('}');
        }

        private static void EscribirCampoTexto(StringBuilder sb, string nombre, string valor)
        {
            sb.Append('"').Append(nombre).Append("\":\"");
            EscaparJson(sb, valor ?? string.Empty);
            sb.Append('"');
        }

        private static void EscribirCampoNumero(StringBuilder sb, string nombre, int valor)
        {
            sb.Append('"').Append(nombre).Append("\":").Append(valor.ToString(CultureInfo.InvariantCulture));
        }

        // Offset explícito siempre (contrato): `2026-10-05T14:52:07Z`. ToUniversalTime() cubre el caso de que a
        // esta función le llegue una fecha que no venga marcada como Utc.
        private static string FormatearFecha(DateTime fecha)
        {
            return fecha.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        }

        // Escapado JSON manual (no hay serializador disponible en este ensamblado de plugin, y no se agregan
        // dependencias nuevas): el nombre del beneficiario viene de un Excel de un cliente, así que es texto
        // hostil hasta que se demuestre lo contrario (diseno/03 §7). Se cubren las dos barras que rompen un
        // string JSON (`"` y `\`), los escapes cortos estándar y CUALQUIER otro carácter de control (< 0x20)
        // vía `\uXXXX`, no solo los que ejercita la prueba.
        private static void EscaparJson(StringBuilder sb, string valor)
        {
            foreach (var c in valor)
            {
                switch (c)
                {
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        if (c < ' ')
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }

                        break;
                }
            }
        }
    }
}
