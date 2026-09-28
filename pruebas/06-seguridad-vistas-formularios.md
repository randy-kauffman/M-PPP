# 06 — Seguridad, sitemap, vistas y formularios

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

## Por qué este set es el más delicado del plan

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

## De GUI a API: qué cambió y por qué

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

## Fase 1 + fase 2: la frontera con el set B (RPA)

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

## G-00 · Los privilegios efectivos, los 4 roles

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

## G-00b · El comportamiento de cada rol, contra el entorno

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

## G-01 · El sitemap: las 15 entradas, cada una a su vista exacta

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

## G-02 · Los íconos del sitemap

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

## G-03 · Las 20 vistas existen y filtran

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

## G-04 · La cartera del ejecutivo (RF-19, D-13)

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

**Automatizable.** API (`set_g.py:g04`, impersonación con `MSCRMCallerID`, sin navegador).

---

## G-05 · Perfil de seguridad de columna

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

## G-06 · (fusionado en G-00 / G-00b)

**Qué prueba.** La matriz de privilegios (`04`) y su comportamiento de rechazo.

**Por qué no es un caso aparte.** Las mismas celdas ("el Administrador de planes no ve Solicitudes ni
Filas", "el Ejecutivo no puede aprobar", "nadie edita catálogos fuera de su rol") ya las prueba G-00
(la matriz en sí) y G-00b (`comportamiento_roles.rechazos()`, que ejercita exactamente esos rechazos
contra el entorno). Escribir una tercera llamada que verifique lo mismo probaría la herramienta dos
veces, no el sistema dos veces (CONVENCIONES §2). El ID queda documentado acá, sin función en
`CASOS`, para que quien busque "G-06" en un documento viejo sepa dónde está ahora.

**Automatizable.** — (ver G-00, G-00b).

---

## G-07 · Cada rol entra donde debe

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

## G-08 · Los 10 formularios

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
- **Motivo de acción**: trae el campo `sanic_motivo`.

> ### Hallazgo — AutorizacionCliente SÍ trae la sección de evidencia (defecto confirmado, no N-A)
>
> `05-app-model-driven.md` §4 dice textual: *"Sin sección de evidencia: esta tabla no tiene
> `sanic_documentofirmado` ni `sanic_fechadocumento`, a diferencia de Autorización"* — y este mismo
> archivo, antes de esta corrección, repetía la misma afirmación. **Verificado en vivo el
> 2026-09-27, en dos niveles**:
>
> 1. La TABLA `sanic_mppp_tbl_autorizacioncliente` **sí tiene** las columnas
>    `sanic_documentofirmado` y `sanic_fechadocumento` (`EntityDefinitions(...)/Attributes`).
> 2. El FORMULARIO "Autorizacion por cliente" **sí las muestra**, en una sección etiquetada
>    "Evidencia de la autorizacion" con los campos "Documento firmado" y "Fecha del documento",
>    `disabled="false"` — visibles y editables, tal cual en AutorizacionPlan.
>
> Esto **contradice el diseño** (DN-01, aprobador: la vía por cliente autoriza TODOS los planes de
> un cliente a la vez y a propósito no lleva evidencia propia, porque no hay "un documento" por
> autorización sino por relación cliente-correo). Por CONVENCIONES §3 ("un defecto conocido no es
> N-A"), este caso queda escrito **contra el diseño**, así que hoy da **FALLA a propósito** — no se
> apaga ni se "arregla" acá. Falta que el aprobador decida y, si corresponde, se registre en
> `diseno/PENDIENTES.md` con su propio ID (fuera del alcance de este archivo: PENDIENTES.md no es de
> este set).

**FALLA si** falta alguno de los 10 formularios, si a Solicitud le falta alguna subgrilla, o si
`AutorizacionCliente` NO trae evidencia (ver el hallazgo de arriba: hoy SÍ la trae, así que el caso
falla).

**Automatizable.** API (`systemforms`; `set_g.py:g08`).

---

## G-09 · La vista "Autorizaciones sin evidencia" (D-42)

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

## G-10 · La campana de notificaciones (DA-08)

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

## G-11 · Multiidioma de la app

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

## G-12 · La app valida limpia

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

## G-13 · La frontera con el RPA: el rol no trae privilegios de negocio ni el perfil de columna

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

## Resultados

| Caso | Resultado | Notas |
|---|---|---|
| G-00 privilegios efectivos (4 roles) | | |
| G-00b comportamiento de cada rol | | |
| G-01 sitemap: 15 entradas, su vista exacta | | |
| G-02 íconos | | GUI, sin arnés |
| G-03 las 20 vistas | | |
| G-04 cartera del ejecutivo | | |
| **G-05 columnas protegidas** | | el más importante |
| G-06 (fusionado en G-00/G-00b) | | sin función propia |
| G-07 sitemap por rol | | GUI, sin arnés |
| G-08 los 10 formularios | | ver hallazgo AutorizacionCliente |
| G-09 sin evidencia pero válida (D-42) | | |
| G-10 notificaciones (DA-08) | | ver hallazgo appnotification |
| G-11 multiidioma | | GUI, sin arnés |
| G-12 ValidateApp | | |
| G-13 frontera con el RPA | | nuevo |
