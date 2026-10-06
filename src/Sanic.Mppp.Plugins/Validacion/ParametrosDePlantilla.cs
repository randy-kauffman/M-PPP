using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using Sanic.Mppp.Plugins.Dominio;
using Sanic.Mppp.Plugins.Plantilla;

namespace Sanic.Mppp.Plugins.Validacion
{
    /// <summary>
    /// Los diez campos de una fila de la plantilla, con el nombre que llevan en `plantilla.estructura` (`campos[].nombre`), en
    /// `plantilla.obligatoriedad` y en el código. Es la ÚNICA lista: un nombre que no esté acá es un parámetro mal cargado.
    /// </summary>
    public static class CamposDeFila
    {
        public const string Gestion = "gestion";
        public const string Clasificacion = "clasificacion";
        public const string TipoIdentificacion = "tipoIdentificacion";
        public const string Moneda = "moneda";
        public const string Banco = "banco";
        public const string NumeroPlan = "numeroPlan";
        public const string NombreBeneficiario = "nombreBeneficiario";
        public const string NumeroIdentificacion = "numeroIdentificacion";
        public const string NumeroCuenta = "numeroCuenta";
        public const string Referencia = "referencia";

        /// <summary>Los diez, en el orden de arriba. Lista nueva en cada llamada.</summary>
        public static IList<string> Todos()
        {
            return new List<string>
            {
                Gestion, Clasificacion, TipoIdentificacion, Moneda, Banco,
                NumeroPlan, NombreBeneficiario, NumeroIdentificacion, NumeroCuenta, Referencia,
            };
        }

        /// <summary>Los que se validan contra `plantilla.listas`: gestion, clasificacion, tipoIdentificacion, moneda y banco.</summary>
        public static IList<string> DeLista()
        {
            return new List<string> { Gestion, Clasificacion, TipoIdentificacion, Moneda, Banco };
        }
    }

    /// <summary>Un valor aceptado de una lista, ya resuelto.</summary>
    public sealed class ValorDeLista
    {
        public ValorDeLista(string valor, string codigoDeBanco)
        {
            Valor = valor;
            CodigoDeBanco = codigoDeBanco;
        }

        /// <summary>El valor canónico: es EXACTAMENTE el nombre del miembro del enum del choice (`Inclusion`, `ACH`, `USD`, `BAC`, `CNA`).</summary>
        public string Valor { get; }

        /// <summary>Solo en la lista de bancos: el código de 3 dígitos (DD-10). En las demás, nulo.</summary>
        public string CodigoDeBanco { get; }
    }

    /// <summary>
    /// Parámetro `plantilla.listas` (D-18a, aprobador 2026-09-21). Forma:
    /// {"gestion":[{"valor":"Inclusion","variantes":["inclusión","alta"]}],"clasificacion":[…],"tipoIdentificacion":[…],"moneda":[…],
    ///  "banco":[{"valor":"BAC","codigo":"102","variantes":["bac credomatic"]}]}
    /// Las claves son los nombres de <see cref="CamposDeFila.DeLista"/>. La comparación no distingue mayúsculas, tildes (ni ñ de n),
    /// ni espacios al inicio y al final, y trata varios espacios seguidos como uno. Las variantes son EXPLÍCITAS: nada de parecidos.
    /// Un parámetro mal cargado es <see cref="FormatException"/> con el motivo (nunca una lista "vacía pero válida"):
    /// JSON ilegible; falta una de las cinco listas o está vacía; un `valor` que no es el nombre exacto de un miembro del enum de
    /// su choice; un `valor` repetido; una variante (o un valor) que, normalizada, apunta a DOS valores distintos de la misma lista;
    /// una variante en blanco; un banco sin `codigo` de exactamente 3 dígitos ASCII; dos bancos con el mismo código.
    /// NO usa System.Text.Json: DataContractJsonSerializer.
    /// </summary>
    public sealed class ListasPlantilla
    {
        /// <summary>Por lista (clave exacta de <see cref="CamposDeFila.DeLista"/>): texto normalizado → valor resuelto. La
        /// misma instancia de <see cref="ValorDeLista"/> queda referenciada tanto desde su propio valor como desde cada
        /// una de sus variantes.</summary>
        private readonly IDictionary<string, IDictionary<string, ValorDeLista>> _porLista;

        private ListasPlantilla(IDictionary<string, IDictionary<string, ValorDeLista>> porLista)
        {
            _porLista = porLista;
        }

        public static ListasPlantilla DesdeJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new FormatException("El parámetro de listas de la plantilla está vacío.");

            ListasPlantillaDto dto;
            try
            {
                dto = LimitesLectura.DeserializarJson<ListasPlantillaDto>(json);
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException || ex is StackOverflowException || ex is System.Threading.ThreadAbortException))
            {
                throw new FormatException($"El parámetro de listas de la plantilla no es un JSON válido: {ex.Message}", ex);
            }

            if (dto == null)
                throw new FormatException("El parámetro de listas de la plantilla no es un JSON válido.");

            var porLista = new Dictionary<string, IDictionary<string, ValorDeLista>>(StringComparer.Ordinal)
            {
                [CamposDeFila.Gestion] = ArmarLista(CamposDeFila.Gestion, dto.Gestion, typeof(Gestion), esBanco: false),
                [CamposDeFila.Clasificacion] = ArmarLista(CamposDeFila.Clasificacion, dto.Clasificacion, typeof(Clasificacion), esBanco: false),
                [CamposDeFila.TipoIdentificacion] = ArmarLista(CamposDeFila.TipoIdentificacion, dto.TipoIdentificacion, typeof(TipoDeIdentificacion), esBanco: false),
                [CamposDeFila.Moneda] = ArmarLista(CamposDeFila.Moneda, dto.Moneda, typeof(Moneda), esBanco: false),
                [CamposDeFila.Banco] = ArmarLista(CamposDeFila.Banco, dto.Banco, typeof(Banco), esBanco: true),
            };

            return new ListasPlantilla(porLista);
        }

        /// <summary>`^[0-9]{3}$` sin expresión regular (revisión de código, 2026-09-21: para un patrón de tres
        /// caracteres `RegexOptions.Compiled` no aporta nada y compilarla emite código dinámico al inicializar el
        /// tipo, un riesgo para el sandbox de Dataverse). Solo dígitos ASCII (D-18a): un dígito de otro alfabeto
        /// (ej. el arábigo-índico) no cuenta como dígito acá, igual que <see cref="CatalogosDeValidacion.EsCodigoDePlanValido"/>.</summary>
        private static bool EsCodigoDeBancoValido(string codigo)
        {
            if (codigo == null || codigo.Length != 3)
                return false;

            foreach (char c in codigo)
            {
                if (c < '0' || c > '9')
                    return false;
            }

            return true;
        }

        private static IDictionary<string, ValorDeLista> ArmarLista(string nombreLista, List<ValorDeListaDto> items, Type tipoDelChoice, bool esBanco)
        {
            if (items == null || items.Count == 0)
                throw new FormatException($"El parámetro de listas de la plantilla no trae valores para la lista '{nombreLista}'.");

            string[] nombresDelChoice = Enum.GetNames(tipoDelChoice);
            var normalizados = new Dictionary<string, ValorDeLista>(StringComparer.Ordinal);
            var valoresVistos = new HashSet<string>(StringComparer.Ordinal);
            var codigosVistos = esBanco ? new HashSet<string>(StringComparer.Ordinal) : null;

            foreach (ValorDeListaDto item in items)
            {
                if (item == null)
                    throw new FormatException($"El parámetro de listas de la plantilla trae un elemento nulo en la lista '{nombreLista}'.");

                string valor = item.Valor;
                if (string.IsNullOrWhiteSpace(valor) || !nombresDelChoice.Contains(valor, StringComparer.Ordinal))
                    throw new FormatException($"El parámetro de listas de la plantilla trae en '{nombreLista}' un valor ('{valor}') que no es un miembro del choice.");

                if (!valoresVistos.Add(valor))
                    throw new FormatException($"El parámetro de listas de la plantilla repite el valor '{valor}' en la lista '{nombreLista}'.");

                string codigoDeBanco = null;
                if (esBanco)
                {
                    codigoDeBanco = item.Codigo;
                    if (string.IsNullOrEmpty(codigoDeBanco) || !EsCodigoDeBancoValido(codigoDeBanco))
                        throw new FormatException($"El parámetro de listas de la plantilla: el banco '{valor}' trae un código inválido ('{codigoDeBanco}'), tiene que ser de exactamente 3 dígitos.");

                    if (!codigosVistos.Add(codigoDeBanco))
                        throw new FormatException($"El parámetro de listas de la plantilla repite el código de banco '{codigoDeBanco}'.");
                }

                var valorDeLista = new ValorDeLista(valor, codigoDeBanco);
                AgregarNormalizado(normalizados, nombreLista, valor, valorDeLista);

                if (item.Variantes != null)
                {
                    foreach (string variante in item.Variantes)
                    {
                        if (string.IsNullOrWhiteSpace(variante))
                            throw new FormatException($"El parámetro de listas de la plantilla trae una variante en blanco en la lista '{nombreLista}' (valor '{valor}').");

                        AgregarNormalizado(normalizados, nombreLista, variante, valorDeLista);
                    }
                }
            }

            return normalizados;
        }

        /// <summary>Agrega <paramref name="texto"/> normalizado al diccionario de la lista, apuntando a <paramref name="valorDeLista"/>.
        /// Si esa forma normalizada ya apunta a OTRO valor, es la ambigüedad que D-18a prohíbe. Que dos entradas (el propio
        /// valor y una de sus variantes) apunten al MISMO <see cref="ValorDeLista"/> no es un problema.</summary>
        private static void AgregarNormalizado(IDictionary<string, ValorDeLista> normalizados, string nombreLista, string texto, ValorDeLista valorDeLista)
        {
            string clave = Normalizar(texto);
            if (normalizados.TryGetValue(clave, out ValorDeLista existente) && !ReferenceEquals(existente, valorDeLista))
                throw new FormatException($"El parámetro de listas de la plantilla tiene una ambigüedad en '{nombreLista}': '{texto.Trim()}' podría ser '{existente.Valor}' o '{valorDeLista.Valor}'.");

            normalizados[clave] = valorDeLista;
        }

        /// <summary>
        /// El valor aceptado que corresponde a lo que escribió el cliente, o nulo si no corresponde a ninguno (también si vino
        /// nulo o en blanco). <paramref name="lista"/> es uno de <see cref="CamposDeFila.DeLista"/>; otro nombre es
        /// <see cref="ArgumentException"/>.
        /// </summary>
        public ValorDeLista Resolver(string lista, string recibido)
        {
            if (string.IsNullOrEmpty(lista) || !_porLista.TryGetValue(lista, out IDictionary<string, ValorDeLista> normalizados))
                throw new ArgumentException($"'{lista}' no es una de las listas de plantilla ({string.Join(", ", CamposDeFila.DeLista())}).", nameof(lista));

            if (string.IsNullOrWhiteSpace(recibido))
                return null;

            return normalizados.TryGetValue(Normalizar(recibido), out ValorDeLista valor) ? valor : null;
        }

        /// <summary>La forma en que se comparan dos textos de lista: minúsculas invariantes, sin tildes, sin espacios a los lados, espacios internos de a uno. Nulo → vacío.</summary>
        public static string Normalizar(string texto)
        {
            if (string.IsNullOrEmpty(texto))
                return string.Empty;

            // PRIMERO se descompone (NFD) y se quitan las marcas diacríticas; RECIÉN DESPUÉS pasa a minúsculas invariantes.
            // En ese orden el resultado no depende de la cultura del servidor (ej. 'İ', que en net462 y en net10 se
            // descompone igual: 'I' + marca de punto encima, y la marca se quita antes de bajar a minúscula).
            string descompuesto = texto.Normalize(NormalizationForm.FormD);
            var sinDiacriticos = new StringBuilder(descompuesto.Length);
            foreach (char caracter in descompuesto)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
                    sinDiacriticos.Append(caracter);
            }

            string minusculas = sinDiacriticos.ToString().ToLowerInvariant().Trim();

            var resultado = new StringBuilder(minusculas.Length);
            var huboEspacio = false;
            foreach (char caracter in minusculas)
            {
                if (char.IsWhiteSpace(caracter))
                {
                    if (!huboEspacio)
                        resultado.Append(' ');
                    huboEspacio = true;
                }
                else
                {
                    resultado.Append(caracter);
                    huboEspacio = false;
                }
            }

            return resultado.ToString();
        }

        [DataContract]
        private sealed class ListasPlantillaDto
        {
            [DataMember(Name = "gestion")]
            public List<ValorDeListaDto> Gestion { get; set; }

            [DataMember(Name = "clasificacion")]
            public List<ValorDeListaDto> Clasificacion { get; set; }

            [DataMember(Name = "tipoIdentificacion")]
            public List<ValorDeListaDto> TipoIdentificacion { get; set; }

            [DataMember(Name = "moneda")]
            public List<ValorDeListaDto> Moneda { get; set; }

            [DataMember(Name = "banco")]
            public List<ValorDeListaDto> Banco { get; set; }
        }

        [DataContract]
        private sealed class ValorDeListaDto
        {
            [DataMember(Name = "valor")]
            public string Valor { get; set; }

            [DataMember(Name = "codigo")]
            public string Codigo { get; set; }

            [DataMember(Name = "variantes")]
            public List<string> Variantes { get; set; }
        }
    }

    /// <summary>
    /// Parámetro `plantilla.obligatoriedad` (D-18c, DN-03): una lista de excepciones sobre "todo obligatorio". Forma:
    /// {"porDefecto":"obligatorio","opcionales":[{"campo":"referencia"}],
    ///  "reglas":[{"gestion":"Exclusion","clasificacion":"*","formato":"*","opcionales":["moneda","banco"]}],
    ///  "porGestion":{"Exclusion":{"opcionales":[{"campo":"numeroCuenta"}]},"Modificacion":{"obligatorios":[{"campo":"referencia"}]}}}
    /// `gestion` y `clasificacion` (de `reglas`): nombre exacto del miembro del enum, o `*`. `formato`: `06`, `10`, `11` o `*`. Los
    /// campos son de <see cref="CamposDeFila.Todos"/>. `reglas` puede faltar o venir vacía; `opcionales` también.
    /// <see cref="FormatException"/> con el motivo si: JSON ilegible; `porDefecto` distinto de `obligatorio` (es lo único que
    /// existe hoy: otra cosa no se interpreta); un campo, gestión, clasificación o formato desconocido; una regla sin alguna de
    /// sus tres dimensiones; una regla sin opcionales.
    ///
    /// <para><b>`porGestion` (DN-03, aprobador 2026-09-26) y `reglas` CONVIVEN, uno no reemplaza al otro.</b> `reglas` es la
    /// matriz completa gestión×clasificación×formato y solo puede aflojar; `porGestion` es el atajo más simple —solo por
    /// gestión— y ADEMÁS puede endurecer con `obligatorios`. `porGestion` se aplica ENCIMA de `porDefecto`+`opcionales`+`reglas`:
    /// primero se arma el conjunto de opcionales de siempre (sin tocar ese cálculo), después la gestión de la fila, si tiene
    /// una entrada en `porGestion`, SUMA sus `opcionales` y RESTA sus `obligatorios` (si un campo queda en las dos listas de la
    /// MISMA entrada, gana `obligatorios`: ante la duda, se endurece). Una gestión vacía o inválida en la fila (nulo) nunca
    /// mira `porGestion`: rige solo el bloque global, igual que con `reglas`. Los nombres de gestión de `porGestion` se
    /// comparan sin distinguir caja ni tildes (<see cref="ListasPlantilla.Normalizar"/>, reusado, no una segunda forma); dos
    /// claves que normalizan a la misma gestión son una ambigüedad que revienta, igual que hace <see cref="ListasPlantilla"/>
    /// con dos variantes que apuntan a valores distintos. Una entrada sin `opcionales` NI `obligatorios` (o con las dos listas
    /// vacías) es una regla que alguien cree que puso y no puso: revienta, mismo criterio que "regla sin opcionales" de
    /// `reglas`. `DataContractJsonSerializer` no puede leer `porGestion` como `Dictionary&lt;string,T&gt;` (lo serializa como
    /// arreglo Key/Value, no como el objeto plano `{"Exclusion":{...}}` que pide esta forma): se lee con el mismo parser JSON
    /// manual, chico y a propósito, que <see cref="Dominio.CatalogoDeCodigosRpa"/> ya usa para `rpa.codigos` por el mismo
    /// motivo (ver <see cref="AnalizadorJson"/>, extraído a su propio archivo en DN-05); el resto del parámetro sigue leyéndose con
    /// `DataContractJsonSerializer`, sin tocarlo.</para>
    /// </summary>
    public sealed class ObligatoriedadPlantilla
    {
        /// <summary>Todos los nombres válidos de campo (<see cref="CamposDeFila.Todos"/>), para no recalcularlos por cada opcional.</summary>
        private static readonly HashSet<string> CamposValidos = new HashSet<string>(CamposDeFila.Todos(), StringComparer.Ordinal);

        /// <summary>Nombre normalizado (<see cref="ListasPlantilla.Normalizar"/>) → miembro del enum, para resolver las
        /// claves de `porGestion` sin distinguir caja ni tildes (DN-03), sin recalcularlo por cada clave.</summary>
        private static readonly IDictionary<string, Gestion> GestionesPorNombreNormalizado = Enum.GetNames(typeof(Gestion))
            .ToDictionary(nombre => ListasPlantilla.Normalizar(nombre), nombre => (Gestion)Enum.Parse(typeof(Gestion), nombre), StringComparer.Ordinal);

        private readonly ISet<string> _opcionalesBase;
        private readonly IList<ReglaDeObligatoriedad> _reglas;
        private readonly IDictionary<Gestion, AjustePorGestion> _porGestion;

        private ObligatoriedadPlantilla(ISet<string> opcionalesBase, IList<ReglaDeObligatoriedad> reglas, IDictionary<Gestion, AjustePorGestion> porGestion)
        {
            _opcionalesBase = opcionalesBase;
            _reglas = reglas;
            _porGestion = porGestion;
        }

        public static ObligatoriedadPlantilla DesdeJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new FormatException("El parámetro de obligatoriedad de la plantilla está vacío.");

            ObligatoriedadDto dto;
            try
            {
                dto = LimitesLectura.DeserializarJson<ObligatoriedadDto>(json);
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException || ex is StackOverflowException || ex is System.Threading.ThreadAbortException))
            {
                throw new FormatException($"El parámetro de obligatoriedad de la plantilla no es un JSON válido: {ex.Message}", ex);
            }

            if (dto == null)
                throw new FormatException("El parámetro de obligatoriedad de la plantilla no es un JSON válido.");

            // Hoy "obligatorio" es el ÚNICO valor por defecto que el motor sabe interpretar (D-18c): otra cosa, aunque
            // suene razonable, no se adivina.
            if (!string.Equals(dto.PorDefecto, "obligatorio", StringComparison.Ordinal))
                throw new FormatException($"El parámetro de obligatoriedad de la plantilla trae un 'porDefecto' inválido ('{dto.PorDefecto}'): hoy solo existe 'obligatorio'.");

            var opcionalesBase = new HashSet<string>(StringComparer.Ordinal);
            if (dto.Opcionales != null)
            {
                foreach (CampoOpcionalDto opcional in dto.Opcionales)
                {
                    string campo = opcional?.Campo;
                    if (opcional == null || !CamposValidos.Contains(campo))
                        throw new FormatException($"El parámetro de obligatoriedad de la plantilla trae un campo opcional desconocido ('{campo}').");

                    opcionalesBase.Add(campo);
                }
            }

            var reglas = new List<ReglaDeObligatoriedad>();
            if (dto.Reglas != null)
            {
                foreach (ReglaDeObligatoriedadDto reglaDto in dto.Reglas)
                {
                    if (reglaDto == null)
                        throw new FormatException("El parámetro de obligatoriedad de la plantilla trae una regla nula.");

                    // Ninguna de las tres dimensiones se asume "*": una regla a la que le falta una es un parámetro mal
                    // cargado (D-18c: "ante la duda, obligatorio", no "ante la duda, cualquier cosa vale").
                    Gestion? gestion = ParseGestion(reglaDto.Gestion);
                    Clasificacion? clasificacion = ParseClasificacion(reglaDto.Clasificacion);
                    TipoDeFormatoDelPlan? formato = ParseFormato(reglaDto.Formato);

                    if (reglaDto.Opcionales == null || reglaDto.Opcionales.Count == 0)
                        throw new FormatException("El parámetro de obligatoriedad de la plantilla trae una regla sin campos opcionales.");

                    var opcionalesRegla = new HashSet<string>(StringComparer.Ordinal);
                    foreach (string campo in reglaDto.Opcionales)
                    {
                        if (campo == null || !CamposValidos.Contains(campo))
                            throw new FormatException($"El parámetro de obligatoriedad de la plantilla trae una regla con un campo opcional desconocido ('{campo}').");

                        opcionalesRegla.Add(campo);
                    }

                    reglas.Add(new ReglaDeObligatoriedad(gestion, clasificacion, formato, opcionalesRegla));
                }
            }

            var porGestion = LeerPorGestion(json);

            return new ObligatoriedadPlantilla(opcionalesBase, reglas, porGestion);
        }

        /// <summary>
        /// DN-03: lee `porGestion` con el parser manual de <see cref="AnalizadorJson"/> (ver el comentario de esa clase). Vacío
        /// (diccionario sin entradas) si `porGestion` falta o es JSON `null`: es la forma en la que se siembra el parámetro
        /// hasta que el negocio diga qué aflojar en cada gestión, y se comporta EXACTAMENTE como si no existiera.
        /// </summary>
        private static IDictionary<Gestion, AjustePorGestion> LeerPorGestion(string json)
        {
            var resultado = new Dictionary<Gestion, AjustePorGestion>();

            // El documento ya es válido (DataContractJsonSerializer lo aceptó más arriba): este parser solo lo
            // recorre de nuevo para reconstruir 'porGestion'. Si igual revienta, es un problema real de este parser
            // (Falla() ya deja el motivo claro), no un documento mal cargado por segunda vez.
            object documento = AnalizadorJson.Leer(json, "El parámetro de obligatoriedad de la plantilla trae 'porGestion' con un problema de JSON");

            if (!(documento is IDictionary<string, object> raiz) || !raiz.TryGetValue("porGestion", out object crudo) || crudo == null)
                return resultado;

            if (!(crudo is IDictionary<string, object> porGestionObj))
                throw new FormatException("El parámetro de obligatoriedad de la plantilla trae 'porGestion' que no es un objeto.");

            // Clave normalizada (ListasPlantilla.Normalizar) → clave tal cual vino, solo para citarla en el mensaje de
            // ambigüedad (DN-03: dos claves que normalizan a la misma gestión revientan, no "gana la última").
            var clavesVistas = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> par in porGestionObj)
            {
                Gestion gestion = ResolverClaveDeGestion(par.Key, clavesVistas);

                if (!(par.Value is IDictionary<string, object> entrada))
                    throw new FormatException($"El parámetro de obligatoriedad de la plantilla trae en 'porGestion' una entrada de '{par.Key}' que no es un objeto.");

                ISet<string> opcionales = LeerCamposDeEntrada(entrada, "opcionales", par.Key);
                ISet<string> obligatorios = LeerCamposDeEntrada(entrada, "obligatorios", par.Key);

                // DN-03 (aprobador, 2026-09-27): una entrada sin efecto es una regla que alguien cree que puso y no
                // puso. Si el negocio quiere que una gestión no tenga ajuste, no escribe la entrada.
                if (opcionales.Count == 0 && obligatorios.Count == 0)
                    throw new FormatException($"El parámetro de obligatoriedad de la plantilla trae en 'porGestion' una entrada de '{par.Key}' sin 'opcionales' ni 'obligatorios': no ajusta nada.");

                resultado[gestion] = new AjustePorGestion(opcionales, obligatorios);
            }

            return resultado;
        }

        /// <summary>La gestión canónica que corresponde a una clave de `porGestion`, comparada sin distinguir caja ni
        /// tildes (<see cref="ListasPlantilla.Normalizar"/>, reusado). Gestión desconocida, o una segunda clave que
        /// normaliza a una ya vista: <see cref="FormatException"/> (DN-03, aprobador 2026-09-27).</summary>
        private static Gestion ResolverClaveDeGestion(string clave, IDictionary<string, string> clavesVistas)
        {
            string normalizada = ListasPlantilla.Normalizar(clave);
            if (!GestionesPorNombreNormalizado.TryGetValue(normalizada, out Gestion gestion))
                throw new FormatException($"El parámetro de obligatoriedad de la plantilla trae en 'porGestion' una gestión desconocida ('{clave}').");

            if (clavesVistas.TryGetValue(normalizada, out string anterior))
                throw new FormatException($"El parámetro de obligatoriedad de la plantilla tiene una ambigüedad en 'porGestion': '{anterior}' y '{clave}' son la misma gestión ({gestion}).");

            clavesVistas[normalizada] = clave;
            return gestion;
        }

        /// <summary>`entrada[clave]` ("opcionales" u "obligatorios" de una entrada de `porGestion`): un arreglo de
        /// `{"campo":"<nombre>"}`, igual forma que el `opcionales` de arriba. Ausente o `null` → vacío (puede faltar,
        /// DN-03: una entrada trae una de las dos listas, o las dos). Un campo desconocido, o algo que no es la forma
        /// esperada, es <see cref="FormatException"/>.</summary>
        private static ISet<string> LeerCamposDeEntrada(IDictionary<string, object> entrada, string clave, string gestionCruda)
        {
            var resultado = new HashSet<string>(StringComparer.Ordinal);
            if (!entrada.TryGetValue(clave, out object valor) || valor == null)
                return resultado;

            if (!(valor is IList<object> lista))
                throw new FormatException($"El parámetro de obligatoriedad de la plantilla trae en 'porGestion.{gestionCruda}.{clave}' algo que no es un arreglo.");

            foreach (object item in lista)
            {
                if (!(item is IDictionary<string, object> campoObj) || !campoObj.TryGetValue("campo", out object campoValor) || !(campoValor is string campo) || !CamposValidos.Contains(campo))
                    throw new FormatException($"El parámetro de obligatoriedad de la plantilla trae en 'porGestion.{gestionCruda}.{clave}' un campo desconocido.");

                resultado.Add(campo);
            }

            return resultado;
        }

        private static Gestion? ParseGestion(string valor)
        {
            if (valor == null)
                throw new FormatException("El parámetro de obligatoriedad de la plantilla trae una regla sin la dimensión 'gestion' (no se asume '*').");
            if (valor == "*")
                return null;
            if (!Enum.GetNames(typeof(Gestion)).Contains(valor, StringComparer.Ordinal))
                throw new FormatException($"El parámetro de obligatoriedad de la plantilla trae una regla con una gestión desconocida ('{valor}').");
            return (Gestion)Enum.Parse(typeof(Gestion), valor);
        }

        private static Clasificacion? ParseClasificacion(string valor)
        {
            if (valor == null)
                throw new FormatException("El parámetro de obligatoriedad de la plantilla trae una regla sin la dimensión 'clasificacion' (no se asume '*').");
            if (valor == "*")
                return null;
            if (!Enum.GetNames(typeof(Clasificacion)).Contains(valor, StringComparer.Ordinal))
                throw new FormatException($"El parámetro de obligatoriedad de la plantilla trae una regla con una clasificación desconocida ('{valor}').");
            return (Clasificacion)Enum.Parse(typeof(Clasificacion), valor);
        }

        private static TipoDeFormatoDelPlan? ParseFormato(string valor)
        {
            if (valor == null)
                throw new FormatException("El parámetro de obligatoriedad de la plantilla trae una regla sin la dimensión 'formato' (no se asume '*').");

            switch (valor)
            {
                case "*":
                    return null;
                case "06":
                    return TipoDeFormatoDelPlan._06;
                case "10":
                    return TipoDeFormatoDelPlan._10;
                case "11":
                    return TipoDeFormatoDelPlan._11;
                default:
                    throw new FormatException($"El parámetro de obligatoriedad de la plantilla trae una regla con un formato de plan desconocido ('{valor}').");
            }
        }

        /// <summary>
        /// Los campos que NO son obligatorios para esa combinación: los opcionales generales más los de TODAS las reglas que
        /// calzan, y por último el ajuste de `porGestion` de la gestión de la fila (DN-03), si tiene una entrada: sus
        /// `opcionales` se SUMAN y sus `obligatorios` se RESTAN (si un campo queda en las dos, gana `obligatorios`: ante la
        /// duda, se endurece). Una dimensión desconocida (nulo: el valor de la fila no era válido, o el plan no existe) solo
        /// calza con `*` en `reglas`, y NUNCA mira `porGestion` (esa fila ya falló `LISTAS_VALIDAS` por su cuenta; sumarle un
        /// efecto derivado sería ruido). Conjunto nuevo en cada llamada.
        /// </summary>
        public ISet<string> CamposOpcionales(Gestion? gestion, Clasificacion? clasificacion, TipoDeFormatoDelPlan? formato)
        {
            var resultado = new HashSet<string>(_opcionalesBase, StringComparer.Ordinal);
            foreach (ReglaDeObligatoriedad regla in _reglas)
            {
                if (Calza(regla.Gestion, gestion) && Calza(regla.Clasificacion, clasificacion) && Calza(regla.Formato, formato))
                {
                    resultado.UnionWith(regla.Opcionales);
                }
            }

            if (gestion.HasValue && _porGestion.TryGetValue(gestion.Value, out AjustePorGestion ajuste))
            {
                resultado.UnionWith(ajuste.Opcionales);
                resultado.ExceptWith(ajuste.Obligatorios);
            }

            return resultado;
        }

        /// <summary>Una dimensión de la regla (nulo = "*") calza con el valor real de la fila (nulo = desconocido: el valor
        /// de la fila no era válido, o el plan no existe). "*" calza con cualquier cosa, incluido lo desconocido; una
        /// dimensión concreta de la regla NUNCA calza con un valor desconocido (D-18c: ante la duda, obligatorio).</summary>
        private static bool Calza<T>(T? deLaRegla, T? real) where T : struct
        {
            return !deLaRegla.HasValue || (real.HasValue && EqualityComparer<T>.Default.Equals(real.Value, deLaRegla.Value));
        }

        private sealed class ReglaDeObligatoriedad
        {
            public ReglaDeObligatoriedad(Gestion? gestion, Clasificacion? clasificacion, TipoDeFormatoDelPlan? formato, ISet<string> opcionales)
            {
                Gestion = gestion;
                Clasificacion = clasificacion;
                Formato = formato;
                Opcionales = opcionales;
            }

            public Gestion? Gestion { get; }

            public Clasificacion? Clasificacion { get; }

            public TipoDeFormatoDelPlan? Formato { get; }

            public ISet<string> Opcionales { get; }
        }

        /// <summary>El ajuste de `porGestion` (DN-03) para UNA gestión, ya validado: los campos de <see cref="CamposDeFila.Todos"/>
        /// que esa gestión afloja y los que endurece.</summary>
        private sealed class AjustePorGestion
        {
            public AjustePorGestion(ISet<string> opcionales, ISet<string> obligatorios)
            {
                Opcionales = opcionales;
                Obligatorios = obligatorios;
            }

            public ISet<string> Opcionales { get; }

            public ISet<string> Obligatorios { get; }
        }

        [DataContract]
        private sealed class ObligatoriedadDto
        {
            [DataMember(Name = "porDefecto")]
            public string PorDefecto { get; set; }

            [DataMember(Name = "opcionales")]
            public List<CampoOpcionalDto> Opcionales { get; set; }

            [DataMember(Name = "reglas")]
            public List<ReglaDeObligatoriedadDto> Reglas { get; set; }
        }

        [DataContract]
        private sealed class CampoOpcionalDto
        {
            [DataMember(Name = "campo")]
            public string Campo { get; set; }
        }

        [DataContract]
        private sealed class ReglaDeObligatoriedadDto
        {
            [DataMember(Name = "gestion")]
            public string Gestion { get; set; }

            [DataMember(Name = "clasificacion")]
            public string Clasificacion { get; set; }

            [DataMember(Name = "formato")]
            public string Formato { get; set; }

            [DataMember(Name = "opcionales")]
            public List<string> Opcionales { get; set; }
        }
    }
}
