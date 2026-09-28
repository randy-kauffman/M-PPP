#!/usr/bin/env python3
"""Siembra (o limpia) los datos que necesita el arnés de NAVEGADOR para poder
correr contra algo real: un Cliente propiedad del usuario de la sesión activa
(para que "Mis clientes - por digitar" no aparezca vacía), un Plan, una
Solicitud y N Filas en estado Validada.

Por qué existe este archivo y no se reusa `herramientas/pruebas/preparar_datos.py`
tal cual (aunque se reusa SU PATRÓN: buscar antes de crear, prefijo
descartable, `--limpiar` simétrico, JSON de una línea):

  1. `preparar_datos.py` sirve a los casos de CORREO/API (`pruebas/01..04`):
     crea Cliente/Plan/Autorizado/AutorizacionPlan, pero NUNCA una Solicitud
     ni una Fila, y nunca toca `ownerid`. No alcanza para "hay filas
     Validadas en la grilla, del dueño correcto".
  2. El `ownerid` del Cliente tiene que ser el usuario de la SESIÓN DE
     NAVEGADOR activa (se lo pasa `--propietario`, nunca hardcodeado: lo
     descubre quien llama con `WhoAmI` sobre esa sesión — ver
     `ayudantes/datos-gui.mjs`). Eso varía por corrida (ejecutivo, supervisor,
     etc.), así que la siembra no puede ser estática.

Por qué esto SÍ usa el service principal (`herramientas/dataverse_api.py`,
`local/pp_secrets.env`) y NO la sesión delegada del navegador: se probó en
vivo (2026-09-27) que el rol Ejecutivo NO tiene el privilegio de Crear sobre
`sanic_mppp_tbl_cliente` (HTTP 403, "is missing prvCreatesanic_mppp_tbl_cliente
privilege") — con toda razón: una persona de negocio no escribe la tabla
Cliente a mano por Web API, la app no se lo permite. El service principal SÍ
puede, y es el mismo que usan todas las herramientas de `herramientas/pruebas/`.

Namespace de datos descartable: prefijo de código `ZG` (Zz Gui) en los campos
cortos (`sanic_codigo`, `sanic_messageid`), DISTINTO del `PR` de
`preparar_datos.py` para no pisar ni confundirse con esos datos — Y, en el
Cliente (el campo de texto largo, el mismo tipo de registro que el incidente
de `CONVENCION-MARCA-AGUA.md` borró por error), el prefijo RESERVADO `ZZP-`
de `herramientas/pruebas/marca_agua.py`, la única fuente de verdad del
proyecto para esa marca. `limpiar()` usa `marca_agua.borrar_marcado()` para
el Cliente: relee `sanic_nombre` justo antes del DELETE y NO borra si no
lleva el prefijo — la misma regla que protege al fixture compartido
(Cliente `be5332ec-03b7-f111-aaad-7ced8d3d6a77`, NUNCA tocado por este script:
se busca siempre por el nombre propio `ZZP-Pruebas GUI SA`, nunca por
parecido).

Por qué este script NO toma el semáforo (`herramientas/pruebas/semaforo.py`)
por su cuenta: lo toma `setup/global-setup.mjs` (Node) para TODA la corrida
(siembra + casos + limpieza), porque ese es el ciclo de vida real que toca
Dev, igual que `correr.py` lo toma una vez por set, no una vez por script
interno. Si corrés este archivo a mano, fuera del arnés de Playwright, sos
vos quien tiene que chequear el semáforo primero (`semaforo.py --estado`):
este script no lo hace por vos a propósito, para no tomar el candado dos
veces en la misma corrida (Node ya lo tiene) y terminar reventando con
"SEMÁFORO EN ROJO" contra su propio arnés.

Uso:
    python3 sembrar_datos_gui.py --propietario <systemuserid> [--filas 3]
    python3 sembrar_datos_gui.py --verificar
    python3 sembrar_datos_gui.py --limpiar
"""
import argparse
import json
import sys
import os

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(_AQUI, "..", "..", "herramientas"))
sys.path.insert(0, os.path.join(_AQUI, "..", "..", "herramientas", "pruebas"))
from dataverse_api import Dataverse  # noqa: E402
import marca_agua  # noqa: E402

CLIENTE_NOMBRE = marca_agua.nombre_de_prueba("Pruebas GUI SA")  # "ZZP-Pruebas GUI SA"
CIF_BAC = "900100299"
CIF_COM = "ZZGUIFIXT001"
PLAN_CODIGO = "ZG01"
SOLICITUD_MESSAGEID = "zz-gui-fixture@pruebas.mppp.local"

FORMATO_06 = 159460001
MONEDA_COR = 159460001
GESTION_INCLUSION = 159460001
CLASIFICACION_BAC = 159460001
TIPOID_CNA = 159460001
BANCO_BAC = 159460002
ESTADOFILA_VALIDADA = 159460003
ESTADOFILA_DIGITADA = 159460004
ESTADOSOLICITUD_EN_PROCESO = 159460005

CLIENTES = "sanic_mppp_tbl_clientes"
PLANES = "sanic_mppp_tbl_plans"
SOLICITUDES = "sanic_mppp_tbl_solicituds"
FILAS = "sanic_mppp_tbl_filas"


def salida(estado, detalle):
    print(json.dumps({"estado": estado, "componente": "datos de prueba GUI", "detalle": detalle},
                     ensure_ascii=False))
    return 0 if estado in ("creado", "ya_existia", "limpio") else 1


def uno(dv, conjunto, filtro, select="*"):
    sel = "" if select == "*" else f"$select={select}&"
    est, r, _ = dv.call("GET", f"{conjunto}?{sel}$filter={filtro}")
    if est != 200:
        raise RuntimeError(f"GET {conjunto} devolvió HTTP {est}: {str(r)[:220]}")
    v = r.get("value") or []
    return v[0] if v else None


def crear(dv, conjunto, cuerpo):
    est, r, cab = dv.call("POST", conjunto, cuerpo)
    if est not in (200, 201, 204):
        raise RuntimeError(f"POST {conjunto} devolvió HTTP {est}: {str(r)[:300]}")
    return Dataverse.id_creado(cab)


def sembrar(dv, propietario, cantidad_filas):
    hechos = []

    cli = uno(dv, CLIENTES, f"sanic_nombre eq '{CLIENTE_NOMBRE}'",
              "sanic_mppp_tbl_clienteid,_ownerid_value")
    if cli is None:
        idc = crear(dv, CLIENTES, {
            "sanic_nombre": CLIENTE_NOMBRE, "sanic_cifbac": CIF_BAC, "sanic_cifcom": CIF_COM,
        })
        hechos.append(f"cliente {CLIENTE_NOMBRE}")
    else:
        idc = cli["sanic_mppp_tbl_clienteid"]

    # El ownerid SIEMPRE se (re)asigna a quien pide la siembra, exista o no el
    # cliente: es lo que hace que "Mis clientes - por digitar" muestre datos
    # para ESTA sesión, y otra sesión (otro rol) puede sembrar después.
    if cli is None or cli.get("_ownerid_value") != propietario:
        est, r, _ = dv.call("PATCH", f"{CLIENTES}({idc})",
                            {"ownerid@odata.bind": f"/systemusers({propietario})"})
        if est not in (200, 204):
            raise RuntimeError(f"No se pudo asignar el cliente a {propietario}: HTTP {est} {r}")
        hechos.append(f"cliente asignado a {propietario}")

    pl = uno(dv, PLANES, f"sanic_codigo eq '{PLAN_CODIGO}'", "sanic_mppp_tbl_planid")
    if pl is None:
        idp = crear(dv, PLANES, {
            "sanic_codigo": PLAN_CODIGO, "sanic_tipoformato": FORMATO_06, "sanic_moneda": MONEDA_COR,
            "sanic_clienteid@odata.bind": f"/{CLIENTES}({idc})",
        })
        hechos.append(f"plan {PLAN_CODIGO}")
    else:
        idp = pl["sanic_mppp_tbl_planid"]

    sol = uno(dv, SOLICITUDES, f"sanic_messageid eq '{SOLICITUD_MESSAGEID}'",
              "sanic_mppp_tbl_solicitudid")
    if sol is None:
        ids = crear(dv, SOLICITUDES, {
            "sanic_messageid": SOLICITUD_MESSAGEID,
            "sanic_remitente": "zz-gui-fixture@pruebas.mppp.local",
            "sanic_fecharecibido": "2026-01-01T00:00:00Z",
            "sanic_fechaingresada": "2026-01-01T00:00:00Z",
            "sanic_estadoprocesamiento": ESTADOSOLICITUD_EN_PROCESO,
        })
        hechos.append(f"solicitud {SOLICITUD_MESSAGEID}")
    else:
        ids = sol["sanic_mppp_tbl_solicitudid"]

    # Filas 1..cantidad_filas en Validada (DN-02, y los 5 comandos cuyo
    # EnableRule admite Validada: digitada, rechazadaas400, anular, atendida,
    # descargarparadigitar — ver TRANSICIONES_VALIDAS en recursos/js/comandos.js).
    # UNA fila mas, al final, en Digitada: es la UNICA forma de que Aprobar y
    # Devolver (que solo admiten Digitada) tengan algo que habilitarlos.
    # Verificado en vivo (2026-09-27): si NINGUNA fila seleccionada esta en
    # Digitada, esos dos botones no se renderizan (no quedan "deshabilitados
    # y grises": desaparecen del todo, MISMO efecto visual que el defecto que
    # motivo este arnes). No es un bug: es que ninguna fila seleccionada
    # admitia la accion. Por eso la siembra necesita las DOS variantes de
    # estado para poder probar los 7 botones de una sola vez.
    definicion_filas = [(n, ESTADOFILA_VALIDADA) for n in range(1, cantidad_filas + 1)]
    definicion_filas.append((cantidad_filas + 1, ESTADOFILA_DIGITADA))

    ids_fila = []
    for n, estado_esperado in definicion_filas:
        f = uno(dv, FILAS,
                f"_sanic_solicitudid_value eq {ids} and sanic_numerofila eq {n}",
                "sanic_mppp_tbl_filaid,sanic_estado,statecode")
        cuerpo_fila = {
            "sanic_numerofila": n,
            "sanic_estado": estado_esperado,
            "sanic_gestion": GESTION_INCLUSION,
            "sanic_clasificacion": CLASIFICACION_BAC,
            "sanic_moneda": MONEDA_COR,
            "sanic_tipoidentificacion": TIPOID_CNA,
            "sanic_banco": BANCO_BAC,
            "sanic_numeroplan": PLAN_CODIGO,
            "sanic_nombrebeneficiario": f"ZZ Beneficiario {n}",
            "sanic_numeroidentificacion": f"000000000{n}",
            "sanic_numerocuenta": f"00000000000{n}",
            "sanic_referencia": f"ZZ-REF-{n:02d}",
        }
        if f is None:
            idf = crear(dv, FILAS, {
                **cuerpo_fila,
                "sanic_solicitudid@odata.bind": f"/{SOLICITUDES}({ids})",
                "sanic_planid@odata.bind": f"/{PLANES}({idp})",
            })
            hechos.append(f"fila {n} ({'Validada' if estado_esperado == ESTADOFILA_VALIDADA else 'Digitada'})")
        else:
            idf = f["sanic_mppp_tbl_filaid"]
            # Reponer al estado esperado: una corrida anterior puede haber
            # ejecutado un comando real (Digitada, Anular, etc.) y dejarla en
            # otro estado. La siembra siempre deja el fixture en el estado
            # conocido que los casos esperan.
            if f.get("sanic_estado") != estado_esperado or f.get("statecode") != 0:
                est, r, _ = dv.call("PATCH", f"{FILAS}({idf})",
                                    {"sanic_estado": estado_esperado, "statecode": 0, "statuscode": 1})
                if est not in (200, 204):
                    raise RuntimeError(f"No se pudo reponer la fila {n}: HTTP {est} {r}")
                hechos.append(f"fila {n} repuesta a su estado esperado")
        ids_fila.append(idf)

    return {"clienteId": idc, "planId": idp, "solicitudId": ids, "filaIds": ids_fila, "hechos": hechos}


def limpiar(dv, cantidad_filas):
    borrados = []
    sol = uno(dv, SOLICITUDES, f"sanic_messageid eq '{SOLICITUD_MESSAGEID}'", "sanic_mppp_tbl_solicitudid")
    if sol:
        ids = sol["sanic_mppp_tbl_solicitudid"]
        est, r, _ = dv.call("GET", f"{FILAS}?$select=sanic_mppp_tbl_filaid,sanic_numerofila"
                                    f"&$filter=_sanic_solicitudid_value eq {ids}")
        for f in r.get("value", []):
            dv.call("DELETE", f"{FILAS}({f['sanic_mppp_tbl_filaid']})")
            borrados.append(f"fila {f.get('sanic_numerofila')}")
        dv.call("DELETE", f"{SOLICITUDES}({ids})")
        borrados.append("solicitud")
    pl = uno(dv, PLANES, f"sanic_codigo eq '{PLAN_CODIGO}'", "sanic_mppp_tbl_planid")
    if pl:
        dv.call("DELETE", f"{PLANES}({pl['sanic_mppp_tbl_planid']})")
        borrados.append("plan")
    cli = uno(dv, CLIENTES, f"sanic_nombre eq '{CLIENTE_NOMBRE}'",
              "sanic_mppp_tbl_clienteid,sanic_nombre")
    if cli:
        # Borrado seguro (CONVENCION-MARCA-AGUA.md): relee `sanic_nombre`
        # justo antes del DELETE y solo borra si lleva el prefijo `ZZP-`. No
        # se le pasa `valor_conocido` (aunque ya lo tenemos de `uno()`) a
        # propósito: se prefiere la relectura fresca de `borrar_marcado`
        # sobre confiar en un valor leído un instante antes, mismo espíritu
        # que motivó la convención.
        ok, motivo = marca_agua.borrar_marcado(
            dv, CLIENTES, cli["sanic_mppp_tbl_clienteid"], campo="sanic_nombre")
        if not ok:
            raise RuntimeError(f"NO se borró el cliente de prueba GUI: {motivo}")
        borrados.append("cliente")
    return borrados


def verificar(dv):
    cli = uno(dv, CLIENTES, f"sanic_nombre eq '{CLIENTE_NOMBRE}'", "sanic_mppp_tbl_clienteid,_ownerid_value")
    pl = uno(dv, PLANES, f"sanic_codigo eq '{PLAN_CODIGO}'", "sanic_mppp_tbl_planid")
    sol = uno(dv, SOLICITUDES, f"sanic_messageid eq '{SOLICITUD_MESSAGEID}'", "sanic_mppp_tbl_solicitudid")
    return {
        "cliente": bool(cli), "propietarioActual": cli.get("_ownerid_value") if cli else None,
        "plan": bool(pl), "solicitud": bool(sol),
    }


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--propietario", help="systemuserid al que se le asigna el Cliente (WhoAmI de la sesion)")
    p.add_argument("--filas", type=int, default=3, help="cuantas Filas Validada sembrar (default 3)")
    p.add_argument("--limpiar", action="store_true")
    p.add_argument("--verificar", action="store_true")
    args = p.parse_args()

    dv = Dataverse()
    try:
        if args.limpiar:
            borrados = limpiar(dv, args.filas)
            return salida("limpio", f"borrados: {borrados}" if borrados else "no habia nada que borrar")
        if args.verificar:
            return salida("ya_existia", verificar(dv))
        if not args.propietario:
            return salida("error", "hace falta --propietario <systemuserid> (WhoAmI de la sesion activa)")
        resultado = sembrar(dv, args.propietario, args.filas)
        return salida("creado" if resultado["hechos"] else "ya_existia", resultado)
    except Exception as e:
        return salida("error", f"{type(e).__name__}: {e}")


if __name__ == "__main__":
    sys.exit(main())
