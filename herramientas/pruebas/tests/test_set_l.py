"""Pruebas de `herramientas/pruebas/sets/set_l.py` que no tocan la red: las piezas que deciden si un caso da OK o FALLA.

Lo que más importa acá es lo contrario de "pasa lo que tiene que pasar": que el arnés NO dé un verde que no se ganó.
Una prueba de Playwright saltada, una prueba de C# citada que no existe o una corrida que no devolvió reporte tienen que
terminar en FALLA (o en N-A con su motivo, si es un límite del entorno), nunca en OK. Y la limpieza no puede borrar nada
que no lleve la marca de agua, releída justo antes del DELETE.
"""
import base64
import io
import os
import sys
import unittest

_AQUI = os.path.dirname(os.path.abspath(__file__))
_PRUEBAS = os.path.dirname(_AQUI)
_HERRAMIENTAS = os.path.dirname(_PRUEBAS)
sys.path.insert(0, _PRUEBAS)
sys.path.insert(0, os.path.join(_HERRAMIENTAS, "construir", "tests"))

import preparacion_comun as pc  # noqa: E402
from sets import set_l  # noqa: E402
from cliente_simulado import ClienteSimulado  # noqa: E402


class _Ctx:
    def __init__(self, dv=None):
        self.dv = dv
        self.extra = {}


def _estado_final(caso):
    caso.cerrar()
    return caso.estado


TRX = """<?xml version="1.0" encoding="utf-8"?>
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <Results>
    <UnitTestResult testName="Sanic.Mppp.Plugins.Tests.Aceptacion.Clase.Una_prueba_simple" outcome="Passed" />
    <UnitTestResult testName="Sanic.Mppp.Plugins.Tests.Aceptacion.Clase.Una_teoria(codigo: 1)" outcome="Passed" />
    <UnitTestResult testName="Sanic.Mppp.Plugins.Tests.Aceptacion.Clase.Una_teoria(codigo: 2)" outcome="Failed" />
  </Results>
</TestRun>"""


class ParsearTrx(unittest.TestCase):
    def test_agrupa_por_metodo_sin_namespace_ni_clase_y_una_teoria_junta_sus_juegos_de_datos(self):
        r = set_l._parsear_trx(TRX)
        self.assertEqual(["Passed"], r["Una_prueba_simple"])
        self.assertEqual(["Passed", "Failed"], r["Una_teoria"])


def _reporte(*specs_por_describe):
    """Un reporte JSON de Playwright con la forma real: suites de primer nivel = archivos; anidadas = describes."""
    describes = []
    for titulo, specs in specs_por_describe:
        describes.append({"title": titulo, "specs": specs, "suites": []})
    return {"suites": [{"title": "casos/dn-04-catalogos-masivos.spec.mjs", "specs": [], "suites": describes}]}


def _spec(titulo, estado, razon=None, error=None):
    resultado = {"annotations": [{"type": "skip", "description": razon}] if razon else []}
    if error:
        resultado["error"] = {"message": error}
    return {"title": titulo, "tests": [{"status": estado, "annotations": [], "results": [resultado]}]}


class ParsearReporteGui(unittest.TestCase):
    def test_la_clave_es_describe_y_prueba_sin_el_archivo_y_trae_estado_motivo_y_error(self):
        pruebas = set_l._parsear_reporte_gui(_reporte(
            ("Grupo", [_spec("pasa", "expected"), _spec("salta", "skipped", razon="porque sí"),
                       _spec("falla", "unexpected", error="boom")])))
        self.assertEqual("expected", pruebas["Grupo › pasa"]["estado"])
        self.assertEqual("porque sí", pruebas["Grupo › salta"]["razon"])
        self.assertEqual("boom", pruebas["Grupo › falla"]["error"])
        self.assertNotIn("casos/dn-04-catalogos-masivos.spec.mjs › Grupo › pasa", pruebas)


class ChequearGui(unittest.TestCase):
    def _caso_con(self, pruebas, claves, salto_aceptado=None, papel="admin_planes"):
        ctx = _Ctx()
        ctx.extra["gui"] = {papel: {"pruebas": pruebas}}
        c = pc.Caso("L-99", "prueba")
        set_l._chequear_gui(c, ctx, papel, claves, salto_aceptado=salto_aceptado)
        return c

    def test_una_prueba_que_paso_da_ok(self):
        self.assertEqual("OK", _estado_final(self._caso_con({"G › a": {"estado": "expected", "razon": "", "error": ""}}, ["G › a"])))

    def test_una_prueba_saltada_sin_salto_aceptado_es_falla_nunca_ok(self):
        c = self._caso_con({"G › a": {"estado": "skipped", "razon": "cualquiera", "error": ""}}, ["G › a"])
        self.assertEqual("FALLA", _estado_final(c))

    def test_un_salto_se_acepta_solo_si_el_motivo_coincide(self):
        aceptado = self._caso_con({"G › a": {"estado": "skipped", "razon": set_l.SALTO_SIN_AREA.replace("\\", ""), "error": ""}},
                                  ["G › a"], salto_aceptado=set_l.SALTO_SIN_AREA)
        self.assertEqual("OK", _estado_final(aceptado))
        otro_motivo = self._caso_con({"G › a": {"estado": "skipped", "razon": "la sesión venció", "error": ""}},
                                     ["G › a"], salto_aceptado=set_l.SALTO_SIN_AREA)
        self.assertEqual("FALLA", _estado_final(otro_motivo))

    def test_una_prueba_que_no_esta_en_el_reporte_es_falla(self):
        self.assertEqual("FALLA", _estado_final(self._caso_con({}, ["G › renombrada"])))

    def test_una_corrida_sin_reporte_es_falla_y_un_limite_del_entorno_es_n_a(self):
        ctx = _Ctx()
        ctx.extra["gui"] = {"admin_planes": {"error": "Playwright no devolvió JSON válido"}}
        c = pc.Caso("L-99", "prueba")
        set_l._chequear_gui(c, ctx, "admin_planes", ["G › a"])
        self.assertEqual("FALLA", _estado_final(c))
        ctx.extra["gui"] = {"admin_planes": {"error": "'npx' no está", "entorno": True}}
        c = pc.Caso("L-99", "prueba")
        set_l._chequear_gui(c, ctx, "admin_planes", ["G › a"])
        self.assertEqual("N-A", _estado_final(c))


class ChequearCsharp(unittest.TestCase):
    def _caso(self, resultados, citadas):
        ctx = _Ctx()
        ctx.extra["csharp"] = resultados
        c = pc.Caso("L-99", "prueba")
        original = dict(set_l.PRUEBAS_CS)
        set_l.PRUEBAS_CS["L-99"] = citadas
        try:
            set_l._chequear_csharp(c, ctx, "L-99")
        finally:
            set_l.PRUEBAS_CS.clear()
            set_l.PRUEBAS_CS.update(original)
        return c

    def test_todas_las_citadas_pasaron_da_ok(self):
        self.assertEqual("OK", _estado_final(self._caso({"resultados": {"A": ["Passed"], "B": ["Passed", "Passed"]}}, ["A", "B"])))

    def test_una_citada_que_no_existe_en_el_trx_es_falla(self):
        self.assertEqual("FALLA", _estado_final(self._caso({"resultados": {"A": ["Passed"]}}, ["A", "Renombrada"])))

    def test_un_juego_de_datos_fallado_de_una_teoria_es_falla(self):
        self.assertEqual("FALLA", _estado_final(self._caso({"resultados": {"A": ["Passed", "Failed"]}}, ["A"])))

    def test_sin_suite_de_csharp_es_n_a(self):
        self.assertEqual("N-A", _estado_final(self._caso({"error": "dotnet no está"}, ["A"])))


class Excel(unittest.TestCase):
    def test_el_archivo_armado_tiene_la_hoja_y_los_encabezados_y_la_columna_resultado_se_lee_de_la_ultima(self):
        import openpyxl
        b64 = set_l._carga([["999980001", "ZZPCA0001001", "ZZP-Cliente 1", "a@b.com"]])
        libro = openpyxl.load_workbook(io.BytesIO(base64.b64decode(b64)), read_only=True)
        self.assertEqual("CargaCatalogos", libro.sheetnames[0])
        filas = list(libro.worksheets[0].iter_rows(values_only=True))
        self.assertEqual(tuple(set_l.ENCABEZADOS_CARGA), filas[0][:12])
        con_resultado = set_l._xlsx("CargaCatalogos", [set_l.ENCABEZADOS_CARGA, ["x"] * 11 + ["Error: algo"]])
        self.assertEqual({2: "Error: algo"}, set_l._columna_resultado(con_resultado))


def _clientes(*filas):
    return lambda ruta: (200, {"value": list(filas)}, {})


class LimpiarSoloLoMarcado(unittest.TestCase):
    """La regla de `marca_agua.py`: sin la marca, releída justo antes del DELETE, no se borra nada."""

    def _dv(self, cliente_del_plan, contacto_del_correo, nombre_del_cliente):
        dv = ClienteSimulado()
        dv.responder("GET", lambda r: r.startswith(set_l.AUT_CLIENTES + "?"), (200, {"value": []}, {}))
        dv.responder("GET", lambda r: r.startswith(set_l.PLANES + "?"), (200, {"value": [{"sanic_mppp_tbl_planid": "p1"}]}, {}))
        dv.responder("GET", lambda r: r.startswith(set_l.PLANES + "(p1)"),
                     (200, {"sanic_clienteid": cliente_del_plan}, {}))
        dv.responder("GET", lambda r: r.startswith(set_l.AUTORIZADOS + "?"),
                     (200, {"value": [{"sanic_mppp_tbl_autorizadoid": "a1", "sanic_nombrecontacto": contacto_del_correo}]}, {}))
        dv.responder("GET", lambda r: r.startswith(set_l.AUTORIZADOS + "(a1)"), (200, {"sanic_nombrecontacto": contacto_del_correo}, {}))
        dv.responder("GET", lambda r: r.startswith(set_l.CLIENTES + "?"),
                     (200, {"value": [{"sanic_mppp_tbl_clienteid": "c1", "sanic_nombre": nombre_del_cliente}]}, {}))
        dv.responder("GET", lambda r: r.startswith(set_l.CLIENTES + "(c1)"), (200, {"sanic_nombre": nombre_del_cliente}, {}))
        dv.responder("DELETE", lambda r: True, (204, {}, {}))
        return dv

    def _borrados(self, dv):
        return [l["ruta"] for l in dv.llamadas if l["metodo"] == "DELETE"]

    def test_lo_que_no_lleva_la_marca_sobrevive(self):
        dv = self._dv({"sanic_nombre": "Ferretería Real", "sanic_cifbac": "999980001"}, "Juan Real", "Ferretería Real")
        _, problemas = set_l._limpiar(dv)
        self.assertEqual([], self._borrados(dv))
        self.assertTrue(any("p1" in p for p in problemas), problemas)
        self.assertTrue(any("c1" in p for p in problemas), problemas)

    def test_lo_marcado_se_borra(self):
        dv = self._dv({"sanic_nombre": "ZZP-Cliente 1", "sanic_cifbac": "999980001"}, "ZZP-Maria", "ZZP-Cliente 1")
        borrados, problemas = set_l._limpiar(dv)
        self.assertEqual([], problemas)
        self.assertEqual(3, borrados)
        self.assertEqual({"sanic_mppp_tbl_plans(p1)", "sanic_mppp_tbl_autorizados(a1)", "sanic_mppp_tbl_clientes(c1)"},
                         set(self._borrados(dv)))

    def test_un_plan_de_un_cliente_marcado_pero_fuera_del_rango_de_cif_sobrevive(self):
        dv = self._dv({"sanic_nombre": "ZZP-Otro set", "sanic_cifbac": "123456789"}, "ZZP-Maria", "ZZP-Cliente 1")
        set_l._limpiar(dv)
        self.assertNotIn("sanic_mppp_tbl_plans(p1)", self._borrados(dv))


class Casos(unittest.TestCase):
    def test_son_los_57_del_archivo_en_orden_y_cada_uno_tiene_titulo(self):
        self.assertEqual(57, len(set_l.CASOS))
        self.assertEqual([f"l{i:02d}" for i in range(1, 58)], [f.__name__ for f in set_l.CASOS])
        self.assertEqual({f"L-{i:02d}" for i in range(1, 58)}, set(set_l.TITULOS))

    def test_l14_cubre_tambien_encabezados_en_otro_orden_y_capitalizacion(self):
        self.assertIn("Encabezados_en_otro_orden_son_MPPP_CAR_005_y_el_mensaje_nombra_los_que_no_coinciden", set_l.PRUEBAS_CS["L-14"])


if __name__ == "__main__":
    unittest.main()
