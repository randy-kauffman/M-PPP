# 08 — Integridad, plugins y claves

Cubre los **steps de plugin** que corren al guardar, y que son la red que impide dejar el sistema
en un estado imposible. Son los que protegen contra alguien que escribe por el Web API sin pasar
por la app.

**Corregido: la tabla de abajo tenía un step que nunca se construyó y le faltaba uno que sí
existe.** Verificado contra el código (`src/Sanic.Mppp.Plugins/Steps/*.cs`) y en vivo contra Dev
(`plugintypes` filtrado por `Sanic.Mppp`): hay **13 tipos de plugin registrados en total** — estos
8 steps más las 5 Custom API (`ClasificarCorreoApi`, `ExportarParaDigitarApi`,
`ObtenerFilasPendientesApi`, `RegistrarResultadoRpaApi`, `ValidarSolicitudApi`, que no son steps y
no se prueban en este archivo). `IntegridadAutorizacionPlanStep` **no existe como clase en el
código** — nunca se construyó así; la integridad de una autorización duplicada (I-05) la hace la
**clave alternativa** `sanic_mppp_key_autorizacionplan_autorizado_plan`, no un plugin. En cambio
faltaba `RevisionAtendidaStep` (el que atiende el comando "Revisado", `diseno/05-app-model-driven.md`
§5): existe desde el 2026-09-23 y quedó fuera de esta tabla por error.

Los ocho steps registrados:

| Step | Qué protege |
|---|---|
| `NombreCalculadoStep` | el nombre de las tablas que no lo digita nadie |
| `NormalizarYValidarStep` | normaliza y valida al guardar |
| `ListaBlancaStep` | que solo se escriba lo que se puede escribir |
| `TransicionDeFilaStep` | que una transición de estado sea legal |
| `PostTransicionDeFilaStep` | los efectos posteriores (Bitácora, estado de la Solicitud) |
| `AtenderPorClasificarStep` | Atendido y Descartar |
| `IntegridadDeReglaStep` | que el catálogo de reglas sea coherente |
| `RevisionAtendidaStep` | deja en Bitácora el motivo que escribió la máquina, al usar "Revisado" |

> La integridad de una autorización duplicada (antes atribuida acá a
> `IntegridadAutorizacionPlanStep`) la prueba la **clave alternativa** sobre AutorizacionPlan, no
> un step: ver I-05 más abajo.

**Cómo se prueban.** Casi todos se prueban **haciendo algo prohibido** y verificando que el sistema
se niegue con un mensaje claro. Un plugin que deja pasar lo prohibido es peor que no tenerlo.

---

## I-01 · El catálogo de reglas: una regla activa sin evaluador

**Qué prueba.** `IntegridadDeReglaStep` (D-13): una regla activa cuyo código no sabe evaluar.

**Necesita.** Sesión `admin_tecnico` (Configuración → Reglas).

**Cómo.** En **Configuración → Reglas**, creá una regla nueva:

- Código: `REGLA_QUE_NO_EXISTE`
- Nivel: Registro · Efecto: Rechaza · Orden: 90

**Esperado.** **No deja guardar.** El mensaje dice que esa regla no tiene evaluador programado
para su nivel.

**FALLA si** guarda. La próxima validación reventaría con una excepción y todas las Solicitudes
quedarían en Ingresada.

**Automatizable.** API (`corredor.py`, caso I-01).

---

## I-02 · Una regla que depende de otra inactiva

**Qué prueba.** `IntegridadDeReglaStep`, segundo caso de D-13.

**Necesita.** Sesión `admin_tecnico`.

**Cómo.** Desactivá `PLAN_EXISTE` (que es de la que dependen cuatro reglas).

**Esperado.** **No deja desactivarla**, o no deja guardar la que queda colgando. El mensaje nombra
la dependencia rota.

> Acordate de dejar `PLAN_EXISTE` **activa** al terminar.

**Automatizable.** API (`corredor.py`, caso I-02).

---

## I-03 · Una regla de nivel Registro con efecto "Envía a revisión"

**Qué prueba.** D-20: ese efecto solo tiene sentido a nivel Solicitud o Correo.

**Necesita.** Sesión `admin_tecnico`.

**Cómo.** Editá `MONEDA_DEL_PLAN` (nivel Registro) y ponele efecto **Envía a revisión**.

**Esperado.** **No deja guardar**, con un mensaje que explique por qué.

> Dejá `MONEDA_DEL_PLAN` con efecto **Rechaza** al terminar.

**Automatizable.** API (`corredor.py`, caso I-03).

---

## I-04 · Una regla que depende de sí misma

**Qué prueba.** El defecto que encontró la revisión de código: la autodependencia se rechaza
**por el código**, antes de mirar ningún orden.

**Necesita.** Sesión `admin_tecnico`.

**Cómo.** Editá cualquier regla y poné en `sanic_dependede` **su propio código**.

**Esperado.** **No deja guardar**: *"No se puede guardar la regla 'X': no puede depender de sí
misma."*

> Probalo con una regla cuyo `orden` sea el mismo que tendría su "dependencia" — es el caso que una
> prueba mal escrita dejaba pasar por coincidencia.

**Automatizable.** API (`corredor.py`, caso I-04).

---

## I-05 · Integridad de una autorización

**Qué prueba.** **Corregido**: no hay un `IntegridadAutorizacionPlanStep` (esa clase no existe en
el código, ver la nota de arriba). Lo que realmente prueba este caso es la **clave alternativa**
`sanic_mppp_key_autorizacionplan_autorizado_plan` sobre AutorizacionPlan.

**Necesita.** Sesión `admin_planes` (Catálogos → Autorizaciones - Planes); una autorización
existente (P-03, TU-CORREO → PR11).

**Cómo.** En **Catálogos → Autorizaciones - Planes**, creá una autorización duplicada: el
**mismo** autorizado sobre el **mismo** plan que ya tiene (`TU-CORREO → PR11`).

**Esperado.** **No deja guardar** (por la clave alternativa), con un mensaje entendible y
**no** un volcado técnico de la plataforma.

**Automatizable.** API (`corredor.py`, caso I-05).

---

## I-06 · El nombre calculado

**Qué prueba.** `NombreCalculadoStep`.

**Necesita.** Sesión `admin_planes`.

**Cómo.** Creá una **Autorización** nueva (autorizado `TU-CORREO`, plan `PR06` si no existe ya) y
**no toques el campo Nombre**.

**Esperado.** Al guardar, el nombre queda calculado solo: **`correo → CODIGO`**.

Lo mismo con un **Plan**: el nombre se arma con su código y su cliente.

**Automatizable.** API (`corredor.py`, caso I-06).

---

## I-07 · Lista blanca de escrituras

**Qué prueba.** `ListaBlancaStep`: que no se pueda escribir a mano lo que escribe el sistema.

**Necesita.** Acceso al Web API con permiso de escritura sobre Fila (credenciales de servicio, o
una sesión con un rol que tenga `W` sobre Fila — Ejecutivo o Supervisor, `04`); una Fila existente
(la de C-01 alcanza). Para el lado de la app: sesión `ejecutivo` o `supervisor`.

**Cómo.** Abrí una **Fila** en la app e intentá editar a mano una columna que llena la validación
—por ejemplo `sanic_referencia`— y guardar. Además, probá un `PATCH` directo a esa columna por Web
API.

**Esperado.** **No deja**, o el campo está de solo lectura en el formulario **y** el plugin lo
rechaza si se intenta por API.

> **DEFECTO CONFIRMADO, no advertencia teórica.** Este archivo ya avisaba del riesgo; hoy está
> demostrado y automatizado en `herramientas/pruebas/corredor.py` (caso I-07): un `PATCH` directo a
> `sanic_referencia` por Web API se guarda con **HTTP 204** (éxito) en lugar de ser rechazado.
> `ListaBlancaStep` **no** bloquea esta columna. El formulario puede estar bien (de solo lectura),
> pero eso no alcanza: cualquiera con acceso al Web API y privilegio de escritura sobre Fila puede
> pisar una referencia que va a AS400. **Queda pendiente de decisión del aprobador**: si
> `ListaBlancaStep` tiene que extender su lista a `sanic_referencia` (y probablemente a las demás
> columnas que solo escribe la validación), o si el riesgo se acepta porque el privilegio de
> escritura ya está acotado a Ejecutivo/Supervisor por la matriz de `04`. No lo des por OK ni por
> FALLA vos mismo: es un hallazgo para que el aprobador decida, anotalo como tal en Resultados.

**Automatizable.** API (corredor.py, caso I-07 — ya demostrado; no hace falta correrlo de nuevo a
mano para confirmar el defecto).

---

## I-08 · Transición de estado ilegal

**Qué prueba.** `TransicionDeFilaStep`, que es lo que hace cierto DA-07.

**Necesita.** Acceso al Web API con permiso de escritura sobre Fila (o una sesión con rol
Ejecutivo/Supervisor) y una fila en Validada.

**Cómo.** Sobre una fila en **Validada**, intentá ponerla directo en **Aprobada** (saltándose
Digitada). Hacelo desde el formulario, no con el botón.

**Esperado.** **No deja**: el mensaje dice que esa transición no es válida desde ese estado.

**Este es el caso que demuestra que la seguridad no está en el botón.** Si pasa, alguien con el
Web API puede aprobar lo que quiera.

**Automatizable.** API (`corredor.py`, caso I-08 — **este es el caso que demuestra que la
seguridad no está en el botón**).

---

## I-09 · El paquete de plugins está al día

**Qué prueba.** Que el ensamblado en el sandbox sea el del repositorio, y no una versión vieja.

**Necesita.** Credenciales de lectura en Dataverse (o de servicio).

**Cómo.**

```bash
python3 herramientas/construir/paquete_plugins.py playbooks/paquete-plugins/*.md --solo-verificar
```

**Esperado.** **Corregido: decía "10 tipos", hoy son 13** (verificado en vivo el 2026-09-27, este
mismo comando: `"13 tipos de plugin registrados"` — 8 steps + 5 Custom API). `"estado":
"ya_existia"` con los **13 tipos** registrados.

> Cuidado con la trampa conocida: el sandbox puede seguir corriendo el binario viejo si no se
> subió el número de build. El síntoma es *"The plug-in type could not be found in the plug-in
> assembly"*, y la señal temprana es un `.nupkg` del mismo tamaño exacto que el anterior.

**Automatizable.** API (`paquete_plugins.py --solo-verificar`; `corredor.py`, caso I-09).

---

## Resultados

| Caso | Resultado | Notas |
|---|---|---|
| I-01 regla sin evaluador | | |
| I-02 dependencia inactiva | | |
| I-03 efecto inválido por nivel | | |
| I-04 autodependencia | | |
| I-05 autorización duplicada | | |
| I-06 nombre calculado | | |
| I-07 lista blanca | | |
| **I-08 transición ilegal** | | |
| I-09 paquete al día | | |

> Dejá el catálogo de **Reglas** como estaba: `PLAN_EXISTE` activa, `MONEDA_DEL_PLAN` con efecto
> Rechaza, y sin la regla inventada de I-01.
