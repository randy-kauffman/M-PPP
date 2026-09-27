# 06 — Seguridad, sitemap, vistas y formularios

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

## G-00 · Los privilegios efectivos, antes de tocar la app

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

## G-00b · El comportamiento de cada rol, contra el entorno

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

## G-01 · El sitemap: 4 áreas y 15 entradas

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

## G-02 · Los íconos del sitemap

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

## G-03 · Las 20 vistas existen y filtran

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

## G-04 · La cartera del ejecutivo (RF-19, D-13)

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

## G-05 · Perfil de seguridad de columna

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

## G-06 · Lo que cada rol NO puede

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

## G-07 · Cada rol entra donde debe

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

## G-08 · Los 10 formularios

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

## G-09 · La vista "Autorizaciones sin evidencia"

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

## G-10 · La campana de notificaciones (DA-08)

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

## G-11 · Multiidioma de la app

**Qué prueba.** La convención: etiquetas en 1033 y en el idioma del cliente.

**Necesita.** Un usuario con el idioma cambiado a inglés (Configuración personal → Idiomas);
cualquiera de las 4 sesiones alcanza.

**Cómo.** Cambiá el idioma del usuario a inglés y recorré el sitemap y un par de formularios.

**Esperado.** Las etiquetas del sitemap, las vistas y las columnas cambian de idioma. Ninguna
queda con el texto del otro idioma ni con el nombre lógico crudo.

**Automatizable.** GUI.

---

## G-12 · La app valida limpia

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

## Resultados

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
