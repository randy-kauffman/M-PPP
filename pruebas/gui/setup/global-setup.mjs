// globalSetup de Playwright: toma el semáforo compartido, siembra el fixture
// de datos, y devuelve la función de limpieza que Playwright corre al final
// (un solo lugar para las dos puntas, sin archivo de estado intermedio: ver
// la guía oficial de Playwright, "Global setup and teardown").
//
// Por qué la siembra vive acá y no en cada caso: "una sola forma, no dos"
// (pedido explícito). Si cada `.spec.mjs` sembrara por su cuenta, dos casos
// corriendo en la misma sesión pisarían el fixture del otro. Acá se siembra
// UNA vez por corrida completa.
//
// Por qué el semáforo se toma ACÁ y se suelta en el teardown (no en cada
// caso ni en `sembrar_datos_gui.py`): esta corrida completa (siembra + los
// casos que interactúan con Dev + limpieza) es, para el entorno compartido,
// el equivalente de UN set de `correr.py` — y `correr.py` toma el semáforo
// una sola vez para todo el set, no una vez por caso. Tomarlo acá cubre toda
// la ventana en la que este arnés toca Dev. Si la siembra falla a mitad de
// camino, el semáforo se suelta antes de relanzar el error (si no, quedaría
// trabado hasta que alguien lo note).
//
// `MPPP_CONSERVAR_DATOS=1` salta la limpieza al final (para inspeccionar el
// fixture a mano después de una corrida) — nunca lo activa el arnés solo.
//
// `MPPP_SEMAFORO_HEREDADO=1` — puesta por `herramientas/pruebas/sets/set_a.py` y
// `set_e.py` cuando invocan este arnés como subproceso DESDE un caso que ya corre
// dentro de `correr.py`: `correr.py` toma `semaforo.py` para TODA la corrida
// (`with semaforo.tomar(...)`, ver `herramientas/pruebas/correr.py`), así que para
// cuando un caso de `set_a.py`/`set_e.py` llega a invocar `npx playwright test`, el
// semáforo YA es de ese mismo proceso Python (su PID sigue vivo). Sin este bypass,
// `tomarSemaforo()` de acá abajo vería el candado ocupado por un PID vivo — el
// SUYO PROPIO, un nivel más arriba — y reventaría SIEMPRE con "SEMÁFORO EN ROJO",
// un falso FALLA en cada corrida conectada, nunca una contención real. Con la
// bandera puesta, este `globalSetup` NO toma ni suelta el semáforo (ya es del
// proceso padre) pero sí corre la guarda de sesiones, la siembra y la limpieza
// igual que siempre — sigue siendo la misma ventana de acceso a Dev, solo que el
// candado que la protege es el de más afuera. Corriendo `npx playwright test` de
// forma DIRECTA (sin pasar por `correr.py`) esta variable no está seteada, y el
// comportamiento es exactamente el de siempre (toma y suelta acá).

import { request } from "@playwright/test";
import { usuarioActivo, verificarSesionExiste } from "../ayudantes/sesion.mjs";
import { usuarioDeLaSesion, sembrarDatosGui, limpiarDatosGui } from "../ayudantes/datos-gui.mjs";
import { tomar as tomarSemaforo } from "../ayudantes/semaforo.mjs";
import { verificarGuardaSesiones } from "../ayudantes/guarda-sesiones.mjs";
import { ENTORNO } from "../config.mjs";

export default async function globalSetup() {
  const usuario = usuarioActivo();
  const rutaSesion = verificarSesionExiste(usuario);

  // LA GUARDA, antes de correr nada (requisito duro): ¿la sesión de
  // `usuario` está viva y su usuario tiene el rol esperado? Revienta acá,
  // ANTES del semáforo y de sembrar un solo dato, si no — nunca sigue de
  // largo con un caso que podría estar "pasando" solo porque la sesión
  // murió (ver el docstring de estado_sesiones.py).
  console.log(`\n[setup] Guarda de sesiones: verificando '${usuario}' (viva + rol esperado)...`);
  await verificarGuardaSesiones([usuario]);
  console.log(`[setup] Guarda de sesiones OK para '${usuario}'.`);

  const semaforoHeredado = process.env.MPPP_SEMAFORO_HEREDADO === "1";
  let soltarSemaforo = () => {};
  if (semaforoHeredado) {
    console.log("[setup] MPPP_SEMAFORO_HEREDADO=1: el semáforo ya es del proceso que nos invocó, no se toma de nuevo.");
  } else {
    console.log(`[setup] Tomando el semáforo compartido (local/semaforo-pruebas.json)...`);
    soltarSemaforo = tomarSemaforo(`pruebas/gui (globalSetup, usuario=${usuario})`);
  }

  try {
    console.log(`[setup] Sembrando datos GUI para la sesión de '${usuario}'...`);

    const contextoApi = await request.newContext({ storageState: rutaSesion, baseURL: ENTORNO.url });
    let propietario;
    try {
      propietario = await usuarioDeLaSesion(contextoApi, ENTORNO.url);
    } finally {
      await contextoApi.dispose();
    }
    console.log(`[setup] WhoAmI de la sesión '${usuario}': ${propietario}`);

    const resultado = await sembrarDatosGui(propietario);
    console.log(`[setup] sembrar_datos_gui.py -> ${resultado.estado}: ${JSON.stringify(resultado.detalle)}`);

    // Publicado por env var para que los casos (otro proceso de Node, un worker de
    // Playwright) puedan navegar DIRECTO a los registros que esta misma corrida
    // sembró (por su GUID propio, nunca "el primer resultado de una vista
    // compartida" — con varios agentes tocando Dev en paralelo esa vista puede
    // tener filas de cualquiera). `process.env` mutado acá SÍ llega a los workers:
    // Playwright los crea después de que `globalSetup` termina, heredando el
    // `process.env` de este mismo proceso (ver la guía oficial de Playwright,
    // "Global setup and teardown" — es el mecanismo documentado para pasar datos
    // de `globalSetup` a los tests, junto con o en vez de un archivo).
    if (resultado?.detalle?.solicitudId) {
      process.env.MPPP_GUI_SOLICITUD_ID = String(resultado.detalle.solicitudId);
    }
    if (Array.isArray(resultado?.detalle?.filaIds)) {
      process.env.MPPP_GUI_FILA_IDS = JSON.stringify(resultado.detalle.filaIds);
    }
  } catch (error) {
    // El semáforo es nuestro desde `tomarSemaforo()` de arriba (salvo que lo
    // heredamos, en cuyo caso no es nuestro para soltar): si la siembra revienta,
    // soltarlo antes de relanzar. Si no, queda trabado hasta que alguien note el
    // candado y lo libere a mano sin saber por qué.
    soltarSemaforo();
    throw error;
  }

  return async function globalTeardown() {
    try {
      if (process.env.MPPP_CONSERVAR_DATOS === "1") {
        console.log("[teardown] MPPP_CONSERVAR_DATOS=1: no se limpia el fixture.");
      } else {
        console.log("[teardown] Limpiando datos GUI...");
        const limpieza = await limpiarDatosGui();
        console.log(
          `[teardown] sembrar_datos_gui.py --limpiar -> ${limpieza.estado}: ${JSON.stringify(limpieza.detalle)}`
        );
      }
    } finally {
      if (semaforoHeredado) {
        console.log("[teardown] Semáforo heredado: no se libera acá (lo libera quien lo tomó).");
      } else {
        soltarSemaforo();
        console.log("[teardown] Semáforo liberado.");
      }
    }
  };
}
