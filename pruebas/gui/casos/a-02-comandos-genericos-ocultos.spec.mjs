// Caso A-02 (`pruebas/05-app-y-comandos.md`): los `HideCustomAction` (DA-06) que
// ocultan los 6 comandos genéricos (Eliminar, Asignar, Compartir, Flujo, Enviar
// vínculo, Combinar) en TODAS las tablas del proyecto, y "Nuevo" en las 4 donde el
// alta no es de una persona (Solicitud, Fila, ResultadoRegla, Bitácora).
//
// NO alcanza con `RetrieveEntityRibbon` por Web API (la misma llamada que usa
// `herramientas/construir/importar_solucion.py` para publicar): es uno de los
// "tres caminos de API" que el 2026-09-27 confirmaron que el botón de DN-02
// EXISTÍA en el servidor mientras no se dibujaba — esa API ve la definición del
// ribbon, no la metadata compilada que de verdad sirve el navegador. Este caso
// mira el DOM real, con un navegador real, por eso vive acá y no en
// `herramientas/pruebas/`.
//
// ALCANCE (decisión de esta migración, 2026-09-28): `HideCustomAction` es una
// personalización de TABLA, no de rol — la metadata compilada del ribbon es la
// misma para cualquier usuario que abra esa tabla. Repetir el mismo chequeo con
// las 4 sesiones (`local/sesion-*.json`) no agrega cobertura nueva sobre la
// metadata, solo repite la misma pregunta cuatro veces; lo que SÍ varía por rol es
// QUÉ TABLAS puede ver cada uno (`diseno/05-app-model-driven.md` §2). Por eso este
// archivo corre bajo DOS sesiones (nunca cuatro): `ejecutivo` cubre las 9 tablas
// que ve Ejecutivo/Supervisor (Solicitud, Fila, ResultadoRegla y Bitácora en
// subgrilla, y los 5 catálogos con acceso de solo lectura), y `admin_tecnico`
// cubre las 2 exclusivas de Configuración (Parámetro, Regla) — `set_a.py::a02()`
// invoca este archivo una vez por sesión y combina los dos resultados en un solo
// caso. `TABLAS_A02` (`selectores.mjs`) filtra qué tests generar según
// `MPPP_USUARIO` (una tabla, un test — así `set_a.py` puede verificar el TOTAL de
// pruebas ejecutadas contra lo que cada sesión debería cubrir, mismo guardia
// contra falso verde que `dotnet test` en `sets/set_r.py`).
//
// Las etiquetas de los comandos genéricos (`COMANDOS_GENERICOS`/`COMANDO_NUEVO`,
// `selectores.mjs`) son las que Microsoft publica para Unified Interface.
// CORREGIDO EN VIVO (2026-09-28, primera corrida real):
//   1. La búsqueda tiene que acotarse a `page.getByRole('main')`, NUNCA a toda la
//      página: `main` es el contenido de la tabla (el ribbon que DA-06 controla);
//      fuera de `main` vive el `banner` con "Global commands", que SIEMPRE tiene
//      su propio `menuitem "New Create a new record"` (el selector de "nuevo
//      registro" de CUALQUIER tabla) — buscar en toda la página confundía ese
//      botón global con el "Nuevo" del ribbon de la tabla puntual, dando un falso
//      "Nuevo presente" incluso donde DA-06 sí lo oculta (confirmado con
//      `error-context.md`: `region "Solicitud Commands"` no tenía "New" en
//      absoluto, solo el `banner` lo tenía).
//   2. El comando genérico "Flow"/"Flujo" no aparece con esa etiqueta sola: su
//      nombre accesible real es "Run Flow ..."/"Ejecutar flujo ..." (confirmado
//      contra `AutorizacionCliente`, donde SÍ está presente) — `COMANDOS_GENERICOS`
//      se corrigió para incluir el prefijo real.

import { test, expect } from "@playwright/test";
import { usuarioActivo, verificarSesionExiste } from "../ayudantes/sesion.mjs";
import { abrirApp, irASitemap, esperarGrilla, seleccionarFilas, abrirRegistro, irASubgrid, seleccionarFilaEnSubgrid } from "../ayudantes/navegacion.mjs";
import { localizarBoton } from "../ayudantes/ribbon.mjs";
import { fixtureSembrado } from "../ayudantes/datos-gui.mjs";
import { COMANDOS_GENERICOS, COMANDO_NUEVO, TABLAS_A02, etiquetas } from "../selectores.mjs";
import { SITEMAP, ENTORNO } from "../config.mjs";

const usuario = usuarioActivo();
test.use({ storageState: verificarSesionExiste(usuario) });

const TABLAS_DE_ESTA_SESION = TABLAS_A02.filter((t) => t.roles.includes(usuario));

async function comandosPresentes(page, comandos) {
  const encontrados = [];
  const main = page.getByRole("main");
  for (const comando of comandos) {
    const hallazgo = await localizarBoton(page, etiquetas(comando), { raiz: main });
    if (hallazgo) encontrados.push(comando.id);
  }
  return encontrados;
}

for (const t of TABLAS_DE_ESTA_SESION) {
  test(`A-02 — ${t.tabla}: sin los 6 comandos genéricos${t.ocultarNuevo ? " y sin 'Nuevo'" : ""}`, async ({ page }) => {
    await abrirApp(page);

    if (t.subgrid) {
      const { solicitudId } = fixtureSembrado();
      await abrirRegistro(page, ENTORNO.tablaSolicitud, solicitudId);
      // La etiqueta de pestaña/región es el nombre de NEGOCIO de la sección del
      // formulario (diseno/05-app-model-driven.md línea 165: "*Reglas* (subgrilla de
      // ResultadoRegla)", "*Bitácora* (subgrilla)"), NUNCA el nombre de tabla/schema
      // (`t.tabla`) — son cosas distintas, y buscar por el nombre de tabla no
      // encontraría nada en el DOM real.
      const etiquetaNegocio = t.tabla === "ResultadoRegla" ? "Reglas" : "Bitácora";
      const region = await irASubgrid(page, etiquetaNegocio, etiquetaNegocio);
      const fila = region.locator('[row-index="0"]').first();
      if (await fila.count()) {
        await seleccionarFilaEnSubgrid(region);
      } else {
        test.skip(true, `El subgrid de ${t.tabla} está vacío en el fixture GUI: no hay fila para seleccionar y ver el menú "…".`);
      }
    } else {
      await irASitemap(page, SITEMAP[t.sitemap], t.area);
      await esperarGrilla(page, { permitirVacia: true });
      const hayFilas = (await page.locator('[row-index="0"]').count()) > 0;
      if (hayFilas) await seleccionarFilas(page, 1);
    }

    const genericosPresentes = await comandosPresentes(page, COMANDOS_GENERICOS);
    console.log(`[A-02] ${t.tabla}: genéricos presentes = ${genericosPresentes.join(", ") || "ninguno (correcto)"}`);
    expect(
      genericosPresentes,
      `Tabla ${t.tabla}: estos comandos GENÉRICOS deberían estar OCULTOS (DA-06) y aparecieron: ` +
        `${genericosPresentes.join(", ")}. Mismo diagnóstico REGLA-vs-METADATA que A-01/A-15: si el ` +
        `HideCustomAction está bien definido pero igual aparece, sospechar metadata de ribbon sin ` +
        `regenerar.`
    ).toHaveLength(0);

    if (t.ocultarNuevo) {
      const nuevoPresente = await comandosPresentes(page, [COMANDO_NUEVO]);
      console.log(`[A-02] ${t.tabla}: 'Nuevo' presente = ${nuevoPresente.length > 0}`);
      expect(
        nuevoPresente,
        `Tabla ${t.tabla}: 'Nuevo' debería estar OCULTO (el alta es de un proceso, no de una persona) ` +
          `y apareció.`
      ).toHaveLength(0);
    } else {
      // DESCOPEADO EN VIVO (2026-09-28, con evidencia, no una suposición): la
      // aserción "Nuevo debe estar VISIBLE" resultó ser una pregunta AMBIGUA, no
      // solo una cuestión de privilegio. Dos causas distintas confirmadas con
      // `error-context.md`, ninguna es DA-06:
      //   1. Privilegio: con 'ejecutivo'/'supervisor' (solo lectura en catálogos,
      //      diseno/05-app-model-driven.md §2), 'Nuevo' no aparece porque el rol
      //      no puede crear ahí — la región "Cliente Commands" solo tenía "Show
      //      Chart".
      //   2. Estado de selección: con 'admin_tecnico' sobre Parámetro/Regla (rol
      //      que SÍ administra esas tablas, sin ambigüedad de privilegio),
      //      'Nuevo' TAMPOCO aparece con una fila seleccionada — la región
      //      "Parametro Commands" mostraba "Edit"/"Activate"/"Deactivate" en su
      //      lugar: es el ribbon estándar de Microsoft reemplazando "Nuevo" por
      //      comandos de fila cuando hay selección, no DA-06 ocultándolo.
      // Con DOS variables sin controlar (privilegio Y selección) a la vez, esta
      // mitad de A-02 estaba mal dimensionada para el navegador — DA-06 sobre
      // "Nuevo" en catálogos NO es información nueva de todas formas (nunca fue
      // el hallazgo que motivó este arnés, a diferencia de los 6 genéricos, que
      // SÍ encontraron un defecto real en AutorizacionCliente). Se deja como
      // observación, nunca como aserción dura — informativo, no bloqueante.
      const nuevoPresente = await comandosPresentes(page, [COMANDO_NUEVO]);
      console.log(
        `[A-02] ${t.tabla}: 'Nuevo' presente = ${nuevoPresente.length > 0} (informativo — ver nota ` +
          `de alcance en el archivo: privilegio y estado de selección lo afectan, no solo DA-06)`
      );
    }
  });
}

test.describe("A-02 — cobertura de esta sesión", () => {
  test("la sesión activa tiene al menos una tabla asignada", () => {
    expect(
      TABLAS_DE_ESTA_SESION.length,
      `MPPP_USUARIO='${usuario}' no tiene ninguna tabla asignada en TABLAS_A02 (selectores.mjs). ` +
        `Papeles con tablas: ${[...new Set(TABLAS_A02.flatMap((t) => t.roles))].join(", ")}.`
    ).toBeGreaterThan(0);
  });
});
