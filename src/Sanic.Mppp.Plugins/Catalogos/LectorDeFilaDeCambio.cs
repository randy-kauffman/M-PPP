using System;
using System.Collections.Generic;
using Sanic.Mppp.Plugins.Plantilla;
using Sanic.Mppp.Plugins.Steps;

namespace Sanic.Mppp.Plugins.Catalogos
{
    /// <summary>Una fila del Excel 2 ya leída: tipo y acción, las celdas de clave (en <see cref="F"/>) y lo que se fue resolviendo de ella.</summary>
    internal sealed class FilaDeCambio
    {
        public FilaDeCambio(FilaDeTrabajo fila) { F = fila; }

        /// <summary>Errores, advertencias y las celdas de clave ya normalizadas (CIF BAC, CIF COM, código de plan y correo).</summary>
        public FilaDeTrabajo F { get; }

        public TipoDeCatalogo? Tipo { get; set; }

        public AccionDeEstado? Accion { get; set; }

        // Lo que resolvió el planificador contra la foto.
        public ClienteExistente Cliente { get; set; }
        public AutorizadoExistente Autorizado { get; set; }

        /// <summary>Identifica el registro al que apunta la fila, igual si se lo nombró por CIF BAC o por CIF COM: "C:&lt;id&gt;", "P:", "A:", "U:". Nulo si no se resolvió.</summary>
        public string Clave { get; set; }

        public Guid Id { get; set; }

        public bool ActivoActual { get; set; }

        /// <summary>"el cliente 000000123", "el plan 0A12"…: cómo se nombra el registro en un motivo.</summary>
        public string Descripcion { get; set; }

        /// <summary>Lo que dice la columna Resultado de esta fila cuando no tiene errores (sin las advertencias).</summary>
        public string Resultado { get; set; }

        public bool Cambia { get; set; }
    }

    /// <summary>Los encabezados propios del Excel 2 (los de clave son los mismos que los del Excel 1).</summary>
    internal static class EncabezadosDeCambio
    {
        internal static readonly string Tipo = ColumnasDeCambioDeEstado.EncabezadoDe(ColumnasDeCambioDeEstado.Tipo);
        internal static readonly string Accion = ColumnasDeCambioDeEstado.EncabezadoDe(ColumnasDeCambioDeEstado.Accion);
    }

    /// <summary>Lee UNA fila del Excel 2: tipo, acción y las claves que ese tipo exige, y deja dicho cada error de formato (DN-04, Excel 2, reglas 1 y 2).</summary>
    internal static class LectorDeFilaDeCambio
    {
        private const string ListaDeTipos = "CLIENTE, PLAN, CORREO o AUTORIZACION_CLIENTE";
        private const string ListaDeAcciones = "ACTIVAR o DESACTIVAR";

        internal static FilaDeCambio Leer(FilaPlantilla fila, int posicion)
        {
            var f = new FilaDeTrabajo(posicion, fila.NumeroFilaExcel);
            var fc = new FilaDeCambio(f);
            var valores = fila.Valores;

            fc.Tipo = LeerTipo(f, Celda(valores, ColumnasDeCambioDeEstado.Tipo));
            fc.Accion = LeerAccion(f, Celda(valores, ColumnasDeCambioDeEstado.Accion));

            var bac = Celda(valores, ColumnasDeCambioDeEstado.CifBac);
            var com = Celda(valores, ColumnasDeCambioDeEstado.CifCom);
            var plan = Celda(valores, ColumnasDeCambioDeEstado.CodigoPlan);
            var correo = Celda(valores, ColumnasDeCambioDeEstado.Correo);

            // Sin tipo conocido no se sabe qué claves sobran ni cuáles faltan: se lee el formato de todas las que vienen.
            bool usaCliente = true, usaPlan = true, usaCorreo = true;
            if (fc.Tipo.HasValue)
            {
                usaCliente = fc.Tipo.Value == TipoDeCatalogo.Cliente || fc.Tipo.Value == TipoDeCatalogo.AutorizacionCliente;
                usaPlan = fc.Tipo.Value == TipoDeCatalogo.Plan;
                usaCorreo = fc.Tipo.Value == TipoDeCatalogo.Correo || fc.Tipo.Value == TipoDeCatalogo.AutorizacionCliente;

                var nombre = NombreDe(fc.Tipo.Value);
                // Regla 2: cada tipo exige sus claves y ninguna otra. Una celda que no se usa es una intención que no se cumple.
                if (usaCliente && bac == null && com == null)
                    f.Error(Encabezados.Cliente + ": una fila de tipo " + nombre + " necesita el CIF BAC, el CIF COM o los dos. Completalos.");
                if (!usaCliente && bac != null) f.Error(Sobrante(Encabezados.CifBac, nombre));
                if (!usaCliente && com != null) f.Error(Sobrante(Encabezados.CifCom, nombre));
                if (usaPlan && plan == null) f.Error(Encabezados.CodigoPlan + ": una fila de tipo " + nombre + " necesita el código del plan. Completalo.");
                if (!usaPlan && plan != null) f.Error(Sobrante(Encabezados.CodigoPlan, nombre));
                if (usaCorreo && correo == null) f.Error(Encabezados.Correo + ": una fila de tipo " + nombre + " necesita el correo. Completalo.");
                if (!usaCorreo && correo != null) f.Error(Sobrante(Encabezados.Correo, nombre));
            }

            LeerClaves(f, bac, com, plan, correo, usaCliente, usaPlan, usaCorreo);
            return fc;
        }

        /// <summary>Solo las celdas de clave, todas, sin mirar el tipo: lo que necesita <see cref="ClavesDeCambioDeEstado"/>.</summary>
        internal static FilaDeTrabajo LeerCeldas(FilaPlantilla fila, int posicion)
        {
            var f = new FilaDeTrabajo(posicion, fila.NumeroFilaExcel);
            var valores = fila.Valores;
            LeerClaves(f, Celda(valores, ColumnasDeCambioDeEstado.CifBac), Celda(valores, ColumnasDeCambioDeEstado.CifCom),
                Celda(valores, ColumnasDeCambioDeEstado.CodigoPlan), Celda(valores, ColumnasDeCambioDeEstado.Correo), true, true, true);
            return f;
        }

        // Una celda que el tipo no usa no se lee: ya lleva su error de sobrante, y otro de formato sería ruido.
        private static void LeerClaves(FilaDeTrabajo f, string bac, string com, string plan, string correo, bool usaCliente, bool usaPlan, bool usaCorreo)
        {
            if (usaCliente)
            {
                f.CifBac = LectorDeFilaDeCarga.Normalizar(f, Encabezados.CifBac, bac, Normalizacion.CifBac);
                f.CifCom = LectorDeFilaDeCarga.Normalizar(f, Encabezados.CifCom, com, Normalizacion.CifCom);
            }

            if (usaPlan) f.CodigoPlan = LectorDeFilaDeCarga.Normalizar(f, Encabezados.CodigoPlan, plan, Normalizacion.CodigoDePlan);
            if (usaCorreo) f.Correo = LectorDeFilaDeCarga.LeerCorreo(f, Encabezados.Correo, correo);
        }

        private static string Celda(IDictionary<string, string> valores, string campo)
            => LectorDeFilaDeCarga.CeldaEnLetra(valores, ColumnasDeCambioDeEstado.LetraDe(campo));

        private static string Sobrante(string encabezado, string tipo)
            => encabezado + ": una fila de tipo " + tipo + " no usa esta celda. Borrala: una celda que no se usa es una intención que no se cumple.";

        private static TipoDeCatalogo? LeerTipo(FilaDeTrabajo f, string crudo)
        {
            if (crudo == null)
            {
                f.Error(EncabezadosDeCambio.Tipo + ": la celda está vacía. Usá " + ListaDeTipos + ".");
                return null;
            }

            foreach (var tipo in new[] { TipoDeCatalogo.Cliente, TipoDeCatalogo.Plan, TipoDeCatalogo.Correo, TipoDeCatalogo.AutorizacionCliente })
            {
                if (string.Equals(crudo, NombreDe(tipo), StringComparison.OrdinalIgnoreCase)) return tipo;
            }

            f.Error(EncabezadosDeCambio.Tipo + ": \"" + MotivosDeCarga.Mostrar(crudo) + "\" no es un tipo válido. Usá " + ListaDeTipos + ". Corregilo.");
            return null;
        }

        private static AccionDeEstado? LeerAccion(FilaDeTrabajo f, string crudo)
        {
            if (crudo == null)
            {
                f.Error(EncabezadosDeCambio.Accion + ": la celda está vacía. Usá " + ListaDeAcciones + ".");
                return null;
            }

            if (string.Equals(crudo, "ACTIVAR", StringComparison.OrdinalIgnoreCase)) return AccionDeEstado.Activar;
            if (string.Equals(crudo, "DESACTIVAR", StringComparison.OrdinalIgnoreCase)) return AccionDeEstado.Desactivar;

            f.Error(EncabezadosDeCambio.Accion + ": \"" + MotivosDeCarga.Mostrar(crudo) + "\" no es una acción válida. Usá " + ListaDeAcciones + ". Corregila.");
            return null;
        }

        internal static string NombreDe(TipoDeCatalogo tipo)
        {
            switch (tipo)
            {
                case TipoDeCatalogo.Cliente: return "CLIENTE";
                case TipoDeCatalogo.Plan: return "PLAN";
                case TipoDeCatalogo.Correo: return "CORREO";
                default: return "AUTORIZACION_CLIENTE";
            }
        }
    }
}
