using System;
using System.Collections.Generic;
using Sanic.Mppp.Plugins.Dominio;

namespace Sanic.Mppp.Plugins.Catalogos
{
    /// <summary>Lo que se pudo leer de una celda: si trae algo, si lo que trae es válido (si no, el error ya se dijo) y el valor ya normalizado.</summary>
    internal sealed class Campo<T>
    {
        public bool Presente { get; set; }

        public bool Valido { get; set; }

        public T Valor { get; set; }

        /// <summary>Trae algo y está bien formado: es lo único que se compara, se agrupa o se crea.</summary>
        public bool Usable => Presente && Valido;
    }

    /// <summary>Filas que nombran una misma cosa (un cliente nuevo, un plan nuevo, un correo nuevo, una autorización nueva).</summary>
    internal class GrupoDeCarga
    {
        public List<FilaDeTrabajo> Filas { get; } = new List<FilaDeTrabajo>();

        /// <summary>La fila de menor número de Excel: es la que dice "Creado" (DN-04), sin importar el orden en que llegaron.</summary>
        public FilaDeTrabajo FilaDeOrigen
        {
            get
            {
                var origen = Filas[0];
                foreach (var fila in Filas)
                {
                    if (fila.Numero < origen.Numero) origen = fila;
                }

                return origen;
            }
        }

        /// <summary>Valor de una columna para la cosa que se crea: el de la fila de menor número que lo trae. Valor por defecto si ninguna lo trae.</summary>
        public T PrimerValor<T>(Func<FilaDeTrabajo, Campo<T>> campo)
        {
            FilaDeTrabajo elegida = null;
            foreach (var fila in Filas)
            {
                if (!campo(fila).Usable) continue;
                if (elegida == null || fila.Numero < elegida.Numero) elegida = fila;
            }

            return elegida == null ? default(T) : campo(elegida).Valor;
        }
    }

    internal sealed class GrupoDeCliente : GrupoDeCarga
    {
        public string CifBac { get; set; }

        public string CifCom { get; set; }

        public Guid DuenoId { get; set; }

        /// <summary>"el cliente 000000555": cómo se nombra en un mensaje, por el CIF que tenga.</summary>
        public string Nombre => "el cliente " + (CifBac ?? CifCom);
    }

    internal sealed class GrupoDePlan : GrupoDeCarga
    {
        public string Codigo { get; set; }
    }

    internal sealed class GrupoDeCorreo : GrupoDeCarga
    {
        public string Correo { get; set; }
    }

    internal sealed class GrupoDeAutorizacion : GrupoDeCarga
    {
        public string Correo { get; set; }
    }

    /// <summary>Una fila ya leída (columna por columna, con los errores de formato dichos) y lo que se fue resolviendo de ella.</summary>
    internal sealed class FilaDeTrabajo
    {
        public FilaDeTrabajo(int posicion, int numero)
        {
            Posicion = posicion;
            Numero = numero;
        }

        /// <summary>Lugar en que llegó: el resultado sale en este orden.</summary>
        public int Posicion { get; }

        public int Numero { get; }

        public List<string> Errores { get; } = new List<string>();

        public List<string> Advertencias { get; } = new List<string>();

        public List<string> Creados { get; } = new List<string>();

        public Campo<string> CifBac { get; set; } = new Campo<string>();
        public Campo<string> CifCom { get; set; } = new Campo<string>();
        public Campo<string> Razon { get; set; } = new Campo<string>();
        public Campo<string> CorreoEjecutivo { get; set; } = new Campo<string>();
        public Campo<string> CodigoPlan { get; set; } = new Campo<string>();
        public Campo<TipoDeFormatoDelPlan> Formato { get; set; } = new Campo<TipoDeFormatoDelPlan>();
        public Campo<Moneda> Moneda { get; set; } = new Campo<Moneda>();
        public Campo<string> Correo { get; set; } = new Campo<string>();
        public Campo<string> Nombre { get; set; } = new Campo<string>();
        public Campo<DateTime> Fecha { get; set; } = new Campo<DateTime>();
        public Campo<string> Comentarios { get; set; } = new Campo<string>();

        // Lo que resolvió el planificador.
        public ClienteExistente ClienteExistente { get; set; }

        public GrupoDeCliente GrupoDeCliente { get; set; }

        public bool EsClienteNuevo { get; set; }

        /// <summary>Identifica al cliente de la fila para comparar entre filas: "E:&lt;id&gt;" si ya existe, "N:&lt;grupo&gt;" si lo crea este archivo; nulo si no se pudo resolver.</summary>
        public string ClaveDeCliente { get; set; }

        public AutorizadoExistente AutorizadoExistente { get; set; }

        // Los mensajes iguales no se repiten dentro de una fila (por ejemplo, el CIF BAC y el CIF COM encuentran al mismo cliente inactivo).
        public void Error(string motivo)
        {
            if (!Errores.Contains(motivo)) Errores.Add(motivo);
        }

        public void Advertencia(string motivo)
        {
            if (!Advertencias.Contains(motivo)) Advertencias.Add(motivo);
        }

        public bool TieneErrores => Errores.Count > 0;
    }
}
