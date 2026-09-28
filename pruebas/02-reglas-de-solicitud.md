# 02 — Reglas de nivel Solicitud

Set **S** (`CONVENCIONES.md` §4). Las cinco reglas que miran **el sobre**, no el contenido: si hay
adjunto, si es Excel, si es uno solo, si tiene la estructura de la plantilla y si trae filas.

Cadena de dependencias (`sanic_dependede`):

```
TRAE_ADJUNTO → ADJUNTO_ES_EXCEL → UN_SOLO_EXCEL → ESTRUCTURA_PLANTILLA → TIENE_FILAS
```

Todas con efecto **Rechaza**. Que fallen deja la Solicitud en **Rechazada**, con acuse.

**Lo que más importa de este archivo**: que una regla cuya dependencia falló quede **Omitida** y no
"No cumplida", y que su razón **diga qué regla la bloqueó** (DD-13). Un sistema que dice "falló la
estructura de la plantilla" cuando en realidad no había adjunto le miente al ejecutivo.

**Set autocontenido** (`CONVENCIONES.md` §8). Este archivo no depende de ningún otro: prepara sus
propios datos, no usa ninguna sesión de navegador, y deja el canon de configuración global al
terminar. La preparación exacta que hace, y por qué **no** hace otras cosas que un set podría
necesitar (roles, flujos), está documentada en `herramientas/pruebas/sets/set_s.py::preparar()` —
acá solo el resumen:

| Qué prepara | Cómo |
|---|---|
| Datos de negocio (`PR*`, fixture compartido) | `preparacion_comun.verificar_datos_negocio()` (antiguo P-03) |
| Los 15 Excel + el adjunto no-Excel | `preparacion_comun.verificar_excels()` (antiguo P-04) |
| `plantilla.estructura` con la forma esperada | `preparacion_comun.verificar_estructura_plantilla()` (antiguo P-06, parcial) |
| `lectura.limites`, CONTENIDO (no solo que exista): las dos claves de tope de bytes | `preparacion_comun.verificar_lectura_limites()` — recupera la cobertura del antiguo P-06 (2026-09-28) |
| `plantilla.listas`, CONTENIDO — banco BAC = código `007` (no solo que exista) | `preparacion_comun.verificar_bac_codigo_007()` — recupera la cobertura del antiguo P-06 (2026-09-28); el mismo chequeo corre también en el set **R**, que es quien de verdad construye y compara la referencia (caso R-10) |

**Corré este set con**:

```bash
python3 herramientas/pruebas/correr.py S
```

---

## S-01 · El adjunto no es un Excel

**Qué prueba.** `ADJUNTO_ES_EXCEL` (D-15): que un adjunto que no es `.xlsx` rechace la Solicitud sin
llegar a abrir nada.

**Necesita.** El `REMITENTE` de este set (autorizado sobre `PR11`, ya preparado por `preparar()`); el
adjunto `A01-no-es-excel.txt`.

**Cómo.** Correo nuevo, asunto `Prueba S-01`, adjuntando **solo** `A01-no-es-excel.txt`.

**Qué tiene que pasar.**

| Qué | Valor |
|---|---|
| adjuntos / excel | `1 / 0` |
| `TRAE_ADJUNTO` | Cumplida |
| `ADJUNTO_ES_EXCEL` | **NO CUMPLIDA** |
| `UN_SOLO_EXCEL`, `ESTRUCTURA_PLANTILLA`, `TIENE_FILAS` | **Omitida** |
| Estado | **Rechazada** |
| Acuse | presente |

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py S` (caso `S-01`); a mano,
`ver_solicitud.py <MPPP-########>` sobre la Solicitud que crea el caso.

**Cómo se sabe que falló.** Dos causas con síntomas distintos: (a) si `ADJUNTO_ES_EXCEL` da
**Cumplida** o el Estado no es Rechazada, la condición `sanic_cantidadexcel ≥ 1` del evaluador está
mal — defecto de la regla misma; (b) si `ADJUNTO_ES_EXCEL` da **No cumplida** pero alguna de las tres
reglas dependientes NO da Omitida, el motor de dependencias (DD-13) está evaluando reglas que
debería saltear — defecto de orquestación, no de la regla puntual.

**Automatizable.** API (`correr.py S`, caso `S-01`).

---

## S-02 · Dos Excel en el mismo correo

**Qué prueba.** `UN_SOLO_EXCEL`. No se adivina cuál procesar.

**Necesita.** El `REMITENTE` de este set; los adjuntos `E01-feliz-formato11.xlsx` y
`E13-formato06-referencia-libre.xlsx` (los usa como simulación de "dos Excel", no se abre ninguno:
ver más abajo).

**Cómo.** Correo nuevo adjuntando **`E01-feliz-formato11.xlsx` y `E13-formato06-referencia-libre.xlsx`**.

**Qué tiene que pasar.**

| Qué | Valor |
|---|---|
| adjuntos / excel | `2 / 2` |
| `UN_SOLO_EXCEL` | **NO CUMPLIDA** |
| `ESTRUCTURA_PLANTILLA`, `TIENE_FILAS` | **Omitida** |
| Estado | **Rechazada** |
| archivos subidos | **ninguno** — a propósito: si no sabemos cuál es el bueno, no elegimos |

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py S` (caso `S-02`).

**Cómo se sabe que falló.** Si `UN_SOLO_EXCEL` da Cumplida con `sanic_cantidadexcel = 2`, la
condición `== 1` del evaluador está mal (defecto de la regla). Si da No cumplida pero la Solicitud
terminó **En proceso** con filas, algo subió y procesó un Excel que no debía elegirse — más grave que
un simple defecto de regla: es la garantía de "no adivinar" la que se rompió.

**Automatizable.** API (`correr.py S`, caso `S-02`).

---

## S-03 · Un Excel y un archivo que no lo es

**Qué prueba.** Que un adjunto extra que no sea Excel **no** rompa nada: el Excel candidato sigue
siendo el único `.xlsx`.

**Necesita.** El `REMITENTE` de este set; los adjuntos `E01-feliz-formato11.xlsx` y
`A01-no-es-excel.txt`.

**Cómo.** Correo nuevo adjuntando **`E01-feliz-formato11.xlsx` y `A01-no-es-excel.txt`**.

**Qué tiene que pasar.**

| Qué | Valor |
|---|---|
| adjuntos / excel | `2 / 1` |
| Las 5 reglas de Solicitud | **Cumplida** |
| Estado | **En proceso**, 3 filas Validadas |

> Caso real y frecuente: el cliente manda la plantilla y además un PDF de respaldo.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py S` (caso `S-03`).

**Cómo se sabe que falló.** Si alguna de las 5 reglas da No cumplida con un Excel válido presente, el
evaluador está confundiendo "hay más de un adjunto" con "hay más de un Excel" (D-15 los distingue a
propósito). Si el Estado no llega a En proceso pero las 5 reglas dieron Cumplida, el problema está
después del nivel Solicitud (nivel Registro, fuera del alcance de este set — ver `03-reglas-de-registro.md`).

**Automatizable.** API (`correr.py S`, caso `S-03`).

---

## S-04 · La hoja de datos tiene otro nombre

**Qué prueba.** `ESTRUCTURA_PLANTILLA` contra `plantilla.estructura` (hoja `Datos`).

**Necesita.** El `REMITENTE` de este set; el adjunto `E12-hoja-renombrada.xlsx` (la hoja se llama
`Datos2`); `plantilla.estructura` verificado por `preparar()` con `hoja=Datos`.

**Cómo.** Correo nuevo con **`E12-hoja-renombrada.xlsx`**.

**Qué tiene que pasar.**

| Qué | Valor |
|---|---|
| `UN_SOLO_EXCEL` | Cumplida |
| `ESTRUCTURA_PLANTILLA` | **NO CUMPLIDA** |
| `TIENE_FILAS` | **Omitida** |
| Estado | **Rechazada** |

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py S` (caso `S-04`).

**Cómo se sabe que falló.** Acá hay que distinguir dos causas con el MISMO síntoma superficial
("no quedó Rechazada limpia"): (a) si el Estado queda **Rechazada** pero con un evento `Error` en la
Bitácora, es un defecto — una plantilla mal armada es un caso de negocio esperable, no una excepción
(`03-reglas-de-registro.md` §1, "Errores"); (b) si el Estado queda **Ingresada** (nunca llegó a
Rechazada), la transacción se revirtió por una excepción real no controlada — mirar
`sanic_reintentosvalidacion` y `MPPP-VIG` antes de asumir que es lo mismo que (a).

**Automatizable.** API (`correr.py S`, caso `S-04`).

---

## S-05 · Excel con la estructura correcta pero sin filas

**Qué prueba.** `TIENE_FILAS`.

**Necesita.** El `REMITENTE` de este set; el adjunto `E11-sin-filas.xlsx`.

**Cómo.** Correo nuevo con **`E11-sin-filas.xlsx`**.

**Qué tiene que pasar.**

| Qué | Valor |
|---|---|
| `ESTRUCTURA_PLANTILLA` | Cumplida |
| `TIENE_FILAS` | **NO CUMPLIDA** |
| Estado | **Rechazada** |
| filas t/v/r | `0 / 0 / 0` |
| Acuse | presente |

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py S` (caso `S-05`).

**Cómo se sabe que falló.** Si `TIENE_FILAS` da Cumplida con `sanic_filastotales = 0`, el lector de
filas (7.2) está contando algo que no es una fila de datos (una fila de encabezado repetida, una fila
vacía con formato). Si `TIENE_FILAS` da correctamente No cumplida pero no hay acuse, el defecto no es
de la regla sino de la comunicación de una Rechazada — mismo síntoma que S-01/S-02/S-04, causa
distinta (ver S-07).

**Automatizable.** API (`correr.py S`, caso `S-05`).

---

## S-06 · Volumen: 100 filas

**Qué prueba.** El presupuesto de tiempo (`03-reglas-de-registro.md` §1: objetivo **< 10 s**) y que
`cantidadFilas = 100` del parámetro se respete, midiendo la llamada a
`sanic_mppp_capi_validarsolicitud` sola (sin el resto del flujo, que este set no ejercita: es una
medida más estricta que la del flujo completo, no más floja).

**Necesita.** El `REMITENTE` de este set; el adjunto `E14-volumen-100.xlsx`.

**Cómo.** Correo nuevo con **`E14-volumen-100.xlsx`**.

**Qué tiene que pasar.**

| Qué | Valor |
|---|---|
| filas t/v/r | `100 / 100 / 0` |
| Tiempo de la llamada a `validarsolicitud` | **menos de 10 s** |

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py S` (caso `S-06`; imprime el tiempo
medido en el detalle del check si falla, y en la Bitácora real quedan además `Ingresada` y
`Validación terminada` para medir el presupuesto del flujo completo, GUI: restá las dos marcas).

**Cómo se sabe que falló.** Si `filastotales/validas/rechazadas` no da `100/100/0`, es un defecto de
conteo o de validación, no de rendimiento — mirar primero eso. Si los conteos están bien pero el
tiempo pasa de 10 s, **no es un FALLA de funcionalidad**: es un dato para la fase de rendimiento, se
anota con el número exacto (§7, "nombrar, no contar") y no se lo confunde con una regresión de la
lógica de reglas.

**Automatizable.** API (`correr.py S`, caso `S-06`; mide el mismo presupuesto de tiempo que pide este caso).

---

## S-07 · El acuse de una Rechazada dice lo que tiene que decir

**Qué prueba.** DD-09 y el texto obligatorio de DF-08, sobre el **contenido** que el sistema calculó
para el acuse (`sanic_acusecontenido`) — no que ese contenido haya salido de verdad por correo real
(eso es `Mail.Send`, fuera del alcance de este set: ver "Corrección al MD" más abajo).

**Necesita.** Una Solicitud Rechazada de este mismo set (la arma el propio caso, con
`cantidad_adjuntos=0`, el mismo camino que S-01 exercita con TRAE_ADJUNTO).

**Cómo.** Correo nuevo sin adjuntos, asunto `Prueba S-07`. Leé `sanic_acusecontenido` de la Solicitud
resultante.

**Qué tiene que pasar.** El contenido dice, con todas las letras:

1. El **número de solicitud** (`MPPP-########`).
2. Que **no hay nada que procesar**.
3. Que **no va a recibir otro correo** por esta solicitud.
4. Que **corrija y reenvíe**.
5. El texto obligatorio: *"Para enviar una plantilla nueva o corregida, escriba un correo nuevo a
   esta dirección. No responda a este mensaje: las respuestas no se procesan."*
6. **No** menciona el nombre técnico de ninguna de las 5 reglas de Solicitud.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py S` (caso `S-07`); a mano, `ver_solicitud.py
<MPPP-########>` y leer el campo `Acuse`.

**Cómo se sabe que falló.** Distinguí **qué** falta: si falta el punto 5 puntual, es el texto fijo el
que se rompió (probablemente un cambio de plantilla de mensaje que no pasó por este caso); si falta
el número de solicitud o aparece un nombre de regla técnico, el motivo es que el evaluador está
usando un mensaje de depuración en vez del mensaje al cliente (D-19). Ninguno de los dos prueba que
el correo **salió de verdad** de la casilla — eso, y que llegue "solo al remitente, no a quienes
estén en copia" y "como respuesta en el hilo", sigue siendo manual y depende de `Mail.Send`
(`04-comunicaciones.md`, fuera del alcance de este archivo).

**Automatizable.** API (`correr.py S`, caso `S-07`).

> **Corrección al MD (2026-09-27).** La versión anterior de este caso decía "Manual (exige leer un
> correo real)". Es un error de alcance, no de intención: `sanic_acusecontenido` es el **mismo
> texto** que `MPPP-ENV` manda tal cual, así que verificarlo por Web API prueba genuinamente el
> contenido (DD-09/DF-08) sin necesitar `Mail.Send`. Lo que sí sigue siendo manual — y no es este
> caso, es `04-comunicaciones.md` — es confirmar que ese texto **llegó** a una bandeja real, en el
> hilo correcto y sin copiar a nadie más. El código (`sets/set_s.py::s07`) ya hacía esto desde antes
> de esta reescritura; lo que estaba desactualizado era el MD, no el comportamiento.

---

## Fase 2 — investigación y conclusión

Se investigó si algo de `diseno/DIRECTRICES-NUEVAS.md` (DN-01, DN-02, DN-03) agrega un
comportamiento nuevo que se resuelva **a nivel Solicitud** (el nivel que cubre este set) y no a nivel
Correo o Registro (que cubren `01-clasificacion-del-correo.md` y `03-reglas-de-registro.md`). Se
evaluaron cuatro candidatos; **ninguno aplica**, y acá está la evidencia de por qué:

1. **Las 5 reglas de este set no cambian.** `playbooks/datos/reglas.md` (el catálogo sembrado)
   confirma que las 15 reglas del proyecto son **todas de fase 1** — no hay una fila nueva de nivel
   Solicitud fechada en fase 2. DN-01 lo dice explícitamente sobre su propia regla: *"La regla
   `AUTORIZACION_CORREO_PLAN` no cambia de código, de orden, de nivel ni de efecto"* (nivel
   **Registro**); DN-03 dice lo mismo de `OBLIGATORIEDAD` (nivel **Registro**). Ninguna de las dos
   toca el nivel Solicitud.

2. **DN-01 (autorización por cliente) resuelve en dos puntos, ninguno es Solicitud.**
   `diseno/DIRECTRICES-NUEVAS.md` ("Hay UN solo punto donde se consulta la autorización") lo deja
   trazado con archivo y línea: `Datos/Catalogos.cs:209 PlanesAutorizadosDe(correo)`, con **dos**
   llamadores — `ClasificarCorreo.cs:242` (`REMITENTE_RECONOCIDO`, nivel **Correo**, lo cubre
   `01-clasificacion-del-correo.md`) y `ValidarSolicitud.cs:246` (`AUTORIZACION_CORREO_PLAN`, nivel
   **Registro**, lo cubre `03-reglas-de-registro.md`). El propio diseño dice *"una función, dos
   llamadores, **ningún tercero**"* — no hay una tercera evaluación a nivel Solicitud que agregar acá.

3. **DN-02 (exportar a Excel para digitar) es de Fila (Registro), no de Solicitud.** Su Custom API
   (`sanic_mppp_capi_exportarparadigitar`) recibe `filaid`, no `solicitudid`; el parámetro nuevo
   (`exportacion.columnas`) describe columnas de **Fila**. No hay ninguna superficie de este
   candidato en el nivel que cubre este set.

4. **`sanic_requiererevision`/`sanic_motivorevision` (la "marca de revisión") y los demás estados de
   Solicitud (`No reconocida`, `Vencida`, `Cerrada`, etc.) SÍ viven en la tabla Solicitud, pero
   NO son trabajo nuevo de fase 2** (`diseno/PENDIENTES.md` D-52 los cierra el 2026-09-23, antes de
   que existieran las directrices nuevas) **y ya tienen dueño**: `sanic_requiererevision` lo prueban
   `04-comunicaciones.md` (M-06), `05-app-y-comandos.md` (A-12, el botón "Revisado") y
   `07-vigilancia.md` (V-03/V-04, el conteo de reintentos que la dispara); las transiciones
   `No reconocida`/`No es correo nuevo → Cerrada/Descartada` las prueba `05-app-y-comandos.md`
   (A-11). Agregar un caso acá **duplicaría** casos que ya existen con otro prefijo — y
   `CONVENCIONES.md` §4 fija un ID por archivo, sin colisiones: no le corresponde a la letra `S`.
   `sanic_notainterna`, además, ni siquiera es de Solicitud: es una columna de **Fila**
   (`diseno/02-diccionario-datos.md` §3.2), nivel Registro.

**Conclusión**: el set S queda con los mismos 7 casos de fase 1, sin agregados de fase 2. Si en el
futuro una directriz nueva agrega o cambia una regla de nivel Solicitud, este archivo es donde
corresponde sumarla — hoy no hay ninguna.

---

## Resultados

| Caso | Solicitud | Resultado | Notas |
|---|---|---|---|
| S-01 adjunto no Excel | | | |
| S-02 dos Excel | | | |
| S-03 Excel + otro archivo | | | |
| S-04 hoja renombrada | | | |
| S-05 sin filas | | | |
| S-06 volumen 100 | | | seg: ____ |
| S-07 texto del acuse | | | |
