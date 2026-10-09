# 07. Diseño de los flujos de Power Automate

Proyecto: 2026-001-referencias-planes-pago · Etapa 3 · Estado: **revisado con el aprobador el 2026-09-20; los cinco flujos están construidos, ACTIVOS y probados contra el entorno** (verificado hoy, 2026-09-28, por Web API: `statecode=1`/`statuscode=2` en los cinco — `MPPP-REC`, `MPPP-ING`, `MPPP-COM`, `MPPP-ENV`, `MPPP-VIG`).

> **Corregido el 2026-09-28.** Esta línea decía "Nada de esto está construido" — cierta cuando se escribió (2026-09-21), falsa desde que se construyeron los cinco flujos la semana del 2026-09-22. Además, dos cambios de diseño se sumaron DESPUÉS de esa construcción, los dos detectados y cerrados el mismo día 2026-09-28: **(a)** la etapa de arranque de `MPPP-VIG` ahora encadena con `["Succeeded","Failed"]` en vez de solo `["Succeeded"]`, y su trigger declara control de concurrencia (`PENDIENTES.md` D-62); **(b)** `MPPP-ING` guarda `sanic_outlookmessageid` con un `coalesce` de respaldo para que la re-ingesta no rompa cuando el id de Outlook cambia (`PENDIENTES.md` D-66). Detalle de cada uno en las secciones 3 y 6. **Este documento no mencionaba `runAfter`, `concurrency`, `MoveV2` ni `Immutable` antes de esta corrección** — verificado con `rg` sobre la versión previa.

Reglas citadas: BP-PP-058, 059, 061, 122, 123, 196. Requisitos: RF-01, RF-03, RF-04, RF-09, RF-10, RNF-02, RNF-05. Decisiones de la definición: D-01, D-02, D-21, D-24, D-31. Decisiones de la etapa 3: `02` DD-03, DD-08, DD-09. Decisiones post-construcción: `PENDIENTES.md` D-62, D-66.

## 0. Decisiones de este documento

| ID | Decisión | Motivo |
|---|---|---|
| DF-01 | **Desaparece el Flow B de la definición.** La ingesta crea la Solicitud, sube los archivos y llama ella misma a las Custom API. | La definición (D-24) disparaba la validación con un segundo flujo, por el alta de la Solicitud: un flujo encadenado a otro por un disparador, que BP-PP-058 desaconseja, con más latencia y otro punto de falla, sin dar nada a cambio: si la validación falla, la Solicitud queda en Ingresada y la vigilancia la reintenta igual. Excepción a la definición: E-15. |
| DF-02 | **Lógica compartida en flujos hijos, no duplicada.** `MPPP-ING` lo usan `MPPP-REC` y `MPPP-VIG`; `MPPP-ENV` lo usan `MPPP-COM` y `MPPP-VIG`. | La vigilancia tiene que poder hacer exactamente lo mismo que el flujo al que reemplaza cuando algo quedó a medias. Copiar las acciones en dos flujos garantiza que un día difieran. Un flujo hijo llamado para reutilizar lógica no es el "bus" de BP-PP-058: no hay cadena de eventos, hay una llamada con respuesta. |
| DF-03 | **Las dos comunicaciones salen como respuesta en el hilo** del correo original, desde el buzón, **solo al remitente**. | Decisión del aprobador. Al cliente le queda todo en una conversación; y solo se le responde a quien se le verificó la autorización, nunca a quienes iban en copia. |
| DF-04 | **Se guarda el identificador de Outlook del correo después de moverlo**, en `sanic_outlookmessageid`, con una acción propia. | Para responder en el hilo hace falta el identificador de Outlook, no el Internet Message-ID, y ese identificador **cambia cuando el correo se mueve de carpeta** (Learn lo advierte: "IDs might change after certain actions such as copy or move"). Como la Solicitud se crea antes de mover, hace falta una actualización explícita después. |
| DF-05 | **El buzón es una environment variable**, `sanic_mppp_ev_buzoningesta`; en el Dev del aprobador vale `mppp@55xljh.onmicrosoft.com`. Es un **buzón compartido**. La carpeta de procesados es otra variable, `sanic_mppp_ev_carpetaprocesados`. | Decisión del aprobador y BP-PP-123. El Dev actual es un tenant propio del aprobador; la solución pasa después al Dev de BAC, donde estos valores se cargan de nuevo. |
| DF-06 | **`MPPP-COM` procesa de a una Solicitud por vez** (concurrencia del disparador en 1). | El "como máximo una vez" depende de que dos ejecuciones no lean a la vez "todavía no iniciado". Con unos cientos de correos por mes, serializar no cuesta nada. |
| DF-07 | **Los adjuntos en línea no cuentan.** Firmas e imágenes incrustadas llegan con `isInline = true`; se descartan antes de contar. | Sin esto, un correo con firma con logo y la plantilla tendría "dos adjuntos" y fallaría `UN_SOLO_EXCEL`. |
| DF-08 | **Solo se procesan correos nuevos. Una respuesta no se procesa nunca; un reenvío, sí.** Lo decide un **plugin liviano**, `sanic_mppp_capi_clasificarcorreo` (`03` §0), antes y aparte del plugin que lee la plantilla. | Regla del aprobador. Una respuesta automática de "fuera de oficina" es siempre una respuesta a algo que le mandamos: al no procesar respuestas, no se le contesta y el bucle no puede empezar. Cubre también al cliente que contesta "gracias" a un acuse. En un plugin y no en el flujo: la regla se prueba con tests, vive en el catálogo de reglas, y no depende de que el conector de Outlook deje leer cabeceras (no se pudo verificar). Y aparte del plugin grande, porque son dos preguntas distintas: "¿este correo merece procesarse?" y "¿qué dice la plantilla?". |
| DF-09 | **Lo que no se procesa va a la bandeja "Por clasificar" y vence.** Estados No reconocida y **No es correo nuevo**; si nadie lo atiende ni lo descarta en `clasificacion.dias.vencimiento` días (inicial 30), pasa a **Vencida**, terminal. | Decisión del aprobador; aplica a los dos estados. Vencida y no Descartada, para distinguir "alguien decidió ignorarlo" de "nadie lo miró". |
| DF-10 | **Sin cortacircuito por remitente, por ahora.** | Decisión del aprobador. Riesgo aceptado: un sistema ajeno que conteste cada mensaje nuestro con un correo **nuevo** (no una respuesta) desde la dirección de un autorizado no queda cubierto por DF-08. |
| DF-11 | **Cada flujo tiene un identificador corto**, `MPPP-` + tres letras, que va en su nombre y es el valor que se anota como origen en la Bitácora. | El aprobador rechazó las letras A, B, C, W de la definición: no dicen nada. El identificador se reconoce aun sin saber de qué solución es el flujo (BP-PP-196). |
| D-62 | **`MPPP-VIG`: la etapa de arranque encadena con `["Succeeded","Failed"]`** en sus cinco eslabones (`Parametro_sin_validar → Parametro_sin_responder → Parametro_reintentos → Parametro_vencimiento → Umbrales → Correos_que_quedaron_en_la_bandeja`), antes solo `["Succeeded"]`; y el trigger declara `runtimeConfiguration.concurrency.runs = 1`. Agregado el 2026-09-28. | Con solo `Succeeded`, una falla en CUALQUIER paso de arranque dejaba `Skipped` a los CINCO bloques de recuperación esa corrida — el mecanismo que existe para recuperarse de fallas se apagaba entero cuando había una falla. Se evaluó cortar en algún eslabón y se decidió que no: `Umbrales` ya trae `coalesce(...)` con valor de respaldo por cada parámetro, así que un `Parametro_*` que falla no deja el flujo con un umbral vacío. La concurrencia se agregó por evidencia de corridas superpuestas. **CERRADA** (`PENDIENTES.md` D-62, commit `d1cac41`). Ver nota de verificación en la sección 6: el trigger declarado NO coincide hoy con lo desplegado. |
| D-66 | **`MPPP-ING`, paso 7b (`Guardar_el_identificador_nuevo`): `sanic_outlookmessageid` = `coalesce(id que devolvió `Mover_el_correo`, id con que se llamó a este flujo)`.** Agregado el 2026-09-28, arreglado por el aprobador. | Los IDs de mensaje de Outlook dejaban de ser válidos entre que se leían y se re-leían (`MoveV2` puede cambiar el id si el tenant no tiene "Immutable Id"), y eso rompía la re-ingesta que dispara `MPPP-VIG`: 898 eventos de error en dos horas, 0% de éxito. Con el `coalesce`, la columna nunca queda sin escribir aunque el paso de mover no devuelva id. **CERRADA** (`PENDIENTES.md` D-66). Verificado: los errores pararon a las 10:52:32Z; en los diez minutos siguientes, 12 eventos de origen `MPPP-VIG` y cero errores. |

## 1. Mapa

| ID | Nombre | Disparador | Qué hace |
|---|---|---|---|
| `MPPP-REC` | `Cloud Flow - MPPP - REC - Recibir correo nuevo` | Llega un correo a la bandeja de entrada del buzón compartido | Llama a `MPPP-ING` |
| `MPPP-ING` | `Cloud Flow - MPPP - ING - Ingerir y validar correo` | Hijo | Crea la Solicitud, sube archivos, mueve el correo, lo clasifica y, si corresponde, lo valida |
| `MPPP-COM` | `Cloud Flow - MPPP - COM - Detectar comunicación pendiente` | Cambia el estado de una Solicitud | Llama a `MPPP-ENV` |
| `MPPP-ENV` | `Cloud Flow - MPPP - ENV - Enviar comunicación al cliente` | Hijo | Envía el acuse o la respuesta final, como máximo una vez |
| `MPPP-VIG` | `Cloud Flow - MPPP - VIG - Vigilar pendientes` | Cada 10 minutos | Recupera lo que quedó a medias llamando a `MPPP-ING` y a `MPPP-ENV`, levanta para revisión lo que no se puede reintentar, y vence lo que nadie clasificó |

Todos son propiedad de la **cuenta de servicio** (D-01, estándar regional de BAC; BP-PP-122 pide un service principal, y esa excepción ya está en la definición). Usan dos connection references, `sanic_mppp_conr_outlook` y `sanic_mppp_conr_dataverse`. Todas las acciones de Outlook son las de buzón compartido o llevan la dirección del buzón como parámetro. Ninguno lee filas, clientes ni planes (`04` §3). Cuando un hijo escribe en la Bitácora, el origen es el flujo **que lo llamó**, que recibe como parámetro.

## 2. `MPPP-REC` — Recibir correo nuevo

- **Disparador**: *When a new email arrives in a shared mailbox (V2)*. Buzón = `sanic_mppp_ev_buzoningesta`; carpeta = Bandeja de entrada; **Include Attachments = No** (los trae `MPPP-ING`; el disparador queda liviano y no falla por tamaño).
- **Concurrencia del disparador**: declarada grado 5 en `recursos/flujos/mppp-rec.json` (`runtimeConfiguration.concurrency.runs = 5`). Dos correos distintos no se pisan: cada uno tiene su clave. **Verificado hoy (2026-09-28) que el trigger DESPLEGADO no la tiene** (ver el recuadro de la sección 6, `MPPP-VIG`, que cubre los tres flujos): el repo la declara, el entorno no. Sin verificar la causa.
- **Acción única**: *Run a Child Flow* → `MPPP-ING`, con el identificador de Outlook del mensaje y `origen = MPPP-REC`.
- Sin condiciones ni lógica: todo lo que este flujo sabe hacer lo tiene que saber hacer la vigilancia.

## 3. `MPPP-ING` — Ingerir y validar correo

Entrada: identificador de Outlook del mensaje y origen. Salida: identificador de la Solicitud y resultado (`creada` · `ya_existia` · `error`).

| # | Acción | Detalle |
|---|---|---|
| 1 | *Get email (V2)* | Con adjuntos. Trae `internetMessageId`, remitente, asunto y fecha de recepción. |
| 2 | Componer la clave | `take(replace(replace(internetMessageId,'<',''),'>',''), 450)` (DD-03). |
| 3 | Filtrar adjuntos | `isInline` = false (DF-07). `cantidadadjuntos` = cuántos quedan; `cantidadexcel` = cuántos de esos adjuntos terminan en `.xlsx` (D-15). El Excel candidato es el único que termina en `.xlsx`; si hay cero o más de uno no se sube ninguno, y la regla correspondiente falla en la validación, que es donde tiene que fallar. |
| 4 | *Add a new row* → Solicitud | `sanic_messageid`, remitente, asunto, `sanic_fecharecibido` (la del correo), `sanic_fechaingresada` = `utcNow()`, estado = Ingresada, `sanic_cantidadadjuntos`, `sanic_cantidadexcel` (D-15). **Alta simple, nunca upsert por clave** (DD-03). |
| 4b | Si el alta falla por **clave duplicada** | La Solicitud ya existe. Se busca por `sanic_messageid` con *List rows*, se sigue desde el paso 7 con esa Solicitud, y el resultado es `ya_existia`. Cualquier **otro** error va al bloque de errores. |
| 5 | *Export email (V2)* → *Upload a file or an image* | El `.eml` a `sanic_correocrudo` (RF-10). Es lo que lee el plugin liviano. |
| 6 | *Upload a file or an image* | El Excel a `sanic_exceloriginal`, si hay exactamente uno (D-31). |
| 7 | *Move email (V2)* | A `sanic_mppp_ev_carpetaprocesados`. **El correo cambia de identificador al moverse**: el nuevo es el que devuelve esta acción. |
| 7b | *Update a row* → Solicitud | **Acción propia, inmediatamente después de mover, encadenada con `["Succeeded","Failed"]`**: `sanic_outlookmessageid` = `coalesce(id que devolvió el paso 7, id con que se llamó a este flujo)` (DF-04; `coalesce` agregado 2026-09-28, D-66). Corre aunque el paso 7 falle, y nunca deja la columna vacía: si `Mover_el_correo` no devuelve id, guarda el id de entrada en su lugar. Si aun así falla, el flujo sigue: `MPPP-ENV` encuentra el mensaje por su Internet Message-ID en la carpeta de procesados y completa la columna en ese momento. |
| 8 | *Perform an unbound action* → `sanic_mppp_capi_clasificarcorreo` | Parámetro `solicitudid`. Devuelve `procesar` (sí/no) y `clasificacion`. Si `procesar` = no, la API ya dejó la Solicitud en No es correo nuevo o en No reconocida, con su aviso a los ejecutivos: **el flujo termina acá, sin validar y sin responderle a nadie**. |
| 9 | *Perform an unbound action* → `sanic_mppp_capi_validarsolicitud` | Solo si `procesar` = sí. Parámetro `solicitudid`. Política de reintento de la acción: **ninguna**; el reintento lo gobierna la vigilancia, con su contador. |
| 10 | Bitácora | Evento "Ingresada", con el origen recibido, actor "Cuenta de servicio". |

**Orden deliberado**: el correo se mueve (7) **después** de tener la Solicitud y sus archivos, y **antes** de clasificar y validar. Si el flujo se cae antes del 7, el correo sigue en la bandeja de entrada y la vigilancia lo reingresa; la clave única evita el duplicado. Si se cae después, la Solicitud queda en Ingresada y la vigilancia retoma desde el paso 8: las dos API son idempotentes. **Corregido el 2026-10-09 (D-77):** esto era falso para `clasificarcorreo` y se afirmaba sin verificar. Su única guarda era "si la Solicitud no está en Ingresada, no se toca", pero cuando el correo se procesa la API graba sus `ResultadoRegla` y **deja la Solicitud en Ingresada** para que siga Validar; si Validar fallaba, todo reintento reventaba en Clasificar por la clave alternativa "Solicitud y código de regla". Ahora, con la Solicitud todavía en Ingresada, la API consulta si ya hay `ResultadoRegla` de alguna de las reglas de Correo evaluadas (códigos comparados sin distinguir mayúsculas, como la clave de Dataverse): si los hay, **recalcula y devuelve la decisión, pero no vuelve a crear los resultados**, y en el reintento feliz (`procesar` = sí) tampoco repite la Bitácora. Si la decisión recalculada es "no procesar" (cambió el catálogo, por ejemplo se quitó la autorización) hace el cambio de estado, el aviso y la Bitácora sin reescribir los resultados originales: falla cerrado, y lo decide el aprobador (`PENDIENTES.md` D-77). Cuesta una consulta más por llamada (`03` §0). **Reintentos de la ingesta:** esta sección no decía cómo se reintenta un error del flujo; según el aprobador (2026-10-09), la ingesta reintenta hasta 3 veces ante un error, además de la vigilancia (bloque 2 de `MPPP-VIG`). Ese mecanismo no está descrito paso a paso acá y conviene verificarlo contra el flujo desplegado.

**Errores**: los pasos van dentro de un ámbito *Try*. Un ámbito *Catch* (corre si *Try* falla o agota el tiempo) escribe la Bitácora con el evento "Error" y el nombre de la acción que falló, **sin el cuerpo del correo ni datos de la plantilla**, y termina el flujo como fallido para que quede en el historial. No hay limpieza: todo lo que queda a medias es recuperable.

## 4. `MPPP-COM` — Detectar comunicación pendiente

- **Disparador**: *When a row is added, modified or deleted*, tabla Solicitud, cambio = Modified, **Select columns** = `sanic_estadoprocesamiento`, **Filter rows** = estado En proceso, Rechazada o Procesada. No corre en cada actualización de la Solicitud (D-24) ni se dispara con las escrituras de `MPPP-ENV`, que no tocan el estado salvo para cerrar, y Cerrada queda fuera del filtro.
- **Concurrencia del disparador**: declarada grado 1 en `recursos/flujos/mppp-com.json` (DF-06). **Verificado hoy (2026-09-28) que el trigger DESPLEGADO no la tiene** (mismo hallazgo que en `MPPP-REC` y `MPPP-VIG`, ver el recuadro de la sección 6). Sin verificar la causa.
- **Acción única**: *Run a Child Flow* → `MPPP-ENV`, con el identificador de la Solicitud y `origen = MPPP-COM`.

## 5. `MPPP-ENV` — Enviar comunicación al cliente

Entrada: identificador de la Solicitud y origen. Decide sola qué comunicación toca; por eso la pueden llamar `MPPP-COM` y `MPPP-VIG` sin decirle nada más.

| Estado de la Solicitud | Acuse enviado | Respuesta final enviada | Qué hace |
|---|---|---|---|
| En proceso o Rechazada | no | — | Envía el **acuse** con `sanic_acusecontenido` |
| Procesada | sí | no | Envía la **respuesta final** con `sanic_respuestafinalcontenido` |
| cualquier otro caso | | | No hace nada y termina bien |

Secuencia de un envío, idéntica para las dos comunicaciones (D-21):

| # | Acción | Detalle |
|---|---|---|
| 1 | Releer la Solicitud | Si la fecha de **iniciado** de esa comunicación ya tiene valor → **no envía**. Si además la de enviado está vacía, marca `sanic_requiererevision` con el motivo "envío iniciado sin confirmar" y termina. Nunca se reenvía ante la duda. |
| 2 | Marcar **iniciado** | `sanic_fechaacuseiniciado` (o `…respuestafinaliniciada`) = `utcNow()`. Bitácora "Acuse iniciado". |
| 3 | *Reply to email (V3)* | Sobre `sanic_outlookmessageid`, buzón = `sanic_mppp_ev_buzoningesta`, **solo al remitente**, cuerpo HTML = el contenido guardado, sin adjuntos. Si el identificador no sirve, se busca el mensaje en la carpeta de procesados por su Internet Message-ID, se completa la columna y se reintenta una vez (DF-04). |
| 4 | Marcar **enviado** | La fecha de enviado = `utcNow()`. Bitácora "Acuse enviado". |
| 5 | Cerrar, si corresponde | Acuse de una **Rechazada** → Cerrada + `sanic_fechacerrada` (DD-09). Respuesta final de una **Procesada** → Cerrada + `sanic_fechacerrada`. Acuse de una En proceso → el estado no cambia. |

El cuerpo lo arma el código, nunca el flujo (DD-08). Si el paso 3 falla, la comunicación queda iniciada y sin enviar, y el paso 1 de la próxima ejecución la levanta para revisión humana: es el costo aceptado de garantizar que el cliente nunca reciba dos veces lo mismo.

**Texto obligatorio en las dos comunicaciones** (consecuencia de DF-08): *"Para enviar una plantilla nueva o corregida, escriba un correo nuevo a esta dirección. No responda a este mensaje: las respuestas no se procesan."*

## 6. `MPPP-VIG` — Vigilar pendientes

Disparador: recurrencia, cada 10 minutos. La mayoría de las ejecuciones termina en segundos sin nada que hacer. Es un sondeo, pero contra nuestro propio estado y contra un buzón que no avisa de "correo que quedó sin procesar": no aplica BP-PP-059.

**Encadenamiento (D-62, 2026-09-28).** La etapa de arranque (los cuatro `Parametro_*` + `Umbrales` + el bloque 1, `Correos_que_quedaron_en_la_bandeja`) y los cuatro eslabones ENTRE los cinco bloques de recuperación usan `runAfter: ["Succeeded","Failed"]`: una falla en un paso no corta la cadena, cada bloque corre igual. Antes del 2026-09-28, la etapa de arranque encadenaba solo con `["Succeeded"]`, y una falla ahí saltaba los cinco bloques de recuperación esa corrida entera. Verificado hoy contra el repo Y contra el `clientdata` desplegado (ver recuadro abajo): los nueve eslabones (5 de arranque + 4 entre bloques) tienen `["Succeeded","Failed"]` en el entorno, igual que en `recursos/flujos/mppp-vig.json`.

> **Hallazgo sin resolver, verificado hoy 2026-09-28 por Web API directa contra `workflows?$select=clientdata`** (no contra el repo, y no contra lo que dice `PENDIENTES.md`). El trigger `Cada_10_minutos` desplegado **NO tiene** `runtimeConfiguration.concurrency`, aunque `recursos/flujos/mppp-vig.json` sí lo declara (`runs: 1`) y aunque `PENDIENTES.md` D-62 y `pruebas/07-vigilancia.md` (sección "D-62 corregido") registran haberlo verificado desplegado ese mismo día. **No es un caso aislado de `MPPP-VIG`**: el mismo chequeo contra el trigger de `MPPP-REC` (el repo declara `runs: 5`) y el de `MPPP-COM` (el repo declara `runs: 1`) muestra que tampoco tienen la propiedad en el entorno ahora mismo, y esos dos nunca tuvieron un D- dedicado a verificar el punto. Lo que SÍ sigue desplegado correctamente son los nueve `runAfter` con `Failed` (ver párrafo de arriba) — eso no cambió. **No se pudo confirmar la causa**: coincide en el tiempo con el arreglo manual de D-67 (el aprobador corrigiendo a mano el trigger de `MPPP-VIG` en el portal, el mismo 2026-09-28, por el disparador que parecía no dispararse), pero eso no explica que `MPPP-REC` y `MPPP-COM` también carezcan de la propiedad. Se marca **sin verificar la causa** y se reporta; esta auditoría es de documentación y no corrige flujos ni datos del entorno — corresponde abrir un ID nuevo en `PENDIENTES.md` para decidir si se re-aplica.

| # | Qué busca | Cómo | Qué hace |
|---|---|---|---|
| 1 | Correos que quedaron en la bandeja de entrada | *Get emails (V3)*, Bandeja de entrada, recibidos hace más de `vigilancia.minutos.sinvalidar`, tope 25 | `MPPP-ING` por cada uno, con `origen = MPPP-VIG` |
| 2 | Solicitudes sin clasificar o sin validar | En Ingresada, con `sanic_fechaingresada` anterior al umbral y `sanic_requiererevision` = no | Si `sanic_reintentosvalidacion` < `vigilancia.reintentos.maximo`: suma uno y llama a las dos API en orden. Si no: `sanic_requiererevision` = sí, motivo "validación fallida N veces". **Nota D-77 (2026-10-09):** antes del arreglo, cuando el correo ya se había procesado, cada reintento moría en `clasificarcorreo` por clave duplicada y tapaba el error real de `validarsolicitud`; ahora Clasificar es idempotente en Ingresada y el error que se ve es el de Validar |
| 3 | Comunicaciones sin enviar | En proceso o Rechazada con acuse sin iniciar, o Procesada con respuesta final sin iniciar, más viejas que `vigilancia.minutos.sinresponder` | `MPPP-ENV` por cada una |
| 4 | Envíos iniciados sin confirmar | Fecha de iniciado con valor, fecha de enviado vacía, más vieja que el umbral, `sanic_requiererevision` = no | **No reenvía.** Marca `sanic_requiererevision` = sí |
| 5 | **Correos por clasificar vencidos** (DF-09) | No reconocida o No es correo nuevo, recibidos hace más de `clasificacion.dias.vencimiento` días. Solo en la primera ejecución de cada día | Estado → **Vencida** + `sanic_fechacerrada`. Bitácora "Vencida" |

Los umbrales salen de la tabla de Parámetros, leídos al empezar cada ejecución.

## 7. Qué le agrega este diseño al resto

| Dónde | Cambio |
|---|---|
| `02` Solicitud | Columna `sanic_outlookmessageid`. Estados nuevos **No es correo nuevo** y **Vencida**. Columna `sanic_motivoclasificacion`. |
| `02` catálogo de reglas | Nivel nuevo **Correo**, con la regla `ES_CORREO_NUEVO`. Parámetros `correo.prefijos.reenvio` y `clasificacion.dias.vencimiento`. |
| `03` | Custom API nueva, `sanic_mppp_capi_clasificarcorreo`, con su plugin liviano. |
| `05` | La bandeja "No reconocidos" pasa a **Por clasificar**, con los dos estados y la columna Motivo. |
| `06` | Cinco flujos con sus identificadores; dos Custom API en la fase 1. |
| `00` | Excepción E-15. |

## 8. Pendiente

> **Corregido el 2026-09-28.** Esta sección se escribió antes de construir (2026-09-21); desde entonces se construyó y se probó todo. Los dos puntos originales se resolvieron parcial o totalmente, y se agrega lo que esta auditoría encontró sin resolver.

- ~~Probar con correos reales, al construir: un correo nuevo, una respuesta y un reenvío, desde Outlook y desde Gmail, contra el buzón de Dev, para confirmar que el criterio de `03` §0 los separa bien.~~ **Con Outlook, resuelto con evidencia real**: `pruebas/01-clasificacion-del-correo.md`, casos C-01 a C-07 — incluye correo real contra el buzón de Dev por Graph (C-05, el correo se mueve y cambia de identificador; C-06) y un reenvío real distinguido de una respuesta por el prefijo del asunto, no por las cabeceras (C-03). **Con Gmail como remitente, sigue sin probarse**: no se encontró evidencia en `pruebas/` ni en `PENDIENTES.md` de un envío real desde una cuenta Gmail contra el buzón de ingesta. Marcado **sin verificar**.
- ~~Idioma base del Dev de BAC: a conversar con el aprobador.~~ **Resuelto**: `PENDIENTES.md` D-3 — el aprobador ya activó español a mano en el Dev (entorno de base inglés); no hace falta un paso nuevo acá.
- **Nuevo, sin resolver (encontrado en esta auditoría, 2026-09-28)**: el control de concurrencia del trigger de `MPPP-REC`, `MPPP-COM` y `MPPP-VIG` está declarado en el repo pero **no está presente en el entorno desplegado**, verificado por Web API directa. Ver el recuadro de la sección 6. Corresponde abrir un ID nuevo en `PENDIENTES.md`.
