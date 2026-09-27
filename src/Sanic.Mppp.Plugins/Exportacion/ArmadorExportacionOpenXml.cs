using System;
using System.Collections.Generic;

namespace Sanic.Mppp.Plugins.Exportacion
{
    /// <summary>
    /// Arma el .xlsx con Open XML SDK 3.1.0 (DN-02: "el Excel se arma en C# con Open XML SDK, que ya se usa y ya se
    /// midió en C-05" — la misma librería que <see cref="Plantilla.LectorOpenXml"/> usa para leer; escribir es la otra
    /// mitad de la misma librería, no una dependencia nueva).
    ///
    /// Todo valor se escribe como TEXTO, nunca como número: numeroCuenta y numeroIdentificacion no pueden perder ceros a
    /// la izquierda ni precisión por una celda que Excel decida interpretar como número (decisión de este dominio, a
    /// confirmar con el aprobador junto con la forma del JSON).
    /// </summary>
    public sealed class ArmadorExportacionOpenXml : IArmadorExportacion
    {
        /// <summary>
        /// Nombre de la única hoja del archivo generado. Es una decisión de este código, no del parámetro: DN-02 solo
        /// deja el ORDEN de columnas en manos del negocio, no el nombre de la hoja.
        /// </summary>
        public const string NombreDeHoja = "Filas";

        public byte[] Armar(IList<FilaParaExportar> filas, ConfiguracionExportacion configuracion)
        {
            throw new NotImplementedException();
        }
    }
}
