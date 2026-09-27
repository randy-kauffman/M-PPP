# Playbook: vista · Clientes que puede modificar

Inventario 12.2 (`05-app-model-driven.md` §vistas). Vista nueva (19.ª), agregada el 2026-09-27,
pedido textual del aprobador: "quiero agregar un nuevo correo autorizado al cliente y solo me sale
'Nombre' y 'Owner'" y, junto con eso, poder ver desde el correo con cuántos CLIENTES está
relacionado (hoy solo ve los planes).

Subgrilla del formulario de Autorizado: los clientes que ese correo puede modificar. Es el espejo
exacto de `planes_que_puede_modificar.md` (misma tabla de unión, mismo lado "Autorizado"), pero del
lado `AutorizacionCliente` en vez de `AutorizacionPlan`; y es hermana de `correos_sobre_este_cliente.md`
(mismo patrón de columna propia para ordenar, por el mismo motivo: `AutorizacionCliente` no tiene
`sanic_fechadocumento`, ver `playbooks/tabla/sanic_mppp_tbl_autorizacioncliente.md` §2, nota para el
aprobador).

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
  "nombre": "Clientes que puede modificar",
  "tabla": "sanic_mppp_tbl_autorizacioncliente",
  "clase": "Publica",
  "descripcion": "Subgrilla del formulario de Autorizado: los clientes que ese correo puede modificar (DN-01).",
  "pordefecto": false,
  "columnas": [
    {
      "nombre": "sanic_nombre",
      "ancho": 240,
      "enlace": "cliente"
    },
    {
      "nombre": "sanic_cifbac",
      "ancho": 130,
      "enlace": "cliente"
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
        "tabla": "sanic_mppp_tbl_cliente",
        "de": "sanic_mppp_tbl_clienteid",
        "a": "sanic_clienteid",
        "alias": "cliente"
      }
    ]
  }
}
```

Tres columnas, adrede. Las dos primeras (`cliente.sanic_nombre`, `cliente.sanic_cifbac`) son "nombre
y el identificador que corresponda" del cliente: la Razón social y el **CIF BAC**, el número de
cliente propio del banco (`playbooks/tabla/sanic_mppp_tbl_cliente.md` §2: "Número de cliente en
BAC"), no el CIF COM (identificador comercial/fiscal externo). La tercera es la propia `sanic_nombre`
de `AutorizacionCliente` (el nombre calculado "correo → cliente"): igual que en
`correos_sobre_este_cliente.md`, es la única columna propia de la tabla y la que se usa para ordenar.
Ordenar por una columna enlazada (`cliente.sanic_nombre`) no es un camino que la herramienta ejercite
en ningún otro playbook de este proyecto — se prefiere la columna propia, probada, antes que inventar
un uso nuevo de `vista.py` sin poder verificarlo primero (mismo razonamiento que
`correos_sobre_este_cliente.md` §2).

## 3. Precondiciones

- La tabla y sus relaciones existen (`sanic_mppp_tbl_autorizacioncliente`, inventario 3.13;
  `sanic_mppp_cliente_autorizacioncliente` y `sanic_mppp_autorizado_autorizacioncliente`, inventario
  4.12 y 4.13).
