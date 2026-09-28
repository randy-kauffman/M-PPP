# 01 — Clasificación del correo

Set **C** (`CONVENCIONES.md` §4). Nivel **Correo**: `sanic_mppp_capi_clasificarcorreo` y sus dos
reglas, `ES_CORREO_NUEVO` y `REMITENTE_RECONOCIDO`.

Cadena de dependencias (`sanic_dependede`, verificada hoy contra `playbooks/datos/reglas.md`):

```
ES_CORREO_NUEVO → REMITENTE_RECONOCIDO
```

Las dos con efecto **Envía a revisión** (es el único efecto válido en nivel Correo, `02` §2.6): que
`ES_CORREO_NUEVO` dé No cumplida deja la Solicitud en **No es correo nuevo**; que
`REMITENTE_RECONOCIDO` dé No cumplida la deja en **No reconocida**. Ninguna de las dos manda
comunicación al cliente — ese es justamente el punto: un correo no reconocido, o una respuesta
automática, no tienen que generar una segunda vuelta de correos.

**Set autocontenido** (`CONVENCIONES.md` §8). Este archivo no depende de ningún otro: prepara sus
propios datos, no usa ninguna sesión de navegador (nivel Correo se prueba entero contra la Custom
API, sin abrir la app) y deja el canon de configuración global al terminar. La preparación exacta
está documentada en `herramientas/pruebas/sets/set_c.py::preparar()` — acá solo el resumen:

| Qué prepara | Cómo |
|---|---|
| Datos de negocio (`PR*`, fixture compartido) y `REMITENTE` autorizado sobre `PR11` | `preparacion_comun.verificar_datos_negocio()` (antiguo P-03) |
| Los 15 Excel + el adjunto no-Excel | `preparacion_comun.verificar_excels()` (antiguo P-04); este archivo solo usa `E01-feliz-formato11.xlsx` |
| `autorizacion.modo` activo (DN-01) | `preparacion_comun.verificar_parametro_activo()` — ya sembrado en Dev desde el 2026-09-26, no hace falta crearlo |
| `correo.prefijos.reenvio`, CONTENIDO (no solo que exista): lista no vacía y trae `FW:` | `preparacion_comun.verificar_prefijos_reenvio()` — recupera la cobertura del antiguo P-06 (2026-09-28) |
| `servicio.cuenta`, CONTENIDO (no solo que exista): tiene forma de UPN/correo | `preparacion_comun.verificar_servicio_cuenta()` — recupera la cobertura del antiguo P-06 (2026-09-28) |
| Autorizado + `AutorizacionCliente` privados de DN-01 (`REMITENTE_DN01`, autorizado SOLO por cliente, cero `AutorizacionPlan`) | `sets/set_c.py::_preparar_autorizacion_por_cliente()` (función local, no está en `preparacion_comun.py` — ver "Funciones para consolidar" al final) |

**Corré este set con**:

```bash
python3 herramientas/pruebas/correr.py C
```

---

## C-01 · Camino feliz — correo nuevo, remitente autorizado, Excel válido

**Qué prueba.** La cadena entera: clasificación (`ES_CORREO_NUEVO`, `REMITENTE_RECONOCIDO`) →
validación → referencia de formato 11.

**Necesita.** El `REMITENTE` de este set, autorizado sobre `PR11` (`preparar()`); el adjunto
`E01-feliz-formato11.xlsx`.

**Cómo.** Correo nuevo, asunto `Prueba C-01 inclusiones`, adjuntando **solo**
`E01-feliz-formato11.xlsx`.

**Qué tiene que pasar.**

| Qué | Valor |
|---|---|
| clasificación / procesar | `nuevo` / `true` |
| Estado | **En proceso** |
| filas t/v/r | `3 / 3 / 0` |
| Reglas de Correo y de Solicitud (las 7) | todas **Cumplida** |
| Las 3 filas | **Validada**, sin mensaje |
| Referencia de cada fila | `007` + cuenta rellena a 17 = **20 caracteres** (`00700010203040506070`, `00700011223344556677`, `00700099887766554433`) |
| Acuse | presente |

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py C` (caso `C-01`); a mano,
`ver_solicitud.py <MPPP-########>`.

**Cómo se sabe que falló.** Si alguna referencia mide distinto de 20 o no empieza en `007`, es
**FALLA grave**: esa referencia va a AS400 y un dígito corrido manda la plata a otra cuenta — no es
lo mismo que una regla mal evaluada (eso se ve en `ES_CORREO_NUEVO`/`REMITENTE_RECONOCIDO` en
Omitida o No cumplida) que un cálculo de referencia mal armado con las reglas en verde.

**Automatizable.** API (`correr.py C`, caso `C-01`, vía `simulador.py`, sin correo real). Con
correo real es manual: enviar y esperar 1-2 minutos.

---

## C-02 · Una respuesta NO se procesa

**Qué prueba.** `ES_CORREO_NUEVO` con una respuesta (DF-08): lo que impide un bucle de correos con
un "fuera de oficina".

**Necesita.** El `REMITENTE` de este set; el adjunto `E01-feliz-formato11.xlsx`; un
`In-Reply-To` simulado (`simulador.py` arma un `.eml` con esa cabecera).

**Cómo.** Correo con asunto `RE: algo anterior`, `In-Reply-To` apuntando a un mensaje anterior,
adjuntando `E01-feliz-formato11.xlsx`.

**Qué tiene que pasar.**

| Qué | Valor |
|---|---|
| clasificación / procesar | `respuesta` / `false` |
| Estado | **No es correo nuevo** |
| filas | **0** — no se abrió el Excel |
| `ES_CORREO_NUEVO` | **NO CUMPLIDA** |
| `REMITENTE_RECONOCIDO` | **Omitida** (la bloqueó la anterior) |
| Comunicación al cliente | **NINGUNA** |

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py C` (caso `C-02`).

**Cómo se sabe que falló.** Si `ES_CORREO_NUEVO` da Cumplida con `In-Reply-To` presente, la regla
no está mirando la cabecera. Si da correctamente No cumplida pero `REMITENTE_RECONOCIDO` NO queda
en Omitida (Cumplida o No cumplida), el motor de dependencias (DD-13) está evaluando una regla que
debería saltear — y si además hay acuse, hay un bucle de correo automático real, el peor de los dos
síntomas.

**Automatizable.** API (`correr.py C`, caso `C-02`, simula la respuesta sin correo real).

---

## C-03 · Un reenvío SÍ se procesa

**Qué prueba.** Que `ES_CORREO_NUEVO` distinga reenvío de respuesta por el **prefijo del asunto**
(`FW:`, `FWD:`, `RV:`, `REENV:`, de `correo.prefijos.reenvio`), no por la presencia de
`In-Reply-To`/`References` (un reenvío también las trae).

**Necesita.** El `REMITENTE` de este set; el adjunto `E01-feliz-formato11.xlsx`; un
`In-Reply-To` simulado (igual que C-02, a propósito: para aislar que lo que decide es el prefijo);
que `correo.prefijos.reenvio` traiga, en su CONTENIDO (no solo que la fila exista), el prefijo
`FW:` — verificado en `preparar()` (`preparacion_comun.verificar_prefijos_reenvio()`, 2026-09-28):
sin ese prefijo en la lista, este caso da No es correo nuevo por una razón de datos, no del sistema.

**Cómo.** Correo con asunto `FW: Prueba C-03 reenvio`, mismas cabeceras de "respuesta" que C-02,
adjuntando `E01-feliz-formato11.xlsx`.

**Qué tiene que pasar.** Igual que C-01: `nuevo`/`true`, **En proceso**, 3 filas Validadas,
`ES_CORREO_NUEVO` **Cumplida**.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py C` (caso `C-03`).

**Cómo se sabe que falló.** Si este caso queda en **No es correo nuevo** con las mismas cabeceras
que C-02 salvo el prefijo, la regla no está mirando el asunto — está copiando el criterio de
`In-Reply-To` de una respuesta común.

**Automatizable.** API (`correr.py C`, caso `C-03`).

---

## C-04 · Remitente no reconocido

**Qué prueba.** `REMITENTE_RECONOCIDO` (RF-02) en su forma de siempre: modo `plan`, sin ninguna
autorización.

**Necesita.** Un correo que **nunca** se registra como Autorizado (`REMITENTE_NO_AUTORIZADO`, no
hace falta ninguna preparación: la ausencia de la fila es justamente la condición del caso); el
adjunto `E01-feliz-formato11.xlsx`.

**Cómo.** Correo desde `REMITENTE_NO_AUTORIZADO`, con `E01-feliz-formato11.xlsx` adjunto.

**Qué tiene que pasar.**

| Qué | Valor |
|---|---|
| clasificación / procesar | `remitente_no_reconocido` / `false` |
| Estado | **No reconocida** |
| filas | **0** |
| `ES_CORREO_NUEVO` | Cumplida |
| `REMITENTE_RECONOCIDO` | **NO CUMPLIDA** |
| Comunicación | **NINGUNA** |

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py C` (caso `C-04`).

**Cómo se sabe que falló.** Si `REMITENTE_RECONOCIDO` da Cumplida para un correo que nunca se
autorizó, alguien está reconociendo por defecto (fail-open) — el peor tipo de defecto de esta
regla: cualquiera podría mandar filas a digitar sin autorización.

**Automatizable.** API (`correr.py C`, caso `C-04`, con un remitente propio no autorizado).

---

## C-05 · El correo se mueve y cambia de identificador

**Qué prueba.** El paso 7 de `MPPP-ING` y DF-04: el identificador de Outlook **cambia al mover**
(`MoveV2` sin "Immutable Id" habilitado en el tenant — D-66, `diseno/PENDIENTES.md`), y que
`MPPP-ING` guarda el identificador **NUEVO** (el de después de moverlo), no el viejo — que es
justo lo que hasta hoy rompía la re-ingesta de `MPPP-VIG`.

**Necesita.** Acceso real (Graph, vía `herramientas/graph_api.py`) al buzón de ingesta
`mppp@55xljh.onmicrosoft.com` — otorgado hoy por el aprobador ("ya le di acceso al buzón real"); un
buzón de prueba de Graph como remitente (`PP_MAIL_BUZON_1`); `MPPP-REC` y `MPPP-ING` activos
(`statecode=1`, se verifica contra `workflows` antes de correr, no se asume).

**Cómo (`c05()` de `set_c.py`).**
1. Manda un correo real por Graph, desde `PP_MAIL_BUZON_1` hacia `mppp@55xljh.onmicrosoft.com`, con
   asunto único `ZZP-C05C06-<nonce>` y un adjunto `.xlsx` válido con nombre propio (lo usa C-06).
2. Poll sobre la carpeta `Inbox` del buzón real hasta que aparezca (captura el identificador de
   Graph **ANTES** de `MoveV2`) — margen 90s (`MPPP-REC` sondea cada 1 minuto).
3. Poll sobre la carpeta `Procesados` hasta que aparezca ahí (captura el identificador **DESPUÉS**)
   — margen ~4-5 minutos.
4. Busca la Solicitud resultante en Dataverse por `sanic_messageid` (el Message-ID RFC822 del
   correo, estable entre carpetas — nunca el `id` de Graph, que es justo lo que cambia).

**Qué tiene que pasar.**

| Qué | Valor |
|---|---|
| El correo aparece en `Inbox` del buzón real | sí (identificador capturado antes de mover) |
| El correo termina en `Procesados` | sí |
| Identificador de Graph antes vs. después | **DISTINTOS** — esto es lo ESPERADO, no un defecto: `MoveV2` sin Immutable Id cambia el ID siempre |
| `sanic_outlookmessageid` guardado en la Solicitud | = el identificador de **DESPUÉS** de mover (NUNCA el de antes) |

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py C` (caso `C-05`, sin navegador, sin
acceso manual al buzón); a mano, `ver_solicitud.py <MPPP-########>` muestra `outlook message id: sí`.

**Cómo se sabe que falló.** Dos causas con el mismo síntoma ("no llegó a Procesados"): (a) el
correo nunca apareció en `Inbox` — Graph no entregó el envío, revisar el HTTP de `mandar_correo()`;
(b) apareció en `Inbox` pero nunca pasó a `Procesados` — `MPPP-REC` puede estar desactivado
(`statecode=0`; revisar `workflows` antes de reintentar, NUNCA asumir que está activo) o `MPPP-ING`
está fallando. Si SÍ llega a `Procesados` pero `sanic_outlookmessageid` guardado coincide con el
identificador de **ANTES** de moverlo (no con el de después), el arreglo del aprobador se revirtió:
es exactamente el bug de D-66 — una re-ingesta futura de `MPPP-VIG` va a fallar con 404 sobre ese
ID viejo.

**Automatizable.** `API` (`correr.py C`, caso `C-05`, vía `herramientas/graph_api.py` contra el
buzón real — sin navegador, `pruebas/gui/` no aplica). **Corrección 2026-09-28**: hasta hoy este
caso era `N-A` porque la Application Access Policy del service principal de Graph limitaba el
acceso a los 4 buzones de prueba (`PP_MAIL_BUZON_1..4`), ninguno el buzón real de ingesta. El
aprobador dio acceso real al buzón (`mppp@55xljh.onmicrosoft.com`) hoy; se verificó que el acceso
YA propagó (`GET users/mppp@55xljh.onmicrosoft.com/mailFolders` responde HTTP 200 y expone la
carpeta `Procesados`, no un 403) antes de escribir este caso — si esa verificación hubiera dado 403,
este caso seguía en `N-A` con la nota de que la Application Access Policy todavía no se propagó
(puede tardar hasta 30 minutos).

---

## C-06 · El Excel queda guardado con su nombre real (el `.eml`, con nombre FIJO)

**Qué prueba.** RF-10 y el insumo del plugin liviano: sin el `.eml` no hay clasificación; y que
`sanic_exceloriginal` se guarda con el nombre ORIGINAL del adjunto tal como lo mandó quien escribió
el correo — no un nombre inventado por el flujo.

**Necesita.** La misma Solicitud real que arma C-05 (comparten el único correo real que este set
manda: `ctx.extra["c05c06"]`, en memoria dentro de la misma corrida de `correr.py C`).

**Cómo.** Con esa Solicitud, leer `sanic_correocrudo_name` y `sanic_exceloriginal_name`.

**Qué tiene que pasar.**

| Campo | Valor esperado |
|---|---|
| `sanic_correocrudo_name` | **`correo.eml`** — nombre FIJO, ver "Corrección al MD" abajo |
| `sanic_exceloriginal_name` | el nombre REAL que tenía el adjunto `.xlsx` del correo real (el que mandó `c05()`, a propósito distinto del nombre del archivo fuente en `pruebas/adjuntos/`) |

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py C` (caso `C-06`); a mano,
`python3 herramientas/pruebas/ver_solicitud.py <MPPP-########>` (línea `archivos`), o un GET
directo: `sanic_mppp_tbl_solicituds(<id>)?$select=sanic_correocrudo_name,sanic_exceloriginal_name`.

**Cómo se sabe que falló.** Si `sanic_exceloriginal_name` viene vacío, o distinto del nombre real
del adjunto que mandó `c05()`, `MPPP-ING` no está leyendo `@first(variables('excels'))?['name']`
del correo real (o el `Foreach`/filtro de adjuntos está descartando el que no es inline mal). Si
`sanic_correocrudo_name` viniera vacío, `ExportEmail_V2`/`UpdateEntityFileImageFieldContent`
fallaron sin que el flujo lo reportara. Si `sanic_correocrudo_name` viniera con un nombre DISTINTO
de `correo.eml`, alguien cambió `x-ms-file-name` en `Guardar_el_correo_crudo` — actualizar esta
fila del MD junto con el cambio, no antes.

**Automatizable.** `API` (`correr.py C`, caso `C-06`, sobre la Solicitud real que deja `c05()` en
la misma corrida — Web API, sin navegador). **Corrección 2026-09-28, DOBLE.**

1. **Se destrabó** por lo mismo que C-05: acceso real al buzón de ingesta, ya operativo.
2. **Se corrigió la premisa del `.eml`.** La versión anterior de este caso esperaba que
   `sanic_correocrudo_name` trajera "el nombre real que Outlook le dio al mensaje". **Es falso, y
   no es un defecto**: se verificó hoy, contra el `clientdata` **DESPLEGADO** de `MPPP-ING`
   (`workflows`, no el repo — mismo criterio que ya usó D-62), que `Guardar_el_correo_crudo` fija
   `"x-ms-file-name": "correo.eml"` a mano, **igual con correo real que con el simulado** — no hay
   ningún paso del flujo que calcule ese nombre a partir del mensaje. `simulador.py` ya subía el
   mismo nombre fijo por la misma razón (imita al flujo, no lo simplifica). La parte "nombre real"
   de este caso SIEMPRE fue, y solo puede ser, sobre `sanic_exceloriginal_name` (el Excel, que sí
   usa `@first(variables('excels'))?['name']`, tomado de los adjuntos reales del correo) — eso es
   lo que el correo real prueba de más que el simulador: que `MPPP-ING` lee el nombre del adjunto
   de un mensaje GENUINO de Outlook/Graph, no que arme un nombre para el `.eml`.

> **Corrección al MD (2026-09-27, sigue vigente).** La versión anterior de este caso pedía "sesión
> con acceso de lectura a Solicitud (Ejecutivo o Supervisor)" para abrir la app y descargar el
> archivo a mano. Los dos campos `_name` se leen por Web API sin ninguna sesión de aplicación — eso
> ya estaba bien corregido; lo que faltaba corregir (2026-09-28) era la expectativa sobre el `.eml`.

---

## C-07 · El mismo correo dos veces no duplica

**Qué prueba.** La clave alternativa sobre `sanic_messageid` (DD-03) y el paso 4b de `MPPP-ING`.

**Necesita.** El `REMITENTE` de este set; el adjunto `E01-feliz-formato11.xlsx`.

**Cómo.** Se simula la reingesta: un segundo `Create` con el **mismo** `sanic_messageid` que la
Solicitud original — exactamente lo que hace `Crear_la_solicitud` de `mppp-ing.json` cuando
`MPPP-VIG` reingiere el mismo correo.

**Qué tiene que pasar.**

- El segundo `Create` es **rechazado** por la clave alternativa (DD-03).
- Solo existe **UNA** Solicitud con ese `sanic_messageid`.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py C` (caso `C-07`). Con correo real:
pedirle a quien tenga acceso al buzón que mueva el correo de `Procesados` de vuelta a la bandeja;
`MPPP-VIG` lo reingiere en los próximos 15 minutos.

**Cómo se sabe que falló.** Si el segundo `Create` se acepta, la clave alternativa no está
realmente aplicada en el entorno — el peor escenario del sistema: procesar la misma plantilla dos
veces y mandarle al cliente dos acuses distintos.

**Automatizable.** API (`correr.py C`, caso `C-07`, simulado sin mover correo real). Con correo
real es manual: requiere acceso al buzón compartido y esperar a `MPPP-VIG`.

---

## C-08 · Sin adjunto

**Qué prueba.** `TRAE_ADJUNTO` y, sobre todo, que las reglas que dependen de ella (todas las de
nivel Solicitud) queden **Omitidas** y no "No cumplida" (DD-13).

**Necesita.** El `REMITENTE` de este set; un correo sin ningún archivo adjunto.

**Cómo.** Correo nuevo, asunto `Prueba C-08 sin adjunto`, **sin ningún archivo adjunto**.

**Qué tiene que pasar.**

| Qué | Valor |
|---|---|
| clasificación / procesar | `nuevo` / `true` (el remitente SÍ está reconocido: esto es nivel Solicitud, no Correo) |
| Estado | **Rechazada** |
| filas | 0 |
| `TRAE_ADJUNTO` | **NO CUMPLIDA** |
| `ADJUNTO_ES_EXCEL`, `UN_SOLO_EXCEL`, `ESTRUCTURA_PLANTILLA`, `TIENE_FILAS` | **Omitida** |
| Acuse | **sí**, y no nombra la regla técnica `TRAE_ADJUNTO` |

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py C` (caso `C-08`).

**Cómo se sabe que falló.** Ojo con la firma con logo: una imagen incrustada llega como adjunto
`isInline = true` y **no cuenta** (DF-07) — si `cantidadadjuntos` viniera contando esa imagen, la
regla no estaría filtrando los inline; acá se simula directo con `cantidad_adjuntos=0`, así que
esa causa puntual no aplica a este caso simulado (si el correo real la mostrara, sería un defecto
de `MPPP-ING`, fuera de lo que `TRAE_ADJUNTO` evalúa).

**Automatizable.** API (`correr.py C`, caso `C-08`).

---

## C-09 · DN-01, modo `cliente`: autorizado SOLO por `AutorizacionCliente` SÍ entra

**Qué prueba.** Que `REMITENTE_RECONOCIDO` (nivel Correo) resuelve `autorizacion.modo` — el bug
real que corrigió el aprobador el 2026-09-26 ("el portero también tiene que preguntar lo mismo"):
antes de esa corrección, un correo autorizado solo por cliente pasaba `AUTORIZACION_CORREO_PLAN`
(nivel Registro) pero quedaba igual **No reconocida** en nivel Correo, y una No reconocida no
recibe respuesta — el cliente mandaba su solicitud y no se enteraba de nada.

**Necesita.** `autorizacion.modo = cliente` (lo fija el propio caso); un Autorizado
(`REMITENTE_DN01`, privado de este set) con **una única vía**: `AutorizacionCliente` hacia el
Cliente `PR - Pruebas MPPP SA` (dueño de `PR11`) y **cero** `AutorizacionPlan` (`preparar()`); el
adjunto `E01-feliz-formato11.xlsx`.

**Cómo.** Con `autorizacion.modo` en `cliente`, correo desde `REMITENTE_DN01` con
`E01-feliz-formato11.xlsx` adjunto.

**Qué tiene que pasar.**

| Qué | Valor |
|---|---|
| clasificación / procesar | `nuevo` / `true` |
| `REMITENTE_RECONOCIDO` | **Cumplida** |
| Estado | **En proceso** (la cadena sigue funcionando después del portero) |

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py C` (caso `C-09`).

**Cómo se sabe que falló.** Si la Solicitud queda **No reconocida**, el nivel Correo no está
resolviendo el modo `cliente` — es exactamente el bug que este caso fija. Si en cambio queda
reconocida pero el resto de la cadena se rompe (no llega a **En proceso** con las reglas de
Solicitud en verde), el defecto ya no es de `REMITENTE_RECONOCIDO`: es de otro nivel, fuera del
alcance de este archivo (ver `03-reglas-de-registro.md`, letra R).

**Automatizable.** API (`correr.py C`, caso `C-09`).

---

## C-10 · DN-01, modo `plan`: el MISMO correo (autorizado solo por cliente) YA NO entra

**Qué prueba.** El complemento de C-09, con el MISMO remitente: prueba que es el **modo** el que
cambia el resultado, no una diferencia entre remitentes. En modo `plan`, `AutorizacionPlan` es la
única vía que se mira — `AutorizacionCliente` "se conserva y no se mira" (DN-01, punto 4).

**Necesita.** `autorizacion.modo = plan` (lo fija el propio caso); el mismo `REMITENTE_DN01` de
C-09 (sigue teniendo su `AutorizacionCliente`, y sigue sin ninguna `AutorizacionPlan`); el adjunto
`E01-feliz-formato11.xlsx`.

**Cómo.** Con `autorizacion.modo` en `plan`, correo desde `REMITENTE_DN01` con
`E01-feliz-formato11.xlsx` adjunto.

**Qué tiene que pasar.**

| Qué | Valor |
|---|---|
| clasificación / procesar | `remitente_no_reconocido` / `false` |
| `REMITENTE_RECONOCIDO` | **NO CUMPLIDA** |
| Estado | **No reconocida** |
| Comunicación | **NINGUNA** |

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py C` (caso `C-10`).

**Cómo se sabe que falló.** Si este correo SÍ entra en modo `plan`, `AutorizacionCliente` está
funcionando como respaldo silencioso en modo `plan` — en contra del diseño explícito ("se
conservan y no se miran"). Comparado con C-09 (mismo correo, resultado opuesto), esto aísla que la
causa es el parámetro, no el dato.

**Automatizable.** API (`correr.py C`, caso `C-10`).

---

## C-11 · DN-01, valor basura en `autorizacion.modo`: degrada a `plan`, sin tumbar nada

**Qué prueba.** `ModoDeAutorizacionParametro.Resolver`: cualquier valor que no sea exactamente
`cliente` (sin distinguir mayúsculas) degrada al fallback quemado `plan`, **nunca lanza ni tumba la
validación** — mismo criterio que `servicio.cuenta` y `rpa.horas.tolerancia`.

**Necesita.** `autorizacion.modo = "clienteX"` (basura, lo fija el propio caso); `REMITENTE_DN01`
(autorizado solo por cliente, de C-09/C-10) y el `REMITENTE` de siempre (autorizado por plan); el
adjunto `E01-feliz-formato11.xlsx`.

**Cómo.**
1. Con `autorizacion.modo = "clienteX"`, correo desde `REMITENTE_DN01` (autorizado solo por
   cliente).
2. Con el mismo valor basura, correo desde `REMITENTE` (autorizado por plan, el camino de
   siempre).

**Qué tiene que pasar.**

| Correo | clasificación | Por qué |
|---|---|---|
| (a) `REMITENTE_DN01` (solo cliente) | `remitente_no_reconocido` | la basura NO se interpreta como `cliente`: se comporta como `plan`, y por `plan` este correo no tiene autorización |
| (b) `REMITENTE` (por plan) | `nuevo`, llega a **En proceso** | la operación **no se tumba**: ni excepción, ni HTTP roto, ni un falso "no reconocido" para el camino de siempre |

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py C` (caso `C-11`).

**Cómo se sabe que falló.** Dos causas con el mismo síntoma superficial ("algo salió mal con el
parámetro basura"): (a) si el correo (a) queda reconocido, el valor basura se está interpretando
como `cliente` — el fallback no es realmente quemado en `plan`; (b) si el correo (b) revienta con
una excepción, un HTTP distinto de 200/204, o queda "no reconocido" siendo el camino de siempre, el
valor basura SÍ está tumbando la operación normal — exactamente lo que el diseño prohíbe.

**Automatizable.** API (`correr.py C`, caso `C-11`).

---

## Fase 2 — investigación y conclusión

Se investigó qué de `diseno/DIRECTRICES-NUEVAS.md` (DN-01, DN-02, DN-03) agrega comportamiento
nuevo que se resuelva **a nivel Correo** (el nivel que cubre este set) y no a nivel Solicitud,
Registro o Fila.

### DN-01 SÍ aplica a este nivel — y ya está cubierto (C-09, C-10, C-11)

`Datos/Catalogos.cs` traza, con archivo y línea, que `PlanesAutorizadosDe` (el ÚNICO punto donde se
resuelve `autorizacion.modo`) tiene EXACTAMENTE dos llamadores:

```
ClasificarCorreo.cs:242   REMITENTE_RECONOCIDO   (nivel Correo — ESTE archivo)
ValidarSolicitud.cs:246   AUTORIZACION_CORREO_PLAN (nivel Registro — 03-reglas-de-registro.md)
```

El propio diseño dice *"una función, dos llamadores, ningún tercero"*, y remarca que **el modo
cambia TODO, no una parte**: el bug real que motivó la corrección del aprobador el 2026-09-26 fue
justamente que el nivel Correo se había quedado afuera del cambio. C-09/C-10/C-11 fijan exactamente
eso — con el mismo correo, cambiando solo el parámetro.

**Por qué no se agregó el resto de `pruebas/10-autorizacion-cliente.md` (letra `U`) acá.** Ese
archivo (todavía no migrado a `sets/`) ya tiene una cobertura mucho más amplia de DN-01: el nombre
calculado de `AutorizacionCliente` (U-07), la evidencia firmada opcional (U-10), las subgrillas de
la app (U-08/U-09), la clave alternativa (U-11), que el mensaje al cliente es idéntico en los dos
modos (U-06), y que cambiar de modo no borra nada (U-05) — todo eso es nivel Registro, tabla o app,
no nivel Correo. `CONVENCIONES.md` §4 fija un ID por archivo, sin colisiones: agregar esos casos acá
con prefijo `C` los duplicaría bajo dos letras distintas. Este archivo agrega SOLO lo que le
corresponde por nivel: que el "portero" también mire el modo. `U-02` (el caso que ya describe "los
DOS niveles, en la misma prueba") queda con la mitad de nivel Correo ahora reforzada acá, y la mitad
de nivel Registro sigue siendo trabajo de la letra `U` cuando se migre.

### DN-02 (exportar a Excel) — no aplica

Su Custom API (`sanic_mppp_capi_exportarparadigitar`) recibe `filaid`, no `solicitudid` ni nada de
nivel Correo; el parámetro nuevo (`exportacion.columnas`) describe columnas de **Fila**. No hay
ninguna superficie de este candidato en `ClasificarCorreo`.

### DN-03 (obligatoriedad por gestión) — no aplica

`plantilla.obligatoriedad` lo consulta la regla `OBLIGATORIEDAD`, de nivel **Registro** (evalúa
campo por campo de una Fila ya creada). Nivel Correo nunca llega a mirar el contenido del Excel —
eso es, por definición, lo que separa "Correo" de "Solicitud"/"Registro" en este diseño.

**Conclusión**: el set C queda con 8 casos de fase 1 (C-01..C-08, migrados sin reescribir la lógica
que ya estaba probada) más 3 casos nuevos de fase 2 (C-09..C-11, DN-01). DN-02 y DN-03 no tienen
lugar en este nivel.

---

## Funciones para consolidar

`sets/set_c.py::_preparar_autorizacion_por_cliente()` es local a este set (CONVENCIONES §8 permite
escribir local lo que `preparacion_comun.py` todavía no tiene, y reportarlo para que el coordinador
lo consolide). Candidata a subir a `preparacion_comun.py` si otro set necesita lo mismo: crea (o
reutiliza) un Autorizado con una `AutorizacionCliente` hacia un Cliente dado, sin ninguna
`AutorizacionPlan`. El caso más probable que la necesitaría es la migración futura de la letra `U`
(`10-autorizacion-cliente.md`), que hoy arma esto mismo a mano con `dataverse_api.py POST` según su
propio README.

**Nuevo 2026-09-28, de C-05/C-06.** `sets/set_c.py::_q()` url-encodea la `ruta` antes de pasarla a
`graph_api.Graph.call()` — `graph_api.py` NO lo hace (a diferencia de `dataverse_api.Dataverse.call()`,
que sí tiene un `safe=` explícito), así que cualquier `$filter` de Graph con espacios revienta con
`InvalidURL`. Se resolvió local (este set no es dueño de `graph_api.py`) pero es un defecto real del
propio `graph_api.py`, no algo para repetir en cada set que lo use — candidato a corregir en
`graph_api.py::Graph.call()` directamente (el mismo `safe=` que ya usa `dataverse_api.py`) la próxima
vez que alguien lo toque. `_folders_buzon_ingesta()`, `_mensaje_por_asunto_en_carpeta()`,
`_esperar_mensaje_en_carpeta()` y `_limpiar_correo_real()` son candidatas a subir a un módulo común si
otro set real necesita correo real contra un buzón de Graph con carpetas (hoy `set_v.py::v02()` y
`set_m.py` ya tienen su propia versión de "mandar y esperar", con otro criterio de correlación —
asunto contra Dataverse, no carpeta de Graph — así que no son el mismo helper todavía).

---

## Resultados

Última corrida: **2026-09-28**, `python3 herramientas/pruebas/correr.py C` contra Dev. **11 OK · 0
FALLA · 0 N-A** — los 11 casos, incluidos C-05/C-06 (correo real contra
`mppp@55xljh.onmicrosoft.com`), en verde. Verificado aparte, después de cerrar el set: cero correos
`ZZP-C05C06-*` residuales en el buzón real (`$filter=startswith(subject,'ZZP-C05C06')` → 0
resultados) y cero Solicitudes `ZZP-C05C06-*` residuales en Dataverse — la limpieza de
`cerrar()` funcionó de punta a punta.

| Caso | Solicitud | Resultado | Notas |
|---|---|---|---|
| C-01 camino feliz | (borrada al cerrar) | **OK** | |
| C-02 respuesta no se procesa | (borrada al cerrar) | **OK** | ver "Defecto ya resuelto" abajo |
| C-03 reenvío sí se procesa | (borrada al cerrar) | **OK** | |
| C-04 remitente no reconocido | (borrada al cerrar) | **OK** | |
| C-05 el correo se mueve y cambia de identificador | (borrada al cerrar; correo real borrado del buzón) | **OK** | primera corrida con acceso real al buzón de ingesta |
| C-06 el Excel con nombre real (el `.eml`, fijo) | (misma Solicitud que C-05) | **OK** | |
| C-07 no duplica | (borrada al cerrar) | **OK** | |
| C-08 sin adjunto | (borrada al cerrar) | **OK** | |
| C-09 DN-01 modo cliente entra | (borrada al cerrar) | **OK** | |
| C-10 DN-01 modo plan no entra (mismo correo) | (borrada al cerrar) | **OK** | |
| C-11 DN-01 valor basura degrada | (borrada al cerrar) | **OK** | |

### Defecto ya resuelto (no era de este set, era de `ClasificarCorreo`)

La corrida del 2026-09-27 (5 OK · 4 FALLA · 2 N-A) había encontrado que C-02/C-04/C-10/C-11 (todo
caso por el camino `!procesar`) fallaban con HTTP 400 genérico
(`"No se pudo clasificar el correo. El equipo tecnico ya tiene el detalle."`) por un
`InvalidCastException` en `src/Sanic.Mppp.Plugins/Datos/Avisos.cs:109`
(`GetAttributeValue<EntityReference>` sobre una columna de `systemuserroles` que Dataverse devuelve
como `Guid` plano). **Confirmado RESUELTO**: el commit `999f9fb` ("fix(plugins): D-59
systemuserroles.systemuserid llega como Guid, no EntityReference") corrigió exactamente esa línea,
y la corrida de hoy (2026-09-28) confirma las cuatro en **OK**, sin el HTTP 400. No se tocó `src/`
desde este set (este agente es dueño de exactamente dos archivos) — el resuelto se confirma por la
corrida en verde y el commit, no por leer el código de nuevo.
