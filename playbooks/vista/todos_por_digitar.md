# Playbook: vista · Todos - por digitar

Inventario 12.2 (`05-app-model-driven.md` §vistas, lista cerrada de 17).

Filas validadas de todos los clientes, con la columna Cliente. La misma pantalla sin el filtro de cartera.

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
  "nombre": "Todos - por digitar",
  "tabla": "sanic_mppp_tbl_fila",
  "clase": "Publica",
  "descripcion": "Filas validadas de todos los clientes, con la columna Cliente. La misma pantalla sin el filtro de cartera.",
  "pordefecto": false,
  "columnas": [
    {
      "nombre": "sanic_solicitudid",
      "ancho": 130
    },
    {
      "nombre": "sanic_gestion",
      "ancho": 110
    },
    {
      "nombre": "sanic_nombre",
      "ancho": 180,
      "enlace": "cliente"
    },
    {
      "nombre": "sanic_planid",
      "ancho": 90
    },
    {
      "nombre": "sanic_numeroplan",
      "ancho": 131
    },
    {
      "nombre": "sanic_tipoformato",
      "ancho": 169,
      "enlace": "a_2bac4e7b833b4f99a5af50f1ba99ebc8"
    },
    {
      "nombre": "sanic_clasificacion",
      "ancho": 100
    },
    {
      "nombre": "sanic_nombrebeneficiario",
      "ancho": 200
    },
    {
      "nombre": "sanic_referencia",
      "ancho": 170
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
      "nombre": "sanic_fechavalidada",
      "ancho": 130
    }
  ],
  "orden": [
    {
      "columna": "sanic_solicitudid",
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
            "alias": "cliente"
          }
        ]
      },
      {
        "tabla": "sanic_mppp_tbl_plan",
        "de": "sanic_mppp_tbl_planid",
        "a": "sanic_planid",
        "alias": "a_2bac4e7b833b4f99a5af50f1ba99ebc8"
      }
    ]
  }
}
```

## 3. Precondiciones

- La tabla y sus relaciones existen (P-03, P-04).
- **Columna `sanic_requiereatencionmanual` agregada el 2026-09-27** (hallazgo del aprobador), misma
  posicion y mismo motivo que en "Mis clientes - por digitar": segunda columna, pegada a
  `sanic_numerofila` y ANTES de `sanic_estado`, ancho chico (60) por ser un Si/No.

## Cambio manual del aprobador 2026-11-27 que la herramienta no genera

Hecho en Dev con el diseñador de vistas; el playbook se alineó con lo que Dev tiene (esto reemplaza la
columna `sanic_requiereatencionmanual` de la nota anterior: el aprobador la quitó de ESTA vista; sigue en
"Mis clientes - por digitar").

- Se quitaron las columnas Número de fila (`sanic_numerofila`), Requiere atención manual y Estado (`sanic_estado`; el filtro por estado sigue).
- Se quitó el segundo criterio de orden (`sanic_numerofila`); queda solo `sanic_solicitudid` ascendente.
- Cliente (`cliente.sanic_nombre`) pasó a ser la tercera columna, después de Gestión.
- Se agregaron `sanic_numeroplan` (ancho 131) después de Plan y el Tipo de formato del plan (`sanic_tipoformato`, ancho 169).

Lo que la herramienta NO puede generar y por eso `--solo-verificar` seguirá diciendo `difiere`: en Dev el
Tipo de formato viene de un `link-entity` a `sanic_mppp_tbl_plan` con alias `a_2bac4e7b833b4f99a5af50f1ba99ebc8`,
`link-type="outer"` y `visible="false"`; la herramienta solo escribe `inner` (aquí es equivalente en
resultado porque ya hay un enlace interno a Plan, pero no es el mismo XML), y además Dev guarda
`<attribute name="sanic_numeroplan"/>` y ese enlace DESPUÉS de los enlaces existentes, mientras que la
herramienta pone todos los atributos propios antes. Un `--completar` reescribiría la vista con la forma
de la herramienta (mismas columnas y filas, sin `outer`/`visible`).
