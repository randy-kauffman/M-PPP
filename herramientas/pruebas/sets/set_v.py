#!/usr/bin/env python3
"""Set V — Vigilancia, `MPPP-VIG` (`pruebas/07-vigilancia.md`).

La red de seguridad de todo el sistema: recupera correos que `MPPP-REC` no llegó a procesar,
reintenta clasificación/validación con su contador, recupera comunicaciones que `MPPP-COM`/`MPPP-ENV`
no llegaron a completar (nunca iniciadas o iniciadas sin confirmar) y vence lo que nadie clasificó.
El contrato completo de un módulo de set está en `sets/__init__.py`; la lógica de preparación
compartida, en `preparacion_comun.py`.

**Migrado el 2026-09-27/28** junto con la investigación de la pregunta que dejó abierta el set M
(`04-comunicaciones.md`, caso M-06): por qué una Solicitud con el acuse iniciado y nunca confirmado
no vio actuar a `MPPP-VIG` en ~22 minutos de espera real. La respuesta completa (con evidencia) está
en `pruebas/07-vigilancia.md`, sección "La pregunta que dejó abierta M-06". En resumen:

- El bloque `Envios_iniciados_sin_confirmar` (el que M-06 necesitaba) **no tiene un defecto de
  consulta**: hay Solicitudes reales, marcadas por este mismo bloque, con timestamps recientes.
  **V-07** (nuevo) lo prueba sin esperar, corriendo la MISMA consulta contra una Solicitud recién
  preparada, antes de arrancar el reloj.
- El encadenamiento **entre los 5 bloques** SÍ blinda con `runAfter: [Succeeded, Failed]` (tanto en
  el repo como en el flujo desplegado — **V-01** lo compara). La cascada que M-06 sospechaba
  ("el flujo se corta antes de llegar a ese bloque") no pasa ahí.
- Lo que estaba roto, confirmado por **V-01** (a propósito en rojo, CONVENCIONES §3), era la etapa
  de arranque común de los 5 bloques (los 4 `Parametro_*` + `Umbrales` + el bloque 1): estaba
  encadenada solo con `["Succeeded"]`. Si esa etapa fallaba en una corrida puntual, los CINCO
  bloques se salteaban esa vez — no solo el bloque 4. Esto explicaba por qué M-06 pudo no ver actuar
  a `MPPP-VIG` en una ventana de ~22 minutos sin que el bloque 4 estuviera roto en sí mismo.
  **Corregido el 2026-09-28 (D-62)**: se agregó `"Failed"` a los cinco `runAfter` de esa etapa
  (razonamiento del alcance en `playbooks/flujo/mppp-vig.md`) y `runtimeConfiguration.concurrency.
  runs=1` al trigger (evidencia de corridas superpuestas en `pruebas/07-vigilancia.md`). **V-01 pasó
  de FALLAR a propósito a verificar que ese blindaje sigue puesto** — ver su docstring más abajo.
- Aparte: ningún bloque de `MPPP-VIG` escribe un evento de Bitácora "Requiere revision" (159460016)
  al marcar una Solicitud — eso lo hace `MPPP-ENV`, en otro escenario. M-06 asumía esa entrada; este
  set no repite esa asunción (ver V-04 y V-07).

**Qué NO se automatiza, y por qué.**

- **V-02** (bloque 1, correo en la bandeja) sigue siendo manual: exige apagar
  `Cloud Flow - MPPP - REC` de verdad, y este set (como ningún otro) **nunca desactiva ni modifica
  un flujo de Power Automate**, aunque el service principal tenga privilegio de sobra para hacerlo.
- **V-03, V-04, V-05, V-06 y V-07** dependen del schedule real de `MPPP-VIG` (recurrence de 10
  minutos, confirmado en vivo — ver `preparar()`). Con el margen de este set (3 vueltas completas +
  colchón = 32 minutos), los cinco juntos superan las dos horas y media: **no corren de rutina**
  (mismo criterio que el set E con `E-08`). Por default quedan `N-A` explicando el comando exacto
  para correrlos a propósito: `MPPP_V_LENTOS=si python3 herramientas/pruebas/correr.py V`.
"""
import datetime
import json
import os
import sys
import time

_AQUI = os.path.dirname(os.path.abspath(__file__))
_HERRAMIENTAS_PRUEBAS = os.path.dirname(_AQUI)
_RAIZ = os.path.dirname(os.path.dirname(_HERRAMIENTAS_PRUEBAS))
sys.path.insert(0, _HERRAMIENTAS_PRUEBAS)

import preparacion_comun as pc  # noqa: E402
import simulador as sim  # noqa: E402

LETRA = "V"
NOMBRE = "Vigilancia (MPPP-VIG)"

# Remitente propio de este set. Nunca el de una persona real (CONVENCIONES §5).
REMITENTE = "zz-corredor-fase1@example.com"

# Bandera explícita para los 5 casos lentos (V-03, V-04, V-05, V-06, V-07). A propósito NO hay forma
# de "recordarla": quien la necesita la escribe cada vez (mismo patrón que set_e.py, BANDERA_E08).
BANDERA_LENTOS = "MPPP_V_LENTOS"

# --- El recurrence real, confirmado en vivo por V-01 (ver preparar()) y documentado en el .md ---
RECURRENCE_MINUTOS = 10
VUELTAS_MARGEN = 3
MARGEN_SEGUNDOS = VUELTAS_MARGEN * RECURRENCE_MINUTOS * 60 + 120  # 32 minutos: 3 vueltas + colchón

NOMBRE_FLUJO_VIG = "Cloud Flow - MPPP - VIG - Vigilar pendientes"
RUTA_JSON_VIG = os.path.join(_RAIZ, "recursos", "flujos", "mppp-vig.json")

PARAMETROS_UMBRAL = (
    "vigilancia.minutos.sinvalidar", "vigilancia.minutos.sinresponder",
    "vigilancia.reintentos.maximo", "clasificacion.dias.vencimiento",
)

PARAMETROS_RETENCION = ("retencion.dias.general", "retencion.dias.noreconocidas")

# --- choices (src/Sanic.Mppp.Plugins/Dominio/Choices.g.cs, valores verificados el 2026-09-27) ---
INGRESADA, NORECON, RECH, ENPROC, PROC, CERR, NOESCORREO, VENCIDA = (
    159460001, 159460002, 159460004, 159460005, 159460006, 159460007, 159460008, 159460009)
EVT_REINTENTO, EVT_ERROR, EVT_VENCIDA = 159460015, 159460017, 159460020
ORIGEN_VIG = 159460005

MOTIVO_V07 = "Envio iniciado sin confirmar: no se reenvia para que el cliente no reciba dos veces lo mismo."


# ===========================================================================
# preparar / cerrar
# ===========================================================================
def preparar(ctx):
    """Deja listo TODO lo que el set V necesita, y nada más (CONVENCIONES §8):

    - **La definición REAL de `MPPP-VIG`** (desplegada, vía `workflows.clientdata`) y la del repo
      (`recursos/flujos/mppp-vig.json`, solo lectura): las usa V-01 para comparar sin esperar nada.
    - **Los 4 umbrales**, leídos EN VIVO (nunca hardcodeados: CONVENCIONES §7, "nombrar no contar")
      — de acá salen los márgenes exactos que V-02..V-07 usan para envejecer sus datos.
    - **`retencion.dias.general` y `retencion.dias.noreconocidas`, CONTENIDO** (no solo que existan):
      que el valor activo de cada uno sea un entero positivo. Ningún caso de ESTE set los consume
      todavía (los usa el job de purga de fase 3, que no tiene set propio aún); se verifican acá
      porque V ya lee, con el mismo patrón, los otros parámetros numéricos de fase 1
      (`vigilancia.*`, `clasificacion.dias.vencimiento`) — es el hogar más cercano que existe hoy,
      no una dependencia de código. Recupera la cobertura que perdió el antiguo P-06 al disolver el
      set P (decisión del aprobador 2026-09-28: la verificación viaja con el set que la necesita).
    - **Los 5 flujos activos**: este set depende de los cinco más que ningún otro (V-05/V-07 dejan
      completar un acuse real antes de forzar el escenario "a medias", igual que hace el set M).

    **Lo que NO prepara si `MPPP_V_LENTOS` no está puesto, y por qué:** el fixture de negocio
    completo (`PR*`) y los Excel solo los necesitan V-05 y V-07 (los dos únicos casos que llaman
    `ctx.procesar()` con un Excel real). Si los casos lentos no van a correr esta vez, prepararlos
    igual sería trabajo — y una llamada más a la plataforma — que nadie usa.
    """
    ctx.extra["vig_live"] = _leer_vig_desplegado(ctx.dv)
    ctx.extra["vig_repo"] = _leer_vig_repo()

    umbrales = {}
    for nombre in PARAMETROS_UMBRAL:
        fila = pc.verificar_parametro_activo(ctx.dv, nombre)
        umbrales[nombre] = int(fila["sanic_valor"])
    ctx.extra["umbrales"] = umbrales

    retencion = {}
    for nombre in PARAMETROS_RETENCION:
        retencion[nombre] = pc.verificar_parametro_entero_positivo(ctx.dv, nombre)
    ctx.extra["retencion"] = retencion

    faltan, apagados = pc.verificar_flujos_activos(ctx.dv)
    if faltan or apagados:
        raise RuntimeError(
            f"MPPP-VIG depende de que los 5 flujos de la solución estén activos: faltan={faltan} "
            f"apagados={apagados} — sin esto ni los casos rápidos (V-01) miden lo mismo que en "
            "producción, ni los lentos (V-03..V-07) tienen sentido")

    if os.environ.get(BANDERA_LENTOS) == "si":
        pc.verificar_datos_negocio(ctx.dv, REMITENTE)
        pc.verificar_excels()
        ctx.extra["estructura"] = pc.verificar_estructura_plantilla(ctx.dv)


def cerrar(ctx):
    """Borra, por GUID, las Solicitudes que este set creó (V-03, V-04, V-05, V-06, V-07). V-02 es
    manual y no crea nada por su cuenta (usa un correo real + el buzón de ingesta compartido, igual
    que el set M, y ESE correo no lo borra este set: no es su dueño)."""
    return ctx.limpiar_solicitudes()


# ===========================================================================
# Helpers: la definición del flujo (vivo y repo), el reloj, los $filter EXACTOS
# ===========================================================================
def _leer_vig_desplegado(dv):
    est, r, _ = dv.call(
        "GET", f"workflows?$select=name,statecode,clientdata&$filter=name eq '{NOMBRE_FLUJO_VIG}'")
    filas = r.get("value", []) if est == 200 else []
    if len(filas) != 1:
        raise RuntimeError(f"no se encontró (o hay más de uno) el flujo {NOMBRE_FLUJO_VIG!r} (HTTP {est}): {filas}")
    fila = filas[0]
    cd = json.loads(fila["clientdata"] or "{}")
    definicion = cd.get("properties", {}).get("definition", cd)
    return {"statecode": fila["statecode"], "definicion": definicion}


def _leer_vig_repo():
    with open(RUTA_JSON_VIG, encoding="utf-8") as f:
        return json.load(f)


def _hace(minutos=None, dias=None):
    delta = datetime.timedelta(minutes=minutos) if minutos is not None else datetime.timedelta(days=dias)
    return (datetime.datetime.now(datetime.timezone.utc) - delta).strftime("%Y-%m-%dT%H:%M:%SZ")


def _filtro_bloque2(minutos):
    """Misma consulta que `Buscar_ingresadas_viejas` (bloque `Solicitudes_sin_validar`)."""
    return (f"sanic_estadoprocesamiento eq {INGRESADA} and sanic_requiererevision eq false "
            f"and sanic_fechaingresada lt {_hace(minutos=minutos)}")


def _filtro_bloque3(minutos):
    """Misma consulta que `Buscar_comunicaciones_pendientes` (bloque `Comunicaciones_sin_enviar`)."""
    u = _hace(minutos=minutos)
    return (f"sanic_fechaingresada lt {u} and (((sanic_estadoprocesamiento eq {ENPROC} or "
            f"sanic_estadoprocesamiento eq {RECH}) and sanic_fechaacuseiniciado eq null) or "
            f"(sanic_estadoprocesamiento eq {PROC} and sanic_fecharespuestafinaliniciada eq null))")


def _filtro_bloque4(minutos):
    """Misma consulta que `Buscar_envios_a_medias` (bloque `Envios_iniciados_sin_confirmar` — el
    bloque de M-06/V-07)."""
    u = _hace(minutos=minutos)
    return (f"sanic_requiererevision eq false and ((sanic_fechaacuseiniciado ne null and "
            f"sanic_fechaacuseenviado eq null and sanic_fechaacuseiniciado lt {u}) or "
            f"(sanic_fecharespuestafinaliniciada ne null and sanic_fecharespuestafinalenviada eq null "
            f"and sanic_fecharespuestafinaliniciada lt {u}))")


def _filtro_bloque5(dias):
    """Misma consulta que `Buscar_vencidos` (bloque `Por_clasificar_vencidos`)."""
    return (f"(sanic_estadoprocesamiento eq {NORECON} or sanic_estadoprocesamiento eq {NOESCORREO}) "
            f"and sanic_fecharecibido lt {_hace(dias=dias)}")


def _coincide(dv, sid, filtro):
    """`True` si la Solicitud `sid` aparece en la MISMA consulta que usa el bloque correspondiente
    de MPPP-VIG. Es la precondición decisiva de cada caso lento: si esto da `False`, el defecto es
    de la CONSULTA y no hace falta esperar el resto del margen para saberlo (la duda exacta que
    dejó abierta M-06 para el bloque 4 — ver V-07)."""
    est, r, _ = dv.call(
        "GET", f"{sim.SOLICITUDES}?$select=sanic_mppp_tbl_solicitudid"
               f"&$filter=({filtro}) and sanic_mppp_tbl_solicitudid eq {sid}")
    return est == 200 and bool(r.get("value"))


def _esperar_campo(ctx, sid, predicado, timeout, intervalo=20):
    """Poll sobre la Solicitud hasta que `predicado(fila)` sea verdadero, o se acabe `timeout`.
    Devuelve la última lectura (nunca `None` si al menos una lectura tuvo éxito), para que el caso
    pueda mostrar el estado real con el que se rindió."""
    t0 = time.monotonic()
    ultimo = None
    while True:
        ultimo = sim.leer_solicitud(ctx.dv, sid)
        if predicado(ultimo):
            return ultimo
        if time.monotonic() - t0 >= timeout:
            return ultimo
        time.sleep(intervalo)


def _na_lento(motivo_especifico):
    return (
        f"NO se corre de rutina: depende del recurrence real de MPPP-VIG (cada {RECURRENCE_MINUTOS} "
        f"minutos, confirmado en vivo por V-01). Con el margen de este set ({VUELTAS_MARGEN} vueltas "
        f"= {MARGEN_SEGUNDOS // 60} min), los 5 casos lentos juntos superan las dos horas: nadie "
        f"corre un set así de rutina (mismo criterio que set E con E-08). {motivo_especifico} Se "
        f"corre a propósito con: {BANDERA_LENTOS}=si python3 herramientas/pruebas/correr.py V")


def _lento(c):
    """`True` si hay que saltear el caso (bandera apagada). Ya deja el caso en N-A con su motivo."""
    if os.environ.get(BANDERA_LENTOS) == "si":
        return False
    c.na(_na_lento("La lógica de este caso vive DENTRO del flujo (un If/consulta de Power Automate), "
                   "no hay Custom API que la ejercite sin esperar el schedule real."))
    return True


# ===========================================================================
# V-01 — el flujo está vivo y bien armado (sin esperar el schedule)
# ===========================================================================
def v01(ctx):
    c = pc.Caso("V-01", "MPPP-VIG está vivo, con su recurrence real y su cadena de bloques bien armada")
    vivo = ctx.extra["vig_live"]
    repo = ctx.extra["vig_repo"]

    c.check("el flujo está Activado (statecode=1)", vivo["statecode"] == 1, f"statecode={vivo['statecode']}")

    trigger = vivo["definicion"].get("triggers", {}).get("Cada_10_minutos", {}) or {}
    rec = trigger.get("recurrence", {})
    c.check(f"la recurrencia real es cada {RECURRENCE_MINUTOS} minutos (leído en vivo: "
            f"frequency={rec.get('frequency')!r} interval={rec.get('interval')!r})",
            rec.get("frequency") == "Minute" and rec.get("interval") == RECURRENCE_MINUTOS, str(rec))

    # D-62 parte (b), corregido el 2026-09-28: Concurrency Control en 1 para que dos corridas de
    # MPPP-VIG no se superpongan reintentando el mismo backlog en simultáneo (evidencia de corridas
    # superpuestas en pruebas/07-vigilancia.md, sección "Investigación de la causa de fondo").
    runs = ((trigger.get("runtimeConfiguration") or {}).get("concurrency") or {}).get("runs")
    c.check(f"el trigger tiene Concurrency Control en 1 (D-62b; leído en vivo: runs={runs!r})",
            runs == 1, f"runtimeConfiguration={trigger.get('runtimeConfiguration')}")

    acts = vivo["definicion"].get("actions", {})
    BLOQUES = ["Correos_que_quedaron_en_la_bandeja", "Solicitudes_sin_validar",
               "Comunicaciones_sin_enviar", "Envios_iniciados_sin_confirmar", "Por_clasificar_vencidos"]
    for nombre in BLOQUES:
        c.check(f"el bloque {nombre!r} existe como acción de nivel superior", nombre in acts)

    # Encadenamiento bloque a bloque: TIENE que blindar con [Succeeded, Failed], nombrado par por
    # par (CONVENCIONES §7) — esto es lo que playbooks/flujo/mppp-vig.md promete.
    PARES = [("Correos_que_quedaron_en_la_bandeja", "Solicitudes_sin_validar"),
             ("Solicitudes_sin_validar", "Comunicaciones_sin_enviar"),
             ("Comunicaciones_sin_enviar", "Envios_iniciados_sin_confirmar"),
             ("Envios_iniciados_sin_confirmar", "Por_clasificar_vencidos")]
    for previo, siguiente in PARES:
        cond = set((acts.get(siguiente, {}).get("runAfter") or {}).get(previo, []))
        c.check(f"{siguiente!r} corre después de {previo!r} tanto si tuvo éxito como si falló",
                {"Succeeded", "Failed"} <= cond, f"runAfter[{previo!r}]={sorted(cond)}")

    # D-62, corregido el 2026-09-28: la etapa de arranque común de los 5 bloques tiene el mismo
    # blindaje que ya tenían los 5 bloques entre sí (hallazgo original de este set investigando por
    # qué M-06 no vio actuar a MPPP-VIG en ~22 min de espera real — razonamiento del alcance del
    # arreglo en playbooks/flujo/mppp-vig.md). Este check ahora verifica que el blindaje SIGUE
    # puesto; si alguien lo revierte (a mano en el portal, o reimportando una versión vieja), este
    # caso vuelve a FALLAR exactamente acá, sin esperar nada.
    CADENA_ARRANQUE = [("Parametro_sin_validar", "Parametro_sin_responder"),
                        ("Parametro_sin_responder", "Parametro_reintentos"),
                        ("Parametro_reintentos", "Parametro_vencimiento"),
                        ("Parametro_vencimiento", "Umbrales"),
                        ("Umbrales", "Correos_que_quedaron_en_la_bandeja")]
    for previo, siguiente in CADENA_ARRANQUE:
        cond = set((acts.get(siguiente, {}).get("runAfter") or {}).get(previo, []))
        c.check(
            f"{siguiente!r} corre después de {previo!r} tanto si tuvo éxito como si falló (D-62: si "
            f"{previo!r} falla en una corrida y esto no está blindado, los CINCO bloques de "
            "recuperación se saltean enteros esa vez, no solo el que falló)",
            {"Succeeded", "Failed"} <= cond, f"runAfter[{previo!r}]={sorted(cond)}")

    # Lo desplegado == el repo: no confiar en que el archivo versionado sea lo que corre de verdad.
    for nombre, accion_repo in repo.get("actions", {}).items():
        real = acts.get(nombre)
        c.check(f"la acción {nombre!r} del repo existe en el flujo desplegado", real is not None)
        if real is not None:
            c.check(f"{nombre!r}: el runAfter desplegado coincide con el del repo",
                    (real.get("runAfter") or {}) == (accion_repo.get("runAfter") or {}),
                    f"repo={accion_repo.get('runAfter')} vivo={real.get('runAfter')}")
            c.check(f"{nombre!r}: el type desplegado coincide con el del repo",
                    real.get("type") == accion_repo.get("type"),
                    f"repo={accion_repo.get('type')} vivo={real.get('type')}")

    # Cobertura barata de los 4 umbrales, nombrados uno por uno (CONVENCIONES §7: no "4 parámetros").
    for nombre in PARAMETROS_UMBRAL:
        c.check(f"el parámetro {nombre!r} tiene una versión activa (valor real leído: "
                f"{ctx.extra['umbrales'].get(nombre)!r})", nombre in ctx.extra["umbrales"])

    # Cobertura del CONTENIDO (no solo existencia) de retencion.dias.general y
    # retencion.dias.noreconocidas, nombrados uno por uno (CONVENCIONES §7): recuperan acá la
    # verificación que perdió el antiguo P-06 al disolver el set P — ver preparar().
    for nombre in PARAMETROS_RETENCION:
        c.check(f"el parámetro {nombre!r} está activo y es un entero positivo (valor real leído: "
                f"{ctx.extra['retencion'].get(nombre)!r})", nombre in ctx.extra["retencion"])

    return c


# ===========================================================================
# V-02 — bloque 1, correo en la bandeja. Manual A PROPÓSITO (ver docstring).
# ===========================================================================
def v02(ctx):
    c = pc.Caso("V-02", "Bloque 1 — un correo que quedó en la bandeja (MPPP-REC caído)")
    minutos = ctx.extra["umbrales"]["vigilancia.minutos.sinvalidar"]
    c.na(
        f"requiere APAGAR 'Cloud Flow - MPPP - REC - Recibir correo nuevo' de verdad, mandar un "
        f"correo real y esperar {minutos}+ minutos reales de MPPP-VIG (margen de {VUELTAS_MARGEN} "
        "vueltas si se quiere automatizar el resto). Este set, como ningún otro, NUNCA desactiva ni "
        "modifica un flujo de Power Automate: el service principal tiene privilegio de sobra "
        "(System Administrator) pero la regla del proyecto es no negociable. Procedimiento manual "
        "completo en pruebas/07-vigilancia.md, sección V-02; el resultado se anota a mano en la "
        "tabla de Resultados de ese archivo.")
    return c


# ===========================================================================
# V-03 — bloque 2, reintento de una Solicitud vieja en Ingresada.
# ===========================================================================
def v03(ctx):
    c = pc.Caso("V-03", "Bloque 2 — una Solicitud vieja en Ingresada se reintenta")
    if _lento(c):
        return c
    dv = ctx.dv
    minutos = ctx.extra["umbrales"]["vigilancia.minutos.sinvalidar"]

    sid, _mid = ctx.solicitud(remitente=REMITENTE, asunto="Prueba V-03 reintento",
                               cantidad_adjuntos=0, cantidad_excel=0)
    est, r, _ = dv.call("PATCH", f"{sim.SOLICITUDES}({sid})",
                         {"sanic_fechaingresada": _hace(minutos=minutos + 1)})
    c.check("se pudo envejecer sanic_fechaingresada por encima del umbral (precondición de datos)",
            est in (200, 204), f"HTTP {est}: {r}")

    c.check("precondición decisiva, sin esperar: la MISMA consulta que usa el bloque encuentra "
            "esta Solicitud (si esto es False, es un defecto de la consulta, no hace falta esperar)",
            _coincide(dv, sid, _filtro_bloque2(minutos)))

    antes = sim.leer_solicitud(dv, sid)
    c.check("sanic_reintentosvalidacion arranca vacío/0", not antes.get("sanic_reintentosvalidacion"))

    resultado = _esperar_campo(ctx, sid, lambda x: (x.get("sanic_reintentosvalidacion") or 0) >= 1,
                                timeout=MARGEN_SEGUNDOS)
    c.check(f"sanic_reintentosvalidacion pasó a 1 (margen: {VUELTAS_MARGEN} vueltas = "
            f"{MARGEN_SEGUNDOS // 60} min)",
            resultado and (resultado.get("sanic_reintentosvalidacion") or 0) >= 1, str(resultado))

    bit = sim.leer_bitacora(dv, sid)
    c.check("Bitácora: Reintento, origen MPPP-VIG",
            any(b["sanic_evento"] == EVT_REINTENTO and b.get("sanic_origen") == ORIGEN_VIG for b in bit))

    filas = sim.leer_filas(dv, sid)
    c.check("no se duplicaron filas", len(filas) in (0, 3), f"{len(filas)} filas")
    return c


# ===========================================================================
# V-04 — bloque 2, se acaban los reintentos.
# ===========================================================================
def v04(ctx):
    c = pc.Caso("V-04", "Bloque 2 — se acaban los reintentos: revisión humana, no reintento eterno")
    if _lento(c):
        return c
    dv = ctx.dv
    minutos = ctx.extra["umbrales"]["vigilancia.minutos.sinvalidar"]
    maximo = ctx.extra["umbrales"]["vigilancia.reintentos.maximo"]

    sid, _mid = ctx.solicitud(remitente=REMITENTE, asunto="Prueba V-04 reintentos agotados",
                               cantidad_adjuntos=0, cantidad_excel=0)
    est, r, _ = dv.call("PATCH", f"{sim.SOLICITUDES}({sid})",
                         {"sanic_fechaingresada": _hace(minutos=minutos + 1),
                          "sanic_reintentosvalidacion": maximo})
    c.check("se pudo envejecer la Solicitud y poner sanic_reintentosvalidacion al máximo (precondición)",
            est in (200, 204), f"HTTP {est}: {r}")

    c.check("precondición decisiva, sin esperar: la MISMA consulta de 'ingresadas viejas' encuentra "
            "esta Solicitud, ya con el máximo de reintentos sembrado",
            _coincide(dv, sid, _filtro_bloque2(minutos)))

    resultado = _esperar_campo(ctx, sid, lambda x: x.get("sanic_requiererevision") is True,
                                timeout=MARGEN_SEGUNDOS)
    c.check(f"sanic_requiererevision pasa a true (margen: {VUELTAS_MARGEN} vueltas = "
            f"{MARGEN_SEGUNDOS // 60} min)",
            bool(resultado and resultado.get("sanic_requiererevision")), str(resultado))

    c.check("NO se sumó un reintento nuevo (sanic_reintentosvalidacion sigue en el máximo)",
            resultado and (resultado.get("sanic_reintentosvalidacion") or 0) == maximo, str(resultado))

    motivo_esperado = f"Validacion fallida {maximo} veces."
    if resultado:
        c.check(f"sanic_motivorevision = {motivo_esperado!r} (el máximo real leído, no un número fijo)",
                resultado.get("sanic_motivorevision") == motivo_esperado,
                resultado.get("sanic_motivorevision"))

    bit = sim.leer_bitacora(dv, sid)
    c.check("no hay una entrada NUEVA de Bitácora 'Reintento' (tomó la rama de revisión, no la de reintentar)",
            not any(b["sanic_evento"] == EVT_REINTENTO for b in bit))
    # NO se verifica Bitácora "Requiere revision": confirmado que MPPP-VIG no la escribe en este
    # bloque (ver la sección de M-06 en el .md) — exigirla sería repetir el error de M-06.
    return c


# ===========================================================================
# V-05 — bloque 3, una comunicación que NUNCA se inició.
# ===========================================================================
def v05(ctx):
    c = pc.Caso("V-05", "Bloque 3 — una comunicación que nunca se inició")
    if _lento(c):
        return c
    dv = ctx.dv
    minutos = ctx.extra["umbrales"]["vigilancia.minutos.sinresponder"]

    sid, _mid, _rc, _rv = ctx.procesar(
        remitente=REMITENTE, asunto="Prueba V-05 comunicación nunca iniciada",
        cantidad_adjuntos=1, cantidad_excel=1, excel_a_subir=pc.excel("E01-feliz-formato11.xlsx"))

    estado = _esperar_campo(ctx, sid, lambda x: x.get("sanic_estadoprocesamiento") == ENPROC
                             and x.get("sanic_fechaacuseenviado"), timeout=180)
    c.check("precondición: la Solicitud llegó a En proceso con el acuse real ya enviado (así se llega "
            "a un estado que después se reescribe, igual que hace el set M en M-06)",
            estado and bool(estado.get("sanic_fechaacuseenviado")), str(estado))
    if not (estado and estado.get("sanic_fechaacuseenviado")):
        return c

    est, r, _ = dv.call("PATCH", f"{sim.SOLICITUDES}({sid})", {
        "sanic_fechaacuseiniciado": None, "sanic_fechaacuseenviado": None,
        "sanic_fechaingresada": _hace(minutos=minutos + 1)})
    c.check("se pudo reescribir el escenario 'nunca arrancó' (precondición de datos)",
            est in (200, 204), f"HTTP {est}: {r}")
    if est not in (200, 204):
        c.na(f"no se pudo preparar el escenario (HTTP {est}): {r}")
        return c

    c.check("precondición decisiva, sin esperar: la MISMA consulta que usa el bloque encuentra "
            "esta Solicitud", _coincide(dv, sid, _filtro_bloque3(minutos)))

    resultado = _esperar_campo(ctx, sid, lambda x: bool(x.get("sanic_fechaacuseenviado")),
                                timeout=MARGEN_SEGUNDOS)
    c.check(f"llega el acuse de nuevo: sanic_fechaacuseenviado vuelve a tener fecha (margen: "
            f"{VUELTAS_MARGEN} vueltas = {MARGEN_SEGUNDOS // 60} min)",
            resultado and bool(resultado.get("sanic_fechaacuseenviado")), str(resultado))

    bit = sim.leer_bitacora(dv, sid)
    c.check("hay una entrada NUEVA de Bitácora Acuse iniciado/enviado con origen MPPP-VIG (la del "
            "acuse original quedó con origen MPPP-COM/MPPP-ENV)",
            any(b.get("sanic_origen") == ORIGEN_VIG for b in bit), str(bit))

    c.check("sanic_requiererevision sigue en false (esto es recuperación transparente, no revisión)",
            resultado and resultado.get("sanic_requiererevision") is False, str(resultado))
    return c


# ===========================================================================
# V-06 — bloque 5, vencimiento de lo que nadie clasificó.
# ===========================================================================
def v06(ctx):
    c = pc.Caso("V-06", "Bloque 5 — los correos por clasificar vencen")
    if _lento(c):
        return c
    dv = ctx.dv
    dias = ctx.extra["umbrales"]["clasificacion.dias.vencimiento"]

    sid, _mid = ctx.solicitud(remitente="zz-remitente-no-reconocido@example.com",
                               asunto="Prueba V-06 vencimiento", cantidad_adjuntos=0, cantidad_excel=0)
    est, r, _ = dv.call("PATCH", f"{sim.SOLICITUDES}({sid})", {
        "sanic_estadoprocesamiento": NORECON, "sanic_fecharecibido": _hace(dias=dias + 1)})
    c.check("se pudo dejar la Solicitud en No reconocida, vieja (precondición de datos)",
            est in (200, 204), f"HTTP {est}: {r}")

    c.check("precondición decisiva, sin esperar: la MISMA consulta que usa el bloque encuentra "
            "esta Solicitud", _coincide(dv, sid, _filtro_bloque5(dias)))

    resultado = _esperar_campo(ctx, sid, lambda x: x.get("sanic_estadoprocesamiento") == VENCIDA,
                                timeout=MARGEN_SEGUNDOS)
    c.check(f"Estado pasa a Vencida (margen: {VUELTAS_MARGEN} vueltas = {MARGEN_SEGUNDOS // 60} min)",
            resultado and resultado.get("sanic_estadoprocesamiento") == VENCIDA, str(resultado))
    c.check("sanic_fechacerrada con fecha", resultado and bool(resultado.get("sanic_fechacerrada")))

    bit = sim.leer_bitacora(dv, sid)
    c.check("Bitácora: Vencida, origen MPPP-VIG",
            any(b["sanic_evento"] == EVT_VENCIDA and b.get("sanic_origen") == ORIGEN_VIG for b in bit))
    return c


# ===========================================================================
# V-07 — bloque 4, un envío se inició y nunca se confirmó. Cierra la pregunta
# abierta de M-06 (ver el docstring del módulo y pruebas/07-vigilancia.md).
# ===========================================================================
def v07(ctx):
    c = pc.Caso("V-07", "Bloque 4 — un envío se inició y nunca se confirmó (cierra M-06)")
    if _lento(c):
        return c
    dv = ctx.dv
    minutos = ctx.extra["umbrales"]["vigilancia.minutos.sinresponder"]

    sid, _mid, _rc, _rv = ctx.procesar(
        remitente=REMITENTE, asunto="Prueba V-07 envio sin confirmar",
        cantidad_adjuntos=1, cantidad_excel=1, excel_a_subir=pc.excel("E01-feliz-formato11.xlsx"))

    estado = _esperar_campo(ctx, sid, lambda x: x.get("sanic_estadoprocesamiento") == ENPROC
                             and x.get("sanic_fechaacuseenviado"), timeout=180)
    c.check("precondición: la Solicitud llegó a En proceso con el acuse real ya enviado",
            estado and bool(estado.get("sanic_fechaacuseenviado")), str(estado))
    if not (estado and estado.get("sanic_fechaacuseenviado")):
        return c

    est, r, _ = dv.call("PATCH", f"{sim.SOLICITUDES}({sid})", {
        "sanic_fechaacuseiniciado": _hace(minutos=minutos + 1), "sanic_fechaacuseenviado": None})
    c.check("se pudo reescribir el escenario 'a medias' (precondición de datos, igual que M-06)",
            est in (200, 204), f"HTTP {est}: {r}")
    if est not in (200, 204):
        c.na(f"no se pudo preparar el escenario (HTTP {est}): {r}")
        return c

    # LA PRECONDICIÓN DECISIVA para la pregunta abierta de M-06: si esto da False, el defecto es
    # de la consulta (hipótesis A) — sin gastar 32 minutos para saberlo.
    ok_consulta = _coincide(dv, sid, _filtro_bloque4(minutos))
    c.check("precondición decisiva, sin esperar nada: la consulta de Envios_iniciados_sin_confirmar "
            "(la MISMA que usa MPPP-VIG) encuentra esta Solicitud AHORA MISMO — si esto es False, el "
            "defecto es de la consulta (hipótesis A de M-06); si es True, cualquier miss posterior es "
            "de ejecución/programación (ver V-01: el hallazgo confirmado de la etapa de arranque)",
            ok_consulta)
    if not ok_consulta:
        return c

    resultado = _esperar_campo(ctx, sid, lambda x: x.get("sanic_requiererevision") is True,
                                timeout=MARGEN_SEGUNDOS)
    c.check(f"sanic_requiererevision pasa a true (margen: {VUELTAS_MARGEN} vueltas = "
            f"{MARGEN_SEGUNDOS // 60} min — M-06 probó 1 y 2 vueltas y no alcanzó; ver el .md)",
            bool(resultado and resultado.get("sanic_requiererevision")), str(resultado))

    if resultado and resultado.get("sanic_motivorevision"):
        c.check("sanic_motivorevision es el texto fijo del bloque",
                resultado["sanic_motivorevision"] == MOTIVO_V07, resultado["sanic_motivorevision"])

    c.check("sanic_fechaacuseenviado sigue vacío (no se completó un envío nuevo: no se reenvía)",
            resultado is not None and not resultado.get("sanic_fechaacuseenviado"))
    # NO se verifica Bitácora "Requiere revision": confirmado que MPPP-VIG no la escribe en ningún
    # bloque de revisión — exigirla era el error de M-06 (ver pruebas/07-vigilancia.md).
    return c


CASOS = [v01, v02, v03, v04, v05, v06, v07]
