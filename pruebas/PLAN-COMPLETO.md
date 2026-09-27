# Plan de pruebas funcionales — MPPP fase 1
Proyecto `2026-001-referencias-planes-pago` · Entorno **Dev** · 2026-09-23, corregido 2026-09-27
Este documento junta los 10 archivos de `pruebas/` en uno solo, para leerlo de corrido.
Los archivos separados (y los Excel) están en el repositorio, en `pruebas/`. Regenerado a partir
de ellos el 2026-09-27 para que las dos copias no se desincronicen: si editás algo, editalo en el
archivo separado y volvé a generar este.

---
## Índice y cómo usarlo


Proyecto: `2026-001-referencias-planes-pago` · Entorno: **Dev**

Los 170 componentes de la fase 1 están construidos y verificados **estructuralmente**: existen,
están bien formados y están en la solución. Lo que estas pruebas comprueban es otra cosa, la que
importa: **que hagan lo que el diseño dice que hacen**.

Ningún flujo se ejecutó nunca. Ningún comando de la barra se ejecutó nunca en un navegador.

---

## Cómo está organizado

| Archivo | Qué cubre | Casos |
|---|---|---|
| `00-preparacion.md` | Datos, buzón, carpeta. **Se hace una sola vez** | 6 |
| `01-clasificacion-del-correo.md` | `MPPP-REC`, `MPPP-ING`, el plugin liviano, reglas de nivel **Correo** | 8 |
| `02-reglas-de-solicitud.md` | Reglas de nivel **Solicitud**: adjunto, Excel, estructura, filas | 7 |
| `03-reglas-de-registro.md` | Reglas de nivel **Registro**: las 8 que deciden fila por fila | 14 |
| `04-comunicaciones.md` | `MPPP-COM`, `MPPP-ENV`: acuse y respuesta final, "como máximo una vez" | 7 |
| `05-app-y-comandos.md` | Los 8 comandos de la barra y el diálogo de motivo | 13 |
| `06-seguridad-vistas-formularios.md` | Roles, perfil de columnas, sitemap, 20 vistas, 10 formularios | 12 |
| `07-vigilancia.md` | `MPPP-VIG`: los 5 bloques de recuperación | 6 |
| `08-integridad-y-plugins.md` | Steps de plugin, catálogo de reglas, claves únicas | 9 |

**82 casos.** Cada uno trae: qué prueba, cómo hacerlo, **qué tiene que pasar** y cómo verificarlo.

---

## Antes de empezar

1. Corré **`00-preparacion.md`** entero. Sin eso, la mitad de los casos no
   tiene sentido: citan planes y autorizaciones que hay que crear.
2. Tené a mano la herramienta de lectura, que evita andar clickeando para ver qué pasó:

   ```bash
   python3 herramientas/pruebas/ver_solicitud.py MPPP-00001234
   python3 herramientas/pruebas/ver_solicitud.py --ultima
   ```

   Muestra, de una sola vez: estado de la Solicitud, sus contadores, **cada regla con su
   resultado y su razón**, las filas con su estado y su mensaje, y la Bitácora completa.

---

## Cómo anotar los resultados

Cada archivo termina con una tabla para llenar. Poné:

- **OK** — pasó exactamente lo esperado
- **FALLA** — pasó otra cosa. Anotá qué pasó, con el número de solicitud
- **N/A** — no se pudo correr, y por qué

**Un caso que "casi" pasa es un FALLA.** Si el estado es el correcto pero el mensaje al cliente
dice otra cosa, es un FALLA: el mensaje lo lee el cliente del banco.

---

## Dos advertencias

**Esto escribe en Dev.** Los casos crean Solicitudes, Filas y Bitácora reales, y mandan correos
reales desde el buzón. Todo con datos ficticios y con planes `PR*` que se borran después
(`preparar_datos.py --limpiar`), pero el ruido en las tablas queda.

**El orden importa dentro de cada archivo.** Varios casos usan la Solicitud que dejó el anterior
(sobre todo en `04` y `05`). Cada caso dice de qué depende.

---

## Qué NO cubren estas pruebas

Para que no haya sorpresas después:

- **Rendimiento real.** `E14-volumen-100.xlsx` mira que 100 filas entren en el presupuesto de
  10 s (`03` §1), pero no es una prueba de carga: son 7.500 filas por mes en producción.
- **Concurrencia real de carga.** Dos correos exactamente simultáneos, o dos supervisores
  aprobando la misma fila en el mismo instante, no se prueban acá. **D-43 ya NO está abierta**:
  se implementó el sistema de claim con vencimiento (`sanic_claimhasta`, fase 2). Los casos que
  prueban ese mecanismo son de fase 2 y los está escribiendo otro agente en paralelo (`09-*.md`
  en adelante); referenciá esos archivos cuando existan, no los IDs de sus casos (no los controla
  este documento).
- **Migración desde SharePoint** (RF-16): es de Producción, no de Dev.
- **Fases 2 y 3** (RPA, histórico) — salvo lo ya cerrado y mencionado arriba (D-43).

---

## Qué corre solo y qué es manual

`herramientas/pruebas/corredor.py` automatiza **45 de los 82 casos** contra Dev, sin correo real,
usando `simulador.py` para llamar a las dos Custom API (clasificación y validación) igual que lo
haría el flujo. Antes de correr algo a mano, fijate si ya lo cubre:

```bash
python3 herramientas/pruebas/corredor.py --cobertura   # solo imprime qué cubre y qué no, sin tocar Dev
python3 herramientas/pruebas/corredor.py               # corre los 45 y limpia sus datos al final
```

| Archivo | Automatizados (API, `corredor.py`) | Manuales |
|---|---|---|
| `00` | P-03, P-04, P-05, P-06 | P-01, P-01b, P-02 |
| `01` | C-01, C-02, C-03, C-04, C-07, C-08 | C-05, C-06 |
| `02` | S-01 a S-07 (los 7) | — |
| `03` | R-01 a R-14 (los 14) | — |
| `04` | — | M-01 a M-07 (los 7): todos exigen `Mail.Send` real |
| `05` | A-06, A-07, A-10 (proxy de servidor, `--con-comportamiento-roles`) | A-01 a A-05, A-08, A-09, A-11 a A-13: ribbon/UI de navegador |
| `06` | G-06, G-12 (además `G-00`/`G-00b` corren aparte, con `verificar_accesos.py` y `comportamiento_roles.py`) | G-01 a G-05, G-07 a G-11: lo que SE VE en el navegador |
| `07` | — | V-01 a V-06 (los 6): dependen del schedule real de Power Automate |
| `08` | I-01 a I-09 (los 9) | — |

`NO_CUBIERTOS` dentro de `corredor.py` trae el motivo exacto de cada caso manual, caso por caso.
**No repitas a mano lo que ya corre solo**: corré `corredor.py` primero y dedicá el tiempo humano
a lo que de verdad exige un navegador, un correo real o esperar un schedule.

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
> un total exacto.

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

## 01 — Ingesta y clasificación del correo

Cubre `MPPP-REC`, `MPPP-ING` y el plugin liviano `sanic_mppp_capi_clasificarcorreo`, o sea las
reglas de **nivel Correo**: `ES_CORREO_NUEVO` y `REMITENTE_RECONOCIDO`.

**Antes**: `00-preparacion.md` completo.
**Cómo verificar**: `python3 herramientas/pruebas/ver_solicitud.py --ultima`

> Dejá pasar **1 o 2 minutos** después de enviar: el disparador de correo nuevo sondea cada minuto.

---

### C-01 · Camino feliz — correo nuevo, remitente autorizado, Excel válido

**Qué prueba.** La cadena entera: `MPPP-REC` → `MPPP-ING` → clasificación → validación.

**Necesita.** El `REMITENTE` de P-02, autorizado sobre el plan `PR11` (P-03); el adjunto
`E01-feliz-formato11.xlsx` (P-04); buzón compartido y los cinco flujos activos (P-01, P-01b, P-05).
Ningún rol de la app: llega por correo.

**Cómo.** Desde tu `REMITENTE`, mandá a `mppp@55xljh.onmicrosoft.com`:

- Asunto: `Prueba C-01 inclusiones`
- Cuerpo: cualquier texto
- Adjunto: **`E01-feliz-formato11.xlsx`**

**Esperado.**

| Qué | Valor |
|---|---|
| Solicitud | se crea, número `MPPP-########` |
| Estado | **En proceso** |
| adjuntos / excel | `1 / 1` |
| filas t/v/r | `3 / 3 / 0` |
| archivos | correo: **sí** · excel: **sí** |
| outlook message id | **sí** |
| Reglas de Solicitud | las 5 en **Cumplida** |
| Las 3 filas | **Validada**, sin mensaje |
| Referencia de cada fila | `007` + cuenta rellena con ceros a la izquierda = **20 caracteres** |
| Bitácora | `Ingresada` (MPPP-REC) y `Validacion terminada` (Custom API) |
| Acuse | `sanic_acusecontenido` con texto, y **acuse enviado con fecha** |
| El correo | ya **no** está en la bandeja: está en `Procesados` |

**Referencia esperada, fila por fila** (regla D-17: código de banco 3 + cuenta rellena a 17):

| Cuenta en el Excel (14 dígitos) | Rellena a 17 | Referencia esperada (20) |
|---|---|---|
| `10203040506070` | `00010203040506070` | `00700010203040506070` |
| `11223344556677` | `00011223344556677` | `00700011223344556677` |
| `99887766554433` | `00099887766554433` | `00700099887766554433` |

> Las tres tienen que medir **exactamente 20 caracteres** y empezar con `007`. Si alguna mide
> distinto o rellena por la derecha, es **FALLA**: la referencia va a AS400 y un dígito corrido
> manda la plata a otra cuenta.

**Automatizable.** API (`corredor.py`, caso C-01, vía `simulador.py`, sin correo real). Con correo
real es manual: enviar y esperar 1-2 minutos.

---

### C-02 · Una respuesta NO se procesa

**Qué prueba.** `ES_CORREO_NUEVO` con una respuesta (DF-08). Es lo que impide un bucle de correos
con un "fuera de oficina".

**Necesita.** El acuse de C-01 en la bandeja del `REMITENTE`, para responderlo.

**Cómo.** Tomá el acuse que te llegó en C-01 y **respondelo** (Responder, no correo nuevo). Adjuntá
`E01-feliz-formato11.xlsx` para que quede claro que no se procesa aunque traiga plantilla.

**Esperado.**

| Qué | Valor |
|---|---|
| Estado | **No es correo nuevo** |
| motivo clasificación | dice que es una respuesta a otro correo |
| filas | **0** — no se abrió el Excel |
| `ES_CORREO_NUEVO` | **NO CUMPLIDA** |
| `REMITENTE_RECONOCIDO` | **Omitida** (la bloqueó la anterior) |
| Comunicación al cliente | **NINGUNA**. No te tiene que llegar nada |
| Aviso en la app | a todos los ejecutivos (campana) |

> **Lo más importante de este caso es lo que NO pasa**: si te llega una respuesta automática del
> sistema, es FALLA grave. Ahí está el bucle infinito que DF-08 evita.

**Automatizable.** API (`corredor.py`, caso C-02, simula la respuesta sin correo real).

---

### C-03 · Un reenvío SÍ se procesa

**Qué prueba.** Que `ES_CORREO_NUEVO` distinga reenvío de respuesta por el prefijo del asunto
(`FW:`, `FWD:`, `RV:`, `REENV:`, de `correo.prefijos.reenvio`).

**Necesita.** El `REMITENTE` autorizado; el adjunto `E01-feliz-formato11.xlsx`.

**Cómo.** Reenviá (Forward) cualquier correo a `mppp@…`, con asunto empezando en `FW:` y adjuntando
`E01-feliz-formato11.xlsx`.

**Esperado.** Igual que C-01: **En proceso**, 3 filas Validadas, `ES_CORREO_NUEVO` **Cumplida**.

> Un reenvío trae `In-Reply-To`/`References` igual que una respuesta. Lo único que lo distingue es
> el prefijo. Si este caso queda en **No es correo nuevo**, la regla no está mirando el asunto.

**Automatizable.** API (`corredor.py`, caso C-03).

---

### C-04 · Remitente no reconocido

**Qué prueba.** `REMITENTE_RECONOCIDO` (RF-02).

**Necesita.** Una dirección de correo que NO esté en Autorizados (distinta del `REMITENTE` de
P-03); el adjunto `E01-feliz-formato11.xlsx`.

**Cómo.** Mandá desde **otra** dirección, una que NO esté en Autorizados, con
`E01-feliz-formato11.xlsx` adjunto.

**Esperado.**

| Qué | Valor |
|---|---|
| Estado | **No reconocida** |
| motivo clasificación | que el remitente no tiene autorización vigente |
| filas | **0** |
| `ES_CORREO_NUEVO` | Cumplida |
| `REMITENTE_RECONOCIDO` | **NO CUMPLIDA** |
| Comunicación | **NINGUNA** |
| Dónde se ve | vista **Por clasificar** del sitemap |

**Automatizable.** API (`corredor.py`, caso C-04, con un remitente propio no autorizado).

---

### C-05 · El correo se mueve y cambia de identificador

**Qué prueba.** El paso 7 de `MPPP-ING` y DF-04: el identificador de Outlook **cambia al mover**, y
por eso hay una acción aparte que lo vuelve a guardar.

**Necesita.** Acceso al buzón compartido para ver la carpeta `Procesados`; la Solicitud de C-01.

**Cómo.** Usá la Solicitud de C-01. Mirá `Procesados` en el buzón y el campo del visor.

**Esperado.**

- El correo de C-01 está en **`Procesados`**, no en la bandeja de entrada.
- `outlook message id` dice **sí**.

> Si el correo quedó en la bandeja, `MPPP-VIG` lo va a reingerir a los 15 minutos y vas a ver una
> segunda Solicitud… **no**: la clave única lo impide (C-07). Vas a ver reintentos en la Bitácora.

**Automatizable.** Manual (exige un correo real movido de verdad por `MPPP-ING`; no hay Web API
para inspeccionar carpetas de Outlook).

---

### C-06 · El `.eml` queda guardado

**Qué prueba.** RF-10 y el insumo del plugin liviano: sin el `.eml` no hay clasificación.

**Necesita.** Sesión con acceso de lectura a Solicitud (Ejecutivo o Supervisor) para abrir la
Solicitud de C-01 y descargar el archivo.

**Cómo.** Abrí la Solicitud de C-01 en la app, pestaña con los archivos.

**Esperado.** `sanic_correocrudo` tiene un archivo `correo.eml` descargable, y `sanic_exceloriginal`
tiene el `.xlsx` con **el nombre original** del adjunto.

> Descargá el `.eml` y abrilo con un editor de texto: tienen que verse las cabeceras
> (`From:`, `Subject:`, `Message-ID:`). Es lo que lee el clasificador.

**Automatizable.** Manual (el `.eml` real lo captura `MPPP-ING` desde Outlook; en `corredor.py` el
`.eml` y el Excel los sube el propio script, así que "verificarlo" ahí probaría el corredor, no el
sistema).

---

### C-07 · El mismo correo dos veces no duplica

**Qué prueba.** La clave alternativa sobre `sanic_messageid` (DD-03) y el paso 4b de `MPPP-ING`.

**Necesita.** Acceso al buzón compartido (para mover el correo de `Procesados` de vuelta a la
bandeja) o, si se simula, la Solicitud de C-01.

**Cómo.** No se puede reenviar el mismo `Message-ID` desde el cliente de correo, así que se fuerza:

```bash
python3 herramientas/pruebas/ver_solicitud.py --ultima     # anotá el número
```

Pedile a quien tenga acceso al buzón que **mueva el correo de C-01 de `Procesados` de vuelta a la
bandeja de entrada**. `MPPP-VIG` lo va a reingerir en los próximos 15 minutos.

**Esperado.**

- **NO** se crea una segunda Solicitud. El total de Solicitudes no cambia.
- La Solicitud original queda como estaba (no se revalida: ya no está en Ingresada).
- En la Bitácora puede aparecer un `Reintento` con origen **MPPP-VIG**.

> Este caso protege contra el peor escenario del sistema: **procesar dos veces la misma plantilla**
> y mandarle al cliente dos acuses distintos.

**Automatizable.** API (`corredor.py`, caso C-07, simulado sin mover correo real). Con correo real
es manual: requiere acceso al buzón compartido y esperar a `MPPP-VIG`.

---

### C-08 · Sin adjunto

**Qué prueba.** `TRAE_ADJUNTO` y, sobre todo, que las reglas que dependen de ella queden
**Omitidas** y no "No cumplidas" (DD-13).

**Necesita.** El `REMITENTE` de P-02; un correo sin ningún archivo adjunto.

**Cómo.** Mandá un correo nuevo desde tu `REMITENTE`, asunto `Prueba C-08 sin adjunto`,
**sin ningún archivo adjunto**.

**Esperado.**

| Regla | Resultado |
|---|---|
| `TRAE_ADJUNTO` | **NO CUMPLIDA** |
| `ADJUNTO_ES_EXCEL` | **Omitida**, razón: la bloqueó `TRAE_ADJUNTO` |
| `UN_SOLO_EXCEL` | **Omitida** |
| `ESTRUCTURA_PLANTILLA` | **Omitida** |
| `TIENE_FILAS` | **Omitida** |

| Qué | Valor |
|---|---|
| Estado | **Rechazada** |
| filas | 0 |
| Acuse | **sí**, y dice que no hay nada que procesar, que no va a recibir otro correo por esta solicitud, y que corrija y reenvíe |

> Ojo con la firma con logo: una imagen incrustada llega como adjunto `isInline = true` y **no
> cuenta** (DF-07). Si este caso dice "trae 1 adjunto", la regla no está filtrando los inline.

**Automatizable.** API (`corredor.py`, caso C-08).

---

### Resultados

| Caso | Solicitud | Resultado | Notas |
|---|---|---|---|
| C-01 camino feliz | | | |
| C-02 respuesta no se procesa | | | |
| C-03 reenvío sí se procesa | | | |
| C-04 remitente no reconocido | | | |
| C-05 el correo se mueve | | | |
| C-06 el `.eml` queda | | | |
| C-07 no duplica | | | |
| C-08 sin adjunto | | | |

---

## 02 — Reglas de nivel Solicitud

Las cinco reglas que miran **el sobre**, no el contenido: si hay adjunto, si es Excel, si es uno
solo, si tiene la estructura de la plantilla y si trae filas.

Cadena de dependencias (`sanic_dependede`):

```
TRAE_ADJUNTO → ADJUNTO_ES_EXCEL → UN_SOLO_EXCEL → ESTRUCTURA_PLANTILLA → TIENE_FILAS
```

Todas con efecto **Rechaza**. Que fallen deja la Solicitud en **Rechazada**, con acuse.

**Lo que más importa de este archivo**: que una regla cuya dependencia falló quede **Omitida** y no
"No cumplida", y que su razón **diga qué regla la bloqueó** (DD-13). Un sistema que dice "falló la
estructura de la plantilla" cuando en realidad no había adjunto le miente al ejecutivo.

**Antes**: `00-preparacion.md`. El caso `TRAE_ADJUNTO` está en `01` como C-08.

---

### S-01 · El adjunto no es un Excel

**Qué prueba.** `ADJUNTO_ES_EXCEL` (D-15).

**Necesita.** El REMITENTE; el adjunto `A01-no-es-excel.txt`.

**Cómo.** Correo nuevo, asunto `Prueba S-01`, adjuntando **solo** `A01-no-es-excel.txt`.

**Esperado.**

| Qué | Valor |
|---|---|
| adjuntos / excel | `1 / 0` |
| `TRAE_ADJUNTO` | Cumplida |
| `ADJUNTO_ES_EXCEL` | **NO CUMPLIDA** |
| `UN_SOLO_EXCEL`, `ESTRUCTURA_PLANTILLA`, `TIENE_FILAS` | **Omitida** |
| Estado | **Rechazada** |
| Acuse | sí |

**Automatizable.** API (`corredor.py`, caso S-01).

---

### S-02 · Dos Excel en el mismo correo

**Qué prueba.** `UN_SOLO_EXCEL`. No se adivina cuál procesar.

**Necesita.** El REMITENTE; los adjuntos `E01-feliz-formato11.xlsx` y
`E13-formato06-referencia-libre.xlsx`.

**Cómo.** Correo nuevo adjuntando **`E01-feliz-formato11.xlsx` y `E13-formato06-referencia-libre.xlsx`**.

**Esperado.**

| Qué | Valor |
|---|---|
| adjuntos / excel | `2 / 2` |
| `UN_SOLO_EXCEL` | **NO CUMPLIDA** |
| `ESTRUCTURA_PLANTILLA`, `TIENE_FILAS` | **Omitida** |
| Estado | **Rechazada** |
| archivos | **excel: NO** — no se subió ninguno, a propósito |

> Que no se suba **ninguno** es parte del diseño: si no sabemos cuál es el bueno, no elegimos.

**Automatizable.** API (`corredor.py`, caso S-02).

---

### S-03 · Un Excel y un archivo que no lo es

**Qué prueba.** Que un adjunto extra que no sea Excel **no** rompa nada: el Excel candidato sigue
siendo el único `.xlsx`.

**Necesita.** El REMITENTE; los adjuntos `E01-feliz-formato11.xlsx` y `A01-no-es-excel.txt`.

**Cómo.** Correo nuevo adjuntando **`E01-feliz-formato11.xlsx` y `A01-no-es-excel.txt`**.

**Esperado.**

| Qué | Valor |
|---|---|
| adjuntos / excel | `2 / 1` |
| Las 5 reglas | **Cumplida** |
| Estado | **En proceso**, 3 filas Validadas |

> Caso real y frecuente: el cliente manda la plantilla y además un PDF de respaldo.

**Automatizable.** API (`corredor.py`, caso S-03).

---

### S-04 · La hoja de datos tiene otro nombre

**Qué prueba.** `ESTRUCTURA_PLANTILLA` contra `plantilla.estructura` (hoja `Datos`).

**Necesita.** El REMITENTE; el adjunto `E12-hoja-renombrada.xlsx`.

**Cómo.** Correo nuevo con **`E12-hoja-renombrada.xlsx`** (la hoja se llama `Datos2`).

**Esperado.**

| Qué | Valor |
|---|---|
| `UN_SOLO_EXCEL` | Cumplida |
| `ESTRUCTURA_PLANTILLA` | **NO CUMPLIDA** |
| `TIENE_FILAS` | **Omitida** |
| Estado | **Rechazada** |

> Esto tiene que ser una **regla fallida**, no un error del sistema. Si en la Bitácora aparece un
> evento `Error` o la Solicitud queda en **Ingresada**, es FALLA: una plantilla mal armada es un
> caso de negocio esperable, no una excepción (`03` §1, "Errores").

**Automatizable.** API (`corredor.py`, caso S-04).

---

### S-05 · Excel con la estructura correcta pero sin filas

**Qué prueba.** `TIENE_FILAS`.

**Necesita.** El REMITENTE; el adjunto `E11-sin-filas.xlsx`.

**Cómo.** Correo nuevo con **`E11-sin-filas.xlsx`**.

**Esperado.**

| Qué | Valor |
|---|---|
| `ESTRUCTURA_PLANTILLA` | Cumplida |
| `TIENE_FILAS` | **NO CUMPLIDA** |
| Estado | **Rechazada** |
| filas t/v/r | `0 / 0 / 0` |
| Acuse | sí |

**Automatizable.** API (`corredor.py`, caso S-05).

---

### S-06 · Volumen: 100 filas

**Qué prueba.** El presupuesto de tiempo (`03` §1: objetivo **< 10 s**) y que `cantidadFilas` = 100
del parámetro se respete.

**Necesita.** El REMITENTE; el adjunto `E14-volumen-100.xlsx`.

**Cómo.** Correo nuevo con **`E14-volumen-100.xlsx`**. **Anotá la hora de envío.**

**Esperado.**

| Qué | Valor |
|---|---|
| filas t/v/r | `100 / 100 / 0` |
| Estado | **En proceso** |
| Las 100 filas | **Validada**, con su referencia de 20 caracteres |
| Tiempo | entre `Ingresada` y `Validacion terminada` en la Bitácora: **menos de 10 s** |

**Cómo medir**: en el visor, restá las dos marcas de la Bitácora. Si pasa de 10 s, anotalo con el
número: no es un FALLA de funcionalidad, pero es un dato para la fase de rendimiento.

**Automatizable.** API (`corredor.py`, caso S-06; mide el mismo presupuesto de tiempo que pide
este caso).

---

### S-07 · El acuse de una Rechazada dice lo que tiene que decir

**Qué prueba.** DD-09 y el texto obligatorio de DF-08. Esto lo lee un cliente del banco.

**Necesita.** Acceso a la bandeja del REMITENTE, para leer el acuse real de un caso Rechazada
(S-01, S-02, S-04, S-05 o C-08).

**Cómo.** Tomá el acuse que llegó en cualquier caso Rechazada (S-01, S-02, S-04, S-05 o C-08) y
leelo completo en tu casilla.

**Esperado.** El correo dice, con todas las letras:

1. El **número de solicitud** (`MPPP-########`).
2. Que **no hay nada que procesar**.
3. Que **no va a recibir otro correo** por esta solicitud.
4. Que **corrija y reenvíe**.
5. El texto obligatorio: *"Para enviar una plantilla nueva o corregida, escriba un correo nuevo a
   esta dirección. No responda a este mensaje: las respuestas no se procesan."*
6. Llega **como respuesta en el hilo** del correo original, y **solo a vos** (el remitente), no a
   quienes fueran en copia.

**FALLA si**: falta cualquiera de los 6 puntos, el texto tiene HTML crudo a la vista, aparece el
nombre de una regla técnica (`TRAE_ADJUNTO` y compañía) o se menciona cualquier SDK o producto.

> El punto 5 no es decorativo: es lo que evita que el cliente conteste el acuse y su respuesta
> caiga en **Por clasificar** para siempre.

**Automatizable.** Manual (exige leer un correo real; `corredor.py` no manda correo real todavía —
`Mail.Send` pendiente).

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

Las ocho reglas que deciden **fila por fila**. Son el corazón del sistema: de acá sale si una
referencia entra a AS400 o no.

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

### R-10 · La referencia de formato 11 se construye sola

**Qué prueba.** `REFERENCIA_FORMATO_11` con D-17 y DD-10, y que la recibida se guarde aparte.

**Necesita.** La Solicitud de C-01 ya creada.

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

**Automatizable.** API (`corredor.py`, caso R-11).

---

### R-12 · Formato 06: la referencia NO se deriva

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

### Resultados

| Caso | Solicitud | Resultado | Notas |
|---|---|---|---|
| R-01 listas inválidas | | | ¿cita el valor? |
| R-02 largos | | | |
| R-03 plan inexistente | | | |
| R-04 plan inactivo | | | |
| R-05 formato 11 no ACH | | | |
| R-06 obligatoriedad | | | fila 4 = Validada |
| R-07 moneda | | | |
| R-08 sin autorización | | | |
| R-09 prioridad + motivos | | | |
| **R-10 referencia construida** | | | **contá los 20** |
| R-11 referencia imposible | | | |
| R-12 formato 06 sin derivar | | | |
| R-13 mezcla | | | |
| R-14 duplicados pasan | | | |

---

## 04 — Comunicaciones al cliente

Cubre `MPPP-COM` (detecta) y `MPPP-ENV` (envía). Las dos comunicaciones son **el acuse** y **la
respuesta final**, y las dos salen como **respuesta en el hilo**, desde el buzón, **solo al
remitente** (DF-03).

**La garantía que hay que romper para que esto falle**: *el cliente nunca recibe dos veces lo
mismo*. Ante la duda, no se reenvía y lo mira una persona (D-21).

**Antes**: `01` y `03`, sobre todo C-01 y R-13.

---

### M-01 · El acuse llega, y llega una sola vez

**Qué prueba.** La cadena `Validación` → estado En proceso → `MPPP-COM` → `MPPP-ENV`.

**Necesita.** Acceso a la bandeja del REMITENTE (para ver el acuse real); la Solicitud de C-01.

**Cómo.** Usá la Solicitud de **C-01**. Mirá tu casilla.

**Esperado.**

| Qué | Valor |
|---|---|
| Llega | **un** correo, como **respuesta en el hilo** de tu envío original |
| Destinatario | **solo vos**, no quienes fueran en copia |
| `sanic_fechaacuseiniciado` | con fecha |
| `sanic_fechaacuseenviado` | con fecha |
| Bitácora | `Acuse iniciado` y `Acuse enviado`, **origen MPPP-COM** |
| Estado de la Solicitud | sigue en **En proceso** (un acuse de En proceso no cambia el estado) |

**Esperá 5 minutos y mirá otra vez**: no tiene que llegar un segundo acuse. `MPPP-VIG` corre cada
10 minutos y **no** debe reenviar nada.

**Automatizable.** Manual (`Mail.Send` real; `corredor.py` lo marca N-A — punto de extensión
pendiente: un cliente Graph que lea la bandeja del remitente de prueba).

---

### M-02 · El acuse de una Rechazada cierra la Solicitud

**Qué prueba.** DD-09: acuse de una **Rechazada** → **Cerrada** + `sanic_fechacerrada`.

**Necesita.** Una Solicitud Rechazada (de S-01, S-05 o C-08); acceso a la bandeja del REMITENTE.

**Cómo.** Usá cualquier Solicitud que haya quedado **Rechazada** (S-01, S-05, C-08…).

**Esperado.**

| Qué | Valor |
|---|---|
| Llega el acuse | sí |
| Estado después | **Cerrada** |
| `sanic_fechacerrada` | con fecha |
| Bitácora | `Acuse enviado` y después `Cerrada` |

> La diferencia con M-01 es todo el punto: una Rechazada **termina acá** (no hay nada que digitar),
> una En proceso sigue viva hasta que se digiten y aprueben sus filas.

**Automatizable.** Manual (N-A en `corredor.py`: falta `Mail.Send`; el cierre lo dispara
`MPPP-COM` al ver que el acuse llegó de verdad).

---

### M-03 · Copia y remitente

**Qué prueba.** DF-03: solo al remitente, nunca a los de copia.

**Necesita.** El REMITENTE con una segunda dirección propia para poner en copia; acceso a ambas
bandejas.

**Cómo.** Correo nuevo con `E01-feliz-formato11.xlsx`, pero poné **otra dirección tuya en CC**.

**Esperado.** El acuse llega **solo** a la dirección del **De**. A la de CC **no le llega nada**.

**FALLA si** le llega a la copia: se le estaría verificando la autorización a uno y respondiéndole
a otro.

**Automatizable.** Manual (N-A en `corredor.py`: falta `Mail.Send`, y hace falta leer una bandeja
real para confirmar que a la copia no le llegó nada).

---

### M-04 · La respuesta final, cuando todas las filas se resuelven

**Qué prueba.** El ciclo completo hasta **Procesada** → respuesta final → **Cerrada**.

**Necesita.** La Solicitud de C-01; sesión `ejecutivo` para marcar Digitada; sesión `supervisor`
para Aprobar; acceso a la bandeja del REMITENTE.

**Depende de**: `05-app-y-comandos.md`, porque hay que digitar y aprobar desde la app.

**Cómo.**

1. Tomá la Solicitud de **C-01** (3 filas Validadas, En proceso).
2. En la app, vista **Por digitar**: seleccioná las 3 y **Digitada**.
3. Entrá como Supervisor, vista **Por aprobar**: seleccioná las 3 y **Aprobar**.

**Esperado.**

| Momento | Qué pasa |
|---|---|
| Al aprobar la última fila | La Solicitud pasa a **Procesada** |
| Enseguida | `MPPP-COM` dispara `MPPP-ENV` |
| Llega | la **respuesta final**, en el mismo hilo, solo al remitente |
| Estado final | **Cerrada** + `sanic_fechacerrada` |
| Bitácora | `Procesada`, `Respuesta final iniciada`, `Respuesta final enviada`, `Cerrada` |

**Leé la respuesta final completa.** Tiene que decir qué pasó con cada fila y llevar el texto
obligatorio de DF-08 ("no responda a este mensaje…").

**Automatizable.** Manual (depende de `05` -interfaz de navegador- y de `Mail.Send` real para la
respuesta final).

---

### M-05 · Nunca dos veces lo mismo

**Qué prueba.** D-21 y el paso 1 de `MPPP-ENV`: si el acuse ya está **iniciado**, no se envía otra vez.

**Necesita.** La Solicitud de C-01 con acuse ya enviado; permiso para editar la Solicitud (rol de
administración, o API con privilegio de escritura).

**Cómo.** Forzá un segundo disparo de `MPPP-COM` sobre una Solicitud que ya tiene acuse enviado:
en la app, abrí la Solicitud de **C-01** y cambiale el estado a **En proceso** (si ya está ahí,
tocá y guardá cualquier otro campo para que se dispare una actualización).

**Esperado.**

| Qué | Valor |
|---|---|
| Llega un segundo acuse | **NO** |
| `sanic_fechaacuseenviado` | **no cambia** |
| Nuevas entradas de Bitácora de acuse | ninguna |

**FALLA si** llega un segundo correo. Es el defecto más caro de todos: el cliente recibe dos acuses
contradictorios de la misma solicitud.

**Automatizable.** Manual (N-A en `corredor.py`: exige comprobar que NO llegó un segundo correo
real).

---

### M-06 · Envío iniciado sin confirmar → revisión humana

**Qué prueba.** Que una comunicación a medias **no se reenvíe** y levante bandera (D-21).

**Necesita.** Una Solicitud En proceso sin acuse; permiso para editar `sanic_fechaacuseiniciado`
(rol de administración o API con privilegio de escritura).

**Cómo.** Simulá el estado a medias: en la app, sobre una Solicitud **En proceso** que todavía no
tenga acuse, poné `sanic_fechaacuseiniciado` con la hora de hace 40 minutos y dejá
`sanic_fechaacuseenviado` **vacío**. Esperá a que corra `MPPP-VIG` (hasta 10 minutos).

**Esperado.**

| Qué | Valor |
|---|---|
| Llega un acuse | **NO** |
| `sanic_requiererevision` | **sí** |
| `sanic_motivorevision` | *"Envio iniciado sin confirmar…"* |
| Bitácora | `Requiere revision`, origen **MPPP-VIG** |
| Dónde se ve | vista **Para revisar** del sitemap |

> Éste es el costo aceptado del diseño: ante la duda no se reenvía y lo mira una persona. Si acá
> llega un correo, la garantía de "como máximo una vez" no existe.

**Automatizable.** Manual (depende del disparo real de `MPPP-VIG` sobre `MPPP-COM`/`MPPP-ENV`, y
de `Mail.Send`).

---

### M-07 · El cuerpo lo arma el código, no el flujo

**Qué prueba.** DD-08: el contenido viene de `sanic_acusecontenido`, que escribe la Custom API.

**Necesita.** Una Solicitud con acuse ya enviado; acceso a la bandeja del REMITENTE para comparar
contra el correo real.

**Cómo.** Abrí una Solicitud con acuse y compará el campo `sanic_acusecontenido` con el correo que
te llegó:

```bash
python3 herramientas/pruebas/ver_solicitud.py MPPP-########
```

**Esperado.** El cuerpo del correo es **exactamente** el HTML de ese campo. No hay texto agregado
por el flujo.

**FALLA si** el correo trae algo que no está en el campo (una firma automática, un encabezado
puesto por Power Automate, un "Sent from…").

**Automatizable.** Manual (compara `sanic_acusecontenido` contra el correo REAL recibido; el
contenido del campo ya se verifica indirectamente en S-07/R-13).

---

### Resultados

| Caso | Solicitud | Resultado | Notas |
|---|---|---|---|
| M-01 acuse, una sola vez | | | |
| M-02 Rechazada → Cerrada | | | |
| M-03 solo al remitente | | | |
| M-04 respuesta final | | | |
| **M-05 nunca dos veces** | | | |
| M-06 a medias → revisión | | | |
| M-07 cuerpo del código | | | |

---

## 05 — La app y sus 8 comandos

Los 8 botones de la barra nunca se ejecutaron en un navegador. Este archivo es el que más
probabilidad tiene de encontrar defectos.

**Dónde**: app **MDA - MPPP - Mantenimiento PPP**.

**Si NINGÚN botón aparece**, antes de reportar caso por caso mirá esto: en el `RibbonDiffXml` la
biblioteca se referencia como `$webresource:sanic_mppp_wr_js_comandos`, **sin `.js`**. El ejemplo
real del entorno que se usó de modelo sí llevaba la extensión en el nombre. Es el sospechoso
número uno y el arreglo es de un minuto.

**Antes**: `01` y `03`. Hace falta al menos una Solicitud **En proceso** con filas Validadas
(C-01) y una **No reconocida** (C-04).

---

### A-01 · Los botones aparecen donde deben

**Qué prueba.** Los `CustomAction` en sus dos ubicaciones.

**Necesita.** Sesión `ejecutivo` (o cualquier rol con acceso a Filas/Solicitudes); la Solicitud de
C-01 con filas Validadas.

**Cómo.** Abrí la vista **Por digitar** (grilla de Filas) y seleccioná una fila.

**Esperado.** En la barra aparecen **5 botones propios** con su ícono:

`Digitada` · `Rechazada en AS400` · `Anular` · `Aprobar` · `Devolver`

Y en **Solicitudes**, seleccionando una fila: `Atendido` · `Descartar` · `Revisado`.

**También verificá en la subgrilla**: abrí una Solicitud y mirá su subgrilla de Filas. Los 5 de
Fila tienen que estar ahí también.

**Automatizable.** GUI (sesión `ejecutivo`; ya cubierto en paralelo por `pruebas/gui/`, ver
`ribbon-fila.spec.mjs`).

---

### A-02 · Los comandos genéricos NO aparecen (12.6)

**Qué prueba.** Los `HideCustomAction` en las 10 tablas.

**Necesita.** Sesión con acceso a todas las tablas del sitemap (un usuario con los 4 roles, o
recorrer con cada sesión).

**Cómo.** En **cada** entrada del sitemap, seleccioná una fila y mirá la barra (incluido el menú
`…`).

**Esperado.** En **ninguna** tabla aparecen: **Eliminar**, **Asignar**, **Compartir**, **Flujo**,
**Enviar vínculo**, **Combinar**. Y **Nuevo** no aparece en Solicitud, Fila, ResultadoRegla ni
Bitácora (sí en los catálogos, donde el alta es de una persona).

| Tabla | Sin los 6 genéricos | Sin "Nuevo" |
|---|---|---|
| Solicitud | | **sí** |
| Fila | | **sí** |
| ResultadoRegla | | **sí** |
| Bitácora | | **sí** |
| Cliente, Plan, Autorizado, Autorización, Parámetro, Regla | | (Nuevo **sí** aparece) |

**Automatizable.** GUI.

---

### A-03 · Digitada, sobre varias filas a la vez

**Qué prueba.** `Sanic.Mppp.Comandos.digitada` y la acción masiva (DA-02).

**Necesita.** Sesión `ejecutivo`; las 3 filas Validadas de C-01.

**Cómo.** Vista **Por digitar**, seleccioná **las 3 filas** de C-01, botón **Digitada**.

**Esperado.**

| Paso | Qué pasa |
|---|---|
| Al apretar | Diálogo de confirmación que **dice la cantidad**: *"¿Aplicar 'Digitada' a 3 fila(s) seleccionada(s)?"* |
| Al confirmar | Mensaje: *"3 de 3 fila(s) actualizada(s)."* |
| Las filas | pasan a **Digitada** |
| La grilla | se refresca sola y las filas **desaparecen** de Por digitar |
| Bitácora | `Fila digitada`, origen **App** |

**FALLA si**: el diálogo no dice la cantidad, hay que refrescar a mano, o el texto sale en inglés
teniendo el usuario español.

**Automatizable.** GUI (sesión `ejecutivo`).

---

### A-04 · Cancelar no cambia nada

**Qué prueba.** Que el diálogo de confirmación realmente frene.

**Necesita.** Sesión `ejecutivo`; filas seleccionables.

**Cómo.** Seleccioná filas, apretá **Digitada** y dale **Cancelar**.

**Esperado.** **Nada** cambia. Ninguna fila cambia de estado, no hay entradas nuevas en Bitácora.

**Automatizable.** GUI (sesión `ejecutivo`).

---

### A-05 · Sin selección

**Qué prueba.** El `EnableRule` `Mscrm.SelectionCountAtLeastOne`.

**Necesita.** Sesión `ejecutivo` (o cualquier rol con acceso a la vista).

**Cómo.** Sin seleccionar ninguna fila, mirá la barra.

**Esperado.** Los botones propios están **deshabilitados** (grises). Si se pudiera apretar, el
mensaje sería *"No hay ninguna fila seleccionada."*

**Automatizable.** GUI.

---

### A-06 · Aprobar, como Supervisor

**Qué prueba.** `Sanic.Mppp.Comandos.aprobar`.

**Necesita.** Sesión `supervisor`; las filas Digitadas que dejó A-03.

**Cómo.** Entrá con un usuario con rol **SR - MPPP - Supervisor**. Vista **Por aprobar**,
seleccioná las filas de A-03, botón **Aprobar**.

**Esperado.** Las filas pasan a **Aprobada**; cuando se resuelven **todas** las de la Solicitud,
pasa a **Procesada** y arranca la respuesta final (caso M-04).

**Automatizable.** API (proxy de servidor: `comportamiento_roles.py --con-escritura`, corrido por
`corredor.py` con la bandera `--con-comportamiento-roles`) + GUI (sesión `supervisor`) para
confirmar la experiencia real en pantalla.

---

### A-07 · El plugin manda, no el botón (DA-07)

**Qué prueba.** Lo más importante de la seguridad: la regla real está en el servidor.

**Necesita.** Sesión `supervisor`; una fila que el mismo supervisor haya digitado (para probar la
segregación de funciones).

**Cómo.** Con el Supervisor, intentá **Aprobar** una fila que **él mismo digitó** (segregación de
funciones), o **Digitada** sobre una fila que ya está **Aprobada**.

**Esperado.**

| Qué | Valor |
|---|---|
| La operación | **falla** |
| El mensaje | *"N de M fila(s) actualizada(s). X no se pudieron actualizar:"* seguido del motivo que devuelve el plugin |
| Las otras filas de la tanda | **sí** se aplican |
| Ninguna fila | queda a medias |

**FALLA si** la transición prohibida se aplica igual: el botón estaría decidiendo, y la regla se
saltearía llamando al Web API.

> Que **unas filas se apliquen y otras no** es el comportamiento pedido (`05` §5), no un defecto.

**Automatizable.** API (proxy de servidor: `comportamiento_roles.py --con-escritura`, corrido por
`corredor.py` con `--con-comportamiento-roles`) — **este es el caso de seguridad más importante
del archivo y ya tiene automatización de servidor**; GUI opcional para confirmar el mensaje que ve
el usuario.

---

### A-08 · Rechazada en AS400 — el diálogo de motivo

**Qué prueba.** El corazón de DA-03 corregida: `navigateTo` a `entityrecord` en modo creación.

**Necesita.** Sesión `ejecutivo` o `supervisor`; 2 filas seleccionadas.

**Cómo.** Vista **Por digitar** o **Por aprobar**, seleccioná 2 filas, botón **Rechazada en AS400**.

**Esperado.**

| Paso | Qué pasa |
|---|---|
| Al apretar | Se abre un **diálogo centrado** con el título *"Rechazada en AS400 — 2 fila(s)"* |
| El diálogo | muestra **un solo campo**: **Motivo**, obligatorio |
| Si guardás vacío | la plataforma no deja: el campo es obligatorio |
| Al guardar con texto | el diálogo cierra y se aplican **las 2 filas** |
| Las filas | pasan a **Rechazada en AS400** |
| `sanic_mensaje` de cada fila | **el texto que escribiste** |
| Registro de motivo | se **borra** después de leerse (es transporte, no dato) |

**FALLA si**: el diálogo no abre, abre en blanco, o se aplica el cambio **sin** el motivo.

> Verificá que no quede basura: en la tabla **Motivo de acción** no debería quedar ningún registro
> después de usar el comando.

**Automatizable.** GUI.

---

### A-09 · Cancelar el diálogo de motivo

**Qué prueba.** Que cerrar sin guardar **no cambie nada**.

**Necesita.** Sesión `ejecutivo`; filas seleccionadas.

**Cómo.** Apretá **Anular** sobre filas seleccionadas y cerrá el diálogo con la **X** (sin guardar).

**Esperado.** **Ninguna** fila cambia de estado. No hay registro de motivo colgado.

**Automatizable.** GUI.

---

### A-10 · Anular y Devolver

**Qué prueba.** Los otros dos comandos con motivo.

**Necesita.** Sesión `ejecutivo` (Anular) y sesión `supervisor` (Devolver); filas en
Validada/Digitada.

**Cómo.**
- **Anular**: sobre filas en Validada o Digitada.
- **Devolver**: como **Supervisor**, sobre filas en **Digitada**.

**Esperado.**

| Comando | Estado destino | Dónde aparece después |
|---|---|---|
| Anular | **Anulada** | en ningún lado del trabajo diario |
| Devolver | **Validada** con mensaje | vista **Devueltas** del ejecutivo |

> **Devolver es el más sutil**: la fila vuelve a **Validada**, igual que una recién validada. Lo
> único que la distingue en la vista **Devueltas** es que **tiene mensaje**. Si aparece también en
> **Por digitar**, las dos vistas están mal filtradas (y está bien que aparezca en las dos si así
> lo dice el diseño — comparalo con `05` §2).

**Automatizable.** API (proxy de servidor: `comportamiento_roles.py --con-escritura`, corrido por
`corredor.py` con `--con-comportamiento-roles`) + GUI para confirmar las vistas Devueltas/estado en
pantalla.

---

### A-11 · Atendido y Descartar, sobre Solicitudes

**Qué prueba.** Los dos comandos de la grilla de Solicitudes.

**Necesita.** Sesión `ejecutivo`; las Solicitudes de C-02 y C-04 (No reconocida / No es correo
nuevo).

**Cómo.** Vista **Por clasificar** (Solicitudes en No reconocida o No es correo nuevo, de C-02 y
C-04). Seleccioná una y probá **Atendido**; con otra, **Descartar**.

**Esperado.**

| Comando | Estado destino |
|---|---|
| Atendido | **Cerrada** |
| Descartar | **Descartada** |

En los dos: confirmación con la cantidad, la Solicitud sale de **Por clasificar**, y **no se le
envía nada al cliente**.

**Automatizable.** GUI (no automatizado esta vuelta: no hay herramienta de orquestación existente
para el PATCH de `sanic_estadoprocesamiento`; punto de extensión directo).

---

### A-12 · Revisado

**Qué prueba.** El octavo comando: apaga la marca y deja la nota.

**Necesita.** Sesión `ejecutivo` o `supervisor`; la Solicitud de M-06 con `requiererevision = sí`.

**Cómo.** Sobre una Solicitud con `sanic_requiererevision = sí` (la de M-06), vista **Para
revisar**, botón **Revisado**. Escribí una nota.

**Esperado.**

| Qué | Valor |
|---|---|
| Diálogo | igual que A-08, pide una nota |
| `sanic_requiererevision` | pasa a **no** |
| La nota | queda en `sanic_motivorevision` y el plugin la pasa a la **Bitácora** |
| La Solicitud | sale de **Para revisar** |

**Automatizable.** GUI (no automatizado esta vuelta, mismo motivo que A-11).

---

### A-13 · Multiidioma de los botones

**Qué prueba.** Que cada etiqueta tenga su texto en 1033 y 3082 (convención del proyecto).

**Necesita.** Un usuario con el idioma configurado en inglés (Configuración personal → Idiomas);
cualquiera de las 4 sesiones.

**Cómo.** Si tenés un usuario con el idioma en inglés, entrá con él. Si no, cambiá el idioma en
**Configuración personal → Idiomas**.

**Esperado.**

| Español (3082) | Inglés (1033) |
|---|---|
| Digitada | Mark as entered |
| Aprobar | Approve |
| Rechazada en AS400 | Rejected in AS400 |
| Anular | Void |
| Devolver | Send back |
| Atendido | Handled |
| Descartar | Discard |
| Revisado | Reviewed |

Y los **mensajes del JavaScript** (confirmación, resultado) también cambian de idioma.

**Automatizable.** GUI.

---

### Resultados

| Caso | Resultado | Notas |
|---|---|---|
| A-01 los botones aparecen | | |
| A-02 genéricos ocultos | | |
| A-03 Digitada masiva | | |
| A-04 cancelar | | |
| A-05 sin selección | | |
| A-06 Aprobar | | |
| **A-07 el plugin manda** | | |
| A-08 diálogo de motivo | | |
| A-09 cancelar diálogo | | |
| A-10 Anular y Devolver | | |
| A-11 Atendido y Descartar | | |
| A-12 Revisado | | |
| A-13 multiidioma | | |

---

## 06 — Seguridad, sitemap, vistas y formularios

Cubre lo que el usuario ve y lo que **no** tiene que poder ver. Los casos de seguridad son los más
importantes de todo el plan: acá hay números de identificación y de cuenta de personas reales.

**Antes**: `00-preparacion.md` y al menos una Solicitud con filas (C-01).

**Vas a necesitar un usuario por rol.** Si no tenés cuatro cuentas, asigná y quitá roles de una
misma cuenta, de a uno por vez (y acordate de dejarla como estaba).

| Rol | Para qué |
|---|---|
| `SR - MPPP - Ejecutivo` | digita |
| `SR - MPPP - Supervisor` | aprueba |
| `SR - MPPP - Administrador de planes` | catálogos |
| `SR - MPPP - Administrador tecnico` | parámetros y reglas |

---

### G-00 · Los privilegios efectivos, antes de tocar la app

**Qué prueba.** Que cada usuario de prueba tenga **exactamente** las celdas de la matriz de `04`,
ni una menos ni una de más.

**Por qué va primero.** En Dataverse los privilegios se **suman**: los de los roles directos más los
de **cada equipo** al que el usuario pertenece. Un usuario reciclado de otro proyecto llega con los
roles de ese proyecto. Entonces, si en la app el usuario puede hacer algo que no debería, mirando la
pantalla **no se sabe quién se lo permitió**. Esto lo resuelve antes de empezar, y con nombre y
apellido del rol culpable.

**Necesita.** Credenciales de lectura en Dataverse; los usuarios de prueba ya creados (User1..User4).

**Cómo.**

```bash
python3 herramientas/pruebas/verificar_accesos.py User1 User2 User3
```

**Esperado.** `Coincide con la matriz, celda por celda.` para los tres. Sale con 0.

**Qué hacer con cada tipo de hallazgo.**

| Tipo | Qué significa | Qué hacer |
|---|---|---|
| `FALTA` | La matriz le da el privilegio y no lo tiene | El rol está mal construido: revisá su playbook y reimportalo |
| `ALCANCE` | Lo tiene, pero con otra profundidad | Ídem. Un `U` donde va `O` hace que el usuario solo vea sus propios registros |
| `SOBRA` | Lo tiene y la matriz no se lo da. **El detalle nombra el rol que se lo trajo** | Si el rol es de otro proyecto, sacalo del equipo antes de probar, o la prueba no vale |
| `COLUMNAS` | No alcanza el perfil de seguridad de columna | Ver G-05: sin eso, el ejecutivo no ve lo que tiene que digitar |

**Correr esto también después de cada cambio de roles.** Es lo único que detecta que un
`ImportSolution` pisó un privilegio.

**Automatizable.** API (`verificar_accesos.py`).

---

### G-00b · El comportamiento de cada rol, contra el entorno

**Qué prueba.** La otra mitad de la pregunta. `verificar_accesos.py` responde *"¿qué privilegios
TIENE cada uno?"*; esta responde *"¿qué puede HACER?"*. Hacen falta las dos: un privilegio correcto
con un plugin que falla da un usuario bloqueado, y un plugin correcto con un privilegio de más da un
agujero.

**Por qué no alcanza con las pruebas de C#.** **Corregido: decía "las 800 pruebas"; el número
crece con cada commit y no es lo que importa** (`diseno/PENDIENTES.md` D-57 lo registra en 981 al
2026-09-26; no se volvió a correr `dotnet test` para esta corrección porque no es una cifra que
haga falta clavar). Lo que importa es el motivo: usan un doble de
`IOrganizationService` que **no modela privilegios**: devuelve lo que se le pide, venga de quien
venga. Por eso ninguna vio el defecto del 2026-09-23 — los plugins leían la tabla Parametro con la
identidad de quien llamaba, y ningún rol de negocio tiene lectura ahí, así que **ninguna persona
podía transicionar una fila**. Eso solo se ve contra la plataforma y con un usuario de verdad.

**Necesita.** Credenciales de servicio con permiso para suplantar (`MSCRMCallerID`) a cada usuario
de prueba; los usuarios User1..User4 ya creados.

**Cómo.**

```bash
python3 herramientas/pruebas/comportamiento_roles.py                   # 9 casos, NO escribe nada
python3 herramientas/pruebas/comportamiento_roles.py --con-escritura   # 16 casos, incluye el recorrido
```

Suplanta a cada usuario con la cabecera `MSCRMCallerID`, así que el plugin ve exactamente el mismo
contexto que si la persona hubiera entrado a la app. Sin `--con-escritura` solo corre los casos de
rechazo, donde el servidor frena antes de tocar el registro: **no deja rastro**, se puede correr
cuando sea. Con la bandera agrega el recorrido completo de dos filas de prueba (digitar → intentar
aprobar uno mismo → aprobar el supervisor; y digitar → devolver sin motivo → devolver con motivo).

**Esperado.** `0 fallas · 0 sin probar` en las dos modalidades.

Un caso `— sin probar` **no es un caso aprobado**: es un caso que no se pudo correr por falta de
datos (por ejemplo, ninguna Fila en Validada). Sale contado aparte justamente para que no se lea
como verde.

**Qué NO reemplaza.** Todo lo que sigue de G-01 en adelante: lo que la persona VE. La barra de
comandos, los íconos, el recorte del sitemap, las notificaciones y las columnas enmascaradas en
pantalla no se ven desde acá. Esto prueba las reglas; los casos de abajo prueban la experiencia.

**Automatizable.** API (`comportamiento_roles.py`; con `--con-escritura` actúa sobre CUALQUIER
Fila del entorno en Validada/Digitada, no solo las propias — coordiná con quien esté probando en
`pruebas/gui/` en paralelo antes de usar esa bandera).

---

### G-01 · El sitemap: 4 áreas y 15 entradas

**Qué prueba.** El sitemap de 12.7 y que cada entrada abra **su** vista.

**Necesita.** Un usuario con **los 4 roles** a la vez (o recorrer el menú 4 veces, una por sesión:
`ejecutivo`, `supervisor`, `admin_planes`, `admin_tecnico`), para ver el sitemap completo sin
recortar.

**Cómo.** Con ese usuario, recorré el menú entero.

**Esperado.** **Corregido: eran 13 entradas, hoy son 15** (verificado en vivo contra Dev el
2026-09-27 con `app.py --solo-verificar`: `"4 areas y 15 entradas"`). Se agregaron **Para atención
manual** (Trabajo, fase 2 enganchada al sitemap recién el 2026-09-27) y **Autorizaciones -
Clientes** (Catálogos, DN-01). Además cambiaron dos títulos: **"Autorizados"** ahora es **"Correos
Autorizados"**, y **"Autorizaciones"** ahora es **"Autorizaciones - Planes"** (para distinguirla de
la nueva "Autorizaciones - Clientes").

| Área | Entradas |
|---|---|
| **Trabajo** | Por digitar · Devueltas · Por aprobar · **Para atención manual** · Por clasificar · Para revisar |
| **Consulta** | Solicitudes · Filas |
| **Catálogos** | Clientes · Planes · **Correos Autorizados** · **Autorizaciones - Planes** · **Autorizaciones - Clientes** |
| **Configuración** | Parámetros · Reglas |

**Lo que más hay que mirar**: las entradas que apuntan a **una vista específica** (no a la vista
por defecto de la tabla). Este patrón **nunca se pudo verificar** contra ningún sitemap del entorno
y quedó anotado como pendiente.

| Entrada | Tiene que abrir |
|---|---|
| Por digitar | **Mis clientes — por digitar** (no "Todos") |
| Devueltas | **Mis clientes — devueltas** |
| Por aprobar | **Por aprobar** |
| **Para atención manual** | vista de Filas con `sanic_requiereatencionmanual` = sí |
| Por clasificar | **Por clasificar** |
| Para revisar | **Para revisar** |
| Solicitudes | **Solicitudes** |
| Filas | la vista de Filas que corresponda |
| **Autorizaciones - Clientes** | **Autorizaciones por cliente activas** (por defecto de AutorizacionCliente) |

**FALLA si** alguna abre la vista por defecto de la tabla en vez de la suya: las cuatro entradas
sobre Fila mostrarían lo mismo y la navegación del diseño se cae.

**Automatizable.** GUI (sesión con los 4 roles, o las 4 sesiones por separado).

---

### G-02 · Los íconos del sitemap

**Qué prueba.** Los 22 SVG de 12.1.

**Necesita.** Sesión con acceso al sitemap (cualquiera de las 4); poder cambiar el tema de la app
entre claro y oscuro.

**Cómo.** Mirá el menú y la app.

**Esperado.** Cada entrada tiene **su** ícono (no el genérico), y la app tiene el suyo en la barra.
Los íconos se ven bien en **tema claro y oscuro** (usan `currentColor`). Siguen siendo 22: las dos
entradas nuevas de sitemap (`Para atención manual`, `Autorizaciones - Clientes`) **reúsan** íconos
existentes (el de "Para revisar" y el de "Autorizaciones - Planes"), no se sumó ninguno.

**Automatizable.** GUI.

---

### G-03 · Las 20 vistas existen y filtran

**Qué prueba.** 12.2.

**Necesita.** Sesión con acceso a cada tabla (Ejecutivo/Supervisor para Fila y Solicitud,
`admin_planes` para los catálogos, `admin_tecnico` para Parámetros/Reglas); datos previos de C-01,
C-04, R-13 y compañía para que las vistas no aparezcan vacías.

**Cómo.** Recorré las vistas desde el selector de cada tabla. **Corregido: eran 18, hoy son 20**
(`diseno/05-app-model-driven.md` §3: se agregaron "Correos autorizados sobre este cliente",
"Clientes que puede modificar" y "Autorizaciones por cliente activas", las tres sobre
`AutorizacionCliente`, DN-01/2026-09-27).

**Esperado.** Cada una trae lo que dice su nombre. Las más fáciles de verificar:

| Vista | Tiene que mostrar | NO tiene que mostrar |
|---|---|---|
| Mis clientes — por digitar | filas **Validada** de **tus** clientes | filas de otros clientes, ni Digitadas |
| Todos — por digitar | **todas** las Validada | |
| Por aprobar | solo **Digitada** | |
| Mis clientes — devueltas | Validada **con mensaje** | las validadas limpias |
| Terminadas | estados terminales | Validada ni Digitada |
| Por clasificar | **No reconocida** y **No es correo nuevo** | Ingresada, En proceso |
| Para revisar | `requiererevision = sí` | las demás |

**Automatizable.** GUI (recorrer el selector de vistas de cada tabla).

---

### G-04 · La cartera del ejecutivo (RF-19, D-13)

**Qué prueba.** Que **Mis clientes — por digitar** filtre por el propietario del Cliente.

**Necesita.** Permiso para cambiar el propietario del Cliente `PR - Pruebas MPPP SA` (un rol de
administración, no uno de los 4 de negocio: ninguno de ellos puede reasignar propietario); sesión
`ejecutivo` para el resto del caso.

**Cómo.**
1. Poné como propietario del Cliente `PR - Pruebas MPPP SA` a **otro** usuario.
2. Entrá como **Ejecutivo** (vos) y abrí **Por digitar**.

**Esperado.** Las filas de ese cliente **no aparecen** en *Mis clientes*, pero **sí** en
*Todos — por digitar*.

> Es comodidad, no seguridad: el ejecutivo **puede** ver todo si cambia de vista. Lo que prueba es
> que la vista por defecto le muestre su cartera.

**Automatizable.** GUI (sesión `ejecutivo`); el cambio de propietario es administrativo, no pasa
por la app de negocio.

---

### G-05 · Perfil de seguridad de columna

**Qué prueba.** 12 / P-06: identificación y cuenta son columnas protegidas.

**Necesita.** Sesión `ejecutivo` o `supervisor` (tienen que VER el valor) y sesión `admin_planes`
(NO tiene que verlo); una Fila con datos reales-ficticios (la de C-01 alcanza).

**Cómo.** Entrá con el **Administrador de planes** (que NO está en el perfil por rol, ver más
abajo) y abrí una Fila.

**Esperado.**

| Columna | Ejecutivo y Supervisor | Administrador de planes / técnico |
|---|---|---|
| `sanic_numeroidentificacion` | **ve el valor** | **ve `*******`** o el campo bloqueado |
| `sanic_numerocuenta` | **ve el valor** | **ve `*******`** |

**FALLA si** el Administrador de planes ve el número de cuenta. Es un dato bancario de una persona
real.

> **Corregido el 2026-09-27 — la advertencia de "bloqueante" de acá abajo YA NO ES CIERTA, y se
> verificó de nuevo antes de tocar este párrafo.** Cuando se escribió (2026-09-23), `CSP - MPPP -
> Datos sensibles` no tenía ni un usuario ni un equipo asignado, así que el caso daba un falso
> positivo (nadie veía la cuenta, ni siquiera el Ejecutivo). **Hoy no es así**: una consulta de
> solo lectura contra `fieldsecurityprofiles` en Dev (2026-09-27) muestra el equipo **`MPPP -
> Ejecutivos`** asociado al perfil, y además **User 1** (`User1@55xljh.onmicrosoft.com`, la sesión
> `ejecutivo`) y **User 2** (`User2@55xljh.onmicrosoft.com`, la sesión `supervisor`) asociados
> **directamente** al perfil como usuarios. Es decir: las dos cuentas de prueba que este mismo caso
> usa para "sí tiene que ver el valor" **ya están cubiertas**, y el caso debería poder pasar de
> verdad, no por el problema teórico anterior. No se verificó si la vía es exactamente la del
> diseño (grupo de Entra + equipo de grupo, D-8) o una asignación directa hecha a mano para
> destrabar las pruebas — para eso hace falta mirar Entra ID, que está fuera del alcance de una
> consulta de Dataverse. Si el caso FALLA igual (el ejecutivo no ve la cuenta), no asumas que es el
> mismo problema de antes: es un defecto nuevo y hay que investigarlo de cero.
>
> El perfil y sus permisos en sí **viajan** en la solución (6.1) y están bien: `IsSecured = true`
> en las dos columnas, y el perfil trae exactamente las dos (`canread = 4`, `cancreate = 0`,
> `canupdate = 0`). Lo que no viaja con la solución es la asociación de miembros (inventario 13.7).

> Probalo también **en la vista**, no solo en el formulario: una columna protegida tiene que
> ocultarse en los dos lados.

**Automatizable.** GUI (sesiones `ejecutivo`, `supervisor` y `admin_planes`); la pertenencia al
perfil se puede confirmar de antemano por API de solo lectura (`fieldsecurityprofiles` →
`systemuserprofiles_association` / `teamprofiles_association`), como se hizo para esta corrección.

---

### G-06 · Lo que cada rol NO puede

**Qué prueba.** La matriz de privilegios (`04`).

**Necesita.** Las 4 sesiones: `ejecutivo`, `supervisor`, `admin_planes`, `admin_tecnico`.

**Cómo.** Con cada rol, intentá lo que no le toca.

**Esperado.**

| Rol | Tiene que poder | NO tiene que poder |
|---|---|---|
| Ejecutivo | ver y digitar Filas; ver Solicitudes | **aprobar**; editar Planes, Clientes, Parámetros, Reglas |
| Supervisor | aprobar y devolver | crear Solicitudes a mano; editar catálogos |
| Administrador de planes | crear y editar Clientes, Planes, Autorizados, Autorizaciones **y Autorizaciones - Clientes** (`AutorizacionCliente`, DN-01) | ver Solicitudes ni Filas |
| Administrador técnico | editar Parámetros y Reglas | ver Filas ni Solicitudes |

**El más importante**: el **Administrador de planes no ve Solicitudes ni Filas**. Si las ve, está
viendo datos bancarios que no le corresponden.

**Automatizable.** API (proxy: `verificar_accesos.py`, corredor.py caso G-06 — verifica los
privilegios subyacentes); GUI con las 4 sesiones para confirmar la experiencia real (ver G-00b:
un privilegio correcto no garantiza que la pantalla se comporte bien).

---

### G-07 · Cada rol entra donde debe

**Qué prueba.** Que el sitemap se recorte solo, por privilegios.

**Necesita.** Las 4 sesiones, una por rol.

**Cómo.** Entrá con cada rol **por separado** y mirá qué entradas del menú aparecen.

**Esperado.**

| Rol | Ve |
|---|---|
| Ejecutivo | Trabajo (Por digitar, Devueltas, Para atención manual, Por clasificar, Para revisar), Consulta; Catálogos **solo lectura** |
| Supervisor | Trabajo (Por aprobar, Para revisar), Consulta |
| Administrador de planes | **solo** Catálogos (incluida Autorizaciones - Clientes) |
| Administrador técnico | **solo** Configuración |

**Automatizable.** GUI (las 4 sesiones, una por vez); los privilegios subyacentes ya los cubre
G-06/`verificar_accesos.py`.

---

### G-08 · Los 10 formularios

**Qué prueba.** 12.3 + 12.4.

**Necesita.** Sesión con acceso a cada tabla (Ejecutivo/Supervisor para Solicitud y Fila,
`admin_planes` para Cliente/Plan/Autorizado/Autorización/Autorización por cliente,
`admin_tecnico` para Parámetro/Regla); al menos un registro de cada tabla (los que ya dejaron C-01
y P-03).

**Cómo.** Abrí un registro de cada tabla con interfaz.

**Esperado.** **Corregido: eran 9, hoy son 10** (`diseno/05-app-model-driven.md` §4 y §7: se agregó
el formulario **Autorización por cliente**, sobre `AutorizacionCliente`, el 2026-09-27, DN-01). Los
10 abren sin error y muestran sus campos agrupados:

Solicitud · Fila · Cliente · Plan · Autorizado · Autorización (AutorizacionPlan) · **Autorización
por cliente (AutorizacionCliente)** · Parámetro · Regla · **Motivo de acción** (el del diálogo).

**Mirá especialmente**:

- **Solicitud**: sus subgrillas de Filas, ResultadoRegla y Bitácora traen datos.
- **Autorización**: el campo de **archivo** `sanic_documentofirmado` deja subir y descargar un PDF.
- **Autorización por cliente**: **no** tiene sección de evidencia (esa tabla no tiene
  `sanic_documentofirmado` ni `sanic_fechadocumento`, a diferencia de Autorización).
- **Motivo de acción**: **un solo campo**, Motivo, obligatorio.

**Automatizable.** GUI (abrir un registro de cada tabla, con la sesión que corresponda).

---

### G-09 · La vista "Autorizaciones sin evidencia"

**Qué prueba.** D-42: la evidencia **no** condiciona la vigencia; es una lista de tareas del
administrador.

**Necesita.** Sesión `admin_planes`; las autorizaciones que dejó P-03 (ninguna tiene evidencia
cargada, a propósito).

**Cómo.** Como Administrador de planes, abrí **Autorizaciones - Planes** y su vista *Sin evidencia*.

**Esperado.** Muestra las autorizaciones **sin** documento firmado — incluidas las que
`preparar_datos.py` creó.

**Y lo más importante**: esas autorizaciones **igual funcionan**. El caso C-01 pasó con
autorizaciones sin evidencia. Si en algún lado una autorización sin documento hace fallar
`AUTORIZACION_CORREO_PLAN`, es **FALLA** contra D-42.

**Automatizable.** GUI (sesión `admin_planes`); que una autorización sin evidencia SÍ autorice ya
lo demuestra cualquier caso de `01`/`02`/`03` corrido por `corredor.py` (todas sus autorizaciones
también nacen sin evidencia).

---

### G-10 · La campana de notificaciones (DA-08)

**Qué prueba.** Los avisos dentro de la app.

**Necesita.** Sesión `ejecutivo`; haber corrido C-02 y C-04 antes (o cualquier caso que deje un
correo en Por clasificar).

**Cómo.** Después de correr C-02 y C-04, entrá como **Ejecutivo** y mirá la campana.

**Esperado.** Hay avisos de que un correo fue a **Por clasificar**. Al tocarlos, **abren** la vista
o la Solicitud correspondiente.

| Cuándo | A quién |
|---|---|
| Un correo va a Por clasificar | a todos los ejecutivos |
| Una solicitud deja filas por digitar | al ejecutivo del cliente |
| Una solicitud pasa a tener filas por aprobar | a los supervisores |
| Le devuelven filas | al ejecutivo que digitó |
| Una solicitud queda para revisar | a ejecutivos y supervisores |

**Una por solicitud y por tanda, nunca una por fila.** Si aparecen 25 avisos por una plantilla de
25 filas, es FALLA.

**Automatizable.** GUI (sesión `ejecutivo`).

---

### G-11 · Multiidioma de la app

**Qué prueba.** La convención: etiquetas en 1033 y en el idioma del cliente.

**Necesita.** Un usuario con el idioma cambiado a inglés (Configuración personal → Idiomas);
cualquiera de las 4 sesiones alcanza.

**Cómo.** Cambiá el idioma del usuario a inglés y recorré el sitemap y un par de formularios.

**Esperado.** Las etiquetas del sitemap, las vistas y las columnas cambian de idioma. Ninguna
queda con el texto del otro idioma ni con el nombre lógico crudo.

**Automatizable.** GUI.

---

### G-12 · La app valida limpia

**Qué prueba.** Que la app no tenga componentes faltantes.

**Necesita.** Credenciales de lectura en Dataverse (las que ya usan las herramientas del proyecto).
Ningún rol de la app.

**Cómo.**

```bash
python3 herramientas/construir/app.py playbooks/app/mantenimientoppp.md --solo-verificar
```

**Esperado.** **Corregido: decía "11 tablas", hoy son 12** (verificado en vivo el 2026-09-27, este
mismo comando: `"4 areas y 15 entradas, 12 tablas, 4 roles ... ValidateApp sin errores"` —
la tabla 12 es `AutorizacionCliente`, DN-01). `"estado": "ya_existia"` con `12 tablas, 4 roles ...
ValidateApp sin errores`.

**Automatizable.** API (`app.py --solo-verificar`; corredor.py caso G-12).

---

### Resultados

| Caso | Resultado | Notas |
|---|---|---|
| G-00 privilegios efectivos | | |
| G-00b comportamiento de cada rol | | |
| G-01 sitemap y sus vistas | | |
| G-02 íconos | | |
| G-03 las 20 vistas | | |
| G-04 cartera del ejecutivo | | |
| **G-05 columnas protegidas** | | |
| **G-06 lo que cada rol no puede** | | |
| G-07 sitemap por rol | | |
| G-08 los 10 formularios | | |
| G-09 sin evidencia pero válida | | |
| G-10 notificaciones | | |
| G-11 multiidioma | | |
| G-12 ValidateApp | | |

---

## 07 — MPPP-VIG, la red de seguridad

`MPPP-VIG` corre **cada 10 minutos** y recupera lo que quedó a medias. Hace lo mismo que `MPPP-REC`
y `MPPP-COM` cuando algo falló, y por eso esos dos no tienen lógica propia (DF-02).

**Estos casos son lentos**: hay que esperar a que se cumplan los umbrales y a que corra el flujo.
Dejalos corriendo y hacé otra cosa mientras.

**Umbrales** (de la tabla de Parámetros, `--parametros` del visor):

| Parámetro | Valor | Bloque |
|---|---|---|
| `vigilancia.minutos.sinvalidar` | 15 | 1 y 2 |
| `vigilancia.minutos.sinresponder` | 30 | 3 y 4 |
| `vigilancia.reintentos.maximo` | 3 | 2 |
| `clasificacion.dias.vencimiento` | 30 | 5 |

> **Atajo**: para no esperar 15 o 30 minutos reales, bajá el parámetro a `1` en la tabla de
> Parámetros, corré el caso, y **devolvelo al valor original al terminar**. Si lo hacés, anotalo en
> las notas del caso: cambia lo que estás probando.

**Cómo mirar si corrió**: en Power Automate, `Cloud Flow - MPPP - VIG`, pestaña de **ejecuciones**.
Casi todas terminan en segundos sin hacer nada — eso es lo normal y lo esperado.

---

### V-01 · Corre y no hace nada

**Qué prueba.** Que la recurrencia esté viva y que una ejecución vacía sea barata.

**Necesita.** Acceso a Power Automate (rol de Maker o administrador del entorno) para ver el
historial de ejecuciones de `MPPP-VIG`.

**Cómo.** Sin tocar nada, mirá las ejecuciones de `MPPP-VIG` de la última hora.

**Esperado.**

| Qué | Valor |
|---|---|
| Ejecuciones | una cada ~10 minutos |
| Resultado | **Succeeded** |
| Duración | segundos |
| Efectos | **ninguno**: no crea filas, no cambia estados, no manda correos |

**FALLA si** hay ejecuciones fallidas, o si alguna cambió algo sin que hubiera nada pendiente.

**Automatizable.** Manual (API de administración de flujos de Power Automate, no Dataverse Web
API; además hay que esperar el schedule real de 10 minutos).

---

### V-02 · Bloque 1 — un correo que quedó en la bandeja

**Qué prueba.** Que un correo que `MPPP-REC` no llegó a procesar se ingiera igual.

**Necesita.** Permiso para apagar/prender `Cloud Flow - MPPP - REC` (rol de Maker/administrador de
Power Automate); el REMITENTE; el adjunto `E01-feliz-formato11.xlsx`.

**Cómo.**
1. **Apagá** `Cloud Flow - MPPP - REC` (Turn off).
2. Mandá un correo nuevo con `E01-feliz-formato11.xlsx`.
3. Comprobá que **no** se creó ninguna Solicitud (REC está apagado).
4. Esperá a que pasen los 15 minutos del umbral + una corrida de VIG.
5. **Volvé a prender** `MPPP-REC` al terminar.

**Esperado.**

| Qué | Valor |
|---|---|
| Se crea la Solicitud | **sí**, la ingirió VIG |
| Bitácora `Ingresada` | origen **MPPP-VIG** (no MPPP-REC) |
| El resto | igual que C-01: En proceso, 3 filas Validadas, acuse enviado |

> Esto prueba lo que promete DF-02: si el disparador de correo se cae, **no se pierde ningún
> correo**. Es la garantía más valiosa del diseño.

**Automatizable.** Manual (exige apagar un flujo real, mandar un correo real y esperar 15+
minutos; la decisión de reingerir vive dentro del flujo `MPPP-VIG`, no en un plugin).

---

### V-03 · Bloque 2 — una Solicitud que quedó en Ingresada

**Qué prueba.** El reintento de clasificación y validación, con su contador.

**Necesita.** Permiso para editar una Solicitud (`sanic_estadoprocesamiento`,
`sanic_fechaingresada`) — rol de administración o API con privilegio de escritura.

**Cómo.** En la app, abrí una Solicitud **Cerrada** o **En proceso** y volvela a **Ingresada**
(y poné `sanic_fechaingresada` con la hora de hace 20 minutos). Esperá a que corra VIG.

**Esperado.**

| Qué | Valor |
|---|---|
| `sanic_reintentosvalidacion` | pasa de vacío a **1** |
| Bitácora | `Reintento`, origen **MPPP-VIG** |
| La Solicitud | se reclasifica y revalida, y termina en su estado final |
| Filas | **no se duplican** (las API son idempotentes) |

**FALLA si** las filas se duplican. Sería procesar dos veces la misma plantilla.

**Automatizable.** Manual (la lógica de reintento — comparar `sanic_reintentosvalidacion` contra
el máximo, incrementar, Bitácora `Reintento` — vive DENTRO del flujo `MPPP-VIG`, no en un
plugin/Custom API que un PATCH pueda ejercitar).

---

### V-04 · Bloque 2 — se acaban los reintentos

**Qué prueba.** Que después de `vigilancia.reintentos.maximo` se levante para revisión humana en
vez de reintentar para siempre.

**Necesita.** Permiso para editar `sanic_reintentosvalidacion` de una Solicitud (rol de
administración o API con privilegio de escritura).

**Cómo.** Sobre una Solicitud en **Ingresada** vieja, poné `sanic_reintentosvalidacion = 3`
(el máximo). Esperá a que corra VIG.

**Esperado.**

| Qué | Valor |
|---|---|
| **No** se reintenta | no hay `Reintento` nuevo |
| `sanic_requiererevision` | **sí** |
| `sanic_motivorevision` | *"Validacion fallida 3 veces."* |
| Dónde se ve | vista **Para revisar** |

**Automatizable.** Manual (mismo motivo que V-03: la condición de "se acabaron los reintentos" es
un If dentro de Power Automate).

---

### V-05 · Bloque 3 — una comunicación que no salió

**Qué prueba.** Que una Solicitud En proceso sin acuse se recupere.

**Necesita.** Permiso para apagar/prender `Cloud Flow - MPPP - COM`; el REMITENTE; el adjunto
`E01-feliz-formato11.xlsx`.

**Cómo.**
1. **Apagá** `Cloud Flow - MPPP - COM`.
2. Mandá un correo con `E01-feliz-formato11.xlsx`.
3. Comprobá que la Solicitud quedó **En proceso** y **sin acuse**.
4. Esperá los 30 minutos del umbral + una corrida de VIG.
5. **Volvé a prender** `MPPP-COM` al terminar.

**Esperado.**

| Qué | Valor |
|---|---|
| Llega el acuse | **sí** |
| Bitácora `Acuse iniciado` / `Acuse enviado` | origen **MPPP-VIG** (no MPPP-COM) |
| Llega **una sola vez** | sí |

**Automatizable.** Manual (depende de `MPPP-COM`/`MPPP-ENV` real —`Mail.Send`— y del schedule de
`MPPP-VIG`).

---

### V-06 · Bloque 5 — los correos por clasificar vencen

**Qué prueba.** DF-09: lo que nadie clasifica en 30 días pasa a **Vencida**.

**Necesita.** Permiso para editar `sanic_fecharecibido` de una Solicitud No reconocida (la de
C-04); rol de administración o API con privilegio de escritura.

**Cómo.** Tomá una Solicitud en **No reconocida** (C-04) y poné `sanic_fecharecibido` con una fecha
de hace **40 días**. Esperá a que corra VIG.

**Esperado.**

| Qué | Valor |
|---|---|
| Estado | **Vencida** |
| `sanic_fechacerrada` | con fecha |
| Bitácora | `Vencida`, origen **MPPP-VIG** |
| Comunicación al cliente | **ninguna** |
| Sale de | **Por clasificar** |

> **Vencida** y no **Descartada**: la distinción es a propósito. Descartada = alguien decidió
> ignorarlo. Vencida = **nadie lo miró**, y eso es un dato de gestión.

**Automatizable.** Manual (la comparación de 30 días y el pase a Vencida viven en `MPPP-VIG`, no
en un plugin).

---

### Resultados

| Caso | Solicitud | Resultado | Notas |
|---|---|---|---|
| V-01 corre en vacío | | | |
| **V-02 correo en la bandeja** | | | |
| V-03 reintento | | | |
| V-04 se acaban los reintentos | | | |
| V-05 comunicación pendiente | | | |
| V-06 vencimiento | | | |

> **Acordate de dejar todo como estaba**: `MPPP-REC` y `MPPP-COM` prendidos, y los parámetros con
> sus valores originales si los bajaste para no esperar.

---

## 08 — Integridad, plugins y claves

Cubre los **steps de plugin** que corren al guardar, y que son la red que impide dejar el sistema
en un estado imposible. Son los que protegen contra alguien que escribe por el Web API sin pasar
por la app.

**Corregido: la tabla de abajo tenía un step que nunca se construyó y le faltaba uno que sí
existe.** Verificado contra el código (`src/Sanic.Mppp.Plugins/Steps/*.cs`) y en vivo contra Dev
(`plugintypes` filtrado por `Sanic.Mppp`): hay **13 tipos de plugin registrados en total** — estos
8 steps más las 5 Custom API (`ClasificarCorreoApi`, `ExportarParaDigitarApi`,
`ObtenerFilasPendientesApi`, `RegistrarResultadoRpaApi`, `ValidarSolicitudApi`, que no son steps y
no se prueban en este archivo). `IntegridadAutorizacionPlanStep` **no existe como clase en el
código** — nunca se construyó así; la integridad de una autorización duplicada (I-05) la hace la
**clave alternativa** `sanic_mppp_key_autorizacionplan_autorizado_plan`, no un plugin. En cambio
faltaba `RevisionAtendidaStep` (el que atiende el comando "Revisado", `diseno/05-app-model-driven.md`
§5): existe desde el 2026-09-23 y quedó fuera de esta tabla por error.

Los ocho steps registrados:

| Step | Qué protege |
|---|---|
| `NombreCalculadoStep` | el nombre de las tablas que no lo digita nadie |
| `NormalizarYValidarStep` | normaliza y valida al guardar |
| `ListaBlancaStep` | que solo se escriba lo que se puede escribir |
| `TransicionDeFilaStep` | que una transición de estado sea legal |
| `PostTransicionDeFilaStep` | los efectos posteriores (Bitácora, estado de la Solicitud) |
| `AtenderPorClasificarStep` | Atendido y Descartar |
| `IntegridadDeReglaStep` | que el catálogo de reglas sea coherente |
| `RevisionAtendidaStep` | deja en Bitácora el motivo que escribió la máquina, al usar "Revisado" |

> La integridad de una autorización duplicada (antes atribuida acá a
> `IntegridadAutorizacionPlanStep`) la prueba la **clave alternativa** sobre AutorizacionPlan, no
> un step: ver I-05 más abajo.

**Cómo se prueban.** Casi todos se prueban **haciendo algo prohibido** y verificando que el sistema
se niegue con un mensaje claro. Un plugin que deja pasar lo prohibido es peor que no tenerlo.

---

### I-01 · El catálogo de reglas: una regla activa sin evaluador

**Qué prueba.** `IntegridadDeReglaStep` (D-13): una regla activa cuyo código no sabe evaluar.

**Necesita.** Sesión `admin_tecnico` (Configuración → Reglas).

**Cómo.** En **Configuración → Reglas**, creá una regla nueva:

- Código: `REGLA_QUE_NO_EXISTE`
- Nivel: Registro · Efecto: Rechaza · Orden: 90

**Esperado.** **No deja guardar.** El mensaje dice que esa regla no tiene evaluador programado
para su nivel.

**FALLA si** guarda. La próxima validación reventaría con una excepción y todas las Solicitudes
quedarían en Ingresada.

**Automatizable.** API (`corredor.py`, caso I-01).

---

### I-02 · Una regla que depende de otra inactiva

**Qué prueba.** `IntegridadDeReglaStep`, segundo caso de D-13.

**Necesita.** Sesión `admin_tecnico`.

**Cómo.** Desactivá `PLAN_EXISTE` (que es de la que dependen cuatro reglas).

**Esperado.** **No deja desactivarla**, o no deja guardar la que queda colgando. El mensaje nombra
la dependencia rota.

> Acordate de dejar `PLAN_EXISTE` **activa** al terminar.

**Automatizable.** API (`corredor.py`, caso I-02).

---

### I-03 · Una regla de nivel Registro con efecto "Envía a revisión"

**Qué prueba.** D-20: ese efecto solo tiene sentido a nivel Solicitud o Correo.

**Necesita.** Sesión `admin_tecnico`.

**Cómo.** Editá `MONEDA_DEL_PLAN` (nivel Registro) y ponele efecto **Envía a revisión**.

**Esperado.** **No deja guardar**, con un mensaje que explique por qué.

> Dejá `MONEDA_DEL_PLAN` con efecto **Rechaza** al terminar.

**Automatizable.** API (`corredor.py`, caso I-03).

---

### I-04 · Una regla que depende de sí misma

**Qué prueba.** El defecto que encontró la revisión de código: la autodependencia se rechaza
**por el código**, antes de mirar ningún orden.

**Necesita.** Sesión `admin_tecnico`.

**Cómo.** Editá cualquier regla y poné en `sanic_dependede` **su propio código**.

**Esperado.** **No deja guardar**: *"No se puede guardar la regla 'X': no puede depender de sí
misma."*

> Probalo con una regla cuyo `orden` sea el mismo que tendría su "dependencia" — es el caso que una
> prueba mal escrita dejaba pasar por coincidencia.

**Automatizable.** API (`corredor.py`, caso I-04).

---

### I-05 · Integridad de una autorización

**Qué prueba.** **Corregido**: no hay un `IntegridadAutorizacionPlanStep` (esa clase no existe en
el código, ver la nota de arriba). Lo que realmente prueba este caso es la **clave alternativa**
`sanic_mppp_key_autorizacionplan_autorizado_plan` sobre AutorizacionPlan.

**Necesita.** Sesión `admin_planes` (Catálogos → Autorizaciones - Planes); una autorización
existente (P-03, TU-CORREO → PR11).

**Cómo.** En **Catálogos → Autorizaciones - Planes**, creá una autorización duplicada: el
**mismo** autorizado sobre el **mismo** plan que ya tiene (`TU-CORREO → PR11`).

**Esperado.** **No deja guardar** (por la clave alternativa), con un mensaje entendible y
**no** un volcado técnico de la plataforma.

**Automatizable.** API (`corredor.py`, caso I-05).

---

### I-06 · El nombre calculado

**Qué prueba.** `NombreCalculadoStep`.

**Necesita.** Sesión `admin_planes`.

**Cómo.** Creá una **Autorización** nueva (autorizado `TU-CORREO`, plan `PR06` si no existe ya) y
**no toques el campo Nombre**.

**Esperado.** Al guardar, el nombre queda calculado solo: **`correo → CODIGO`**.

Lo mismo con un **Plan**: el nombre se arma con su código y su cliente.

**Automatizable.** API (`corredor.py`, caso I-06).

---

### I-07 · Lista blanca de escrituras

**Qué prueba.** `ListaBlancaStep`: que no se pueda escribir a mano lo que escribe el sistema.

**Necesita.** Acceso al Web API con permiso de escritura sobre Fila (credenciales de servicio, o
una sesión con un rol que tenga `W` sobre Fila — Ejecutivo o Supervisor, `04`); una Fila existente
(la de C-01 alcanza). Para el lado de la app: sesión `ejecutivo` o `supervisor`.

**Cómo.** Abrí una **Fila** en la app e intentá editar a mano una columna que llena la validación
—por ejemplo `sanic_referencia`— y guardar. Además, probá un `PATCH` directo a esa columna por Web
API.

**Esperado.** **No deja**, o el campo está de solo lectura en el formulario **y** el plugin lo
rechaza si se intenta por API.

> **DEFECTO CONFIRMADO, no advertencia teórica.** Este archivo ya avisaba del riesgo; hoy está
> demostrado y automatizado en `herramientas/pruebas/corredor.py` (caso I-07): un `PATCH` directo a
> `sanic_referencia` por Web API se guarda con **HTTP 204** (éxito) en lugar de ser rechazado.
> `ListaBlancaStep` **no** bloquea esta columna. El formulario puede estar bien (de solo lectura),
> pero eso no alcanza: cualquiera con acceso al Web API y privilegio de escritura sobre Fila puede
> pisar una referencia que va a AS400. **Queda pendiente de decisión del aprobador**: si
> `ListaBlancaStep` tiene que extender su lista a `sanic_referencia` (y probablemente a las demás
> columnas que solo escribe la validación), o si el riesgo se acepta porque el privilegio de
> escritura ya está acotado a Ejecutivo/Supervisor por la matriz de `04`. No lo des por OK ni por
> FALLA vos mismo: es un hallazgo para que el aprobador decida, anotalo como tal en Resultados.

**Automatizable.** API (corredor.py, caso I-07 — ya demostrado; no hace falta correrlo de nuevo a
mano para confirmar el defecto).

---

### I-08 · Transición de estado ilegal

**Qué prueba.** `TransicionDeFilaStep`, que es lo que hace cierto DA-07.

**Necesita.** Acceso al Web API con permiso de escritura sobre Fila (o una sesión con rol
Ejecutivo/Supervisor) y una fila en Validada.

**Cómo.** Sobre una fila en **Validada**, intentá ponerla directo en **Aprobada** (saltándose
Digitada). Hacelo desde el formulario, no con el botón.

**Esperado.** **No deja**: el mensaje dice que esa transición no es válida desde ese estado.

**Este es el caso que demuestra que la seguridad no está en el botón.** Si pasa, alguien con el
Web API puede aprobar lo que quiera.

**Automatizable.** API (`corredor.py`, caso I-08 — **este es el caso que demuestra que la
seguridad no está en el botón**).

---

### I-09 · El paquete de plugins está al día

**Qué prueba.** Que el ensamblado en el sandbox sea el del repositorio, y no una versión vieja.

**Necesita.** Credenciales de lectura en Dataverse (o de servicio).

**Cómo.**

```bash
python3 herramientas/construir/paquete_plugins.py playbooks/paquete-plugins/*.md --solo-verificar
```

**Esperado.** **Corregido: decía "10 tipos", hoy son 13** (verificado en vivo el 2026-09-27, este
mismo comando: `"13 tipos de plugin registrados"` — 8 steps + 5 Custom API). `"estado":
"ya_existia"` con los **13 tipos** registrados.

> Cuidado con la trampa conocida: el sandbox puede seguir corriendo el binario viejo si no se
> subió el número de build. El síntoma es *"The plug-in type could not be found in the plug-in
> assembly"*, y la señal temprana es un `.nupkg` del mismo tamaño exacto que el anterior.

**Automatizable.** API (`paquete_plugins.py --solo-verificar`; `corredor.py`, caso I-09).

---

### Resultados

| Caso | Resultado | Notas |
|---|---|---|
| I-01 regla sin evaluador | | |
| I-02 dependencia inactiva | | |
| I-03 efecto inválido por nivel | | |
| I-04 autodependencia | | |
| I-05 autorización duplicada | | |
| I-06 nombre calculado | | |
| I-07 lista blanca | | |
| **I-08 transición ilegal** | | |
| I-09 paquete al día | | |

> Dejá el catálogo de **Reglas** como estaba: `PLAN_EXISTE` activa, `MONEDA_DEL_PLAN` con efecto
> Rechaza, y sin la regla inventada de I-01.
