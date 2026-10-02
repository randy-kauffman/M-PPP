#!/usr/bin/env python3
"""Arma `pruebas/PLAN-COMPLETO.md` concatenando `pruebas/README.md` + los 13
archivos numerados de `pruebas/` (`00-preparacion.md` a `12-catalogos-masivos.md`),
para leerlos de corrido sin abrir uno por uno.

    python3 herramientas/pruebas/armar_plan_completo.py

**`PLAN-COMPLETO.md` NO se edita a mano** (lo dice también en su propio
encabezado): se edita el archivo separado que corresponda y se vuelve a
correr este script. Si en algún momento se agrega o se saca un archivo de
`pruebas/`, actualizá la lista `ARCHIVOS` de acá abajo — es la única fuente
de verdad del orden y de qué entra.

**Regla de niveles de encabezado** (para que el documento junto tenga un
único índice coherente: H1 el documento, H2 cada sección, H3 cada caso):

- El cuerpo de `README.md` entra CASI tal cual: se le saca su propio `#`
  (título) y se lo reemplaza por el encabezado fijo `## Índice y cómo
  usarlo`; el resto de sus encabezados (`## Cómo está organizado`, etc.) NO
  se tocan — quedan como secciones hermanas de "Índice y cómo usarlo".
- Cada uno de los 13 archivos numerados entra con TODOS sus encabezados
  corridos un nivel (`#` → `##`, `##` → `###`, `###` → `####`, ...), así su
  título (`# 00 — Preparación`) pasa a ser la sección (`## 00 — ...`) y sus
  casos (`## P-01 · ...`) pasan a ser subsecciones (`### P-01 · ...`).
- Los `#` que aparecen DENTRO de un bloque ```` ``` ```` (comentarios de shell
  o de Python en los ejemplos) nunca se tocan: el script seguí el estado de
  "adentro/afuera de un bloque de código" línea por línea antes de decidir
  si una línea es un encabezado.
"""
import datetime
import os
import re

_AQUI = os.path.dirname(os.path.abspath(__file__))
_RAIZ = os.path.dirname(os.path.dirname(_AQUI))
PRUEBAS = os.path.join(_RAIZ, "pruebas")

README = os.path.join(PRUEBAS, "README.md")
SALIDA = os.path.join(PRUEBAS, "PLAN-COMPLETO.md")

# Orden canónico de los archivos de casos (`CONVENCIONES.md` §4): el mismo
# que usa `herramientas/pruebas/sets/__init__.py` para C/S/R/M/A/G/V/I/B/U/E/L,
# pero acá por nombre de archivo, no por letra (00-preparacion.md no tiene
# letra: el set P desapareció, pero el archivo sigue siendo parte del plan).
#
# El 2026-09-28 (`CONVENCIONES.md` §4, "La regla del nombre") se sacaron
# `12-obligatoriedad-gestion.md` y `13-construido-hoy.md`: sus casos se
# fusionaron dentro de `03-reglas-de-registro.md` (R-19..R-28),
# `05-app-y-comandos.md` (A-15) y `06-seguridad-vistas-formularios.md`
# (G-14..G-19) — ver la tabla de equivalencias en `pruebas/README.md`.
ARCHIVOS = [
    "00-preparacion.md",
    "01-clasificacion-del-correo.md",
    "02-reglas-de-solicitud.md",
    "03-reglas-de-registro.md",
    "04-comunicaciones.md",
    "05-app-y-comandos.md",
    "06-seguridad-vistas-formularios.md",
    "07-vigilancia.md",
    "08-integridad-y-plugins.md",
    "09-rpa-fase2.md",
    "10-autorizacion-cliente.md",
    "11-exportacion-excel.md",
    "12-catalogos-masivos.md",
]

_ENCABEZADO = re.compile(r"^(#{1,6})(\s.*)?$")
_FENCE = re.compile(r"^\s*```")


def _demoter_encabezados(texto):
    """Corre un nivel TODOS los encabezados ATX del texto, respetando los
    bloques de código (```...```): un `#` que abre un comentario de shell
    DENTRO de un bloque nunca se toca."""
    salida = []
    en_bloque = False
    for linea in texto.splitlines():
        if _FENCE.match(linea):
            en_bloque = not en_bloque
            salida.append(linea)
            continue
        m = _ENCABEZADO.match(linea)
        if not en_bloque and m:
            salida.append("#" + linea)
        else:
            salida.append(linea)
    return "\n".join(salida)


def _leer(ruta):
    with open(ruta, encoding="utf-8") as f:
        return f.read()


def _sin_primera_linea(texto):
    """Saca la primera línea (el título `# ...`) y el salto de línea en
    blanco que típicamente la sigue."""
    lineas = texto.splitlines()
    resto = lineas[1:]
    while resto and not resto[0].strip():
        resto = resto[1:]
    return "\n".join(resto)


def armar():
    hoy = datetime.date.today().isoformat()
    partes = [
        "# Plan de pruebas funcionales — MPPP",
        f"Proyecto `2026-001-referencias-planes-pago` · Entorno **Dev** · "
        f"generado el {hoy} por `herramientas/pruebas/armar_plan_completo.py`",
        "",
        "Este documento junta `README.md` y los 13 archivos de `pruebas/` en uno solo, para",
        "leerlo de corrido. Los archivos separados (y los Excel) están en el repositorio, en",
        "`pruebas/`. **No lo edites a mano**: si editás algo, editalo en el archivo separado y",
        "volvé a correr `python3 herramientas/pruebas/armar_plan_completo.py`.",
        "",
        "---",
        "## Índice y cómo usarlo",
        "",
        _sin_primera_linea(_leer(README)),
    ]

    for nombre in ARCHIVOS:
        ruta = os.path.join(PRUEBAS, nombre)
        partes.append("")
        partes.append("---")
        partes.append(_demoter_encabezados(_leer(ruta)).rstrip("\n"))

    contenido = "\n".join(partes).rstrip("\n") + "\n"
    with open(SALIDA, "w", encoding="utf-8") as f:
        f.write(contenido)
    print(f"escrito {SALIDA} ({len(contenido.splitlines())} líneas, {len(ARCHIVOS)} archivos + README)")


if __name__ == "__main__":
    armar()
