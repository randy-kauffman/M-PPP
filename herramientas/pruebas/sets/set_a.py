#!/usr/bin/env python3
"""Set A — La app y sus comandos de la barra (`pruebas/05-app-y-comandos.md`).

Los 5 comandos de Fila (`digitada`, `aprobar`, `rechazadaAs400`, `anular`, `devolver`), los 3 de
Solicitud (`atendido`, `descartar`, `revisado`) y el nuevo de fase 2 (`atendida`, sobre Fila) —
`recursos/js/comandos.js`. `descargarParaDigitar` (DN-02) tiene dueño propio (set E, E-01): no se
duplica acá. El contrato completo de un módulo de set está en `sets/__init__.py`; la preparación
compartida, en `preparacion_comun.py`.

**La lección que separa este set en dos mitades.** Un botón hace dos cosas: DIBUJARSE (solo lo ve
un navegador) y producir un EFECTO en el servidor (una fila cambia de estado, se registra un
motivo, se libera un claim). El 2026-09-27 el botón "Descargar para digitar" existía en el
servidor —verificado por API, tres caminos distintos— y no se dibujaba: la causa era la metadata
compilada del ribbon. Ninguna prueba de este archivo hubiera visto eso, y ninguna prueba de
`pruebas/gui/` puede ver el EFECTO tan barato como una llamada directa al Web API. Este módulo
prueba SOLO el efecto; lo que dibuja el navegador se reporta `N-A` citando el comando exacto del
arnés de `pruebas/gui/` (CONVENCIONES §8.2 no aplica acá — esto es un límite de herramienta, no de
configuración).

**Por qué esto NO usa `comportamiento_roles.py --con-escritura`.** Ese script actúa sobre
CUALQUIER fila en Validada que encuentre en el entorno, sin filtrar por dueño. Con varios agentes
corriendo sets en paralelo sobre el mismo Dev, eso puede pisar el trabajo de otro. En su lugar, este
módulo repite el patrón que ya establecieron `set_i.py` (I-13) y `set_b.py` (B-07/B-12/B-18/B-20):
impersonar `User1`/`User2` con `MSCRMCallerID` directo sobre `ctx.dv`, actuando solo sobre
Solicitudes/Filas que el propio caso creó con `ctx.procesar()`/`ctx.solicitud()`.

**Dos hallazgos de esta migración, con evidencia, documentados también en el MD:**

1. **El origen de Bitácora nunca es "App".** La versión vieja de A-03 esperaba
   `sanic_origen = App` para "Fila digitada". Leyendo `Steps/StepsDeFila.cs` (las cuatro llamadas a
   `RegistrarEvento` de ese archivo) todas usan `OrigenDelEvento.Plugin`; `OrigenDelEvento.App`
   está definido en el choice pero ningún step lo asigna (`grep -rn "OrigenDelEvento.App" src/` no
   devuelve nada). Corregido acá y en el MD.
2. **A-14 (nuevo) puede estar destapando un defecto real, no confirmado todavía.**
   `diseno/04-matriz-privilegios.md` dice que `sanic_requiereatencionmanual` "entra a la lista
   blanca" de Fila desde fase 2; `Steps/ReglasDeSteps.cs.PermitidasEnFila` NO la incluye (solo
   `sanic_estado`, `sanic_mensaje`, `sanic_notainterna`). A-14 prueba el comportamiento DISEÑADO
   (el botón "Atendida" funciona); si da FALLA con "sanic_requiereatencionmanual no está(n)
   permitida(s)", es exactamente esa discrepancia — ver el MD, sección "Otro hallazgo".

**A-15 (fusionado el 2026-09-28, `CONVENCIONES.md` §4, "La regla del nombre").** El ex set H
(`13-construido-hoy.md`, nombrado por la fecha en que se escribió, no por lo que prueba) se repartió
entre este set y el set G. Su caso `H-06` — el más caro del plan: documenta los dos días de
diagnóstico del botón fantasma de `descargarParaDigitar` — es sobre un COMANDO de la barra, así que
pasó acá como `A-15`, mismo cuerpo, `N-A` a propósito por el mismo motivo (ver el caso). Los otros
seis casos de H (sitemap/vistas/formulario) pasaron a `G-14`..`G-19` en `set_g.py`; `set_h.py`
desapareció. La tabla de equivalencias completa vive en `pruebas/README.md`.
"""
import json
import os
import re
import subprocess
import sys

_AQUI = os.path.dirname(os.path.abspath(__file__))
_HERRAMIENTAS_PRUEBAS = os.path.dirname(_AQUI)
_RAIZ = os.path.dirname(os.path.dirname(_HERRAMIENTAS_PRUEBAS))
_PRUEBAS_GUI = os.path.join(_RAIZ, "pruebas", "gui")
sys.path.insert(0, _HERRAMIENTAS_PRUEBAS)

import preparacion_comun as pc  # noqa: E402
import simulador as sim  # noqa: E402
from dataverse_api import Dataverse  # noqa: E402

LETRA = "A"
NOMBRE = "La app y sus comandos"

# Remitentes propios de este set (CONVENCIONES §5: nunca los de una persona real). El fixture
# compartido `PR*` (create-if-missing) es el mismo que usan C/S/I: `verificar_datos_negocio` es
# idempotente por remitente, no hay colisión entre sets que lo comparten.
REMITENTE = "zz-corredor-fase1@example.com"
REMITENTE_NO_AUTORIZADO = "zz-corredor-set-a-no-autorizado@example.com"

# sanic_estado de Fila (Choices.g.cs, EstadoDeLaFila — misma fuente que set_i.py/set_b.py).
VALIDADA, DIGITADA, APROBADA, RECHAZADA_AS400, ANULADA = 159460003, 159460004, 159460005, 159460006, 159460007

# sanic_estadoprocesamiento de Solicitud (Choices.g.cs, EstadoDeLaSolicitud).
NORECONOCIDA_SOL = 159460002
DESCARTADA_SOL = 159460003
PROCESADA_SOL = 159460006
CERRADA_SOL = 159460007
NOESNUEVO_SOL = 159460008

# sanic_evento de Bitácora (Choices.g.cs, EventoDeBitacora).
EVT_FILA_DIGITADA = 159460005
EVT_FILA_APROBADA = 159460006
EVT_FILA_DEVUELTA = 159460007
EVT_FILA_RECHAZADA_AS400 = 159460008
EVT_PROCESADA = 159460009
EVT_NORECONOCIDA_ATENDIDA = 159460013
EVT_DESCARTADA = 159460014
EVT_FILA_ANULADA = 159460018
EVT_REVISION_ATENDIDA = 159460021

# sanic_origen de Bitácora (Choices.g.cs, OrigenDelEvento). Ningún step de src/ asigna nunca `APP`
# (verificado el 2026-09-27, ver el docstring del módulo): todas las transiciones registran PLUGIN.
ORIGEN_PLUGIN = 159460007
ORIGEN_APP = 159460008

# `sanic_mppp_tbl_motivoaccion` (playbooks/tabla/sanic_mppp_tbl_motivoaccion.md): pluralización
# automática de Dataverse (agrega "s", igual que "solicitud" -> "solicituds" en simulador.py: no es
# pluralización en español).
MOTIVOACCIONES = "sanic_mppp_tbl_motivoaccions"


# ===========================================================================
# preparar / cerrar
# ===========================================================================
def preparar(ctx):
    """Deja listo TODO lo que el set A necesita, y nada más (CONVENCIONES §8):

    - **Datos de negocio** (fixture compartido `PR*`, create-if-missing) y el `REMITENTE` de este
      set autorizado sobre él — de acá sale la Solicitud en Validada que arman A-03/A-06/A-07/A-08/
      A-10/A-14 con `ctx.procesar()`.
    - **Los 15 Excel + el adjunto que no es Excel**, para `ctx.procesar()`.
    - **Los usuarios `User1`/`User2`** (Ejecutivo/Supervisor), resueltos por alias y con su rol
      verificado EN VIVO (`_preparar_personas`, igual patrón que `set_b.py`/`set_i.py` I-13). La
      verificación de rol es blanda: si algún día cambiara, los casos que no impersonan (A-01,
      A-02, A-04, A-05, A-09, A-13 — todos GUI) siguen corriendo igual; solo los que SÍ impersonan
      se reportan N-A con el motivo exacto.

    **Lo que NO prepara, y por qué:**

    - **El canon de configuración global** (§8.2). Lo aplica `correr.py` antes y después de CADA
      set, no cada set por separado.
    - **Las sesiones de navegador.** Ningún caso de este módulo abre un navegador: los casos que
      necesitan uno devuelven `N-A` citando el comando exacto de `pruebas/gui/` (ver el MD).
    """
    pc.verificar_datos_negocio(ctx.dv, REMITENTE)
    pc.verificar_excels()
    _preparar_personas(ctx)


def cerrar(ctx):
    """Borra, por GUID, las Solicitudes que este set creó. Los `MotivoAccion` que crea A-08 se
    crean y se borran DENTRO del propio caso (igual que hace `comandos.js`: son transporte, no
    dato) — no queda ninguno vivo al llegar acá."""
    return ctx.limpiar_solicitudes()


# ===========================================================================
# Helpers locales (no viven en preparacion_comun.py porque solo los necesita este set)
# ===========================================================================
def _preparar_personas(ctx):
    """Resuelve User1/User2 y verifica su rol, sin lanzar (igual criterio que `set_b.py`): la
    resolución del id es una precondición dura (si falla, lanza y el set entero no se prepara); el
    ROL correcto queda en `ctx.extra['personas_ok']`, y solo lo consultan los casos que
    impersonan (todos salvo los puramente GUI)."""
    ejecutivo_id = pc.usuario_por_alias(ctx.dv, "User1")
    supervisor_id = pc.usuario_por_alias(ctx.dv, "User2")
    ctx.extra["ejecutivo_id"] = ejecutivo_id
    ctx.extra["supervisor_id"] = supervisor_id
    ok_ej, roles_ej = pc.verificar_roles(ctx.dv, ejecutivo_id, ["SR - MPPP - Ejecutivo"])
    ok_su, roles_su = pc.verificar_roles(ctx.dv, supervisor_id, ["SR - MPPP - Supervisor"])
    ctx.extra["personas_ok"] = ok_ej and ok_su
    if not ctx.extra["personas_ok"]:
        ctx.extra["personas_motivo"] = (
            f"User1/User2 no tienen los roles que estos casos asumen (User1={roles_ej}, "
            f"User2={roles_su}); sin esos roles el caso daría 400 por falta de rol, no por lo que "
            "el caso quiere probar, y el resultado mentiría")


def _como(ctx, systemuserid, metodo, ruta, cuerpo=None):
    """`ctx.dv.call`, impersonando con `MSCRMCallerID` (CONVENCIONES §6)."""
    return ctx.dv.call(metodo, ruta, cuerpo, cabeceras={"MSCRMCallerID": systemuserid})


def _mensaje(cuerpo):
    return str((cuerpo or {}).get("error") or "")


def _crear_validadas(ctx, asunto, excel="E01-feliz-formato11.xlsx"):
    """Una Solicitud propia de este caso, con Filas en Validada (E01 trae 3). Devuelve
    `(sid, numero, filas, fids)` — `fids` en el mismo orden que `filas`."""
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto=asunto, cantidad_adjuntos=1,
                                     cantidad_excel=1, excel_a_subir=pc.excel(excel))
    numero = sim.leer_solicitud(ctx.dv, sid).get("sanic_nombre")
    filas = sim.leer_filas(ctx.dv, sid)
    fids = [_fila_id(ctx, sid, f["sanic_numerofila"]) for f in filas]
    return sid, numero, filas, fids


def _fila_id(ctx, sid, numerofila):
    est, r, _ = ctx.dv.call(
        "GET", f"{sim.FILAS}?$select=sanic_mppp_tbl_filaid&$filter=sanic_numerofila eq {numerofila}"
               f" and _sanic_solicitudid_value eq {sid}")
    if est != 200 or not r.get("value"):
        raise RuntimeError(f"no se encontró la fila {numerofila} de la solicitud {sid} (HTTP {est}): {r}")
    return r["value"][0]["sanic_mppp_tbl_filaid"]


def _leer_fila(ctx, fid, campos="sanic_estado,sanic_mensaje,sanic_notainterna,"
                                 "sanic_requiereatencionmanual,_sanic_digitadapor_value"):
    est, r, _ = ctx.dv.call("GET", f"{sim.FILAS}({fid})?$select={campos}")
    if est != 200:
        raise RuntimeError(f"no se pudo leer la fila {fid} (HTTP {est}): {r}")
    return r


# ===========================================================================
# Puente hacia `pruebas/gui/` (Playwright), por subproceso — mismo patrón que
# `sets/set_r.py::_dotnet_test` (dueño: set R, no se toca; acá una copia local,
# CONVENCIONES §8: un set no depende de otro set, tampoco de su código interno).
# ===========================================================================
def _extraer_reporte_json(stdout):
    """El reporter `json` de Playwright imprime UN objeto JSON al final de `stdout`,
    pero ANTES de eso `setup/global-setup.mjs` ya imprimió sus propios `console.log`
    (guarda de sesiones, semáforo, siembra — incluso una línea con JSON embebido:
    `sembrar_datos_gui.py -> creado: {...}`) — nada de eso es el reporte. `json.loads`
    a secas sobre TODO `stdout` revienta siempre con una corrida real (CONFIRMADO EN
    VIVO 2026-09-28: primera corrida por `correr.py`, el falso 'Playwright no devolvió
    JSON válido' que ESTA función corrige). Se busca la ÚLTIMA línea que es
    EXACTAMENTE '{' (el objeto de nivel superior que arma `JSON.stringify(reporte,
    undefined, 2)`; los objetos anidados como '"config": {' nunca quedan solos en su
    propia línea, y la línea de siembra tampoco: es una sola línea larga con el JSON
    pegado al texto) y se parsea desde ahí hasta el final. `None` si no se encontró."""
    posiciones = [m.start() for m in re.finditer(r"^\{[ \t]*$", stdout, re.MULTILINE)]
    if not posiciones:
        return None
    try:
        return json.loads(stdout[posiciones[-1]:])
    except json.JSONDecodeError:
        return None


def _playwright_test(archivo, esperados, usuario="ejecutivo", timeout=480, extra_env=None):
    """Corre `npx playwright test <archivo>` (`pruebas/gui/`) como subproceso, con el
    reporter JSON — más robusto que el regex de `_dotnet_test` (Playwright ya sabe
    contar sus propias pruebas, no hace falta parsear texto libre).

    Devuelve `(ok, total, passed, failed, detalle)`. `total is None` es SOLO el límite
    de ENTORNO real (node/npx ausente, timeout del proceso): N-A, igual criterio que
    `_dotnet_test`. Cualquier otro resultado — incluido `total == 0` — es un número que
    el LLAMADOR compara contra `esperados` con `c.check(...)`, NUNCA con `c.na(...)`:
    un archivo/filtro que no matchea ningún test de Playwright igual imprime `"0
    passed"` con exit code 0, el MISMO riesgo de falso verde que un FQN mal escrito en
    `dotnet test` — por eso el total ejecutado se verifica siempre, no solo el
    `returncode` (pedido explícito de esta migración).

    `timeout=480` (8 min): `a02-comandos-genericos-ocultos` con `ejecutivo` (9 tablas,
    ~1min cada una por la navegación real) tarda ~12min en el peor caso medido en vivo
    (2026-09-28) — se pasa `timeout` explícito más alto para ESE archivo puntual desde
    `a02()`; el resto de los archivos (1-2 tests) corre en 1-2 minutos.

    `MPPP_SEMAFORO_HEREDADO=1`: este proceso Python YA tiene el semáforo tomado por
    quien nos invocó (`correr.py`, para TODA la corrida) — sin este aviso,
    `setup/global-setup.mjs` (Node) intentaría tomarlo de nuevo, lo vería ocupado por
    un PID vivo (el nuestro, un nivel más arriba) y reventaría SIEMPRE con "SEMÁFORO EN
    ROJO": un falso FALLA en cada corrida conectada, no una contención real. Ver el
    comentario en `pruebas/gui/setup/global-setup.mjs`.

    Si la guarda de sesiones (o cualquier otra cosa dentro de `globalSetup`) revienta,
    esto NO se reporta como N-A: es un resultado real (0 pruebas corridas, con el
    motivo exacto en `detalle`) que el llamador convierte en FALLA — "Regla de
    aislamiento de fallas" de esta migración: FALLA ese caso puntual, nunca una
    excepción que tire abajo el resto del set (`correr.py` ya aísla por caso, pero acá
    además se captura para dar un motivo legible, no un traceback crudo)."""
    env = dict(os.environ)
    env["MPPP_USUARIO"] = usuario
    env["MPPP_SEMAFORO_HEREDADO"] = "1"
    if extra_env:
        env.update(extra_env)
    try:
        proc = subprocess.run(
            ["npx", "playwright", "test", archivo, "--reporter=json"],
            cwd=_PRUEBAS_GUI, env=env, capture_output=True, text=True, timeout=timeout)
    except FileNotFoundError as e:
        return False, None, None, None, f"'npx'/'playwright' no está disponible en el PATH: {e}"
    except subprocess.TimeoutExpired as e:
        # `TimeoutExpired.stdout`/`.stderr` son SIEMPRE bytes, incluso con `text=True`
        # en el `subprocess.run` original (vienen del buffer crudo, antes del decoder
        # de texto) — CONFIRMADO EN VIVO 2026-09-28 (`TypeError: can't concat str to
        # bytes` en la primera corrida por `correr.py`, con A-02/ejecutivo superando
        # el timeout de entonces). Se decodifican acá, nunca se asume `str`.
        def _texto(x):
            if x is None:
                return ""
            return x.decode("utf-8", errors="replace") if isinstance(x, bytes) else x
        parcial = _texto(e.stdout) + "\n" + _texto(e.stderr)
        return False, None, None, None, f"playwright test no terminó en {timeout}s:\n{parcial[-1500:]}"

    salida = (proc.stdout or "") + "\n" + (proc.stderr or "")
    reporte = _extraer_reporte_json(proc.stdout or "")
    if reporte is None:
        # globalSetup pudo reventar ANTES de que Playwright llegue a emitir JSON
        # válido (p.ej. la guarda de sesiones) — se trata como una corrida real con 0
        # pruebas, nunca como N-A (CONVENCIONES §3: un bloqueo real no es N-A).
        return False, 0, 0, 0, f"Playwright no devolvió JSON válido (rc={proc.returncode}):\n{salida[-1800:]}"

    stats = reporte.get("stats", {})
    passed = stats.get("expected", 0)
    failed = stats.get("unexpected", 0)
    flaky = stats.get("flaky", 0)
    skipped = stats.get("skipped", 0)
    total = passed + failed + flaky + skipped
    ok = proc.returncode == 0 and failed == 0 and flaky == 0
    detalle = f"rc={proc.returncode} passed={passed} failed={failed} flaky={flaky} skipped={skipped} total={total}\n{salida[-1800:]}"
    return ok, total, passed, failed, detalle


def _chequear_playwright(c, etiqueta, archivo, esperados, usuario="ejecutivo", extra_env=None, timeout=480):
    """Corre `_playwright_test` y traduce el resultado a `c.check(...)` — el patrón
    repetido por todos los casos GUI de este set (A-01, A-02, A-04, A-05, A-09, A-13,
    A-15): primero el guardia de "matcheó lo que se esperaba" (el que evita el falso
    verde), después el resultado real."""
    ok, total, passed, failed, detalle = _playwright_test(
        archivo, esperados, usuario=usuario, extra_env=extra_env, timeout=timeout)
    if total is None:
        c.na(f"no se pudo correr Playwright ({archivo}): {detalle}")
        return False
    c.check(
        f"{etiqueta}: '{archivo}' (usuario={usuario}) matcheó exactamente {esperados} prueba(s) de "
        "Playwright — un total distinto (0 incluido) es el mismo riesgo de falso verde que un FQN "
        "mal escrito en `dotnet test` (ver set_r.py)",
        total == esperados, f"matcheó {total} (passed={passed}, failed={failed})")
    c.check(f"{etiqueta}: las pruebas de '{archivo}' (usuario={usuario}) pasan", ok, detalle)
    return ok and total == esperados


# ===========================================================================
# A-01, A-02, A-04, A-05, A-09, A-13 — puramente GUI: solo un navegador ve un
# ribbon dibujado, un diálogo que se cerró sin guardar, o un idioma de UI.
# Ninguna llamada al Web API puede distinguir estos casos de "no pasó nada".
# Conectados el 2026-09-28 (antes N-A citando el comando exacto): ahora invocan
# `npx playwright test` por subproceso y reportan OK/FALLA real (pedido del
# aprobador: "un comando que hay que acordarse de correr es un comando que no se
# corre").
# ===========================================================================
def a01(ctx):
    c = pc.Caso("A-01", "Los botones aparecen donde deben (HomepageGrid y SubGrid)")
    ok, total, passed, failed, detalle = _playwright_test("ribbon-fila", esperados=2)
    ctx.extra["a01_resultado"] = (ok, total, passed, failed, detalle)  # A-15 lo reusa, no repite la corrida
    if total is None:
        c.na(f"no se pudo correr Playwright (ribbon-fila): {detalle}")
        return c
    c.check(
        "'ribbon-fila' matcheó exactamente 2 pruebas (HomepageGrid Validada + SubGrid) — un total "
        "distinto (0 incluido) es el mismo riesgo de falso verde que un FQN mal escrito en `dotnet "
        "test`", total == 2, f"matcheó {total} (passed={passed}, failed={failed})")
    c.check(
        "los botones que admiten Validada se dibujan, presentes y habilitados, en HomepageGrid Y en "
        "SubGrid (diagnóstico AUSENTE-vs-DESHABILITADO en el detalle si falla)", ok, detalle)
    return c


def a02(ctx):
    c = pc.Caso("A-02", "Los comandos genéricos NO aparecen en las tablas (12.6)")
    # HideCustomAction es una personalización de TABLA, no de rol: la metadata
    # compilada es la misma para cualquier usuario que abra esa tabla. Dos sesiones
    # alcanzan para cubrir las 11 tablas (ver TABLAS_A02 en selectores.mjs y el
    # docstring del .spec.mjs) sin repetir el mismo chequeo cuatro veces.
    # 9 tablas x ~1min de navegación real cada una: ~12min medido en vivo (2026-09-28)
    # en el peor caso -- timeout explícito más generoso que el default de 480s.
    _chequear_playwright(c, "Ejecutivo (9 tablas + verificación de cobertura)",
                          "a-02-comandos-genericos-ocultos", esperados=10, usuario="ejecutivo",
                          timeout=900)
    _chequear_playwright(c, "Administrador técnico (Parámetro, Regla + verificación de cobertura)",
                          "a-02-comandos-genericos-ocultos", esperados=3, usuario="admin_tecnico")
    return c


def a04(ctx):
    c = pc.Caso("A-04", "Cancelar no cambia nada")
    _chequear_playwright(c, "Cancelar 'Digitada'", "a-04-cancelar-no-cambia-nada", esperados=1)
    return c


def a05(ctx):
    c = pc.Caso("A-05", "Sin selección: ningún botón propio se dibuja (AUSENTE, no 'deshabilitado')")
    # CORRECCIÓN 2026-09-28: el título de este caso decía "los botones propios están
    # deshabilitados". Es la expectativa VIEJA e INCORRECTA — el propio arnés (2026-09-27,
    # `ribbon-fila.spec.mjs` de entonces) ya había confirmado en vivo que la plataforma
    # NO dibuja los botones sin selección (AUSENTE), no los deja "presentes pero
    # grises" (DESHABILITADO): son diagnósticos OPUESTOS con el mismo síntoma en
    # pantalla (ver A-15, mismo archivo). El cuerpo de `pruebas/05-app-y-comandos.md`
    # ya reflejaba el hallazgo correcto; lo que quedaba mal era este título corto (lo
    # único que lee quien solo mira el resumen de `correr.py`) y que la aserción vivía
    # mezclada dentro de `ribbon-fila.spec.mjs` (A-01) en vez de en su propio caso
    # invocable — separado a `casos/a-05-sin-seleccion.spec.mjs`, con la nota de
    # corrección fechada.
    _chequear_playwright(c, "Sin selección", "a-05-sin-seleccion", esperados=1)
    return c


def a09(ctx):
    c = pc.Caso("A-09", "Cancelar el diálogo de motivo (la X, sin guardar)")
    _chequear_playwright(c, "Cerrar con la X el diálogo de 'Anular'", "a-09-cancelar-dialogo-motivo", esperados=1)
    return c


def a13(ctx):
    c = pc.Caso("A-13", "Multiidioma de los botones y de los mensajes del JavaScript")
    # `local/sesion-ejecutivo.json` está confirmada en inglés (1033) en vivo desde el
    # 2026-09-27 (selectores.mjs); se usa para probar 1033. `local/sesion-supervisor.json`
    # se prueba esperando 3082 (español, la convención del proyecto) — si resultara
    # estar TAMBIÉN en inglés, este caso lo reporta como FALLA con el texto real
    # encontrado, un hallazgo legítimo (no se fuerza un verde inventando el idioma que
    # "debería" tener una sesión sin haberlo confirmado).
    _chequear_playwright(c, "Sesión 'ejecutivo' (confirmada en inglés, 1033)",
                          "a-13-multiidioma-botones", esperados=2, usuario="ejecutivo",
                          extra_env={"MPPP_IDIOMA_ESPERADO": "1033"})
    _chequear_playwright(c, "Sesión 'supervisor' (esperada en español, 3082)",
                          "a-13-multiidioma-botones", esperados=2, usuario="supervisor",
                          extra_env={"MPPP_IDIOMA_ESPERADO": "3082"})
    return c


# ===========================================================================
# A-03 — Digitada, sobre varias filas a la vez (acción masiva, DA-02): el
# EFECTO, sobre 3 Filas propias, una petición por fila (igual que aplicar()).
# ===========================================================================
def a03(ctx):
    c = pc.Caso("A-03", "Digitada, sobre varias filas a la vez (acción masiva, DA-02)")
    if not ctx.extra.get("personas_ok"):
        c.na(ctx.extra.get("personas_motivo"))
        return c
    sid, numero, filas, fids = _crear_validadas(ctx, "Prueba A-03 digitada masiva")
    ejecutivo = ctx.extra["ejecutivo_id"]

    for fid in fids:
        est, r, _ = _como(ctx, ejecutivo, "PATCH", f"{sim.FILAS}({fid})", {"sanic_estado": DIGITADA})
        c.check(f"fila {fid}: PATCH a Digitada devuelve 204 (una petición por fila, como aplicar())",
                est == 204, f"HTTP {est}: {_mensaje(r)}")

    for fid in fids:
        f = _leer_fila(ctx, fid)
        c.check(f"fila {fid}: quedó en Digitada", f.get("sanic_estado") == DIGITADA, str(f))

    bit = sim.leer_bitacora(ctx.dv, sid)
    for f in filas:
        c.check(
            f"Bitácora: 'Fila digitada' para la fila {f['sanic_numerofila']}, origen Plugin "
            "(corregido: el MD citaba 'App', pero OrigenDelEvento.App no se asigna en ningún step "
            "de src/ — ver el docstring del módulo)",
            any(b["sanic_evento"] == EVT_FILA_DIGITADA and b.get("sanic_numerofila") == f["sanic_numerofila"]
                and b.get("sanic_origen") == ORIGEN_PLUGIN for b in bit),
            str([(b.get("sanic_evento"), b.get("sanic_numerofila"), b.get("sanic_origen")) for b in bit]))
    return c


# ===========================================================================
# A-06 — Aprobar, como Supervisor, y el cierre automático de la Solicitud.
# ===========================================================================
def a06(ctx):
    c = pc.Caso("A-06", "Aprobar, como Supervisor — y el cierre automático de la Solicitud")
    if not ctx.extra.get("personas_ok"):
        c.na(ctx.extra.get("personas_motivo"))
        return c
    sid, numero, filas, fids = _crear_validadas(ctx, "Prueba A-06 aprobar")
    ejecutivo, supervisor = ctx.extra["ejecutivo_id"], ctx.extra["supervisor_id"]

    for fid in fids:
        est, r, _ = _como(ctx, ejecutivo, "PATCH", f"{sim.FILAS}({fid})", {"sanic_estado": DIGITADA})
        c.check(f"fila {fid}: el Ejecutivo digita", est == 204, f"HTTP {est}: {_mensaje(r)}")

    for fid in fids:
        est, r, _ = _como(ctx, supervisor, "PATCH", f"{sim.FILAS}({fid})", {"sanic_estado": APROBADA})
        c.check(f"fila {fid}: el Supervisor aprueba (otro usuario digitó: segregación de funciones OK)",
                est == 204, f"HTTP {est}: {_mensaje(r)}")

    for fid in fids:
        f = _leer_fila(ctx, fid)
        c.check(f"fila {fid}: quedó en Aprobada", f.get("sanic_estado") == APROBADA, str(f))

    s = sim.leer_solicitud(ctx.dv, sid)
    c.check("al resolverse las 3 filas, la Solicitud pasa a Procesada (CierreDeSolicitud)",
            s.get("sanic_estadoprocesamiento") == PROCESADA_SOL, str(s.get("sanic_estadoprocesamiento")))
    c.check("se armó una respuesta final (contenido no vacío; el TEXTO exacto lo verifica M-04, no este caso)",
            bool(s.get("sanic_respuestafinalcontenido")))
    bit = sim.leer_bitacora(ctx.dv, sid)
    c.check("Bitácora: evento Procesada", any(b["sanic_evento"] == EVT_PROCESADA for b in bit), str(bit))
    return c


# ===========================================================================
# A-07 — El plugin manda, no el botón (DA-07): rechazos por rol y lote
# parcial (DA-02), llamando al Web API directo, sobre datos propios.
# ===========================================================================
def a07(ctx):
    c = pc.Caso("A-07", "El plugin manda, no el botón (DA-07)")
    if not ctx.extra.get("personas_ok"):
        c.na(ctx.extra.get("personas_motivo"))
        return c
    sid, numero, filas, fids = _crear_validadas(ctx, "Prueba A-07 el plugin manda")
    ejecutivo, supervisor = ctx.extra["ejecutivo_id"], ctx.extra["supervisor_id"]
    f_validada, f_digitada_1, f_digitada_2 = fids[0], fids[1], fids[2]

    # 1) el botón "Digitada" ni se dibujaría para el Supervisor sobre esta selección; la llamada
    #    directa al Web API prueba que el SERVIDOR (no el botón) es quien decide.
    est, r, _ = _como(ctx, supervisor, "PATCH", f"{sim.FILAS}({f_validada})", {"sanic_estado": DIGITADA})
    c.check("el Supervisor no puede digitar, aunque llame al Web API directo (bypass del botón)",
            est == 400 and "rol" in _mensaje(r).lower(), f"HTTP {est}: {_mensaje(r)}")
    f = _leer_fila(ctx, f_validada)
    c.check("esa fila sigue Validada (el rechazo no la tocó)", f.get("sanic_estado") == VALIDADA, str(f))

    # 2) el Ejecutivo digita legítimamente las otras dos.
    for fid in (f_digitada_1, f_digitada_2):
        est, r, _ = _como(ctx, ejecutivo, "PATCH", f"{sim.FILAS}({fid})", {"sanic_estado": DIGITADA})
        c.check(f"fila {fid}: el Ejecutivo digita (setup legítimo)", est == 204, f"HTTP {est}: {_mensaje(r)}")

    # 3) el Ejecutivo no puede aprobar lo que él mismo digitó (rol: Aprobar es de Supervisor/RPA).
    est, r, _ = _como(ctx, ejecutivo, "PATCH", f"{sim.FILAS}({f_digitada_1})", {"sanic_estado": APROBADA})
    c.check("el Ejecutivo no puede aprobar (rol: solo Supervisor o RPA aprueban)",
            est == 400 and "rol" in _mensaje(r).lower(), f"HTTP {est}: {_mensaje(r)}")
    f = _leer_fila(ctx, f_digitada_1)
    c.check("esa fila sigue Digitada (el rechazo no la tocó)", f.get("sanic_estado") == DIGITADA, str(f))

    # 4) lote mixto, EN LA MISMA TANDA (una petición por fila, como aplicar()): f_validada (salta
    #    Digitada: transición inexistente) y f_digitada_2 (Digitada, corresponde). Ninguna de las
    #    dos tiene que arrastrar a la otra (DA-02).
    est_a, r_a, _ = _como(ctx, supervisor, "PATCH", f"{sim.FILAS}({f_validada})", {"sanic_estado": APROBADA})
    est_b, r_b, _ = _como(ctx, supervisor, "PATCH", f"{sim.FILAS}({f_digitada_2})", {"sanic_estado": APROBADA})
    c.check("lote mixto, fila Validada→Aprobada (salta Digitada): 'Transición no permitida'",
            est_a == 400 and "no permitida" in _mensaje(r_a).lower(), f"HTTP {est_a}: {_mensaje(r_a)}")
    c.check("lote mixto, fila Digitada→Aprobada (la OTRA de la MISMA tanda): SE APLICA igual",
            est_b == 204, f"HTTP {est_b}: {_mensaje(r_b)}")
    f_a = _leer_fila(ctx, f_validada)
    f_b = _leer_fila(ctx, f_digitada_2)
    c.check("la fila rechazada del lote sigue Validada (nada a medias)", f_a.get("sanic_estado") == VALIDADA, str(f_a))
    c.check("la fila aceptada del lote quedó Aprobada", f_b.get("sanic_estado") == APROBADA, str(f_b))
    return c


# ===========================================================================
# A-08 — Rechazada en AS400: el EFECTO del diálogo de motivo (transporte
# sanic_mppp_tbl_motivoaccion, DA-03), replicado tal cual lo hace comandos.js.
# ===========================================================================
def a08(ctx):
    c = pc.Caso("A-08", "Rechazada en AS400 — el efecto del diálogo de motivo")
    if not ctx.extra.get("personas_ok"):
        c.na(ctx.extra.get("personas_motivo"))
        return c
    sid, numero, filas, fids = _crear_validadas(ctx, "Prueba A-08 rechazada as400")
    ejecutivo = ctx.extra["ejecutivo_id"]
    f1 = fids[0]

    # 1) bypass total del diálogo: el servidor tiene que exigir el mensaje aunque nadie lo pida.
    est, r, _ = _como(ctx, ejecutivo, "PATCH", f"{sim.FILAS}({f1})", {"sanic_estado": RECHAZADA_AS400})
    c.check("sin mensaje, el SERVIDOR rechaza (no hace falta abrir ningún diálogo para probarlo)",
            est == 400 and "mensaje" in _mensaje(r).lower(), f"HTTP {est}: {_mensaje(r)}")
    f = _leer_fila(ctx, f1)
    c.check("esa fila sigue Validada (el rechazo no la tocó)", f.get("sanic_estado") == VALIDADA, str(f))

    # 2) replica exacta de pedirMotivo(): crear, leer, borrar el transporte.
    texto = f"Prueba A-08 motivo {numero}"
    est_c, r_c, cab_c = _como(ctx, ejecutivo, "POST", MOTIVOACCIONES, {"sanic_motivo": texto})
    c.check("el Ejecutivo puede crear su propio MotivoAccion (C R W D en alcance Usuario, 04 §2)",
            est_c in (200, 201, 204), f"HTTP {est_c}: {_mensaje(r_c)}")
    if est_c not in (200, 201, 204):
        return c
    mid_ = Dataverse.id_creado(cab_c)
    est_l, r_l, _ = _como(ctx, ejecutivo, "GET", f"{MOTIVOACCIONES}({mid_})?$select=sanic_motivo")
    c.check("se puede leer de vuelta el mismo texto (lo que hace el comando antes de aplicarlo)",
            est_l == 200 and r_l.get("sanic_motivo") == texto, f"HTTP {est_l}: {r_l}")
    est_b, r_b, _ = _como(ctx, ejecutivo, "DELETE", f"{MOTIVOACCIONES}({mid_})")
    c.check("el comando borra el MotivoAccion después de leerlo (transporte, no dato de negocio)",
            est_b in (200, 204), f"HTTP {est_b}: {_mensaje(r_b)}")

    # 3) con el motivo, la transición se aplica.
    est2, r2, _ = _como(ctx, ejecutivo, "PATCH", f"{sim.FILAS}({f1})",
                        {"sanic_estado": RECHAZADA_AS400, "sanic_mensaje": texto})
    c.check("con el motivo, la transición se aplica", est2 == 204, f"HTTP {est2}: {_mensaje(r2)}")
    f = _leer_fila(ctx, f1)
    c.check("quedó en Rechazada en AS400 con el texto en sanic_mensaje (lo lee el cliente)",
            f.get("sanic_estado") == RECHAZADA_AS400 and f.get("sanic_mensaje") == texto, str(f))
    bit = sim.leer_bitacora(ctx.dv, sid)
    c.check("Bitácora: Fila rechazada en AS400, origen Plugin",
            any(b["sanic_evento"] == EVT_FILA_RECHAZADA_AS400 and b.get("sanic_origen") == ORIGEN_PLUGIN for b in bit),
            str(bit))

    # 4) `sanic_motivo` es "ApplicationRequired" (Business Required) en la metadata
    #    (playbooks/tabla/sanic_mppp_tbl_motivoaccion.md dice "la obligatoriedad la impone el
    #    formulario y la plataforma"). Verificado EN VIVO el 2026-09-27: un POST directo al Web API
    #    SIN sanic_motivo se guarda igual (HTTP 204/201) — "Business Required" es una validación de
    #    FORMULARIO (Microsoft Learn, RequiredLevel: "the platform will not enforce this"), no del
    #    Web API. Corregido acá: la mitad "la plataforma" del playbook no aplica a una llamada
    #    directa; solo el diálogo de creación rápida (un formulario) la exige, y eso es GUI.
    est_sin, r_sin, cab_sin = _como(ctx, ejecutivo, "POST", MOTIVOACCIONES, {})
    c.check(
        "un MotivoAccion sin sanic_motivo SE GUARDA igual por Web API directo (ApplicationRequired "
        "es 'Business Required': lo exige el FORMULARIO de creación rápida, no la plataforma — así "
        "que la obligatoriedad real del diálogo de motivo depende 100% de la GUI, no de este caso)",
        est_sin in (200, 201, 204), f"HTTP {est_sin}: {_mensaje(r_sin)}")
    if est_sin in (200, 201, 204):
        _como(ctx, ejecutivo, "DELETE", f"{MOTIVOACCIONES}({Dataverse.id_creado(cab_sin)})")
    return c


# ===========================================================================
# A-10 — Anular y Devolver: motivo/nota interna, y qué distingue de verdad a
# una fila Devuelta.
# ===========================================================================
def a10(ctx):
    c = pc.Caso("A-10", "Anular y Devolver")
    if not ctx.extra.get("personas_ok"):
        c.na(ctx.extra.get("personas_motivo"))
        return c
    sid, numero, filas, fids = _crear_validadas(ctx, "Prueba A-10 anular y devolver")
    ejecutivo, supervisor = ctx.extra["ejecutivo_id"], ctx.extra["supervisor_id"]
    f_anular, f_devolver, f_sin_nota = fids[0], fids[1], fids[2]

    # --- Anular (Ejecutivo, Validada -> Anulada, exige mensaje al cliente) ---
    texto_anular = f"Prueba A-10 anular {numero}"
    est, r, _ = _como(ctx, ejecutivo, "PATCH", f"{sim.FILAS}({f_anular})",
                      {"sanic_estado": ANULADA, "sanic_mensaje": texto_anular})
    c.check("Anular con mensaje se aplica", est == 204, f"HTTP {est}: {_mensaje(r)}")
    f = _leer_fila(ctx, f_anular)
    c.check("quedó en Anulada con el mensaje al cliente", f.get("sanic_estado") == ANULADA
            and f.get("sanic_mensaje") == texto_anular, str(f))
    bit = sim.leer_bitacora(ctx.dv, sid)
    c.check("Bitácora: Fila anulada, origen Plugin",
            any(b["sanic_evento"] == EVT_FILA_ANULADA and b.get("sanic_origen") == ORIGEN_PLUGIN for b in bit), str(bit))

    # --- Devolver (Supervisor, Digitada -> Validada, exige nota interna) ---
    est, r, _ = _como(ctx, ejecutivo, "PATCH", f"{sim.FILAS}({f_devolver})", {"sanic_estado": DIGITADA})
    c.check("setup: el Ejecutivo digita la fila a devolver", est == 204, f"HTTP {est}: {_mensaje(r)}")
    texto_nota = f"Prueba A-10 nota interna {numero}"
    est, r, _ = _como(ctx, supervisor, "PATCH", f"{sim.FILAS}({f_devolver})",
                      {"sanic_estado": VALIDADA, "sanic_notainterna": texto_nota})
    c.check("Devolver con nota interna se aplica", est == 204, f"HTTP {est}: {_mensaje(r)}")
    f = _leer_fila(ctx, f_devolver)
    c.check("volvió a Validada", f.get("sanic_estado") == VALIDADA, str(f))
    c.check("la nota interna quedó en sanic_notainterna", f.get("sanic_notainterna") == texto_nota, str(f))
    c.check("la nota interna NUNCA llega a sanic_mensaje (04 §1: un correo ya enviado mostró "
            "'Devuelta' al cliente antes de esta corrección, 2026-09-24)",
            not (f.get("sanic_mensaje") or "").strip(), str(f))
    c.check("se olvida quién digitó (LimpiarDigitacion): la próxima aprobación exige la segregación entera de nuevo",
            f.get("_sanic_digitadapor_value") is None, str(f))
    bit = sim.leer_bitacora(ctx.dv, sid)
    c.check("Bitácora: Fila devuelta, origen Plugin",
            any(b["sanic_evento"] == EVT_FILA_DEVUELTA and b.get("sanic_origen") == ORIGEN_PLUGIN for b in bit), str(bit))

    # --- Devolver sin nota: el servidor lo exige aunque nadie pase por el diálogo ---
    est, r, _ = _como(ctx, ejecutivo, "PATCH", f"{sim.FILAS}({f_sin_nota})", {"sanic_estado": DIGITADA})
    c.check("setup: el Ejecutivo digita la tercera fila", est == 204, f"HTTP {est}: {_mensaje(r)}")
    est, r, _ = _como(ctx, supervisor, "PATCH", f"{sim.FILAS}({f_sin_nota})", {"sanic_estado": VALIDADA})
    c.check("Devolver sin nota interna se rechaza (bypass del diálogo)",
            est == 400 and "nota" in _mensaje(r).lower(), f"HTTP {est}: {_mensaje(r)}")
    return c


# ===========================================================================
# A-11 — Atendido y Descartar, sobre Solicitudes "Por clasificar".
# ===========================================================================
def _notificaciones_de(ctx, ownerid, numero):
    """`appnotification` del dueño `ownerid` cuyo título nombra la Solicitud `numero`
    (D-64/D-65: `AvisosDataverse`, alcance `usuario` en `prvReadappnotification`)."""
    est, r, _ = ctx.dv.call(
        "GET", f"appnotifications?$select=title,body,_ownerid_value&$filter=_ownerid_value eq {ownerid}")
    if est != 200:
        raise RuntimeError(f"no se pudieron leer las notificaciones de {ownerid} (HTTP {est}): {r}")
    return [n for n in r.get("value", []) if numero in (n.get("title") or "")]


def a11(ctx):
    c = pc.Caso("A-11", "Atendido y Descartar, sobre Solicitudes")
    if not ctx.extra.get("personas_ok"):
        c.na(ctx.extra.get("personas_motivo"))
        return c
    ejecutivo = ctx.extra["ejecutivo_id"]

    # --- Atendido: No reconocida -> Cerrada ---
    # Hasta el 2026-09-28 este caso daba FALLA: `sim.clasificar()` reventaba con
    # `InvalidCastException` en `AvisosDataverse.AvisarAEjecutivos` (D-59) y, arreglado ESE, con
    # "Read Privilege Check For Owner failed ... prvReadappnotification" (D-65) — TODO camino de
    # clasificación con `procesar=false` (remitente no reconocido, respuesta, reenvío ilegible) pasa
    # por ahí. Las dos están cerradas (`Datos/Avisos.cs`, `diseno/04-matriz-privilegios.md` §2): el
    # aviso "a todos los ejecutivos, cuando un correo va a Por clasificar" (DA-08, el único que ya
    # estaba construido antes del recorte de D-64) crea su notificación de verdad, y este caso ya no
    # necesita ningún `try/except` para sobrevivirlo.
    sid1, mid1 = ctx.solicitud(remitente=REMITENTE_NO_AUTORIZADO, asunto="Prueba A-11 atendido",
                                cantidad_adjuntos=1, cantidad_excel=1,
                                excel_a_subir=pc.excel("E01-feliz-formato11.xlsx"))
    sim.clasificar(ctx.dv, sid1)
    s1_antes = sim.leer_solicitud(ctx.dv, sid1)
    numero1 = s1_antes.get("sanic_nombre")
    c.check("setup: la Solicitud quedó No reconocida (remitente sin autorización)",
            s1_antes.get("sanic_estadoprocesamiento") == NORECONOCIDA_SOL, str(s1_antes.get("sanic_estadoprocesamiento")))
    avisos1 = _notificaciones_de(ctx, ejecutivo, numero1)
    c.check("AvisarAEjecutivos creó EXACTAMENTE una notificación para el Ejecutivo (D-59 y D-65 cerradas)",
            len(avisos1) == 1, str(avisos1))
    est, r, _ = _como(ctx, ejecutivo, "PATCH", f"{sim.SOLICITUDES}({sid1})", {"sanic_estadoprocesamiento": CERRADA_SOL})
    c.check("Atendido se aplica", est == 204, f"HTTP {est}: {_mensaje(r)}")
    s1 = sim.leer_solicitud(ctx.dv, sid1)
    c.check("quedó Cerrada", s1.get("sanic_estadoprocesamiento") == CERRADA_SOL, str(s1))
    c.check("nada nuevo se comunicó al cliente (acuse/respuesta sin cambios)",
            s1.get("sanic_acusecontenido") == s1_antes.get("sanic_acusecontenido")
            and s1.get("sanic_respuestafinalcontenido") == s1_antes.get("sanic_respuestafinalcontenido"), str(s1))
    bit1 = sim.leer_bitacora(ctx.dv, sid1)
    c.check("Bitácora: No reconocida atendida, origen Plugin",
            any(b["sanic_evento"] == EVT_NORECONOCIDA_ATENDIDA and b.get("sanic_origen") == ORIGEN_PLUGIN for b in bit1), str(bit1))

    # --- Descartar: No es correo nuevo -> Descartada ---
    sid2, mid2 = ctx.solicitud(remitente=REMITENTE, asunto="RE: Prueba A-11 descartar",
                                cantidad_adjuntos=1, cantidad_excel=1,
                                excel_a_subir=pc.excel("E01-feliz-formato11.xlsx"),
                                in_reply_to="original-simulado-a11@herramientas.local")
    sim.clasificar(ctx.dv, sid2)
    s2_antes = sim.leer_solicitud(ctx.dv, sid2)
    numero2 = s2_antes.get("sanic_nombre")
    c.check("setup: la Solicitud quedó No es correo nuevo (asunto RE:)",
            s2_antes.get("sanic_estadoprocesamiento") == NOESNUEVO_SOL, str(s2_antes.get("sanic_estadoprocesamiento")))
    avisos2 = _notificaciones_de(ctx, ejecutivo, numero2)
    c.check("AvisarAEjecutivos creó EXACTAMENTE una notificación para el Ejecutivo también acá",
            len(avisos2) == 1, str(avisos2))
    est, r, _ = _como(ctx, ejecutivo, "PATCH", f"{sim.SOLICITUDES}({sid2})", {"sanic_estadoprocesamiento": DESCARTADA_SOL})
    c.check("Descartar se aplica", est == 204, f"HTTP {est}: {_mensaje(r)}")
    s2 = sim.leer_solicitud(ctx.dv, sid2)
    c.check("quedó Descartada", s2.get("sanic_estadoprocesamiento") == DESCARTADA_SOL, str(s2))
    bit2 = sim.leer_bitacora(ctx.dv, sid2)
    c.check("Bitácora: Descartada, origen Plugin",
            any(b["sanic_evento"] == EVT_DESCARTADA and b.get("sanic_origen") == ORIGEN_PLUGIN for b in bit2), str(bit2))
    return c


# ===========================================================================
# A-12 — Revisado: apaga la marca y deja el motivo en la Bitácora.
# ===========================================================================
def a12(ctx):
    c = pc.Caso("A-12", "Revisado")
    if not ctx.extra.get("personas_ok"):
        c.na(ctx.extra.get("personas_motivo"))
        return c
    ejecutivo = ctx.extra["ejecutivo_id"]
    sid, mid = ctx.solicitud(remitente=REMITENTE, asunto="Prueba A-12 revisado",
                              cantidad_adjuntos=0, cantidad_excel=0)
    numero = sim.leer_solicitud(ctx.dv, sid).get("sanic_nombre")
    motivo_original = f"Prueba A-12: validación fallida 3 veces ({numero})"

    # Precondición, código de servidor (sin impersonar): en la vida real la prenden los flujos.
    est, r, _ = ctx.dv.call("PATCH", f"{sim.SOLICITUDES}({sid})",
                             {"sanic_requiererevision": True, "sanic_motivorevision": motivo_original})
    c.check("setup: la marca queda prendida con su motivo (código de servidor, exento de la lista blanca)",
            est in (200, 204), f"HTTP {est}: {r}")

    est2, r2, _ = _como(ctx, ejecutivo, "PATCH", f"{sim.SOLICITUDES}({sid})", {"sanic_requiererevision": False})
    c.check("el Ejecutivo apaga la marca (sin pedir texto: el motivo no es del operador)",
            est2 == 204, f"HTTP {est2}: {_mensaje(r2)}")

    s = sim.leer_solicitud(ctx.dv, sid)
    c.check("sanic_requiererevision quedó en No", s.get("sanic_requiererevision") is False, str(s))
    c.check("sanic_motivorevision se limpió (para no leer el motivo viejo la próxima vez)",
            not s.get("sanic_motivorevision"), str(s))
    bit = sim.leer_bitacora(ctx.dv, sid)
    c.check("Bitácora: Revision atendida, con el motivo ORIGINAL, origen Plugin",
            any(b["sanic_evento"] == EVT_REVISION_ATENDIDA and b.get("sanic_detalle") == motivo_original
                and b.get("sanic_origen") == ORIGEN_PLUGIN for b in bit), str(bit))
    return c


# ===========================================================================
# A-14 (fase 2, nuevo) — Atendida sobre Fila: apaga sanic_requiereatencionmanual
# sin cambiar sanic_estado. Ver el docstring del módulo: puede destapar un
# defecto real (lista blanca desactualizada, diseno/04 vs ReglasDeSteps.cs).
# ===========================================================================
def a14(ctx):
    c = pc.Caso("A-14", "Atendida, sobre Fila (fase 2): apaga el cerrojo del bot sin cambiar de estado")
    if not ctx.extra.get("personas_ok"):
        c.na(ctx.extra.get("personas_motivo"))
        return c
    sid, numero, filas, fids = _crear_validadas(ctx, "Prueba A-14 atendida")
    ejecutivo = ctx.extra["ejecutivo_id"]
    fid = fids[0]

    est, r, _ = ctx.dv.call("PATCH", f"{sim.FILAS}({fid})", {"sanic_requiereatencionmanual": True})
    c.check("setup: la marca queda prendida (en la vida real la prende el bot, B-19; acá código de servidor)",
            est in (200, 204), f"HTTP {est}: {r}")

    est2, r2, _ = _como(ctx, ejecutivo, "PATCH", f"{sim.FILAS}({fid})", {"sanic_requiereatencionmanual": False})
    c.check(
        "el Ejecutivo apaga la marca SIN cambiar sanic_estado (05 'Fase 2': el botón 'Atendida' no "
        "es una transición de Fila). Si esto da HTTP 400 'sanic_requiereatencionmanual no está(n) "
        "permitida(s)', NO es un problema de datos de prueba: es la discrepancia documentada en el "
        "MD entre diseno/04-matriz-privilegios.md (dice que la columna entra a la lista blanca "
        "desde fase 2) y Steps/ReglasDeSteps.cs.PermitidasEnFila (no la incluye) — reportar como "
        "FALLA con esa evidencia, no reintentar.",
        est2 == 204, f"HTTP {est2}: {_mensaje(r2)}")

    f = _leer_fila(ctx, fid)
    c.check("sanic_estado NO cambió (sigue Validada)", f.get("sanic_estado") == VALIDADA, str(f))
    c.check("la marca quedó apagada", f.get("sanic_requiereatencionmanual") is False, str(f))
    return c


# ===========================================================================
# A-15 — La lección del botón fantasma. El ex `H-06` de `sets/set_h.py` (fusionado
# el 2026-09-28: CONVENCIONES.md §4 — H se nombraba por la fecha, no por lo que
# prueba; este caso es sobre un COMANDO de la barra, tema de ESTE set).
#
# CONECTADO el 2026-09-28 (antes N-A completo). La MITAD que un navegador puede
# confirmar de verdad — que los botones NO estén AUSENTES de la lista real del
# DOM, la señal concreta del defecto de metadata compilada que costó dos días —
# ahora corre de verdad, reusando la MISMA corrida de Playwright que A-01
# (`ctx.extra["a01_resultado"]`, cacheado por `a01()`: correr Playwright DOS veces
# por la misma evidencia sería duplicar infraestructura y Dev innecesariamente,
# CONVENCIONES §9). La OTRA mitad — diagnosticar con el Command Checker
# (`&ribbondebug=true`) y, si hiciera falta, regenerar metadata desde
# make.powerapps.com — sigue siendo 100% manual (privilegio de PLATAFORMA, ningún
# usuario de prueba lo tiene) y se documenta en el `check` como el paso siguiente
# si este caso alguna vez da FALLA, no como motivo de N-A.
# ===========================================================================
def a15(ctx):
    c = pc.Caso("A-15", "La lección del botón fantasma: distinguir REGLAS de METADATA COMPILADA")
    cacheado = ctx.extra.get("a01_resultado")
    if cacheado is not None:
        ok, total, passed, failed, detalle = cacheado
        origen = "reusando la corrida de Playwright de A-01 (mismo archivo, mismo fixture: no se duplica)"
    else:
        ok, total, passed, failed, detalle = _playwright_test("ribbon-fila", esperados=2)
        origen = "corrida propia (A-01 no dejó nada cacheado en ctx.extra — no debería pasar en un set completo)"
    if total is None:
        c.na(f"no se pudo correr Playwright (ribbon-fila): {detalle}")
        return c
    c.check(
        f"'ribbon-fila' matcheó exactamente 2 pruebas ({origen}) — un total distinto es el mismo "
        "riesgo de falso verde que un FQN mal escrito en `dotnet test`",
        total == 2, f"matcheó {total} (passed={passed}, failed={failed})")
    c.check(
        "los botones de Fila NO están AUSENTES de la lista real del DOM (HomepageGrid y SubGrid) — "
        "la confirmación en vivo de que hoy NO se repite el defecto de metadata compilada que costó "
        "dos días el 2026-09-27 (DN-02, 'Descargar para digitar' publicado en el servidor y ausente "
        "en el cliente). SI ESTO DA FALLA: el diagnóstico manual completo (Command Checker con "
        "'&ribbondebug=true', privilegio de PLATAFORMA — System Administrator/System Customizer, NO "
        "el rol de negocio 'SR - MPPP - Administrador tecnico' — y, si un botón aparece AUSENTE de "
        "la lista entera del Command Checker, 'Regenerate ribbon metadata' en make.powerapps.com "
        "sobre 'sanic_mppp_sol_mantenimientoppp') sigue siendo manual y es el paso siguiente, no "
        "algo que este caso pueda hacer solo — pero la DETECCIÓN, que antes costó dos días, ahora "
        "corre en cada corrida del set.",
        ok, detalle)
    return c


CASOS = [a01, a02, a03, a04, a05, a06, a07, a08, a09, a10, a11, a12, a13, a14, a15]
