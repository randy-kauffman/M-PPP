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
| 6 | La app necesita poder cargar y ver las autorizaciones por cliente, y el Administrador de planes es quien las administra (RF-11) | **confirmado por el aprobador, 2026-09-26** |

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

**Comando *Descargar para digitar*** sobre la grilla de Filas, masivo como todos los demás.

**Lo masivo NO se construye: ya existe.** Seleccionar todas y marcarlas funciona desde el principio
—el ribbon pasa la selección y `comandos.js` aplica una petición por fila en paralelo—, y las vistas
*por digitar* y *Por aprobar* también.
