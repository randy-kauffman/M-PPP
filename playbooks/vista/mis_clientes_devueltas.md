# Playbook: vista · Mis clientes - devueltas

Inventario 12.2 (`05-app-model-driven.md` §vistas, lista cerrada de 17).

Filas que el supervisor devolvio al ejecutivo, con el motivo. Entrada de sitemap Devueltas.

El `fetchxml` y el `layoutxml` NO se escriben aca: los genera la herramienta
desde las columnas, el orden y el filtro.

## 1. Identidad

```json
{
  "tipo_playbook": "vista",
  "version_skill": "0.1.0",
  "proyecto": "2026-001-referencias-planes-pago",
  "inventario": "12.2",
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
  "nombre": "Mis clientes - devueltas",
  "tabla": "sanic_mppp_tbl_fila",
  "clase": "Publica",
  "descripcion": "Filas que el supervisor devolvio al ejecutivo, con el motivo. Entrada de sitemap Devueltas.",
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
      "nombre": "sanic_requiereatencionmanual",
      "ancho": 60
    },
    {
      "nombre": "sanic_estado",
      "ancho": 130
    },
    {
      "nombre": "sanic_gestion",
      "ancho": 110
    },
    {
      "nombre": "sanic_clasificacion",
      "ancho": 100
    },
    {
      "nombre": "sanic_planid",
      "ancho": 90
    },
    {
      "nombre": "sanic_referencia",
      "ancho": 170
    },
    {
      "nombre": "sanic_nombrebeneficiario",
      "ancho": 200
    },
    {
      "nombre": "sanic_tipoidentificacion",
      "ancho": 90
    },
    {
      "nombre": "sanic_numeroidentificacion",
      "ancho": 140
    },
    {
      "nombre": "sanic_numerocuenta",
      "ancho": 140
    },
    {
      "nombre": "sanic_moneda",
      "ancho": 80
    },
    {
      "nombre": "sanic_banco",
      "ancho": 110
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
        "columna": "sanic_estado",
        "operador": "eq",
        "valores": [
          159460003
        ]
      },
      {
        "columna": "sanic_notainterna",
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
    "enlaces": [
      {
        "tabla": "sanic_mppp_tbl_plan",
        "de": "sanic_mppp_tbl_planid",
        "a": "sanic_planid",
        "alias": "plan",
        "enlaces": [
          {
            "tabla": "sanic_mppp_tbl_cliente",
            "de": "sanic_mppp_tbl_clienteid",
            "a": "sanic_clienteid",
            "alias": "cliente",
            "condiciones": [
              {
                "columna": "ownerid",
                "operador": "eq-userid"
              }
            ]
          }
        ]
      }
    ]
  }
}
```

## 3. Precondiciones

- La tabla y sus relaciones existen (P-03, P-04).
- **Columna `sanic_requiereatencionmanual` agregada el 2026-09-27** (hallazgo del aprobador). Se
  evaluo si correspondia sumarla acá (argumento en contra: la fila devuelta ya trae su propio motivo
  visible en `sanic_notainterna`, y sumar columnas encarece la grilla) y **se decidio que si
  corresponde**, porque el argumento en contra no aguanta un detalle tecnico: `sanic_notainterna` es
  la MISMA columna que usa el bot para dejar "el porqué" cuando marca `atencionmanual` (`03`
  §"Transición `atencionmanual`": "Prende `sanic_requiereatencionmanual`, deja el porqué en
  `sanic_notainterna` y libera el claim"), y esa transición **no cambia el estado** (queda Validada).
  Sin la columna nueva, el ejecutivo que abre "Mis clientes - devueltas" y ve una nota interna no
  puede distinguir si la escribio el supervisor al devolver la fila o si la dejo el bot al marcarla
  para atencion manual: son dos motivos distintos compartiendo el mismo texto. Va segunda, pegada a
  `sanic_numerofila` y ANTES de `sanic_estado`, ancho chico (60) por ser un Si/No.
