# Playbook: vista · Bitacora - Auditoria

Inventario 12.2, **agregada el 2026-11-27 por DN-09** (`DIRECTRICES-NUEVAS.md` §D). Vista de la entrada «Bitácora» del área Auditoría del sitemap.

Sirve para la pregunta transversal: «todo lo que hizo tal persona», «todos los errores del mes». Por eso lleva la **Solicitud** (lookup) y la **Fila** además del evento, el actor, el origen y el detalle, y va **de la más reciente a la más antigua**. La vista «Eventos de la solicitud» (subgrilla del formulario de Solicitud) es la cronológica ascendente de UNA solicitud; esta no la reemplaza.

**Todas, activas e inactivas** (filtro vacío), igual que «Solicitudes - Auditoria».

**Una columna más que la lista de DN-09 §D:** `sanic_nombre` (el `BIT-…` autonumérico). `vista.py` exige que toda columna de orden se muestre, y el segundo criterio de orden tiene que ser `sanic_nombre` descendente porque `sanic_fechaevento` empata seguido (ver la nota de `eventos_de_la_solicitud.md`); sin él, eventos de la misma hora salen en un orden que la plataforma elige. Va primero y angosta. Si el aprobador la prefiere fuera, se quita el desempate y se acepta el orden indefinido entre empates.

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
  "nombre": "Bitacora - Auditoria",
  "tabla": "sanic_mppp_tbl_bitacora",
  "clase": "Publica",
  "descripcion": "Todos los eventos de la Bitacora, activos e inactivos, del mas reciente al mas antiguo, con solicitud, fila, actor, origen y detalle. Entrada Bitacora del area Auditoria.",
  "pordefecto": false,
  "columnas": [
    {
      "nombre": "sanic_nombre",
      "ancho": 110
    },
    {
      "nombre": "sanic_fechaevento",
      "ancho": 150
    },
    {
      "nombre": "sanic_solicitudid",
      "ancho": 130
    },
    {
      "nombre": "sanic_evento",
      "ancho": 190
    },
    {
      "nombre": "sanic_numerofila",
      "ancho": 70
    },
    {
      "nombre": "sanic_actortexto",
      "ancho": 160
    },
    {
      "nombre": "sanic_origen",
      "ancho": 130
    },
    {
      "nombre": "sanic_detalle",
      "ancho": 320
    }
  ],
  "orden": [
    {
      "columna": "sanic_fechaevento",
      "sentido": "desc"
    },
    {
      "columna": "sanic_nombre",
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

- La tabla Bitacora existe (P-03) con sus columnas `sanic_fechaevento`, `sanic_evento`, `sanic_origen`, `sanic_actortexto`, `sanic_numerofila` y `sanic_detalle`, y el lookup `sanic_solicitudid` (relación `sanic_mppp_solicitud_bitacora`).

## 4. Cómo se construye

```
python3 herramientas/construir/vista.py playbooks/vista/bitacora_auditoria.md
```

## 5. Verificación

```
python3 herramientas/construir/vista.py playbooks/vista/bitacora_auditoria.md --solo-verificar
```

Segunda ejecución sin `--solo-verificar`: estado `ya_existia`. La vista figura una vez en la solución y su `querytype` es el de una vista pública.
