# 10 — DN-01: autorización por cliente, con interruptor a plan

Cubre `autorizacion.modo` (`cliente`/`plan`, degradación a `plan` con basura), la tabla
`AutorizacionCliente`, el nombre calculado `<correo> → <cliente>`, la evidencia firmada
(`sanic_documentofirmado`/`sanic_fechadocumento`) y las subgrillas nuevas en Cliente y Autorizado.
Fuente: `diseno/DIRECTRICES-NUEVAS.md` DN-01, `diseno/02-diccionario-datos.md` §2.5,
`src/Sanic.Mppp.Plugins/Validacion/ReglasDeRegistro.cs` (`ModoDeAutorizacion`,
`ModoDeAutorizacionParametro`), `Datos/Catalogos.cs` (`PlanesAutorizadosDe`),
`Steps/ReglasDeSteps.cs`/`StepsDePlataforma.cs` (`NombreCalculado.DeAutorizacionCliente`).

Convención de IDs: prefijo `U-` (a**U**torización), ver el encabezado de `09-rpa-fase2.md`.

## Preparativos comunes a este archivo

Implementados en `herramientas/pruebas/sets/set_u.py::preparar()`. Automatizados por completo (Web
API): nada de esto se hace a mano.

- **`autorizacion.modo` es un parámetro de ENTORNO COMPARTIDO, pero NO se restaura caso por caso.**
  CONVENCIONES §8.2 reemplaza esa idea: el set no restaura lo que encontró, lo lleva a su **canon**
  (`plan`), y eso lo hace `correr.py` — automáticamente, antes y después de CADA set, sea cual sea —
  no cada caso ni cada set por separado. Lo único que le toca a `set_u.py` es que cada CASO que
  necesita un valor puntual de `autorizacion.modo` lo fija ÉL MISMO al empezar (nunca asume el valor
  que dejó el caso anterior): así el set da el mismo resultado si se corre entero o caso por caso.
- El fixture compartido `PR*` (`preparar_datos.py`: Cliente `PR - Pruebas MPPP SA`, planes `PR11`/
  `PR06`/`PR10`/`PRIN`/`PRNA`) y el correo `REMITENTE = zz-corredor-fase1@example.com` (el mismo que
  usan los sets C/S/R), autorizado por `AutorizacionPlan` — **sin ninguna `AutorizacionCliente`**:
  es justo lo que U-03 necesita.
- **`REMITENTE_SOLOCLIENTE = zz-corredor-set-u-dn01-cliente@example.com`**: Autorizado propio de
  este set, con UNA SOLA vía de autorización — `AutorizacionCliente` hacia el Cliente compartido —,
  CERO `AutorizacionPlan`. Para U-02, U-04(a), U-09 y U-11.
- **Un segundo Cliente "ajeno" (`ZZP-Cliente ajeno DN-01 (set U)`), con su propio Plan activo
  (`ZZU1`), y un tercer correo propio (`REMITENTE_OTRO_CLIENTE`) autorizado SOLO por ese cliente
  ajeno.** Hace falta para dos cosas que el diseño original de este archivo no anticipaba:
  - **U-06** necesita comparar el mensaje al cliente citando el MISMO plan en los dos modos, sin que
    ninguna de las dos Solicitudes quede "No reconocida" (ver la nota de D-59 más abajo). Con el
    cliente ajeno, `REMITENTE_OTRO_CLIENTE` queda RECONOCIDO en modo `cliente` (tiene un plan propio
    activo) pero NO autorizado sobre `PRNA` (que es del cliente compartido) — el mismo plan, la
    misma regla, la misma razón de rechazo que en modo `plan`.
  - **U-09** pide "un Autorizado con `AutorizacionCliente` hacia más de un Cliente": `preparar()` le
    agrega a `REMITENTE_SOLOCLIENTE` una segunda `AutorizacionCliente`, hacia este cliente ajeno.
  - Se borra por GUID trackeado en `preparar()`, igual que el resto de la fixture de DN-01 (ver el
    punto siguiente): no es un cliente real, no lo busca nadie por nombre.
- `preparar_datos.py` **no crea `AutorizacionCliente`**: `set_u.py` la arma por Web API, citando los
  lookups `sanic_autorizadoid`/`sanic_clienteid` como `@odata.bind`, **sin mandar `sanic_nombre`**
  (lo calcula un step del servidor — U-07).
- **Ni la `AutorizacionCliente` ni el `Autorizado` propios de este set llevan la marca `ZZP-`
  intacta**: el `sanic_nombre` de `AutorizacionCliente` lo calcula un step del servidor y lo pisa; el
  de `Autorizado` se normaliza a minúsculas. Los dos se borran por el GUID que devolvió el propio
  `POST` de esta misma corrida (`ctx.extra`), nunca por `marca_agua.borrar_marcado()`. El Cliente y
  el Plan ajenos SÍ podrían llevar la marca (nada les pisa el `sanic_nombre`), pero también se borran
  por GUID trackeado, por consistencia con el resto del set.

### D-59 (`diseno/PENDIENTES.md`, ABIERTA, pospuesta por el aprobador el 2026-09-27)

`AvisosDataverse.AvisarAEjecutivos` revienta con `InvalidCastException` cada vez que
`ClasificarCorreo` termina en un estado que dispara el aviso a ejecutivos (remitente no reconocido,
respuesta, ilegible): la transacción se revierte entera y la Solicitud NUNCA llega a persistir "No
reconocida". **U-03, U-04 (mitad a) y U-05 (paso 2) ejercitan justo ese camino** (un correo que
tiene que terminar "No reconocida" bajo `autorizacion.modo = cliente`), así que se espera que
choquen con D-59 en el entorno de hoy — el mismo defecto que ya cruzaron el set R en R-17 y el set C
en C-02/C-04/C-10/C-11. CONVENCIONES §3, "Un defecto conocido no es N-A": esos casos van a quedar en
**FALLA**, citando D-59 en el motivo, nunca en N-A. U-06 se diseñó a propósito para NO pasar por
"No reconocida" (ver arriba), así que no debería toparse con D-59.

---

### U-01 — Compatibilidad: sin el parámetro (o en `plan`), nada cambia

**Qué prueba.** Un `autorizacion.modo` ausente, o con el valor `plan`, se comporta EXACTAMENTE
como el sistema se comportaba antes de DN-01: mira `AutorizacionPlan`.

**Necesita.** El estado de HOY del entorno (sin `autorizacion.modo` sembrado, o sembrado en
`plan`) — confirmarlo con `ver_solicitud.py --parametros` antes de tocar nada. Un Autorizado con
`AutorizacionPlan` a un plan, y SIN ninguna `AutorizacionCliente`.

**Cómo.** Correr cualquier caso de `01-clasificacion-del-correo.md`/`02-reglas-de-solicitud.md` tal
cual están (no citan `AutorizacionCliente` para nada).

**Qué tiene que pasar.** Exactamente lo que esos archivos ya documentan como esperado — ningún
cambio de comportamiento.

**Cómo se verifica.** Los mismos criterios de `01`/`02`/`03` (no se agrega nada nuevo).

**Cómo se sabe que falló.** Cualquier diferencia contra lo que `01`–`08` ya establecieron como
esperado, sin haber tocado `autorizacion.modo`: sería una regresión de DN-01 sobre fase 1.

**Automatizable.** API (reutiliza los casos existentes).

---

### U-02 — Modo `cliente`: un correo autorizado SOLO por cliente entra, en los DOS niveles

**Qué prueba.** La prueba que fija el diseño (`DIRECTRICES-NUEVAS.md`, "La prueba que lo fija"):
con `modo = cliente`, un correo autorizado ÚNICAMENTE por `AutorizacionCliente` (sin ninguna
`AutorizacionPlan`) tiene que PASAR `REMITENTE_RECONOCIDO` (nivel Correo) **y** sus filas tienen
que quedar autorizadas por `AUTORIZACION_CORREO_PLAN` (nivel Registro) — las dos cosas, porque hay
un solo punto de consulta (`Catalogos.PlanesAutorizadosDe`) y nadie puede olvidarse de uno.

**Necesita.** `autorizacion.modo = cliente` (valor de prueba, no versionado: §8.2 solo versiona un
cambio de negocio). `REMITENTE_SOLOCLIENTE = zz-corredor-set-u-dn01-cliente@example.com`, con
Autorizado creado, **CERO** `AutorizacionPlan`, y UNA `AutorizacionCliente` activa hacia el Cliente
compartido `PR - Pruebas MPPP SA` (dueño de `PR11` entre otros) — armado por
`set_u.py::preparar()`.

**Cómo.**
1. Fijar `autorizacion.modo = cliente`.
2. Mandar un correo desde `REMITENTE_SOLOCLIENTE` con `E01-feliz-formato11.xlsx` (cita `PR11`, del
   Cliente autorizado).
3. `ver_solicitud.py --ultima`.

**Qué tiene que pasar.** La Solicitud entra (no "No reconocida"): `REMITENTE_RECONOCIDO` en
Cumplida, y las filas quedan Validada (no "Sin autorización").

**Cómo se verifica.** `ver_solicitud.py --ultima` (estado de la Solicitud, resultado de cada regla,
estado de cada fila).

**Cómo se sabe que falló.** Si la Solicitud queda "No reconocida" (el nivel Correo no está
resolviendo el modo cliente — el bug real que corrigió el aprobador el 2026-09-26: *"el portero
también tiene que preguntar lo mismo"*), o si el correo SÍ entra pero las filas quedan "Sin
autorización" (el nivel Registro sí lo resuelve pero el de Correo no, o viceversa: son dos síntomas
distintos de la MISMA causa raíz — un olvido de modo en uno de los dos llamadores).

**Automatizable.** API.

---

### U-03 — Modo `cliente`: solo `AutorizacionPlan` (sin `AutorizacionCliente`) NO alcanza

**Qué prueba.** El complemento de U-02: en modo `cliente`, `AutorizacionPlan` queda "muda" — no es
un respaldo automático.

**Necesita.** `autorizacion.modo = cliente`. Un correo con `AutorizacionPlan` vigente a un plan,
pero SIN ninguna `AutorizacionCliente`.

**Cómo.** Mandar un correo desde ese remitente citando el plan autorizado por plan.

**Qué tiene que pasar.** La Solicitud queda "No reconocida" (o, si el correo ya era reconocido por
otra vía, las filas de ese plan quedan "Sin autorización").

**Cómo se verifica.** `ver_solicitud.py --ultima`.

**Cómo se sabe que falló.** El correo entra o las filas se autorizan: `AutorizacionPlan` estaría
funcionando como respaldo silencioso en modo `cliente`, en contra del diseño ("se conservan y no se
miran"). **Riesgo conocido (D-59, `diseno/PENDIENTES.md`, ABIERTA):** con `REMITENTE` reconocido
SOLO por `AutorizacionPlan` y ninguna vía por cliente, este caso hace que `REMITENTE_RECONOCIDO`
falle enteramente (no solo el plan citado) y la Solicitud tiene que terminar "No reconocida" —
justo el camino que dispara el aviso a ejecutivos, y ese aviso revienta con `InvalidCastException`
(mismo defecto que ya cruzaron R-17 y C-02/C-04/C-10/C-11). Si el entorno todavía no tiene D-59
corregido, este caso da **FALLA citando D-59**, no N-A (CONVENCIONES §3).

**Automatizable.** API.

---

### U-04 — Valor basura en `autorizacion.modo` degrada a `plan`, sin tumbar nada

**Qué prueba.** `ModoDeAutorizacionParametro.Resolver`: cualquier valor que no sea exactamente
`cliente` (sin distinguir mayúsculas) — vacío, `"clienteX"`, `"PLAN"`, lo que sea — degrada al
fallback quemado `plan`, nunca lanza ni tumba la validación.

**Necesita.** `autorizacion.modo` sembrado con un valor inválido, p. ej. `"clienteX"`.

**Cómo.**
1. Sembrar `autorizacion.modo = "clienteX"`.
2. Mandar el correo de U-02 (autorizado SOLO por cliente).
3. Mandar el correo de `preparar_datos.py` (autorizado por plan, como en fase 1).

**Qué tiene que pasar.** Se comporta como modo `plan`: el correo de U-02 (solo por cliente) NO
entra; el de fase 1 (por plan) sí.

**Cómo se verifica.** `ver_solicitud.py --ultima` sobre las dos Solicitudes.

**Cómo se sabe que falló.** Cualquier excepción o rechazo genérico por el valor del parámetro (el
diseño exige degradar, nunca romper), o que el valor basura se interprete como `cliente`. **Riesgo
conocido (D-59):** la mitad (a) — el correo autorizado SOLO por cliente, que bajo la basura degrada
a `plan` y queda sin ninguna vía — termina "No reconocida" y puede cruzarse con D-59 (ver la nota
de U-03); la mitad (b) no debería verse afectada (el correo por plan SÍ está autorizado y nunca pasa
por el camino del aviso a ejecutivos). Si (a) da FALLA citando D-59, es el defecto conocido, no un
síntoma nuevo de U-04.

**Automatizable.** API.

---

### U-05 — Cambiar el modo no borra nada: `AutorizacionPlan` revive al volver a `plan`

**Qué prueba.** "Cambiar el parámetro vuelve a activarlas sin recargar nada" — las dos vías
conviven en el modelo de datos siempre; el parámetro solo decide cuál se MIRA.

**Necesita.** Un Autorizado con `AutorizacionPlan` vigente, creado ANTES de este caso (el de fase
1, `preparar_datos.py`).

**Cómo.**
1. Confirmar que ese correo entra con `autorizacion.modo = plan` (o ausente).
2. Cambiar a `autorizacion.modo = cliente` y confirmar que ESE MISMO correo ahora NO entra (U-03).
3. Volver a `autorizacion.modo = plan`.
4. Mandar el correo de nuevo.

**Qué tiene que pasar.** Paso 4: vuelve a entrar exactamente como en el paso 1, sin haber tocado la
fila de `AutorizacionPlan` en ningún momento.

**Cómo se verifica.** `ver_solicitud.py --ultima` en los tres momentos.

**Cómo se sabe que falló.** El correo no vuelve a entrar en el paso 4 (algo se limpió o se
desactivó al cambiar de modo, que el diseño dice explícitamente que NO tiene que pasar). **Riesgo
conocido (D-59):** el paso 2 (confirmar que el correo NO entra en modo cliente) es el mismo camino
de U-03 y puede cruzarse con el mismo defecto — un `check` propio deja eso citado por separado, sin
tapar el resultado de los pasos 1 y 4 (el punto real de este caso: que la `AutorizacionPlan` revive
sin recargar nada).

**Automatizable.** API.

---

### U-06 — El mensaje al cliente es idéntico en los dos modos

**Qué prueba.** "No existe" y "no autorizado" siguen sin distinguirse en el texto — el oráculo que
ya se cerró el 2026-09-24 no puede reabrirse por la puerta de atrás de un modo nuevo.

**Corregido contra CONVENCIONES y D-59 (este set).** La redacción original pedía reusar U-03 (una
Solicitud "No reconocida") para el lado `cliente`: eso no sirve para comparar `sanic_mensaje` porque
una Solicitud "No reconocida" nunca abre el Excel — no hay Fila, no hay mensaje que leer — y además
cruza D-59 (ver la nota de U-03). Se rediseñó para que las DOS mitades terminen en una Fila
"Sin autorización" (con mensaje) citando el **mismo plan**, sin que ninguna pase por "No reconocida":

**Necesita.** Dos correos RECONOCIDOS (ninguno "No reconocida") que terminen "Sin autorización"
sobre el MISMO plan `PRNA` (existe, activo, del Cliente compartido):
- (a) modo `plan`: `REMITENTE` (autorizado por `AutorizacionPlan` sobre `PR11`/`PR06`/`PR10`/`PRIN`,
  nunca sobre `PRNA` — el mismo fixture de R-08).
- (b) modo `cliente`: `REMITENTE_OTRO_CLIENTE`, autorizado por `AutorizacionCliente` sobre un
  Cliente AJENO a `PRNA` (con su propio Plan activo, para que `REMITENTE_RECONOCIDO` lo reconozca) —
  preparado por `set_u.py::preparar()`.

**Cómo.**
1. Modo `plan`: mandar `E08-sin-autorizacion.xlsx` (cita `PRNA`) desde `REMITENTE`.
2. Modo `cliente`: mandar el mismo `E08-sin-autorizacion.xlsx` desde `REMITENTE_OTRO_CLIENTE`.
3. Comparar el texto exacto de `sanic_mensaje` en la fila rechazada de cada una.

**Qué tiene que pasar.** Las dos Solicitudes son reconocidas (`procesar = true`); las dos filas
quedan "Sin autorización"; el texto es el mismo unificado del 2026-09-24 (`"El plan {plan} no existe
o usted no está autorizado sobre él."`) **byte a byte** en los dos modos (mismo plan citado: `PRNA`).

**Cómo se verifica.** `ver_solicitud.py` sobre las dos Solicitudes, columna de mensaje de la fila.

**Cómo se sabe que falló.** Un texto distinto entre modos, o un texto que distinga "no existe" de
"no autorizado": eso le da a un atacante un oráculo para enumerar planes válidos. También falla si
alguna de las dos Solicitudes queda "No reconocida" en vez de "Sin autorización" a nivel fila (el
diseño de este caso está armado justo para que eso no pase; si pasa, algo del fixture cambió).

**Automatizable.** API.

---

### U-07 — Nombre calculado de `AutorizacionCliente`: `<correo> → <cliente>`, T(400)

**Qué prueba.** `NombreCalculado.DeAutorizacionCliente` arma el nombre igual que su hermana
`AutorizacionPlan` (mismo formato, mismo recorte), y lo hace el step, no quien crea el registro.

**Necesita.** Un Autorizado y un Cliente existentes, sus dos ids.

**Cómo.** `dataverse_api.py POST sanic_mppp_tbl_autorizacionclientes` con
`sanic_autorizadoid@odata.bind` y `sanic_clienteid@odata.bind`, **sin** mandar `sanic_nombre`.

**Qué tiene que pasar.** El registro creado trae `sanic_nombre = "<correo del autorizado> →
<nombre del cliente>"`.

**Cómo se verifica.** `dataverse_api.py GET "sanic_mppp_tbl_autorizacionclientes(<id>)?$select=sanic_nombre"`.

**Cómo se sabe que falló.** `sanic_nombre` vacío, con el separador equivocado, o mandado por quien
crea el registro sin que el step lo pise (el step tiene que pisar SIEMPRE lo que venga de una
persona).

**Automatizable.** API.

---

### U-08 — Subgrilla "Correos autorizados sobre este cliente" en el formulario de Cliente

**Qué prueba.** DN-01 punto 6: la app necesita poder VER las autorizaciones por cliente desde el
lado del Cliente (vista 18 de `diseno/05-app-model-driven.md` §3, agregada el 2026-09-27).

**Corregido contra el diseño (este set): pasa de GUI a API.** El diseño original marcaba este caso
como navegador puro ("inspección visual"). No hace falta: lo que hay que verificar es que el
`formxml` publicado del formulario principal de Cliente tenga un control de subgrilla apuntando a
`AutorizacionCliente` con la vista y la relación correctas, y que la condición que esa subgrilla usa
para filtrar (el lookup `sanic_clienteid`) devuelva datos — exactamente lo que
`herramientas/construir/formulario.py` ya usa para publicar y verificar formularios, acá solo de
lectura. Una subgrilla vacía por un filtro mal armado (el segundo síntoma que este caso nombra) se
distingue leyendo la MISMA condición de filtro por Web API, no mirando la pantalla.

**Necesita.** El Cliente compartido `PR - Pruebas MPPP SA` con al menos una `AutorizacionCliente`
(la que arma `set_u.py::preparar()`). Ninguna sesión de navegador.

> **Nota histórica.** El diseño original pedía la sesión `local/sesion-admin_planes.json` (rol
> `SR - MPPP - Administrador de planes` — DN-01 punto 6: "el Administrador de planes es quien las
> administra", RF-11; el Administrador técnico no tiene lectura de Cliente ni de
> AutorizacionCliente, `04-matriz-privilegios.md`). Sigue siendo el rol correcto SI algún día hace
> falta una inspección visual real (por ejemplo, ante una sospecha de que la subgrilla quedó en una
> pestaña oculta, algo que el `formxml` de este formulario no puede tener: `formulario.py` genera
> siempre una única pestaña). El comando de ese camino, si hiciera falta: `cd pruebas/gui &&
> MPPP_USUARIO=admin_planes npx playwright test`.

**Cómo.**
1. `GET systemforms?$filter=objecttypecode eq 'sanic_mppp_tbl_cliente' and type eq 2` — el
   formulario principal de Cliente, su `formxml`.
2. Buscar en el `formxml` el control de subgrilla con `TargetEntityType =
   sanic_mppp_tbl_autorizacioncliente`, y el título de su sección.
3. `GET EntityDefinitions(LogicalName='sanic_mppp_tbl_cliente')/OneToManyRelationships` — confirmar
   que la relación del control es la del lookup `sanic_clienteid`.
4. `GET savedqueries` — confirmar que la vista `Correos autorizados sobre este cliente` existe y que
   el `ViewId` del control apunta a ella.
5. `GET sanic_mppp_tbl_autorizacionclientes?$filter=_sanic_clienteid_value eq <id del Cliente>` — la
   MISMA condición que usaría la subgrilla, para confirmar que hay algo que mostrar.

**Qué tiene que pasar.** El control existe, en una sección titulada "Correos autorizados sobre este
cliente", apuntando a la vista y la relación correctas; la consulta del paso 5 devuelve al menos una
fila.

**Cómo se verifica.** Los cinco pasos de arriba, todos por Web API (`herramientas/pruebas/sets/set_u.py::u08`).

**Cómo se sabe que falló.** El `formxml` no trae ningún control con ese `TargetEntityType` (nunca se
agregó al formulario), la relación o la vista no coinciden (subgrilla mal armada), o el paso 5
devuelve cero filas con datos que sí existen (filtro mal configurado — confundir esto con "no hay
autorizaciones" es el mismo tipo de error que motivó A-15 en `05-app-y-comandos.md`).

**Automatizable.** API.

---

### U-09 — Subgrilla "Clientes que puede modificar" en el formulario de Autorizado

**Qué prueba.** El pedido textual del aprobador del 2026-09-27: poder entrar por correo y ver con
cuántos CLIENTES está relacionado (antes solo se veían los planes).

**Corregido contra el diseño (este set): pasa de GUI a API**, mismo criterio que U-08.

**Necesita.** Un Autorizado con `AutorizacionCliente` hacia más de un Cliente: `set_u.py::preparar()`
le da a `REMITENTE_SOLOCLIENTE` una segunda `AutorizacionCliente`, hacia un Cliente "ajeno" propio de
este set (ver "Preparativos comunes"). Ninguna sesión de navegador.

**Cómo.**
1. `GET systemforms?$filter=objecttypecode eq 'sanic_mppp_tbl_autorizado' and type eq 2` — el
   formulario principal de Autorizado.
2. Confirmar que la subgrilla existente `Planes que puede modificar` (`AutorizacionPlan`) sigue en
   el `formxml`, intacta.
3. Buscar el control de subgrilla con `TargetEntityType = sanic_mppp_tbl_autorizacioncliente`, su
   sección y su relación (lookup `sanic_autorizadoid`).
4. `GET savedqueries` — confirmar que la vista `Clientes que puede modificar` existe y que el
   `ViewId` del control apunta a ella.
5. `GET sanic_mppp_tbl_autorizacionclientes?$filter=_sanic_autorizadoid_value eq <id del Autorizado>`
   — confirmar que hay AutorizacionCliente hacia MÁS DE UN Cliente distinto.

**Qué tiene que pasar.** Las dos subgrillas conviven en el formulario ("Planes que puede modificar"
sin tocar, "Clientes que puede modificar" nueva); la consulta del paso 5 devuelve al menos dos
clientes distintos.

**Cómo se verifica.** Los cinco pasos de arriba, todos por Web API (`herramientas/pruebas/sets/set_u.py::u09`).

**Cómo se sabe que falló.** Solo aparece el control de planes (el de clientes no se agregó), la
relación o la vista no coinciden, o el paso 5 devuelve menos de dos clientes distintos (el Autorizado
de prueba no está armado como pide el MD, o la subgrilla mostraría menos de lo que realmente tiene
autorizado).

**Automatizable.** API.

---

### U-10 — Evidencia firmada opcional en `AutorizacionCliente`

**Qué prueba.** El hueco que señaló el aprobador el 2026-09-27 (*"no está la columna de archivo
adjunto... al igual que en autorizacion por plan"*) está cerrado: `sanic_documentofirmado` (archivo,
10 MB, no requerida) y `sanic_fechadocumento` (fecha, no requerida), ambas auditadas, con la MISMA
forma que sus pares de `AutorizacionPlan`.

**Corregido contra el diseño (este set): pasa de GUI a API, y de "inspección" a FUNCIONAL.** No solo
se puede leer la metadata y el `formxml` sin navegador: se puede probar la escritura REAL de la
evidencia (subir el archivo, escribir la fecha, releer) por Web API, igual que `simulador.py` ya
sube `sanic_correocrudo`/`sanic_exceloriginal` a la Solicitud. Es una prueba más fuerte que la
inspección visual: no solo confirma que los controles están en el formulario, confirma que Dataverse
acepta guardarlos.

**Necesita.** Una `AutorizacionCliente` existente sin evidencia cargada (la que arma
`set_u.py::preparar()` para U-02/U-08). Ninguna sesión de navegador.

**Cómo.**
1. `GET EntityDefinitions(LogicalName='sanic_mppp_tbl_autorizacioncliente')/Attributes` — confirmar
   que `sanic_documentofirmado` (`FileType`) y `sanic_fechadocumento` (`DateTimeType`) existen con
   `RequiredLevel = None`.
2. `GET systemforms` — el formulario principal de `AutorizacionCliente`, confirmar que trae la
   sección "Evidencia de la autorizacion" con los dos campos.
3. Leer la `AutorizacionCliente` existente: confirmar que los dos campos están vacíos (se guardó sin
   evidencia y no la exigió).
4. Subir un archivo de prueba a `sanic_documentofirmado`
   (`dv.subir_archivo(".../sanic_documentofirmado", "...", contenido)`, el mismo mecanismo que
   `dataverse_api.py`/`simulador.py` usan para `sanic_correocrudo`) y escribir
   `sanic_fechadocumento` con un `PATCH`.
5. Releer y confirmar que los dos quedaron guardados.

**Qué tiene que pasar.** Los dos campos existen, no son requeridos, y el formulario los muestra;
guardado sin ellos no falla (paso 3); subirlos después SÍ se puede (pasos 4 y 5).

**Cómo se verifica.** Los cinco pasos de arriba, todos por Web API (`herramientas/pruebas/sets/set_u.py::u10`).

**Cómo se sabe que falló.** El guardado sin evidencia se rechaza (se volvieron requeridas por
error), los campos no existen en la metadata o en el `formxml` (el hueco que señaló el aprobador
seguiría abierto), o la subida del archivo / la escritura de la fecha devuelve un error HTTP.

**Automatizable.** API.

---

### U-11 — Clave alternativa: no se repite el par autorizado + cliente

**Qué prueba.** Igual que `AutorizacionPlan`, la clave alternativa autorizado+cliente impide
duplicar la misma relación.

**Necesita.** Una `AutorizacionCliente` ya existente (autorizado A, cliente X).

**Cómo.** `dataverse_api.py POST sanic_mppp_tbl_autorizacionclientes` con el MISMO par
autorizado+cliente.

**Qué tiene que pasar.** Rechazo por violación de clave alternativa (HTTP 4xx citando la clave
duplicada).

**Cómo se verifica.** El mensaje de error de la llamada.

**Cómo se sabe que falló.** El segundo registro se crea sin problema: la clave alternativa no está
realmente aplicada en el entorno (una cosa es que el playbook la declare, otra que Dataverse la
esté haciendo cumplir).

**Automatizable.** API.

---

## Resultados

Corrida real, `python3 herramientas/pruebas/correr.py U`, 2026-09-27: **8 OK · 3 FALLA · 0 N-A**.

| Caso | Resultado | Notas |
|---|---|---|
| U-01 compatibilidad sin el parámetro | OK | parámetro compartido |
| U-02 modo cliente — entra en los dos niveles | OK | parámetro compartido |
| U-03 modo cliente — solo AutorizacionPlan no alcanza | FALLA — bloqueado por D-59 | parámetro compartido · confirmado: la Solicitud no llega a "No reconocida", `ClasificarCorreo` corta con HTTP 400 |
| U-04 valor basura degrada a plan | FALLA — bloqueado por D-59 en (a) | parámetro compartido · (b) SÍ corrió y pasó (el correo autorizado por plan sigue entrando igual con el valor basura); el caso es FALLA solo por (a), bloqueado por D-59 |
| U-05 cambiar de modo no borra nada | FALLA — bloqueado por D-59 en el paso 2 | parámetro compartido · los pasos 1 y 4 SÍ corrieron y pasaron (el punto real del caso — `AutorizacionPlan` revive sin recargar nada — quedó demostrado); el caso entero es FALLA porque UN `check` en rojo alcanza (CONVENCIONES: "OK si TODO pasó"), y el rojo es el paso 2, bloqueado por D-59 |
| U-06 mismo mensaje en los dos modos | OK | rediseñado (evita D-59), texto byte a byte idéntico confirmado |
| U-07 nombre calculado `<correo> → <cliente>` | OK | |
| U-08 subgrilla en Cliente | OK | API (formxml + metadata, sin navegador) |
| U-09 subgrilla en Autorizado | OK | API (formxml + metadata, sin navegador) |
| U-10 evidencia firmada opcional | OK | API (funcional: subió el archivo y la fecha de verdad, y los releyó) |
| U-11 clave alternativa autorizado+cliente | OK | |

**Próximo paso, una vez resuelto D-59:** U-03/U-04/U-05 no necesitan tocarse — en cuanto
`AvisosDataverse.AvisarAEjecutivos` deje de reventar van a poder terminar de evaluar el resto de sus
`check`s (en U-05, los pasos 1 y 4, que hoy quedan sin correr porque el caso corta al primer defecto
bloqueante). No son casos mal escritos: son casos que ya están probando lo que tienen que probar, y
lo que encuentran es D-59.
