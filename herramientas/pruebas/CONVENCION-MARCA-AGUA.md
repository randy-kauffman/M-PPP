# Convención: marca de agua de los datos de prueba

## Por qué existe

Un `preparar_datos.py --limpiar` borró alguna vez el fixture **compartido**
completo, incluidas autorizaciones reales del aprobador que hubo que
restaurar a mano. El limpiador borraba por parecido de nombre, sin verificar
antes de cada `DELETE` que el registro fuera, sin ninguna duda, algo que el
propio arnés había creado.

## La regla

1. **Todo lo que crea el arnés nace con el prefijo reservado `ZZP-`** (ZZ
   Prueba) al arranque de su campo identificador de texto (típicamente
   `sanic_nombre`). Es la única marca que un limpiador puede confiar.
2. **El limpiador SOLO puede borrar una fila si ese campo lleva el prefijo**,
   releído **justo antes** de cada `DELETE` — nunca contra una lista armada
   de antes, que pudo quedar vieja.
3. **No existe una bandera de forzado.** Un registro sin la marca no se
   borra desde el arnés, nunca. Si hace falta borrarlo, es una decisión
   humana y manual, fuera de esta herramienta.
4. El prefijo `ZZP-` **no entra en campos cortos** (códigos de negocio de
   3-4 caracteres): ahí la marca va en el campo de nombre largo, no en el
   código.

## Dónde vive

- `herramientas/pruebas/marca_agua.py` — `PREFIJO`, `marcado()`,
  `nombre_de_prueba()` y `borrar_marcado()` (el borrado seguro, registro por
  registro, sin bandera de forzado). Única fuente de verdad: no dupliques el
  literal `"ZZP-"` en otro archivo.
- `herramientas/pruebas/semaforo.py` — el semáforo que evita que el arnés
  automático corra mientras el aprobador prueba a mano sobre el mismo
  entorno (ver su docstring).

## Pendiente: `preparar_datos.py`

`preparar_datos.py` todavía usa el prefijo `PR` (no `ZZP-`) y su `limpiar()`
borra por coincidencia de nombre, sin releer ni verificar el registro antes
del `DELETE`. **No se tocó** su comportamiento destructivo sin autorización
del aprobador — ver el reporte de la tarea para el cambio exacto propuesto.
