// Semáforo compartido con `herramientas/pruebas/semaforo.py` — el MISMO
// archivo (`local/semaforo-pruebas.json`), el MISMO esquema JSON. No hay una
// "librería de semáforo de Node" aparte: este arnés lee y escribe el mismo
// candado que usan los sets de Python (`correr.py`), para que ninguno de los
// dos lo pise. Ver el docstring de `herramientas/pruebas/semaforo.py` para el
// porqué del semáforo (evitar pisar una prueba manual del aprobador, u otro
// arnés, sobre el mismo entorno compartido).
//
// A PROPÓSITO, igual que `semaforo.py::tomar()`: si está ocupado, revienta EN
// EL MOMENTO con un mensaje claro — no reintenta ni espera solo. Esperar con
// polling a que se libere es una decisión de quien corre el arnés (ver
// pruebas/gui/README.md), nunca algo que el arnés haga por su cuenta.

import { existsSync, readFileSync, writeFileSync, unlinkSync } from "node:fs";
import path from "node:path";
import { RAIZ_REPO } from "../config.mjs";

export const RUTA_SEMAFORO = path.join(RAIZ_REPO, "local", "semaforo-pruebas.json");

function leer() {
  if (!existsSync(RUTA_SEMAFORO)) return null;
  try {
    return JSON.parse(readFileSync(RUTA_SEMAFORO, "utf-8"));
  } catch {
    return null;
  }
}

/** Bloqueo MANUAL del aprobador (`pid` null) nunca se libera solo: solo con
 * `--liberar` a mano. Un PID de arnés se chequea de verdad con `kill(pid, 0)`. */
function vivo(pid) {
  if (pid === null || pid === undefined) return true;
  try {
    process.kill(pid, 0);
    return true;
  } catch (error) {
    if (error.code === "ESRCH") return false; // el proceso ya no existe
    return true; // EPERM u otro: existe (de otro usuario), no asumir muerto
  }
}

/**
 * Estado actual del semáforo: `null` si está libre. Si el archivo quedó de un
 * arnés que murió sin soltarlo, lo limpia solo y devuelve `null` también
 * (mismo comportamiento que `semaforo.py::estado()`).
 */
export function estado() {
  const actual = leer();
  if (actual === null) return null;
  if (!vivo(actual.pid)) {
    try {
      unlinkSync(RUTA_SEMAFORO);
    } catch {
      /* si ya no está, no pasa nada */
    }
    return null;
  }
  return actual;
}

/**
 * Toma el semáforo para ESTE proceso (Node) o revienta con `Error` y un
 * mensaje claro si está ocupado. Devuelve una función `soltar()` — llamarla
 * SIEMPRE al terminar, pase lo que pase (try/finally o el teardown de
 * Playwright), igual que `semaforo.py::tomar()` como context manager.
 */
export function tomar(motivo) {
  const ocupado = estado();
  if (ocupado !== null) {
    const quien =
      ocupado.pid === null || ocupado.pid === undefined
        ? "un bloqueo MANUAL del aprobador"
        : `otro proceso (PID ${ocupado.pid})`;
    throw new Error(
      "SEMÁFORO EN ROJO: no arranca nada.\n" +
        `  Lo puso ${quien} el ${ocupado.cuando}: ${JSON.stringify(ocupado.motivo)}\n` +
        "  Esperá a que se libere (sondear con `python3 herramientas/pruebas/semaforo.py --estado`) " +
        "antes de reintentar.\n" +
        "  NUNCA uses --liberar vos mismo: solo lo libera quien lo puso."
    );
  }
  writeFileSync(
    RUTA_SEMAFORO,
    JSON.stringify({ pid: process.pid, motivo, cuando: new Date().toISOString() })
  );
  let soltado = false;
  return function soltar() {
    if (soltado) return;
    soltado = true;
    if (existsSync(RUTA_SEMAFORO)) {
      try {
        unlinkSync(RUTA_SEMAFORO);
      } catch {
        /* nada más que hacer si ya no está */
      }
    }
  };
}
