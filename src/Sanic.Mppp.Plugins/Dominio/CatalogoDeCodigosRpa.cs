using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Sanic.Mppp.Plugins.Dominio
{
    /// <summary>Un código de `rpa.codigos` ya resuelto (diseno/03 §3, "Los dos textos").</summary>
    public sealed class CodigoRpa
    {
        public CodigoRpa(string cliente, string nota)
        {
            Cliente = cliente;
            Nota = nota;
        }

        /// <summary>El texto que traduce el `codigo` para el CLIENTE (`sanic_mensaje`). `null` en un código de éxito (`OK`).</summary>
        public string Cliente { get; }

        /// <summary>El texto interno, para quien lea el catálogo. No es lo que se escribe en `sanic_notainterna` (eso lo manda el bot en `mensaje`).</summary>
        public string Nota { get; }
    }

    /// <summary>
    /// Parámetro `rpa.codigos` (diseno/03 §3): {"OK":{"cliente":null,"nota":"..."},"RECH-GEN":{"cliente":"...","nota":"..."}}.
    /// Las claves son códigos que el BOT manda tal cual (no son un choice de Dataverse): la comparación es EXACTA,
    /// sin normalizar (a diferencia de `plantilla.listas`, que tolera variantes de texto escrito por un cliente).
    /// Un código que NO está en el catálogo es `MPPP-RPA-005` (falla del bot, no de este parser); ESTE parser solo
    /// falla si el JSON del PARÁMETRO está mal formado (parámetro mal cargado por una persona), y ahí es
    /// <see cref="FormatException"/>, igual que <see cref="Validacion.ListasPlantilla"/>.
    ///
    /// No usa `DataContractJsonSerializer`: esa clase serializa un `Dictionary&lt;string,T&gt;` como un arreglo de
    /// pares clave/valor, no como el objeto plano `{"CODIGO": {...}}` que trae este parámetro (claves arbitrarias,
    /// no conocidas de antemano). Por eso hay un parser manual, chico y a propósito, del mismo espíritu que el
    /// escrito a mano en <see cref="ArmadorJsonDelRpa"/> para lo mismo, al revés (escribir, no leer). No se agrega
    /// ninguna dependencia nueva.
    /// </summary>
    public sealed class CatalogoDeCodigosRpa
    {
        private readonly IDictionary<string, CodigoRpa> _porCodigo;

        private CatalogoDeCodigosRpa(IDictionary<string, CodigoRpa> porCodigo)
        {
            _porCodigo = porCodigo;
        }

        /// <summary>El código resuelto, o `null` si no está en el catálogo (eso lo traduce quien llama a `MPPP-RPA-005`).</summary>
        public CodigoRpa Resolver(string codigo)
        {
            if (codigo == null)
            {
                return null;
            }

            return _porCodigo.TryGetValue(codigo, out var valor) ? valor : null;
        }

        public static CatalogoDeCodigosRpa DesdeJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new FormatException("El parámetro 'rpa.codigos' está vacío.");
            }

            var lector = new LectorJson(json);
            var raiz = lector.LeerObjetoDeObjetos();

            var porCodigo = new Dictionary<string, CodigoRpa>(StringComparer.Ordinal);
            foreach (var entrada in raiz)
            {
                var campos = entrada.Value;
                string cliente = campos.TryGetValue("cliente", out var c) ? c : throw new FormatException(
                    $"El parámetro 'rpa.codigos' no trae 'cliente' para el código '{entrada.Key}'.");
                string nota = campos.TryGetValue("nota", out var n) ? n : throw new FormatException(
                    $"El parámetro 'rpa.codigos' no trae 'nota' para el código '{entrada.Key}'.");

                porCodigo[entrada.Key] = new CodigoRpa(cliente, nota);
            }

            return new CatalogoDeCodigosRpa(porCodigo);
        }

        /// <summary>
        /// Parser JSON manual, deliberadamente chico: solo entiende lo que `rpa.codigos` necesita — un objeto plano
        /// cuyos valores son objetos planos de cadenas (o `null`). Cualquier otra forma (arreglos, números,
        /// booleanos, anidamiento más profundo) es <see cref="FormatException"/>: este parser no es de uso general.
        /// </summary>
        private sealed class LectorJson
        {
            private readonly string _texto;
            private int _pos;

            public LectorJson(string texto)
            {
                _texto = texto;
                _pos = 0;
            }

            public IDictionary<string, IDictionary<string, string>> LeerObjetoDeObjetos()
            {
                SaltarEspacios();
                Esperar('{');
                var resultado = new Dictionary<string, IDictionary<string, string>>(StringComparer.Ordinal);
                SaltarEspacios();
                if (MirarProximo() == '}')
                {
                    _pos++;
                    SaltarEspacios();
                    ExigirFin();
                    return resultado;
                }

                while (true)
                {
                    SaltarEspacios();
                    var clave = LeerCadena();
                    SaltarEspacios();
                    Esperar(':');
                    SaltarEspacios();
                    resultado[clave] = LeerObjetoDeCadenas();
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

                    throw Falla("se esperaba ',' o '}'");
                }

                SaltarEspacios();
                ExigirFin();
                return resultado;
            }

            private IDictionary<string, string> LeerObjetoDeCadenas()
            {
                Esperar('{');
                var resultado = new Dictionary<string, string>(StringComparer.Ordinal);
                SaltarEspacios();
                if (MirarProximo() == '}')
                {
                    _pos++;
                    return resultado;
                }

                while (true)
                {
                    SaltarEspacios();
                    var clave = LeerCadena();
                    SaltarEspacios();
                    Esperar(':');
                    SaltarEspacios();
                    resultado[clave] = LeerCadenaOJson();
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

                    throw Falla("se esperaba ',' o '}' dentro de un código");
                }

                return resultado;
            }

            /// <summary>Una cadena, o `null` (el literal JSON), como valor de un campo (`cliente` acepta `null`).</summary>
            private string LeerCadenaOJson()
            {
                if (MirarProximo() == 'n')
                {
                    Esperar('n');
                    Esperar('u');
                    Esperar('l');
                    Esperar('l');
                    return null;
                }

                return LeerCadena();
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

            private void SaltarEspacios()
            {
                while (_pos < _texto.Length && char.IsWhiteSpace(_texto[_pos]))
                {
                    _pos++;
                }
            }

            private void ExigirFin()
            {
                if (_pos != _texto.Length)
                {
                    throw Falla("texto sobrante después del objeto principal");
                }
            }

            private FormatException Falla(string motivo)
            {
                return new FormatException($"El parámetro 'rpa.codigos' no es un JSON válido: {motivo}.");
            }
        }
    }
}
