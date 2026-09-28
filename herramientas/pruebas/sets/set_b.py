#!/usr/bin/env python3
"""Set B — RPA fase 2, el bot (`pruebas/09-rpa-fase2.md`).

La puerta D-9/D-44 (`Steps/PuertaDelRpa`), FIFO estricto, el claim con
vencimiento, el tope de lote (rechaza el LOTE ENTERO), la tolerancia de fecha,
el catálogo `MPPP-RPA-001..009` y la marca de atención manual (con su cerrojo
y su auto-apagado). Fuente: `diseno/03-contratos-custom-api.md` §2-§3 y el
código en `src/Sanic.Mppp.Plugins/Dominio/ReglasDelRpa.cs`,
`Api/RegistrarResultadoRpa*.cs`, `Steps/PuertaDelRpa.cs`,
`Dominio/TransicionesDeFila.cs`. El contrato completo de un módulo de set está
en `sets/__init__.py`; la preparación compartida, en `preparacion_comun.py`.

**Dos identidades distintas, y por qué importa no confundirlas**:

- **El bot** (`local/rpa_cred.env`, nunca leído/impreso acá): la única que
  pasa la puerta D-9. Se usa a través de `probar_rpa.Bot` (`herramientas/pruebas/probar_rpa.py`),
  reutilizando su autenticación tal cual — este módulo NO reescribe el flujo
  de credenciales de cliente, solo llama `bot.llamar(...)` directo para tener
  control fino sobre cada request y poder armar los `Caso` con asserts, en vez
  de los `print()` que trae `probar_rpa.py` para uso manual.
- **El service principal** (`local/pp_secrets.env`, vía `ctx.dv`): verificado
  en vivo el 2026-09-27 (igual que dejó anotado `set_i.py` en I-12) — SÍ es una
  identidad de aplicación, pero **solo tiene `System Administrator`**, ningún
  rol de negocio. Sirve para el sub-caso "identidad de aplicación sin el rol
  RPA" (B-02) y como llamador de plataforma para todo lo que no necesita ser
  ni el bot ni una persona.

**Corrección respecto del MD tal como estaba escrito** (se explica también en
el reporte de esta migración): varios casos (B-07, B-12, la mitad de B-18 y
B-20) decían usar `actuar_fila.py` con el service principal como si fuera "una
persona" para chocar contra el claim o la segregación de funciones. Verificado
en vivo: el service principal **no tiene ningún rol de negocio**
(`SR - MPPP - Ejecutivo`/`Supervisor`/`RPA`), y `actuar_fila.py` exige
exactamente ese rol ANTES de escribir nada — con el SP, esos comandos
fallarían con "necesita el rol ... y no lo tiene", nunca con el mensaje del
claim o de la segregación que el caso quiere ver. La forma correcta, ya usada
por `set_i.py` en I-13, es impersonar directamente a un usuario real con el
rol (`User1` = Ejecutivo, `User2` = Supervisor) con la cabecera
`MSCRMCallerID` sobre `ctx.dv`, verificando antes el rol con
`preparacion_comun.verificar_roles`. Este módulo hace eso, y el MD se corrigió
para decirlo.

**B-01 (renombrar el rol del RPA) queda SIEMPRE N-A**, nunca se ejecuta: el
motivo está en su caso más abajo.
"""
import datetime
import json
import os
import subprocess
import sys

_AQUI = os.path.dirname(os.path.abspath(__file__))
_HERRAMIENTAS_PRUEBAS = os.path.dirname(_AQUI)
sys.path.insert(0, _HERRAMIENTAS_PRUEBAS)

import preparacion_comun as pc  # noqa: E402
import simulador as sim  # noqa: E402
import probar_rpa as rpa  # noqa: E402  (reusa Bot: la autenticación del bot no se reescribe acá)
from dataverse_api import Dataverse  # noqa: E402

LETRA = "B"
NOMBRE = "RPA fase 2 (bot)"

# Remitente propio de este set. Nunca el de una persona real (CONVENCIONES §5).
REMITENTE = "zz-corredor-fase2-rpa@example.com"

OBTENER = rpa.OBTENER
REGISTRAR = rpa.REGISTRAR

# sanic_estado de Fila (misma fuente que actuar_fila.py / set_i.py: metadata, no memoria).
VALIDADA, DIGITADA, APROBADA = 159460003, 159460004, 159460005
RECHAZADA_AS400, ANULADA = 159460006, 159460007

PARAMETROS = "sanic_mppp_tbl_parametros"


# ===========================================================================
# preparar / cerrar
# ===========================================================================
def preparar(ctx):
    """Deja listo TODO lo que el set B necesita, y nada más (CONVENCIONES §8):

    - **Datos de negocio** (fixture compartido `PR*`, create-if-missing) y el
      `REMITENTE` propio de este set autorizado sobre él — hace falta para que
      `simulador.ingresar_y_procesar` deje Filas en **Validada**, que es la
      materia prima de los 20 casos. Cada caso arma su PROPIA Solicitud con
      `E01-feliz-formato11.xlsx` (3 filas, misma `fechavalidada`: sirve tanto
      para FIFO como para lote) — el set NO depende de que haya corrido antes
      `01`/`02`/`03` ni otro set (a diferencia de lo que decía la versión vieja
      del MD).
    - **Los 15 Excel + el adjunto que no es Excel.**
    - **La identidad del bot** (`probar_rpa.Bot()`, credenciales de
      `local/rpa_cred.env`, nunca leídas/impresas acá). Si no autentica, NINGÚN
      caso de este set puede correr — es una precondición dura, y por eso hace
      fallar `preparar()` (el set entero se reporta como no preparado, en vez
      de 20 casos en rojo por la misma causa repetida 20 veces).
    - **Los usuarios `User1`/`User2`** (Ejecutivo/Supervisor), resueltos por
      alias (igual que `set_i.py` I-13). Su verificación de ROL es blanda
      (`ctx.extra["personas_ok"]`): si algún día cambiara, los casos que no
      necesitan una persona (la mayoría) siguen corriendo igual; solo los que
      impersonan (B-07, B-12, B-18, B-20) se reportan N-A con el motivo.

    **Lo que NO prepara, y por qué:**

    - **El canon de configuración global** (§8.2: `rpa.lote.maximo`,
      `rpa.horas.tolerancia`, `rpa.minutos.claimmaximo`, `rpa.puedeaprobar`).
      Lo aplica `correr.py` antes y después de CADA set, no cada set por
      separado. B-11 sí versiona `rpa.puedeaprobar` DENTRO de su propio caso
      (nunca edita la fila vigente) y la desactiva en su propio `finally`
      (§8.2: "volver al canon es desactivar la versión de prueba").
    - **El rol `SR - MPPP - RPA`** ni tocarlo ni renombrarlo: ver B-01.
    """
    pc.verificar_datos_negocio(ctx.dv, REMITENTE)
    pc.verificar_excels()

    try:
        ctx.extra["bot"] = rpa.Bot()
    except SystemExit as e:
        # `probar_rpa.Bot()` usa `raise SystemExit(...)` para no imprimir nunca
        # un secreto en el traceback. SystemExit no es una `Exception`: si no
        # se convierte acá, se escapa de `correr.py` y tira abajo la corrida
        # ENTERA (los otros sets de la lista ni llegan a correr) en vez de
        # reportar, con prolijidad, "el set B no se pudo preparar".
        raise RuntimeError(f"no se pudo autenticar como el bot: {e}")

    _preparar_personas(ctx)


def cerrar(ctx):
    """Borra, por GUID, las Solicitudes que este set creó Y las versiones de
    parámetro que `_versionar_parametro` sembró (CONVENCIONES §8.2: limpiar es
    DESACTIVAR (B-11 ya lo hace en su propio `finally`, paso 1) Y BORRAR (acá,
    paso 2) — "desactivar no es limpiar"). Si algo se cayó a mitad, el
    canon-después de `correr.py` sigue actuando como red de seguridad sobre el
    VALOR de la fila activa, aunque no sobre cuál fila quedó activa (§8.2 lo
    documenta)."""
    errores = ctx.limpiar_solicitudes()
    for pid in ctx.extra.get("parametros_creados", []):
        est, r, _ = ctx.dv.call("DELETE", f"{PARAMETROS}({pid})")
        if est not in (200, 204, 404):
            errores.append(f"no se pudo borrar la versión de prueba de parámetro {pid} (HTTP {est}): {r}")
    return errores


# ===========================================================================
# Helpers locales (no viven en preparacion_comun.py porque solo los necesita
# este set; se reportan en el mensaje de la migración, como pide la consigna)
# ===========================================================================
def _ahora_iso():
    return datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def _crear_validadas(ctx, asunto, excel="E01-feliz-formato11.xlsx"):
    """Una Solicitud propia de este caso, con Filas en Validada (E01 trae 3,
    todas con la misma `fechavalidada`: sirve para FIFO/desempate y para
    armar lotes). Devuelve `(sid, numero, filas)`."""
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto=asunto, cantidad_adjuntos=1,
                                     cantidad_excel=1, excel_a_subir=pc.excel(excel))
    numero = sim.leer_solicitud(ctx.dv, sid).get("sanic_nombre")
    filas = sim.leer_filas(ctx.dv, sid)
    return sid, numero, filas


def _fila_id(ctx, sid, numerofila):
    est, r, _ = ctx.dv.call(
        "GET", f"{sim.FILAS}?$select=sanic_mppp_tbl_filaid&$filter=sanic_numerofila eq {numerofila}"
                f" and _sanic_solicitudid_value eq {sid}")
    if est != 200 or not r.get("value"):
        raise RuntimeError(f"no se encontró la fila {numerofila} de la solicitud {sid} (HTTP {est}): {r}")
    return r["value"][0]["sanic_mppp_tbl_filaid"]


def _leer_fila(ctx, fid, campos="sanic_estado,sanic_claimhasta,sanic_requiereatencionmanual"):
    est, r, _ = ctx.dv.call("GET", f"{sim.FILAS}({fid})?$select={campos}")
    if est != 200:
        raise RuntimeError(f"no se pudo leer la fila {fid} (HTTP {est}): {r}")
    return r


def _texto_error(cuerpo):
    """El mensaje de un rechazo de `probar_rpa.Bot.llamar()`: puede venir
    envuelto (`{"error": {"message": ...}}`, el sobre OData completo que
    devuelve un 4xx real de Dataverse) o ya aplanado (`{"error": "..."}`, lo
    que arma `Bot.llamar` cuando el cuerpo no es JSON). Mismo criterio que
    `probar_rpa._mensaje_de_error`, copiado acá en vez de importar un símbolo
    privado de otro módulo."""
    error = cuerpo.get("error") if isinstance(cuerpo, dict) else None
    if isinstance(error, dict):
        return error.get("message", "")
    return str(error) if error else str(cuerpo or "")


def _guid_inventado(n):
    return "00000000-0000-0000-0000-%012d" % n


def _usuario_por_alias(dv, alias):
    """El `systemuserid` de un usuario de prueba por su alias de dominio,
    igual que `set_i.py`/`comportamiento_roles.py` (CONVENCIONES §6)."""
    est, r, _ = dv.call(
        "GET", "systemusers?$select=systemuserid,fullname"
               f"&$filter=startswith(domainname,'{alias}@') and applicationid eq null")
    filas = r.get("value", []) if est == 200 else []
    if len(filas) != 1:
        raise RuntimeError(f"{alias!r} no identifica un usuario único en el entorno: {filas}")
    return filas[0]["systemuserid"]


def _preparar_personas(ctx):
    """Resuelve User1/User2 y verifica su rol, sin lanzar: la resolución del
    id es lo único que se trata como precondición dura de las cosas que la
    necesitan (B-02 sub-caso 'persona'); el ROL correcto (Ejecutivo/
    Supervisor) queda en `ctx.extra['personas_ok']`, y solo lo consultan los
    casos que impersonan una transición real (B-07, B-12, B-18, B-20)."""
    try:
        ejecutivo_id = _usuario_por_alias(ctx.dv, "User1")
        supervisor_id = _usuario_por_alias(ctx.dv, "User2")
    except RuntimeError as e:
        ctx.extra["personas_ok"] = False
        ctx.extra["personas_motivo"] = f"no se pudo resolver User1/User2 en el entorno: {e}"
        return
    ctx.extra["ejecutivo_id"] = ejecutivo_id
    ctx.extra["supervisor_id"] = supervisor_id
    ok_ej, roles_ej = pc.verificar_roles(ctx.dv, ejecutivo_id, ["SR - MPPP - Ejecutivo"])
    ok_su, roles_su = pc.verificar_roles(ctx.dv, supervisor_id, ["SR - MPPP - Supervisor"])
    ctx.extra["personas_ok"] = ok_ej and ok_su
    if not ctx.extra["personas_ok"]:
        ctx.extra["personas_motivo"] = (
            f"User1/User2 no tienen los roles que estos casos asumen (User1={roles_ej}, User2={roles_su}); "
            "sin esos roles el caso daría 400 por falta de rol, no por lo que el caso quiere probar, y "
            "el resultado mentiría")


def _parametro_activo(dv, nombre):
    est, r, _ = dv.call(
        "GET", f"{PARAMETROS}?$select=sanic_mppp_tbl_parametroid,sanic_valor,sanic_version"
               f"&$filter=sanic_nombre eq '{nombre}' and statecode eq 0&$orderby=sanic_version desc&$top=1")
    if est != 200 or not r.get("value"):
        raise RuntimeError(f"no se encontró el parámetro activo {nombre!r} (HTTP {est}): {r}")
    return r["value"][0]


def _version_maxima_sembrada(dv, nombre):
    """La versión más alta SEMBRADA de `nombre`, activa o no (CONVENCIONES
    §8.2: "calcular la versión nueva como máximo sembrado + 1, no 'activa +
    1' ... protege contra restos de una corrida que murió antes de limpiar").
    Sin filtro de `statecode` a propósito: una versión desactivada (residuo de
    una corrida vieja que no llegó a borrar) sigue contando para no chocar
    con la clave alternativa (HTTP 412, el defecto real que dejó documentado
    §8.2 sobre el set E)."""
    est, r, _ = dv.call(
        "GET", f"{PARAMETROS}?$select=sanic_version"
               f"&$filter=sanic_nombre eq '{nombre}'&$orderby=sanic_version desc&$top=1")
    if est != 200 or not r.get("value"):
        raise RuntimeError(f"no se encontró ninguna versión sembrada de {nombre!r} (HTTP {est}): {r}")
    return int(r["value"][0]["sanic_version"])


def _versionar_parametro(ctx, nombre, valor):
    """Crea una versión NUEVA (fila) de `nombre` con `valor`, en
    **máximo sembrado + 1** (CONVENCIONES §8.2, nunca "activa + 1": protege
    contra restos de una corrida que murió antes de limpiar). Devuelve el
    GUID de la fila nueva y lo registra en `ctx.extra["parametros_creados"]`
    para que `cerrar()` la BORRE — desactivarla (el caso que llama a esto lo
    hace en su propio `finally`) es solo el paso 1 de 2; borrarla es el 2
    (§8.2: "desactivar no es limpiar")."""
    nueva_version = _version_maxima_sembrada(ctx.dv, nombre) + 1
    est, r, cab = ctx.dv.call("POST", PARAMETROS, {
        "sanic_nombre": nombre, "sanic_valor": valor, "sanic_version": nueva_version})
    if est not in (200, 201, 204):
        raise RuntimeError(f"no se pudo crear la versión de prueba de {nombre!r} (HTTP {est}): {r}")
    pid = Dataverse.id_creado(cab)
    ctx.extra.setdefault("parametros_creados", []).append(pid)
    return pid


def _desactivar_parametro(ctx, pid):
    ctx.dv.call("PATCH", f"{PARAMETROS}({pid})", {"statecode": 1, "statuscode": 2})


# ===========================================================================
# B-01 — SIEMPRE N-A. Alto riesgo de entorno compartido.
# ===========================================================================
def b01(ctx):
    c = pc.Caso("B-01", "Rol del RPA no instalado en el entorno (MPPP-RPA-007)")
    c.na(
        "renombrar/borrar el rol 'SR - MPPP - RPA' rompe la puerta del RPA para TODO Dev mientras dura "
        "(la propia ventaja de este caso es también su costo). Con varios agentes corriendo pruebas de "
        "fase 2 EN PARALELO sobre el mismo entorno compartido (coordinación de esta migración, "
        "2026-09-27), no hay ventana segura para tomarlo. Esta rama YA está cubierta a nivel unitario: "
        "`PuertaDelRpaAceptacion`, 8 pruebas, 7 de rechazo, en verde (diseno/FASE2-ESTADO.md) — la "
        "misma cobertura que I-12 usó como precedente para no repetir un riesgo de entorno sin "
        "necesidad. No es un límite de esta herramienta: es una decisión de no pagar ese riesgo contra "
        "Dev compartido cuando ya existe la cobertura equivalente sin él.")
    return c


# ===========================================================================
# B-02 — Las dos condiciones de D-9 fallan cerrado (MPPP-RPA-006).
# ===========================================================================
def b02(ctx):
    c = pc.Caso("B-02", "Sin identidad de aplicación, o sin el rol (MPPP-RPA-006)")
    # (a) identidad de aplicación (el service principal) SIN el rol RPA — verificado en vivo (I-12):
    # '# PowerPlatform-Dev' tiene applicationid pero solo el rol 'System Administrator'.
    est1, r1, _ = ctx.dv.call("GET", f"{OBTENER}(maximo=5,claimminutos=5)")
    texto1 = str(r1.get("error") or "")
    c.check("identidad de aplicación SIN el rol RPA: obtenerfilaspendientes rechaza con MPPP-RPA-006",
            est1 == 400 and "MPPP-RPA-006" in texto1, f"HTTP {est1}: {texto1}")

    est2, r2, _ = ctx.dv.call("POST", REGISTRAR, {"resultados": "[]"})
    texto2 = str(r2.get("error") or "")
    c.check("identidad de aplicación SIN el rol RPA: registrarresultadorpa rechaza con MPPP-RPA-006",
            est2 == 400 and "MPPP-RPA-006" in texto2, f"HTTP {est2}: {texto2}")

    # (b) una PERSONA (sin applicationid): impersonar un usuario humano real. El MD original solo
    # cubría (a) con el service principal ("sirve para el sub-caso 'sin el rol'"); esto completa la
    # otra mitad de lo que el caso dice probar, sin tocar nada del entorno (misma técnica de I-13).
    ejecutivo_id = ctx.extra.get("ejecutivo_id")
    if not ejecutivo_id:
        c.check("(sub-caso 'persona sin identidad de aplicación' no verificado: "
                f"{ctx.extra.get('personas_motivo')})", False)
        return c
    est3, r3, _ = ctx.dv.call("GET", f"{OBTENER}(maximo=5,claimminutos=5)",
                               cabeceras={"MSCRMCallerID": ejecutivo_id})
    texto3 = str(r3.get("error") or "")
    c.check("una PERSONA (sin applicationid, impersonando User1): obtenerfilaspendientes rechaza con MPPP-RPA-006",
            est3 == 400 and "MPPP-RPA-006" in texto3, f"HTTP {est3}: {texto3}")
    return c


# ===========================================================================
# B-03 — El bot pasa la puerta (D-9, camino feliz).
# ===========================================================================
def b03(ctx):
    c = pc.Caso("B-03", "El bot pasa la puerta (D-9, camino feliz)")
    proc = subprocess.run(
        [sys.executable, os.path.join(_HERRAMIENTAS_PRUEBAS, "como_rpa.py")],
        cwd=os.path.dirname(_HERRAMIENTAS_PRUEBAS), capture_output=True, text=True, timeout=60)
    c.check("como_rpa.py termina con código de salida 0", proc.returncode == 0,
            f"rc={proc.returncode} stdout={proc.stdout[-500:]!r} stderr={proc.stderr[-300:]!r}")
    salida = proc.stdout
    c.check("informa SÍ en la condición de identidad de aplicación", "identidad de aplicación? SÍ" in salida, salida[-500:])
    c.check("informa SÍ en la condición del rol SR - MPPP - RPA", "SR - MPPP - RPA?   SÍ" in salida, salida[-500:])
    c.check("termina diciendo que la puerta lo deja pasar", "lo deja pasar" in salida, salida[-500:])
    return c


# ===========================================================================
# B-04 — Orden FIFO estricto.
# ===========================================================================
def b04(ctx):
    c = pc.Caso("B-04", "Orden FIFO estricto (fecha validada, y a igualdad, número de fila)")
    bot = ctx.extra["bot"]
    sid, numero, filas = _crear_validadas(ctx, "Prueba B-04 FIFO")
    esperado = sorted(f["sanic_numerofila"] for f in filas)

    def _mis_filas():
        # maximo grande (el tope duro, 500) para no depender de que el entorno esté vacío de otras
        # filas pendientes (CONVENCIONES §8: un set autocontenido no asume un entorno limpio ajeno);
        # se filtra por el NÚMERO de esta Solicitud, no por posición absoluta en la cola.
        est, cuerpo = bot.llamar(f"{OBTENER}(maximo=500,claimminutos=10)")
        if est != 200:
            return None, est, cuerpo
        todas = json.loads(cuerpo.get("filas") or "[]")
        mias = [f["numerofila"] for f in todas if f.get("solicitud") == numero]
        return mias, est, cuerpo

    orden1, est1, cuerpo1 = _mis_filas()
    c.check("primera llamada: HTTP 200", est1 == 200, f"HTTP {est1}: {cuerpo1}")
    c.check(f"primera llamada: mis {len(esperado)} filas están, en orden ascendente de número de fila "
            "(misma fechavalidada: el desempate es por numerofila)", orden1 == esperado, f"obtenido {orden1}")
    orden2, est2, cuerpo2 = _mis_filas()
    c.check("segunda llamada, SIN reportar nada en el medio: exactamente el mismo orden (determinístico)",
            orden2 == orden1, f"1={orden1} 2={orden2}")
    return c


# ===========================================================================
# B-05 — `maximo`: defecto 100, recorte silencioso al tope 500 (nunca error).
# ===========================================================================
def b05(ctx):
    c = pc.Caso("B-05", "`maximo`: defecto 100, recorte silencioso al tope (nunca error)")
    bot = ctx.extra["bot"]
    _crear_validadas(ctx, "Prueba B-05 maximo defecto y recorte")

    est1, cuerpo1 = bot.llamar(f"{OBTENER}(claimminutos=5)")
    c.check("sin 'maximo': HTTP 200 (usa el defecto de 100, nunca error)", est1 == 200, f"HTTP {est1}: {cuerpo1}")

    est2, cuerpo2 = bot.llamar(f"{OBTENER}(maximo=999999,claimminutos=5)")
    c.check("maximo=999999: HTTP 200 (se recorta al tope 500, nunca error por pedir de más)",
            est2 == 200, f"HTTP {est2}: {cuerpo2}")
    if est2 == 200:
        c.check("'entregadas' no pasa de 500 (el tope duro)", (cuerpo2.get("entregadas") or 0) <= 500,
                str(cuerpo2.get("entregadas")))
    return c


# ===========================================================================
# B-06 — `maximo`/`claimminutos` inválidos rechazan la LLAMADA.
# ===========================================================================
def b06(ctx):
    c = pc.Caso("B-06", "`maximo`/`claimminutos` inválidos rechazan la llamada")
    bot = ctx.extra["bot"]
    _crear_validadas(ctx, "Prueba B-06 parametros invalidos")

    pruebas = [
        # D-61 (2026-09-28): `0` dejó de ser un error y pasó a significar el defecto (100).
        # En una Function, ausente y 0 son indistinguibles: Dataverse pone la clave con
        # `default(int)` cuando el bot omite el parámetro. El contrato viejo pedía las dos
        # cosas a la vez y por eso un bot que lo seguía al pie de la letra recibía un 400.
        # El tercer elemento es el HTTP esperado del RECHAZO (None = tiene que aceptar, 200).
        ("maximo=0,claimminutos=5", None, "maximo=0"),
        ("maximo=-3,claimminutos=5", 400, "maximo negativo"),
        ("claimminutos=5", None, "sin maximo: usa el defecto, NO es error"),
        # Corregido el 2026-09-28 (diagnóstico de D-60, evaluación del set B §Paso 4): este sub-caso
        # esperaba HTTP 400, pero verificado en vivo contra Dev da HTTP 404 "Resource not found for
        # the segment 'sanic_mppp_capi_obtenerfilaspendientes'". `claimminutos` está declarado
        # `isoptional: false` en `customapirequestparameters`: sin él, el ruteo OData rechaza el
        # segmento del recurso ANTES de resolver la Function — nunca llega al plugin, nunca pasa por
        # D-9 ni por ninguna línea de C#. No es un defecto del sistema: la plataforma hace lo
        # correcto, solo que MÁS TEMPRANO de lo que este caso asumía. Se corrige la expectativa, no
        # el sistema (CONVENCIONES §3: un defecto conocido no es N-A, pero tampoco se inventa uno
        # donde la plataforma actúa bien).
        ("maximo=5", 404, "sin claimminutos: falta un parámetro `isoptional:false` del ruteo OData"),
        ("maximo=5,claimminutos=0", 400, "claimminutos=0"),
    ]
    for parametros, estado_rechazo, titulo in pruebas:
        est, cuerpo = bot.llamar(f"{OBTENER}({parametros})")
        if estado_rechazo is None:
            c.check(f"{titulo}: acepta con HTTP 200", est == 200, f"HTTP {est}: {cuerpo}")
        else:
            texto = _texto_error(cuerpo)
            c.check(f"{titulo}: rechazada con HTTP {estado_rechazo} y un mensaje que nombra la causa",
                    est == estado_rechazo and bool(texto), f"HTTP {est}: {cuerpo}")
    return c


# ===========================================================================
# B-07 — El claim se pone al entregar, y frena a una PERSONA.
# ===========================================================================
def b07(ctx):
    c = pc.Caso("B-07", "El claim se pone al entregar, y frena a una PERSONA")
    if not ctx.extra.get("personas_ok"):
        c.na(ctx.extra.get("personas_motivo"))
        return c
    bot = ctx.extra["bot"]
    sid, numero, filas = _crear_validadas(ctx, "Prueba B-07 claim frena a una persona")
    fid = _fila_id(ctx, sid, filas[0]["sanic_numerofila"])

    est, cuerpo = bot.llamar(f"{OBTENER}(maximo=500,claimminutos=10)")
    c.check("el bot tomó el lote (precondición: el claim queda puesto al entregar)",
            est == 200, f"HTTP {est}: {cuerpo}")

    # Corrección respecto del MD original: NO se usa `actuar_fila.py` con el service principal (no
    # tiene el rol Ejecutivo, per I-12/preparar()) — se impersona a User1 (Ejecutivo real) directo,
    # mismo patrón que `set_i.py` I-13.
    est2, r2, _ = ctx.dv.call("PATCH", f"{sim.FILAS}({fid})", {"sanic_estado": DIGITADA},
                              cabeceras={"MSCRMCallerID": ctx.extra["ejecutivo_id"]})
    c.check("el Ejecutivo NO puede digitar mientras el claim está vigente", est2 == 400, f"HTTP {est2}: {r2}")
    texto = str(r2.get("error") or "")
    c.check("el mensaje dice CUÁNTO FALTA ('se libera'), nunca 'no autorizado' ni una hora del reloj",
            "se libera" in texto.lower() and "no autorizado" not in texto.lower(), texto)
    return c


# ===========================================================================
# B-08 — El claim NO frena al bot cuando reporta, aunque haya vencido.
# ===========================================================================
def b08(ctx):
    c = pc.Caso("B-08", "El claim NO frena al bot cuando reporta, aunque haya vencido")
    bot = ctx.extra["bot"]
    sid, numero, filas = _crear_validadas(ctx, "Prueba B-08 claim vencido no frena al bot")
    fid = _fila_id(ctx, sid, filas[0]["sanic_numerofila"])

    est, cuerpo = bot.llamar(f"{OBTENER}(maximo=500,claimminutos=1)")
    c.check("el bot tomó el lote (precondición: claim de 1 minuto puesto)", est == 200, f"HTTP {est}: {cuerpo}")

    # En vez de esperar el minuto real (el MD original decía "esperar más de 1 minuto"), se fuerza el
    # claim a un pasado reciente con un PATCH directo del service principal — EXACTAMENTE la misma
    # técnica que `set_i.py` ya probó en I-13 (`sanic_claimhasta` no entra a la lista blanca de una
    # PERSONA, pero sí la escribe una identidad de aplicación). Mismo resultado, sin sumarle un minuto
    # real a cada corrida de este set.
    pasado = (datetime.datetime.now(datetime.timezone.utc) - datetime.timedelta(minutes=5)).strftime("%Y-%m-%dT%H:%M:%SZ")
    est2, r2, _ = ctx.dv.call("PATCH", f"{sim.FILAS}({fid})", {"sanic_claimhasta": pasado})
    c.check("se pudo forzar el claim a un pasado reciente (mismo mecanismo de I-13, en vez de esperar el minuto real)",
            est2 == 204, f"HTTP {est2}: {r2}")

    items = [{"filaid": fid, "resultado": "digitada", "codigo": "OK", "fechaproceso": _ahora_iso()}]
    est3, cuerpo3 = bot.llamar(REGISTRAR, "POST", {"resultados": json.dumps(items)})
    c.check("registrarresultadorpa acepta la llamada (HTTP 200)", est3 == 200, f"HTTP {est3}: {cuerpo3}")
    if est3 == 200:
        item = next((i for i in json.loads(cuerpo3.get("resultados") or "[]") if i.get("filaid") == fid), None)
        c.check("el reporte se aceptó pese al claim vencido (estadofila=digitada)",
                item is not None and item.get("estadofila") == "digitada", str(item))
    return c


# ===========================================================================
# B-09 — El claim NO excluye: el bot puede volver a pedir lo mismo.
# ===========================================================================
def b09(ctx):
    c = pc.Caso("B-09", "El claim NO excluye: el bot puede volver a pedir lo mismo")
    bot = ctx.extra["bot"]
    sid, numero, filas = _crear_validadas(ctx, "Prueba B-09 claim no excluye")
    esperado = sorted(f["sanic_numerofila"] for f in filas)

    def _mias():
        est, cuerpo = bot.llamar(f"{OBTENER}(maximo=500,claimminutos=10)")
        todas = json.loads(cuerpo.get("filas") or "[]") if est == 200 else []
        return sorted(f["numerofila"] for f in todas if f.get("solicitud") == numero), est

    mias1, est1 = _mias()
    c.check("primera llamada: HTTP 200 y trae mis filas", est1 == 200 and mias1 == esperado, f"HTTP {est1}: {mias1}")
    mias2, est2 = _mias()
    c.check("segunda llamada, SIN reportar nada: trae LAS MISMAS filas (el claim no excluye al bot)",
            est2 == 200 and mias2 == mias1, f"1={mias1} 2={mias2}")
    return c


# ===========================================================================
# B-10 — Cuándo se libera el claim: terminal sí, Validada→Digitada no.
# ===========================================================================
def b10(ctx):
    c = pc.Caso("B-10", "Cuándo se libera el claim: en terminal sí, en Validada→Digitada no")
    bot = ctx.extra["bot"]
    sid, numero, filas = _crear_validadas(ctx, "Prueba B-10 libera en terminal, no en Digitada")
    fid_digitada = _fila_id(ctx, sid, filas[0]["sanic_numerofila"])
    fid_terminal = _fila_id(ctx, sid, filas[1]["sanic_numerofila"])

    est, cuerpo = bot.llamar(f"{OBTENER}(maximo=500,claimminutos=10)")
    c.check("el bot tomó el lote (precondición: claim puesto sobre las dos)", est == 200, f"HTTP {est}: {cuerpo}")

    ahora = _ahora_iso()
    items = [
        {"filaid": fid_digitada, "resultado": "digitada", "codigo": "OK", "fechaproceso": ahora},
        {"filaid": fid_terminal, "resultado": "rechazada", "codigo": "RECH-GEN", "mensaje": "prueba B-10", "fechaproceso": ahora},
    ]
    est2, cuerpo2 = bot.llamar(REGISTRAR, "POST", {"resultados": json.dumps(items)})
    c.check("registrarresultadorpa aceptó el lote (HTTP 200)", est2 == 200, f"HTTP {est2}: {cuerpo2}")

    f1 = _leer_fila(ctx, fid_digitada)
    f2 = _leer_fila(ctx, fid_terminal)
    c.check("la fila que quedó Digitada CONSERVA el claim (valor futuro, no se liberó: al bot le queda aprobar)",
            bool(f1.get("sanic_claimhasta")), f"sanic_claimhasta={f1.get('sanic_claimhasta')!r}")
    c.check("la fila en estado terminal (Rechazada en AS400) LIBERÓ el claim",
            f2.get("sanic_claimhasta") is None, f"sanic_claimhasta={f2.get('sanic_claimhasta')!r}")
    return c


# ===========================================================================
# B-11 — `rpa.puedeaprobar = no` oculta las Digitadas del lote.
# ===========================================================================
def b11(ctx):
    c = pc.Caso("B-11", "`rpa.puedeaprobar = no` oculta las Digitadas del lote")
    bot = ctx.extra["bot"]
    sid, numero, filas = _crear_validadas(ctx, "Prueba B-11 puedeaprobar=no oculta Digitadas")
    fid = _fila_id(ctx, sid, filas[0]["sanic_numerofila"])

    est, cuerpo = bot.llamar(f"{OBTENER}(maximo=500,claimminutos=10)")
    est2, cuerpo2 = bot.llamar(REGISTRAR, "POST", {"resultados": json.dumps(
        [{"filaid": fid, "resultado": "digitada", "codigo": "OK", "fechaproceso": _ahora_iso()}])})
    c.check("precondición: el bot digitó la fila", est == 200 and est2 == 200, f"HTTP {est}/{est2}: {cuerpo2}")

    pid = _versionar_parametro(ctx, "rpa.puedeaprobar", "no")
    try:
        est3, cuerpo3 = bot.llamar(f"{OBTENER}(maximo=500,claimminutos=10)")
        c.check("con rpa.puedeaprobar=no, la llamada sigue en 200", est3 == 200, f"HTTP {est3}: {cuerpo3}")
        todas = json.loads(cuerpo3.get("filas") or "[]") if est3 == 200 else []
        c.check("la fila Digitada por el propio bot NO aparece en el lote (solo Validadas)",
                not any(f.get("filaid") == fid for f in todas), str([f.get("filaid") for f in todas]))
    finally:
        # Nunca se edita la fila vigente: se desactiva la versión de prueba (§8.2), volviendo al canon
        # 'si' que ya tenía la fila original (versión 1), que sigue intacta y activa.
        _desactivar_parametro(ctx, pid)
    return c


# ===========================================================================
# B-12 — Para aprobar, solo lo que digitó el mismo bot.
# ===========================================================================
def b12(ctx):
    c = pc.Caso("B-12", "Para aprobar, solo lo que digitó el mismo bot")
    if not ctx.extra.get("personas_ok"):
        c.na(ctx.extra.get("personas_motivo"))
        return c
    bot = ctx.extra["bot"]
    sid, numero, filas = _crear_validadas(ctx, "Prueba B-12 solo aprueba lo que el mismo bot digito")
    fid = _fila_id(ctx, sid, filas[0]["sanic_numerofila"])

    est, r, _ = ctx.dv.call("PATCH", f"{sim.FILAS}({fid})", {"sanic_estado": DIGITADA},
                             cabeceras={"MSCRMCallerID": ctx.extra["ejecutivo_id"]})
    c.check("el Ejecutivo digitó la fila (persona, NO el bot)", est == 204, f"HTTP {est}: {r}")

    est2, cuerpo2 = bot.llamar(f"{OBTENER}(maximo=500,claimminutos=10)")
    c.check("obtenerfilaspendientes respondió 200", est2 == 200, f"HTTP {est2}: {cuerpo2}")
    todas = json.loads(cuerpo2.get("filas") or "[]") if est2 == 200 else []
    c.check("esa fila (Digitada por una persona) NO aparece en el lote del bot (no le consta que la digitó él)",
            not any(f.get("filaid") == fid for f in todas), str([f.get("filaid") for f in todas]))
    return c


# ===========================================================================
# B-13 — `fechaproceso`: offset explícito, no en el futuro, dentro de tolerancia (MPPP-RPA-003).
# ===========================================================================
def b13(ctx):
    c = pc.Caso("B-13", "`fechaproceso`: offset explícito, no en el futuro, dentro de tolerancia (MPPP-RPA-003)")
    bot = ctx.extra["bot"]
    sid, numero, filas = _crear_validadas(ctx, "Prueba B-13 fechaproceso invalida")
    fids = [_fila_id(ctx, sid, f["sanic_numerofila"]) for f in filas[:3]]

    manana = (datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(days=1)).strftime("%Y-%m-%dT%H:%M:%SZ")
    vieja = (datetime.datetime.now(datetime.timezone.utc) - datetime.timedelta(hours=48)).strftime("%Y-%m-%dT%H:%M:%SZ")
    casos_fecha = [
        (fids[0], "2026-10-05T15:04:12", "sin offset", "offset"),
        (fids[1], manana, "en el futuro", "futuro"),
        (fids[2], vieja, "más vieja que rpa.horas.tolerancia (48h > 24h)", "tolerancia"),
    ]
    items = [{"filaid": fid, "resultado": "digitada", "codigo": "OK", "fechaproceso": fecha}
             for fid, fecha, _titulo, _clave in casos_fecha]
    est, cuerpo = bot.llamar(REGISTRAR, "POST", {"resultados": json.dumps(items)})
    c.check("la llamada responde 200 (falla por ítem, no de la llamada entera)", est == 200, f"HTTP {est}: {cuerpo}")
    if est != 200:
        return c
    by_id = {r.get("filaid"): r for r in json.loads(cuerpo.get("resultados") or "[]")}
    textos = []
    for fid, _fecha, titulo, clave in casos_fecha:
        item = by_id.get(fid)
        texto = str((item or {}).get("error") or "")
        textos.append(texto)
        c.check(f"{titulo}: el error trae MPPP-RPA-003 y nombra la causa ({clave!r})",
                "MPPP-RPA-003" in texto and clave in texto.lower(), texto)
    c.check("los tres motivos son un texto DISTINTO cada vez (no el mismo repetido)",
            len(set(textos)) == 3, str(textos))
    return c


# ===========================================================================
# B-14 — Catálogo de errores por ítem: 001, 002, 004, 005.
# ===========================================================================
def b14(ctx):
    c = pc.Caso("B-14", "Catálogo de errores por ítem: 001, 002, 004, 005")
    bot = ctx.extra["bot"]
    sid, numero, filas = _crear_validadas(ctx, "Prueba B-14 catalogo de errores")
    # Tres filas REALES distintas para 002/004/005 (no la misma repetida: `probar_rpa.py` ya deja
    # anotado que dos operaciones sobre la MISMA fila en un lote rompe la llamada entera, no solo el
    # ítem) y un filaid inventado para 001.
    fid_002, fid_004, fid_005 = (_fila_id(ctx, sid, f["sanic_numerofila"]) for f in filas[:3])
    ahora = _ahora_iso()
    items = [
        {"filaid": _guid_inventado(1), "resultado": "digitada", "codigo": "OK", "fechaproceso": ahora},
        {"filaid": fid_002, "resultado": "aprobadaXYZ", "codigo": "OK", "fechaproceso": ahora},
        {"filaid": fid_004, "resultado": "rechazada", "codigo": "RECH-GEN", "fechaproceso": ahora},
        {"filaid": fid_005, "resultado": "rechazada", "codigo": "NO-EXISTE-ESTE-CODIGO", "mensaje": "prueba B-14", "fechaproceso": ahora},
    ]
    est, cuerpo = bot.llamar(REGISTRAR, "POST", {"resultados": json.dumps(items)})
    c.check("HTTP 200", est == 200, f"HTTP {est}: {cuerpo}")
    if est != 200:
        return c
    c.check("conerror = 4", cuerpo.get("conerror") == 4, str(cuerpo))
    errores = [str(r.get("error") or "") for r in json.loads(cuerpo.get("resultados") or "[]")]
    c.check("MPPP-RPA-001: filaid que no existe", any("MPPP-RPA-001" in e for e in errores), str(errores))
    c.check("MPPP-RPA-002: 'resultado' no es uno de los cuatro válidos", any("MPPP-RPA-002" in e for e in errores), str(errores))
    c.check("MPPP-RPA-004: falta 'mensaje' en un rechazada", any("MPPP-RPA-004" in e for e in errores), str(errores))
    c.check("MPPP-RPA-005: 'codigo' no está en rpa.codigos", any("MPPP-RPA-005" in e for e in errores), str(errores))
    c.check("ningún ítem afecta a los demás (los cuatro se reportan por separado)", len(errores) == 4, str(errores))
    return c


# ===========================================================================
# B-15 — Lote más grande que `rpa.lote.maximo` se rechaza ENTERO (MPPP-RPA-008).
# ===========================================================================
def b15(ctx):
    c = pc.Caso("B-15", "Lote más grande que `rpa.lote.maximo` se rechaza ENTERO (MPPP-RPA-008)")
    bot = ctx.extra["bot"]
    tope = int(_parametro_activo(ctx.dv, "rpa.lote.maximo")["sanic_valor"])
    sid, numero, filas = _crear_validadas(ctx, "Prueba B-15 lote mayor al tope")
    fid_real = _fila_id(ctx, sid, filas[0]["sanic_numerofila"])
    antes = _leer_fila(ctx, fid_real, campos="sanic_estado,sanic_claimhasta")

    ahora = _ahora_iso()
    items = [{"filaid": fid_real, "resultado": "digitada", "codigo": "OK", "fechaproceso": ahora}]
    items += [{"filaid": _guid_inventado(n), "resultado": "digitada", "codigo": "OK", "fechaproceso": ahora}
              for n in range(tope)]  # total = tope + 1
    est, cuerpo = bot.llamar(REGISTRAR, "POST", {"resultados": json.dumps(items)})
    c.check(f"un lote de {len(items)} ítems (rpa.lote.maximo={tope}) se rechaza ENTERO",
            est == 400, f"HTTP {est}: {cuerpo}")
    c.check("el error trae MPPP-RPA-008", "MPPP-RPA-008" in _texto_error(cuerpo), str(cuerpo))

    despues = _leer_fila(ctx, fid_real, campos="sanic_estado,sanic_claimhasta")
    c.check("la fila real queda EXACTAMENTE como estaba (nada se aplicó, ni siquiera el ítem bueno)",
            despues == antes, f"antes={antes} despues={despues}")
    return c


# ===========================================================================
# B-16 — Lote vacío se rechaza (MPPP-RPA-009).
# ===========================================================================
def b16(ctx):
    c = pc.Caso("B-16", "Lote vacío se rechaza (MPPP-RPA-009)")
    bot = ctx.extra["bot"]
    est, cuerpo = bot.llamar(REGISTRAR, "POST", {"resultados": "[]"})
    c.check("un lote vacío se rechaza (HTTP 400)", est == 400, f"HTTP {est}: {cuerpo}")
    c.check("el error trae MPPP-RPA-009", "MPPP-RPA-009" in _texto_error(cuerpo), str(cuerpo))
    return c


# ===========================================================================
# B-17 — Falla parcial: un ítem malo no arrastra a los buenos.
# ===========================================================================
def b17(ctx):
    c = pc.Caso("B-17", "Falla parcial: un ítem malo no arrastra a los buenos")
    bot = ctx.extra["bot"]
    sid, numero, filas = _crear_validadas(ctx, "Prueba B-17 falla parcial no arrastra")
    fid1 = _fila_id(ctx, sid, filas[0]["sanic_numerofila"])
    fid2 = _fila_id(ctx, sid, filas[1]["sanic_numerofila"])
    ahora = _ahora_iso()
    items = [
        {"filaid": fid1, "resultado": "digitada", "codigo": "OK", "fechaproceso": ahora},
        {"filaid": fid2, "resultado": "rechazada", "codigo": "RECH-GEN", "mensaje": "prueba B-17", "fechaproceso": ahora},
        {"filaid": _guid_inventado(1), "resultado": "digitada", "codigo": "OK", "fechaproceso": ahora},
    ]
    est, cuerpo = bot.llamar(REGISTRAR, "POST", {"resultados": json.dumps(items)})
    c.check("HTTP 200 (nunca todo o nada)", est == 200, f"HTTP {est}: {cuerpo}")
    if est == 200:
        c.check("procesadas=2, conerror=1", (cuerpo.get("procesadas"), cuerpo.get("conerror")) == (2, 1), str(cuerpo))
    f1 = _leer_fila(ctx, fid1, campos="sanic_estado")
    f2 = _leer_fila(ctx, fid2, campos="sanic_estado")
    c.check("fila 1 (buena) SÍ cambió a Digitada, pese al ítem malo del mismo lote",
            f1.get("sanic_estado") == DIGITADA, str(f1))
    c.check("fila 2 (buena) SÍ cambió a Rechazada en AS400", f2.get("sanic_estado") == RECHAZADA_AS400, str(f2))
    return c


# ===========================================================================
# B-18 — Idempotencia: `estadofila` manda, nunca `yaregistrado`.
# ===========================================================================
def b18(ctx):
    c = pc.Caso("B-18", "Idempotencia: `estadofila` manda, nunca `yaregistrado`")
    if not ctx.extra.get("personas_ok"):
        c.na(ctx.extra.get("personas_motivo"))
        return c
    bot = ctx.extra["bot"]
    sid, numero, filas = _crear_validadas(ctx, "Prueba B-18 idempotencia estadofila manda")
    fid1 = _fila_id(ctx, sid, filas[0]["sanic_numerofila"])
    fid2 = _fila_id(ctx, sid, filas[1]["sanic_numerofila"])
    ahora = _ahora_iso()

    def _reportar(fid):
        est, cuerpo = bot.llamar(REGISTRAR, "POST", {"resultados": json.dumps(
            [{"filaid": fid, "resultado": "digitada", "codigo": "OK", "fechaproceso": ahora}])})
        item = next(iter(json.loads(cuerpo.get("resultados") or "[]")), {}) if est == 200 else {}
        return est, item

    est1, r1 = _reportar(fid1)
    c.check("paso 1: se acepta (HTTP 200)", est1 == 200, str(r1))
    est2, r2 = _reportar(fid1)
    c.check("paso 2: repetir EXACTO devuelve yaregistrado=true, estadofila=digitada (nada cambia)",
            est2 == 200 and r2.get("yaregistrado") is True and r2.get("estadofila") == "digitada", str(r2))

    est3, r3 = _reportar(fid2)
    c.check("paso 3a: el bot digita la SEGUNDA fila", est3 == 200 and r3.get("estadofila") == "digitada", str(r3))
    est3b, r3b, _ = ctx.dv.call("PATCH", f"{sim.FILAS}({fid2})", {"sanic_estado": ANULADA, "sanic_mensaje": "prueba B-18"},
                                cabeceras={"MSCRMCallerID": ctx.extra["ejecutivo_id"]})
    c.check("paso 3b: el Ejecutivo anula esa fila (una persona actuó en el medio)", est3b == 204, f"HTTP {est3b}: {r3b}")

    est4, r4 = _reportar(fid2)
    c.check("paso 4: reportar digitada OTRA VEZ sobre la fila anulada: yaregistrado=true TAMBIÉN, "
            "pero estadofila=anulada (dice la verdad, el bot tiene que soltarla)",
            est4 == 200 and r4.get("yaregistrado") is True and r4.get("estadofila") == "anulada", str(r4))
    return c


# ===========================================================================
# B-19 — Atención manual: marca, libera el claim, y cierra la puerta al bot.
# ===========================================================================
def b19(ctx):
    c = pc.Caso("B-19", "Atención manual: marca, libera el claim, y cierra la puerta al bot")
    bot = ctx.extra["bot"]
    sid, numero, filas = _crear_validadas(ctx, "Prueba B-19 atencion manual marca y cerrojo")
    fid = _fila_id(ctx, sid, filas[0]["sanic_numerofila"])

    est, cuerpo = bot.llamar(f"{OBTENER}(maximo=500,claimminutos=10)")
    c.check("el bot tomó el lote (precondición: claim puesto)", est == 200, f"HTTP {est}: {cuerpo}")

    est2, cuerpo2 = bot.llamar(REGISTRAR, "POST", {"resultados": json.dumps(
        [{"filaid": fid, "resultado": "atencionmanual", "codigo": "RECH-GEN", "mensaje": "prueba B-19", "fechaproceso": _ahora_iso()}])})
    c.check("atencionmanual se acepta (HTTP 200)", est2 == 200, f"HTTP {est2}: {cuerpo2}")

    f = _leer_fila(ctx, fid)
    c.check("sanic_requiereatencionmanual quedó en sí", f.get("sanic_requiereatencionmanual") is True, str(f))
    c.check("sanic_claimhasta quedó vacío (se liberó)", f.get("sanic_claimhasta") is None, str(f))

    est3, cuerpo3 = bot.llamar(f"{OBTENER}(maximo=500,claimminutos=10)")
    c.check("segunda llamada: HTTP 200", est3 == 200, f"HTTP {est3}: {cuerpo3}")
    todas = json.loads(cuerpo3.get("filas") or "[]") if est3 == 200 else []
    c.check("esa fila NO aparece en el lote, aunque el FIFO se la daría primero (gana el cerrojo)",
            not any(x.get("filaid") == fid for x in todas), str([x.get("filaid") for x in todas]))
    return c


# ===========================================================================
# B-20 — La marca de atención manual se apaga sola con cualquier transición exitosa.
# ===========================================================================
def b20(ctx):
    c = pc.Caso("B-20", "La marca de atención manual se apaga sola con cualquier transición exitosa")
    if not ctx.extra.get("personas_ok"):
        c.na(ctx.extra.get("personas_motivo"))
        return c
    bot = ctx.extra["bot"]
    sid, numero, filas = _crear_validadas(ctx, "Prueba B-20 la marca se apaga sola")
    fid = _fila_id(ctx, sid, filas[0]["sanic_numerofila"])

    bot.llamar(f"{OBTENER}(maximo=500,claimminutos=10)")
    est, cuerpo = bot.llamar(REGISTRAR, "POST", {"resultados": json.dumps(
        [{"filaid": fid, "resultado": "atencionmanual", "codigo": "RECH-GEN", "mensaje": "prueba B-20", "fechaproceso": _ahora_iso()}])})
    c.check("precondición: la fila quedó marcada para atención manual", est == 200, f"HTTP {est}: {cuerpo}")
    antes = _leer_fila(ctx, fid)
    c.check("la marca está prendida antes de la transición", antes.get("sanic_requiereatencionmanual") is True, str(antes))

    est2, r2, _ = ctx.dv.call("PATCH", f"{sim.FILAS}({fid})", {"sanic_estado": DIGITADA},
                              cabeceras={"MSCRMCallerID": ctx.extra["ejecutivo_id"]})
    c.check("el Ejecutivo digita la fila (una transición exitosa cualquiera; el claim ya está liberado por B-19/atencionmanual)",
            est2 == 204, f"HTTP {est2}: {r2}")

    despues = _leer_fila(ctx, fid)
    c.check("la marca se apagó SOLA, sin que nadie la haya tocado a propósito",
            despues.get("sanic_requiereatencionmanual") is False, str(despues))
    return c


CASOS = [b01, b02, b03, b04, b05, b06, b07, b08, b09, b10,
         b11, b12, b13, b14, b15, b16, b17, b18, b19, b20]
