# Directrices nuevas del negocio — cierre de la fase 2

Recibidas del aprobador el **2026-09-26**. Se resuelven **dentro de la fase 2**, y con eso se cierra
el proyecto.

> **La fase 3 queda DESCARTADA de este proyecto.** No es "pendiente": no va. Va a evolucionar a un
> proyecto nuevo, aparte, fuera de esta jurisdicción. Todo lo que en el diseño diga "fase 3" pasa a
> ser una nota histórica, no un compromiso.

---

## DN-01 · La autorización pasa a ser por CLIENTE, con un interruptor para volver a PLAN

**Lo que hay hoy.** Un correo autorizado se vincula a **planes**, uno por uno:

```
Autorizado (correo)  →  AutorizacionPlan  →  Plan
```

La regla `AUTORIZACION_CORREO_PLAN` comprueba que el correo remitente tenga una autorización sobre
**ese plan puntual**.

**Lo que pide el negocio.** Que la autorización sea **por cliente**:

- un correo puede estar relacionado con **más de un cliente**;
- y ese correo queda autorizado sobre **todos los planes de ese cliente**, sin enumerarlos.

**Y lo que pide además, y es lo que manda el diseño**: que más adelante pueda ser **por plan o por
cliente**, así que:

1. hay un **parámetro de configuración** que declara el modo: **por cliente** o **por plan**;
2. **la estructura de datos tiene que soportar los dos**, no uno migrado al otro.

### Lo que esto implica, para revisar con el aprobador

| # | Punto | Estado |
|---|---|---|
| 1 | Nace una vía nueva `Autorizado → Cliente` (varios a varios), y **convive** con `AutorizacionPlan`; ninguna reemplaza a la otra | **confirmado por el aprobador, 2026-09-26** |
| 2 | El modo lo decide **un parámetro**, no una columna por cliente: es uno solo para todo el entorno | **confirmado por el aprobador, 2026-09-26** |
| 3 | `AUTORIZACION_CORREO_PLAN` consulta **una vía u otra según el parámetro** — misma regla, misma posición en el orden, mismo efecto | **confirmado por el aprobador, 2026-09-26** |
| 4 | En modo **cliente**, las filas de `AutorizacionPlan` **se conservan** y no se miran. Cambiar el parámetro vuelve a activarlas sin recargar nada | **confirmado por el aprobador, 2026-09-26** |
| 5 | El **mensaje al cliente no cambia** en ningún modo. Sigue siendo el unificado del 2026-09-24 (`"El plan {plan} no existe o usted no está autorizado sobre él."`): distinguir "no existe" de "no autorizado" es el oráculo que ya cerramos | **no negociable** |
| 6 | La app necesita poder cargar y ver las autorizaciones por cliente, y el Administrador de planes es quien las administra (RF-11) | **cerrado 2026-09-27**: formulario nuevo `Autorizacion por cliente` (encabezado con `sanic_nombre`, `sanic_autorizadoid`, `sanic_clienteid`, y sección "Evidencia de la autorizacion" con `sanic_documentofirmado` y `sanic_fechadocumento` — ver decisión del aprobador arriba), subgrilla nueva "Clientes que puede modificar" en el formulario de Autorizado, y entrada de sitemap nueva "Autorizaciones - Clientes" con su propia vista por defecto ("Autorizaciones por cliente activas"); construido y verificado contra Dev (`playbooks/formulario/autorizacioncliente.md`, `playbooks/formulario/autorizado.md`, `playbooks/vista/clientes_que_puede_modificar.md`, `playbooks/vista/autorizaciones_por_cliente_activas.md`, `playbooks/app/mantenimientoppp.md`) |

---

## DN-02 · Descargar a Excel lo que hay que digitar, y trabajar en lote

**Por qué.** Los ejecutivos se van a **rotar** quién carga en AS400. El que le toca necesita bajarse
lo pendiente, digitarlo, y volver a marcarlo — sin ir fila por fila.

### Lo que el ejecutivo tiene que poder hacer

1. **Ver** las filas que todavía **no se digitaron**.
2. **Seleccionarlas todas.**
3. **Descargarlas en un Excel**, en un **orden de columnas que define un parámetro**: columna A = tal
   campo, columna B = tal campo, y así.
4. **Marcarlas todas como digitadas** — y si hubo una excepción, **desmarcar esa** y listo.

### Lo mismo para el supervisor

Una vista donde vea lo que tiene en sus manos, **seleccione todo y apruebe**, o rechace alguna suelta.

### Qué de esto YA EXISTE (verificado en `05`)

| Pieza | Estado |
|---|---|
| Vistas *Mis clientes — por digitar* y *Todos — por digitar* (Fila) | **existen** |
| Vista *Por aprobar* (Fila), entrada de sitemap del Supervisor | **existe** |
| Selección múltiple y **acción masiva** en los comandos | **existe** — el ribbon pasa `SelectedControlSelectedItemIds` y `comandos.js` aplica una petición por fila, en paralelo, informando cuáles fallaron y por qué |
| Comandos *Digitada*, *Aprobar*, *Devolver*, *Rechazada en AS400*, *Anular* | **existen**, todos masivos |

> **Lo de "seleccionar todas y marcarlas" ya está construido y probado.** Lo que NO existe es la
> **descarga a Excel**. Ese es el trabajo nuevo de DN-02.

### Lo que hay que construir, y lo que hay que decidir

| # | Punto | Estado |
|---|---|---|
| 1 | **Un parámetro nuevo** con el orden de columnas de la exportación — hermano de `plantilla.estructura`, pero de SALIDA | **confirmado por el aprobador, 2026-09-26** |
| 2 | **Cómo se genera el archivo.** Propuesta: una **Custom API** que recibe los `filaid` seleccionados y devuelve el `.xlsx` en base64, y un **comando** que lo descarga. El Excel se arma en C# con Open XML SDK, que ya se usa y ya se midió en el spike C-05 | **confirmado por el aprobador, 2026-09-26** |
| 3 | **Qué columnas se pueden elegir**: las de la Fila, incluidas `numeroidentificacion` y `numerocuenta`, que están **protegidas** por seguridad de columna | **confirmado por el aprobador, 2026-09-26** |
| 4 | **Ese archivo sale del sistema con datos sensibles en claro.** Hoy la protección de esas dos columnas es la razón de ser del perfil `CSP - MPPP - Datos sensibles` (RNF-04, BP-PP-004). Un Excel descargado **no lleva esa protección encima**: queda en la máquina de quien lo bajó. **Hay que decidirlo a conciencia, no de rebote** | **decisión del aprobador** |
| 5 | ¿La descarga **marca** algo (por ejemplo que ya se bajó y quién), o no deja rastro? | **confirmado por el aprobador, 2026-09-26** |

---

# Diseño resuelto — lo que se va a construir

Confirmado por el aprobador el 2026-09-26. **Son dos directrices, no tres.**

## DN-01 · Modelo

**Tabla nueva `sanic_mppp_tbl_autorizacioncliente`** — la vía por cliente, hermana de
`AutorizacionPlan` y con la misma forma:

| | |
|---|---|
| Lookups | `sanic_autorizadoid` → Autorizado · `sanic_clienteid` → Cliente (los dos referenciales) |
| Clave alternativa | autorizado + cliente (no se repite el par) |
| Primaria | nombre calculado `<correo> → <cliente>`, T(400), como en AutorizacionPlan |
| Privilegios | igual que AutorizacionPlan: Administrador de planes `C R W Ap At : O`; Ejecutivo y Supervisor `R:O` |

**Evidencia firmada — agregada el 2026-09-27, decisión del aprobador.** Esta tabla se construyó
primero tal cual el texto de arriba, sin `sanic_documentofirmado` ni `sanic_fechadocumento`, porque
este "Diseño resuelto" no las mencionaba (a diferencia de `AutorizacionPlan`, §2.4 del diccionario de
datos). El playbook de la tabla dejó constancia de esa ausencia como una nota explícita para el
aprobador, sin decidir por cuenta propia si era intencional o un hueco. El aprobador decidió: es un
hueco y hay que cerrarlo. Palabras del aprobador: *"Cuando entro a autorizacion x cliente, no esta la
columna de archivo adjunto para adjuntar la autorizacion por escrito y la fecha de ese documento (al
igual que en autorizacion por plan)."* **Motivo de negocio:** una autorización por cliente habilita
TODOS los planes de ese cliente de una vez, o sea que es MÁS amplia que la autorización por plan — no
tiene sentido que exija MENOS respaldo. Se agregaron `sanic_documentofirmado` (archivo, 10 MB, no
requerida, auditada) y `sanic_fechadocumento` (fecha, no requerida, auditada), con la misma forma que
sus pares de `AutorizacionPlan` (`playbooks/tabla/sanic_mppp_tbl_autorizacioncliente.md` §2).

**`AutorizacionPlan` NO se toca, NO se migra, NO se borra.** Las dos vías conviven y el parámetro
elige cuál se mira.

**Parámetro nuevo `autorizacion.modo`**, Texto, valores `cliente` o `plan`.
**Fallback quemado en el código: `plan`** — el comportamiento de hoy. Un parámetro que falta o viene
con basura **degrada al modo conocido, nunca tumba la validación** (mismo criterio que
`servicio.cuenta` y `rpa.horas.tolerancia`).

**La regla `AUTORIZACION_CORREO_PLAN` no cambia de código, de orden, de nivel ni de efecto.** Lo
único que cambia es dónde busca:

```
modo = plan     →  ¿hay AutorizacionPlan(correo, plan)?
modo = cliente  →  ¿hay AutorizacionCliente(correo, cliente dueño de ese plan)?
```

**El mensaje al cliente es el MISMO en los dos modos**, y es el unificado del 2026-09-24. **Esto no
se negocia**: un texto que distinga "el plan no existe" de "no estás autorizado" es un oráculo, y ya
lo cerramos una vez.

## DN-01 · El modo cambia TODO, no una parte

**Corrección del aprobador, 2026-09-26**, después de que el diseño inicial dejara `REMITENTE_RECONOCIDO`
afuera:

> *"El portero también tiene que preguntar lo mismo. Cuando cambien los modos hay que cambiarlo todo
> en su totalidad, no pedacito por pedacito."*

**Y tenía razón: un interruptor que cambia la mitad del sistema no es un interruptor, es una trampa.**
Con el modo en `cliente`, un correo autorizado solo por cliente era rechazado por
`REMITENTE_RECONOCIDO` **antes de abrir el Excel** — y una No reconocida **no recibe respuesta**, así
que el cliente mandaba su solicitud y no se enteraba de nada.

### Hay UN solo punto donde se consulta la autorización, y ahí va el modo

Barrido del código (2026-09-26): **una función, dos llamadores, ningún tercero**.

```
Datos/Catalogos.cs:209   PlanesAutorizadosDe(correo)
         ↑                          ↑
ClasificarCorreo.cs:242      ValidarSolicitud.cs:246
  REMITENTE_RECONOCIDO          AUTORIZACION_CORREO_PLAN
     (nivel Correo)                (nivel Registro)
```

**El modo se resuelve AHÍ, no en cada llamador.** `PlanesAutorizadosDe` devuelve, según
`autorizacion.modo`:

| modo | qué devuelve |
|---|---|
| `plan` | los planes de `AutorizacionPlan` para ese correo — **exactamente como hoy** |
| `cliente` | **todos los planes de los clientes** sobre los que ese correo tiene `AutorizacionCliente` |

Así los dos niveles contestan lo mismo **sin que ninguno tenga que saber del modo**, y **nadie puede
olvidarse de uno**: no hay dónde olvidarse. Si mañana aparece un tercer consumidor, hereda el modo por
construcción.

> **La prueba que lo fija**: con `modo = cliente` y un correo autorizado SOLO por cliente, el correo
> **entra** (pasa `REMITENTE_RECONOCIDO`) **y** sus filas quedan autorizadas. Los dos niveles, en la
> misma prueba. Si alguien vuelve a partir el modo en dos, esa prueba se pone roja.

---

## DN-02 · Exportación

**Custom API `sanic_mppp_capi_exportarparadigitar`** — recibe los `filaid` seleccionados, devuelve
el `.xlsx` en base64. Lo arma **C# con Open XML SDK 3.1.0**, que ya está referenciado en el ensamblado de producción, no
JavaScript: un `.xlsx` a mano en el navegador es frágil y no se agregan librerías al cliente.

**Parámetro nuevo `exportacion.columnas`**, JSON: la lista ordenada de campos, y **su orden ES el
orden de las columnas** del Excel. Hermano de `plantilla.estructura`, pero de salida.

> **Qué columnas salen lo decide ese parámetro, no el código.** Ahí está la respuesta a la pregunta
> de los datos sensibles: si el negocio quiere que el número de cuenta no salga, **se saca del
> parámetro**, sin tocar una línea. Se siembra con lo que el ejecutivo necesita para digitar en
> AS400 —cuenta e identificación incluidas— porque ese es el trabajo; y queda escrito que **ese
> archivo sale de la plataforma sin la protección de columna encima** (RNF-04, BP-PP-004).

> **PRECISIÓN (2026-09-26, la marcó el constructor y tenía razón).** El spike C-05 midió **LECTURA**
> en el sandbox real, no escritura. Que la librería esté no garantiza que **escribir** un `.xlsx` de
> cientos de filas entre en el presupuesto de 2 minutos (BP-PP-053). **Hay que MEDIRLO**, igual que se
> midió `rpa.lote.maximo`: la escritura se cronometra con un volumen real antes de confiar en ella.

### Decisión final del aprobador (2026-09-26): sin tope de filas, Action en vez de Function

La primera versión se registró como **Function** (invocación por GET, `filaids` en la URL) y se
sembró `exportacion.maximo = 500` como tope PROVISIONAL. La medición real con volumen (BP-PP-053)
encontró:

- **HTTP 414 a las ~700 filas**: como Function, TODA llamada es un GET con los `filaid` en la query
  string, y ese es un **techo de plataforma que ningún parámetro mueve** (ni `exportacion.maximo` ni
  ningún otro). El tope de 500 quedaba con menos de 200 filas de colchón frente a ese techo.
- El tope de 500 filas abrió un **agujero de integridad** que detectó el aprobador: si lo que se
  descarga no es exactamente lo que el ejecutivo seleccionó (por ejemplo, un recorte silencioso),
  el sistema puede terminar marcando como digitadas filas que el ejecutivo nunca tipeó en AS400.

**Decisión**: la Custom API pasa a **Action** (POST, los `filaid` viajan en el cuerpo) — `isfunction`
es **inmutable** una vez guardada la Custom API (Learn, "CustomAPI tables"), así que se borró la API
entera (parámetros de entrada/salida en cascada, verificado que no quedó nada huérfano) y se volvió a
crear. Se **elimina `exportacion.maximo`** por completo (parámetro, playbook, entorno y plugin, con
su excepción `LoteDeExportacionExcedidoException`). El único límite que queda es el **presupuesto
síncrono de 2 minutos** de la plataforma (BP-PP-053).

**El plugin se cuida solo.** Sin tope de filas, tarde o temprano alguien selecciona un lote enorme.
`ExportarParaDigitar.Ejecutar` lleva la cuenta del tiempo transcurrido mientras arma cada fila y, si
se acerca al presupuesto (umbral: 120 s menos 20 s de margen = 100 s), **corta él mismo** con
`PresupuestoDeExportacionAgotandoseException` en vez de dejar que la plataforma reviente con un error
genérico. El mensaje le dice a una persona **qué hacer** — cuántas filas alcanzó a preparar y que
vuelva a seleccionar menos —, nunca expone milisegundos, nombres de excepción ni códigos HTTP.

**Criterio del proyecto para estos mensajes** (aplica a toda la Custom API, no solo a esta): **se
genericiza lo que revela cómo funciona el sistema por dentro; se deja pasar lo que le dice a la
persona qué tiene que hacer.**

**Medición con volumen real, ya sin el techo de URL (2026-09-26, Dev, reciclando los 148 `Fila`
reales que hay en el entorno — el costo de armar y escribir el `.xlsx` se ejerce entero por cada
posición de `filaids`, aunque el id se repita; la LECTURA a Dataverse siempre es sobre ≤148 filas
distintas, porque `FilasDataverse.PorIds` deduplica antes de consultar — la lectura de un volumen
realmente grande de filas DISTINTAS no se pudo ejercitar en este entorno y queda como riesgo abierto):**

| Filas (`filaids`) | Resultado | Tiempo |
|---:|---|---:|
| 500 | OK | 1,8 s |
| 1.000 | OK | 0,7 s |
| 2.000 | OK | 0,7 s |
| 5.000 | OK | 1,1 s |
| 10.000 | OK | 1,6 s |
| 20.000 | OK | 2,5 s |
| 50.000 | OK | 5,8 s |
| 100.000 | OK | 10,0 s |
| 200.000 | OK | 21,4 s |
| 250.000 | OK | 40,6 s |
| 300.000 | **falla** | ~157–169 s — `SandboxFabricHostCommunicationException` ("no Sandbox Hosts are currently available"), reproducido dos veces |
| 500.000 | **falla** | 153,4 s — `System.OutOfMemoryException` |

**Conclusión honesta**: para cualquier volumen realista (el propio temor del aprobador era "alguien
selecciona 50.000 filas algún día") la curva es sana y rapidísima — 50.000 filas tardan 5,8 s, menos
del 5 % del presupuesto. El **primer límite real** aparece muy por encima de eso, entre 250.000 (sano)
y 300.000 (revienta): es un **agotamiento de memoria/sandbox de la plataforma**, no el presupuesto de
2 minutos — de hecho ambas fallas tardaron MÁS de 120 s en manifestarse, porque el crash del proceso
sandbox (y el reintento/recuperación de la plataforma) ocurre ANTES de que el chequeo de tiempo del
plugin tenga otra vuelta de loop para cortar con su propio mensaje. Por eso, en esos dos casos
extremos, lo que llega a quien llama SÍ es un error técnico crudo de la plataforma — exactamente lo
que `PresupuestoDeExportacionAgotandoseException` busca evitar, pero a un volumen (250.000+) muy por
encima de cualquier selección real de este negocio. Queda como riesgo abierto, no resuelto en esta
directriz: un chequeo de memoria (no solo de tiempo) si algún día se necesitara blindar también ese
extremo.

**Comando *Descargar para digitar*** sobre la grilla de Filas, masivo como todos los demás.

**Lo masivo NO se construye: ya existe.** Seleccionar todas y marcarlas funciona desde el principio
—el ribbon pasa la selección y `comandos.js` aplica una petición por fila en paralelo—, y las vistas
*por digitar* y *Por aprobar* también.

---

# DN-03 · La obligatoriedad se define por GESTIÓN

Recibida del aprobador el **2026-09-26**, con instrucción de resolverla **de 0 a 100 sin consultar**.
Las decisiones de abajo las toma el arquitecto; quedan escritas acá para poder discutirlas después,
no antes.

> **Corrección fechada 2026-09-28.** Lo de arriba ("quedan escritas acá para poder discutirlas
> después, no antes") ya se discutió: el aprobador **confirmó** el criterio sembrado de
> `porGestion.Exclusion` (que la gestión Exclusión afloje `numeroCuenta`, `banco` y `moneda`) el
> 2026-09-28. Dejó de ser una decisión unilateral del arquitecto a la espera de revisión y pasó a ser
> una decisión de negocio tomada (`playbooks/datos/parametros.md`, `pruebas/03-reglas-de-registro.md`
> R-18, `pruebas/12-obligatoriedad-gestion.md` O-02; commit `62be7af`). `Inclusion` y `Modificacion`
> siguen sin entrada en `porGestion` — eso no cambió, sigue abierto a que el negocio lo pida.

## Lo que hay hoy

`plantilla.obligatoriedad` es **global**: rige igual para toda fila, sin importar qué pide.

```json
{"porDefecto":"obligatorio","opcionales":[{"campo":"referencia"}]}
```

## Lo que pide el negocio

Que se pueda definir **por gestión**: `Inclusion`, `Exclusion`, `Modificacion`. Tiene todo el sentido
— una **exclusión** no necesita los mismos datos que una **inclusión**: para dar de baja a alguien
alcanza con identificarlo, no hace falta repetir su cuenta ni su banco.

## Diseño resuelto

**Forma nueva del parámetro. El bloque de hoy NO se toca y sigue rigiendo**: es el piso, y lo de
`porGestion` lo ajusta encima.

```json
{
  "porDefecto": "obligatorio",
  "opcionales": [{"campo": "referencia"}],
  "porGestion": {
    "Exclusion":    {"opcionales": [{"campo": "numeroCuenta"}, {"campo": "banco"}]},
    "Modificacion": {"obligatorios": [{"campo": "referencia"}]}
  }
}
```

| Decisión | Cuál y por qué |
|---|---|
| **Compatibilidad** | Un parámetro **sin** `porGestion` se comporta **exactamente como hoy**. Así el que ya está sembrado en tres entornos sigue siendo válido y nadie tiene que migrar nada |
| **Precedencia** | Lo de la gestión **gana** sobre el bloque global. `opcionales` afloja, `obligatorios` endurece: las dos direcciones, porque el negocio puede querer cualquiera de las dos |
| **Gestión vacía o inválida** | Rige **solo el bloque global**. No se falla cerrado acá: esa fila ya falló `LISTAS_VALIDAS` y va a ser rechazada por su propio motivo. Sumarle un segundo motivo derivado sería ruido, no información |
| **Nombres de gestión** | Los **valores canónicos** del dominio (`Inclusion`, `Exclusion`, `Modificacion`), comparados **sin distinguir caja ni tildes**, igual que ya hace la validación de listas |
| **Una gestión que no existe** en `porGestion` | **Error ruidoso de configuración**, como cualquier parámetro mal cargado. Un typo que se ignora en silencio es una regla que alguien cree que puso y no puso |
| **La regla `OBLIGATORIEDAD`** | **No cambia de código, de orden, de nivel ni de efecto.** Solo cambia qué considera obligatorio. Mismo principio que DN-01 |
| **El mensaje al cliente** | **No cambia.** Sigue nombrando el campo y **sin citar el valor** (D-41). Que la obligatoriedad ahora dependa de la gestión **no puede filtrarse al texto**: el cliente no tiene por qué deducir la configuración interna del banco |

## Lo que hay que construir

1. El dominio: leer la forma nueva y resolver la obligatoriedad **efectiva** de una fila según su gestión.
2. Sus pruebas, con TDD, incluida **la de compatibilidad**: un parámetro sin `porGestion` se comporta idéntico a hoy.
3. Sembrar el parámetro con la forma nueva, **conservando el comportamiento actual** hasta que el negocio diga qué aflojar en cada gestión.
4. Documentarlo en `02` §Parámetros.

---

# DN-04 · Carga masiva y activación masiva de catálogos por Excel, y Comentarios en la autorización por cliente

Recibida del aprobador el **2026-10-01**, **después** del release 1.1.0.0. Diseño discutido y cerrado
con el aprobador el mismo día, punto por punto; nada de lo de abajo es decisión unilateral del
arquitecto salvo donde se dice expresamente ("propuesta del arquitecto").

## Lo que pide el negocio

1. Una columna **Comentarios** (unos 50 caracteres) en la **autorización por cliente**.
2. Un botón para **cargar un Excel** con Clientes, Planes, Correos y Autorizaciones **desnormalizados**:
   la solución decide qué crear y qué ya existe.
3. Un botón para **activar o desactivar** en masa Clientes, Planes, Correos y Autorizaciones,
   también desde un Excel, **registro por registro** (incluida una sola autorización, un solo correo
   o un solo plan).

## Por qué no alcanza la importación nativa

`05` §6 punto 5 decía que las cargas grandes se hacían con la importación nativa desde Excel de
model-driven. No sirve para esto: carga **una tabla por archivo**, no entiende datos
desnormalizados, no busca un cliente por CIF BAC **o** CIF COM, no resuelve el ejecutivo dueño por
su correo y no valida todo el archivo antes de grabar. Confirmado por el aprobador: "le queda corto".

## Decisiones cerradas con el aprobador (2026-10-01)

| # | Decisión | Estado |
|---|---|---|
| 1 | **Solo autorizaciones por cliente** (`AutorizacionCliente`). Ningún Excel crea, activa ni desactiva `AutorizacionPlan`. "Por el momento" — si más adelante se pide la vía por plan, es una DN nueva | confirmado |
| 2 | Para **crear** un Plan hacen falta código, tipo de formato, **moneda** y cliente. El Plan **no tiene nombre que se cargue**: `sanic_nombre` es calculado (`<código> - <cliente>`, `NombreCalculadoStep`) | confirmado ("cambiá el nombre por la moneda") |
| 3 | Para **crear** un Cliente es obligatorio el **correo del ejecutivo encargado**. No es una columna nueva en Cliente: se resuelve a `ownerid`, que ya ES el ejecutivo asignado (RF-19, D-23) | confirmado |
| 4 | **Sin cascada.** Desactivar un Cliente o un Correo no desactiva sus planes ni sus autorizaciones: dejan de valer solas, porque la vigencia ya exige que todo esté activo (`02` §2.4) | confirmado |
| 5 | **Todo o nada.** Se valida el archivo entero sin grabar; con un solo error no se graba nada | confirmado |
| 6 | **Comentarios solo en `AutorizacionCliente`**, no en `AutorizacionPlan` | confirmado |
| 7 | **Dos Custom API** (carga y cambio de estado), **dos botones**, cada botón **en las cuatro grillas**: Clientes, Planes, Correos autorizados, Autorizaciones por cliente | confirmado |
| 8 | **Tope de 300 filas por archivo, en un parámetro**, a medir contra Dev y ajustar | confirmado; **medido y bajado a 100 el 2026-10-01** (D-75, §Parámetro nuevo) |
| 9 | Motivos de error **claros y con el número de fila** | confirmado |
| 10 | **Indicador de progreso nativo con un mensaje por etapa**, no barra de porcentaje. Un porcentaje real exige partir la carga en varias llamadas, y eso rompe el todo o nada (si falla la tercera tanda, las dos primeras ya quedaron grabadas) | confirmado |
| 11 | **Sin mockup HTML** (excepción a la regla del proyecto): no hay pantallas nuevas, solo botones sobre grillas existentes. Lo único de interfaz nuevo, el resumen en pantalla, queda especificado abajo para que nadie lo invente | confirmado |

## Columna nueva

`sanic_mppp_tbl_autorizacioncliente.sanic_comentarios` · `T(50)` · opcional · auditada (DD-18) ·
nombre visible "Comentarios" en 1033 (`01` §0). Va al formulario **Autorizacion por cliente**, en la
sección de la evidencia, después de la fecha del documento, y a las vistas "Autorizaciones por
cliente activas" y "Correos autorizados sobre este cliente". El largo se puede ampliar después; achicar, no.

## Mecanismo (común a los dos botones)

```
Botón (grilla) → JS: elegir .xlsx → base64 → Custom API (Action, plugin C#)
   → Pasada 1: leer y validar TODO, sin escribir
       → con errores: no graba nada; devuelve el Excel de resultado
       → sin errores: Pasada 2, graba todo en orden de dependencias
   → devuelve el Excel de resultado + contadores → JS lo descarga, muestra el resumen, refresca la grilla
```

| Decisión | Cuál y por qué |
|---|---|
| **Escribe con el usuario que llama, no con SYSTEM** (propuesta del arquitecto) | Al revés que la unidad histórica (`04` §1). Los catálogos están auditados (DD-18): tiene que quedar **quién** dio de alta o desactivó cada cosa. Además los privilegios del Administrador de planes los aplica la plataforma en cada `Create`/`Update`, no una copia nuestra de la matriz |
| **Lee usuarios y roles con SYSTEM** | Resolver el ejecutivo por correo y comprobar su rol no requiere privilegios de seguridad para el Administrador de planes, y no conviene dárselos. Es solo lectura, y los catálogos se siguen leyendo con el usuario |
| **Nunca modifica un registro que ya existe** | La carga crea y vincula, nada más. Si un dato del archivo no coincide con lo grabado, la fila da error (abajo); corregir se hace en el formulario. Una carga masiva que "de paso" pisa razones sociales o dueños es un cambio que nadie revisó |
| **Todo o nada en dos pasadas** | La pasada 1 no escribe. La pasada 2 corre dentro de la transacción de la Custom API: si una escritura falla (por ejemplo, alguien creó el mismo CIF entre las dos pasadas), la excepción deshace todo. **Verificado en Dev el 2026-10-01 (D-72)**: `IsInTransaction = true` en la etapa 30, y una excepción deja cero registros |
| **Orden de escritura** | Clientes → Planes → Correos → Autorizaciones, sin importar el orden de las filas del archivo. Cada entidad se crea **una sola vez** aunque aparezca en varias filas |
| **Normalizar antes de buscar** | El step `NormalizarYValidarStep` normaliza al grabar, pero la búsqueda ocurre antes. Si no se normaliza, `123` no encuentra al cliente `000000123`. **Se reutilizan las mismas funciones de normalización y validación de formato que usa el step** (`Steps/ReglasDeSteps.cs`): dos implementaciones de "qué es un CIF COM válido" terminan diciendo cosas distintas |
| **Búsquedas por lote, no fila por fila** | Una consulta por tabla con todas las claves del archivo (condición `In`), no una por fila. 300 filas × 4 búsquedas serían 1.200 viajes |
| **Presupuesto de escritura** | Se diseñó como `exportarparadigitar` (DN-02), con un umbral de 100 s. **Bajado a 30 s el 2026-10-01 (D-75)**: la medición mostró que en Dev la base corta una transacción a los ~40 s. Si se alcanza, se corta con excepción (que deshace lo grabado) y un mensaje que pide partir el archivo. El tope de filas se calibró midiendo, no estimando (§Parámetro nuevo) |
| **El archivo se lee con las reglas LP-01 a LP-07** (`03` §7) | Frontera de confianza única, tope de bytes comprimidos y descomprimidos (`lectura.limites`), orden exigido, nada se pierde en silencio. **Excepción a LP-08**: la hoja y los encabezados están **fijos en el código**, no en un parámetro. En la plantilla del cliente la estructura es configuración del negocio; acá cada columna tiene un significado que solo el código entiende, así que una columna nueva exige código nuevo igual. Ponerla en un parámetro sería configurable solo en apariencia |
| **Las filas de más se rechazan, no se cortan** | En la plantilla del cliente, una fila más allá de la ventana corta la lectura (D-12). **Acá no**: con más filas no vacías que `carga.filas.maximo`, se rechaza el archivo entero. El usuario creería que cargó todo |

### Lectura del archivo (los dos Excel)

- **Hoja**: tiene que existir una con el nombre exacto (sin distinguir mayúsculas). Las demás hojas se ignoran, así se puede agregar una de instrucciones.
- **Encabezados en la fila 1**, en el orden exacto de la tabla del Excel. Se comparan sin distinguir mayúsculas, tildes ni espacios a los lados (mismo criterio que `plantilla.listas`). Si falta, sobra (hay un encabezado no vacío después de la última columna) o está fuera de orden **cualquiera**, se rechaza el archivo y el mensaje nombra **cuáles**.
- **Datos desde la fila 2.** Una fila **totalmente vacía** se saltea: no tiene nada que perder. El número de fila de los mensajes es **el número de fila de Excel**, el que el usuario ve a la izquierda.
- **Ningún mensaje cita un valor entero**: una celda enorme se cita acortada (60 caracteres) o no se cita, para que la columna Resultado nunca se acerque al tope de 32.767 caracteres de una celda de Excel.
- **Celdas numéricas**: un CIF o un código de plan que Excel guardó como número (`123`, perdió los ceros) se lee como texto y se rellena. Un número en notación científica (`1.23E+08`) se rechaza en esa celda: no se puede reconstruir con certeza.
- **Fechas**: se acepta una celda de fecha de Excel o texto `dd/mm/aaaa`. Cualquier otra forma es error de la celda. Una fecha futura es error (un documento firmado no puede tener fecha de mañana). **Una anterior al 01/01/1900 también** (precisado el 2026-10-01 en la revisión del código): Dataverse no admite fechas anteriores a 1753, y sin este tope la fecha pasaría la pasada 1 y reventaría la escritura entera en la pasada 2 sin decir qué fila.

## Excel 1 — Carga masiva · hoja `CargaCatalogos` · `sanic_mppp_capi_cargarcatalogos`

| Col | Encabezado | Formato | Obligatoria cuando… |
|---|---|---|---|
| A | CIF BAC | dígitos, hasta 9; se rellena con ceros | siempre, A o B |
| B | CIF COM | `^[A-Z0-9 ]{9}\d{3}$`, en mayúscula | siempre, A o B; **las dos** si se crea el cliente |
| C | Razon social | texto, hasta 200 | se crea el cliente |
| D | Correo ejecutivo | correo, hasta 320 | se crea el cliente |
| E | Codigo plan | alfanumérico, hasta 4; se rellena con ceros | la fila trae un plan |
| F | Tipo formato | `06`, `10` u `11` (`6` se acepta como `06`) | se crea el plan |
| G | Moneda | `COR` o `USD` | se crea el plan |
| H | Correo | correo; se guarda en minúscula y sin espacios | la fila trae una autorización |
| I | Nombre contacto | texto, hasta 100 | se crea el correo |
| J | Fecha documento | fecha | opcional |
| K | Comentarios | texto, hasta 50 | opcional |

**Qué significa una fila.** Toda fila gira alrededor de **un cliente** (A y/o B). Opcionalmente trae
**un plan de ese cliente** (E, F, G), y opcionalmente **un correo que queda autorizado sobre ese
cliente** (H, I, J, K). La autorización es **implícita**: si hay correo, se autoriza sobre el cliente
de la fila. Por la desnormalización, el mismo cliente y el mismo correo se repiten en varias filas
(una por plan); eso es normal y se crea una sola vez.

### Reglas por fila

**Cliente**
1. Sin A ni B → error: toda fila necesita su cliente.
2. Se busca por A y por B, normalizados. Si vienen los dos y encuentran **clientes distintos**, o uno encuentra un cliente y el otro no existe → error: no coinciden con lo grabado.
3. **Existe**: si C o D vienen y **no coinciden** con lo grabado (razón social sin distinguir mayúsculas ni espacios a los lados; ejecutivo = el dueño actual) → error. La carga no modifica. Si existe **inactivo** → advertencia: lo que se le cargue no va a estar vigente hasta activarlo.
4. **No existe**: hacen falta A, B, C y D; si falta alguno → error, nombrando cuál. D tiene que ser **exactamente un** usuario **habilitado**, con ese correo y con el rol `SR - MPPP - Ejecutivo` (propuesta del arquitecto: un cliente a nombre de alguien que no es ejecutivo no aparece en "Mis clientes" de nadie, y nadie lo nota). Cero usuarios, varios usuarios, deshabilitado o sin el rol → error, nombrando cuál de esos casos es.

**Plan** (solo si viene E)
5. F o G sin E → error: son datos de un plan que no se nombró.
6. **Existe**: su cliente tiene que ser el de la fila → si no, error nombrando el cliente real (el código de plan es único en todo el banco). Si F o G vienen y no coinciden con lo grabado → error. Inactivo → advertencia.
7. **No existe**: hacen falta F y G; si falta alguno → error.

**Correo y autorización** (solo si viene H)
8. I, J o K sin H → error: son datos de una autorización sin correo.
9. H con formato de correo inválido → error.
10. **El correo existe**: si I viene y no coincide → error. Inactivo → advertencia.
11. **El correo no existe**: hace falta I.
12. **La autorización (correo, cliente) existe activa**: queda "ya existía". Si J o K vienen y no coinciden con lo grabado → error.
13. **Existe inactiva** → error: se reactiva con el botón de Activar/Desactivar, no desde la carga.
14. **No existe**: se crea con J y K. Nace **sin evidencia firmada**, como hoy (D-42): un archivo no puede viajar en una celda. Aparece en la vista "sin evidencia".

**Coherencia dentro del archivo**
15. **La misma clave tiene que tener los mismos datos en todas sus filas.** *Precisado el 2026-10-01 (revisión del código):* esto rige para lo que el archivo **crea**. Sobre lo que **ya existe**, cada fila se compara contra lo grabado (reglas 3, 6, 10 y 12), así que solo lleva error la fila que lo contradice; la que coincide está bien. Es más preciso que marcar todas: le dice al usuario cuál de las dos corregir. Para cliente, plan, correo y autorización, comparando entre las filas solo las celdas que vienen llenas (las vacías no contradicen). Ejemplo: `000123456` con dos razones sociales distintas → error **en todas las filas involucradas**, nombrándolas ("filas 3 y 9").
16. Un mismo CIF BAC con dos CIF COM distintos en el archivo (o al revés) → error en esas filas.

**Lo que NO es error.** Una fila donde todo ya existía queda "Sin cambios: todo ya existía o lo crea una fila anterior" (texto
del código desde la pieza 2: la segunda fila de un cliente que el mismo archivo crea tampoco crea nada por sí misma). El
archivo se puede volver a subir sin efectos (carga idempotente); si una carga se cortó, se repite
entera.

## Excel 2 — Activar / Desactivar · hoja `ActivarDesactivar` · `sanic_mppp_capi_cambiarestadocatalogos`

| Col | Encabezado | Valores |
|---|---|---|
| A | Tipo | `CLIENTE` · `PLAN` · `CORREO` · `AUTORIZACION_CLIENTE` |
| B | Accion | `ACTIVAR` · `DESACTIVAR` |
| C | CIF BAC | para `CLIENTE` y `AUTORIZACION_CLIENTE` |
| D | CIF COM | alternativo a C (o los dos, que tienen que apuntar al mismo cliente) |
| E | Codigo plan | para `PLAN` |
| F | Correo | para `CORREO` y `AUTORIZACION_CLIENTE` |

Ejemplo (el que pidió el aprobador):

| Tipo | Accion | CIF BAC | CIF COM | Codigo plan | Correo |
|---|---|---|---|---|---|
| CLIENTE | DESACTIVAR | 000123456 | | | |
| CLIENTE | ACTIVAR | | ABC123456001 | | |
| PLAN | DESACTIVAR | | | 0A12 | |
| CORREO | DESACTIVAR | | | | juan.perez@empresa.com |
| AUTORIZACION_CLIENTE | DESACTIVAR | 000123456 | | | maria.lopez@empresa.com |
| AUTORIZACION_CLIENTE | ACTIVAR | | XYZ987654002 | | juan.perez@empresa.com |

Qué hace cada fila: la 2 apaga el cliente (sus autorizaciones dejan de valer, aunque siguen activas).
La 4 apaga **solo** ese plan. La 5 apaga el correo en **todos** los clientes donde está autorizado.
La 6 apaga **solo** la autorización de María sobre ese cliente; María sigue autorizada sobre otros.

### Reglas por fila

1. Tipo o Accion fuera de su lista → error (se comparan sin distinguir mayúsculas ni espacios a los lados).
2. **Cada Tipo exige sus claves y ninguna otra.** Si falta una → error; si sobra una (por ejemplo, un código de plan en una fila `CORREO`) → error: una celda que no se usa es una intención que no se cumple.
3. El registro tiene que **existir** → si no, error. En `AUTORIZACION_CLIENTE`, la autorización (correo, cliente) tiene que existir; que el correo y el cliente existan por separado no alcanza.
4. Ya está en el estado pedido → "Sin cambios", no es error.
5. El mismo registro dos veces: con **acciones opuestas** → error en las dos filas; con la **misma acción** → se aplica una vez, y la segunda fila lleva advertencia de repetida.
6. **Advertencias de efecto** (calculadas sobre el estado final, después de aplicar el archivo entero):
   - Desactivar un `CLIENTE` o un `CORREO` → "deja sin efecto N autorizaciones por cliente activas".
   - Activar una `AUTORIZACION_CLIENTE` cuyo cliente o correo queda inactivo → "no va a estar vigente: el cliente / el correo está inactivo".
     *Precisado el 2026-10-01 (revisión del código):* advierte **aunque la autorización ya estuviera activa** (la fila no cambia nada, pero quien pidió ACTIVAR cree que opera). En cambio, "deja sin efecto N" solo sale si la fila realmente desactiva algo. Las filas opuestas o repetidas se detectan aunque alguna tenga otro error (reportar todo de una vez); el estado final se calcula solo con las filas sin error, así que en un archivo con errores una advertencia de efecto puede ser aproximada, sin consecuencia porque ese archivo no se graba.
7. El cambio de estado es el nativo (`statecode`/`statuscode`): Activo/Inactivo (`02`, encabezado).

## Contratos de las dos Custom API

Las dos: Action, unbound, `isprivate = false`, plugin en `Api/`, mismo patrón que
`exportarparadigitar` (`03` §1b).

| | `sanic_mppp_capi_cargarcatalogos` | `sanic_mppp_capi_cambiarestadocatalogos` |
|---|---|---|
| `ExecutePrivilegeName` | `prvCreatesanic_mppp_tbl_cliente` (solo lo tiene el Administrador de planes, `04` §2) | `prvWritesanic_mppp_tbl_cliente` (ídem) |
| Entrada `archivo` (String) | `.xlsx` en base64. Requerido salvo con `plantilla = true` | ídem |
| Entrada `plantilla` (Boolean, opcional) | `true` → no lee nada: devuelve en `resultado` la **plantilla vacía** (hoja y encabezados) | ídem |
| Salida `grabado` (Boolean) | `true` solo si la pasada 2 terminó | ídem |
| Salida `resultado` (String) | Excel de resultado en base64 | ídem |
| Salida `filas` · `errores` · `advertencias` (Integer) | filas no vacías leídas, filas con error, filas con advertencia | ídem |
| Salidas de conteo (Integer) | `clientescreados` · `planescreados` · `correoscreados` · `autorizacionescreadas` | `activados` · `desactivados` · `sincambios` |

**"Hoy" es la fecha de Nicaragua** (precisado el 2026-10-01 al diseñar la pieza 4c): el plugin corre en UTC y una
"Fecha documento" posterior a hoy es error. El envoltorio calcula hoy como la fecha UTC menos 6 horas (Nicaragua es UTC-6
todo el año, sin horario de verano; el proyecto ya lo da por sentado en `TransicionesDeFila.cs`). No hay parámetro: un
negocio con otra zona horaria es otro proyecto.

**Los parámetros (`carga.filas.maximo`, `lectura.limites`) se leen con SYSTEM**: el Administrador de planes no tiene
ningún privilegio sobre Parámetro (`04` §2). Los catálogos, en cambio, se leen y se escriben con el usuario que llama.

**El privilegio de entrada es solo la puerta.** Cada `Create` y cada `Update` de la pasada 2 vuelve a
pasar por la seguridad de la plataforma con el usuario que llama.

**Errores de ARCHIVO** (no de fila): la llamada falla con un código y un texto propio. **Nunca llega
al usuario el mensaje de la librería** (LP-01); ese va a la traza. No hay Excel de resultado, porque
no hay filas confiables a las que asignarle un motivo.

| Código | Cuándo |
|---|---|
| `MPPP-CAR-001` | `archivo` vacío o no es base64 válido |
| `MPPP-CAR-002` | No es un `.xlsx` legible |
| `MPPP-CAR-003` | Supera el peso de `lectura.limites` (comprimido o descomprimido) |
| `MPPP-CAR-004` | No existe la hoja esperada (nombra cuál se esperaba) |
| `MPPP-CAR-005` | Encabezados que faltan, sobran o están fuera de orden (los nombra) |
| `MPPP-CAR-006` | Más filas no vacías que `carga.filas.maximo` (dice cuál es el máximo; *precisado el 2026-10-01*: no dice cuántas trae, porque el lector corta en la primera de más para no recorrer un archivo hostil entero) |
| `MPPP-CAR-007` | Ninguna fila de datos |
| `MPPP-CAR-008` | Se agotó el presupuesto de tiempo; **no se grabó nada** (pide partir el archivo) |
| `MPPP-CAR-009` | Falló una escritura en la pasada 2; **no se grabó nada** (nombra la fila y el motivo de Dataverse) |
| `MPPP-CAR-010` | `plantilla = true` y además vino `archivo`: no se adivina cuál de las dos cosas se quiso |
| `MPPP-CAR-011` | Falló una lectura o una escritura por **sobrecarga de la plataforma**, no por un dato; **no se grabó nada** (pide reintentar en unos minutos o partir el archivo, y no nombra ninguna fila: la fila no tiene nada malo) |

**Falla de plataforma (MPPP-CAR-011) contra error de dato (MPPP-CAR-009)** (decisión del aprobador, 2026-10-01, D-75). En la
medición, una carga de 300 filas falló con el error 10928 de Azure SQL y el 009 le decía al usuario "Corregí esa fila", cuando
la fila no tenía nada malo. Se clasifica por el `ErrorCode` del `OrganizationServiceFault`, **nunca por el texto**. Cuentan como
falla de plataforma estos códigos de Learn ("Web service error codes"; el de timeout, de "Troubleshoot Dataverse plug-ins"):
`0x80072324` AzureSqlMaxConcurrentWorkersLimitExceededError · `0x80072323` AzureSqlDatabaseResourceLimitExceededError ·
`0x80072326` ThrottlingConcurrencyLimitExceededError · `0x80072321` ThrottlingTimeExceededError ·
`0x80072322` ThrottlingBurstRequestLimitExceededError · `0x8005F103` Throttling · `0x80072330` CacheLoadConcurrencyError ·
`-2147204783` "Sql error: Execution Timeout Expired". Cualquier otro código, o un fault sin detalle, sigue siendo 009 (en una escritura) o el mensaje genérico (en una lectura).
*Ampliado el 2026-10-01 tras la revisión del código*: también una LECTURA con código de plataforma (pasada 1, parámetros,
usuarios, roles) da 011, porque el 10928 puede caer en cualquier llamada. El código de Dataverse (en hexadecimal) va SIEMPRE a
la traza, sea 009, 011 o genérico: el 10928 de la medición no dejó su código en la traza, y
sin él no se puede confirmar que la lista lo cubre. **Verificado en Dev el 2026-10-01** (ensamblado 1.0.21.0, presupuesto todavía
en 100 s): una carga de 200 filas falló a los 42 s, el usuario recibió el 011 y la traza dijo `Fila 66 (código 0x80072324)`. El
10928 llega como `AzureSqlMaxConcurrentWorkersLimitExceededError`, en el `ErrorCode` de arriba del fault (no en el `InnerFault`).

**Excel de resultado.** Las mismas columnas, con los valores tal como se leyeron, más una columna al
final, **Resultado**, y la fila 1 de encabezados igual.

> **Precisado el 2026-10-01 (arquitecto, al diseñar el armador): la columna Resultado es parte de la PLANTILLA.**
> El diálogo le dice al usuario "corregí y volvé a subirlo". Si la columna Resultado solo apareciera en el archivo
> de resultado, el modo estricto rechazaría el archivo corregido por traer una columna de más ("la celda L1 trae
> un valor…"): una trampa. Por eso la plantilla vacía ya nace con **Resultado** como última columna (**L** en
> `CargaCatalogos`, **G** en `ActivarDesactivar`), con su encabezado exigido como los demás y su contenido
> **nunca leído** (es solo de salida). Plantilla, archivo de resultado y archivo corregido tienen la misma forma:
> se baja, se corrige y se vuelve a subir tal cual. Cualquier cosa después de esa columna sigue siendo rechazo.
> Las filas del archivo de resultado van en el **mismo número de fila** que en el archivo subido, para que "Fila 7"
> en un mensaje sea la fila 7 que el usuario ve. Las celdas se escriben siempre como **texto** (nunca fórmula: un
> valor que empieza con `=` queda como texto). Valores posibles de Resultado:

- `Error: <motivo>` (puede haber varios en una fila, separados por ` | `; **se reportan todos los errores de la fila**, no solo el primero, para que se corrija de una vez).
- `Advertencia: <motivo>`.
- `Creado: cliente, plan, correo, autorización` (lo que corresponda) · `Activado` · `Desactivado` · `Sin cambios: <por qué>`.
- Si el archivo tuvo errores, las filas sin error dicen `Sin errores — no se grabó: el archivo tiene errores en otras filas`.

**Formato de los motivos**: `<Columna>: <qué pasa>. <Qué hacer>.`, por ejemplo:
- `CIF COM: "ABC12" no tiene el formato de CIF COM (9 letras o números y 3 dígitos). Corregilo.`
- `CIF BAC / CIF COM: apuntan a dos clientes distintos (Ferretería Sol y Distribuidora Luna). Dejá uno solo o corregí el que está mal.`
- `Correo ejecutivo: ana@banco.com no tiene el rol Ejecutivo. Pedí que se lo asignen o usá otro correo.`
- `Razon social: el cliente 000123456 aparece con otra razón social en la fila 9. Usá la misma en las dos.`
- `Codigo plan: el plan 0A12 ya existe y es de Distribuidora Luna, no de este cliente.`

## Interfaz

**Botones** (clásicos, `RibbonDiffXml` + `recursos/js/comandos.js`, como los demás, `06` §12.5):
"Carga masiva" y "Activar/Desactivar masivo", **cada uno es un menú desplegable** (`FlyoutAnchor`, el primero
del proyecto) con dos opciones: *Cargar archivo* (el flujo de abajo) y *Descargar plantilla* (llama a la
Custom API con `plantilla = true` y descarga `CargaCatalogos.xlsx` o `ActivarDesactivar.xlsx`, con el
indicador "Generando la plantilla…"). El playbook guarda los textos en 1033 y 3082, pero el menú muestra el texto fijo en
español (ver el párrafo de abajo, D-74), y lleva ícono
propio (`recursos/svg/`). Van en la **grilla principal** de las cuatro tablas. Los ve **solo el
Administrador de planes**: Ejecutivo y Supervisor solo leen catálogos. La regla real igual es el
privilegio de la Custom API.

**Flujo del JS** (mismo para los dos):
1. Abre el selector de archivo (solo `.xlsx`). Si el usuario cancela, no pasa nada.
2. `Xrm.Utility.showProgressIndicator("Leyendo el archivo…")` → lo pasa a base64.
3. `"Validando y cargando el archivo… puede tardar hasta un minuto"` → llama a la Custom API. *(Era "hasta 2 minutos"; corregido el
   2026-10-01 por D-75: la escritura se corta a los 30 s, y el minuto cubre la lectura y la descarga con margen. Decisión del aprobador.)*
4. `"Preparando el resultado…"` → si vino `resultado`, lo descarga como `<nombre original>-resultado.xlsx`.
5. Cierra el indicador y muestra un diálogo de resumen:
   - **Grabado**: `"Carga terminada: 12 clientes, 30 planes, 8 correos y 45 autorizaciones creados. 3 filas con advertencias: revisá el archivo de resultado."`
   - **Con errores**: `"No se grabó nada: 4 filas con errores. Revisá la columna Resultado del archivo que se descargó, corregí y volvé a subirlo."`
   - **Error de archivo**: el texto del código `MPPP-CAR-00x`.
6. Refresca la grilla.

El indicador es modal: mientras está abierto no se puede volver a hacer clic.

**Los menús llevan texto fijo en español, no etiquetas localizadas** (decisión del aprobador, 2026-10-01; excepción
declarada a la regla de multiidioma, `01` §0, **solo para menús desplegables**: los botones simples siguen con
`$LocLabels`). Observado en Dev: Unified Interface **no mostró las `$LocLabels` dentro de un `FlyoutAnchor`**; no está
demostrado que sea imposible y el experimento controlado es D-74 (`PENDIENTES.md`). Las etiquetas quedaban guardadas (`ribbondiff`, tipo 3) pero `GetClientMetadata` no las mandaba, y
el menú y sus opciones decían "LabelText", aun después de "Regenerate ribbon metadata". El texto directo en `LabelText`
lo admite el ribbon (Learn, "Use localized labels with ribbons") y **no depende de los idiomas instalados**: se ve igual
en cualquier entorno, también para un usuario con la interfaz en inglés. El playbook conserva los textos en 1033 y 3082
y declara `idioma_del_texto: 3082`. Si se necesitara multiidioma acá, la alternativa probada son botones simples
(LocLabels sí funciona en un `Button`, como en Fila).

### Contrato del JS (`recursos/js/comandos.js`, fijado el 2026-10-01 para la pieza 5)

Funciones públicas nuevas de `Sanic.Mppp.Comandos`, todas reciben el control de la grilla (`SelectedControl`):

| Función | Qué hace |
|---|---|
| `esAdministradorDePlanes(control)` | Regla de visibilidad del ancla y de las opciones. `true` solo si `Xrm.Utility.getGlobalContext().userSettings.roles` trae un rol llamado exactamente `SR - MPPP - Administrador de planes`. **Ante la duda (error al leer los roles), `false`**: a diferencia de las reglas de estado de Fila (que muestran ante la duda porque el plugin igual valida), acá ocultar no le quita nada a nadie que deba verlo — el privilegio de la Custom API es la puerta real |
| `cargarCatalogos(control)` / `cambiarEstadoCatalogos(control)` | Abren el selector de archivo **sincrónicamente dentro del clic** (un `input type="file"` con `accept=".xlsx"`; fuera del gesto del usuario el navegador lo bloquea). Cancelar no hace nada. Un archivo que no termina en `.xlsx` → aviso, sin llamar a la API. Después: indicador "Leyendo el archivo…" (FileReader → base64 sin el prefijo `data:`), "Validando y cargando el archivo… puede tardar hasta un minuto" (la Custom API como Action, `archivo`), "Preparando el resultado…" (descarga de `resultado` como `<nombre original sin extensión>-resultado.xlsx` con `Xrm.Navigation.openFile`, `openMode: 2`). Cierra el indicador, muestra el resumen y refresca la grilla. Un error de la API (los MPPP-CAR) se muestra con `error.message` **tal cual** |
| `plantillaCarga(control)` / `plantillaCambioEstado(control)` | Indicador "Generando la plantilla…", la Custom API con `plantilla: true`, descarga `CargaCatalogos.xlsx` / `ActivarDesactivar.xlsx` |

Textos del resumen (en `TEXTOS`, en 3082 y 1033 como los demás):
- Carga grabada: "Carga terminada: {clientes} clientes, {planes} planes, {correos} correos y {autorizaciones} autorizaciones creados." / "Load finished: {clientes} clients, {planes} plans, {correos} emails and {autorizaciones} authorizations created."
- Cambio grabado: "Cambio de estado terminado: {activados} activados, {desactivados} desactivados y {sincambios} sin cambios." / "Status change finished: {activados} activated, {desactivados} deactivated and {sincambios} unchanged."
- Si `advertencias` > 0, se agrega: " {advertencias} filas con advertencias: revisá el archivo de resultado." / " {advertencias} rows with warnings: check the result file."
- No grabado: "No se grabó nada: {errores} filas con errores. Revisá la columna Resultado del archivo que se descargó, corregí y volvé a subirlo." / "Nothing was saved: {errores} rows with errors. Check the Result column of the downloaded file, fix it and upload it again."


## Parámetro nuevo

`carga.filas.maximo` · Número · **100** (era 300; medido en Dev el 2026-10-01, D-75). Lo usan las dos Custom API. Si
falta, se usa un valor quemado de **100** (mismo criterio que `rpa.horas.tolerancia`). El peso del archivo reutiliza
`lectura.limites`.

**Medición (D-75, peor caso: cada fila crea cliente, plan, correo y autorización, con la sesión real del Administrador de
planes):** 50 filas en 14,0 s, 100 en 25,5 s y 150 en 39,0 s, todas grabadas. 200 y 300 fallaron siempre a los ~40 s con el
error 10928 de Azure SQL (tope de workers de la base). En Dev el techo es de **tiempo de transacción abierta (~40 s)**, no de
filas. **Decisión del aprobador (2026-10-01):** tope de 100 filas: el peor caso tardó 25,5 s, con 36% de margen frente al corte de la base (~40 s) y unos 15% frente al
presupuesto de 30 s. Si la base está más lenta, puede cortar nuestro MPPP-CAR-008, con su mensaje claro, y ese es el
propósito del presupuesto. Y el presupuesto de
escritura baja de 100 s a **30 s**, para que corte nuestro MPPP-CAR-008, con su mensaje claro, antes que la base. El tope de
la base de Producción no está medido.

## Abierto

| # | Punto | Estado |
|---|---|---|
| A | **D-72**: spike de transacción de la Custom API (arriba) | **cerrado 2026-10-01: hay transacción**, y la excepción deshace todo lo escrito, con el servicio del usuario y con SYSTEM (`spikes/d72-transaccion-capi/CONCLUSIONES.md`). El todo o nada queda como está diseñado. **Regla que sale del spike para la pasada 2**: si un `Create` o un `Update` falla, se atrapa SOLO para envolverlo en `InvalidPluginExecutionException` con el código `MPPP-CAR-009` y la fila, y se relanza **sin ninguna otra llamada al servicio** (Learn: un plugin que se traga el error y sigue rompe la transacción; ese camino no se midió y no se usa) |
| B | **Plantillas vacías**: el usuario necesita de dónde bajar los dos Excel con los encabezados correctos. Un web resource no admite `.xlsx` | **cerrado 2026-10-01, aprobador**: la genera **la misma Custom API** (`plantilla = true`) a partir de **la misma definición de columnas que valida**, así el encabezado que se exige y el de la plantilla no pueden separarse. Una prueba lo fija: la plantilla generada, subida vacía, falla solo por `MPPP-CAR-007` (sin filas), nunca por encabezados |
| C | **Versión de la solución**: esto sale después del release 1.1.0.0 | **cerrado 2026-10-01, aprobador: se publica como 1.2.0.0** |
| D | **Cómo se pide la plantilla desde la app** | **cerrado 2026-10-01, aprobador: opción A, menú desplegable** (ver §Interfaz). Descartadas: dos botones más (16 en total, se llena la barra) y ofrecerla solo cuando falla (el usuario nuevo no tiene de dónde sacarla). `openConfirmDialog` **no sirve** para elegir entre cargar y bajar la plantilla: devuelve `confirmed = false` tanto con el segundo botón como con la X, así que cerrar el diálogo descargaría la plantilla |

## Lo que hay que construir (con orden del aprobador, después de A)

1. `sanic_comentarios` en `AutorizacionCliente`, más el formulario y las dos vistas.
2. Dominio de la carga (lectura y validación por fila y entre filas), puro y con TDD estricto. Las pruebas de aceptación y las firmas las escribe el coordinador (ciclo de plugins).
3. Dominio del cambio de estado, ídem.
4. Las dos Custom API y su registro.
5. Parámetro `carga.filas.maximo`.
6. Los dos comandos en las cuatro grillas, el JS y los íconos.
7. Medición de volumen contra Dev, y ajuste del tope.
8. Casos de prueba nuevos en `pruebas/` (set de catálogos).
