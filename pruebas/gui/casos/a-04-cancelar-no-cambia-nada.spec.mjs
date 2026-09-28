// Caso A-04 (`pruebas/05-app-y-comandos.md`): que el diálogo de confirmación
// (`openConfirmDialog`) realmente frene — cuando `respuesta.confirmed` es falso,
// `ejecutar()` NUNCA llama a `aplicar()`.
//
// CÓMO SE EVITA EL FALSO VERDE (pedido explícito de la tarea): "nada cambió" es
// EXACTAMENTE lo mismo que se ve si el botón nunca se apretó — un test que solo
// mirara "la fila sigue Validada" pasaría igual con un selector roto que nunca
// llegó a clickear nada. Este caso arma DOS evidencias independientes (ver el
// comentario largo en `ayudantes/dialogos.mjs`, `grabarEscriturasApi`):
//   1. POSITIVA: el diálogo de confirmación REALMENTE apareció después de
//      apretar "Digitada" (si no aparece, el test explota acá mismo, con un
//      error que dice que no se pudo ejercitar la rama de cancelación — nunca
//      sigue de largo como si "no pasó nada" fuera la prueba).
//   2. NEGATIVA con RED: CERO peticiones de escritura al Web API durante TODA la
//      ventana (desde antes del click hasta después de cerrar el diálogo) — la
//      prueba de que el SERVIDOR nunca se enteró, no solo que la grilla "se ve
//      igual" (que podría tardar en refrescarse y dar un falso "no cambió" por
//      otro motivo).

import { test, expect } from "@playwright/test";
import { usuarioActivo, verificarSesionExiste } from "../ayudantes/sesion.mjs";
import { abrirApp, irASitemap, esperarGrilla, seleccionarFilas } from "../ayudantes/navegacion.mjs";
import { verificarBoton } from "../ayudantes/ribbon.mjs";
import { grabarEscriturasApi, esperarDialogo, cancelarDialogo, esperarQueSeCierre } from "../ayudantes/dialogos.mjs";
import { botonPorId } from "../selectores.mjs";
import { SITEMAP } from "../config.mjs";

const usuario = usuarioActivo();
test.use({ storageState: verificarSesionExiste(usuario) });

test.describe("A-04 — Cancelar el diálogo de confirmación no cambia nada", () => {
  test("apretar 'Digitada' y Cancelar no dispara ninguna escritura al Web API", async ({ page }) => {
    await abrirApp(page);
    await irASitemap(page, SITEMAP.porDigitar);
    await esperarGrilla(page);
    await seleccionarFilas(page, 1);

    // Grabando DESDE ACÁ, antes del primer click: una escritura disparada por el
    // click inicial (antes de que aparezca el diálogo) también tiene que contar.
    const grabacion = grabarEscriturasApi(page);

    const boton = botonPorId("digitada");
    const diagnostico = await verificarBoton(page, boton);
    expect(diagnostico.presente, "El botón 'Digitada' está AUSENTE: no se puede ejercitar este caso.").toBe(true);
    expect(diagnostico.habilitado, "El botón 'Digitada' está deshabilitado con una fila Validada seleccionada.").toBe(true);

    await diagnostico.locator.click();

    // Evidencia POSITIVA: el diálogo tiene que aparecer de verdad.
    const dialogo = await esperarDialogo(page);
    await cancelarDialogo(dialogo);
    await esperarQueSeCierre(dialogo);

    // Colchón: darle tiempo a una escritura async tardía para que, si existiera,
    // ya haya llegado a la red antes de leer la grabación.
    await page.waitForTimeout(3_000);

    const escrituras = grabacion.detener();
    expect(
      escrituras,
      `Cancelar 'Digitada' no debería disparar NINGUNA escritura al Web API, y se grabaron: ` +
        `${JSON.stringify(escrituras)}. Esto sería el diálogo simulando frenar del lado del cliente ` +
        `mientras el servidor igual recibe el cambio — el defecto exacto que A-04 existe para cazar.`
    ).toHaveLength(0);
  });
});
