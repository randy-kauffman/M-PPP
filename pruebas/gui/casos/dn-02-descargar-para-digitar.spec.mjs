// Caso DN-02: "Descargar para digitar" de punta a punta.
//
// 1. Abrir "Mis clientes - por digitar", seleccionar filas.
// 2. Verificar que el boton "Download for entry" / "Descargar para digitar"
//    esta PRESENTE y HABILITADO (no alcanza con que exista en el servidor:
//    ese fue justo el defecto que motivo este arnes).
// 3. Apretarlo y CAPTURAR la descarga real (Playwright puede: el boton usa
//    `Xrm.Navigation.openFile` con `openMode: 2`, que en el navegador es una
//    descarga de verdad — ver recursos/js/comandos.js).
// 4. Verificar que el archivo es un .xlsx valido, con el nombre esperado,
//    los encabezados esperados (parametro `exportacion.columnas`) y tantas
//    filas de datos como filas se seleccionaron.

import path from "node:path";
import { fileURLToPath } from "node:url";
import { mkdirSync } from "node:fs";
import { test, expect } from "@playwright/test";
import { usuarioActivo, verificarSesionExiste } from "../ayudantes/sesion.mjs";
import { abrirApp, irASitemap, esperarGrilla, seleccionarFilas } from "../ayudantes/navegacion.mjs";
import { verificarBoton, formatearDiagnostico } from "../ayudantes/ribbon.mjs";
import { leerXlsx } from "../ayudantes/xlsx.mjs";
import {
  botonPorId,
  COLUMNAS_EXPORTACION_ESPERADAS,
  PATRON_NOMBRE_ARCHIVO_EXPORTACION,
} from "../selectores.mjs";
import { SITEMAP } from "../config.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const CARPETA_DESCARGAS = path.resolve(__dirname, "..", "descargas");

const CANTIDAD_FILAS_A_PROBAR = 3; // igual a los escenarios C-01 de `pruebas/00-preparacion.md`

const usuario = usuarioActivo();
test.use({ storageState: verificarSesionExiste(usuario) });

test.describe("DN-02 — Descargar para digitar", () => {
  test("el boton descarga un .xlsx valido con las filas seleccionadas", async ({ page }) => {
    mkdirSync(CARPETA_DESCARGAS, { recursive: true });

    await abrirApp(page);
    await irASitemap(page, SITEMAP.porDigitar);
    await esperarGrilla(page);
    await seleccionarFilas(page, CANTIDAD_FILAS_A_PROBAR);

    const boton = botonPorId("descargarparadigitar");
    const diagnostico = await verificarBoton(page, boton); // sin EnableRule de estado: siempre esperado con selección
    console.log(formatearDiagnostico(diagnostico));

    expect(diagnostico.presente, "El boton 'Descargar para digitar' esta AUSENTE de la barra.").toBe(true);
    expect(
      diagnostico.habilitado,
      "El boton 'Descargar para digitar' esta deshabilitado con filas seleccionadas."
    ).toBe(true);

    // Se clickea el locator YA resuelto por verificarBoton (rol "menuitem",
    // no "button" — ver la nota en ayudantes/ribbon.mjs), en vez de volver a
    // buscarlo por texto: evita repetir el mismo error de rol dos veces.
    const [descarga] = await Promise.all([
      page.waitForEvent("download", { timeout: 30_000 }),
      diagnostico.locator.click(),
    ]);

    const nombreSugerido = descarga.suggestedFilename();
    expect(
      nombreSugerido,
      `El nombre del archivo descargado ('${nombreSugerido}') no coincide con el patron esperado ` +
        `'MPPP-para-digitar-YYYYMMDD-HHMMSS.xlsx' (recursos/js/comandos.js, nombreDeArchivoExportacion).`
    ).toMatch(PATRON_NOMBRE_ARCHIVO_EXPORTACION);

    const rutaGuardada = path.join(CARPETA_DESCARGAS, nombreSugerido);
    await descarga.saveAs(rutaGuardada);

    const { encabezados, filas } = await leerXlsx(rutaGuardada);

    expect(
      encabezados,
      "Los encabezados del .xlsx no coinciden con el parametro 'exportacion.columnas' " +
        "(playbooks/datos/parametros.md). Si el parametro cambio en el entorno, actualizar " +
        "COLUMNAS_EXPORTACION_ESPERADAS en selectores.mjs."
    ).toEqual(COLUMNAS_EXPORTACION_ESPERADAS);

    expect(
      filas.length,
      `Se seleccionaron ${CANTIDAD_FILAS_A_PROBAR} fila(s) pero el .xlsx trae ${filas.length}.`
    ).toBe(CANTIDAD_FILAS_A_PROBAR);
  });
});
