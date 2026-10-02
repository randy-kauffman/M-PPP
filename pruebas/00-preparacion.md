# 00 — Preparación

Se hace **una sola vez**, antes de todos los demás archivos. Si algo de acá falla, no sigas: los
casos siguientes van a fallar por el andamio y no por el sistema, y eso no prueba nada.

---

## P-01 · La carpeta del buzón

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

## P-01b · Los permisos del buzón compartido

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

## P-02 · El remitente de las pruebas

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

## P-03 · Datos de negocio de prueba

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

## P-04 · Los Excel de los casos

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

## P-05 · Los cinco flujos activos

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

## P-06 · Los parámetros cargados

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

## Resultados

| Caso | Resultado | Notas |
|---|---|---|
| P-01 carpeta `Procesados` | | |
| **P-01b permisos del buzón** | | Full Access + Send As |
| P-02 remitente | | |
| P-03 datos de negocio | | |
| P-04 Excel generados | | |
| P-05 flujos activos | | |
| P-06 parámetros | | |
