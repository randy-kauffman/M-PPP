# 07 — MPPP-VIG, la red de seguridad

`MPPP-VIG` corre **cada 10 minutos** y recupera lo que quedó a medias. Hace lo mismo que `MPPP-REC`
y `MPPP-COM` cuando algo falló, y por eso esos dos no tienen lógica propia (DF-02).

Migrado a `sets/set_v.py` el 2026-09-27/28 (CONVENCIONES §8). El set es autocontenido: no depende de
que otro set haya corrido antes.

## El recurrence real, y de dónde sale el margen

**10 minutos**, confirmado dos veces el 2026-09-27/28, no supuesto:

- En el repo, `recursos/flujos/mppp-vig.json`: `"recurrence": { "frequency": "Minute", "interval": 10 }`.
- **En vivo**, leyendo `workflows?$select=clientdata&$filter=name eq 'Cloud Flow - MPPP - VIG - ...'`
  por Web API: el `clientdata` desplegado trae el mismo `recurrence` — lo que corre en el entorno es
  lo que hay en el repo. Esto lo verifica **V-01** en cada corrida, no una sola vez a mano.

**Margen de espera de los casos lentos: 3 vueltas completas del recurrence (30 minutos) + 2 minutos
de colchón = 32 minutos por caso.** No es un número arbitrario:

- El set M ya probó 1 vuelta (~11 min) y 2 vueltas (~22 min) para su caso M-06 (envío iniciado sin
  confirmar) y **ninguna de las dos vio actuar a MPPP-VIG**, sin que eso signifique que el bloque
  esté roto (ver la sección siguiente: hay Solicitudes reales marcadas por ese mismo bloque, la más
  reciente a horas de este análisis).
- Investigando por qué, este set encontró un **defecto real y confirmado** (V-01 lo prueba, sin
  esperar nada): la etapa de arranque común de los 5 bloques (los 4 `Parametro_*` + `Umbrales` + el
  propio bloque 1) está encadenada solo con `runAfter: ["Succeeded"]`, no con
  `["Succeeded", "Failed"]` como sí están encadenados los 5 bloques entre sí. Si esa etapa falla en
  una corrida puntual (hay evidencia en vivo de que sí falla: decenas de eventos `Error` recientes
  en la Bitácora con origen `MPPP-VIG`), **los cinco bloques de recuperación se saltean enteros esa
  vez** — no perdés uno, los perdés todos. Con 1 o 2 vueltas de margen, tener la mala suerte de caer
  en una o dos corridas rotas seguidas no es imposible.
- 3 vueltas le da al caso una tercera oportunidad más allá de lo que ya se probó y falló, sin llegar
  a los ~40+ minutos por caso que hacen un set inusable (CONVENCIONES, pedido de este set).
- Si con 3 vueltas también da FALLA de forma sistemática, **no es cuestión de agrandar el margen**:
  es evidencia de que el defecto de arranque (o algo peor) es más frecuente de lo esperado, y hay que
  mirar `diseno/PENDIENTES.md`, no este número.

## D-62 corregido (2026-09-28) — la etapa de arranque ya blinda con Failed

**El defecto que describe esta sección ya se corrigió.** `recursos/flujos/mppp-vig.json` y
`playbooks/flujo/mppp-vig.md` (razonamiento completo del alcance ahí) quedaron con `"Failed"`
agregado al `runAfter` de los cinco eslabones de la etapa de arranque, desplegado contra el
entorno con `herramientas/construir/flujo.py playbooks/flujo/mppp-vig.md --completar` y
verificado leyendo `workflows?$select=clientdata` (no el repo) el 2026-09-28: `statecode=1`
(activo), `recurrence` intacto (10 minutos), los cinco eslabones de arranque Y los cuatro entre
bloques con `["Succeeded","Failed"]`, y repo == desplegado en `runAfter`/`type` de cada acción de
nivel superior. De paso se agregó `runtimeConfiguration.concurrency.runs = 1` al trigger
(parte (b) de D-62, ver la sección de concurrencia más abajo), también verificado desplegado.

El texto que sigue (el hallazgo original, el razonamiento de M-06, las tablas de V-01) se conserva
tal cual se escribió durante la investigación — es la evidencia de POR QUÉ el arreglo es el que
es. **V-01 pasó de estar en rojo a propósito a verificar que el blindaje sigue puesto**; ver su
sección más abajo para la tabla actualizada y el síntoma que delataría un revert.

## La pregunta que dejó abierta M-06 (set M, `04-comunicaciones.md`)

M-06 preparó una Solicitud con `sanic_fechaacuseiniciado` de hace 40 minutos y
`sanic_fechaacuseenviado` vacío (un envío iniciado y nunca confirmado) y esperó hasta ~22 minutos sin
ver `sanic_requiererevision` pasar a `true`. Dos hipótesis quedaron abiertas: **(A)** un defecto en la
consulta del bloque `Envios_iniciados_sin_confirmar`, o **(B)** que el flujo se corta antes de llegar
a ese bloque por los errores del bloque anterior.

**Ninguna de las dos es correcta tal como estaba planteada.** Con evidencia real del entorno
(2026-09-27/28):

1. **(A) descartada con evidencia POSITIVA.** Hay Solicitudes reales, ya marcadas por este bloque
   exacto (`sanic_motivorevision = "Envio iniciado sin confirmar: no se reenvia para que el cliente
   no reciba dos veces lo mismo."`, el texto fijo de `Marcar_para_revision` en
   `recursos/flujos/mppp-vig.json`), con timestamps que van desde hace pocas horas hasta varios días
   atrás. La consulta funciona: si no funcionara, esas filas no existirían. **V-07** (nuevo, ver
   abajo) prueba esto mismo sin esperar: arma el escenario y corre LA MISMA consulta ($filter) que
   usa el bloque, contra la Solicitud recién creada, antes de esperar nada — si algún día esto deja
   de coincidir, el caso lo dice de inmediato, sin gastar 30 minutos para enterarse.
2. **(B) descartada tal como estaba planteada, mejor explicada.** El encadenamiento **entre los 5
   bloques** (`Correos_que_quedaron_en_la_bandeja` → `Solicitudes_sin_validar` →
   `Comunicaciones_sin_enviar` → `Envios_iniciados_sin_confirmar` → `Por_clasificar_vencidos`) SÍ usa
   `runAfter: ["Succeeded", "Failed"]` en los cuatro eslabones, tanto en el repo como en el
   `clientdata` desplegado (idénticos, verificado por **V-01**): un bloque que falla NO le impide
   correr al siguiente. Eso es justo lo que promete `playbooks/flujo/mppp-vig.md`. La cascada no pasa
   ahí.
3. **La causa más probable, con evidencia de apoyo:** la etapa de **arranque común** (los 4
   `Parametro_*` + `Umbrales` + el bloque 1), que corre ANTES de los 5 bloques nombrados en el punto
   2, sí está encadenada solo con `["Succeeded"]` (ver arriba). Si un `Parametro_*` falla — plausible
   bajo la carga real de 3-4 agentes de prueba corriendo en paralelo contra el mismo entorno, como
   pasaba cuando M-06 esperaba —, **todos** los bloques (incluido el 4) quedan `Skipped` esa corrida,
   no `Failed`. Esto explica por qué el bloque funciona en general (punto 1) pero M-06 pudo no verlo
   actuar en una ventana puntual de ~22 minutos: no hizo falta que el bloque 4 fallara, alcanzó con
   que la etapa de arranque fallara una o dos veces seguidas en esa ventana. Hay más evidencia
   circunstancial: en la Bitácora aparecen decenas de eventos `Error` con origen `MPPP-VIG` y el
   texto `"Fallo la ingesta del correo"` (generados por `MPPP-ING` cuando lo llama el bloque 1),
   agrupados en ventanas de 2-3 minutos — más seguido de lo que un recurrence de 10 minutos
   explicaría solo, lo que sugiere además corridas superpuestas de `MPPP-VIG` (su trigger no tiene
   control de concurrencia configurado, y el bloque 1 puede tardar bastante si el backlog de correos
   viejos es grande y cada reintento a `MPPP-ING` agota su política de reintentos).
4. **Hallazgo aparte, sobre el propio caso M-06 (no sobre el flujo):** ningún bloque de `MPPP-VIG`
   escribe un evento de Bitácora `Requiere revision` (159460016) cuando marca una Solicitud para
   revisión — ni el bloque 2 (reintentos agotados) ni el bloque 4. Ese evento sólo lo genera
   `MPPP-ENV` (`recursos/flujos/mppp-env.json`), en otro escenario. Esto es consistente con **V-04**
   (el caso análogo de este mismo archivo, bloque 2): su tabla de resultados esperados nunca pidió
   una entrada de Bitácora, solo los campos de la Solicitud. La aserción de M-06 sobre la Bitácora
   probaba algo que `MPPP-VIG` nunca prometió — es una expectativa mal fundada del propio caso M-06,
   no un defecto de `MPPP-VIG`. **V-07** no repite ese error.

**Para quien registre esto en `diseno/PENDIENTES.md`:** el defecto verificable y accionable del punto 3
(el `runAfter` sin `Failed`) **ya se corrigió el 2026-09-28** — ver la sección "D-62 corregido" arriba.
La `Concurrency Control` en el trigger recurrente **también se aplicó** (`runs: 1`), para evitar
corridas superpuestas. Lo que sigue abierto, sin tocar, es la causa de fondo: por qué
`"Fallo la ingesta del correo"` es tan frecuente. No se toca el flujo desde este set — es de quien lo
diseñó.

## Investigación de la causa de fondo de "Fallo la ingesta del correo" (2026-09-28, SIN corregir)

Parte (c) de D-62: investigar, no arreglar. Evidencia recolectada el 2026-09-28 contra el entorno,
por consulta directa a la Bitácora (`sanic_mppp_tbl_bitacoras`), sin esperar ningún schedule:

- **898 eventos `Error` (159460017) con origen MPPP-VIG (159460005)** en las 2 horas previas a
  ~06:26 UTC del 2026-09-28 (`$filter=sanic_evento eq 159460017 and sanic_origen eq 159460005 and
  sanic_fechaevento ge <hace 2h>&$count=true`). Es MUCHO más que "decenas": es un incidente activo,
  no un ruido de fondo ocasional.
- **Ritmo continuo, no en ráfagas de 10 minutos.** Entre 06:15:06 y 06:26:42 UTC se registraron 76
  eventos, a un ritmo de ~1 cada 8-9 segundos, DISTRIBUIDOS a lo largo de toda la ventana — no
  agrupados en una sola ráfaga de hasta 25 (el tope de `Leer_la_bandeja`) cada 10 minutos. Una sola
  corrida secuencial de `MPPP-VIG` (con `concurrency.repetitions: 1` en su `Foreach` interno) no
  puede producir más de 25 fallos cada 10 minutos (~2,5/min) si procesa un solo lote; el ritmo medido
  (~7/min) triplica ese máximo teórico. Esto es consistente con **corridas superpuestas de
  `MPPP-VIG`** (el hallazgo de la parte (b) de este mismo D-62, sin Concurrency Control hasta este
  arreglo) reintentando el mismo backlog en simultáneo, no con una sola corrida bien comportada.
- **0% de éxito en la misma ventana.** En las mismas 2 horas, **cero** eventos `Ingresada` (159460001)
  con origen MPPP-VIG (`Bitacora_ingresada` de `mppp-ing.json`, que se escribe SOLO si la ingesta
  llega al final). Ningún intento de reingesta vía VIG terminó bien en esa ventana.
- **No es un problema general de Outlook/Graph.** En la misma ventana, **cero** eventos `Error` con
  origen MPPP-REC (159460001): la ingesta normal (correo nuevo, disparada por `MPPP-REC`) no muestra
  el mismo síntoma — de hecho no hubo ninguna Solicitud nueva creada por ningún origen en esa ventana
  (entorno de prueba, sin tráfico real). El problema es específico de la RE-ingesta que dispara VIG
  sobre correos que ya estaban en la bandeja, no de leer un correo nuevo.
- **La Bitácora no alcanza para saber QUÉ acción de `MPPP-ING` falla.** El propio texto que graba
  `Si_algo_fallo/Bitacora_error` en `recursos/flujos/mppp-vig.json` lo dice: *"El detalle de que accion
  fallo esta en el historial de ejecuciones del flujo, no aca"*. Ese historial de ejecuciones vive en
  la API de administración de Power Automate (`service.flow.microsoft.com`), no en el Web API de
  Dataverse — `herramientas/dataverse_api.py` solo tiene un token para el recurso de Dataverse, no
  para ese scope, y este set no tiene credenciales para leerlo. Tampoco hay acceso directo al buzón de
  ingesta (API de Outlook/Graph) desde acá. **Por eso lo que sigue es una hipótesis con evidencia
  circunstancial, no un diagnóstico confirmado — no se afirma como hecho.**

**Hipótesis más consistente con la evidencia (a confirmar por quien tenga acceso al historial de
ejecuciones o al buzón):** IDs de mensaje de Outlook que dejan de ser válidos entre que
`Correos_que_quedaron_en_la_bandeja` los lee (`GetEmailsV3`) y que `Reingerir`/`MPPP-ING` los vuelve a
leer (`GetEmailV2` por `messageId`). Si el tenant no tiene habilitado "Immutable Id" para el conector
de Outlook/Graph, mover un mensaje de carpeta (`MoveV2`, el último paso de `MPPP-ING` antes de
clasificar) **cambia su ID** salvo que se pida explícitamente el ID inmutable. Con corridas
superpuestas de VIG (parte (b), antes de este arreglo) leyendo la MISMA bandeja casi al mismo tiempo,
es plausible que una corrida ya haya movido un correo (cambiándole el ID) cuando otra corrida, que lo
leyó un instante antes con el ID viejo, intenta `Leer_el_correo` con ese ID y reciba 404 — lo que
haría fallar `Ingerir` completo y dispararía exactamente este evento. **Esto explicaría por qué el
ritmo bajó por primera vez a analizarse recién después de aplicar el Concurrency Control** (parte
(b): si las corridas dejan de superponerse, dejan de pisarse los IDs), pero **no explica un backlog
que nunca se drena si además hay mensajes genuinamente rotos** (adjunto corrupto, remitente con
formato inválido, etc.) — ambas causas pueden convivir. **Reportado, no corregido**: no es un defecto
de `mppp-vig.json` (que ya hace lo que puede: reintenta y, con el arreglo de D-62, ya no se apaga
entero cuando algo falla), sino de `mppp-ing.json` y/o de la configuración del conector de Outlook, o
de mensajes concretos en la bandeja — **merece su propio ID en `diseno/PENDIENTES.md`**, a criterio
de quien lo diseñó.

## Qué bloque prueba cada caso (nombrado, no contado)

| Bloque (`recursos/flujos/mppp-vig.json`) | Umbral | Caso |
|---|---|---|
| `Correos_que_quedaron_en_la_bandeja` | `vigilancia.minutos.sinvalidar` | V-02 |
| `Solicitudes_sin_validar` (reintento) | `vigilancia.minutos.sinvalidar` + `vigilancia.reintentos.maximo` | V-03 |
| `Solicitudes_sin_validar` (reintentos agotados) | ídem | V-04 |
| `Comunicaciones_sin_enviar` | `vigilancia.minutos.sinresponder` | V-05 |
| `Por_clasificar_vencidos` | `clasificacion.dias.vencimiento` | V-06 |
| `Envios_iniciados_sin_confirmar` | `vigilancia.minutos.sinresponder` | **V-07 (nuevo)** |

**V-07 se agrega ahora, al final** (CONVENCIONES §4: un ID asignado no se renumera ni se reusa;
`V-01` a `V-06` ya están citados por letra y número en `herramientas/pruebas/corredor.py` y
`pruebas/PLAN-COMPLETO.md`). El bloque `Envios_iniciados_sin_confirmar` nunca tuvo un caso propio en
este archivo — el hueco que dejó abierto M-06 no era solo una duda, era una ausencia real de
cobertura.

## Qué corre por default y qué no

Cinco de los siete casos (V-03, V-04, V-05, V-06, V-07) dependen del schedule real de `MPPP-VIG`: con
el margen de 32 minutos cada uno, los cinco juntos superan las **2 horas y media**. Un set así **no lo
corre nadie de rutina** (mismo criterio que el set E con E-08). Por eso:

- **V-01 y V-02 corren siempre.** V-01 es 100% API, sin esperar nada (activo, recurrence real,
  encadenamiento de los 5 bloques, el hallazgo de la etapa de arranque, comparación repo↔desplegado,
  y los 4 parámetros nombrados). V-02 es manual por una razón distinta (ver su sección): siempre
  queda `N-A` con el motivo.
- **V-03, V-04, V-05, V-06 y V-07 quedan `N-A` por default**, con el comando exacto para correrlos a
  propósito:

  ```bash
  MPPP_V_LENTOS=si python3 herramientas/pruebas/correr.py V
  ```

  Se corren así cuando `mppp-vig.json` cambió de forma que pudiera afectar alguno de los 5 bloques,
  avisando al equipo y fuera de horario de otras pruebas (comparten el mismo entorno con S/I/M/etc.).

---

## V-01 · El flujo está vivo y bien armado (sin esperar el schedule)

**Qué prueba.** Que `MPPP-VIG` esté activo, que su recurrence real sea la que se documenta acá, que
los 5 bloques existan y estén encadenados de forma que uno que falle no tumbe a los que siguen, que
la etapa de arranque tenga el mismo blindaje (D-62, corregido el 2026-09-28 — este caso pasó de dar
FALLA a propósito a verificar que el blindaje sigue puesto), y que el trigger tenga Concurrency
Control en 1 (parte (b) de D-62).

**Necesita.** Nada de negocio: solo lectura de la definición del flujo (`workflows.clientdata`, Web
API) y de `recursos/flujos/mppp-vig.json` (solo lectura, nunca se edita desde este set).

**Cómo.** `GET workflows?$select=name,statecode,clientdata&$filter=name eq 'Cloud Flow - MPPP - VIG
- Vigilar pendientes'`, se parsea `clientdata` (JSON) y se compara contra el JSON del repo y contra
lo documentado acá.

**Qué tiene que pasar.**

| Qué | Valor esperado |
|---|---|
| `statecode` | `1` (Activado) |
| `triggers.Cada_10_minutos.recurrence` | `frequency=Minute`, `interval=10` |
| Los 5 bloques (nombrados) | existen como acciones de nivel superior |
| `Solicitudes_sin_validar` depende de `Correos_que_quedaron_en_la_bandeja` | con `Succeeded` y `Failed` |
| `Comunicaciones_sin_enviar` depende de `Solicitudes_sin_validar` | con `Succeeded` y `Failed` |
| `Envios_iniciados_sin_confirmar` depende de `Comunicaciones_sin_enviar` | con `Succeeded` y `Failed` |
| `Por_clasificar_vencidos` depende de `Envios_iniciados_sin_confirmar` | con `Succeeded` y `Failed` |
| `Parametro_sin_responder` depende de `Parametro_sin_validar` | con `Succeeded` y `Failed` (D-62) |
| `Parametro_reintentos` depende de `Parametro_sin_responder` | ídem (D-62) |
| `Parametro_vencimiento` depende de `Parametro_reintentos` | ídem (D-62) |
| `Umbrales` depende de `Parametro_vencimiento` | ídem (D-62) |
| `Correos_que_quedaron_en_la_bandeja` depende de `Umbrales` | ídem (D-62) |
| `triggers.Cada_10_minutos.runtimeConfiguration.concurrency.runs` | `1` (D-62, parte b) |
| Lo desplegado == el repo | mismo `runAfter` y `type` en cada acción de nivel superior |
| Los 4 parámetros (`vigilancia.minutos.sinvalidar`, `vigilancia.minutos.sinresponder`, `vigilancia.reintentos.maximo`, `clasificacion.dias.vencimiento`) | cada uno con una versión activa |

**D-62, corregido el 2026-09-28 (histórico: así estaba antes).** Hasta esa fecha, los cinco checks de
la "etapa de arranque" de la tabla de arriba (`Parametro_sin_responder`...`Correos_que_quedaron_en_la_bandeja`)
daban **FALLA a propósito** (CONVENCIONES §3, "un defecto conocido no es N-A"): la etapa de arranque
común de los 5 bloques estaba encadenada solo con `["Succeeded"]`, así que un `Parametro_*` que
fallaba saltaba los CINCO bloques de recuperación esa corrida. Se corrigió agregando `"Failed"` a esos
cinco `runAfter` (igual que ya tenían los 5 bloques entre sí) y agregando Concurrency Control (`runs:
1`) al trigger — razonamiento completo del alcance en `playbooks/flujo/mppp-vig.md`, sección
"D-62 — blindaje de la etapa de arranque". Este caso ahora verifica que ese blindaje **sigue puesto**,
no que esté roto.

**Cómo se verifica.** `GET workflows?$select=name,statecode,clientdata&$filter=name eq 'Cloud Flow -
MPPP - VIG - Vigilar pendientes'`, parseando `clientdata.properties.definition` (o `clientdata`
directo si no trae `properties`), comparado campo a campo contra
`recursos/flujos/mppp-vig.json` (léido del disco, nunca escrito).

**Cómo se sabe que falló.** Si `statecode != 1` o el recurrence no es `10` minutos, es un cambio de
configuración real (alguien lo apagó o le cambió el schedule) — mirar quién lo tocó. Si falta un
bloque o un `runAfter` (entre los 5 bloques O en la etapa de arranque) no trae `Failed`, es un cambio
real en el diseño del flujo — no un capricho de esta prueba.

**Síntoma de un revert de D-62 (si alguien lo pisa).** Si alguien vuelve a dejar la etapa de arranque
con solo `["Succeeded"]` (a mano en el portal, o reimportando una versión vieja del JSON), este caso
vuelve a FALLAR exactamente en los checks de `Parametro_sin_responder`...`Correos_que_quedaron_en_la_bandeja`,
sin esperar nada — no hace falta que el defecto se manifieste en producción para detectarlo. Si
además se saca el Concurrency Control, el check de `runtimeConfiguration.concurrency.runs` falla
también. El síntoma EN PRODUCCIÓN de un revert (si V-01 no se corre) es el mismo que originó este
hallazgo: los cinco bloques de recuperación dejan de actuar TODOS JUNTOS en corridas puntuales
(no uno solo), coincidiendo con eventos `Error` de origen MPPP-VIG en la Bitácora en esa misma
ventana — y, si además se pierde el Concurrency Control, vuelve a verse más de un evento `Error` en
la misma franja de pocos segundos (ver la sección "Investigación de la causa de fondo" más arriba
para el patrón real medido).

**Automatizable.** `API` (Dataverse Web API contra `workflows`, sin esperar el schedule real — mejora
sobre la versión anterior de este caso, que era 100% manual).

---

## V-02 · Bloque 1 — un correo que quedó en la bandeja

**Qué prueba.** Que un correo que `MPPP-REC` no llegó a procesar se ingiera igual, por `MPPP-VIG`.

**Necesita.** Apagar de verdad `Cloud Flow - MPPP - REC - Recibir correo nuevo` (rol de
Maker/administrador de Power Automate); el REMITENTE; el adjunto `E01-feliz-formato11.xlsx`.

**Cómo.**
1. **Apagá** `Cloud Flow - MPPP - REC` (Turn off) — a mano, en el portal.
2. Mandá un correo nuevo con `E01-feliz-formato11.xlsx` al buzón de ingesta.
3. Comprobá que **no** se creó ninguna Solicitud (REC está apagado).
4. Esperá a que pasen los minutos de `vigilancia.minutos.sinvalidar` (leído en vivo por V-01) + una
   corrida de VIG.
5. **Volvé a prender** `MPPP-REC` al terminar.

**Qué tiene que pasar.**

| Qué | Valor |
|---|---|
| Se crea la Solicitud | **sí**, la ingirió VIG |
| Bitácora `Ingresada` | origen **MPPP-VIG** (no MPPP-REC) |
| El resto | igual que C-01: En proceso, 3 filas Validadas, acuse enviado |

**Cómo se verifica.** `ver_solicitud.py <numero>` después del paso 4, mirando el origen de la entrada
`Ingresada` en la Bitácora.

**Cómo se sabe que falló.** No aparece ninguna Solicitud después del margen completo (32 min): o el
correo no llegó de verdad a la bandeja (revisar el envío), o el bloque 1 no lo está viendo (mirar
`vigilancia.minutos.sinvalidar` con V-01, y si el problema persiste, el hallazgo del arranque común
de esta misma sección).

**Automatizable.** Manual, **a propósito, por regla del proyecto**: el service principal
`# PowerPlatform-Dev` SÍ tiene privilegio para apagar/prender un flujo por Web API (tiene System
Administrator) — pero este set (como ningún otro) **nunca desactiva ni modifica un flujo de Power
Automate**, sea cual sea el motivo. Se corre a mano, con el procedimiento de arriba, y se anota el
resultado en la tabla de Resultados. (Atajo para no esperar el umbral completo: bajar
`vigilancia.minutos.sinvalidar` a `1` antes del paso 4 y devolverlo a su valor original al terminar
— anotarlo en las notas si se usa: cambia lo que se está probando, igual que documentaba la versión
anterior de este archivo.)

---

## V-03 · Bloque 2 — una Solicitud vieja en Ingresada se reintenta

**Qué prueba.** El reintento de clasificación/validación, con su contador, sobre una Solicitud que
quedó en `Ingresada` (el mismo estado en que queda una Solicitud si `MPPP-ING` falla a mitad de
camino — hay evidencia real de esto en la Bitácora, ver la sección de M-06 arriba).

**Necesita.** Los 5 flujos activos (V-01/`preparacion_comun.verificar_flujos_activos`); el REMITENTE;
el margen de 32 minutos; `MPPP_V_LENTOS=si`.

**Cómo.** El set crea una Solicitud propia directo por Web API (sin adjuntos: no hace falta ningún
Excel para probar el mecanismo de reintento), la deja en `Ingresada` con `sanic_fechaingresada` más
vieja que `vigilancia.minutos.sinvalidar` (leído en vivo), y espera hasta 3 vueltas de `MPPP-VIG`.

**Qué tiene que pasar.**

| Qué | Valor |
|---|---|
| Precondición, sin esperar | la MISMA consulta que usa el bloque (`sanic_estadoprocesamiento eq Ingresada and sanic_requiererevision eq false and sanic_fechaingresada lt <umbral>`) encuentra esta Solicitud |
| `sanic_reintentosvalidacion` | pasa de vacío/0 a **1** |
| Bitácora | `Reintento`, origen **MPPP-VIG** |
| Filas | **no se duplican** |

**Cómo se verifica.** `ver_solicitud.py <numero>` + la Bitácora, después de esperar. La precondición
se verifica con el `$filter` exacto arriba, filtrado además por el GUID de esta Solicitud.

**Cómo se sabe que falló.** Si la precondición (sin esperar) ya da `False`, es un defecto de la
CONSULTA — no hace falta gastar 30 minutos para saberlo. Si la precondición da `True` pero
`sanic_reintentosvalidacion` sigue en 0 después de las 3 vueltas, es la etapa de arranque (ver la
sección de M-06) o un defecto real de ejecución — mirar la Bitácora de eventos `Error` con origen
MPPP-VIG en esa ventana.

**Automatizable.** `API`, detrás de `MPPP_V_LENTOS=si` (la lógica de reintento vive DENTRO del flujo,
no hay Custom API que la ejercite sin esperar el schedule real).

---

## V-04 · Bloque 2 — se acaban los reintentos

**Qué prueba.** Que después de `vigilancia.reintentos.maximo` (leído en vivo) se levante para
revisión humana en vez de reintentar para siempre.

**Necesita.** Igual que V-03.

**Cómo.** Igual que V-03, pero con `sanic_reintentosvalidacion` ya en el máximo antes de que corra
VIG.

**Qué tiene que pasar.**

| Qué | Valor |
|---|---|
| Precondición, sin esperar | la Solicitud aparece en la misma consulta de V-03 |
| **No** se reintenta | `sanic_reintentosvalidacion` sigue en el máximo (no sube) |
| `sanic_requiererevision` | **sí** |
| `sanic_motivorevision` | `"Validacion fallida <máximo> veces."` (el máximo real leído, no un número fijo) |
| Bitácora `Requiere revision` | **NO se verifica** — `MPPP-VIG` no la escribe en este bloque (hallazgo de este set, ver la sección de M-06) |

**Cómo se verifica.** `ver_solicitud.py <numero>` después de esperar.

**Cómo se sabe que falló.** `sanic_reintentosvalidacion` sube por encima del máximo (el `If` interno
tomó la rama equivocada), o `sanic_requiererevision` sigue en `false` después de 3 vueltas completas.

**Automatizable.** `API`, detrás de `MPPP_V_LENTOS=si` (mismo motivo que V-03).

---

## V-05 · Bloque 3 — una comunicación que nunca se inició

**Qué prueba.** Que una Solicitud que llegó a **En proceso** (o **Rechazada**, camino de cierre) sin
que el acuse **arrancara siquiera** se recupere — a diferencia de V-07 (bloque 4), donde el acuse SÍ
arrancó y quedó sin confirmar.

**Necesita.** Los 5 flujos activos; el REMITENTE; el Excel `E01-feliz-formato11.xlsx`; el margen de
32 minutos; `MPPP_V_LENTOS=si`.

**Cómo.** El set deja completar el acuse real primero (`ctx.procesar()`, igual que M-01: es la única
forma de llegar a **En proceso** con una Solicitud real), y después reescribe
`sanic_fechaacuseiniciado` y `sanic_fechaacuseenviado` a vacío (como si el acuse nunca hubiera
arrancado) y `sanic_fechaingresada` a más vieja que `vigilancia.minutos.sinresponder`. Espera hasta 3
vueltas de `MPPP-VIG`.

**Qué tiene que pasar.**

| Qué | Valor |
|---|---|
| Precondición, sin esperar | la consulta del bloque (`sanic_fechaingresada lt <umbral> and (((estado=EnProceso or estado=Rechazada) and fechaacuseiniciado eq null) or (estado=Procesada and fecharespuestafinaliniciada eq null))`) encuentra esta Solicitud |
| Llega el acuse (de nuevo) | **sí**: `sanic_fechaacuseenviado` vuelve a tener fecha |
| Bitácora `Acuse iniciado` / `Acuse enviado` | una entrada **NUEVA** con origen **MPPP-VIG** (la del acuse original quedó con origen MPPP-COM/MPPP-ENV) |
| `sanic_requiererevision` | sigue en `false` (esto es recuperación transparente, no una revisión) |

**Cómo se verifica.** `ver_solicitud.py <numero>` + Bitácora, contando entradas por origen.

**Cómo se sabe que falló.** La precondición en `False` es un defecto de consulta, verificable sin
esperar. Si la precondición es `True` y no hay una entrada nueva de Bitácora con origen VIG después
de 3 vueltas, es la etapa de arranque o un defecto real (mismo diagnóstico que V-03).

**Automatizable.** `API`, detrás de `MPPP_V_LENTOS=si` (depende de `MPPP-COM`/`MPPP-ENV` reales y del
schedule de `MPPP-VIG`).

---

## V-06 · Bloque 5 — los correos por clasificar vencen

**Qué prueba.** DF-09: lo que nadie clasificó en `clasificacion.dias.vencimiento` días pasa a
**Vencida**.

**Necesita.** Los 5 flujos activos; el margen de 32 minutos; `MPPP_V_LENTOS=si`. No necesita Excel ni
el REMITENTE autorizado: una Solicitud en **No reconocida** no pasa por clasificación de plantilla.

**Cómo.** El set crea una Solicitud propia, la deja directamente en **No reconocida** con
`sanic_fecharecibido` más vieja que `clasificacion.dias.vencimiento` (leído en vivo), y espera hasta
3 vueltas.

**Qué tiene que pasar.**

| Qué | Valor |
|---|---|
| Precondición, sin esperar | la consulta del bloque (`(estado=No reconocida or estado=No es correo nuevo) and fecharecibido lt <umbral>`) encuentra esta Solicitud |
| Estado | **Vencida** |
| `sanic_fechacerrada` | con fecha |
| Bitácora | `Vencida`, origen **MPPP-VIG** |

**Cómo se verifica.** `ver_solicitud.py <numero>` después de esperar.

**Cómo se sabe que falló.** Precondición en `False` → consulta rota, sin esperar. Precondición en
`True` y sigue en No reconocida después de 3 vueltas → arranque o defecto real.

**Automatizable.** `API`, detrás de `MPPP_V_LENTOS=si` (la comparación de días y el pase a Vencida
viven en `MPPP-VIG`).

---

## V-07 · Bloque 4 — un envío se inició y nunca se confirmó (cierra la pregunta abierta de M-06)

**Qué prueba.** Exactamente el escenario que M-06 dejó sin resolver: una Solicitud con el acuse (o la
respuesta final) **iniciado** pero nunca **confirmado**, más vieja que `vigilancia.minutos.sinresponder`,
tiene que terminar con `sanic_requiererevision = true` — sin reenviar nada.

**Necesita.** Los 5 flujos activos; el REMITENTE; el Excel `E01-feliz-formato11.xlsx`; el margen de
32 minutos; `MPPP_V_LENTOS=si`.

**Cómo.** Igual construcción que V-05, pero al revés: se deja completar el acuse real
(`ctx.procesar()`), y después se reescribe `sanic_fechaacuseiniciado` a más vieja que
`vigilancia.minutos.sinresponder` **dejando `sanic_fechaacuseenviado` vacío** (el envío "quedó a
mitad de camino", nunca "nunca arrancó" como en V-05). Antes de esperar nada, corre LA MISMA consulta
que usa `Envios_iniciados_sin_confirmar` contra esta Solicitud — esto es lo que M-06 no pudo hacer
sin reimplementar el flujo, y es lo que cierra la hipótesis (A) de la pregunta abierta.

**Qué tiene que pasar.**

| Qué | Valor |
|---|---|
| **Precondición decisiva, sin esperar nada** | la consulta de `Envios_iniciados_sin_confirmar` (`sanic_requiererevision eq false and ((fechaacuseiniciado ne null and fechaacuseenviado eq null and fechaacuseiniciado lt <umbral>) or (equivalente de respuesta final)))`) encuentra esta Solicitud |
| `sanic_requiererevision` | pasa a **true** (dentro del margen de 3 vueltas) |
| `sanic_motivorevision` | el texto fijo `"Envio iniciado sin confirmar: no se reenvia para que el cliente no reciba dos veces lo mismo."` |
| `sanic_fechaacuseenviado` | sigue vacío (no se completó un envío nuevo — no se reenvía) |
| Bitácora `Requiere revision` | **NO se verifica** — confirmado que `MPPP-VIG` no la escribe en ningún bloque de revisión (ver la sección de M-06); exigirla es el error que tenía M-06 |

**Cómo se verifica.** El `$filter` exacto de arriba contra el GUID de esta Solicitud (antes de
esperar); `ver_solicitud.py <numero>` después de esperar.

**Cómo se sabe que falló.** Si la precondición da `False`, es un defecto de la consulta —
confirmado que ESO no es lo que le pasó a M-06 (hay marcas reales e históricas de este mismo bloque
funcionando, ver la sección de M-06). Si la precondición da `True` y `sanic_requiererevision` sigue
en `false` después de 3 vueltas completas, es la etapa de arranque sin blindar (V-01 ya lo prueba en
rojo) o una corrida real perdida — mirar la Bitácora de eventos `Error` con origen MPPP-VIG en esa
ventana exacta.

**Automatizable.** `API`, detrás de `MPPP_V_LENTOS=si` (depende del schedule real de `MPPP-VIG`,
igual que sus hermanos).

---

## Resultados

| Caso | Solicitud | Resultado | Notas |
|---|---|---|---|
| V-01 configuración y hallazgo de arranque | | | |
| V-02 correo en la bandeja | | | |
| V-03 reintento | | | |
| V-04 se acaban los reintentos | | | |
| V-05 comunicación nunca iniciada | | | |
| V-06 vencimiento | | | |
| V-07 envío iniciado sin confirmar (M-06) | | | |

> **Acordate de dejar todo como estaba**: `MPPP-REC` prendido si V-02 lo apagó, y los parámetros con
> sus valores originales si los bajaste para no esperar (V-02).
