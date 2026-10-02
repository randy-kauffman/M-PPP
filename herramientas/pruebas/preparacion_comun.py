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
        self.avisos = []

    def avisar(self, mensaje):
        """Un mensaje que `preparar()` (o un caso) necesita que se vea EN VIVO, ni bien
        `preparar()` termina — no un log perdido en un archivo aparte. `correr.py` los imprime
        apenas vuelve `preparar()`, se haya preparado bien o no (2026-09-28: lo estrena
        `set_v.py` para anunciar que tuvo que reencender `MPPP-REC` al arrancar, RED 2 de la
        excepción V-02 — ver `CONVENCIONES.md`). Cualquier otro set lo puede usar igual para un
        aviso que no puede esperar al resumen final."""
        self.avisos.append(mensaje)

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
    # DN-04 (D-75, 2026-10-01): el tope de la carga masiva. Si el set L muriera con su versión de prueba (200)
    # todavía activa, esto la lleva a 100 igual: la versión activa más alta es la que lee el plugin.
    "carga.filas.maximo": "100",
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


def verificar_parametro_entero_positivo(dv, nombre):
    """Que exista una versión activa de `nombre` Y que su valor sea un
    entero positivo. Para los parámetros numéricos de fase 1 cuyo CONTENIDO,
    no solo su existencia, le importa a quien lo consume (un valor no
    numérico o negativo revienta al primer consumidor real, en vez de quedar
    detectado acá, antes de que corra un solo caso). Devuelve el entero."""
    fila = verificar_parametro_activo(dv, nombre)
    crudo = (fila.get("sanic_valor") or "").strip()
    try:
        valor = int(crudo)
    except ValueError:
        raise RuntimeError(f"'{nombre}' activo no es un entero: {crudo!r}")
    if valor <= 0:
        raise RuntimeError(f"'{nombre}' activo tiene que ser un entero positivo: obtenido {valor}")
    return valor


# ===========================================================================
# Verificaciones de CONTENIDO (no solo existencia) de los 6 parámetros de
# fase 1 que quedaron sin verificación directa al disolver el set P
# (`CONVENCIONES.md` §7, "nombrar no contar"; decisión del aprobador
# 2026-09-28: la verificación viaja con el set que la necesita, no vuelve a
# un set de preparación propio). Cada una la llama, desde su `preparar()`,
# el set al que el reparto se la asignó — nunca un set lejano.
# ===========================================================================
def verificar_bac_codigo_007(dv):
    """`plantilla.listas`, banco BAC = código `007` (`diseno/PENDIENTES.md`
    sección B, D-46): el código de 3 dígitos con el que se construye la
    referencia de formato 11 (`REFERENCIA_FORMATO_11`, caso R-10). Con el
    código equivocado la referencia sale mal y ASÍ LLEGA A AS400. Que la fila
    exista no alcanza: D-46 probó justo esto con un código inventado (`102`)
    que también tiene 3 dígitos y pasa cualquier chequeo de forma — hay que
    leer el valor y compararlo. Devuelve el JSON ya parseado."""
    fila = verificar_parametro_activo(dv, "plantilla.listas")
    crudo = fila.get("sanic_valor") or "{}"
    try:
        datos = json.loads(crudo)
    except json.JSONDecodeError as e:
        raise RuntimeError(f"'plantilla.listas' no es JSON válido: {e}")
    bancos = datos.get("banco") or []
    bac = next((b for b in bancos if b.get("valor") == "BAC"), None)
    if bac is None:
        raise RuntimeError(f"'plantilla.listas' no trae el banco 'BAC' en su lista: {bancos}")
    codigo = bac.get("codigo")
    if codigo != "007":
        raise RuntimeError(
            f"'plantilla.listas': BAC tiene código {codigo!r}, se esperaba '007' — con este código la "
            "referencia de formato 11 (REFERENCIA_FORMATO_11, caso R-10) sale mal y ASÍ LLEGA A AS400 "
            "(diseno/PENDIENTES.md, sección B)")
    return datos


def verificar_lectura_limites(dv):
    """`lectura.limites` (`03-contratos-custom-api.md` §249,
    `ValidarSolicitud.cs::ParametroLimites`): los dos topes de bytes con los
    que se lee el Excel adjunto, comprimido y descomprimido. `sanic_mppp_capi_validarsolicitud`
    los lee como obligatorios ANTES de evaluar ninguna regla de Solicitud —
    con una clave mal escrita o en 0, la lectura del primer Excel real
    revienta ahí, no acá. Verifica que las DOS claves existan y sean enteros
    positivos. Devuelve el JSON ya parseado."""
    fila = verificar_parametro_activo(dv, "lectura.limites")
    crudo = fila.get("sanic_valor") or "{}"
    try:
        datos = json.loads(crudo)
    except json.JSONDecodeError as e:
        raise RuntimeError(f"'lectura.limites' no es JSON válido: {e}")
    for clave in ("maximoBytesComprimido", "maximoBytesDescomprimido"):
        valor = datos.get(clave)
        if not isinstance(valor, int) or isinstance(valor, bool) or valor <= 0:
            raise RuntimeError(f"'lectura.limites': {clave!r} tiene que ser un entero positivo, obtenido {valor!r}")
    return datos


def verificar_prefijos_reenvio(dv):
    """`correo.prefijos.reenvio` (`ClasificarCorreo.cs::LeerPrefijosDeReenvio`):
    lista JSON de textos contra la que se compara el asunto para decidir
    'reenvío' (caso C-03). Verifica, NOMBRADO (CONVENCIONES §7, no un
    conteo), que la lista no esté vacía y traiga el prefijo `FW:` del que
    depende C-03 — que la fila exista no alcanza: una lista vacía o sin ese
    prefijo deja pasar como 'nuevo' un correo que en realidad es un reenvío,
    sin que ningún chequeo de forma lo note. Devuelve la lista."""
    fila = verificar_parametro_activo(dv, "correo.prefijos.reenvio")
    crudo = fila.get("sanic_valor") or "[]"
    try:
        prefijos = json.loads(crudo)
    except json.JSONDecodeError as e:
        raise RuntimeError(f"'correo.prefijos.reenvio' no es JSON válido: {e}")
    if not isinstance(prefijos, list) or not prefijos:
        raise RuntimeError(f"'correo.prefijos.reenvio' tiene que ser una lista no vacía de textos: {prefijos!r}")
    if "FW:" not in prefijos:
        raise RuntimeError(f"'correo.prefijos.reenvio' no trae 'FW:' (lo usa el caso C-03): {prefijos!r}")
    return prefijos


def verificar_servicio_cuenta(dv):
    """`servicio.cuenta` (`ListaBlancaStep`, CONVENCIONES §8.2): el UPN con
    el que los cinco flujos escriben sus marcas de envío — sin él, la lista
    blanca los trata como personas y les rechaza la escritura. Verifica que
    el valor activo tenga forma de UPN/correo (no vacío, con `@` y algo a
    los dos lados) — que la fila exista no alcanza: un valor vacío o sin
    `@` deja a los flujos escribiendo como si fueran una persona común, sin
    que ningún chequeo de forma lo note. Devuelve el valor."""
    fila = verificar_parametro_activo(dv, "servicio.cuenta")
    valor = (fila.get("sanic_valor") or "").strip()
    if "@" not in valor or valor.startswith("@") or valor.endswith("@"):
        raise RuntimeError(f"'servicio.cuenta' no tiene forma de UPN/correo: {valor!r}")
    return valor


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


def usuario_por_alias(dv, alias):
    """El `systemuserid` de un usuario de prueba por su alias de dominio
    (`User1`, `User2`, ...), igual que resuelve `comportamiento_roles.py`
    (CONVENCIONES §6). Lanza si el alias no identifica exactamente un
    usuario. Consolidado acá (2026-09-28): era IDÉNTICA en `set_a.py`,
    `set_b.py` e `set_i.py` (firma `(dv, alias)`) y en `set_e.py` (misma
    lógica, pero con la firma `(ctx, alias)`: unificada a `(dv, alias)` acá,
    y el llamador de `set_e.py` ahora pasa `ctx.dv`). Es plomería pura, sin
    ningún criterio de negocio adentro — exactamente lo que CONVENCIONES §8
    pide consolidar."""
    est, r, _ = dv.call(
        "GET", "systemusers?$select=systemuserid,fullname"
               f"&$filter=startswith(domainname,'{alias}@') and applicationid eq null")
    filas = r.get("value", []) if est == 200 else []
    if len(filas) != 1:
        raise RuntimeError(f"{alias!r} no identifica un usuario único en el entorno: {filas}")
    return filas[0]["systemuserid"]


# ===========================================================================
# DN-01 — Autorización por cliente. Consolidado acá (2026-09-28) lo que era
# IDÉNTICO entre `set_c.py`, `set_r.py` y `set_u.py`:
#
# - `fijar_modo_autorizacion`: el PATCH directo del valor ACTIVO de
#   `autorizacion.modo` era byte a byte igual en `set_c.py::_fijar_modo_autorizacion`
#   y `set_u.py::_fijar_modo` (el propio `set_u.py` citaba a `set_c.py` como
#   "mismo criterio" — ya eran la misma función con dos nombres).
# - `crear_autorizacion_cliente`: el create-if-missing del VÍNCULO
#   `AutorizacionCliente` (dado un `autorizado_id` YA CREADO) era también
#   idéntico entre `set_r.py` y `set_u.py` — verificado línea por línea; la
#   única diferencia era el texto del mensaje de error de una excepción
#   (cosmético, no de comportamiento). Esto CORRIGE lo que se asumía al
#   repartir esta tarea ("uno arma solo el vínculo, el otro también el
#   Autorizado"): ninguna de las dos crea el Autorizado — las dos lo reciben
#   ya creado, y cada set lo arma aparte con su propio `_crear_autorizado()`
#   local (que SÍ es una tercera copia idéntica, pero no estaba en el
#   alcance aprobado de esta consolidación: queda documentada, no tocada,
#   para que el aprobador decida aparte).
# ===========================================================================
AUTORIZACIONCLIENTES = "sanic_mppp_tbl_autorizacionclientes"
AUTORIZADOS = "sanic_mppp_tbl_autorizados"
CLIENTES = "sanic_mppp_tbl_clientes"


def fijar_modo_autorizacion(dv, valor):
    """PATCH directo del valor ACTIVO de `autorizacion.modo`: mismo patrón
    simple que usa `aplicar_canon` para llevarlo al canon, acá para escribir
    el valor de PRUEBA que cada caso de DN-01 necesita (§8.2 solo versiona
    un cambio de NEGOCIO; un valor efímero de prueba no lo es). Lanza si no
    hay versión activa — no debería pasar nunca: ya lo comprobó `preparar()`."""
    fila = verificar_parametro_activo(dv, "autorizacion.modo")
    pid = fila["sanic_mppp_tbl_parametroid"]
    est, r, _ = dv.call("PATCH", f"sanic_mppp_tbl_parametros({pid})", {"sanic_valor": valor})
    if est not in (200, 204):
        raise RuntimeError(f"no se pudo escribir autorizacion.modo={valor!r} (HTTP {est}): {r}")


def crear_autorizacion_cliente(dv, autorizado_id, cliente_id):
    """Create-if-missing (clave alternativa autorizado+cliente, DN-01): si ya
    existe una `AutorizacionCliente` para este par (p. ej. de una corrida
    anterior que no llegó a `cerrar()`), la reusa en vez de duplicarla. No
    manda `sanic_nombre`: lo calcula un step (`<correo> → <cliente>`) y pisa
    cualquier valor que se le mande. Asume que `autorizado_id` YA EXISTE:
    arma solo el vínculo, nunca el Autorizado (ver la nota del módulo)."""
    est, r, _ = dv.call(
        "GET",
        f"{AUTORIZACIONCLIENTES}?$select=sanic_mppp_tbl_autorizacionclienteid"
        f"&$filter=_sanic_autorizadoid_value eq {autorizado_id} and _sanic_clienteid_value eq {cliente_id}")
    if est == 200 and r.get("value"):
        return r["value"][0]["sanic_mppp_tbl_autorizacionclienteid"]

    cuerpo = {
        "sanic_autorizadoid@odata.bind": f"/{AUTORIZADOS}({autorizado_id})",
        "sanic_clienteid@odata.bind": f"/{CLIENTES}({cliente_id})",
    }
    est, r, cab = dv.call("POST", AUTORIZACIONCLIENTES, cuerpo)
    if est not in (200, 201, 204):
        raise RuntimeError(f"no se pudo crear la AutorizacionCliente (autorizado {autorizado_id}, "
                           f"cliente {cliente_id}) (HTTP {est}): {r}")
    from dataverse_api import Dataverse
    return Dataverse.id_creado(cab)


def cliente_compartido(dv):
    """`(id, nombre)` del Cliente compartido de `preparar_datos.py` (dueño de
    PR11/PR06/PR10/PRIN/PRNA), localizado por `sanic_cifbac` — su clave de
    negocio (clave alternativa `KEY - MPPP - Cliente - CIF BAC`) — NUNCA por
    `sanic_nombre`: ese campo es decorativo, y en Dev el Cliente real de este
    fixture puede llevar la marca vieja (`"PR - Pruebas MPPP SA"`, vivo desde
    el 2026-09-23) en vez de la nueva (`prep.CLIENTE`, `ZZP-...`) — ver el
    docstring de `preparar_datos.py`, "El fixture compartido del 2026-09-23...".

    Devuelve también el `nombre` REAL leído (no asumas `prep.CLIENTE`): quien
    arma un nombre calculado tipo `'<algo> - <Cliente>'` para comparar contra
    un plugin (p. ej. `set_i.py::i06`) tiene que usar el nombre que el Cliente
    tiene DE VERDAD en el entorno, no el literal nuevo — si el entorno todavía
    tiene la marca vieja, comparar contra `prep.CLIENTE` da un FALLA que no
    tiene nada que ver con el plugin que se está probando.

    **Consolidado acá 2026-09-28** (defecto real: la migración de marca del
    Cliente — buscar por CIF en vez de por nombre — solo se había corregido en
    `preparar_datos.py`; la misma búsqueda por `sanic_nombre` seguía duplicada,
    rota, en `set_c.py`, `set_r.py::_id_cliente_compartido`,
    `set_u.py::_id_cliente_compartido` y `set_i.py` (casos I-06 e I-11) — cinco
    copias de la misma consulta, y arreglar una sola dejó las otras cuatro en
    rojo. No repitas esta búsqueda en un sexto lugar: importá esta función."""
    cli = prep.uno(dv, "sanic_mppp_tbl_clientes", f"sanic_cifbac eq '{prep.CIF_BAC}'",
                   "sanic_mppp_tbl_clienteid,sanic_nombre")
    if cli is None:
        raise RuntimeError(f"no se encontró el Cliente compartido (CIF BAC {prep.CIF_BAC!r}) "
                           "(¿preparar_datos.py no corrió todavía?)")
    return cli["sanic_mppp_tbl_clienteid"], cli["sanic_nombre"]


def id_cliente_compartido(dv):
    """Atajo de `cliente_compartido()` para quien solo necesita el GUID."""
    return cliente_compartido(dv)[0]
