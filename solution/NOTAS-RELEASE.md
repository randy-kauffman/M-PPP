# Notas de release — borrador para el aprobador

**Este documento es un borrador de trabajo, no un release publicado.** Nadie
hizo `git push`, ni tag, ni GitHub release: eso queda para quien apruebe.

Preparado el 2026-09-28, rama `cierre/fase-1`, contra el release anterior
**`v1.0.0`** (2026-09-24, "Fase 1 — solución unmanaged 1.0.0.0").

---

## 1. Qué cambió desde v1.0.0

### Fase 2 completa: bot RPA
Automatización de la digitación en AS400 vía dos Custom API nuevas:

- **`sanic_mppp_capi_obtenerfilaspendientes`** — el bot pide filas pendientes (FIFO
  por fecha de validación + número de fila), con `claim` temporal para que dos
  corridas no se pisen.
- **`sanic_mppp_capi_registrarresultadorpa`** — el bot informa el resultado por
  lote (digitada / rechazada), traducido con el catálogo `rpa.codigos`.

Incluye rol dedicado `SR - MPPP - RPA` (0 privilegios sobre tablas MPPP — el bot
entra solo por las dos Custom API), app user `# MPPP-RPA`, y el dominio de reglas
del bot (`PuertaDelRpa`, `ReglasDelRpa`, `SeleccionParaElRpa`, `FilaParaElRpa`)
construido con TDD.

### DN-01 · Autorización por cliente
La autorización de un correo remitente deja de ser solo "por plan" — ahora puede
ser **"por cliente"** (autoriza sobre todos los planes de ese cliente, sin
enumerarlos). El modo lo decide un parámetro (`autorizacion.modo`, sembrado en
`plan` = comportamiento de hoy); `AutorizacionPlan` **se conserva** en modo
cliente, no se migra ni se pierde. El mensaje al cliente no cambia en ningún
modo.

### DN-02 · Exportar a Excel para digitar
El ejecutivo puede bajar en un `.xlsx` las filas seleccionadas para digitarlas en
lote fuera de la app, en el orden de columnas que define el parámetro
`exportacion.columnas` (JSON, editable sin tocar código). Se arma en C# con Open
XML SDK vía la Custom API `sanic_mppp_capi_exportarparadigitar` y un comando
nuevo en el ribbon.

### DN-03 · Obligatoriedad por gestión
`plantilla.obligatoriedad` agrega `porGestion`: un ajuste más simple que las
`reglas` existentes, que aflojan o endurecen campos obligatorios solo por
Gestión (Inclusión/Exclusión/Modificación), sin reemplazar el mecanismo
anterior — conviven. Sembrado con `Exclusion` (cuenta, banco y moneda
opcionales). Un parámetro sin `porGestion` se comporta exactamente como antes.

### Arnés de pruebas
**149 casos en 11 sets**, varios con navegador real (Playwright) para lo que solo
un clic de verdad puede probar (la "X" de cerrar un diálogo, el DOM del ribbon).
Incluye el set B (automatización del bot RPA contra la plataforma) y el set V
(vigilancia / recuperación de fallas de flujo).

---

## 2. Los nueve defectos reparados (`diseno/PENDIENTES.md`)

| # | Qué era | Cómo quedó |
|---|---|---|
| D-59 | `Avisos.cs`: `InvalidCastException` (Guid→EntityReference) tumbaba SIEMPRE el aviso a ejecutivos cuando un correo no se podía procesar — justo el caso para el que existe el aviso. | Corregido con TDD; el doble de prueba propio (no FakeXrmEasy) repetía la misma equivocación en dos fixtures, también corregidas. Ensamblado `1.0.16.0`. |
| D-60 | `StepsDeFila.cs`: una transición del RPA a `RechazadaEnAS400` (el camino MÁS común del bot) leía `sanic_digitadapor` como proxy de identidad; esa columna es NULL si la fila nunca pasó por Digitada, y el rechazo se caía SIEMPRE aunque el bot ya hubiera pasado la puerta de seguridad. Incluía además B-17 (un ítem bueno se perdía en un lote mixto). | `ResolverIdentidadDelActor` ya no inventa identidad; confía en el RPA solo para la combinación verificada. Ensamblado `1.0.18.0`. |
| D-61 | Contrato de `obtenerfilaspendientes` pedía a la vez "`maximo` ausente → 100" y "`0` → error", imposible: en una Function ausente ES `0`. | Decisión del aprobador: `0` pasa a significar el defecto (100); un negativo sigue siendo error. Ensamblado `1.0.17.0`. |
| D-62 | `mppp-vig.json`: la etapa de arranque (5 eslabones) solo encadenaba con `"Succeeded"` — si fallaba cualquiera, los 5 bloques de recuperación quedaban `Skipped` esa corrida entera. | Se agregó `"Failed"` a los 5 `runAfter`, más control de concurrencia (`runs=1`) en el trigger. |
| D-63 | `sanic_requiereatencionmanual` faltaba en la lista blanca de escritura de Fila: el botón "Atendida" daba HTTP 400 y dejaba filas trabadas (el bot no las podía tomar, la persona no las podía liberar). | Agregada a `PermitidasEnFila`. Ensamblado `1.0.17.0`. |
| D-64 | DA-08 define 5 avisos en la campana de la app; solo existía 1. | Se implementaron 2 más ("filas por digitar", "filas por aprobar"); los otros 3 se descartaron por decisión del aprobador — ya están cubiertos por vistas de menú agregadas el 2026-09-27, y un aviso que duplica una vista es ruido. Ensamblado `1.0.19.0` (el ensamblado actual). |
| D-65 | Faltaba `prvReadappnotification` en los roles Ejecutivo/Supervisor: la campana no podía CREAR notificaciones aunque D-59 ya estuviera resuelto (error tapaba error). | Otorgado por playbook de rol, no a mano en el portal. |
| D-66 | Re-ingesta de `MPPP-VIG` fallaba con 0% de éxito (los IDs de mensaje de Outlook dejaban de ser válidos entre leerlos y re-leerlos). | Corregido por el aprobador en el `MoveV2` del flujo; verificado por tasa de error (no por total acumulado — la primera medición se equivocó comparando dos acumulados tomados en momentos distintos). |
| D-67 | Se creyó que el trigger de `MPPP-VIG` había dejado de dispararse (5.5 horas sin marca en Bitácora). | El aprobador corrigió el flujo; verificado con eventos reales de Bitácora. Nota de método: el silencio de VIG no prueba por sí solo que esté caído, solo que no tuvo nada que recuperar. |

**I-07** (no listada arriba porque **no era un defecto**): se sospechaba que
`ListaBlancaStep` no protegía escrituras directas por Web API. Era una
identidad exenta por diseño (el propio service principal del arnés de pruebas
cuenta como "código de servidor"); rehecho impersonando usuarios humanos reales,
la lista blanca funciona como se diseñó.

> **Nota de verificación del coordinador de este release**: la consigna original
> decía "D-59 a D-66, más I-07". `PENDIENTES.md` muestra **D-67 también CERRADA
> el 2026-09-28**, con el mismo patrón de evidencia que las otras ocho (commit,
> ensamblado, verificación independiente). Se cuenta acá porque la tabla de
> arriba ya suma nueve reparadas de punta a punta (D-59 a D-67); si el rango
> pretendía excluir a D-67 a propósito, hace falta que el aprobador lo confirme.

---

## 3. Lo que queda ABIERTO (`diseno/PENDIENTES.md`)

| # | Qué | Estado |
|---|---|---|
| D-68 | La RED 2 de V-02 (detectar al arrancar que un flujo quedó apagado por una corrida que murió de golpe) nunca se ejercitó: el sistema de permisos bloqueó el PATCH manual necesario para probarla. | **Riesgo residual ACEPTADO por el aprobador** el 2026-09-28. La RED 1 (restaurar en el `finally`) sí se probó y funcionó. |
| D-69 | `AutorizacionCliente` — comandos genéricos ("Nuevo", etc.) siguen visibles en el ribbon aunque el bloque de ocultos ya está bien importado y verificado por Web API (`RetrieveEntityRibbon`). | **Reparado en código, abierto a la espera de UN CLIC MANUAL**: un administrador de plataforma tiene que entrar con `&ribbondebug=true` y usar "Regenerate ribbon metadata" — es de plataforma, ninguna herramienta de este repo lo puede hacer. El caso A-02 queda en rojo hasta ese clic. |
| D-70 | El arnés de navegador (`pruebas/gui`) no encuentra la entrada "Correos Autorizados" del sitemap. | **Abierto, menor.** Sin confirmar si es el nombre viejo del sitemap o el selector. |
| D-71 | Los 4 flujos vivos (REC, COM, ING, VIG) **no tienen desplegado** el control de concurrencia (`runtimeConfiguration.concurrency.runs`) que `recursos/flujos/*.json` sí declara. | **Abierto.** Contradice una verificación previa (D-62) que se dio por buena sin repetirse; causa sin confirmar (¿un ajuste manual sobre VIG lo reescribió? ¿`flujo.py --completar` no lo aplica aunque reporte éxito?). Sin control de concurrencia, dos corridas del mismo flujo pueden solaparse — ya hubo evidencia de eso en `MPPP-VIG` antes de D-62. |

**Un release que solo cuenta lo bueno es propaganda, no un release.** Estos
cuatro puntos, sobre todo D-69 (un caso de prueba en rojo) y D-71 (un riesgo de
concurrencia real, sin mitigar, en los cuatro flujos vivos), son candidatos a
condición de aceptación antes de publicar, no solo a nota al pie.

---

## 4. Versión propuesta

**Hallazgo previo a la propuesta**: el campo `version` de la solución en
Dataverse **sigue en `1.0.0.0`** — nunca se incrementó desde `v1.0.0`, a pesar
de toda la fase 2. Es distinto del *ensamblado* de plugins (`Sanic.Mppp.Plugins.dll`),
que sí avanzó release a release y hoy está en **`1.0.19.0`** (`AssemblyVersion`/
`FileVersion` del `.csproj`, verificado además dentro del `.nupkg` exportado hoy:
el binario trae literalmente la cadena `1.0.19.0`, no `1.0.0.0`). Son dos
números independientes y hoy están desalineados. **No toqué el campo `version`
de la solución en Dev** — el export de hoy sigue llevando `1.0.0.0` adentro,
a propósito, para no tomar por mi cuenta una decisión que es del aprobador.

**Propuesta: `2.0.0.0`.**

Por qué no es un parche ni un minor menor:
- Entró una **fase entera** (fase 2: automatización con bot, identidad y rol
  propios, dos Custom API nuevas) — no un fix, no una feature chica.
- Entraron **tres directivas de negocio** (DN-01, DN-02, DN-03) que tocan el
  corazón del sistema: el modo de autorización (`AUTORIZACION_CORREO_PLAN`
  cambia de vía según parámetro), una vía de exportación de datos sensibles
  nueva, y una segunda forma de calcular obligatoriedad.
- **Cierra el alcance original del proyecto**: la fase 3 quedó formalmente
  descartada (`DIRECTRICES-NUEVAS.md`), así que esta no es "una fase más": es
  la versión que entrega TODO lo que el proyecto se propuso, contra `v1.0.0`
  que era fase 1 nada más.
- Se repararon 9 defectos reales, varios graves (D-59, D-60 rompían caminos
  centrales: avisos y el rechazo del bot).

**Alternativa más conservadora, con su tradeoff**: `1.1.0.0` (minor), si se
interpreta semver estricto — nada de lo nuevo rompe un consumidor existente:
DN-01 nace con el parámetro en `plan` (comportamiento de hoy), DN-03 sin
`porGestion` se comporta igual que antes, y el bot es una capacidad ADITIVA
(nadie que no lo use nota diferencia). El tradeoff: un `1.1.0.0` no comunica
que el proyecto cerró su alcance completo, y alguien que solo mire el número
puede subestimar el tamaño del cambio.

**Decide el aprobador.** Si elige `2.0.0.0` o cualquier otro número, hace falta
además **actualizar el campo `version` de la solución en Dev** (Web API o
`pac`, fuera del alcance de este agente) y volver a exportar — los dos `.zip`
de hoy quedan con `1.0.0.0` adentro y sirven de base de contenido, no de
paquete final con el número ya puesto.

---

## 5. Artefactos de este release, en `solution/`

| Archivo | Qué es | Verificado |
|---|---|---|
| `sanic_mppp_sol_mantenimientoppp_1_0_0_0_fase2cierre_20260928_unmanaged.zip` | Export unmanaged, `ExportSolution` (`Managed: false`), publicado ANTES de exportar (`PublishAllXml`). | `solution.xml`: `UniqueName=sanic_mppp_sol_mantenimientoppp`, `Version=1.0.0.0`, `Managed=0`. Trae las 5 Custom API (incluidas `exportarparadigitar`, `obtenerfilaspendientes`, `registrarresultadorpa`) y `sanic_mppp_tbl_autorizacioncliente`. El `.nupkg` de plugins trae el DLL con la cadena `1.0.19.0` (no `1.0.0.0`). |
| `sanic_mppp_sol_mantenimientoppp_1_0_0_0_fase2cierre_20260928_managed.zip` | Mismo export, `Managed: true`. | `solution.xml`: `Managed=1`, mismo `UniqueName` y `Version`, mismo contenido de fase 2 y mismo DLL `1.0.19.0`. |
| `sanic_mppp_tbl_parametros_activos_2026-09-28.xlsx` | 21 filas activas de `sanic_mppp_tbl_parametro` (19 nombres distintos; ver nota abajo). Columnas: Nombre, Versión, Tipo, Valor, Descripción. | Releído después de guardado; el valor de `servicio.cuenta` sale enmascarado. |
| `sanic_mppp_tbl_reglas_activas_2026-09-28.xlsx` | 15 filas activas de `sanic_mppp_tbl_regla` (todas las reglas activas del catálogo). Columnas: Código, Nivel, Orden, Efecto, Dependencias, Estado. | Releído después de guardado; ningún valor sensible. |

### Enmascarado en los Excel — qué se tapó y qué se revisó

- **`servicio.cuenta`** (pedido explícito): el valor real es el UPN de una
  cuenta de administrador del tenant. Enmascarado a
  `<enmascarado: UPN de la cuenta de servicio>`. Se dejó la fila con nombre,
  tipo (Texto) y descripción completos.
- **Revisión del resto** (los 19 nombres, valor por valor, no solo el
  nombre): ningún otro parámetro activo expone cuenta, dominio interno,
  tenant id ni URL de entorno. `rpa.codigos`, `plantilla.listas` y
  `exportacion.columnas` traen JSON de configuración de negocio (catálogos de
  banco, mensajes al cliente, mapeo de columnas) sin identificadores del
  tenant. Nada más se enmascaró.
- **Hallazgo adicional, sin enmascarar (no es un secreto, es un dato)**:
  `plantilla.obligatoriedad` y `rpa.lote.maximo` tienen **dos filas activas**
  cada uno (versión 1 y versión 2), cuando DD-02 dice explícitamente que
  cambiar de versión tiene que desactivar la anterior ("nunca se edita la
  vigente"). Los dos Excel muestran las dos versiones tal como están en Dev
  (fiel a "solo los activos"), pero esto es una inconsistencia de datos que
  vale la pena que alguien revise y desactive la versión vieja — no es parte
  de los D-68 a D-71 numerados, así que no se le abrió ticket por su cuenta.

---

## 6. Cómo verificar lo de arriba de nuevo

```
python3 - <<'PY'
import zipfile, re
for n in ["sanic_mppp_sol_mantenimientoppp_1_0_0_0_fase2cierre_20260928_unmanaged.zip",
          "sanic_mppp_sol_mantenimientoppp_1_0_0_0_fase2cierre_20260928_managed.zip"]:
    z = zipfile.ZipFile(f"solution/{n}")
    sol = z.read("solution.xml").decode()
    print(n, re.search(r"<Version>([^<]+)</Version>", sol).group(1),
          re.search(r"<Managed>([^<]+)</Managed>", sol).group(1))
PY
```
