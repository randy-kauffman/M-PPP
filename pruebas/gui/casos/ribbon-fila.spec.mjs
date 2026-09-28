// Caso "ribbon": los botones propios de la barra de Fila tienen que
// DIBUJARSE cuando corresponde. Es exactamente la clase de defecto que
// motivo este arnes: el boton "Descargar para digitar" existia en el
// servidor (verificado por API) y no se dibujaba — la causa era la metadata
// compilada del ribbon. Ninguna prueba de API detecta eso; esta si, porque
// mira el DOM real.
//
// ALCANCE VERIFICADO (2026-09-27, contra la app real, no supuesto): esta
// vista ("Mis clientes - por digitar") filtra Fila por Estado = Validada
// (diseno/05-app-model-driven.md). De los 7 botones, 5 tienen un EnableRule
// que admite Validada (digitada, rechazadaas400, anular, atendida,
// descargarparadigitar — ver TRANSICIONES_VALIDAS en selectores.mjs) y esos
// son los que este caso puede probar acá. `aprobar` y `devolver` SOLO
// admiten Estado = Digitada: no hay forma de que aparezcan seleccionando
// filas de ESTA vista, por diseño de negocio, no por un bug. Probarlos
// necesita la vista "Por aprobar" con una fila Digitada — pendiente, listado
// en el README junto con los demás casos de `pruebas/05-app-y-comandos.md`
// todavía sin cubrir.
//
// Ausente vs deshabilitado son diagnosticos OPUESTOS y el reporte los separa:
//   - AUSENTE cuando SÍ se esperaba -> sospechar metadata de ribbon
//   - DESHABILITADO -> sospechar el EnableRule contra la seleccion actual
//   - AUSENTE cuando NO se esperaba (aprobar/devolver acá) -> comportamiento correcto

import { test, expect } from "@playwright/test";
import { usuarioActivo, verificarSesionExiste } from "../ayudantes/sesion.mjs";
import { abrirApp, irASitemap, esperarGrilla, seleccionarFilas } from "../ayudantes/navegacion.mjs";
import { verificarBotones, formatearDiagnostico } from "../ayudantes/ribbon.mjs";
import { BOTONES_FILA, ESTADOFILA, botonesEsperados } from "../selectores.mjs";
import { SITEMAP } from "../config.mjs";

const CANTIDAD_FILAS_VALIDADA = 3; // las 3 filas Validada que siembra sembrar_datos_gui.py

const usuario = usuarioActivo();
test.use({ storageState: verificarSesionExiste(usuario) });

test.describe("Ribbon de Fila — presencia de los botones (DN-02 y fase 1)", () => {
  test("con filas Validada seleccionadas, los 5 comandos que las admiten están presentes y habilitados", async ({
    page,
  }) => {
    await abrirApp(page);
    await irASitemap(page, SITEMAP.porDigitar);
    await esperarGrilla(page);
    await seleccionarFilas(page, CANTIDAD_FILAS_VALIDADA);

    const idsEsperados = botonesEsperados([ESTADOFILA.VALIDADA]);
    const resultados = await verificarBotones(page, BOTONES_FILA, idsEsperados);

    // Se imprime el diagnostico de cada boton SIEMPRE (pase o falle): es
    // justo lo que el reporte tiene que decir, no un si/no generico.
    for (const resultado of resultados) console.log(formatearDiagnostico(resultado));

    const ausentesInesperados = resultados.filter((r) => !r.presente && r.esperado);
    expect(
      ausentesInesperados,
      `Botones AUSENTES que SÍ se esperaban con Validada: ` +
        `${ausentesInesperados.map((r) => r.id).join(", ") || "ninguno"}. ` +
        `Sospechar metadata de ribbon compilada: regenerar desde Command Checker.`
    ).toHaveLength(0);

    const deshabilitadosInesperados = resultados.filter((r) => r.presente && !r.habilitado && r.esperado);
    expect(
      deshabilitadosInesperados,
      `Botones que deberían estar HABILITADOS con Validada y no lo están: ` +
        `${deshabilitadosInesperados.map((r) => r.id).join(", ") || "ninguno"}.`
    ).toHaveLength(0);

    const presentesInesperados = resultados.filter((r) => r.presente && !r.esperado);
    expect(
      presentesInesperados,
      `Botones presentes que NO deberían estarlo con solo Validada seleccionada (revisar ` +
        `TRANSICIONES_VALIDAS en selectores.mjs, puede estar desactualizado): ` +
        `${presentesInesperados.map((r) => r.id).join(", ") || "ninguno"}.`
    ).toHaveLength(0);
  });

  test("sin ninguna fila seleccionada, ningún botón propio se dibuja", async ({ page }) => {
    // Confirmado en vivo (2026-09-27): sin selección, la plataforma NO
    // dibuja ninguno de los 7 botones propios (no quedan "grises": el grupo
    // entero no aparece). Distinto de lo que documentaba una versión previa
    // de este caso, corregida tras verificar contra la app real en vez de
    // asumir el comportamiento clásico de ribbon.
    await abrirApp(page);
    await irASitemap(page, SITEMAP.porDigitar);
    await esperarGrilla(page);
    // Sin seleccionar nada.

    const resultados = await verificarBotones(page, BOTONES_FILA, []); // nada esperado
    for (const resultado of resultados) console.log(formatearDiagnostico(resultado));

    const presentes = resultados.filter((r) => r.presente);
    expect(
      presentes,
      `Sin selección, no se esperaba NINGÚN botón propio, y aparecieron: ` +
        `${presentes.map((r) => r.id).join(", ") || "ninguno"}. Si esto cambió, puede ser un ` +
        `cambio real de comportamiento de la plataforma — revisar antes de asumir bug.`
    ).toHaveLength(0);
  });
});
