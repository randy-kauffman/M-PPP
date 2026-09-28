// Caso A-09 (`pruebas/05-app-y-comandos.md`): cerrar el diálogo de motivo con la
// X (sin guardar) no cambia nada — `pedirMotivo()` devuelve `null` en el rechazo
// de `openForm`/`useQuickCreateForm`, y `ejecutarConMotivo` nunca llama a
// `aplicar()`.
//
// Mismo motivo que A-04, mismo mecanismo de evidencia (ver el comentario largo en
// `ayudantes/dialogos.mjs`): "no pasó nada" es indistinguible de "el botón nunca
// se apretó", así que este caso también arma DOS evidencias independientes — que
// el diálogo de motivo (`sanic_mppp_tbl_motivoaccion` en modo creación rápida)
// REALMENTE apareció, y que CERO peticiones de escritura (ni el `POST` del
// `MotivoAccion` transporte, ni el `PATCH` de la Fila) se dispararon durante toda
// la ventana, desde antes del primer click hasta después de cerrar con la X.
//
// Se usa "Anular" (no "Rechazada en AS400", A-08) porque admite Validada directo
// (`TRANSICIONES_VALIDAS.anular`, selectores.mjs) sin precondición extra.

import { test, expect } from "@playwright/test";
import { usuarioActivo, verificarSesionExiste } from "../ayudantes/sesion.mjs";
import { abrirApp, irASitemap, esperarGrilla, seleccionarFilas } from "../ayudantes/navegacion.mjs";
import { verificarBoton } from "../ayudantes/ribbon.mjs";
import { grabarEscriturasApi, esperarDialogo, cerrarDialogoConX, esperarQueSeCierre } from "../ayudantes/dialogos.mjs";
import { botonPorId } from "../selectores.mjs";
import { SITEMAP } from "../config.mjs";

const usuario = usuarioActivo();
test.use({ storageState: verificarSesionExiste(usuario) });

test.describe("A-09 — Cancelar el diálogo de motivo (la X, sin guardar) no cambia nada", () => {
  test("apretar 'Anular' y cerrar el diálogo de motivo con la X no dispara ninguna escritura al Web API", async ({
    page,
  }) => {
    await abrirApp(page);
    await irASitemap(page, SITEMAP.porDigitar);
    await esperarGrilla(page);
    await seleccionarFilas(page, 1);

    const grabacion = grabarEscriturasApi(page);

    const boton = botonPorId("anular");
    const diagnostico = await verificarBoton(page, boton);
    expect(diagnostico.presente, "El botón 'Anular' está AUSENTE: no se puede ejercitar este caso.").toBe(true);
    expect(diagnostico.habilitado, "El botón 'Anular' está deshabilitado con una fila Validada seleccionada.").toBe(true);

    await diagnostico.locator.click();

    // Evidencia POSITIVA: el diálogo de motivo (quick create de MotivoAccion)
    // tiene que aparecer de verdad.
    const dialogo = await esperarDialogo(page);
    await cerrarDialogoConX(dialogo);
    await esperarQueSeCierre(dialogo);

    await page.waitForTimeout(3_000);

    const escrituras = grabacion.detener();
    expect(
      escrituras,
      `Cerrar el diálogo de motivo con la X no debería disparar NINGUNA escritura al Web API (ni el ` +
        `POST del MotivoAccion transporte, ni el PATCH de la Fila), y se grabaron: ` +
        `${JSON.stringify(escrituras)}.`
    ).toHaveLength(0);
  });
});
