#!/usr/bin/env python3
"""Cuenta lo que queda del spike D-72 en Dev (todo debe dar 0).
    python3 spikes/d72-transaccion-capi/scripts/verificar_limpieza.py
"""
import os, sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "herramientas"))
from dataverse_api import Dataverse

dv = Dataverse()
consultas = [
    ("registros zz-d72-*", "sanic_mppp_tbl_autorizados?$select=sanic_nombre&$filter=startswith(sanic_nombre,'zz-d72-')&$count=true"),
    ("customapis zzspiked72", "customapis?$select=uniquename&$filter=contains(uniquename,'zzspiked72')&$count=true"),
    ("customapirequestparameters del spike", "customapirequestparameters?$select=uniquename&$filter=contains(name,'zzspiked72') or contains(name,'zzspike')&$count=true"),
    ("pluginpackages zzspiked72", "pluginpackages?$select=uniquename&$filter=contains(uniquename,'zzspiked72')&$count=true"),
    ("pluginassemblies Sanic.Mppp.Spike.D72", "pluginassemblies?$select=name&$filter=name eq 'Sanic.Mppp.Spike.D72'&$count=true"),
    ("plugintypes del spike", "plugintypes?$select=typename&$filter=startswith(typename,'Sanic.Mppp.Spike.D72')&$count=true"),
    ("solutions zzspiked72", "solutions?$select=uniquename&$filter=contains(uniquename,'zzspiked72')&$count=true"),
]
for nombre, ruta in consultas:
    est, c, _ = dv.call("GET", ruta)
    print(f"{nombre}: HTTP {est} count={c.get('@odata.count') if est == 200 else c}")
