# Playbook: clave alternativa · sanic_mppp_key_autorizacioncliente_autorizado_cliente

## 1. Identidad

```json
{
  "tipo_playbook": "clave",
  "version_skill": "0.1.0",
  "proyecto": "2026-001-referencias-planes-pago",
  "inventario": "5.11",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 y §7, verificados contra Dev el 2026-09-20. El nombre de esta clave viene **confirmado por el aprobador el 2026-09-26** (`diseno/DIRECTRICES-NUEVAS.md`, alcance de este cambio) y es **irreversible**: no se modifica. Figura en `diseno/06-inventario-componentes.md` §5, renglón 5.11.

Advertencias: en Dev existe otro publisher con el mismo prefijo de texto, "Sanic Corp", con otro unique name; no es el de este proyecto. El idioma base del entorno es inglés y es el único provisionado: las etiquetas van en español bajo ese LCID.

## 2. Qué se crea

```json
{
  "tipo": "clave",
  "nombre": "sanic_mppp_key_autorizacioncliente_autorizado_cliente",
  "displayname": "KEY - MPPP - Autorizacion por cliente - Autorizado y cliente",
  "tabla": "sanic_mppp_tbl_autorizacioncliente",
  "columnas": [
    "sanic_autorizadoid",
    "sanic_clienteid"
  ]
}
```

Una clave alternativa sobre `sanic_mppp_tbl_autorizacioncliente`: la plataforma no admite dos registros con la misma combinación de `sanic_autorizadoid` · `sanic_clienteid` — el mismo correo no se autoriza dos veces sobre el mismo cliente.

Valores vacíos:

Las dos columnas de la clave son requeridas por sus relaciones (inventario 4.12 y 4.13): ningún registro creado desde la app queda fuera de la unicidad. (El nivel requerido lo aplica la app, no el servicio: lo que entra por API con una columna vacía queda fuera de la unicidad.)

Decisiones que este playbook toma y el diseño no traía:

| Decisión | Valor | Por qué |
|---|---|---|
| Nombre visible de la clave | «KEY - MPPP - Autorizacion por cliente - Autorizado y cliente» | El nombre lógico viene confirmado por el aprobador (irreversible); el visible sigue el patrón `KEY - MPPP - Tabla - Campos` (`01` §2), con «Autorizacion por cliente» como nombre de tabla, igual que el `displayname` elegido en el playbook de la tabla |

Viene del diseño, no lo decide este playbook:

- Nombre lógico de la clave: confirmado por el aprobador, irreversible.
- Columnas de la clave: `sanic_autorizadoid` + `sanic_clienteid` (DN-01, "Diseño resuelto": «Clave alternativa: autorizado + cliente (no se repite el par)»).

## 3. Precondiciones

Las de la receta (`patrones.md` §2.4), con estos valores esperados:

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es managed, y su publisher coincide en unique name, prefijo y prefijo de opciones | los de la sección 1 |
| 2 | El `lcid` es el idioma base y está provisionado | `1033` en los dos |
| 3 | La tabla existe | `sanic_mppp_tbl_autorizacioncliente` (inventario 3.13, construida) |
| 4 | Cada columna de la clave existe, es de un tipo que admite clave y no tiene seguridad de columna | `sanic_autorizadoid` (inventario 4.12) · `sanic_clienteid` (inventario 4.13) |
| 5 | La clave no pasa de 900 bytes (un texto ocupa 2 por carácter) | dos lookups (16 bytes cada uno en Dataverse); la herramienta lo calcula con los largos reales del entorno |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, `references/modelo-datos/patrones.md` §2.4 "Clave alternativa". Herramienta del proyecto: `herramientas/construir/clave.py`. El índice de la clave se arma en segundo plano y tarda unos dos minutos aun con la tabla vacía: la herramienta espera hasta verlo `Active`.

```
python3 herramientas/construir/clave.py playbooks/clave/sanic_mppp_key_autorizacioncliente_autorizado_cliente.md
```

## 5. Verificación

```
python3 herramientas/construir/clave.py playbooks/clave/sanic_mppp_key_autorizacioncliente_autorizado_cliente.md --solo-verificar
python3 herramientas/construir/muestra_clave.py playbooks/clave/sanic_mppp_key_autorizacioncliente_autorizado_cliente.md --guardar
python3 herramientas/construir/tabla.py playbooks/tabla/sanic_mppp_tbl_autorizacioncliente.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET EntityDefinitions(LogicalName='sanic_mppp_tbl_autorizacioncliente')/Keys(LogicalName='sanic_mppp_key_autorizacioncliente_autorizado_cliente')` | 200 · `IsManaged = false` · nombre visible «KEY - MPPP - Autorizacion por cliente - Autorizado y cliente» en 1033, sin etiquetas en otro idioma |
| 2 | Columnas de la clave (`KeyAttributes`, sin importar el orden) | `sanic_autorizadoid` · `sanic_clienteid` |
| 3 | Índice (`EntityKeyIndexStatus`) | `Active` |
| 4 | Pertenencia a la solución | la de la tabla: `sanic_mppp_tbl_autorizacioncliente` está una vez en la solución de la sección 1, con todos sus subcomponentes (una clave no es un componente propio) |
| 5 | Segunda ejecución sin `--solo-verificar` | estado `ya_existia`, código de salida 0 |
| 6 | La tabla sigue coincidiendo con su propio playbook | `ya_existia`: una clave no la hace diferir |
| 7 | Comprobación independiente contra el XML exportado (`muestra_clave.py`) | `OK`, y la muestra queda en `playbooks/clave/muestras/` |

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5. Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, **sin tocar nada**. Un índice `Failed` también es `difiere`: casi siempre hay datos duplicados, y eso lo resuelve una persona antes de reactivar la clave.

## 7. Reversa

`DELETE EntityDefinitions(LogicalName='sanic_mppp_tbl_autorizacioncliente')/Keys(<MetadataId>)`. No borra datos ni columnas: solo deja de exigir la unicidad. **Irreversible desde que se crea**: el nombre de la clave. Las columnas de una clave no se editan: para cambiarlas se borra la clave y se crea otra. Mientras la clave exista, sus columnas no se pueden borrar.

## 8. Fuera de alcance

- Las relaciones que crean los lookups de esta clave: inventario 4.12 y 4.13, ya construidas antes que esta clave.
- Cualquier plugin o Custom API que use esta clave para detectar duplicados: fuera de alcance de este cambio.
- Importación de datos por clave (semillas): fuera de alcance de este cambio (no hay datos semilla de `AutorizacionCliente` en este pedido).
