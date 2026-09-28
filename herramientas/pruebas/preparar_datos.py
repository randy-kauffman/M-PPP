#!/usr/bin/env python3
"""Crea (o verifica) los datos de negocio que necesitan los casos de prueba
funcionales, y los borra cuando se termina.

    python3 herramientas/pruebas/preparar_datos.py --remitente <correo>
    python3 herramientas/pruebas/preparar_datos.py --verificar
    python3 herramientas/pruebas/preparar_datos.py --limpiar

`--remitente` es la dirección DESDE la que se van a mandar los correos de
prueba. Tiene que ser una casilla real a la que tengas acceso, porque varios
casos se comprueban leyendo la respuesta que llega a ese buzón.

**El incidente del 2026-09-27 y el rediseño del 2026-09-28.** Un `--limpiar`
de esta herramienta borró autorizaciones REALES del aprobador. La causa: el
borrado de `AutorizacionPlan` se armaba con un `GET` de TODA la tabla y un
`in` de Python contra `sanic_nombre` ("¿contiene `→ PR11`?") — cualquier fila
real cuyo texto coincidiera por casualidad caía, sin que nadie releyera el
registro ni comprobara que de verdad era del fixture. Tres cambios, aprobados
por el aprobador ese mismo día:

1. **La marca de fixture es `ZZP-`** (`marca_agua.py`), no `PR`. Vive en
   `sanic_nombre` del Cliente (`ZZP-Pruebas MPPP SA`), el único campo de este
   fixture que es texto libre y no lo recalcula ningún plugin. El código
   corto de cada Plan (`sanic_codigo`, `T(4)`) **sigue siendo `PR11`/`PR06`/
   `PR10`/`PRIN`/`PRNA`, sin marca**: la columna no tiene los 4 caracteres de
   `ZZP-` MÁS algo que distinga un plan de otro, y de cualquier forma no es
   lo que el limpiador verifica (identifica el caso de prueba, nada más —
   ver el "Aviso" al final de `marca_agua.py`). Ni el `sanic_nombre` de Plan
   ni el de `AutorizacionPlan` pueden llevar `ZZP-` tampoco: los dos son
   **calculados por un plugin PreOperation** que los sobrescribe siempre
   (`playbooks/tabla/sanic_mppp_tbl_plan.md`, `…autorizacionplan.md`, §1) con
   un formato que empieza por el código o por el correo, nunca por la marca.
   Por eso `limpiar()` (abajo) no llama a `marca_agua.borrar_marcado()` para
   Plan ni para AutorizacionPlan: usa la protección EQUIVALENTE — releer el
   registro puntual justo antes de cada `DELETE` y exigir que sus lookups
   (`_sanic_clienteid_value`, `_sanic_autorizadoid_value`,
   `_sanic_planid_value`) sigan apuntando, exactamente, al Cliente/Plan/
   Autorizado de ESTE fixture, en vez de un prefijo de texto. Es la misma
   garantía de `marca_agua` (releer inmediatamente antes de borrar, abortar
   si no se puede probar pertenencia), aplicada por relación porque el
   texto no tiene dónde vivir.
2. **`limpiar()` ya no arma listas por `GET` sin filtro + coincidencia de
   nombre.** Cada búsqueda usa un `$filter` exacto (código de plan, correo
   del remitente, o los dos lookups de la `AutorizacionPlan`), y cada
   `DELETE` relee el registro puntual inmediatamente antes.
3. **`--limpiar` y la creación (`--remitente`) van adentro de
   `semaforo.tomar(...)`**: si alguien está probando a mano (semáforo en
   rojo), esta herramienta ya no puede pisarlo. `--verificar` es de solo
   lectura y no toma el semáforo.

**Qué crea:**

| Qué | Para qué caso |
|---|---|
| Cliente `ZZP-Pruebas MPPP SA` | dueño de todos los planes de prueba |
| Plan `PR11` formato 11, COR | el camino feliz, y las reglas de formato 11 |
| Plan `PR06` formato 06, COR | que la referencia NO se derive (DD-15) |
| Plan `PR10` formato 10, USD | `MONEDA_DEL_PLAN` |
| Plan `PRIN` formato 06, COR, **inactivo** | `PLAN_EXISTE` con un plan que existe pero no vale |
| Plan `PRNA` formato 06, COR | `AUTORIZACION_CORREO_PLAN`: existe, pero el remitente no está autorizado |
| Autorizado = el remitente | `REMITENTE_RECONOCIDO` (su `sanic_nombre` ES el correo real: no lleva `ZZP-`, ver `limpiar()`) |
| Autorizaciones a PR11, PR06, PR10 y PRIN | **a PRNA NO**, a propósito |

Última línea: JSON de una línea, igual que las herramientas de construcción.
"""
import json
import os
import re
import sys

_AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(os.path.dirname(_AQUI))
sys.path.insert(0, os.path.join(RAIZ, "herramientas"))
sys.path.insert(0, _AQUI)

import marca_agua  # noqa: E402
import semaforo  # noqa: E402

CLIENTE_BASE = "Pruebas MPPP SA"
CLIENTE = marca_agua.nombre_de_prueba(CLIENTE_BASE)  # "ZZP-Pruebas MPPP SA"
CIF_BAC = "900100200"      # `sanic_cifbac` admite 9, y usa los 9
# `sanic_cifcom` exige EXACTAMENTE 12 caracteres, con una forma concreta que
# valida Dataverse (no solo el largo): 9 alfanuméricos o espacio + 3 dígitos.
# Mensaje real de la plataforma: "El CIF COM tiene que tener 12 caracteres: 9
# alfanuméricos o espacio y 3 dígitos." "PRUEBASMPPP" (11, todo letras) no
# cumplía ni el largo ni la forma — quedó dormido porque el Cliente del
# fixture ya existía en Dev, y saltó recién al migrar la marca de agua a
# `ZZP-` (commit d8fb94e), cuando hubo que crearlo por primera vez.
CIF_COM = "PRUEBASMP001"   # 9 alfanuméricos ("PRUEBASMP") + 3 dígitos ("001")
CIF_COM_FORMA = re.compile(r"^[A-Za-z0-9 ]{9}[0-9]{3}$")

# Largos máximos reales, leídos de la metadata el 2026-09-22. Se comprueban
# ANTES de tocar la red: un 400 de la plataforma a mitad de la preparación deja
# datos a medias y obliga a limpiar a mano.
LARGOS = {"CLIENTE": (CLIENTE, 200), "CIF_BAC": (CIF_BAC, 9), "CIF_COM": (CIF_COM, 12)}

FORMATO = {"06": 159460001, "10": 159460002, "11": 159460003}
MONEDA = {"COR": 159460001, "USD": 159460002}

PLANES = [
    {"codigo": "PR11", "formato": "11", "moneda": "COR", "activo": True,  "autorizado": True},
    {"codigo": "PR06", "formato": "06", "moneda": "COR", "activo": True,  "autorizado": True},
    {"codigo": "PR10", "formato": "10", "moneda": "USD", "activo": True,  "autorizado": True},
    {"codigo": "PRIN", "formato": "06", "moneda": "COR", "activo": False, "autorizado": True},
    {"codigo": "PRNA", "formato": "06", "moneda": "COR", "activo": True,  "autorizado": False},
]


def salida(estado, detalle):
    print(json.dumps({"estado": estado, "componente": "datos de prueba", "detalle": detalle},
                     ensure_ascii=False))
    return 0 if estado in ("creado", "ya_existia", "limpio") else 1


def uno(dv, conjunto, filtro, select="*"):
    sel = "" if select == "*" else f"$select={select}&"
    est, r, _ = dv.call("GET", f"{conjunto}?{sel}$filter={filtro}")
    v = r.get("value") or []
    return v[0] if v else None


def crear(dv, conjunto, cuerpo, sol="sanic_mppp_sol_mantenimientoppp"):
    from dataverse_api import Dataverse

    est, r, cab = dv.call("POST", conjunto, cuerpo)
    if est not in (200, 201, 204):
        raise RuntimeError(f"POST {conjunto} devolvió HTTP {est}: {str(r)[:220]}")
    return Dataverse.id_creado(cab)


def comprobar_largos(remitente):
    """Sin red. Un largo (o una forma) pasada se ve acá y no a mitad de la
    preparación con un HTTP 400 de la plataforma. El largo solo no alcanza:
    `sanic_cifcom` exige además una forma concreta (9 alfanuméricos o espacio
    + 3 dígitos) que un CIF_COM del largo correcto igual puede incumplir."""
    malos = [f"{k}={v!r} usa {len(v)} y el máximo es {n}" for k, (v, n) in LARGOS.items() if len(v) > n]
    if not CIF_COM_FORMA.match(CIF_COM):
        malos.append(
            f"CIF_COM={CIF_COM!r} no cumple la forma que exige Dataverse: 9 alfanuméricos "
            "o espacio + 3 dígitos (\"El CIF COM tiene que tener 12 caracteres: 9 "
            "alfanuméricos o espacio y 3 dígitos.\")")
    if len(remitente) > 320:
        malos.append(f"el remitente usa {len(remitente)} y el máximo es 320")
    for p in PLANES:
        if len(p["codigo"]) > 4:
            malos.append(f"el código de plan {p['codigo']!r} usa {len(p['codigo'])} y el máximo es 4")
        if len(f"{p['codigo']} - {CLIENTE}") > 100:
            malos.append(f"el nombre del plan {p['codigo']} pasa de 100")
        if len(f"{remitente} → {p['codigo']}") > 400:
            malos.append(f"el nombre de la autorización de {p['codigo']} pasa de 400")
    return malos


def preparar(dv, remitente):
    hechos = []

    cli = uno(dv, "sanic_mppp_tbl_clientes", f"sanic_nombre eq '{CLIENTE}'",
              "sanic_mppp_tbl_clienteid,sanic_nombre")
    if cli is None:
        idc = crear(dv, "sanic_mppp_tbl_clientes",
                    {"sanic_nombre": CLIENTE, "sanic_cifbac": CIF_BAC, "sanic_cifcom": CIF_COM})
        hechos.append(f"cliente {CLIENTE}")
    else:
        idc = cli["sanic_mppp_tbl_clienteid"]

    ids_plan = {}
    for p in PLANES:
        nombre = f"{p['codigo']} - {CLIENTE}"
        pl = uno(dv, "sanic_mppp_tbl_plans", f"sanic_codigo eq '{p['codigo']}'",
                 "sanic_mppp_tbl_planid,sanic_codigo,statecode")
        if pl is None:
            idp = crear(dv, "sanic_mppp_tbl_plans", {
                "sanic_nombre": nombre, "sanic_codigo": p["codigo"],
                "sanic_tipoformato": FORMATO[p["formato"]], "sanic_moneda": MONEDA[p["moneda"]],
                "sanic_clienteid@odata.bind": f"/sanic_mppp_tbl_clientes({idc})"})
            hechos.append(f"plan {p['codigo']} (formato {p['formato']}, {p['moneda']})")
        else:
            idp = pl["sanic_mppp_tbl_planid"]
        ids_plan[p["codigo"]] = idp
        # El plan inactivo se desactiva DESPUÉS de crear su autorización, más abajo.

    aut = uno(dv, "sanic_mppp_tbl_autorizados", f"sanic_nombre eq '{remitente}'",
              "sanic_mppp_tbl_autorizadoid,sanic_nombre")
    if aut is None:
        ida = crear(dv, "sanic_mppp_tbl_autorizados", {"sanic_nombre": remitente})
        hechos.append(f"autorizado {remitente}")
    else:
        ida = aut["sanic_mppp_tbl_autorizadoid"]

    for p in PLANES:
        if not p["autorizado"]:
            continue
        nombre = f"{remitente} → {p['codigo']}"
        ap = uno(dv, "sanic_mppp_tbl_autorizacionplans", f"sanic_nombre eq '{nombre}'",
                 "sanic_mppp_tbl_autorizacionplanid")
        if ap is None:
            crear(dv, "sanic_mppp_tbl_autorizacionplans", {
                "sanic_nombre": nombre,
                "sanic_autorizadoid@odata.bind": f"/sanic_mppp_tbl_autorizados({ida})",
                "sanic_planid@odata.bind": f"/sanic_mppp_tbl_plans({ids_plan[p['codigo']]})"})
            hechos.append(f"autorizacion {nombre}")

    # Recién ahora se desactiva el plan inactivo: su autorización ya existe, así
    # el caso prueba "el plan no vale", no "falta la autorización".
    for p in PLANES:
        if p["activo"]:
            continue
        est, _, _ = dv.call("PATCH", f"sanic_mppp_tbl_plans({ids_plan[p['codigo']]})",
                            {"statecode": 1, "statuscode": 2})
        if est in (200, 204):
            hechos.append(f"plan {p['codigo']} DESACTIVADO a propósito")

    return hechos


def verificar(dv, remitente=None):
    faltan, bien = [], []
    cli = uno(dv, "sanic_mppp_tbl_clientes", f"sanic_nombre eq '{CLIENTE}'", "sanic_nombre")
    (bien if cli else faltan).append(f"cliente {CLIENTE}")
    for p in PLANES:
        pl = uno(dv, "sanic_mppp_tbl_plans", f"sanic_codigo eq '{p['codigo']}'",
                 "sanic_codigo,statecode,sanic_tipoformato,sanic_moneda")
        if pl is None:
            faltan.append(f"plan {p['codigo']}")
            continue
        activo_ok = (pl["statecode"] == 0) == p["activo"]
        formato_ok = pl["sanic_tipoformato"] == FORMATO[p["formato"]]
        moneda_ok = pl["sanic_moneda"] == MONEDA[p["moneda"]]
        if activo_ok and formato_ok and moneda_ok:
            bien.append(f"plan {p['codigo']}")
        else:
            faltan.append(f"plan {p['codigo']} mal configurado "
                          f"(activo={activo_ok}, formato={formato_ok}, moneda={moneda_ok})")
    if remitente:
        aut = uno(dv, "sanic_mppp_tbl_autorizados", f"sanic_nombre eq '{remitente}'", "sanic_nombre")
        (bien if aut else faltan).append(f"autorizado {remitente}")
        for p in PLANES:
            nombre = f"{remitente} → {p['codigo']}"
            ap = uno(dv, "sanic_mppp_tbl_autorizacionplans", f"sanic_nombre eq '{nombre}'", "sanic_nombre")
            existe = ap is not None
            if existe == p["autorizado"]:
                bien.append(f"autorizacion {p['codigo']}: {'existe' if existe else 'NO existe (correcto)'}")
            else:
                faltan.append(f"autorizacion {p['codigo']}: se esperaba "
                              f"{'que existiera' if p['autorizado'] else 'que NO existiera'}")
    return bien, faltan


def limpiar(dv, remitente=None):
    """Borra el fixture `PR*`/`ZZP-` — nunca por coincidencia de nombre (ver
    el docstring del módulo, "El incidente del 2026-09-27"). Orden inverso a
    las dependencias: autorizaciones, autorizado, planes, cliente. Devuelve
    `(borrados, ignorados)`: `ignorados` son registros que EXISTEN con el
    código/correo esperado pero que, releídos justo antes de decidir, no se
    pudo probar que son de este fixture — no se tocan, y quedan reportados en
    vez de desaparecer en silencio.

    Cómo se prueba pertenencia, entidad por entidad (no todas pueden llevar
    el prefijo `ZZP-` de `marca_agua.borrar_marcado()` — ver el docstring del
    módulo):
    - **Cliente**: `sanic_nombre` es texto libre, nadie lo recalcula. Se
      borra con `marca_agua.borrar_marcado()`, releído justo antes.
    - **Plan**: su `sanic_nombre` lo recalcula un plugin (`<codigo> -
      <cliente>`) y nunca puede empezar con `ZZP-`; su `sanic_codigo` (4
      caracteres) no tiene lugar para la marca. La protección equivalente:
      releer el Plan puntual justo antes del `DELETE` y exigir que su
      `_sanic_clienteid_value` siga siendo, exactamente, el GUID del
      Cliente marcado.
    - **AutorizacionPlan**: su `sanic_nombre` también lo recalcula un
      plugin (`<correo> → <código>`) y tampoco puede llevar la marca. Se
      busca por los DOS lookups exactos (Autorizado × Plan, los dos ya
      verificados como propios de este fixture) y, justo antes del
      `DELETE`, se relee esa fila puntual por su GUID y se reconfirma que
      los dos lookups siguen siendo los esperados — nunca por `sanic_nombre`
      ni por un `GET` de toda la tabla.
    - **Autorizado (el remitente)**: su `sanic_nombre` ES el correo real de
      quien corre las pruebas (BP-PP-192): no se le puede poner `ZZP-` sin
      romper el dato. Se busca por `$filter` EXACTO sobre ese correo (nunca
      por substring) y se borra por el GUID que devolvió esa misma
      búsqueda — mismo criterio ya usado para `REMITENTE_MODOCLIENTE` en
      `herramientas/pruebas/sets/set_r.py::cerrar()`.
    """
    borrados, ignorados = [], []

    cli = uno(dv, "sanic_mppp_tbl_clientes", f"sanic_nombre eq '{CLIENTE}'",
              "sanic_mppp_tbl_clienteid,sanic_nombre")
    cliente_id = cli["sanic_mppp_tbl_clienteid"] if cli else None

    # Planes que EXISTEN con el código esperado y cuelgan del Cliente marcado
    # (verificado ahora; se vuelve a releer justo antes de cada DELETE más
    # abajo, no se reusa esta lectura para borrar).
    ids_plan = {}
    for p in PLANES:
        pl = uno(dv, "sanic_mppp_tbl_plans", f"sanic_codigo eq '{p['codigo']}'",
                 "sanic_mppp_tbl_planid,_sanic_clienteid_value")
        if pl is None:
            continue
        if cliente_id is None or pl.get("_sanic_clienteid_value") != cliente_id:
            ignorados.append(f"plan {p['codigo']} existe pero no cuelga del Cliente marcado "
                             f"({CLIENTE!r}); no se borra (¿código chocó con un plan real?)")
            continue
        ids_plan[p["codigo"]] = pl["sanic_mppp_tbl_planid"]

    aut = uno(dv, "sanic_mppp_tbl_autorizados", f"sanic_nombre eq '{remitente}'",
              "sanic_mppp_tbl_autorizadoid") if remitente else None
    autorizado_id = aut["sanic_mppp_tbl_autorizadoid"] if aut else None

    # AutorizacionPlan: por los dos lookups exactos, nunca por nombre.
    if autorizado_id:
        for codigo, plan_id in ids_plan.items():
            ap = uno(dv, "sanic_mppp_tbl_autorizacionplans",
                     f"_sanic_autorizadoid_value eq {autorizado_id} and _sanic_planid_value eq {plan_id}",
                     "sanic_mppp_tbl_autorizacionplanid")
            if ap is None:
                continue
            ap_id = ap["sanic_mppp_tbl_autorizacionplanid"]
            # Releer INMEDIATAMENTE antes del DELETE, no la fila de arriba.
            fresca = uno(dv, "sanic_mppp_tbl_autorizacionplans",
                        f"sanic_mppp_tbl_autorizacionplanid eq {ap_id}",
                        "_sanic_autorizadoid_value,_sanic_planid_value,sanic_nombre")
            if (fresca is None or fresca.get("_sanic_autorizadoid_value") != autorizado_id
                    or fresca.get("_sanic_planid_value") != plan_id):
                ignorados.append(f"autorizacion {codigo}: cambió entre el listado y el borrado; no se toca")
                continue
            est, r, _ = dv.call("DELETE", f"sanic_mppp_tbl_autorizacionplans({ap_id})")
            if est in (200, 204):
                borrados.append(fresca.get("sanic_nombre") or f"autorizacion {codigo}")
            else:
                ignorados.append(f"autorizacion {codigo}: DELETE devolvió HTTP {est}: {r}")

    # Autorizado (remitente): match exacto por correo, borrado por GUID
    # directo (no puede llevar `ZZP-`, ver el docstring de esta función).
    if aut:
        est, r, _ = dv.call("DELETE", f"sanic_mppp_tbl_autorizados({autorizado_id})")
        if est in (200, 204):
            borrados.append(remitente)
        else:
            ignorados.append(f"autorizado {remitente}: DELETE devolvió HTTP {est}: {r}")

    # Plan: releer justo antes y reconfirmar que sigue colgando del Cliente marcado.
    for p in PLANES:
        plan_id = ids_plan.get(p["codigo"])
        if plan_id is None:
            continue
        fresca = uno(dv, "sanic_mppp_tbl_plans", f"sanic_mppp_tbl_planid eq {plan_id}",
                     "_sanic_clienteid_value")
        if fresca is None or cliente_id is None or fresca.get("_sanic_clienteid_value") != cliente_id:
            ignorados.append(f"plan {p['codigo']}: ya no cuelga del Cliente marcado justo antes de "
                             "borrar; no se toca")
            continue
        est, r, _ = dv.call("DELETE", f"sanic_mppp_tbl_plans({plan_id})")
        if est in (200, 204):
            borrados.append(p["codigo"])
        else:
            ignorados.append(f"plan {p['codigo']}: DELETE devolvió HTTP {est}: {r}")

    # Cliente: último, con la marca de agua estándar (releída justo antes).
    if cli:
        ok, motivo = marca_agua.borrar_marcado(dv, "sanic_mppp_tbl_clientes", cliente_id,
                                               "sanic_nombre")
        if ok:
            borrados.append(CLIENTE)
        else:
            ignorados.append(f"cliente {CLIENTE}: {motivo}")

    return borrados, ignorados


def main():
    argv = sys.argv[1:]
    remitente = next((a.split("=", 1)[1] for a in argv if a.startswith("--remitente=")), None)
    if remitente is None and "--remitente" in argv:
        i = argv.index("--remitente")
        remitente = argv[i + 1] if i + 1 < len(argv) else None

    from dataverse_api import Dataverse

    dv = Dataverse()
    try:
        if "--limpiar" in argv:
            # Bajo semáforo: si alguien está probando a mano, esto no arranca
            # (ver "El incidente del 2026-09-27" en el docstring del módulo).
            with semaforo.tomar(f"preparar_datos.py --limpiar{f' --remitente {remitente}' if remitente else ''}"):
                borrados, ignorados = limpiar(dv, remitente)
            detalle = f"borrados {len(borrados)}: {borrados}" if borrados else "no había nada que borrar"
            if ignorados:
                detalle += f"; IGNORADOS (no se pudo probar pertenencia, no se tocaron): {ignorados}"
            return salida("limpio", detalle)
        if "--verificar" in argv:
            # Solo lectura: no toma el semáforo.
            bien, faltan = verificar(dv, remitente)
            if faltan:
                return salida("difiere", f"faltan o están mal: {faltan}")
            return salida("ya_existia", f"{len(bien)} comprobaciones en verde")
        if not remitente or "@" not in remitente:
            return salida("error", "hace falta --remitente <correo desde el que vas a enviar las pruebas>")
        malos = comprobar_largos(remitente)
        if malos:
            return salida("error", "hay valores más largos de lo que admite la tabla: " + "; ".join(malos))
        # Bajo semáforo también la creación: escribe en el entorno compartido.
        with semaforo.tomar(f"preparar_datos.py --remitente {remitente}"):
            hechos = preparar(dv, remitente)
        bien, faltan = verificar(dv, remitente)
        if faltan:
            return salida("error", f"se escribió pero no quedó bien: {faltan}")
        return salida("creado" if hechos else "ya_existia",
                      (f"{len(hechos)} altas: {hechos}; " if hechos else "ya estaba todo; ")
                      + f"{len(bien)} comprobaciones en verde")
    except Exception as e:
        return salida("error", f"{type(e).__name__}: {e}")


if __name__ == "__main__":
    sys.exit(main())
