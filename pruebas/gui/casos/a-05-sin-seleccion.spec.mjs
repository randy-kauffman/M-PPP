// Caso A-05 (`pruebas/05-app-y-comandos.md`): el `EnableRule`
// `Mscrm.SelectionCountAtLeastOne` — sin ninguna fila seleccionada, ningún botón
// propio se dibuja.
//
// CORRECCIÓN FECHADA (2026-09-28, esta migración). El título corto de este caso
// venía arrastrando, en `herramientas/pruebas/sets/set_a.py` (`a05()`, título de
// `pc.Caso`) y en una versión anterior de este mismo arnés, la expectativa VIEJA:
// "sin selección, los botones propios están DESHABILITADOS". Esa expectativa es
// INCORRECTA — el propio arnés ya la había descartado el 2026-09-27, con
// evidencia en vivo: sin selección, la plataforma no deja los 7 botones
// "presentes pero grises" (deshabilitados), los AUSENTA por completo — el grupo
// entero de comandos desaparece de la barra, no se dibuja nada que inspeccionar.
// `pruebas/05-app-y-comandos.md` (cuerpo del caso A-05) ya documentaba el hallazgo
// correcto; lo que faltaba corregir era el título corto (que seguía diciendo
// "deshabilitados" en `set_a.py`, moviendo a error a quien solo lee el resumen de
// `correr.py`) y separar esta aserción de `ribbon-fila.spec.mjs` (A-01) a su
// propio archivo, para que `set_a.py` pueda invocarla y reportarla de forma
// independiente. Ver el título corregido de `a05()` en `set_a.py` y este archivo.
//
// Por qué esto es un caso propio y no "confiar en que A-01 también lo prueba":
// AUSENTE y DESHABILITADO son diagnósticos OPUESTOS con el mismo síntoma en
// pantalla ("no lo veo") — confundirlos es justo lo que costó dos días con DN-02
// (A-15, el mismo archivo). Un caso que afirmara "deshabilitados" sin selección
// estaría describiendo mal el comportamiento real de la plataforma, y alguien que
// lo lea para diagnosticar un botón que "no aparece" buscaría la causa equivocada
// (un EnableRule) en vez de la correcta (nada que revisar: es el comportamiento
// esperado sin selección).

import { test, expect } from "@playwright/test";
import { usuarioActivo, verificarSesionExiste } from "../ayudantes/sesion.mjs";
import { abrirApp, irASitemap, esperarGrilla } from "../ayudantes/navegacion.mjs";
import { verificarBotones, formatearDiagnostico } from "../ayudantes/ribbon.mjs";
import { BOTONES_FILA } from "../selectores.mjs";
import { SITEMAP } from "../config.mjs";

const usuario = usuarioActivo();
test.use({ storageState: verificarSesionExiste(usuario) });

test.describe("A-05 — sin selección, ningún botón propio se dibuja (no 'deshabilitado': AUSENTE)", () => {
  test("sin ninguna fila seleccionada, la plataforma no dibuja ninguno de los 7 botones propios", async ({ page }) => {
    // Confirmado en vivo (2026-09-27): sin selección, la plataforma NO dibuja
    // ninguno de los 7 botones propios (no quedan "grises": el grupo entero no
    // aparece). Distinto de "deshabilitados", que era la expectativa vieja e
    // incorrecta que este caso reemplaza — ver la nota de corrección arriba.
    await abrirApp(page);
    await irASitemap(page, SITEMAP.porDigitar);
    await esperarGrilla(page);
    // Sin seleccionar nada.

    const resultados = await verificarBotones(page, BOTONES_FILA, []); // nada esperado
    for (const resultado of resultados) console.log(formatearDiagnostico(resultado));

    const presentes = resultados.filter((r) => r.presente);
    expect(
      presentes,
      `Sin selección, no se esperaba NINGÚN botón propio (AUSENTE, no deshabilitado), y aparecieron: ` +
        `${presentes.map((r) => r.id).join(", ") || "ninguno"}. Si esto cambió, puede ser un ` +
        `cambio real de comportamiento de la plataforma — revisar antes de asumir bug.`
    ).toHaveLength(0);

    // Segunda aserción, explícita: ninguno quedó "presente pero deshabilitado"
    // (la forma vieja e incorrecta de esperar este caso). Si algún día la
    // plataforma cambiara a ESE comportamiento, este caso lo señala como lo que
    // sería: un cambio real a investigar, no un defecto silencioso.
    const presentesPeroDeshabilitados = resultados.filter((r) => r.presente && !r.habilitado);
    expect(
      presentesPeroDeshabilitados,
      `Ninguno debería quedar 'presente pero deshabilitado' sin selección (esa era la expectativa ` +
        `VIEJA e incorrecta de este caso, corregida el 2026-09-28): ` +
        `${presentesPeroDeshabilitados.map((r) => r.id).join(", ") || "ninguno"}.`
    ).toHaveLength(0);
  });
});
