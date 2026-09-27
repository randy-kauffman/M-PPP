// Lectura minima del .xlsx descargado, para verificar que DN-02 bajo un
// archivo valido con las columnas y filas esperadas (no solo "algo se bajo").

import ExcelJS from "exceljs";

/**
 * Lee la primera hoja de un .xlsx y devuelve encabezados + filas de datos
 * como arreglos de strings (celdas vacias -> "").
 *
 * Si el archivo no es un .xlsx valido, ExcelJS tira, y ESO es la señal
 * correcta de "no bajo un Excel valido" (no se atrapa el error aca: que
 * la prueba lo reporte tal cual).
 */
export async function leerXlsx(rutaArchivo) {
  const libro = new ExcelJS.Workbook();
  await libro.xlsx.readFile(rutaArchivo);

  const hoja = libro.worksheets[0];
  if (!hoja) {
    throw new Error("El .xlsx no tiene ninguna hoja.");
  }

  const filas = [];
  hoja.eachRow({ includeEmpty: false }, (fila) => {
    const valores = [];
    // ExcelJS indexa celdas desde 1 y `fila.values[0]` es undefined.
    fila.eachCell({ includeEmpty: true }, (celda) => {
      valores.push(celda.value == null ? "" : String(celda.value));
    });
    filas.push(valores);
  });

  const [encabezados, ...datos] = filas;
  return { encabezados: encabezados ?? [], filas: datos, hoja };
}
