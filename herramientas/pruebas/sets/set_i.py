#!/usr/bin/env python3
"""Set I — Integridad y plugins (`pruebas/08-integridad-y-plugins.md`).

Los steps de plugin que corren al guardar (la red que impide dejar el sistema
en un estado imposible), el catálogo de Reglas, las dos claves alternativas de
autorización (por plan y, desde fase 2, por cliente — DN-01) y la puerta de
las Custom API del RPA (D-9/D-44). El contrato completo de un módulo de set
está en `sets/__init__.py`; la lógica de preparación compartida, en
`preparacion_comun.py`.

**Migrado de `corredor.py` el 2026-09-27** (CONVENCIONES §8.1). Tres cosas
cambiaron respecto de la versión vieja, y las tres importan:

1. **I-05 e I-07 dejaron de citar otro archivo.** La versión vieja del MD
   decía "una autorización existente (P-03, ...)" (`00-preparacion.md`) y "una
   Fila existente (la de C-01 alcanza)" (`01-clasificacion-del-correo.md`) —
   la deuda que `pruebas/README.md` declaraba para este set. Los dos casos ya
   usaban en el CÓDIGO su propio fixture (`preparar()` de este mismo módulo);
   lo que estaba mal era solo la prosa del MD, que este archivo corrige.
2. **I-09 dejó de contar tipos.** `paquete_plugins.py --solo-verificar` YA
   compara por NOMBRE lo que falta y lo que sobra (`_verificar()`, D-57 c); acá
   además se nombran los 13 tipos esperados, uno por uno, contra
   `GET plugintypes` en vivo — no un conteo.
3. **Se agregaron I-10 a I-13** (fase 2): los 20 registros de step nombrados
   uno por uno (I-10), la clave alternativa nueva de `AutorizacionCliente`
   (I-11, DN-01), la puerta del RPA (I-12, D-9/D-44) y el step de claim (I-13,
   `03` §2) — verificado contra `playbooks/steps/sanic_mppp_pkg_plugins.md` y
   contra `sdkmessageprocessingsteps` EN VIVO el 2026-09-27.

**I-07 se reescribió el 2026-09-27: el diagnóstico viejo medía la identidad
equivocada.** La versión anterior hacía el `PATCH` prohibido con el service
principal de este set (`# PowerPlatform-Dev`, que tiene `applicationid`), y
`ListaBlancaStep` exime A PROPÓSITO a toda identidad de aplicación
(`BaseDeStep.EsCodigoDeServidor`, `diseno/04-matriz-privilegios.md` §1) —
igual que exime al código de servidor. El HTTP 204 que se registró como
"defecto" era el sistema haciendo exactamente lo que el diseño pide. La
pregunta que el caso tiene que responder es otra: **¿puede un HUMANO, con su
rol de Ejecutivo, hacer ese mismo `PATCH` saltándose el formulario?** Eso se
mide ahora impersonando con `MSCRMCallerID` (mismo patrón que I-13,
`comportamiento_roles.py`), y además se prueban los dos bordes: un rol sin
privilegio de escritura sobre Fila (Administrador de planes — tiene que
frenarlo la falta de privilegio, no la lista blanca) y una columna que sí
está permitida (`sanic_notainterna` — tiene que pasar).
"""
import datetime
import json
import os
import subprocess
import sys

_AQUI = os.path.dirname(os.path.abspath(__file__))
_HERRAMIENTAS_PRUEBAS = os.path.dirname(_AQUI)
_RAIZ = os.path.dirname(os.path.dirname(_HERRAMIENTAS_PRUEBAS))
sys.path.insert(0, _HERRAMIENTAS_PRUEBAS)

import preparacion_comun as pc  # noqa: E402
import preparar_datos as prep  # noqa: E402
import simulador as sim  # noqa: E402
from dataverse_api import Dataverse  # noqa: E402

LETRA = "I"
NOMBRE = "Integridad y plugins"

# Remitente propio de este set. Nunca el de una persona real (CONVENCIONES §5).
REMITENTE = "zz-corredor-fase1@example.com"

REGLAS = "sanic_mppp_tbl_reglas"
PLANES = "sanic_mppp_tbl_plans"
AUTORIZADOS = "sanic_mppp_tbl_autorizados"
CLIENTES = "sanic_mppp_tbl_clientes"
AUTORIZACIONPLANES = "sanic_mppp_tbl_autorizacionplans"
AUTORIZACIONCLIENTES = "sanic_mppp_tbl_autorizacionclientes"

NIVEL_REGISTRO = 159460002
EFECTO_RECHAZA = 159460001
EFECTO_ENVIA_REVISION = 159460002

VALIDADA, DIGITADA, APROBADA = 159460003, 159460004, 159460005


# ===========================================================================
# preparar / cerrar
# ===========================================================================
def preparar(ctx):
    """Deja listo TODO lo que el set I necesita, y nada más (CONVENCIONES §8):

    - **Datos de negocio** (fixture compartido `PR*`, create-if-missing, y el
      `REMITENTE` de este set autorizado sobre PR11/PR06/PR10/PRIN — a PRNA
      NO, a propósito) — de acá salen la autorización que I-05 duplica, el
      Cliente y el Plan que I-06 usa para el nombre calculado, y el plan PRNA
      que I-06 usa para probar una AutorizacionPlan nueva.
    - **Los 15 Excel + el adjunto que no es Excel**, para I-06, I-07 e I-13,
      que crean su propia Solicitud/Fila con `ctx.procesar()`.

    **Lo que NO prepara, y por qué:**

    - **Los roles de Ejecutivo/Supervisor (User1/User2)**: solo I-13 los
      necesita (para llegar a una transición LEGÍTIMA de Fila y ejercitar el
      step de claim), y si un día esos roles cambiaran, el resto de los 12
      casos de este set no tiene por qué dejar de correr. Se resuelven y
      verifican DENTRO de I-13 (`_usuario_por_alias` + `pc.verificar_roles`),
      no acá.
    - **El canon de configuración global** (§8.2). Lo aplica `correr.py` antes
      y después de CADA set, no cada set por separado.
    """
    pc.verificar_datos_negocio(ctx.dv, REMITENTE)
    pc.verificar_excels()


def cerrar(ctx):
    """Borra, por GUID, las Solicitudes que este set creó (I-06, I-07, I-13).
    Las reglas del catálogo que I-02/I-03/I-04 tocan se revierten SOLAS, en el
    propio caso (`finally`), porque son filas del catálogo COMPARTIDO
    (`PLAN_EXISTE`, `MONEDA_DEL_PLAN`) y tienen que quedar bien pase lo que
    pase dentro del caso mismo, no recién al final del set. No toca el
    fixture compartido `PR*`: eso no se borra nunca desde un set."""
    return ctx.limpiar_solicitudes()


# ===========================================================================
# Helpers locales (no viven en preparacion_comun.py porque solo los necesita
# este set; se reportan acá, como pide la consigna)
# ===========================================================================
def _regla(ctx, codigo):
    est, r, _ = ctx.dv.call(
        "GET",
        f"{REGLAS}?$select=sanic_mppp_tbl_reglaid,sanic_codigo,sanic_nivel,sanic_efecto,"
        f"sanic_orden,sanic_dependede,statecode&$filter=sanic_codigo eq '{codigo}'")
    if est != 200 or not r.get("value"):
        raise RuntimeError(f"no se encontró la regla {codigo!r} (HTTP {est}): {r}")
    return r["value"][0]


def _usuario_por_alias(dv, alias):
    """El `systemuserid` de un usuario de prueba por su alias de dominio
    (`User1`, `User2`, ...), igual que resuelve `comportamiento_roles.py`
    (CONVENCIONES §6). Local a este módulo: I es el único set migrado que
    necesita impersonar un rol de negocio real para llegar hasta el step de
    claim (I-13, `03` §2) — ese step solo se ejercita con una transición de
    Fila LEGÍTIMA (Ejecutivo/Supervisor), nunca con el service principal (que
    tiene System Administrator pero ningún rol de negocio de MPPP: verificado
    en vivo el 2026-09-27, `WhoAmI` + `systemuserroles_association` = solo
    `['System Administrator']`)."""
    est, r, _ = dv.call(
        "GET",
        "systemusers?$select=systemuserid,fullname"
        f"&$filter=startswith(domainname,'{alias}@') and applicationid eq null")
    filas = r.get("value", []) if est == 200 else []
    if len(filas) != 1:
        raise RuntimeError(f"{alias!r} no identifica un usuario único en el entorno: {filas}")
    return filas[0]["systemuserid"]


# ===========================================================================
# Casos I-01 .. I-04 — el catálogo de Reglas (IntegridadDeReglaStep)
# ===========================================================================
def i01(ctx):
    c = pc.Caso("I-01", "El catálogo de reglas: una regla activa sin evaluador")
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


def i02(ctx):
    c = pc.Caso("I-02", "Una regla que depende de otra inactiva")
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


def i03(ctx):
    c = pc.Caso("I-03", 'Una regla de nivel Registro con efecto "Envía a revisión"')
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


def i04(ctx):
    c = pc.Caso("I-04", "Una regla que depende de sí misma")
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


# ===========================================================================
# I-05 — clave alternativa de AutorizacionPlan (fase 1)
# ===========================================================================
def i05(ctx):
    c = pc.Caso("I-05", "Integridad de una autorización por PLAN (duplicada)")
    # El REMITENTE de este set ya tiene autorización a PR11 (preparar() → preparacion_comun.verificar_datos_negocio).
    pl = ctx.dv.call("GET", f"{PLANES}?$select=sanic_mppp_tbl_planid&$filter=sanic_codigo eq 'PR11'")[1]["value"][0]
    au = ctx.dv.call("GET", f"{AUTORIZADOS}?$select=sanic_mppp_tbl_autorizadoid&$filter=sanic_nombre eq '{REMITENTE}'")[1]["value"][0]
    cuerpo = {"sanic_nombre": f"ZZ duplicada {REMITENTE} → PR11",
              "sanic_autorizadoid@odata.bind": f"/{AUTORIZADOS}({au['sanic_mppp_tbl_autorizadoid']})",
              "sanic_planid@odata.bind": f"/{PLANES}({pl['sanic_mppp_tbl_planid']})"}
    est, r, cab = ctx.dv.call("POST", AUTORIZACIONPLANES, cuerpo)
    if est in (200, 201, 204):
        ctx.dv.call("DELETE", f"{AUTORIZACIONPLANES}({Dataverse.id_creado(cab)})")
        c.check("no deja guardar una AutorizacionPlan duplicada (mismo autorizado, mismo plan)", False,
                f"SE GUARDÓ (HTTP {est}) y se borró de inmediato: la clave alternativa "
                "sanic_mppp_key_autorizacionplan_autorizado_plan no está frenando el duplicado")
    else:
        c.check("no deja guardar una AutorizacionPlan duplicada (mismo autorizado, mismo plan)", True,
                f"HTTP {est}: {r.get('error')}")
        c.check("el mensaje no es un volcado técnico crudo de la plataforma", "Microsoft.Crm.CrmException" not in str(r.get("error")))
    return c


# ===========================================================================
# I-06 — NombreCalculadoStep, las 4 tablas que cubre HOY, nombradas una por
# una (CONVENCIONES §7: "nombrar, no contar" — no dice "4 tablas", nombra
# Plan, AutorizacionPlan, AutorizacionCliente y Fila).
# ===========================================================================
def i06(ctx):
    c = pc.Caso("I-06", "El nombre calculado: Plan, AutorizacionPlan, AutorizacionCliente y Fila")

    cli = ctx.dv.call("GET", f"{CLIENTES}?$select=sanic_mppp_tbl_clienteid&$filter=sanic_nombre eq '{prep.CLIENTE}'")[1]["value"][0]
    au = ctx.dv.call("GET", f"{AUTORIZADOS}?$select=sanic_mppp_tbl_autorizadoid&$filter=sanic_nombre eq '{REMITENTE}'")[1]["value"][0]

    # --- Plan: 'CODIGO - Cliente' ---
    est, r, cab = ctx.dv.call("POST", PLANES, {
        "sanic_codigo": "ZZNM", "sanic_tipoformato": 159460001, "sanic_moneda": 159460001,
        "sanic_clienteid@odata.bind": f"/{CLIENTES}({cli['sanic_mppp_tbl_clienteid']})"})
    if est in (200, 201, 204):
        idp = Dataverse.id_creado(cab)
        _, r2, _ = ctx.dv.call("GET", f"{PLANES}({idp})?$select=sanic_nombre")
        ctx.dv.call("DELETE", f"{PLANES}({idp})")
        esperado = f"ZZNM - {prep.CLIENTE}"
        c.check("Plan: nombre calculado 'CODIGO - Cliente'", r2.get("sanic_nombre") == esperado, f"obtenido {r2.get('sanic_nombre')!r}")
    else:
        c.check("se pudo crear el Plan sin nombre para probar el cálculo", False, f"HTTP {est}: {r}")

    # --- AutorizacionPlan: 'correo → CODIGO del plan' (PRNA: NO autorizado a propósito por preparar_datos.py) ---
    pl = ctx.dv.call("GET", f"{PLANES}?$select=sanic_mppp_tbl_planid&$filter=sanic_codigo eq 'PRNA'")[1]["value"][0]
    est, r, cab = ctx.dv.call("POST", AUTORIZACIONPLANES, {
        "sanic_autorizadoid@odata.bind": f"/{AUTORIZADOS}({au['sanic_mppp_tbl_autorizadoid']})",
        "sanic_planid@odata.bind": f"/{PLANES}({pl['sanic_mppp_tbl_planid']})"})
    if est in (200, 201, 204):
        idr = Dataverse.id_creado(cab)
        _, r2, _ = ctx.dv.call("GET", f"{AUTORIZACIONPLANES}({idr})?$select=sanic_nombre")
        ctx.dv.call("DELETE", f"{AUTORIZACIONPLANES}({idr})")
        esperado = f"{REMITENTE} → PRNA"
        c.check("AutorizacionPlan: nombre calculado 'correo → CODIGO del plan'",
                r2.get("sanic_nombre") == esperado, f"obtenido {r2.get('sanic_nombre')!r}")
    else:
        c.check("se pudo crear la AutorizacionPlan sin nombre para probar el cálculo", False, f"HTTP {est}: {r}")

    # --- AutorizacionCliente (fase 2, DN-01): 'correo → NOMBRE del cliente' (no el código: no tiene) ---
    est, r, cab = ctx.dv.call("POST", AUTORIZACIONCLIENTES, {
        "sanic_autorizadoid@odata.bind": f"/{AUTORIZADOS}({au['sanic_mppp_tbl_autorizadoid']})",
        "sanic_clienteid@odata.bind": f"/{CLIENTES}({cli['sanic_mppp_tbl_clienteid']})"})
    if est in (200, 201, 204):
        idac = Dataverse.id_creado(cab)
        _, r2, _ = ctx.dv.call("GET", f"{AUTORIZACIONCLIENTES}({idac})?$select=sanic_nombre")
        ctx.dv.call("DELETE", f"{AUTORIZACIONCLIENTES}({idac})")
        esperado = f"{REMITENTE} → {prep.CLIENTE}"
        c.check("AutorizacionCliente: nombre calculado 'correo → NOMBRE del cliente' (DN-01, fase 2)",
                r2.get("sanic_nombre") == esperado, f"obtenido {r2.get('sanic_nombre')!r}")
    else:
        c.check("se pudo crear la AutorizacionCliente sin nombre para probar el cálculo", False, f"HTTP {est}: {r}")

    # --- Fila: '<numero de solicitud>-F<NN>' ---
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba I-06 nombre de fila",
                                     cantidad_adjuntos=1, cantidad_excel=1,
                                     excel_a_subir=pc.excel("E01-feliz-formato11.xlsx"))
    s = sim.leer_solicitud(ctx.dv, sid)
    filas = sim.leer_filas(ctx.dv, sid)
    numero = s.get("sanic_nombre")
    if not filas:
        c.check("se generó al menos una Fila para probar su nombre calculado", False, f"Solicitud {sid} sin filas")
    for f in filas:
        esperado = f"{numero}-F{f['sanic_numerofila']:02d}"
        c.check(f"Fila {f['sanic_numerofila']}: nombre calculado '<solicitud>-F{f['sanic_numerofila']:02d}'",
                f.get("sanic_nombre") == esperado, f"obtenido {f.get('sanic_nombre')!r}")
    return c


# ===========================================================================
# I-07 — ListaBlancaStep. Reescrito el 2026-09-27 (ver docstring del módulo):
# la identidad que importa es la de un HUMANO, no la del service principal
# (exento por diseño). Prueba la identidad correcta y los dos bordes: el rol
# sin privilegio de escritura sobre Fila, y la columna que SÍ está permitida.
# ===========================================================================
def i07(ctx):
    c = pc.Caso("I-07", "Lista blanca de escrituras: identidad exenta vs. un humano (Ejecutivo), y sus dos bordes")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba I-07", cantidad_adjuntos=1, cantidad_excel=1,
                                     excel_a_subir=pc.excel("E01-feliz-formato11.xlsx"))
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

    # --- El service principal de este set (identidad de aplicación, `applicationid`) está EXENTO de la lista
    # blanca A PROPÓSITO (BaseDeStep.EsCodigoDeServidor, diseno/04 §1). Esto NO mide la lista blanca: queda acá,
    # documentado, para que no se repita el diagnóstico viejo (confundir "exento por diseño" con "defecto").
    est_sp, r_sp, _ = ctx.dv.call("PATCH", f"{sim.FILAS}({fid})", {"sanic_referencia": "ZZ-INTENTO-SP"})
    c.check("el service principal (identidad de aplicación, exenta por diseño, 04 §1) SÍ puede escribir "
            "sanic_referencia — esperado, y NO es lo que este caso tiene que medir",
            est_sp == 204, f"HTTP {est_sp}: {r_sp.get('error') if est_sp != 204 else '(sin error, esperado)'}")

    # --- La pregunta correcta: ¿puede un HUMANO, con su rol de Ejecutivo, hacer el mismo PATCH saltándose el
    # formulario? Se impersona con MSCRMCallerID (mismo patrón que I-13 y comportamiento_roles.py) y se verifica
    # el rol EN VIVO: sin eso, el resultado mentiría sobre lo que en realidad se probó.
    try:
        ejecutivo_id = _usuario_por_alias(ctx.dv, "User1")
        admin_planes_id = _usuario_por_alias(ctx.dv, "User3")
    except RuntimeError as e:
        c.na(f"no se pudo resolver User1/User3 en el entorno: {e}")
        return c
    ok_ej, roles_ej = pc.verificar_roles(ctx.dv, ejecutivo_id, ["SR - MPPP - Ejecutivo"])
    if not ok_ej:
        c.na(f"User1 no tiene el rol Ejecutivo que este caso asume (tiene {roles_ej}); con otro rol el resultado "
             "no mediría lo que este caso dice medir")
        return c

    est_h, r_h, _ = ctx.dv.call("PATCH", f"{sim.FILAS}({fid})", {"sanic_referencia": "ZZ-INTENTO-DIRECTO"},
                                 cabeceras={"MSCRMCallerID": ejecutivo_id})
    c.check("un Ejecutivo (humano, sin exención) NO puede escribir sanic_referencia por Web API (ListaBlancaStep, 04 §1)",
            est_h == 400 and "sanic_referencia" in str(r_h.get("error")), f"HTTP {est_h}: {r_h.get('error')}")

    # --- Borde 1: un rol SIN privilegio de escritura sobre Fila (Administrador de planes, 04 §2). Tiene que
    # frenarlo la falta de privilegio, no la lista blanca: son dos protecciones distintas y conviene saber cuál actúa.
    ok_ap, roles_ap = pc.verificar_roles(ctx.dv, admin_planes_id, ["SR - MPPP - Administrador de planes"])
    if ok_ap:
        est_ap, r_ap, _ = ctx.dv.call("PATCH", f"{sim.FILAS}({fid})", {"sanic_notainterna": "prueba de borde I-07"},
                                       cabeceras={"MSCRMCallerID": admin_planes_id})
        c.check("el Administrador de planes no puede escribir NADA en Fila (falta de privilegio, 04 §2 — "
                "no llega a la lista blanca)", est_ap in (401, 403),
                f"HTTP {est_ap}: {r_ap.get('error') if isinstance(r_ap, dict) else r_ap}")
    else:
        c.na(f"User3 no tiene el rol Administrador de planes que este borde asume (tiene {roles_ap})")

    # --- Borde 2: una columna que SÍ está permitida (sanic_notainterna, ReglasDeSteps.PermitidasEnFila) tiene
    # que dejar pasar al mismo Ejecutivo. Si esta también se rechazara, el step sería más restrictivo que lo
    # que dice el diseño, y eso también sería un hallazgo.
    est_ok, r_ok, _ = ctx.dv.call("PATCH", f"{sim.FILAS}({fid})", {"sanic_notainterna": "prueba I-07 columna permitida"},
                                   cabeceras={"MSCRMCallerID": ejecutivo_id})
    c.check("un Ejecutivo SÍ puede escribir sanic_notainterna (columna permitida en la lista blanca, 04 §1)",
            est_ok == 204, f"HTTP {est_ok}: {r_ok.get('error') if est_ok != 204 else ''}")

    return c


# ===========================================================================
# I-08 — TransicionDeFilaStep: un salto que NO existe en la tabla de
# transiciones se rechaza SIEMPRE, sea cual sea el rol de quien lo intenta
# (Dominio/TransicionesDeFila.cs: (Validada, Aprobada) no es una clave de la
# tabla → "Transición no permitida", antes de mirar ningún rol).
# ===========================================================================
def i08(ctx):
    c = pc.Caso("I-08", "Transición de estado ilegal (Validada → Aprobada, saltando Digitada)")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba I-08", cantidad_adjuntos=1, cantidad_excel=1,
                                     excel_a_subir=pc.excel("E01-feliz-formato11.xlsx"))
    filas = sim.leer_filas(ctx.dv, sid)
    if not filas:
        c.na("no se generó ninguna Fila Validada para probar la transición")
        return c
    est, r, _ = ctx.dv.call("GET", f"{sim.FILAS}?$select=sanic_mppp_tbl_filaid&$filter=sanic_numerofila eq {filas[0]['sanic_numerofila']}"
                                    f" and _sanic_solicitudid_value eq {sid}")
    fid = r["value"][0]["sanic_mppp_tbl_filaid"]
    est, r, _ = ctx.dv.call("PATCH", f"{sim.FILAS}({fid})", {"sanic_estado": APROBADA})  # saltando Digitada
    c.check("Validada → Aprobada directo es rechazado (TransicionDeFilaStep)", est == 400, f"HTTP {est}: {r.get('error')}")
    return c


# ===========================================================================
# I-09 — El paquete de plugins al día: 13 tipos, NOMBRADOS uno por uno contra
# `plugintypes` en vivo (no un conteo).
# ===========================================================================
TIPOS_ESPERADOS = {
    "Sanic.Mppp.Plugins.Steps.ListaBlancaStep",
    "Sanic.Mppp.Plugins.Steps.NormalizarYValidarStep",
    "Sanic.Mppp.Plugins.Steps.NombreCalculadoStep",
    "Sanic.Mppp.Plugins.Steps.TransicionDeFilaStep",
    "Sanic.Mppp.Plugins.Steps.PostTransicionDeFilaStep",
    "Sanic.Mppp.Plugins.Steps.AtenderPorClasificarStep",
    "Sanic.Mppp.Plugins.Steps.RevisionAtendidaStep",
    "Sanic.Mppp.Plugins.Steps.IntegridadDeReglaStep",
    "Sanic.Mppp.Plugins.Api.ClasificarCorreoApi",
    "Sanic.Mppp.Plugins.Api.ExportarParaDigitarApi",
    "Sanic.Mppp.Plugins.Api.ObtenerFilasPendientesApi",
    "Sanic.Mppp.Plugins.Api.RegistrarResultadoRpaApi",
    "Sanic.Mppp.Plugins.Api.ValidarSolicitudApi",
}


def i09(ctx):
    c = pc.Caso("I-09", "El paquete de plugins está al día (13 tipos, nombrados uno por uno)")
    proc = subprocess.run(
        [sys.executable, os.path.join(_RAIZ, "herramientas", "construir", "paquete_plugins.py"),
         os.path.join(_RAIZ, "playbooks", "paquete-plugins", "sanic_mppp_pkg_plugins.md"), "--solo-verificar"],
        cwd=_RAIZ, capture_output=True, text=True, timeout=120)
    ultima = (proc.stdout.strip().splitlines() or [""])[-1]
    try:
        datos = json.loads(ultima)
    except Exception:
        c.check("paquete_plugins.py --solo-verificar corrió y devolvió JSON", False,
                f"rc={proc.returncode} stdout={proc.stdout[-400:]!r} stderr={proc.stderr[-400:]!r}")
        return c
    c.check("estado = ya_existia (paquete_plugins.py ya compara por NOMBRE lo que falta y lo que sobra, D-57 c)",
            datos.get("estado") == "ya_existia", str(datos))

    est, r, _ = ctx.dv.call("GET", "plugintypes?$select=typename&$filter=startswith(typename,'Sanic.Mppp.Plugins.')")
    registrados = {t["typename"] for t in r.get("value", [])} if est == 200 else set()
    for tipo in sorted(TIPOS_ESPERADOS):
        c.check(f"el tipo {tipo!r} está registrado", tipo in registrados)
    sobran = registrados - TIPOS_ESPERADOS
    c.check("no hay tipos de más sin declarar (nada sobra fuera de los 13 nombrados)", not sobran, f"sobran: {sorted(sobran)}")
    return c


# ===========================================================================
# I-10 (fase 2) — Los 20 registros de step, NOMBRADOS uno por uno, con su
# mensaje, su tabla y su etapa contra `sdkmessageprocessingsteps` en vivo.
# Reemplaza a la vieja tabla de prosa "8 steps registrados" (CONVENCIONES §7).
# ===========================================================================
STEPS_ESPERADOS = [
    # (nombre del step, mensaje, tabla, etapa: 20=PreOperation, 40=PostOperation)
    ("MPPP - Lista blanca - Update de Fila", "Update", "sanic_mppp_tbl_fila", 20),
    ("MPPP - Lista blanca - Update de Solicitud", "Update", "sanic_mppp_tbl_solicitud", 20),
    ("MPPP - Normalizar y validar - Create de Cliente", "Create", "sanic_mppp_tbl_cliente", 20),
    ("MPPP - Normalizar y validar - Update de Cliente", "Update", "sanic_mppp_tbl_cliente", 20),
    ("MPPP - Normalizar y validar - Create de Plan", "Create", "sanic_mppp_tbl_plan", 20),
    ("MPPP - Normalizar y validar - Update de Plan", "Update", "sanic_mppp_tbl_plan", 20),
    ("MPPP - Normalizar y validar - Create de Autorizado", "Create", "sanic_mppp_tbl_autorizado", 20),
    ("MPPP - Normalizar y validar - Update de Autorizado", "Update", "sanic_mppp_tbl_autorizado", 20),
    ("MPPP - Normalizar y validar - Create de Parametro", "Create", "sanic_mppp_tbl_parametro", 20),
    ("MPPP - Normalizar y validar - Update de Parametro", "Update", "sanic_mppp_tbl_parametro", 20),
    ("MPPP - Nombre calculado - Create de Plan", "Create", "sanic_mppp_tbl_plan", 20),
    ("MPPP - Nombre calculado - Create de Autorizacionplan", "Create", "sanic_mppp_tbl_autorizacionplan", 20),
    ("MPPP - Nombre calculado - Create de Autorizacioncliente", "Create", "sanic_mppp_tbl_autorizacioncliente", 20),
    ("MPPP - Nombre calculado - Create de Fila", "Create", "sanic_mppp_tbl_fila", 20),
    ("MPPP - Transicion de Fila - Update", "Update", "sanic_mppp_tbl_fila", 20),
    ("MPPP - Post transicion de Fila - Update", "Update", "sanic_mppp_tbl_fila", 40),
    ("MPPP - Atender correo por clasificar - Update de Solicitud", "Update", "sanic_mppp_tbl_solicitud", 20),
    ("MPPP - Revision atendida - Update de Solicitud", "Update", "sanic_mppp_tbl_solicitud", 20),
    ("MPPP - Integridad de Regla - Create", "Create", "sanic_mppp_tbl_regla", 20),
    ("MPPP - Integridad de Regla - Update", "Update", "sanic_mppp_tbl_regla", 20),
]


def i10(ctx):
    c = pc.Caso("I-10", "Los 20 registros de step de plugin, uno por uno (mensaje, etapa y tabla)")
    for nombre, mensaje, tabla, etapa in STEPS_ESPERADOS:
        est, r, _ = ctx.dv.call(
            "GET",
            f"sdkmessageprocessingsteps?$select=stage,statecode&$filter=name eq '{nombre}'"
            "&$expand=sdkmessageid($select=name),sdkmessagefilterid($select=primaryobjecttypecode)")
        filas = r.get("value", []) if est == 200 else []
        if not filas:
            c.check(f"{nombre!r} está registrado", False, f"no aparece (HTTP {est})")
            continue
        fila = filas[0]
        msg_real = (fila.get("sdkmessageid") or {}).get("name")
        tabla_real = (fila.get("sdkmessagefilterid") or {}).get("primaryobjecttypecode")
        c.check(
            f"{nombre!r}: mensaje={mensaje}, tabla={tabla}, etapa={etapa}, habilitado",
            msg_real == mensaje and tabla_real == tabla and fila.get("stage") == etapa and fila.get("statecode") == 0,
            f"obtenido mensaje={msg_real!r} tabla={tabla_real!r} etapa={fila.get('stage')!r} statecode={fila.get('statecode')!r}")
    return c


# ===========================================================================
# I-11 (fase 2, DN-01) — clave alternativa nueva de AutorizacionCliente.
# ===========================================================================
def i11(ctx):
    c = pc.Caso("I-11", "Integridad de una autorización por CLIENTE (duplicada, fase 2 DN-01)")
    cli = ctx.dv.call("GET", f"{CLIENTES}?$select=sanic_mppp_tbl_clienteid&$filter=sanic_nombre eq '{prep.CLIENTE}'")[1]["value"][0]
    au = ctx.dv.call("GET", f"{AUTORIZADOS}?$select=sanic_mppp_tbl_autorizadoid&$filter=sanic_nombre eq '{REMITENTE}'")[1]["value"][0]
    cuerpo = {
        "sanic_autorizadoid@odata.bind": f"/{AUTORIZADOS}({au['sanic_mppp_tbl_autorizadoid']})",
        "sanic_clienteid@odata.bind": f"/{CLIENTES}({cli['sanic_mppp_tbl_clienteid']})"}
    est1, r1, cab1 = ctx.dv.call("POST", AUTORIZACIONCLIENTES, cuerpo)
    if est1 not in (200, 201, 204):
        c.check("se pudo crear la PRIMERA AutorizacionCliente (precondición de este caso)", False, f"HTTP {est1}: {r1}")
        return c
    id1 = Dataverse.id_creado(cab1)
    try:
        est2, r2, cab2 = ctx.dv.call("POST", AUTORIZACIONCLIENTES, cuerpo)
        if est2 in (200, 201, 204):
            ctx.dv.call("DELETE", f"{AUTORIZACIONCLIENTES}({Dataverse.id_creado(cab2)})")
            c.check("no deja guardar una AutorizacionCliente duplicada (mismo autorizado, mismo cliente)", False,
                    f"SE GUARDÓ una segunda (HTTP {est2}) y se borró de inmediato: la clave alternativa "
                    "sanic_mppp_key_autorizacioncliente_autorizado_cliente no está frenando el duplicado")
        else:
            c.check("no deja guardar una AutorizacionCliente duplicada (mismo autorizado, mismo cliente)", True,
                    f"HTTP {est2}: {r2.get('error')}")
            c.check("el mensaje no es un volcado técnico crudo de la plataforma", "Microsoft.Crm.CrmException" not in str(r2.get("error")))
    finally:
        ctx.dv.call("DELETE", f"{AUTORIZACIONCLIENTES}({id1})")
    return c


# ===========================================================================
# I-12 (fase 2, D-9/D-44) — la puerta de las Custom API del RPA.
# ===========================================================================
def i12(ctx):
    c = pc.Caso("I-12", "La puerta del RPA rechaza a quien no es el bot (PuertaDelRpa, D-9/D-44)")
    est, r, _ = ctx.dv.call("GET", "sanic_mppp_capi_obtenerfilaspendientes(claimminutos=5)")
    c.check(
        "la Custom API del RPA rechaza a un llamador que no tiene el rol 'SR - MPPP - RPA' "
        "(este set corre con el service principal de Dev: SÍ es una identidad de aplicación, pero "
        "solo tiene 'System Administrator' — verificado en vivo, sin ese rol de negocio; es exactamente "
        "el caso que la puerta tiene que frenar, y no toca ninguna Fila si frena bien)",
        est == 400, f"HTTP {est}: {r}")
    texto = str(r.get("error") or "")
    c.check("el motivo nombra el código MPPP-RPA-006 o MPPP-RPA-007 (D-44: nunca un \"no autorizado\" genérico)",
            "MPPP-RPA-006" in texto or "MPPP-RPA-007" in texto, texto)
    return c


# ===========================================================================
# I-13 (fase 2, `03` §2) — el step de claim: no se toca en un estado que
# sigue abierto, se libera en un estado terminal.
# ===========================================================================
def i13(ctx):
    c = pc.Caso("I-13", "El claim se libera en un estado terminal, y no antes (03 §2)")
    try:
        ejecutivo_id = _usuario_por_alias(ctx.dv, "User1")
        supervisor_id = _usuario_por_alias(ctx.dv, "User2")
    except RuntimeError as e:
        c.na(f"no se pudo resolver User1/User2 en el entorno: {e}")
        return c
    ok_ej, roles_ej = pc.verificar_roles(ctx.dv, ejecutivo_id, ["SR - MPPP - Ejecutivo"])
    ok_su, roles_su = pc.verificar_roles(ctx.dv, supervisor_id, ["SR - MPPP - Supervisor"])
    if not (ok_ej and ok_su):
        c.na(f"User1/User2 no tienen los roles que este caso asume (User1={roles_ej}, User2={roles_su}); "
             "sin esos roles el caso daría 400 por falta de rol, no por el claim, y el resultado mentiría")
        return c

    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba I-13 claim", cantidad_adjuntos=1,
                                     cantidad_excel=1, excel_a_subir=pc.excel("E01-feliz-formato11.xlsx"))
    filas = sim.leer_filas(ctx.dv, sid)
    if not filas:
        c.na("no se generó ninguna Fila para probar el claim")
        return c
    est, r, _ = ctx.dv.call("GET", f"{sim.FILAS}?$select=sanic_mppp_tbl_filaid&$filter=sanic_numerofila eq {filas[0]['sanic_numerofila']}"
                                    f" and _sanic_solicitudid_value eq {sid}")
    fid = r["value"][0]["sanic_mppp_tbl_filaid"]

    # `sanic_claimhasta` NO está en la lista blanca de una persona (ReglasDeSteps.PermitidasEnFila):
    # solo lo escribe código de servidor. Un PATCH directo del service principal (identidad de
    # aplicación) lo deja en un pasado reciente — YA VENCIDO, para no bloquear con el claim la
    # transición legítima que viene después (03 §2: "el claim vigente frena a las personas").
    pasado = (datetime.datetime.now(datetime.timezone.utc) - datetime.timedelta(minutes=5)).strftime("%Y-%m-%dT%H:%M:%SZ")
    est, r, _ = ctx.dv.call("PATCH", f"{sim.FILAS}({fid})", {"sanic_claimhasta": pasado})
    c.check("se pudo sembrar un claim ya vencido (precondición: PATCH de sanic_claimhasta como código de servidor)",
            est == 204, f"HTTP {est}: {r}")

    def _claimhasta():
        _, r2, _ = ctx.dv.call("GET", f"{sim.FILAS}({fid})?$select=sanic_claimhasta")
        return r2.get("sanic_claimhasta")

    antes = _claimhasta()
    c.check("el claim vencido quedó grabado", bool(antes), f"sanic_claimhasta={antes!r}")

    # Validada → Digitada (Ejecutivo): transición legítima que SIGUE "abierta".
    est, r, _ = ctx.dv.call("PATCH", f"{sim.FILAS}({fid})", {"sanic_estado": DIGITADA},
                             cabeceras={"MSCRMCallerID": ejecutivo_id})
    c.check("el Ejecutivo digita (Validada → Digitada)", est == 204, f"HTTP {est}: {r}")

    despues_de_digitar = _claimhasta()
    c.check("Validada → Digitada NO tocó el claim (sigue siendo el mismo valor vencido)",
            despues_de_digitar == antes, f"antes={antes!r} después={despues_de_digitar!r}")

    # Digitada → Aprobada (Supervisor, otro usuario: segregación de funciones): estado TERMINAL.
    est, r, _ = ctx.dv.call("PATCH", f"{sim.FILAS}({fid})", {"sanic_estado": APROBADA},
                             cabeceras={"MSCRMCallerID": supervisor_id})
    c.check("el Supervisor aprueba (Digitada → Aprobada)", est == 204, f"HTTP {est}: {r}")

    despues_de_aprobar = _claimhasta()
    c.check("Digitada → Aprobada (estado terminal) SÍ liberó el claim (sanic_claimhasta quedó vacío)",
            despues_de_aprobar is None, f"sanic_claimhasta={despues_de_aprobar!r}")
    return c


CASOS = [i01, i02, i03, i04, i05, i06, i07, i08, i09, i10, i11, i12, i13]
