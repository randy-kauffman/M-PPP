#!/usr/bin/env python3
"""Set G — Seguridad, sitemap, vistas y formularios (`pruebas/06-seguridad-vistas-formularios.md`).

El set más delicado del plan: acá se decide si un dato bancario de una persona
real queda protegido o no. El contrato completo de un módulo de set está en
`sets/__init__.py`; la lógica de preparación compartida, en `preparacion_comun.py`.

**Por qué la guarda de sesiones va PRIMERO, y va siempre** (CONVENCIONES §6).
En seguridad, una prueba que pasa porque el usuario no ve algo es indistinguible
de una que pasa porque la sesión está muerta. Las dos dan verde, y una miente.
Por eso `preparar()` llama a `estado_sesiones.verificar_o_fallar()` para las
cuatro sesiones ANTES de tocar cualquier otra cosa — aunque la mayoría de los
casos de este archivo terminen corriendo por API (impersonación con
`MSCRMCallerID`, no por navegador): el set entero, MD incluido, sigue citando
esas cuatro sesiones para lo que todavía es GUI (G-02, G-07, G-11 y el ícono/
recorte de G-01), y **el set no corre un solo caso si algo no cuadra** — cero
casos corridos es un resultado honesto (CONVENCIONES §3). Además se construye
UNA `comportamiento_roles.Entorno()`, que verifica —contra
`systemuserroles_association`, nunca supuesto— que User1..User4 tengan
exactamente el rol de negocio que este set asume, antes de impersonar a nadie.

**De GUI a API, caso por caso** (lo que pidió el pedido de migración):

- G-00 / G-00b ya eran API (`verificar_accesos.py` / `comportamiento_roles.py`).
  Se corrige G-00 para que incluya también a **User4** (`admin_tecnico`): el MD
  viejo solo invocaba la herramienta con User1..User3, y la matriz SÍ define
  privilegios para el Administrador técnico (`04` §2) — un hueco de cobertura,
  no una decisión.
- G-01 (sitemap) pasa a API: se lee la sección `## 2. Qué se crea` del propio
  playbook (`playbooks/app/mantenimientoppp.md`, el que ya construyó el
  sitemap) y se nombran, una por una, las 15 entradas con su tabla y su vista
  — y se corre `app.py --solo-verificar` para confirmar que el sitemap YA
  DESPLEGADO en el entorno coincide EXACTO con lo que ese playbook pide, con
  el `savedqueryid` resuelto EN VIVO: es la prueba de "cada entrada abre SU
  vista" que el MD viejo daba por "nunca se pudo verificar".
- G-03 (vistas) y G-08 (formularios) pasan a API: se leen `savedqueries` y
  `systemforms` en vivo. G-03 nombra las 20 vistas y, para las siete más
  fáciles de leer mal, compara el `fetchxml` contra el filtro exacto que
  declara `05-app-model-driven.md` §3. G-08 nombra los 10 formularios y lee
  el `formxml` de los puntos que el MD marca "mirá especialmente".
- G-04 (cartera del ejecutivo) pasa a API: en vez de reasignar a mano el
  propietario del Cliente compartido `PR - Pruebas MPPP SA` (que usan los
  otros dos agentes en paralelo), se aprovecha que ESE Cliente YA es propiedad
  de la cuenta de servicio, no de ningún User1..User4 (verificado en vivo el
  2026-09-27) — así que ninguna Fila que este set procese ahí va a aparecer
  nunca en "Mis clientes" de nadie, y el caso se reduce a ejecutar el
  `fetchxml` REAL de "Mis clientes — por digitar" y de "Todos — por digitar",
  impersonando al ejecutivo, sobre las filas que este mismo set acaba de crear.
  Cero escritura extra sobre el fixture compartido.
- G-05 (perfil de columna) pasa a API: se lee `sanic_numeroidentificacion` y
  `sanic_numerocuenta` impersonando a los 4 roles directo por Web API — **el
  caso más importante del set**, y el que se verificó con más cuidado (ver su
  docstring en el código).
- G-09 (sin evidencia) suma una pata de API: ejecutar el `fetchxml` real de
  "Autorizaciones sin evidencia" impersonando a `admin_planes`.
- G-10 (notificaciones) suma una pata de API: crear una tanda real y leer
  `appnotification` después. Lo que se ve al tocar la notificación (que
  navegue a la vista/Solicitud correcta) sigue siendo GUI.
- G-13 es **nuevo**: la frontera con el set B (fase 2, RPA). Ver su docstring.
- G-02 (íconos), G-06 (ya cubierto, no se repite), G-07 (recorte del sitemap
  por rol) y G-11 (multiidioma) siguen siendo GUI/N-A: piden ver algo
  RENDERIZADO (un SVG en dos temas, un menú recortado, una etiqueta traducida)
  y no tienen arnés en `pruebas/gui/` todavía. Quedan documentados en el MD
  con el comando exacto y la sesión que corresponde, pero **no** tienen
  función en `CASOS`: `correr.py G` no inventa un resultado para algo que no
  corrió.
- **G-14 a G-19 (fusionados el 2026-09-28, `CONVENCIONES.md` §4, "La regla
  del nombre")**: el set `H` (`13-construido-hoy.md`, nombrado por la fecha
  en que se escribió, no por lo que prueba) se repartió entre este set y el
  set A. Los seis casos de sitemap, columna de trabajo y formulario que eran
  `H-01` a `H-05` y `H-07` — exactamente el TEMA de este archivo — pasaron
  acá como `G-14`..`G-19`, mismo cuerpo, funciones `h01..h05,h07` renombradas
  `g14..g19` (el séptimo, `H-06`, no es de sitemap/vistas/formularios: es un
  comando de la barra, y pasó a `A-15` en `set_a.py`). `set_h.py` desapareció;
  la tabla de equivalencias completa vive en `pruebas/README.md`.

**Fase 1 + fase 2, la frontera con el set B** (09-rpa-fase2.md). El rol
`SR - MPPP - RPA` es una identidad de APLICACIÓN, nunca abre esta app model-
driven ni tiene sesión de navegador: por eso NINGÚN caso de sitemap/vistas/
formularios de este archivo lo menciona. Lo que SÍ es de este set —porque es
exactamente el tema de `04-matriz-privilegios.md` §3/§4, el mismo diseño que
G-00/G-05 verifican para los cuatro roles humanos— es que el rol del RPA
**no traiga privilegios de negocio** y **no esté en el perfil de columna**
(D-9, D-44, aprobador 2026-09-25). El set B (`09-rpa-fase2.md`, B-01/B-02/B-03)
ya prueba la PUERTA de las Custom API (quién pasa y quién no); ESTE set (G-13)
prueba la FORMA del rol en sí (qué privilegios y qué perfiles lo alcanzan), que
B no toca. No hay superposición: B ejercita el comportamiento en tiempo de
llamada, G-13 audita la configuración declarada del rol.
"""
import datetime
import json
import os
import subprocess
import sys
import time
import urllib.parse
import xml.etree.ElementTree as ET

_AQUI = os.path.dirname(os.path.abspath(__file__))
_HERRAMIENTAS_PRUEBAS = os.path.dirname(_AQUI)
_HERRAMIENTAS = os.path.dirname(_HERRAMIENTAS_PRUEBAS)
_RAIZ = os.path.dirname(_HERRAMIENTAS)
sys.path.insert(0, _HERRAMIENTAS)
sys.path.insert(0, _HERRAMIENTAS_PRUEBAS)
sys.path.insert(0, os.path.join(_HERRAMIENTAS, "construir"))

from dataverse_api import Dataverse  # noqa: E402
import preparacion_comun as pc  # noqa: E402
import simulador as sim  # noqa: E402
import comportamiento_roles as cr  # noqa: E402
import estado_sesiones  # noqa: E402
import verificar_accesos as va  # noqa: E402
import _comun as construir_comun  # noqa: E402

LETRA = "G"
NOMBRE = "Seguridad, sitemap, vistas y formularios"

# Remitente propio de este set. Nunca el de una persona real (CONVENCIONES §5).
REMITENTE = "zz-corredor-fase1@example.com"

PLAYBOOK_APP = os.path.join(_RAIZ, "playbooks", "app", "mantenimientoppp.md")
APP_PY = os.path.join(_HERRAMIENTAS, "construir", "app.py")

VALIDADA, DIGITADA, APROBADA, ANULADA = 159460003, 159460004, 159460005, 159460007

CSP_DATOS_SENSIBLES = "CSP - MPPP - Datos sensibles"
ROL_RPA = "SR - MPPP - RPA"

# --- de acá para abajo, constantes que traía `sets/set_h.py` (fusionado el 2026-09-28), usadas
# solo por G-14..G-19 ---
SITEMAP_UNIQUE = "sanic_mppp_sm_mantenimientoppp"
APP_UNIQUE = "sanic_mppp_mda_mantenimientoppp"

TABLA_FILA = "sanic_mppp_tbl_fila"
TABLA_AUTORIZADO = "sanic_mppp_tbl_autorizado"
TABLA_AUTORIZACIONPLAN = "sanic_mppp_tbl_autorizacionplan"
TABLA_AUTORIZACIONCLIENTE = "sanic_mppp_tbl_autorizacioncliente"

COLUMNA_ATENCION_MANUAL = "sanic_requiereatencionmanual"
ANCLA_NUMEROFILA = "sanic_numerofila"

# Las 4 vistas de trabajo que SÍ tienen que traer la columna (nombradas, no
# contadas — CONVENCIONES §7), y las 3 vistas de Fila que NO son de trabajo y
# por lo tanto NO tienen que traerla.
VISTAS_DE_TRABAJO = [
    "Mis clientes - por digitar",
    "Todos - por digitar",
    "Por aprobar",
    "Mis clientes - devueltas",
]
VISTAS_SIN_LA_COLUMNA = ["Filas", "Terminadas", "Tomadas por el bot"]

# `classid` del control de Lookup en un `formxml` (Learn; el mismo valor que
# usa `herramientas/construir/formulario.py`, CONTROL_POR_TIPO["LookupType"]).
CLASSID_LOOKUP = "{270BD3DB-D9AF-4782-9025-509E298DEC0A}"
FORMULARIO_PRINCIPAL = 2  # systemform.type: 2 = Main (formulario.py, FORMULARIO_PRINCIPAL)


def _ahora_iso():
    return datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def _fetchxml(dv, tabla, xml, cabeceras=None):
    """Ejecuta el `fetchxml` TAL CUAL lo trae una vista real (`savedqueries`),
    impersonando si se pasa `cabeceras`. Es la forma más fiel de probar "esta
    vista filtra así": no se reinterpreta la condición a mano, se corre la
    condición real."""
    ruta = f"{tabla}?fetchXml={urllib.parse.quote(xml)}"
    return dv.call("GET", ruta, cabeceras=cabeceras)


def _vista(dv, tabla, nombre):
    est, r, _ = dv.call("GET", f"savedqueries?$select=savedqueryid,fetchxml&$filter="
                                f"name eq '{nombre}' and returnedtypecode eq '{tabla}'")
    filas = r.get("value", []) if est == 200 else []
    return filas[0] if len(filas) == 1 else None


# ===========================================================================
# Helpers — sitemap, vistas (detalle de layout) y formulario. Migrados tal cual de
# `sets/set_h.py` (fusionado el 2026-09-28); solo los usan G-14..G-19.
# ===========================================================================
def _leer_sitemapxml(dv):
    est, r, _ = dv.call(
        "GET", f"sitemaps?$select=sitemapxml&$filter=sitemapnameunique eq '{SITEMAP_UNIQUE}'")
    filas = r.get("value", []) if est == 200 else []
    if len(filas) != 1:
        raise RuntimeError(f"no se encontró (una sola vez) el sitemap {SITEMAP_UNIQUE!r} (HTTP {est}): {filas}")
    return filas[0]["sitemapxml"]


def _titulos_de(elemento):
    """`{LCID: Title}` del hijo directo `<Titles>` de un `Area`/`Group`/`SubArea`."""
    nodo = elemento.find("Titles")
    if nodo is None:
        return {}
    return {t.get("LCID"): t.get("Title") for t in nodo.findall("Title")}


def _area_por_titulo_1033(root, titulo):
    for area in root.findall("Area"):
        if _titulos_de(area).get("1033") == titulo:
            return area
    return None


def _subareas_por_id(elemento):
    """Todas las `<SubArea>` descendientes de `elemento` (un `Area` o la raíz
    `SiteMap`), indexadas por su `Id` — en el orden en que aparecen en el XML,
    que es justo lo que las posiciones de G-14 necesitan."""
    return {s.get("Id"): s for s in elemento.findall(".//SubArea")}


def _en_orden_consecutivo(ids_en_documento, subsecuencia):
    """`True` si cada id de `subsecuencia` aparece en `ids_en_documento`, EN
    ESE ORDEN y CONSECUTIVOS (sin nada en el medio) — lo que pide G-14: no
    alcanza con que estén, tienen que estar exactamente uno después del otro."""
    if any(i not in ids_en_documento for i in subsecuencia):
        return False
    posiciones = [ids_en_documento.index(i) for i in subsecuencia]
    return all(posiciones[k + 1] - posiciones[k] == 1 for k in range(len(posiciones) - 1))


def _savedquery(ctx, nombre, tabla):
    """Como `_vista()`, pero trayendo también `name`/`layoutxml`/`isdefault` — lo que necesitan
    G-14 (comparar contra el `savedqueryid` de la Url) y G-15 (leer el `layoutxml`). No se unifica
    con `_vista()` (que solo trae `savedqueryid`/`fetchxml`, y lo usan G-04/G-09): mismo criterio
    que ya separaba estos dos módulos, ahora conviven en el mismo archivo sin pisarse."""
    est, r, _ = ctx.dv.call(
        "GET",
        "savedqueries?$select=savedqueryid,name,fetchxml,layoutxml,isdefault"
        f"&$filter=name eq '{nombre}' and returnedtypecode eq '{tabla}'")
    filas = r.get("value", []) if est == 200 else []
    if len(filas) != 1:
        raise RuntimeError(f"la vista {nombre!r} sobre {tabla!r} no se encontró de forma única (HTTP {est}): {filas}")
    return filas[0]


def _celdas_del_layout(layoutxml_texto):
    root = ET.fromstring(layoutxml_texto)
    fila = root.find("row")
    return [c.get("name") for c in fila.findall("cell")] if fila is not None else []


def _atributos_del_fetch(fetchxml_texto):
    root = ET.fromstring(fetchxml_texto)
    entidad = root.find("entity")
    return [a.get("name") for a in entidad.findall("attribute")] if entidad is not None else []


def _controles_con_campo(elemento):
    """`{datafieldname: classid}` de todos los `<control>` con `datafieldname`
    bajo `elemento` (un `<header>` o un `<section>` del `formxml`)."""
    return {ctrl.get("datafieldname"): ctrl.get("classid")
            for ctrl in elemento.findall(".//control") if ctrl.get("datafieldname")}


def _seccion_por_titulo(root, titulo):
    for seccion in root.findall(".//section"):
        etiqueta = seccion.find("labels/label")
        if etiqueta is not None and etiqueta.get("description") == titulo:
            return seccion
    return None


def _tablas_de_la_app(ctx, id_app):
    est, r, _ = ctx.dv.call("GET", f"RetrieveAppComponents(AppModuleId={id_app})")
    if est != 200:
        raise RuntimeError(f"RetrieveAppComponents devolvió HTTP {est}: {r}")
    nombres = set()
    for fila in r.get("value", []):
        if fila.get("componenttype") != 1:  # 1 = tabla (AppModuleComponent)
            continue
        est2, r2, _ = ctx.dv.call("GET", f"EntityDefinitions({fila['objectid']})?$select=LogicalName")
        if est2 == 200 and r2.get("LogicalName"):
            nombres.add(r2["LogicalName"])
    return nombres


# ===========================================================================
# preparar / cerrar
# ===========================================================================
def preparar(ctx):
    """Deja listo TODO lo que el set G necesita, y nada más (CONVENCIONES §8).

    **1. La guarda de sesiones, primero que nada.** `estado_sesiones.
    verificar_o_fallar()` para las cuatro: si una sesión de navegador está
    muerta o el usuario detrás no tiene el rol que le toca, el set entero NO
    arranca — aunque la mayoría de los casos de abajo no abran un navegador.
    Ver el docstring del módulo.

    **2. Los cuatro roles, verificados contra la plataforma, no supuestos.**
    `comportamiento_roles.Entorno()` resuelve User1..User4 y revienta si
    alguno no tiene EXACTAMENTE el rol de negocio esperado, o si quien corre
    esto no puede impersonar (`prvActOnBehalfOfAnotherUser`). Se guarda una
    sola vez en `ctx.extra["entorno"]`: todos los casos de abajo que
    impersonan lo reusan, así los cuatro roles se verifican una vez, no seis.

    **3. Datos de negocio y Excel** (fixture compartido `PR*`, create-if-
    missing) — igual que todos los sets migrados.

    **4. Una tanda propia**, tres Filas Validada bajo el fixture compartido,
    vía `ctx.procesar()` (E01). Varios casos de abajo (G-00b, G-04, G-05,
    G-10) necesitan al menos una Fila real; en vez de que cada uno je
    construya la suya (y de paso especular con si el entorno ya tenía algo
    de otro set corriendo en paralelo), esto lo deja listo una sola vez y
    los casos leen SUS PROPIAS filas por `_sanic_solicitudid_value`, nunca
    "la primera Fila que encuentre en el entorno".

    **Lo que NO prepara, y por qué:**

    - **El canon de configuración global** (§8.2): lo aplica `correr.py`
      antes y después de CADA set, no cada set por separado.
    - **Ningún dato nuevo para G-04**: el Cliente compartido `PR - Pruebas
      MPPP SA` YA es propiedad de la cuenta de servicio (no de User1..User4),
      así que no hace falta crear un Cliente aparte ni reasignar el
      propietario del compartido — ver el docstring del módulo.

    **5. El `sitemapxml` en vivo, leído UNA sola vez** (lo que traía
    `preparar()` de `sets/set_h.py`, fusionado el 2026-09-28): lo comparten
    G-14, G-16 y G-18 (sitemap), para no repetir la misma llamada de red tres
    veces. El CONTENIDO de ese XML (qué entradas tiene, en qué orden, con qué
    título) es lo que cada caso verifica, nunca algo que `preparar()` da por
    sentado: si faltara una entrada, el caso que la busca da FALLA, no tumba
    el set entero. Lo que SÍ es una precondición real (y por eso lanza si
    falla): que el objeto `sitemap` en sí exista en el entorno.
    """
    estado_sesiones.verificar_o_fallar(list(cr.ALIAS.keys()))

    pc.verificar_datos_negocio(ctx.dv, REMITENTE)
    pc.verificar_excels()

    ctx.extra["entorno"] = cr.Entorno()
    ctx.extra["sitemapxml"] = _leer_sitemapxml(ctx.dv)

    sid, mid, rc, rv = ctx.procesar(
        remitente=REMITENTE, asunto="Prueba G semilla (privilegios y vistas)",
        cantidad_adjuntos=1, cantidad_excel=1, excel_a_subir=pc.excel("E01-feliz-formato11.xlsx"))
    filas = sim.leer_filas(ctx.dv, sid)
    validadas = [f for f in filas if f.get("sanic_estado") == VALIDADA]
    if not validadas:
        raise RuntimeError(
            f"la tanda semilla (Solicitud {sid}) no dejó ninguna Fila en Validada "
            f"(filas={filas}); sin eso G-00b/G-04/G-05/G-10 no tienen nada que leer")
    ctx.extra["sid_semilla"] = sid
    ctx.extra["filas_semilla"] = filas


def cerrar(ctx):
    """Borra, por GUID, las Solicitudes que este set creó. No toca el fixture
    compartido `PR*`: eso no se borra nunca desde un set."""
    return ctx.limpiar_solicitudes()


# ===========================================================================
# G-00 — Los privilegios efectivos, los 4 roles (proxy: verificar_accesos.py)
# ===========================================================================
def g00(ctx):
    c = pc.Caso("G-00", "Los privilegios efectivos, antes de tocar la app (los 4 roles)")
    est, r, _ = ctx.dv.call(
        "GET", f"privileges?$select=privilegeid,name&$filter=contains(name,'{va.SOLUCION}')&$top=500")
    if est != 200:
        c.check("se pudo leer el catálogo de privilegios de la solución", False, f"HTTP {est}: {r}")
        return c
    privilegios_mppp = r.get("value", [])

    # `USUARIOS_POR_DEFECTO` de la herramienta es `["User1", "User2", "User3"]`:
    # un hueco real del MD viejo, que nunca pedía User4 (admin_tecnico), aunque
    # la matriz SÍ define privilegios para el Administrador técnico (`04` §2).
    for alias in ("User1", "User2", "User3", "User4"):
        inf = va.revisar(ctx.dv, alias, privilegios_mppp)
        c.check(f"{alias} ({inf['rol_mppp']!r}) coincide con la matriz, celda por celda, y abre la app",
                not inf["hallazgos"] and not inf["nota"],
                f"nota={inf['nota']!r} hallazgos={inf['hallazgos']}"[:500])
    return c


# ===========================================================================
# G-00b — El comportamiento de cada rol, contra el entorno (sin escritura)
# ===========================================================================
def g00b(ctx):
    c = pc.Caso("G-00b", "El comportamiento de cada rol, contra el entorno (sin escritura)")
    ent = ctx.extra["entorno"]
    t = cr.Tablero()
    cr.rechazos(ent, t)

    omitidos = []
    for estado, nombre, detalle in t.lineas:
        estado = estado.strip()
        if estado == "—":
            omitidos.append(f"{nombre} ({detalle})")
            continue
        c.check(nombre, estado == "OK", detalle)

    if omitidos:
        # Igual que hace la propia herramienta: "sin probar" no cuenta en
        # contra (falta una precondición de datos), pero se deja constancia,
        # sin pintarlo de verde en silencio.
        c.check(f"(informativo) {len(omitidos)} verificación(es) sin datos para correr: {omitidos}", True)

    if not c.checks:
        c.na("comportamiento_roles.rechazos() no dejó ninguna comprobación real corrida (todo sin datos)")

    # G-06 ("Lo que cada rol NO puede") queda documentado en el MD como
    # cubierto acá y por G-00: las mismas celdas que probaría por separado ya
    # las prueba `rechazos()` arriba (Fila/Solicitud del administrador de
    # planes, transición ilegítima, borrado) y la matriz de privilegios en sí
    # (G-00). Repetir la llamada sería probar la herramienta dos veces, no el
    # sistema dos veces (CONVENCIONES §2).
    return c


# ===========================================================================
# G-01 — El sitemap: las 15 entradas, cada una a su vista exacta
# ===========================================================================
# `05-app-model-driven.md` §2, nombradas una por una (id del playbook → tabla
# lógica → nombre de vista, o None si abre la vista POR DEFECTO de la tabla).
SITEMAP_ESPERADO = [
    ("subarea_pordigitar", "sanic_mppp_tbl_fila", "Mis clientes - por digitar"),
    ("subarea_devueltas", "sanic_mppp_tbl_fila", "Mis clientes - devueltas"),
    ("subarea_poraprobar", "sanic_mppp_tbl_fila", "Por aprobar"),
    ("subarea_paraatencionmanual", "sanic_mppp_tbl_fila", "Para atencion manual"),
    ("subarea_porclasificar", "sanic_mppp_tbl_solicitud", "Por clasificar"),
    ("subarea_pararevisar", "sanic_mppp_tbl_solicitud", "Para revisar"),
    ("subarea_solicitudes", "sanic_mppp_tbl_solicitud", "Solicitudes"),
    ("subarea_filas", "sanic_mppp_tbl_fila", "Filas"),
    ("subarea_clientes", "sanic_mppp_tbl_cliente", None),
    ("subarea_planes", "sanic_mppp_tbl_plan", None),
    ("subarea_autorizados", "sanic_mppp_tbl_autorizado", None),
    ("subarea_autorizaciones", "sanic_mppp_tbl_autorizacionplan", None),
    ("subarea_autorizacionescliente", "sanic_mppp_tbl_autorizacioncliente", None),
    ("subarea_parametros", "sanic_mppp_tbl_parametro", None),
    ("subarea_reglas", "sanic_mppp_tbl_regla", None),
]


def _correr_app_py(ctx):
    """Cachea la corrida de `app.py --solo-verificar`: G-01 y G-12 la usan
    las dos, y es la misma llamada (no escribe nada con `--solo-verificar`)."""
    if "app_py" in ctx.extra:
        return ctx.extra["app_py"]
    proc = subprocess.run(
        [sys.executable, APP_PY, PLAYBOOK_APP, "--solo-verificar"],
        cwd=_RAIZ, capture_output=True, text=True, timeout=180)
    ultima = (proc.stdout.strip().splitlines() or [""])[-1]
    try:
        datos = json.loads(ultima)
    except Exception:
        datos = None
    ctx.extra["app_py"] = (datos, proc)
    return ctx.extra["app_py"]


def g01(ctx):
    c = pc.Caso("G-01", "El sitemap: las 15 entradas, cada una a su vista exacta")
    texto = construir_comun.leer_texto(PLAYBOOK_APP)
    secciones = construir_comun.dividir_secciones(texto)
    try:
        datos = construir_comun.obtener_componente(secciones, "app")
    except Exception as e:
        c.check("el playbook del sitemap se pudo leer y parsear", False, f"{type(e).__name__}: {e}")
        return c

    observadas = []
    for area in datos["areas"]:
        for grupo in area["grupos"]:
            for e in grupo["entradas"]:
                observadas.append((e["id"], e["tabla"], e.get("vista")))

    c.check(f"el playbook declara exactamente {len(SITEMAP_ESPERADO)} entradas (nombradas, no contadas)",
            len(observadas) == len(SITEMAP_ESPERADO), f"declara {len(observadas)}: {[o[0] for o in observadas]}")

    por_id = {o[0]: (o[1], o[2]) for o in observadas}
    for id_, tabla, vista in SITEMAP_ESPERADO:
        real = por_id.get(id_)
        c.check(f"{id_!r} abre '{tabla}'" + (f" con la vista {vista!r}" if vista else " (vista por defecto)"),
                real == (tabla, vista), f"el playbook trae {real!r}")

    # La parte que el MD viejo daba por "nunca se pudo verificar": que la vista
    # DESPLEGADA en el entorno sea, hoy, exactamente la que el playbook pide
    # (con el savedqueryid resuelto EN VIVO, no el que tenía cuando se construyó).
    datos_app, proc = _correr_app_py(ctx)
    if datos_app is None:
        c.check("app.py --solo-verificar corrió y devolvió JSON", False,
                f"rc={proc.returncode} stdout={proc.stdout[-400:]!r} stderr={proc.stderr[-400:]!r}")
        return c
    c.check("el sitemap desplegado coincide EXACTO con el que pide el playbook (savedqueryid resuelto en vivo)",
            datos_app.get("estado") == "ya_existia", str(datos_app))
    detalle = str(datos_app.get("detalle", ""))
    c.check("el detalle nombra 15 entradas", "15 entradas" in detalle, detalle)
    return c


# ===========================================================================
# G-03 — Las 20 vistas existen y filtran
# ===========================================================================
# `05-app-model-driven.md` §3 (inventario 12.2, "hoy 20"). Nombre EXACTO tal
# como está construido en el entorno (verificado en vivo el 2026-09-27: las
# vistas propias del playbook nacen SIN tilde a propósito, a diferencia de
# las vistas nativas de la plataforma como "Autorización Advanced Find View",
# que sí la traen porque salen del DISPLAY NAME de la tabla).
VISTAS_ESPERADAS = [
    ("Mis clientes - por digitar", "sanic_mppp_tbl_fila"),
    ("Todos - por digitar", "sanic_mppp_tbl_fila"),
    ("Por aprobar", "sanic_mppp_tbl_fila"),
    ("Mis clientes - devueltas", "sanic_mppp_tbl_fila"),
    ("Terminadas", "sanic_mppp_tbl_fila"),
    ("Por clasificar", "sanic_mppp_tbl_solicitud"),
    ("Para revisar", "sanic_mppp_tbl_solicitud"),
    ("Solicitudes", "sanic_mppp_tbl_solicitud"),
    ("Clientes activos", "sanic_mppp_tbl_cliente"),
    ("Planes activos", "sanic_mppp_tbl_plan"),
    ("Autorizados activos", "sanic_mppp_tbl_autorizado"),
    ("Autorizaciones activas", "sanic_mppp_tbl_autorizacionplan"),
    ("Parametros activos", "sanic_mppp_tbl_parametro"),
    ("Reglas activas", "sanic_mppp_tbl_regla"),
    ("Autorizaciones sin evidencia", "sanic_mppp_tbl_autorizacionplan"),
    ("Correos autorizados sobre este plan", "sanic_mppp_tbl_autorizacionplan"),
    ("Planes que puede modificar", "sanic_mppp_tbl_autorizacionplan"),
    ("Correos autorizados sobre este cliente", "sanic_mppp_tbl_autorizacioncliente"),
    ("Clientes que puede modificar", "sanic_mppp_tbl_autorizacioncliente"),
    ("Autorizaciones por cliente activas", "sanic_mppp_tbl_autorizacioncliente"),
]

# Para las siete "más fáciles de leer mal" (05 §3) más "sin evidencia" (D-42):
# el fragmento de `fetchxml` que TIENE que estar, verificado contra el
# entorno el 2026-09-27 (ver el reporte de esta migración para el fetchxml
# completo de cada una).
FETCHXML_ESPERADO = {
    "Mis clientes - por digitar": [
        '<condition attribute="sanic_estado" operator="eq" value="159460003" />',
        '<condition attribute="ownerid" operator="eq-userid" />',
    ],
    "Todos - por digitar": [
        '<condition attribute="sanic_estado" operator="eq" value="159460003" />',
    ],
    "Por aprobar": [
        '<condition attribute="sanic_estado" operator="eq" value="159460004" />',
    ],
    "Mis clientes - devueltas": [
        '<condition attribute="sanic_estado" operator="eq" value="159460003" />',
        '<condition attribute="sanic_notainterna" operator="not-null" />',
        '<condition attribute="ownerid" operator="eq-userid" />',
    ],
    "Terminadas": [
        '<value>159460005</value>',   # Aprobada, un estado terminal
        '<value>159460007</value>',   # Anulada, otro estado terminal
    ],
    "Por clasificar": [
        '<condition attribute="sanic_estadoprocesamiento" operator="in">',
    ],
    "Para revisar": [
        '<condition attribute="sanic_requiererevision" operator="eq" value="True" />',
    ],
    "Autorizaciones sin evidencia": [
        '<condition attribute="sanic_documentofirmado" operator="null" />',
    ],
}

# Lo que "Terminadas" tiene que EXCLUIR (Validada/Digitada no son terminales).
FETCHXML_TERMINADAS_NO_INCLUYE = ['<value>159460003</value>', '<value>159460004</value>']


def g03(ctx):
    c = pc.Caso("G-03", "Las 20 vistas existen y filtran")
    vistas = {}
    for nombre, tabla in VISTAS_ESPERADAS:
        v = _vista(ctx.dv, tabla, nombre)
        c.check(f"la vista {nombre!r} existe sobre {tabla}", v is not None)
        if v:
            vistas[nombre] = v

    for nombre, fragmentos in FETCHXML_ESPERADO.items():
        v = vistas.get(nombre)
        if not v:
            continue  # ya quedó marcado arriba
        fx = v["fetchxml"]
        for frag in fragmentos:
            c.check(f"{nombre!r}: el fetchxml trae {frag!r}", frag in fx)

    v_term = vistas.get("Terminadas")
    if v_term:
        fx = v_term["fetchxml"]
        for frag in FETCHXML_TERMINADAS_NO_INCLUYE:
            c.check(f"'Terminadas': el fetchxml NO incluye {frag!r} (Validada/Digitada no son terminales)",
                    frag not in fx)
    return c


# ===========================================================================
# G-04 — La cartera del ejecutivo (RF-19, D-13)
# ===========================================================================
def g04(ctx):
    """CORREGIDO el 2026-09-27, en la propia jornada de la migración: la
    primera versión de este caso comparaba las filas por `sanic_nombre` (el
    número calculado `<solicitud>-F<NN>`), y dio FALLA dos corridas seguidas
    con el mismo síntoma ("faltaron" las tres de la tanda semilla en
    'Todos - por digitar'). Se investigó en vivo, sin confiar en la primera
    lectura ("otro agente en paralelo debe haber tocado mis filas"): se
    reprodujo el caso a mano, se pidió la fila devuelta por la vista COMPLETA
    (todas sus claves), y las tres SÍ estaban — la vista funciona bien. El
    bug era del caso: **ninguna de las dos vistas de Fila selecciona
    `sanic_nombre` como columna propia de Fila** (no está entre las columnas
    de trabajo del diseño, `05-app-model-driven.md` §3 — la columna "Nombre"
    de esa tabla es `sanic_nombrebeneficiario`, no el número calculado). El
    único `sanic_nombre` que SÍ viaja en el JSON es el de Cliente, bajo la
    clave `cliente.sanic_nombre` (viene del `<link-entity>`), y por eso la
    comparación daba vacía siempre, sin que la vista hubiera hecho nada malo.
    Corregido: se compara por `sanic_mppp_tbl_filaid` (el GUID), que las dos
    vistas SÍ traen siempre, sea cual sea el diseño de columnas de negocio."""
    c = pc.Caso("G-04", "La cartera del ejecutivo: 'Mis clientes' filtra por propietario del Cliente")
    ent = ctx.extra["entorno"]
    sid = ctx.extra["sid_semilla"]
    filas = ctx.extra["filas_semilla"]
    if not filas:
        c.na("la tanda semilla no dejó ninguna Fila (ver preparar())")
        return c

    est_ids, r_ids, _ = ctx.dv.call(
        "GET", f"{sim.FILAS}?$select=sanic_mppp_tbl_filaid&$filter=_sanic_solicitudid_value eq {sid}")
    if est_ids != 200 or not r_ids.get("value"):
        c.na(f"no se pudieron releer los GUID de la tanda semilla (HTTP {est_ids})")
        return c
    ids_semilla = {f["sanic_mppp_tbl_filaid"] for f in r_ids["value"]}

    v_mis = _vista(ctx.dv, "sanic_mppp_tbl_fila", "Mis clientes - por digitar")
    v_todos = _vista(ctx.dv, "sanic_mppp_tbl_fila", "Todos - por digitar")
    if not (v_mis and v_todos):
        c.na("no se pudo leer 'Mis clientes - por digitar' o 'Todos - por digitar' (ver G-03)")
        return c

    uid_ejecutivo = ent.usuarios["ejecutivo"]["systemuserid"]
    est_mis, r_mis, _ = _fetchxml(ctx.dv, "sanic_mppp_tbl_filas", v_mis["fetchxml"],
                                   cabeceras={"MSCRMCallerID": uid_ejecutivo})
    est_todos, r_todos, _ = _fetchxml(ctx.dv, "sanic_mppp_tbl_filas", v_todos["fetchxml"],
                                       cabeceras={"MSCRMCallerID": uid_ejecutivo})
    c.check("el Ejecutivo pudo ejecutar 'Mis clientes - por digitar'", est_mis == 200, f"HTTP {est_mis}: {r_mis}")
    c.check("el Ejecutivo pudo ejecutar 'Todos - por digitar'", est_todos == 200, f"HTTP {est_todos}: {r_todos}")
    if est_mis != 200 or est_todos != 200:
        return c

    ids_en_mis = {f.get("sanic_mppp_tbl_filaid") for f in r_mis.get("value", [])}
    ids_en_todos = {f.get("sanic_mppp_tbl_filaid") for f in r_todos.get("value", [])}

    c.check("las filas del Cliente compartido NO aparecen en 'Mis clientes' del Ejecutivo "
            "(el Cliente PR - Pruebas MPPP SA es propiedad de la cuenta de servicio, no de User1)",
            not (ids_semilla & ids_en_mis), f"aparecieron: {ids_semilla & ids_en_mis}")
    c.check("esas mismas filas SÍ aparecen en 'Todos - por digitar' (es comodidad, no seguridad)",
            ids_semilla <= ids_en_todos, f"faltaron: {ids_semilla - ids_en_todos}")
    return c


# ===========================================================================
# G-05 — Perfil de seguridad de columna. EL CASO MÁS IMPORTANTE DEL SET.
# ===========================================================================
def g05(ctx):
    """Lee `sanic_numeroidentificacion`/`sanic_numerocuenta` impersonando a
    los 4 roles, directo por Web API — sin formulario, sin navegador.

    **Por qué NO alcanza con "admin_planes no ve la cuenta" a secas**
    (`04-matriz-privilegios.md` §5, la advertencia sobre la prueba más fácil
    de leer mal). Verificado en vivo el 2026-09-27: `admin_planes` y
    `admin_tecnico` NO llegan ni siquiera a evaluar el perfil de columna —
    Dataverse los frena ANTES, con un `403` porque ninguno de los dos tiene
    `prvReadsanic_mppp_tbl_fila` (04 §2: ninguno de los dos lee Fila). El
    perfil de columna (`CSP - MPPP - Datos sensibles`) es una barrera
    SEGUNDA que hoy nunca se llega a ejercitar sola con los 4 roles humanos,
    porque los dos únicos roles con `R` sobre Fila (Ejecutivo y Supervisor)
    son también los dos que están en el perfil. Este caso lo deja escrito
    así, con el código HTTP real de cada uno, en vez de repetir la lectura
    ambigua: "no ve la cuenta" es cierto para los 4, pero por DOS mecanismos
    distintos, y solo importa el de columna para los que además leen Fila.
    """
    c = pc.Caso("G-05", "Perfil de seguridad de columna: identificación y cuenta")
    ent = ctx.extra["entorno"]
    filas = ctx.extra["filas_semilla"]
    if not filas:
        c.na("la tanda semilla no dejó ninguna Fila (ver preparar())")
        return c
    sid = ctx.extra["sid_semilla"]
    est, r, _ = ctx.dv.call(
        "GET", f"{sim.FILAS}?$select=sanic_mppp_tbl_filaid&$filter=_sanic_solicitudid_value eq {sid}&$top=1")
    if est != 200 or not r.get("value"):
        c.na(f"no se pudo releer una Fila de la tanda semilla (HTTP {est})")
        return c
    fid = r["value"][0]["sanic_mppp_tbl_filaid"]

    for papel in ("ejecutivo", "supervisor"):
        e, b, _ = ent.como(papel, "GET", f"{sim.FILAS}({fid})?$select=sanic_numeroidentificacion,sanic_numerocuenta")
        tiene = e == 200 and b.get("sanic_numerocuenta") and b.get("sanic_numeroidentificacion")
        c.check(f"{papel} SÍ ve identificación y cuenta (HTTP {e})", tiene, str(b)[:200])

    for papel in ("admin_planes", "admin_tecnico"):
        e, b, _ = ent.como(papel, "GET", f"{sim.FILAS}({fid})?$select=sanic_numeroidentificacion,sanic_numerocuenta")
        c.check(f"{papel} NO ve datos bancarios (bloqueado con HTTP {e}, antes de llegar a la columna)",
                e in (401, 403), str(b)[:200])
    return c


# ===========================================================================
# G-08 — Los 10 formularios
# ===========================================================================
FORMULARIOS_ESPERADOS = [
    ("sanic_mppp_tbl_solicitud", "Solicitud"),
    ("sanic_mppp_tbl_fila", "Fila"),
    ("sanic_mppp_tbl_cliente", "Cliente"),
    ("sanic_mppp_tbl_plan", "Plan"),
    ("sanic_mppp_tbl_autorizado", "Autorizado"),
    ("sanic_mppp_tbl_autorizacionplan", "Autorizacion"),
    ("sanic_mppp_tbl_autorizacioncliente", "Autorizacion por cliente"),
    ("sanic_mppp_tbl_parametro", "Parametro"),
    ("sanic_mppp_tbl_regla", "Regla"),
    ("sanic_mppp_tbl_motivoaccion", "Motivo de accion"),
]

SUBGRILLAS_SOLICITUD = ["sanic_mppp_tbl_fila", "sanic_mppp_tbl_resultadoregla", "sanic_mppp_tbl_bitacora"]


def _formxml(dv, tabla, nombre):
    est, r, _ = dv.call("GET", f"systemforms?$select=formxml&$filter="
                                f"objecttypecode eq '{tabla}' and type eq 2 and name eq '{nombre}'")
    filas = r.get("value", []) if est == 200 else []
    return filas[0]["formxml"] if len(filas) == 1 else None


def g08(ctx):
    c = pc.Caso("G-08", "Los 10 formularios, nombrados uno por uno")
    formularios = {}
    for tabla, nombre in FORMULARIOS_ESPERADOS:
        x = _formxml(ctx.dv, tabla, nombre)
        c.check(f"el formulario principal {nombre!r} existe sobre {tabla}", x is not None)
        if x:
            formularios[tabla] = x

    x_sol = formularios.get("sanic_mppp_tbl_solicitud")
    if x_sol:
        for tabla_rel in SUBGRILLAS_SOLICITUD:
            c.check(f"Solicitud trae la subgrilla de {tabla_rel}",
                    f"<TargetEntityType>{tabla_rel}</TargetEntityType>" in x_sol)

    x_ap = formularios.get("sanic_mppp_tbl_autorizacionplan")
    if x_ap:
        c.check("Autorización (AutorizacionPlan) trae 'Documento firmado' habilitado",
                'control id="sanic_documentofirmado"' in x_ap and 'disabled="true"' not in
                x_ap[x_ap.find('control id="sanic_documentofirmado"'):x_ap.find('control id="sanic_documentofirmado"') + 120])
        c.check("Autorización (AutorizacionPlan) trae 'Fecha del documento'", "sanic_fechadocumento" in x_ap)

    x_ac = formularios.get("sanic_mppp_tbl_autorizacioncliente")
    if x_ac:
        # CORREGIDO el 2026-09-27, misma jornada: esta aserción decía "NO
        # trae evidencia", citando `05-app-model-driven.md` §4 ("sin sección
        # de evidencia... a diferencia de Autorización"). Esa celda del
        # diseño quedó VIEJA el mismo día en que se escribió este caso: el
        # aprobador decidió, esa misma jornada, que la autorización por
        # cliente SÍ lleva el mismo respaldo firmado que la de plan
        # ("no está la columna de archivo adjunto para adjuntar la
        # autorización por escrito y la fecha de ese documento, al igual que
        # en autorización por plan") — porque autorizar por cliente habilita
        # TODOS los planes de ese cliente a la vez, así que es MÁS amplio que
        # autorizar por plan, y no tiene sentido que exija MENOS respaldo.
        # Se agregaron `sanic_documentofirmado` (archivo, 10 MB) y
        # `sanic_fechadocumento` (fecha) a la TABLA y al FORMULARIO
        # (`diseno/02-diccionario-datos.md` §2.4b, `diseno/06-inventario-
        # componentes.md` 3.13, `diseno/DIRECTRICES-NUEVAS.md`), y
        # `diseno/05-app-model-driven.md` §4 ya se corrigió (por el
        # aprobador, no acá: este set no toca `diseno/`).
        #
        # La aserción vieja (que este mismo caso traía) verificaba contra un
        # documento desactualizado y por eso acusó a un sistema CORRECTO de
        # un defecto que no existía — la misma familia de error que ya costó
        # caro con los conteos ("12 parámetros", "8 steps"): un dato que no
        # se vuelve a mirar contra la plataforma envejece y nadie lo nota.
        # Corregido: ahora se verifica que la evidencia SÍ esté, en los dos
        # niveles (metadata de la tabla, ya implícito en que el `formxml` la
        # pueda mostrar; y el propio `formxml`, con el control habilitado).
        c.check("AutorizacionCliente SÍ trae 'Documento firmado' habilitado (decisión del aprobador, 2026-09-27: "
                "la misma evidencia que AutorizacionPlan, porque autorizar por cliente es MÁS amplio, no menos)",
                'control id="sanic_documentofirmado"' in x_ac and 'disabled="true"' not in
                x_ac[x_ac.find('control id="sanic_documentofirmado"'):x_ac.find('control id="sanic_documentofirmado"') + 120])
        c.check("AutorizacionCliente SÍ trae 'Fecha del documento' (misma decisión)",
                "sanic_fechadocumento" in x_ac)
        c.check("AutorizacionCliente trae sus tres lookups (correo, autorizado, cliente)",
                all(campo in x_ac for campo in ("sanic_nombre", "sanic_autorizadoid", "sanic_clienteid")))

    x_mot = formularios.get("sanic_mppp_tbl_motivoaccion")
    if x_mot:
        c.check("Motivo de acción trae el campo Motivo", "sanic_motivo" in x_mot)

    return c


# ===========================================================================
# G-09 — La vista "Autorizaciones sin evidencia" (D-42)
# ===========================================================================
def g09(ctx):
    c = pc.Caso("G-09", "La vista 'Autorizaciones sin evidencia' (D-42): lista de tareas, no de inválidas")
    ent = ctx.extra["entorno"]
    v = _vista(ctx.dv, "sanic_mppp_tbl_autorizacionplan", "Autorizaciones sin evidencia")
    if v is None:
        c.na("no existe la vista 'Autorizaciones sin evidencia' (ver G-03)")
        return c

    uid_admin = ent.usuarios["admin_planes"]["systemuserid"]
    est, r, _ = _fetchxml(ctx.dv, "sanic_mppp_tbl_autorizacionplans", v["fetchxml"],
                           cabeceras={"MSCRMCallerID": uid_admin})
    c.check("el Administrador de planes pudo ejecutar 'Autorizaciones sin evidencia'", est == 200, f"HTTP {est}: {r}")
    if est != 200:
        return c

    nombres = {a.get("sanic_nombre") for a in r.get("value", [])}
    esperado = f"{REMITENTE} → PR11"
    c.check(f"la vista incluye la autorización sin evidencia del fixture ({esperado!r}, ninguna trae documento)",
            esperado in nombres, f"vio {len(nombres)} autorizaciones sin evidencia")

    c.check("una autorización sin evidencia SÍ autoriza igual (D-42): ya lo prueba cualquier caso de C/S/R "
            "corrido por corredor.py — todas sus autorizaciones nacen sin documento (ver preparar_datos.py)", True)
    return c


# ===========================================================================
# G-10 — La campana de notificaciones (DA-08)
# ===========================================================================
def g10(ctx):
    c = pc.Caso("G-10", "La campana de notificaciones: una por solicitud y por tanda (DA-08)")
    t0 = _ahora_iso()
    sid, mid, rc, rv = ctx.procesar(
        remitente=REMITENTE, asunto="Prueba G-10 notificaciones",
        cantidad_adjuntos=1, cantidad_excel=1, excel_a_subir=pc.excel("E01-feliz-formato11.xlsx"))
    s = sim.leer_solicitud(ctx.dv, sid)
    numero = s.get("sanic_nombre") or ""
    filas = sim.leer_filas(ctx.dv, sid)
    c.check(f"la tanda de prueba dejó filas por digitar (Solicitud {numero})", bool(filas) and bool(numero))
    if not (filas and numero):
        return c

    time.sleep(5)  # el plugin posterior a la validación crea la notificación; da un margen real
    est, r, _ = ctx.dv.call(
        "GET", f"appnotifications?$select=title,body,data,ownerid,createdon&$filter=createdon ge {t0}")
    if est != 200:
        c.check("se pudo leer appnotifications", False, f"HTTP {est}: {r}")
        return c

    relevantes = [n for n in r.get("value", [])
                  if numero in ((n.get("title") or "") + (n.get("body") or "") + (n.get("data") or ""))]
    c.check(f"se creó EXACTAMENTE 1 notificación al dejar {len(filas)} filas por digitar de la Solicitud "
            f"{numero} (una por tanda, DA-08 — nunca una por fila)",
            len(relevantes) == 1, f"aparecieron {len(relevantes)}: {relevantes}")

    c.check("el resto (que la notificación abra la vista/Solicitud correcta al tocarla) es GUI: "
            "ver G-10 en el MD para el comando exacto", True)
    return c


# ===========================================================================
# G-12 — La app valida limpia (ValidateApp)
# ===========================================================================
def g12(ctx):
    c = pc.Caso("G-12", "La app valida limpia (ValidateApp)")
    datos, proc = _correr_app_py(ctx)
    if datos is None:
        c.check("app.py --solo-verificar corrió y devolvió JSON", False,
                f"rc={proc.returncode} stdout={proc.stdout[-400:]!r} stderr={proc.stderr[-400:]!r}")
        return c
    c.check("estado = ya_existia", datos.get("estado") == "ya_existia", str(datos))
    detalle = str(datos.get("detalle", ""))
    c.check("ValidateApp sin errores", "sin errores" in detalle, detalle)
    for fragmento in ("4 areas y 15 entradas", "12 tablas", "4 roles"):
        c.check(f"el detalle nombra {fragmento!r}", fragmento in detalle, detalle)
    return c


# ===========================================================================
# G-13 (NUEVO) — La frontera con el RPA: el rol no trae privilegios de
# negocio ni el perfil de columna. Ver el docstring del módulo para la
# frontera con el set B.
# ===========================================================================
def g13(ctx):
    c = pc.Caso("G-13", "El rol del RPA no trae privilegios de negocio ni el perfil de columna (D-9/D-44)")
    est, r, _ = ctx.dv.call("GET", f"roles?$select=roleid,name&$filter=name eq '{ROL_RPA}'")
    filas = r.get("value", []) if est == 200 else []
    if len(filas) != 1:
        c.check(f"el rol {ROL_RPA!r} está instalado (inventario 6.7)", False, f"HTTP {est}: {filas}")
        return c
    rid = filas[0]["roleid"]

    est2, r2, _ = ctx.dv.call("GET", f"roles({rid})/roleprivileges_association?$select=name&$top=5000")
    if est2 != 200:
        c.check(f"se pudieron leer los privilegios de {ROL_RPA!r}", False, f"HTTP {est2}: {r2}")
        return c
    nombres_priv = {p["name"] for p in r2.get("value", [])}
    de_negocio = sorted(set(va.todos_los_privilegios()) & nombres_priv)
    c.check(f"{ROL_RPA!r} no trae NINGUNO de los privilegios de negocio de la matriz "
            f"({len(nombres_priv)} privilegios en total, 0 esperados sobre las tablas de `04`)",
            not de_negocio, f"trae de más: {de_negocio}")

    est3, r3, _ = ctx.dv.call(
        "GET", f"systemusers?$select=systemuserid,fullname&$filter="
               f"systemuserroles_association/any(x:x/roleid eq {rid}) and applicationid ne null")
    usuarios_rpa = r3.get("value", []) if est3 == 200 else []
    c.check(f"hay exactamente un usuario de aplicación con el rol {ROL_RPA!r}", len(usuarios_rpa) == 1,
            f"HTTP {est3}: {usuarios_rpa}")
    if len(usuarios_rpa) == 1:
        uid = usuarios_rpa[0]["systemuserid"]
        _, _, ids_equipos = va.roles_del_usuario(ctx.dv, uid)
        perfiles = va.perfiles_de_columna(ctx.dv, uid, ids_equipos)
        nombres_perfiles = [n for n, _, _ in perfiles]
        c.check(f"el usuario de aplicación del RPA NO está en {CSP_DATOS_SENSIBLES!r} "
                "(decisión del aprobador 2026-09-25: el bot nunca lee identificación/cuenta por esa vía)",
                CSP_DATOS_SENSIBLES not in nombres_perfiles, f"perfiles: {nombres_perfiles}")

    c.check("la PUERTA de las Custom API del RPA (quién pasa y quién no) es del set B "
            "(09-rpa-fase2.md, B-01/B-02/B-03) — este caso no la repite, audita la FORMA del rol", True)
    return c


# ===========================================================================
# G-14 a G-19 — sitemap, columna de trabajo, formulario y componentes de la app. El ex set H
# (`13-construido-hoy.md`, fusionado el 2026-09-28: CONVENCIONES.md §4, "La regla del nombre" —
# H se nombraba por la FECHA en que se escribió, no por lo que prueba). `set_h.py` desapareció;
# estas seis funciones son `h01..h05,h07` renombradas `g14..g19`, mismo cuerpo, IDs y cruces
# internos actualizados (el séptimo caso de H, `H-06`, no es de este tema: pasó a `A-15` en
# `set_a.py`).
# ===========================================================================
def g14(ctx):
    c = pc.Caso("G-14", "Sitemap: 'Para atencion manual' en Trabajo, entre 'Por aprobar' y 'Por clasificar'")
    root = ET.fromstring(ctx.extra["sitemapxml"])
    area = _area_por_titulo_1033(root, "Trabajo")
    if area is None:
        c.check("existe el area con titulo 1033 = 'Trabajo' en el sitemap en vivo", False)
        return c

    ids = [s.get("Id") for s in area.findall(".//SubArea")]
    c.check(
        "en el area Trabajo, 'subarea_paraatencionmanual' esta EXACTAMENTE entre "
        "'subarea_poraprobar' y 'subarea_porclasificar' (consecutivas, sin nada en medio)",
        _en_orden_consecutivo(ids, ["subarea_poraprobar", "subarea_paraatencionmanual", "subarea_porclasificar"]),
        f"orden real de subareas en Trabajo: {ids}")

    por_id = _subareas_por_id(area)
    entrada = por_id.get("subarea_paraatencionmanual")
    if entrada is None:
        c.check("la entrada 'subarea_paraatencionmanual' existe en el sitemap en vivo", False, f"ids presentes: {ids}")
        return c

    titulos = _titulos_de(entrada)
    c.check("titulo 1033 = 'Para atencion manual'", titulos.get("1033") == "Para atencion manual", titulos)
    c.check("titulo 3082 = 'Para atencion manual'", titulos.get("3082") == "Para atencion manual", titulos)

    url = entrada.get("Url") or ""
    c.check("la entrada abre la tabla sanic_mppp_tbl_fila (Url con etn=sanic_mppp_tbl_fila)",
            "etn=sanic_mppp_tbl_fila" in url, url)

    try:
        vista = _savedquery(ctx, "Para atencion manual", TABLA_FILA)
    except RuntimeError as e:
        c.check("la vista 'Para atencion manual' (la que la entrada dice abrir) existe sobre Fila", False, str(e))
        return c
    guid_vista = (vista["savedqueryid"] or "").replace("-", "").lower()
    url_normalizada = url.replace("-", "").lower()
    c.check("la Url del sitemap apunta al savedqueryid REAL de la vista 'Para atencion manual' "
            "(no una vista distinta ni un id inventado)", guid_vista and guid_vista in url_normalizada, url)
    return c


def g15(ctx):
    c = pc.Caso("G-15", "Columna 'Requiere atencion manual' en las 4 vistas de trabajo, y en ninguna otra")
    for nombre in VISTAS_DE_TRABAJO:
        try:
            vista = _savedquery(ctx, nombre, TABLA_FILA)
        except RuntimeError as e:
            c.check(f"{nombre!r}: la vista existe", False, str(e))
            continue
        celdas = _celdas_del_layout(vista["layoutxml"])
        atributos = _atributos_del_fetch(vista["fetchxml"])
        c.check(f"{nombre!r}: el layoutxml trae la celda {COLUMNA_ATENCION_MANUAL!r}",
                COLUMNA_ATENCION_MANUAL in celdas, celdas)
        c.check(f"{nombre!r}: el fetchxml trae el atributo {COLUMNA_ATENCION_MANUAL!r}",
                COLUMNA_ATENCION_MANUAL in atributos, atributos)
        if COLUMNA_ATENCION_MANUAL in celdas and ANCLA_NUMEROFILA in celdas:
            c.check(f"{nombre!r}: {COLUMNA_ATENCION_MANUAL!r} va inmediatamente despues de {ANCLA_NUMEROFILA!r}",
                    celdas.index(COLUMNA_ATENCION_MANUAL) == celdas.index(ANCLA_NUMEROFILA) + 1, celdas)

    for nombre in VISTAS_SIN_LA_COLUMNA:
        try:
            vista = _savedquery(ctx, nombre, TABLA_FILA)
        except RuntimeError as e:
            c.check(f"{nombre!r}: la vista existe", False, str(e))
            continue
        celdas = _celdas_del_layout(vista["layoutxml"])
        c.check(f"{nombre!r}: NO trae la celda {COLUMNA_ATENCION_MANUAL!r} (no es vista de trabajo)",
                COLUMNA_ATENCION_MANUAL not in celdas, celdas)
    return c


def g16(ctx):
    c = pc.Caso("G-16", "Catalogos: 'Correos Autorizados', 'Autorizaciones - Planes' y 'Autorizaciones - Clientes'")
    root = ET.fromstring(ctx.extra["sitemapxml"])
    area = _area_por_titulo_1033(root, "Catalogos")
    if area is None:
        c.check("existe el area con titulo 1033 = 'Catalogos' en el sitemap en vivo", False)
        return c
    por_id = _subareas_por_id(area)

    autorizados = por_id.get("subarea_autorizados")
    if autorizados is None:
        c.check("existe 'subarea_autorizados'", False)
    else:
        t = _titulos_de(autorizados)
        c.check("'subarea_autorizados': titulo 1033 = 'Correos Autorizados' (renombrado de 'Autorizados')",
                t.get("1033") == "Correos Autorizados", t)
        c.check("'subarea_autorizados': titulo 3082 = 'Correos Autorizados'", t.get("3082") == "Correos Autorizados", t)
        c.check("'subarea_autorizados' abre sanic_mppp_tbl_autorizado",
                autorizados.get("Entity") == TABLA_AUTORIZADO, autorizados.get("Entity"))

    autorizaciones = por_id.get("subarea_autorizaciones")
    if autorizaciones is None:
        c.check("existe 'subarea_autorizaciones'", False)
    else:
        t = _titulos_de(autorizaciones)
        c.check("'subarea_autorizaciones': titulo 1033 = 'Autorizaciones - Planes' (renombrado de 'Autorizaciones')",
                t.get("1033") == "Autorizaciones - Planes", t)
        c.check("'subarea_autorizaciones': titulo 3082 = 'Autorizaciones - Planes'", t.get("3082") == "Autorizaciones - Planes", t)
        c.check("'subarea_autorizaciones' abre sanic_mppp_tbl_autorizacionplan",
                autorizaciones.get("Entity") == TABLA_AUTORIZACIONPLAN, autorizaciones.get("Entity"))

    autorizacionescliente = por_id.get("subarea_autorizacionescliente")
    if autorizacionescliente is None:
        c.check("existe la entrada NUEVA 'subarea_autorizacionescliente'", False)
    else:
        t = _titulos_de(autorizacionescliente)
        c.check("'subarea_autorizacionescliente': titulo 1033 = 'Autorizaciones - Clientes'",
                t.get("1033") == "Autorizaciones - Clientes", t)
        c.check("'subarea_autorizacionescliente': titulo 3082 = 'Autorizaciones - Clientes'",
                t.get("3082") == "Autorizaciones - Clientes", t)
        c.check("'subarea_autorizacionescliente' abre sanic_mppp_tbl_autorizacioncliente (DN-01)",
                autorizacionescliente.get("Entity") == TABLA_AUTORIZACIONCLIENTE, autorizacionescliente.get("Entity"))

    if autorizaciones is not None and autorizacionescliente is not None:
        c.check("son DOS entradas separadas que abren tablas DISTINTAS (no la misma vista duplicada)",
                autorizaciones.get("Entity") != autorizacionescliente.get("Entity"),
                (autorizaciones.get("Entity"), autorizacionescliente.get("Entity")))

    # Cada una abre, por Entity=, la vista POR DEFECTO de su propia tabla: que
    # esa vista por defecto sea la correcta se confirma contra savedqueries en
    # vivo (no alcanza con que el sitemap "apunte a la tabla").
    for tabla, nombre_vista_default in ((TABLA_AUTORIZACIONPLAN, "Autorizaciones activas"),
                                         (TABLA_AUTORIZACIONCLIENTE, "Autorizaciones por cliente activas")):
        est, r, _ = ctx.dv.call(
            "GET", "savedqueries?$select=name&$filter="
                   f"returnedtypecode eq '{tabla}' and isdefault eq true and querytype eq 0")
        filas = r.get("value", []) if est == 200 else []
        c.check(f"la vista por defecto de {tabla!r} es {nombre_vista_default!r}",
                len(filas) == 1 and filas[0].get("name") == nombre_vista_default, filas)
    return c


def g17(ctx):
    c = pc.Caso("G-17", "Formulario 'Autorizacion por cliente': encabezado (2 lookups) + seccion de evidencia")
    est, r, _ = ctx.dv.call(
        "GET", "systemforms?$select=formid,name,formxml&$filter="
               f"objecttypecode eq '{TABLA_AUTORIZACIONCLIENTE}' and type eq {FORMULARIO_PRINCIPAL}")
    filas = r.get("value", []) if est == 200 else []
    if len(filas) != 1:
        c.check("hay exactamente un formulario Main de AutorizacionCliente", False, f"HTTP {est}: {filas}")
        return c

    root = ET.fromstring(filas[0]["formxml"])

    header = root.find("header")
    if header is None:
        c.check("el formulario tiene encabezado (<header>)", False)
    else:
        controles = _controles_con_campo(header)
        for campo in ("sanic_nombre", "sanic_autorizadoid", "sanic_clienteid"):
            c.check(f"el encabezado trae el campo {campo!r}", campo in controles, sorted(controles))
        for campo in ("sanic_autorizadoid", "sanic_clienteid"):
            c.check(f"{campo!r} es un control de tipo Lookup (classid {CLASSID_LOOKUP})",
                    controles.get(campo) == CLASSID_LOOKUP, controles.get(campo))

    seccion = _seccion_por_titulo(root, "Evidencia de la autorizacion")
    if seccion is None:
        c.check("existe la seccion separada 'Evidencia de la autorizacion' (no mezclada con el encabezado)", False)
    else:
        controles_evidencia = _controles_con_campo(seccion)
        c.check("la seccion 'Evidencia de la autorizacion' trae sanic_documentofirmado",
                "sanic_documentofirmado" in controles_evidencia, sorted(controles_evidencia))
        c.check("la seccion 'Evidencia de la autorizacion' trae sanic_fechadocumento",
                "sanic_fechadocumento" in controles_evidencia, sorted(controles_evidencia))
    return c


def g18(ctx):
    c = pc.Caso("G-18", "El icono es reutilizado, no uno nuevo (mismo web resource en el sitemap en vivo)")
    root = ET.fromstring(ctx.extra["sitemapxml"])
    por_id = {}
    for area in root.findall("Area"):
        por_id.update(_subareas_por_id(area))

    pararevisar, paraatencionmanual = por_id.get("subarea_pararevisar"), por_id.get("subarea_paraatencionmanual")
    if pararevisar is None or paraatencionmanual is None:
        c.check("existen 'subarea_pararevisar' y 'subarea_paraatencionmanual' para comparar su icono", False,
                {"pararevisar": pararevisar is not None, "paraatencionmanual": paraatencionmanual is not None})
    else:
        c.check("'Para atencion manual' reusa el icono de 'Para revisar' (mismo web resource, "
                "sin crear uno nuevo): /WebResources/sanic_mppp_wr_svg_pararevisar",
                paraatencionmanual.get("Icon") == pararevisar.get("Icon") == "/WebResources/sanic_mppp_wr_svg_pararevisar",
                {"pararevisar": pararevisar.get("Icon"), "paraatencionmanual": paraatencionmanual.get("Icon")})

    autorizaciones, autorizacionescliente = por_id.get("subarea_autorizaciones"), por_id.get("subarea_autorizacionescliente")
    if autorizaciones is None or autorizacionescliente is None:
        c.check("existen 'subarea_autorizaciones' y 'subarea_autorizacionescliente' para comparar su icono", False,
                {"autorizaciones": autorizaciones is not None, "autorizacionescliente": autorizacionescliente is not None})
    else:
        c.check("'Autorizaciones - Clientes' reusa el icono de 'Autorizaciones - Planes' (mismo web resource, "
                "sin crear uno nuevo): /WebResources/sanic_mppp_wr_svg_autorizaciones",
                autorizacionescliente.get("Icon") == autorizaciones.get("Icon") == "/WebResources/sanic_mppp_wr_svg_autorizaciones",
                {"autorizaciones": autorizaciones.get("Icon"), "autorizacionescliente": autorizacionescliente.get("Icon")})
    return c


def g19(ctx):
    c = pc.Caso("G-19", "sanic_mppp_tbl_autorizacioncliente es un componente REAL de la app (no solo del sitemap)")

    # 1) La herramienta del proyecto, en modo lectura pura (nunca escribe con
    #    --solo-verificar): confirma que el sitemapxml y los componentes en
    #    vivo coinciden con el playbook.
    ruta_playbook = os.path.join(_RAIZ, "playbooks", "app", "mantenimientoppp.md")
    proc = subprocess.run(
        [sys.executable, os.path.join(_RAIZ, "herramientas", "construir", "app.py"), ruta_playbook, "--solo-verificar"],
        cwd=_RAIZ, capture_output=True, text=True, timeout=120)
    ultima = (proc.stdout.strip().splitlines() or [""])[-1]
    try:
        datos = json.loads(ultima)
    except Exception:
        c.check("app.py --solo-verificar corrió y devolvió JSON", False,
                f"rc={proc.returncode} stdout={proc.stdout[-400:]!r} stderr={proc.stderr[-400:]!r}")
        return c
    c.check("app.py --solo-verificar dice 'ya_existia' (sitemapxml y componentes en vivo == playbook)",
            datos.get("estado") == "ya_existia", str(datos))

    # 2) Independiente de la herramienta (CONVENCIONES §2): RetrieveAppComponents
    #    EN VIVO, releído por este mismo caso, buscando la tabla POR NOMBRE.
    est, r, _ = ctx.dv.call(
        "GET", "appmodules/Microsoft.Dynamics.CRM.RetrieveUnpublishedMultiple()"
               f"?$select=appmoduleid&$filter=uniquename eq '{APP_UNIQUE}'")
    filas = r.get("value", []) if est == 200 else []
    if len(filas) != 1:
        c.check("la app 'MDA - MPPP - Mantenimiento PPP' existe", False, f"HTTP {est}: {filas}")
        return c
    try:
        nombres = _tablas_de_la_app(ctx, filas[0]["appmoduleid"])
    except RuntimeError as e:
        c.check("se pudo leer RetrieveAppComponents de la app", False, str(e))
        return c
    c.check("'sanic_mppp_tbl_autorizacioncliente' aparece POR NOMBRE en RetrieveAppComponents "
            "(el import manual de la solución la agregó de verdad, no solo el sitemap)",
            TABLA_AUTORIZACIONCLIENTE in nombres, sorted(nombres))
    return c


CASOS = [g00, g00b, g01, g03, g04, g05, g08, g09, g10, g12, g13, g14, g15, g16, g17, g18, g19]
