# Playbook: choice-global · sanic_mppp_ch_canal

## 1. Identidad

```json
{
  "tipo_playbook": "choice-global",
  "version_skill": "0.1.0",
  "proyecto": "2026-001-referencias-planes-pago",
  "inventario": "2.15",
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

De dónde sale cada dato: todos de `diseno/01-convenciones.md` §0 y §7, verificados contra Dev el 2026-09-20. El componente sale de **DN-06** (`diseno/DIRECTRICES-NUEVAS.md`, A. Esquema), aprobado el 2026-11-24. Después de construirlo en la solución principal se agrega al parche `sanic_mppp_sol_parche_1_3_1`.

Advertencias: en Dev existe otro publisher con el mismo prefijo de texto, "Sanic Corp", con otro unique name y prefijo de opciones 10000; no es el de este proyecto. El idioma base del entorno es inglés y es el único provisionado: las etiquetas van en español bajo ese LCID.

## 2. Qué se crea

```json
{
  "tipo": "choice-global",
  "nombre": "sanic_mppp_ch_canal",
  "displayname": "CH - MPPP - Canal",
  "descripcion": "Canal por el que el cliente de un plan opera los pagos. Decide qué matriz de obligatoriedad se aplica a las filas del plan. La usa la tabla Plan.",
  "opciones": [
    { "valor": 159460001, "etiqueta": "Banca en Linea", "descripcion": "El cliente paga por la banca en línea. Usa el parámetro plantilla.obligatoriedad." },
    { "valor": 159460002, "etiqueta": "Conectividad", "descripcion": "El cliente paga por conectividad. Usa el parámetro plantilla.obligatoriedad.conectividad." }
  ]
}
```

## 3. Precondiciones

Las de la receta (`patrones.md` §2.1), con estos valores esperados:

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es managed, y su publisher coincide en unique name, prefijo y prefijo de opciones | los de la sección 1 |
| 2 | Todos los valores están en el rango del publisher | `159460000 ≤ valor < 159470000` |
| 3 | El `lcid` es el idioma base y está provisionado | `1033` en los dos |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, `references/modelo-datos/patrones.md` §2.1 "Choice global", con el contrato de herramientas de su §1.

Herramienta del proyecto: `herramientas/construir/choice_global.py`.

```
python3 herramientas/construir/choice_global.py playbooks/choice-global/sanic_mppp_ch_canal.md
```

## 5. Verificación

```
python3 herramientas/construir/choice_global.py playbooks/choice-global/sanic_mppp_ch_canal.md --solo-verificar
```

Las comprobaciones de la receta, con estos valores esperados:

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET GlobalOptionSetDefinitions(Name='sanic_mppp_ch_canal')` | 200 · `IsGlobal = true` · `OptionSetType = Picklist` · `IsManaged = false` |
| 2 | Display name y descripción en 1033 | los de la sección 2 |
| 3 | Opciones, en orden, con valor, etiqueta y descripción | `159460001 = Banca en Linea`, `159460002 = Conectividad`; ninguna más; ninguna etiqueta en otro idioma |
| 4 | Pertenece a la solución de la sección 1 | exactamente una fila en `solutioncomponents` |
| 5 | Segunda ejecución de la herramienta sin `--solo-verificar` | estado `ya_existia`, código de salida 0, no cambia nada |

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5. Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, **sin tocar nada**.

## 7. Reversa

`DELETE GlobalOptionSetDefinitions(Name='sanic_mppp_ch_canal')`, posible solo mientras ninguna columna lo use. **Irreversible desde que tenga uso**: el nombre lógico `sanic_mppp_ch_canal`, y los valores `159460001` y `159460002` una vez que haya datos con ellos; nunca se renumeran ni se reutilizan.

## 8. Fuera de alcance

La columna `sanic_canal` del Plan que usa este choice: se agrega con el playbook de la tabla Plan (`tabla.py --agregar-columnas`). La etiqueta **Banca en Linea** va sin tilde a propósito (BP-PP-197: la herramienta rechaza tildes en las etiquetas de opciones, y `generar_enums.py` genera el enum a partir de ellas).
