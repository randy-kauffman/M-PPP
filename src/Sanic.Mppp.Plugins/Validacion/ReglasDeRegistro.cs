using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sanic.Mppp.Plugins.Dominio;
using Sanic.Mppp.Plugins.Plantilla;

namespace Sanic.Mppp.Plugins.Validacion
{
    /// <summary>Un plan ACTIVO del maestro (`sanic_mppp_tbl_plan`), tal como lo entrega la capa de datos. Sin SDK.</summary>
    public sealed class PlanDelCatalogo
    {
        public PlanDelCatalogo(Guid id, string codigo, TipoDeFormatoDelPlan tipoFormato, Moneda moneda, Guid? clienteId = null,
            Canal canal = Canal.BancaEnLinea, bool requierePreregistro = true)
        {
            Id = id;
            Codigo = codigo;
            TipoFormato = tipoFormato;
            Moneda = moneda;
            ClienteId = clienteId;
            Canal = canal;
            RequierePreregistro = requierePreregistro;
        }

        public Guid Id { get; }

        /// <summary>`sanic_codigo`: `^[A-Z0-9]{4}$`.</summary>
        public string Codigo { get; }

        public TipoDeFormatoDelPlan TipoFormato { get; }

        public Moneda Moneda { get; }

        /// <summary>
        /// DN-01 (aprobador, 2026-09-26): el cliente dueño de este plan (`sanic_clienteid` del Plan). Nulo cuando quien
        /// arma el catálogo no lo cargó (el modo Plan no lo necesita). En modo Cliente, un plan sin dueño conocido
        /// NUNCA se da por autorizado: no se inventa un cliente (mismo criterio de "nunca se adivina" de esta clase).
        /// </summary>
        public Guid? ClienteId { get; }

        /// <summary>
        /// DN-06 (aprobador, 2026-11-24): el canal del plan (`sanic_canal`). Decide qué matriz de obligatoriedad se usa. Ausente
        /// = <see cref="Canal.BancaEnLinea"/>: es el valor de los planes que ya existían.
        /// </summary>
        public Canal Canal { get; }

        /// <summary>
        /// DN-06: `sanic_requierepreregistro`. Si es falso, la regla `PLAN_REQUIERE_PREREGISTRO` rechaza todas las filas del plan.
        /// Ausente = verdadero (el valor de los planes que ya existían).
        /// </summary>
        public bool RequierePreregistro { get; }
    }

    /// <summary>
    /// DN-01 (aprobador, 2026-09-26): la vía que consulta `AUTORIZACION_CORREO_PLAN`. Las dos conviven en el modelo
    /// (`AutorizacionPlan`, la de hoy, y `AutorizacionCliente`, la nueva); el parámetro `autorizacion.modo` decide
    /// cuál mira. El código de la regla, su orden, su nivel y su efecto NO cambian: lo único que cambia es DÓNDE
    /// busca la autorización (diseno/DIRECTRICES-NUEVAS.md, "Diseño resuelto · DN-01 · Modelo").
    /// </summary>
    public enum ModoDeAutorizacion
    {
        /// <summary>El comportamiento de hoy, y el FALLBACK QUEMADO cuando el parámetro falta o trae basura: mira
        /// `AutorizacionPlan(correo, plan)`.</summary>
        Plan,

        /// <summary>Mira `AutorizacionCliente(correo, cliente dueño del plan)`.</summary>
        Cliente,
    }

    /// <summary>
    /// Resuelve el parámetro `autorizacion.modo` (DN-01). Función pura: no lee Dataverse, no lanza — un parámetro
    /// ausente o con basura DEGRADA al fallback quemado (<see cref="ModoDeAutorizacion.Plan"/>), nunca tumba la
    /// validación (mismo criterio que `servicio.cuenta` y `rpa.horas.tolerancia`, diseno/02 §2.5).
    /// </summary>
    public static class ModoDeAutorizacionParametro
    {
        /// <summary>`sanic_nombre` del parámetro en `sanic_mppp_tbl_parametro`.</summary>
        public const string Nombre = "autorizacion.modo";

        private const string ValorCliente = "cliente";

        /// <summary>
        /// 'cliente' (sin distinguir mayúsculas, recortado a los lados) → <see cref="ModoDeAutorizacion.Cliente"/>.
        /// Cualquier otro valor —nulo, vacío, espacios, "plan", o basura como "clienteX"— DEGRADA al fallback quemado:
        /// <see cref="ModoDeAutorizacion.Plan"/>, el comportamiento de hoy. Un valor mal cargado NO es un error de
        /// programación acá: ya es un patrón del proyecto. Sus hermanos: `servicio.cuenta`
        /// (<see cref="Sanic.Mppp.Plugins.Steps.BaseDeStep.ParametroCuentaDeServicio"/>) y `rpa.horas.tolerancia`
        /// (<see cref="Sanic.Mppp.Plugins.Api.RegistrarResultadoRpaApi.ParametroToleranciaHoras"/>) hacen lo mismo: un
        /// parámetro de configuración que falta o viene con basura degrada al control conocido, nunca tumba la
        /// validación. Nunca lanza.
        /// </summary>
        public static ModoDeAutorizacion Resolver(string valorCrudo)
        {
            var recortado = (valorCrudo ?? string.Empty).Trim();
            return string.Equals(recortado, ValorCliente, StringComparison.OrdinalIgnoreCase)
                ? ModoDeAutorizacion.Cliente
                : ModoDeAutorizacion.Plan;
        }
    }

    /// <summary>
    /// Todo lo que las reglas de registro consultan, cargado UNA vez por ejecución (diseno/03 §1 paso 6: nunca una consulta por
    /// fila). Inmutable. Un catálogo mal armado es un error real (<see cref="ArgumentException"/>), no una regla fallida:
    /// algún argumento nulo; la estructura no trae EXACTAMENTE los diez campos de <see cref="CamposDeFila.Todos"/> por su
    /// `Nombre` (ni uno menos, ni uno de más, ni uno sin nombre); un plan nulo, con código que no cumple `^[A-Z0-9]{4}$`, o dos
    /// planes con el mismo código.
    /// </summary>
    public sealed class CatalogosDeValidacion
    {
        private readonly IDictionary<string, CampoPlantilla> _camposPorNombre;
        private readonly IDictionary<string, PlanDelCatalogo> _planesPorCodigo;
        private readonly HashSet<Guid> _planesAutorizados;
        private readonly ModoDeAutorizacion _modo;
        private readonly IDictionary<Guid, Guid?> _clienteDelPlanPorId;
        private readonly HashSet<Guid> _clientesAutorizados;

        /// <param name="planesAutorizadosDelRemitente">
        /// Los ids de plan sobre los que el remitente tiene una autorización ACTIVA (Autorizado, Plan, Cliente y la propia
        /// AutorizacionPlan activos; diseno/02 §2.4). La evidencia NO cuenta (D-42, aprobador 2026-09-21): es informativa. Se copia.
        /// Solo se mira en modo <see cref="ModoDeAutorizacion.Plan"/> (DN-01).
        /// </param>
        /// <param name="modo">
        /// DN-01 (aprobador, 2026-09-26): qué vía consulta <c>EstaAutorizado</c>. Ausente = <see cref="ModoDeAutorizacion.Plan"/>
        /// (el comportamiento de hoy, y el fallback quemado de <see cref="ModoDeAutorizacionParametro"/>).
        /// </param>
        /// <param name="clientesAutorizadosDelRemitente">
        /// DN-01: los ids de CLIENTE sobre los que el remitente tiene una autorización ACTIVA (AutorizacionCliente). Solo se
        /// mira en modo <see cref="ModoDeAutorizacion.Cliente"/>; nulo se trata como vacío (nunca lanza: mismo criterio de
        /// "nunca tumba la validación" que el resto de esta clase).
        /// </param>
        public CatalogosDeValidacion(
            ConfiguracionPlantilla estructura,
            ListasPlantilla listas,
            ObligatoriedadPlantilla obligatoriedad,
            IEnumerable<PlanDelCatalogo> planesActivos,
            IEnumerable<Guid> planesAutorizadosDelRemitente,
            ModoDeAutorizacion modo = ModoDeAutorizacion.Plan,
            IEnumerable<Guid> clientesAutorizadosDelRemitente = null,
            FormatosDelPlan formatos = null,
            LargosDeCuenta largosDeCuenta = null,
            ObligatoriedadPlantilla obligatoriedadConectividad = null)
        {
            // DN-06: la matriz del canal Conectividad es OPCIONAL al armar el catálogo. Solo se exige (ObligatoriedadPara) cuando
            // una fila es de un plan de Conectividad: así un ambiente sin el parámetro cargado sigue validando Banca en Linea.
            ObligatoriedadConectividad = obligatoriedadConectividad;
            // DN-05: nulos admitidos para no romper a quien arma el catálogo sin ellos (pruebas, normalización de planes).
            // Una regla ACTIVA que los necesita y no los encuentra lanza InvalidOperationException (error de configuración,
            // falla cerrado): nunca se da por buena una fila que no se pudo comprobar.
            Formatos = formatos;
            LargosDeCuenta = largosDeCuenta;

            if (estructura == null)
            {
                throw new ArgumentNullException(nameof(estructura));
            }

            if (listas == null)
            {
                throw new ArgumentNullException(nameof(listas));
            }

            if (obligatoriedad == null)
            {
                throw new ArgumentNullException(nameof(obligatoriedad));
            }

            if (planesActivos == null)
            {
                throw new ArgumentNullException(nameof(planesActivos));
            }

            if (planesAutorizadosDelRemitente == null)
            {
                throw new ArgumentNullException(nameof(planesAutorizadosDelRemitente));
            }

            // La estructura tiene que traer EXACTAMENTE los diez campos de CamposDeFila.Todos(), por su Nombre: ni uno
            // menos, ni uno de más, ni uno sin nombre, ni uno repetido (diseno/03 §1 paso 5: el resto del código asume esto).
            var nombresEsperados = new HashSet<string>(CamposDeFila.Todos(), StringComparer.Ordinal);
            var camposPorNombre = new Dictionary<string, CampoPlantilla>(StringComparer.Ordinal);
            foreach (var campo in estructura.Campos ?? new List<CampoPlantilla>())
            {
                if (campo == null || string.IsNullOrWhiteSpace(campo.Nombre))
                {
                    throw new ArgumentException("La estructura de la plantilla trae un campo sin nombre.", nameof(estructura));
                }

                if (!nombresEsperados.Contains(campo.Nombre))
                {
                    throw new ArgumentException($"La estructura de la plantilla trae un campo que no es de la fila ('{campo.Nombre}').", nameof(estructura));
                }

                if (camposPorNombre.ContainsKey(campo.Nombre))
                {
                    throw new ArgumentException($"La estructura de la plantilla repite el campo '{campo.Nombre}'.", nameof(estructura));
                }

                // Copia, no la referencia (revisión de código, 2026-09-21): "inmutable" de palabra no alcanza cuando
                // CampoPlantilla tiene setters públicos. Mutar el campo que se pasó, o el que devuelve Campo(), no
                // puede aflojar la validación de las filas que todavía faltan.
                camposPorNombre[campo.Nombre] = Copiar(campo);
            }

            if (camposPorNombre.Count != nombresEsperados.Count)
            {
                throw new ArgumentException("La estructura de la plantilla no trae los diez campos de la fila.", nameof(estructura));
            }

            // Un plan nulo, con código sin normalizar (^[A-Z0-9]{4}$), o repetido: es el catálogo el que está mal armado,
            // no una fila (diseno/02 §2.2).
            var planesPorCodigo = new Dictionary<string, PlanDelCatalogo>(StringComparer.Ordinal);
            var clienteDelPlanPorId = new Dictionary<Guid, Guid?>();
            foreach (var plan in planesActivos)
            {
                if (plan == null)
                {
                    throw new ArgumentException("Los planes activos traen un plan nulo.", nameof(planesActivos));
                }

                if (!EsCodigoDePlanValido(plan.Codigo))
                {
                    throw new ArgumentException($"El plan '{plan.Codigo}' no tiene un código de plan válido (tiene que cumplir ^[A-Z0-9]{{4}}$).", nameof(planesActivos));
                }

                if (planesPorCodigo.ContainsKey(plan.Codigo))
                {
                    throw new ArgumentException($"Hay dos planes activos con el código '{plan.Codigo}'.", nameof(planesActivos));
                }

                planesPorCodigo[plan.Codigo] = plan;
                // DN-01: indexado por Id (no por código) para que EstaAutorizado() resuelva el modo Cliente sin
                // recorrer la colección en cada llamada. ClienteId nulo se guarda tal cual: en modo Cliente un plan
                // sin dueño conocido nunca se da por autorizado (no se inventa un cliente).
                clienteDelPlanPorId[plan.Id] = plan.ClienteId;
            }

            Listas = listas;
            Obligatoriedad = obligatoriedad;
            _camposPorNombre = camposPorNombre;
            _planesPorCodigo = planesPorCodigo;
            _clienteDelPlanPorId = clienteDelPlanPorId;
            // Copia: cambiar la colección después de construir el catálogo no lo cambia (revisión de código, 2026-09-21).
            // D-42 (aprobador, 2026-09-21): la evidencia no cuenta, así que basta con el SET de planes autorizados (sin el
            // tercer estado "sin evidencia" que tenía la versión anterior).
            _planesAutorizados = new HashSet<Guid>(planesAutorizadosDelRemitente);
            _modo = modo;
            // DN-01: nulo se trata como vacío (nunca lanza, mismo criterio de "nunca tumba la validación" que el resto
            // de esta clase); solo se mira en modo Cliente.
            _clientesAutorizados = new HashSet<Guid>(clientesAutorizadosDelRemitente ?? new Guid[0]);
        }

        public ListasPlantilla Listas { get; }

        public ObligatoriedadPlantilla Obligatoriedad { get; }

        /// <summary>DN-06: `plantilla.obligatoriedad.conectividad`, la matriz del canal Conectividad. Puede ser nula (ver el constructor).</summary>
        public ObligatoriedadPlantilla ObligatoriedadConectividad { get; }

        /// <summary>
        /// DN-06: la matriz de obligatoriedad del canal. Banca en Linea usa la de siempre; Conectividad usa su parámetro propio y,
        /// si no está cargado, es un error real que lo nombra (falla cerrado: no se valida con la matriz equivocada).
        /// </summary>
        public ObligatoriedadPlantilla ObligatoriedadPara(Canal canal)
        {
            if (canal == Canal.Conectividad)
            {
                return ObligatoriedadConectividad ?? throw new InvalidOperationException(
                    $"Falta el parámetro '{ObligatoriedadPlantilla.NombreParametroConectividad}' en el catálogo de validación: el plan es del canal Conectividad y no se puede comprobar la obligatoriedad.");
            }

            return Obligatoriedad;
        }

        /// <summary>DN-05: `plan.formatos`. Puede ser nulo (ver el constructor). FIRMA DEL ARQUITECTO.</summary>
        public FormatosDelPlan Formatos { get; }

        /// <summary>DN-05: `cuenta.largos`. Puede ser nulo (ver el constructor). FIRMA DEL ARQUITECTO.</summary>
        public LargosDeCuenta LargosDeCuenta { get; }

        /// <summary>El campo de la estructura con ese nombre de <see cref="CamposDeFila"/>: una COPIA NUEVA en cada llamada,
        /// así quien la reciba y la mute no afecta al catálogo ni a otro llamador (revisión de código, 2026-09-21). Otro
        /// nombre: <see cref="ArgumentException"/>.</summary>
        public CampoPlantilla Campo(string campo)
        {
            if (campo == null || !_camposPorNombre.TryGetValue(campo, out var campoPlantilla))
            {
                throw new ArgumentException($"'{campo}' no es un campo de la fila ({string.Join(", ", CamposDeFila.Todos())}).", nameof(campo));
            }

            return Copiar(campoPlantilla);
        }

        /// <summary>Copia todas las propiedades de un <see cref="CampoPlantilla"/> a una instancia nueva (tiene setters
        /// públicos: la única forma de que "inmutable" sea de verdad, revisión de código, 2026-09-21).</summary>
        private static CampoPlantilla Copiar(CampoPlantilla campo)
        {
            return new CampoPlantilla
            {
                Nombre = campo.Nombre,
                Columna = campo.Columna,
                EncabezadoEsperado = campo.EncabezadoEsperado,
                LargoMinimo = campo.LargoMinimo,
                LargoMaximo = campo.LargoMaximo,
                Formato = campo.Formato,
                // Inmutable (CaracteresPermitidos no tiene setters ni listas mutables): se comparte la instancia, no hace falta copiarla.
                Caracteres = campo.Caracteres,
            };
        }

        /// <summary>El plan activo con ese código EXACTO (ya normalizado), o nulo. Nunca uno parecido.</summary>
        public PlanDelCatalogo PlanPorCodigo(string codigoNormalizado)
        {
            if (codigoNormalizado == null)
            {
                return null;
            }

            return _planesPorCodigo.TryGetValue(codigoNormalizado, out var plan) ? plan : null;
        }

        /// <summary>
        /// True si el remitente tiene una autorización ACTIVA sobre ese plan (D-42: la evidencia no cuenta). DN-01: en modo
        /// <see cref="ModoDeAutorizacion.Plan"/> (el fallback quemado) es un simple "está o no está" en el set que se copió
        /// en el constructor; en modo <see cref="ModoDeAutorizacion.Cliente"/> mira el cliente dueño del plan.
        /// </summary>
        public bool EstaAutorizado(Guid planId)
        {
            if (_modo == ModoDeAutorizacion.Cliente)
            {
                // DN-01: en modo Cliente la vía AutorizacionPlan (_planesAutorizados) NO SE MIRA — se conserva, pero
                // queda muda. Un plan que no está entre los activos, o que no trae ClienteId (dueño desconocido),
                // nunca se da por autorizado: no se inventa un cliente (mismo criterio de "nunca se adivina" que el
                // resto del catálogo).
                return _clienteDelPlanPorId.TryGetValue(planId, out var clienteId)
                    && clienteId.HasValue
                    && _clientesAutorizados.Contains(clienteId.Value);
            }

            return _planesAutorizados.Contains(planId);
        }

        /// <summary>
        /// `^[A-Z0-9]{4}$`, sin expresiones regulares (dígito y letra son SIEMPRE ASCII, comparados por rango de <see cref="char"/>):
        /// lo usan tanto el catálogo (código de un <see cref="PlanDelCatalogo"/>) como <see cref="FilaEnValidacion.NumeroPlanNormalizado"/>.
        /// </summary>
        internal static bool EsCodigoDePlanValido(string codigo)
        {
            if (codigo == null || codigo.Length != 4)
            {
                return false;
            }

            foreach (char c in codigo)
            {
                bool esDigitoAscii = c >= '0' && c <= '9';
                bool esLetraMayusculaAscii = c >= 'A' && c <= 'Z';
                if (!esDigitoAscii && !esLetraMayusculaAscii)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Una fila de la plantilla, vista por las reglas de registro (diseno/03 §1 paso 5). Es una VISTA DE SOLO LECTURA: todo lo
    /// derivado (el valor de cada lista, el plan, la referencia que rige) se calcula de lo recibido y de los catálogos, siempre
    /// igual, lo pida quien lo pida y en el orden que sea. Ningún evaluador la modifica: así una regla no depende de que otra
    /// haya corrido antes, y el plugin lee de acá mismo lo que guarda en `sanic_mppp_tbl_fila`.
    /// </summary>
    public sealed class FilaEnValidacion
    {
        /// <summary>Del `FilaPlantilla` recibido: por LETRA DE COLUMNA. Nulo = todo vacío (`Valores` nulo, diseno/03 §1 paso 5).</summary>
        private readonly IDictionary<string, string> _valores;

        public FilaEnValidacion(FilaPlantilla fila, CatalogosDeValidacion catalogos)
        {
            if (fila == null)
            {
                throw new ArgumentNullException(nameof(fila));
            }

            Catalogos = catalogos ?? throw new ArgumentNullException(nameof(catalogos));
            _valores = fila.Valores;
            NumeroFila = fila.NumeroOrden;
        }

        public CatalogosDeValidacion Catalogos { get; }

        /// <summary>`sanic_numerofila`: la posición de la fila en la ventana (<see cref="FilaPlantilla.NumeroOrden"/>).</summary>
        public int NumeroFila { get; }

        /// <summary>Lo que escribió el cliente en ese campo, sin espacios a los lados; vacío si no escribió nada. Campo desconocido: <see cref="ArgumentException"/>.</summary>
        public string Recibido(string campo)
        {
            var columna = Catalogos.Campo(campo).Columna;
            if (_valores == null || !_valores.TryGetValue(columna, out var valor) || valor == null)
            {
                return string.Empty;
            }

            return valor.Trim();
        }

        /// <summary>Cómo se llama ese campo para el cliente: el encabezado de su columna en la plantilla.</summary>
        public string Encabezado(string campo)
        {
            return Catalogos.Campo(campo).EncabezadoEsperado;
        }

        /// <summary>El valor de la lista, o nulo si vino vacío o no es válido (DD-01: la columna queda vacía).</summary>
        public Gestion? Gestion => ResolverEnumDeLista<Gestion>(CamposDeFila.Gestion);

        public Clasificacion? Clasificacion => ResolverEnumDeLista<Clasificacion>(CamposDeFila.Clasificacion);

        public TipoDeIdentificacion? TipoIdentificacion => ResolverEnumDeLista<TipoDeIdentificacion>(CamposDeFila.TipoIdentificacion);

        public Moneda? Moneda => ResolverEnumDeLista<Moneda>(CamposDeFila.Moneda);

        public Banco? Banco => ResolverEnumDeLista<Banco>(CamposDeFila.Banco);

        /// <summary>
        /// DN-05 (decisión 5): la clasificación que se GUARDA y la que mira OBLIGATORIEDAD. Si el cliente escribió algo, es
        /// <see cref="Clasificacion"/> (nulo si no es válida: nunca se reemplaza un valor inválido). Si la celda vino vacía,
        /// el plan existe, el catálogo trae <see cref="CatalogosDeValidacion.Formatos"/> y su formato admite UNA sola
        /// clasificación, es esa. En cualquier otro caso, nulo. Nunca lanza. FIRMA DEL ARQUITECTO.
        /// </summary>
        public Clasificacion? ClasificacionEfectiva
        {
            get
            {
                if (Recibido(CamposDeFila.Clasificacion).Length > 0)
                {
                    return Clasificacion;
                }

                var admitidas = ReglasDeSuFormato()?.Clasificaciones;
                return admitidas != null && admitidas.Count == 1 ? admitidas[0] : (Clasificacion?)null;
            }
        }

        /// <summary>
        /// DN-05 (decisión 10): el banco que se GUARDA, el que mira OBLIGATORIEDAD y contra el que mide CUENTA_DEL_BANCO.
        /// Misma lógica que <see cref="ClasificacionEfectiva"/> con <see cref="ReglasDelFormato.Bancos"/> (06 y 10: Banco
        /// vacío = BAC). Si el formato no restringe bancos (lista nula), un Banco vacío queda nulo. Nunca lanza.
        /// FIRMA DEL ARQUITECTO.
        /// </summary>
        public Banco? BancoEfectivo
        {
            get
            {
                if (Recibido(CamposDeFila.Banco).Length > 0)
                {
                    return Banco;
                }

                var admitidos = ReglasDeSuFormato()?.Bancos;
                return admitidos != null && admitidos.Count == 1 ? admitidos[0] : (Banco?)null;
            }
        }

        /// <summary>DN-05: lo que admite el formato del plan de la fila, o nulo si no hay plan o el catálogo no trae `plan.formatos`.
        /// Nunca lanza (el plan del catálogo siempre trae uno de los tres formatos que el parámetro exige completos).</summary>
        private ReglasDelFormato ReglasDeSuFormato()
        {
            var plan = Plan;
            return plan == null ? null : Catalogos.Formatos?.Para(plan.TipoFormato);
        }

        /// <summary>DN-05 (decisión 3): el plan es formato 11 Y la gestión es Inclusión, el único caso en que la referencia se
        /// ARMA. Con la gestión vacía o inválida (nulo) no se arma nada: esa fila ya la rechazan LISTAS_VALIDAS u OBLIGATORIEDAD.</summary>
        internal bool ArmaLaReferencia => Plan?.TipoFormato == TipoDeFormatoDelPlan._11 && Gestion == Dominio.Gestion.Inclusion;

        /// <summary>El código de 3 dígitos del banco de la fila (de `plantilla.listas`), o nulo si el banco vino vacío o no es válido.</summary>
        public string CodigoDeBanco => Catalogos.Listas.Resolver(CamposDeFila.Banco, Recibido(CamposDeFila.Banco))?.CodigoDeBanco;

        /// <summary>
        /// `sanic_numeroplan`: lo recibido en mayúsculas invariantes y relleno con ceros a la izquierda hasta 4 (`12` → `0012`,
        /// `a1` → `00A1`). Nulo si vino vacío o si el resultado no cumple `^[A-Z0-9]{4}$` (ASCII): nunca se recorta ni se limpia.
        /// </summary>
        public string NumeroPlanNormalizado
        {
            get
            {
                var recibido = Recibido(CamposDeFila.NumeroPlan);
                if (recibido.Length == 0)
                {
                    return null;
                }

                // Mayúscula SOLO ascii (a-z → A-Z): un carácter fuera de ese rango (letra con tilde, dígito de otro alfabeto,
                // símbolo) queda igual, y por eso el patrón de abajo lo rechaza en vez de "arreglarlo" en silencio.
                var mayusculas = new StringBuilder(recibido.Length);
                foreach (char c in recibido)
                {
                    mayusculas.Append(c >= 'a' && c <= 'z' ? (char)(c - 32) : c);
                }

                // Relleno con ceros A LA IZQUIERDA hasta 4; si ya tiene 4 o más, nunca se recorta.
                string relleno = mayusculas.Length >= 4
                    ? mayusculas.ToString()
                    : new string('0', 4 - mayusculas.Length) + mayusculas;

                return CatalogosDeValidacion.EsCodigoDePlanValido(relleno) ? relleno : null;
            }
        }

        /// <summary>`sanic_planid`: el plan activo con ese código exacto, o nulo. NUNCA se adivina un plan parecido.</summary>
        public PlanDelCatalogo Plan => Catalogos.PlanPorCodigo(NumeroPlanNormalizado);

        /// <summary>`sanic_referenciarecibida`: tal cual vino en el Excel (sin recortar); si no vino, nulo. Se guarda siempre.</summary>
        public string ReferenciaRecibida
        {
            get
            {
                var columna = Catalogos.Campo(CamposDeFila.Referencia).Columna;
                return _valores != null && _valores.TryGetValue(columna, out var valor) ? valor : null;
            }
        }

        /// <summary>
        /// La referencia de formato 11 (D-17): código de banco (3) + cuenta rellena con ceros A LA IZQUIERDA hasta 17 = 20
        /// exactos. DN-05 (decisión 3): SOLO en una Inclusión. Nulo si el plan no es formato 11 (o no hay plan), si la gestión no es
        /// Inclusión (Exclusión, Modificación, vacía o inválida), si no hay código de banco, o si la cuenta viene vacía, tiene más
        /// de 17 caracteres o trae algo que no sea 0-9 ASCII.
        /// </summary>
        public string ReferenciaConstruida
        {
            get
            {
                if (!ArmaLaReferencia)
                {
                    return null;
                }

                var codigoDeBanco = CodigoDeBanco;
                if (codigoDeBanco == null)
                {
                    return null;
                }

                var cuenta = Recibido(CamposDeFila.NumeroCuenta);
                if (cuenta.Length == 0 || cuenta.Length > 17)
                {
                    return null;
                }

                foreach (char c in cuenta)
                {
                    if (c < '0' || c > '9')
                    {
                        return null;
                    }
                }

                return codigoDeBanco + cuenta.PadLeft(17, '0');
            }
        }

        /// <summary>
        /// `sanic_referencia`, la que rige. DN-05: formato 11 en Inclusión, la construida, sin mirar la recibida (DD-10); en
        /// cualquier otro caso (Exclusión, Modificación, formato 10), la recibida sin espacios a los lados, aunque quede
        /// vacía (DD-15). DN-10 (corregida): en Inclusión con la referencia vacía (o solo espacios), formato 06 → rige el número
        /// de identificación recibido; formato 10 con clasificación efectiva BAC → rige el número de cuenta recibido; en los dos
        /// casos sin espacios a los lados, sin rellenar y sin pasar a mayúsculas. Si el cliente escribió una referencia, se
        /// respeta. Formato 10 con CK: la recibida (vacía). Sin plan: nulo.
        /// </summary>
        public string ReferenciaQueRige
        {
            get
            {
                var plan = Plan;
                if (plan == null)
                {
                    return null;
                }

                if (ArmaLaReferencia)
                {
                    return ReferenciaConstruida;
                }

                var recibida = Recibido(CamposDeFila.Referencia);
                if (Gestion == Dominio.Gestion.Inclusion && recibida.Length == 0)
                {
                    if (plan.TipoFormato == TipoDeFormatoDelPlan._06)
                    {
                        return Recibido(CamposDeFila.NumeroIdentificacion);
                    }

                    if (plan.TipoFormato == TipoDeFormatoDelPlan._10 && ClasificacionEfectiva == Dominio.Clasificacion.BAC)
                    {
                        return Recibido(CamposDeFila.NumeroCuenta);
                    }
                }

                return recibida;
            }
        }

        /// <summary>Resuelve un campo de lista contra `plantilla.listas` y lo convierte al enum del choice (el valor
        /// resuelto por <see cref="ListasPlantilla"/> es EXACTAMENTE el nombre de un miembro de <typeparamref name="T"/>).</summary>
        private T? ResolverEnumDeLista<T>(string campo) where T : struct
        {
            var valorDeLista = Catalogos.Listas.Resolver(campo, Recibido(campo));
            return valorDeLista == null ? (T?)null : (T)Enum.Parse(typeof(T), valorDeLista.Valor, false);
        }
    }

    /// <summary>
    /// Los evaluadores de las ocho reglas semilla de nivel Registro (diseno/03 §1 paso 5; D-17, D-18 y D-19) y de las cinco de DN-05. Son lectores de
    /// <see cref="FilaEnValidacion"/>. El catálogo es editable: un evaluador al que le sacaron una dependencia no da por bueno
    /// lo que no puede comprobar. Ningún mensaje cita la cuenta ni la identificación (datos sensibles, DD-08); lo que se cita
    /// del cliente pasa por <see cref="MensajeAlCliente.Citar"/>; los campos se nombran por su encabezado en la plantilla.
    /// </summary>
    public static class ReglasDeRegistro
    {
        public const string ListasValidas = "LISTAS_VALIDAS";
        public const string LargosYFormato = "LARGOS_Y_FORMATO";
        public const string PlanExiste = "PLAN_EXISTE";
        public const string Formato11SoloAch = "FORMATO_11_SOLO_ACH";
        public const string Obligatoriedad = "OBLIGATORIEDAD";
        public const string ReferenciaFormato11 = "REFERENCIA_FORMATO_11";
        public const string MonedaDelPlan = "MONEDA_DEL_PLAN";
        public const string AutorizacionCorreoPlan = "AUTORIZACION_CORREO_PLAN";

        /// <summary>DN-06 (aprobador, 2026-11-24): rechaza las filas de un plan que no requiere pre-registro.</summary>
        public const string PlanRequierePreregistro = "PLAN_REQUIERE_PREREGISTRO";

        // DN-05 (aprobador, 2026-10-06). FIRMA DEL ARQUITECTO. FORMATO_11_SOLO_ACH queda en el código mientras el catálogo
        // de Dev la tenga activa (orden de despliegue de DN-05): CLASIFICACION_DEL_FORMATO la reemplaza.
        public const string ClasificacionDelFormato = "CLASIFICACION_DEL_FORMATO";
        public const string GestionDelFormato = "GESTION_DEL_FORMATO";
        public const string BancoDelFormato = "BANCO_DEL_FORMATO";
        public const string CaracteresPermitidos = "CARACTERES_PERMITIDOS";
        public const string CuentaDelBanco = "CUENTA_DEL_BANCO";

        /// <summary>DN-07 (aprobador, 2026-11-24): el número de identificación se valida según su tipo.</summary>
        public const string IdentificacionDelTipo = "IDENTIFICACION_DEL_TIPO";

        /// <summary>Un evaluador por cada código de arriba, ni uno más. Instancias nuevas en cada llamada: no guardan estado.</summary>
        public static IList<IEvaluador<FilaEnValidacion>> Evaluadores()
        {
            return new List<IEvaluador<FilaEnValidacion>>
            {
                new EvaluadorListasValidas(),
                new EvaluadorLargosYFormato(),
                new EvaluadorPlanExiste(),
                new EvaluadorFormato11SoloAch(),
                new EvaluadorObligatoriedad(),
                new EvaluadorReferenciaFormato11(),
                new EvaluadorMonedaDelPlan(),
                new EvaluadorAutorizacionCorreoPlan(),
                new EvaluadorClasificacionDelFormato(),
                new EvaluadorGestionDelFormato(),
                new EvaluadorBancoDelFormato(),
                new EvaluadorCaracteresPermitidos(),
                new EvaluadorCuentaDelBanco(),
                new EvaluadorPlanRequierePreregistro(),
                new EvaluadorIdentificacionDelTipo(),
            };
        }

        /// <summary>
        /// Gestión, Clasificación, Tipo de identificación, Moneda y Banco contra `plantilla.listas` (diseno/03 §1 paso 5). Un
        /// campo vacío no falla acá (eso es de OBLIGATORIEDAD); uno con un valor que no está en su lista deja la columna vacía
        /// y su valor recibido se cita SIEMPRE en la precisión (DD-01), la redacte como la redacte el negocio.
        /// </summary>
        private sealed class EvaluadorListasValidas : IEvaluador<FilaEnValidacion>
        {
            public string Codigo => ListasValidas;

            public Veredicto Evaluar(FilaEnValidacion fila)
            {
                if (fila == null)
                {
                    throw new ArgumentNullException(nameof(fila));
                }

                var camposInvalidos = new List<string>();
                foreach (var campo in CamposDeFila.DeLista())
                {
                    var recibido = fila.Recibido(campo);
                    if (recibido.Length == 0)
                    {
                        continue;
                    }

                    if (fila.Catalogos.Listas.Resolver(campo, recibido) == null)
                    {
                        camposInvalidos.Add(campo);
                    }
                }

                if (camposInvalidos.Count == 0)
                {
                    return Veredicto.Cumplida();
                }

                var encabezados = string.Join(", ", camposInvalidos.Select(fila.Encabezado));
                var valoresCitados = string.Join(", ", camposInvalidos.Select(c => MensajeAlCliente.Citar(fila.Recibido(c))));
                var marcadores = new Dictionary<string, string> { ["campo"] = encabezados, ["valor"] = valoresCitados };

                // D-41 (aprobador, 2026-09-21): menos detalle. El texto por defecto nombra el campo y NO cita lo que
                // escribió el cliente (sin precisión); el marcador {valor} sigue disponible para quien redacte el catálogo.
                return Veredicto.NoCumplida($"Uno o más valores no son válidos: {encabezados}.", marcadores, null);
            }
        }

        /// <summary>
        /// Largo mínimo, máximo y formato de cada campo de la estructura (diseno/03 §1 paso 5). El largo se mide sobre lo
        /// recibido sin espacios a los lados; un campo vacío no falla por mínimo (eso es de OBLIGATORIEDAD), y uno sin ningún
        /// límite en la estructura no se mira. D-41 (aprobador, 2026-09-21): menos detalle, el texto por defecto solo nombra
        /// los campos, SIN precisión (ni el límite ni el valor): el de cuenta y el de identificación son datos sensibles
        /// (DD-08), y para los demás tampoco hace falta el número para que el cliente revise la celda.
        /// </summary>
        private sealed class EvaluadorLargosYFormato : IEvaluador<FilaEnValidacion>
        {
            public string Codigo => LargosYFormato;

            public Veredicto Evaluar(FilaEnValidacion fila)
            {
                if (fila == null)
                {
                    throw new ArgumentNullException(nameof(fila));
                }

                var encabezadosQueFallan = new List<string>();

                foreach (var campo in CamposDeFila.Todos())
                {
                    var campoPlantilla = fila.Catalogos.Campo(campo);
                    if (campoPlantilla.LargoMinimo == null && campoPlantilla.LargoMaximo == null && campoPlantilla.Formato == null)
                    {
                        continue;
                    }

                    var recibido = fila.Recibido(campo);
                    if (recibido.Length == 0)
                    {
                        continue;
                    }

                    bool falla = (campoPlantilla.LargoMinimo.HasValue && recibido.Length < campoPlantilla.LargoMinimo.Value)
                        || (campoPlantilla.LargoMaximo.HasValue && recibido.Length > campoPlantilla.LargoMaximo.Value)
                        || (campoPlantilla.Formato.HasValue && !CumpleFormato(recibido, campoPlantilla.Formato.Value));

                    if (falla)
                    {
                        encabezadosQueFallan.Add(fila.Encabezado(campo));
                    }
                }

                if (encabezadosQueFallan.Count == 0)
                {
                    return Veredicto.Cumplida();
                }

                var encabezados = string.Join(", ", encabezadosQueFallan);
                var marcadores = new Dictionary<string, string> { ["campo"] = encabezados };
                return Veredicto.NoCumplida($"Revise el largo o el formato de: {encabezados}.", marcadores, null);
            }

            /// <summary>`FormatoDeCampo` es una lista cerrada y ASCII (D-18b): dígito y letra se comparan por rango de <see cref="char"/>,
            /// nunca con `char.IsDigit`/`char.IsLetter` (que aceptan dígitos y letras de otros alfabetos).</summary>
            private static bool CumpleFormato(string valor, FormatoDeCampo formato)
            {
                switch (formato)
                {
                    case FormatoDeCampo.Digitos:
                        foreach (char c in valor)
                        {
                            if (c < '0' || c > '9')
                            {
                                return false;
                            }
                        }

                        return true;
                    case FormatoDeCampo.Alfanumerico:
                        foreach (char c in valor)
                        {
                            bool esDigito = c >= '0' && c <= '9';
                            bool esLetra = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
                            if (!esDigito && !esLetra)
                            {
                                return false;
                            }
                        }

                        return true;
                    default:
                        // Texto: cualquier texto vale (LARGOS_Y_FORMATO ya midió el largo arriba).
                        return true;
                }
            }
        }

        /// <summary>
        /// `PLAN_EXISTE` (diseno/03 §1 paso 5): el plan normalizado tiene que existir activo. Nunca se adivina un plan parecido.
        /// D-81 (aprobador, 2026-11-24): con el número de plan vacío o en blanco el cliente recibe SOLO "Falta el número de plan.".
        /// Para eso el veredicto sale SIN marcadores: si la plantilla del catálogo cita {plan} o {valor}, <see cref="MensajeAlCliente"/>
        /// no sabe completarla (marcador desconocido) y rige el texto por defecto (D-19). El arreglo depende de que esa plantilla use
        /// {plan} o {valor}; una plantilla sin marcadores se mostraría tal cual. Con un plan escrito (exista o no) todo sigue igual.
        /// </summary>
        private sealed class EvaluadorPlanExiste : IEvaluador<FilaEnValidacion>
        {
            public string Codigo => PlanExiste;

            public Veredicto Evaluar(FilaEnValidacion fila)
            {
                if (fila == null)
                {
                    throw new ArgumentNullException(nameof(fila));
                }

                if (fila.Plan != null)
                {
                    return Veredicto.Cumplida();
                }

                // D-81: plan vacío -> "Falta el número de plan." sin marcadores (ver el resumen de la clase).
                if (string.IsNullOrWhiteSpace(fila.Recibido(CamposDeFila.NumeroPlan)))
                {
                    return Veredicto.NoCumplida("Falta el número de plan.");
                }

                // D-41 (aprobador, 2026-09-21): menos detalle. El texto por defecto ya NO cita lo que escribió el cliente
                // (nunca se adivina un plan parecido, así que citarlo no ayuda); el marcador queda para quien redacte el catálogo.
                var citado = MensajeAlCliente.Citar(fila.Recibido(CamposDeFila.NumeroPlan));
                var marcadores = new Dictionary<string, string> { ["plan"] = citado, ["valor"] = citado };
                return Veredicto.NoCumplida("El plan no existe o no está activo.", marcadores, null);
            }
        }

        /// <summary>`FORMATO_11_SOLO_ACH` (DD-17): un plan formato 11 solo admite clasificación ACH.</summary>
        private sealed class EvaluadorFormato11SoloAch : IEvaluador<FilaEnValidacion>
        {
            public string Codigo => Formato11SoloAch;

            public Veredicto Evaluar(FilaEnValidacion fila)
            {
                if (fila == null)
                {
                    throw new ArgumentNullException(nameof(fila));
                }

                var plan = fila.Plan;
                if (plan == null)
                {
                    // Suelta y sin plan: no se puede comprobar (depende de PLAN_EXISTE, que ya lo dice).
                    return Veredicto.NoCumplida("No se puede comprobar el formato del plan porque el plan no existe.");
                }

                if (plan.TipoFormato != TipoDeFormatoDelPlan._11 || fila.Clasificacion == Clasificacion.ACH)
                {
                    return Veredicto.Cumplida();
                }

                var marcadores = new Dictionary<string, string> { ["plan"] = plan.Codigo };
                return Veredicto.NoCumplida($"El plan {plan.Codigo} solo admite la clasificación ACH.", marcadores, null);
            }
        }

        /// <summary>
        /// `OBLIGATORIEDAD` (diseno/03 §1 paso 5): según `plantilla.obligatoriedad` para la Gestión, Clasificación y tipo de
        /// formato del plan de la fila. Un campo de lista con un valor INVÁLIDO cuenta como vacío (DD-01: la columna queda
        /// vacía), igual que uno que nunca se escribió.
        /// </summary>
        private sealed class EvaluadorObligatoriedad : IEvaluador<FilaEnValidacion>
        {
            private static readonly HashSet<string> CamposDeLista = new HashSet<string>(CamposDeFila.DeLista(), StringComparer.Ordinal);

            public string Codigo => Obligatoriedad;

            public Veredicto Evaluar(FilaEnValidacion fila)
            {
                if (fila == null)
                {
                    throw new ArgumentNullException(nameof(fila));
                }

                // DN-05: la clasificación EFECTIVA (la que se completó sola, si el formato admite una) y el banco efectivo.
                var opcionales = fila.Catalogos.ObligatoriedadPara(fila.Plan?.Canal ?? Canal.BancaEnLinea).CamposOpcionales(fila.Gestion, fila.ClasificacionEfectiva, fila.Plan?.TipoFormato);
                var faltantes = new List<string>();

                foreach (var campo in CamposDeFila.Todos())
                {
                    if (opcionales.Contains(campo))
                    {
                        continue;
                    }

                    bool vacio = CamposDeLista.Contains(campo) ? EsCampoDeListaVacio(fila, campo) : fila.Recibido(campo).Length == 0;
                    if (vacio)
                    {
                        faltantes.Add(fila.Encabezado(campo));
                    }
                }

                if (faltantes.Count == 0)
                {
                    return Veredicto.Cumplida();
                }

                var encabezados = string.Join(", ", faltantes);
                var marcadores = new Dictionary<string, string> { ["campo"] = encabezados };
                return Veredicto.NoCumplida($"Faltan datos obligatorios: {encabezados}.", marcadores, null);
            }

            private static bool EsCampoDeListaVacio(FilaEnValidacion fila, string campo)
            {
                switch (campo)
                {
                    case CamposDeFila.Gestion:
                        return fila.Gestion == null;
                    case CamposDeFila.Clasificacion:
                        return fila.ClasificacionEfectiva == null;
                    case CamposDeFila.TipoIdentificacion:
                        return fila.TipoIdentificacion == null;
                    case CamposDeFila.Moneda:
                        return fila.Moneda == null;
                    case CamposDeFila.Banco:
                        return fila.BancoEfectivo == null;
                    default:
                        // CamposDeLista solo trae los cinco de arriba (CamposDeFila.DeLista()): no hay otro caso posible.
                        throw new InvalidOperationException($"'{campo}' no es un campo de lista.");
                }
            }
        }

        /// <summary>`REFERENCIA_FORMATO_11` (D-17): en una INCLUSIÓN de formato 11 (DN-05, decisión 3), se cumple si
        /// <see cref="FilaEnValidacion.ReferenciaConstruida"/> se pudo armar. En cualquier otro caso (Exclusión, Modificación,
        /// gestión vacía o inválida, otros formatos) siempre se cumple: no deriva nada (DD-15), rige la referencia recibida. El
        /// mensaje nunca cita la cuenta.</summary>
        private sealed class EvaluadorReferenciaFormato11 : IEvaluador<FilaEnValidacion>
        {
            public string Codigo => ReferenciaFormato11;

            public Veredicto Evaluar(FilaEnValidacion fila)
            {
                if (fila == null)
                {
                    throw new ArgumentNullException(nameof(fila));
                }

                var plan = fila.Plan;
                if (plan == null)
                {
                    return Veredicto.NoCumplida("No se puede armar la referencia porque el plan no existe.");
                }

                if (!fila.ArmaLaReferencia || fila.ReferenciaConstruida != null)
                {
                    return Veredicto.Cumplida();
                }

                // D-42 (aprobador, 2026-09-21): el texto por defecto ya no menciona el largo ("hasta 17").
                var marcadores = new Dictionary<string, string> { ["plan"] = plan.Codigo };
                return Veredicto.NoCumplida($"Para el plan {plan.Codigo}, la cuenta debe tener solo dígitos y el banco debe ser válido.", marcadores, null);
            }
        }

        /// <summary>`MONEDA_DEL_PLAN` (RF-07): la moneda de la fila tiene que ser la del plan. Sin moneda recibida no hay nada
        /// que contradiga al plan (si era obligatoria, lo dice OBLIGATORIEDAD).</summary>
        private sealed class EvaluadorMonedaDelPlan : IEvaluador<FilaEnValidacion>
        {
            public string Codigo => MonedaDelPlan;

            public Veredicto Evaluar(FilaEnValidacion fila)
            {
                if (fila == null)
                {
                    throw new ArgumentNullException(nameof(fila));
                }

                var plan = fila.Plan;
                if (plan == null)
                {
                    return Veredicto.NoCumplida("No se puede comprobar la moneda porque el plan no existe.");
                }

                if (fila.Recibido(CamposDeFila.Moneda).Length == 0)
                {
                    return Veredicto.Cumplida();
                }

                var moneda = fila.Moneda;
                if (moneda != null && moneda.Value == plan.Moneda)
                {
                    return Veredicto.Cumplida();
                }

                // D-41 (aprobador, 2026-09-21): menos detalle, un solo texto tanto si la moneda no es válida como si no
                // coincide con la del plan; el marcador {valor} cita lo recibido para quien redacte el catálogo.
                var valorCitado = moneda != null ? moneda.Value.ToString() : MensajeAlCliente.Citar(fila.Recibido(CamposDeFila.Moneda));
                var marcadores = new Dictionary<string, string> { ["plan"] = plan.Codigo, ["valor"] = valorCitado };
                return Veredicto.NoCumplida($"La moneda no es la del plan {plan.Codigo}.", marcadores, null);
            }
        }

        /// <summary>
        /// `AUTORIZACION_CORREO_PLAN` (RF-02, D-14, D-42): el remitente tiene que tener una autorización ACTIVA sobre el
        /// plan. D-42 (aprobador, 2026-09-21): la evidencia no cuenta para la vigencia, así que hay un SOLO caso de falla,
        /// sin precisión ni mención de "evidencia" en el texto al cliente (eso queda para la vista del administrador).
        /// </summary>
        private sealed class EvaluadorAutorizacionCorreoPlan : IEvaluador<FilaEnValidacion>
        {
            public string Codigo => AutorizacionCorreoPlan;

            public Veredicto Evaluar(FilaEnValidacion fila)
            {
                if (fila == null)
                {
                    throw new ArgumentNullException(nameof(fila));
                }

                var plan = fila.Plan;
                if (plan == null)
                {
                    return Veredicto.NoCumplida("No se puede comprobar la autorización porque el plan no existe.");
                }

                if (fila.Catalogos.EstaAutorizado(plan.Id))
                {
                    return Veredicto.Cumplida();
                }

                var marcadores = new Dictionary<string, string> { ["plan"] = plan.Codigo };
                return Veredicto.NoCumplida($"Su correo no está autorizado sobre el plan {plan.Codigo}.", marcadores, null);
            }
        }

        /// <summary>
        /// `PLAN_REQUIERE_PREREGISTRO` (DN-06, aprobador 2026-11-24): un plan con Requiere pre-registro = No rechaza todas sus
        /// filas. El texto no cita el plan ni lleva marcadores: es el del aprobador, tal cual. Depende de la autorización (solo
        /// se le dice a un remitente autorizado sobre el plan, criterio D-41).
        /// </summary>
        private sealed class EvaluadorPlanRequierePreregistro : IEvaluador<FilaEnValidacion>
        {
            public string Codigo => PlanRequierePreregistro;

            public Veredicto Evaluar(FilaEnValidacion fila)
            {
                if (fila == null)
                {
                    throw new ArgumentNullException(nameof(fila));
                }

                var plan = fila.Plan;
                if (plan == null)
                {
                    return Veredicto.NoCumplida("No se puede comprobar el pre-registro porque el plan no existe.");
                }

                return plan.RequierePreregistro ? Veredicto.Cumplida() : Veredicto.NoCumplida("Este plan no requiere pre-registro.");
            }
        }

        /// <summary>
        /// `IDENTIFICACION_DEL_TIPO` (DN-07, aprobador 2026-11-24): el número de identificación tiene que corresponder a su tipo.
        /// Sin Tipo ID válido no hay contra qué validar (eso lo dice LISTAS_VALIDAS). El motivo no lleva marcadores ni cita el número.
        /// </summary>
        private sealed class EvaluadorIdentificacionDelTipo : IEvaluador<FilaEnValidacion>
        {
            public string Codigo => IdentificacionDelTipo;

            public Veredicto Evaluar(FilaEnValidacion fila)
            {
                if (fila == null)
                {
                    throw new ArgumentNullException(nameof(fila));
                }

                var tipo = fila.TipoIdentificacion;
                if (tipo == null)
                {
                    return Veredicto.Cumplida();
                }

                var motivo = IdentificacionPorTipo.Motivo(tipo.Value, fila.Recibido(CamposDeFila.NumeroIdentificacion));
                return motivo == null ? Veredicto.Cumplida() : Veredicto.NoCumplida(motivo);
            }
        }

        // ================================================================== DN-05 (aprobador, 2026-10-06)

        /// <summary>El `plan.formatos` del catálogo, o error de configuración: una regla ACTIVA que lo necesita y no lo
        /// encuentra no da por buena una fila que no pudo comprobar (falla cerrado, DN-05 §B).</summary>
        private static FormatosDelPlan FormatosObligatorios(FilaEnValidacion fila)
        {
            return fila.Catalogos.Formatos ?? throw new InvalidOperationException(
                $"Falta el parámetro '{FormatosDelPlan.NombreParametro}' en el catálogo de validación: no se puede comprobar lo que admite el formato del plan.");
        }

        /// <summary>Los valores de una lista, para nombrarlos al cliente: "BAC", "BAC o CK", "BAC, CK o ACH".</summary>
        private static string Enumerar<T>(IReadOnlyList<T> valores)
        {
            var textos = valores.Select(v => v.ToString()).ToList();
            return textos.Count == 1 ? textos[0] : string.Join(", ", textos.Take(textos.Count - 1)) + " o " + textos[textos.Count - 1];
        }

        /// <summary>
        /// `CLASIFICACION_DEL_FORMATO` (DN-05, decisiones 5 y 10 del aprobador): REEMPLAZA a `FORMATO_11_SOLO_ACH`, que era su
        /// caso particular. La clasificación recibida tiene que estar entre las que `plan.formatos` admite para el formato del
        /// plan. Vacía cumple: que sea obligatoria lo decide OBLIGATORIEDAD, y que se complete sola lo hace
        /// <see cref="FilaEnValidacion.ClasificacionEfectiva"/>. Con un valor que no es de la lista (nulo), no se da por buena.
        /// </summary>
        private sealed class EvaluadorClasificacionDelFormato : IEvaluador<FilaEnValidacion>
        {
            public string Codigo => ClasificacionDelFormato;

            public Veredicto Evaluar(FilaEnValidacion fila)
            {
                if (fila == null)
                {
                    throw new ArgumentNullException(nameof(fila));
                }

                var formatos = FormatosObligatorios(fila);
                var plan = fila.Plan;
                if (plan == null)
                {
                    return Veredicto.NoCumplida("No se puede comprobar el formato del plan porque el plan no existe.");
                }

                if (fila.Recibido(CamposDeFila.Clasificacion).Length == 0)
                {
                    return Veredicto.Cumplida();
                }

                var admitidas = formatos.Para(plan.TipoFormato).Clasificaciones;
                var clasificacion = fila.Clasificacion;
                if (clasificacion.HasValue && admitidas.Contains(clasificacion.Value))
                {
                    return Veredicto.Cumplida();
                }

                var marcadores = new Dictionary<string, string> { ["plan"] = plan.Codigo };
                return Veredicto.NoCumplida($"El plan {plan.Codigo} solo admite la clasificación {Enumerar(admitidas)}.", marcadores, null);
            }
        }

        /// <summary>
        /// `GESTION_DEL_FORMATO` (DN-05, decisión 4): la gestión tiene que estar entre las que `plan.formatos` admite para el
        /// formato del plan (el 11 no admite Modificación). Vacía cumple (es de OBLIGATORIEDAD). Una gestión que no es de la
        /// lista de gestiones (nulo) no se da por buena; la cita sale de <see cref="MensajeAlCliente.Citar"/> porque lo
        /// escribió el cliente.
        /// </summary>
        private sealed class EvaluadorGestionDelFormato : IEvaluador<FilaEnValidacion>
        {
            public string Codigo => GestionDelFormato;

            public Veredicto Evaluar(FilaEnValidacion fila)
            {
                if (fila == null)
                {
                    throw new ArgumentNullException(nameof(fila));
                }

                var formatos = FormatosObligatorios(fila);
                var plan = fila.Plan;
                if (plan == null)
                {
                    return Veredicto.NoCumplida("No se puede comprobar el formato del plan porque el plan no existe.");
                }

                if (fila.Recibido(CamposDeFila.Gestion).Length == 0)
                {
                    return Veredicto.Cumplida();
                }

                var gestion = fila.Gestion;
                if (gestion.HasValue && formatos.Para(plan.TipoFormato).Gestiones.Contains(gestion.Value))
                {
                    return Veredicto.Cumplida();
                }

                var nombre = gestion.HasValue ? gestion.Value.ToString() : MensajeAlCliente.Citar(fila.Recibido(CamposDeFila.Gestion));
                var marcadores = new Dictionary<string, string> { ["plan"] = plan.Codigo, ["gestion"] = nombre };
                return Veredicto.NoCumplida($"El plan {plan.Codigo} no admite la gestión {nombre}.", marcadores, null);
            }
        }

        /// <summary>
        /// `BANCO_DEL_FORMATO` (DN-05, decisión 10): igual que la de clasificación, con los bancos. Vacío cumple (es de
        /// OBLIGATORIEDAD; <see cref="FilaEnValidacion.BancoEfectivo"/> completa el único posible). Sin `bancos` en el formato,
        /// sirve cualquier banco de `plantilla.listas`; uno que no está en esa lista (nulo) no se da por bueno.
        /// </summary>
        private sealed class EvaluadorBancoDelFormato : IEvaluador<FilaEnValidacion>
        {
            public string Codigo => BancoDelFormato;

            public Veredicto Evaluar(FilaEnValidacion fila)
            {
                if (fila == null)
                {
                    throw new ArgumentNullException(nameof(fila));
                }

                var formatos = FormatosObligatorios(fila);
                var plan = fila.Plan;
                if (plan == null)
                {
                    return Veredicto.NoCumplida("No se puede comprobar el formato del plan porque el plan no existe.");
                }

                if (fila.Recibido(CamposDeFila.Banco).Length == 0)
                {
                    return Veredicto.Cumplida();
                }

                var admitidos = formatos.Para(plan.TipoFormato).Bancos;
                var banco = fila.Banco;
                if (banco.HasValue && (admitidos == null || admitidos.Contains(banco.Value)))
                {
                    return Veredicto.Cumplida();
                }

                var marcadores = new Dictionary<string, string> { ["plan"] = plan.Codigo };
                var texto = admitidos == null
                    ? $"El plan {plan.Codigo} no admite el banco indicado."
                    : $"El plan {plan.Codigo} solo admite cuentas de {Enumerar(admitidos)}.";
                return Veredicto.NoCumplida(texto, marcadores, null);
            }
        }

        /// <summary>
        /// `CARACTERES_PERMITIDOS` (DN-05, decisión 7, escenario 21): cada campo cuya estructura trae `caracteres` solo puede
        /// llevar esos caracteres. Se mide sobre lo recibido (recortado); vacío o sin `caracteres` no se revisa. El mensaje nombra
        /// los caracteres prohibidos de cada campo, en orden de primera aparición, sin repetir (se deduplica por el texto que se
        /// MUESTRA), a lo sumo <see cref="MaximoDeCaracteresPorCampo"/> y después "…"; nunca el valor (D-41, DD-08). Un carácter
        /// que no se puede mostrar se nombra, no se muestra. Una sola pasada por cada valor, que se corta al juntar uno más del
        /// tope: el costo queda atado al largo del valor y nunca a lo que traiga.
        /// </summary>
        private sealed class EvaluadorCaracteresPermitidos : IEvaluador<FilaEnValidacion>
        {
            private const int MaximoDeCaracteresPorCampo = 10;
            private const string Espacio = "(espacio)";
            private const string Invisible = "(carácter invisible)";

            public string Codigo => CaracteresPermitidos;

            public Veredicto Evaluar(FilaEnValidacion fila)
            {
                if (fila == null)
                {
                    throw new ArgumentNullException(nameof(fila));
                }

                var encabezados = new List<string>();
                var partes = new List<string>();

                foreach (var campo in CamposDeFila.Todos())
                {
                    var permitidos = fila.Catalogos.Campo(campo).Caracteres;
                    if (permitidos == null)
                    {
                        continue;
                    }

                    var recibido = fila.Recibido(campo);
                    if (recibido.Length == 0)
                    {
                        continue;
                    }

                    var prohibidos = Prohibidos(recibido, permitidos, out var hayMas);
                    if (prohibidos.Count == 0)
                    {
                        continue;
                    }

                    var encabezado = fila.Encabezado(campo);
                    encabezados.Add(encabezado);
                    partes.Add($"{encabezado} ({string.Join(" ", prohibidos)}{(hayMas ? " …" : string.Empty)})");
                }

                if (partes.Count == 0)
                {
                    return Veredicto.Cumplida();
                }

                var marcadores = new Dictionary<string, string> { ["campo"] = string.Join(", ", encabezados) };
                return Veredicto.NoCumplida($"Caracteres no permitidos: {string.Join("; ", partes)}.", marcadores, null);
            }

            /// <summary>Cómo se muestran los caracteres prohibidos de un valor: hasta el tope, sin repetir. <paramref name="hayMas"/>
            /// si había al menos uno más (distinto) que no entró.</summary>
            private static List<string> Prohibidos(string valor, CaracteresPermitidos permitidos, out bool hayMas)
            {
                var mostrados = new List<string>();
                var vistos = new HashSet<string>(StringComparer.Ordinal);
                hayMas = false;

                for (var i = 0; i < valor.Length; i++)
                {
                    var c = valor[i];
                    string texto;
                    if (char.IsHighSurrogate(c) && i + 1 < valor.Length && char.IsLowSurrogate(valor[i + 1]))
                    {
                        // Un par sustituto válido es UN carácter (un emoji): se revisa y se muestra entero, nunca por mitades.
                        var baja = valor[i + 1];
                        i++;
                        if (permitidos.Permite(c) && permitidos.Permite(baja))
                        {
                            continue;
                        }

                        // Revisión DN-05, hallazgo 7: un par que es invisible (selector de variación U+E0100-E01EF, marca combinante o
                        // carácter de formato fuera del plano básico) se nombra como invisible, igual que uno suelto.
                        texto = EsParInvisible(c, baja) ? Invisible : new string(new[] { c, baja });
                    }
                    else
                    {
                        if (permitidos.Permite(c))
                        {
                            continue;
                        }

                        texto = Mostrar(c);
                    }

                    if (!vistos.Add(texto))
                    {
                        continue;
                    }

                    if (mostrados.Count == MaximoDeCaracteresPorCampo)
                    {
                        hayMas = true;
                        break;
                    }

                    mostrados.Add(texto);
                }

                return mostrados;
            }

            private static bool EsParInvisible(char alta, char baja)
            {
                var punto = char.ConvertToUtf32(alta, baja);
                if (punto >= 0xE0100 && punto <= 0xE01EF)
                {
                    return true;
                }

                var categoria = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(new string(new[] { alta, baja }), 0);
                return categoria == System.Globalization.UnicodeCategory.NonSpacingMark
                    || categoria == System.Globalization.UnicodeCategory.EnclosingMark
                    || categoria == System.Globalization.UnicodeCategory.Format;
            }

            /// <summary>El texto con el que se nombra un carácter prohibido (no sustituto válido): el espacio común, "(espacio)";
            /// control, formato (Cf), marca combinante (Mn, Me), selector de variación, relleno en blanco (U+3164, U+115F, U+1160, U+FFA0, U+2800),
            /// cualquier otro espacio en blanco y sustituto suelto, "(carácter invisible)"; el resto, él mismo.</summary>
            private static string Mostrar(char c)
            {
                if (c == ' ')
                {
                    return Espacio;
                }

                var categoria = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (char.IsControl(c) || char.IsWhiteSpace(c) || char.IsSurrogate(c)
                    || categoria == System.Globalization.UnicodeCategory.Format
                    // Revisión DN-05, hallazgo 7: marcas combinantes (Mn, Me), selectores de variación (U+FE00-FE0F; los de
                    // U+E0100-E01EF son pares sustitutos y ya los cubre IsSurrogate) y los rellenos que se dibujan en blanco.
                    || categoria == System.Globalization.UnicodeCategory.NonSpacingMark
                    || categoria == System.Globalization.UnicodeCategory.EnclosingMark
                    || (c >= '\uFE00' && c <= '\uFE0F')
                    || c == '\u3164' || c == '\u115F' || c == '\u1160' || c == '\uFFA0' || c == '\u2800')
                {
                    return Invisible;
                }

                return c.ToString();
            }
        }

        /// <summary>
        /// `CUENTA_DEL_BANCO` (DN-05, decisión 8, escenario 20): el largo de la cuenta (recibida, sin espacios a los lados,
        /// CON los ceros a la izquierda) tiene que estar en el rango de `cuenta.largos` del banco efectivo
        /// (<see cref="FilaEnValidacion.BancoEfectivo"/>); sin banco efectivo, el `porDefecto`. Cuenta vacía cumple (es de
        /// OBLIGATORIEDAD); que sean solo dígitos es de CARACTERES_PERMITIDOS. El mensaje no cita el largo ni la cuenta (D-41).
        /// </summary>
        private sealed class EvaluadorCuentaDelBanco : IEvaluador<FilaEnValidacion>
        {
            public string Codigo => CuentaDelBanco;

            public Veredicto Evaluar(FilaEnValidacion fila)
            {
                if (fila == null)
                {
                    throw new ArgumentNullException(nameof(fila));
                }

                var largos = fila.Catalogos.LargosDeCuenta ?? throw new InvalidOperationException(
                    $"Falta el parámetro '{LargosDeCuenta.NombreParametro}' en el catálogo de validación: no se puede comprobar el largo de la cuenta.");

                var cuenta = fila.Recibido(CamposDeFila.NumeroCuenta);
                if (cuenta.Length == 0)
                {
                    return Veredicto.Cumplida();
                }

                var banco = fila.BancoEfectivo;
                var rango = largos.Para(banco);
                if (cuenta.Length >= rango.Minimo && cuenta.Length <= rango.Maximo)
                {
                    return Veredicto.Cumplida();
                }

                if (banco == null)
                {
                    return Veredicto.NoCumplida("El largo de la cuenta no es válido.");
                }

                var marcadores = new Dictionary<string, string> { ["banco"] = banco.Value.ToString() };
                return Veredicto.NoCumplida($"El largo de la cuenta no corresponde al banco {banco.Value}.", marcadores, null);
            }
        }
    }
}
