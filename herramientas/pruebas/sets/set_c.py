#!/usr/bin/env python3
"""Set C — Clasificación del correo (`pruebas/01-clasificacion-del-correo.md`).

Nivel **Correo**: `sanic_mppp_capi_clasificarcorreo` y las dos reglas que vive ahí,
`ES_CORREO_NUEVO` y `REMITENTE_RECONOCIDO` — camino feliz, respuesta, reenvío,
remitente no reconocido, duplicados, sin adjunto (C-01..C-08, migrados de
`corredor.py` sin reescribirlos: ya estaban probados) y, nuevo de fase 2,
DN-01 (`diseno/DIRECTRICES-NUEVAS.md`): el parámetro `autorizacion.modo` cambia
DÓNDE `REMITENTE_RECONOCIDO` busca la autorización (C-09, C-10, C-11). El
contrato de un módulo de set está en `sets/__init__.py`; la preparación
compartida, en `preparacion_comun.py`.

**Por qué DN-01 toca este archivo y no otro.** `Datos/Catalogos.cs` deja
trazado, con archivo y línea, que `PlanesAutorizadosDe` tiene EXACTAMENTE dos
llamadores: `ClasificarCorreo.cs:242` (`REMITENTE_RECONOCIDO`, nivel Correo —
ESTE archivo) y `ValidarSolicitud.cs:246` (`AUTORIZACION_CORREO_PLAN`, nivel
Registro — `03-reglas-de-registro.md`, letra R). El resto de DN-01 (la tabla
`AutorizacionCliente`, el nombre calculado, la evidencia firmada, las
subgrillas de la app, la clave alternativa) NO es nivel Correo: ya lo cubre
`pruebas/10-autorizacion-cliente.md` (letra `U`, todavía sin migrar a
`sets/`). Este set agrega SOLO lo que le corresponde por CONVENCIONES §4 (un
ID por archivo, sin colisiones): que el "portero" (`REMITENTE_RECONOCIDO`)
tamibén resuelva el modo — el bug real que el aprobador corrigió el
2026-09-26 ("el portero también tiene que preguntar lo mismo").

**Fase 2, lo demás**: investigado el 2026-09-27. DN-02 (exportar a Excel) y
DN-03 (obligatoriedad por gestión) son nivel Fila/Registro, no Correo — la
justificación caso por caso está en la sección "Fase 2" de
`pruebas/01-clasificacion-del-correo.md` (el MD manda; acá solo se registra
que la conclusión ya se tomó).

**C-05/C-06, de N-A a API (2026-09-28).** El aprobador dio acceso real al buzón de ingesta
(`mppp@55xljh.onmicrosoft.com`) al service principal de Graph — hasta hoy solo tenía los 4 buzones
de prueba. `c05()` manda el ÚNICO correo real de todo este archivo (por `graph_api.py`, desde
`PP_MAIL_BUZON_1`) y `c06()` lee, de la MISMA Solicitud que resultó, los dos campos de nombre de
archivo — ambos por Web API, sin navegador. Ver el docstring de cada caso para el detalle; la
corrección de fondo (por qué C-06 YA NO espera que el `.eml` lleve "el nombre real de Outlook") está
en el propio `c06()` y en el MD.
"""
import os
import sys
import time
import urllib.parse
import uuid

_AQUI = os.path.dirname(os.path.abspath(__file__))
_HERRAMIENTAS_PRUEBAS = os.path.dirname(_AQUI)
sys.path.insert(0, _HERRAMIENTAS_PRUEBAS)

import preparacion_comun as pc  # noqa: E402
import simulador as sim  # noqa: E402
import preparar_datos as prep  # noqa: E402
from graph_api import Graph  # noqa: E402 (solo lo usan c05()/c06(), correo real hacia el buzón de ingesta)

LETRA = "C"
NOMBRE = "Clasificación del correo"

# Remitentes propios de este set. Nunca los de una persona real (CONVENCIONES
# §5): los mismos dos que ya usaba `corredor.py` (se migran sin cambiarlos,
# C-01..C-08 ya estaban probados contra ellos) más uno nuevo, exclusivo de
# las tres pruebas de DN-01.
REMITENTE = "zz-corredor-fase1@example.com"
REMITENTE_NO_AUTORIZADO = "zz-corredor-no-autorizado@example.com"
REMITENTE_DN01 = "zz-corredor-set-c-dn01-cliente@example.com"

# Buzón real de Graph (`local/pp_secrets.env`, `PP_MAIL_BUZON_1`) que usan c05()/c06() para mandar
# el ÚNICO correo real de este set, hacia `simulador.BUZON_INGESTA` (el buzón real de producción,
# `mppp@55xljh.onmicrosoft.com`). Distinto del 3 que ya usan `set_v.py`/`set_m.py` (no colisiona
# nunca — el semáforo impide que dos arneses corran a la vez — pero así queda más fácil de rastrear
# en Enviados si alguien tiene que auditar qué mandó este set puntual).
BUZON_REMITENTE_N = 1

CUMPLIDA, NOCUMPLIDA, OMITIDA = 159460001, 159460002, 159460003
ING, NOREC, DESC, RECH, ENPROC, PROC, CERR, NOESNUEVO, VENC = (159460001 + i for i in range(9))

RESULTADO_TXT = {CUMPLIDA: "Cumplida", NOCUMPLIDA: "No cumplida", OMITIDA: "Omitida", None: "(no evaluada)"}
ESTADO_SOL_TXT = {ING: "Ingresada", NOREC: "No reconocida", DESC: "Descartada", RECH: "Rechazada",
                  ENPROC: "En proceso", PROC: "Procesada", CERR: "Cerrada",
                  NOESNUEVO: "No es correo nuevo", VENC: "Vencida"}


def _excel(nombre):
    return pc.excel(nombre)


# ===========================================================================
# preparar / cerrar
# ===========================================================================
def preparar(ctx):
    """Deja listo TODO lo que el set C necesita, y nada más (CONVENCIONES §8):

    - **Datos de negocio** (fixture compartido `PR*`, create-if-missing) y el
      `REMITENTE` de este set autorizado sobre `PR11` — antiguo P-03.
      `REMITENTE_NO_AUTORIZADO` **a propósito NO se registra** (C-04): no
      tiene ninguna preparación que hacerle, es la ausencia de una fila en
      Autorizados lo que el caso necesita, y esa ausencia es el estado
      natural de un correo que nadie sembró nunca.
    - **Los 15 Excel + el adjunto que no es Excel** en `pruebas/adjuntos/` —
      antiguo P-04 (este archivo solo usa `E01-feliz-formato11.xlsx`, pero
      `verificar_excels()` es todo o nada: generar uno solo no es una opción
      de `generar_excel.py`).
    - **`autorizacion.modo` activo** (DN-01): SOLO se verifica que exista una
      versión activa — ya está sembrado en Dev desde el 2026-09-26
      (`playbooks/datos/parametros.md`, confirmado hoy contra el entorno:
      versión 1, valor `plan`) — no se siembra acá porque no hace falta; si
      algún día faltara, este chequeo lo dice con un motivo claro en vez de
      dejar que C-09/C-10/C-11 fallen más adelante con un KeyError críptico.
    - **`correo.prefijos.reenvio`, CONTENIDO** (no solo que exista): lista
      JSON de prefijos con la que `ClasificarCorreo.cs` decide si un correo
      es un reenvío — verifica, NOMBRADO (CONVENCIONES §7), que la lista no
      esté vacía y traiga `FW:`, el prefijo del que depende **C-03**
      (`ES_CORREO_NUEVO` = Cumplida "el prefijo del asunto manda"). Recupera
      la cobertura que perdió el antiguo P-06 al disolver el set P (decisión
      del aprobador 2026-09-28: la verificación viaja con el set que la
      necesita).
    - **`servicio.cuenta`, CONTENIDO** (no solo que exista): el UPN con el
      que la lista blanca de escrituras (`ListaBlancaStep`,
      `StepsDePlataforma.EsCodigoDeServidor`) reconoce a los cinco flujos
      como "código de servidor" — verifica que tenga forma de UPN/correo, no
      solo que la fila esté sembrada. Ningún caso de ESTE archivo ejercita
      ese camino directamente (lo toca el guardado real de Fila/Solicitud, no
      la clasificación del correo); se verifica acá porque no tiene un set
      dedicado propio y, como `retencion.dias.*` en el set V, este es el
      lugar existente más cercano — recupera la cobertura del antiguo P-06.
    - **El Autorizado y la `AutorizacionCliente` de DN-01** (`REMITENTE_DN01`,
      create-if-missing): un correo con **una única vía de autorización**
      —`AutorizacionCliente` hacia el Cliente compartido `PR - Pruebas MPPP
      SA` (el mismo dueño de `PR11`), **cero** `AutorizacionPlan`— para poder
      probar, con el MISMO correo, que el modo (no el remitente) es lo que
      decide si `REMITENTE_RECONOCIDO` lo reconoce (C-09/C-10/C-11). No es
      parte del fixture compartido de `preparar_datos.py` (que solo crea
      `AutorizacionPlan`): es privado de este set, así que este set lo
      borra en `cerrar()` — no aplica `marca_agua` porque el nombre de
      `AutorizacionCliente` es CALCULADO por un step (queda `"<correo> →
      PR - Pruebas MPPP SA"`, nunca lo que se mande al crear: ver
      `pruebas/10-autorizacion-cliente.md` U-07), así que no hay un prefijo
      que buscarle después — se borra por el GUID que devuelve el propio
      `POST` de este mismo proceso, la misma garantía que ya usa
      `Contexto.limpiar_solicitudes()`.

    **Lo que NO prepara, y por qué (para que no se lea como un olvido):**

    - **Roles de usuarios de prueba** ni **sesiones de navegador**
      (CONVENCIONES §6). Nivel Correo se prueba entero contra
      `sanic_mppp_capi_clasificarcorreo` vía Web API (`simulador.py`), como
      el propio service principal — ningún caso de este archivo abre la app.
    - **Los cinco flujos activos** (antiguo P-05). Ningún caso de este set
      dispara `MPPP-REC`/`MPPP-ING` de verdad (ver "Automatizable" de C-05 y
      C-06, y el docstring de `simulador.py`) — salvo esa excepción explícita,
      que se deja N-A a propósito (ver más abajo), nada de este archivo
      necesita los flujos prendidos.
    - **El canon de configuración global** (§8.2), incluido `autorizacion.modo`.
      Lo aplica `correr.py` antes y después de CADA set (no cada set por
      separado): así es el mismo valor para todos y ningún set puede correr
      fuera de sincronía con otro. C-09/C-10/C-11 SÍ cambian `autorizacion.modo`
      como parte de la prueba (§8 lo permite: "dentro del mismo set, un caso
      puede cambiar... configuración"), pero ninguno lo restaura: el motor
      lo lleva de vuelta a `plan` al cerrar el set, pase lo que pase.
    """
    pc.verificar_datos_negocio(ctx.dv, REMITENTE)
    pc.verificar_excels()
    pc.verificar_parametro_activo(ctx.dv, "autorizacion.modo")
    pc.verificar_prefijos_reenvio(ctx.dv)
    pc.verificar_servicio_cuenta(ctx.dv)
    ida, idac = _preparar_autorizacion_por_cliente(ctx.dv)
    ctx.extra["dn01_ids"] = (ida, idac)


def cerrar(ctx):
    """Borra, por GUID (nunca por nombre), las Solicitudes que este set creó,
    y el Autorizado/AutorizacionCliente privados de DN-01 que armó
    `preparar()` — la AutorizacionCliente primero (referencia al Autorizado
    por lookup), el Autorizado después. No toca el fixture compartido `PR*`
    (eso no se borra nunca desde un set: ver `preparar_datos.py`, sección
    "POR QUÉ NO --limpiar"), ni `autorizacion.modo` (lo lleva al canon
    `correr.py`, no este `cerrar()`: ver `preparar()` más arriba).

    También limpia el ÚNICO correo real que c05()/c06() pudieron haber mandado al buzón de
    ingesta de producción (`_limpiar_correo_real()`, por asunto único, sea cual sea la carpeta en
    la que haya quedado) — corre SIEMPRE que c05() llegó a mandarlo, se haya procesado bien, mal,
    o ni siquiera haya llegado a moverse: `ctx.extra["c05c06"]["asunto"]` se guarda ANTES de mandar
    el correo, justamente para que esta limpieza no dependa de que el resto haya salido bien."""
    errores = ctx.limpiar_solicitudes()
    ida, idac = ctx.extra.get("dn01_ids", (None, None))
    if idac:
        est, r, _ = ctx.dv.call("DELETE", f"sanic_mppp_tbl_autorizacionclientes({idac})")
        if est not in (200, 204):
            errores.append(f"no se pudo borrar la AutorizacionCliente {idac} de DN-01 (HTTP {est}): {r}")
    if ida:
        est, r, _ = ctx.dv.call("DELETE", f"sanic_mppp_tbl_autorizados({ida})")
        if est not in (200, 204):
            errores.append(f"no se pudo borrar el Autorizado {ida} de DN-01 (HTTP {est}): {r}")

    datos_c05c06 = ctx.extra.get("c05c06")
    if datos_c05c06 and datos_c05c06.get("asunto"):
        try:
            g = Graph()
            errores.extend(_limpiar_correo_real(g, datos_c05c06["asunto"]))
        except Exception as e:
            errores.append("no se pudo limpiar el correo real de C-05/C-06 en el buzón de ingesta "
                            f"(quedó, revisar A MANO): {type(e).__name__}: {e}")
    return errores


# ===========================================================================
# Helpers de C-05/C-06 — correo real contra el buzón de producción, por Graph.
#
# `graph_api.Graph.call()` NO url-encodea `ruta` (a diferencia de
# `dataverse_api.Dataverse.call()`, que sí lo hace con un `safe=` explícito) — un `$filter` con
# espacios ("subject eq 'x'") revienta con `InvalidURL: URL can't contain control characters`.
# Confirmado hoy contra el entorno real antes de escribir nada más. `_q()` acá abajo aplica el
# MISMO `safe=` que ya usa `dataverse_api.py`, sin tocar `graph_api.py` (no es de este set).
# ===========================================================================
def _q(ruta):
    return urllib.parse.quote(ruta, safe="/?&=$(),'@._-:")


def _folders_buzon_ingesta(g):
    """IDs de Graph de las carpetas `Inbox` y `Procesados` del buzón REAL de ingesta — una sola
    consulta, para no listar las carpetas dos veces por caso."""
    est, r = g.call("GET", _q(f"users/{sim.BUZON_INGESTA}/mailFolders?$top=50&$select=id,displayName"))
    if est != 200:
        raise RuntimeError(f"no se pudieron listar las carpetas del buzón real de ingesta (HTTP {est}): {r}")
    carpetas = {f["displayName"]: f["id"] for f in r.get("value", [])}
    faltan = {"Inbox", "Procesados"} - carpetas.keys()
    if faltan:
        raise RuntimeError(f"faltan carpetas esperadas en el buzón real de ingesta: {sorted(faltan)} "
                            f"(tiene: {sorted(carpetas)})")
    return carpetas["Inbox"], carpetas["Procesados"]


def _mensaje_por_asunto_en_carpeta(g, folder_id, asunto):
    """El mensaje (si existe) con este asunto EXACTO en una carpeta puntual del buzón real de
    ingesta. `id` es el identificador de Graph DENTRO DE ESA carpeta — es justo lo que `MoveV2`
    cambia (D-66) si el tenant no tiene "Immutable Id" habilitado."""
    est, r = g.call(
        "GET", _q(f"users/{sim.BUZON_INGESTA}/mailFolders/{folder_id}/messages"
                  f"?$filter=subject eq '{asunto}'&$select=id,internetMessageId&$top=1"))
    if est == 200 and r.get("value"):
        return r["value"][0]
    return None


def _esperar_mensaje_en_carpeta(g, folder_id, asunto, timeout, intervalo=8):
    """Poll hasta que el mensaje aparezca en esa carpeta puntual, o se acabe `timeout`. `None` si
    no aparece — lo decide el caso que llama, igual que `set_v.py::_esperar_solicitud_por_asunto`."""
    t0 = time.monotonic()
    while True:
        m = _mensaje_por_asunto_en_carpeta(g, folder_id, asunto)
        if m:
            return m
        if time.monotonic() - t0 >= timeout:
            return None
        time.sleep(intervalo)


def _esperar_solicitud_por_messageid(dv, mid, timeout, intervalo=8):
    """Poll sobre Dataverse hasta que exista una Solicitud con este `sanic_messageid` EXACTO (el
    Message-ID RFC822, estable entre carpetas — nunca el `id` de Graph, que MoveV2 puede cambiar),
    o se acabe `timeout`."""
    t0 = time.monotonic()
    while True:
        est, r, _ = dv.call(
            "GET", f"{sim.SOLICITUDES}?$select=sanic_mppp_tbl_solicitudid,sanic_nombre"
                   f"&$filter=sanic_messageid eq '{mid}'&$top=1")
        if est == 200 and r.get("value"):
            return r["value"][0]
        if time.monotonic() - t0 >= timeout:
            return None
        time.sleep(intervalo)


def _limpiar_correo_real(g, asunto):
    """Borra, por asunto único, el ÚNICO correo real que este set manda — sea cual sea la carpeta
    en la que haya quedado (`Inbox` si `MPPP-REC` nunca lo levantó, `Procesados` si sí). Busca en
    TODO el buzón, no en una carpeta fija, justamente para cubrir los dos casos. Nunca lanza: junta
    el motivo y sigue, igual que `Contexto.limpiar_solicitudes()` — un residuo acá es grave (buzón
    de PRODUCCIÓN) pero no puede tumbar el resto de la limpieza del set."""
    try:
        est, r = g.call("GET", _q(f"users/{sim.BUZON_INGESTA}/messages?$filter=subject eq '{asunto}'"
                                   "&$select=id&$top=10"))
    except Exception as e:
        return [f"no se pudo buscar el correo real de C-05/C-06 para borrarlo del buzón de ingesta "
                f"(asunto {asunto!r}, REVISAR A MANO): {type(e).__name__}: {e}"]
    if est != 200:
        return [f"no se pudo buscar el correo real de C-05/C-06 para borrarlo (HTTP {est}, "
                f"asunto {asunto!r}, REVISAR A MANO): {r}"]
    errores = []
    for m in r.get("value", []):
        est_del, r_del = g.call("DELETE", _q(f"users/{sim.BUZON_INGESTA}/messages/{m['id']}"))
        if est_del not in (200, 204):
            errores.append(f"no se pudo borrar el correo real de prueba {m['id']} del buzón de "
                            f"ingesta (HTTP {est_del}, REVISAR A MANO): {r_del}")
    return errores


def _preparar_autorizacion_por_cliente(dv):
    """Create-if-missing: el Autorizado `REMITENTE_DN01` con una ÚNICA vía de
    autorización — `AutorizacionCliente` hacia el Cliente compartido
    (dueño de `PR11`, `PR06`, `PR10`, `PRIN`, `PRNA`; verificado hoy contra
    Dev) — y CERO `AutorizacionPlan` (nunca se le crea una: es un correo
    inventado exclusivamente para esto, no hay otro proceso que pueda
    tocarlo). Devuelve `(id_autorizado, id_autorizacioncliente)` para que
    `cerrar()` los borre por GUID.

    El Cliente se busca por `pc.cliente_compartido()` (CIF BAC, no nombre):
    en Dev puede llevar la marca vieja `"PR - Pruebas MPPP SA"` en vez de la
    nueva `ZZP-...` (ver el docstring de `preparar_datos.py`). Buscarlo acá
    por `sanic_nombre` fue justamente el defecto del 2026-09-28: esta
    búsqueda estaba duplicada en cinco lugares y la migración de marca solo
    se corrigió en uno."""
    idc, _nombre_cliente = pc.cliente_compartido(dv)

    aut = prep.uno(dv, "sanic_mppp_tbl_autorizados", f"sanic_nombre eq '{REMITENTE_DN01}'",
                    "sanic_mppp_tbl_autorizadoid")
    ida = aut["sanic_mppp_tbl_autorizadoid"] if aut else prep.crear(
        dv, "sanic_mppp_tbl_autorizados", {"sanic_nombre": REMITENTE_DN01})

    ac = prep.uno(
        dv, "sanic_mppp_tbl_autorizacionclientes",
        f"_sanic_autorizadoid_value eq {ida} and _sanic_clienteid_value eq {idc}",
        "sanic_mppp_tbl_autorizacionclienteid")
    idac = ac["sanic_mppp_tbl_autorizacionclienteid"] if ac else prep.crear(
        dv, "sanic_mppp_tbl_autorizacionclientes", {
            "sanic_autorizadoid@odata.bind": f"/sanic_mppp_tbl_autorizados({ida})",
            "sanic_clienteid@odata.bind": f"/sanic_mppp_tbl_clientes({idc})",
        })

    return ida, idac


# ===========================================================================
# Casos C-01 .. C-11
# ===========================================================================
def c01(ctx):
    c = pc.Caso("C-01", "Camino feliz: correo nuevo, remitente autorizado, Excel válido")
    sid, mid, rc, rv = ctx.procesar(
        remitente=REMITENTE, asunto="Prueba C-01 inclusiones",
        cantidad_adjuntos=1, cantidad_excel=1, excel_a_subir=_excel("E01-feliz-formato11.xlsx"))
    c.check("clasificación = nuevo, procesar = true", rc.get("clasificacion") == "nuevo" and rc.get("procesar") is True, str(rc))
    c.check("validación llamada (procesar=true)", rv is not None)
    if rv is None:
        return c
    s = sim.leer_solicitud(ctx.dv, sid)
    c.check("Estado = En proceso", s.get("sanic_estadoprocesamiento") == ENPROC, ESTADO_SOL_TXT.get(s.get("sanic_estadoprocesamiento")))
    c.check("filas t/v/r = 3/3/0", (s.get("sanic_filastotales"), s.get("sanic_filasvalidas"), s.get("sanic_filasrechazadas")) == (3, 3, 0))
    reglas = sim.leer_reglas(ctx.dv, sid)
    for cod in ("ES_CORREO_NUEVO", "REMITENTE_RECONOCIDO", "TRAE_ADJUNTO", "ADJUNTO_ES_EXCEL",
                "UN_SOLO_EXCEL", "ESTRUCTURA_PLANTILLA", "TIENE_FILAS"):
        r = reglas.get(cod)
        c.check(f"regla {cod} = Cumplida", r is not None and r["sanic_resultado"] == CUMPLIDA,
                RESULTADO_TXT.get(r["sanic_resultado"]) if r else "no aparece")
    filas = sim.leer_filas(ctx.dv, sid)
    c.check("3 filas Validadas sin mensaje",
            len(filas) == 3 and all(f["sanic_estado"] == 159460003 and not f.get("sanic_mensaje") for f in filas),
            str([(f["sanic_estado"], f.get("sanic_mensaje")) for f in filas]))
    esperadas = {1: "00700010203040506070", 2: "00700011223344556677", 3: "00700099887766554433"}
    for f in filas:
        n = f["sanic_numerofila"]
        ref = f.get("sanic_referencia")
        c.check(f"fila {n}: referencia de 20 caracteres, empieza en 007", ref == esperadas.get(n), f"obtenida {ref!r}")
    c.check("acuse presente", bool(s.get("sanic_acusecontenido")))
    return c


def c02(ctx):
    c = pc.Caso("C-02", "Una respuesta NO se procesa (ES_CORREO_NUEVO)")
    sid, mid = ctx.solicitud(
        remitente=REMITENTE, asunto="RE: algo anterior",
        cantidad_adjuntos=1, cantidad_excel=1, excel_a_subir=_excel("E01-feliz-formato11.xlsx"),
        in_reply_to="original-simulado@herramientas.local")
    rc = sim.clasificar(ctx.dv, sid)
    c.check("clasificación = respuesta, procesar = false", rc.get("clasificacion") == "respuesta" and rc.get("procesar") is False, str(rc))
    s = sim.leer_solicitud(ctx.dv, sid)
    c.check("Estado = No es correo nuevo", s.get("sanic_estadoprocesamiento") == NOESNUEVO, ESTADO_SOL_TXT.get(s.get("sanic_estadoprocesamiento")))
    filas = sim.leer_filas(ctx.dv, sid)
    c.check("0 filas (no se abrió el Excel)", len(filas) == 0, f"hay {len(filas)}")
    reglas = sim.leer_reglas(ctx.dv, sid)
    r_nuevo = reglas.get("ES_CORREO_NUEVO")
    c.check("ES_CORREO_NUEVO = No cumplida", r_nuevo is not None and r_nuevo["sanic_resultado"] == NOCUMPLIDA)
    r_rec = reglas.get("REMITENTE_RECONOCIDO")
    c.check("REMITENTE_RECONOCIDO = Omitida", r_rec is not None and r_rec["sanic_resultado"] == OMITIDA,
            RESULTADO_TXT.get(r_rec["sanic_resultado"]) if r_rec else "no aparece")
    c.check("ninguna comunicación al cliente (sin acuse)", not s.get("sanic_acusecontenido"))
    return c


def c03(ctx):
    c = pc.Caso("C-03", "Un reenvío SÍ se procesa (prefijo FW:)")
    sid, mid, rc, rv = ctx.procesar(
        remitente=REMITENTE, asunto="FW: Prueba C-03 reenvio",
        cantidad_adjuntos=1, cantidad_excel=1, excel_a_subir=_excel("E01-feliz-formato11.xlsx"),
        in_reply_to="original-simulado-c03@herramientas.local")
    c.check("clasificación = reenvio, procesar = true", rc.get("clasificacion") == "reenvio" and rc.get("procesar") is True, str(rc))
    c.check("validación llamada", rv is not None)
    if rv is None:
        return c
    s = sim.leer_solicitud(ctx.dv, sid)
    c.check("Estado = En proceso", s.get("sanic_estadoprocesamiento") == ENPROC)
    c.check("filas t/v/r = 3/3/0", (s.get("sanic_filastotales"), s.get("sanic_filasvalidas"), s.get("sanic_filasrechazadas")) == (3, 3, 0))
    reglas = sim.leer_reglas(ctx.dv, sid)
    r = reglas.get("ES_CORREO_NUEVO")
    c.check("ES_CORREO_NUEVO = Cumplida (el prefijo del asunto manda, no la ausencia de referencias)",
            r is not None and r["sanic_resultado"] == CUMPLIDA)
    return c


def c04(ctx):
    c = pc.Caso("C-04", "Remitente no reconocido")
    sid, mid = ctx.solicitud(
        remitente=REMITENTE_NO_AUTORIZADO, asunto="Prueba C-04 remitente no reconocido",
        cantidad_adjuntos=1, cantidad_excel=1, excel_a_subir=_excel("E01-feliz-formato11.xlsx"))
    rc = sim.clasificar(ctx.dv, sid)
    c.check("clasificación = remitente_no_reconocido, procesar = false",
            rc.get("clasificacion") == "remitente_no_reconocido" and rc.get("procesar") is False, str(rc))
    s = sim.leer_solicitud(ctx.dv, sid)
    c.check("Estado = No reconocida", s.get("sanic_estadoprocesamiento") == NOREC, ESTADO_SOL_TXT.get(s.get("sanic_estadoprocesamiento")))
    filas = sim.leer_filas(ctx.dv, sid)
    c.check("0 filas", len(filas) == 0)
    reglas = sim.leer_reglas(ctx.dv, sid)
    c.check("ES_CORREO_NUEVO = Cumplida", reglas.get("ES_CORREO_NUEVO", {}).get("sanic_resultado") == CUMPLIDA)
    c.check("REMITENTE_RECONOCIDO = No cumplida", reglas.get("REMITENTE_RECONOCIDO", {}).get("sanic_resultado") == NOCUMPLIDA)
    c.check("ninguna comunicación al cliente", not s.get("sanic_acusecontenido"))
    return c


def c05(ctx):
    c = pc.Caso("C-05", "El correo se mueve a Procesados y cambia de identificador")

    try:
        g = Graph()
    except Exception as e:
        c.check("se pudo autenticar contra Graph (precondición)", False, f"{type(e).__name__}: {e}")
        return c

    try:
        remitente = g.buzon(BUZON_REMITENTE_N)
    except Exception as e:
        c.check(f"se pudo obtener el buzón de prueba real PP_MAIL_BUZON_{BUZON_REMITENTE_N} "
                "(precondición)", False, f"{type(e).__name__}: {e}")
        return c

    try:
        id_inbox, id_procesados = _folders_buzon_ingesta(g)
    except Exception as e:
        c.check("se pudieron listar las carpetas del buzón real de ingesta, incluida 'Procesados' "
                "(precondición)", False, f"{type(e).__name__}: {e}")
        return c

    # Asunto único: ÚNICA correlación posible con el correo real (igual criterio que
    # `set_v.py::v02()`). El nombre del Excel también es propio de ESTE correo — a propósito
    # DISTINTO del nombre del archivo fuente en `pruebas/adjuntos/` (C-06 lo necesita así: si
    # coincidieran, no se podría distinguir "el sistema leyó el nombre real del adjunto de Outlook"
    # de "por casualidad es el mismo nombre que ya tenía el archivo en el fixture").
    asunto = f"ZZP-C05C06-{uuid.uuid4().hex[:8]}"
    nombre_excel_real = f"ZZP-C06-adjunto-{uuid.uuid4().hex[:6]}.xlsx"
    # Se guarda ANTES de mandar el correo: así `cerrar()` intenta limpiar el buzón real pase lo que
    # pase de acá para abajo (envío que no llegó a mandarse aparte, la búsqueda no encuentra nada y
    # no hace daño).
    ctx.extra["c05c06"] = {"asunto": asunto}

    with open(_excel("E01-feliz-formato11.xlsx"), "rb") as f:
        contenido_excel = f.read()

    est_envio, r_envio = g.mandar_correo(
        remitente, sim.BUZON_INGESTA, asunto,
        "Correo de prueba automatizada de C-05/C-06 (herramientas/pruebas/sets/set_c.py). Ignorar "
        "este mensaje: el propio caso lo borra al cerrar el set.",
        adjunto=(nombre_excel_real, contenido_excel))
    c.check("Graph aceptó el envío real del correo hacia el buzón de ingesta (HTTP 202)",
            est_envio == 202, f"HTTP {est_envio}: {r_envio}")
    if est_envio != 202:
        return c

    # Capturado ANTES de MoveV2, mientras el correo todavía está en Inbox — es el ÚNICO momento en
    # que se puede leer el identificador "de antes". `MPPP-REC` sondea cada 1 minuto (verificado
    # hoy contra el `recurrence` de `mppp-rec.json`), así que hay una ventana real de hasta 60s.
    antes = _esperar_mensaje_en_carpeta(g, id_inbox, asunto, timeout=90)
    c.check("el correo real llegó a la bandeja de entrada del buzón de ingesta (identificador "
            "capturado ANTES de MoveV2)", antes is not None,
            "no apareció en Inbox en 90s buscando el asunto único — Graph pudo no haber entregado "
            "el correo, o MPPP-REC lo levantó y movió más rápido de lo que se pudo capturar")

    # Margen: recurrence de MPPP-REC (1 min) + tiempo de ejecución de MPPP-ING (varias llamadas con
    # reintento) + colchón — bastante menos que el margen de 10+ minutos que usa V-02 para MPPP-VIG
    # (esa recurre cada 10 min; esta, cada 1).
    despues = _esperar_mensaje_en_carpeta(g, id_procesados, asunto, timeout=280)
    c.check("el correo terminó en la carpeta 'Procesados' del buzón real (MPPP-REC + MPPP-ING lo "
            "procesaron de punta a punta; margen ~4-5 min)", despues is not None,
            "no apareció en Procesados dentro del margen — si MPPP-REC está desactivado (statecode "
            "0), este es exactamente el síntoma: revisar `workflows` antes de reintentar")
    if despues is None:
        return c

    id_antes = antes["id"] if antes else None
    id_despues = despues["id"]
    if id_antes is not None:
        c.check(
            "el identificador de Graph del mensaje CAMBIÓ al moverlo a Procesados — esto es LO "
            "ESPERADO, no un defecto: MoveV2 cambia el ID salvo que el tenant tenga 'Immutable Id' "
            "habilitado (D-66, diseno/PENDIENTES.md). Lo que el aprobador corrigió hoy en el flujo "
            "no es que el ID deje de cambiar, sino que MPPP-ING deje de guardarse el ID VIEJO "
            "después de moverlo — eso es lo que rompía la re-ingesta de MPPP-VIG",
            id_antes != id_despues, f"antes={id_antes!r} después={id_despues!r}")
    else:
        c.check("no se pudo comparar el identificador antes/después (no se llegó a capturar el de "
                "antes de moverlo)", False)

    mid_crudo = despues.get("internetMessageId") or ""
    mid = mid_crudo.replace("<", "").replace(">", "")[:450]
    fila = _esperar_solicitud_por_messageid(ctx.dv, mid, timeout=60) if mid else None
    c.check("la Solicitud real quedó creada en Dataverse, correlacionada por sanic_messageid "
            "(el Message-ID RFC822, estable entre carpetas — nunca el id de Graph, que sí cambia)",
            fila is not None, f"sanic_messageid buscado: {mid!r}")
    if fila is None:
        return c

    sid = fila["sanic_mppp_tbl_solicitudid"]
    ctx.creadas.append(sid)  # para que cerrar() la borre por GUID, igual que cualquier otra

    s = sim.leer_solicitud(ctx.dv, sid)
    c.check(
        "sanic_outlookmessageid guardado = el identificador de Graph DESPUÉS de moverlo — el "
        "arreglo de hoy: `Guardar_el_identificador_nuevo` en mppp-ing.json usa "
        "`coalesce(outputs('Mover_el_correo')?['body/id'], triggerBody()['text'])`, o sea el ID "
        "nuevo que devuelve el propio MoveV2, con el viejo como respaldo solo si el move falló",
        s.get("sanic_outlookmessageid") == id_despues,
        f"guardado={s.get('sanic_outlookmessageid')!r} esperado(después)={id_despues!r}")
    if id_antes is not None:
        c.check(
            "sanic_outlookmessageid NO quedó con el identificador VIEJO (de antes de moverlo) — si "
            "quedara, una re-ingesta de MPPP-VIG sobre esta misma Solicitud fallaría con 404 al "
            "intentar leerlo: exactamente el bug real de D-66, ya corregido hoy por el aprobador",
            s.get("sanic_outlookmessageid") != id_antes,
            f"guardado={s.get('sanic_outlookmessageid')!r} viejo(antes)={id_antes!r}")

    ctx.extra["c05c06"].update({"ok": True, "sid": sid, "nombre_excel_real": nombre_excel_real})
    return c


def c06(ctx):
    c = pc.Caso("C-06", "El .eml y el Excel quedan guardados con su nombre real")
    datos = ctx.extra.get("c05c06") or {}
    if not datos.get("ok"):
        c.na(
            "Depende de la MISMA Solicitud real que arma C-05 (mismo correo real, mismo adjunto): "
            "C-05 no llegó a tener una Solicitud real procesada en esta corrida (ver el detalle de "
            "C-05, arriba, para la causa puntual) — este archivo NO manda un segundo correo real "
            "solo para C-06: el buzón de ingesta es de producción, cada correo real tiene que ser "
            "quirúrgico (una sola vez por corrida, ver `c05()`), nunca duplicado 'por las dudas'.")
        return c

    sid = datos["sid"]
    nombre_excel_real = datos["nombre_excel_real"]
    s = sim.leer_solicitud(ctx.dv, sid)

    c.check("sanic_correocrudo_name presente", bool(s.get("sanic_correocrudo_name")),
            s.get("sanic_correocrudo_name"))
    c.check(
        "sanic_correocrudo_name = 'correo.eml' — CORRECCIÓN sobre lo que este caso asumía antes: "
        "el .eml NUNCA lleva el asunto/nombre real del mensaje, NI CON CORREO REAL. Verificado hoy "
        "contra el `clientdata` DESPLEGADO de MPPP-ING (`workflows`, no el repo): "
        "`Guardar_el_correo_crudo` fija `\"x-ms-file-name\": \"correo.eml\"` a mano — exactamente "
        "lo mismo que ya sube `simulador.py`. La parte 'nombre real' de este caso es EXCLUSIVA del "
        "Excel; para el .eml lo único que un correo real prueba de más es que `ExportEmail_V2` "
        "funciona de punta a punta contra un mensaje genuino, no que el nombre sea distinto",
        s.get("sanic_correocrudo_name") == "correo.eml", s.get("sanic_correocrudo_name"))
    c.check(
        f"sanic_exceloriginal_name = el nombre REAL del adjunto tal como lo leyó Outlook/Graph del "
        f"correo real ({nombre_excel_real!r}) — a propósito DISTINTO del nombre del archivo fuente "
        "en pruebas/adjuntos/ (E01-feliz-formato11.xlsx), para que este check solo pueda pasar si "
        "`@first(variables('excels'))?['name']` extrajo el nombre del adjunto REAL del correo, no "
        "de una convención local ni de una coincidencia con el fixture",
        s.get("sanic_exceloriginal_name") == nombre_excel_real, s.get("sanic_exceloriginal_name"))
    return c


def c07(ctx):
    c = pc.Caso("C-07", "El mismo correo dos veces no duplica (alcance reducido, ver Notas)")
    mid = sim.nuevo_message_id()
    sid1, _ = ctx.solicitud(remitente=REMITENTE, asunto="Prueba C-07 no duplica", message_id=mid,
                             cantidad_adjuntos=1, cantidad_excel=1, excel_a_subir=_excel("E01-feliz-formato11.xlsx"))
    # El segundo intento reproduce EXACTAMENTE lo que hace `Crear_la_solicitud` de
    # mppp-ing.json cuando MPPP-VIG reingiere el mismo correo: un CreateRecord con el
    # mismo sanic_messageid. La clave alternativa `sanic_mppp_key_solicitud_messageid`
    # (DD-03) tiene que rechazarlo con un error de duplicado.
    try:
        sid2, _ = sim.crear_solicitud(
            ctx.dv, remitente=REMITENTE, asunto="Prueba C-07 no duplica (reingesta simulada)",
            message_id=mid, cantidad_adjuntos=1, cantidad_excel=1, excel_a_subir=_excel("E01-feliz-formato11.xlsx"))
        ctx.creadas.append(sid2)
        c.check("un segundo Create con el mismo sanic_messageid es rechazado (clave alternativa DD-03)",
                False, "se creó una SEGUNDA Solicitud con el mismo sanic_messageid: la clave alternativa no está frenando el duplicado")
    except RuntimeError as e:
        c.check("un segundo Create con el mismo sanic_messageid es rechazado (clave alternativa DD-03)", True, str(e)[:200])
    est, r, _ = ctx.dv.call("GET", f"{sim.SOLICITUDES}?$select=sanic_mppp_tbl_solicitudid&$filter=sanic_messageid eq '{mid}'")
    total = len(r.get("value", [])) if est == 200 else -1
    c.check("solo existe UNA Solicitud con ese sanic_messageid", total == 1, f"hay {total}")
    return c


def c08(ctx):
    c = pc.Caso("C-08", "Sin adjunto")
    sid, mid, rc, rv = ctx.procesar(
        remitente=REMITENTE, asunto="Prueba C-08 sin adjunto", cantidad_adjuntos=0, cantidad_excel=0)
    c.check("clasificación = nuevo, procesar = true (el remitente sí está reconocido)",
            rc.get("clasificacion") == "nuevo" and rc.get("procesar") is True, str(rc))
    c.check("validación llamada", rv is not None)
    if rv is None:
        return c
    s = sim.leer_solicitud(ctx.dv, sid)
    c.check("Estado = Rechazada", s.get("sanic_estadoprocesamiento") == RECH, ESTADO_SOL_TXT.get(s.get("sanic_estadoprocesamiento")))
    c.check("0 filas", s.get("sanic_filastotales") in (0, None))
    reglas = sim.leer_reglas(ctx.dv, sid)
    r = reglas.get("TRAE_ADJUNTO")
    c.check("TRAE_ADJUNTO = No cumplida", r is not None and r["sanic_resultado"] == NOCUMPLIDA)
    for cod in ("ADJUNTO_ES_EXCEL", "UN_SOLO_EXCEL", "ESTRUCTURA_PLANTILLA", "TIENE_FILAS"):
        r = reglas.get(cod)
        c.check(f"{cod} = Omitida (la bloqueó TRAE_ADJUNTO)", r is not None and r["sanic_resultado"] == OMITIDA,
                RESULTADO_TXT.get(r["sanic_resultado"]) if r else "no aparece")
    c.check("hay acuse (dice que no hay nada que procesar)", bool(s.get("sanic_acusecontenido")))
    if s.get("sanic_acusecontenido"):
        texto = s["sanic_acusecontenido"]
        c.check("el acuse NO nombra la regla técnica 'TRAE_ADJUNTO'", "TRAE_ADJUNTO" not in texto)
    return c


def c09(ctx):
    c = pc.Caso("C-09", "DN-01 modo cliente: autorizado SOLO por AutorizacionCliente SÍ entra")
    pc.fijar_modo_autorizacion(ctx.dv, "cliente")
    sid, mid, rc, rv = ctx.procesar(
        remitente=REMITENTE_DN01, asunto="Prueba C-09 DN-01 modo cliente",
        cantidad_adjuntos=1, cantidad_excel=1, excel_a_subir=_excel("E01-feliz-formato11.xlsx"))
    c.check("clasificación = nuevo, procesar = true (el portero también mira el modo cliente)",
            rc.get("clasificacion") == "nuevo" and rc.get("procesar") is True, str(rc))
    reglas = sim.leer_reglas(ctx.dv, sid)
    r = reglas.get("REMITENTE_RECONOCIDO")
    c.check("REMITENTE_RECONOCIDO = Cumplida (vía AutorizacionCliente, con CERO AutorizacionPlan)",
            r is not None and r["sanic_resultado"] == CUMPLIDA,
            RESULTADO_TXT.get(r["sanic_resultado"]) if r else "no aparece")
    if rv is not None:
        s = sim.leer_solicitud(ctx.dv, sid)
        c.check("Estado = En proceso (el resto de la cadena no se rompió por el modo nuevo)",
                s.get("sanic_estadoprocesamiento") == ENPROC, ESTADO_SOL_TXT.get(s.get("sanic_estadoprocesamiento")))
    return c


def c10(ctx):
    c = pc.Caso("C-10", "DN-01 modo plan: el MISMO correo (autorizado solo por cliente) YA NO entra")
    pc.fijar_modo_autorizacion(ctx.dv, "plan")
    sid, mid = ctx.solicitud(
        remitente=REMITENTE_DN01, asunto="Prueba C-10 DN-01 modo plan",
        cantidad_adjuntos=1, cantidad_excel=1, excel_a_subir=_excel("E01-feliz-formato11.xlsx"))
    rc = sim.clasificar(ctx.dv, sid)
    c.check("clasificación = remitente_no_reconocido, procesar = false (AutorizacionCliente queda muda en modo plan)",
            rc.get("clasificacion") == "remitente_no_reconocido" and rc.get("procesar") is False, str(rc))
    s = sim.leer_solicitud(ctx.dv, sid)
    c.check("Estado = No reconocida", s.get("sanic_estadoprocesamiento") == NOREC, ESTADO_SOL_TXT.get(s.get("sanic_estadoprocesamiento")))
    reglas = sim.leer_reglas(ctx.dv, sid)
    c.check("REMITENTE_RECONOCIDO = No cumplida", reglas.get("REMITENTE_RECONOCIDO", {}).get("sanic_resultado") == NOCUMPLIDA)
    c.check("ninguna comunicación al cliente", not s.get("sanic_acusecontenido"))
    return c


def c11(ctx):
    c = pc.Caso("C-11", "DN-01 valor basura en autorizacion.modo: degrada a plan, no tumba nada")
    pc.fijar_modo_autorizacion(ctx.dv, "clienteX")

    # (a) el correo autorizado SOLO por cliente: la basura NO se interpreta como 'cliente'.
    sid1, mid1 = ctx.solicitud(
        remitente=REMITENTE_DN01, asunto="Prueba C-11a basura (solo-cliente)",
        cantidad_adjuntos=1, cantidad_excel=1, excel_a_subir=_excel("E01-feliz-formato11.xlsx"))
    rc1 = sim.clasificar(ctx.dv, sid1)
    c.check("(a) autorizado SOLO por cliente: 'clienteX' degrada a plan → remitente_no_reconocido",
            rc1.get("clasificacion") == "remitente_no_reconocido" and rc1.get("procesar") is False, str(rc1))

    # (b) la operación NORMAL (el correo de siempre, autorizado por plan) sigue andando: la basura
    # degrada, no tumba nada — ni una excepción, ni un HTTP roto, ni un falso "no reconocido".
    sid2, mid2, rc2, rv2 = ctx.procesar(
        remitente=REMITENTE, asunto="Prueba C-11b basura (autorizado por plan)",
        cantidad_adjuntos=1, cantidad_excel=1, excel_a_subir=_excel("E01-feliz-formato11.xlsx"))
    c.check("(b) autorizado por AutorizacionPlan de siempre: sigue entrando (la operación no se tumbó)",
            rc2.get("clasificacion") == "nuevo" and rc2.get("procesar") is True, str(rc2))
    if rv2 is not None:
        s2 = sim.leer_solicitud(ctx.dv, sid2)
        c.check("(b) Estado = En proceso (llegó hasta el final sin excepción por el valor basura)",
                s2.get("sanic_estadoprocesamiento") == ENPROC, ESTADO_SOL_TXT.get(s2.get("sanic_estadoprocesamiento")))
    return c


CASOS = [c01, c02, c03, c04, c05, c06, c07, c08, c09, c10, c11]
