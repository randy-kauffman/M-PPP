# Convenciones de pruebas

Proyecto: `2026-001-referencias-planes-pago` · Escrito el 2026-09-27

Este documento fija **cómo se escribe y cómo se corre una prueba en este proyecto**. Hasta hoy
esa regla existía solamente en la cabeza de quien pedía el trabajo: los 57 casos nuevos de fase 2
se escribieron siguiendo un formato que no estaba en ningún archivo. Eso se termina acá.

`README.md` dice cómo está **organizado** el plan. Este documento dice cómo se **escribe**.

---

## 1. El formato de un caso

Todo caso lleva estos siete campos, en este orden, sin excepción:

```markdown
## <ID> — <título corto>

**Qué prueba.** Una o dos frases: qué comportamiento del diseño se verifica.

**Necesita.** Usuario y rol · datos previos · permisos · estado del entorno.

**Cómo.** Los pasos, numerados.

**Qué tiene que pasar.** El resultado esperado, concreto y observable.

**Cómo se verifica.** El comando o la consulta EXACTA.

**Cómo se sabe que falló.** El síntoma, y cómo distinguirlo de otras causas posibles.

**Automatizable.** `API` | `GUI` | `manual` — y por qué.
```

### Por qué cada campo

**`Necesita`** declara las precondiciones del caso. **No es una instrucción para una persona: es la
especificación de lo que el set tiene que dejar listo por su cuenta** (§8). Si dice "los datos de
siempre", el campo está mal escrito. De la suma de todos los `Necesita` de un archivo sale
exactamente lo que ese set debe preparar antes de su primer caso.

**`Cómo se verifica`** lleva el comando textual, no una descripción. `ver_solicitud.py MPPP-00001234`
es verificable; "revisar que la solicitud esté bien" no lo es.

**`Cómo se sabe que falló`** no es relleno, y es el campo que más se subestima. El 2026-09-27 se
perdieron dos días diagnosticando "el botón no se ve", que tenía **dos causas opuestas con el mismo
síntoma**: un botón oculto por una regla (defecto de definición) y un botón ausente de la metadata
compilada (caché del servidor). Un caso que solo dice "falla si no aparece el botón" manda al que
prueba a adivinar. Un caso que dice "si el Command Checker lo lista como `(hidden)` es una regla; si
no lo lista, es la metadata" lo manda directo a la causa.

**`Automatizable`** dice si lo corre una herramienta o una persona. Si es `GUI`, nombra la sesión
exacta (§6). Sin ese campo, alguien va a repetir a mano algo que ya corre solo.

---

## 2. Qué NO es un caso

- **Lo que no se puede verificar objetivamente.** Si el resultado depende del criterio de quien
  mira, no es un caso. O se reescribe con un criterio observable, o se escribe **por qué** no se
  puede verificar y queda documentado como límite.
- **Lo que cuenta en vez de nombrar.** Ver §7.
- **Lo que prueba la herramienta en vez del sistema.** Que `tabla.py` diga `ya_existia` no prueba
  que la tabla esté bien: prueba que la herramienta está conforme. La verificación va contra la
  **plataforma**, no contra la salida del constructor.

---

## 3. Los tres resultados

| Resultado | Cuándo |
|---|---|
| **OK** | Pasó exactamente lo esperado |
| **FALLA** | Pasó otra cosa. Se anota qué pasó, con el número de Solicitud |
| **N-A** | No se pudo correr, y por qué |

**Un caso que "casi" pasa es FALLA.** Si el estado quedó correcto pero el mensaje al cliente dice
otra cosa, es FALLA: ese mensaje lo lee un cliente del banco.

**Un caso que no corrió y no se reportó es peor que un caso que no existe**, porque el que lee el
resumen cree que está cubierto. Todo `N-A` se reporta explícitamente, con su motivo.

### Un defecto conocido no es N-A

`N-A` es para **límites del entorno**: un permiso que no tenemos, un buzón fuera de la Application
Access Policy, un flujo que depende del schedule real de Power Automate. Cosas que nos impiden
correr el caso y que **no son el sistema fallando**.

**Un caso bloqueado por un defecto registrado es FALLA**, aunque el defecto no sea el tema del caso
y aunque ya tenga su ID en `diseno/PENDIENTES.md`. Se reporta con el ID en el motivo
(`FALLA — bloqueado por D-59`).

Por qué: si un defecto del sistema produce `N-A`, el resumen dice **"0 FALLA"** y el suite se ve
sano mientras producción está rota. Un defecto conocido no se arregla apagando la prueba que lo
detecta, y tampoco pintándola de gris.

El rojo incómodo es el punto: **mide el costo real de posponer.** El 2026-09-27, D-59 —una línea de
código— dejaba cinco casos en rojo repartidos en dos sets. Ese número es la razón para arreglarlo, y
solo se ve si los casos dicen FALLA.

---

## 4. Convención de IDs

Un prefijo por archivo, una letra, sin colisiones. Estado al 2026-09-27:

| Prefijo | Archivo |
|---|---|
| `P` | `00-preparacion.md` |
| `C` | `01-clasificacion-del-correo.md` |
| `S` | `02-reglas-de-solicitud.md` |
| `R` | `03-reglas-de-registro.md` |
| `M` | `04-comunicaciones.md` |
| `A` | `05-app-y-comandos.md` |
| `G` | `06-seguridad-vistas-formularios.md` |
| `V` | `07-vigilancia.md` |
| `I` | `08-integridad-y-plugins.md` |
| `B` | `09-rpa-fase2.md` (Bot) |
| `U` | `10-autorizacion-cliente.md` (aUtorización) |
| `E` | `11-exportacion-excel.md` |
| `O` | `12-obligatoriedad-gestion.md` |
| `H` | `13-construido-hoy.md` |

Un archivo nuevo elige una letra libre y **la agrega a esta tabla en el mismo commit**. Un ID
asignado no se reusa ni se renumera: se referencia desde otros documentos y desde los módulos de `herramientas/pruebas/sets/`.

---

## 5. Aislamiento de los datos

El 2026-09-27 un `preparar_datos.py --limpiar` borró el fixture compartido completo, incluidas
autorizaciones reales del aprobador que hubo que restaurar a mano. De ahí salen estas reglas.

- **Marca de agua.** Todo lo que crea el arnés nace con el prefijo reservado `ZZP-`.
  `herramientas/pruebas/marca_agua.py` es la única fuente de verdad de ese prefijo.
- **Nadie borra lo que no creó.** `borrar_marcado()` relee el campo **justo antes de cada DELETE** y
  aborta si no lleva la marca. **No tiene bandera de forzado, a propósito**: un borrado seguro que
  se puede forzar no es un borrado seguro, es un borrado peligroso con un paso extra.
- **No se confía en una lista armada de antes.** Así fue como se borraron los datos del aprobador:
  la lista se armó por coincidencia de nombre y para cuando se ejecutó el DELETE ya incluía cosas
  ajenas.
- **Semáforo.** `herramientas/pruebas/semaforo.py` bloquea el entorno mientras el arnés corre. Si lo
  toma el arnés, guarda su PID y se libera solo cuando el proceso muere. Si lo pone una **persona**,
  el PID queda vacío y **no se autolibera nunca**: un arnés que murió no tiene por qué bloquear a
  nadie, pero si una persona dijo "estoy probando", ningún proceso decide que ya terminó.
- **Todo caso limpia lo que creó**, y solo eso.

---

## 6. Sesiones de navegador

Cuatro sesiones, una por rol. Las genera el aprobador con su MFA y viven en `local/`, **fuera del
repositorio**. Son credenciales: permisos `600`, nunca se imprime su contenido, nunca se commitean.

| Archivo | Usuario | Rol |
|---|---|---|
| `local/sesion-ejecutivo.json` | `# User 1` | `SR - MPPP - Ejecutivo` |
| `local/sesion-supervisor.json` | `# User 2` | `SR - MPPP - Supervisor` |
| `local/sesion-admin_planes.json` | `# User 3` | `SR - MPPP - Administrador de planes` |
| `local/sesion-admin_tecnico.json` | `# User 4` | `SR - MPPP - Administrador tecnico` |

**Hay DOS administradores y hacen cosas distintas.** `admin_planes` toca Clientes, Planes,
Autorizados y Autorizaciones; `admin_tecnico` toca Parámetros y Reglas. Un caso nunca dice
"administrador" a secas.

**Ni uno ni el otro alcanza para tareas de plataforma.** Entrar al maker portal, tocar roles de
seguridad o regenerar la metadata de un ribbon exige System Administrator o Customizer de Dataverse,
que es otra cosa y se llama parecido. Un caso que necesita eso lo dice explícitamente.

### La guarda previa es obligatoria

Antes de correr un solo caso GUI:

```bash
python3 herramientas/pruebas/estado_sesiones.py
```

Si una sesión está vencida o su usuario no tiene el rol esperado, **no se corre nada**.

El motivo está escrito en `herramientas/pruebas/comportamiento_roles.py`: el 2026-09-24 le cambiaron
el rol a `User1` y dos casos dieron rojo con un `403` que parecía un defecto del producto. En
seguridad el riesgo es peor todavía, y al revés: **una prueba que pasa porque el usuario no ve algo
es indistinguible de una que pasa porque la sesión está muerta. Las dos dan verde, y una está
mintiendo.**

---

## 7. Nombrar, no contar

**Una prueba que cuenta cosas envejece sola; una que nombra cosas, no.**

`00-preparacion.md` decía "12 parámetros activos". Hoy hay 19, y el caso daba FALLA sin que nada
estuviera roto. Peor: al revisarlo se descubrió que el commit que escribió ese caso **ya sembraba
13**. Nació mal el día uno y nadie lo notó durante cinco días, porque un número no se discute.

La forma correcta es **nombrar los 13 parámetros de fase 1, uno por uno**, y verificar que estén.
Que existan otros no rompe nada; que falte uno de esos trece, sí.

Lo mismo con "las 800 pruebas de C#", "18 vistas", "13 entradas de sitemap" y "8 steps registrados"
— los cuatro estaban mal al 2026-09-27, y uno de ellos listaba un step
(`IntegridadAutorizacionPlanStep`) **que nunca existió en el código**.

Si un caso necesita un número, ese número va con su fuente y con la fecha en que se verificó.

---

## 8. Cada set es autocontenido

Un **set** es un archivo de casos: una letra, un archivo, un set. `09-rpa-fase2.md` es el set `B`.

**Un set no depende de otro set. Un set no supone nada.**

Esto reemplaza lo que el plan decía hasta el 2026-09-27 (`README.md`: *"el orden importa dentro de
cada archivo; varios casos usan la Solicitud que dejó el anterior"*). Esa regla se da de baja.

### Qué significa en concreto

Todo set, antes de su primer caso, **verifica y deja listo TODO lo que necesita**:

- Los **datos**: clientes, planes, autorizados, autorizaciones, solicitudes y filas, con la marca
  `ZZP-` y con el dueño correcto (las vistas "Mis clientes" filtran por `ownerid` del Cliente: un
  fixture a nombre de otro usuario deja la grilla vacía y el caso falla sin que nada esté roto).
- Los **roles de los usuarios** de prueba, verificados uno por uno contra
  `systemuserroles_association`, no supuestos.
- La **configuración global** (§8.2), llevada al canon antes de empezar.
- Las **sesiones** de navegador que va a usar (§6), con la guarda previa.

Si algo no está, el set **lo prepara**. No lo asume, no lo pide por documentación, no falla pidiendo
que alguien lo haga antes. El service principal `# PowerPlatform-Dev` tiene **System Administrator**
en el entorno: puede asignar roles, sembrar parámetros y tocar configuración de solución. No hay
nada del entorno que un set no pueda dejar listo por su cuenta.

**Dentro del mismo set**, un caso SÍ puede apoyarse en lo que dejó el anterior, y puede cambiar
datos, roles y configuración para llevar a cabo la prueba. La frontera es el set, no el caso.

### Por qué

Un set que depende de otro tiene tres problemas, y el tercero es el que importa:

1. **No se puede correr solo.** Para probar comunicaciones habría que correr antes media hora de
   clasificación.
2. **Un fallo temprano tira todo lo de atrás.** Si el caso 2 falla, del 3 al 7 salen en rojo sin
   haber probado nada: siete rojos, un defecto, y una hora buscando siete causas.
3. **Un set que supone no prueba lo que dice probar.** Si supone una autorización que dejó otro set,
   y mientras tanto alguien le cambió el rol a un usuario — como pasó el 2026-09-24 —, el set corre
   igual y da verde o rojo **por un motivo que no es el que está probando**. El resultado existe y
   no significa nada.

**Un set autocontenido no es más prolijo: es el único que dice la verdad.**

### 8.1 Un set a la vez

Los sets **no corren en paralelo**. El semáforo (§5) los serializa.

Mientras la configuración global sea compartida, dos sets que la tocan al mismo tiempo se pisan y
**los dos mienten**. La solución de fondo es un entorno por set; hasta que exista, se serializa.

### 8.2 Configuración global: canon, no restauración

Estos parámetros **cambian el comportamiento de todo el entorno**:

| Parámetro | Qué cambia | **Canon** |
|---|---|---|
| `autorizacion.modo` | Autorización por cliente o por plan (DN-01) | `plan` |
| `plantilla.obligatoriedad` | Qué columnas son obligatorias por gestión (DN-03) | la versión activa más alta |
| `rpa.puedeaprobar` | Si el bot puede aprobar filas digitadas | `si` |
| `rpa.lote.maximo` | Tope de filas por lote | `100` |
| `rpa.horas.tolerancia` | Ventana de fecha aceptable | `24` |
| `rpa.minutos.claimmaximo` | Duración de un claim | `60` |
| `exportacion.columnas` | Orden de columnas del Excel (DN-02) | el sembrado por `parametros.md` |

Valores verificados contra Dev el 2026-09-27.

La regla es: **al terminar, el set deja cada parámetro global en su valor canónico.** No restaura el
valor que encontró: lo lleva al canon.

**Por qué el canon y no la restauración.** Un set que restaura deja el entorno bien solo si terminó
bien. Y los sets se van a morir a la mitad — por un timeout, por una sesión vencida, por un `Ctrl-C`.
Un set que restaura y murió deja el entorno en cualquier lado, y el siguiente hereda esa basura sin
saberlo. Un set que setea al canon lo deja **siempre igual, muera como muera**: es idempotente por
diseño, no por disciplina.

Y por eso mismo el canon se aplica **dos veces**: al arrancar (para no heredar lo que dejó un set
que murió) y al terminar.

Una prueba de obligatoriedad que deja el sistema en "Exclusión afloja tres campos" y nadie lo nota
**se descubre cuando un cliente del banco manda una exclusión**. Ese es el costo real de no tener
canon.

**Versionar un parámetro es agregar una fila, no editar la anterior**: gana la versión más alta
activa. Un set **nunca** edita ni borra una versión que no sembró él.

Y una versión de prueba, la que sembró el propio set, se limpia en **dos pasos**, en este orden:

1. **Desactivarla** — devuelve la precedencia al canon de inmediato.
2. **Borrarla** en `cerrar()` — el set limpia lo que creó (§5).

Desactivar no es limpiar. Si el set solo desactiva, cada corrida deja una fila más: el 2026-09-27,
tres corridas del set E dejaron tres versiones de `exportacion.columnas` colgando, y esa
acumulación **produjo un defecto real** — la cuarta corrida chocaba con la clave alternativa
(`HTTP 412`) porque calculaba la versión nueva contra la activa en vez de contra el máximo
sembrado. Basura que se acumula no es inofensiva: tarde o temprano alguien tropieza con ella.

Calcular la versión nueva como **máximo sembrado + 1** (no "activa + 1") sigue siendo lo correcto,
aunque se borre: protege contra restos de una corrida que murió antes de limpiar.

---

## 9. Herramientas antes que clicks

Lo que ya tiene herramienta no se hace a mano:

| Herramienta | Para qué |
|---|---|
| `correr.py` | Corre los sets (`correr.py S`, `A,B,C`, `--todos`, `--listar`) y reporta OK/FALLA/N-A |
| `ver_solicitud.py` | Estado, reglas con su razón, filas y Bitácora de una Solicitud |
| `estado_sesiones.py` | Las cuatro sesiones: quién es, si vive, si el rol coincide |
| `probar_rpa.py` | El ciclo completo del bot, como el bot |
| `comportamiento_roles.py` | Privilegios por rol, con impersonación, sin navegador |
| `verificar_accesos.py` | Accesos de los usuarios de prueba |
| `preparar_datos.py` | Siembra el fixture |
| `generar_excel.py` | Arma los adjuntos de los casos |
| `graph_api.py` | Manda y lee correo de los buzones de prueba |
| `marca_agua.py` · `semaforo.py` | Aislamiento (§5) |

Si un caso necesita algo que no tiene herramienta, se evalúa escribirla antes de resignarse a
clickear: lo que se hace a mano se hace distinto cada vez.

---

## 10. Lo que este documento NO decide

- **Qué se prueba.** Eso lo dicen el diseño (`diseno/02` a `07`), las directrices
  (`diseno/DIRECTRICES-NUEVAS.md`) y los contratos (`diseno/03`).
- **Cuándo se corre.** Eso es del plan de trabajo.
- **Qué hacer con un FALLA.** Un defecto confirmado va a `diseno/PENDIENTES.md` con su ID y su
  decisión pendiente, no se arregla dentro del archivo de pruebas.
