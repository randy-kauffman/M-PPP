#!/usr/bin/env node
// Guarda la sesion de un usuario para que el arnes de Playwright la reutilice
// despues, SIN volver a pedir MFA en cada corrida.
//
// Lo corre el APROBADOR, en SU maquina (con escritorio): abre Chromium con
// cabeza (headed), navega a la app, espera a que la persona complete su
// login (usuario, contraseña, MFA) a mano, detecta que ya esta adentro, y
// guarda el storageState en local/sesion-<usuario>.json.
//
// Reglas duras de este script (no negociables):
//   1. NUNCA imprime cookies, tokens ni el contenido del storageState. El
//      resultado de `context.storageState(...)` ni siquiera se guarda en una
//      variable: se escribe directo a disco.
//   2. Antes de escribir, verifica con `git check-ignore` que la ruta de
//      destino este REALMENTE ignorada por git. Si no lo esta, PARA sin
//      escribir nada: seria una fuga de sesion al repositorio.
//
// Uso:
//   node guardar-sesion.mjs ejecutivo
//   node guardar-sesion.mjs supervisor

import { chromium } from "@playwright/test";
import { mkdirSync } from "node:fs";
import { execFileSync } from "node:child_process";
import { CARPETA_LOCAL, RAIZ_REPO, urlApp, rutaSesion } from "./config.mjs";

const ESPERA_LOGIN_MS = 15 * 60 * 1000; // 15 minutos: da tiempo de sobra para MFA

function usuarioDesdeArgumentos() {
  const usuario = process.argv[2];
  if (!usuario) {
    console.error("Uso: node guardar-sesion.mjs <usuario>");
    console.error("Ejemplo: node guardar-sesion.mjs ejecutivo");
    process.exit(1);
  }
  return usuario;
}

/**
 * Verifica con el propio git que la ruta de destino queda ignorada.
 * Fail-closed: cualquier duda razonable frena el script antes de escribir.
 */
function verificarQueGitIgnoraLaRuta(ruta) {
  try {
    execFileSync("git", ["check-ignore", "-q", ruta], { cwd: RAIZ_REPO });
    return true; // exit code 0 = esta ignorada
  } catch (error) {
    if (typeof error.status === "number" && error.status === 1) {
      return false; // exit code 1 = NO esta ignorada
    }
    // Cualquier otro caso (git no instalado, no es un repo git, etc.):
    // no se puede confirmar. Se trata como "no verificado" y se para.
    console.error("No se pudo confirmar con git si 'local/' esta ignorado (¿git no esta disponible?).");
    console.error(`Detalle: ${error.message}`);
    return false;
  }
}

async function esperarLoginCompleto(page) {
  console.log("");
  console.log("Se abrio Chromium. Completa el login (usuario, contraseña y MFA) en esa ventana.");
  console.log("Este script espera hasta 15 minutos a que la app termine de cargar.");
  console.log("");
  // Se espera por el sitemap (navegacion) de la app, que solo aparece una vez
  // adentro: cubre cualquier cantidad de redirecciones de login/MFA en el medio.
  await page.getByRole("navigation").first().waitFor({ state: "visible", timeout: ESPERA_LOGIN_MS });
}

async function main() {
  const usuario = usuarioDesdeArgumentos();
  const ruta = rutaSesion(usuario);

  if (!verificarQueGitIgnoraLaRuta(ruta)) {
    console.error("");
    console.error(
      `PARADO: '${ruta}' no quedo confirmada como ignorada por git. No se escribe ninguna sesion.`
    );
    console.error(
      "Revisa el .gitignore de la raiz del repo (tiene que cubrir /local/) antes de reintentar."
    );
    process.exit(1);
  }

  mkdirSync(CARPETA_LOCAL, { recursive: true });

  console.log(`Abriendo Chromium (headed) para guardar la sesion de '${usuario}'...`);
  const navegador = await chromium.launch({ headless: false });
  const contexto = await navegador.newContext();
  const pagina = await contexto.newPage();

  try {
    await pagina.goto(urlApp());
    await esperarLoginCompleto(pagina);

    // Se escribe DIRECTO a disco: el contenido nunca pasa por una variable
    // de este script ni por consola.
    await contexto.storageState({ path: ruta });

    console.log("");
    console.log(`listo, guardado en local/sesion-${usuario}.json`);
  } catch (error) {
    if (error instanceof Error && error.message.includes("Timeout")) {
      console.error("");
      console.error(
        "No se detecto que el login haya terminado dentro de los 15 minutos. No se guardo ninguna sesion."
      );
    } else {
      console.error("Fallo al guardar la sesion:", error.message);
    }
    process.exitCode = 1;
  } finally {
    await navegador.close();
  }
}

main();
