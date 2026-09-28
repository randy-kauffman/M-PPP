"""Pruebas de `herramientas/construir/parametro.py`. Nunca tocan la red.

D-53: esta herramienta existe para que agregar UN parámetro por playbook sea
más corto que editarlo a mano en el entorno. Lo que importa acá: nunca edita
una fila sembrada (siempre versión = máximo del ENTORNO + 1), nunca escribe
el playbook antes de confirmar el alta en el entorno, y el archivo que deja
atrás sigue siendo un playbook válido para `datos.py`.
"""
import json
import os
import sys
import tempfile
import unittest

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _AQUI)
sys.path.insert(0, os.path.dirname(_AQUI))

import parametro as pm  # noqa: E402
from cliente_simulado import ClienteSimulado, FabricaCentinela  # noqa: E402

CONJUNTO = "sanic_mppp_tbl_parametros"
PRIMARIA = "sanic_mppp_tbl_parametroid"
CHOICE = pm.CHOICE_TIPO
VALORES_CHOICE = {"Texto": 159460001, "Numero": 159460002, "JSON": 159460003}


def bloque_identidad():
    return {
        "tipo_playbook": "datos", "version_skill": "0.1.0", "proyecto": "prueba",
        "inventario": "9.1", "fase": 1, "entorno_url": "https://ensayo.crm.dynamics.com/",
        "solucion": "sanic_mppp_sol_prueba", "publisher": "prueba", "prefijo": "sanic",
        "abrev": "prb", "prefijo_opciones": 15946, "lcid": 1033,
    }


def texto_playbook(filas):
    """Un playbook mínimo, con el mismo formato exacto (dos bloques ```json```,
    'filas' como última clave del componente) que espera `_agregar_al_playbook`."""
    componente = {
        "tipo": "datos", "tabla": pm.TABLA, "clave": ["sanic_nombre", "sanic_version"], "filas": filas,
    }
    return (
        "# Playbook: datos · prueba\n\n"
        "## 1. Identidad\n\n```json\n" + json.dumps(bloque_identidad(), ensure_ascii=False, indent=2) + "\n```\n\n"
        "## 2. Qué se crea\n\n```json\n" + json.dumps(componente, ensure_ascii=False, indent=2) + "\n```\n\n"
        "## 3. Precondiciones\n\n(no hace falta para las pruebas)\n"
    )


class Base(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.TemporaryDirectory()
        self.addCleanup(self.dir.cleanup)
        self.ruta = os.path.join(self.dir.name, "parametros.md")

    def escribir(self, filas=None):
        with open(self.ruta, "w", encoding="utf-8") as f:
            f.write(texto_playbook([] if filas is None else filas))
        return self.ruta

    def leer(self):
        with open(self.ruta, encoding="utf-8") as f:
            return f.read()

    def armar(self, cliente, filas_entorno=None, sin_tabla=False, sin_choice=False, etiquetas=None):
        self.filas_entorno = [] if filas_entorno is None else filas_entorno
        cliente.responder(
            "GET", lambda r: r.startswith("EntityDefinitions("),
            (404, {}, {}) if sin_tabla else (200, {"EntitySetName": CONJUNTO, "PrimaryIdAttribute": PRIMARIA}, {}),
        )
        etiquetas = VALORES_CHOICE if etiquetas is None else etiquetas
        opciones = [{"Value": v, "Label": {"LocalizedLabels": [{"Label": k, "LanguageCode": 1033}]}}
                    for k, v in etiquetas.items()]
        cliente.responder(
            "GET", lambda r: r.startswith("GlobalOptionSetDefinitions("),
            (404, {}, {}) if sin_choice else (200, {"Options": opciones}, {}),
        )

        def responder_filas(ruta, cuerpo, solucion):
            return (200, {"value": list(self.filas_entorno)}, {})

        cliente.responder("GET", lambda r: r.startswith(CONJUNTO + "?"), responder_filas)
        return cliente

    def crear(self, nombre, valor, tipo, descripcion="una razon cualquiera", solo_verificar=False, cliente=None,
              ruta=None):
        cliente = cliente or self.armar(ClienteSimulado())
        return pm.construir(nombre, valor, tipo, descripcion, ruta or self.ruta, solo_verificar, lambda: cliente), cliente


# ---------------------------------------------------------------------------
# Validación offline
# ---------------------------------------------------------------------------
class ValidacionOffline(Base):
    def rechaza(self, nombre, valor, tipo, descripcion, fragmento):
        centinela = FabricaCentinela()
        estado, _, detalle = pm.construir(nombre, valor, tipo, descripcion, self.escribir(), False, centinela)
        self.assertEqual("error", estado, detalle)
        self.assertIn(fragmento, detalle)
        self.assertFalse(centinela.llamada, "no tenía que intentar construir el cliente de Dataverse")

    def test_nombre_vacio_se_rechaza(self):
        self.rechaza("   ", "x", "Texto", "d", "no puede estar vacío")

    def test_nombre_con_punto_al_principio_o_al_final_se_rechaza(self):
        self.rechaza(".malo", "x", "Texto", "d", "empezar ni terminar con punto")
        self.rechaza("malo.", "x", "Texto", "d", "empezar ni terminar con punto")

    def test_nombre_con_puntos_seguidos_se_rechaza(self):
        self.rechaza("algo..malo", "x", "Texto", "d", "puntos seguidos")

    def test_nombre_con_caracter_fuera_de_ascii_se_rechaza(self):
        self.rechaza("con.ñ", "x", "Texto", "d", "solo admite letras, dígitos y puntos")

    def test_nombre_se_normaliza_a_minuscula_sin_tocar_la_red_de_mas(self):
        # Mayúsculas no son un error: se normalizan, igual que hace el step del entorno.
        cliente = self.armar(ClienteSimulado())
        estado, componente, _ = pm.construir("RPA.Horas.Tolerancia", "1", "Numero", "d", self.escribir(), True, lambda: cliente)
        self.assertEqual("difiere", estado)
        self.assertIn("rpa.horas.tolerancia", componente)

    def test_tipo_invalido_se_rechaza(self):
        self.rechaza("algo.bien", "x", "Fecha", "d", "tiene que ser uno de")

    def test_descripcion_vacia_se_rechaza(self):
        self.rechaza("algo.bien", "x", "Texto", "  ", "obligatoria y no puede estar vacía")

    def test_valor_vacio_se_rechaza(self):
        self.rechaza("algo.bien", "", "Texto", "d", "no puede estar vacío")

    def test_valor_no_numerico_con_tipo_numero_se_rechaza(self):
        self.rechaza("algo.bien", "doce", "Numero", "d", "no es un entero")

    def test_valor_negativo_numerico_se_acepta_offline(self):
        cliente = self.armar(ClienteSimulado())
        estado, _, detalle = pm.construir("algo.bien", "-5", "Numero", "d", self.escribir(), True, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)

    def test_valor_json_invalido_se_rechaza(self):
        self.rechaza("algo.bien", "{mal", "JSON", "d", "no es JSON válido")

    def test_valor_json_valido_se_acepta_offline(self):
        cliente = self.armar(ClienteSimulado())
        estado, _, detalle = pm.construir("algo.bien", '{"a":1}', "JSON", "d", self.escribir(), True, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)


# ---------------------------------------------------------------------------
# Contra el entorno
# ---------------------------------------------------------------------------
class ContraElEntorno(Base):
    def test_tabla_que_no_existe_queda_bloqueada_sin_escribir(self):
        cliente = self.armar(ClienteSimulado(), sin_tabla=True)
        (estado, _, detalle), cliente = self.crear("algo.bien", "1", "Numero", cliente=cliente)
        self.assertEqual("bloqueado", estado, detalle)
        self.assertIn("no existe en el entorno", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_version_maxima_se_calcula_del_entorno_no_del_playbook(self):
        cliente = self.armar(ClienteSimulado(), filas_entorno=[
            {PRIMARIA: "id-1", "sanic_version": 1}, {PRIMARIA: "id-2", "sanic_version": 2},
        ])
        (estado, _, detalle), _ = self.crear("plantilla.obligatoriedad", "{}", "JSON", solo_verificar=True, cliente=cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("sanic_version=3", detalle)
        self.assertIn("máximo sembrado hoy: 2", detalle)

    def test_sin_filas_previas_la_version_es_1(self):
        cliente = self.armar(ClienteSimulado())
        (estado, _, detalle), _ = self.crear("parametro.nuevo", "x", "Texto", solo_verificar=True, cliente=cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("sanic_version=1", detalle)
        self.assertIn("máximo sembrado hoy: 0", detalle)

    def test_solo_verificar_nunca_escribe_ni_el_entorno_ni_el_playbook(self):
        cliente = self.armar(ClienteSimulado())
        self.escribir()
        original = self.leer()
        (estado, _, _), cliente = self.crear("algo.bien", "1", "Numero", solo_verificar=True, cliente=cliente)
        self.assertEqual("difiere", estado)
        self.assertFalse(cliente.hubo_escritura())
        self.assertEqual(original, self.leer(), "el playbook no se tenía que tocar")

    def test_una_etiqueta_de_tipo_que_no_existe_en_el_choice_queda_bloqueada(self):
        cliente = self.armar(ClienteSimulado(), etiquetas={"Texto": 159460001})
        (estado, _, detalle), cliente = self.crear("algo.bien", "{}", "JSON", cliente=cliente)
        self.assertEqual("bloqueado", estado, detalle)
        self.assertIn("no tiene la opción 'JSON'", detalle)
        self.assertFalse(cliente.hubo_escritura())

    def test_alta_exitosa_traduce_tipo_manda_version_y_confirma_releyendo(self):
        cliente = self.armar(ClienteSimulado())
        self.escribir(filas=[{
            "sanic_nombre": "correo.prefijos.reenvio", "sanic_version": 1,
            "sanic_tipo": {"choice": CHOICE, "etiqueta": "JSON"}, "sanic_valor": "[]", "sanic_descripcion": "ya estaba",
        }])

        def post(ruta, cuerpo, solucion):
            self.filas_entorno.append({
                PRIMARIA: "id-nuevo", "sanic_nombre": cuerpo["sanic_nombre"], "sanic_version": cuerpo["sanic_version"],
                "sanic_valor": cuerpo["sanic_valor"], "sanic_tipo": cuerpo["sanic_tipo"],
                "sanic_descripcion": cuerpo["sanic_descripcion"],
            })
            return (204, {}, {})

        cliente.responder("POST", CONJUNTO, post)
        (estado, componente, detalle), cliente = self.crear(
            "rpa.horas.tolerancia", "48", "Numero", descripcion="nueva tolerancia", cliente=cliente)

        self.assertEqual("creado", estado, detalle)
        self.assertEqual("sanic_mppp_tbl_parametro:rpa.horas.tolerancia", componente)
        posts = [l["cuerpo"] for l in cliente.llamadas if l["metodo"] == "POST"]
        self.assertEqual(1, len(posts))
        self.assertEqual(1, posts[0]["sanic_version"])
        self.assertEqual(VALORES_CHOICE["Numero"], posts[0]["sanic_tipo"])
        # No es componente de solución: no lleva la cabecera.
        for l in cliente.llamadas:
            if l["metodo"] == "POST":
                self.assertIsNone(l["solucion"])

    def test_el_alta_agrega_la_fila_al_playbook_sin_tocar_las_demas(self):
        existente = {
            "sanic_nombre": "correo.prefijos.reenvio", "sanic_version": 1,
            "sanic_tipo": {"choice": CHOICE, "etiqueta": "JSON"}, "sanic_valor": "[]", "sanic_descripcion": "ya estaba",
        }
        self.escribir(filas=[existente])
        cliente = self.armar(ClienteSimulado())

        def post(ruta, cuerpo, solucion):
            self.filas_entorno = [{
                PRIMARIA: "id-nuevo", "sanic_nombre": cuerpo["sanic_nombre"], "sanic_version": cuerpo["sanic_version"],
                "sanic_valor": cuerpo["sanic_valor"], "sanic_tipo": cuerpo["sanic_tipo"],
            }]
            return (204, {}, {})

        cliente.responder("POST", CONJUNTO, post)
        (estado, _, detalle), _ = self.crear(
            "nuevo.parametro", '{"x":1}', "JSON", descripcion="motivo del cambio", cliente=cliente)
        self.assertEqual("creado", estado, detalle)

        texto = self.leer()
        bloque = json.loads(texto.split("```json\n")[2].split("```", 1)[0])
        self.assertEqual(2, len(bloque["filas"]))
        self.assertEqual(existente, bloque["filas"][0], "la fila que ya estaba no se tocó")
        nueva = bloque["filas"][1]
        self.assertEqual("nuevo.parametro", nueva["sanic_nombre"])
        self.assertEqual(1, nueva["sanic_version"])
        self.assertEqual({"choice": CHOICE, "etiqueta": "JSON"}, nueva["sanic_tipo"])
        self.assertEqual('{"x":1}', nueva["sanic_valor"])
        self.assertEqual("motivo del cambio", nueva["sanic_descripcion"])

    def test_un_plugin_que_rechaza_el_valor_se_informa_con_su_mensaje_y_no_toca_el_playbook(self):
        self.escribir()
        original = self.leer()
        cliente = self.armar(ClienteSimulado())
        cliente.responder("POST", CONJUNTO, (400, {"error": "La columna 'sanic_nombre' no tiene un valor válido"}, {}))
        (estado, _, detalle), _ = self.crear("algo.bien", "1", "Numero", cliente=cliente)
        self.assertEqual("error", estado, detalle)
        self.assertIn("no tiene un valor válido", detalle)
        self.assertEqual(original, self.leer())

    def test_si_al_releer_no_aparece_exactamente_una_fila_es_error(self):
        cliente = self.armar(ClienteSimulado())
        # El POST "funciona" pero la relectura no ve la fila nueva (self.filas_entorno queda vacía).
        cliente.responder("POST", CONJUNTO, (204, {}, {}))
        (estado, _, detalle), _ = self.crear("algo.bien", "1", "Numero", cliente=cliente)
        self.assertEqual("error", estado, detalle)
        self.assertIn("al releer aparecieron 0 filas", detalle)

    def test_si_lo_releido_no_coincide_con_lo_pedido_es_error(self):
        cliente = self.armar(ClienteSimulado())

        def post(ruta, cuerpo, solucion):
            self.filas_entorno.append({
                PRIMARIA: "id-nuevo", "sanic_nombre": cuerpo["sanic_nombre"], "sanic_version": cuerpo["sanic_version"],
                "sanic_valor": "OTRO VALOR", "sanic_tipo": cuerpo["sanic_tipo"],
            })
            return (204, {}, {})

        cliente.responder("POST", CONJUNTO, post)
        (estado, _, detalle), _ = self.crear("algo.bien", "1", "Numero", cliente=cliente)
        self.assertEqual("error", estado, detalle)
        self.assertIn("no quedó como se pidió", detalle)

    def test_el_entorno_ya_escrito_se_informa_aunque_el_playbook_no_se_pueda_actualizar(self):
        # Playbook roto a propósito: un solo bloque ```json``` en vez de dos.
        with open(self.ruta, "w", encoding="utf-8") as f:
            f.write("# roto\n\n## 2. Qué se crea\n\n```json\n{}\n```\n")
        cliente = self.armar(ClienteSimulado())

        def post(ruta, cuerpo, solucion):
            self.filas_entorno.append({
                PRIMARIA: "id-nuevo", "sanic_nombre": cuerpo["sanic_nombre"], "sanic_version": cuerpo["sanic_version"],
                "sanic_valor": cuerpo["sanic_valor"], "sanic_tipo": cuerpo["sanic_tipo"],
            })
            return (204, {}, {})

        cliente.responder("POST", CONJUNTO, post)
        (estado, _, detalle), _ = self.crear("algo.bien", "1", "Numero", cliente=cliente)
        self.assertEqual("error", estado, detalle)
        self.assertIn("se creó en el entorno", detalle)
        self.assertIn("Agregala a mano", detalle)

    def test_agregar_una_clave_que_el_playbook_ya_tiene_se_rechaza(self):
        # Caso de deriva: el entorno no tiene ninguna fila (versión calculada
        # 1), pero el playbook YA trae una fila con esa misma clave (alguien
        # la agregó a mano o quedó de un ensayo anterior). No se pisa.
        chocando = {
            "sanic_nombre": "algo.bien", "sanic_version": 1,
            "sanic_tipo": {"choice": CHOICE, "etiqueta": "Numero"}, "sanic_valor": "0", "sanic_descripcion": "vieja",
        }
        self.escribir(filas=[chocando])
        cliente = self.armar(ClienteSimulado())
        cliente.responder("POST", CONJUNTO, lambda r, c, s: (
            self.filas_entorno.append({PRIMARIA: "id", "sanic_nombre": c["sanic_nombre"],
                                        "sanic_version": c["sanic_version"], "sanic_valor": c["sanic_valor"],
                                        "sanic_tipo": c["sanic_tipo"]}) or (204, {}, {})
        ))
        (estado, _, detalle), _ = self.crear("algo.bien", "1", "Numero", cliente=cliente)
        self.assertEqual("error", estado, detalle)
        self.assertIn("ya tiene una fila", detalle)


class Uso(unittest.TestCase):
    def test_faltan_banderas_obligatorias_da_uso_incorrecto(self):
        original, capturado = sys.argv, []
        real = pm.salida
        try:
            sys.argv = ["parametro.py", "algo.bien", "1"]
            pm.salida = lambda e, c, d: capturado.append((e, c, d)) or 1
            pm.main()
        finally:
            sys.argv, pm.salida = original, real
        self.assertEqual("error", capturado[0][0])
        self.assertIn("uso incorrecto", capturado[0][2])

    def test_una_bandera_desconocida_no_se_ignora_en_silencio(self):
        original, capturado = sys.argv, []
        real = pm.salida
        try:
            sys.argv = ["parametro.py", "algo.bien", "1", "--tipo=Numero", "--descripcion=d", "--forzar"]
            pm.salida = lambda e, c, d: capturado.append((e, c, d)) or 1
            pm.main()
        finally:
            sys.argv, pm.salida = original, real
        self.assertEqual("error", capturado[0][0])
        self.assertIn("--forzar", capturado[0][2])


if __name__ == "__main__":
    unittest.main()
