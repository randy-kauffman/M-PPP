// Puente entre el arnés (Node/Playwright) y la siembra de datos real
// (`sembrar_datos_gui.py`, service principal). Un solo camino: el arnés NUNCA
// escribe en Dataverse por su cuenta — ni con la sesión delegada (no tiene el
// privilegio de Crear sobre Cliente/Plan, confirmado en vivo 2026-09-27, HTTP
// 403 "is missing prvCreatesanic_mppp_tbl_cliente privilege") ni con un
// segundo cliente Web API propio. Siempre pasa por este script Python, que
// reusa `herramientas/dataverse_api.py` (el mismo service principal que usan
// todas las `herramientas/pruebas/*.py`).
//
// El ÚNICO dato que el lado Node aporta es el `propietario`: el systemuserid
// de la sesión de navegador activa, descubierto con WhoAmI sobre ESA sesión
// (delegada, de solo lectura — WhoAmI sí funciona con la sesión delegada,
// confirmado en vivo). Nunca se hardcodea.

import { execFile } from "node:child_process";
import { promisify } from "node:util";
import path from "node:path";
import { fileURLToPath } from "node:url";

const execFileAsync = promisify(execFile);
const __dirname = path.dirname(fileURLToPath(import.meta.url));
const SCRIPT = path.resolve(__dirname, "..", "sembrar_datos_gui.py");

const H = { Accept: "application/json", "OData-MaxVersion": "4.0", "OData-Version": "4.0" };

/**
 * Descubre el usuario de la sesión activa vía WhoAmI, usando el mismo
 * storageState que van a usar los casos (sesión DELEGADA, de solo lectura
 * para este propósito).
 */
export async function usuarioDeLaSesion(apiRequestContext, entornoUrl) {
  const resp = await apiRequestContext.get(new URL("api/data/v9.2/WhoAmI", entornoUrl).toString(), {
    headers: H,
  });
  if (!resp.ok()) {
    throw new Error(`WhoAmI devolvió HTTP ${resp.status()}: ${(await resp.text()).slice(0, 300)}`);
  }
  const cuerpo = await resp.json();
  return cuerpo.UserId;
}

function correrPython(args) {
  return execFileAsync("python3", [SCRIPT, ...args], { cwd: path.dirname(SCRIPT), timeout: 120_000 });
}

/** Última línea de stdout es el JSON de resultado (mismo contrato que preparar_datos.py). */
function ultimaLineaJson(stdout) {
  const lineas = stdout.trim().split("\n");
  return JSON.parse(lineas[lineas.length - 1]);
}

/**
 * Siembra el fixture de datos GUI (Cliente/Plan/Solicitud/Filas), con el
 * Cliente asignado al usuario dado. Lanza si el script Python reporta error.
 */
export async function sembrarDatosGui(propietarioUserId, { filas = 3 } = {}) {
  const { stdout, stderr } = await correrPython(["--propietario", propietarioUserId, "--filas", String(filas)]);
  if (stderr && stderr.trim()) console.error("[sembrar_datos_gui.py stderr]", stderr.trim());
  const resultado = ultimaLineaJson(stdout);
  if (resultado.estado === "error") {
    throw new Error(`sembrar_datos_gui.py falló: ${JSON.stringify(resultado.detalle)}`);
  }
  return resultado;
}

/**
 * El `solicitudId`/`filaIds` que `sembrarDatosGui()` creó (o encontró ya creados)
 * en esta corrida, publicados por `setup/global-setup.mjs` vía `process.env`
 * (ver el comentario ahí: es el mecanismo documentado de Playwright para pasar
 * datos de `globalSetup` a los tests). Los casos que necesitan abrir un registro
 * PROPIO por su GUID (p.ej. la SubGrid de A-01, dentro del formulario de la
 * Solicitud sembrada) lo resuelven acá, nunca navegando una vista compartida y
 * asumiendo "la primera fila" (con varios agentes tocando Dev a la vez, esa fila
 * puede ser de cualquiera).
 */
export function fixtureSembrado() {
  const solicitudId = process.env.MPPP_GUI_SOLICITUD_ID;
  if (!solicitudId) {
    throw new Error(
      "No hay MPPP_GUI_SOLICITUD_ID: este proceso no pasó por setup/global-setup.mjs (¿se está " +
        "llamando a fixtureSembrado() fuera de un test de Playwright?) o la siembra no devolvió " +
        "solicitudId."
    );
  }
  let filaIds = [];
  try {
    filaIds = JSON.parse(process.env.MPPP_GUI_FILA_IDS || "[]");
  } catch {
    filaIds = [];
  }
  return { solicitudId, filaIds };
}

/** Limpia el fixture. No lanza si ya estaba limpio; sí si el script falla. */
export async function limpiarDatosGui() {
  const { stdout, stderr } = await correrPython(["--limpiar"]);
  if (stderr && stderr.trim()) console.error("[sembrar_datos_gui.py stderr]", stderr.trim());
  const resultado = ultimaLineaJson(stdout);
  if (resultado.estado === "error") {
    throw new Error(`sembrar_datos_gui.py --limpiar falló: ${JSON.stringify(resultado.detalle)}`);
  }
  return resultado;
}
