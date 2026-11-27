using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Sanic.Mppp.Plugins.Datos
{
    /// <summary>
    /// DN-08 (diseno/DIRECTRICES-NUEVAS.md): quién TIENE un rol de MPPP. Un usuario tiene un rol si lo tiene asignado directo
    /// (`systemuserroles`) o si es miembro (`teammembership`) de un equipo que lo tiene (`teamroles`): de propietario o de grupo
    /// de Entra. Es la ÚNICA pieza que responde esa pregunta: la usan `PlomeriaDeFila.DeterminarRoles`, `AvisosDataverse`,
    /// `FotoDeCatalogosDataverse` (carga masiva) y `PuertaDelRpa`, para que no vuelvan a divergir.
    ///
    /// Contrato (lo fijan las pruebas del arquitecto, `Dn08Aceptacion`):
    ///  - número FIJO de consultas, nunca una por equipo ni por usuario (a lo sumo 1 en <see cref="IdsPorNombre"/> y 3 en
    ///    cada una de las otras dos; páginas aparte);
    ///  - las tres tablas son intersect entities: sus columnas llegan como `Guid` crudo; se aceptan también como
    ///    `EntityReference`, por si el SDK cambia (mismo criterio que D-59);
    ///  - una lista `In` vacía no se manda (Dataverse no la admite): sin ids que buscar se devuelve vacío sin consultar;
    ///  - no se pagina, igual que las cuatro consultas de antes: los usuarios con roles de MPPP son decenas, no miles;
    ///  - no lanza por "no hay rol": decidir qué significa un rol no instalado es de cada llamador.
    /// </summary>
    public static class RolesDeMppp
    {
        /// <summary>
        /// Los ids de los roles con esos nombres (comparación de `name` de Dataverse: sin distinguir caja), con su nombre.
        /// Un rol existe una vez POR UNIDAD DE NEGOCIO con el mismo nombre: vienen TODOS. Ninguno → diccionario vacío.
        /// </summary>
        public static IReadOnlyDictionary<Guid, string> IdsPorNombre(IOrganizationService servicio, IReadOnlyCollection<string> nombres)
        {
            if (servicio == null) throw new ArgumentNullException(nameof(servicio));
            var resultado = new Dictionary<Guid, string>();
            if (nombres == null || nombres.Count == 0) return resultado;

            var consulta = new QueryExpression(TablasNativas.Rol) { ColumnSet = new ColumnSet("name") };
            consulta.Criteria.AddCondition("name", ConditionOperator.In, nombres.Cast<object>().ToArray());
            foreach (var rol in servicio.RetrieveMultiple(consulta).Entities)
            {
                resultado[rol.Id] = rol.GetAttributeValue<string>("name");
            }

            return resultado;
        }

        /// <summary>
        /// De <paramref name="roleIds"/>, los que el usuario tiene: directo o por algún equipo del que es miembro.
        /// </summary>
        public static ISet<Guid> RolesDelUsuario(IOrganizationService servicio, Guid usuarioId, IReadOnlyCollection<Guid> roleIds)
        {
            if (servicio == null) throw new ArgumentNullException(nameof(servicio));
            var tiene = new HashSet<Guid>();
            if (roleIds == null || roleIds.Count == 0) return tiene;
            var pedidos = new HashSet<Guid>(roleIds);

            // Consulta 1: asignaciones directas.
            var directas = new QueryExpression(TablasNativas.UsuarioRol) { ColumnSet = new ColumnSet("roleid") };
            directas.Criteria.AddCondition("systemuserid", ConditionOperator.Equal, usuarioId);
            directas.Criteria.AddCondition("roleid", ConditionOperator.In, Valores(pedidos));
            foreach (var fila in servicio.RetrieveMultiple(directas).Entities)
            {
                var id = IdDe(fila, "roleid");
                if (id.HasValue && pedidos.Contains(id.Value)) tiene.Add(id.Value);
            }

            // Consulta 2: los equipos del usuario.
            var membresias = new QueryExpression(TablasNativas.MiembroDeEquipo) { ColumnSet = new ColumnSet("teamid") };
            membresias.Criteria.AddCondition("systemuserid", ConditionOperator.Equal, usuarioId);
            var equipos = new HashSet<Guid>();
            foreach (var fila in servicio.RetrieveMultiple(membresias).Entities)
            {
                var id = IdDe(fila, "teamid");
                if (id.HasValue) equipos.Add(id.Value);
            }

            // Sin equipos no hay nada que mirar (y un `In` vacío no se manda).
            if (equipos.Count == 0) return tiene;

            // Consulta 3: los roles de esos equipos.
            var porEquipo = new QueryExpression(TablasNativas.RolDeEquipo) { ColumnSet = new ColumnSet("roleid", "teamid") };
            porEquipo.Criteria.AddCondition("teamid", ConditionOperator.In, Valores(equipos));
            porEquipo.Criteria.AddCondition("roleid", ConditionOperator.In, Valores(pedidos));
            foreach (var fila in servicio.RetrieveMultiple(porEquipo).Entities)
            {
                var id = IdDe(fila, "roleid");
                if (id.HasValue && pedidos.Contains(id.Value)) tiene.Add(id.Value);
            }

            return tiene;
        }

        /// <summary>
        /// Los usuarios que tienen alguno de <paramref name="roleIds"/>, directo o por equipo, sin repetidos. Con
        /// <paramref name="usuariosFiltro"/> (no nulo) solo se consideran esos usuarios; filtro vacío → vacío sin consultar.
        /// No filtra habilitados: eso lo decide cada llamador.
        /// </summary>
        public static ISet<Guid> UsuariosConElRol(IOrganizationService servicio, IReadOnlyCollection<Guid> roleIds, IReadOnlyCollection<Guid> usuariosFiltro = null)
        {
            if (servicio == null) throw new ArgumentNullException(nameof(servicio));
            var usuarios = new HashSet<Guid>();
            if (roleIds == null || roleIds.Count == 0) return usuarios;
            if (usuariosFiltro != null && usuariosFiltro.Count == 0) return usuarios;
            var filtro = usuariosFiltro == null ? null : new HashSet<Guid>(usuariosFiltro);
            var roles = new HashSet<Guid>(roleIds);

            // Consulta 1: asignaciones directas.
            var directas = new QueryExpression(TablasNativas.UsuarioRol) { ColumnSet = new ColumnSet("systemuserid", "roleid") };
            directas.Criteria.AddCondition("roleid", ConditionOperator.In, Valores(roles));
            if (filtro != null) directas.Criteria.AddCondition("systemuserid", ConditionOperator.In, Valores(filtro));
            foreach (var fila in servicio.RetrieveMultiple(directas).Entities)
            {
                var id = IdDe(fila, "systemuserid");
                if (id.HasValue && (filtro == null || filtro.Contains(id.Value))) usuarios.Add(id.Value);
            }

            // Consulta 2: los equipos que tienen alguno de los roles.
            var porEquipo = new QueryExpression(TablasNativas.RolDeEquipo) { ColumnSet = new ColumnSet("teamid", "roleid") };
            porEquipo.Criteria.AddCondition("roleid", ConditionOperator.In, Valores(roles));
            var equipos = new HashSet<Guid>();
            foreach (var fila in servicio.RetrieveMultiple(porEquipo).Entities)
            {
                var id = IdDe(fila, "teamid");
                if (id.HasValue) equipos.Add(id.Value);
            }

            if (equipos.Count == 0) return usuarios;

            // Consulta 3: los miembros de esos equipos.
            var miembros = new QueryExpression(TablasNativas.MiembroDeEquipo) { ColumnSet = new ColumnSet("systemuserid", "teamid") };
            miembros.Criteria.AddCondition("teamid", ConditionOperator.In, Valores(equipos));
            if (filtro != null) miembros.Criteria.AddCondition("systemuserid", ConditionOperator.In, Valores(filtro));
            foreach (var fila in servicio.RetrieveMultiple(miembros).Entities)
            {
                var id = IdDe(fila, "systemuserid");
                if (id.HasValue && (filtro == null || filtro.Contains(id.Value))) usuarios.Add(id.Value);
            }

            return usuarios;
        }

        private static object[] Valores(IEnumerable<Guid> ids) => ids.Cast<object>().ToArray();

        /// <summary>El id de una columna de intersect entity, venga como `Guid` crudo o como `EntityReference`.</summary>
        private static Guid? IdDe(Entity fila, string columna)
        {
            if (!fila.Contains(columna)) return null;
            var crudo = fila[columna];
            if (crudo is Guid id) return id;
            return (crudo as EntityReference)?.Id;
        }
    }
}
