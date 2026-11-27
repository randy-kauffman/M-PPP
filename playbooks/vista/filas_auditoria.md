# Playbook: vista · Filas - Auditoria

Inventario 12.2, **agregada el 2026-11-27 por DN-09** (`DIRECTRICES-NUEVAS.md` §D, sección 3 del formulario «Auditoria» de Solicitud). Es la subgrilla «Qué pasó con cada registro»: **no está en la lista de vistas de DN-09 §D, pero la subgrilla la necesita**, porque «Filas» (la vista de la subgrilla de hoy) no trae quién digitó ni quién aprobó, y las demás vistas de Fila filtran por estado.

Contesta la pregunta 4 de DN-09: qué pasó con cada registro y quién lo hizo. N° · Gestión · Plan · Beneficiario · Estado · Digitó · Aprobó · Mensaje al cliente. Sin `statecode` en el filtro: todas las filas de la solicitud, activas o no. En una subgrilla el filtro por solicitud lo pone la relación, no la vista.

Orden por N° de fila ascendente: es el orden en que vienen en el Excel, el que el auditor compara con lo que recibió. `sanic_numerofila` se muestra, que es lo que exige `vista.py` para ordenar por él.

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
  "nombre": "Filas - Auditoria",
  "tabla": "sanic_mppp_tbl_fila",
  "clase": "Publica",
  "descripcion": "Todas las filas de una solicitud con gestion, plan, beneficiario, estado, quien digito, quien aprobo y el mensaje al cliente. Subgrilla del formulario Auditoria de Solicitud.",
  "pordefecto": false,
  "columnas": [
    {
      "nombre": "sanic_numerofila",
      "ancho": 70
    },
    {
      "nombre": "sanic_gestion",
      "ancho": 110
    },
    {
      "nombre": "sanic_planid",
      "ancho": 150
    },
    {
      "nombre": "sanic_nombrebeneficiario",
      "ancho": 200
    },
    {
      "nombre": "sanic_estado",
      "ancho": 130
    },
    {
      "nombre": "sanic_digitadapor",
      "ancho": 150
    },
    {
      "nombre": "sanic_aprobadapor",
      "ancho": 150
    },
    {
      "nombre": "sanic_mensaje",
      "ancho": 300
    }
  ],
  "orden": [
    {
      "columna": "sanic_numerofila",
      "sentido": "asc"
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

- La tabla Fila existe (P-03) con sus columnas y los lookups `sanic_planid`, `sanic_digitadapor` y `sanic_aprobadapor` (inventario 4.8, 4.10 y 4.11).
- Después de crear la vista hay que construir el formulario «Auditoria» de Solicitud, que es el que apunta la subgrilla.

## 4. Cómo se construye

```
python3 herramientas/construir/vista.py playbooks/vista/filas_auditoria.md
```

## 5. Verificación

```
python3 herramientas/construir/vista.py playbooks/vista/filas_auditoria.md --solo-verificar
```

Segunda ejecución sin `--solo-verificar`: estado `ya_existia`. La vista figura una vez en la solución y su `querytype` es el de una vista pública.
