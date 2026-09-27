# Pruebas de navegador (Playwright) — MPPP

Por que existe esto: el boton **"Descargar para digitar"** existió en el servidor, **verificado
por tres caminos distintos de API**, y no se dibujaba en el navegador. La causa era la metadata
compilada del ribbon. **Ninguna prueba de API lo hubiera detectado nunca.** Este arnés mira el DOM
real, con un navegador real, para cazar justo esa clase de defecto.

No reemplaza `herramientas/pruebas/` (API/backend, otro agente trabaja ahí): lo complementa.

## 1. Cómo el aprobador genera su sesión

Esta máquina (la que construyó el arnés) corre **headless**, sin escritorio, y no puede completar
un login con MFA. La sesión la tiene que generar **una persona, en su propia máquina, con
escritorio**.

```bash
cd pruebas/gui
npm install                    # una sola vez
npx playwright install chromium  # una sola vez
node guardar-sesion.mjs ejecutivo   # o: supervisor, administrador, etc.
```

Qué pasa:

1. Se abre una ventana real de Chromium (headed) con la app.
2. Completá el login a mano: usuario, contraseña, MFA. El script espera hasta **15 minutos**.
3. Apenas la app termina de cargar (aparece el sitemap), el script detecta que ya estás adentro.
4. Guarda la sesión en `local/sesion-<usuario>.json` — **fuera del repositorio** (`/local/` está en
   el `.gitignore` de la raíz, verificado con `git check-ignore`, y el script vuelve a verificarlo
   él mismo antes de escribir nada; si no lo confirma, **no escribe** y avisa).

El script **nunca imprime cookies, tokens ni el contenido del storageState**. Solo vas a ver:

```
listo, guardado en local/sesion-ejecutivo.json
```

### Cuánto dura la sesión y qué hacer cuando caduca

El `storageState` de Playwright guarda cookies y almacenamiento local del navegador en el momento
de guardarlo. Dataverse/Entra ID emiten tokens con vencimiento (normalmente horas; la política de
sesión exacta la define el tenant, no este arnés). Cuando caduque vas a ver que los casos fallan
al esperar el sitemap (timeout esperando `navigation`) o quedan en la pantalla de login. **La
solución es siempre la misma**: volver a correr `node guardar-sesion.mjs <usuario>` y generar una
sesión nueva. No hay forma de "refrescarla" sin volver a pasar por el login.

## 2. Cómo correr el arnés

Necesitás una sesión ya guardada (paso 1) y decir con cuál usuario correr:

```bash
cd pruebas/gui
MPPP_USUARIO=ejecutivo npx playwright test              # todos los casos
MPPP_USUARIO=ejecutivo npx playwright test --headed     # viéndolo (si hay DISPLAY)
MPPP_USUARIO=ejecutivo npx playwright test --ui         # modo interactivo
MPPP_USUARIO=ejecutivo npx playwright test ribbon-fila  # un archivo puntual
```

Si falta `MPPP_USUARIO` o no existe `local/sesion-<usuario>.json`, el arnés lo dice con un mensaje
claro (en español) y no arranca ningún navegador.

### Sin sesión, qué SÍ se puede correr

```bash
node verificar-sin-sesion.mjs
```

Chequea lo que no necesita login: que `selectores.mjs` centraliza los 7 botones sin ids repetidos,
que las columnas de exportación esperadas están completas, que `/local/` sigue ignorado por git,
que `guardar-sesion.mjs` no tiene ningún `console.log` sospechoso, y que ningún archivo del arnés
importa de `herramientas/pruebas/`, `src/` ni `playbooks/`.

## 3. Estructura

```
pruebas/gui/
├── config.mjs                        URL del entorno, appid, rutas — un solo lugar
├── selectores.mjs                     LOS 7 BOTONES, centralizados (ver §4)
├── guardar-sesion.mjs                 headed, lo corre el aprobador (§1)
├── descubrir-selectores.mjs           headless, con sesión real, para completar `dataId` (§4)
├── verificar-sin-sesion.mjs           chequeos que no necesitan login
├── playwright.config.mjs
├── ayudantes/
│   ├── sesion.mjs                     resuelve/valida la ruta del storageState, nunca la lee
│   ├── navegacion.mjs                 abrir la app, sitemap, grilla, selección de filas
│   ├── ribbon.mjs                     presente vs deshabilitado (diagnóstico opuesto)
│   └── xlsx.mjs                       lee el .xlsx descargado (ExcelJS)
├── casos/
│   ├── ribbon-fila.spec.mjs           caso "ribbon": los 7 botones se dibujan
│   └── dn-02-descargar-para-digitar.spec.mjs   caso DN-02: descarga de punta a punta
└── descargas/                         (gitignored) .xlsx capturados durante una corrida
```

## 4. Selectores centralizados — cómo se obtuvieron

Todo vive en `selectores.mjs`. Dos partes:

**a) Los 7 botones, su id, entidad, ubicaciones y etiquetas en los dos idiomas** — copiados TAL
CUAL de `playbooks/importacion/comandos.md` (sección 2, tabla de `sanic_mppp_tbl_fila`), que es el
playbook que ya se aplicó al entorno Dev. No es una suposición: es la orden real que generó el
`RibbonDiffXml` que hoy está en el servidor.

**b) La estrategia de selector** — se investigó en Microsoft Learn
("Power Platform Playwright samples › Locators", consultado 2026-09-27) cómo el propio toolkit
oficial de Microsoft ubica botones del command bar. La recomendación documentada es
`page.getByRole('button', { name: '<etiqueta visible>' })`, no un CSS selector por `data-id`
crudo. Por eso el arnés ubica los 7 botones por su etiqueta visible (con manejo del menú
"Más comandos" si un botón queda colapsado por falta de espacio) — ver `ayudantes/ribbon.mjs`.

Es cierto que UCI suele ponerle un atributo `data-id` a estos botones, pero su valor exacto **no
está documentado públicamente y no se pudo confirmar contra esta app** porque este agente no tiene
(ni debe tener) una sesión con MFA. `selectores.mjs` deja un campo `dataId: null` por botón,
explícitamente sin inventar, con instrucciones de cómo completarlo:

```bash
MPPP_USUARIO=ejecutivo node descubrir-selectores.mjs
```

Ese script abre la vista con la sesión real, ubica cada botón por etiqueta (sin depender de
`data-id`) e imprime el `data-id` que encuentre en el DOM, para pegarlo a mano en `selectores.mjs`.
Mientras `dataId` siga en `null`, el arnés funciona igual: es un respaldo opcional, no un
requisito.

**Si mañana Microsoft cambia el DOM o el proyecto cambia una etiqueta**, se toca `selectores.mjs`
y nada más — ningún caso de prueba tiene un id ni una etiqueta escritos a mano.

## 5. Qué NO se pudo correr todavía (y por qué)

Este agente construyó y verificó todo lo que se puede verificar **sin** una sesión real:

- El proyecto instala (`npm install`) y Chromium se descargó bien (`npx playwright install
  chromium`).
- Los dos archivos de casos cargan sin errores de sintaxis/imports (`npx playwright test --list`
  los lista: 3 tests en 2 archivos).
- Sin `MPPP_USUARIO` o sin sesión guardada, el arnés falla con un mensaje claro y **no** intenta
  adivinar ni inventar un resultado.
- Con una sesión de prueba vacía (no una sesión real, solo `{"cookies":[],"origins":[]}`, para
  probar el circuito), el arnés SÍ llega a `https://org36e60d9d.crm.dynamics.com/` (confirma que
  el entorno es alcanzable desde esta máquina), es redirigido a login de Entra ID como se espera
  sin sesión válida, y falla con un timeout **legible** (no un crash), con screenshot, video y
  trace capturados — confirma que el reporte de fallas del arnés funciona.

Lo que **falta correr de verdad** (necesita `local/sesion-<usuario>.json`, que solo genera el
aprobador con MFA):

- Caso DN-02 completo (seleccionar filas reales, apretar el botón, capturar la descarga, abrir el
  `.xlsx` y comparar contra `COLUMNAS_EXPORTACION_ESPERADAS`).
- Caso "ribbon": los 7 botones presentes/habilitados con selección, y presentes/deshabilitados sin
  selección (A-05).
- Confirmar o completar los `dataId` de `selectores.mjs` con `descubrir-selectores.mjs`.

No se afirma ningún resultado de estos casos: no corrieron.

## 6. Agregar los demás casos de `pruebas/05-app-y-comandos.md`

El archivo lista 13 casos (A-01 a A-13); este arnés cubre hoy el equivalente de A-01/A-05
(presencia y habilitación) y el caso DN-02 nuevo. Para sumar el resto (A-03 confirmación masiva,
A-04 cancelar, A-06/A-07 transiciones de estado y "el plugin manda", A-08/A-09 diálogo de motivo,
A-10 Anular/Devolver, A-11/A-12 Solicitud, A-13 multiidioma):

1. Agregar un archivo nuevo en `casos/` (uno por caso o por grupo relacionado).
2. Reusar `ayudantes/navegacion.mjs` y `ayudantes/ribbon.mjs` — no duplicar selectores ahí, todo
   sale de `selectores.mjs`.
3. Para los diálogos de motivo (A-08, A-09, A-12): son `navigateTo` a `entityrecord` de
   `sanic_mppp_tbl_motivoaccion` en modo creación dentro de un diálogo modal — buscar el `[role="dialog"]`
   y el campo de texto por su etiqueta, con la misma estrategia de `getByRole`.
4. Para A-13 (multiidioma): correr el mismo caso con dos sesiones (una de un usuario en inglés),
   pasando el idioma a `verificarBotones(page, BOTONES_FILA, 1033)` en vez de `3082`.
