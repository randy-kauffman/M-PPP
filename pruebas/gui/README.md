# Pruebas de navegador (Playwright) — MPPP

Por qué existe esto: el botón **"Descargar para digitar"** existió en el servidor, **verificado
por tres caminos distintos de API** (`RetrieveEntityRibbon`, `ribbondiff` con sus LocLabels en los
dos idiomas, y el verificador del proyecto), y **no se dibujaba** en el navegador. La causa era la
metadata compilada del ribbon (caché del servidor). Encontrarlo así costó **dos días** (2026-09-27).
**Ninguna prueba de API lo hubiera detectado nunca.** Este arnés mira el DOM real, con un navegador
real, para cazar justo esa clase de defecto — la que falla en silencio, no la que se nota al primer
clic.

No reemplaza `herramientas/pruebas/` (API/backend, otro dueño): lo complementa.

## Alcance: ocho casos conectados (2026-09-28)

Hasta el 2026-09-27 este arnés cubría **dos** casos a propósito (ver el historial de esta sección
más abajo). El aprobador pidió sumar los casos de navegador que hoy no cubre nadie **y conectarlos**
a los sets de Python en vez de dejarlos como un comando que hay que acordarse de correr
("un comando que hay que acordarse de correr es un comando que no se corre"). Esta migración
(2026-09-28) agregó seis casos nuevos y conectó los ocho — los dos viejos más los seis nuevos — a
`herramientas/pruebas/sets/set_a.py` y `set_e.py`, que ahora invocan `npx playwright test` por
subproceso y reportan OK/FALLA real en el resumen de `correr.py`, no `N-A`:

1. **`casos/ribbon-fila.spec.mjs`** (A-01) — los 5 botones que admiten Validada, presentes y
   habilitados, en **HomepageGrid Y SubGrid** (el SubGrid se agregó esta migración).
2. **`casos/a-05-sin-seleccion.spec.mjs`** (A-05) — sin selección, ningún botón propio se dibuja
   (AUSENTE, no "deshabilitado" — ver la nota de corrección fechada en el propio archivo). Vivía
   como un segundo test de `ribbon-fila.spec.mjs`; se separó a su propio archivo para poder
   invocarse y reportarse de forma independiente.
3. **`casos/a-02-comandos-genericos-ocultos.spec.mjs`** (A-02) — los 6 comandos genéricos (DA-06) y
   "Nuevo" ocultos/visibles según corresponda, en las 11 tablas del proyecto (2 sesiones: `ejecutivo`
   cubre 9, `admin_tecnico` cubre Parámetro/Regla).
4. **`casos/a-04-cancelar-no-cambia-nada.spec.mjs`** (A-04) — cancelar el diálogo de confirmación no
   dispara ninguna escritura al Web API (evidencia con red, no solo con la UI).
5. **`casos/a-09-cancelar-dialogo-motivo.spec.mjs`** (A-09) — cerrar el diálogo de motivo con la X,
   mismo mecanismo de evidencia que A-04.
6. **`casos/a-13-multiidioma-botones.spec.mjs`** (A-13) — etiquetas de botones Y mensaje de
   confirmación del JS, en el idioma real de la sesión (1033/3082).
7. **`casos/dn-02-descargar-para-digitar.spec.mjs`** (E-01) — DN-02 de punta a punta: abrir la
   vista, seleccionar filas, apretar "Download for entry", **capturar la descarga real**, y validar
   el `.xlsx`: nombre contra el patrón, encabezados contra `exportacion.columnas`, cantidad de filas
   igual a lo seleccionado.
8. **A-15** (`05-app-y-comandos.md`) no tiene archivo propio: reusa la corrida de `ribbon-fila`
   (`set_a.py::a15()` cachea el resultado de `a01()`, nunca duplica la corrida).

**Historial: por qué originalmente eran solo dos.** Son diálogos, íconos e idioma de UI (A-03
confirmación masiva, A-06/A-07 transiciones de estado, A-08 diálogo de motivo, A-10 Anular/Devolver,
A-11/A-12 Solicitud): si se rompen, se notan al primer clic — alguien lo ve ese mismo día. El botón
invisible (A-01/A-15) era el único defecto de esta clase que podía pasar **días** sin que nadie lo
note, porque el síntoma (nada aparece) es indistinguible de "no había nada que mostrar". Esa
distinción sigue siendo válida para lo que queda sin cubrir (§10 al final de este archivo).

## La distinción que le da sentido al caso 1: AUSENTE ≠ DESHABILITADO

Son diagnósticos **opuestos** con el mismo síntoma en pantalla:

- **DESHABILITADO** (presente, gris) → problema de REGLAS (`EnableRule` / `TRANSICIONES_VALIDAS`
  en `recursos/js/comandos.js`, definición, culpa nuestra).
- **AUSENTE** de la lista (no se dibuja nada) → problema de METADATA COMPILADA del ribbon (caché
  del servidor), que se regenera con "Regenerate ribbon metadata" del Command Checker
  (`&ribbondebug=true`).

`casos/ribbon-fila.spec.mjs` y `ayudantes/ribbon.mjs` (`formatearDiagnostico`) separan los dos
explícitamente en cada línea de diagnóstico y en cada mensaje de `expect(...)` — nunca un genérico
"el botón no apareció".

## 1. Prerrequisitos

Node 22, Playwright y Chromium ya están instalados en esta máquina (`npm install` +
`npx playwright install chromium` corridos). Si hace falta desde cero:

```bash
cd pruebas/gui
npm install
npx playwright install chromium
```

## 2. Las sesiones — quién las genera y cuánto duran

Las cuatro sesiones (`ejecutivo`, `supervisor`, `admin_planes`, `admin_tecnico`) viven en
`local/sesion-<papel>.json`, **fuera del repositorio** (`/local/` está en el `.gitignore` de la
raíz). Esta máquina corre **headless**, sin escritorio, y no puede completar un login con MFA: la
sesión la genera **el aprobador, en su propia máquina, con escritorio**:

```bash
cd pruebas/gui
node guardar-sesion.mjs ejecutivo   # o: supervisor, admin_planes, admin_tecnico
```

1. Se abre una ventana real de Chromium (headed) con la app.
2. Login a mano: usuario, contraseña, MFA. El script espera hasta 15 minutos.
3. Apenas la app termina de cargar (aparece el sitemap), detecta que ya estás adentro.
4. Guarda la sesión en `local/sesion-<papel>.json` (`guardar-sesion.mjs` verifica el `.gitignore`
   él mismo antes de escribir; si no lo confirma, no escribe nada). **Nunca imprime cookies, tokens
   ni el contenido del storageState.**

### Cuándo caduca, y qué hacer

Dataverse/Entra ID emiten tokens con vencimiento (horas, según la política del tenant). Cuando
caduca, la GUARDA DE SESIONES (ver §3) lo detecta y **el arnés no arranca ni un caso** — no vas a
ver un timeout confuso esperando un selector, vas a ver un mensaje que dice explícitamente que la
sesión está muerta o que el rol no es el esperado, con el nombre del papel afectado. **La solución
es siempre la misma**: volver a correr `node guardar-sesion.mjs <papel>` y generar una sesión
nueva — no hay forma de "refrescarla" sin volver a pasar por el login, y **solo el aprobador puede
hacerlo** (necesita su MFA, en su propia máquina). Si esto pasa en medio de una corrida automática,
el arnés PARA y reporta; no lo rodea ni lo reintenta.

## 3. La guarda de sesiones — antes de correr nada

`ayudantes/guarda-sesiones.mjs` invoca, como subproceso, la MISMA función que usan los sets de
Python: `herramientas/pruebas/estado_sesiones.py::verificar_o_fallar([usuario])`. Es el primer
paso de `setup/global-setup.mjs`, antes del semáforo y antes de sembrar un solo dato.

Por qué existe: **una prueba que pasa porque el usuario no ve algo es indistinguible de una que
pasa porque la sesión está muerta.** Las dos dan verde, y una está mintiendo. La guarda resuelve
`WhoAmI` contra la sesión real y compara el rol del usuario (vía el service principal, nunca
inventado) contra `ROL_ESPERADO` de `herramientas/pruebas/comportamiento_roles.py`. Si la sesión
está muerta o el rol no es el que toca, **revienta ahí mismo** con la causa exacta — nunca sigue de
largo con un warning ignorable.

Solo se verifica el papel que esta corrida va a usar (`MPPP_USUARIO`), no las cuatro sesiones: no
tiene sentido bloquear una corrida de `ejecutivo` porque la de `admin_tecnico` venció si esta
corrida nunca la toca.

Se puede correr la guarda sola, para las cuatro sesiones, sin arrancar nada de Playwright:

```bash
python3 herramientas/pruebas/estado_sesiones.py
```

## 4. Cómo correr el arnés

```bash
cd pruebas/gui
MPPP_USUARIO=ejecutivo npx playwright test              # los dos casos
MPPP_USUARIO=ejecutivo npx playwright test --headed     # viéndolo (si hay DISPLAY)
MPPP_USUARIO=ejecutivo npx playwright test --ui         # modo interactivo
MPPP_USUARIO=ejecutivo npx playwright test ribbon-fila  # un archivo puntual
```

Si falta `MPPP_USUARIO` o no existe `local/sesion-<usuario>.json`, el arnés lo dice con un mensaje
claro y no arranca ningún navegador.

**Antes de correr contra Dev, revisá el semáforo** (§5): si está en rojo, esperá a que se libere
con polling manual (`python3 herramientas/pruebas/semaforo.py --estado` en loop) — el arnés mismo
NO espera solo, revienta en el momento si está ocupado (mismo criterio que `correr.py`).

Orden real de `setup/global-setup.mjs` en cada corrida:

1. Guarda de sesiones (§3) — revienta acá si la sesión no sirve.
2. Toma el semáforo (§5) — revienta acá si está ocupado.
3. `WhoAmI` sobre la sesión activa → siembra el fixture (§6) con ese usuario como dueño.
4. Corre los casos.
5. Limpia el fixture, y siempre libera el semáforo (pase lo que pase en 3/4).

### Sin sesión, qué SÍ se puede correr

```bash
node verificar-sin-sesion.mjs
```

Chequea lo que no necesita login: que `selectores.mjs` centraliza los 7 botones sin ids repetidos,
que las columnas de exportación esperadas están completas, que `/local/` sigue ignorado por git,
que `guardar-sesion.mjs` no tiene ningún `console.log` sospechoso, y que ningún archivo del arnés
importa de `herramientas/pruebas/`, `src/` ni `playbooks/` (la siembra y la guarda hablan con
Python por **subproceso** — `execFile` a un script de `pruebas/gui/` o a `estado_sesiones.py` con
`python3 -c` — nunca con un `import`/`from` de JavaScript hacia fuera de `pruebas/gui/`).

## 5. El semáforo — nunca pisar una prueba manual del aprobador

`ayudantes/semaforo.mjs` lee y escribe el **mismo archivo** que
`herramientas/pruebas/semaforo.py` (`local/semaforo-pruebas.json`), con el mismo esquema JSON: no
es una copia de la lógica, es el MISMO candado visto desde Node. Se prueba interoperabilidad
cruzada (Node toma → Python lo ve en rojo y lo respeta; Node suelta → Python lo ve en verde de
nuevo) antes de confiar en él — ver el comentario en `ayudantes/semaforo.mjs`.

**`MPPP_SEMAFORO_HEREDADO=1` (2026-09-28).** Cuando `set_a.py`/`set_e.py` invocan este arnés por
subproceso DESDE un caso que ya corre dentro de `correr.py` (que toma el semáforo para TODA la
corrida, no una vez por set), `setup/global-setup.mjs` NO vuelve a tomarlo: ya es del proceso Python
que nos invocó, un nivel más arriba, y un segundo intento de tomarlo reventaría siempre contra su
propio PID (vivo). Con la bandera puesta, `global-setup.mjs` corre la guarda de sesiones, la siembra
y la limpieza igual que siempre, pero no toma ni suelta el candado. Corriendo `npx playwright test`
de forma DIRECTA (como en este README) esta variable no está seteada, y el comportamiento es el de
siempre. Ver el comentario largo en `setup/global-setup.mjs`.

`setup/global-setup.mjs` lo toma UNA vez para toda la corrida (siembra + los dos casos +
limpieza) — es el equivalente, para este arnés, de lo que `correr.py` hace por cada set de Python:
un solo candado para toda la ventana en la que se toca Dev, no uno por caso. Si la siembra falla a
mitad de camino, se suelta antes de relanzar el error (si no, quedaría trabado).

- `python3 herramientas/pruebas/semaforo.py --estado` — ver el estado sin tocarlo.
- **NUNCA `--liberar` un candado que no pusiste vos.** Un bloqueo manual del aprobador
  (`"pid": null`) no se libera solo — es a propósito: solo la persona que lo puso sabe cuándo
  terminó.
- Un candado de un arnés muerto (proceso con ese PID ya no existe) SÍ se libera solo, la próxima
  vez que alguien intente tomarlo — ni Python ni Node necesitan intervención manual para eso.

## 6. La siembra de datos — por qué existe y cómo se limpia

La vista "Mis clientes - por digitar" filtra Fila por `cliente.ownerid eq-userid`: si el Cliente
del fixture no es propiedad del usuario de la sesión activa, la grilla sale **vacía** y el caso
falla sin que nada esté roto en la app. `sembrar_datos_gui.py` (Python, service principal — el rol
Ejecutivo NO tiene privilegio de Crear sobre Cliente/Plan, confirmado en vivo con HTTP 403)
resuelve esto:

- El dueño del Cliente se descubre con `WhoAmI` sobre la sesión de navegador activa
  (`ayudantes/datos-gui.mjs::usuarioDeLaSesion`) — **nunca hardcodeado**: varía según con qué
  `MPPP_USUARIO` se corra.
- El Cliente nace con el prefijo reservado del proyecto (`herramientas/pruebas/marca_agua.py`,
  `CONVENCION-MARCA-AGUA.md`): `ZZP-Pruebas GUI SA`. `limpiar()` usa
  `marca_agua.borrar_marcado()` — relee `sanic_nombre` justo antes del `DELETE` y **no borra si no
  lleva el prefijo**. Es la misma regla que protege al fixture compartido
  (Cliente `be5332ec-03b7-f111-aaad-7ced8d3d6a77`): este script nunca lo toca, porque busca
  siempre por su propio nombre (`ZZP-Pruebas GUI SA`), nunca por parecido.
- Plan (`sanic_codigo = ZG01`, prefijo de código `ZG` — distinto del `PR` de `preparar_datos.py`,
  para no confundirse con ese fixture) y Solicitud (`sanic_messageid` con dominio
  `@pruebas.mppp.local`) usan claves ya exclusivas del fixture GUI, no necesitan la marca de texto.
- Filas: tantas en `Validada` como pida el caso (default 3) más una extra en `Digitada` — la única
  forma de que `aprobar`/`devolver` (que solo admiten `Digitada`) tengan algo que habilitarlos.
- `sembrar_datos_gui.py` **no toma el semáforo por su cuenta**: lo toma `global-setup.mjs` (Node)
  para toda la corrida. Si se corre este script a mano, fuera del arnés, quien lo corre tiene que
  chequear el semáforo primero — ver el docstring del script.

```bash
python3 sembrar_datos_gui.py --verificar   # solo lectura, no toca el semáforo
```

## 7. Estructura

```
pruebas/gui/
├── config.mjs                        URL del entorno, appid, rutas — un solo lugar
├── selectores.mjs                     LOS 7 BOTONES, centralizados (ver §8)
├── guardar-sesion.mjs                 headed, lo corre el aprobador (§2)
├── descubrir-selectores.mjs           headless, con sesión real, para completar `dataId`
├── verificar-sin-sesion.mjs           chequeos que no necesitan login
├── sembrar_datos_gui.py               siembra/limpia el fixture GUI (§6)
├── playwright.config.mjs
├── setup/
│   └── global-setup.mjs               guarda de sesiones → semáforo → siembra → (casos) → limpieza
├── ayudantes/
│   ├── sesion.mjs                     resuelve/valida la ruta del storageState, nunca la lee
│   ├── guarda-sesiones.mjs            invoca estado_sesiones.py::verificar_o_fallar (§3)
│   ├── semaforo.mjs                   semáforo compartido con semaforo.py (§5, con bypass MPPP_SEMAFORO_HEREDADO)
│   ├── datos-gui.mjs                  puente Node → sembrar_datos_gui.py + WhoAmI del dueño + fixtureSembrado()
│   ├── navegacion.mjs                 abrir la app, sitemap, grilla, selección de filas, subgrids, deep-link a un registro
│   ├── ribbon.mjs                     presente vs deshabilitado (diagnóstico opuesto, §"AUSENTE ≠ DESHABILITADO")
│   ├── dialogos.mjs                   grabar escrituras al Web API + cancelar/cerrar diálogos (A-04, A-09)
│   └── xlsx.mjs                       lee el .xlsx descargado (parser propio, ver el comentario del archivo)
├── casos/
│   ├── ribbon-fila.spec.mjs                    A-01: los botones se dibujan (HomepageGrid y SubGrid)
│   ├── a-05-sin-seleccion.spec.mjs             A-05: sin selección, ningún botón se dibuja (AUSENTE)
│   ├── a-02-comandos-genericos-ocultos.spec.mjs A-02: los 6 genéricos y "Nuevo" ocultos/visibles, 11 tablas
│   ├── a-04-cancelar-no-cambia-nada.spec.mjs   A-04: cancelar el diálogo de confirmación
│   ├── a-09-cancelar-dialogo-motivo.spec.mjs   A-09: cerrar el diálogo de motivo con la X
│   ├── a-13-multiidioma-botones.spec.mjs       A-13: etiquetas y mensaje de confirmación, por idioma
│   └── dn-02-descargar-para-digitar.spec.mjs   E-01: descarga de punta a punta
└── descargas/                         (gitignored) .xlsx capturados durante una corrida
```

## 8. Selectores centralizados — cómo se obtuvieron

Todo vive en `selectores.mjs`. Verificado contra la fuente, no solo copiado a ojo:

**a) Los 7 botones** (id, entidad, ubicaciones, secuencia y etiquetas en los dos idiomas) — TAL
CUAL `playbooks/importacion/comandos.md` (sección 2, tabla de `sanic_mppp_tbl_fila`), que es el
playbook que generó el `RibbonDiffXml` real del entorno Dev. Cruzado línea por línea contra ese
archivo (ids, secuencias 10-70, etiquetas 1033/3082) al retomar este arnés (2026-09-28): coincide.

**b) `TRANSICIONES_VALIDAS`** — copiado de `recursos/js/comandos.js` (la única fuente del lado
cliente; `herramientas/pruebas/verificar_transiciones.py` la compara contra el dominio C#).
Cruzado línea por línea (2026-09-28): coincide byte a byte.

**c) La estrategia de selector** — confirmada EN VIVO (no solo por documentación de Microsoft): el
rol de accesibilidad real de estos comandos es **`menuitem`** dentro de un `menubar` (no
`button`), y el nombre accesible es la etiqueta seguida del tooltip completo sin separador. Por
eso `ayudantes/ribbon.mjs` matchea por PREFIJO de un patrón combinado (`^(etiqueta1033|etiqueta3082)`),
no por texto exacto, y prueba las dos etiquetas de una sola vez.

**d) `dataId`** — respaldo opcional, `null` para `aprobar`/`devolver` (piden una fila `Digitada`,
fuera del alcance de la vista usada para descubrirlos hasta ahora). Completar con:

```bash
MPPP_USUARIO=ejecutivo node descubrir-selectores.mjs
```

**e) Columnas de exportación** (`COLUMNAS_EXPORTACION_ESPERADAS`) — copiadas del parámetro
`exportacion.columnas` (`playbooks/datos/parametros.md`), 12 columnas. El caso DN-02 las compara
contra los encabezados reales del `.xlsx` descargado.

## 9. Última corrida real (2026-09-28)

Con las cuatro sesiones vivas (generadas 2026-09-27 por la tarde) y el semáforo libre:

```
[setup] Guarda de sesiones: verificando 'ejecutivo' (viva + rol esperado)...
[setup] Guarda de sesiones OK para 'ejecutivo'.
[setup] Tomando el semáforo compartido (local/semaforo-pruebas.json)...
[setup] Sembrando datos GUI para la sesión de 'ejecutivo'...
[setup] WhoAmI de la sesión 'ejecutivo': 592f1177-6116-f111-8342-000d3a316609
[setup] sembrar_datos_gui.py -> creado: {...}

Running 3 tests using 1 worker

  ✓  DN-02 — Descargar para digitar › el boton descarga un .xlsx valido con las filas seleccionadas (32.9s)
  ✓  Ribbon de Fila › con filas Validada seleccionadas, los 5 comandos que las admiten están presentes y habilitados (34.9s)
  ✓  Ribbon de Fila › sin ninguna fila seleccionada, ningún botón propio se dibuja (1.0m)

[teardown] Limpiando datos GUI...
[teardown] sembrar_datos_gui.py --limpiar -> limpio: "borrados: [...]"
[teardown] Semáforo liberado.

  3 passed (2.5m)
```

Los 7 botones se comportaron como esperaba `TRANSICIONES_VALIDAS`: 5 presentes y habilitados con
una fila `Validada` seleccionada (`digitada`, `rechazadaas400`, `anular`, `atendida`,
`descargarparadigitar`), `aprobar`/`devolver` ausentes-y-esperados (piden `Digitada`, no
`Validada`), y ninguno presente sin selección. El `.xlsx` de DN-02 bajó con el nombre esperado
(`MPPP-para-digitar-YYYYMMDD-HHMMSS.xlsx`), las 12 columnas de `exportacion.columnas`, y 3 filas
(igual a las 3 seleccionadas). El fixture (`ZZP-Pruebas GUI SA` y sus hijos) quedó limpio después
(`sembrar_datos_gui.py --verificar` confirmó `cliente: false` al terminar).

## 10. Lo que sigue sin cubrir de `pruebas/05-app-y-comandos.md`

Después de esta migración (2026-09-28, ocho casos conectados — §1) queda, todavía fuera del
alcance de este arnés:

- **A-03** (confirmación masiva: el texto exacto del diálogo "¿Aplicar 'Digitada' a 3 fila(s)
  seleccionada(s)?" y del resultado "3 de 3 fila(s) actualizada(s).") — el EFECTO ya lo cubre
  `set_a.py::a03()` por API; solo el DOM del diálogo queda GUI, opcional.
- **A-06/A-07** (transiciones de estado, "el plugin manda") — el EFECTO ya lo cubren `set_a.py::
  a06()/a07()` por API; la experiencia en pantalla queda GUI, opcional, no bloqueante.
- **A-08** (que el diálogo de motivo se DIBUJE centrado, con el título "Rechazada en AS400 — 2
  fila(s)") — el EFECTO ya lo cubre `set_a.py::a08()` por API. `ayudantes/dialogos.mjs` (agregado
  esta migración para A-09) da la base para escribir este caso: reusarlo, no duplicar.
- **A-10** (vistas Devueltas/estado en pantalla) — el EFECTO ya lo cubre `set_a.py::a10()` por API;
  GUI opcional.
- **A-11/A-12** (Solicitud: vista "Por clasificar", vista "Para revisar") — el EFECTO ya lo cubren
  `set_a.py::a11()/a12()` por API; GUI opcional. Necesitan los 3 botones de Solicitud
  (`atendido`/`descartar`/`revisado`), que tampoco tienen `dataId` descubierto todavía (§8 punto d).
- **La SubGrid de Solicitud, sus 3 botones propios** (`atendido`/`descartar`/`revisado`) — A-01 ya
  cubre la SubGrid de FILA (esta migración); la de Solicitud (otra tabla, otros botones) sigue sin
  caso.
- **A-02, las otras 2 sesiones** (`supervisor`, `admin_planes`) — el arnés ya corre con `ejecutivo` +
  `admin_tecnico` (alcanza para las 11 tablas, `HideCustomAction` es de tabla, no de rol — ver la
  nota en `05-app-y-comandos.md`); agregar las otras dos sesiones no agrega cobertura nueva sobre la
  metadata, solo lo haría si algún día `HideCustomAction` pasara a depender del rol.

Para agregar cualquiera de estos: reusar `ayudantes/navegacion.mjs`, `ayudantes/ribbon.mjs` y
`ayudantes/dialogos.mjs` — no duplicar selectores, todo sale de `selectores.mjs`; y conectar el caso
nuevo a `set_a.py` con el mismo patrón `_playwright_test`/`_chequear_playwright` que ya usan A-01,
A-02, A-04, A-05, A-09, A-13 y A-15.
