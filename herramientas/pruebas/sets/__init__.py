"""Descubre los sets de pruebas (`set_*.py` en este directorio) y expone:

- `SETS` — dict `{letra: módulo}`.
- `orden(letras=None)` — en qué orden corren (el canónico de
  `CONVENCIONES.md` §4 primero; una letra nueva que ese orden todavía no
  conozca corre al final, alfabética — nunca desaparece en silencio).
- `listar()` — `[(letra, nombre, cantidad_de_casos)]`, para `--listar`.

Contrato que tiene que cumplir cada `set_<letra>.py` (fijado en
`CONVENCIONES.md` §8 y en la Parte 1 del pedido que armó este motor):

    LETRA  = "S"                  # una letra, sin colisión con otro set
    NOMBRE = "Reglas de Solicitud"
    def preparar(ctx): ...        # deja listo TODO: datos, roles, canon, sesiones
    CASOS  = [s01, s02, ...]      # cada uno recibe ctx y devuelve un preparacion_comun.Caso
    def cerrar(ctx): ...          # borra lo ZZP-/lo que este set creó; corre SIEMPRE (try/finally en correr.py)

Un módulo que no cumple el contrato hace fallar el descubrimiento con un
`ImportError` explícito — mejor eso que un set a medias corriendo en silencio.
"""
import importlib
import os
import pkgutil

_AQUI = os.path.dirname(os.path.abspath(__file__))

# Orden canónico de CONVENCIONES.md §4. El set P desapareció (sus
# verificaciones se repartieron a `preparacion_comun.py`), pero la letra
# queda documentada acá por prolijidad histórica: nunca va a haber un
# `set_p.py`.
ORDEN_CANONICO = ["C", "S", "R", "M", "A", "G", "V", "I", "B", "U", "E", "O", "H"]

CAMPOS_OBLIGATORIOS = ("LETRA", "NOMBRE", "preparar", "CASOS", "cerrar")


def _validar(modulo):
    faltan = [c for c in CAMPOS_OBLIGATORIOS if not hasattr(modulo, c)]
    if faltan:
        raise ImportError(f"{modulo.__name__} no cumple el contrato de set: le falta {faltan}")
    if not isinstance(modulo.LETRA, str) or len(modulo.LETRA) != 1:
        raise ImportError(f"{modulo.__name__}.LETRA tiene que ser una sola letra, es {modulo.LETRA!r}")


def _descubrir():
    sets = {}
    for _, nombre_mod, _ in pkgutil.iter_modules([_AQUI]):
        if not nombre_mod.startswith("set_"):
            continue
        modulo = importlib.import_module(f"{__name__}.{nombre_mod}")
        _validar(modulo)
        letra = modulo.LETRA
        if letra in sets:
            raise ImportError(
                f"letra {letra!r} duplicada entre {sets[letra].__name__} y {modulo.__name__}: "
                "CONVENCIONES §4 exige una letra por archivo, sin colisiones")
        sets[letra] = modulo
    return sets


SETS = _descubrir()


def orden(letras=None):
    """Los módulos a correr, en el orden en que tienen que correr. `letras`
    filtra (si se pasa `None`, son todos los descubiertos)."""
    disponibles = set(letras) if letras is not None else set(SETS)
    conocidas = [l for l in ORDEN_CANONICO if l in disponibles and l in SETS]
    resto = sorted(l for l in disponibles if l in SETS and l not in ORDEN_CANONICO)
    return conocidas + resto


def listar():
    """`[(letra, nombre, cantidad_de_casos)]`, en orden canónico."""
    return [(l, SETS[l].NOMBRE, len(SETS[l].CASOS)) for l in orden()]
