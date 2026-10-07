# 08 — Integridad y plugins

Set **I** (`CONVENCIONES.md` §4). Cubre los **steps de plugin** que corren al guardar (la red que
impide dejar el sistema en un estado imposible), el **catálogo de Reglas**, las **dos claves
alternativas de autorización** (por plan y, desde fase 2, por cliente — DN-01) y la **puerta de
las Custom API del RPA** (D-9/D-44).

**Reescrito el 2026-09-27.** La versión anterior de este archivo tenía las afirmaciones falsas más
graves de todo el plan, y las tres se corrigen acá:

1. **La tabla de "8 steps registrados" listaba un step que nunca existió** (`IntegridadAutorizacionPlanStep`)
   **y le faltaba uno que sí existe** (`RevisionAtendidaStep`). Verificado contra el código
   (`src/Sanic.Mppp.Plugins/Steps/*.cs`) y en vivo contra Dev el 2026-09-27: la integridad de una
   autorización duplicada la hace una **clave alternativa** (`sanic_mppp_key_autorizacionplan_autorizado_plan`),
   nunca un plugin — no existe ni existió ningún `IntegridadAutorizacionPlanStep`. Ver I-05.
2. **I-09 decía "10 tipos"; en vivo, el 2026-09-27, son 13** (8 clases de step + 5 Custom API,
   `GET plugintypes` filtrado por el ensamblado). Ver I-09. *(Nota 2026-10-06: desde DN-04, 2026-10-01, son 15 — se sumaron `CargarCatalogosApi` y `CambiarEstadoCatalogosApi`.)*
3. **Dos casos citaban un archivo ajeno** (`pruebas/README.md`, la deuda declarada del
   2026-09-27: "cuatro en `04`, **dos en `08`** y uno en `07`"): I-05 decía "una autorización
   existente (P-03, `00-preparacion.md`)" e I-07 decía "una Fila existente (la de C-01 alcanza,
   `01-clasificacion-del-correo.md`)". El **código** de los dos casos ya usaba su propio fixture
   (`sets/set_i.py::preparar()`); lo que estaba mal era solo la prosa de este MD, que ya no cita
   ningún otro archivo (CONVENCIONES §8).
4. **I-07 estaba mal PLANTEADO, no solo mal escrito** (corregido más tarde el mismo 2026-09-27):
   el "defecto confirmado" que este archivo declaraba se medía con el **service principal**
   (`applicationid`), una identidad **exenta de `ListaBlancaStep` por diseño** (`04` §1, código de
   servidor) — nunca con un humano. Probado en vivo con `User1` (Ejecutivo, impersonado con
   `MSCRMCallerID`): el `PATCH` a `sanic_referencia` **se rechaza** (HTTP 400). **No hay defecto.**
   Ver el detalle completo en I-07; `diseno/PENDIENTES.md` (entrada `I-07`) queda pendiente de que
   su dueño la cierre con esta evidencia.

**Además, siguiendo CONVENCIONES §7 ("nombrar, no contar")**: en vez de repetir la vieja tabla de
prosa "8 steps con su función", este archivo **nombra los 20 registros de step, uno por uno**, con
su mensaje, su etapa y su tabla, y los verifica contra `sdkmessageprocessingsteps` EN VIVO (I-10).
Un conteo envejece con cada step nuevo; una lista de nombres, no.

**Los 15 tipos de plugin registrados** (8 clases de step + 7 Custom API — las Custom API no son
steps y no se prueban en este archivo, las cubre `simulador.py`/`02`/`03`):

| Clase | Qué protege |
|---|---|
| `NombreCalculadoStep` | el nombre de las tablas que no lo digita nadie (hoy 4: Plan, AutorizacionPlan, AutorizacionCliente, Fila — I-06) |
| `NormalizarYValidarStep` | normaliza y valida al guardar (Cliente, Plan, Autorizado, Parametro) |
| `ListaBlancaStep` | que solo se escriba lo que se puede escribir (Fila, Solicitud) |
| `TransicionDeFilaStep` | que una transición de estado sea legal, y el step de claim (03 §2) |
| `PostTransicionDeFilaStep` | los efectos posteriores (Bitácora, cierre de la Solicitud) |
| `AtenderPorClasificarStep` | Atendido y Descartar sobre una Solicitud por clasificar |
| `IntegridadDeReglaStep` | que el catálogo de reglas sea coherente |
| `RevisionAtendidaStep` | deja en Bitácora el motivo que escribió la máquina, al usar "Revisado" |

**Fase 2** agregó, y este set ya los cubre: la 4ª tabla de `NombreCalculadoStep`
(`AutorizacionCliente`, I-06), la clave alternativa nueva `sanic_mppp_key_autorizacioncliente_autorizado_cliente`
(I-11, DN-01), la puerta de las Custom API del RPA (`PuertaDelRpa.cs`, I-12, D-9/D-44) y el
comportamiento del claim dentro de `TransicionDeFilaStep` (I-13, `03` §2).

**Cómo se prueban.** Casi todos, **haciendo algo prohibido** y verificando que el sistema se niegue
con un mensaje claro. Un plugin que deja pasar lo prohibido es peor que no tenerlo.

**Set autocontenido** (`CONVENCIONES.md` §8). Este archivo no depende de ningún otro: prepara sus
propios datos y no supone lo que dejó otro set. La preparación exacta que hace `preparar()`, y
por qué no hace otras cosas (roles de Ejecutivo/Supervisor, canon), está documentada en
`herramientas/pruebas/sets/set_i.py::preparar()` — acá solo el resumen:

| Qué prepara | Cómo |
|---|---|
| Datos de negocio (`PR*`, fixture compartido, `REMITENTE` autorizado sobre PR11/PR06/PR10/PRIN) | `preparacion_comun.verificar_datos_negocio()` |
| Los 15 Excel + el adjunto no-Excel | `preparacion_comun.verificar_excels()` |
| Roles de Ejecutivo (User1) y Supervisor (User2) — SOLO los necesita I-13; Ejecutivo (User1) y Administrador de planes (User3) — SOLO los necesita I-07 | resueltos y verificados DENTRO de cada caso, no en `preparar()` (si un día cambiaran, el resto de los 11 casos sigue corriendo igual) |

**Corré este set con**:

```bash
python3 herramientas/pruebas/correr.py I
```

---

## I-01 · El catálogo de reglas: una regla activa sin evaluador

**Qué prueba.** `IntegridadDeReglaStep` (D-13): una regla activa cuyo código no tiene un evaluador
programado para su nivel.

**Necesita.** Nada además de lo que `preparar()` de este set ya deja listo: el service principal
de Dev (System Administrator) alcanza para crear y borrar una Regla.

**Cómo.** `POST sanic_mppp_tbl_reglas` con `sanic_codigo = "ZZ_REGLA_QUE_NO_EXISTE"`, `sanic_nivel =
Registro`, `sanic_efecto = Rechaza`, `sanic_orden = 90`.

**Qué tiene que pasar.** El `POST` **no se guarda**: HTTP distinto de 200/201/204. El mensaje dice
que ese código no tiene evaluador programado para ese nivel.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py I` (caso `I-01`).

**Cómo se sabe que falló.** Si el `POST` da 200/201/204, `IntegridadDeReglaStep` no está frenando
este caso — el caso lo detecta y **borra la regla inventada de inmediato**, antes de que una
validación real la evalúe y reviente con una excepción real (todas las Solicitudes quedarían en
Ingresada).

**Automatizable.** API (`correr.py I`, caso `I-01`).

---

## I-02 · Una regla que depende de otra inactiva

**Qué prueba.** `IntegridadDeReglaStep`, el segundo caso de D-13: no se puede desactivar una regla
de la que dependen otras reglas activas.

**Necesita.** La regla `PLAN_EXISTE` del catálogo sembrado (`playbooks/datos/reglas.md`), de la
que dependen cuatro reglas activas.

**Cómo.** `PATCH sanic_mppp_tbl_reglas(<PLAN_EXISTE>)` con `statecode = Inactivo`.

**Qué tiene que pasar.** **No deja desactivarla**: HTTP distinto de 200/204. El mensaje nombra las
reglas que dependen de ella. Al terminar, `PLAN_EXISTE` queda **activa** (el caso la revierte en
un `finally`, pase lo que pase: de esto depende cualquier otra validación que corra en paralelo,
incluida la del otro agente en `pruebas/gui/`).

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py I` (caso `I-02`).

**Cómo se sabe que falló.** Si el `PATCH` da 200/204, el catálogo compartido de Reglas quedó con
`PLAN_EXISTE` inactiva — **hay que reactivarla a mano de inmediato** (`sanic_codigo eq
'PLAN_EXISTE'`, `statecode = 0`) antes de que cualquier otra validación la evalúe, porque el propio
caso reintenta reactivarla en su `finally` pero un FALLA acá significa que esa reactivación también
podría estar fallando.

**Automatizable.** API (`correr.py I`, caso `I-02`).

---

## I-03 · Una regla de nivel Registro con efecto "Envía a revisión"

**Qué prueba.** D-20: ese efecto solo es válido en las reglas del sobre (niveles Correo y
Solicitud), nunca en Registro.

**Necesita.** La regla `MONEDA_DEL_PLAN` (nivel Registro, efecto Rechaza) del catálogo sembrado.

**Cómo.** `PATCH sanic_mppp_tbl_reglas(<MONEDA_DEL_PLAN>)` con `sanic_efecto = Envía a revisión`.

**Qué tiene que pasar.** **No deja guardar**: HTTP distinto de 200/204, con un mensaje que nombra
el motivo. Al terminar, `MONEDA_DEL_PLAN` queda con efecto **Rechaza** (revertido en `finally`).

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py I` (caso `I-03`).

**Cómo se sabe que falló.** Si el `PATCH` da 200/204, `MONEDA_DEL_PLAN` quedó con un efecto que el
motor de validación (D-20) no sabe interpretar en nivel Registro — **hay que revertirla a mano**
(`sanic_efecto = Rechaza`) antes de correr cualquier caso de `03-reglas-de-registro.md` que la use.

**Automatizable.** API (`correr.py I`, caso `I-03`).

---

## I-04 · Una regla que depende de sí misma

**Qué prueba.** La autodependencia se rechaza **por el código** (comparación de `sanic_codigo`),
antes de mirar ningún `sanic_orden` — el defecto que encontró la revisión de código del
2026-09-22: un `Update` que sube el orden Y agrega la autodependencia en el mismo guardado no podía
usar "orden estrictamente menor" como única defensa, porque el catálogo leído todavía trae el orden
VIEJO de esa misma regla.

**Necesita.** La regla `MONEDA_DEL_PLAN` del catálogo sembrado.

**Cómo.** `PATCH sanic_mppp_tbl_reglas(<MONEDA_DEL_PLAN>)` con `sanic_dependede =
"MONEDA_DEL_PLAN"` (su propio código).

**Qué tiene que pasar.** **No deja guardar**: *"No se puede guardar la regla 'MONEDA_DEL_PLAN': no
puede depender de sí misma."* Al terminar, `sanic_dependede` queda como estaba (revertido en
`finally`).

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py I` (caso `I-04`).

**Cómo se sabe que falló.** Si el `PATCH` da 200/204, la regla quedó con un ciclo de tamaño uno en
`sanic_dependede` — **hay que revertirla a mano** a su valor original antes de que el motor de
validación intente resolver sus dependencias y entre en un ciclo.

**Automatizable.** API (`correr.py I`, caso `I-04`).

---

## I-05 · Integridad de una autorización por PLAN (duplicada)

**Qué prueba.** La **clave alternativa** `sanic_mppp_key_autorizacionplan_autorizado_plan` sobre
AutorizacionPlan. **No hay ningún `IntegridadAutorizacionPlanStep`** (esa clase nunca existió en el
código: ver la corrección al principio de este archivo) — la integridad de una autorización
duplicada la garantiza la clave, no un plugin.

**Necesita.** El `REMITENTE` de este set, ya autorizado sobre `PR11` por `preparar()` de este mismo
set (`preparacion_comun.verificar_datos_negocio`, que siembra el fixture `PR*` — no depende de
ningún otro archivo del plan).

**Cómo.** `POST sanic_mppp_tbl_autorizacionplans` con el **mismo** autorizado (`REMITENTE`) y el
**mismo** plan (`PR11`) que ya tiene autorizado.

**Qué tiene que pasar.** **No deja guardar** (por la clave alternativa): HTTP distinto de
200/201/204, con un mensaje entendible y **no** un volcado técnico de la plataforma
(`Microsoft.Crm.CrmException` no debería aparecer en el texto).

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py I` (caso `I-05`).

**Cómo se sabe que falló.** Si el `POST` da 200/201/204, la clave alternativa no está frenando el
duplicado — el caso la borra de inmediato. Si el `POST` se rechaza pero el mensaje trae
`Microsoft.Crm.CrmException` (o un volcado similar), el rechazo funciona pero expone un mensaje que
un Administrador de planes no puede leer.

**Automatizable.** API (`correr.py I`, caso `I-05`).

---

## I-06 · El nombre calculado: Plan, AutorizacionPlan, AutorizacionCliente y Fila

**Qué prueba.** `NombreCalculadoStep`, en **las cuatro tablas que cubre hoy** — nombradas, no
contadas (CONVENCIONES §7): `Plan`, `AutorizacionPlan`, `AutorizacionCliente` (agregada en fase 2,
DN-01) y `Fila`.

**Necesita.** El Cliente (`prep.CLIENTE`) y el Autorizado (`REMITENTE`) que `preparar()` de este
set deja listos; el plan `PRNA` del fixture (existe, pero el `REMITENTE` de este set **no** está
autorizado sobre él — a propósito, por `preparar_datos.py`); una Solicitud propia de este caso
(`ctx.procesar()` con `E01-feliz-formato11.xlsx`) para la Fila.

**Cómo.**

1. Crea un **Plan** (`sanic_codigo = "ZZNM"`) sin tocar `sanic_nombre`.
2. Crea una **AutorizacionPlan** (`REMITENTE` → `PRNA`) sin tocar `sanic_nombre`.
3. Crea una **AutorizacionCliente** (`REMITENTE` → `prep.CLIENTE`) sin tocar `sanic_nombre`.
4. Procesa una Solicitud con 3 filas (`E01-feliz-formato11.xlsx`) y lee el `sanic_nombre` de cada
   Fila.

**Qué tiene que pasar.**

| Tabla | Nombre calculado esperado |
|---|---|
| Plan | `ZNM - PR - Pruebas MPPP SA` (`CODIGO - Cliente`) |
| AutorizacionPlan | `<REMITENTE> → PRNA` (`correo → CODIGO del plan`) |
| AutorizacionCliente | `<REMITENTE> → PR - Pruebas MPPP SA` (`correo → NOMBRE del cliente`, no el código: no tiene) |
| Fila (cada una de las 3) | `<MPPP-########>-F01`, `-F02`, `-F03` (`<solicitud>-F<NN>`, dos dígitos como mínimo) |

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py I` (caso `I-06`).

**Cómo se sabe que falló.** Distinguí CUÁL de las cuatro tablas falló por el nombre del check (cada
una se reporta por separado, con el valor obtenido): si es **Plan** o **AutorizacionPlan**, es una
regresión en código ya viejo (fase 1); si es **AutorizacionCliente**, es la parte nueva de fase 2
(DN-01) la que no quedó bien enchufada al step; si es **Fila**, el problema no es de esta fase — es
el mismo cálculo que usa TODO el resto del plan para citar el número de fila.

**Automatizable.** API (`correr.py I`, caso `I-06`).

---

## I-07 · Lista blanca de escrituras (columna calculada por PATCH directo)

**Reescrito el 2026-09-27: el diagnóstico viejo medía la identidad equivocada, no es un defecto.**
Ver el detalle completo de la investigación y la evidencia abajo. **`diseno/PENDIENTES.md` (entrada
`I-07`) todavía dice "ABIERTA": ese archivo es de otro dueño en esta ronda y no se tocó — hace
falta que quien lo mantiene la cierre con la evidencia de acá.**

**Qué prueba.** `ListaBlancaStep` (`04` §1): que un **humano**, con su rol de negocio, no pueda
escribir a mano por Web API una columna que solo llena la validación (`sanic_referencia`) —
saltándose el formulario de solo lectura. Y de paso, sus dos bordes: que la protección que en
verdad frena a un rol sin privilegio de escritura sobre Fila es otra (04 §2, no la lista blanca), y
que una columna que SÍ está permitida (`sanic_notainterna`) no se vea afectada.

**Por qué el planteo viejo estaba mal.** La versión anterior de este caso hacía el `PATCH`
prohibido con el **service principal** de este set (`# PowerPlatform-Dev`, `dataverse_api.py`),
que tiene `applicationid`. `ListaBlancaStep.Execute` (`src/Sanic.Mppp.Plugins/Steps/StepsDePlataforma.cs`)
empieza así:

```csharp
if (BaseDeStep.EsCodigoDeServidor(contexto, servicio))
{
    // 04 §1: el código de servidor no pasa por la lista blanca.
    return;
}
```

y `BaseDeStep.EsCodigoDeServidor` trata como "código de servidor" a **toda identidad con
`applicationid`**, no solo a SYSTEM — es exactamente lo que dice `diseno/04-matriz-privilegios.md`
§1 ("Las dos claves alternativas..." — la exención de identidad de aplicación es la misma que ya
usa I-12 para la puerta del RPA). El `HTTP 204` que el caso viejo registraba como "defecto" era
el sistema haciendo lo que el diseño pide: el service principal **tiene que** poder escribir
cualquier columna, porque es código de servidor a todos los efectos. El diagnóstico viejo nunca
midió lo que decía medir. Y `ReglasDeSteps.cs` ya lo advertía desde el 2026-09-23 (comentario sobre
`PermitidasEnFila`): *"todos los actores probados hasta entonces quedaban exentos de la lista
blanca (el service principal por su `applicationid`, el administrador por ser `servicio.cuenta`,
los plugins por su `Depth`). El primer humano que escribió una Fila desde la app chocó con esto de
entrada."* Nadie había probado la lista blanca con un humano hasta ahora.

**Necesita.** Una Fila propia de este caso (`ctx.procesar()` con `E01-feliz-formato11.xlsx`) y los
roles de Ejecutivo (`User1`) y Administrador de planes (`User3`), verificados EN VIVO contra
`systemuserroles_association` dentro del propio caso (mismo criterio que I-13: si el rol no
coincide, el caso da **N-A**, no un resultado que mentiría sobre lo que se probó).

**Cómo (4 pasos, verificados en el entorno el 2026-09-27, `MSCRMCallerID` para impersonar).**

| # | Quién | Qué | Qué pasó | Interpretación |
|---|---|---|---|---|
| 1 | Service principal (`applicationid`, sin impersonar) | `PATCH sanic_referencia` = `"ZZ-INTENTO-SP"` | **HTTP 204** — se guardó | Esperado: exento por diseño (código de servidor). **No mide la lista blanca.** |
| 2 | **`User1` (Ejecutivo, humano)** | `PATCH sanic_referencia` = `"ZZ-INTENTO-DIRECTO"` | **HTTP 400**: *"No se puede guardar: la(s) columna(s) sanic_referencia no está(n) permitida(s) para este usuario."* | **La lista blanca FUNCIONA.** Un humano no puede saltarse el formulario. |
| 3 | `User3` (Administrador de planes, borde 1) | `PATCH sanic_notainterna` = `"prueba de borde I-07"` | **HTTP 403**: *"...is missing prvReadsanic_mppp_tbl_fila privilege..."* | Lo frena la **falta de privilegio** (04 §2) — nunca llega a `ListaBlancaStep`. Protección distinta, y actúa antes. |
| 4 | `User1` (Ejecutivo, borde 2) | `PATCH sanic_notainterna` = `"prueba I-07 columna permitida"` | **HTTP 204** — se guardó | La columna permitida **sí** pasa: el step no es más restrictivo que lo que dice el diseño. |

**Qué tiene que pasar.** El paso 2 (el que en verdad mide `ListaBlancaStep` contra un humano) tiene
que dar HTTP 400 con un mensaje que nombre `sanic_referencia`. Los pasos 1, 3 y 4 son de control:
confirman que la exención de diseño existe, que el borde de privilegio actúa antes que la lista
blanca, y que la columna permitida no se ve afectada.

**Conclusión: NO ES UN DEFECTO. I-07 pasa a OK.** `ListaBlancaStep` protege exactamente lo que
`04` §1 dice que protege, contra la identidad que en verdad importa (un humano con rol de negocio).
El defecto que se había registrado en `diseno/PENDIENTES.md` (entrada `I-07`) era un artefacto de
probar con la identidad equivocada — corresponde cerrar esa entrada con esta evidencia.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py I` (caso `I-07`) — tiene que dar
**OK**. Cuatro checks: el service principal exento (204, informativo), el Ejecutivo humano
rechazado (400, con `sanic_referencia` en el mensaje), el Administrador de planes frenado por
privilegio (401/403) y el Ejecutivo escribiendo la columna permitida (204).

**Cómo se sabe que falló.** Si el paso 2 (Ejecutivo) da 200/201/204, la lista blanca **sí** tiene
un agujero real con un humano — eso sería grave (cualquier Ejecutivo con acceso al Web API podría
escribir `sanic_referencia` a mano) y hay que reabrir la investigación con el `plugintracelog`. Si
el paso 3 (Administrador de planes) da otro HTTP que no sea 401/403 (por ejemplo 400 con el mensaje
de la lista blanca), significa que SÍ tiene privilegio de escritura sobre Fila cuando no debería
(revisar la matriz `04` §2 contra el entorno). Si el paso 4 (columna permitida) se rechaza, el step
es más restrictivo que el diseño y hay que revisar `ReglasDeSteps.PermitidasEnFila`.

**Automatizable.** API (`correr.py I`, caso `I-07`).

---

## I-08 · Transición de estado ilegal (Validada → Aprobada, saltando Digitada)

**Qué prueba.** `TransicionDeFilaStep`: un salto que no existe en la tabla de transiciones
(`Dominio/TransicionesDeFila.cs`) se rechaza **siempre**, sea cual sea el rol de quien lo intenta —
`(Validada, Aprobada)` no es una clave de esa tabla, así que la respuesta es "Transición no
permitida" antes incluso de mirar roles.

**Necesita.** Una Fila propia de este caso, en Validada (`ctx.procesar()` con
`E01-feliz-formato11.xlsx`).

**Cómo.** `PATCH` directo por Web API a `sanic_estado = Aprobada` sobre una Fila en Validada
(saltando Digitada), sin pasar por ningún botón.

**Qué tiene que pasar.** **No deja**: HTTP 400, con un mensaje que dice que esa transición no es
válida desde ese estado.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py I` (caso `I-08`).

**Cómo se sabe que falló.** Si el `PATCH` da 200/204, **la seguridad no estaba en el botón**:
cualquiera con acceso al Web API y privilegio de escritura sobre Fila puede aprobar lo que quiera,
saltándose el circuito completo de digitación. Es el caso más grave de todo este archivo si falla.

**Automatizable.** API (`correr.py I`, caso `I-08` — el caso que demuestra que la seguridad no está
en el botón).

---

## I-09 · El paquete de plugins está al día

**Qué prueba.** Que el ensamblado en el sandbox tenga registrados exactamente los tipos de plugin
del repositorio — **nombrados, no contados** (CONVENCIONES §7: la versión anterior de este caso
decía "10 tipos"; hoy hay 15, desde DN-04).

**Necesita.** Credenciales de lectura en Dataverse (las que ya usa `preparar()` de este set).

**Cómo.**

```bash
python3 herramientas/construir/paquete_plugins.py playbooks/paquete-plugins/sanic_mppp_pkg_plugins.md --solo-verificar
```

y además, dentro del caso, `GET plugintypes` filtrado por el ensamblado, para nombrar cada uno.

**Qué tiene que pasar.** `"estado": "ya_existia"` (la herramienta ya compara por NOMBRE lo que
falta y lo que sobra — D-57 c, no un conteo), **y** cada uno de estos 15 tipos presente en
`plugintypes`, **sin ninguno de más**:

`Sanic.Mppp.Plugins.Steps.ListaBlancaStep` · `NormalizarYValidarStep` · `NombreCalculadoStep` ·
`TransicionDeFilaStep` · `PostTransicionDeFilaStep` · `AtenderPorClasificarStep` ·
`RevisionAtendidaStep` · `IntegridadDeReglaStep` — y `Sanic.Mppp.Plugins.Api.ClasificarCorreoApi` ·
`ExportarParaDigitarApi` · `ObtenerFilasPendientesApi` · `RegistrarResultadoRpaApi` ·
`ValidarSolicitudApi` · `CargarCatalogosApi` · `CambiarEstadoCatalogosApi` (estas dos, desde DN-04, 2026-10-01).

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py I` (caso `I-09`).

**Cómo se sabe que falló.** Si falta un nombre puntual, ESE es el tipo que no se subió al sandbox
(revisar el `.nupkg`, no todo el paquete). Si "sobra" un nombre, es una derivación que el playbook
no declara (D-57 c: pasó una vez con `RevisionAtendidaStep`) — no es necesariamente un defecto,
pero hay que decidir si se agrega al playbook o se borra del entorno. **Cuidado con la trampa
conocida**: el sandbox puede seguir corriendo el binario viejo si no se subió el número de build; el
síntoma es *"The plug-in type could not be found in the plug-in assembly"*.

**Automatizable.** API (`paquete_plugins.py --solo-verificar`; `correr.py I`, caso `I-09`).

---

## I-10 · Los 20 registros de step de plugin, uno por uno

**Qué prueba.** Que los **20 registros** de `playbooks/steps/sanic_mppp_pkg_plugins.md` (inventario
8.2) existan en vivo, **nombrados uno por uno**, cada uno con su mensaje, su tabla y su etapa
exactos — no un conteo (CONVENCIONES §7). Reemplaza a la vieja tabla de prosa "8 steps registrados"
de este archivo, que solo describía qué protege cada CLASE sin verificar nada contra el entorno.

**Necesita.** Credenciales de lectura en Dataverse.

**Cómo.** Por cada uno de los 20 nombres de `playbooks/steps/sanic_mppp_pkg_plugins.md` §2, `GET
sdkmessageprocessingsteps?$filter=name eq '<nombre>'` con `$expand` de `sdkmessageid` (el mensaje) y
`sdkmessagefilterid` (la tabla).

**Qué tiene que pasar.** Los 20, cada uno **habilitado** (`statecode = 0`) y con su mensaje, su
tabla y su etapa exactos:

| Step (nombre exacto) | Mensaje | Tabla | Etapa |
|---|---|---|---|
| `MPPP - Lista blanca - Update de Fila` | Update | `sanic_mppp_tbl_fila` | PreOperation |
| `MPPP - Lista blanca - Update de Solicitud` | Update | `sanic_mppp_tbl_solicitud` | PreOperation |
| `MPPP - Normalizar y validar - Create de Cliente` | Create | `sanic_mppp_tbl_cliente` | PreOperation |
| `MPPP - Normalizar y validar - Update de Cliente` | Update | `sanic_mppp_tbl_cliente` | PreOperation |
| `MPPP - Normalizar y validar - Create de Plan` | Create | `sanic_mppp_tbl_plan` | PreOperation |
| `MPPP - Normalizar y validar - Update de Plan` | Update | `sanic_mppp_tbl_plan` | PreOperation |
| `MPPP - Normalizar y validar - Create de Autorizado` | Create | `sanic_mppp_tbl_autorizado` | PreOperation |
| `MPPP - Normalizar y validar - Update de Autorizado` | Update | `sanic_mppp_tbl_autorizado` | PreOperation |
| `MPPP - Normalizar y validar - Create de Parametro` | Create | `sanic_mppp_tbl_parametro` | PreOperation |
| `MPPP - Normalizar y validar - Update de Parametro` | Update | `sanic_mppp_tbl_parametro` | PreOperation |
| `MPPP - Nombre calculado - Create de Plan` | Create | `sanic_mppp_tbl_plan` | PreOperation |
| `MPPP - Nombre calculado - Create de Autorizacionplan` | Create | `sanic_mppp_tbl_autorizacionplan` | PreOperation |
| `MPPP - Nombre calculado - Create de Autorizacioncliente` | Create | `sanic_mppp_tbl_autorizacioncliente` | PreOperation |
| `MPPP - Nombre calculado - Create de Fila` | Create | `sanic_mppp_tbl_fila` | PreOperation |
| `MPPP - Transicion de Fila - Update` | Update | `sanic_mppp_tbl_fila` | PreOperation |
| `MPPP - Post transicion de Fila - Update` | Update | `sanic_mppp_tbl_fila` | **PostOperation** |
| `MPPP - Atender correo por clasificar - Update de Solicitud` | Update | `sanic_mppp_tbl_solicitud` | PreOperation |
| `MPPP - Revision atendida - Update de Solicitud` | Update | `sanic_mppp_tbl_solicitud` | PreOperation |
| `MPPP - Integridad de Regla - Create` | Create | `sanic_mppp_tbl_regla` | PreOperation |
| `MPPP - Integridad de Regla - Update` | Update | `sanic_mppp_tbl_regla` | PreOperation |

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py I` (caso `I-10`).

**Cómo se sabe que falló.** El check nombra el step exacto que no coincide (no aparece, o aparece
con otro mensaje/tabla/etapa/deshabilitado): ESE es el que hay que revisar en el entorno, sin tener
que adivinar cuál de los 20.

**Automatizable.** API (`correr.py I`, caso `I-10`).

---

## I-11 · Integridad de una autorización por CLIENTE (duplicada, fase 2 DN-01)

**Qué prueba.** La clave alternativa nueva `sanic_mppp_key_autorizacioncliente_autorizado_cliente`
sobre AutorizacionCliente (inventario 5.11, DN-01) — la vía por cliente agregada en fase 2, hermana
de la que prueba I-05 para AutorizacionPlan.

**Necesita.** El Cliente (`prep.CLIENTE`) y el Autorizado (`REMITENTE`) que `preparar()` de este set
deja listos.

**Cómo.** Crea una **AutorizacionCliente** (`REMITENTE` → `prep.CLIENTE`); después intenta crear
una **segunda**, con el mismo autorizado y el mismo cliente.

**Qué tiene que pasar.** La primera se crea sin problema. La segunda **no deja guardar** (por la
clave alternativa): HTTP distinto de 200/201/204, con un mensaje entendible y **no** un volcado
técnico de la plataforma.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py I` (caso `I-11`).

**Cómo se sabe que falló.** Si la precondición (la primera AutorizacionCliente) no se pudo crear,
el caso lo reporta como tal, sin llegar a probar nada más. Si la SEGUNDA se guarda (HTTP
200/201/204), la clave alternativa de fase 2 no está frenando el duplicado — el caso la borra de
inmediato. Si se rechaza pero con un volcado técnico crudo, el rechazo funciona pero el mensaje no
sirve para un Administrador de planes.

**Automatizable.** API (`correr.py I`, caso `I-11`).

---

## I-12 · La puerta del RPA rechaza a quien no es el bot (fase 2, D-9/D-44)

**Qué prueba.** `PuertaDelRpa.Exigir` (`src/Sanic.Mppp.Plugins/Steps/PuertaDelRpa.cs`): las dos
Custom API de fase 2 (`sanic_mppp_capi_obtenerfilaspendientes`,
`sanic_mppp_capi_registrarresultadorpa`) llevan `ExecutePrivilegeName` **vacío a propósito** (D-9);
la puerta es el código, con dos condiciones obligatorias: identidad de aplicación **y** rol `SR -
MPPP - RPA`.

**Necesita.** Nada además de las credenciales que ya usa este set. El service principal de Dev (`#
PowerPlatform-Dev`) **es** una identidad de aplicación (tiene `applicationid`), pero **solo tiene
el rol `System Administrator`** — verificado en vivo el 2026-09-27 contra
`systemuserroles_association` — así que es exactamente el llamador que la puerta tiene que
rechazar. No hace falta (ni se usan) las credenciales reales del bot (`local/rpa_cred.env`).

**Cómo.** `GET sanic_mppp_capi_obtenerfilaspendientes(claimminutos=5)` con las credenciales del
service principal de este set.

**Qué tiene que pasar.** **No deja pasar**: HTTP 400, con un mensaje que trae el código
`MPPP-RPA-006` (identidad de aplicación sin el rol) — o `MPPP-RPA-007` si algún día el rol no
existiera en el entorno. Ninguna Fila se toca: la puerta corre antes de cualquier lógica de
negocio.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py I` (caso `I-12`).

**Cómo se sabe que falló.** Si el `GET` da 200 (la Custom API respondió filas), la puerta no está
frenando y **cualquier identidad de aplicación sin el rol puede reservar y leer filas pendientes de
digitar** — hay que revisar de inmediato si además reservó un claim sobre filas reales (columna
`sanic_claimhasta`) y liberarlas a mano. Si el HTTP es 400 pero el mensaje no trae ninguno de los
dos códigos `MPPP-RPA-0*`, el rechazo funciona pero por otro motivo (por ejemplo, un parámetro mal
formado) — no confirma que la puerta sea la que frenó.

**Automatizable.** API (`correr.py I`, caso `I-12`).

---

## I-13 · El claim se libera en un estado terminal, y no antes (fase 2, `03` §2)

**Qué prueba.** El comportamiento del claim dentro de `TransicionDeFilaStep`: mientras la Fila
sigue en un estado "abierto" (Validada, Digitada) el claim **no se toca**; al llegar a un estado
terminal (Aprobada, Rechazada en AS400, Anulada) el claim **se libera** (`sanic_claimhasta =
null`), sea cual sea quien hizo la transición.

**Necesita.** Los roles de Ejecutivo (`User1`) y Supervisor (`User2`), verificados EN VIVO contra
`systemuserroles_association` dentro de este mismo caso (no en `preparar()`: es el único caso de
todo el set que impersona un rol de negocio — CONVENCIONES §8, "no supuesto"); una Fila propia de
este caso, en Validada (`ctx.procesar()` con `E01-feliz-formato11.xlsx`).

**Cómo.**

1. `PATCH` directo (service principal, sin impersonar) de `sanic_claimhasta` a un instante **5
   minutos en el pasado** (ya vencido) — esta columna no está en la lista blanca de una persona,
   solo la escribe código de servidor.
2. Impersonando a **Ejecutivo** (`MSCRMCallerID`), `PATCH sanic_estado = Digitada` (Validada →
   Digitada).
3. Impersonando a **Supervisor** (otro usuario: segregación de funciones), `PATCH sanic_estado =
   Aprobada` (Digitada → Aprobada).
4. Lee `sanic_claimhasta` después de cada paso.

**Qué tiene que pasar.**

| Paso | `sanic_claimhasta` |
|---|---|
| Después de sembrar el claim vencido | el valor sembrado (pasado) |
| Después de Validada → Digitada | **el mismo valor** (sin tocar: sigue "abierta") |
| Después de Digitada → Aprobada | **vacío** (liberado: es terminal) |

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py I` (caso `I-13`).

**Cómo se sabe que falló.** Si el claim se BORRA en Validada → Digitada, el step está liberando de
más (un bot que reservó una fila para trabajarla perdería la reserva a mitad de camino). Si el
claim SIGUE presente después de Digitada → Aprobada, el step no libera en un estado terminal, y una
fila ya aprobada quedaría "reservada" para siempre, bloqueando cualquier lectura futura del bot
sobre esa fila. Si User1/User2 no tienen los roles esperados, el caso da **N-A** (no FALLA): con
otros roles dan 400 por falta de rol y el resultado mentiría sobre lo que en realidad se probó.

**Automatizable.** API (`correr.py I`, caso `I-13`).

---

## Resultados

| Caso | Resultado | Notas |
|---|---|---|
| I-01 regla sin evaluador | | |
| I-02 dependencia inactiva | | |
| I-03 efecto inválido por nivel | | |
| I-04 autodependencia | | |
| I-05 autorización por plan duplicada | | |
| I-06 nombre calculado (4 tablas) | | |
| **I-07 lista blanca (humano vs. identidad exenta)** | | **OK — no es defecto (reescrito 2026-09-27); falta que se cierre `diseno/PENDIENTES.md` entrada I-07** |
| **I-08 transición ilegal** | | |
| I-09 paquete al día (15 tipos) | | |
| I-10 los 20 steps, uno por uno | | |
| I-11 autorización por cliente duplicada (fase 2) | | |
| I-12 puerta del RPA (fase 2) | | |
| I-13 claim: no toca / libera (fase 2) | | |

> Dejá el catálogo de **Reglas** como estaba: `PLAN_EXISTE` activa, `MONEDA_DEL_PLAN` con efecto
> Rechaza, y sin la regla inventada de I-01. Los propios casos lo revierten en `finally`; esta nota
> es para quien inspeccione el entorno después de una corrida interrumpida (`Ctrl-C`, timeout).
