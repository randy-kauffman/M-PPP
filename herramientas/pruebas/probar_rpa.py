#!/usr/bin/env python3
"""Ejercita la fase 2 ENTERA desde afuera, con la identidad real del bot.

    python3 herramientas/pruebas/probar_rpa.py                 # solo mira: pide un lote y lo muestra
    python3 herramientas/pruebas/probar_rpa.py --confirmar     # el ciclo completo, escribe en Dev

**Por qué existe.** Las 967 pruebas de plugins corren en memoria con dobles: prueban la lógica, y
no pueden probar nada de lo que de verdad se rompe al desplegar — que los steps hayan quedado
encendidos, que las Custom API estén bien registradas, que el paquete tenga las clases nuevas, que
los parámetros sembrados tengan sentido. Eso solo se ve llamando a la API de verdad, con el token
del bot, contra el entorno.

`como_rpa.py` llega hasta la puerta y verifica que lo dejan pasar (D-9). Esta entra.

**Credenciales**: `local/rpa_cred.env`, que no se commitea. Se leen, se usan y **nunca se imprimen**:
ni el `client_secret`, ni el token, ni parte de ellos. Si falla la autenticación no se muestra el
detalle del proveedor, porque puede incluir la solicitud de token.

**Esto ESCRIBE en el entorno** con `--confirmar`: pone claims, transiciona filas y prende marcas.
Es para Dev, con datos de prueba. Sin `--confirmar` solo pide un lote — que igual pone el claim,
porque ponerlo es parte de entregar (`03` §2); por eso el claim que usa por defecto es corto.
"""
import json
import os
import sys
import urllib.parse
import urllib.request

_AQUI = os.path.dirname(os.path.abspath(__file__))
_RAIZ = os.path.dirname(os.path.dirname(_AQUI))

CLAVES = ("DATAVERSE_URL", "RPA_TENANT_ID", "RPA_CLIENT_ID", "RPA_CLIENT_SECRET")
OBTENER = "sanic_mppp_capi_obtenerfilaspendientes"
REGISTRAR = "sanic_mppp_capi_registrarresultadorpa"


# ---------------------------------------------------------------------------
# Identidad del bot (mismo criterio que como_rpa.py: nunca se imprime un secreto)
# ---------------------------------------------------------------------------
def _credenciales():
    ruta = os.path.join(_RAIZ, "local", "rpa_cred.env")
    if not os.path.exists(ruta):
        raise SystemExit(f"falta {ruta}; las credenciales del bot viven en local/ y no se commitean")
    datos = {}
    with open(ruta, encoding="utf-8") as f:
        for linea in f:
            linea = linea.strip()
            if linea and not linea.startswith("#") and "=" in linea:
                clave, valor = linea.split("=", 1)
                datos[clave.strip()] = valor.strip().strip('"').strip("'")
    faltan = [c for c in CLAVES if not datos.get(c)]
    if faltan:
        raise SystemExit(f"a local/rpa_cred.env le faltan claves con valor: {faltan}")
    return datos


def _token(cred):
    cuerpo = urllib.parse.urlencode({
        "grant_type": "client_credentials",
        "client_id": cred["RPA_CLIENT_ID"],
        "client_secret": cred["RPA_CLIENT_SECRET"],
        "scope": cred["DATAVERSE_URL"].rstrip("/") + "/.default",
    }).encode()
    url = f"https://login.microsoftonline.com/{cred['RPA_TENANT_ID']}/oauth2/v2.0/token"
    try:
        with urllib.request.urlopen(urllib.request.Request(url, data=cuerpo)) as r:
            return json.load(r)["access_token"]
    except Exception:
        # Sin el detalle a propósito: puede traer partes de la solicitud de token.
        raise SystemExit("no se pudo obtener el token del bot (credenciales, tenant o permisos de la app)")


class Bot(object):
    """Llama al Web API COMO EL BOT. Devuelve (codigo, cuerpo) y nunca lanza por un 4xx:
    un rechazo del servidor es un RESULTADO de esta prueba, no un accidente."""

    def __init__(self):
        self.cred = _credenciales()
        self.token = _token(self.cred)
        self.base = self.cred["DATAVERSE_URL"].rstrip("/") + "/api/data/v9.2/"

    def llamar(self, recurso, metodo="GET", cuerpo=None):
        datos = json.dumps(cuerpo).encode() if cuerpo is not None else None
        pedido = urllib.request.Request(self.base + recurso, data=datos, method=metodo)
        pedido.add_header("Authorization", "Bearer " + self.token)
        pedido.add_header("Accept", "application/json")
        pedido.add_header("OData-Version", "4.0")
        if datos is not None:
            pedido.add_header("Content-Type", "application/json")
        try:
            with urllib.request.urlopen(pedido) as r:
                crudo = r.read().decode("utf-8") or "{}"
                return r.status, json.loads(crudo)
        except urllib.error.HTTPError as e:
            crudo = e.read().decode("utf-8") or "{}"
            try:
                return e.code, json.loads(crudo)
            except ValueError:
                return e.code, {"error": {"message": crudo[:300]}}


def _mensaje_de_error(cuerpo):
    error = cuerpo.get("error") if isinstance(cuerpo, dict) else None
    if isinstance(error, dict):
        return error.get("message", "")
    return str(cuerpo)[:300]


# ---------------------------------------------------------------------------
# Los pasos
# ---------------------------------------------------------------------------
def pedir_lote(bot, maximo, claimminutos):
    """`obtenerfilaspendientes`. Es una Function: los parámetros van en la URL."""
    recurso = f"{OBTENER}(maximo={maximo},claimminutos={claimminutos})"
    codigo, cuerpo = bot.llamar(recurso)
    if codigo >= 400:
        print(f"  ✗ HTTP {codigo}: {_mensaje_de_error(cuerpo)}")
        return None
    filas = json.loads(cuerpo.get("filas") or "[]")
    print(f"  entregadas = {cuerpo.get('entregadas')} · pendientes = {cuerpo.get('pendientes')}")
    return filas


def mostrar_fila(fila):
    print(f"    {fila.get('solicitud')}-F{fila.get('numerofila'):02d}  {fila.get('estado'):<9}"
          f" plan {fila.get('plan')} · {fila.get('tipoformato')} · {fila.get('gestion')}"
          f" · {fila.get('banco')}/{fila.get('bancocodigo')} · {fila.get('moneda')}")
    print(f"       referencia={fila.get('referencia')!r}  recibida={fila.get('referenciarecibida')!r}"
          f"  validada={fila.get('fechavalidada')}")


def revisar_contrato(fila):
    """Lo que el contrato promete y acá se puede comprobar de verdad (`03` §2)."""
    problemas = []
    faltan = [c for c in (
        "filaid", "solicitud", "numerofila", "estado", "plan", "tipoformato", "gestion",
        "clasificacion", "nombrebeneficiario", "tipoidentificacion", "numeroidentificacion",
        "numerocuenta", "moneda", "banco", "bancocodigo", "referencia", "referenciarecibida",
        "fechavalidada") if c not in fila]
    if faltan:
        problemas.append(f"faltan campos: {faltan}")
    if fila.get("estado") not in ("validada", "digitada"):
        problemas.append(f"estado {fila.get('estado')!r} no es 'validada' ni 'digitada'")
    if not str(fila.get("fechavalidada", "")).endswith("Z"):
        problemas.append("fechavalidada sin offset explícito")
    for campo, valor in fila.items():
        if valor is None:
            problemas.append(f"{campo} vino null (el contrato dice cadena vacía)")
    return problemas


def registrar(bot, items):
    """`registrarresultadorpa`. Una Action: el lote va en el cuerpo, como un String."""
    codigo, cuerpo = bot.llamar(REGISTRAR, "POST", {"resultados": json.dumps(items)})
    if codigo >= 400:
        print(f"  ✗ la llamada ENTERA fue rechazada — HTTP {codigo}: {_mensaje_de_error(cuerpo)}")
        return None
    print(f"  procesadas = {cuerpo.get('procesadas')} · conerror = {cuerpo.get('conerror')}")
    for r in json.loads(cuerpo.get("resultados") or "[]"):
        marca = "✗" if r.get("error") else "·"
        print(f"    {marca} {r.get('filaid')}  estadofila={r.get('estadofila')!r}"
              f" yaregistrado={r.get('yaregistrado')}  {r.get('error') or ''}")
    return cuerpo


def ahora_utc():
    import datetime
    return datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


# ---------------------------------------------------------------------------
def main(argv):
    confirmar = "--confirmar" in argv
    maximo = 5
    claimminutos = 10
    for a in argv:
        if a.startswith("--maximo="):
            maximo = int(a.split("=", 1)[1])
        if a.startswith("--claimminutos="):
            claimminutos = int(a.split("=", 1)[1])

    bot = Bot()
    _, yo = bot.llamar("WhoAmI")
    print(f"Llamando como el usuario de aplicación {yo.get('UserId')}\n")

    print(f"1. obtenerfilaspendientes(maximo={maximo}, claimminutos={claimminutos})")
    filas = pedir_lote(bot, maximo, claimminutos)
    if filas is None:
        return 1
    if not filas:
        print("  (no hay filas pendientes: sembrá una solicitud con "
              "herramientas/pruebas/generar_excel.py y mandala al buzón)")
        return 0
    for f in filas:
        mostrar_fila(f)
    problemas = []
    for f in filas:
        problemas.extend(revisar_contrato(f))
    print("  contrato del JSON:", "OK" if not problemas else "PROBLEMAS -> " + "; ".join(sorted(set(problemas))))

    if not confirmar:
        print("\n(solo lectura: el claim quedó puesto porque ponerlo es parte de entregar. "
              "Para el ciclo completo, --confirmar)")
        return 1 if problemas else 0

    validadas = [f for f in filas if f.get("estado") == "validada"]
    if not validadas:
        print("\n(no vino ninguna Validada; el resto del ciclo necesita al menos una)")
        return 0
    uno = validadas[0]

    print(f"\n2. El claim tiene que frenar a una PERSONA sobre {uno['filaid']}")
    print("   Corré en otra terminal, con un usuario humano:")
    print(f"     python3 herramientas/pruebas/actuar_fila.py {uno['filaid']} digitada")
    print("   Esperado: rechazo que diga 'se libera en N minutos'. NO 'no autorizado'.")

    print("\n3. registrarresultadorpa — lote con una buena, una rechazada y un código inexistente")
    # UNA operación por fila, y filas DISTINTAS. Dos transiciones sobre la MISMA fila dentro del mismo
    # lote no es un caso real —el bot digita y después aprueba en llamadas separadas, `03` §3— y encima
    # rompe la llamada entera. Hasta el 2026-09-26 esta prueba lo hacía, y el fallo se leyó tres veces
    # como un defecto del sistema cuando era del andamio.
    lote = [{"filaid": uno["filaid"], "resultado": "digitada", "codigo": "OK", "fechaproceso": ahora_utc()}]
    if len(validadas) > 1:
        lote.append({"filaid": validadas[1]["filaid"], "resultado": "rechazada",
                     "codigo": "NO-EXISTE-ESTE-CODIGO", "mensaje": "PRUEBA: condicion inventada",
                     "fechaproceso": ahora_utc()})
    if len(validadas) > 2:
        lote.append({"filaid": validadas[2]["filaid"], "resultado": "rechazada", "codigo": "RECH-GEN",
                     "mensaje": "PRUEBA: AS400 no acepto", "fechaproceso": ahora_utc()})
    registrar(bot, lote)
    print("   Esperado: la del código inexistente da MPPP-RPA-005 y NO arrastra a las otras.")

    print("\n4. obtenerfilaspendientes de nuevo")
    otra = pedir_lote(bot, maximo, claimminutos)
    if otra is not None:
        digitadas = [f for f in otra if f.get("estado") == "digitada"]
        print(f"   Digitadas devueltas: {len(digitadas)}")
        print("   Con rpa.puedeaprobar = si (como está en Dev), las Digitadas por el propio bot vuelven")
        print("   para que las apruebe. Si el parámetro está en 'no', esta lista tiene que venir vacía.")

    print("\n5. atencionmanual — la marca prende y el claim se libera")
    registrar(bot, [{"filaid": uno["filaid"], "resultado": "atencionmanual", "codigo": "RECH-GEN",
                     "mensaje": "PRUEBA: el bot no supo resolverla", "fechaproceso": ahora_utc()}])
    print("   Esperado: la fila desaparece del lote y aparece en la vista 'Para atención manual'.")
    print("\n   Ojo con el texto que le llega al CLIENTE en la fila rechazada: sale de rpa.codigos,")
    print("   y hoy el de RECH-GEN es PROVISORIO (D-54). Miralo y decidí si es el que querés.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
