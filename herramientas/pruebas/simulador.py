#!/usr/bin/env python3
"""Simula lo que `MPPP-ING` deja armado ANTES de clasificar y validar, y llama a
las dos Custom API de fase 1 (`sanic_mppp_capi_clasificarcorreo`,
`sanic_mppp_capi_validarsolicitud`) directo por Web API.

**Por qué existe.** `MPPP-REC` y `MPPP-ING` (Power Automate) exigen un correo
real entrando al buzón compartido: eso no se puede automatizar por Web API.
Pero TODA la lógica de negocio que las pruebas de `01`, `02` y `03` quieren
comprobar (clasificación, reglas de Solicitud, reglas de Registro, referencia)
vive en las dos Custom API, no en el flujo. `MPPP-ING` solo arma la Solicitud
con los campos exactos que ve en `recursos/flujos/mppp-ing.json` (acción
`Crear_la_solicitud`) y sube dos archivos; este módulo hace lo mismo por Web
API y después llama a las Custom API, igual que haría el flujo.

**Qué SÍ prueba** lo que se arma acá: `ClasificarCorreo`, `ValidarSolicitud`,
las reglas de nivel Correo/Solicitud/Registro, la referencia de formato 11, el
acuse. Es el corazón del sistema.

**Qué NO prueba**: que `MPPP-ING` de verdad lea Outlook, cuente adjuntos
reales (incluida la exclusión de inline, DF-07), mueva el correo a
`Procesados` o guarde el `.eml`/Excel con su nombre original. Eso exige un
correo real en el buzón compartido — lo cubre `corredor.py` marcando esos
casos puntuales N/A, nunca simulándolos como si fueran lo mismo.

Nada de acá manda correo real: `sanic_mppp_capi_clasificarcorreo` y
`sanic_mppp_capi_validarsolicitud` no comunican nada al cliente por sí mismas
(eso es `MPPP-COM`/`MPPP-ENV`, fuera de este módulo).
"""
import datetime
import os
import sys
import uuid

_AQUI = os.path.dirname(os.path.abspath(__file__))
_RAIZ = os.path.dirname(os.path.dirname(_AQUI))
sys.path.insert(0, os.path.join(_RAIZ, "herramientas"))

SOLICITUDES = "sanic_mppp_tbl_solicituds"
FILAS = "sanic_mppp_tbl_filas"
BITACORAS = "sanic_mppp_tbl_bitacoras"
RESULTADOREGLAS = "sanic_mppp_tbl_resultadoreglas"

ESTADO_INGRESADA = 159460001

CLASIFICAR_CORREO = "sanic_mppp_capi_clasificarcorreo"
VALIDAR_SOLICITUD = "sanic_mppp_capi_validarsolicitud"

BUZON_INGESTA = "mppp@55xljh.onmicrosoft.com"


def _ahora_iso():
    return datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def eml(remitente, asunto, message_id, in_reply_to=None, references=None,
        cuerpo="Correo de prueba automatizada (herramientas/pruebas/corredor.py)."):
    """Un `.eml` mínimo, válido para `LectorDeCabeceras` (fin de cabeceras = línea en
    blanco CRLFCRLF). Solo lo que el plugin liviano mira: Subject, In-Reply-To,
    References. El cuerpo nunca se interpreta (DF-08): puede ser cualquier texto."""
    lineas = [
        f"From: {remitente}",
        f"To: {BUZON_INGESTA}",
        f"Subject: {asunto}",
        f"Message-ID: <{message_id}>",
    ]
    if in_reply_to:
        lineas.append(f"In-Reply-To: <{in_reply_to}>")
    if references:
        lineas.append(f"References: <{references}>")
    lineas.append("MIME-Version: 1.0")
    lineas.append('Content-Type: text/plain; charset="utf-8"')
    texto = "\r\n".join(lineas) + "\r\n\r\n" + cuerpo + "\r\n"
    return texto.encode("utf-8")


def nuevo_message_id():
    return f"prueba-{uuid.uuid4()}@herramientas.local"


def crear_solicitud(dv, *, remitente, asunto, message_id=None, cantidad_adjuntos,
                     cantidad_excel, excel_a_subir=None, in_reply_to=None,
                     references=None, fecha_recibido=None):
    """Arma la Solicitud exactamente con los campos que pone
    `Crear_la_solicitud` de `mppp-ing.json`, sube el `.eml` simulado a
    `sanic_correocrudo` y, si `cantidad_excel == 1` y se pasó `excel_a_subir`,
    el Excel a `sanic_exceloriginal` (igual que `Hay_exactamente_un_excel` del
    flujo: con más de un Excel, NINGUNO se sube). Devuelve el GUID creado."""
    message_id = message_id or nuevo_message_id()
    cuerpo = {
        "sanic_messageid": message_id,
        "sanic_remitente": remitente,
        "sanic_asunto": asunto,
        "sanic_fecharecibido": fecha_recibido or _ahora_iso(),
        "sanic_fechaingresada": _ahora_iso(),
        "sanic_estadoprocesamiento": ESTADO_INGRESADA,
        "sanic_cantidadadjuntos": cantidad_adjuntos,
        "sanic_cantidadexcel": cantidad_excel,
    }
    est, r, cab = dv.call("POST", SOLICITUDES, cuerpo)
    if est not in (200, 201, 204):
        raise RuntimeError(f"no se pudo crear la Solicitud simulada (HTTP {est}): {r}")
    from dataverse_api import Dataverse
    sid = Dataverse.id_creado(cab)

    correo = eml(remitente, asunto, message_id, in_reply_to, references)
    est, r, _ = dv.subir_archivo(f"{SOLICITUDES}({sid})/sanic_correocrudo", "correo.eml", correo)
    if est not in (200, 204):
        raise RuntimeError(f"no se pudo subir el correo crudo simulado (HTTP {est}): {r}")

    if cantidad_excel == 1 and excel_a_subir:
        with open(excel_a_subir, "rb") as f:
            contenido = f.read()
        est, r, _ = dv.subir_archivo(f"{SOLICITUDES}({sid})/sanic_exceloriginal",
                                      os.path.basename(excel_a_subir), contenido)
        if est not in (200, 204):
            raise RuntimeError(f"no se pudo subir el excel simulado (HTTP {est}): {r}")

    return sid, message_id


def clasificar(dv, sid):
    est, r, _ = dv.call("POST", CLASIFICAR_CORREO, {"solicitudid": sid})
    if est not in (200, 204):
        raise RuntimeError(f"{CLASIFICAR_CORREO} devolvió HTTP {est}: {r}")
    return r


def validar(dv, sid):
    est, r, _ = dv.call("POST", VALIDAR_SOLICITUD, {"solicitudid": sid})
    if est not in (200, 204):
        raise RuntimeError(f"{VALIDAR_SOLICITUD} devolvió HTTP {est}: {r}")
    return r


def ingresar_y_procesar(dv, **kw):
    """El camino completo: crea la Solicitud simulada, clasifica y, si
    `procesar` da verdadero, valida. Devuelve `(sid, message_id, clasificacion, validacion_o_None)`."""
    sid, message_id = crear_solicitud(dv, **kw)
    rc = clasificar(dv, sid)
    rv = validar(dv, sid) if rc.get("procesar") else None
    return sid, message_id, rc, rv


# ---------------------------------------------------------------------------
# Lectura (solo GET, para verificar). Devuelve estructuras, no imprime nada:
# lo que imprime es `corredor.py`.
# ---------------------------------------------------------------------------
def leer_solicitud(dv, sid):
    est, r, _ = dv.call("GET", f"{SOLICITUDES}({sid})")
    if est != 200:
        raise RuntimeError(f"no se pudo leer la Solicitud {sid} (HTTP {est}): {r}")
    return r


def leer_reglas(dv, sid):
    ruta = (f"{RESULTADOREGLAS}?$select=sanic_reglacodigo,sanic_resultado,sanic_razon,sanic_efectoaplicado,sanic_orden"
            f"&$filter=_sanic_solicitudid_value eq {sid}&$orderby=sanic_orden")
    est, r, _ = dv.call("GET", ruta)
    if est != 200:
        raise RuntimeError(f"no se pudieron leer las reglas de la Solicitud {sid} (HTTP {est}): {r}")
    return {x["sanic_reglacodigo"]: x for x in r.get("value", [])}


def leer_filas(dv, sid):
    ruta = (f"{FILAS}?$select=sanic_nombre,sanic_numerofila,sanic_estado,sanic_mensaje,sanic_referencia,"
            "sanic_referenciarecibida,sanic_numerocuenta,sanic_banco,sanic_moneda,sanic_gestion,"
            f"sanic_clasificacion,_sanic_planid_value&$filter=_sanic_solicitudid_value eq {sid}"
            "&$orderby=sanic_numerofila")
    est, r, _ = dv.call("GET", ruta)
    if est != 200:
        raise RuntimeError(f"no se pudieron leer las filas de la Solicitud {sid} (HTTP {est}): {r}")
    return r.get("value", [])


def leer_bitacora(dv, sid):
    ruta = (f"{BITACORAS}?$select=sanic_evento,sanic_origen,sanic_fechaevento,sanic_detalle,sanic_numerofila"
            f"&$filter=_sanic_solicitudid_value eq {sid}&$orderby=sanic_fechaevento")
    est, r, _ = dv.call("GET", ruta)
    if est != 200:
        raise RuntimeError(f"no se pudo leer la Bitácora de la Solicitud {sid} (HTTP {est}): {r}")
    return r.get("value", [])


def borrar_solicitud(dv, sid):
    """Limpieza. Borra primero lo que cuelga de la Solicitud (por si la relación
    no tuviera cascada configurada) y al final la Solicitud misma. Ignora los
    errores de un borrado individual: lo que importa es dejar el entorno lo
    más limpio posible, no que un residuo tumbe la limpieza entera."""
    for conjunto, campo in ((FILAS, "sanic_mppp_tbl_filaid"),
                             (RESULTADOREGLAS, "sanic_mppp_tbl_resultadoreglaid"),
                             (BITACORAS, "sanic_mppp_tbl_bitacoraid")):
        est, r, _ = dv.call("GET", f"{conjunto}?$select={campo}&$filter=_sanic_solicitudid_value eq {sid}")
        for x in (r.get("value", []) if est == 200 else []):
            dv.call("DELETE", f"{conjunto}({x[campo]})")
    dv.call("DELETE", f"{SOLICITUDES}({sid})")


def cargar_encabezados(dv):
    """`{nombre_de_campo: encabezado_esperado}` de `plantilla.estructura`, la
    versión activa más alta. Se usa para armar los mensajes exactos que las
    reglas de Registro citan (nombran el CAMPO, nunca el valor: DD-01/D-41)."""
    import json as _json
    est, r, _ = dv.call(
        "GET",
        "sanic_mppp_tbl_parametros?$select=sanic_valor,sanic_version"
        "&$filter=sanic_nombre eq 'plantilla.estructura' and statecode eq 0"
        "&$orderby=sanic_version desc&$top=1")
    if est != 200 or not r.get("value"):
        raise RuntimeError(f"no se pudo leer el parámetro 'plantilla.estructura' (HTTP {est}): {r}")
    datos = _json.loads(r["value"][0]["sanic_valor"])
    return {c["nombre"]: c["encabezado"] for c in datos["campos"]}
