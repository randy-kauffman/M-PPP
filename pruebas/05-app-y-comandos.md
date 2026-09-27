# 05 — La app y sus 8 comandos

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

## A-01 · Los botones aparecen donde deben

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

## A-02 · Los comandos genéricos NO aparecen (12.6)

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

## A-03 · Digitada, sobre varias filas a la vez

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

## A-04 · Cancelar no cambia nada

**Qué prueba.** Que el diálogo de confirmación realmente frene.

**Necesita.** Sesión `ejecutivo`; filas seleccionables.

**Cómo.** Seleccioná filas, apretá **Digitada** y dale **Cancelar**.

**Esperado.** **Nada** cambia. Ninguna fila cambia de estado, no hay entradas nuevas en Bitácora.

**Automatizable.** GUI (sesión `ejecutivo`).

---

## A-05 · Sin selección

**Qué prueba.** El `EnableRule` `Mscrm.SelectionCountAtLeastOne`.

**Necesita.** Sesión `ejecutivo` (o cualquier rol con acceso a la vista).

**Cómo.** Sin seleccionar ninguna fila, mirá la barra.

**Esperado.** Los botones propios están **deshabilitados** (grises). Si se pudiera apretar, el
mensaje sería *"No hay ninguna fila seleccionada."*

**Automatizable.** GUI.

---

## A-06 · Aprobar, como Supervisor

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

## A-07 · El plugin manda, no el botón (DA-07)

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

## A-08 · Rechazada en AS400 — el diálogo de motivo

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

## A-09 · Cancelar el diálogo de motivo

**Qué prueba.** Que cerrar sin guardar **no cambie nada**.

**Necesita.** Sesión `ejecutivo`; filas seleccionadas.

**Cómo.** Apretá **Anular** sobre filas seleccionadas y cerrá el diálogo con la **X** (sin guardar).

**Esperado.** **Ninguna** fila cambia de estado. No hay registro de motivo colgado.

**Automatizable.** GUI.

---

## A-10 · Anular y Devolver

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

## A-11 · Atendido y Descartar, sobre Solicitudes

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

## A-12 · Revisado

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

## A-13 · Multiidioma de los botones

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

## Resultados

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
