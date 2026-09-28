#!/usr/bin/env python3
"""Set S — Reglas de nivel Solicitud (`pruebas/02-reglas-de-solicitud.md`).

Las cinco reglas que miran **el sobre** del correo (si trae adjunto, si es
Excel, si es uno solo, si tiene la estructura de la plantilla, si trae filas)
más el presupuesto de tiempo (S-06) y el contenido del acuse (S-07). El
contrato completo de un módulo de set está en `sets/__init__.py`; la lógica de
preparación compartida, en `preparacion_comun.py`.

**Fase 2**: investigado el 2026-09-27 (DN-01, DN-02, DN-03 en
`diseno/DIRECTRICES-NUEVAS.md`) — **no se agrega ningún caso nuevo**. El
porqué, caso por candidato, está en la sección "Fase 2" de
`pruebas/02-reglas-de-solicitud.md` (el MD manda; acá solo se registra que la
conclusión ya se tomó, para quien lea el código sin abrir el MD).
"""
import os
import sys
import time

_AQUI = os.path.dirname(os.path.abspath(__file__))
_HERRAMIENTAS_PRUEBAS = os.path.dirname(_AQUI)
sys.path.insert(0, _HERRAMIENTAS_PRUEBAS)

import preparacion_comun as pc  # noqa: E402
import simulador as sim  # noqa: E402

LETRA = "S"
NOMBRE = "Reglas de Solicitud"

# Remitentes propios de este set. Nunca los de una persona real (CONVENCIONES
# §5): si alguna vez alguien corrió `preparar_datos.py --remitente su-correo@...`,
# este set no lo toca ni lo confunde con el suyo.
REMITENTE = "zz-corredor-fase1@example.com"

CUMPLIDA, NOCUMPLIDA, OMITIDA = 159460001, 159460002, 159460003
RECH, ENPROC = 159460004, 159460005


# ===========================================================================
# preparar / cerrar
# ===========================================================================
def preparar(ctx):
    """Deja listo TODO lo que el set S necesita, y nada más (CONVENCIONES §8):

    - **Datos de negocio** (fixture compartido `PR*`, create-if-missing, y el
      `REMITENTE` de este set autorizado sobre `PR11`) — antiguo P-03.
    - **Los 15 Excel + el adjunto que no es Excel** en `pruebas/adjuntos/` —
      antiguo P-04.
    - **`plantilla.estructura`** con la forma que `ESTRUCTURA_PLANTILLA` da
      por sentada (hoja `Datos`, encabezado fila 12, datos desde la 13) —
      antiguo P-06 (parcial).
    - **`lectura.limites`, CONTENIDO** (no solo que exista): las dos claves
      de tope de bytes (comprimido/descomprimido) que
      `sanic_mppp_capi_validarsolicitud` lee como obligatorias ANTES de
      evaluar ninguna regla de Solicitud (`ValidarSolicitud.cs`) — con una
      falta o un valor no numérico, la lectura del primer Excel real de
      CUALQUIER caso de este set revienta ahí. Recupera la cobertura que
      perdió el antiguo P-06 al disolver el set P (decisión del aprobador
      2026-09-28: la verificación viaja con el set que la necesita).
    - **`plantilla.listas`, CONTENIDO — banco BAC = código `007`** (no solo
      que exista): el código de 3 dígitos con el que se construye la
      referencia de formato 11. `sanic_mppp_capi_validarsolicitud` lee este
      parámetro como obligatorio en el mismo punto que `lectura.limites`, así
      que un JSON malformado también revienta la validación de CUALQUIER
      caso de este set antes de la primera regla. El contenido exacto
      (BAC=007) lo vuelve a verificar, además, el set **R** (caso R-10, que
      es quien realmente construye y compara la referencia) — acá alcanza
      con que la forma sea válida y el banco esté. Recupera la cobertura que
      perdió el antiguo P-06.

    **Lo que NO prepara, y por qué (para que no se lea como un olvido):**

    - **Roles de usuarios de prueba** (`systemuserroles_association`). Este
      set no usa ninguna sesión de navegador ni impersona a User1-4
      (CONVENCIONES §6): los siete casos llaman a las Custom API directo, como
      el propio service principal (`simulador.py`). El `REMITENTE` es una fila
      de la tabla `Autorizado` (un correo, texto plano) — no una identidad de
      Dataverse, no tiene rol que verificar. Si algún día un caso de este set
      necesitara impersonar un rol, se agrega la llamada a
      `preparacion_comun.verificar_roles()` acá.
    - **Los cinco flujos activos** (antiguo P-05). Ningún caso de este set
      dispara `MPPP-REC`/`MPPP-ING` de verdad: `simulador.py` llama a
      `sanic_mppp_capi_clasificarcorreo`/`sanic_mppp_capi_validarsolicitud`
      directo por Web API, sin pasar por el flujo (ver el docstring de
      `simulador.py`). Verificar que el flujo esté prendido no cambiaría el
      resultado de ningún caso de este archivo.
    - **El canon de configuración global** (§8.2). Lo aplica `correr.py` antes
      y después de CADA set (no cada set por separado): así es el mismo valor
      para todos y ningún set puede correr fuera de sincronía con otro.
    """
    pc.verificar_datos_negocio(ctx.dv, REMITENTE)
    pc.verificar_excels()
    ctx.extra["estructura"] = pc.verificar_estructura_plantilla(ctx.dv)
    ctx.extra["encabezados"] = sim.cargar_encabezados(ctx.dv)
    pc.verificar_lectura_limites(ctx.dv)
    pc.verificar_bac_codigo_007(ctx.dv)


def cerrar(ctx):
    """Borra, por GUID (nunca por nombre), las Solicitudes que este set creó.
    No toca el fixture compartido `PR*`: eso no se borra nunca desde un set
    (ver `preparar_datos.py`, sección "POR QUÉ NO --limpiar")."""
    return ctx.limpiar_solicitudes()


def _excel(nombre):
    return pc.excel(nombre)


# ===========================================================================
# Casos S-01 .. S-07
# ===========================================================================
def s01(ctx):
    c = pc.Caso("S-01", "El adjunto no es un Excel")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba S-01",
                                     cantidad_adjuntos=1, cantidad_excel=0)
    reglas = sim.leer_reglas(ctx.dv, sid)
    c.check("TRAE_ADJUNTO = Cumplida", reglas.get("TRAE_ADJUNTO", {}).get("sanic_resultado") == CUMPLIDA)
    c.check("ADJUNTO_ES_EXCEL = No cumplida", reglas.get("ADJUNTO_ES_EXCEL", {}).get("sanic_resultado") == NOCUMPLIDA)
    for cod in ("UN_SOLO_EXCEL", "ESTRUCTURA_PLANTILLA", "TIENE_FILAS"):
        c.check(f"{cod} = Omitida", reglas.get(cod, {}).get("sanic_resultado") == OMITIDA)
    s = sim.leer_solicitud(ctx.dv, sid)
    c.check("Estado = Rechazada", s.get("sanic_estadoprocesamiento") == RECH)
    c.check("acuse presente", bool(s.get("sanic_acusecontenido")))
    return c


def s02(ctx):
    c = pc.Caso("S-02", "Dos Excel en el mismo correo")
    # Dos Excel: ING no sube NINGUNO (Hay_exactamente_un_excel exige ==1). Se
    # simula igual: cantidad_excel=2 y no se pasa excel_a_subir.
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba S-02",
                                     cantidad_adjuntos=2, cantidad_excel=2)
    reglas = sim.leer_reglas(ctx.dv, sid)
    c.check("UN_SOLO_EXCEL = No cumplida", reglas.get("UN_SOLO_EXCEL", {}).get("sanic_resultado") == NOCUMPLIDA)
    for cod in ("ESTRUCTURA_PLANTILLA", "TIENE_FILAS"):
        c.check(f"{cod} = Omitida", reglas.get(cod, {}).get("sanic_resultado") == OMITIDA)
    s = sim.leer_solicitud(ctx.dv, sid)
    c.check("Estado = Rechazada", s.get("sanic_estadoprocesamiento") == RECH)
    return c


def s03(ctx):
    c = pc.Caso("S-03", "Un Excel y un archivo que no lo es")
    sid, mid, rc, rv = ctx.procesar(
        remitente=REMITENTE, asunto="Prueba S-03", cantidad_adjuntos=2, cantidad_excel=1,
        excel_a_subir=_excel("E01-feliz-formato11.xlsx"))
    reglas = sim.leer_reglas(ctx.dv, sid)
    for cod in ("TRAE_ADJUNTO", "ADJUNTO_ES_EXCEL", "UN_SOLO_EXCEL", "ESTRUCTURA_PLANTILLA", "TIENE_FILAS"):
        c.check(f"{cod} = Cumplida", reglas.get(cod, {}).get("sanic_resultado") == CUMPLIDA)
    s = sim.leer_solicitud(ctx.dv, sid)
    c.check("Estado = En proceso, 3 filas Validadas",
            s.get("sanic_estadoprocesamiento") == ENPROC and s.get("sanic_filasvalidas") == 3)
    return c


def s04(ctx):
    c = pc.Caso("S-04", "La hoja de datos tiene otro nombre")
    sid, mid, rc, rv = ctx.procesar(
        remitente=REMITENTE, asunto="Prueba S-04", cantidad_adjuntos=1, cantidad_excel=1,
        excel_a_subir=_excel("E12-hoja-renombrada.xlsx"))
    reglas = sim.leer_reglas(ctx.dv, sid)
    c.check("UN_SOLO_EXCEL = Cumplida", reglas.get("UN_SOLO_EXCEL", {}).get("sanic_resultado") == CUMPLIDA)
    c.check("ESTRUCTURA_PLANTILLA = No cumplida", reglas.get("ESTRUCTURA_PLANTILLA", {}).get("sanic_resultado") == NOCUMPLIDA)
    c.check("TIENE_FILAS = Omitida", reglas.get("TIENE_FILAS", {}).get("sanic_resultado") == OMITIDA)
    s = sim.leer_solicitud(ctx.dv, sid)
    c.check("Estado = Rechazada (regla fallida, no un Error de plataforma)",
            s.get("sanic_estadoprocesamiento") == RECH)
    return c


def s05(ctx):
    c = pc.Caso("S-05", "Excel con la estructura correcta pero sin filas")
    sid, mid, rc, rv = ctx.procesar(
        remitente=REMITENTE, asunto="Prueba S-05", cantidad_adjuntos=1, cantidad_excel=1,
        excel_a_subir=_excel("E11-sin-filas.xlsx"))
    reglas = sim.leer_reglas(ctx.dv, sid)
    c.check("ESTRUCTURA_PLANTILLA = Cumplida", reglas.get("ESTRUCTURA_PLANTILLA", {}).get("sanic_resultado") == CUMPLIDA)
    c.check("TIENE_FILAS = No cumplida", reglas.get("TIENE_FILAS", {}).get("sanic_resultado") == NOCUMPLIDA)
    s = sim.leer_solicitud(ctx.dv, sid)
    c.check("Estado = Rechazada, filas t/v/r = 0/0/0",
            s.get("sanic_estadoprocesamiento") == RECH and (s.get("sanic_filastotales") or 0) == 0)
    c.check("acuse presente", bool(s.get("sanic_acusecontenido")))
    return c


def s06(ctx):
    c = pc.Caso("S-06", "Volumen: 100 filas (presupuesto < 10 s)")
    sid, _ = ctx.solicitud(remitente=REMITENTE, asunto="Prueba S-06 volumen 100",
                            cantidad_adjuntos=1, cantidad_excel=1, excel_a_subir=_excel("E14-volumen-100.xlsx"))
    sim.clasificar(ctx.dv, sid)
    t0 = time.monotonic()
    rv = sim.validar(ctx.dv, sid)
    segundos = time.monotonic() - t0
    c.check("filas t/v/r = 100/100/0",
            (rv.get("filastotales"), rv.get("filasvalidas"), rv.get("filasrechazadas")) == (100, 100, 0), str(rv))
    # Mide SOLO la llamada a la Custom API (sin el resto del flujo, que acá no
    # corre): es una medida más estricta que la del caso original, no más
    # floja. Si esto ya tarda cerca de 10s, el presupuesto del flujo completo
    # está en riesgo.
    c.check(f"la llamada a {sim.VALIDAR_SOLICITUD} tardó menos de 10 s (tardó {segundos:.2f}s)", segundos < 10.0)
    return c


def s07(ctx):
    c = pc.Caso("S-07", "El acuse de una Rechazada dice lo que tiene que decir")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba S-07",
                                     cantidad_adjuntos=0, cantidad_excel=0)
    s = sim.leer_solicitud(ctx.dv, sid)
    texto = s.get("sanic_acusecontenido") or ""
    numero = s.get("sanic_nombre") or ""
    c.check("contiene el número de solicitud", numero in texto, numero)
    c.check("dice 'no hay nada que procesar' o equivalente",
            "no hay nada" in texto.lower() or "nada que procesar" in texto.lower())
    c.check("dice que no va a recibir otro correo por esta solicitud",
            "no va a recibir" in texto.lower() or "no recibirá" in texto.lower() or "no recibira" in texto.lower())
    c.check("pide corregir y reenviar",
            "reenv" in texto.lower() or "corrija" in texto.lower() or "correo nuevo" in texto.lower())
    c.check("trae el texto obligatorio de 'no responda a este mensaje'", "no responda" in texto.lower())
    for tecnico in ("TRAE_ADJUNTO", "ADJUNTO_ES_EXCEL", "UN_SOLO_EXCEL", "ESTRUCTURA_PLANTILLA", "TIENE_FILAS"):
        c.check(f"no menciona el nombre técnico de la regla '{tecnico}'", tecnico not in texto)
    return c


CASOS = [s01, s02, s03, s04, s05, s06, s07]
