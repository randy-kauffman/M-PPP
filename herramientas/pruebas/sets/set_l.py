#!/usr/bin/env python3
"""Set L — Carga masiva y activación masiva de catálogos por Excel, DN-04 (`pruebas/12-catalogos-masivos.md`).

Las dos Custom API (`sanic_mppp_capi_cargarcatalogos` y `sanic_mppp_capi_cambiarestadocatalogos`), los dos menús de
las cuatro grillas de catálogos y la columna Comentarios de la autorización por cliente. El contrato de un módulo de
set está en `sets/__init__.py`; la preparación compartida, en `preparacion_comun.py`.

**Tres fuentes de evidencia, cada una corrida UNA sola vez por set** (no una vez por caso: 57 casos por subproceso
serían una hora de navegadores y compilaciones):

1. **La suite de C#** (`tests/Sanic.Mppp.Plugins.Tests`), con el logger TRX: resultado POR PRUEBA. Cada caso que el
   archivo marca "cubierto por pruebas automáticas" exige que CADA prueba que cita exista en el TRX y haya pasado
   (`PRUEBAS_CS`, generado del propio archivo). Una prueba citada que no aparece es FALLA: es el falso verde de un
   filtro que no matchea nada (`set_r.py::_dotnet_test`, mismo riesgo).
2. **El spec de Playwright** `pruebas/gui/casos/dn-04-catalogos-masivos.spec.mjs`, una corrida por rol
   (`MPPP_USUARIO`), con el reporter JSON: resultado POR PRUEBA, por título exacto. **Un test saltado NUNCA cuenta
   como aprobado**: es la diferencia con el puente de `set_a.py`, que suma los saltados al total. El único salto
   aceptado es el de L-03 para el Administrador técnico, y solo si el motivo dice que su sitemap no tiene el área
   Catalogos (la matriz de `diseno/04` le da "—" en las cinco tablas de catálogo).
3. **Llamadas a la Web API con las sesiones REALES** de los cuatro roles (`local/sesion-<papel>.json`), con
   Playwright de Python: la escritura de estas Custom API va con el usuario que llama, y una suplantación con
   `MSCRMCallerID` desde el service principal no prueba lo mismo. La sesión nunca se lee ni se imprime: se le pasa a
   Playwright como `storage_state`. La guarda de sesiones (`estado_sesiones.verificar_o_fallar`, CONVENCIONES §6)
   corre primero, en `preparar()`.

**Datos.** Todo lo que crea este set lleva CIF BAC que empieza con `99998`, razón social `ZZP-…` y correos
`zz-l-…@pruebas.mppp.local` con nombre de contacto `ZZP-…`. La limpieza (`_limpiar`, en `preparar()` para los restos
de una corrida que murió y en `cerrar()`) verifica la marca REGISTRO POR REGISTRO, justo antes de cada DELETE
(`marca_agua.py`); un plan o una autorización se borra solo si su cliente, releído en ese momento, lleva la marca.
Los datos del caso L-09 y del formulario de L-55 los crea y los borra el propio spec (`pruebas/gui/datos_dn04_l09.py`).

**L-17 y L-45 suben temporalmente el tope.** Con el tope en 100 no hay forma de llegar al presupuesto de 30 s ni de
provocar un rollback real. L-17 siembra una versión NUEVA de `carga.filas.maximo` con valor 200 (versionar, nunca
editar, CONVENCIONES §8.2), sube 200 filas que crean todo y espera el MPPP-CAR-008 a los ~30 s; la versión se
desactiva en el `finally` del caso y se borra por GUID en `cerrar()`. Si el set muriera a la mitad, el canon de
`correr.py` lleva la versión activa más alta a 100 igual (`preparacion_comun.CANON_SIMPLE`). L-45 verifica que esa
carga cortada dejó cero registros. La transacción dura 30 s, como la de L-48: no es de alto riesgo (la base de Dev
corta recién a los ~40 s, D-75), así que corre siempre, sin `--full`.

**L-20 (MPPP-CAR-011) es solo C#.** Se reprodujo en Dev UNA vez, con el ensamblado 1.0.21.0 y el presupuesto todavía
en 100 s (D-75). Con 30 s, cualquier archivo grande corta con el 008 antes de que la base llegue a su error: el 011 ya
no se puede provocar por volumen, y forzar la sobrecarga de la base por otros medios sería degradar el entorno a
propósito.

**L-04 en 3082 es N-A dentro del caso**: Dev solo tiene 1033 instalado. Instalar un idioma cambia el entorno y no es
algo que un set de pruebas haga.
"""
import base64
import glob
import io
import json
import os
import re
import subprocess
import sys
import tempfile
import time
import xml.etree.ElementTree as ET

_AQUI = os.path.dirname(os.path.abspath(__file__))
_HERRAMIENTAS_PRUEBAS = os.path.dirname(_AQUI)
_RAIZ = os.path.dirname(os.path.dirname(_HERRAMIENTAS_PRUEBAS))
_PRUEBAS_GUI = os.path.join(_RAIZ, "pruebas", "gui")
_LOCAL = os.path.join(_RAIZ, "local")
sys.path.insert(0, _HERRAMIENTAS_PRUEBAS)

import preparacion_comun as pc  # noqa: E402
import marca_agua  # noqa: E402
from dataverse_api import Dataverse  # noqa: E402

LETRA = "L"
NOMBRE = "Carga y activación masiva de catálogos (DN-04)"

CAPI_CARGA = "sanic_mppp_capi_cargarcatalogos"
CAPI_ESTADO = "sanic_mppp_capi_cambiarestadocatalogos"
CLIENTES = "sanic_mppp_tbl_clientes"
PLANES = "sanic_mppp_tbl_plans"
AUTORIZADOS = "sanic_mppp_tbl_autorizados"
AUT_CLIENTES = "sanic_mppp_tbl_autorizacionclientes"
PARAMETROS = "sanic_mppp_tbl_parametros"

PREFIJO_CIF = "99998"            # todo cliente de este set; ningún cliente real empieza así (se verifica en preparar)
PREFIJO_CORREO = "zz-l-"
DOMINIO = "@pruebas.mppp.local"
PARAMETRO_TOPE = "carga.filas.maximo"
TOPE = 100                        # D-75: el canon de carga.filas.maximo (CONVENCIONES §8.2)
PRESUPUESTO_S = 30                # D-75: CatalogosMasivos.PresupuestoSegundos

RUTA_PROYECTO_TESTS = os.path.join(_RAIZ, "tests", "Sanic.Mppp.Plugins.Tests")
SPEC_GUI = "dn-04-catalogos-masivos"
PAPELES = ["admin_planes", "ejecutivo", "supervisor", "admin_tecnico"]

ENCABEZADOS_CARGA = ["CIF BAC", "CIF COM", "Razon social", "Correo ejecutivo", "Codigo plan", "Tipo formato", "Moneda",
                     "Correo", "Nombre contacto", "Fecha documento", "Comentarios", "Resultado"]
ENCABEZADOS_ESTADO = ["Tipo", "Accion", "CIF BAC", "CIF COM", "Codigo plan", "Correo", "Resultado"]

# Títulos de los `describe` del spec (los casos exigen sus pruebas por título EXACTO: un título que cambia sin
# actualizar acá es FALLA "no existe en el reporte", nunca un verde por omisión).
D_ADMIN = "DN-04 — menús de catálogos (Administrador de planes)"
D_L09 = "DN-04 — carga y cambio de estado que graban (L-09, Administrador de planes)"
D_L55 = "DN-04 — L-55: Comentarios admite 50 caracteres en el formulario (Administrador de planes)"
D_ROLES = "DN-04 — menús de catálogos (roles que NO son Administrador de planes)"
GRILLAS = ["Clientes", "Planes", "Correos Autorizados", "Autorizaciones - Clientes"]
SALTO_SIN_AREA = r"no tiene el área 'Catalogos' en el sitemap"


# ===========================================================================
# Excel: armar (base64) y leer la columna Resultado
# ===========================================================================
def _xlsx(hoja, filas):
    """Un .xlsx mínimo y válido (celdas `inlineStr`), en base64, como lo manda el JS. `filas` es una lista de listas;
    la primera, los encabezados. Una celda vacía no se escribe (como Excel)."""
    import zipfile
    ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main"
    rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships"

    def esc(t):
        return t.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")

    def letra(k):
        return chr(65 + k)

    datos = "".join(
        f'<row r="{n}">' + "".join(f'<c r="{letra(k)}{n}" t="inlineStr"><is><t>{esc(v)}</t></is></c>'
                                   for k, v in enumerate(fila) if v != "") + "</row>"
        for n, fila in enumerate(filas, start=1))
    partes = {
        "[Content_Types].xml": '<?xml version="1.0" encoding="UTF-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>',
        "_rels/.rels": f'<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="{rel}/officeDocument" Target="xl/workbook.xml"/></Relationships>',
        "xl/workbook.xml": f'<?xml version="1.0" encoding="UTF-8"?><workbook xmlns="{ns}" xmlns:r="{rel}"><sheets><sheet name="{hoja}" sheetId="1" r:id="rId1"/></sheets></workbook>',
        "xl/_rels/workbook.xml.rels": f'<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="{rel}/worksheet" Target="worksheets/sheet1.xml"/></Relationships>',
        "xl/worksheets/sheet1.xml": f'<?xml version="1.0" encoding="UTF-8"?><worksheet xmlns="{ns}"><sheetData>{datos}</sheetData></worksheet>',
    }
    buffer = io.BytesIO()
    with zipfile.ZipFile(buffer, "w", zipfile.ZIP_DEFLATED) as z:
        for nombre, contenido in partes.items():
            z.writestr(nombre, contenido)
    return base64.b64encode(buffer.getvalue()).decode("ascii")


def _carga(filas):
    return _xlsx("CargaCatalogos", [ENCABEZADOS_CARGA] + filas)


def _estado(filas):
    return _xlsx("ActivarDesactivar", [ENCABEZADOS_ESTADO] + filas)


def _columna_resultado(resultado_b64):
    """`{fila de Excel: texto de la columna Resultado}` del archivo que devuelve la API (la última columna)."""
    import openpyxl
    libro = openpyxl.load_workbook(io.BytesIO(base64.b64decode(resultado_b64)), read_only=True)
    hoja = libro.worksheets[0]
    salida = {}
    for n, fila in enumerate(hoja.iter_rows(values_only=True), start=1):
        if n == 1:
            continue
        salida[n] = "" if fila[-1] is None else str(fila[-1])
    return salida


# ===========================================================================
# Fuente 1 — la suite de C#, una sola corrida, resultado por prueba (TRX)
# ===========================================================================
def _parsear_trx(xml_texto):
    """`{nombre del método: [outcome, ...]}`. Una `[Theory]` aparece una vez por juego de datos; el nombre del método
    es lo que va antes del paréntesis, sin el namespace ni la clase."""
    raiz = ET.fromstring(xml_texto)
    resultados = {}
    for nodo in raiz.iter():
        if not nodo.tag.endswith("UnitTestResult"):
            continue
        nombre = (nodo.get("testName") or "").split("(")[0].rsplit(".", 1)[-1]
        resultados.setdefault(nombre, []).append(nodo.get("outcome"))
    return resultados


def _csharp(ctx):
    """Corre la suite de C# UNA vez por set y guarda el resultado en `ctx.extra["csharp"]`:
    `{"resultados": {...}}` o `{"error": motivo}` (dotnet ausente, timeout, sin TRX: límite del entorno)."""
    if "csharp" in ctx.extra:
        return ctx.extra["csharp"]
    carpeta = tempfile.mkdtemp(prefix="set_l_trx_")
    try:
        proc = subprocess.run(
            ["dotnet", "test", RUTA_PROYECTO_TESTS, "--nologo", "--logger", "trx;LogFileName=set_l.trx",
             "--results-directory", carpeta],
            cwd=_RAIZ, capture_output=True, text=True, timeout=900)
    except FileNotFoundError as e:
        ctx.extra["csharp"] = {"error": f"'dotnet' no está disponible en el PATH: {e}"}
        return ctx.extra["csharp"]
    except subprocess.TimeoutExpired:
        ctx.extra["csharp"] = {"error": "dotnet test no terminó en 900 s"}
        return ctx.extra["csharp"]
    archivos = glob.glob(os.path.join(carpeta, "**", "*.trx"), recursive=True)
    if not archivos:
        salida = (proc.stdout or "") + (proc.stderr or "")
        ctx.extra["csharp"] = {"error": f"dotnet test no dejó TRX (rc={proc.returncode}):\n{salida[-1200:]}"}
        return ctx.extra["csharp"]
    with open(archivos[0], encoding="utf-8") as f:
        ctx.extra["csharp"] = {"resultados": _parsear_trx(f.read()), "rc": proc.returncode}
    return ctx.extra["csharp"]


def _chequear_csharp(c, ctx, id_caso):
    """Exige que cada prueba que el archivo cita para este caso exista en el TRX y haya pasado. Devuelve False si la
    suite no se pudo correr (el caso queda N-A, salvo que ya tenga otras comprobaciones)."""
    nombres = PRUEBAS_CS.get(id_caso, [])
    if not nombres:
        return True
    corrida = _csharp(ctx)
    if "error" in corrida:
        if not c.checks:
            c.na(f"no se pudo correr la suite de C#: {corrida['error']}")
        else:
            c.check("[C#] la suite se pudo correr", False, corrida["error"])
        return False
    for nombre in nombres:
        salidas = corrida["resultados"].get(nombre)
        c.check(f"[C#] {nombre} existe y pasa", bool(salidas) and all(s == "Passed" for s in salidas),
                "no aparece en el TRX: ¿se renombró o se borró la prueba?" if not salidas else f"resultados: {salidas}")
    return True


# ===========================================================================
# Fuente 2 — el spec de Playwright, una corrida por rol, resultado por prueba
# ===========================================================================
def _extraer_reporte_json(stdout):
    """Copia local (CONVENCIONES §8: un set no depende del código interno de otro) de `set_a.py`: el reporter JSON
    imprime UN objeto al final de stdout, después de los `console.log` del globalSetup. Se busca la última línea que es
    exactamente '{' y se parsea desde ahí."""
    posiciones = [m.start() for m in re.finditer(r"^\{[ \t]*$", stdout, re.MULTILINE)]
    if not posiciones:
        return None
    try:
        return json.loads(stdout[posiciones[-1]:])
    except json.JSONDecodeError:
        return None


def _parsear_reporte_gui(reporte):
    """`{"describe › prueba": {"estado", "razon", "error"}}`. Los `suites` de primer nivel son los archivos (su título
    no entra en la clave); los anidados, los `describe`. `estado` es el de Playwright: expected, skipped, unexpected o
    flaky. `razon` es la descripción de la anotación de salto, si la hay."""
    pruebas = {}

    def recorrer(suite, camino):
        for spec in suite.get("specs", []):
            clave = " › ".join(camino + [spec["title"]])
            prueba = (spec.get("tests") or [{}])[0]
            resultados = prueba.get("results") or [{}]
            anotaciones = list(prueba.get("annotations") or [])
            for r in resultados:
                anotaciones += r.get("annotations") or []
            razon = next((a.get("description", "") for a in anotaciones if a.get("type") == "skip"), "")
            error = ((resultados[-1].get("error") or {}).get("message") or "")
            pruebas[clave] = {"estado": prueba.get("status"), "razon": razon, "error": error[-600:]}
        for hija in suite.get("suites", []):
            recorrer(hija, camino + [hija["title"]])

    for archivo in reporte.get("suites", []):
        recorrer(archivo, [])
    return pruebas


def _gui(ctx, papel):
    """Corre el spec de DN-04 como `papel` UNA vez por set (`ctx.extra["gui"][papel]`): `{"pruebas": {...}}` o
    `{"error": motivo}`. `MPPP_SEMAFORO_HEREDADO=1`: `correr.py` ya tiene el semáforo (ver `set_a.py`)."""
    cache = ctx.extra.setdefault("gui", {})
    if papel in cache:
        return cache[papel]
    env = dict(os.environ, MPPP_USUARIO=papel, MPPP_SEMAFORO_HEREDADO="1")
    try:
        proc = subprocess.run(["npx", "playwright", "test", SPEC_GUI, "--reporter=json"],
                              cwd=_PRUEBAS_GUI, env=env, capture_output=True, text=True, timeout=1800)
    except FileNotFoundError as e:
        cache[papel] = {"error": f"'npx'/'playwright' no está disponible en el PATH: {e}", "entorno": True}
        return cache[papel]
    except subprocess.TimeoutExpired:
        cache[papel] = {"error": f"playwright test como {papel} no terminó en 1800 s", "entorno": True}
        return cache[papel]
    reporte = _extraer_reporte_json(proc.stdout or "")
    if reporte is None:
        salida = (proc.stdout or "") + "\n" + (proc.stderr or "")
        cache[papel] = {"error": f"Playwright no devolvió JSON válido (rc={proc.returncode}):\n{salida[-1500:]}"}
        return cache[papel]
    cache[papel] = {"pruebas": _parsear_reporte_gui(reporte)}
    return cache[papel]


def _chequear_gui(c, ctx, papel, claves, salto_aceptado=None):
    """Cada prueba de `claves` (título exacto "describe › prueba") tiene que haber PASADO en la corrida de `papel`.
    Un salto solo se acepta si `salto_aceptado` (regex) coincide con su motivo, y se deja anotado en el detalle."""
    corrida = _gui(ctx, papel)
    if "error" in corrida:
        if corrida.get("entorno") and not c.checks:
            c.na(corrida["error"])
        else:
            c.check(f"[GUI] la corrida de Playwright como {papel} devolvió su reporte", False, corrida["error"])
        return
    for clave in claves:
        p = corrida["pruebas"].get(clave)
        if p is None:
            c.check(f"[GUI {papel}] {clave}", False, "no existe en el reporte: ¿cambió el título de la prueba?")
        elif p["estado"] == "expected":
            c.check(f"[GUI {papel}] {clave}", True)
        elif p["estado"] == "skipped" and salto_aceptado and re.search(salto_aceptado, p["razon"]):
            c.check(f"[GUI {papel}] {clave} (saltada con motivo aceptado)", True, p["razon"])
        else:
            c.check(f"[GUI {papel}] {clave}", False,
                    f"estado {p['estado']}" + (f"; motivo: {p['razon']}" if p["razon"] else "")
                    + (f"; error: {p['error']}" if p["error"] else ""))


# ===========================================================================
# Fuente 3 — la Web API con las sesiones REALES de los cuatro roles
# ===========================================================================
_FETCH = """async ({metodo, ruta, cuerpo}) => {
  const cabeceras = {"Content-Type": "application/json", "Accept": "application/json", "OData-Version": "4.0",
                     "OData-MaxVersion": "4.0", "Prefer": 'odata.include-annotations="*"'};
  const t0 = performance.now();
  const resp = await fetch(`/api/data/v9.2/${ruta}`,
                           {method: metodo, headers: cabeceras, body: cuerpo === null ? undefined : JSON.stringify(cuerpo)});
  const ms = Math.round(performance.now() - t0);
  const texto = await resp.text();
  let json = null;
  try { json = texto ? JSON.parse(texto) : null; } catch (e) { json = {texto: texto.slice(0, 500)}; }
  return {http: resp.status, ms, cuerpo: json};
}"""


class _Sesiones:
    """Un navegador para todo el set y una página por rol, abierta en WhoAmI (mismo origen que la Web API, así el
    `fetch` relativo lleva la cookie de la sesión). Se cierra en `cerrar()`."""

    def __init__(self):
        self._pw = None
        self._navegador = None
        self._paginas = {}

    def pagina(self, papel):
        if self._pw is None:
            from playwright.sync_api import sync_playwright
            self._pw = sync_playwright().start()
            self._navegador = self._pw.chromium.launch(headless=True)
        if papel not in self._paginas:
            import estado_sesiones
            contexto = self._navegador.new_context(storage_state=os.path.join(_LOCAL, f"sesion-{papel}.json"))
            pagina = contexto.new_page()
            respuesta = pagina.goto(estado_sesiones.WHOAMI_URL, timeout=60000)
            if not respuesta or not respuesta.ok:
                raise RuntimeError(f"la sesión de {papel} no respondió WhoAmI (¿vencida?)")
            self._paginas[papel] = pagina
        return self._paginas[papel]

    def llamar(self, papel, metodo, ruta, cuerpo=None):
        """`(http, cuerpo, ms)` de la llamada hecha CON la sesión de `papel`."""
        r = self.pagina(papel).evaluate(_FETCH, {"metodo": metodo, "ruta": ruta, "cuerpo": cuerpo})
        return r["http"], r["cuerpo"], r["ms"]

    def cerrar(self):
        try:
            if self._navegador:
                self._navegador.close()
        finally:
            if self._pw:
                self._pw.stop()
            self._pw = self._navegador = None
            self._paginas = {}


def _api(ctx, papel, metodo, ruta, cuerpo=None):
    return ctx.extra["sesiones"].llamar(papel, metodo, ruta, cuerpo)


def _mensaje_de_error(cuerpo):
    return ((cuerpo or {}).get("error") or {}).get("message") or ""


def _codigo_de_error(cuerpo):
    return ((cuerpo or {}).get("error") or {}).get("code") or ""


def _subir(ctx, api, archivo_b64, papel="admin_planes"):
    return _api(ctx, papel, "POST", api, {"archivo": archivo_b64})


# ===========================================================================
# Consultas con el service principal y limpieza por marca
# ===========================================================================
def _todos(dv, ruta):
    filas = []
    while ruta:
        est, r, _ = dv.call("GET", ruta)
        if est != 200:
            raise RuntimeError(f"GET {ruta[:120]} devolvió HTTP {est}: {str(r)[:300]}")
        filas += r.get("value", [])
        ruta = (r.get("@odata.nextLink") or "").split("/api/data/v9.2/")[-1] or None
    return filas


def _cliente_marcado(dv, conjunto, id_registro):
    """Relee el registro (plan o autorización) JUSTO antes del DELETE y devuelve si su cliente lleva la marca y el CIF
    del set (marca_agua.py, regla 3: la verificación es por registro, nunca contra una lista de antes)."""
    est, r, _ = dv.call("GET", f"{conjunto}({id_registro})?$select=_sanic_clienteid_value"
                               "&$expand=sanic_clienteid($select=sanic_nombre,sanic_cifbac)")
    cliente = (r or {}).get("sanic_clienteid") or {}
    return est == 200 and marca_agua.marcado(cliente.get("sanic_nombre")) and \
        (cliente.get("sanic_cifbac") or "").startswith(PREFIJO_CIF)


def _limpiar(dv):
    """Borra lo de este set: autorizaciones y planes de clientes marcados, correos marcados, clientes marcados. Devuelve
    `(borrados, problemas)`; nunca lanza por un registro sin marca (no se toca y se informa)."""
    borrados, problemas = 0, []
    filtro_cliente = f"startswith(sanic_clienteid/sanic_cifbac,'{PREFIJO_CIF}')"
    for conjunto, idc in ((AUT_CLIENTES, "sanic_mppp_tbl_autorizacionclienteid"), (PLANES, "sanic_mppp_tbl_planid")):
        for fila in _todos(dv, f"{conjunto}?$select={idc}&$filter={filtro_cliente}"):
            if not _cliente_marcado(dv, conjunto, fila[idc]):
                problemas.append(f"{conjunto} {fila[idc]}: su cliente no lleva la marca: NO se borra")
                continue
            est, r, _ = dv.call("DELETE", f"{conjunto}({fila[idc]})")
            if est in (200, 204):
                borrados += 1
            else:
                problemas.append(f"DELETE {conjunto}({fila[idc]}) devolvió HTTP {est}: {str(r)[:200]}")
    for fila in _todos(dv, f"{AUTORIZADOS}?$select=sanic_mppp_tbl_autorizadoid,sanic_nombrecontacto"
                           f"&$filter=startswith(sanic_nombre,'{PREFIJO_CORREO}')"):
        ok, motivo = marca_agua.borrar_marcado(dv, AUTORIZADOS, fila["sanic_mppp_tbl_autorizadoid"],
                                               campo="sanic_nombrecontacto")
        if ok:
            borrados += 1
        elif "no lleva el prefijo" not in motivo:
            problemas.append(f"correo {fila['sanic_mppp_tbl_autorizadoid']}: {motivo}")
    for fila in _todos(dv, f"{CLIENTES}?$select=sanic_mppp_tbl_clienteid,sanic_nombre"
                           f"&$filter=startswith(sanic_cifbac,'{PREFIJO_CIF}')"):
        ok, motivo = marca_agua.borrar_marcado(dv, CLIENTES, fila["sanic_mppp_tbl_clienteid"])
        if ok:
            borrados += 1
        else:
            problemas.append(f"cliente {fila['sanic_mppp_tbl_clienteid']}: {motivo}")
    return borrados, problemas


def _parametro_activo(dv):
    filas = _todos(dv, f"{PARAMETROS}?$select=sanic_mppp_tbl_parametroid,sanic_version,sanic_valor,sanic_tipo"
                       f"&$filter=sanic_nombre eq '{PARAMETRO_TOPE}' and statecode eq 0&$orderby=sanic_version desc&$top=1")
    return filas[0] if filas else None


# ===========================================================================
# preparar / cerrar
# ===========================================================================
def preparar(ctx):
    """Deja listo TODO lo que el set L necesita (CONVENCIONES §8):

    - **La guarda de sesiones de los cuatro roles** (CONVENCIONES §6): viva y con su rol, o no se corre nada.
    - **Las sesiones abiertas** en un navegador de Python (`_Sesiones`) y los `systemuserid` de cada rol, por WhoAmI.
    - **El correo del Ejecutivo** (`# User 1`), que es el "Correo ejecutivo" de los clientes nuevos.
    - **El canon de `carga.filas.maximo` en 100** (lo aplica `correr.py`; acá se comprueba).
    - **Ningún resto** de una corrida anterior (`_limpiar`), y ningún dato REAL en los rangos reservados: un cliente
      con CIF `99998…` sin la marca, o un plan real con alguno de los códigos que este set crea, haría que una
      carga diera error de fila en vez de lo que el caso espera (falso FALLA) o que la limpieza no lo pudiera borrar.
    """
    import estado_sesiones
    estado_sesiones.verificar_o_fallar(PAPELES)

    ctx.extra["sesiones"] = _Sesiones()
    ids = {}
    for papel in PAPELES:
        est, cuerpo, _ = _api(ctx, papel, "GET", "WhoAmI")
        if est != 200 or "UserId" not in (cuerpo or {}):
            raise RuntimeError(f"WhoAmI con la sesión de {papel} devolvió HTTP {est}")
        ids[papel] = cuerpo["UserId"]
    ctx.extra["ids"] = ids

    est, r, _ = ctx.dv.call("GET", f"systemusers({ids['ejecutivo']})?$select=internalemailaddress")
    if est != 200 or not r.get("internalemailaddress"):
        raise RuntimeError(f"no se pudo leer el correo del Ejecutivo (HTTP {est})")
    ctx.extra["correo_ejecutivo"] = r["internalemailaddress"]

    activo = _parametro_activo(ctx.dv)
    if not activo or (activo.get("sanic_valor") or "").strip() != str(TOPE):
        raise RuntimeError(f"{PARAMETRO_TOPE} no está en su canon ({TOPE}): {activo}")

    _, problemas = _limpiar(ctx.dv)
    if problemas:
        raise RuntimeError("hay datos en los rangos reservados del set L que no llevan la marca:\n  " + "\n  ".join(problemas))
    reservados = ["PZ01", "PZ06"] + [f"Q{i:03d}" for i in range(1, TOPE + 1)] + [f"R{i:03d}" for i in range(1, 201)]
    ocupados = []
    for i in range(0, len(reservados), 40):
        tanda = reservados[i:i + 40]
        filtro = " or ".join(f"sanic_codigo eq '{x}'" for x in tanda)
        ocupados += [f["sanic_codigo"] for f in _todos(ctx.dv, f"{PLANES}?$select=sanic_codigo&$filter={filtro}")]
    if ocupados:
        raise RuntimeError(f"códigos de plan reservados por el set L ya existen en Dev (son de otro cliente): {ocupados}")


def cerrar(ctx):
    """Borra lo que creó el set (por marca, registro por registro), la versión de prueba del parámetro (por GUID,
    CONVENCIONES §8.2: "desactivar no es limpiar") y cierra el navegador. Devuelve los errores, nunca lanza."""
    errores = []
    idp = ctx.extra.get("parametro_prueba_id")
    if idp:
        try:
            est, r, _ = ctx.dv.call("GET", f"{PARAMETROS}({idp})?$select=sanic_descripcion,statecode")
            if est == 200 and marca_agua.marcado(r.get("sanic_descripcion")):
                if r.get("statecode") == 0:
                    ctx.dv.call("PATCH", f"{PARAMETROS}({idp})", {"statecode": 1, "statuscode": 2})
                est2, r2, _ = ctx.dv.call("DELETE", f"{PARAMETROS}({idp})")
                if est2 not in (200, 204):
                    errores.append(f"no se pudo borrar la versión de prueba de {PARAMETRO_TOPE} {idp}: HTTP {est2} {r2}")
            elif est == 200:
                errores.append(f"la versión {idp} de {PARAMETRO_TOPE} no lleva la marca en su descripción: NO se borra")
        except Exception as e:
            errores.append(f"limpieza del parámetro de prueba: {type(e).__name__}: {e}")
    try:
        _, problemas = _limpiar(ctx.dv)
        errores += problemas
    except Exception as e:
        errores.append(f"limpieza de catálogos: {type(e).__name__}: {e}")
    try:
        proc = subprocess.run([sys.executable, os.path.join(_PRUEBAS_GUI, "datos_dn04_l09.py"), "--limpiar"],
                              capture_output=True, text=True, timeout=300)
        if proc.returncode != 0:
            errores.append(f"datos_dn04_l09.py --limpiar rc={proc.returncode}: {(proc.stdout + proc.stderr)[-400:]}")
    except Exception as e:
        errores.append(f"datos_dn04_l09.py --limpiar: {type(e).__name__}: {e}")
    if ctx.extra.get("sesiones"):
        try:
            ctx.extra["sesiones"].cerrar()
        except Exception as e:
            errores.append(f"cierre del navegador: {type(e).__name__}: {e}")
    return errores


# ===========================================================================
# Casos de GUI (Playwright): L-01..L-06, L-08, L-09, L-13, L-43
# ===========================================================================
def _caso(id_caso):
    return pc.Caso(id_caso, TITULOS[id_caso])


def l01(ctx):
    c = _caso("L-01")
    _chequear_gui(c, ctx, "admin_planes",
                  [f"{D_ADMIN} › {g}: los dos menús están y cada uno trae sus dos opciones" for g in GRILLAS])
    _chequear_csharp(c, ctx, "L-01")
    return c


def _sin_menus(c, ctx, papel, salto_aceptado=None):
    _chequear_gui(c, ctx, papel,
                  [f"{D_ROLES} › {g}: '{papel}' no ve los menús de carga ni de activación masiva" for g in GRILLAS],
                  salto_aceptado=salto_aceptado)


def l02(ctx):
    c = _caso("L-02")
    _sin_menus(c, ctx, "ejecutivo")
    return c


def l03(ctx):
    c = _caso("L-03")
    _sin_menus(c, ctx, "supervisor")
    # El Administrador técnico no tiene el área Catalogos (matriz de `04`: "—" en las cinco tablas de catálogo): sus
    # cuatro pruebas se SALTAN con ese motivo. Cualquier otro salto, o una prueba que corra y vea un menú, es FALLA.
    _sin_menus(c, ctx, "admin_tecnico", salto_aceptado=SALTO_SIN_AREA)
    return c


def l04(ctx):
    c = _caso("L-04")
    _chequear_gui(c, ctx, "admin_planes", [
        f"{D_ADMIN} › L-04: con la interfaz en inglés, los menús salen en inglés (siguen el idioma) y un botón nativo también"])
    # La mitad en 3082 no se puede correr: Dev solo tiene 1033 instalado (instalar un idioma no lo hace un set).
    est, r, _ = ctx.dv.call("GET", "RetrieveProvisionedLanguages()")
    idiomas = (r or {}).get("RetrieveProvisionedLanguages") or []
    c.check("la mitad en 3082 queda N-A porque Dev solo tiene 1033 (si se instala 3082, este caso tiene que crecer)",
            est == 200 and idiomas == [1033], f"idiomas instalados: {idiomas}")
    return c


def l05(ctx):
    c = _caso("L-05")
    _chequear_gui(c, ctx, "admin_planes",
                  [f"{D_ADMIN} › 'Carga masiva' → 'Descargar plantilla' baja la plantilla vacía con sus encabezados"])
    _chequear_csharp(c, ctx, "L-05")
    return c


def l06(ctx):
    c = _caso("L-06")
    _chequear_gui(c, ctx, "admin_planes",
                  [f"{D_ADMIN} › 'Activar/Desactivar masivo' → 'Descargar plantilla' baja la plantilla vacía con sus encabezados"])
    _chequear_csharp(c, ctx, "L-06")
    return c


def l08(ctx):
    c = _caso("L-08")
    _chequear_gui(c, ctx, "admin_planes", [
        f"{D_ADMIN} › L-08 (a): 'Carga masiva' → 'Cargar archivo' y cancelar el selector no hace nada",
        f"{D_ADMIN} › L-08 (b): 'Activar/Desactivar masivo' → 'Cargar archivo' y cancelar el selector no hace nada",
        f"{D_ADMIN} › L-08 (c): 'Cargar archivo' con un .txt avisa que tiene que ser .xlsx y no llama a la API"])
    return c


def l09(ctx):
    c = _caso("L-09")
    _chequear_gui(c, ctx, "admin_planes", [
        f"{D_L09} › L-09: la carga que graba y el cambio de estado que la sigue (indicador, resultado, resumen y refresco de la grilla)"])
    return c


def l13(ctx):
    c = _caso("L-13")
    _chequear_gui(c, ctx, "admin_planes", [f"{D_ADMIN} › 'Cargar archivo' con la hoja equivocada muestra MPPP-CAR-004 y no descarga nada"])
    _chequear_csharp(c, ctx, "L-13")
    return c


def l43(ctx):
    c = _caso("L-43")
    _chequear_gui(c, ctx, "admin_planes",
                  [f"{D_ADMIN} › 'Cargar archivo' con un error de fila descarga el resultado y avisa que no se grabó nada"])
    _chequear_csharp(c, ctx, "L-43")
    return c


# ===========================================================================
# Casos con la Web API y sesiones reales
# ===========================================================================
def _fila_carga(cif_bac, cif_com, razon, correo_ejecutivo, plan="", formato="", moneda="", correo="", contacto="",
                fecha="", comentarios=""):
    return [cif_bac, cif_com, razon, correo_ejecutivo, plan, formato, moneda, correo, contacto, fecha, comentarios]


def _peor_caso(ctx, digito, n, letra_plan, letra_com, prefijo_correo):
    """`n` filas que crean cliente, plan, correo y autorización cada una (el peor caso medido en D-75)."""
    e = ctx.extra["correo_ejecutivo"]
    return [_fila_carga(f"{PREFIJO_CIF}{digito}{i:03d}", f"ZZP{letra_com}{i:05d}001", f"ZZP-L {letra_com} {i:03d}", e,
                        f"{letra_plan}{i:03d}", "10", "USD", f"{PREFIJO_CORREO}{prefijo_correo}-{i:03d}{DOMINIO}",
                        f"ZZP-{prefijo_correo} {i:03d}", "15/09/2026", "set L")
            for i in range(1, n + 1)]


def _contar(dv, conjunto, idc, filtro):
    return len(_todos(dv, f"{conjunto}?$select={idc}&$filter={filtro}"))


def l15(ctx):
    c = _caso("L-15")
    filas = _peor_caso(ctx, "2", TOPE + 1, "X", "X", "x")
    est, cuerpo, ms = _subir(ctx, CAPI_CARGA, _carga(filas))
    mensaje = _mensaje_de_error(cuerpo)
    c.check(f"{TOPE + 1} filas: la llamada se rechaza", est >= 400, f"HTTP {est}")
    c.check("con MPPP-CAR-006 y el máximo vigente en el mensaje",
            mensaje.startswith("MPPP-CAR-006") and f"más de {TOPE} filas" in mensaje, mensaje[:300])
    c.check("no se grabó ningún cliente", _contar(ctx.dv, CLIENTES, "sanic_mppp_tbl_clienteid",
                                                   f"startswith(sanic_cifbac,'{PREFIJO_CIF}2')") == 0)
    _chequear_csharp(c, ctx, "L-15")
    return c


def l17(ctx):
    """El presupuesto de 30 s en Dev: versión temporal del tope en 200 y 200 filas que crean todo (~52 s de trabajo)."""
    c = _caso("L-17")
    _chequear_csharp(c, ctx, "L-17")
    activo = _parametro_activo(ctx.dv)
    maximo = _todos(ctx.dv, f"{PARAMETROS}?$select=sanic_version&$filter=sanic_nombre eq '{PARAMETRO_TOPE}'"
                            "&$orderby=sanic_version desc&$top=1")
    if not activo or not maximo:
        c.check(f"{PARAMETRO_TOPE} tiene una versión activa (precondición)", False, str(activo))
        return c
    cuerpo_param = {
        "sanic_nombre": PARAMETRO_TOPE, "sanic_version": maximo[0]["sanic_version"] + 1, "sanic_valor": "200",
        "sanic_tipo": activo["sanic_tipo"],
        "sanic_descripcion": marca_agua.nombre_de_prueba(
            "version de PRUEBA del set L (L-17/L-45): tope 200 para llegar al presupuesto de 30 s. Se desactiva en "
            "el mismo caso y se borra por GUID en cerrar() (CONVENCIONES §8.2)."),
    }
    est, r, cab = ctx.dv.call("POST", PARAMETROS, cuerpo_param)
    if not c.check("se sembró la versión temporal del tope (versionar, nunca editar)", est in (200, 201, 204), f"HTTP {est}: {r}"):
        return c
    ctx.extra["parametro_prueba_id"] = Dataverse.id_creado(cab)
    try:
        est, cuerpo, ms = _subir(ctx, CAPI_CARGA, _carga(_peor_caso(ctx, "4", 200, "R", "R", "r")))
    finally:
        ctx.dv.call("PATCH", f"{PARAMETROS}({ctx.extra['parametro_prueba_id']})", {"statecode": 1, "statuscode": 2})
    mensaje = _mensaje_de_error(cuerpo)
    ctx.extra["l17"] = {"http": est, "mensaje": mensaje, "ms": ms}
    c.check("200 filas que crean todo cortan con MPPP-CAR-008 (no se graba nada)",
            est >= 400 and mensaje.startswith("MPPP-CAR-008"), f"HTTP {est} en {ms} ms: {mensaje[:300]}")
    c.check(f"el mensaje nombra los {PRESUPUESTO_S} segundos", f"({PRESUPUESTO_S} segundos)" in mensaje, mensaje[:300])
    c.check(f"cortó cerca de los {PRESUPUESTO_S} s, antes del corte de la base (~40 s, D-75)",
            PRESUPUESTO_S * 1000 - 1000 <= ms <= 39000, f"{ms} ms")
    vigente = _parametro_activo(ctx.dv)
    c.check(f"después del caso, la versión activa de {PARAMETRO_TOPE} vuelve a valer {TOPE}",
            vigente and (vigente.get("sanic_valor") or "").strip() == str(TOPE), str(vigente))
    return c


def _seed_l41(ctx):
    """Los datos del ejemplo del aprobador: Clientes 1, 2 y 3; plan PZ01 del Cliente 1; María sobre 1 y 2; Juan sobre 1
    y 3. Se siembran con la PROPIA carga masiva, como el Administrador de planes."""
    e = ctx.extra["correo_ejecutivo"]
    maria = (f"{PREFIJO_CORREO}maria{DOMINIO}", "ZZP-Maria")
    juan = (f"{PREFIJO_CORREO}juan{DOMINIO}", "ZZP-Juan")

    def cli(i):
        return [f"{PREFIJO_CIF}000{i}", f"ZZPCA000{i}001", f"ZZP-Cliente {i}", e]
    filas = [cli(1) + ["PZ01", "10", "USD", maria[0], maria[1], "", "set L"],
             cli(1) + ["", "", "", juan[0], juan[1], "", ""],
             cli(2) + ["", "", "", maria[0], maria[1], "", ""],
             cli(3) + ["", "", "", juan[0], juan[1], "", ""]]
    est, cuerpo, _ = _subir(ctx, CAPI_CARGA, _carga(filas))
    ctx.extra["l41_sembrado"] = est == 200 and (cuerpo or {}).get("grabado") is True
    return est, cuerpo


def _foto(dv):
    """Estado (0 activo, 1 inactivo) de todo lo del ejemplo del aprobador."""
    foto = {}
    for f in _todos(dv, f"{CLIENTES}?$select=sanic_nombre,statecode&$filter=startswith(sanic_cifcom,'ZZPCA000')"):
        foto[f["sanic_nombre"]] = f["statecode"]
    for f in _todos(dv, f"{PLANES}?$select=sanic_codigo,statecode&$filter=sanic_codigo eq 'PZ01'"):
        foto["plan PZ01"] = f["statecode"]
    for f in _todos(dv, f"{AUTORIZADOS}?$select=sanic_nombre,statecode&$filter=sanic_nombre eq '{PREFIJO_CORREO}maria{DOMINIO}'"
                        f" or sanic_nombre eq '{PREFIJO_CORREO}juan{DOMINIO}'"):
        foto["correo " + f["sanic_nombre"][len(PREFIJO_CORREO):].split("@")[0]] = f["statecode"]
    for f in _todos(dv, f"{AUT_CLIENTES}?$select=statecode&$expand=sanic_autorizadoid($select=sanic_nombre),"
                        "sanic_clienteid($select=sanic_nombre)"
                        f"&$filter=sanic_clienteid/sanic_cifcom eq 'ZZPCA0001001' or sanic_clienteid/sanic_cifcom eq "
                        "'ZZPCA0002001' or sanic_clienteid/sanic_cifcom eq 'ZZPCA0003001'"):
        quien = f["sanic_autorizadoid"]["sanic_nombre"][len(PREFIJO_CORREO):].split("@")[0]
        foto[f"aut {quien}@{f['sanic_clienteid']['sanic_nombre'][-1]}"] = f["statecode"]
    return foto


def l41(ctx):
    c = _caso("L-41")
    est, cuerpo = _seed_l41(ctx)
    if not c.check("se sembró el ejemplo con la carga masiva (3 clientes, 1 plan, 2 correos, 4 autorizaciones)",
                   ctx.extra["l41_sembrado"] and ((cuerpo or {}).get("clientescreados"), cuerpo.get("planescreados"),
                                                  cuerpo.get("correoscreados"), cuerpo.get("autorizacionescreadas")) == (3, 1, 2, 4),
                   f"HTTP {est}: {cuerpo}"):
        return c
    desde = time.time()
    est, cuerpo, _ = _subir(ctx, CAPI_ESTADO, _estado([["CLIENTE", "DESACTIVAR", f"{PREFIJO_CIF}0001"]]))
    c.check("desactivar el Cliente 1 se graba", est == 200 and cuerpo.get("grabado") is True and cuerpo.get("desactivados") == 1,
            f"HTTP {est}: {cuerpo if est != 200 else {k: v for k, v in cuerpo.items() if k != 'resultado'}}")
    if est == 200:
        texto = _columna_resultado(cuerpo["resultado"]).get(2, "")
        c.check("la fila dice Desactivado y advierte que deja sin efecto 2 autorizaciones",
                texto.startswith("Desactivado") and "deja sin efecto 2 autorizaciones" in texto, texto)
    foto = _foto(ctx.dv)
    c.check("sin cascada: el cliente quedó inactivo y su plan y sus dos autorizaciones siguen activos",
            foto.get("ZZP-Cliente 1") == 1 and foto.get("plan PZ01") == 0 and foto.get("aut maria@1") == 0
            and foto.get("aut juan@1") == 0, str(foto))
    # La auditoría vive en el almacenamiento de logs y aparece con demora (Learn, "Manage Dataverse auditing"): se
    # espera hasta 6 minutos un registro que NO sea el Create, hecho por el Administrador de planes y no por SYSTEM.
    id_cliente = _todos(ctx.dv, f"{CLIENTES}?$select=sanic_mppp_tbl_clienteid&$filter=sanic_cifbac eq '{PREFIJO_CIF}0001'")
    hallado, vistos = False, []
    if id_cliente:
        cid = id_cliente[0]["sanic_mppp_tbl_clienteid"]
        limite = time.time() + 360
        while time.time() < limite and not hallado:
            vistos = _todos(ctx.dv, f"audits?$select=createdon,action,_userid_value&$filter=_objectid_value eq {cid}")
            hallado = any(a.get("action") != 1 and a.get("_userid_value") == ctx.extra["ids"]["admin_planes"] for a in vistos)
            if not hallado:
                time.sleep(15)
    c.check("la auditoría registra el cambio hecho por el Administrador de planes (no por SYSTEM)", hallado,
            f"{len(vistos)} registro(s) de auditoría, esperados {round(time.time() - desde)} s: "
            + str([(a.get("action"), a.get("_userid_value")) for a in vistos]))
    return c


def l42(ctx):
    c = _caso("L-42")
    if not ctx.extra.get("l41_sembrado"):
        c.check("los datos del ejemplo existen (los siembra L-41)", False, "L-41 no pudo sembrarlos")
        return c
    juan, maria = f"{PREFIJO_CORREO}juan{DOMINIO}", f"{PREFIJO_CORREO}maria{DOMINIO}"
    # Preparación: Cliente 1 activo, Cliente 2 inactivo, juan@3 inactiva (para que las filas de ACTIVAR activen algo).
    est, cuerpo, _ = _subir(ctx, CAPI_ESTADO, _estado([
        ["CLIENTE", "ACTIVAR", f"{PREFIJO_CIF}0001"], ["CLIENTE", "DESACTIVAR", f"{PREFIJO_CIF}0002"],
        ["AUTORIZACION_CLIENTE", "DESACTIVAR", f"{PREFIJO_CIF}0003", "", "", juan]]))
    if not c.check("preparación del ejemplo", est == 200 and cuerpo.get("grabado") is True, f"HTTP {est}"):
        return c
    est, cuerpo, _ = _subir(ctx, CAPI_ESTADO, _estado([
        ["CLIENTE", "DESACTIVAR", f"{PREFIJO_CIF}0001"],
        ["CLIENTE", "ACTIVAR", "", "ZZPCA0002001"],
        ["PLAN", "DESACTIVAR", "", "", "PZ01"],
        ["CORREO", "DESACTIVAR", "", "", "", juan],
        ["AUTORIZACION_CLIENTE", "DESACTIVAR", f"{PREFIJO_CIF}0001", "", "", maria],
        ["AUTORIZACION_CLIENTE", "ACTIVAR", "", "ZZPCA0003001", "", juan]]))
    if not c.check("las seis filas se graban", est == 200 and cuerpo.get("grabado") is True, f"HTTP {est}: {_mensaje_de_error(cuerpo)}"):
        return c
    c.check("2 activados, 4 desactivados, 0 sin cambios",
            (cuerpo.get("activados"), cuerpo.get("desactivados"), cuerpo.get("sincambios")) == (2, 4, 0),
            str({k: v for k, v in cuerpo.items() if k != "resultado"}))
    textos = _columna_resultado(cuerpo["resultado"])
    esperados = {2: "Desactivado", 3: "Activado", 4: "Desactivado", 5: "Desactivado", 6: "Desactivado", 7: "Activado"}
    c.check("la columna Resultado dice lo que hizo cada fila",
            all(textos.get(f, "").startswith(t) for f, t in esperados.items()), str(textos))
    c.check("la fila 5 (correo de Juan) advierte que deja sin efecto 2 autorizaciones",
            "deja sin efecto 2 autorizaciones" in textos.get(5, ""), textos.get(5, ""))
    c.check("la fila 7 (activar juan@3) advierte que no va a valer porque el correo está inactivo",
            "no va a estar vigente" in textos.get(7, ""), textos.get(7, ""))
    foto = _foto(ctx.dv)
    esperado = {"ZZP-Cliente 1": 1, "ZZP-Cliente 2": 0, "ZZP-Cliente 3": 0, "plan PZ01": 1, "correo juan": 1,
                "correo maria": 0, "aut juan@1": 0, "aut juan@3": 0, "aut maria@1": 1, "aut maria@2": 0}
    c.check("el estado final es el del diseño (el plan solo; el correo sin tocar sus autorizaciones; solo maria@1)",
            foto == esperado, f"diferencias: { {k: (foto.get(k), v) for k, v in esperado.items() if foto.get(k) != v} }")
    return c


def l45(ctx):
    c = _caso("L-45")
    l17 = ctx.extra.get("l17")
    if not c.check("L-17 provocó una falla real en la pasada 2 (MPPP-CAR-008)",
                   bool(l17) and l17["mensaje"].startswith("MPPP-CAR-008"), str(l17)):
        return c
    c.check("la transacción deshizo todo: cero clientes de esa carga",
            _contar(ctx.dv, CLIENTES, "sanic_mppp_tbl_clienteid", f"startswith(sanic_cifbac,'{PREFIJO_CIF}4')") == 0)
    c.check("cero correos de esa carga",
            _contar(ctx.dv, AUTORIZADOS, "sanic_mppp_tbl_autorizadoid", f"startswith(sanic_nombre,'{PREFIJO_CORREO}r-')") == 0)
    c.check("cero planes de esa carga",
            _contar(ctx.dv, PLANES, "sanic_mppp_tbl_planid", "startswith(sanic_codigo,'R') and "
                                                              f"startswith(sanic_clienteid/sanic_cifbac,'{PREFIJO_CIF}4')") == 0)
    return c


def l46(ctx):
    c = _caso("L-46")
    e = ctx.extra["correo_ejecutivo"]
    archivo = _carga([_fila_carga(f"{PREFIJO_CIF}0006", "ZZPCA0006001", "ZZP-Cliente 6", e, "PZ06", "10", "USD",
                                  f"{PREFIJO_CORREO}eva{DOMINIO}", "ZZP-Eva", "", "set L")])
    est, cuerpo, _ = _subir(ctx, CAPI_CARGA, archivo)
    if not c.check("la primera subida crea todo", est == 200 and cuerpo.get("grabado") is True and cuerpo.get("clientescreados") == 1,
                   f"HTTP {est}: {_mensaje_de_error(cuerpo)}"):
        return c
    est, cuerpo, ms = _subir(ctx, CAPI_CARGA, archivo)
    c.check("la segunda subida del MISMO archivo se graba sin crear nada",
            est == 200 and cuerpo.get("grabado") is True and (cuerpo.get("clientescreados"), cuerpo.get("planescreados"),
                                                              cuerpo.get("correoscreados"), cuerpo.get("autorizacionescreadas")) == (0, 0, 0, 0),
            f"HTTP {est} en {ms} ms")
    if est == 200:
        texto = _columna_resultado(cuerpo["resultado"]).get(2, "")
        c.check("y la fila dice Sin cambios", texto.startswith("Sin cambios"), texto)
    _chequear_csharp(c, ctx, "L-46")
    return c


def l48(ctx):
    c = _caso("L-48")
    inicio = time.time()
    est, cuerpo, ms = _subir(ctx, CAPI_CARGA, _carga(_peor_caso(ctx, "3", TOPE, "Q", "M", "med")))
    ctx.extra["l48_grabado"] = est == 200 and (cuerpo or {}).get("grabado") is True
    c.check(f"{TOPE} filas que crean todo se graban dentro del presupuesto de {PRESUPUESTO_S} s", ctx.extra["l48_grabado"],
            f"HTTP {est} en {ms} ms: {_mensaje_de_error(cuerpo)[:300]}")
    if ctx.extra["l48_grabado"]:
        c.check(f"{TOPE} clientes, planes, correos y autorizaciones creados",
                (cuerpo.get("clientescreados"), cuerpo.get("planescreados"), cuerpo.get("correoscreados"),
                 cuerpo.get("autorizacionescreadas")) == (TOPE,) * 4,
                f"{ms} ms (medido el 2026-10-01: 25,1 s); total del caso {round(time.time() - inicio)} s")
    return c


def l49(ctx):
    c = _caso("L-49")
    if not c.check("hay clientes de L-48 para mirar", ctx.extra.get("l48_grabado"), "L-48 no grabó"):
        return c
    filas = _todos(ctx.dv, f"{CLIENTES}?$select=_ownerid_value,_createdby_value&$filter=startswith(sanic_cifbac,'{PREFIJO_CIF}3')")
    ids = ctx.extra["ids"]
    c.check(f"los {TOPE} clientes tienen de dueño al Ejecutivo", len(filas) == TOPE and all(f["_ownerid_value"] == ids["ejecutivo"] for f in filas),
            f"{len(filas)} clientes; dueños distintos: {sorted({f['_ownerid_value'] for f in filas})}")
    c.check(f"y de creador al Administrador de planes", all(f["_createdby_value"] == ids["admin_planes"] for f in filas),
            f"creadores: {sorted({f['_createdby_value'] for f in filas})}")
    return c


def l50(ctx):
    c = _caso("L-50")
    if not c.check("hay clientes de L-48 para mirar", ctx.extra.get("l48_grabado"), "L-48 no grabó"):
        return c
    est, cuerpo, _ = _api(ctx, "ejecutivo", "GET",
                          f"{CLIENTES}?$select=sanic_cifbac&$filter=startswith(sanic_cifbac,'{PREFIJO_CIF}3')")
    c.check(f"con su sesión, el Ejecutivo ve los {TOPE} clientes", est == 200 and len(cuerpo.get("value", [])) == TOPE,
            f"HTTP {est}, ve {len((cuerpo or {}).get('value', []))}")
    est, cuerpo, _ = _api(ctx, "ejecutivo", "GET",
                          f"{AUT_CLIENTES}?$select=sanic_nombre&$filter=startswith(sanic_clienteid/sanic_cifbac,'{PREFIJO_CIF}3')")
    c.check(f"y las {TOPE} autorizaciones", est == 200 and len(cuerpo.get("value", [])) == TOPE,
            f"HTTP {est}, ve {len((cuerpo or {}).get('value', []))}")
    return c


def l51(ctx):
    c = _caso("L-51")
    archivos = {
        CAPI_CARGA: ("prvCreatesanic_mppp_tbl_cliente",
                     _carga([[f"{PREFIJO_CIF}9999", "ZZPXX9999001"]])),          # cliente nuevo sin C ni D: error de fila
        CAPI_ESTADO: ("prvWritesanic_mppp_tbl_cliente",
                      _estado([["CLIENTE", "DESACTIVAR", f"{PREFIJO_CIF}9999"]])),  # no existe: error de fila
    }
    # Archivos con error de fila a propósito: aunque la puerta fallara, no podrían escribir nada.
    for papel in ("ejecutivo", "supervisor", "admin_tecnico"):
        for api, (privilegio, archivo) in archivos.items():
            for etiqueta, cuerpo_pedido in (("plantilla", {"plantilla": True}), ("archivo", {"archivo": archivo})):
                est, cuerpo, _ = _api(ctx, papel, "POST", api, cuerpo_pedido)
                mensaje = _mensaje_de_error(cuerpo)
                c.check(f"{papel} · {api.replace('sanic_mppp_capi_', '')} · {etiqueta}: 403 de la plataforma por falta de {privilegio}",
                        est == 403 and _codigo_de_error(cuerpo) == "0x80040220" and f"missing {privilegio} privilege" in mensaje
                        and "MPPP-CAR" not in mensaje, f"HTTP {est} {_codigo_de_error(cuerpo)}: {mensaje[-200:]}")
    return c


def l52(ctx):
    c = _caso("L-52")
    est, r, _ = ctx.dv.call("GET", "EntityDefinitions(LogicalName='sanic_mppp_tbl_autorizacioncliente')/Attributes(LogicalName='sanic_comentarios')"
                                   "/Microsoft.Dynamics.CRM.StringAttributeMetadata?$select=MaxLength,RequiredLevel,DisplayName")
    etiquetas = [(x["LanguageCode"], x["Label"]) for x in ((r or {}).get("DisplayName") or {}).get("LocalizedLabels", [])]
    c.check("T(50), opcional, \"Comentarios\" en 1033 (`01` §0: las etiquetas de metadatos van en 1033)",
            est == 200 and r.get("MaxLength") == 50 and r["RequiredLevel"]["Value"] == "None" and (1033, "Comentarios") in etiquetas,
            f"HTTP {est}: MaxLength {(r or {}).get('MaxLength')}, etiquetas {etiquetas}")
    formularios = _todos(ctx.dv, "systemforms?$select=name,formxml&$filter=objecttypecode eq 'sanic_mppp_tbl_autorizacioncliente' and type eq 2")
    seccion = None
    for f in formularios:
        for s in re.findall(r"<section\b.*?</section>", f["formxml"], re.S):
            campos = re.findall(r'datafieldname="([^"]+)"', s)
            if "sanic_comentarios" in campos:
                seccion = (f["name"], campos)
    justo_despues = bool(seccion) and seccion[0] == "Autorizacion por cliente" and "sanic_fechadocumento" in seccion[1] \
        and seccion[1].index("sanic_comentarios") == seccion[1].index("sanic_fechadocumento") + 1
    c.check("en el formulario \"Autorizacion por cliente\", en la sección de la evidencia, justo después de la fecha del documento",
            justo_despues, str(seccion))
    for vista in ("Autorizaciones por cliente activas", "Correos autorizados sobre este cliente"):
        filas = _todos(ctx.dv, f"savedqueries?$select=layoutxml&$filter=returnedtypecode eq 'sanic_mppp_tbl_autorizacioncliente' and name eq '{vista}'")
        c.check(f"columna en la vista \"{vista}\"", bool(filas) and 'cell name="sanic_comentarios"' in filas[0]["layoutxml"],
                "vista no encontrada" if not filas else "")
    return c


def l53(ctx):
    c = _caso("L-53")
    est, r, _ = ctx.dv.call("GET", "EntityDefinitions(LogicalName='sanic_mppp_tbl_autorizacionplan')/Attributes?$select=LogicalName"
                                   "&$filter=LogicalName eq 'sanic_comentarios'")
    c.check("AutorizacionPlan NO tiene la columna", est == 200 and r.get("value") == [], f"HTTP {est}: {r}")
    est, r, _ = ctx.dv.call("GET", "EntityDefinitions(LogicalName='sanic_mppp_tbl_autorizacioncliente')/Attributes(LogicalName='sanic_comentarios')?$select=IsAuditEnabled")
    c.check("en AutorizacionCliente la columna es auditada", est == 200 and (r.get("IsAuditEnabled") or {}).get("Value") is True, str(r)[:200])
    est, r, _ = ctx.dv.call("GET", "EntityDefinitions(LogicalName='sanic_mppp_tbl_autorizacioncliente')?$select=IsAuditEnabled")
    c.check("con la auditoría encendida en la tabla", est == 200 and (r.get("IsAuditEnabled") or {}).get("Value") is True, str(r)[:200])
    est, r, _ = ctx.dv.call("GET", "organizations?$select=isauditenabled")
    c.check("y en la organización", est == 200 and r["value"][0]["isauditenabled"] is True, str(r)[:200])
    return c


def l55(ctx):
    c = _caso("L-55")
    filas = _todos(ctx.dv, f"{AUT_CLIENTES}?$select=sanic_mppp_tbl_autorizacionclienteid&$filter=sanic_clienteid/sanic_cifbac eq "
                           f"'{PREFIJO_CIF}0002' and sanic_autorizadoid/sanic_nombre eq '{PREFIJO_CORREO}maria{DOMINIO}'")
    if c.check("existe una autorización del set para probar (maria@2, de L-41)", bool(filas)):
        aid = filas[0]["sanic_mppp_tbl_autorizacionclienteid"]
        est, cuerpo, _ = _api(ctx, "admin_planes", "PATCH", f"{AUT_CLIENTES}({aid})", {"sanic_comentarios": "X" * 51})
        c.check("Web API, 51 caracteres: la plataforma lo rechaza por largo",
                est == 400 and "maximum allowed length of '50'" in _mensaje_de_error(cuerpo), f"HTTP {est}: {_mensaje_de_error(cuerpo)[:200]}")
        est, _, _ = _api(ctx, "admin_planes", "PATCH", f"{AUT_CLIENTES}({aid})", {"sanic_comentarios": "Y" * 50})
        est2, cuerpo2, _ = _api(ctx, "admin_planes", "GET", f"{AUT_CLIENTES}({aid})?$select=sanic_comentarios")
        c.check("Web API, 50 caracteres: se guardan los 50", est == 204 and est2 == 200 and cuerpo2.get("sanic_comentarios") == "Y" * 50,
                f"PATCH {est}, GET {est2}")
    _chequear_gui(c, ctx, "admin_planes", [f"{D_L55} › L-55: tipear 51 caracteres en Comentarios deja 50 en el campo y se guardan 50"])
    return c


def l57(ctx):
    c = _caso("L-57")
    activo = _parametro_activo(ctx.dv)
    c.check(f"{PARAMETRO_TOPE}: la versión activa más alta vale {TOPE} en Dev",
            bool(activo) and (activo.get("sanic_valor") or "").strip() == str(TOPE), str(activo))
    _chequear_csharp(c, ctx, "L-57")
    return c


# ===========================================================================
# Casos cubiertos solo por la suite de C#
# ===========================================================================
def _solo_csharp(id_caso):
    def caso(ctx):
        c = _caso(id_caso)
        _chequear_csharp(c, ctx, id_caso)
        return c
    caso.__name__ = id_caso.lower().replace("-", "")
    return caso


# Generados del propio archivo `pruebas/12-catalogos-masivos.md` (títulos de los casos y, por caso, las pruebas de C#
# que cita su sección "Automatizable"; a L-14 se le sumaron las dos que cubren encabezados en otro orden y en otra
# capitalización). Si el archivo cambia, se regeneran: un caso nuevo sin entrada acá revienta al importar (abajo).
TITULOS = {
    'L-01': 'El Administrador de planes ve los dos menús, con sus dos opciones, en las cuatro grillas',
    'L-02': 'El Ejecutivo no ve ninguno de los dos menús, en ninguna de las cuatro grillas',
    'L-03': 'El Supervisor y el Administrador técnico tampoco ven los menús',
    'L-04': 'Los menús siguen el idioma de la interfaz',
    'L-05': '"Descargar plantilla" de Carga masiva baja `CargaCatalogos.xlsx` vacía, con sus encabezados',
    'L-06': '"Descargar plantilla" de Activar/Desactivar baja `ActivarDesactivar.xlsx` vacía, con sus encabezados',
    'L-07': 'Plantilla, archivo de resultado y archivo corregido tienen la misma forma: se baja, se corrige y se vuelve a subir tal cual',
    'L-08': 'Cancelar el selector no hace nada; un archivo que no es `.xlsx` avisa sin llamar a la API',
    'L-09': 'El indicador por etapas, el resumen de una carga grabada y el refresco de la grilla',
    'L-10': '`MPPP-CAR-001`: archivo vacío o que no es base64',
    'L-11': '`MPPP-CAR-002`: no es un `.xlsx` legible',
    'L-12': '`MPPP-CAR-003`: el archivo supera el peso de `lectura.limites`',
    'L-13': '`MPPP-CAR-004`: no existe la hoja esperada, y no se descarga nada',
    'L-14': '`MPPP-CAR-005`: encabezados que faltan, sobran o están fuera de orden (los nombra)',
    'L-15': '`MPPP-CAR-006`: más de 100 filas, rechazado entero y sin escribir',
    'L-16': '`MPPP-CAR-007`: ninguna fila de datos',
    'L-17': '`MPPP-CAR-008`: se agotó el presupuesto de 30 s; no se grabó nada',
    'L-18': '`MPPP-CAR-009`: falla un error de dato al grabar (pasada 2), nombra la fila y no graba nada',
    'L-19': '`MPPP-CAR-010`: `plantilla = true` y además vino `archivo`',
    'L-20': '`MPPP-CAR-011`: falla de plataforma al leer o al escribir, sin nombrar ninguna fila',
    'L-21': 'Ningún error de archivo muestra el texto de la librería, y todos empiezan con su código',
    'L-22': 'Reglas 1 y 2: la fila necesita su cliente, y los dos CIF tienen que ser coherentes con lo grabado',
    'L-23': 'Regla 3: un cliente existente no se modifica; si el archivo lo contradice, error; si está inactivo, advertencia',
    'L-24': 'Regla 4: un cliente nuevo exige A, B, C y D, y el ejecutivo tiene que ser exactamente un usuario habilitado con el rol Ejecutivo',
    'L-25': 'Reglas 5 y 7: los datos de un plan sin código son error, y un plan nuevo exige tipo de formato y moneda',
    'L-26': 'Regla 6: un plan existente tiene que ser del cliente de la fila; si el archivo lo contradice, error; si está inactivo, advertencia',
    'L-27': 'Reglas 8 y 9: los datos de una autorización sin correo son error, y el correo tiene que tener formato válido',
    'L-28': 'Reglas 10 y 11: un correo existente se reutiliza (sin pisar su nombre); uno nuevo exige Nombre contacto',
    'L-29': 'Reglas 12, 13 y 14: una autorización existente activa queda "ya existía", una inactiva es error y una nueva nace sin evidencia firmada',
    'L-30': 'Regla 15: la misma clave tiene que tener los mismos datos en todas sus filas',
    'L-31': 'Regla 16: un mismo CIF BAC con dos CIF COM distintos en el archivo (o al revés) es error',
    'L-32': 'Lectura de celdas: números sin ceros, notación científica, filas vacías y número de fila de Excel',
    'L-33': 'Fecha documento: celda de fecha o `dd/mm/aaaa`; futura, ilegible o anterior al 01/01/1900 es error',
    'L-34': 'Una fila reporta todos sus errores, y ningún mensaje cita un valor entero',
    'L-35': 'Regla 1: Tipo o Accion fuera de su lista es error (sin distinguir mayúsculas ni espacios)',
    'L-36': 'Regla 2: cada Tipo exige sus claves y ninguna otra',
    'L-37': 'Regla 3: el registro tiene que existir; en `AUTORIZACION_CLIENTE`, el par (correo, cliente) tiene que existir',
    'L-38': 'Regla 4: lo que ya está en el estado pedido es "Sin cambios", no error',
    'L-39': 'Regla 5: el mismo registro dos veces con acciones opuestas es error; con la misma acción, se aplica una vez',
    'L-40': 'Regla 6: advertencias de efecto, calculadas sobre el estado final del archivo',
    'L-41': 'Regla 7 y sin cascada: el cambio de estado es el nativo, y desactivar un cliente o un correo no toca lo que depende de él',
    'L-42': 'El ejemplo del aprobador, de punta a punta en Dev',
    'L-43': 'Con un error de fila no se graba nada: se descarga el resultado y el diálogo dice "No se grabó nada"',
    'L-44': 'Las filas sin error dicen "Sin errores — no se grabó: el archivo tiene errores en otras filas"',
    'L-45': 'Si una escritura falla en la pasada 2, la transacción deja cero registros',
    'L-46': 'Re-subir el mismo archivo no crea nada ("Sin cambios")',
    'L-47': 'Un archivo desnormalizado crea cada cosa una sola vez, y escribe en orden de dependencias',
    'L-48': '100 filas, el peor caso, se graban dentro del presupuesto de 30 s',
    'L-49': 'El cliente nuevo queda con el ejecutivo como dueño; el que cargó queda como creador',
    'L-50': 'El Ejecutivo dueño ve el cliente nuevo y sus autorizaciones',
    'L-51': 'La puerta de las dos Custom API: solo el Administrador de planes entra',
    'L-52': 'La columna Comentarios existe, en el formulario y en las dos vistas',
    'L-53': 'Comentarios solo en `AutorizacionCliente`, no en `AutorizacionPlan`; y auditada',
    'L-54': 'Comentarios: hasta 50 caracteres por Excel; 51 es error de fila',
    'L-55': 'Comentarios: la plataforma rechaza 51 caracteres también por el formulario y por Web API',
    'L-56': 'Los comentarios de la carga quedan guardados en la autorización nueva, y no se pisan en una existente',
    'L-57': '`carga.filas.maximo` vale 100, y sin un valor usable el tope es 100',
}

PRUEBAS_CS = {
    'L-07': [
        'La_plantilla_vacia_trae_solo_los_encabezados_y_el_lector_estricto_la_acepta_sin_filas',
        'El_resultado_va_en_la_misma_fila_con_los_valores_leidos_y_se_puede_volver_a_subir',
        'El_resultado_del_cambio_de_estado_tambien_se_puede_volver_a_subir',
    ],
    'L-10': [
        'Archivo_vacio_o_base64_invalido_es_MPPP_CAR_001',
    ],
    'L-11': [
        'Los_rechazos_del_lector_se_traducen_a_su_codigo_sin_escribir_nada',
    ],
    'L-12': [
        'Un_archivo_que_supera_lectura_limites_es_MPPP_CAR_003',
        'El_tope_de_peso_se_mira_antes_de_decodificar_el_base64',
    ],
    'L-13': [
        'Los_rechazos_del_lector_se_traducen_a_su_codigo_sin_escribir_nada',
    ],
    'L-14': [
        'Los_rechazos_del_lector_se_traducen_a_su_codigo_sin_escribir_nada',
        'Encabezados_en_otro_orden_son_MPPP_CAR_005_y_el_mensaje_nombra_los_que_no_coinciden',
        'Estricto_compara_encabezados_sin_mayusculas_tildes_ni_espacios_a_los_lados',
    ],
    'L-15': [
        'Los_rechazos_del_lector_se_traducen_a_su_codigo_sin_escribir_nada',
    ],
    'L-16': [
        'Los_rechazos_del_lector_se_traducen_a_su_codigo_sin_escribir_nada',
    ],
    'L-17': [
        'Presupuesto_agotado_es_MPPP_CAR_008',
        'Con_el_presupuesto_agotado_no_escribe_mas_y_dice_cuantas_alcanzo',
        'Si_no_hay_nada_que_escribir_el_presupuesto_no_tira_el_resultado',
    ],
    'L-18': [
        'Una_escritura_que_falla_es_MPPP_CAR_009_con_su_fila_y_sube_para_que_la_transaccion_deshaga_todo',
        'Un_error_de_dato_sigue_siendo_MPPP_CAR_009_y_pide_corregir_su_fila',
        'Un_fault_sin_codigo_conocido_ni_detalle_sigue_siendo_MPPP_CAR_009',
        'El_motivo_de_Dataverse_en_el_009_va_recortado',
    ],
    'L-19': [
        'Con_plantilla_y_sin_archivo_devuelve_la_plantilla_vacia_sin_escribir',
        'Plantilla_y_archivo_juntos_es_MPPP_CAR_010_y_ninguno_es_MPPP_CAR_001',
    ],
    'L-20': [
        'Una_falla_de_plataforma_es_MPPP_CAR_011_y_no_culpa_a_la_fila',
        'El_cambio_de_estado_tambien_distingue_la_falla_de_plataforma',
        'El_codigo_de_Dataverse_queda_en_la_traza_en_hexadecimal_y_el_usuario_recibe_el_011',
        'Una_falla_de_plataforma_al_leer_los_catalogos_es_MPPP_CAR_011',
        'Una_falla_de_plataforma_al_leer_con_SYSTEM_tambien_es_MPPP_CAR_011',
        'El_cambio_de_estado_tambien_da_MPPP_CAR_011_si_la_plataforma_falla_al_leer',
        'El_texto_del_011_es_el_mismo_al_leer_y_al_escribir',
    ],
    'L-21': [
        'Los_mensajes_de_rechazo_empiezan_con_su_codigo_y_no_traen_jerga_de_la_libreria',
        'Un_rechazo_sale_con_su_mensaje_y_un_error_inesperado_con_el_generico_y_la_traza',
    ],
    'L-22': [
        'Sin_CIF_BAC_ni_CIF_COM_la_fila_es_error',
        'El_cliente_se_encuentra_por_CIF_BAC_normalizado_y_se_autoriza_el_correo_existente',
        'El_cliente_se_encuentra_por_CIF_COM_en_minuscula',
        'CIF_BAC_y_CIF_COM_de_dos_clientes_distintos_es_error_que_nombra_a_los_dos',
        'Un_CIF_encuentra_un_cliente_y_el_otro_no_existe_es_error',
        'CIF_BAC_con_formato_invalido_es_error_de_esa_columna',
        'CIF_COM_con_formato_invalido_es_error_de_esa_columna',
    ],
    'L-23': [
        'Cliente_existente_con_otra_razon_social_es_error_y_no_se_pisa',
        'Cliente_existente_con_otro_ejecutivo_es_error_y_con_el_mismo_no',
        'Cliente_existente_inactivo_es_advertencia_no_error',
        'Razon_social_de_mas_de_200_es_error_y_de_200_no',
    ],
    'L-24': [
        'Cliente_nuevo_incompleto_nombra_cada_columna_que_falta',
        'El_ejecutivo_tiene_que_ser_exactamente_un_usuario_habilitado_con_el_rol_y_cada_caso_tiene_su_motivo',
        'El_correo_del_ejecutivo_se_compara_sin_mayusculas_ni_espacios_a_los_lados',
        'Cliente_nuevo_completo_se_crea_normalizado_con_el_ejecutivo_como_dueno',
        'Los_usuarios_se_leen_con_SYSTEM_y_dicen_si_estan_habilitados_y_si_son_ejecutivos',
        'Si_el_rol_de_ejecutivo_no_existe_en_el_entorno_es_un_error_de_configuracion_que_lo_nombra',
    ],
    'L-25': [
        'Tipo_formato_o_moneda_sin_codigo_de_plan_es_error',
        'Tipo_formato_acepta_06_10_11_y_6',
        'Tipo_formato_invalido_es_error',
        'Moneda_invalida_es_error',
        'Plan_nuevo_incompleto_nombra_lo_que_falta',
        'Codigo_de_plan_invalido_es_error',
        'Plan_nuevo_de_cliente_existente_se_crea_normalizado',
        'Formato_11_no_tiene_regla_de_moneda_ni_de_clasificacion_al_crear_el_plan',
    ],
    'L-26': [
        'Plan_existente_del_mismo_cliente_no_se_crea_y_sus_datos_tienen_que_coincidir',
        'Plan_existente_de_otro_cliente_es_error_que_nombra_al_cliente_real',
        'Plan_existente_con_un_cliente_nuevo_en_la_fila_es_error',
        'Plan_existente_inactivo_es_advertencia',
        'Plan_nuevo_de_un_cliente_nuevo_apunta_al_cliente_por_su_CIF_BAC',
    ],
    'L-27': [
        'Nombre_fecha_o_comentarios_sin_correo_es_error',
        'Correo_con_formato_invalido_es_error',
        'Correo_nuevo_crea_el_correo_y_la_autorizacion_normalizados',
        'Largos_maximos_de_correo_nombre_y_comentarios',
    ],
    'L-28': [
        'Correo_nuevo_sin_nombre_de_contacto_es_error',
        'Correo_existente_con_otro_nombre_es_error_y_con_el_mismo_no',
        'Correo_existente_se_autoriza_sobre_otro_cliente_usando_su_id',
        'Correo_existente_inactivo_es_advertencia',
    ],
    'L-29': [
        'Autorizacion_existente_activa_queda_ya_existia_y_sus_datos_tienen_que_coincidir',
        'Autorizacion_existente_inactiva_es_error_se_reactiva_con_el_otro_boton',
        'Correo_nuevo_crea_el_correo_y_la_autorizacion_normalizados',
        'Fecha_documento_acepta_celda_de_fecha_y_texto_dd_mm_aaaa_sin_hora',
    ],
    'L-30': [
        'La_misma_clave_con_datos_distintos_es_error_en_todas_las_filas_y_nombra_a_las_otras',
        'Los_datos_de_un_cliente_nuevo_pueden_repartirse_entre_filas_si_no_se_contradicen',
        'Si_a_un_cliente_nuevo_le_falta_un_dato_en_todo_el_archivo_el_error_va_en_todas_sus_filas',
        'El_mismo_plan_nuevo_bajo_dos_clientes_es_error_en_las_dos_filas',
        'Sobre_algo_que_ya_existe_lleva_error_solo_la_fila_que_contradice_lo_grabado',
        'La_misma_autorizacion_en_varias_filas_se_crea_una_vez_y_las_celdas_vacias_no_contradicen',
    ],
    'L-31': [
        'Un_CIF_BAC_con_dos_CIF_COM_en_el_archivo_o_al_reves_es_error',
        'Un_CIF_BAC_solo_y_un_CIF_COM_solo_son_el_mismo_cliente_nuevo_si_otra_fila_trae_los_dos',
    ],
    'L-32': [
        'Una_celda_con_error_de_Excel_no_se_graba_en_silencio',
        'Un_aviso_del_lector_es_error_de_su_fila_con_el_encabezado_de_su_columna',
    ],
    'L-33': [
        'Fecha_documento_acepta_celda_de_fecha_y_texto_dd_mm_aaaa_sin_hora',
        'Fecha_documento_futura_o_ilegible_es_error',
        'Fecha_documento_anterior_a_1900_es_error_en_la_pasada_1',
        'Fecha_documento_desde_el_1_de_enero_de_1900_vale',
        'La_fecha_de_hoy_la_decide_quien_construye_el_nucleo',
    ],
    'L-34': [
        'Una_fila_reporta_todos_sus_errores_no_solo_el_primero',
        'El_texto_lleva_errores_luego_la_accion_y_al_final_las_advertencias',
        'Ningun_valor_enorme_en_ninguna_columna_produce_un_Resultado_enorme',
        'Correo_del_ejecutivo_de_mas_de_320_es_error_corto_y_no_entra_a_las_claves',
        'Un_texto_de_mas_de_32767_caracteres_es_error_y_uno_de_32767_no',
        'Un_valor_del_usuario_de_mas_de_32767_caracteres_se_escribe_acortado_en_el_eco_sin_fallar',
        'Un_caracter_que_XML_no_admite_se_reemplaza_y_nunca_tira_el_armado',
    ],
    'L-35': [
        'Tipo_fuera_de_su_lista_es_error',
        'Accion_fuera_de_su_lista_es_error',
        'Tipo_y_accion_se_comparan_sin_mayusculas_ni_espacios_a_los_lados',
    ],
    'L-36': [
        'Cada_tipo_exige_sus_claves_y_ninguna_otra',
        'Cliente_por_CIF_BAC_por_CIF_COM_o_por_los_dos_si_coinciden',
        'Cliente_con_CIF_que_no_coinciden_inexistente_o_sin_CIF_es_error',
    ],
    'L-37': [
        'Cliente_con_CIF_que_no_coinciden_inexistente_o_sin_CIF_es_error',
        'Plan_por_codigo_normalizado_inexistente_o_invalido',
        'Correo_por_correo_normalizado_inexistente_o_invalido',
        'Autorizacion_por_cliente_es_el_par_correo_cliente_y_tiene_que_existir_como_par',
    ],
    'L-38': [
        'Lo_que_ya_esta_en_el_estado_pedido_no_cambia_y_no_es_error',
        'Los_contadores_cuentan_cambios_reales',
    ],
    'L-39': [
        'El_mismo_registro_con_la_misma_accion_se_aplica_una_vez_y_la_segunda_fila_lo_advierte',
        'El_mismo_registro_con_acciones_opuestas_es_error_en_las_dos_filas_nombrando_a_la_otra',
        'Opuestas_y_repetidas_se_detectan_aunque_una_de_las_filas_tenga_otro_error',
        'Una_repetida_de_tres_filas_que_nombran_el_registro_de_formas_distintas_se_aplica_una_vez',
    ],
    'L-40': [
        'Desactivar_un_cliente_o_un_correo_advierte_cuantas_autorizaciones_activas_deja_sin_efecto',
        'El_efecto_se_cuenta_sobre_el_estado_final_del_archivo',
        'Activar_una_autorizacion_cuyo_cliente_o_correo_queda_inactivo_advierte_que_no_va_a_estar_vigente',
        'Pedir_activar_una_autorizacion_que_ya_esta_activa_pero_no_vigente_tambien_advierte',
    ],
    'L-41': [
        'El_escritor_cambia_el_estado_nativo_en_la_tabla_de_cada_tipo',
        'Si_un_cambio_de_estado_falla_lanza_con_su_fila_y_no_sigue',
        'El_cambio_de_estado_escribe_con_el_usuario_y_cuenta_lo_que_cambio',
    ],
    'L-44': [
        'Con_un_error_en_cualquier_fila_no_se_crea_nada_y_las_filas_sanas_lo_dicen',
        'Con_un_error_en_cualquier_fila_no_cambia_nada',
        'Una_fila_reporta_todos_sus_errores_y_el_texto_respeta_el_orden',
        'Con_errores_de_fila_devuelve_el_resultado_sin_escribir_nada',
    ],
    'L-47': [
        'Archivo_desnormalizado_tipico_crea_cada_cosa_una_sola_vez_y_la_atribuye_a_la_primera_fila',
        'Los_resultados_salen_en_el_orden_recibido_y_lo_crea_la_fila_de_menor_numero',
        'El_escritor_crea_en_orden_con_las_columnas_exactas_y_resuelve_las_referencias_a_lo_nuevo',
    ],
    'L-54': [
        'Largos_maximos_de_correo_nombre_y_comentarios',
    ],
    'L-56': [
        'Autorizacion_existente_activa_queda_ya_existia_y_sus_datos_tienen_que_coincidir',
        'Largos_maximos_de_correo_nombre_y_comentarios',
    ],
    'L-57': [
        'Sin_un_carga_filas_maximo_usable_el_tope_es_100',
        'Los_parametros_se_leen_con_SYSTEM_nunca_con_el_usuario',
    ],
}



_PROPIOS = {"L-01": l01, "L-02": l02, "L-03": l03, "L-04": l04, "L-05": l05, "L-06": l06, "L-08": l08, "L-09": l09,
            "L-13": l13, "L-15": l15, "L-17": l17, "L-41": l41, "L-42": l42, "L-43": l43, "L-45": l45, "L-46": l46,
            "L-48": l48, "L-49": l49, "L-50": l50, "L-51": l51, "L-52": l52, "L-53": l53, "L-55": l55, "L-57": l57}

# En el orden del archivo (L-01..L-57). El orden importa: L-42 y L-55 usan los datos que siembra L-41; L-45 lee lo que
# provocó L-17; L-49 y L-50, lo que grabó L-48.
CASOS = [_PROPIOS.get(f"L-{i:02d}") or _solo_csharp(f"L-{i:02d}") for i in range(1, 58)]

_sin_evidencia = [f"L-{i:02d}" for i in range(1, 58) if f"L-{i:02d}" not in _PROPIOS and f"L-{i:02d}" not in PRUEBAS_CS]
if _sin_evidencia:
    raise ImportError(f"set_l: casos sin ninguna comprobación (ni propia ni de C#): {_sin_evidencia}")
