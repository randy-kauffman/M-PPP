#!/usr/bin/env python3
"""Motor modular de pruebas funcionales (`2026-001-referencias-planes-pago`).

    python3 herramientas/pruebas/correr.py S            # un set
    python3 herramientas/pruebas/correr.py A,B,C        # varios, en ESE orden
    python3 herramientas/pruebas/correr.py --todos      # todos los sets descubiertos, orden canónico
    python3 herramientas/pruebas/correr.py --listar     # qué sets hay y qué cubren (no toca Dev)

**Qué es esto.** Reemplaza a `corredor.py` (1315 líneas, todos los sets y la
preparación mezclados: estructura incompatible con "cada set es autocontenido",
`CONVENCIONES.md` §8). Acá el motor solo hace lo que es IGUAL para todos los
sets; cada set (`sets/set_<letra>.py`) sabe únicamente de sus propios casos:

- Toma el **semáforo** (`semaforo.py`) para toda la corrida y lo suelta al salir.
- Aplica el **canon** de §8.2 ANTES y DESPUÉS de cada set (el de antes no es
  redundante: impide heredar lo que dejó un set que murió a la mitad).
- Corre `preparar → CASOS → cerrar`, y `cerrar` corre **siempre**, pase lo que
  pase en `preparar` o en los casos (try/finally).
- Imprime **en vivo**, caso por caso, a medida que corre — no todo junto al final.
- Cierra con un resumen: cuántos OK, cuántos FALLA, cuántos N-A, y el POR QUÉ
  de cada FALLA.
- Código de salida **1** si hubo al menos un FALLA, o si algún set no se pudo
  preparar; **0** si todo salió limpio.
- **Un set que no puede prepararse NO corre sus casos**: reporta el motivo y
  sigue con el siguiente set. Cero casos corridos es un resultado honesto;
  casos verdes sobre un entorno mal preparado es una mentira.

`corredor.py` se retiró el 2026-09-27, con los 12 sets ya migrados y después de
verificar uno por uno que ninguno de sus 83 Ids quedaba huérfano. Este es el
único corredor.
"""
import argparse
import os
import sys
import traceback

_AQUI = os.path.dirname(os.path.abspath(__file__))
_RAIZ = os.path.dirname(os.path.dirname(_AQUI))
sys.path.insert(0, os.path.join(_RAIZ, "herramientas"))
sys.path.insert(0, _AQUI)

from dataverse_api import Dataverse  # noqa: E402
import semaforo  # noqa: E402
import preparacion_comun as pc  # noqa: E402
import sets as sets_pkg  # noqa: E402


def _imprimir_caso(c):
    print(f"  [{c.estado:5}] {c.id:6} {c.titulo}")
    if c.estado == "N-A":
        print(f"           motivo: {c.motivo_na}")
    elif c.estado == "FALLA":
        for nombre, ok, detalle in c.checks:
            if not ok:
                print(f"           FALLA: {nombre}" + (f"  ({detalle})" if detalle else ""))


def _correr_un_set(letra, modulo):
    """Corre un set completo: canon antes, preparar → CASOS → cerrar (cerrar
    SIEMPRE, try/finally), canon después. Devuelve
    `(letra, nombre, preparado, motivo_no_preparado, [Caso, ...], errores_cierre)`."""
    print(f"\n{'=' * 88}")
    print(f"=== Set {letra} — {modulo.NOMBRE} ===")
    print("=" * 88)

    dv = Dataverse()
    ctx = pc.Contexto(dv)

    print("  canon (antes)...")
    for cambio in pc.aplicar_canon(dv):
        print(f"    canon: {cambio}")

    preparado = True
    motivo_no_preparado = None
    resultados = []
    try:
        try:
            modulo.preparar(ctx)
        except Exception as e:
            preparado = False
            motivo_no_preparado = f"{type(e).__name__}: {e}"

        if not preparado:
            print(f"  NO SE PUDO PREPARAR: {motivo_no_preparado}")
            print("  0 casos corridos de este set (un set que no se prepara no corre sus casos).")
        else:
            for fn in modulo.CASOS:
                try:
                    c = fn(ctx)
                except Exception as e:
                    c = pc.Caso(getattr(fn, "__name__", "?"), "(el caso reventó antes de construir su resultado)")
                    c.check("el caso terminó sin excepciones", False,
                            f"{type(e).__name__}: {e}\n{traceback.format_exc()[-800:]}")
                c.cerrar()
                resultados.append(c)
                _imprimir_caso(c)
    finally:
        print("  cerrando el set (limpieza)...")
        try:
            errores_cierre = modulo.cerrar(ctx) or []
        except Exception as e:
            errores_cierre = [f"{type(e).__name__}: {e}\n{traceback.format_exc()[-500:]}"]
        for err in errores_cierre:
            print(f"    ! {err}")
        if ctx.excels_extra:
            print(f"    Excel ad hoc armados (quedan, son insumo reutilizable): "
                  f"{[os.path.basename(x) for x in ctx.excels_extra]}")

    print("  canon (después)...")
    for cambio in pc.aplicar_canon(dv):
        print(f"    canon: {cambio}")

    return letra, modulo.NOMBRE, preparado, motivo_no_preparado, resultados, errores_cierre


def _resumen(resultados_globales):
    print(f"\n{'=' * 88}")
    print("RESUMEN")
    print("=" * 88)
    total_ok = total_falla = total_na = 0
    hubo_falla_preparacion = False
    for letra, nombre, preparado, motivo, resultados, errores_cierre in resultados_globales:
        if not preparado:
            hubo_falla_preparacion = True
            print(f"  Set {letra} ({nombre}): NO SE PREPARÓ — {motivo}")
            continue
        ok = sum(1 for c in resultados if c.estado == "OK")
        falla = sum(1 for c in resultados if c.estado == "FALLA")
        na = sum(1 for c in resultados if c.estado == "N-A")
        total_ok += ok
        total_falla += falla
        total_na += na
        print(f"  Set {letra} ({nombre}): {ok} OK · {falla} FALLA · {na} N-A")
        for c in resultados:
            if c.estado == "FALLA":
                for nombre_check, ok_check, detalle in c.checks:
                    if not ok_check:
                        print(f"      {c.id}: {nombre_check}" + (f"  ({detalle})" if detalle else ""))
            elif c.estado == "N-A":
                print(f"      {c.id}: N-A — {c.motivo_na}")

    print(f"\nTOTAL: {total_ok} OK · {total_falla} FALLA · {total_na} N-A"
          + (" · HUBO SETS QUE NO SE PUDIERON PREPARAR (ver arriba)" if hubo_falla_preparacion else ""))
    print("=" * 88)
    return (total_falla > 0) or hubo_falla_preparacion


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("sets", nargs="?", help="una letra (S) o varias separadas por coma (A,B,C), en ESE orden")
    p.add_argument("--todos", action="store_true", help="corre todos los sets descubiertos, en el orden canónico")
    p.add_argument("--listar", action="store_true", help="qué sets hay y qué cubren; no toca Dev")
    args = p.parse_args()

    if args.listar:
        if not sets_pkg.SETS:
            print("(todavía no hay ningún set migrado a sets/set_*.py)")
            return 0
        for letra, nombre, cantidad in sets_pkg.listar():
            print(f"  {letra}  {nombre}  ({cantidad} casos)")
        return 0

    if args.todos:
        letras = sets_pkg.orden()
        if not letras:
            print("(todavía no hay ningún set migrado a sets/set_*.py; nada que correr)")
            return 0
    elif args.sets:
        pedidas = [s.strip().upper() for s in args.sets.split(",") if s.strip()]
        desconocidas = [l for l in pedidas if l not in sets_pkg.SETS]
        if desconocidas:
            print(f"set(s) desconocido(s): {desconocidas}. Disponibles: {sorted(sets_pkg.SETS)}", file=sys.stderr)
            return 2
        letras = pedidas  # respeta el orden que pidió quien lo corre, no el canónico
    else:
        p.print_help()
        return 2

    with semaforo.tomar(f"correr.py {','.join(letras)}"):
        resultados_globales = [_correr_un_set(letra, sets_pkg.SETS[letra]) for letra in letras]

    hubo_falla = _resumen(resultados_globales)
    return 1 if hubo_falla else 0


if __name__ == "__main__":
    sys.exit(main())
