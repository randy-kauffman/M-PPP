// DN-09 E (diseno/DIRECTRICES-NUEVAS.md): los botones de la barra de comandos se ven segun el ROL, ademas del estado.
// Pruebas del ARQUITECTO: el constructor no las cambia.
//
//     node --test "tests/js/*.test.mjs"
//
// Cargan `recursos/js/comandos.js` tal cual, en un contexto `vm` con un `Xrm` falso, y prueban las reglas del ribbon
// (las `EnableRule` / `CustomRule` que devuelven true para mostrar el boton).
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import vm from "node:vm";

const RUTA = fileURLToPath(new URL("../../recursos/js/comandos.js", import.meta.url));
const FUENTE = readFileSync(RUTA, "utf8");

const EJECUTIVO = "SR - MPPP - Ejecutivo";
const SUPERVISOR = "SR - MPPP - Supervisor";
const AUDITOR = "SR - MPPP - Auditor";
const ADMIN_PLANES = "SR - MPPP - Administrador de planes";
const VALIDADA = 159460003;
const DIGITADA = 159460004;

/** Carga comandos.js con un usuario que tiene esos roles (como los da `userSettings.roles`: directos y por equipo). */
function cargar(roles, { rolesQueLanzan = false } = {}) {
  const lista = roles.map((name, i) => ({ id: `00000000-0000-0000-0000-00000000000${i}`, name }));
  const contexto = {
    Xrm: {
      Utility: {
        getGlobalContext() {
          if (rolesQueLanzan) throw new Error("sin contexto");
          return { userSettings: { roles: { getAll: () => lista, getLength: () => lista.length, get: (i) => lista[i] } } };
        },
      },
    },
  };
  vm.createContext(contexto);
  vm.runInContext(FUENTE + "\n;globalThis.__C = Sanic.Mppp.Comandos;", contexto);
  return contexto.__C;
}

/** Una grilla con filas seleccionadas en esos estados (forma `data.entity.attributes.get(col).getValue()`). */
function grilla(...estados) {
  const filas = estados.map((v) => ({ data: { entity: { attributes: { get: () => ({ getValue: () => v }) } } } }));
  return { getGrid: () => ({ getSelectedRows: () => ({ getAll: () => filas, getLength: () => filas.length, get: (i) => filas[i] }) }) };
}

const DE_FILA_EJECUTIVO = ["puedeDigitada", "puedeRechazadaAs400", "puedeAnular", "puedeAtendida", "puedeDescargarParaDigitar"];
const DE_FILA_SUPERVISOR = ["puedeAprobar", "puedeDevolver"];
const DE_SOLICITUD = ["puedeAtendido", "puedeDescartar", "puedeRevisado"];
const TODAS = [...DE_FILA_EJECUTIVO, ...DE_FILA_SUPERVISOR, ...DE_SOLICITUD];

test("existen las reglas nuevas de DN-09", () => {
  const C = cargar([EJECUTIVO]);
  for (const r of TODAS) assert.equal(typeof C[r], "function", `falta Sanic.Mppp.Comandos.${r}`);
});

test("el Auditor no ve ningun boton", () => {
  const C = cargar([AUDITOR]);
  for (const r of TODAS) assert.equal(C[r](grilla(VALIDADA, DIGITADA)), false, r);
  assert.equal(C.esAdministradorDePlanes(), false);
});

test("un usuario sin roles de MPPP no ve ningun boton", () => {
  const C = cargar(["Basic User", "System Customizer"]);
  for (const r of TODAS) assert.equal(C[r](grilla(VALIDADA, DIGITADA)), false, r);
});

test("el Ejecutivo ve los suyos y no los del Supervisor", () => {
  const C = cargar([EJECUTIVO]);
  const g = grilla(VALIDADA);
  for (const r of [...DE_FILA_EJECUTIVO, ...DE_SOLICITUD]) assert.equal(C[r](g), true, r);
  for (const r of DE_FILA_SUPERVISOR) assert.equal(C[r](grilla(DIGITADA)), false, r);
});

test("el Supervisor ve Aprobar y Devolver y nada mas", () => {
  const C = cargar([SUPERVISOR]);
  const g = grilla(DIGITADA);
  for (const r of DE_FILA_SUPERVISOR) assert.equal(C[r](g), true, r);
  for (const r of [...DE_FILA_EJECUTIVO, ...DE_SOLICITUD]) assert.equal(C[r](grilla(VALIDADA)), false, r);
});

test("Revisado es solo del Ejecutivo (el Supervisor no tiene escritura sobre Solicitud)", () => {
  assert.equal(cargar([EJECUTIVO]).puedeRevisado(), true);
  assert.equal(cargar([SUPERVISOR]).puedeRevisado(), false);
});

test("Ejecutivo y Supervisor a la vez no ve ninguno (D-25)", () => {
  const C = cargar([EJECUTIVO, SUPERVISOR]);
  for (const r of TODAS) assert.equal(C[r](grilla(VALIDADA, DIGITADA)), false, r);
});

test("tener ademas el rol Auditor no le quita nada al Ejecutivo", () => {
  const C = cargar([EJECUTIVO, AUDITOR]);
  assert.equal(C.puedeDigitada(grilla(VALIDADA)), true);
  assert.equal(C.puedeAtendido(), true);
});

test("el nombre del rol se compara sin distinguir mayusculas, como el servidor", () => {
  const C = cargar(["sr - mppp - ejecutivo"]);
  assert.equal(C.puedeDigitada(grilla(VALIDADA)), true);
});

test("el estado se sigue mirando: el rol se suma, no reemplaza", () => {
  const C = cargar([EJECUTIVO]);
  assert.equal(C.puedeDigitada(grilla(DIGITADA)), false, "Digitada solo desde Validada");
  assert.equal(C.puedeAnular(grilla(DIGITADA)), true, "Anular desde Validada o Digitada");
  const S = cargar([SUPERVISOR]);
  assert.equal(S.puedeAprobar(grilla(VALIDADA)), false, "Aprobar solo desde Digitada");
});

test("si no se puede leer el estado de la grilla, decide el rol (como hoy, ante la duda se muestra por estado)", () => {
  const sinGrilla = { getGrid: () => { throw new Error("grilla cargando"); } };
  assert.equal(cargar([EJECUTIVO]).puedeDigitada(sinGrilla), true);
  assert.equal(cargar([AUDITOR]).puedeDigitada(sinGrilla), false);
});

test("si no se pueden leer los roles, se oculta", () => {
  const C = cargar([EJECUTIVO], { rolesQueLanzan: true });
  for (const r of TODAS) assert.equal(C[r](grilla(VALIDADA, DIGITADA)), false, r);
});

test("la carga masiva sigue siendo del Administrador de planes", () => {
  assert.equal(cargar([ADMIN_PLANES]).esAdministradorDePlanes(), true);
  assert.equal(cargar([EJECUTIVO]).esAdministradorDePlanes(), false);
});
