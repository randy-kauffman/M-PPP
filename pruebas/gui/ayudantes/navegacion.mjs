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

// CONFIRMADO EN VIVO (2026-09-28, exploración puntual con `ariaSnapshot()`): el
// NOMBRE ACCESIBLE que arma la plataforma para entradas de sitemap y áreas pierde
// los acentos ("Configuración" -> "Configuracion", "Para atención manual" -> "Para
// atencion manual", el área "Catálogos" -> "Catalogos"), aunque el texto VISIBLE sí
// los tenga. `selectores.mjs`/`config.mjs` guardan las etiquetas con su acento
// correcto (para que un humano las lea bien en el código y en los reportes); acá se
// le sacan los acentos ANTES de buscar por rol/nombre accesible, para no fallar por
// una diferencia que no tiene nada que ver con si la entrada existe o no.
function quitarAcentos(texto) {
  return texto.normalize("NFD").replace(/[̀-ͯ]/g, "");
}

function escaparRegex(texto) {
  return texto.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function patronSinAcentos(etiqueta) {
  return new RegExp(escaparRegex(quitarAcentos(etiqueta)), "i");
}

/**
 * Cambia de ÁREA del sitemap (Trabajo/Consulta/Catálogos/Configuración —
 * `diseno/05-app-model-driven.md` §2: NO son un solo árbol plano, son áreas
 * separadas con su propio selector "Change area"/"Cambiar de área"). CONFIRMADO
 * EN VIVO (2026-09-28): el disparador es `button` con nombre que contiene "change
 * area", y abre un `menu` con un `menuitemradio` por área. Si ya se está en esa
 * área (`[checked]`), no hace nada.
 */
export async function cambiarArea(page, nombreArea) {
  const disparador = page.getByRole("button", { name: /change area|cambiar de.rea/i }).first();
  await disparador.waitFor({ state: "visible", timeout: 15_000 });
  const yaActiva = await page
    .getByRole("menuitemradio", { name: patronSinAcentos(nombreArea), checked: true })
    .count();
  if (yaActiva > 0) return; // ya estábamos en esa área, el menú ni se abrió
  await disparador.click();
  const opcion = page.getByRole("menuitemradio", { name: patronSinAcentos(nombreArea) }).first();
  await opcion.waitFor({ state: "visible", timeout: 10_000 });
  await opcion.click();
}

/**
 * Navega a una entrada del sitemap por su etiqueta visible (ver
 * `config.mjs` -> `SITEMAP`). Si `area` se pasa (y no es "Trabajo", el área por
 * defecto de todas las sesiones), cambia de área primero (`cambiarArea`). Intenta
 * primero por rol semantico (recomendado por Microsoft) y cae a `[title="..."]`
 * como respaldo, que es el selector que la propia guia de Microsoft usa para el
 * sitemap cuando el rol no alcanza — con los acentos sacados en los dos caminos
 * (ver `quitarAcentos`).
 */
export async function irASitemap(page, etiqueta, area) {
  const patron = patronSinAcentos(etiqueta);
  const porRol = page.getByRole("treeitem", { name: patron });

  // CARRERA OBSERVADA (2026-10-01, DN-04): la app abre en "Por digitar"; para un usuario sin permiso sobre Fila esa
  // página pinta un error MIENTRAS se cambia de área, y el clic en el área se perdía (la captura quedaba en
  // "Trabajo"). `count()` no espera, así que la entrada se buscaba antes de que el área cambiara. Ahora se ESPERA la
  // entrada, y si no aparece se vuelve a cambiar de área una vez.
  for (let intento = 1; intento <= (area ? 2 : 1); intento++) {
    if (area) await cambiarArea(page, area);
    try {
      await porRol.first().waitFor({ state: "visible", timeout: 15_000 });
      await porRol.first().click();
      return;
    } catch {
      // sin treeitem visible: otro intento de área, o los respaldos de abajo
    }
  }

  const porLink = page.getByRole("link", { name: patron });
  if ((await porLink.count()) > 0) {
    await porLink.first().click();
    return;
  }

  // Respaldo por `title`, sin acentos tampoco: como es un atributo CSS exacto
  // (no un matcher de Playwright), se prueban las dos formas del texto (con y
  // sin acento) en vez de una regex.
  const candidatos = [etiqueta, quitarAcentos(etiqueta)];
  for (const candidato of candidatos) {
    const porTitulo = page.locator(`[role="presentation"][title="${candidato}"]`).first();
    if ((await porTitulo.count()) > 0) {
      await porTitulo.waitFor({ state: "visible", timeout: 15_000 });
      await porTitulo.click();
      return;
    }
  }
  throw new Error(
    `No se encontró la entrada de sitemap '${etiqueta}' (probado con y sin acentos, por treeitem, ` +
      `link y [title]).${area ? ` Área: '${area}'.` : ""} Puede que el área no sea la correcta, o que ` +
      `la etiqueta real haya cambiado — revisar con 'ariaSnapshot()' antes de asumir que la entrada no existe.`
  );
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

/** Texto visible de una columna de la grilla principal, por su `col-id` (el nombre
 * lógico de la columna, p.ej. `sanic_estado`). NO CONFIRMADO EN VIVO todavía (mismo
 * caso que `[role="grid"]` en `esperarGrilla`: hay que verificarlo contra la app
 * real la primera vez que se corra este caso) — si `col-id` no es el atributo real,
 * el locator no encuentra nada y `textContent()` tira, con el `row-index`/columna
 * pedidos en el mensaje, nunca un string vacío que parezca "no cambió nada". */
export async function leerTextoColumna(page, indiceFila, columna) {
  const celda = page.locator(`[row-index="${indiceFila}"] [col-id="${columna}"]`).first();
  await celda.waitFor({ state: "visible", timeout: 10_000 });
  const texto = await celda.textContent();
  if (texto === null) {
    throw new Error(
      `No se pudo leer la columna '${columna}' de la fila ${indiceFila} ([col-id="${columna}"] no ` +
        `encontrado o sin texto). Puede que el atributo real no sea 'col-id' en esta grilla — revisar ` +
        `con el inspector de Playwright antes de asumir que la columna cambió o no cambió.`
    );
  }
  return texto.trim();
}

/**
 * Abre un registro DIRECTO por su GUID (deep-link `pagetype=entityrecord`), en vez
 * de navegar por sitemap+grilla+click. A diferencia de `irASitemap`/`abrirApp` (que
 * a propósito NO usan deep-link a una VISTA, config.mjs: el id de una vista no es
 * un dato estable que se haya verificado), acá el GUID SÍ es estable: es el de un
 * registro que esta misma corrida sembró (`ayudantes/datos-gui.mjs::fixtureSembrado`),
 * no un id de metadata supuesto. Navegar así es además más confiable que abrir una
 * vista compartida (p.ej. "Solicitudes", sin filtro por dueño) y hacer click en "la
 * primera fila": con varios agentes tocando Dev en paralelo esa vista puede tener
 * filas de cualquiera, y "la primera" no es necesariamente la propia.
 */
export async function abrirRegistro(page, etn, id) {
  const url = new URL(urlApp());
  url.searchParams.set("pagetype", "entityrecord");
  url.searchParams.set("etn", etn);
  url.searchParams.set("id", id);
  await page.goto(url.toString());
  await page.getByRole("navigation").first().waitFor({ state: "visible", timeout: 30_000 });
}

/**
 * Ubica un subgrid DENTRO de un formulario ya abierto (`abrirRegistro`), por la
 * etiqueta de su pestaña/sección (p.ej. "Filas", "Reglas", "Bitácora" —
 * `diseno/05-app-model-driven.md` línea 165).
 *
 * CONFIRMADO EN VIVO (2026-09-28, corrida real de A-01/A-02): el formulario de
 * Solicitud NO tiene una pestaña por sección — todas las secciones ("Correo",
 * "Filas", "Reglas", "Comunicaciones", "Revision", "Bitacora") viven apiladas
 * dentro de la única pestaña "General"; por eso `pestaña` casi nunca encuentra
 * nada y el `if` de abajo no hace nada — comportamiento correcto, no un bug (se
 * deja el intento por si algún formulario SÍ tuviera pestañas separadas). Y, mismo
 * hallazgo que `irASitemap`: la región pierde los acentos en su nombre accesible
 * (`region "Bitacora"`, confirmado con `ariaSnapshot()`, no "Bitácora") — se
 * busca sin acentos con el mismo helper.
 */
export async function irASubgrid(page, etiquetaPestaña, etiquetaSubgrid) {
  const pestaña = page.getByRole("tab", { name: etiquetaPestaña, exact: false });
  if ((await pestaña.count()) > 0) {
    await pestaña.first().click();
  }
  const region = page.getByRole("region", { name: patronSinAcentos(etiquetaSubgrid) }).first();
  await region.waitFor({ state: "visible", timeout: 15_000 });
  return region;
}

/** Selecciona la primera fila DENTRO de un subgrid ya localizado (`irASubgrid`). */
export async function seleccionarFilaEnSubgrid(region) {
  const fila = region.locator('[row-index="0"]').first();
  await fila.waitFor({ state: "visible", timeout: 15_000 });
  const casillero = fila.getByRole("checkbox").first();
  await casillero.click({ force: true });
}
