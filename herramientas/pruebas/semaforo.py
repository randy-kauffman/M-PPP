#!/usr/bin/env python3
"""Semáforo: evita que el arnés automático pise una prueba manual del aprobador
(o a otro arnés corriendo en simultáneo) sobre el mismo entorno compartido.

Es un archivo de bloqueo en `local/` — nunca en el repo, nunca compartido entre
máquinas: `/local/` ya está en `.gitignore` completo. El arnés lo toma al
arrancar y lo suelta al terminar (pase lo que pase adentro, con `tomar()` como
context manager). Si el arnés muere sin soltarlo (Ctrl+C, corte de luz, panic),
el semáforo se libera SOLO la próxima vez que alguien intente tomarlo: guarda
el PID de quien lo tiene y, si ese proceso ya no existe, no hace falta borrar
nada a mano.

Un bloqueo MANUAL del aprobador es distinto a propósito: no tiene PID de
ningún arnés (`"pid": null`), así que la comprobación de "¿sigue vivo?" nunca
aplica y el semáforo NUNCA se libera solo. Se libera con `--liberar`, a mano,
que es lo que corresponde cuando quien lo puso en rojo fue una persona y no
un proceso.

Como comando:
    python3 herramientas/pruebas/semaforo.py --estado
    python3 herramientas/pruebas/semaforo.py --bloquear "probando a mano hasta las 18"
    python3 herramientas/pruebas/semaforo.py --liberar

Como librería (el arnés):
    from semaforo import tomar
    with tomar("corredor.py"):
        ...   # acá el semáforo es tuyo; se suelta solo al salir del bloque
"""
import contextlib
import datetime
import json
import os
import sys

_AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(os.path.dirname(_AQUI))
RUTA = os.path.join(RAIZ, "local", "semaforo-pruebas.json")


class SemaforoOcupado(SystemExit):
    """Se lanza (como SystemExit: el mensaje ya es lo bastante claro para
    la consola) cuando el semáforo está en rojo al querer tomarlo."""


def _leer():
    if not os.path.exists(RUTA):
        return None
    try:
        with open(RUTA, encoding="utf-8") as f:
            return json.load(f)
    except (json.JSONDecodeError, OSError):
        return None


def _vivo(pid):
    """Bloqueo manual (`pid=None`) se considera siempre vivo: solo lo apaga
    una persona con `--liberar`. Un PID de arnés se chequea de verdad."""
    if pid is None:
        return True
    try:
        os.kill(pid, 0)
    except ProcessLookupError:
        return False
    except PermissionError:
        return True  # existe, es de otro usuario: no asumimos que está muerto
    return True


def _escribir(pid, motivo):
    os.makedirs(os.path.dirname(RUTA), exist_ok=True)
    with open(RUTA, "w", encoding="utf-8") as f:
        json.dump({
            "pid": pid,
            "motivo": motivo,
            "cuando": datetime.datetime.now().isoformat(timespec="seconds"),
        }, f)


def estado():
    """None si está libre. Si hay un archivo de un arnés muerto, lo limpia
    solo y devuelve None también. Si está ocupado de verdad, el dict."""
    actual = _leer()
    if actual is None:
        return None
    if not _vivo(actual.get("pid")):
        try:
            os.remove(RUTA)
        except OSError:
            pass
        return None
    return actual


@contextlib.contextmanager
def tomar(motivo):
    """Adquiere el semáforo para el proceso actual o revienta con
    `SemaforoOcupado` y un mensaje claro. Lo suelta solo al salir del `with`,
    haya terminado bien o mal el bloque."""
    ocupado = estado()
    if ocupado is not None:
        quien = ("un bloqueo MANUAL del aprobador" if ocupado.get("pid") is None
                 else f"otro arnés (PID {ocupado['pid']})")
        raise SemaforoOcupado(
            "SEMÁFORO EN ROJO: no arranca nada.\n"
            f"  Lo puso {quien} el {ocupado.get('cuando')}: {ocupado.get('motivo')!r}\n"
            "  Si es un bloqueo manual y ya terminaste, liberalo con:\n"
            "  python3 herramientas/pruebas/semaforo.py --liberar")
    _escribir(os.getpid(), motivo)
    try:
        yield
    finally:
        if os.path.exists(RUTA):
            os.remove(RUTA)


def main():
    argv = sys.argv[1:]
    if "--bloquear" in argv:
        i = argv.index("--bloquear")
        motivo = argv[i + 1] if i + 1 < len(argv) else "bloqueo manual del aprobador"
        actual = estado()
        if actual is not None:
            print(f"ya está ocupado, no se pisa: {actual}")
            return 1
        _escribir(None, motivo)
        print(f"semáforo en ROJO (manual): {motivo!r}")
        return 0
    if "--liberar" in argv:
        if os.path.exists(RUTA):
            os.remove(RUTA)
            print("semáforo liberado")
        else:
            print("ya estaba libre")
        return 0
    actual = estado()
    if actual is None:
        print("semáforo en VERDE (libre)")
        return 0
    print(f"semáforo en ROJO: {actual}")
    return 1


if __name__ == "__main__":
    sys.exit(main())
