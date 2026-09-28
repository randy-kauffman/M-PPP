// Caso A-13 (`pruebas/05-app-y-comandos.md`): que cada etiqueta de botón tenga su
// texto en el idioma REAL de la sesión (1033 o 3082, nunca el otro), y que el
// mensaje de confirmación del JS (`TEXTOS` de `recursos/js/comandos.js`) también
// cambie de idioma.
//
// A diferencia de `ayudantes/ribbon.mjs::localizarBoton` (que a propósito prueba
// las DOS etiquetas juntas, porque no sabe de antemano el idioma de la sesión),
// este caso SÍ conoce el idioma esperado — se lo pasa `set_a.py` por
// `MPPP_IDIOMA_ESPERADO` (1033 o 3082, default 3082) según la sesión que invoca —
// y hace la aserción ESTRICTA: la etiqueta tiene que empezar con el texto del
// idioma esperado, NUNCA con el del otro. Es la aserción que realmente prueba
// multiidioma: "aparece un texto conocido" no alcanza si apareciera siempre en el
// mismo idioma pase lo que pase.
//
// HALLAZGO (leyendo `recursos/js/comandos.js`, 2026-09-28): el nombre de la acción
// que arma el mensaje de confirmación (`ejecutar("Digitada", ...)`, línea 568) es
// un literal en ESPAÑOL, fijo, sin pasar por `TEXTOS` — a diferencia del resto de
// la plantilla ("Apply \"{accion}\"..."), que sí traduce. En una sesión en inglés
// el diálogo dice literalmente `Apply "Digitada" to 1 selected row(s)?`: el
// CONTENEDOR del mensaje se traduce, el NOMBRE DE LA ACCIÓN adentro no. Este caso
// verifica el comportamiento REAL (con "Digitada" fijo), y deja esto documentado
// para quien decida si es un gap de UX a corregir — no es parte del alcance de
// esta migración arreglarlo, solo de las pruebas nombrarlo con evidencia
// (CONVENCIONES.md §7).

import { test, expect } from "@playwright/test";
import { usuarioActivo, verificarSesionExiste } from "../ayudantes/sesion.mjs";
import { abrirApp, irASitemap, esperarGrilla, seleccionarFilas } from "../ayudantes/navegacion.mjs";
import { verificarBotones, verificarBoton, formatearDiagnostico } from "../ayudantes/ribbon.mjs";
import { esperarDialogo, cancelarDialogo, esperarQueSeCierre } from "../ayudantes/dialogos.mjs";
import { BOTONES_FILA, ESTADOFILA, botonesEsperados, botonPorId, TEXTOS_JS, armarTexto } from "../selectores.mjs";
import { SITEMAP } from "../config.mjs";

const usuario = usuarioActivo();
test.use({ storageState: verificarSesionExiste(usuario) });

const IDIOMA_ESPERADO = Number(process.env.MPPP_IDIOMA_ESPERADO || 3082);
const IDIOMA_OTRO = IDIOMA_ESPERADO === 1033 ? 3082 : 1033;

test.describe(`A-13 — Multiidioma (esperado: ${IDIOMA_ESPERADO})`, () => {
  test("las etiquetas de los botones están en el idioma esperado, no en el otro", async ({ page }) => {
    await abrirApp(page);
    await irASitemap(page, SITEMAP.porDigitar);
    await esperarGrilla(page);
    await seleccionarFilas(page, 3);

    const idsEsperados = botonesEsperados([ESTADOFILA.VALIDADA]);
    const resultados = await verificarBotones(page, BOTONES_FILA, idsEsperados);
    for (const resultado of resultados) console.log(formatearDiagnostico(resultado));

    const presentesEsperados = resultados.filter((r) => r.presente && r.esperado);
    expect(presentesEsperados.length, "no se dibujó ningún botón para comparar idioma").toBeGreaterThan(0);

    const enIdiomaEquivocado = [];
    for (const r of presentesEsperados) {
      const boton = BOTONES_FILA.find((b) => b.id === r.id);
      const etiquetaEsperada = boton.etiqueta[IDIOMA_ESPERADO];
      const etiquetaOtra = boton.etiqueta[IDIOMA_OTRO];
      const empiezaConEsperada = r.etiqueta && r.etiqueta.startsWith(etiquetaEsperada);
      const empiezaConLaOtra = r.etiqueta && r.etiqueta.startsWith(etiquetaOtra);
      console.log(
        `[A-13] ${r.id}: texto real="${r.etiqueta}" · esperado(${IDIOMA_ESPERADO})="${etiquetaEsperada}" ` +
          `· coincide=${empiezaConEsperada}`
      );
      if (!empiezaConEsperada || empiezaConLaOtra) {
        enIdiomaEquivocado.push({ id: r.id, texto: r.etiqueta, esperaba: etiquetaEsperada });
      }
    }
    expect(
      enIdiomaEquivocado,
      `Botones que NO están en el idioma esperado (${IDIOMA_ESPERADO}): ${JSON.stringify(enIdiomaEquivocado)}`
    ).toHaveLength(0);
  });

  test("el mensaje de confirmación del JS cambia de idioma", async ({ page }) => {
    await abrirApp(page);
    await irASitemap(page, SITEMAP.porDigitar);
    await esperarGrilla(page);
    await seleccionarFilas(page, 1);

    const boton = botonPorId("digitada");
    const diagnostico = await verificarBoton(page, boton);
    expect(diagnostico.presente && diagnostico.habilitado, "'Digitada' no está presente/habilitado: no se puede leer el mensaje de confirmación.").toBe(true);
    await diagnostico.locator.click();

    const dialogo = await esperarDialogo(page);
    const textoDialogo = (await dialogo.textContent()) || "";

    // "Digitada" queda SIEMPRE fijo (ver el hallazgo arriba): se arma la plantilla
    // con ese literal, no con una traducción — el caso prueba el comportamiento
    // REAL del código, no una expectativa idealizada.
    const esperado = armarTexto(TEXTOS_JS[IDIOMA_ESPERADO].confirmar, { accion: "Digitada", n: 1 });
    console.log(`[A-13] mensaje de confirmación (idioma ${IDIOMA_ESPERADO}): "${textoDialogo.trim()}"`);
    expect(
      textoDialogo,
      `El diálogo de confirmación no contiene el texto esperado en el idioma ${IDIOMA_ESPERADO}: ` +
        `se esperaba encontrar "${esperado}" dentro de "${textoDialogo.trim()}".`
    ).toContain(esperado);

    // Nunca se guarda nada: se cancela para dejar el fixture como estaba.
    await cancelarDialogo(dialogo);
    await esperarQueSeCierre(dialogo);
  });
});
