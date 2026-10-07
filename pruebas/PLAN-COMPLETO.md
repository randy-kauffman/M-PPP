# Plan de pruebas funcionales — MPPP
Proyecto `2026-001-referencias-planes-pago` · Entorno **Dev** · generado el 2026-10-06 por `herramientas/pruebas/armar_plan_completo.py`

Este documento junta `README.md` y los 13 archivos de `pruebas/` en uno solo, para
leerlo de corrido. Los archivos separados (y los Excel) están en el repositorio, en
`pruebas/`. **No lo edites a mano**: si editás algo, editalo en el archivo separado y
volvé a correr `python3 herramientas/pruebas/armar_plan_completo.py`.

---
## Índice y cómo usarlo

Proyecto: `2026-001-referencias-planes-pago` · Entorno: **Dev**

Los componentes de la fase 1 y de la fase 2 (RPA, DN-01/02/03) están construidos y verificados
**estructuralmente**: existen, están bien formados y están en la solución. Lo que estas pruebas
comprueban es otra cosa, la que importa: **que hagan lo que el diseño dice que hacen**.

---

## Cómo está organizado

Cada archivo de esta carpeta es un **set autocontenido** (`CONVENCIONES.md` §8: no depende de otro
archivo, prepara por su cuenta todo lo que necesita) con su letra, corrido por
`herramientas/pruebas/correr.py`:

| Letra | Archivo | Qué cubre | Casos |
|---|---|---|---|
| C | [`01-clasificacion-del-correo.md`](01-clasificacion-del-correo.md) | `MPPP-REC`/`MPPP-ING`, el plugin liviano, reglas de nivel **Correo**, y DN-01 (autorización por cliente) en la clasificación | 11 |
| S | [`02-reglas-de-solicitud.md`](02-reglas-de-solicitud.md) | Reglas de nivel **Solicitud**: adjunto, Excel, estructura, filas | 7 |
| R | [`03-reglas-de-registro.md`](03-reglas-de-registro.md) | Reglas de nivel **Registro**: las que deciden fila por fila, más DN-01, y DN-03 completo (absorbió el set O) | 28 |
| M | [`04-comunicaciones.md`](04-comunicaciones.md) | `MPPP-COM`/`MPPP-ENV`: acuse y respuesta final, correo **real** de punta a punta | 7 |
| A | [`05-app-y-comandos.md`](05-app-y-comandos.md) | Los comandos de la barra y el diálogo de motivo, por API con `MSCRMCallerID`, y la lección del botón fantasma (ex H-06) | 15 |
| G | [`06-seguridad-vistas-formularios.md`](06-seguridad-vistas-formularios.md) | Roles, privilegios efectivos, sitemap, vistas, formularios (absorbió el resto del set H) | 20 |
| V | [`07-vigilancia.md`](07-vigilancia.md) | `MPPP-VIG`: los 5 bloques de recuperación | 7 |
| I | [`08-integridad-y-plugins.md`](08-integridad-y-plugins.md) | Steps de plugin, catálogo de reglas, claves únicas, la puerta del RPA | 13 |
| B | [`09-rpa-fase2.md`](09-rpa-fase2.md) | El bot RPA: Custom API, claim, orden FIFO, catálogo de errores | 20 |
| U | [`10-autorizacion-cliente.md`](10-autorizacion-cliente.md) | DN-01: autorización por cliente, con interruptor a plan | 11 |
| E | [`11-exportacion-excel.md`](11-exportacion-excel.md) | DN-02: descargar a Excel lo que hay que digitar | 10 |
| L | [`12-catalogos-masivos.md`](12-catalogos-masivos.md) | DN-04: carga masiva y activación masiva de catálogos por Excel (menús, plantillas, `MPPP-CAR-001` a `011`, reglas por fila, todo o nada, dueño del cliente nuevo) y Comentarios en la autorización por cliente | 57 |

**206 casos** (contados de los encabezados de caso de cada archivo el 2026-10-01: los 149 de los
sets C a E, contados el 2026-09-28 después del reagrupamiento de sets, más los 57 del set L). Cada uno
trae: qué prueba, cómo hacerlo, **qué tiene que pasar** y cómo verificarlo.

> **Set L, nota al 2026-10-02.** El set L corre con `correr.py L` (`herramientas/pruebas/sets/set_l.py`; primera
> corrida: 57 OK · 0 FALLA · 0 N-A). Su archivo trae además el estado de cada caso en su
> tabla `## Resultados`, con tres estados que **no se mezclan**: *EJECUTADO — OK* (con evidencia
> citada: el arnés de Playwright `pruebas/gui/casos/dn-04-catalogos-masivos.spec.mjs` y las
> mediciones de Dev de D-73/D-75), *CUBIERTO POR PRUEBAS AUTOMÁTICAS* (solo la suite de C#, sin
> correr en Dev: no es un OK) y *PENDIENTE*. Al 2026-10-02: 24 ejecutados, 33 cubiertos por pruebas
> automáticas y 0 pendientes (los 10 que faltaban se corrieron ese día, con las sesiones reales de los
> cuatro roles y el spec de Playwright extendido).

> **Set G, nota al 2026-09-28.** El archivo documenta 20 casos (`G-06` quedó fusionada en `G-00`/
> `G-00b`, así que no cuenta como caso aparte). De esos 20, tres siguen siendo GUI sin arnés todavía
> (`G-02` íconos, `G-07` recorte del sitemap por rol, `G-11` multiidioma — documentados con el
> comando exacto, sin función en `CASOS`): no es una migración a medias, es un hueco de cobertura
> conocido. `correr.py --listar` siempre dice la verdad del momento, más que este número.

### Tabla de equivalencias — el reagrupamiento de sets del 2026-09-28

Trece sets se reagruparon en once (`CONVENCIONES.md` §4, "La regla del nombre": el nombre de un set
dice QUÉ prueba, nunca cuándo se escribió ni qué pedido lo originó). `12-obligatoriedad-gestion.md`
y `13-construido-hoy.md` desaparecieron. Un reporte o una nota vieja que cite un ID de antes del
2026-09-28 se sigue leyendo con esta tabla:

| ID viejo | Archivo viejo | → | ID nuevo | Archivo nuevo |
|---|---|---|---|---|
| `O-01` | `12-obligatoriedad-gestion.md` | → | `R-19` | `03-reglas-de-registro.md` |
| `O-02` | `12-obligatoriedad-gestion.md` | → | `R-20` | `03-reglas-de-registro.md` |
| `O-03` | `12-obligatoriedad-gestion.md` | → | `R-21` | `03-reglas-de-registro.md` |
| `O-04` | `12-obligatoriedad-gestion.md` | → | `R-22` | `03-reglas-de-registro.md` |
| `O-05` | `12-obligatoriedad-gestion.md` | → | `R-23` | `03-reglas-de-registro.md` |
| `O-06` | `12-obligatoriedad-gestion.md` | → | `R-24` | `03-reglas-de-registro.md` |
| `O-07` | `12-obligatoriedad-gestion.md` | → | `R-25` | `03-reglas-de-registro.md` |
| `O-08` | `12-obligatoriedad-gestion.md` | → | `R-26` | `03-reglas-de-registro.md` |
| `O-09` | `12-obligatoriedad-gestion.md` | → | `R-27` | `03-reglas-de-registro.md` |
| `O-10` | `12-obligatoriedad-gestion.md` | → | `R-28` | `03-reglas-de-registro.md` |
| `H-01` | `13-construido-hoy.md` | → | `G-14` | `06-seguridad-vistas-formularios.md` |
| `H-02` | `13-construido-hoy.md` | → | `G-15` | `06-seguridad-vistas-formularios.md` |
| `H-03` | `13-construido-hoy.md` | → | `G-16` | `06-seguridad-vistas-formularios.md` |
| `H-04` | `13-construido-hoy.md` | → | `G-17` | `06-seguridad-vistas-formularios.md` |
| `H-05` | `13-construido-hoy.md` | → | `G-18` | `06-seguridad-vistas-formularios.md` |
| `H-06` | `13-construido-hoy.md` | → | `A-15` | `05-app-y-comandos.md` |
| `H-07` | `13-construido-hoy.md` | → | `G-19` | `06-seguridad-vistas-formularios.md` |

**Por qué `O` entra entero en `R` y `H` se reparte entre `A` y `G`, y por qué `U` no se tocó**: la
obligatoriedad por gestión (`O`) **ES** una de las ocho reglas de Registro — un tema, un archivo.
Los siete casos de `H` no compartían un solo tema entre sí: seis eran de sitemap/vistas/formulario
(el tema de `G`) y uno era de un comando de la barra (el tema de `A`), así que cada uno fue a donde
correspondía por TEMA, no los siete juntos a un archivo por fecha. `U` (autorización por cliente)
sigue aparte porque es transversal a dos niveles (Correo y Registro), no porque no se haya
revisado — ver `CONVENCIONES.md` §4 para el detalle completo de los tres casos.

### El set P desapareció

`00-preparacion.md` sigue existiendo, pero **ya no es un set** (no tiene letra, `correr.py` no lo
corre). Sus seis verificaciones se mudaron a `herramientas/pruebas/preparacion_comun.py`
(`verificar_datos_negocio`, `verificar_excels`, `verificar_flujos_activos`,
`verificar_estructura_plantilla`, `verificar_parametro_activo`, `verificar_roles`), y cada set migrado
llama las que necesita desde su propio `preparar()` — así, cada set queda autocontenido de verdad
(`CONVENCIONES.md` §8) en vez de depender de "correr `00` primero".

Lo que **sigue siendo puramente manual** — y por eso `00-preparacion.md` queda como referencia, no
como set — son las tres cosas que no son Dataverse: la carpeta `Procesados` del buzón compartido
(P-01), los permisos de ese buzón en el Exchange admin center (P-01b) y la elección de la casilla
real desde la que se manda correo de prueba (P-02).

---

## Cómo se corre

```bash
python3 herramientas/pruebas/correr.py S            # un set
python3 herramientas/pruebas/correr.py A,B,C        # varios, en ESE orden (no el canónico)
python3 herramientas/pruebas/correr.py --todos      # todos los sets descubiertos, en orden canónico
python3 herramientas/pruebas/correr.py --listar     # qué sets hay y cuántos casos, sin tocar Dev
python3 herramientas/pruebas/correr.py --todos --full   # TODO prendido: ver la sección de abajo
```

`correr.py` toma el semáforo (`semaforo.py`), aplica el canon de parámetros de `CONVENCIONES.md` §8.2
antes y después de cada set, corre `preparar → CASOS → cerrar` (`cerrar` siempre, aunque algo reviente
antes), e imprime en vivo caso por caso. Un set que no se pudo preparar no corre sus casos: cero casos
corridos es un resultado honesto, casos verdes sobre un entorno mal preparado es una mentira.

Tené a mano la herramienta de lectura, que evita andar clickeando para ver qué pasó:

```bash
python3 herramientas/pruebas/ver_solicitud.py MPPP-00001234
python3 herramientas/pruebas/ver_solicitud.py --ultima
```

**Este documento (`README.md`) dice CÓMO está organizado el plan** — qué archivo cubre qué, cuántos
casos tiene, cómo se corre. **`CONVENCIONES.md` dice CÓMO se escribe una prueba** — el formato de un
caso, el contrato de un set, el canon de configuración, el manejo de datos y sesiones. Si estás
escribiendo o migrando un caso, es `CONVENCIONES.md` lo que tenés que seguir; si estás buscando dónde
vive algo, es este archivo.

---

## Cómo anotar los resultados

Cada archivo termina con una tabla `## Resultados` para llenar (o ya llena, de la última corrida
real). Los tres estados posibles:

- **OK** — pasó exactamente lo esperado
- **FALLA** — pasó otra cosa. Anotá qué pasó, con el número de solicitud
- **N/A** — no se pudo correr, y por qué (`c.na(motivo)` en el código)

**Un caso que "casi" pasa es un FALLA.** Si el estado es el correcto pero el mensaje al cliente dice
otra cosa, es un FALLA: el mensaje lo lee el cliente del banco.

---

## Esto escribe en Dev

Los casos crean Solicitudes, Filas y Bitácora reales, y algunos (el set M, y los casos "lentos" del
set V) mandan correo real desde buzones de prueba dedicados. Todo con datos ficticios (prefijo `PR*`
para el fixture de negocio compartido, `ZZP-`/`zz-` para lo que crea cada corrida) y cada set borra
por GUID, en su propio `cerrar()`, lo que él mismo creó — nunca lo de otro set ni lo de otro agente
corriendo en paralelo (`CONVENCIONES.md` §5).

---

## Casos de alto riesgo, apagados por default — y el modo `--full`

Tres banderas de entorno prenden casos que por default quedan `N-A` porque su costo (tiempo real) o
su riesgo (degradar el sandbox compartido de Dev, tocar un flujo real) no se paga en cada corrida:

| Bandera | Qué prende | Por qué está apagada por default |
|---|---|---|
| `MPPP_E08_ALTO_RIESGO=si` | `E-08` (set E): ~300.000 filas hasta el límite real de memoria del sandbox | La llamada real tarda 150-170s y puede degradar el sandbox de Dataverse Dev para cualquier otro usuario mientras corre. Es un riesgo ya medido y aceptado, no un defecto — correrlo de nuevo no agrega cobertura, solo riesgo. |
| `MPPP_V02_ALTO_RIESGO=si` | `V-02` (set V): apaga `Cloud Flow - MPPP - REC` DE VERDAD, corre la prueba, lo vuelve a encender | Es la única excepción de todo el arnés a "nunca se toca el estado de un flujo" — autorizada puntualmente por el aprobador el 2026-09-28, con dos redes de seguridad (`CONVENCIONES.md`, "Excepción V-02"). |
| `MPPP_V_LENTOS=si` | `V-03` a `V-07` (set V): los bloques que dependen del schedule real de `MPPP-VIG` (recurrence de 10 minutos) | Con el margen que necesita este set (3 vueltas completas + colchón), los cinco casos juntos superan las dos horas y media. |

```bash
MPPP_E08_ALTO_RIESGO=si python3 herramientas/pruebas/correr.py E
MPPP_V02_ALTO_RIESGO=si python3 herramientas/pruebas/correr.py V
MPPP_V_LENTOS=si python3 herramientas/pruebas/correr.py V
```

`E-07` (corte por presupuesto de tiempo) queda siempre `N-A` en `correr.py`, por una razón distinta:
la medición real ya mostró que ni 250.000 posiciones de `filaids` se acercan al umbral, y forzar más
volumen por API es el mismo riesgo de `E-08` sin agregar cobertura — esa cobertura puntual la da la
prueba unitaria con reloj inyectado (`Aceptacion/ExportarParaDigitarApiAceptacion.cs`).

### `--full` — un solo interruptor para "corré todas, todas sin importar nada"

Decisión del aprobador (2026-09-28): en vez de tener que acordarse de las banderas sueltas de
arriba, un solo parámetro las prende todas de una:

```bash
python3 herramientas/pruebas/correr.py --todos --full   # todo, con TODO prendido
python3 herramientas/pruebas/correr.py V --full          # solo el set V, con lo suyo prendido
python3 herramientas/pruebas/correr.py E --full          # solo el set E, con lo suyo prendido
```

`--full` es la forma **recomendada** de prender `MPPP_E08_ALTO_RIESGO`, `MPPP_V02_ALTO_RIESGO` y
`MPPP_V_LENTOS` a la vez — pero no reemplaza a las banderas sueltas, las incluye: quien ya las usa
por separado sigue funcionando exactamente igual.

**Antes de arrancar, `--full` grita un banner** con qué casos extra va a correr, cuánto tarda de
más y qué riesgos asume — nadie se tiene que enterar cuarenta minutos después de que el arnés está
haciendo algo caro o riesgoso. `python3 herramientas/pruebas/correr.py --listar` también distingue,
set por set, qué corre siempre y qué solo corre con `--full`.

**Qué cuesta un `--full` completo (`--todos --full`):** además de los ~150-170s de `E-08`, el set V
con TODO prendido (`V-02` + `V-03`..`V-07`) supera las **3 horas** — el schedule real de `MPPP-VIG`
(10 minutos) no se puede acelerar desde afuera. Correr `V --full` o `E --full` por separado, para
verificar un cambio puntual, es mucho más rápido que un `--todos --full` completo.

**`B-01` (set B) queda afuera de `--full`, A PROPÓSITO.** No es un olvido: `B-01` renombra el rol
`SR - MPPP - RPA` y rompe la puerta del bot para TODO el entorno mientras dura el renombre. El
aprobador confirmó explícitamente que sigue `N-A` incluso con `--full` (`pruebas/09-rpa-fase2.md`);
ya está cubierto por 8 pruebas unitarias (7 de rechazo). `--full` no tiene ninguna bandera que lo
prenda — no existe.

---

## Rojo a propósito: los defectos abiertos que hacen fallar casos, y por qué eso está bien

Estos siete defectos de `diseno/PENDIENTES.md` están **ABIERTOS**, pospuestos por el aprobador. Los
casos que los cruzan dan **FALLA** a propósito, y así tiene que quedar hasta que alguien los cierre:
**ese rojo es un sensor que sigue funcionando, no un defecto de las pruebas.** Si alguna vez ves uno
de estos casos en verde sin que el defecto se haya cerrado en `PENDIENTES.md`, sospechá del cambio,
no le creas a la corrida.

| Defecto | Dónde pega | Casos que lo cruzan | Qué pasa |
|---|---|---|---|
| **D-59** | `Datos/Avisos.cs:109` | C-02, C-04, C-10, C-11 (set C); R-17 (set R); U-03, U-04, U-05 (set U) | Cast de `systemuserid` como `EntityReference` en vez de `Guid` sobre `systemuserroles`: el aviso a los ejecutivos falla siempre que un correo no se puede procesar. |
| **D-60** | `Api/RegistrarResultadoRpa.cs` → `AplicarRechazada` | B-10, B-17 (set B) | `resultado: "rechazada"` revienta con error genérico incluso con un solo ítem válido; además viola "nunca todo o nada" (un ítem bueno se pierde en el mismo lote). |
| **D-61** | `sanic_mppp_capi_obtenerfilaspendientes` | B-05, B-06 (set B) | `maximo` está declarado opcional pero omitirlo devuelve HTTP 400 en vez de aplicar el defecto documentado (100). |
| **D-62** | `recursos/flujos/mppp-vig.json` (arranque) | V-01 (set V, en rojo a propósito) | La etapa de arranque de los 5 bloques de `MPPP-VIG` solo encadena con `["Succeeded"]`: si un paso de arranque falla, los CINCO bloques de recuperación se saltean esa corrida. |
| **D-63** | `Steps/ReglasDeSteps.cs:26` (`PermitidasEnFila`) | A-14 (set A) | La lista blanca de escrituras sobre Fila no incluye `sanic_requiereatencionmanual`: el botón "Atendida" da HTTP 400 real, y la fila queda trabada (el bot no la puede tomar, la persona no la puede liberar). |
| **D-64** | DA-08 — avisos de la app (`Datos/Avisos.cs`) | G-10 (set G) | DA-08 define 5 avisos en la campana; el código solo implementa 1, y ese es justo el que revienta por D-59. Cuatro de los cinco avisos no existen. |
| **I-07** | `pruebas/08-integridad-y-plugins.md` | I-07 (set I) | Un `PATCH` directo a `sanic_referencia` por Web API se guarda y `ListaBlancaStep` no lo bloquea — la protección de solo lectura es del formulario, no del dato. |

---

## Qué NO cubren estas pruebas

- **Rendimiento real bajo volumen productivo.** Los casos de volumen (`E14`, `E-08`) miden el
  presupuesto de la plataforma, no una prueba de carga con el tráfico real de producción.
- **Concurrencia real de carga** (dos correos simultáneos, dos supervisores aprobando la misma fila
  al mismo instante). El mecanismo de claim con vencimiento (`sanic_claimhasta`) ya está implementado
  y probado (set B, set I), pero no bajo carga concurrente real.
- **Migración desde SharePoint** (RF-16): es de Producción, no de Dev.
- **Fase 3** (histórico): todavía no construida.
- **Lo que solo se ve en un navegador** (ribbon, íconos, multiidioma renderizado, recorte de sitemap
  por rol): eso lo cubre el arnés de Playwright en `pruebas/gui/` (ver su propio README), no este
  motor de casos por API.

---
## 00 — Preparación

Se hace **una sola vez**, antes de todos los demás archivos. Si algo de acá falla, no sigas: los
casos siguientes van a fallar por el andamio y no por el sistema, y eso no prueba nada.

---

### P-01 · La carpeta del buzón

**Qué prueba.** Que exista el destino al que `MPPP-ING` mueve el correo ya ingresado.
`MoveV2` la busca por **nombre exacto** (`folderPath`), no por identificador.

**Necesita.** Acceso al buzón compartido `mppp@55xljh.onmicrosoft.com` en Outlook (web o cliente).
No hace falta ningún rol de Dataverse: esto es administración de Exchange/Outlook.

**Cómo.** Entrá al buzón compartido `mppp@55xljh.onmicrosoft.com` y fijate si existe una carpeta
de primer nivel llamada exactamente **`Procesados`** — con P mayúscula, sin tilde, sin espacios
alrededor. Si no existe, creala.

**Esperado.** La carpeta existe y está vacía (o con lo que haya de antes).

> Si preferís otro nombre, cambialo en la variable de entorno
> `sanic_mppp_ev_carpetaprocesados` y en `playbooks/variable-entorno/sanic_mppp_ev_carpetaprocesados.md`.
> **No alcanza con crear la carpeta**: el valor de la variable tiene que coincidir.

**Automatizable.** Manual (Outlook/Exchange; no hay Web API de Dataverse para crear una carpeta de
correo).

---

### P-01b · Los permisos del buzón compartido

**Qué prueba.** Que la cuenta de la **conexión de Outlook** pueda leer, mover y responder en el
buzón compartido. **Sin esto no funciona NINGÚN flujo**: los cinco pasan por ahí.

**Necesita.** Acceso al **Exchange admin center** (rol de administración de Microsoft 365/Exchange,
no un rol de la app MPPP) para revisar delegación del buzón.

**Por qué está acá.** El 2026-09-23 los flujos fallaron con **HTTP 403** en `Leer_la_bandeja`, y
costó varias vueltas llegar: la cuenta veía el buzón en Outlook web, pero la conexión no podía
leerlo por API.

**Los siete puntos que tocan el buzón compartido**, todos con *Original Mailbox Address*:

| Flujo | Acción | Qué necesita |
|---|---|---|
| REC | trigger `SharedMailboxOnNewEmailV2` | **Full Access** |
| ING | `GetEmailV2`, `ExportEmail_V2`, `MoveV2` | **Full Access** |
| VIG | `GetEmailsV3` | **Full Access** |
| ENV | `ReplyToV3` ×2 | **Full Access + Send As** |

**Cómo.** En el Exchange admin center → **Recipients → Mailboxes** → el buzón de ingesta:

1. **Tipo de buzón**: tiene que decir **Shared**, no **User**. Las acciones con *Original Mailbox
   Address* están pensadas para buzones compartidos.
2. **Manage mailbox delegation** → la cuenta de la conexión de Outlook tiene que estar en:
   - **Full Access** — para leer, exportar y mover
   - **Send As** (o Send on Behalf) — **para responder**. Learn es explícito: *"a user with Full
     Access permission can't send email from the shared mailbox unless they also have Send As or
     Send on Behalf permission"*. Con solo Full Access, ENV falla y el cliente no recibe el acuse.
3. **Qué cuenta es**: Power Automate → Conexiones → la de Office 365 Outlook. Ojo: **la cuenta con
   la que vos abrís el buzón no es necesariamente la de la conexión.**

**Esperado.** Buzón de tipo Shared, y la cuenta de la conexión con **Full Access y Send As**.

**Automatizable.** Manual (Exchange admin center; no es Dataverse Web API).

> **Los permisos tardan.** Learn: de **30 a 60 minutos**, y a veces hasta 24 horas. Si acabás de
> darlos, esperá antes de dar nada por roto. El *remove and re-add* de la delegación fuerza el
> refresco del token.

> **Si el 403 persiste con los permisos puestos**, la prueba que discrimina: un flujo nuevo con una
> sola acción *Get emails (V3)*, probándolo contra **otro buzón compartido del tenant que ya
> funcione** y contra el de ingesta. Si el otro anda y el de ingesta no, el problema es del buzón,
> no del flujo ni de la cuenta.

---

### P-02 · El remitente de las pruebas

**Qué prueba.** Que tengas una casilla real desde la que mandar, y a la que lleguen las respuestas.

**Necesita.** Una casilla de correo real del tenant, distinta del buzón de ingesta. Ningún rol de
Dataverse todavía: esto se resuelve antes de tocar la app.

**Cómo.** Elegí la dirección desde la que vas a enviar todos los correos de prueba. Puede ser tu
cuenta personal del tenant. **No uses el propio buzón de ingesta**: un correo del buzón a sí mismo
se comporta distinto y ensucia los casos.

Anotala acá, porque la vas a citar en todos los casos:

```
REMITENTE = ______________________________
```

**Esperado.** Podés enviar desde esa dirección y leer lo que llega a ella.

**Automatizable.** Manual (es una decisión humana: elegir la casilla real; `corredor.py` usa su
propio remitente ficticio `zz-corredor-fase1@example.com` para todo lo que sí automatiza).

---

### P-03 · Datos de negocio de prueba

**Qué prueba.** Que existan los planes y autorizaciones que citan los Excel de prueba.

**Necesita.** Credenciales con permiso de escritura en Dataverse (las que ya usa
`herramientas/dataverse_api.py`); el `REMITENTE` de P-02.

**Cómo.**

```bash
python3 herramientas/pruebas/preparar_datos.py --remitente TU-CORREO@dominio.com
```

**Esperado.** Última línea con `"estado": "creado"` y las comprobaciones en verde. Crea:

| Qué | Detalle | Para qué |
|---|---|---|
| Cliente | `PR - Pruebas MPPP SA` | dueño de todos los planes |
| Plan `PR11` | formato **11**, COR, activo | camino feliz y reglas de formato 11 |
| Plan `PR06` | formato **06**, COR, activo | que la referencia **no** se derive (DD-15) |
| Plan `PR10` | formato **10**, **USD**, activo | `MONEDA_DEL_PLAN` |
| Plan `PRIN` | formato 06, COR, **INACTIVO** | `PLAN_EXISTE` con un plan que existe pero no vale |
| Plan `PRNA` | formato 06, COR, activo | `AUTORIZACION_CORREO_PLAN`: **sin** autorización del remitente |
| Autorizado | tu dirección | `REMITENTE_RECONOCIDO` |
| Autorizaciones | a PR11, PR06, PR10 y PRIN | **a PRNA no, a propósito** |

**Verificá** con `python3 herramientas/pruebas/preparar_datos.py --verificar --remitente TU-CORREO@dominio.com`.

> Al terminar TODAS las pruebas: `preparar_datos.py --limpiar --remitente TU-CORREO@dominio.com`.

**Automatizable.** API (`preparar_datos.py`; también automatizado por `corredor.py` con su propio
remitente ficticio, caso P-03 — usa `create-if-missing`, nunca `--limpiar`, porque el fixture
`PR*` es compartido con quien esté probando en `pruebas/gui/`).

---

### P-04 · Los Excel de los casos

**Qué prueba.** Que estén los 15 Excel y el adjunto que no es Excel.

**Necesita.** Nada de Dataverse: solo Python local y la plantilla real en `datos/plantilla/`.

**Cómo.**

```bash
python3 herramientas/pruebas/generar_excel.py
ls pruebas/adjuntos/
```

**Esperado.** 15 `.xlsx` y 1 `.txt` en `pruebas/adjuntos/`. Todos salen de la plantilla **real**
del cliente: misma hoja `Datos`, encabezados en la fila 12, datos desde la 13.

> Abrí `E01-feliz-formato11.xlsx` y mirá que las tres filas estén desde la fila 13, en las
> columnas B a K. Si eso está bien, los demás también.

**Automatizable.** API/local (`generar_excel.py`; corredor.py lo corre solo si faltan Excel,
como parte de su preparación, caso P-04).

---

### P-05 · Los cinco flujos activos

**Qué prueba.** Que la automatización esté encendida.

**Necesita.** Credenciales de lectura en Dataverse (o de servicio) para consultar `workflows`.
Ningún rol de la app.

**Cómo.**

```bash
for f in ing rec env com vig; do python3 herramientas/construir/flujo.py playbooks/flujo/mppp-$f.md --solo-verificar; done
```

**Esperado.** Los cinco con `"estado": "ya_existia"` y la palabra **`activado`** en el detalle.
Si alguno dice `difiere ... statecode 0`, está apagado: prendelo antes de seguir.

**Automatizable.** API (`flujo.py --solo-verificar`; corredor.py caso P-05).

---

### P-06 · Los parámetros cargados

**Qué prueba.** Que estén los umbrales y las listas que usa la validación.

**Necesita.** Credenciales de lectura en Dataverse (las que ya usa `ver_solicitud.py`). Ningún rol
de la app: es una consulta directa a la tabla Parametro.

**Cómo.**

```bash
python3 herramientas/pruebas/ver_solicitud.py --parametros
```

**Esperado.** **Al menos estos 13 parámetros de fase 1, nombrados uno por uno**, activos (no un
conteo total: total envejece con cada parámetro nuevo de fases posteriores):

`correo.prefijos.reenvio` · `clasificacion.dias.vencimiento` · `lectura.limites` ·
`plantilla.estructura` · `plantilla.listas` · `plantilla.obligatoriedad` ·
`vigilancia.minutos.sinvalidar` · `vigilancia.minutos.sinresponder` ·
`vigilancia.reintentos.maximo` · `retencion.dias.general` · `retencion.dias.noreconocidas` ·
`rpa.puedeaprobar` · `servicio.cuenta`

> **Por qué 13 y no "12" como decía antes esta línea.** Esta afirmación se verificó contra el
> commit que cerró la construcción de fase 1 (`53994c3`, 2026-09-24 — el mismo que escribió esta
> misma página): `playbooks/datos/parametros.md` en ese commit ya sembraba estos 13 nombres, no 12.
> El "12" fue un error de conteo del propio plan desde el día uno, no algo que envejeció. **Hoy
> Dev tiene 19 parámetros activos** (verificado con el comando de arriba; salen 20 filas porque
> `plantilla.obligatoriedad` tiene dos versiones activas a la vez): los 6 que se agregaron después
> y no son de fase 1 son `autorizacion.modo` (DN-01, 2026-09-26), `exportacion.columnas` (DN-02,
> 2026-09-26), `rpa.codigos`, `rpa.horas.tolerancia`, `rpa.lote.maximo` y `rpa.minutos.claimmaximo`
> (fase 2, 2026-09-25/26). El criterio correcto para este caso es "al menos estos 13 activos", no
> un total exacto. **Después se agregó `carga.filas.maximo`** (DN-04, 2026-10-01): la versión 2 vale
> **100** (medido en Dev, D-75) y es la que tienen que encontrar los casos del set L (`12-catalogos-masivos.md`).

Los que más van a importar:

| Parámetro | Valor esperado | Dónde pega |
|---|---|---|
| `vigilancia.minutos.sinvalidar` | `15` | caso 07 |
| `vigilancia.minutos.sinresponder` | `30` | caso 07 |
| `vigilancia.reintentos.maximo` | `3` | caso 07 |
| `clasificacion.dias.vencimiento` | `30` | caso 07 |
| `plantilla.estructura` | hoja `Datos`, encabezado 12, primera fila 13 | casos 02 y 03 |
| `plantilla.listas` | banco **BAC = código `007`** | caso 03 |
| `plantilla.obligatoriedad` | todo obligatorio salvo `referencia` | caso 03 |

> El código `007` de BAC importa: es el que se usa para construir la referencia de formato 11, y
> sale del Excel real del cliente. Si ahí dijera otra cosa, las referencias saldrían mal en AS400.

**Automatizable.** API (`ver_solicitud.py --parametros`; corredor.py caso P-06).

---

### Resultados

| Caso | Resultado | Notas |
|---|---|---|
| P-01 carpeta `Procesados` | | |
| **P-01b permisos del buzón** | | Full Access + Send As |
| P-02 remitente | | |
| P-03 datos de negocio | | |
| P-04 Excel generados | | |
| P-05 flujos activos | | |
| P-06 parámetros | | |

---
## 01 — Clasificación del correo

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

### C-01 · Camino feliz — correo nuevo, remitente autorizado, Excel válido

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

### C-02 · Una respuesta NO se procesa

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

### C-03 · Un reenvío SÍ se procesa

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

### C-04 · Remitente no reconocido

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

### C-05 · El correo se mueve y cambia de identificador

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

### C-06 · El Excel queda guardado con su nombre real (el `.eml`, con nombre FIJO)

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

### C-07 · El mismo correo dos veces no duplica

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

### C-08 · Sin adjunto

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

### C-09 · DN-01, modo `cliente`: autorizado SOLO por `AutorizacionCliente` SÍ entra

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

### C-10 · DN-01, modo `plan`: el MISMO correo (autorizado solo por cliente) YA NO entra

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

### C-11 · DN-01, valor basura en `autorizacion.modo`: degrada a `plan`, sin tumbar nada

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

### Fase 2 — investigación y conclusión

Se investigó qué de `diseno/DIRECTRICES-NUEVAS.md` (DN-01, DN-02, DN-03) agrega comportamiento
nuevo que se resuelva **a nivel Correo** (el nivel que cubre este set) y no a nivel Solicitud,
Registro o Fila.

#### DN-01 SÍ aplica a este nivel — y ya está cubierto (C-09, C-10, C-11)

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

#### DN-02 (exportar a Excel) — no aplica

Su Custom API (`sanic_mppp_capi_exportarparadigitar`) recibe `filaid`, no `solicitudid` ni nada de
nivel Correo; el parámetro nuevo (`exportacion.columnas`) describe columnas de **Fila**. No hay
ninguna superficie de este candidato en `ClasificarCorreo`.

#### DN-03 (obligatoriedad por gestión) — no aplica

`plantilla.obligatoriedad` lo consulta la regla `OBLIGATORIEDAD`, de nivel **Registro** (evalúa
campo por campo de una Fila ya creada). Nivel Correo nunca llega a mirar el contenido del Excel —
eso es, por definición, lo que separa "Correo" de "Solicitud"/"Registro" en este diseño.

**Conclusión**: el set C queda con 8 casos de fase 1 (C-01..C-08, migrados sin reescribir la lógica
que ya estaba probada) más 3 casos nuevos de fase 2 (C-09..C-11, DN-01). DN-02 y DN-03 no tienen
lugar en este nivel.

---

### Funciones para consolidar

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
vez que alguien lo toque.
`_folders_buzon_ingesta()`, `_mensaje_por_asunto_en_carpeta()`,
`_esperar_mensaje_en_carpeta()` y `_limpiar_correo_real()` son candidatas a subir a un módulo común si
otro set real necesita correo real contra un buzón de Graph con carpetas (hoy `set_v.py::v02()` y
`set_m.py` ya tienen su propia versión de "mandar y esperar", con otro criterio de correlación —
asunto contra Dataverse, no carpeta de Graph — así que no son el mismo helper todavía).

**Corrección 2026-10-06 (la nota de arriba ya es historia).** `graph_api.py::Graph.call()` pasó a url-encodear la ruta (commit `a7e493b`), así que el `_q()` local doble-codificaba el `$filter` (`subject%2520eq…`) y Graph contestaba 400 "character '%' is not valid"; como `_mensaje_por_asunto_en_carpeta()` devolvía `None` si el HTTP no era 200, el 400 se leía como "el correo no llegó". Se eliminó `_q()` (las rutas van crudas a `Graph.call()`) y esa función ahora lanza error con el código y el cuerpo ante cualquier HTTP distinto de 200; C-05 lo reporta como FALLA visible.

---

### Resultados

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

#### Defecto ya resuelto (no era de este set, era de `ClasificarCorreo`)

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

---
## 02 — Reglas de nivel Solicitud

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

### S-01 · El adjunto no es un Excel

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

### S-02 · Dos Excel en el mismo correo

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

### S-03 · Un Excel y un archivo que no lo es

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

### S-04 · La hoja de datos tiene otro nombre

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

### S-05 · Excel con la estructura correcta pero sin filas

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

### S-06 · Volumen: 100 filas

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

### S-07 · El acuse de una Rechazada dice lo que tiene que decir

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

### Fase 2 — investigación y conclusión

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

### Resultados

| Caso | Solicitud | Resultado | Notas |
|---|---|---|---|
| S-01 adjunto no Excel | | | |
| S-02 dos Excel | | | |
| S-03 Excel + otro archivo | | | |
| S-04 hoja renombrada | | | |
| S-05 sin filas | | | |
| S-06 volumen 100 | | | seg: ____ |
| S-07 texto del acuse | | | |

---
## 03 — Reglas de nivel Registro

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

### R-01 · Valores fuera de las listas

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

### R-02 · Largos y formato

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

### R-03 · Plan que no existe

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

### R-04 · Plan que existe pero está inactivo

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

### R-05 · Plan formato 11 con clasificación que no es ACH

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

### R-06 · Campos obligatorios vacíos, y el opcional

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

### R-07 · Moneda distinta de la del plan

**Qué prueba.** `MONEDA_DEL_PLAN` (RF-07).

**Necesita.** El REMITENTE; el adjunto `E07-moneda-distinta.xlsx`; el plan `PR10` (USD).

**Cómo.** Correo nuevo con **`E07-moneda-distinta.xlsx`** (plan `PR10` es **USD**, la fila trae **COR**).

**Esperado.** La fila **Rechazada en validación**, mensaje sobre la moneda del plan.

**Automatizable.** API (`corredor.py`, caso R-07).

---

### R-08 · Sin autorización sobre ese plan

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

### R-09 · Sin autorización GANA sobre otra regla que rechaza

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

### R-10 · La referencia de formato 11 se construye sola (en la Inclusión)

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

### R-11 · La referencia de formato 11 que NO se puede construir

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

### R-12 · Formato 06: la referencia NO se deriva

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

### R-13 · Mezcla de filas válidas y rechazadas

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

### R-14 · Duplicados dentro de la plantilla NO se validan

**Qué prueba.** DD-12: el control de duplicados está en AS400, no acá.

**Necesita.** El REMITENTE; un Excel armado con la fila 13 de `E01-feliz-formato11.xlsx` repetida
en las filas 14 y 15.

**Cómo.** Hacé un Excel con **la misma fila repetida tres veces** (copiá la fila 13 de
`E01-feliz-formato11.xlsx` a las filas 14 y 15). Mandalo.

**Esperado.** Las **3 filas Validadas**. `3 / 3 / 0`, Solicitud **En proceso**.

**FALLA si** alguna quedó rechazada por duplicada: sería una regla que el diseño no pidió.

**Automatizable.** API (`corredor.py`, caso R-14).

---

### R-15 · DN-01: `autorizacion.modo` en su canon (`plan`) antes de correr las reglas de Registro

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

### R-16 · DN-01: modo `cliente` — un correo autorizado SOLO por `AutorizacionCliente` valida sus filas

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

### R-17 · DN-01: valor basura en `autorizacion.modo` degrada a `plan`, sin tumbar la operación

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

### R-18 · DN-05: el criterio de `Exclusion` de la v3 (cuenta y referencia obligatorias, el resto opcional)

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

### Obligatoriedad por gestión, exhaustivo — casos R-19 a R-28 (fusionado del ex set O)

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

> **Nota del 2026-10-06 (DN-05).** Esta sección habla de la v2 como la versión vigente. Hoy la vigente es la **v3**
> (matriz del negocio), la v1 está **inactiva** desde el 2026-09-28 (decisión pendiente del aprobador, ver R-26) y
> la v2 sigue activa pero la v3 la tapa. Lo que cambió en los casos: R-20 y R-21 se reescribieron contra la v3 (más
> abajo); R-19, R-22 a R-25, R-27 y R-28 siguen como están. Lo del párrafo anterior sobre "Exclusion afloja tres
> campos" (R-18, R-20) es historia de la v2.

**Necesita, en general** (además de lo que ya deja listo `preparar()` de este set, arriba): `dotnet`
disponible en el PATH para R-22, R-24 y R-25 (`dotnet test tests/Sanic.Mppp.Plugins.Tests --filter
<FullyQualifiedName>`).

---

### R-19 · Compatibilidad: un parámetro SIN `porGestion` se comporta igual que antes de DN-03

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

### R-20 · El contenido sembrado de `plantilla.obligatoriedad` v3 es exactamente el de la matriz del negocio

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

### R-21 · DN-05: `Inclusion` tiene sus `reglas` por formato y `Modificacion` su entrada en `porGestion`

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

### R-22 · Precedencia `obligatorios` sobre `opcionales` (cobertura unitaria, no sembrar en Dev)

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

### R-23 · Gestión inválida en la FILA: `OBLIGATORIEDAD` ni siquiera se evalúa

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

### R-24 · Las CLAVES de `porGestion` (dentro del parámetro) no distinguen caja ni tildes (unitario, no Dev)

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

### R-25 · Gestión desconocida DENTRO de `porGestion` es error de configuración (alto riesgo, no sembrar en Dev)

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

### R-26 · Versionar es AGREGAR una fila, nunca editar la vigente

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

> **Nota del 2026-10-06.** El caso da **FALLA** hoy y **no es de DN-05**: la versión 1 de `plantilla.obligatoriedad` está
> **inactiva en Dev desde el 2026-09-28** (`statecode=1`) y el primer check exige "las que hay están activas".
> Decisión pendiente del aprobador (reactivarla o relajar el check): no se toca desde las pruebas. La v1 sigue
> existiendo e intacta (R-19 lo confirma) y la v3 se agregó sin pisar ninguna.

**Cómo se sabe que falló.** Solo aparece una fila (la versión 1 se editó en vez de agregar la 2:
rompería `sanic_versionparametros` de Solicitudes ya procesadas con la versión anterior, que
dejaría de decir la verdad), o la versión 1 tiene un `sanic_valor` distinto al original.

**Automatizable.** API.

---

### R-27 · El mensaje al cliente no cambia con DN-03

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

### R-28 · La regla `OBLIGATORIEDAD` no cambia de código, orden, nivel ni dependencias

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

### R-29 a R-38 — DN-05 (2026-10-06): un caso por comportamiento nuevo

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

### Fase 2 — investigación y conclusión

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

### Resultados

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
| R-26 versionar agrega, no edita | | | ex O-08; **FALLA hoy, no es de DN-05**: la v1 está inactiva en Dev desde el 2026-09-28 (decisión pendiente del aprobador) |
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
La FALLA es R-26 y no es de DN-05 (nota en el propio caso). La corrida del mismo día antes de reescribir daba 20 OK ·
7 FALLA · 1 N-A: R-10, R-12, R-16, R-17, R-18 y R-20 por fixtures y aserciones de antes de DN-05, R-21 N-A por lo mismo,
y R-26.

---
## 04 — Comunicaciones al cliente

Set **M** (`CONVENCIONES.md` §4). Cubre `MPPP-COM` (detecta) y `MPPP-ENV` (envía). Las dos
comunicaciones son **el acuse** y **la respuesta final**, y las dos salen como **respuesta en el
hilo**, desde el buzón, **solo al remitente** (DF-03).

**La garantía que hay que romper para que esto falle**: *el cliente nunca recibe dos veces lo
mismo*. Ante la duda, no se reenvía y lo mira una persona (D-21).

---

### Por qué este set ya no es manual (2026-09-27)

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

- **(Retirado 2026-09-28) M-06 ya no espera el *recurrence* real de `MPPP-VIG`.** La versión
  anterior de esta nota decía que la espera podía tardar hasta ~11 minutos; en los hechos, tras la
  corrección del 2026-09-27, ya esperaba ~22 minutos (dos vueltas) y **igual daba FALLA** (ver la
  sección M-06 más abajo para la evidencia completa y por qué ese tramo del caso se movió a
  **V-07** en `pruebas/07-vigilancia.md`).

---

### Lo más valioso de este set: verificar qué le llega al cliente, no solo que "se mandó"

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

### M-01 · El acuse llega, y llega una sola vez

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

### M-02 · El acuse de una Rechazada cierra la Solicitud

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

### M-03 · Copia y remitente

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

### M-04 · La respuesta final, cuando todas las filas se resuelven

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

### M-05 · Nunca dos veces lo mismo

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

### M-06 · Envío iniciado sin confirmar → arma la precondición real (la espera la cierra V-07)

**Alcance recortado el 2026-09-28 — leé esto antes que el resto de la sección.** Este caso
verificaba dos cosas, y las dos estaban mal planteadas:

1. **Un evento de Bitácora `Requiere revision` con origen `MPPP-VIG`.** Investigado a fondo por el
   set V (`pruebas/07-vigilancia.md`, "Hallazgo aparte, sobre el propio caso M-06"): **ningún**
   bloque de `MPPP-VIG` escribe ese evento al marcar una Solicitud — ni el bloque 2 (reintentos
   agotados) ni el bloque 4 (`Envios_iniciados_sin_confirmar`, el que le importa a este caso). Ese
   evento solo lo genera `MPPP-ENV`, en otro escenario (el envío real de una comunicación, no la
   marca de revisión). Pedirle esto a `MPPP-VIG` era una expectativa mal fundada de este caso, no
   un defecto del flujo — **se sacó**.
2. **Que `sanic_requiererevision` pase a `true` dentro de un margen de espera real.** Esto es
   correcto en principio, pero **V-07** (`pruebas/07-vigilancia.md`) ya lo prueba, mejor, con el
   *recurrence* real de `MPPP-VIG`: lee el umbral de `vigilancia.minutos.sinresponder` EN VIVO
   (acá estaba hardcodeado a 40 minutos, asumiendo un valor de `P-06` sin comprobrarlo en cada
   corrida), corre la MISMA consulta `$filter` que usa el bloque
   `Envios_iniciados_sin_confirmar` **antes** de esperar nada (para saber si el defecto es de la
   consulta sin gastar tiempo), y espera con un margen medido con evidencia — **3 vueltas del
   `recurrence` (30 min) + colchón = 32 minutos**, no las 1-2 vueltas (~11 y ~22 min) que probó
   este caso el 2026-09-27 y que **ninguna de las dos alcanzó** (ver la nota diagnóstica al final
   de esta sección, conservada tal cual se escribió). Repetir esa espera acá, con un margen más
   corto y una precondición hardcodeada, **duplicaría cobertura entre dos prefijos distintos**
   (`CONVENCIONES.md` §4: un ID por archivo, sin colisiones de qué prueba cada uno) pagando otra
   vez ~20-32 minutos por corrida del set M por algo que V-07 ya prueba mejor. **Se sacó de acá.**

**D-62 (arreglado el 2026-09-28) es, con evidencia, la explicación más probable de por qué esas
1-2 vueltas no alcanzaron**: la etapa de arranque de `MPPP-VIG` estaba encadenada solo con
`runAfter: ["Succeeded"]`, así que un solo `Parametro_*` fallando (frecuente bajo carga de varios
agentes en paralelo, como pasaba en esa corrida) salteaba los 5 bloques enteros esa vez — ver
`pruebas/07-vigilancia.md`, "D-62 corregido". El defecto ya se corrigió y viajó al entorno; medir
si eso alcanza (con el margen correcto) es trabajo de V-07, no de este caso.

**Qué prueba ahora.** Que un correo real llegue hasta "En proceso" con el acuse real enviado, y que
la plataforma acepte el `PATCH` que arma el escenario "a medias" (`sanic_fechaacuseiniciado` más
viejo que el umbral real de `vigilancia.minutos.sinresponder`, `sanic_fechaacuseenviado` vacío) —
la misma precondición de datos que necesita V-07, pero ejercitada acá por el camino de correo REAL
(`MPPP-ING`), que V-07 no usa (arma la Solicitud por Custom API directa).

**Necesita.** El REMITENTE de este set. La Solicitud En proceso la crea este mismo caso, con su
propio correo real.

**Cómo.** Correo real (con adjunto válido) → se deja que el pipeline real complete su acuse normal
(no hay ventana para "interceptar a medias" un envío que el webhook dispara casi al instante: hay
que dejarlo terminar). Inmediatamente después, se fuerza el escenario a medias con un `PATCH`
directo: `sanic_fechaacuseiniciado` = más viejo que `vigilancia.minutos.sinresponder` (leído en
vivo), `sanic_fechaacuseenviado` = vacío. No se espera ningún ciclo de `MPPP-VIG`.

**Qué tiene que pasar.**

| Qué | Valor |
|---|---|
| El correo real llega a Solicitud "En proceso" con acuse enviado | **sí** |
| El `PATCH` que arma "a medias" se acepta (HTTP 200/204) | **sí** |

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py M` (caso `M-06`; ya no es el más
lento del set — corre en el orden de 1-3 minutos, dominado por la ingesta real del correo, no por
el *recurrence* de `MPPP-VIG`).

**Cómo se sabe que falló.** Si el correo real no genera Solicitud o no llega a "En proceso" con
acuse, es el mismo defecto que rompería M-01 (el camino de ingesta), visto desde acá. Si el
`PATCH` que arma el escenario a medias es rechazado (HTTP distinto de 200/204), no es simulable
desde acá NI desde V-07 (la misma precondición): hay un control de plataforma bloqueando la
escritura directa de esos dos campos, y el caso queda `N-A` con el motivo exacto.

**Automatizable.** API (`correr.py M`, caso `M-06`) — correo real, sin navegador, sin esperar el
*recurrence* de `MPPP-VIG`.

---

#### Nota diagnóstica conservada (versión anterior de este caso, 2026-09-27)

Se conserva tal cual se escribió — es la evidencia de POR QUÉ el margen de V-07 es 3 vueltas + colchón
en vez de 1 o 2, y de por qué D-62 es la explicación más probable. Esto ya NO corre en este caso;
lo corre V-07.

> **Corrección de margen (2026-09-27).** La primera versión de este caso esperaba hasta ~11
> minutos (un `recurrence` de 10 minutos + 1 de colchón). Con ese margen, **no alcanzó a verse**
> ni una sola vez, pese a que la condición ya estaba dada (`sanic_fechaacuseiniciado` bien
> backdateado, `sanic_fechaacuseenviado` vacío, verificado por Web API en el momento). El margen se
> subió a ~22 minutos (dos vueltas completas de colchón) para poder afirmar una cosa o la otra con
> evidencia, no con una sola muestra.
>
> **FALLA real, reproducida dos veces el 2026-09-27 (no fue un bug del caso).** Con la precondición
> confirmada por lectura directa (`sanic_fechaacuseiniciado` correctamente backdateado,
> `sanic_fechaacuseenviado` vacío, `sanic_requiererevision` en `false`), `sanic_requiererevision`
> **nunca pasó a `true`** ni con ~11 ni con ~22 minutos de margen (dos vueltas completas del
> `recurrence`). `MPPP-VIG` estaba vivo y corriendo en ese mismo lapso — se confirmó leyendo la
> Bitácora del entorno completo: había decenas de eventos `Error` con `sanic_origen = MPPP-VIG` y
> detalle `"Fallo la ingesta del correo..."` (del bloque `Correos_que_quedaron_en_la_bandeja`, otro
> bloque del mismo flujo) fechados en la ventana exacta de la espera — evidencia de apoyo directa
> de la causa que después confirmó y corrigió D-62 (ver `pruebas/07-vigilancia.md`).

---

### M-07 · El cuerpo lo arma el código, no el flujo — y no revela información interna

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

### Fase 2 — investigación y conclusión

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

### Resultados

Corrida real contra Dev, 2026-09-27/28 (`python3 herramientas/pruebas/correr.py M`, dos corridas:
la primera detectó el bug de M-03 descrito arriba y se corrigió el código; la segunda es la que
vale). Esa corrida tenía a M-06 con su alcance viejo: **6 OK · 1 FALLA · 0 N-A.**

**Recorrida el 2026-09-28** (`python3 herramientas/pruebas/correr.py C,R,U,I,M`), ya con el alcance
de M-06 recortado (ver su sección arriba) y con D-62 corregido: **7 OK · 0 FALLA · 0 N-A.**

| Caso | Solicitud | Resultado | Notas |
|---|---|---|---|
| M-01 acuse real, una sola vez | creada por el caso (correo real) | **OK** | acuse real leído, contenido y privacidad verificados |
| M-02 Rechazada real → Cerrada | creada por el caso (correo real) | **OK** | cierre automático confirmado |
| M-03 solo al remitente (correo real con copia) | creada por el caso (correo real) | **OK** | tras corregir el falso FALLA (ver nota arriba) |
| M-04 respuesta final real (digitar/aprobar por API) | la de M-01 | **OK** | digitar/aprobar por `MSCRMCallerID`, sin navegador |
| **M-05 nunca dos veces** | la de M-01 | **OK** | segundo disparo de MPPP-COM no reenvía |
| M-06 precondición real "a medias" (la espera pasó a V-07) | creada por el caso (correo real) | **OK** | 2026-09-28: alcance recortado (ver su sección) — corre en segundos, ya no en ~22 minutos |
| M-07 cuerpo del código + sin información interna | la de M-01 (+ M-04) | **OK** | ningún término interno encontrado en los correos reales |

---
## 05 — La app y sus comandos de la barra

Los botones de la barra nunca se ejecutaron en un navegador. Este archivo es el que más
probabilidad tiene de encontrar defectos.

**Dónde**: app **MDA - MPPP - Mantenimiento PPP**.

**Si NINGÚN botón aparece**, antes de reportar caso por caso mirá esto: en el `RibbonDiffXml` la
biblioteca se referencia como `$webresource:sanic_mppp_wr_js_comandos`, **sin `.js`**. El ejemplo
real del entorno que se usó de modelo sí llevaba la extensión en el nombre. Es el sospechoso
número uno y el arreglo es de un minuto.

**Este set es autocontenido (CONVENCIONES §8).** La versión de este archivo hasta el 2026-09-27
decía "Antes: `01` y `03`. Hace falta al menos una Solicitud En proceso con filas Validadas (C-01)
y una No reconocida (C-04)" — esa regla se da de baja: `set_a.py` arma su PROPIA Solicitud con
`E01-feliz-formato11.xlsx` (3 filas Validada) en cada caso que las necesita, y su propia Solicitud
"No reconocida"/"No es correo nuevo" para los casos de la grilla de Solicitudes. No depende de que
haya corrido antes ningún otro set.

### El corazón de este set, y la lección que lo justifica

Los comandos de la barra son **JavaScript que corre en el navegador**
(`recursos/js/comandos.js`). Separá dos cosas que se confunden:

- **El EFECTO del comando** (que la fila cambie de estado, que se registre el motivo, que se libere
  el claim) → se verifica **por API**, impersonando al usuario correcto (`MSCRMCallerID`), **sobre
  filas que este mismo set crea** — nunca sobre una fila cualquiera del entorno (ver más abajo, "por
  qué esto NO usa `comportamiento_roles.py --con-escritura`"). Automatizado.
- **Que el botón se DIBUJE y sea clickeable** → solo el navegador.

Y esa segunda mitad no es un detalle: el 2026-09-27 el botón "Descargar para digitar" estaba
**definido y publicado en el servidor** —verificado por tres caminos de API distintos— y **no se
dibujaba en el cliente**. Costó dos días. La causa era la metadata compilada del ribbon, que se
regenera desde el Command Checker (`&ribbondebug=true`). Y el diagnóstico clave: en el Command
Checker, un botón `(hidden)` es problema de **REGLAS**; un botón **AUSENTE de la lista** es problema
de **METADATA COMPILADA**. Mismo síntoma en pantalla, causas opuestas. El procedimiento completo de
diagnóstico está documentado, con evidencia, en `A-15` (fusionado acá el 2026-09-28 desde el ex set
H, `13-construido-hoy.md` — `CONVENCIONES.md` §4, "La regla del nombre") — acá se cita esa
distinción en los casos donde aplica (A-01, A-02), sin repetirla entera.

#### Por qué esto NO usa `comportamiento_roles.py --con-escritura`

`comportamiento_roles.py --con-escritura` (`recorrido_completo`) elige **cualquier** Fila en
Validada que encuentre en el entorno (`GET ...&$filter=sanic_estado eq {VALIDADA}&$top=2`, sin
filtrar por dueño ni por marca `ZZP-`) y la hace transicionar de verdad. Con **tres agentes
corriendo sets en paralelo sobre el mismo entorno Dev**, eso puede tomar y modificar una fila que
otro set sembró para su propio caso, en plena corrida. `corredor.py` ya lo apagaba por defecto
(bandera `--con-comportamiento-roles`, `CASOS_RIESGOSOS_PARA_CONCURRENCIA`) por esta misma razón.
Este set no reactiva esa bandera: en su lugar, **repite el mismo patrón que `set_i.py` (I-13) y
`set_b.py` (B-07/B-12/B-18/B-20) ya establecieron** — impersonar `User1`/`User2` con `MSCRMCallerID`
directo sobre `ctx.dv`, actuando **solo sobre Solicitudes/Filas que el propio caso creó con
`ctx.procesar()`**. Mismo comportamiento del servidor, cero riesgo de tocar el trabajo de otro
agente.

### Fase 1 + fase 2, qué cubre este set y qué no

Investigado el 2026-09-27 contra `recursos/js/comandos.js` y `diseno/05-app-model-driven.md`:

- **Fase 1** (8 comandos originales: `digitada`, `aprobar`, `rechazadaAs400`, `anular`, `devolver`
  en Fila; `atendido`, `descartar`, `revisado` en Solicitud) — cubiertos por A-01 a A-13.
- **Fase 2 agregó dos cosas a la barra**, y las dos se investigaron:
  - **"Descargar para digitar"** (DN-02, `descargarParaDigitar`): tiene dueño propio, **set E,
    caso E-01** (`pruebas/11-exportacion-excel.md`). No se duplica acá.
  - **"Atendida"** sobre Fila (`atendida`, apaga `sanic_requiereatencionmanual`, el cerrojo que le
    impide al bot volver a tomar una fila que ya es de un ejecutivo — `03` §3). **No tenía caso en
    este archivo hasta hoy**: se agregó como **A-14**. `set_b.py` (B-19, B-20) ya prueba que el BOT
    prende la marca y que **cualquier transición de estado** la apaga sola; lo único que faltaba
    era el botón en sí — apagarla **sin** cambiar `sanic_estado` — y A-14 lo cubre sin duplicar B.

### Un hallazgo de esta migración: el origen de Bitácora nunca es "App"

La versión vieja de A-03 decía "Bitácora `Fila digitada`, origen **App**". Verificado leyendo
`src/Sanic.Mppp.Plugins/Steps/StepsDeFila.cs` (las cuatro llamadas a `RegistrarEvento` de este
archivo: `PostTransicionDeFilaStep`, `AtenderPorClasificarStep`, `RevisionAtendidaStep`) y
`Steps/PlomeriaDeFila` — **las cuatro usan `OrigenDelEvento.Plugin`**. `grep -rn
"OrigenDelEvento.App" src/` no devuelve NINGÚN resultado: el valor `App` (159460008) está definido
en el choice pero **ningún step lo asigna nunca**, sin importar si quien transiciona es una persona
por la app o el bot. A-03, A-08, A-10, A-11 y A-12 quedaron corregidos para esperar `Plugin`
(159460007), con esta evidencia.

### Otro hallazgo: la lista blanca puede no tener a "Atendida" (ver A-14)

`diseno/04-matriz-privilegios.md` §1 dice, sobre la lista blanca de columnas de Fila: "—desde la
fase 2— `sanic_requiereatencionmanual` (la apaga el botón *Atendida*... por eso sí entra a la lista
blanca)". Pero `src/Sanic.Mppp.Plugins/Steps/ReglasDeSteps.cs`, `PermitidasEnFila`, dice
textualmente:

```csharp
private static readonly string[] PermitidasEnFila = { "sanic_estado", "sanic_mensaje", "sanic_notainterna" };
```

`sanic_requiereatencionmanual` **no está**. Si esto es así en el entorno real, el botón "Atendida"
falla siempre con `HTTP 400`: "la(s) columna(s) sanic_requiereatencionmanual no está(n)
permitida(s) para este usuario". A-14 se escribió para probar el comportamiento **diseñado**
(que el botón funcione); si da FALLA con ese mensaje exacto, es esta discrepancia entre el diseño y
`ReglasDeSteps.cs`, no un problema de datos de prueba — y corresponde abrir un defecto en
`diseno/PENDIENTES.md` (fuera del alcance de este archivo: lo hace quien lo confirme contra Dev).

---

### A-01 · Los botones aparecen donde deben

**Qué prueba.** Los `CustomAction` de Fila (HomepageGrid y SubGrid) y de Solicitud, dibujados.

**Necesita.** Sesión `local/sesion-ejecutivo.json` (rol `SR - MPPP - Ejecutivo`, verificada por
`estado_sesiones.py`); la vista **Por digitar** con al menos una fila en Validada.

**Cómo.** `MPPP_USUARIO=ejecutivo npx playwright test ribbon-fila` (`pruebas/gui/`).

**Qué tiene que pasar.** Con una fila Validada seleccionada, los 5 botones que la admiten
(`digitada`, `rechazadaas400`, `anular`, `atendida`, `descargarparadigitar`) están presentes y
habilitados; `aprobar`/`devolver` no aparecen (solo admiten Digitada, fuera del alcance de esta
vista — comportamiento correcto, no un defecto). Sin selección, ningún botón propio se dibuja.

**Cómo se verifica.** La salida de `MPPP_USUARIO=ejecutivo npx playwright test ribbon-fila`
(`pruebas/gui/casos/ribbon-fila.spec.mjs`, ya escrito y con sus selectores verificados en vivo el
2026-09-27 — falta correrlo de punta a punta con sesión real).

**Cómo se sabe que falló.** Ver la distinción de `A-15` (este mismo archivo): en el
Command Checker (`&ribbondebug=true`), un botón que aparece **`(hidden)`** es un problema de
**REGLAS** (`EnableRule` contra la selección actual, revisar `TRANSICIONES_VALIDAS`); un botón
**AUSENTE de la lista entera** es un problema de **METADATA COMPILADA** (regenerar desde el maker
portal), y ningún ajuste de reglas lo arregla. Mismo síntoma en pantalla ("no lo veo"), causas
opuestas — confundirlas es lo que costó dos días con DN-02.

**Automatizable.** GUI (`pruebas/gui/casos/ribbon-fila.spec.mjs`, sesión `ejecutivo`), **HomepageGrid
Y SubGrid** desde el 2026-09-28 (antes solo HomepageGrid — gap cerrado esta migración: el segundo
test del archivo abre la Solicitud sembrada por su GUID propio y verifica los mismos 5 botones en la
subgrilla de Filas). `herramientas/pruebas/sets/set_a.py::a01()` invoca este arnés por subproceso y
reporta OK/FALLA real, ya no N-A. **Gap que sigue sin cubrir**: los 3 botones de Solicitud
(`atendido`/`descartar`/`revisado`) — punto de extensión, ver `pruebas/gui/README.md` §6.

---

### A-02 · Los comandos genéricos NO aparecen (12.6)

**Qué prueba.** Los `HideCustomAction` en las tablas del sitemap.

**CORRECCIÓN 2026-09-28 (CONVENCIONES.md §7, "nombrar, no contar").** Esta sección contaba "10
tablas" desde antes del 2026-09-27, cuando `AutorizacionCliente` todavía no tenía entrada propia de
sitemap (se agregó el 2026-09-27, `diseno/05-app-model-driven.md` §2). Hoy son **11**: se nombran
las 11 en la tabla de abajo, no se repite el número viejo.

**D-69, capa 1 (el playbook) cerrada el 2026-09-28; el caso SIGUE EN ROJO por una causa
distinta, ya conocida.** Esta corrección solo actualizaba el NÚMERO de tablas; no alcanzaba a decir
que `playbooks/importacion/comandos.md` seguía sin el bloque `ribbon` de `AutorizacionCliente` — la
tabla nunca había entrado al playbook, así que sus comandos genéricos ("Flujo", "Enviar vínculo" y el
resto) seguían a la vista en el entorno real. Este caso fue el que lo cazó: la primera corrida real
de `a-02-comandos-genericos-ocultos.spec.mjs` (2026-09-28) encontró `AutorizacionCliente` en rojo
mientras las otras 10 tablas pasaban. Causa raíz y las dos capas del arreglo del PLAYBOOK (el bloque
que faltaba + `importar_solucion.py --solo-verificar` denunciando ahora cualquier tabla de la app sin
bloque de ocultos) en `diseno/05-app-model-driven.md`, nota de corrección de DA-06.

**Con el bloque agregado y reimportado, el `RibbonDiffXml` de `AutorizacionCliente` está bien
definido y publicado en el servidor** — `importar_solucion.py` lo confirmó releyendo el entorno
(`RetrieveEntityRibbon`) después de importar: `creado`, sin faltantes. **Pero la corrida real de este
caso (2026-09-28, después del import) sigue en rojo para `AutorizacionCliente`: "Flujo" y "Enviar
vínculo" siguen apareciendo en el DOM.** Es el MISMO defecto REGLA-vs-METADATA que costó dos días con
DN-02 (`diseno/06-inventario-componentes.md`, ítem 5; `A-15` más abajo): `AutorizacionCliente` nunca
había tenido una personalización de ribbon antes de hoy, así que su metadata COMPILADA nunca se
regeneró — y publicar (`PublishXml`, que `importar_solucion.py` sí hace) **no alcanza** para eso.
Hace falta un clic manual, de alguien con privilegio de PLATAFORMA (System Administrator o System
Customizer, no un rol de negocio): Command Checker (`&ribbondebug=true` en la URL de la app) →
"Regenerate ribbon metadata" sobre `sanic_mppp_sol_mantenimientoppp`. **Ninguna herramienta de este
repo puede hacerlo por API** (confirmado en `herramientas/pruebas/sets/set_a.py::a01()`, misma nota).
Las otras 10 tablas pasan porque su metadata ya se había regenerado antes, en otra sesión — el
reimport de hoy no las tocó de forma que necesitaran regenerarse de nuevo.

**Hallazgo aparte, no relacionado con D-69**: la misma corrida encontró `Autorizado` en rojo por un
motivo distinto — `irASitemap` no encontró la entrada "Correos Autorizados" (error de navegación, no
de `HideCustomAction`). `playbooks/importacion/comandos.md` no toca esa tabla en este cambio; queda
para quien mantiene `pruebas/gui/` investigar si es un flake o una etiqueta que cambió.

**Necesita.** Las 4 sesiones (`estado_sesiones.py` primero); acceso a las 11 entradas/subgrids entre
las cuatro. **Alcance real del arnés automatizado (esta migración):** `HideCustomAction` es una
personalización de TABLA, no de rol — la metadata compilada es la misma para cualquier usuario que
abra esa tabla. Dos sesiones alcanzan sin repetir el mismo chequeo cuatro veces: `ejecutivo` (9
tablas que ve Ejecutivo/Supervisor) y `admin_tecnico` (Parámetro y Regla, exclusivas de
Configuración) — ver `pruebas/gui/casos/a-02-comandos-genericos-ocultos.spec.mjs` y
`TABLAS_A02`/`selectores.mjs` para el detalle completo de la decisión.

**Cómo.** En **cada** entrada del sitemap (o subgrid), con la sesión que la vea, seleccioná una fila
y mirá la barra (incluido el menú `…`).

**Esperado.** En **ninguna** tabla aparecen: **Eliminar**, **Asignar**, **Compartir**, **Flujo**,
**Enviar vínculo**, **Combinar**. Y **Nuevo** no aparece en Solicitud, Fila, ResultadoRegla ni
Bitácora (sí en los catálogos, donde el alta es de una persona).

| Tabla | Sin los 6 genéricos | Sin "Nuevo" | Dónde |
|---|---|---|---|
| Solicitud | | **sí** | sitemap "Solicitudes" |
| Fila | | **sí** | sitemap "Filas" |
| ResultadoRegla | | **sí** | subgrid "Reglas" del formulario de Solicitud |
| Bitácora | | **sí** | subgrid "Bitácora" del formulario de Solicitud |
| Cliente | | (Nuevo **sí** aparece) | sitemap "Clientes" |
| Plan | | (Nuevo **sí** aparece) | sitemap "Planes" |
| Autorizado | | (Nuevo **sí** aparece) | sitemap "Correos Autorizados" |
| AutorizacionPlan | | (Nuevo **sí** aparece) | sitemap "Autorizaciones - Planes" |
| AutorizacionCliente | | (Nuevo **sí** aparece) | sitemap "Autorizaciones - Clientes" (agregada 2026-09-27) |
| Parámetro | | (Nuevo **sí** aparece) | sitemap "Parámetros" (solo Administrador técnico) |
| Regla | | (Nuevo **sí** aparece) | sitemap "Reglas" (solo Administrador técnico) |

**Cómo se verifica.** `MPPP_USUARIO=ejecutivo npx playwright test a-02-comandos-genericos-ocultos` y
`MPPP_USUARIO=admin_tecnico npx playwright test a-02-comandos-genericos-ocultos` (`pruebas/gui/`);
`herramientas/pruebas/sets/set_a.py::a02()` invoca las dos corridas por subproceso y las combina en
un solo caso.

**Cómo se sabe que falló.** Mismo diagnóstico REGLA-vs-METADATA de A-01/`A-15` si un
`HideCustomAction` no surtiera efecto en alguna tabla puntual. **No alcanza con `RetrieveEntityRibbon`
por Web API** (la misma llamada que usa `herramientas/construir/importar_solucion.py` para publicar):
es uno de los "tres caminos de API" que el 2026-09-27 confirmaron que el botón de DN-02 **existía**
en el servidor mientras no se dibujaba — esa API ve la definición del ribbon, no la metadata
compilada que de verdad sirve el navegador. Usarla acá repetiría el mismo error de diagnóstico que
motivó este archivo entero.

**Automatizable.** GUI (`pruebas/gui/casos/a-02-comandos-genericos-ocultos.spec.mjs`, sesiones
`ejecutivo` + `admin_tecnico`) — escrito y conectado el 2026-09-28. Las etiquetas de los 6 comandos
genéricos son las estándar de Unified Interface (`COMANDOS_GENERICOS`, `selectores.mjs`), no
confirmadas todavía contra esta app puntual — a corregir con la primera corrida real si no calzan.

---

### A-03 · Digitada, sobre varias filas a la vez (acción masiva, DA-02)

**Qué prueba.** `Sanic.Mppp.Comandos.digitada` y su EFECTO cuando se aplica a varias filas: **una
petición por fila** (`aplicar()` en `comandos.js`), nunca un lote, para que una fila rechazada no
arrastre a las demás.

**Necesita.** Los roles de Ejecutivo (`User1`), verificados EN VIVO contra
`systemuserroles_association` (`preparacion_comun.verificar_roles`, resuelto una vez en
`preparar()` — igual que `set_b.py`); una Solicitud propia de este caso con 3 Filas Validada
(`ctx.procesar()` con `E01-feliz-formato11.xlsx`).

**Cómo.** Por cada una de las 3 filas: `PATCH sanic_mppp_tbl_filas(<id>) {sanic_estado: Digitada}`
impersonando a Ejecutivo (`MSCRMCallerID`) — una petición por fila, como hace `aplicar()`.

**Qué tiene que pasar.** Las 3 dan `HTTP 204`; las 3 quedan en **Digitada**; la Bitácora de la
Solicitud tiene un evento `Fila digitada` por cada una, con el número de fila correcto y **origen
Plugin** (no "App": ver el hallazgo arriba).

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py A` (caso `A-03`).

**Cómo se sabe que falló.** Si alguna fila no llega a `204`, o queda en otro estado, o la Bitácora
no registra su evento (o lo registra con `sanic_origen = App`, que ningún step asigna hoy — sería
indicio de que alguien "corrigió" el origen sin actualizar este caso).

**Automatizable.** API (`correr.py A`, caso `A-03`) para el EFECTO. **GUI, no automatizado esta
vuelta**: que el diálogo diga la cantidad exacta (*"¿Aplicar 'Digitada' a 3 fila(s)
seleccionada(s)?"*), que el resultado diga *"3 de 3 fila(s) actualizada(s)."*, que el idioma sea el
del usuario, y que la grilla se refresque sola — todo eso es DOM, punto de extensión de
`pruebas/gui/`.

---

### A-04 · Cancelar no cambia nada

**Qué prueba.** Que el diálogo de confirmación (`openConfirmDialog`) realmente frene: cuando
`respuesta.confirmed` es falso, `ejecutar()` no llama a `aplicar()`.

**Necesita.** Sesión `local/sesion-ejecutivo.json`.

**Cómo.** Seleccioná filas, apretá **Digitada** y dale **Cancelar**.

**Esperado.** **Nada** cambia. Ninguna fila cambia de estado, no hay entradas nuevas en Bitácora.

**Cómo se verifica.** `MPPP_USUARIO=ejecutivo npx playwright test a-04-cancelar-no-cambia-nada`
(`pruebas/gui/casos/a-04-cancelar-no-cambia-nada.spec.mjs`, escrito el 2026-09-28).
`herramientas/pruebas/sets/set_a.py::a04()` lo invoca por subproceso.

**Cómo se sabe que falló.** Sin llamar al Web API no hay ninguna diferencia observable entre
"canceló" y "no hizo nada": esta rama del JS **solo la puede probar un navegador real**, apretando
el botón y después Cancelar, y mirando que ninguna fila haya cambiado. Ningún caso de API puede
sustituir esto — sería probar lo que el sistema *no* hizo por no habérselo pedido, no lo que el
diálogo *decidió*. **Cómo evita el falso verde** (el mismo riesgo: "no pasó nada" es lo que también
se ve si el botón nunca se apretó): el caso arma DOS evidencias independientes, no una — (1)
POSITIVA, que el diálogo de confirmación REALMENTE apareció después del click (si no aparece, el
test explota con un error explícito, nunca sigue de largo como si "nada" fuera la prueba); (2)
NEGATIVA con RED, no solo con la UI: cero peticiones de escritura al Web API grabadas desde antes
del primer click hasta después de cerrar el diálogo con Cancelar (`ayudantes/dialogos.mjs::
grabarEscriturasApi`). Detalle completo en el comentario de cabecera de ese archivo.

**Automatizable.** GUI (`pruebas/gui/casos/a-04-cancelar-no-cambia-nada.spec.mjs`, sesión
`ejecutivo`) — escrito y conectado el 2026-09-28.

---

### A-05 · Sin selección: ningún botón propio se dibuja (AUSENTE, no "deshabilitado")

**CORRECCIÓN FECHADA 2026-09-28.** El título corto de este caso venía arrastrando, en
`herramientas/pruebas/sets/set_a.py` (título de `pc.Caso`) y en el nombre con el que se lo citaba en
otros documentos, la expectativa VIEJA e INCORRECTA: "sin selección, los botones propios están
**deshabilitados**". El cuerpo de este caso ya documentaba, desde el 2026-09-27, el hallazgo real
(ver "Esperado" abajo: la plataforma los **ausenta**, no los deja grises) — lo que faltaba corregir
era el título, que es lo único que lee quien solo mira el resumen de `correr.py`. AUSENTE y
DESHABILITADO son diagnósticos **opuestos** con el mismo síntoma en pantalla ("no lo veo") — ver
A-15, este mismo archivo: confundirlos es justo lo que costó dos días con DN-02. Esta migración
además separó la aserción a su propio archivo (`casos/a-05-sin-seleccion.spec.mjs`, con la misma
nota de corrección) para que pueda invocarse y reportarse de forma independiente de A-01.

**Qué prueba.** El `EnableRule` `Mscrm.SelectionCountAtLeastOne`.

**Necesita.** Sesión `local/sesion-ejecutivo.json`.

**Cómo.** `MPPP_USUARIO=ejecutivo npx playwright test a-05-sin-seleccion`.

**Esperado.** Sin ninguna fila seleccionada, **ningún** botón propio se dibuja (confirmado en vivo
el 2026-09-27: no quedan "grises" — desaparece el grupo entero, distinto de lo que asumía una
versión anterior del arnés).

**Cómo se verifica.** La salida de `MPPP_USUARIO=ejecutivo npx playwright test a-05-sin-seleccion`
(`pruebas/gui/casos/a-05-sin-seleccion.spec.mjs`).
`herramientas/pruebas/sets/set_a.py::a05()` lo invoca por subproceso.

**Cómo se sabe que falló.** Si aparece cualquiera de los botones propios sin selección — puede ser
un cambio real de comportamiento de la plataforma, revisar antes de asumir defecto. También falla si
alguno queda "presente pero deshabilitado" (la expectativa vieja): sería un cambio real de
comportamiento a investigar, no un defecto silencioso de este caso.

**Automatizable.** GUI (`pruebas/gui/casos/a-05-sin-seleccion.spec.mjs`, sesión `ejecutivo`) —
conectado el 2026-09-28.

---

### A-06 · Aprobar, como Supervisor — y el cierre automático de la Solicitud

**Qué prueba.** `Sanic.Mppp.Comandos.aprobar`, la segregación de funciones en el camino FELIZ
(otro usuario aprueba lo que el Ejecutivo digitó), y que al resolverse todas las filas de la
Solicitud, `PostTransicionDeFilaStep` la pase a **Procesada** y arme la respuesta final.

**Necesita.** Roles de Ejecutivo (`User1`) y Supervisor (`User2`) verificados; una Solicitud propia
con 3 Filas Validada (`ctx.procesar()`, `E01-feliz-formato11.xlsx`).

**Cómo.**
1. Impersonando Ejecutivo, `PATCH sanic_estado = Digitada` en las 3 filas.
2. Impersonando Supervisor (otro usuario, no quien digitó), `PATCH sanic_estado = Aprobada` en las
   3 filas.
3. Leer la Solicitud y su Bitácora.

**Qué tiene que pasar.** Las 3 filas quedan **Aprobada**; la Solicitud pasa a **Procesada**, con
`sanic_respuestafinalcontenido` no vacío (el TEXTO exacto de esa respuesta lo verifica **M-04**, no
este caso) y un evento `Procesada` en la Bitácora.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py A` (caso `A-06`).

**Cómo se sabe que falló.** Si alguna fila no llega a Aprobada, o la Solicitud no cierra sola al
quedar sin filas Validada/Digitada abiertas (`CierreDeSolicitud`), o no arma ninguna respuesta.

**Automatizable.** API (`correr.py A`, caso `A-06`), sobre datos propios — reemplaza el proxy viejo
de `comportamiento_roles.py --con-escritura`, que actuaba sobre cualquier fila del entorno (ver
arriba, "por qué esto NO usa..."). GUI opcional (sesión `supervisor`) para confirmar la experiencia
en pantalla — no bloqueante.

---

### A-07 · El plugin manda, no el botón (DA-07)

**Qué prueba.** Lo más importante de la seguridad: la regla real está en el servidor, no en qué
botón dibuja el cliente — y que una tanda mixta aplique lo que corresponde y rechace el resto **sin
que unas filas arrastren a otras** (DA-02).

**Necesita.** Roles de Ejecutivo y Supervisor verificados; una Solicitud propia con 3 Filas
Validada.

**Cómo.** Llamadas DIRECTAS al Web API (bypaseando cualquier botón), impersonando con
`MSCRMCallerID`:
1. Supervisor intenta `PATCH sanic_estado = Digitada` sobre una fila Validada (el botón ni se
   dibujaría para este rol, pero la llamada se manda igual).
2. Ejecutivo digita legítimamente las otras dos filas.
3. Ejecutivo intenta `PATCH sanic_estado = Aprobada` sobre una fila que él mismo digitó.
4. En una MISMA tanda (una petición por fila, como `aplicar()`), Supervisor aplica "Aprobar" sobre
   dos filas a la vez: una que sigue Validada (transición inexistente, salta Digitada) y otra
   Digitada por Ejecutivo (transición legítima).

**Qué tiene que pasar.**

| Intento | Resultado |
|---|---|
| 1: Supervisor digita | `400`, motivo nombra el **rol** |
| 3: Ejecutivo aprueba lo que digitó | `400`, motivo nombra el **rol** (Aprobar es de Supervisor/RPA) |
| 4, fila Validada→Aprobada (salta Digitada) | `400`, "Transición no permitida" |
| 4, fila Digitada→Aprobada (la otra, MISMA tanda) | `204` — **se aplica igual**, aunque la otra de la tanda haya fallado |

Ninguna fila rechazada cambia de estado; ninguna queda a medias.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py A` (caso `A-07`).

**Cómo se sabe que falló.** Si CUALQUIERA de las transiciones prohibidas se aplica, el botón estaría
decidiendo y la regla se saltearía llamando al Web API directo — es el caso de seguridad más grave
de este archivo. Si la fila que SÍ correspondía no se aplica porque la otra de la tanda falló, DA-02
(una petición por fila) está rota y se volvió a un lote todo-o-nada.

**Automatizable.** API (`correr.py A`, caso `A-07`), sobre datos propios. La segregación de
funciones con el MISMO usuario en los dos roles (alguien que digitó como Ejecutivo y después
aprueba como Supervisor) ya la prueba `I-13` de punta a punta con roles reales — no se repite acá.
GUI opcional para confirmar el mensaje que ve el usuario — no bloqueante.

---

### A-08 · Rechazada en AS400 — el diálogo de motivo

**Qué prueba.** El EFECTO del diálogo de motivo (DA-03: creación rápida de
`sanic_mppp_tbl_motivoaccion`, transporte que se lee y se borra) y que el servidor exija el mensaje
**aunque nadie pase por el diálogo**.

**Necesita.** Rol de Ejecutivo verificado (tiene `C R W D` en alcance Usuario sobre
`sanic_mppp_tbl_motivoaccion`, `diseno/04-matriz-privilegios.md` §2); una Solicitud propia con Filas
Validada.

**Cómo.**
1. Impersonando Ejecutivo, `PATCH sanic_estado = RechazadaEnAS400` **sin** `sanic_mensaje` sobre una
   fila (bypass total del diálogo: nunca se creó ningún `MotivoAccion`).
2. Impersonando Ejecutivo: `POST sanic_mppp_tbl_motivoaccions {sanic_motivo: "<texto>"}` → `GET` de
   vuelta el mismo texto → `DELETE` — exactamente lo que hace `pedirMotivo()` en `comandos.js`.
3. Impersonando Ejecutivo, `PATCH sanic_estado = RechazadaEnAS400, sanic_mensaje = "<texto>"`.
4. Impersonando Ejecutivo, `POST sanic_mppp_tbl_motivoaccions {}` (sin `sanic_motivo`).

**Qué tiene que pasar.**

| Paso | Resultado |
|---|---|
| 1 (sin mensaje) | `400`, motivo nombra el **mensaje** exigido |
| 2 (crear/leer/borrar el transporte) | los tres, `2xx` |
| 3 (con mensaje) | `204`; la fila queda **Rechazada en AS400** con `sanic_mensaje = "<texto>"`; Bitácora `Fila rechazada en AS400`, **origen Plugin** |
| 4 (`sanic_motivo` vacío) | **SE GUARDA igual** (`2xx`) — corregido tras correrlo en vivo: ver abajo |

**Corrección tras la corrida real del 2026-09-27 (paso 4).** El playbook
(`playbooks/tabla/sanic_mppp_tbl_motivoaccion.md`) dice que `sanic_motivo` es obligatoria y que "la
obligatoriedad la impone el formulario **y la plataforma**". Verificado contra Dev: la metadata SÍ
tiene `RequiredLevel = ApplicationRequired` ("Business Required"), pero ese nivel es una validación
de **FORMULARIO** (Microsoft Learn: *"the platform will not enforce this"*), no del Web API — un
`POST` directo sin `sanic_motivo` se guarda igual. La mitad "la plataforma" del playbook es
imprecisa: la obligatoriedad real depende 100% de que el diálogo (un formulario) la exija, que es
GUI y no lo prueba este caso.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py A` (caso `A-08`).

**Cómo se sabe que falló.** Si el paso 1 se aplica sin mensaje, el servidor no está exigiendo lo que
el diálogo solo simula del lado del cliente. Si el paso 3 no deja el texto en `sanic_mensaje`, el
cliente del banco no va a leer el motivo real. Si el paso 4 empieza a dar `400`, alguien le agregó
una validación server-side a `sanic_motivo` — hay que actualizar este caso (y sería una buena
noticia: la obligatoriedad dejaría de depender solo del formulario).

**Automatizable.** API (`correr.py A`, caso `A-08`) para el EFECTO completo, incluida la
obligatoriedad. **GUI, no automatizado esta vuelta**: que el diálogo se DIBUJE centrado, con el
título *"Rechazada en AS400 — 2 fila(s)"* y un solo campo — arnés pendiente, ver
`pruebas/gui/README.md` §6 punto 3.

---

### A-09 · Cancelar el diálogo de motivo

**Qué prueba.** Que cerrar con la **X** (sin guardar) no cambie nada: `pedirMotivo()` devuelve
`null` en el rechazo del `openForm`, y `ejecutarConMotivo` no llama a `aplicar()`.

**Necesita.** Sesión `local/sesion-ejecutivo.json`.

**Cómo.** Apretá **Anular** sobre filas seleccionadas y cerrá el diálogo con la **X**.

**Esperado.** **Ninguna** fila cambia de estado. No hay ningún `MotivoAccion` colgado.

**Cómo se verifica.** `MPPP_USUARIO=ejecutivo npx playwright test a-09-cancelar-dialogo-motivo`
(`pruebas/gui/casos/a-09-cancelar-dialogo-motivo.spec.mjs`, escrito el 2026-09-28 — usa "Anular"
como comando con diálogo de motivo, admite Validada directo sin precondición extra).
`herramientas/pruebas/sets/set_a.py::a09()` lo invoca por subproceso.

**Cómo se sabe que falló.** Mismo motivo que A-04: no hay ninguna llamada al Web API que distinga
"cerró con la X" de "no hizo nada". Solo un navegador real, apretando la X de verdad, prueba esta
rama. Mismo mecanismo de evidencia que A-04 (positiva: el diálogo apareció de verdad; negativa con
red: cero escrituras al Web API, ni el `POST` del `MotivoAccion` transporte ni el `PATCH` de la
Fila) — ver `ayudantes/dialogos.mjs`.

**Automatizable.** GUI (`pruebas/gui/casos/a-09-cancelar-dialogo-motivo.spec.mjs`, sesión
`ejecutivo`) — escrito y conectado el 2026-09-28.

---

### A-10 · Anular y Devolver

**Qué prueba.** Los otros dos comandos con motivo, y qué distingue de verdad a una fila **Devuelta**
de una recién validada: **tiene nota interna**, y **nunca** mensaje al cliente.

**Necesita.** Roles de Ejecutivo y Supervisor verificados; una Solicitud propia con Filas Validada.

**Cómo.**
1. Impersonando Ejecutivo, `PATCH sanic_estado = Anulada, sanic_mensaje = "<texto>"` sobre una fila
   Validada.
2. Impersonando Ejecutivo, digitar OTRA fila (`Digitada`); impersonando Supervisor, `PATCH
   sanic_estado = Validada, sanic_notainterna = "<nota>"` (Devolver).
3. Impersonando Ejecutivo, digitar una TERCERA fila; impersonando Supervisor, `PATCH sanic_estado =
   Validada` **sin** `sanic_notainterna`.

**Qué tiene que pasar.**

| Paso | Resultado |
|---|---|
| 1 (Anular) | `204`; fila **Anulada**, `sanic_mensaje = "<texto>"`; Bitácora `Fila anulada`, origen Plugin |
| 2 (Devolver) | `204`; fila vuelve a **Validada**; `sanic_notainterna = "<nota>"`; **`sanic_mensaje` sigue vacío** (la nota NUNCA sale en el correo al cliente — `04 §1`, corregido el 2026-09-24 tras un incidente real); `_sanic_digitadapor_value` queda **vacío** (se olvida quién digitó: la próxima aprobación exige la segregación entera otra vez); Bitácora `Fila devuelta`, origen Plugin |
| 3 (Devolver sin nota) | `400`, motivo nombra la **nota** |

> **Devolver es el más sutil**: la fila vuelve a **Validada**, igual que una recién validada. Lo
> único que la distingue (y lo que la vista **Devueltas** filtra) es que **tiene
> `sanic_notainterna`**. Que también aparezca en **Por digitar** es un asunto de la VISTA (GUI, fuera
> de este caso): la condición de DATOS que las separa es la que este caso verifica.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py A` (caso `A-10`).

**Cómo se sabe que falló.** Si `sanic_mensaje` queda con algo después de Devolver, un correo real le
va a mostrar al cliente una nota interna que no es asunto suyo (ya pasó una vez). Si
`sanic_digitadapor` no se limpia, la próxima aprobación podría saltarse la segregación de
funciones.

**Automatizable.** API (`correr.py A`, caso `A-10`), sobre datos propios — reemplaza el proxy viejo
de `comportamiento_roles.py --con-escritura`. GUI opcional para confirmar las vistas
Devueltas/estado en pantalla — no bloqueante.

---

### A-11 · Atendido y Descartar, sobre Solicitudes

**Qué prueba.** Los dos comandos de la grilla de Solicitudes (`AtenderPorClasificarStep`): cierran
lo que está **por clasificar** sin comunicarle nada al cliente.

**Necesita.** Rol de Ejecutivo verificado; dos Solicitudes propias: una que clasifique como **No
reconocida** (remitente sin autorización) y otra como **No es correo nuevo** (asunto `RE:`, igual
que `C-02`).

**Cómo.**
1. Crear y clasificar (`sim.clasificar`) las dos Solicitudes propias.
2. Impersonando Ejecutivo, `PATCH sanic_estadoprocesamiento = Cerrada` sobre la primera (Atendido).
3. Impersonando Ejecutivo, `PATCH sanic_estadoprocesamiento = Descartada` sobre la segunda
   (Descartar).

**Qué tiene que pasar.** Las dos dan `204`; la primera queda **Cerrada** (Bitácora
`No reconocida atendida`, origen Plugin), la segunda **Descartada** (Bitácora `Descartada`, origen
Plugin); en las dos, `sanic_acusecontenido`/`sanic_respuestafinalcontenido` **no cambian** (ninguna
comunicación nueva al cliente: `AtenderPorClasificarStep` no llama a ningún armador de respuesta,
verificado leyendo el código).

> **DEFECTO REAL encontrado el 2026-09-27, corriendo este caso por primera vez.** El setup del paso
> 1 (`sim.clasificar` sobre la Solicitud "No reconocida") reventó con el error genérico de
> `ClasificarCorreoApi` ("No se pudo clasificar el correo. El equipo tecnico ya tiene el detalle.").
> Leyendo `plugintracelogs` en vivo, la causa real es:
>
> ```
> System.InvalidCastException: Unable to cast object of type 'System.Guid' to type
> 'Microsoft.Xrm.Sdk.EntityReference'.
>    at Sanic.Mppp.Plugins.Datos.AvisosDataverse.<>c.<AvisarAEjecutivos>b__4_1(Entity e)
> ```
>
> `AvisosDataverse.AvisarAEjecutivos` (`Datos/Avisos.cs`) lee `systemuserroles.systemuserid` con
> `GetAttributeValue<EntityReference>` — pero `systemuserroles` es una tabla **intersect** (la
> asociación N:N nativa entre `systemuser` y `role`), y sus columnas de relación son `Guid` crudo,
> no `EntityReference`. **Esto rompe TODO camino de clasificación donde `procesar = false`**
> (remitente no reconocido, respuesta, reenvío ilegible) — es decir, cada vez que
> `ClasificarCorreo.Ejecutar` llama a `_avisos.AvisarAEjecutivos` y existe al menos un Ejecutivo
> habilitado (lo hay: `User1`). **Probablemente también rompe `C-02` y `C-04` de `set_c.py`**, que
> pasan por el mismo camino — no es exclusivo de este caso ni de este set. Reportado como `FALLA`
> con la evidencia completa (CONVENCIONES §3: un defecto real no se disfraza de `N-A`); falta
> abrirlo en `diseno/PENDIENTES.md` con un ID.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py A` (caso `A-11`).

**Cómo se sabe que falló.** Si el estado no cambia, o cambia a algo que no sea Cerrada/Descartada
(`AtenderPorClasificarStep` rechaza cualquier otro destino con "solo se puede cerrar o descartar lo
que está por clasificar"), o si aparece contenido nuevo de acuse/respuesta donde no debería.

**Automatizable.** API (`correr.py A`, caso `A-11`) — convertido esta migración: la versión vieja de
este caso decía "no automatizado, no hay herramienta de orquestación"; el PATCH directo con
`MSCRMCallerID` es exactamente esa herramienta, y `PermitidasEnSolicitud` (lista blanca) admite
`sanic_estadoprocesamiento` de una persona. GUI opcional para la vista **Por clasificar** — no
bloqueante.

---

### A-12 · Revisado

**Qué prueba.** El octavo comando de fase 1: apaga `sanic_requiererevision` y deja el motivo (que
NO escribe el operador, lo escriben los flujos) en la Bitácora.

**Necesita.** Rol de Ejecutivo o Supervisor verificado; una Solicitud propia.

**Cómo.**
1. Precondición (código de servidor, sin impersonar — igual que `I-13` con `sanic_claimhasta`):
   `PATCH sanic_requiererevision = true, sanic_motivorevision = "<motivo>"` (en la vida real lo
   escriben `MPPP-VIG`/`MPPP-ENV`).
2. Impersonando Ejecutivo, `PATCH sanic_requiererevision = false` (sin texto: el botón no lo pide).

**Qué tiene que pasar.** `204`; `sanic_requiererevision` queda en **No**; `sanic_motivorevision`
queda **vacío** (se limpia para que la próxima vez no se lea el motivo viejo); la Bitácora tiene un
evento `Revision atendida` con el motivo **original** (el de antes de limpiarse) y **origen Plugin**.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py A` (caso `A-12`).

**Cómo se sabe que falló.** Si `sanic_motivorevision` no se limpia, la próxima vez que la marca se
prenda el operador va a leer el motivo VIEJO. Si la Bitácora no registra el motivo antes de
limpiarlo, se pierde el porqué de la revisión.

**Automatizable.** API (`correr.py A`, caso `A-12`) — convertido esta migración, mismo motivo que
A-11: `PermitidasEnSolicitud` admite `sanic_requiererevision` de una persona.

---

### A-13 · Multiidioma de los botones

> **Decisión del aprobador (2026-10-06): el usuario Supervisor de prueba se deja en inglés (1033).** El diagnóstico de
> esa fecha mostró que la sesión `supervisor` falla porque `usersettings` de User2 tiene `uilanguageid=1033` (los cuatro
> usuarios de prueba están en 1033; el español 3082 está aprovisionado y la app no tiene la culpa). Mientras siga así,
> la parte "supervisor en 3082" de A-13 queda en FALLA a sabiendas; no es un defecto de la app.

**Qué prueba.** Que cada etiqueta tenga su texto en 1033 y 3082 (convención del proyecto), y que
los mensajes del JavaScript (confirmación, resultado) también cambien de idioma.

**Necesita.** Un usuario con el idioma configurado en inglés; cualquiera de las 4 sesiones.

**Cómo.** Con un usuario en inglés, pasar `1033` a `verificarBotones(page, BOTONES_FILA, 1033)` en
vez de `3082` (`pruebas/gui/README.md` §6 punto 4).

**Esperado.**

| Español (3082) | Inglés (1033) |
|---|---|
| Digitada | Mark as entered |
| Aprobar | Approve |
| Rechazada en AS400 | Rejected in AS400 |
| Anular | Annul |
| Devolver | Send back |
| Atendida | Mark attended |
| Atendido | Handled |
| Descartar | Discard |
| Revisado | Reviewed |

(Las 8 etiquetas de Fila, con las dos versiones de idioma, ya están centralizadas en
`pruebas/gui/selectores.mjs`, `BOTONES_FILA`, copiadas de `playbooks/importacion/comandos.md`.)

**Cómo se verifica.** `MPPP_USUARIO=ejecutivo MPPP_IDIOMA_ESPERADO=1033 npx playwright test
a-13-multiidioma-botones` y `MPPP_USUARIO=supervisor MPPP_IDIOMA_ESPERADO=3082 npx playwright test
a-13-multiidioma-botones` (`pruebas/gui/casos/a-13-multiidioma-botones.spec.mjs`, escrito el
2026-09-28). `local/sesion-ejecutivo.json` está confirmada en inglés (1033) en vivo desde el
2026-09-27 (`selectores.mjs`); `local/sesion-supervisor.json` se prueba esperando español (3082, la
convención del proyecto) — si resultara estar también en inglés, el caso lo reporta como FALLA con
el texto real encontrado (hallazgo legítimo, no se fuerza un verde). `herramientas/pruebas/sets/
set_a.py::a13()` invoca las dos corridas por subproceso.

**Cómo se sabe que falló.** Si con el idioma en inglés aparece una etiqueta en español, o al revés.
**Los MENSAJES del JS SÍ tienen caso de arnés desde esta migración**: el segundo `test` del archivo
compara el texto real del diálogo de confirmación contra la plantilla `TEXTOS[idioma].confirmar` de
`recursos/js/comandos.js` (copiada a `TEXTOS_JS`, `selectores.mjs`). **Hallazgo, leyendo el código
fuente (2026-09-28):** el nombre de la acción que arma ese mensaje (`ejecutar("Digitada", ...)`,
`comandos.js` línea 568) es un literal en **español, fijo**, sin pasar por `TEXTOS` — a diferencia
del resto de la plantilla, que sí traduce. En una sesión en inglés el diálogo dice literalmente
`Apply "Digitada" to 1 selected row(s)?`: el contenedor se traduce, el nombre de la acción adentro
no. El caso verifica el comportamiento REAL (con "Digitada" fijo en las dos plantillas), y deja esto
documentado (CONVENCIONES.md §7) para que el aprobador decida si es un gap de UX a corregir — no es
parte del alcance de esta migración arreglarlo.

**Automatizable.** GUI (`pruebas/gui/casos/a-13-multiidioma-botones.spec.mjs`, sesiones `ejecutivo`
+ `supervisor`) — escrito y conectado el 2026-09-28.

---

### A-14 · Atendida, sobre Fila (fase 2): apaga el cerrojo del bot sin cambiar de estado

**Qué prueba.** El noveno comando, agregado en fase 2 (`recursos/js/comandos.js`, función
`atendida`): apaga `sanic_requiereatencionmanual` — el cerrojo que impide que el bot vuelva a tomar
una fila que ya es de un ejecutivo (`03` §3) — **sin** tocar `sanic_estado`. `set_b.py` (B-19,
B-20) ya prueba que el bot prende la marca y que cualquier transición de estado la apaga sola; lo
que faltaba era el botón en sí.

**Necesita.** Rol de Ejecutivo verificado; una Fila propia en Validada.

**Cómo.**
1. Precondición (código de servidor): `PATCH sanic_requiereatencionmanual = true` (en la vida real
   la prende el bot, `resultado = atencionmanual`, ver `pruebas/09-rpa-fase2.md` B-19).
2. Impersonando Ejecutivo, `PATCH sanic_requiereatencionmanual = false` (sin tocar `sanic_estado`).

**Qué tiene que pasar.** `204`; `sanic_requiereatencionmanual` queda en **No**; `sanic_estado` sigue
**Validada** (el botón "Atendida" no es una transición de Fila, a diferencia de los otros cinco).

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py A` (caso `A-14`).

**Cómo se sabe que falló.** Si el paso 2 da `HTTP 400` con el texto "la(s) columna(s)
sanic_requiereatencionmanual no está(n) permitida(s) para este usuario", **no es un problema de
datos de prueba**: es la discrepancia documentada arriba entre `diseno/04-matriz-privilegios.md`
(dice que la columna "sí entra a la lista blanca" desde fase 2) y
`Steps/ReglasDeSteps.cs.PermitidasEnFila` (no la incluye). Un defecto confirmado así se reporta
`FALLA — bloqueado por` el ID que se le asigne en `diseno/PENDIENTES.md` (CONVENCIONES §3: "un
defecto conocido no es N-A").

> **CONFIRMADO en la corrida del 2026-09-27**: dio exactamente ese `HTTP 400`. El botón "Atendida"
> no funciona hoy contra Dev. Falta abrirlo en `diseno/PENDIENTES.md` con un ID.

**Automatizable.** API (`correr.py A`, caso `A-14`), sobre datos propios. Caso nuevo de esta
migración: no existía en el archivo antes del 2026-09-27.

---

### A-15 · La lección del botón fantasma: distinguir REGLAS de METADATA COMPILADA

**Fusionado acá el 2026-09-28** (`CONVENCIONES.md` §4, "La regla del nombre"): este caso era `H-06`
del set `H` (`13-construido-hoy.md`, prefijo `H-`), un archivo aparte nombrado por la fecha en que
se escribió, no por lo que prueba. `H-06` es sobre un comando de la barra — el tema de ESTE
archivo—, así que quedó acá con el mismo cuerpo, solo cambió el ID. `13-construido-hoy.md`
desapareció; la tabla de equivalencias completa vive en `pruebas/README.md`.

**Qué prueba.** El hallazgo real del 2026-09-27: el botón "Descargar para digitar" existía en el
servidor —verificado por API por TRES caminos distintos (`RetrieveEntityRibbon`, los `ribbondiff` con
sus LocLabels en 1033/3082, y el verificador del proyecto)— y NO se dibujaba en el navegador: la causa
era la metadata de ribbon **compilada**, no una regla de habilitación. Este caso documenta el
PROCEDIMIENTO DE DIAGNÓSTICO para la próxima vez que un botón nuevo "no aparezca", sea cual sea, y que
quien lo encuentre sepa en minutos —no en dos días, como esta vez— si el problema es de REGLAS o de
METADATA COMPILADA: son diagnósticos opuestos con el mismo síntoma superficial ("no lo veo").

**Necesita.** Acceso de **customización de la solución** en `make.powerapps.com` sobre
`sanic_mppp_sol_mantenimientoppp` — privilegio de PLATAFORMA (System Administrator/System Customizer
de Dataverse), **no** el rol de negocio `SR - MPPP - Administrador tecnico` que prueban
`local/sesion-admin_tecnico.json` y `estado_sesiones.py`: ese rol está acotado a las tablas Parámetro
y Reglas dentro de la app (`04-matriz-privilegios.md`) y no da acceso al maker portal para regenerar
metadata. Además: `local/sesion-ejecutivo.json` (rol `SR - MPPP - Ejecutivo`), confirmada viva y con
ese rol por `python3 herramientas/pruebas/estado_sesiones.py` ANTES de correr nada; el parámetro de
URL `&ribbondebug=true` agregado a la URL de la app model-driven.

**Cómo.**
1. Abrir la vista de Filas en la app con `&ribbondebug=true` en la URL, y usar el **Command
   Checker** sobre el comando en cuestión.
2. Leer la lista de comandos que devuelve el Command Checker:
   - Si el botón aparece marcado **`(hidden)`**: es un problema de **REGLAS** (`EnableRule`/
     `DisplayRule`) — revisar contra la selección actual, la metadata está bien.
   - Si el botón está **AUSENTE de la lista entera**: es un problema de **METADATA COMPILADA** del
     ribbon — ningún ajuste de reglas lo va a arreglar.
3. Para el segundo caso: en el maker portal, sobre la tabla/app correspondiente, usar **"Regenerate
   ribbon metadata"** (o volver a publicar la personalización completa) y repetir el paso 1.

**Qué tiene que pasar.** Después de "Regenerate ribbon metadata", un botón que estaba AUSENTE de la
lista del Command Checker pasa a aparecer (visible o `(hidden)`, según la selección actual).

**Cómo se verifica.** La lista del Command Checker antes y después. Para la confirmación en el DOM
real (con sesión y sin depender del maker portal): `cd pruebas/gui && MPPP_USUARIO=ejecutivo npx
playwright test ribbon-fila` (ver `pruebas/gui/ayudantes/ribbon.mjs`, que codifica esta misma
distinción: **AUSENTE** → "Sospechar metadata de ribbon (regenerar desde Command Checker) antes que el
EnableRule"; **presente pero DESHABILITADO** → "revisar el EnableRule contra la selección actual, la
metadata está bien"). Este set nunca duplica ese arnés (`CONVENCIONES` §9).

**Cómo se sabe que falló (es decir, que se erró el diagnóstico).** Se pasaron minutos u horas
ajustando `EnableRule`/`DisplayRule` sobre un botón que en realidad estaba AUSENTE de la lista del
Command Checker (metadata compilada) — o al revés, se regeneró metadata sobre un botón que SÍ estaba
en la lista pero `(hidden)` por una regla. Confundirlos es exactamente lo que costó dos días este
mismo día.

**Automatizable.** La DETECCIÓN (que un botón esté AUSENTE del DOM real, HomepageGrid y SubGrid) está
conectada desde el 2026-09-28: `herramientas/pruebas/sets/set_a.py::a15()` reusa la MISMA corrida de
Playwright que `a01()` (mismo archivo `ribbon-fila.spec.mjs`, cacheada en `ctx.extra` para no
duplicar Dev) y reporta OK/FALLA real. La ACCIÓN de "Regenerate ribbon metadata" en caso de FALLA
sigue siendo manual, en el maker portal (privilegio de PLATAFORMA) — el caso documenta ese paso
siguiente en el mensaje de falla, ya no como motivo de `N-A` del caso completo.

> **Nota para quien mantenga el documento de instalación en otro entorno** (fuera del alcance de este
> archivo de pruebas, pero registrado acá porque de acá sale la lección): después de importar la
> solución y publicar (`PublishAllXml`), hay que **regenerar la metadata del ribbon** desde el Command
> Checker, o los botones custom no aparecen — aunque la publicación haya terminado sin ningún error.

---

### Resultados

Última corrida real: `python3 herramientas/pruebas/correr.py A`, 2026-09-28, después de reparar la
capa 1 (el playbook) de D-69. **13 OK · 2 FALLA · 0 N-A.** Formato por caso: OK / FALLA (con el
motivo o el ID del defecto) / N-A (con el motivo) — CONVENCIONES §3. Desde el 2026-09-28 los seis
casos que antes eran `N-A` por ser GUI (A-01, A-02, A-04, A-05, A-09, A-13) tienen su caso de arnés
en `pruebas/gui/` y `set_a.py` los invoca por subproceso, igual que el set R invoca `dotnet test`:
ya no queda ningún "correlo aparte con sesión real". **A-15** (fusión del ex `H-06`,
`13-construido-hoy.md`) también reporta OK/FALLA real desde esta corrida — la DETECCIÓN reusa el
mismo arnés de Playwright que A-01; solo la ACCIÓN de reparación ("Regenerate ribbon metadata") en
caso de falla sigue siendo manual, y ya no cuenta como `N-A` del caso completo.

Los 2 `FALLA` quedan en rojo con su causa, sin maquillar:
- **A-02** — bloqueado por **D-69**: el playbook ya está corregido (commit `782f0ed`) y
  `RetrieveEntityRibbon` confirma que "Flujo" y "Enviar vínculo" quedaron marcados como ocultos en
  el servidor, pero falta el paso MANUAL de plataforma ("Regenerate ribbon metadata", Command
  Checker con `&ribbondebug=true`) que ninguna herramienta de este repo puede hacer por API —
  mismo mecanismo REGLA-vs-METADATA COMPILADA que costó dos días con el botón de DN-02 (ver A-15),
  con el síntoma invertido.
- **A-13** — pasa con la sesión `ejecutivo`/1033 (confirmado tres veces); con `supervisor`/3082
  **nunca se confirmó limpio**. No está determinado si es un defecto del sistema o de la prueba.

| Caso | Resultado | Notas |
|---|---|---|
| A-01 los botones aparecen | OK | GUI, `ribbon-fila.spec.mjs` invocado por `set_a.py::a01()` (HomepageGrid y SubGrid) |
| A-02 genéricos ocultos | **FALLA** | bloqueado por **D-69**: playbook corregido, pero falta el paso manual "Regenerate ribbon metadata" — ver el párrafo arriba |
| A-03 Digitada masiva | OK | |
| A-04 cancelar | OK | GUI, `a-04-cancelar-no-cambia-nada.spec.mjs` invocado por `set_a.py::a04()` |
| A-05 sin selección | OK | GUI, `a-05-sin-seleccion.spec.mjs` invocado por `set_a.py::a05()` |
| A-06 Aprobar | OK | incluye el cierre automático a Procesada |
| **A-07 el plugin manda** | OK | rechazos por rol + lote mixto, sobre datos propios |
| A-08 diálogo de motivo (Rechazada en AS400) | OK | corregido: `sanic_motivo` no lo exige la plataforma, solo el formulario |
| A-09 cancelar diálogo | OK | GUI, `a-09-cancelar-dialogo-motivo.spec.mjs` invocado por `set_a.py::a09()` |
| A-10 Anular y Devolver | OK | |
| A-11 Atendido y Descartar | OK | Estuvo en FALLA hasta el 2026-09-28 por **D-59** (`Avisos.cs`: `GetAttributeValue<EntityReference>` sobre `systemuserroles`, donde `systemuserid` viaja como `Guid`). Reparado y desplegado en `1.0.16.0`; el caso pasó a verde sin tocarse |
| A-12 Revisado | OK | |
| A-13 multiidioma | **FALLA** | limpio con `ejecutivo`/1033 (x3); `supervisor`/3082 nunca confirmó limpio — no determinado si es defecto de sistema o de prueba |
| A-14 Atendida (fase 2) | OK | Estuvo en FALLA hasta el 2026-09-28 por **D-63**: faltaba `sanic_requiereatencionmanual` en la lista blanca de Fila, así que el botón daba HTTP 400 y dejaba la fila trabada (el bot no la podía tomar y la persona no la podía liberar). Reparado y desplegado en `1.0.17.0` |
| A-15 diagnóstico REGLAS vs. METADATA COMPILADA | OK | ex `H-06`; GUI, misma corrida de Playwright que A-01 (`set_a.py::a15()`) — la acción de reparación ante una falla sigue siendo manual, en el maker portal |

---
## 06 — Seguridad, sitemap, vistas y formularios

Cubre lo que el usuario ve y lo que **no** tiene que poder ver. Los casos de seguridad son los más
importantes de todo el plan: acá hay números de identificación y de cuenta de personas reales.

**Antes**: `00-preparacion.md`. Desde el 2026-09-27 esto ya no es una dependencia entre sets
(CONVENCIONES §8): el set `G` (`herramientas/pruebas/sets/set_g.py`) prepara y limpia TODO lo que
necesita por su cuenta, incluida su propia tanda de datos.

**Los cuatro roles de negocio** — verificados uno por uno contra `systemuserroles_association` el
2026-09-27, nunca supuestos:

| Papel | Usuario | Rol | Para qué |
|---|---|---|---|
| `ejecutivo` | `# User 1` | `SR - MPPP - Ejecutivo` | digita |
| `supervisor` | `# User 2` | `SR - MPPP - Supervisor` | aprueba |
| `admin_planes` | `# User 3` | `SR - MPPP - Administrador de planes` | Clientes, Planes, Autorizados, Autorizaciones (Planes y Clientes) |
| `admin_tecnico` | `# User 4` | `SR - MPPP - Administrador tecnico` | Parámetros y Reglas |

**Hay DOS administradores y hacen cosas distintas.** `admin_planes` toca Clientes, Planes,
Autorizados, Autorizaciones - Planes y Autorizaciones - Clientes; `admin_tecnico` toca Parámetros y
Reglas. Un caso de este archivo nunca dice "administrador" a secas.

**Ni uno ni el otro alcanza para tareas de plataforma.** Entrar al maker portal, tocar roles de
seguridad de Dataverse o regenerar la metadata de un ribbon exige **System Administrator o
Customizer de Dataverse**, que se llama parecido a los roles de arriba y es otra cosa. Ningún caso
de este archivo pide eso: donde hace falta, lo hace el service principal `# PowerPlatform-Dev`
(tiene System Administrator) desde las herramientas, nunca desde una sesión de las cuatro de arriba.

---

### Por qué este set es el más delicado del plan

**En seguridad, una prueba que pasa porque el usuario no ve algo es indistinguible de una que pasa
porque la sesión está muerta. Las dos dan verde, y una está mintiendo.**

Por eso `set_g.py.preparar()` llama a la guarda de sesiones ANTES de tocar cualquier otra cosa:

```bash
python3 herramientas/pruebas/estado_sesiones.py
```

Si una sesión está vencida o su usuario no tiene el rol esperado, **no se corre nada** — cero casos
corridos es un resultado honesto (CONVENCIONES §3). El motivo completo está en el comentario de
`ROL_ESPERADO` en `herramientas/pruebas/comportamiento_roles.py`: el 2026-09-24 le cambiaron el rol
a `User1` y dos casos dieron rojo con un `403` que parecía un defecto del producto.

**Esto vale aunque la mayoría de los casos de abajo corran por API, no por navegador.** El set entero
—MD incluido— sigue citando las cuatro sesiones porque una parte (G-02, G-07, G-11 y el ícono/recorte
de G-01) sigue siendo GUI, y la guarda las cubre a las cuatro de una vez, en `preparar()`, antes del
primer caso.

Además, `preparar()` construye una sola vez `comportamiento_roles.Entorno()`, que revalida los cuatro
roles contra `systemuserroles_association` (no contra la sesión de navegador: por otra vía, con el
service principal) y confirma que quien corre esto puede impersonar (`prvActOnBehalfOfAnotherUser`).
Todos los casos que impersonan (G-00b, G-04, G-05, G-13) reusan ese mismo `Entorno`.

---

### De GUI a API: qué cambió y por qué

Mucho de este set se puede verificar por API, sin abrir un navegador. Este archivo evaluó caso por
caso, y lo que se pudo convertir, se convirtió:

| Caso | Antes | Ahora |
|---|---|---|
| G-00 | API (`verificar_accesos.py`), pero solo con `User1..User3` | API, con **`User1..User4`** — el MD viejo nunca pedía `User4` (admin_tecnico), y la matriz sí define sus privilegios |
| G-00b | API (`comportamiento_roles.py`) | Igual, sin `--con-escritura` (ver más abajo) |
| G-01 | GUI ("nunca se pudo verificar contra ningún sitemap del entorno") | **API**: se lee el propio playbook del sitemap y se corre `app.py --solo-verificar`, que compara el sitemap YA DESPLEGADO contra lo que el playbook pide, con el `savedqueryid` de cada vista resuelto EN VIVO |
| G-03 | GUI | **API**: `savedqueries` — existencia de las 20, y el `fetchxml` de las ocho más importantes |
| G-04 | GUI (reasignar el propietario del Cliente a mano) | **API**: el Cliente compartido ya es propiedad de la cuenta de servicio, no de ningún `User1..User4` — alcanza con correr el `fetchxml` real impersonando al ejecutivo |
| G-05 | GUI | **API**: se lee `sanic_numeroidentificacion`/`sanic_numerocuenta` impersonando a los 4 roles. **El caso más importante del set** |
| G-08 | GUI | **API**: `systemforms` — existencia de los 10, y el `formxml` de lo que este archivo marca "mirá especialmente" |
| G-09 | GUI (con una nota de que ya se prueba indirecto) | **API**: se suma correr el `fetchxml` real de "Autorizaciones sin evidencia" impersonando a `admin_planes` |
| G-10 | GUI | **API (parcial)**: se crea una tanda real y se lee `appnotification`. La navegación al tocar la notificación sigue siendo GUI |
| G-02, G-07, G-11 | GUI | Siguen GUI/N-A: piden ver algo RENDERIZADO (un SVG en dos temas, un menú recortado, una etiqueta traducida) y hoy no tienen arnés en `pruebas/gui/` |
| G-06 | API (proxy) | Se **fusiona** en G-00/G-00b: son las mismas celdas, correrlo aparte probaría la herramienta dos veces, no el sistema dos veces (CONVENCIONES §2) |
| G-12 | API | Igual |
| **G-13** | — | **Nuevo**: la frontera con el set B (fase 2, RPA) |

---

### Fase 1 + fase 2: la frontera con el set B (RPA)

Fase 2 agregó el rol `SR - MPPP - RPA` y le sacó el perfil de columna que tuvo brevemente en una
versión anterior de la matriz (`04-matriz-privilegios.md` §4, decisión del aprobador 2026-09-25).

El rol `SR - MPPP - RPA` es una **identidad de aplicación**: nunca abre esta app model-driven, nunca
tiene una sesión de navegador. Por eso **ningún caso de sitemap/vistas/formularios de este archivo lo
menciona** — no hay nada que ver ahí.

Lo que sí es de este set —porque es exactamente el tema de `04-matriz-privilegios.md` §3/§4, el mismo
diseño que G-00 y G-05 verifican para los cuatro roles humanos— es que el rol del RPA **no traiga
privilegios de negocio** y **no esté en el perfil de columna** (D-9, D-44, aprobador 2026-09-25). Eso
es **G-13**, nuevo en esta migración.

**El set B (`09-rpa-fase2.md`, casos B-01/B-02/B-03) ya prueba la PUERTA de las dos Custom API**: quién
pasa y quién no, en tiempo de llamada (identidad de aplicación + rol, las dos condiciones, D-9/D-44).
**G-13 no repite eso.** G-13 audita la FORMA del rol en sí —qué privilegios trae, qué perfiles lo
alcanzan— con las mismas herramientas de lectura que G-00/G-05 usan para los roles humanos. La
frontera es: **B ejercita el comportamiento; G audita la configuración declarada.** No hay
superposición.

---

### G-00 · Los privilegios efectivos, los 4 roles

**Qué prueba.** Que cada usuario de prueba tenga **exactamente** las celdas de la matriz de `04`,
ni una menos ni una de más — los cuatro, no tres.

**Por qué va primero.** En Dataverse los privilegios se **suman**: los de los roles directos más los
de **cada equipo** al que el usuario pertenece. Un usuario reciclado de otro proyecto llega con los
roles de ese proyecto. Entonces, si en la app el usuario puede hacer algo que no debería, mirando la
pantalla **no se sabe quién se lo permitió**. Esto lo resuelve antes de empezar, y con nombre y
apellido del rol culpable.

**Corregido el 2026-09-27**: el MD y el `corredor.py` viejos invocaban `verificar_accesos.py` solo con
`User1 User2 User3` — un hueco real, no una decisión: la matriz sí define privilegios para
`admin_tecnico` (`04` §2, columna "Administrador tecnico"), y nunca se había verificado por esta vía.

**Necesita.** Credenciales de lectura en Dataverse; los cuatro usuarios de prueba ya creados
(User1..User4).

**Cómo.**

```bash
python3 herramientas/pruebas/verificar_accesos.py User1 User2 User3 User4
```

**Esperado.** `Coincide con la matriz, celda por celda.` para los cuatro. Sale con 0.

**Qué hacer con cada tipo de hallazgo.**

| Tipo | Qué significa | Qué hacer |
|---|---|---|
| `FALTA` | La matriz le da el privilegio y no lo tiene | El rol está mal construido: revisá su playbook y reimportalo |
| `ALCANCE` | Lo tiene, pero con otra profundidad | Ídem. Un `U` donde va `O` hace que el usuario solo vea sus propios registros |
| `SOBRA` | Lo tiene y la matriz no se lo da. **El detalle nombra el rol que se lo trajo** | Si el rol es de otro proyecto, sacalo del equipo antes de probar, o la prueba no vale |
| `COLUMNAS` | No alcanza el perfil de seguridad de columna | Ver G-05: sin eso, el ejecutivo no ve lo que tiene que digitar |
| `LICENCIA` | No tiene una licencia que le permita abrir apps custom | Se ve como un `502` en pantalla, no como un error de permisos (hallazgo del 2026-09-24) |

**Correr esto también después de cada cambio de roles.** Es lo único que detecta que un
`ImportSolution` pisó un privilegio.

**Cómo se sabe que falló.** Un `FALTA`/`ALCANCE`/`SOBRA`/`COLUMNAS`/`LICENCIA` en la salida, con el
alias afectado.

**Automatizable.** API (`verificar_accesos.py`; `set_g.py:g00` corre esto mismo para los 4 alias).

---

### G-00b · El comportamiento de cada rol, contra el entorno

**Qué prueba.** La otra mitad de la pregunta. `verificar_accesos.py` responde *"¿qué privilegios
TIENE cada uno?"*; esta responde *"¿qué puede HACER?"*. Hacen falta las dos: un privilegio correcto
con un plugin que falla da un usuario bloqueado, y un plugin correcto con un privilegio de más da un
agujero.

**Por qué no alcanza con las pruebas de C#.** Usan un doble de `IOrganizationService` que **no
modela privilegios**: devuelve lo que se le pide, venga de quien venga. Por eso ninguna vio el
defecto del 2026-09-23 — los plugins leían la tabla Parametro con la identidad de quien llamaba, y
ningún rol de negocio tiene lectura ahí, así que **ninguna persona podía transicionar una fila**
(`diseno/PENDIENTES.md`, D-50). Eso solo se ve contra la plataforma y con un usuario de verdad.

**Necesita.** Credenciales de servicio con permiso para suplantar (`MSCRMCallerID`) a cada usuario
de prueba; los usuarios User1..User4 ya creados; al menos una Fila en Validada (la tanda semilla que
`set_g.py.preparar()` deja lista alcanza).

**Cómo.**

```bash
python3 herramientas/pruebas/comportamiento_roles.py
```

Suplanta a cada usuario con la cabecera `MSCRMCallerID`, así que el plugin ve exactamente el mismo
contexto que si la persona hubiera entrado a la app. Sin `--con-escritura` solo corre los casos de
rechazo, donde el servidor frena antes de tocar el registro: **no deja rastro**, se puede correr
cuando sea.

**Corregido el 2026-09-27 — este set NO usa `--con-escritura` dentro de `correr.py G`.** La
herramienta misma avisa que esa bandera "actúa sobre CUALQUIER Fila del entorno en
Validada/Digitada, no solo las propias", y hay otros dos agentes trabajando en paralelo (uno de ellos
en `pruebas/gui/`, sobre sesiones reales). Sin una forma de coordinar ese uso en el momento de esta
migración, `set_g.py` corre la mitad segura (los 9 casos de rechazo, que no escriben nada de verdad).
El recorrido completo (digitar → devolver → digitar → aprobar, 7 casos más) sigue disponible a mano,
coordinado, con la bandera de arriba — no se perdió, se dejó fuera del automatismo por prudencia.

**Esperado.** `0 fallas` en los casos de rechazo. Un caso `— sin probar` **no es un caso aprobado**:
es un caso que no se pudo correr por falta de datos. Se cuenta aparte, nunca como verde.

**Qué NO reemplaza.** Todo lo que sigue de G-01 en adelante: lo que la persona VE. La barra de
comandos, los íconos, el recorte del sitemap, las notificaciones y las columnas enmascaradas en
pantalla no se ven desde acá. Esto prueba las reglas; los casos de abajo prueban la experiencia.

**G-06 ("Lo que cada rol NO puede") queda fusionado acá y en G-00.** Es la misma matriz de
privilegios (G-00) y el mismo comportamiento de rechazo (G-00b: el administrador de planes no lee
Filas ni Solicitudes, una transición ilegítima se rechaza, nadie borra). Correrlo como un caso aparte
repetiría exactamente estas mismas llamadas — probaría la herramienta dos veces, no el sistema dos
veces (CONVENCIONES §2). Lo que G-06 agregaba de distinto —confirmar la EXPERIENCIA en pantalla, no
solo el privilegio subyacente— sigue siendo GUI y lo cubre G-07.

**Automatizable.** API (`comportamiento_roles.py`, sin `--con-escritura`; `set_g.py:g00b`).

---

### G-01 · El sitemap: las 15 entradas, cada una a su vista exacta

**Qué prueba.** El sitemap de 12.7 (`playbooks/app/mantenimientoppp.md`) y que cada entrada abra
**su** vista, no la vista por defecto de la tabla.

**Corregido el 2026-09-27 — esto pasa de GUI a API.** El MD viejo decía "este patrón NUNCA se pudo
verificar contra ningún sitemap del entorno". Se puede: `app.py --solo-verificar` ya compara, byte a
byte, el sitemap XML desplegado contra el que generaría el playbook con el `savedqueryid` de cada
vista **resuelto en vivo, ahora mismo**. Si una entrada apuntara a la vista equivocada (o a la vista
por defecto en vez de la suya), el estado no sería `ya_existia`, sería `difiere` — y eso es
precisamente lo que hace de esta corrida una prueba, no una lectura.

**Necesita.** Credenciales de lectura en Dataverse (las que ya usan las herramientas del proyecto).
Ningún rol de la app.

**Cómo.**

```bash
python3 herramientas/construir/app.py playbooks/app/mantenimientoppp.md --solo-verificar
```

**Esperado.** `"estado": "ya_existia"`, con `"4 areas y 15 entradas, 12 tablas, 4 roles ...
ValidateApp sin errores"` en el detalle (verificado en vivo el 2026-09-27).

**Las 15 entradas, nombradas una por una** (`05-app-model-driven.md` §2; las 4 áreas: Trabajo,
Consulta, Catálogos, Configuración):

| Entrada | Tabla | Vista que abre |
|---|---|---|
| Por digitar | Fila | **Mis clientes — por digitar** (no "Todos") |
| Devueltas | Fila | **Mis clientes — devueltas** |
| Por aprobar | Fila | **Por aprobar** |
| Para atención manual | Fila | vista de Filas con `sanic_requiereatencionmanual` = sí |
| Por clasificar | Solicitud | **Por clasificar** |
| Para revisar | Solicitud | **Para revisar** |
| Solicitudes | Solicitud | **Solicitudes** |
| Filas | Fila | **Filas** |
| Clientes | Cliente | por defecto de la tabla (**Clientes activos**) |
| Planes | Plan | por defecto de la tabla (**Planes activos**) |
| Correos Autorizados | Autorizado | por defecto de la tabla (**Autorizados activos**) |
| Autorizaciones - Planes | AutorizacionPlan | por defecto de la tabla (**Autorizaciones activas**) |
| Autorizaciones - Clientes | AutorizacionCliente | por defecto de la tabla (**Autorizaciones por cliente activas**) |
| Parámetros | Parametro | por defecto de la tabla (**Parametros activos**) |
| Reglas | Regla | por defecto de la tabla (**Reglas activas**) |

Los títulos "Autorizados" y "Autorizaciones" se renombraron a **"Correos Autorizados"** y
**"Autorizaciones - Planes"** (para distinguirla de la nueva "Autorizaciones - Clientes"), y se
agregaron **"Para atención manual"** (fase 2, enganchada al sitemap recién el 2026-09-27, hallazgo del
aprobador — antes existía la vista pero no la entrada) y **"Autorizaciones - Clientes"** (DN-01).

**FALLA si** el estado no es `ya_existia` (por ejemplo, si alguna entrada abre la vista por defecto de
Fila o de Solicitud en vez de la suya: las cuatro entradas sobre Fila mostrarían lo mismo y la
navegación del diseño se cae), o si el playbook mismo no declara las 15 con la tabla/vista de arriba.

**Lo que sigue siendo GUI.** Que el sitemap se vea bien —22 íconos, tema claro/oscuro— es G-02. Que
cada rol vea solo SUS entradas es G-07 (consecuencia de la matriz de G-00, ver ahí).

**Automatizable.** API (`app.py --solo-verificar`; `set_g.py:g01` además nombra las 15 entradas
contra lo que declara el playbook, antes de correrlo).

---

### G-02 · Los íconos del sitemap

**Qué prueba.** Los 22 SVG de 12.1.

**Necesita.** Sesión con acceso al sitemap (cualquiera de las 4); poder cambiar el tema de la app
entre claro y oscuro.

**Cómo.** Con la sesión guardada, mirá el menú y la app.

```bash
cd pruebas/gui && MPPP_USUARIO=ejecutivo npx playwright test --headed
```

(o cualquiera de las 4 sesiones — el arnés no tiene todavía un caso que capture el sitemap; esto abre
la app con la sesión real para mirarlo a ojo, ver `pruebas/gui/README.md` §2).

**Esperado.** Cada entrada tiene **su** ícono (no el genérico), y la app tiene el suyo en la barra.
Los íconos se ven bien en **tema claro y oscuro** (usan `currentColor`). Siguen siendo 22: las dos
entradas nuevas de sitemap (`Para atención manual`, `Autorizaciones - Clientes`) **reúsan** íconos
existentes (el de "Para revisar" y el de "Autorizaciones - Planes"), no se sumó ninguno.

**Cómo se sabe que falló.** Un ícono genérico (el de "página en blanco" de la plataforma) en vez del
propio, o un SVG que se ve invisible en un tema porque usa un color fijo en vez de `currentColor`.

**Automatizable.** GUI — pide ver algo RENDERIZADO (un SVG en dos temas) y hoy no tiene arnés en
`pruebas/gui/`. No tiene función en `set_g.py`: `correr.py G` no le inventa un resultado.

---

### G-03 · Las 20 vistas existen y filtran

**Qué prueba.** 12.2.

**Corregido el 2026-09-27 — pasa de GUI a API.** Se lee `savedqueries` en vivo: existencia de las 20
por nombre exacto, y el `fetchxml` de las más importantes.

**Corregido: eran 18, hoy son 20** (`diseno/05-app-model-driven.md` §3: se agregaron "Correos
autorizados sobre este cliente", "Clientes que puede modificar" y "Autorizaciones por cliente
activas", las tres sobre `AutorizacionCliente`, DN-01/2026-09-27).

**Corregido — los nombres reales no llevan tilde.** El diseño escribe "Parámetros activos"; la vista
construida en Dev se llama **"Parametros activos"**, sin acento (verificado en vivo el 2026-09-27:
las vistas propias del playbook nacen en ASCII a propósito; las vistas nativas de la plataforma, como
"Autorización Advanced Find View", sí llevan tilde porque salen del *display name* de la tabla). Es
una diferencia de ortografía, no de comportamiento — se deja anotada para que nadie la lea como un
`FALTA` al comparar contra el diseño.

**Necesita.** Credenciales de lectura en Dataverse; datos previos (la tanda semilla de `set_g.py`
alcanza para las vistas de Fila).

**Cómo.** Leer `savedqueries?$filter=name eq '<nombre>' and returnedtypecode eq '<tabla>'` para cada
una de las 20; para las más importantes, comparar su `fetchxml` contra lo esperado.

**Las 20, nombradas una por una** (con su fetchxml **verificado en vivo el 2026-09-27** para las
ocho más fáciles de leer mal):

| Vista | Tabla | Filtro real (verificado en vivo) |
|---|---|---|
| Mis clientes — por digitar | Fila | `sanic_estado = 159460003` (Validada) **y** `Plan → Cliente → ownerid = usuario actual` |
| Todos — por digitar | Fila | `sanic_estado = 159460003`, sin el filtro de propietario |
| Por aprobar | Fila | `sanic_estado = 159460004` (Digitada) |
| Mis clientes — devueltas | Fila | `sanic_estado = 159460003` **y** `sanic_notainterna` no vacío **y** propietario = usuario actual. **Corregido: el MD viejo decía "con mensaje de devolución" (`sanic_mensaje`) — el filtro real usa `sanic_notainterna`** (la nota interna del supervisor al devolver, `04` §1), no el mensaje al cliente |
| Terminadas | Fila | `sanic_estado in (…, 159460005 Aprobada, …, 159460007 Anulada)`; NO incluye 159460003 ni 159460004 |
| Por clasificar | Solicitud | `sanic_estadoprocesamiento in (No reconocida, No es correo nuevo)` |
| Para revisar | Solicitud | `sanic_requiererevision = true` |
| Autorizaciones sin evidencia | AutorizacionPlan | `sanic_documentofirmado` es nulo |
| Solicitudes | Solicitud | todas |
| Clientes activos | Cliente | `statecode = 0` |
| Planes activos | Plan | `statecode = 0` |
| Autorizados activos | Autorizado | `statecode = 0` |
| Autorizaciones activas | AutorizacionPlan | `statecode = 0` |
| Parametros activos | Parametro | `statecode = 0` |
| Reglas activas | Regla | `statecode = 0` |
| Correos autorizados sobre este plan | AutorizacionPlan | subgrilla del formulario de Plan |
| Planes que puede modificar | AutorizacionPlan | subgrilla del formulario de Autorizado |
| Correos autorizados sobre este cliente | AutorizacionCliente | subgrilla del formulario de Cliente |
| Clientes que puede modificar | AutorizacionCliente | subgrilla del formulario de Autorizado |
| Autorizaciones por cliente activas | AutorizacionCliente | vista por defecto de la tabla |

**FALLA si** falta alguna de las 20, o si el `fetchxml` de las ocho de la tabla de arriba no trae la
condición esperada.

**Automatizable.** API (`savedqueries`; `set_g.py:g03`). El comportamiento con datos reales y usuario
real de "Mis clientes" lo prueba G-04; el de "Autorizaciones sin evidencia", G-09.

---

### G-04 · La cartera del ejecutivo (RF-19, D-13)

**Qué prueba.** Que **Mis clientes — por digitar** filtre por el propietario del Cliente, y que
**Todos — por digitar** no.

**Corregido el 2026-09-27 — pasa de GUI a API, sin tocar el fixture compartido.** El MD viejo pedía
"poné como propietario del Cliente `PR - Pruebas MPPP SA` a otro usuario": una escritura sobre un
dato que usan **los otros dos agentes en paralelo**. Verificado en vivo: el Cliente compartido
`PR - Pruebas MPPP SA` **ya es propiedad de la cuenta de servicio** (`# PowerPlatform-Dev`), no de
ningún `User1..User4` — nadie lo reasignó nunca. Así que cualquier Fila que este set procese ahí
**ya** cumple la condición que el caso quiere probar ("el Cliente no es del ejecutivo"), sin escribir
nada de más.

**Necesita.** La tanda semilla que `set_g.py.preparar()` deja lista (tres Filas Validada bajo el
Cliente compartido); permiso para impersonar al ejecutivo.

**Cómo.** Ejecutar el `fetchxml` real de "Mis clientes — por digitar" y de "Todos — por digitar"
(G-03), impersonando al ejecutivo, y ver en cuál aparecen las filas de la tanda semilla.

**Esperado.** Las filas de la tanda semilla **no aparecen** en *Mis clientes — por digitar* (el
Cliente no es del ejecutivo), pero **sí** en *Todos — por digitar* (esa vista no filtra por
propietario).

> Es comodidad, no seguridad: el ejecutivo **puede** ver todo si cambia de vista. Lo que prueba es
> que la vista por defecto le muestre su cartera.

**Cómo se sabe que falló.** Las filas de la tanda semilla aparecen en "Mis clientes" (el filtro de
propietario no está andando), o no aparecen en "Todos" (una regresión distinta, más grave: nadie
vería nada para digitar).

> ### Corrección — el caso comparaba por una columna que ninguna de las dos vistas trae
>
> Las dos primeras corridas de esta migración dieron FALLA acá: "las filas de la tanda semilla no
> aparecen en 'Todos — por digitar'". Antes de escribirlo como un defecto del sistema, se reprodujo
> a mano, pidiendo la fila devuelta por la vista **completa**, con todas sus claves — y las tres
> filas SÍ estaban. La vista funciona bien. El caso comparaba por `sanic_nombre` (el número calculado
> `<solicitud>-F<NN>`), y **ninguna de las dos vistas de Fila selecciona esa columna**: no es una de
> las columnas de trabajo del diseño (`05-app-model-driven.md` §3 — la columna "Nombre" de esa tabla
> es `sanic_nombrebeneficiario`, el beneficiario, no el número calculado de la Fila). El único
> `sanic_nombre` que sí viaja en la respuesta es el del **Cliente**, bajo la clave anidada
> `cliente.sanic_nombre` (viene del `<link-entity>` que arma la vista para mostrar el nombre del
> cliente en pantalla) — por eso la comparación por `sanic_nombre` daba vacía siempre, sin que la
> vista hubiera hecho nada mal. Corregido: se compara por `sanic_mppp_tbl_filaid` (el GUID), que las
> dos vistas SÍ traen siempre, sea cual sea el diseño de columnas de negocio del día. Es la misma
> lección de G-08: antes de escribir "esto es un defecto", hay que confirmar contra qué se está
> comparando de verdad.

**Automatizable.** API (`set_g.py:g04`, impersonación con `MSCRMCallerID`, sin navegador).

---

### G-05 · Perfil de seguridad de columna

**Qué prueba.** 12 / P-06: identificación y cuenta son columnas protegidas. **El caso más importante
del set**: es un dato bancario de una persona real.

**Corregido el 2026-09-27 — pasa de GUI a API, y se reescribe con más cuidado.** `04-matriz-
privilegios.md` §5 avisa: *"ojo con este: es la prueba más fácil de leer mal"*. Verificado en vivo:

- `ejecutivo` y `supervisor` leen `sanic_numeroidentificacion`/`sanic_numerocuenta` con **HTTP 200** y
  el valor real.
- `admin_planes` y `admin_tecnico` reciben **HTTP 403**, con el mensaje *"is missing
  prvReadsanic_mppp_tbl_fila"* — es decir, **ni siquiera llegan a evaluar el perfil de columna**:
  Dataverse los frena antes, porque ninguno de los dos tiene privilegio de lectura sobre Fila
  (`04` §2).

**Esto importa para leer el resultado bien.** El perfil de columna (`CSP - MPPP - Datos sensibles`)
es una barrera SEGUNDA que, con los 4 roles humanos de hoy, **nunca se llega a ejercitar sola**: los
dos únicos roles con `R` sobre Fila (Ejecutivo y Supervisor) son también los dos que están en el
perfil (`04` §4 — el equipo `MPPP - Ejecutivos` asociado, y User1/User2 asociados también directo,
verificado en vivo). Que "admin_planes no vea la cuenta" es cierto, pero por el mecanismo MÁS
GRUESO (no lee Fila en absoluto), no por el de columna. Esto reemplaza la advertencia anterior de
este archivo (que hablaba de un perfil vacío, ya corregida antes): la que queda ahora es sobre CUÁL
de los dos mecanismos frena a cada uno, con el código HTTP real de cada uno como evidencia.

**Necesita.** Permiso para impersonar (`MSCRMCallerID`) a los 4 roles; una Fila con datos
reales-ficticios (la tanda semilla alcanza).

**Cómo.** Leer `sanic_numeroidentificacion` y `sanic_numerocuenta` de esa Fila, impersonando cada
uno de los 4 roles, directo por Web API.

**Esperado.**

| Rol | Resultado esperado |
|---|---|
| Ejecutivo | HTTP 200, ve el valor real de las dos columnas |
| Supervisor | HTTP 200, ve el valor real de las dos columnas |
| Administrador de planes | HTTP 401/403 (no lee Fila; nunca llega a la columna) |
| Administrador técnico | HTTP 401/403 (ídem) |

**FALLA si** el Administrador de planes o el técnico ven el número de cuenta (con cualquier HTTP), o
si el Ejecutivo/Supervisor NO lo ven (ahí sí sería el perfil de columna fallando, o la asociación
equipo↔perfil rota — `diseno/PENDIENTES.md` D-49, prerrequisito de migración a Test/Producción).

> Probalo también **en la vista**, no solo por API directa: una columna protegida tiene que
> ocultarse en los dos lados. Las vistas de Fila (G-03) ya seleccionan estas dos columnas; si algún
> día se agrega un caso que corra esas vistas completas impersonando, hereda la misma protección
> automáticamente (es la misma plataforma la que enmascara, no una lógica de la vista).

**Automatizable.** API (`set_g.py:g05`, impersonación directa, sin formulario). La pertenencia al
perfil se puede confirmar de antemano por API de solo lectura (`fieldsecurityprofiles` →
`systemuserprofiles_association` / `teamprofiles_association`).

---

### G-06 · (fusionado en G-00 / G-00b)

**Qué prueba.** La matriz de privilegios (`04`) y su comportamiento de rechazo.

**Por qué no es un caso aparte.** Las mismas celdas ("el Administrador de planes no ve Solicitudes ni
Filas", "el Ejecutivo no puede aprobar", "nadie edita catálogos fuera de su rol") ya las prueba G-00
(la matriz en sí) y G-00b (`comportamiento_roles.rechazos()`, que ejercita exactamente esos rechazos
contra el entorno). Escribir una tercera llamada que verifique lo mismo probaría la herramienta dos
veces, no el sistema dos veces (CONVENCIONES §2). El ID queda documentado acá, sin función en
`CASOS`, para que quien busque "G-06" en un documento viejo sepa dónde está ahora.

**Automatizable.** — (ver G-00, G-00b).

---

### G-07 · Cada rol entra donde debe

**Qué prueba.** Que el sitemap se recorte solo, por privilegios — la EXPERIENCIA, no el dato
subyacente.

**Por qué sigue siendo GUI.** El recorte del sitemap por rol es un mecanismo NATIVO de Dataverse (una
`SubArea` con `Entity=X` se oculta sola si el usuario no tiene `R` sobre `X`; un `Area`/`Group` sin
ninguna entrada visible se oculta entero) — no hay una API que devuelva "qué ve este usuario en el
menú" sin en verdad renderizarlo. G-00 ya prueba, celda por celda, que cada rol tiene EXACTAMENTE los
privilegios que le corresponden; dado eso, el recorte es una consecuencia de la plataforma, y verlo
una vez con cada sesión es más rápido y más real que tratar de simularlo.

**Necesita.** Las 4 sesiones, una por rol.

**Cómo.**

```bash
cd pruebas/gui && MPPP_USUARIO=ejecutivo npx playwright test --headed   # repetir por sesión
```

**Esperado.**

| Rol | Ve |
|---|---|
| Ejecutivo | Trabajo (Por digitar, Devueltas, Para atención manual, Por clasificar, Para revisar), Consulta; Catálogos **solo lectura** |
| Supervisor | Trabajo (Por aprobar, Para revisar), Consulta |
| Administrador de planes | **solo** Catálogos (incluida Autorizaciones - Clientes) |
| Administrador técnico | **solo** Configuración |

**Cómo se sabe que falló.** Una entrada visible que la matriz de G-00 no le da (o al revés).

**Automatizable.** GUI (las 4 sesiones, una por vez); los privilegios subyacentes ya los cubre G-00.

---

### G-08 · Los 10 formularios

**Qué prueba.** 12.3 + 12.4.

**Corregido el 2026-09-27 — pasa de GUI a API.** Se lee `systemforms` (formulario principal, `type =
2`) por tabla, y el `formxml` de lo que este archivo marca "mirá especialmente".

**Necesita.** Credenciales de lectura en Dataverse.

**Cómo.** `systemforms?$filter=objecttypecode eq '<tabla>' and type eq 2 and name eq '<nombre>'`.

**Esperado.** Los 10 existen, nombrados uno por uno:

Solicitud · Fila · Cliente · Plan · Autorizado · Autorizacion (AutorizacionPlan) · **Autorizacion por
cliente (AutorizacionCliente)** · Parametro · Regla · **Motivo de accion** (el del diálogo).

**Mirá especialmente** (verificado contra el `formxml` real el 2026-09-27):

- **Solicitud**: sus subgrillas de Filas, ResultadoRegla y Bitácora existen en el formulario
  (`<TargetEntityType>` de las tres).
- **Autorización (AutorizacionPlan)**: el campo de **archivo** `sanic_documentofirmado` está en el
  formulario, habilitado.
- **Autorización por cliente (AutorizacionCliente)**: **desde el 2026-09-27 SÍ trae la misma
  evidencia que AutorizacionPlan** — `sanic_documentofirmado` (archivo, 10 MB) y
  `sanic_fechadocumento` (fecha), en una sección "Evidencia de la autorizacion", habilitados. Ver la
  corrección de más abajo: esto no siempre fue así, y este mismo archivo llegó a acusarlo de defecto.
- **Motivo de acción**: trae el campo `sanic_motivo`.

> ### Corrección — este caso estaba al revés: verificaba contra un documento viejo, no contra el sistema
>
> Esta migración (2026-09-27, la misma jornada en que se agregó la evidencia) escribió primero este
> caso citando `05-app-model-driven.md` §4, que decía textual: *"Sin sección de evidencia: esta tabla
> no tiene `sanic_documentofirmado` ni `sanic_fechadocumento`, a diferencia de Autorización"*. Contra
> esa cita, el caso afirmaba que `AutorizacionCliente` **NO** debía traer esos campos — y al verlos
> presentes y habilitados en vivo, los reportó como un **defecto confirmado**.
>
> **Era al revés.** El sistema estaba bien; el documento estaba viejo. El aprobador decidió, esa
> MISMA jornada (2026-09-27), agregar la evidencia a la autorización por cliente, con esta orden
> textual: *"Cuando entro a autorización x cliente, no está la columna de archivo adjunto para
> adjuntar la autorización por escrito y la fecha de ese documento (al igual que en autorización por
> plan)."* El motivo de negocio: una autorización por cliente habilita TODOS los planes de ese
> cliente a la vez — es MÁS amplia que la de plan — y no tiene sentido que exija MENOS respaldo que
> ella. La tabla, el formulario, `diseno/02-diccionario-datos.md` §2.4b, `diseno/06-inventario-
> componentes.md` (3.13) y `diseno/DIRECTRICES-NUEVAS.md` se actualizaron ese mismo día.
> `diseno/05-app-model-driven.md` §4 quedó afuera de esa ronda de corrección — y esa celda vieja
> también describía una sección técnica "Datos" con `ownerid` que ya no existe (se quitó cuando
> apareció la sección de evidencia, con campos de negocio reales). El aprobador ya corrigió esa celda
> (con su propia nota fechada); este archivo no toca `diseno/`.
>
> **Por qué importa dejarlo escrito, no solo corregido.** Es la misma familia de error que ya costó
> caro hoy con los conteos (CONVENCIONES §7: "12 parámetros" que nunca fueron 12; "8 steps" que
> incluían uno que nunca existió). Un dato — sea un número o una afirmación de "esta tabla no tiene
> tal columna" — que no se vuelve a mirar contra la plataforma envejece, y nadie lo nota hasta que
> alguien lo usa como vara para medir algo que sí cambió. La lección para este archivo: **verificar
> "en vivo" no es solo correr un comando contra Dataverse — es también preguntarse, cuando el
> resultado contradice al diseño, cuál de los dos envejeció**, en vez de asumir que el documento
> manda por default.
>
> El caso quedó **invertido** en `set_g.py:g08`: ahora verifica que `AutorizacionCliente` SÍ trae
> las dos columnas, en la tabla y en el formulario, habilitadas.

**FALLA si** falta alguno de los 10 formularios, si a Solicitud le falta alguna subgrilla, o si
`AutorizacionCliente` NO trae su evidencia (el sistema estaría retrocediendo respecto de la decisión
del aprobador del 2026-09-27).

**Automatizable.** API (`systemforms`; `set_g.py:g08`).

---

### G-09 · La vista "Autorizaciones sin evidencia" (D-42)

**Qué prueba.** Que la evidencia **no** condiciona la vigencia; es una lista de tareas del
administrador, no una lista de autorizaciones inválidas.

**Necesita.** Sesión o impersonación de `admin_planes`; las autorizaciones del fixture compartido
(ninguna tiene evidencia cargada, a propósito — ver `preparar_datos.py`).

**Cómo.** Ejecutar el `fetchxml` real de "Autorizaciones sin evidencia" (G-03), impersonando a
`admin_planes`.

**Esperado.** La vista incluye la autorización sin evidencia del fixture (`<REMITENTE> → PR11`).

**Y lo más importante**: esas autorizaciones **igual funcionan**. Cualquier caso de los sets
`C`/`S`/`R` (`correr.py C`, `correr.py S`, `correr.py R`) pasa con autorizaciones sin evidencia —
todas las que siembra `preparar_datos.py` nacen así. Si en algún lado una autorización sin documento
hace fallar `AUTORIZACION_CORREO_PLAN`, es **FALLA** contra D-42, y se vería ahí, no acá.

**Automatizable.** API (`set_g.py:g09`; impersonación, sin navegador).

---

### G-10 · La campana de notificaciones (DA-08)

**Qué prueba.** Los avisos dentro de la app: uno por solicitud y por tanda, **nunca uno por fila**.

**Corregido el 2026-09-27 — suma una pata de API.** `appnotification` es una tabla nativa; se puede
crear la tanda y leerla directo, sin esperar a que alguien la mire en pantalla.

**Necesita.** Nada previo especial: el propio caso crea su tanda (una Solicitud con 3 Filas, vía el
Excel E01).

**Cómo.** Crear una Solicitud con 3 Filas por digitar y leer `appnotifications` (filtrado por
`createdon` desde antes de crearla).

**Esperado.** Exactamente **1** notificación nueva, no 3 (una por tanda, nunca una por fila).

> **Hallazgo, verificado en vivo el 2026-09-27 (exploratorio, antes de escribir el caso; la
> Solicitud de prueba se creó y se borró en el momento).** Al procesar una Solicitud que deja 3
> Filas por digitar, **no apareció ninguna notificación en `appnotification`**. Según DA-08, este
> aviso lo crea "la Custom API de validación", que es exactamente la que se llamó. Esto es
> compatible con dos lecturas: (a) el aviso todavía no está implementado del lado del servidor para
> este evento, o (b) se dispara por un camino distinto al que toma `sanic_mppp_capi_validarsolicitud`
> llamada directo. **No se investigó el código del plugin para decidir cuál es** (fuera del alcance
> de este archivo, que prueba contra la plataforma, no lee C#): si `set_g.py:g10` da FALLA al
> correr `correr.py G`, es este hallazgo, confirmado, no un dato nuevo — y por CONVENCIONES §3 no se
> convierte en N-A.

**Qué NO prueba.** Que al tocar la notificación se abra la vista o la Solicitud correcta: eso es
render (necesita ver el panel de notificaciones abrirse) y queda GUI, sin arnés propio todavía.

```bash
cd pruebas/gui && MPPP_USUARIO=ejecutivo npx playwright test --headed
```

**Automatizable.** API (parcial: creación + lectura de `appnotifications`, `set_g.py:g10`); GUI para
la navegación al tocar.

---

### G-11 · Multiidioma de la app

**Qué prueba.** La convención: etiquetas en 1033 y en el idioma del cliente, en sitemap, vistas y
columnas — no solo en los botones de comando (eso ya lo cubre A-13, en `pruebas/05-app-y-comandos.md`,
para los 7 botones del ribbon).

**Necesita.** Un usuario con el idioma cambiado a inglés (Configuración personal → Idiomas);
cualquiera de las 4 sesiones alcanza.

**Cómo.** Cambiá el idioma del usuario a inglés y recorré el sitemap, una vista y un formulario.

```bash
cd pruebas/gui && MPPP_USUARIO=ejecutivo npx playwright test --headed
```

(sesión real, idioma cambiado a mano antes de correr — el arnés no tiene todavía un caso de
multiidioma para sitemap/vistas/columnas, distinto del de los botones de A-13).

**Esperado.** Las etiquetas del sitemap, las vistas y las columnas cambian de idioma. Ninguna
queda con el texto del otro idioma ni con el nombre lógico crudo.

**Nota, no bloqueante.** El playbook del sitemap (`playbooks/app/mantenimientoppp.md`) declara sus
títulos SIN tilde en los dos idiomas ("Configuracion", "Catalogos", "Para atencion manual") — no es
un problema de multiidioma (los DOS locales, 1033 y 3082, están presentes y son iguales a propósito
en este entorno de habla hispana), pero si algún día el cliente pide tildes, es este playbook el que
hay que tocar, no algo que este caso pueda arreglar solo con ver la pantalla.

**Cómo se sabe que falló.** Un texto que queda en español cuando el usuario está en inglés, o un
nombre lógico crudo (`sanic_numerocuenta` en vez de una etiqueta).

**Automatizable.** GUI — pide ver texto renderizado en el idioma del usuario.

---

### G-12 · La app valida limpia

**Qué prueba.** Que la app no tenga componentes faltantes.

**Necesita.** Credenciales de lectura en Dataverse (las que ya usan las herramientas del proyecto).
Ningún rol de la app.

**Cómo.**

```bash
python3 herramientas/construir/app.py playbooks/app/mantenimientoppp.md --solo-verificar
```

**Esperado.** `"estado": "ya_existia"` con `"4 areas y 15 entradas, 12 tablas, 4 roles ...
ValidateApp sin errores"` (verificado en vivo el 2026-09-27; la tabla 12 es `AutorizacionCliente`,
DN-01). Comparte la corrida con G-01: es el mismo comando, `set_g.py` lo cachea para no llamarlo dos
veces.

**Automatizable.** API (`app.py --solo-verificar`; `set_g.py:g12`).

---

### G-13 · La frontera con el RPA: el rol no trae privilegios de negocio ni el perfil de columna

**Qué prueba.** Que `SR - MPPP - RPA` (fase 2, D-9/D-44, identidad de aplicación) tenga **cero**
privilegios sobre las tablas de negocio de `04-matriz-privilegios.md`, y que el usuario de aplicación
que lo trae **no** esté en `CSP - MPPP - Datos sensibles` (decisión del aprobador 2026-09-25). Ver
"Fase 1 + fase 2" arriba para la frontera exacta con el set B.

**Necesita.** Credenciales de lectura en Dataverse. Ningún rol de la app (el RPA no la abre).

**Cómo.**

1. `roles?$filter=name eq 'SR - MPPP - RPA'` → `roleid`.
2. `roles(<roleid>)/roleprivileges_association` → comparar contra los privilegios de las 11 tablas
   de negocio de `04-matriz-privilegios.md` (mismo catálogo que usa `verificar_accesos.py`).
3. `systemusers?$filter=systemuserroles_association/any(x:x/roleid eq <roleid>) and applicationid ne
   null` → el usuario de aplicación del bot.
4. `fieldsecurityprofiles` → confirmar que ese usuario no está en `CSP - MPPP - Datos sensibles`, ni
   directo ni por equipo.

**Esperado.** `81` privilegios en total (verificado en vivo el 2026-09-27, coincide con
`diseno/FASE2-ESTADO.md` 6.7), **0** de ellos sobre las tablas de `04`; exactamente un usuario de
aplicación con el rol (`# MPP-RPA`); ese usuario no pertenece a `CSP - MPPP - Datos sensibles` ni
directo ni por el equipo `MPPP - Ejecutivos`.

**FALLA si** el rol trae algún privilegio sobre una tabla de negocio (D-9/D-44 dejarían de ser
ciertos: el bot podría leer/escribir por una vía que no es su Custom API), o si el usuario de
aplicación aparece en el perfil de columna (podría leer identificación/cuenta por fuera de su API,
que es exactamente lo que el aprobador descartó el 2026-09-25).

**Automatizable.** API (`set_g.py:g13`; ninguna impersonación, son lecturas de configuración).

---

### Sitemap, vistas y formulario, casos G-14 a G-19 (fusionado del ex set H)

**Fusionado acá el 2026-09-28** (`CONVENCIONES.md` §4, "La regla del nombre"): estos seis casos
eran el set `H` (`13-construido-hoy.md`, prefijo `H-`), un archivo aparte nombrado por la fecha en
que se escribió — "Construido el 2026-09-27" — no por lo que prueba. Seis de sus siete casos son
sobre sitemap, columna de trabajo, formulario y componentes de la app: exactamente el tema de ESTE
archivo, así que pasaron acá como `G-14`..`G-19` (`H-01`→`G-14` … `H-05`→`G-18`, `H-07`→`G-19`,
saltando `H-06`, que es sobre un comando de la barra y pasó a `A-15` en `05-app-y-comandos.md`).
`13-construido-hoy.md` desapareció; la tabla de equivalencias completa vive en `pruebas/README.md`.

**Corrección que ya traían estos casos contra `CONVENCIONES.md` (§1, §2, §7, §8).** La versión
original de este archivo (`13-construido-hoy.md`) marcaba `H-01` a `H-05` como `Automatizable: GUI`
(inspección visual con sesión de navegador). Eso estaba mal por partida doble: (1) el sitemap, el
`layoutxml`/`fetchxml` de una vista y el `formxml` de un formulario son metadata **publicada**,
legible por Web API con el mismo service principal que usa cualquier otro set de este archivo —
mismo criterio que ya aplican G-01/G-03/G-08 arriba; (2) una inspección visual no es lo que pide
`CONVENCIONES` §2 ("la verificación va contra la plataforma, no contra la salida del constructor" —
ni contra el ojo de quien mira). Por eso corren como `API`, contra la plataforma en vivo, igual que
el resto de este set.

**Ninguna sesión de navegador para G-14 a G-18 y G-19**: se verifican contra Web API directo, con el
mismo service principal `# PowerPlatform-Dev` que usa el resto de este archivo. No crean ningún
dato: es lectura de metadata ya publicada (sitemap, vistas, formulario, componentes de la app) —
nada que `cerrar()` tenga que borrar por estos seis casos en particular.

---

### G-14 · Sitemap: "Para atención manual" en el área Trabajo, en su posición exacta

**Qué prueba.** El olvido corregido el 2026-09-27: la vista "Para atencion manual" existía desde
fase 2 pero no tenía entrada propia en el sitemap. Ahora `subarea_paraatencionmanual` está,
EXACTAMENTE, entre `subarea_poraprobar` y `subarea_porclasificar` dentro del área con título 1033
"Trabajo", con sus títulos en 1033 y 3082, y abre la vista real "Para atención manual" (no una vista
distinta ni una `Url` inventada).

**Necesita.** Nada del navegador: conexión a Dataverse por Web API (el mismo service principal que
usan los demás sets). Ningún dato previo — es lectura de metadata ya publicada.

**Cómo.** `GET sitemaps?$select=sitemapxml&$filter=sitemapnameunique eq 'sanic_mppp_sm_mantenimientoppp'`;
parsear el XML devuelto; ubicar el `Area` cuyo `Titles/Title[@LCID='1033']` sea "Trabajo"; leer, en
orden de documento, los `Id` de sus `SubArea`; ubicar `subarea_paraatencionmanual` y confirmar que su
`Url` apunta al `savedqueryid` real de la vista "Para atencion manual" sobre `sanic_mppp_tbl_fila`
(`GET savedqueries?$select=savedqueryid&$filter=name eq 'Para atencion manual' and returnedtypecode eq 'sanic_mppp_tbl_fila'`).

**Qué tiene que pasar.** `subarea_paraatencionmanual` aparece consecutiva entre `subarea_poraprobar` y
`subarea_porclasificar` (sin ninguna otra entrada en el medio), con título 1033 y 3082 = "Para
atencion manual", y su `Url` contiene `etn=sanic_mppp_tbl_fila` y el `savedqueryid` real de esa vista.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py G` (caso G-14) — las dos consultas
`GET` de arriba, comparadas en código, no a mano.

**Cómo se sabe que falló.** Alguna de estas, todas objetivas y distinguibles entre sí: la entrada no
existe (regresión del arreglo); existe pero no está pegada entre `poraprobar` y `porclasificar`
(alguien la movió); le falta el título en 1033 o en 3082 (`playbooks/app/mantenimientoppp.md`
§"Cada titulo lleva etiqueta por cada idioma"); o su `Url` apunta a un `savedqueryid` que no es el
de "Para atencion manual" (abre otra vista sin que se note a simple vista).

**Automatizable.** API (Web API directo, sin sesión de navegador).

---

### G-15 · Columna "Requiere atención manual" en las 4 vistas de trabajo, y en ninguna otra

**Qué prueba.** El hallazgo del aprobador: sin esta columna, un ejecutivo no podía ver de un vistazo
si el bot marcó la fila en las vistas donde ya trabaja todos los días. Se agregó a **Mis clientes —
por digitar**, **Todos — por digitar**, **Por aprobar** y **Mis clientes — devueltas** — y a NINGUNA
otra vista de Fila (ni **Filas**, ni **Terminadas**, ni **Tomadas por el bot**).

**Necesita.** Nada del navegador: Web API directo. Ningún dato previo.

**Cómo.** Para cada una de las 7 vistas nombradas arriba:
`GET savedqueries?$select=fetchxml,layoutxml&$filter=name eq '<nombre>' and returnedtypecode eq 'sanic_mppp_tbl_fila'`;
parsear el `layoutxml` (`<row>/<cell name=...>`) y el `fetchxml` (`<entity>/<attribute name=...>`).

**Qué tiene que pasar.** En **Mis clientes - por digitar**, **Todos - por digitar**, **Por aprobar** y
**Mis clientes - devueltas**: el `layoutxml` trae una `<cell name="sanic_requiereatencionmanual">`
inmediatamente después de `sanic_numerofila`, y el `fetchxml` trae su `<attribute>`. En **Filas**,
**Terminadas** y **Tomadas por el bot**: la celda NO está.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py G` (caso G-15).

**Cómo se sabe que falló.** Falta la celda o el atributo en alguna de las 4 vistas de trabajo (el
ejecutivo sigue sin ver la marca donde más la necesita); está pero no pegada a `sanic_numerofila`
(queda perdida entre las demás columnas, contra el motivo de diseño de cada playbook de vista); o
SOBRA en Filas/Terminadas/Tomadas por el bot (ruido en vistas que el diseño dejó explícitamente
afuera).

**Automatizable.** API (`layoutxml`/`fetchxml` reales vía Web API).

---

### G-16 · Catálogos: "Correos Autorizados", "Autorizaciones - Planes" y "Autorizaciones - Clientes"

**Qué prueba.** Los dos renombres del 2026-09-27 ("Autorizados" → "Correos Autorizados",
"Autorizaciones" → "Autorizaciones - Planes") y la entrada nueva de DN-01 ("Autorizaciones -
Clientes"), las tres en el sitemap en vivo, cada una abriendo la vista por defecto de su propia
tabla.

**Necesita.** Nada del navegador: Web API directo. Ningún dato previo.

**Cómo.** Sobre el mismo `sitemapxml` de G-14, ubicar el `Area` con título 1033 "Catalogos" y sus
`SubArea` `subarea_autorizados`, `subarea_autorizaciones` y `subarea_autorizacionescliente`; leer
título 1033/3082 y el atributo `Entity` de cada una; y confirmar la vista por defecto real de cada
tabla con
`GET savedqueries?$select=name&$filter=returnedtypecode eq '<tabla>' and isdefault eq true and querytype eq 0`.

**Qué tiene que pasar.** `subarea_autorizados`: título "Correos Autorizados", `Entity` =
`sanic_mppp_tbl_autorizado`. `subarea_autorizaciones`: título "Autorizaciones - Planes", `Entity` =
`sanic_mppp_tbl_autorizacionplan`, cuya vista por defecto es "Autorizaciones activas".
`subarea_autorizacionescliente`: título "Autorizaciones - Clientes", `Entity` =
`sanic_mppp_tbl_autorizacioncliente`, cuya vista por defecto es "Autorizaciones por cliente activas".
Las dos últimas son entradas DISTINTAS que abren tablas DISTINTAS.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py G` (caso G-16).

**Cómo se sabe que falló.** Falta alguna de las tres entradas; algún título quedó con el nombre viejo
("Autorizados"/"Autorizaciones" a secas); las dos entradas de autorización abren la MISMA tabla (no
se diferenciaron); o la vista por defecto de alguna de las dos tablas no es la esperada (el sitemap
"apunta bien" pero la vista que se abre no es la correcta).

**Automatizable.** API.

---

### G-17 · Formulario "Autorizacion por cliente": encabezado (2 lookups) + evidencia

**Qué prueba.** El formulario nuevo (DN-01, 2026-09-27), leyendo su `formxml` real de
`systemforms`: el encabezado con los 3 campos (`sanic_nombre`, y los lookups
`sanic_autorizadoid`/`sanic_clienteid`) y la sección separada "Evidencia de la autorizacion" con
`sanic_documentofirmado` y `sanic_fechadocumento` (ver también `10-autorizacion-cliente.md` U-10, que
prueba el guardado; este caso es solo de estructura del formulario).

**Necesita.** Nada del navegador: Web API directo. Ningún dato previo.

**Cómo.**
`GET systemforms?$select=formxml&$filter=objecttypecode eq 'sanic_mppp_tbl_autorizacioncliente' and type eq 2`
(2 = Main); parsear el `formxml`: los `<control datafieldname="...">` del `<header>`, y los de la
`<section>` cuya `<label description="Evidencia de la autorizacion">` coincide.

**Qué tiene que pasar.** El encabezado trae `sanic_nombre`, `sanic_autorizadoid` y `sanic_clienteid`,
y los dos últimos son controles de tipo Lookup (`classid` `{270BD3DB-D9AF-4782-9025-509E298DEC0A}`).
Existe una sección separada "Evidencia de la autorizacion" con `sanic_documentofirmado` y
`sanic_fechadocumento`.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py G` (caso G-17).

**Cómo se sabe que falló.** Falta algún campo del encabezado, o alguno de los dos lookups no es un
control Lookup real (`classid` distinto); o falta la sección de evidencia, o sus campos están
mezclados con el encabezado en vez de en su propia sección (el hueco que señaló el aprobador seguiría
sin cerrarse en la interfaz, aunque las columnas ya existan en la tabla).

**Automatizable.** API (`formxml` real vía Web API).

---

### G-18 · El ícono reutilizado, no uno nuevo

**Qué prueba.** "Para atención manual" reusa el SVG de "Para revisar" (mismo patrón de negocio);
"Autorizaciones - Clientes" reusa el ícono de "Autorizaciones - Planes". Ninguna de las dos generó un
web resource nuevo.

**Necesita.** Nada del navegador: reusa el mismo `sitemapxml` de G-14/G-16. Ningún dato previo.

**Cómo.** Sobre el `sitemapxml` en vivo, comparar el atributo `Icon` de `subarea_paraatencionmanual`
contra el de `subarea_pararevisar`, y el de `subarea_autorizacionescliente` contra el de
`subarea_autorizaciones`.

**Qué tiene que pasar.** Los dos pares de `Icon` son, cada uno, el MISMO valor
(`/WebResources/sanic_mppp_wr_svg_pararevisar` y `/WebResources/sanic_mppp_wr_svg_autorizaciones`
respectivamente) — nada de un web resource nuevo.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py G` (caso G-18).

**Cómo se sabe que falló.** El `Icon` de alguna de las dos entradas nuevas difiere del de su par (se
habría creado o asignado un web resource distinto sin necesidad).

**Automatizable.** API (mismo `sitemapxml` de G-14/G-16, sin navegador).

---

### G-19 · `sanic_mppp_tbl_autorizacioncliente` como componente real de la app

**Qué prueba.** El hueco que dejó resolver esta tabla con un **import manual de la solución** en vez
de con la herramienta del proyecto en modo escritura: que la tabla sea un componente REAL de la app
`MDA - MPPP - Mantenimiento PPP` (`RetrieveAppComponents` en vivo), no solo algo que el sitemap
menciona (G-16) sin que la tabla misma viaje con la app.

**Necesita.** Nada del navegador: Web API directo, más una corrida de solo lectura de
`herramientas/construir/app.py` (con `--solo-verificar` nunca escribe). Ningún dato previo.

**Cómo.**
1. `python3 herramientas/construir/app.py playbooks/app/mantenimientoppp.md --solo-verificar`.
2. Releer, en el propio caso (no confiar solo en la salida del paso 1 — `CONVENCIONES` §2):
   `GET appmodules/Microsoft.Dynamics.CRM.RetrieveUnpublishedMultiple()?$select=appmoduleid&$filter=uniquename eq 'sanic_mppp_mda_mantenimientoppp'`,
   después `GET RetrieveAppComponents(AppModuleId=<id>)` y, para cada componente de tabla
   (`componenttype eq 1`), `GET EntityDefinitions(<objectid>)?$select=LogicalName`.

**Qué tiene que pasar.** El paso 1 termina en `ya_existia` (nunca `difiere`/`error`/`bloqueado`), y
entre los nombres lógicos que trae el paso 2 aparece, POR NOMBRE, `sanic_mppp_tbl_autorizacioncliente`
— no se afirma un total de tablas (`CONVENCIONES` §7): ese número puede cambiar sin que nada esté
roto.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py G` (caso G-19).

**Cómo se sabe que falló.** El paso 1 no da `ya_existia` (el sitemap o los componentes en vivo se
despegaron del playbook); o `sanic_mppp_tbl_autorizacioncliente` no aparece por nombre en
`RetrieveAppComponents` (la tabla no viaja con la app, aunque el sitemap la mencione — exactamente el
síntoma que motivó este caso: un import manual que quedó a medias).

**Automatizable.** API.

---

### Resultados

| Caso | Resultado | Notas |
|---|---|---|
| G-00 privilegios efectivos (4 roles) | | |
| G-00b comportamiento de cada rol | | |
| G-01 sitemap: 15 entradas, su vista exacta | | |
| G-02 íconos | | GUI, sin arnés |
| G-03 las 20 vistas | | |
| G-04 cartera del ejecutivo | | corregido: el caso comparaba mal, ver nota en G-04 |
| **G-05 columnas protegidas** | | el más importante |
| G-06 (fusionado en G-00/G-00b) | | sin función propia |
| G-07 sitemap por rol | | GUI, sin arnés |
| G-08 los 10 formularios | | corregido: el caso estaba al revés, ver nota en G-08 |
| G-09 sin evidencia pero válida (D-42) | | |
| G-10 notificaciones (DA-08) | | ver hallazgo appnotification |
| G-11 multiidioma | | GUI, sin arnés |
| G-12 ValidateApp | | |
| G-13 frontera con el RPA | | nuevo |
| G-14 sitemap "Para atención manual", en su posición | OK | ex H-01, API, sin sesión |
| G-15 columna en las 4 vistas de trabajo, y en ninguna más | OK | ex H-02, API, sin sesión |
| G-16 "Correos Autorizados" / "Autorizaciones - Planes" / "Autorizaciones - Clientes" | OK | ex H-03, API, sin sesión |
| G-17 formulario AutorizacionCliente: encabezado + evidencia | OK | ex H-04, API, sin sesión |
| G-18 íconos reutilizados, no nuevos | OK | ex H-05, API, sin sesión |
| G-19 la tabla AutorizacionCliente es componente real de la app | OK | ex H-07, API, sin sesión |

---
## 07 — MPPP-VIG, la red de seguridad

`MPPP-VIG` corre **cada 10 minutos** y recupera lo que quedó a medias. Hace lo mismo que `MPPP-REC`
y `MPPP-COM` cuando algo falló, y por eso esos dos no tienen lógica propia (DF-02).

Migrado a `sets/set_v.py` el 2026-09-27/28 (CONVENCIONES §8). El set es autocontenido: no depende de
que otro set haya corrido antes.

### El recurrence real, y de dónde sale el margen

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

### D-62 corregido (2026-09-28) — la etapa de arranque ya blinda con Failed

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

### La pregunta que dejó abierta M-06 (set M, `04-comunicaciones.md`)

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

### Investigación de la causa de fondo de "Fallo la ingesta del correo" (2026-09-28, SIN corregir)

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

### Qué bloque prueba cada caso (nombrado, no contado)

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

### Qué corre por default y qué no

Seis de los siete casos (V-02, V-03, V-04, V-05, V-06, V-07) dependen del schedule real de
`MPPP-VIG` (V-02 de una sola vuelta; los otros cinco, de tres): un set con los seis prendidos
supera las **3 horas**. Nadie corre eso de rutina (mismo criterio que el set E con E-08). Por eso:

- **V-01 corre siempre.** Es 100% API, sin esperar nada (activo, recurrence real, encadenamiento de
  los 5 bloques, el hallazgo de la etapa de arranque, comparación repo↔desplegado, y los 4
  parámetros nombrados).
- **V-02 queda `N-A` por default**, con el comando exacto para correrlo a propósito:

  ```bash
  MPPP_V02_ALTO_RIESGO=si python3 herramientas/pruebas/correr.py V
  ```

  **Corrección 2026-09-28.** Hasta esta fecha V-02 era 100% manual: el arnés nunca tocaba el estado
  de un flujo. El aprobador autorizó una excepción puntual — ver la sección de V-02 más abajo y
  `CONVENCIONES.md` ("Excepción V-02") para las dos redes de seguridad con las que corre.
- **V-03, V-04, V-05, V-06 y V-07 quedan `N-A` por default**, con el comando exacto para correrlos a
  propósito:

  ```bash
  MPPP_V_LENTOS=si python3 herramientas/pruebas/correr.py V
  ```

  Se corren así cuando `mppp-vig.json` cambió de forma que pudiera afectar alguno de los 5 bloques,
  avisando al equipo y fuera de horario de otras pruebas (comparten el mismo entorno con S/I/M/etc.).

**`correr.py --full`** (2026-09-28, decisión del aprobador: "corré todas, todas sin importar nada")
prende `MPPP_V02_ALTO_RIESGO` y `MPPP_V_LENTOS` a la vez, con un banner previo que avisa el costo y
el riesgo antes de arrancar — ver `herramientas/pruebas/correr.py --listar` y su docstring.

---

### V-01 · El flujo está vivo y bien armado (sin esperar el schedule)

**Qué prueba.** Que `MPPP-VIG` esté activo, que su recurrence real sea la que se documenta acá, que
los 5 bloques existan y estén encadenados de forma que uno que falle no tumbe a los que siguen, que
la etapa de arranque tenga el mismo blindaje (D-62, corregido el 2026-09-28 — este caso pasó de dar
FALLA a propósito a verificar que el blindaje sigue puesto), y que el trigger tenga Concurrency
Control en 1 (parte (b) de D-62).

**Necesita.** Nada de negocio: solo lectura de la definición del flujo (`workflows.clientdata`, Web
API) y de `recursos/flujos/mppp-vig.json` (solo lectura, nunca se edita desde este set); los 4
umbrales y, desde el 2026-09-28, `retencion.dias.general`/`retencion.dias.noreconocidas` con una
versión activa y CONTENIDO válido (entero positivo) — verificado en `preparar()`.

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
| `retencion.dias.general` y `retencion.dias.noreconocidas`, CONTENIDO (no solo que existan, 2026-09-28) | cada uno con una versión activa y un valor entero positivo (`preparacion_comun.verificar_parametro_entero_positivo()`) — recupera la cobertura del antiguo P-06; ningún bloque de `MPPP-VIG` los consume hoy (los usa el futuro job de purga de fase 3), pero es el hogar más cercano que existe entre los parámetros numéricos de fase 1 |

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

### V-02 · Bloque 1 — un correo que quedó en la bandeja

**Qué prueba.** Que un correo que `MPPP-REC` no llegó a procesar se ingiera igual, por `MPPP-VIG`.

**Corrección 2026-09-28 — de manual a automatizado, con excepción autorizada.** Hasta esta fecha
este caso era 100% manual: la regla del proyecto era que el arnés nunca desactiva un flujo, sin
excepción. El aprobador autorizó una excepción puntual, única en todo el arnés, para poder probar
justo este escenario de verdad — ver `CONVENCIONES.md`, sección "Excepción V-02", para las dos
redes de seguridad con las que `set_v.py::v02()` corre esto ahora. El texto que sigue describe el
comportamiento AUTOMATIZADO actual; el procedimiento manual anterior queda reemplazado, no
documentado aparte (era exactamente estos mismos pasos, hechos a mano).

**Necesita.** El buzón de prueba real de Graph (`PP_MAIL_BUZON_3`, mismo que usa `set_m.py` como
remitente — los sets no corren en paralelo, no hay colisión), autorizado sobre PR11; el adjunto
`E01-feliz-formato11.xlsx`; `MPPP_V02_ALTO_RIESGO=si` (o `--full`); y que `MPPP-REC` esté
**ACTIVO** antes de empezar — lo garantiza la RED 2 (`_verificar_y_recuperar_rec()`, corre siempre
al arrancar `preparar()` del set, con o sin `MPPP_V02_ALTO_RIESGO`).

**Cómo (`v02()` de `set_v.py`).**
1. Confirma que `MPPP-REC` está Activo (precondición) y lo **apaga de verdad** (`PATCH
   statecode=0`), releyendo el estado para confirmar que quedó apagado — nunca se confía en el
   HTTP 200/204 del PATCH solo.
2. Manda un correo REAL, por Graph, desde el buzón de prueba hacia `simulador.BUZON_INGESTA`, con
   `E01-feliz-formato11.xlsx` adjunto y un asunto único (`ZZP-V02-<nonce>`).
3. Espera 60s y comprueba que **no** se creó ninguna Solicitud (confirma que el apagado de MPPP-REC
   es real, no un PATCH que la plataforma ignoró).
4. Espera hasta que pasen los minutos de `vigilancia.minutos.sinvalidar` (leído en vivo) + 1 vuelta
   de `MPPP-VIG` (~10 min) + colchón, poll por el asunto único.
5. En el `finally` (corre SIEMPRE, incluso si algo de arriba reventó): **vuelve a encender**
   `MPPP-REC` (`PATCH statecode=1`) y **relee el estado** para confirmar que quedó Activado. Si no
   pudo, es un FALLA ruidoso del propio caso — RED 1 de la excepción, ver `CONVENCIONES.md`.

**Qué tiene que pasar.**

| Qué | Valor |
|---|---|
| MPPP-REC queda desactivado, verificado | **sí**, antes de mandar el correo |
| Con REC apagado, no se crea sola ninguna Solicitud en el primer minuto | **confirmado** |
| Se crea la Solicitud | **sí**, la ingirió VIG (dentro del margen: umbral + 1 vuelta + colchón) |
| Bitácora `Ingresada` | origen **MPPP-VIG** (no MPPP-REC) |
| El resto | igual que C-01: En proceso, 3 filas Validadas, acuse enviado |
| MPPP-REC queda reencendido, verificado (RED 1) | **sí**, pase lo que pase arriba |

**Cómo se verifica.** `ver_solicitud.py <numero>` después del margen, mirando el origen de la
entrada `Ingresada` en la Bitácora; el propio caso ya deja el estado final de `MPPP-REC` en su
último `check`.

**Cómo se sabe que falló.** No aparece ninguna Solicitud después del margen completo: o el correo
no llegó de verdad a la bandeja (revisar el envío por Graph), o el bloque 1 no lo está viendo
(mirar `vigilancia.minutos.sinvalidar` con V-01, y si el problema persiste, el hallazgo del
arranque común de esta misma sección). Si el ÚLTIMO check (RED 1, reencendido y verificado) da
FALSE, es la alarma más seria de todo el set: **hay que reencender `MPPP-REC` a mano YA** — los
correos del banco no están entrando mientras siga apagado.

**Automatizable.** `API`, detrás de `MPPP_V02_ALTO_RIESGO=si` (o `--full`) — excepción autorizada,
única en todo el arnés, a que "el arnés nunca desactiva un flujo" (`CONVENCIONES.md`). En modo
normal (sin la bandera) sigue `N-A`, con el comando exacto para correrlo a propósito.

---

### V-03 · Bloque 2 — una Solicitud vieja en Ingresada se reintenta

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

### V-04 · Bloque 2 — se acaban los reintentos

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

### V-05 · Bloque 3 — una comunicación que nunca se inició

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

### V-06 · Bloque 5 — los correos por clasificar vencen

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

### V-07 · Bloque 4 — un envío se inició y nunca se confirmó (cierra la pregunta abierta de M-06)

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

### Resultados

| Caso | Solicitud | Resultado | Notas |
|---|---|---|---|
| V-01 configuración y hallazgo de arranque | | OK | |
| V-02 correo en la bandeja | (ninguna: nunca se creó) | **FALLA** | corrida real, 2026-09-28 ~15:53-16:22 UTC (`MPPP_V02_ALTO_RIESGO=si`). Ver la sección "V-02, primera corrida real (2026-09-28) — FALLA" más abajo. |
| V-03 reintento | | N-A | no corrido esta vez (requiere `MPPP_V_LENTOS=si`) |
| V-04 se acaban los reintentos | | N-A | ídem |
| V-05 comunicación nunca iniciada | | N-A | ídem |
| V-06 vencimiento | | N-A | ídem |
| V-07 envío iniciado sin confirmar (M-06) | | N-A | ídem |

> **V-02 (2026-09-28) ya deja `MPPP-REC` prendido solo, verificado, en su propio `finally`** — no
> hace falta acordarse a mano. Si de todos modos algo raro pasó, comprobalo:
> `python3 herramientas/dataverse_api.py GET "workflows?\$select=name,statecode&\$filter=name eq 'Cloud Flow - MPPP - REC - Recibir correo nuevo'"`
> — tiene que decir `statecode: 1`.

#### V-02, primera corrida real (2026-09-28) — FALLA

**Primera vez que se ejercitó el mecanismo de punta a punta** desde que se escribió (el agente que
lo escribió fue detenido durante la verificación). Comando: `MPPP_V02_ALTO_RIESGO=si python3
herramientas/pruebas/correr.py V`.

**Las dos redes de seguridad funcionaron.** `MPPP-REC` quedó DESACTIVADO, verificado; el correo real
se mandó (HTTP 202, Graph); con `MPPP-REC` apagado no se creó ninguna Solicitud sola en el primer
minuto (confirma que el apagado fue real). Al terminar (por el `finally`, RED 1), `MPPP-REC` quedó
REENCENDIDO y VERIFICADO — confirmado además de forma independiente, releyendo la plataforma después
de que `correr.py` terminó (`statecode=1`), no solo confiando en el check del propio caso.

**El único check que dio FALLA:** "MPPP-VIG (bloque 'Correos_que_quedaron_en_la_bandeja') terminó
creando la Solicitud" — no se creó dentro del margen (15 min de umbral + 1 vuelta + colchón = 28
min), ni tampoco después: a las 16:24 UTC (más de 30 min después de mandado el correo) seguía sin
existir ninguna Solicitud con el asunto `ZZP-V02-9033d81f`.

**No es un defecto de la consulta ni del envío.** El correo llegó de verdad a la bandeja
(`mppp@55xljh.onmicrosoft.com`, recibido `2026-09-28T15:53:03Z`, confirmado leyendo el buzón por
Graph directamente — y era el ÚNICO mensaje en la bandeja, sin backlog que lo tapara). `V-01` (misma
corrida) dio OK: el flujo está Activado, con el recurrence de 10 minutos, los 5 bloques y la etapa de
arranque (D-62) bien encadenados, y lo desplegado coincide con el repo.

**Evidencia que sí apunta a un defecto real: `MPPP-VIG` no dejó NINGUNA marca de haberse ejecutado en
más de 5 horas.** Consultando la Bitácora por origen `MPPP-VIG` (159460005), el evento más reciente
de cualquier tipo es de `2026-09-28T10:59:12Z`; la corrida de V-02 terminó de esperar a las
`~16:22Z` y esta verificación se hizo a las `16:24:58Z` — más de 5 horas sin un solo evento
`Ingresada`, `Reintento`, `Error` ni `Vencida` con ese origen, pese a que el flujo figura `statecode=1`
(Activado) en Dataverse y `V-01` valida su definición. Esto es consistente con que el TRIGGER del
flujo no se está disparando de verdad en Power Automate (recurrencia pausada del lado de Flow,
problema de conexión/autenticación, DLP, licencia, throttling), no con un defecto de la consulta del
bloque 1 ni de este caso. **No se pudo confirmar la causa exacta**: el historial de ejecuciones de
Power Automate vive en `service.flow.microsoft.com`, fuera del alcance de las credenciales de
Dataverse que usa este arnés (mismo límite documentado en la sección "Investigación de la causa de
fondo" más arriba). **Merece su propio ID en `diseno/PENDIENTES.md`**, a criterio de quien diseñó el
flujo — no se corrige desde acá.

**La segunda red (verificar al arrancar) no se pudo ejercitar en esta sesión.** El intento de apagar
`MPPP-REC` A MANO (con `dataverse_api.py`, fuera de `v02()`) para simular una corrida anterior que
murió a la mitad fue bloqueado por el sistema de permisos del entorno (clasificador de modo
automático: "Modify Shared Resources"). Se paró de inmediato sin reintentar ni buscar otra vía; se
confirmó releyendo la plataforma que `MPPP-REC` quedó intacto (`statecode=1`), sin efecto del intento
bloqueado. Queda pendiente para quien tenga el permiso habilitado.

---
## 08 — Integridad y plugins

Set **I** (`CONVENCIONES.md` §4). Cubre los **steps de plugin** que corren al guardar (la red que
impide dejar el sistema en un estado imposible), el **catálogo de Reglas**, las **dos claves
alternativas de autorización** (por plan y, desde fase 2, por cliente — DN-01) y la **puerta de
las Custom API del RPA** (D-9/D-44).

**Reescrito el 2026-09-27.** La versión anterior de este archivo tenía las afirmaciones falsas más
graves de todo el plan, y las tres se corrigen acá:

1. **La tabla de "8 steps registrados" listaba un step que nunca existió** (`IntegridadAutorizacionPlanStep`)
   **y le faltaba uno que sí existe** (`RevisionAtendidaStep`). Verificado contra el código
   (`src/Sanic.Mppp.Plugins/Steps/*.cs`) y en vivo contra Dev el 2026-09-27: la integridad de una
   autorización duplicada la hace una **clave alternativa** (`sanic_mppp_key_autorizacionplan_autorizado_plan`),
   nunca un plugin — no existe ni existió ningún `IntegridadAutorizacionPlanStep`. Ver I-05.
2. **I-09 decía "10 tipos"; en vivo, el 2026-09-27, son 13** (8 clases de step + 5 Custom API,
   `GET plugintypes` filtrado por el ensamblado). Ver I-09. *(Nota 2026-10-06: desde DN-04, 2026-10-01, son 15 — se sumaron `CargarCatalogosApi` y `CambiarEstadoCatalogosApi`.)*
3. **Dos casos citaban un archivo ajeno** (`pruebas/README.md`, la deuda declarada del
   2026-09-27: "cuatro en `04`, **dos en `08`** y uno en `07`"): I-05 decía "una autorización
   existente (P-03, `00-preparacion.md`)" e I-07 decía "una Fila existente (la de C-01 alcanza,
   `01-clasificacion-del-correo.md`)". El **código** de los dos casos ya usaba su propio fixture
   (`sets/set_i.py::preparar()`); lo que estaba mal era solo la prosa de este MD, que ya no cita
   ningún otro archivo (CONVENCIONES §8).
4. **I-07 estaba mal PLANTEADO, no solo mal escrito** (corregido más tarde el mismo 2026-09-27):
   el "defecto confirmado" que este archivo declaraba se medía con el **service principal**
   (`applicationid`), una identidad **exenta de `ListaBlancaStep` por diseño** (`04` §1, código de
   servidor) — nunca con un humano. Probado en vivo con `User1` (Ejecutivo, impersonado con
   `MSCRMCallerID`): el `PATCH` a `sanic_referencia` **se rechaza** (HTTP 400). **No hay defecto.**
   Ver el detalle completo en I-07; `diseno/PENDIENTES.md` (entrada `I-07`) queda pendiente de que
   su dueño la cierre con esta evidencia.

**Además, siguiendo CONVENCIONES §7 ("nombrar, no contar")**: en vez de repetir la vieja tabla de
prosa "8 steps con su función", este archivo **nombra los 20 registros de step, uno por uno**, con
su mensaje, su etapa y su tabla, y los verifica contra `sdkmessageprocessingsteps` EN VIVO (I-10).
Un conteo envejece con cada step nuevo; una lista de nombres, no.

**Los 15 tipos de plugin registrados** (8 clases de step + 7 Custom API — las Custom API no son
steps y no se prueban en este archivo, las cubre `simulador.py`/`02`/`03`):

| Clase | Qué protege |
|---|---|
| `NombreCalculadoStep` | el nombre de las tablas que no lo digita nadie (hoy 4: Plan, AutorizacionPlan, AutorizacionCliente, Fila — I-06) |
| `NormalizarYValidarStep` | normaliza y valida al guardar (Cliente, Plan, Autorizado, Parametro) |
| `ListaBlancaStep` | que solo se escriba lo que se puede escribir (Fila, Solicitud) |
| `TransicionDeFilaStep` | que una transición de estado sea legal, y el step de claim (03 §2) |
| `PostTransicionDeFilaStep` | los efectos posteriores (Bitácora, cierre de la Solicitud) |
| `AtenderPorClasificarStep` | Atendido y Descartar sobre una Solicitud por clasificar |
| `IntegridadDeReglaStep` | que el catálogo de reglas sea coherente |
| `RevisionAtendidaStep` | deja en Bitácora el motivo que escribió la máquina, al usar "Revisado" |

**Fase 2** agregó, y este set ya los cubre: la 4ª tabla de `NombreCalculadoStep`
(`AutorizacionCliente`, I-06), la clave alternativa nueva `sanic_mppp_key_autorizacioncliente_autorizado_cliente`
(I-11, DN-01), la puerta de las Custom API del RPA (`PuertaDelRpa.cs`, I-12, D-9/D-44) y el
comportamiento del claim dentro de `TransicionDeFilaStep` (I-13, `03` §2).

**Cómo se prueban.** Casi todos, **haciendo algo prohibido** y verificando que el sistema se niegue
con un mensaje claro. Un plugin que deja pasar lo prohibido es peor que no tenerlo.

**Set autocontenido** (`CONVENCIONES.md` §8). Este archivo no depende de ningún otro: prepara sus
propios datos y no supone lo que dejó otro set. La preparación exacta que hace `preparar()`, y
por qué no hace otras cosas (roles de Ejecutivo/Supervisor, canon), está documentada en
`herramientas/pruebas/sets/set_i.py::preparar()` — acá solo el resumen:

| Qué prepara | Cómo |
|---|---|
| Datos de negocio (`PR*`, fixture compartido, `REMITENTE` autorizado sobre PR11/PR06/PR10/PRIN) | `preparacion_comun.verificar_datos_negocio()` |
| Los 15 Excel + el adjunto no-Excel | `preparacion_comun.verificar_excels()` |
| Roles de Ejecutivo (User1) y Supervisor (User2) — SOLO los necesita I-13; Ejecutivo (User1) y Administrador de planes (User3) — SOLO los necesita I-07 | resueltos y verificados DENTRO de cada caso, no en `preparar()` (si un día cambiaran, el resto de los 11 casos sigue corriendo igual) |

**Corré este set con**:

```bash
python3 herramientas/pruebas/correr.py I
```

---

### I-01 · El catálogo de reglas: una regla activa sin evaluador

**Qué prueba.** `IntegridadDeReglaStep` (D-13): una regla activa cuyo código no tiene un evaluador
programado para su nivel.

**Necesita.** Nada además de lo que `preparar()` de este set ya deja listo: el service principal
de Dev (System Administrator) alcanza para crear y borrar una Regla.

**Cómo.** `POST sanic_mppp_tbl_reglas` con `sanic_codigo = "ZZ_REGLA_QUE_NO_EXISTE"`, `sanic_nivel =
Registro`, `sanic_efecto = Rechaza`, `sanic_orden = 90`.

**Qué tiene que pasar.** El `POST` **no se guarda**: HTTP distinto de 200/201/204. El mensaje dice
que ese código no tiene evaluador programado para ese nivel.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py I` (caso `I-01`).

**Cómo se sabe que falló.** Si el `POST` da 200/201/204, `IntegridadDeReglaStep` no está frenando
este caso — el caso lo detecta y **borra la regla inventada de inmediato**, antes de que una
validación real la evalúe y reviente con una excepción real (todas las Solicitudes quedarían en
Ingresada).

**Automatizable.** API (`correr.py I`, caso `I-01`).

---

### I-02 · Una regla que depende de otra inactiva

**Qué prueba.** `IntegridadDeReglaStep`, el segundo caso de D-13: no se puede desactivar una regla
de la que dependen otras reglas activas.

**Necesita.** La regla `PLAN_EXISTE` del catálogo sembrado (`playbooks/datos/reglas.md`), de la
que dependen cuatro reglas activas.

**Cómo.** `PATCH sanic_mppp_tbl_reglas(<PLAN_EXISTE>)` con `statecode = Inactivo`.

**Qué tiene que pasar.** **No deja desactivarla**: HTTP distinto de 200/204. El mensaje nombra las
reglas que dependen de ella. Al terminar, `PLAN_EXISTE` queda **activa** (el caso la revierte en
un `finally`, pase lo que pase: de esto depende cualquier otra validación que corra en paralelo,
incluida la del otro agente en `pruebas/gui/`).

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py I` (caso `I-02`).

**Cómo se sabe que falló.** Si el `PATCH` da 200/204, el catálogo compartido de Reglas quedó con
`PLAN_EXISTE` inactiva — **hay que reactivarla a mano de inmediato** (`sanic_codigo eq
'PLAN_EXISTE'`, `statecode = 0`) antes de que cualquier otra validación la evalúe, porque el propio
caso reintenta reactivarla en su `finally` pero un FALLA acá significa que esa reactivación también
podría estar fallando.

**Automatizable.** API (`correr.py I`, caso `I-02`).

---

### I-03 · Una regla de nivel Registro con efecto "Envía a revisión"

**Qué prueba.** D-20: ese efecto solo es válido en las reglas del sobre (niveles Correo y
Solicitud), nunca en Registro.

**Necesita.** La regla `MONEDA_DEL_PLAN` (nivel Registro, efecto Rechaza) del catálogo sembrado.

**Cómo.** `PATCH sanic_mppp_tbl_reglas(<MONEDA_DEL_PLAN>)` con `sanic_efecto = Envía a revisión`.

**Qué tiene que pasar.** **No deja guardar**: HTTP distinto de 200/204, con un mensaje que nombra
el motivo. Al terminar, `MONEDA_DEL_PLAN` queda con efecto **Rechaza** (revertido en `finally`).

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py I` (caso `I-03`).

**Cómo se sabe que falló.** Si el `PATCH` da 200/204, `MONEDA_DEL_PLAN` quedó con un efecto que el
motor de validación (D-20) no sabe interpretar en nivel Registro — **hay que revertirla a mano**
(`sanic_efecto = Rechaza`) antes de correr cualquier caso de `03-reglas-de-registro.md` que la use.

**Automatizable.** API (`correr.py I`, caso `I-03`).

---

### I-04 · Una regla que depende de sí misma

**Qué prueba.** La autodependencia se rechaza **por el código** (comparación de `sanic_codigo`),
antes de mirar ningún `sanic_orden` — el defecto que encontró la revisión de código del
2026-09-22: un `Update` que sube el orden Y agrega la autodependencia en el mismo guardado no podía
usar "orden estrictamente menor" como única defensa, porque el catálogo leído todavía trae el orden
VIEJO de esa misma regla.

**Necesita.** La regla `MONEDA_DEL_PLAN` del catálogo sembrado.

**Cómo.** `PATCH sanic_mppp_tbl_reglas(<MONEDA_DEL_PLAN>)` con `sanic_dependede =
"MONEDA_DEL_PLAN"` (su propio código).

**Qué tiene que pasar.** **No deja guardar**: *"No se puede guardar la regla 'MONEDA_DEL_PLAN': no
puede depender de sí misma."* Al terminar, `sanic_dependede` queda como estaba (revertido en
`finally`).

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py I` (caso `I-04`).

**Cómo se sabe que falló.** Si el `PATCH` da 200/204, la regla quedó con un ciclo de tamaño uno en
`sanic_dependede` — **hay que revertirla a mano** a su valor original antes de que el motor de
validación intente resolver sus dependencias y entre en un ciclo.

**Automatizable.** API (`correr.py I`, caso `I-04`).

---

### I-05 · Integridad de una autorización por PLAN (duplicada)

**Qué prueba.** La **clave alternativa** `sanic_mppp_key_autorizacionplan_autorizado_plan` sobre
AutorizacionPlan. **No hay ningún `IntegridadAutorizacionPlanStep`** (esa clase nunca existió en el
código: ver la corrección al principio de este archivo) — la integridad de una autorización
duplicada la garantiza la clave, no un plugin.

**Necesita.** El `REMITENTE` de este set, ya autorizado sobre `PR11` por `preparar()` de este mismo
set (`preparacion_comun.verificar_datos_negocio`, que siembra el fixture `PR*` — no depende de
ningún otro archivo del plan).

**Cómo.** `POST sanic_mppp_tbl_autorizacionplans` con el **mismo** autorizado (`REMITENTE`) y el
**mismo** plan (`PR11`) que ya tiene autorizado.

**Qué tiene que pasar.** **No deja guardar** (por la clave alternativa): HTTP distinto de
200/201/204, con un mensaje entendible y **no** un volcado técnico de la plataforma
(`Microsoft.Crm.CrmException` no debería aparecer en el texto).

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py I` (caso `I-05`).

**Cómo se sabe que falló.** Si el `POST` da 200/201/204, la clave alternativa no está frenando el
duplicado — el caso la borra de inmediato. Si el `POST` se rechaza pero el mensaje trae
`Microsoft.Crm.CrmException` (o un volcado similar), el rechazo funciona pero expone un mensaje que
un Administrador de planes no puede leer.

**Automatizable.** API (`correr.py I`, caso `I-05`).

---

### I-06 · El nombre calculado: Plan, AutorizacionPlan, AutorizacionCliente y Fila

**Qué prueba.** `NombreCalculadoStep`, en **las cuatro tablas que cubre hoy** — nombradas, no
contadas (CONVENCIONES §7): `Plan`, `AutorizacionPlan`, `AutorizacionCliente` (agregada en fase 2,
DN-01) y `Fila`.

**Necesita.** El Cliente (`prep.CLIENTE`) y el Autorizado (`REMITENTE`) que `preparar()` de este
set deja listos; el plan `PRNA` del fixture (existe, pero el `REMITENTE` de este set **no** está
autorizado sobre él — a propósito, por `preparar_datos.py`); una Solicitud propia de este caso
(`ctx.procesar()` con `E01-feliz-formato11.xlsx`) para la Fila.

**Cómo.**

1. Crea un **Plan** (`sanic_codigo = "ZZNM"`) sin tocar `sanic_nombre`.
2. Crea una **AutorizacionPlan** (`REMITENTE` → `PRNA`) sin tocar `sanic_nombre`.
3. Crea una **AutorizacionCliente** (`REMITENTE` → `prep.CLIENTE`) sin tocar `sanic_nombre`.
4. Procesa una Solicitud con 3 filas (`E01-feliz-formato11.xlsx`) y lee el `sanic_nombre` de cada
   Fila.

**Qué tiene que pasar.**

| Tabla | Nombre calculado esperado |
|---|---|
| Plan | `ZNM - PR - Pruebas MPPP SA` (`CODIGO - Cliente`) |
| AutorizacionPlan | `<REMITENTE> → PRNA` (`correo → CODIGO del plan`) |
| AutorizacionCliente | `<REMITENTE> → PR - Pruebas MPPP SA` (`correo → NOMBRE del cliente`, no el código: no tiene) |
| Fila (cada una de las 3) | `<MPPP-########>-F01`, `-F02`, `-F03` (`<solicitud>-F<NN>`, dos dígitos como mínimo) |

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py I` (caso `I-06`).

**Cómo se sabe que falló.** Distinguí CUÁL de las cuatro tablas falló por el nombre del check (cada
una se reporta por separado, con el valor obtenido): si es **Plan** o **AutorizacionPlan**, es una
regresión en código ya viejo (fase 1); si es **AutorizacionCliente**, es la parte nueva de fase 2
(DN-01) la que no quedó bien enchufada al step; si es **Fila**, el problema no es de esta fase — es
el mismo cálculo que usa TODO el resto del plan para citar el número de fila.

**Automatizable.** API (`correr.py I`, caso `I-06`).

---

### I-07 · Lista blanca de escrituras (columna calculada por PATCH directo)

**Reescrito el 2026-09-27: el diagnóstico viejo medía la identidad equivocada, no es un defecto.**
Ver el detalle completo de la investigación y la evidencia abajo. **`diseno/PENDIENTES.md` (entrada
`I-07`) todavía dice "ABIERTA": ese archivo es de otro dueño en esta ronda y no se tocó — hace
falta que quien lo mantiene la cierre con la evidencia de acá.**

**Qué prueba.** `ListaBlancaStep` (`04` §1): que un **humano**, con su rol de negocio, no pueda
escribir a mano por Web API una columna que solo llena la validación (`sanic_referencia`) —
saltándose el formulario de solo lectura. Y de paso, sus dos bordes: que la protección que en
verdad frena a un rol sin privilegio de escritura sobre Fila es otra (04 §2, no la lista blanca), y
que una columna que SÍ está permitida (`sanic_notainterna`) no se vea afectada.

**Por qué el planteo viejo estaba mal.** La versión anterior de este caso hacía el `PATCH`
prohibido con el **service principal** de este set (`# PowerPlatform-Dev`, `dataverse_api.py`),
que tiene `applicationid`. `ListaBlancaStep.Execute` (`src/Sanic.Mppp.Plugins/Steps/StepsDePlataforma.cs`)
empieza así:

```csharp
if (BaseDeStep.EsCodigoDeServidor(contexto, servicio))
{
    // 04 §1: el código de servidor no pasa por la lista blanca.
    return;
}
```

y `BaseDeStep.EsCodigoDeServidor` trata como "código de servidor" a **toda identidad con
`applicationid`**, no solo a SYSTEM — es exactamente lo que dice `diseno/04-matriz-privilegios.md`
§1 ("Las dos claves alternativas..." — la exención de identidad de aplicación es la misma que ya
usa I-12 para la puerta del RPA). El `HTTP 204` que el caso viejo registraba como "defecto" era
el sistema haciendo lo que el diseño pide: el service principal **tiene que** poder escribir
cualquier columna, porque es código de servidor a todos los efectos. El diagnóstico viejo nunca
midió lo que decía medir. Y `ReglasDeSteps.cs` ya lo advertía desde el 2026-09-23 (comentario sobre
`PermitidasEnFila`): *"todos los actores probados hasta entonces quedaban exentos de la lista
blanca (el service principal por su `applicationid`, el administrador por ser `servicio.cuenta`,
los plugins por su `Depth`). El primer humano que escribió una Fila desde la app chocó con esto de
entrada."* Nadie había probado la lista blanca con un humano hasta ahora.

**Necesita.** Una Fila propia de este caso (`ctx.procesar()` con `E01-feliz-formato11.xlsx`) y los
roles de Ejecutivo (`User1`) y Administrador de planes (`User3`), verificados EN VIVO contra
`systemuserroles_association` dentro del propio caso (mismo criterio que I-13: si el rol no
coincide, el caso da **N-A**, no un resultado que mentiría sobre lo que se probó).

**Cómo (4 pasos, verificados en el entorno el 2026-09-27, `MSCRMCallerID` para impersonar).**

| # | Quién | Qué | Qué pasó | Interpretación |
|---|---|---|---|---|
| 1 | Service principal (`applicationid`, sin impersonar) | `PATCH sanic_referencia` = `"ZZ-INTENTO-SP"` | **HTTP 204** — se guardó | Esperado: exento por diseño (código de servidor). **No mide la lista blanca.** |
| 2 | **`User1` (Ejecutivo, humano)** | `PATCH sanic_referencia` = `"ZZ-INTENTO-DIRECTO"` | **HTTP 400**: *"No se puede guardar: la(s) columna(s) sanic_referencia no está(n) permitida(s) para este usuario."* | **La lista blanca FUNCIONA.** Un humano no puede saltarse el formulario. |
| 3 | `User3` (Administrador de planes, borde 1) | `PATCH sanic_notainterna` = `"prueba de borde I-07"` | **HTTP 403**: *"...is missing prvReadsanic_mppp_tbl_fila privilege..."* | Lo frena la **falta de privilegio** (04 §2) — nunca llega a `ListaBlancaStep`. Protección distinta, y actúa antes. |
| 4 | `User1` (Ejecutivo, borde 2) | `PATCH sanic_notainterna` = `"prueba I-07 columna permitida"` | **HTTP 204** — se guardó | La columna permitida **sí** pasa: el step no es más restrictivo que lo que dice el diseño. |

**Qué tiene que pasar.** El paso 2 (el que en verdad mide `ListaBlancaStep` contra un humano) tiene
que dar HTTP 400 con un mensaje que nombre `sanic_referencia`. Los pasos 1, 3 y 4 son de control:
confirman que la exención de diseño existe, que el borde de privilegio actúa antes que la lista
blanca, y que la columna permitida no se ve afectada.

**Conclusión: NO ES UN DEFECTO. I-07 pasa a OK.** `ListaBlancaStep` protege exactamente lo que
`04` §1 dice que protege, contra la identidad que en verdad importa (un humano con rol de negocio).
El defecto que se había registrado en `diseno/PENDIENTES.md` (entrada `I-07`) era un artefacto de
probar con la identidad equivocada — corresponde cerrar esa entrada con esta evidencia.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py I` (caso `I-07`) — tiene que dar
**OK**. Cuatro checks: el service principal exento (204, informativo), el Ejecutivo humano
rechazado (400, con `sanic_referencia` en el mensaje), el Administrador de planes frenado por
privilegio (401/403) y el Ejecutivo escribiendo la columna permitida (204).

**Cómo se sabe que falló.** Si el paso 2 (Ejecutivo) da 200/201/204, la lista blanca **sí** tiene
un agujero real con un humano — eso sería grave (cualquier Ejecutivo con acceso al Web API podría
escribir `sanic_referencia` a mano) y hay que reabrir la investigación con el `plugintracelog`. Si
el paso 3 (Administrador de planes) da otro HTTP que no sea 401/403 (por ejemplo 400 con el mensaje
de la lista blanca), significa que SÍ tiene privilegio de escritura sobre Fila cuando no debería
(revisar la matriz `04` §2 contra el entorno). Si el paso 4 (columna permitida) se rechaza, el step
es más restrictivo que el diseño y hay que revisar `ReglasDeSteps.PermitidasEnFila`.

**Automatizable.** API (`correr.py I`, caso `I-07`).

---

### I-08 · Transición de estado ilegal (Validada → Aprobada, saltando Digitada)

**Qué prueba.** `TransicionDeFilaStep`: un salto que no existe en la tabla de transiciones
(`Dominio/TransicionesDeFila.cs`) se rechaza **siempre**, sea cual sea el rol de quien lo intenta —
`(Validada, Aprobada)` no es una clave de esa tabla, así que la respuesta es "Transición no
permitida" antes incluso de mirar roles.

**Necesita.** Una Fila propia de este caso, en Validada (`ctx.procesar()` con
`E01-feliz-formato11.xlsx`).

**Cómo.** `PATCH` directo por Web API a `sanic_estado = Aprobada` sobre una Fila en Validada
(saltando Digitada), sin pasar por ningún botón.

**Qué tiene que pasar.** **No deja**: HTTP 400, con un mensaje que dice que esa transición no es
válida desde ese estado.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py I` (caso `I-08`).

**Cómo se sabe que falló.** Si el `PATCH` da 200/204, **la seguridad no estaba en el botón**:
cualquiera con acceso al Web API y privilegio de escritura sobre Fila puede aprobar lo que quiera,
saltándose el circuito completo de digitación. Es el caso más grave de todo este archivo si falla.

**Automatizable.** API (`correr.py I`, caso `I-08` — el caso que demuestra que la seguridad no está
en el botón).

---

### I-09 · El paquete de plugins está al día

**Qué prueba.** Que el ensamblado en el sandbox tenga registrados exactamente los tipos de plugin
del repositorio — **nombrados, no contados** (CONVENCIONES §7: la versión anterior de este caso
decía "10 tipos"; hoy hay 15, desde DN-04).

**Necesita.** Credenciales de lectura en Dataverse (las que ya usa `preparar()` de este set).

**Cómo.**

```bash
python3 herramientas/construir/paquete_plugins.py playbooks/paquete-plugins/sanic_mppp_pkg_plugins.md --solo-verificar
```

y además, dentro del caso, `GET plugintypes` filtrado por el ensamblado, para nombrar cada uno.

**Qué tiene que pasar.** `"estado": "ya_existia"` (la herramienta ya compara por NOMBRE lo que
falta y lo que sobra — D-57 c, no un conteo), **y** cada uno de estos 15 tipos presente en
`plugintypes`, **sin ninguno de más**:

`Sanic.Mppp.Plugins.Steps.ListaBlancaStep` · `NormalizarYValidarStep` · `NombreCalculadoStep` ·
`TransicionDeFilaStep` · `PostTransicionDeFilaStep` · `AtenderPorClasificarStep` ·
`RevisionAtendidaStep` · `IntegridadDeReglaStep` — y `Sanic.Mppp.Plugins.Api.ClasificarCorreoApi` ·
`ExportarParaDigitarApi` · `ObtenerFilasPendientesApi` · `RegistrarResultadoRpaApi` ·
`ValidarSolicitudApi` · `CargarCatalogosApi` · `CambiarEstadoCatalogosApi` (estas dos, desde DN-04, 2026-10-01).

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py I` (caso `I-09`).

**Cómo se sabe que falló.** Si falta un nombre puntual, ESE es el tipo que no se subió al sandbox
(revisar el `.nupkg`, no todo el paquete). Si "sobra" un nombre, es una derivación que el playbook
no declara (D-57 c: pasó una vez con `RevisionAtendidaStep`) — no es necesariamente un defecto,
pero hay que decidir si se agrega al playbook o se borra del entorno. **Cuidado con la trampa
conocida**: el sandbox puede seguir corriendo el binario viejo si no se subió el número de build; el
síntoma es *"The plug-in type could not be found in the plug-in assembly"*.

**Automatizable.** API (`paquete_plugins.py --solo-verificar`; `correr.py I`, caso `I-09`).

---

### I-10 · Los 20 registros de step de plugin, uno por uno

**Qué prueba.** Que los **20 registros** de `playbooks/steps/sanic_mppp_pkg_plugins.md` (inventario
8.2) existan en vivo, **nombrados uno por uno**, cada uno con su mensaje, su tabla y su etapa
exactos — no un conteo (CONVENCIONES §7). Reemplaza a la vieja tabla de prosa "8 steps registrados"
de este archivo, que solo describía qué protege cada CLASE sin verificar nada contra el entorno.

**Necesita.** Credenciales de lectura en Dataverse.

**Cómo.** Por cada uno de los 20 nombres de `playbooks/steps/sanic_mppp_pkg_plugins.md` §2, `GET
sdkmessageprocessingsteps?$filter=name eq '<nombre>'` con `$expand` de `sdkmessageid` (el mensaje) y
`sdkmessagefilterid` (la tabla).

**Qué tiene que pasar.** Los 20, cada uno **habilitado** (`statecode = 0`) y con su mensaje, su
tabla y su etapa exactos:

| Step (nombre exacto) | Mensaje | Tabla | Etapa |
|---|---|---|---|
| `MPPP - Lista blanca - Update de Fila` | Update | `sanic_mppp_tbl_fila` | PreOperation |
| `MPPP - Lista blanca - Update de Solicitud` | Update | `sanic_mppp_tbl_solicitud` | PreOperation |
| `MPPP - Normalizar y validar - Create de Cliente` | Create | `sanic_mppp_tbl_cliente` | PreOperation |
| `MPPP - Normalizar y validar - Update de Cliente` | Update | `sanic_mppp_tbl_cliente` | PreOperation |
| `MPPP - Normalizar y validar - Create de Plan` | Create | `sanic_mppp_tbl_plan` | PreOperation |
| `MPPP - Normalizar y validar - Update de Plan` | Update | `sanic_mppp_tbl_plan` | PreOperation |
| `MPPP - Normalizar y validar - Create de Autorizado` | Create | `sanic_mppp_tbl_autorizado` | PreOperation |
| `MPPP - Normalizar y validar - Update de Autorizado` | Update | `sanic_mppp_tbl_autorizado` | PreOperation |
| `MPPP - Normalizar y validar - Create de Parametro` | Create | `sanic_mppp_tbl_parametro` | PreOperation |
| `MPPP - Normalizar y validar - Update de Parametro` | Update | `sanic_mppp_tbl_parametro` | PreOperation |
| `MPPP - Nombre calculado - Create de Plan` | Create | `sanic_mppp_tbl_plan` | PreOperation |
| `MPPP - Nombre calculado - Create de Autorizacionplan` | Create | `sanic_mppp_tbl_autorizacionplan` | PreOperation |
| `MPPP - Nombre calculado - Create de Autorizacioncliente` | Create | `sanic_mppp_tbl_autorizacioncliente` | PreOperation |
| `MPPP - Nombre calculado - Create de Fila` | Create | `sanic_mppp_tbl_fila` | PreOperation |
| `MPPP - Transicion de Fila - Update` | Update | `sanic_mppp_tbl_fila` | PreOperation |
| `MPPP - Post transicion de Fila - Update` | Update | `sanic_mppp_tbl_fila` | **PostOperation** |
| `MPPP - Atender correo por clasificar - Update de Solicitud` | Update | `sanic_mppp_tbl_solicitud` | PreOperation |
| `MPPP - Revision atendida - Update de Solicitud` | Update | `sanic_mppp_tbl_solicitud` | PreOperation |
| `MPPP - Integridad de Regla - Create` | Create | `sanic_mppp_tbl_regla` | PreOperation |
| `MPPP - Integridad de Regla - Update` | Update | `sanic_mppp_tbl_regla` | PreOperation |

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py I` (caso `I-10`).

**Cómo se sabe que falló.** El check nombra el step exacto que no coincide (no aparece, o aparece
con otro mensaje/tabla/etapa/deshabilitado): ESE es el que hay que revisar en el entorno, sin tener
que adivinar cuál de los 20.

**Automatizable.** API (`correr.py I`, caso `I-10`).

---

### I-11 · Integridad de una autorización por CLIENTE (duplicada, fase 2 DN-01)

**Qué prueba.** La clave alternativa nueva `sanic_mppp_key_autorizacioncliente_autorizado_cliente`
sobre AutorizacionCliente (inventario 5.11, DN-01) — la vía por cliente agregada en fase 2, hermana
de la que prueba I-05 para AutorizacionPlan.

**Necesita.** El Cliente (`prep.CLIENTE`) y el Autorizado (`REMITENTE`) que `preparar()` de este set
deja listos.

**Cómo.** Crea una **AutorizacionCliente** (`REMITENTE` → `prep.CLIENTE`); después intenta crear
una **segunda**, con el mismo autorizado y el mismo cliente.

**Qué tiene que pasar.** La primera se crea sin problema. La segunda **no deja guardar** (por la
clave alternativa): HTTP distinto de 200/201/204, con un mensaje entendible y **no** un volcado
técnico de la plataforma.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py I` (caso `I-11`).

**Cómo se sabe que falló.** Si la precondición (la primera AutorizacionCliente) no se pudo crear,
el caso lo reporta como tal, sin llegar a probar nada más. Si la SEGUNDA se guarda (HTTP
200/201/204), la clave alternativa de fase 2 no está frenando el duplicado — el caso la borra de
inmediato. Si se rechaza pero con un volcado técnico crudo, el rechazo funciona pero el mensaje no
sirve para un Administrador de planes.

**Automatizable.** API (`correr.py I`, caso `I-11`).

---

### I-12 · La puerta del RPA rechaza a quien no es el bot (fase 2, D-9/D-44)

**Qué prueba.** `PuertaDelRpa.Exigir` (`src/Sanic.Mppp.Plugins/Steps/PuertaDelRpa.cs`): las dos
Custom API de fase 2 (`sanic_mppp_capi_obtenerfilaspendientes`,
`sanic_mppp_capi_registrarresultadorpa`) llevan `ExecutePrivilegeName` **vacío a propósito** (D-9);
la puerta es el código, con dos condiciones obligatorias: identidad de aplicación **y** rol `SR -
MPPP - RPA`.

**Necesita.** Nada además de las credenciales que ya usa este set. El service principal de Dev (`#
PowerPlatform-Dev`) **es** una identidad de aplicación (tiene `applicationid`), pero **solo tiene
el rol `System Administrator`** — verificado en vivo el 2026-09-27 contra
`systemuserroles_association` — así que es exactamente el llamador que la puerta tiene que
rechazar. No hace falta (ni se usan) las credenciales reales del bot (`local/rpa_cred.env`).

**Cómo.** `GET sanic_mppp_capi_obtenerfilaspendientes(claimminutos=5)` con las credenciales del
service principal de este set.

**Qué tiene que pasar.** **No deja pasar**: HTTP 400, con un mensaje que trae el código
`MPPP-RPA-006` (identidad de aplicación sin el rol) — o `MPPP-RPA-007` si algún día el rol no
existiera en el entorno. Ninguna Fila se toca: la puerta corre antes de cualquier lógica de
negocio.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py I` (caso `I-12`).

**Cómo se sabe que falló.** Si el `GET` da 200 (la Custom API respondió filas), la puerta no está
frenando y **cualquier identidad de aplicación sin el rol puede reservar y leer filas pendientes de
digitar** — hay que revisar de inmediato si además reservó un claim sobre filas reales (columna
`sanic_claimhasta`) y liberarlas a mano. Si el HTTP es 400 pero el mensaje no trae ninguno de los
dos códigos `MPPP-RPA-0*`, el rechazo funciona pero por otro motivo (por ejemplo, un parámetro mal
formado) — no confirma que la puerta sea la que frenó.

**Automatizable.** API (`correr.py I`, caso `I-12`).

---

### I-13 · El claim se libera en un estado terminal, y no antes (fase 2, `03` §2)

**Qué prueba.** El comportamiento del claim dentro de `TransicionDeFilaStep`: mientras la Fila
sigue en un estado "abierto" (Validada, Digitada) el claim **no se toca**; al llegar a un estado
terminal (Aprobada, Rechazada en AS400, Anulada) el claim **se libera** (`sanic_claimhasta =
null`), sea cual sea quien hizo la transición.

**Necesita.** Los roles de Ejecutivo (`User1`) y Supervisor (`User2`), verificados EN VIVO contra
`systemuserroles_association` dentro de este mismo caso (no en `preparar()`: es el único caso de
todo el set que impersona un rol de negocio — CONVENCIONES §8, "no supuesto"); una Fila propia de
este caso, en Validada (`ctx.procesar()` con `E01-feliz-formato11.xlsx`).

**Cómo.**

1. `PATCH` directo (service principal, sin impersonar) de `sanic_claimhasta` a un instante **5
   minutos en el pasado** (ya vencido) — esta columna no está en la lista blanca de una persona,
   solo la escribe código de servidor.
2. Impersonando a **Ejecutivo** (`MSCRMCallerID`), `PATCH sanic_estado = Digitada` (Validada →
   Digitada).
3. Impersonando a **Supervisor** (otro usuario: segregación de funciones), `PATCH sanic_estado =
   Aprobada` (Digitada → Aprobada).
4. Lee `sanic_claimhasta` después de cada paso.

**Qué tiene que pasar.**

| Paso | `sanic_claimhasta` |
|---|---|
| Después de sembrar el claim vencido | el valor sembrado (pasado) |
| Después de Validada → Digitada | **el mismo valor** (sin tocar: sigue "abierta") |
| Después de Digitada → Aprobada | **vacío** (liberado: es terminal) |

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py I` (caso `I-13`).

**Cómo se sabe que falló.** Si el claim se BORRA en Validada → Digitada, el step está liberando de
más (un bot que reservó una fila para trabajarla perdería la reserva a mitad de camino). Si el
claim SIGUE presente después de Digitada → Aprobada, el step no libera en un estado terminal, y una
fila ya aprobada quedaría "reservada" para siempre, bloqueando cualquier lectura futura del bot
sobre esa fila. Si User1/User2 no tienen los roles esperados, el caso da **N-A** (no FALLA): con
otros roles dan 400 por falta de rol y el resultado mentiría sobre lo que en realidad se probó.

**Automatizable.** API (`correr.py I`, caso `I-13`).

---

### Resultados

| Caso | Resultado | Notas |
|---|---|---|
| I-01 regla sin evaluador | | |
| I-02 dependencia inactiva | | |
| I-03 efecto inválido por nivel | | |
| I-04 autodependencia | | |
| I-05 autorización por plan duplicada | | |
| I-06 nombre calculado (4 tablas) | | |
| **I-07 lista blanca (humano vs. identidad exenta)** | | **OK — no es defecto (reescrito 2026-09-27); falta que se cierre `diseno/PENDIENTES.md` entrada I-07** |
| **I-08 transición ilegal** | | |
| I-09 paquete al día (15 tipos) | | |
| I-10 los 20 steps, uno por uno | | |
| I-11 autorización por cliente duplicada (fase 2) | | |
| I-12 puerta del RPA (fase 2) | | |
| I-13 claim: no toca / libera (fase 2) | | |

> Dejá el catálogo de **Reglas** como estaba: `PLAN_EXISTE` activa, `MONEDA_DEL_PLAN` con efecto
> Rechaza, y sin la regla inventada de I-01. Los propios casos lo revierten en `finally`; esta nota
> es para quien inspeccione el entorno después de una corrida interrumpida (`Ctrl-C`, timeout).

---
## 09 — Fase 2: el bot RPA (custom API, claim, FIFO, catálogo de errores)

Cubre lo que **no tiene un solo caso escrito**: `sanic_mppp_capi_obtenerfilaspendientes`,
`sanic_mppp_capi_registrarresultadorpa`, la puerta D-9 (`Steps/PuertaDelRpa`), el claim con
vencimiento, el orden FIFO, el tope de lote, la tolerancia de fecha, el catálogo `MPPP-RPA-00x` y
la marca de atención manual. Fuente: `diseno/03-contratos-custom-api.md` §2-§3,
`diseno/FASE2-ESTADO.md`, y el código en `src/Sanic.Mppp.Plugins/Steps/PuertaDelRpa.cs`,
`Dominio/SeleccionParaElRpa.cs`, `Dominio/ReglasDelRpa.cs`, `Dominio/CatalogoDeCodigosRpa.cs`,
`Api/ObtenerFilasPendientes*.cs`, `Api/RegistrarResultadoRpa*.cs`.

### Convención de IDs de este archivo y de los que siguen

Prefijos ya usados por `00`–`08` (verificado el 2026-09-27 con
`rg -o --no-filename '^## [A-Z]+-[0-9]+' pruebas/0*.md`): **A, C, G, I, M, P, R, S, V**. Los archivos
nuevos (`09`–`11`) usan prefijos que no colisionan con esos ni entre sí:

| Prefijo | Archivo | Tema |
|---|---|---|
| `B-` (**B**ot) | `09-rpa-fase2.md` | este archivo |
| `U-` (a**U**torización) | `10-autorizacion-cliente.md` | DN-01 |
| `E-` (**E**xportación) | `11-exportacion-excel.md` | DN-02 |

Hasta el 2026-09-28 había, además, un set `O-` (Obligatoriedad, DN-03,
`12-obligatoriedad-gestion.md`) y un set `H-` (Hoy, `13-construido-hoy.md`) — los dos nombrados por
la directiva o la fecha que los originó, no por lo que probaban. Ese día se fusionaron: `O` dentro
de `03-reglas-de-registro.md` (set `R`, casos `R-19`..`R-28` — la obligatoriedad por gestión ES una
de las ocho reglas de Registro) y `H` repartido entre `05-app-y-comandos.md` (`A-15`) y
`06-seguridad-vistas-formularios.md` (`G-14`..`G-19`). Ver `CONVENCIONES.md` §4, "La regla del
nombre", y la tabla de equivalencias en `pruebas/README.md`.

### Herramientas de este archivo

| Herramienta | Para qué | Identidad que usa |
|---|---|---|
| `herramientas/pruebas/como_rpa.py` | solo verifica que la puerta D-9 deje pasar | el bot (`local/rpa_cred.env`) |
| `herramientas/pruebas/probar_rpa.py [--maximo=N] [--claimminutos=N] [--confirmar]` | ciclo completo, para correr a mano | el bot (`local/rpa_cred.env`) |
| `herramientas/pruebas/sets/set_b.py` | **la automatización de este archivo** (`correr.py B`): reusa `probar_rpa.Bot` para llamar como el bot, con asserts en vez de `print()` | el bot, y el service principal para todo lo demás |
| `herramientas/pruebas/ver_solicitud.py <Solicitud\|--ultima\|--parametros>` | inspección de solo lectura | service principal |
| `herramientas/dataverse_api.py GET\|POST "<recurso>" '<json>'` | llamadas directas (roles, parámetros, o para provocar un rechazo con una identidad que NO es el bot) | service principal |

Ninguna de estas herramientas se edita ni se ejecuta desde acá: los comandos de abajo son los que
**quien corra la prueba a mano** tiene que teclear. La corrida automatizada la hace
`herramientas/pruebas/sets/set_b.py` (`python3 herramientas/pruebas/correr.py B`).

> **`actuar_fila.py` NO sirve para los casos que impersonan "una persona" contra el claim o la
> segregación de funciones** (B-07, B-12, B-18, B-20). Esa herramienta actúa siempre como el service
> principal de `local/pp_secrets.env`, y ese service principal — verificado en vivo el 2026-09-27
> (`# PowerPlatform-Dev`, con `WhoAmI` + `systemuserroles_association`) — **solo tiene el rol
> `System Administrator`**, ningún rol de negocio. `actuar_fila.py` exige el rol de negocio
> (`SR - MPPP - Ejecutivo`/`Supervisor`) ANTES de escribir nada, así que con el service principal esos
> comandos fallan con "necesita el rol ... y no lo tiene" — nunca llegan a chocar contra el claim ni
> contra la segregación, que es justo lo que esos casos quieren ver. La forma correcta, ya usada en
> `08-integridad-y-plugins.md` (I-13), es impersonar directo a un usuario real con el rol
> (`local/sesion-ejecutivo.json` es `User1` = Ejecutivo; `local/sesion-supervisor.json` es `User2` =
> Supervisor) con la cabecera `MSCRMCallerID` sobre una llamada del service principal — verificando
> antes el rol contra `systemuserroles_association`, nunca supuesto. Los casos de abajo ya lo dicen
> así; `set_b.py` lo implementa así.

### Preparativos comunes a todo este archivo

Los deja listos `set_b.py` por su cuenta (CONVENCIONES §8): datos de negocio (fixture `PR*` +
remitente propio de este set), los 15 Excel, la autenticación del bot (`local/rpa_cred.env`, nunca
leído ni impreso), y User1/User2 verificados por rol. **Este archivo no depende de haber corrido
antes `01`/`02`/`03` ni ningún otro set** — cada caso arma su propia Solicitud con
`E01-feliz-formato11.xlsx` (3 Filas Validada, misma `fechavalidada`: sirve tanto para el desempate de
FIFO como para armar lotes). Eso reemplaza lo que decía la versión anterior de esta sección ("correr
cualquier caso de `01`/`02`/`03`"), que violaba CONVENCIONES §8 (un set no depende de otro).

Para correr un caso suelto A MANO (fuera de `set_b.py`) sigue haciendo falta:

- Rol `SR - MPPP - RPA` instalado (inventario 6.7) y app user `# MPP-RPA` con ese rol asignado
  (13.5), con sus credenciales en `local/rpa_cred.env` (no se toca ni se lee acá).
- `local/pp_secrets.env` con una identidad de service principal para `dataverse_api.py`.
- Al menos una Solicitud con Filas en **Validada** (crear filas con `dataverse_api.py`, o correr
  `set_b.py`, que las arma sola). Varios casos de acá abajo piden **más de una** fila Validada a la
  vez: usar un Excel con varias filas (`E01-feliz-formato11.xlsx` trae 3).
- Parámetros sembrados: `rpa.horas.tolerancia`, `rpa.minutos.claimmaximo`, `rpa.lote.maximo` (canon
  **100**, CONVENCIONES §8.2 — confirmar con `ver_solicitud.py --parametros` antes de asumirlo:
  distinto del tope DURO de 500 de `maximo` en `obtenerfilaspendientes`, que no es un parámetro, está
  fijo en el código), `rpa.codigos` (con al menos `OK` y `RECH-GEN`), `rpa.puedeaprobar`.
- Datos ficticios, descartables con `zz`/`ZZ`. Ninguna fila con datos reales de clientes.

---

#### B-01 — Rol del RPA no instalado en el entorno (MPPP-RPA-007)

**Qué prueba.** `PuertaDelRpa.Exigir` comprueba PRIMERO que el rol `SR - MPPP - RPA` exista antes de
evaluar nada más, y si no existe lo dice con `MPPP-RPA-007` en vez de un "no autorizado" genérico
— la confusión que costó horas el 2026-09-22 (rol nunca creado vs. permisos).

**Necesita.** Acceso de **plataforma** a Seguridad → Roles de seguridad (System
Administrator/Customizer de Dataverse) — esto **no** es el rol de negocio `SR - MPPP -
Administrador tecnico` que prueba `local/sesion-admin_tecnico.json`: ese rol está acotado a
Parámetros y Reglas dentro de la app (`04-matriz-privilegios.md`) y no incluye administrar roles de
seguridad de la plataforma. Anotado el nombre exacto y el `roleid` vigente de `SR - MPPP - RPA`
para poder restaurarlo; una ventana de Dev donde nadie más esté corriendo pruebas de fase 2 (este
caso apaga la puerta para **todo** el entorno mientras dura).

> **DECISIÓN (2026-09-27, migración a `set_b.py`): este caso queda SIEMPRE N-A en la corrida
> automatizada, sin excepción.** Rompe temporalmente el acceso del bot real para TODO Dev, y esta
> migración corrió con **cuatro agentes automatizados en paralelo sobre el mismo entorno compartido**
> — no hay ventana segura para tomarlo, ni siquiera "rápido": cualquier otro set que en ese momento
> llame a una Custom API del RPA (el propio `set_b.py` incluido) se rompería sin que sea un defecto
> del sistema. Ya existe cobertura sin este riesgo: `PuertaDelRpaAceptacion` (8 pruebas unitarias, 7
> de rechazo, en verde — `diseno/FASE2-ESTADO.md`) prueba esta rama con un doble en memoria — el mismo
> precedente que ya usó I-12 para no repetir un riesgo de entorno sin necesidad. Si alguna vez hace
> falta la confirmación end-to-end contra Dev de verdad, es una operación MANUAL, coordinada con quien
> más esté usando el entorno en ese momento, nunca parte de una corrida automática.

**Cómo.**
1. `dataverse_api.py GET "roles?$filter=name eq 'SR - MPPP - RPA'&$select=roleid,name"` y anotar el
   `roleid`.
2. Renombrar el rol temporalmente (agregar sufijo, p. ej. `SR - MPPP - RPA.bak`) — **nunca
   eliminarlo**.
3. `python3 herramientas/pruebas/como_rpa.py`.
4. Restaurar el nombre original **de inmediato**, incluso si el paso 3 falló.

**Qué tiene que pasar.** `como_rpa.py` informa que NO pasa la puerta; una llamada a cualquiera de
las dos Custom API devuelve `MPPP-RPA-007` citando el nombre del rol.

**Cómo se verifica.** La salida de `como_rpa.py`, y el mensaje de error de una llamada directa
(`dataverse_api.py POST sanic_mppp_capi_obtenerfilaspendientes ...`).

**Cómo se sabe que falló.** Si el mensaje dice `MPPP-RPA-006` ("no autorizado") en vez de
`MPPP-RPA-007` ("el rol no está creado"), la comprobación del rol no corrió primero: es la MISMA
confusión de horas del 2026-09-22, reaparecida.

**Automatizable.** API, pero `set_b.py` lo reporta **siempre N-A por política** (ver la DECISIÓN más
arriba): nunca toca el rol real. Si algún día se necesita correrlo de verdad, es manual, alto riesgo,
requiere ventana exclusiva y restauración inmediata — la cobertura repetible y segura mientras tanto
es la unitaria, ya cerrada.

---

#### B-02 — Sin identidad de aplicación, o sin el rol (MPPP-RPA-006)

**Qué prueba.** Las dos condiciones de D-9 fallan cerrado: una persona (sin `applicationid`) nunca
pasa, y un app user CON `applicationid` pero SIN el rol tampoco.

**Necesita.** Dos identidades: (a) el service principal de `local/pp_secrets.env` — **verificado en
vivo el 2026-09-27**: SÍ es una identidad de aplicación, pero solo tiene el rol `System
Administrator`, ningún rol de negocio, así que sirve para el sub-caso "identidad de aplicación sin el
rol"; y (b) un usuario humano real (`User1`, sin `applicationid`) para el sub-caso "sin identidad de
aplicación" — impersonado con la cabecera `MSCRMCallerID` sobre una llamada del service principal, no
hace falta su sesión de navegador ni su MFA para esto.

**Cómo.**
1. `dataverse_api.py GET "sanic_mppp_capi_obtenerfilaspendientes(maximo=5,claimminutos=5)"` con la
   identidad del service principal (sub-caso "aplicación sin el rol").
2. Repetir contra `sanic_mppp_capi_registrarresultadorpa` (Action, POST, cuerpo `{"resultados":"[]"}`).
3. La misma llamada del paso 1, pero con la cabecera `MSCRMCallerID` puesta al `systemuserid` de
   `User1` (sub-caso "persona sin identidad de aplicación").

**Qué tiene que pasar.** Las tres llamadas rechazadas con `MPPP-RPA-006`.

**Cómo se verifica.** El mensaje de error de cada llamada.

**Cómo se sabe que falló.** Si en cambio devuelve datos, la puerta no se está evaluando (regresión
de D-9): revisar que `PuertaDelRpa.Exigir` siga siendo la primera línea de las dos clases API. Si
da `MPPP-RPA-007`, es el caso B-01 confundido con este (el rol SÍ existe, lo que falta es el
privilegio de esta identidad puntual).

**Automatizable.** API.

---

#### B-03 — El bot pasa la puerta (D-9, camino feliz)

**Qué prueba.** Con `applicationid` presente y el rol asignado, las dos condiciones se cumplen y
la puerta deja pasar.

**Necesita.** `local/rpa_cred.env` con las credenciales reales del bot (D-9 ya cerrado, inventario
6.7/13.5).

**Cómo.** `python3 herramientas/pruebas/como_rpa.py`.

**Qué tiene que pasar.** El script imprime "SÍ" en las dos condiciones y termina con
"La puerta de D-9 lo deja pasar."

**Cómo se verifica.** La salida del script (código de salida 0).

**Cómo se sabe que falló.** Cualquiera de las dos condiciones en "NO", o código de salida 1.
Distinguir CUÁL de las dos falló (el mensaje las nombra por separado) antes de tocar nada: son dos
causas distintas (rol vs. identidad).

**Automatizable.** API.

---

#### B-04 — Orden FIFO estricto (fecha validada, y a igualdad, número de fila)

**Qué prueba.** El orden de entrega es determinístico: `sanic_fechavalidada` y, a igualdad,
`sanic_numerofila`. Sin esto, un lote sobre miles de pendientes puede repetir siempre las mismas y
dejar las del fondo esperando para siempre sin que nada falle.

**Necesita.** Una Solicitud con al menos 3 Filas Validada, validadas en momentos distintos (o en el
mismo momento, para probar el desempate por número de fila) — el Excel `E01-feliz-formato11.xlsx`
trae 3 filas que se validan juntas, sirve para el desempate.

**Cómo.**
1. `python3 herramientas/pruebas/probar_rpa.py --maximo=2` (pide menos de las que hay).
2. Anotar qué `filaid`/número de fila vino.
3. `python3 herramientas/pruebas/probar_rpa.py --maximo=2` de nuevo, **sin** haber reportado nada
   (el claim no excluye: ver B-09).

> **Nota de la automatización (`set_b.py`).** Corriendo contra un Dev COMPARTIDO no se puede asumir
> que las 3 filas de este caso sean las únicas pendientes (CONVENCIONES §8: un set autocontenido no
> asume un entorno ajeno vacío). `set_b.py` pide con `maximo=500` (el tope duro, para traer toda la
> cola) y filtra el resultado por el número de SU PROPIA Solicitud, verificando el orden relativo
> entre sus 3 filas — equivalente a lo de arriba, pero no se rompe si hay otras filas pendientes de
> otro origen.

**Qué tiene que pasar.** Las mismas 2 primeras filas por orden de `numerofila` (a igual
`fechavalidada`), en el mismo orden las dos veces.

**Cómo se verifica.** La salida de `probar_rpa.py` (`mostrar_fila` imprime número de fila y
`fechavalidada` de cada una).

**Cómo se sabe que falló.** El orden cambia entre llamadas sin que nadie haya reportado nada, o
una fila de número más alto sale antes que una de número más bajo con la misma fecha: el FIFO no es
determinístico y una solicitud puede quedar esperando indefinidamente sin que nada lo denuncie.

**Automatizable.** API.

---

#### B-05 — `maximo`: defecto 100, recorte silencioso al tope (nunca error)

**Qué prueba.** `maximo` ausente entrega hasta 100; un valor mayor al tope (500) se recorta sin
error — pedir de más no es un error del bot.

**Necesita.** Al menos 1 Fila Validada. (El tope EXACTO de 500 no se puede verificar de punta a
punta en Dev sin sembrar 500+ filas reales: eso ya lo cubre `ReglasDelRpaAceptacion` a nivel
unitario. Este caso verifica el COMPORTAMIENTO de recorte, no el número exacto.)

**Cómo.**
1. `python3 herramientas/pruebas/probar_rpa.py` (sin `--maximo`: usa el defecto del script, que es
   5 — para ver el defecto REAL de 100 del contrato, llamar directo:
   `dataverse_api.py GET "sanic_mppp_capi_obtenerfilaspendientes(claimminutos=5)"` como el bot).
2. `dataverse_api.py GET "sanic_mppp_capi_obtenerfilaspendientes(maximo=999999,claimminutos=5)"`
   como el bot.

**Qué tiene que pasar.** Las dos llamadas responden `200`, nunca un error por `maximo` grande;
`entregadas` nunca supera lo que había pendiente ni pasa de 500.

**Cómo se verifica.** El código HTTP y el campo `entregadas` de la respuesta.

**Cómo se sabe que falló.** Un HTTP 4xx por `maximo` grande (se está rechazando en vez de
recortar), o `entregadas` mayor que lo realmente pendiente.

**Automatizable.** API.

---

#### B-06 — `maximo` / `claimminutos` inválidos rechazan la llamada

**Qué prueba.** `maximo` presente pero 0, negativo o no numérico, y `claimminutos` ausente, 0,
negativo o no numérico, rechazan la llamada ENTERA con un error claro (a diferencia de "faltar", que
tiene defecto solo para `maximo`).

**Necesita.** Identidad del bot.

**Cómo.**
1. `GET sanic_mppp_capi_obtenerfilaspendientes(maximo=0,claimminutos=5)`
2. `GET sanic_mppp_capi_obtenerfilaspendientes(maximo=-3,claimminutos=5)`
3. `GET sanic_mppp_capi_obtenerfilaspendientes(claimminutos=5)` (sin `maximo`: **esto NO es un
   error**, es el defecto — ver B-05)
4. `GET sanic_mppp_capi_obtenerfilaspendientes(maximo=5)` (sin `claimminutos`: acá SÍ es error,
   `claimminutos` no tiene defecto)
5. `GET sanic_mppp_capi_obtenerfilaspendientes(maximo=5,claimminutos=0)`

**Qué tiene que pasar.** 1, 2 y 5 rechazan con **HTTP 400** y un mensaje que nombra el parámetro y
exige "entero mayor que cero"; 3 responde 200 con el defecto de 100; **4 rechaza con HTTP 404**
("Resource not found for the segment"), NO 400 — verificado en vivo contra Dev el 2026-09-28
(diagnóstico de D-60): `claimminutos` está declarado `isoptional: false` en
`customapirequestparameters`, así que sin él el ruteo OData rechaza el segmento del recurso ANTES
de resolver la Function — la llamada nunca llega al plugin, nunca pasa por D-9. No es un defecto
del sistema: la plataforma actúa correctamente, solo que más temprano de lo que esta ficha asumía
originalmente (que esperaba 400 para los cinco sub-casos por igual).

**Cómo se verifica.** Código HTTP y mensaje de cada llamada.

**Cómo se sabe que falló.** Si 3 rechaza (el defecto de `maximo` dejó de aplicarse), si 1/2/5
aceptan silenciosamente un valor sin sentido, o si 4 deja de dar 404 (por ejemplo, si un cambio de
registro vuelve `claimminutos` opcional: ahí sí pasaría a valer 400 desde el plugin y esta ficha
tendría que actualizarse de nuevo).

**Automatizable.** API.

---

#### B-07 — El claim se pone al entregar, y frena a una PERSONA

**Qué prueba.** `obtenerfilaspendientes` pone/refresca `sanic_claimhasta` sobre cada fila que
entrega; mientras esté vigente, una persona no puede transicionarla, y el mensaje dice CUÁNTO
FALTA, nunca una hora (Nicaragua es UTC-6 y una hora sin convertir miente).

**Necesita.** Una Fila Validada; un usuario real con el rol `SR - MPPP - Ejecutivo` (`User1`).
**No** sirve `actuar_fila.py` con el service principal (ver el aviso al principio de este archivo):
se impersona a `User1` directo con `MSCRMCallerID`.

**Cómo.**
1. `python3 herramientas/pruebas/probar_rpa.py --claimminutos=10` (deja el claim puesto aunque no
   se confirme: "ponerlo es parte de entregar").
2. Anotar el `filaid` devuelto.
3. Un `PATCH` a `sanic_mppp_tbl_filas(<filaid>)` con `{"sanic_estado": 159460004}` (Digitada),
   con la cabecera `MSCRMCallerID` puesta al `systemuserid` de `User1`, dentro de los 10 minutos.

**Qué tiene que pasar.** El paso 3 se rechaza con un mensaje del tipo "La fila está reservada por
el proceso automático; se libera en N minutos." (o "en menos de un minuto"), **nunca** "no
autorizado" ni una hora del reloj.

**Cómo se verifica.** El mensaje de error del `PATCH` del paso 3.

**Cómo se sabe que falló.** Si la transición se permite (el claim no frena), o si el mensaje dice
una hora en vez de una duración (bug ya documentado y corregido una vez — ver
`TransicionesDeFila.MensajeClaimVigente`), o si dice "0 minutos" (nunca debería, redondea hacia
arriba).

**Automatizable.** API (mensaje se verifica por texto, sin necesidad de UI).

---

#### B-08 — El claim NO frena al bot cuando reporta, aunque haya vencido

**Qué prueba.** El claim protege TOMAR, no REPORTAR: si el bot tardó más que su propio claim, el
reporte tiene que aceptarse igual — lo contrario crearía divergencia con AS400 (la operación YA
pasó ahí).

**Necesita.** Una Fila Validada; posibilidad de pedir un claim MUY corto (1 minuto) y esperar a que
venza antes de reportar — o, equivalente y más rápido (lo que usa `set_b.py`, mismo mecanismo que
`08-integridad-y-plugins.md` I-13): forzar `sanic_claimhasta` a un pasado reciente con un `PATCH`
directo del service principal (identidad de aplicación: no entra a la lista blanca de una persona,
pero sí la escribe una identidad de aplicación) en vez de esperar el minuto real.

**Cómo.**
1. `python3 herramientas/pruebas/probar_rpa.py --claimminutos=1` y anotar el `filaid`.
2. Esperar más de 1 minuto (sin que nadie más actúe sobre la fila) — o, más rápido:
   `dataverse_api.py PATCH sanic_mppp_tbl_filas(<filaid>) '{"sanic_claimhasta":"<5 minutos atrás, con Z>"}'`.
3. `dataverse_api.py POST sanic_mppp_capi_registrarresultadorpa` (como el bot) con
   `{"resultados":"[{\"filaid\":\"<id>\",\"resultado\":\"digitada\",\"codigo\":\"OK\",\"fechaproceso\":\"<ahora ISO con Z>\"}]"}`.

**Qué tiene que pasar.** El reporte se acepta (`estadofila: "digitada"`), pese a que el claim ya
venció.

**Cómo se verifica.** La respuesta de la llamada (`procesadas`, y `estadofila` del ítem).

**Cómo se sabe que falló.** Un rechazo del reporte por "claim vencido" o similar: eso reintroduce
la divergencia con AS400 que este diseño existe para evitar.

**Automatizable.** API.

---

#### B-09 — El claim NO excluye: el bot puede volver a pedir lo mismo

**Qué prueba.** El caso decisivo del diseño: si el bot se cae y reinicia (pierde su lista en
memoria), tiene que poder recuperar lo que ya tenía tomado, no quedar trabado hasta que venza el
claim.

**Necesita.** Una Fila Validada, sin reportar nada sobre ella.

**Cómo.**
1. `probar_rpa.py --claimminutos=10` y anotar qué filas entrega.
2. `probar_rpa.py --claimminutos=10` de nuevo, sin reportar nada en el medio.

**Qué tiene que pasar.** Las mismas filas vuelven en la segunda llamada (el claim se refresca, no
se excluye).

**Cómo se verifica.** Comparar los `filaid` de las dos respuestas.

**Cómo se sabe que falló.** La segunda llamada devuelve 0 filas o filas distintas: el claim está
excluyendo, y un bot que reinicia quedaría trabado hasta el vencimiento.

**Automatizable.** API.

---

#### B-10 — Cuándo se libera el claim: en terminal sí, en Validada→Digitada no

**Qué prueba.** El claim se libera al llegar a un estado terminal (Aprobada, Rechazada en AS400,
Anulada) o al marcar atención manual; en `Validada → Digitada` NO se libera, porque al bot le
queda trabajo (aprobarla).

**Necesita.** Dos Filas Validada distintas, con un claim puesto sobre las dos.

**Cómo.**
1. `probar_rpa.py --confirmar` con al menos 2 filas Validada (deja una en Digitada vía el paso 3
   del script, y deja constancia del resto).
2. Sobre la fila que quedó **Digitada**: `ver_solicitud.py <Solicitud>` y mirar `sanic_claimhasta`.
3. Sobre una fila llevada a un estado terminal (por ejemplo, `rechazada` en el mismo lote del
   script): mirar su `sanic_claimhasta`.

**Qué tiene que pasar.** La fila Digitada conserva `sanic_claimhasta` con un valor futuro (no se
liberó); la fila en estado terminal tiene `sanic_claimhasta` vacío.

**Cómo se verifica.** `ver_solicitud.py <Solicitud>` (muestra el claim de cada fila), o
`dataverse_api.py GET "sanic_mppp_tbl_filas(<id>)?$select=sanic_claimhasta"`.

**Cómo se sabe que falló.** El claim se libera en Digitada (un ejecutivo podría tomarla mientras el
bot todavía tiene que aprobarla), o NO se libera en un estado terminal (queda "tomada por el bot"
para siempre en la vista *Tomadas por el bot*).

**Automatizable.** API.

---

#### B-11 — `rpa.puedeaprobar = no` oculta las Digitadas del lote

**Qué prueba.** Sin el aval de C-03, el bot no debe ni recibir trabajo de aprobar: solo se le
entregan Validadas.

**Necesita.** Coordinar con quien esté corriendo pruebas de fase 2 en simultáneo: `rpa.puedeaprobar`
es un parámetro de ENTORNO COMPARTIDO. Cambiarlo afecta a cualquier otra prueba que dependa de su
valor actual (en Dev hoy vale `si`, según el comentario de `probar_rpa.py`). Restaurar el valor
original apenas termine el caso.

**Cómo.**
1. Dejar una Fila en Digitada por el propio bot (`probar_rpa.py --confirmar`).
2. Cambiar `rpa.puedeaprobar` a `no` (nueva versión del parámetro, nunca editar la vigente — ver
   `03-reglas-de-registro.md` R-26 para el mismo criterio de versionado).
3. `probar_rpa.py` de nuevo (sin `--confirmar` alcanza para ver el lote).
4. Restaurar `rpa.puedeaprobar` a su valor original.

**Qué tiene que pasar.** El paso 3 no trae ninguna fila en estado `digitada` en el JSON, aunque
haya una digitada por el propio bot.

**Cómo se verifica.** El campo `filas` de la respuesta (o la salida de `probar_rpa.py`, que cuenta
las Digitadas devueltas).

**Cómo se sabe que falló.** Una fila Digitada aparece en el lote con `rpa.puedeaprobar = no`: el
bot va a intentar aprobar, va a fallar, y va a reintentarlo en cada corrida durante meses.

**Automatizable.** API — pero de alto cuidado por ser parámetro compartido.

---

#### B-12 — Para aprobar, solo lo que digitó el mismo bot

**Qué prueba.** `SeleccionParaElRpa.EntraEnElLote` compara contra el usuario que llama, no contra
"cualquiera con el rol del RPA": el bot no aprueba lo que digitó una persona.

**Necesita.** Una Fila Validada; un usuario real con el rol `SR - MPPP - Ejecutivo` (`User1`) para
digitarla como una persona — **no** `actuar_fila.py` con el service principal (ver el aviso al
principio de este archivo): se impersona a `User1` directo con `MSCRMCallerID`.

**Cómo.**
1. Un `PATCH` a la Fila con `{"sanic_estado": 159460004}` (Digitada), con `MSCRMCallerID` puesto al
   `systemuserid` de `User1` (la digita una persona, NO el bot).
2. `python3 herramientas/pruebas/probar_rpa.py` — pedir el lote como el bot.

**Qué tiene que pasar.** Esa fila (Digitada, pero por otra identidad) **no** aparece en el lote del
bot.

**Cómo se verifica.** El campo `filas`/`entregadas` de la respuesta, comparando contra el `filaid`
digitado en el paso 1.

**Cómo se sabe que falló.** La fila aparece en el lote: el bot intentaría "aprobar" un trabajo que
no hizo, rompiendo la responsabilidad que sostiene esta regla (no es una regla de permisos, es de
"le consta o no le consta").

**Automatizable.** API.

---

#### B-13 — `fechaproceso`: offset explícito, no en el futuro, dentro de tolerancia (MPPP-RPA-003)

**Qué prueba.** Las tres condiciones de `ReglasDelRpa.ParsearFechaConOffset` /
`ExigirFechaCreible`: sin offset se rechaza (nunca se asume hora local del servidor), en el futuro
se rechaza, y más vieja que `rpa.horas.tolerancia` (fallback 24 h) se rechaza.

**Necesita.** Una Fila Validada, identidad del bot.

**Cómo.** Tres llamadas a `registrarresultadorpa`, cada una con un ítem sobre la misma fila (o
filas distintas, para no depender de que la anterior haya fallado):
1. `"fechaproceso": "2026-10-05T15:04:12"` (sin `Z` ni offset).
2. `"fechaproceso": "<mañana, con Z>"`.
3. `"fechaproceso": "<hace 48 horas, con Z>"` (mayor que el fallback de 24 h).

**Qué tiene que pasar.** Las tres devuelven el ítem con `error` conteniendo `MPPP-RPA-003` y un
motivo distinto cada vez (sin offset / en el futuro / más vieja que la tolerancia).

**Cómo se verifica.** El campo `error` de cada ítem en la respuesta (HTTP sigue siendo 200: es un
fallo por ítem, no de la llamada).

**Cómo se sabe que falló.** Alguna de las tres se acepta silenciosamente, o el HTTP de la llamada
es 4xx (se estaría tratando como error de la LLAMADA cuando es de la fecha de UN ítem).

**Automatizable.** API.

---

#### B-14 — Catálogo de errores por ítem: 001, 002, 004, 005

**Qué prueba.** Los cuatro códigos restantes del catálogo por ítem, en un solo lote (mismo espíritu
que `probar_rpa.py` paso 3: un lote mixto no debe arrastrar lo malo sobre lo bueno).

**Necesita.** Tres Filas Validada reales, DISTINTAS (una por ítem que sí resuelve a una fila), y un
`filaid` inventado para el ítem que no. **No repetir la misma fila real en dos ítems del mismo
lote**: `probar_rpa.py` ya deja anotado que dos operaciones sobre la MISMA fila dentro de un lote
rompen la llamada ENTERA, no solo el ítem — y acá se necesitan los cuatro resultados por separado
para verificar que ninguno arrastra a los demás.

**Cómo.** Un lote de 4 ítems en una sola llamada a `registrarresultadorpa`:
1. `filaid` de un GUID que no existe → esperar `MPPP-RPA-001`.
2. Sobre una fila real: `resultado: "aprobadaXYZ"` (no es ninguno de los 4 valores válidos) →
   `MPPP-RPA-002`.
3. Sobre OTRA fila real: `resultado: "rechazada"` **sin** `mensaje` → `MPPP-RPA-004`.
4. Sobre una TERCERA fila real: `codigo: "NO-EXISTE-ESTE-CODIGO"`, `resultado: "rechazada"` con
   `mensaje` → `MPPP-RPA-005`.

**Qué tiene que pasar.** HTTP 200, `conerror: 4`, cada ítem con su código propio en `error`, y
ninguno afecta a los demás.

**Cómo se verifica.** El array `resultados` de la respuesta.

**Cómo se sabe que falló.** Un código equivocado (por ejemplo 002 donde debería ir 001), o que
alguno de los otros tres ítems del lote se vea afectado por el fallo de uno.

**Automatizable.** API.

---

#### B-15 — Lote más grande que `rpa.lote.maximo` se rechaza ENTERO (MPPP-RPA-008)

**Qué prueba.** A diferencia de `maximo` en `obtenerfilaspendientes` (que recorta), un lote que
excede `rpa.lote.maximo` se rechaza completo: son hechos que ya ocurrieron en AS400, y recortar
perdería el resto en silencio.

**Necesita.** Confirmar el valor actual de `rpa.lote.maximo` (`ver_solicitud.py --parametros`; hoy
100). Al menos una Fila Validada real para poder comprobar que NINGUNA cambió después del rechazo.

**Cómo.**
1. Anotar el estado actual de una Fila real conocida.
2. Armar un lote de `rpa.lote.maximo + 1` ítems (pueden repetir el mismo `filaid` real intercalado
   con `filaid` inventados — lo que importa es la CANTIDAD de ítems, no que todos sean válidos).
3. `dataverse_api.py POST sanic_mppp_capi_registrarresultadorpa` con ese lote, como el bot.
4. Releer el estado de la fila real del paso 1.

**Qué tiene que pasar.** La llamada entera falla con un error que trae `MPPP-RPA-008`; la fila del
paso 1 queda EXACTAMENTE como estaba (no se aplicó nada, ni siquiera los ítems "buenos" del lote).

**Cómo se verifica.** El HTTP y el mensaje de la llamada; el estado de la fila antes/después con
`ver_solicitud.py`.

**Cómo se sabe que falló.** Si la fila real cambió de estado a pesar del rechazo (el lote se aplicó
parcialmente, exactamente lo que este diseño prohíbe), o si el lote se recortó en silencio en vez
de rechazarse.

**Automatizable.** API.

---

#### B-16 — Lote vacío se rechaza (MPPP-RPA-009)

**Qué prueba.** `resultados` vacío (`"[]"` o ausente) es un error de la llamada, no un no-op
silencioso.

**Necesita.** Identidad del bot.

**Cómo.** `dataverse_api.py POST sanic_mppp_capi_registrarresultadorpa '{"resultados":"[]"}'`.

**Qué tiene que pasar.** Rechazo con `MPPP-RPA-009`.

**Cómo se verifica.** El mensaje de error de la llamada.

**Cómo se sabe que falló.** HTTP 200 con `procesadas: 0` sin ningún error: un lote vacío pasaría
inadvertido.

**Automatizable.** API.

---

#### B-17 — Falla parcial: un ítem malo no arrastra a los buenos

**Qué prueba.** "Nunca todo o nada" (dentro del límite de `rpa.lote.maximo`): la llamada devuelve
200 con resultado por ítem, y los ítems buenos SÍ se aplican aunque otro del mismo lote falle.

**Necesita.** Dos Filas Validada reales.

**Cómo.** Un lote de 3 ítems: dos sobre las Filas reales (una `digitada` válida, otra `rechazada`
con `codigo` y `mensaje` válidos) y una tercera con un `filaid` inventado.

**Qué tiene que pasar.** `procesadas: 2`, `conerror: 1`; las dos filas reales SÍ cambiaron de
estado (confirmable con `ver_solicitud.py`).

**Cómo se verifica.** La respuesta de la llamada, y el estado real de las dos filas después.

**Cómo se sabe que falló.** Las filas buenas NO cambiaron (el fallo de la tercera tumbó todo el
lote): eso perdería hechos reales de AS400, que es justo lo que este diseño evita.

**Automatizable.** API.

---

#### B-18 — Idempotencia: `estadofila` manda, nunca `yaregistrado`

**Qué prueba.** Repetir un resultado ya aplicado no cambia nada y devuelve `yaregistrado: true`;
pero ese booleano tapa DOS situaciones opuestas ("ya me lo dijiste" vs. "una persona actuó antes") y
el bot tiene que decidir por `estadofila`, nunca por `yaregistrado`.

**Necesita.** Una Fila Validada real; un usuario real con el rol `SR - MPPP - Ejecutivo` (`User1`)
para simular que una persona actuó en el medio — **no** `actuar_fila.py` con el service principal
(ver el aviso al principio de este archivo): se impersona a `User1` directo con `MSCRMCallerID`.

**Cómo.**
1. Reportar `digitada` sobre la fila (como el bot).
2. Repetir EXACTAMENTE el mismo reporte (`digitada`, mismo `filaid`).
3. Sobre otra Fila Validada: reportar `digitada` como el bot, y luego un `PATCH` a esa Fila con
   `{"sanic_estado": 159460007, "sanic_mensaje": "prueba B-18"}` (Anulada), con `MSCRMCallerID`
   puesto al `systemuserid` de `User1` (una persona anula).
4. Reportar `digitada` OTRA VEZ sobre esa misma fila anulada, como el bot.

**Qué tiene que pasar.** Paso 2: `yaregistrado: true`, `estadofila: "digitada"`, nada cambia. Paso
4: `yaregistrado: true` TAMBIÉN, pero `estadofila: "anulada"` — el bot tiene que leer esto y
soltar la fila, no intentar aprobarla.

**Cómo se verifica.** El campo `estadofila` de cada respuesta.

**Cómo se sabe que falló.** Si el paso 4 devuelve `estadofila: "digitada"` (mintiendo sobre lo que
en realidad pasó), o si intenta aplicar algún cambio sobre una fila ya anulada.

**Automatizable.** API.

---

#### B-19 — Atención manual: marca, libera el claim, y cierra la puerta al bot

**Qué prueba.** `atencionmanual` prende `sanic_requiereatencionmanual`, libera el claim, y desde
ese momento **el bot no puede volver a levantar esa fila** aunque el FIFO la pondría primero
(`SeleccionParaElRpa.EntraEnElLote`: gana incluso sobre una Digitada propia).

**Necesita.** Una Fila Validada real con un claim puesto (pedirla primero con
`obtenerfilaspendientes`).

**Cómo.**
1. `probar_rpa.py --claimminutos=10` y anotar el `filaid`.
2. Reportar `atencionmanual` sobre esa fila (con `codigo` y `mensaje`).
3. `probar_rpa.py` de nuevo, pidiendo un lote que por FIFO debería incluirla.

**Qué tiene que pasar.** Paso 2: `sanic_requiereatencionmanual = sí`, `sanic_claimhasta` vacío.
Paso 3: esa fila NO aparece en el lote, aunque por orden FIFO le tocaría.

**Cómo se verifica.** `ver_solicitud.py <Solicitud>` (estado y marca de la fila), y el `filas` de
la respuesta del paso 3.

**Cómo se sabe que falló.** La fila sigue en el lote del bot (el cerrojo no está funcionando: el
bot volvería a intentar algo que ya se determinó que no sabe resolver), o el claim no se liberó
(quedaría "tomada por el bot" en la vista *Tomadas por el bot* sin que el bot la esté trabajando).

**Automatizable.** API.

---

#### B-20 — La marca de atención manual se apaga sola con cualquier transición exitosa

**Qué prueba.** Nadie tiene que acordarse de apagarla a mano (aunque también existe el botón
*Atendida* — ver `06-seguridad-vistas-formularios.md` G-14/G-19): CUALQUIER transición exitosa de la
fila la limpia, sin importar quién la hizo ni a qué estado fue.

**Necesita.** Una Fila marcada `sanic_requiereatencionmanual = sí` (repetir B-19, o seguir su
resultado si corrió antes en el mismo set); un usuario real con el rol `SR - MPPP - Ejecutivo`
(`User1`) — **no** `actuar_fila.py` con el service principal (ver el aviso al principio de este
archivo).

**Cómo.** Un `PATCH` a esa Fila con `{"sanic_estado": 159460004}` (Digitada), con `MSCRMCallerID`
puesto al `systemuserid` de `User1` (una transición cualquiera que la plataforma permita desde su
estado actual).

**Qué tiene que pasar.** La transición se aplica, y `sanic_requiereatencionmanual` vuelve a `no`,
sin que nadie haya tocado esa columna a propósito.

**Cómo se verifica.** `ver_solicitud.py <Solicitud>` después de la transición.

**Cómo se sabe que falló.** La marca sigue en `sí` después de una transición exitosa: alguien
tendría que acordarse de apagarla a mano en cada caso, que es exactamente lo que este diseño evita.

**Automatizable.** API.

---

### Resultados

| Caso | Resultado | Notas |
|---|---|---|
| B-01 rol no instalado (007) | | alto riesgo — ver advertencia |
| B-02 sin identidad/rol (006) | | |
| B-03 el bot pasa | | |
| B-04 FIFO estricto | | |
| B-05 `maximo` defecto/recorte | | |
| B-06 `maximo`/`claimminutos` inválidos | | |
| B-07 claim frena a una persona | | |
| B-08 claim no frena al bot al reportar | | |
| B-09 claim no excluye | | |
| B-10 libera en terminal, no en Digitada | | |
| B-11 `rpa.puedeaprobar=no` | | parámetro compartido |
| B-12 solo aprueba lo que digitó | | |
| B-13 tolerancia/offset de fecha (003) | | |
| B-14 catálogo 001/002/004/005 | | |
| B-15 lote grande rechazado (008) | | |
| B-16 lote vacío (009) | | |
| B-17 falla parcial no arrastra | | |
| B-18 idempotencia / `estadofila` manda | | |
| B-19 atención manual: marca + cerrojo | | |
| B-20 atención manual se apaga sola | | |

---
## 10 — DN-01: autorización por cliente, con interruptor a plan

Cubre `autorizacion.modo` (`cliente`/`plan`, degradación a `plan` con basura), la tabla
`AutorizacionCliente`, el nombre calculado `<correo> → <cliente>`, la evidencia firmada
(`sanic_documentofirmado`/`sanic_fechadocumento`) y las subgrillas nuevas en Cliente y Autorizado.
Fuente: `diseno/DIRECTRICES-NUEVAS.md` DN-01, `diseno/02-diccionario-datos.md` §2.5,
`src/Sanic.Mppp.Plugins/Validacion/ReglasDeRegistro.cs` (`ModoDeAutorizacion`,
`ModoDeAutorizacionParametro`), `Datos/Catalogos.cs` (`PlanesAutorizadosDe`),
`Steps/ReglasDeSteps.cs`/`StepsDePlataforma.cs` (`NombreCalculado.DeAutorizacionCliente`).

Convención de IDs: prefijo `U-` (a**U**torización), ver el encabezado de `09-rpa-fase2.md`.

### Preparativos comunes a este archivo

Implementados en `herramientas/pruebas/sets/set_u.py::preparar()`. Automatizados por completo (Web
API): nada de esto se hace a mano.

- **`autorizacion.modo` es un parámetro de ENTORNO COMPARTIDO, pero NO se restaura caso por caso.**
  CONVENCIONES §8.2 reemplaza esa idea: el set no restaura lo que encontró, lo lleva a su **canon**
  (`plan`), y eso lo hace `correr.py` — automáticamente, antes y después de CADA set, sea cual sea —
  no cada caso ni cada set por separado. Lo único que le toca a `set_u.py` es que cada CASO que
  necesita un valor puntual de `autorizacion.modo` lo fija ÉL MISMO al empezar (nunca asume el valor
  que dejó el caso anterior): así el set da el mismo resultado si se corre entero o caso por caso.
- El fixture compartido `PR*` (`preparar_datos.py`: Cliente `PR - Pruebas MPPP SA`, planes `PR11`/
  `PR06`/`PR10`/`PRIN`/`PRNA`) y el correo `REMITENTE = zz-corredor-fase1@example.com` (el mismo que
  usan los sets C/S/R), autorizado por `AutorizacionPlan` — **sin ninguna `AutorizacionCliente`**:
  es justo lo que U-03 necesita.
- **`REMITENTE_SOLOCLIENTE = zz-corredor-set-u-dn01-cliente@example.com`**: Autorizado propio de
  este set, con UNA SOLA vía de autorización — `AutorizacionCliente` hacia el Cliente compartido —,
  CERO `AutorizacionPlan`. Para U-02, U-04(a), U-09 y U-11.
- **Un segundo Cliente "ajeno" (`ZZP-Cliente ajeno DN-01 (set U)`), con su propio Plan activo
  (`ZZU1`), y un tercer correo propio (`REMITENTE_OTRO_CLIENTE`) autorizado SOLO por ese cliente
  ajeno.** Hace falta para dos cosas que el diseño original de este archivo no anticipaba:
  - **U-06** necesita comparar el mensaje al cliente citando el MISMO plan en los dos modos, sin que
    ninguna de las dos Solicitudes quede "No reconocida" (ver la nota de D-59 más abajo). Con el
    cliente ajeno, `REMITENTE_OTRO_CLIENTE` queda RECONOCIDO en modo `cliente` (tiene un plan propio
    activo) pero NO autorizado sobre `PRNA` (que es del cliente compartido) — el mismo plan, la
    misma regla, la misma razón de rechazo que en modo `plan`.
  - **U-09** pide "un Autorizado con `AutorizacionCliente` hacia más de un Cliente": `preparar()` le
    agrega a `REMITENTE_SOLOCLIENTE` una segunda `AutorizacionCliente`, hacia este cliente ajeno.
  - Se borra por GUID trackeado en `preparar()`, igual que el resto de la fixture de DN-01 (ver el
    punto siguiente): no es un cliente real, no lo busca nadie por nombre.
- `preparar_datos.py` **no crea `AutorizacionCliente`**: `set_u.py` la arma por Web API, citando los
  lookups `sanic_autorizadoid`/`sanic_clienteid` como `@odata.bind`, **sin mandar `sanic_nombre`**
  (lo calcula un step del servidor — U-07).
- **Ni la `AutorizacionCliente` ni el `Autorizado` propios de este set llevan la marca `ZZP-`
  intacta**: el `sanic_nombre` de `AutorizacionCliente` lo calcula un step del servidor y lo pisa; el
  de `Autorizado` se normaliza a minúsculas. Los dos se borran por el GUID que devolvió el propio
  `POST` de esta misma corrida (`ctx.extra`), nunca por `marca_agua.borrar_marcado()`. El Cliente y
  el Plan ajenos SÍ podrían llevar la marca (nada les pisa el `sanic_nombre`), pero también se borran
  por GUID trackeado, por consistencia con el resto del set.

#### D-59 (`diseno/PENDIENTES.md`, ABIERTA, pospuesta por el aprobador el 2026-09-27)

`AvisosDataverse.AvisarAEjecutivos` revienta con `InvalidCastException` cada vez que
`ClasificarCorreo` termina en un estado que dispara el aviso a ejecutivos (remitente no reconocido,
respuesta, ilegible): la transacción se revierte entera y la Solicitud NUNCA llega a persistir "No
reconocida". **U-03, U-04 (mitad a) y U-05 (paso 2) ejercitan justo ese camino** (un correo que
tiene que terminar "No reconocida" bajo `autorizacion.modo = cliente`), así que se espera que
choquen con D-59 en el entorno de hoy — el mismo defecto que ya cruzaron el set R en R-17 y el set C
en C-02/C-04/C-10/C-11. CONVENCIONES §3, "Un defecto conocido no es N-A": esos casos van a quedar en
**FALLA**, citando D-59 en el motivo, nunca en N-A. U-06 se diseñó a propósito para NO pasar por
"No reconocida" (ver arriba), así que no debería toparse con D-59.

---

#### U-01 — Compatibilidad: sin el parámetro (o en `plan`), nada cambia

**Qué prueba.** Un `autorizacion.modo` ausente, o con el valor `plan`, se comporta EXACTAMENTE
como el sistema se comportaba antes de DN-01: mira `AutorizacionPlan`.

**Necesita.** El estado de HOY del entorno (sin `autorizacion.modo` sembrado, o sembrado en
`plan`) — confirmarlo con `ver_solicitud.py --parametros` antes de tocar nada. Un Autorizado con
`AutorizacionPlan` a un plan, y SIN ninguna `AutorizacionCliente`.

**Cómo.** Correr cualquier caso de `01-clasificacion-del-correo.md`/`02-reglas-de-solicitud.md` tal
cual están (no citan `AutorizacionCliente` para nada).

**Qué tiene que pasar.** Exactamente lo que esos archivos ya documentan como esperado — ningún
cambio de comportamiento.

**Cómo se verifica.** Los mismos criterios de `01`/`02`/`03` (no se agrega nada nuevo).

**Cómo se sabe que falló.** Cualquier diferencia contra lo que `01`–`08` ya establecieron como
esperado, sin haber tocado `autorizacion.modo`: sería una regresión de DN-01 sobre fase 1.

**Automatizable.** API (reutiliza los casos existentes).

---

#### U-02 — Modo `cliente`: un correo autorizado SOLO por cliente entra, en los DOS niveles

**Qué prueba.** La prueba que fija el diseño (`DIRECTRICES-NUEVAS.md`, "La prueba que lo fija"):
con `modo = cliente`, un correo autorizado ÚNICAMENTE por `AutorizacionCliente` (sin ninguna
`AutorizacionPlan`) tiene que PASAR `REMITENTE_RECONOCIDO` (nivel Correo) **y** sus filas tienen
que quedar autorizadas por `AUTORIZACION_CORREO_PLAN` (nivel Registro) — las dos cosas, porque hay
un solo punto de consulta (`Catalogos.PlanesAutorizadosDe`) y nadie puede olvidarse de uno.

**Necesita.** `autorizacion.modo = cliente` (valor de prueba, no versionado: §8.2 solo versiona un
cambio de negocio). `REMITENTE_SOLOCLIENTE = zz-corredor-set-u-dn01-cliente@example.com`, con
Autorizado creado, **CERO** `AutorizacionPlan`, y UNA `AutorizacionCliente` activa hacia el Cliente
compartido `PR - Pruebas MPPP SA` (dueño de `PR11` entre otros) — armado por
`set_u.py::preparar()`.

**Cómo.**
1. Fijar `autorizacion.modo = cliente`.
2. Mandar un correo desde `REMITENTE_SOLOCLIENTE` con `E01-feliz-formato11.xlsx` (cita `PR11`, del
   Cliente autorizado).
3. `ver_solicitud.py --ultima`.

**Qué tiene que pasar.** La Solicitud entra (no "No reconocida"): `REMITENTE_RECONOCIDO` en
Cumplida, y las filas quedan Validada (no "Sin autorización").

**Cómo se verifica.** `ver_solicitud.py --ultima` (estado de la Solicitud, resultado de cada regla,
estado de cada fila).

**Cómo se sabe que falló.** Si la Solicitud queda "No reconocida" (el nivel Correo no está
resolviendo el modo cliente — el bug real que corrigió el aprobador el 2026-09-26: *"el portero
también tiene que preguntar lo mismo"*), o si el correo SÍ entra pero las filas quedan "Sin
autorización" (el nivel Registro sí lo resuelve pero el de Correo no, o viceversa: son dos síntomas
distintos de la MISMA causa raíz — un olvido de modo en uno de los dos llamadores).

**Automatizable.** API.

---

#### U-03 — Modo `cliente`: solo `AutorizacionPlan` (sin `AutorizacionCliente`) NO alcanza

**Qué prueba.** El complemento de U-02: en modo `cliente`, `AutorizacionPlan` queda "muda" — no es
un respaldo automático.

**Necesita.** `autorizacion.modo = cliente`. Un correo con `AutorizacionPlan` vigente a un plan,
pero SIN ninguna `AutorizacionCliente`.

**Cómo.** Mandar un correo desde ese remitente citando el plan autorizado por plan.

**Qué tiene que pasar.** La Solicitud queda "No reconocida" (o, si el correo ya era reconocido por
otra vía, las filas de ese plan quedan "Sin autorización").

**Cómo se verifica.** `ver_solicitud.py --ultima`.

**Cómo se sabe que falló.** El correo entra o las filas se autorizan: `AutorizacionPlan` estaría
funcionando como respaldo silencioso en modo `cliente`, en contra del diseño ("se conservan y no se
miran"). **Riesgo conocido (D-59, `diseno/PENDIENTES.md`, ABIERTA):** con `REMITENTE` reconocido
SOLO por `AutorizacionPlan` y ninguna vía por cliente, este caso hace que `REMITENTE_RECONOCIDO`
falle enteramente (no solo el plan citado) y la Solicitud tiene que terminar "No reconocida" —
justo el camino que dispara el aviso a ejecutivos, y ese aviso revienta con `InvalidCastException`
(mismo defecto que ya cruzaron R-17 y C-02/C-04/C-10/C-11). Si el entorno todavía no tiene D-59
corregido, este caso da **FALLA citando D-59**, no N-A (CONVENCIONES §3).

**Automatizable.** API.

---

#### U-04 — Valor basura en `autorizacion.modo` degrada a `plan`, sin tumbar nada

**Qué prueba.** `ModoDeAutorizacionParametro.Resolver`: cualquier valor que no sea exactamente
`cliente` (sin distinguir mayúsculas) — vacío, `"clienteX"`, `"PLAN"`, lo que sea — degrada al
fallback quemado `plan`, nunca lanza ni tumba la validación.

**Necesita.** `autorizacion.modo` sembrado con un valor inválido, p. ej. `"clienteX"`.

**Cómo.**
1. Sembrar `autorizacion.modo = "clienteX"`.
2. Mandar el correo de U-02 (autorizado SOLO por cliente).
3. Mandar el correo de `preparar_datos.py` (autorizado por plan, como en fase 1).

**Qué tiene que pasar.** Se comporta como modo `plan`: el correo de U-02 (solo por cliente) NO
entra; el de fase 1 (por plan) sí.

**Cómo se verifica.** `ver_solicitud.py --ultima` sobre las dos Solicitudes.

**Cómo se sabe que falló.** Cualquier excepción o rechazo genérico por el valor del parámetro (el
diseño exige degradar, nunca romper), o que el valor basura se interprete como `cliente`. **Riesgo
conocido (D-59):** la mitad (a) — el correo autorizado SOLO por cliente, que bajo la basura degrada
a `plan` y queda sin ninguna vía — termina "No reconocida" y puede cruzarse con D-59 (ver la nota
de U-03); la mitad (b) no debería verse afectada (el correo por plan SÍ está autorizado y nunca pasa
por el camino del aviso a ejecutivos). Si (a) da FALLA citando D-59, es el defecto conocido, no un
síntoma nuevo de U-04.

**Automatizable.** API.

---

#### U-05 — Cambiar el modo no borra nada: `AutorizacionPlan` revive al volver a `plan`

**Qué prueba.** "Cambiar el parámetro vuelve a activarlas sin recargar nada" — las dos vías
conviven en el modelo de datos siempre; el parámetro solo decide cuál se MIRA.

**Necesita.** Un Autorizado con `AutorizacionPlan` vigente, creado ANTES de este caso (el de fase
1, `preparar_datos.py`).

**Cómo.**
1. Confirmar que ese correo entra con `autorizacion.modo = plan` (o ausente).
2. Cambiar a `autorizacion.modo = cliente` y confirmar que ESE MISMO correo ahora NO entra (U-03).
3. Volver a `autorizacion.modo = plan`.
4. Mandar el correo de nuevo.

**Qué tiene que pasar.** Paso 4: vuelve a entrar exactamente como en el paso 1, sin haber tocado la
fila de `AutorizacionPlan` en ningún momento.

**Cómo se verifica.** `ver_solicitud.py --ultima` en los tres momentos.

**Cómo se sabe que falló.** El correo no vuelve a entrar en el paso 4 (algo se limpió o se
desactivó al cambiar de modo, que el diseño dice explícitamente que NO tiene que pasar). **Riesgo
conocido (D-59):** el paso 2 (confirmar que el correo NO entra en modo cliente) es el mismo camino
de U-03 y puede cruzarse con el mismo defecto — un `check` propio deja eso citado por separado, sin
tapar el resultado de los pasos 1 y 4 (el punto real de este caso: que la `AutorizacionPlan` revive
sin recargar nada).

**Automatizable.** API.

---

#### U-06 — El mensaje al cliente es idéntico en los dos modos

**Qué prueba.** "No existe" y "no autorizado" siguen sin distinguirse en el texto — el oráculo que
ya se cerró el 2026-09-24 no puede reabrirse por la puerta de atrás de un modo nuevo.

**Corregido contra CONVENCIONES y D-59 (este set).** La redacción original pedía reusar U-03 (una
Solicitud "No reconocida") para el lado `cliente`: eso no sirve para comparar `sanic_mensaje` porque
una Solicitud "No reconocida" nunca abre el Excel — no hay Fila, no hay mensaje que leer — y además
cruza D-59 (ver la nota de U-03). Se rediseñó para que las DOS mitades terminen en una Fila
"Sin autorización" (con mensaje) citando el **mismo plan**, sin que ninguna pase por "No reconocida":

**Necesita.** Dos correos RECONOCIDOS (ninguno "No reconocida") que terminen "Sin autorización"
sobre el MISMO plan `PRNA` (existe, activo, del Cliente compartido):
- (a) modo `plan`: `REMITENTE` (autorizado por `AutorizacionPlan` sobre `PR11`/`PR06`/`PR10`/`PRIN`,
  nunca sobre `PRNA` — el mismo fixture de R-08).
- (b) modo `cliente`: `REMITENTE_OTRO_CLIENTE`, autorizado por `AutorizacionCliente` sobre un
  Cliente AJENO a `PRNA` (con su propio Plan activo, para que `REMITENTE_RECONOCIDO` lo reconozca) —
  preparado por `set_u.py::preparar()`.

**Cómo.**
1. Modo `plan`: mandar `E08-sin-autorizacion.xlsx` (cita `PRNA`) desde `REMITENTE`.
2. Modo `cliente`: mandar el mismo `E08-sin-autorizacion.xlsx` desde `REMITENTE_OTRO_CLIENTE`.
3. Comparar el texto exacto de `sanic_mensaje` en la fila rechazada de cada una.

**Qué tiene que pasar.** Las dos Solicitudes son reconocidas (`procesar = true`); las dos filas
quedan "Sin autorización"; el texto es el mismo unificado del 2026-09-24 (`"El plan {plan} no existe
o usted no está autorizado sobre él."`) **byte a byte** en los dos modos (mismo plan citado: `PRNA`).

**Cómo se verifica.** `ver_solicitud.py` sobre las dos Solicitudes, columna de mensaje de la fila.

**Cómo se sabe que falló.** Un texto distinto entre modos, o un texto que distinga "no existe" de
"no autorizado": eso le da a un atacante un oráculo para enumerar planes válidos. También falla si
alguna de las dos Solicitudes queda "No reconocida" en vez de "Sin autorización" a nivel fila (el
diseño de este caso está armado justo para que eso no pase; si pasa, algo del fixture cambió).

**Automatizable.** API.

---

#### U-07 — Nombre calculado de `AutorizacionCliente`: `<correo> → <cliente>`, T(400)

**Qué prueba.** `NombreCalculado.DeAutorizacionCliente` arma el nombre igual que su hermana
`AutorizacionPlan` (mismo formato, mismo recorte), y lo hace el step, no quien crea el registro.

**Necesita.** Un Autorizado y un Cliente existentes, sus dos ids.

**Cómo.** `dataverse_api.py POST sanic_mppp_tbl_autorizacionclientes` con
`sanic_autorizadoid@odata.bind` y `sanic_clienteid@odata.bind`, **sin** mandar `sanic_nombre`.

**Qué tiene que pasar.** El registro creado trae `sanic_nombre = "<correo del autorizado> →
<nombre del cliente>"`.

**Cómo se verifica.** `dataverse_api.py GET "sanic_mppp_tbl_autorizacionclientes(<id>)?$select=sanic_nombre"`.

**Cómo se sabe que falló.** `sanic_nombre` vacío, con el separador equivocado, o mandado por quien
crea el registro sin que el step lo pise (el step tiene que pisar SIEMPRE lo que venga de una
persona).

**Automatizable.** API.

---

#### U-08 — Subgrilla "Correos autorizados sobre este cliente" en el formulario de Cliente

**Qué prueba.** DN-01 punto 6: la app necesita poder VER las autorizaciones por cliente desde el
lado del Cliente (vista 18 de `diseno/05-app-model-driven.md` §3, agregada el 2026-09-27).

**Corregido contra el diseño (este set): pasa de GUI a API.** El diseño original marcaba este caso
como navegador puro ("inspección visual"). No hace falta: lo que hay que verificar es que el
`formxml` publicado del formulario principal de Cliente tenga un control de subgrilla apuntando a
`AutorizacionCliente` con la vista y la relación correctas, y que la condición que esa subgrilla usa
para filtrar (el lookup `sanic_clienteid`) devuelva datos — exactamente lo que
`herramientas/construir/formulario.py` ya usa para publicar y verificar formularios, acá solo de
lectura. Una subgrilla vacía por un filtro mal armado (el segundo síntoma que este caso nombra) se
distingue leyendo la MISMA condición de filtro por Web API, no mirando la pantalla.

**Necesita.** El Cliente compartido `PR - Pruebas MPPP SA` con al menos una `AutorizacionCliente`
(la que arma `set_u.py::preparar()`). Ninguna sesión de navegador.

> **Nota histórica.** El diseño original pedía la sesión `local/sesion-admin_planes.json` (rol
> `SR - MPPP - Administrador de planes` — DN-01 punto 6: "el Administrador de planes es quien las
> administra", RF-11; el Administrador técnico no tiene lectura de Cliente ni de
> AutorizacionCliente, `04-matriz-privilegios.md`). Sigue siendo el rol correcto SI algún día hace
> falta una inspección visual real (por ejemplo, ante una sospecha de que la subgrilla quedó en una
> pestaña oculta, algo que el `formxml` de este formulario no puede tener: `formulario.py` genera
> siempre una única pestaña). El comando de ese camino, si hiciera falta: `cd pruebas/gui &&
> MPPP_USUARIO=admin_planes npx playwright test`.

**Cómo.**
1. `GET systemforms?$filter=objecttypecode eq 'sanic_mppp_tbl_cliente' and type eq 2` — el
   formulario principal de Cliente, su `formxml`.
2. Buscar en el `formxml` el control de subgrilla con `TargetEntityType =
   sanic_mppp_tbl_autorizacioncliente`, y el título de su sección.
3. `GET EntityDefinitions(LogicalName='sanic_mppp_tbl_cliente')/OneToManyRelationships` — confirmar
   que la relación del control es la del lookup `sanic_clienteid`.
4. `GET savedqueries` — confirmar que la vista `Correos autorizados sobre este cliente` existe y que
   el `ViewId` del control apunta a ella.
5. `GET sanic_mppp_tbl_autorizacionclientes?$filter=_sanic_clienteid_value eq <id del Cliente>` — la
   MISMA condición que usaría la subgrilla, para confirmar que hay algo que mostrar.

**Qué tiene que pasar.** El control existe, en una sección titulada "Correos autorizados sobre este
cliente", apuntando a la vista y la relación correctas; la consulta del paso 5 devuelve al menos una
fila.

**Cómo se verifica.** Los cinco pasos de arriba, todos por Web API (`herramientas/pruebas/sets/set_u.py::u08`).

**Cómo se sabe que falló.** El `formxml` no trae ningún control con ese `TargetEntityType` (nunca se
agregó al formulario), la relación o la vista no coinciden (subgrilla mal armada), o el paso 5
devuelve cero filas con datos que sí existen (filtro mal configurado — confundir esto con "no hay
autorizaciones" es el mismo tipo de error que motivó A-15 en `05-app-y-comandos.md`).

**Automatizable.** API.

---

#### U-09 — Subgrilla "Clientes que puede modificar" en el formulario de Autorizado

**Qué prueba.** El pedido textual del aprobador del 2026-09-27: poder entrar por correo y ver con
cuántos CLIENTES está relacionado (antes solo se veían los planes).

**Corregido contra el diseño (este set): pasa de GUI a API**, mismo criterio que U-08.

**Necesita.** Un Autorizado con `AutorizacionCliente` hacia más de un Cliente: `set_u.py::preparar()`
le da a `REMITENTE_SOLOCLIENTE` una segunda `AutorizacionCliente`, hacia un Cliente "ajeno" propio de
este set (ver "Preparativos comunes"). Ninguna sesión de navegador.

**Cómo.**
1. `GET systemforms?$filter=objecttypecode eq 'sanic_mppp_tbl_autorizado' and type eq 2` — el
   formulario principal de Autorizado.
2. Confirmar que la subgrilla existente `Planes que puede modificar` (`AutorizacionPlan`) sigue en
   el `formxml`, intacta.
3. Buscar el control de subgrilla con `TargetEntityType = sanic_mppp_tbl_autorizacioncliente`, su
   sección y su relación (lookup `sanic_autorizadoid`).
4. `GET savedqueries` — confirmar que la vista `Clientes que puede modificar` existe y que el
   `ViewId` del control apunta a ella.
5. `GET sanic_mppp_tbl_autorizacionclientes?$filter=_sanic_autorizadoid_value eq <id del Autorizado>`
   — confirmar que hay AutorizacionCliente hacia MÁS DE UN Cliente distinto.

**Qué tiene que pasar.** Las dos subgrillas conviven en el formulario ("Planes que puede modificar"
sin tocar, "Clientes que puede modificar" nueva); la consulta del paso 5 devuelve al menos dos
clientes distintos.

**Cómo se verifica.** Los cinco pasos de arriba, todos por Web API (`herramientas/pruebas/sets/set_u.py::u09`).

**Cómo se sabe que falló.** Solo aparece el control de planes (el de clientes no se agregó), la
relación o la vista no coinciden, o el paso 5 devuelve menos de dos clientes distintos (el Autorizado
de prueba no está armado como pide el MD, o la subgrilla mostraría menos de lo que realmente tiene
autorizado).

**Automatizable.** API.

---

#### U-10 — Evidencia firmada opcional en `AutorizacionCliente`

**Qué prueba.** El hueco que señaló el aprobador el 2026-09-27 (*"no está la columna de archivo
adjunto... al igual que en autorizacion por plan"*) está cerrado: `sanic_documentofirmado` (archivo,
10 MB, no requerida) y `sanic_fechadocumento` (fecha, no requerida), ambas auditadas, con la MISMA
forma que sus pares de `AutorizacionPlan`.

**Corregido contra el diseño (este set): pasa de GUI a API, y de "inspección" a FUNCIONAL.** No solo
se puede leer la metadata y el `formxml` sin navegador: se puede probar la escritura REAL de la
evidencia (subir el archivo, escribir la fecha, releer) por Web API, igual que `simulador.py` ya
sube `sanic_correocrudo`/`sanic_exceloriginal` a la Solicitud. Es una prueba más fuerte que la
inspección visual: no solo confirma que los controles están en el formulario, confirma que Dataverse
acepta guardarlos.

**Necesita.** Una `AutorizacionCliente` existente sin evidencia cargada (la que arma
`set_u.py::preparar()` para U-02/U-08). Ninguna sesión de navegador.

**Cómo.**
1. `GET EntityDefinitions(LogicalName='sanic_mppp_tbl_autorizacioncliente')/Attributes` — confirmar
   que `sanic_documentofirmado` (`FileType`) y `sanic_fechadocumento` (`DateTimeType`) existen con
   `RequiredLevel = None`.
2. `GET systemforms` — el formulario principal de `AutorizacionCliente`, confirmar que trae la
   sección "Evidencia de la autorizacion" con los dos campos.
3. Leer la `AutorizacionCliente` existente: confirmar que los dos campos están vacíos (se guardó sin
   evidencia y no la exigió).
4. Subir un archivo de prueba a `sanic_documentofirmado`
   (`dv.subir_archivo(".../sanic_documentofirmado", "...", contenido)`, el mismo mecanismo que
   `dataverse_api.py`/`simulador.py` usan para `sanic_correocrudo`) y escribir
   `sanic_fechadocumento` con un `PATCH`.
5. Releer y confirmar que los dos quedaron guardados.

**Qué tiene que pasar.** Los dos campos existen, no son requeridos, y el formulario los muestra;
guardado sin ellos no falla (paso 3); subirlos después SÍ se puede (pasos 4 y 5).

**Cómo se verifica.** Los cinco pasos de arriba, todos por Web API (`herramientas/pruebas/sets/set_u.py::u10`).

**Cómo se sabe que falló.** El guardado sin evidencia se rechaza (se volvieron requeridas por
error), los campos no existen en la metadata o en el `formxml` (el hueco que señaló el aprobador
seguiría abierto), o la subida del archivo / la escritura de la fecha devuelve un error HTTP.

**Automatizable.** API.

---

#### U-11 — Clave alternativa: no se repite el par autorizado + cliente

**Qué prueba.** Igual que `AutorizacionPlan`, la clave alternativa autorizado+cliente impide
duplicar la misma relación.

**Necesita.** Una `AutorizacionCliente` ya existente (autorizado A, cliente X).

**Cómo.** `dataverse_api.py POST sanic_mppp_tbl_autorizacionclientes` con el MISMO par
autorizado+cliente.

**Qué tiene que pasar.** Rechazo por violación de clave alternativa (HTTP 4xx citando la clave
duplicada).

**Cómo se verifica.** El mensaje de error de la llamada.

**Cómo se sabe que falló.** El segundo registro se crea sin problema: la clave alternativa no está
realmente aplicada en el entorno (una cosa es que el playbook la declare, otra que Dataverse la
esté haciendo cumplir).

**Automatizable.** API.

---

### Resultados

Corrida real, `python3 herramientas/pruebas/correr.py U`, 2026-09-27: **8 OK · 3 FALLA · 0 N-A**.

| Caso | Resultado | Notas |
|---|---|---|
| U-01 compatibilidad sin el parámetro | OK | parámetro compartido |
| U-02 modo cliente — entra en los dos niveles | OK | parámetro compartido |
| U-03 modo cliente — solo AutorizacionPlan no alcanza | FALLA — bloqueado por D-59 | parámetro compartido · confirmado: la Solicitud no llega a "No reconocida", `ClasificarCorreo` corta con HTTP 400 |
| U-04 valor basura degrada a plan | FALLA — bloqueado por D-59 en (a) | parámetro compartido · (b) SÍ corrió y pasó (el correo autorizado por plan sigue entrando igual con el valor basura); el caso es FALLA solo por (a), bloqueado por D-59 |
| U-05 cambiar de modo no borra nada | FALLA — bloqueado por D-59 en el paso 2 | parámetro compartido · los pasos 1 y 4 SÍ corrieron y pasaron (el punto real del caso — `AutorizacionPlan` revive sin recargar nada — quedó demostrado); el caso entero es FALLA porque UN `check` en rojo alcanza (CONVENCIONES: "OK si TODO pasó"), y el rojo es el paso 2, bloqueado por D-59 |
| U-06 mismo mensaje en los dos modos | OK | rediseñado (evita D-59), texto byte a byte idéntico confirmado |
| U-07 nombre calculado `<correo> → <cliente>` | OK | |
| U-08 subgrilla en Cliente | OK | API (formxml + metadata, sin navegador) |
| U-09 subgrilla en Autorizado | OK | API (formxml + metadata, sin navegador) |
| U-10 evidencia firmada opcional | OK | API (funcional: subió el archivo y la fecha de verdad, y los releyó) |
| U-11 clave alternativa autorizado+cliente | OK | |

**Próximo paso, una vez resuelto D-59:** U-03/U-04/U-05 no necesitan tocarse — en cuanto
`AvisosDataverse.AvisarAEjecutivos` deje de reventar van a poder terminar de evaluar el resto de sus
`check`s (en U-05, los pasos 1 y 4, que hoy quedan sin correr porque el caso corta al primer defecto
bloqueante). No son casos mal escritos: son casos que ya están probando lo que tienen que probar, y
lo que encuentran es D-59.

---
## 11 — DN-02: descargar a Excel lo que hay que digitar

Cubre el comando *Descargar para digitar*, la Custom API `sanic_mppp_capi_exportarparadigitar`, el
orden de columnas por `exportacion.columnas`, el corte por presupuesto de tiempo, y el riesgo
conocido de memoria a volumen extremo. Fuente: `diseno/DIRECTRICES-NUEVAS.md` DN-02,
`src/Sanic.Mppp.Plugins/Api/ExportarParaDigitar.cs` y `ExportarParaDigitarApi.cs`.

Convención de IDs: prefijo `E-` (**E**xportación), ver el encabezado de `09-rpa-fase2.md`.

### Preparativos comunes a este archivo

Este set es autocontenido (CONVENCIONES §8): no supone ninguna Fila, Solicitud ni configuración que
haya dejado otro set. `herramientas/pruebas/sets/set_e.py` deja listo, en su propio `preparar()`:

- El fixture `PR*` compartido (create-if-missing) y su propio remitente de prueba autorizado sobre
  `PR11` (mismo criterio que `set_s.py`/`set_i.py`).
- Los 15 Excel + el adjunto que no es Excel, en `pruebas/adjuntos/`.
- Que `exportacion.columnas` tenga al menos una versión activa (si no existe ninguna, el set no
  arranca: sembrar el CONJUNTO de columnas es una decisión de negocio, no algo que un set de pruebas
  deba inventar — ver `playbooks/datos/parametros.md`).
- **Su propio fixture de Filas reales**: una Solicitud simulada con `E01-feliz-formato11.xlsx` (tres
  Inclusiones válidas en `PR11` (hasta DN-05 eran Inclusión, Exclusión y Modificación) que pasan todas las reglas y quedan en
  Validada), creada con `ctx.procesar()`. Todos los casos de API que necesitan `filaid`s reales usan
  ESTE fixture, nunca datos de otro set ni una fila cualquiera del entorno.

- `local/sesion-ejecutivo.json` (rol `SR - MPPP - Ejecutivo`) o `local/sesion-supervisor.json`
  (rol `SR - MPPP - Supervisor`) para el caso de GUI — generadas por el aprobador, ver
  `pruebas/gui/README.md`; confirmarlas con `estado_sesiones.py`. El arnés de Playwright de
  `pruebas/gui/` **ya existe y ya cubre la descarga de punta a punta**
  (`casos/dn-02-descargar-para-digitar.spec.mjs`): los casos de acá que dependen de sesión real
  APUNTAN a ese arnés en vez de duplicarlo.
- Ninguno de los casos de acá toca `pruebas/gui/`.

---

#### E-01 — El comando "Descargar para digitar" se dibuja en el ribbon

**Qué prueba.** Que el botón exista de verdad en el DOM, no solo en el servidor — la lección cara
del 2026-09-27 (ver `05-app-y-comandos.md` A-15): un comando puede estar publicado y no dibujarse.

**Necesita.** `local/sesion-ejecutivo.json` (rol `SR - MPPP - Ejecutivo`) o
`local/sesion-supervisor.json` (rol `SR - MPPP - Supervisor`) — cualquiera de las dos sirve, ambas
tienen el privilegio de la Custom API (ver E-09); confirmarla viva y con el rol correcto con
`estado_sesiones.py` antes de correr Playwright.

**Cómo.** Ya está automatizado: `MPPP_USUARIO=ejecutivo npx playwright test dn-02` (o
`ribbon-fila`) desde `pruebas/gui/`.

**Qué tiene que pasar.** El botón "Descargar para digitar" presente y habilitado con filas
seleccionadas.

**Cómo se verifica.** El resultado de esa corrida de Playwright. **CONECTADO el 2026-09-28**
(antes `N-A` con el comando exacto): `herramientas/pruebas/sets/set_e.py::e01()` invoca
`dn-02-descargar-para-digitar` por subproceso (`--reporter=json`, verifica el total de pruebas
ejecutadas contra lo esperado — mismo guardia contra falso verde que `dotnet test` en `set_r.py`) y
reporta OK/FALLA real en el resumen de `correr.py E`, ya no `N-A`.

**Cómo se sabe que falló.** Ver A-15 (`05-app-y-comandos.md`): distinguir AUSENTE (metadata de
ribbon) de DESHABILITADO (regla de habilitación) — son diagnósticos opuestos. También falla si el
archivo `dn-02-descargar-para-digitar` no matchea exactamente 1 prueba (filtro roto, 0 pruebas
corridas con exit 0 — el mismo riesgo que un FQN mal escrito en `dotnet test`).

**Automatizable.** GUI — ya construido en `pruebas/gui/casos/`, no se reescribe acá; conectado a
`set_e.py` el 2026-09-28.

---

#### E-02 — Exportación básica: `archivo` y `exportadas`

**Qué prueba.** El contrato mínimo: la Custom API devuelve un `.xlsx` en base64 y cuántas filas
quedaron en el archivo.

**Necesita.** 2-3 `filaid` reales conocidos.

**Cómo.** `dataverse_api.py POST sanic_mppp_capi_exportarparadigitar
'{"filaids":["<id1>","<id2>"]}'` (con sesión de Ejecutivo/Supervisor o el service principal, según
qué identidad tenga el privilegio — ver E-09).

**Qué tiene que pasar.** `exportadas: 2`, `archivo` no vacío; decodificado de base64, un `.xlsx`
válido (`unzip -l` o `herramientas/pruebas/verificar` si existe una utilidad, o abrirlo con
`openpyxl`/Excel).

**Cómo se verifica.** El campo `exportadas` de la respuesta, y abrir el archivo decodificado.

**Cómo se sabe que falló.** `exportadas` no coincide con la cantidad pedida, o el base64 no decodifica
a un `.xlsx` válido.

**Automatizable.** API.

---

#### E-03 — El orden de columnas sigue `exportacion.columnas`, sin tocar código

**Qué prueba.** El parámetro decide el orden; cambiarlo cambia el Excel sin ningún despliegue.

**Necesita.** El valor de la versión ACTIVA actual de `exportacion.columnas` (lo lee el propio caso).
El caso siembra su propia versión de prueba, con las dos primeras columnas intercambiadas, en
`sanic_version` = la MÁXIMA versión ya sembrada (activa o no) + 1 — nunca edita ni borra ninguna fila
que no sembró él, mismo criterio de versionado que `03-reglas-de-registro.md` R-26 y que
CONVENCIONES §8.2. Usar la máxima sembrada (no solo la activa) importa: si una corrida murió antes de
limpiar y dejó una versión de prueba colgando, "activa + 1" volvería a chocar con esa fila y la clave
alterna `KEY - MPPP - Parámetro - Nombre y versión` rechazaría el alta con HTTP 412.

**Limpieza en DOS pasos (CONVENCIONES §8.2: "desactivar no es limpiar").** El 2026-09-27, tres
corridas de este caso solo desactivaban la versión de prueba y nunca la borraban: quedaron tres
filas colgando en Dev, y la cuarta corrida chocó con la clave alterna (HTTP 412) porque calculaba
la versión nueva contra la activa en vez de contra el máximo sembrado. Basura que se acumula no es
inofensiva. Por eso la limpieza real son dos pasos, en dos momentos distintos:

1. **Desactivarla de inmediato**, en el `finally` de `e03()` — devuelve la precedencia al canon sin
   esperar a que el set entero termine, pase lo que pase con el resto del caso.
2. **Borrarla por GUID**, en `cerrar()` — nunca por nombre ni por versión (así fue como se borraron
   datos ajenos el 2026-09-27, CONVENCIONES §5). `cerrar()` relee la fila antes de cada DELETE y
   confirma que sigue desactivada y que trae la marca `ZZP -` de este caso; si no, NO la borra y
   reporta el motivo en vez de arriesgar borrar algo que no es de este set.

**Cómo (lo hace el caso, `e03()` + `cerrar()` de `set_e.py`).**
1. Exportar 1 fila del fixture propio con el orden actual, anotar los encabezados del `.xlsx`.
2. Sembrar una versión NUEVA de `exportacion.columnas` (máxima sembrada + 1) con las dos primeras
   columnas intercambiadas — nunca tocar ninguna fila existente.
3. Exportar la misma fila de nuevo.
4. En el `finally` de `e03()`, sea cual sea el resultado: DESACTIVAR (`statecode=1`) la versión de
   prueba que sembró en el paso 2 (paso 1 de la limpieza). La versión anterior queda intacta y
   vuelve a ser la activa más alta — ese es el canon real de este parámetro, y por eso no hace falta
   "restaurar" ningún valor.
5. En `cerrar()` (corre siempre, al final del set): releer esa misma fila por GUID, confirmar que
   sigue desactivada y marcada, y BORRARLA (paso 2 de la limpieza).

**Qué tiene que pasar.** El orden de columnas del paso 3 refleja el cambio del paso 2, sin haber
tocado el ensamblado. Al terminar el SET (no el caso: la fila vive hasta `cerrar()`), no queda
ninguna versión de prueba de `exportacion.columnas` en el entorno — ni activa ni desactivada.

**Cómo se verifica.** Comparar los encabezados del `.xlsx` de los pasos 1 y 3; y, después de correr
el set completo (`correr.py`, no solo este caso), que `sanic_mppp_tbl_parametros` filtrado por
`sanic_nombre eq 'exportacion.columnas'` solo devuelve la versión canónica.

**Cómo se sabe que falló.** El orden no cambia (el código está usando un orden fijo en vez de leer
el parámetro). Aparte, si después de una corrida completa del set queda una fila de PRUEBA (activa o
desactivada) de `exportacion.columnas`: la limpieza de dos pasos no funcionó — ver `cerrar()` en
`set_e.py`, que reporta el motivo en vez de fallar en silencio.

**Automatizable.** API.

---

#### E-04 — `filaids` vacío: archivo válido con cero filas de datos

**Qué prueba.** Un arreglo vacío no es un error: se exporta un archivo con solo el encabezado.

**Necesita.** Nada más que la Custom API sembrada.

**Cómo.** `dataverse_api.py POST sanic_mppp_capi_exportarparadigitar '{"filaids":[]}'`.

**Qué tiene que pasar.** HTTP 200, `exportadas: 0`, `archivo` es un `.xlsx` válido (con encabezado,
sin filas de datos).

**Cómo se verifica.** El código HTTP y el archivo decodificado.

**Cómo se sabe que falló.** Un error 4xx sobre un arreglo vacío (se estaría tratando como caso
inválido cuando el contrato dice que es válido).

**Automatizable.** API.

---

#### E-05 — Un `filaid` inexistente revienta LA LLAMADA ENTERA, nombrando cuáles

**Qué prueba.** Decisión del aprobador (2026-09-26): nunca se omite en silencio una fila pedida que
no existe — un archivo "completo" con una fila de menos es peor que un rechazo ruidoso.

**Necesita.** Un `filaid` real y un GUID inventado.

**Cómo.** `dataverse_api.py POST sanic_mppp_capi_exportarparadigitar
'{"filaids":["<id real>","00000000-0000-0000-0000-000000000000"]}'`.

**Qué tiene que pasar.** Rechazo (no HTTP 200) con un mensaje que cita el GUID inventado y dice
"volvé a seleccionar y descargá de nuevo".

**Cómo se verifica.** El mensaje de error de la llamada.

**Cómo se sabe que falló.** La llamada devuelve 200 con `exportadas: 1` (la fila inexistente se
omitió en silencio): es exactamente el riesgo de integridad que el aprobador detectó y que motivó
sacar el tope de 500 filas.

**Automatizable.** API.

---

#### E-06 — Columnas protegidas (identificación, cuenta) salen igual, sin la protección

**Qué prueba.** DN-02 punto 3-4: el Excel decide qué sale por el PARÁMETRO, no por el perfil de
columna — el archivo descargado NO lleva encima la protección de `CSP - MPPP - Datos sensibles`.

**Necesita.** Una Fila con `numeroidentificacion` y `numerocuenta` reales (ficticios); confirmar
que esos campos están en `exportacion.columnas`.

**Cómo.** Exportar esa fila y abrir el `.xlsx`.

**Qué tiene que pasar.** Los valores de `numeroidentificacion` y `numerocuenta` aparecen en CLARO
en el archivo, sin enmascarar.

**Cómo se verifica.** Inspección del archivo.

**Cómo se sabe que falló.** Los valores vienen enmascarados o vacíos (alguien agregó una protección
que el diseño explícitamente NO pide — y que dejaría al ejecutivo sin poder digitar en AS400), o al
revés, si el negocio decide sacar esas columnas del parámetro y siguen apareciendo (el parámetro
dejó de mandar).

**Automatizable.** API (con inspección del archivo descargado).

---

#### E-07 — Corte por presupuesto de tiempo (antes de que la plataforma reviente)

**Qué prueba.** `ExportarParaDigitar.Ejecutar` corta él mismo a los ~100 s (100 s de margen sobre
el presupuesto síncrono de 120 s), con un mensaje que dice qué hacer — no un error genérico de
plataforma.

**Necesita.** Un volumen de `filaids` lo bastante grande. **Importante, según la medición real ya
hecha (`DIRECTRICES-NUEVAS.md`, tabla de 2026-09-26): con datos reales de Dev, ni 250.000 posiciones
de `filaids` llegaron a acercarse al umbral de 100 s (tardó 40,6 s)** — el corte propio del plugin
NO se ha observado disparar contra un volumen real en este entorno. Repetirlo hoy con más volumen
tiene el mismo riesgo que E-08 (ver abajo). La cobertura confiable y repetible de ESTE mensaje
puntual es la unitaria, con el reloj inyectado (`ExportarParaDigitarApiAceptacion`,
`RelojParaPruebas`), que si fuerza el corte sin esperar casi 2 minutos de verdad.

**Cómo (solo si hace falta la confirmación end-to-end, y con las mismas prevenciones de E-08).**
Repetir `filaids` reales hasta un volumen entre 250.000 y 300.000 posiciones (recordar: el costo se
paga por POSICIÓN, no por fila distinta — `FilasDataverse.PorIds` deduplica antes de leer).

**Qué tiene que pasar.** Rechazo con `PresupuestoDeExportacionAgotandoseException`: menciona cuántas
filas alcanzó a preparar y pide volver a seleccionar menos.

**Cómo se verifica.** El mensaje de la llamada.

**Cómo se sabe que falló.** El mensaje no aparece y en cambio la plataforma corta con un error
técnico crudo (ver E-08: a partir de cierto volumen, es justamente lo que pasa, y es un riesgo
aceptado, no una regresión de este caso).

**Automatizable.** Unit (confiable, ya existe: `tests/Sanic.Mppp.Plugins.Tests/Aceptacion/ExportarParaDigitarApiAceptacion.cs`,
seam `RelojParaPruebas`) / API — pero en `set_e.py` este caso queda **SIEMPRE N-A, sin excepción ni
bandera**: no hay hoy un volumen conocido que dispare el mensaje elegante sin correr el mismo riesgo
de E-08 (arriba de ~250k, antes de llegar a los 100s del corte propio, el sandbox puede morir por
memoria — ver E-08). Forzarlo por API no agrega cobertura real sobre la unitaria, solo riesgo.

---

#### E-08 — Riesgo conocido: ~300.000 filas mueren por memoria/sandbox ANTES del mensaje elegante

**Qué prueba.** Documenta el límite real medido el 2026-09-26 (`DIRECTRICES-NUEVAS.md`): entre
250.000 (sano, 40,6 s) y 300.000 `filaids` (falla), el proceso sandbox de la plataforma se cae por
agotamiento de memoria/hosts ANTES de que el chequeo de tiempo del plugin tenga otra vuelta de loop
para cortar con su propio mensaje. A 300.000 el error real fue
`SandboxFabricHostCommunicationException`; a 500.000, `System.OutOfMemoryException`. **Esto es un
riesgo ACEPTADO, no un defecto a corregir en esta fase**: ninguna selección real de este negocio se
acerca a ese volumen (7.500 filas/mes en producción). Resumen del borde medido: **100.000 filas OK
· 250.000 OK en 40,6s · 300.000 revienta por memoria del sandbox**, y revienta ANTES de que el
plugin llegue a su propio corte de 100s — por eso el mensaje elegante de E-07 no sale a ese
volumen. E-08 documenta ese borde, no lo descubre.

**Corrección 2026-09-28.** Decisión del aprobador: *"no importa que degrade el ambiente, en el modo
FULL debería correr esta prueba"*. `correr.py --full` prende `MPPP_E08_ALTO_RIESGO` junto con las
banderas de V — ver `herramientas/pruebas/correr.py --listar` y su docstring. La bandera suelta
sigue funcionando igual: `--full` no la rompe, solo la reemplaza como forma recomendada.

**Necesita.** **NO CORRER DE RUTINA.** Esta llamada tarda ~150-170 s y puede degradar el sandbox de
Dataverse para otros usuarios de Dev mientras corre. Solo se repite si `ExportarParaDigitar.cs`
cambia de forma que pudiera afectar este límite (por ejemplo, si se agrega el chequeo de memoria
que quedó como riesgo abierto). Si se corre: avisar a todo el equipo, y NO en horario de otras
pruebas.

En `set_e.py`, `e08()` está detrás de una bandera explícita: por default da **N-A** con el motivo y
el comando exacto para correrlo a propósito. Solo ejecuta la llamada real con la variable de entorno
puesta ANTES de invocar al motor:

```bash
MPPP_E08_ALTO_RIESGO=si python3 herramientas/pruebas/correr.py E
python3 herramientas/pruebas/correr.py E --full   # equivalente, y la forma recomendada desde 2026-09-28
```

Sin esa variable (una corrida normal, `--todos` incluido), el caso NUNCA hace la llamada de 300.000
`filaids`: un set que degrada el entorno cada vez que alguien lo corre es un set que nadie va a
correr.

**Cómo (solo si se decide repetir la medición, con la bandera de arriba).** Repite un `filaid` real
(del fixture propio de este set) 300.000 veces en el arreglo `filaids` de una sola llamada a
`exportarparadigitar` (el costo se paga por POSICIÓN, no por fila distinta: `FilasDataverse.PorIds`
deduplica antes de leer).

**Qué tiene que pasar hoy.** Falla con un error de plataforma (no el mensaje elegante de E-07): eso
es lo ESPERADO y documentado, no un hallazgo nuevo.

**Cómo se verifica.** El tipo de excepción en la respuesta/log.

**Cómo se sabe que "falló" (en el sentido de que empeoró).** Si el volumen al que revienta bajó
significativamente respecto de la medición de referencia (250k sano / 300k+ revienta): indicaría
una regresión de memoria en el código nuevo, y ahí sí amerita investigar.

**Automatizable.** API, pero detrás de la bandera `MPPP_E08_ALTO_RIESGO=si` (o `--full`, ver
"Necesita" arriba) — alto costo, no forma parte de una corrida de regresión habitual ni de
`--todos` (`--todos` solo, sin `--full`, tampoco la prende).

---

#### E-09 — Privilegio de la Custom API: solo Ejecutivo y Supervisor

**Qué prueba.** A diferencia de las Custom API del RPA (puerta en código, D-9), esta usa
`ExecutePrivilegeName = prvReadsanic_mppp_tbl_fila` — un privilegio de plataforma que YA tienen
Ejecutivo y Supervisor, y que Administrador de planes/técnico NO tienen.

**Necesita.** No hace falta abrir un navegador: se resuelve cada usuario de prueba por su alias
(`admin_planes` = `User3`, rol `SR - MPPP - Administrador de planes`; `admin_tecnico` = `User4`, rol
`SR - MPPP - Administrador tecnico`), se verifica el rol contra `systemuserroles_association` (nunca
supuesto — CONVENCIONES §8) y se impersona por `MSCRMCallerID`, el mismo mecanismo de
`comportamiento_roles.py` y de `set_i.py` (`_usuario_por_alias` + `pc.verificar_roles`, igual que
I-13). **Este caso NO es GUI**: no usa `local/sesion-*.json` (esos son `storageState` de Playwright,
para el arnés de `pruebas/gui/`), sino impersonación por API.

**Importante:** la llamada impersonada usa un `filaid` REAL, pero del fixture propio de este set (el
mismo que crea `preparar()` con `E01-feliz-formato11.xlsx`) — nunca una fila cualquiera del entorno.
El privilegio se evalúa a nivel de MENSAJE antes de tocar ningún registro (`ExecutePrivilegeName`),
así que usar un id real y propio prueba lo mismo que un arreglo vacío, pero además confirma que el
rechazo llega ANTES de leer la fila, sin depender de datos de otro set.

**Cómo (`e09()` de `set_e.py`).** Por cada papel (`admin_planes`, `admin_tecnico`): resolver su
`systemuserid` por alias, verificar su rol, y llamar
`sanic_mppp_capi_exportarparadigitar` con `MSCRMCallerID` de ese usuario y
`{"filaids": ["<filaid propio del fixture>"]}`.

**Qué tiene que pasar.** Las dos llamadas rechazadas por falta de privilegio (error de plataforma,
no del plugin).

**Cómo se verifica.** El código HTTP (403) y el mensaje de la plataforma.

**Cómo se sabe que falló.** Alguno de los dos roles SÍ puede exportar: el registro real de la
Custom API (tarea de `pac`/Dev, fuera de este plugin) no quedó con el privilegio correcto.

**Automatizable.** API.

---

#### E-10 — `filaids` es un StringArray, no un JSON serializado

**Qué prueba.** El parámetro calza con `SelectedControlSelectedItemIds` del ribbon SIN
transformación: es un arreglo de texto nativo de la Custom API, no un string con JSON adentro. Un
elemento que no es un GUID válido nombra la POSICIÓN exacta.

**Necesita.** Nada especial.

**Cómo.**
1. `dataverse_api.py POST sanic_mppp_capi_exportarparadigitar '{"filaids": "no-es-un-arreglo"}'`
   (mandado como string en vez de arreglo).
2. `dataverse_api.py POST sanic_mppp_capi_exportarparadigitar '{"filaids": ["<id real>", "no-es-un-guid"]}'`.

**Qué tiene que pasar.** Paso 1: rechazo diciendo que tiene que ser un StringArray. Paso 2: rechazo
que nombra la posición 2 como inválida.

**Cómo se verifica.** El mensaje de error de cada llamada.

**Cómo se sabe que falló.** Un mensaje genérico que no dice CUÁL posición está mal: con un arreglo
de cientos de ids seleccionados en la grilla, encontrar el culpable a mano sería inviable.

**Automatizable.** API.

---

### Resultados

Última corrida real: `python3 herramientas/pruebas/correr.py A,E`, 2026-09-28. **8 OK · 0 FALLA · 2
N-A.** Formato por caso: OK / FALLA (con el motivo o el ID del defecto) / N-A (con el motivo) —
CONVENCIONES §3. **E-01 dejó de ser `N-A`** desde el 2026-09-28: tiene su caso de arnés en
`pruebas/gui/` (`dn-02-descargar-para-digitar`) y `set_e.py::e01()` lo invoca por subproceso, igual
que el set R invoca `dotnet test` — ya no hace falta correrlo aparte. Los 2 `N-A` que quedan
(**E-07** y **E-08**) no son un gap: son un riesgo medido y aceptado por el aprobador, documentado
en su propio caso más abajo, no un límite de arnés.

| Caso | Resultado | Notas |
|---|---|---|
| E-01 el botón se dibuja | OK | GUI, `dn-02-descargar-para-digitar` invocado por `set_e.py::e01()`, ya no `N-A` |
| E-02 exportación básica | OK | |
| E-03 orden de columnas por parámetro | OK | |
| E-04 `filaids` vacío | OK | |
| E-05 `filaid` inexistente revienta entero | OK | |
| E-06 columnas protegidas salen en claro | OK | |
| E-07 corte por presupuesto de tiempo | N-A | siempre N-A en `set_e.py`, sin excepción ni bandera — ni 250k `filaids` reales llegaron a disparar el corte propio (40,6 s); cobertura real es la unitaria (`ExportarParaDigitarApiAceptacion`, `RelojParaPruebas`) |
| E-08 riesgo de memoria a ~300k | N-A | N-A por default; solo se ejecuta con `MPPP_E08_ALTO_RIESGO=si` (o `--full`) — riesgo medido y aceptado, no corrido de rutina por el costo de degradar el sandbox |
| E-09 privilegio de la Custom API | OK | |
| E-10 `filaids` StringArray, posición del error | OK | |

---
## 12 — DN-04: carga masiva y activación masiva de catálogos por Excel, y Comentarios

Set **L** (`CONVENCIONES.md` §4: **L**ote — cargar y cambiar el estado de los catálogos *en lote*, desde
un Excel). Cubre los dos menús de las grillas de catálogos ("Carga masiva" y "Activar/Desactivar
masivo"), las plantillas, las dos Custom API (`sanic_mppp_capi_cargarcatalogos` y
`sanic_mppp_capi_cambiarestadocatalogos`), el catálogo de códigos de archivo `MPPP-CAR-001` a `011`,
las reglas por fila de los dos Excel, el todo o nada, la idempotencia, el dueño del cliente nuevo
(D-73) y la columna **Comentarios** de la autorización por cliente. Fuente de verdad del
comportamiento: `diseno/DIRECTRICES-NUEVAS.md`, sección DN-04 completa, y `diseno/PENDIENTES.md`,
filas D-73, D-74 y D-75. Código: `src/Sanic.Mppp.Plugins/Api/CatalogosMasivosApi.cs` y
`src/Sanic.Mppp.Plugins/Catalogos/`.

Convención de IDs: prefijo `L-`.

### Cómo leer el estado de cada caso

Cada caso de abajo es **el diseño escrito como prueba**. Su **estado** no está en el caso sino en la
tabla `## Resultados` del final, con la evidencia citada. Hay tres estados, y **no se mezclan**:

| Estado | Qué significa | Qué NO significa |
|---|---|---|
| **EJECUTADO — OK** | Se corrió contra Dev (o con el navegador real) y pasó. La evidencia está citada: qué se corrió, cuándo, con qué ensamblado | — |
| **CUBIERTO POR PRUEBAS AUTOMÁTICAS** | Lo ejercita la suite de C# (`tests/Sanic.Mppp.Plugins.Tests`, 1580 en verde al 2026-10-01), con el archivo de aceptación citado. **No se corrió en Dev** | **No es un OK.** `CONVENCIONES.md` §3 solo define OK / FALLA / N-A para una corrida real; este estado dice "hay una prueba que lo cubre", no "se vio funcionar en el entorno". Un doble de pruebas no reproduce la plataforma (D-73 lo dejó escrito) |
| **PENDIENTE** | Nadie lo corrió. Se corre en Dev | — |

**El set corre con `python3 herramientas/pruebas/correr.py L`** (`herramientas/pruebas/sets/set_l.py`, desde el
2026-10-02). Junta tres fuentes de evidencia, cada una corrida una sola vez por set: la suite de C# (resultado por
prueba, con TRX), el spec de Playwright `pruebas/gui/casos/dn-04-catalogos-masivos.spec.mjs` (una corrida por rol; un
test saltado nunca cuenta como aprobado) y la Web API con las sesiones reales de los cuatro roles. En la salida de
`correr.py`, un caso "cubierto por pruebas automáticas" da OK cuando TODAS las pruebas de C# que cita existen y pasan;
la tabla de `## Resultados` sigue distinguiendo ese estado de un OK con evidencia de Dev.

### Preparativos comunes a este archivo

Este set es autocontenido (`CONVENCIONES.md` §8): no supone ningún cliente, plan, correo ni
autorización que haya dejado otro set. Lo que `set_l.py` tiene que dejar listo en su `preparar()`:

- **Sesiones y roles verificados, no supuestos**: `local/sesion-admin_planes.json` (`# User 3`,
  `SR - MPPP - Administrador de planes`), `local/sesion-ejecutivo.json` (`# User 1`,
  `SR - MPPP - Ejecutivo`), `local/sesion-supervisor.json` (`# User 2`) y
  `local/sesion-admin_tecnico.json` (`# User 4`), confirmadas con
  `python3 herramientas/pruebas/estado_sesiones.py` antes de correr un caso GUI y con
  `systemuserroles_association` para los casos de API (`CONVENCIONES.md` §6 y §8).
- **Quién llama la Custom API.** Los casos de API se corren **como el Administrador de planes**, no
  con el service principal: la Custom API escribe con el usuario que llama (DN-04, "Mecanismo") y
  tiene como puerta el privilegio `prvCreatesanic_mppp_tbl_cliente`, que el service principal no
  garantiza. O la sesión real de `# User 3`, o `MSCRMCallerID` de `# User 3` (mismo mecanismo que
  `comportamiento_roles.py` y `E-09`).
- **Parámetros globales** (`CONVENCIONES.md` §8.2): `carga.filas.maximo` en **100** (versión 2, activa,
  D-75) y `lectura.limites` vigente. El set los verifica al arrancar y no los toca, salvo el caso que
  siembra su propia versión de prueba (versión = máximo sembrado + 1, desactivada y luego borrada
  por GUID en `cerrar()`).
- **El ejecutivo dueño.** El correo de `# User 1` se **lee de `systemusers`** por alias, nunca se
  escribe a mano en el archivo. Un usuario sin el rol Ejecutivo: `# User 2` (Supervisor).
- **Los archivos.** Lo único que existe hoy en el repositorio son los dos del arnés GUI:
  `pruebas/gui/datos/dn04/dn04-carga-con-error.xlsx` y `dn04-carga-hoja-equivocada.xlsx`. Los demás se
  arman al escribir `set_l.py` (hoja `CargaCatalogos`, 12 columnas A a L; hoja `ActivarDesactivar`,
  7 columnas A a G; encabezados en la fila 1, datos desde la fila 2).
- **Marca de agua y limpieza** (`CONVENCIONES.md` §5): todo lo que cree un caso lleva `ZZP-` en la razón
  social del cliente y en el nombre del contacto del correo; los CIF y los códigos de plan se
  eligen en rangos reservados del set. La limpieza es por GUID, en orden inverso a la creación
  (autorizaciones → planes → correos → clientes), releyendo la marca antes de cada `DELETE` con
  `marca_agua.borrar_marcado()`. **Un caso que carga por la Custom API no deja nada sin borrar.**
- **Cómo se lee un `resultado`**: la respuesta de las dos Custom API trae `resultado` en base64; se
  decodifica a `.xlsx` y se lee la columna **Resultado** (**L** en `CargaCatalogos`, **G** en
  `ActivarDesactivar`) de la fila que el caso nombra, con `openpyxl`. El número de fila del archivo de
  resultado es el mismo que el del archivo subido.
- **Tablas, para las verificaciones**: `sanic_mppp_tbl_clientes` (`sanic_cifbac`, `sanic_cifcom`,
  `sanic_nombre` = razón social, `_ownerid_value`, `_createdby_value`, `statecode`),
  `sanic_mppp_tbl_plans` (`sanic_codigo`, `sanic_tipoformato`, `sanic_moneda`, `_sanic_clienteid_value`),
  `sanic_mppp_tbl_autorizados` (`sanic_nombre` = el correo, `sanic_nombrecontacto`) y
  `sanic_mppp_tbl_autorizacionclientes` (`_sanic_clienteid_value`, `_sanic_autorizadoid_value`,
  `sanic_comentarios`, `sanic_documentofirmado`, `sanic_fechadocumento`).

---

### Grupo 1 — Menús, plantillas y flujo del navegador

#### L-01 — El Administrador de planes ve los dos menús, con sus dos opciones, en las cuatro grillas

**Qué prueba.** DN-04, "Interfaz": "Carga masiva" y "Activar/Desactivar masivo" son menús
desplegables (`FlyoutAnchor`) que viven en la grilla principal de Clientes, Planes, Correos
Autorizados y Autorizaciones - Clientes, y cada uno trae *Cargar archivo* y *Descargar plantilla*.

**Necesita.** `local/sesion-admin_planes.json` (`# User 3`, `SR - MPPP - Administrador de planes`),
viva y con el rol correcto según `estado_sesiones.py`. No necesita datos: las grillas pueden estar
vacías.

**Cómo.** Desde `pruebas/gui/`: `MPPP_USUARIO=admin_planes npx playwright test dn-04`. Por cada una de
las cuatro grillas (entradas de sitemap "Clientes", "Planes", "Correos Autorizados" y
"Autorizaciones - Clientes", área Catálogos): abrir la grilla, abrir cada menú y confirmar que
trae las dos opciones.

**Qué tiene que pasar.** En cada grilla, el menú "Carga masiva" y el menú "Activar/Desactivar masivo"
están presentes; cada uno, al abrirse, lista "Cargar archivo" y "Descargar plantilla".

**Cómo se verifica.** El resultado de `MPPP_USUARIO=admin_planes npx playwright test dn-04`: las
cuatro pruebas `<grilla>: los dos menús están y cada uno trae sus dos opciones`.

**Cómo se sabe que falló.** Dos causas opuestas con el mismo síntoma (`CONVENCIONES.md` §1, la lección
del botón fantasma, `05-app-y-comandos.md` A-15). **Si el ancla falta de la barra**: metadata
compilada del ribbon sin regenerar (se confirma con el Command Checker, `&ribbondebug=true`) — no es
un defecto de definición. **Si el ancla está pero la regla la oculta**: `esAdministradorDePlanes` dio
`false`, y lo primero es `estado_sesiones.py` (¿la sesión sigue siendo `# User 3` con su rol?). Si el
menú se ve pero dice "LabelText", las `$LocLabels` volvieron a un `FlyoutAnchor` y el texto fijo se
perdió (D-74).

**Automatizable.** GUI — sesión `admin_planes`. Ya construido en
`pruebas/gui/casos/dn-04-catalogos-masivos.spec.mjs`.

---

#### L-02 — El Ejecutivo no ve ninguno de los dos menús, en ninguna de las cuatro grillas

**Qué prueba.** DN-04, "Interfaz": los menús los ve **solo** el Administrador de planes; el Ejecutivo
solo lee catálogos. La regla es `Sanic.Mppp.Comandos.esAdministradorDePlanes`.

**Necesita.** `local/sesion-ejecutivo.json` (`# User 1`, `SR - MPPP - Ejecutivo`), viva y con el rol
correcto. Esto es seguridad: **una prueba que pasa porque el usuario no ve algo es indistinguible de
una que pasa porque la sesión está muerta** (`CONVENCIONES.md` §6). Por eso la guarda previa
`estado_sesiones.py` es obligatoria, y el caso se acompaña de L-01, que demuestra que en la misma
pantalla el Administrador de planes SÍ los ve.

**Cómo.** Desde `pruebas/gui/`: `MPPP_USUARIO=ejecutivo npx playwright test dn-04`. En cada una de las
cuatro grillas, buscar los dos menús en la barra de la página.

**Qué tiene que pasar.** Ni "Carga masiva" ni "Activar/Desactivar masivo" aparecen en ninguna de las
cuatro grillas.

**Cómo se verifica.** El resultado de `MPPP_USUARIO=ejecutivo npx playwright test dn-04`: las cuatro
pruebas `<grilla>: un Ejecutivo no ve los menús de carga ni de activación masiva`.

**Cómo se sabe que falló.** Si el Ejecutivo ve un menú: `esAdministradorDePlanes` devolvió `true` para
un usuario que no tiene el rol exacto `SR - MPPP - Administrador de planes` (revisar
`Xrm.Utility.getGlobalContext().userSettings.roles` en la consola). Aunque eso pase, la puerta real
es el privilegio de la Custom API (L-51): el menú visible no le daría acceso, pero es un defecto de
interfaz. Si la prueba pasa pero la sesión estaba vencida: es un falso verde; por eso la guarda.

**Automatizable.** GUI — sesión `ejecutivo`. Ya construido en
`pruebas/gui/casos/dn-04-catalogos-masivos.spec.mjs`.

---

#### L-03 — El Supervisor y el Administrador técnico tampoco ven los menús

**Qué prueba.** El mismo diseño que L-02, para los otros dos roles que no administran catálogos. La
regla dice que solo el rol exacto `SR - MPPP - Administrador de planes` los ve; el set del arnés GUI
solo cubre al Ejecutivo.

**Necesita.** `local/sesion-supervisor.json` (`# User 2`, `SR - MPPP - Supervisor`) y
`local/sesion-admin_tecnico.json` (`# User 4`, `SR - MPPP - Administrador tecnico`), vivas y con su
rol. El Administrador técnico **no tiene ningún privilegio sobre las cinco tablas de catálogo** (`diseno/04`,
matriz: "—" en Cliente, Plan, Autorizado, AutorizacionPlan y AutorizacionCliente) y su sitemap no tiene
el área Catalogos: el caso se resuelve por la ausencia de las grillas, y se anota cuáles no se pudieron
abrir en vez de darlas por verdes. *(Corregido el 2026-10-02: decía solo "no ve Clientes ni
Autorizados"; la corrida mostró que no ve ninguna, y es lo que dice la matriz.)*

**Cómo.** Con cada sesión, abrir cada grilla de catálogo que el rol pueda abrir y buscar los dos menús
(mismos selectores que `dn-04-catalogos-masivos.spec.mjs`, que hoy filtra por `MPPP_USUARIO`).

**Qué tiene que pasar.** Ningún menú en ninguna grilla que el rol pueda abrir.

**Cómo se verifica.** Una corrida del mismo spec con `MPPP_USUARIO=supervisor` y
`MPPP_USUARIO=admin_tecnico` una vez que el spec incluya esos dos roles (hoy `test.skip` los saltea a
los dos: el arnés no los cubre).

**Cómo se sabe que falló.** Un menú visible = `esAdministradorDePlanes` mal escrita. Una grilla que no
abre = es el recorte de sitemap del rol (`G-07`), no un defecto de este caso: se anota, no se da por
verde.

**Automatizable.** GUI — sesiones `supervisor` y `admin_tecnico`. Falta extender el spec.

---

#### L-04 — Los menús siguen el idioma de la interfaz

**Qué prueba.** Que los dos menús desplegables (`FlyoutAnchor`) y sus opciones llevan **etiquetas localizadas**
(`$LocLabels`, un texto por idioma), como cualquier botón del proyecto (`01` §0, multiidioma desde que nace).
*Reescrito el 2026-10-02 (D-74 cerrada):* hasta ese día este caso exigía texto fijo en español, porque el 2026-10-01
un menú con `$LocLabels` mostró "LabelText". El experimento controlado probó que eso fue metadata del ribbon sin
regenerar: con la metadata regenerada después de importar, un menú localizado mostró "Bulk load" en una interfaz en
inglés, y el menú de control con texto fijo siguió en español.

**Necesita.** `local/sesion-admin_planes.json`, con la interfaz del usuario en inglés (1033, el único idioma instalado en
Dev). La metadata del ribbon regenerada DESPUÉS de la última importación del ribbon.

**Cómo.** Abrir la grilla Clientes y leer el texto del ancla de cada menú y de sus dos opciones; en la misma barra, el
de un botón nativo de la plataforma ("New"), como control de que la interfaz está en inglés.

**Qué tiene que pasar.** Con la interfaz en inglés, el texto es exactamente "Bulk load", "Bulk activate/deactivate",
"Upload file" y "Download template" (los 1033 de `playbooks/importacion/comandos.md`), y el botón nativo dice "New".
Con la interfaz en español, "Carga masiva", "Activar/Desactivar masivo", "Cargar archivo" y "Descargar plantilla".

**Cómo se verifica.** El texto de los elementos del menú en el DOM, comparado EXACTO (no con el patrón de dos idiomas
del resto del spec, que no distinguiría un menú localizado de uno con texto fijo).

**Cómo se sabe que falló.** El menú dice el español con la interfaz en inglés: volvió el texto fijo. Dice "LabelText" o
una referencia `$LocLabels` cruda: la metadata del ribbon no se regeneró después de la última importación.

**Automatizable.** GUI — sesión `admin_planes`. La mitad en español (3082) es N/A: Dev tiene solo 1033 instalado, e
instalar un idioma cambia el entorno (no lo hace un set de pruebas).

---

#### L-05 — "Descargar plantilla" de Carga masiva baja `CargaCatalogos.xlsx` vacía, con sus encabezados

**Qué prueba.** DN-04, "Interfaz" y "Abierto B": la opción llama a la Custom API con
`plantilla = true` y descarga la plantilla vacía, con el indicador "Generando la plantilla…". La
plantilla sale de la **misma definición de columnas que valida el archivo**.

**Necesita.** `local/sesion-admin_planes.json`, viva y con el rol correcto.

**Cómo.** `MPPP_USUARIO=admin_planes npx playwright test dn-04`: en la grilla Clientes, "Carga masiva"
→ "Descargar plantilla", capturar la descarga y leer el `.xlsx`.

**Qué tiene que pasar.** El archivo se llama `CargaCatalogos.xlsx`; la hoja `CargaCatalogos` trae en la
fila 1, en este orden: CIF BAC, CIF COM, Razon social, Correo ejecutivo, Codigo plan, Tipo formato,
Moneda, Correo, Nombre contacto, Fecha documento, Comentarios, **Resultado** (columna L); no trae
ninguna fila de datos.

**Cómo se verifica.** La prueba `'Carga masiva' → 'Descargar plantilla' baja la plantilla vacía con sus
encabezados` del spec (`suggestedFilename()`, encabezados contra la lista de 12, filas vacías).

**Cómo se sabe que falló.** No hay descarga: el indicador quedó abierto o la Custom API falló con
`plantilla = true` (se ve el código `MPPP-CAR-0xx` en el diálogo). Encabezados distintos: la
plantilla y el validador dejaron de compartir definición, y eso rompe la garantía de "Abierto B".

**Automatizable.** GUI — sesión `admin_planes`. Ya construido en
`pruebas/gui/casos/dn-04-catalogos-masivos.spec.mjs`.

---

#### L-06 — "Descargar plantilla" de Activar/Desactivar baja `ActivarDesactivar.xlsx` vacía, con sus encabezados

**Qué prueba.** Lo mismo que L-05 para la segunda Custom API
(`sanic_mppp_capi_cambiarestadocatalogos`, `plantilla = true`).

**Necesita.** `local/sesion-admin_planes.json`, viva y con el rol correcto.

**Cómo.** `MPPP_USUARIO=admin_planes npx playwright test dn-04`: en la grilla Clientes,
"Activar/Desactivar masivo" → "Descargar plantilla".

**Qué tiene que pasar.** El archivo se llama `ActivarDesactivar.xlsx`; la hoja `ActivarDesactivar`
trae en la fila 1: Tipo, Accion, CIF BAC, CIF COM, Codigo plan, Correo, **Resultado** (columna G);
sin filas de datos.

**Cómo se verifica.** La prueba `'Activar/Desactivar masivo' → 'Descargar plantilla' baja la plantilla
vacía con sus encabezados` del spec.

**Cómo se sabe que falló.** Igual que L-05. Además: si baja `CargaCatalogos.xlsx` desde este menú, los
dos comandos del ribbon quedaron apuntando a la misma función del JS.

**Automatizable.** GUI — sesión `admin_planes`. Ya construido en
`pruebas/gui/casos/dn-04-catalogos-masivos.spec.mjs`.

---

#### L-07 — Plantilla, archivo de resultado y archivo corregido tienen la misma forma: se baja, se corrige y se vuelve a subir tal cual

**Qué prueba.** DN-04, "Excel de resultado" (precisado el 2026-10-01): la columna **Resultado** es parte
de la plantilla (L en `CargaCatalogos`, G en `ActivarDesactivar`), se exige como encabezado y **nunca
se lee**. Sin esto, el diálogo diría "corregí y volvé a subirlo" y el modo estricto rechazaría el
archivo corregido por traer una columna de más. También: una plantilla subida vacía falla solo por
`MPPP-CAR-007` (sin filas), nunca por encabezados ("Abierto B").

**Necesita.** Un archivo de carga con un error de fila (por ejemplo, la fila 2 sin CIF) y su plantilla
vacía. Sesión de Administrador de planes.

**Cómo.**
1. Subir la plantilla vacía de `CargaCatalogos` tal cual se bajó.
2. Subir el archivo con el error; guardar el `resultado`.
3. Sin tocar la columna Resultado, corregir la fila y volver a subir **el archivo de resultado**
   corregido (con la columna L llena de texto viejo).
4. Repetir el paso 1 y el 3 con `ActivarDesactivar` (columna G).

**Qué tiene que pasar.** Paso 1: rechazo `MPPP-CAR-007`, nunca `MPPP-CAR-005`. Paso 3: el archivo
corregido se lee (el contenido de la columna Resultado se ignora) y la carga sigue su curso (se
graba, o da errores de fila reales, nunca un rechazo de encabezados). Paso 4: lo mismo con la
columna G.

**Cómo se verifica.** El código del rechazo en la respuesta (`MPPP-CAR-007`), y que `grabado` sea `true`
en el paso 3 si el archivo corregido ya no tiene errores.

**Cómo se sabe que falló.** `MPPP-CAR-005` en el paso 1 o 3: la columna Resultado dejó de ser parte de
la plantilla. Un error de fila que cita el contenido viejo de la columna Resultado: el lector la está
leyendo.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`tests/Sanic.Mppp.Plugins.Tests/Aceptacion/ExcelDeCatalogosAceptacion.cs`:
`La_plantilla_vacia_trae_solo_los_encabezados_y_el_lector_estricto_la_acepta_sin_filas`,
`El_resultado_va_en_la_misma_fila_con_los_valores_leidos_y_se_puede_volver_a_subir`,
`El_resultado_del_cambio_de_estado_tambien_se_puede_volver_a_subir`).

---

#### L-08 — Cancelar el selector no hace nada; un archivo que no es `.xlsx` avisa sin llamar a la API

**Qué prueba.** Contrato del JS (`cargarCatalogos` / `cambiarEstadoCatalogos`): el selector de archivo
se abre **sincrónicamente dentro del clic** (si no, el navegador lo bloquea); cancelar no hace nada; un
archivo que no termina en `.xlsx` da un aviso y no llama a la Custom API.

**Necesita.** `local/sesion-admin_planes.json`; un archivo `.txt` cualquiera.

**Cómo.**
1. En Clientes, "Carga masiva" → "Cargar archivo"; cuando se abre el selector, cancelarlo.
2. Repetir con "Activar/Desactivar masivo".
3. Repetir "Cargar archivo" y elegir el `.txt`.

**Qué tiene que pasar.** Paso 1 y 2: no aparece indicador de progreso, no hay llamada de red a ninguna
Custom API, la grilla queda como estaba. Paso 3: un aviso dice que el archivo tiene que ser `.xlsx`;
no hay llamada a la Custom API.

**Cómo se verifica.** Con Playwright (`page.on("request")`, mismo mecanismo de evidencia de red que
`a-04-cancelar-no-cambia-nada.spec.mjs`): cero peticiones a `sanic_mppp_capi_cargarcatalogos` y
`sanic_mppp_capi_cambiarestadocatalogos` en los tres pasos.

**Cómo se sabe que falló.** Una petición a la Custom API con un `.txt`: el JS no valida la extensión y
el usuario recibiría `MPPP-CAR-002`, un mensaje que no le dice "elegí un Excel". El selector no
abre: se perdió el gesto del usuario (la apertura dejó de ser sincrónica con el clic).

**Automatizable.** GUI — sesión `admin_planes`. Falta el caso del arnés.

---

#### L-09 — El indicador por etapas, el resumen de una carga grabada y el refresco de la grilla

**Qué prueba.** DN-04, "Flujo del JS": tres mensajes de progreso ("Leyendo el archivo…", "Validando y
cargando el archivo… puede tardar hasta un minuto", "Preparando el resultado…"), la descarga de
`<nombre original sin extensión>-resultado.xlsx`, el diálogo de resumen y el refresco de la grilla.
El indicador es modal: mientras está abierto no se puede volver a hacer clic.

> **Evidencia parcial (2026-10-01).** El mensaje del medio ya lo comprueba el caso de Playwright de la carga con error de
> fila (`pruebas/gui/casos/dn-04-catalogos-masivos.spec.mjs`, dos corridas en verde): el indicador muestra "puede tardar
> hasta un minuto" (antes decía "hasta 2 minutos"; se corrigió por D-75). El resto del caso sigue pendiente.

**Necesita.** `local/sesion-admin_planes.json`; un archivo de carga **sin errores** que cree un cliente
(con su ejecutivo `# User 1`), un plan, un correo y su autorización, con una advertencia buscada (un
cliente existente inactivo, o bien dos filas que repiten una acción en el caso del cambio de
estado); la grilla Clientes abierta **antes** de cargar.

**Cómo.**
1. En Clientes, "Carga masiva" → "Cargar archivo", elegir el archivo, y mirar el indicador durante la
   llamada. Intentar hacer clic en otro lado de la pantalla.
2. Al terminar, leer el diálogo y la descarga.
3. Cerrar el diálogo y mirar la grilla.
4. Repetir con un archivo de Activar/Desactivar sin errores.

**Qué tiene que pasar.** El JS **pide** los tres mensajes en ese orden (espía sobre
`Xrm.Utility.showProgressIndicator`) y el de la llamada a la API ("Validando…") **llega a verse**. *(Corregido
el 2026-10-02: decía "los tres aparecen". La plataforma pinta el indicador ~400 ms después de pedirlo, así que
"Leyendo" y "Preparando", que duran milisegundos, se cierran antes de verse: exigirlos en pantalla era pedir
algo no observable. El defecto era del caso, no del producto.)* El indicador bloquea la pantalla; se
descarga `<nombre>-resultado.xlsx`; el diálogo dice "Carga terminada: 1 clientes, 1 planes, 1 correos
y 1 autorizaciones creados." (en 3082; en 1033 "Load finished: ... created."), más
" N filas con advertencias: revisá el archivo de resultado." si `advertencias` > 0; el cliente nuevo
aparece en la grilla sin recargar a mano. Con Activar/Desactivar: "Cambio de estado terminado: N
activados, N desactivados y N sin cambios.", más el mismo sufijo de advertencias si las hay (desactivar un
cliente con autorizaciones activas siempre advierte, regla 6).

**Cómo se verifica.** La captura del diálogo y de la descarga con Playwright, y `GET
sanic_mppp_tbl_clientes?$filter=sanic_cifbac eq '<CIF del archivo>'` para el cliente creado.

**Cómo se sabe que falló.** La grilla no refresca: el JS no llama al refresco. El diálogo dice algo
distinto del texto del contrato: `TEXTOS` quedó desalineado (ese mensaje lo lee el Administrador de
planes). Si no se descarga el resultado pero el diálogo sí aparece: falló `Xrm.Navigation.openFile`.

**Automatizable.** GUI — sesión `admin_planes`. El spec de hoy cubre solo los dos desenlaces de error
(L-13 y L-43); falta el desenlace grabado.

---

### Grupo 2 — Los once códigos de archivo `MPPP-CAR-001` a `011`

Un error de **archivo** (no de fila) hace que la llamada falle con un código y un texto propio, y **no
devuelve Excel de resultado**: no hay filas confiables a las que asignarle un motivo. Los once códigos
son los de las **dos** Custom API; los casos de abajo los disparan sobre `cargarcatalogos` y repiten el
disparo sobre `cambiarestadocatalogos` cuando el código depende de la hoja.

#### L-10 — `MPPP-CAR-001`: archivo vacío o que no es base64

**Qué prueba.** DN-04, tabla de códigos: `archivo` vacío o que no es base64 válido es `MPPP-CAR-001`.

**Necesita.** Sesión (o `MSCRMCallerID`) del Administrador de planes.

**Cómo.**
1. `dataverse_api.py POST sanic_mppp_capi_cargarcatalogos '{"archivo":""}'`.
2. Lo mismo con `{"archivo":"esto no es base64 !!"}`.
3. Lo mismo contra `sanic_mppp_capi_cambiarestadocatalogos`.

**Qué tiene que pasar.** Las tres llamadas se rechazan con un mensaje que empieza con `MPPP-CAR-001` y
pide elegir un Excel (`.xlsx`); ninguna devuelve `resultado`.

**Cómo se verifica.** El mensaje de error de cada llamada.

**Cómo se sabe que falló.** Un error genérico de la plataforma en vez del código: el plugin no
atrapó el base64 inválido. `MPPP-CAR-010` en vez de `001`: se mandó también `plantilla = true`
(no es este caso).

**Automatizable.** API — cubierto por pruebas automáticas C#
(`tests/Sanic.Mppp.Plugins.Tests/Aceptacion/CatalogosMasivosApiAceptacion.cs`:
`Archivo_vacio_o_base64_invalido_es_MPPP_CAR_001`).

---

#### L-11 — `MPPP-CAR-002`: no es un `.xlsx` legible

**Qué prueba.** Un archivo que no se puede leer como Excel (dañado, o de otro formato) da
`MPPP-CAR-002`, y **el mensaje de la librería de lectura nunca llega al usuario** (LP-01): va a la
traza.

**Necesita.** Sesión del Administrador de planes; cuatro bytes cualesquiera en base64, y un `.xlsx`
cortado a la mitad.

**Cómo.**
1. Llamar a `cargarcatalogos` con los cuatro bytes en base64.
2. Llamar con el `.xlsx` truncado.

**Qué tiene que pasar.** Rechazo `MPPP-CAR-002` con un texto en español que manda a bajar la plantilla;
sin jerga de la librería ni nombres de clases.

**Cómo se verifica.** El mensaje de error; y que no contenga texto de la librería (`OpenXml`,
`System.`, un rastro de pila).

**Cómo se sabe que falló.** El usuario recibe un texto técnico: la excepción de la librería se mostró
en lugar de ir a la traza (violación de LP-01).

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CatalogosMasivosApiAceptacion.cs`: `Los_rechazos_del_lector_se_traducen_a_su_codigo_sin_escribir_nada`).

---

#### L-12 — `MPPP-CAR-003`: el archivo supera el peso de `lectura.limites`

**Qué prueba.** El tope de bytes (comprimidos y descomprimidos) de `lectura.limites` se aplica, y el
peso del texto base64 se mira **antes de decodificar**: un texto enorme no se convierte en bytes para
después rechazarlo.

**Necesita.** `lectura.limites` vigente (se lee, no se asume); un archivo que lo supere (para el tope de
entrada) y uno chico comprimido que se expanda por encima del tope descomprimido.

**Cómo.** Llamar a `cargarcatalogos` con cada uno de los dos archivos.

**Qué tiene que pasar.** Rechazo `MPPP-CAR-003` que pide quitar filas o partir el archivo.

**Cómo se verifica.** El mensaje de error y el tiempo de la llamada (el rechazo por texto enorme no
tiene que tardar: no decodificó).

**Cómo se sabe que falló.** La llamada tarda mucho antes de rechazar (decodificó el texto entero) o
revienta con un error de memoria de la plataforma.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CatalogosMasivosApiAceptacion.cs`: `Un_archivo_que_supera_lectura_limites_es_MPPP_CAR_003`,
`El_tope_de_peso_se_mira_antes_de_decodificar_el_base64`).

---

#### L-13 — `MPPP-CAR-004`: no existe la hoja esperada, y no se descarga nada

**Qué prueba.** Un archivo sin la hoja `CargaCatalogos` (o `ActivarDesactivar`) da `MPPP-CAR-004`, nombra
la hoja esperada y **no descarga ningún Excel de resultado**. Subir a un botón el archivo del otro (el
de `ActivarDesactivar` en Carga masiva) cae en este caso.

**Necesita.** `local/sesion-admin_planes.json`; `pruebas/gui/datos/dn04/dn04-carga-hoja-equivocada.xlsx`.

**Cómo.** `MPPP_USUARIO=admin_planes npx playwright test dn-04`: en Clientes, "Carga masiva" →
"Cargar archivo", elegir `dn04-carga-hoja-equivocada.xlsx`, escuchar el evento `download`.

**Qué tiene que pasar.** El diálogo muestra el texto con `MPPP-CAR-004`; no hubo ninguna descarga.

**Cómo se verifica.** La prueba `'Cargar archivo' con la hoja equivocada muestra MPPP-CAR-004 y no
descarga nada` del spec (`texto` contiene `MPPP-CAR-004`, `huboDescarga === false`).

**Cómo se sabe que falló.** Se descarga un resultado: el plugin trató un archivo sin hoja como un archivo
con filas. Aparece `MPPP-CAR-002` en lugar de `004`: el lector clasificó "no hay hoja" como "archivo
ilegible".

**Automatizable.** GUI — sesión `admin_planes`. Ya construido en
`pruebas/gui/casos/dn-04-catalogos-masivos.spec.mjs`; además lo cubre
`CatalogosMasivosApiAceptacion.cs` (`Los_rechazos_del_lector_se_traducen_a_su_codigo_sin_escribir_nada`).

---

#### L-14 — `MPPP-CAR-005`: encabezados que faltan, sobran o están fuera de orden (los nombra)

**Qué prueba.** Los encabezados se comparan en la fila 1, en el orden exacto de la tabla del diseño,
sin distinguir mayúsculas, tildes ni espacios a los lados. Si falta uno, sobra un encabezado no
vacío después de la última columna, o hay uno fuera de orden, se rechaza el archivo y el mensaje
nombra **cuáles**.

**Necesita.** Sesión del Administrador de planes; la plantilla vacía de `CargaCatalogos` en cuatro
variantes: (a) un encabezado con otro nombre ("Divisa" en vez de "Moneda"), (b) una columna `Extra` al
final, (c) dos columnas intercambiadas, (d) los mismos encabezados con otras mayúsculas y un espacio
al final (no debe ser rechazo).

**Cómo.** Llamar a `cargarcatalogos` con cada variante, con **una fila de datos** para que no caiga en
`MPPP-CAR-007`.

**Qué tiene que pasar.** (a), (b) y (c): `MPPP-CAR-005` nombrando los encabezados que no
coinciden. (d): no hay rechazo de encabezados.

**Cómo se verifica.** El mensaje de error de cada llamada.

**Cómo se sabe que falló.** (c) pasa sin rechazo: el lector está comparando por nombre y no por
posición, y una carga con las columnas cruzadas grabaría datos en el campo equivocado. (d) se
rechaza: la comparación distingue mayúsculas o espacios.

**Automatizable.** API — cubierto por pruebas automáticas C# en (a) y (b)
(`CatalogosMasivosApiAceptacion.cs`: `Los_rechazos_del_lector_se_traducen_a_su_codigo_sin_escribir_nada`).
**(c) y (d) no tienen una prueba de aceptación propia nombrada** en esa clase; se corren en Dev
la primera vez.

---

#### L-15 — `MPPP-CAR-006`: más de 100 filas, rechazado entero y sin escribir

**Qué prueba.** DN-04, "Las filas de más se rechazan, no se cortan": con más filas no vacías que
`carga.filas.maximo` (100, D-75) se rechaza **el archivo entero**; no se carga la ventana. El mensaje
dice cuál es el máximo, **no cuántas filas trae** (el lector corta en la primera de más para no
recorrer un archivo hostil).

**Necesita.** Sesión del Administrador de planes; `carga.filas.maximo` en 100 verificado; un archivo de
**101 filas** no vacías (cada una un cliente distinto, rangos de CIF reservados del set); el
conteo previo de clientes de ese rango (cero).

**Cómo.** Llamar a `cargarcatalogos` con el archivo de 101 filas. Después contar los clientes del rango.

**Qué tiene que pasar.** Rechazo `MPPP-CAR-006` que dice que el máximo es 100 y pide partir el archivo;
cero clientes nuevos en el rango; ningún `resultado`.

**Cómo se verifica.** El mensaje de error; `dataverse_api.py GET
"sanic_mppp_tbl_clientes?$filter=startswith(sanic_cifbac,'<prefijo del set>')&$count=true"`.

**Cómo se sabe que falló.** Se cargan las primeras 100 (el archivo se cortó en vez de rechazarse: es
exactamente lo que DN-04 prohíbe). El mensaje dice "101": el lector recorrió todo el archivo. Con 100
filas exactas, la llamada tiene que **aceptarlo** (ver L-48).

**Automatizable.** API — sesión real del Administrador de planes. También cubierto por pruebas
automáticas C# (`CatalogosMasivosApiAceptacion.cs`:
`Los_rechazos_del_lector_se_traducen_a_su_codigo_sin_escribir_nada`).

---

#### L-16 — `MPPP-CAR-007`: ninguna fila de datos

**Qué prueba.** Un archivo con hoja y encabezados correctos pero sin ninguna fila de datos (la plantilla
vacía) da `MPPP-CAR-007`, nunca un rechazo de encabezados.

**Necesita.** Sesión del Administrador de planes; la plantilla vacía; un archivo con solo filas
totalmente vacías (se saltean).

**Cómo.** Llamar a las dos Custom API con la plantilla vacía correspondiente y con el archivo de filas
vacías.

**Qué tiene que pasar.** Rechazo `MPPP-CAR-007` que pide completar al menos una fila.

**Cómo se verifica.** El mensaje de error.

**Cómo se sabe que falló.** `MPPP-CAR-005`: la plantilla generada por la propia Custom API no pasa su
propio validador de encabezados (se rompió "Abierto B").

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CatalogosMasivosApiAceptacion.cs`: `Los_rechazos_del_lector_se_traducen_a_su_codigo_sin_escribir_nada`).

---

#### L-17 — `MPPP-CAR-008`: se agotó el presupuesto de 30 s; no se grabó nada

**Qué prueba.** El presupuesto de escritura es de **30 s** (bajado de 100 s el 2026-10-01, D-75: en Dev la
base corta una transacción a los ~40 s). Al alcanzarlo, la Custom API corta con una excepción
(que deshace lo grabado) y un mensaje que pide partir el archivo.

**Necesita.** Un volumen que tarde más de 30 s en escribir. **Con el tope de 100 filas, el peor caso
medido tarda 25,1 s (D-75): hoy no hay un archivo permitido que dispare este caso en Dev sin ayuda.**
Para dispararlo de verdad hay que sembrar una versión de prueba de `carga.filas.maximo` más alta
(máximo sembrado + 1, desactivada y borrada después, `CONVENCIONES.md` §8.2) y subir 130 a 150 filas
peor caso, y a esos volúmenes la base ya cortó a los ~40 s (D-75: 150 filas = 39,0 s): el
mismo riesgo y la misma regla que `E-08` — **no se corre de rutina**.

**Cómo (solo si se decide la confirmación de punta a punta).** Con el parámetro de prueba sembrado,
subir 140 filas peor caso (cada una crea cliente, plan, correo y autorización) y mirar la respuesta.

**Qué tiene que pasar.** Rechazo `MPPP-CAR-008` que dice "30 segundos", "No se grabó nada" y pide partir
el archivo; cero registros nuevos.

**Cómo se verifica.** El mensaje de error y el conteo de los clientes del rango (cero).

**Cómo se sabe que falló.** Aparece `MPPP-CAR-011` o un error de plataforma (código 10928) en lugar
de `008`: la base cortó antes que el presupuesto propio, que es justo lo que D-75 quiso evitar bajando
a 30 s.

**Automatizable.** API en `set_l.py`, **siempre** (corregido el 2026-10-02: no hace falta bandera de alto riesgo).
El set siembra una versión temporal de `carga.filas.maximo` en 200, sube 200 filas que crean todo y espera el 008
cerca de los 30 s; la transacción dura lo mismo que la de L-48, lejos del corte de la base (~40 s). La versión se
desactiva en el mismo caso y se borra en `cerrar()`. Además, la cobertura automática C#
(`CatalogosMasivosApiAceptacion.cs`: `Presupuesto_agotado_es_MPPP_CAR_008`;
`CatalogosMasivosAceptacion.cs`: `Con_el_presupuesto_agotado_no_escribe_mas_y_dice_cuantas_alcanzo`,
`CatalogosMasivosApiAceptacion.cs`: `Si_no_hay_nada_que_escribir_el_presupuesto_no_tira_el_resultado`).

---

#### L-18 — `MPPP-CAR-009`: falla un error de dato al grabar (pasada 2), nombra la fila y no graba nada

**Qué prueba.** Si una escritura de la pasada 2 falla por un **dato** (por ejemplo, alguien creó el
mismo CIF entre las dos pasadas), se atrapa solo para envolverla en `MPPP-CAR-009` con la fila y el
motivo de Dataverse (recortado a ~300 caracteres), y se relanza **sin ninguna otra llamada al
servicio**: la transacción deshace todo.

**Necesita.** Un archivo válido de 2 filas (cliente nuevo cada una) y la forma de colisionar una
clave **entre las dos pasadas**: crear con otro usuario el CIF de la fila 3 mientras corre la pasada 1.
Eso es una carrera y no se puede forzar de manera determinista contra Dev desde afuera.

**Cómo.** No hay forma segura de forzar el cambio entre las dos pasadas en Dev. En Dev, la variante
determinista es una violación de clave alternativa que la pasada 1 no ve (por ejemplo, un correo ya
existente con otras mayúsculas que Dataverse normaliza distinto): solo si aparece un caso real se
corre y se anota. El resto de la cobertura es la suite de C# con un servicio que falla a propósito.

**Qué tiene que pasar.** Rechazo `MPPP-CAR-009`: "No se pudo grabar la fila N: <motivo>. No se grabó
nada. Corregí esa fila y volvé a subir el archivo."; cero registros nuevos, incluidos los de las
filas anteriores.

**Cómo se verifica.** El mensaje de error, el `ErrorCode` en la traza (siempre va, en hexadecimal) y
el conteo de los registros del archivo.

**Cómo se sabe que falló.** Se grabaron las filas anteriores a la que falló: se perdió el todo o nada.
El usuario recibe `MPPP-CAR-009` por una falla de **plataforma**: el código está fuera de la lista de
`FallasDePlataforma` y debería ser `011` (ver L-20).

**Automatizable.** API en Dev solo cuando aparezca una colisión real; la cobertura repetible es la
automática C# (`CatalogosMasivosApiAceptacion.cs`:
`Una_escritura_que_falla_es_MPPP_CAR_009_con_su_fila_y_sube_para_que_la_transaccion_deshaga_todo`,
`Un_error_de_dato_sigue_siendo_MPPP_CAR_009_y_pide_corregir_su_fila`,
`Un_fault_sin_codigo_conocido_ni_detalle_sigue_siendo_MPPP_CAR_009`,
`El_motivo_de_Dataverse_en_el_009_va_recortado`).

---

#### L-19 — `MPPP-CAR-010`: `plantilla = true` y además vino `archivo`

**Qué prueba.** No se adivina cuál de las dos cosas se quiso: con `plantilla = true` y un `archivo` a la
vez, la llamada se rechaza con `MPPP-CAR-010`, **no** con `MPPP-CAR-001`. Con `plantilla = true` sola
se devuelve la plantilla vacía sin escribir ni leer nada.

**Necesita.** Sesión del Administrador de planes; un `.xlsx` válido en base64.

**Cómo.**
1. `POST sanic_mppp_capi_cargarcatalogos '{"plantilla":true,"archivo":"<base64>"}'`.
2. `POST sanic_mppp_capi_cargarcatalogos '{"plantilla":true}'`.
3. Ambos contra `sanic_mppp_capi_cambiarestadocatalogos`.

**Qué tiene que pasar.** Paso 1 y 3 (con archivo): `MPPP-CAR-010`. Paso 2: `grabado = false`, un
`resultado` que es la plantilla vacía, ningún cambio en Dataverse.

**Cómo se verifica.** El mensaje de error y el `resultado` decodificado.

**Cómo se sabe que falló.** `MPPP-CAR-001` en el paso 1: el plugin ignoró la bandera y trató el archivo
como carga. Con la plantilla, que cree cosas: `plantilla = true` no puede escribir.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CatalogosMasivosApiAceptacion.cs`: `Con_plantilla_y_sin_archivo_devuelve_la_plantilla_vacia_sin_escribir`,
`Plantilla_y_archivo_juntos_es_MPPP_CAR_010_y_ninguno_es_MPPP_CAR_001`).

---

#### L-20 — `MPPP-CAR-011`: falla de plataforma al leer o al escribir, sin nombrar ninguna fila

**Qué prueba.** DN-04 / D-75: una falla de **plataforma** (sobrecarga, límites; clasificada por el
`ErrorCode` del `OrganizationServiceFault`, nunca por el texto) sale como `MPPP-CAR-011`, pide
reintentar en unos minutos o partir el archivo, **no culpa a ninguna fila** y no graba nada. Antes,
una carga de 300 filas fallaba con el error 10928 de Azure SQL y el 009 le decía al usuario "Corregí
esa fila" cuando la fila no tenía nada malo. El código de Dataverse va **siempre** a la traza, en
hexadecimal.

**Necesita.** Una falla de plataforma real: con el tope en 100 filas **hoy no se reproduce por
volumen** (200 filas ya dan `MPPP-CAR-006`). Reproducirla exige una versión de prueba de
`carga.filas.maximo` más alta y un archivo de 200 filas peor caso: mismo riesgo y misma regla que
`E-08`, **no se corre de rutina**. Sesión real del Administrador de planes.

**Cómo (solo si se decide repetir la medición).** Sembrar la versión de prueba de
`carga.filas.maximo` con 300 (máximo sembrado + 1); subir un archivo de 200 filas peor caso; leer la
respuesta y la traza del plugin; desactivar y borrar la versión de prueba.

**Qué tiene que pasar.** Rechazo `MPPP-CAR-011` ("Dataverse estaba sobrecargado y no pudo procesar el
archivo. No se grabó nada. Esperá unos minutos y volvé a subir el mismo archivo; si vuelve a pasar,
partilo en archivos más chicos."), sin número de fila; en la traza, el código `0x80072324`.

**Cómo se verifica.** El mensaje de error (que **no** contenga "fila" seguido de un número) y la traza
del plugin, con el código en hexadecimal.

**Cómo se sabe que falló.** El usuario recibe `MPPP-CAR-009` con "Corregí esa fila": el código no está en
la lista de plataforma. La traza no trae el código: sin él no se puede confirmar que la lista lo
cubre (el 10928 de la primera medición no lo dejó). Una falla de **lectura** de plataforma que sale
como error genérico en vez de `011`.

**Automatizable.** API — cubierto además por pruebas automáticas C#
(`CatalogosMasivosApiAceptacion.cs`: `Una_falla_de_plataforma_es_MPPP_CAR_011_y_no_culpa_a_la_fila`,
`El_cambio_de_estado_tambien_distingue_la_falla_de_plataforma`,
`El_codigo_de_Dataverse_queda_en_la_traza_en_hexadecimal_y_el_usuario_recibe_el_011`,
`Una_falla_de_plataforma_al_leer_los_catalogos_es_MPPP_CAR_011`,
`Una_falla_de_plataforma_al_leer_con_SYSTEM_tambien_es_MPPP_CAR_011`,
`El_cambio_de_estado_tambien_da_MPPP_CAR_011_si_la_plataforma_falla_al_leer`,
`El_texto_del_011_es_el_mismo_al_leer_y_al_escribir`).

---

#### L-21 — Ningún error de archivo muestra el texto de la librería, y todos empiezan con su código

**Qué prueba.** DN-04, "Errores de ARCHIVO": la llamada falla con un código y un texto propio; **nunca llega
al usuario el mensaje de la librería** (LP-01); ese va a la traza. Un error inesperado sale con el
mensaje genérico y su traza, no con la excepción cruda.

**Necesita.** Los archivos de L-10 a L-16.

**Cómo.** Para cada una de las llamadas rechazadas en L-10 a L-16, leer el mensaje.

**Qué tiene que pasar.** Cada mensaje empieza con `MPPP-CAR-0xx: ` y está redactado en español para
quien carga el archivo; ninguno trae nombres de clases, rastros de pila ni texto de la librería de
lectura.

**Cómo se verifica.** Una expresión regular sobre cada mensaje: `^MPPP-CAR-0(0[1-9]|1[01]): ` y que
no contenga `Exception`, `System.` ni `OpenXml`.

**Cómo se sabe que falló.** Un mensaje que muestra una excepción interna: el envoltorio dejó pasar el
texto técnico. Un código fuera del rango 001-011: se agregó un código sin documentarlo en el diseño.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CatalogosMasivosApiAceptacion.cs`: `Los_mensajes_de_rechazo_empiezan_con_su_codigo_y_no_traen_jerga_de_la_libreria`,
`Un_rechazo_sale_con_su_mensaje_y_un_error_inesperado_con_el_generico_y_la_traza`).

---

### Grupo 3 — Excel 1, Carga masiva: reglas por fila

Cada caso arma un archivo de pocas filas, lo sube a `cargarcatalogos` como el Administrador de planes y
lee la columna **Resultado** (L) de la fila nombrada. Los motivos tienen el formato
`<Columna>: <qué pasa>. <Qué hacer>.`. Un error de una fila **no graba nada** del archivo
(L-43): para ver el resultado "Creado: ..." de una fila hay que subirla en un archivo sin errores.

#### L-22 — Reglas 1 y 2: la fila necesita su cliente, y los dos CIF tienen que ser coherentes con lo grabado

**Qué prueba.** Regla 1: sin CIF BAC ni CIF COM, error. Regla 2: se busca por A y por B, normalizados
(`123` encuentra a `000000123`); si vienen los dos y encuentran **clientes distintos**, o uno
encuentra un cliente y el otro no existe, error.

**Necesita.** Dos clientes existentes del set (`ZZP-Cliente A`, CIF BAC `000900001`, CIF COM
`ZZP900001001`; `ZZP-Cliente B`, CIF BAC `000900002`, CIF COM `ZZP900002001`), creados por el propio
set.

**Cómo.** Subir un archivo con cuatro filas: (1) sin A ni B; (2) A de `Cliente A` y B de `Cliente B`;
(3) A de `Cliente A` y B inexistente; (4) A = `900001` (sin ceros) y B de `Cliente A` (debe ser válido).

**Qué tiene que pasar.** Fila 1: error "toda fila necesita su cliente". Fila 2: error de `CIF BAC / CIF
COM`: "apuntan a dos clientes distintos (ZZP-Cliente A y ZZP-Cliente B)". Fila 3: error: no
coinciden con lo grabado. Fila 4: sin error (el CIF se normalizó antes de buscar). El archivo no
se graba por las filas 1 a 3.

**Cómo se verifica.** La columna L de las filas 2 a 5 del `resultado`.

**Cómo se sabe que falló.** La fila 4 da error: la búsqueda no normaliza (es el motivo de "Normalizar
antes de buscar" del diseño). La fila 2 no nombra a los dos clientes.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`tests/Sanic.Mppp.Plugins.Tests/Aceptacion/CargaDeCatalogosAceptacion.cs`:
`Sin_CIF_BAC_ni_CIF_COM_la_fila_es_error`,
`El_cliente_se_encuentra_por_CIF_BAC_normalizado_y_se_autoriza_el_correo_existente`,
`El_cliente_se_encuentra_por_CIF_COM_en_minuscula`,
`CIF_BAC_y_CIF_COM_de_dos_clientes_distintos_es_error_que_nombra_a_los_dos`,
`Un_CIF_encuentra_un_cliente_y_el_otro_no_existe_es_error`,
`CIF_BAC_con_formato_invalido_es_error_de_esa_columna`,
`CIF_COM_con_formato_invalido_es_error_de_esa_columna`).

---

#### L-23 — Regla 3: un cliente existente no se modifica; si el archivo lo contradice, error; si está inactivo, advertencia

**Qué prueba.** Para un cliente que **ya existe**: si Razón social (C) o Correo ejecutivo (D) vienen y no
coinciden con lo grabado (razón social sin distinguir mayúsculas ni espacios a los lados; ejecutivo =
el dueño actual), error: la carga **no modifica**. Si existe **inactivo**, advertencia: lo que se le
cargue no va a estar vigente hasta activarlo.

**Necesita.** Un cliente existente activo `ZZP-Cliente A` con dueño `# User 1`, y un cliente
existente **inactivo** (`statecode = 1`) `ZZP-Cliente I`.

**Cómo.** Subir un archivo con: (1) A de `Cliente A` con razón social `ZZP-OTRA`; (2) A de `Cliente A`
con un Correo ejecutivo de `# User 2`; (3) A de `Cliente A` con la razón social en minúscula y con
espacios al final (debe pasar); (4) A de `Cliente I`, sin más datos. Después, sin errores, repetir
solo (3) y (4) y comparar el cliente en Dataverse antes y después.

**Qué tiene que pasar.** (1) y (2): error que dice que no coincide con lo grabado; (3): sin error; (4):
"Advertencia: ... no va a estar vigente hasta activarlo". El cliente `Cliente A` sigue con la misma
razón social y el mismo dueño (`modifiedon` sin cambios).

**Cómo se verifica.** La columna L del `resultado` y `dataverse_api.py GET
"sanic_mppp_tbl_clientes(<id>)?$select=sanic_nombre,_ownerid_value,modifiedon"` antes y después.

**Cómo se sabe que falló.** La razón social o el dueño cambiaron: la carga está pisando datos (la
razón del diseño es que "una carga masiva que de paso pisa razones sociales o dueños es un cambio
que nadie revisó").

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CargaDeCatalogosAceptacion.cs`: `Cliente_existente_con_otra_razon_social_es_error_y_no_se_pisa`,
`Cliente_existente_con_otro_ejecutivo_es_error_y_con_el_mismo_no`,
`Cliente_existente_inactivo_es_advertencia_no_error`,
`Razon_social_de_mas_de_200_es_error_y_de_200_no`).

---

#### L-24 — Regla 4: un cliente nuevo exige A, B, C y D, y el ejecutivo tiene que ser exactamente un usuario habilitado con el rol Ejecutivo

**Qué prueba.** Para un cliente que **no existe**: hacen falta CIF BAC, CIF COM, Razón social y Correo
ejecutivo; si falta alguno, error nombrando cuál. D tiene que ser **exactamente un** usuario
**habilitado**, con ese correo y con el rol `SR - MPPP - Ejecutivo`. Cero usuarios, varios usuarios,
deshabilitado o sin el rol: error, nombrando cuál de esos casos es.

**Necesita.** El correo de `# User 1` (Ejecutivo, habilitado); el de `# User 2` (Supervisor, sin el rol
Ejecutivo); un correo inventado (cero usuarios). **Los motivos "deshabilitado" y "varios usuarios con
ese correo" necesitan un usuario deshabilitado y dos usuarios con el mismo correo, que el entorno de
prueba hoy no garantiza**: si `set_l.py` no los puede preparar, esos dos motivos se anotan como
cubiertos solo por la suite automática, no como OK.

**Cómo.** Subir un archivo con cinco filas de cliente nuevo: (1) sin CIF COM; (2) sin Razón social; (3)
Correo ejecutivo = el de `# User 2`; (4) Correo ejecutivo inventado; (5) todo correcto con el
correo de `# User 1`.

**Qué tiene que pasar.** (1) y (2): error que nombra la columna que falta. (3): error "no tiene el rol
Ejecutivo. Pedí que se lo asignen o usá otro correo." (4): error que dice que no hay ningún usuario
con ese correo. (5): sin error. Para "deshabilitado" y "varios", cada uno con su propio motivo, y los
dos distintos entre sí.

**Cómo se verifica.** La columna L del `resultado`; y la consulta que respalda cada motivo:
`dataverse_api.py GET "systemusers?$filter=internalemailaddress eq '<correo>'&$select=isdisabled"` y
los roles de `# User 2` en `systemuserroles_association`.

**Cómo se sabe que falló.** Un cliente a nombre de alguien que no es ejecutivo se acepta: ese cliente no
aparece en "Mis clientes" de nadie y nadie lo nota (el motivo de la regla). Dos motivos con el mismo
texto: el usuario no puede distinguir qué corregir.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CargaDeCatalogosAceptacion.cs`: `Cliente_nuevo_incompleto_nombra_cada_columna_que_falta`,
`El_ejecutivo_tiene_que_ser_exactamente_un_usuario_habilitado_con_el_rol_y_cada_caso_tiene_su_motivo`,
`El_correo_del_ejecutivo_se_compara_sin_mayusculas_ni_espacios_a_los_lados`,
`Cliente_nuevo_completo_se_crea_normalizado_con_el_ejecutivo_como_dueno`;
`CatalogosMasivosAceptacion.cs`: `Los_usuarios_se_leen_con_SYSTEM_y_dicen_si_estan_habilitados_y_si_son_ejecutivos`,
`Si_el_rol_de_ejecutivo_no_existe_en_el_entorno_es_un_error_de_configuracion_que_lo_nombra`).

---

#### L-25 — Reglas 5 y 7: los datos de un plan sin código son error, y un plan nuevo exige tipo de formato y moneda

**Qué prueba.** Regla 5: Tipo formato (F) o Moneda (G) sin Código de plan (E) es error: son datos de un
plan que no se nombró. Regla 7: un plan que **no existe** necesita F y G; si falta alguno, error. El
Plan **no tiene nombre que se cargue**: `sanic_nombre` es calculado (`<código> - <cliente>`).
Tipo formato acepta `06`, `10` u `11` (y `6` como `06`); Moneda `COR` o `USD`.

**Necesita.** Un cliente existente `ZZP-Cliente A`.

**Cómo.** Subir un archivo con: (1) A de `Cliente A`, F = `10`, sin E; (2) A de `Cliente A`, E = `ZP91`,
sin F ni G; (3) E = `ZP92`, F = `7`, G = `COR`; (4) E = `ZP93`, F = `6`, G = `usd`; (5) E = `ZP94`,
F = `11`, G = `EUR`.

**Qué tiene que pasar.** (1): error, datos de un plan sin código. (2): error que nombra Tipo formato y
Moneda. (3): error de Tipo formato. (4): sin error (el `6` se acepta como `06`; la moneda se normaliza).
(5): error de Moneda.

**Cómo se verifica.** La columna L del `resultado`. Para (4), en un archivo sin errores:
`GET sanic_mppp_tbl_plans?$filter=sanic_codigo eq 'ZP93'&$select=sanic_nombre,sanic_tipoformato,sanic_moneda`
con el nombre armado como `ZP93 - <razón social del cliente>`.

**Cómo se sabe que falló.** El plan se crea con un nombre distinto del calculado (`NombreCalculadoStep`
no corrió o el archivo pisó el nombre). `6` rechazado: no se aplicó la normalización de `06`.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CargaDeCatalogosAceptacion.cs`: `Tipo_formato_o_moneda_sin_codigo_de_plan_es_error`,
`Tipo_formato_acepta_06_10_11_y_6`, `Tipo_formato_invalido_es_error`, `Moneda_invalida_es_error`,
`Plan_nuevo_incompleto_nombra_lo_que_falta`, `Codigo_de_plan_invalido_es_error`,
`Plan_nuevo_de_cliente_existente_se_crea_normalizado`,
`Formato_11_no_tiene_regla_de_moneda_ni_de_clasificacion_al_crear_el_plan`).

---

#### L-26 — Regla 6: un plan existente tiene que ser del cliente de la fila; si el archivo lo contradice, error; si está inactivo, advertencia

**Qué prueba.** Para un plan que **ya existe**: su cliente tiene que ser el de la fila; si no, error
nombrando el cliente real (el código de plan es único en todo el banco). Si F o G vienen y no
coinciden con lo grabado, error. Inactivo, advertencia.

**Necesita.** Un plan existente `ZP81` de `ZZP-Cliente A` (formato `10`, moneda `COR`); un plan
existente inactivo `ZP82` de `Cliente A`; un cliente `ZZP-Cliente B`.

**Cómo.** Subir un archivo con: (1) `Cliente B` con E = `ZP81`; (2) `Cliente A` con E = `ZP81` y G =
`USD`; (3) `Cliente A` con E = `ZP81`, F = `10`, G = `COR` (debe pasar); (4) `Cliente A` con E =
`ZP82`; (5) un cliente **nuevo** con E = `ZP81`.

**Qué tiene que pasar.** (1): error "el plan ZP81 ya existe y es de ZZP-Cliente A, no de este cliente."
(2): error: moneda no coincide con lo grabado. (3): sin error. (4): "Advertencia: ... inactivo ...". (5):
error (un plan existente con un cliente nuevo en la fila).

**Cómo se verifica.** La columna L del `resultado`.

**Cómo se sabe que falló.** (1) pasa o crea un segundo plan con el mismo código: se rompe la unicidad
del código de plan en todo el banco. El texto de (1) no nombra al cliente real.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CargaDeCatalogosAceptacion.cs`: `Plan_existente_del_mismo_cliente_no_se_crea_y_sus_datos_tienen_que_coincidir`,
`Plan_existente_de_otro_cliente_es_error_que_nombra_al_cliente_real`,
`Plan_existente_con_un_cliente_nuevo_en_la_fila_es_error`, `Plan_existente_inactivo_es_advertencia`,
`Plan_nuevo_de_un_cliente_nuevo_apunta_al_cliente_por_su_CIF_BAC`).

---

#### L-27 — Reglas 8 y 9: los datos de una autorización sin correo son error, y el correo tiene que tener formato válido

**Qué prueba.** Regla 8: Nombre contacto (I), Fecha documento (J) o Comentarios (K) sin Correo (H) es
error: son datos de una autorización sin correo. Regla 9: H con formato de correo inválido es error. El
correo se guarda en minúscula y sin espacios.

**Necesita.** Un cliente existente `ZZP-Cliente A`.

**Cómo.** Subir un archivo con: (1) `Cliente A`, I = `ZZP-Persona`, sin H; (2) `Cliente A`, K =
`un comentario`, sin H; (3) `Cliente A`, H = `no-es-un-correo`, I = `ZZP-Persona`; (4) `Cliente A`,
H = ` ZZP.Persona@Empresa.com ` (mayúsculas y espacios), I = `ZZP-Persona`.

**Qué tiene que pasar.** (1) y (2): error que dice que los datos son de una autorización sin correo.
(3): error de formato de correo. (4): sin error; el correo guardado es `zzp.persona@empresa.com`.

**Cómo se verifica.** La columna L del `resultado`; y en el archivo sin errores,
`GET sanic_mppp_tbl_autorizados?$filter=sanic_nombre eq 'zzp.persona@empresa.com'`.

**Cómo se sabe que falló.** El correo queda guardado con mayúsculas o espacios: la normalización no
corrió y la búsqueda de correos existentes se rompería.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CargaDeCatalogosAceptacion.cs`: `Nombre_fecha_o_comentarios_sin_correo_es_error`,
`Correo_con_formato_invalido_es_error`, `Correo_nuevo_crea_el_correo_y_la_autorizacion_normalizados`,
`Largos_maximos_de_correo_nombre_y_comentarios`).

---

#### L-28 — Reglas 10 y 11: un correo existente se reutiliza (sin pisar su nombre); uno nuevo exige Nombre contacto

**Qué prueba.** Regla 10: el correo **existe**: si Nombre contacto (I) viene y no coincide, error;
inactivo, advertencia. Regla 11: el correo **no existe**: hace falta I. Un correo existente se puede
autorizar sobre otro cliente reutilizando su `id`.

**Necesita.** Un correo existente `zzp.uno@empresa.com` (contacto `ZZP-Uno`, activo) y uno inactivo
`zzp.dos@empresa.com`; dos clientes `ZZP-Cliente A` y `ZZP-Cliente B`; el correo `zzp.uno@...` ya
autorizado sobre `Cliente A`.

**Cómo.** Subir un archivo con: (1) `Cliente A`, H = `zzp.uno@empresa.com`, I = `OTRO NOMBRE`; (2)
`Cliente B`, H = `zzp.uno@empresa.com`, sin I (debe pasar: se autoriza el correo existente sobre otro
cliente); (3) `Cliente A`, H = `zzp.nuevo@empresa.com`, sin I; (4) `Cliente A`, H =
`zzp.dos@empresa.com`.

**Qué tiene que pasar.** (1): error: el nombre no coincide con lo grabado. (2): sin error. (3): error:
hace falta Nombre contacto. (4): advertencia de correo inactivo.

**Cómo se verifica.** La columna L del `resultado`; y, tras grabar (2), `GET
sanic_mppp_tbl_autorizados?$filter=sanic_nombre eq 'zzp.uno@empresa.com'&$count=true` devuelve **uno**
solo (no se duplicó el correo).

**Cómo se sabe que falló.** Se crea un segundo `Autorizado` con el mismo correo: choca con su clave
alterna, y en Dev el síntoma es un `HTTP 412` o un `MPPP-CAR-009` en la pasada 2 en vez de un error
de fila en la pasada 1.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CargaDeCatalogosAceptacion.cs`: `Correo_nuevo_sin_nombre_de_contacto_es_error`,
`Correo_existente_con_otro_nombre_es_error_y_con_el_mismo_no`,
`Correo_existente_se_autoriza_sobre_otro_cliente_usando_su_id`, `Correo_existente_inactivo_es_advertencia`).

---

#### L-29 — Reglas 12, 13 y 14: una autorización existente activa queda "ya existía", una inactiva es error y una nueva nace sin evidencia firmada

**Qué prueba.** Regla 12: la autorización (correo, cliente) existe **activa**: queda "ya existía"; si J o
K vienen y no coinciden con lo grabado, error. Regla 13: existe **inactiva**: error, se reactiva con el
botón de Activar/Desactivar, no desde la carga. Regla 14: **no existe**: se crea con J y K, y nace
**sin evidencia firmada** (D-42: un archivo no puede viajar en una celda); aparece en la vista
"Autorizaciones sin evidencia".

**Necesita.** `ZZP-Cliente A`; el correo `zzp.uno@empresa.com`; una autorización activa (`Cliente A`,
`zzp.uno`) con comentario `viejo`; una autorización **inactiva** (`Cliente A`, `zzp.dos`).

**Cómo.** Subir un archivo con: (1) `Cliente A` + `zzp.uno` + K = `viejo` (coincide); (2) `Cliente A` +
`zzp.uno` + K = `nuevo` (no coincide); (3) `Cliente A` + `zzp.dos`; (4) `Cliente A` + `zzp.tres` (correo
nuevo con I) + J = `01/09/2026` + K = `alta masiva`. Para (4), en un archivo sin errores.

**Qué tiene que pasar.** (1): sin error y "Sin cambios: todo ya existía...". (2): error: el comentario no
coincide con lo grabado. (3): error: se reactiva con el botón de Activar/Desactivar. (4): "Creado:
correo, autorización" (con el cliente existente); la autorización tiene `sanic_documentofirmado`
vacío, `sanic_fechadocumento = 2026-09-01` y `sanic_comentarios = 'alta masiva'`, y aparece en
la vista "Autorizaciones sin evidencia".

**Cómo se verifica.** La columna L del `resultado`; `GET
sanic_mppp_tbl_autorizacionclientes?$filter=_sanic_clienteid_value eq <id>&$select=sanic_comentarios,sanic_fechadocumento,sanic_documentofirmado`;
y la vista "Autorizaciones sin evidencia" en la app.

**Cómo se sabe que falló.** (3) pasa y reactiva sola: se mezcló la carga con el cambio de estado. (4)
nace con evidencia: algo cargó un archivo que el diseño dice que no puede viajar.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CargaDeCatalogosAceptacion.cs`: `Autorizacion_existente_activa_queda_ya_existia_y_sus_datos_tienen_que_coincidir`,
`Autorizacion_existente_inactiva_es_error_se_reactiva_con_el_otro_boton`,
`Correo_nuevo_crea_el_correo_y_la_autorizacion_normalizados`,
`Fecha_documento_acepta_celda_de_fecha_y_texto_dd_mm_aaaa_sin_hora`).

---

#### L-30 — Regla 15: la misma clave tiene que tener los mismos datos en todas sus filas

**Qué prueba.** Para lo que el archivo **crea**: un mismo cliente, plan, correo o autorización repetido en
varias filas (una por plan, por la desnormalización) tiene que traer los mismos datos; las celdas
vacías no contradicen. Una contradicción es error **en todas las filas involucradas**, nombrándolas
("filas 3 y 9"). Para lo que **ya existe**, cada fila se compara contra lo grabado, así que solo
lleva error la que lo contradice. Un cliente nuevo puede repartir sus datos entre filas si no se
contradicen.

**Necesita.** Ningún cliente existente con los CIF del archivo.

**Cómo.** Subir un archivo con: filas 2 y 3: mismo CIF BAC `000900101` con Razón social `ZZP-Uno` y
`ZZP-Dos`; filas 4 y 5: mismo CIF BAC `000900102`, la fila 4 con razón social y ejecutivo, la fila 5
con CIF BAC, CIF COM y otro plan (sin razón social: debe pasar); fila 6: mismo plan nuevo `ZP71` bajo
`000900101` y `000900102` (dos clientes).

**Qué tiene que pasar.** Filas 2 y 3: error "...aparece con otra razón social en la fila N" en **las dos**.
Filas 4 y 5: sin error. Fila 6 (junto a su par): error en las dos filas del mismo plan bajo dos clientes.

**Cómo se verifica.** La columna L del `resultado`, filas 2 a 7.

**Cómo se sabe que falló.** Solo una de las dos filas lleva error: el usuario corrige la que no es. O
dos filas con datos contradictorios se aceptan y la que "gana" es la de menor número, en silencio.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CargaDeCatalogosAceptacion.cs`: `La_misma_clave_con_datos_distintos_es_error_en_todas_las_filas_y_nombra_a_las_otras`,
`Los_datos_de_un_cliente_nuevo_pueden_repartirse_entre_filas_si_no_se_contradicen`,
`Si_a_un_cliente_nuevo_le_falta_un_dato_en_todo_el_archivo_el_error_va_en_todas_sus_filas`,
`El_mismo_plan_nuevo_bajo_dos_clientes_es_error_en_las_dos_filas`,
`Sobre_algo_que_ya_existe_lleva_error_solo_la_fila_que_contradice_lo_grabado`,
`La_misma_autorizacion_en_varias_filas_se_crea_una_vez_y_las_celdas_vacias_no_contradicen`).

---

#### L-31 — Regla 16: un mismo CIF BAC con dos CIF COM distintos en el archivo (o al revés) es error

**Qué prueba.** Un CIF BAC con dos CIF COM distintos en el archivo, o al revés, es error en esas filas, aun
cuando ninguno de los clientes exista. Un CIF BAC solo y un CIF COM solo son el mismo cliente nuevo si
otra fila trae los dos.

**Necesita.** Ningún cliente existente con esos CIF.

**Cómo.** Subir un archivo con: (1) filas 2 y 3, CIF BAC `000900201` con CIF COM `ZZP900201001` y
`ZZP900201002`; (2) filas 4 y 5, CIF COM `ZZP900202001` con CIF BAC `000900202` y `000900203`; (3)
fila 6 con solo CIF BAC `000900204` y fila 7 con solo CIF COM `ZZP900204001` y fila 8 con los dos
(debe pasar: es un solo cliente).

**Qué tiene que pasar.** (1) y (2): error en las dos filas de cada par. (3): sin error; se crea un cliente.

**Cómo se verifica.** La columna L del `resultado`.

**Cómo se sabe que falló.** Se crean dos clientes con el mismo CIF BAC o el mismo CIF COM (queda
violada la unicidad del cliente).

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CargaDeCatalogosAceptacion.cs`: `Un_CIF_BAC_con_dos_CIF_COM_en_el_archivo_o_al_reves_es_error`,
`Un_CIF_BAC_solo_y_un_CIF_COM_solo_son_el_mismo_cliente_nuevo_si_otra_fila_trae_los_dos`).

---

#### L-32 — Lectura de celdas: números sin ceros, notación científica, filas vacías y número de fila de Excel

**Qué prueba.** DN-04, "Lectura del archivo": un CIF o un código de plan que Excel guardó como número
(`123`) se lee como texto y se rellena con ceros; un número en notación científica (`1.23E+08`)
se rechaza en esa celda: no se puede reconstruir con certeza. Una fila totalmente vacía se saltea;
el número de fila de los mensajes es **el de Excel**. Las demás hojas del archivo se ignoran.

**Necesita.** Un archivo armado con la celda A2 numérica `123`, A3 con `1.23E+08`, una fila 4 totalmente
vacía, una fila 5 con error, y una segunda hoja `Instrucciones` con texto.

**Cómo.** Subir el archivo y leer la columna L de las filas 2, 3 y 5.

**Qué tiene que pasar.** La fila 2 se interpreta como CIF `000000123`. La fila 3 lleva error en CIF BAC
por la notación científica. La fila 4 no genera resultado. El error de la fila 5 dice "fila 5", la
que se ve a la izquierda en Excel. La hoja `Instrucciones` no molesta.

**Cómo se verifica.** La columna L de las filas 2, 3 y 5 y la cantidad de filas del contador `filas`
(no vacías).

**Cómo se sabe que falló.** La fila 3 se acepta (un cliente mal creado con un CIF inventado) o el número
de fila no coincide con el de Excel (el usuario corrige otra fila).

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CatalogosMasivosApiAceptacion.cs`: `Una_celda_con_error_de_Excel_no_se_graba_en_silencio`;
`CargaDeCatalogosAceptacion.cs`: `Un_aviso_del_lector_es_error_de_su_fila_con_el_encabezado_de_su_columna`).

---

#### L-33 — Fecha documento: celda de fecha o `dd/mm/aaaa`; futura, ilegible o anterior al 01/01/1900 es error

**Qué prueba.** Se acepta una celda de fecha de Excel o texto `dd/mm/aaaa`; cualquier otra forma es
error. Una fecha futura es error (un documento firmado no puede tener fecha de mañana). Una anterior
al 01/01/1900 también (Dataverse no admite fechas anteriores a 1753 y reventaría la pasada 2 sin
decir qué fila). "Hoy" es **la fecha de Nicaragua** (UTC menos 6 horas).

**Necesita.** Un cliente existente y un correo existente; el archivo de cada variante. **La fecha de
"mañana" se calcula como UTC menos 6 horas**, no con la fecha local de la máquina: entre las 18:00 y las
24:00 de Nicaragua la fecha UTC ya es la de mañana, y una prueba armada con la fecha UTC daría un
resultado equivocado.

**Cómo.** Subir un archivo con la autorización repetida en varias filas, cada una con una Fecha
documento distinta: (1) celda de fecha de ayer; (2) texto `15/09/2026`; (3) `2026-09-15` (otro
formato); (4) mañana de Nicaragua; (5) `31/12/1899`; (6) `01/01/1900`.

**Qué tiene que pasar.** (1), (2) y (6): sin error. (3): error de formato en Fecha documento. (4) y (5):
error.

**Cómo se verifica.** La columna L del `resultado`; la fecha guardada de la autorización creada.

**Cómo se sabe que falló.** (4) pasa: la fecha de hoy se calculó en UTC. (5) pasa la pasada 1 y revienta
la 2 (aparece `MPPP-CAR-009` sin que la fila 5 tuviera un error de pasada 1): se perdió el tope de 1900.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CargaDeCatalogosAceptacion.cs`: `Fecha_documento_acepta_celda_de_fecha_y_texto_dd_mm_aaaa_sin_hora`,
`Fecha_documento_futura_o_ilegible_es_error`, `Fecha_documento_anterior_a_1900_es_error_en_la_pasada_1`,
`Fecha_documento_desde_el_1_de_enero_de_1900_vale`;
`CatalogosMasivosApiAceptacion.cs`: `La_fecha_de_hoy_la_decide_quien_construye_el_nucleo`).

---

#### L-34 — Una fila reporta todos sus errores, y ningún mensaje cita un valor entero

**Qué prueba.** DN-04, "Formato de los motivos" y "Lectura": **se reportan todos los errores de la fila**,
no solo el primero (para corregir de una vez), separados por ` | `; el texto va en el orden errores,
la acción y las advertencias; y una celda enorme se cita acortada (60 caracteres) o no se cita, para
que Resultado nunca se acerque al tope de 32.767 caracteres de una celda de Excel.

**Necesita.** Un cliente existente `ZZP-Cliente A`.

**Cómo.** Subir una fila con cinco errores a la vez (CIF COM inválido, Moneda inválida sin Tipo
formato, correo mal formado con Nombre contacto, fecha futura, comentario de 51 caracteres) y otra
fila con una Razón social de 40.000 caracteres.

**Qué tiene que pasar.** La columna L de la primera fila lista los cinco errores, cada uno con el
formato `<Columna>: <qué pasa>. <Qué hacer>.`, separados por ` | `; la de la segunda, un error que
no reproduce los 40.000 caracteres (a lo sumo 60, acortado con `…`) y la celda no pasa de 32.767.

**Cómo se verifica.** La columna L del `resultado` y su largo.

**Cómo se sabe que falló.** Solo aparece el primer error: el usuario corrige, sube de nuevo, ve el
segundo, y así cinco vueltas. O el resultado no se puede abrir en Excel por exceder el tope de una
celda.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CargaDeCatalogosAceptacion.cs`: `Una_fila_reporta_todos_sus_errores_no_solo_el_primero`,
`El_texto_lleva_errores_luego_la_accion_y_al_final_las_advertencias`,
`Ningun_valor_enorme_en_ninguna_columna_produce_un_Resultado_enorme`,
`Correo_del_ejecutivo_de_mas_de_320_es_error_corto_y_no_entra_a_las_claves`;
`ExcelDeCatalogosAceptacion.cs`: `Un_texto_de_mas_de_32767_caracteres_es_error_y_uno_de_32767_no`,
`Un_valor_del_usuario_de_mas_de_32767_caracteres_se_escribe_acortado_en_el_eco_sin_fallar`,
`Un_caracter_que_XML_no_admite_se_reemplaza_y_nunca_tira_el_armado`).

---

### Grupo 4 — Excel 2, Activar / Desactivar: reglas por fila

Cada caso arma un archivo de `ActivarDesactivar` (columnas Tipo, Accion, CIF BAC, CIF COM, Codigo plan,
Correo), lo sube a `cambiarestadocatalogos` como el Administrador de planes y lee la columna **Resultado**
(G). El cambio de estado es **todo o nada** igual que la carga.

#### L-35 — Regla 1: Tipo o Accion fuera de su lista es error (sin distinguir mayúsculas ni espacios)

**Qué prueba.** Tipo es uno de `CLIENTE`, `PLAN`, `CORREO`, `AUTORIZACION_CLIENTE`; Accion es `ACTIVAR` o
`DESACTIVAR`. Se comparan sin distinguir mayúsculas ni espacios a los lados.

**Necesita.** Un cliente existente `ZZP-Cliente A`.

**Cómo.** Subir un archivo con: (1) Tipo = `PRODUCTO`; (2) Accion = `BORRAR`; (3) Tipo = ` cliente `, Accion =
`desactivar` (A de `Cliente A`).

**Qué tiene que pasar.** (1) y (2): error que nombra Tipo o Accion. (3): sin error.

**Cómo se verifica.** La columna G del `resultado`.

**Cómo se sabe que falló.** (3) se rechaza: la comparación distingue mayúsculas o espacios. (1) se acepta
y la fila se ignora en silencio.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`tests/Sanic.Mppp.Plugins.Tests/Aceptacion/CambioDeEstadoAceptacion.cs`: `Tipo_fuera_de_su_lista_es_error`,
`Accion_fuera_de_su_lista_es_error`, `Tipo_y_accion_se_comparan_sin_mayusculas_ni_espacios_a_los_lados`).

---

#### L-36 — Regla 2: cada Tipo exige sus claves y ninguna otra

**Qué prueba.** `CLIENTE` y `AUTORIZACION_CLIENTE` llevan CIF BAC y/o CIF COM (si vienen los dos, tienen
que apuntar al mismo cliente); `PLAN` lleva Codigo plan; `CORREO` lleva Correo; `AUTORIZACION_CLIENTE`
lleva además Correo. Si falta una clave, error; si **sobra** una (por ejemplo, un código de plan en una
fila `CORREO`), error: "una celda que no se usa es una intención que no se cumple".

**Necesita.** Un cliente, un plan, un correo y su autorización existentes del set.

**Cómo.** Subir un archivo con: (1) `CLIENTE` sin CIF; (2) `CLIENTE` con CIF y un Correo de más; (3) `PLAN`
sin Codigo plan; (4) `PLAN` con Codigo plan y CIF BAC de más; (5) `CORREO` con Correo y Codigo plan de
más; (6) `AUTORIZACION_CLIENTE` con CIF pero sin Correo; (7) `CLIENTE` con los dos CIF de clientes
distintos; (8) `CLIENTE` con solo CIF COM (debe pasar).

**Qué tiene que pasar.** (1) a (7): error que nombra la clave que falta o la que sobra; (8): sin error.

**Cómo se verifica.** La columna G del `resultado`.

**Cómo se sabe que falló.** (2), (4) o (5) pasan: la celda de más se ignora y quien cargó cree que algo
se aplicó.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CambioDeEstadoAceptacion.cs`: `Cada_tipo_exige_sus_claves_y_ninguna_otra`,
`Cliente_por_CIF_BAC_por_CIF_COM_o_por_los_dos_si_coinciden`,
`Cliente_con_CIF_que_no_coinciden_inexistente_o_sin_CIF_es_error`).

---

#### L-37 — Regla 3: el registro tiene que existir; en `AUTORIZACION_CLIENTE`, el par (correo, cliente) tiene que existir

**Qué prueba.** Si el cliente, el plan, el correo o la autorización no existe, error. Que el correo y el
cliente existan **por separado** no alcanza para `AUTORIZACION_CLIENTE`: tiene que existir la
autorización de ese correo sobre ese cliente.

**Necesita.** `ZZP-Cliente A` y `ZZP-Cliente B`; el correo `zzp.uno@empresa.com` autorizado solo sobre
`Cliente A`.

**Cómo.** Subir un archivo con: (1) `CLIENTE` con un CIF que no existe; (2) `PLAN` con un código que no
existe; (3) `CORREO` con un correo que no existe; (4) `AUTORIZACION_CLIENTE` de `Cliente B` + `zzp.uno`
(los dos existen, el par no).

**Qué tiene que pasar.** Las cuatro filas con error: no existe.

**Cómo se verifica.** La columna G del `resultado`.

**Cómo se sabe que falló.** (4) pasa y crea una autorización: el cambio de estado se está usando como
alta.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CambioDeEstadoAceptacion.cs`: `Cliente_con_CIF_que_no_coinciden_inexistente_o_sin_CIF_es_error`,
`Plan_por_codigo_normalizado_inexistente_o_invalido`, `Correo_por_correo_normalizado_inexistente_o_invalido`,
`Autorizacion_por_cliente_es_el_par_correo_cliente_y_tiene_que_existir_como_par`).

---

#### L-38 — Regla 4: lo que ya está en el estado pedido es "Sin cambios", no error

**Qué prueba.** Pedir DESACTIVAR de algo ya inactivo, o ACTIVAR de algo ya activo, da "Sin cambios: ya
estaba inactivo" / "ya estaba activo". No es error, y los contadores `sincambios` lo cuentan.

**Necesita.** Un cliente activo y un plan inactivo del set.

**Cómo.** Subir un archivo con: (1) `CLIENTE` ACTIVAR (ya activo); (2) `PLAN` DESACTIVAR (ya inactivo).

**Qué tiene que pasar.** Las dos filas: "Sin cambios: ya estaba activo/inactivo"; `grabado = true`;
`sincambios = 2`, `activados = 0`, `desactivados = 0`; nada cambió en Dataverse.

**Cómo se verifica.** Las columnas del `resultado`, los contadores de la respuesta y `GET
sanic_mppp_tbl_clientes(<id>)?$select=statecode,modifiedon`.

**Cómo se sabe que falló.** Una de las dos filas da error: el archivo no se puede volver a subir
(pierde la idempotencia, L-46).

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CambioDeEstadoAceptacion.cs`: `Lo_que_ya_esta_en_el_estado_pedido_no_cambia_y_no_es_error`,
`Los_contadores_cuentan_cambios_reales`).

---

#### L-39 — Regla 5: el mismo registro dos veces con acciones opuestas es error; con la misma acción, se aplica una vez

**Qué prueba.** Dos filas que nombran el mismo registro con acciones **opuestas**: error en las dos,
nombrando a la otra. Con la **misma acción**: se aplica una vez y la segunda lleva advertencia de
repetida. Se detecta aunque el registro se nombre de formas distintas (por CIF BAC en una fila y por
CIF COM en otra) y aunque alguna de las filas tenga otro error.

**Necesita.** Un cliente activo `ZZP-Cliente A` y su plan `ZP81`.

**Cómo.** Subir un archivo con: filas 2 y 3 `CLIENTE` `Cliente A` DESACTIVAR y ACTIVAR (opuestas); filas
4, 5 y 6 `PLAN` `ZP81` DESACTIVAR (por código, y otras dos repeticiones) (repetida).

**Qué tiene que pasar.** Filas 2 y 3: error cada una, que nombra a la otra fila. Filas 4 a 6: sin error;
la fila 4 aplica; la 5 y la 6 llevan advertencia "ya lo pide la fila 4 con la misma acción. Se aplica una
sola vez.". Como el archivo tiene errores, no se graba nada.

**Cómo se verifica.** La columna G del `resultado`; y el segundo envío, sin las filas opuestas: el plan
queda inactivo una sola vez y `desactivados = 1`.

**Cómo se sabe que falló.** Las opuestas se aplican en el orden de las filas (queda el estado de la
última): se pierde la intención de quien cargó el archivo.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CambioDeEstadoAceptacion.cs`: `El_mismo_registro_con_la_misma_accion_se_aplica_una_vez_y_la_segunda_fila_lo_advierte`,
`El_mismo_registro_con_acciones_opuestas_es_error_en_las_dos_filas_nombrando_a_la_otra`,
`Opuestas_y_repetidas_se_detectan_aunque_una_de_las_filas_tenga_otro_error`,
`Una_repetida_de_tres_filas_que_nombran_el_registro_de_formas_distintas_se_aplica_una_vez`).

---

#### L-40 — Regla 6: advertencias de efecto, calculadas sobre el estado final del archivo

**Qué prueba.** Se calculan **después de aplicar el archivo entero**. Desactivar un `CLIENTE` o un
`CORREO` advierte "deja sin efecto N autorizaciones por cliente activas" (solo si la fila realmente
desactiva algo). Activar una `AUTORIZACION_CLIENTE` cuyo cliente o correo queda inactivo advierte "no
va a estar vigente: el cliente / el correo está inactivo", **aunque la autorización ya estuviera
activa**.

**Necesita.** Un cliente `ZZP-Cliente A` con dos autorizaciones activas; un correo con autorizaciones
sobre dos clientes; un cliente `ZZP-Cliente I` inactivo con una autorización inactiva; una
autorización ya activa cuyo cliente está inactivo.

**Cómo.** Subir un archivo con: (1) `CLIENTE` `Cliente A` DESACTIVAR; (2) `CORREO` del correo compartido
DESACTIVAR; (3) `AUTORIZACION_CLIENTE` de `Cliente I` ACTIVAR (el cliente queda inactivo); (4)
`AUTORIZACION_CLIENTE` ya activa sobre un cliente inactivo, ACTIVAR (no cambia nada); (5) un archivo
con `CLIENTE` ACTIVAR de `Cliente I` **y** `AUTORIZACION_CLIENTE` ACTIVAR de `Cliente I` (el estado final
tiene al cliente activo: no debe advertir).

**Qué tiene que pasar.** (1) "deja sin efecto 2 autorizaciones por cliente activas"; (2) "deja sin efecto
2" (las de los dos clientes); (3) y (4) "no va a estar vigente: el cliente está inactivo", también la
(4) aunque no cambie nada; (5) sin advertencia de vigencia.

**Cómo se verifica.** La columna G del `resultado`.

**Cómo se sabe que falló.** (5) advierte: el efecto se calculó contra el estado de **antes** del
archivo, no contra el final. (4) no advierte: quien pidió ACTIVAR cree que opera.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CambioDeEstadoAceptacion.cs`: `Desactivar_un_cliente_o_un_correo_advierte_cuantas_autorizaciones_activas_deja_sin_efecto`,
`El_efecto_se_cuenta_sobre_el_estado_final_del_archivo`,
`Activar_una_autorizacion_cuyo_cliente_o_correo_queda_inactivo_advierte_que_no_va_a_estar_vigente`,
`Pedir_activar_una_autorizacion_que_ya_esta_activa_pero_no_vigente_tambien_advierte`).

---

#### L-41 — Regla 7 y sin cascada: el cambio de estado es el nativo, y desactivar un cliente o un correo no toca lo que depende de él

**Qué prueba.** El cambio es el nativo (`statecode`/`statuscode`, Activo/Inactivo). **Sin cascada**
(decisión 4): desactivar un Cliente o un Correo **no** desactiva sus planes ni sus autorizaciones;
dejan de valer solas porque la vigencia exige que todo esté activo (`02` §2.4). Al escribir con el
usuario que llama, queda en la auditoría quién desactivó cada cosa (DD-18).

**Necesita.** `ZZP-Cliente A` activo con un plan activo y dos autorizaciones activas.

**Cómo.**
1. Subir un archivo con `CLIENTE` `Cliente A` DESACTIVAR.
2. Consultar el plan y las autorizaciones del cliente.
3. Consultar la auditoría del cliente.

**Qué tiene que pasar.** El cliente queda `statecode = 1`; el plan y las dos autorizaciones siguen en
`statecode = 0`; la auditoría del cliente registra el cambio hecho por `# User 3`.

**Cómo se verifica.** `GET sanic_mppp_tbl_clientes(<id>)?$select=statecode`; `GET
sanic_mppp_tbl_plans?$filter=_sanic_clienteid_value eq <id>&$select=statecode`; `GET
sanic_mppp_tbl_autorizacionclientes?$filter=_sanic_clienteid_value eq <id>&$select=statecode`; `GET
audits?$filter=_objectid_value eq <id>` (con el privilegio de auditoría del service principal).

**Cómo se sabe que falló.** El plan o las autorizaciones pasaron a inactivos: alguien agregó una
cascada que el aprobador descartó. La auditoría dice que lo hizo `SYSTEM`: el plugin escribió con el
servicio equivocado.

**Automatizable.** API — cubierto por pruebas automáticas C# en el cambio nativo y la regla de
estado (`tests/Sanic.Mppp.Plugins.Tests/Aceptacion/CatalogosMasivosAceptacion.cs`:
`El_escritor_cambia_el_estado_nativo_en_la_tabla_de_cada_tipo`,
`Si_un_cambio_de_estado_falla_lanza_con_su_fila_y_no_sigue`; `CatalogosMasivosApiAceptacion.cs`:
`El_cambio_de_estado_escribe_con_el_usuario_y_cuenta_lo_que_cambio`). La ausencia de cascada y la
auditoría real contra Dev no tienen prueba automática: se verifican en Dev.

---

#### L-42 — El ejemplo del aprobador, de punta a punta en Dev

**Qué prueba.** Las seis filas del ejemplo de DN-04 (CLIENTE DESACTIVAR por CIF BAC, CLIENTE ACTIVAR por
CIF COM, PLAN DESACTIVAR, CORREO DESACTIVAR, AUTORIZACION_CLIENTE DESACTIVAR por CIF BAC y
AUTORIZACION_CLIENTE ACTIVAR por CIF COM) contra una Custom API real, con un registro real de cada
tipo, y que cada fila hace exactamente lo que dice el diseño: la 2 apaga el cliente; la 4 apaga **solo**
ese plan; la 5 apaga el correo en **todos** los clientes donde está autorizado; la 6 apaga **solo** la
autorización de María sobre ese cliente y María sigue autorizada sobre otros.

**Necesita.** Los datos del ejemplo armados por el propio set con la marca `ZZP-`: dos clientes, un plan,
dos correos, y autorizaciones cruzadas (el correo 2 autorizado sobre dos clientes).

**Cómo.** Subir el archivo con las seis filas a `cambiarestadocatalogos` como `# User 3` y consultar el
estado de cada registro antes y después.

**Qué tiene que pasar.** `grabado = true`; `desactivados`, `activados` y `sincambios` suman 6 filas
con su resultado "Activado" / "Desactivado" en la columna G; el estado de cada registro es el del diseño; el
texto de resumen del diálogo en la interfaz dice "Cambio de estado terminado: N activados, N
desactivados y N sin cambios.".

**Cómo se verifica.** La columna G y los `statecode` de los registros tras la llamada.

**Cómo se sabe que falló.** La fila 5 apaga solo un cliente (se interpretó el correo por el cliente de la
fila) o la fila 6 apaga la autorización de María en todos los clientes.

**Automatizable.** API — sesión real del Administrador de planes. Pendiente: no hay evidencia de Dev
para la Custom API de cambio de estado.

---

### Grupo 5 — Todo o nada

#### L-43 — Con un error de fila no se graba nada: se descarga el resultado y el diálogo dice "No se grabó nada"

**Qué prueba.** Decisión 5 y "Mecanismo": con un solo error de fila, la Pasada 1 devuelve el Excel de
resultado y **no escribe**. El navegador descarga `<nombre original>-resultado.xlsx`, la fila con error
dice `Error: ...` en la columna Resultado y el diálogo dice "No se grabó nada: {errores} filas con
errores. Revisá la columna Resultado del archivo que se descargó, corregí y volvé a subirlo.".

**Necesita.** `local/sesion-admin_planes.json`; `pruebas/gui/datos/dn04/dn04-carga-con-error.xlsx` (una
fila con error).

**Cómo.** `MPPP_USUARIO=admin_planes npx playwright test dn-04`: en Clientes, "Carga masiva" →
"Cargar archivo", elegir `dn04-carga-con-error.xlsx`; capturar la descarga y el diálogo.

**Qué tiene que pasar.** Se descarga `dn04-carga-con-error-resultado.xlsx` con 1 fila de datos, cuya columna
L empieza con `Error: `; el diálogo contiene "No se grabó nada" (o "Nothing was saved").

**Cómo se verifica.** La prueba `'Cargar archivo' con un error de fila descarga el resultado y avisa que no
se grabó nada` del spec. **Límite de este caso**: el spec no consulta Dataverse, así que "no se grabó
nada" lo afirma el texto del diálogo, no un conteo de registros; el conteo en Dataverse lo da L-45
(rollback) y, para este archivo en concreto, hay que consultar `sanic_mppp_tbl_clientes` por el CIF
del archivo.

**Cómo se sabe que falló.** No hay descarga y el diálogo muestra un error técnico: la Custom API reventó
en vez de devolver el resultado. Hay descarga pero el diálogo dice "Carga terminada": se grabó con un
error en el archivo.

**Automatizable.** GUI — sesión `admin_planes`. Ya construido en
`pruebas/gui/casos/dn-04-catalogos-masivos.spec.mjs`.

---

#### L-44 — Las filas sin error dicen "Sin errores — no se grabó: el archivo tiene errores en otras filas"

**Qué prueba.** Cuando el archivo tiene errores, las filas **sin** error no dicen "Creado" (no se creó
nada), sino `Sin errores — no se grabó: el archivo tiene errores en otras filas`. Es el texto que
evita que el usuario crea que sus filas buenas ya quedaron.

**Necesita.** Un archivo de cuatro filas, tres válidas (que crearían un cliente, un plan y un correo
cada una) y una con error. Sesión del Administrador de planes.

**Cómo.** Subir el archivo a `cargarcatalogos`. Repetir con un archivo de `ActivarDesactivar` de cuatro
filas con una con error.

**Qué tiene que pasar.** La fila con error: `Error: ...`; las tres sanas: `Sin errores — no se grabó: el
archivo tiene errores en otras filas`; `grabado = false`; los contadores de creados en 0. Lo mismo para
el cambio de estado, con `activados`, `desactivados` y `sincambios` en 0.

**Cómo se verifica.** La columna L (o G) del `resultado`; los contadores; y `GET
sanic_mppp_tbl_clientes?$filter=startswith(sanic_cifbac,'<prefijo>')&$count=true` = 0.

**Cómo se sabe que falló.** Las filas sanas dicen "Creado": el usuario cree que se grabaron y no se grabó
nada. O dicen "Sin cambios": se confunden con una fila que ya existía.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CargaDeCatalogosAceptacion.cs`: `Con_un_error_en_cualquier_fila_no_se_crea_nada_y_las_filas_sanas_lo_dicen`;
`CambioDeEstadoAceptacion.cs`: `Con_un_error_en_cualquier_fila_no_cambia_nada`,
`Una_fila_reporta_todos_sus_errores_y_el_texto_respeta_el_orden`;
`CatalogosMasivosApiAceptacion.cs`: `Con_errores_de_fila_devuelve_el_resultado_sin_escribir_nada`).

---

#### L-45 — Si una escritura falla en la pasada 2, la transacción deja cero registros

**Qué prueba.** DN-04, "Todo o nada en dos pasadas": la pasada 2 corre dentro de la transacción de la
Custom API; si una escritura falla, la excepción deshace todo. Verificado en Dev el 2026-10-01
(D-72): `IsInTransaction = true` en la etapa 30, y una excepción deja cero registros, con el
servicio del usuario y con SYSTEM.

**Necesita.** Una carga que falle **a mitad de la pasada 2**: hoy, la forma comprobada es el corte de la
base a los ~40 s con un archivo grande (200 o 300 filas, con un tope de prueba más alto: ver el riesgo de
L-17 y L-20). Sesión real del Administrador de planes.

**Cómo (solo si se decide repetir).** Con una versión de prueba de `carga.filas.maximo`, subir un
archivo de 300 filas peor caso; después contar clientes, planes, correos y autorizaciones del rango.

**Qué tiene que pasar.** La llamada falla, y el conteo de los cuatro tipos de registro es cero.

**Cómo se verifica.** `dataverse_api.py GET
"sanic_mppp_tbl_clientes?$filter=startswith(sanic_cifbac,'<prefijo>')&$count=true"`, igual para los otros tres.

**Cómo se sabe que falló.** Queda alguno de los registros de las filas que ya se habían grabado: se
perdió la transacción (se atrapó un error y se siguió, que es el camino que Learn desaconseja y que
el spike D-72 no usó).

**Automatizable.** API. Es el caso que mide D-75; no se repite de rutina por el riesgo de L-17.

---

### Grupo 6 — Idempotencia, volumen y orden de escritura

#### L-46 — Re-subir el mismo archivo no crea nada ("Sin cambios")

**Qué prueba.** DN-04, "Lo que NO es error": una fila donde todo ya existía queda "Sin cambios: todo ya
existía"; el archivo se puede volver a subir sin efectos (carga idempotente). Si una carga se cortó, se
repite entera.

**Necesita.** Un archivo de carga sin errores que cree un cliente, un plan, un correo y una autorización
por fila, ya cargado una vez por el propio caso (con los registros creados bajo la marca `ZZP-`).

**Cómo.**
1. Subir el archivo la primera vez y anotar los contadores.
2. Subir **el mismo archivo** otra vez.
3. Subirlo una tercera vez con una fila más (la nueva sí se crea).

**Qué tiene que pasar.** Paso 2: `grabado = true`, `clientescreados = 0`, `planescreados = 0`,
`correoscreados = 0`, `autorizacionescreadas = 0`, y la columna L de cada fila dice "Sin cambios: todo
ya existía...". Paso 3: se crea solo lo de la fila nueva.

**Cómo se verifica.** Los contadores de la respuesta y la columna L del `resultado`; `GET
sanic_mppp_tbl_clientes?$filter=startswith(sanic_cifbac,'<prefijo>')&$count=true` igual en los pasos 1 y 2.

**Cómo se sabe que falló.** Aparecen registros duplicados (la clave alterna del cliente o del correo
rechaza, y el usuario ve `MPPP-CAR-009`); o el segundo envío da errores de fila contra los registros
que acaba de crear él mismo.

**Automatizable.** API — sesión real del Administrador de planes.

---

#### L-47 — Un archivo desnormalizado crea cada cosa una sola vez, y escribe en orden de dependencias

**Qué prueba.** El mismo cliente y el mismo correo se repiten en varias filas (una por plan) y se crean
**una sola vez**; la fila de menor número es la que "crea" lo que aparece en varias. El orden de
escritura es Clientes → Planes → Correos → Autorizaciones, sin importar el orden de las filas del
archivo.

**Necesita.** Un archivo de 6 filas: un cliente nuevo con 3 planes nuevos y 2 correos (cada correo
autorizado sobre ese cliente), repartidos en filas en desorden (la autorización antes que el plan del
mismo cliente en el archivo).

**Cómo.** Subir el archivo y contar.

**Qué tiene que pasar.** `clientescreados = 1`, `planescreados = 3`, `correoscreados = 2`,
`autorizacionescreadas = 2`; la fila 2 dice "Creado: cliente, plan, correo, autorización" y las demás
solo lo que ellas crean; en Dataverse, un cliente, tres planes, dos correos y dos autorizaciones.

**Cómo se verifica.** Los contadores y las consultas por el CIF del cliente.

**Cómo se sabe que falló.** Se crean dos clientes con el mismo CIF o el segundo plan apunta a un cliente
que todavía no existía (el orden de dependencias se rompió).

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CargaDeCatalogosAceptacion.cs`: `Archivo_desnormalizado_tipico_crea_cada_cosa_una_sola_vez_y_la_atribuye_a_la_primera_fila`,
`Los_resultados_salen_en_el_orden_recibido_y_lo_crea_la_fila_de_menor_numero`;
`CatalogosMasivosAceptacion.cs`: `El_escritor_crea_en_orden_con_las_columnas_exactas_y_resuelve_las_referencias_a_lo_nuevo`).

---

#### L-48 — 100 filas, el peor caso, se graban dentro del presupuesto de 30 s

**Qué prueba.** El tope de 100 filas (D-75) se sostiene en Dev: un archivo de 100 filas en el **peor caso**
(cada fila crea cliente, plan, correo y autorización: 4 escrituras) se graba completo, y por debajo
del presupuesto de escritura de 30 s.

**Necesita.** `carga.filas.maximo = 100`; sesión real del Administrador de planes; un archivo de 100
filas peor caso con CIF, códigos y correos del rango reservado del set.

**Cómo.** Subir el archivo a `cargarcatalogos` y medir el tiempo de la llamada.

**Qué tiene que pasar.** `grabado = true`; 100 clientes, 100 planes, 100 correos y 100 autorizaciones
creados; tiempo por debajo de 30 s.

**Cómo se verifica.** Los contadores y la medición del tiempo de la llamada; el conteo por rango de CIF.

**Cómo se sabe que falló.** `MPPP-CAR-008`: el tiempo pasó de 30 s (con 25,1 s medidos, el margen contra
el presupuesto propio es de unos 5 s, y una plataforma más lenta lo puede cruzar: se anota como
riesgo, no como defecto). `MPPP-CAR-011`: la base cortó la transacción (10928).

**Automatizable.** API — sesión real del Administrador de planes. Tarda unos 25 s y escribe 400
registros: es un caso caro; se corre cuando cambia el código de la carga.

---

### Grupo 7 — Dueño del cliente nuevo y permisos (D-73)

#### L-49 — El cliente nuevo queda con el ejecutivo como dueño; el que cargó queda como creador

**Qué prueba.** DN-04 "Decisiones" 3 y "Mecanismo": un cliente nuevo no lleva una columna nueva de
ejecutivo; se resuelve a `ownerid` (RF-19, D-23), que **ya es** el ejecutivo asignado. La Custom API
escribe con el usuario que llama (no con SYSTEM), así que `createdby` es el Administrador de planes y
queda quién dio de alta cada cosa (DD-18). D-73 pedía comprobar que el Administrador de planes puede
crear un Cliente con `ownerid` de OTRO usuario (privilegio Assign).

**Necesita.** Sesión del Administrador de planes (`# User 3`) y el correo de `# User 1` (Ejecutivo); un
archivo con clientes nuevos con el Correo ejecutivo de `# User 1`.

**Cómo.** Subir el archivo y consultar cada cliente creado.

**Qué tiene que pasar.** Cada cliente nuevo tiene `_ownerid_value` = `# User 1` y `_createdby_value` =
`# User 3`.

**Cómo se verifica.** `dataverse_api.py GET
"sanic_mppp_tbl_clientes?$filter=startswith(sanic_cifbac,'<prefijo>')&$select=sanic_nombre,_ownerid_value,_createdby_value"`.

**Cómo se sabe que falló.** `_ownerid_value` es `# User 3`: el cliente quedó a nombre de quien cargó y
no aparece en "Mis clientes" del ejecutivo. `_createdby_value` es `SYSTEM`: la API escribió con el
servicio equivocado y se perdió la auditoría de quién lo dio de alta.

**Automatizable.** API — sesión real del Administrador de planes (el sistema de permisos de Dataverse es
el que se prueba: un doble no sirve).

---

#### L-50 — El Ejecutivo dueño ve el cliente nuevo y sus autorizaciones

**Qué prueba.** D-73: que el Ejecutivo dueño ve el cliente cargado por el Administrador de planes, y las
autorizaciones de ese cliente.

**Necesita.** Los clientes creados por L-49 con dueño `# User 1`, y `local/sesion-ejecutivo.json`.
Verificar con L-02 que la sesión del Ejecutivo está viva.

**Cómo.** Como `# User 1` (sesión real o `MSCRMCallerID`): consultar los clientes del rango y sus
autorizaciones por cliente.

**Qué tiene que pasar.** El Ejecutivo ve cada cliente nuevo y las autorizaciones que cuelgan de él.

**Cómo se verifica.** `GET sanic_mppp_tbl_clientes?$filter=startswith(sanic_cifbac,'<prefijo>')&$count=true`
y `GET sanic_mppp_tbl_autorizacionclientes?$filter=_sanic_clienteid_value eq <id>` con
`MSCRMCallerID` de `# User 1`.

**Cómo se sabe que falló.** El conteo del Ejecutivo da cero mientras el del Administrador de planes da
cien: un cliente a nombre de alguien que no lo ve (el defecto que la regla 4 de la carga quiere evitar).
Cuidado con el falso verde de la sesión muerta: la guarda de L-02 aplica acá también.

**Automatizable.** API — impersonación de `# User 1`.

---

#### L-51 — La puerta de las dos Custom API: solo el Administrador de planes entra

**Qué prueba.** DN-04, "Contratos": `ExecutePrivilegeName` es `prvCreatesanic_mppp_tbl_cliente` para la
carga y `prvWritesanic_mppp_tbl_cliente` para el cambio de estado, y solo los tiene el Administrador de
planes (`04` §2). **El privilegio es solo la puerta**: cada `Create` y cada `Update` de la pasada 2
vuelve a pasar por la seguridad de la plataforma con el usuario que llama.

**Necesita.** Ejecutivo (`# User 1`), Supervisor (`# User 2`) y Administrador técnico (`# User 4`), cada
uno resuelto por alias y con su rol **verificado** en `systemuserroles_association` (no supuesto); un
archivo válido.

**Cómo.** Con `MSCRMCallerID` de cada uno de los tres usuarios, llamar a `cargarcatalogos` y a
`cambiarestadocatalogos` con `{"plantilla": true}` (la llamada más barata que igual pasa por la
puerta), y una vez con un archivo válido.

**Qué tiene que pasar.** Las seis llamadas se rechazan por falta de privilegio, con error de plataforma
(no un `MPPP-CAR-0xx`), **antes** de leer el archivo.

**Cómo se verifica.** El código HTTP (403) y el mensaje de la plataforma.

**Cómo se sabe que falló.** Alguno de los tres entra: el registro real de la Custom API quedó con otro
privilegio, o el rol del usuario cambió (la lección del 2026-09-24: verificar el rol primero). Un
rechazo con `MPPP-CAR-0xx` quiere decir que la llamada pasó la puerta y el rechazo lo hizo el plugin.

**Automatizable.** API — impersonación por `MSCRMCallerID`, mismo mecanismo que `E-09`.

---

### Grupo 8 — Comentarios en la autorización por cliente

#### L-52 — La columna Comentarios existe, en el formulario y en las dos vistas

**Qué prueba.** DN-04, "Columna nueva": `sanic_mppp_tbl_autorizacioncliente.sanic_comentarios` es `T(50)`,
opcional, con nombre visible "Comentarios" en 1033 (`01` §0: las etiquetas de metadatos van en 1033 y la plataforma las usa si al destino le falta su idioma; *corregido el 2026-10-02*, decía "y en 3082").
Va al formulario **Autorizacion por cliente**, en la sección de la evidencia, **después de la fecha del
documento**, y a las vistas "Autorizaciones por cliente activas" y "Correos autorizados sobre este
cliente".

**Necesita.** `local/sesion-admin_planes.json`; una autorización por cliente con un comentario
(`ZZP-comentario`) creada por el set.

**Cómo.**
1. `GET EntityDefinitions(LogicalName='sanic_mppp_tbl_autorizacioncliente')/Attributes(LogicalName='sanic_comentarios')?$select=MaxLength,RequiredLevel,DisplayName`.
2. Abrir el formulario de la autorización y ubicar el campo.
3. Abrir las dos vistas y buscar la columna.

**Qué tiene que pasar.** `MaxLength = 50`, `RequiredLevel = None`, etiqueta "Comentarios" en 1033; el campo está en la sección de la evidencia, debajo de la fecha del documento; la columna aparece
en las dos vistas.

**Cómo se verifica.** La consulta de metadata del paso 1 y la inspección del formulario y de las vistas
(`FormXml` / `layoutxml`, o con Playwright como el resto de los casos de `06-seguridad-vistas-formularios.md`).
La muestra exportada de la columna se verificó estructuralmente el 2026-10-01 (`diseno/06`, 3.13),
pero eso comprueba que la columna existe, no que se vea donde el diseño dice.

**Cómo se sabe que falló.** La columna existe pero no aparece en el formulario (falta publicar) o aparece
en otra sección: cambió el `formxml` sin pasar por el diseño.

**Automatizable.** API (metadata) y GUI (formulario y vistas) — sesión `admin_planes`.

---

#### L-53 — Comentarios solo en `AutorizacionCliente`, no en `AutorizacionPlan`; y auditada

**Qué prueba.** Decisión 6: los comentarios van solo en la autorización por cliente, no en la de plan. La
columna es **auditada** (DD-18).

**Necesita.** El service principal para consultar metadata.

**Cómo.**
1. `GET EntityDefinitions(LogicalName='sanic_mppp_tbl_autorizacionplan')/Attributes?$filter=LogicalName eq 'sanic_comentarios'`.
2. `GET EntityDefinitions(LogicalName='sanic_mppp_tbl_autorizacioncliente')/Attributes(LogicalName='sanic_comentarios')?$select=IsAuditEnabled`.

**Qué tiene que pasar.** Paso 1: ninguna columna. Paso 2: `IsAuditEnabled` con valor `true`.

**Cómo se verifica.** Las dos consultas de metadata.

**Cómo se sabe que falló.** Paso 1 devuelve la columna: se agregó a `AutorizacionPlan` sin decisión del
aprobador. Paso 2 en `false`: un cambio de comentario no deja huella de quién lo hizo.

**Automatizable.** API.

---

#### L-54 — Comentarios: hasta 50 caracteres por Excel; 51 es error de fila

**Qué prueba.** Con la columna K del Excel de carga: 50 caracteres valen; 51 es error de esa fila (en la
pasada 1, nombrando la columna).

**Necesita.** Un cliente existente y un correo nuevo con Nombre contacto; dos filas con un comentario de
50 y de 51 caracteres.

**Cómo.** Subir el archivo con las dos filas a `cargarcatalogos`.

**Qué tiene que pasar.** La fila de 50: sin error; la de 51: error en Comentarios.

**Cómo se verifica.** La columna L del `resultado`; y, tras una carga sin errores con la de 50, `GET
sanic_mppp_tbl_autorizacionclientes(<id>)?$select=sanic_comentarios`.

**Cómo se sabe que falló.** La de 51 pasa la pasada 1 y revienta en la 2 con `MPPP-CAR-009`: el validador
de la carga y el largo real de la columna no coinciden.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CargaDeCatalogosAceptacion.cs`: `Largos_maximos_de_correo_nombre_y_comentarios`).

---

#### L-55 — Comentarios: la plataforma rechaza 51 caracteres también por el formulario y por Web API

**Qué prueba.** Que el límite de 50 no lo aplica solo la carga masiva: `sanic_comentarios` es `T(50)` en la
plataforma, y un comentario de 51 caracteres desde el formulario o por Web API se rechaza. ("El largo se
puede ampliar después; achicar, no.")

**Necesita.** `local/sesion-admin_planes.json`; una autorización por cliente del set.

**Cómo.**
1. En el formulario de la autorización, pegar un texto de 51 caracteres en Comentarios y guardar.
2. `PATCH sanic_mppp_tbl_autorizacionclientes(<id>)` con `{"sanic_comentarios": "<51 caracteres>"}`.
3. Lo mismo con exactamente 50.

**Qué tiene que pasar.** Con 51: el campo no admite más de 50 o el guardado falla con un error de
longitud; con 50: se guarda.

**Cómo se verifica.** El código HTTP del PATCH y el valor guardado.

**Cómo se sabe que falló.** Se guardan 51 caracteres: la columna no es `T(50)` en el entorno.

**Automatizable.** API (PATCH) y GUI (formulario).

---

#### L-56 — Los comentarios de la carga quedan guardados en la autorización nueva, y no se pisan en una existente

**Qué prueba.** Regla 14: la autorización nueva se crea con J (fecha) y K (comentarios). Regla 12: si ya
existe activa y el archivo trae otro comentario, error; la carga no lo pisa.

**Necesita.** Un cliente existente; dos correos: uno nuevo y uno ya autorizado sobre ese cliente con
comentario `viejo`.

**Cómo.** Subir dos archivos: (a) el correo nuevo con K = `alta masiva`; (b) el correo ya autorizado con
K = `nuevo`.

**Qué tiene que pasar.** (a): "Creado: correo, autorización"; la autorización tiene `sanic_comentarios =
'alta masiva'`. (b): error: el comentario no coincide con lo grabado; sigue `viejo`.

**Cómo se verifica.** La columna L del `resultado` y `GET
sanic_mppp_tbl_autorizacionclientes?$filter=_sanic_clienteid_value eq <id>&$select=sanic_comentarios`.

**Cómo se sabe que falló.** (b) cambia el comentario a `nuevo`: la carga está modificando lo existente.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CargaDeCatalogosAceptacion.cs`: `Autorizacion_existente_activa_queda_ya_existia_y_sus_datos_tienen_que_coincidir`,
`Largos_maximos_de_correo_nombre_y_comentarios`; `tests/Sanic.Mppp.Plugins.Tests/Unitarias/Catalogos/PlanificadorDeCargaTests.cs`,
`tests/Sanic.Mppp.Plugins.Tests/Unitarias/Datos/CatalogosMasivosTests.cs`).

---

### Grupo 9 — El parámetro `carga.filas.maximo`

#### L-57 — `carga.filas.maximo` vale 100, y sin un valor usable el tope es 100

**Qué prueba.** DN-04, "Parámetro nuevo": `carga.filas.maximo` es un número, **100** (era 300; medido en
Dev el 2026-10-01, D-75), y lo usan las dos Custom API. Si falta, no es un entero o es menor que 1,
se usa un valor quemado de **100** (mismo criterio que `rpa.horas.tolerancia`). El peso del archivo
reutiliza `lectura.limites`. Los parámetros se leen con SYSTEM: el Administrador de planes no tiene
ningún privilegio sobre Parámetro.

**Necesita.** El service principal; el parámetro `carga.filas.maximo` sembrado en Dev con
`sanic_version = 2` y valor `100`.

**Cómo.**
1. `GET sanic_mppp_tbl_parametros?$filter=sanic_nombre eq 'carga.filas.maximo' and statecode eq 0&$select=sanic_version,sanic_valor&$orderby=sanic_version desc&$top=1`.
2. Confirmar que L-15 (101 filas → rechazo) y L-48 (100 filas → grabadas) coinciden con el valor.

**Qué tiene que pasar.** La versión activa más alta vale `100`. Sin versión activa, con un valor `abc` o
con `0`, la Custom API usa 100.

**Cómo se verifica.** La consulta del paso 1. Para el valor por defecto, la suite automática.

**Cómo se sabe que falló.** El parámetro vale 300 (la versión 1 sigue siendo la activa más alta): el tope
de la base de Dev se vuelve a cruzar (D-75). Un valor mal escrito que revienta la llamada en vez de
degradar a 100.

**Automatizable.** API — el valor en Dev se prueba con L-15 y L-48; el valor por defecto está cubierto por
pruebas automáticas C#
(`CatalogosMasivosApiAceptacion.cs`: `Sin_un_carga_filas_maximo_usable_el_tope_es_100`,
`Los_parametros_se_leen_con_SYSTEM_nunca_con_el_usuario`).

---

### Resultados

Estado al **2026-10-02** (los 10 pendientes se corrieron ese día). Los tres estados y lo que significa cada uno están en "Cómo leer el estado de
cada caso", arriba. Ni un solo caso está marcado como EJECUTADO sin evidencia citada.

- **[correr.py 2026-10-02]** = `python3 herramientas/pruebas/correr.py L`, primera corrida del arnés: **57 OK · 0 FALLA
  · 0 N-A** en 20 minutos, Dev limpio al terminar. La auditoría del parámetro confirma L-17: versión temporal creada a
  las 15:37:34 UTC, desactivada 31 s después (el corte del 008) y borrada en `cerrar()`.

Evidencia citada en la tabla:

- **[GUI]** = `pruebas/gui/casos/dn-04-catalogos-masivos.spec.mjs`, Playwright, 2026-10-01: tres corridas
  seguidas en verde como Administrador de planes (`MPPP_USUARIO=admin_planes`) y una como Ejecutivo
  (`MPPP_USUARIO=ejecutivo`).
- **[Dev 1.0.22.0]** = pruebas con la sesión real del Administrador de planes (`# User 3`) contra la
  Custom API en Dev, 2026-10-01, ensamblado 1.0.22.0 (`diseno/PENDIENTES.md`, D-73 y D-75).
- **[GUI 2026-10-02]** = el mismo spec, extendido con L-03, L-04, L-08, L-09 y el formulario de L-55: dos
  corridas seguidas en verde como Administrador de planes (14 de 14), una como Supervisor (4 de 4), una
  como Ejecutivo (4 de 4) y una como Administrador técnico (4 saltados por motivo verificado: su
  sitemap no tiene el área Catalogos). Datos propios (`pruebas/gui/datos_dn04_l09.py`, `--sembrar` y
  `--limpiar`), limpios al terminar.
- **[Dev API 2026-10-02]** = llamadas con las sesiones reales de los cuatro usuarios (verificados con
  `WhoAmI` y su rol en `systemuserroles`) y consultas de metadatos con el service principal, ensamblado
  1.0.22.0. Datos `ZZP-` sembrados con la propia carga masiva y borrados al terminar.
- **[Dev 1.0.21.0]** = la misma sesión, ensamblado 1.0.21.0, con el presupuesto todavía en 100 s
  (D-75).
- **[C#]** = suite de pruebas automáticas de C# en `tests/Sanic.Mppp.Plugins.Tests` (1580 en verde al
  2026-10-01): cubre, **sin correr en Dev**, lo que cada caso cita.

| Caso | Estado | Evidencia / qué falta |
|---|---|---|
| L-01 los dos menús en las cuatro grillas (Administrador de planes) | EJECUTADO — OK | [GUI]: las cuatro pruebas `<grilla>: los dos menús están...`, 3 corridas seguidas en verde |
| L-02 el Ejecutivo no ve los menús | EJECUTADO — OK | [GUI]: las cuatro pruebas `un Ejecutivo no ve los menús...`, 1 corrida en verde |
| L-03 Supervisor y Administrador técnico no los ven | EJECUTADO — OK | [GUI 2026-10-02]: Supervisor, sin menús en las 4 grillas. Administrador técnico: las 4 saltadas porque su sitemap no tiene el área Catalogos (coincide con la matriz de `04`) |
| L-04 los menús siguen el idioma de la interfaz | EJECUTADO — OK (1033); 3082 N-A | [correr.py 2026-10-02, segunda corrida, 57 OK]: con las cuatro tablas en etiquetas localizadas y la metadata regenerada, en una interfaz en inglés (el botón nativo dice "New") ancla y opciones dicen EXACTAMENTE "Bulk load", "Bulk activate/deactivate", "Upload file" y "Download template". La mitad en 3082 es N-A: Dev solo tiene 1033 instalado |
| L-05 plantilla `CargaCatalogos.xlsx` | EJECUTADO — OK | [GUI]: nombre del archivo, 12 encabezados con Resultado en L, sin filas |
| L-06 plantilla `ActivarDesactivar.xlsx` | EJECUTADO — OK | [GUI]: nombre del archivo, 7 encabezados con Resultado en G, sin filas |
| L-07 plantilla = resultado = archivo corregido | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `ExcelDeCatalogosAceptacion.cs`. No se re-subió un resultado corregido en Dev |
| L-08 cancelar el selector y extensión distinta de `.xlsx` | EJECUTADO — OK | [GUI 2026-10-02]: el selector se abre con el clic; sin archivo (`setFiles([])`, que dispara `change` y no `cancel`) no hay indicador, diálogo ni llamada; con un `.txt`, el aviso exacto del JS y cero llamadas a las dos Custom API |
| L-09 indicador por etapas, resumen de carga grabada y refresco de la grilla | EJECUTADO — OK | [GUI 2026-10-02]: los tres `show` en orden (espía) y "Validando…" visible; descarga del resultado; "Load finished: 1 clients, 1 plans, 1 emails and 1 authorizations created. 1 rows with warnings: check the result file."; el cliente nuevo aparece en la grilla sin recargar; cambio de estado: "Status change finished: 0 activated, 1 deactivated and 1 unchanged. 1 rows with warnings: check the result file." |
| L-10 `MPPP-CAR-001` | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CatalogosMasivosApiAceptacion.cs` |
| L-11 `MPPP-CAR-002` | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CatalogosMasivosApiAceptacion.cs` |
| L-12 `MPPP-CAR-003` | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CatalogosMasivosApiAceptacion.cs` |
| L-13 `MPPP-CAR-004`, sin descarga | EJECUTADO — OK | [GUI]: `'Cargar archivo' con la hoja equivocada muestra MPPP-CAR-004 y no descarga nada` |
| L-14 `MPPP-CAR-005` | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] solo (a) encabezado distinto y (b) columna de más; **(c) columnas intercambiadas y (d) mayúsculas/espacios no tienen prueba de aceptación propia**: se corren en Dev |
| L-15 `MPPP-CAR-006`, 101 filas | EJECUTADO — OK | [Dev 1.0.22.0]: 101 filas → `MPPP-CAR-006` sin escribir (D-75) |
| L-16 `MPPP-CAR-007` | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CatalogosMasivosApiAceptacion.cs` |
| L-17 `MPPP-CAR-008` por presupuesto de 30 s | EJECUTADO — OK | [correr.py 2026-10-02]: con una versión temporal del tope en 200, 200 filas que crean todo cortaron con `MPPP-CAR-008` a los ~31 s, con "(30 segundos)" en el mensaje; la versión volvió a 100. Además [C#] `CatalogosMasivosApiAceptacion.cs` y `CatalogosMasivosAceptacion.cs` |
| L-18 `MPPP-CAR-009` por error de dato al grabar | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CatalogosMasivosApiAceptacion.cs`. No se provocó en Dev |
| L-19 `MPPP-CAR-010` | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CatalogosMasivosApiAceptacion.cs` |
| L-20 `MPPP-CAR-011` por falla de plataforma, sin nombrar fila | EJECUTADO — OK | [Dev 1.0.21.0]: 200 filas fallaron a los 42 s, el usuario recibió `MPPP-CAR-011` y la traza dijo `Fila 66 (código 0x80072324)`; además [C#]. No se repitió con 1.0.22.0: con el tope en 100 ya no se alcanza por volumen |
| L-21 ningún error de archivo muestra el texto de la librería | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CatalogosMasivosApiAceptacion.cs` |
| L-22 reglas 1 y 2 (cliente y coherencia de CIF) | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CargaDeCatalogosAceptacion.cs` |
| L-23 regla 3 (cliente existente) | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CargaDeCatalogosAceptacion.cs` |
| L-24 regla 4 (cliente nuevo y ejecutivo) | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CargaDeCatalogosAceptacion.cs`, `CatalogosMasivosAceptacion.cs`. En Dev, solo se probó el ejecutivo válido (L-49); los motivos de error de usuario no se corrieron |
| L-25 reglas 5 y 7 (plan: datos sin código, plan nuevo) | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CargaDeCatalogosAceptacion.cs` |
| L-26 regla 6 (plan existente) | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CargaDeCatalogosAceptacion.cs` |
| L-27 reglas 8 y 9 (correo sin datos, formato) | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CargaDeCatalogosAceptacion.cs` |
| L-28 reglas 10 y 11 (correo existente o nuevo) | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CargaDeCatalogosAceptacion.cs` |
| L-29 reglas 12, 13 y 14 (autorización) | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CargaDeCatalogosAceptacion.cs`. La vista "sin evidencia" no se miró en Dev |
| L-30 regla 15 (coherencia dentro del archivo) | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CargaDeCatalogosAceptacion.cs` |
| L-31 regla 16 (un CIF con dos CIF) | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CargaDeCatalogosAceptacion.cs` |
| L-32 lectura de celdas | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CatalogosMasivosApiAceptacion.cs`, `CargaDeCatalogosAceptacion.cs` |
| L-33 Fecha documento | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CargaDeCatalogosAceptacion.cs`, `CatalogosMasivosApiAceptacion.cs` |
| L-34 todos los errores de la fila y valores acortados | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CargaDeCatalogosAceptacion.cs`, `ExcelDeCatalogosAceptacion.cs` |
| L-35 Excel 2, regla 1 (listas) | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CambioDeEstadoAceptacion.cs` |
| L-36 Excel 2, regla 2 (claves exigidas) | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CambioDeEstadoAceptacion.cs` |
| L-37 Excel 2, regla 3 (existir) | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CambioDeEstadoAceptacion.cs` |
| L-38 Excel 2, regla 4 (sin cambios) | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CambioDeEstadoAceptacion.cs` |
| L-39 Excel 2, regla 5 (opuestas y repetidas) | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CambioDeEstadoAceptacion.cs` |
| L-40 Excel 2, regla 6 (advertencias de efecto) | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CambioDeEstadoAceptacion.cs` |
| L-41 Excel 2, regla 7 y sin cascada | EJECUTADO — OK | [Dev API 2026-10-02]: desactivar `ZZP-Cliente 1` lo dejó inactivo y su plan y sus dos autorizaciones siguieron activos, con la advertencia "deja sin efecto 2 autorizaciones"; `modifiedby` y la auditoría (un `Update` de `# User 3`, no de SYSTEM) registran al usuario real. Ojo: la auditoría apareció con unos minutos de demora (Learn: los logs viven en el almacenamiento de logs) |
| L-42 el ejemplo del aprobador de punta a punta | EJECUTADO — OK | [Dev API 2026-10-02]: las seis filas sobre 3 clientes, 1 plan y 2 correos con autorizaciones cruzadas: 2 activados, 4 desactivados, 0 sin cambios; el plan solo, el correo de Juan inactivo con sus dos autorizaciones intactas ("deja sin efecto 2"), solo maria@1 desactivada (María sigue sobre el Cliente 2), juan@3 activada con la advertencia de correo inactivo. El texto del resumen en la interfaz lo cubre L-09 |
| L-43 error de fila: no se graba, se descarga el resultado, "No se grabó nada" | EJECUTADO — OK | [GUI]: `'Cargar archivo' con un error de fila descarga el resultado y avisa que no se grabó nada`. Límite: el spec no cuenta registros en Dataverse |
| L-44 filas sanas: "Sin errores — no se grabó" | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CargaDeCatalogosAceptacion.cs`, `CambioDeEstadoAceptacion.cs`. El archivo del GUI tiene una sola fila: no ejercita este texto |
| L-45 rollback: cero registros tras una falla de la pasada 2 | EJECUTADO — OK | [correr.py 2026-10-02]: la carga cortada de L-17 dejó cero clientes, planes y correos. Antes: D-72 (`spikes/d72-transaccion-capi/CONCLUSIONES.md`: `IsInTransaction = true` y una excepción deja cero registros) y la medición de D-75 ("El todo o nada funcionó en las cuatro (cero registros)", 200 y 300 filas) |
| L-46 re-subir el mismo archivo: "Sin cambios" | EJECUTADO — OK | [Dev 1.0.22.0]: re-subir las 100 filas → 0 creados en 0,6 s. El texto por fila ("Sin cambios: todo ya existía...") lo cubre [C#] `CargaDeCatalogosAceptacion.cs`; no se capturó la columna Resultado de esa corrida |
| L-47 desnormalizado: una sola vez, en orden de dependencias | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CargaDeCatalogosAceptacion.cs`, `CatalogosMasivosAceptacion.cs`. La corrida de Dev de 100 filas no era desnormalizada (cada fila creaba todo) |
| L-48 100 filas peor caso dentro del presupuesto | EJECUTADO — OK | [Dev 1.0.22.0]: 100 filas que crean cliente, plan, correo y autorización, grabadas en 25,1 s. Margen contra los 30 s: unos 5 s |
| L-49 dueño = el ejecutivo; creador = el Administrador de planes | EJECUTADO — OK | [Dev 1.0.22.0]: 100 de 100 clientes con dueño "# User 1" (Ejecutiva) y creador "# User 3" (Administrador de planes) (D-73) |
| L-50 el Ejecutivo dueño ve cliente y autorizaciones | EJECUTADO — OK | [Dev 1.0.22.0]: la Ejecutiva los ve, a ellos y a sus autorizaciones (D-73) |
| L-51 la puerta de las dos Custom API | EJECUTADO — OK | [Dev API 2026-10-02]: 12 de 12 llamadas (3 usuarios × 2 API × plantilla y archivo) rechazadas con 403 `0x80040220` de la plataforma, sin `MPPP-CAR`; el privilegio que falta es exactamente `prvCreatesanic_mppp_tbl_cliente` (carga) y `prvWritesanic_mppp_tbl_cliente` (cambio de estado). Los archivos traían un error de fila: aunque la puerta fallara, no podían escribir |
| L-52 la columna Comentarios en formulario y vistas | EJECUTADO — OK | [Dev API 2026-10-02]: `MaxLength` 50, `RequiredLevel` None, "Comentarios" en 1033; en el `formxml` de "Autorizacion por cliente", sección "Evidencia de la autorizacion", justo después de `sanic_fechadocumento`; en el `layoutxml` de las dos vistas |
| L-53 solo en `AutorizacionCliente`, auditada | EJECUTADO — OK | [Dev API 2026-10-02]: `AutorizacionPlan` no tiene la columna; en `AutorizacionCliente` es auditada, con la auditoría encendida en la tabla y en la organización |
| L-54 Comentarios: 50 por Excel | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CargaDeCatalogosAceptacion.cs` |
| L-55 Comentarios: 51 rechazado por formulario y Web API | EJECUTADO — OK | [Dev API 2026-10-02]: PATCH con 51 → 400 `0x80044331` ("exceeded the maximum allowed length of '50'"); con 50 → 204 y se guardan 50. [GUI 2026-10-02]: en el formulario, tipear 51 deja 50 en el campo y Ctrl+S guarda 50 |
| L-56 los comentarios de la carga se guardan y no se pisan | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CargaDeCatalogosAceptacion.cs`, `PlanificadorDeCargaTests.cs`, `CatalogosMasivosTests.cs` |
| L-57 `carga.filas.maximo` = 100 y valor por defecto 100 | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CatalogosMasivosApiAceptacion.cs` (valor por defecto). El valor 100 en Dev es la base de L-15 y L-48, que sí están ejecutados |

#### Cuenta por grupo

| Grupo | Casos | EJECUTADO — OK | CUBIERTO POR PRUEBAS AUTOMÁTICAS | PENDIENTE |
|---|---|---|---|---|
| 1 — Menús, plantillas y flujo del navegador (L-01 a L-09) | 9 | 8 | 1 | 0 |
| 2 — Códigos de archivo `MPPP-CAR-001` a `011` (L-10 a L-21) | 12 | 4 | 8 | 0 |
| 3 — Excel 1: reglas por fila (L-22 a L-34) | 13 | 0 | 13 | 0 |
| 4 — Excel 2: reglas por fila (L-35 a L-42) | 8 | 2 | 6 | 0 |
| 5 — Todo o nada (L-43 a L-45) | 3 | 2 | 1 | 0 |
| 6 — Idempotencia, volumen y orden (L-46 a L-48) | 3 | 2 | 1 | 0 |
| 7 — Dueño y permisos (L-49 a L-51) | 3 | 3 | 0 | 0 |
| 8 — Comentarios (L-52 a L-56) | 5 | 3 | 2 | 0 |
| 9 — Parámetro (L-57) | 1 | 0 | 1 | 0 |
| **Total** | **57** | **24** | **33** | **0** |
