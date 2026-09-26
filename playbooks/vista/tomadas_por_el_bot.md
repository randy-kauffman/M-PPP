# Playbook: vista · Tomadas por el bot

Inventario 12.2 (`05-app-model-driven.md` §"Fase 2: dos vistas y un comando más"). **+1 vista**
sobre la lista cerrada de 17 (más "Filas" y "Para atención manual", esta es la 20).

Para qué: saber qué fila está en manos del bot y hasta qué hora, sin preguntarle a nadie
(`03-contratos-custom-api.md` §2).

**Detalle de implementación verificado contra Learn el 2026-09-25 (`condition operator values`,
`power-apps/developer/data-platform/fetchxml/reference/operators`).** El diseño pedía confirmar
si existe un operador de FetchXML para "vigente ahora" (familia `next-x-hours`) antes de usarlo.
`next-x-hours` existe, pero su semántica es **"el valor cae DENTRO de las próximas X horas"**
(una ventana acotada que hay que declarar en `value`), no "el valor es mayor que ahora" sin
límite. No hay ningún operador de la lista de Learn con esa segunda semántica (`gt`/`ge` piden un
valor **literal**, no "ahora"; no hay un `gt-now` ni equivalente). Además `vista.py` declara una
lista CERRADA de operadores (`OPERADORES` en `herramientas/construir/vista.py`) que **no**
incluye ninguno de la familia `next-x-hours`/`olderthan-x-*`: agregarlo sería tocar la
herramienta, y no es lo que este playbook decide. Se aplica entonces la alternativa que `05` ya
tenía decidida: **se muestra la columna `sanic_claimhasta` tal cual y el ejecutivo lee la hora**
(`02-diccionario-datos.md` DD `sanic_claimhasta`: "la vista la muestra tal cual"). El filtro que
sí se aplica (`not-null`) no intenta representar "vigente ahora": solo separa las filas que
**alguna vez** tuvieron una reserva del bot de las que nunca la tuvieron, que es lo que distingue
esta vista de "Filas" (12.2, la que muestra todas). No es una tercera vía sobre el detalle de
fecha: es el filtro mínimo para que la vista tenga sentido, y ya estaba disponible en la lista
cerrada de operadores.

El `fetchxml` y el `layoutxml` NO se escriben acá: los genera la herramienta desde las columnas,
el orden y el filtro.

## 1. Identidad

```json
{
  "tipo_playbook": "vista",
  "version_skill": "0.1.0",
  "proyecto": "2026-001-referencias-planes-pago",
  "inventario": "12.2",
  "fase": 2,
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
  "nombre": "Tomadas por el bot",
  "tabla": "sanic_mppp_tbl_fila",
  "clase": "Publica",
  "descripcion": "Filas que el bot reservo alguna vez, con la hora hasta la que vale la reserva. No filtra por vigencia (no hay operador de FetchXML para eso sin una ventana acotada): el ejecutivo lee sanic_claimhasta.",
  "pordefecto": false,
  "columnas": [
    {
      "nombre": "sanic_solicitudid",
      "ancho": 130
    },
    {
      "nombre": "sanic_numerofila",
      "ancho": 70
    },
    {
      "nombre": "sanic_estado",
      "ancho": 130
    },
    {
      "nombre": "sanic_claimhasta",
      "ancho": 130
    }
  ],
  "orden": [
    {
      "columna": "sanic_claimhasta",
      "sentido": "asc"
    }
  ],
  "filtro": {
    "tipo": "and",
    "condiciones": [
      {
        "columna": "sanic_claimhasta",
        "operador": "not-null"
      },
      {
        "columna": "statecode",
        "operador": "eq",
        "valores": [
          0
        ]
      }
    ],
    "enlaces": []
  }
}
```

## 3. Precondiciones

- La tabla Fila existe con `sanic_claimhasta` (P-03, fase 2, verificado contra
  `EntityDefinitions('sanic_mppp_tbl_fila')/Attributes` el 2026-09-25: `DateTime`).
