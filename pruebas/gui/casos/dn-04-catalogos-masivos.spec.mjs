// Caso DN-04: los menús "Carga masiva" y "Activar/Desactivar masivo" de las grillas de catálogos, de punta a punta.
// Prueba de aceptación del JS (contrato en diseno/DIRECTRICES-NUEVAS.md, DN-04 §"Contrato del JS"). Salvo L-09 y L-55,
// ninguno de estos casos ESCRIBE en Dataverse: suben un archivo que se rechaza o que no se graba (todo o nada).
// L-09 (carga y cambio de estado que GRABAN) y L-55 (Comentarios por el formulario) escriben, y por eso traen datos
// propios y limpieza propia (`datos_dn04_l09.py`, `beforeAll --sembrar` / `afterAll --limpiar`).
//
//   MPPP_USUARIO=admin_planes   npx playwright test dn-04   → L-01 a L-09 y L-55 (formulario): menús, texto fijo, plantillas,
//                                                              cancelar, rechazo, error y carga grabada
//   MPPP_USUARIO=ejecutivo      npx playwright test dn-04   → L-02: los menús NO aparecen en ninguna (esAdministradorDePlanes)
//   MPPP_USUARIO=supervisor     npx playwright test dn-04   → L-03: ídem
//   MPPP_USUARIO=admin_tecnico  npx playwright test dn-04   → L-03: ídem, y las grillas que su sitemap no tiene se SALTAN
//
// Las cargas de archivo corren solo en Clientes: las cuatro grillas llaman al mismo JS y a la misma Custom API.

import path from "node:path";
import { fileURLToPath } from "node:url";
import { mkdirSync } from "node:fs";
import { execFile } from "node:child_process";
import { promisify } from "node:util";
import { test, expect } from "@playwright/test";
import { usuarioActivo, verificarSesionExiste } from "../ayudantes/sesion.mjs";
import { abrirApp, irASitemap, esperarGrilla, abrirRegistro } from "../ayudantes/navegacion.mjs";
import { localizarBoton } from "../ayudantes/ribbon.mjs";
import { esperarDialogo, esperarQueSeCierre } from "../ayudantes/dialogos.mjs";
import { leerXlsx } from "../ayudantes/xlsx.mjs";
import { ENTORNO, SITEMAP } from "../config.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const CARPETA_DESCARGAS = path.resolve(__dirname, "..", "descargas");
const DATOS = path.resolve(__dirname, "..", "datos", "dn04");
const DATOS_L09 = path.resolve(__dirname, "..", "datos_dn04_l09.py"); // siembra y limpia lo que escriben L-09 y L-55
const execFileAsync = promisify(execFile);

// Etiquetas en los dos idiomas del proyecto: no se puede asumir el idioma de la sesión (ayudantes/ribbon.mjs).
const MENU_CARGA = ["Carga masiva", "Bulk load"];
const MENU_ESTADO = ["Activar/Desactivar masivo", "Bulk activate/deactivate"];
const OPCION_CARGAR = ["Cargar archivo", "Upload file"];
const OPCION_PLANTILLA = ["Descargar plantilla", "Download template"];

const ENCABEZADOS_CARGA = ["CIF BAC", "CIF COM", "Razon social", "Correo ejecutivo", "Codigo plan", "Tipo formato",
  "Moneda", "Correo", "Nombre contacto", "Fecha documento", "Comentarios", "Resultado"];
const ENCABEZADOS_ESTADO = ["Tipo", "Accion", "CIF BAC", "CIF COM", "Codigo plan", "Correo", "Resultado"];

// L-04: el texto FIJO de los menús (decisión del aprobador, DN-04: van en `LabelText`, no en `$LocLabels`). A diferencia
// de las constantes de arriba, estas NO son un patrón de dos idiomas: son la única forma correcta, en cualquier idioma.
const TEXTO_FIJO = {
  menuCarga: "Carga masiva",
  menuEstado: "Activar/Desactivar masivo",
  opcionCargar: "Cargar archivo",
  opcionPlantilla: "Descargar plantilla",
};

// Los textos del JS que estos casos comparan, COPIADOS de `TEXTOS` en recursos/js/comandos.js (1033 y 3082). El caso no
// puede importar el JS (es un recurso web que se carga en el navegador), así que si alguien cambia un texto allá, el caso
// falla acá: eso es lo que se quiere ("TEXTOS quedó desalineado", L-09). `{x}` son los valores que arma `armar()`.
const TEXTOS_JS = {
  1033: {
    cerrar: "OK",
    leyendo: "Reading the file\u2026",
    validando: "Validating and loading the file\u2026 it may take up to a minute",
    preparando: "Preparing the result\u2026",
    extensionInvalida: "The file must be an Excel workbook (.xlsx). Nothing was sent.",
    cargaGrabada: "Load finished: {clientes} clients, {planes} plans, {correos} emails and {autorizaciones} authorizations created.",
    estadoGrabado: "Status change finished: {activados} activated, {desactivados} deactivated and {sincambios} unchanged.",
    conAdvertencias: "{advertencias} rows with warnings: check the result file.",
  },
  3082: {
    cerrar: "Aceptar",
    leyendo: "Leyendo el archivo\u2026",
    validando: "Validando y cargando el archivo\u2026 puede tardar hasta un minuto",
    preparando: "Preparando el resultado\u2026",
    extensionInvalida: "El archivo tiene que ser un libro de Excel (.xlsx). No se envi\u00f3 nada.",
    cargaGrabada: "Carga terminada: {clientes} clientes, {planes} planes, {correos} correos y {autorizaciones} autorizaciones creados.",
    estadoGrabado: "Cambio de estado terminado: {activados} activados, {desactivados} desactivados y {sincambios} sin cambios.",
    conAdvertencias: "{advertencias} filas con advertencias: revis\u00e1 el archivo de resultado.",
  },
};

const armar = (plantilla, valores) =>
  Object.entries(valores).reduce((texto, [clave, valor]) => texto.split(`{${clave}}`).join(String(valor)), plantilla);

// Mismo nombre de área que usa A-02 (casos/a-02-comandos-genericos-ocultos.spec.mjs).
const AREA_CATALOGOS = "Catalogos"; // sin tilde, como la expone la plataforma (selectores.mjs, TABLAS_A02)

const usuario = usuarioActivo();
test.use({ storageState: verificarSesionExiste(usuario) });

// Las cuatro grillas con menús (playbooks/importacion/comandos.md, DN-04).
const GRILLAS = [SITEMAP.clientes, SITEMAP.planes, SITEMAP.autorizados, SITEMAP.autorizacionesClientes];

async function abrirGrilla(page, entrada) {
  await abrirApp(page);
  await irASitemap(page, entrada, AREA_CATALOGOS); // los catálogos viven en el área Catálogos; la app abre en Trabajo
  await esperarGrilla(page, { permitirVacia: true });
}

const abrirClientes = (page) => abrirGrilla(page, SITEMAP.clientes);

/** L-03: abre una grilla de catálogo SIN suponer que el rol puede verla, y distingue dos cosas que no se pueden mezclar:
 *    - la grilla NO EXISTE para el rol (el sitemap recortado no trae el área o la entrada, G-07): devuelve
 *      `{ existe: false, motivo }` con la evidencia (las áreas / entradas que SÍ ve), y el test se SALTA, nunca se da por verde;
 *    - cualquier otro problema (el sitemap no cargó, el área no cambió, la entrada está pero la grilla no abre): LANZA.
 *  La ausencia solo se declara con evidencia POSITIVA de que el sitemap se pintó: el menú de áreas con al menos un área,
 *  y, si el área existe, su árbol con la entrada buscada ausente entre otras que sí están. Un sitemap que no cargó no
 *  tiene áreas ni entradas, y eso es un error, no "la grilla no existe". Hecho a propósito sin `irASitemap`: ese tira
 *  un `Error` genérico para "no la encontré", y acá hace falta saber POR QUÉ no está. */
async function abrirGrillaDelRol(page, entrada) {
  await abrirApp(page);
  const cambiador = page.getByRole("button", { name: /change area|cambiar de.rea/i }).first();
  const radios = page.getByRole("menuitemradio");
  const opcionArea = page.getByRole("menuitemradio", { name: new RegExp(AREA_CATALOGOS, "i") });
  const encabezadoArea = page.getByRole("treeitem", { name: AREA_CATALOGOS, exact: true });

  // Dos intentos: la app abre en "Por digitar" y, para un rol sin permiso sobre Fila, esa página pinta un error mientras
  // se cambia de área y el clic en el área se perdía (la carrera que documenta `irASitemap`). Se repite UNA vez.
  for (let intento = 1; intento <= 2; intento++) {
    await cambiador.waitFor({ state: "visible", timeout: 15_000 });
    await cambiador.click();
    await radios.first().waitFor({ state: "visible", timeout: 10_000 }); // evidencia positiva: el menú de áreas se pintó
    if ((await opcionArea.count()) === 0) {
      const areas = (await radios.allTextContents()).map((a) => a.trim());
      await page.keyboard.press("Escape");
      return {
        existe: false,
        motivo: `El rol '${usuario}' no tiene el área '${AREA_CATALOGOS}' en el sitemap (áreas que sí ve: ${areas.join(", ")}): ` +
          `la grilla '${entrada}' no existe para él (recorte de sitemap, G-07), no hay menús que mirar.`,
      };
    }
    await opcionArea.first().click();
    if (await encabezadoArea.waitFor({ state: "visible", timeout: 15_000 }).then(() => true).catch(() => false)) break;
    if (intento === 2) {
      throw new Error(`El área '${AREA_CATALOGOS}' existe en el menú de áreas pero su árbol no apareció tras dos intentos.`);
    }
  }

  const laEntrada = page.getByRole("treeitem", { name: entrada, exact: true });
  if ((await laEntrada.count()) === 0) {
    const entradas = (await page.getByRole("tree").first().ariaSnapshot()).split("\n")
      .filter((l) => l.includes("treeitem")).map((l) => l.trim());
    return {
      existe: false,
      motivo: `El rol '${usuario}' ve el área '${AREA_CATALOGOS}' pero no la entrada '${entrada}' (entradas: ${entradas.join(" ")}): ` +
        "la grilla no existe para él (recorte de sitemap, G-07), no hay menús que mirar.",
    };
  }
  await laEntrada.first().click();
  await esperarGrilla(page, { permitirVacia: true });
  return { existe: true };
}

/** Abre un menú desplegable y devuelve el locator de una de sus opciones (ya visible). */
async function opcionDelMenu(page, menu, opcion, grilla = SITEMAP.clientes) {
  return (await abrirMenu(page, menu, opcion, grilla)).item;
}

/** Como `opcionDelMenu`, pero devuelve también el ancla (para poder cerrar el menú). */
async function abrirMenu(page, menu, opcion, grilla) {
  const ancla = await localizarBoton(page, menu, { raiz: page.getByRole("main") });
  expect(ancla, `El menú '${menu[0]}' está AUSENTE de la barra de ${grilla}.`).not.toBeNull();
  await ancla.locator.click();
  const item = await localizarBoton(page, opcion);
  expect(item, `En ${grilla}, el menú '${menu[0]}' se abrió pero no trae la opción '${opcion[0]}'.`).not.toBeNull();
  return { ancla: ancla.locator, item: item.locator };
}

/** Cierra el desplegable y ESPERA a que se cierre: mientras está abierto, su capa tapa la barra y el siguiente menú
 *  "no aparece" (falso AUSENTE observado 2026-10-01). `Escape` solo no alcanza: en una de tres corridas idénticas el
 *  menú siguió abierto tras tres `Escape` (el menú solo atiende `Escape` si el foco está adentro). Se escala: Escape,
 *  Escape con el foco en una opción, clic en el ancla (alterna el menú; con tope, por si la capa lo tapa). */
async function cerrarMenu(page, ancla, opcionVisible) {
  const cerrado = () => opcionVisible.waitFor({ state: "hidden", timeout: 3_000 }).then(() => true).catch(() => false);
  const formas = [
    ["Escape", () => page.keyboard.press("Escape")],
    ["Escape con foco en el menú", async () => { await opcionVisible.focus({ timeout: 3_000 }).catch(() => {}); await page.keyboard.press("Escape"); }],
    ["clic en el ancla", () => ancla.click({ timeout: 5_000 }).catch(() => {})],
  ];
  for (const [, cerrar] of formas) {
    await cerrar();
    if (await cerrado()) return;
  }
  throw new Error(`El menú desplegable no se cerró (probado: ${formas.map(([n]) => n).join(", ")}).`);
}

async function textoDelDialogo(page) {
  const dialogo = await esperarDialogo(page, { timeout: 150_000 }); // la Custom API puede tardar hasta 2 minutos
  return { dialogo, texto: (await dialogo.textContent()) || "" };
}

/** Idioma que muestra la sesión y los textos del JS en ese idioma. Es lo que lee el propio JS (`idioma()` en comandos.js):
 *  `userSettings.languageId`, con 1033 de respaldo para un idioma que el JS no conoce. Sin leerlo no se sabe contra qué
 *  texto comparar (Dev tiene solo 1033 instalado, pero el caso no lo supone). */
async function idiomaDeLaSesion(page) {
  const lcid = await page.evaluate(() => {
    try { return window.Xrm.Utility.getGlobalContext().userSettings.languageId; } catch { return null; }
  });
  expect(lcid, "No se pudo leer Xrm.Utility.getGlobalContext().userSettings.languageId de la página.").not.toBeNull();
  return { lcid, t: TEXTOS_JS[lcid] ?? TEXTOS_JS[1033] };
}

/** Abre el selector de archivo de "Cargar archivo" de un menú y devuelve el filechooser. Que el selector se abra es la
 *  evidencia POSITIVA de que el clic llegó al JS y abrió el `input` sincrónicamente (L-08: si no, se perdió el gesto). */
async function abrirSelector(page, menu) {
  const cargar = await opcionDelMenu(page, menu, OPCION_CARGAR);
  const [selector] = await Promise.all([page.waitForEvent("filechooser", { timeout: 15_000 }), cargar.click()]);
  return selector;
}

// Las dos Custom API del set. `[\d.]+` = la versión del Web API (v9.2); cualquier método: una llamada es una llamada.
const RUTA_API_CATALOGOS = /\/api\/data\/v[\d.]+\/(sanic_mppp_capi_(?:cargarcatalogos|cambiarestadocatalogos))/;

/** Graba, desde YA, toda petición de red a las dos Custom API (mismo mecanismo que `grabarEscriturasApi` de A-04, pero
 *  acotado a estas dos llamadas y sin filtrar por método). Como en A-04, un "cero peticiones" solo vale si el mismo
 *  mecanismo VE una cuando la hay: eso lo prueba L-09, donde la misma grabación tiene que ver exactamente una. */
function grabarLlamadasDeCatalogos(page) {
  const llamadas = [];
  const oyente = (request) => {
    const m = RUTA_API_CATALOGOS.exec(request.url());
    if (m) llamadas.push({ api: m[1], metodo: request.method() });
  };
  page.on("request", oyente);
  return { detener() { page.off("request", oyente); return llamadas; } };
}

/** Instala en la página DOS testigos del indicador de progreso, ANTES de elegir el archivo:
 *    - un MutationObserver que anota cuál de los tres mensajes llega a pintarse en el DOM y en qué orden (`enOrden`);
 *    - un espía sobre `Xrm.Utility.showProgressIndicator` que anota cada mensaje con que el JS LO PIDE (`mostrados`).
 *  Hacen falta los dos porque NO son lo mismo: la plataforma pinta el indicador de forma asíncrona y lo sustituye en el
 *  siguiente `show`, así que un mensaje que dura milisegundos ("Leyendo el archivo…" dura lo que el FileReader,
 *  "Preparando el resultado…" lo que tarda `abrirXlsx`) puede pedirse sin llegar nunca al DOM (medido en Dev el
 *  2026-10-02 con una carga de 180 ms: los tres `show` salieron en orden y el DOM no pintó ninguno). El espía es la
 *  evidencia de que el JS cumple su contrato; el DOM, de lo que el usuario puede llegar a ver. Con L-08 (cancelar, `.txt`)
 *  el espía es además el testigo fuerte de "no hay indicador": un indicador fugaz no se vería en el DOM. */
async function observarProgreso(page, t) {
  const mensajes = [t.leyendo, t.validando, t.preparando];
  await page.evaluate((mensajes) => {
    window.__progreso = [];
    window.__progresoPedido = [];
    const anotar = (texto) => {
      if (!texto) return;
      mensajes.forEach((m, i) => { if (texto.includes(m)) window.__progreso.push(i); });
    };
    if (window.__observadorProgreso) window.__observadorProgreso.disconnect();
    window.__observadorProgreso = new MutationObserver((cambios) => {
      for (const c of cambios) {
        if (c.type === "characterData") anotar(c.target.data);
        c.addedNodes.forEach((n) => anotar(n.textContent));
      }
    });
    window.__observadorProgreso.observe(document.body, { childList: true, subtree: true, characterData: true });
    const utilidades = window.Xrm.Utility;
    if (!utilidades.__showOriginal) utilidades.__showOriginal = utilidades.showProgressIndicator; // no envolver dos veces
    utilidades.showProgressIndicator = (mensaje) => {
      window.__progresoPedido.push(mensaje);
      return utilidades.__showOriginal.call(utilidades, mensaje);
    };
  }, mensajes);
  return {
    /** Índices (0 leyendo, 1 validando, 2 preparando) en el orden en que aparecieron por primera vez EN EL DOM. */
    async enOrden() {
      const crudo = await page.evaluate(() => window.__progreso);
      return [...new Set(crudo)];
    },
    /** Los mensajes que el JS pidió con `showProgressIndicator`, en orden, estén o no llegado a pintarse. */
    mostrados: () => page.evaluate(() => window.__progresoPedido),
  };
}

/** Cierra el aviso (`openAlertDialog`) con su botón y espera a que desaparezca. */
async function cerrarAviso(dialogo, t) {
  await dialogo.getByRole("button", { name: t.cerrar, exact: true }).click();
  await esperarQueSeCierre(dialogo);
}

/** Corre `datos_dn04_l09.py` (`--sembrar` o `--limpiar`) por subproceso, igual que el teardown del arnés con
 *  `sembrar_datos_gui.py`: la última línea de stdout es el JSON de resultado, y un `estado: "error"` hace fallar. */
async function correrDatosL09(modo) {
  let stdout;
  try {
    ({ stdout } = await execFileAsync("python3", [DATOS_L09, modo], { cwd: path.dirname(DATOS_L09), timeout: 120_000 }));
  } catch (error) {
    stdout = error.stdout || ""; // el script sale con 1 y deja el JSON de error en stdout
    if (!stdout.trim()) throw error;
  }
  const lineas = stdout.trim().split("\n");
  const resultado = JSON.parse(lineas[lineas.length - 1]);
  if (resultado.estado === "error") {
    throw new Error(`datos_dn04_l09.py ${modo} falló: ${JSON.stringify(resultado.detalle)}`);
  }
  console.log(`[dn04-l09] ${modo} -> ${resultado.estado}: ${JSON.stringify(resultado.detalle)}`);
  return resultado;
}

/** Lo que Dataverse tiene guardado, leído con la sesión del navegador (solo lectura; la misma que usa `usuarioDeLaSesion`). */
async function leerDeDataverse(page, ruta) {
  const respuesta = await page.request.get(new URL(`api/data/v9.2/${ruta}`, ENTORNO.url).toString(), {
    headers: { Accept: "application/json", "OData-MaxVersion": "4.0", "OData-Version": "4.0" },
  });
  expect(respuesta.ok(), `GET ${ruta} devolvió HTTP ${respuesta.status()}`).toBe(true);
  return respuesta.json();
}

test.describe("DN-04 — menús de catálogos (Administrador de planes)", () => {
  test.skip(usuario !== "admin_planes", "solo con la sesión del Administrador de planes");
  test.beforeEach(() => mkdirSync(CARPETA_DESCARGAS, { recursive: true }));

  for (const grilla of GRILLAS) {
    test(`${grilla}: los dos menús están y cada uno trae sus dos opciones`, async ({ page }) => {
      await abrirGrilla(page, grilla);
      for (const menu of [MENU_CARGA, MENU_ESTADO]) {
        const { ancla, item: cargar } = await abrirMenu(page, menu, OPCION_CARGAR, grilla); // abre el menú
        const plantilla = await localizarBoton(page, OPCION_PLANTILLA);         // mismo menú, ya abierto
        expect(plantilla, `En ${grilla}, el menú '${menu[0]}' no trae la opción '${OPCION_PLANTILLA[0]}'.`).not.toBeNull();
        await cerrarMenu(page, ancla, cargar);
      }
    });
  }

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
    const { t } = await idiomaDeLaSesion(page);
    const progreso = await observarProgreso(page, t);
    const cargar = await opcionDelMenu(page, MENU_CARGA, OPCION_CARGAR);
    const [selector] = await Promise.all([page.waitForEvent("filechooser", { timeout: 15_000 }), cargar.click()]);
    const [descarga] = await Promise.all([
      page.waitForEvent("download", { timeout: 150_000 }),
      selector.setFiles(path.join(DATOS, "dn04-carga-con-error.xlsx")),
    ]);
    // D-75 (2026-10-01): el indicador anuncia "hasta un minuto" (era "hasta 2 minutos"; la escritura se corta a los 30 s).
    // Se comprueba con el ESPÍA, no en el DOM: esta carga no escribe nada y la API puede responder en menos de los ~400 ms
    // que tarda la plataforma en pintar el indicador (la primera versión esperaba el texto en el DOM y falló así el
    // 2026-10-02, después de dos corridas verdes de casualidad).
    expect(t.validando).toMatch(/puede tardar hasta un minuto|it may take up to a minute/);
    expect(await progreso.mostrados()).toContain(t.validando);
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

  // L-04 — texto FIJO en español con la interfaz en inglés. El arnés de arriba localiza los menús con las etiquetas de
  // los dos idiomas (no puede suponer el idioma de la sesión), y por eso NO sirve para este caso: un menú localizado
  // ("Bulk load") también lo pasaría. Acá la comparación es exacta contra el texto español, y el localizador solo
  // busca el español: si el menú dice otra cosa, el item "no aparece" y el mensaje lista lo que la barra sí trae.
  //
  // Solo la mitad en inglés (1033). La mitad en español (3082) es N/A: Dev tiene SOLO el idioma 1033 instalado, así que
  // la interfaz de `# User 3` no se puede llevar a 3082 sin cambiar la configuración del entorno (no autorizado). Cuando
  // 3082 esté instalado, esta misma comparación exacta vale tal cual con la interfaz en español.
  test("L-04: con la interfaz en inglés, los menús llevan texto fijo en español y un botón nativo sale en inglés", async ({ page }) => {
    await abrirClientes(page);
    const { lcid } = await idiomaDeLaSesion(page);
    expect(lcid, "Este caso es la mitad en inglés: la sesión tiene que estar en 1033.").toBe(1033);

    const barra = page.getByRole("main");
    const itemsDeLaBarra = async () => (await barra.getByRole("menuitem").allTextContents()).map((t) => t.trim());

    // Control de que la interfaz está en inglés: un botón nativo (de la plataforma, sin nada de este proyecto) en la
    // MISMA barra. Su etiqueta sí cambia con el idioma de la interfaz; si saliera "Nuevo", el 1033 del usuario no rige.
    const nuevo = barra.getByRole("menuitem", { name: /^New\b/ }).first();
    expect(await nuevo.waitFor({ state: "visible", timeout: 10_000 }).then(() => true).catch(() => false),
      `Falta el botón nativo 'New' en la barra: no se puede probar que la interfaz esté en inglés. Items: ${JSON.stringify(await itemsDeLaBarra())}`).toBe(true);
    expect((await nuevo.textContent()).trim()).toBe("New");

    /** El item que EMPIEZA con `texto` (su nombre accesible es la etiqueta + el tooltip) y su texto visible exacto. */
    const textoDelItem = async (contexto, texto, listar) => {
      const item = contexto.getByRole("menuitem", { name: new RegExp(`^${texto.replace(/[.*+?^${}()|[\]\\/]/g, "\\$&")}`) }).first();
      const esta = await item.waitFor({ state: "visible", timeout: 10_000 }).then(() => true).catch(() => false);
      expect(esta, `No hay ningún item que empiece con '${texto}'. Items presentes: ${JSON.stringify(await listar())}`).toBe(true);
      return { item, texto: ((await item.textContent()) ?? "").trim() };
    };
    const itemsAbiertos = async () => (await page.getByRole("menuitem").allTextContents()).map((t) => t.trim());

    for (const textoAncla of [TEXTO_FIJO.menuCarga, TEXTO_FIJO.menuEstado]) {
      const ancla = await textoDelItem(barra, textoAncla, itemsDeLaBarra);
      expect(ancla.texto, `El ancla del menú '${textoAncla}'`).toBe(textoAncla);
      await ancla.item.click();
      const cargar = await textoDelItem(page, TEXTO_FIJO.opcionCargar, itemsAbiertos);
      expect(cargar.texto, `La opción de '${textoAncla}'`).toBe(TEXTO_FIJO.opcionCargar);
      const plantilla = await textoDelItem(page, TEXTO_FIJO.opcionPlantilla, itemsAbiertos);
      expect(plantilla.texto, `La opción de '${textoAncla}'`).toBe(TEXTO_FIJO.opcionPlantilla);
      await cerrarMenu(page, ancla.item, cargar.item);
    }
  });

  // L-08 — cancelar el selector. Playwright NO puede "cancelar" el diálogo nativo de archivos: en headless intercepta el
  // selector (`filechooser`) y solo ofrece completarlo. `setFiles([])` lo completa con CERO archivos, que es el estado
  // en que un usuario deja el `input` al cancelar: sin archivo elegido, y el manejador del JS (`change` → `files[0]`)
  // no encuentra nada y no hace nada. Lo que NO ejercita: el evento `cancel` del navegador (el que dispara un cancelar
  // real), cuyo único efecto en el JS es retirar el `input` suelto (`retirarSelector`); Playwright emite `input`/`change`,
  // no `cancel`. Lo que SÍ prueba: que el selector se abrió con el clic (si no, el `filechooser` no llega y el test falla
  // ahí), y que sin archivo no hay indicador, ni diálogo, ni llamada a ninguna Custom API.
  for (const [paso, menu] of [["a", MENU_CARGA], ["b", MENU_ESTADO]]) {
    test(`L-08 (${paso}): '${menu[0]}' → 'Cargar archivo' y cancelar el selector no hace nada`, async ({ page }) => {
      await abrirClientes(page);
      const { t } = await idiomaDeLaSesion(page);
      const progreso = await observarProgreso(page, t);
      const llamadas = grabarLlamadasDeCatalogos(page);

      const selector = await abrirSelector(page, menu); // evidencia positiva: el selector SE ABRIÓ con el clic
      await selector.setFiles([]);

      await page.waitForTimeout(3_000); // colchón: una llamada tardía, si existiera, tiene que llegar a la red antes de leer
      expect(llamadas.detener(), "Cancelar el selector no debe llamar a ninguna Custom API de catálogos.").toEqual([]);
      expect(await progreso.mostrados(), "Cancelar el selector no debe pedir ningún indicador de progreso.").toEqual([]);
      expect(await progreso.enOrden(), "Cancelar el selector no debe pintar ningún indicador de progreso.").toEqual([]);
      await expect(page.getByRole("dialog"), "Cancelar el selector no debe abrir ningún diálogo.").toHaveCount(0);
      await expect(page.locator('[row-index="0"]'), "La grilla tiene que quedar como estaba.").toBeVisible();
    });
  }

  test("L-08 (c): 'Cargar archivo' con un .txt avisa que tiene que ser .xlsx y no llama a la API", async ({ page }) => {
    await abrirClientes(page);
    const { t } = await idiomaDeLaSesion(page);
    const progreso = await observarProgreso(page, t);
    const llamadas = grabarLlamadasDeCatalogos(page);

    const selector = await abrirSelector(page, MENU_CARGA);
    await selector.setFiles({ name: "no-es-excel.txt", mimeType: "text/plain", buffer: Buffer.from("esto no es un libro de Excel") });

    // Evidencia positiva: el aviso aparece, con el texto EXACTO del JS (`getByText(..., { exact: true })` exige que el
    // elemento tenga ese texto y nada más: un aviso con algo pegado no pasa).
    const dialogo = await esperarDialogo(page);
    await expect(dialogo.getByText(t.extensionInvalida, { exact: true })).toBeVisible();

    await page.waitForTimeout(3_000);
    expect(llamadas.detener(), "Un .txt no debe llegar a ninguna Custom API: el JS valida la extensión antes.").toEqual([]);
    expect(await progreso.mostrados(), "Un .txt no debe llegar ni a pedir 'Leyendo el archivo…'.").toEqual([]);
    expect(await progreso.enOrden(), "Un .txt no debe pintar ningún indicador de progreso.").toEqual([]);
    await cerrarAviso(dialogo, t);
  });
});

// L-09 y L-55 ESCRIBEN en Dev. Cada describe siembra sus propios datos (`datos_dn04_l09.py --sembrar`, que antes borra
// cualquier resto) y los limpia al final (`--limpiar`, que además verifica que no queda nada) aunque el caso falle:
// el `afterAll` corre siempre. `MPPP_CONSERVAR_DATOS=1` salta la limpieza, igual que el teardown del arnés.
function conDatosPropios(destino = {}) {
  test.beforeAll(async () => {
    if (usuario !== "admin_planes") return;
    destino.datos = (await correrDatosL09("--sembrar")).detalle;
  });
  test.afterAll(async () => {
    if (usuario !== "admin_planes") return;
    if (process.env.MPPP_CONSERVAR_DATOS === "1") {
      console.log("[dn04-l09] MPPP_CONSERVAR_DATOS=1: no se limpian los datos.");
      return;
    }
    await correrDatosL09("--limpiar");
  });
}

test.describe("DN-04 — carga y cambio de estado que graban (L-09, Administrador de planes)", () => {
  test.skip(usuario !== "admin_planes", "solo con la sesión del Administrador de planes");
  test.beforeEach(() => mkdirSync(CARPETA_DESCARGAS, { recursive: true }));
  conDatosPropios();

  // Un solo test con dos pasos: el paso 4 desactiva el cliente que crea la carga, así que no tiene sentido sin ella. Dos
  // `test.step` y no dos tests en serie, para que un fallo del paso 1 no oculte (con "did not run") el resultado del paso 4.
  test("L-09: la carga que graba y el cambio de estado que la sigue (indicador, resultado, resumen y refresco de la grilla)", async ({ page }) => {
    test.setTimeout(300_000);
    await abrirClientes(page);
    const { t } = await idiomaDeLaSesion(page);

    await test.step("carga que graba: tres mensajes, descarga, resumen y grilla refrescada", async () => {
      const filaNueva = page.locator("[row-index]", { hasText: "ZZP-Cliente 4" });
      expect(await filaNueva.count(), "ZZP-Cliente 4 no debe estar en la grilla ANTES de cargar (el cliente lo crea este caso).").toBe(0);
      // Marca para probar después que la página NO se recargó (una recarga la borra): la grilla se refrescó sola.
      await page.evaluate(() => { window.__sinRecargar = true; });
      const progreso = await observarProgreso(page, t); // ANTES de elegir el archivo
      const llamadas = grabarLlamadasDeCatalogos(page);

      const selector = await abrirSelector(page, MENU_CARGA);
      const [descarga] = await Promise.all([
        page.waitForEvent("download", { timeout: 150_000 }),
        selector.setFiles(path.join(DATOS, "dn04-carga-ok.xlsx")),
      ]);

      // La descarga: `<nombre original sin extensión>-resultado.xlsx`, con una fila por fila del archivo subido.
      expect(descarga.suggestedFilename()).toBe("dn04-carga-ok-resultado.xlsx");
      const ruta = path.join(CARPETA_DESCARGAS, descarga.suggestedFilename());
      await descarga.saveAs(ruta);
      const { filas } = await leerXlsx(ruta);
      expect(filas.length, "El resultado tiene una fila por cada fila de datos del archivo subido.").toBe(2);
      expect(filas[0][11], "Columna Resultado (L) de la fila 2: crea el cliente, el plan, el correo y la autorización.").toMatch(/^Creado: cliente, plan, correo, autorización/);
      expect(filas[1][11], "Columna Resultado (L) de la fila 3: el cliente 999980005 existe INACTIVO, no se crea nada y se advierte.").toMatch(/^Sin cambios.*Advertencia: .*inactivo/);

      // El resumen, con el texto EXACTO del JS: 1 cliente, 1 plan, 1 correo y 1 autorización creados, más la advertencia de
      // la fila 3. `exact: true` exige que el elemento diga eso y nada más.
      const dialogo = await esperarDialogo(page, { timeout: 150_000 });
      const resumen = armar(t.cargaGrabada, { clientes: 1, planes: 1, correos: 1, autorizaciones: 1 }) + " " +
        armar(t.conAdvertencias, { advertencias: 1 });
      await expect(dialogo.getByText(resumen, { exact: true })).toBeVisible();

      // Control del mecanismo de red de L-08: acá la misma grabación tiene que ver EXACTAMENTE la llamada de la carga.
      expect(llamadas.detener()).toEqual([{ api: "sanic_mppp_capi_cargarcatalogos", metodo: "POST" }]);
      // Los tres mensajes de progreso. El CONTRATO DEL JS (lo que pide, en este orden): los tres `show`, exactos.
      expect(await progreso.mostrados(), "Mensajes que el JS pide con showProgressIndicator, en orden.").toEqual([t.leyendo, t.validando, t.preparando]);
      // Lo que llega a PINTARSE en el DOM. Hallazgo del 2026-10-02: la plataforma pinta el indicador ~400 ms después del
      // `show`, así que los mensajes fugaces ("Leyendo", que dura lo que el FileReader, y "Preparando", lo que `abrirXlsx`)
      // se cierran antes de verse. Exigirlos en el DOM era pedir algo NO OBSERVABLE (decisión del arquitecto: el defecto era
      // del caso, no del producto). El contrato de los tres queda en la aserción del espía, de arriba. En el DOM se exige
      // el único que el usuario llega a ver, el de la llamada: acá la carga escribe 4 registros y tarda segundos (con una
      // API que responde en menos de ~400 ms, ni ese se pintaría).
      expect(await progreso.enOrden(), "El mensaje de la llamada a la API (1, Validando) tiene que llegar a verse.").toContain(1);

      // Al cerrar el diálogo, el cliente nuevo aparece en la grilla SIN recargar la página.
      await cerrarAviso(dialogo, t);
      await expect(filaNueva.first(), "ZZP-Cliente 4 tiene que aparecer en la grilla tras cerrar el resumen.").toBeVisible({ timeout: 30_000 });
      expect(await page.evaluate(() => window.__sinRecargar === true),
        "La página se recargó: la grilla no se refrescó sola, la recargó el navegador.").toBe(true);
    });

    await test.step("paso 4: desactivar el cliente nuevo y activar un correo que ya está activo", async () => {
      const llamadas = grabarLlamadasDeCatalogos(page);
      const selector = await abrirSelector(page, MENU_ESTADO);
      const [descarga] = await Promise.all([
        page.waitForEvent("download", { timeout: 150_000 }),
        selector.setFiles(path.join(DATOS, "dn04-estado-ok.xlsx")),
      ]);
      expect(descarga.suggestedFilename()).toBe("dn04-estado-ok-resultado.xlsx");
      const ruta = path.join(CARPETA_DESCARGAS, descarga.suggestedFilename());
      await descarga.saveAs(ruta);
      const { filas } = await leerXlsx(ruta);
      expect(filas.length).toBe(2);
      console.log(`[dn04-l09] columna Resultado del cambio de estado: ${JSON.stringify(filas.map((f) => f[6]))}`);

      // CLIENTE 999980004 DESACTIVAR (lo desactiva) + CORREO zz-l-ana ACTIVAR (ya activo: sin cambios). Desactivar un
      // cliente que tiene una autorización activa advierte "deja sin efecto 1 autorizaciones" (DN-04 Excel 2, regla 6:
      // la advertencia sale porque la fila REALMENTE desactiva algo), así que el resumen lleva la advertencia al final.
      const dialogo = await esperarDialogo(page, { timeout: 150_000 });
      const resumen = armar(t.estadoGrabado, { activados: 0, desactivados: 1, sincambios: 1 }) + " " +
        armar(t.conAdvertencias, { advertencias: 1 });
      await expect(dialogo.getByText(resumen, { exact: true })).toBeVisible();
      expect(llamadas.detener()).toEqual([{ api: "sanic_mppp_capi_cambiarestadocatalogos", metodo: "POST" }]);
      await cerrarAviso(dialogo, t);

      // Lo grabado, leído de Dataverse: el cliente quedó inactivo (statecode 1).
      const cliente = await leerDeDataverse(page, "sanic_mppp_tbl_clientes?$select=sanic_nombre,statecode&$filter=sanic_cifbac eq '999980004'");
      expect(cliente.value.map((c) => [c.sanic_nombre, c.statecode])).toEqual([["ZZP-Cliente 4", 1]]);
    });
  });
});

// L-55, parte del formulario: la plataforma no deja pasar de 50 caracteres en Comentarios. (La parte de Web API, PATCH con
// 51 y con 50, es del set de API; acá solo el formulario.)
test.describe("DN-04 — L-55: Comentarios admite 50 caracteres en el formulario (Administrador de planes)", () => {
  test.skip(usuario !== "admin_planes", "solo con la sesión del Administrador de planes");
  const sembrado = {};
  conDatosPropios(sembrado);

  test("L-55: tipear 51 caracteres en Comentarios deja 50 en el campo y se guardan 50", async ({ page }) => {
    test.setTimeout(120_000);
    await abrirRegistro(page, "sanic_mppp_tbl_autorizacioncliente", sembrado.datos.autorizacionId);
    const campo = page.getByRole("textbox", { name: "Comentarios" });
    await campo.waitFor({ state: "visible", timeout: 30_000 });

    // `pressSequentially` y no `fill`: `fill` asigna el valor entero de golpe y no respeta `maxlength`; teclear carácter
    // por carácter es lo que hace una persona, y ahí el navegador corta en el límite del campo.
    const texto = "0123456789".repeat(5) + "X"; // 51 caracteres; el 51 es la X
    expect(texto.length).toBe(51);
    await campo.click();
    await campo.pressSequentially(texto);
    expect(await campo.inputValue(), "El campo tiene que haberse quedado con los primeros 50 caracteres.").toBe(texto.slice(0, 50));

    // Guardar (Ctrl+S) y leer lo que quedó en Dataverse: 50 caracteres, ni uno más.
    await page.keyboard.press("Control+S");
    const guardado = async () => {
      const r = await leerDeDataverse(page, `sanic_mppp_tbl_autorizacionclientes(${sembrado.datos.autorizacionId})?$select=sanic_comentarios`);
      return r.sanic_comentarios ?? "";
    };
    await expect.poll(async () => (await guardado()).length, { timeout: 30_000, message: "El formulario no guardó el comentario." }).toBeGreaterThan(0);
    expect(await guardado()).toBe(texto.slice(0, 50));
  });
});

test.describe("DN-04 — menús de catálogos (roles que NO son Administrador de planes)", () => {
  // L-02 (Ejecutivo) y L-03 (Supervisor y Administrador técnico): un test por rol y por grilla. Cada corrida usa UNA
  // sesión (`MPPP_USUARIO`), así que el mismo test cubre al rol activo.
  test.skip(!["ejecutivo", "supervisor", "admin_tecnico"].includes(usuario), "solo con la sesión del Ejecutivo, el Supervisor o el Administrador técnico");

  for (const grilla of GRILLAS) {
    test(`${grilla}: '${usuario}' no ve los menús de carga ni de activación masiva`, async ({ page }) => {
      const abierta = await abrirGrillaDelRol(page, grilla);
      // "La grilla no existe para el rol" (sitemap recortado) se SALTA con su evidencia; nunca se da por verde. Cualquier
      // otro error ya se lanzó arriba y hace fallar el test.
      if (!abierta.existe) test.skip(true, abierta.motivo);

      // Control: la barra de comandos SE DIBUJÓ. Sin esto, "no hay menús" también se vería con una barra que no cargó.
      const principal = page.getByRole("main");
      await expect(principal.getByRole("menubar").first(), "La barra de comandos no se dibujó: 'sin menús' no probaría nada.").toBeVisible({ timeout: 30_000 });
      await expect(principal.getByRole("menuitem").first()).toBeVisible();

      expect(await localizarBoton(page, MENU_CARGA, { raiz: principal }), `'${MENU_CARGA[0]}' visible en ${grilla} para ${usuario}.`).toBeNull();
      expect(await localizarBoton(page, MENU_ESTADO, { raiz: principal }), `'${MENU_ESTADO[0]}' visible en ${grilla} para ${usuario}.`).toBeNull();
    });
  }
});
