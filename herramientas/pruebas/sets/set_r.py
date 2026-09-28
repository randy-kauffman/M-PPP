#!/usr/bin/env python3
"""Set R — Reglas de nivel Registro (`pruebas/03-reglas-de-registro.md`).

Las ocho reglas que deciden fila por fila (`LISTAS_VALIDAS`, `LARGOS_Y_FORMATO`,
`PLAN_EXISTE`, `FORMATO_11_SOLO_ACH`, `OBLIGATORIEDAD`, `REFERENCIA_FORMATO_11`,
`MONEDA_DEL_PLAN`, `AUTORIZACION_CORREO_PLAN`). R-01..R-14 son la migración
literal de `corredor.py` (ya probados: se migran, no se reescriben) adaptados
al contrato de `sets/__init__.py` (`ctx.extra` en vez de atributos sueltos,
`ctx.excel_ad_hoc()` en vez de un `_armar_excel_ad_hoc` local).

**Fase 2**: investigado el 2026-09-27 (DN-01, DN-02, DN-03 en
`diseno/DIRECTRICES-NUEVAS.md`) — se agregan R-15..R-18. El porqué, caso por
candidato, y qué le corresponde a este archivo frente al set dedicado
`10-autorizacion-cliente.md` (U), está en la sección "Fase 2" de
`pruebas/03-reglas-de-registro.md` (el MD manda; acá solo se registra que la
conclusión ya se tomó).

**Reagrupamiento del 2026-09-28** (`CONVENCIONES.md` §4, "La regla del
nombre"): el set `O` (`12-obligatoriedad-gestion.md`, nombrado por la
directiva DN-03 en vez de por lo que prueba) se fusionó ACÁ, como R-19..R-28
— la obligatoriedad por gestión **ES** una de las ocho reglas de Registro
(`OBLIGATORIEDAD`), probarla en otro archivo partía el motor en dos sin
necesidad. `set_o.py` desapareció; sus funciones `o01..o10` viven abajo,
renombradas `r19..r28`, con el mismo cuerpo (CONVENCIONES.md, tabla de
equivalencias completa en `pruebas/README.md`). El límite que ya explicaba la
frontera con R-18 (más abajo, junto a R-19..R-28) sigue siendo válido — ahora
es un límite DENTRO del mismo archivo, no entre dos archivos.
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

LETRA = "R"
NOMBRE = "Reglas de Registro"

# Mismo remitente que ya usan `corredor.py` y `sets/set_s.py`: es el dueño de
# las autorizaciones por PLAN del fixture compartido `PR*` (preparar_datos.py).
REMITENTE = "zz-corredor-fase1@example.com"

# DN-01: el Autorizado que este set crea a mano (preparar_datos.py NO crea
# AutorizacionCliente). NO lleva el prefijo `ZZP-` de `marca_agua` a propósito
# (mismo criterio que `sets/set_c.py::REMITENTE_DN01`): `sanic_nombre` de
# `AutorizacionCliente` es CALCULADO por un step (`<correo> → <cliente>`, pisa
# lo que se mande al crear) y el de `Autorizado` se NORMALIZA a minúsculas
# (`StepsDePlataforma.cs`, `Normalizacion.CorreoAutorizado`) — un prefijo
# `ZZP-` quedaría `zzp-` y `marca_agua.borrar_marcado()` nunca lo va a
# reconocer. Se borra por el GUID que devuelve el propio `POST` de este mismo
# proceso (la misma garantía de `Contexto.limpiar_solicitudes()`), no por
# nombre.
REMITENTE_MODOCLIENTE = "zz-corredor-set-r-dn01-modocliente@example.com"

CUMPLIDA, NOCUMPLIDA, OMITIDA = 159460001, 159460002, 159460003
ING, NOREC, DESC, RECH, ENPROC, PROC, CERR, NOESNUEVO, VENC = (159460001 + i for i in range(9))
FILA_RECH_VAL, FILA_SIN_AUT, FILA_VALIDADA = 159460001, 159460002, 159460003

TIPO_TEXTO = 159460001  # sanic_mppp_ch_tipoparametro: Texto

PARAMETROS = "sanic_mppp_tbl_parametros"
AUTORIZADOS = "sanic_mppp_tbl_autorizados"
AUTORIZACIONCLIENTES = "sanic_mppp_tbl_autorizacionclientes"

# --- de acá para abajo, constantes que traía `sets/set_o.py` (fusionado el 2026-09-28), usadas
# solo por R-19..R-28 ---
REGLAS = "sanic_mppp_tbl_reglas"

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


def _mensaje_de(filas, numero):
    for f in filas:
        if f["sanic_numerofila"] == numero:
            return f.get("sanic_mensaje") or ""
    return None


# ===========================================================================
# preparar / cerrar
# ===========================================================================
def preparar(ctx):
    """Deja listo TODO lo que el set R necesita, y nada más (CONVENCIONES §8):

    - **Datos de negocio** (fixture compartido `PR*`, el REMITENTE de siempre
      autorizado por `AutorizacionPlan`) — igual que `set_s.py`.
    - **Los 15 Excel + el adjunto que no es Excel**.
    - **`plantilla.estructura`** (encabezados que citan los mensajes de
      `LISTAS_VALIDAS`/`LARGOS_Y_FORMATO`) y **los encabezados** para armar
      esos mensajes exactos.
    - **`plantilla.listas`, CONTENIDO — banco BAC = código `007`** (no solo
      que exista): es el código de 3 dígitos con el que R-10 construye y
      compara la referencia de formato 11 (`REFERENCIA_FORMATO_11`). Con el
      código equivocado la referencia sale mal y ASÍ LLEGA A AS400
      (`diseno/PENDIENTES.md` sección B) — un código inventado pero también
      de 3 dígitos (p. ej. `102`, el caso real que destapó D-46) pasaría
      cualquier chequeo de EXISTENCIA sin que nada se note; por eso acá se
      lee el valor y se compara. Recupera la cobertura que perdió el antiguo
      P-06 al disolver el set P (decisión del aprobador 2026-09-28: la
      verificación viaja con el set que la necesita — y R es, literalmente,
      el set que prueba la referencia).
    - **`plantilla.obligatoriedad` y `autorizacion.modo` activos** — solo se
      COMPRUEBA que exista una versión activa de cada uno (§8.2: su canon es
      "la versión activa más alta", no un valor fijo que este set deba
      sembrar). R-18 lee el CONTENIDO del primero para decidir si corre su
      aserción o si reporta N-A (criterio confirmado por el aprobador, 2026-09-28; el N-A es por
      precondición de entorno, no por duda sobre el criterio); R-16/R-17 versionan
      el segundo ellos mismos, de forma transitoria, y lo devuelven a su
      estado antes de terminar (ver esos casos).
    - **DN-01: un Autorizado + una `AutorizacionCliente`** hacia el Cliente
      compartido, con una ÚNICA vía de autorización (cero `AutorizacionPlan`).
      `preparar_datos.py` NO crea `AutorizacionCliente`: sin esto, R-16/R-17
      no tendrían con qué probar el modo `cliente`. Create-if-missing (igual
      que el resto de este `preparar()`): correr el set dos veces sin que el
      anterior haya limpiado no falla, no duplica. Se borran por GUID en
      `cerrar()`, no por `marca_agua` (ver el comentario de
      `REMITENTE_MODOCLIENTE` más arriba).

    **Lo que NO prepara, y por qué:**

    - **Roles de usuarios de prueba.** Igual que `set_s.py`: los 28 casos de
      este set llaman a las Custom API directo (`simulador.py`), nunca
      impersonan una sesión de navegador.
    - **Los cinco flujos activos.** Mismo motivo que `set_s.py`.
    - **El canon de configuración global** (§8.2, `autorizacion.modo` y
      `plantilla.obligatoriedad` incluidos). Lo aplica `correr.py` antes y
      después de CADA set, no cada set por separado.
    - **Ninguna versión nueva de `plantilla.obligatoriedad`.** R-19..R-28
      (el ex set O) nunca siembran: leen la activa (`obligatoriedad_activa`,
      ya cargada arriba) o corren `dotnet test` contra la suite unitaria —
      ver la nota que abre R-19.

    Lo de arriba ya deja listo, sin ningún paso extra, lo que necesitaban
    R-19..R-28 (el ex set O, fusionado el 2026-09-28): `obligatoriedad_activa`
    ya estaba en `ctx.extra` para R-18, y ningún caso de O necesitaba roles,
    sesiones ni ningún dato que este `preparar()` no dejara listo ya.
    """
    pc.verificar_datos_negocio(ctx.dv, REMITENTE)
    pc.verificar_excels()
    ctx.extra["estructura"] = pc.verificar_estructura_plantilla(ctx.dv)
    ctx.extra["encabezados"] = sim.cargar_encabezados(ctx.dv)
    pc.verificar_bac_codigo_007(ctx.dv)
    ctx.extra["obligatoriedad_activa"] = pc.verificar_parametro_activo(ctx.dv, "plantilla.obligatoriedad")
    pc.verificar_parametro_activo(ctx.dv, "autorizacion.modo")

    cliente_id = pc.id_cliente_compartido(ctx.dv)
    autorizado_id = _crear_autorizado(ctx.dv, REMITENTE_MODOCLIENTE)
    ctx.extra["autorizado_modocliente_id"] = autorizado_id
    ctx.extra["autorizacioncliente_id"] = pc.crear_autorizacion_cliente(ctx.dv, autorizado_id, cliente_id)


def cerrar(ctx):
    """Borra, por GUID (nunca por nombre), las Solicitudes que este set creó,
    y el Autorizado/`AutorizacionCliente` privados de DN-01 que armó
    `preparar()` — la `AutorizacionCliente` primero (referencia al Autorizado
    por lookup), el Autorizado después. Por GUID directo, no por
    `marca_agua.borrar_marcado()` (ver el comentario de `REMITENTE_MODOCLIENTE`:
    ninguno de los dos puede llevar el prefijo `ZZP-` intacto). No toca el
    fixture compartido `PR*` ni ninguna `AutorizacionPlan`."""
    errores = ctx.limpiar_solicitudes()

    ac_id = ctx.extra.get("autorizacioncliente_id")
    if ac_id:
        est, r, _ = ctx.dv.call("DELETE", f"{AUTORIZACIONCLIENTES}({ac_id})")
        if est not in (200, 204):
            errores.append(f"no se pudo borrar la AutorizacionCliente {ac_id} de DN-01 (HTTP {est}): {r}")

    au_id = ctx.extra.get("autorizado_modocliente_id")
    if au_id:
        est, r, _ = ctx.dv.call("DELETE", f"{AUTORIZADOS}({au_id})")
        if est not in (200, 204):
            errores.append(f"no se pudo borrar el Autorizado {au_id} de DN-01 (HTTP {est}): {r}")

    return errores


# ===========================================================================
# DN-01 — helpers locales. `_crear_autorizacion_cliente` se consolidó en
# `preparacion_comun.crear_autorizacion_cliente()` (2026-09-28: era idéntica
# a la copia de `set_u.py`, byte a byte salvo el texto de un mensaje de
# error). `_id_cliente_compartido` se consolidó TAMBIÉN en
# `preparacion_comun.id_cliente_compartido()` el 2026-09-28 (defecto real: la
# copia de acá y su gemela en `set_u.py`/`set_c.py`/`set_i.py` seguían
# buscando el Cliente por `sanic_nombre`, y se rompieron todas cuando la marca
# de fixture migró de "PR" a "ZZP-" — ver el docstring de esa función). Lo que
# queda acá (`_crear_autorizado`) es propio de este set.
# ===========================================================================
def _crear_autorizado(dv, correo):
    """Create-if-missing, por `sanic_nombre` (el correo, igual que
    `preparar_datos.py`)."""
    est, r, _ = dv.call("GET", f"{AUTORIZADOS}?$select=sanic_mppp_tbl_autorizadoid"
                                f"&$filter=sanic_nombre eq '{correo}'")
    if est == 200 and r.get("value"):
        return r["value"][0]["sanic_mppp_tbl_autorizadoid"]
    est, r, cab = dv.call("POST", AUTORIZADOS, {"sanic_nombre": correo})
    if est not in (200, 201, 204):
        raise RuntimeError(f"no se pudo crear el Autorizado {correo!r} (HTTP {est}): {r}")
    from dataverse_api import Dataverse
    return Dataverse.id_creado(cab)


def _mayor_version(dv, nombre):
    """La mayor `sanic_version` existente (activa o no) de ese parámetro, o 0
    si no hay ninguna. Necesario para versionar SIN chocar con la clave
    alternativa nombre+version (`playbooks/clave/sanic_mppp_key_parametro_nombre_version.md`)."""
    est, r, _ = dv.call("GET", f"{PARAMETROS}?$select=sanic_version&$filter=sanic_nombre eq '{nombre}'"
                                "&$orderby=sanic_version desc&$top=1")
    if est != 200 or not r.get("value"):
        return 0
    return r["value"][0]["sanic_version"]


def _sembrar_version_modo(dv, valor, descripcion):
    """DN-01/§8.2: versionar es AGREGAR una fila, nunca editar la vigente.
    Devuelve el id creado, para desactivarlo (nunca editarlo ni borrarlo) al
    terminar el caso que lo usó — así el canon (`plan`, la versión activa más
    alta) queda restablecido ANTES de que `correr.py` aplique el canon de
    después, y antes de que cualquier otro set pueda leerlo."""
    version = _mayor_version(dv, "autorizacion.modo") + 1
    cuerpo = {
        "sanic_nombre": "autorizacion.modo",
        "sanic_version": version,
        "sanic_tipo": TIPO_TEXTO,
        "sanic_valor": valor,
        "sanic_descripcion": descripcion,
    }
    est, r, cab = dv.call("POST", PARAMETROS, cuerpo)
    if est not in (200, 201, 204):
        raise RuntimeError(f"no se pudo sembrar autorizacion.modo v{version}={valor!r} (HTTP {est}): {r}")
    from dataverse_api import Dataverse
    return Dataverse.id_creado(cab)


def _desactivar_parametro(dv, pid):
    """Vuelve al canon (§8.2): DESACTIVA la versión de prueba. Nunca la edita,
    nunca la borra, nunca toca la versión anterior."""
    dv.call("PATCH", f"{PARAMETROS}({pid})", {"statecode": 1, "statuscode": 2})


# ===========================================================================
# Casos R-01 .. R-14 (migrados de `corredor.py`, sin reescribir su lógica)
# ===========================================================================
def r01(ctx):
    c = pc.Caso("R-01", "Valores fuera de las listas (LISTAS_VALIDAS)")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba R-01", cantidad_adjuntos=1, cantidad_excel=1,
                                     excel_a_subir=_excel("E02-listas-invalidas.xlsx"))
    filas = sim.leer_filas(ctx.dv, sid)
    c.check("5 filas, todas Rechazada en validación", len(filas) == 5 and all(f["sanic_estado"] == FILA_RECH_VAL for f in filas),
            str([(f["sanic_numerofila"], f["sanic_estado"]) for f in filas]))
    encabezados = ctx.extra["encabezados"]
    campos_esperados = {1: encabezados["gestion"], 2: encabezados["moneda"], 3: encabezados["banco"],
                        4: encabezados["tipoIdentificacion"], 5: encabezados["clasificacion"]}
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


def r02(ctx):
    c = pc.Caso("R-02", "Largos y formato (LARGOS_Y_FORMATO)")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba R-02", cantidad_adjuntos=1, cantidad_excel=1,
                                     excel_a_subir=_excel("E03-largos-invalidos.xlsx"))
    filas = sim.leer_filas(ctx.dv, sid)
    c.check("4 filas, todas Rechazada en validación", len(filas) == 4 and all(f["sanic_estado"] == FILA_RECH_VAL for f in filas),
            str([(f["sanic_numerofila"], f["sanic_estado"]) for f in filas]))
    encabezados = ctx.extra["encabezados"]
    for n, campo in ((1, "nombreBeneficiario"), (3, "numeroCuenta"), (4, "numeroIdentificacion")):
        msg = _mensaje_de(filas, n) or ""
        c.check(f"fila {n}: el mensaje nombra {encabezados[campo]!r}", encabezados[campo] in msg, msg)
    return c


def r03(ctx):
    c = pc.Caso("R-03", "Plan que no existe (PLAN_EXISTE)")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba R-03", cantidad_adjuntos=1, cantidad_excel=1,
                                     excel_a_subir=_excel("E04-plan-inexistente.xlsx"))
    filas = sim.leer_filas(ctx.dv, sid)
    c.check("1 fila, Rechazada en validación", len(filas) == 1 and filas[0]["sanic_estado"] == FILA_RECH_VAL)
    c.check("sanic_planid vacío (nunca se adivina un plan parecido)", not filas[0].get("_sanic_planid_value"))
    msg = _mensaje_de(filas, 1) or ""
    c.check("el mensaje dice que el plan no existe o no está activo", "no existe" in msg.lower() or "no está activo" in msg.lower())
    return c


def r04(ctx):
    c = pc.Caso("R-04", "Plan que existe pero está inactivo")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba R-04", cantidad_adjuntos=1, cantidad_excel=1,
                                     excel_a_subir=_excel("E15-plan-inactivo.xlsx"))
    filas = sim.leer_filas(ctx.dv, sid)
    c.check("1 fila, Rechazada en validación", len(filas) == 1 and filas[0]["sanic_estado"] == FILA_RECH_VAL)
    c.check("sanic_planid vacío", not filas[0].get("_sanic_planid_value"))
    return c


def r05(ctx):
    c = pc.Caso("R-05", "Plan formato 11 con clasificación que no es ACH")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba R-05", cantidad_adjuntos=1, cantidad_excel=1,
                                     excel_a_subir=_excel("E05-formato11-no-ach.xlsx"))
    filas = sim.leer_filas(ctx.dv, sid)
    c.check("2 filas, Rechazada en validación", len(filas) == 2 and all(f["sanic_estado"] == FILA_RECH_VAL for f in filas))
    for f in filas:
        msg = f.get("sanic_mensaje") or ""
        c.check(f"fila {f['sanic_numerofila']}: el mensaje dice 'solo admite la clasificación ACH'", "ach" in msg.lower())
    return c


def r06(ctx):
    c = pc.Caso("R-06", "Campos obligatorios vacíos, y el opcional")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba R-06", cantidad_adjuntos=1, cantidad_excel=1,
                                     excel_a_subir=_excel("E06-obligatoriedad.xlsx"))
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


def r07(ctx):
    c = pc.Caso("R-07", "Moneda distinta de la del plan")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba R-07", cantidad_adjuntos=1, cantidad_excel=1,
                                     excel_a_subir=_excel("E07-moneda-distinta.xlsx"))
    filas = sim.leer_filas(ctx.dv, sid)
    c.check("1 fila, Rechazada en validación", len(filas) == 1 and filas[0]["sanic_estado"] == FILA_RECH_VAL)
    msg = _mensaje_de(filas, 1) or ""
    c.check("el mensaje habla de la moneda del plan", "moneda" in msg.lower())
    return c


def r08(ctx):
    c = pc.Caso("R-08", "Sin autorización sobre ese plan")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba R-08", cantidad_adjuntos=1, cantidad_excel=1,
                                     excel_a_subir=_excel("E08-sin-autorizacion.xlsx"))
    filas = sim.leer_filas(ctx.dv, sid)
    c.check("1 fila, estado Sin autorización (distinto de Rechazada en validación)",
            len(filas) == 1 and filas[0]["sanic_estado"] == FILA_SIN_AUT, str(filas[0]["sanic_estado"] if filas else None))
    msg = _mensaje_de(filas, 1) or ""
    c.check("el mensaje nombra el plan PRNA", "PRNA" in msg, msg)
    s = sim.leer_solicitud(ctx.dv, sid)
    c.check("Estado de la Solicitud = Rechazada", s.get("sanic_estadoprocesamiento") == RECH)
    return c


def r09(ctx):
    c = pc.Caso("R-09", "Sin autorización GANA sobre otra regla que rechaza (D-14)")
    ruta = ctx.excel_ad_hoc("ZZ-R09-sinautorizacion-moneda.xlsx",
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


def r10(ctx):
    c = pc.Caso("R-10", "La referencia de formato 11 se construye sola (la prueba más importante del plan)")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba R-10", cantidad_adjuntos=1, cantidad_excel=1,
                                     excel_a_subir=_excel("E01-feliz-formato11.xlsx"))
    filas = sim.leer_filas(ctx.dv, sid)
    esperadas = {1: "00700010203040506070", 2: "00700011223344556677", 3: "00700099887766554433"}
    for f in filas:
        n = f["sanic_numerofila"]
        ref = f.get("sanic_referencia")
        c.check(f"fila {n}: referencia construida EXACTA (007 + cuenta rellena a 17 = 20)", ref == esperadas.get(n), f"obtenida {ref!r}")
        c.check(f"fila {n}: referencia mide exactamente 20", len(ref or "") == 20)
        c.check(f"fila {n}: referenciarecibida vacía (el Excel no trae nada en esa columna)", not f.get("sanic_referenciarecibida"))
    return c


def r11(ctx):
    c = pc.Caso("R-11", "La referencia de formato 11 que NO se puede construir")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba R-11", cantidad_adjuntos=1, cantidad_excel=1,
                                     excel_a_subir=_excel("E09-referencia-formato11.xlsx"))
    filas = sim.leer_filas(ctx.dv, sid)
    c.check("2 filas, ambas Rechazada en validación", len(filas) == 2 and all(f["sanic_estado"] == FILA_RECH_VAL for f in filas),
            str([(f["sanic_numerofila"], f["sanic_estado"]) for f in filas]))
    return c


def r12(ctx):
    c = pc.Caso("R-12", "Formato 06: la referencia NO se deriva")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba R-12", cantidad_adjuntos=1, cantidad_excel=1,
                                     excel_a_subir=_excel("E13-formato06-referencia-libre.xlsx"))
    filas = sim.leer_filas(ctx.dv, sid)
    c.check("1 fila, Validada", len(filas) == 1 and filas[0]["sanic_estado"] == FILA_VALIDADA)
    if filas:
        c.check("referencia = REF-LIBRE-000123 (tal cual, sin 007 ni ceros)", filas[0].get("sanic_referencia") == "REF-LIBRE-000123",
                f"obtenida {filas[0].get('sanic_referencia')!r}")
        c.check("referenciarecibida = REF-LIBRE-000123", filas[0].get("sanic_referenciarecibida") == "REF-LIBRE-000123")
    return c


def r13(ctx):
    c = pc.Caso("R-13", "Mezcla de filas válidas y rechazadas")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba R-13", cantidad_adjuntos=1, cantidad_excel=1,
                                     excel_a_subir=_excel("E10-mixto.xlsx"))
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


def r14(ctx):
    c = pc.Caso("R-14", "Duplicados dentro de la plantilla NO se validan (DD-12)")
    fila_repetida = genexcel.fila(nombre="Fila Duplicada Prueba", cuenta="10203040506070")
    ruta = ctx.excel_ad_hoc("ZZ-R14-duplicados.xlsx", [fila_repetida, fila_repetida, fila_repetida])
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


# ===========================================================================
# Casos R-15 .. R-18 — fase 2 (DN-01, DN-03)
# ===========================================================================
def r15(ctx):
    c = pc.Caso("R-15", "DN-01: `autorizacion.modo` en su canon (`plan`) antes de correr las reglas de Registro")
    fila = pc.verificar_parametro_activo(ctx.dv, "autorizacion.modo")
    valor = (fila.get("sanic_valor") or "").strip()
    c.check("autorizacion.modo activo = 'plan' (canon, CONVENCIONES §8.2)",
            valor.lower() == "plan", f"obtenido {valor!r}")
    return c


def r16(ctx):
    c = pc.Caso("R-16", "DN-01: modo `cliente` — autorizado SOLO por AutorizacionCliente valida sus filas")
    pid = _sembrar_version_modo(ctx.dv, "cliente", "ZZP- prueba set R (R-16): modo cliente")
    try:
        sid, mid, rc, rv = ctx.procesar(
            remitente=REMITENTE_MODOCLIENTE, asunto="Prueba R-16 modo cliente",
            cantidad_adjuntos=1, cantidad_excel=1, excel_a_subir=_excel("E01-feliz-formato11.xlsx"))
        c.check("REMITENTE_RECONOCIDO = Cumplida (nivel Correo, mismo punto de consulta que Registro)",
                rc.get("clasificacion") == "nuevo" and rc.get("procesar") is True, str(rc))
        if rv is None:
            c.check("se llegó a validar (no quedó 'No reconocida')", False, str(rc))
            return c
        filas = sim.leer_filas(ctx.dv, sid)
        c.check("las 3 filas quedan Validada (AUTORIZACION_CORREO_PLAN resuelve por AutorizacionCliente, "
                "sin enumerar el plan)",
                len(filas) == 3 and all(f["sanic_estado"] == FILA_VALIDADA for f in filas),
                str([(f["sanic_numerofila"], f["sanic_estado"], f.get("sanic_mensaje")) for f in filas]))
    finally:
        _desactivar_parametro(ctx.dv, pid)
    return c


def _r17_verificar_remitente_por_plan(ctx, c):
    """La segunda mitad de R-17: el correo autorizado por PLAN (`REMITENTE`, de siempre) sigue
    entrando igual bajo el valor basura. Nunca la bloquea D-59: `REMITENTE` queda reconocido
    (`procesar=True`), así que jamás entra a la rama de `ClasificarCorreo` que dispara el aviso a
    ejecutivos."""
    sid2, mid2, rc2, rv2 = ctx.procesar(
        remitente=REMITENTE, asunto="Prueba R-17 basura (por plan, de siempre)",
        cantidad_adjuntos=1, cantidad_excel=1, excel_a_subir=_excel("E01-feliz-formato11.xlsx"))
    c.check("el correo autorizado por PLAN sigue reconocido y procesado (el valor basura no tumbó nada)",
            rc2.get("procesar") is True, str(rc2))
    if rv2 is not None:
        filas2 = sim.leer_filas(ctx.dv, sid2)
        c.check("sus 3 filas siguen Validada, igual que siempre",
                len(filas2) == 3 and all(f["sanic_estado"] == FILA_VALIDADA for f in filas2),
                str([(f["sanic_numerofila"], f["sanic_estado"]) for f in filas2]))


def r17(ctx):
    c = pc.Caso("R-17", "DN-01: valor basura en `autorizacion.modo` degrada a `plan`, sin tumbar la operación")
    pid = _sembrar_version_modo(ctx.dv, "clienteX", "ZZP- prueba set R (R-17): valor basura")
    try:
        sid1, mid1 = ctx.solicitud(
            remitente=REMITENTE_MODOCLIENTE, asunto="Prueba R-17 basura (solo cliente)",
            cantidad_adjuntos=1, cantidad_excel=1, excel_a_subir=_excel("E01-feliz-formato11.xlsx"))
        try:
            rc1 = sim.clasificar(ctx.dv, sid1)
        except RuntimeError as e:
            if "No se pudo clasificar el correo" in str(e):
                # D-59 (diseno/PENDIENTES.md, ABIERTA, pospuesta por el aprobador 2026-09-27):
                # `AvisosDataverse.AvisarAEjecutivos` revienta con `InvalidCastException`
                # (Guid -> EntityReference sobre `systemuserroles`) CADA VEZ que una Solicitud
                # termina en un estado que dispara el aviso a ejecutivos (respuesta, ilegible,
                # remitente no reconocido) — exactamente el camino que esta mitad del caso
                # necesita ejercitar. Confirmado en `plugintracelog` de Dev el 2026-09-28 (mismo
                # stack que documenta D-59, y reproducido también con un Autorizado sin ninguna
                # relación con DN-01: no es un bug nuevo de este caso, es D-59 de siempre).
                #
                # CONVENCIONES §3, "Un defecto conocido no es N-A": un caso bloqueado por un
                # defecto YA REGISTRADO es FALLA, aunque el defecto no sea el tema del caso y ya
                # tenga su ID en PENDIENTES.md — el mismo criterio que ya aplicó el set C en
                # C-02/C-04/C-10/C-11 contra este mismo D-59. Nunca N-A: N-A es para límites del
                # entorno, no para el sistema fallando.
                c.check(f"bloqueado por D-59 (diseno/PENDIENTES.md): {e}", False,
                        "AvisosDataverse.AvisarAEjecutivos revienta con InvalidCastException "
                        "(Guid -> EntityReference sobre systemuserroles) al clasificar un correo "
                        "que termina 'No reconocida'; la transacción entera se revierte, así que "
                        "tampoco queda 'No reconocida' persistida para comparar con la segunda mitad")
                # Se intenta igual la segunda mitad — da evidencia extra, pero el caso YA es
                # FALLA por el check de arriba, pase lo que pase acá.
                _r17_verificar_remitente_por_plan(ctx, c)
                return c
            raise
        c.check("el correo SOLO autorizado por cliente queda No reconocido bajo el valor basura "
                "(degradó a plan, y por plan no tiene autorización)",
                rc1.get("clasificacion") == "remitente_no_reconocido" and rc1.get("procesar") is False, str(rc1))
        s1 = sim.leer_solicitud(ctx.dv, sid1)
        c.check("Estado = No reconocida", s1.get("sanic_estadoprocesamiento") == NOREC,
                str(s1.get("sanic_estadoprocesamiento")))

        _r17_verificar_remitente_por_plan(ctx, c)
    finally:
        _desactivar_parametro(ctx.dv, pid)
    return c


def r18(ctx):
    c = pc.Caso("R-18", "DN-03: `Exclusion` afloja numeroCuenta/banco/moneda")
    activo = ctx.extra["obligatoriedad_activa"]
    valor = activo.get("sanic_valor") or ""
    if "porGestion" not in valor or '"Exclusion"' not in valor:
        c.na("plantilla.obligatoriedad activo no trae 'porGestion.Exclusion' en este entorno: el "
             "criterio ya está confirmado por el aprobador (2026-09-28, diseno/DIRECTRICES-NUEVAS.md, "
             "DN-03) — esto es una precondición de entorno (podría faltar sembrar acá), no una duda "
             f"sobre el criterio. Valor activo: {valor!r}")
        return c

    fila_dict = genexcel.fila(gestion="Exclusion", plan="PR06", clasificacion="ACH",
                               nombre="Exclusion Prueba R-18", cuenta="", banco="", moneda="")
    ruta = ctx.excel_ad_hoc("ZZ-R18-exclusion-aflojada.xlsx", [fila_dict])
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba R-18", cantidad_adjuntos=1,
                                     cantidad_excel=1, excel_a_subir=ruta)
    filas = sim.leer_filas(ctx.dv, sid)
    c.check("1 fila, Validada (numeroCuenta/banco/moneda vacíos NO la rechazan)",
            len(filas) == 1 and filas[0]["sanic_estado"] == FILA_VALIDADA,
            str([(f["sanic_numerofila"], f["sanic_estado"], f.get("sanic_mensaje")) for f in filas]))
    if filas and filas[0]["sanic_estado"] != FILA_VALIDADA:
        msg = (filas[0].get("sanic_mensaje") or "").lower()
        c.check("si rechazó, que NO sea citando numeroCuenta/banco/moneda (serían las reglas viejas, no DN-03)",
                not any(x in msg for x in ("cuenta", "banco", "moneda")), msg)
    return c


# ===========================================================================
# `dotnet test` — evidencia unitaria para R-22, R-24 y R-25 (nunca contra Dev). Migrado tal cual de
# `sets/set_o.py` (fusionado el 2026-09-28).
# ===========================================================================
def _dotnet_test(filtro, timeout=240):
    """Corre `dotnet test` con un filtro EXACTO/parcial contra la suite de aceptación C#
    (`tests/Sanic.Mppp.Plugins.Tests`). Es la evidencia que R-19/R-22/R-24/R-25 usan en vez de
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
# R-19 a R-28 — DN-03: cobertura EXHAUSTIVA de la obligatoriedad por gestión (el ex set O,
# `pruebas/12-obligatoriedad-gestion.md`, fusionado acá el 2026-09-28 — CONVENCIONES.md §4, "La
# regla del nombre": la obligatoriedad por gestión ES una de las ocho reglas de Registro, no un
# tema aparte). `set_o.py` desapareció; estas diez funciones son `o01..o10` renombradas `r19..r28`,
# mismo cuerpo, IDs y cruces internos actualizados.
#
# `plantilla.obligatoriedad` es un parámetro GLOBAL (CONVENCIONES §8.2): sembrar una versión nueva
# la vuelve la ACTIVA de inmediato, para toda Solicitud que se valide mientras tanto, en TODO el
# entorno. **Ninguno de estos diez casos escribe sobre esa tabla.** Ni un solo `POST`/`PATCH` sobre
# `sanic_mppp_tbl_parametros`:
#
# - R-19, R-20, R-26 leen lo YA sembrado (versión 1 y versión 2 activas hoy), nunca lo cambian.
# - R-21, R-23, R-27 mandan Excel contra las Custom API, pero ejercitan gestiones/escenarios que la
#   versión YA activa cubre (nunca siembran una versión nueva para forzar un escenario).
# - R-22 (precedencia), R-24 (claves de `porGestion` sin distinguir caja/tildes) y R-25 (clave
#   desconocida dentro de `porGestion`) son los tres escenarios de mayor riesgo del diseño: los tres
#   se verifican corriendo `dotnet test` contra la suite ya construida con TDD
#   (`tests/Sanic.Mppp.Plugins.Tests/Aceptacion/ParametrosDePlantillaAceptacion.cs`), nunca sembrando
#   nada en Dev.
# - R-28 lee el catálogo de Reglas (`sanic_mppp_tbl_reglas`), no toca nada tampoco.
#
# **Frontera con R-18, ahora dentro del mismo archivo.** R-18 (arriba) ya manda un Excel real con
# `gestion=Exclusion` y `numeroCuenta`/`banco`/`moneda` en blanco contra las Custom API en vivo, y
# confirma que la fila queda Validada — la prueba EN VIVO de que `porGestion.Exclusion` funciona,
# dentro del motor de las ocho reglas. Estos diez casos NO repiten ese envío (sería el mismo Excel,
# el mismo resultado, la misma evidencia con otro ID): R-20 verifica, de solo lectura, que el
# CONTENIDO sembrado es el que el aprobador pidió (los tres campos exactos), que es la mitad que
# R-18 no cubre; R-21/R-23/R-27 ejercitan escenarios que R-18 no toca (`Inclusion`/`Modificacion`
# sin aflojar, una gestión inválida en la FILA, el mensaje al cliente); R-22/R-24/R-25 son los de
# alto riesgo o precedencia, que nunca se siembran en Dev; R-26 es la evidencia de que versionar
# agrega, no edita.
# ===========================================================================
def r19(ctx):
    c = pc.Caso("R-19", "Compatibilidad: un parámetro SIN `porGestion` se comporta igual que antes de DN-03")
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
        c.check("su sanic_valor sigue siendo EXACTAMENTE el de DD-16 (nunca se editó, ver R-26)",
                valor == VALOR_V1_CANONICO, f"obtenido {valor!r}")
    return c


def r20(ctx):
    c = pc.Caso("R-20", "El contenido sembrado de `porGestion.Exclusion` es exactamente el pedido")
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
    c.check("ninguna otra gestión tiene entrada en porGestion todavía (ver R-21)",
            not otras, f"obtenido {sorted(otras)}")
    return c


def r21(ctx):
    c = pc.Caso("R-21", "`Inclusion` y `Modificacion` NO tienen entrada en `porGestion` todavía")
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

    ruta = ctx.excel_ad_hoc("ZZ-R21-inclusion-modificacion-sin-aflojar.xlsx", [
        genexcel.fila(gestion="Inclusion", plan="PR11", clasificacion="ACH",
                      nombre="Inclusion Prueba R-21", cuenta="", banco="", moneda=""),
        genexcel.fila(gestion="Modificacion", plan="PR11", clasificacion="ACH",
                      nombre="Modificacion Prueba R-21", cuenta="", banco="", moneda=""),
    ])
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba R-21", cantidad_adjuntos=1,
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


def r22(ctx):
    c = pc.Caso("R-22", "Precedencia `obligatorios` sobre `opcionales` (cobertura unitaria, NO se siembra en Dev)")
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


def r23(ctx):
    c = pc.Caso("R-23", "Gestión inválida en la FILA: `OBLIGATORIEDAD` ni siquiera se evalúa (depende de LISTAS_VALIDAS)")
    ruta = ctx.excel_ad_hoc("ZZ-R23-gestion-invalida-cuenta-banco-vacios.xlsx", [
        genexcel.fila(gestion="Baja", plan="PR11", clasificacion="ACH",
                      nombre="Gestion Invalida Prueba R-23", cuenta="", banco=""),
    ])
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba R-23", cantidad_adjuntos=1,
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


def r24(ctx):
    c = pc.Caso("R-24", "Las CLAVES de `porGestion` (dentro del parámetro) no distinguen caja ni tildes (unitario, no Dev)")
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


def r25(ctx):
    c = pc.Caso("R-25", "Gestión desconocida DENTRO de `porGestion` revienta como error de configuración (alto riesgo, no sembrar en Dev)")
    fuente = _leer_fuente_tests(c)
    if fuente is None:
        return c
    c.check("existe un caso unitario con una gestión desconocida DENTRO de `porGestion` "
            "(distinto de R-23, que es sobre el valor de la FILA)", "Bajas" in fuente)

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


def r26(ctx):
    c = pc.Caso("R-26", "Versionar es AGREGAR una fila, nunca editar la vigente")
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


def r27(ctx):
    c = pc.Caso("R-27", "El mensaje al cliente no cambia con DN-03 (sigue nombrando el CAMPO, nunca la gestión)")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba R-27", cantidad_adjuntos=1,
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


def r28(ctx):
    c = pc.Caso("R-28", "La regla `OBLIGATORIEDAD` no cambia de código, orden, nivel ni dependencias")
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


CASOS = [r01, r02, r03, r04, r05, r06, r07, r08, r09, r10, r11, r12, r13, r14, r15, r16, r17, r18,
         r19, r20, r21, r22, r23, r24, r25, r26, r27, r28]
