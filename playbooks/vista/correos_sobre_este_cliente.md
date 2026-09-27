# Playbook: vista · Correos autorizados sobre este cliente

Inventario 12.2 (`05-app-model-driven.md` §vistas). Vista nueva (18.ª), agregada el 2026-09-27:
DN-01 creó `sanic_mppp_tbl_autorizacioncliente` (correo autorizado sobre TODOS los planes de un
cliente) y nunca se mostró en ningún formulario. **No es la subgrilla "Correos autorizados del
cliente" que se quitó el 2026-09-24** (`05` §4, fila de Cliente): aquella mostraba `Autorizado` por
un lookup directo a Cliente que ya no existe ("un correo ya no pertenece a un cliente"). Esta es
distinta: muestra la tabla de unión `AutorizacionCliente` (DN-01, un correo autoriza TODOS los
planes de un cliente a la vez), el mismo patrón que ya usa `AutorizacionPlan` para la subgrilla del
formulario de Plan (`correos_sobre_este_plan.md`).

Subgrilla del formulario de Cliente: los correos que pueden modificar cualquier plan de ese cliente.

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
  "nombre": "Correos autorizados sobre este cliente",
  "tabla": "sanic_mppp_tbl_autorizacioncliente",
  "clase": "Publica",
  "descripcion": "Subgrilla del formulario de Cliente: los correos que pueden modificar cualquier plan de ese cliente (DN-01).",
  "pordefecto": false,
  "columnas": [
    {
      "nombre": "sanic_nombre",
      "ancho": 260,
      "enlace": "autorizado"
    },
    {
      "nombre": "sanic_nombre",
      "ancho": 260
    }
  ],
  "orden": [
    {
      "columna": "sanic_nombre",
      "sentido": "asc"
    }
  ],
  "filtro": {
    "tipo": "and",
    "condiciones": [
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
        "tabla": "sanic_mppp_tbl_autorizado",
        "de": "sanic_mppp_tbl_autorizadoid",
        "a": "sanic_autorizadoid",
        "alias": "autorizado"
      }
    ]
  }
}
```

Dos columnas, adrede: la primera (`autorizado.sanic_nombre`) es el correo tal cual vive en
`Autorizado`, igual que en `correos_sobre_este_plan.md`. `AutorizacionCliente` **no tiene**
`sanic_fechadocumento` (esa tabla no lleva evidencia, ver `playbooks/tabla/sanic_mppp_tbl_autorizacioncliente.md`
§2, nota para el aprobador), así que no hay una columna propia de fecha para ordenar como en la de
Plan. Por eso se agrega la propia `sanic_nombre` de `AutorizacionCliente` (el nombre calculado
"correo → cliente") como segunda columna: es la única columna propia de la tabla, y ordenar por una
columna enlazada (`autorizado.sanic_nombre`) no es un camino que la herramienta ejercite en ningún
otro playbook de este proyecto — se prefiere ordenar por la columna propia, probada, antes que
inventar un uso nuevo de `vista.py` sin poder verificarlo primero.

## 3. Precondiciones

- La tabla y sus relaciones existen (`sanic_mppp_tbl_autorizacioncliente`, inventario 3.13;
  `sanic_mppp_cliente_autorizacioncliente` y `sanic_mppp_autorizado_autorizacioncliente`, inventario
  4.12 y 4.13).
