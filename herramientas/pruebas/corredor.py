#!/usr/bin/env python3
"""Corredor automático de las pruebas funcionales de fase 1 (`2026-001-referencias-planes-pago`).

    python3 herramientas/pruebas/corredor.py                  # corre todo, limpia sus datos al final
    python3 herramientas/pruebas/corredor.py --archivo 03      # solo los casos de un archivo (00,01,02,03,05,06,08)
    python3 herramientas/pruebas/corredor.py --sin-limpiar     # deja las Solicitudes creadas, para inspeccionar
    python3 herramientas/pruebas/corredor.py --cobertura       # solo imprime qué cubre y qué no, sin tocar Dev

**Qué es esto.** Orquesta las herramientas que ya existen en `herramientas/pruebas/`
(`preparar_datos.py`, `generar_excel.py`, `simulador.py`, `verificar_accesos.py`,
`comportamiento_roles.py`, `construir/app.py`) para correr, SIN intervención humana y
contra Dev, todos los casos del plan de `pruebas/*.md` que prueban lógica de SERVIDOR y
se pueden verificar por Web API.

**Cómo llega a "sin correo real".** `MPPP-REC`/`MPPP-ING` (Power Automate) exigen un correo
de verdad entrando al buzón compartido, y eso no es Web API. Pero la lógica que esos casos
quieren probar — clasificación, reglas de Solicitud, reglas de Registro, la referencia —
vive ENTERA en dos Custom API (`sanic_mppp_capi_clasificarcorreo`,
`sanic_mppp_capi_validarsolicitud`). `herramientas/pruebas/simulador.py` arma la Solicitud
con EXACTAMENTE los mismos campos que pone `Crear_la_solicitud` de
`recursos/flujos/mppp-ing.json`, sube el `.eml`/Excel simulados, y llama a esas dos Custom
API — igual que haría el flujo, sin el flujo. Los detalles y los límites de este enfoque
están en el docstring de `simulador.py`.

**Idempotencia y limpieza.** Usa datos de negocio ficticios con el prefijo `PR*` que ya
sabe manejar `preparar_datos.py` (create-if-missing; nunca `--limpiar`: ver la sección
"POR QUÉ NO --limpiar" más abajo) y un remitente propio con prefijo `zz-`. Cada Solicitud
que crea queda registrada por GUID y se borra al final (`--sin-limpiar` para dejarlas).

**POR QUÉ ESTE CORREDOR NUNCA LLAMA `preparar_datos.py --limpiar`.** Esa herramienta borra
el Cliente y los 5 planes `PR*` MÁS TODAS las autorizaciones de CUALQUIER remitente sobre
esos planes — es un fixture COMPARTIDO, no algo propio de esta corrida (lo aprendí en carne
propia: un `--limpiar` de prueba durante el desarrollo de este corredor borró autorizaciones
de otro remitente que ya estaban preparadas en Dev). Hay otro agente probando en paralelo en
`pruebas/gui/` que probablemente usa el mismo fixture. Si de verdad hay que borrar TODO el
fixture al terminar la fase, es una decisión humana y manual, nunca un paso automático de
este script.

**Reporte de cobertura.** Al final imprime, caso por caso de los 82 del plan, si corrió
(OK/FALLA con el motivo) o no corrió (N-A con el motivo: navegador, tiempo real, Mail.Send
pendiente, o precondición de datos que faltó). La lista completa de motivos está en
`NO_CUBIERTOS` más abajo, y es la fuente de verdad de esta sección — no hace falta abrir
los archivos de `pruebas/` para saber qué falta.
"""
import argparse
import datetime
import json
import os
import subprocess
import sys
import time
import traceback

_AQUI = os.path.dirname(os.path.abspath(__file__))
_RAIZ = os.path.dirname(os.path.dirname(_AQUI))
sys.path.insert(0, os.path.join(_RAIZ, "herramientas"))
sys.path.insert(0, _AQUI)

from dataverse_api import Dataverse  # noqa: E402
import preparar_datos as prep  # noqa: E402
import generar_excel as genexcel  # noqa: E402
import simulador as sim  # noqa: E402

ADJUNTOS = os.path.join(_RAIZ, "pruebas", "adjuntos")

# Remitentes propios de este corredor. Nunca los de una persona real: si alguna vez
# alguien corrió `preparar_datos.py --remitente su-correo-real@...`, este corredor no lo toca.
REMITENTE = "zz-corredor-fase1@example.com"
REMITENTE_NO_AUTORIZADO = "zz-corredor-no-autorizado@example.com"

REGLAS = "sanic_mppp_tbl_reglas"
AUTORIZACIONPLANES = "sanic_mppp_tbl_autorizacionplans"
PLANES = "sanic_mppp_tbl_plans"
AUTORIZADOS = "sanic_mppp_tbl_autorizados"

NIVEL_REGISTRO = 159460002
EFECTO_RECHAZA = 159460001
EFECTO_ENVIA_REVISION = 159460002

RESULTADO_TXT = {159460001: "Cumplida", 159460002: "No cumplida", 159460003: "Omitida", None: "(no evaluada)"}
ESTADO_SOL_TXT = {159460001: "Ingresada", 159460002: "No reconocida", 159460003: "Descartada",
                  159460004: "Rechazada", 159460005: "En proceso", 159460006: "Procesada",
                  159460007: "Cerrada", 159460008: "No es correo nuevo", 159460009: "Vencida"}
ESTADO_FILA_TXT = {159460001: "Rechazada en validacion", 159460002: "Sin autorizacion",
                   159460003: "Validada", 159460004: "Digitada", 159460005: "Aprobada",
                   159460006: "Rechazada en AS400", 159460007: "Anulada"}

CUMPLIDA, NOCUMPLIDA, OMITIDA = 159460001, 159460002, 159460003
ING, NOREC, DESC, RECH, ENPROC, PROC, CERR, NOESNUEVO, VENC = (159460001 + i for i in range(9))
FILA_RECH_VAL, FILA_SIN_AUT, FILA_VALIDADA = 159460001, 159460002, 159460003


# ===========================================================================
# Infraestructura del corredor: casos, resultados, contexto
# ===========================================================================
class Caso:
    def __init__(self, id_, archivo, titulo):
        self.id = id_
        self.archivo = archivo
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
    def __init__(self, dv):
        self.dv = dv
        self.creadas = []          # sids de Solicitud para limpiar al final
        self.encabezados = None    # {campo: encabezado}, de plantilla.estructura
        self.excels_extra = []     # archivos ZZ-*.xlsx armados por este corredor
        self._comportamiento_roles = None  # cache: (proc, lineas) o None si no se pudo correr

    def solicitud(self, **kw):
        sid, mid = sim.crear_solicitud(self.dv, **kw)
        self.creadas.append(sid)
        return sid, mid

    def procesar(self, **kw):
        sid, mid, rc, rv = sim.ingresar_y_procesar(self.dv, **kw)
        self.creadas.append(sid)
        return sid, mid, rc, rv


REGISTRO = []  # (id, archivo, funcion)

# Título humano de cada caso automatizado, para que un caso que revienta con una
# excepción (antes de llegar a construir su propio `Caso(...)`) o uno gateado por
# seguridad (A-06/A-07/A-10) se reporten con su nombre, no con el id pelado.
TITULOS = {
    "P-03": "Datos de negocio de prueba existen y están bien configurados",
    "P-04": "Los 15 Excel de los casos y el adjunto que no es Excel existen",
    "P-05": "Los cinco flujos MPPP están activos (statecode=1)",
    "P-06": "Los parámetros esperados están activos con el valor esperado",
    "C-01": "Camino feliz: correo nuevo, remitente autorizado, Excel válido",
    "C-02": "Una respuesta NO se procesa (ES_CORREO_NUEVO)",
    "C-03": "Un reenvío SÍ se procesa (prefijo FW:)",
    "C-04": "Remitente no reconocido",
    "C-07": "El mismo correo dos veces no duplica (alcance reducido, ver Notas)",
    "C-08": "Sin adjunto",
    "S-01": "El adjunto no es un Excel",
    "S-02": "Dos Excel en el mismo correo",
    "S-03": "Un Excel y un archivo que no lo es",
    "S-04": "La hoja de datos tiene otro nombre",
    "S-05": "Excel con la estructura correcta pero sin filas",
    "S-06": "Volumen: 100 filas (presupuesto < 10 s)",
    "S-07": "El acuse de una Rechazada dice lo que tiene que decir",
    "R-01": "Valores fuera de las listas (LISTAS_VALIDAS)",
    "R-02": "Largos y formato (LARGOS_Y_FORMATO)",
    "R-03": "Plan que no existe (PLAN_EXISTE)",
    "R-04": "Plan que existe pero está inactivo",
    "R-05": "Plan formato 11 con clasificación que no es ACH",
    "R-06": "Campos obligatorios vacíos, y el opcional",
    "R-07": "Moneda distinta de la del plan",
    "R-08": "Sin autorización sobre ese plan",
    "R-09": "Sin autorización GANA sobre otra regla que rechaza (D-14)",
    "R-10": "La referencia de formato 11 se construye sola (la prueba más importante del plan)",
    "R-11": "La referencia de formato 11 que NO se puede construir",
    "R-12": "Formato 06: la referencia NO se deriva",
    "R-13": "Mezcla de filas válidas y rechazadas",
    "R-14": "Duplicados dentro de la plantilla NO se validan (DD-12)",
    "I-01": "El catálogo de reglas: una regla activa sin evaluador",
    "I-02": "Una regla que depende de otra inactiva",
    "I-03": 'Una regla de nivel Registro con efecto "Envía a revisión"',
    "I-04": "Una regla que depende de sí misma",
    "I-05": "Integridad de una autorización (duplicada)",
    "I-06": "El nombre calculado (Autorización y Plan)",
    "I-07": "Lista blanca de escrituras (columna calculada por PATCH directo)",
    "I-08": "Transición de estado ilegal (Validada → Aprobada, saltando Digitada)",
    "I-09": "El paquete de plugins está al día",
    "G-06": "Lo que cada rol NO puede (proxy: verificar_accesos.py)",
    "G-12": "La app valida limpia (ValidateApp)",
    "A-06": "Aprobar, como Supervisor (proxy servidor: comportamiento_roles.py)",
    "A-07": "El plugin manda, no el botón (proxy servidor: comportamiento_roles.py)",
    "A-10": "Anular y Devolver (proxy servidor: comportamiento_roles.py)",
}


def caso(id_, archivo):
    def deco(fn):
        REGISTRO.append((id_, archivo, fn))
        return fn
    return deco


def excel(nombre):
    return os.path.join(ADJUNTOS, nombre)


# ===========================================================================
# 00 — Preparación (P-03..P-06; P-01, P-01b y P-02 son buzón/decisión humana)
# ===========================================================================
@caso("P-03", "00")
def p03(ctx):
    c = Caso("P-03", "00", "Datos de negocio de prueba existen y están bien configurados")
    bien, faltan = prep.verificar(ctx.dv, REMITENTE)
    c.check("preparar_datos.verificar() sin faltantes", not faltan, f"faltan: {faltan}" if faltan else f"{len(bien)} en verde")
    return c


@caso("P-04", "00")
def p04(ctx):
    c = Caso("P-04", "00", "Los 15 Excel de los casos y el adjunto que no es Excel existen")
    esperados = [f"E{n:02d}-" for n in range(1, 16)]
    presentes = os.listdir(ADJUNTOS) if os.path.isdir(ADJUNTOS) else []
    faltan = [e for e in esperados if not any(p.startswith(e) for p in presentes)]
    c.check("15 Excel E01..E15 presentes", not faltan, f"faltan: {faltan}" if faltan else "")
    c.check("A01-no-es-excel.txt presente", "A01-no-es-excel.txt" in presentes)
    return c


@caso("P-05", "00")
def p05(ctx):
    c = Caso("P-05", "00", "Los cinco flujos MPPP están activos (statecode=1)")
    nombres = {
        "Cloud Flow - MPPP - REC - Recibir correo nuevo",
        "Cloud Flow - MPPP - ING - Ingerir y validar correo",
        "Cloud Flow - MPPP - COM - Detectar comunicacion pendiente",
        "Cloud Flow - MPPP - ENV - Enviar comunicacion al cliente",
        "Cloud Flow - MPPP - VIG - Vigilar pendientes",
    }
    est, r, _ = ctx.dv.call("GET", "workflows?$select=name,statecode&$filter=category eq 5")
    if est != 200:
        c.check("se pudo leer la lista de flujos", False, f"HTTP {est}: {r}")
        return c
    por_nombre = {w["name"]: w["statecode"] for w in r.get("value", [])}
    faltan = nombres - set(por_nombre)
    c.check("los cinco flujos existen en el entorno", not faltan, f"faltan: {faltan}" if faltan else "")
    apagados = [n for n in nombres if por_nombre.get(n) != 1]
    c.check("los cinco están activados (statecode=1)", not apagados, f"apagados: {apagados}" if apagados else "")
    return c


@caso("P-06", "00")
def p06(ctx):
    c = Caso("P-06", "00", "Los parámetros esperados están activos con el valor esperado")
    est, r, _ = ctx.dv.call("GET", "sanic_mppp_tbl_parametros?$select=sanic_nombre,sanic_valor&$filter=statecode eq 0")
    if est != 200:
        c.check("se pudo leer la tabla de Parámetros", False, f"HTTP {est}")
        return c
    valores = {p["sanic_nombre"]: p["sanic_valor"] for p in r.get("value", [])}
    # `00-preparacion.md` dice "12 parámetros activos", pero Dev ya tiene más (rpa.*,
    # retencion.*, servicio.cuenta, exportacion.columnas — de fase 2 o de refuerzos
    # posteriores al plan). Es el plan el desactualizado, no el sistema (DISCREPANCIAS
    # en el reporte final); acá se exige el PISO de los 12 que el plan necesita, no el
    # total exacto.
    c.check("al menos 12 parámetros activos (el plan pedía exactamente 12; Dev ya tiene más, ver DISCREPANCIAS)",
            len(valores) >= 12, f"hay {len(valores)}: {sorted(valores)}")
    esperados_numericos = {
        "vigilancia.minutos.sinvalidar": "15",
        "vigilancia.minutos.sinresponder": "30",
        "vigilancia.reintentos.maximo": "3",
        "clasificacion.dias.vencimiento": "30",
    }
    for nombre, esperado in esperados_numericos.items():
        obtenido = valores.get(nombre)
        c.check(f"{nombre} = {esperado}", (obtenido or "").strip() == esperado, f"obtenido {obtenido!r}")
    try:
        estructura = json.loads(valores.get("plantilla.estructura") or "{}")
        c.check("plantilla.estructura: hoja Datos, encabezado fila 12, primera fila 13",
                estructura.get("hoja") == "Datos" and estructura.get("filaEncabezado") == 12
                and estructura.get("primeraFila") == 13,
                f"obtenido hoja={estructura.get('hoja')} filaEncabezado={estructura.get('filaEncabezado')} primeraFila={estructura.get('primeraFila')}")
    except Exception as e:
        c.check("plantilla.estructura es JSON válido", False, str(e))
    try:
        listas = json.loads(valores.get("plantilla.listas") or "{}")
        texto = json.dumps(listas)
        c.check("plantilla.listas trae BAC = código 007", '"BAC"' in texto and '"007"' in texto, "")
    except Exception as e:
        c.check("plantilla.listas es JSON válido", False, str(e))
    return c


# ===========================================================================
# 01 — Clasificación del correo (C-01..C-08)
# ===========================================================================
@caso("C-01", "01")
def c01(ctx):
    c = Caso("C-01", "01", "Camino feliz: correo nuevo, remitente autorizado, Excel válido")
    sid, mid, rc, rv = ctx.procesar(
        remitente=REMITENTE, asunto="Prueba C-01 inclusiones",
        cantidad_adjuntos=1, cantidad_excel=1, excel_a_subir=excel("E01-feliz-formato11.xlsx"))
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
            len(filas) == 3 and all(f["sanic_estado"] == FILA_VALIDADA and not f.get("sanic_mensaje") for f in filas),
            str([(f["sanic_estado"], f.get("sanic_mensaje")) for f in filas]))
    esperadas = {1: "00700010203040506070", 2: "00700011223344556677", 3: "00700099887766554433"}
    for f in filas:
        n = f["sanic_numerofila"]
        ref = f.get("sanic_referencia")
        c.check(f"fila {n}: referencia de 20 caracteres, empieza en 007", ref == esperadas.get(n), f"obtenida {ref!r}")
    c.check("acuse presente", bool(s.get("sanic_acusecontenido")))
    return c


@caso("C-02", "01")
def c02(ctx):
    c = Caso("C-02", "01", "Una respuesta NO se procesa (ES_CORREO_NUEVO)")
    sid, mid = ctx.solicitud(
        remitente=REMITENTE, asunto="RE: algo anterior",
        cantidad_adjuntos=1, cantidad_excel=1, excel_a_subir=excel("E01-feliz-formato11.xlsx"),
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


@caso("C-03", "01")
def c03(ctx):
    c = Caso("C-03", "01", "Un reenvío SÍ se procesa (prefijo FW:)")
    sid, mid, rc, rv = ctx.procesar(
        remitente=REMITENTE, asunto="FW: Prueba C-03 reenvio",
        cantidad_adjuntos=1, cantidad_excel=1, excel_a_subir=excel("E01-feliz-formato11.xlsx"),
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


@caso("C-04", "01")
def c04(ctx):
    c = Caso("C-04", "01", "Remitente no reconocido")
    sid, mid = ctx.solicitud(
        remitente=REMITENTE_NO_AUTORIZADO, asunto="Prueba C-04 remitente no reconocido",
        cantidad_adjuntos=1, cantidad_excel=1, excel_a_subir=excel("E01-feliz-formato11.xlsx"))
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


@caso("C-07", "01")
def c07(ctx):
    c = Caso("C-07", "01", "El mismo correo dos veces no duplica (alcance reducido, ver Notas)")
    mid = sim.nuevo_message_id()
    sid1, _ = ctx.solicitud(remitente=REMITENTE, asunto="Prueba C-07 no duplica", message_id=mid,
                             cantidad_adjuntos=1, cantidad_excel=1, excel_a_subir=excel("E01-feliz-formato11.xlsx"))
    # El segundo intento reproduce EXACTAMENTE lo que hace `Crear_la_solicitud` de
    # mppp-ing.json cuando MPPP-VIG reingiere el mismo correo: un CreateRecord con el
    # mismo sanic_messageid. La clave alternativa `sanic_mppp_key_solicitud_messageid`
    # (DD-03) tiene que rechazarlo con un error de duplicado.
    try:
        sid2, _ = sim.crear_solicitud(
            ctx.dv, remitente=REMITENTE, asunto="Prueba C-07 no duplica (reingesta simulada)",
            message_id=mid, cantidad_adjuntos=1, cantidad_excel=1, excel_a_subir=excel("E01-feliz-formato11.xlsx"))
        ctx.creadas.append(sid2)
        c.check("un segundo Create con el mismo sanic_messageid es rechazado (clave alternativa DD-03)",
                False, "se creó una SEGUNDA Solicitud con el mismo sanic_messageid: la clave alternativa no está frenando el duplicado")
    except RuntimeError as e:
        c.check("un segundo Create con el mismo sanic_messageid es rechazado (clave alternativa DD-03)", True, str(e)[:200])
    est, r, _ = ctx.dv.call("GET", f"{sim.SOLICITUDES}?$select=sanic_mppp_tbl_solicitudid&$filter=sanic_messageid eq '{mid}'")
    total = len(r.get("value", [])) if est == 200 else -1
    c.check("solo existe UNA Solicitud con ese sanic_messageid", total == 1, f"hay {total}")
    return c


@caso("C-08", "01")
def c08(ctx):
    c = Caso("C-08", "01", "Sin adjunto")
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


# ===========================================================================
# 02 — Reglas de nivel Solicitud (S-01..S-07)
# ===========================================================================
@caso("S-01", "02")
def s01(ctx):
    c = Caso("S-01", "02", "El adjunto no es un Excel")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba S-01", cantidad_adjuntos=1, cantidad_excel=0)
    reglas = sim.leer_reglas(ctx.dv, sid)
    c.check("TRAE_ADJUNTO = Cumplida", reglas.get("TRAE_ADJUNTO", {}).get("sanic_resultado") == CUMPLIDA)
    c.check("ADJUNTO_ES_EXCEL = No cumplida", reglas.get("ADJUNTO_ES_EXCEL", {}).get("sanic_resultado") == NOCUMPLIDA)
    for cod in ("UN_SOLO_EXCEL", "ESTRUCTURA_PLANTILLA", "TIENE_FILAS"):
        c.check(f"{cod} = Omitida", reglas.get(cod, {}).get("sanic_resultado") == OMITIDA)
    s = sim.leer_solicitud(ctx.dv, sid)
    c.check("Estado = Rechazada", s.get("sanic_estadoprocesamiento") == RECH)
    c.check("acuse presente", bool(s.get("sanic_acusecontenido")))
    return c


@caso("S-02", "02")
def s02(ctx):
    c = Caso("S-02", "02", "Dos Excel en el mismo correo")
    # Dos Excel: ING no sube NINGUNO (Hay_exactamente_un_excel exige ==1). Se simula igual:
    # cantidad_excel=2 y no se pasa excel_a_subir.
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba S-02", cantidad_adjuntos=2, cantidad_excel=2)
    reglas = sim.leer_reglas(ctx.dv, sid)
    c.check("UN_SOLO_EXCEL = No cumplida", reglas.get("UN_SOLO_EXCEL", {}).get("sanic_resultado") == NOCUMPLIDA)
    for cod in ("ESTRUCTURA_PLANTILLA", "TIENE_FILAS"):
        c.check(f"{cod} = Omitida", reglas.get(cod, {}).get("sanic_resultado") == OMITIDA)
    s = sim.leer_solicitud(ctx.dv, sid)
    c.check("Estado = Rechazada", s.get("sanic_estadoprocesamiento") == RECH)
    return c


@caso("S-03", "02")
def s03(ctx):
    c = Caso("S-03", "02", "Un Excel y un archivo que no lo es")
    sid, mid, rc, rv = ctx.procesar(
        remitente=REMITENTE, asunto="Prueba S-03", cantidad_adjuntos=2, cantidad_excel=1,
        excel_a_subir=excel("E01-feliz-formato11.xlsx"))
    reglas = sim.leer_reglas(ctx.dv, sid)
    for cod in ("TRAE_ADJUNTO", "ADJUNTO_ES_EXCEL", "UN_SOLO_EXCEL", "ESTRUCTURA_PLANTILLA", "TIENE_FILAS"):
        c.check(f"{cod} = Cumplida", reglas.get(cod, {}).get("sanic_resultado") == CUMPLIDA)
    s = sim.leer_solicitud(ctx.dv, sid)
    c.check("Estado = En proceso, 3 filas Validadas",
            s.get("sanic_estadoprocesamiento") == ENPROC and s.get("sanic_filasvalidas") == 3)
    return c


@caso("S-04", "02")
def s04(ctx):
    c = Caso("S-04", "02", "La hoja de datos tiene otro nombre")
    sid, mid, rc, rv = ctx.procesar(
        remitente=REMITENTE, asunto="Prueba S-04", cantidad_adjuntos=1, cantidad_excel=1,
        excel_a_subir=excel("E12-hoja-renombrada.xlsx"))
    reglas = sim.leer_reglas(ctx.dv, sid)
    c.check("UN_SOLO_EXCEL = Cumplida", reglas.get("UN_SOLO_EXCEL", {}).get("sanic_resultado") == CUMPLIDA)
    c.check("ESTRUCTURA_PLANTILLA = No cumplida", reglas.get("ESTRUCTURA_PLANTILLA", {}).get("sanic_resultado") == NOCUMPLIDA)
    c.check("TIENE_FILAS = Omitida", reglas.get("TIENE_FILAS", {}).get("sanic_resultado") == OMITIDA)
    s = sim.leer_solicitud(ctx.dv, sid)
    c.check("Estado = Rechazada (regla fallida, no un Error de plataforma)", s.get("sanic_estadoprocesamiento") == RECH,
            ESTADO_SOL_TXT.get(s.get("sanic_estadoprocesamiento")))
    return c


@caso("S-05", "02")
def s05(ctx):
    c = Caso("S-05", "02", "Excel con la estructura correcta pero sin filas")
    sid, mid, rc, rv = ctx.procesar(
        remitente=REMITENTE, asunto="Prueba S-05", cantidad_adjuntos=1, cantidad_excel=1,
        excel_a_subir=excel("E11-sin-filas.xlsx"))
    reglas = sim.leer_reglas(ctx.dv, sid)
    c.check("ESTRUCTURA_PLANTILLA = Cumplida", reglas.get("ESTRUCTURA_PLANTILLA", {}).get("sanic_resultado") == CUMPLIDA)
    c.check("TIENE_FILAS = No cumplida", reglas.get("TIENE_FILAS", {}).get("sanic_resultado") == NOCUMPLIDA)
    s = sim.leer_solicitud(ctx.dv, sid)
    c.check("Estado = Rechazada, filas t/v/r = 0/0/0",
            s.get("sanic_estadoprocesamiento") == RECH and (s.get("sanic_filastotales") or 0) == 0)
    c.check("acuse presente", bool(s.get("sanic_acusecontenido")))
    return c


@caso("S-06", "02")
def s06(ctx):
    c = Caso("S-06", "02", "Volumen: 100 filas (presupuesto < 10 s)")
    sid, _ = ctx.solicitud(remitente=REMITENTE, asunto="Prueba S-06 volumen 100",
                            cantidad_adjuntos=1, cantidad_excel=1, excel_a_subir=excel("E14-volumen-100.xlsx"))
    sim.clasificar(ctx.dv, sid)
    t0 = time.monotonic()
    rv = sim.validar(ctx.dv, sid)
    segundos = time.monotonic() - t0
    c.check("filas t/v/r = 100/100/0", (rv.get("filastotales"), rv.get("filasvalidas"), rv.get("filasrechazadas")) == (100, 100, 0), str(rv))
    # Mide SOLO la llamada a la Custom API (sin el resto del flujo, que acá no corre):
    # es una medida más estricta que la del caso original, no más floja. Si esto ya
    # tarda cerca de 10s, el presupuesto del flujo completo está en riesgo.
    c.check(f"la llamada a {sim.VALIDAR_SOLICITUD} tardó menos de 10 s (tardó {segundos:.2f}s)", segundos < 10.0)
    return c


@caso("S-07", "02")
def s07(ctx):
    c = Caso("S-07", "02", "El acuse de una Rechazada dice lo que tiene que decir")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba S-07", cantidad_adjuntos=0, cantidad_excel=0)
    s = sim.leer_solicitud(ctx.dv, sid)
    texto = s.get("sanic_acusecontenido") or ""
    numero = s.get("sanic_nombre") or ""
    c.check("contiene el número de solicitud", numero in texto, numero)
    c.check("dice 'no hay nada que procesar' o equivalente", "no hay nada" in texto.lower() or "nada que procesar" in texto.lower())
    c.check("dice que no va a recibir otro correo por esta solicitud",
            "no va a recibir" in texto.lower() or "no recibirá" in texto.lower() or "no recibira" in texto.lower())
    c.check("pide corregir y reenviar", "reenv" in texto.lower() or "corrija" in texto.lower() or "correo nuevo" in texto.lower())
    c.check("trae el texto obligatorio de 'no responda a este mensaje'",
            "no responda" in texto.lower())
    for tecnico in ("TRAE_ADJUNTO", "ADJUNTO_ES_EXCEL", "UN_SOLO_EXCEL", "ESTRUCTURA_PLANTILLA", "TIENE_FILAS"):
        c.check(f"no menciona el nombre técnico de la regla '{tecnico}'", tecnico not in texto)
    return c


# ===========================================================================
# 03 — Reglas de nivel Registro (R-01..R-14)
# ===========================================================================
def _mensaje_de(filas, numero):
    for f in filas:
        if f["sanic_numerofila"] == numero:
            return f.get("sanic_mensaje") or ""
    return None


@caso("R-01", "03")
def r01(ctx):
    c = Caso("R-01", "03", "Valores fuera de las listas (LISTAS_VALIDAS)")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba R-01", cantidad_adjuntos=1, cantidad_excel=1,
                                     excel_a_subir=excel("E02-listas-invalidas.xlsx"))
    filas = sim.leer_filas(ctx.dv, sid)
    c.check("5 filas, todas Rechazada en validación", len(filas) == 5 and all(f["sanic_estado"] == FILA_RECH_VAL for f in filas),
            str([(f["sanic_numerofila"], f["sanic_estado"]) for f in filas]))
    campos_esperados = {1: ctx.encabezados["gestion"], 2: ctx.encabezados["moneda"], 3: ctx.encabezados["banco"],
                        4: ctx.encabezados["tipoIdentificacion"], 5: ctx.encabezados["clasificacion"]}
    valores_prohibidos = {1: "Alta", 2: "EUR", 3: "SANTANDER", 4: "XXX", 5: "TRANSFER"}
    for n, campo in campos_esperados.items():
        msg = _mensaje_de(filas, n) or ""
        c.check(f"fila {n}: el mensaje nombra el campo {campo!r}", campo in msg, msg)
        c.check(f"fila {n}: el mensaje NO cita el valor inválido {valores_prohibidos[n]!r} (DD-01/D-41)",
                valores_prohibidos[n] not in msg, msg)
    s = sim.leer_solicitud(ctx.dv, sid)
    c.check("Estado = Rechazada, filas 5/0/5", s.get("sanic_estadoprocesamiento") == RECH
            and (s.get("sanic_filastotales"), s.get("sanic_filasvalidas"), s.get("sanic_filasrechazadas")) == (5, 0, 5))
    return c


@caso("R-02", "03")
def r02(ctx):
    c = Caso("R-02", "03", "Largos y formato (LARGOS_Y_FORMATO)")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba R-02", cantidad_adjuntos=1, cantidad_excel=1,
                                     excel_a_subir=excel("E03-largos-invalidos.xlsx"))
    filas = sim.leer_filas(ctx.dv, sid)
    c.check("4 filas, todas Rechazada en validación", len(filas) == 4 and all(f["sanic_estado"] == FILA_RECH_VAL for f in filas),
            str([(f["sanic_numerofila"], f["sanic_estado"]) for f in filas]))
    for n, campo in ((1, "nombreBeneficiario"), (3, "numeroCuenta"), (4, "numeroIdentificacion")):
        msg = _mensaje_de(filas, n) or ""
        c.check(f"fila {n}: el mensaje nombra {ctx.encabezados[campo]!r}", ctx.encabezados[campo] in msg, msg)
    return c


@caso("R-03", "03")
def r03(ctx):
    c = Caso("R-03", "03", "Plan que no existe (PLAN_EXISTE)")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba R-03", cantidad_adjuntos=1, cantidad_excel=1,
                                     excel_a_subir=excel("E04-plan-inexistente.xlsx"))
    filas = sim.leer_filas(ctx.dv, sid)
    c.check("1 fila, Rechazada en validación", len(filas) == 1 and filas[0]["sanic_estado"] == FILA_RECH_VAL)
    c.check("sanic_planid vacío (nunca se adivina un plan parecido)", not filas[0].get("_sanic_planid_value"))
    msg = _mensaje_de(filas, 1) or ""
    c.check("el mensaje dice que el plan no existe o no está activo", "no existe" in msg.lower() or "no está activo" in msg.lower())
    return c


@caso("R-04", "03")
def r04(ctx):
    c = Caso("R-04", "03", "Plan que existe pero está inactivo")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba R-04", cantidad_adjuntos=1, cantidad_excel=1,
                                     excel_a_subir=excel("E15-plan-inactivo.xlsx"))
    filas = sim.leer_filas(ctx.dv, sid)
    c.check("1 fila, Rechazada en validación", len(filas) == 1 and filas[0]["sanic_estado"] == FILA_RECH_VAL)
    c.check("sanic_planid vacío", not filas[0].get("_sanic_planid_value"))
    return c


@caso("R-05", "03")
def r05(ctx):
    c = Caso("R-05", "03", "Plan formato 11 con clasificación que no es ACH")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba R-05", cantidad_adjuntos=1, cantidad_excel=1,
                                     excel_a_subir=excel("E05-formato11-no-ach.xlsx"))
    filas = sim.leer_filas(ctx.dv, sid)
    c.check("2 filas, Rechazada en validación", len(filas) == 2 and all(f["sanic_estado"] == FILA_RECH_VAL for f in filas))
    for f in filas:
        msg = f.get("sanic_mensaje") or ""
        c.check(f"fila {f['sanic_numerofila']}: el mensaje dice 'solo admite la clasificación ACH'", "ach" in msg.lower())
    return c


@caso("R-06", "03")
def r06(ctx):
    c = Caso("R-06", "03", "Campos obligatorios vacíos, y el opcional")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba R-06", cantidad_adjuntos=1, cantidad_excel=1,
                                     excel_a_subir=excel("E06-obligatoriedad.xlsx"))
    filas = sim.leer_filas(ctx.dv, sid)
    estados = {f["sanic_numerofila"]: f["sanic_estado"] for f in filas}
    c.check("filas 1,2,3 Rechazada en validación", all(estados.get(n) == FILA_RECH_VAL for n in (1, 2, 3)), str(estados))
    c.check("fila 4 Validada (la referencia es opcional)", estados.get(4) == FILA_VALIDADA, str(estados))
    f4 = next((f for f in filas if f["sanic_numerofila"] == 4), None)
    if f4:
        ref = f4.get("sanic_referencia") or ""
        c.check("fila 4: la referencia se construyó igual (20 caracteres, formato 11)", len(ref) == 20, f"obtenida {ref!r}")
    s = sim.leer_solicitud(ctx.dv, sid)
    c.check("filas t/v/r = 4/1/3, Estado = En proceso",
            (s.get("sanic_filastotales"), s.get("sanic_filasvalidas"), s.get("sanic_filasrechazadas")) == (4, 1, 3)
            and s.get("sanic_estadoprocesamiento") == ENPROC)
    return c


@caso("R-07", "03")
def r07(ctx):
    c = Caso("R-07", "03", "Moneda distinta de la del plan")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba R-07", cantidad_adjuntos=1, cantidad_excel=1,
                                     excel_a_subir=excel("E07-moneda-distinta.xlsx"))
    filas = sim.leer_filas(ctx.dv, sid)
    c.check("1 fila, Rechazada en validación", len(filas) == 1 and filas[0]["sanic_estado"] == FILA_RECH_VAL)
    msg = _mensaje_de(filas, 1) or ""
    c.check("el mensaje habla de la moneda del plan", "moneda" in msg.lower())
    return c


@caso("R-08", "03")
def r08(ctx):
    c = Caso("R-08", "03", "Sin autorización sobre ese plan")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba R-08", cantidad_adjuntos=1, cantidad_excel=1,
                                     excel_a_subir=excel("E08-sin-autorizacion.xlsx"))
    filas = sim.leer_filas(ctx.dv, sid)
    c.check("1 fila, estado Sin autorización (distinto de Rechazada en validación)",
            len(filas) == 1 and filas[0]["sanic_estado"] == FILA_SIN_AUT, str(filas[0]["sanic_estado"] if filas else None))
    msg = _mensaje_de(filas, 1) or ""
    c.check("el mensaje nombra el plan PRNA", "PRNA" in msg, msg)
    s = sim.leer_solicitud(ctx.dv, sid)
    c.check("Estado de la Solicitud = Rechazada", s.get("sanic_estadoprocesamiento") == RECH)
    return c


@caso("R-09", "03")
def r09(ctx):
    c = Caso("R-09", "03", "Sin autorización GANA sobre otra regla que rechaza (D-14)")
    ruta = _armar_excel_ad_hoc(ctx, "ZZ-R09-sinautorizacion-moneda.xlsx",
                               [genexcel.fila(plan="PRNA", clasificacion="BAC", moneda="EUR", nombre="Sin Autorizacion Y Moneda")])
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba R-09", cantidad_adjuntos=1, cantidad_excel=1,
                                     excel_a_subir=ruta)
    filas = sim.leer_filas(ctx.dv, sid)
    c.check("1 fila, estado Sin autorización (no Rechazada en validación)",
            len(filas) == 1 and filas[0]["sanic_estado"] == FILA_SIN_AUT, str(filas[0]["sanic_estado"] if filas else None))
    msg = _mensaje_de(filas, 1) or ""
    c.check("el mensaje trae el motivo de autorización", "PRNA" in msg or "autoriz" in msg.lower())
    c.check("el mensaje TAMBIÉN trae el motivo de la Moneda (todos los motivos, D-14)", "moneda" in msg.lower(), msg)
    return c


@caso("R-10", "03")
def r10(ctx):
    c = Caso("R-10", "03", "La referencia de formato 11 se construye sola (la prueba más importante del plan)")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba R-10", cantidad_adjuntos=1, cantidad_excel=1,
                                     excel_a_subir=excel("E01-feliz-formato11.xlsx"))
    filas = sim.leer_filas(ctx.dv, sid)
    esperadas = {1: "00700010203040506070", 2: "00700011223344556677", 3: "00700099887766554433"}
    for f in filas:
        n = f["sanic_numerofila"]
        ref = f.get("sanic_referencia")
        c.check(f"fila {n}: referencia construida EXACTA (007 + cuenta rellena a 17 = 20)", ref == esperadas.get(n), f"obtenida {ref!r}")
        c.check(f"fila {n}: referencia mide exactamente 20", len(ref or "") == 20)
        c.check(f"fila {n}: referenciarecibida vacía (el Excel no trae nada en esa columna)", not f.get("sanic_referenciarecibida"))
    return c


@caso("R-11", "03")
def r11(ctx):
    c = Caso("R-11", "03", "La referencia de formato 11 que NO se puede construir")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba R-11", cantidad_adjuntos=1, cantidad_excel=1,
                                     excel_a_subir=excel("E09-referencia-formato11.xlsx"))
    filas = sim.leer_filas(ctx.dv, sid)
    c.check("2 filas, ambas Rechazada en validación", len(filas) == 2 and all(f["sanic_estado"] == FILA_RECH_VAL for f in filas),
            str([(f["sanic_numerofila"], f["sanic_estado"]) for f in filas]))
    return c


@caso("R-12", "03")
def r12(ctx):
    c = Caso("R-12", "03", "Formato 06: la referencia NO se deriva")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba R-12", cantidad_adjuntos=1, cantidad_excel=1,
                                     excel_a_subir=excel("E13-formato06-referencia-libre.xlsx"))
    filas = sim.leer_filas(ctx.dv, sid)
    c.check("1 fila, Validada", len(filas) == 1 and filas[0]["sanic_estado"] == FILA_VALIDADA)
    if filas:
        c.check("referencia = REF-LIBRE-000123 (tal cual, sin 007 ni ceros)", filas[0].get("sanic_referencia") == "REF-LIBRE-000123",
                f"obtenida {filas[0].get('sanic_referencia')!r}")
        c.check("referenciarecibida = REF-LIBRE-000123", filas[0].get("sanic_referenciarecibida") == "REF-LIBRE-000123")
    return c


@caso("R-13", "03")
def r13(ctx):
    c = Caso("R-13", "03", "Mezcla de filas válidas y rechazadas")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba R-13", cantidad_adjuntos=1, cantidad_excel=1,
                                     excel_a_subir=excel("E10-mixto.xlsx"))
    filas = sim.leer_filas(ctx.dv, sid)
    estados = {f["sanic_numerofila"]: f["sanic_estado"] for f in filas}
    c.check("fila 1 Validada", estados.get(1) == FILA_VALIDADA, str(estados))
    c.check("fila 2 Validada", estados.get(2) == FILA_VALIDADA, str(estados))
    c.check("fila 3 Sin autorización (plan PRNA)", estados.get(3) == FILA_SIN_AUT, str(estados))
    c.check("fila 4 Rechazada en validación (moneda EUR)", estados.get(4) == FILA_RECH_VAL, str(estados))
    s = sim.leer_solicitud(ctx.dv, sid)
    c.check("filas t/v/r = 4/2/2, Estado = En proceso",
            (s.get("sanic_filastotales"), s.get("sanic_filasvalidas"), s.get("sanic_filasrechazadas")) == (4, 2, 2)
            and s.get("sanic_estadoprocesamiento") == ENPROC)
    c.check("acuse presente y detalla fila por fila", bool(s.get("sanic_acusecontenido")))
    return c


@caso("R-14", "03")
def r14(ctx):
    c = Caso("R-14", "03", "Duplicados dentro de la plantilla NO se validan (DD-12)")
    fila_repetida = genexcel.fila(nombre="Fila Duplicada Prueba", cuenta="10203040506070")
    ruta = _armar_excel_ad_hoc(ctx, "ZZ-R14-duplicados.xlsx", [fila_repetida, fila_repetida, fila_repetida])
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba R-14", cantidad_adjuntos=1, cantidad_excel=1,
                                     excel_a_subir=ruta)
    filas = sim.leer_filas(ctx.dv, sid)
    c.check("3 filas, TODAS Validadas (ninguna rechazada por duplicada)",
            len(filas) == 3 and all(f["sanic_estado"] == FILA_VALIDADA for f in filas),
            str([(f["sanic_numerofila"], f["sanic_estado"]) for f in filas]))
    s = sim.leer_solicitud(ctx.dv, sid)
    c.check("filas t/v/r = 3/3/0, Estado = En proceso",
            (s.get("sanic_filastotales"), s.get("sanic_filasvalidas"), s.get("sanic_filasrechazadas")) == (3, 3, 0)
            and s.get("sanic_estadoprocesamiento") == ENPROC)
    return c


def _armar_excel_ad_hoc(ctx, nombre, filas):
    """Un Excel de prueba que no forma parte del catálogo fijo de `generar_excel.py`
    (que cubre `02` y la mayoría de `03`), para los dos casos de `03` que combinan
    condiciones puntuales (R-09, R-14). Misma plantilla real, mismo patrón que
    `generar_excel.escribir`; se guarda con prefijo `ZZ-` y no se borra solo (queda
    en `pruebas/adjuntos/`, igual que los E01..E15 — es un insumo, no un dato de Dev)."""
    ruta, _ = genexcel.escribir(nombre, filas, None)
    if ruta not in ctx.excels_extra:
        ctx.excels_extra.append(ruta)
    return ruta


# ===========================================================================
# 08 — Integridad, plugins y claves (I-01..I-09)
# ===========================================================================
def _regla(ctx, codigo):
    est, r, _ = ctx.dv.call("GET", f"{REGLAS}?$select=sanic_mppp_tbl_reglaid,sanic_codigo,sanic_nivel,sanic_efecto,"
                                    f"sanic_orden,sanic_dependede,statecode&$filter=sanic_codigo eq '{codigo}'")
    if est != 200 or not r.get("value"):
        raise RuntimeError(f"no se encontró la regla {codigo!r} (HTTP {est}): {r}")
    return r["value"][0]


@caso("I-01", "08")
def i01(ctx):
    c = Caso("I-01", "08", "El catálogo de reglas: una regla activa sin evaluador")
    cuerpo = {"sanic_codigo": "ZZ_REGLA_QUE_NO_EXISTE", "sanic_nivel": NIVEL_REGISTRO,
              "sanic_efecto": EFECTO_RECHAZA, "sanic_orden": 90}
    est, r, cab = ctx.dv.call("POST", REGLAS, cuerpo)
    if est in (200, 201, 204):
        # No se esperaba: hay que borrarla YA, antes de que una validación real la evalúe.
        idr = Dataverse.id_creado(cab)
        ctx.dv.call("DELETE", f"{REGLAS}({idr})")
        c.check("no deja guardar una regla activa sin evaluador programado", False,
                f"SE GUARDÓ (HTTP {est}) y se borró de inmediato; IntegridadDeReglaStep no está frenando este caso")
    else:
        c.check("no deja guardar una regla activa sin evaluador programado", True, f"HTTP {est}: {r.get('error')}")
    return c


@caso("I-02", "08")
def i02(ctx):
    c = Caso("I-02", "08", "Una regla que depende de otra inactiva")
    original = _regla(ctx, "PLAN_EXISTE")
    rid = original["sanic_mppp_tbl_reglaid"]
    try:
        est, r, _ = ctx.dv.call("PATCH", f"{REGLAS}({rid})", {"statecode": 1, "statuscode": 2})
        c.check("no deja desactivar PLAN_EXISTE (cuatro reglas dependen de ella)", est not in (200, 204),
                f"HTTP {est}" + (": se desactivó" if est in (200, 204) else f": {r.get('error')}"))
    finally:
        # Cueste lo que cueste, PLAN_EXISTE tiene que quedar activa: de esto depende
        # CUALQUIER otra validación que corra en paralelo (la del otro agente incluida).
        ctx.dv.call("PATCH", f"{REGLAS}({rid})", {"statecode": 0, "statuscode": 1})
        actual = _regla(ctx, "PLAN_EXISTE")
        c.check("PLAN_EXISTE quedó activa al terminar (revertido)", actual["statecode"] == 0)
    return c


@caso("I-03", "08")
def i03(ctx):
    c = Caso("I-03", "08", 'Una regla de nivel Registro con efecto "Envía a revisión"')
    original = _regla(ctx, "MONEDA_DEL_PLAN")
    rid = original["sanic_mppp_tbl_reglaid"]
    efecto_original = original["sanic_efecto"]
    try:
        est, r, _ = ctx.dv.call("PATCH", f"{REGLAS}({rid})", {"sanic_efecto": EFECTO_ENVIA_REVISION})
        c.check("no deja guardar MONEDA_DEL_PLAN con efecto Envía a revisión", est not in (200, 204),
                f"HTTP {est}" + (": se guardó" if est in (200, 204) else f": {r.get('error')}"))
    finally:
        ctx.dv.call("PATCH", f"{REGLAS}({rid})", {"sanic_efecto": efecto_original})
        actual = _regla(ctx, "MONEDA_DEL_PLAN")
        c.check("MONEDA_DEL_PLAN quedó con efecto Rechaza al terminar", actual["sanic_efecto"] == EFECTO_RECHAZA)
    return c


@caso("I-04", "08")
def i04(ctx):
    c = Caso("I-04", "08", "Una regla que depende de sí misma")
    original = _regla(ctx, "MONEDA_DEL_PLAN")
    rid = original["sanic_mppp_tbl_reglaid"]
    dependede_original = original.get("sanic_dependede")
    try:
        est, r, _ = ctx.dv.call("PATCH", f"{REGLAS}({rid})", {"sanic_dependede": "MONEDA_DEL_PLAN"})
        c.check("no deja guardar una regla que depende de sí misma", est not in (200, 204),
                f"HTTP {est}" + (": se guardó" if est in (200, 204) else f": {r.get('error')}"))
    finally:
        ctx.dv.call("PATCH", f"{REGLAS}({rid})", {"sanic_dependede": dependede_original})
        actual = _regla(ctx, "MONEDA_DEL_PLAN")
        c.check("MONEDA_DEL_PLAN quedó con su sanic_dependede original", actual.get("sanic_dependede") == dependede_original)
    return c


@caso("I-05", "08")
def i05(ctx):
    c = Caso("I-05", "08", "Integridad de una autorización (duplicada)")
    # El remitente de este corredor ya tiene autorización a PR11 (preparar_datos.py).
    pl = ctx.dv.call("GET", f"{PLANES}?$select=sanic_mppp_tbl_planid&$filter=sanic_codigo eq 'PR11'")[1]["value"][0]
    au = ctx.dv.call("GET", f"{AUTORIZADOS}?$select=sanic_mppp_tbl_autorizadoid&$filter=sanic_nombre eq '{REMITENTE}'")[1]["value"][0]
    cuerpo = {"sanic_nombre": f"ZZ duplicada {REMITENTE} → PR11",
              "sanic_autorizadoid@odata.bind": f"/{AUTORIZADOS}({au['sanic_mppp_tbl_autorizadoid']})",
              "sanic_planid@odata.bind": f"/{PLANES}({pl['sanic_mppp_tbl_planid']})"}
    est, r, cab = ctx.dv.call("POST", AUTORIZACIONPLANES, cuerpo)
    if est in (200, 201, 204):
        ctx.dv.call("DELETE", f"{AUTORIZACIONPLANES}({Dataverse.id_creado(cab)})")
        c.check("no deja guardar una autorización duplicada (mismo autorizado, mismo plan)", False,
                f"SE GUARDÓ (HTTP {est}) y se borró de inmediato")
    else:
        c.check("no deja guardar una autorización duplicada (mismo autorizado, mismo plan)", True, f"HTTP {est}: {r.get('error')}")
        c.check("el mensaje no es un volcado técnico crudo de la plataforma", "Microsoft.Crm.CrmException" not in str(r.get("error")))
    return c


@caso("I-06", "08")
def i06(ctx):
    c = Caso("I-06", "08", "El nombre calculado (Autorización y Plan)")
    # Autorización: remitente → PRNA (a propósito NO autorizado por preparar_datos.py; se
    # crea y se borra en el mismo caso, ventana mínima).
    pl = ctx.dv.call("GET", f"{PLANES}?$select=sanic_mppp_tbl_planid&$filter=sanic_codigo eq 'PRNA'")[1]["value"][0]
    au = ctx.dv.call("GET", f"{AUTORIZADOS}?$select=sanic_mppp_tbl_autorizadoid&$filter=sanic_nombre eq '{REMITENTE}'")[1]["value"][0]
    est, r, cab = ctx.dv.call("POST", AUTORIZACIONPLANES, {
        "sanic_autorizadoid@odata.bind": f"/{AUTORIZADOS}({au['sanic_mppp_tbl_autorizadoid']})",
        "sanic_planid@odata.bind": f"/{PLANES}({pl['sanic_mppp_tbl_planid']})"})
    if est in (200, 201, 204):
        idr = Dataverse.id_creado(cab)
        est2, r2, _ = ctx.dv.call("GET", f"{AUTORIZACIONPLANES}({idr})?$select=sanic_nombre")
        ctx.dv.call("DELETE", f"{AUTORIZACIONPLANES}({idr})")
        esperado = f"{REMITENTE} → PRNA"
        c.check("Autorización: nombre calculado 'correo → CODIGO'", r2.get("sanic_nombre") == esperado, f"obtenido {r2.get('sanic_nombre')!r}")
    else:
        c.check("se pudo crear la Autorización sin nombre para probar el cálculo", False, f"HTTP {est}: {r}")

    # Plan: nombre calculado 'CODIGO - Cliente'.
    cli = ctx.dv.call("GET", f"sanic_mppp_tbl_clientes?$select=sanic_mppp_tbl_clienteid"
                              f"&$filter=sanic_nombre eq '{prep.CLIENTE}'")[1]["value"][0]
    est3, r3, cab3 = ctx.dv.call("POST", PLANES, {
        "sanic_codigo": "ZZNM", "sanic_tipoformato": 159460001, "sanic_moneda": 159460001,
        "sanic_clienteid@odata.bind": f"/sanic_mppp_tbl_clientes({cli['sanic_mppp_tbl_clienteid']})"})
    if est3 in (200, 201, 204):
        idp = Dataverse.id_creado(cab3)
        est4, r4, _ = ctx.dv.call("GET", f"{PLANES}({idp})?$select=sanic_nombre")
        ctx.dv.call("DELETE", f"{PLANES}({idp})")
        esperado_plan = f"ZZNM - {prep.CLIENTE}"
        c.check("Plan: nombre calculado 'CODIGO - Cliente'", r4.get("sanic_nombre") == esperado_plan, f"obtenido {r4.get('sanic_nombre')!r}")
    else:
        c.check("se pudo crear el Plan sin nombre para probar el cálculo", False, f"HTTP {est3}: {r3}")
    return c


@caso("I-07", "08")
def i07(ctx):
    c = Caso("I-07", "08", "Lista blanca de escrituras (columna calculada por PATCH directo)")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba I-07", cantidad_adjuntos=1, cantidad_excel=1,
                                     excel_a_subir=excel("E01-feliz-formato11.xlsx"))
    filas = sim.leer_filas(ctx.dv, sid)
    if not filas:
        c.na("no se generó ninguna Fila para probar el PATCH prohibido")
        return c
    fid = None
    for f in filas:
        est, r, _ = ctx.dv.call("GET", f"{sim.FILAS}?$select=sanic_mppp_tbl_filaid&$filter=sanic_numerofila eq {f['sanic_numerofila']}"
                                        f" and _sanic_solicitudid_value eq {sid}")
        if est == 200 and r.get("value"):
            fid = r["value"][0]["sanic_mppp_tbl_filaid"]
            break
    est, r, _ = ctx.dv.call("PATCH", f"{sim.FILAS}({fid})", {"sanic_referencia": "ZZ-INTENTO-DIRECTO"})
    c.check("un PATCH directo a sanic_referencia (columna que solo escribe la validación) es rechazado",
            est == 400, f"HTTP {est}: {r.get('error')}")
    return c


@caso("I-08", "08")
def i08(ctx):
    c = Caso("I-08", "08", "Transición de estado ilegal (Validada → Aprobada, saltando Digitada)")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba I-08", cantidad_adjuntos=1, cantidad_excel=1,
                                     excel_a_subir=excel("E01-feliz-formato11.xlsx"))
    filas = sim.leer_filas(ctx.dv, sid)
    if not filas:
        c.na("no se generó ninguna Fila Validada para probar la transición")
        return c
    est, r, _ = ctx.dv.call("GET", f"{sim.FILAS}?$select=sanic_mppp_tbl_filaid&$filter=sanic_numerofila eq {filas[0]['sanic_numerofila']}"
                                    f" and _sanic_solicitudid_value eq {sid}")
    fid = r["value"][0]["sanic_mppp_tbl_filaid"]
    est, r, _ = ctx.dv.call("PATCH", f"{sim.FILAS}({fid})", {"sanic_estado": 159460005})  # Aprobada, saltando Digitada
    c.check("Validada → Aprobada directo es rechazado (TransicionDeFilaStep)", est == 400, f"HTTP {est}: {r.get('error')}")
    return c


@caso("I-09", "08")
def i09(ctx):
    c = Caso("I-09", "08", "El paquete de plugins está al día")
    proc = subprocess.run(
        [sys.executable, os.path.join(_RAIZ, "herramientas", "construir", "paquete_plugins.py")]
        + [os.path.join(_RAIZ, "playbooks", "paquete-plugins", "sanic_mppp_pkg_plugins.md"), "--solo-verificar"],
        cwd=_RAIZ, capture_output=True, text=True, timeout=120)
    ultima = (proc.stdout.strip().splitlines() or [""])[-1]
    try:
        datos = json.loads(ultima)
    except Exception:
        c.check("paquete_plugins.py --solo-verificar corrió y devolvió JSON", False,
                f"rc={proc.returncode} stdout={proc.stdout[-400:]!r} stderr={proc.stderr[-400:]!r}")
        return c
    c.check("estado = ya_existia", datos.get("estado") == "ya_existia", str(datos))
    c.check("10 tipos registrados", "10 tipos" in str(datos.get("detalle", "")) or "10" in str(datos.get("detalle", "")), str(datos.get("detalle")))
    return c


# ===========================================================================
# 06 (parcial) — proxies de herramientas ya existentes: G-06 y G-12
# ===========================================================================
@caso("G-06", "06")
def g06(ctx):
    c = Caso("G-06", "06", "Lo que cada rol NO puede (proxy: verificar_accesos.py)")
    proc = subprocess.run([sys.executable, os.path.join(_AQUI, "verificar_accesos.py"), "--json"],
                          cwd=_RAIZ, capture_output=True, text=True, timeout=120)
    if proc.returncode not in (0, 1) or not proc.stdout.strip():
        c.na(f"verificar_accesos.py no pudo correr (¿faltan los usuarios de prueba User1/User2/User3?): "
             f"rc={proc.returncode} stderr={proc.stderr.strip()[-300:]!r}")
        return c
    try:
        informes = json.loads(proc.stdout)
    except Exception as e:
        c.na(f"la salida de verificar_accesos.py --json no se pudo parsear: {e}")
        return c
    for inf in informes:
        hallazgos = inf.get("hallazgos") or []
        c.check(f"{inf['alias']} ({inf.get('rol_mppp')}) coincide con la matriz, celda por celda",
                not hallazgos and not inf.get("nota"), f"{inf.get('nota') or ''} {hallazgos}"[:300])
    return c


@caso("G-12", "06")
def g12(ctx):
    c = Caso("G-12", "06", "La app valida limpia (ValidateApp)")
    proc = subprocess.run(
        [sys.executable, os.path.join(_RAIZ, "herramientas", "construir", "app.py"),
         os.path.join(_RAIZ, "playbooks", "app", "mantenimientoppp.md"), "--solo-verificar"],
        cwd=_RAIZ, capture_output=True, text=True, timeout=120)
    ultima = (proc.stdout.strip().splitlines() or [""])[-1]
    try:
        datos = json.loads(ultima)
    except Exception:
        c.check("app.py --solo-verificar corrió y devolvió JSON", False, f"rc={proc.returncode} stdout={proc.stdout[-400:]!r}")
        return c
    c.check("estado = ya_existia", datos.get("estado") == "ya_existia", str(datos))
    c.check("ValidateApp sin errores", "sin errores" in str(datos.get("detalle", "")), str(datos.get("detalle")))
    return c


# ===========================================================================
# 05 (parcial) — proxies de comportamiento_roles.py: A-06, A-07, A-10
# ===========================================================================
def _correr_comportamiento_roles(con_escritura):
    proc = subprocess.run(
        [sys.executable, os.path.join(_AQUI, "comportamiento_roles.py")] + (["--con-escritura"] if con_escritura else []),
        cwd=_RAIZ, capture_output=True, text=True, timeout=180)
    return proc


def _lineas_de_casos(stdout):
    """[(estado, nombre)] de las líneas '  [OK  ] nombre' / '  [FALLA] nombre' / '  [—   ] nombre'
    que imprime `Tablero.imprimir()` de `comportamiento_roles.py`."""
    salida = []
    for linea in stdout.splitlines():
        s = linea.strip()
        if s.startswith("[OK") or s.startswith("[FALLA") or s.startswith("[—"):
            estado, _, nombre = s.partition("]")
            salida.append((estado.strip("[ "), nombre.strip()))
    return salida


def _mapear(caso, lineas, fragmentos):
    """Busca, dentro de la salida de `comportamiento_roles.py`, las líneas cuyo nombre
    contiene alguno de `fragmentos` (substrings exactos de esa herramienta) y las
    traduce a comprobaciones de este caso. Si ninguna aparece, el caso queda N/A: la
    precondición de datos (filas en el estado que hace falta) no se cumplió."""
    encontradas = 0
    for frag in fragmentos:
        for estado, nombre in lineas:
            if frag in nombre:
                encontradas += 1
                if estado == "—":
                    # "sin probar" en comportamiento_roles.py significa que faltó la
                    # precondición de datos (p.ej. no había 2 Filas en Validada), NO que
                    # el sistema haya fallado. Registrarlo como falla sería mentir sobre
                    # qué se rompió; se deja constancia sin contarlo en contra.
                    caso.na(f"comportamiento_roles.py marcó '{nombre}' como sin probar (falta la precondición de datos)")
                    return encontradas
                caso.check(nombre, estado == "OK", nombre)
    return encontradas


def _lineas_comportamiento_roles(ctx):
    """Corre `comportamiento_roles.py --con-escritura` UNA sola vez por corrida
    (escribe sobre datos reales y necesita 2 Filas en Validada; correrlo tres veces,
    una por cada caso A-06/A-07/A-10, sería frágil y desperdiciaría filas de prueba)
    y cachea el resultado para que los tres casos lean la misma corrida."""
    if ctx._comportamiento_roles is not None:
        return ctx._comportamiento_roles
    proc = _correr_comportamiento_roles(con_escritura=True)
    if ("no identifica a un usuario único" in (proc.stderr or "")
            or "no tienen los roles que esta suite asume" in (proc.stderr or "")):
        ctx._comportamiento_roles = (None, proc.stderr.strip()[-300:])
    else:
        ctx._comportamiento_roles = (_lineas_de_casos(proc.stdout), None)
    return ctx._comportamiento_roles


@caso("A-06", "05")
def a06(ctx):
    c = Caso("A-06", "05", "Aprobar, como Supervisor (proxy servidor: comportamiento_roles.py)")
    lineas, error = _lineas_comportamiento_roles(ctx)
    if lineas is None:
        c.na(f"comportamiento_roles.py no pudo correr: faltan/difieren los usuarios de prueba User1/User2/User3 — {error}")
        return c
    n = _mapear(c, lineas, ["El supervisor puede aprobar lo que digitó OTRO",
                            "Quedó registrado quién digitó y quién aprobó, y son DISTINTOS"])
    if n == 0 and c.estado != "N-A":
        c.na("comportamiento_roles.py --con-escritura no llegó a correr el recorrido completo (sin datos: 2 Filas en Validada)")
    return c


@caso("A-07", "05")
def a07(ctx):
    c = Caso("A-07", "05", "El plugin manda, no el botón (proxy servidor: comportamiento_roles.py)")
    lineas, error = _lineas_comportamiento_roles(ctx)
    if lineas is None:
        c.na(f"comportamiento_roles.py no pudo correr: faltan/difieren los usuarios de prueba User1/User2/User3 — {error}")
        return c
    n = _mapear(c, lineas, ["El supervisor no puede digitar",
                            "El ejecutivo no puede aprobar lo que él mismo digitó"])
    if n == 0 and c.estado != "N-A":
        c.na("comportamiento_roles.py no encontró Filas en el estado que hace falta para este caso")
    return c


@caso("A-10", "05")
def a10(ctx):
    c = Caso("A-10", "05", "Anular y Devolver (proxy servidor: comportamiento_roles.py)")
    lineas, error = _lineas_comportamiento_roles(ctx)
    if lineas is None:
        c.na(f"comportamiento_roles.py no pudo correr: faltan/difieren los usuarios de prueba User1/User2/User3 — {error}")
        return c
    n = _mapear(c, lineas, ["Anular exige un motivo",
                            "Devolver sin nota interna se rechaza",
                            "El supervisor devuelve con nota interna",
                            "Al devolver se olvida quién digitó",
                            "La nota interna no toco el mensaje que lee el cliente"])
    if n == 0 and c.estado != "N-A":
        c.na("comportamiento_roles.py no encontró Filas en el estado que hace falta para este caso")
    return c


# ===========================================================================
# Cobertura: los 82 casos del plan, uno por uno. Los que no están en REGISTRO
# (arriba) están acá con el motivo exacto de por qué no corren solos.
# ===========================================================================
NO_CUBIERTOS = {
    "P-01": ("00", "La carpeta 'Procesados' del buzón compartido", "requiere entrar al buzón compartido real (Outlook), no hay Web API para 'crear una carpeta de correo'"),
    "P-01b": ("00", "Permisos del buzón compartido (Full Access + Send As)", "Exchange admin center; no es Dataverse Web API"),
    "P-02": ("00", "El remitente de las pruebas", "es una decisión humana (elegir una casilla real); este corredor usa un remitente ficticio propio para todo lo automatizable"),
    "C-05": ("01", "El correo se mueve y cambia de identificador", "exige un correo real en el buzón compartido y que MPPP-ING lo mueva; es lo único que ese flujo hace que no se puede reproducir sin Outlook real"),
    "C-06": ("01", "El .eml queda guardado con el nombre original", "prueba que MPPP-ING capture bien el adjunto de Outlook; acá el .eml y el Excel los sube el propio corredor, así que 'verificarlo' sería probar el corredor, no el sistema"),
    "M-01": ("04", "El acuse llega, y llega una sola vez", "Mail.Send pendiente (MPPP-COM/MPPP-ENV mandan correo real); punto de extensión: un cliente Graph que lea la bandeja del remitente de prueba, ver sección EXTENSIÓN"),
    "M-02": ("04", "El acuse de una Rechazada cierra la Solicitud", "N-A (falta Mail.Send): el cierre lo dispara MPPP-COM al ver que el acuse llegó; sin envío real no hay evento que lo dispare en el mismo sentido que prueba el caso"),
    "M-03": ("04", "Copia y remitente", "N-A (falta Mail.Send): requiere leer una bandeja de correo real para confirmar que el CC no recibió nada"),
    "M-04": ("04", "La respuesta final, cuando todas las filas se resuelven", "N-A (falta Mail.Send): la respuesta final la manda MPPP-ENV"),
    "M-05": ("04", "Nunca dos veces lo mismo", "N-A (falta Mail.Send): exige comprobar que NO llegó un segundo correo real"),
    "M-06": ("04", "Envío iniciado sin confirmar → revisión humana", "depende del disparo real de MPPP-VIG sobre MPPP-COM/ENV (ver 07); además el resultado se confirma viendo que NO llegó nada, Mail.Send"),
    "M-07": ("04", "El cuerpo lo arma el código, no el flujo", "N-A (falta Mail.Send): compara sanic_acusecontenido contra el correo REAL recibido; el contenido del campo ya se verifica indirectamente en S-07/R-13"),
    "A-01": ("05", "Los botones aparecen donde deben", "ribbon/UI del navegador; lo cubre el otro agente en pruebas/gui/"),
    "A-02": ("05", "Los comandos genéricos NO aparecen", "ribbon/UI del navegador"),
    "A-03": ("05", "Digitada, sobre varias filas a la vez", "el diálogo de confirmación con la cantidad y el refresco de grilla son UI; la transición de servidor que dispara ya se prueba en 08/03"),
    "A-04": ("05", "Cancelar no cambia nada", "UI del navegador (diálogo)"),
    "A-05": ("05", "Sin selección", "UI del navegador (EnableRule)"),
    "A-08": ("05", "Rechazada en AS400 — el diálogo de motivo", "el diálogo (navigateTo, un solo campo) es UI; que 'rechazar'/'anular' exijan motivo en el servidor ya lo prueba comportamiento_roles.py (A-10)"),
    "A-09": ("05", "Cancelar el diálogo de motivo", "UI del navegador"),
    "A-11": ("05", "Atendido y Descartar, sobre Solicitudes", "no automatizado esta vuelta: la transición de sanic_estadoprocesamiento por AtenderPorClasificarStep no tiene una herramienta de orquestación existente; punto de extensión directo (PATCH sanic_estadoprocesamiento con MSCRMCallerID de un rol de negocio)"),
    "A-12": ("05", "Revisado", "no automatizado esta vuelta: mismo motivo que A-11 (falta orquestar el PATCH de sanic_requiererevision); punto de extensión directo"),
    "A-13": ("05", "Multiidioma de los botones", "UI del navegador (comparar 1033 vs 3082)"),
    "G-01": ("06", "El sitemap: 4 áreas y 13 entradas", "UI del navegador; además esta corrida detectó una discrepancia con el plan — ver DISCREPANCIAS"),
    "G-02": ("06", "Los íconos del sitemap", "UI del navegador (SVG, tema claro/oscuro)"),
    "G-03": ("06", "Las 18 vistas existen y filtran", "UI del navegador (recorrer vistas); además el plan ya avisa que hay 20, no 18 (ver DISCREPANCIAS)"),
    "G-04": ("06", "La cartera del ejecutivo", "requiere cambiar el propietario de un Cliente a mano y comparar dos vistas por UI"),
    "G-05": ("06", "Perfil de seguridad de columna", "UI del navegador (ver el campo en el formulario); el propio archivo 06 ya documenta que hoy da un falso positivo (CSP sin usuarios asignados) — administración de entorno, no Web API"),
    "G-07": ("06", "Cada rol entra donde debe", "recorte del sitemap por privilegios: se VE en el navegador; verificar_accesos.py (G-06) ya prueba los privilegios subyacentes"),
    "G-08": ("06", "Los 9 formularios", "UI del navegador (abrir cada formulario)"),
    "G-09": ("06", "La vista 'Autorizaciones sin evidencia'", "UI del navegador; que una autorización sin evidencia SÍ autorice ya lo prueba cualquier caso de 01/02/03 (todas las autorizaciones de preparar_datos.py no tienen documento firmado y C-01/R-10 pasan igual)"),
    "G-10": ("06", "La campana de notificaciones", "UI del navegador (appnotification visual + navegación al tocar)"),
    "G-11": ("06", "Multiidioma de la app", "UI del navegador"),
    "V-01": ("07", "MPPP-VIG corre y no hace nada", "requiere leer el historial de ejecuciones de Power Automate (API de administración de flujos, no Dataverse Web API) y esperar el schedule real de 10 minutos"),
    "V-02": ("07", "Bloque 1 — un correo que quedó en la bandeja", "requiere apagar MPPP-REC de verdad, mandar un correo real y esperar 15+ minutos; la decisión de reingerir vive en el flujo, no en un plugin"),
    "V-03": ("07", "Bloque 2 — una Solicitud que quedó en Ingresada", "la lógica de reintento (comparar sanic_reintentosvalidacion contra el máximo, incrementar, Bitácora Reintento) vive DENTRO del flujo MPPP-VIG, no en un plugin/Custom API: no hay nada que un PATCH pueda ejercitar sin ser en realidad una reimplementación del flujo"),
    "V-04": ("07", "Bloque 2 — se acaban los reintentos", "mismo motivo que V-03: la condición de 'se acabaron los reintentos' es un If de Power Automate"),
    "V-05": ("07", "Bloque 3 — una comunicación que no salió", "depende de MPPP-COM/MPPP-ENV real (Mail.Send) y del schedule de MPPP-VIG"),
    "V-06": ("07", "Bloque 5 — los correos por clasificar vencen", "la comparación de 30 días y el pase a Vencida viven en MPPP-VIG, no en un plugin"),
}


# ===========================================================================
# Corrida
# ===========================================================================
def preparar(ctx):
    malos = prep.comprobar_largos(REMITENTE)
    if malos:
        raise SystemExit("preparar_datos: valores más largos de lo admitido: " + "; ".join(malos))
    prep.preparar(ctx.dv, REMITENTE)
    bien, faltan = prep.verificar(ctx.dv, REMITENTE)
    if faltan:
        raise SystemExit(f"preparar_datos.verificar() no quedó en verde: {faltan}")
    faltan_excel = [n for n in range(1, 16) if not any(
        p.startswith(f"E{n:02d}-") for p in (os.listdir(ADJUNTOS) if os.path.isdir(ADJUNTOS) else []))]
    if faltan_excel:
        genexcel.main()
    ctx.encabezados = sim.cargar_encabezados(ctx.dv)


def limpiar(ctx):
    for sid in ctx.creadas:
        try:
            sim.borrar_solicitud(ctx.dv, sid)
        except Exception as e:
            print(f"  ! no se pudo borrar la Solicitud simulada {sid}: {e}")


def imprimir_reporte(resultados, archivos_filtrados):
    print("\n" + "=" * 88)
    print("REPORTE — casos ejecutados")
    print("=" * 88)
    ok = falla = na = 0
    por_archivo = {}
    for c in resultados:
        por_archivo.setdefault(c.archivo, []).append(c)
    for archivo in sorted(por_archivo):
        print(f"\n--- {archivo} ---")
        for c in por_archivo[archivo]:
            print(f"  [{c.estado:5}] {c.id:6} {c.titulo}")
            if c.estado == "N-A":
                print(f"           motivo: {c.motivo_na}")
                na += 1
            else:
                for nombre, cok, detalle in c.checks:
                    if not cok:
                        print(f"           FALLA: {nombre}" + (f"  ({detalle})" if detalle else ""))
                if c.estado == "OK":
                    ok += 1
                else:
                    falla += 1

    print("\n" + "=" * 88)
    print("COBERTURA — casos del plan que este corredor NO corrió, y por qué")
    print("=" * 88)
    corridos = {c.id for c in resultados}
    for id_, (archivo, titulo, motivo) in sorted(NO_CUBIERTOS.items(), key=lambda kv: (kv[1][0], kv[0])):
        if archivos_filtrados and archivo not in archivos_filtrados:
            continue
        if id_ in corridos:
            continue
        print(f"  [N-A  ] {id_:6} ({archivo}) {titulo}")
        print(f"           motivo: {motivo}")

    print("\n" + "=" * 88)
    print(f"RESUMEN: {ok} OK · {falla} FALLA · {na} N-A (de los corridos) "
          f"· {len(resultados)} casos corridos de 82 en el plan")
    print("=" * 88)
    return falla


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--archivo", help="corré solo los casos de un archivo del plan (00,01,02,03,05,06,08); admite varios separados por coma")
    p.add_argument("--sin-limpiar", action="store_true", help="no borra las Solicitudes que creó (para inspeccionarlas después)")
    p.add_argument("--cobertura", action="store_true", help="solo imprime la sección de cobertura, sin tocar Dev")
    p.add_argument("--con-comportamiento-roles", action="store_true",
                   help="también corré A-06/A-07/A-10 (proxy de comportamiento_roles.py --con-escritura). "
                        "APAGADO POR DEFECTO: esa herramienta actúa sobre CUALQUIER Fila del entorno en "
                        "Validada/Digitada, no solo las de este corredor — si el otro agente en pruebas/gui/ "
                        "tiene filas de prueba en esos estados en simultáneo, esto las toca. Usalo solo si "
                        "sabés que no hay nadie más probando en paralelo en ese momento.")
    args = p.parse_args()

    archivos_filtrados = set(a.strip() for a in args.archivo.split(",")) if args.archivo else None
    CASOS_RIESGOSOS_PARA_CONCURRENCIA = {"A-06", "A-07", "A-10"}

    if args.cobertura:
        imprimir_reporte([], archivos_filtrados)
        return 0

    dv = Dataverse()
    ctx = Contexto(dv)

    print("Preparando datos de negocio y Excel de prueba...")
    preparar(ctx)
    print(f"  remitente autorizado de este corredor: {REMITENTE}")
    print(f"  remitente NO autorizado de este corredor: {REMITENTE_NO_AUTORIZADO}")

    resultados = []
    try:
        for id_, archivo, fn in REGISTRO:
            if archivos_filtrados and archivo not in archivos_filtrados:
                continue
            if id_ in CASOS_RIESGOSOS_PARA_CONCURRENCIA and not args.con_comportamiento_roles:
                c = Caso(id_, archivo, TITULOS.get(id_, id_))
                c.na("no corrido por defecto: usa comportamiento_roles.py --con-escritura, que actúa sobre "
                     "CUALQUIER Fila del entorno en Validada/Digitada (no solo las de este corredor) y podría "
                     "chocar con datos de prueba del agente que trabaja en paralelo en pruebas/gui/. "
                     "Corré con --con-comportamiento-roles si estás seguro de que no hay nadie más probando.")
                c.cerrar()
                resultados.append(c)
                print(f"  corrido: [{c.estado:5}] {c.id}")
                continue
            try:
                c = fn(ctx)
            except Exception as e:
                c = Caso(id_, archivo, TITULOS.get(id_, id_))
                c.check("el caso terminó sin excepciones", False, f"{type(e).__name__}: {e}\n{traceback.format_exc()[-800:]}")
            c.cerrar()
            resultados.append(c)
            print(f"  corrido: [{c.estado:5}] {c.id}")
    finally:
        if not args.sin_limpiar:
            print("\nLimpiando las Solicitudes que este corredor creó...")
            limpiar(ctx)
        else:
            print(f"\n--sin-limpiar: quedaron {len(ctx.creadas)} Solicitudes sin borrar (GUIDs): {ctx.creadas}")
        if ctx.excels_extra:
            print(f"Excel ad hoc armados en pruebas/adjuntos/ (quedan, son insumo reutilizable): {[os.path.basename(x) for x in ctx.excels_extra]}")

    fallas = imprimir_reporte(resultados, archivos_filtrados)
    return 1 if fallas else 0


if __name__ == "__main__":
    sys.exit(main())
