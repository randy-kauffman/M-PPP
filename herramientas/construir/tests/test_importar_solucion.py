"""Pruebas de `herramientas/construir/importar_solucion.py`. Nunca tocan la red.

Lo que más importa: que NO se importe nada cuando no hace falta (reimportar la
solución entera es la operación más pesada del proyecto), que el resto del
`customizations.xml` salga byte por byte igual al que entró, y que el éxito se
mida releyendo el entorno y no contando lo que pedía el playbook.
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

import importar_solucion as imp  # noqa: E402
from cliente_simulado import ClienteSimulado, FabricaCentinela  # noqa: E402
from fixtures import IDENTIDAD_VALIDA, armar_cliente_precondiciones_ok  # noqa: E402

IDENT = dict(IDENTIDAD_VALIDA, tipo_playbook="importacion", inventario="12.8")

APP_ID = "aaaa1111-2222-3333-4444-555566667777"
APP = "sanic_mppp_mda_mantenimientoppp"
SITEMAP = "sanic_mppp_sm_mantenimientoppp"
TABLAS = ["sanic_mppp_tbl_fila", "sanic_mppp_tbl_solicitud"]

COMPONENTE = {
    "tipo": "importacion",
    "idiomas": [1033, 3082],
    "cambios": [{"clase": "app_componentes", "app": APP, "sitemap": SITEMAP, "tablas": TABLAS}],
}

# Un customizations.xml chico pero con la misma forma que el real: dos apps,
# para que quede probado que se toca SOLO la que nombra el playbook.
CUSTOMIZATIONS = """<?xml version="1.0" encoding="utf-8"?>
<ImportExportXml>
  <Entities />
  <AppModules>
    <AppModule>
      <UniqueName>sanic_otra_app</UniqueName>
      <AppModuleComponents>
        <AppModuleComponent type="1" schemaName="entity" />
      </AppModuleComponents>
    </AppModule>
    <AppModule>
      <UniqueName>{app}</UniqueName>
      <ClientType>2</ClientType>
      <AppModuleComponents>
        <AppModuleComponent type="1" schemaName="entity" />
        <AppModuleComponent type="62" schemaName="{sitemap}" />
      </AppModuleComponents>
      <AppModuleRoleMaps />
    </AppModule>
  </AppModules>
  <Languages />
</ImportExportXml>
""".format(app=APP, sitemap=SITEMAP)


TABLA = TABLAS[0]
BOTON = {"id": "digitada", "funcion": "Sanic.Mppp.Comandos.digitada",
         "icono": "sanic_mppp_wr_svg_cmd_digitada",
         "etiqueta": {"1033": "Mark as entered", "3082": "Digitada"},
         "tooltip": {"1033": "Marks the selected rows as entered.",
                     "3082": "Marca como digitadas las filas seleccionadas."},
         "ubicaciones": ["HomepageGrid", "SubGrid"], "secuencia": 50}
CAMBIO_RIBBON = {"clase": "ribbon", "tabla": TABLA, "biblioteca": "sanic_mppp_wr_js_comandos",
                 "botones": [BOTON], "ocultar": [".Delete", ".Assign"]}

# Ribbon como lo devuelve `RetrieveEntityRibbon`: `.Assign` existe, `.Delete`
# solo en la pestaña de gráficos (que no se toca) y uno de otra tabla.
RIBBON = f"""<RibbonDefinitions>
  <Button Id="Mscrm.HomepageGrid.{TABLA}.Assign" Command="Mscrm.AssignSelectedRecord" />
  <Button Id="Mscrm.HomepageGrid.{TABLA}.Chart.Delete" Command="Mscrm.DeleteSelectedRecord" />
  <Button Id="Mscrm.HomepageGrid.{TABLAS[1]}.Assign" Command="Mscrm.AssignSelectedRecord" />
  <Button Id="Mscrm.HomepageGrid.{TABLA}.Edit" Command="Mscrm.EditSelectedRecord" />
</RibbonDefinitions>"""

RIBBON_VACIO = ("<RibbonDiffXml><CustomActions /><Templates>"
                '<RibbonTemplates Id="Mscrm.Templates"></RibbonTemplates></Templates>'
                "<CommandDefinitions /><RuleDefinitions><TabDisplayRules /><DisplayRules />"
                "<EnableRules /></RuleDefinitions><LocLabels /></RibbonDiffXml>")

CUST_CON_ENTIDADES = """<?xml version="1.0" encoding="utf-8"?>
<ImportExportXml>
  <Entities>
    <Entity>
      <Name LocalizedName="Otra" OriginalName="Otra">{otra}</Name>
      {ribbon}
    </Entity>
    <Entity>
      <Name LocalizedName="Fila" OriginalName="Fila">{tabla}</Name>
      <SavedQueries />
      {ribbon}
    </Entity>
  </Entities>
  <Languages />
</ImportExportXml>
""".format(tabla=TABLA, otra=TABLAS[1], ribbon=RIBBON_VACIO)


def zip_ribbon(xml=RIBBON):
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w", zipfile.ZIP_DEFLATED) as z:
        z.writestr("RibbonXml.xml", xml)
        z.writestr("[Content_Types].xml", "<Types />")
    return base64.b64encode(buf.getvalue()).decode("ascii")


def zip_de(cust=CUSTOMIZATIONS):
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w", zipfile.ZIP_DEFLATED) as z:
        z.writestr("customizations.xml", cust)
        z.writestr("solution.xml", "<ImportExportXml />")
        z.writestr("[Content_Types].xml", "<Types />")
    return buf.getvalue()


def comp(**cambios):
    d = copy.deepcopy(COMPONENTE)
    d.update(cambios)
    return d


def playbook(componente=None):
    componente = COMPONENTE if componente is None else componente
    return (
        "# Playbook: importacion · prueba\n\n"
        "## 1. Identidad\n\n```json\n" + json.dumps(IDENT, ensure_ascii=False, indent=2) + "\n```\n\n"
        "## 2. Qué se crea\n\n```json\n" + json.dumps(componente, ensure_ascii=False, indent=2) + "\n```\n\n"
        "## 3. Precondiciones\n\n(no hace falta para las pruebas)\n"
    )


class Base(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.TemporaryDirectory()
        self.addCleanup(self.dir.cleanup)
        self.ruta = os.path.join(self.dir.name, "playbook.md")

    def escribir(self, componente=None):
        with open(self.ruta, "w", encoding="utf-8") as f:
            f.write(playbook(componente))
        return self.ruta

    def armar(self, cliente, puestas_antes=(), puestas_despues=None, cust=CUSTOMIZATIONS, data="",
              steps_apagados_despues=(), falla_al_reencender=False):
        armar_cliente_precondiciones_ok(cliente, IDENT)
        self.puestas = list(puestas_antes)
        self.despues = TABLAS if puestas_despues is None else list(puestas_despues)
        self.importado = False

        cliente.responder("GET", lambda r: r.startswith("appmodules/Microsoft.Dynamics.CRM.RetrieveUnpublishedMultiple"),
                          (200, {"value": [{"appmoduleid": APP_ID, "uniquename": APP}]}, {}))
        cliente.responder("GET", lambda r: r.startswith("RetrieveAppComponents("),
                          lambda r, c, s: (200, {"value": [{"componenttype": 1, "objectid": "md-" + t}
                                                           for t in (self.despues if self.importado
                                                                     else self.puestas)]}, {}))
        cliente.responder("GET", lambda r: r.startswith("EntityDefinitions(md-"),
                          lambda r, c, s: (200, {"LogicalName": r.split("(md-")[1].split(")")[0]}, {}))
        cliente.responder("POST", "ExportSolution",
                          (200, {"ExportSolutionFile": base64.b64encode(zip_de(cust)).decode("ascii")}, {}))

        def importar(ruta, cuerpo, solucion):
            self.importado = True
            self.enviado = base64.b64decode(cuerpo["CustomizationFile"])
            return (204, {}, {})

        cliente.responder("POST", "ImportSolution", importar)
        cliente.responder("GET", lambda r: r.startswith("importjobs("),
                          (200, {"progress": 100.0, "data": data}, {}))
        cliente.responder("POST", "PublishXml", (204, {}, {}))
        self.steps_apagados_despues = list(steps_apagados_despues)
        # El doble devuelve el ID además del nombre, como la plataforma: la
        # herramienta lo necesita para volver a encender el step, no solo para
        # nombrarlo en un mensaje.
        cliente.responder("GET", lambda r: r.startswith("sdkmessageprocessingsteps?"),
                          lambda r, c, s: (200, {"value": [
                              {"name": n, "statecode": 1, "sdkmessageprocessingstepid": "id-" + n}
                              for n in (self.steps_apagados_despues if self.importado else [])]}, {}))
        cliente.responder("PATCH", lambda r: r.startswith("sdkmessageprocessingsteps("),
                          (400, {"error": "no se pudo"}, {}) if falla_al_reencender else (204, {}, {}))
        return cliente

    def cust_enviado(self):
        return zipfile.ZipFile(io.BytesIO(self.enviado)).read("customizations.xml").decode("utf-8")


class EdicionDelXml(unittest.TestCase):
    def test_se_reemplaza_solo_la_app_que_nombra_el_playbook(self):
        nuevo, hubo = imp.reemplazar_componentes(CUSTOMIZATIONS, APP, SITEMAP, TABLAS)
        self.assertTrue(hubo)
        otra = nuevo[nuevo.index("sanic_otra_app"):nuevo.index(APP)]
        self.assertIn('type="1" schemaName="entity"', otra)
        self.assertNotIn("sanic_mppp_tbl_fila", otra)

    def test_el_resto_del_archivo_no_se_toca(self):
        nuevo, _ = imp.reemplazar_componentes(CUSTOMIZATIONS, APP, SITEMAP, TABLAS)
        # Todo lo que no es el bloque de componentes de esa app sigue igual.
        for trozo in ('<?xml version="1.0" encoding="utf-8"?>', "<Entities />", "<Languages />",
                      "<ClientType>2</ClientType>", "<AppModuleRoleMaps />"):
            self.assertIn(trozo, nuevo)

    def test_la_basura_entity_desaparece_de_esa_app(self):
        nuevo, _ = imp.reemplazar_componentes(CUSTOMIZATIONS, APP, SITEMAP, TABLAS)
        mia = nuevo[nuevo.index(APP):]
        self.assertNotIn('schemaName="entity"', mia)
        for t in TABLAS:
            self.assertIn(f'type="1" schemaName="{t}"', mia)
        self.assertIn(f'type="62" schemaName="{SITEMAP}"', mia)

    def test_aplicar_dos_veces_no_cambia_nada(self):
        # Idempotencia: es lo que evita reimportar la solución al pedo.
        una, hubo1 = imp.reemplazar_componentes(CUSTOMIZATIONS, APP, SITEMAP, TABLAS)
        dos, hubo2 = imp.reemplazar_componentes(una, APP, SITEMAP, TABLAS)
        self.assertTrue(hubo1)
        self.assertFalse(hubo2)
        self.assertEqual(una, dos)

    def test_una_app_que_no_esta_en_el_xml_queda_bloqueada(self):
        with self.assertRaises(imp.Bloqueado) as ctx:
            imp.reemplazar_componentes(CUSTOMIZATIONS, "sanic_mppp_mda_fantasma", SITEMAP, TABLAS)
        self.assertIn("no contiene ninguna app", str(ctx.exception))

    def test_el_zip_conserva_los_demas_archivos(self):
        datos = imp.rearmar_zip(zip_de(), "<nuevo />")
        z = zipfile.ZipFile(io.BytesIO(datos))
        self.assertEqual(["customizations.xml", "solution.xml", "[Content_Types].xml"], z.namelist())
        self.assertEqual("<nuevo />", z.read("customizations.xml").decode("utf-8"))
        self.assertEqual("<ImportExportXml />", z.read("solution.xml").decode("utf-8"))


class RibbonGenerado(unittest.TestCase):
    def xml(self, cambio=None, ocultables=None):
        cambio = CAMBIO_RIBBON if cambio is None else cambio
        presentes = imp.botones_a_ocultar(RIBBON, TABLA, cambio["ocultar"]) if ocultables is None else ocultables
        return imp.ribbondiffxml(IDENT, cambio, COMPONENTE["idiomas"], presentes)

    def test_un_boton_va_en_cada_ubicacion_que_declara(self):
        x = self.xml()
        self.assertIn(f'Location="Mscrm.HomepageGrid.{TABLA}.MainTab.Management.Controls._children"', x)
        self.assertIn(f'Location="Mscrm.SubGrid.{TABLA}.MainTab.Management.Controls._children"', x)
        self.assertNotIn("Mscrm.Form.", x)

    def test_las_dos_ubicaciones_comparten_un_solo_CommandDefinition(self):
        x = self.xml()
        self.assertEqual(1, x.count("<CommandDefinition "))
        self.assertEqual(2, x.count("<CustomAction "))

    def test_el_comando_llama_al_javascript_con_las_filas_seleccionadas(self):
        x = self.xml()
        self.assertIn('FunctionName="Sanic.Mppp.Comandos.digitada"', x)
        self.assertIn('Library="$webresource:sanic_mppp_wr_js_comandos"', x)
        self.assertIn('<CrmParameter Value="SelectedControlSelectedItemIds" />', x)

    def test_cada_etiqueta_lleva_un_texto_por_idioma(self):
        # En el XML del ribbon la plataforma NO sustituye.
        x = self.xml()
        self.assertEqual(x.count('languagecode="1033"'), x.count('languagecode="3082"'))
        self.assertIn('description="Digitada" languagecode="3082"', x)
        self.assertIn('description="Mark as entered" languagecode="1033"', x)

    def test_solo_se_oculta_lo_que_existe_y_nunca_la_pestana_de_graficos(self):
        x = self.xml()
        self.assertIn(f'Location="Mscrm.HomepageGrid.{TABLA}.Assign"', x)
        # `.Delete` solo existe bajo `.Chart.`, que no se toca.
        self.assertNotIn(".Chart.", x)
        # Y nunca un botón de OTRA tabla.
        self.assertNotIn(TABLAS[1], x)

    def test_nunca_se_sobrescribe_un_CommandDefinition_de_la_plataforma(self):
        # Los comandos OOB son GLOBALES: tocarlos apagaría ese botón en TODAS
        # las tablas del entorno. Se oculta por el id del BOTÓN.
        x = self.xml()
        self.assertNotIn('<CommandDefinition Id="Mscrm.', x)
        self.assertNotIn("Mscrm.AssignSelectedRecord", x)

    def test_generar_dos_veces_da_el_mismo_texto(self):
        self.assertEqual(self.xml(), self.xml())

    def test_un_hide_ya_declarado_no_se_pierde_cuando_el_boton_ya_no_se_ve(self):
        # La trampa: los ocultables salen del ribbon REAL, y un botón ya oculto
        # NO aparece ahí. Sin esto, la segunda corrida lo des-oculta. Pasó de
        # verdad en Dev el 2026-09-22: importar las 10 tablas revirtió la que
        # ya estaba hecha.
        primero, _ = imp.reemplazar_ribbon(CUST_CON_ENTIDADES, TABLA, self.xml())
        # Ahora el ribbon real ya no muestra `.Assign`: está oculto.
        ribbon_sin_assign = RIBBON.replace(f'<Button Id="Mscrm.HomepageGrid.{TABLA}.Assign" '
                                           'Command="Mscrm.AssignSelectedRecord" />', "")
        ocultables = imp.ocultables_de_la_tabla(primero, ribbon_sin_assign, TABLA, CAMBIO_RIBBON["ocultar"])
        self.assertIn(f"Mscrm.HomepageGrid.{TABLA}.Assign", ocultables)
        _, hubo = imp.reemplazar_ribbon(primero, TABLA, self.xml(ocultables=ocultables))
        self.assertFalse(hubo, "la segunda corrida no tiene que cambiar nada")

    def test_un_hide_que_el_playbook_ya_no_pide_se_suelta(self):
        primero, _ = imp.reemplazar_ribbon(CUST_CON_ENTIDADES, TABLA, self.xml())
        ocultables = imp.ocultables_de_la_tabla(primero, RIBBON, TABLA, [".Delete"])
        self.assertEqual([], ocultables)

    def test_se_reemplaza_solo_el_ribbon_de_la_tabla_nombrada(self):
        nuevo, hubo = imp.reemplazar_ribbon(CUST_CON_ENTIDADES, TABLA, self.xml())
        self.assertTrue(hubo)
        otra = nuevo[:nuevo.index(TABLA)]
        self.assertIn("<CustomActions />", otra)
        self.assertNotIn("Sanic.Mppp.Comandos", otra)

    def test_reemplazar_dos_veces_no_cambia_nada(self):
        una, hubo1 = imp.reemplazar_ribbon(CUST_CON_ENTIDADES, TABLA, self.xml())
        dos, hubo2 = imp.reemplazar_ribbon(una, TABLA, self.xml())
        self.assertTrue(hubo1)
        self.assertFalse(hubo2)

    def test_una_tabla_que_no_esta_en_la_solucion_queda_bloqueada(self):
        with self.assertRaises(imp.Bloqueado) as ctx:
            imp.reemplazar_ribbon(CUST_CON_ENTIDADES, "sanic_mppp_tbl_fantasma", self.xml())
        self.assertIn("no contiene la tabla", str(ctx.exception))

    def test_un_texto_con_comillas_no_rompe_el_atributo(self):
        import copy as _copy
        cambio = _copy.deepcopy(CAMBIO_RIBBON)
        cambio["botones"][0]["etiqueta"]["3082"] = 'Marcar "ya"'
        x = self.xml(cambio)
        self.assertIn("&quot;ya&quot;", x)
        self.assertNotIn('description="Marcar "ya""', x)


def _boton_menu(**cambios):
    """Un botón con menú desplegable (DN-04, M-PPP, 2026-10-01): sin `funcion`, con `menu`."""
    b = {"id": "cargamasiva", "icono": "sanic_mppp_wr_svg_cmdcargamasiva",
         "regla": "Sanic.Mppp.Comandos.esAdministradorDePlanes",
         "etiqueta": {"1033": "Bulk load", "3082": "Carga masiva"},
         "tooltip": {"1033": "Loads or downloads the bulk Excel.", "3082": "Carga o descarga el Excel masivo."},
         "ubicaciones": ["HomepageGrid"], "secuencia": 80, "idioma_del_texto": 3082,
         "menu": [
             {"id": "cargar", "funcion": "Sanic.Mppp.Comandos.cargarCatalogos", "secuencia": 10,
              "etiqueta": {"1033": "Upload file", "3082": "Cargar archivo"},
              "tooltip": {"1033": "Uploads the Excel.", "3082": "Sube el Excel."}},
             {"id": "plantilla", "funcion": "Sanic.Mppp.Comandos.plantillaCarga", "secuencia": 20,
              "etiqueta": {"1033": "Download template", "3082": "Descargar plantilla"},
              "tooltip": {"1033": "Downloads the empty template.", "3082": "Descarga la plantilla vacía."}},
         ]}
    b.update(cambios)
    return b


class RibbonConMenu(unittest.TestCase):
    """Menú desplegable (`FlyoutAnchor`): el patrón es el de la plataforma, sacado del ribbon REAL de Dev el 2026-10-01
    (`Mscrm.HomepageGrid.<tabla>.Flows.RefreshCommandBar.Flows`): ancla con Command SIN acciones (solo reglas),
    Menu > MenuSection (DisplayMode Menu16) > Controls > un Button por opción, cada uno con su propio Command."""

    def xml(self, boton=None, extra=()):
        cambio = dict(CAMBIO_RIBBON, botones=[boton or _boton_menu(), *extra])
        return imp.ribbondiffxml(IDENT, cambio, COMPONENTE["idiomas"], [])

    def test_el_ancla_es_un_FlyoutAnchor_con_menu_y_un_boton_por_opcion(self):
        x = self.xml()
        base = "sanic.mppp." + TABLA.split("_")[-1] + ".cargamasiva"
        self.assertIn(f'<FlyoutAnchor Id="{base}.HomepageGrid.FlyoutAnchor"', x)
        self.assertIn(f'Command="{base}.Command"', x)
        self.assertIn(f'<Menu Id="{base}.HomepageGrid.Menu">', x)
        self.assertIn(f'<MenuSection Id="{base}.HomepageGrid.MenuSection" Sequence="10" DisplayMode="Menu16">', x)
        self.assertIn(f'<Controls Id="{base}.HomepageGrid.Controls">', x)
        self.assertIn(f'<Button Command="{base}.cargar.Command" Id="{base}.HomepageGrid.cargar.Button"', x)
        self.assertIn(f'<Button Command="{base}.plantilla.Command" Id="{base}.HomepageGrid.plantilla.Button"', x)
        self.assertIn('ModernImage="$webresource:sanic_mppp_wr_svg_cmdcargamasiva"', x)
        self.assertLess(x.index(".cargar.Button"), x.index(".plantilla.Button"))

    def test_el_comando_del_ancla_no_tiene_acciones_y_lleva_la_regla_de_visibilidad(self):
        x = self.xml()
        base = "sanic.mppp." + TABLA.split("_")[-1] + ".cargamasiva"
        i = x.index(f'<CommandDefinition Id="{base}.Command">')
        ancla = x[i:x.index("</CommandDefinition>", i)]
        self.assertIn("<Actions />", ancla)
        self.assertIn(f'<EnableRule Id="{base}.EnableRule" />', ancla)

    def test_ni_el_ancla_ni_las_opciones_exigen_filas_seleccionadas(self):
        # Cargar un archivo no depende de la selección; con SelectionCountAtLeastOne el menú quedaría oculto en UCI.
        x = self.xml()
        self.assertNotIn("Mscrm.SelectionCountAtLeastOne", x)

    def test_cada_opcion_llama_a_su_funcion_con_el_control_para_refrescar(self):
        x = self.xml()
        base = "sanic.mppp." + TABLA.split("_")[-1] + ".cargamasiva"
        for opcion, funcion in (("cargar", "Sanic.Mppp.Comandos.cargarCatalogos"), ("plantilla", "Sanic.Mppp.Comandos.plantillaCarga")):
            i = x.index(f'<CommandDefinition Id="{base}.{opcion}.Command">')
            cuerpo = x[i:x.index("</CommandDefinition>", i)]
            self.assertIn(f'FunctionName="{funcion}"', cuerpo)
            self.assertIn('<CrmParameter Value="SelectedControl" />', cuerpo)
            self.assertIn(f'<EnableRule Id="{base}.EnableRule" />', cuerpo)

    def test_ancla_y_opciones_llevan_texto_fijo_en_el_idioma_declarado_y_ningun_LocLabel(self):
        # El modo de texto fijo (`idioma_del_texto`). Learn: el ribbon admite texto directo ("direct entry of text"), que
        # no depende de los idiomas instalados. Ya no es la regla de los menús: D-74 (2026-10-02) probó que $LocLabels
        # funciona dentro de un FlyoutAnchor con la metadata regenerada después de importar.
        x = self.xml()
        self.assertNotIn("$LocLabels:", x)
        self.assertNotIn("<LocLabel ", x)
        self.assertIn('LabelText="Carga masiva"', x)
        self.assertIn('LabelText="Cargar archivo"', x)
        self.assertIn('LabelText="Descargar plantilla"', x)
        self.assertIn('ToolTipDescription="Descarga la plantilla vacía."', x)
        self.assertIn('Alt="Carga masiva"', x)

    def test_el_texto_fijo_se_escapa(self):
        b = _boton_menu()
        b["etiqueta"]["3082"] = 'Carga "masiva" & más'
        self.assertIn('LabelText="Carga &quot;masiva&quot; &amp; más"', self.xml(b))

    def test_un_menu_en_dos_ubicaciones_comparte_los_comandos(self):
        x = self.xml(_boton_menu(ubicaciones=["HomepageGrid", "SubGrid"]))
        self.assertEqual(2, x.count("<FlyoutAnchor "))
        self.assertEqual(3, x.count("<CommandDefinition "))   # ancla + 2 opciones, una sola vez

    def test_un_boton_comun_al_lado_del_menu_no_cambia(self):
        x = self.xml(extra=[BOTON])
        self.assertIn("Mscrm.SelectionCountAtLeastOne", x)   # el botón común sigue exigiendo selección
        self.assertIn('FunctionName="Sanic.Mppp.Comandos.digitada"', x)

    def test_generar_dos_veces_da_el_mismo_texto(self):
        self.assertEqual(self.xml(), self.xml())


def _boton_menu_localizado():
    """El mismo menú, con `etiquetas_localizadas` (la forma recomendada desde D-74, 2026-10-02) en vez de texto fijo."""
    b = _boton_menu()
    del b["idioma_del_texto"]
    b["etiquetas_localizadas"] = True
    return b


class RibbonConMenuLocalizado(unittest.TestCase):
    """Un menú con `$LocLabels`. D-74 (2026-10-02) probó en Dev que Unified Interface las resuelve dentro de un
    `FlyoutAnchor` cuando la metadata se regenera DESPUÉS de importar ("Bulk load" en una interfaz en inglés); el
    "LabelText" del 2026-10-01 fue metadata vieja."""

    def xml(self):
        cambio = dict(CAMBIO_RIBBON, botones=[_boton_menu_localizado()])
        return imp.ribbondiffxml(IDENT, cambio, COMPONENTE["idiomas"], [])

    def test_ancla_y_opciones_apuntan_a_su_loclabel_y_no_llevan_texto_fijo(self):
        x = self.xml()
        base = "sanic.mppp." + TABLA.split("_")[-1] + ".cargamasiva"
        self.assertIn(f'LabelText="$LocLabels:{base}.Button.LabelText"', x)
        self.assertIn(f'ToolTipDescription="$LocLabels:{base}.Button.ToolTipDescription"', x)
        self.assertIn(f'LabelText="$LocLabels:{base}.cargar.Button.LabelText"', x)
        self.assertIn(f'LabelText="$LocLabels:{base}.plantilla.Button.LabelText"', x)
        self.assertNotIn('LabelText="Carga masiva"', x)
        self.assertNotIn('LabelText="Cargar archivo"', x)

    def test_cada_loclabel_lleva_un_texto_por_idioma(self):
        x = self.xml()
        base = "sanic.mppp." + TABLA.split("_")[-1] + ".cargamasiva"
        for id_, ingles, espanol in ((f"{base}.Button.LabelText", "Bulk load", "Carga masiva"),
                                     (f"{base}.cargar.Button.LabelText", "Upload file", "Cargar archivo"),
                                     (f"{base}.plantilla.Button.LabelText", "Download template", "Descargar plantilla")):
            i = x.index(f'<LocLabel Id="{id_}">')
            etiqueta = x[i:x.index("</LocLabel>", i)]
            self.assertIn(f'<Title description="{ingles}" languagecode="1033" />', etiqueta)
            self.assertIn(f'<Title description="{espanol}" languagecode="3082" />', etiqueta)

    def test_la_estructura_del_menu_no_cambia(self):
        x = self.xml()
        base = "sanic.mppp." + TABLA.split("_")[-1] + ".cargamasiva"
        self.assertIn(f'<FlyoutAnchor Id="{base}.HomepageGrid.FlyoutAnchor"', x)
        self.assertIn(f'<Button Command="{base}.cargar.Command" Id="{base}.HomepageGrid.cargar.Button"', x)
        self.assertNotIn("Mscrm.SelectionCountAtLeastOne", x)


class VerificacionDelMenu(unittest.TestCase):
    """La verificación tiene que acompañar a lo que se genera (2026-10-01: la primera versión de los menús generaba un
    FlyoutAnchor y la verificación seguía buscando un Button; el ribbon quedó bien en Dev y la herramienta dijo error)."""

    def verificar(self, ribbon_real, boton=None):
        cambio = dict(CAMBIO_RIBBON, botones=[boton or _boton_menu()], ocultar=[])
        original = imp.ribbon_de_la_tabla
        imp.ribbon_de_la_tabla = lambda dv, tabla: ribbon_real
        try:
            return imp._verificar_ribbon(None, cambio, IDENT)
        finally:
            imp.ribbon_de_la_tabla = original

    def ribbon_generado(self, boton=None):
        cambio = dict(CAMBIO_RIBBON, botones=[boton or _boton_menu()], ocultar=[])
        return imp.ribbondiffxml(IDENT, cambio, COMPONENTE["idiomas"], [])

    def test_un_menu_bien_desplegado_no_falta_nada(self):
        self.assertEqual([], self.verificar(self.ribbon_generado()))

    def test_falta_el_ancla_del_menu(self):
        base = "sanic.mppp." + TABLA.split("_")[-1] + ".cargamasiva"
        real = self.ribbon_generado().replace(f'Id="{base}.HomepageGrid.FlyoutAnchor"', 'Id="otra.cosa"')
        self.assertTrue(any("cargamasiva" in f and "HomepageGrid" in f for f in self.verificar(real)), self.verificar(real))

    def test_falta_una_opcion_del_menu(self):
        base = "sanic.mppp." + TABLA.split("_")[-1] + ".cargamasiva"
        real = self.ribbon_generado().replace(f'Id="{base}.HomepageGrid.plantilla.Button"', 'Id="otra.cosa"')
        faltan = self.verificar(real)
        self.assertTrue(any("plantilla" in f for f in faltan), faltan)

    def test_un_menu_con_el_texto_viejo_no_se_da_por_bueno(self):
        # Un menú desplegado con $LocLabels (la primera versión) tiene los mismos ids: sin mirar el texto, la herramienta
        # diría ya_existia y nunca importaría el texto fijo.
        real = self.ribbon_generado().replace('LabelText="Carga masiva"', 'LabelText="$LocLabels:x.Button.LabelText"')
        faltan = self.verificar(real)
        self.assertTrue(any("Carga masiva" in f for f in faltan), faltan)

    def test_un_menu_localizado_bien_desplegado_no_falta_nada(self):
        # RetrieveEntityRibbon devuelve la referencia SIN resolver (`LabelText="$LocLabels:..."`), verificado en Dev el
        # 2026-10-02 con los botones de Fila.
        b = _boton_menu_localizado()
        self.assertEqual([], self.verificar(self.ribbon_generado(b), b))

    def test_un_menu_localizado_que_sigue_con_texto_fijo_no_se_da_por_bueno(self):
        # El estado de Dev antes del experimento: sin esto, la herramienta diría ya_existia y nunca importaría el cambio.
        faltan = self.verificar(self.ribbon_generado(), _boton_menu_localizado())
        self.assertTrue(any("$LocLabels" in f for f in faltan), faltan)

    def test_una_opcion_que_no_llama_a_su_funcion(self):
        real = self.ribbon_generado().replace('FunctionName="Sanic.Mppp.Comandos.plantillaCarga"', 'FunctionName="Otra.Cosa"')
        faltan = self.verificar(real)
        self.assertTrue(any("plantillaCarga" in f for f in faltan), faltan)


class ValidacionDelMenu(Base):
    """Misma vía que `ValidacionOffline`: el playbook se valida entero sin red; un error no llama al entorno."""

    def componente(self, boton):
        return dict(copy.deepcopy(COMPONENTE), cambios=[dict(CAMBIO_RIBBON, botones=[boton])])

    def rechaza(self, boton, fragmento):
        centinela = FabricaCentinela()
        estado, _, detalle = imp.construir(self.escribir(self.componente(boton)), False, centinela)
        self.assertEqual("error", estado, detalle)
        self.assertIn(fragmento, detalle)
        self.assertFalse(centinela.llamada)

    def test_un_menu_bien_armado_se_acepta(self):
        datos = json.loads(json.dumps(self.componente(_boton_menu())))
        imp.validar_playbook(datos, IDENT)   # no lanza

    def test_funcion_y_menu_juntos_se_rechaza(self):
        self.rechaza(_boton_menu(funcion="Sanic.Mppp.Comandos.x"), "funcion")

    def test_un_menu_vacio_se_rechaza(self):
        self.rechaza(_boton_menu(menu=[]), "menu")

    def test_una_opcion_sin_funcion_se_rechaza(self):
        b = _boton_menu()
        del b["menu"][0]["funcion"]
        self.rechaza(b, "funcion")

    def test_una_opcion_con_una_clave_de_mas_se_rechaza(self):
        b = _boton_menu()
        b["menu"][0]["ubicaciones"] = ["HomepageGrid"]
        self.rechaza(b, "ubicaciones")

    def test_ids_de_opcion_repetidos_se_rechazan(self):
        b = _boton_menu()
        b["menu"][1]["id"] = "cargar"
        self.rechaza(b, "cargar")

    def test_una_opcion_sin_texto_en_un_idioma_se_rechaza(self):
        b = _boton_menu()
        del b["menu"][0]["etiqueta"]["3082"]
        self.rechaza(b, "3082")

    def test_un_menu_sin_idioma_del_texto_se_rechaza(self):
        b = _boton_menu()
        del b["idioma_del_texto"]
        self.rechaza(b, "idioma_del_texto")

    def test_un_menu_localizado_se_acepta(self):
        imp.validar_playbook(json.loads(json.dumps(self.componente(_boton_menu_localizado()))), IDENT)   # no lanza

    def test_idioma_del_texto_y_etiquetas_localizadas_juntos_se_rechaza(self):
        self.rechaza(dict(_boton_menu_localizado(), idioma_del_texto=3082), "etiquetas_localizadas")

    def test_etiquetas_localizadas_en_false_se_rechaza(self):
        b = _boton_menu_localizado()
        b["etiquetas_localizadas"] = False
        self.rechaza(b, "etiquetas_localizadas")

    def test_etiquetas_localizadas_en_un_boton_comun_se_rechaza(self):
        self.rechaza(dict(BOTON, etiquetas_localizadas=True), "etiquetas_localizadas")

    def test_un_idioma_del_texto_fuera_de_los_idiomas_se_rechaza(self):
        self.rechaza(_boton_menu(idioma_del_texto=1031), "idioma_del_texto")

    def test_idioma_del_texto_en_un_boton_comun_se_rechaza(self):
        b = dict(BOTON, idioma_del_texto=3082)
        self.rechaza(b, "idioma_del_texto")

    def test_un_boton_sin_funcion_ni_menu_se_rechaza(self):
        b = _boton_menu()
        del b["menu"]
        self.rechaza(b, "funcion")


class ValidacionOffline(Base):
    def rechaza(self, componente, fragmento):
        centinela = FabricaCentinela()
        estado, _, detalle = imp.construir(self.escribir(componente), False, centinela)
        self.assertEqual("error", estado, detalle)
        self.assertIn(fragmento, detalle)
        self.assertFalse(centinela.llamada)

    def test_una_clase_desconocida_se_rechaza(self):
        malo = copy.deepcopy(COMPONENTE)
        malo["cambios"][0]["clase"] = "lo_que_sea"
        self.rechaza(malo, "las clases admitidas son")

    def test_una_tabla_sin_el_prefijo_del_publisher_se_rechaza(self):
        malo = copy.deepcopy(COMPONENTE)
        malo["cambios"][0]["tablas"] = ["account"]
        self.rechaza(malo, "no llevan el prefijo del publisher")

    def test_tablas_repetidas_se_rechazan(self):
        malo = copy.deepcopy(COMPONENTE)
        malo["cambios"][0]["tablas"] = TABLAS + [TABLAS[0]]
        self.rechaza(malo, "tiene repetidos")

    def test_la_misma_app_en_dos_cambios_se_rechaza(self):
        malo = copy.deepcopy(COMPONENTE)
        malo["cambios"] = malo["cambios"] + copy.deepcopy(malo["cambios"])
        self.rechaza(malo, "aparece en más de un cambio")


class ContraElEntorno(Base):
    def test_si_ya_estan_las_tablas_no_se_importa_nada(self):
        # Reimportar la solución entera al pedo es lo más caro que puede hacer
        # esta herramienta.
        cliente = self.armar(ClienteSimulado(), puestas_antes=TABLAS)
        estado, _, detalle = imp.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("ya_existia", estado, detalle)
        rutas = [l["ruta"] for l in cliente.llamadas if l["metodo"] == "POST"]
        self.assertNotIn("ImportSolution", rutas)
        self.assertNotIn("ExportSolution", rutas)

    def test_solo_verificar_nunca_importa(self):
        cliente = self.armar(ClienteSimulado(), puestas_antes=[])
        estado, _, detalle = imp.construir(self.escribir(), True, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("le faltan", detalle)
        rutas = [l["ruta"] for l in cliente.llamadas if l["metodo"] == "POST"]
        self.assertNotIn("ImportSolution", rutas)

    def test_el_ciclo_completo_exporta_edita_importa_publica_y_verifica(self):
        cliente = self.armar(ClienteSimulado(), puestas_antes=[])
        estado, _, detalle = imp.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("creado", estado, detalle)
        rutas = [l["ruta"] for l in cliente.llamadas if l["metodo"] == "POST"]
        self.assertEqual(["ExportSolution", "ImportSolution", "PublishXml"],
                         [r for r in rutas if r in ("ExportSolution", "ImportSolution", "PublishXml")])
        enviado = self.cust_enviado()
        for t in TABLAS:
            self.assertIn(f'type="1" schemaName="{t}"', enviado)

    def test_si_despues_de_importar_falta_una_tabla_no_se_informa_exito(self):
        # Un 204 de ImportSolution no prueba nada: se relee el entorno.
        cliente = self.armar(ClienteSimulado(), puestas_antes=[], puestas_despues=[TABLAS[0]])
        estado, _, detalle = imp.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("error", estado, detalle)
        self.assertIn("le siguen faltando", detalle)
        self.assertIn(TABLAS[1], detalle)

    def test_si_el_import_deshabilita_un_step_la_herramienta_lo_vuelve_a_encender(self):
        # Pasó de verdad: el ImportSolution del 2026-09-22 dejó apagados los 20
        # steps del proyecto y nadie se enteró hasta el día siguiente. Un step
        # apagado NO da error: simplemente deja de correr.
        #
        # Detectarlo no alcanzaba. El 2026-09-23 la herramienta lo detectó TRES
        # veces en una hora y las tres hubo que reactivar a mano, leyendo el
        # mensaje y corriendo un script aparte. Un paso manual después de una
        # operación que ya falló es un paso que alguien va a saltear, y el costo
        # de saltearlo es la lógica de servidor muerta sin que nada avise.
        # La herramienta sabe EXACTAMENTE cuáles apagó: los reenciende ella.
        cliente = self.armar(ClienteSimulado(), puestas_antes=[],
                             steps_apagados_despues=["MPPP - Transicion de Fila - Update"])
        estado, _, detalle = imp.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("creado", estado, detalle)
        self.assertIn("volvió a encender", detalle)
        self.assertIn("Transicion de Fila", detalle)

    def test_si_un_step_no_se_puede_reencender_eso_si_es_un_error(self):
        # Reparar puede fallar. Si falla, NO se informa éxito: quedaría lógica de
        # servidor apagada y el mensaje diciendo que todo salió bien.
        cliente = self.armar(ClienteSimulado(), puestas_antes=[],
                             steps_apagados_despues=["MPPP - Transicion de Fila - Update"],
                             falla_al_reencender=True)
        estado, _, detalle = imp.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("error", estado, detalle)
        self.assertIn("NO SE PUDO VOLVER A ENCENDER", detalle)

    def test_un_componente_con_failure_en_el_importjob_no_se_informa_exito(self):
        cliente = self.armar(ClienteSimulado(), puestas_antes=[],
                             data='<result result="failure" errorcode="0x80040265" />')
        estado, _, detalle = imp.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("error", estado, detalle)
        self.assertIn("result=\"failure\"", detalle)

    def armar_ribbon(self, cliente, despues_ok=True, cust=CUST_CON_ENTIDADES):
        armar_cliente_precondiciones_ok(cliente, IDENT)
        self.importado = False
        base = f"{IDENT['prefijo']}.{IDENT['abrev']}.{TABLA.split('_')[-1]}.digitada"
        # Después de importar, el ribbon trae los botones propios y ya no el genérico.
        despues = ("<RibbonDefinitions>"
                   # El botón sale con `ModernImage`, que es el icono que mira Unified
                   # Interface: sin él la verificación tiene que fallar, y este doble
                   # representa el ribbon TAL COMO QUEDA después de importar.
                   + "".join(f'<Button Id="{base}.{u}.Button" Command="{base}.Command" '
                             f'ModernImage="$webresource:{BOTON["icono"]}" />'
                             for u in BOTON["ubicaciones"])
                   + (f'<Button Id="Mscrm.HomepageGrid.{TABLA}.Edit" Command="Mscrm.EditSelectedRecord" />'
                      if despues_ok
                      else f'<Button Id="Mscrm.HomepageGrid.{TABLA}.Assign" Command="Mscrm.Assign" />')
                   + "</RibbonDefinitions>")
        cliente.responder("GET", lambda r: r.startswith("RetrieveEntityRibbon("),
                          lambda r, c, s: (200, {"CompressedEntityXml":
                                                 zip_ribbon(despues if self.importado else RIBBON)}, {}))
        cliente.responder("POST", "ExportSolution",
                          (200, {"ExportSolutionFile": base64.b64encode(zip_de(cust)).decode("ascii")}, {}))

        def importar(ruta, cuerpo, solucion):
            self.importado = True
            self.enviado = base64.b64decode(cuerpo["CustomizationFile"])
            return (204, {}, {})

        cliente.responder("POST", "ImportSolution", importar)
        cliente.responder("GET", lambda r: r.startswith("importjobs("),
                          (200, {"progress": 100.0, "data": ""}, {}))
        cliente.responder("POST", "PublishXml", (204, {}, {}))
        cliente.responder("GET", lambda r: r.startswith("sdkmessageprocessingsteps?"),
                          (200, {"value": []}, {}))
        cliente.responder("PATCH", lambda r: r.startswith("sdkmessageprocessingsteps("), (204, {}, {}))
        return cliente

    def test_el_ciclo_de_ribbon_importa_publica_la_tabla_y_verifica(self):
        cliente = self.armar_ribbon(ClienteSimulado())
        estado, _, detalle = imp.construir(self.escribir(comp(cambios=[CAMBIO_RIBBON])), False, lambda: cliente)
        self.assertEqual("creado", estado, detalle)
        # Un cambio de ribbon no se ve hasta publicar la TABLA.
        publicaciones = [l["cuerpo"]["ParameterXml"] for l in cliente.llamadas if l["ruta"] == "PublishXml"]
        self.assertTrue(any(f"<entity>{TABLA}</entity>" in p for p in publicaciones), publicaciones)
        self.assertIn("Sanic.Mppp.Comandos.digitada", self.cust_enviado())

    def test_un_ribbon_importado_avisa_que_hay_que_regenerar_la_metadata(self):
        # DN-04 (2026-10-01): RetrieveEntityRibbon ya traía los menús nuevos y la herramienta dijo "creado", pero
        # la app siguió mostrando la barra vieja hasta "Regenerate ribbon metadata" (Command Checker). Publicar NO
        # regenera la metadata compilada que lee la app (GetClientMetadata), y la herramienta no puede hacerlo: el
        # éxito tiene que decir el paso manual que falta.
        cliente = self.armar_ribbon(ClienteSimulado())
        estado, _, detalle = imp.construir(self.escribir(comp(cambios=[CAMBIO_RIBBON])), False, lambda: cliente)
        self.assertEqual("creado", estado, detalle)
        self.assertIn("Regenerate ribbon metadata", detalle)
        self.assertIn("ribbondebug=true", detalle)

    def test_sin_ribbon_importado_no_hay_aviso_de_regenerar(self):
        cliente = self.armar(ClienteSimulado(), puestas_antes=[])
        estado, _, detalle = imp.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("creado", estado, detalle)
        self.assertNotIn("Regenerate ribbon metadata", detalle)

    def test_si_el_generico_sigue_a_la_vista_despues_de_importar_no_se_informa_exito(self):
        cliente = self.armar_ribbon(ClienteSimulado(), despues_ok=False)
        estado, _, detalle = imp.construir(self.escribir(comp(cambios=[CAMBIO_RIBBON])), False, lambda: cliente)
        self.assertEqual("error", estado, detalle)
        self.assertIn("siguen a la vista", detalle)

    def test_solo_verificar_con_ribbon_nunca_importa(self):
        cliente = self.armar_ribbon(ClienteSimulado())
        estado, _, detalle = imp.construir(self.escribir(comp(cambios=[CAMBIO_RIBBON])), True, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertNotIn("ImportSolution", [l["ruta"] for l in cliente.llamadas])

    def test_una_app_que_no_existe_en_el_entorno_queda_bloqueada_sin_exportar(self):
        cliente = ClienteSimulado()
        armar_cliente_precondiciones_ok(cliente, IDENT)
        cliente.responder("GET", lambda r: r.startswith("appmodules/Microsoft.Dynamics.CRM.RetrieveUnpublishedMultiple"),
                          (200, {"value": []}, {}))
        estado, _, detalle = imp.construir(self.escribir(), False, lambda: cliente)
        self.assertEqual("bloqueado", estado, detalle)
        self.assertIn("hay que crearla primero", detalle)
        self.assertFalse(any(l["ruta"] == "ExportSolution" for l in cliente.llamadas))


class DenunciaTablasSinBloque(ContraElEntorno):
    """D-69: `sanic_mppp_tbl_autorizacioncliente` entró a la app el 2026-09-27 y
    nunca se agregó a `comandos.md`; nada lo denunció hasta que un caso de GUI
    miró el DOM real, semanas después. La clave opcional 'app' tiene que hacer
    que `--solo-verificar` (y una corrida real) DEJEN de salir en verde cuando la
    app tiene una tabla sin cambio `ribbon`."""

    # Cambio `ribbon` SIN botones propios (como las tablas de catálogo reales:
    # `sanic_mppp_tbl_cliente` y compañía), para poder armar un ribbon "ya
    # aplicado" sin tener que simular todo el ciclo de un botón con icono.
    CAMBIO_SIN_BOTONES = {"clase": "ribbon", "tabla": TABLA, "biblioteca": "sanic_mppp_wr_js_comandos",
                          "botones": [], "ocultar": [".Assign"]}
    RIBBON_YA_OCULTO = "<RibbonDefinitions></RibbonDefinitions>"  # `.Assign` ya no está: nada que importar.

    def _cliente_con_tablas_de_app(self, tablas_app, pendiente=True):
        """`pendiente=True`: el ribbon real todavía no tiene lo que pide el
        cambio (fuerza `difiere`/import real, para probar que la denuncia es
        ADITIVA). `pendiente=False`: el ribbon ya está como el playbook lo pide
        (fast-path `ya_existia`), para probar que sin nada más que denunciar
        el resultado sigue siendo verde."""
        if pendiente:
            cliente = self.armar_ribbon(ClienteSimulado())
        else:
            cliente = ClienteSimulado()
            armar_cliente_precondiciones_ok(cliente, IDENT)
            cliente.responder("GET", lambda r: r.startswith("RetrieveEntityRibbon("),
                              (200, {"CompressedEntityXml": zip_ribbon(self.RIBBON_YA_OCULTO)}, {}))
        cliente.responder("GET", lambda r: r.startswith("appmodules/Microsoft.Dynamics.CRM.RetrieveUnpublishedMultiple"),
                          (200, {"value": [{"appmoduleid": APP_ID, "uniquename": APP}]}, {}))
        cliente.responder("GET", lambda r: r.startswith("RetrieveAppComponents("),
                          (200, {"value": [{"componenttype": 1, "objectid": "md-" + t}
                                           for t in tablas_app]}, {}))
        cliente.responder("GET", lambda r: r.startswith("EntityDefinitions(md-"),
                          lambda r, c, s: (200, {"LogicalName": r.split("(md-")[1].split(")")[0]}, {}))
        return cliente

    def test_solo_verificar_denuncia_la_tabla_de_la_app_sin_bloque(self):
        cliente = self._cliente_con_tablas_de_app([TABLA, TABLAS[1]])
        estado, _, detalle = imp.construir(
            self.escribir(comp(app=APP, cambios=[CAMBIO_RIBBON])), True, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn(f"la tabla '{TABLAS[1]}' está en la app '{APP}' y no tiene bloque de ocultos", detalle)

    def test_sin_tablas_de_mas_no_hay_aviso(self):
        cliente = self._cliente_con_tablas_de_app([TABLA], pendiente=False)
        estado, _, detalle = imp.construir(
            self.escribir(comp(app=APP, cambios=[self.CAMBIO_SIN_BOTONES])), True, lambda: cliente)
        self.assertEqual("ya_existia", estado, detalle)
        self.assertNotIn("AVISO", detalle)

    def test_una_tabla_excluida_a_proposito_no_se_denuncia(self):
        componente = comp(app=APP, cambios=[self.CAMBIO_SIN_BOTONES])
        componente["tablas_excluidas_de_ocultar"] = [TABLAS[1]]
        cliente = self._cliente_con_tablas_de_app([TABLA, TABLAS[1]], pendiente=False)
        estado, _, detalle = imp.construir(self.escribir(componente), True, lambda: cliente)
        self.assertEqual("ya_existia", estado, detalle)
        self.assertNotIn("AVISO", detalle)

    def test_un_playbook_sin_app_no_denuncia_nada(self):
        # Comportamiento de antes: sin la clave opcional, cero llamadas nuevas y
        # cero cambio de estado. Backward compatible con `mantenimientoppp.md`.
        cliente = self.armar_ribbon(ClienteSimulado())
        estado, _, detalle = imp.construir(
            self.escribir(comp(cambios=[CAMBIO_RIBBON])), True, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)  # difiere por el propio cambio ribbon, no por AVISO
        self.assertNotIn("AVISO", detalle)
        self.assertFalse(any(l["ruta"].startswith("RetrieveAppComponents") for l in cliente.llamadas))

    def test_un_creado_con_una_tabla_de_la_app_sin_bloque_no_sale_en_verde(self):
        # La denuncia es ADITIVA: el import que sí estaba declarado se hace igual,
        # pero el estado final no puede ser 'creado' (código de salida 0) mientras
        # quede una tabla de la app sin cubrir.
        cliente = self._cliente_con_tablas_de_app([TABLA, TABLAS[1]])
        estado, _, detalle = imp.construir(
            self.escribir(comp(app=APP, cambios=[CAMBIO_RIBBON])), False, lambda: cliente)
        self.assertEqual("difiere", estado, detalle)
        self.assertIn("AVISO", detalle)
        self.assertIn(TABLAS[1], detalle)
        self.assertIn("ImportSolution", [l["ruta"] for l in cliente.llamadas if l["metodo"] == "POST"])


class ValidacionDeClavesOpcionales(Base):
    def rechaza(self, componente, fragmento):
        centinela = FabricaCentinela()
        estado, _, detalle = imp.construir(self.escribir(componente), False, centinela)
        self.assertEqual("error", estado, detalle)
        self.assertIn(fragmento, detalle)
        self.assertFalse(centinela.llamada)

    def test_tablas_excluidas_sin_app_se_rechaza(self):
        malo = comp(cambios=[CAMBIO_RIBBON])
        malo["tablas_excluidas_de_ocultar"] = [TABLAS[1]]
        self.rechaza(malo, "no tiene sentido sin 'app'")

    def test_tablas_excluidas_sin_prefijo_se_rechaza(self):
        malo = comp(app=APP, cambios=[CAMBIO_RIBBON])
        malo["tablas_excluidas_de_ocultar"] = ["account"]
        self.rechaza(malo, "no llevan el prefijo del publisher")

    def test_tablas_excluidas_repetidas_se_rechazan(self):
        malo = comp(app=APP, cambios=[CAMBIO_RIBBON])
        malo["tablas_excluidas_de_ocultar"] = [TABLAS[1], TABLAS[1]]
        self.rechaza(malo, "tiene repetidos")


if __name__ == "__main__":
    unittest.main()
