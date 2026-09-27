# Playbook: tabla · sanic_mppp_tbl_autorizacioncliente

## 1. Identidad

```json
{
  "tipo_playbook": "tabla",
  "version_skill": "0.1.0",
  "proyecto": "2026-001-referencias-planes-pago",
  "inventario": "3.13",
  "fase": 2,
  "entorno_url": "https://org36e60d9d.crm.dynamics.com/",
  "solucion": "sanic_mppp_sol_mantenimientoppp",
  "publisher": "Sistemas_Abiertos_Nicaragua",
  "prefijo": "sanic",
  "abrev": "mppp",
  "prefijo_opciones": 15946,
  "lcid": 1033
}
```

De dónde sale cada dato: `diseno/01-convenciones.md` §0 y §7, verificados contra Dev el 2026-09-20. La tabla nace de DN-01 (`diseno/DIRECTRICES-NUEVAS.md`, "Diseño resuelto · DN-01 · Modelo", confirmado por el aprobador el 2026-09-26) y figura en `diseno/06-inventario-componentes.md` §3, renglón 3.13.

Advertencias: en Dev existe otro publisher con el mismo prefijo de texto, "Sanic Corp", con otro unique name; no es el de este proyecto. El idioma base del entorno es inglés y es el único provisionado: las etiquetas van en español bajo ese LCID.

## 2. Qué se crea

```json
{
  "tipo": "tabla",
  "nombre": "sanic_mppp_tbl_autorizacioncliente",
  "displayname": "Autorizacion por cliente",
  "displayname_plural": "Autorizaciones por cliente",
  "descripcion": "Autorización de un correo para operar sobre TODOS los planes de un cliente (DN-01). Convive con AutorizacionPlan, la vía por plan: ninguna reemplaza a la otra. El parámetro autorizacion.modo decide cuál vía consulta AUTORIZACION_CORREO_PLAN.",
  "propiedad": "usuario",
  "notas": false,
  "actividades": false,
  "auditoria": true,
  "primaria": {
    "nombre": "sanic_nombre",
    "displayname": "Nombre",
    "descripcion": "Nombre calculado por la solución: correo → cliente. Nadie lo digita.",
    "largo": 400,
    "requerida": true,
    "autonumerico": ""
  },
  "columnas": []
}
```

Decisiones que este playbook toma y el diseño no traía:

| Decisión | Valor | Por qué |
|---|---|---|
| Nombre visible de la tabla | `Autorizacion por cliente` / `Autorizaciones por cliente` | `AutorizacionPlan` ya ocupa «Autorizacion»/«Autorizaciones» en la app (`05` §4); el nombre lógico sigue siendo `autorizacioncliente` y el texto de negocio lo fija el playbook (`02` DD-19) |
| Nombre visible y descripción de la primaria | `Nombre` — «Nombre calculado por la solución: correo → cliente. Nadie lo digita.» | Mismo patrón que `sanic_mppp_tbl_autorizacionplan` (DIRECTRICES-NUEVAS.md, DN-01: "Primaria: nombre calculado `<correo> → <cliente>`, T(400), como en AutorizacionPlan") |
| La primaria es requerida, aunque sea calculada | `requerida: true` | Mismo razonamiento que `AutorizacionPlan`: la calcula la solución y nadie la digita (`01` §4); en el formulario va en solo lectura u oculta (BP-PP-192), y eso no bloquea el guardado (Learn, *Troubleshoot form issues*) |
| Sin columnas propias además de la primaria | `"columnas": []` | DN-01 ("Diseño resuelto") describe la forma de esta tabla en cuatro renglones — Lookups, Clave alternativa, Primaria, Privilegios — y **no incluye** evidencia firmada ni fecha de documento, a diferencia de `AutorizacionPlan` (`02` §2.4). Ver nota de alcance abajo |

Viene del diseño, no lo decide este playbook: **auditoría nativa, notas y actividades** = activada · no · no (`02` DD-18: es un catálogo, mismo criterio que `AutorizacionPlan`).

**Nota para el aprobador (no se resuelve acá):** `AutorizacionPlan` exige un documento firmado como evidencia de esa autorización puntual (`02` §2.4, RF-11, D-08). El texto de DN-01 "Diseño resuelto" para `AutorizacionCliente` no menciona evidencia ni fecha de documento, y esta tabla se construye tal cual esa especificación: **sin `sanic_documentofirmado` ni `sanic_fechadocumento`**. Eso significa que autorizar por cliente no deja el mismo rastro de respaldo firmado que autorizar por plan. Si eso es intencional o un hueco de la directriz, lo decide el aprobador; no se agregó por cuenta propia.

## 3. Precondiciones

Las de la receta (`patrones.md` §2.2), con estos valores esperados:

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es managed, y su publisher coincide en unique name, prefijo y prefijo de opciones | los de la sección 1 |
| 2 | El `lcid` es el idioma base y está provisionado | `1033` en los dos |
| 3 | Cada choice global que usa una columna existe | esta tabla no usa ninguno |
| 4 | Todo lo que usa el playbook está verificado contra la plataforma | tipo texto; auditoría de tabla: verificados en el ensayo del 2026-09-20 (mismo patrón que `sanic_mppp_tbl_autorizacionplan`) |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, `references/modelo-datos/patrones.md` §2.2 "Tabla con sus columnas". Herramienta del proyecto: `herramientas/construir/tabla.py`.

```
python3 herramientas/construir/tabla.py playbooks/tabla/sanic_mppp_tbl_autorizacioncliente.md
```

## 5. Verificación

```
python3 herramientas/construir/tabla.py playbooks/tabla/sanic_mppp_tbl_autorizacioncliente.md --solo-verificar
python3 herramientas/construir/muestra_tabla.py playbooks/tabla/sanic_mppp_tbl_autorizacioncliente.md --guardar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET EntityDefinitions(LogicalName='sanic_mppp_tbl_autorizacioncliente')` | 200 · `OwnershipType = UserOwned` · `IsManaged = false` · `IsCustomEntity = true` · `HasNotes = false` · `HasActivities = false` · `IsAuditEnabled = true` · `PrimaryNameAttribute = sanic_nombre` |
| 2 | Nombre, plural y descripción en 1033 | los de la sección 2; ninguna etiqueta en otro idioma |
| 3 | Columnas (1, solo la primaria): tipo, requerida, protegida, auditoría, nombre visible, descripción | `sanic_nombre` texto 400 requerida, auditada |
| 4 | Ninguna columna propia de más | salvo lookups, que nacen con su relación |
| 5 | Pertenece a la solución de la sección 1 | una fila en `solutioncomponents` (tipo de componente 1) |
| 6 | Segunda ejecución sin `--solo-verificar` | estado `ya_existia`, código de salida 0 |
| 7 | Comprobación independiente contra el XML exportado (`muestra_tabla.py`) | `OK`, y la muestra queda en `playbooks/tabla/muestras/` |

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5. Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, **sin tocar nada**.

## 7. Reversa

`DELETE EntityDefinitions(LogicalName='sanic_mppp_tbl_autorizacioncliente')`, posible mientras la tabla esté vacía y nada la referencie. **Irreversible desde que se crea**: el nombre lógico de la tabla, el tipo de propiedad (`usuario`) y el tipo de la primaria. El largo se puede ampliar después; achicarlo, no.

## 8. Fuera de alcance

- Los lookups `sanic_autorizadoid` y `sanic_clienteid` y sus relaciones: inventario 4.12 y 4.13.
- La clave alternativa `sanic_mppp_key_autorizacioncliente_autorizado_cliente`: inventario 5.11, después de las relaciones.
- El cálculo de `sanic_nombre` y cualquier regla de integridad: plugins, fuera de alcance de este cambio (punto 4 solo cubre `Datos/Catalogos.cs` y `Api/ValidarSolicitud.cs`; no hay step de nombre calculado para esta tabla todavía — la primaria queda sin calcular hasta que se escriba ese step).
- Vistas, formulario y seguridad de la tabla: inventario 12 y 6 (privilegios: `diseno/04-matriz-privilegios.md` §2, ya actualizada por este cambio).
- La app para cargar y ver estas autorizaciones (RF-11, DN-01 punto 6): **fuera de alcance de este cambio**, según la orden recibida.
