# Playbook: formulario · Fila · Auditoria

Inventario 12.3, **agregado el 2026-11-27 por DN-09** (`DIRECTRICES-NUEVAS.md` §D). Segundo formulario principal de Fila, **asignado solo al rol `SR - MPPP - Auditor`**; los demás roles siguen con el formulario «Fila» de hoy (`playbooks/formulario/fila.md`). Es el formulario al que llega el auditor desde el chip «Fila 3» del timeline.

Campos, los de DN-09 §D: N° · Estado · Gestión · Plan · Beneficiario · Tipo y número de identificación · Cuenta · Banco · Moneda · Referencia recibida y vigente · Mensaje al cliente · Nota interna · Digitó/cuándo · Aprobó/cuándo. N° y Estado van en el encabezado (con la Solicitud, que es el enlace de vuelta); el resto en tres secciones. No lleva Clasificación ni Fecha validada, que DN-09 no pide.

**El número de cuenta y el de identificación** (decisión 2 de DN-09: el auditor los ve completos) están bajo el perfil de columna `CSP - MPPP - Datos sensibles` (inventario 6.1). Que el auditor los vea completos depende de que **su equipo sea miembro del perfil** (tarea de administración 13.7), no de este formulario. Hasta entonces los ve enmascarados (`********`).

Los títulos de sección van sin tildes (BP-PP-197).

## 0. NO CONSTRUIR TODAVÍA: `formulario.py` no puede con este playbook

Este playbook declara lo que el aprobador pidió (DN-09 §D: un segundo formulario principal por tabla, visible **solo** para el rol `SR - MPPP - Auditor`). `herramientas/construir/formulario.py` hoy hace dos cosas que lo vuelven **peligroso** para este caso, verificadas leyendo el código el 2026-11-27:

| # | Qué hace hoy la herramienta | Qué pasaría con este playbook |
|---|---|---|
| 1 | `_leer_formulario(dv, tabla, tipo)` busca el Main **solo por tabla y tipo**, sin nombre, y **ACTUALIZA el que existe** (su docstring, decisión 1: «ACTUALIZA el formulario principal que ya existe, no crea uno nuevo»). Si encuentra dos Main, lanza `ErrorEntorno` | Con el formulario `Fila` ya construido, el `PATCH` le **cambiaría el nombre a «Auditoria» y le reemplazaría el `formxml`**: el formulario de los demás roles desaparecería. Y en cuanto exista el segundo Main, **todo** `formulario.py` de esa tabla (incluida la corrida de `Fila`) falla con «se esperaba uno solo» |
| 2 | No lee ni escribe **ninguna** asignación por rol: el esquema (`CLAVES`) no tiene `roles` y el `formxml` que arma no trae `<DisplayConditions>` | Un playbook con la clave `roles` es rechazado («sobran [roles]»). Sin la clave, el formulario nacería visible para todos |

**Cambio mínimo propuesto a la herramienta** (lo decide quien dirige la construcción; este playbook no lo hace):

1. Clave opcional `roles` (lista de nombres de rol). Cuando está presente, el formulario es **adicional**: `_leer_formulario` agrega `and name eq '<nombre>'` al filtro, de modo que se busca el formulario por su nombre y **se crea** (`POST`) si no existe, sin tocar al Main existente. La guarda «uno solo» pasa a ser «uno solo con ese nombre».
2. Con `roles`, la herramienta resuelve cada nombre a su `roleid` en la unidad raíz (igual que `app.py::_rol`) y escribe en el `formxml` el bloque de visibilidad `<DisplayConditions FallbackForm="false" Order="…"><Role Id="{roleid}" /></DisplayConditions>`. **El XML exacto hay que sacarlo de una muestra real** (asignar el rol a mano a un formulario de prueba, exportar la solución y leerlo), como ya se hizo con los `classid`; no se escribe de memoria. El `roleid` cambia por entorno: se resuelve en cada corrida y la verificación lo compara.
3. El formulario existente (`Fila`) tiene que quedar **«Everyone» y habilitado como fallback**, que es la estrategia «formulario genérico» de Microsoft Learn (*Manage model-driven app forms settings* → *Set the fallback form for a table*). Probablemente ya lo está (un Main sin condiciones se comporta así), pero hay que **comprobarlo en la muestra**, no suponerlo.
4. **Orden de formularios** (*Form order*): el de Auditoría tiene que ir **primero** (de más exclusivo a menos exclusivo), o el auditor abre el formulario genérico por omisión. Es un dato de la tabla/solución, no del `formxml` de un formulario; la herramienta tampoco lo cubre hoy. Si no se automatiza, es un paso manual documentado (Solutions → tabla → Forms → Form settings → Form order).
5. Verificación nueva: `roles` del playbook = roles del `formxml` del entorno; y el formulario existente sigue con su nombre y su `formid`.

Mientras eso no exista, **no se corre `formulario.py` sobre este playbook**: la validación offline del bloque de la sección 2 sí pasa (se comprobó con la herramienta, sin entorno), pero la construcción dañaría el formulario `Fila`.

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

El bloque cumple el esquema **actual** de `formulario.py` (sin `roles`): la asignación al rol está en la sección 0 y en la sección 5, no en el JSON.

```json
{
  "tipo": "formulario",
  "nombre": "Auditoria",
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

Hoy: **no se construye** (sección 0). Con el cambio propuesto en la herramienta:

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
