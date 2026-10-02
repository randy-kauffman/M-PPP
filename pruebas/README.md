# Pruebas funcionales — MPPP

Proyecto: `2026-001-referencias-planes-pago` · Entorno: **Dev**

Los componentes de la fase 1 y de la fase 2 (RPA, DN-01/02/03) están construidos y verificados
**estructuralmente**: existen, están bien formados y están en la solución. Lo que estas pruebas
comprueban es otra cosa, la que importa: **que hagan lo que el diseño dice que hacen**.

---

## Cómo está organizado

Cada archivo de esta carpeta es un **set autocontenido** (`CONVENCIONES.md` §8: no depende de otro
archivo, prepara por su cuenta todo lo que necesita) con su letra, corrido por
`herramientas/pruebas/correr.py`:

| Letra | Archivo | Qué cubre | Casos |
|---|---|---|---|
| C | [`01-clasificacion-del-correo.md`](01-clasificacion-del-correo.md) | `MPPP-REC`/`MPPP-ING`, el plugin liviano, reglas de nivel **Correo**, y DN-01 (autorización por cliente) en la clasificación | 11 |
| S | [`02-reglas-de-solicitud.md`](02-reglas-de-solicitud.md) | Reglas de nivel **Solicitud**: adjunto, Excel, estructura, filas | 7 |
| R | [`03-reglas-de-registro.md`](03-reglas-de-registro.md) | Reglas de nivel **Registro**: las que deciden fila por fila, más DN-01, y DN-03 completo (absorbió el set O) | 28 |
| M | [`04-comunicaciones.md`](04-comunicaciones.md) | `MPPP-COM`/`MPPP-ENV`: acuse y respuesta final, correo **real** de punta a punta | 7 |
| A | [`05-app-y-comandos.md`](05-app-y-comandos.md) | Los comandos de la barra y el diálogo de motivo, por API con `MSCRMCallerID`, y la lección del botón fantasma (ex H-06) | 15 |
| G | [`06-seguridad-vistas-formularios.md`](06-seguridad-vistas-formularios.md) | Roles, privilegios efectivos, sitemap, vistas, formularios (absorbió el resto del set H) | 20 |
| V | [`07-vigilancia.md`](07-vigilancia.md) | `MPPP-VIG`: los 5 bloques de recuperación | 7 |
| I | [`08-integridad-y-plugins.md`](08-integridad-y-plugins.md) | Steps de plugin, catálogo de reglas, claves únicas, la puerta del RPA | 13 |
| B | [`09-rpa-fase2.md`](09-rpa-fase2.md) | El bot RPA: Custom API, claim, orden FIFO, catálogo de errores | 20 |
| U | [`10-autorizacion-cliente.md`](10-autorizacion-cliente.md) | DN-01: autorización por cliente, con interruptor a plan | 11 |
| E | [`11-exportacion-excel.md`](11-exportacion-excel.md) | DN-02: descargar a Excel lo que hay que digitar | 10 |
| L | [`12-catalogos-masivos.md`](12-catalogos-masivos.md) | DN-04: carga masiva y activación masiva de catálogos por Excel (menús, plantillas, `MPPP-CAR-001` a `011`, reglas por fila, todo o nada, dueño del cliente nuevo) y Comentarios en la autorización por cliente | 57 |

**206 casos** (contados de los encabezados de caso de cada archivo el 2026-10-01: los 149 de los
sets C a E, contados el 2026-09-28 después del reagrupamiento de sets, más los 57 del set L). Cada uno
trae: qué prueba, cómo hacerlo, **qué tiene que pasar** y cómo verificarlo.

> **Set L, nota al 2026-10-01.** El set L **todavía no tiene `herramientas/pruebas/sets/set_l.py`**:
> `correr.py` no lo conoce y `--listar` no lo muestra. Su archivo trae el estado de cada caso en su
> tabla `## Resultados`, con tres estados que **no se mezclan**: *EJECUTADO — OK* (con evidencia
> citada: el arnés de Playwright `pruebas/gui/casos/dn-04-catalogos-masivos.spec.mjs` y las
> mediciones de Dev de D-73/D-75), *CUBIERTO POR PRUEBAS AUTOMÁTICAS* (solo la suite de C#, sin
> correr en Dev: no es un OK) y *PENDIENTE*. Al 2026-10-01: 13 ejecutados, 34 cubiertos por pruebas
> automáticas y 10 pendientes.

> **Set G, nota al 2026-09-28.** El archivo documenta 20 casos (`G-06` quedó fusionada en `G-00`/
> `G-00b`, así que no cuenta como caso aparte). De esos 20, tres siguen siendo GUI sin arnés todavía
> (`G-02` íconos, `G-07` recorte del sitemap por rol, `G-11` multiidioma — documentados con el
> comando exacto, sin función en `CASOS`): no es una migración a medias, es un hueco de cobertura
> conocido. `correr.py --listar` siempre dice la verdad del momento, más que este número.

### Tabla de equivalencias — el reagrupamiento de sets del 2026-09-28

Trece sets se reagruparon en once (`CONVENCIONES.md` §4, "La regla del nombre": el nombre de un set
dice QUÉ prueba, nunca cuándo se escribió ni qué pedido lo originó). `12-obligatoriedad-gestion.md`
y `13-construido-hoy.md` desaparecieron. Un reporte o una nota vieja que cite un ID de antes del
2026-09-28 se sigue leyendo con esta tabla:

| ID viejo | Archivo viejo | → | ID nuevo | Archivo nuevo |
|---|---|---|---|---|
| `O-01` | `12-obligatoriedad-gestion.md` | → | `R-19` | `03-reglas-de-registro.md` |
| `O-02` | `12-obligatoriedad-gestion.md` | → | `R-20` | `03-reglas-de-registro.md` |
| `O-03` | `12-obligatoriedad-gestion.md` | → | `R-21` | `03-reglas-de-registro.md` |
| `O-04` | `12-obligatoriedad-gestion.md` | → | `R-22` | `03-reglas-de-registro.md` |
| `O-05` | `12-obligatoriedad-gestion.md` | → | `R-23` | `03-reglas-de-registro.md` |
| `O-06` | `12-obligatoriedad-gestion.md` | → | `R-24` | `03-reglas-de-registro.md` |
| `O-07` | `12-obligatoriedad-gestion.md` | → | `R-25` | `03-reglas-de-registro.md` |
| `O-08` | `12-obligatoriedad-gestion.md` | → | `R-26` | `03-reglas-de-registro.md` |
| `O-09` | `12-obligatoriedad-gestion.md` | → | `R-27` | `03-reglas-de-registro.md` |
| `O-10` | `12-obligatoriedad-gestion.md` | → | `R-28` | `03-reglas-de-registro.md` |
| `H-01` | `13-construido-hoy.md` | → | `G-14` | `06-seguridad-vistas-formularios.md` |
| `H-02` | `13-construido-hoy.md` | → | `G-15` | `06-seguridad-vistas-formularios.md` |
| `H-03` | `13-construido-hoy.md` | → | `G-16` | `06-seguridad-vistas-formularios.md` |
| `H-04` | `13-construido-hoy.md` | → | `G-17` | `06-seguridad-vistas-formularios.md` |
| `H-05` | `13-construido-hoy.md` | → | `G-18` | `06-seguridad-vistas-formularios.md` |
| `H-06` | `13-construido-hoy.md` | → | `A-15` | `05-app-y-comandos.md` |
| `H-07` | `13-construido-hoy.md` | → | `G-19` | `06-seguridad-vistas-formularios.md` |

**Por qué `O` entra entero en `R` y `H` se reparte entre `A` y `G`, y por qué `U` no se tocó**: la
obligatoriedad por gestión (`O`) **ES** una de las ocho reglas de Registro — un tema, un archivo.
Los siete casos de `H` no compartían un solo tema entre sí: seis eran de sitemap/vistas/formulario
(el tema de `G`) y uno era de un comando de la barra (el tema de `A`), así que cada uno fue a donde
correspondía por TEMA, no los siete juntos a un archivo por fecha. `U` (autorización por cliente)
sigue aparte porque es transversal a dos niveles (Correo y Registro), no porque no se haya
revisado — ver `CONVENCIONES.md` §4 para el detalle completo de los tres casos.

### El set P desapareció

`00-preparacion.md` sigue existiendo, pero **ya no es un set** (no tiene letra, `correr.py` no lo
corre). Sus seis verificaciones se mudaron a `herramientas/pruebas/preparacion_comun.py`
(`verificar_datos_negocio`, `verificar_excels`, `verificar_flujos_activos`,
`verificar_estructura_plantilla`, `verificar_parametro_activo`, `verificar_roles`), y cada set migrado
llama las que necesita desde su propio `preparar()` — así, cada set queda autocontenido de verdad
(`CONVENCIONES.md` §8) en vez de depender de "correr `00` primero".

Lo que **sigue siendo puramente manual** — y por eso `00-preparacion.md` queda como referencia, no
como set — son las tres cosas que no son Dataverse: la carpeta `Procesados` del buzón compartido
(P-01), los permisos de ese buzón en el Exchange admin center (P-01b) y la elección de la casilla
real desde la que se manda correo de prueba (P-02).

---

## Cómo se corre

```bash
python3 herramientas/pruebas/correr.py S            # un set
python3 herramientas/pruebas/correr.py A,B,C        # varios, en ESE orden (no el canónico)
python3 herramientas/pruebas/correr.py --todos      # todos los sets descubiertos, en orden canónico
python3 herramientas/pruebas/correr.py --listar     # qué sets hay y cuántos casos, sin tocar Dev
python3 herramientas/pruebas/correr.py --todos --full   # TODO prendido: ver la sección de abajo
```

`correr.py` toma el semáforo (`semaforo.py`), aplica el canon de parámetros de `CONVENCIONES.md` §8.2
antes y después de cada set, corre `preparar → CASOS → cerrar` (`cerrar` siempre, aunque algo reviente
antes), e imprime en vivo caso por caso. Un set que no se pudo preparar no corre sus casos: cero casos
corridos es un resultado honesto, casos verdes sobre un entorno mal preparado es una mentira.

Tené a mano la herramienta de lectura, que evita andar clickeando para ver qué pasó:

```bash
python3 herramientas/pruebas/ver_solicitud.py MPPP-00001234
python3 herramientas/pruebas/ver_solicitud.py --ultima
```

**Este documento (`README.md`) dice CÓMO está organizado el plan** — qué archivo cubre qué, cuántos
casos tiene, cómo se corre. **`CONVENCIONES.md` dice CÓMO se escribe una prueba** — el formato de un
caso, el contrato de un set, el canon de configuración, el manejo de datos y sesiones. Si estás
escribiendo o migrando un caso, es `CONVENCIONES.md` lo que tenés que seguir; si estás buscando dónde
vive algo, es este archivo.

---

## Cómo anotar los resultados

Cada archivo termina con una tabla `## Resultados` para llenar (o ya llena, de la última corrida
real). Los tres estados posibles:

- **OK** — pasó exactamente lo esperado
- **FALLA** — pasó otra cosa. Anotá qué pasó, con el número de solicitud
- **N/A** — no se pudo correr, y por qué (`c.na(motivo)` en el código)

**Un caso que "casi" pasa es un FALLA.** Si el estado es el correcto pero el mensaje al cliente dice
otra cosa, es un FALLA: el mensaje lo lee el cliente del banco.

---

## Esto escribe en Dev

Los casos crean Solicitudes, Filas y Bitácora reales, y algunos (el set M, y los casos "lentos" del
set V) mandan correo real desde buzones de prueba dedicados. Todo con datos ficticios (prefijo `PR*`
para el fixture de negocio compartido, `ZZP-`/`zz-` para lo que crea cada corrida) y cada set borra
por GUID, en su propio `cerrar()`, lo que él mismo creó — nunca lo de otro set ni lo de otro agente
corriendo en paralelo (`CONVENCIONES.md` §5).

---

## Casos de alto riesgo, apagados por default — y el modo `--full`

Tres banderas de entorno prenden casos que por default quedan `N-A` porque su costo (tiempo real) o
su riesgo (degradar el sandbox compartido de Dev, tocar un flujo real) no se paga en cada corrida:

| Bandera | Qué prende | Por qué está apagada por default |
|---|---|---|
| `MPPP_E08_ALTO_RIESGO=si` | `E-08` (set E): ~300.000 filas hasta el límite real de memoria del sandbox | La llamada real tarda 150-170s y puede degradar el sandbox de Dataverse Dev para cualquier otro usuario mientras corre. Es un riesgo ya medido y aceptado, no un defecto — correrlo de nuevo no agrega cobertura, solo riesgo. |
| `MPPP_V02_ALTO_RIESGO=si` | `V-02` (set V): apaga `Cloud Flow - MPPP - REC` DE VERDAD, corre la prueba, lo vuelve a encender | Es la única excepción de todo el arnés a "nunca se toca el estado de un flujo" — autorizada puntualmente por el aprobador el 2026-09-28, con dos redes de seguridad (`CONVENCIONES.md`, "Excepción V-02"). |
| `MPPP_V_LENTOS=si` | `V-03` a `V-07` (set V): los bloques que dependen del schedule real de `MPPP-VIG` (recurrence de 10 minutos) | Con el margen que necesita este set (3 vueltas completas + colchón), los cinco casos juntos superan las dos horas y media. |

```bash
MPPP_E08_ALTO_RIESGO=si python3 herramientas/pruebas/correr.py E
MPPP_V02_ALTO_RIESGO=si python3 herramientas/pruebas/correr.py V
MPPP_V_LENTOS=si python3 herramientas/pruebas/correr.py V
```

`E-07` (corte por presupuesto de tiempo) queda siempre `N-A` en `correr.py`, por una razón distinta:
la medición real ya mostró que ni 250.000 posiciones de `filaids` se acercan al umbral, y forzar más
volumen por API es el mismo riesgo de `E-08` sin agregar cobertura — esa cobertura puntual la da la
prueba unitaria con reloj inyectado (`Aceptacion/ExportarParaDigitarApiAceptacion.cs`).

### `--full` — un solo interruptor para "corré todas, todas sin importar nada"

Decisión del aprobador (2026-09-28): en vez de tener que acordarse de las banderas sueltas de
arriba, un solo parámetro las prende todas de una:

```bash
python3 herramientas/pruebas/correr.py --todos --full   # todo, con TODO prendido
python3 herramientas/pruebas/correr.py V --full          # solo el set V, con lo suyo prendido
python3 herramientas/pruebas/correr.py E --full          # solo el set E, con lo suyo prendido
```

`--full` es la forma **recomendada** de prender `MPPP_E08_ALTO_RIESGO`, `MPPP_V02_ALTO_RIESGO` y
`MPPP_V_LENTOS` a la vez — pero no reemplaza a las banderas sueltas, las incluye: quien ya las usa
por separado sigue funcionando exactamente igual.

**Antes de arrancar, `--full` grita un banner** con qué casos extra va a correr, cuánto tarda de
más y qué riesgos asume — nadie se tiene que enterar cuarenta minutos después de que el arnés está
haciendo algo caro o riesgoso. `python3 herramientas/pruebas/correr.py --listar` también distingue,
set por set, qué corre siempre y qué solo corre con `--full`.

**Qué cuesta un `--full` completo (`--todos --full`):** además de los ~150-170s de `E-08`, el set V
con TODO prendido (`V-02` + `V-03`..`V-07`) supera las **3 horas** — el schedule real de `MPPP-VIG`
(10 minutos) no se puede acelerar desde afuera. Correr `V --full` o `E --full` por separado, para
verificar un cambio puntual, es mucho más rápido que un `--todos --full` completo.

**`B-01` (set B) queda afuera de `--full`, A PROPÓSITO.** No es un olvido: `B-01` renombra el rol
`SR - MPPP - RPA` y rompe la puerta del bot para TODO el entorno mientras dura el renombre. El
aprobador confirmó explícitamente que sigue `N-A` incluso con `--full` (`pruebas/09-rpa-fase2.md`);
ya está cubierto por 8 pruebas unitarias (7 de rechazo). `--full` no tiene ninguna bandera que lo
prenda — no existe.

---

## Rojo a propósito: los defectos abiertos que hacen fallar casos, y por qué eso está bien

Estos siete defectos de `diseno/PENDIENTES.md` están **ABIERTOS**, pospuestos por el aprobador. Los
casos que los cruzan dan **FALLA** a propósito, y así tiene que quedar hasta que alguien los cierre:
**ese rojo es un sensor que sigue funcionando, no un defecto de las pruebas.** Si alguna vez ves uno
de estos casos en verde sin que el defecto se haya cerrado en `PENDIENTES.md`, sospechá del cambio,
no le creas a la corrida.

| Defecto | Dónde pega | Casos que lo cruzan | Qué pasa |
|---|---|---|---|
| **D-59** | `Datos/Avisos.cs:109` | C-02, C-04, C-10, C-11 (set C); R-17 (set R); U-03, U-04, U-05 (set U) | Cast de `systemuserid` como `EntityReference` en vez de `Guid` sobre `systemuserroles`: el aviso a los ejecutivos falla siempre que un correo no se puede procesar. |
| **D-60** | `Api/RegistrarResultadoRpa.cs` → `AplicarRechazada` | B-10, B-17 (set B) | `resultado: "rechazada"` revienta con error genérico incluso con un solo ítem válido; además viola "nunca todo o nada" (un ítem bueno se pierde en el mismo lote). |
| **D-61** | `sanic_mppp_capi_obtenerfilaspendientes` | B-05, B-06 (set B) | `maximo` está declarado opcional pero omitirlo devuelve HTTP 400 en vez de aplicar el defecto documentado (100). |
| **D-62** | `recursos/flujos/mppp-vig.json` (arranque) | V-01 (set V, en rojo a propósito) | La etapa de arranque de los 5 bloques de `MPPP-VIG` solo encadena con `["Succeeded"]`: si un paso de arranque falla, los CINCO bloques de recuperación se saltean esa corrida. |
| **D-63** | `Steps/ReglasDeSteps.cs:26` (`PermitidasEnFila`) | A-14 (set A) | La lista blanca de escrituras sobre Fila no incluye `sanic_requiereatencionmanual`: el botón "Atendida" da HTTP 400 real, y la fila queda trabada (el bot no la puede tomar, la persona no la puede liberar). |
| **D-64** | DA-08 — avisos de la app (`Datos/Avisos.cs`) | G-10 (set G) | DA-08 define 5 avisos en la campana; el código solo implementa 1, y ese es justo el que revienta por D-59. Cuatro de los cinco avisos no existen. |
| **I-07** | `pruebas/08-integridad-y-plugins.md` | I-07 (set I) | Un `PATCH` directo a `sanic_referencia` por Web API se guarda y `ListaBlancaStep` no lo bloquea — la protección de solo lectura es del formulario, no del dato. |

---

## Qué NO cubren estas pruebas

- **Rendimiento real bajo volumen productivo.** Los casos de volumen (`E14`, `E-08`) miden el
  presupuesto de la plataforma, no una prueba de carga con el tráfico real de producción.
- **Concurrencia real de carga** (dos correos simultáneos, dos supervisores aprobando la misma fila
  al mismo instante). El mecanismo de claim con vencimiento (`sanic_claimhasta`) ya está implementado
  y probado (set B, set I), pero no bajo carga concurrente real.
- **Migración desde SharePoint** (RF-16): es de Producción, no de Dev.
- **Fase 3** (histórico): todavía no construida.
- **Lo que solo se ve en un navegador** (ribbon, íconos, multiidioma renderizado, recorte de sitemap
  por rol): eso lo cubre el arnés de Playwright en `pruebas/gui/` (ver su propio README), no este
  motor de casos por API.
