# Playbook: vista · Solicitudes - Auditoria

Inventario 12.2, **agregada el 2026-11-27 por DN-09** (`DIRECTRICES-NUEVAS.md` §D). Vista de la entrada «Solicitudes» del área Auditoría del sitemap.

La pregunta que contesta el auditor es «qué recibimos, qué pasó y si respondimos»: por eso lleva el estado, los contadores y **las fechas de las dos respuestas** (acuse y respuesta final), no los datos del correo.

**Todas, activas e inactivas.** El filtro va vacío a propósito: el auditor tiene que ver también las solicitudes desactivadas, que es justo donde se esconden los problemas. La vista de operación «Solicitudes» sigue filtrando `statecode = 0`. Una vista sin condiciones es válida para `vista.py` (arma `fetchxml` sin `<filter>`).

**No es la vista por defecto** de la tabla (`pordefecto: false`): la por defecto sigue siendo «Solicitudes», la de los demás roles. Esta se abre solo desde el sitemap, por `viewid`.

`sanic_nombre` va primero: es el N° y es lo que abre la solicitud.

El `fetchxml` y el `layoutxml` NO se escriben aca: los genera la herramienta
desde las columnas, el orden y el filtro.

## 1. Identidad

```json
{
  "tipo_playbook": "vista",
  "version_skill": "0.1.0",
  "proyecto": "2026-001-referencias-planes-pago",
  "inventario": "12.2 (DN-09)",
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
  "tipo": "vista",
  "nombre": "Solicitudes - Auditoria",
  "tabla": "sanic_mppp_tbl_solicitud",
  "clase": "Publica",
  "descripcion": "Todas las solicitudes, activas e inactivas, con estado, contadores y las fechas del acuse y de la respuesta final. Entrada Solicitudes del area Auditoria.",
  "pordefecto": false,
  "columnas": [
    {
      "nombre": "sanic_nombre",
      "ancho": 130
    },
    {
      "nombre": "sanic_fecharecibido",
      "ancho": 140
    },
    {
      "nombre": "sanic_remitente",
      "ancho": 220
    },
    {
      "nombre": "sanic_estadoprocesamiento",
      "ancho": 140
    },
    {
      "nombre": "sanic_filasvalidas",
      "ancho": 90
    },
    {
      "nombre": "sanic_filasrechazadas",
      "ancho": 100
    },
    {
      "nombre": "sanic_fechaacuseenviado",
      "ancho": 150
    },
    {
      "nombre": "sanic_fecharespuestafinalenviada",
      "ancho": 170
    },
    {
      "nombre": "sanic_fechacerrada",
      "ancho": 140
    }
  ],
  "orden": [
    {
      "columna": "sanic_fecharecibido",
      "sentido": "desc"
    }
  ],
  "filtro": {
    "tipo": "and",
    "condiciones": [],
    "enlaces": []
  }
}
```

## 3. Precondiciones

- La tabla Solicitud existe (P-03) con las nueve columnas de la vista.
- Va antes que el formulario «Auditoria» y que el sitemap: ninguno de los dos la necesita para construirse, pero el sitemap la nombra.

## 4. Cómo se construye

```
python3 herramientas/construir/vista.py playbooks/vista/solicitudes_auditoria.md
```

## 5. Verificación

```
python3 herramientas/construir/vista.py playbooks/vista/solicitudes_auditoria.md --solo-verificar
```

Segunda ejecución sin `--solo-verificar`: estado `ya_existia`. La vista figura una vez en la solución y su `querytype` es el de una vista pública.
