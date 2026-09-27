#!/usr/bin/env python3
"""La marca de agua de los datos de prueba: única fuente de verdad del
prefijo reservado y del borrado seguro que lo exige.

Por qué existe. Un `preparar_datos.py --limpiar` borró alguna vez el fixture
COMPARTIDO completo, incluidas autorizaciones reales del aprobador que hubo
que restaurar a mano. La causa de fondo: el limpiador borraba por NOMBRE
parecido ("contiene tal código"), nunca verificó un registro por registro
antes del DELETE que fuera, sin ninguna duda, algo que el arnés había creado.

La regla, para cualquier herramienta que cree o borre datos de prueba:

1. Todo lo que crea el arnés nace con el prefijo `ZZP-` (ZZ Prueba) al
   arranque de su campo identificador (`sanic_nombre` u otro campo de texto
   visible — NUNCA un código corto de negocio de 3-4 caracteres, que no
   tiene lugar para el prefijo completo: ver el aviso al final de este
   archivo).
2. El limpiador SOLO puede borrar una fila si, releída justo antes del
   DELETE, ese campo lleva el prefijo. Si no lo lleva, no se toca —ni con
   una bandera de forzado: no existe tal bandera acá, a propósito.
3. La verificación es POR REGISTRO e INMEDIATAMENTE ANTES de cada DELETE,
   nunca contra una lista cacheada de antes: el registro pudo cambiar de
   nombre (a mano, por otro proceso) entre que se armó la lista de borrado
   y el momento del borrado.

Como librería:
    from marca_agua import PREFIJO, marcado, nombre_de_prueba, borrar_marcado

    nombre = nombre_de_prueba("Pruebas MPPP SA")   # -> "ZZP-Pruebas MPPP SA"
    ok, motivo = borrar_marcado(dv, "sanic_mppp_tbl_clientes", id_cliente, "sanic_nombre")
    if not ok:
        print("no se borró:", motivo)
"""

# Prefijo reservado. Cambiarlo acá alcanza para todo lo que use este módulo:
# no lo dupliques como literal en otro archivo.
PREFIJO = "ZZP-"


def marcado(valor):
    """True si `valor` (típicamente `sanic_nombre` u otro campo de texto)
    lleva el prefijo reservado. Nunca hace `in`, siempre `startswith`: la
    marca tiene que estar al principio, no en cualquier lado del texto."""
    return isinstance(valor, str) and valor.startswith(PREFIJO)


def nombre_de_prueba(resto):
    """Arma un nombre marcado, para que quien crea datos de prueba no tenga
    que concatenar el prefijo a mano y arriesgarse a un typo."""
    return f"{PREFIJO}{resto}"


def borrar_marcado(dv, conjunto, id_registro, campo="sanic_nombre", valor_conocido=None):
    """Borra `conjunto(id_registro)` SOLO si `campo` lleva el prefijo reservado,
    releído justo antes del DELETE (salvo que ya se lo pases en `valor_conocido`
    porque lo acabás de leer vos mismo en la misma pasada, sin ventana de tiempo
    en el medio).

    Devuelve `(borrado: bool, motivo: str)`. Nunca lanza por un registro sin
    marca: eso es el comportamiento esperado (no tocarlo), no un error del
    programa. Sí puede lanzar si Dataverse no contesta lo que se espera.

    A propósito NO tiene parámetro `forzar`. Un registro sin la marca no se
    borra nunca desde acá — si hace falta borrar algo así, es una operación
    manual y consciente, no un flag de este módulo."""
    if valor_conocido is None:
        estado, cuerpo, _ = dv.call("GET", f"{conjunto}({id_registro})?$select={campo}")
        if estado != 200:
            return False, f"no se pudo releer el registro antes de borrar (HTTP {estado})"
        valor_conocido = cuerpo.get(campo)

    if not marcado(valor_conocido):
        return False, f"{campo}={valor_conocido!r} no lleva el prefijo {PREFIJO!r}: NO se borra"

    estado, cuerpo, _ = dv.call("DELETE", f"{conjunto}({id_registro})")
    if estado not in (200, 204):
        return False, f"DELETE devolvió HTTP {estado}: {cuerpo}"
    return True, f"borrado ({campo}={valor_conocido!r})"


# --- Aviso para quien migre `preparar_datos.py` a esta convención ---------
#
# `preparar_datos.py` usa hoy el prefijo "PR" y códigos de plan de 4
# caracteres exactos (`PR11`, `PR06`, ...): ahí NO entra el prefijo completo
# "ZZP-" (4 caracteres, sin espacio para nada más). La marca de agua tiene
# que ir en un campo con lugar de sobra — `sanic_nombre` (200 caracteres) es
# el candidato natural, ya lo usan `CLIENTE` y el nombre de cada plan/
# autorización. El código corto de negocio (`sanic_codigo`) puede seguir
# como está: identifica el caso de prueba, no es lo que el limpiador verifica.
