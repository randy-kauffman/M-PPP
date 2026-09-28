// Caso A-01 (`pruebas/05-app-y-comandos.md`): los botones propios de la barra de
// Fila tienen que DIBUJARSE cuando corresponde, en las DOS ubicaciones que declara
// `selectores.mjs` (`ubicaciones: ["HomepageGrid", "SubGrid"]`): la vista principal
// (HomepageGrid) Y la subgrilla de Filas dentro del formulario de una Solicitud
// (SubGrid). Es exactamente la clase de defecto que motivó este arnés: el botón
// "Descargar para digitar" existía en el servidor (verificado por API) y no se
// dibujaba — la causa era la metadata compilada del ribbon. Ninguna prueba de API
// detecta eso; esta sí, porque mira el DOM real.
//
// ALCANCE VERIFICADO (2026-09-27, contra la app real, no supuesto) para el test de
// HomepageGrid: esta vista ("Mis clientes - por digitar") filtra Fila por Estado =
// Validada (diseno/05-app-model-driven.md). De los 7 botones, 5 tienen un
// EnableRule que admite Validada (digitada, rechazadaas400, anular, atendida,
// descargarparadigitar — ver TRANSICIONES_VALIDAS en selectores.mjs) y esos son
// los que ese test puede probar acá. `aprobar` y `devolver` SOLO admiten Estado =
// Digitada: no hay forma de que aparezcan seleccionando filas de ESTA vista, por
// diseño de negocio, no por un bug. Probarlos necesita la vista "Por aprobar" con
// una fila Digitada — pendiente, listado en el README junto con los demás casos
// de `pruebas/05-app-y-comandos.md` todavía sin cubrir.
//
// El test de SubGrid (2026-09-28, esta migración) NO está confirmado en vivo
// todavía — reusa la misma lógica de diagnóstico (`ayudantes/ribbon.mjs`) sobre el
// subgrid que `ayudantes/navegacion.mjs::irASubgrid` ubica dentro del formulario
// de la Solicitud que esta misma corrida sembró (`fixtureSembrado()`, nunca una
// Solicitud cualquiera de una vista compartida).
//
// Ausente vs deshabilitado son diagnosticos OPUESTOS y el reporte los separa:
//   - AUSENTE cuando SÍ se esperaba -> sospechar metadata de ribbon
//   - DESHABILITADO -> sospechar el EnableRule contra la seleccion actual
//   - AUSENTE cuando NO se esperaba (aprobar/devolver acá) -> comportamiento correcto
//
// A-05 ("sin selección, ningún botón propio se dibuja") vivía como un segundo test
// de este mismo archivo hasta el 2026-09-28: se movió a su propio caso
// (`casos/a-05-sin-seleccion.spec.mjs`) para que `herramientas/pruebas/sets/set_a.py`
// pueda invocarlo y reportarlo de forma independiente (CONVENCIONES.md §4/§9: un
// archivo por caso, sin duplicar la infraestructura que ya prueba lo mismo).

import { test, expect } from "@playwright/test";
import { usuarioActivo, verificarSesionExiste } from "../ayudantes/sesion.mjs";
import { abrirApp, irASitemap, esperarGrilla, seleccionarFilas, abrirRegistro, irASubgrid, seleccionarFilaEnSubgrid } from "../ayudantes/navegacion.mjs";
import { verificarBotones, formatearDiagnostico } from "../ayudantes/ribbon.mjs";
import { fixtureSembrado } from "../ayudantes/datos-gui.mjs";
import { BOTONES_FILA, ESTADOFILA, botonesEsperados } from "../selectores.mjs";
import { SITEMAP, ENTORNO } from "../config.mjs";

const CANTIDAD_FILAS_VALIDADA = 3; // las 3 filas Validada que siembra sembrar_datos_gui.py

const usuario = usuarioActivo();
test.use({ storageState: verificarSesionExiste(usuario) });

test.describe("A-01 — Ribbon de Fila: los botones aparecen donde deben (HomepageGrid y SubGrid)", () => {
  test("HomepageGrid: con filas Validada seleccionadas, los 5 comandos que las admiten están presentes y habilitados", async ({
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

  test("SubGrid: dentro de la Solicitud, la subgrilla de Filas dibuja los mismos botones", async ({ page }) => {
    const { solicitudId } = fixtureSembrado();

    await abrirRegistro(page, ENTORNO.tablaSolicitud, solicitudId);
    const subgrid = await irASubgrid(page, "Filas", "Filas");
    await seleccionarFilaEnSubgrid(subgrid);

    const idsEsperados = botonesEsperados([ESTADOFILA.VALIDADA]);
    const resultados = await verificarBotones(page, BOTONES_FILA, idsEsperados);
    for (const resultado of resultados) console.log(`[SubGrid] ${formatearDiagnostico(resultado)}`);

    const ausentesInesperados = resultados.filter((r) => !r.presente && r.esperado);
    expect(
      ausentesInesperados,
      `SubGrid: botones AUSENTES que SÍ se esperaban con Validada: ` +
        `${ausentesInesperados.map((r) => r.id).join(", ") || "ninguno"}. Los 5 comandos declaran ` +
        `"SubGrid" entre sus ubicaciones en selectores.mjs: si no aparecen acá pero SÍ en el ` +
        `HomepageGrid (test anterior), es la metadata compilada de la ubicación SubGrid puntual, no ` +
        `un problema de reglas (mismo diagnóstico REGLA-vs-METADATA de A-15).`
    ).toHaveLength(0);
  });
});
