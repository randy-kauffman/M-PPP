"""Pruebas de `herramientas/construir/paquete_plugins.py`. Nunca tocan la red:
el cliente de Dataverse es `ClienteSimulado`, y el .nupkg es un archivo de
mentira en un directorio temporal.
"""
import base64
import copy
import io
import json
import os
import sys
import tempfile
import unittest
import zipfile

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _AQUI)
sys.path.insert(0, os.path.dirname(_AQUI))

import paquete_plugins as pp  # noqa: E402
from cliente_simulado import ClienteSimulado, FabricaCentinela  # noqa: E402
from fixtures import (  # noqa: E402
    IDENTIDAD_VALIDA,
    SOLUTION_ID,
    armar_cliente_precondiciones_ok,
    es_ruta_solutioncomponents,
)

IDENT = dict(IDENTIDAD_VALIDA, tipo_playbook="paquete-plugins", inventario="8.0")

COMPONENTE = {
    "tipo": "paquete-plugins",
    "nombre": "sanic_SanicMpppPlugins",
    "uniquename": "sanic_SanicMpppPlugins",
    "version": "1.0.0",
    "nupkg": "src/Sanic.Mppp.Plugins.Paquete/bin/Release/SanicMpppPlugins.1.0.0.nupkg",
    "tipos": [
        "Sanic.Mppp.Plugins.Api.ClasificarCorreoApi",
        "Sanic.Mppp.Plugins.Api.ValidarSolicitudApi",
        "Sanic.Mppp.Plugins.Steps.ListaBlancaStep",
    ],
}

PAQUETE_ID = "33333333-3333-3333-3333-333333333333"
ASM_ID = "44444444-4444-4444-4444-444444444444"


def _dll_de_mentira(tipos):
    """Bytes de un ".dll" de juguete que "trae" esos tipos: el nombre de clase
    (el último tramo del tipo completo) aparece como cadena terminada en NUL,
    igual que el heap #Strings de la metadata .NET — sin necesidad de un
    ensamblado real para probar la verificación por contenido (D-57 b)."""
    cuerpo = b"esto hace de cabecera de un PE de mentira"
    for t in tipos:
        clase = t.rsplit(".", 1)[-1]
        cuerpo += clase.encode("utf-8") + b"\x00"
    return cuerpo


def _nupkg_de_mentira(tipos, nombre_dll="Sanic.Mppp.Plugins.dll", con_dll=True):
    """Un .nupkg de verdad (es un .zip) con un .dll de mentira adentro, para
    poder probar que la verificación abre el paquete y mira el contenido del
    ensamblado en vez de confiar en la fecha del archivo."""
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w") as z:
        if con_dll:
            z.writestr(f"lib/net462/{nombre_dll}", _dll_de_mentira(tipos))
        z.writestr("SanicMpppPlugins.nuspec", "<package></package>")
    return buf.getvalue()


# Hasta el 2026-09-25 esto era `b"PK\x03\x04 esto hace de .nupkg"`: un relleno
# que alcanzaba porque NADIE lo leía. D-57(b) (2026-09-26) hizo que
# `paquete_plugins.py` abra el .nupkg como el .zip que es y mire el contenido
# del .dll de adentro, así que el relleno dejó de alcanzar. Ahora es un .zip
# VÁLIDO de verdad, con un `lib/net462/Sanic.Mppp.Plugins.dll` que trae los
# tipos de `COMPONENTE["tipos"]` — así todas las pruebas que ya subían este
# fixture (alta, HTTP inesperado, etc.) siguen andando sin que nadie tenga que
# enterarse de la verificación nueva. Si esto vuelve a ser bytes cualquiera,
# las pruebas de `ContenidoDelNupkg` quedan de decorado: dejan de probar nada
# porque el .nupkg real (con datos completos) sería indistinguible del roto.
BYTES_NUPKG = _nupkg_de_mentira(COMPONENTE["tipos"])


def comp(**cambios):
    d = copy.deepcopy(COMPONENTE)
    d.update(cambios)
    return d


def playbook(componente=None, identidad=None):
    componente = COMPONENTE if componente is None else componente
    identidad = IDENT if identidad is None else identidad
    return (
        "# Playbook: paquete-plugins · prueba\n\n"
        "## 1. Identidad\n\n```json\n" + json.dumps(identidad, ensure_ascii=False, indent=2) + "\n```\n\n"
        "## 2. Qué se crea\n\n```json\n" + json.dumps(componente, ensure_ascii=False, indent=2) + "\n```\n\n"
        "## 3. Precondiciones\n\n(no hace falta para las pruebas)\n"
    )


def es_ruta_paquetes(ruta):
    return ruta.startswith("pluginpackages?")


def es_ruta_ensamblados(ruta):
    return ruta.startswith("pluginassemblies?")


def es_ruta_tipos(ruta):
    return ruta.startswith("plugintypes?")


def fila_paquete(**cambios):
    fila = {
        "pluginpackageid": PAQUETE_ID,
        "name": COMPONENTE["nombre"],
        "uniquename": COMPONENTE["uniquename"],
        "version": COMPONENTE["version"],
        "ismanaged": False,
    }
    fila.update(cambios)
    return fila


class Base(unittest.TestCase):
    """Cada prueba corre con su propio directorio temporal haciendo de raíz del
    repositorio, así el .nupkg de mentira no ensucia el repo de verdad."""

    def setUp(self):
        self.dir = tempfile.TemporaryDirectory()
        self.addCleanup(self.dir.cleanup)
        self.raiz_original = pp._RAIZ
        pp._RAIZ = self.dir.name
        self.addCleanup(lambda: setattr(pp, "_RAIZ", self.raiz_original))
        self.ruta_playbook = os.path.join(self.dir.name, "playbook.md")

    def escribir_playbook(self, componente=None, identidad=None):
        with open(self.ruta_playbook, "w", encoding="utf-8") as f:
            f.write(playbook(componente, identidad))
        return self.ruta_playbook

    def escribir_nupkg(self, contenido=BYTES_NUPKG, relativa=None):
        relativa = COMPONENTE["nupkg"] if relativa is None else relativa
        destino = os.path.join(self.dir.name, relativa)
        os.makedirs(os.path.dirname(destino), exist_ok=True)
        with open(destino, "wb") as f:
            f.write(contenido)
        return destino

    def armar(self, cliente, paquete=None, tipos=None, componentes=None, ensamblados=None):
        """Configura las respuestas del entorno: precondiciones felices, el
        paquete (o su ausencia), sus ensamblados, sus tipos y su pertenencia."""
        armar_cliente_precondiciones_ok(cliente, IDENT)
        filas = [] if paquete is None else [paquete]
        cliente.responder("GET", es_ruta_paquetes, (200, {"value": filas}, {}))
        if ensamblados is None:
            ensamblados = [{"pluginassemblyid": ASM_ID, "name": "Sanic.Mppp.Plugins", "version": "1.0.0.0"}]
        cliente.responder("GET", es_ruta_ensamblados, (200, {"value": ensamblados}, {}))
        tipos = COMPONENTE["tipos"] if tipos is None else tipos
        cliente.responder(
            "GET", es_ruta_tipos,
            (200, {"value": [{"plugintypeid": f"tipo-{i}", "typename": t} for i, t in enumerate(tipos)]}, {}),
        )
        if componentes is None:
            componentes = [{"componenttype": 10101, "objectid": PAQUETE_ID}]
        cliente.responder("GET", es_ruta_solutioncomponents, (200, {"value": componentes}, {}))
        return cliente


# ---------------------------------------------------------------------------
# Validación offline: un playbook mal armado no llega a tocar la red
# ---------------------------------------------------------------------------
class ValidacionOffline(Base):
    def rechaza(self, componente, fragmento):
        ruta = self.escribir_playbook(componente)
        centinela = FabricaCentinela()
        estado, _, detalle = pp.construir(ruta, False, centinela)
        self.assertEqual("error", estado, detalle)
        self.assertIn(fragmento, detalle)
        self.assertFalse(centinela.llamada, "no tenía que intentar construir el cliente de Dataverse")

    def test_una_clave_de_mas_o_de_menos_se_rechaza(self):
        sin_tipos = {k: v for k, v in COMPONENTE.items() if k != "tipos"}
        self.rechaza(sin_tipos, "faltan ['tipos']")
        self.rechaza(comp(sobrante="x"), "sobran ['sobrante']")

    def test_el_uniquename_tiene_que_llevar_el_prefijo_del_publisher(self):
        # Convención nuestra: en Dev hay otro publisher con el mismo prefijo.
        self.rechaza(comp(uniquename="SanicMpppPlugins"), "no empieza con el prefijo del publisher")

    def test_el_uniquename_no_admite_cualquier_caracter(self):
        for malo in ["sanic_Mppp Plugins", "sanic_Mppp-Plugins", "sanic_Mpppñ"]:
            self.rechaza(comp(uniquename=malo), "solo puede llevar letras y dígitos ASCII")

    def test_una_version_mal_formada_se_rechaza_antes_de_crear_nada(self):
        # `version` es INMUTABLE en el servidor: equivocarse obliga a borrar el paquete.
        for mala in ["1", "1.0.0.0.0", "1.0.beta", "01.0.0", "1..0", "v1.0.0"]:
            self.rechaza(comp(version=mala), "no tiene la forma esperada")

    def test_una_version_de_dos_a_cuatro_numeros_se_acepta(self):
        for buena in ["1.0", "1.0.0", "1.0.0.0", "10.20.30.40", "0.1.0"]:
            self.assertTrue(pp._version_bien_formada(buena), buena)

    def test_la_ruta_del_nupkg_es_relativa_y_sin_saltos(self):
        self.rechaza(comp(nupkg="/etc/passwd.nupkg"), "ruta relativa a la raíz del repositorio")
        self.rechaza(comp(nupkg="../fuera/x.nupkg"), "ruta relativa a la raíz del repositorio")
        self.rechaza(comp(nupkg="src/paquete.zip"), "no termina en '.nupkg'")

    def test_los_tipos_tienen_que_ser_nombres_dotnet_completos_y_sin_repetir(self):
        self.rechaza(comp(tipos=["ListaBlancaStep"]), "no parece un nombre de tipo .NET completo")
        self.rechaza(comp(tipos=["A.B", "A.B"]), "tiene entradas repetidas")
        self.rechaza(comp(tipos=[]), "tipos")

    def test_el_nombre_visible_va_sin_tildes(self):
        self.rechaza(comp(nombre="Paquete de código"), "fuera de ASCII")

    def test_el_largo_de_cada_columna_se_respeta(self):
        self.rechaza(comp(nombre="a" * (pp.LARGO_NOMBRE + 1)), f"el máximo de la columna es {pp.LARGO_NOMBRE}")
        self.rechaza(comp(uniquename="sanic_" + "a" * pp.LARGO_UNIQUENAME), f"el máximo de la columna es {pp.LARGO_UNIQUENAME}")


# ---------------------------------------------------------------------------
# El paquete en disco
# ---------------------------------------------------------------------------
class PaqueteEnDisco(Base):
    def test_sin_dotnet_pack_queda_bloqueado_y_no_escribe_nada(self):
        ruta = self.escribir_playbook()
        cliente = self.armar(ClienteSimulado(), paquete=None)
        estado, _, detalle = pp.construir(ruta, False, lambda: cliente)
        self.assertEqual("bloqueado", estado, detalle)
        self.assertIn("dotnet pack", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_un_paquete_vacio_queda_bloqueado(self):
        self.escribir_nupkg(b"")
        ruta = self.escribir_playbook()
        cliente = self.armar(ClienteSimulado(), paquete=None)
        estado, _, detalle = pp.construir(ruta, False, lambda: cliente)
        self.assertEqual("bloqueado", estado, detalle)
        self.assertIn("vacío", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_un_paquete_demasiado_pesado_queda_bloqueado_antes_de_subirlo(self):
        # El caso real: se coló el SDK de Dataverse, que ya vive en el sandbox.
        self.escribir_nupkg(b"x" * (pp.MAXIMO_BYTES_NUPKG + 1))
        ruta = self.escribir_playbook()
        cliente = self.armar(ClienteSimulado(), paquete=None)
        estado, _, detalle = pp.construir(ruta, False, lambda: cliente)
        self.assertEqual("bloqueado", estado, detalle)
        self.assertIn("que ya vive en el sandbox", detalle)
        self.assertFalse(cliente.hubo_escritura())


# ---------------------------------------------------------------------------
# El contenido del .nupkg (D-57 b, 2026-09-25): `dotnet pack` puede terminar
# con éxito sin regenerar el .nupkg si MSBuild da `GenerateNuspec` por
# "up-to-date" contra una caché de `obj/` vieja. Una fecha dice que el archivo
# se escribió; solo el contenido dice qué tiene adentro. Por eso se abre el
# .nupkg como el .zip que es, se extrae el .dll y se comprueba que los tipos
# que el playbook declara están de verdad adentro, ANTES de subir nada.
# ---------------------------------------------------------------------------
class ContenidoDelNupkg(Base):
    def test_si_al_dll_le_falta_un_tipo_declarado_queda_bloqueado_y_no_sube_nada(self):
        # El caso real del 2026-09-25: el .nupkg se "armó" pero con el .dll
        # viejo, sin las clases nuevas.
        self.escribir_nupkg(_nupkg_de_mentira(COMPONENTE["tipos"][:-1]))  # falta ListaBlancaStep
        ruta = self.escribir_playbook()
        cliente = self.armar(ClienteSimulado(), paquete=None)
        estado, _, detalle = pp.construir(ruta, False, lambda: cliente)
        self.assertEqual("bloqueado", estado, detalle)
        self.assertIn("Sanic.Mppp.Plugins.Steps.ListaBlancaStep", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_si_al_dll_no_le_falta_ningun_tipo_declarado_se_sube_normalmente(self):
        self.escribir_nupkg(_nupkg_de_mentira(COMPONENTE["tipos"]))
        ruta = self.escribir_playbook()
        cliente = ClienteSimulado()
        respuestas = [(200, {"value": []}, {}), (200, {"value": [fila_paquete()]}, {})]
        cliente.responder(
            "GET", es_ruta_paquetes,
            lambda r, c, s: respuestas.pop(0) if respuestas else (200, {"value": [fila_paquete()]}, {}),
        )
        self.armar(cliente, paquete=fila_paquete())
        cliente.responder("POST", "pluginpackages", (204, {}, {"OData-EntityId": f"x(  {PAQUETE_ID})"}))
        estado, _, detalle = pp.construir(ruta, False, lambda: cliente)
        self.assertEqual("creado", estado, detalle)

    def test_un_nupkg_sin_ningun_dll_adentro_queda_bloqueado(self):
        self.escribir_nupkg(_nupkg_de_mentira(COMPONENTE["tipos"], con_dll=False))
        ruta = self.escribir_playbook()
        cliente = self.armar(ClienteSimulado(), paquete=None)
        estado, _, detalle = pp.construir(ruta, False, lambda: cliente)
        self.assertEqual("bloqueado", estado, detalle)
        self.assertIn(".dll", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_un_nupkg_que_no_es_un_zip_valido_queda_bloqueado_no_error(self):
        # No es forma inesperada de la plataforma: es la señal de que
        # `dotnet pack` no regeneró el archivo (D-57 b).
        self.escribir_nupkg(b"esto no es un zip, aunque pese y exista")
        ruta = self.escribir_playbook()
        cliente = self.armar(ClienteSimulado(), paquete=None)
        estado, _, detalle = pp.construir(ruta, False, lambda: cliente)
        self.assertEqual("bloqueado", estado, detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_actualizar_contenido_tambien_verifica_el_dll_antes_del_patch(self):
        self.escribir_nupkg(_nupkg_de_mentira(COMPONENTE["tipos"][:-1]))
        ruta = self.escribir_playbook()
        cliente = self.armar(ClienteSimulado(), paquete=fila_paquete())
        estado, _, detalle = pp.construir(ruta, False, lambda: cliente, actualizar_contenido=True)
        self.assertEqual("bloqueado", estado, detalle)
        self.assertIn("Sanic.Mppp.Plugins.Steps.ListaBlancaStep", detalle)
        self.assertFalse(cliente.hubo_escritura())


# ---------------------------------------------------------------------------
# Contra el entorno
# ---------------------------------------------------------------------------
class ContraElEntorno(Base):
    def test_el_alta_sube_el_nupkg_en_base64_dentro_de_la_solucion(self):
        self.escribir_nupkg()
        ruta = self.escribir_playbook()
        cliente = ClienteSimulado()
        # La primera lectura no encuentra nada; después del POST, sí.
        respuestas = [(200, {"value": []}, {}), (200, {"value": [fila_paquete()]}, {})]
        cliente.responder("GET", es_ruta_paquetes, lambda r, c, s: respuestas.pop(0) if respuestas else (200, {"value": [fila_paquete()]}, {}))
        self.armar(cliente, paquete=fila_paquete())
        cliente.responder("POST", "pluginpackages", (204, {}, {"OData-EntityId": f"x(  {PAQUETE_ID})"}))

        estado, componente, detalle = pp.construir(ruta, False, lambda: cliente)

        self.assertEqual("creado", estado, detalle)
        self.assertEqual(COMPONENTE["uniquename"], componente)
        posts = [l for l in cliente.llamadas if l["metodo"] == "POST"]
        self.assertEqual(1, len(posts))
        cuerpo = posts[0]["cuerpo"]
        self.assertEqual(base64.b64encode(BYTES_NUPKG).decode("ascii"), cuerpo["content"])
        self.assertEqual(COMPONENTE["nombre"], cuerpo["name"])
        self.assertEqual(COMPONENTE["uniquename"], cuerpo["uniquename"])
        self.assertEqual(COMPONENTE["version"], cuerpo["version"])
        self.assertEqual(IDENT["solucion"], posts[0]["solucion"])

    def test_si_ya_esta_bien_no_se_escribe_nada(self):
        self.escribir_nupkg()
        ruta = self.escribir_playbook()
        cliente = self.armar(ClienteSimulado(), paquete=fila_paquete())
        estado, _, detalle = pp.construir(ruta, False, lambda: cliente)
        self.assertEqual("ya_existia", estado, detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_el_componenttype_leido_se_informa_porque_Learn_no_lo_documenta(self):
        self.escribir_nupkg()
        ruta = self.escribir_playbook()
        cliente = self.armar(ClienteSimulado(), paquete=fila_paquete(), componentes=[{"componenttype": 10101, "objectid": PAQUETE_ID}])
        _, _, detalle = pp.construir(ruta, False, lambda: cliente)
        self.assertIn("componenttype 10101", detalle)
        # Y la consulta NO filtra por componenttype: se lee el que haya.
        consultas = [l["ruta"] for l in cliente.llamadas if l["ruta"].startswith("solutioncomponents?")]
        self.assertTrue(consultas)
        for c in consultas:
            self.assertNotIn("componenttype eq", c)

    def test_una_version_distinta_es_difiere_y_dice_que_es_inmutable(self):
        self.escribir_nupkg()
        ruta = self.escribir_playbook()
        cliente = self.armar(ClienteSimulado(), paquete=fila_paquete(version="0.9.0"))
        estado, _, detalle = pp.construir(ruta, False, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("INMUTABLE", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_un_nombre_distinto_es_difiere_y_dice_que_es_inmutable(self):
        self.escribir_nupkg()
        ruta = self.escribir_playbook()
        cliente = self.armar(ClienteSimulado(), paquete=fila_paquete(name="OtroNombre"))
        estado, _, detalle = pp.construir(ruta, False, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("INMUTABLE", detalle)

    def test_un_tipo_de_plugin_que_la_plataforma_no_registro_se_denuncia_por_su_nombre(self):
        # Es el fallo que importa: el paquete sube bien y no sirve para nada,
        # porque los steps y las Custom API no tienen a qué apuntar.
        self.escribir_nupkg()
        ruta = self.escribir_playbook()
        cliente = self.armar(ClienteSimulado(), paquete=fila_paquete(), tipos=COMPONENTE["tipos"][:2])
        estado, _, detalle = pp.construir(ruta, False, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("Sanic.Mppp.Plugins.Steps.ListaBlancaStep", detalle)
        self.assertIn("no registró estos tipos", detalle)

    def test_un_tipo_registrado_que_el_playbook_no_declara_se_denuncia_como_sobrante(self):
        # D-57(c), 2026-09-25: `RevisionAtendidaStep` es una derivación previa del
        # 2026-09-23 que sigue viva en el entorno y el playbook no la declara.
        # Es la misma lección del ribbon (2026-09-24): verificar solo lo que
        # FALTA miente si algo sobra.
        self.escribir_nupkg()
        ruta = self.escribir_playbook()
        extra = "Sanic.Mppp.Plugins.Steps.RevisionAtendidaStep"
        cliente = self.armar(ClienteSimulado(), paquete=fila_paquete(), tipos=COMPONENTE["tipos"] + [extra])
        estado, _, detalle = pp.construir(ruta, False, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn(extra, detalle)
        self.assertIn("sobran", detalle)

    def test_solo_verificar_tambien_denuncia_los_tipos_sobrantes(self):
        self.escribir_nupkg()
        ruta = self.escribir_playbook()
        extra = "Sanic.Mppp.Plugins.Steps.RevisionAtendidaStep"
        cliente = self.armar(ClienteSimulado(), paquete=fila_paquete(), tipos=COMPONENTE["tipos"] + [extra])
        estado, _, detalle = pp.construir(ruta, True, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn(extra, detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_sin_tipos_sobrantes_no_se_denuncia_nada_de_mas(self):
        # Que la denuncia de sobrantes no dispare falsos positivos cuando el
        # entorno tiene EXACTAMENTE lo que el playbook declara.
        self.escribir_nupkg()
        ruta = self.escribir_playbook()
        cliente = self.armar(ClienteSimulado(), paquete=fila_paquete(), tipos=COMPONENTE["tipos"])
        estado, _, detalle = pp.construir(ruta, False, lambda: cliente)
        self.assertEqual("ya_existia", estado, detalle)

    def test_un_paquete_sin_ensamblados_se_denuncia_con_la_lista_vacia(self):
        self.escribir_nupkg()
        ruta = self.escribir_playbook()
        cliente = self.armar(ClienteSimulado(), paquete=fila_paquete(), ensamblados=[], tipos=[])
        estado, _, detalle = pp.construir(ruta, False, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("Ensamblados del paquete: ninguno", detalle)

    def test_un_paquete_fuera_de_la_solucion_es_difiere(self):
        self.escribir_nupkg()
        ruta = self.escribir_playbook()
        cliente = self.armar(ClienteSimulado(), paquete=fila_paquete(), componentes=[])
        estado, _, detalle = pp.construir(ruta, False, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn(SOLUTION_ID, detalle)

    def test_un_paquete_managed_no_se_toca(self):
        self.escribir_nupkg()
        ruta = self.escribir_playbook()
        cliente = self.armar(ClienteSimulado(), paquete=fila_paquete(ismanaged=True))
        estado, _, detalle = pp.construir(ruta, False, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("managed", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_solo_verificar_nunca_escribe_ni_cuando_falta_el_paquete(self):
        self.escribir_nupkg()
        ruta = self.escribir_playbook()
        cliente = self.armar(ClienteSimulado(), paquete=None)
        estado, _, detalle = pp.construir(ruta, True, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("no existe en el entorno", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_actualizar_contenido_hace_patch_del_content_y_nada_mas(self):
        # `nuevo` tiene que ser un .nupkg real (D-57 b): con `--actualizar-contenido`
        # se lee y se verifica igual que en el alta, así que un literal cualquiera
        # ya no alcanza.
        nuevo = _nupkg_de_mentira(COMPONENTE["tipos"])
        self.escribir_nupkg(nuevo)
        ruta = self.escribir_playbook()
        cliente = self.armar(ClienteSimulado(), paquete=fila_paquete())
        cliente.responder("PATCH", lambda r: r.startswith("pluginpackages("), (204, {}, {}))

        estado, _, detalle = pp.construir(ruta, False, lambda: cliente, actualizar_contenido=True)

        self.assertEqual("actualizado", estado, detalle)
        patches = [l for l in cliente.llamadas if l["metodo"] == "PATCH"]
        self.assertEqual(1, len(patches))
        self.assertEqual({"content"}, set(patches[0]["cuerpo"]))
        self.assertEqual(base64.b64encode(nuevo).decode("ascii"), patches[0]["cuerpo"]["content"])
        self.assertEqual(f"pluginpackages({PAQUETE_ID})", patches[0]["ruta"])

    def test_sin_la_bandera_no_se_actualiza_el_contenido_aunque_el_nupkg_haya_cambiado(self):
        # No hace falta que este .nupkg sea válido para que la prueba pase (esta
        # rama nunca lo lee), pero se deja igual como zip real: que ningún
        # literal `b"PK\x03\x04..."` de relleno quede para copiar (D-57 b).
        self.escribir_nupkg(_nupkg_de_mentira(COMPONENTE["tipos"]))
        ruta = self.escribir_playbook()
        cliente = self.armar(ClienteSimulado(), paquete=fila_paquete())
        estado, _, _ = pp.construir(ruta, False, lambda: cliente)
        self.assertEqual("ya_existia", estado)
        self.assertFalse(cliente.hubo_escritura())

    def test_dos_paquetes_con_el_mismo_unique_name_es_forma_inesperada(self):
        self.escribir_nupkg()
        ruta = self.escribir_playbook()
        cliente = ClienteSimulado()
        armar_cliente_precondiciones_ok(cliente, IDENT)
        cliente.responder("GET", es_ruta_paquetes, (200, {"value": [fila_paquete(), fila_paquete()]}, {}))
        estado, _, detalle = pp.construir(ruta, False, lambda: cliente)
        self.assertEqual("error", estado, detalle)
        self.assertIn("se esperaba a lo sumo una", detalle)

    def test_un_http_inesperado_en_el_alta_no_se_confunde_con_exito(self):
        self.escribir_nupkg()
        ruta = self.escribir_playbook()
        cliente = self.armar(ClienteSimulado(), paquete=None)
        cliente.responder("POST", "pluginpackages", (400, {"error": "unique name en uso"}, {}))
        estado, _, detalle = pp.construir(ruta, False, lambda: cliente, dormir=lambda s: None)
        self.assertEqual("error", estado, detalle)
        self.assertIn("HTTP 400", detalle)


class Uso(Base):
    def test_una_bandera_desconocida_no_se_ignora_en_silencio(self):
        original = sys.argv
        try:
            sys.argv = ["paquete_plugins.py", self.escribir_playbook(), "--forzar"]
            salida = []
            real = pp.salida
            pp.salida = lambda e, c, d: salida.append((e, c, d)) or 1
            try:
                pp.main()
            finally:
                pp.salida = real
        finally:
            sys.argv = original
        self.assertEqual("error", salida[0][0])
        self.assertIn("--forzar", salida[0][2])


if __name__ == "__main__":
    unittest.main()
