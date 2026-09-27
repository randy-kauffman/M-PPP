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

/** Espera a que la grilla tenga al menos una fila renderizada (o quede vacia a proposito). */
export async function esperarGrilla(page, { permitirVacia = false } = {}) {
  const primeraFila = page.locator('[row-index="0"]');
  if (permitirVacia) {
    await page
      .locator('[role="grid"]')
      .first()
      .waitFor({ state: "visible", timeout: 15_000 });
    return;
  }
  await primeraFila.waitFor({ state: "visible", timeout: 15_000 });
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
