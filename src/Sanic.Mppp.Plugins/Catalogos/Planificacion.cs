using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Sanic.Mppp.Plugins.Dominio;

namespace Sanic.Mppp.Plugins.Catalogos
{
    /// <summary>
    /// Una planificación de la carga: sobre las filas ya leídas y la foto de lo que existe, resuelve a qué cliente, plan, correo y
    /// autorización se refiere cada fila, junta los errores de cada fila (DN-04, "Reglas por fila" 1 a 14) y de coherencia entre
    /// filas (15 y 16), y decide qué se crea. Una instancia por llamada: no tiene estado que sobreviva.
    ///
    /// Lineal: todo lo que se busca está en un diccionario armado una vez. Lo único cuadrático es nombrar las filas en conflicto
    /// dentro de un grupo que ya es error, donde el archivo no se graba de todos modos.
    /// </summary>
    internal sealed class Planificacion
    {
        private readonly List<FilaDeTrabajo> _filas;

        // La foto de lo que existe, indexada. Las claves ya vienen normalizadas como las guarda el step.
        private readonly Dictionary<string, ClienteExistente> _clientesPorBac = new Dictionary<string, ClienteExistente>(StringComparer.Ordinal);
        private readonly Dictionary<string, ClienteExistente> _clientesPorCom = new Dictionary<string, ClienteExistente>(StringComparer.Ordinal);
        private readonly Dictionary<Guid, ClienteExistente> _clientesPorId = new Dictionary<Guid, ClienteExistente>();
        private readonly Dictionary<string, PlanExistente> _planesPorCodigo = new Dictionary<string, PlanExistente>(StringComparer.Ordinal);
        private readonly Dictionary<string, AutorizadoExistente> _autorizadosPorCorreo = new Dictionary<string, AutorizadoExistente>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AutorizacionClienteExistente> _autorizacionesPorPar = new Dictionary<string, AutorizacionClienteExistente>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<UsuarioCandidato>> _usuariosPorCorreo = new Dictionary<string, List<UsuarioCandidato>>(StringComparer.OrdinalIgnoreCase);

        // Un mismo CIF BAC y CIF COM, aunque estén en filas distintas, son el mismo cliente nuevo: unión de claves.
        private readonly Dictionary<string, string> _padre = new Dictionary<string, string>(StringComparer.Ordinal);

        private readonly Dictionary<string, GrupoDeCliente> _indiceClientes = new Dictionary<string, GrupoDeCliente>(StringComparer.Ordinal);
        private readonly List<GrupoDeCliente> _gruposDeCliente = new List<GrupoDeCliente>();
        private readonly Dictionary<string, GrupoDePlan> _indicePlanes = new Dictionary<string, GrupoDePlan>(StringComparer.Ordinal);
        private readonly List<GrupoDePlan> _gruposDePlan = new List<GrupoDePlan>();
        private readonly Dictionary<string, GrupoDeCorreo> _indiceCorreos = new Dictionary<string, GrupoDeCorreo>(StringComparer.Ordinal);
        private readonly List<GrupoDeCorreo> _gruposDeCorreo = new List<GrupoDeCorreo>();
        private readonly Dictionary<string, GrupoDeAutorizacion> _indiceAutorizaciones = new Dictionary<string, GrupoDeAutorizacion>(StringComparer.Ordinal);
        private readonly List<GrupoDeAutorizacion> _gruposDeAutorizacion = new List<GrupoDeAutorizacion>();

        internal Planificacion(List<FilaDeTrabajo> filas, EstadoDeCatalogos estado)
        {
            _filas = filas;

            foreach (var c in estado.Clientes)
            {
                _clientesPorId[c.Id] = c;
                if (!string.IsNullOrWhiteSpace(c.CifBac)) _clientesPorBac[c.CifBac] = c;
                if (!string.IsNullOrWhiteSpace(c.CifCom)) _clientesPorCom[c.CifCom] = c;
            }

            foreach (var p in estado.Planes)
            {
                if (!string.IsNullOrWhiteSpace(p.Codigo)) _planesPorCodigo[p.Codigo] = p;
            }

            foreach (var a in estado.Autorizados)
            {
                if (!string.IsNullOrWhiteSpace(a.Correo)) _autorizadosPorCorreo[a.Correo.Trim()] = a;
            }

            foreach (var au in estado.Autorizaciones)
                _autorizacionesPorPar[Par(au.AutorizadoId, au.ClienteId)] = au;

            foreach (var u in estado.Usuarios)
            {
                if (string.IsNullOrWhiteSpace(u.Correo)) continue;
                List<UsuarioCandidato> lista;
                var correo = u.Correo.Trim();
                if (!_usuariosPorCorreo.TryGetValue(correo, out lista))
                {
                    lista = new List<UsuarioCandidato>();
                    _usuariosPorCorreo[correo] = lista;
                }

                lista.Add(u);
            }
        }

        internal PlanDeCarga Ejecutar()
        {
            foreach (var f in _filas) ResolverCliente(f);
            AgruparClientesNuevos();
            foreach (var g in _gruposDeCliente) ValidarClienteNuevo(g);

            foreach (var f in _filas) ResolverPlan(f);
            foreach (var g in _gruposDePlan) ValidarPlanNuevo(g);

            foreach (var f in _filas) ResolverCorreo(f);
            foreach (var g in _gruposDeCorreo) ValidarCorreoNuevo(g);

            foreach (var f in _filas) ResolverAutorizacion(f);
            foreach (var g in _gruposDeAutorizacion) ValidarAutorizacionNueva(g);

            return Armar();
        }

        // ================================================================================================ cliente (reglas 2, 3 y 4)

        private void ResolverCliente(FilaDeTrabajo f)
        {
            // Si una clave vino mal escrita o no vino ninguna, el lector ya dijo el error: no se inventa uno más.
            if ((f.CifBac.Presente && !f.CifBac.Valido) || (f.CifCom.Presente && !f.CifCom.Valido)) return;
            if (!f.CifBac.Presente && !f.CifCom.Presente) return;

            ClienteExistente porBac = null, porCom = null;
            if (f.CifBac.Usable) _clientesPorBac.TryGetValue(f.CifBac.Valor, out porBac);
            if (f.CifCom.Usable) _clientesPorCom.TryGetValue(f.CifCom.Valor, out porCom);

            ClienteExistente existente;
            if (f.CifBac.Usable && f.CifCom.Usable)
            {
                // Regla 2: los dos CIF tienen que decir lo mismo. Dos clientes distintos, o uno que existe y otro que no, es un error.
                if (porBac != null && porCom != null && porBac.Id != porCom.Id)
                {
                    f.Error(Encabezados.Cliente + ": apuntan a dos clientes distintos (" + porBac.RazonSocial + " y " + porCom.RazonSocial + "). Dejá uno solo o corregí el que está mal.");
                    return;
                }

                if (porBac != null && porCom == null)
                {
                    f.Error(Encabezados.Cliente + ": el CIF BAC " + f.CifBac.Valor + " es del cliente " + porBac.RazonSocial + ", pero el CIF COM " + f.CifCom.Valor + " no está grabado. Dejá uno solo o corregí el que está mal.");
                    return;
                }

                if (porBac == null && porCom != null)
                {
                    f.Error(Encabezados.Cliente + ": el CIF COM " + f.CifCom.Valor + " es del cliente " + porCom.RazonSocial + ", pero el CIF BAC " + f.CifBac.Valor + " no está grabado. Dejá uno solo o corregí el que está mal.");
                    return;
                }

                existente = porBac;
            }
            else
            {
                existente = porBac ?? porCom;
            }

            if (existente != null)
            {
                f.ClienteExistente = existente;
                f.ClaveDeCliente = "E:" + existente.Id.ToString("N");
                ChequearClienteExistente(f, existente);
                return;
            }

            f.EsClienteNuevo = true;
            if (f.CifBac.Usable && f.CifCom.Usable) Unir(NodoBac(f.CifBac.Valor), NodoCom(f.CifCom.Valor));
        }

        private void ChequearClienteExistente(FilaDeTrabajo f, ClienteExistente c)
        {
            if (!c.Activo)
                f.Advertencia(Encabezados.Cliente + ": el cliente " + c.CifBac + " (" + c.RazonSocial + ") está inactivo. Lo que se le cargue no va a estar vigente hasta que se lo active.");

            // Regla 3: la carga nunca modifica lo que existe; si el archivo dice otra cosa, la fila es error.
            if (f.Razon.Usable && !Iguales(f.Razon.Valor, c.RazonSocial))
            {
                f.Error(Encabezados.RazonSocial + ": el cliente " + c.CifBac + " está grabado como \"" + MotivosDeCarga.Mostrar(c.RazonSocial) + "\", no como \"" + MotivosDeCarga.Mostrar(f.Razon.Valor)
                        + "\". La carga no modifica clientes que ya existen: usá la razón social grabada o corregila en el formulario del cliente.");
            }

            if (f.CorreoEjecutivo.Usable && !EsElDueno(f.CorreoEjecutivo.Valor, c.DuenoId))
            {
                f.Error(Encabezados.CorreoEjecutivo + ": " + f.CorreoEjecutivo.Valor + " no es el ejecutivo actual del cliente " + c.CifBac
                        + ". La carga no modifica clientes que ya existen: usá el correo del ejecutivo actual, dejá la celda vacía o reasignalo desde el formulario del cliente.");
            }
        }

        private bool EsElDueno(string correo, Guid duenoId)
        {
            List<UsuarioCandidato> usuarios;
            if (!_usuariosPorCorreo.TryGetValue(correo, out usuarios)) return false;
            return usuarios.Any(u => u.Id == duenoId);
        }

        private void AgruparClientesNuevos()
        {
            foreach (var f in _filas)
            {
                if (!f.EsClienteNuevo) continue;

                var raiz = Raiz(f.CifBac.Usable ? NodoBac(f.CifBac.Valor) : NodoCom(f.CifCom.Valor));
                GrupoDeCliente g;
                if (!_indiceClientes.TryGetValue(raiz, out g))
                {
                    g = new GrupoDeCliente();
                    _indiceClientes[raiz] = g;
                    _gruposDeCliente.Add(g);
                }

                g.Filas.Add(f);
                f.GrupoDeCliente = g;
                f.ClaveDeCliente = "N:" + raiz;
            }
        }

        // Reglas 4, 15 y 16 sobre un cliente que crea este archivo: los datos pueden repartirse entre sus filas mientras no se contradigan.
        private void ValidarClienteNuevo(GrupoDeCliente g)
        {
            var bacs = Distintos(g.Filas, f => f.CifBac);
            var coms = Distintos(g.Filas, f => f.CifCom);

            // Regla 16: un CIF BAC con dos CIF COM en el archivo, o al revés. El error va en las filas que chocan, nombrando a las otras.
            if (bacs.Count > 1 || coms.Count > 1)
            {
                foreach (var f in g.Filas)
                {
                    var otras = new List<int>();
                    foreach (var s in g.Filas)
                    {
                        if (ReferenceEquals(s, f)) continue;
                        if (Difieren(f.CifBac, s.CifBac) || Difieren(f.CifCom, s.CifCom)) otras.Add(s.Numero);
                    }

                    if (otras.Count > 0)
                    {
                        f.Error(Encabezados.Cliente + ": el cliente aparece con otro CIF BAC o CIF COM en " + MotivosDeCarga.Filas(otras)
                                + ". Un CIF BAC va con un solo CIF COM, y al revés: usá siempre el mismo par.");
                    }
                }

                return;
            }

            g.CifBac = bacs.FirstOrDefault();
            g.CifCom = coms.FirstOrDefault();

            // Regla 4: para crear un cliente hacen falta A, B, C y D, juntando todas sus filas.
            if (g.CifBac == null)
                ErrorEnTodas(g, Encabezados.CifBac + ": hace falta para crear " + g.Nombre + ". Completalo en alguna de las filas de ese cliente.");
            if (g.CifCom == null)
                ErrorEnTodas(g, Encabezados.CifCom + ": hace falta para crear " + g.Nombre + ". Completalo en alguna de las filas de ese cliente.");

            var conflictoDeRazon = Contradicciones(g.Filas, f => f.Razon.Usable ? f.Razon.Valor : null, StringComparer.OrdinalIgnoreCase,
                otras => Encabezados.RazonSocial + ": " + g.Nombre + " aparece con otra razón social en " + MotivosDeCarga.Filas(otras) + ". Usá la misma en todas.");
            if (!conflictoDeRazon && !g.Filas.Any(f => f.Razon.Presente))
                ErrorEnTodas(g, Encabezados.RazonSocial + ": hace falta para crear " + g.Nombre + ". Completala en alguna de las filas de ese cliente.");

            var conflictoDeEjecutivo = Contradicciones(g.Filas, f => f.CorreoEjecutivo.Usable ? f.CorreoEjecutivo.Valor : null, StringComparer.Ordinal,
                otras => Encabezados.CorreoEjecutivo + ": " + g.Nombre + " aparece con otro ejecutivo en " + MotivosDeCarga.Filas(otras) + ". Usá el mismo en todas.");
            if (conflictoDeEjecutivo) return;

            if (!g.Filas.Any(f => f.CorreoEjecutivo.Presente))
            {
                ErrorEnTodas(g, Encabezados.CorreoEjecutivo + ": hace falta para crear " + g.Nombre + ". Completalo en alguna de las filas de ese cliente.");
                return;
            }

            var correo = g.PrimerValor(f => f.CorreoEjecutivo);
            if (correo == null) return; // el correo vino mal escrito: el lector ya lo dijo.

            Guid duenoId;
            var motivo = MotivoDelEjecutivo(correo, out duenoId);
            if (motivo == null)
            {
                g.DuenoId = duenoId;
                return;
            }

            foreach (var f in g.Filas)
            {
                if (f.CorreoEjecutivo.Usable) f.Error(motivo);
            }
        }

        /// <summary>
        /// Regla 4: el ejecutivo tiene que ser exactamente un usuario habilitado con el rol Ejecutivo. Un cliente a nombre de alguien
        /// que no lo es no aparece en "Mis clientes" de nadie, y nadie lo nota. Cada caso dice cuál es.
        /// </summary>
        private string MotivoDelEjecutivo(string correo, out Guid duenoId)
        {
            duenoId = Guid.Empty;
            List<UsuarioCandidato> usuarios;
            _usuariosPorCorreo.TryGetValue(correo, out usuarios);

            if (usuarios == null || usuarios.Count == 0)
                return Encabezados.CorreoEjecutivo + ": no hay ningún usuario con el correo " + correo + ". Revisá que esté bien escrito o usá el correo de un usuario que exista.";

            if (usuarios.Count > 1)
            {
                return Encabezados.CorreoEjecutivo + ": hay " + usuarios.Count.ToString(CultureInfo.InvariantCulture) + " usuarios con el correo " + correo
                       + " y no se sabe cuál es el ejecutivo. Pedí que quede uno solo o usá otro correo.";
            }

            var usuario = usuarios[0];
            if (!usuario.Habilitado)
                return Encabezados.CorreoEjecutivo + ": el usuario " + correo + " está deshabilitado. Pedí que lo habiliten o usá otro correo.";

            if (!usuario.EsEjecutivo)
                return Encabezados.CorreoEjecutivo + ": " + correo + " no tiene el rol Ejecutivo (SR - MPPP - Ejecutivo). Pedí que se lo asignen o usá otro correo.";

            duenoId = usuario.Id;
            return null;
        }

        // ================================================================================================ plan (reglas 5, 6 y 7)

        private void ResolverPlan(FilaDeTrabajo f)
        {
            if (!f.CodigoPlan.Usable) return;

            var codigo = f.CodigoPlan.Valor;
            PlanExistente existente;
            if (_planesPorCodigo.TryGetValue(codigo, out existente))
            {
                ChequearPlanExistente(f, existente);
                return;
            }

            GrupoDePlan g;
            if (!_indicePlanes.TryGetValue(codigo, out g))
            {
                g = new GrupoDePlan { Codigo = codigo };
                _indicePlanes[codigo] = g;
                _gruposDePlan.Add(g);
            }

            g.Filas.Add(f);
        }

        private void ChequearPlanExistente(FilaDeTrabajo f, PlanExistente p)
        {
            // Regla 6: el código de plan es único en todo el banco, así que si existe tiene que ser del cliente de esta fila.
            if (f.ClienteExistente != null && f.ClienteExistente.Id != p.ClienteId)
            {
                f.Error(Encabezados.CodigoPlan + ": el plan " + p.Codigo + " ya existe y es de " + NombreDelCliente(p.ClienteId) + ", no de este cliente. Usá otro código o el cliente correcto.");
            }
            else if (f.EsClienteNuevo)
            {
                f.Error(Encabezados.CodigoPlan + ": el plan " + p.Codigo + " ya existe y es de " + NombreDelCliente(p.ClienteId) + ", y esta fila crea un cliente nuevo. Usá otro código para el plan nuevo.");
            }

            if (f.Formato.Usable && f.Formato.Valor != p.Formato)
            {
                f.Error(Encabezados.TipoFormato + ": el plan " + p.Codigo + " está grabado con formato " + TextoDeFormato(p.Formato) + ", no " + TextoDeFormato(f.Formato.Valor)
                        + ". La carga no modifica planes que ya existen: usá el formato grabado o dejá la celda vacía.");
            }

            if (f.Moneda.Usable && f.Moneda.Valor != p.Moneda)
            {
                f.Error(Encabezados.Moneda + ": el plan " + p.Codigo + " está grabado en " + TextoDeMoneda(p.Moneda) + ", no en " + TextoDeMoneda(f.Moneda.Valor)
                        + ". La carga no modifica planes que ya existen: usá la moneda grabada o dejá la celda vacía.");
            }

            if (!p.Activo)
                f.Advertencia(Encabezados.CodigoPlan + ": el plan " + p.Codigo + " está inactivo. Lo que se le cargue no va a estar vigente hasta que se lo active.");
        }

        // Reglas 7 y 15 sobre un plan que crea este archivo.
        private void ValidarPlanNuevo(GrupoDePlan g)
        {
            Contradicciones(g.Filas, f => f.ClaveDeCliente, StringComparer.Ordinal,
                otras => Encabezados.CodigoPlan + ": el plan " + g.Codigo + " aparece con otro cliente en " + MotivosDeCarga.Filas(otras)
                         + ". Un código de plan es único en todo el banco: usá un código distinto para cada cliente.");

            var conflictoDeFormato = Contradicciones(g.Filas, f => f.Formato.Usable ? f.Formato.Valor.ToString() : null, StringComparer.Ordinal,
                otras => Encabezados.TipoFormato + ": el plan " + g.Codigo + " aparece con otro tipo de formato en " + MotivosDeCarga.Filas(otras) + ". Usá el mismo en todas.");
            if (!conflictoDeFormato && !g.Filas.Any(f => f.Formato.Presente))
                ErrorEnTodas(g, Encabezados.TipoFormato + ": hace falta para crear el plan " + g.Codigo + ". Completalo (06, 10 u 11).");

            var conflictoDeMoneda = Contradicciones(g.Filas, f => f.Moneda.Usable ? f.Moneda.Valor.ToString() : null, StringComparer.Ordinal,
                otras => Encabezados.Moneda + ": el plan " + g.Codigo + " aparece con otra moneda en " + MotivosDeCarga.Filas(otras) + ". Usá la misma en todas.");
            if (!conflictoDeMoneda && !g.Filas.Any(f => f.Moneda.Presente))
                ErrorEnTodas(g, Encabezados.Moneda + ": hace falta para crear el plan " + g.Codigo + ". Completala (COR o USD).");
        }

        private string NombreDelCliente(Guid clienteId)
        {
            // La foto trae los clientes de las claves del archivo; el dueño de un plan existente puede no estar. Se dice igual, sin nombrarlo.
            ClienteExistente cliente;
            return _clientesPorId.TryGetValue(clienteId, out cliente) ? cliente.RazonSocial : "otro cliente";
        }

        private static string TextoDeFormato(TipoDeFormatoDelPlan formato)
        {
            switch (formato)
            {
                case TipoDeFormatoDelPlan._06: return "06";
                case TipoDeFormatoDelPlan._10: return "10";
                default: return "11";
            }
        }

        private static string TextoDeMoneda(Moneda moneda) => moneda == Moneda.COR ? "COR" : "USD";

        // ================================================================================================ correo (reglas 9 a 11)

        private void ResolverCorreo(FilaDeTrabajo f)
        {
            if (!f.Correo.Usable) return;

            var correo = f.Correo.Valor;
            AutorizadoExistente existente;
            if (_autorizadosPorCorreo.TryGetValue(correo, out existente))
            {
                f.AutorizadoExistente = existente;
                ChequearCorreoExistente(f, existente);
                return;
            }

            GrupoDeCorreo g;
            if (!_indiceCorreos.TryGetValue(correo, out g))
            {
                g = new GrupoDeCorreo { Correo = correo };
                _indiceCorreos[correo] = g;
                _gruposDeCorreo.Add(g);
            }

            g.Filas.Add(f);
        }

        private void ChequearCorreoExistente(FilaDeTrabajo f, AutorizadoExistente a)
        {
            if (f.Nombre.Usable && !Iguales(f.Nombre.Valor, a.NombreContacto))
            {
                f.Error(Encabezados.NombreContacto + ": el correo " + f.Correo.Valor + " ya existe con el nombre \"" + MotivosDeCarga.Mostrar(a.NombreContacto) + "\", no \"" + MotivosDeCarga.Mostrar(f.Nombre.Valor)
                        + "\". La carga no modifica correos que ya existen: usá el mismo nombre o dejá la celda vacía.");
            }

            if (!a.Activo)
                f.Advertencia(Encabezados.Correo + ": el correo " + f.Correo.Valor + " está inactivo. Lo que se autorice con él no va a estar vigente hasta que se lo active.");
        }

        private void ValidarCorreoNuevo(GrupoDeCorreo g)
        {
            var conflicto = Contradicciones(g.Filas, f => f.Nombre.Usable ? f.Nombre.Valor : null, StringComparer.OrdinalIgnoreCase,
                otras => Encabezados.NombreContacto + ": el correo " + g.Correo + " aparece con otro nombre de contacto en " + MotivosDeCarga.Filas(otras) + ". Usá el mismo en todas.");

            // Regla 11: para crear el correo hace falta el nombre, en alguna de sus filas.
            if (!conflicto && !g.Filas.Any(f => f.Nombre.Presente))
                ErrorEnTodas(g, Encabezados.NombreContacto + ": hace falta para crear el correo " + g.Correo + ". Completalo en alguna de las filas de ese correo.");
        }

        // ================================================================================================ autorización (reglas 12 a 14)

        private void ResolverAutorizacion(FilaDeTrabajo f)
        {
            if (!f.Correo.Usable || f.ClaveDeCliente == null) return;

            if (f.AutorizadoExistente != null && f.ClienteExistente != null)
            {
                AutorizacionClienteExistente existente;
                if (_autorizacionesPorPar.TryGetValue(Par(f.AutorizadoExistente.Id, f.ClienteExistente.Id), out existente))
                {
                    ChequearAutorizacionExistente(f, existente);
                    return;
                }
            }

            var clave = (f.AutorizadoExistente != null ? "E:" + f.AutorizadoExistente.Id.ToString("N") : "N:" + f.Correo.Valor) + "|" + f.ClaveDeCliente;
            GrupoDeAutorizacion g;
            if (!_indiceAutorizaciones.TryGetValue(clave, out g))
            {
                g = new GrupoDeAutorizacion { Correo = f.Correo.Valor };
                _indiceAutorizaciones[clave] = g;
                _gruposDeAutorizacion.Add(g);
            }

            g.Filas.Add(f);
        }

        private void ChequearAutorizacionExistente(FilaDeTrabajo f, AutorizacionClienteExistente au)
        {
            var sobre = f.Correo.Valor + " sobre el cliente " + f.ClienteExistente.CifBac;

            // Regla 13: una autorización inactiva no se reactiva desde la carga.
            if (!au.Activo)
            {
                f.Error(Encabezados.Correo + ": la autorización de " + sobre + " ya existe pero está inactiva. No se reactiva desde la carga: usá el botón Activar/Desactivar.");
                return;
            }

            // Regla 12: ya existía; si el archivo trae otra fecha o comentario, la carga no los pisa.
            if (f.Fecha.Usable && (au.FechaDocumento == null || au.FechaDocumento.Value.Date != f.Fecha.Valor))
            {
                var grabada = au.FechaDocumento == null ? "sin fecha" : "fecha " + LectorDeFilaDeCarga.Dia(au.FechaDocumento.Value);
                f.Error(Encabezados.FechaDocumento + ": la autorización de " + sobre + " ya existe con " + grabada + ", no con " + LectorDeFilaDeCarga.Dia(f.Fecha.Valor)
                        + ". La carga no modifica autorizaciones que ya existen: usá la fecha grabada o dejá la celda vacía.");
            }

            if (f.Comentarios.Usable && !Iguales(f.Comentarios.Valor, au.Comentarios))
            {
                f.Error(Encabezados.Comentarios + ": la autorización de " + sobre + " ya existe con otro comentario (\"" + MotivosDeCarga.Mostrar(au.Comentarios) + "\"). La carga no modifica autorizaciones que ya existen: usá el comentario grabado o dejá la celda vacía.");
            }
        }

        private void ValidarAutorizacionNueva(GrupoDeAutorizacion g)
        {
            var sobre = g.Correo + " sobre " + DescribirCliente(g.Filas[0]);

            Contradicciones(g.Filas, f => f.Fecha.Usable ? f.Fecha.Valor.ToString("yyyyMMdd", CultureInfo.InvariantCulture) : null, StringComparer.Ordinal,
                otras => Encabezados.FechaDocumento + ": la autorización de " + sobre + " aparece con otra fecha en " + MotivosDeCarga.Filas(otras) + ". Usá la misma en todas.");

            Contradicciones(g.Filas, f => f.Comentarios.Usable ? f.Comentarios.Valor : null, StringComparer.OrdinalIgnoreCase,
                otras => Encabezados.Comentarios + ": la autorización de " + sobre + " aparece con otro comentario en " + MotivosDeCarga.Filas(otras) + ". Usá el mismo en todas.");
        }

        private static string DescribirCliente(FilaDeTrabajo f)
            => f.ClienteExistente != null ? "el cliente " + f.ClienteExistente.CifBac : f.GrupoDeCliente.Nombre;

        // ================================================================================================ el resultado

        private PlanDeCarga Armar()
        {
            var tieneErrores = _filas.Any(f => f.TieneErrores);
            var clientes = new List<ClienteACrear>();
            var planes = new List<PlanACrear>();
            var autorizados = new List<AutorizadoACrear>();
            var autorizaciones = new List<AutorizacionACrear>();

            // Todo o nada (DN-04, decisión 5): con un solo error, no se crea nada y nadie dice "Creado".
            if (!tieneErrores)
            {
                // El orden de las listas es el de escritura (clientes, planes, correos, autorizaciones); dentro de cada una, por fila de origen.
                foreach (var g in _gruposDeCliente.OrderBy(x => x.FilaDeOrigen.Numero))
                {
                    var origen = g.FilaDeOrigen;
                    origen.Creados.Add("cliente");
                    clientes.Add(new ClienteACrear(g.CifBac, g.CifCom, g.PrimerValor(f => f.Razon), g.DuenoId, origen.Numero));
                }

                foreach (var g in _gruposDePlan.OrderBy(x => x.FilaDeOrigen.Numero))
                {
                    var origen = g.FilaDeOrigen;
                    origen.Creados.Add("plan");
                    planes.Add(new PlanACrear(g.Codigo, g.PrimerValor(f => f.Formato), g.PrimerValor(f => f.Moneda), ReferenciaAlCliente(g.Filas[0]), origen.Numero));
                }

                foreach (var g in _gruposDeCorreo.OrderBy(x => x.FilaDeOrigen.Numero))
                {
                    var origen = g.FilaDeOrigen;
                    origen.Creados.Add("correo");
                    autorizados.Add(new AutorizadoACrear(g.Correo, g.PrimerValor(f => f.Nombre), origen.Numero));
                }

                foreach (var g in _gruposDeAutorizacion.OrderBy(x => x.FilaDeOrigen.Numero))
                {
                    var origen = g.FilaDeOrigen;
                    origen.Creados.Add("autorización");
                    var fecha = g.Filas.Any(f => f.Fecha.Usable) ? g.PrimerValor(f => f.Fecha) : (DateTime?)null;
                    autorizaciones.Add(new AutorizacionACrear(ReferenciaAlAutorizado(g.Filas[0]), ReferenciaAlCliente(g.Filas[0]), fecha, g.PrimerValor(f => f.Comentarios), origen.Numero));
                }
            }

            var filas = _filas.Select(f => new ResultadoDeFilaDeCarga(f.Numero, f.Errores, f.Advertencias, f.Creados, tieneErrores)).ToList();
            return new PlanDeCarga(filas, tieneErrores, clientes, planes, autorizados, autorizaciones);
        }

        private static ReferenciaACliente ReferenciaAlCliente(FilaDeTrabajo f)
            => f.ClienteExistente != null
                ? new ReferenciaACliente(f.ClienteExistente.Id, null)
                : new ReferenciaACliente(null, f.GrupoDeCliente.CifBac);

        private static ReferenciaAAutorizado ReferenciaAlAutorizado(FilaDeTrabajo f)
            => f.AutorizadoExistente != null
                ? new ReferenciaAAutorizado(f.AutorizadoExistente.Id, null)
                : new ReferenciaAAutorizado(null, f.Correo.Valor);

        // ================================================================================================ piezas comunes

        private static void ErrorEnTodas(GrupoDeCarga g, string motivo)
        {
            foreach (var f in g.Filas) f.Error(motivo);
        }

        /// <summary>
        /// Regla 15: la misma clave tiene que tener los mismos datos en todas sus filas, comparando solo las celdas que vienen
        /// llenas. Si hay dos o más valores distintos, TODAS las filas que traen alguno llevan el error, nombrando a las que traen otro.
        /// <paramref name="clave"/> devuelve nulo para una fila que no aporta valor (celda vacía o ya inválida: el lector ya lo dijo).
        /// </summary>
        private static bool Contradicciones(IList<FilaDeTrabajo> filas, Func<FilaDeTrabajo, string> clave, StringComparer comparador, Func<List<int>, string> mensaje)
        {
            var cubos = new Dictionary<string, List<FilaDeTrabajo>>(comparador);
            var orden = new List<List<FilaDeTrabajo>>();
            foreach (var f in filas)
            {
                var valor = clave(f);
                if (valor == null) continue;

                List<FilaDeTrabajo> cubo;
                if (!cubos.TryGetValue(valor, out cubo))
                {
                    cubo = new List<FilaDeTrabajo>();
                    cubos[valor] = cubo;
                    orden.Add(cubo);
                }

                cubo.Add(f);
            }

            if (orden.Count < 2) return false;

            foreach (var cubo in orden)
            {
                var otras = new List<int>();
                foreach (var otro in orden)
                {
                    if (ReferenceEquals(otro, cubo)) continue;
                    foreach (var f in otro) otras.Add(f.Numero);
                }

                var texto = mensaje(otras);
                foreach (var f in cubo) f.Error(texto);
            }

            return true;
        }

        private static List<string> Distintos(IEnumerable<FilaDeTrabajo> filas, Func<FilaDeTrabajo, Campo<string>> campo)
        {
            var vistos = new HashSet<string>(StringComparer.Ordinal);
            var lista = new List<string>();
            foreach (var f in filas)
            {
                var c = campo(f);
                if (c.Usable && vistos.Add(c.Valor)) lista.Add(c.Valor);
            }

            return lista;
        }

        private static bool Difieren(Campo<string> a, Campo<string> b) => a.Usable && b.Usable && !string.Equals(a.Valor, b.Valor, StringComparison.Ordinal);

        /// <summary>Comparación de texto de negocio: sin distinguir mayúsculas y sin espacios a los lados; una tilde distinta SÍ es distinta.</summary>
        private static bool Iguales(string a, string b)
            => string.Equals((a ?? string.Empty).Trim(), (b ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);

        private static string Par(Guid autorizadoId, Guid clienteId) => autorizadoId.ToString("N") + clienteId.ToString("N");

        private static string NodoBac(string cifBac) => "B:" + cifBac;

        private static string NodoCom(string cifCom) => "C:" + cifCom;

        private string Raiz(string nodo)
        {
            if (!_padre.ContainsKey(nodo))
            {
                _padre[nodo] = nodo;
                return nodo;
            }

            var raiz = nodo;
            while (_padre[raiz] != raiz) raiz = _padre[raiz];

            while (_padre[nodo] != raiz)
            {
                var siguiente = _padre[nodo];
                _padre[nodo] = raiz;
                nodo = siguiente;
            }

            return raiz;
        }

        private void Unir(string a, string b)
        {
            var raizA = Raiz(a);
            var raizB = Raiz(b);
            if (raizA != raizB) _padre[raizB] = raizA;
        }
    }
}
