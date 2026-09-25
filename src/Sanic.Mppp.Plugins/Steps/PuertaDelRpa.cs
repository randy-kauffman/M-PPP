using System;
using System.ServiceModel;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Mppp.Plugins.Datos;

namespace Sanic.Mppp.Plugins.Steps
{
    /// <summary>
    /// La puerta de las dos Custom API de fase 2 (diseno/03 encabezado, D-9).
    ///
    /// **Por qué está en el código y no en la plataforma.** `ExecutePrivilegeName` exige nombrar un privilegio que YA
    /// exista, y el RPA no tiene ninguno propio: todo lo que tiene lo tiene también el Ejecutivo. Cualquier privilegio
    /// existente le abriría estas API a los ejecutivos, y `registrarresultadorpa` transiciona filas a Digitada y a
    /// Aprobada: sería romper los cuatro ojos. Por eso las dos API llevan `ExecutePrivilegeName` VACÍO y la puerta es
    /// esta clase.
    ///
    /// **Dos condiciones, las dos obligatorias** (falla cerrado):
    ///  1. quien llama es una **identidad de aplicación** (`systemuser.applicationid` con valor). Una persona no tiene
    ///     esa columna, nunca, así que ningún humano pasa — ni con el rol asignado por error;
    ///  2. quien llama tiene el rol **`SR - MPPP - RPA`**, resuelto por `name` (el nombre viaja con la solución; el
    ///     `roleid` es de cada entorno).
    ///
    /// **Y una comprobación previa que no es cosmética**: si el rol NO EXISTE en el entorno, el error lo dice
    /// (`MPPP-RPA-007`) en vez de devolver un "no autorizado" genérico. Sin eso, quien lea el error sale a revisar
    /// permisos cuando el problema es que nadie creó un rol (inventario 6.7). Esa confusión costó horas dos veces la
    /// semana del 2026-09-22: el `Name not found` que era falta de publicar, y el rechazo de la lista blanca que era
    /// falta del parámetro `servicio.cuenta`.
    /// </summary>
    public static class PuertaDelRpa
    {
        /// <summary>El rol que habilita. Se compara por `name`, sin distinguir caja.</summary>
        public const string Rol = TablasNativas.RolRpa;

        public const string MensajeRolNoInstalado =
            "MPPP-RPA-007: el rol '" + TablasNativas.RolRpa + "' no está creado en este entorno (inventario 6.7); " +
            "sin él ninguna llamada del bot puede autorizarse. No es un problema de permisos del usuario.";

        public const string MensajeNoAutorizado =
            "MPPP-RPA-006: quien llama no es el usuario de aplicación del RPA. Estas API exigen una identidad de " +
            "aplicación con el rol '" + TablasNativas.RolRpa + "' (diseno/03, D-9).";

        /// <summary>
        /// Deja pasar, o lanza <see cref="InvalidPluginExecutionException"/> con su código. Nunca devuelve `false`:
        /// una puerta que devuelve un booleano es una puerta que alguien se olvida de mirar.
        /// </summary>
        public static void Exigir(IPluginExecutionContext contexto, IOrganizationService servicio)
        {
            // Primero el rol: si no existe en el entorno nadie puede pasar, y el motivo tiene que decir
            // eso (MPPP-RPA-007), no "no autorizado" (D-44). La comparación por `name` no distingue caja
            // porque Dataverse no la distingue en un Equal de texto (Microsoft Learn).
            var consultaRol = new QueryExpression(TablasNativas.Rol) { ColumnSet = new ColumnSet() };
            consultaRol.Criteria.AddCondition("name", ConditionOperator.Equal, Rol);
            var rol = servicio.RetrieveMultiple(consultaRol).Entities.FirstOrDefault();
            if (rol == null)
            {
                throw new InvalidPluginExecutionException(MensajeRolNoInstalado);
            }

            // Un Retrieve que falla no puede tumbar la operación con una NullReferenceException: si el
            // usuario no se puede leer, se trata igual que si no estuviera autorizado.
            Entity usuario;
            try
            {
                usuario = servicio.Retrieve(TablasNativas.Usuario, contexto.InitiatingUserId, new ColumnSet("applicationid"));
            }
            catch (FaultException<OrganizationServiceFault>)
            {
                // SOLO el fallo de la plataforma (el usuario no existe, no se puede leer). Un `catch (Exception)`
                // suelto convertiria CUALQUIER defecto nuestro —una NullReferenceException, un tipo mal leido— en
                // un tranquilizador "no autorizado", y el bug viviria escondido detras de un mensaje de seguridad
                // durante meses. Mismo criterio que `BaseDeStep.EsCodigoDeServidor`.
                throw new InvalidPluginExecutionException(MensajeNoAutorizado);
            }

            var esIdentidadDeAplicacion = usuario.GetAttributeValue<Guid?>("applicationid").HasValue;
            if (!esIdentidadDeAplicacion)
            {
                throw new InvalidPluginExecutionException(MensajeNoAutorizado);
            }

            var consultaAsignacion = new QueryExpression(TablasNativas.UsuarioRol) { ColumnSet = new ColumnSet() };
            consultaAsignacion.Criteria.AddCondition("systemuserid", ConditionOperator.Equal, contexto.InitiatingUserId);
            consultaAsignacion.Criteria.AddCondition("roleid", ConditionOperator.Equal, rol.Id);
            var tieneElRol = servicio.RetrieveMultiple(consultaAsignacion).Entities.Count > 0;
            if (!tieneElRol)
            {
                throw new InvalidPluginExecutionException(MensajeNoAutorizado);
            }
        }
    }
}
