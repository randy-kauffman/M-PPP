# Playbook: vista · Para atención manual

Inventario 12.2 (`05-app-model-driven.md` §"Fase 2: dos vistas y un comando más"). **+1 vista
sobre la lista cerrada de 17** (más "Filas", ya son 18: esta es la 19).

Filas que el bot marcó para atención manual (`sanic_requiereatencionmanual` = sí): se topó con
algo que no sabe resolver y la fila pasó a ser de un ejecutivo. Mismo patrón que *Para revisar*
en Solicitud.

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
  "nombre": "Para atencion manual",
  "tabla": "sanic_mppp_tbl_fila",
  "clase": "Publica",
  "descripcion": "Filas que el bot marco para atencion manual: se topo con algo que no sabe resolver y la fila paso a ser de un ejecutivo. Mismo patron que Para revisar en Solicitud.",
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
      "nombre": "sanic_planid",
      "ancho": 90
    },
    {
      "nombre": "sanic_nombrebeneficiario",
      "ancho": 200
    },
    {
      "nombre": "sanic_notainterna",
      "ancho": 300
    }
  ],
  "orden": [
    {
      "columna": "sanic_solicitudid",
      "sentido": "asc"
    },
    {
      "columna": "sanic_numerofila",
      "sentido": "asc"
    }
  ],
  "filtro": {
    "tipo": "and",
    "condiciones": [
      {
        "columna": "sanic_requiereatencionmanual",
        "operador": "eq",
        "valores": [
          true
        ]
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

- La tabla Fila existe con `sanic_requiereatencionmanual` (P-03, fase 2, verificado contra
  `EntityDefinitions('sanic_mppp_tbl_fila')/Attributes` el 2026-09-25: `Boolean`).
