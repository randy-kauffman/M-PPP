#!/usr/bin/env python3
"""Datos propios de los casos GUI L-09 (carga masiva grabada + cambio de estado) y L-55 (Comentarios por el formulario)
del set L (`pruebas/12-catalogos-masivos.md`), que ESCRIBEN en Dev. Los usa `casos/dn-04-catalogos-masivos.spec.mjs`:
`--sembrar` en el `beforeAll`, `--limpiar` en el `afterAll`, ambos por `child_process` (mismo puente que
`ayudantes/datos-gui.mjs` con `sembrar_datos_gui.py`: el arnés nunca escribe en Dataverse por su cuenta).

Por qué un script aparte y no `sembrar_datos_gui.py`: ese siembra el fixture compartido de TODA la corrida (Cliente/Plan/
Solicitud/Filas). Este es de UN solo set de casos y tiene que poder limpiarse por separado y repetirse sin residuos:
la carga masiva CREA un cliente, un plan, un correo y una autorización que el script no sembró (los crea la Custom API
con el Administrador de planes), así que `--limpiar` busca por las claves reservadas del set, no por GUID.

Qué hay en juego (todo en rangos reservados, nada compartido con otros sets):
  - CIF BAC 999980004 / CIF COM ZZPCA0004001 -> "ZZP-Cliente 4": lo CREA la carga del caso (con el plan PZ04 y el correo
    zz-l-ana@pruebas.mppp.local). `--sembrar` NO lo siembra; solo se asegura de que no haya restos.
  - CIF BAC 999980005 / CIF COM ZZPCA0005001 -> "ZZP-Cliente 5": lo siembra este script, INACTIVO (la carga lo nombra solo
    para provocar la advertencia "cliente inactivo"), con el correo zz-l-form@pruebas.mppp.local ("Form ZZP") y su
    autorización por cliente (la que abre el caso L-55 en el formulario). Dueño: "# User 1".

Borrado seguro (CONVENCION-MARCA-AGUA.md): los clientes se borran con `marca_agua.borrar_marcado` (relee `sanic_nombre` y
exige el prefijo `ZZP-`). Los correos NO llevan el prefijo en `sanic_nombre` (ahí va el correo mismo), así que solo se
borran los dos correos EXACTOS de la lista reservada. Planes y autorizaciones se borran solo si cuelgan de un cliente
marcado de este set o de uno de los dos correos reservados. Un CIF reservado cuyo cliente no lleva `ZZP-` NO se toca:
`--limpiar` termina en error y lo dice.

Uso:
    python3 pruebas/gui/datos_dn04_l09.py --sembrar    # limpia restos, siembra, imprime un JSON con los ids
    python3 pruebas/gui/datos_dn04_l09.py --limpiar    # borra todo lo del set, imprime qué borró y verifica que no queda nada
La última línea de stdout es siempre el JSON de resultado (mismo contrato que `sembrar_datos_gui.py`).
"""
import argparse
import json
import os
import sys

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(_AQUI, "..", "..", "herramientas"))
sys.path.insert(0, os.path.join(_AQUI, "..", "..", "herramientas", "pruebas"))
from dataverse_api import Dataverse  # noqa: E402
import marca_agua  # noqa: E402

CLIENTES = "sanic_mppp_tbl_clientes"
PLANES = "sanic_mppp_tbl_plans"
AUTORIZADOS = "sanic_mppp_tbl_autorizados"
AUT_CLIENTES = "sanic_mppp_tbl_autorizacionclientes"
AUT_PLANES = "sanic_mppp_tbl_autorizacionplans"

CIF_BAC_4, CIF_BAC_5 = "999980004", "999980005"
CIF_COM_5 = "ZZPCA0005001"
CLIENTE_5 = marca_agua.nombre_de_prueba("Cliente 5")  # "ZZP-Cliente 5"
PLAN_4 = "PZ04"
CORREO_FORM = "zz-l-form@pruebas.mppp.local"
CONTACTO_FORM = "Form ZZP"
CORREO_ANA = "zz-l-ana@pruebas.mppp.local"
CORREOS_RESERVADOS = (CORREO_FORM, CORREO_ANA)
EJECUTIVO = "# User 1"  # fullname del dueño del cliente sembrado


def salida(estado, detalle):
    print(json.dumps({"estado": estado, "componente": "datos L-09/L-55", "detalle": detalle}, ensure_ascii=False))
    return 0 if estado in ("creado", "limpio") else 1


def obtener(dv, ruta):
    est, r, _ = dv.call("GET", ruta)
    if est != 200:
        raise RuntimeError(f"GET {ruta} devolvió HTTP {est}: {str(r)[:300]}")
    return r.get("value") or []


def crear(dv, conjunto, cuerpo):
    est, r, cab = dv.call("POST", conjunto, cuerpo)
    if est not in (200, 201, 204):
        raise RuntimeError(f"POST {conjunto} devolvió HTTP {est}: {str(r)[:300]}")
    return Dataverse.id_creado(cab)


def borrar(dv, conjunto, id_registro):
    est, r, _ = dv.call("DELETE", f"{conjunto}({id_registro})")
    if est not in (200, 204):
        raise RuntimeError(f"DELETE {conjunto}({id_registro}) devolvió HTTP {est}: {str(r)[:300]}")


def clientes_reservados(dv):
    """Los clientes con los dos CIF BAC reservados (activos o inactivos: la Web API no filtra por estado)."""
    filtro = f"sanic_cifbac eq '{CIF_BAC_4}' or sanic_cifbac eq '{CIF_BAC_5}'"
    return obtener(dv, f"{CLIENTES}?$select=sanic_mppp_tbl_clienteid,sanic_nombre,sanic_cifbac&$filter={filtro}")


def correos_reservados(dv):
    filtro = " or ".join(f"sanic_nombre eq '{c}'" for c in CORREOS_RESERVADOS)
    return obtener(dv, f"{AUTORIZADOS}?$select=sanic_mppp_tbl_autorizadoid,sanic_nombre&$filter={filtro}")


def limpiar(dv):
    """Borra autorizaciones -> correos -> planes -> clientes. Devuelve (borrados, problemas)."""
    borrados, problemas = [], []
    clientes = clientes_reservados(dv)
    correos = correos_reservados(dv)

    # Solo los clientes con la marca son "de este set"; un CIF reservado sin marca se informa y no se toca.
    propios = []
    for c in clientes:
        if marca_agua.marcado(c.get("sanic_nombre")):
            propios.append(c)
        else:
            problemas.append(f"el cliente con CIF BAC {c['sanic_cifbac']} se llama {c.get('sanic_nombre')!r}: "
                             f"no lleva {marca_agua.PREFIJO!r}, NO se toca")
    ids_clientes = [c["sanic_mppp_tbl_clienteid"] for c in propios]
    ids_correos = [c["sanic_mppp_tbl_autorizadoid"] for c in correos]

    # 1) autorizaciones: las de nuestros clientes y las de nuestros correos (por cliente y por plan).
    aut_cli, aut_plan = {}, {}
    for idc in ids_clientes:
        for a in obtener(dv, f"{AUT_CLIENTES}?$select=sanic_mppp_tbl_autorizacionclienteid&$filter=_sanic_clienteid_value eq {idc}"):
            aut_cli[a["sanic_mppp_tbl_autorizacionclienteid"]] = "autorizacion por cliente"
    for ida in ids_correos:
        for a in obtener(dv, f"{AUT_CLIENTES}?$select=sanic_mppp_tbl_autorizacionclienteid&$filter=_sanic_autorizadoid_value eq {ida}"):
            aut_cli[a["sanic_mppp_tbl_autorizacionclienteid"]] = "autorizacion por cliente"
        for a in obtener(dv, f"{AUT_PLANES}?$select=sanic_mppp_tbl_autorizacionplanid&$filter=_sanic_autorizadoid_value eq {ida}"):
            aut_plan[a["sanic_mppp_tbl_autorizacionplanid"]] = "autorizacion por plan"
    # Los planes de nuestros clientes y su autorización por plan (si la hubiera).
    planes = {}
    for idc in ids_clientes:
        for p in obtener(dv, f"{PLANES}?$select=sanic_mppp_tbl_planid,sanic_codigo&$filter=_sanic_clienteid_value eq {idc}"):
            planes[p["sanic_mppp_tbl_planid"]] = p.get("sanic_codigo")
    for idp in planes:
        for a in obtener(dv, f"{AUT_PLANES}?$select=sanic_mppp_tbl_autorizacionplanid&$filter=_sanic_planid_value eq {idp}"):
            aut_plan[a["sanic_mppp_tbl_autorizacionplanid"]] = "autorizacion por plan"
    for ida in aut_cli:
        borrar(dv, AUT_CLIENTES, ida)
        borrados.append(f"autorizacion por cliente {ida}")
    for ida in aut_plan:
        borrar(dv, AUT_PLANES, ida)
        borrados.append(f"autorizacion por plan {ida}")

    # 2) correos (solo los dos exactos de la lista reservada).
    for c in correos:
        borrar(dv, AUTORIZADOS, c["sanic_mppp_tbl_autorizadoid"])
        borrados.append(f"correo {c['sanic_nombre']}")

    # 3) planes (todos los de nuestros clientes marcados; PZ04 incluido, por construcción).
    for idp, codigo in planes.items():
        borrar(dv, PLANES, idp)
        borrados.append(f"plan {codigo}")
    # PZ04 que cuelgue de OTRO cliente no es de este set: se informa, no se borra.
    for p in obtener(dv, f"{PLANES}?$select=sanic_mppp_tbl_planid,_sanic_clienteid_value&$filter=sanic_codigo eq '{PLAN_4}'"):
        problemas.append(f"el plan {PLAN_4} {p['sanic_mppp_tbl_planid']} cuelga del cliente {p.get('_sanic_clienteid_value')}, "
                         "que no es de este set: NO se toca")

    # 4) clientes, con el borrado seguro (relee el nombre justo antes del DELETE).
    for c in propios:
        ok, motivo = marca_agua.borrar_marcado(dv, CLIENTES, c["sanic_mppp_tbl_clienteid"], campo="sanic_nombre")
        if ok:
            borrados.append(f"cliente {c['sanic_nombre']} (CIF BAC {c['sanic_cifbac']})")
        else:
            problemas.append(f"cliente {c['sanic_nombre']}: {motivo}")

    # Verificación: no queda nada reservado (salvo lo que se informó como problema).
    restos = [f"cliente {c['sanic_cifbac']}" for c in clientes_reservados(dv) if marca_agua.marcado(c.get("sanic_nombre"))]
    restos += [f"correo {c['sanic_nombre']}" for c in correos_reservados(dv)]
    if restos:
        problemas.append(f"quedaron restos: {restos}")
    return borrados, problemas


def id_del_ejecutivo(dv):
    filas = obtener(dv, f"systemusers?$select=systemuserid,fullname&$filter=fullname eq '{EJECUTIVO}'")
    if len(filas) != 1:
        raise RuntimeError(f"se esperaba exactamente un usuario {EJECUTIVO!r} y hay {len(filas)}")
    return filas[0]["systemuserid"]


def sembrar(dv):
    previos, problemas = limpiar(dv)  # idempotente: parte siempre de cero
    if problemas:
        raise RuntimeError(f"la limpieza previa no terminó limpia: {problemas}")
    dueño = id_del_ejecutivo(dv)

    id_cliente = crear(dv, CLIENTES, {
        "sanic_nombre": CLIENTE_5, "sanic_cifbac": CIF_BAC_5, "sanic_cifcom": CIF_COM_5,
        "ownerid@odata.bind": f"/systemusers({dueño})",
    })
    id_correo = crear(dv, AUTORIZADOS, {"sanic_nombre": CORREO_FORM, "sanic_nombrecontacto": CONTACTO_FORM})
    # `sanic_nombre` de la autorización lo recalcula siempre el step NombreCalculado; se manda uno para el nivel
    # "ApplicationRequired" de la metadata.
    id_aut = crear(dv, AUT_CLIENTES, {
        "sanic_nombre": f"{CORREO_FORM} -> {CLIENTE_5}",
        "sanic_autorizadoid@odata.bind": f"/{AUTORIZADOS}({id_correo})",
        "sanic_clienteid@odata.bind": f"/{CLIENTES}({id_cliente})",
    })
    # El cliente se desactiva DESPUÉS de crear su autorización (mismo orden que `preparar_datos.py` con el plan inactivo):
    # el caso prueba "el cliente está inactivo", no "no se pudo autorizar".
    est, r, _ = dv.call("PATCH", f"{CLIENTES}({id_cliente})", {"statecode": 1, "statuscode": 2})
    if est not in (200, 204):
        raise RuntimeError(f"no se pudo desactivar {CLIENTE_5}: HTTP {est} {str(r)[:300]}")
    ver = obtener(dv, f"{CLIENTES}?$select=statecode,_ownerid_value&$filter=sanic_mppp_tbl_clienteid eq {id_cliente}")
    if not ver or ver[0].get("statecode") != 1 or ver[0].get("_ownerid_value") != dueño:
        raise RuntimeError(f"el cliente sembrado no quedó inactivo y de {EJECUTIVO}: {ver}")
    return {"clienteId": id_cliente, "correoId": id_correo, "autorizacionId": id_aut, "duenoId": dueño,
            "restosBorradosAntes": previos}


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    modo = p.add_mutually_exclusive_group(required=True)
    modo.add_argument("--sembrar", action="store_true")
    modo.add_argument("--limpiar", action="store_true")
    args = p.parse_args()
    try:
        dv = Dataverse()
        if args.sembrar:
            return salida("creado", sembrar(dv))
        borrados, problemas = limpiar(dv)
        if problemas:
            return salida("error", {"borrados": borrados, "problemas": problemas})
        return salida("limpio", {"borrados": borrados} if borrados else "no habia nada que borrar")
    except Exception as e:  # noqa: BLE001 — cualquier falla sale como JSON de error, nunca como traza suelta
        return salida("error", f"{type(e).__name__}: {e}")


if __name__ == "__main__":
    sys.exit(main())
