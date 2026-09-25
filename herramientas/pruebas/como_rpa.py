#!/usr/bin/env python3
"""Se autentica como el usuario de aplicación del RPA y responde UNA pregunta:
¿la plataforma lo reconoce, y con qué roles?

    python3 herramientas/pruebas/como_rpa.py

Por qué existe: la puerta de las dos Custom API de fase 2 (diseno/03, D-9) exige
que quien llama sea una identidad de aplicación **y** tenga el rol
`SR - MPPP - RPA`. Esta herramienta ejercita esa condición **desde afuera**, con
las credenciales reales del bot, en vez de darla por buena leyendo la matriz.

Credenciales: `local/rpa_cred.env`, que NO se commitea (`.gitignore`: `/local/`
y `*.env`). Este archivo lo lee, lo usa y **nunca imprime un secreto**: ni el
`client_secret`, ni el token, ni parte de ellos. Si falla la autenticación no se
muestra el detalle del proveedor, porque puede incluir la solicitud de token.
"""
import os
import sys
import urllib.parse
import urllib.request

_AQUI = os.path.dirname(os.path.abspath(__file__))
_RAIZ = os.path.dirname(os.path.dirname(_AQUI))

ROL_ESPERADO = "SR - MPPP - RPA"
CLAVES = ("DATAVERSE_URL", "RPA_TENANT_ID", "RPA_CLIENT_ID", "RPA_CLIENT_SECRET")


def _credenciales():
    ruta = os.path.join(_RAIZ, "local", "rpa_cred.env")
    if not os.path.exists(ruta):
        raise SystemExit(f"falta {ruta}; las credenciales del bot viven en local/ y no se commitean")
    datos = {}
    with open(ruta, encoding="utf-8") as f:
        for linea in f:
            linea = linea.strip()
            if not linea or linea.startswith("#") or "=" not in linea:
                continue
            clave, valor = linea.split("=", 1)
            datos[clave.strip()] = valor.strip().strip('"').strip("'")
    faltan = [c for c in CLAVES if not datos.get(c)]
    if faltan:
        raise SystemExit(f"a local/rpa_cred.env le faltan claves con valor: {faltan}")
    return datos


def _token(cred):
    url = f"https://login.microsoftonline.com/{cred['RPA_TENANT_ID']}/oauth2/v2.0/token"
    cuerpo = urllib.parse.urlencode({
        "grant_type": "client_credentials",
        "client_id": cred["RPA_CLIENT_ID"],
        "client_secret": cred["RPA_CLIENT_SECRET"],
        "scope": cred["DATAVERSE_URL"].rstrip("/") + "/.default",
    }).encode()
    try:
        with urllib.request.urlopen(urllib.request.Request(url, data=cuerpo)) as r:
            import json
            return json.load(r)["access_token"]
    except Exception:
        # A propósito sin el detalle: puede traer partes de la solicitud de token.
        raise SystemExit("no se pudo obtener el token del bot (credenciales, tenant o permisos de la app)")


def _get(cred, token, recurso):
    import json
    url = cred["DATAVERSE_URL"].rstrip("/") + "/api/data/v9.2/" + recurso
    pedido = urllib.request.Request(url, headers={
        "Authorization": "Bearer " + token,
        "Accept": "application/json",
        "OData-Version": "4.0",
    })
    with urllib.request.urlopen(pedido) as r:
        return json.load(r)


def main():
    cred = _credenciales()
    token = _token(cred)
    yo = _get(cred, token, "WhoAmI")
    uid = yo["UserId"]
    usuario = _get(cred, token, f"systemusers({uid})?$select=fullname,applicationid")
    roles = _get(cred, token, f"systemusers({uid})/systemuserroles_association?$select=name")
    nombres = sorted(r["name"] for r in roles.get("value", []))

    es_aplicacion = bool(usuario.get("applicationid"))
    tiene_rol = ROL_ESPERADO in nombres

    print(f"  se autenticó como : {usuario.get('fullname')}")
    print(f"  UserId            : {uid}")
    print(f"  ¿identidad de aplicación? {'SÍ' if es_aplicacion else 'NO'}   (primera condición de D-9)")
    print(f"  ¿tiene {ROL_ESPERADO}?   {'SÍ' if tiene_rol else 'NO'}   (segunda condición de D-9)")
    print(f"  roles             : {nombres}")

    if es_aplicacion and tiene_rol:
        print("\n  La puerta de D-9 lo deja pasar. Las dos condiciones se cumplen DESDE AFUERA.")
        return 0
    print("\n  NO pasa la puerta de D-9. Las dos Custom API de fase 2 lo van a rechazar.")
    return 1


if __name__ == "__main__":
    sys.exit(main())
