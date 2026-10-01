using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Sanic.Mppp.Plugins.Catalogos
{
    /// <summary>
    /// Una planificación del cambio de estado (DN-04, Excel 2, reglas 3 a 6): resuelve a qué registro existente apunta cada fila,
    /// junta las filas que piden lo mismo, decide qué estados cambian de verdad y calcula las advertencias de efecto sobre el
    /// estado FINAL. Una instancia por llamada. Lineal: todo se busca en diccionarios armados una vez.
    /// </summary>
    internal sealed class PlanificacionDeCambio
    {
        private readonly List<FilaDeCambio> _filas;

        private readonly IndiceDeClientes _clientes;
        private readonly Dictionary<string, PlanExistente> _planesPorCodigo = new Dictionary<string, PlanExistente>(StringComparer.Ordinal);
        private readonly Dictionary<string, AutorizadoExistente> _autorizadosPorCorreo = new Dictionary<string, AutorizadoExistente>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<Guid, AutorizadoExistente> _autorizadosPorId = new Dictionary<Guid, AutorizadoExistente>();
        private readonly Dictionary<string, AutorizacionClienteExistente> _autorizacionesPorPar = new Dictionary<string, AutorizacionClienteExistente>(StringComparer.Ordinal);

        // Autorizaciones activas (por su propio estado) de cada cliente y de cada correo: primero las de la foto, después con el archivo aplicado.
        private readonly Dictionary<Guid, int> _activasPorCliente = new Dictionary<Guid, int>();
        private readonly Dictionary<Guid, int> _activasPorAutorizado = new Dictionary<Guid, int>();

        private readonly Dictionary<string, Grupo> _indice = new Dictionary<string, Grupo>(StringComparer.Ordinal);
        private readonly List<Grupo> _grupos = new List<Grupo>();

        /// <summary>Las filas que piden algo sobre un mismo registro existente.</summary>
        private sealed class Grupo
        {
            public readonly List<FilaDeCambio> Filas = new List<FilaDeCambio>();
            public AccionDeEstado Accion;
            public FilaDeCambio Origen;

            public bool Consistente;
        }

        internal PlanificacionDeCambio(List<FilaDeCambio> filas, EstadoDeCatalogos estado)
        {
            _filas = filas;
            _clientes = new IndiceDeClientes(estado.Clientes);

            foreach (var p in estado.Planes)
            {
                if (!string.IsNullOrWhiteSpace(p.Codigo)) _planesPorCodigo[p.Codigo] = p;
            }

            foreach (var a in estado.Autorizados)
            {
                _autorizadosPorId[a.Id] = a;
                if (!string.IsNullOrWhiteSpace(a.Correo)) _autorizadosPorCorreo[a.Correo.Trim()] = a;
            }

            foreach (var au in estado.Autorizaciones)
            {
                _autorizacionesPorPar[Par(au.AutorizadoId, au.ClienteId)] = au;
                if (au.Activo)
                {
                    Sumar(_activasPorCliente, au.ClienteId, 1);
                    Sumar(_activasPorAutorizado, au.AutorizadoId, 1);
                }
            }
        }

        internal PlanDeCambioDeEstado Ejecutar()
        {
            foreach (var fc in _filas) Resolver(fc);
            foreach (var fc in _filas) Agrupar(fc);
            foreach (var g in _grupos) Decidir(g);
            AplicarAlRecuento();
            foreach (var g in _grupos) AdvertirEfectos(g);
            return Armar();
        }

        // ================================================================================================ resolver (reglas 3)

        private void Resolver(FilaDeCambio fc)
        {
            if (!fc.Tipo.HasValue) return;

            switch (fc.Tipo.Value)
            {
                case TipoDeCatalogo.Cliente:
                    ResolverCliente(fc);
                    break;
                case TipoDeCatalogo.Plan:
                    ResolverPlan(fc);
                    break;
                case TipoDeCatalogo.Correo:
                    ResolverCorreo(fc);
                    break;
                default:
                    ResolverAutorizacion(fc);
                    break;
            }
        }

        private void ResolverCliente(FilaDeCambio fc)
        {
            var c = BuscarCliente(fc.F);
            if (c == null) return;

            fc.Cliente = c;
            Fijar(fc, "C:" + c.Id.ToString("N"), c.Id, c.Activo, "el cliente " + c.CifBac);
        }

        private void ResolverPlan(FilaDeCambio fc)
        {
            if (!fc.F.CodigoPlan.Usable) return;

            PlanExistente p;
            if (!_planesPorCodigo.TryGetValue(fc.F.CodigoPlan.Valor, out p))
            {
                fc.F.Error(Encabezados.CodigoPlan + ": no existe ningún plan con el código " + fc.F.CodigoPlan.Valor + ". Revisá el código: esta carga no activa ni desactiva planes que no existen.");
                return;
            }

            Fijar(fc, "P:" + p.Id.ToString("N"), p.Id, p.Activo, "el plan " + p.Codigo);
        }

        private void ResolverCorreo(FilaDeCambio fc)
        {
            var a = BuscarAutorizado(fc.F);
            if (a == null) return;

            fc.Autorizado = a;
            Fijar(fc, "A:" + a.Id.ToString("N"), a.Id, a.Activo, "el correo " + MotivosDeCarga.Mostrar(a.Correo));
        }

        // Regla 3: en AUTORIZACION_CLIENTE la autorización (correo, cliente) tiene que existir como par; que el correo y el cliente
        // existan por separado no alcanza. Se buscan las dos cosas antes de mirar el par para reportar todos los errores de la fila.
        private void ResolverAutorizacion(FilaDeCambio fc)
        {
            var cliente = BuscarCliente(fc.F);
            var autorizado = BuscarAutorizado(fc.F);
            if (cliente == null || autorizado == null) return;

            AutorizacionClienteExistente au;
            if (!_autorizacionesPorPar.TryGetValue(Par(autorizado.Id, cliente.Id), out au))
            {
                fc.F.Error(Encabezados.Correo + ": no existe la autorización de " + MotivosDeCarga.Mostrar(autorizado.Correo) + " sobre el cliente " + cliente.CifBac
                           + ". Que el correo y el cliente existan por separado no alcanza: revisá el par.");
                return;
            }

            fc.Cliente = cliente;
            fc.Autorizado = autorizado;
            Fijar(fc, "U:" + au.Id.ToString("N"), au.Id, au.Activo, "la autorización de " + MotivosDeCarga.Mostrar(autorizado.Correo) + " sobre el cliente " + cliente.CifBac);
        }

        private ClienteExistente BuscarCliente(FilaDeTrabajo f)
        {
            // Si una clave vino mal escrita o no vino ninguna, el lector ya dijo el error: no se inventa uno más.
            if ((f.CifBac.Presente && !f.CifBac.Valido) || (f.CifCom.Presente && !f.CifCom.Valido)) return null;
            if (!f.CifBac.Presente && !f.CifCom.Presente) return null;

            var busqueda = _clientes.Buscar(f);
            if (busqueda.Motivo != null)
            {
                f.Error(busqueda.Motivo);
                return null;
            }

            if (busqueda.Cliente != null) return busqueda.Cliente;

            var nombrado = f.CifBac.Usable && f.CifCom.Usable
                ? "el CIF BAC " + f.CifBac.Valor + " ni el CIF COM " + f.CifCom.Valor
                : f.CifBac.Usable ? "el CIF BAC " + f.CifBac.Valor : "el CIF COM " + f.CifCom.Valor;
            f.Error(Encabezados.Cliente + ": no existe ningún cliente con " + nombrado + ". Revisá el número: esta carga no activa ni desactiva clientes que no existen.");
            return null;
        }

        private AutorizadoExistente BuscarAutorizado(FilaDeTrabajo f)
        {
            if (!f.Correo.Usable) return null;

            AutorizadoExistente a;
            if (_autorizadosPorCorreo.TryGetValue(f.Correo.Valor, out a)) return a;

            f.Error(Encabezados.Correo + ": no existe ningún correo autorizado " + MotivosDeCarga.Mostrar(f.Correo.Valor) + ". Revisá que esté bien escrito: esta carga no activa ni desactiva correos que no existen.");
            return null;
        }

        private static void Fijar(FilaDeCambio fc, string clave, Guid id, bool activo, string descripcion)
        {
            fc.Clave = clave;
            fc.Id = id;
            fc.ActivoActual = activo;
            fc.Descripcion = descripcion;
        }

        // ================================================================================================ agrupar y decidir (reglas 4 y 5)

        private void Agrupar(FilaDeCambio fc)
        {
            // Una fila con algún error no entra: el archivo no se graba, y sus problemas ya están dichos.
            if (fc.Clave == null || !fc.Accion.HasValue || fc.F.TieneErrores) return;

            Grupo g;
            if (!_indice.TryGetValue(fc.Clave, out g))
            {
                g = new Grupo();
                _indice[fc.Clave] = g;
                _grupos.Add(g);
            }

            g.Filas.Add(fc);
        }

        private static void Decidir(Grupo g)
        {
            var activar = g.Filas.Where(f => f.Accion == AccionDeEstado.Activar).ToList();
            var desactivar = g.Filas.Where(f => f.Accion == AccionDeEstado.Desactivar).ToList();

            // Regla 5: acciones opuestas sobre el mismo registro → error en las dos filas, nombrando a la otra.
            if (activar.Count > 0 && desactivar.Count > 0)
            {
                Opuestas(activar, desactivar, "ACTIVAR", "DESACTIVAR");
                Opuestas(desactivar, activar, "DESACTIVAR", "ACTIVAR");
                g.Consistente = false;
                return;
            }

            g.Consistente = true;
            g.Accion = g.Filas[0].Accion.Value;
            g.Origen = g.Filas[0];
            foreach (var fc in g.Filas)
            {
                if (fc.F.Numero < g.Origen.F.Numero) g.Origen = fc;
            }

            var pedido = g.Accion == AccionDeEstado.Activar;
            foreach (var fc in g.Filas)
            {
                if (!ReferenceEquals(fc, g.Origen))
                {
                    // Misma acción repetida: se aplica una vez (la fila de menor número) y la otra lleva advertencia.
                    fc.Resultado = "Sin cambios: lo pide una fila anterior";
                    fc.F.Advertencia(EncabezadosDeCambio.Accion + ": " + fc.Descripcion + " ya lo pide la fila " + g.Origen.F.Numero.ToString(CultureInfo.InvariantCulture) + " con la misma acción. Se aplica una sola vez.");
                }
            }

            if (g.Origen.ActivoActual == pedido)
            {
                g.Origen.Resultado = pedido ? "Sin cambios: ya estaba activo" : "Sin cambios: ya estaba inactivo";
            }
            else
            {
                g.Origen.Resultado = pedido ? "Activado" : "Desactivado";
                g.Origen.Cambia = true;
            }
        }

        private static void Opuestas(List<FilaDeCambio> estas, List<FilaDeCambio> otras, string lasQuePiden, string lasOtras)
        {
            var numeros = otras.Select(f => f.F.Numero).ToList();
            var texto = EncabezadosDeCambio.Accion + ": " + estas[0].Descripcion + " se pide " + lasQuePiden + " en esta fila y " + lasOtras + " en " + MotivosDeCarga.Filas(numeros) + ". Dejá una sola acción.";
            foreach (var fc in estas) fc.F.Error(texto);
        }

        // ================================================================================================ el estado final (regla 6)

        /// <summary>Las autorizaciones que el archivo activa o desactiva cambian cuántas quedan activas de cada cliente y de cada correo.</summary>
        private void AplicarAlRecuento()
        {
            foreach (var g in _grupos)
            {
                if (!g.Consistente || !g.Origen.Cambia || g.Origen.Tipo != TipoDeCatalogo.AutorizacionCliente) continue;

                var delta = g.Accion == AccionDeEstado.Activar ? 1 : -1;
                Sumar(_activasPorCliente, g.Origen.Cliente.Id, delta);
                Sumar(_activasPorAutorizado, g.Origen.Autorizado.Id, delta);
            }
        }

        /// <summary>Regla 6: se calcula sobre el estado final, con el archivo entero aplicado; y solo cuando la fila de verdad cambia el estado.</summary>
        private void AdvertirEfectos(Grupo g)
        {
            if (!g.Consistente || !g.Origen.Cambia) return;

            var fc = g.Origen;
            if (g.Accion == AccionDeEstado.Desactivar && fc.Tipo == TipoDeCatalogo.Cliente)
            {
                var n = Contar(_activasPorCliente, fc.Id);
                if (n > 0) fc.F.Advertencia(Encabezados.Cliente + ": desactivar " + fc.Descripcion + " deja sin efecto " + Autorizaciones(n) + ". Siguen activas, pero no valen mientras el cliente esté inactivo.");
            }
            else if (g.Accion == AccionDeEstado.Desactivar && fc.Tipo == TipoDeCatalogo.Correo)
            {
                var n = Contar(_activasPorAutorizado, fc.Id);
                if (n > 0) fc.F.Advertencia(Encabezados.Correo + ": desactivar " + fc.Descripcion + " deja sin efecto " + Autorizaciones(n) + ". Siguen activas, pero no valen mientras el correo esté inactivo.");
            }
            else if (g.Accion == AccionDeEstado.Activar && fc.Tipo == TipoDeCatalogo.AutorizacionCliente)
            {
                if (!ActivoAlFinal("C:" + fc.Cliente.Id.ToString("N"), fc.Cliente.Activo))
                    fc.F.Advertencia(Encabezados.Cliente + ": " + fc.Descripcion + " no va a estar vigente: el cliente " + fc.Cliente.CifBac + " está inactivo. Activalo también para que valga.");
                if (!ActivoAlFinal("A:" + fc.Autorizado.Id.ToString("N"), fc.Autorizado.Activo))
                    fc.F.Advertencia(Encabezados.Correo + ": " + fc.Descripcion + " no va a estar vigente: el correo " + MotivosDeCarga.Mostrar(fc.Autorizado.Correo) + " está inactivo. Activalo también para que valga.");
            }
        }

        /// <summary>Estado de un cliente o un correo con el archivo aplicado: lo que pide el archivo si lo nombra, y si no, lo que ya tenía.</summary>
        private bool ActivoAlFinal(string clave, bool activoEnLaFoto)
        {
            Grupo g;
            if (_indice.TryGetValue(clave, out g) && g.Consistente) return g.Accion == AccionDeEstado.Activar;
            return activoEnLaFoto;
        }

        private static string Autorizaciones(int n)
            => n == 1 ? "1 autorización por cliente activa" : n.ToString(CultureInfo.InvariantCulture) + " autorizaciones por cliente activas";

        private static int Contar(Dictionary<Guid, int> recuento, Guid id)
        {
            int n;
            return recuento.TryGetValue(id, out n) ? n : 0;
        }

        private static void Sumar(Dictionary<Guid, int> recuento, Guid id, int delta)
        {
            int n;
            recuento.TryGetValue(id, out n);
            recuento[id] = n + delta;
        }

        // ================================================================================================ el resultado

        private PlanDeCambioDeEstado Armar()
        {
            var tieneErrores = _filas.Any(fc => fc.F.TieneErrores);
            var cambios = new List<CambioDeEstadoACrear>();
            var sinCambios = 0;

            // Todo o nada (DN-04, decisión 5): con un solo error no se cambia nada y los contadores quedan en 0.
            if (!tieneErrores)
            {
                foreach (var g in _grupos.OrderBy(x => x.Origen.F.Numero))
                {
                    if (g.Origen.Cambia) cambios.Add(new CambioDeEstadoACrear(g.Origen.Tipo.Value, g.Origen.Id, g.Accion, g.Origen.F.Numero));
                }

                sinCambios = _filas.Count(fc => !fc.Cambia);
            }

            var filas = _filas.Select(fc => new ResultadoDeFilaDeCambio(fc.F.Numero, fc.F.Errores, fc.F.Advertencias, fc.Resultado ?? "Sin cambios", tieneErrores)).ToList();
            return new PlanDeCambioDeEstado(filas, tieneErrores, cambios, sinCambios);
        }

        private static string Par(Guid autorizadoId, Guid clienteId) => autorizadoId.ToString("N") + clienteId.ToString("N");
    }
}
