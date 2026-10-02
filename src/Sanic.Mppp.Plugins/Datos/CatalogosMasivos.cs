using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Mppp.Plugins.Catalogos;
using Sanic.Mppp.Plugins.Dominio;

namespace Sanic.Mppp.Plugins.Datos
{
    // =====================================================================================================================
    // DN-04 (diseno/DIRECTRICES-NUEVAS.md; diseno/03 §1c/§1d): la frontera con Dataverse de la carga y el cambio de estado
    // masivos. Arma la foto (pasada 1) y aplica lo planificado (pasada 2). FIRMAS DEL ARQUITECTO: no se cambian sin
    // consultarlo. Pruebas: tests/.../Aceptacion/CatalogosMasivosAceptacion.cs.
    // =====================================================================================================================

    /// <summary>
    /// La foto de los catálogos para las claves de un archivo. Reglas:
    ///  - Los CATÁLOGOS se leen con <c>catalogos</c> (el servicio del usuario que llama: sus privilegios los aplica la plataforma).
    ///    USUARIOS y ROLES se leen con <c>sistema</c> (SYSTEM): el Administrador de planes no tiene por qué ver la seguridad.
    ///  - Trae activos E inactivos (la foto dice el estado; el planificador decide qué hacer con él).
    ///  - Número FIJO de consultas, que no depende de cuántas claves trae el archivo; ninguna consulta si no hay claves de ese tipo.
    ///    Sin joins (LinkEntities): consultas planas con condición In.
    ///  - Si una consulta trae `MoreRecords` = true, falla con <see cref="InvalidOperationException"/>: una foto truncada haría
    ///    que el planificador tome por nuevo algo que ya existe. Nunca se trabaja con una foto incompleta.
    ///  - Un choice fuera del enum o una columna requerida en nulo es <see cref="InvalidOperationException"/> que nombra tabla,
    ///    columna y registro (mismo criterio que <see cref="CatalogosDataverse"/>).
    /// </summary>
    public sealed class FotoDeCatalogosDataverse
    {
        /// <summary>Nombre del rol que hace de un usuario un ejecutivo válido como dueño de un cliente (diseno/04 §2).</summary>
        public const string RolEjecutivo = "SR - MPPP - Ejecutivo";

        private const string ColumnaEstado = "statecode";
        private const string ColumnaIdCliente = Tablas.Cliente + "id";

        private readonly IOrganizationService _catalogos;
        private readonly IOrganizationService _sistema;

        public FotoDeCatalogosDataverse(IOrganizationService catalogos, IOrganizationService sistema)
        {
            _catalogos = catalogos ?? throw new ArgumentNullException(nameof(catalogos));
            _sistema = sistema ?? throw new ArgumentNullException(nameof(sistema));
        }

        /// <summary>
        /// Excel 1. Clientes por CIF BAC o CIF COM; planes por código, MÁS los clientes dueños de esos planes aunque el archivo no
        /// los nombre (el planificador nombra al dueño real de un plan ajeno, DN-04 regla 6); correos por correo; autorizaciones por
        /// cliente de los pares (correo encontrado × cliente encontrado); usuarios por "Correo ejecutivo", con
        /// <see cref="UsuarioCandidato.Habilitado"/> = no deshabilitado y <see cref="UsuarioCandidato.EsEjecutivo"/> = tiene el rol
        /// <see cref="RolEjecutivo"/>.
        /// </summary>
        public EstadoDeCatalogos ParaCarga(ClavesDeCarga claves)
        {
            if (claves == null) throw new ArgumentNullException(nameof(claves));

            var clientes = LeerClientes(claves.CifsBac, claves.CifsCom);
            var idsNombrados = new HashSet<Guid>(clientes.Select(c => c.Id));

            var planes = LeerPlanes(claves.CodigosDePlan);

            // DN-04 regla 6: el planificador nombra al dueño real de un plan ajeno, y ese cliente puede no estar en el archivo.
            // UNA consulta más por id, y solo si falta alguno (no una por plan).
            var duenosQueFaltan = planes.Select(p => p.ClienteId).Where(id => !idsNombrados.Contains(id)).Distinct().ToList();
            if (duenosQueFaltan.Count > 0) clientes.AddRange(LeerClientesPorId(duenosQueFaltan));

            var autorizados = LeerAutorizados(claves.Correos);

            // Solo los pares (correo encontrado × cliente nombrado en el archivo): los dueños traídos arriba no entran acá.
            var autorizaciones = LeerAutorizaciones(
                clientesIds: idsNombrados.ToList(),
                autorizadosIds: autorizados.Select(a => a.Id).ToList(),
                ambos: true);

            var usuarios = LeerUsuarios(claves.CorreosDeEjecutivo);
            return new EstadoDeCatalogos(clientes, planes, autorizados, autorizaciones, usuarios);
        }

        /// <summary>
        /// Excel 2. Clientes, planes y correos por sus claves; y TODAS las autorizaciones por cliente de los clientes encontrados
        /// o de los correos encontrados (el planificador cuenta cuántas quedan activas, DN-04 Excel 2 regla 6). Sin usuarios.
        /// </summary>
        public EstadoDeCatalogos ParaCambioDeEstado(ClavesDeCambioDeEstado claves)
        {
            if (claves == null) throw new ArgumentNullException(nameof(claves));

            var clientes = LeerClientes(claves.CifsBac, claves.CifsCom);
            var planes = LeerPlanes(claves.CodigosDePlan);
            var autorizados = LeerAutorizados(claves.Correos);

            // TODOS los pares de los clientes o de los correos encontrados: el planificador cuenta cuántas quedan activas (Excel 2, regla 6).
            var autorizaciones = LeerAutorizaciones(clientes.Select(c => c.Id).ToList(), autorizados.Select(a => a.Id).ToList(), ambos: false);
            return new EstadoDeCatalogos(clientes, planes, autorizados, autorizaciones, new UsuarioCandidato[0]);
        }

        // ================================================================================================ consultas de catálogos

        private List<ClienteExistente> LeerClientes(IReadOnlyCollection<string> cifsBac, IReadOnlyCollection<string> cifsCom)
        {
            // Una condición In con lista vacía no se manda: si no hay claves de ese tipo, no hay condición (y sin ninguna, no hay consulta).
            var consulta = NuevaConsulta(Tablas.Cliente, LogicalOperator.Or, "sanic_cifbac", "sanic_cifcom", "sanic_nombre", "ownerid", ColumnaEstado);
            AgregarIn(consulta, "sanic_cifbac", cifsBac);
            AgregarIn(consulta, "sanic_cifcom", cifsCom);
            return Leer(_catalogos, consulta, ACliente);
        }

        private List<ClienteExistente> LeerClientesPorId(IReadOnlyCollection<Guid> ids)
        {
            var consulta = NuevaConsulta(Tablas.Cliente, LogicalOperator.And, "sanic_cifbac", "sanic_cifcom", "sanic_nombre", "ownerid", ColumnaEstado);
            AgregarIn(consulta, ColumnaIdCliente, ids);
            return Leer(_catalogos, consulta, ACliente);
        }

        private List<PlanExistente> LeerPlanes(IReadOnlyCollection<string> codigos)
        {
            var consulta = NuevaConsulta(Tablas.Plan, LogicalOperator.And, "sanic_codigo", "sanic_tipoformato", "sanic_moneda", "sanic_clienteid", ColumnaEstado);
            AgregarIn(consulta, "sanic_codigo", codigos);
            return Leer(_catalogos, consulta, APlan);
        }

        private List<AutorizadoExistente> LeerAutorizados(IReadOnlyCollection<string> correos)
        {
            var consulta = NuevaConsulta(Tablas.Autorizado, LogicalOperator.And, "sanic_nombre", "sanic_nombrecontacto", ColumnaEstado);
            AgregarIn(consulta, "sanic_nombre", correos);
            return Leer(_catalogos, consulta, AAutorizado);
        }

        /// <summary>
        /// <paramref name="ambos"/> = true (carga): cliente Y correo, el producto de los dos conjuntos; si falta uno de los dos, no hay par posible.
        /// false (cambio de estado): cliente O correo.
        /// </summary>
        private List<AutorizacionClienteExistente> LeerAutorizaciones(IReadOnlyCollection<Guid> clientesIds, IReadOnlyCollection<Guid> autorizadosIds, bool ambos)
        {
            if (ambos && (clientesIds.Count == 0 || autorizadosIds.Count == 0)) return new List<AutorizacionClienteExistente>();

            var consulta = NuevaConsulta(Tablas.AutorizacionCliente, ambos ? LogicalOperator.And : LogicalOperator.Or,
                "sanic_autorizadoid", "sanic_clienteid", "sanic_fechadocumento", "sanic_comentarios", ColumnaEstado);
            AgregarIn(consulta, "sanic_clienteid", clientesIds);
            AgregarIn(consulta, "sanic_autorizadoid", autorizadosIds);
            return Leer(_catalogos, consulta, AAutorizacion);
        }

        // ================================================================================================ usuarios y roles (SYSTEM)

        private List<UsuarioCandidato> LeerUsuarios(IReadOnlyCollection<string> correos)
        {
            // Sin correos de ejecutivo que resolver no se consulta nada, ni el rol.
            if (correos.Count == 0) return new List<UsuarioCandidato>();

            // Un rol que no existe en el entorno es un error de configuración, no "este usuario no es ejecutivo": cada fila diría eso
            // y esconderían la causa. Se comprueba aunque ningún usuario coincida.
            var roles = IdsDelRolEjecutivo();

            var consulta = NuevaConsulta(TablasNativas.Usuario, LogicalOperator.And, "internalemailaddress", "isdisabled");
            AgregarIn(consulta, "internalemailaddress", correos);
            var registros = LeerRegistros(_sistema, consulta);
            if (registros.Count == 0) return new List<UsuarioCandidato>();

            var ejecutivos = IdsDeUsuariosConElRol(registros.Select(u => u.Id).ToList(), roles);
            var usuarios = new List<UsuarioCandidato>(registros.Count);
            foreach (var u in registros)
            {
                var correo = CatalogosDataverse.TextoOpcional(u, "internalemailaddress");
                if (string.IsNullOrWhiteSpace(correo)) continue; // la consulta es por correo: sin él no hay a quién comparar.

                // Seguridad: un dato que falta falla cerrado. Solo es habilitado el usuario que DICE explícitamente isdisabled = false.
                var habilitado = u.Contains("isdisabled") && u["isdisabled"] is bool d && !d;
                usuarios.Add(new UsuarioCandidato(u.Id, correo, habilitado, ejecutivos.Contains(u.Id)));
            }

            return usuarios;
        }

        /// <summary>
        /// Los ids del rol <see cref="RolEjecutivo"/>: existe una vez POR UNIDAD DE NEGOCIO con el mismo nombre, así que se buscan todos.
        /// Ninguno es <see cref="InvalidOperationException"/> que nombra el rol.
        /// </summary>
        private List<Guid> IdsDelRolEjecutivo()
        {
            var consultaRol = NuevaConsulta(TablasNativas.Rol, LogicalOperator.And);
            consultaRol.Criteria.AddCondition("name", ConditionOperator.Equal, RolEjecutivo);
            var roles = LeerRegistros(_sistema, consultaRol).Select(r => r.Id).ToList();
            if (roles.Count == 0)
            {
                throw new InvalidOperationException(
                    "No existe el rol '" + RolEjecutivo + "' en el entorno, así que no se puede saber quién es ejecutivo. "
                    + "Es un problema de configuración: importá la solución con sus roles de seguridad.");
            }

            return roles;
        }

        /// <summary>
        /// Los usuarios de la lista que tienen alguno de esos roles. `systemuserroles` es una intersect entity: `systemuserid` y `roleid`
        /// llegan como Guid crudo (no EntityReference), y se aceptan los dos por si el SDK cambia.
        /// </summary>
        private HashSet<Guid> IdsDeUsuariosConElRol(IReadOnlyCollection<Guid> usuariosIds, IReadOnlyCollection<Guid> roles)
        {
            var consultaAsignaciones = NuevaConsulta(TablasNativas.UsuarioRol, LogicalOperator.And, "systemuserid", "roleid");
            AgregarIn(consultaAsignaciones, "systemuserid", usuariosIds);
            AgregarIn(consultaAsignaciones, "roleid", roles);

            var conRol = new HashSet<Guid>();
            foreach (var asignacion in LeerRegistros(_sistema, consultaAsignaciones))
            {
                var id = IdDe(asignacion, "systemuserid");
                if (id.HasValue) conRol.Add(id.Value);
            }

            return conRol;
        }

        private static Guid? IdDe(Entity registro, string columna)
        {
            if (!registro.Contains(columna)) return null;
            var crudo = registro[columna];
            if (crudo is Guid id) return id;
            return (crudo as EntityReference)?.Id;
        }

        // ================================================================================================ piezas comunes

        private static QueryExpression NuevaConsulta(string tabla, LogicalOperator operador, params string[] columnas)
        {
            return new QueryExpression(tabla)
            {
                ColumnSet = new ColumnSet(columnas),
                Criteria = new FilterExpression(operador),
            };
        }

        /// <summary>Agrega una condición In. Con lista vacía no agrega nada: Dataverse no admite un In sin valores.</summary>
        private static void AgregarIn<T>(QueryExpression consulta, string columna, IReadOnlyCollection<T> valores)
        {
            if (valores.Count == 0) return;
            consulta.Criteria.AddCondition(columna, ConditionOperator.In, valores.Cast<object>().ToArray());
        }

        private static List<T> Leer<T>(IOrganizationService servicio, QueryExpression consulta, Func<Entity, T> traducir)
        {
            var registros = LeerRegistros(servicio, consulta);
            var resultado = new List<T>(registros.Count);
            foreach (var registro in registros) resultado.Add(traducir(registro));
            return resultado;
        }

        /// <summary>
        /// Sin condiciones no hay consulta: una consulta sin filtro traería la tabla entera. Y una sola página: si Dataverse dice que hay
        /// más, se falla, porque una foto truncada haría tomar por nuevo algo que ya existe. Nunca se pagina en silencio ni se trunca.
        /// </summary>
        private static List<Entity> LeerRegistros(IOrganizationService servicio, QueryExpression consulta)
        {
            if (consulta.Criteria.Conditions.Count == 0 && consulta.Criteria.Filters.Count == 0)
                return new List<Entity>();

            var respuesta = servicio.RetrieveMultiple(consulta);
            if (respuesta.MoreRecords)
            {
                throw new InvalidOperationException(
                    "La consulta sobre la tabla '" + consulta.EntityName + "' tiene más registros de los que devolvió en una página. "
                    + "No se trabaja con una foto incompleta: partí el archivo en uno más chico.");
            }

            return respuesta.Entities.ToList();
        }

        // ================================================================================================ traducción Entity -> tipos del dominio

        private static bool Activo(Entity registro)
        {
            if (registro.Contains(ColumnaEstado) && registro[ColumnaEstado] is OptionSetValue estado)
                return estado.Value == Tablas.Activo;

            throw CatalogosDataverse.ErrorColumna(registro, ColumnaEstado);
        }

        private static ClienteExistente ACliente(Entity r) => new ClienteExistente(
            r.Id,
            CatalogosDataverse.RequeridoTexto(r, "sanic_cifbac"),
            CatalogosDataverse.RequeridoTexto(r, "sanic_cifcom"),
            CatalogosDataverse.RequeridoTexto(r, "sanic_nombre"),
            CatalogosDataverse.RequeridoReferencia(r, "ownerid").Id,
            Activo(r));

        private static PlanExistente APlan(Entity r) => new PlanExistente(
            r.Id,
            CatalogosDataverse.RequeridoTexto(r, "sanic_codigo"),
            CatalogosDataverse.RequeridoReferencia(r, "sanic_clienteid").Id,
            CatalogosDataverse.RequeridoChoice<TipoDeFormatoDelPlan>(r, "sanic_tipoformato"),
            CatalogosDataverse.RequeridoChoice<Moneda>(r, "sanic_moneda"),
            Activo(r));

        // El nombre de contacto es opcional al LEER: la columna se agregó el 2026-09-24 (diseno/02 §2.3) y un correo anterior puede no tenerla.
        private static AutorizadoExistente AAutorizado(Entity r) => new AutorizadoExistente(
            r.Id,
            CatalogosDataverse.RequeridoTexto(r, "sanic_nombre"),
            CatalogosDataverse.TextoOpcional(r, "sanic_nombrecontacto"),
            Activo(r));

        private static AutorizacionClienteExistente AAutorizacion(Entity r)
        {
            DateTime? fecha = null;
            if (r.Contains("sanic_fechadocumento") && r["sanic_fechadocumento"] is DateTime f)
                fecha = DateTime.SpecifyKind(f.Date, DateTimeKind.Unspecified); // solo fecha

            return new AutorizacionClienteExistente(
                r.Id,
                CatalogosDataverse.RequeridoReferencia(r, "sanic_autorizadoid").Id,
                CatalogosDataverse.RequeridoReferencia(r, "sanic_clienteid").Id,
                fecha,
                CatalogosDataverse.TextoOpcional(r, "sanic_comentarios"),
                Activo(r));
        }
    }

    /// <summary>
    /// Pasada 2: aplica un plan SIN errores con el servicio que recibe (el del usuario que llama, DN-04: los catálogos son
    /// auditados). Solo escribe: ninguna lectura. Reglas (spike D-72, diseno/PENDIENTES.md):
    ///  - Antes de CADA escritura mira el presupuesto; si <c>transcurrido()</c> ya alcanzó <c>presupuesto</c>, lanza
    ///    <see cref="PresupuestoDeEscrituraAgotadoException"/> sin escribir más (la transacción de la Custom API deshace lo escrito).
    ///  - Si una escritura falla, la atrapa SOLO para lanzar <see cref="EscrituraDeCatalogoFallidaException"/> con la fila de
    ///    origen, y no hace NINGUNA otra llamada al servicio después.
    /// </summary>
    public sealed class EscritorDeCatalogos
    {
        private readonly IOrganizationService _servicio;
        private readonly Func<TimeSpan> _transcurrido;
        private readonly TimeSpan _presupuesto;

        public EscritorDeCatalogos(IOrganizationService servicio, Func<TimeSpan> transcurrido, TimeSpan presupuesto)
        {
            _servicio = servicio ?? throw new ArgumentNullException(nameof(servicio));
            _transcurrido = transcurrido ?? throw new ArgumentNullException(nameof(transcurrido));
            _presupuesto = presupuesto;
        }

        /// <summary>Cuántas escrituras van, y la cuenta total: lo que dice la excepción de presupuesto.</summary>
        private sealed class Avance
        {
            public Avance(int total) { Total = total; }

            public int Total { get; }

            public int Escritas { get; set; }
        }

        /// <summary>
        /// Crea en orden Clientes, Planes, Correos, Autorizaciones (el orden de las listas del plan). Columnas:
        /// Cliente: sanic_nombre (razón social), sanic_cifbac, sanic_cifcom, ownerid (systemuser del dueño).
        /// Plan: sanic_codigo, sanic_tipoformato y sanic_moneda (OptionSetValue del enum), sanic_clienteid.
        /// Autorizado: sanic_nombre (el correo), sanic_nombrecontacto.
        /// AutorizacionCliente: sanic_autorizadoid, sanic_clienteid, y sanic_fechadocumento y sanic_comentarios SOLO si vienen.
        /// Nunca escribe sanic_nombre de Plan ni de AutorizacionCliente (lo calcula NombreCalculadoStep). Una referencia a algo
        /// nuevo (<see cref="ReferenciaACliente.CifBacNuevo"/>, <see cref="ReferenciaAAutorizado.CorreoNuevo"/>) se resuelve con el
        /// id que devolvió su Create. Plan con errores, o nulo → <see cref="ArgumentException"/>, sin ninguna llamada.
        /// </summary>
        public void Aplicar(PlanDeCarga plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (plan.TieneErrores) throw new ArgumentException("El plan tiene errores: no se aplica.", nameof(plan));

            var avance = new Avance(plan.Clientes.Count + plan.Planes.Count + plan.Autorizados.Count + plan.Autorizaciones.Count);

            // Lo nuevo se resuelve con el id que devolvió su Create: por CIF BAC el cliente, por correo el autorizado.
            var clientesNuevos = new Dictionary<string, Guid>(StringComparer.Ordinal);
            var autorizadosNuevos = new Dictionary<string, Guid>(StringComparer.Ordinal);

            foreach (var c in plan.Clientes)
            {
                var cliente = new Entity(Tablas.Cliente);
                cliente["sanic_nombre"] = c.RazonSocial;
                cliente["sanic_cifbac"] = c.CifBac;
                cliente["sanic_cifcom"] = c.CifCom;
                cliente["ownerid"] = new EntityReference(TablasNativas.Usuario, c.DuenoId);
                clientesNuevos[c.CifBac] = Crear(cliente, c.FilaOrigen, avance);
            }

            foreach (var p in plan.Planes)
            {
                // Nunca sanic_nombre: lo calcula NombreCalculadoStep.
                var entidad = new Entity(Tablas.Plan);
                entidad["sanic_codigo"] = p.Codigo;
                entidad["sanic_tipoformato"] = new OptionSetValue((int)p.Formato);
                entidad["sanic_moneda"] = new OptionSetValue((int)p.Moneda);
                entidad["sanic_clienteid"] = new EntityReference(Tablas.Cliente, IdDelCliente(p.Cliente, clientesNuevos));
                Crear(entidad, p.FilaOrigen, avance);
            }

            foreach (var a in plan.Autorizados)
            {
                var entidad = new Entity(Tablas.Autorizado);
                entidad["sanic_nombre"] = a.Correo;
                entidad["sanic_nombrecontacto"] = a.NombreContacto;
                autorizadosNuevos[a.Correo] = Crear(entidad, a.FilaOrigen, avance);
            }

            foreach (var a in plan.Autorizaciones)
            {
                var entidad = new Entity(Tablas.AutorizacionCliente);
                entidad["sanic_autorizadoid"] = new EntityReference(Tablas.Autorizado, IdDelAutorizado(a.Autorizado, autorizadosNuevos));
                entidad["sanic_clienteid"] = new EntityReference(Tablas.Cliente, IdDelCliente(a.Cliente, clientesNuevos));
                // Solo si vienen. La fecha es solo fecha: se escribe el DateTime tal cual sale del plan (Unspecified, 00:00).
                if (a.FechaDocumento.HasValue) entidad["sanic_fechadocumento"] = a.FechaDocumento.Value;
                if (a.Comentarios != null) entidad["sanic_comentarios"] = a.Comentarios;
                Crear(entidad, a.FilaOrigen, avance);
            }
        }

        private static Guid IdDelCliente(ReferenciaACliente referencia, Dictionary<string, Guid> nuevos)
        {
            if (referencia.IdExistente.HasValue) return referencia.IdExistente.Value;

            Guid id;
            if (referencia.CifBacNuevo != null && nuevos.TryGetValue(referencia.CifBacNuevo, out id)) return id;

            throw new InvalidOperationException("El plan apunta a un cliente nuevo (CIF BAC " + referencia.CifBacNuevo + ") que no figura entre los que crea.");
        }

        private static Guid IdDelAutorizado(ReferenciaAAutorizado referencia, Dictionary<string, Guid> nuevos)
        {
            if (referencia.IdExistente.HasValue) return referencia.IdExistente.Value;

            Guid id;
            if (referencia.CorreoNuevo != null && nuevos.TryGetValue(referencia.CorreoNuevo, out id)) return id;

            throw new InvalidOperationException("El plan apunta a un correo nuevo (" + referencia.CorreoNuevo + ") que no figura entre los que crea.");
        }

        /// <summary>
        /// Un Update por cambio, sobre la tabla de su <see cref="TipoDeCatalogo"/>, con statecode/statuscode nativos:
        /// Activar = 0/1, Desactivar = 1/2. Plan con errores, o nulo → <see cref="ArgumentException"/>, sin ninguna llamada.
        /// </summary>
        public void Aplicar(PlanDeCambioDeEstado plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (plan.TieneErrores) throw new ArgumentException("El plan tiene errores: no se aplica.", nameof(plan));

            // Un valor fuera del enum es un error de quien armó el plan, y se descubre ANTES de escribir nada: la transacción de la Custom API
            // lo deshace igual, pero no hay por qué gastar el presupuesto de tiempo en escrituras que se van a perder.
            foreach (var cambio in plan.Cambios)
            {
                if (!Enum.IsDefined(typeof(TipoDeCatalogo), cambio.Tipo))
                    throw new ArgumentOutOfRangeException(nameof(plan), cambio.Tipo, "El tipo de catálogo del cambio de la fila " + cambio.FilaOrigen.ToString(CultureInfo.InvariantCulture) + " no existe.");
                if (!Enum.IsDefined(typeof(AccionDeEstado), cambio.Accion))
                    throw new ArgumentOutOfRangeException(nameof(plan), cambio.Accion, "La acción del cambio de la fila " + cambio.FilaOrigen.ToString(CultureInfo.InvariantCulture) + " no existe.");
            }

            var avance = new Avance(plan.Cambios.Count);
            foreach (var cambio in plan.Cambios)
            {
                // El estado nativo: Activo = 0 / motivo 1, Inactivo = 1 / motivo 2 (diseno/02, encabezado).
                var activar = cambio.Accion == AccionDeEstado.Activar;
                var entidad = new Entity(TablaDe(cambio.Tipo), cambio.Id);
                entidad["statecode"] = new OptionSetValue(activar ? 0 : 1);
                entidad["statuscode"] = new OptionSetValue(activar ? 1 : 2);
                Escribir(() => _servicio.Update(entidad), cambio.FilaOrigen, avance);
            }
        }

        private static string TablaDe(TipoDeCatalogo tipo)
        {
            switch (tipo)
            {
                case TipoDeCatalogo.Cliente: return Tablas.Cliente;
                case TipoDeCatalogo.Plan: return Tablas.Plan;
                case TipoDeCatalogo.Correo: return Tablas.Autorizado;
                case TipoDeCatalogo.AutorizacionCliente: return Tablas.AutorizacionCliente;
                default: throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "El tipo de catálogo no existe."); // ya se validó antes de escribir; sin caída silenciosa.
            }
        }

        private Guid Crear(Entity entidad, int fila, Avance avance)
        {
            var id = Guid.Empty;
            Escribir(() => id = _servicio.Create(entidad), fila, avance);
            return id;
        }

        /// <summary>
        /// El tiempo se mira ANTES de cada escritura (DN-04, "Presupuesto de 2 minutos"). El fallo de la escritura se atrapa SOLO para
        /// decir qué fila era, y se relanza sin ninguna otra llamada al servicio: un plugin que se traga un error y sigue rompe la
        /// transacción (spike D-72, diseno/PENDIENTES.md). Cualquier otra excepción sube como vino.
        /// </summary>
        private void Escribir(Action escritura, int fila, Avance avance)
        {
            if (_transcurrido() >= _presupuesto)
                throw new PresupuestoDeEscrituraAgotadoException(avance.Escritas, avance.Total);

            try
            {
                escritura();
            }
            catch (FaultException<OrganizationServiceFault> falla)
            {
                var motivo = falla.Detail != null && !string.IsNullOrWhiteSpace(falla.Detail.Message) ? falla.Detail.Message : falla.Message;
                throw new EscrituraDeCatalogoFallidaException(fila, motivo, falla, falla.Detail?.ErrorCode);
            }

            avance.Escritas++;
        }
    }

    /// <summary>Una escritura de la pasada 2 falló (MPPP-CAR-009). La Custom API la deja subir: la transacción deshace todo.</summary>
    public sealed class EscrituraDeCatalogoFallidaException : Exception
    {
        public EscrituraDeCatalogoFallidaException(int fila, string motivo)
            : base("Fila " + fila.ToString(System.Globalization.CultureInfo.InvariantCulture) + ": " + motivo)
        {
            Fila = fila;
            Motivo = motivo;
        }

        /// <summary>
        /// Igual que la anterior, y conserva la falla original de Dataverse como <see cref="Exception.InnerException"/>: la traza del
        /// plugin la necesita entera (código de error, detalles), y el mensaje de arriba es solo lo que se muestra.
        /// </summary>
        public EscrituraDeCatalogoFallidaException(int fila, string motivo, Exception interna)
            : base("Fila " + fila.ToString(System.Globalization.CultureInfo.InvariantCulture) + ": " + motivo, interna)
        {
            Fila = fila;
            Motivo = motivo;
        }

        /// <summary>
        /// Como la anterior, con el código de Dataverse. Mensaje: "Fila N (código 0xXXXXXXXX): motivo" si hay código (8 dígitos
        /// hexadecimales en mayúsculas), y "Fila N: motivo" si es null. El mensaje va a la traza, nunca al usuario (D-75).
        /// </summary>
        public EscrituraDeCatalogoFallidaException(int fila, string motivo, Exception interna, int? codigoDeError)
            : base(Mensaje(fila, motivo, codigoDeError), interna)
        {
            Fila = fila;
            Motivo = motivo;
            CodigoDeError = codigoDeError;
        }

        private static string Mensaje(int fila, string motivo, int? codigoDeError)
        {
            var encabezado = "Fila " + fila.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (codigoDeError.HasValue)
                encabezado += " (código " + FallasDePlataforma.EnHexadecimal(codigoDeError.Value) + ")";
            return encabezado + ": " + motivo;
        }

        /// <summary>La fila de Excel de origen de lo que no se pudo escribir.</summary>
        public int Fila { get; }

        /// <summary>Lo que dijo Dataverse.</summary>
        public string Motivo { get; }

        /// <summary>
        /// El <c>ErrorCode</c> del <c>OrganizationServiceFault</c>, o null si la falla no traía detalle. Con él la Custom API distingue
        /// una falla de plataforma (MPPP-CAR-011) de un error de dato (009), y va a la traza en hexadecimal (D-75).
        /// </summary>
        public int? CodigoDeError { get; }
    }

    /// <summary>
    /// Los <c>ErrorCode</c> de Dataverse que son una falla de PLATAFORMA (sobrecarga, límites, tiempo agotado) y no un error del dato
    /// que se intentó grabar (D-75). Se clasifica SIEMPRE por código, nunca por el texto del mensaje. Fuente: Learn, "Web service
    /// error codes" y "Troubleshoot Dataverse plug-ins". Un código que no está acá es un error de dato (MPPP-CAR-009).
    /// </summary>
    internal static class FallasDePlataforma
    {
        private static readonly HashSet<int> Codigos = new HashSet<int>
        {
            unchecked((int)0x80072324), // AzureSqlMaxConcurrentWorkersLimitExceededError (candidato para el 10928 de Azure SQL; PENDIENTE de verificar en Dev, D-75)
            unchecked((int)0x80072323), // AzureSqlDatabaseResourceLimitExceededError
            unchecked((int)0x80072326), // ThrottlingConcurrencyLimitExceededError
            unchecked((int)0x80072321), // ThrottlingTimeExceededError
            unchecked((int)0x80072322), // ThrottlingBurstRequestLimitExceededError
            unchecked((int)0x8005F103), // Throttling
            unchecked((int)0x80072330), // CacheLoadConcurrencyError
            -2147204783,                // "Sql error: Execution Timeout Expired" (Troubleshoot Dataverse plug-ins)
        };

        /// <summary>El código como se muestra en la traza: "0x" y 8 dígitos hexadecimales en mayúsculas (0x80072324).</summary>
        public static string EnHexadecimal(int codigoDeError)
        {
            return "0x" + codigoDeError.ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>True si el código es de plataforma; null (la falla no traía detalle) nunca lo es.</summary>
        public static bool EsDePlataforma(int? codigoDeError)
        {
            return codigoDeError.HasValue && Codigos.Contains(codigoDeError.Value);
        }
    }

    /// <summary>Se alcanzó el presupuesto de tiempo antes de terminar (MPPP-CAR-008).</summary>
    public sealed class PresupuestoDeEscrituraAgotadoException : Exception
    {
        public PresupuestoDeEscrituraAgotadoException(int escritas, int total)
            : base("Se alcanzó el tiempo máximo después de " + escritas.ToString(System.Globalization.CultureInfo.InvariantCulture)
                   + " de " + total.ToString(System.Globalization.CultureInfo.InvariantCulture) + " escrituras.")
        {
            Escritas = escritas;
            Total = total;
        }

        public int Escritas { get; }

        public int Total { get; }
    }
}
