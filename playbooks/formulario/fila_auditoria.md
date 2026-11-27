# Playbook: formulario · Fila · Auditoria

Inventario 12.3, **agregado el 2026-11-27 por DN-09** (`DIRECTRICES-NUEVAS.md` §D). Segundo formulario principal de Fila, **asignado solo al rol `SR - MPPP - Auditor`**; los demás roles siguen con el formulario «Fila» de hoy (`playbooks/formulario/fila.md`). Es el formulario al que llega el auditor desde el chip «Fila 3» del timeline.

Campos, los de DN-09 §D: N° · Estado · Gestión · Plan · Beneficiario · Tipo y número de identificación · Cuenta · Banco · Moneda · Referencia recibida y vigente · Mensaje al cliente · Nota interna · Digitó/cuándo · Aprobó/cuándo. N° y Estado van en el encabezado (con la Solicitud, que es el enlace de vuelta); el resto en tres secciones. No lleva Clasificación ni Fecha validada, que DN-09 no pide.

**El número de cuenta y el de identificación** (decisión 2 de DN-09: el auditor los ve completos) están bajo el perfil de columna `CSP - MPPP - Datos sensibles` (inventario 6.1). Que el auditor los vea completos depende de que **su equipo sea miembro del perfil** (tarea de administración 13.7), no de este formulario. Hasta entonces los ve enmascarados (`********`).

Los títulos de sección van sin tildes (BP-PP-197).

> **Formulario por rol (DN-09).** Lleva `roles`: `formulario.py` lo busca por su nombre, lo crea al lado del
> formulario general de la tabla (que no se toca) y le escribe `<DisplayConditions Order="0">` con el rol de la
> unidad raíz. El rol tiene que existir antes (`playbooks/rol/sr_mppp_auditor.md`). Soporte agregado a la
> herramienta el 2026-11-27.

## 1. Identidad

```json
{
  "tipo_playbook": "formulario",
  "version_skill": "0.1.0",
  "proyecto": "2026-001-referencias-planes-pago",
  "inventario": "12.3 (DN-09)",
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

La asignación al rol va en la clave `roles` del bloque: `formulario.py` la convierte en `<DisplayConditions>`.

```json
{
  "tipo": "formulario",
  "nombre": "Auditoria",
  "roles": ["SR - MPPP - Auditor"],
  "tabla": "sanic_mppp_tbl_fila",
  "solo_lectura": true,
  "descripcion": "Formulario principal de Auditoria para Fila. Solo lectura y solo para el rol Auditor: que se recibio, que se hizo con el registro y quien.",
  "pestana": "General",
  "encabezado": [
    "sanic_numerofila",
    "sanic_estado",
    "sanic_solicitudid"
  ],
  "secciones": [
    {
      "titulo": "Que recibimos",
      "campos": [
        "sanic_gestion",
        "sanic_planid",
        "sanic_nombrebeneficiario",
        "sanic_tipoidentificacion",
        "sanic_numeroidentificacion",
        "sanic_numerocuenta",
        "sanic_banco",
        "sanic_moneda",
        "sanic_referenciarecibida",
        "sanic_referencia"
      ]
    },
    {
      "titulo": "Que se respondio",
      "campos": [
        "sanic_mensaje",
        "sanic_notainterna"
      ]
    },
    {
      "titulo": "Quien actuo",
      "campos": [
        "sanic_digitadapor",
        "sanic_fechadigitada",
        "sanic_aprobadapor",
        "sanic_fechaaprobada"
      ]
    }
  ]
}
```

## 3. Precondiciones

- Existe el rol `SR - MPPP - Auditor` (`playbooks/rol/sr_mppp_auditor.md`).
- La tabla Fila, sus columnas y los lookups `sanic_solicitudid`, `sanic_planid`, `sanic_digitadapor` y `sanic_aprobadapor`.
- El formulario «Fila» de hoy está construido y **no se toca**.
- Para ver las dos columnas protegidas completas: el equipo del Auditor como miembro de `CSP - MPPP - Datos sensibles` (administración 13.7).

## 4. Cómo se construye

Después del rol `SR - MPPP - Auditor` y del web resource del timeline:

```
python3 herramientas/construir/formulario.py playbooks/formulario/fila_auditoria.md
```

## 5. Verificación

```
python3 herramientas/construir/formulario.py playbooks/formulario/fila_auditoria.md --solo-verificar
```

Además de lo que ya verifica la herramienta (nombre, descripción, `formxml`, pertenencia a la solución, que no sea managed):

| # | Comprobación | Esperado |
|---|---|---|
| 1 | Hay **dos** formularios principales de la tabla | `Fila` y `Auditoria`, cada uno con su `formid`; el primero **sin cambios** |
| 2 | Visibilidad de `Auditoria` | solo el rol `SR - MPPP - Auditor`; **no** es fallback |
| 3 | Visibilidad de `Fila` | todos (`Everyone`) y fallback |
| 4 | Orden de formularios | `Auditoria` primero |
| 5 | En vivo, con un usuario que tiene **solo** el rol Auditor | abre `Auditoria` por omisión |
| 6 | En vivo, con un Ejecutivo, un Supervisor y un Administrador de planes | abren `Fila`; el selector de formularios **no** les ofrece `Auditoria` |
| 7 | Todo el formulario es de lectura | ningún campo editable; el auditor no tiene `prvWrite` y la herramienta marca `solo_lectura` |
