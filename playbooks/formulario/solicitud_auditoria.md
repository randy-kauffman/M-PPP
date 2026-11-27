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

## 0. NO CONSTRUIR TODAVÍA: `formulario.py` no puede con este playbook

Este playbook declara lo que el aprobador pidió (DN-09 §D: un segundo formulario principal por tabla, visible **solo** para el rol `SR - MPPP - Auditor`). `herramientas/construir/formulario.py` hoy hace dos cosas que lo vuelven **peligroso** para este caso, verificadas leyendo el código el 2026-11-27:

| # | Qué hace hoy la herramienta | Qué pasaría con este playbook |
|---|---|---|
| 1 | `_leer_formulario(dv, tabla, tipo)` busca el Main **solo por tabla y tipo**, sin nombre, y **ACTUALIZA el que existe** (su docstring, decisión 1: «ACTUALIZA el formulario principal que ya existe, no crea uno nuevo»). Si encuentra dos Main, lanza `ErrorEntorno` | Con el formulario `Solicitud` ya construido, el `PATCH` le **cambiaría el nombre a «Auditoria» y le reemplazaría el `formxml`**: el formulario de los demás roles desaparecería. Y en cuanto exista el segundo Main, **todo** `formulario.py` de esa tabla (incluida la corrida de `Solicitud`) falla con «se esperaba uno solo» |
| 2 | No lee ni escribe **ninguna** asignación por rol: el esquema (`CLAVES`) no tiene `roles` y el `formxml` que arma no trae `<DisplayConditions>` | Un playbook con la clave `roles` es rechazado («sobran [roles]»). Sin la clave, el formulario nacería visible para todos |

**Cambio mínimo propuesto a la herramienta** (lo decide quien dirige la construcción; este playbook no lo hace):

1. Clave opcional `roles` (lista de nombres de rol). Cuando está presente, el formulario es **adicional**: `_leer_formulario` agrega `and name eq '<nombre>'` al filtro, de modo que se busca el formulario por su nombre y **se crea** (`POST`) si no existe, sin tocar al Main existente. La guarda «uno solo» pasa a ser «uno solo con ese nombre».
2. Con `roles`, la herramienta resuelve cada nombre a su `roleid` en la unidad raíz (igual que `app.py::_rol`) y escribe en el `formxml` el bloque de visibilidad `<DisplayConditions FallbackForm="false" Order="…"><Role Id="{roleid}" /></DisplayConditions>`. **El XML exacto hay que sacarlo de una muestra real** (asignar el rol a mano a un formulario de prueba, exportar la solución y leerlo), como ya se hizo con los `classid`; no se escribe de memoria. El `roleid` cambia por entorno: se resuelve en cada corrida y la verificación lo compara.
3. El formulario existente (`Solicitud`) tiene que quedar **«Everyone» y habilitado como fallback**, que es la estrategia «formulario genérico» de Microsoft Learn (*Manage model-driven app forms settings* → *Set the fallback form for a table*). Probablemente ya lo está (un Main sin condiciones se comporta así), pero hay que **comprobarlo en la muestra**, no suponerlo.
4. **Orden de formularios** (*Form order*): el de Auditoría tiene que ir **primero** (de más exclusivo a menos exclusivo), o el auditor abre el formulario genérico por omisión. Es un dato de la tabla/solución, no del `formxml` de un formulario; la herramienta tampoco lo cubre hoy. Si no se automatiza, es un paso manual documentado (Solutions → tabla → Forms → Form settings → Form order).
5. Verificación nueva: `roles` del playbook = roles del `formxml` del entorno; y el formulario existente sigue con su nombre y su `formid`.

Mientras eso no exista, **no se corre `formulario.py` sobre este playbook**: la validación offline del bloque de la sección 2 sí pasa (se comprobó con la herramienta, sin entorno), pero la construcción dañaría el formulario `Solicitud`.

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

Hoy: **no se construye** (sección 0). Con el cambio propuesto en la herramienta:

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
