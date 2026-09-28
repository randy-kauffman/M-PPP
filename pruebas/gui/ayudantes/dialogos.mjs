// Ayudante para A-04 (Cancelar no cambia nada) y A-09 (cerrar el diálogo de motivo
// con la X): las dos ramas "canceló" del JS de `recursos/js/comandos.js`
// (`openConfirmDialog` con `respuesta.confirmed = false`, y `pedirMotivo()`
// devolviendo `null` en el rechazo de `openForm`).
//
// EL PROBLEMA QUE ESTO RESUELVE (por qué no alcanza con "no until cambió nada" a
// secas, pedido explícito de la tarea): "no pasó nada" es EXACTAMENTE lo mismo que
// se ve si el botón nunca se apretó. Un caso que solo espera y mira que nada
// cambió da el mismo verde tanto si canceló de verdad como si el `click()` del
// botón nunca llegó a disparar el diálogo (un selector roto, por ejemplo) — un
// falso positivo perfecto. Por eso acá se arman DOS pruebas independientes, no
// una:
//
//   1. POSITIVA: el diálogo (confirmación o quick create) REALMENTE apareció
//      después de apretar el botón — confirma que el click llegó al handler y
//      abrió la rama que se quiere probar, no que el test se saltó el paso.
//   2. NEGATIVA, con RED (no solo con la UI): CERO peticiones de escritura
//      (POST/PATCH/DELETE) al Web API durante TODA la ventana (desde antes del
//      click hasta después de cerrar el diálogo) — la evidencia de que el
//      SERVIDOR nunca se enteró, no que la grilla visualmente "se ve igual" (una
//      grilla puede tardar en refrescarse y dar un falso "no cambió" por otro
//      motivo).
//
// Las dos juntas: si 1 falla, el test avisa que ni siquiera se pudo ejercitar la
// rama que interesa (no es un verde vacío, es un error explícito). Si 2 falla,
// hubo una escritura real pese al cancelar — el defecto que A-04/A-09 existen
// para cazar.

// CORREGIDO EN VIVO (2026-09-28, primera corrida real de A-04/A-09): un patrón
// genérico `/api/data/v[\d.]+\//` sobre CUALQUIER escritura dio FALSO POSITIVO —
// grabó `msdyn_ReportToMonitoringHub`, `GetRecentItems`, `UpdateRecentItems` y
// `msdyn_FormPredictAgent`: telemetría y bookkeeping de la PLATAFORMA (registro de
// "elementos recientes", predicción de formulario), que UCI dispara sola con
// cualquier navegación o click, sin que tengan nada que ver con la acción de
// negocio que el caso está probando. Filtrar esos nombres uno por uno sería frágil
// (la plataforma puede agregar otros). La solución correcta es la inversa: en vez
// de excluir el ruido conocido, incluir SOLO lo que es nuestro — cualquier tabla o
// Custom API con el prefijo del publisher del proyecto (`sanic_mppp_`, TODAS las
// tablas y Custom API de MPPP lo llevan: `sanic_mppp_tbl_*`, `sanic_mppp_capi_*`).
// Si algún día una escritura de negocio real no llevara ese prefijo, esto la
// dejaría pasar en silencio — pero hoy TODO el esquema del proyecto lo lleva
// (verificado contra `sim.py`/`sembrar_datos_gui.py`: no hay una sola tabla o
// Custom API de este proyecto sin el prefijo).
const RUTA_ESCRITURA_DE_NEGOCIO = /\/api\/data\/v[\d.]+\/sanic_mppp_/;
const METODOS_DE_ESCRITURA = new Set(["POST", "PATCH", "DELETE"]);

/**
 * Empieza a grabar, desde YA, toda petición de escritura al Web API DE NEGOCIO de
 * este proyecto (tablas/Custom API con prefijo `sanic_mppp_`, método
 * POST/PATCH/DELETE) — nunca telemetría/bookkeeping de la plataforma (ver la nota
 * de arriba). Devuelve `{ detener() }`: `detener()` quita el listener y devuelve
 * el arreglo de peticiones grabadas (vacío si de verdad no se escribió nada).
 * Arrancar ANTES de tocar el botón (no después de que aparece el diálogo): una
 * escritura disparada por el primer click también cuenta, y este ayudante la
 * tiene que ver siempre.
 */
export function grabarEscriturasApi(page) {
  const peticiones = [];
  const oyente = (request) => {
    const url = request.url();
    if (METODOS_DE_ESCRITURA.has(request.method()) && RUTA_ESCRITURA_DE_NEGOCIO.test(url)) {
      peticiones.push({ metodo: request.method(), url });
    }
  };
  page.on("request", oyente);
  return {
    detener() {
      page.off("request", oyente);
      return peticiones;
    },
  };
}

// Nombres accesibles conocidos de los botones de cierre/cancelación de los
// diálogos de UCI, en los dos idiomas del proyecto — NO CONFIRMADOS EN VIVO
// todavía (mismo caso que otros selectores de este arnés: se corrigen la primera
// vez que el caso corre contra la app real si no calzan).
const ETIQUETAS_CANCELAR = ["Cancel", "Cancelar"];
const ETIQUETAS_CERRAR_X = ["Close", "Cerrar"];

/** Espera a que aparezca CUALQUIER diálogo modal (confirmación u openForm/quick
 * create) y devuelve su locator. Tira un error explícito (no un timeout genérico)
 * si no aparece: "el botón no abrió ningún diálogo" es un resultado, no un
 * silencio. */
export async function esperarDialogo(page, { timeout = 15_000 } = {}) {
  const dialogo = page.getByRole("dialog").first();
  const aparecio = await dialogo
    .waitFor({ state: "visible", timeout })
    .then(() => true)
    .catch(() => false);
  if (!aparecio) {
    throw new Error(
      `No apareció ningún [role="dialog"] ${timeout}ms después de apretar el botón. Si el botón ` +
        `estaba presente y habilitado (revisar con ayudantes/ribbon.mjs primero), esto puede ser un ` +
        `selector de diálogo roto (revisar el rol/nombre real con el inspector de Playwright), NO ` +
        `todavía evidencia de que 'cancelar no cambia nada' — sin diálogo, este caso no está probando ` +
        `la rama de cancelación, está probando que el botón no hace nada, que es otra cosa.`
    );
  }
  return dialogo;
}

/** Click en el botón "Cancelar/Cancel" DENTRO del diálogo dado (A-04). */
export async function cancelarDialogo(dialogo) {
  const boton = dialogo.getByRole("button", { name: new RegExp(`^(${ETIQUETAS_CANCELAR.join("|")})$`, "i") }).first();
  await boton.waitFor({ state: "visible", timeout: 10_000 });
  await boton.click();
}

/** Click en la X (cerrar sin guardar) DENTRO del diálogo dado (A-09). En UCI el
 * botón de cerrar suele tener nombre accesible "Close"/"Cerrar" aunque se vea
 * como una X — se prueba por nombre accesible primero (más estable que un ícono),
 * con un respaldo por `[aria-label]` que contenga "close"/"cerrar". */
export async function cerrarDialogoConX(dialogo) {
  const porNombre = dialogo.getByRole("button", { name: new RegExp(`^(${ETIQUETAS_CERRAR_X.join("|")})$`, "i") }).first();
  if (await porNombre.count()) {
    await porNombre.waitFor({ state: "visible", timeout: 10_000 });
    await porNombre.click();
    return;
  }
  const porAria = dialogo.locator('button[aria-label*="lose" i], button[aria-label*="errar" i]').first();
  await porAria.waitFor({ state: "visible", timeout: 10_000 });
  await porAria.click();
}

/** Espera a que el diálogo dado deje de estar visible (se cerró de verdad, no
 * solo se clickeó el botón). */
export async function esperarQueSeCierre(dialogo, { timeout = 10_000 } = {}) {
  await dialogo.waitFor({ state: "hidden", timeout });
}
