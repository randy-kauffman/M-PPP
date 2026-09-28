#!/usr/bin/env python3
"""Versiona UN parámetro de `sanic_mppp_tbl_parametro` desde la línea de
comandos: agrega una fila nueva (nunca edita la sembrada), con la versión
calculada como el máximo sembrado en el entorno más uno — la misma regla que
ya dice `02-diccionario-datos.md` (columna `sanic_version`: "nunca se edita
la vigente") — y deja esa misma fila agregada en `playbooks/datos/parametros.md`,
que es la fuente contra la que después verifica `datos.py --solo-verificar`.

    python3 herramientas/construir/parametro.py <nombre> <valor> --tipo=Texto|Numero|JSON --descripcion="..." [--solo-verificar] [--playbook=<ruta>]

Por qué existe (D-53). La auditoría mostró 7 UPDATE a mano sobre esta tabla
el 2026-09-24: no fue malicia, fue comodidad — editar a mano en el portal es
más corto que editar el JSON de 700 líneas de `parametros.md` sin romper nada
(una coma de más, una clave duplicada, la indentación). Esta herramienta no
agrega otra advertencia: hace que el camino CORRECTO (agregar fila, nunca
editar) sea también el camino CORTO, un solo comando.

Nunca decide el `--tipo` por vos, ni siquiera copiándolo de la versión
anterior: la regla de todo `herramientas/` es que nada se deduce solo (ver
`_comun.py`). Si el tipo cambia entre versiones, se ve en el comando.

Falta a mano UNA sola cosa a propósito, porque es criterio y no mecánica:
arriba del bloque json de `parametros.md`, el párrafo que explica el POR QUÉ
del cambio (quién lo aprobó, qué reemplaza) y una línea en "Valores con
fuente" — como ya tienen los demás parámetros.

Nunca borra ni edita una fila existente, ni en el entorno ni en el playbook.
`--solo-verificar` no toca ninguno de los dos: solo calcula y muestra qué
versión se crearía.

Última línea de la salida: JSON de una línea con `estado`
(creado | difiere | bloqueado | error), `componente`, `detalle`.
"""
import json
import os
import sys

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _AQUI)
sys.path.insert(0, os.path.dirname(_AQUI))

from _comun import (  # noqa: E402
    Bloqueado,
    ErrorEntorno,
    ErrorPlaybook,
    Rastro,
    escribir_metadatos,
    exigir_forma,
    leer_entorno,
    leer_texto,
    salida,
)
from datos import literal_odata, validar_playbook  # noqa: E402 — reuso, no duplico la validación del playbook.

TABLA = "sanic_mppp_tbl_parametro"
CHOICE_TIPO = "sanic_mppp_ch_tipoparametro"
TIPOS_VALIDOS = ("Texto", "Numero", "JSON")

RAIZ = os.path.dirname(os.path.dirname(_AQUI))
PLAYBOOK_DEFECTO = os.path.join(RAIZ, "playbooks", "datos", "parametros.md")

MENSAJE_FALLO_CLIENTE = (
    "no se pudo inicializar el cliente de Dataverse (fallo de red o de credenciales); "
    "no se expone el detalle porque podría incluir datos de la solicitud de token"
)

C_TABLA = "la consulta de la definición de la tabla (GET EntityDefinitions)"
C_CHOICE = "la consulta del choice global (GET GlobalOptionSetDefinitions)"
C_FILAS = "la consulta de las filas sembradas de este parámetro"

MARCADOR_FENCE = "```json\n"
CIERRE_FILAS = "\n  ]\n}\n"


# ---------------------------------------------------------------------------
# Validación sin red
# ---------------------------------------------------------------------------
def _normalizar_nombre(crudo):
    """Mismas reglas que `Normalizacion.NombreDeParametro`
    (`src/Sanic.Mppp.Plugins/Steps/ReglasDeSteps.cs`), para fallar ANTES de
    tocar el entorno con el mismo motivo que el step de guardado daría
    después: minúsculas, solo letras/dígitos/puntos, sin punto al principio
    o al final, sin puntos seguidos. No reemplaza al step — es el mismo
    control, más rápido, offline."""
    if not crudo or not crudo.strip():
        raise ErrorPlaybook("el nombre del parámetro no puede estar vacío")
    minuscula = crudo.strip().lower()
    if minuscula[0] == "." or minuscula[-1] == ".":
        raise ErrorPlaybook("el nombre del parámetro no puede empezar ni terminar con punto")
    for i, c in enumerate(minuscula):
        es_ascii_valido = c.isascii() and (c.isdigit() or ("a" <= c <= "z") or c == ".")
        if not es_ascii_valido:
            raise ErrorPlaybook("el nombre del parámetro solo admite letras, dígitos y puntos")
        if c == "." and minuscula[i - 1] == ".":
            raise ErrorPlaybook("el nombre del parámetro no puede tener puntos seguidos")
    return minuscula


def _validar_valor(tipo, valor):
    if not valor:
        raise ErrorPlaybook("el valor no puede estar vacío")
    if tipo == "Numero":
        cuerpo = valor[1:] if valor.startswith("-") else valor
        if not cuerpo or not cuerpo.isdigit():
            raise ErrorPlaybook(f"--tipo=Numero pero el valor {valor!r} no es un entero")
    elif tipo == "JSON":
        try:
            json.loads(valor)
        except json.JSONDecodeError as e:
            raise ErrorPlaybook(f"--tipo=JSON pero el valor no es JSON válido: {e}") from e
    # Texto: cualquier cadena no vacía es válida.


def _validar_entrada(nombre_crudo, valor, tipo, descripcion):
    if tipo not in TIPOS_VALIDOS:
        raise ErrorPlaybook(f"--tipo tiene que ser uno de {TIPOS_VALIDOS}, no {tipo!r}")
    if not descripcion or not descripcion.strip():
        raise ErrorPlaybook(
            "--descripcion es obligatoria y no puede estar vacía: por qué se cambia y quién lo aprobó"
        )
    nombre = _normalizar_nombre(nombre_crudo)
    _validar_valor(tipo, valor)
    return nombre


# ---------------------------------------------------------------------------
# Contra el entorno
# ---------------------------------------------------------------------------
def _resolver_tabla(dv):
    ruta = f"EntityDefinitions(LogicalName='{TABLA}')?$select=EntitySetName,PrimaryIdAttribute"
    cuerpo = leer_entorno(dv, ruta, C_TABLA, admite_404=True)
    if cuerpo is None:
        raise Bloqueado(f"la tabla '{TABLA}' no existe en el entorno")
    conjunto = exigir_forma(cuerpo.get("EntitySetName"), str, C_TABLA, "EntitySetName", no_vacio=True)
    primaria = exigir_forma(cuerpo.get("PrimaryIdAttribute"), str, C_TABLA, "PrimaryIdAttribute", no_vacio=True)
    return conjunto, primaria


def _version_maxima_sembrada(dv, conjunto, primaria, nombre):
    ruta = f"{conjunto}?$select={primaria},sanic_version&$filter=sanic_nombre eq {literal_odata(nombre)}"
    cuerpo = leer_entorno(dv, ruta, C_FILAS)
    filas = exigir_forma(cuerpo.get("value"), [dict], C_FILAS, "value")
    versiones = [exigir_forma(f.get("sanic_version"), int, C_FILAS, "sanic_version") for f in filas]
    return (max(versiones) if versiones else 0), len(filas)


def _resolver_choice_tipo(dv, tipo):
    ruta = f"GlobalOptionSetDefinitions(Name='{CHOICE_TIPO}')"
    cuerpo = leer_entorno(dv, ruta, C_CHOICE, admite_404=True)
    if cuerpo is None:
        raise Bloqueado(f"el choice global '{CHOICE_TIPO}' no existe en el entorno")
    por_etiqueta = {}
    for opcion in exigir_forma(cuerpo.get("Options"), [dict], C_CHOICE, "Options", no_vacio=True):
        etiquetas = exigir_forma(opcion.get("Label"), dict, C_CHOICE, "Options.Label")
        for loc in exigir_forma(etiquetas.get("LocalizedLabels"), [dict], C_CHOICE, "LocalizedLabels"):
            texto = exigir_forma(loc.get("Label"), str, C_CHOICE, "LocalizedLabels.Label")
            por_etiqueta[texto] = exigir_forma(opcion.get("Value"), int, C_CHOICE, "Options.Value")
    if tipo not in por_etiqueta:
        raise Bloqueado(f"el choice '{CHOICE_TIPO}' no tiene la opción {tipo!r}; las que tiene son {sorted(por_etiqueta)}")
    return por_etiqueta[tipo]


# ---------------------------------------------------------------------------
# El playbook: agregar fila, nunca editar
# ---------------------------------------------------------------------------
def _agregar_al_playbook(ruta_playbook, nombre, version, tipo, valor, descripcion):
    """Inserta la fila nueva justo antes del cierre de `filas`, sin tocar ni
    reordenar ninguna fila existente: se edita el archivo por texto, no se
    re-serializa el JSON entero (el archivo mezcla formato escapado y sin
    escapar entre filas viejas y nuevas — re-serializarlo todo ensuciaría el
    diff de filas que nadie tocó)."""
    texto = leer_texto(ruta_playbook)
    partes = texto.split(MARCADOR_FENCE)
    if len(partes) != 3:
        raise ErrorPlaybook(
            f"se esperaban exactamente 2 bloques ```json``` en '{ruta_playbook}' (Identidad y Qué se crea); "
            f"hay {len(partes) - 1}"
        )
    resto_bloque2 = partes[2]
    if "```" not in resto_bloque2:
        raise ErrorPlaybook(f"el bloque 'Qué se crea' de '{ruta_playbook}' no cierra con ```")
    bloque2, cola = resto_bloque2.split("```", 1)

    try:
        datos = json.loads(bloque2)
    except json.JSONDecodeError as e:
        raise ErrorPlaybook(f"el bloque 'Qué se crea' de '{ruta_playbook}' no es JSON válido: {e}") from e
    validar_playbook(datos)

    for f in datos["filas"]:
        if f.get("sanic_nombre") == nombre and f.get("sanic_version") == version:
            raise ErrorPlaybook(f"el playbook ya tiene una fila sanic_nombre={nombre!r} sanic_version={version}")

    if not bloque2.endswith(CIERRE_FILAS):
        raise ErrorPlaybook(
            "el bloque 'Qué se crea' no termina con el cierre esperado de 'filas' ('  ]' + '}'); "
            "no se edita a ciegas — agregá la fila a mano esta vez"
        )
    cuerpo_sin_cierre = bloque2[: -len(CIERRE_FILAS)]
    if not cuerpo_sin_cierre.endswith("}"):
        raise ErrorPlaybook("no se encontró el cierre de la fila anterior justo antes de 'filas'; no se edita a ciegas")

    fila_nueva = {
        "sanic_nombre": nombre,
        "sanic_version": version,
        "sanic_tipo": {"choice": CHOICE_TIPO, "etiqueta": tipo},
        "sanic_valor": valor,
        "sanic_descripcion": descripcion,
    }
    texto_fila = json.dumps(fila_nueva, indent=2, ensure_ascii=False)
    texto_fila_indentado = "\n".join(("    " + linea if linea else linea) for linea in texto_fila.split("\n"))

    bloque2_nuevo = cuerpo_sin_cierre + ",\n" + texto_fila_indentado + CIERRE_FILAS

    # Última red: el bloque reconstruido tiene que seguir siendo JSON válido
    # y traer exactamente una fila más que antes.
    datos_nuevo = json.loads(bloque2_nuevo)
    validar_playbook(datos_nuevo)
    if len(datos_nuevo["filas"]) != len(datos["filas"]) + 1:
        raise ErrorPlaybook("la reconstrucción del bloque no dio exactamente una fila más; no se escribe nada")

    texto_nuevo = partes[0] + MARCADOR_FENCE + partes[1] + MARCADOR_FENCE + bloque2_nuevo + "```" + cola
    with open(ruta_playbook, "w", encoding="utf-8") as f:
        f.write(texto_nuevo)


# ---------------------------------------------------------------------------
def _contra_entorno(dv, nombre, valor, tipo, descripcion, ruta_playbook, solo_verificar, componente, dormir):
    conjunto, primaria = _resolver_tabla(dv)
    version_maxima, existentes = _version_maxima_sembrada(dv, conjunto, primaria, nombre)
    nueva_version = version_maxima + 1

    if solo_verificar:
        detalle = (
            f"se crearía sanic_nombre={nombre!r} sanic_version={nueva_version} "
            f"(máximo sembrado hoy: {version_maxima}, {existentes} versión(es) existentes); "
            "no se tocó el entorno ni el playbook"
        )
        return "difiere", componente, detalle

    numero_tipo = _resolver_choice_tipo(dv, tipo)

    cuerpo_post = {
        "sanic_nombre": nombre,
        "sanic_version": nueva_version,
        "sanic_tipo": numero_tipo,
        "sanic_valor": valor,
        "sanic_descripcion": descripcion,
    }
    est, resp, _ = escribir_metadatos(dv, "POST", conjunto, cuerpo_post, dormir=dormir)
    if est not in (200, 201, 204):
        # Un 400 acá suele ser el step de guardado ('NormalizarYValidarStep' u
        # otro) rechazando el valor: el mensaje del plugin es lo que hay que leer.
        return "error", componente, f"el alta devolvió HTTP {est} (se esperaba 204): {resp}"

    ruta_verif = (
        f"{conjunto}?$select={primaria},sanic_valor,sanic_tipo,sanic_descripcion"
        f"&$filter=sanic_nombre eq {literal_odata(nombre)} and sanic_version eq {nueva_version}"
    )
    cuerpo_verif = leer_entorno(dv, ruta_verif, C_FILAS)
    creadas = exigir_forma(cuerpo_verif.get("value"), [dict], C_FILAS, "value")
    if len(creadas) != 1:
        return "error", componente, f"se creó pero al releer aparecieron {len(creadas)} filas con esa clave (se esperaba 1)"
    fila_creada = creadas[0]
    if fila_creada.get("sanic_valor") != valor or fila_creada.get("sanic_tipo") != numero_tipo:
        return "error", componente, f"se creó pero no quedó como se pidió: {fila_creada}"

    # El entorno ya tiene la fila confirmada: recién ahora se toca el
    # playbook. Si esto falla, el entorno queda bien y se avisa qué falta.
    try:
        _agregar_al_playbook(ruta_playbook, nombre, nueva_version, tipo, valor, descripcion)
    except (OSError, ErrorPlaybook) as e:
        return (
            "error", componente,
            f"se creó en el entorno (sanic_version={nueva_version}) pero no se pudo agregar a "
            f"'{ruta_playbook}': {e}. Agregala a mano antes de terminar, o el playbook queda mintiendo."
        )

    detalle = (
        f"creado sanic_nombre={nombre!r} sanic_version={nueva_version} (máximo anterior: {version_maxima}); "
        f"agregado a '{ruta_playbook}'. Falta a mano: el párrafo que explica el PORQUÉ arriba del bloque json, "
        "y una línea en 'Valores con fuente'."
    )
    return "creado", componente, detalle


def construir(nombre_crudo, valor, tipo, descripcion, ruta_playbook, solo_verificar, fabrica_cliente, dormir=None):
    """Nunca lanza: siempre devuelve `(estado, componente, detalle)`."""
    if dormir is None:
        import time

        dormir = time.sleep
    componente = f"{TABLA}:{nombre_crudo}"
    try:
        nombre = _validar_entrada(nombre_crudo, valor, tipo, descripcion)
        componente = f"{TABLA}:{nombre}"
    except ErrorPlaybook as e:
        return "error", componente, str(e)

    try:
        dv = fabrica_cliente()
    except Exception:
        return "error", componente, MENSAJE_FALLO_CLIENTE

    rastro = Rastro(dv)
    try:
        return _contra_entorno(rastro, nombre, valor, tipo, descripcion, ruta_playbook, solo_verificar, componente, dormir)
    except Bloqueado as e:
        return "bloqueado", componente, str(e)
    except ErrorEntorno as e:
        return "error", componente, str(e)
    except Exception as e:
        return "error", componente, f"fallo inesperado hablando con Dataverse, durante {rastro.en_curso}: {type(e).__name__}: {e}"


def main():
    argv = sys.argv[1:]
    posicionales = [a for a in argv if not a.startswith("--")]
    banderas = [a for a in argv if a.startswith("--")]
    solo_verificar = "--solo-verificar" in banderas
    tipo = next((a.split("=", 1)[1] for a in banderas if a.startswith("--tipo=")), None)
    descripcion = next((a.split("=", 1)[1] for a in banderas if a.startswith("--descripcion=")), None)
    ruta_playbook = next((a.split("=", 1)[1] for a in banderas if a.startswith("--playbook=")), PLAYBOOK_DEFECTO)
    conocidas = {b for b in banderas if b == "--solo-verificar" or b.split("=", 1)[0] in ("--tipo", "--descripcion", "--playbook")}
    desconocidas = sorted(set(banderas) - conocidas)

    if len(posicionales) != 2 or tipo is None or descripcion is None or desconocidas:
        return salida(
            "error", "desconocido",
            "uso incorrecto: parametro.py <nombre> <valor> --tipo=Texto|Numero|JSON --descripcion=\"...\" "
            "[--solo-verificar] [--playbook=<ruta>]"
            + (f"; banderas desconocidas: {desconocidas}" if desconocidas else ""),
        )
    from dataverse_api import Dataverse

    nombre_crudo, valor = posicionales
    estado, componente, detalle = construir(nombre_crudo, valor, tipo, descripcion, ruta_playbook, solo_verificar, Dataverse)
    return salida(estado, componente, detalle)


if __name__ == "__main__":
    sys.exit(main())
