// Caso "ribbon": los 7 botones propios de la barra de Fila tienen que
// DIBUJARSE. Es exactamente la clase de defecto que motivo este arnes: el
// boton "Descargar para digitar" existia en el servidor (verificado por API)
// y no se dibujaba — la causa era la metadata compilada del ribbon. Ninguna
// prueba de API detecta eso; esta si, porque mira el DOM real.
//
// Ausente vs deshabilitado son diagnosticos OPUESTOS y el reporte los separa:
//   - AUSENTE     -> sospechar metadata de ribbon (RibbonDiffXml/Command Checker)
//   - DESHABILITADO -> sospechar el EnableRule contra la seleccion actual

import { test, expect } from "@playwright/test";
import { usuarioActivo, verificarSesionExiste } from "../ayudantes/sesion.mjs";
import { abrirApp, irASitemap, esperarGrilla, seleccionarFilas } from "../ayudantes/navegacion.mjs";
import { verificarBotones, formatearDiagnostico } from "../ayudantes/ribbon.mjs";
import { BOTONES_FILA, IDIOMA_POR_DEFECTO } from "../selectores.mjs";
import { SITEMAP } from "../config.mjs";

const usuario = usuarioActivo();
test.use({ storageState: verificarSesionExiste(usuario) });

test.describe("Ribbon de Fila — presencia de los 7 botones (DN-02 y fase 1)", () => {
  test("con una fila seleccionada, los 7 botones estan presentes y habilitados", async ({ page }) => {
    await abrirApp(page);
    await irASitemap(page, SITEMAP.porDigitar);
    await esperarGrilla(page);
    await seleccionarFilas(page, 1);

    const resultados = await verificarBotones(page, BOTONES_FILA, IDIOMA_POR_DEFECTO);

    // Se imprime el diagnostico de cada boton SIEMPRE (pase o falle), porque
    // es justo lo que el reporte tiene que decir: cual esta ausente y cual
    // deshabilitado, no un si/no generico.
    for (const resultado of resultados) {
      console.log(formatearDiagnostico(resultado));
    }

    const ausentes = resultados.filter((r) => !r.presente);
    const deshabilitados = resultados.filter((r) => r.presente && !r.habilitado);

    expect(
      ausentes,
      `Botones AUSENTES (no se dibujaron): ${ausentes.map((r) => r.id).join(", ") || "ninguno"}. ` +
        `Sospechar metadata de ribbon compilada: regenerar desde Command Checker.`
    ).toHaveLength(0);

    expect(
      deshabilitados,
      `Botones presentes pero DESHABILITADOS con una fila seleccionada: ` +
        `${deshabilitados.map((r) => r.id).join(", ") || "ninguno"}. ` +
        `Sospechar el EnableRule del comando, no la metadata.`
    ).toHaveLength(0);
  });

  test("sin ninguna fila seleccionada, los 7 botones estan presentes pero deshabilitados (A-05)", async ({
    page,
  }) => {
    await abrirApp(page);
    await irASitemap(page, SITEMAP.porDigitar);
    await esperarGrilla(page);
    // Sin seleccionar nada: EnableRule Mscrm.SelectionCountAtLeastOne.

    const resultados = await verificarBotones(page, BOTONES_FILA, IDIOMA_POR_DEFECTO);
    for (const resultado of resultados) {
      console.log(formatearDiagnostico(resultado));
    }

    const ausentes = resultados.filter((r) => !r.presente);
    expect(
      ausentes,
      `Botones AUSENTES sin seleccion: ${ausentes.map((r) => r.id).join(", ") || "ninguno"}. ` +
        `Estos botones deben estar PRESENTES (solo deshabilitados), no ausentes.`
    ).toHaveLength(0);

    const habilitados = resultados.filter((r) => r.presente && r.habilitado);
    expect(
      habilitados,
      `Botones que deberian estar deshabilitados sin seleccion, y no lo estan: ` +
        `${habilitados.map((r) => r.id).join(", ") || "ninguno"}.`
    ).toHaveLength(0);
  });
});
