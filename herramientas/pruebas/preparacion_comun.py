#!/usr/bin/env python3
"""Verificaciones y utilidades COMUNES a todos los sets de `herramientas/pruebas/sets/`.

Nace de la desaparición del set P (`00-preparacion.md`, `CONVENCIONES.md` §8):
sus seis verificaciones (datos de negocio, Excel, flujos activos, parámetros,
estructura de plantilla, roles) no desaparecieron — se mudaron ACÁ, para que
cada set las LLAME desde su `preparar()` en vez de copiarlas. Lógica en un
solo lugar, invocación en cada set.

También vive acá el contrato compartido entre el motor (`correr.py`) y cada
set:

- `Caso` — una comprobación con su resultado (OK/FALLA/N-A), igual en todos
  los sets.
- `Contexto` — la conexión a Dataverse y lo que el set fue creando en esta
  corrida, para que `cerrar()` lo pueda borrar **por GUID, nunca por nombre**
  (`limpiar_solicitudes()`): la lista la arma este mismo proceso al crear cada
  Solicitud, no una relectura posterior — que es justo el error que borró
  autorizaciones reales del aprobador el 2026-09-27 (CONVENCIONES §5).

Nada de acá imprime nada: eso es trabajo de `correr.py`, que imprime en vivo.
"""
import json
import os
import sys

_AQUI = os.path.dirname(os.path.abspath(__file__))
_RAIZ = os.path.dirname(os.path.dirname(_AQUI))
sys.path.insert(0, os.path.join(_RAIZ, "herramientas"))
sys.path.insert(0, _AQUI)

from dataverse_api import Dataverse  # noqa: E402
import preparar_datos as prep  # noqa: E402
import generar_excel as genexcel  # noqa: E402
import simulador as sim  # noqa: E402
import marca_agua  # noqa: E402  (reexportado: los sets que creen datos propios lo usan directo)

ADJUNTOS = os.path.join(_RAIZ, "pruebas", "adjuntos")


def excel(nombre):
    return os.path.join(ADJUNTOS, nombre)


# ===========================================================================
# Contrato compartido: Caso y Contexto
# ===========================================================================
class Caso:
    """Una comprobación con su resultado. El caso (la función `sNN` de un set)
    solo llama `check()`/`na()`; quien decide el estado final (OK/FALLA) y lo
    imprime es el motor (`correr.py`), llamando `cerrar()` después de correr
    la función — nunca el propio caso, así el criterio es el mismo para
    todos los sets."""

    def __init__(self, id_, titulo):
        self.id = id_
        self.titulo = titulo
        self.checks = []
        self.estado = None
        self.motivo_na = None

    def check(self, nombre, ok, detalle=""):
        self.checks.append((nombre, bool(ok), detalle))
        return ok

    def na(self, motivo):
        self.estado = "N-A"
        self.motivo_na = motivo

    def cerrar(self):
        if self.estado == "N-A":
            return
        if not self.checks:
            self.estado = "FALLA"
            self.checks.append(("(el caso no dejó ninguna comprobación registrada)", False, ""))
            return
        self.estado = "OK" if all(ok for _, ok, _ in self.checks) else "FALLA"


class Contexto:
    """Lo que un set arma mientras corre: la conexión a Dataverse (`dv`), las
    Solicitudes que creó (`creadas`, para borrarlas por GUID en `cerrar()`),
    los Excel ad hoc que armó (`excels_extra`, quedan — son insumo reutilizable,
    igual que los E01..E15) y un espacio libre (`extra`) para lo que cada set
    necesite recordar entre `preparar()` y sus casos (p.ej. los encabezados de
    la plantilla)."""

    def __init__(self, dv):
        self.dv = dv
        self.creadas = []
        self.excels_extra = []
        self.extra = {}

    def solicitud(self, **kw):
        sid, mid = sim.crear_solicitud(self.dv, **kw)
        self.creadas.append(sid)
        return sid, mid

    def procesar(self, **kw):
        sid, mid, rc, rv = sim.ingresar_y_procesar(self.dv, **kw)
        self.creadas.append(sid)
        return sid, mid, rc, rv

    def excel_ad_hoc(self, nombre, filas):
        """Un Excel de prueba fuera del catálogo fijo de `generar_excel.py`,
        para casos que combinan condiciones puntuales. Se guarda con prefijo
        `ZZ-` y NO se borra solo: queda en `pruebas/adjuntos/` como insumo
        reutilizable, igual que los E01..E15."""
        ruta, _ = genexcel.escribir(nombre, filas, None)
        if ruta not in self.excels_extra:
            self.excels_extra.append(ruta)
        return ruta

    def limpiar_solicitudes(self):
        """Borra, por GUID, todo lo que este Contexto creó en esta corrida.
        Es lo que cada set llama desde su `cerrar()`. Nunca lanza por un
        borrado individual que falle: junta el motivo y sigue, para que un
        residuo no tumbe la limpieza de los demás."""
        errores = []
        for sid in self.creadas:
            try:
                sim.borrar_solicitud(self.dv, sid)
            except Exception as e:
                errores.append(f"no se pudo borrar la Solicitud simulada {sid}: {e}")
        return errores


# ===========================================================================
# §8.2 — Canon de configuración global.
#
# Un set no restaura: LLEVA AL CANON, antes y después. Lo aplica `correr.py`
# antes y después de CADA set (sea cual sea) — no cada set por separado: es
# el mismo valor para todos, y si cada set lo aplicara por su cuenta un set
# que no lo llama dejaría el canon sin garantía.
# ===========================================================================
CANON_SIMPLE = {
    # Los parámetros de valor simple (texto/número) de la tabla de §8.2 que
    # se pueden llevar al canon con un PATCH directo. Los otros dos de la
    # tabla (`plantilla.obligatoriedad`, `exportacion.columnas`) NO son un
    # valor simple — su canon es "la versión activa más alta" / "el sembrado
    # por parametros.md" — y se dejan fuera de este PATCH automático: acá solo
    # se comprueba que existan activos (`verificar_parametro_activo`).
    "autorizacion.modo": "plan",
    "rpa.puedeaprobar": "si",
    "rpa.lote.maximo": "100",
    "rpa.horas.tolerancia": "24",
    "rpa.minutos.claimmaximo": "60",
}

PARAMETROS_NO_SIMPLES = ("plantilla.obligatoriedad", "exportacion.columnas")


def _parametro_activo(dv, nombre):
    est, r, _ = dv.call(
        "GET",
        "sanic_mppp_tbl_parametros?$select=sanic_mppp_tbl_parametroid,sanic_valor"
        f"&$filter=sanic_nombre eq '{nombre}' and statecode eq 0"
        "&$orderby=sanic_version desc&$top=1")
    if est != 200 or not r.get("value"):
        return None, est, r
    return r["value"][0], est, r


def aplicar_canon(dv):
    """Lleva cada parámetro de `CANON_SIMPLE` a su valor canónico. Idempotente:
    si ya está en el canon, no escribe nada. Devuelve la lista de cambios que
    hizo (vacía = ya estaba todo bien) — nunca lanza: un parámetro que no se
    puede leer/escribir queda anotado como cambio fallido, no tumba la corrida
    (el canon es una red de seguridad, no un requisito bloqueante de un set)."""
    cambios = []
    for nombre, valor in CANON_SIMPLE.items():
        fila, est, r = _parametro_activo(dv, nombre)
        if fila is None:
            cambios.append(f"{nombre}: no se encontró activo (HTTP {est}) — no se pudo llevar al canon")
            continue
        actual = (fila.get("sanic_valor") or "").strip()
        if actual == valor:
            continue
        pid = fila["sanic_mppp_tbl_parametroid"]
        est2, r2, _ = dv.call("PATCH", f"sanic_mppp_tbl_parametros({pid})", {"sanic_valor": valor})
        if est2 in (200, 204):
            cambios.append(f"{nombre}: {actual!r} → {valor!r} (canon)")
        else:
            cambios.append(f"{nombre}: PATCH a canon falló (HTTP {est2}): {r2}")
    return cambios


# ===========================================================================
# Antiguo P-03 — datos de negocio de prueba (fixture COMPARTIDO `PR*`)
# ===========================================================================
def verificar_datos_negocio(dv, remitente):
    """El fixture `PR*` que crea `preparar_datos.py`: Cliente, planes, el
    Autorizado y sus autorizaciones. create-if-missing, JAMÁS `--limpiar`
    (es compartido con `pruebas/gui/`: ver el docstring de `preparar_datos.py`
    y CONVENCIONES §5). Lanza si algo no quedó en verde."""
    malos = prep.comprobar_largos(remitente)
    if malos:
        raise RuntimeError("preparar_datos: valores más largos de lo admitido: " + "; ".join(malos))
    prep.preparar(dv, remitente)
    bien, faltan = prep.verificar(dv, remitente)
    if faltan:
        raise RuntimeError(f"preparar_datos.verificar() no quedó en verde: {faltan}")
    return bien


# ===========================================================================
# Antiguo P-04 — los Excel de los casos
# ===========================================================================
def _excels_faltantes():
    presentes = os.listdir(ADJUNTOS) if os.path.isdir(ADJUNTOS) else []
    faltan = [f"E{n:02d}" for n in range(1, 16) if not any(p.startswith(f"E{n:02d}-") for p in presentes)]
    if "A01-no-es-excel.txt" not in presentes:
        faltan.append("A01-no-es-excel.txt")
    return faltan


def verificar_excels():
    """Los 15 `.xlsx` de los casos y el adjunto que no es Excel. Los genera
    (`generar_excel.py`, sin red) si falta alguno. Lanza si después de
    generarlos algo sigue faltando."""
    if _excels_faltantes():
        genexcel.main()
    faltan = _excels_faltantes()
    if faltan:
        raise RuntimeError(f"generar_excel.py corrió pero siguen faltando: {faltan}")


# ===========================================================================
# Antiguo P-05 — los cinco flujos activos
#
# Ningún set migrado hasta el 2026-09-27 lo necesita: todos validan contra
# las Custom API directo, vía `simulador.py`, sin disparar el flujo real (ver
# el docstring de `simulador.py`). Queda acá lista para el set que sí lo
# necesite (uno que alguna vez dispare el flujo de Power Automate de verdad).
# ===========================================================================
NOMBRES_FLUJOS = {
    "Cloud Flow - MPPP - REC - Recibir correo nuevo",
    "Cloud Flow - MPPP - ING - Ingerir y validar correo",
    "Cloud Flow - MPPP - COM - Detectar comunicacion pendiente",
    "Cloud Flow - MPPP - ENV - Enviar comunicacion al cliente",
    "Cloud Flow - MPPP - VIG - Vigilar pendientes",
}


def verificar_flujos_activos(dv, nombres=None):
    """`(faltan, apagados)`: nombres de flujo que no existen en el entorno, y
    nombres que existen pero con `statecode != 1`. Los dos vacíos = en verde."""
    nombres = nombres or NOMBRES_FLUJOS
    est, r, _ = dv.call("GET", "workflows?$select=name,statecode&$filter=category eq 5")
    if est != 200:
        raise RuntimeError(f"no se pudo leer la lista de flujos (HTTP {est})")
    por_nombre = {w["name"]: w["statecode"] for w in r.get("value", [])}
    faltan = nombres - set(por_nombre)
    apagados = [n for n in nombres if n in por_nombre and por_nombre.get(n) != 1]
    return faltan, apagados


# ===========================================================================
# Antiguo P-06 — los parámetros cargados
# ===========================================================================
def verificar_estructura_plantilla(dv):
    """Que `plantilla.estructura` (activo, versión más alta) tenga la forma
    que las reglas de nivel Solicitud y Registro dan por sentada: hoja
    `Datos`, encabezado en la fila 12, datos desde la 13. Lanza si no."""
    fila, est, r = _parametro_activo(dv, "plantilla.estructura")
    if fila is None:
        raise RuntimeError(f"no se pudo leer el parámetro 'plantilla.estructura' activo (HTTP {est}): {r}")
    try:
        datos = json.loads(fila.get("sanic_valor") or "{}")
    except json.JSONDecodeError as e:
        raise RuntimeError(f"'plantilla.estructura' no es JSON válido: {e}")
    ok = (datos.get("hoja") == "Datos" and datos.get("filaEncabezado") == 12
          and datos.get("primeraFila") == 13)
    if not ok:
        raise RuntimeError(
            f"'plantilla.estructura' no tiene la forma esperada (hoja=Datos, filaEncabezado=12, "
            f"primeraFila=13): obtenido {datos}")
    return datos


def verificar_parametro_activo(dv, nombre):
    """Que exista AL MENOS UNA versión activa de `nombre` (para los
    parámetros no-simples de §8.2, cuyo valor no se puede llevar a un canon
    fijo: solo importa que estén sembrados y activos)."""
    fila, est, r = _parametro_activo(dv, nombre)
    if fila is None:
        raise RuntimeError(f"no hay ninguna versión activa de '{nombre}' (HTTP {est}): {r}")
    return fila


# ===========================================================================
# Roles de usuario — contra `systemuserroles_association`, nunca supuestos
# (CONVENCIONES §8). Solo lo usan los sets que impersonan un rol (sesión de
# navegador o `MSCRMCallerID`); un set 100% API-directa (como el service
# principal) no tiene ningún usuario cuyo rol verificar.
# ===========================================================================
def verificar_roles(dv, systemuserid, roles_esperados):
    """`(ok, roles_actuales)`. Nunca asigna el rol que falte: el service
    principal `# PowerPlatform-Dev` puede hacerlo (tiene System Administrator),
    pero asignárselo a un usuario de prueba es una decisión de datos que toma
    el set que lo necesita, no este chequeo genérico."""
    est, r, _ = dv.call("GET", f"systemusers({systemuserid})/systemuserroles_association?$select=roleid,name")
    if est != 200:
        raise RuntimeError(f"no se pudo leer systemuserroles_association de {systemuserid} (HTTP {est}): {r}")
    actuales = {x["name"] for x in r.get("value", [])}
    faltan = set(roles_esperados) - actuales
    return not faltan, actuales
