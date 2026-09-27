// Ayudante para verificar botones del command bar: distingue "ausente" de
// "presente pero deshabilitado", que son diagnosticos OPUESTOS (el primero es
// casi siempre metadata de ribbon rota; el segundo puede ser un EnableRule
// funcionando bien con la seleccion incorrecta).

import { etiquetaDe } from "../selectores.mjs";

// Textos conocidos del boton "mas comandos" (overflow) en los dos idiomas del
// proyecto. Si un boton no aparece en la barra visible, puede estar colapsado
// ahi por falta de espacio: hay que abrirlo antes de decidir "ausente".
const ETIQUETAS_MAS_COMANDOS = ["More commands", "Más comandos", "Mas comandos"];

/**
 * Busca un boton por su etiqueta visible dentro del command bar de la pagina
 * (no de un dialogo). Devuelve el locator, ya sea de la barra visible o del
 * menu de "mas comandos" si tuvo que abrirlo.
 */
export async function localizarBoton(page, etiqueta) {
  // Command bar principal (grilla). El rol semantico es "button": es lo mismo
  // que usa `getByRole` documentado por Microsoft para este escenario.
  const enBarra = page.getByRole("button", { name: etiqueta, exact: true });
  if ((await enBarra.count()) > 0 && (await enBarra.first().isVisible())) {
    return enBarra.first();
  }

  // No esta a la vista: puede estar colapsado en "mas comandos".
  for (const nombreOverflow of ETIQUETAS_MAS_COMANDOS) {
    const overflow = page.getByRole("button", { name: nombreOverflow, exact: false });
    if ((await overflow.count()) > 0) {
      await overflow.first().click();
      const enMenu = page.getByRole("menuitem", { name: etiqueta, exact: true });
      if ((await enMenu.count()) > 0) {
        return enMenu.first();
      }
      // Cerrar el menu si no era ahi, para no ensuciar el siguiente intento.
      await page.keyboard.press("Escape").catch(() => {});
    }
  }

  return null;
}

/**
 * Verifica UN boton de la barra por su definicion en selectores.mjs.
 *
 * @returns {Promise<{id: string, etiqueta: string, presente: boolean, habilitado: boolean|null}>}
 *   `habilitado` es `null` cuando el boton esta ausente (no aplica).
 */
export async function verificarBoton(page, boton, idioma) {
  const etiqueta = etiquetaDe(boton, idioma);
  const locator = await localizarBoton(page, etiqueta);

  if (!locator) {
    return { id: boton.id, etiqueta, presente: false, habilitado: null };
  }

  // UCI casi nunca usa el atributo HTML `disabled` en estos botones (son
  // <button>/<li role="menuitem"> de Fluent UI): la señal real es
  // `aria-disabled="true"`. Se comprueban las dos por robustez.
  const ariaDisabled = await locator.getAttribute("aria-disabled");
  const nativeDisabled = await locator.isDisabled().catch(() => false);
  const habilitado = ariaDisabled !== "true" && !nativeDisabled;

  return { id: boton.id, etiqueta, presente: true, habilitado };
}

/**
 * Verifica una lista de botones y devuelve un resumen legible para el reporte
 * de la prueba (nunca solo true/false: "ausente" y "deshabilitado" quedan
 * explicitos y separados).
 */
export async function verificarBotones(page, botones, idioma) {
  const resultados = [];
  for (const boton of botones) {
    resultados.push(await verificarBoton(page, boton, idioma));
  }
  return resultados;
}

export function formatearDiagnostico(resultado) {
  if (!resultado.presente) {
    return `${resultado.id} ("${resultado.etiqueta}"): AUSENTE — no se dibujo. Sospechar metadata de ribbon (regenerar desde Command Checker) antes que el EnableRule.`;
  }
  if (!resultado.habilitado) {
    return `${resultado.id} ("${resultado.etiqueta}"): presente pero DESHABILITADO — revisar el EnableRule contra la seleccion actual, la metadata esta bien.`;
  }
  return `${resultado.id} ("${resultado.etiqueta}"): presente y habilitado.`;
}
