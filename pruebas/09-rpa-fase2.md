# 09 — Fase 2: el bot RPA (custom API, claim, FIFO, catálogo de errores)

Cubre lo que **no tiene un solo caso escrito**: `sanic_mppp_capi_obtenerfilaspendientes`,
`sanic_mppp_capi_registrarresultadorpa`, la puerta D-9 (`Steps/PuertaDelRpa`), el claim con
vencimiento, el orden FIFO, el tope de lote, la tolerancia de fecha, el catálogo `MPPP-RPA-00x` y
la marca de atención manual. Fuente: `diseno/03-contratos-custom-api.md` §2-§3,
`diseno/FASE2-ESTADO.md`, y el código en `src/Sanic.Mppp.Plugins/Steps/PuertaDelRpa.cs`,
`Dominio/SeleccionParaElRpa.cs`, `Dominio/ReglasDelRpa.cs`, `Dominio/CatalogoDeCodigosRpa.cs`,
`Api/ObtenerFilasPendientes*.cs`, `Api/RegistrarResultadoRpa*.cs`.

## Convención de IDs de este archivo y de los que siguen

Prefijos ya usados por `00`–`08` (verificado el 2026-09-27 con
`rg -o --no-filename '^## [A-Z]+-[0-9]+' pruebas/0*.md`): **A, C, G, I, M, P, R, S, V**. Los cinco
archivos nuevos (`09`–`13`) usan prefijos que no colisionan con esos ni entre sí:

| Prefijo | Archivo | Tema |
|---|---|---|
| `B-` (**B**ot) | `09-rpa-fase2.md` | este archivo |
| `U-` (a**U**torización) | `10-autorizacion-cliente.md` | DN-01 |
| `E-` (**E**xportación) | `11-exportacion-excel.md` | DN-02 |
| `O-` (**O**bligatoriedad) | `12-obligatoriedad-gestion.md` | DN-03 |
| `H-` (**H**oy) | `13-construido-hoy.md` | lo construido el 2026-09-27 |

## Herramientas de este archivo

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

## Preparativos comunes a todo este archivo

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

### B-01 — Rol del RPA no instalado en el entorno (MPPP-RPA-007)

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

### B-02 — Sin identidad de aplicación, o sin el rol (MPPP-RPA-006)

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

### B-03 — El bot pasa la puerta (D-9, camino feliz)

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

### B-04 — Orden FIFO estricto (fecha validada, y a igualdad, número de fila)

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

### B-05 — `maximo`: defecto 100, recorte silencioso al tope (nunca error)

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

### B-06 — `maximo` / `claimminutos` inválidos rechazan la llamada

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

**Qué tiene que pasar.** 1, 2, 4 y 5 rechazan con un mensaje que nombra el parámetro y exige
"entero mayor que cero"; 3 responde 200 con el defecto de 100.

**Cómo se verifica.** Código HTTP y mensaje de cada llamada.

**Cómo se sabe que falló.** Si 3 rechaza (el defecto de `maximo` dejó de aplicarse), o si 4/5
aceptan silenciosamente un valor sin sentido.

**Automatizable.** API.

---

### B-07 — El claim se pone al entregar, y frena a una PERSONA

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

### B-08 — El claim NO frena al bot cuando reporta, aunque haya vencido

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

### B-09 — El claim NO excluye: el bot puede volver a pedir lo mismo

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

### B-10 — Cuándo se libera el claim: en terminal sí, en Validada→Digitada no

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

### B-11 — `rpa.puedeaprobar = no` oculta las Digitadas del lote

**Qué prueba.** Sin el aval de C-03, el bot no debe ni recibir trabajo de aprobar: solo se le
entregan Validadas.

**Necesita.** Coordinar con quien esté corriendo pruebas de fase 2 en simultáneo: `rpa.puedeaprobar`
es un parámetro de ENTORNO COMPARTIDO. Cambiarlo afecta a cualquier otra prueba que dependa de su
valor actual (en Dev hoy vale `si`, según el comentario de `probar_rpa.py`). Restaurar el valor
original apenas termine el caso.

**Cómo.**
1. Dejar una Fila en Digitada por el propio bot (`probar_rpa.py --confirmar`).
2. Cambiar `rpa.puedeaprobar` a `no` (nueva versión del parámetro, nunca editar la vigente — ver
   `12-obligatoriedad-gestion.md` O-08 para el mismo criterio de versionado).
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

### B-12 — Para aprobar, solo lo que digitó el mismo bot

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

### B-13 — `fechaproceso`: offset explícito, no en el futuro, dentro de tolerancia (MPPP-RPA-003)

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

### B-14 — Catálogo de errores por ítem: 001, 002, 004, 005

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

### B-15 — Lote más grande que `rpa.lote.maximo` se rechaza ENTERO (MPPP-RPA-008)

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

### B-16 — Lote vacío se rechaza (MPPP-RPA-009)

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

### B-17 — Falla parcial: un ítem malo no arrastra a los buenos

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

### B-18 — Idempotencia: `estadofila` manda, nunca `yaregistrado`

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

### B-19 — Atención manual: marca, libera el claim, y cierra la puerta al bot

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

### B-20 — La marca de atención manual se apaga sola con cualquier transición exitosa

**Qué prueba.** Nadie tiene que acordarse de apagarla a mano (aunque también existe el botón
*Atendida* — ver `13-construido-hoy.md` H-01/H-07): CUALQUIER transición exitosa de la fila la
limpia, sin importar quién la hizo ni a qué estado fue.

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

## Resultados

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
