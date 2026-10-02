// Caso DN-04: los menús "Carga masiva" y "Activar/Desactivar masivo" de las grillas de catálogos, de punta a punta.
// Prueba de aceptación del JS (contrato en diseno/DIRECTRICES-NUEVAS.md, DN-04 §"Contrato del JS"). Ninguno de estos
// casos ESCRIBE en Dataverse: suben un archivo que se rechaza o que no se graba (todo o nada). La carga con escrituras
// reales es D-73 (diseno/PENDIENTES.md).
//
//   MPPP_USUARIO=admin_planes npx playwright test dn-04   → menús presentes, plantillas, rechazo y error de fila
//   MPPP_USUARIO=ejecutivo    npx playwright test dn-04   → los menús NO aparecen (regla esAdministradorDePlanes)

import path from "node:path";
import { fileURLToPath } from "node:url";
import { mkdirSync } from "node:fs";
import { test, expect } from "@playwright/test";
import { usuarioActivo, verificarSesionExiste } from "../ayudantes/sesion.mjs";
import { abrirApp, irASitemap, esperarGrilla } from "../ayudantes/navegacion.mjs";
import { localizarBoton } from "../ayudantes/ribbon.mjs";
import { esperarDialogo } from "../ayudantes/dialogos.mjs";
import { leerXlsx } from "../ayudantes/xlsx.mjs";
import { SITEMAP } from "../config.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const CARPETA_DESCARGAS = path.resolve(__dirname, "..", "descargas");
const DATOS = path.resolve(__dirname, "..", "datos", "dn04");

// Etiquetas en los dos idiomas del proyecto: no se puede asumir el idioma de la sesión (ayudantes/ribbon.mjs).
const MENU_CARGA = ["Carga masiva", "Bulk load"];
const MENU_ESTADO = ["Activar/Desactivar masivo", "Bulk activate/deactivate"];
const OPCION_CARGAR = ["Cargar archivo", "Upload file"];
const OPCION_PLANTILLA = ["Descargar plantilla", "Download template"];

const ENCABEZADOS_CARGA = ["CIF BAC", "CIF COM", "Razon social", "Correo ejecutivo", "Codigo plan", "Tipo formato",
  "Moneda", "Correo", "Nombre contacto", "Fecha documento", "Comentarios", "Resultado"];
const ENCABEZADOS_ESTADO = ["Tipo", "Accion", "CIF BAC", "CIF COM", "Codigo plan", "Correo", "Resultado"];

// Mismo nombre de área que usa A-02 (casos/a-02-comandos-genericos-ocultos.spec.mjs).
const AREA_CATALOGOS = "Catalogos"; // sin tilde, como la expone la plataforma (selectores.mjs, TABLAS_A02)

const usuario = usuarioActivo();
test.use({ storageState: verificarSesionExiste(usuario) });

async function abrirClientes(page) {
  await abrirApp(page);
  await irASitemap(page, SITEMAP.clientes, AREA_CATALOGOS); // "Clientes" vive en el área Catálogos; la app abre en Trabajo
  await esperarGrilla(page, { permitirVacia: true });
}

/** Abre un menú desplegable y devuelve el locator de una de sus opciones (ya visible). */
async function opcionDelMenu(page, menu, opcion) {
  const ancla = await localizarBoton(page, menu, { raiz: page.getByRole("main") });
  expect(ancla, `El menú '${menu[0]}' está AUSENTE de la barra de Clientes.`).not.toBeNull();
  await ancla.locator.click();
  const item = await localizarBoton(page, opcion);
  expect(item, `El menú '${menu[0]}' se abrió pero no trae la opción '${opcion[0]}'.`).not.toBeNull();
  return item.locator;
}

async function textoDelDialogo(page) {
  const dialogo = await esperarDialogo(page, { timeout: 150_000 }); // la Custom API puede tardar hasta 2 minutos
  return { dialogo, texto: (await dialogo.textContent()) || "" };
}

test.describe("DN-04 — menús de catálogos (Administrador de planes)", () => {
  test.skip(usuario !== "admin_planes", "solo con la sesión del Administrador de planes");
  test.beforeEach(() => mkdirSync(CARPETA_DESCARGAS, { recursive: true }));

  for (const [menu, archivo, encabezados] of [
    [MENU_CARGA, "CargaCatalogos.xlsx", ENCABEZADOS_CARGA],
    [MENU_ESTADO, "ActivarDesactivar.xlsx", ENCABEZADOS_ESTADO],
  ]) {
    test(`'${menu[0]}' → 'Descargar plantilla' baja la plantilla vacía con sus encabezados`, async ({ page }) => {
      await abrirClientes(page);
      const plantilla = await opcionDelMenu(page, menu, OPCION_PLANTILLA);
      const [descarga] = await Promise.all([page.waitForEvent("download", { timeout: 60_000 }), plantilla.click()]);
      expect(descarga.suggestedFilename()).toBe(archivo);
      const ruta = path.join(CARPETA_DESCARGAS, `dn04-${archivo}`);
      await descarga.saveAs(ruta);
      const { encabezados: leidos, filas } = await leerXlsx(ruta);
      expect(leidos).toEqual(encabezados);
      expect(filas).toEqual([]);
    });
  }

  test("'Cargar archivo' con un error de fila descarga el resultado y avisa que no se grabó nada", async ({ page }) => {
    await abrirClientes(page);
    const cargar = await opcionDelMenu(page, MENU_CARGA, OPCION_CARGAR);
    const [selector] = await Promise.all([page.waitForEvent("filechooser", { timeout: 15_000 }), cargar.click()]);
    const [descarga] = await Promise.all([
      page.waitForEvent("download", { timeout: 150_000 }),
      selector.setFiles(path.join(DATOS, "dn04-carga-con-error.xlsx")),
    ]);
    expect(descarga.suggestedFilename()).toBe("dn04-carga-con-error-resultado.xlsx");
    const ruta = path.join(CARPETA_DESCARGAS, descarga.suggestedFilename());
    await descarga.saveAs(ruta);
    const { filas } = await leerXlsx(ruta);
    expect(filas.length).toBe(1);
    expect(filas[0][11], "La columna Resultado (L) de la fila con error").toMatch(/^Error: /);

    const { texto } = await textoDelDialogo(page);
    expect(texto).toMatch(/No se grabó nada|Nothing was saved/);
  });

  test("'Cargar archivo' con la hoja equivocada muestra MPPP-CAR-004 y no descarga nada", async ({ page }) => {
    await abrirClientes(page);
    let huboDescarga = false;
    page.on("download", () => { huboDescarga = true; });
    const cargar = await opcionDelMenu(page, MENU_CARGA, OPCION_CARGAR);
    const [selector] = await Promise.all([page.waitForEvent("filechooser", { timeout: 15_000 }), cargar.click()]);
    await selector.setFiles(path.join(DATOS, "dn04-carga-hoja-equivocada.xlsx"));
    const { texto } = await textoDelDialogo(page);
    expect(texto).toContain("MPPP-CAR-004");
    expect(huboDescarga).toBe(false);
  });
});

test.describe("DN-04 — menús de catálogos (Ejecutivo)", () => {
  test.skip(usuario !== "ejecutivo", "solo con la sesión del Ejecutivo");

  test("un Ejecutivo no ve los menús de carga ni de activación masiva", async ({ page }) => {
    await abrirClientes(page);
    expect(await localizarBoton(page, MENU_CARGA, { raiz: page.getByRole("main") })).toBeNull();
    expect(await localizarBoton(page, MENU_ESTADO, { raiz: page.getByRole("main") })).toBeNull();
  });
});
