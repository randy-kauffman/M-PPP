using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Sanic.Mppp.Plugins.Validacion
{
    /// <summary>
    /// Parser JSON manual, genérico (objetos, arreglos, cadenas y el literal `null`, anidados) y deliberadamente chico, del
    /// mismo espíritu que <see cref="Dominio.CatalogoDeCodigosRpa"/>: `DataContractJsonSerializer` serializa un
    /// `Dictionary&lt;string,T&gt;` como un arreglo de pares clave/valor, no como el objeto plano `{"Exclusion":{...}}` que piden
    /// `porGestion` (DN-03), `plan.formatos` y `cuenta.largos` (DN-05). Extraído de <see cref="ObligatoriedadPlantilla"/> en
    /// DN-05 para que los tres parámetros lean con UNA sola implementación, no con tres copias.
    /// Devuelve: objeto → <c>IDictionary&lt;string, object&gt;</c>, arreglo → <c>IList&lt;object&gt;</c>, cadena → <c>string</c>,
    /// `null` → <c>null</c>; y solo en <see cref="LeerEstricto"/> también número entero → <c>long</c>, otro número →
    /// <c>double</c>, booleano → <c>bool</c>. Todo error es <see cref="FormatException"/> con el motivo. Una sola pasada sobre
    /// el texto (O(N)); el anidamiento está acotado por <see cref="ProfundidadMaxima"/>.
    /// </summary>
    internal static class AnalizadorJson
    {
        /// <summary>Tope de anidamiento: un parámetro legítimo tiene tres o cuatro niveles; más es un documento hostil o roto,
        /// y la recursión del lector no puede llegar a desbordar la pila.</summary>
        internal const int ProfundidadMaxima = 32;

        /// <summary>
        /// Comportamiento de DN-03 (`porGestion`), sin cambios: NO entiende números ni booleanos, la clave repetida de un
        /// objeto se pisa (gana la última) y no exige que el documento termine justo después del valor leído, porque la
        /// validez completa del documento ya la garantizó `DataContractJsonSerializer` antes. <paramref name="prefijoDelError"/>
        /// abre el mensaje de todo <see cref="FormatException"/>.
        /// </summary>
        public static object Leer(string json, string prefijoDelError)
        {
            return new Lector(json, prefijoDelError, estricto: false).LeerDocumento();
        }

        /// <summary>
        /// DN-05 (`plan.formatos`, `cuenta.largos`), donde el parser es la ÚNICA lectura del documento: entiende números y
        /// booleanos, una clave repetida dentro de un objeto es error (no se pisa en silencio) y después del valor solo puede
        /// haber espacios en blanco. Un documento con basura al final no se da por leído.
        /// </summary>
        public static object LeerEstricto(string json, string prefijoDelError)
        {
            return new Lector(json, prefijoDelError, estricto: true).LeerDocumento();
        }

        private sealed class Lector
        {
            private readonly string _texto;
            private readonly string _prefijo;
            private readonly bool _estricto;
            private int _pos;
            private int _profundidad;

            public Lector(string texto, string prefijo, bool estricto)
            {
                _texto = texto ?? string.Empty;
                _prefijo = prefijo;
                _estricto = estricto;
                _pos = 0;
            }

            public object LeerDocumento()
            {
                SaltarEspacios();
                var valor = LeerValor();
                if (_estricto)
                {
                    SaltarEspacios();
                    if (_pos < _texto.Length)
                    {
                        throw Falla("hay texto después del final del documento");
                    }
                }

                return valor;
            }

            private object LeerValor()
            {
                SaltarEspacios();
                var proximo = MirarProximo();
                switch (proximo)
                {
                    case '{':
                        return LeerObjeto();
                    case '[':
                        return LeerArreglo();
                    case '"':
                        return LeerCadena();
                    case 'n':
                        EsperarLiteral("null");
                        return null;
                }

                if (_estricto)
                {
                    if (proximo == 't')
                    {
                        EsperarLiteral("true");
                        return true;
                    }

                    if (proximo == 'f')
                    {
                        EsperarLiteral("false");
                        return false;
                    }

                    if (proximo == '-' || (proximo >= '0' && proximo <= '9'))
                    {
                        return LeerNumero();
                    }
                }

                throw Falla(_estricto
                    ? "se esperaba un objeto, un arreglo, una cadena, un número, un booleano o 'null'"
                    : "se esperaba un objeto, un arreglo, una cadena o 'null'");
            }

            private IDictionary<string, object> LeerObjeto()
            {
                Entrar();
                Esperar('{');
                var resultado = new Dictionary<string, object>(StringComparer.Ordinal);
                SaltarEspacios();
                if (MirarProximo() == '}')
                {
                    _pos++;
                    Salir();
                    return resultado;
                }

                while (true)
                {
                    SaltarEspacios();
                    var clave = LeerCadena();
                    SaltarEspacios();
                    Esperar(':');
                    var valor = LeerValor();
                    if (_estricto && resultado.ContainsKey(clave))
                    {
                        throw Falla($"la clave '{clave}' está repetida en un objeto");
                    }

                    resultado[clave] = valor;
                    SaltarEspacios();
                    var siguiente = LeerCaracter();
                    if (siguiente == ',')
                    {
                        continue;
                    }

                    if (siguiente == '}')
                    {
                        break;
                    }

                    throw Falla("se esperaba ',' o '}' dentro de un objeto");
                }

                Salir();
                return resultado;
            }

            private IList<object> LeerArreglo()
            {
                Entrar();
                Esperar('[');
                var resultado = new List<object>();
                SaltarEspacios();
                if (MirarProximo() == ']')
                {
                    _pos++;
                    Salir();
                    return resultado;
                }

                while (true)
                {
                    resultado.Add(LeerValor());
                    SaltarEspacios();
                    var siguiente = LeerCaracter();
                    if (siguiente == ',')
                    {
                        continue;
                    }

                    if (siguiente == ']')
                    {
                        break;
                    }

                    throw Falla("se esperaba ',' o ']' dentro de un arreglo");
                }

                Salir();
                return resultado;
            }

            private string LeerCadena()
            {
                Esperar('"');
                var sb = new StringBuilder();
                while (true)
                {
                    if (_pos >= _texto.Length)
                    {
                        throw Falla("cadena sin cerrar");
                    }

                    var c = _texto[_pos++];
                    if (c == '"')
                    {
                        break;
                    }

                    if (c == '\\')
                    {
                        if (_pos >= _texto.Length)
                        {
                            throw Falla("escape incompleto");
                        }

                        var escape = _texto[_pos++];
                        switch (escape)
                        {
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case '/': sb.Append('/'); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case 'u':
                                if (_pos + 4 > _texto.Length)
                                {
                                    throw Falla("escape unicode incompleto");
                                }

                                var hex = _texto.Substring(_pos, 4);
                                _pos += 4;
                                sb.Append((char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                                break;
                            default:
                                throw Falla($"escape desconocido '\\{escape}'");
                        }

                        continue;
                    }

                    sb.Append(c);
                }

                return sb.ToString();
            }

            /// <summary>Gramática de JSON: <c>-? (0 | [1-9][0-9]*) (. [0-9]+)? ([eE] [+-]? [0-9]+)?</c>, recorrida de a un carácter
            /// (sin expresiones regulares). Entero sin fracción ni exponente que cabe en <c>long</c> → <c>long</c>; cualquier
            /// otro número válido → <c>double</c> (quien lo lea decide si lo acepta).</summary>
            private object LeerNumero()
            {
                var inicio = _pos;
                var esEntero = true;

                if (_texto[_pos] == '-')
                {
                    _pos++;
                }

                if (_pos >= _texto.Length || _texto[_pos] < '0' || _texto[_pos] > '9')
                {
                    throw Falla("número mal formado");
                }

                if (_texto[_pos] == '0')
                {
                    _pos++;
                }
                else
                {
                    SaltarDigitos();
                }

                if (_pos < _texto.Length && _texto[_pos] == '.')
                {
                    esEntero = false;
                    _pos++;
                    if (SaltarDigitos() == 0)
                    {
                        throw Falla("número mal formado");
                    }
                }

                if (_pos < _texto.Length && (_texto[_pos] == 'e' || _texto[_pos] == 'E'))
                {
                    esEntero = false;
                    _pos++;
                    if (_pos < _texto.Length && (_texto[_pos] == '+' || _texto[_pos] == '-'))
                    {
                        _pos++;
                    }

                    if (SaltarDigitos() == 0)
                    {
                        throw Falla("número mal formado");
                    }
                }

                var token = _texto.Substring(inicio, _pos - inicio);
                if (esEntero && long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var entero))
                {
                    return entero;
                }

                if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var real) && !double.IsInfinity(real))
                {
                    return real;
                }

                throw Falla("número fuera de rango");
            }

            private int SaltarDigitos()
            {
                var cantidad = 0;
                while (_pos < _texto.Length && _texto[_pos] >= '0' && _texto[_pos] <= '9')
                {
                    _pos++;
                    cantidad++;
                }

                return cantidad;
            }

            private void EsperarLiteral(string literal)
            {
                foreach (char c in literal)
                {
                    Esperar(c);
                }
            }

            private char MirarProximo()
            {
                if (_pos >= _texto.Length)
                {
                    throw Falla("fin de texto inesperado");
                }

                return _texto[_pos];
            }

            private char LeerCaracter()
            {
                var c = MirarProximo();
                _pos++;
                return c;
            }

            private void Esperar(char esperado)
            {
                var c = LeerCaracter();
                if (c != esperado)
                {
                    throw Falla($"se esperaba '{esperado}' y vino '{c}'");
                }
            }

            public void SaltarEspacios()
            {
                while (_pos < _texto.Length && char.IsWhiteSpace(_texto[_pos]))
                {
                    _pos++;
                }
            }

            private void Entrar()
            {
                if (++_profundidad > ProfundidadMaxima)
                {
                    throw Falla($"el anidamiento pasa de {ProfundidadMaxima} niveles");
                }
            }

            private void Salir()
            {
                _profundidad--;
            }

            private FormatException Falla(string motivo)
            {
                return new FormatException($"{_prefijo}: {motivo}.");
            }
        }
    }
}
