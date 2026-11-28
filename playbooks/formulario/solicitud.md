# Playbook: formulario · Solicitud

Inventario 12.3 (`05-app-model-driven.md` §formularios). El playbook no declara etiquetas
ni classid: la herramienta los resuelve contra la metadata de la tabla.

## 1. Identidad

```json
{
  "tipo_playbook": "formulario",
  "version_skill": "0.1.0",
  "proyecto": "2026-001-referencias-planes-pago",
  "inventario": "12.3",
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
  "tipo": "formulario",
  "nombre": "Solicitud",
  "tabla": "sanic_mppp_tbl_solicitud",
  "solo_lectura": true,
  "descripcion": "Formulario principal de Solicitud. Solo lectura: los estados los mueven el plugin y los botones.",
  "pestana": "General",
  "encabezado": [
    "sanic_nombre",
    "sanic_estadoprocesamiento",
    "sanic_remitente"
  ],
  "secciones": [
    {
      "titulo": "Correo",
      "campos": [
        "sanic_fecharecibido",
        "sanic_asunto",
        "sanic_messageid",
        "sanic_correocrudo",
        "sanic_exceloriginal",
        "sanic_cantidadadjuntos",
        "sanic_cantidadexcel",
        "sanic_motivoclasificacion"
      ]
    },
    {
      "titulo": "Filas",
      "subgrilla": {
        "tabla": "sanic_mppp_tbl_fila",
        "lookup": "sanic_solicitudid",
        "vista": "Filas"
      }
    },
    {
      "titulo": "Reglas",
      "subgrilla": {
        "tabla": "sanic_mppp_tbl_resultadoregla",
        "lookup": "sanic_solicitudid",
        "vista": "Reglas evaluadas"
      }
    },
    {
      "titulo": "Comunicaciones",
      "campos": [
        "sanic_fechavalidada",
        "sanic_filastotales",
        "sanic_filasvalidas",
        "sanic_filasrechazadas",
        "sanic_acusecontenido",
        "sanic_fechaacuseenviado",
        "sanic_respuestafinalcontenido",
        "sanic_fecharespuestafinalenviada",
        "sanic_fechacerrada"
      ]
    },
    {
      "titulo": "Correo enviado al cliente",
      "webresource": {
        "nombre": "sanic_mppp_wr_html_comunicacion",
        "alto": 14
      }
    },
    {
      "titulo": "Revision",
      "campos": [
        "sanic_requiererevision",
        "sanic_motivorevision",
        "sanic_reintentosvalidacion",
        "sanic_versionparametros"
      ]
    },
    {
      "titulo": "Bitacora",
      "subgrilla": {
        "tabla": "sanic_mppp_tbl_bitacora",
        "lookup": "sanic_solicitudid",
        "vista": "Eventos de la solicitud"
      }
    }
  ]
}
```

## 3. Precondiciones

- El web resource `sanic_mppp_wr_html_comunicacion` existe (12.9): muestra el correo enviado al cliente **tal cual**, dentro de un `iframe` con sandbox.

- La tabla, sus columnas y las vistas de las subgrillas existen.

## Cambio manual del aprobador 2026-11-27 que la herramienta no genera

El aprobador cambió en Dev el formulario «Solicitud» con el diseñador. El playbook NO lo puede expresar
(`formulario.py` arma una sola pestaña y un encabezado de 3 celdas), así que se deja el JSON tal cual y
`--solo-verificar` dice `difiere`. Lo que Dev tiene de más que el playbook:

1. **Pestaña «Esencial», primera, antes de «General»** (`tab_2`, `IsUserDefined="0"`):
   - sección «Correo»: Asunto, Correo original y Excel original, de solo lectura (`disabled="true"`; el aprobador los
     dejó editables y se corrigieron el 2026-11-27 a pedido suyo: la lista blanca rechaza que una persona los escriba);
   - sección «Filas»: subgrilla de `sanic_mppp_tbl_fila` (relación `sanic_mppp_solicitud_fila`), 4 filas por página,
     con selector de vista apagado, vista inicial «Active Filas» (`ed029da0-cc6c-4351-8d4f-b4b96757c817`, que el aprobador
     también cambió: ver `playbooks/vista/muestras/sanic_mppp_tbl_fila.active_filas.vista.xml`) y vistas disponibles
     «Mis clientes - por digitar», «Filas» y «Active Filas».
2. **Cuarta celda en el encabezado**: «Recibido el» (`sanic_fecharecibido`, de solo lectura desde el 2026-11-27). La
   herramienta admite 3.
3. `<footer>` con tres celdas vacías y `<DisplayConditions Order="0" FallbackForm="true"><Everyone/>` (los agrega el
   diseñador; la herramienta no los escribe).

**Riesgo:** un `--completar` sobre este playbook borraría la pestaña «Esencial» y la cuarta celda del encabezado.
No correrlo sobre este formulario sin antes decidir si se amplía la herramienta o se acepta el cambio manual.
