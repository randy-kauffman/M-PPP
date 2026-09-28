"""Pruebas de `herramientas/pruebas/preparar_datos.py`. Nunca tocan la red ni
`local/*.env`: usan `ClienteSimulado` (el mismo doble de prueba de
`herramientas/construir/tests/`) en vez de `dataverse_api.Dataverse`.

Por qué existe este archivo (2026-09-28). El 2026-09-27, un `--limpiar` de
esta herramienta borró autorizaciones REALES del aprobador: `limpiar()`
armaba la lista de borrado con un `GET` de TODA la tabla `AutorizacionPlan` y
un `in` de Python contra `sanic_nombre` — cualquier fila real cuyo texto
coincidiera por casualidad con "→ PR11" (u otro código de plan del fixture)
caía, sin releer el registro ni comprobar que de verdad fuera del fixture.

El aprobador aprobó tres cambios ese mismo día (mover la marca de fixture de
`PR` a `ZZP-` sobre `sanic_nombre`; reemplazar los `DELETE` directos por una
verificación de pertenencia releída justo antes de cada borrado; envolver
`--limpiar` y la creación con el semáforo). La prueba que más importa acá NO
es "¿se borra lo que hay que borrar?" — es la contraria: **¿sobrevive un
registro que no lleva la marca (o cuya pertenencia no se puede probar en el
momento del borrado)?** Eso es lo que este archivo verifica primero.
"""
import os
import sys
import unittest

_AQUI = os.path.dirname(os.path.abspath(__file__))
_PRUEBAS = os.path.dirname(_AQUI)
_HERRAMIENTAS = os.path.dirname(_PRUEBAS)
sys.path.insert(0, _PRUEBAS)
sys.path.insert(0, os.path.join(_HERRAMIENTAS, "construir", "tests"))

import preparar_datos as prep  # noqa: E402
import semaforo  # noqa: E402
from cliente_simulado import ClienteSimulado  # noqa: E402

REMITENTE = "zz-tester@example.com"


def _contiene(*fragmentos):
    """Matcher robusto para `ClienteSimulado.responder`: exige que la ruta
    contenga TODOS los fragmentos, sin depender del orden exacto de
    `$select`/`$filter` que arma `preparar_datos.uno()`."""
    return lambda ruta: all(f in ruta for f in fragmentos)


def _sin_resultados(*a, **k):
    return (200, {"value": []}, {})


class LimpiarSobreviveSinMarca(unittest.TestCase):
    """La prueba que más importa (consigna del aprobador): un registro SIN
    la marca de fixture, o cuya pertenencia no se puede reconfirmar justo
    antes del `DELETE`, tiene que sobrevivir — no la inversa."""

    def test_cliente_que_pierde_la_marca_entre_el_listado_y_el_borrado_sobrevive(self):
        """Reproduce la garantía de `marca_agua.borrar_marcado()`: si el
        registro se releyó y YA NO lleva `ZZP-` (alguien lo renombró en el
        medio, a mano), no se borra. Nada de POST/PATCH/DELETE en total."""
        cs = ClienteSimulado()
        # El listado por $filter todavía lo encuentra con el nombre marcado...
        cs.responder("GET", _contiene("sanic_mppp_tbl_clientes?", f"sanic_nombre eq '{prep.CLIENTE}'"),
                     (200, {"value": [{"sanic_mppp_tbl_clienteid": "C1", "sanic_nombre": prep.CLIENTE}]}, {}))
        # ...pero la relectura puntual, justo antes del DELETE, ya no.
        cs.responder("GET", _contiene("sanic_mppp_tbl_clientes(C1)", "$select=sanic_nombre"),
                     (200, {"sanic_nombre": "Pruebas MPPP SA (renombrado a mano, sin la marca)"}, {}))
        for codigo in (p["codigo"] for p in prep.PLANES):
            cs.responder("GET", _contiene("sanic_mppp_tbl_plans?", f"sanic_codigo eq '{codigo}'"), _sin_resultados)

        borrados, ignorados = prep.limpiar(cs, remitente=None)

        self.assertFalse(cs.hubo_escritura(), "no se esperaba ningún POST/PATCH/DELETE")
        self.assertNotIn(prep.CLIENTE, borrados)
        self.assertTrue(any("cliente" in m for m in ignorados), ignorados)

    def test_plan_cuyo_codigo_choca_con_un_plan_real_de_otro_cliente_sobrevive(self):
        """El código corto (`PR11`) no es la marca — nunca lo fue, no tiene
        lugar para `ZZP-`. La protección real es la relación con el Cliente
        marcado: un Plan `PR11` que en realidad cuelga de OTRO cliente (una
        coincidencia de código con un plan real del banco) tiene que
        sobrevivir, aunque el Cliente del fixture sí se borre."""
        cs = ClienteSimulado()
        cs.responder("GET", _contiene("sanic_mppp_tbl_clientes?", f"sanic_nombre eq '{prep.CLIENTE}'"),
                     (200, {"value": [{"sanic_mppp_tbl_clienteid": "C1", "sanic_nombre": prep.CLIENTE}]}, {}))
        cs.responder("GET", _contiene("sanic_mppp_tbl_clientes(C1)", "$select=sanic_nombre"),
                     (200, {"sanic_nombre": prep.CLIENTE}, {}))
        cs.responder("DELETE", "sanic_mppp_tbl_clientes(C1)", (204, {}, {}))

        cs.responder("GET", _contiene("sanic_mppp_tbl_plans?", "sanic_codigo eq 'PR11'"),
                     (200, {"value": [{"sanic_mppp_tbl_planid": "P-REAL",
                                       "_sanic_clienteid_value": "C-DE-OTRO-CLIENTE-REAL"}]}, {}))
        for codigo in (p["codigo"] for p in prep.PLANES if p["codigo"] != "PR11"):
            cs.responder("GET", _contiene("sanic_mppp_tbl_plans?", f"sanic_codigo eq '{codigo}'"), _sin_resultados)

        borrados, ignorados = prep.limpiar(cs, remitente=None)

        self.assertNotIn("sanic_mppp_tbl_plans(P-REAL)", [f"{l['ruta']}" for l in cs.llamadas if l["metodo"] == "DELETE"])
        self.assertNotIn("PR11", borrados)
        self.assertTrue(any("PR11" in m and "no cuelga del Cliente marcado" in m for m in ignorados), ignorados)
        # El Cliente marcado, en cambio, sí es propio del fixture y se borra.
        self.assertIn(prep.CLIENTE, borrados)

    def test_autorizacionplan_ajena_que_solo_coincide_en_el_texto_del_nombre_nunca_se_toca(self):
        """Reproduce la FORMA exacta del incidente del 2026-09-27: una
        `AutorizacionPlan` real cuyo `sanic_nombre` contiene "→ PR11" pero
        pertenece a otro correo y otro plan. Como `limpiar()` ya no arma la
        lista con un `GET` sin filtro + `in` de texto (busca por los dos
        lookups EXACTOS del fixture), esa fila ni siquiera entra en juego: si
        alguien reintrodujera el patrón viejo, `ClienteSimulado` haría
        explotar la prueba con `AssertionError` por no tener una regla para
        ese `GET` sin filtro (a propósito: no se configura ninguna)."""
        cs = ClienteSimulado()
        cs.responder("GET", _contiene("sanic_mppp_tbl_clientes?", f"sanic_nombre eq '{prep.CLIENTE}'"),
                     (200, {"value": [{"sanic_mppp_tbl_clienteid": "C1", "sanic_nombre": prep.CLIENTE}]}, {}))
        cs.responder("GET", _contiene("sanic_mppp_tbl_clientes(C1)", "$select=sanic_nombre"),
                     (200, {"sanic_nombre": prep.CLIENTE}, {}))
        cs.responder("DELETE", "sanic_mppp_tbl_clientes(C1)", (204, {}, {}))

        cs.responder("GET", _contiene("sanic_mppp_tbl_plans?", "sanic_codigo eq 'PR11'"),
                     (200, {"value": [{"sanic_mppp_tbl_planid": "P1", "_sanic_clienteid_value": "C1"}]}, {}))
        cs.responder("GET", _contiene("sanic_mppp_tbl_plans?", "sanic_mppp_tbl_planid eq P1"),
                     (200, {"value": [{"_sanic_clienteid_value": "C1"}]}, {}))
        cs.responder("DELETE", "sanic_mppp_tbl_plans(P1)", (204, {}, {}))
        for codigo in (p["codigo"] for p in prep.PLANES if p["codigo"] != "PR11"):
            cs.responder("GET", _contiene("sanic_mppp_tbl_plans?", f"sanic_codigo eq '{codigo}'"), _sin_resultados)

        cs.responder("GET", _contiene("sanic_mppp_tbl_autorizados?", f"sanic_nombre eq '{REMITENTE}'"),
                     (200, {"value": [{"sanic_mppp_tbl_autorizadoid": "A1"}]}, {}))
        cs.responder("DELETE", "sanic_mppp_tbl_autorizados(A1)", (204, {}, {}))

        # La ÚNICA búsqueda de AutorizacionPlan que este código hace: por los
        # dos lookups exactos de ESTE fixture. No hay ninguna otra fila real
        # (con → PR11 en el texto pero otros lookups) que pueda colarse:
        # nunca se lista "todo" para filtrar después en Python.
        cs.responder("GET", _contiene("sanic_mppp_tbl_autorizacionplans?",
                                      "_sanic_autorizadoid_value eq A1", "_sanic_planid_value eq P1"),
                     _sin_resultados)

        borrados, ignorados = prep.limpiar(cs, remitente=REMITENTE)

        self.assertFalse(any(l["metodo"] == "DELETE" and "autorizacionplan" in l["ruta"] for l in cs.llamadas))
        self.assertIn(prep.CLIENTE, borrados)
        self.assertIn(REMITENTE, borrados)


class LimpiarBorraElFixturePropio(unittest.TestCase):
    def test_borra_cliente_plan_autorizacion_y_autorizado_cuando_todo_esta_marcado_y_enlazado(self):
        """Camino feliz: Cliente marcado, Plan colgando de él, Autorizado por
        correo exacto y AutorizacionPlan con los dos lookups exactos — todo
        lo del fixture se borra, y nada más."""
        cs = ClienteSimulado()
        cs.responder("GET", _contiene("sanic_mppp_tbl_clientes?", f"sanic_nombre eq '{prep.CLIENTE}'"),
                     (200, {"value": [{"sanic_mppp_tbl_clienteid": "C1", "sanic_nombre": prep.CLIENTE}]}, {}))
        cs.responder("GET", _contiene("sanic_mppp_tbl_clientes(C1)", "$select=sanic_nombre"),
                     (200, {"sanic_nombre": prep.CLIENTE}, {}))
        cs.responder("DELETE", "sanic_mppp_tbl_clientes(C1)", (204, {}, {}))

        cs.responder("GET", _contiene("sanic_mppp_tbl_plans?", "sanic_codigo eq 'PR11'"),
                     (200, {"value": [{"sanic_mppp_tbl_planid": "P1", "_sanic_clienteid_value": "C1"}]}, {}))
        cs.responder("GET", _contiene("sanic_mppp_tbl_plans?", "sanic_mppp_tbl_planid eq P1"),
                     (200, {"value": [{"_sanic_clienteid_value": "C1"}]}, {}))
        cs.responder("DELETE", "sanic_mppp_tbl_plans(P1)", (204, {}, {}))
        for codigo in (p["codigo"] for p in prep.PLANES if p["codigo"] != "PR11"):
            cs.responder("GET", _contiene("sanic_mppp_tbl_plans?", f"sanic_codigo eq '{codigo}'"), _sin_resultados)

        cs.responder("GET", _contiene("sanic_mppp_tbl_autorizados?", f"sanic_nombre eq '{REMITENTE}'"),
                     (200, {"value": [{"sanic_mppp_tbl_autorizadoid": "A1"}]}, {}))
        cs.responder("DELETE", "sanic_mppp_tbl_autorizados(A1)", (204, {}, {}))

        nombre_ap = f"{REMITENTE} → PR11 - {prep.CLIENTE}"
        cs.responder("GET", _contiene("sanic_mppp_tbl_autorizacionplans?",
                                      "_sanic_autorizadoid_value eq A1", "_sanic_planid_value eq P1"),
                     (200, {"value": [{"sanic_mppp_tbl_autorizacionplanid": "AP1"}]}, {}))
        cs.responder("GET", _contiene("sanic_mppp_tbl_autorizacionplans?",
                                      "sanic_mppp_tbl_autorizacionplanid eq AP1"),
                     (200, {"value": [{"_sanic_autorizadoid_value": "A1", "_sanic_planid_value": "P1",
                                       "sanic_nombre": nombre_ap}]}, {}))
        cs.responder("DELETE", "sanic_mppp_tbl_autorizacionplans(AP1)", (204, {}, {}))

        borrados, ignorados = prep.limpiar(cs, remitente=REMITENTE)

        self.assertEqual(ignorados, [])
        self.assertEqual(sorted(borrados), sorted([nombre_ap, REMITENTE, "PR11", prep.CLIENTE]))
        deletes = sorted(l["ruta"] for l in cs.llamadas if l["metodo"] == "DELETE")
        self.assertEqual(deletes, sorted([
            "sanic_mppp_tbl_autorizacionplans(AP1)",
            "sanic_mppp_tbl_autorizados(A1)",
            "sanic_mppp_tbl_plans(P1)",
            "sanic_mppp_tbl_clientes(C1)",
        ]))


class SemaforoBloqueaLimpiarYCreacion(unittest.TestCase):
    """`--limpiar` y la creación (`--remitente`) tienen que respetar el
    semáforo: si está en rojo (por ejemplo, un bloqueo MANUAL del
    aprobador), esta herramienta no puede arrancar ni una sola llamada a
    Dataverse."""

    def setUp(self):
        self._ruta_original = semaforo.RUTA
        self._tmp = os.path.join(_AQUI, "_semaforo_prueba.json")
        semaforo.RUTA = self._tmp
        self.addCleanup(self._restaurar)
        self._modulo_dv_original = sys.modules.get("dataverse_api")
        self.addCleanup(self._restaurar_modulo_dv)

    def _restaurar(self):
        semaforo.RUTA = self._ruta_original
        if os.path.exists(self._tmp):
            os.remove(self._tmp)

    def _restaurar_modulo_dv(self):
        if self._modulo_dv_original is not None:
            sys.modules["dataverse_api"] = self._modulo_dv_original
        else:
            sys.modules.pop("dataverse_api", None)

    def _instalar_dataverse_que_no_debe_llamarse(self):
        import types

        modulo = types.ModuleType("dataverse_api")

        class _DataverseCentinela:
            def __init__(self, *a, **kw):
                pass

            def call(self, metodo, ruta, *a, **kw):
                raise AssertionError(f"no se esperaba ninguna llamada a Dataverse: {metodo} {ruta}")

        modulo.Dataverse = _DataverseCentinela
        sys.modules["dataverse_api"] = modulo

    def test_limpiar_no_arranca_si_el_semaforo_esta_en_rojo(self):
        # Bloqueo MANUAL (pid=None), como hace `semaforo.py --bloquear`.
        import json as _json
        with open(self._tmp, "w", encoding="utf-8") as f:
            _json.dump({"pid": None, "motivo": "probando a mano", "cuando": "2026-09-28T00:00:00"}, f)

        self._instalar_dataverse_que_no_debe_llamarse()
        argv_original = sys.argv
        sys.argv = ["preparar_datos.py", "--limpiar"]
        try:
            with self.assertRaises(SystemExit):
                prep.main()
        finally:
            sys.argv = argv_original


if __name__ == "__main__":
    unittest.main()
