#!/usr/bin/env python3
"""Corre los 5 casos x 2 corridas contra la Custom API del spike D-72 (Dev) y guarda
`resultados.json`. Por cada llamada: POST al Web API, cuenta de registros por prefijo
`zz-d72-<marca>-`, y la traza nueva de `plugintracelogs` del tipo del spike.
    python3 spikes/d72-transaccion-capi/scripts/correr_casos.py [modo ...]
"""
import json, os, sys, time

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(AQUI, "..", "..", "..", "herramientas"))
from dataverse_api import Dataverse

TIPO = "Sanic.Mppp.Spike.D72.TransaccionCapiApi"
MODOS = ["exito", "excepcion", "fallo_escritura", "sistema", "mixto"]
SALIDA = os.path.join(AQUI, "..", "resultados.json")

dv = Dataverse()


def contar(marca):
    est, c, _ = dv.call(
        "GET",
        f"sanic_mppp_tbl_autorizados?$select=sanic_nombre&$filter=startswith(sanic_nombre,'zz-d72-{marca}-')&$count=true",
    )
    assert est == 200, (est, c)
    return c["@odata.count"], sorted(f["sanic_nombre"] for f in c["value"])


def trazas_vistas():
    est, c, _ = dv.call(
        "GET",
        f"plugintracelogs?$select=plugintracelogid,createdon,messageblock,exceptiondetails,depth,mode,operationtype,messagename,primaryentity,performanceexecutionstarttime,correlationid&$filter=startswith(typename,'{TIPO},')&$orderby=createdon asc",
    )
    assert est == 200, (est, c)
    return {t["plugintracelogid"]: t for t in c["value"]}


def main():
    modos = sys.argv[1:] or MODOS
    previos = {}
    if os.path.exists(SALIDA):
        previos = {(r["modo"], r["corrida"]): r for r in json.load(open(SALIDA))}
    vistas = set(trazas_vistas())
    stamp = time.strftime("%H%M%S")
    for modo in modos:
        for corrida in (1, 2):
            marca = f"{modo.replace('_', '')}{corrida}x{stamp}"
            inicio = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
            est, cuerpo, _ = dv.call("POST", "sanic_mppp_capi_zzspiked72", {"modo": modo, "marca": marca})
            n, nombres = contar(marca)
            nuevas = []
            for _ in range(12):
                actuales = trazas_vistas()
                nuevas = [t for k, t in actuales.items() if k not in vistas]
                if nuevas:
                    break
                time.sleep(3)
            vistas |= {t["plugintracelogid"] for t in nuevas}
            r = {
                "modo": modo, "corrida": corrida, "marca": marca, "inicio_utc": inicio,
                "http": est,
                "respuesta": cuerpo,
                "registros_despues": n, "nombres": nombres,
                "trazas": [{k: t.get(k) for k in ("plugintracelogid", "createdon", "messageblock", "exceptiondetails", "depth", "mode", "messagename", "correlationid")} for t in nuevas],
            }
            previos[(modo, corrida)] = r
            json.dump(list(previos.values()), open(SALIDA, "w"), ensure_ascii=False, indent=1)
            print(json.dumps({k: r[k] for k in ("modo", "corrida", "marca", "http", "respuesta", "registros_despues")}, ensure_ascii=False))
            for t in nuevas:
                print("   traza:", (t["messageblock"] or "").strip().replace("\n", " | ")[:300])


main()
