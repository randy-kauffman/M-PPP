#!/usr/bin/env python3
"""Set E — Exportación a Excel, DN-02 (`pruebas/11-exportacion-excel.md`).

La Custom API `sanic_mppp_capi_exportarparadigitar` (Action, POST, cuerpo —
NUNCA Function: fue Function al principio y dio HTTP 414 con ~700
identificadores en la URL; se recreó como Action el 2026-09-26, ver el
playbook `playbooks/custom-api/sanic_mppp_capi_exportarparadigitar.md`), el
orden de columnas por el parámetro GLOBAL `exportacion.columnas`
(CONVENCIONES §8.2), `filaids` vacío o inexistente, el corte por presupuesto
de tiempo, el riesgo conocido de memoria a volumen extremo, y el privilegio
(solo Ejecutivo/Supervisor). El contrato completo de un módulo de set está en
`sets/__init__.py`; la lógica de preparación compartida, en
`preparacion_comun.py`.

**E-01 no corre acá.** El botón "Descargar para digitar" se dibuja o no en el
DOM real — eso ya lo cubre `pruebas/gui/casos/dn-02-descargar-para-digitar.spec.mjs`
(y `ribbon-fila.spec.mjs`). Este set NUNCA duplica esa cobertura (CONVENCIONES
§9): `e01()` existe únicamente para que el caso quede NOMBRADO en el resumen
de la corrida y no desaparezca en silencio, y devuelve N-A con el comando
exacto del arnés.

**E-08 (riesgo de memoria a ~300k) NO corre de rutina.** Es un riesgo YA
MEDIDO y ACEPTADO (no un defecto), pero la llamada real tarda ~150-170s y
puede degradar el sandbox de Dataverse Dev para cualquier otro usuario
mientras corre. Por default `e08()` da N-A explicando cómo correrlo a
propósito. Solo se ejecuta de verdad con la variable de entorno
`MPPP_E08_ALTO_RIESGO=si` puesta ANTES de invocar a `correr.py` — nunca por
defecto, y nunca reintentada sola si algo la bloquea.

**E-07 (corte por presupuesto) queda SIEMPRE N-A acá**, a propósito: la
medición real (`diseno/DIRECTRICES-NUEVAS.md`, 2026-09-26) muestra que ni
250.000 posiciones de `filaids` se acercaron al umbral de 100s (tardó 40,6s),
y que arriba de eso el riesgo es el MISMO de E-08 (el sandbox muere por
memoria antes de que el plugin tenga otra vuelta de reloj para cortar con su
propio mensaje). No existe hoy un volumen conocido que dispare
`PresupuestoDeExportacionAgotandoseException` de forma confiable sin correr
el riesgo de E-08: forzarlo por API no agrega cobertura real, solo riesgo. La
cobertura confiable de ESE mensaje puntual es la prueba unitaria con el
reloj inyectado (`tests/Sanic.Mppp.Plugins.Tests/Aceptacion/ExportarParaDigitarApiAceptacion.cs`,
`RelojParaPruebas`), que no vive en este set (es C#, no Python/API).
"""
import base64
import io
import json
import os
import sys
import time

_AQUI = os.path.dirname(os.path.abspath(__file__))
_HERRAMIENTAS_PRUEBAS = os.path.dirname(_AQUI)
sys.path.insert(0, _HERRAMIENTAS_PRUEBAS)

import preparacion_comun as pc  # noqa: E402
import simulador as sim  # noqa: E402
from dataverse_api import Dataverse  # noqa: E402

LETRA = "E"
NOMBRE = "Exportación a Excel (DN-02)"

# Remitente propio de este set. Nunca el de una persona real (CONVENCIONES §5).
REMITENTE = "zz-corredor-fase1@example.com"

CUSTOM_API = "sanic_mppp_capi_exportarparadigitar"
PARAMETROS = "sanic_mppp_tbl_parametros"

# Bandera explícita para E-08 (ver el docstring del módulo). A propósito NO
# hay forma de "recordarla": quien la necesita la escribe cada vez.
BANDERA_E08 = "MPPP_E08_ALTO_RIESGO"


# ===========================================================================
# preparar / cerrar
# ===========================================================================
def preparar(ctx):
    """Deja listo TODO lo que el set E necesita, y nada más (CONVENCIONES §8):

    - **Datos de negocio** (fixture compartido `PR*`, create-if-missing, y el
      `REMITENTE` de este set autorizado sobre PR11).
    - **Los 15 Excel + el adjunto que no es Excel** en `pruebas/adjuntos/`.
    - **`exportacion.columnas`**, al menos una versión activa (si no existe
      ninguna, `preparar()` revienta y el set no corre sus casos: CONVENCIONES
      §8.2 dice que este parámetro llega al canon "el sembrado por
      parametros.md", pero sembrarlo desde cero es una decisión de negocio del
      CONJUNTO de columnas — no algo que este set deba inventar).
    - **El fixture propio de Filas reales**: una Solicitud simulada con
      `E01-feliz-formato11.xlsx` (tres filas válidas: Inclusión, Exclusión,
      Modificación — pasan TODAS las reglas, quedan en Validada), creada acá
      mismo con `ctx.procesar()`. TODOS los casos de este set que necesitan
      `filaid`s reales usan ESTE fixture (`ctx.extra["filas"]`) — ninguno
      supone una Fila que dejó otro set (CONVENCIONES §8: "un set no depende
      de otro set").

    **Lo que NO prepara, y por qué:**

    - **Los roles de admin_planes/admin_tecnico** (solo E-09 los necesita):
      se resuelven y verifican DENTRO de E-09, igual que I-13 en `set_i.py`.
    - **El canon de configuración global** (§8.2). Lo aplica `correr.py` antes
      y después de CADA set, no cada set por separado. `exportacion.columnas`
      es uno de los DOS parámetros no-simples que ese canon automático NO
      toca (`preparacion_comun.PARAMETROS_NO_SIMPLES`): si E-03 siembra una
      versión de prueba, es E-03 quien la desactiva al cerrar (try/finally),
      nunca el canon genérico.
    """
    pc.verificar_datos_negocio(ctx.dv, REMITENTE)
    pc.verificar_excels()
    pc.verificar_parametro_activo(ctx.dv, "exportacion.columnas")

    sid, mid, rc, rv = ctx.procesar(
        remitente=REMITENTE, asunto="Prueba set E — fixture de exportación",
        cantidad_adjuntos=1, cantidad_excel=1, excel_a_subir=pc.excel("E01-feliz-formato11.xlsx"))
    filas = _filas_de_solicitud(ctx, sid)
    if len(filas) < 2:
        raise RuntimeError(
            f"el fixture de exportación (E01-feliz-formato11.xlsx) no generó al menos 2 Filas: {filas}")
    ctx.extra["filas"] = filas
    ctx.extra["numero_solicitud"] = sim.leer_solicitud(ctx.dv, sid).get("sanic_nombre")


def cerrar(ctx):
    """Borra, por GUID, la Solicitud simulada que este set creó en `preparar()`
    (y cualquier otra que un caso hubiera creado). No toca el fixture
    compartido `PR*`: eso no se borra nunca desde un set."""
    return ctx.limpiar_solicitudes()


# ===========================================================================
# Helpers locales (no viven en preparacion_comun.py porque solo los necesita
# este set; se reportan acá, como pide la consigna)
# ===========================================================================
def _filas_de_solicitud(ctx, sid):
    """Los `filaid` reales de una Solicitud, con los campos que E-06 necesita
    para comparar. `sim.leer_filas()` no trae el GUID (ningún set migrado lo
    necesitaba hasta ahora); acá SÍ hace falta para llamar a la Custom API."""
    est, r, _ = ctx.dv.call(
        "GET",
        f"{sim.FILAS}?$select=sanic_mppp_tbl_filaid,sanic_numerofila,sanic_numeroidentificacion,"
        f"sanic_numerocuenta&$filter=_sanic_solicitudid_value eq {sid}&$orderby=sanic_numerofila")
    if est != 200:
        raise RuntimeError(f"no se pudieron leer las Filas de la Solicitud {sid} (HTTP {est}): {r}")
    return r.get("value", [])


def _decodificar_xlsx(archivo_base64):
    """Decodifica el campo `archivo` (base64) de la respuesta de la Custom API
    y devuelve `(encabezados, filas_de_datos, nombre_de_hoja)`. Levanta
    `ValueError` con el motivo si no es un `.xlsx` válido — nunca revienta con
    una excepción críptica de openpyxl/base64 sin contexto."""
    if not archivo_base64:
        raise ValueError("el campo 'archivo' de la respuesta vino vacío")
    try:
        contenido = base64.b64decode(archivo_base64, validate=True)
    except Exception as e:
        raise ValueError(f"'archivo' no decodifica como base64 válido: {type(e).__name__}: {e}")
    try:
        import openpyxl
        libro = openpyxl.load_workbook(io.BytesIO(contenido), read_only=True, data_only=True)
    except Exception as e:
        raise ValueError(f"el contenido decodificado no es un .xlsx válido: {type(e).__name__}: {e}")
    nombre_hoja = libro.sheetnames[0]
    filas = [list(f) for f in libro[nombre_hoja].iter_rows(values_only=True)]
    libro.close()
    encabezados = filas[0] if filas else []
    datos = filas[1:]
    return encabezados, datos, nombre_hoja


def _exportar(ctx, filaids, cabeceras=None, timeout=180):
    return ctx.dv.call("POST", CUSTOM_API, {"filaids": filaids}, cabeceras=cabeceras, timeout=timeout)


def _usuario_por_alias(ctx, alias):
    """El `systemuserid` de un usuario de prueba por su alias de dominio,
    igual que `comportamiento_roles.py` y `set_i.py._usuario_por_alias`
    (CONVENCIONES §6). Local a este módulo por el mismo motivo que en `set_i.py`."""
    est, r, _ = ctx.dv.call(
        "GET",
        "systemusers?$select=systemuserid,fullname"
        f"&$filter=startswith(domainname,'{alias}@') and applicationid eq null")
    filas = r.get("value", []) if est == 200 else []
    if len(filas) != 1:
        raise RuntimeError(f"{alias!r} no identifica un usuario único en el entorno: {filas}")
    return filas[0]["systemuserid"]


# El alias de DOMINIO (User3/User4) de cada papel de negocio (CONVENCIONES §6,
# mismo mapeo que `comportamiento_roles.ALIAS`). `_usuario_por_alias` filtra
# por dominio, no por el nombre del papel: pasarle "admin_planes" directo
# buscaría un domainname que no existe y el caso daría FALLA por un motivo
# que no es el que dice probar.
_ALIAS_DOMINIO = {"admin_planes": "User3", "admin_tecnico": "User4"}


# ===========================================================================
# E-01 — el botón se dibuja en el ribbon. NO corre acá (GUI): ver el
# docstring del módulo y `pruebas/gui/casos/dn-02-descargar-para-digitar.spec.mjs`.
# ===========================================================================
def e01(ctx):
    c = pc.Caso("E-01", "El comando 'Descargar para digitar' se dibuja en el ribbon")
    c.na(
        "Requiere navegador real con una sesión de Ejecutivo o Supervisor (CONVENCIONES §6); ya está "
        "automatizado en pruebas/gui/ y este set NUNCA lo duplica (CONVENCIONES §9). Guarda previa "
        "obligatoria: 'python3 herramientas/pruebas/estado_sesiones.py'. Si la sesión vive y el rol "
        "coincide, correr desde pruebas/gui/: 'MPPP_USUARIO=ejecutivo npx playwright test dn-02' (o "
        "'ribbon-fila' para los 7 botones del ribbon de Fila). LECCIÓN cara (H-06, 2026-09-27): este "
        "comando estuvo definido y PUBLICADO en el servidor y NO se dibujaba en el cliente — la causa "
        "era la metadata compilada del ribbon. Tres caminos de API distintos lo confirmaron 'publicado' "
        "y NINGUNO detectó que no se veía; costó dos días de diagnóstico. Por eso este caso, aunque acá "
        "sea N-A, es el único que mira el DOM real en vez de confiar en que 'publicado' == 'se dibuja'.")
    return c


# ===========================================================================
# E-02 — contrato mínimo: archivo + exportadas.
# ===========================================================================
def e02(ctx):
    c = pc.Caso("E-02", "Exportación básica: 'archivo' y 'exportadas'")
    filas = ctx.extra["filas"]
    filaids = [f["sanic_mppp_tbl_filaid"] for f in filas[:2]]

    est, r, _ = _exportar(ctx, filaids)
    c.check("HTTP 200", est == 200, f"HTTP {est}: {r}")
    if est != 200:
        return c
    c.check("exportadas = 2", r.get("exportadas") == 2, f"exportadas={r.get('exportadas')!r}")

    try:
        encabezados, datos, hoja = _decodificar_xlsx(r.get("archivo"))
    except ValueError as e:
        c.check("'archivo' decodifica a un .xlsx válido", False, str(e))
        return c
    c.check("'archivo' es un .xlsx válido, hoja 'Filas' (ArmadorExportacionOpenXml.NombreDeHoja)",
            hoja == "Filas", f"hoja={hoja!r}")
    c.check("trae al menos un encabezado", len(encabezados) > 0)
    c.check("trae 2 filas de datos (una por filaid pedido)", len(datos) == 2, f"{len(datos)} filas: {datos}")
    return c


# ===========================================================================
# E-03 — el orden de columnas sigue `exportacion.columnas`, SIN tocar código.
# Versiona (agrega una fila nueva, CONVENCIONES §8.2) y la desactiva al
# cerrar el caso (try/finally): nunca edita ni borra la versión vigente.
# ===========================================================================
def e03(ctx):
    c = pc.Caso("E-03", "El orden de columnas sigue exportacion.columnas, sin tocar código")
    filaid = ctx.extra["filas"][0]["sanic_mppp_tbl_filaid"]

    est, r, _ = ctx.dv.call(
        "GET",
        f"{PARAMETROS}?$select=sanic_mppp_tbl_parametroid,sanic_version,sanic_valor,sanic_tipo"
        "&$filter=sanic_nombre eq 'exportacion.columnas' and statecode eq 0"
        "&$orderby=sanic_version desc&$top=1")
    if est != 200 or not r.get("value"):
        c.check("hay una versión activa de exportacion.columnas (precondición)", False, f"HTTP {est}: {r}")
        return c
    activo = r["value"][0]
    tipo = activo["sanic_tipo"]

    # La versión NUEVA tiene que ser mayor que CUALQUIER versión ya sembrada
    # (activa o no): si una corrida anterior de este mismo caso dejó una
    # versión de prueba desactivada (CONVENCIONES §8.2: se desactiva, nunca se
    # borra), "activa + 1" puede colisionar con esa fila y la clave alterna
    # `KEY - MPPP - Parámetro - Nombre y versión` rechaza el POST con un 412.
    est_max, r_max, _ = ctx.dv.call(
        "GET",
        f"{PARAMETROS}?$select=sanic_version&$filter=sanic_nombre eq 'exportacion.columnas'"
        "&$orderby=sanic_version desc&$top=1")
    if est_max != 200 or not r_max.get("value"):
        c.check("se pudo leer la versión máxima ya sembrada de exportacion.columnas (precondición)",
                False, f"HTTP {est_max}: {r_max}")
        return c
    version_maxima_sembrada = r_max["value"][0]["sanic_version"]

    est1, r1, _ = _exportar(ctx, [filaid])
    if est1 != 200:
        c.check("exportación con el orden ACTUAL", False, f"HTTP {est1}: {r1}")
        return c
    try:
        encabezados_1, _, _ = _decodificar_xlsx(r1.get("archivo"))
    except ValueError as e:
        c.check("'archivo' (orden ACTUAL) decodifica a un .xlsx válido", False, str(e))
        return c

    try:
        config = json.loads(activo["sanic_valor"])
        columnas = config["columnas"]
    except Exception as e:
        c.check("exportacion.columnas activo es el JSON esperado ({'columnas': [...]})", False,
                f"{type(e).__name__}: {e} — valor: {activo['sanic_valor']!r}")
        return c
    if len(columnas) < 2:
        c.check("exportacion.columnas trae al menos 2 columnas para poder intercambiarlas", False, str(columnas))
        return c
    columnas_nuevas = list(columnas)
    columnas_nuevas[0], columnas_nuevas[1] = columnas_nuevas[1], columnas_nuevas[0]
    valor_nuevo = json.dumps({"columnas": columnas_nuevas}, ensure_ascii=False)
    version_nueva = version_maxima_sembrada + 1

    idp_nuevo = None
    try:
        cuerpo = {
            "sanic_nombre": "exportacion.columnas", "sanic_version": version_nueva,
            "sanic_valor": valor_nuevo, "sanic_tipo": tipo,
            "sanic_descripcion": "ZZP - version de PRUEBA de set E (E-03): dos columnas intercambiadas. "
                                  "Se desactiva al cerrar el caso, nunca se edita ni se borra.",
        }
        est2, r2, cab2 = ctx.dv.call("POST", PARAMETROS, cuerpo)
        c.check("se pudo sembrar una versión NUEVA de exportacion.columnas (versionar, nunca editar)",
                est2 in (200, 201, 204), f"HTTP {est2}: {r2}")
        if est2 not in (200, 201, 204):
            return c
        idp_nuevo = Dataverse.id_creado(cab2)

        est3, r3, _ = _exportar(ctx, [filaid])
        c.check("exportación con el orden NUEVO", est3 == 200, f"HTTP {est3}: {r3}")
        if est3 != 200:
            return c
        try:
            encabezados_2, _, _ = _decodificar_xlsx(r3.get("archivo"))
        except ValueError as e:
            c.check("'archivo' (orden NUEVO) decodifica a un .xlsx válido", False, str(e))
            return c

        c.check("el orden de encabezados CAMBIÓ al cambiar el parámetro, sin tocar el ensamblado",
                encabezados_1 != encabezados_2, f"antes={encabezados_1} · después={encabezados_2}")
        c.check("el nuevo orden refleja EXACTAMENTE el intercambio sembrado (columnas 1 y 2)",
                len(encabezados_2) >= 2
                and encabezados_2[0] == columnas_nuevas[0]["encabezado"]
                and encabezados_2[1] == columnas_nuevas[1]["encabezado"],
                f"encabezados={encabezados_2!r}")
    finally:
        # Canon §8.2: nunca se edita ni se borra una versión ya sembrada — se
        # DESACTIVA la de prueba, la anterior queda intacta y vuelve a ser la
        # activa más alta (statecode=0), que es el canon real.
        if idp_nuevo:
            ctx.dv.call("PATCH", f"{PARAMETROS}({idp_nuevo})", {"statecode": 1, "statuscode": 2})
            _, ract, _ = ctx.dv.call("GET", f"{PARAMETROS}({idp_nuevo})?$select=statecode")
            c.check("la versión de PRUEBA quedó DESACTIVADA al cerrar (nunca editada ni borrada)",
                    ract.get("statecode") == 1, f"statecode={ract.get('statecode')!r}")
    return c


# ===========================================================================
# E-04 — `filaids` vacío: archivo válido con cero filas de datos.
# ===========================================================================
def e04(ctx):
    c = pc.Caso("E-04", "'filaids' vacío: archivo válido con cero filas de datos")
    est, r, _ = _exportar(ctx, [])
    c.check("HTTP 200 (un arreglo vacío no es un error)", est == 200, f"HTTP {est}: {r}")
    if est != 200:
        return c
    c.check("exportadas = 0", r.get("exportadas") == 0, f"exportadas={r.get('exportadas')!r}")
    try:
        encabezados, datos, _ = _decodificar_xlsx(r.get("archivo"))
    except ValueError as e:
        c.check("'archivo' decodifica a un .xlsx válido", False, str(e))
        return c
    c.check("'archivo' es un .xlsx válido, con encabezado y CERO filas de datos",
            len(encabezados) > 0 and len(datos) == 0, f"encabezados={encabezados} · {len(datos)} filas de datos")
    return c


# ===========================================================================
# E-05 — un `filaid` inexistente revienta LA LLAMADA ENTERA, nombrando cuál.
# ===========================================================================
def e05(ctx):
    c = pc.Caso("E-05", "Un 'filaid' inexistente revienta LA LLAMADA ENTERA, nombrando cuál")
    filaid_real = ctx.extra["filas"][0]["sanic_mppp_tbl_filaid"]
    inventado = "00000000-0000-0000-0000-000000000000"

    est, r, _ = _exportar(ctx, [filaid_real, inventado])
    c.check("la llamada se rechaza (no HTTP 200): NUNCA se omite en silencio una fila que no existe",
            est != 200, f"HTTP {est}: {r}")
    texto = str(r.get("error") or "")
    c.check(f"el mensaje cita el GUID inventado ({inventado})", inventado in texto, texto)
    c.check("el mensaje dice qué hacer (volver a seleccionar y descargar de nuevo)",
            "volvé a seleccionar" in texto.lower() or "volver a seleccionar" in texto.lower(), texto)
    return c


# ===========================================================================
# E-06 — columnas protegidas (identificación, cuenta) salen en claro.
# ===========================================================================
def e06(ctx):
    c = pc.Caso("E-06", "Columnas protegidas (identificación, cuenta) salen en claro, sin protección")
    fila = ctx.extra["filas"][0]
    numeroid = fila.get("sanic_numeroidentificacion")
    numerocuenta = fila.get("sanic_numerocuenta")
    if not (numeroid and numerocuenta):
        c.na(f"la Fila del fixture de este set no trae numeroidentificacion/numerocuenta para comparar: {fila}")
        return c

    est, r, _ = _exportar(ctx, [fila["sanic_mppp_tbl_filaid"]])
    c.check("HTTP 200", est == 200, f"HTTP {est}: {r}")
    if est != 200:
        return c
    try:
        _, datos, _ = _decodificar_xlsx(r.get("archivo"))
    except ValueError as e:
        c.check("'archivo' decodifica a un .xlsx válido", False, str(e))
        return c
    texto_filas = " | ".join(str(v) for fila_datos in datos for v in fila_datos)
    c.check(f"el número de identificación ({numeroid!r}) sale EN CLARO, sin enmascarar", numeroid in texto_filas)
    c.check(f"el número de cuenta ({numerocuenta!r}) sale EN CLARO, sin enmascarar", numerocuenta in texto_filas)
    return c


# ===========================================================================
# E-07 — corte por presupuesto de tiempo. SIEMPRE N-A acá (ver docstring del
# módulo): no hay volumen conocido que dispare el mensaje elegante sin correr
# el mismo riesgo que E-08.
# ===========================================================================
def e07(ctx):
    c = pc.Caso("E-07", "Corte por presupuesto de tiempo (antes de que la plataforma reviente)")
    c.na(
        "No se repite por API a propósito. Medición real (DIRECTRICES-NUEVAS.md, 2026-09-26): ni "
        "250.000 posiciones de filaids se acercaron al umbral de 100s (tardó 40,6s); por encima de eso "
        "el riesgo es el MISMO de E-08 (el sandbox de Dev muere por memoria ANTES de que el plugin "
        "tenga otra vuelta de reloj para cortar con su propio mensaje) — hoy no hay un volumen conocido "
        "que dispare PresupuestoDeExportacionAgotandoseException de forma confiable sin arriesgar "
        "degradar el entorno. La cobertura confiable de ESTE mensaje puntual es la prueba unitaria con "
        "el reloj inyectado: tests/Sanic.Mppp.Plugins.Tests/Aceptacion/ExportarParaDigitarApiAceptacion.cs "
        "(seam RelojParaPruebas) — ya existe, ya está en verde, y no vive en este set de Python/API.")
    return c


# ===========================================================================
# E-08 — riesgo conocido de memoria a ~300k. Detrás de bandera explícita
# (ver docstring del módulo): NO corre de rutina.
# ===========================================================================
def e08(ctx):
    c = pc.Caso("E-08", "Riesgo conocido: ~300.000 filas mueren por memoria/sandbox ANTES del mensaje elegante")
    if os.environ.get(BANDERA_E08) != "si":
        c.na(
            f"NO se corre de rutina: riesgo YA MEDIDO y ACEPTADO (11-exportacion-excel.md E-08), no un "
            f"defecto. La llamada tarda ~150-170s con ~300.000 posiciones de filaids repetidas y puede "
            f"degradar el sandbox de Dataverse Dev para cualquier otro usuario mientras corre. Se corre "
            f"a propósito solo si `ExportarParaDigitar.cs` cambió de forma que pudiera afectar este "
            f"límite, avisando al equipo y fuera de horario de otras pruebas, con: "
            f"{BANDERA_E08}=si python3 herramientas/pruebas/correr.py E")
        return c

    filaid_real = ctx.extra["filas"][0]["sanic_mppp_tbl_filaid"]
    filaids = [filaid_real] * 300_000  # el costo se paga por POSICIÓN, no por fila distinta (dedup interno)

    t0 = time.monotonic()
    est, r, _ = _exportar(ctx, filaids, timeout=300)
    segundos = time.monotonic() - t0

    c.check("la llamada NO devolvió HTTP 200 (a este volumen, la medición de referencia dice que el "
            "sandbox revienta ANTES del mensaje elegante — un HTTP 200 acá sería una mejora real que "
            "hay que investigar y documentar, no algo esperado hoy)",
            est != 200, f"HTTP {est} en {segundos:.1f}s: {str(r)[:300]}")
    texto = str(r.get("error") or "")
    c.check("el error NO es el mensaje elegante de PresupuestoDeExportacionAgotandoseException (si lo "
            "fuera, el riesgo documentado ya no aplicaría a este volumen: hay que actualizar el MD, no "
            "es un defecto de este caso)",
            "volvé a seleccionar con menos filas" not in texto.lower(), f"{segundos:.1f}s: {texto[:300]}")
    return c


# ===========================================================================
# E-09 — privilegio de la Custom API: solo Ejecutivo y Supervisor.
# Impersonación por MSCRMCallerID (CONVENCIONES §6, patrón de
# comportamiento_roles.py / set_i.py), SOLO sobre una Fila que este mismo set
# creó (`ctx.extra["filas"]`) — nunca sobre una fila cualquiera del entorno.
# ===========================================================================
def e09(ctx):
    c = pc.Caso("E-09", "Privilegio de la Custom API: solo Ejecutivo y Supervisor pueden exportar")
    filaid_propio = ctx.extra["filas"][0]["sanic_mppp_tbl_filaid"]

    for papel, rol in (("admin_planes", "SR - MPPP - Administrador de planes"),
                       ("admin_tecnico", "SR - MPPP - Administrador tecnico")):
        try:
            uid = _usuario_por_alias(ctx, _ALIAS_DOMINIO[papel])
        except RuntimeError as e:
            c.check(f"se pudo resolver {papel!r} en el entorno (precondición)", False, str(e))
            continue
        ok_rol, roles = pc.verificar_roles(ctx.dv, uid, [rol])
        if not ok_rol:
            c.check(f"{papel!r} tiene el rol {rol!r} que este caso asume (precondición)", False,
                    f"tiene: {roles}")
            continue
        est, r, _ = _exportar(ctx, [filaid_propio], cabeceras={"MSCRMCallerID": uid})
        c.check(f"{papel!r} ({rol}) NO puede exportar (falta prvReadsanic_mppp_tbl_fila)",
                est in (401, 403), f"HTTP {est}: {r}")
    return c


# ===========================================================================
# E-10 — `filaids` es un StringArray, no un JSON serializado.
# ===========================================================================
def e10(ctx):
    c = pc.Caso("E-10", "'filaids' es un StringArray, no un JSON serializado")
    filaid_real = ctx.extra["filas"][0]["sanic_mppp_tbl_filaid"]

    est1, r1, _ = ctx.dv.call("POST", CUSTOM_API, {"filaids": "no-es-un-arreglo"})
    c.check("un 'filaids' que NO es un arreglo se rechaza", est1 != 200, f"HTTP {est1}: {r1}")
    texto1 = str(r1.get("error") or "").lower()
    c.check("el mensaje dice que tiene que ser un arreglo de texto (StringArray)",
            "arreglo" in texto1 or "stringarray" in texto1 or "array" in texto1, texto1)

    est2, r2, _ = _exportar(ctx, [filaid_real, "no-es-un-guid"])
    c.check("un elemento que no es un Guid válido se rechaza", est2 != 200, f"HTTP {est2}: {r2}")
    texto2 = str(r2.get("error") or "").lower()
    c.check("el mensaje nombra la POSICIÓN 2, no un mensaje genérico",
            "posición 2" in texto2 or "posicion 2" in texto2, texto2)
    return c


CASOS = [e01, e02, e03, e04, e05, e06, e07, e08, e09, e10]
