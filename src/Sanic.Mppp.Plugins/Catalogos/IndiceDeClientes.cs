using System;
using System.Collections.Generic;

namespace Sanic.Mppp.Plugins.Catalogos
{
    /// <summary>
    /// Lo que dio buscar el cliente de una fila por sus dos CIF: lo encontró (<see cref="Cliente"/>), los CIF se contradicen entre sí
    /// o con lo grabado (<see cref="Motivo"/>), o no existe (las dos propiedades en nulo).
    /// </summary>
    internal sealed class BusquedaDeCliente
    {
        public ClienteExistente Cliente { get; set; }

        public string Motivo { get; set; }
    }

    /// <summary>
    /// Los clientes de la foto, indexados por CIF BAC, CIF COM e id. La regla 2 de DN-04 (Excel 1) y la del Excel 2 son la misma:
    /// los dos CIF tienen que decir lo mismo. Una sola implementación, para que la carga y el cambio de estado no se separen.
    /// </summary>
    internal sealed class IndiceDeClientes
    {
        private readonly Dictionary<string, ClienteExistente> _porBac = new Dictionary<string, ClienteExistente>(StringComparer.Ordinal);
        private readonly Dictionary<string, ClienteExistente> _porCom = new Dictionary<string, ClienteExistente>(StringComparer.Ordinal);
        private readonly Dictionary<Guid, ClienteExistente> _porId = new Dictionary<Guid, ClienteExistente>();

        internal IndiceDeClientes(IEnumerable<ClienteExistente> clientes)
        {
            foreach (var c in clientes)
            {
                _porId[c.Id] = c;
                if (!string.IsNullOrWhiteSpace(c.CifBac)) _porBac[c.CifBac] = c;
                if (!string.IsNullOrWhiteSpace(c.CifCom)) _porCom[c.CifCom] = c;
            }
        }

        internal ClienteExistente PorId(Guid id)
        {
            ClienteExistente cliente;
            return _porId.TryGetValue(id, out cliente) ? cliente : null;
        }

        /// <summary>
        /// Precondición: la fila trae al menos un CIF y los que trae están bien formados (si no, el lector ya dijo el error).
        /// </summary>
        internal BusquedaDeCliente Buscar(FilaDeTrabajo f)
        {
            ClienteExistente porBac = null, porCom = null;
            if (f.CifBac.Usable) _porBac.TryGetValue(f.CifBac.Valor, out porBac);
            if (f.CifCom.Usable) _porCom.TryGetValue(f.CifCom.Valor, out porCom);

            if (!(f.CifBac.Usable && f.CifCom.Usable))
                return new BusquedaDeCliente { Cliente = porBac ?? porCom };

            // Los dos CIF tienen que decir lo mismo. Dos clientes distintos, o uno que existe y otro que no, es un error.
            if (porBac != null && porCom != null && porBac.Id != porCom.Id)
                return Contradiccion("apuntan a dos clientes distintos (" + porBac.RazonSocial + " y " + porCom.RazonSocial + "). Dejá uno solo o corregí el que está mal.");

            if (porBac != null && porCom == null)
                return Contradiccion("el CIF BAC " + f.CifBac.Valor + " es del cliente " + porBac.RazonSocial + ", pero el CIF COM " + f.CifCom.Valor + " no está grabado. Dejá uno solo o corregí el que está mal.");

            if (porBac == null && porCom != null)
                return Contradiccion("el CIF COM " + f.CifCom.Valor + " es del cliente " + porCom.RazonSocial + ", pero el CIF BAC " + f.CifBac.Valor + " no está grabado. Dejá uno solo o corregí el que está mal.");

            return new BusquedaDeCliente { Cliente = porBac };
        }

        private static BusquedaDeCliente Contradiccion(string motivo)
            => new BusquedaDeCliente { Motivo = Encabezados.Cliente + ": " + motivo };
    }
}
