# Playbook: importacion · comandos de la barra (12.5 y 12.6)

Van JUNTOS porque comparten el `<RibbonDiffXml>`: hay UNO por tabla y la herramienta lo
reemplaza entero, asi que el playbook tiene que declarar TODO lo que esa tabla debe tener.

**12.5 — botones propios.** Los OCHO, clasicos con web resource JavaScript (DA-02). Los cuatro
que piden un texto (Rechazada en AS400, Anular, Devolver, Revisado) abren el dialogo de motivo:
el formulario de `sanic_mppp_tbl_motivoaccion` en modo creacion dentro de un dialogo (DA-03
corregida el 2026-09-22). Ya no hay ninguna pagina custom en la fase 1.

**12.6 — genericos que se ocultan** (DA-06). Se ocultan por el id del BOTON con
`HideCustomAction`, NUNCA sobrescribiendo su `CommandDefinition`. Los comandos de la
plataforma son GLOBALES (`Mscrm.DeleteSelectedRecord`, `Mscrm.AssignSelectedRecord`,
`Mscrm.ShareSelectedRecord` y companiia no llevan el nombre de la tabla): tocar uno apagaria
ese boton en TODAS las tablas del entorno, incluidas las de los otros proyectos del estudio.
El id del boton si lleva la tabla. Verificado con `RetrieveEntityRibbon` el 2026-09-22.

**Experimento D-74 (2026-10-02, orden del aprobador): los dos menús de `sanic_mppp_tbl_autorizacioncliente` llevan
`"etiquetas_localizadas": true`** en vez del texto fijo de las otras tres tablas, que quedan como control. Pregunta:
con la metadata del ribbon regenerada DESPUÉS de importar, ¿Unified Interface resuelve `$LocLabels` dentro de un
`FlyoutAnchor`? (La evidencia del 2026-10-01 quedó mezclada con metadata vieja.) Con la interfaz en inglés, el menú
tiene que decir "Bulk load". Según el resultado, o las cuatro tablas pasan a etiquetas localizadas, o esta vuelve a
`"idioma_del_texto": 3082` y D-74 se cierra como límite de la plataforma (`diseno/PENDIENTES.md`).

Los sufijos declarados en `ocultar` son candidatos: la herramienta oculta solo los que existen
de verdad en el ribbon de cada tabla, y nunca los de la pestaña de graficos.

**Botón "Atendida" agregado el 2026-09-25 (fase 2, `05-app-model-driven.md` §"Fase 2: dos vistas
y un comando más").** Va en el MISMO cambio `ribbon` de `sanic_mppp_tbl_fila` que los cinco de
fase 1: la herramienta reemplaza el `RibbonDiffXml` ENTERO de esa tabla, así que un cambio
separado para "Atendida" habría borrado los otros cinco. Apaga `sanic_requiereatencionmanual`
(no cambia `sanic_estado`); no pide texto, mismo motivo que "revisado" más abajo (no lleva
`regla`: no es una transición de estado, es un botón de "ya lo resolví", igual que "revisado",
"atendido" y "descartar").

**Botón "Descargar para digitar" agregado el 2026-09-26 (DN-02, `diseno/DIRECTRICES-NUEVAS.md`,
orden explícita del aprobador de construir en Dev).** Mismo motivo que "Atendida": va en el MISMO
cambio `ribbon` de `sanic_mppp_tbl_fila`, ahora con SIETE botones (secuencia 70, después de
"atendida"). No lleva `regla`: no hay una restricción de estado para descargar (masivo, sobre
cualquier selección de Fila, igual que "atendida"). Su función (`Sanic.Mppp.Comandos.
descargarParaDigitar`, en `recursos/js/comandos.js`) NO escribe nada en Dataverse: llama UNA vez a
la Custom API `sanic_mppp_capi_exportarparadigitar` con `Xrm.WebApi.online.execute` y baja el
`.xlsx` que devuelve con `Xrm.Navigation.openFile`.

**Bloque de `sanic_mppp_tbl_autorizacioncliente` agregado el 2026-09-28 (D-69).** La tabla entró a
la solución y a la app el 2026-09-27 (DN-01, `02` §2.4b) pero este playbook nunca se actualizó: es
un olvido de alcance, no una decisión — nadie agregó el bloque `ribbon` que le tocaba cuando se
creó la tabla, y `rg` sobre este archivo daba cero menciones. Lo detectó el caso `A-02` del arnés de
navegador (`pruebas/05-app-y-comandos.md`), el primero que mira el DOM real en vez de la
definición. Mismo `ocultar` que `sanic_mppp_tbl_autorizacionplan`, su hermana exacta en privilegios
y en flujo (`04-matriz-privilegios.md` línea 43: "tiene EXACTAMENTE los mismos privilegios"; el
arnés de navegador lo confirma con `ocultarNuevo: false` en `pruebas/gui/selectores.mjs`): el alta
de una autorización SÍ es de una persona (el Administrador de planes), así que "Nuevo" NO se oculta,
a diferencia de Solicitud/Fila/ResultadoRegla/Bitácora. Sin botones propios: no hay ninguna
transición de estado sobre esta tabla.

**Claves opcionales `app` y `tablas_excluidas_de_ocultar` agregadas el 2026-09-28 (D-69, capa 2).**
Le dicen a `importar_solucion.py --solo-verificar` (y a una corrida real) contra qué app comparar
la lista de tablas de este playbook, para que denuncie una tabla que entra a la app y se queda sin
bloque acá — el mecanismo exacto que falló con `AutorizacionCliente`. `sanic_mppp_tbl_motivoaccion`
es la única exclusión: es una tabla técnica que solo se abre en el diálogo de creación rápida del
motivo (DA-03, `05-app-model-driven.md`), nunca como grilla con barra de comandos, así que no le
corresponde ningún bloque `ribbon` — confirmado contra el arnés de navegador, que tampoco la incluye
en `TABLAS_A02` (`pruebas/gui/selectores.mjs`).

## 1. Identidad

```json
{
  "tipo_playbook": "app",
  "version_skill": "0.1.0",
  "proyecto": "2026-001-referencias-planes-pago",
  "inventario": "12.5+12.6",
  "fase": 1,
  "entorno_url": "https://org36e60d9d.crm.dynamics.com/",
  "solucion": "sanic_mppp_sol_mantenimientoppp",
  "publisher": "Sistemas_Abiertos_Nicaragua",
  "prefijo": "sanic",
  "abrev": "mppp",
  "prefijo_opciones": 15946,
  "lcid": 1033
}
```

## 2. Qué se crea

```json
{
  "tipo": "importacion",
  "idiomas": [
    1033,
    3082
  ],
  "app": "sanic_mppp_mda_mantenimientoppp",
  "tablas_excluidas_de_ocultar": [
    "sanic_mppp_tbl_motivoaccion"
  ],
  "cambios": [
    {
      "clase": "ribbon",
      "tabla": "sanic_mppp_tbl_fila",
      "biblioteca": "sanic_mppp_wr_js_comandos",
      "botones": [
        {
          "id": "digitada",
          "regla": "Sanic.Mppp.Comandos.puedeDigitada",
          "funcion": "Sanic.Mppp.Comandos.digitada",
          "icono": "sanic_mppp_wr_svg_cmddigitada",
          "etiqueta": {
            "1033": "Mark as entered",
            "3082": "Digitada"
          },
          "tooltip": {
            "1033": "Marks the selected rows as entered in AS400.",
            "3082": "Marca como digitadas en AS400 las filas seleccionadas."
          },
          "ubicaciones": [
            "HomepageGrid",
            "SubGrid"
          ],
          "secuencia": 10
        },
        {
          "id": "aprobar",
          "regla": "Sanic.Mppp.Comandos.puedeAprobar",
          "funcion": "Sanic.Mppp.Comandos.aprobar",
          "icono": "sanic_mppp_wr_svg_cmdaprobar",
          "etiqueta": {
            "1033": "Approve",
            "3082": "Aprobar"
          },
          "tooltip": {
            "1033": "Approves the selected rows.",
            "3082": "Aprueba las filas seleccionadas."
          },
          "ubicaciones": [
            "HomepageGrid",
            "SubGrid"
          ],
          "secuencia": 20
        },
        {
          "id": "rechazadaas400",
          "regla": "Sanic.Mppp.Comandos.puedeRechazadaAs400",
          "funcion": "Sanic.Mppp.Comandos.rechazadaAs400",
          "icono": "sanic_mppp_wr_svg_cmdrechazadaas400",
          "etiqueta": {
            "1033": "Rejected in AS400",
            "3082": "Rechazada en AS400"
          },
          "tooltip": {
            "1033": "Asks for a reason and marks the selected rows as rejected in AS400.",
            "3082": "Pide un motivo y marca las filas seleccionadas como rechazadas en AS400."
          },
          "ubicaciones": [
            "HomepageGrid",
            "SubGrid"
          ],
          "secuencia": 30
        },
        {
          "id": "anular",
          "regla": "Sanic.Mppp.Comandos.puedeAnular",
          "funcion": "Sanic.Mppp.Comandos.anular",
          "icono": "sanic_mppp_wr_svg_cmdanular",
          "etiqueta": {
            "1033": "Annul",
            "3082": "Anular"
          },
          "tooltip": {
            "1033": "Asks for a reason and voids the selected rows.",
            "3082": "Pide un motivo y anula las filas seleccionadas."
          },
          "ubicaciones": [
            "HomepageGrid",
            "SubGrid"
          ],
          "secuencia": 40
        },
        {
          "id": "devolver",
          "regla": "Sanic.Mppp.Comandos.puedeDevolver",
          "funcion": "Sanic.Mppp.Comandos.devolver",
          "icono": "sanic_mppp_wr_svg_cmddevolver",
          "etiqueta": {
            "1033": "Send back",
            "3082": "Devolver"
          },
          "tooltip": {
            "1033": "Asks for a reason and sends the selected rows back to the executive.",
            "3082": "Pide un motivo y devuelve al ejecutivo las filas seleccionadas."
          },
          "ubicaciones": [
            "HomepageGrid",
            "SubGrid"
          ],
          "secuencia": 50
        },
        {
          "id": "atendida",
          "funcion": "Sanic.Mppp.Comandos.atendida",
          "icono": "sanic_mppp_wr_svg_cmdatendida",
          "etiqueta": {
            "1033": "Mark attended",
            "3082": "Atendida"
          },
          "tooltip": {
            "1033": "Clears the manual-attention flag on the selected rows, without changing their state.",
            "3082": "Apaga la marca de atencion manual de las filas seleccionadas, sin cambiarles el estado."
          },
          "ubicaciones": [
            "HomepageGrid",
            "SubGrid"
          ],
          "secuencia": 60
        },
        {
          "id": "descargarparadigitar",
          "funcion": "Sanic.Mppp.Comandos.descargarParaDigitar",
          "icono": "sanic_mppp_wr_svg_cmddescargarparadigitar",
          "etiqueta": {
            "1033": "Download for entry",
            "3082": "Descargar para digitar"
          },
          "tooltip": {
            "1033": "Downloads the selected rows as an Excel file to enter in AS400.",
            "3082": "Descarga en Excel las filas seleccionadas para digitarlas en AS400."
          },
          "ubicaciones": [
            "HomepageGrid",
            "SubGrid"
          ],
          "secuencia": 70
        }
      ],
      "ocultar": [
        ".Abandon",
        ".Activate",
        ".AddAppointment",
        ".AddCampaignResponse",
        ".AddConnection",
        ".AddConnectionNew",
        ".AddConnectionToMe",
        ".AddEmail",
        ".AddExistingAssoc",
        ".AddExistingStandard",
        ".AddFax",
        ".AddFile",
        ".AddLetter",
        ".AddNewStandard",
        ".AddNote",
        ".AddOtherActivities",
        ".AddPhone",
        ".AddRecurringAppointment",
        ".AddServiceActivity",
        ".AddTask",
        ".AddToQueue",
        ".AdvMergeRecords",
        ".Assign",
        ".AssociateParentChildCase",
        ".BulkDelete",
        ".Convert.Case",
        ".Convert.Opportunity",
        ".CopySelected",
        ".CustomizeEntity",
        ".CustomizePreviewPane",
        ".Deactivate",
        ".Delete",
        ".DeleteMenu",
        ".DesignView",
        ".Detect",
        ".Detect.All",
        ".Detect.Selected",
        ".DocumentTemplate",
        ".DynamicPivotTable",
        ".DynamicWorkesheet",
        ".Edit",
        ".EditSalesProcess",
        ".ExportSelectedToExcel",
        ".ExportTemplate",
        ".ExportToExcel",
        ".ExportToExcelOnline",
        ".FinishStage",
        ".Flows.RefreshCommandBar",
        ".FormDesign",
        ".Import",
        ".ImportData",
        ".ImportDataFromCSV",
        ".ImportDataFromExcel",
        ".ImportDataFromXML",
        ".MBPF.ConvertTo",
        ".MailMerge",
        ".NewRecord",
        ".NewRecordForBPFEntity",
        ".OpenActiveStage",
        ".Permissions",
        ".Permissions.Sharing",
        ".Permissions.SharingNonRefresh",
        ".PromoteToResponse",
        ".PublishAll",
        ".PublishEntity",
        ".QueueItemDetail",
        ".Re-activate",
        ".Remove",
        ".Reports",
        ".RunReport",
        ".RunScript",
        ".RunWorkflow",
        ".SaveAsComplete",
        ".SaveAsNew",
        ".Send",
        ".Send.Selected",
        ".Send.View",
        ".SendDirectEmail",
        ".SendSelected",
        ".Sharing",
        ".StaticWorksheet",
        ".StaticWorksheetAll",
        ".SuggestProduct",
        ".SwitchProcess",
        ".WordTemplate",
        ".modern.AddEmail",
        ".modern.SendDirectEmail"
      ]
    },
    {
      "clase": "ribbon",
      "tabla": "sanic_mppp_tbl_solicitud",
      "biblioteca": "sanic_mppp_wr_js_comandos",
      "botones": [
        {
          "id": "atendido",
          "funcion": "Sanic.Mppp.Comandos.atendido",
          "icono": "sanic_mppp_wr_svg_cmdatendido",
          "etiqueta": {
            "1033": "Handled",
            "3082": "Atendido"
          },
          "tooltip": {
            "1033": "Closes the selected requests.",
            "3082": "Cierra las solicitudes seleccionadas."
          },
          "ubicaciones": [
            "HomepageGrid"
          ],
          "secuencia": 10
        },
        {
          "id": "descartar",
          "funcion": "Sanic.Mppp.Comandos.descartar",
          "icono": "sanic_mppp_wr_svg_cmddescartar",
          "etiqueta": {
            "1033": "Discard",
            "3082": "Descartar"
          },
          "tooltip": {
            "1033": "Discards the selected requests.",
            "3082": "Descarta las solicitudes seleccionadas."
          },
          "ubicaciones": [
            "HomepageGrid"
          ],
          "secuencia": 20
        },
        {
          "id": "revisado",
          "funcion": "Sanic.Mppp.Comandos.revisado",
          "icono": "sanic_mppp_wr_svg_cmdrevisado",
          "etiqueta": {
            "1033": "Reviewed",
            "3082": "Revisado"
          },
          "tooltip": {
            "1033": "Asks for a note, clears the review flag and leaves the note in the log.",
            "3082": "Pide una nota, apaga la marca de revision y deja la nota en la bitacora."
          },
          "ubicaciones": [
            "HomepageGrid"
          ],
          "secuencia": 30
        }
      ],
      "ocultar": [
        ".Abandon",
        ".Activate",
        ".AddAppointment",
        ".AddCampaignResponse",
        ".AddConnection",
        ".AddConnectionNew",
        ".AddConnectionToMe",
        ".AddEmail",
        ".AddExistingAssoc",
        ".AddExistingStandard",
        ".AddFax",
        ".AddFile",
        ".AddLetter",
        ".AddNewStandard",
        ".AddNote",
        ".AddOtherActivities",
        ".AddPhone",
        ".AddRecurringAppointment",
        ".AddServiceActivity",
        ".AddTask",
        ".AddToQueue",
        ".AdvMergeRecords",
        ".Assign",
        ".AssociateParentChildCase",
        ".BulkDelete",
        ".Convert.Case",
        ".Convert.Opportunity",
        ".CopySelected",
        ".CustomizeEntity",
        ".CustomizePreviewPane",
        ".Deactivate",
        ".Delete",
        ".DeleteMenu",
        ".DesignView",
        ".Detect",
        ".Detect.All",
        ".Detect.Selected",
        ".DocumentTemplate",
        ".DynamicPivotTable",
        ".DynamicWorkesheet",
        ".Edit",
        ".EditSalesProcess",
        ".ExportSelectedToExcel",
        ".ExportTemplate",
        ".ExportToExcel",
        ".ExportToExcelOnline",
        ".FinishStage",
        ".Flows.RefreshCommandBar",
        ".FormDesign",
        ".Import",
        ".ImportData",
        ".ImportDataFromCSV",
        ".ImportDataFromExcel",
        ".ImportDataFromXML",
        ".MBPF.ConvertTo",
        ".MailMerge",
        ".NewRecord",
        ".NewRecordForBPFEntity",
        ".OpenActiveStage",
        ".Permissions",
        ".Permissions.Sharing",
        ".Permissions.SharingNonRefresh",
        ".PromoteToResponse",
        ".PublishAll",
        ".PublishEntity",
        ".QueueItemDetail",
        ".Re-activate",
        ".Remove",
        ".Reports",
        ".RunReport",
        ".RunScript",
        ".RunWorkflow",
        ".SaveAsComplete",
        ".SaveAsNew",
        ".Send",
        ".Send.Selected",
        ".Send.View",
        ".SendDirectEmail",
        ".SendSelected",
        ".Sharing",
        ".StaticWorksheet",
        ".StaticWorksheetAll",
        ".SuggestProduct",
        ".SwitchProcess",
        ".WordTemplate",
        ".modern.AddEmail",
        ".modern.SendDirectEmail"
      ]
    },
    {
      "clase": "ribbon",
      "tabla": "sanic_mppp_tbl_cliente",
      "biblioteca": "sanic_mppp_wr_js_comandos",
      "botones": [
        {
          "id": "cargamasiva",
          "regla": "Sanic.Mppp.Comandos.esAdministradorDePlanes",
          "icono": "sanic_mppp_wr_svg_cmdcargamasiva",
          "etiqueta": {
            "1033": "Bulk load",
            "3082": "Carga masiva"
          },
          "tooltip": {
            "1033": "Bulk load of clients, plans, emails and client authorizations from Excel.",
            "3082": "Carga masiva de clientes, planes, correos y autorizaciones por cliente desde Excel."
          },
          "ubicaciones": [
            "HomepageGrid"
          ],
          "secuencia": 80,
          "idioma_del_texto": 3082,
          "menu": [
            {
              "id": "cargar",
              "funcion": "Sanic.Mppp.Comandos.cargarCatalogos",
              "secuencia": 10,
              "etiqueta": {
                "1033": "Upload file",
                "3082": "Cargar archivo"
              },
              "tooltip": {
                "1033": "Uploads the filled-in Excel template and processes it: all or nothing.",
                "3082": "Sube la plantilla de Excel completada y la procesa: todo o nada."
              }
            },
            {
              "id": "plantilla",
              "funcion": "Sanic.Mppp.Comandos.plantillaCarga",
              "secuencia": 20,
              "etiqueta": {
                "1033": "Download template",
                "3082": "Descargar plantilla"
              },
              "tooltip": {
                "1033": "Downloads the empty Excel template with the expected columns.",
                "3082": "Descarga la plantilla de Excel vacía con las columnas esperadas."
              }
            }
          ]
        },
        {
          "id": "activardesactivar",
          "regla": "Sanic.Mppp.Comandos.esAdministradorDePlanes",
          "icono": "sanic_mppp_wr_svg_cmdactivardesactivar",
          "etiqueta": {
            "1033": "Bulk activate/deactivate",
            "3082": "Activar/Desactivar masivo"
          },
          "tooltip": {
            "1033": "Activates or deactivates clients, plans, emails and client authorizations from Excel.",
            "3082": "Activa o desactiva desde Excel clientes, planes, correos y autorizaciones por cliente."
          },
          "ubicaciones": [
            "HomepageGrid"
          ],
          "secuencia": 90,
          "idioma_del_texto": 3082,
          "menu": [
            {
              "id": "cargar",
              "funcion": "Sanic.Mppp.Comandos.cambiarEstadoCatalogos",
              "secuencia": 10,
              "etiqueta": {
                "1033": "Upload file",
                "3082": "Cargar archivo"
              },
              "tooltip": {
                "1033": "Uploads the filled-in Excel template and processes it: all or nothing.",
                "3082": "Sube la plantilla de Excel completada y la procesa: todo o nada."
              }
            },
            {
              "id": "plantilla",
              "funcion": "Sanic.Mppp.Comandos.plantillaCambioEstado",
              "secuencia": 20,
              "etiqueta": {
                "1033": "Download template",
                "3082": "Descargar plantilla"
              },
              "tooltip": {
                "1033": "Downloads the empty Excel template with the expected columns.",
                "3082": "Descarga la plantilla de Excel vacía con las columnas esperadas."
              }
            }
          ]
        }
      ],
      "ocultar": [
        ".Abandon",
        ".AddAppointment",
        ".AddCampaignResponse",
        ".AddConnection",
        ".AddConnectionNew",
        ".AddConnectionToMe",
        ".AddEmail",
        ".AddFax",
        ".AddFile",
        ".AddLetter",
        ".AddNote",
        ".AddOtherActivities",
        ".AddPhone",
        ".AddRecurringAppointment",
        ".AddServiceActivity",
        ".AddTask",
        ".AddToQueue",
        ".AdvMergeRecords",
        ".Assign",
        ".AssociateParentChildCase",
        ".BulkDelete",
        ".Convert.Case",
        ".Convert.Opportunity",
        ".CustomizeEntity",
        ".CustomizePreviewPane",
        ".Delete",
        ".DeleteMenu",
        ".DesignView",
        ".Detect",
        ".Detect.All",
        ".Detect.Selected",
        ".DocumentTemplate",
        ".DynamicPivotTable",
        ".DynamicWorkesheet",
        ".EditSalesProcess",
        ".ExportSelectedToExcel",
        ".ExportTemplate",
        ".ExportToExcel",
        ".ExportToExcelOnline",
        ".FinishStage",
        ".Flows.RefreshCommandBar",
        ".FormDesign",
        ".Import",
        ".ImportData",
        ".ImportDataFromCSV",
        ".ImportDataFromExcel",
        ".ImportDataFromXML",
        ".MBPF.ConvertTo",
        ".MailMerge",
        ".OpenActiveStage",
        ".Permissions",
        ".Permissions.Sharing",
        ".Permissions.SharingNonRefresh",
        ".PromoteToResponse",
        ".PublishAll",
        ".PublishEntity",
        ".QueueItemDetail",
        ".Remove",
        ".Reports",
        ".RunReport",
        ".RunScript",
        ".RunWorkflow",
        ".SaveAsComplete",
        ".Send",
        ".Send.Selected",
        ".Send.View",
        ".SendDirectEmail",
        ".SendSelected",
        ".Sharing",
        ".StaticWorksheet",
        ".StaticWorksheetAll",
        ".SuggestProduct",
        ".SwitchProcess",
        ".WordTemplate",
        ".modern.AddEmail",
        ".modern.SendDirectEmail"
      ]
    },
    {
      "clase": "ribbon",
      "tabla": "sanic_mppp_tbl_plan",
      "biblioteca": "sanic_mppp_wr_js_comandos",
      "botones": [
        {
          "id": "cargamasiva",
          "regla": "Sanic.Mppp.Comandos.esAdministradorDePlanes",
          "icono": "sanic_mppp_wr_svg_cmdcargamasiva",
          "etiqueta": {
            "1033": "Bulk load",
            "3082": "Carga masiva"
          },
          "tooltip": {
            "1033": "Bulk load of clients, plans, emails and client authorizations from Excel.",
            "3082": "Carga masiva de clientes, planes, correos y autorizaciones por cliente desde Excel."
          },
          "ubicaciones": [
            "HomepageGrid"
          ],
          "secuencia": 80,
          "idioma_del_texto": 3082,
          "menu": [
            {
              "id": "cargar",
              "funcion": "Sanic.Mppp.Comandos.cargarCatalogos",
              "secuencia": 10,
              "etiqueta": {
                "1033": "Upload file",
                "3082": "Cargar archivo"
              },
              "tooltip": {
                "1033": "Uploads the filled-in Excel template and processes it: all or nothing.",
                "3082": "Sube la plantilla de Excel completada y la procesa: todo o nada."
              }
            },
            {
              "id": "plantilla",
              "funcion": "Sanic.Mppp.Comandos.plantillaCarga",
              "secuencia": 20,
              "etiqueta": {
                "1033": "Download template",
                "3082": "Descargar plantilla"
              },
              "tooltip": {
                "1033": "Downloads the empty Excel template with the expected columns.",
                "3082": "Descarga la plantilla de Excel vacía con las columnas esperadas."
              }
            }
          ]
        },
        {
          "id": "activardesactivar",
          "regla": "Sanic.Mppp.Comandos.esAdministradorDePlanes",
          "icono": "sanic_mppp_wr_svg_cmdactivardesactivar",
          "etiqueta": {
            "1033": "Bulk activate/deactivate",
            "3082": "Activar/Desactivar masivo"
          },
          "tooltip": {
            "1033": "Activates or deactivates clients, plans, emails and client authorizations from Excel.",
            "3082": "Activa o desactiva desde Excel clientes, planes, correos y autorizaciones por cliente."
          },
          "ubicaciones": [
            "HomepageGrid"
          ],
          "secuencia": 90,
          "idioma_del_texto": 3082,
          "menu": [
            {
              "id": "cargar",
              "funcion": "Sanic.Mppp.Comandos.cambiarEstadoCatalogos",
              "secuencia": 10,
              "etiqueta": {
                "1033": "Upload file",
                "3082": "Cargar archivo"
              },
              "tooltip": {
                "1033": "Uploads the filled-in Excel template and processes it: all or nothing.",
                "3082": "Sube la plantilla de Excel completada y la procesa: todo o nada."
              }
            },
            {
              "id": "plantilla",
              "funcion": "Sanic.Mppp.Comandos.plantillaCambioEstado",
              "secuencia": 20,
              "etiqueta": {
                "1033": "Download template",
                "3082": "Descargar plantilla"
              },
              "tooltip": {
                "1033": "Downloads the empty Excel template with the expected columns.",
                "3082": "Descarga la plantilla de Excel vacía con las columnas esperadas."
              }
            }
          ]
        }
      ],
      "ocultar": [
        ".Abandon",
        ".AddAppointment",
        ".AddCampaignResponse",
        ".AddConnection",
        ".AddConnectionNew",
        ".AddConnectionToMe",
        ".AddEmail",
        ".AddFax",
        ".AddFile",
        ".AddLetter",
        ".AddNote",
        ".AddOtherActivities",
        ".AddPhone",
        ".AddRecurringAppointment",
        ".AddServiceActivity",
        ".AddTask",
        ".AddToQueue",
        ".AdvMergeRecords",
        ".Assign",
        ".AssociateParentChildCase",
        ".BulkDelete",
        ".Convert.Case",
        ".Convert.Opportunity",
        ".CustomizeEntity",
        ".CustomizePreviewPane",
        ".Delete",
        ".DeleteMenu",
        ".DesignView",
        ".Detect",
        ".Detect.All",
        ".Detect.Selected",
        ".DocumentTemplate",
        ".DynamicPivotTable",
        ".DynamicWorkesheet",
        ".EditSalesProcess",
        ".ExportSelectedToExcel",
        ".ExportTemplate",
        ".ExportToExcel",
        ".ExportToExcelOnline",
        ".FinishStage",
        ".Flows.RefreshCommandBar",
        ".FormDesign",
        ".Import",
        ".ImportData",
        ".ImportDataFromCSV",
        ".ImportDataFromExcel",
        ".ImportDataFromXML",
        ".MBPF.ConvertTo",
        ".MailMerge",
        ".OpenActiveStage",
        ".Permissions",
        ".Permissions.Sharing",
        ".Permissions.SharingNonRefresh",
        ".PromoteToResponse",
        ".PublishAll",
        ".PublishEntity",
        ".QueueItemDetail",
        ".Remove",
        ".Reports",
        ".RunReport",
        ".RunScript",
        ".RunWorkflow",
        ".SaveAsComplete",
        ".Send",
        ".Send.Selected",
        ".Send.View",
        ".SendDirectEmail",
        ".SendSelected",
        ".Sharing",
        ".StaticWorksheet",
        ".StaticWorksheetAll",
        ".SuggestProduct",
        ".SwitchProcess",
        ".WordTemplate",
        ".modern.AddEmail",
        ".modern.SendDirectEmail"
      ]
    },
    {
      "clase": "ribbon",
      "tabla": "sanic_mppp_tbl_autorizado",
      "biblioteca": "sanic_mppp_wr_js_comandos",
      "botones": [
        {
          "id": "cargamasiva",
          "regla": "Sanic.Mppp.Comandos.esAdministradorDePlanes",
          "icono": "sanic_mppp_wr_svg_cmdcargamasiva",
          "etiqueta": {
            "1033": "Bulk load",
            "3082": "Carga masiva"
          },
          "tooltip": {
            "1033": "Bulk load of clients, plans, emails and client authorizations from Excel.",
            "3082": "Carga masiva de clientes, planes, correos y autorizaciones por cliente desde Excel."
          },
          "ubicaciones": [
            "HomepageGrid"
          ],
          "secuencia": 80,
          "idioma_del_texto": 3082,
          "menu": [
            {
              "id": "cargar",
              "funcion": "Sanic.Mppp.Comandos.cargarCatalogos",
              "secuencia": 10,
              "etiqueta": {
                "1033": "Upload file",
                "3082": "Cargar archivo"
              },
              "tooltip": {
                "1033": "Uploads the filled-in Excel template and processes it: all or nothing.",
                "3082": "Sube la plantilla de Excel completada y la procesa: todo o nada."
              }
            },
            {
              "id": "plantilla",
              "funcion": "Sanic.Mppp.Comandos.plantillaCarga",
              "secuencia": 20,
              "etiqueta": {
                "1033": "Download template",
                "3082": "Descargar plantilla"
              },
              "tooltip": {
                "1033": "Downloads the empty Excel template with the expected columns.",
                "3082": "Descarga la plantilla de Excel vacía con las columnas esperadas."
              }
            }
          ]
        },
        {
          "id": "activardesactivar",
          "regla": "Sanic.Mppp.Comandos.esAdministradorDePlanes",
          "icono": "sanic_mppp_wr_svg_cmdactivardesactivar",
          "etiqueta": {
            "1033": "Bulk activate/deactivate",
            "3082": "Activar/Desactivar masivo"
          },
          "tooltip": {
            "1033": "Activates or deactivates clients, plans, emails and client authorizations from Excel.",
            "3082": "Activa o desactiva desde Excel clientes, planes, correos y autorizaciones por cliente."
          },
          "ubicaciones": [
            "HomepageGrid"
          ],
          "secuencia": 90,
          "idioma_del_texto": 3082,
          "menu": [
            {
              "id": "cargar",
              "funcion": "Sanic.Mppp.Comandos.cambiarEstadoCatalogos",
              "secuencia": 10,
              "etiqueta": {
                "1033": "Upload file",
                "3082": "Cargar archivo"
              },
              "tooltip": {
                "1033": "Uploads the filled-in Excel template and processes it: all or nothing.",
                "3082": "Sube la plantilla de Excel completada y la procesa: todo o nada."
              }
            },
            {
              "id": "plantilla",
              "funcion": "Sanic.Mppp.Comandos.plantillaCambioEstado",
              "secuencia": 20,
              "etiqueta": {
                "1033": "Download template",
                "3082": "Descargar plantilla"
              },
              "tooltip": {
                "1033": "Downloads the empty Excel template with the expected columns.",
                "3082": "Descarga la plantilla de Excel vacía con las columnas esperadas."
              }
            }
          ]
        }
      ],
      "ocultar": [
        ".Abandon",
        ".AddAppointment",
        ".AddCampaignResponse",
        ".AddConnection",
        ".AddConnectionNew",
        ".AddConnectionToMe",
        ".AddEmail",
        ".AddFax",
        ".AddFile",
        ".AddLetter",
        ".AddNote",
        ".AddOtherActivities",
        ".AddPhone",
        ".AddRecurringAppointment",
        ".AddServiceActivity",
        ".AddTask",
        ".AddToQueue",
        ".AdvMergeRecords",
        ".Assign",
        ".AssociateParentChildCase",
        ".BulkDelete",
        ".Convert.Case",
        ".Convert.Opportunity",
        ".CustomizeEntity",
        ".CustomizePreviewPane",
        ".Delete",
        ".DeleteMenu",
        ".DesignView",
        ".Detect",
        ".Detect.All",
        ".Detect.Selected",
        ".DocumentTemplate",
        ".DynamicPivotTable",
        ".DynamicWorkesheet",
        ".EditSalesProcess",
        ".ExportSelectedToExcel",
        ".ExportTemplate",
        ".ExportToExcel",
        ".ExportToExcelOnline",
        ".FinishStage",
        ".Flows.RefreshCommandBar",
        ".FormDesign",
        ".Import",
        ".ImportData",
        ".ImportDataFromCSV",
        ".ImportDataFromExcel",
        ".ImportDataFromXML",
        ".MBPF.ConvertTo",
        ".MailMerge",
        ".OpenActiveStage",
        ".Permissions",
        ".Permissions.Sharing",
        ".Permissions.SharingNonRefresh",
        ".PromoteToResponse",
        ".PublishAll",
        ".PublishEntity",
        ".QueueItemDetail",
        ".Remove",
        ".Reports",
        ".RunReport",
        ".RunScript",
        ".RunWorkflow",
        ".SaveAsComplete",
        ".Send",
        ".Send.Selected",
        ".Send.View",
        ".SendDirectEmail",
        ".SendSelected",
        ".Sharing",
        ".StaticWorksheet",
        ".StaticWorksheetAll",
        ".SuggestProduct",
        ".SwitchProcess",
        ".WordTemplate",
        ".modern.AddEmail",
        ".modern.SendDirectEmail"
      ]
    },
    {
      "clase": "ribbon",
      "tabla": "sanic_mppp_tbl_autorizacionplan",
      "biblioteca": "sanic_mppp_wr_js_comandos",
      "botones": [],
      "ocultar": [
        ".Abandon",
        ".AddAppointment",
        ".AddCampaignResponse",
        ".AddConnection",
        ".AddConnectionNew",
        ".AddConnectionToMe",
        ".AddEmail",
        ".AddFax",
        ".AddFile",
        ".AddLetter",
        ".AddNote",
        ".AddOtherActivities",
        ".AddPhone",
        ".AddRecurringAppointment",
        ".AddServiceActivity",
        ".AddTask",
        ".AddToQueue",
        ".AdvMergeRecords",
        ".Assign",
        ".AssociateParentChildCase",
        ".BulkDelete",
        ".Convert.Case",
        ".Convert.Opportunity",
        ".CustomizeEntity",
        ".CustomizePreviewPane",
        ".Delete",
        ".DeleteMenu",
        ".DesignView",
        ".Detect",
        ".Detect.All",
        ".Detect.Selected",
        ".DocumentTemplate",
        ".DynamicPivotTable",
        ".DynamicWorkesheet",
        ".EditSalesProcess",
        ".ExportSelectedToExcel",
        ".ExportTemplate",
        ".ExportToExcel",
        ".ExportToExcelOnline",
        ".FinishStage",
        ".Flows.RefreshCommandBar",
        ".FormDesign",
        ".Import",
        ".ImportData",
        ".ImportDataFromCSV",
        ".ImportDataFromExcel",
        ".ImportDataFromXML",
        ".MBPF.ConvertTo",
        ".MailMerge",
        ".OpenActiveStage",
        ".Permissions",
        ".Permissions.Sharing",
        ".Permissions.SharingNonRefresh",
        ".PromoteToResponse",
        ".PublishAll",
        ".PublishEntity",
        ".QueueItemDetail",
        ".Remove",
        ".Reports",
        ".RunReport",
        ".RunScript",
        ".RunWorkflow",
        ".SaveAsComplete",
        ".Send",
        ".Send.Selected",
        ".Send.View",
        ".SendDirectEmail",
        ".SendSelected",
        ".Sharing",
        ".StaticWorksheet",
        ".StaticWorksheetAll",
        ".SuggestProduct",
        ".SwitchProcess",
        ".WordTemplate",
        ".modern.AddEmail",
        ".modern.SendDirectEmail"
      ]
    },
    {
      "clase": "ribbon",
      "tabla": "sanic_mppp_tbl_autorizacioncliente",
      "biblioteca": "sanic_mppp_wr_js_comandos",
      "botones": [
        {
          "id": "cargamasiva",
          "regla": "Sanic.Mppp.Comandos.esAdministradorDePlanes",
          "icono": "sanic_mppp_wr_svg_cmdcargamasiva",
          "etiqueta": {
            "1033": "Bulk load",
            "3082": "Carga masiva"
          },
          "tooltip": {
            "1033": "Bulk load of clients, plans, emails and client authorizations from Excel.",
            "3082": "Carga masiva de clientes, planes, correos y autorizaciones por cliente desde Excel."
          },
          "ubicaciones": [
            "HomepageGrid"
          ],
          "secuencia": 80,
          "etiquetas_localizadas": true,
          "menu": [
            {
              "id": "cargar",
              "funcion": "Sanic.Mppp.Comandos.cargarCatalogos",
              "secuencia": 10,
              "etiqueta": {
                "1033": "Upload file",
                "3082": "Cargar archivo"
              },
              "tooltip": {
                "1033": "Uploads the filled-in Excel template and processes it: all or nothing.",
                "3082": "Sube la plantilla de Excel completada y la procesa: todo o nada."
              }
            },
            {
              "id": "plantilla",
              "funcion": "Sanic.Mppp.Comandos.plantillaCarga",
              "secuencia": 20,
              "etiqueta": {
                "1033": "Download template",
                "3082": "Descargar plantilla"
              },
              "tooltip": {
                "1033": "Downloads the empty Excel template with the expected columns.",
                "3082": "Descarga la plantilla de Excel vacía con las columnas esperadas."
              }
            }
          ]
        },
        {
          "id": "activardesactivar",
          "regla": "Sanic.Mppp.Comandos.esAdministradorDePlanes",
          "icono": "sanic_mppp_wr_svg_cmdactivardesactivar",
          "etiqueta": {
            "1033": "Bulk activate/deactivate",
            "3082": "Activar/Desactivar masivo"
          },
          "tooltip": {
            "1033": "Activates or deactivates clients, plans, emails and client authorizations from Excel.",
            "3082": "Activa o desactiva desde Excel clientes, planes, correos y autorizaciones por cliente."
          },
          "ubicaciones": [
            "HomepageGrid"
          ],
          "secuencia": 90,
          "etiquetas_localizadas": true,
          "menu": [
            {
              "id": "cargar",
              "funcion": "Sanic.Mppp.Comandos.cambiarEstadoCatalogos",
              "secuencia": 10,
              "etiqueta": {
                "1033": "Upload file",
                "3082": "Cargar archivo"
              },
              "tooltip": {
                "1033": "Uploads the filled-in Excel template and processes it: all or nothing.",
                "3082": "Sube la plantilla de Excel completada y la procesa: todo o nada."
              }
            },
            {
              "id": "plantilla",
              "funcion": "Sanic.Mppp.Comandos.plantillaCambioEstado",
              "secuencia": 20,
              "etiqueta": {
                "1033": "Download template",
                "3082": "Descargar plantilla"
              },
              "tooltip": {
                "1033": "Downloads the empty Excel template with the expected columns.",
                "3082": "Descarga la plantilla de Excel vacía con las columnas esperadas."
              }
            }
          ]
        }
      ],
      "ocultar": [
        ".Abandon",
        ".AddAppointment",
        ".AddCampaignResponse",
        ".AddConnection",
        ".AddConnectionNew",
        ".AddConnectionToMe",
        ".AddEmail",
        ".AddFax",
        ".AddFile",
        ".AddLetter",
        ".AddNote",
        ".AddOtherActivities",
        ".AddPhone",
        ".AddRecurringAppointment",
        ".AddServiceActivity",
        ".AddTask",
        ".AddToQueue",
        ".AdvMergeRecords",
        ".Assign",
        ".AssociateParentChildCase",
        ".BulkDelete",
        ".Convert.Case",
        ".Convert.Opportunity",
        ".CustomizeEntity",
        ".CustomizePreviewPane",
        ".Delete",
        ".DeleteMenu",
        ".DesignView",
        ".Detect",
        ".Detect.All",
        ".Detect.Selected",
        ".DocumentTemplate",
        ".DynamicPivotTable",
        ".DynamicWorkesheet",
        ".EditSalesProcess",
        ".ExportSelectedToExcel",
        ".ExportTemplate",
        ".ExportToExcel",
        ".ExportToExcelOnline",
        ".FinishStage",
        ".Flows.RefreshCommandBar",
        ".FormDesign",
        ".Import",
        ".ImportData",
        ".ImportDataFromCSV",
        ".ImportDataFromExcel",
        ".ImportDataFromXML",
        ".MBPF.ConvertTo",
        ".MailMerge",
        ".OpenActiveStage",
        ".Permissions",
        ".Permissions.Sharing",
        ".Permissions.SharingNonRefresh",
        ".PromoteToResponse",
        ".PublishAll",
        ".PublishEntity",
        ".QueueItemDetail",
        ".Remove",
        ".Reports",
        ".RunReport",
        ".RunScript",
        ".RunWorkflow",
        ".SaveAsComplete",
        ".Send",
        ".Send.Selected",
        ".Send.View",
        ".SendDirectEmail",
        ".SendSelected",
        ".Sharing",
        ".StaticWorksheet",
        ".StaticWorksheetAll",
        ".SuggestProduct",
        ".SwitchProcess",
        ".WordTemplate",
        ".modern.AddEmail",
        ".modern.SendDirectEmail"
      ]
    },
    {
      "clase": "ribbon",
      "tabla": "sanic_mppp_tbl_parametro",
      "biblioteca": "sanic_mppp_wr_js_comandos",
      "botones": [],
      "ocultar": [
        ".Abandon",
        ".AddAppointment",
        ".AddCampaignResponse",
        ".AddConnection",
        ".AddConnectionNew",
        ".AddConnectionToMe",
        ".AddEmail",
        ".AddFax",
        ".AddFile",
        ".AddLetter",
        ".AddNote",
        ".AddOtherActivities",
        ".AddPhone",
        ".AddRecurringAppointment",
        ".AddServiceActivity",
        ".AddTask",
        ".AddToQueue",
        ".AdvMergeRecords",
        ".Assign",
        ".AssociateParentChildCase",
        ".BulkDelete",
        ".Convert.Case",
        ".Convert.Opportunity",
        ".CustomizeEntity",
        ".CustomizePreviewPane",
        ".Delete",
        ".DeleteMenu",
        ".DesignView",
        ".Detect",
        ".Detect.All",
        ".Detect.Selected",
        ".DocumentTemplate",
        ".DynamicPivotTable",
        ".DynamicWorkesheet",
        ".EditSalesProcess",
        ".ExportSelectedToExcel",
        ".ExportTemplate",
        ".ExportToExcel",
        ".ExportToExcelOnline",
        ".FinishStage",
        ".Flows.RefreshCommandBar",
        ".FormDesign",
        ".Import",
        ".ImportData",
        ".ImportDataFromCSV",
        ".ImportDataFromExcel",
        ".ImportDataFromXML",
        ".MBPF.ConvertTo",
        ".MailMerge",
        ".OpenActiveStage",
        ".Permissions",
        ".Permissions.Sharing",
        ".Permissions.SharingNonRefresh",
        ".PromoteToResponse",
        ".PublishAll",
        ".PublishEntity",
        ".QueueItemDetail",
        ".Remove",
        ".Reports",
        ".RunReport",
        ".RunScript",
        ".RunWorkflow",
        ".SaveAsComplete",
        ".Send",
        ".Send.Selected",
        ".Send.View",
        ".SendDirectEmail",
        ".SendSelected",
        ".Sharing",
        ".StaticWorksheet",
        ".StaticWorksheetAll",
        ".SuggestProduct",
        ".SwitchProcess",
        ".WordTemplate",
        ".modern.AddEmail",
        ".modern.SendDirectEmail"
      ]
    },
    {
      "clase": "ribbon",
      "tabla": "sanic_mppp_tbl_regla",
      "biblioteca": "sanic_mppp_wr_js_comandos",
      "botones": [],
      "ocultar": [
        ".Abandon",
        ".AddAppointment",
        ".AddCampaignResponse",
        ".AddConnection",
        ".AddConnectionNew",
        ".AddConnectionToMe",
        ".AddEmail",
        ".AddFax",
        ".AddFile",
        ".AddLetter",
        ".AddNote",
        ".AddOtherActivities",
        ".AddPhone",
        ".AddRecurringAppointment",
        ".AddServiceActivity",
        ".AddTask",
        ".AddToQueue",
        ".AdvMergeRecords",
        ".Assign",
        ".AssociateParentChildCase",
        ".BulkDelete",
        ".Convert.Case",
        ".Convert.Opportunity",
        ".CustomizeEntity",
        ".CustomizePreviewPane",
        ".Delete",
        ".DeleteMenu",
        ".DesignView",
        ".Detect",
        ".Detect.All",
        ".Detect.Selected",
        ".DocumentTemplate",
        ".DynamicPivotTable",
        ".DynamicWorkesheet",
        ".EditSalesProcess",
        ".ExportSelectedToExcel",
        ".ExportTemplate",
        ".ExportToExcel",
        ".ExportToExcelOnline",
        ".FinishStage",
        ".Flows.RefreshCommandBar",
        ".FormDesign",
        ".Import",
        ".ImportData",
        ".ImportDataFromCSV",
        ".ImportDataFromExcel",
        ".ImportDataFromXML",
        ".MBPF.ConvertTo",
        ".MailMerge",
        ".OpenActiveStage",
        ".Permissions",
        ".Permissions.Sharing",
        ".Permissions.SharingNonRefresh",
        ".PromoteToResponse",
        ".PublishAll",
        ".PublishEntity",
        ".QueueItemDetail",
        ".Remove",
        ".Reports",
        ".RunReport",
        ".RunScript",
        ".RunWorkflow",
        ".SaveAsComplete",
        ".Send",
        ".Send.Selected",
        ".Send.View",
        ".SendDirectEmail",
        ".SendSelected",
        ".Sharing",
        ".StaticWorksheet",
        ".StaticWorksheetAll",
        ".SuggestProduct",
        ".SwitchProcess",
        ".WordTemplate",
        ".modern.AddEmail",
        ".modern.SendDirectEmail"
      ]
    },
    {
      "clase": "ribbon",
      "tabla": "sanic_mppp_tbl_resultadoregla",
      "biblioteca": "sanic_mppp_wr_js_comandos",
      "botones": [],
      "ocultar": [
        ".Abandon",
        ".Activate",
        ".AddAppointment",
        ".AddCampaignResponse",
        ".AddConnection",
        ".AddConnectionNew",
        ".AddConnectionToMe",
        ".AddEmail",
        ".AddExistingAssoc",
        ".AddExistingStandard",
        ".AddFax",
        ".AddFile",
        ".AddLetter",
        ".AddNewStandard",
        ".AddNote",
        ".AddOtherActivities",
        ".AddPhone",
        ".AddRecurringAppointment",
        ".AddServiceActivity",
        ".AddTask",
        ".AddToQueue",
        ".AdvMergeRecords",
        ".Assign",
        ".AssociateParentChildCase",
        ".BulkDelete",
        ".Convert.Case",
        ".Convert.Opportunity",
        ".CopySelected",
        ".CustomizeEntity",
        ".CustomizePreviewPane",
        ".Deactivate",
        ".Delete",
        ".DeleteMenu",
        ".DesignView",
        ".Detect",
        ".Detect.All",
        ".Detect.Selected",
        ".DocumentTemplate",
        ".DynamicPivotTable",
        ".DynamicWorkesheet",
        ".Edit",
        ".EditSalesProcess",
        ".ExportSelectedToExcel",
        ".ExportTemplate",
        ".ExportToExcel",
        ".ExportToExcelOnline",
        ".FinishStage",
        ".Flows.RefreshCommandBar",
        ".FormDesign",
        ".Import",
        ".ImportData",
        ".ImportDataFromCSV",
        ".ImportDataFromExcel",
        ".ImportDataFromXML",
        ".MBPF.ConvertTo",
        ".MailMerge",
        ".NewRecord",
        ".NewRecordForBPFEntity",
        ".OpenActiveStage",
        ".Permissions",
        ".Permissions.Sharing",
        ".Permissions.SharingNonRefresh",
        ".PromoteToResponse",
        ".PublishAll",
        ".PublishEntity",
        ".QueueItemDetail",
        ".Re-activate",
        ".Remove",
        ".Reports",
        ".RunReport",
        ".RunScript",
        ".RunWorkflow",
        ".SaveAsComplete",
        ".SaveAsNew",
        ".Send",
        ".Send.Selected",
        ".Send.View",
        ".SendDirectEmail",
        ".SendSelected",
        ".Sharing",
        ".StaticWorksheet",
        ".StaticWorksheetAll",
        ".SuggestProduct",
        ".SwitchProcess",
        ".WordTemplate",
        ".modern.AddEmail",
        ".modern.SendDirectEmail"
      ]
    },
    {
      "clase": "ribbon",
      "tabla": "sanic_mppp_tbl_bitacora",
      "biblioteca": "sanic_mppp_wr_js_comandos",
      "botones": [],
      "ocultar": [
        ".Abandon",
        ".Activate",
        ".AddAppointment",
        ".AddCampaignResponse",
        ".AddConnection",
        ".AddConnectionNew",
        ".AddConnectionToMe",
        ".AddEmail",
        ".AddExistingAssoc",
        ".AddExistingStandard",
        ".AddFax",
        ".AddFile",
        ".AddLetter",
        ".AddNewStandard",
        ".AddNote",
        ".AddOtherActivities",
        ".AddPhone",
        ".AddRecurringAppointment",
        ".AddServiceActivity",
        ".AddTask",
        ".AddToQueue",
        ".AdvMergeRecords",
        ".Assign",
        ".AssociateParentChildCase",
        ".BulkDelete",
        ".Convert.Case",
        ".Convert.Opportunity",
        ".CopySelected",
        ".CustomizeEntity",
        ".CustomizePreviewPane",
        ".Deactivate",
        ".Delete",
        ".DeleteMenu",
        ".DesignView",
        ".Detect",
        ".Detect.All",
        ".Detect.Selected",
        ".DocumentTemplate",
        ".DynamicPivotTable",
        ".DynamicWorkesheet",
        ".Edit",
        ".EditSalesProcess",
        ".ExportSelectedToExcel",
        ".ExportTemplate",
        ".ExportToExcel",
        ".ExportToExcelOnline",
        ".FinishStage",
        ".Flows.RefreshCommandBar",
        ".FormDesign",
        ".Import",
        ".ImportData",
        ".ImportDataFromCSV",
        ".ImportDataFromExcel",
        ".ImportDataFromXML",
        ".MBPF.ConvertTo",
        ".MailMerge",
        ".NewRecord",
        ".NewRecordForBPFEntity",
        ".OpenActiveStage",
        ".Permissions",
        ".Permissions.Sharing",
        ".Permissions.SharingNonRefresh",
        ".PromoteToResponse",
        ".PublishAll",
        ".PublishEntity",
        ".QueueItemDetail",
        ".Re-activate",
        ".Remove",
        ".Reports",
        ".RunReport",
        ".RunScript",
        ".RunWorkflow",
        ".SaveAsComplete",
        ".SaveAsNew",
        ".Send",
        ".Send.Selected",
        ".Send.View",
        ".SendDirectEmail",
        ".SendSelected",
        ".Sharing",
        ".StaticWorksheet",
        ".StaticWorksheetAll",
        ".SuggestProduct",
        ".SwitchProcess",
        ".WordTemplate",
        ".modern.AddEmail",
        ".modern.SendDirectEmail"
      ]
    }
  ]
}
```

## 3. Precondiciones

- El web resource `sanic_mppp_wr_js_comandos` existe (`playbooks/web-resource/js_comandos.md`).
- Los iconos `sanic_mppp_wr_svg_cmd*` existen (12.1).
- Las 11 tablas estan en la solucion (agregada `sanic_mppp_tbl_autorizacioncliente` el 2026-09-28, D-69).
- La app `sanic_mppp_mda_mantenimientoppp` existe y ya tiene sus tablas enganchadas (`mantenimientoppp.md`, se corre antes que este playbook): `app` la usa para denunciar una tabla de la app sin bloque acá.
