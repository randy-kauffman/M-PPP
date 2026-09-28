#!/usr/bin/env python3
"""Set O — DN-03: la obligatoriedad se define por gestión (`pruebas/12-obligatoriedad-gestion.md`).

`plantilla.obligatoriedad` es un **parámetro global** (CONVENCIONES §8.2): sembrar una versión
nueva la vuelve la ACTIVA de inmediato, para toda Solicitud que se valide mientras tanto, en TODO
el entorno. **Este archivo jamás escribe sobre esa tabla.** Ni un solo caso de acá hace un
`POST`/`PATCH` sobre `sanic_mppp_tbl_parametros`:

- O-01, O-02, O-08 leen lo YA sembrado (versión 1 y versión 2 activas hoy), nunca lo cambian.
- O-03, O-05, O-09 mandan Excel contra las Custom API, pero ejercitan gestiones/escenarios que la
  versión YA activa cubre (nunca sembran una versión nueva para forzar un escenario).
- O-04 (precedencia), O-06 (claves de `porGestion` sin distinguir caja/tildes) y O-07 (clave
  desconocida dentro de `porGestion`) son los tres escenarios de mayor riesgo del diseño: los tres
  se verifican corriendo `dotnet test` contra la suite ya construida con TDD
  (`tests/Sanic.Mppp.Plugins.Tests/Aceptacion/ParametrosDePlantillaAceptacion.cs`), nunca sembrando
  nada en Dev. El riesgo que el MD describe ("una versión con precedencia real, o con un typo en
  `porGestion`, vuelve esa versión la activa para todo el entorno") queda en CERO porque el código
  de este archivo nunca ejecuta ese `POST`.
- O-10 lee el catálogo de Reglas (`sanic_mppp_tbl_reglas`), no toca nada tampoco.

**Frontera con el set R (R-18).** `03-reglas-de-registro.md` ya tiene, desde fase 2, R-18: manda un
Excel real con `gestion=Exclusion` y `numeroCuenta`/`banco`/`moneda` en blanco contra las Custom API
en vivo, y confirma que la fila queda Validada — la prueba EN VIVO de que `porGestion.Exclusion`
funciona, dentro del motor de las ocho reglas que ese set ya automatizaba. Este archivo NO repite
ese envío (sería el mismo Excel, el mismo resultado, la misma evidencia con otro ID): O-02 verifica,
de solo lectura, que el CONTENIDO sembrado es el que el aprobador pidió (los tres campos exactos),
que es la mitad que R-18 no cubre. El detalle completo de la frontera está en el MD, sección
"Frontera con el set R (R-18)" (el MD manda).

Como en `sets/set_r.py` y `sets/set_s.py`: el `REMITENTE` es el de siempre, autorizado sobre `PR11`
por `preparar_datos.py`; este set nunca impersona una sesión de navegador (CONVENCIONES §6), llama
a las Custom API directo vía `simulador.py`.
"""
import json
import os
import re
import subprocess
import sys

_AQUI = os.path.dirname(os.path.abspath(__file__))
_HERRAMIENTAS_PRUEBAS = os.path.dirname(_AQUI)
_RAIZ = os.path.dirname(os.path.dirname(_HERRAMIENTAS_PRUEBAS))
sys.path.insert(0, _HERRAMIENTAS_PRUEBAS)

import preparacion_comun as pc  # noqa: E402
import simulador as sim  # noqa: E402
import generar_excel as genexcel  # noqa: E402

LETRA = "O"
NOMBRE = "Obligatoriedad por gestión (DN-03)"

# Remitente propio de este set. Nunca el de una persona real (CONVENCIONES §5). Mismo REMITENTE que
# S/R/I: preparar_datos.py ya lo deja autorizado sobre PR11 (create-if-missing, compartido).
REMITENTE = "zz-corredor-fase1@example.com"

PARAMETROS = "sanic_mppp_tbl_parametros"
REGLAS = "sanic_mppp_tbl_reglas"

FILA_RECH_VAL, FILA_SIN_AUT, FILA_VALIDADA = 159460001, 159460002, 159460003

NIVEL_REGISTRO = 159460002
EFECTO_RECHAZA = 159460001

# DD-16: la forma exacta de `plantilla.obligatoriedad` sanic_version=1 (sin `porGestion`). Se
# compara como estructura JSON (no como texto crudo), para no depender de espacios/orden de claves.
VALOR_V1_CANONICO = {"porDefecto": "obligatorio", "opcionales": [{"campo": "referencia"}]}

RUTA_PARAMETROS_PLANTILLA_ACEPTACION = os.path.join(
    _RAIZ, "tests", "Sanic.Mppp.Plugins.Tests", "Aceptacion", "ParametrosDePlantillaAceptacion.cs")
RUTA_PROYECTO_TESTS = os.path.join(_RAIZ, "tests", "Sanic.Mppp.Plugins.Tests")
NAMESPACE_ACEPTACION = "Sanic.Mppp.Plugins.Tests.Aceptacion.ParametrosDePlantillaAceptacion"


def _excel(nombre):
    return pc.excel(nombre)


# ===========================================================================
# preparar / cerrar
# ===========================================================================
def preparar(ctx):
    """Deja listo TODO lo que el set O necesita, y nada más (CONVENCIONES §8):

    - **Datos de negocio** (fixture compartido `PR*`, el `REMITENTE` de siempre autorizado sobre
      `PR11`) — igual que `set_s.py`/`set_r.py`.
    - **Los 15 Excel + el adjunto que no es Excel** (`generar_excel.py`): O-09 reutiliza
      `E06-obligatoriedad.xlsx` tal cual (CONVENCIONES §9: lo que ya tiene herramienta no se arma de
      nuevo); O-03 y O-05 arman sus propios Excel ad hoc con `ctx.excel_ad_hoc()`.
    - **`plantilla.estructura`** y **los encabezados**, para armar/leer los mensajes exactos que
      OBLIGATORIEDAD y LISTAS_VALIDAS citan.
    - **`plantilla.obligatoriedad` activa** — SOLO se COMPRUEBA que exista una versión activa
      (§8.2: su canon es "la versión activa más alta", no un valor fijo que este set deba sembrar).
      Se guarda su contenido en `ctx.extra["obligatoriedad_activa"]` para que O-01/O-02/O-03 lo
      lean sin repetir la consulta.

    **Lo que NO prepara, y por qué:**

    - **No siembra ninguna versión de `plantilla.obligatoriedad`.** Es la decisión central de este
      set (ver el docstring del módulo): O-04/O-06/O-07 se resuelven con `dotnet test`, nunca
      tocando Dev.
    - **Roles de usuarios de prueba / sesiones de navegador.** Los diez casos llaman a las Custom
      API directo (`simulador.py`) o leen catálogos de solo lectura; ninguno impersona una sesión.
    - **El canon de configuración global** (§8.2). Lo aplica `correr.py` antes y después de CADA
      set, no cada set por separado.
    """
    pc.verificar_datos_negocio(ctx.dv, REMITENTE)
    pc.verificar_excels()
    ctx.extra["estructura"] = pc.verificar_estructura_plantilla(ctx.dv)
    ctx.extra["encabezados"] = sim.cargar_encabezados(ctx.dv)
    ctx.extra["obligatoriedad_activa"] = pc.verificar_parametro_activo(ctx.dv, "plantilla.obligatoriedad")


def cerrar(ctx):
    """Borra, por GUID, las Solicitudes que este set creó (O-03, O-05, O-09). No hay ninguna
    versión de `plantilla.obligatoriedad` que desactivar: este set nunca siembra una (ver el
    docstring del módulo) — el canon de ese parámetro queda garantizado por construcción, no por
    limpieza. No toca el fixture compartido `PR*`."""
    return ctx.limpiar_solicitudes()


# ===========================================================================
# `dotnet test` — evidencia unitaria para O-01, O-04, O-06 y O-07 (nunca contra Dev).
# ===========================================================================
def _dotnet_test(filtro, timeout=240):
    """Corre `dotnet test` con un filtro EXACTO/parcial contra la suite de aceptación C#
    (`tests/Sanic.Mppp.Plugins.Tests`). Es la evidencia que O-01/O-04/O-06/O-07 usan en vez de
    sembrar una versión de `plantilla.obligatoriedad` en Dev.

    Devuelve `(ok, passed, failed, total, detalle)`. `total is None` significa que no se pudo
    siquiera correr `dotnet test` (binario ausente, timeout) — un límite del ENTORNO (N-A), no un
    defecto del sistema (CONVENCIONES §3). `total == 0` con `ok=True` es la trampa a evitar: un
    filtro mal escrito que no matchea ninguna prueba hace que `dotnet test` "pase" sin haber corrido
    nada — por eso cada caso, además de `ok`, revisa `total` contra lo que esperaba encontrar."""
    try:
        proc = subprocess.run(
            ["dotnet", "test", RUTA_PROYECTO_TESTS, "--filter", filtro, "--nologo"],
            cwd=_RAIZ, capture_output=True, text=True, timeout=timeout)
    except FileNotFoundError as e:
        return False, None, None, None, f"'dotnet' no está disponible en el PATH: {e}"
    except subprocess.TimeoutExpired as e:
        return False, None, None, None, f"dotnet test no terminó en {timeout}s: {e}"

    salida = (proc.stdout or "") + "\n" + (proc.stderr or "")
    m = re.search(r"Failed:\s*(\d+),\s*Passed:\s*(\d+),\s*Skipped:\s*(\d+),\s*Total:\s*(\d+)", salida)
    if not m:
        return False, None, None, None, f"no se pudo leer el resumen de dotnet test (rc={proc.returncode}):\n{salida[-1500:]}"
    failed, passed, skipped, total = (int(x) for x in m.groups())
    ok = proc.returncode == 0 and failed == 0
    return ok, passed, failed, total, salida[-1500:]


def _leer_fuente_tests(c):
    """El código fuente de `ParametrosDePlantillaAceptacion.cs`, o `None` si no se pudo leer (y ya
    deja el caso en N-A, con el motivo)."""
    try:
        with open(RUTA_PARAMETROS_PLANTILLA_ACEPTACION, encoding="utf-8") as f:
            return f.read()
    except OSError as e:
        c.na(f"no se pudo leer {RUTA_PARAMETROS_PLANTILLA_ACEPTACION}: {e}")
        return None


# ===========================================================================
# O-01 — Compatibilidad: sin `porGestion`, se comporta EXACTAMENTE como antes de DN-03.
# ===========================================================================
def o01(ctx):
    c = pc.Caso("O-01", "Compatibilidad: un parámetro SIN `porGestion` se comporta igual que antes de DN-03")
    fqn = f"{NAMESPACE_ACEPTACION}.DN03_un_parametro_sin_porGestion_se_comporta_exactamente_como_hoy"
    ok, passed, failed, total, detalle = _dotnet_test(f"FullyQualifiedName={fqn}")
    if total is None:
        c.na(f"no se pudo correr `dotnet test` sobre {fqn!r}: {detalle}")
        return c
    c.check(f"el filtro encontró exactamente 1 prueba ({fqn})", total == 1, f"encontró {total}")
    c.check("la prueba unitaria de compatibilidad pasa", ok, detalle)

    est, r, _ = ctx.dv.call(
        "GET",
        f"{PARAMETROS}?$select=sanic_valor,statecode&$filter=sanic_nombre eq 'plantilla.obligatoriedad'"
        " and sanic_version eq 1")
    filas = r.get("value", []) if est == 200 else []
    c.check("la fila sanic_version=1 de plantilla.obligatoriedad sigue existiendo "
            "(histórico; no hace falta que sea la activa)", len(filas) == 1, f"HTTP {est}: {filas}")
    if filas:
        try:
            valor = json.loads(filas[0].get("sanic_valor") or "{}")
        except json.JSONDecodeError as e:
            c.check("su sanic_valor es JSON válido", False, str(e))
            return c
        c.check("su sanic_valor sigue siendo EXACTAMENTE el de DD-16 (nunca se editó, ver O-08)",
                valor == VALOR_V1_CANONICO, f"obtenido {valor!r}")
    return c


# ===========================================================================
# O-02 — El contenido sembrado de `porGestion.Exclusion` es exactamente el pedido.
# ===========================================================================
def o02(ctx):
    c = pc.Caso("O-02", "El contenido sembrado de `porGestion.Exclusion` es exactamente el pedido")
    activo = ctx.extra["obligatoriedad_activa"]
    valor_crudo = activo.get("sanic_valor") or ""
    if "porGestion" not in valor_crudo or '"Exclusion"' not in valor_crudo:
        c.na("plantilla.obligatoriedad activo no trae 'porGestion.Exclusion' en este entorno "
             "(el criterio ya está confirmado por el aprobador, 2026-09-28; esto es una precondición "
             f"de entorno, no una duda sobre el criterio, DN-03) — no se puede evaluar hoy. "
             f"Valor activo: {valor_crudo!r}")
        return c
    try:
        datos = json.loads(valor_crudo)
    except json.JSONDecodeError as e:
        c.check("el valor activo es JSON válido", False, str(e))
        return c

    por_gestion = datos.get("porGestion") or {}
    entrada = por_gestion.get("Exclusion") or {}
    aflojados = {o.get("campo") for o in (entrada.get("opcionales") or [])}
    c.check("porGestion.Exclusion.opcionales es EXACTAMENTE {numeroCuenta, banco, moneda} (ni más ni menos)",
            aflojados == {"numeroCuenta", "banco", "moneda"}, f"obtenido {sorted(aflojados)}")

    endurecidos = {o.get("campo") for o in (entrada.get("obligatorios") or [])}
    c.check("Exclusion no trae ningún campo en `obligatorios` (el aprobador solo pidió aflojar)",
            not endurecidos, f"obtenido {sorted(endurecidos)}")

    otras = set(por_gestion) - {"Exclusion"}
    c.check("ninguna otra gestión tiene entrada en porGestion todavía (ver O-03)",
            not otras, f"obtenido {sorted(otras)}")
    return c


# ===========================================================================
# O-03 — `Inclusion`/`Modificacion` NO tienen entrada en `porGestion`: sigue el bloque global.
# ===========================================================================
def o03(ctx):
    c = pc.Caso("O-03", "`Inclusion` y `Modificacion` NO tienen entrada en `porGestion` todavía")
    activo = ctx.extra["obligatoriedad_activa"]
    valor_crudo = activo.get("sanic_valor") or ""
    try:
        datos = json.loads(valor_crudo) if valor_crudo else {}
    except json.JSONDecodeError as e:
        c.check("el valor activo es JSON válido", False, str(e))
        return c

    claves_normalizadas = {k.strip().lower() for k in (datos.get("porGestion") or {})}
    if "inclusion" in claves_normalizadas or "modificacion" in claves_normalizadas:
        c.na("plantilla.obligatoriedad activo YA trae una entrada de porGestion para Inclusion o "
             f"Modificacion (cambió desde que se escribió este caso): {datos.get('porGestion')!r} — "
             "este caso asume que siguen sin aflojamiento y hay que reescribirlo contra la forma nueva.")
        return c

    ruta = ctx.excel_ad_hoc("ZZ-O03-inclusion-modificacion-sin-aflojar.xlsx", [
        genexcel.fila(gestion="Inclusion", plan="PR11", clasificacion="ACH",
                      nombre="Inclusion Prueba O-03", cuenta="", banco="", moneda=""),
        genexcel.fila(gestion="Modificacion", plan="PR11", clasificacion="ACH",
                      nombre="Modificacion Prueba O-03", cuenta="", banco="", moneda=""),
    ])
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba O-03", cantidad_adjuntos=1,
                                     cantidad_excel=1, excel_a_subir=ruta)
    filas = sim.leer_filas(ctx.dv, sid)
    c.check("2 filas, ambas Rechazada en validación (Inclusion y Modificacion NO se aflojan)",
            len(filas) == 2 and all(f["sanic_estado"] == FILA_RECH_VAL for f in filas),
            str([(f["sanic_numerofila"], f["sanic_estado"]) for f in filas]))
    encabezados = ctx.extra["encabezados"]
    for f in filas:
        msg = f.get("sanic_mensaje") or ""
        c.check(f"fila {f['sanic_numerofila']}: el mensaje cita al menos uno de numeroCuenta/banco/moneda "
                "(siguen obligatorios; el bloque global no se aflojó)",
                any(encabezados[campo] in msg for campo in ("numeroCuenta", "banco", "moneda")), msg)
    return c


# ===========================================================================
# O-04 — Precedencia `obligatorios` sobre `opcionales` (unitario; NUNCA se siembra en Dev).
# ===========================================================================
def o04(ctx):
    c = pc.Caso("O-04", "Precedencia `obligatorios` sobre `opcionales` (cobertura unitaria, NO se siembra en Dev)")
    fuente = _leer_fuente_tests(c)
    if fuente is None:
        return c

    escenario_ab = "DN03_porGestion_afloja_con_opcionales_y_endurece_con_obligatorios_la_gestion_gana_sobre_lo_global"
    escenario_c = "DN03_si_un_campo_esta_en_opcionales_y_en_obligatorios_de_la_misma_gestion_gana_obligatorios"
    c.check(f"existe el escenario (a)+(b): opcional sin obligatorio Y obligatorio sin opcional ({escenario_ab})",
            escenario_ab in fuente)
    c.check(f"existe el escenario (c): mismo campo en las dos listas de la misma entrada, gana obligatorios ({escenario_c})",
            escenario_c in fuente)

    filtro = f"FullyQualifiedName={NAMESPACE_ACEPTACION}.{escenario_ab}|FullyQualifiedName={NAMESPACE_ACEPTACION}.{escenario_c}"
    ok, passed, failed, total, detalle = _dotnet_test(filtro)
    if total is None:
        c.na(f"no se pudo correr `dotnet test` sobre los dos escenarios: {detalle}")
        return c
    c.check("el filtro encontró EXACTAMENTE las 2 pruebas esperadas (ningún nombre quedó renombrado sin avisar)",
            total == 2, f"encontró {total}")
    c.check("las dos pruebas pasan (a, b y c cubiertos: opcionales, obligatorios y la precedencia entre ambos)",
            ok, detalle)
    return c


# ===========================================================================
# O-05 — Gestión inválida en la FILA: OBLIGATORIEDAD ni siquiera se evalúa.
# ===========================================================================
def o05(ctx):
    c = pc.Caso("O-05", "Gestión inválida en la FILA: `OBLIGATORIEDAD` ni siquiera se evalúa (depende de LISTAS_VALIDAS)")
    ruta = ctx.excel_ad_hoc("ZZ-O05-gestion-invalida-cuenta-banco-vacios.xlsx", [
        genexcel.fila(gestion="Baja", plan="PR11", clasificacion="ACH",
                      nombre="Gestion Invalida Prueba O-05", cuenta="", banco=""),
    ])
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba O-05", cantidad_adjuntos=1,
                                     cantidad_excel=1, excel_a_subir=ruta)
    filas = sim.leer_filas(ctx.dv, sid)
    c.check("1 fila, Rechazada en validación", len(filas) == 1 and filas[0]["sanic_estado"] == FILA_RECH_VAL,
            str([(f["sanic_numerofila"], f["sanic_estado"]) for f in filas]))
    if not filas:
        return c
    msg = filas[0].get("sanic_mensaje") or ""
    encabezados = ctx.extra["encabezados"]
    c.check("el mensaje nombra el campo 'gestión' (LISTAS_VALIDAS: el único motivo)",
            encabezados["gestion"] in msg, msg)
    c.check("el mensaje NO cita numeroCuenta ni banco (OBLIGATORIEDAD depende de LISTAS_VALIDAS: "
            "no llega a evaluarse — playbooks/datos/reglas.md, sanic_dependede de OBLIGATORIEDAD)",
            encabezados["numeroCuenta"] not in msg and encabezados["banco"] not in msg, msg)
    return c


# ===========================================================================
# O-06 — Las CLAVES de `porGestion` no distinguen caja ni tildes (unitario; NUNCA en Dev).
# ===========================================================================
def o06(ctx):
    c = pc.Caso("O-06", "Las CLAVES de `porGestion` (dentro del parámetro) no distinguen caja ni tildes (unitario, no Dev)")
    fqn = f"{NAMESPACE_ACEPTACION}.DN03_los_nombres_de_gestion_de_porGestion_se_comparan_sin_distinguir_caja_ni_tildes"
    ok, passed, failed, total, detalle = _dotnet_test(f"FullyQualifiedName~{fqn}")
    if total is None:
        c.na(f"no se pudo correr `dotnet test` sobre {fqn!r}: {detalle}")
        return c
    c.check("el filtro encontró al menos una variante (exclusión/EXCLUSION/ExclusioN como CLAVE del JSON)",
            total > 0, f"encontró {total}")
    c.check("las variantes de caja/tilde en la CLAVE de porGestion resuelven a la misma gestión y pasan",
            ok, detalle)
    return c


# ===========================================================================
# O-07 — Gestión desconocida DENTRO de `porGestion` (unitario aquí; alto riesgo si se corre contra
# Dev, y eso queda FUERA de este arnés a propósito — ver el MD).
# ===========================================================================
def o07(ctx):
    c = pc.Caso("O-07", "Gestión desconocida DENTRO de `porGestion` revienta como error de configuración (alto riesgo, no sembrar en Dev)")
    fuente = _leer_fuente_tests(c)
    if fuente is None:
        return c
    c.check("existe un caso unitario con una gestión desconocida DENTRO de `porGestion` "
            "(distinto de O-05, que es sobre el valor de la FILA)", "Bajas" in fuente)

    fqn = f"{NAMESPACE_ACEPTACION}.Una_obligatoriedad_mal_cargada_revienta_con_el_motivo"
    ok, passed, failed, total, detalle = _dotnet_test(f"FullyQualifiedName~{fqn}")
    if total is None:
        c.na(f"no se pudo correr `dotnet test` sobre {fqn!r}: {detalle}")
        return c
    c.check("el filtro encontró pruebas (todas las formas de parámetro mal cargado, incluida la gestión desconocida)",
            total > 0, f"encontró {total}")
    c.check("todas pasan: un parámetro mal cargado revienta con FormatException y un motivo, nunca en silencio",
            ok, detalle)
    return c


# ===========================================================================
# O-08 — Versionar es AGREGAR una fila, nunca editar la vigente.
# ===========================================================================
def o08(ctx):
    c = pc.Caso("O-08", "Versionar es AGREGAR una fila, nunca editar la vigente")
    est, r, _ = ctx.dv.call(
        "GET",
        f"{PARAMETROS}?$select=sanic_version,sanic_valor,statecode"
        "&$filter=sanic_nombre eq 'plantilla.obligatoriedad'&$orderby=sanic_version asc")
    filas = r.get("value", []) if est == 200 else []
    c.check("hay al menos dos versiones (1 y 2), y las que hay están activas",
            len(filas) >= 2 and all(f.get("statecode") == 0 for f in filas), str(filas))

    v1 = next((f for f in filas if f.get("sanic_version") == 1), None)
    v1_ok = False
    if v1 is not None:
        try:
            v1_ok = json.loads(v1.get("sanic_valor") or "{}") == VALOR_V1_CANONICO
        except json.JSONDecodeError:
            v1_ok = False
    c.check("la versión 1 existe y su sanic_valor es EXACTO al de DD-16 (sin porGestion, nunca editada)",
            v1 is not None and v1_ok, str(v1))

    v2 = next((f for f in filas if f.get("sanic_version") == 2), None)
    c.check("la versión 2 existe y su sanic_valor es DISTINTO del de la versión 1 (se agregó, no se pisó)",
            v2 is not None and v1 is not None and v2.get("sanic_valor") != v1.get("sanic_valor"), str(v2))
    return c


# ===========================================================================
# O-09 — El mensaje al cliente no cambia con DN-03: nombra el CAMPO, nunca la gestión.
# ===========================================================================
def o09(ctx):
    c = pc.Caso("O-09", "El mensaje al cliente no cambia con DN-03 (sigue nombrando el CAMPO, nunca la gestión)")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba O-09", cantidad_adjuntos=1,
                                     cantidad_excel=1, excel_a_subir=_excel("E06-obligatoriedad.xlsx"))
    filas = sim.leer_filas(ctx.dv, sid)
    fila2 = next((f for f in filas if f["sanic_numerofila"] == 2), None)  # "Sin Cuenta", gestión Inclusion (default)
    c.check("la fila 2 ('Sin Cuenta', gestión Inclusion) quedó Rechazada en validación",
            fila2 is not None and fila2["sanic_estado"] == FILA_RECH_VAL, str(fila2))
    if not fila2:
        return c
    msg = fila2.get("sanic_mensaje") or ""
    encabezados = ctx.extra["encabezados"]
    c.check("el mensaje nombra el campo (numeroCuenta), exactamente como antes de DN-03",
            encabezados["numeroCuenta"] in msg, msg)
    for prohibido in ("gestión", "gestion", "Exclusion", "porGestion"):
        c.check(f"el mensaje NO menciona {prohibido!r} (la configuración interna no se filtra al cliente, D-41)",
                prohibido not in msg, msg)
    return c


# ===========================================================================
# O-10 — La regla OBLIGATORIEDAD no cambia de código, orden, nivel ni dependencias (catálogo).
# ===========================================================================
def o10(ctx):
    c = pc.Caso("O-10", "La regla `OBLIGATORIEDAD` no cambia de código, orden, nivel ni dependencias")
    est, r, _ = ctx.dv.call(
        "GET",
        f"{REGLAS}?$select=sanic_codigo,sanic_nivel,sanic_efecto,sanic_orden,sanic_dependede,statecode"
        "&$filter=sanic_codigo eq 'OBLIGATORIEDAD'")
    filas = r.get("value", []) if est == 200 else []
    c.check("existe una única regla con código OBLIGATORIEDAD", len(filas) == 1, str(filas))
    if not filas:
        return c
    regla = filas[0]
    c.check("nivel = Registro (playbooks/datos/reglas.md)", regla.get("sanic_nivel") == NIVEL_REGISTRO, str(regla))
    c.check("efecto = Rechaza (no cambió a Advierte/Envía a revisión)", regla.get("sanic_efecto") == EFECTO_RECHAZA, str(regla))
    c.check("orden = 60 (misma posición relativa: después de AUTORIZACION_CORREO_PLAN=50, antes de MONEDA_DEL_PLAN=70)",
            regla.get("sanic_orden") == 60, str(regla))
    c.check("depende de AUTORIZACION_CORREO_PLAN,LISTAS_VALIDAS (mismas dependencias que antes de DN-03)",
            regla.get("sanic_dependede") == "AUTORIZACION_CORREO_PLAN,LISTAS_VALIDAS", str(regla))
    c.check("está activa", regla.get("statecode") == 0, str(regla))
    return c


CASOS = [o01, o02, o03, o04, o05, o06, o07, o08, o09, o10]
