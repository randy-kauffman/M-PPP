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

**DN-05 (2026-10-06, desplegado en Dev con el ensamblado 1.0.23.0)**: reescribe R-10, R-12, R-18, R-20 y R-21
contra la matriz de obligatoriedad v3 y los formatos del plan, y agrega R-29..R-38 (DN-05) R-39..R-41 (DN-06) y R-42 (DN-07) (un caso por comportamiento
nuevo). Las reglas nuevas de fila son `GESTION_DEL_FORMATO`, `CLASIFICACION_DEL_FORMATO` (reemplaza a
`FORMATO_11_SOLO_ACH`, que quedó inactiva), `BANCO_DEL_FORMATO`, `CARACTERES_PERMITIDOS` y `CUENTA_DEL_BANCO`
(`diseno/DIRECTRICES-NUEVAS.md`, DN-05).

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

# Valores de los choices de la fila (`playbooks/choice-global/`): clasificación y banco.
CLASIF_BAC, CLASIF_ACH, CLASIF_CK = 159460001, 159460002, 159460003
BANCO_LAFISE, BANCO_BAC, BANCO_ATLANTIDA = 159460001, 159460002, 159460008

TIPO_TEXTO = 159460001  # sanic_mppp_ch_tipoparametro: Texto

PARAMETROS = "sanic_mppp_tbl_parametros"
AUTORIZADOS = "sanic_mppp_tbl_autorizados"
AUTORIZACIONCLIENTES = "sanic_mppp_tbl_autorizacionclientes"

# --- de acá para abajo, constantes que traía `sets/set_o.py` (fusionado el 2026-09-28), usadas
# solo por R-19..R-28 ---
REGLAS = "sanic_mppp_tbl_reglas"

NIVEL_REGISTRO = 159460002
EFECTO_RECHAZA = 159460001

# DN-05: el contenido EXACTO sembrado de `plantilla.obligatoriedad` v3 (`playbooks/datos/parametros.md`), como
# estructura JSON (no texto crudo: no depende de espacios ni del orden de las claves).
_CAMPOS_DE_IDENTIFICACION = ["clasificacion", "nombreBeneficiario", "tipoIdentificacion", "numeroIdentificacion", "banco"]
VALOR_V3_CANONICO = {
    "porDefecto": "obligatorio",
    "opcionales": [{"campo": "referencia"}, {"campo": "moneda"}],
    "reglas": [
        {"gestion": "Inclusion", "clasificacion": "*", "formato": "06",
         "opcionales": ["clasificacion", "tipoIdentificacion", "banco"]},
        {"gestion": "Inclusion", "clasificacion": "*", "formato": "10", "opcionales": ["banco"]},
        {"gestion": "Inclusion", "clasificacion": "*", "formato": "11", "opcionales": ["clasificacion"]},
    ],
    "porGestion": {
        "Exclusion": {"opcionales": [{"campo": c} for c in _CAMPOS_DE_IDENTIFICACION],
                      "obligatorios": [{"campo": "referencia"}]},
        "Modificacion": {"opcionales": [{"campo": c} for c in _CAMPOS_DE_IDENTIFICACION],
                         "obligatorios": [{"campo": "referencia"}]},
    },
}

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


def _faltan(encabezados, *campos):
    """El mensaje EXACTO de OBLIGATORIEDAD para esos campos (en el orden de la fila)."""
    return "Faltan datos obligatorios: " + ", ".join(encabezados[c] for c in campos) + "."


def _enviar(ctx, asunto, archivo, filas):
    """Arma el Excel ad hoc, lo procesa con el REMITENTE de siempre y devuelve (sid, filas leídas)."""
    ruta = ctx.excel_ad_hoc(archivo, filas)
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto=asunto, cantidad_adjuntos=1, cantidad_excel=1,
                                     excel_a_subir=ruta)
    return sid, sim.leer_filas(ctx.dv, sid)


def _resumen(filas):
    return str([(f["sanic_numerofila"], f["sanic_estado"], f.get("sanic_mensaje")) for f in filas])


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
    c = pc.Caso("R-05", "Plan formato 11 con clasificación que no es ACH (CLASIFICACION_DEL_FORMATO, DN-05)")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba R-05", cantidad_adjuntos=1, cantidad_excel=1,
                                     excel_a_subir=_excel("E05-formato11-no-ach.xlsx"))
    filas = sim.leer_filas(ctx.dv, sid)
    c.check("2 filas, Rechazada en validación", len(filas) == 2 and all(f["sanic_estado"] == FILA_RECH_VAL for f in filas))
    for f in filas:
        msg = f.get("sanic_mensaje") or ""
        c.check(f"fila {f['sanic_numerofila']}: el mensaje es exactamente el de CLASIFICACION_DEL_FORMATO (DN-05)",
                msg == "El plan PR11 solo admite la clasificación ACH.", msg)
    return c


def r06(ctx):
    c = pc.Caso("R-06", "Campos obligatorios vacíos de una Inclusión en formato 11, y el opcional")
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
    c = pc.Caso("R-10", "La referencia de formato 11 se construye sola en la Inclusión (la prueba más importante del plan)")
    sid, mid, rc, rv = ctx.procesar(remitente=REMITENTE, asunto="Prueba R-10", cantidad_adjuntos=1, cantidad_excel=1,
                                     excel_a_subir=_excel("E01-feliz-formato11.xlsx"))
    filas = sim.leer_filas(ctx.dv, sid)
    c.check("3 filas, todas Validada (E01: tres Inclusiones en PR11, DN-05)",
            len(filas) == 3 and all(f["sanic_estado"] == FILA_VALIDADA for f in filas), _resumen(filas))
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
        c.check("referencia = REFLIBRE000123 (tal cual, sin 007 ni ceros)", filas[0].get("sanic_referencia") == "REFLIBRE000123",
                f"obtenida {filas[0].get('sanic_referencia')!r}")
        c.check("referenciarecibida = REFLIBRE000123", filas[0].get("sanic_referenciarecibida") == "REFLIBRE000123")
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
    c = pc.Caso("R-18", "DN-05: el criterio de `Exclusion` de la v3 (cuenta y referencia obligatorias, el resto opcional)")
    activo = ctx.extra["obligatoriedad_activa"]
    valor = activo.get("sanic_valor") or ""
    if "porGestion" not in valor or '"Exclusion"' not in valor:
        c.na("plantilla.obligatoriedad activo no trae 'porGestion.Exclusion' en este entorno: es una precondición "
             "de entorno (falta sembrar la v3, DN-05), no una duda sobre el criterio. "
             f"Valor activo: {valor!r}")
        return c

    ref = "REFEXCL0006"
    filas_excel = [
        # 1: lo mínimo de una exclusión (plan, referencia, cuenta); todo lo demás vacío. PR06 completa clasificación y banco.
        genexcel.fila(gestion="Exclusion", plan="PR06", clasificacion="", nombre="", tipoid="", numeroid="",
                      referencia=ref, cuenta="10203040506070", moneda="", banco=""),
        # 2: sin cuenta.
        genexcel.fila(gestion="Exclusion", plan="PR06", clasificacion="", nombre="", tipoid="", numeroid="",
                      referencia=ref, cuenta="", moneda="", banco=""),
        # 3: sin referencia.
        genexcel.fila(gestion="Exclusion", plan="PR06", clasificacion="", nombre="", tipoid="", numeroid="",
                      referencia="", cuenta="10203040506070", moneda="", banco=""),
    ]
    sid, filas = _enviar(ctx, "Prueba R-18", "ZZ-R18-exclusion-aflojada.xlsx", filas_excel)
    estados = {f["sanic_numerofila"]: f["sanic_estado"] for f in filas}
    encabezados = ctx.extra["encabezados"]
    c.check("fila 1 Validada (identificación, nombre, tipo ID, clasificación, banco y moneda vacíos NO la rechazan)",
            len(filas) == 3 and estados.get(1) == FILA_VALIDADA, _resumen(filas))
    c.check("fila 2 (sin cuenta) Rechazada en validación, y el mensaje nombra SOLO la cuenta",
            estados.get(2) == FILA_RECH_VAL and _mensaje_de(filas, 2) == _faltan(encabezados, "numeroCuenta"),
            _resumen(filas))
    c.check("fila 3 (sin referencia) Rechazada en validación, y el mensaje nombra SOLO la referencia",
            estados.get(3) == FILA_RECH_VAL and _mensaje_de(filas, 3) == _faltan(encabezados, "referencia"),
            _resumen(filas))
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
# - R-19, R-20, R-26 leen lo YA sembrado (hoy solo la v3 activa; v1 y v2 existen inactivas), nunca lo cambian.
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
    c = pc.Caso("R-20", "El contenido sembrado de `plantilla.obligatoriedad` v3 es exactamente el de la matriz del negocio")
    est, r, _ = ctx.dv.call(
        "GET",
        f"{PARAMETROS}?$select=sanic_valor,statecode&$filter=sanic_nombre eq 'plantilla.obligatoriedad'"
        " and sanic_version eq 3")
    filas = r.get("value", []) if est == 200 else []
    if not filas:
        c.na("plantilla.obligatoriedad no tiene versión 3 en este entorno: es una precondición de entorno "
             f"(falta sembrar DN-05, `playbooks/datos/parametros.md`), no una duda sobre el criterio (HTTP {est}).")
        return c
    c.check("la versión 3 está activa", filas[0].get("statecode") == 0, str(filas[0].get("statecode")))
    try:
        datos = json.loads(filas[0].get("sanic_valor") or "{}")
    except json.JSONDecodeError as e:
        c.check("el valor de la v3 es JSON válido", False, str(e))
        return c

    c.check("la v3 es EXACTAMENTE la sembrada (porDefecto, opcionales, reglas por formato y porGestion), "
            "como estructura JSON", datos == VALOR_V3_CANONICO, f"obtenido {datos!r}")

    # Lo que decidió el aprobador, nombrado campo por campo: si el JSON de arriba cambia, estos dicen QUÉ.
    por_gestion = datos.get("porGestion") or {}
    for gestion in ("Exclusion", "Modificacion"):
        entrada = por_gestion.get(gestion) or {}
        opcionales = {o.get("campo") for o in (entrada.get("opcionales") or [])}
        obligatorios = {o.get("campo") for o in (entrada.get("obligatorios") or [])}
        c.check(f"{gestion}: opcionales = clasificación, nombre, tipo ID, identificación y banco",
                opcionales == set(_CAMPOS_DE_IDENTIFICACION), f"obtenido {sorted(opcionales)}")
        c.check(f"{gestion}: la referencia es obligatoria (cuenta y plan lo son por el bloque global)",
                obligatorios == {"referencia"}, f"obtenido {sorted(obligatorios)}")
    c.check("el bloque global deja opcionales la referencia y la moneda, y nada más",
            {o.get("campo") for o in datos.get("opcionales", [])} == {"referencia", "moneda"})
    return c


def r21(ctx):
    c = pc.Caso("R-21", "DN-05: `Inclusion` tiene sus `reglas` por formato y `Modificacion` su entrada en `porGestion`")
    activo = ctx.extra["obligatoriedad_activa"]
    try:
        datos = json.loads(activo.get("sanic_valor") or "{}")
    except json.JSONDecodeError as e:
        c.check("el valor activo es JSON válido", False, str(e))
        return c

    # Solo lectura: las dos entradas que antes de DN-05 no existían.
    reglas = {(str(r.get("gestion")), str(r.get("formato"))): set(r.get("opcionales") or [])
              for r in (datos.get("reglas") or [])}
    c.check("Inclusion·06 aflojan clasificación, tipo ID y banco",
            reglas.get(("Inclusion", "06")) == {"clasificacion", "tipoIdentificacion", "banco"}, str(reglas))
    c.check("Inclusion·10 afloja solo el banco", reglas.get(("Inclusion", "10")) == {"banco"}, str(reglas))
    c.check("Inclusion·11 afloja solo la clasificación", reglas.get(("Inclusion", "11")) == {"clasificacion"}, str(reglas))
    claves = {k.strip().lower() for k in (datos.get("porGestion") or {})}
    c.check("porGestion trae Exclusion y Modificacion (y ninguna Inclusion: esa va por `reglas`)",
            claves == {"exclusion", "modificacion"}, str(sorted(claves)))

    # En vivo: PR06 admite las tres gestiones; PR10 y PR11 sirven para lo obligatorio de la Inclusión.
    encabezados = ctx.extra["encabezados"]
    filas_excel = [
        # 1: Inclusión 06 con clasificación, tipo ID, banco y moneda vacíos: Validada.
        genexcel.fila(gestion="Inclusion", plan="PR06", clasificacion="", nombre="Inclusion Seis Prueba",
                      tipoid="", moneda="", banco=""),
        # 2: Inclusión 10 sin clasificación: obligatoria (el formato admite dos, no se completa).
        genexcel.fila(gestion="Inclusion", plan="PR10", clasificacion="", nombre="Inclusion Diez Prueba",
                      moneda="USD", banco=""),
        # 3: Inclusión 11 sin banco: obligatorio.
        genexcel.fila(gestion="Inclusion", plan="PR11", clasificacion="ACH", nombre="Inclusion Once Prueba", banco=""),
        # 4: Modificación 06 con solo plan, referencia y cuenta: Validada.
        genexcel.fila(gestion="Modificacion", plan="PR06", clasificacion="", nombre="", tipoid="", numeroid="",
                      referencia="REFMODI0001", cuenta="10203040506070", moneda="", banco=""),
        # 5: Modificación 06 sin referencia: obligatoria.
        genexcel.fila(gestion="Modificacion", plan="PR06", clasificacion="", nombre="", tipoid="", numeroid="",
                      referencia="", cuenta="10203040506070", moneda="", banco=""),
    ]
    sid, filas = _enviar(ctx, "Prueba R-21", "ZZ-R21-inclusion-modificacion-sin-aflojar.xlsx", filas_excel)
    estados = {f["sanic_numerofila"]: f["sanic_estado"] for f in filas}
    c.check("5 filas leídas", len(filas) == 5, _resumen(filas))
    c.check("fila 1 (Inclusión 06, sin clasificación/tipo ID/banco/moneda) Validada",
            estados.get(1) == FILA_VALIDADA, _resumen(filas))
    c.check("fila 2 (Inclusión 10 sin clasificación) rechazada: 'Faltan datos obligatorios: Clasificación.'",
            estados.get(2) == FILA_RECH_VAL and _mensaje_de(filas, 2) == _faltan(encabezados, "clasificacion"),
            _resumen(filas))
    c.check("fila 3 (Inclusión 11 sin banco) rechazada: 'Faltan datos obligatorios: Banco.'",
            estados.get(3) == FILA_RECH_VAL and _mensaje_de(filas, 3) == _faltan(encabezados, "banco"),
            _resumen(filas))
    c.check("fila 4 (Modificación 06, solo plan/referencia/cuenta) Validada",
            estados.get(4) == FILA_VALIDADA, _resumen(filas))
    c.check("fila 5 (Modificación 06 sin referencia) rechazada: 'Faltan datos obligatorios: Referencia.'",
            estados.get(5) == FILA_RECH_VAL and _mensaje_de(filas, 5) == _faltan(encabezados, "referencia"),
            _resumen(filas))
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


VALOR_V2_CANONICO = {
    "porDefecto": "obligatorio",
    "opcionales": [{"campo": "referencia"}],
    "porGestion": {"Exclusion": {"opcionales": [{"campo": "numeroCuenta"}, {"campo": "banco"}, {"campo": "moneda"}]}},
}


def r26(ctx):
    c = pc.Caso("R-26", "Versionar agrega una fila y desactiva la anterior: UNA sola versión activa por parámetro (DD-02)")
    est, r, _ = ctx.dv.call(
        "GET", f"{PARAMETROS}?$select=sanic_nombre,sanic_version,sanic_valor,statecode&$orderby=sanic_nombre asc,sanic_version asc")
    todas = r.get("value", []) if est == 200 else []
    c.check("se leyó toda la tabla de parámetros", est == 200 and len(todas) > 0, f"HTTP {est}")

    obl = {f.get("sanic_version"): f for f in todas if f.get("sanic_nombre") == "plantilla.obligatoriedad"}
    c.check("plantilla.obligatoriedad tiene las versiones 1, 2 y 3", {1, 2, 3} <= set(obl), f"hay {sorted(obl)}")
    c.check("de plantilla.obligatoriedad SOLO la versión 3 está activa",
            sorted(v for v, f in obl.items() if f.get("statecode") == 0) == [3],
            str({v: f.get("statecode") for v, f in obl.items()}))
    for version, canonico in ((1, VALOR_V1_CANONICO), (2, VALOR_V2_CANONICO)):
        fila = obl.get(version)
        try:
            valor = json.loads(fila.get("sanic_valor") or "{}") if fila else None
        except json.JSONDecodeError:
            valor = None
        c.check(f"la versión {version} conserva EXACTO su sanic_valor (como JSON; versionar nunca edita)",
                fila is not None and valor == canonico, f"obtenido {valor!r}")

    por_nombre = {}
    for f in todas:
        por_nombre.setdefault(f.get("sanic_nombre"), []).append(f)
    # En TODA la tabla: exactamente UNA versión activa por nombre (DD-02). No se exige que sea la más alta: los sets R y U
    # siembran versiones temporales de `autorizacion.modo` (y antes de `rpa.puedeaprobar`) y las desactivan al terminar,
    # así que en esos parámetros de arnés la activa legítima puede no ser la mayor. Para un parámetro versionado por
    # negocio, "la activa es la más alta" se comprueba arriba (plantilla.obligatoriedad).
    for nombre, filas in sorted(por_nombre.items()):
        activas = sorted(f.get("sanic_version") for f in filas if f.get("statecode") == 0)
        c.check(f"{nombre}: exactamente una versión activa", len(activas) == 1, f"activas {activas}")
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
    # DN-06 (2026-11-24): pasó a depender de PLAN_REQUIERE_PREREGISTRO, que a su vez depende de la autorización.
    c.check("depende de PLAN_REQUIERE_PREREGISTRO,LISTAS_VALIDAS (DN-06)",
            regla.get("sanic_dependede") == "PLAN_REQUIERE_PREREGISTRO,LISTAS_VALIDAS", str(regla))
    c.check("está activa", regla.get("statecode") == 0, str(regla))
    return c


# ===========================================================================
# R-29 .. R-38 — DN-05 (2026-10-06): un caso por comportamiento nuevo. Cada uno arma su Excel ad hoc y su
# Solicitud, y cada fila falla SOLO por la regla que prueba (o por ninguna).
# ===========================================================================
def r29(ctx):
    c = pc.Caso("R-29", "DN-05, escenario 9: clasificación ACH en un plan 06 (CLASIFICACION_DEL_FORMATO)")
    sid, filas = _enviar(ctx, "Prueba R-29", "ZZ-R29-ach-en-formato06.xlsx", [
        genexcel.fila(plan="PR06", clasificacion="ACH", nombre="Seis Con ACH")])
    c.check("1 fila, Rechazada en validación", len(filas) == 1 and filas[0]["sanic_estado"] == FILA_RECH_VAL, _resumen(filas))
    c.check("el mensaje es exactamente 'El plan PR06 solo admite la clasificación BAC.'",
            _mensaje_de(filas, 1) == "El plan PR06 solo admite la clasificación BAC.", _resumen(filas))
    return c


def r30(ctx):
    c = pc.Caso("R-30", "DN-05, escenario 10: CK en un plan 11 y CK en un plan 06")
    sid, filas = _enviar(ctx, "Prueba R-30", "ZZ-R30-ck-en-formato11-y-06.xlsx", [
        genexcel.fila(plan="PR11", clasificacion="CK", nombre="Once Con CK Diez"),
        genexcel.fila(plan="PR06", clasificacion="CK", nombre="Seis Con CK"),
    ])
    c.check("2 filas, ambas Rechazada en validación",
            len(filas) == 2 and all(f["sanic_estado"] == FILA_RECH_VAL for f in filas), _resumen(filas))
    c.check("fila 1 (PR11): 'El plan PR11 solo admite la clasificación ACH.'",
            _mensaje_de(filas, 1) == "El plan PR11 solo admite la clasificación ACH.", _resumen(filas))
    c.check("fila 2 (PR06): 'El plan PR06 solo admite la clasificación BAC.'",
            _mensaje_de(filas, 2) == "El plan PR06 solo admite la clasificación BAC.", _resumen(filas))
    return c


def r31(ctx):
    c = pc.Caso("R-31", "DN-05: clasificación y banco vacíos se completan y se GUARDAN (06 → BAC/BAC; 10 → banco BAC)")
    sid, filas = _enviar(ctx, "Prueba R-31", "ZZ-R31-clasificacion-y-banco-vacios.xlsx", [
        genexcel.fila(plan="PR06", clasificacion="", banco="", nombre="Seis Vacios"),
        genexcel.fila(plan="PR10", clasificacion="CK", banco="", moneda="USD", nombre="Diez Sin Banco"),
    ])
    c.check("2 filas, ambas Validada",
            len(filas) == 2 and all(f["sanic_estado"] == FILA_VALIDADA for f in filas), _resumen(filas))
    por_n = {f["sanic_numerofila"]: f for f in filas}
    f1, f2 = por_n.get(1, {}), por_n.get(2, {})
    c.check("fila 1 (PR06): sanic_clasificacion guardada = BAC", f1.get("sanic_clasificacion") == CLASIF_BAC,
            str(f1.get("sanic_clasificacion")))
    c.check("fila 1 (PR06): sanic_banco guardado = BAC", f1.get("sanic_banco") == BANCO_BAC, str(f1.get("sanic_banco")))
    c.check("fila 2 (PR10): la clasificación recibida (CK) se respeta, no se pisa",
            f2.get("sanic_clasificacion") == CLASIF_CK, str(f2.get("sanic_clasificacion")))
    c.check("fila 2 (PR10): sanic_banco guardado = BAC", f2.get("sanic_banco") == BANCO_BAC, str(f2.get("sanic_banco")))
    return c


def r32(ctx):
    c = pc.Caso("R-32", "DN-05: Modificación en un plan 11 (GESTION_DEL_FORMATO)")
    sid, filas = _enviar(ctx, "Prueba R-32", "ZZ-R32-modificacion-en-formato11.xlsx", [
        genexcel.fila(gestion="Modificacion", plan="PR11", clasificacion="ACH", referencia="REFMODI0011",
                      cuenta="10203040506070", nombre="")])
    c.check("1 fila, Rechazada en validación", len(filas) == 1 and filas[0]["sanic_estado"] == FILA_RECH_VAL, _resumen(filas))
    c.check("el mensaje es exactamente 'El plan PR11 no admite la gestión Modificacion.' (y nada más: "
            "la obligatoriedad de la Modificación está cumplida)",
            _mensaje_de(filas, 1) == "El plan PR11 no admite la gestión Modificacion.", _resumen(filas))
    return c


def r33(ctx):
    c = pc.Caso("R-33", "DN-05: Exclusión en un plan 11, con referencia y sin banco — la referencia es la RECIBIDA")
    sid, filas = _enviar(ctx, "Prueba R-33", "ZZ-R33-exclusion-formato11.xlsx", [
        genexcel.fila(gestion="Exclusion", plan="PR11", clasificacion="ACH", referencia="REFEXCL0001",
                      cuenta="10203040506070", nombre="", tipoid="", numeroid="", moneda="", banco="")])
    c.check("1 fila, Validada", len(filas) == 1 and filas[0]["sanic_estado"] == FILA_VALIDADA, _resumen(filas))
    if filas:
        c.check("sanic_referencia = REFEXCL0001 (la recibida; en la Exclusión NO se arma con banco + cuenta)",
                filas[0].get("sanic_referencia") == "REFEXCL0001", f"obtenida {filas[0].get('sanic_referencia')!r}")
        c.check("sanic_referenciarecibida = REFEXCL0001", filas[0].get("sanic_referenciarecibida") == "REFEXCL0001",
                f"obtenida {filas[0].get('sanic_referenciarecibida')!r}")
    return c


def r34(ctx):
    c = pc.Caso("R-34", "DN-05: Exclusión sin referencia (la referencia es obligatoria en la Exclusión)")
    sid, filas = _enviar(ctx, "Prueba R-34", "ZZ-R34-exclusion-sin-referencia.xlsx", [
        genexcel.fila(gestion="Exclusion", plan="PR11", clasificacion="ACH", referencia="",
                      cuenta="10203040506070", nombre="", tipoid="", numeroid="", moneda="", banco="")])
    c.check("1 fila, Rechazada en validación", len(filas) == 1 and filas[0]["sanic_estado"] == FILA_RECH_VAL, _resumen(filas))
    c.check("el mensaje es exactamente 'Faltan datos obligatorios: Referencia.'",
            _mensaje_de(filas, 1) == _faltan(ctx.extra["encabezados"], "referencia"), _resumen(filas))
    return c


def r35(ctx):
    c = pc.Caso("R-35", "DN-05, escenario 21: caracteres no permitidos (CARACTERES_PERMITIDOS) — el mensaje NO trae el valor")
    enc = ctx.extra["encabezados"]
    n, ref = enc["nombreBeneficiario"], enc["referencia"]
    sid, filas = _enviar(ctx, "Prueba R-35", "ZZ-R35-caracteres.xlsx", [
        genexcel.fila(plan="PR06", clasificacion="BAC", nombre="Nuñez Prueba"),
        genexcel.fila(plan="PR06", clasificacion="BAC", nombre="Jose Ramírez"),
        genexcel.fila(plan="PR06", clasificacion="BAC", nombre="Nombre Correcto", referencia="REF-0001"),
        genexcel.fila(plan="PR06", clasificacion="BAC", nombre="Peña Dos", referencia="REF-0002"),
        genexcel.fila(plan="PR06", clasificacion="BAC", nombre="Prueba 123"),
    ])
    c.check("5 filas, todas Rechazada en validación",
            len(filas) == 5 and all(f["sanic_estado"] == FILA_RECH_VAL for f in filas), _resumen(filas))
    esperados = {
        1: f"Caracteres no permitidos: {n} (ñ).",
        2: f"Caracteres no permitidos: {n} (í).",
        3: f"Caracteres no permitidos: {ref} (-).",
        4: f"Caracteres no permitidos: {n} (ñ); {ref} (-).",
        5: f"Caracteres no permitidos: {n} (1 2 3).",
    }
    for num, esperado in esperados.items():
        c.check(f"fila {num}: el mensaje es exactamente {esperado!r}", _mensaje_de(filas, num) == esperado, _resumen(filas))
    for num, valor in {1: "Nuñez Prueba", 2: "Ramírez", 3: "REF-0001", 4: "Peña Dos", 5: "Prueba 123"}.items():
        c.check(f"fila {num}: el mensaje NO contiene el valor completo {valor!r} (D-41)",
                valor not in (_mensaje_de(filas, num) or ""), _resumen(filas))
    return c


def r36(ctx):
    c = pc.Caso("R-36", "DN-05, escenario 20: cuenta de 8 dígitos en un plan 06, banco vacío → BAC (9-16) (CUENTA_DEL_BANCO)")
    sid, filas = _enviar(ctx, "Prueba R-36", "ZZ-R36-cuenta-corta-formato06.xlsx", [
        genexcel.fila(plan="PR06", clasificacion="BAC", banco="", cuenta="12345678", nombre="Cuenta Corta Seis")])
    c.check("1 fila, Rechazada en validación", len(filas) == 1 and filas[0]["sanic_estado"] == FILA_RECH_VAL, _resumen(filas))
    msg = _mensaje_de(filas, 1) or ""
    c.check("el mensaje es exactamente 'El largo de la cuenta no corresponde al banco BAC.'",
            msg == "El largo de la cuenta no corresponde al banco BAC.", msg)
    c.check("el mensaje NO trae el largo ni la cuenta (D-41)", "12345678" not in msg and "8" not in msg, msg)
    return c


def r37(ctx):
    c = pc.Caso("R-37", "DN-05, escenario 20: PR11 con LAFISE y 9 dígitos, y con ATLANTIDA y 17 dígitos, son válidas")
    sid, filas = _enviar(ctx, "Prueba R-37", "ZZ-R37-cuenta-por-banco-formato11.xlsx", [
        genexcel.fila(plan="PR11", clasificacion="ACH", banco="LAFISE", cuenta="123456789", nombre="Lafise Nueve"),
        genexcel.fila(plan="PR11", clasificacion="ACH", banco="ATLANTIDA", cuenta="12345678901234567",
                      nombre="Atlantida Diecisiete"),
    ])
    c.check("2 filas, ambas Validada (LAFISE 9-16; ATLANTIDA usa el porDefecto 6-17)",
            len(filas) == 2 and all(f["sanic_estado"] == FILA_VALIDADA for f in filas), _resumen(filas))
    por_n = {f["sanic_numerofila"]: f for f in filas}
    c.check("fila 1: banco guardado = LAFISE", por_n.get(1, {}).get("sanic_banco") == BANCO_LAFISE,
            str(por_n.get(1, {}).get("sanic_banco")))
    c.check("fila 2: banco guardado = ATLANTIDA", por_n.get(2, {}).get("sanic_banco") == BANCO_ATLANTIDA,
            str(por_n.get(2, {}).get("sanic_banco")))
    c.check("fila 1: la referencia mide 20 (006 + cuenta a 17)", len(por_n.get(1, {}).get("sanic_referencia") or "") == 20,
            str(por_n.get(1, {}).get("sanic_referencia")))
    c.check("fila 2: la referencia mide 20 (037 + 17 dígitos)", len(por_n.get(2, {}).get("sanic_referencia") or "") == 20,
            str(por_n.get(2, {}).get("sanic_referencia")))
    return c


def r38(ctx):
    c = pc.Caso("R-38", "DN-05: plan ajeno (PRNA) — las reglas por formato NO hablan (sin fuga del formato del plan)")
    sid, filas = _enviar(ctx, "Prueba R-38", "ZZ-R38-plan-ajeno-sin-fuga.xlsx", [
        # clasificación y banco que el formato del plan no admitiría, gestión Modificación
        genexcel.fila(gestion="Modificacion", plan="PRNA", clasificacion="ACH", banco="LAFISE", referencia="REFAJENO001",
                      nombre="Plan Ajeno Uno"),
        # cuenta de 8 dígitos (con el banco efectivo BAC no pasaría)
        genexcel.fila(plan="PRNA", clasificacion="BAC", banco="", cuenta="12345678", nombre="Plan Ajeno Dos"),
    ])
    c.check("2 filas, ambas Sin autorización",
            len(filas) == 2 and all(f["sanic_estado"] == FILA_SIN_AUT for f in filas), _resumen(filas))
    for num in (1, 2):
        msg = _mensaje_de(filas, num) or ""
        c.check(f"fila {num}: el mensaje nombra el plan PRNA", "PRNA" in msg, msg)
        for frag in ("solo admite", "no admite", "no corresponde al banco"):
            c.check(f"fila {num}: el mensaje NO contiene {frag!r} (ninguna regla por formato habló)", frag not in msg, msg)
    return c


# ===========================================================================
# DN-06 (aprobador, 2026-11-24): canal y pre-registro del plan
# ===========================================================================
PLANES = "sanic_mppp_tbl_plans"
CANAL_BANCA, CANAL_CONECTIVIDAD = 159460001, 159460002


class _PlanCambiado:
    """Cambia columnas de un plan del fixture y las DEVUELVE a lo que tenían al salir, pase lo que pase."""

    def __init__(self, dv, codigo, **cambios):
        self.dv, self.codigo, self.cambios = dv, codigo, cambios

    def __enter__(self):
        cols = ",".join(self.cambios)
        est, r, _ = self.dv.call("GET", f"{PLANES}?$select={cols}&$filter=sanic_codigo eq '{self.codigo}'")
        if est != 200 or len(r.get("value", [])) != 1:
            raise RuntimeError(f"no encuentro el plan {self.codigo}: HTTP {est} {r}")
        fila = r["value"][0]
        self.id = fila["sanic_mppp_tbl_planid"]
        self.antes = {k: fila.get(k) for k in self.cambios}
        est, r, _ = self.dv.call("PATCH", f"{PLANES}({self.id})", self.cambios)
        if est not in (200, 204):
            raise RuntimeError(f"no pude cambiar el plan {self.codigo}: HTTP {est} {r}")
        return self

    def __exit__(self, *_):
        self.dv.call("PATCH", f"{PLANES}({self.id})", self.antes)
        return False


def r39(ctx):
    c = pc.Caso("R-39", "DN-06: plan con Requiere pre-registro = No — todas sus filas se rechazan con 'Este plan no requiere pre-registro.'")
    with _PlanCambiado(ctx.dv, "PR11", sanic_requierepreregistro=False):
        sid, filas = _enviar(ctx, "Prueba R-39", "ZZ-R39-plan-sin-preregistro.xlsx", [
            genexcel.fila(plan="PR11", clasificacion="ACH", banco="LAFISE", cuenta="123456789", nombre="Sin Preregistro Uno"),
            # además le falta el nombre: OBLIGATORIEDAD no tiene que hablar
            genexcel.fila(plan="PR11", clasificacion="ACH", banco="LAFISE", cuenta="123456789", nombre=""),
        ])
    c.check("2 filas, ambas Rechazada en validación (no Sin autorización)",
            len(filas) == 2 and all(f["sanic_estado"] == FILA_RECH_VAL for f in filas), _resumen(filas))
    for num in (1, 2):
        c.check(f"fila {num}: mensaje exacto 'Este plan no requiere pre-registro.'",
                _mensaje_de(filas, num) == "Este plan no requiere pre-registro.", _mensaje_de(filas, num))
    return c


def r40(ctx):
    c = pc.Caso("R-40", "DN-06: plan de canal Conectividad — usa plantilla.obligatoriedad.conectividad y lo registra en la versión")
    with _PlanCambiado(ctx.dv, "PR06", sanic_canal=CANAL_CONECTIVIDAD):
        sid, filas = _enviar(ctx, "Prueba R-40", "ZZ-R40-plan-conectividad.xlsx", [
            genexcel.fila(gestion="Inclusion", plan="PR06", clasificacion="", nombre="Conectividad Seis Prueba",
                          tipoid="", moneda="", banco=""),
        ])
    solicitud = sim.leer_solicitud(ctx.dv, sid)
    version = solicitud.get("sanic_versionparametros") or ""
    c.check("la fila se valida (la v1 de Conectividad es copia de la v3)",
            len(filas) == 1 and filas[0]["sanic_estado"] == FILA_VALIDADA, _resumen(filas))
    c.check("sanic_versionparametros termina en ';obligatoriedadconectividad=N'",
            re.search(r";obligatoriedadconectividad=\d+$", version) is not None, version)
    return c


def r41(ctx):
    c = pc.Caso("R-41", "DN-06: plan ajeno sin pre-registro — solo se dice que no está autorizado (D-41)")
    with _PlanCambiado(ctx.dv, "PRNA", sanic_requierepreregistro=False):
        sid, filas = _enviar(ctx, "Prueba R-41", "ZZ-R41-plan-ajeno-sin-preregistro.xlsx", [
            genexcel.fila(plan="PRNA", clasificacion="ACH", banco="LAFISE", cuenta="123456789", nombre="Ajeno Sin Preregistro"),
        ])
    msg = _mensaje_de(filas, 1) or ""
    c.check("1 fila, Sin autorización", len(filas) == 1 and filas[0]["sanic_estado"] == FILA_SIN_AUT, _resumen(filas))
    c.check("el mensaje NO habla del pre-registro", "pre-registro" not in msg, msg)
    return c


# ===========================================================================
# DN-07 (aprobador, 2026-11-24): el número de identificación según su tipo. Cédulas SINTÉTICAS.
# ===========================================================================
def r42(ctx):
    c = pc.Caso("R-42", "DN-07: IDENTIFICACION_DEL_TIPO — cédula, RUC y pasaporte, en Inclusión y en Modificación")
    sid, filas = _enviar(ctx, "Prueba R-42", "ZZ-R42-identificacion-por-tipo.xlsx", [
        # 1: la cédula por defecto del arnés (letra correcta)
        genexcel.fila(plan="PR11", nombre="Cedula Buena", cuenta="10000000000001"),
        # 2: la misma con la letra que no corresponde
        genexcel.fila(plan="PR11", nombre="Cedula Letra Mal", cuenta="10000000000002", numeroid="0011805900012X"),
        # 3: 32/13: fecha imposible (la letra sí corresponde)
        genexcel.fila(plan="PR11", nombre="Cedula Fecha Mal", cuenta="10000000000003", numeroid="0013213800000M"),
        # 4: RUC de persona jurídica
        genexcel.fila(plan="PR11", nombre="Ruc Juridico", cuenta="10000000000004", tipoid="RUC", numeroid="J0310000001234"),
        # 5: pasaporte de 5 caracteres
        genexcel.fila(plan="PR11", nombre="Pasaporte Corto", cuenta="10000000000005", tipoid="PAS", numeroid="AB123"),
        # 6: Modificación (identificación opcional) con una cédula mal escrita: si viene, se valida
        genexcel.fila(gestion="Modificacion", plan="PR06", clasificacion="", nombre="", tipoid="CNA", numeroid="0011805900012X",
                      referencia="REFMODI0042", cuenta="10203040506070", moneda="", banco=""),
        # 7: la letra en minúscula vale
        genexcel.fila(plan="PR11", nombre="Cedula Minuscula", cuenta="10000000000007", numeroid="0011805900012n"),
    ])
    por_n = {f["sanic_numerofila"]: f for f in filas}
    esperado = {
        1: (FILA_VALIDADA, None),
        2: (FILA_RECH_VAL, "El número de identificación no es una cédula válida: la letra final no corresponde al número."),
        3: (FILA_RECH_VAL, "El número de identificación no es una cédula válida: la fecha de nacimiento no existe."),
        4: (FILA_VALIDADA, None),
        5: (FILA_RECH_VAL, "El número de identificación no es un pasaporte válido: debe tener entre 6 y 9 letras o números."),
        6: (FILA_RECH_VAL, "El número de identificación no es una cédula válida: la letra final no corresponde al número."),
        7: (FILA_VALIDADA, None),
    }
    c.check("7 filas leídas", len(filas) == 7, _resumen(filas))
    for n, (estado, mensaje) in esperado.items():
        f = por_n.get(n, {})
        if mensaje is None:
            c.check(f"fila {n} Validada", f.get("sanic_estado") == estado, _resumen(filas))
        else:
            c.check(f"fila {n} rechazada con {mensaje!r}",
                    f.get("sanic_estado") == estado and f.get("sanic_mensaje") == mensaje, _resumen(filas))
    for n in (2, 3, 6):
        c.check(f"fila {n}: el mensaje no repite el número", "0011805900012" not in (por_n.get(n, {}).get("sanic_mensaje") or "")
                and "0013213800000" not in (por_n.get(n, {}).get("sanic_mensaje") or ""), _resumen(filas))
    return c


CASOS = [r01, r02, r03, r04, r05, r06, r07, r08, r09, r10, r11, r12, r13, r14, r15, r16, r17, r18,
         r19, r20, r21, r22, r23, r24, r25, r26, r27, r28,
         r29, r30, r31, r32, r33, r34, r35, r36, r37, r38,
         r39, r40, r41, r42]
