using System.Collections.Generic;

namespace Sanic.Mppp.Plugins.Exportacion
{
    /// <summary>
    /// Arma el .xlsx de exportación (DN-02) a partir de filas ya resueltas y la configuración de columnas. El orden de
    /// <see cref="ConfiguracionExportacion.Columnas"/> ES el orden de las columnas del archivo: columna A = la primera,
    /// B = la segunda, etc. Un campo que la configuración pide y una fila no trae como clave en
    /// <see cref="FilaParaExportar.Valores"/> es un error de configuración (<see cref="System.ArgumentException"/>),
    /// nunca una columna vacía en silencio.
    /// </summary>
    public interface IArmadorExportacion
    {
        byte[] Armar(IList<FilaParaExportar> filas, ConfiguracionExportacion configuracion);
    }
}
