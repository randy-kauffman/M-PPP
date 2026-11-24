# Playbook: tabla · sanic_mppp_tbl_plan

## 1. Identidad

```json
{
  "tipo_playbook": "tabla",
  "version_skill": "0.1.0",
  "proyecto": "2026-001-referencias-planes-pago",
  "inventario": "3.2",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 y §7, verificados contra Dev el 2026-09-20. La tabla está definida en `diseno/02-diccionario-datos.md` §2.2 y figura en `diseno/06-inventario-componentes.md` §3, renglón 3.2.

Advertencias: en Dev existe otro publisher con el mismo prefijo de texto, "Sanic Corp", con otro unique name; no es el de este proyecto. El idioma base del entorno es inglés y es el único provisionado: las etiquetas van en español bajo ese LCID.

## 2. Qué se crea

```json
{
  "tipo": "tabla",
  "nombre": "sanic_mppp_tbl_plan",
  "displayname": "Plan",
  "displayname_plural": "Planes",
  "descripcion": "Plan de pago de planilla o de proveedores de una empresa cliente, tal como existe en el AS400. Su código es único en todo el banco.",
  "propiedad": "usuario",
  "notas": false,
  "actividades": false,
  "auditoria": true,
  "primaria": {
    "nombre": "sanic_nombre",
    "displayname": "Nombre",
    "descripcion": "Nombre calculado por la solución: código del plan y nombre del cliente. Nadie lo digita.",
    "largo": 100,
    "requerida": true,
    "autonumerico": ""
  },
  "columnas": [
    {
      "nombre": "sanic_codigo",
      "displayname": "Codigo",
      "descripcion": "Código del plan en el AS400. Único en todo el banco. Alfanumérico, en mayúscula, rellenado con ceros a la izquierda hasta 4. Es clave alternativa.",
      "tipo": "texto",
      "largo": 4,
      "requerida": true,
      "protegida": false,
      "auditoria": true
    },
    {
      "nombre": "sanic_tipoformato",
      "displayname": "Tipo de formato",
      "descripcion": "Formato del plan en el AS400: 06, 10 u 11. Define cómo se arma la referencia.",
      "tipo": "choice",
      "choice": "sanic_mppp_ch_tipoformatoplan",
      "requerida": true,
      "protegida": false,
      "auditoria": true
    },
    {
      "nombre": "sanic_moneda",
      "displayname": "Moneda",
      "descripcion": "Moneda del plan. La moneda de la cuenta de cada referencia tiene que coincidir con ella.",
      "tipo": "choice",
      "choice": "sanic_mppp_ch_moneda",
      "requerida": true,
      "protegida": false,
      "auditoria": true
    },
    {
      "nombre": "sanic_canal",
      "displayname": "Canal",
      "descripcion": "Canal por el que el cliente opera el plan: Banca en Linea o Conectividad. Decide qué matriz de obligatoriedad se aplica a sus filas. Vacío se lee como Banca en Linea.",
      "tipo": "choice",
      "choice": "sanic_mppp_ch_canal",
      "requerida": true,
      "protegida": false,
      "auditoria": true
    },
    {
      "nombre": "sanic_requierepreregistro",
      "displayname": "Requiere pre-registro",
      "descripcion": "Si es No, el plan no admite pre-registro de referencias: toda fila de ese plan se rechaza con la regla PLAN_REQUIERE_PREREGISTRO. Vacío se lee como Si.",
      "tipo": "sino",
      "requerida": true,
      "protegida": false,
      "auditoria": true,
      "etiqueta_si": "Si",
      "etiqueta_no": "No",
      "defecto": true
    }
  ]
}
```

Decisiones que este playbook toma y el diccionario no traía:

| Decisión | Valor | Por qué |
|---|---|---|
| Nombres visibles de las columnas | `Nombre`, `Codigo`, `Tipo de formato`, `Moneda`, `Canal`, `Requiere pre-registro` | Texto de negocio (`02` DD-19) |
| `sanic_canal` sin valor por defecto; `sanic_requierepreregistro` por defecto Si (DN-06) | requerida las dos | DN-06 decisiones 1 y 3. Las filas que ya existen quedan vacías: las completa el backfill (`playbooks/datos/backfill_plan_dn06.md`) y, mientras tanto, el plugin las lee como Banca en Linea y Si |
| La primaria es requerida, como dice el diccionario, aunque sea calculada | `requerida: true` | `02` §2.2 la marca `S`. La calcula un plugin PreOperation y nadie la digita (`01` §4): en el formulario va **en solo lectura u oculta** (BP-PP-192), y una columna requerida en solo lectura u oculta **no bloquea el guardado** (Learn, *Troubleshoot form issues*: "Business required column doesn't block saving"). Eso se resuelve en el playbook del formulario, no bajando el nivel de requerida |
| Auditoría nativa, notas y actividades | activada · no · no | `02` DD-18: es un catálogo |

## 3. Precondiciones

Las de la receta (`patrones.md` §2.2), con estos valores esperados:

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es managed, y su publisher coincide en unique name, prefijo y prefijo de opciones | los de la sección 1 |
| 2 | El `lcid` es el idioma base y está provisionado | `1033` en los dos |
| 3 | Cada choice global que usa una columna existe | `sanic_mppp_ch_tipoformatoplan`, `sanic_mppp_ch_moneda` y `sanic_mppp_ch_canal` (inventario 2.4, 2.1 y DN-06, construidos) |
| 4 | Todo lo que usa el playbook está verificado contra la plataforma | texto, choice, sí/no, auditoría de tabla y de columna: verificados en el ensayo del 2026-09-20; agregar columnas a una tabla existente, en el del 2026-09-21 |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, `references/modelo-datos/patrones.md` §2.2 "Tabla con sus columnas". Herramienta del proyecto: `herramientas/construir/tabla.py`.

```
python3 herramientas/construir/tabla.py playbooks/tabla/sanic_mppp_tbl_plan.md
```

## 5. Verificación

```
python3 herramientas/construir/tabla.py playbooks/tabla/sanic_mppp_tbl_plan.md --solo-verificar
python3 herramientas/construir/muestra_tabla.py playbooks/tabla/sanic_mppp_tbl_plan.md --guardar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET EntityDefinitions(LogicalName='sanic_mppp_tbl_plan')` | 200 · `OwnershipType = UserOwned` · `IsManaged = false` · `IsCustomEntity = true` · `HasNotes = false` · `HasActivities = false` · `IsAuditEnabled = true` · `PrimaryNameAttribute = sanic_nombre` |
| 2 | Nombre, plural y descripción en 1033 | los de la sección 2; ninguna etiqueta en otro idioma |
| 3 | Columnas | `sanic_nombre` texto 100 requerida · `sanic_codigo` texto 4 requerida · `sanic_tipoformato` choice `sanic_mppp_ch_tipoformatoplan` requerida · `sanic_moneda` choice `sanic_mppp_ch_moneda` requerida · `sanic_canal` choice `sanic_mppp_ch_canal` requerida · `sanic_requierepreregistro` sí/no requerida, por defecto Si; las seis auditadas, ninguna protegida |
| 4 | Ninguna columna propia de más | salvo lookups, que nacen con su relación |
| 5 | Pertenece a la solución de la sección 1 | una fila en `solutioncomponents` (tipo de componente 1) |
| 6 | Segunda ejecución sin `--solo-verificar` | estado `ya_existia`, código de salida 0 |
| 7 | Comprobación independiente contra el XML exportado (`muestra_tabla.py`) | `OK`, y la muestra queda en `playbooks/tabla/muestras/` |

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5. Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, **sin tocar nada**.

## 7. Reversa

`DELETE EntityDefinitions(LogicalName='sanic_mppp_tbl_plan')`, posible mientras la tabla esté vacía y nada la referencie. **Irreversible desde que se crea**: el nombre lógico de la tabla y el de cada columna, el tipo de propiedad (`usuario`) y el tipo de cada columna, incluido a qué choice global apunta cada una. Los largos se pueden ampliar después; achicarlos, no. **`sanic_canal` y `sanic_requierepreregistro` (DN-06)**: se agregan con `tabla.py --agregar-columnas`; su reversa es `DELETE` de cada columna sola (`EntityDefinitions(...)/Attributes(LogicalName='...')`), que borra lo que tenga cargado.

## 8. Fuera de alcance

- El lookup `sanic_clienteid` y la relación cliente → plan: inventario 4.1.
- La clave alternativa `sanic_mppp_key_plan_codigo`: inventario 5.3.
- El cálculo de `sanic_nombre` y la normalización y validación del código: plugins PreOperation, inventario 7.11 y 7.10.
- Vistas, formulario y seguridad de la tabla: inventario 12 y 6.
