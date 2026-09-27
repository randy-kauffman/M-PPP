// Configuracion del entorno para el arnes de navegador.
// Un solo lugar para URLs, ids y rutas: si el entorno cambia, se toca aca.

import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

// Raiz del repo (pruebas/gui esta dos niveles adentro).
export const RAIZ_REPO = path.resolve(__dirname, "..", "..");

// `local/` vive en la raiz del repo, fuera del control de versiones
// (`/local/` esta en el .gitignore raiz). Ahi caen las sesiones guardadas.
export const CARPETA_LOCAL = path.join(RAIZ_REPO, "local");

export const ENTORNO = {
  url: "https://org36e60d9d.crm.dynamics.com/",
  appId: "7221de0a-9db6-f111-aaad-000d3a350099", // MDA - MPPP - Mantenimiento PPP
  tablaFila: "sanic_mppp_tbl_fila",
};

// URL de entrada a la app (sitemap por defecto). Desde aca se navega por UI
// a la entrada del sitemap que corresponda a cada caso (nunca por deep-link
// a una vista puntual: el id de la vista no es un dato estable que hayamos
// verificado, y navegar por UI es ademas mas fiel a lo que hace una persona).
export function urlApp() {
  const base = new URL("main.aspx", ENTORNO.url);
  base.searchParams.set("appid", ENTORNO.appId);
  return base.toString();
}

// Ruta al storageState de un usuario. Nunca se lee ni se imprime su contenido
// desde este archivo: solo se resuelve la ruta.
export function rutaSesion(usuario) {
  if (!usuario) {
    throw new Error(
      "Falta el nombre de usuario. Uso: node guardar-sesion.mjs <usuario>, y para correr " +
        "las pruebas: MPPP_USUARIO=<usuario> npx playwright test"
    );
  }
  return path.join(CARPETA_LOCAL, `sesion-${usuario}.json`);
}

// Nombres de las 10 entradas de sitemap y de las vistas que usa el arnes.
// Solo las que ya estan documentadas y verificadas en `diseno/05-app-model-driven.md`
// y `pruebas/05-app-y-comandos.md`. Si el arnes necesita otra vista, se agrega aca,
// nunca con el nombre "quemado" adentro de un caso de prueba.
export const SITEMAP = {
  porDigitar: "Por digitar", // abre "Mis clientes - por digitar" (Fila), la vista por defecto del Ejecutivo
};
