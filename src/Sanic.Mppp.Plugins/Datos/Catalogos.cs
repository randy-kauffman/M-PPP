using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Mppp.Plugins.Dominio;
using Sanic.Mppp.Plugins.Validacion;

namespace Sanic.Mppp.Plugins.Datos
{
    /// <summary>Nombres lógicos de las tablas y columnas que toca la capa de datos (diseno/02). ÚNICO lugar donde se escriben.</summary>
    public static class Tablas
    {
        public const string Cliente = "sanic_mppp_tbl_cliente";
        public const string Plan = "sanic_mppp_tbl_plan";
        public const string Autorizado = "sanic_mppp_tbl_autorizado";
        public const string AutorizacionPlan = "sanic_mppp_tbl_autorizacionplan";

        /// <summary>DN-01 (diseno/DIRECTRICES-NUEVAS.md): la vía por CLIENTE, hermana de <see cref="AutorizacionPlan"/>. Ya existe en Dev.</summary>
        public const string AutorizacionCliente = "sanic_mppp_tbl_autorizacioncliente";

        public const string Parametro = "sanic_mppp_tbl_parametro";
        public const string Regla = "sanic_mppp_tbl_regla";

        /// <summary>`statecode` activo, en todas las tablas.</summary>
        public const int Activo = 0;
    }

    /// <summary>Un parámetro leído: la versión ACTIVA más alta de ese nombre (diseno/02 §2.5).</summary>
    public sealed class ParametroLeido
    {
        public ParametroLeido(string nombre, int version, string valor)
        {
            Nombre = nombre;
            Version = version;
            Valor = valor;
        }

        public string Nombre { get; }

        public int Version { get; }

        public string Valor { get; }
    }

    /// <summary>Una regla activa del catálogo con su id, para el lookup `sanic_reglaid` del historial (diseno/02 §3.3). El motor solo usa la definición.</summary>
    public sealed class ReglaDelCatalogo
    {
        public ReglaDelCatalogo(Guid id, DefinicionDeRegla definicion)
        {
            Id = id;
            Definicion = definicion;
        }

        public Guid Id { get; }

        public DefinicionDeRegla Definicion { get; }
    }

    /// <summary>
    /// Lectores de catálogos sobre <see cref="IOrganizationService"/> (pieza 7.6, `Datos/`). Reglas comunes (lo fijan las pruebas
    /// `CatalogosDataverseAceptacion`): cada método hace un número FIJO de consultas, nunca una por elemento (diseno/03 §1 paso 6);
    /// solo lee lo ACTIVO (`statecode` = 0); pide solo las columnas que usa; devuelve tipos del dominio, nunca `Entity`; un valor
    /// que no se puede traducir (un choice fuera del enum, una columna requerida en nulo) es un error real
    /// (<see cref="InvalidOperationException"/> que nombra tabla, columna y registro), no un dato "vacío". Servicio nulo:
    /// <see cref="ArgumentNullException"/>.
    /// </summary>
    public sealed class CatalogosDataverse
    {
        /// <summary>`statecode` está en todas las tablas de diseno/02 §2.</summary>
        private const string ColumnaEstado = "statecode";

        /// <summary>Clave primaria de una tabla custom: `&lt;logicalname&gt;id` (convención de Dataverse).</summary>
        private const string ColumnaIdPlan = Tablas.Plan + "id";

        private const string ColumnaIdCliente = Tablas.Cliente + "id";

        private readonly IOrganizationService _servicio;

        public CatalogosDataverse(IOrganizationService servicio)
        {
            _servicio = servicio ?? throw new ArgumentNullException(nameof(servicio));
        }

        /// <summary>
        /// La versión activa más alta del parámetro con ese nombre (`sanic_nombre`; Dataverse compara texto sin distinguir
        /// mayúsculas, y el nombre va en minúscula por convención), o nulo si no hay ninguna activa.
        /// UNA consulta: filtro por nombre y activo, orden por `sanic_version` descendente, `TopCount` 1.
        /// </summary>
        public ParametroLeido Parametro(string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre))
            {
                throw new ArgumentException("El nombre del parámetro no puede estar vacío.", nameof(nombre));
            }

            var consulta = new QueryExpression(Tablas.Parametro)
            {
                ColumnSet = new ColumnSet("sanic_nombre", "sanic_version", "sanic_valor"),
                TopCount = 1
            };
            consulta.Criteria.AddCondition("sanic_nombre", ConditionOperator.Equal, nombre);
            consulta.Criteria.AddCondition(ColumnaEstado, ConditionOperator.Equal, Tablas.Activo);
            consulta.AddOrder("sanic_version", OrderType.Descending);

            var registro = _servicio.RetrieveMultiple(consulta).Entities.FirstOrDefault();
            if (registro == null)
            {
                return null;
            }

            var version = RequeridoValor<int>(registro, "sanic_version");
            var valor = RequeridoTexto(registro, "sanic_valor");
            return new ParametroLeido(nombre, version, valor);
        }

        /// <summary>
        /// Las reglas ACTIVAS de ese nivel, como las necesita el motor. UNA consulta. `DependeDe` sale de `sanic_dependede`
        /// separado por comas, sin espacios a los lados, sin vacíos; nulo o en blanco → lista vacía. `Efecto` de `sanic_efecto`;
        /// `Orden` de `sanic_orden`; `MensajeCliente` de `sanic_mensajecliente` (puede ser nulo). El orden de la lista no importa:
        /// lo decide el motor.
        /// </summary>
        public IList<DefinicionDeRegla> ReglasActivas(NivelDeLaRegla nivel)
        {
            var conId = ReglasActivasConId(nivel);
            var definiciones = new List<DefinicionDeRegla>(conId.Count);
            foreach (var regla in conId)
            {
                definiciones.Add(regla.Definicion);
            }

            return definiciones;
        }

        /// <summary>
        /// Lo mismo que <see cref="ReglasActivas"/> pero con el id de cada regla: lo necesita quien guarda el historial
        /// (`sanic_reglaid`). UNA consulta, igual que aquella (revisión de 7.6b, 2026-09-21: `DefinicionDeRegla` es del motor y
        /// no conoce Dataverse, así que el id viaja aparte).
        /// </summary>
        public IList<ReglaDelCatalogo> ReglasActivasConId(NivelDeLaRegla nivel)
        {
            var consulta = new QueryExpression(Tablas.Regla)
            {
                ColumnSet = new ColumnSet("sanic_codigo", "sanic_orden", "sanic_dependede", "sanic_efecto", "sanic_mensajecliente")
            };
            consulta.Criteria.AddCondition("sanic_nivel", ConditionOperator.Equal, (int)nivel);
            consulta.Criteria.AddCondition(ColumnaEstado, ConditionOperator.Equal, Tablas.Activo);

            var registros = _servicio.RetrieveMultiple(consulta).Entities;
            var reglas = new List<ReglaDelCatalogo>(registros.Count);
            foreach (var registro in registros)
            {
                var definicion = new DefinicionDeRegla
                {
                    Codigo = RequeridoTexto(registro, "sanic_codigo"),
                    Orden = RequeridoValor<int>(registro, "sanic_orden"),
                    DependeDe = SepararDependeDe(TextoOpcional(registro, "sanic_dependede")),
                    Efecto = RequeridoChoice<EfectoDeLaRegla>(registro, "sanic_efecto"),
                    MensajeCliente = TextoOpcional(registro, "sanic_mensajecliente")
                };

                reglas.Add(new ReglaDelCatalogo(registro.Id, definicion));
            }

            return reglas;
        }

        /// <summary>
        /// Los planes ACTIVOS cuyo `sanic_codigo` está en la lista (ya normalizados). UNA consulta con `In`, sin repetidos y sin
        /// nulos ni blancos en la lista; lista vacía → sin consulta y lista vacía. Trae también el cliente dueño
        /// (`sanic_clienteid`), pero SIN exigirlo (DN-01, corrección del aprobador 2026-09-26): esta función la usan
        /// los DOS modos, y en modo <see cref="ModoDeAutorizacion.Plan"/> el dueño del plan no se mira para nada —
        /// exigirlo acá rompería el modo que está activo en TODOS los entornos hoy, para proteger un modo que
        /// todavía nadie usa (exactamente al revés de "un dato de configuración incompleto degrada el control,
        /// nunca voltea la operación": mismo criterio que `servicio.cuenta`, `rpa.horas.tolerancia`,
        /// `rpa.minutos.claimmaximo` y `autorizacion.modo`). Un plan sin dueño conocido queda con
        /// <see cref="PlanDelCatalogo.ClienteId"/> nulo, y en modo Cliente eso significa fallar cerrado: nunca se da
        /// por autorizado (ya lo prueba <see cref="CatalogosDeValidacion.EstaAutorizado"/>).
        /// </summary>
        public IList<PlanDelCatalogo> PlanesActivosPorCodigo(IEnumerable<string> codigos)
        {
            if (codigos == null)
            {
                throw new ArgumentNullException(nameof(codigos));
            }

            var codigosLimpios = LimpiarLista(codigos);
            if (codigosLimpios.Count == 0)
            {
                return new List<PlanDelCatalogo>();
            }

            var consulta = new QueryExpression(Tablas.Plan)
            {
                ColumnSet = new ColumnSet("sanic_codigo", "sanic_tipoformato", "sanic_moneda", "sanic_clienteid")
            };
            consulta.Criteria.AddCondition("sanic_codigo", ConditionOperator.In, codigosLimpios.Cast<object>().ToArray());
            consulta.Criteria.AddCondition(ColumnaEstado, ConditionOperator.Equal, Tablas.Activo);

            var registros = _servicio.RetrieveMultiple(consulta).Entities;
            var planes = new List<PlanDelCatalogo>(registros.Count);
            foreach (var registro in registros)
            {
                planes.Add(new PlanDelCatalogo(
                    registro.Id,
                    RequeridoTexto(registro, "sanic_codigo"),
                    RequeridoChoice<TipoDeFormatoDelPlan>(registro, "sanic_tipoformato"),
                    RequeridoChoice<Moneda>(registro, "sanic_moneda"),
                    OpcionalReferencia(registro, "sanic_clienteid")?.Id));
            }

            return planes;
        }

        /// <summary>
        /// Los ids de plan sobre los que ese correo tiene autorización vigente (diseno/02 §2.4; DN-01, diseno/DIRECTRICES-NUEVAS.md
        /// "El modo cambia TODO, no una parte"). ÚNICO punto donde se consulta la autorización: acá adentro, y solo acá, se
        /// resuelve `autorizacion.modo` — <see cref="Api.ClasificarCorreo"/> y <see cref="Api.ValidarSolicitud"/> llaman esto
        /// mismo sin saber de modos, así ninguno puede quedarse afuera si el modo cambia.
        ///  - modo <see cref="ModoDeAutorizacion.Plan"/> (fallback quemado, comportamiento de hoy): el Autorizado, la
        ///    AutorizacionPlan, el Plan y el Cliente del plan, todo activo (D-42: la evidencia no cuenta).
        ///  - modo <see cref="ModoDeAutorizacion.Cliente"/>: el Autorizado, la AutorizacionCliente y el Cliente, todo activo;
        ///    devuelve TODOS los planes activos de esos clientes, sin enumerarlos. `AutorizacionPlan` se conserva pero
        ///    queda muda (diseno/DIRECTRICES-NUEVAS.md DN-01 punto 4).
        /// Ninguna si el correo viene nulo o en blanco (conjunto vacío, ni una consulta). Conjunto nuevo en cada llamada.
        /// Corta en el primer paso que da vacío, para no gastar las consultas siguientes.
        /// </summary>
        public ISet<Guid> PlanesAutorizadosDe(string correo)
        {
            if (string.IsNullOrWhiteSpace(correo))
            {
                return new HashSet<Guid>();
            }

            var correoNormalizado = correo.Trim().ToLowerInvariant();
            var modo = ResolverModoDeAutorizacion();

            return modo == ModoDeAutorizacion.Cliente
                ? PlanesAutorizadosPorCliente(correoNormalizado)
                : PlanesAutorizadosPorPlan(correoNormalizado);
        }

        // ------------------------------------------------------------------ helpers de PlanesAutorizadosDe

        /// <summary>`autorizacion.modo` (DN-01): degrada al fallback quemado (<see cref="ModoDeAutorizacion.Plan"/>) si el
        /// parámetro falta o trae basura — nunca lanza, nunca tumba la validación.</summary>
        private ModoDeAutorizacion ResolverModoDeAutorizacion()
        {
            var parametro = Parametro(ModoDeAutorizacionParametro.Nombre);
            return ModoDeAutorizacionParametro.Resolver(parametro?.Valor);
        }

        /// <summary>El comportamiento de hoy, EXACTAMENTE (diseno/DIRECTRICES-NUEVAS.md, "DN-01 · Modelo": "sin cambiar una
        /// línea de comportamiento"): Autorizado -&gt; AutorizacionPlan -&gt; Plan -&gt; Cliente, todo activo.</summary>
        private ISet<Guid> PlanesAutorizadosPorPlan(string correoNormalizado)
        {
            // Paso 1: Autorizados activos con ese correo exacto.
            var autorizadosIds = IdsActivosPorCondicion(Tablas.Autorizado, "sanic_nombre", ConditionOperator.Equal, correoNormalizado);
            if (autorizadosIds.Count == 0)
            {
                return new HashSet<Guid>();
            }

            // Paso 2: AutorizacionPlan activas de esos autorizados -> ids de plan (columna de referencia, no la propia id).
            var planIds = PlanIdsDeAutorizaciones(autorizadosIds);
            if (planIds.Count == 0)
            {
                return new HashSet<Guid>();
            }

            // Paso 3: Planes activos entre esos ids, con el id de su cliente.
            var clientePorPlan = PlanesActivosConCliente(planIds);
            if (clientePorPlan.Count == 0)
            {
                return new HashSet<Guid>();
            }

            // Paso 4: Clientes activos entre los clientes de esos planes.
            var clientesActivos = IdsActivosPorCondicion(Tablas.Cliente, ColumnaIdCliente, ConditionOperator.In, clientePorPlan.Values.Distinct().Cast<object>().ToArray());

            return new HashSet<Guid>(clientePorPlan.Where(p => clientesActivos.Contains(p.Value)).Select(p => p.Key));
        }

        /// <summary>
        /// DN-01: Autorizado -&gt; AutorizacionCliente -&gt; Cliente, todo activo, y TODOS los planes activos de esos
        /// clientes (sin enumerarlos). `AutorizacionPlan` no se mira: esa vía queda muda en este modo.
        /// </summary>
        private ISet<Guid> PlanesAutorizadosPorCliente(string correoNormalizado)
        {
            // Paso 1: Autorizados activos con ese correo exacto (mismo paso 1 que el modo Plan).
            var autorizadosIds = IdsActivosPorCondicion(Tablas.Autorizado, "sanic_nombre", ConditionOperator.Equal, correoNormalizado);
            if (autorizadosIds.Count == 0)
            {
                return new HashSet<Guid>();
            }

            // Paso 2: AutorizacionCliente activas de esos autorizados -> ids de cliente.
            var clienteIds = ClienteIdsDeAutorizaciones(autorizadosIds);
            if (clienteIds.Count == 0)
            {
                return new HashSet<Guid>();
            }

            // Paso 3: Clientes activos entre esos ids.
            var clientesActivos = IdsActivosPorCondicion(Tablas.Cliente, ColumnaIdCliente, ConditionOperator.In, clienteIds.Cast<object>().ToArray());
            if (clientesActivos.Count == 0)
            {
                return new HashSet<Guid>();
            }

            // Paso 4: TODOS los planes activos de esos clientes, sin enumerarlos.
            return PlanesActivosDeClientes(clientesActivos);
        }

        private List<Guid> IdsActivosPorCondicion(string tabla, string columna, ConditionOperator operador, params object[] valores)
        {
            var consulta = new QueryExpression(tabla) { ColumnSet = new ColumnSet() };
            consulta.Criteria.AddCondition(columna, operador, valores);
            consulta.Criteria.AddCondition(ColumnaEstado, ConditionOperator.Equal, Tablas.Activo);
            return _servicio.RetrieveMultiple(consulta).Entities.Select(e => e.Id).ToList();
        }

        private List<Guid> PlanIdsDeAutorizaciones(IReadOnlyCollection<Guid> autorizadosIds)
        {
            var consulta = new QueryExpression(Tablas.AutorizacionPlan) { ColumnSet = new ColumnSet("sanic_planid") };
            consulta.Criteria.AddCondition("sanic_autorizadoid", ConditionOperator.In, autorizadosIds.Cast<object>().ToArray());
            consulta.Criteria.AddCondition(ColumnaEstado, ConditionOperator.Equal, Tablas.Activo);
            return _servicio.RetrieveMultiple(consulta).Entities
                .Select(e => RequeridoReferencia(e, "sanic_planid").Id)
                .Distinct()
                .ToList();
        }

        private Dictionary<Guid, Guid> PlanesActivosConCliente(IReadOnlyCollection<Guid> planIds)
        {
            var consulta = new QueryExpression(Tablas.Plan) { ColumnSet = new ColumnSet("sanic_clienteid") };
            consulta.Criteria.AddCondition(ColumnaIdPlan, ConditionOperator.In, planIds.Cast<object>().ToArray());
            consulta.Criteria.AddCondition(ColumnaEstado, ConditionOperator.Equal, Tablas.Activo);
            return _servicio.RetrieveMultiple(consulta).Entities
                .ToDictionary(e => e.Id, e => RequeridoReferencia(e, "sanic_clienteid").Id);
        }

        /// <summary>DN-01: AutorizacionCliente activas de esos autorizados -&gt; ids de CLIENTE (columna de referencia).</summary>
        private List<Guid> ClienteIdsDeAutorizaciones(IReadOnlyCollection<Guid> autorizadosIds)
        {
            var consulta = new QueryExpression(Tablas.AutorizacionCliente) { ColumnSet = new ColumnSet("sanic_clienteid") };
            consulta.Criteria.AddCondition("sanic_autorizadoid", ConditionOperator.In, autorizadosIds.Cast<object>().ToArray());
            consulta.Criteria.AddCondition(ColumnaEstado, ConditionOperator.Equal, Tablas.Activo);
            return _servicio.RetrieveMultiple(consulta).Entities
                .Select(e => RequeridoReferencia(e, "sanic_clienteid").Id)
                .Distinct()
                .ToList();
        }

        /// <summary>DN-01: todos los ids de Plan ACTIVOS cuyo `sanic_clienteid` está entre esos clientes. Un plan sin
        /// `sanic_clienteid` (columna opcional para este modo, ver <see cref="PlanesActivosPorCodigo"/>) nunca calza
        /// acá: no se inventa un dueño.</summary>
        private HashSet<Guid> PlanesActivosDeClientes(IReadOnlyCollection<Guid> clienteIds)
        {
            var consulta = new QueryExpression(Tablas.Plan) { ColumnSet = new ColumnSet() };
            consulta.Criteria.AddCondition("sanic_clienteid", ConditionOperator.In, clienteIds.Cast<object>().ToArray());
            consulta.Criteria.AddCondition(ColumnaEstado, ConditionOperator.Equal, Tablas.Activo);
            return new HashSet<Guid>(_servicio.RetrieveMultiple(consulta).Entities.Select(e => e.Id));
        }

        // ------------------------------------------------------------------ traducción Entity -> tipos del dominio

        internal static InvalidOperationException ErrorColumna(Entity registro, string columna)
        {
            return new InvalidOperationException(
                $"La tabla '{registro.LogicalName}' tiene el registro {registro.Id} con la columna '{columna}' vacía o con un valor que no se puede traducir.");
        }

        internal static string RequeridoTexto(Entity registro, string columna)
        {
            if (registro.Contains(columna) && registro[columna] is string valor && valor.Length > 0)
            {
                return valor;
            }

            throw ErrorColumna(registro, columna);
        }

        internal static string TextoOpcional(Entity registro, string columna)
        {
            return registro.Contains(columna) ? registro[columna] as string : null;
        }

        private static T RequeridoValor<T>(Entity registro, string columna) where T : struct
        {
            if (registro.Contains(columna) && registro[columna] is T valor)
            {
                return valor;
            }

            throw ErrorColumna(registro, columna);
        }

        internal static TEnum RequeridoChoice<TEnum>(Entity registro, string columna) where TEnum : struct, Enum
        {
            if (registro.Contains(columna) && registro[columna] is OptionSetValue opcion && Enum.IsDefined(typeof(TEnum), opcion.Value))
            {
                return (TEnum)(object)opcion.Value;
            }

            throw ErrorColumna(registro, columna);
        }

        internal static EntityReference RequeridoReferencia(Entity registro, string columna)
        {
            if (registro.Contains(columna) && registro[columna] is EntityReference referencia)
            {
                return referencia;
            }

            throw ErrorColumna(registro, columna);
        }

        /// <summary>Como <see cref="RequeridoReferencia"/> pero SIN exigir: nulo si la columna falta o no es una referencia. Para
        /// columnas que un modo mira y el otro no (DN-01: `sanic_clienteid` del Plan).</summary>
        internal static EntityReference OpcionalReferencia(Entity registro, string columna)
        {
            return registro.Contains(columna) ? registro[columna] as EntityReference : null;
        }

        private static IList<string> SepararDependeDe(string crudo)
        {
            if (string.IsNullOrWhiteSpace(crudo))
            {
                return new List<string>();
            }

            return crudo.Split(',')
                .Select(parte => parte.Trim())
                .Where(parte => parte.Length > 0)
                .ToList();
        }

        private static IList<string> LimpiarLista(IEnumerable<string> valores)
        {
            var vistos = new HashSet<string>(StringComparer.Ordinal);
            var limpios = new List<string>();
            foreach (var valor in valores)
            {
                if (string.IsNullOrWhiteSpace(valor))
                {
                    continue;
                }

                var recortado = valor.Trim();
                if (vistos.Add(recortado))
                {
                    limpios.Add(recortado);
                }
            }

            return limpios;
        }
    }
}
