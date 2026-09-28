#!/usr/bin/env python3
"""Set M — Comunicaciones al cliente (`pruebas/04-comunicaciones.md`).

`MPPP-COM` (detecta) y `MPPP-ENV` (envía): el acuse y la respuesta final, y la garantía de
"como máximo una vez". El contrato de un módulo de set está en `sets/__init__.py`; la
preparación compartida, en `preparacion_comun.py`.

**Por qué este set ya no es manual.** Hasta el 2026-09-27 los 7 casos de este archivo estaban
`N-A` porque no había manera de mandar un correo real ni de leer lo que salió. Eso cambió:
`herramientas/graph_api.py` tiene `Mail.Send` (probado: HTTP 202 real) y `Mail.Read` (probado:
se leyó Enviados) sobre los buzones de prueba `PP_MAIL_BUZON_1..4`. Este set manda un correo
REAL al buzón de ingesta (`simulador.BUZON_INGESTA`) desde uno de esos buzones, deja que el
sistema REAL lo procese de punta a punta (`MPPP-REC` → `MPPP-ING` → las Custom API →
`MPPP-COM` → `MPPP-ENV`, los cinco activos y confirmados por `preparacion_comun.verificar_flujos_activos()`
el 2026-09-27) y lee la respuesta real que llega a la bandeja — no la simula, no le cree a la
Bitácora por sí sola: la Bitácora y el correo real se contrastan entre sí.

**Cómo dispara el ciclo completo sin usar un navegador.** `MPPP-COM` es un webhook de Dataverse
sobre `Update` de Solicitud con `filteringattributes=sanic_estadoprocesamiento`
(`recursos/flujos/mppp-com.json`): dispara cuando ese atributo viene en el `Target` del update,
sea cual sea el valor nuevo. Digitar y aprobar (M-04) tampoco necesitan navegador: se hacen por
Web API con el encabezado `MSCRMCallerID` suplantando a los usuarios de prueba `User1`
(Ejecutivo) y `User2` (Supervisor) — el mismo mecanismo, ya probado, de
`comportamiento_roles.py` (`prvActOnBehalfOfAnotherUser`, que el service principal tiene). Acá
se reimplementa LOCAL y acotado a las filas de la Solicitud que este set creó — nunca a "la
primera fila en Validada que haya en el entorno" (eso es lo que hace `comportamiento_roles.py`,
y por eso no se lo llama desde acá: tocaría datos de otro set corriendo en paralelo).

**La deuda que este set salda (`pruebas/README.md`).** Las versiones anteriores de M-01, M-02,
M-04 y M-05 pedían "la Solicitud de C-01" o "una Solicitud Rechazada (de S-01, S-05 o C-08)":
tres de otro archivo, una de otro caso de este mismo archivo. `CONVENCIONES.md` §8 lo prohíbe.
Ahora M-01 crea su propia Solicitud con un correo real y M-04/M-05 reutilizan ESA Solicitud
—dentro del mismo set, que es la única reutilización que §8 permite—; M-02, M-03 y M-06 crean
cada una la suya.

**Qué NO se automatiza, y por qué.**

- Nada de este set manda correo a una dirección real de un cliente del banco: el destino
  siempre es `simulador.BUZON_INGESTA` (el buzón de prueba que ya usan los demás sets) y el
  remitente/copia son `PP_MAIL_BUZON_3`/`PP_MAIL_BUZON_4`, buzones de prueba dedicados. Todo
  asunto empieza con `ZZP-`.
- M-06 ya NO espera el recurrence real de `MPPP-VIG` (2026-09-28: ese tramo pasó a V-07,
  `pruebas/07-vigilancia.md` — ver el porqué en `pruebas/04-comunicaciones.md`, sección M-06).
  Este caso arma el escenario "a medias" con correo real y verifica la precondición; es rápido.
- Este set NO usa ninguna sesión de navegador (`local/sesion-*.json`) ni pasa por
  `estado_sesiones.py`: esa guarda es para pruebas que manejan un navegador de verdad: acá no
  hay ninguno.
"""
import base64
import datetime
import os
import re
import sys
import time
import urllib.parse
import uuid

_AQUI = os.path.dirname(os.path.abspath(__file__))
_HERRAMIENTAS_PRUEBAS = os.path.dirname(_AQUI)
_HERRAMIENTAS = os.path.dirname(_HERRAMIENTAS_PRUEBAS)
sys.path.insert(0, _HERRAMIENTAS_PRUEBAS)
sys.path.insert(0, _HERRAMIENTAS)

import preparacion_comun as pc  # noqa: E402
import simulador as sim  # noqa: E402
from graph_api import Graph  # noqa: E402

LETRA = "M"
NOMBRE = "Comunicaciones al cliente"

# Buzones de prueba dedicados a este set (`local/pp_secrets.env`, nunca leído/impreso acá:
# `graph_api.Graph` lo carga solo). 3 = remitente ("el De"); 4 = solo se usa como copia en M-03.
BUZON_REMITENTE_N = 3
BUZON_CC_N = 4

# --- choices (src/Sanic.Mppp.Plugins/Dominio/Choices.g.cs, valores verificados el 2026-09-27) ---
RECH, ENPROC, PROC, CERR = 159460004, 159460005, 159460006, 159460007          # EstadoDeLaSolicitud
VALIDADA, DIGITADA, APROBADA = 159460003, 159460004, 159460005                 # EstadoDeLaFila
EVT_ACUSE_INICIADO, EVT_ACUSE_ENVIADO = 159460003, 159460004                   # EventoDeBitacora
EVT_RESP_INICIADA, EVT_RESP_ENVIADA = 159460010, 159460011

CAMPOS_SOLICITUD = (
    "sanic_estadoprocesamiento,sanic_fechaacuseiniciado,sanic_fechaacuseenviado,"
    "sanic_fecharespuestafinaliniciada,sanic_fecharespuestafinalenviada,sanic_fechacerrada,"
    "sanic_filastotales,sanic_filasvalidas,sanic_filasrechazadas,sanic_requiererevision,sanic_motivorevision"
)

# Términos que NO tienen que aparecer en un correo que lee un cliente del banco (el criterio del
# aprobador, 2026-09-27: "se generaliza lo que revela internals, se deja pasar lo que le dice a
# la persona qué hacer"). Nombres de regla, de flujo, de tabla/columna, de excepción de
# plataforma: todo lo que un cliente no puede accionar y que además le muestra cómo está armado
# el sistema por dentro.
TERMINOS_INTERNOS = [
    "sanic_", "TRAE_ADJUNTO", "ADJUNTO_ES_EXCEL", "UN_SOLO_EXCEL", "ESTRUCTURA_PLANTILLA",
    "TIENE_FILAS", "PLAN_EXISTE", "MONEDA_DEL_PLAN", "LISTAS_VALIDAS", "LARGOS_Y_FORMATO",
    "AUTORIZACION_CORREO_PLAN", "OBLIGATORIEDAD", "MPPP-ING", "MPPP-REC", "MPPP-COM", "MPPP-ENV",
    "MPPP-VIG", "Custom API", "CustomAPI", "webhook", "Dataverse", "Plugin", "Exception",
    "StackTrace", "Microsoft.Crm", "systemuserid", "GUID",
]


# ===========================================================================
# preparar / cerrar
# ===========================================================================
def preparar(ctx):
    """Deja listo TODO lo que el set M necesita (CONVENCIONES §8):

    - **Datos de negocio** (fixture `PR*`, el `REMITENTE` real autorizado sobre `PR11`).
    - **Los Excel** de los casos (solo se usa `E01-feliz-formato11.xlsx`, ya generado por
      `verificar_excels()`).
    - **`plantilla.estructura`** (igual que el set S; las Custom API la consultan).
    - **Que Graph funcione de verdad**: token obtenido, y `Mail.Read` confirmado sobre el
      `REMITENTE` (si el aprobador algún día revoca `Mail.Read`, este set no corre — no finge
      que puede leer lo que no puede leer).
    - **Los dos actores de M-04** (`User1`/Ejecutivo, `User2`/Supervisor): se resuelve su
      `systemuserid` y se verifica su rol de negocio, LOCAL a este módulo (no se llama a
      `comportamiento_roles.py`, que opera sobre "la primera fila que encuentre en el entorno" y
      pisaría datos de otro set corriendo en paralelo — CONVENCIONES §8.1).

    **Lo que NO prepara, y por qué:**

    - **Ninguna sesión de navegador.** Este set no usa GUI: digitar/aprobar se hace por Web API
      suplantando con `MSCRMCallerID` (ver el docstring del módulo).
    - **Los cinco flujos activos.** Se comprobaron a mano el 2026-09-27
      (`preparacion_comun.verificar_flujos_activos`, sin faltantes ni apagados) porque ESTE set
      depende en serio de que estén prendidos — a diferencia de S/C/R, que nunca los tocan. No
      se vuelve a comprobar en cada corrida para no sumar una llamada más al motor común; si
      algún día se apagan, los casos lo van a mostrar solos (la Solicitud nunca aparece, o
      aparece y nunca llega el acuse) con un mensaje que ya distingue esa causa.
    """
    remitente_addr = None
    g = Graph()
    ctx.extra["g"] = g
    remitente_addr = g.buzon(BUZON_REMITENTE_N)
    ctx.extra["remitente"] = remitente_addr
    ctx.extra["cc"] = g.buzon(BUZON_CC_N)

    pc.verificar_datos_negocio(ctx.dv, remitente_addr)
    pc.verificar_excels()
    ctx.extra["estructura"] = pc.verificar_estructura_plantilla(ctx.dv)

    disponible, est, r = g.enviados(remitente_addr)
    if not disponible:
        raise RuntimeError(
            f"el service principal de correo no tiene Mail.Read sobre {remitente_addr} "
            f"(HTTP {est}): {r} — sin esto, este set no puede verificar lo que el cliente "
            "recibe de verdad, y correr los casos sería volver a creerle solo a la Bitácora")

    ctx.extra["ejecutivo"] = _resolver_actor(ctx.dv, "User1", "SR - MPPP - Ejecutivo")
    ctx.extra["supervisor"] = _resolver_actor(ctx.dv, "User2", "SR - MPPP - Supervisor")


def cerrar(ctx):
    """Borra, por GUID, las Solicitudes que este set creó (todas nacieron de un correo real,
    no de `simulador.crear_solicitud`, pero `borrar_solicitud` borra igual por GUID — no le
    importa cómo nació la Solicitud). **No borra los correos reales** que mandó o que recibió:
    Mail.Read no da permiso de borrado, y de todos modos son buzones de prueba dedicados —
    quedan como registro, con asunto `ZZP-`, igual que los Excel `ZZ-*` de otros sets."""
    return ctx.limpiar_solicitudes()


def _resolver_actor(dv, alias, rol_esperado):
    """El `systemuserid` de un usuario de prueba por su alias de dominio, con su rol de negocio
    verificado (nunca supuesto — CONVENCIONES §8, mismo criterio que
    `comportamiento_roles.py.Entorno`, reimplementado acá porque ese módulo resuelve LAS FILAS
    sobre las que actúa mirando "cualquiera que haya en el entorno", y este set necesita actuar
    solo sobre las suyas)."""
    est, r, _ = dv.call(
        "GET", "systemusers?$select=systemuserid,fullname,domainname"
               f"&$filter=startswith(domainname,'{alias}@') and applicationid eq null")
    filas = r.get("value", []) if est == 200 else []
    if len(filas) != 1:
        raise RuntimeError(f"{alias!r} no identifica un único usuario de prueba (HTTP {est}): {filas}")
    uid = filas[0]["systemuserid"]
    est2, r2, _ = dv.call("GET", f"systemusers({uid})/systemuserroles_association?$select=name")
    roles = [x["name"] for x in r2.get("value", [])] if est2 == 200 else []
    if rol_esperado not in roles:
        raise RuntimeError(
            f"{alias!r} tendría que tener el rol {rol_esperado!r} y el entorno le ve {roles} — "
            "sin esto M-04 mediría otra cosa (un 403 que parece un defecto del producto cuando "
            "el problema son los datos de prueba, CONVENCIONES §8)")
    return uid


# ===========================================================================
# Correo real: mandar, leer, esperar
# ===========================================================================
def _bytes_excel(nombre):
    with open(pc.excel(nombre), "rb") as f:
        return nombre, f.read()


def _enviar(ctx, sufijo, *, adjunto=None, cc=None):
    """Manda un correo REAL desde el `REMITENTE` de este set hacia `simulador.BUZON_INGESTA`,
    con un asunto único `ZZP-<sufijo>-<nonce>`. Devuelve el asunto (única forma de correlacionar
    después: no hay `message_id` simulado, es un `Message-ID` real que pone Exchange)."""
    g = ctx.extra["g"]
    asunto = f"ZZP-{sufijo}-{uuid.uuid4().hex[:8]}"
    mensaje = {
        "subject": asunto,
        "body": {"contentType": "Text",
                 "content": "Correo de prueba automatizada del set M "
                            "(herramientas/pruebas/sets/set_m.py). Ignorar este mensaje."},
        "toRecipients": [{"emailAddress": {"address": sim.BUZON_INGESTA}}],
    }
    if cc:
        mensaje["ccRecipients"] = [{"emailAddress": {"address": cc}}]
    if adjunto:
        nombre_archivo, contenido = adjunto
        mensaje["attachments"] = [{
            "@odata.type": "#microsoft.graph.fileAttachment",
            "name": nombre_archivo,
            "contentBytes": base64.b64encode(contenido).decode("ascii"),
        }]
    est, r = g.call("POST", f"users/{ctx.extra['remitente']}/sendMail",
                     {"message": mensaje, "saveToSentItems": True})
    if est != 202:
        raise RuntimeError(f"Graph no aceptó el envío real del correo de prueba (HTTP {est}): {r}")
    return asunto


def _esperar_solicitud(ctx, asunto, timeout=240, intervalo=10):
    """Poll sobre Dataverse hasta que `MPPP-REC`→`MPPP-ING` (reales, cada 1 minuto) hayan creado
    la Solicitud del correo que se acaba de mandar. `None` si no aparece en `timeout` segundos:
    esto NO es N-A, es información — lo decide cada caso (puede ser FALLA del pipeline real)."""
    dv = ctx.dv
    t0 = time.monotonic()
    while True:
        est, r, _ = dv.call(
            "GET", f"{sim.SOLICITUDES}?$select=sanic_mppp_tbl_solicitudid,sanic_nombre"
                   f"&$filter=sanic_asunto eq '{asunto}'&$top=1")
        if est == 200 and r.get("value"):
            return r["value"][0]
        if time.monotonic() - t0 >= timeout:
            return None
        time.sleep(intervalo)


def _esperar_campo(ctx, sid, predicado, timeout=180, intervalo=10):
    """Poll sobre los campos de comunicación de una Solicitud hasta que `predicado(fila)` sea
    verdadero. Devuelve la última lectura (verdadera o no) — nunca `None` si al menos una lectura
    tuvo éxito, para que el caso pueda mostrar el estado real con el que se rindió."""
    dv = ctx.dv
    t0 = time.monotonic()
    ultimo = None
    while True:
        est, r, _ = dv.call("GET", f"{sim.SOLICITUDES}({sid})?$select={CAMPOS_SOLICITUD}")
        if est == 200:
            ultimo = r
            if predicado(r):
                return r
        if time.monotonic() - t0 >= timeout:
            return ultimo
        time.sleep(intervalo)


def _leer_inbox(g, buzon, top=50):
    ruta = f"users/{buzon}/mailFolders/Inbox/messages?" + urllib.parse.urlencode({
        "$top": top, "$orderby": "receivedDateTime desc",
        "$select": "subject,receivedDateTime,from,body",
    })
    est, r = g.call("GET", ruta)
    if est != 200:
        raise RuntimeError(f"no se pudo leer la bandeja de entrada de {buzon} (HTTP {est}): {r}")
    return r.get("value", [])


def _buscar_inbox(g, buzon, asunto):
    return [m for m in _leer_inbox(g, buzon) if asunto in (m.get("subject") or "")]


def _buscar_respuestas_del_sistema(g, buzon, asunto):
    """Como `_buscar_inbox`, pero solo cuenta correos que el SISTEMA respondió (`From` =
    `simulador.BUZON_INGESTA`). Filtrar solo por asunto no alcanza cuando `buzon` estuvo en
    copia del correo original: ese correo original TAMBIÉN le llega a la copia por entrega
    normal de Exchange (mismo asunto, `From` = el remitente, no el sistema), y contarlo como si
    fuera una respuesta del sistema es un falso FALLA — el 2026-09-27 M-03 dio FALLA por
    exactamente esto antes de este fix (`encontrados: 1`, que resultó ser el correo original que
    la copia recibe siempre, sin que el sistema le haya respondido nada)."""
    return [m for m in _buscar_inbox(g, buzon, asunto)
            if (m.get("from", {}).get("emailAddress", {}).get("address") or "").lower() == sim.BUZON_INGESTA.lower()]


def _esperar_respuesta(g, buzon, asunto, timeout=180, intervalo=10):
    """Poll sobre la bandeja REAL hasta que aparezca una respuesta cuyo asunto contenga el
    `asunto` único de esta prueba (Outlook antepone `RE:`, por eso es `in`, no `==`)."""
    t0 = time.monotonic()
    while True:
        encontrados = _buscar_inbox(g, buzon, asunto)
        if encontrados:
            return encontrados[0]
        if time.monotonic() - t0 >= timeout:
            return None
        time.sleep(intervalo)


def _texto_plano(html):
    """Quita etiquetas y colapsa espacios. Comparar texto plano (no HTML crudo) es más robusto
    contra lo que Exchange reformatea en tránsito (indentado, atributos, saltos de línea) sin
    dejar de probar que el CONTENIDO es el mismo."""
    sin_tags = re.sub(r"<[^>]+>", " ", html or "")
    return re.sub(r"\s+", " ", sin_tags).strip()


def _revisar_privacidad(c, etiqueta, texto):
    """El criterio del aprobador (2026-09-27): un correo que lee un cliente del banco no puede
    nombrar reglas, tablas, columnas, flujos ni excepciones de plataforma — sí puede decirle a la
    persona qué hacer (reenviar, corregir, esperar)."""
    plano = _texto_plano(texto)
    for termino in TERMINOS_INTERNOS:
        c.check(f"{etiqueta}: no revela el término interno {termino!r}",
                termino.lower() not in plano.lower(),
                plano[:200] if termino.lower() in plano.lower() else "")


# ===========================================================================
# M-01 · El acuse llega, y llega una sola vez
# ===========================================================================
def m01(ctx):
    c = pc.Caso("M-01", "El acuse llega, y llega una sola vez (correo real, propia Solicitud)")
    dv = ctx.dv
    g = ctx.extra["g"]
    remitente = ctx.extra["remitente"]

    asunto = _enviar(ctx, "M01", adjunto=_bytes_excel("E01-feliz-formato11.xlsx"))
    ctx.extra["m01_asunto"] = asunto

    sol = _esperar_solicitud(ctx, asunto, timeout=240)
    c.check("el correo real generó una Solicitud (MPPP-REC/MPPP-ING reales)", sol is not None,
            f"asunto {asunto!r}: no apareció ninguna Solicitud en 240s — revisar si MPPP-REC "
            "sigue viendo el buzón (corre cada 1 min) o si el filtro hasAttachments del trigger "
            "está excluyendo el adjunto")
    if not sol:
        return c
    sid = sol["sanic_mppp_tbl_solicitudid"]
    ctx.creadas.append(sid)
    ctx.extra["m01_sid"] = sid

    r = _esperar_campo(ctx, sid, lambda x: x.get("sanic_estadoprocesamiento") == ENPROC
                       and x.get("sanic_fechaacuseenviado"), timeout=180)
    c.check("Estado = En proceso", r.get("sanic_estadoprocesamiento") == ENPROC if r else False, str(r))
    c.check("filas t/v/r = 3/3/0",
            r and (r.get("sanic_filastotales"), r.get("sanic_filasvalidas"), r.get("sanic_filasrechazadas")) == (3, 3, 0),
            str(r))
    c.check("sanic_fechaacuseiniciado y sanic_fechaacuseenviado con fecha",
            r and bool(r.get("sanic_fechaacuseiniciado")) and bool(r.get("sanic_fechaacuseenviado")), str(r))

    bit = sim.leer_bitacora(dv, sid)
    c.check("Bitácora: Acuse iniciado", any(b["sanic_evento"] == EVT_ACUSE_INICIADO for b in bit))
    c.check("Bitácora: Acuse enviado", any(b["sanic_evento"] == EVT_ACUSE_ENVIADO for b in bit))

    msg = _esperar_respuesta(g, remitente, asunto, timeout=150)
    c.check("llegó un correo real de respuesta a la bandeja del remitente", msg is not None)
    if msg:
        ctx.extra["m01_msg"] = msg
        origen = (msg.get("from", {}).get("emailAddress", {}).get("address") or "").lower()
        c.check("viene de la casilla de ingesta, como respuesta en el hilo",
                origen == sim.BUZON_INGESTA.lower(), origen)
        coincidencias = _buscar_inbox(g, remitente, asunto)
        c.check("llega UN solo correo (no más) para este asunto", len(coincidencias) == 1,
                f"hay {len(coincidencias)}")
    return c


# ===========================================================================
# M-05 · Nunca dos veces lo mismo
# ===========================================================================
def m05(ctx):
    c = pc.Caso("M-05", "Nunca dos veces lo mismo: un segundo disparo de MPPP-COM no reenvía")
    sid = ctx.extra.get("m01_sid")
    if not sid:
        c.check("depende de la Solicitud que M-01 creó en esta misma corrida", False,
                "M-01 no dejó una Solicitud lista — correlo primero y mirá por qué falló")
        return c
    dv = ctx.dv
    g = ctx.extra["g"]
    remitente = ctx.extra["remitente"]
    asunto = ctx.extra["m01_asunto"]

    antes = dv.call("GET", f"{sim.SOLICITUDES}({sid})?$select=sanic_fechaacuseenviado,sanic_estadoprocesamiento")[1]
    fecha_antes = antes.get("sanic_fechaacuseenviado")
    bit_antes = sum(1 for b in sim.leer_bitacora(dv, sid) if b["sanic_evento"] in (EVT_ACUSE_INICIADO, EVT_ACUSE_ENVIADO))
    inbox_antes = len(_buscar_inbox(g, remitente, asunto))

    # Forzar un segundo disparo de MPPP-COM: PATCH del MISMO valor de sanic_estadoprocesamiento.
    # El webhook (recursos/flujos/mppp-com.json) está suscripto a Update con
    # filteringattributes=sanic_estadoprocesamiento: Dataverse lo dispara cuando ese atributo
    # viene en el Target del Update, sea cual sea el valor nuevo (no compara valor viejo vs
    # nuevo). La garantía que se prueba es la de MPPP-ENV (sanic_fechaacuseiniciado != null →
    # no reenvía), no si el webhook refirió o no.
    est, r, _ = dv.call("PATCH", f"{sim.SOLICITUDES}({sid})",
                         {"sanic_estadoprocesamiento": antes.get("sanic_estadoprocesamiento")})
    c.check("se pudo re-disparar la actualización (PATCH aceptado)", est in (200, 204), f"HTTP {est}: {r}")

    time.sleep(60)  # tiempo prudencial: si el flujo reenviara, ya habría llegado.

    despues = dv.call("GET", f"{sim.SOLICITUDES}({sid})?$select=sanic_fechaacuseenviado")[1]
    c.check("sanic_fechaacuseenviado NO cambió", despues.get("sanic_fechaacuseenviado") == fecha_antes,
            f"antes={fecha_antes} después={despues.get('sanic_fechaacuseenviado')}")
    bit_despues = sum(1 for b in sim.leer_bitacora(dv, sid) if b["sanic_evento"] in (EVT_ACUSE_INICIADO, EVT_ACUSE_ENVIADO))
    c.check("no hay NUEVAS entradas de Bitácora de acuse", bit_despues == bit_antes,
            f"antes={bit_antes} después={bit_despues}")
    inbox_despues = len(_buscar_inbox(g, remitente, asunto))
    c.check("no llegó un segundo correo real a la bandeja", inbox_despues == inbox_antes,
            f"antes={inbox_antes} después={inbox_despues}")
    return c


# ===========================================================================
# M-04 · La respuesta final, cuando todas las filas se resuelven
# ===========================================================================
def m04(ctx):
    c = pc.Caso("M-04", "La respuesta final, cuando todas las filas se resuelven (digitar/aprobar por API, sin navegador)")
    sid = ctx.extra.get("m01_sid")
    if not sid:
        c.check("depende de la Solicitud que M-01 creó en esta misma corrida", False,
                "M-01 no dejó una Solicitud lista")
        return c
    dv = ctx.dv
    g = ctx.extra["g"]
    remitente = ctx.extra["remitente"]
    ejecutivo = ctx.extra["ejecutivo"]
    supervisor = ctx.extra["supervisor"]

    est, r, _ = dv.call(
        "GET", f"{sim.FILAS}?$select=sanic_mppp_tbl_filaid,sanic_numerofila,sanic_estado"
               f"&$filter=_sanic_solicitudid_value eq {sid}&$orderby=sanic_numerofila")
    filas = r.get("value", []) if est == 200 else []
    c.check("las 3 filas de esta Solicitud están Validadas antes de digitar",
            len(filas) == 3 and all(f["sanic_estado"] == VALIDADA for f in filas),
            str([(f["sanic_numerofila"], f["sanic_estado"]) for f in filas]))
    if len(filas) != 3:
        return c

    digitadas = sum(1 for f in filas if dv.call(
        "PATCH", f"{sim.FILAS}({f['sanic_mppp_tbl_filaid']})", {"sanic_estado": DIGITADA},
        cabeceras={"MSCRMCallerID": ejecutivo})[0] in (200, 204))
    c.check("el Ejecutivo (User1, por API con MSCRMCallerID) digita las 3 filas", digitadas == 3, f"{digitadas}/3")

    aprobadas = sum(1 for f in filas if dv.call(
        "PATCH", f"{sim.FILAS}({f['sanic_mppp_tbl_filaid']})", {"sanic_estado": APROBADA},
        cabeceras={"MSCRMCallerID": supervisor})[0] in (200, 204))
    c.check("el Supervisor (User2, por API con MSCRMCallerID) aprueba las 3 filas", aprobadas == 3, f"{aprobadas}/3")
    if digitadas != 3 or aprobadas != 3:
        return c

    resultado = _esperar_campo(ctx, sid, lambda x: x.get("sanic_estadoprocesamiento") == CERR
                                and x.get("sanic_fecharespuestafinalenviada"), timeout=180)
    c.check("Estado final = Cerrada", resultado and resultado.get("sanic_estadoprocesamiento") == CERR, str(resultado))
    c.check("sanic_fecharespuestafinalenviada y sanic_fechacerrada con fecha",
            resultado and bool(resultado.get("sanic_fecharespuestafinalenviada")) and bool(resultado.get("sanic_fechacerrada")))

    bit = sim.leer_bitacora(dv, sid)
    c.check("Bitácora: Respuesta final iniciada", any(b["sanic_evento"] == EVT_RESP_INICIADA for b in bit))
    c.check("Bitácora: Respuesta final enviada", any(b["sanic_evento"] == EVT_RESP_ENVIADA for b in bit))

    asunto = ctx.extra["m01_asunto"]
    mensajes = _buscar_inbox(g, remitente, asunto)
    c.check("llegan exactamente 2 correos reales para esta Solicitud (acuse + respuesta final)",
            len(mensajes) == 2, f"hay {len(mensajes)}")

    est2, sfin, _ = dv.call("GET", f"{sim.SOLICITUDES}({sid})?$select=sanic_respuestafinalcontenido")
    contenido = (sfin.get("sanic_respuestafinalcontenido") or "").strip() if est2 == 200 else ""
    c.check("sanic_respuestafinalcontenido no está vacío", bool(contenido))
    ctx.extra["m04_respuesta_contenido"] = contenido
    if len(mensajes) >= 2 and contenido:
        segundo = sorted(mensajes, key=lambda m: m.get("receivedDateTime") or "")[-1]
        ctx.extra["m04_msg"] = segundo
        cuerpo_real = (segundo.get("body") or {}).get("content") or ""
        c.check("el cuerpo del correo REAL de respuesta final coincide con sanic_respuestafinalcontenido",
                _texto_plano(contenido) in _texto_plano(cuerpo_real), "")
    return c


# ===========================================================================
# M-07 · El cuerpo lo arma el código, no el flujo — y no revela información interna
# ===========================================================================
def m07(ctx):
    c = pc.Caso("M-07", "El cuerpo lo arma el código, no el flujo — y no revela información interna")
    sid = ctx.extra.get("m01_sid")
    msg = ctx.extra.get("m01_msg")
    if not sid or not msg:
        c.check("depende del acuse real que M-01 dejó leído en esta misma corrida", False,
                "M-01 no dejó un correo real para comparar")
        return c
    dv = ctx.dv

    est, r, _ = dv.call("GET", f"{sim.SOLICITUDES}({sid})?$select=sanic_acusecontenido")
    contenido = (r.get("sanic_acusecontenido") or "").strip() if est == 200 else ""
    cuerpo_real = (msg.get("body") or {}).get("content") or ""
    c.check("sanic_acusecontenido no está vacío", bool(contenido))
    c.check("el cuerpo del correo REAL recibido contiene el texto de sanic_acusecontenido",
            bool(contenido) and _texto_plano(contenido) in _texto_plano(cuerpo_real),
            f"acusecontenido[:150]={contenido[:150]!r}")
    _revisar_privacidad(c, "acuse real recibido", cuerpo_real)

    msg2 = ctx.extra.get("m04_msg")
    contenido2 = ctx.extra.get("m04_respuesta_contenido")
    if msg2 and contenido2:
        cuerpo_real2 = (msg2.get("body") or {}).get("content") or ""
        _revisar_privacidad(c, "respuesta final real recibida", cuerpo_real2)
    return c


# ===========================================================================
# M-02 · El acuse de una Rechazada cierra la Solicitud
# ===========================================================================
def m02(ctx):
    c = pc.Caso("M-02", "El acuse de una Rechazada cierra la Solicitud (correo real sin adjunto)")
    dv = ctx.dv
    g = ctx.extra["g"]
    remitente = ctx.extra["remitente"]

    asunto = _enviar(ctx, "M02")  # sin adjunto: TRAE_ADJUNTO falla → Rechazada (mismo camino que S-01/C-08)
    sol = _esperar_solicitud(ctx, asunto, timeout=240)
    c.check("el correo real (sin adjunto) generó una Solicitud", sol is not None,
            f"asunto {asunto!r} no apareció en 240s")
    if not sol:
        return c
    sid = sol["sanic_mppp_tbl_solicitudid"]
    ctx.creadas.append(sid)

    resultado = _esperar_campo(ctx, sid, lambda x: x.get("sanic_estadoprocesamiento") == CERR, timeout=180)
    c.check("Estado = Rechazada → Cerrada (automático, sin digitar nada)",
            resultado and resultado.get("sanic_estadoprocesamiento") == CERR, str(resultado))
    c.check("sanic_fechacerrada con fecha", resultado and bool(resultado.get("sanic_fechacerrada")))
    c.check("hubo acuse enviado antes de cerrar", resultado and bool(resultado.get("sanic_fechaacuseenviado")))

    bit = sim.leer_bitacora(dv, sid)
    c.check("Bitácora: Acuse enviado", any(b["sanic_evento"] == EVT_ACUSE_ENVIADO for b in bit))

    msg = _esperar_respuesta(g, remitente, asunto, timeout=120)
    c.check("llegó el acuse real a la bandeja", msg is not None)
    if msg:
        _revisar_privacidad(c, "acuse real de la Rechazada", (msg.get("body") or {}).get("content") or "")
    return c


# ===========================================================================
# M-03 · Copia y remitente
# ===========================================================================
def m03(ctx):
    c = pc.Caso("M-03", "Copia y remitente: solo al remitente, nunca a los de copia (correo real con CC)")
    g = ctx.extra["g"]
    remitente = ctx.extra["remitente"]
    copia = ctx.extra["cc"]

    asunto = _enviar(ctx, "M03", adjunto=_bytes_excel("E01-feliz-formato11.xlsx"), cc=copia)
    sol = _esperar_solicitud(ctx, asunto, timeout=240)
    c.check("el correo real con copia generó una Solicitud", sol is not None,
            f"asunto {asunto!r} no apareció en 240s")
    if not sol:
        return c
    ctx.creadas.append(sol["sanic_mppp_tbl_solicitudid"])

    msg = _esperar_respuesta(g, remitente, asunto, timeout=180)
    c.check("el acuse llega a la casilla del remitente (el De)", msg is not None)
    # OJO: la copia SÍ recibe el correo ORIGINAL (entrega normal de Exchange, from=REMITENTE) —
    # eso no prueba nada. Lo que este caso prohíbe es una RESPUESTA DEL SISTEMA (from=BUZON_INGESTA).
    respuestas_del_sistema = _buscar_respuestas_del_sistema(g, copia, asunto)
    c.check("a la casilla de copia NO le llega ninguna respuesta DEL SISTEMA (el correo original que sí le "
            "llega por estar en copia no cuenta)", len(respuestas_del_sistema) == 0,
            f"respuestas del sistema encontradas: {len(respuestas_del_sistema)}")
    return c


# ===========================================================================
# M-06 · Envío iniciado sin confirmar → arma el escenario "a medias"
#
# **Alcance recortado el 2026-09-28 (dos correcciones, con evidencia real).**
#
# 1. **La aserción de Bitácora era una expectativa mal fundada, no un defecto.**
#    Investigado a fondo por el set V (`pruebas/07-vigilancia.md`, "Hallazgo
#    aparte, sobre el propio caso M-06"): ningún bloque de `MPPP-VIG` escribe
#    un evento de Bitácora `Requiere revision` (159460016) al marcar una
#    Solicitud — eso lo hace `MPPP-ENV`, en otro escenario. Exigirla acá era
#    el error del caso, no del flujo. Se sacó.
# 2. **Esperar que `sanic_requiererevision` pase a `true` en tiempo real ya lo
#    cubre V-07** (`pruebas/07-vigilancia.md`), y mejor: V-07 lee el umbral
#    REAL de `vigilancia.minutos.sinresponder` en vivo (acá estaba
#    hardcodeado a 40 min, asumiendo P-06 = 30 sin comprobrarlo), corre la
#    MISMA consulta que usa el bloque `Envios_iniciados_sin_confirmar` ANTES
#    de esperar nada (precondición decisiva, sin gastar tiempo si la consulta
#    está rota), y espera con el margen medido con evidencia — 3 vueltas del
#    `recurrence` (30 min) + colchón = 32 min, no las 1-2 vueltas (~11 y ~22
#    min) que este caso probó el 2026-09-27 y que **NINGUNA de las dos
#    alcanzó** (ver la nota diagnóstica que dejó este caso, conservada en
#    `pruebas/04-comunicaciones.md`). Repetir esa espera acá — con un margen
#    encima más corto y una precondición hardcodeada — duplicaría cobertura
#    entre dos prefijos distintos (`CONVENCIONES.md` §4) pagando otra vez
#    ~20-32 minutos por corrida del set M, por algo que V-07 ya prueba mejor.
#
# **Lo que sigue siendo trabajo de ESTE set** (M = comunicaciones, correo
# REAL — no lo prueba V, que arma la Solicitud por Custom API directa, sin
# pasar por un correo real ni por `MPPP-ING`): que un correo real llegue
# hasta "En proceso" con el acuse real enviado, y que el `PATCH` que arma el
# escenario "a medias" (precondición de datos de V-07 también) sea aceptado
# por la plataforma. Si esto último se rechaza, ni este caso ni V-07 pueden
# simular el escenario, y hay que saberlo rápido, no después de 32 minutos.
# ===========================================================================
def m06(ctx):
    c = pc.Caso("M-06", "Envío iniciado sin confirmar: precondición real de correo + escenario "
                        "'a medias' (la espera de MPPP-VIG y su detección las prueba V-07, "
                        "pruebas/07-vigilancia.md — ver el porqué en 04-comunicaciones.md)")
    dv = ctx.dv

    asunto = _enviar(ctx, "M06", adjunto=_bytes_excel("E01-feliz-formato11.xlsx"))
    sol = _esperar_solicitud(ctx, asunto, timeout=240)
    c.check("el correo real generó una Solicitud", sol is not None, f"asunto {asunto!r} no apareció en 240s")
    if not sol:
        return c
    sid = sol["sanic_mppp_tbl_solicitudid"]
    ctx.creadas.append(sid)

    # 1) Dejar que el pipeline real complete su acuse normal (es la única forma de llegar a
    #    En proceso con Solicitud real: no hay ventana para "interceptar a medias" un envío que
    #    el webhook dispara casi al instante).
    estado = _esperar_campo(ctx, sid, lambda x: x.get("sanic_estadoprocesamiento") == ENPROC
                             and x.get("sanic_fechaacuseenviado"), timeout=180)
    c.check("precondición: la Solicitud llegó a En proceso con acuse ya enviado",
            estado and bool(estado.get("sanic_fechaacuseenviado")), str(estado))
    if not (estado and estado.get("sanic_fechaacuseenviado")):
        return c

    # 2) Forzar el escenario "a medias": acuse iniciado más viejo que el umbral REAL de
    #    `vigilancia.minutos.sinresponder` (leído en vivo, nunca hardcodeado — CONVENCIONES §7,
    #    mismo criterio que usa V-07), sin confirmar. Esto reescribe un resultado que ya se dio de
    #    verdad (el acuse real ya salió); lo que se prueba con este PATCH es que la plataforma deja
    #    escribir el escenario, no la recuperación de MPPP-VIG (eso es V-07).
    minutos = pc.verificar_parametro_entero_positivo(dv, "vigilancia.minutos.sinresponder")
    hace = (datetime.datetime.now(datetime.timezone.utc)
            - datetime.timedelta(minutes=minutos + 1)).strftime("%Y-%m-%dT%H:%M:%SZ")
    est, r, _ = dv.call("PATCH", f"{sim.SOLICITUDES}({sid})",
                         {"sanic_fechaacuseiniciado": hace, "sanic_fechaacuseenviado": None})
    c.check("se pudo forzar el escenario a medias (PATCH de fechaacuseiniciado/fechaacuseenviado)",
            est in (200, 204),
            f"HTTP {est}: {r} — si esto falla, hay un control de plataforma bloqueando la "
            "escritura directa de estos campos y el caso queda N-A de verdad, no simulable")
    if est not in (200, 204):
        c.na(f"no se pudo preparar el escenario 'a medias' (HTTP {est}): {r}")
    return c


CASOS = [m01, m05, m04, m07, m02, m03, m06]
