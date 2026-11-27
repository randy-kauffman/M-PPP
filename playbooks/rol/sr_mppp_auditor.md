# Playbook: rol · SR - MPPP - Auditor

## 1. Identidad

```json
{
  "tipo_playbook": "rol",
  "version_skill": "0.1.0",
  "proyecto": "2026-001-referencias-planes-pago",
  "inventario": "6.9 (DN-09)",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 y §7, verificados contra Dev el 2026-09-20. El rol lo define `diseno/DIRECTRICES-NUEVAS.md` DN-09 §A (aprobador, 2026-11-27); entra a `diseno/04-matriz-privilegios.md` §2 y a `diseno/06-inventario-componentes.md` §6 como renglón 6.9 (pendiente de anotar ahí).

Advertencias: en Dev existe otro publisher con el mismo prefijo de texto, "Sanic Corp", con otro unique name; no es el de este proyecto.

## 2. Qué se crea

```json
{
  "tipo": "rol",
  "nombre": "SR - MPPP - Auditor",
  "descripcion": "Auditor: lee todo el modelo de datos de MPPP, sin crear, modificar, borrar ni accionar nada.",
  "base": "App Opener",
  "tablas": {
    "sanic_mppp_tbl_cliente": {
      "leer": "organizacion"
    },
    "sanic_mppp_tbl_plan": {
      "leer": "organizacion"
    },
    "sanic_mppp_tbl_autorizado": {
      "leer": "organizacion"
    },
    "sanic_mppp_tbl_autorizacionplan": {
      "leer": "organizacion"
    },
    "sanic_mppp_tbl_autorizacioncliente": {
      "leer": "organizacion"
    },
    "sanic_mppp_tbl_regla": {
      "leer": "organizacion"
    },
    "sanic_mppp_tbl_parametro": {
      "leer": "organizacion"
    },
    "sanic_mppp_tbl_solicitud": {
      "leer": "organizacion"
    },
    "sanic_mppp_tbl_fila": {
      "leer": "organizacion"
    },
    "sanic_mppp_tbl_resultadoregla": {
      "leer": "organizacion"
    },
    "sanic_mppp_tbl_bitacora": {
      "leer": "organizacion"
    }
  },
  "otros_privilegios": {}
}
```

Un security role en la unidad de negocio raíz, con los privilegios del rol `App Opener` (los que tenga el entorno en el momento de construir) más los 11 de este playbook: **Read en alcance Global sobre 11 tablas, y nada más**. Alcances: `usuario` = Basic (solo lo propio) · `unidad` = Local · `unidad_e_hijas` = Deep · `organizacion` = Global.

Decisiones que este playbook toma y el diseño no traía:

| Decisión | Valor | Por qué |
|---|---|---|
| Descripción del rol | «Auditor: lee todo el modelo de datos de MPPP, sin crear, modificar, borrar ni accionar nada.» | DN-09 §A trae el nombre y el alcance; el texto lo fija el playbook |
| `otros_privilegios` | vacío | Lo que el Auditor necesita fuera de las tablas de la solución (abrir la app, leer `systemuser` para "digitó" y "aprobó", leer web resources) ya lo trae `App Opener`: `muestras/sr_mppp_supervisor.solucion.xml` contiene `prvReadUser`, `prvReadWebResource`, `prvReadAppModule`, `prvReadSystemForm`, `prvReadQuery`, `prvReadRole`, `prvReadTeam`, `prvReadBusinessUnit` y demás. Si una actualización de Microsoft los quitara de `App Opener`, el rol dejaría de abrir la app; eso lo detecta la verificación 2 de la sección 5 y se corrige con `--completar` |
| Sin `prvReadappnotification` | no se declara | Los avisos de la solución (D-65) van a Ejecutivo y Supervisor; el Auditor no recibe ninguno, y el privilegio solo existe para que el servidor pueda crear el aviso a nombre del destinatario |

Viene del diseño, no lo decide este playbook:

- Alcance y lista de tablas: `DIRECTRICES-NUEVAS.md` DN-09 §A y decisión 3 (el Auditor ve **toda** la data): Solicitud, Fila, Bitácora, Resultado de regla, Regla, Parámetro, Cliente, Plan, Autorizado, Autorización por plan y Autorización por cliente.
- **Ningún** Create, Write, Delete, Append, AppendTo, Assign ni Share: ni sobre estas tablas ni sobre ninguna otra (`motivoaccion` tampoco: el diálogo de motivo es de los roles que accionan).
- No es Ejecutivo, Supervisor ni RPA: el plugin no le autoriza ninguna transición, y aunque quisiera no tiene `prvWrite`.
- Se parte de "App Opener", no de "Basic User" (`04` §2).
- Los números de cuenta y de identificación completos (decisión 2) **no** los resuelve este rol: están bajo el perfil de columna `CSP - MPPP - Datos sensibles` (inventario 6.1), cuyos miembros son equipos (tarea de administración 13.7). Ver sección 8.

## 3. Precondiciones

Las de la receta (`seguridad/patrones.md` §2.1), con estos valores esperados:

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es managed, y su publisher coincide | los de la sección 1 |
| 2 | Hay exactamente una unidad de negocio raíz | una |
| 3 | El rol base existe una vez en la unidad raíz | `App Opener` |
| 4 | Cada tabla nombrada existe, y cada privilegio admite el alcance pedido | `sanic_mppp_tbl_cliente` · `sanic_mppp_tbl_plan` · `sanic_mppp_tbl_autorizado` · `sanic_mppp_tbl_autorizacionplan` · `sanic_mppp_tbl_autorizacioncliente` · `sanic_mppp_tbl_regla` · `sanic_mppp_tbl_parametro` · `sanic_mppp_tbl_solicitud` · `sanic_mppp_tbl_fila` · `sanic_mppp_tbl_resultadoregla` · `sanic_mppp_tbl_bitacora` |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, `references/seguridad/patrones.md` §2.1 "Security role". Herramienta del proyecto: `herramientas/construir/rol.py`.

```
python3 herramientas/construir/rol.py playbooks/rol/sr_mppp_auditor.md
```

## 5. Verificación

```
python3 herramientas/construir/rol.py playbooks/rol/sr_mppp_auditor.md --solo-verificar
python3 herramientas/construir/muestra_rol.py playbooks/rol/sr_mppp_auditor.md --guardar
```

Privilegios sobre las tablas de la solución. **Exactamente estos**: uno de menos, uno de más u otro alcance es `difiere`.

| Tabla | Privilegios esperados |
|---|---|
| `sanic_mppp_tbl_cliente` | `prvReadsanic_mppp_tbl_cliente` = Global |
| `sanic_mppp_tbl_plan` | `prvReadsanic_mppp_tbl_plan` = Global |
| `sanic_mppp_tbl_autorizado` | `prvReadsanic_mppp_tbl_autorizado` = Global |
| `sanic_mppp_tbl_autorizacionplan` | `prvReadsanic_mppp_tbl_autorizacionplan` = Global |
| `sanic_mppp_tbl_autorizacioncliente` | `prvReadsanic_mppp_tbl_autorizacioncliente` = Global |
| `sanic_mppp_tbl_regla` | `prvReadsanic_mppp_tbl_regla` = Global |
| `sanic_mppp_tbl_parametro` | `prvReadsanic_mppp_tbl_parametro` = Global |
| `sanic_mppp_tbl_solicitud` | `prvReadsanic_mppp_tbl_solicitud` = Global |
| `sanic_mppp_tbl_fila` | `prvReadsanic_mppp_tbl_fila` = Global |
| `sanic_mppp_tbl_resultadoregla` | `prvReadsanic_mppp_tbl_resultadoregla` = Global |
| `sanic_mppp_tbl_bitacora` | `prvReadsanic_mppp_tbl_bitacora` = Global |

Otros privilegios: **ninguno** más que los de `App Opener`.

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET roles` por nombre en la unidad raíz | un rol · `ismanaged = false` · la descripción de la sección 2 |
| 2 | `RetrieveRolePrivilegesRole` | los privilegios de `App Opener` más los 11 `Read` de arriba; **ninguno más**, y en particular ningún `Create`, `Write`, `Delete`, `Append`, `AppendTo`, `Assign` ni `Share` fuera de los que traiga `App Opener` |
| 3 | Pertenencia a la solución | el rol figura una vez en la solución de la sección 1 (`solutioncomponents`, tipo 20) |
| 4 | Segunda ejecución sin `--solo-verificar` | estado `ya_existia`, código de salida 0 |
| 5 | Comprobación independiente contra el XML exportado (`muestra_rol.py`) | `OK`, y la muestra queda en `playbooks/rol/muestras/` |

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5. Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, **sin tocar nada**. La herramienta nunca quita un privilegio ni cambia un alcance. Si lo único que pasa es que **faltan** privilegios (una corrida cortada, o una actualización de Microsoft que amplió `App Opener`), se completa con `--completar`; eso lo decide quien dirige la construcción, no el constructor.

## 7. Reversa

`DELETE roles(<roleid>)`. No es posible mientras el rol esté asignado a un usuario o a un equipo. Nada es irreversible: el nombre, la descripción y los privilegios se pueden cambiar después; pero **cambiar un rol ya asignado cambia lo que esas personas pueden hacer en el momento**.

## 8. Fuera de alcance

- Asignar el rol a las personas o a un equipo: tarea de administración en cada entorno, inventario 13.
- Agregar el rol a la app model-driven: va con la app (`playbooks/app/mantenimientoppp.md`, lista `roles`).
- **Los números de cuenta e identificación completos** (DN-09, decisión 2): el rol da `Read` sobre Fila, pero las dos columnas siguen protegidas por `CSP - MPPP - Datos sensibles` (solo lectura, inventario 6.1). Quien tiene solo el rol Auditor las ve enmascaradas hasta que el **equipo** del Auditor sea miembro del perfil (tarea de administración 13.7). Es una pendiente de administración, no de este playbook.
- La asignación de los formularios "Auditoria" a este rol: la hacen los playbooks de formulario (`playbooks/formulario/solicitud_auditoria.md` y `fila_auditoria.md`); necesitan que este rol exista antes.
