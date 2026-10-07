# 03 — Reglas de nivel Registro

> **Nota del 2026-10-06 (DN-05, ensamblado 1.0.23.0, ya en Dev).** Las reglas de fila pasaron de ocho a once activas:
> `GESTION_DEL_FORMATO`, `CLASIFICACION_DEL_FORMATO` (reemplaza a `FORMATO_11_SOLO_ACH`, que quedó **inactiva**),
> `BANCO_DEL_FORMATO`, `CARACTERES_PERMITIDOS` y `CUENTA_DEL_BANCO`. `plantilla.obligatoriedad` v3 es la matriz del
> negocio (reemplaza el criterio de exclusión de la v2, DN-03). Donde este archivo dice "las ocho reglas" o habla de
> `FORMATO_11_SOLO_ACH` es historia de antes de DN-05: lo vigente son los casos R-05, R-10, R-12, R-18, R-20 y R-21
> reescritos y **R-29..R-38** nuevos. El diagrama de abajo es el de antes de DN-05, se deja como historia.

Set **R** (`CONVENCIONES.md` §4). Las reglas que deciden **fila por fila** (ocho hasta DN-03, once desde DN-05). Son el corazón del
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

**Qué prueba.** `CLASIFICACION_DEL_FORMATO` (DN-05; hasta el 2026-10-05 la misma regla era `FORMATO_11_SOLO_ACH`, DD-17,
y el texto del mensaje es el mismo).

**Necesita.** El REMITENTE; el adjunto `E05-formato11-no-ach.xlsx`; el plan `PR11`.

**Cómo.** Correo nuevo con **`E05-formato11-no-ach.xlsx`** (plan `PR11`, clasificaciones `BAC` y `CK`).

**Esperado.**

| Qué | Valor |
|---|---|
| Las 2 filas | **Rechazada en validación** |
| Mensaje | exactamente **"El plan PR11 solo admite la clasificación ACH."** |
| `REFERENCIA_FORMATO_11` en esas filas | no aplica: su dependencia falló |

**Automatizable.** API (`correr.py R`, caso R-05).

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

> **Nota DN-05 (2026-10-06).** Las 4 filas de `E06-obligatoriedad.xlsx` son **Inclusión en formato 11**. En la matriz v3 eso
> exige gestión, plan, nombre, tipo ID, identificación, cuenta y banco; **la moneda ya es opcional** y la referencia
> también. Las filas 1 a 3 siguen faltando de nombre, cuenta y tipo ID, así que el caso no cambia. Lo de arriba sobre
> "todo obligatorio salvo `referencia`" (v1/v2) es historia: ahora es "todo obligatorio salvo referencia y moneda, y
> menos todavía según gestión y formato" (R-18, R-21).

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

## R-10 · La referencia de formato 11 se construye sola (en la Inclusión)

**Qué prueba.** `REFERENCIA_FORMATO_11` con D-17 y DD-10, y que la recibida se guarde aparte. **Desde DN-05 (decisión 3)
la referencia se arma con banco + cuenta SOLO en Inclusión**; en Exclusión rige la recibida (R-33).

**Necesita.** El `REMITENTE` de este set (autorizado sobre `PR11`); el adjunto
`E01-feliz-formato11.xlsx` (este caso arma su propia Solicitud con `ctx.procesar()`, no depende de
otro set ni de otro caso — `CONVENCIONES.md` §8); que `plantilla.listas` traiga, en su CONTENIDO
(no solo que la fila exista), el banco `BAC` con código `007` — verificado en `preparar()`
(`preparacion_comun.verificar_bac_codigo_007()`, 2026-09-28): con un código distinto de `007` (aunque
también tenga 3 dígitos, como el `102` que documenta `diseno/PENDIENTES.md` sección B) la referencia
esperada abajo no coincidiría, y este caso daría FALLA por una razón de datos, no del sistema.

**Cómo.** Correo nuevo con **`E01-feliz-formato11.xlsx`**: **tres Inclusiones** en `PR11` (hasta el 2026-10-05 eran
Inclusión, Exclusión y Modificación; DN-05 lo cambió: el 11 no admite Modificación y la Exclusión exige referencia).

**Esperado.** Las 3 filas **Validada**, y por cada una:

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

**Automatizable.** API (`correr.py R`, caso R-10) — **igual conviene recalcular a mano los 20
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

> **Nota DN-05 (2026-10-06).** Estas dos cuentas ahora las rechazan **también** otras reglas: la de letras,
> `CARACTERES_PERMITIDOS` (la cuenta solo lleva dígitos); la de 18 dígitos, `LARGOS_Y_FORMATO` (máximo 17) y
> `CUENTA_DEL_BANCO` (BAC 9-16). No hay forma de aislar `REFERENCIA_FORMATO_11` con esos datos, así que el caso solo
> comprueba que las dos filas **queden rechazadas**, que es lo que protege a AS400.

**Automatizable.** API (`corredor.py`, caso R-11).

---

## R-12 · Formato 06: la referencia NO se deriva

**Qué prueba.** DD-15: en formatos 06 y 10 la referencia es **la recibida**, aunque venga vacía.

**Necesita.** El REMITENTE; el adjunto `E13-formato06-referencia-libre.xlsx`; el plan `PR06`.

**Cómo.** Correo nuevo con **`E13-formato06-referencia-libre.xlsx`** (plan `PR06`, referencia
`REFLIBRE000123`; hasta el 2026-10-05 llevaba guiones, `REF-LIBRE-000123`, y DN-05 los prohíbe: la referencia solo
admite letras y dígitos ASCII).

**Esperado.**

| Campo | Valor |
|---|---|
| Estado de la fila | **Validada** |
| `sanic_referencia` | **`REFLIBRE000123`** — tal cual, sin tocar |
| `sanic_referenciarecibida` | `REFLIBRE000123` |

**FALLA si** la referencia salió con `007` adelante o rellena con ceros: se estaría aplicando la
regla de formato 11 a un plan que no es 11.

**Automatizable.** API (`correr.py R`, caso R-12).

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
`E01-feliz-formato11.xlsx` (plan `PR11`, del Cliente compartido; tres Inclusiones desde DN-05).

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

## R-18 · DN-05: el criterio de `Exclusion` de la v3 (cuenta y referencia obligatorias, el resto opcional)

> **Reescrito el 2026-10-06 (DN-05, decisión 2).** Hasta el 2026-10-05 este caso probaba el criterio de la v2 (DN-03,
> confirmado el 2026-09-28): una exclusión podía omitir cuenta, banco y moneda. **La matriz del negocio lo reemplazó**:
> en la Exclusión la **cuenta y la referencia son obligatorias** y la identificación, el nombre, el tipo ID, la
> clasificación y el banco son opcionales. La moneda es opcional siempre.

**Qué prueba.** `plantilla.obligatoriedad` v3 aplicada a una Exclusión, en vivo, dentro del motor de reglas.

**Necesita.** La versión activa más alta de `plantilla.obligatoriedad` con `porGestion.Exclusion` (lo confirma
`preparar()`; si no está, el caso es `N-A` por precondición de entorno, no por duda sobre el criterio). El plan `PR06`
(formato 06, para no mezclar con `REFERENCIA_FORMATO_11`).

**Cómo.** Correo nuevo con un Excel ad hoc (`ZZ-R18-exclusion-aflojada.xlsx`, armado con `generar_excel.fila()` +
`ctx.excel_ad_hoc()`), tres filas `Exclusion` en `PR06`, todas con nombre, tipo ID, identificación, clasificación,
banco y moneda **vacíos**:

| Fila | Referencia | Cuenta | Esperado |
|---|---|---|---|
| 1 | `REFEXCL0006` | `10203040506070` | **Validada** |
| 2 | `REFEXCL0006` | vacía | **Rechazada**: *"Faltan datos obligatorios: No. Cuenta."* (solo la cuenta) |
| 3 | vacía | `10203040506070` | **Rechazada**: *"Faltan datos obligatorios: Referencia."* (solo la referencia) |

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py R` (caso `R-18`); a mano,
`ver_solicitud.py <MPPP-########>`.

**Cómo se sabe que falló.** La fila 1 se rechaza citando nombre, identificación, tipo ID, clasificación o banco: la
versión activa no es la v3 (puede seguir activa la v2 o una anterior) o el motor no aplica `porGestion.Exclusion`. Si
la fila 2 o la 3 se valida, la cuenta o la referencia dejaron de ser obligatorias en la exclusión.

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

- R-19, R-20 y R-26 leen lo YA sembrado (hoy solo la v3 activa; v1 y v2 existen inactivas), nunca lo cambian.
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

> **Nota del 2026-10-06 (DN-05).** Esta sección habla de la v2 como la versión vigente. Hoy la vigente es la **v3**
> (matriz del negocio), v1 y v2 están **inactivas** (DD-02: una sola versión activa; ver R-26). Lo que cambió en los casos: R-20 y R-21 se reescribieron contra la v3 (más
> abajo); R-19, R-22 a R-25, R-27 y R-28 siguen como están. Lo del párrafo anterior sobre "Exclusion afloja tres
> campos" (R-18, R-20) es historia de la v2.

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

## R-20 · El contenido sembrado de `plantilla.obligatoriedad` v3 es exactamente el de la matriz del negocio

> **Reescrito el 2026-10-06 (DN-05).** Hasta el 2026-10-05 verificaba que `porGestion.Exclusion` aflojara exactamente
> `{numeroCuenta, banco, moneda}` (v2). Esa es historia: la v3 lo reemplazó.

**Qué prueba.** Que la versión 3 de `plantilla.obligatoriedad` (`playbooks/datos/parametros.md`) tiene **exactamente**
el contenido que decidió el aprobador. **La prueba EN VIVO de que ese contenido funciona** es R-18, R-21 y R-32..R-34;
este caso es de solo lectura.

**Necesita.** La fila `sanic_version = 3` de `plantilla.obligatoriedad`. Si no existe (por ejemplo, todavía no se
sembró en Test o Producción), el caso reporta `N-A` por precondición de entorno.

**Cómo.** `GET` de solo lectura sobre `sanic_mppp_tbl_parametros` filtrando `sanic_version eq 3`, con el `sanic_valor`
parseado como JSON.

**Qué tiene que pasar.** La v3 está activa y su valor, como estructura JSON, es idéntico a:

| Parte | Contenido |
|---|---|
| `porDefecto` | `obligatorio` |
| `opcionales` (global) | `referencia`, `moneda` |
| `reglas` | Inclusion·06 afloja `clasificacion`, `tipoIdentificacion`, `banco`; Inclusion·10 afloja `banco`; Inclusion·11 afloja `clasificacion` (todas con `clasificacion: "*"`) |
| `porGestion.Exclusion` | opcionales: `clasificacion`, `nombreBeneficiario`, `tipoIdentificacion`, `numeroIdentificacion`, `banco`; obligatorios: `referencia` |
| `porGestion.Modificacion` | lo mismo que Exclusion |

Además el caso verifica, campo por campo (nombrados, no contados, CONVENCIONES §7), lo que decidió el aprobador para
`Exclusion` y `Modificacion`, para que un cambio diga **qué** campo se movió.

**Cómo se verifica.** La respuesta de esa consulta `GET`, ya parseada.

**Cómo se sabe que falló.** Cualquier diferencia con la tabla de arriba: el parámetro sembrado no es el que el
aprobador pidió, y R-18/R-21 (que mandan Excel reales) podrían estar pasando por otra razón.

**Automatizable.** API de solo lectura.

---

## R-21 · DN-05: `Inclusion` tiene sus `reglas` por formato y `Modificacion` su entrada en `porGestion`

> **Reescrito el 2026-10-06 (DN-05).** Hasta el 2026-10-05 probaba lo contrario: que `Inclusion` y `Modificacion`
> **no** tenían entrada y seguían obligando cuenta, banco y moneda. Con la v3 ambas tienen la suya.

**Qué prueba.** (a) De solo lectura: la versión activa trae `reglas` para Inclusión en los formatos 06, 10 y 11 y
`porGestion` para Exclusión y Modificación (y ninguna Inclusión ahí). (b) En vivo: lo que esas entradas dejan
obligatorio y opcional.

**Necesita.** La versión activa más alta de `plantilla.obligatoriedad` (la v3). Los planes `PR06`, `PR10`, `PR11`.

**Cómo.** Correo nuevo con un Excel ad hoc (`ZZ-R21-inclusion-modificacion-sin-aflojar.xlsx`), cinco filas:

| Fila | Qué trae | Esperado |
|---|---|---|
| 1 | Inclusión `PR06` sin clasificación, tipo ID, banco ni moneda | **Validada** |
| 2 | Inclusión `PR10` sin clasificación (el formato admite dos, no se completa) | **Rechazada**: *"Faltan datos obligatorios: Clasificación."* |
| 3 | Inclusión `PR11` sin banco | **Rechazada**: *"Faltan datos obligatorios: Banco."* |
| 4 | Modificación `PR06` con solo plan, referencia `REFMODI0001` y cuenta | **Validada** |
| 5 | Modificación `PR06` sin referencia | **Rechazada**: *"Faltan datos obligatorios: Referencia."* |

**Cómo se verifica.** `correr.py R` (caso `R-21`) y `ver_solicitud.py` sobre la Solicitud creada.

**Cómo se sabe que falló.** Las filas 1 o 4 se rechazan (la entrada de la v3 no se aplica), las 2, 3 o 5 se validan
(un campo que debía ser obligatorio quedó opcional) o el mensaje nombra más campos que el esperado.

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

## R-26 · Versionar agrega una fila y desactiva la anterior: UNA sola versión activa por parámetro

> **Nota del 2026-10-06 (orden del aprobador).** La versión anterior de este caso ("Versionar es AGREGAR una fila,
> nunca editar la vigente") exigía que la v1 y la v2 de `plantilla.obligatoriedad` siguieran ACTIVAS. Eso contradecía
> DD-02 (`diseno/02-diccionario-datos.md` §2.5, `sanic_version`: "Cambiar = crear una versión nueva y desactivar la
> anterior"). La v1 se reactivó por error en Dev el 2026-10-06 para satisfacer ese check, y se volvió a desactivar el
> mismo día junto con la v2 de `plantilla.obligatoriedad` y la v1 de `plantilla.estructura`, que habían quedado
> duplicadas activas porque `parametro.py` agrega la versión nueva sin desactivar la anterior. El caso se reescribió
> abajo. El historial de la versión anterior (y su FALLA del 2026-09-28) queda en git.

**Qué prueba.** DD-02/la nota de `playbooks/datos/parametros.md`: cambiar una regla de negocio siembra
`sanic_version` más alto como fila NUEVA y **desactiva** la anterior; nunca se edita `sanic_valor`. Resultado: cada
parámetro tiene exactamente UNA versión activa, la más alta, y las viejas quedan inactivas e intactas como historia.

**Necesita.** Nada que crear: alcanza con leer lo ya sembrado.

**Cómo.** `GET sanic_mppp_tbl_parametros?$select=sanic_nombre,sanic_version,sanic_valor,statecode&$orderby=sanic_nombre
asc,sanic_version asc` (toda la tabla, sin ningún `POST`/`PATCH`).

**Qué tiene que pasar.**
1. `plantilla.obligatoriedad` tiene las versiones 1, 2 y 3, y SOLO la 3 está activa.
2. La v1 conserva EXACTO `{"porDefecto":"obligatorio","opcionales":[{"campo":"referencia"}]}` y la v2 conserva EXACTO
   `{"porDefecto":"obligatorio","opcionales":[{"campo":"referencia"}],"porGestion":{"Exclusion":{"opcionales":[{"campo":"numeroCuenta"},{"campo":"banco"},{"campo":"moneda"}]}}}`
   (comparación como JSON, no como texto).
3. En TODA la tabla, cada nombre tiene exactamente UNA versión activa (si no, FALLA nombrando el parámetro). No se
   exige que sea la más alta en toda la tabla: los sets R y U siembran versiones temporales de `autorizacion.modo` (y
   antes de `rpa.puedeaprobar`) y las desactivan al terminar, así que en esos parámetros de arnés la activa legítima
   es la v1. Para `plantilla.obligatoriedad`, versionado por negocio, sí se exige que la activa sea la más alta (punto 1).

**Cómo se verifica.** La respuesta de esa consulta.

**Cómo se sabe que falló.** Una versión vieja sigue activa (duplicada), la activa de `plantilla.obligatoriedad` no es la
más alta, falta una de las
versiones 1/2/3 de `plantilla.obligatoriedad`, o el `sanic_valor` de la v1/v2 cambió (alguien editó en vez de agregar
una versión: rompería `sanic_versionparametros` de Solicitudes ya procesadas).

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

## R-29 a R-38 — DN-05 (2026-10-06): un caso por comportamiento nuevo

Cada caso arma su propio Excel ad hoc (`ZZ-R29..R38-*.xlsx`) y su propia Solicitud con `ctx.excel_ad_hoc()` y
`ctx.procesar()`, con el REMITENTE de este set. Cada fila falla **solo por la regla que prueba**, o por ninguna. Los
mensajes salen de `ReglasDeRegistro.cs`; el nombre de los campos, de los encabezados que lee `preparar()`.

**Necesitan, todos.** Lo que ya deja listo `preparar()`: el REMITENTE autorizado sobre `PR06`/`PR10`/`PR11` (y **sin**
autorización sobre `PRNA`), `plantilla.estructura`, `plantilla.listas` y los parámetros `plan.formatos`,
`cuenta.largos` y `plantilla.obligatoriedad` v3 activos, más las reglas de DN-05 activas en el catálogo.

| Caso | Qué prueba | Excel | Esperado |
|---|---|---|---|
| **R-29** | Escenario 9: ACH en `PR06` | `ZZ-R29-ach-en-formato06.xlsx` | fila **Rechazada**: *"El plan PR06 solo admite la clasificación BAC."* |
| **R-30** | Escenario 10: CK en `PR11` y CK en `PR06` | `ZZ-R30-ck-en-formato11-y-06.xlsx` | 2 filas **Rechazadas**: *"El plan PR11 solo admite la clasificación ACH."* y *"El plan PR06 solo admite la clasificación BAC."* |
| **R-31** | Clasificación y banco vacíos en `PR06` se completan y se **guardan**; en `PR10` solo el banco | `ZZ-R31-clasificacion-y-banco-vacios.xlsx` | 2 filas **Validada**; fila 1: `sanic_clasificacion = BAC` y `sanic_banco = BAC`; fila 2 (CK recibida): clasificación CK (no se pisa) y `sanic_banco = BAC` |
| **R-32** | Modificación en `PR11` (`GESTION_DEL_FORMATO`) | `ZZ-R32-modificacion-en-formato11.xlsx` | fila **Rechazada**: *"El plan PR11 no admite la gestión Modificacion."* y nada más |
| **R-33** | Exclusión en `PR11`, referencia `REFEXCL0001`, sin banco | `ZZ-R33-exclusion-formato11.xlsx` | fila **Validada**; `sanic_referencia` = `sanic_referenciarecibida` = `REFEXCL0001` (no se arma con banco + cuenta) |
| **R-34** | Exclusión en `PR11` sin referencia | `ZZ-R34-exclusion-sin-referencia.xlsx` | fila **Rechazada**: *"Faltan datos obligatorios: Referencia."* |
| **R-35** | Escenario 21: caracteres no permitidos | `ZZ-R35-caracteres.xlsx` | 5 filas **Rechazadas** con el mensaje exacto (abajo); el mensaje **no** contiene el valor completo |
| **R-36** | Escenario 20: `PR06`, banco vacío (BAC 9-16), cuenta de 8 dígitos | `ZZ-R36-cuenta-corta-formato06.xlsx` | fila **Rechazada**: *"El largo de la cuenta no corresponde al banco BAC."*, sin el largo ni la cuenta |
| **R-37** | Escenario 20: `PR11` con LAFISE y 9 dígitos, y con ATLANTIDA y 17 dígitos | `ZZ-R37-cuenta-por-banco-formato11.xlsx` | 2 filas **Validada** (LAFISE 9-16; ATLANTIDA usa el `porDefecto` 6-17), referencias de 20 caracteres |
| **R-38** | Plan ajeno `PRNA`: clasificación y banco que su formato no admitiría, Modificación, cuenta corta | `ZZ-R38-plan-ajeno-sin-fuga.xlsx` | 2 filas **Sin autorización**; el mensaje nombra `PRNA` y **ninguna** regla por formato habla (ni "solo admite", ni "no admite", ni "no corresponde al banco") |

**R-35, los cinco mensajes** (el encabezado sale de `plantilla.estructura`; los caracteres, uno por prohibido, en orden
de aparición, sin repetir):

| Fila | Dato | Mensaje |
|---|---|---|
| 1 | nombre `Nuñez Prueba` | *"Caracteres no permitidos: Nombre Colaborador / Proveedor (ñ)."* |
| 2 | nombre `Jose Ramírez` | *"… Nombre Colaborador / Proveedor (í)."* |
| 3 | referencia `REF-0001` | *"… Referencia (-)."* |
| 4 | nombre `Peña Dos` y referencia `REF-0002` | *"… Nombre Colaborador / Proveedor (ñ); Referencia (-)."* |
| 5 | nombre `Prueba 123` | *"… Nombre Colaborador / Proveedor (1 2 3)."* |

(Los encabezados exactos son los de la plantilla del cliente; el caso los lee, no los supone.)

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py R`, casos `R-29` a `R-38`; a mano, `ver_solicitud.py`
sobre la Solicitud de cada caso.

**Cómo se sabe que falló.** Un mensaje distinto del esperado (aunque el estado sea el correcto: lo lee un cliente del
banco, `CONVENCIONES.md` §3); una fila que se valida sin que `sanic_clasificacion`/`sanic_banco` queden completados
(R-31: la regla valida pero no guarda); en R-38, **cualquier** frase de una regla por formato en el mensaje: sería una
fuga del formato de un plan sobre el que el remitente no tiene autorización.

**Automatizable.** API (`correr.py R`).

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

> **Nota del 2026-10-06 (DN-05).** El punto 3 de arriba habla de DN-03 y de R-18 como "`Exclusion` afloja cuenta, banco y
> moneda": **DN-05 lo reemplazó** (la matriz del negocio, `plantilla.obligatoriedad` v3; ver R-18, R-20 y R-21
> reescritos). DN-05 toca este nivel con cinco reglas nuevas de fila, cubiertas por R-29..R-38. Se deja el texto de
> DN-03 como historia.

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
| R-05 formato 11 no ACH | | | `CLASIFICACION_DEL_FORMATO`, mensaje exacto (DN-05) |
| R-06 obligatoriedad | | | fila 4 = Validada; Inclusión en formato 11 |
| R-07 moneda | | | |
| R-08 sin autorización | | | |
| R-09 prioridad + motivos | | | |
| **R-10 referencia construida** | | | **contá los 20**; E01 = tres Inclusiones (DN-05) |
| R-11 referencia imposible | | | |
| R-12 formato 06 sin derivar | | | referencia `REFLIBRE000123` (sin guiones, DN-05) |
| R-13 mezcla | | | |
| R-14 duplicados pasan | | | |
| R-15 DN-01 canon `plan` | | | |
| R-16 DN-01 modo `cliente` | | | |
| R-17 DN-01 valor basura degrada | | | D-59 cerrada el 2026-09-28; OK en la corrida del 2026-10-06 (la nota de R-17 que dice "FALLA hoy" es historia) |
| R-18 DN-05 criterio de `Exclusion` de la v3 | | | reescrito 2026-10-06; N-A si el entorno no lo tiene sembrado |
| R-19 compatibilidad sin `porGestion` | | | ex O-01 |
| R-20 contenido exacto de la v3 | | | ex O-02; reescrito 2026-10-06; N-A si el entorno no tiene la v3 |
| R-21 `Inclusion` por formato y `Modificacion` en `porGestion` | | | ex O-03; reescrito 2026-10-06 |
| R-22 precedencia `obligatorios` | | | ex O-04; unitario, no sembrar |
| R-23 gestión inválida en la fila | | | ex O-05 |
| R-24 sin distinguir caja/tildes | | | ex O-06 |
| R-25 gestión desconocida en el parámetro | | | ex O-07; alto riesgo, preferir unitario |
| R-26 versionar agrega, no edita | | | ex O-08; reescrito el 2026-10-06 contra DD-02 (una sola versión activa por parámetro) |
| R-27 mensaje al cliente sin cambios | | | ex O-09 |
| R-28 regla sin cambio de código/orden/nivel | | | ex O-10 |
| R-29 ACH en plan 06 | | | DN-05, escenario 9 |
| R-30 CK en plan 11 y en plan 06 | | | DN-05, escenario 10 |
| R-31 clasificación y banco vacíos se guardan | | | DN-05 |
| R-32 Modificación en plan 11 | | | DN-05 |
| R-33 Exclusión en plan 11, referencia recibida | | | DN-05 |
| R-34 Exclusión sin referencia | | | DN-05 |
| R-35 caracteres no permitidos | | | DN-05, escenario 21; el mensaje no trae el valor |
| R-36 cuenta de 8 dígitos en plan 06 | | | DN-05, escenario 20 |
| R-37 LAFISE 9 y ATLANTIDA 17 dígitos válidos | | | DN-05, escenario 20 |
| R-38 plan ajeno sin fuga | | | DN-05 |

**Corrida del 2026-10-06 contra Dev (ensamblado 1.0.23.0, después de reescribir el set):** 37 OK · 1 FALLA · 0 N-A.
Después de reescribir R-26 contra DD-02 y desactivar los duplicados de Dev: 37 OK · 1 FALLA · 0 N-A. La FALLA era R-26, por
`autorizacion.modo` y `rpa.puedeaprobar` (activa la v1, la más alta inactiva). No es un defecto de datos: son versiones
temporales que siembran y desactivan los propios sets R y U. Se ajustó el punto 3 del caso a "exactamente una activa por
nombre" (coordinador, 2026-10-06). Corrida posterior: **38 OK · 0 FALLA · 0 N-A**. La corrida anterior, antes de reescribir el set, daba 20 OK · 7 FALLA ·
1 N-A: R-10, R-12, R-16, R-17, R-18 y R-20 por fixtures y aserciones de antes de DN-05, R-21 N-A por lo mismo, y R-26.
