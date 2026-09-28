// Ayudante para verificar botones del command bar: distingue "ausente" de
// "presente pero deshabilitado", que son diagnosticos OPUESTOS (el primero es
// casi siempre metadata de ribbon rota; el segundo puede ser un EnableRule
// funcionando bien con la seleccion incorrecta).

import { etiquetas } from "../selectores.mjs";

// Textos conocidos del boton "mas comandos" (overflow) en los dos idiomas del
// proyecto. Si un boton no aparece en la barra visible, puede estar colapsado
// ahi por falta de espacio: hay que abrirlo antes de decidir "ausente".
const ETIQUETAS_MAS_COMANDOS = ["More commands", "Más comandos", "Mas comandos"];

function escaparRegex(texto) {
  return texto.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

/**
 * Une las etiquetas posibles (1033/3082) en UN solo patrón, para una sola
 * espera en vez de una por idioma (ver nota de rendimiento más abajo).
 *
 * SIN anclar el final (`$`) a propósito: el nombre accesible real de estos
 * "menuitem" es la etiqueta SEGUIDA del tooltip completo, pegados sin
 * separador — confirmado en vivo (2026-09-27) leyendo el `error-context.md`
 * de una corrida: `"Mark as entered Marks the selected rows as entered in
 * AS400."`. Un patrón `^(...)$ ` exigía calzar el tooltip entero (frágil y
 * no es lo que se quiere probar); "empieza con la etiqueta" alcanza y no
 * colisiona entre los 7 comandos (ninguna etiqueta es prefijo literal de
 * otra, revisado a mano).
 */
function patronDeEtiquetas(etiquetasPosibles) {
  return new RegExp(`^(${etiquetasPosibles.map(escaparRegex).join("|")})`);
}

/**
 * Busca un boton probando TODAS sus etiquetas conocidas (1033 y 3082) A LA
 * VEZ, con un patrón combinado: no se puede asumir el idioma de la sesión
 * activa (confirmado en vivo: una sesión de prueba real tenía la UI en
 * inglés). Devuelve el locator y CON QUE etiqueta lo encontró, o `null` si no
 * apareció con ninguna.
 */
export async function localizarBoton(page, etiquetasPosibles) {
  // IMPORTANTE (aprendido en vivo, 2026-09-27, tres rondas — cada una con
  // evidencia real, no supuesta):
  //  1) El ROL de accesibilidad real de estos comandos es "menuitem" dentro
  //     de un "menubar" (`region "Fila Commands" > menubar > menuitem "Mark
  //     as entered..."`), confirmado leyendo el `error-context.md` (dump de
  //     accesibilidad) que Playwright adjunta a una corrida fallida. NO es
  //     "button": ese fue el motivo real de los primeros falsos "AUSENTE",
  //     no una condición de carrera. `getByRole('button', ...)` nunca iba a
  //     encontrar nada acá, sin importar cuánto se esperara.
  //  2) Aun así, tras seleccionar filas el command bar tarda un instante en
  //     re-renderizarse: un `count()` instantáneo (sin auto-espera) puede
  //     correr ANTES de ese re-render. Por eso se usa `waitFor`, no `count()`.
  //  3) Esperar CADA etiqueta por separado (una espera de N segundos para
  //     1033, y otra para 3082) hace que un botón GENUINAMENTE ausente tarde
  //     2×N en descartarse, y con 7 botones eso alcanza a superar el timeout
  //     del test. Por eso se prueban las dos etiquetas EN UN SOLO patrón
  //     (regex `1033|3082`): una sola espera acotada, no dos.
  const patron = patronDeEtiquetas(etiquetasPosibles);
  const enBarra = page.getByRole("menuitem", { name: patron }).first();
  const aparecio = await enBarra
    .waitFor({ state: "visible", timeout: 6_000 })
    .then(() => true)
    .catch(() => false);
  if (aparecio) {
    const texto = (await enBarra.textContent().catch(() => null)) || etiquetasPosibles[0];
    return { locator: enBarra, etiqueta: texto.trim() };
  }

  // No esta a la vista con ninguna etiqueta: puede estar colapsado en "mas
  // comandos" (ESE disparador sí es un "button" real, distinto de los items).
  for (const nombreOverflow of ETIQUETAS_MAS_COMANDOS) {
    const overflow = page.getByRole("button", { name: nombreOverflow, exact: false });
    if ((await overflow.count()) > 0) {
      await overflow.first().click();
      const enMenu = page.getByRole("menuitem", { name: patron }).first();
      const apareceEnMenu = await enMenu
        .waitFor({ state: "visible", timeout: 2_000 })
        .then(() => true)
        .catch(() => false);
      if (apareceEnMenu) {
        const texto = (await enMenu.textContent().catch(() => null)) || etiquetasPosibles[0];
        return { locator: enMenu, etiqueta: texto.trim() };
      }
      // Cerrar el menu si no era ahi, para no ensuciar el siguiente intento.
      await page.keyboard.press("Escape").catch(() => {});
    }
  }

  return null;
}

/**
 * Verifica UN boton de la barra por su definicion en selectores.mjs, probando
 * sus dos etiquetas posibles.
 *
 * @param {boolean} esperado si `false`, la ausencia de este boton es el
 *   resultado CORRECTO para la seleccion actual (ver `botonesEsperados` en
 *   selectores.mjs) y así lo dice el diagnóstico — no es sospecha de bug.
 * @returns {Promise<{id: string, etiqueta: string|null, presente: boolean, habilitado: boolean|null, esperado: boolean, locator: import('@playwright/test').Locator|null}>}
 *   `locator` viene resuelto (rol + etiqueta ya encontrados): quien llama
 *   puede interactuar directamente (`.click()`) sin volver a buscarlo por su
 *   cuenta — evita repetir, y posiblemente errar, el rol/etiqueta correctos.
 */
export async function verificarBoton(page, boton, { esperado = true } = {}) {
  const hallazgo = await localizarBoton(page, etiquetas(boton));

  if (!hallazgo) {
    return { id: boton.id, etiqueta: null, presente: false, habilitado: null, esperado, locator: null };
  }

  // UCI casi nunca usa el atributo HTML `disabled` en estos items (son
  // "menuitem" de Fluent UI, no <button> nativos): la señal real es
  // `aria-disabled="true"`. Se comprueban las dos por robustez.
  const ariaDisabled = await hallazgo.locator.getAttribute("aria-disabled");
  const nativeDisabled = await hallazgo.locator.isDisabled().catch(() => false);
  const habilitado = ariaDisabled !== "true" && !nativeDisabled;

  return { id: boton.id, etiqueta: hallazgo.etiqueta, presente: true, habilitado, esperado, locator: hallazgo.locator };
}

/**
 * Verifica una lista de botones. `idsEsperados` son los ids que DEBERÍAN
 * estar presentes+habilitados dado lo seleccionado (típicamente el resultado
 * de `botonesEsperados()` en selectores.mjs); el resto se verifica igual,
 * pero su ausencia no se reporta como sospechosa.
 */
export async function verificarBotones(page, botones, idsEsperados) {
  const resultados = [];
  for (const boton of botones) {
    resultados.push(await verificarBoton(page, boton, { esperado: idsEsperados.includes(boton.id) }));
  }
  return resultados;
}

export function formatearDiagnostico(resultado) {
  const etiquetaTexto = resultado.etiqueta ? `("${resultado.etiqueta}")` : "";
  if (!resultado.presente) {
    if (!resultado.esperado) {
      return `${resultado.id}: ausente, y es lo ESPERADO (ninguna fila seleccionada admite este comando — ver TRANSICIONES_VALIDAS en selectores.mjs).`;
    }
    return `${resultado.id}: AUSENTE — no se dibujó, y SÍ se esperaba. Sospechar metadata de ribbon (regenerar desde Command Checker) antes que el EnableRule.`;
  }
  if (!resultado.habilitado) {
    return `${resultado.id} ${etiquetaTexto}: presente pero DESHABILITADO — revisar el EnableRule contra la selección actual, la metadata está bien.`;
  }
  if (!resultado.esperado) {
    return `${resultado.id} ${etiquetaTexto}: presente y habilitado, pero NO se lo esperaba con esta selección — revisar TRANSICIONES_VALIDAS, puede estar desactualizado.`;
  }
  return `${resultado.id} ${etiquetaTexto}: presente y habilitado.`;
}
