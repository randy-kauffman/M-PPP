# 04 — Comunicaciones al cliente

Set **M** (`CONVENCIONES.md` §4). Cubre `MPPP-COM` (detecta) y `MPPP-ENV` (envía). Las dos
comunicaciones son **el acuse** y **la respuesta final**, y las dos salen como **respuesta en el
hilo**, desde el buzón, **solo al remitente** (DF-03).

**La garantía que hay que romper para que esto falle**: *el cliente nunca recibe dos veces lo
mismo*. Ante la duda, no se reenvía y lo mira una persona (D-21).

---

## Por qué este set ya no es manual (2026-09-27)

Hasta hoy los 7 casos estaban `N-A (falta Mail.Send)`. Eso cambió: `herramientas/graph_api.py` es
nuevo y quedó probado el mismo día contra el entorno real — envío HTTP 202 desde un buzón de
prueba, y el service principal tiene además `Mail.Read` confirmado (se leyó el correo real en
Enviados y en la bandeja de entrada). Con eso alcanza para mandar un correo real, dejar que el
sistema lo procese de punta a punta, y leer lo que de verdad le llega al cliente — no simularlo,
no creerle solo a la Bitácora.

**El ciclo completo corre sin abrir un navegador.** `MPPP-COM` es un webhook de Dataverse sobre
`Update` de Solicitud (`recursos/flujos/mppp-com.json`, `filteringattributes=sanic_estadoprocesamiento`):
dispara solo, en tiempo real, cuando el estado cambia a Rechazada, En proceso o Procesada — no hace
falta ningún caso que lo invoque a mano. Y **digitar/aprobar tampoco necesitan navegador** (M-04):
se hacen por Web API suplantando con la cabecera `MSCRMCallerID` a los usuarios de prueba `User1`
(Ejecutivo) y `User2` (Supervisor) — el mismo mecanismo, ya probado en este proyecto, de
`comportamiento_roles.py` (`prvActOnBehalfOfAnotherUser`, que el service principal tiene).
Reimplementado LOCAL en `sets/set_m.py`, acotado a las filas de la Solicitud que este set creó
— nunca a "la primera fila que haya en el entorno", porque eso pisaría datos de otro set corriendo
en paralelo.

**Los cinco flujos MPPP están activos y confirmados contra Dev el 2026-09-27**
(`preparacion_comun.verificar_flujos_activos`, sin faltantes ni apagados) — sin esto nada de lo de
abajo sería posible.

**Set autocontenido** (`CONVENCIONES.md` §8). Este archivo prepara sus propios datos y no usa
ninguna sesión de navegador. La preparación exacta, y por qué NO hace otras cosas, está en
`herramientas/pruebas/sets/set_m.py::preparar()` — acá el resumen:

| Qué prepara | Cómo |
|---|---|
| Datos de negocio (`PR*`, el REMITENTE real autorizado sobre `PR11`) | `preparacion_comun.verificar_datos_negocio()` |
| El Excel `E01-feliz-formato11.xlsx` | `preparacion_comun.verificar_excels()` |
| `plantilla.estructura` con la forma esperada | `preparacion_comun.verificar_estructura_plantilla()` |
| Que Graph funcione de verdad (`Mail.Read` sobre el REMITENTE) | `graph_api.Graph().enviados()` |
| Los dos actores de M-04 (`User1`/Ejecutivo, `User2`/Supervisor), rol verificado | resuelto local en `set_m.py` |

**Corré este set con**:

```bash
python3 herramientas/pruebas/correr.py M
```

**Cuidado con el correo (obligatorio para quien edite este set).** El remitente y la copia son
buzones de prueba dedicados (`PP_MAIL_BUZON_3`/`PP_MAIL_BUZON_4` de `local/pp_secrets.env`, nunca
leído ni impreso acá), el destino siempre es `simulador.BUZON_INGESTA` (el mismo buzón de prueba
que ya usan los demás sets) y todo asunto empieza con `ZZP-`. **Ningún caso manda nada a una
dirección real de un cliente del banco.**

**La deuda que este set salda** (`pruebas/README.md`, "Deuda conocida al 2026-09-27"). Las
versiones anteriores de M-01, M-02, M-04 y M-05 pedían una Solicitud "de C-01" (otro archivo) o "de
S-01/S-05/C-08" (otro archivo): eso viola CONVENCIONES §8. Ahora **M-01 crea su propia Solicitud
con un correo real**, y M-04/M-05 la reutilizan — dentro del mismo set, que es la única
reutilización que §8 permite ("Dentro del mismo set, un caso SÍ puede apoyarse en lo que dejó el
anterior"). M-02, M-03 y M-06 crean cada una la suya. El orden de EJECUCIÓN dentro de
`set_m.py::CASOS` no es M-01..M-07: es `m01, m05, m04, m07, m02, m03, m06`, porque M-05 tiene que
correr sobre la Solicitud de M-01 **antes** de que M-04 la digite/apruebe y la cierre, y M-07 solo
compara contenido que M-01 (y, si corrió, M-04) ya leyeron de verdad. Los **resultados** de cada
caso se identifican por su ID, no por el orden en que aparecen en la consola.

**Lo que no se automatiza, y por qué:**

- **M-06** depende del *recurrence* real de `MPPP-VIG` (cada 10 minutos, `recursos/flujos/mppp-vig.json`,
  fase desconocida desde este script). La espera puede tardar hasta ~11 minutos. Es tiempo real de
  infraestructura, no un `sleep` inventado — y es la razón por la que correr el set M completo
  tarda más que los otros sets automatizados.

---

## Lo más valioso de este set: verificar qué le llega al cliente, no solo que "se mandó"

El aprobador dijo, hace unos días: *"me preocupa que estemos revelando información para un
atacante"* sobre los mensajes que le llegan al cliente del banco. Hasta el 2026-09-27 la única
forma de verificar el acuse y la respuesta final era creerle a `sanic_acusecontenido`/
`sanic_respuestafinalcontenido` en Dataverse. Con `Mail.Read` real, **M-01, M-02 y M-07 leen el
correo REAL que salió** (vía `graph_api.py`, buzón de prueba) y lo comparan contra esos campos —
y M-07, además, audita su texto contra una lista de términos internos que **no** tienen que
aparecer.

**El criterio no es inventado para este set**: ya está fijado en el proyecto para el mensaje de
`ExportarParaDigitar` (`diseno/DIRECTRICES-NUEVAS.md`, DN-02): *"se genericiza lo que revela cómo
funciona el sistema por dentro; se deja pasar lo que le dice a la persona qué tiene que hacer."*
M-07 aplica el mismo criterio a las dos comunicaciones al cliente: nombres de regla
(`TRAE_ADJUNTO`, `ADJUNTO_ES_EXCEL`, `PLAN_EXISTE`...), de flujo (`MPPP-ING`, `MPPP-COM`...), de
tabla/columna (`sanic_...`), de plataforma (`Custom API`, `Plugin`, `Dataverse`, `webhook`) o de
excepción (`Exception`, `StackTrace`, `Microsoft.Crm`) no tienen que aparecer nunca en lo que lee
el cliente; pedirle que reenvíe, que corrija, o decirle que no responda al mensaje, sí.

---

## M-01 · El acuse llega, y llega una sola vez

**Qué prueba.** La cadena real `MPPP-REC` → `MPPP-ING` → Custom API → estado En proceso →
`MPPP-COM` → `MPPP-ENV`, de punta a punta, con un correo real.

**Necesita.** El `REMITENTE` real de este set (uno de los buzones de prueba, autorizado sobre
`PR11` por `preparar()`); el adjunto `E01-feliz-formato11.xlsx`; los cinco flujos MPPP activos.
Nada de otro archivo ni de otro caso: la Solicitud la crea este mismo caso, con un correo real.

**Cómo.** `graph_api.Graph` manda un correo real desde el REMITENTE hacia `simulador.BUZON_INGESTA`,
asunto `ZZP-M01-<nonce>`, con **un solo adjunto**, `E01-feliz-formato11.xlsx`. El caso espera
(polling sobre Dataverse) a que `MPPP-REC`/`MPPP-ING` reales creen la Solicitud, y después a que
llegue a estado final.

**Qué tiene que pasar.**

| Qué | Valor |
|---|---|
| La Solicitud aparece sola (correo real, sin `simulador.crear_solicitud`) | dentro de 240 s |
| Estado | **En proceso** |
| filas t/v/r | `3 / 3 / 0` |
| `sanic_fechaacuseiniciado` y `sanic_fechaacuseenviado` | con fecha |
| Bitácora | `Acuse iniciado` y `Acuse enviado` |
| Llega a la bandeja REAL del REMITENTE | **un** correo, como respuesta en el hilo, desde `simulador.BUZON_INGESTA` |
| Cantidad de correos con ese asunto en la bandeja | **exactamente 1** |

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py M` (caso `M-01`); a mano,
`ver_solicitud.py <MPPP-########>` sobre la Solicitud que el caso deja loggeada, y mirando la
bandeja real del buzón de prueba configurado como `PP_MAIL_BUZON_3`.

**Cómo se sabe que falló.** Tres causas con síntomas parecidos: (a) si la Solicitud **nunca
aparece**, `MPPP-REC` no está viendo el buzón (corre cada 1 minuto) o el filtro `hasAttachments`
del trigger está excluyendo el correo con adjunto — mirar el historial de ejecuciones del flujo
antes que asumir un defecto de negocio; (b) si la Solicitud aparece pero nunca llega a **En
proceso** con acuse, el problema está en la Custom API o en `MPPP-COM`/`MPPP-ENV` — mirar la
Bitácora real con `ver_solicitud.py`; (c) si el estado y la Bitácora están bien pero **no llega
ningún correo real** (o llega más de uno), el defecto está específicamente en `MPPP-ENV`
(`ReplyToV3`) o en la Application Access Policy del service principal sobre ese buzón — acá es
donde "creerle a la Bitácora" mentía antes del 2026-09-27.

**Automatizable.** API (`correr.py M`, caso `M-01`) — correo real, sin navegador.

---

## M-02 · El acuse de una Rechazada cierra la Solicitud

**Qué prueba.** DD-09: acuse de una **Rechazada** → **Cerrada** + `sanic_fechacerrada`, con un
correo real de punta a punta (mismo camino de rechazo que S-01/C-08: sin adjunto, `TRAE_ADJUNTO`
falla).

**Necesita.** El REMITENTE de este set. Nada más: la Solicitud Rechazada la crea este mismo caso,
con su propio correo real (ya no depende de S-01, S-05 ni C-08 — esa era la deuda declarada en
`pruebas/README.md`).

**Cómo.** Correo real, asunto `ZZP-M02-<nonce>`, **sin ningún adjunto**, al buzón de ingesta.

**Qué tiene que pasar.**

| Qué | Valor |
|---|---|
| Estado | **Rechazada** → **Cerrada**, automático, sin digitar nada |
| `sanic_fechacerrada` | con fecha |
| Hubo acuse enviado antes de cerrar | sí |
| Bitácora | `Acuse enviado` |
| Llega el acuse real a la bandeja | sí |
| El texto del acuse real | no nombra ningún término interno (ver "Lo más valioso de este set") |

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py M` (caso `M-02`).

**Cómo se sabe que falló.** Si el Estado queda en Rechazada pero nunca pasa a Cerrada, el defecto
está en `Cerrar_si_estaba_rechazada` de `MPPP-ENV` (revisa que se ejecute solo cuando
`sanic_estadoprocesamiento` seguía siendo Rechazada al momento del acuse). Si cierra pero sin
`sanic_fechacerrada`, es un campo que no se está seteando, no un problema de la transición en sí.
Si el correo real llega pero nombra un término interno, es un defecto de privacidad (ver el
criterio de DN-02), no un defecto funcional — igual cuenta como FALLA.

**Automatizable.** API (`correr.py M`, caso `M-02`) — correo real, sin navegador.

---

## M-03 · Copia y remitente

**Qué prueba.** DF-03: solo al remitente, nunca a los de copia — con un correo real que sí trae
copia.

**Necesita.** El REMITENTE de este set y un segundo buzón de prueba (`PP_MAIL_BUZON_4`) para poner
en copia. Ambos ya resueltos por `preparar()`.

**Cómo.** Correo real con `E01-feliz-formato11.xlsx`, asunto `ZZP-M03-<nonce>`, con el segundo
buzón de prueba **en CC**.

**Qué tiene que pasar.** El acuse llega **solo** a la bandeja del REMITENTE (el **De**). A la
bandeja de la copia **no le llega ninguna respuesta DEL SISTEMA** (`From = simulador.BUZON_INGESTA`).

> **Ojo con un falso FALLA (ya pasó una vez, 2026-09-27).** La copia SÍ recibe el correo
> **ORIGINAL** en su Inbox — es la entrega normal de Exchange a todo destinatario en `Cc`, no algo
> que el sistema mande. Filtrar la bandeja de la copia solo por asunto cuenta ese correo original
> como si fuera una respuesta y da un FALLA que no es tal. La verificación tiene que exigir,
> además del asunto, que el remitente del hallazgo sea `simulador.BUZON_INGESTA` — si no, se está
> confundiendo "recibió el correo que él mismo copió" con "el sistema le respondió a él".

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py M` (caso `M-03`).

**Cómo se sabe que falló.** Si a la copia le llega algo **con remitente `simulador.BUZON_INGESTA`**
(no el correo original, una respuesta real del sistema), `ReplyToV3` se está llamando con
`ReplyAll: true` (o algo cambió `mailboxAddress`/el destinatario calculado) — se le estaría
respondiendo a quien está en copia, no solo a quien preguntó. Es el defecto más caro de este caso
puntual: revela a un tercero en copia que ese correo se procesó y qué contiene.

**Automatizable.** API (`correr.py M`, caso `M-03`) — correo real, sin navegador.

---

## M-04 · La respuesta final, cuando todas las filas se resuelven

**Qué prueba.** El ciclo completo hasta **Procesada** → respuesta final real → **Cerrada**.

**Necesita.** La Solicitud que **M-01** creó en esta misma corrida (3 filas Validadas, En proceso,
acuse ya enviado); los dos actores `ejecutivo`/`supervisor` (systemuserid resuelto y rol verificado
por `preparar()`).

**Cómo.** Sobre las 3 filas de la Solicitud de M-01: se digita cada una por Web API suplantando a
`User1` (`MSCRMCallerID`, sin sesión de navegador ni cookies), y se aprueba cada una suplantando a
`User2`. Se espera a que el estado real llegue a Cerrada.

**Qué tiene que pasar.**

| Momento | Qué pasa |
|---|---|
| Las 3 filas están Validadas antes de digitar | sí |
| El Ejecutivo digita las 3 | sí (segregación de funciones: lo prueba con detalle `comportamiento_roles.py`, acá solo se usa el mecanismo) |
| El Supervisor aprueba las 3 | sí |
| Al aprobar la última fila | la Solicitud pasa a **Procesada** |
| Enseguida (webhook real) | `MPPP-COM` dispara `MPPP-ENV` |
| Llega | la **respuesta final real**, en el mismo hilo, solo al REMITENTE |
| Estado final | **Cerrada** + `sanic_fechacerrada` |
| Bitácora | `Respuesta final iniciada`, `Respuesta final enviada` |
| Correos reales totales para esta Solicitud | **exactamente 2** (el acuse de M-01 + la respuesta final) |
| El cuerpo de la respuesta final real | coincide con `sanic_respuestafinalcontenido` |

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py M` (caso `M-04`; corre después de
M-05 y antes de M-07 dentro de `set_m.py::CASOS`, para no cerrar la Solicitud de M-01 antes de que
M-05 pruebe su guardia de reenvío).

**Cómo se sabe que falló.** Si digitar o aprobar devuelve un HTTP de rechazo, el problema es de
roles/segregación (mirar el motivo textual que devuelve el plugin, no asumir). Si las 3 filas
quedan Aprobadas pero la Solicitud nunca pasa a Procesada, el defecto está en el step que
recalcula el estado de Solicitud a partir de sus filas — anterior a cualquier cosa de este set. Si
llega a Procesada pero nunca llega el segundo correo real, es lo mismo que en M-01 pero sobre
`Toca_la_respuesta_final` de `MPPP-ENV`, no sobre `Toca_el_acuse`.

**Automatizable.** API (`correr.py M`, caso `M-04`) — digitar/aprobar por `MSCRMCallerID`, sin
navegador; respuesta final real, sin navegador.

---

## M-05 · Nunca dos veces lo mismo

**Qué prueba.** D-21 y el paso 1 de `MPPP-ENV`: si el acuse ya está **iniciado**
(`sanic_fechaacuseiniciado != null`), un segundo disparo de `MPPP-COM` no lo reenvía.

**Necesita.** La Solicitud que **M-01** creó en esta misma corrida, ya con acuse enviado.

**Cómo.** Se fuerza un segundo disparo REAL del webhook de `MPPP-COM`: `PATCH` del **mismo**
valor de `sanic_estadoprocesamiento` que la Solicitud ya tenía. El webhook de Dataverse
(`recursos/flujos/mppp-com.json`) está suscripto a `Update` con
`filteringattributes=sanic_estadoprocesamiento`: dispara cuando ese atributo viene en el `Target`
del `Update`, sea cual sea el valor nuevo — no compara valor viejo contra nuevo. Lo que se prueba
es la guardia de `MPPP-ENV` (`sanic_fechaacuseiniciado != null` → no reenvía), no si el webhook
"refirió" o no: cualquiera de los dos resultados posibles del webhook (refirió y `MPPP-ENV` lo
frenó, o Dataverse lo consideró no-cambio y ni llamó) es indistinguible desde afuera y **los dos
son un pase** de este caso, porque los dos dejan el mismo resultado observable.

**Qué tiene que pasar.**

| Qué | Valor |
|---|---|
| `sanic_fechaacuseenviado` | **no cambia** tras el segundo disparo |
| Nuevas entradas de Bitácora de acuse (`Acuse iniciado`/`Acuse enviado`) | ninguna |
| Correos reales nuevos en la bandeja del REMITENTE | ninguno (60 s de margen tras el `PATCH`) |

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py M` (caso `M-05`; corre justo después
de M-01, antes de que M-04 digite/apruebe la Solicitud — así se prueba el guardado del acuse
todavía en estado En proceso, no ya Cerrada).

**Cómo se sabe que falló.** Si `sanic_fechaacuseenviado` cambia, o llega un segundo correo real, es
el defecto más caro de todo el set: el cliente recibe dos acuses de la misma Solicitud. Es
exactamente lo que M-05 existe para blindar.

**Automatizable.** API (`correr.py M`, caso `M-05`) — sin navegador, sin correo nuevo (reusa la
bandeja ya abierta por M-01).

---

## M-06 · Envío iniciado sin confirmar → revisión humana

**Qué prueba.** Que una comunicación a medias **no se reenvíe** y levante bandera (D-21), con el
`recurrence` REAL de `MPPP-VIG` (cada 10 minutos).

**Necesita.** El REMITENTE de este set. La Solicitud En proceso la crea este mismo caso, con su
propio correo real.

**Cómo.** Correo real (con adjunto válido) → se deja que el pipeline real complete su acuse normal
(no hay ventana para "interceptar a medias" un envío que el webhook dispara casi al instante: hay
que dejarlo terminar). Inmediatamente después, se fuerza el escenario a medias con un `PATCH`
directo: `sanic_fechaacuseiniciado` = hace 40 minutos (> `vigilancia.minutos.sinresponder` = 30,
`P-06`), `sanic_fechaacuseenviado` = vacío. Se espera al próximo ciclo real de `MPPP-VIG`.

> **Corrección de margen (2026-09-27).** La primera versión de este caso esperaba hasta ~11
> minutos (un `recurrence` de 10 minutos + 1 de colchón). Con ese margen, **no alcanzó a verse**
> ni una sola vez, pese a que la condición ya estaba dada (`sanic_fechaacuseiniciado` bien
> backdateado, `sanic_fechaacuseenviado` vacío, verificado por Web API en el momento). Un
> `recurrence` trigger de Power Automate en un entorno compartido puede arrancar con demora real,
> y un solo intervalo de margen no alcanza para distinguir esa demora de un defecto de
> `MPPP-VIG`. El margen se subió a ~22 minutos (dos vueltas completas de colchón) para poder
> afirmar una cosa o la otra con evidencia, no con una sola muestra.

**Qué tiene que pasar.**

| Qué | Valor |
|---|---|
| Llega un acuse nuevo | **NO** |
| `sanic_requiererevision` | pasa a **true** |
| `sanic_motivorevision` | menciona "sin confirmar" |
| Bitácora | `Requiere revision`, origen **MPPP-VIG** |
| `sanic_fechaacuseenviado` | sigue vacío al terminar |

> Éste es el costo aceptado del diseño: ante la duda no se reenvía y lo mira una persona. Si acá
> llega un correo, la garantía de "como máximo una vez" no existe.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py M` (caso `M-06`; el más lento del
set — puede tardar hasta ~22 minutos).

**Cómo se sabe que falló.** Si el `PATCH` que arma el escenario a medias es rechazado (HTTP
distinto de 200/204), no es simulable desde acá: hay un control de plataforma bloqueando la
escritura directa de esos dos campos, y el caso queda `N-A` con el motivo exacto (nunca se fuerza
un forzado). Si el `PATCH` se acepta pero `sanic_requiererevision` nunca pasa a `true` dentro de
la espera, el defecto está en el bloque `Envios_iniciados_sin_confirmar` de `MPPP-VIG` — o el
parámetro `vigilancia.minutos.sinresponder` cambió su valor por debajo de los 40 minutos simulados
(mirar P-06 antes de asumir un defecto). Si llega un correo nuevo, es la misma garantía que rompe
M-05, vista desde el otro disparador (`MPPP-VIG` en vez de `MPPP-COM`).

> **FALLA real, reproducida dos veces el 2026-09-27 (no es un bug del caso).** Con la
> precondición confirmada por lectura directa (`sanic_fechaacuseiniciado` correctamente
> backdateado, `sanic_fechaacuseenviado` vacío, `sanic_requiererevision` en `false`),
> `sanic_requiererevision` **nunca pasó a `true`** ni con ~11 ni con ~22 minutos de margen (dos
> vueltas completas del `recurrence`). `MPPP-VIG` está vivo y corriendo en ese mismo lapso — se
> confirmó leyendo la Bitácora del entorno completo: hay decenas de eventos `Error` con
> `sanic_origen = MPPP-VIG` y detalle `"Fallo la ingesta del correo..."` (del bloque
> `Correos_que_quedaron_en_la_bandeja`, otro bloque del mismo flujo) fechados en la ventana exacta
> de la espera. También hay eventos `Requiere revision` reales en el entorno, pero con
> `sanic_origen = MPPP-COM`, nunca `MPPP-VIG`, en ese lapso. No se pudo determinar desde acá si la
> causa es (a) un defecto en la condición/consulta de `Envios_iniciados_sin_confirmar`, o (b) que
> el flujo se corta antes de llegar a ese bloque por los errores del bloque anterior — plausiblemente
> agravado por tráfico de otros sets corriendo en paralelo sobre el mismo buzón de ingesta
> compartido. **Queda reportado tal cual para que lo revise quien mantiene `recursos/flujos/mppp-vig.json`**:
> no es un archivo que este set pueda tocar ni diagnosticar más a fondo.

**Automatizable.** API (`correr.py M`, caso `M-06`) — correo real + espera real de `MPPP-VIG`, sin
navegador.

---

## M-07 · El cuerpo lo arma el código, no el flujo — y no revela información interna

**Qué prueba.** DD-08 (el contenido viene de `sanic_acusecontenido`/`sanic_respuestafinalcontenido`,
que escribe la Custom API, no el flujo) **y** el criterio de privacidad del aprobador
(`diseno/DIRECTRICES-NUEVAS.md`, DN-02): ningún correo real que reciba el cliente puede nombrar una
regla, un flujo, una tabla/columna ni una excepción de plataforma.

**Necesita.** El acuse real que **M-01** dejó leído en esta misma corrida (obligatorio); la
respuesta final real que **M-04** dejó leída, si corrió (opcional: si M-04 no llegó a tener éxito,
M-07 igual audita el acuse solo, y lo reporta).

**Cómo.** Compara el texto plano del correo REAL recibido en M-01 contra `sanic_acusecontenido`, y
—si está disponible— el de M-04 contra `sanic_respuestafinalcontenido`. Después audita el texto
plano de cada correo real contra la lista de términos internos: `sanic_`, `TRAE_ADJUNTO`,
`ADJUNTO_ES_EXCEL`, `UN_SOLO_EXCEL`, `ESTRUCTURA_PLANTILLA`, `TIENE_FILAS`, `PLAN_EXISTE`,
`MONEDA_DEL_PLAN`, `LISTAS_VALIDAS`, `LARGOS_Y_FORMATO`, `AUTORIZACION_CORREO_PLAN`,
`OBLIGATORIEDAD`, `MPPP-ING`, `MPPP-REC`, `MPPP-COM`, `MPPP-ENV`, `MPPP-VIG`, `Custom API`,
`CustomAPI`, `webhook`, `Dataverse`, `Plugin`, `Exception`, `StackTrace`, `Microsoft.Crm`,
`systemuserid`, `GUID`.

**Qué tiene que pasar.** El cuerpo real coincide con el campo calculado, y **ninguno** de los
términos de la lista aparece en el texto plano de ninguno de los dos correos reales.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py M` (caso `M-07`).

**Cómo se sabe que falló.** Si el cuerpo real NO coincide con el campo calculado, el correo trae
algo agregado por el flujo o el conector (una firma automática, un encabezado de Power Automate,
un "Sent from…") — eso rompe DD-08. Si coincide pero aparece un término de la lista, es un defecto
de privacidad: **quien lo escribió puso un mensaje de depuración donde tenía que ir el mensaje al
cliente** (mismo patrón que ya se vio en D-19, set S). Distinguí cuál regla/campo se filtró: eso
apunta directo a qué mensaje de la Custom API hay que corregir.

**Automatizable.** API (`correr.py M`, caso `M-07`) — sin navegador.

---

## Fase 2 — investigación y conclusión

Se investigó si algo de fase 2 (`diseno/DIRECTRICES-NUEVAS.md` DN-01/DN-02/DN-03, y
`diseno/FASE2-ESTADO.md`, el RPA) agrega o cambia comportamiento que se resuelva en el nivel que
cubre este set (`MPPP-COM`/`MPPP-ENV`, el acuse, la respuesta final, "como máximo una vez").
**Ninguno aplica**, y acá está la evidencia:

1. **DN-01 (autorización por cliente)**: dice explícitamente, dos veces, que **el mensaje al
   cliente no cambia en ningún modo** — *"Esto no se negocia: un texto que distinga 'el plan no
   existe' de 'no estás autorizado' es un oráculo, y ya lo cerramos una vez."* Y el barrido de
   código que el propio documento trae (`diseno/DIRECTRICES-NUEVAS.md`, "Hay UN solo punto donde
   se consulta la autorización") ubica los dos únicos llamadores de `PlanesAutorizadosDe` en
   `ClasificarCorreo.cs:242` (nivel Correo) y `ValidarSolicitud.cs:246` (nivel Registro) — ninguno
   es `MPPP-COM` ni `MPPP-ENV`.

2. **DN-02 (exportar a Excel para digitar)**: es una descarga **interna** para el ejecutivo que
   digita en AS400 (`sanic_mppp_capi_exportarparadigitar`, recibe `filaid`), no una comunicación al
   cliente. Su propio criterio de mensajes — *"se genericiza lo que revela cómo funciona el sistema
   por dentro; se deja pasar lo que le dice a la persona qué tiene que hacer"* — es el que M-07
   toma prestado para auditar el acuse y la respuesta final, pero DN-02 en sí no toca
   `sanic_acusecontenido`, `sanic_respuestafinalcontenido` ni ningún paso de `MPPP-COM`/`MPPP-ENV`.

3. **DN-03 (obligatoriedad por gestión)**: dice explícitamente **"El mensaje al cliente. No
   cambia. Sigue nombrando el campo y sin citar el valor (D-41)."** Cambia qué campos son
   obligatorios según la gestión de la fila, nivel Registro — no toca cuándo ni cómo se comunica
   una Solicitud.

4. **`FASE2-ESTADO.md` (el RPA)**: es íntegramente sobre que un bot digite/apruebe filas
   (`sanic_mppp_capi_obtenerfilaspendientes`, `sanic_mppp_capi_registrarresultadorpa`, el claim
   `sanic_claimhasta`). No aparece `MPPP-COM`, `MPPP-ENV`, `acuse` ni `respuesta final` en ningún
   punto del documento (comprobado por búsqueda exhaustiva el 2026-09-27). Que el bot digite y
   apruebe en vez de un humano no cambia el disparador de la comunicación: la Solicitud sigue
   pasando a Procesada exactamente igual, y `MPPP-COM` sigue sin saber quién aprobó.

**Conclusión**: el set M queda con los mismos 7 casos, sin agregados de fase 2. Si en el futuro una
directriz nueva cambia el contenido o el disparador de una comunicación al cliente, este archivo es
donde corresponde sumarla — hoy no hay ninguna.

---

## Resultados

Corrida real contra Dev, 2026-09-27/28 (`python3 herramientas/pruebas/correr.py M`, dos corridas:
la primera detectó el bug de M-03 descrito arriba y se corrigió el código; la segunda es la que
vale). **6 OK · 1 FALLA · 0 N-A.**

| Caso | Solicitud | Resultado | Notas |
|---|---|---|---|
| M-01 acuse real, una sola vez | creada por el caso (correo real) | **OK** | acuse real leído, contenido y privacidad verificados |
| M-02 Rechazada real → Cerrada | creada por el caso (correo real) | **OK** | cierre automático confirmado |
| M-03 solo al remitente (correo real con copia) | creada por el caso (correo real) | **OK** | tras corregir el falso FALLA (ver nota arriba) |
| M-04 respuesta final real (digitar/aprobar por API) | la de M-01 | **OK** | digitar/aprobar por `MSCRMCallerID`, sin navegador |
| **M-05 nunca dos veces** | la de M-01 | **OK** | segundo disparo de MPPP-COM no reenvía |
| M-06 a medias → revisión (espera real de MPPP-VIG) | creada por el caso (correo real) | **FALLA** | ver nota diagnóstica arriba — `MPPP-VIG` no marcó `sanic_requiererevision` en ~22 min pese a la precondición confirmada; reportado para quien mantiene `mppp-vig.json` |
| M-07 cuerpo del código + sin información interna | la de M-01 (+ M-04) | **OK** | ningún término interno encontrado en los correos reales |
