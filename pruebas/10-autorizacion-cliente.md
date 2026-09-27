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

- **`autorizacion.modo` es un parámetro de ENTORNO COMPARTIDO.** Cambiarlo afecta la validación de
  CUALQUIER Solicitud que se procese mientras esté en `cliente` — incluidas las de otros archivos
  de `pruebas/` que alguien más esté corriendo al mismo tiempo. Coordinar la ventana, y restaurar el
  valor a `plan` (o borrarlo) apenas termine cada caso que lo necesite en `cliente`.
- Un Cliente de prueba (puede ser el mismo `PR - Pruebas MPPP SA` de `preparar_datos.py`, o uno
  nuevo `ZZ - Cliente DN-01`), con al menos un Plan activo.
- Un correo `REMITENTE` de prueba (el mismo de `00-preparacion.md` P-02 sirve), y un Autorizado
  creado sobre él.
- `preparar_datos.py` **no crea `AutorizacionCliente` todavía** (solo `AutorizacionPlan`): hay que
  crearla a mano con `dataverse_api.py POST sanic_mppp_tbl_autorizacionclientes '{...}'`, citando
  los lookups `sanic_autorizadoid`/`sanic_clienteid` como `@odata.bind`.
- Datos ficticios, descartables con `zz`/`ZZ`.

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

**Necesita.** `autorizacion.modo = cliente` (nueva versión del parámetro). Un correo
`REMITENTE-DN01@dominio.com` con Autorizado creado, **CERO** `AutorizacionPlan`, y UNA
`AutorizacionCliente` activa hacia el Cliente dueño de un Plan activo (p. ej. `PR11`).

**Cómo.**
1. Sembrar `autorizacion.modo = cliente`.
2. Crear el Autorizado y la `AutorizacionCliente` (sin ninguna `AutorizacionPlan`).
3. Mandar un correo desde ese remitente con `E01-feliz-formato11.xlsx` (citando el plan del
   Cliente autorizado).
4. `ver_solicitud.py --ultima`.

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
miran").

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
diseño exige degradar, nunca romper), o que el valor basura se interprete como `cliente`.

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
desactivó al cambiar de modo, que el diseño dice explícitamente que NO tiene que pasar).

**Automatizable.** API.

---

### U-06 — El mensaje al cliente es idéntico en los dos modos

**Qué prueba.** "No existe" y "no autorizado" siguen sin distinguirse en el texto — el oráculo que
ya se cerró el 2026-09-24 no puede reabrirse por la puerta de atrás de un modo nuevo.

**Necesita.** Dos Solicitudes rechazadas por falta de autorización: una en modo `plan` (un correo
sin `AutorizacionPlan`, como `PRNA` de `preparar_datos.py`) y otra en modo `cliente` (U-03).

**Cómo.** Comparar el texto exacto de `sanic_mensaje` en las filas rechazadas de las dos.

**Qué tiene que pasar.** El mismo texto unificado del 2026-09-24 (`"El plan {plan} no existe o
usted no está autorizado sobre él."`) en los dos modos.

**Cómo se verifica.** `ver_solicitud.py` sobre las dos Solicitudes, columna de mensaje de cada
fila.

**Cómo se sabe que falló.** Un texto distinto entre modos, o un texto que distinga "no existe" de
"no autorizado": eso le da a un atacante un oráculo para enumerar planes válidos.

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
lado del Cliente (vista 18 de `05-app-model-driven.md` §3, agregada el 2026-09-27).

**Necesita.** Sesión `local/sesion-admin_planes.json` (rol `SR - MPPP - Administrador de planes` —
generada por el aprobador con `node guardar-sesion.mjs admin_planes`, ver `pruebas/gui/README.md`;
confirmar que está viva y con el rol correcto con `python3 herramientas/pruebas/estado_sesiones.py`
antes de correr el caso). Un Cliente con al menos una `AutorizacionCliente`.

> **Por qué este rol y no el otro.** DN-01 punto 6 es explícito: "el Administrador de planes es
> quien las administra (RF-11)". El Administrador técnico (`SR - MPPP - Administrador tecnico`)
> **no tiene lectura de Cliente ni de AutorizacionCliente** (`04-matriz-privilegios.md`: "solo toca
> lo técnico, Parámetros y Reglas"): si se corre este caso con esa sesión por error, el formulario
> ni siquiera abre — 403, no un defecto de la subgrilla.

**Cómo.** Abrir el formulario de ese Cliente en la app.

**Qué tiene que pasar.** Una subgrilla visible listando las `AutorizacionCliente` de ese cliente,
con al menos el correo del autorizado.

**Cómo se verifica.** Inspección visual del formulario.

**Cómo se sabe que falló.** La subgrilla no aparece (nunca se agregó al formulario, o quedó en
otra pestaña no visible), o aparece vacía teniendo datos (filtro mal configurado — confundir esto
con "no hay autorizaciones" es el mismo tipo de error que motivó H-06 en `13-construido-hoy.md`).

**Automatizable.** GUI (sesión `admin_planes`).

---

### U-09 — Subgrilla "Clientes que puede modificar" en el formulario de Autorizado

**Qué prueba.** El pedido textual del aprobador del 2026-09-27: poder entrar por correo y ver con
cuántos CLIENTES está relacionado (antes solo se veían los planes).

**Necesita.** Sesión `local/sesion-admin_planes.json` (rol `SR - MPPP - Administrador de planes`,
confirmada con `estado_sesiones.py` — mismo motivo que U-08: es quien administra Autorizados y
Autorizaciones, el Administrador técnico no ve estas tablas). Un Autorizado con
`AutorizacionCliente` hacia más de un Cliente.

**Cómo.** Abrir el formulario de ese Autorizado.

**Qué tiene que pasar.** Una subgrilla "Clientes que puede modificar" con TODOS los clientes
relacionados, junto a la ya existente "Planes que puede modificar".

**Cómo se verifica.** Inspección visual del formulario.

**Cómo se sabe que falló.** Solo se ve la subgrilla de planes (la de clientes no se agregó, o
quedó oculta), o aparece con menos clientes de los que realmente tiene autorizados.

**Automatizable.** GUI (sesión `admin_planes`).

---

### U-10 — Evidencia firmada opcional en `AutorizacionCliente`

**Qué prueba.** El hueco que señaló el aprobador el 2026-09-27 (*"no está la columna de archivo
adjunto... al igual que en autorizacion por plan"*) está cerrado: `sanic_documentofirmado` (archivo,
10 MB, no requerida) y `sanic_fechadocumento` (fecha, no requerida), ambas auditadas, con la MISMA
forma que sus pares de `AutorizacionPlan`.

**Necesita.** Sesión `local/sesion-admin_planes.json` (rol `SR - MPPP - Administrador de planes`,
confirmada con `estado_sesiones.py`); una `AutorizacionCliente` existente.

**Cómo.**
1. Abrir el formulario y verificar que existe la sección "Evidencia de la autorización" con los
   dos campos.
2. Guardar el registro SIN llenarlos (tienen que aceptar quedar vacíos).
3. Adjuntar un archivo de prueba (ficticio) y una fecha, guardar de nuevo.

**Qué tiene que pasar.** Paso 2: guarda sin error (no son requeridos). Paso 3: guarda con el
archivo y la fecha.

**Cómo se verifica.** Inspección del formulario, y
`dataverse_api.py GET "sanic_mppp_tbl_autorizacionclientes(<id>)?$select=sanic_fechadocumento,_sanic_documentofirmado_value"` (o el nombre real de la columna de archivo).

**Cómo se sabe que falló.** El guardado sin evidencia se rechaza (se volvieron requeridas por
error), o los campos no existen en el formulario (el hueco que señaló el aprobador seguiría
abierto).

**Automatizable.** GUI (sesión `admin_planes`), con un chequeo de datos por API.

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

| Caso | Resultado | Notas |
|---|---|---|
| U-01 compatibilidad sin el parámetro | | |
| U-02 modo cliente — entra en los dos niveles | | parámetro compartido |
| U-03 modo cliente — solo AutorizacionPlan no alcanza | | parámetro compartido |
| U-04 valor basura degrada a plan | | parámetro compartido |
| U-05 cambiar de modo no borra nada | | parámetro compartido |
| U-06 mismo mensaje en los dos modos | | |
| U-07 nombre calculado `<correo> → <cliente>` | | |
| U-08 subgrilla en Cliente | | sesión admin_planes |
| U-09 subgrilla en Autorizado | | sesión admin_planes |
| U-10 evidencia firmada opcional | | sesión admin_planes |
| U-11 clave alternativa autorizado+cliente | | |
