#!/usr/bin/env python3
"""Motor modular de pruebas funcionales (`2026-001-referencias-planes-pago`).

    python3 herramientas/pruebas/correr.py S            # un set
    python3 herramientas/pruebas/correr.py A,B,C        # varios, en ESE orden
    python3 herramientas/pruebas/correr.py --todos      # todos los sets descubiertos, orden canónico
    python3 herramientas/pruebas/correr.py --listar     # qué sets hay y qué cubren (no toca Dev)
    python3 herramientas/pruebas/correr.py V --full     # V, PRENDIENDO todo lo que hoy queda afuera
    python3 herramientas/pruebas/correr.py --todos --full   # todo, con TODO prendido (ver más abajo)

## `--full` — un solo interruptor para "corré todas, todas sin importar nada"

Decisión del aprobador (2026-09-28): un solo parámetro que prenda todo lo que hoy queda apagado
detrás de banderas de entorno sueltas (`MPPP_V_LENTOS`, `MPPP_E08_ALTO_RIESGO`) por costo (tiempo
real) o por riesgo (degradar el sandbox compartido de Dev). `--full` es la forma RECOMENDADA de
prenderlas: hace lo mismo que ponerlas todas a mano, así que **las banderas sueltas siguen
funcionando igual que siempre** — quien ya las usa no se rompe.

Antes de tocar Dev, `--full` imprime un banner que dice, en concreto, qué casos extra va a correr,
cuánto va a tardar de más y qué riesgos asume — nadie se tiene que enterar de eso cuarenta minutos
después (ver `_banner_full()`).

**`B-01` (set B) sigue `N-A` SIEMPRE, incluso con `--full`.** No es un olvido: `B-01` renombra el
rol `SR - MPPP - RPA` y rompe la puerta del bot para TODO el entorno mientras dura. El aprobador lo
confirmó explícitamente excluido (`pruebas/09-rpa-fase2.md`); ya está cubierto por 8 pruebas
unitarias (7 de rechazo). `--full` no tiene ningún mecanismo para prenderlo — no existe la bandera.

**`V-02` es la única excepción autorizada, en todo el arnés, a "el arnés nunca desactiva un
flujo"** (`CONVENCIONES.md`, sección "Excepción V-02"): con `--full` (o `MPPP_V02_ALTO_RIESGO=si`
sola), el set V apaga `MPPP-REC` de verdad, corre la prueba, y lo vuelve a encender — con dos redes
de seguridad. Ver el docstring de `sets/set_v.py`.

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

# Nombres de bandera EXACTOS que cada set consulta con `os.environ.get(...)` (documentados en su
# propio docstring: `sets/set_v.py` para las dos primeras, `sets/set_e.py` para la tercera). Viven
# acá, no importados de los sets, para que `--full` no tenga que cargar ningún módulo de set para
# saber qué prender — son el contrato público entre el motor y los sets, nada más.
BANDERA_V_LENTOS = "MPPP_V_LENTOS"
BANDERA_V02 = "MPPP_V02_ALTO_RIESGO"
BANDERA_E08 = "MPPP_E08_ALTO_RIESGO"


def _banner_full(letras):
    """Grita ANTES de arrancar (regla del aprobador: "nadie tiene que enterarse cuarenta minutos
    después"). Lista, por cada set que se va a correr, sus casos `CASOS_FULL` (atributo OPCIONAL
    de un módulo de set: `{"X-NN": "motivo, con el costo/riesgo concreto"}` — si un set no lo
    define, `--full` no le agrega nada, sin que haga falta tocarlo). Si `B` está entre los sets
    pedidos, recuerda la excepción de `B-01` (sigue N-A siempre, a propósito)."""
    print("\n" + "#" * 88)
    print("###  MODO FULL — vas a prender todo lo que hoy queda afuera por costo o por riesgo  ###")
    print("#" * 88)
    hubo_extra = False
    for letra in letras:
        casos_full = getattr(sets_pkg.SETS[letra], "CASOS_FULL", {})
        if not casos_full:
            continue
        hubo_extra = True
        print(f"\n  Set {letra} ({sets_pkg.SETS[letra].NOMBRE}):")
        for id_caso, motivo in casos_full.items():
            print(f"    + {id_caso}: {motivo}")
    if not hubo_extra:
        print("\n  (Ninguno de los sets que vas a correr tiene casos extra detrás de --full: no cambia nada acá.)")
    if "B" in letras:
        print(
            "\n  Set B (RPA): B-01 sigue N-A A PROPÓSITO, incluso con --full. Renombra el rol "
            "'SR - MPPP - RPA' y rompe la puerta del bot para TODO el entorno mientras dura. "
            "Decisión explícita del aprobador (pruebas/09-rpa-fase2.md); ya cubierto por 8 pruebas "
            "unitarias (7 de rechazo). No es un olvido: --full no tiene ninguna forma de prenderlo.")
    print("\n" + "#" * 88 + "\n")


def _listar():
    if not sets_pkg.SETS:
        print("(todavía no hay ningún set migrado a sets/set_*.py)")
        return 0
    for letra, nombre, cantidad in sets_pkg.listar():
        casos_full = getattr(sets_pkg.SETS[letra], "CASOS_FULL", {})
        print(f"  {letra}  {nombre}  ({cantidad} casos)")
        for id_caso, motivo in casos_full.items():
            print(f"        con --full: {id_caso} — {motivo}")
    return 0


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

        # Avisos que `preparar()` necesita que se vean YA, no en el resumen final (p.ej. RED 2 de
        # la excepción V-02 en `set_v.py`: si tuvo que reencender MPPP-REC al arrancar). Se
        # imprimen se haya preparado bien o no — un aviso de "tuve que arreglar algo" importa
        # incluso si después el set no pudo terminar de prepararse.
        for aviso in ctx.avisos:
            print(f"  AVISO: {aviso}")

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
    hubo_error_cierre = False
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
        # Un error en `cerrar()` (la limpieza) NO es cosmético: puede significar, por ejemplo, que
        # la excepción V-02 dejó MPPP-REC sin reencender. Antes esto solo se imprimía en vivo
        # ("! ..." al cerrar el set) y se perdía del resumen y del código de salida — exactamente
        # el "log perdido" que la excepción V-02 no se puede permitir. Ahora cuenta.
        if errores_cierre:
            hubo_error_cierre = True
            print(f"      ERROR(ES) EN EL CIERRE de {letra} (ver '! ...' más arriba en la corrida):")
            for err in errores_cierre:
                print(f"        ! {err}")

    print(f"\nTOTAL: {total_ok} OK · {total_falla} FALLA · {total_na} N-A"
          + (" · HUBO SETS QUE NO SE PUDIERON PREPARAR (ver arriba)" if hubo_falla_preparacion else "")
          + (" · HUBO ERRORES EN EL CIERRE DE ALGÚN SET (ver arriba)" if hubo_error_cierre else ""))
    print("=" * 88)
    return (total_falla > 0) or hubo_falla_preparacion or hubo_error_cierre


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("sets", nargs="?", help="una letra (S) o varias separadas por coma (A,B,C), en ESE orden")
    p.add_argument("--todos", action="store_true", help="corre todos los sets descubiertos, en el orden canónico")
    p.add_argument("--listar", action="store_true", help="qué sets hay y qué cubren (normal y con --full); no toca Dev")
    p.add_argument(
        "--full", action="store_true",
        help="prende TODO lo que hoy queda afuera por costo o por riesgo (equivale a poner "
             f"{BANDERA_V_LENTOS}=si + {BANDERA_V02}=si + {BANDERA_E08}=si a la vez); grita un "
             "banner antes de arrancar; B-01 (set B) sigue N-A siempre, a propósito")
    args = p.parse_args()

    if args.listar:
        return _listar()

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

    if args.full:
        _banner_full(letras)
        # Regla 1 de `--full`: reemplaza a las banderas sueltas COMO FORMA RECOMENDADA, pero no
        # las rompe — esto es exactamente lo mismo que si quien corre esto hubiera puesto las tres
        # variables a mano antes de invocar. Si alguna ya estaba puesta (p.ej. alguien encadenó
        # `MPPP_V_LENTOS=si correr.py V --full`), queda en "si" igual: no hay conflicto posible.
        os.environ[BANDERA_V_LENTOS] = "si"
        os.environ[BANDERA_V02] = "si"
        os.environ[BANDERA_E08] = "si"

    with semaforo.tomar(f"correr.py {','.join(letras)}"):
        resultados_globales = [_correr_un_set(letra, sets_pkg.SETS[letra]) for letra in letras]

    hubo_falla = _resumen(resultados_globales)
    return 1 if hubo_falla else 0


if __name__ == "__main__":
    sys.exit(main())
