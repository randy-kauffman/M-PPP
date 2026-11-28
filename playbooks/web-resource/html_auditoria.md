# Playbook: web-resource · WR - MPPP - HTML - Auditoria

DN-09 F (`diseno/DIRECTRICES-NUEVAS.md`). El contenido NO va en este playbook: vive en
`recursos/html/auditoria.html`, se edita y se revisa como un archivo, y la herramienta lo sube.
El aspecto esta aprobado en `diseno/mockups/auditoria.html` (seccion 5, el timeline).

**Para que existe.** Es la seccion 5 del formulario "Auditoria" de Solicitud: un eje vertical por
tiempo con todo lo que le paso a UNA solicitud, para que el auditor conteste de un vistazo que paso,
quien lo hizo, cuando y por que. Etapas con color (Recepcion, Validacion, Acuse, Trabajo sobre los
registros, Cierre y respuesta), recorrido de una sola fila, reglas no cumplidas y contenido del acuse y de la
respuesta final desplegables, chip "Fila N" que abre la fila e impresion limpia. Filtros: Todo,
Solicitud, Registros (sin el filtro Incidencias ni eventos de incidencia: ver nota del aprobador).

**Solo lee.** `Xrm.WebApi.retrieveRecord` / `retrieveMultipleRecords` del formulario padre
(`window.parent.Xrm`): la Solicitud, su Bitacora (por fecha del evento ascendente), sus Filas y sus
Resultados de regla. Nunca escribe. El id de la Solicitud llega por la cadena de consulta, igual que
en `sanic_mppp_wr_html_comunicacion`.

**Idioma y tema.** Solo espanol (sin seleccion por `userSettings.languageId`) y solo tema claro,
estilo Fluent (botones de 4 px de radio, estados hover). Fechas en la hora local del usuario, dia/mes.
Sin librerias externas.

**Cambio manual del aprobador 2026-11-27 (ya en Dev, ya en el archivo del repositorio).** El
aprobador edito el web resource en Dev: quito el ingles (1033) y la seleccion de idioma, quito el
modo oscuro, quito el filtro Incidencias y los eventos de incidencia (Vencida, Reintento, Requiere
revision, Revision atendida, Error) y la etapa/pildora "Incidencia", y reestilo con apariencia
Fluent. Nota: un evento de bitacora que no este en la tabla de eventos ya no se marca como
incidencia. Esto contradice la regla de multiidioma del proyecto (etiquetas en 1033 + idioma del
cliente); queda como decision del aprobador.

**Seguridad.** Todo texto que viene de los datos se escapa; el contenido HTML del acuse y de la
respuesta final se muestra como texto, nunca se interpreta.

## 1. Identidad

```json
{
  "tipo_playbook": "web-resource",
  "version_skill": "0.1.0",
  "proyecto": "2026-001-referencias-planes-pago",
  "inventario": "12.9",
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
  "tipo": "web-resource",
  "nombre": "sanic_mppp_wr_html_auditoria",
  "displayname": "WR - MPPP - HTML - Auditoria",
  "descripcion": "Timeline de auditoria de una Solicitud: Bitacora, filas y reglas en un eje vertical por tiempo, solo lectura.",
  "recurso": "HTML",
  "archivo": "recursos/html/auditoria.html"
}
```

## 3. Precondiciones

- El archivo `recursos/html/auditoria.html` existe en el repositorio.
- Existen las tablas Solicitud, Fila, Bitacora y Resultado de regla con las columnas que lee el archivo (`sanic_mppp_tbl_bitacora`: `sanic_fechaevento`, `sanic_evento`, `sanic_origen`, `sanic_numerofila`, `sanic_actortexto`, `sanic_detalle`; `sanic_mppp_tbl_resultadoregla`: `sanic_orden`, `sanic_reglacodigo`, `sanic_resultado`, `sanic_efectoaplicado`, `sanic_razon`) y el lookup `sanic_solicitudid` en las tres tablas hijas.
