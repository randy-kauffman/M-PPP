// Resuelve y valida la ruta del storageState de un usuario, SIN leer ni
// imprimir su contenido en ningun momento (regla no negociable del proyecto).

import { existsSync } from "node:fs";
import { rutaSesion } from "../config.mjs";

/**
 * Usuario activo para esta corrida. Se toma de la variable de entorno
 * MPPP_USUARIO (no hay default: obliga a elegir explicitamente CON QUE
 * sesion se corre, para no correr por error con la sesion de otro rol).
 */
export function usuarioActivo() {
  const usuario = process.env.MPPP_USUARIO;
  if (!usuario) {
    throw new Error(
      "Falta MPPP_USUARIO. Ejemplo: MPPP_USUARIO=ejecutivo npx playwright test\n" +
        "(el usuario tiene que tener antes su sesion guardada con guardar-sesion.mjs)"
    );
  }
  return usuario;
}

/**
 * Verifica que exista el archivo de sesion del usuario activo. No abre el
 * archivo: solo `existsSync`. El contenido lo lee unicamente Playwright, por
 * dentro, al usarlo como `storageState`.
 */
export function verificarSesionExiste(usuario = usuarioActivo()) {
  const ruta = rutaSesion(usuario);
  if (!existsSync(ruta)) {
    throw new Error(
      `No existe la sesion de '${usuario}' (se esperaba en local/sesion-${usuario}.json).\n` +
        `Generala primero en una maquina con escritorio:\n` +
        `  node guardar-sesion.mjs ${usuario}\n` +
        `Ver pruebas/gui/README.md para el detalle.`
    );
  }
  return ruta;
}
