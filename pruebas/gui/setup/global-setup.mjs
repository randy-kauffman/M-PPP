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

  console.log(`[setup] Tomando el semáforo compartido (local/semaforo-pruebas.json)...`);
  const soltarSemaforo = tomarSemaforo(`pruebas/gui (globalSetup, usuario=${usuario})`);

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
  } catch (error) {
    // El semáforo es nuestro desde `tomarSemaforo()` de arriba: si la
    // siembra revienta, soltarlo antes de relanzar. Si no, queda trabado
    // hasta que alguien note el candado y lo libere a mano sin saber por qué.
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
      soltarSemaforo();
      console.log("[teardown] Semáforo liberado.");
    }
  };
}
