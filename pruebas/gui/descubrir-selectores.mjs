#!/usr/bin/env node
// Descubre, contra la app REAL, el `data-id` que Unified Interface le pone a
// cada uno de los 7 botones de la barra de Fila. NO se corre en CI ni como
// parte de la suite: es una herramienta manual, para usar UNA VEZ que exista
// una sesion real, y volver a usar solo si Microsoft cambia algo.
//
// Por que existe: `selectores.mjs` ubica los botones por su etiqueta visible
// (estrategia recomendada por Microsoft, ver el comentario largo en ese
// archivo), y eso alcanza para que el arnes funcione. El `data-id` es un
// respaldo opcional, mas estable que el texto pero cuyo valor real nadie
// puede inventar sin mirar el DOM. Este script lo imprime en un bloque
// copiable para pegar a mano en `selectores.mjs` (a proposito no escribe el
// archivo solo: un cambio a un archivo centralizado se revisa antes de
// guardarse).
//
// Uso:
//   MPPP_USUARIO=ejecutivo node descubrir-selectores.mjs

import { chromium } from "@playwright/test";
import { usuarioActivo, verificarSesionExiste } from "./ayudantes/sesion.mjs";
import { abrirApp, irASitemap, esperarGrilla, seleccionarFilas } from "./ayudantes/navegacion.mjs";
import { localizarBoton } from "./ayudantes/ribbon.mjs";
import { BOTONES_FILA, IDIOMA_POR_DEFECTO, etiquetaDe } from "./selectores.mjs";
import { SITEMAP } from "./config.mjs";

async function main() {
  const usuario = usuarioActivo();
  const ruta = verificarSesionExiste(usuario);

  console.log(`Usando la sesion de '${usuario}'. Abriendo Chromium headless...`);
  const navegador = await chromium.launch({ headless: true });
  const contexto = await navegador.newContext({ storageState: ruta, acceptDownloads: true });
  const pagina = await contexto.newPage();

  try {
    await abrirApp(pagina);
    await irASitemap(pagina, SITEMAP.porDigitar);
    await esperarGrilla(pagina);
    await seleccionarFilas(pagina, 1); // con 1 fila alcanza: los 7 no llevan regla de estado

    console.log("");
    console.log("Copia estos valores de 'dataId' a mano en selectores.mjs, uno por boton:");
    console.log("---------------------------------------------------------------------");

    for (const boton of BOTONES_FILA) {
      const etiqueta = etiquetaDe(boton, IDIOMA_POR_DEFECTO);
      const locator = await localizarBoton(pagina, etiqueta);
      if (!locator) {
        console.log(`${boton.id}: NO SE ENCONTRO ("${etiqueta}"). Revisar antes que nada — ver caso ribbon.`);
        continue;
      }
      const dataId = await locator.getAttribute("data-id");
      console.log(`${boton.id}: dataId = ${dataId ? `"${dataId}"` : "null (no tiene atributo data-id)"}`);
    }

    console.log("---------------------------------------------------------------------");
    console.log(
      "Si un boton no tiene 'data-id', no pasa nada: el arnes sigue funcionando por etiqueta."
    );
  } finally {
    await navegador.close();
  }
}

main();
