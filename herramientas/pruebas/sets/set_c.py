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
"""
import os
import sys

_AQUI = os.path.dirname(os.path.abspath(__file__))
_HERRAMIENTAS_PRUEBAS = os.path.dirname(_AQUI)
sys.path.insert(0, _HERRAMIENTAS_PRUEBAS)

import preparacion_comun as pc  # noqa: E402
import simulador as sim  # noqa: E402
import preparar_datos as prep  # noqa: E402

LETRA = "C"
NOMBRE = "Clasificación del correo"

# Remitentes propios de este set. Nunca los de una persona real (CONVENCIONES
# §5): los mismos dos que ya usaba `corredor.py` (se migran sin cambiarlos,
# C-01..C-08 ya estaban probados contra ellos) más uno nuevo, exclusivo de
# las tres pruebas de DN-01.
REMITENTE = "zz-corredor-fase1@example.com"
REMITENTE_NO_AUTORIZADO = "zz-corredor-no-autorizado@example.com"
REMITENTE_DN01 = "zz-corredor-set-c-dn01-cliente@example.com"

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
    `correr.py`, no este `cerrar()`: ver `preparar()` más arriba)."""
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
    return errores


def _preparar_autorizacion_por_cliente(dv):
    """Create-if-missing: el Autorizado `REMITENTE_DN01` con una ÚNICA vía de
    autorización — `AutorizacionCliente` hacia el Cliente compartido
    `PR - Pruebas MPPP SA` (dueño de `PR11`, `PR06`, `PR10`, `PRIN`, `PRNA`;
    verificado hoy contra Dev) — y CERO `AutorizacionPlan` (nunca se le crea
    una: es un correo inventado exclusivamente para esto, no hay otro
    proceso que pueda tocarlo). Devuelve `(id_autorizado, id_autorizacioncliente)`
    para que `cerrar()` los borre por GUID."""
    cli = prep.uno(dv, "sanic_mppp_tbl_clientes", f"sanic_nombre eq '{prep.CLIENTE}'",
                   "sanic_mppp_tbl_clienteid")
    if cli is None:
        raise RuntimeError(
            f"el cliente compartido {prep.CLIENTE!r} no existe: verificar_datos_negocio() "
            "debería haberlo creado antes de llegar acá")
    idc = cli["sanic_mppp_tbl_clienteid"]

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
    c.na(
        "Necesita un correo REAL procesado por MPPP-REC/MPPP-ING (Power Automate: el disparador "
        "sondea cada minuto) contra el buzón compartido mppp@55xljh.onmicrosoft.com. "
        "`simulador.py` fabrica la Solicitud directo por Web API y nunca toca ese buzón (ver su "
        "docstring). NOVEDAD verificada hoy (2026-09-27): `herramientas/graph_api.py` ya manda y "
        "lee correo real (HTTP 202 al mandar, Mail.Read disponible), pero su Application Access "
        "Policy restringe el service principal a 4 buzones (`PP_MAIL_BUZON_1..4`) y NINGUNO de los "
        "4 es `mppp@55xljh.onmicrosoft.com` (comparado hoy, programáticamente, sin imprimir "
        "ninguna dirección: las 4 configuradas no calzan con el buzón de ingesta) — Graph no puede "
        "leer la carpeta `Procesados` de ESE buzón aunque el permiso exista en general. No es un "
        "caso de navegador: no hay pantalla de la app model-driven que mostrar (`pruebas/gui/` no "
        "aplica acá, es un tema de Exchange, no de Dataverse). Los dos flujos SÍ están activos hoy "
        "(REC y ING, statecode=1, verificado). Reproducción manual: mandar un correo real a "
        "mppp@55xljh.onmicrosoft.com, esperar 1-2 minutos, y confirmar A MANO —con acceso al buzón "
        "compartido— que el correo quedó en Procesados y que `ver_solicitud.py --ultima` muestra "
        "'outlook message id: sí'.")
    return c


def c06(ctx):
    c = pc.Caso("C-06", "El .eml y el Excel quedan guardados con su nombre real")
    c.na(
        "Mismo bloqueo de fondo que C-05: hace falta un correo REAL procesado por MPPP-ING para que "
        "`sanic_correocrudo`/`sanic_exceloriginal` tengan un nombre de archivo genuino que "
        "verificar — `simulador.py` sube el `.eml` con un nombre FIJO ('correo.eml'), así que "
        "verificarlo acá probaría al simulador, no al sistema (mismo criterio que ya usaba este "
        "caso). NOVEDAD verificada hoy: la verificación YA NO exige la app ni una sesión de "
        "navegador — Dataverse expone `sanic_correocrudo_name`/`sanic_exceloriginal_name` por Web "
        "API (`ver_solicitud.py` ya los lee para su línea 'archivos'), así que en cuanto exista una "
        "Solicitud de un correo real el chequeo es 'python3 herramientas/pruebas/ver_solicitud.py "
        "--ultima' (o un GET directo a esos dos campos) — sin browser. Lo único que falta de "
        "verdad es el correo real de C-05, no una herramienta de verificación.")
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
