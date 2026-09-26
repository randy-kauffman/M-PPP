# 03. Contratos de Custom API, plugins y máquina de estados

Proyecto: 2026-001-referencias-planes-pago · Etapa 3 · Estado: **revisado con el aprobador el 2026-09-18**

Reglas citadas: BP-PP-051, 052, 053, 055, 057, 060. Todas las Custom API: **unbound**, `AllowedCustomProcessingStepType = None`, **`IsPrivate = false` mientras la solución sea unmanaged** (se pone en `true` recién al empaquetarla managed para terceros), plugin de respaldo en el paquete `Sanic.Mppp.Plugins`. Sin llamadas HTTP salientes (D-22, BP-PP-052).

*(Corregido el 2026-09-22: decía `IsPrivate = true`, y eso **impide activar los flujos que llaman a la API**. `IsPrivate` bloquea que la Custom API aparezca en el documento `$metadata`, y de ahí la lee el conector de Dataverse para resolver una unbound action: con `true`, Power Automate falla con `GetMetadataForUnboundActionInput ... NotFound`. Además **no es una medida de seguridad**: Learn dice textual que "setting this property doesn't mean that other developers can't use your message if they know about it" — la seguridad real es `ExecutePrivilegeName`, que en las dos API es `prvCreatesanic_mppp_tbl_solicitud`. Microsoft recomienda dejarlo en `false` en desarrollo y ponerlo en `true` recién antes de shippear una solución managed. Ver [Create and use custom APIs](https://learn.microsoft.com/power-apps/developer/data-platform/custom-api#when-to-make-your-custom-api-private).)*

**Quién puede ejecutarlas** (D-9, decisión del aprobador del 2026-09-21). La plataforma gobierna la ejecución de una Custom API con `ExecutePrivilegeName`, que tiene que nombrar un privilegio **que ya exista**: no se puede crear uno a medida. Las dos de fase 1 (`clasificarcorreo` y `validarsolicitud`) llevan `ExecutePrivilegeName = prvCreatesanic_mppp_tbl_solicitud`: en fase 1 el único rol con `C` sobre Solicitud es `SR - MPPP - Servicio de ingesta` (`04` §3), y quien puede dar de alta una solicitud puede clasificarla y validarla. En fase 3 el rol Histórico también tendrá ese privilegio: se acepta o se revisa al diseñar esa fase. **Las dos de fase 2 (RPA) van con `ExecutePrivilegeName` VACÍO a propósito, y la puerta es el plugin** (D-9, decisión del aprobador del 2026-09-25). El motivo: el RPA **no tiene un solo privilegio propio** — todo lo que tiene lo tiene también el Ejecutivo (`04` §3), así que cualquier privilegio existente le abriría la API a los ejecutivos, y `registrarresultadorpa` transiciona filas a Digitada y a Aprobada: sería romper los cuatro ojos. Y elegir un privilegio de los que el Ejecutivo **no** tiene es peor: el único candidato es sobre Parámetro, que daría al bot escritura sobre `servicio.cuenta` y `rpa.puedeaprobar`, o sea la capacidad de auto-autorizarse a aprobar.

Se evaluó y se descartó la **tabla marcadora** que Learn recomienda para estos casos (*"Create a custom entity and use one of the privileges created for that entity"*; Microsoft la usa en `msdyn_ocagentassignedcustomapiprivilege`). No hace falta: **un humano no tiene `applicationid`**, nunca, así que un chequeo en el plugin cierra el hueco sin agregar un componente irreversible.

> **La puerta de las dos API de fase 2**: el que llama tiene que ser una **identidad de aplicación** (`systemuser.applicationid` con valor) **y** tener el rol **`SR - MPPP - RPA`**. Las dos condiciones, y falla cerrado. Se reusa `DeterminarRoles` (`StepsDeFila`), que ya resuelve los roles por `name` sin distinguir caja y grita si la solución está mal instalada.
>
> **Antes de evaluar nada, las dos API verifican que el rol `SR - MPPP - RPA` EXISTA en el entorno** (D-44, decisión del aprobador del 2026-09-25). Si no existe, el error dice eso y no otra cosa: *"el rol del RPA no está creado en este entorno (inventario 6.7); sin él ninguna llamada del bot puede autorizarse"*. **Por qué se paga esa consulta**: `DeterminarRoles` solo grita cuando faltan los TRES roles; si están Ejecutivo y Supervisor y falta únicamente el del RPA —el estado de todos los entornos hasta que se construya la fase 2— el bot recibiría un genérico "no autorizado" y quien lo lea saldría a revisar permisos, cuando el problema es que nadie creó un rol. Esta semana esa confusión costó horas dos veces: el `Name not found` que era falta de publicar (inventario §13.8) y el rechazo de la lista blanca que era falta del parámetro `servicio.cuenta`. **Un hueco de instalación disfrazado de problema de permisos se paga en horas de diagnóstico.**
>
> El rol se identifica por **`name`**, no por `roleid`: el nombre viaja con la solución, el id es de cada entorno (verificado el 2026-09-25: los cinco roles de MPPP tienen `roletemplateid = None` e id propios de Dev). La constante ya existe y ya sigue la convención: `TablasNativas.RolRpa = "SR - MPPP - RPA"` (`Datos/Avisos.cs`).

**Por qué el rol y no un parámetro.** Declarar la identidad del bot en la tabla de Parámetro era la alternativa, y es más barata todavía. Se descartó porque esa tabla ya gobierna `servicio.cuenta` (quién se salta la lista blanca) y `rpa.puedeaprobar` (si los cuatro ojos se pueden romper): sumarle la identidad del bot concentraría **tres decisiones de seguridad** bajo `prvWritesanic_mppp_tbl_parametro`, que tiene el **Administrador técnico** — el rol que justamente **no ve datos de negocio** (`04` §2). El rol que menos ve no puede ser el que más decide. Con el rol como puerta, la autorización queda donde vive la autorización.

**Lo que esto cuesta, dicho de frente**: la plataforma deja de frenar antes del plugin. Una tercera API del RPA que nazca sin el chequeo nace abierta. Se cubre con **una prueba de aceptación por API** que verifique el rechazo de un llamador no autorizado; sin esa prueba, esta decisión no está completa.

## 0. `sanic_mppp_capi_clasificarcorreo` — fase 1 (Action) · el plugin liviano

Responde una sola pregunta, **¿este correo merece procesarse?**, sin abrir la plantilla (`07` DF-08). Quién la llama: `MPPP-ING`, apenas el correo quedó guardado y movido; y `MPPP-VIG` en un reintento. Identidad: cuenta de servicio.

| Parámetro | Dirección | Tipo | Nota |
|---|---|---|---|
| `solicitudid` | entrada | Guid | |
| `procesar` | salida | Boolean | `true` → el flujo sigue con la validación. `false` → el flujo termina: no se valida y **no se le responde a nadie**. |
| `clasificacion` | salida | String | `nuevo` · `reenvio` · `respuesta` · `remitente_no_reconocido` · `ilegible` |
| `yaprocesada` | salida | Boolean | `true` si la Solicitud ya no estaba en Ingresada. |

**Algoritmo**
1. Leer la Solicitud. Si no está en Ingresada → devolver lo guardado, `yaprocesada = true` (idempotente, BP-PP-055).
2. Leer **solo las cabeceras** del `.eml` de `sanic_correocrudo`: se descarga el primer bloque del archivo y se toma hasta la primera línea en blanco, con un tope de 256 KB, uniendo las líneas de continuación. **Es entrada no confiable**: nunca se interpreta el cuerpo, nunca se siguen enlaces, y si las cabeceras no se pueden leer la clasificación es `ilegible` y el correo va a Por clasificar. Ante la duda, lo ve una persona.
3. Evaluar las reglas activas de **nivel Correo**, con el mismo motor de orden y dependencias (DD-13), y dejar un `ResultadoRegla` por cada una:
   - `ES_CORREO_NUEVO`. **Nuevo**: no trae `In-Reply-To` ni `References`. **Reenvío**: trae alguna, y el asunto empieza con un prefijo de la lista `correo.prefijos.reenvio` (`FW:`, `FWD:`, `RV:`, `REENV:`), sin distinguir mayúsculas. **Respuesta**: trae alguna y el asunto no es de reenvío. Se cumple para nuevo y para reenvío.
   - `REMITENTE_RECONOCIDO` (depende de la anterior): el remitente tiene al menos una **autorización vigente** sobre algún plan (RF-02, RF-03). "Vigente" = todo activo; la evidencia no cuenta (`02` §2.4, D-42).
4. Resultado:
   - Respuesta o ilegible → estado **No es correo nuevo**, `sanic_motivoclasificacion` con el motivo ("es una respuesta a otro correo"; "no se pudieron leer las cabeceras"). `procesar = false`.
   - Remitente no reconocido → estado **No reconocida**, con su motivo. `procesar = false`.
   - Si no → la Solicitud sigue en Ingresada. `procesar = true`.
5. Cuando `procesar = false`: Bitácora "Correo clasificado" y **un aviso dentro de la app a todos los ejecutivos** (`05` DA-08). No se arma ninguna comunicación para el remitente.

Reenvíos y respuestas llevan las mismas cabeceras en los programas de correo más comunes; lo que los separa es el prefijo del asunto, que es una convención del programa y no una garantía. Por eso la lista es un parámetro, y por eso **el criterio se prueba con correos reales** al construir (`07` §8). Es liviano de verdad: no abre el Excel, no carga planes, hace un número fijo de consultas (las autorizaciones del remitente, hasta cuatro: §1 paso 6), y corre en milisegundos.

## 1. `sanic_mppp_capi_validarsolicitud` — fase 1 (Action)

Quién la llama: el flujo `MPPP-ING`, solo después de que `sanic_mppp_capi_clasificarcorreo` (§0) devolvió `procesar` = sí; y `MPPP-VIG` en un reintento. Identidad: cuenta de servicio. **Este es el plugin grande: abre la plantilla.** No decide si el correo merece procesarse; eso ya lo decidió el liviano.

| Parámetro | Dirección | Tipo | Req | Nota |
|---|---|---|---|---|
| `solicitudid` | entrada | Guid | sí | Solo el identificador (D-31). El Excel se lee de `sanic_exceloriginal`. |
| `estado` | salida | Integer | | Valor de `EstadoSolicitud` resultante. |
| `yaprocesada` | salida | Boolean | | `true` si no hizo nada porque ya estaba validada. |
| `filastotales` / `filasvalidas` / `filasrechazadas` | salida | Integer | | |
| `resumen` | salida | String | | Una línea para el run history. |

Los nombres de la tabla son el `uniquename`; `name` y `displayname` siguen `01-convenciones.md` §2.

**Algoritmo**
1. Leer Solicitud. Si `estadoprocesamiento` ≠ Ingresada → devolver lo guardado con `yaprocesada = true` (idempotencia, D-24, BP-PP-055).
2. **Atomicidad**: el plugin corre en la etapa principal de la Custom API, dentro de la transacción de Dataverse. Si falla a mitad, se revierte todo: nunca quedan Filas ni ResultadoRegla a medias, y por eso **no hace falta ningún borrado de limpieza** (la solución no borra nada fuera del job nativo, D-35). **Confirmado en el spike C-05 parte B.** Consecuencia: no existe ningún estado propio de "en validación" observable desde afuera — la transacción o termina en un estado final (No reconocida, Rechazada, En proceso) o se revierte entera y la Solicitud sigue en Ingresada; una Solicitud que sigue en Ingresada pasado el plazo es la señal de `MPPP-VIG`.
3. Cargar `plantilla.estructura`, `plantilla.listas`, `plantilla.obligatoriedad` (versión activa más alta) y anotar `versionparametros`.
4. **Reglas de solicitud** (nivel Solicitud; las de nivel Correo ya las evaluó el plugin liviano, §0): evaluar en `sanic_orden` las reglas activas y escribir un `ResultadoRegla` por cada una, con resultado **Cumplida, No cumplida u Omitida**. Una regla cuyas dependencias (`sanic_dependede`) no resultaron todas Cumplida **no se evalúa**: queda Omitida, y su razón dice qué regla la bloqueó (DD-13). Por ejemplo, sin adjunto, `ESTRUCTURA_PLANTILLA` queda Omitida por `TRAE_ADJUNTO`. **En los tres finales posibles** (No reconocida, Rechazada, En proceso) se escribe la Bitácora "Validación terminada" con el resultado: ningún camino termina sin dejar rastro.
   - Falla alguna con efecto **Envía a revisión** → estado **No reconocida**, haya o no filas válidas y **aunque además otra regla rechace**: no se le responde al cliente sin que lo mire una persona (D-39, 2026-09-21). Hoy ninguna semilla de nivel Solicitud usa ese efecto. Fin.
   - Si no, falla alguna con efecto **Rechaza** → estado **Rechazada**; se arma el acuse (RF-04, DD-09), que dice expresamente que no hay nada que procesar, que no recibirá otro correo por esta solicitud, y que corrija y reenvíe. Fin.
5. **Reglas de registro** (nivel Registro), por cada fila no vacía: mismo motor que el paso 4, con las reglas activas de ese nivel en su `sanic_orden` y respetando `sanic_dependede`. Las dependencias deciden qué se evalúa, pero **por fila no se guarda un historial regla por regla**: `sanic_mensaje` lleva solo los motivos de las reglas que fallaron (`definicion.md` D-05). El historial con Cumplida, No cumplida y Omitida es del **sobre** —el correo—, y vive en `ResultadoRegla` (paso 4). Reglas iniciales:
   - `LISTAS_VALIDAS`: Gestión, Clasificación, Tipo de identificación, Moneda y Banco contra `plantilla.listas`. Un valor inválido deja su columna vacía y el mensaje nombra el campo, sin citar el valor (DD-01, D-41).
   - `LARGOS_Y_FORMATO`: largos mínimos y máximos y formato de cada campo, según `plantilla.estructura`.
   - `PLAN_EXISTE`: normaliza el número de plan (mayúscula, relleno con ceros a 4) y lo busca activo. Si no existe, `sanic_planid` queda vacío. **Nunca se adivina un plan parecido.**
   - `FORMATO_11_SOLO_ACH` (depende de `PLAN_EXISTE` y `LISTAS_VALIDAS`): un plan formato 11 solo admite clasificación ACH (DD-17).
   - `OBLIGATORIEDAD` (depende de `PLAN_EXISTE` y `LISTAS_VALIDAS`): según Gestión × Clasificación × tipo de formato del plan, de `plantilla.obligatoriedad`. Versión inicial: todo obligatorio salvo Referencia (DD-16).
   - `REFERENCIA_FORMATO_11` (depende de `FORMATO_11_SOLO_ACH` y `OBLIGATORIEDAD`): en planes formato 11 deja en `sanic_referencia` la construida: código de banco (3 dígitos) + número de cuenta relleno con ceros **a la izquierda** hasta 17 = **20 caracteres exactos**, sin mirar la recibida (DD-10). **No se cumple** (la regla falla y la fila se rechaza) si la cuenta tiene más de 17 caracteres, si trae algo que no sea dígitos, o si el banco no tiene código de 3 dígitos en `plantilla.listas` (D-17). En los formatos 06 y 10 **no deriva nada**: `sanic_referencia` es la recibida, aunque venga vacía, y la convención la aplica quien digita (DD-15). En todos los casos la recibida se guarda aparte, tal cual vino, en `sanic_referenciarecibida`.
   - `MONEDA_DEL_PLAN` (depende de `PLAN_EXISTE` y `LISTAS_VALIDAS`): moneda de la cuenta = moneda del plan (RF-07).
   - `AUTORIZACION_CORREO_PLAN` (depende de `PLAN_EXISTE`): el remitente tiene una **autorización vigente** sobre ese plan (RF-02): todo activo (`02` §2.4; la evidencia no cuenta, D-42). Un solo mensaje, que nombra el plan: "Su correo no está autorizado sobre el plan X".
   - Estado de la fila: falla `AUTORIZACION_CORREO_PLAN` → **Sin autorización**, **gane o no** también otra regla con efecto Rechaza: Sin autorización tiene prioridad sobre Rechazada en validación, y `sanic_mensaje` lleva igual **todos** los motivos que fallaron (D-14). Si no falla `AUTORIZACION_CORREO_PLAN` pero falla cualquier otra con efecto Rechaza → **Rechazada en validación**; ninguna falla → **Validada**. Una regla Omitida no cuenta como falla por sí misma: la fila ya quedó rechazada por la regla que la bloqueó.
   - **No se validan duplicados dentro de la plantilla** (DD-12): el control está en AS400.

> **Cerrado el 2026-09-24: fuga de información por los mensajes al cliente.**
>
> Hasta esa fecha, un remitente autorizado sobre **un solo plan** podía enumerar el catálogo de planes del
> banco. El sistema respondía distinto según el caso —*"El plan no existe o no está activo"* contra *"Su correo
> no está autorizado sobre el plan X"*— y, peor, `AUTORIZACION_CORREO_PLAN` se evaluaba **después** de
> `FORMATO_11_SOLO_ACH`, `OBLIGATORIEDAD` y `MONEDA_DEL_PLAN`, así que una fila sobre un plan ajeno igual
> recibía los motivos de esas reglas. Probado en Dev: una fila para un plan ajeno con la moneda equivocada
> devolvía *"La moneda no es la del plan PRNA. Su correo no está autorizado sobre el plan PRNA."* — o sea, la
> existencia del plan **y su moneda**. Con 100 filas por correo son 100 sondeos, y los códigos de plan siguen
> convenciones que hacen barato adivinarlos.
>
> Tres cambios, dos de ellos **solo configuración**:
> 1. `AUTORIZACION_CORREO_PLAN` pasa al orden **40**, antes que las tres reglas que hablan del plan, y esas
>    tres pasan a **depender de ella**: sin permiso quedan Omitidas y no dicen nada.
> 2. `PLAN_EXISTE` y `AUTORIZACION_CORREO_PLAN` comparten el mismo `sanic_mensajecliente`:
>    *"El plan {plan} no existe o usted no está autorizado sobre él."*
> 3. `ArmadorRespuesta.EstadoParaElCliente` devuelve **la misma palabra** para `SinAutorizacion` y
>    `RechazadaEnValidacion`. Sin esto la corrección quedaba a medias: el mensaje era uniforme pero la columna
>    **Resultado** seguía separando los dos casos. Adentro los dos estados siguen siendo distintos.
>
> **Lo que cuesta**: un cliente que escriba mal un código de plan recibe un mensaje más vago. Es el mismo
> intercambio que hace un formulario de login al decir "usuario o contraseña incorrectos".

6. Catálogos cargados **una vez** por ejecución: planes por código (una consulta con `In`) y autorizaciones del remitente (un número FIJO de consultas: hasta cuatro, una por tabla —Autorizado, AutorizacionPlan, Plan, Cliente— encadenadas con `In`, para que "todo activo" se compruebe sin un join que el doble de pruebas no simula; revisión de código, 2026-09-21). Nunca una consulta por fila (N+1). Alta de filas y resultados **una por una con `Create`**: Microsoft desaconseja los mensajes de lote (`ExecuteMultiple`, `ExecuteTransaction`) dentro de un plugin, que ya corre en la transacción y no tiene latencia de red que ahorrar (Learn, "Don't use batch request types in plug-ins"; revisión de código, 2026-09-21).
7. Armar el contenido del acuse (`sanic_acusecontenido`, DD-08) fila por fila. Contadores (`filastotales`/`filasvalidas`/`filasrechazadas`), estado resultante — **Rechazada** si ninguna fila quedó Validada (mismo texto del paso 4, DD-09), si no **En proceso** —, `fechavalidada`, Bitácora. `sanic_fechaacuseiniciado`/`sanic_fechaacuseenviado` no los toca esta API: los marca `MPPP-ENV` al enviar (§6).

**Errores**: excepción de negocio esperable (Excel corrupto, hoja inexistente) = regla fallida, **no** excepción. Excepción real → `InvalidPluginExecutionException` con mensaje sin datos sensibles; la transacción se revierte, la Solicitud sigue en Ingresada, y `MPPP-VIG` la reintenta hasta `vigilancia.reintentos.maximo`, luego marca `sanic_requiererevision`. **Mala configuración del catálogo de reglas** —una regla ACTIVA sin evaluador en el código, una regla activa que depende de otra INACTIVA, o una regla de nivel Registro con efecto Envía a revisión— también es excepción real (`ConfiguracionDeReglasInvalidaException`): mismo camino, falla cerrado y ruidoso (D-13, D-20).

**Presupuesto de tiempo** (BP-PP-053): objetivo < 10 s. Medido en el spike C-05 parte B, dentro del sandbox: leer 25 o 100 filas tarda entre 8 y 20 ms en caliente, y cerca de 1 s la primera ejecución en frío. El lector no es el cuello de botella; lo que hay que cuidar son las consultas y las altas (paso 6).

## 2. `sanic_mppp_capi_obtenerfilaspendientes` — fase 2 (Function)

El bot **consulta**; la solución nunca lo llama a él (los bots de Automation Anywhere corren calendarizados).

| Parámetro | Dirección | Tipo | Nota |
|---|---|---|---|
| `maximo` | entrada | Integer | **Cuántas filas entregar como máximo en esta llamada.** El bot pide lo que puede procesar: `maximo = 10` devuelve 10, no 100. Defecto 100; **tope 500** (un valor mayor se recorta al tope, sin error); 0 o negativo se rechaza. |
| `claimminutos` | entrada | Integer | **Por cuánto tiempo el bot reserva las filas que se lleva.** Entero **mayor que 0**; mayor que `rpa.minutos.claimmaximo` se recorta al tope, igual que `maximo`. Ver "El claim" más abajo. |
| `filas` | salida | String (JSON) | Arreglo de filas en estado **Validada**, y de filas **Digitada** cuyo `digitadapor` es **exactamente el usuario que llama** (las que le falta aprobar). Ver "Qué filas entran" más abajo. |
| `entregadas` | salida | Integer | Cuántas filas trae **este** lote. Evita que el bot tenga que parsear para contar, y hace evidente en el log si el lote vino corto. |
| `pendientes` | salida | Integer | Cuántas quedan sin entregar **después** de este lote. Con esto el bot decide si vuelve a llamar enseguida o espera su próxima corrida; sin esto, 2.500 filas podrían esperar horas sin que nadie lo sepa. |

Por cada fila: `filaid`, solicitud, número de fila, estado, plan, **tipo de formato del plan**, gestión, clasificación, nombre, tipo y número de identificación, número de cuenta, moneda, banco **con su código de 3 dígitos**, `fechavalidada`, y **dos referencias**:

- `referencia`: la que rige. En planes formato 11 es la construida (DD-10); en los demás, la recibida.
- `referenciarecibida`: **tal cual vino en el Excel; si vino vacía, va vacía.** Siempre se entrega, también en formato 11.

**Los choices viajan como TEXTO, no como valor numérico** (`Inclusion`, `ACH`, `RUC`, `COR`, `BANPRO`, `validada`…): son los valores canónicos que ya declara `plantilla.listas` y los que el bot tipea en AS400. Un `159460001` no le dice nada a un bot de Automation Anywhere. Mismo criterio que `resultado` en §3. Y el **banco viaja partido en dos**, `banco` y `bancocodigo`: el bot no tiene por qué saber que BANPRO es 008.

**Todas las fechas llevan offset explícito** (`2026-10-05T14:52:07Z`).

**Orden: FIFO estricto** (decisión del aprobador, 2026-09-25), por `sanic_fechavalidada` y, a igualdad, por `sanic_numerofila`. **Por qué se fija en el contrato y no se deja al motor**: sin orden determinístico, un lote de 500 sobre 3.000 pendientes puede devolver siempre las mismas y **las del fondo no se digitan nunca** — la solicitud de un cliente queda esperando para siempre sin que falle nada ni nadie se entere. Lo primero que entró es lo primero que sale.

El bot no necesita conocer reglas de referencia ni códigos de banco: todo le llega resuelto.

### El claim: reserva con vencimiento

**Revierte "sin reserva de filas"** (decisión del aprobador, 2026-09-25). La decisión anterior aceptaba el riesgo de colisión con un ejecutivo porque *"la ventana es de minutos"*. **Esa premisa era falsa**: a 10 segundos por fila, 3.000 filas son **8,33 horas** de bot trabajando. En esa ventana un ejecutivo toma una fila y la graba en AS400 sí o sí.

Una columna nueva en Fila, y nada más:

```
sanic_claimhasta   (DateTime)   el instante en que la reserva VENCE
```

Se guarda el vencimiento, no el inicio: así "¿está tomada?" es `sanic_claimhasta > ahora`, sin cuentas, y la MDA muestra la columna tal cual (§4 de esta sección). La escribe **solo SYSTEM**, desde esta API; por eso **no entra a la lista blanca** de `04` §1 y ninguna persona la toca.

`obtenerfilaspendientes` **pone o refresca el claim** sobre cada fila que entrega, en la misma llamada: tomar y marcar, juntos.

**El claim NO excluye filas de futuras entregas al bot** (razón del aprobador, 2026-09-25, y corrige la propuesta inicial). El claim es **un candado contra las personas, no contra el bot**: nunca tuvo que impedirle al bot volver a tomar lo suyo. Si el bot pide más sin haber reportado lo que tiene, por algo es — y el caso que lo vuelve decisivo es que **el bot se caiga y reinicie**: perdió su lista en memoria, vuelve a pedir, y con exclusión recibiría **cero filas** (están todas tomadas por él mismo) y quedaría trabado hasta el vencimiento. Sin exclusión, pide, recupera lo suyo y sigue.

> **Limitación conocida, aceptada**: la columna no guarda **quién** tomó la fila. Con un solo bot no hace falta. Si algún día hubiera dos, B podría tomar lo de A: ese día se agrega el dueño, y no antes.

**Qué frena el claim.** Mientras `sanic_claimhasta` esté en el futuro, **una persona no puede transicionar esa fila**: el step de transición la rechaza nombrando la causa y la hora en que se libera. Es exactamente el problema que venimos a resolver.

**Qué NO frena, y esto es lo importante**: el claim **no frena al bot cuando reporta**. El claim protege **tomar**, no **reportar**. Si el bot tardó 12 minutos con un claim de 10 y le rechazáramos el reporte, pasaría esto:

```
el bot digitó la fila EN AS400  →  el claim venció  →  rechazamos el reporte
                                                         ↓
                    AS400 dice "digitada"  ·  Dataverse dice "validada"
```

**Divergencia con el core bancario**, y después alguien la digita de nuevo porque para Dataverse sigue pendiente. Lo que ya pasó en AS400 se registra siempre; si mientras tanto una persona la atendió, ya está la red de §3: gana el que llegó primero y `estadofila` dice la verdad.

**Cuándo se libera.** Al vencer, y al llegar la fila a un estado terminal (Aprobada, Rechazada en AS400, Anulada) o al marcarse para atención manual. **En `Validada → Digitada` el claim NO se libera**: al bot le queda trabajo sobre esa fila, aprobarla.

**Sin botón "Liberar" por ahora** (decisión del aprobador, 2026-09-25): el tope `rpa.minutos.claimmaximo` acota el daño de un bot colgado, y agregar el botón después es barato. Meter una columna más en la lista blanca sin necesitarla, no.

### Qué filas entran, y por qué

**Para aprobar, solo las que digitó ÉL MISMO** (razón del aprobador, 2026-09-25). No es una regla de permisos: es una regla de responsabilidad. El bot aprueba en AS400 porque **le consta** que la digitación se hizo bien — la hizo él, sabe exactamente qué tecleó y sabe que no modificó nada más. **De lo que hizo un ejecutivo no le consta nada**, y aprobarlo sería firmar un trabajo que no vio. Quien aprueba lo que digitó una persona es otra persona: ahí los cuatro ojos son dos humanos, como siempre.

La comparación es contra **el usuario que llama**, no contra "cualquiera con el rol del RPA": si el día de mañana hubiera dos bots, ninguno aprueba lo del otro, por el mismo motivo. `sanic_digitadapor == InitiatingUserId`, exacto.

**No se entrega nada que esté marcado para atención manual** (`sanic_requiereatencionmanual`): esa fila ya es de un ejecutivo.

**Y si `rpa.puedeaprobar` vale `no`, no se devuelve ninguna Digitada: solo Validadas.** Con el aval de C-03 pendiente, esas filas son de un supervisor, no del bot. Entregárselas sería darle trabajo que le vamos a prohibir: intentaría aprobar, fallaría, y volvería a intentarlo en cada corrida **durante meses**, llenando el log de errores normales hasta enterrar el primer error de verdad. El día que llegue el aval se cambia **un parámetro** y las filas aparecen solas. Cero código condicional del lado del bot.

**El usuario de aplicación del RPA NO entra al column security profile** (decisión del aprobador, 2026-09-25; corrige lo que decía este párrafo). El motivo por el que lo decía era falso: el plugin de la Custom API lee y arma el JSON **con SYSTEM** (`04` §1), y `04` §4 dice textual que *"SYSTEM (la Custom API) no necesita estar en ningún perfil"*. **El bot nunca lee esas columnas: lee un JSON.** Meterlo en el perfil le daría un privilegio permanente de leer cuenta e identificación **por cualquier vía**, para algo que no necesita — y encima traería una incógnita que no hace falta comerse: a ese perfil se entra **por equipo desde un grupo de Entra** (D-8), y si un service principal sincroniza a un equipo de grupo está sin verificar.

## 3. `sanic_mppp_capi_registrarresultadorpa` — fase 2 (Action, D-14)

En AS400 el bot **digita y después aprueba**: son dos eventos distintos, separados por segundos, y así se registran. Es **una sola API** que el bot llama **una vez por cada cosa que pasó**, diciendo qué pasó. Ninguna llamada lleva la fila de un salto a Aprobada.

| Parámetro | Dirección | Tipo | Req | Nota |
|---|---|---|---|---|
| `filaid` | entrada | Guid | sí | El que recibió de `obtenerfilaspendientes`. |
| `resultado` | entrada | String | sí | `digitada` · `aprobada` · `rechazada`. Texto y no número: el contrato se lee solo desde el lado del bot. |
| `mensaje` | entrada | String | si `rechazada` | **El texto de la condición tal cual lo mostró AS400**, sin interpretar. Va a **`sanic_notainterna`**, que lee el ejecutivo y **nunca sale del banco**. |
| `codigo` | entrada | String | sí | **La clasificación de lo que pasó, según el bot.** No es texto libre: es una clave del catálogo `rpa.codigos`, y de ahí sale —traducido por nosotros— lo que lee el cliente. Ver "Los dos textos" más abajo. |
| `fechaproceso` | entrada | DateTime | sí | Cuándo ocurrió en AS400. **Con offset explícito** (`2026-10-05T15:04:12Z` o `...-06:00`); una fecha sin offset se rechaza. Se valida contra una ventana de tolerancia (ver abajo). |
| `estadofila` | salida | **String** | | Estado en que quedó la fila, **como texto** (`digitada`, `aprobada`, `rechazadaas400`, `anulada`…). **Es el campo que manda**: dice la verdad sobre la fila, la haya cambiado esta llamada o no. |
| `yaregistrado` | salida | Boolean | | `true` si la llamada no cambió nada. **Es informativo, no decisorio** (ver idempotencia). |

| `resultado` | Desde | Hacia | Efecto |
|---|---|---|---|
| `digitada` | Validada | **Digitada** | `sanic_digitadapor` = usuario de aplicación del RPA, `sanic_fechadigitada` = `fechaproceso` |
| `aprobada` | Digitada, digitada por el propio bot | **Aprobada** | `sanic_aprobadapor` = el mismo usuario, `sanic_fechaaprobada` = `fechaproceso`. Solo si `rpa.puedeaprobar` vale `si`; si vale `no` (sin el aval de C-03) devuelve error, la fila queda Digitada y la aprueba un supervisor |
| `rechazada` | Validada **o** Digitada | **Rechazada en AS400** | AS400 no aceptó la operación por una condición suya, al digitar o al aprobar. La API sabe en qué paso fue por el estado en que estaba la fila, y lo anota en la Bitácora |
| `atencionmanual` | Validada **o Digitada** | **no cambia** | El bot se topó con algo que no sabe resolver. Prende `sanic_requiereatencionmanual`, deja el porqué en `sanic_notainterna` y **libera el claim**. La fila pasa a ser de un ejecutivo. **Vale también sobre una Digitada** (corregido el 2026-09-25): restringirlo a Validada dejaría al bot sin forma de reportar un problema no clasificable **mientras está aprobando**, que es justo cuando AS400 le puede contestar algo raro |

**Cómo lo llama el bot.** Una llamada HTTP al Web API de Dataverse, con un token de Entra ID obtenido con las credenciales de su aplicación (flujo de credenciales de cliente):

```http
POST https://<entorno>.crm.dynamics.com/api/data/v9.2/sanic_mppp_capi_registrarresultadorpa
Authorization: Bearer <token>
Content-Type: application/json

{ "filaid": "5f0c…", "resultado": "digitada", "fechaproceso": "2026-10-05T15:04:12Z" }
```

y segundos después, cuando aprueba:

```json
{ "filaid": "5f0c…", "resultado": "aprobada", "fechaproceso": "2026-10-05T15:04:31Z" }
```

Si AS400 no lo aceptó:

```json
{ "filaid": "5f0c…", "resultado": "rechazada", "mensaje": "CUENTA INACTIVA", "fechaproceso": "2026-10-05T15:04:12Z" }
```

Respuesta: `{ "estadofila": 159460004, "yaregistrado": false }`.

**Reglas del contrato**

- **Solo se reporta lo que pasó en AS400.** `rechazada` es un motivo de negocio (cuenta inexistente o inactiva, titular que no se parece, moneda que no coincide, plan inactivo, referencia repetida o inexistente). Si AS400 está caído o el bot se rompe, el bot **no llama**: la fila sigue como estaba y se reintenta en la próxima corrida (acordado con el aprobador).
- **El bot nunca anula.** Anular es la decisión de una persona (DD-14).
- **Idempotente** (BP-PP-055): repetir un resultado sobre una fila que ya está en ese estado devuelve `yaregistrado = true` sin cambiar nada. Si la fila ya no está en el estado de partida porque una persona la atendió antes, tampoco cambia nada ni da error: devuelve el estado actual. El que llegó primero gana.
- **El bot decide por `estadofila`, NUNCA por `yaregistrado`.** `yaregistrado = true` tapa dos situaciones opuestas: *"ya me lo dijiste, seguí adelante"* y *"una persona llegó antes y la fila está en otro estado, no la toques más"*. Con el mismo booleano para las dos, un bot que reporta `digitada` sobre una fila que un ejecutivo **anuló** creería que puede aprobarla, y se pondría a pelear contra una persona. `estadofila` viene siempre y dice la verdad: si no es lo que el bot esperaba, el bot suelta esa fila. Por eso viaja como texto legible y no como número.
- Aplica las mismas transiciones del plugin de §4, en nombre del usuario de aplicación del RPA.

**El texto de AS400 NO sale al cliente** (decisión del aprobador, 2026-09-25). El contrato decía que `mensaje` era *"de donde sale lo que se le dice al cliente"*. Eso abre un caño directo desde el core bancario hasta la bandeja de un cliente, sin que lo lea nadie: si AS400 contesta algo como `CUENTA PERTENECE A OTRO TITULAR: <nombre>`, ese nombre saldría. Y rompe la premisa sobre la que está construida toda la defensa de `ArmadorRespuesta.DefenderMensaje`, que dice textual: *"`sanic_mensaje` lo redacta el catálogo (D-19, marcador `{valor}`) y hoy nunca cita la cuenta ni la identificación completas"* — la máscara solo tapa la cuenta y la identificación **de esa misma fila**, si aparecen literales. En fase 2 el texto deja de redactarlo el catálogo.

### Atención manual: una MARCA, no un estado

El bot se topa con algo que no tiene mapeado y lo manda a un humano. Se resolvió con **una marca sobre la fila, no con un estado nuevo** (decisión del aprobador, 2026-09-25):

```
sanic_requiereatencionmanual  (sí/no)   la prende SOLO el bot
sanic_notainterna                        el porqué, con el texto crudo de AS400
```

**Por qué no un estado.** Primero, porque **la fila sigue siendo Validada**: lo que le falta es exactamente lo que le falta a cualquier Validada, que alguien la digite; un estado nuevo obligaría a redeclararle **todas** las transiciones de Validada sin agregar un solo camino. Segundo, y pesa más: un estado nuevo entra a `EstadoParaElCliente`, que es **el lugar exacto donde el 2026-09-24 se cerró la fuga de información** colapsando estados — más superficie ahí, y sin aportarle nada al cliente, para quien la fila sigue en curso. Tercero: **este patrón ya existe y funciona** — `sanic_requiererevision` en Solicitud es lo mismo, con su vista *Para revisar* y su botón *Revisado*.

**Se apaga sola.** La marca la limpia **cualquier transición exitosa de la fila** (aprobador, 2026-09-25): si un ejecutivo o un supervisor la digita, la aprueba, la anula o la lleva a cualquier estado terminal, la marca se va sin que nadie tenga que acordarse. Y además hay botón **"Atendida"** para el ejecutivo que la resolvió sin cambiarla de estado — por eso esta columna **sí entra a la lista blanca** de `04` §1, como ya entró `sanic_requiererevision` en Solicitud.

### Los dos textos: el bot manda uno y clasifica; nosotros traducimos

Son **dos campos distintos con dos destinos distintos** (decisión del aprobador, 2026-09-25):

| Campo que manda el bot | Dónde va | Quién lo lee |
|---|---|---|
| `mensaje` — el texto de AS400 **tal cual** | `sanic_notainterna` | el ejecutivo. **No sale del banco** |
| `codigo` — la **clasificación** de lo que pasó | se traduce con `rpa.codigos` → `sanic_mensaje` | **el cliente** |

**El bot nunca redacta lo que lee un cliente.** Manda una clave; el texto lo pone el catálogo, que es nuestro y lo aprueba alguien. Es la misma separación de columnas que se construyó el 2026-09-24 para la nota del supervisor al devolver (`04` §1).

**El catálogo crece de a poco.** El parámetro `rpa.codigos` arranca con lo mínimo —un código de OK y uno de rechazo genérico— y se le agregan condiciones de AS400 a medida que el negocio las mapee y apruebe su texto. Forma:

```json
{
  "OK":       { "cliente": null, "nota": "AS400 aceptó la operación" },
  "RECH-GEN": { "cliente": "<la frase única para el cliente>",
                "nota": "Rechazo de AS400 sin clasificar todavía" }
}
```

**Un código que no está en el catálogo es un fallo NO reintentable** (`MPPP-RPA-005`): la fila no se mueve y el bot no debe volver a intentarlo hasta que alguien agregue el mapeo. Falla cerrado a propósito: **una fila trabada que alguien nota es infinitamente mejor que un texto equivocado en la bandeja de un cliente.** Y como la fila queda en su estado anterior, la Solicitud no avanza y termina cayendo en la vigilancia de `MPPP-VIG`, que es la red que ya existe.

**Todos los rechazos de AS400 que no tengan un código propio aprobado comparten el MISMO texto al cliente.** Eso no es pereza: es lo que se decidió el 2026-09-24 al cerrar la fuga de información. Un texto que distingue casos es un oráculo, y un oráculo se explota. Si el negocio quiere que el cliente vea motivos específicos, cada motivo entra al catálogo **uno por uno, con alguien que lo aprueba** — nunca por reenvío automático.

### Los errores llevan código, para que el bot sepa si reintentar

Hoy todo lo que falla sale como excepción con prosa, y el bot no puede distinguir *"reintentá"* de *"esto está roto"*: o reintenta para siempre, o abandona para siempre. Cada error empieza con una clave de una lista cerrada, y el bot decide por el prefijo:

| Código | Qué pasó | ¿Reintentar? |
|---|---|---|
| `MPPP-RPA-001` | `filaid` no existe | **no** |
| `MPPP-RPA-002` | `resultado` no es `digitada`, `aprobada`, `rechazada` ni `atencionmanual` | **no** |
| `MPPP-RPA-003` | `fechaproceso` sin offset, en el futuro, o fuera de `rpa.horas.tolerancia` | **no** — está mal el reloj del bot |
| `MPPP-RPA-004` | falta `mensaje` en un `rechazada` **o en un `atencionmanual`** | **no** — los dos escriben `sanic_notainterna`, y sin texto no explican nada |
| `MPPP-RPA-005` | `codigo` no está en `rpa.codigos` | **no** — falta mapearlo |
| `MPPP-RPA-006` | el que llama no es el bot autorizado (D-9) | **no** |
| `MPPP-RPA-007` | el rol `SR - MPPP - RPA` no existe en el entorno (D-44) | **no** — falta instalarlo |
| *(sin código)* | cualquier otra cosa: plataforma, tiempo de espera | **sí** |

### Se reporta por lote

**Por qué** (aprobador, 2026-09-25): a una llamada por hecho, 3.000 filas son 6.000 llamadas —digitar y aprobar— más las de consulta. Consume cuota y hace lento al bot. El lote acepta **de 1 a `rpa.lote.maximo`** resultados en una llamada, cada uno con la misma forma de arriba.

**Falla parcial: NUNCA todo o nada.** Si el ítem 23 de 50 trae un código sin mapear y rechazáramos el lote entero, **perderíamos 49 hechos reales de AS400**. La respuesta es **por ítem**, y la llamada devuelve **200**:

```json
{ "resultados": [
    { "filaid": "5f0c…", "estadofila": "digitada", "yaregistrado": false },
    { "filaid": "a1b2…", "estadofila": "validada", "yaregistrado": true,
      "error": "MPPP-RPA-005: el código RECH-XYZ no está en rpa.codigos" }
  ],
  "procesadas": 49, "conerror": 1 }
```

Los códigos `MPPP-RPA-001` a `007` son **por ítem**, no de la llamada. El error HTTP queda para lo que rompe la llamada entera:

| Código | Qué pasó | ¿Reintentar? |
|---|---|---|
| `MPPP-RPA-008` | el lote trae **más de `rpa.lote.maximo`** ítems: **se rechaza entero** | **no** — que lo parta el bot |
| `MPPP-RPA-009` | el lote viene vacío | **no** |

**El lote grande se rechaza entero, y `maximo` en §2 se recorta al tope. La asimetría es a propósito**: `maximo` pide trabajo, y darle menos no pierde nada; un lote son **hechos que ya ocurrieron en AS400**, y procesar solo los primeros X **perdería el resto en silencio**. Quien "unifique" estos dos comportamientos más adelante va a romper esto.

**El plugin no puede tirar excepción por un ítem.** Un plugin corre **dentro de una transacción**: si tira en el ítem 23, revierte los 22 anteriores. Atrapa por ítem y anota; nunca deja subir.

**El tope no lo manda la cuota, lo manda el reloj.** Toda operación de Dataverse tiene **2 minutos** (BP-PP-053). `rpa.lote.maximo` se calibra contra ese presupuesto, midiendo, no estimando.

**`fechaproceso`: ventana de tolerancia parametrizable** (decisión del aprobador, 2026-09-25). `sanic_fechadigitada` y `sanic_fechaaprobada` son el rastro de los cuatro ojos: un bot con el reloj o la zona horaria mal los envenena en silencio. Se rechaza toda fecha **en el futuro** o más vieja que la tolerancia, con un error que nombra la causa. La ventana la declara el parámetro **`rpa.horas.tolerancia`**, con un **fallback quemado en el código** para que la ausencia del parámetro no tumbe al bot (mismo criterio que `EsCodigoDeServidor` con `servicio.cuenta`: un dato de configuración que falta degrada, no rompe). **Todas las fechas, de entrada y de salida, llevan offset explícito.**

**Concurrencia: sin reserva de filas.** Decisión del aprobador: el control de concurrencia lo tiene AS400, que rechaza la segunda operación sobre la misma referencia. Riesgo residual aceptado: si un ejecutivo ya digitó una fila en AS400 pero todavía no la marcó en la app, y en ese momento el bot la intenta, AS400 la rechaza por repetida y el bot la reportaría como rechazada. Se mitiga por operación —con el bot activo, las personas digitan solo lo que el bot dejó— y porque la ventana es de minutos.

## 4. Plugin de transición de estado — fase 1 (D-25)

Step: `Update` de `sanic_mppp_tbl_fila`, **PreOperation**, síncrono, filtering attributes = `sanic_estado`, con **pre-image** (`sanic_estado`, `sanic_digitadapor`, `sanic_solicitudid`). Orden de ejecución 1 (BP-PP-060). Un segundo step **PostOperation** escribe la Bitácora y evalúa el paso de la Solicitud a Procesada (ver **Cierre** más abajo). **El RPA es un actor, no un estado**: cuando digita o aprueba, la fila pasa por los mismos estados Digitada/Aprobada que con una persona; el usuario de aplicación del RPA queda en `sanic_digitadapor`/`sanic_aprobadapor` igual que cualquier otro usuario.

| De | A | Quién | Condición | Efecto |
|---|---|---|---|---|
| Validada | Digitada | Ejecutivo, o el usuario de aplicación del RPA | | `digitadapor` = usuario que llama, `fechadigitada` = ahora |
| Validada o Digitada | Rechazada en AS400 | Quien digita: Ejecutivo, o el usuario de aplicación del RPA | `mensaje` obligatorio: la condición por la que AS400 no aceptó la operación (DD-14) | Bitácora "Fila rechazada en AS400" |
| Validada o Digitada | Anulada | **Solo Ejecutivo** (acción **Anular**); el RPA nunca anula | `mensaje` obligatorio: por qué la fila está mal y no se puede componer. Caso típico: el supervisor la devolvió y el ejecutivo decide anularla en vez de corregirla | Bitácora "Fila anulada"; al cliente se le pide que la reenvíe |
| Digitada | Aprobada | Supervisor, **usuario ≠ `digitadapor`** (segregación) | **Excepción**: el usuario de aplicación del RPA puede aprobar lo que él mismo digitó **solo si** el parámetro `rpa.puedeaprobar` vale `si` (por defecto `no`, hasta contar con el aval de C-03), y siempre como un evento aparte: una llamada con `resultado = aprobada` (§3) | `aprobadapor` = usuario que llama, `fechaaprobada` = ahora |
| Digitada | Validada | Supervisor (acción **Devolver**; es lo único que puede hacer además de aprobar, DD-14) | `sanic_notainterna` obligatoria (DD-07 — confirmada por el aprobador: pasa en la operación real) | Limpia `digitadapor`/`fechadigitada`; Bitácora "Fila devuelta" |

| cualquier otra | | | | **Error**: "Transición no permitida" |

> **Corregido el 2026-09-24.** La nota de la **devolución** dejó de escribirse en `sanic_mensaje` y pasó a `sanic_notainterna`. `sanic_mensaje` es la columna que el correo al cliente muestra como **Motivos**, así que la nota que un supervisor le escribe al ejecutivo se le estaba mandando al cliente. Evidencia en correos ya enviados: una fila que terminó **Aprobada** le llegó al cliente con el motivo **“Devuelta”**. Rechazar en AS400 y Anular **sí** siguen escribiendo en `sanic_mensaje`: esos textos sí son para el cliente.


- **Quién es el actor: a SYSTEM se le cree, a todos los demás se los pisa.** Probado en el spike C-05 parte B (`spikes/c05-parseo-excel/CONCLUSIONES-B.md`, pregunta 5 bis): cuando un plugin actualiza con el servicio de SYSTEM, dentro del step anidado `UserId` **e** `InitiatingUserId` valen SYSTEM **en todos los niveles** de `ParentContext`, incluido el de la propia Custom API, y las `SharedVariables` no llegan a ningún nivel. No hay forma de preguntarle a la plataforma quién originó la llamada. La regla del step es entonces una sola:
  - Si quien llama **no** es SYSTEM (una persona, por la app o por Web API): `digitadapor`, `aprobadapor` y las fechas que vengan en el `Target` **se ignoran** y se pisan con `InitiatingUserId` y la hora actual. Con actualización directa el spike mostró que `InitiatingUserId` es confiable, y que un valor inventado en el `Target` queda sobrescrito.
  - Si quien llama **es** SYSTEM, el `Update` solo puede venir de código de servidor. El plugin de la Custom API del RPA (§3) toma **su propio** `InitiatingUserId` —que en su nivel, antes de elevar, sí es el usuario de aplicación del bot— y lo pone en el `Target` junto con `fechaproceso`; el step **confía en ese `Target`**. Ningún cliente externo puede hacerse pasar por SYSTEM, y la lista blanca de columnas (§5) le impide a cualquier otro mandar esas columnas.
  - La validación de la transición y la segregación de funciones (`aprobadapor` ≠ `digitadapor`, con la excepción de `rpa.puedeaprobar`) se evalúan **siempre**, sobre los valores que quedaron, venga de quien venga.
  - Así el RPA sigue **sin privilegio de escritura** sobre ninguna tabla (`04` §3). Alternativa descartada: que la API actualice con el usuario del bot, que en el spike sí conserva al actor; obligaría a darle `W` sobre Fila a una credencial que vive fuera del banco, en Automation Anywhere. Riesgo residual aceptado: otro plugin del entorno compartido que corra como SYSTEM podría escribir esas columnas; tendría que ser código desplegado a propósito por alguien con rol de personalizador.
- Ya no existe `sanic_usuarioas400aprobador`: el usuario que marca Aprobada **es** el aprobador de AS400, no hace falta una columna aparte.
- Las transiciones iniciales (alta de la fila con su estado) las hace la Custom API §1 en el `Create`, que este step no intercepta.
- **Cierre**: el step PostOperation revisa, tras cada cambio de estado de Fila, si la Solicitud sigue **En proceso** y ya no queda ninguna fila en Validada ni Digitada. Si es así, pasa la Solicitud a **Procesada**, pone `sanic_fechaprocesada` y arma `sanic_respuestafinalcontenido` (qué se hizo en AS400, qué no y por qué — DD-08). El paso final Procesada → Cerrada lo hace `MPPP-ENV` al enviar la respuesta final (§6); este plugin no cierra la Solicitud.

## 5. Otros steps

| Step | Mensaje / etapa | Qué hace |
|---|---|---|
| Lista blanca de columnas | Update de Fila y de Solicitud · PreOperation · **sin** filtro de atributos · orden 0 | Si quien llama es un humano, el `Target` solo puede traer las columnas permitidas (`04-matriz-privilegios.md` §1). Es el control que compensa que `W` en Dataverse sea sobre la fila entera |
| ~~Integridad de AutorizacionPlan~~ | — | **ELIMINADO el 2026-09-24**: el Autorizado ya no tiene cliente, así que la regla no tenía qué comparar |
| Nombre calculado | Create · PreOperation en Plan, AutorizacionPlan y Fila (Bitácora, Regla y ResultadoRegla llevan primaria autonumérica, `02` DD-20) | Completa `sanic_nombre`. **No** aplica a Autorizado ni a Parametro: en esas dos tablas la columna primaria es la clave de negocio (correo, código), no un valor calculado (BP-PP-192) |
| Normalizar y validar | Create y Update de Cliente, Plan, Autorizado y Parametro · PreOperation | Cliente: `sanic_cifbac` solo dígitos, relleno con ceros a 9; `sanic_cifcom` en mayúscula, `^[A-Z0-9 ]{9}\d{3}$`. Plan: `sanic_codigo` en mayúscula, relleno con ceros a 4, `^[A-Z0-9]{4}$`. Autorizado: `sanic_nombre` (correo) sin espacios, en minúscula, con formato de correo válido. Parametro: `sanic_nombre` (código) en minúscula, separado por puntos |
| Atender correo por clasificar | Update de Solicitud, filtro `sanic_estadoprocesamiento` · PreOperation · **con pre-image** (`sanic_estadoprocesamiento`) | Solo No reconocida o No es correo nuevo → Cerrada / Descartada; ya no completa columnas propias — quién atendió y cuándo queda en la Bitácora, con `sanic_actortexto` |

### 5.1 Lista cerrada de steps a registrar (P-09, inventario 8.2)

Las filas de arriba y las de §4 describen el COMPORTAMIENTO; cada una se expande en un `sdkmessageprocessingstep` por cada par (mensaje, tabla). Esta es la lista cerrada, contrastada contra las clases de `src/Sanic.Mppp.Plugins/Steps/` — son **20**. (El inventario decía "~17"; la expansión real dio 18 el 2026-09-22, y ese mismo día se detectó que faltaban los dos de la tabla Regla, que el inventario 7.10 pedía y no estaban en el código.) Todos síncronos, en el paquete `Sanic.Mppp.Plugins`.

| # | Clase (`Sanic.Mppp.Plugins.Steps.*`) | Mensaje | Tabla | Etapa | Orden | Filtro de atributos | Pre-image |
|---|---|---|---|---|---|---|---|
| 1 | `ListaBlancaStep` | Update | `sanic_mppp_tbl_fila` | PreOperation | 0 | — (sin filtro, a propósito) | — |
| 2 | `ListaBlancaStep` | Update | `sanic_mppp_tbl_solicitud` | PreOperation | 0 | — (sin filtro, a propósito) | — |
| 3 | `NormalizarYValidarStep` | Create | `sanic_mppp_tbl_cliente` | PreOperation | 10 | — | — |
| 4 | `NormalizarYValidarStep` | Update | `sanic_mppp_tbl_cliente` | PreOperation | 10 | `sanic_cifbac,sanic_cifcom` | — |
| 5 | `NormalizarYValidarStep` | Create | `sanic_mppp_tbl_plan` | PreOperation | 10 | — | — |
| 6 | `NormalizarYValidarStep` | Update | `sanic_mppp_tbl_plan` | PreOperation | 10 | `sanic_codigo` | — |
| 7 | `NormalizarYValidarStep` | Create | `sanic_mppp_tbl_autorizado` | PreOperation | 10 | — | — |
| 8 | `NormalizarYValidarStep` | Update | `sanic_mppp_tbl_autorizado` | PreOperation | 10 | `sanic_nombre` | — |
| 9 | `NormalizarYValidarStep` | Create | `sanic_mppp_tbl_parametro` | PreOperation | 10 | — | — |
| 10 | `NormalizarYValidarStep` | Update | `sanic_mppp_tbl_parametro` | PreOperation | 10 | `sanic_nombre` | — |
| 11 | `NombreCalculadoStep` | Create | `sanic_mppp_tbl_plan` | PreOperation | 20 | — | — |
| 12 | `NombreCalculadoStep` | Create | `sanic_mppp_tbl_autorizacionplan` | PreOperation | 20 | — | — |
| 13 | `NombreCalculadoStep` | Create | `sanic_mppp_tbl_fila` | PreOperation | 20 | — | — |
| 16 | `TransicionDeFilaStep` | Update | `sanic_mppp_tbl_fila` | PreOperation | 1 | `sanic_estado` | `sanic_estado,sanic_digitadapor,sanic_solicitudid` |
| 17 | `PostTransicionDeFilaStep` | Update | `sanic_mppp_tbl_fila` | PostOperation | 10 | `sanic_estado` | `sanic_estado,sanic_solicitudid,sanic_numerofila` |
| 18 | `AtenderPorClasificarStep` | Update | `sanic_mppp_tbl_solicitud` | PreOperation | 10 | `sanic_estadoprocesamiento` | `sanic_estadoprocesamiento` |
| 19 | `IntegridadDeReglaStep` | Create | `sanic_mppp_tbl_regla` | PreOperation | 30 | — | — |
| 20 | `IntegridadDeReglaStep` | Update | `sanic_mppp_tbl_regla` | PreOperation | 30 | `sanic_codigo,sanic_nivel,sanic_orden,sanic_dependede,sanic_efecto,statecode` | `sanic_codigo,sanic_nivel,sanic_orden,sanic_dependede,sanic_efecto,statecode` |

Notas que fija esta tabla y no estaban antes:

- **Los dos de la tabla Regla (#19 y #20) son el control PREVENTIVO del catálogo** (D-13, D-14, D-20; inventario 7.10). El motor ya falla cerrado en tiempo de ejecución, así que un catálogo malo no corrompe datos; lo que estos dos evitan es que alguien lo guarde y recién se entere cuando dejan de procesarse las solicitudes. El de `Update` lleva pre-image porque un `Update` parcial trae una sola columna y la regla hay que validarla entera. Faltaban: se detectaron el 2026-09-22, al cargar las 15 reglas semilla.
- **Cuatro steps llevan pre-image, no uno**: #16, #17, #18 y #20. El de atender por clasificar (#18) lee el estado de ORIGEN de la pre-image, igual que los dos de Fila: sin imagen registrada revienta. Estaba en el código desde 7.12 y faltaba acá.
- **El orden importa en Fila/Update**: lista blanca (0) corre antes que la transición (1), para que una persona no pueda colar una columna prohibida aprovechando un cambio de estado legítimo.
- **La lista blanca va sin filtro de atributos a propósito**: filtrar sería dejar pasar justamente los `Update` que traen columnas que nadie declaró.
- **`Upsert`, `UpdateMultiple` y `CreateMultiple` NO evitan el control, y no hacen falta steps extra** (cerrado el 2026-09-22 con cita de Learn; ver `PENDIENTES.md` §B). Dataverse funde los pipelines: un step sobre `Update` se ejecuta también en un `UpdateMultiple`, una vez por entidad, y un `Upsert` sobre una tabla estándar llama `Create` o `Update` según exista el registro. **Ojo**: en una tabla **elástica** esto NO vale — `Upsert` ahí no dispara ningún evento. Si alguna vez se convierte una de estas tablas a elástica, la lista blanca se cae en silencio.

## 6. Máquina de estados de la Solicitud

```
Ingresada ──(plugin liviano, §0)──┬─► No es correo nuevo ─┐
 (MPPP-ING)                       ├─► No reconocida ──────┴─(Ejecutivo)─► Cerrada | Descartada
                                  │                        └─(MPPP-VIG, a los 30 días)─► Vencida
                                  ▼ procesar = sí
                        (plugin de validación, §1)──┬─► Rechazada ─(MPPP-ENV envía el acuse)─► Cerrada
                                                    └─► En proceso ─(MPPP-ENV envía el acuse; el estado no cambia)
                                                            │ plugin posterior a la transición de Fila (§4):
                                                            │ ninguna fila queda en Validada ni Digitada
                                                            ▼
                                                        Procesada ─(MPPP-ENV envía la respuesta final)─► Cerrada
```

El archivado de la fase 3 va en una columna aparte, que se crea en esa fase, fuera de este choice.

| Quién | Transiciones que puede hacer |
|---|---|
| `MPPP-ING` | Alta en Ingresada |
| Plugin liviano, `clasificarcorreo` (§0) | Ingresada → No es correo nuevo \| No reconocida; o la deja en Ingresada para que siga |
| Plugin de validación, `validarsolicitud` (§1) | Ingresada → Rechazada \| En proceso |
| `MPPP-ENV` | Acuse de una En proceso: **no** cambia el estado, solo marca las fechas de iniciado y enviado. Acuse de una Rechazada → Cerrada. Respuesta final de una Procesada → Cerrada. Cada comunicación por separado: "iniciado" se marca antes de enviar, y si queda iniciada sin enviarse **no se reenvía**: se marca `sanic_requiererevision` (D-21) |
| Plugin posterior a la transición de Fila (§4) | En proceso → Procesada, cuando ninguna fila queda en Validada ni Digitada (pone `sanic_fechaprocesada` y arma `sanic_respuestafinalcontenido`) |
| `MPPP-VIG` | No es correo nuevo \| No reconocida → **Vencida**, pasados `clasificacion.dias.vencimiento` días. Aparte de eso no cambia estados: reinvoca las API, reingresa correos, o marca `sanic_requiererevision` |
| Ejecutivo | No es correo nuevo \| No reconocida → Cerrada (atendida) \| Descartada |

## 7. Lector de plantilla: el archivo es hostil hasta que demuestre lo contrario

El Excel llega por correo desde fuera del banco. Aunque el remitente esté autorizado, su cuenta puede estar comprometida. El lector corre dentro de un sandbox con tiempo y memoria limitados, compartido con el resto del entorno. Principio: **falla cerrado, nunca pierde datos callado, y su costo está acotado por diseño, no por suerte**. Origen: dos rechazos seguidos del revisor en el spike C-05 (2026-09-18), y la corrección del aprobador a la primera versión de estas reglas, que leía mucho más de lo necesario (LP-04, LP-05, LP-08).

| ID | Regla | Por qué |
|---|---|---|
| LP-01 | **Frontera de confianza única.** Todo el trato con Open XML SDK (abrir el paquete, ubicar partes, recorrer) vive dentro de un solo `try` cuyo `catch` general convierte cualquier excepción en "archivo inválido: <motivo>". La lógica propia (configuración, armado del resultado) queda **fuera** de ese `try`, para que un bug nuestro no se disfrace de archivo corrupto. | Enumerar los tipos de excepción que puede tirar un SDK de terceros es una lista que nunca se termina: el revisor encontró `FormatException` en una vuelta y `ArgumentOutOfRangeException` en la siguiente. |
| LP-02 | **Tope de bytes de entrada**, aplicado con copia acotada: se lee hasta `máximo + 1` bytes a memoria y se rechaza si se pasa. Nunca depende de `Stream.Length` ni de `CanSeek`. | Con un stream no seekable el chequeo por `Length` se salteaba entero. |
| LP-03 | **Tope de bytes descomprimidos**: antes de abrir con el SDK se recorre el zip y se suma el tamaño declarado de las entradas; si supera el tope, se rechaza. | 2,5 MB comprimidos eran 1,7 GB de XML (686:1). El tope sobre el contenedor no protege de nada. |
| LP-04 | **Solo se lee la ventana configurada.** `plantilla.estructura` dice la hoja, la fila de encabezado, la primera fila de datos, la cantidad de filas y, por cada campo, su columna. El lector lee la fila de encabezado y las filas de la ventana, y **deja de leer en la primera fila que queda más allá de la última configurada**. Dentro de cada fila lee solo hasta la última columna configurada. Nada de lo que está fuera de la ventana se mira: ni las otras hojas, ni las filas de más, ni las columnas de más. | Decisión del aprobador (2026-09-18): en la práctica son unas 11 columnas y unas 100 filas. El costo del lector queda atado a lo que se configuró leer, no a lo que el archivo traiga. Reemplaza al diseño anterior, que recorría el archivo entero hasta un tope de 5.000 filas. |
| LP-05 | **El orden se exige, no se supone.** Las filas tienen que venir en orden **estrictamente ascendente** (por su atributo `r`, o por su posición si no lo traen, LP-06), y lo mismo las celdas dentro de una fila. Una fila o celda fuera de orden, o repetida, es **archivo inválido**: se rechaza con motivo. | Es lo que hace seguro el corte de LP-04. El bug que encontró el revisor (una fila 500 escrita antes que la fila 6 hacía que la 6 se perdiera en silencio) venía de **suponer** el orden; recorrer todo el archivo lo evitaba, pero a un costo que no hacía falta. Excel siempre escribe en orden y trata como dañado un archivo que no lo está: rechazarlo es fallar cerrado, y además acota el recorrido a la ventana aunque el archivo traiga millones de filas. |
| LP-06 | **Fila sin atributo `r`**: su posición es la de la fila anterior + 1, como dice el estándar. Celda sin `r`: columna anterior + 1. Nunca se descartan. | Descartarlas era perder gestiones sin avisar. |
| LP-07 | **Nada se pierde callado.** Todo lo que el lector no puede interpretar termina como error del archivo o como advertencia con celda y motivo. Si duda, rechaza: el cliente reenvía la plantilla; una gestión perdida no la reclama nadie hasta que falla un pago. | |
| LP-08 | **Todo lo que gobierna la lectura es parámetro**, nada está fijo en el código: qué hoja, desde qué fila, cuántas filas, qué columnas y en qué posición (`plantilla.estructura`); peso máximo del archivo comprimido y peso máximo descomprimido (`lectura.limites`). El código trae valores por defecto conservadores solo para el caso de que el parámetro falte. | RF-05 y decisión del aprobador (2026-09-18). |

**Cómo se leen juntas LP-04 y LP-05 (D-12, aprobador, 2026-09-21).** El orden se exige en todo lo que el lector **llega a recorrer**: una fila o una celda que retrocede o se repite es archivo inválido. Una fila que está en orden pero más allá de la última de la ventana **corta la lectura sin invalidar nada** (caso real: pocas filas llenas y una nota al pie más abajo; Excel no escribe las filas vacías); una celda en orden pero más allá de la última columna configurada corta esa fila, y los campos que no aparecieron quedan vacíos. **Lo que venga después del corte no se mira, aunque fuera de la ventana**: un archivo armado a mano con la fila 500 antes que la 6 se lee como una plantilla sin filas. Detectarlo exigiría seguir leyendo, que es el costo no acotado que LP-04 eliminó; Excel nunca escribe ese archivo, y quien lo arme a propósito no consigue nada que no logre con no mandar esas filas. **El número de orden de una fila es su posición en la ventana** (la primera fila de datos configurada es la 1, tenga o no valores): es el número que el cliente ve impreso en su plantilla y el que va a `sanic_numerofila`. **Al cliente nunca le llega el mensaje de la librería**: si el archivo no se puede abrir recibe un texto propio, y lo que dijo la librería va solo a la traza del plugin.

Valores iniciales propuestos: ventana de 100 filas de datos y las 10 columnas de la plantilla vigente (B a K) más la A de numeración · 2 MB de archivo comprimido · 20 MB descomprimido. La plantilla real pesa 26 KB. La tabla de textos compartidos del Excel se carga entera y es lo único cuyo tamaño no depende de la ventana: lo acota el tope de peso descomprimido (LP-03).

## 8. Estructura del código

```
Sanic.Mppp.Plugins/
├── Api/            ValidarSolicitudApi · ObtenerFilasPendientesApi · RegistrarResultadoRpaApi
├── Steps/          FilaTransicionStep · FilaPostTransicionStep · AutorizacionPlanIntegridadStep · …
├── Dominio/        Solicitud, Fila, EstadoFila, Transiciones      ← sin dependencia del SDK
├── Validacion/     IReglaSolicitud, IReglaRegistro + una clase por regla
├── Plantilla/      ILectorPlantilla, LectorOpenXml, ConfiguracionPlantilla (JSON)
├── Respuesta/      ArmadorRespuesta (HTML, enmascarado)
└── Datos/          IRepositorio… sobre IOrganizationService
```

`Dominio`, `Validacion` y `Respuesta` no conocen Dataverse: se prueban con tests unitarios puros. Solo `Api`, `Steps` y `Datos` tocan `IOrganizationService`, y se prueban contra un **doble propio en memoria** (`OrganizationServiceEnMemoria`, en el proyecto de tests): guarda entidades en un diccionario, resuelve `Create`, `Retrieve`, `Update`, `RetrieveMultiple` por igualdad y por lista, y `Execute` para los pocos mensajes que el código usa. **No se usa FakeXrmEasy**: exige licencia comercial paga para uso en una empresa (`docs/licencias-terceros.md`). Lo que el doble no puede probar —el pipeline real, la transacción, la seguridad— se prueba en Dev, no se simula. Cuanto más fina sea la capa `Datos`, más barato es el TDD estricto acá.
