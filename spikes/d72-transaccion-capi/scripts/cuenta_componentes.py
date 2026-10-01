#!/usr/bin/env python3
"""Cuenta los solutioncomponents de una solucion (total, por componenttype) y imprime un
hash estable del conjunto (componenttype, objectid). Uso:
    python3 spikes/d72-transaccion-capi/scripts/cuenta_componentes.py sanic_mppp_sol_mantenimientoppp
"""
import collections, hashlib, json, os, sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "herramientas"))
from dataverse_api import Dataverse

dv = Dataverse()
sol = sys.argv[1]
est, c, _ = dv.call("GET", f"solutions?$select=solutionid&$filter=uniquename eq '{sol}'")
assert est == 200 and c["value"], (est, c)
sid = c["value"][0]["solutionid"]
ruta = f"solutioncomponents?$select=componenttype,objectid&$filter=_solutionid_value eq {sid}"
filas = []
while ruta:
    est, c, _ = dv.call("GET", ruta, cabeceras={"Prefer": "odata.maxpagesize=5000"})
    assert est == 200, (est, c)
    filas += c["value"]
    nxt = c.get("@odata.nextLink")
    ruta = nxt.split("/api/data/v9.2/")[1] if nxt else None
claves = sorted(f"{f['componenttype']}:{f['objectid']}" for f in filas)
print("solucion", sol)
print("total", len(filas))
print("por_tipo", json.dumps(sorted(collections.Counter(f["componenttype"] for f in filas).items())))
print("sha256", hashlib.sha256("\n".join(claves).encode()).hexdigest())
