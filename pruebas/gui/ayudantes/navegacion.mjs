// Ayudante de navegacion dentro de la MDA. Los patrones de selector (grid con
// `[row-index]`, sitemap por `title`, dialogos por `[role="dialog"]`) salen de
// la guia oficial de Microsoft "Test model-driven apps" (Microsoft Learn,
// power-platform/developer/playwright-samples/model-driven-application,
// consultada 2026-09-27) — no son una invencion, pero SI son genericos de
// Unified Interface: falta confirmar contra ESTA app en particular en cuanto
// exista una sesion real (mismo espiritu que selectores.mjs con `dataId`).

import { urlApp, SITEMAP } from "../config.mjs";

/** Abre la app y espera a que el shell (sitemap) este visible. */
export async function abrirApp(page) {
  await page.goto(urlApp());
  await esperarSitemap(page);
}

async function esperarSitemap(page) {
  // El sitemap se renderiza como una lista de navegacion; se espera por rol
  // en vez de por clase, para no atarse a nombres de CSS generados.
  await page.getByRole("navigation").first().waitFor({ state: "visible", timeout: 30_000 });
}

/**
 * Navega a una entrada del sitemap por su etiqueta visible (ver
 * `config.mjs` -> `SITEMAP`). Intenta primero por rol semantico
 * (recomendado por Microsoft) y cae a `[title="..."]` como respaldo, que es
 * el selector que la propia guia de Microsoft usa para el sitemap cuando el
 * rol no alcanza.
 */
export async function irASitemap(page, etiqueta) {
  const porRol = page.getByRole("treeitem", { name: etiqueta, exact: true });
  if ((await porRol.count()) > 0) {
    await porRol.first().click();
    return;
  }

  const porLink = page.getByRole("link", { name: etiqueta, exact: true });
  if ((await porLink.count()) > 0) {
    await porLink.first().click();
    return;
  }

  const porTitulo = page.locator(`[role="presentation"][title="${etiqueta}"]`).first();
  await porTitulo.waitFor({ state: "visible", timeout: 15_000 });
  await porTitulo.click();
}

// Mensaje de "vista vacia". Confirmado EN VIVO el 2026-09-27 contra esta app
// (en ingles, idioma de la sesion de prueba disponible): "We didn't find
// anything to show here". La variante en espanol es la traduccion esperada
// de la plataforma, NO confirmada todavia contra esta app — si no calza, el
// peor caso es caer en el branch de "timeout" de abajo, nunca un falso OK.
//
// Importante, tambien confirmado en vivo: `[role="grid"]` NO es un selector
// confiable aca (no aparecio ni con la grilla vacia ni con datos, viendolo
// con Playwright); `[row-index]` si es confiable (documentado por Microsoft
// y verificado). Por eso `esperarGrilla` corre las dos señales en carrera
// (datos vs. vacio) y NUNCA usa `[role="grid"]`.
const PATRON_VACIO = /didn.?t find anything|no encontramos nada|no se encontraron/i;

/**
 * Espera a que la grilla termine de cargar, y distingue "vista vacia" de
 * "todavia no cargo" — son dos causas de falla completamente distintas
 * (la primera es de datos/siembra, la segunda puede ser de la app misma).
 *
 * @param {object} opciones
 * @param {boolean} opciones.permitirVacia si es false (default) y la vista
 *   carga vacia, TIRA un error explicito en vez de dejar que el resto del
 *   caso falle con un timeout generico esperando `[row-index="0"]`.
 */
export async function esperarGrilla(page, { permitirVacia = false } = {}) {
  const primeraFila = page.locator('[row-index="0"]');
  const mensajeVacio = page.getByText(PATRON_VACIO).first();

  const resultado = await Promise.race([
    primeraFila.waitFor({ state: "visible", timeout: 20_000 }).then(() => "con-datos"),
    mensajeVacio.waitFor({ state: "visible", timeout: 20_000 }).then(() => "vacia"),
  ]).catch(() => "timeout");

  if (resultado === "vacia" && !permitirVacia) {
    throw new Error(
      'La vista quedó VACÍA ("We didn\'t find anything to show here" o equivalente). ' +
        "NO es un timeout de selector de boton: es la vista sin filas. Revisar la siembra " +
        "de datos (pruebas/gui/sembrar_datos_gui.py) antes de sospechar del ribbon."
    );
  }
  if (resultado === "timeout") {
    throw new Error(
      'Pasaron 20s sin que aparezca ni una fila ([row-index="0"]) ni el mensaje de vista ' +
        "vacía. Puede ser que la vista siga cargando, o que el texto de 'vacío' haya " +
        "cambiado de idioma/redacción (ver PATRON_VACIO en este archivo)."
    );
  }
  // "con-datos" y permitirVacia=true: se sigue de largo en cualquier caso.
}

/** Cuenta cuantas filas tiene la grilla actualmente cargada. */
export async function contarFilas(page) {
  return page.locator('[row-index]').count();
}

/**
 * Selecciona las primeras `cantidad` filas de la grilla marcando su
 * checkbox. Usa `force: true` porque el icono de check puede tapar el input
 * (troubleshooting documentado por Microsoft para grillas AG Grid en UCI).
 */
export async function seleccionarFilas(page, cantidad) {
  for (let i = 0; i < cantidad; i++) {
    const fila = page.locator(`[row-index="${i}"]`);
    await fila.waitFor({ state: "visible", timeout: 15_000 });
    const casillero = fila.getByRole("checkbox").first();
    await casillero.click({ force: true });
  }
}
