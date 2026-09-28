#!/usr/bin/env python3
"""Set H — Lo construido el 2026-09-27 (`pruebas/13-construido-hoy.md`).

El sitemap (entrada nueva de Fila, los dos renombres y la entrada nueva de
Catalogos), la columna `sanic_requiereatencionmanual` en las 4 vistas de
trabajo (y en ninguna otra), el formulario nuevo de `AutorizacionCliente`, la
tabla `sanic_mppp_tbl_autorizacioncliente` como componente REAL de la app, y
la lección del botón fantasma (REGLAS vs. METADATA COMPILADA). El contrato
completo de un módulo de set está en `sets/__init__.py`; la lógica de
preparación compartida, en `preparacion_comun.py`.

**Corregido contra CONVENCIONES.md al escribir este módulo** (§1, §7, §8): el
MD original marcaba H-01 a H-05 como `GUI` (inspección visual). Eso estaba mal
por partida doble:

1. **Es verificable contra la plataforma sin navegador.** El sitemap, el
   `layoutxml`/`fetchxml` de una vista y el `formxml` de un formulario son
   metadata publicada, legible por Web API con el mismo service principal que
   usan los demás sets — exactamente el mismo criterio que ya aplicó el set U
   para H-08/H-09/H-10 de `10-autorizacion-cliente.md` (ver su docstring:
   "se pudo automatizar leyendo el `formxml` publicado ... sin navegador").
2. **Una inspección visual no es lo que dice `CONVENCIONES` §2**: "la
   verificación va contra la plataforma, no contra la salida del
   constructor" — y tampoco contra el ojo de quien mira. Un `<cell>` que falta
   en un `layoutxml` es un hecho objetivo; "se ve la columna" no lo es de la
   misma forma (¿en qué resolución, con qué idioma de sesión?).

Por eso este módulo corre H-01 a H-05 y el caso nuevo H-07 como `API`. La
única excepción real es **H-06**, que se deja como `N-A` (Manual) a
propósito: es la lección del día, y su punto central es que NINGUNA prueba de
API la detecta (ver el docstring del propio caso).

**H-07 es nuevo**, no estaba en la versión anterior del MD: la tabla
`sanic_mppp_tbl_autorizacioncliente` como componente de la app se resolvió
con un import manual de la solución (no con `app.py` en modo escritura), así
que merece su propio caso que la verifique EN VIVO contra
`RetrieveAppComponents` — nombrada, no contada (CONVENCIONES §7): no se
afirma "12 tablas" (ese número puede cambiar sin que nada esté roto; es
exactamente el error que ya costó caro con "12 parámetros activos" y "13
entradas de sitemap|CONVENCIONES §7|"), se afirma que ESTA tabla puntual está.

**Este set no crea ningún dato.** Es lectura de metadata ya publicada
(sitemap, vistas, formulario, componentes de la app): no hay ninguna
Solicitud, Fila, Cliente ni Autorizado que sembrar, y por lo tanto nada que
`cerrar()` tenga que borrar.
"""
import json
import os
import subprocess
import sys
import xml.etree.ElementTree as ET

_AQUI = os.path.dirname(os.path.abspath(__file__))
_HERRAMIENTAS_PRUEBAS = os.path.dirname(_AQUI)
_RAIZ = os.path.dirname(os.path.dirname(_HERRAMIENTAS_PRUEBAS))
sys.path.insert(0, _HERRAMIENTAS_PRUEBAS)

import preparacion_comun as pc  # noqa: E402

LETRA = "H"
NOMBRE = "Construido el 2026-09-27"

SITEMAP_UNIQUE = "sanic_mppp_sm_mantenimientoppp"
APP_UNIQUE = "sanic_mppp_mda_mantenimientoppp"

TABLA_FILA = "sanic_mppp_tbl_fila"
TABLA_AUTORIZADO = "sanic_mppp_tbl_autorizado"
TABLA_AUTORIZACIONPLAN = "sanic_mppp_tbl_autorizacionplan"
TABLA_AUTORIZACIONCLIENTE = "sanic_mppp_tbl_autorizacioncliente"

COLUMNA_ATENCION_MANUAL = "sanic_requiereatencionmanual"
ANCLA_NUMEROFILA = "sanic_numerofila"

# Las 4 vistas de trabajo que SÍ tienen que traer la columna (nombradas, no
# contadas — CONVENCIONES §7), y las 3 vistas de Fila que NO son de trabajo y
# por lo tanto NO tienen que traerla.
VISTAS_DE_TRABAJO = [
    "Mis clientes - por digitar",
    "Todos - por digitar",
    "Por aprobar",
    "Mis clientes - devueltas",
]
VISTAS_SIN_LA_COLUMNA = ["Filas", "Terminadas", "Tomadas por el bot"]

# `classid` del control de Lookup en un `formxml` (Learn; el mismo valor que
# usa `herramientas/construir/formulario.py`, CONTROL_POR_TIPO["LookupType"]).
CLASSID_LOOKUP = "{270BD3DB-D9AF-4782-9025-509E298DEC0A}"
FORMULARIO_PRINCIPAL = 2  # systemform.type: 2 = Main (formulario.py, FORMULARIO_PRINCIPAL)


# ===========================================================================
# preparar / cerrar
# ===========================================================================
def preparar(ctx):
    """Este set NO crea ningún dato (CONVENCIONES §8): es lectura de metadata
    ya publicada contra la plataforma (sitemap, vistas, formulario,
    componentes de la app). Lo único que deja listo es el propio `sitemapxml`
    en vivo, leído UNA sola vez y compartido entre H-01/H-03/H-05 (para no
    repetir la misma llamada de red tres veces) — el CONTENIDO de ese XML
    (qué entradas tiene, en qué orden, con qué título) es lo que cada caso
    verifica, nunca algo que `preparar()` da por sentado: si faltara una
    entrada, el caso que la busca tiene que dar FALLA, no tumbar el set entero.

    Lo que SÍ es una precondición real de `preparar()` (y por eso lanza si
    falla): que el objeto `sitemap` en sí exista en el entorno. Sin eso no hay
    NADA que ningún caso de sitemap pueda revisar.

    **Lo que NO prepara, y por qué:**

    - **Roles ni sesiones de navegador** (CONVENCIONES §6). Los siete casos
      automatizables de este set (H-01 a H-05, H-07) se verifican contra Web
      API directo, con el service principal `# PowerPlatform-Dev` (System
      Administrator) — igual que I-09/I-10 en `set_i.py`. Ninguno abre la app.
    - **El canon de configuración global** (§8.2). Lo aplica `correr.py` antes
      y después de CADA set, no cada set por separado; además este set no
      toca ningún parámetro.
    """
    ctx.extra["sitemapxml"] = _leer_sitemapxml(ctx.dv)


def cerrar(ctx):
    """Nada que borrar: este set no creó ninguna Solicitud, Fila, Cliente ni
    Autorizado (ver `preparar()`). Se mantiene por contrato (`sets/__init__.py`
    exige `cerrar`), documentando por qué está vacío en vez de omitirlo en
    silencio."""
    return []


# ===========================================================================
# Helpers — sitemap
# ===========================================================================
def _leer_sitemapxml(dv):
    est, r, _ = dv.call(
        "GET", f"sitemaps?$select=sitemapxml&$filter=sitemapnameunique eq '{SITEMAP_UNIQUE}'")
    filas = r.get("value", []) if est == 200 else []
    if len(filas) != 1:
        raise RuntimeError(f"no se encontró (una sola vez) el sitemap {SITEMAP_UNIQUE!r} (HTTP {est}): {filas}")
    return filas[0]["sitemapxml"]


def _titulos_de(elemento):
    """`{LCID: Title}` del hijo directo `<Titles>` de un `Area`/`Group`/`SubArea`."""
    nodo = elemento.find("Titles")
    if nodo is None:
        return {}
    return {t.get("LCID"): t.get("Title") for t in nodo.findall("Title")}


def _area_por_titulo_1033(root, titulo):
    for area in root.findall("Area"):
        if _titulos_de(area).get("1033") == titulo:
            return area
    return None


def _subareas_por_id(elemento):
    """Todas las `<SubArea>` descendientes de `elemento` (un `Area` o la raíz
    `SiteMap`), indexadas por su `Id` — en el orden en que aparecen en el XML,
    que es justo lo que las posiciones de H-01 necesitan."""
    return {s.get("Id"): s for s in elemento.findall(".//SubArea")}


def _en_orden_consecutivo(ids_en_documento, subsecuencia):
    """`True` si cada id de `subsecuencia` aparece en `ids_en_documento`, EN
    ESE ORDEN y CONSECUTIVOS (sin nada en el medio) — lo que pide H-01: no
    alcanza con que estén, tienen que estar exactamente uno después del otro."""
    if any(i not in ids_en_documento for i in subsecuencia):
        return False
    posiciones = [ids_en_documento.index(i) for i in subsecuencia]
    return all(posiciones[k + 1] - posiciones[k] == 1 for k in range(len(posiciones) - 1))


# ===========================================================================
# Helpers — vistas (savedqueries) y formulario (systemforms)
# ===========================================================================
def _savedquery(ctx, nombre, tabla):
    est, r, _ = ctx.dv.call(
        "GET",
        "savedqueries?$select=savedqueryid,name,fetchxml,layoutxml,isdefault"
        f"&$filter=name eq '{nombre}' and returnedtypecode eq '{tabla}'")
    filas = r.get("value", []) if est == 200 else []
    if len(filas) != 1:
        raise RuntimeError(f"la vista {nombre!r} sobre {tabla!r} no se encontró de forma única (HTTP {est}): {filas}")
    return filas[0]


def _celdas_del_layout(layoutxml_texto):
    root = ET.fromstring(layoutxml_texto)
    fila = root.find("row")
    return [c.get("name") for c in fila.findall("cell")] if fila is not None else []


def _atributos_del_fetch(fetchxml_texto):
    root = ET.fromstring(fetchxml_texto)
    entidad = root.find("entity")
    return [a.get("name") for a in entidad.findall("attribute")] if entidad is not None else []


def _controles_con_campo(elemento):
    """`{datafieldname: classid}` de todos los `<control>` con `datafieldname`
    bajo `elemento` (un `<header>` o un `<section>` del `formxml`)."""
    return {ctrl.get("datafieldname"): ctrl.get("classid")
            for ctrl in elemento.findall(".//control") if ctrl.get("datafieldname")}


def _seccion_por_titulo(root, titulo):
    for seccion in root.findall(".//section"):
        etiqueta = seccion.find("labels/label")
        if etiqueta is not None and etiqueta.get("description") == titulo:
            return seccion
    return None


# ===========================================================================
# H-01 — Sitemap: "Para atencion manual" en el área Trabajo, en su posición.
# ===========================================================================
def h01(ctx):
    c = pc.Caso("H-01", "Sitemap: 'Para atencion manual' en Trabajo, entre 'Por aprobar' y 'Por clasificar'")
    root = ET.fromstring(ctx.extra["sitemapxml"])
    area = _area_por_titulo_1033(root, "Trabajo")
    if area is None:
        c.check("existe el area con titulo 1033 = 'Trabajo' en el sitemap en vivo", False)
        return c

    ids = [s.get("Id") for s in area.findall(".//SubArea")]
    c.check(
        "en el area Trabajo, 'subarea_paraatencionmanual' esta EXACTAMENTE entre "
        "'subarea_poraprobar' y 'subarea_porclasificar' (consecutivas, sin nada en medio)",
        _en_orden_consecutivo(ids, ["subarea_poraprobar", "subarea_paraatencionmanual", "subarea_porclasificar"]),
        f"orden real de subareas en Trabajo: {ids}")

    por_id = _subareas_por_id(area)
    entrada = por_id.get("subarea_paraatencionmanual")
    if entrada is None:
        c.check("la entrada 'subarea_paraatencionmanual' existe en el sitemap en vivo", False, f"ids presentes: {ids}")
        return c

    titulos = _titulos_de(entrada)
    c.check("titulo 1033 = 'Para atencion manual'", titulos.get("1033") == "Para atencion manual", titulos)
    c.check("titulo 3082 = 'Para atencion manual'", titulos.get("3082") == "Para atencion manual", titulos)

    url = entrada.get("Url") or ""
    c.check("la entrada abre la tabla sanic_mppp_tbl_fila (Url con etn=sanic_mppp_tbl_fila)",
            "etn=sanic_mppp_tbl_fila" in url, url)

    try:
        vista = _savedquery(ctx, "Para atencion manual", TABLA_FILA)
    except RuntimeError as e:
        c.check("la vista 'Para atencion manual' (la que la entrada dice abrir) existe sobre Fila", False, str(e))
        return c
    guid_vista = (vista["savedqueryid"] or "").replace("-", "").lower()
    url_normalizada = url.replace("-", "").lower()
    c.check("la Url del sitemap apunta al savedqueryid REAL de la vista 'Para atencion manual' "
            "(no una vista distinta ni un id inventado)", guid_vista and guid_vista in url_normalizada, url)
    return c


# ===========================================================================
# H-02 — Columna sanic_requiereatencionmanual en las 4 vistas de trabajo, y
# en ninguna otra (layoutxml/fetchxml reales, no la salida de ninguna
# herramienta).
# ===========================================================================
def h02(ctx):
    c = pc.Caso("H-02", "Columna 'Requiere atencion manual' en las 4 vistas de trabajo, y en ninguna otra")
    for nombre in VISTAS_DE_TRABAJO:
        try:
            vista = _savedquery(ctx, nombre, TABLA_FILA)
        except RuntimeError as e:
            c.check(f"{nombre!r}: la vista existe", False, str(e))
            continue
        celdas = _celdas_del_layout(vista["layoutxml"])
        atributos = _atributos_del_fetch(vista["fetchxml"])
        c.check(f"{nombre!r}: el layoutxml trae la celda {COLUMNA_ATENCION_MANUAL!r}",
                COLUMNA_ATENCION_MANUAL in celdas, celdas)
        c.check(f"{nombre!r}: el fetchxml trae el atributo {COLUMNA_ATENCION_MANUAL!r}",
                COLUMNA_ATENCION_MANUAL in atributos, atributos)
        if COLUMNA_ATENCION_MANUAL in celdas and ANCLA_NUMEROFILA in celdas:
            c.check(f"{nombre!r}: {COLUMNA_ATENCION_MANUAL!r} va inmediatamente despues de {ANCLA_NUMEROFILA!r}",
                    celdas.index(COLUMNA_ATENCION_MANUAL) == celdas.index(ANCLA_NUMEROFILA) + 1, celdas)

    for nombre in VISTAS_SIN_LA_COLUMNA:
        try:
            vista = _savedquery(ctx, nombre, TABLA_FILA)
        except RuntimeError as e:
            c.check(f"{nombre!r}: la vista existe", False, str(e))
            continue
        celdas = _celdas_del_layout(vista["layoutxml"])
        c.check(f"{nombre!r}: NO trae la celda {COLUMNA_ATENCION_MANUAL!r} (no es vista de trabajo)",
                COLUMNA_ATENCION_MANUAL not in celdas, celdas)
    return c


# ===========================================================================
# H-03 — Los dos renombres y la entrada nueva en Catalogos (sitemap en vivo).
# ===========================================================================
def h03(ctx):
    c = pc.Caso("H-03", "Catalogos: 'Correos Autorizados', 'Autorizaciones - Planes' y 'Autorizaciones - Clientes'")
    root = ET.fromstring(ctx.extra["sitemapxml"])
    area = _area_por_titulo_1033(root, "Catalogos")
    if area is None:
        c.check("existe el area con titulo 1033 = 'Catalogos' en el sitemap en vivo", False)
        return c
    por_id = _subareas_por_id(area)

    autorizados = por_id.get("subarea_autorizados")
    if autorizados is None:
        c.check("existe 'subarea_autorizados'", False)
    else:
        t = _titulos_de(autorizados)
        c.check("'subarea_autorizados': titulo 1033 = 'Correos Autorizados' (renombrado de 'Autorizados')",
                t.get("1033") == "Correos Autorizados", t)
        c.check("'subarea_autorizados': titulo 3082 = 'Correos Autorizados'", t.get("3082") == "Correos Autorizados", t)
        c.check("'subarea_autorizados' abre sanic_mppp_tbl_autorizado",
                autorizados.get("Entity") == TABLA_AUTORIZADO, autorizados.get("Entity"))

    autorizaciones = por_id.get("subarea_autorizaciones")
    if autorizaciones is None:
        c.check("existe 'subarea_autorizaciones'", False)
    else:
        t = _titulos_de(autorizaciones)
        c.check("'subarea_autorizaciones': titulo 1033 = 'Autorizaciones - Planes' (renombrado de 'Autorizaciones')",
                t.get("1033") == "Autorizaciones - Planes", t)
        c.check("'subarea_autorizaciones': titulo 3082 = 'Autorizaciones - Planes'", t.get("3082") == "Autorizaciones - Planes", t)
        c.check("'subarea_autorizaciones' abre sanic_mppp_tbl_autorizacionplan",
                autorizaciones.get("Entity") == TABLA_AUTORIZACIONPLAN, autorizaciones.get("Entity"))

    autorizacionescliente = por_id.get("subarea_autorizacionescliente")
    if autorizacionescliente is None:
        c.check("existe la entrada NUEVA 'subarea_autorizacionescliente'", False)
    else:
        t = _titulos_de(autorizacionescliente)
        c.check("'subarea_autorizacionescliente': titulo 1033 = 'Autorizaciones - Clientes'",
                t.get("1033") == "Autorizaciones - Clientes", t)
        c.check("'subarea_autorizacionescliente': titulo 3082 = 'Autorizaciones - Clientes'",
                t.get("3082") == "Autorizaciones - Clientes", t)
        c.check("'subarea_autorizacionescliente' abre sanic_mppp_tbl_autorizacioncliente (DN-01)",
                autorizacionescliente.get("Entity") == TABLA_AUTORIZACIONCLIENTE, autorizacionescliente.get("Entity"))

    if autorizaciones is not None and autorizacionescliente is not None:
        c.check("son DOS entradas separadas que abren tablas DISTINTAS (no la misma vista duplicada)",
                autorizaciones.get("Entity") != autorizacionescliente.get("Entity"),
                (autorizaciones.get("Entity"), autorizacionescliente.get("Entity")))

    # Cada una abre, por Entity=, la vista POR DEFECTO de su propia tabla: que
    # esa vista por defecto sea la correcta se confirma contra savedqueries en
    # vivo (no alcanza con que el sitemap "apunte a la tabla").
    for tabla, nombre_vista_default in ((TABLA_AUTORIZACIONPLAN, "Autorizaciones activas"),
                                         (TABLA_AUTORIZACIONCLIENTE, "Autorizaciones por cliente activas")):
        est, r, _ = ctx.dv.call(
            "GET", "savedqueries?$select=name&$filter="
                   f"returnedtypecode eq '{tabla}' and isdefault eq true and querytype eq 0")
        filas = r.get("value", []) if est == 200 else []
        c.check(f"la vista por defecto de {tabla!r} es {nombre_vista_default!r}",
                len(filas) == 1 and filas[0].get("name") == nombre_vista_default, filas)
    return c


# ===========================================================================
# H-04 — Formulario "Autorizacion por cliente": encabezado + evidencia,
# leyendo el formxml real de systemforms (no inspección visual).
# ===========================================================================
def h04(ctx):
    c = pc.Caso("H-04", "Formulario 'Autorizacion por cliente': encabezado (2 lookups) + seccion de evidencia")
    est, r, _ = ctx.dv.call(
        "GET", "systemforms?$select=formid,name,formxml&$filter="
               f"objecttypecode eq '{TABLA_AUTORIZACIONCLIENTE}' and type eq {FORMULARIO_PRINCIPAL}")
    filas = r.get("value", []) if est == 200 else []
    if len(filas) != 1:
        c.check("hay exactamente un formulario Main de AutorizacionCliente", False, f"HTTP {est}: {filas}")
        return c

    root = ET.fromstring(filas[0]["formxml"])

    header = root.find("header")
    if header is None:
        c.check("el formulario tiene encabezado (<header>)", False)
    else:
        controles = _controles_con_campo(header)
        for campo in ("sanic_nombre", "sanic_autorizadoid", "sanic_clienteid"):
            c.check(f"el encabezado trae el campo {campo!r}", campo in controles, sorted(controles))
        for campo in ("sanic_autorizadoid", "sanic_clienteid"):
            c.check(f"{campo!r} es un control de tipo Lookup (classid {CLASSID_LOOKUP})",
                    controles.get(campo) == CLASSID_LOOKUP, controles.get(campo))

    seccion = _seccion_por_titulo(root, "Evidencia de la autorizacion")
    if seccion is None:
        c.check("existe la seccion separada 'Evidencia de la autorizacion' (no mezclada con el encabezado)", False)
    else:
        controles_evidencia = _controles_con_campo(seccion)
        c.check("la seccion 'Evidencia de la autorizacion' trae sanic_documentofirmado",
                "sanic_documentofirmado" in controles_evidencia, sorted(controles_evidencia))
        c.check("la seccion 'Evidencia de la autorizacion' trae sanic_fechadocumento",
                "sanic_fechadocumento" in controles_evidencia, sorted(controles_evidencia))
    return c


# ===========================================================================
# H-05 — El icono reutilizado, no uno nuevo (mismo /WebResources/... en el
# sitemap en vivo, sin abrir el navegador).
# ===========================================================================
def h05(ctx):
    c = pc.Caso("H-05", "El icono es reutilizado, no uno nuevo (mismo web resource en el sitemap en vivo)")
    root = ET.fromstring(ctx.extra["sitemapxml"])
    por_id = {}
    for area in root.findall("Area"):
        por_id.update(_subareas_por_id(area))

    pararevisar, paraatencionmanual = por_id.get("subarea_pararevisar"), por_id.get("subarea_paraatencionmanual")
    if pararevisar is None or paraatencionmanual is None:
        c.check("existen 'subarea_pararevisar' y 'subarea_paraatencionmanual' para comparar su icono", False,
                {"pararevisar": pararevisar is not None, "paraatencionmanual": paraatencionmanual is not None})
    else:
        c.check("'Para atencion manual' reusa el icono de 'Para revisar' (mismo web resource, "
                "sin crear uno nuevo): /WebResources/sanic_mppp_wr_svg_pararevisar",
                paraatencionmanual.get("Icon") == pararevisar.get("Icon") == "/WebResources/sanic_mppp_wr_svg_pararevisar",
                {"pararevisar": pararevisar.get("Icon"), "paraatencionmanual": paraatencionmanual.get("Icon")})

    autorizaciones, autorizacionescliente = por_id.get("subarea_autorizaciones"), por_id.get("subarea_autorizacionescliente")
    if autorizaciones is None or autorizacionescliente is None:
        c.check("existen 'subarea_autorizaciones' y 'subarea_autorizacionescliente' para comparar su icono", False,
                {"autorizaciones": autorizaciones is not None, "autorizacionescliente": autorizacionescliente is not None})
    else:
        c.check("'Autorizaciones - Clientes' reusa el icono de 'Autorizaciones - Planes' (mismo web resource, "
                "sin crear uno nuevo): /WebResources/sanic_mppp_wr_svg_autorizaciones",
                autorizacionescliente.get("Icon") == autorizaciones.get("Icon") == "/WebResources/sanic_mppp_wr_svg_autorizaciones",
                {"autorizaciones": autorizaciones.get("Icon"), "autorizacionescliente": autorizacionescliente.get("Icon")})
    return c


# ===========================================================================
# H-06 — La lección del botón fantasma. GUI, N-A a propósito: NINGUNA prueba
# de API la detecta, ese es el punto (ver `pruebas/13-construido-hoy.md`).
# ===========================================================================
def h06(ctx):
    c = pc.Caso("H-06", "La leccion del boton fantasma: distinguir REGLAS de METADATA COMPILADA")
    c.na(
        "Requiere el Command Checker en vivo ('&ribbondebug=true') y, si hay que regenerar metadata, acceso de "
        "CUSTOMIZACION DE LA SOLUCION en make.powerapps.com sobre 'sanic_mppp_sol_mantenimientoppp' — privilegio de "
        "PLATAFORMA (System Administrator/System Customizer de Dataverse), NO el rol de negocio "
        "'SR - MPPP - Administrador tecnico' que prueba 'local/sesion-admin_tecnico.json' (acotado a Parametro/"
        "Reglas dentro de la app, '04-matriz-privilegios.md'). Ninguna prueba de API detecta este defecto y ESE es "
        "el punto: 'RetrieveEntityRibbon', los 'ribbondiff' con sus LocLabels en 1033/3082 y el verificador del "
        "proyecto confirmaron el boton 'Descargar para digitar' PUBLICADO en el servidor, y ninguno de los tres vio "
        "que no se dibujaba en el cliente (dos dias de diagnostico, 2026-09-27). La causa: la metadata COMPILADA del "
        "ribbon, que 'PublishAllXml' NO regenera; se arregla con el boton 'Regenerate ribbon metadata' del Command "
        "Checker. Guarda previa obligatoria: 'python3 herramientas/pruebas/estado_sesiones.py'. Con sesion "
        "'local/sesion-ejecutivo.json' viva y el rol 'SR - MPPP - Ejecutivo' confirmado, el arnes que SI mira el DOM "
        "real (no confia en 'publicado' == 'se dibuja') se corre desde 'pruebas/gui/': "
        "'MPPP_USUARIO=ejecutivo npx playwright test ribbon-fila' (ver 'pruebas/gui/ayudantes/ribbon.mjs', que "
        "codifica esta misma distincion: boton AUSENTE de la lista del Command Checker -> sospechar metadata "
        "compilada antes que el EnableRule; boton presente pero '(hidden)' -> revisar el EnableRule, la metadata "
        "esta bien). Este set NUNCA duplica ese arnes (CONVENCIONES §9). PENDIENTE fuera de este set, para quien "
        "mantenga el documento de instalación en otro entorno: después de importar la solución y publicar "
        "(PublishAllXml), hay que regenerar la metadata del ribbon desde el Command Checker o los botones custom "
        "no aparecen, aunque la publicación haya terminado sin ningún error.")
    return c


# ===========================================================================
# H-07 (nuevo) — sanic_mppp_tbl_autorizacioncliente como componente REAL de
# la app (RetrieveAppComponents en vivo, nombrada, no contada — CONVENCIONES
# §7: no se afirma un total de tablas, se afirma que ESTA tabla está).
# ===========================================================================
def _tablas_de_la_app(ctx, id_app):
    est, r, _ = ctx.dv.call("GET", f"RetrieveAppComponents(AppModuleId={id_app})")
    if est != 200:
        raise RuntimeError(f"RetrieveAppComponents devolvió HTTP {est}: {r}")
    nombres = set()
    for fila in r.get("value", []):
        if fila.get("componenttype") != 1:  # 1 = tabla (AppModuleComponent)
            continue
        est2, r2, _ = ctx.dv.call("GET", f"EntityDefinitions({fila['objectid']})?$select=LogicalName")
        if est2 == 200 and r2.get("LogicalName"):
            nombres.add(r2["LogicalName"])
    return nombres


def h07(ctx):
    c = pc.Caso("H-07", "sanic_mppp_tbl_autorizacioncliente es un componente REAL de la app (no solo del sitemap)")

    # 1) La herramienta del proyecto, en modo lectura pura (nunca escribe con
    #    --solo-verificar): confirma que el sitemapxml y los componentes en
    #    vivo coinciden con el playbook.
    ruta_playbook = os.path.join(_RAIZ, "playbooks", "app", "mantenimientoppp.md")
    proc = subprocess.run(
        [sys.executable, os.path.join(_RAIZ, "herramientas", "construir", "app.py"), ruta_playbook, "--solo-verificar"],
        cwd=_RAIZ, capture_output=True, text=True, timeout=120)
    ultima = (proc.stdout.strip().splitlines() or [""])[-1]
    try:
        datos = json.loads(ultima)
    except Exception:
        c.check("app.py --solo-verificar corrió y devolvió JSON", False,
                f"rc={proc.returncode} stdout={proc.stdout[-400:]!r} stderr={proc.stderr[-400:]!r}")
        return c
    c.check("app.py --solo-verificar dice 'ya_existia' (sitemapxml y componentes en vivo == playbook)",
            datos.get("estado") == "ya_existia", str(datos))

    # 2) Independiente de la herramienta (CONVENCIONES §2): RetrieveAppComponents
    #    EN VIVO, releído por este mismo caso, buscando la tabla POR NOMBRE.
    est, r, _ = ctx.dv.call(
        "GET", "appmodules/Microsoft.Dynamics.CRM.RetrieveUnpublishedMultiple()"
               f"?$select=appmoduleid&$filter=uniquename eq '{APP_UNIQUE}'")
    filas = r.get("value", []) if est == 200 else []
    if len(filas) != 1:
        c.check("la app 'MDA - MPPP - Mantenimiento PPP' existe", False, f"HTTP {est}: {filas}")
        return c
    try:
        nombres = _tablas_de_la_app(ctx, filas[0]["appmoduleid"])
    except RuntimeError as e:
        c.check("se pudo leer RetrieveAppComponents de la app", False, str(e))
        return c
    c.check("'sanic_mppp_tbl_autorizacioncliente' aparece POR NOMBRE en RetrieveAppComponents "
            "(el import manual de la solución la agregó de verdad, no solo el sitemap)",
            TABLA_AUTORIZACIONCLIENTE in nombres, sorted(nombres))
    return c


CASOS = [h01, h02, h03, h04, h05, h06, h07]
