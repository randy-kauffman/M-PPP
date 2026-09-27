# Playbook: web-resource · WR - MPPP - JS - Comandos

Inventario 12.5. El contenido NO va en este playbook: vive en
`recursos/js/comandos.js`, se edita y se revisa como un archivo, y la herramienta lo sube.

Trae los OCHO comandos de fase 1 más **Atendida** (fase 2, `05-app-model-driven.md`
§"Fase 2: dos vistas y un comando más") más **Descargar para digitar** (DN-02,
`diseno/DIRECTRICES-NUEVAS.md`, orden explícita del aprobador de construir en Dev el 2026-09-26):
diez funciones en total. Los cuatro que piden un texto (Rechazada en AS400, Anular, Devolver,
Revisado) abren el dialogo de motivo: el formulario de `sanic_mppp_tbl_motivoaccion` en modo
creacion dentro de un dialogo (DA-03 corregida el 2026-09-22), leen el motivo del registro que
devuelve `savedEntityReference[0]` y lo borran. **Atendida no pide texto**, mismo patrón y mismo
motivo que Revisado (`05` §5, corregido el 2026-09-23). **Descargar para digitar NO escribe nada
en Dataverse**: llama UNA vez (no fila por fila) a la Custom API
`sanic_mppp_capi_exportarparadigitar` con `Xrm.WebApi.online.execute` y baja el `.xlsx` que
devuelve con `Xrm.Navigation.openFile`.

## 1. Identidad

```json
{
  "tipo_playbook": "web-resource",
  "version_skill": "0.1.0",
  "proyecto": "2026-001-referencias-planes-pago",
  "inventario": "12.5",
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
  "nombre": "sanic_mppp_wr_js_comandos",
  "displayname": "WR - MPPP - JS - Comandos",
  "descripcion": "Funciones de los diez botones de la barra de comandos de Fila y Solicitud (12.5), incluido el dialogo de motivo (12.4).",
  "recurso": "JS",
  "archivo": "recursos/js/comandos.js"
}
```

## 3. Precondiciones

- El archivo `recursos/js/comandos.js` existe en el repositorio.
