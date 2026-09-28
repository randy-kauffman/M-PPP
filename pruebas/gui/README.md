# Pruebas de navegador (Playwright) — MPPP

Por qué existe esto: el botón **"Descargar para digitar"** existió en el servidor, **verificado
por tres caminos distintos de API** (`RetrieveEntityRibbon`, `ribbondiff` con sus LocLabels en los
dos idiomas, y el verificador del proyecto), y **no se dibujaba** en el navegador. La causa era la
metadata compilada del ribbon (caché del servidor). Encontrarlo así costó **dos días** (2026-09-27).
**Ninguna prueba de API lo hubiera detectado nunca.** Este arnés mira el DOM real, con un navegador
real, para cazar justo esa clase de defecto — la que falla en silencio, no la que se nota al primer
clic.

No reemplaza `herramientas/pruebas/` (API/backend, otro dueño): lo complementa.

## Alcance aprobado: dos casos, no trece

`pruebas/05-app-y-comandos.md` lista 13 casos (A-01 a A-13). Este arnés cubre **solo dos**, a
propósito:

1. **`casos/ribbon-fila.spec.mjs`** — los 7 botones del ribbon de Fila (`digitada`, `aprobar`,
   `rechazadaas400`, `anular`, `devolver`, `atendida`, `descargarparadigitar`): presentes y
   habilitados con una fila que admite el comando seleccionada; ausentes cuando ninguna fila
   seleccionada admite el comando (A-01/A-05 de `pruebas/05-app-y-comandos.md`).
2. **`casos/dn-02-descargar-para-digitar.spec.mjs`** — DN-02 de punta a punta: abrir la vista,
   seleccionar filas, apretar "Download for entry", **capturar la descarga real**, y validar el
   `.xlsx`: nombre contra el patrón, encabezados contra `exportacion.columnas`, cantidad de filas
   igual a lo seleccionado.

**Por qué no los otros once.** Son diálogos, íconos e idioma de UI (A-03 confirmación masiva, A-04
cancelar, A-06/A-07 transiciones de estado, A-08/A-09 diálogo de motivo, A-10 Anular/Devolver,
A-11/A-12 Solicitud, A-13 multiidioma): si se rompen, se notan al primer clic — alguien lo ve ese
mismo día. El botón invisible es el único defecto de esta clase que puede pasar **días** sin que
nadie lo note, porque el síntoma (nada aparece) es indistinguible de "no había nada que mostrar".
Agregar el resto queda documentado en `§6` de este archivo, para cuando el aprobador lo pida.

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
│   ├── semaforo.mjs                   semáforo compartido con semaforo.py (§5)
│   ├── datos-gui.mjs                  puente Node → sembrar_datos_gui.py + WhoAmI del dueño
│   ├── navegacion.mjs                 abrir la app, sitemap, grilla, selección de filas
│   ├── ribbon.mjs                     presente vs deshabilitado (diagnóstico opuesto, §"AUSENTE ≠ DESHABILITADO")
│   └── xlsx.mjs                       lee el .xlsx descargado (parser propio, ver el comentario del archivo)
├── casos/
│   ├── ribbon-fila.spec.mjs           caso 1: los 7 botones se dibujan cuando corresponde
│   └── dn-02-descargar-para-digitar.spec.mjs   caso 2: descarga de punta a punta
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

## 10. Agregar los demás casos de `pruebas/05-app-y-comandos.md`

Para sumar el resto (A-03 confirmación masiva, A-04 cancelar, A-06/A-07 transiciones de estado y
"el plugin manda", A-08/A-09 diálogo de motivo, A-10 Anular/Devolver, A-11/A-12 Solicitud, A-13
multiidioma) — fuera del alcance aprobado hoy:

1. Agregar un archivo nuevo en `casos/` (uno por caso o por grupo relacionado).
2. Reusar `ayudantes/navegacion.mjs` y `ayudantes/ribbon.mjs` — no duplicar selectores ahí, todo
   sale de `selectores.mjs`.
3. Para los diálogos de motivo (A-08, A-09, A-12): son `navigateTo` a `entityrecord` de
   `sanic_mppp_tbl_motivoaccion` en modo creación dentro de un diálogo modal — buscar el
   `[role="dialog"]` y el campo de texto por su etiqueta, con la misma estrategia de `getByRole`.
4. Para A-13 (multiidioma): correr el mismo caso con dos sesiones (una de un usuario en inglés),
   pasando el idioma a `verificarBotones(page, BOTONES_FILA, 1033)` en vez de `3082`.
