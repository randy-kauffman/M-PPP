#!/usr/bin/env python3
"""Paso de DATOS de DN-06 (diseno/DIRECTRICES-NUEVAS.md, "D. Datos"): lo que el parche
`sanic_mppp_sol_parche_1_3_1` no lleva, porque son filas y no componentes de solución.

    python3 herramientas/despliegue/dn06_datos.py [--env=<archivo .env>] [--solo-verificar]

Tres pasos, EN ESTE ORDEN, todos idempotentes (correrlo dos veces no cambia nada la segunda):

1. Backfill de planes (activos e inactivos): `sanic_canal` vacío -> Banca en Linea y
   `sanic_requierepreregistro` vacío -> Sí. Solo completa lo vacío: NUNCA pisa un valor cargado.
2. Parámetro `plantilla.obligatoriedad.conectividad`: crea la fila de `playbooks/datos/parametros.md`
   si no existe esa versión. Si ya existe con otro valor, la denuncia y no la toca.
3. Catálogo de reglas: crea `PLAN_REQUIERE_PREREGISTRO` y después reapunta las dependencias de las
   cinco reglas, con los valores de `playbooks/datos/reglas.md`. La regla nueva va primero porque el
   plugin de integridad exige que la dependencia exista; y el ensamblado 1.0.27.0 tiene que estar
   subido antes (sin evaluador, el mismo plugin rechaza el código).

Los valores salen de los playbooks del repo (una sola fuente de verdad), no de este archivo.
`--env` apunta al archivo de credenciales del ambiente (por omisión `local/pp_secrets.env`, que es
Dev): el mismo script sirve para el otro ambiente. Imprime la URL del ambiente antes de escribir.
`--solo-verificar` no escribe nada: dice qué haría.

Última línea: JSON con `estado` (actualizado | ya_existia | difiere | error) y `detalle`.
"""
import json
import os
import sys

_AQUI = os.path.dirname(os.path.abspath(__file__))
_RAIZ = os.path.dirname(os.path.dirname(_AQUI))
sys.path.insert(0, os.path.dirname(_AQUI))
sys.path.insert(0, os.path.join(os.path.dirname(_AQUI), "construir"))

from dataverse_api import ARCHIVO_ENV, Dataverse  # noqa: E402
from _comun import dividir_secciones, leer_texto, obtener_componente  # noqa: E402

BANCA_EN_LINEA = 159460001          # sanic_mppp_ch_canal (playbooks/choice-global/sanic_mppp_ch_canal.md)
NIVEL = {"Solicitud": 159460001, "Registro": 159460002, "Correo": 159460003}   # sanic_mppp_ch_nivelregla
EFECTO = {"Rechaza": 159460001, "Envia a revision": 159460002, "Advierte": 159460003}  # sanic_mppp_ch_efectoregla
TIPO_PARAMETRO = {"Texto": 159460001, "Numero": 159460002, "JSON": 159460003}  # sanic_mppp_ch_tipoparametro

PARAMETRO = ("plantilla.obligatoriedad.conectividad", 1)
REGLA_NUEVA = "PLAN_REQUIERE_PREREGISTRO"
REGLAS_REAPUNTADAS = ["GESTION_DEL_FORMATO", "CLASIFICACION_DEL_FORMATO", "BANCO_DEL_FORMATO", "OBLIGATORIEDAD", "MONEDA_DEL_PLAN"]


def filas_del_playbook(ruta):
    return obtener_componente(dividir_secciones(leer_texto(os.path.join(_RAIZ, ruta))), "datos")["filas"]


def todas(dv, ruta):
    filas, siguiente = [], ruta
    while siguiente:
        est, cuerpo, _ = dv.call("GET", siguiente)
        if est != 200:
            raise RuntimeError(f"GET {siguiente}: HTTP {est} {cuerpo}")
        filas.extend(cuerpo.get("value", []))
        enlace = cuerpo.get("@odata.nextLink")
        siguiente = enlace.split("/api/data/v9.2/", 1)[1] if enlace else None
    return filas


def escribir(dv, metodo, ruta, cuerpo, solo_verificar, informe, que):
    if solo_verificar:
        informe.append(f"HARÍA {metodo} {ruta}: {que}")
        return
    est, resp, _ = dv.call(metodo, ruta, cuerpo)
    if est not in (200, 201, 204):
        raise RuntimeError(f"{metodo} {ruta} ({que}): HTTP {est} {resp}")
    informe.append(f"{metodo} {ruta}: {que}")


def paso_planes(dv, solo_verificar, informe):
    cambios = 0
    for p in todas(dv, "sanic_mppp_tbl_plans?$select=sanic_codigo,sanic_canal,sanic_requierepreregistro"):
        cuerpo = {}
        if p.get("sanic_canal") is None:
            cuerpo["sanic_canal"] = BANCA_EN_LINEA
        if p.get("sanic_requierepreregistro") is None:
            cuerpo["sanic_requierepreregistro"] = True
        if cuerpo:
            cambios += 1
            escribir(dv, "PATCH", f"sanic_mppp_tbl_plans({p['sanic_mppp_tbl_planid']})", cuerpo, solo_verificar, informe,
                     f"plan {p.get('sanic_codigo')} completa {sorted(cuerpo)}")
    return cambios


def paso_parametro(dv, solo_verificar, informe):
    nombre, version = PARAMETRO
    fila = next(f for f in filas_del_playbook("playbooks/datos/parametros.md")
                if f["sanic_nombre"] == nombre and f["sanic_version"] == version)
    existentes = todas(dv, f"sanic_mppp_tbl_parametros?$select=sanic_valor,sanic_version&$filter=sanic_nombre eq '{nombre}'")
    actual = next((e for e in existentes if e["sanic_version"] == version), None)
    if actual is not None:
        if json.loads(actual["sanic_valor"]) != json.loads(fila["sanic_valor"]):
            raise ValueError(f"el parámetro {nombre} v{version} ya existe con OTRO valor; no se toca (se versiona con parametro.py)")
        return 0
    cuerpo = {"sanic_nombre": nombre, "sanic_version": version, "sanic_valor": fila["sanic_valor"],
              "sanic_tipo": TIPO_PARAMETRO[fila["sanic_tipo"]["etiqueta"]], "sanic_descripcion": fila["sanic_descripcion"]}
    escribir(dv, "POST", "sanic_mppp_tbl_parametros", cuerpo, solo_verificar, informe, f"crea {nombre} v{version}")
    return 1


def paso_reglas(dv, solo_verificar, informe):
    playbook = {f["sanic_codigo"]: f for f in filas_del_playbook("playbooks/datos/reglas.md")}
    codigos = [REGLA_NUEVA] + REGLAS_REAPUNTADAS
    filtro = " or ".join(f"sanic_codigo eq '{c}'" for c in codigos)
    reales = {r["sanic_codigo"]: r for r in todas(
        dv, f"sanic_mppp_tbl_reglas?$select=sanic_codigo,sanic_orden,sanic_dependede,sanic_nivel,sanic_efecto&$filter={filtro}")}
    cambios = 0
    nueva = playbook[REGLA_NUEVA]
    if REGLA_NUEVA not in reales:
        cuerpo = {"sanic_codigo": REGLA_NUEVA, "sanic_orden": nueva["sanic_orden"], "sanic_dependede": nueva["sanic_dependede"],
                  "sanic_nivel": NIVEL[nueva["sanic_nivel"]["etiqueta"]], "sanic_efecto": EFECTO[nueva["sanic_efecto"]["etiqueta"]]}
        escribir(dv, "POST", "sanic_mppp_tbl_reglas", cuerpo, solo_verificar, informe, f"crea {REGLA_NUEVA}")
        cambios += 1
    else:
        r = reales[REGLA_NUEVA]
        if (r["sanic_orden"], r["sanic_dependede"]) != (nueva["sanic_orden"], nueva["sanic_dependede"]):
            raise ValueError(f"{REGLA_NUEVA} ya existe con orden/dependencias distintas: {r['sanic_orden']} / {r['sanic_dependede']}")
    for codigo in REGLAS_REAPUNTADAS:
        if codigo not in reales:
            raise ValueError(f"la regla {codigo} no existe en el ambiente: el catálogo no es el de DN-05")
        deseado = playbook[codigo]["sanic_dependede"]
        if reales[codigo]["sanic_dependede"] != deseado:
            escribir(dv, "PATCH", f"sanic_mppp_tbl_reglas({reales[codigo]['sanic_mppp_tbl_reglaid']})", {"sanic_dependede": deseado},
                     solo_verificar, informe, f"{codigo}: '{reales[codigo]['sanic_dependede']}' -> '{deseado}'")
            cambios += 1
    return cambios


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
        planes = paso_planes(dv, solo_verificar, informe)
        parametro = paso_parametro(dv, solo_verificar, informe)
        reglas = paso_reglas(dv, solo_verificar, informe)
    except Exception as e:  # noqa: BLE001 - se informa y se sale con error
        print("\n".join(informe))
        print(json.dumps({"estado": "error", "detalle": str(e)}, ensure_ascii=False))
        return 1
    print("\n".join(informe))
    total = planes + parametro + reglas
    estado = "ya_existia" if total == 0 else ("difiere" if solo_verificar else "actualizado")
    print(json.dumps({"estado": estado, "detalle": f"planes {planes}, parámetro {parametro}, reglas {reglas}"}, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
