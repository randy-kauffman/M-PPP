using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;

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
            throw new NotImplementedException();
        }

        /// <summary>
        /// De <paramref name="roleIds"/>, los que el usuario tiene: directo o por algún equipo del que es miembro.
        /// </summary>
        public static ISet<Guid> RolesDelUsuario(IOrganizationService servicio, Guid usuarioId, IReadOnlyCollection<Guid> roleIds)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Los usuarios que tienen alguno de <paramref name="roleIds"/>, directo o por equipo, sin repetidos. Con
        /// <paramref name="usuariosFiltro"/> (no nulo) solo se consideran esos usuarios; filtro vacío → vacío sin consultar.
        /// No filtra habilitados: eso lo decide cada llamador.
        /// </summary>
        public static ISet<Guid> UsuariosConElRol(IOrganizationService servicio, IReadOnlyCollection<Guid> roleIds, IReadOnlyCollection<Guid> usuariosFiltro = null)
        {
            throw new NotImplementedException();
        }
    }
}
