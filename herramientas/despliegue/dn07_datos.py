#!/usr/bin/env python3
"""Paso de DATOS de DN-07 (diseno/DIRECTRICES-NUEVAS.md): la regla `IDENTIFICACION_DEL_TIPO` en el catálogo. El parche
lleva el ensamblado 1.0.28.0; la regla es una fila y viaja acá.

    python3 herramientas/despliegue/dn07_datos.py [--env=<archivo .env>] [--solo-verificar]

Idempotente: si la regla no existe, la crea con los valores de `playbooks/datos/reglas.md`; si existe y coincide, no
hace nada; si existe con otro orden, dependencia, nivel o efecto, la denuncia y no la toca. Va DESPUÉS del ensamblado
1.0.28.0: sin evaluador, el plugin de integridad rechaza el código. Imprime la URL del ambiente antes de escribir.

Última línea: JSON con `estado` (actualizado | ya_existia | difiere | error) y `detalle`.
"""
import json
import os
import sys

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _AQUI)

from dn06_datos import ARCHIVO_ENV, EFECTO, NIVEL, Dataverse, escribir, filas_del_playbook, todas  # noqa: E402

REGLA = "IDENTIFICACION_DEL_TIPO"


def paso_regla(dv, solo_verificar, informe):
    fila = next(f for f in filas_del_playbook("playbooks/datos/reglas.md") if f["sanic_codigo"] == REGLA)
    deseado = {"sanic_codigo": REGLA, "sanic_orden": fila["sanic_orden"], "sanic_dependede": fila["sanic_dependede"],
               "sanic_nivel": NIVEL[fila["sanic_nivel"]["etiqueta"]], "sanic_efecto": EFECTO[fila["sanic_efecto"]["etiqueta"]]}
    reales = todas(dv, "sanic_mppp_tbl_reglas?$select=sanic_codigo,sanic_orden,sanic_dependede,sanic_nivel,sanic_efecto"
                       f"&$filter=sanic_codigo eq '{REGLA}'")
    if reales:
        r = reales[0]
        distinto = {k: (r.get(k), v) for k, v in deseado.items() if r.get(k) != v}
        if distinto:
            raise ValueError(f"{REGLA} ya existe con valores distintos (entorno, playbook): {distinto}; no se toca")
        return 0
    escribir(dv, "POST", "sanic_mppp_tbl_reglas", deseado, solo_verificar, informe, f"crea {REGLA}")
    return 1


def main(argv):
    solo_verificar = "--solo-verificar" in argv
    env = next((a.split("=", 1)[1] for a in argv if a.startswith("--env=")), ARCHIVO_ENV)
    desconocidas = [a for a in argv if a != "--solo-verificar" and not a.startswith("--env=")]
    if desconocidas:
        print(json.dumps({"estado": "error", "detalle": f"argumentos desconocidos: {desconocidas}"}, ensure_ascii=False))
        return 2
    informe = []
    try:
        dv = Dataverse(archivo_env=env)
        print(f"Ambiente: {dv.org}{' (solo verificar)' if solo_verificar else ''}")
        cambios = paso_regla(dv, solo_verificar, informe)
    except Exception as e:  # noqa: BLE001 - se informa y se sale con error
        print("\n".join(informe))
        print(json.dumps({"estado": "error", "detalle": str(e)}, ensure_ascii=False))
        return 1
    print("\n".join(informe))
    estado = "ya_existia" if cambios == 0 else ("difiere" if solo_verificar else "actualizado")
    print(json.dumps({"estado": estado, "detalle": f"reglas {cambios}"}, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
