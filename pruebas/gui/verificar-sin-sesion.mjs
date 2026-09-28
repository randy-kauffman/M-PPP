#!/usr/bin/env node
// Verifica lo que SI se puede verificar sin una sesion real guardada.
// No corre ningun caso de Playwright: eso necesita local/sesion-<usuario>.json,
// que todavia no existe (lo genera el aprobador con guardar-sesion.mjs).
//
// Uso: node verificar-sin-sesion.mjs

import { readFileSync, existsSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { execFileSync } from "node:child_process";
import { BOTONES_FILA, COLUMNAS_EXPORTACION_ESPERADAS } from "./selectores.mjs";
import { RAIZ_REPO, CARPETA_LOCAL, rutaSesion, urlApp } from "./config.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

let fallas = 0;

function chequear(nombre, fn) {
  try {
    const detalle = fn();
    console.log(`PASA — ${nombre}${detalle ? `: ${detalle}` : ""}`);
  } catch (error) {
    fallas++;
    console.log(`FALLA — ${nombre}: ${error.message}`);
  }
}

chequear("selectores.mjs centraliza los 7 botones de Fila, sin ids repetidos", () => {
  if (BOTONES_FILA.length !== 7) {
    throw new Error(`se esperaban 7 botones, hay ${BOTONES_FILA.length}`);
  }
  const ids = BOTONES_FILA.map((b) => b.id);
  const unicos = new Set(ids);
  if (unicos.size !== ids.length) {
    throw new Error(`hay ids repetidos: ${ids.join(", ")}`);
  }
  return ids.join(", ");
});

chequear("cada boton tiene etiqueta en 1033 y 3082 (multiidioma)", () => {
  for (const boton of BOTONES_FILA) {
    if (!boton.etiqueta[1033] || !boton.etiqueta[3082]) {
      throw new Error(`'${boton.id}' no tiene las dos etiquetas`);
    }
  }
  return "ok";
});

chequear("las columnas de exportacion esperadas coinciden con el parametro sembrado", () => {
  if (COLUMNAS_EXPORTACION_ESPERADAS.length !== 12) {
    throw new Error(`se esperaban 12 columnas (playbooks/datos/parametros.md), hay ${COLUMNAS_EXPORTACION_ESPERADAS.length}`);
  }
  return `${COLUMNAS_EXPORTACION_ESPERADAS.length} columnas`;
});

chequear("urlApp() arma una URL valida con el appid correcto", () => {
  const url = new URL(urlApp());
  if (url.searchParams.get("appid") !== "7221de0a-9db6-f111-aaad-000d3a350099") {
    throw new Error("el appid no coincide con el de la MDA - MPPP - Mantenimiento PPP");
  }
  return url.toString();
});

chequear("/local/ esta ignorado por git (git check-ignore)", () => {
  const rutaDePrueba = rutaSesion("zz-verificacion");
  try {
    execFileSync("git", ["check-ignore", "-q", rutaDePrueba], { cwd: RAIZ_REPO });
  } catch (error) {
    if (typeof error.status === "number" && error.status === 1) {
      throw new Error(`'${rutaDePrueba}' NO esta ignorada por git. FUGA POTENCIAL, corregir el .gitignore.`);
    }
    throw new Error(`no se pudo confirmar con git (${error.message})`);
  }
  return rutaDePrueba;
});

chequear("guardar-sesion.mjs no tiene ningun console.log/error que imprima el storageState", () => {
  const fuente = readFileSync(path.join(__dirname, "guardar-sesion.mjs"), "utf8");
  // Heuristica: ninguna linea de consola debe mencionar la variable donde
  // viviria el storageState si alguien la asignara (el script actual NUNCA
  // la asigna: llama a `contexto.storageState({ path: ruta })` sin capturar
  // el resultado). Se chequea que siga sin haber una asignacion sospechosa.
  if (/=\s*await\s+contexto\.storageState\(\s*\)/.test(fuente)) {
    throw new Error("el script esta capturando el resultado de storageState() en una variable — riesgo de fuga si alguien la imprime");
  }
  if (/console\.(log|error)\([^)]*\b(cookie|token|storageState)\b/i.test(fuente)) {
    throw new Error("se encontro un console.log/error que menciona cookie/token/storageState");
  }
  return "sin coincidencias sospechosas";
});

chequear("el arnes no importa nada de herramientas/pruebas, src/ ni playbooks/", () => {
  const archivos = [
    "config.mjs",
    "selectores.mjs",
    "guardar-sesion.mjs",
    "descubrir-selectores.mjs",
    "ayudantes/sesion.mjs",
    "ayudantes/ribbon.mjs",
    "ayudantes/navegacion.mjs",
    "ayudantes/xlsx.mjs",
    "ayudantes/datos-gui.mjs",
    "ayudantes/semaforo.mjs",
    "ayudantes/guarda-sesiones.mjs",
    "setup/global-setup.mjs",
    "casos/ribbon-fila.spec.mjs",
    "casos/dn-02-descargar-para-digitar.spec.mjs",
  ];
  for (const archivo of archivos) {
    const fuente = readFileSync(path.join(__dirname, archivo), "utf8");
    if (/from\s+["'].*(herramientas\/pruebas|\/src\/|\/playbooks\/)/.test(fuente)) {
      throw new Error(`'${archivo}' importa algo fuera de pruebas/gui/`);
    }
  }
  return `${archivos.length} archivos revisados`;
});

chequear("el chequeo no asume ninguna sesion guardada (informativo, no es un requisito)", () => {
  const existe = existsSync(CARPETA_LOCAL);
  return existe
    ? `local/ existe en el filesystem (normal, es donde vive tambien local/pp_secrets.env)`
    : `local/ todavia no existe en este filesystem`;
});

console.log("");
if (fallas > 0) {
  console.log(`${fallas} verificacion(es) fallaron.`);
  process.exit(1);
} else {
  console.log("Todas las verificaciones posibles sin sesion pasaron.");
}
