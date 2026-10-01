#!/usr/bin/env python3
"""Limpieza del spike D-72 en Dev, en este orden: registros zz-d72-*, Custom API (parametros y
salidas primero, luego la API), paquete, solucion temporal. Borra SOLO por nombre del spike.
    python3 spikes/d72-transaccion-capi/scripts/limpiar.py
"""
import os, sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "herramientas"))
from dataverse_api import Dataverse

dv = Dataverse()


def borrar(ruta, que):
    est, c, _ = dv.call("DELETE", ruta)
    print(f"DELETE {que}: HTTP {est}" + ("" if est in (200, 204) else f" {c}"))
    return est in (200, 204)


# 1. registros
est, c, _ = dv.call("GET", "sanic_mppp_tbl_autorizados?$select=sanic_mppp_tbl_autorizadoid&$filter=startswith(sanic_nombre,'zz-d72-')")
assert est == 200, (est, c)
for f in c["value"]:
    borrar(f"sanic_mppp_tbl_autorizados({f['sanic_mppp_tbl_autorizadoid']})", "registro zz-d72")

# 2. Custom API
est, c, _ = dv.call("GET", "customapis?$select=customapiid&$filter=uniquename eq 'sanic_mppp_capi_zzspiked72'")
assert est == 200, (est, c)
for api in c["value"]:
    i = api["customapiid"]
    est, p, _ = dv.call("GET", f"customapirequestparameters?$select=customapirequestparameterid&$filter=_customapiid_value eq {i}")
    for x in p["value"]:
        borrar(f"customapirequestparameters({x['customapirequestparameterid']})", "parametro de entrada")
    est, p, _ = dv.call("GET", f"customapiresponseproperties?$select=customapiresponsepropertyid&$filter=_customapiid_value eq {i}")
    for x in p["value"]:
        borrar(f"customapiresponseproperties({x['customapiresponsepropertyid']})", "propiedad de salida")
    borrar(f"customapis({i})", "customapi sanic_mppp_capi_zzspiked72")

# 3. paquete
est, c, _ = dv.call("GET", "pluginpackages?$select=pluginpackageid&$filter=uniquename eq 'sanic_mppp_pkg_zzspiked72'")
assert est == 200, (est, c)
for x in c["value"]:
    borrar(f"pluginpackages({x['pluginpackageid']})", "paquete sanic_mppp_pkg_zzspiked72")

# 4. solucion temporal
est, c, _ = dv.call("GET", "solutions?$select=solutionid&$filter=uniquename eq 'sanic_mppp_sol_zzspiked72'")
assert est == 200, (est, c)
for x in c["value"]:
    borrar(f"solutions({x['solutionid']})", "solucion sanic_mppp_sol_zzspiked72")
