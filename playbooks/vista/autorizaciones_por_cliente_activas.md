# Playbook: vista · Autorizaciones por cliente activas

Inventario 12.2 (`05-app-model-driven.md` §vistas). Vista nueva (20.ª), agregada el 2026-09-27,
pedido textual del aprobador: diferenciar "Autorizaciones - Planes" de "Autorizaciones - Clientes"
en el sitemap. Es la vista **por defecto** que abre la entrada nueva `subarea_autorizacionescliente`
(`playbooks/app/mantenimientoppp.md`).

Calcada de `autorizaciones_activas.md` (la de `AutorizacionPlan`), sobre `AutorizacionCliente`: mismo
patrón, sin la columna `sanic_fechadocumento` porque esta tabla no la tiene (ver
`playbooks/tabla/sanic_mppp_tbl_autorizacioncliente.md` §2, nota para el aprobador). El nombre
calculado (`sanic_nombre`, "correo → cliente") ya identifica la autorización sin falta de una
segunda columna.

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
  "nombre": "Autorizaciones por cliente activas",
  "tabla": "sanic_mppp_tbl_autorizacioncliente",
  "clase": "Publica",
  "descripcion": "Autorizaciones de correo sobre cliente vigentes (DN-01).",
  "pordefecto": true,
  "columnas": [
    {
      "nombre": "sanic_nombre",
      "ancho": 280
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
    "enlaces": []
  }
}
```

## 3. Precondiciones

- La tabla y sus relaciones existen (P-03, P-04; DN-01).
