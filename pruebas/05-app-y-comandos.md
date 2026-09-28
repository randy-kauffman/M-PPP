# 05 — La app y sus comandos de la barra

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

## El corazón de este set, y la lección que lo justifica

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

### Por qué esto NO usa `comportamiento_roles.py --con-escritura`

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

## Fase 1 + fase 2, qué cubre este set y qué no

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

## Un hallazgo de esta migración: el origen de Bitácora nunca es "App"

La versión vieja de A-03 decía "Bitácora `Fila digitada`, origen **App**". Verificado leyendo
`src/Sanic.Mppp.Plugins/Steps/StepsDeFila.cs` (las cuatro llamadas a `RegistrarEvento` de este
archivo: `PostTransicionDeFilaStep`, `AtenderPorClasificarStep`, `RevisionAtendidaStep`) y
`Steps/PlomeriaDeFila` — **las cuatro usan `OrigenDelEvento.Plugin`**. `grep -rn
"OrigenDelEvento.App" src/` no devuelve NINGÚN resultado: el valor `App` (159460008) está definido
en el choice pero **ningún step lo asigna nunca**, sin importar si quien transiciona es una persona
por la app o el bot. A-03, A-08, A-10, A-11 y A-12 quedaron corregidos para esperar `Plugin`
(159460007), con esta evidencia.

## Otro hallazgo: la lista blanca puede no tener a "Atendida" (ver A-14)

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

## A-01 · Los botones aparecen donde deben

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

## A-02 · Los comandos genéricos NO aparecen (12.6)

**Qué prueba.** Los `HideCustomAction` en las tablas del sitemap.

**CORRECCIÓN 2026-09-28 (CONVENCIONES.md §7, "nombrar, no contar").** Esta sección contaba "10
tablas" desde antes del 2026-09-27, cuando `AutorizacionCliente` todavía no tenía entrada propia de
sitemap (se agregó el 2026-09-27, `diseno/05-app-model-driven.md` §2). Hoy son **11**: se nombran
las 11 en la tabla de abajo, no se repite el número viejo.

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

## A-03 · Digitada, sobre varias filas a la vez (acción masiva, DA-02)

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

## A-04 · Cancelar no cambia nada

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

## A-05 · Sin selección: ningún botón propio se dibuja (AUSENTE, no "deshabilitado")

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

## A-06 · Aprobar, como Supervisor — y el cierre automático de la Solicitud

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

## A-07 · El plugin manda, no el botón (DA-07)

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

## A-08 · Rechazada en AS400 — el diálogo de motivo

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

## A-09 · Cancelar el diálogo de motivo

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

## A-10 · Anular y Devolver

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

## A-11 · Atendido y Descartar, sobre Solicitudes

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

## A-12 · Revisado

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

## A-13 · Multiidioma de los botones

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

## A-14 · Atendida, sobre Fila (fase 2): apaga el cerrojo del bot sin cambiar de estado

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

## A-15 · La lección del botón fantasma: distinguir REGLAS de METADATA COMPILADA

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

## Resultados

Última corrida real: `python3 herramientas/pruebas/correr.py A`, 2026-09-27. **6 OK · 2 FALLA · 6
N-A.** Los 2 `FALLA` son defectos reales confirmados (no fallas de datos de prueba); ninguno tiene
ID todavía en `diseno/PENDIENTES.md` — pendiente de abrir. Formato por caso: OK / FALLA (con el
motivo o el ID del defecto) / N-A (con el motivo) — CONVENCIONES §3. **A-15** se agregó el
2026-09-28 (fusión del ex `H-06`, `13-construido-hoy.md`); su resultado (`N-A`, a propósito) es el
mismo que ya tenía como `H-06`.

| Caso | Resultado | Notas |
|---|---|---|
| A-01 los botones aparecen | N-A | GUI, arnés escrito, falta correrlo con sesión real |
| A-02 genéricos ocultos | N-A | GUI, sin caso de arnés todavía |
| A-03 Digitada masiva | OK | |
| A-04 cancelar | N-A | GUI, sin caso de arnés todavía |
| A-05 sin selección | N-A | GUI, arnés escrito, falta correrlo con sesión real |
| A-06 Aprobar | OK | incluye el cierre automático a Procesada |
| **A-07 el plugin manda** | OK | rechazos por rol + lote mixto, sobre datos propios |
| A-08 diálogo de motivo (Rechazada en AS400) | OK | corregido: `sanic_motivo` no lo exige la plataforma, solo el formulario |
| A-09 cancelar diálogo | N-A | GUI, sin caso de arnés todavía |
| A-10 Anular y Devolver | OK | |
| A-11 Atendido y Descartar | **FALLA** | defecto real: `AvisosDataverse.AvisarAEjecutivos` — `InvalidCastException` (ver el caso arriba). Falta ID en PENDIENTES.md |
| A-12 Revisado | OK | |
| A-13 multiidioma | N-A | GUI, sin caso de arnés todavía |
| A-14 Atendida (fase 2) | **FALLA** | defecto real: lista blanca de Fila no incluye `sanic_requiereatencionmanual` (ver el caso arriba). Falta ID en PENDIENTES.md |
| A-15 diagnóstico REGLAS vs. METADATA COMPILADA | N-A | ex `H-06`; maker portal (System Administrator/Customizer de plataforma, NO `admin_tecnico`), manual — motivo completo en el caso |
