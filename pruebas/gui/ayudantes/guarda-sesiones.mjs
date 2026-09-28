// La GUARDA DE SESIONES, antes de correr nada (requisito duro del proyecto).
// Reusa `herramientas/pruebas/estado_sesiones.py::verificar_o_fallar` TAL
// CUAL — no reimplementa la lógica de "¿está viva? ¿el rol es el que toca?"
// acá: ese archivo ya resuelve WhoAmI contra la sesión, resuelve el rol con
// el service principal, y compara contra `ROL_ESPERADO` de
// `comportamiento_roles.py`. Este archivo solo lo invoca desde Node, como
// subproceso, con el mismo espíritu que `ayudantes/datos-gui.mjs` invoca
// `sembrar_datos_gui.py`: un solo camino para la lógica de negocio (Python,
// service principal), Node solo la dispara y traduce el resultado.
//
// Por qué esto importa (motivo citado en `estado_sesiones.py`): una prueba
// que pasa porque el usuario NO VE un botón es indistinguible de una que
// pasa porque la sesión está MUERTA. Las dos dan verde, y una está
// mintiendo. Por eso el arnés revienta ACÁ, antes de sembrar un solo dato o
// abrir un solo caso, si la sesión que va a usar (`MPPP_USUARIO`) no está
// viva o su usuario no tiene el rol esperado — nunca sigue de largo con un
// warning.
//
// Se le pasa SOLO el papel que esta corrida va a usar (no las cuatro
// sesiones): no tiene sentido bloquear una corrida de 'ejecutivo' porque la
// sesión de 'admin_tecnico' venció, si esta corrida nunca la va a tocar.

import { execFile } from "node:child_process";
import { promisify } from "node:util";
import { RAIZ_REPO } from "../config.mjs";

const execFileAsync = promisify(execFile);

// Código mínimo, sin escribir un .py nuevo (se llama con `python3 -c`):
// importa el módulo real y le pasa los papeles pedidos por argv. No imprime
// nada del contenido de la sesión — `verificar_o_fallar` tampoco lo hace.
const CODIGO_PYTHON = `
import sys
sys.path.insert(0, "herramientas/pruebas")
from estado_sesiones import verificar_o_fallar
verificar_o_fallar(sys.argv[1:])
`;

/**
 * Corre la guarda para los `papeles` dados (típicamente `[usuarioActivo()]`).
 * No devuelve nada si todo está bien. Si alguna sesión está muerta o el rol
 * no es el que toca, revienta con un `Error` cuyo mensaje ES el mensaje de
 * `estado_sesiones.py` (la causa exacta, no un timeout genérico).
 */
export async function verificarGuardaSesiones(papeles) {
  try {
    await execFileAsync("python3", ["-c", CODIGO_PYTHON, ...papeles], {
      cwd: RAIZ_REPO,
      timeout: 60_000,
    });
  } catch (error) {
    // `verificar_o_fallar` sale con `SystemExit(mensaje)`: Python imprime
    // ese mensaje en stderr al terminar el proceso con código != 0. Se
    // relanza tal cual — es la causa real, no debe perderse.
    const detalle = (error.stderr || error.stdout || error.message || "").trim();
    throw new Error(
      `GUARDA DE SESIONES: no se arranca nada.\n${detalle || "(sin detalle: ver salida de estado_sesiones.py)"}`
    );
  }
}
