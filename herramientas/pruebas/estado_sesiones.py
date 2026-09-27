#!/usr/bin/env python3
"""Estado y guarda de las sesiones de navegador usadas por el arnés de GUI.

Cuatro sesiones viven en `local/sesion-<papel>.json` (`ejecutivo`, `supervisor`,
`admin_planes`, `admin_tecnico`): un `storageState` de Playwright ya autenticado
contra el entorno. Acá **nunca se lee ni se imprime su contenido** — solo se le
pasa el PATH a Playwright, que lo consume solo.

Por qué existe la guarda (`verificar_o_fallar`). El 24/09 le cambiaron el rol a
`User1` para probar otra cosa y dos casos de `comportamiento_roles.py` salieron
en rojo con un `403` que parecía un defecto del producto (ver el comentario de
`ROL_ESPERADO` ahí). En pruebas de seguridad el problema es peor todavía: **una
prueba que pasa porque el usuario no ve algo es indistinguible de una que pasa
porque la sesión está muerta.** Las dos dan verde, y una te está mintiendo. Por
eso todo arnés que dependa de estas sesiones tiene que llamar a
`verificar_o_fallar(...)` ANTES de correr un solo caso, y fallar en seco —nunca
loguear un warning que se puede ignorar— si algo no cuadra.

Reusa `ALIAS` y `ROL_ESPERADO` de `comportamiento_roles.py`: una sola fuente de
verdad para qué alias de login y qué rol de negocio le corresponde a cada papel.

Resuelve Playwright sin duplicar la instalación de Chromium: `pruebas/gui` ya
lo tiene instalado (Node) en el caché por defecto de Playwright
(`~/.cache/ms-playwright`), que es el mismo caché que usa la variante Python.
Este módulo instala solo el paquete `playwright` de Python (liviano) en un
venv dedicado bajo `local/` (no versionado), pidiendo la MISMA versión de
`playwright-core` que ya usa `pruebas/gui/node_modules` — así el Chromium que
ya está bajado le sirve a los dos y no se vuelve a bajar. No escribe nada
dentro de `pruebas/gui/`, solo lee su `package.json` resuelto.

Como comando:
    python3 herramientas/pruebas/estado_sesiones.py
    → un renglón por papel: quién es, si está viva, si el rol es el que toca.
    Sale con 0 si las cuatro están bien, 1 si alguna falla.

Como librería:
    from estado_sesiones import verificar_o_fallar
    verificar_o_fallar(["ejecutivo", "supervisor"])   # revienta si algo no cuadra
"""
import glob
import json
import os
import subprocess
import sys

_AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(os.path.dirname(_AQUI))
LOCAL = os.path.join(RAIZ, "local")
VENV = os.path.join(LOCAL, "venv-estado-sesiones")
NODE_PLAYWRIGHT_CORE_PKG = os.path.join(
    RAIZ, "pruebas", "gui", "node_modules", "playwright-core", "package.json")

WHOAMI_URL = "https://org36e60d9d.crm.dynamics.com/api/data/v9.2/WhoAmI"

sys.path.insert(0, os.path.dirname(_AQUI))  # herramientas/ → dataverse_api
from dataverse_api import Dataverse  # noqa: E402

sys.path.insert(0, _AQUI)  # herramientas/pruebas/ → comportamiento_roles
from comportamiento_roles import ALIAS, ROL_ESPERADO  # noqa: E402

PAPELES = list(ALIAS.keys())


def _version_playwright_de_gui():
    """Versión de `playwright-core` que ya instaló (y descargó) el arnés de
    GUI. Se pide la misma del lado Python para reusar el mismo Chromium."""
    with open(NODE_PLAYWRIGHT_CORE_PKG, encoding="utf-8") as f:
        return json.load(f)["version"]


def _asegurar_playwright():
    """Si el intérprete actual no tiene `playwright`, agrega al `sys.path` el
    site-packages de un venv dedicado en `local/` (creándolo e instalando ahí
    la primera vez). A propósito NO reejecuta el proceso (`os.execv`): este
    módulo también se importa como librería desde otros arneses, y reemplazar
    el proceso del que llama sería un efecto colateral inaceptable."""
    try:
        import playwright.sync_api  # noqa: F401
        return
    except ImportError:
        pass

    if not os.path.exists(VENV):
        version = _version_playwright_de_gui()
        print(f"(primera vez: preparando venv en local/ con playwright=={version} "
              f"— reusa el Chromium que ya bajó pruebas/gui, no baja uno nuevo)",
              file=sys.stderr)
        subprocess.run([sys.executable, "-m", "venv", VENV], check=True)
        pip = os.path.join(VENV, "bin", "pip")
        subprocess.run([pip, "install", "--quiet", "--disable-pip-version-check",
                        f"playwright=={version}"], check=True)

    candidatos = glob.glob(os.path.join(VENV, "lib", "python3.*", "site-packages"))
    if not candidatos:
        raise SystemExit(f"no encuentro site-packages en {VENV}: ¿venv corrupto? Borralo y reintentá.")
    sys.path.insert(0, candidatos[0])
    import playwright.sync_api  # noqa: F401  — si sigue sin poder, que reviente acá y se vea por qué


_asegurar_playwright()
from playwright.sync_api import sync_playwright  # noqa: E402


def _archivo_sesion(papel):
    return os.path.join(LOCAL, f"sesion-{papel}.json")


def _quien_es(dv, user_id):
    """Resuelve fullname/domainname y roles de un UserId con el service principal
    (NUNCA con la sesión de la persona: esa solo sirve para pegarle a WhoAmI)."""
    e, b, _ = dv.call("GET", f"systemusers({user_id})?$select=fullname,domainname")
    if e != 200:
        return None, None, []
    e2, b2, _ = dv.call("GET", f"systemusers({user_id})/systemuserroles_association?$select=name")
    roles = [r["name"] for r in b2.get("value", [])] if e2 == 200 else []
    return b.get("fullname"), b.get("domainname"), roles


def verificar_sesiones(papeles=None):
    """Devuelve una lista de dict, uno por papel:
    {papel, archivo, viva, fullname, domainname, roles, rol_ok, motivo}.

    NO falla y NO imprime nada del contenido de la sesión: el archivo se le
    pasa a Playwright como `storage_state` y ya."""
    papeles = papeles or PAPELES
    desconocidos = [p for p in papeles if p not in ALIAS]
    if desconocidos:
        raise ValueError(f"papel(es) desconocido(s), no están en ALIAS: {desconocidos}")

    dv = Dataverse()
    resultados = []
    with sync_playwright() as p:
        navegador = p.chromium.launch(headless=True)
        try:
            for papel in papeles:
                archivo = _archivo_sesion(papel)
                r = {"papel": papel, "archivo": os.path.basename(archivo), "viva": False,
                     "fullname": None, "domainname": None, "roles": [], "rol_ok": False,
                     "motivo": ""}
                if not os.path.exists(archivo):
                    r["motivo"] = "no existe el archivo de sesión"
                    resultados.append(r)
                    continue

                cuerpo = None
                contexto = navegador.new_context(storage_state=archivo)
                try:
                    pagina = contexto.new_page()
                    resp = pagina.goto(WHOAMI_URL, timeout=15000)
                    if resp and resp.ok:
                        try:
                            cuerpo = json.loads(resp.text())
                        except (json.JSONDecodeError, ValueError):
                            cuerpo = None  # normalmente: la sesión venció y esto es la página de login
                except Exception as ex:
                    r["motivo"] = f"no se pudo navegar: {type(ex).__name__}"
                finally:
                    contexto.close()

                if not cuerpo or "UserId" not in cuerpo:
                    r["motivo"] = r["motivo"] or "WhoAmI no respondió como se esperaba (sesión vencida)"
                    resultados.append(r)
                    continue

                r["viva"] = True
                fullname, domainname, roles = _quien_es(dv, cuerpo["UserId"])
                r["fullname"], r["domainname"], r["roles"] = fullname, domainname, roles

                alias_esperado = ALIAS[papel]
                es_quien_toca = bool(domainname) and domainname.lower().startswith(
                    alias_esperado.lower() + "@")
                rol_esperado = ROL_ESPERADO[papel]
                tiene_rol = rol_esperado in roles
                r["rol_ok"] = es_quien_toca and tiene_rol
                if not es_quien_toca:
                    r["motivo"] = f"la sesión autentica a {domainname!r}, se esperaba {alias_esperado!r}"
                elif not tiene_rol:
                    r["motivo"] = f"tiene {roles}, se esperaba {rol_esperado!r}"
                resultados.append(r)
        finally:
            navegador.close()
    return resultados


def verificar_o_fallar(papeles=None):
    """La guarda. Llamala al arrancar cualquier arnés que dependa de estas
    sesiones. Revienta con `SystemExit` — no devuelve `False`, no loguea un
    warning que se puede ignorar — si alguna sesión está muerta o el rol no
    es el que toca. Ver el docstring del módulo para el motivo."""
    resultados = verificar_sesiones(papeles)
    malos = [r for r in resultados if not r["viva"] or not r["rol_ok"]]
    if malos:
        detalle = "\n".join(f"  - {r['papel']} ({r['archivo']}): {r['motivo']}" for r in malos)
        raise SystemExit(
            "GUARDA DE SESIONES: no se arranca nada.\n" + detalle +
            "\n\nRenová la sesión que falló (o devolvé el rol) antes de reintentar — "
            "ver herramientas/pruebas/comportamiento_roles.py (ROL_ESPERADO) para el motivo "
            "por el que esto frena en seco en vez de seguir.")
    return resultados


def main():
    resultados = verificar_sesiones()
    ancho = max(len(r["papel"]) for r in resultados)
    for r in resultados:
        estado = "VIVA  " if r["viva"] else "MUERTA"
        rol = "rol OK " if r["rol_ok"] else ("ROL MAL" if r["viva"] else "—      ")
        quien = r["domainname"] or "?"
        linea = f"  {r['papel']:<{ancho}}  {estado}  {rol}  {quien}"
        if r["motivo"]:
            linea += f"   ({r['motivo']})"
        print(linea)
    malas = sum(1 for r in resultados if not r["viva"] or not r["rol_ok"])
    print(f"\n{len(resultados)} sesiones · {malas} con problemas")
    return 1 if malas else 0


if __name__ == "__main__":
    sys.exit(main())
