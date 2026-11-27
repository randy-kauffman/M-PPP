# Playbook: formulario · Solicitud · Auditoria

Inventario 12.3, **agregado el 2026-11-27 por DN-09** (`DIRECTRICES-NUEVAS.md` §D). Segundo formulario principal de Solicitud, **asignado solo al rol `SR - MPPP - Auditor`**; los demás roles siguen con el formulario «Solicitud» de hoy (`playbooks/formulario/solicitud.md`).

Una pestaña, de arriba hacia abajo en el orden de las preguntas del auditor (qué recibimos → qué pasó → qué pasó con cada registro → qué respondimos → el recorrido en el tiempo). Todo de solo lectura. El playbook no declara etiquetas ni classid: la herramienta los resuelve contra la metadata.

Cómo se traduce DN-09 §D a lo que `formulario.py` sabe armar (cada diferencia es una limitación de la herramienta, no una decisión nueva):

| DN-09 §D | Aquí | Por qué |
|---|---|---|
| Encabezado: N° · Estado · Recibida · **Cerrada** | `encabezado`: N° · Estado · Recibida | La herramienta admite **3** columnas en el encabezado (el header del Main tiene tres huecos). **Cerrada** pasa a la sección 2, donde se lee junto a «Validada» y los contadores |
| Sección 2 con campos **y** subgrilla Resultado de regla | dos secciones: «2 Que paso con la solicitud» (campos) y «2 Reglas evaluadas» (subgrilla) | Una sección lleva `campos`, `subgrilla` **o** `webresource`, exactamente uno |
| Subgrilla Resultado de regla: Orden · Regla · Resultado · Efecto · Razón | vista existente «Reglas evaluadas» | Ya trae esas cinco columnas y además la fecha de evaluación; no hace falta una vista nueva |
| Subgrilla Filas: N° · Gestión · Plan · Beneficiario · Estado · Digitó · Aprobó · Mensaje | vista **nueva** «Filas - Auditoria» (`playbooks/vista/filas_auditoria.md`) | «Filas», la de la subgrilla de hoy, no trae Digitó ni Aprobó |
| Sección 5: web resource a todo el ancho | `webresource` `sanic_mppp_wr_html_auditoria`, `alto` 20 (el máximo que admite la herramienta) | Se embebe igual que `sanic_mppp_wr_html_comunicacion` en «Solicitud»: control con `PassParameters = true`, que le pasa `id` y `typename` del registro en la cadena de consulta. La herramienta no tiene opción de ancho: el control ocupa la fila de la sección |

Los títulos de sección van sin tildes (BP-PP-197; `exigir_sin_tildes` los revisa).

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
  "tabla": "sanic_mppp_tbl_solicitud",
  "solo_lectura": true,
  "descripcion": "Formulario principal de Auditoria para Solicitud. Solo lectura y solo para el rol Auditor: recorre la solicitud de lo que se recibio a lo que se respondio, con la linea de tiempo.",
  "pestana": "General",
  "encabezado": [
    "sanic_nombre",
    "sanic_estadoprocesamiento",
    "sanic_fecharecibido"
  ],
  "secciones": [
    {
      "titulo": "1 Que recibimos",
      "campos": [
        "sanic_remitente",
        "sanic_asunto",
        "sanic_correocrudo",
        "sanic_exceloriginal",
        "sanic_motivoclasificacion"
      ]
    },
    {
      "titulo": "2 Que paso con la solicitud",
      "campos": [
        "sanic_fechavalidada",
        "sanic_filastotales",
        "sanic_filasvalidas",
        "sanic_filasrechazadas",
        "sanic_requiererevision",
        "sanic_motivorevision",
        "sanic_fechacerrada"
      ]
    },
    {
      "titulo": "2 Reglas evaluadas",
      "subgrilla": {
        "tabla": "sanic_mppp_tbl_resultadoregla",
        "lookup": "sanic_solicitudid",
        "vista": "Reglas evaluadas"
      }
    },
    {
      "titulo": "3 Que paso con cada registro",
      "subgrilla": {
        "tabla": "sanic_mppp_tbl_fila",
        "lookup": "sanic_solicitudid",
        "vista": "Filas - Auditoria"
      }
    },
    {
      "titulo": "4 Que respondimos",
      "campos": [
        "sanic_fechaacuseenviado",
        "sanic_acusecontenido",
        "sanic_fecharespuestafinalenviada",
        "sanic_respuestafinalcontenido"
      ]
    },
    {
      "titulo": "5 Linea de tiempo",
      "webresource": {
        "nombre": "sanic_mppp_wr_html_auditoria",
        "alto": 20
      }
    }
  ]
}
```

## 3. Precondiciones

- Existe el rol `SR - MPPP - Auditor` (`playbooks/rol/sr_mppp_auditor.md`): sin él no se puede asignar el formulario.
- Existe el web resource `sanic_mppp_wr_html_auditoria` y es HTML (`playbooks/web-resource/html_auditoria.md`): `formulario.py` lo exige y se detiene si falta o no es `webresourcetype` 1.
- Existen las vistas de las subgrillas: «Reglas evaluadas» y «Filas - Auditoria» (`playbooks/vista/filas_auditoria.md`).
- La tabla Solicitud, sus columnas y sus relaciones (`sanic_solicitudid` en Fila y en Resultado de regla).
- El formulario «Solicitud» de hoy está construido y **no se toca**.
- **Probar la visibilidad** (verificación 5 y 6 de la sección 5) necesita una persona con solo el rol Auditor, directo y por equipo (DN-09 §Construcción, punto 5).

## 4. Cómo se construye

Después del rol `SR - MPPP - Auditor` y del web resource del timeline:

```
python3 herramientas/construir/formulario.py playbooks/formulario/solicitud_auditoria.md
```

## 5. Verificación

```
python3 herramientas/construir/formulario.py playbooks/formulario/solicitud_auditoria.md --solo-verificar
```

Además de lo que ya verifica la herramienta (nombre, descripción, `formxml`, pertenencia a la solución, que no sea managed):

| # | Comprobación | Esperado |
|---|---|---|
| 1 | Hay **dos** formularios principales de la tabla | `Solicitud` y `Auditoria`, cada uno con su `formid`; el primero **sin cambios** |
| 2 | Visibilidad de `Auditoria` | solo el rol `SR - MPPP - Auditor`; **no** es fallback |
| 3 | Visibilidad de `Solicitud` | todos (`Everyone`) y fallback |
| 4 | Orden de formularios | `Auditoria` primero |
| 5 | En vivo, con un usuario que tiene **solo** el rol Auditor | abre `Auditoria` por omisión |
| 6 | En vivo, con un Ejecutivo, un Supervisor y un Administrador de planes | abren `Solicitud`; el selector de formularios **no** les ofrece `Auditoria` |
| 7 | Todo el formulario es de lectura | ningún campo editable; el auditor no tiene `prvWrite` y la herramienta marca `solo_lectura` |
