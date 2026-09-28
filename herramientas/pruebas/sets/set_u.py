#!/usr/bin/env python3
"""Set U — Autorización por cliente, DN-01 (`pruebas/10-autorizacion-cliente.md`).

`autorizacion.modo` (`cliente`/`plan`, degradación a `plan` con basura), la tabla
`AutorizacionCliente`, el nombre calculado `<correo> → <cliente>`, la evidencia
firmada (`sanic_documentofirmado`/`sanic_fechadocumento`) y las subgrillas
nuevas de Cliente y Autorizado.

**Frontera con el set R (`03-reglas-de-registro.md`).** R-15..R-18 cubren
`autorizacion.modo` DENTRO del motor de las ocho reglas de nivel Registro (que
la regla resuelva por una vía u otra, con las mismas filas que ya usa R). Este
set cubre TODO lo demás de DN-01: el nombre calculado, la evidencia firmada, la
clave alternativa, las subgrillas de la app, y — a diferencia de R, que solo
mira el nivel Registro — el nivel CORREO (`REMITENTE_RECONOCIDO`) y la
comparación de mensaje entre modos, que R no toca.

**Frontera con el set C (`01-clasificacion-del-correo.md`).** C-09/C-10/C-11
cubren `autorizacion.modo` a nivel Correo, con SU propio Autorizado privado
(`REMITENTE_DN01`). Ese es su alcance: que el "portero" (`REMITENTE_RECONOCIDO`)
resuelva el modo. Este set no repite esa prueba: usa el nivel Correo solo como
tránsito hacia los casos que sí le corresponden (U-02, U-03, U-05).

**D-59 (`diseno/PENDIENTES.md`, ABIERTA, pospuesta por el aprobador el
2026-09-27).** `AvisosDataverse.AvisarAEjecutivos` revienta con
`InvalidCastException` cada vez que `ClasificarCorreo` termina en un estado que
dispara el aviso a ejecutivos (remitente no reconocido, respuesta, ilegible):
la transacción se revierte entera. U-03, U-04 (mitad a) y U-05 (paso 2)
EJERCITAN justo ese camino (un correo que queda "No reconocida"), así que van
a toparse con D-59 en el entorno de hoy. CONVENCIONES §3: "un defecto conocido
no es N-A" — quedan en FALLA, citando D-59, no en N-A y no silenciados.
"""
import os
import sys

_AQUI = os.path.dirname(os.path.abspath(__file__))
_HERRAMIENTAS_PRUEBAS = os.path.dirname(_AQUI)
sys.path.insert(0, _HERRAMIENTAS_PRUEBAS)

import preparacion_comun as pc  # noqa: E402
import simulador as sim  # noqa: E402
import preparar_datos as prep  # noqa: E402

LETRA = "U"
NOMBRE = "Autorización por cliente (DN-01)"

# Remitentes propios de este set (CONVENCIONES §5: nunca los de una persona real).
REMITENTE = "zz-corredor-fase1@example.com"  # compartido con C/S/R: SOLO AutorizacionPlan.
REMITENTE_SOLOCLIENTE = "zz-corredor-set-u-dn01-cliente@example.com"
REMITENTE_OTRO_CLIENTE = "zz-corredor-set-u-dn01-otrocliente@example.com"

CUMPLIDA, NOCUMPLIDA, OMITIDA = 159460001, 159460002, 159460003
ING, NOREC, DESC, RECH, ENPROC, PROC, CERR, NOESNUEVO, VENC = (159460001 + i for i in range(9))
FILA_RECH_VAL, FILA_SIN_AUT, FILA_VALIDADA = 159460001, 159460002, 159460003
TIPO_TEXTO = 159460001  # sanic_mppp_ch_tipoparametro: Texto

ESTADO_TXT = {ING: "Ingresada", NOREC: "No reconocida", DESC: "Descartada", RECH: "Rechazada",
              ENPROC: "En proceso", PROC: "Procesada", CERR: "Cerrada",
              NOESNUEVO: "No es correo nuevo", VENC: "Vencida"}

PARAMETROS = "sanic_mppp_tbl_parametros"
CLIENTES = "sanic_mppp_tbl_clientes"
PLANES = "sanic_mppp_tbl_plans"
AUTORIZADOS = "sanic_mppp_tbl_autorizados"
AUTORIZACIONCLIENTES = "sanic_mppp_tbl_autorizacionclientes"

# Logical names (singular): lo que piden `systemforms.objecttypecode` y `EntityDefinitions`.
TABLA_CLIENTE = "sanic_mppp_tbl_cliente"
TABLA_AUTORIZADO = "sanic_mppp_tbl_autorizado"
TABLA_AUTORIZACIONCLIENTE = "sanic_mppp_tbl_autorizacioncliente"
TABLA_AUTORIZACIONPLAN = "sanic_mppp_tbl_autorizacionplan"

FORM_TIPO_PRINCIPAL = 2  # Learn, systemform.type: 2 = Main (mismo valor que usa herramientas/construir/formulario.py)

CAMPO_ARCHIVO = "sanic_documentofirmado"
CAMPO_FECHA = "sanic_fechadocumento"

# El "cliente ajeno" y su plan: fixture privada de este set, necesaria para U-06 y U-09 (ver sus
# docstrings). Nombres marcados (aunque se borran por GUID trackeado, nunca por `marca_agua`, para
# no depender de que nadie los busque por nombre).
CLIENTE_AJENO = "ZZP-Cliente ajeno DN-01 (set U)"
PLAN_AJENO_CODIGO = "ZZU1"

_DETALLE_D59 = (
    "AvisosDataverse.AvisarAEjecutivos (Datos/Avisos.cs:109) revienta con InvalidCastException "
    "(Guid -> EntityReference sobre systemuserroles) cada vez que ClasificarCorreo termina en un "
    "estado que dispara el aviso a ejecutivos (remitente no reconocido, respuesta, ilegible); la "
    "transacción se revierte entera, así que la Solicitud queda en Ingresada, no en 'No reconocida'. "
    "diseno/PENDIENTES.md, D-59, ABIERTA, pospuesta por el aprobador el 2026-09-27. Mismo defecto que "
    "ya cruzó el set R en R-17 y el set C en C-02/C-04/C-10/C-11. CONVENCIONES §3: "
    "'un defecto conocido no es N-A' — este caso es FALLA, no N-A."
)


def _excel(nombre):
    return pc.excel(nombre)


# ===========================================================================
# preparar / cerrar
# ===========================================================================
def preparar(ctx):
    """Deja listo TODO lo que el set U necesita, y nada más (CONVENCIONES §8):

    - **Datos de negocio** (fixture compartido `PR*`, el `REMITENTE` de siempre autorizado por
      `AutorizacionPlan`) — igual que `set_s.py`/`set_r.py`/`set_c.py`.
    - **Los 15 Excel + el adjunto que no es Excel**.
    - **`plantilla.estructura`** (la necesita `ctx.procesar()`/`ctx.solicitud()` para cualquier
      correo con Excel real) y **`autorizacion.modo`**: solo se COMPRUEBA que exista una versión
      activa (§8.2: su canon `plan` lo aplica `correr.py` antes y después de CADA set, no este
      `preparar()`).
    - **DN-01: un Autorizado propio (`REMITENTE_SOLOCLIENTE`) con UNA ÚNICA vía de autorización**
      — `AutorizacionCliente` hacia el Cliente compartido `PR - Pruebas MPPP SA`, CERO
      `AutorizacionPlan` — para U-02/U-04/U-11.
    - **Un segundo Cliente "ajeno" (`CLIENTE_AJENO`) con un Plan propio activo**, y un segundo
      Autorizado propio (`REMITENTE_OTRO_CLIENTE`) autorizado SOLO por ese cliente ajeno. Hace
      falta para U-06 (ver su docstring: comparar el mensaje al cliente en los dos modos SIN pasar
      por 'No reconocida', que cruzaría D-59) y para U-09 (un Autorizado con `AutorizacionCliente`
      hacia MÁS DE UN Cliente: `REMITENTE_SOLOCLIENTE` se autoriza TAMBIÉN sobre este cliente ajeno).

    **Lo que NO prepara, y por qué:**

    - **Roles de usuarios de prueba ni sesiones de navegador** (CONVENCIONES §6). Los once casos de
      este set se verifican contra las Custom API (`simulador.py`) y contra `systemforms`/
      `EntityDefinitions`/`savedqueries` por Web API directo — ninguno abre la app (ver U-08/U-09/U-10
      más abajo: lo que el diseño dejó como "GUI, sesión admin_planes" se pudo automatizar leyendo el
      `formxml` publicado y probando la escritura real de la evidencia, sin navegador).
    - **Los cinco flujos activos.** Mismo motivo que `set_s.py`.
    - **El canon de configuración global** (§8.2, `autorizacion.modo` incluido). Lo aplica
      `correr.py` antes y después de CADA set. Cada caso que necesita un valor puntual de
      `autorizacion.modo` lo fija ÉL MISMO al empezar (nunca asume lo que dejó el caso anterior):
      así el set corre igual de bien caso por caso que entero.
    """
    pc.verificar_datos_negocio(ctx.dv, REMITENTE)
    pc.verificar_excels()
    ctx.extra["estructura"] = pc.verificar_estructura_plantilla(ctx.dv)
    pc.verificar_parametro_activo(ctx.dv, "autorizacion.modo")

    cliente_id = _id_cliente_compartido(ctx.dv)
    ctx.extra["cliente_compartido_id"] = cliente_id

    autorizado_id = _crear_autorizado(ctx.dv, REMITENTE_SOLOCLIENTE)
    ctx.extra["autorizado_soloclienteid"] = autorizado_id
    ctx.extra["autorizacioncliente_id"] = pc.crear_autorizacion_cliente(ctx.dv, autorizado_id, cliente_id)

    cliente_ajeno_id, plan_ajeno_id = _crear_cliente_y_plan_ajenos(ctx.dv)
    ctx.extra["cliente_ajeno_id"] = cliente_ajeno_id
    ctx.extra["plan_ajeno_id"] = plan_ajeno_id

    # U-09 pide un Autorizado con AutorizacionCliente hacia MÁS DE UN cliente.
    ctx.extra["autorizacioncliente_id_2"] = pc.crear_autorizacion_cliente(ctx.dv, autorizado_id, cliente_ajeno_id)

    autorizado_ajeno_id = _crear_autorizado(ctx.dv, REMITENTE_OTRO_CLIENTE)
    ctx.extra["autorizado_otroclienteid"] = autorizado_ajeno_id
    ctx.extra["autorizacioncliente_ajena_id"] = pc.crear_autorizacion_cliente(
        ctx.dv, autorizado_ajeno_id, cliente_ajeno_id)


def cerrar(ctx):
    """Borra, por GUID (nunca por nombre ni por `marca_agua`: ninguna `AutorizacionCliente` ni
    ningún `Autorizado` puede llevar la marca `ZZP-` intacta, ver el módulo `set_r.py`), TODO lo que
    `preparar()` armó — las `AutorizacionCliente` primero (referencian Autorizado y Cliente por
    lookup), después los `Autorizado`, después el Plan ajeno, y al final el Cliente ajeno. No toca
    el fixture compartido `PR*` ni ninguna `AutorizacionPlan`."""
    errores = ctx.limpiar_solicitudes()

    for clave in ("autorizacioncliente_id_2", "autorizacioncliente_id", "autorizacioncliente_ajena_id"):
        idac = ctx.extra.get(clave)
        if idac:
            est, r, _ = ctx.dv.call("DELETE", f"{AUTORIZACIONCLIENTES}({idac})")
            if est not in (200, 204):
                errores.append(f"no se pudo borrar la AutorizacionCliente {idac} ({clave}) (HTTP {est}): {r}")

    for clave in ("autorizado_soloclienteid", "autorizado_otroclienteid"):
        ida = ctx.extra.get(clave)
        if ida:
            est, r, _ = ctx.dv.call("DELETE", f"{AUTORIZADOS}({ida})")
            if est not in (200, 204):
                errores.append(f"no se pudo borrar el Autorizado {ida} ({clave}) (HTTP {est}): {r}")

    plan_ajeno_id = ctx.extra.get("plan_ajeno_id")
    if plan_ajeno_id:
        est, r, _ = ctx.dv.call("DELETE", f"{PLANES}({plan_ajeno_id})")
        if est not in (200, 204):
            errores.append(f"no se pudo borrar el Plan ajeno {plan_ajeno_id} (HTTP {est}): {r}")

    cliente_ajeno_id = ctx.extra.get("cliente_ajeno_id")
    if cliente_ajeno_id:
        est, r, _ = ctx.dv.call("DELETE", f"{CLIENTES}({cliente_ajeno_id})")
        if est not in (200, 204):
            errores.append(f"no se pudo borrar el Cliente ajeno {cliente_ajeno_id} (HTTP {est}): {r}")

    return errores


# ===========================================================================
# Helpers locales. `_fijar_modo` y `_crear_autorizacion_cliente` se
# consolidaron en `preparacion_comun.py` (2026-09-28: eran idénticas a las
# copias de `set_c.py`/`set_r.py` — ver el docstring de esas funciones ahí).
# Lo que queda acá (`_crear_autorizado`, `_id_cliente_compartido`,
# `_crear_cliente_y_plan_ajenos`) es propio de este set o tiene una gemela
# en `set_r.py`/`set_c.py` que no estaba en el alcance aprobado de esta
# consolidación — queda documentado, no tocado.
# ===========================================================================
def _id_cliente_compartido(dv):
    est, r, _ = dv.call("GET", f"{CLIENTES}?$select=sanic_mppp_tbl_clienteid"
                                f"&$filter=sanic_nombre eq '{prep.CLIENTE}'")
    if est != 200 or not r.get("value"):
        raise RuntimeError(f"no se encontró el Cliente compartido {prep.CLIENTE!r} "
                           "(¿preparar_datos.py no corrió todavía?)")
    return r["value"][0]["sanic_mppp_tbl_clienteid"]


def _crear_autorizado(dv, correo):
    """Create-if-missing, por `sanic_nombre` (el correo)."""
    est, r, _ = dv.call("GET", f"{AUTORIZADOS}?$select=sanic_mppp_tbl_autorizadoid"
                                f"&$filter=sanic_nombre eq '{correo}'")
    if est == 200 and r.get("value"):
        return r["value"][0]["sanic_mppp_tbl_autorizadoid"]
    est, r, cab = dv.call("POST", AUTORIZADOS, {"sanic_nombre": correo})
    if est not in (200, 201, 204):
        raise RuntimeError(f"no se pudo crear el Autorizado {correo!r} (HTTP {est}): {r}")
    from dataverse_api import Dataverse
    return Dataverse.id_creado(cab)


def _crear_cliente_y_plan_ajenos(dv):
    """El Cliente "ajeno" y su Plan propio (activo, formato 06/COR: no importa cuál, solo que
    exista y esté activo para que `REMITENTE_RECONOCIDO` cuente al menos un plan). Create-if-missing."""
    cli = prep.uno(dv, CLIENTES, f"sanic_nombre eq '{CLIENTE_AJENO}'", "sanic_mppp_tbl_clienteid")
    idc = cli["sanic_mppp_tbl_clienteid"] if cli else prep.crear(
        # sanic_cifcom exige EXACTAMENTE 12 caracteres (9 alfanuméricos + 3 dígitos), y
        # sanic_cifbac tiene clave única en el entorno: "900100201" ya lo usaba un Cliente de OTRO
        # agente en paralelo ("ZZ - Recon GUI SA") — valores secuenciales chocan entre agentes que
        # preparan fixtures al mismo tiempo. Ninguna de las dos reglas está en
        # `preparar_datos.comprobar_largos()` (que solo revisa longitud): descubierto corriendo
        # este set contra Dev.
        dv, CLIENTES, {"sanic_nombre": CLIENTE_AJENO, "sanic_cifbac": "900199001", "sanic_cifcom": "ZZPUDN010001"})

    pl = prep.uno(dv, PLANES, f"sanic_codigo eq '{PLAN_AJENO_CODIGO}'", "sanic_mppp_tbl_planid")
    idp = pl["sanic_mppp_tbl_planid"] if pl else prep.crear(dv, PLANES, {
        "sanic_nombre": f"{PLAN_AJENO_CODIGO} - {CLIENTE_AJENO}",
        "sanic_codigo": PLAN_AJENO_CODIGO,
        "sanic_tipoformato": prep.FORMATO["06"],
        "sanic_moneda": prep.MONEDA["COR"],
        "sanic_clienteid@odata.bind": f"/{CLIENTES}({idc})",
    })
    return idc, idp


def _clasificar_o_d59(ctx, c, sid, etiqueta):
    """Clasifica `sid` y devuelve el resultado, o `None` si D-59 lo cruzó — en ese caso ya deja
    anotado el `check` de FALLA con la cita del defecto (CONVENCIONES §3), y quien llama sigue
    con lo que pueda seguir probando (mismo patrón que `set_r.py::r17`)."""
    try:
        return sim.clasificar(ctx.dv, sid)
    except RuntimeError as e:
        if "No se pudo clasificar el correo" in str(e):
            c.check(f"{etiqueta}: bloqueado por D-59 (diseno/PENDIENTES.md) — {e}", False, _DETALLE_D59)
            return None
        raise


# ===========================================================================
# Formulario/vista/relación por Web API (sin navegador): U-08, U-09, U-10.
# Mismas rutas que ya usa `herramientas/construir/formulario.py` (solo lectura acá).
# ===========================================================================
def _leer_formulario_principal(dv, tabla):
    ruta = ("systemforms?$select=formid,name,formxml"
            f"&$filter=objecttypecode eq '{tabla}' and type eq {FORM_TIPO_PRINCIPAL}")
    est, r, _ = dv.call("GET", ruta)
    if est != 200:
        raise RuntimeError(f"no se pudo leer el formulario principal de '{tabla}' (HTTP {est}): {r}")
    filas = r.get("value") or []
    if len(filas) != 1:
        raise RuntimeError(f"se esperaba 1 formulario principal de '{tabla}', hay {len(filas)}")
    return filas[0]


def _vista_id(dv, tabla, nombre_vista):
    ruta = f"savedqueries?$select=savedqueryid&$filter=name eq '{nombre_vista}' and returnedtypecode eq '{tabla}'"
    est, r, _ = dv.call("GET", ruta)
    if est != 200 or not r.get("value"):
        return None
    return r["value"][0]["savedqueryid"]


def _relacion(dv, tabla_padre, tabla_hija, lookup):
    ruta = (f"EntityDefinitions(LogicalName='{tabla_padre}')/OneToManyRelationships"
            "?$select=SchemaName,ReferencingEntity,ReferencingAttribute")
    est, r, _ = dv.call("GET", ruta)
    if est != 200:
        return None
    for x in r.get("value", []):
        if x.get("ReferencingEntity") == tabla_hija and x.get("ReferencingAttribute") == lookup:
            return x.get("SchemaName")
    return None


def _subgrilla_en_formxml(formxml_texto, tabla_destino):
    """Busca, dentro del `formxml`, el control de subgrilla cuyo `TargetEntityType` sea
    `tabla_destino`, y devuelve `{titulo, view_id, relacion}` (o `None` si no está). El título es
    el `<label>` MÁS CERCANO antes del marcador (la celda de la subgrilla lleva el mismo título que
    su sección: `herramientas/construir/formulario.py::_celda_subgrilla`)."""
    import re
    marcador = f"<TargetEntityType>{tabla_destino}</TargetEntityType>"
    pos = formxml_texto.find(marcador)
    if pos == -1:
        return None
    etiquetas_antes = re.findall(r'<label description="([^"]*)"', formxml_texto[:pos])
    titulo = etiquetas_antes[-1] if etiquetas_antes else None
    despues = formxml_texto[pos:pos + 600]
    m_view = re.search(r"<ViewId>([^<]+)</ViewId>", despues)
    m_rel = re.search(r"<RelationshipName>([^<]+)</RelationshipName>", despues)
    return {"titulo": titulo,
            "view_id": (m_view.group(1) if m_view else None),
            "relacion": (m_rel.group(1) if m_rel else None)}


def _columnas_por_nombre(dv, tabla, columnas):
    """`{logicalname: atributo_o_None}` desde la metadata, sin `$filter` (las colecciones de
    `EntityDefinitions` no lo admiten de forma confiable: mismo patrón que
    `herramientas/construir/formulario.py::_columnas_de`, que trae todas y filtra en Python)."""
    ruta = f"EntityDefinitions(LogicalName='{tabla}')/Attributes?$select=LogicalName,RequiredLevel,AttributeTypeName"
    est, r, _ = dv.call("GET", ruta)
    if est != 200:
        raise RuntimeError(f"no se pudo leer las columnas de '{tabla}' (HTTP {est}): {r}")
    por_nombre = {a.get("LogicalName"): a for a in r.get("value", [])}
    return {c: por_nombre.get(c) for c in columnas}


# ===========================================================================
# Casos U-01 .. U-11
# ===========================================================================
def u01(ctx):
    c = pc.Caso("U-01", "Compatibilidad: sin el parámetro (o en 'plan'), nada cambia")
    fila = pc.verificar_parametro_activo(ctx.dv, "autorizacion.modo")
    valor = (fila.get("sanic_valor") or "").strip()
    c.check("autorizacion.modo activo = 'plan' (o vacío) ANTES de tocar nada (estado de hoy del entorno)",
            valor == "" or valor.lower() == "plan", f"obtenido {valor!r}")
    pc.fijar_modo_autorizacion(ctx.dv, "plan")

    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba U-01 compatibilidad",
                                     cantidad_adjuntos=1, cantidad_excel=1, excel_a_subir=_excel("E01-feliz-formato11.xlsx"))
    c.check("clasificación = nuevo, procesar = true (comportamiento de fase 1, sin cambios)",
            rc.get("clasificacion") == "nuevo" and rc.get("procesar") is True, str(rc))
    if rv is not None:
        s = sim.leer_solicitud(ctx.dv, sid)
        c.check("Estado = En proceso, filas t/v/r = 3/3/0 (EXACTAMENTE lo que 01/02/03 ya documentan)",
                s.get("sanic_estadoprocesamiento") == ENPROC
                and (s.get("sanic_filastotales"), s.get("sanic_filasvalidas"), s.get("sanic_filasrechazadas")) == (3, 3, 0),
                ESTADO_TXT.get(s.get("sanic_estadoprocesamiento")))
    return c


def u02(ctx):
    c = pc.Caso("U-02", "Modo 'cliente': un correo autorizado SOLO por cliente entra, en los DOS niveles")
    pc.fijar_modo_autorizacion(ctx.dv, "cliente")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE_SOLOCLIENTE, asunto="Prueba U-02 modo cliente",
                                     cantidad_adjuntos=1, cantidad_excel=1, excel_a_subir=_excel("E01-feliz-formato11.xlsx"))
    c.check("REMITENTE_RECONOCIDO (nivel Correo): la Solicitud entra (no 'No reconocida')",
            rc.get("clasificacion") == "nuevo" and rc.get("procesar") is True, str(rc))
    if rv is None:
        c.check("se llegó a validar (procesar=true tenía que llamar a validar)", False, str(rc))
        return c
    filas = sim.leer_filas(ctx.dv, sid)
    c.check("las 3 filas quedan Validada (AUTORIZACION_CORREO_PLAN resuelve por AutorizacionCliente, "
            "sin enumerar el plan)",
            len(filas) == 3 and all(f["sanic_estado"] == FILA_VALIDADA for f in filas),
            str([(f["sanic_numerofila"], f["sanic_estado"], f.get("sanic_mensaje")) for f in filas]))
    s = sim.leer_solicitud(ctx.dv, sid)
    c.check("Estado = En proceso", s.get("sanic_estadoprocesamiento") == ENPROC,
            ESTADO_TXT.get(s.get("sanic_estadoprocesamiento")))
    return c


def u03(ctx):
    c = pc.Caso("U-03", "Modo 'cliente': solo AutorizacionPlan (sin AutorizacionCliente) NO alcanza")
    pc.fijar_modo_autorizacion(ctx.dv, "cliente")
    sid, mid = ctx.solicitud(remitente=REMITENTE, asunto="Prueba U-03 solo AutorizacionPlan",
                              cantidad_adjuntos=1, cantidad_excel=1, excel_a_subir=_excel("E01-feliz-formato11.xlsx"))
    rc = _clasificar_o_d59(ctx, c, sid, "U-03")
    if rc is None:
        return c
    c.check("la Solicitud queda 'No reconocida' (AutorizacionPlan de REMITENTE queda muda en modo cliente)",
            rc.get("clasificacion") == "remitente_no_reconocido" and rc.get("procesar") is False, str(rc))
    s = sim.leer_solicitud(ctx.dv, sid)
    c.check("Estado = No reconocida", s.get("sanic_estadoprocesamiento") == NOREC,
            ESTADO_TXT.get(s.get("sanic_estadoprocesamiento")))
    return c


def u04(ctx):
    c = pc.Caso("U-04", "Valor basura en 'autorizacion.modo' degrada a 'plan', sin tumbar nada")
    pc.fijar_modo_autorizacion(ctx.dv, "clienteX")

    sid1, mid1 = ctx.solicitud(remitente=REMITENTE_SOLOCLIENTE, asunto="Prueba U-04a basura (solo-cliente)",
                                cantidad_adjuntos=1, cantidad_excel=1, excel_a_subir=_excel("E01-feliz-formato11.xlsx"))
    rc1 = _clasificar_o_d59(ctx, c, sid1, "U-04a")
    if rc1 is not None:
        c.check("(a) autorizado SOLO por cliente: 'clienteX' degrada a plan -> No reconocida "
                "(la basura NO se interpreta como 'cliente')",
                rc1.get("clasificacion") == "remitente_no_reconocido" and rc1.get("procesar") is False, str(rc1))

    sid2, mid2, rc2, rv2 = ctx.procesar(remitente=REMITENTE, asunto="Prueba U-04b basura (autorizado por plan)",
                                         cantidad_adjuntos=1, cantidad_excel=1, excel_a_subir=_excel("E01-feliz-formato11.xlsx"))
    c.check("(b) autorizado por AutorizacionPlan de siempre: sigue entrando (la operación no se tumbó)",
            rc2.get("clasificacion") == "nuevo" and rc2.get("procesar") is True, str(rc2))
    if rv2 is not None:
        s2 = sim.leer_solicitud(ctx.dv, sid2)
        c.check("(b) Estado = En proceso", s2.get("sanic_estadoprocesamiento") == ENPROC,
                ESTADO_TXT.get(s2.get("sanic_estadoprocesamiento")))
    return c


def u05(ctx):
    c = pc.Caso("U-05", "Cambiar el modo no borra nada: AutorizacionPlan revive al volver a 'plan'")
    pc.fijar_modo_autorizacion(ctx.dv, "plan")
    sid1, mid1, rc1, rv1 = ctx.procesar(remitente=REMITENTE, asunto="Prueba U-05 paso1 (modo plan)",
                                         cantidad_adjuntos=1, cantidad_excel=1, excel_a_subir=_excel("E01-feliz-formato11.xlsx"))
    c.check("paso 1: entra en modo plan", rc1.get("procesar") is True, str(rc1))
    if rv1 is not None:
        filas1 = sim.leer_filas(ctx.dv, sid1)
        c.check("paso 1: 3 filas Validada", len(filas1) == 3 and all(f["sanic_estado"] == FILA_VALIDADA for f in filas1),
                str([(f["sanic_numerofila"], f["sanic_estado"]) for f in filas1]))

    pc.fijar_modo_autorizacion(ctx.dv, "cliente")
    sid2, mid2 = ctx.solicitud(remitente=REMITENTE, asunto="Prueba U-05 paso2 (modo cliente, mismo correo)",
                                cantidad_adjuntos=1, cantidad_excel=1, excel_a_subir=_excel("E01-feliz-formato11.xlsx"))
    rc2 = _clasificar_o_d59(ctx, c, sid2, "U-05 paso2")
    if rc2 is not None:
        c.check("paso 2: el MISMO correo ahora NO entra (AutorizacionPlan queda muda en modo cliente)",
                rc2.get("clasificacion") == "remitente_no_reconocido" and rc2.get("procesar") is False, str(rc2))

    pc.fijar_modo_autorizacion(ctx.dv, "plan")
    sid3, mid3, rc3, rv3 = ctx.procesar(remitente=REMITENTE, asunto="Prueba U-05 paso4 (vuelta a modo plan)",
                                         cantidad_adjuntos=1, cantidad_excel=1, excel_a_subir=_excel("E01-feliz-formato11.xlsx"))
    c.check("paso 4: vuelve a entrar exactamente como en el paso 1, sin haber tocado la AutorizacionPlan",
            rc3.get("procesar") is True, str(rc3))
    if rv3 is not None:
        filas3 = sim.leer_filas(ctx.dv, sid3)
        c.check("paso 4: 3 filas Validada, igual que en el paso 1",
                len(filas3) == 3 and all(f["sanic_estado"] == FILA_VALIDADA for f in filas3),
                str([(f["sanic_numerofila"], f["sanic_estado"]) for f in filas3]))
    return c


def u06(ctx):
    c = pc.Caso("U-06", "El mensaje al cliente es idéntico en los dos modos")
    # (a) modo plan: PRNA existe pero REMITENTE no tiene AutorizacionPlan sobre él (R-08, mismo Excel).
    pc.fijar_modo_autorizacion(ctx.dv, "plan")
    sid_a, mid_a, rc_a, rv_a = ctx.procesar(remitente=REMITENTE, asunto="Prueba U-06a (modo plan, PRNA)",
                                             cantidad_adjuntos=1, cantidad_excel=1, excel_a_subir=_excel("E08-sin-autorizacion.xlsx"))
    msg_a = None
    if rv_a is not None:
        filas_a = sim.leer_filas(ctx.dv, sid_a)
        c.check("(a) 1 fila, 'Sin autorización' (PRNA existe, REMITENTE no autorizado por plan sobre él)",
                len(filas_a) == 1 and filas_a[0]["sanic_estado"] == FILA_SIN_AUT, str(filas_a))
        msg_a = filas_a[0].get("sanic_mensaje") if filas_a else None

    # (b) modo cliente: REMITENTE_OTRO_CLIENTE está reconocido (autorizado sobre el cliente AJENO,
    # que tiene su propio plan activo), pero PRNA es del cliente COMPARTIDO -> también 'Sin
    # autorización', citando el MISMO plan PRNA. Evita 'No reconocida' (y con eso D-59): el correo
    # SÍ está autorizado en general, solo que no sobre el cliente dueño de PRNA.
    pc.fijar_modo_autorizacion(ctx.dv, "cliente")
    sid_b, mid_b, rc_b, rv_b = ctx.procesar(remitente=REMITENTE_OTRO_CLIENTE, asunto="Prueba U-06b (modo cliente, PRNA)",
                                             cantidad_adjuntos=1, cantidad_excel=1, excel_a_subir=_excel("E08-sin-autorizacion.xlsx"))
    c.check("(b) el correo SÍ es reconocido (autorizado vía su propio cliente, ajeno al dueño de PRNA)",
            rc_b.get("clasificacion") == "nuevo" and rc_b.get("procesar") is True, str(rc_b))
    msg_b = None
    if rv_b is not None:
        filas_b = sim.leer_filas(ctx.dv, sid_b)
        c.check("(b) 1 fila, 'Sin autorización' (PRNA es de OTRO cliente, no del autorizado por AutorizacionCliente)",
                len(filas_b) == 1 and filas_b[0]["sanic_estado"] == FILA_SIN_AUT, str(filas_b))
        msg_b = filas_b[0].get("sanic_mensaje") if filas_b else None

    if msg_a and msg_b:
        c.check("el texto es EXACTAMENTE el mismo en los dos modos (mismo plan citado, byte a byte)",
                msg_a == msg_b, f"plan: {msg_a!r} · cliente: {msg_b!r}")
        c.check("nombra el plan PRNA, sin distinguir 'no existe' de 'no autorizado' (oráculo del 2026-09-24)",
                "PRNA" in msg_a, msg_a)
    else:
        c.check("se pudieron comparar los dos mensajes (las dos filas llegaron a 'Sin autorización')",
                False, f"plan={msg_a!r} cliente={msg_b!r}")
    return c


def u07(ctx):
    c = pc.Caso("U-07", "Nombre calculado de AutorizacionCliente: '<correo> → <cliente>', T(400)")
    from dataverse_api import Dataverse

    cliente_id = ctx.extra["cliente_compartido_id"]
    aut = prep.uno(ctx.dv, AUTORIZADOS, f"sanic_nombre eq '{REMITENTE}'", "sanic_mppp_tbl_autorizadoid")
    if aut is None:
        c.check("el Autorizado compartido existe para poder probar el cálculo", False)
        return c
    autorizado_id = aut["sanic_mppp_tbl_autorizadoid"]

    cuerpo = {
        "sanic_autorizadoid@odata.bind": f"/{AUTORIZADOS}({autorizado_id})",
        "sanic_clienteid@odata.bind": f"/{CLIENTES}({cliente_id})",
    }
    est, r, cab = ctx.dv.call("POST", AUTORIZACIONCLIENTES, cuerpo)
    if est not in (200, 201, 204):
        c.check("se pudo crear la AutorizacionCliente SIN sanic_nombre, para probar el cálculo", False,
                f"HTTP {est}: {r}")
        return c
    idac = Dataverse.id_creado(cab)
    try:
        _, r2, _ = ctx.dv.call("GET", f"{AUTORIZACIONCLIENTES}({idac})?$select=sanic_nombre")
        esperado = f"{REMITENTE} → {prep.CLIENTE}"
        c.check("sanic_nombre = '<correo del autorizado> → <nombre del cliente>' "
                "(lo calcula el step, pisando lo que no se mandó)",
                r2.get("sanic_nombre") == esperado, f"obtenido {r2.get('sanic_nombre')!r}")
    finally:
        ctx.dv.call("DELETE", f"{AUTORIZACIONCLIENTES}({idac})")
    return c


def u08(ctx):
    c = pc.Caso("U-08", "Subgrilla 'Correos autorizados sobre este cliente' en el formulario de Cliente")
    form = _leer_formulario_principal(ctx.dv, TABLA_CLIENTE)
    sub = _subgrilla_en_formxml(form["formxml"], TABLA_AUTORIZACIONCLIENTE)
    c.check("el formulario principal de Cliente tiene una subgrilla de AutorizacionCliente",
            sub is not None,
            "no se encontró <TargetEntityType>sanic_mppp_tbl_autorizacioncliente</TargetEntityType> en el formxml")
    if sub is None:
        return c
    c.check("la sección se titula 'Correos autorizados sobre este cliente'",
            sub["titulo"] == "Correos autorizados sobre este cliente", f"obtenido {sub['titulo']!r}")

    relacion_esperada = _relacion(ctx.dv, TABLA_CLIENTE, TABLA_AUTORIZACIONCLIENTE, "sanic_clienteid")
    c.check("la subgrilla filtra por la relación del lookup 'sanic_clienteid' (Cliente -> AutorizacionCliente)",
            relacion_esperada is not None and sub["relacion"] == relacion_esperada,
            f"formxml={sub['relacion']!r} esperada={relacion_esperada!r}")

    vista_id = _vista_id(ctx.dv, TABLA_AUTORIZACIONCLIENTE, "Correos autorizados sobre este cliente")
    c.check("existe la vista 'Correos autorizados sobre este cliente' en el entorno",
            vista_id is not None, "GET savedqueries no la encontró")
    if vista_id and sub["view_id"]:
        c.check("el ViewId del formulario apunta exactamente a esa vista",
                sub["view_id"].strip("{}").lower() == vista_id.lower(),
                f"formxml={sub['view_id']!r} vista={vista_id!r}")

    # Que "aparece vacía teniendo datos" (el otro síntoma que el MD nombra) no pase: la subgrilla
    # filtra por 'sanic_clienteid', así que consultamos ESA MISMA condición y confirmamos que hay
    # algo que mostrar (la AutorizacionCliente de U-02/preparar()).
    cliente_id = ctx.extra["cliente_compartido_id"]
    est, r, _ = ctx.dv.call(
        "GET", f"{AUTORIZACIONCLIENTES}?$select=sanic_nombre&$filter=_sanic_clienteid_value eq {cliente_id}")
    filas = r.get("value", []) if est == 200 else []
    c.check("hay al menos una AutorizacionCliente para ese Cliente (lo que la subgrilla mostraría, filtrando igual)",
            est == 200 and len(filas) >= 1, f"HTTP {est}: {len(filas)} filas")
    return c


def u09(ctx):
    c = pc.Caso("U-09", "Subgrilla 'Clientes que puede modificar' en el formulario de Autorizado")
    form = _leer_formulario_principal(ctx.dv, TABLA_AUTORIZADO)

    sub_planes = _subgrilla_en_formxml(form["formxml"], TABLA_AUTORIZACIONPLAN)
    c.check("la subgrilla existente 'Planes que puede modificar' sigue presente (no se pisó)",
            sub_planes is not None and sub_planes["titulo"] == "Planes que puede modificar", str(sub_planes))

    sub = _subgrilla_en_formxml(form["formxml"], TABLA_AUTORIZACIONCLIENTE)
    c.check("el formulario de Autorizado tiene una subgrilla de AutorizacionCliente", sub is not None,
            "no se encontró <TargetEntityType>sanic_mppp_tbl_autorizacioncliente</TargetEntityType> en el formxml")
    if sub is None:
        return c
    c.check("la sección se titula 'Clientes que puede modificar'",
            sub["titulo"] == "Clientes que puede modificar", f"obtenido {sub['titulo']!r}")

    relacion_esperada = _relacion(ctx.dv, TABLA_AUTORIZADO, TABLA_AUTORIZACIONCLIENTE, "sanic_autorizadoid")
    c.check("la subgrilla filtra por la relación del lookup 'sanic_autorizadoid' (Autorizado -> AutorizacionCliente)",
            relacion_esperada is not None and sub["relacion"] == relacion_esperada,
            f"formxml={sub['relacion']!r} esperada={relacion_esperada!r}")

    vista_id = _vista_id(ctx.dv, TABLA_AUTORIZACIONCLIENTE, "Clientes que puede modificar")
    c.check("existe la vista 'Clientes que puede modificar' en el entorno",
            vista_id is not None, "GET savedqueries no la encontró")
    if vista_id and sub["view_id"]:
        c.check("el ViewId del formulario apunta exactamente a esa vista",
                sub["view_id"].strip("{}").lower() == vista_id.lower(),
                f"formxml={sub['view_id']!r} vista={vista_id!r}")

    autorizado_id = ctx.extra["autorizado_soloclienteid"]
    est, r, _ = ctx.dv.call(
        "GET", f"{AUTORIZACIONCLIENTES}?$select=_sanic_clienteid_value"
               f"&$filter=_sanic_autorizadoid_value eq {autorizado_id}")
    filas = r.get("value", []) if est == 200 else []
    clientes_distintos = {f.get("_sanic_clienteid_value") for f in filas}
    c.check("ese Autorizado tiene AutorizacionCliente hacia MÁS DE UN Cliente (lo que la subgrilla mostraría)",
            est == 200 and len(clientes_distintos) >= 2, f"HTTP {est}: {len(clientes_distintos)} clientes distintos")
    return c


def u10(ctx):
    c = pc.Caso("U-10", "Evidencia firmada opcional en AutorizacionCliente")
    cols = _columnas_por_nombre(ctx.dv, TABLA_AUTORIZACIONCLIENTE, (CAMPO_ARCHIVO, CAMPO_FECHA))
    for columna, tipo_esperado in ((CAMPO_ARCHIVO, "FileType"), (CAMPO_FECHA, "DateTimeType")):
        a = cols.get(columna)
        nivel = ((a or {}).get("RequiredLevel") or {}).get("Value")
        tipo = ((a or {}).get("AttributeTypeName") or {}).get("Value")
        c.check(f"{columna} existe, es {tipo_esperado} y NO es requerida (RequiredLevel=None)",
                a is not None and nivel == "None" and tipo == tipo_esperado, f"obtenido {a}")

    form = _leer_formulario_principal(ctx.dv, TABLA_AUTORIZACIONCLIENTE)
    tiene_seccion = "Evidencia de la autorizacion" in form["formxml"]
    tiene_archivo = f'datafieldname="{CAMPO_ARCHIVO}"' in form["formxml"]
    tiene_fecha = f'datafieldname="{CAMPO_FECHA}"' in form["formxml"]
    c.check("el formulario 'Autorizacion por cliente' tiene la sección 'Evidencia de la autorizacion' "
            "con los dos campos",
            tiene_seccion and tiene_archivo and tiene_fecha,
            f"seccion={tiene_seccion} archivo={tiene_archivo} fecha={tiene_fecha}")

    # Funcional: la AutorizacionCliente de preparar()/U-02 ya está guardada SIN evidencia — que
    # exista prueba que el alta sin esos campos no se rechazó (no son requeridos de verdad).
    idac = ctx.extra["autorizacioncliente_id"]
    est, r, _ = ctx.dv.call(
        "GET", f"{AUTORIZACIONCLIENTES}({idac})?$select={CAMPO_ARCHIVO}_name,{CAMPO_FECHA}")
    c.check("guardada sin evidencia, los dos campos quedan vacíos (el alta no la exigió)",
            est == 200 and not r.get(f"{CAMPO_ARCHIVO}_name") and not r.get(CAMPO_FECHA), f"HTTP {est}: {r}")

    contenido = "ZZP- evidencia de prueba U-10, sin valor legal.".encode("utf-8")
    est_up, r_up, _ = ctx.dv.subir_archivo(f"{AUTORIZACIONCLIENTES}({idac})/{CAMPO_ARCHIVO}",
                                           "ZZP-evidencia-u10.txt", contenido)
    c.check("se pudo subir el documento firmado (archivo, sin haber sido requerido antes)",
            est_up in (200, 204), f"HTTP {est_up}: {r_up}")

    est_f, r_f, _ = ctx.dv.call("PATCH", f"{AUTORIZACIONCLIENTES}({idac})", {CAMPO_FECHA: "2026-09-27"})
    c.check("se pudo escribir la fecha del documento", est_f in (200, 204), f"HTTP {est_f}: {r_f}")

    est2, r2, _ = ctx.dv.call(
        "GET", f"{AUTORIZACIONCLIENTES}({idac})?$select={CAMPO_ARCHIVO}_name,{CAMPO_FECHA}")
    c.check("releído, trae el archivo y la fecha que se acaban de cargar",
            est2 == 200 and r2.get(f"{CAMPO_ARCHIVO}_name") == "ZZP-evidencia-u10.txt"
            and r2.get(CAMPO_FECHA) == "2026-09-27",
            f"HTTP {est2}: {r2}")
    return c


def u11(ctx):
    c = pc.Caso("U-11", "Clave alternativa: no se repite el par autorizado + cliente")
    from dataverse_api import Dataverse

    autorizado_id = ctx.extra["autorizado_soloclienteid"]
    cliente_id = ctx.extra["cliente_compartido_id"]
    cuerpo = {
        "sanic_autorizadoid@odata.bind": f"/{AUTORIZADOS}({autorizado_id})",
        "sanic_clienteid@odata.bind": f"/{CLIENTES}({cliente_id})",
    }
    est, r, cab = ctx.dv.call("POST", AUTORIZACIONCLIENTES, cuerpo)
    if est in (200, 201, 204):
        idac_dup = Dataverse.id_creado(cab)
        ctx.dv.call("DELETE", f"{AUTORIZACIONCLIENTES}({idac_dup})")
        c.check("no deja guardar una AutorizacionCliente duplicada (mismo autorizado, mismo cliente)", False,
                f"SE GUARDÓ (HTTP {est}) y se borró de inmediato: la clave alternativa "
                "sanic_mppp_key_autorizacioncliente_autorizado_cliente no está frenando el duplicado")
    else:
        c.check("no deja guardar una AutorizacionCliente duplicada (mismo autorizado, mismo cliente)", True,
                f"HTTP {est}: {r.get('error')}")
        c.check("el mensaje no es un volcado técnico crudo de la plataforma",
                "Microsoft.Crm.CrmException" not in str(r.get("error")))
    return c


CASOS = [u01, u02, u03, u04, u05, u06, u07, u08, u09, u10, u11]
