# 05. Diseño de la app model-driven

Proyecto: 2026-001-referencias-planes-pago · Etapa 3 · Estado: **decisiones DA-01 a DA-09 y los seis supuestos aprobados por el aprobador el 2026-09-20; mockup revisado y aprobado ese mismo día** (`PENDIENTES.md` D-1: pidió la navegación entre Cliente, Plan y correo Autorizado, ya incorporada; "aparte de eso, lo miro todo bien"). Nada de esto está construido. *(Corregido el 2026-09-22: esta línea decía "falta su opinión sobre el mockup", que contradecía a D-1.)*

Reglas citadas: BP-PP-091, 093, 094, 095, 100, 186. Requisitos: RF-03, RF-11 a RF-14, RF-19, RNF-04. Decisiones: D-12, D-13, `02` DD-07, DD-14. Referencia estética: `anexos/referencia-dataverse-accelerator.md`.

## 0. Decisiones de este documento

| ID | Decisión | Motivo |
|---|---|---|
| DA-01 | **Enfoque híbrido** (aprobado 2026-09-20): model-driven nativa y limpia para todo, más **páginas custom solo donde la interfaz nativa no alcanza**. | La estética de la Dataverse Accelerator sale de no usar la interfaz genérica; pero cada página custom es tecnología canvas, cuesta mucho más que una vista, y exige resolver a mano accesibilidad y delegación (BP-PP-100). El plazo de la fase 1 es el cierre de septiembre. |
| DA-02 | **Las transiciones se disparan con botones propios en la barra de comandos de la grilla de Filas, sobre varias filas seleccionadas a la vez.** Los que no piden dato (Digitada, Aprobar) actúan directo; los que piden motivo (Rechazada en AS400, Anular, Devolver) abren un **diálogo**. | Son unas 7.500 filas por mes: abrir cada una para cambiarle el estado es inviable; el comando recibe las filas seleccionadas y la acción masiva es nativa. **Corregido el 2026-09-22 (aprobador):** los botones son **clásicos, con un web resource JavaScript**, no modernos con Power Fx. Motivo: un comando moderno **no se puede crear por API** — Learn solo documenta el diseñador visual —, y eso sacaba 18 de los 56 componentes del ciclo de construcción. Con `RibbonDiffXml` + JS se construyen por `ImportSolution` y quedan versionados en el repo. La acción masiva no se pierde: JavaScript recibe las filas seleccionadas por `SelectedControlSelectedItemIds`, igual que Power Fx por `Self.Selected.AllItems`. |
| DA-03 | **El diálogo de motivo es el formulario de creación rápida de una tabla técnica**, `sanic_mppp_tbl_motivoaccion`: un texto obligatorio y los botones nativos de guardar y cancelar. El comando lo abre con `Xrm.Navigation.navigateTo({pageType:"entityrecord", entityName:"sanic_mppp_tbl_motivoaccion"}, {target:2})`, lee el motivo del registro que devuelve y lo aplica a TODAS las filas seleccionadas. **No hay ninguna página custom en la fase 1.** | **Corregida el 2026-09-22 con evidencia de Learn.** Decía "una página custom pequeña abierta como diálogo", y eso **no puede funcionar con acción masiva**: (1) `navigateTo` a una página custom resuelve el promise **sin ningún valor** al cerrar el diálogo — *"An object is passed only if the `pageType` = `entityRecord` and you opened the form in create mode"* ([navigateTo](https://learn.microsoft.com/power-apps/developer/model-driven-apps/clientapi/reference/xrm-navigation/navigateto)) —, así que la página no tiene cómo devolverle el motivo al comando; y (2) el `pageInput` de una página custom solo admite `entityName` y `recordId`, y `recordId` **debe ser un GUID** porque viaja en la URL: no hay forma de pasarle las 25 filas seleccionadas. DA-02 (acción masiva) y la página custom eran **incompatibles entre sí**. En cambio `pageType: "entityrecord"` en modo create **sí** devuelve `savedEntityReference[0]` con `id` y `name`, y admite `data` para prellenar columnas. Además saca de la fase 1 el único componente que no se podía construir por API. |
| DA-04 | **La "bandeja de trabajo" como página custom completa** —lista de solicitudes a la izquierda, filas a la derecha, detalle en panel lateral, al estilo de la Accelerator— **queda para la versión 1.1**, después de la fase 1. | Es donde más rinde una página custom, pero es también lo más caro de la app. La fase 1 sale con la grilla nativa y sus comandos, que ya resuelven la acción masiva. |
| DA-05 | **Solo controles modernos. El Creator Kit no se usa para nada.** | Decisión del aprobador: **ninguna dependencia externa** por ahora. El Creator Kit no tiene soporte oficial de Microsoft y obligaría a instalar `CreatorKitCore` en Test y Prod antes que la solución. |
| DA-06 | **Se ocultan todos los comandos genéricos que no se usan**, en **todas** las tablas de la app — las de hoy y las que se agreguen después, sin excepción salvo una tabla técnica sin grilla propia (ver nota abajo): Eliminar, Asignar, Compartir, Flujo, Enviar vínculo, Combinar, y "Nuevo" donde el alta no es de una persona (Solicitud, Fila, ResultadoRegla, Bitácora). | Es lo que más ensucia una model-driven y lo más barato de arreglar. Además, varios de esos comandos ofrecen acciones que la matriz de privilegios no permite. |
| DA-08 | **Notificaciones dentro de la app desde la fase 1** (campana de la model-driven, tabla nativa `appnotification`). Las crea el código de servidor, no los botones: la Custom API de validación y el plugin posterior a la transición. **Una por solicitud y por tanda, nunca una por fila.** **Alcance recortado a DOS avisos, decisión del aprobador del 2026-09-28** (ver la nota de corrección abajo): "Tenés filas por digitar" (al ejecutivo dueño del cliente, cuando su solicitud deja filas por digitar) y "Tenés filas por aprobar" (a los supervisores, cuando una solicitud pasa a tener filas por aprobar). | Decisión del aprobador. Cada aviso abre la vista o la solicitud que corresponde. Es nativo: no agrega dependencias ni licencias. |
| DA-09 | **Toda pantalla se valida primero en un mockup HTML 100 % funcional** (`diseno/mockups/`), con datos ficticios, antes de construir nada. | Estándar del aprobador para cualquier Power App (BP-PP-102): probar la interfaz y dar opinión cuesta minutos en un mockup y días en la plataforma. |
| DA-07 | **La lógica no vive en los botones.** Un botón solo cambia `sanic_estado` (y `sanic_mensaje` o `sanic_notainterna`, según a quién vaya dirigido el texto); que la transición sea válida, quién puede hacerla y la segregación de funciones los decide el plugin (`03` §4). | BP-PP-051. Un usuario que llame al Web API sin pasar por la app encuentra las mismas reglas. La visibilidad de cada botón según el rol es comodidad, no seguridad. |

> **Nota de corrección, 2026-09-28 (D-69).** DA-06 estaba escrito como una **lista de tablas**, no
> como una regla: cuando se redactó, `AutorizacionCliente` no existía todavía (nació el 2026-09-27,
> DN-01). La tabla entró a la app y a la solución, pero `playbooks/importacion/comandos.md` nunca se
> actualizó — nadie se acordó, y nada lo decía —, así que sus comandos genéricos ("Flujo", "Enviar
> vínculo" y compañía) siguieron a la vista. Lo detectó el caso `A-02` del arnés de navegador
> (`pruebas/05-app-y-comandos.md`), el primero que mira el DOM real y no la definición del ribbon.
>
> **Se cerró en dos capas.** La visible: se agregó el bloque `ribbon` que le faltaba a
> `AutorizacionCliente` en `comandos.md`, con el mismo `ocultar` que su hermana `AutorizacionPlan`
> (misma matriz de privilegios, `04-matriz-privilegios.md` línea 43; el alta SÍ es de una persona,
> así que "Nuevo" no se oculta, a diferencia de Solicitud/Fila/ResultadoRegla/Bitácora). La de fondo:
> **"todas las tablas de la app" ahora se GARANTIZA, no se espera que alguien se acuerde.**
> `comandos.md` declara la clave opcional `app` (el `uniquename` de la app) y
> `importar_solucion.py --solo-verificar` compara, contra `RetrieveAppComponents` (la plataforma, no
> lo que el playbook dice tener), la lista real de tablas de la app contra las que sí tienen bloque
> `ribbon`; una tabla que falte sale como `"la tabla X está en la app y no tiene bloque de ocultos"`
> y el comando **deja de salir en verde** (código de salida distinto de 0) hasta que se agregue. La
> única excepción admitida es `sanic_mppp_tbl_motivoaccion` (`tablas_excluidas_de_ocultar`): una
> tabla técnica que solo se abre en el diálogo de creación rápida del motivo (DA-03), nunca como
> grilla con barra de comandos — no tiene un `ribbon` que ocultar porque nunca dibuja uno.
>
> **Se evaluó armar el playbook entero desde la lista de tablas de la app y se descartó.** Los
> botones propios (Fila, Solicitud) son decisiones de diseño por tabla — qué función JS, qué ícono,
> qué regla de habilitación — que ningún generador podría inventar sin criterio humano; automatizar
> solo la MITAD del archivo (el `ocultar`) y dejar la otra mitad manual habría hecho el playbook más
> difícil de auditar (una parte generada, otra escrita a mano, en el mismo bloque JSON), sin resolver
> el problema real: lo que falló no fue que el `ocultar` estuviera mal escrito, fue que la tabla
> entera no tenía bloque. Eso es exactamente lo que la denuncia de `--solo-verificar` resuelve, sin
> tocar cómo se escribe el playbook.
>
> **OJO, esto cierra la CAPA del playbook, no todo el caso `A-02`.** Reimportada la solución,
> `importar_solucion.py` confirma contra el entorno que el `RibbonDiffXml` de `AutorizacionCliente`
> está bien definido y publicado — pero la corrida real de `A-02` (`pruebas/05-app-y-comandos.md`)
> sigue en rojo para esa tabla: "Flujo" y "Enviar vínculo" siguen dibujándose. Es el mismo defecto de
> **metadata compilada del ribbon** que costó dos días con DN-02 (`06-inventario-componentes.md` ítem
> 5): una tabla que estrena personalización de ribbon necesita, además de publicar, un "Regenerate
> ribbon metadata" manual desde el Command Checker — con privilegio de plataforma, no por API. Las
> otras 10 tablas ya tenían esa metadata regenerada de antes; `AutorizacionCliente` es la primera vez
> que la necesita. Sigue pendiente de ese paso manual; no es un defecto del playbook ni de
> `importar_solucion.py`.

> **Nota de corrección, 2026-09-28 (D-64, decisión del aprobador).** DA-08 nombraba **cinco** avisos: al ejecutivo del cliente
> cuando su solicitud deja filas por digitar; a los supervisores cuando pasa a tener filas por aprobar; al ejecutivo que
> digitó cuando le devuelven filas; a todos los ejecutivos cuando un correo va a Por clasificar; y a ejecutivos y
> supervisores cuando una solicitud queda para revisar. El 2026-09-27 el set G (caso G-10) encontró que, de los cinco,
> **cuatro no estaban implementados**: `Datos/Avisos.cs` exponía un solo método público (`AvisarAEjecutivos`) con un solo
> llamador en todo el repositorio (`Api/ClasificarCorreo.cs`) — el aviso "a todos los ejecutivos, cuando un correo va a
> Por clasificar". Ni `ValidarSolicitud.cs` ni ningún step posterior a la transición llamaban a `IAvisos` para nada más.
>
> **El aprobador decidió construir DOS de los cuatro que faltaban** — "Tenés filas por digitar" y "Tenés filas por
> aprobar" — **y NO construir los otros tres** ("le devuelven filas", "correo va a Por clasificar" como aviso nuevo, y
> "solicitud para revisar"): el menú ya tiene vistas propias que cubren esos tres casos (Fila "Mis clientes — devueltas",
> Solicitud "Por clasificar" y Solicitud "Para revisar"; sus columnas se ampliaron el 2026-09-27, `06` §12.2), y **un
> aviso que duplica una vista que el usuario ya mira es ruido**: el ruido entrena a la gente a ignorar la campana. Estos
> tres avisos quedan **decididos a propósito como NO implementados**, no olvidados.
>
> **El aviso "a todos los ejecutivos, cuando un correo va a Por clasificar" es la única excepción**, y sigue existiendo:
> es el que YA estaba construido, bajo el plan original de cinco, antes de esta decisión — `AvisarAEjecutivos`, llamado
> desde `ClasificarCorreo.cs` cuando un correo no se procesa (ilegible, respuesta, o remitente no reconocido). Recortar
> el alcance de los avisos que faltaban por construir no es motivo para retirar código ya construido y probado: sigue
> funcionando tal cual, sin ampliarse. Estuvo bloqueado por D-59 (`InvalidCastException` en `systemuserroles`) y después
> por D-65 (faltaba `prvReadappnotification`); con las dos cerradas, crea su notificación de verdad.
>
> **Regla dura que se mantiene sin cambios**: una notificación por solicitud y por tanda, nunca una por fila.

## 1. La app

`MDA - MPPP - Mantenimiento PPP` (`sanic_mppp_mda_mantenimientoppp`). Una sola app para los cuatro perfiles (D-12); cada uno ve solo las entradas del sitemap cuyos datos puede leer.

## 2. Sitemap: un área, por tareas

| Grupo | Entrada | Qué abre | Quién la ve |
|---|---|---|---|
| **Trabajo** | Por digitar | Vista de Filas en estado Validada | Ejecutivo |
| | **Devueltas** | Vista de Filas que el Supervisor devolvió (Validada con mensaje de devolución) | Ejecutivo |
| | Por aprobar | Vista de Filas en estado Digitada | Supervisor |
| | **Para atención manual** | Vista de Filas con `sanic_requiereatencionmanual` = sí. Existía desde la fase 2 pero **no tenía entrada propia en el sitemap** — un olvido, no una decisión: su hermana conceptual del lado Solicitud, *Para revisar*, sí la tenía. Corregido el 2026-09-27 (hallazgo del aprobador); reúsa el ícono de *Para revisar*, mismo patrón de negocio | Ejecutivo |
| | **Por clasificar** | Vista de Solicitudes en estado No reconocida o No es correo nuevo, con su motivo | Ejecutivo |
| | Para revisar | Vista de Solicitudes con `sanic_requiererevision` = sí | Ejecutivo, Supervisor |
| **Consulta** | Solicitudes | Todas las Solicitudes | Ejecutivo, Supervisor |
| | Filas | Todas las Filas | Ejecutivo, Supervisor |
| **Catálogos** | Clientes · Planes · Correos Autorizados · Autorizaciones - Planes · Autorizaciones - Clientes | Sus vistas. Autorizaciones - Planes (AutorizacionPlan) trae la vista **"Sin evidencia"**: el respaldo que el Administrador de planes tiene pendiente de cargar. Es una lista de tareas suya, **no** una lista de autorizaciones inválidas: esas autorizaciones ya valen (D-42). Autorizaciones - Clientes (AutorizacionCliente) es la entrada nueva del 2026-09-27, pedido textual del aprobador de diferenciar las dos vías de DN-01 en el sitemap; reúsa el ícono de Autorizaciones - Planes, no se creó uno nuevo | Administrador de planes (Ejecutivo y Supervisor, solo lectura) |
| **Configuración** | Parámetros · Reglas | Sus vistas | Administrador técnico |

Un solo nivel, nombres de tarea y no de tabla, íconos Fluent en SVG (web resources `WR - MPPP - SVG - …`). Sin tableros en la fase 1. La entrada por defecto de cada perfil es su primera entrada de Trabajo.

## 3. Vistas

Columnas pensadas para digitar sin abrir el registro. Las protegidas (identificación y cuenta) se ven porque Ejecutivo y Supervisor están en el perfil de seguridad de columna.

| Vista (tabla) | Filtro | Columnas | Orden |
|---|---|---|---|
| **Mis clientes — por digitar** (Fila) · *por defecto del Ejecutivo* | Estado = Validada **y** Plan → Cliente → propietario = usuario actual (RF-19, D-13) | Solicitud, n.º de fila, **Requiere atención manual**, Gestión, Clasificación, Plan, Referencia, Nombre, Tipo ID, Identificación, Cuenta, Moneda, Banco, Validada el | Solicitud, n.º de fila |
| Todos — por digitar (Fila) | Estado = Validada | las mismas + Cliente | ídem |
| **Por aprobar** (Fila) · *por defecto del Supervisor* | Estado = Digitada | las mismas + Digitada por, Digitada el | Digitada el |
| Mis clientes — devueltas (Fila) | Estado = Validada, con mensaje de devolución | + Mensaje | |
| Terminadas (Fila) | Estado terminal | + Estado, Mensaje, Aprobada por | más recientes primero |
| **Por clasificar** (Solicitud) | Estado = No reconocida o No es correo nuevo | Recibido el, Remitente, Asunto, **Motivo**, Adjuntos, Vence el | más viejos primero |
| Para revisar (Solicitud) | `requiererevision` = sí | Número, Remitente, Estado, Motivo de revisión | |
| Solicitudes (Solicitud) | todas | Número, Recibido el, Remitente, Estado, Filas totales / válidas / rechazadas, Cerrada el | más recientes primero |
| Catálogos y configuración | activos | las de negocio de cada tabla | |

### Fase 2: dos vistas y un comando más (2026-09-25)

Las entra el RPA y no existen hasta la fase 2, así que **no cuentan en las 17 de abajo**:

| Vista | Tabla | Filtro | Columnas | Para qué |
|---|---|---|---|---|
| Para atención manual | Fila | `sanic_requiereatencionmanual` = sí | Solicitud, N.º fila, Plan, Beneficiario, **Nota interna** | El bot se topó con algo que no sabe resolver y la fila pasó a ser de un ejecutivo. Mismo patrón que *Para revisar* en Solicitud |
| Tomadas por el bot | Fila | `sanic_claimhasta` en el futuro | Solicitud, N.º fila, Estado, **Tomada por el bot hasta** | Saber qué está en manos del bot y hasta qué hora, sin preguntarle a nadie (`03` §2) |

**Comando nuevo: "Atendida"** sobre Fila, que apaga `sanic_requiereatencionmanual` — igual que *Revisado* apaga `sanic_requiererevision` en Solicitud, y sin pedir texto por el mismo motivo. La marca además **se apaga sola con cualquier transición exitosa** de la fila, así que el botón es para el ejecutivo que la resolvió **sin** cambiarla de estado.

**La columna "Tomada por el bot hasta" se agrega también a la grilla de Filas**, para que la reserva se vea sin cambiar de vista.

> **Corregido el 2026-09-27 (hallazgo del aprobador).** *Para atención manual* se construyó en Dev en
> la fase 2 pero **nunca se enganchó al sitemap**: un olvido de fase 2, no una decisión — su hermana
> conceptual del lado Solicitud, *Para revisar*, sí tenía su entrada propia (`subarea_pararevisar`).
> Se agregó `subarea_paraatencionmanual` en el área Trabajo, entre *Por aprobar* y *Por clasificar*
> (§2 de arriba), reusando el ícono `sanic_mppp_wr_svg_pararevisar` — mismo patrón de negocio, no se
> creó un web resource nuevo. Además, el aprobador señaló que en las vistas de trabajo no había forma
> de ver que una fila estaba marcada: se agregó la columna `sanic_requiereatencionmanual` (Sí/No,
> ancho chico) a **Mis clientes — por digitar**, **Todos — por digitar**, **Por aprobar** y **Mis
> clientes — devueltas** (§3 de arriba), segunda columna en las cuatro, pegada al n.º de fila y antes
> del resto, para que se vea de un vistazo. Por aprobar y devueltas se evaluaron y sí correspondía:
> `atencionmanual` (`03` §"Qué filas entran") vale también sobre una fila Digitada, y
> `sanic_notainterna` es la misma columna que usa tanto el bot al marcar atención manual como el
> supervisor al devolver una fila, así que sin la columna nueva esas dos vistas de trabajo tenían el
> mismo problema de fondo que motivó el hallazgo. **No** se agregó a Filas, Terminadas ni Tomadas por
> el bot: no son vistas de trabajo donde alguien levanta filas para actuar sobre ellas.
> Detalle de implementación a verificar antes de construir: filtrar *"claim vigente ahora"* en una vista guardada necesita un operador de fecha relativa de FetchXML (familia `next-x-hours`). **Se confirma en Learn al construir la vista**; si no diera, se muestra la columna y el ejecutivo lee la hora, que resuelve lo mismo.

### Lista cerrada de las 17 vistas (inventario 12.2), hoy 20

Cerrada el 2026-09-22 con el aprobador. Antes el inventario decía "~19" y la fórmula del párrafo de abajo daba 16: ninguno de los dos números se podía reconstruir. **Cada tabla de catálogo lleva UNA sola vista, "Activos".** La vista de búsqueda rápida (Quick Find) no se cuenta: toda tabla la trae y no hay nada que diseñar en ella.

**Se agregó una 18.ª el 2026-09-27** (fila 18 de la tabla de abajo): DN-01 creó `AutorizacionCliente`
y nunca se mostró en ningún formulario; orden explícita del aprobador de agregarle su subgrilla al
Cliente. **Y dos más, 19.ª y 20.ª, ese mismo día**: los tres pedidos textuales del aprobador (ver
`PENDIENTES.md`/la orden del 2026-09-27) — poder ver desde el correo con cuántos CLIENTES está
relacionado (19), y diferenciar "Autorizaciones - Planes" de "Autorizaciones - Clientes" en el
sitemap, para lo cual la segunda necesita su propia vista por defecto (20). La lista deja de estar
"cerrada" en el número 17, pero el criterio que la cerró (una vista por necesidad real de la app, no
una vista "por las dudas") no cambia.

| # | Vista | Tabla | Para qué |
|---|---|---|---|
| 1 | Mis clientes — por digitar | Fila | entrada de sitemap "Por digitar" (Ejecutivo) |
| 2 | Todos — por digitar | Fila | la misma pantalla, sin el filtro de cartera |
| 3 | Por aprobar | Fila | entrada de sitemap "Por aprobar" (Supervisor) |
| 4 | Mis clientes — devueltas | Fila | entrada de sitemap "Devueltas" (agregada el 2026-09-22) |
| 5 | Terminadas | Fila | consulta |
| 6 | Por clasificar | Solicitud | entrada de sitemap |
| 7 | Para revisar | Solicitud | entrada de sitemap |
| 8 | Solicitudes | Solicitud | entrada de sitemap "Solicitudes" |
| 9 | Clientes activos | Cliente | entrada de sitemap "Clientes" |
| 10 | Planes activos | Plan | entrada de sitemap "Planes" |
| 11 | Autorizados activos | Autorizado | entrada de sitemap "Autorizados" |
| 12 | Autorizaciones activas | AutorizacionPlan | entrada de sitemap "Autorizaciones - Planes" |
| 13 | Parámetros activos | Parametro | entrada de sitemap "Parámetros" |
| 14 | Reglas activas | Regla | entrada de sitemap "Reglas" |
| 15 | Autorizaciones sin evidencia | AutorizacionPlan | pendientes del Administrador de planes. **No** son autorizaciones inválidas (D-42) |
| 16 | Correos autorizados sobre este plan | AutorizacionPlan | subgrilla del formulario de Plan |
| 17 | Planes que puede modificar | AutorizacionPlan | subgrilla del formulario de Autorizado |
| 18 | Correos autorizados sobre este cliente | AutorizacionCliente | subgrilla del formulario de Cliente (agregada 2026-09-27, DN-01) |
| 19 | Clientes que puede modificar | AutorizacionCliente | subgrilla del formulario de Autorizado (agregada 2026-09-27, pedido textual del aprobador) |
| 20 | Autorizaciones por cliente activas | AutorizacionCliente | entrada de sitemap "Autorizaciones - Clientes" (agregada 2026-09-27, pedido textual del aprobador) |

Las 16 y 17 son la misma tabla vista desde cada lado, como dice la nota de navegación de abajo. La
18 y la 19 también: `AutorizacionCliente` ya tiene su espejo completo en Autorizado, agregado el
2026-09-27 (el playbook de tabla la dejaba "fuera de alcance" el 2026-09-26; el aprobador la pidió al
día siguiente, textual: "si quiero entrar por correo y ver con cuántos clientes está relacionado ese
correo, ¿cómo lo hago? Hoy por hoy sí puedo ver los planes, pero no los clientes").

**Vista por defecto de cada tabla** (la que usa la plataforma en subgrillas y lookups cuando nadie elige otra): Fila → *Mis clientes — por digitar*; Solicitud → *Solicitudes*; AutorizacionPlan → *Autorizaciones activas*; AutorizacionCliente → *Autorizaciones por cliente activas*; y cada catálogo, su propia "Activos".

La vista "mis clientes" filtra por una relación de dos saltos (Fila → Plan → Cliente). Las vistas nativas lo admiten en su FetchXML; hay que comprobar al construir que el diseñador moderno no lo rompe al editarla.

## 4. Formularios

Uno principal por tabla, **una sola pestaña**, sin pestaña de "Relacionados" visible donde no aporta.

| Tabla | Secciones | Editable |
|---|---|---|
| **Fila** | Encabezado: Estado, Solicitud, n.º de fila. *Gestión* (los diez datos de la plantilla, más la referencia recibida). *Resultado* (Mensaje). *Trazabilidad* (validada, digitada por y cuándo, aprobada por y cuándo). | **Todo de solo lectura.** El estado se cambia con los botones; la lista blanca de columnas del plugin rechazaría cualquier otra edición (`04` §1). |
| **Solicitud** | Encabezado: Número, Estado, Remitente. *Correo* (recibido, asunto, archivos: correo original y Excel, para descargar). *Filas* (subgrilla con las filas y los mismos botones). *Reglas* (subgrilla de ResultadoRegla). *Comunicaciones* (acuse y respuesta final: cuándo y qué se dijo). *Bitácora* (subgrilla). | Solo lectura. |
| **Cliente** | Nombre, CIFBAC, CIFCOM, Ejecutivo asignado. Subgrilla **Planes del cliente** (código, moneda, tipo de formato, cantidad de correos autorizados, estado). Subgrilla **Correos autorizados sobre este cliente** (correo, estado), sobre `AutorizacionCliente` — agregada el 2026-09-27, DN-01, orden explícita del aprobador. *(No es la misma subgrilla que se quitó el 2026-09-24, "Correos autorizados del cliente": esa mostraba `Autorizado` por un lookup directo a Cliente que ya no existe, "un correo ya no pertenece a un cliente". Esta es la tabla de unión `AutorizacionCliente` de DN-01 — un correo autoriza TODOS los planes del cliente a la vez —, que se creó el 2026-09-26 y no se había mostrado en ningún formulario todavía.)* | Administrador de planes |
| **Plan** | Código, Cliente (el lookup lleva al cliente), Moneda, Tipo de formato. Subgrilla **Correos autorizados sobre este plan** (correo, evidencia, fecha del documento, estado), sobre AutorizacionPlan; cada renglón abre esa autorización. | Administrador de planes |
| **Autorizado** | Correo, Cliente (el lookup lleva al cliente). Subgrilla **Planes que puede modificar** (plan, moneda, tipo de formato, evidencia, fecha del documento, estado), sobre AutorizacionPlan; cada renglón abre esa autorización. Subgrilla **Clientes que puede modificar** (cliente, CIF BAC, estado), sobre `AutorizacionCliente` — agregada el 2026-09-27, pedido textual del aprobador ("si quiero entrar por correo y ver con cuántos clientes está relacionado ese correo, ¿cómo lo hago? Hoy por hoy sí puedo ver los planes, pero no los clientes"). Desde ahí el Administrador de planes agrega una autorización nueva (solo planes del mismo cliente, o clientes enteros) o la desactiva; no se borra. **El Autorizado ya no lleva evidencia propia** (E-17). | Administrador de planes |
| **Autorización** (AutorizacionPlan) | Correo, Plan y Cliente (los tres lookups navegables). Sección **Evidencia de la autorización**: Documento firmado (`sanic_documentofirmado`, control nativo de archivo: cargar, descargar, reemplazar y quitar; un archivo, hasta 10 MB) y Fecha del documento. **La evidencia NO condiciona la vigencia** (D-42, aprobador 2026-09-21; `02` §2.4): una autorización vale con el Autorizado, el Plan, el Cliente y la propia AutorizacionPlan activos, tenga o no el documento cargado. La evidencia es el respaldo que el administrador de planes tiene que subir, y el formulario se lo muestra pendiente con una notificación, pero **al cliente no se lo afecta porque el banco no cargó un papel**. El archivo solo se puede cargar después de guardar el registro (así funciona una columna de archivo), así que toda autorización nace "sin evidencia" y eso es normal. *(Corregido el 2026-09-22: esta celda decía "la evidencia es obligatoria" y que la autorización "no vale" hasta cargarla, que es justo la validación que D-42 mandó sacar.)* Ejecutivo y Supervisor la pueden descargar, no cambiar (`04` §1). | Administrador de planes |
| **Autorización por cliente** (AutorizacionCliente) | Correo, Autorizado y Cliente (`sanic_nombre`, `sanic_autorizadoid`, `sanic_clienteid`, los tres en el encabezado, como en AutorizacionPlan). Sección **Evidencia de la autorización**: Documento firmado (`sanic_documentofirmado`, archivo hasta 10 MB) y Fecha del documento (`sanic_fechadocumento`), igual que en AutorizacionPlan. Formulario nuevo, agregado el 2026-09-27, pedido textual del aprobador ("Quiero agregar un nuevo correo autorizado al cliente y solo me sale 'Nombre' y 'Owner'"): antes solo existía el formulario "Information" de fábrica, sin los lookups. *(Corregido el 2026-09-27, misma jornada: esta celda decía "**Sin sección de evidencia**: esta tabla no tiene `sanic_documentofirmado` ni `sanic_fechadocumento` (DN-01)" y describía una sección técnica "Datos" con `ownerid` puesta solo para cumplir el mínimo de la herramienta. Las dos cosas dejaron de ser ciertas el mismo día: el aprobador decidió que la autorización por cliente lleva el mismo respaldo firmado que la de plan — "no está la columna de archivo adjunto para adjuntar la autorización por escrito y la fecha de ese documento (al igual que en autorización por plan)" —, se agregaron las dos columnas, y la sección "Datos" se quitó porque ya hay una sección con campos de negocio reales. El motivo de negocio: una autorización por cliente habilita TODOS los planes de ese cliente de una vez, o sea que es MÁS amplia que la de plan; no tiene sentido que exija MENOS respaldo. **Esta celda vieja hizo que el caso G-08 del set de seguridad acusara al sistema de un defecto que no existía**: verificaba contra el documento, y el documento mentía.)* | Administrador de planes |
| **Parámetro** · **Regla** | Sus columnas; el valor JSON en un área de texto grande. | Administrador técnico |

**Navegación entre catálogos** (pedido del aprobador sobre el mockup, 2026-09-20). Desde un Cliente se ven sus planes y sus correos autorizados; desde un Plan se viaja a su cliente y se ven los correos autorizados sobre él; desde un Autorizado se ven los planes que puede modificar. Desde una Fila, el Plan y el Cliente son enlaces. Todo con lo nativo: lookups y subgrillas; "Volver" regresa al registro anterior, no a la lista. La subgrilla de un Plan y la de un Autorizado muestran la misma tabla, AutorizacionPlan, vista desde cada lado: hacen falta dos vistas de esa tabla, una por lado (`06`).

## 5. Comandos

En la grilla principal de Filas, en la subgrilla de Filas de la Solicitud y en el formulario de Fila. Visibles según rol y estado; **la validación real es del plugin** (DA-07).

| Botón | Quién lo ve | Sobre filas en | Pide | Efecto |
|---|---|---|---|---|
| **Digitada** | Ejecutivo | Validada | confirmación con la cantidad | Estado → Digitada |
| **Rechazada en AS400** | Ejecutivo | Validada o Digitada | motivo (diálogo) | Estado → Rechazada en AS400 + mensaje |
| **Anular** | Ejecutivo | Validada o Digitada | motivo (diálogo) | Estado → Anulada + mensaje |
| **Aprobar** | Supervisor | Digitada | confirmación con la cantidad | Estado → Aprobada |
| **Devolver** | Supervisor | Digitada | nota interna (diálogo) | Estado → Validada + `sanic_notainterna`. **La nota NO sale en el correo al cliente** (2026-09-24) |
| **Atendido** · **Descartar** | Ejecutivo | Solicitud No reconocida o No es correo nuevo | confirmación | Estado → Cerrada · Descartada. Lo que nadie clasifica en 30 días pasa solo a Vencida (`07` DF-09) |
| **Revisado** | Ejecutivo, Supervisor | Solicitud con `requiererevision` | confirmación | Apaga `requiererevision`. El step `RevisionAtendidaStep` deja el **motivo que había escrito la máquina** en la Bitácora, con quién y cuándo, y limpia `sanic_motivorevision` |
| **Carga masiva** (DN-04, construido y verificado en Dev el 2026-10-01) | Administrador de planes | grilla principal de Cliente, Plan, Autorizado y AutorizacionCliente (no opera sobre la selección) | un `.xlsx`, hoja `CargaCatalogos` | `sanic_mppp_capi_cargarcatalogos`: crea Clientes, Planes, Correos y Autorizaciones por cliente, todo o nada; descarga el Excel de resultado y muestra el resumen |
| **Activar/Desactivar masivo** (DN-04, construido y verificado en Dev el 2026-10-01) | Administrador de planes | ídem | un `.xlsx`, hoja `ActivarDesactivar` | `sanic_mppp_capi_cambiarestadocatalogos`: activa o desactiva registro por registro, todo o nada; ídem |

> **DN-04: los dos comandos de catálogos no siguen el patrón de confirmación de los de arriba.** Cada uno es un **menú desplegable** con *Cargar archivo* y *Descargar plantilla* (decisión del aprobador, 2026-10-01). Piden un archivo, no actúan sobre filas seleccionadas, y mientras trabajan muestran el indicador nativo `Xrm.Utility.showProgressIndicator` con un mensaje por etapa. El flujo exacto del JS y los textos del resumen están en `DIRECTRICES-NUEVAS.md` DN-04 §Interfaz.

> **Corregido el 2026-09-23.** Esta fila decía que Revisado pide una **nota**, y el comando la pedía y la escribía en `sanic_motivorevision`. Dos cosas estaban mal. La primera: esa columna **no es del operador** — la escriben `MPPP-VIG` y `MPPP-ENV` para explicar por qué hace falta revisar ("Validación fallida 3 veces", "Envío iniciado sin confirmar"), y es lo que el operador **lee** en la vista *Para revisar*; la nota se la habría comido. La segunda: la lista blanca de `04` §1 no admite esa columna de una persona, así que el botón **fallaba siempre**, y el plugin que esta tabla daba por hecho ("deja la nota en la Bitácora") **no existía**. Se resolvió como el **mockup aprobado el 2026-09-20**, que no pide texto. Evento nuevo de Bitácora: `Revision atendida` (159460021).

Una acción masiva puede fallar en algunas filas y en otras no (por ejemplo, el supervisor intenta aprobar una fila que él mismo digitó): el botón informa cuántas se aplicaron y cuáles no y por qué, y no deja nada a medias dentro de una misma fila.

## 6. Cómo trabaja la gente — confirmado por el aprobador el 2026-09-20

1. **Unidad de trabajo.** El ejecutivo trabaja una **lista plana de filas por digitar**, ordenada por solicitud, y no entra solicitud por solicitud. Tiene la Solicitud a un clic si quiere ver el correo.
2. **Acción masiva.** Selecciona varias filas y las marca juntas. El caso normal es "digité las 25 de esta plantilla". La grilla ofrece además **"Seleccionar todas" y "Desmarcar todas"** como comandos visibles, no solo la casilla del encabezado.
3. **Verificación del supervisor.** Le alcanza con las filas que extrajimos, y si duda **descarga el Excel original** desde la Solicitud. No hay visor del Excel dentro de la app en la fase 1.
4. **Por clasificar** (correos no reconocidos y correos que no son nuevos). Los ven **todos** los ejecutivos. Para decidir les alcanza con remitente, asunto y fecha, y si no, descargan el correo original.
5. **Catálogos.** El Administrador de planes carga **uno por uno en formularios**, y para cargas grandes usa ~~la importación nativa desde Excel de model-driven~~ **el comando "Carga masiva"** (DN-04), y para activar o desactivar en lote, **"Activar/Desactivar masivo"**. La migración inicial desde SharePoint es aparte (RF-16). *(Corregido el 2026-10-01, DN-04: el aprobador confirmó que la importación nativa "le queda corto". Carga una tabla por archivo, no entiende datos desnormalizados, no busca un cliente por CIF ni resuelve el ejecutivo por su correo, y no valida el archivo entero antes de grabar.)*
6. **Avisos.** **Notificaciones dentro de la app desde la fase 1** (DA-08): "Tenés filas por digitar" y "Tenés filas por aprobar" (alcance recortado a estos dos, nota de corrección 2026-09-28).

## 7. Inventario que sale de este diseño (completa `06` §12)

| Tipo | Cantidad | Detalle |
|---|---|---|
| App y sitemap | 1 + 1 | |
| Vistas | 20 | Lista cerrada en `05` §3 (`06` §12.2): 5 de Fila, 3 de Solicitud, activos + búsqueda de cada catálogo, y las 4 de AutorizacionPlan/AutorizacionCliente para subgrillas y "sin evidencia" |
| Formularios | 9 | Uno por tabla con interfaz (ResultadoRegla y Bitácora solo se ven en subgrilla); incluye `AutorizacionCliente`, agregado el 2026-09-27 |
| Comandos | 8 | Los de §5. **Hoy son 10 construidos** (fase 2 sumó *Atendida* y *Descargar para digitar*, `06` §12.5), **+2 construidos el 2026-10-01** (DN-04: *Carga masiva* y *Activar/Desactivar masivo*, en 4 grillas cada uno): **12** en total |
| Tabla técnica + su creación rápida | 1 + 1 | `sanic_mppp_tbl_motivoaccion`: el diálogo de motivo (DA-03 corregida) |
| Web resources | ~10 | Íconos SVG del sitemap y de los botones |
| Ocultamiento de comandos genéricos | 11 tablas | DA-06 (11 desde el 2026-09-28, D-69: `AutorizacionCliente` había quedado afuera) |

**Aviso sobre cómo se construye.** Vistas, formularios, sitemap y comandos se crean por Web API o por archivo de solución, así que **toda la fase 1 entra al ciclo de playbooks**. *(Corregido el 2026-09-22: acá decía que la página custom era el único componente que un agente no podía crear solo. Con DA-03 corregida ya no hay ninguna página custom, así que no queda ningún componente de la fase 1 fuera del ciclo. La bandeja de trabajo de DA-04 sigue siendo página custom, pero es versión 1.1.)*
