# 03 — Reglas de nivel Registro

Set **R** (`CONVENCIONES.md` §4). Las ocho reglas que deciden **fila por fila**. Son el corazón del
sistema: de acá sale si una referencia entra a AS400 o no.

```
LISTAS_VALIDAS ──┬─→ FORMATO_11_SOLO_ACH ─┬─→ REFERENCIA_FORMATO_11
LARGOS_Y_FORMATO │                         │
PLAN_EXISTE ─────┼─→ OBLIGATORIEDAD ───────┘
                 ├─→ MONEDA_DEL_PLAN
                 └─→ AUTORIZACION_CORREO_PLAN
```

**Tres cosas que este archivo vigila especialmente:**

1. **El historial regla por regla es del sobre, no de la fila** (`03` §5). En `ResultadoRegla` ves
   Cumplida/No cumplida/Omitida del **correo**; en `sanic_mensaje` de cada fila ves **solo los
   motivos que fallaron en esa fila**.
2. **`Sin autorización` gana** sobre `Rechazada en validación`, aunque además falle otra regla
   (D-14), pero el mensaje lleva **todos** los motivos.
3. **Un valor inválido no se cita** en el mensaje (DD-01, D-41): se nombra el campo, no el valor.
   Los datos del cliente no se repiten en un texto que puede terminar en cualquier lado.

**Antes**: `00-preparacion.md`, con los planes `PR11`, `PR06`, `PR10`, `PRIN` y `PRNA`.

**Fase 2 (DN-01, DN-03): acá es donde de verdad pega.** `AUTORIZACION_CORREO_PLAN` y
`OBLIGATORIEDAD` — dos de las ocho reglas de este mismo diagrama — cambiaron de comportamiento sin
cambiar de código, orden, nivel ni efecto (`diseno/DIRECTRICES-NUEVAS.md`). R-15..R-18 cubren esas
dos reglas bajo el comportamiento nuevo, automatizadas por API con el mismo arnés que R-01..R-14; la
sección "Fase 2 — investigación y conclusión" explica exactamente qué agrega este archivo y qué
queda a cargo del set dedicado `10-autorizacion-cliente.md` (`U`), para no duplicar los mismos casos
con otro nombre. **R-19..R-28** son la cobertura exhaustiva de DN-03 (el ex set `O`,
`12-obligatoriedad-gestion.md`, fusionado acá el 2026-09-28 — `CONVENCIONES.md` §4, "La regla del
nombre"): ver la sección propia más abajo, justo después de R-18.

**Set autocontenido** (`CONVENCIONES.md` §8). Prepara sus propios datos (el fixture compartido `PR*`
más, para DN-01, un Autorizado y una `AutorizacionCliente` que `preparar_datos.py` **no** crea), no
usa ninguna sesión de navegador, y deja el canon de configuración global al terminar. El detalle
completo, y por qué, está en `herramientas/pruebas/sets/set_r.py::preparar()` — acá el resumen:

| Qué prepara | Cómo |
|---|---|
| Datos de negocio (`PR*`, fixture compartido, remitente autorizado por plan) | `preparacion_comun.verificar_datos_negocio()` |
| Los 15 Excel + el adjunto no-Excel | `preparacion_comun.verificar_excels()` |
| `plantilla.estructura` con la forma esperada | `preparacion_comun.verificar_estructura_plantilla()` |
| `plantilla.listas`, CONTENIDO — banco BAC = código `007` (no solo que exista) | `preparacion_comun.verificar_bac_codigo_007()` — recupera la cobertura del antiguo P-06 (2026-09-28): es el código con el que R-10 construye y compara la referencia de formato 11 |
| `plantilla.obligatoriedad` y `autorizacion.modo` activos (sin forzar valor: el canon de ambos es "la versión activa", DN-01/DN-03) | `preparacion_comun.verificar_parametro_activo()` |
| Un Autorizado + una `AutorizacionCliente` (borrados por GUID en `cerrar()`, no por `marca_agua`: su nombre lo calcula un step y lo pisa — ver R-16), para R-16/R-17 (DN-01) | Web API directa (`preparar_datos.py` no la crea) |

**Corré este set con**:

```bash
python3 herramientas/pruebas/correr.py R
```

---

## R-01 · Valores fuera de las listas

**Qué prueba.** `LISTAS_VALIDAS` contra `plantilla.listas`.

**Necesita.** El REMITENTE, autorizado sobre algún plan; el adjunto `E02-listas-invalidas.xlsx`.

**Cómo.** Correo nuevo con **`E02-listas-invalidas.xlsx`** (5 filas, una por campo).

**Esperado.** Las 5 filas en **Rechazada en validación**. Cada `sanic_mensaje` nombra **el campo**:

| Fila | Qué trae mal | Mensaje esperado (nombra el campo) |
|---|---|---|
| 1 | `gestion = Alta` | …no son válidos: **Gestión** |
| 2 | `moneda = EUR` | …no son válidos: **Moneda** |
| 3 | `banco = SANTANDER` | …no son válidos: **Banco** |
| 4 | `tipoIdentificacion = XXX` | …no son válidos: **Tipo de identificación** |
| 5 | `clasificacion = TRANSFER` | …no son válidos: **Clasificación** |

**FALLA si** el mensaje **cita el valor** (`"Alta no es válido"`, `"EUR"`, `"SANTANDER"`). Eso es
DD-01/D-41: se nombra el campo, nunca el dato del cliente.

| Qué | Valor |
|---|---|
| Estado de la Solicitud | **Rechazada** (ninguna fila quedó Validada) |
| filas t/v/r | `5 / 0 / 5` |

**Automatizable.** API (`corredor.py`, caso R-01).

---

## R-02 · Largos y formato

**Qué prueba.** `LARGOS_Y_FORMATO` contra los `largoMinimo`/`largoMaximo` de `plantilla.estructura`.

**Necesita.** El REMITENTE; el adjunto `E03-largos-invalidos.xlsx`.

**Cómo.** Correo nuevo con **`E03-largos-invalidos.xlsx`**.

**Esperado.** Las 4 filas en **Rechazada en validación**:

| Fila | Qué trae mal | Máximo |
|---|---|---|
| 1 | nombre de 64 caracteres | 44 |
| 2 | plan `PR11XX` (6) | 4 |
| 3 | cuenta de 21 | 16 |
| 4 | identificación de 22 | 16 |

> La fila 2 es interesante: el plan es **largo**, así que falla `LARGOS_Y_FORMATO`. Mirá qué pasa
> con `PLAN_EXISTE` en esa fila — el plan `PR11XX` tampoco existe. El mensaje puede traer los dos
> motivos, y está bien: `sanic_mensaje` lleva **todos** los que fallaron.

**Automatizable.** API (`corredor.py`, caso R-02).

---

## R-03 · Plan que no existe

**Qué prueba.** `PLAN_EXISTE`. **Nunca se adivina un plan parecido.**

**Necesita.** El REMITENTE; el adjunto `E04-plan-inexistente.xlsx`.

**Cómo.** Correo nuevo con **`E04-plan-inexistente.xlsx`** (plan `ZZ99`).

**Esperado.**

| Qué | Valor |
|---|---|
| La fila | **Rechazada en validación** |
| `sanic_planid` de la fila | **vacío** |
| Mensaje | que el plan no existe o no está activo |
| Estado | **Rechazada** |

**FALLA si** la fila quedó enganchada a `PR11` u otro plan "parecido". Adivinar un plan es mandar
plata al cliente equivocado.

**Automatizable.** API (`corredor.py`, caso R-03).

---

## R-04 · Plan que existe pero está inactivo

**Qué prueba.** Que `PLAN_EXISTE` exija **activo**, no solo que exista.

**Necesita.** El REMITENTE; el adjunto `E15-plan-inactivo.xlsx`; el plan `PRIN` ya desactivado
(P-03 lo desactiva después de crear su autorización, a propósito).

**Cómo.** Correo nuevo con **`E15-plan-inactivo.xlsx`** (plan `PRIN`, que `preparar_datos.py`
desactivó a propósito).

**Esperado.** Igual que R-03: fila **Rechazada en validación**, `sanic_planid` vacío.

> Este caso y R-03 se ven iguales desde afuera, y así tiene que ser. Lo que prueba es que el
> filtro de la consulta lleva el estado, no solo el código.

**Automatizable.** API (`corredor.py`, caso R-04).

---

## R-05 · Plan formato 11 con clasificación que no es ACH

**Qué prueba.** `FORMATO_11_SOLO_ACH` (DD-17).

**Necesita.** El REMITENTE; el adjunto `E05-formato11-no-ach.xlsx`; el plan `PR11`.

**Cómo.** Correo nuevo con **`E05-formato11-no-ach.xlsx`** (plan `PR11`, clasificaciones `BAC` y `CK`).

**Esperado.**

| Qué | Valor |
|---|---|
| Las 2 filas | **Rechazada en validación** |
| Mensaje | que un plan de formato 11 solo admite ACH |
| `REFERENCIA_FORMATO_11` en esas filas | no aplica: su dependencia falló |

**Automatizable.** API (`corredor.py`, caso R-05).

---

## R-06 · Campos obligatorios vacíos, y el opcional

**Qué prueba.** `OBLIGATORIEDAD` contra `plantilla.obligatoriedad` (todo obligatorio **salvo
`referencia`**, DD-16).

**Necesita.** El REMITENTE; el adjunto `E06-obligatoriedad.xlsx`.

**Cómo.** Correo nuevo con **`E06-obligatoriedad.xlsx`** (4 filas).

**Esperado.**

| Fila | Qué falta | Resultado |
|---|---|---|
| 1 | nombre del beneficiario | **Rechazada en validación** |
| 2 | número de cuenta | **Rechazada en validación** |
| 3 | tipo de identificación | **Rechazada en validación** |
| 4 | **solo la referencia** | **Validada** ← la referencia es opcional |

| Qué | Valor |
|---|---|
| filas t/v/r | `4 / 1 / 3` |
| Estado | **En proceso** (hay una válida) |

> **La fila 4 es el caso que más vale.** Si queda Rechazada, la obligatoriedad está mal leída y el
> sistema va a rechazar plantillas perfectamente correctas. Además, como es plan formato 11, su
> referencia se **construye** igual (no la trae el cliente): mirá que tenga sus 20 caracteres.

> **Nota DN-03 (2026-09-27).** Las 4 filas de `E06-obligatoriedad.xlsx` usan gestión **`Inclusion`**
> (el valor por defecto de `generar_excel.fila()`), que **no** tiene entrada en `porGestion` (solo
> `Exclusion` la tiene hoy, versión 2, confirmada por el aprobador el 2026-09-28 — ver R-18). Por eso este caso sigue probando
> exactamente "todo obligatorio salvo referencia" **sin importar si la versión activa de
> `plantilla.obligatoriedad` es la 1 o la 2**: es, de hecho, la evidencia de compatibilidad hacia
> atrás que pide DN-03 ("un parámetro sin `porGestion`, o una gestión sin entrada en `porGestion`, se
> comporta EXACTAMENTE como antes"). No hace falta un caso nuevo para "v1": este mismo caso, sin
> tocarlo, ya lo es.

**Automatizable.** API (`corredor.py`, caso R-06).

---

## R-07 · Moneda distinta de la del plan

**Qué prueba.** `MONEDA_DEL_PLAN` (RF-07).

**Necesita.** El REMITENTE; el adjunto `E07-moneda-distinta.xlsx`; el plan `PR10` (USD).

**Cómo.** Correo nuevo con **`E07-moneda-distinta.xlsx`** (plan `PR10` es **USD**, la fila trae **COR**).

**Esperado.** La fila **Rechazada en validación**, mensaje sobre la moneda del plan.

**Automatizable.** API (`corredor.py`, caso R-07).

---

## R-08 · Sin autorización sobre ese plan

**Qué prueba.** `AUTORIZACION_CORREO_PLAN` (RF-02) y el estado propio **Sin autorización**.

**Necesita.** El REMITENTE (reconocido, pero sin autorización sobre `PRNA` a propósito, P-03);
el adjunto `E08-sin-autorizacion.xlsx`.

**Cómo.** Correo nuevo con **`E08-sin-autorizacion.xlsx`** (plan `PRNA`, al que tu remitente no
está autorizado).

**Esperado.**

| Qué | Valor |
|---|---|
| La fila | **Sin autorización** ← un estado distinto de "Rechazada en validación" |
| Mensaje | *"Su correo no está autorizado sobre el plan PRNA"* — **nombra el plan** |
| Estado de la Solicitud | **Rechazada** |

> Ojo con el matiz: el **remitente sí está reconocido** (tiene autorización sobre otros planes), así
> que el correo se procesa. Lo que no está autorizado es **ese plan**. Si la Solicitud quedó en
> **No reconocida**, se están confundiendo las dos reglas.

**Automatizable.** API (`corredor.py`, caso R-08).

---

## R-09 · Sin autorización GANA sobre otra regla que rechaza

**Qué prueba.** D-14: `Sin autorización` tiene prioridad, **pero el mensaje lleva todos los motivos**.

**Necesita.** El REMITENTE; un Excel armado a mano a partir de `E08-sin-autorizacion.xlsx`
(plan `PRNA` + `moneda = EUR`).

**Cómo.** Hacé un Excel a mano a partir de `E08-sin-autorizacion.xlsx`: en la fila 13, dejá el plan
`PRNA` **y además** poné `moneda = EUR`. Mandalo.

**Esperado.**

| Qué | Valor |
|---|---|
| Estado de la fila | **Sin autorización** (no "Rechazada en validación") |
| `sanic_mensaje` | trae **los dos** motivos: el de autorización **y** el de la lista de Moneda |

**FALLA si** el estado es Rechazada en validación, o si el mensaje trae un solo motivo.

**Automatizable.** API (`corredor.py`, caso R-09; el propio corredor arma el Excel modificado,
no hace falta tocarlo a mano).

---

## R-10 · La referencia de formato 11 se construye sola

**Qué prueba.** `REFERENCIA_FORMATO_11` con D-17 y DD-10, y que la recibida se guarde aparte.

**Necesita.** El `REMITENTE` de este set (autorizado sobre `PR11`); el adjunto
`E01-feliz-formato11.xlsx` (este caso arma su propia Solicitud con `ctx.procesar()`, no depende de
otro set ni de otro caso — `CONVENCIONES.md` §8); que `plantilla.listas` traiga, en su CONTENIDO
(no solo que la fila exista), el banco `BAC` con código `007` — verificado en `preparar()`
(`preparacion_comun.verificar_bac_codigo_007()`, 2026-09-28): con un código distinto de `007` (aunque
también tenga 3 dígitos, como el `102` que documenta `diseno/PENDIENTES.md` sección B) la referencia
esperada abajo no coincidiría, y este caso daría FALLA por una razón de datos, no del sistema.

**Cómo.** Usá la Solicitud de **C-01** (`E01-feliz-formato11.xlsx`).

**Esperado.** Por cada fila:

| Campo | Valor |
|---|---|
| `sanic_referencia` | **construida**: `007` + cuenta rellena con ceros **a la izquierda** hasta 17 = **20 exactos** |
| `sanic_referenciarecibida` | lo que vino en el Excel, **tal cual** (en E01 viene vacía) |

**Verificá con la calculadora a mano** para al menos una fila. Cuenta `10203040506070` (14 dígitos):

```
007 + 000 + 10203040506070  =  00700010203040506070   (20 caracteres)
```

**FALLA si**: mide distinto de 20, rellena por la derecha, o usa un código de banco que no sea el
de `plantilla.listas` (BAC = `007`).

> Esta es **la prueba más importante de todo el plan**. Una referencia mal construida no falla: le
> paga a otra persona.

**Automatizable.** API (`corredor.py`, caso R-10) — **igual conviene recalcular a mano los 20
caracteres al menos una vez**, como pide el propio caso: la automatización no reemplaza esa
verificación puntual.

---

## R-11 · La referencia de formato 11 que NO se puede construir

**Qué prueba.** Los tres casos en que `REFERENCIA_FORMATO_11` falla (D-17).

**Necesita.** El REMITENTE; el adjunto `E09-referencia-formato11.xlsx`.

**Cómo.** Correo nuevo con **`E09-referencia-formato11.xlsx`**.

**Esperado.** Las 2 filas en **Rechazada en validación**:

| Fila | Cuenta | Por qué falla |
|---|---|---|
| 1 | `1234ABCD5678` | trae algo que no son dígitos |
| 2 | `123456789012345678` (18) | más de 17 caracteres |

> Que una cuenta con letras **rechace** en vez de construir una referencia rara es exactamente lo
> que protege a AS400.

**Automatizable.** API (`corredor.py`, caso R-11).

---

## R-12 · Formato 06: la referencia NO se deriva

**Qué prueba.** DD-15: en formatos 06 y 10 la referencia es **la recibida**, aunque venga vacía.

**Necesita.** El REMITENTE; el adjunto `E13-formato06-referencia-libre.xlsx`; el plan `PR06`.

**Cómo.** Correo nuevo con **`E13-formato06-referencia-libre.xlsx`** (plan `PR06`, referencia
`REF-LIBRE-000123`).

**Esperado.**

| Campo | Valor |
|---|---|
| Estado de la fila | **Validada** |
| `sanic_referencia` | **`REF-LIBRE-000123`** — tal cual, sin tocar |
| `sanic_referenciarecibida` | `REF-LIBRE-000123` |

**FALLA si** la referencia salió con `007` adelante o rellena con ceros: se estaría aplicando la
regla de formato 11 a un plan que no es 11.

**Automatizable.** API (`corredor.py`, caso R-12).

---

## R-13 · Mezcla de filas válidas y rechazadas

**Qué prueba.** Que una plantilla con de todo se procese fila por fila y la Solicitud quede
**En proceso** mientras haya al menos una válida (`03` §7).

**Necesita.** El REMITENTE; el adjunto `E10-mixto.xlsx`; sesión `ejecutivo` si además se quiere
confirmar la vista "Por digitar" en pantalla.

**Cómo.** Correo nuevo con **`E10-mixto.xlsx`** (4 filas).

**Esperado.**

| Fila | Estado esperado |
|---|---|
| 1 | **Validada** |
| 2 | **Validada** |
| 3 (plan `PRNA`) | **Sin autorización** |
| 4 (`moneda = EUR`) | **Rechazada en validación** |

| Qué | Valor |
|---|---|
| filas t/v/r | `4 / 2 / 2` |
| Estado | **En proceso** |
| Acuse | sí, y **detalla fila por fila** cuáles entraron y cuáles no, y por qué |
| Vista **Por digitar** | muestra **las 2 validadas**, no las otras |

> Leé el acuse completo. Es el caso más parecido a la realidad: el cliente manda 25 filas y algunas
> fallan. El acuse tiene que dejarle clarísimo qué corregir.

**Automatizable.** API (`corredor.py`, caso R-13, para la validación) + GUI (sesión `ejecutivo`)
si se quiere ver el resultado en la vista "Por digitar".

---

## R-14 · Duplicados dentro de la plantilla NO se validan

**Qué prueba.** DD-12: el control de duplicados está en AS400, no acá.

**Necesita.** El REMITENTE; un Excel armado con la fila 13 de `E01-feliz-formato11.xlsx` repetida
en las filas 14 y 15.

**Cómo.** Hacé un Excel con **la misma fila repetida tres veces** (copiá la fila 13 de
`E01-feliz-formato11.xlsx` a las filas 14 y 15). Mandalo.

**Esperado.** Las **3 filas Validadas**. `3 / 3 / 0`, Solicitud **En proceso**.

**FALLA si** alguna quedó rechazada por duplicada: sería una regla que el diseño no pidió.

**Automatizable.** API (`corredor.py`, caso R-14).

---

## R-15 · DN-01: `autorizacion.modo` en su canon (`plan`) antes de correr las reglas de Registro

**Qué prueba.** Que `autorizacion.modo` esté sembrado en su valor canónico (`plan`,
`CONVENCIONES.md` §8.2) en el momento en que corren las reglas de Registro de este archivo — el
ancla de configuración antes de que R-16/R-17 lo cambien a propósito y lo devuelvan. `plan` es
además el **fallback quemado** del propio código si el parámetro faltara (`ModoDeAutorizacionParametro.Resolver`,
`src/Sanic.Mppp.Plugins/Validacion/ReglasDeRegistro.cs`), así que este caso documenta la
configuración, no la degradación (eso es R-17).

**Necesita.** El REMITENTE de este set, ya autorizado por `AutorizacionPlan` sobre `PR11`/`PR06`/`PR10`/`PRIN`
(lo deja `preparar()`); el parámetro `autorizacion.modo` activo (lo deja el canon de `correr.py`,
antes de que este set corra).

**Cómo.** Leer la versión activa más alta de `autorizacion.modo` (misma consulta que usa
`Datos/Catalogos.cs::Parametro`: `sanic_nombre eq 'autorizacion.modo' and statecode eq 0`, orden
`sanic_version desc`, `$top=1`).

**Qué tiene que pasar.** `sanic_valor` recortado, sin distinguir mayúsculas, es exactamente `plan`.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py R` (caso `R-15`); a mano,
`dataverse_api.py GET "sanic_mppp_tbl_parametros?$filter=sanic_nombre eq 'autorizacion.modo' and statecode eq 0&$orderby=sanic_version desc&$top=1&$select=sanic_valor,sanic_version"`.

**Cómo se sabe que falló.** El valor activo no es `plan`: o el canon de `CONVENCIONES.md` §8.2 no se
aplicó antes de este set (defecto de `correr.py`/`preparacion_comun.aplicar_canon`), o algún caso
anterior (de este set o de otro corrido en la misma sesión) dejó una versión de prueba activa sin
desactivarla al terminar — exactamente el riesgo que describe la advertencia de `10-autorizacion-cliente.md`
sobre `autorizacion.modo` como parámetro de entorno compartido.

**Automatizable.** API (`correr.py R`, caso `R-15`).

---

## R-16 · DN-01: modo `cliente` — un correo autorizado SOLO por `AutorizacionCliente` valida sus filas

**Qué prueba.** El diseño resuelto de DN-01: en modo `cliente`, un correo autorizado únicamente por
`AutorizacionCliente` (CERO `AutorizacionPlan`) queda autorizado sobre **todos los planes activos del
cliente**, sin enumerarlos — ejercitado acá en el mismo punto donde vive `AUTORIZACION_CORREO_PLAN`
(nivel Registro, `ValidarSolicitud.cs:246` → `Catalogos.cs::PlanesAutorizadosDe`). Es el mismo
mecanismo que fija `U-02` de `10-autorizacion-cliente.md`, pero automatizado con el arnés de API de
este archivo en vez de `ver_solicitud.py --ultima` a mano.

**Necesita.** `autorizacion.modo` versionado a `cliente` **por este mismo caso** (agrega una fila
nueva, nunca edita la vigente — `CONVENCIONES.md` §8.2 — y la desactiva en su propio `finally`,
antes de devolver el control); el Autorizado `ZZP-` y la `AutorizacionCliente` `ZZP-` que crea
`preparar()` hacia el Cliente compartido `PR - Pruebas MPPP SA` (dueño de `PR11`); el adjunto
`E01-feliz-formato11.xlsx`.

**Cómo.** Con `autorizacion.modo = cliente` activo, correo nuevo desde el Autorizado `ZZP-` con
`E01-feliz-formato11.xlsx` (plan `PR11`, del Cliente compartido).

**Qué tiene que pasar.**

| Qué | Valor |
|---|---|
| `REMITENTE_RECONOCIDO` (nivel Correo, mismo punto de consulta) | Cumplida — el correo **entra** |
| Las 3 filas | **Validada** (`AUTORIZACION_CORREO_PLAN` resuelve por `AutorizacionCliente`, sin que el plan esté enumerado en ningún lado) |

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py R` (caso `R-16`).

**Cómo se sabe que falló.** Dos síntomas de la MISMA causa raíz (`DIRECTRICES-NUEVAS.md`, "El modo
cambia TODO, no una parte"): si la Solicitud queda **No reconocida**, el nivel Correo
(`ClasificarCorreo.cs`) no está resolviendo el modo; si el correo entra pero las filas quedan **Sin
autorización**, es el nivel Registro el que no lo resuelve. Cualquiera de los dos es el mismo bug que
ya corrigió el aprobador una vez ("el portero también tiene que preguntar lo mismo").

**Automatizable.** API (`correr.py R`, caso `R-16`).

---

## R-17 · DN-01: valor basura en `autorizacion.modo` degrada a `plan`, sin tumbar la operación

**Qué prueba.** `ModoDeAutorizacionParametro.Resolver`: cualquier valor que no sea exactamente
`cliente` (sin distinguir mayúsculas) degrada al fallback quemado `plan` — nunca lanza, nunca tumba
la validación de nadie.

**Necesita.** `autorizacion.modo` versionado a `"clienteX"` (basura) por este caso, desactivada en su
propio `finally`; el Autorizado de R-16 (autorizado SOLO por `AutorizacionCliente`, cero
`AutorizacionPlan`); el REMITENTE compartido de este set (autorizado SOLO por plan);
`E01-feliz-formato11.xlsx`.

**Cómo.**
1. Con `autorizacion.modo = "clienteX"` activo, correo desde el Autorizado de R-16 (solo cliente) con `E01-feliz-formato11.xlsx`.
2. Con el mismo parámetro basura activo, correo desde el REMITENTE de siempre (solo plan) con el mismo Excel.

**Qué tiene que pasar, según el DISEÑO.**

| Correo | Resultado esperado |
|---|---|
| Autorizado de R-16 (solo cliente) | **No reconocida** — el valor basura degradó a `plan`, y por `plan` ese correo no tiene ninguna autorización |
| REMITENTE de siempre (solo plan) | Igual que siempre: **En proceso**, 3 filas **Validada** |

> **DEFECTO CONFIRMADO — este caso da FALLA a propósito, hasta que se resuelva D-59.** El paso 1
> nunca llega a devolver la clasificación: `sanic_mppp_capi_clasificarcorreo` responde **HTTP 400**
> con el mensaje genérico *"No se pudo clasificar el correo. El equipo tecnico ya tiene el
> detalle."* en vez de la respuesta normal (`clasificacion=remitente_no_reconocido, procesar=false`).
> Confirmado contra `plugintracelog` de Dev el 2026-09-28: la causa real es
> `Datos/Avisos.cs:109` (`AvisosDataverse.AvisarAEjecutivos`), que hace
> `e.GetAttributeValue<EntityReference>("systemuserid")` sobre `systemuserroles` (tabla
> intersección) — esa columna viaja como `Guid`, no como `EntityReference`, y el cast lanza
> `InvalidCastException`. Pasa **siempre** que una Solicitud termina en un estado que dispara el
> aviso a ejecutivos (respuesta, ilegible, remitente no reconocido), y la transacción entera se
> revierte: ni siquiera queda `sanic_estadoprocesamiento = No reconocida` persistido. Es
> `diseno/PENDIENTES.md`, entrada **D-59: ABIERTA, pospuesta por el aprobador (2026-09-27)** — el
> mismo defecto que ya dejó en rojo `C-02`/`C-04`/`C-10`/`C-11` del set C. **Por CONVENCIONES §3,
> "Un defecto conocido no es N-A": un caso bloqueado por un defecto ya registrado es FALLA**, aunque
> D-59 no sea el tema de este caso — nunca se apaga la prueba que lo detecta, ni se lo pinta de gris
> con un N-A que dejaría el resumen mintiendo "0 FALLA".

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py R` (caso `R-17`) — hoy tiene que dar
**FALLA**, citando `D-59` en el motivo del primer check.

**Cómo se sabe que falló *de la forma esperada*.** El síntoma correcto es el check
`bloqueado por D-59 (diseno/PENDIENTES.md): ...HTTP 400...` en rojo, con el detalle
`InvalidCastException`/`systemuserroles` (vía `plugintracelog`, si hace falta confirmarlo a mano).
Si algún día `sanic_mppp_capi_clasificarcorreo` empieza a devolver 200 con
`clasificacion=remitente_no_reconocido, procesar=false` para este correo, **este caso pasa a
ejercitar sus aserciones normales** (la tabla de arriba) y hay que actualizar `diseno/PENDIENTES.md`
(cerrar `D-59`) y esta nota. Si en cambio aparece cualquier OTRO síntoma (un HTTP distinto de 400,
un mensaje que no sea el genérico de siempre, o que la segunda mitad —remitente por plan— deje de
entrar), **eso es un defecto NUEVO, distinto de D-59**, y se investiga aparte: no se le atribuye a
D-59 solo porque el caso ya venía en rojo.

**Automatizable.** API (`correr.py R`, caso `R-17`; **FALLA hoy, bloqueado por D-59** — ver la nota
de arriba).

---

## R-18 · DN-03: `Exclusion` afloja `numeroCuenta`, `banco` y `moneda`

**Qué prueba.** La versión 2 de `plantilla.obligatoriedad`, sembrada en Dev el 2026-09-27
(`playbooks/datos/parametros.md`): una fila de gestión `Exclusion` puede omitir cuenta, banco y
moneda sin que `OBLIGATORIEDAD` la rechace. **El criterio lo confirmó el aprobador el 2026-09-28**
(`playbooks/datos/parametros.md`): ya no es tentativo. Sigue habiendo una dependencia de ENTORNO —
que la versión activa hoy traiga `porGestion.Exclusion` sembrado— y ese caso se reporta `N-A`, no
FALLA, si no está (ver abajo).

**Necesita.** La versión activa más alta de `plantilla.obligatoriedad` con `porGestion.Exclusion`
trayendo `numeroCuenta`, `banco` y `moneda` como opcionales (lo confirma `preparar()`, sin sembrar
nada nuevo: **no se versiona este parámetro solo para este caso** — es GLOBAL y de negocio, a
diferencia de `autorizacion.modo` en R-16/R-17, que este set sí puede versionar y desactivar de
forma transitoria). Un Excel ad hoc con una fila `gestion=Exclusion`, plan `PR06` (formato 06, para
no mezclar con `REFERENCIA_FORMATO_11`), `numeroCuenta`/`banco`/`moneda` en blanco, y
`numeroPlan`/`nombreBeneficiario`/`tipoIdentificacion`/`numeroIdentificacion` completos.

**Cómo.** Correo nuevo con ese Excel ad hoc (`ZZ-R18-exclusion-aflojada.xlsx`, armado por el propio
caso con `generar_excel.fila()` + `ctx.excel_ad_hoc()`, igual que R-09/R-14).

**Qué tiene que pasar.** La fila queda **Validada**: `OBLIGATORIEDAD` no la rechaza por
`numeroCuenta`, `banco` ni `moneda` vacíos.

**Si el parámetro activo HOY no trae `porGestion.Exclusion`** (por ejemplo, en un entorno donde
todavía no se sembró esa versión, o si alguien sembró una versión 3 que lo saca): el caso se reporta
**N-A**, con el motivo exacto, en vez de FALLA — no es un defecto de la regla, es una precondición de
entorno, no una duda sobre el criterio (que ya está confirmado, `CONVENCIONES.md` §3, "todo N-A se
reporta explícitamente, con su motivo").

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py R` (caso `R-18`); a mano,
`ver_solicitud.py <MPPP-########>` sobre la fila creada.

**Cómo se sabe que falló.** La fila se rechaza citando `numeroCuenta`, `banco` o `moneda`: la versión
activa no trae el `porGestion.Exclusion` esperado (y en ese caso el caso debería haber dado N-A, no
FALLA — si dio FALLA con esos tres campos citados y el parámetro SÍ trae `porGestion.Exclusion`, el
motor no lo está aplicando).

**Automatizable.** API (`correr.py R`, caso `R-18`).

---

## Obligatoriedad por gestión, exhaustivo — casos R-19 a R-28 (fusionado del ex set O)

**Fusionado acá el 2026-09-28** (`CONVENCIONES.md` §4, "La regla del nombre"): estos diez casos eran
el set `O` (`12-obligatoriedad-gestion.md`, prefijo `O-`), un archivo aparte nombrado por la
directiva que lo originó (DN-03) en vez de por lo que prueba. La obligatoriedad por gestión **ES**
una de las ocho reglas de este mismo archivo (`OBLIGATORIEDAD`) — tenerla en otro archivo partía el
motor de reglas en dos sin necesidad. El archivo `12-obligatoriedad-gestion.md` desapareció; la
tabla de equivalencias completa (`O-01`→`R-19` … `O-10`→`R-28`) vive en `pruebas/README.md`.

**`plantilla.obligatoriedad` es un parámetro GLOBAL** (§8.2 de `CONVENCIONES.md`): sembrar una
versión nueva la vuelve la ACTIVA de inmediato, para toda Solicitud que se valide mientras esté
activa, en TODO el entorno. **Ninguno de estos diez casos siembra una versión nueva.** Ni un solo
`POST`/`PATCH` sobre `sanic_mppp_tbl_parametros`:

- R-19, R-20 y R-26 leen lo YA sembrado (versión 1 y versión 2 activas hoy), nunca lo cambian.
- R-21, R-23 y R-27 mandan Excel contra las Custom API, pero ejercitan gestiones/escenarios que la
  versión YA activa cubre (nunca siembran una versión nueva para forzar un escenario).
- R-22 (precedencia), R-24 (claves de `porGestion` sin distinguir caja/tildes) y R-25 (clave
  desconocida dentro de `porGestion`) son los tres escenarios de mayor riesgo del diseño: los tres
  se verifican corriendo `dotnet test` contra la suite ya construida con TDD
  (`tests/Sanic.Mppp.Plugins.Tests/Aceptacion/ParametrosDePlantillaAceptacion.cs`), nunca sembrando
  nada en Dev. Necesitan `dotnet` disponible en el PATH.
- R-28 lee el catálogo de Reglas (`sanic_mppp_tbl_reglas`), no toca nada tampoco.

**Frontera con R-18 (arriba), ahora dentro del mismo archivo.** R-18 ya manda un Excel real con
`gestion=Exclusion` y `numeroCuenta`/`banco`/`moneda` en blanco contra las Custom API en vivo, y
confirma que la fila queda Validada — la prueba EN VIVO, dentro del motor de las ocho reglas, de que
`porGestion.Exclusion` funciona. Estos diez casos NO repiten ese envío (sería el mismo Excel, el
mismo resultado, la misma evidencia con otro ID): son la cobertura **exhaustiva** de DN-03, lo que
R-18 no cubre — R-20 verifica de solo lectura que el CONTENIDO sembrado es exactamente el que pidió
el aprobador; R-21/R-23/R-27 ejercitan escenarios en vivo que R-18 no toca
(`Inclusion`/`Modificacion` sin aflojar, una gestión inválida en la FILA, el mensaje al cliente);
R-22/R-24/R-25 son de alto riesgo o de precedencia, nunca sembrados en Dev; R-26 es la evidencia de
que versionar agrega una fila y nunca edita la vigente; R-19, R-28 cierran la compatibilidad y el
catálogo.

**Necesita, en general** (además de lo que ya deja listo `preparar()` de este set, arriba): `dotnet`
disponible en el PATH para R-22, R-24 y R-25 (`dotnet test tests/Sanic.Mppp.Plugins.Tests --filter
<FullyQualifiedName>`).

---

## R-19 · Compatibilidad: un parámetro SIN `porGestion` se comporta igual que antes de DN-03

**Qué prueba.** La garantía explícita del diseño: "un parámetro sin `porGestion` se comporta
EXACTAMENTE como hoy". Es la condición bajo la que lo ya sembrado en los tres entornos sigue siendo
válido sin migrar nada. La prueba EN VIVO de una gestión sin ajuste ya existe en el propio motor de
las ocho reglas (R-06, gestión `Inclusion`, estable v1/v2); este caso no la repite (ver la frontera
de arriba) — verifica la garantía general a nivel unitario más la integridad de lo sembrado.

**Necesita.** Confirmar que la versión 1 de `plantilla.obligatoriedad` (`{"porDefecto":
"obligatorio","opcionales":[{"campo":"referencia"}]}`, sin `porGestion`) sigue existiendo intacta
(no hace falta que sea la ACTIVA para este caso: alcanza con que la fila exista — ver R-26 para el
chequeo de que ninguna versión se edita). `dotnet` en el PATH.

**Cómo.** (1) `dotnet test` con `--filter` exacto sobre
`ParametrosDePlantillaAceptacion.DN03_un_parametro_sin_porGestion_se_comporta_exactamente_como_hoy`
(la garantía general: con y sin `reglas`, ninguna combinación cambia). (2) `GET` de solo lectura
sobre `sanic_mppp_tbl_parametros` filtrando `sanic_nombre eq 'plantilla.obligatoriedad' and
sanic_version eq 1`.

**Qué tiene que pasar.** (1) La prueba unitaria pasa, y el filtro encontró exactamente 1 prueba (si
encontrara 0, el filtro está mal escrito y el caso mentiría "OK" sin haber corrido nada). (2) La
fila de la versión 1 existe y su `sanic_valor`, interpretado como JSON, es idéntico al de DD-16.

**Cómo se verifica.** La salida de `dotnet test` (recuento Failed/Passed/Total) y la respuesta de
esa consulta `GET`.

**Cómo se sabe que falló.** La prueba unitaria falla (una regresión real en el dominio), el filtro
encontró 0 o más de 1 prueba (el nombre del `[Fact]` cambió sin actualizar este caso), o la versión
1 no existe o su valor cambió (alguien la editó en vez de agregar una versión nueva — ver R-26).

**Automatizable.** Unit (`dotnet test`) + API de solo lectura.

---

## R-20 · El contenido sembrado de `porGestion.Exclusion` es exactamente el pedido

**Qué prueba.** Que la versión ACTIVA hoy de `plantilla.obligatoriedad` trae, dentro de
`porGestion.Exclusion`, exactamente los tres campos que pidió el aprobador (`numeroCuenta`, `banco`,
`moneda`) como `opcionales`, y ninguno como `obligatorios` — ni de más ni de menos. **La prueba EN
VIVO de que ese aflojamiento funciona contra una Solicitud real ya la hace R-18** (arriba): este
caso es sobre el CONTENIDO del parámetro sembrado, de solo lectura, no vuelve a mandar un Excel.

**Necesita.** `plantilla.obligatoriedad` en su versión activa más alta (§8.2: no se siembra nada
acá). **El criterio lo confirmó el aprobador el 2026-09-28** (`playbooks/datos/parametros.md`): ya
no es tentativo. Si en el entorno que se está corriendo `porGestion.Exclusion` no está sembrado
(por ejemplo, todavía no se sembró en Test o Producción), este caso reporta `N-A` en vez de asumir
un valor que no existe — esa sigue siendo una precondición legítima de entorno, no una duda sobre el
criterio.

**Cómo.** `GET` de solo lectura sobre `sanic_mppp_tbl_parametros` (versión activa más alta de
`plantilla.obligatoriedad`, la misma consulta que ya hace `preparacion_comun.verificar_parametro_activo`),
parseando su `sanic_valor` como JSON y mirando `porGestion.Exclusion`.

**Qué tiene que pasar.** `porGestion.Exclusion.opcionales` es EXACTAMENTE el conjunto
`{numeroCuenta, banco, moneda}` (nombrados, no contados: CONVENCIONES §7), sin `obligatorios`, y
ninguna otra gestión (`Inclusion`, `Modificacion`) tiene entrada todavía (ver R-21).

**Cómo se verifica.** La respuesta de esa consulta `GET`, ya parseada.

**Cómo se sabe que falló.** Falta alguno de los tres campos, sobra alguno, o `Exclusion` trae
`obligatorios`: el parámetro sembrado no es el que el aprobador pidió, y R-18 (que sí manda un
Excel real) podría estar pasando por una razón distinta a la documentada.

**Automatizable.** API de solo lectura.

---

## R-21 · `Inclusion` y `Modificacion` NO tienen entrada en `porGestion` todavía

**Qué prueba.** Solo `Exclusion` está sembrada en `porGestion` hoy; `Inclusion` y `Modificacion`
siguen bajo el bloque global sin ningún ajuste.

**Necesita.** La misma versión activa que R-20 (se relee, no se asume el resultado de R-20: si
mientras tanto `Inclusion`/`Modificacion` ganaron una entrada en `porGestion`, este caso lo detecta
y reporta `N-A` en vez de mentir un `FALLA` contra una forma del parámetro que ya cambió).

**Cómo.** Un Excel ad hoc (`ctx.excel_ad_hoc`, no a mano) con dos filas: `gestion=Inclusion` y
`gestion=Modificacion`, las dos con `numeroCuenta`/`banco`/`moneda` en blanco.

**Qué tiene que pasar.** Las dos filas se rechazan por `OBLIGATORIEDAD` (esos campos siguen siendo
obligatorios para estas dos gestiones): comportamiento idéntico al de antes de DN-03.

**Cómo se verifica.** `sanic_estado` y `sanic_mensaje` de las dos filas (cita al menos uno de
`numeroCuenta`/`banco`/`moneda`).

**Cómo se sabe que falló.** Alguna fila NO se rechaza, o se rechaza sin citar ninguno de esos tres
campos: `porGestion` se estaría aplicando a una gestión que no tiene entrada sembrada (contagio
entre gestiones que el diseño no pide).

**Automatizable.** API.

---

## R-22 · Precedencia `obligatorios` sobre `opcionales` (cobertura unitaria, no sembrar en Dev)

**Qué prueba.** "`opcionales` afloja, `obligatorios` endurece: si un campo queda en las dos listas
de la MISMA entrada, gana `obligatorios`" — la mitad "endurece" de esta regla no está sembrada hoy
en ningún entorno (el ejemplo de `Modificacion` con `obligatorios` en `DIRECTRICES-NUEVAS.md` es
ilustrativo, no está en Dev).

**Necesita.** **No sembrar una versión nueva en Dev solo para este caso**: crear una entrada con
`obligatorios` cambiaría la versión ACTIVA para todo el entorno de forma permanente (agregar
versiones es fácil; "deshacer" es sembrar otra versión más, nunca vuelve sola a la anterior). La
cobertura confiable es `ParametrosDePlantillaAceptacion` (unitaria, ya construida con TDD según
`DIRECTRICES-NUEVAS.md`).

**Cómo.** Revisar que existan casos unitarios para: (a) un campo en `opcionales` de `porGestion`
sin estar en `obligatorios`, (b) un campo en `obligatorios` sin estar en `opcionales`, (c) el mismo
campo en las dos listas de la misma entrada (gana `obligatorios`). Los tres viven en
`ParametrosDePlantillaAceptacion.cs`: (a)+(b) en
`DN03_porGestion_afloja_con_opcionales_y_endurece_con_obligatorios_la_gestion_gana_sobre_lo_global`
(gestión `Exclusion` = solo opcionales, gestión `Modificacion` = solo obligatorios) y (c) en
`DN03_si_un_campo_esta_en_opcionales_y_en_obligatorios_de_la_misma_gestion_gana_obligatorios`.

**Qué tiene que pasar.** Los tres escenarios ya están cubiertos en la suite de plugins, y las dos
pruebas pasan.

**Cómo se verifica.** (1) Que el código fuente de `ParametrosDePlantillaAceptacion.cs` siga
declarando esos dos nombres de `[Fact]` (si se renombraron, este caso lo detecta en vez de correr un
filtro que no encuentra nada y "pasa" en falso). (2) `dotnet test
tests/Sanic.Mppp.Plugins.Tests --filter "FullyQualifiedName=<FQN1>|FullyQualifiedName=<FQN2>"`
sobre esos dos nombres exactos — el filtro tiene que encontrar EXACTAMENTE 2 pruebas.

**Cómo se sabe que falló.** Falta alguno de los dos `[Fact]` en el archivo, el filtro encuentra un
número de pruebas distinto de 2 (nombre cambiado sin avisar), o alguna de las dos falla.

**Automatizable.** Unit (no Dev).

---

## R-23 · Gestión inválida en la FILA: `OBLIGATORIEDAD` ni siquiera se evalúa

**Qué prueba.** "Una fila cuyo valor de gestión no es válido SOLO calza con `*` en `reglas`, y NUNCA
mira `porGestion`" — y, más preciso todavía: `OBLIGATORIEDAD` **depende de `LISTAS_VALIDAS`**
(`playbooks/datos/reglas.md`, `sanic_dependede` de `OBLIGATORIEDAD` =
`"AUTORIZACION_CORREO_PLAN,LISTAS_VALIDAS"`; DD-13, "las reglas de registro guardan los motivos de
lo que falló, sin historial por regla"). Con una gestión inválida, `LISTAS_VALIDAS` YA rechaza esa
fila, así que `OBLIGATORIEDAD` **no se evalúa en absoluto** — no es que se evalúe "con el bloque
global": directamente no corre, y por eso no puede sumar ningún motivo derivado de `porGestion` (ni
aflojado ni endurecido).

**Necesita.** Un Excel con una fila con un valor de `gestion` que no es
`Inclusion`/`Exclusion`/`Modificacion` (p. ej. `"Baja"`), y ADEMÁS con `numeroCuenta`/`banco` en
blanco (para poder distinguir, por el mensaje, si `OBLIGATORIEDAD` corrió o no).

**Cómo.** Un Excel ad hoc (`ctx.excel_ad_hoc`) con esa única fila, mandado contra las Custom API.

**Qué tiene que pasar.** La fila se rechaza citando el campo `gestión` (el único motivo:
`LISTAS_VALIDAS`); el mensaje NO cita `numeroCuenta` ni `banco` — la prueba de que `OBLIGATORIEDAD`
nunca llegó a correr, con o sin `porGestion`.

**Cómo se verifica.** `sanic_estado` y `sanic_mensaje` de esa fila.

**Cómo se sabe que falló.** El mensaje cita `numeroCuenta` o `banco` (señal de que `OBLIGATORIEDAD`
sí corrió pese a la dependencia declarada — un cambio de comportamiento no documentado), o no cita
`gestión` en absoluto.

**Automatizable.** API.

---

## R-24 · Las CLAVES de `porGestion` (dentro del parámetro) no distinguen caja ni tildes (unitario, no Dev)

**Qué prueba.** Cuidado con una trampa de redacción: esto **no** es sobre lo que un cliente escribe
en la columna `Gestión` del Excel — eso ya lo normaliza `ListasPlantilla` (D-18a) ANTES de llegar a
`ObligatoriedadPlantilla`, así que un Excel con `"exclusión"` en la celda se comporta igual con o sin
DN-03 y no ejercitaría nada nuevo. Lo que DN-03 agrega es la comparación sin distinguir caja ni
tildes de las **claves del propio JSON** sembrado en `plantilla.obligatoriedad` (p. ej. que
`"porGestion":{"EXCLUSION":{...}}` se reconozca como la gestión `Exclusion`) — y eso solo se puede
ejercitar con una forma de parámetro que hoy NO está sembrada en ningún entorno.

**Necesita.** **No sembrar una versión nueva en Dev para esto**: cambiar la CLAVE con la que está
sembrado `porGestion` en Dev no aporta nada que un Excel no pueda ya probar con la forma actual, y sí
suma el mismo riesgo de R-22/R-25 (tocar la versión activa de todo el entorno) sin necesidad. `dotnet`
en el PATH.

**Cómo.** `dotnet test` con `--filter` sobre
`ParametrosDePlantillaAceptacion.DN03_los_nombres_de_gestion_de_porGestion_se_comparan_sin_distinguir_caja_ni_tildes`
(un `[Theory]` con las variantes `"  exclusión  "`, `"EXCLUSION"` y `"ExclusioN"` como CLAVE del
JSON, no como valor de fila).

**Qué tiene que pasar.** El filtro encuentra al menos una prueba y todas pasan: las tres variantes
de clave resuelven a la misma gestión (`Exclusion`) y aplican el mismo aflojamiento.

**Cómo se verifica.** La salida de `dotnet test` (recuento Failed/Passed/Total).

**Cómo se sabe que falló.** El filtro no encuentra ninguna prueba (el nombre cambió y este caso
quedó "verde" sin haber corrido nada), o alguna variante falla.

**Automatizable.** Unit (`dotnet test`), no Dev.

---

## R-25 · Gestión desconocida DENTRO de `porGestion` es error de configuración (alto riesgo, no sembrar en Dev)

**Qué prueba.** Un typo en la CLAVE del parámetro (p. ej. `"Exclusio"` en vez de `"Exclusion"`) es
un `FormatException` al leer el parámetro — error ruidoso de configuración, distinto de R-23 (que es
sobre el VALOR de la fila, no sobre el parámetro).

**Necesita.** **NO sembrar esto en Dev de rutina**: un `plantilla.obligatoriedad` con esta clave
mal escrita, si se convierte en la versión activa, rompe la validación de TODA Solicitud del
entorno hasta que alguien lo note y siembre una versión corregida (más alta). Es exactamente el
"falla cerrado a propósito" del diseño, pero contra un entorno compartido el costo es alto.

**Cómo.** Lo que corre `herramientas/pruebas/sets/set_r.py` (arnés API, sin navegador) es SOLO la
mitad unitaria, a propósito: (1) confirma que el archivo de pruebas sigue trayendo un caso con una
gestión desconocida DENTRO de `porGestion` (distinto de R-23, que es sobre el valor de la FILA), y
(2) corre con `dotnet test --filter` el `[Theory]`
`ParametrosDePlantillaAceptacion.Una_obligatoriedad_mal_cargada_revienta_con_el_motivo` completo (la
clase entera de "parámetro mal cargado", que incluye esa fila de datos). **La mitad de alto riesgo
contra Dev de verdad (sembrar `"porGestion":{"Exclusioon":{...}}` y mandar una Solicitud) NO se
automatiza acá**: si algún día hace falta la confirmación contra un entorno real, se hace a mano, en
una ventana coordinada, con una versión corregida lista para sembrar de inmediato después — y se
documenta el resultado en la tabla de Resultados con la fecha y quién la corrió.

**Qué tiene que pasar.** El `[Theory]` completo pasa (incluida la fila de la gestión desconocida): un
parámetro con una clave de `porGestion` que no es ninguna gestión válida revienta con
`FormatException` y un motivo que nombra la clave, nunca una lista "vacía pero válida".

**Cómo se verifica.** La salida de `dotnet test` (recuento Failed/Passed/Total).

**Cómo se sabe que falló.** El caso de la gestión desconocida desapareció del archivo de pruebas
(la búsqueda del literal no lo encuentra), o el `[Theory]` completo (o esa fila en particular) falla.

**Automatizable.** Unit (`dotnet test`, lo que corre este archivo) / manual de alto riesgo contra
Dev (fuera de este arnés, ver "Cómo").

---

## R-26 · Versionar es AGREGAR una fila, nunca editar la vigente

**Qué prueba.** DD-02/la nota de `playbooks/datos/parametros.md`: cambiar una regla de negocio
siembra `sanic_version` más alto como fila NUEVA; la anterior queda intacta, de respaldo/histórico,
y el motor resuelve la activa por `sanic_version` descendente con `statecode` activo.

**Necesita.** Nada que crear: alcanza con leer lo ya sembrado.

**Cómo.** `GET sanic_mppp_tbl_parametros?$select=sanic_version,sanic_valor,statecode&$filter=sanic_nombre
eq 'plantilla.obligatoriedad'&$orderby=sanic_version asc` (mismo patrón de lectura que usa
`preparacion_comun.py`, sin ningún `POST`/`PATCH`).

**Qué tiene que pasar.** Aparecen AL MENOS dos filas (`sanic_version=1` y `sanic_version=2`), las
dos activas, con `sanic_valor` distinto entre sí, y la versión 1 sigue exactamente como la
documenta `DD-16` (sin `porGestion`).

**Cómo se verifica.** La respuesta de esa consulta.

**Cómo se sabe que falló.** Solo aparece una fila (la versión 1 se editó en vez de agregar la 2:
rompería `sanic_versionparametros` de Solicitudes ya procesadas con la versión anterior, que
dejaría de decir la verdad), o la versión 1 tiene un `sanic_valor` distinto al original.

**Automatizable.** API.

---

## R-27 · El mensaje al cliente no cambia con DN-03

**Qué prueba.** El texto de rechazo de `OBLIGATORIEDAD` sigue nombrando el CAMPO, sin citar su
valor (D-41) — que la obligatoriedad ahora dependa de la gestión no puede filtrarse al texto que
lee el cliente (no tiene por qué deducir la configuración interna del banco).

**Necesita.** Una fila de gestión `Inclusion` con `numeroCuenta` vacío (sigue siendo obligatoria
para esa gestión — R-21): la fila 2 ("Sin Cuenta") de `E06-obligatoriedad.xlsx`, que
`generar_excel.py` YA genera para R-06 — se reutiliza tal cual, sin armar un Excel nuevo
(CONVENCIONES §9).

**Cómo.** Mandar ese Excel (`E06-obligatoriedad.xlsx`) y leer `sanic_mensaje` de la fila 2.

**Qué tiene que pasar.** El mensaje nombra el campo (`numeroCuenta`) exactamente como antes de
DN-03, sin mencionar "gestión", "Exclusion" ni ninguna palabra de configuración interna.

**Cómo se verifica.** `ver_solicitud.py --ultima`.

**Cómo se sabe que falló.** El mensaje cambió de forma, o menciona la gestión o el hecho de que
otra gestión no exigiría este campo.

**Automatizable.** API.

---

## R-28 · La regla `OBLIGATORIEDAD` no cambia de código, orden, nivel ni dependencias

**Qué prueba.** DN-03 es explícito: "No cambia de código, de orden, de nivel ni de efecto. Solo
cambia qué considera obligatorio" — mismo principio que DN-01 sobre `AUTORIZACION_CORREO_PLAN`.
DD-13 (`diseno/02-diccionario-datos.md`) dice que las reglas de REGISTRO (las ocho de
`ReglasDeRegistro.cs`, `OBLIGATORIEDAD` entre ellas) NO dejan un `ResultadoRegla` propio ni un
evento de Bitácora por regla — "la fila guarda los motivos de lo que falló, sin historial por
regla". No hay ninguna Bitácora de la que leer "el orden en que se evaluaron las reglas de una
fila": eso solo existe para las cinco reglas de nivel Solicitud (set `S`). Lo que SÍ tiene código,
nivel, orden y dependencias declarados, en vivo, es el catálogo `sanic_mppp_tbl_reglas` — la misma
tabla que ya lee `sets/set_i.py` (I-01..I-04) — y es contra eso que se verifica este caso, no contra
una Solicitud procesada.

**Necesita.** Nada que crear: el catálogo de Reglas ya existe (`preparar_datos.py`/playbooks).

**Cómo.** `GET sanic_mppp_tbl_reglas?$select=sanic_codigo,sanic_nivel,sanic_efecto,sanic_orden,
sanic_dependede,statecode&$filter=sanic_codigo eq 'OBLIGATORIEDAD'`.

**Qué tiene que pasar.** Una única fila, activa, con `sanic_nivel=Registro`, `sanic_efecto=Rechaza`,
`sanic_orden=60` y `sanic_dependede='AUTORIZACION_CORREO_PLAN,LISTAS_VALIDAS'` — exactamente lo que
ya trae `playbooks/datos/reglas.md` desde antes de DN-03.

**Cómo se verifica.** La respuesta de esa consulta `GET`.

**Cómo se sabe que falló.** Cualquiera de esos cinco valores cambió, o hay más de una fila con ese
código: señal de que se reimplementó (otro código, otro orden) en vez de ajustarse, contra la
instrucción explícita del diseño.

**Automatizable.** API de solo lectura.

---

## Fase 2 — investigación y conclusión

Se investigó si algo de `diseno/DIRECTRICES-NUEVAS.md` (DN-01, DN-02, DN-03) agrega o cambia un
comportamiento que se resuelva **a nivel Registro** (el nivel que cubre este set, las ocho reglas
fila por fila) y qué le corresponde exactamente a este archivo frente a los sets dedicados que ya
existen.

1. **DN-01 (autorización por cliente) SÍ toca este nivel, y con evidencia de código exacta.**
   `diseno/DIRECTRICES-NUEVAS.md` traza el punto único de consulta: `Datos/Catalogos.cs:209`
   (`PlanesAutorizadosDe`), con dos llamadores — `ClasificarCorreo.cs:242` (`REMITENTE_RECONOCIDO`,
   nivel Correo) y **`ValidarSolicitud.cs:246` (`AUTORIZACION_CORREO_PLAN`, nivel Registro)**. El
   propio diseño es explícito: *"La regla `AUTORIZACION_CORREO_PLAN` no cambia de código, de orden,
   de nivel ni de efecto"* — sigue siendo una de las ocho reglas de este archivo. Por eso se
   agregaron R-15 (canon `plan`), R-16 (modo `cliente` autoriza sin enumerar planes) y R-17 (valor
   basura degrada a `plan`), automatizados con el mismo arnés de API que R-01..R-14.

   **Lo que este archivo NO duplica.** `10-autorizacion-cliente.md` (set `U`, letra ya reservada en
   `CONVENCIONES.md` §4) es la cobertura **exhaustiva** de DN-01: además de lo que R-15..R-17
   ejercitan por API, cubre el mensaje idéntico en los dos modos (`U-06`), que `AutorizacionPlan`
   "revive" al volver a `plan` sin haberse borrado nunca (`U-05`), el nombre calculado de
   `AutorizacionCliente` (`U-07`), la clave alternativa (`U-11`) y tres casos de GUI sobre
   formularios y subgrillas (`U-08`, `U-09`, `U-10`) que no tienen nada que ver con las ocho reglas
   de Registro que este archivo cubre. Agregar esos mismos casos acá con ID `R-` sería exactamente
   el error que ya evitó `02-reglas-de-solicitud.md` con `sanic_requiererevision`: duplicar, con otro
   nombre, algo que ya tiene dueño (`CONVENCIONES.md` §4, un ID por archivo, sin colisiones). R-15..R-18
   son el subconjunto de DN-01/DN-03 que vive específicamente en el motor de las ocho reglas
   (`ReglasDeRegistro.cs`) y que este archivo ya automatizaba por API; `U` sigue siendo el dueño de
   la cobertura completa.

2. **DN-02 (exportar a Excel para digitar) NO toca las ocho reglas de este archivo**, aunque su
   Custom API (`sanic_mppp_capi_exportarparadigitar`, `src/Sanic.Mppp.Plugins/Api/ExportarParaDigitar.cs`)
   opere sobre la tabla `Fila` (que sí es de nivel Registro). La diferencia es de fondo: `Fila` es una
   tabla de nivel Registro, pero `ExportarParaDigitar` **no es una de las ocho reglas de
   `ReglasDeRegistro.cs`** ni pasa por `MotorDeReglas<FilaEnValidacion>` — es una operación **posterior**
   a la validación, sobre filas que YA quedaron `Digitada`/`Por digitar`, con su propio parámetro
   (`exportacion.columnas`) y su propio archivo dedicado: `11-exportacion-excel.md` (set `E`,
   `CONVENCIONES.md` §4). Ningún caso se agrega acá por DN-02.

3. **DN-03 (obligatoriedad por gestión) SÍ toca este nivel**: `OBLIGATORIEDAD` es una de las ocho
   reglas de `ReglasDeRegistro.cs` (`EvaluadorObligatoriedad`, `Validacion/ReglasDeRegistro.cs`), y
   ya tenía un caso en este archivo (R-06) antes de que existiera DN-03. La nota agregada a R-06 deja
   escrito por qué ese caso, sin cambiarle una línea de código, sigue siendo la evidencia de
   compatibilidad hacia atrás (gestión `Inclusion`, sin entrada en `porGestion`). Se agregó R-18 para
   el aflojamiento nuevo de `Exclusion`, confirmado por el aprobador el 2026-09-28.

   **Lo que este archivo ya NO separa en otro archivo.** Hasta el 2026-09-28, `12-obligatoriedad-
   gestion.md` (set `O`) era la cobertura exhaustiva de DN-03 en un archivo aparte, nombrado por la
   directiva que lo originó. Ese día se fusionó ACÁ (`CONVENCIONES.md` §4, "La regla del nombre"):
   la precedencia `obligatorios`/`opcionales` (ahora `R-22`, deliberadamente **unitaria**, nunca
   sembrada en Dev), la gestión inválida DENTRO del parámetro (`R-25`, alto riesgo, no se siembra de
   rutina), el versionado agrega-nunca-edita (`R-26`) y el mensaje al cliente sin cambios (`R-27`)
   — ver la sección "R-19 a R-28" arriba. R-06 y R-18 siguen siendo el subconjunto que se ejercita
   EN VIVO dentro del motor de las ocho reglas; R-19..R-28 son la cobertura exhaustiva completa
   (incluida la unitaria y la de alto riesgo, que no se automatizan contra Dev a propósito), ahora en
   el mismo archivo en vez de en uno separado.

**Conclusión**: el set R suma catorce casos de fase 2 (R-15..R-28) sobre dos de sus ocho reglas
(`AUTORIZACION_CORREO_PLAN`, `OBLIGATORIEDAD`) — cuatro (R-15..R-18) automatizados en vivo dentro del
propio motor de reglas, diez (R-19..R-28, el ex set O) como cobertura exhaustiva de DN-03 — sin
duplicar la cobertura que sigue teniendo `U` para DN-01. DN-02 no aplica a este archivo: tiene su
propio dueño (`E`).

---

## Resultados

| Caso | Solicitud | Resultado | Notas |
|---|---|---|---|
| R-01 listas inválidas | | | ¿cita el valor? |
| R-02 largos | | | |
| R-03 plan inexistente | | | |
| R-04 plan inactivo | | | |
| R-05 formato 11 no ACH | | | |
| R-06 obligatoriedad | | | fila 4 = Validada; gestión Inclusion, estable v1/v2 |
| R-07 moneda | | | |
| R-08 sin autorización | | | |
| R-09 prioridad + motivos | | | |
| **R-10 referencia construida** | | | **contá los 20** |
| R-11 referencia imposible | | | |
| R-12 formato 06 sin derivar | | | |
| R-13 mezcla | | | |
| R-14 duplicados pasan | | | |
| R-15 DN-01 canon `plan` | | | |
| R-16 DN-01 modo `cliente` | | | |
| R-17 DN-01 valor basura degrada | | | **FALLA, bloqueado por D-59** (ya ABIERTA, pospuesta) |
| R-18 DN-03 `Exclusion` afloja tres campos | | | confirmado por el aprobador 2026-09-28; N-A si el entorno no lo tiene sembrado |
| R-19 compatibilidad sin `porGestion` | | | ex O-01 |
| R-20 `Exclusion` afloja los tres campos | | | ex O-02; confirmado por el aprobador 2026-09-28; N-A si el entorno no lo tiene sembrado |
| R-21 `Inclusion`/`Modificacion` sin cambios | | | ex O-03 |
| R-22 precedencia `obligatorios` | | | ex O-04; unitario, no sembrar |
| R-23 gestión inválida en la fila | | | ex O-05 |
| R-24 sin distinguir caja/tildes | | | ex O-06 |
| R-25 gestión desconocida en el parámetro | | | ex O-07; alto riesgo, preferir unitario |
| R-26 versionar agrega, no edita | | | ex O-08 |
| R-27 mensaje al cliente sin cambios | | | ex O-09 |
| R-28 regla sin cambio de código/orden/nivel | | | ex O-10 |
