# 01 — Clasificación del correo

Set **C** (`CONVENCIONES.md` §4). Nivel **Correo**: `sanic_mppp_capi_clasificarcorreo` y sus dos
reglas, `ES_CORREO_NUEVO` y `REMITENTE_RECONOCIDO`.

Cadena de dependencias (`sanic_dependede`, verificada hoy contra `playbooks/datos/reglas.md`):

```
ES_CORREO_NUEVO → REMITENTE_RECONOCIDO
```

Las dos con efecto **Envía a revisión** (es el único efecto válido en nivel Correo, `02` §2.6): que
`ES_CORREO_NUEVO` dé No cumplida deja la Solicitud en **No es correo nuevo**; que
`REMITENTE_RECONOCIDO` dé No cumplida la deja en **No reconocida**. Ninguna de las dos manda
comunicación al cliente — ese es justamente el punto: un correo no reconocido, o una respuesta
automática, no tienen que generar una segunda vuelta de correos.

**Set autocontenido** (`CONVENCIONES.md` §8). Este archivo no depende de ningún otro: prepara sus
propios datos, no usa ninguna sesión de navegador (nivel Correo se prueba entero contra la Custom
API, sin abrir la app) y deja el canon de configuración global al terminar. La preparación exacta
está documentada en `herramientas/pruebas/sets/set_c.py::preparar()` — acá solo el resumen:

| Qué prepara | Cómo |
|---|---|
| Datos de negocio (`PR*`, fixture compartido) y `REMITENTE` autorizado sobre `PR11` | `preparacion_comun.verificar_datos_negocio()` (antiguo P-03) |
| Los 15 Excel + el adjunto no-Excel | `preparacion_comun.verificar_excels()` (antiguo P-04); este archivo solo usa `E01-feliz-formato11.xlsx` |
| `autorizacion.modo` activo (DN-01) | `preparacion_comun.verificar_parametro_activo()` — ya sembrado en Dev desde el 2026-09-26, no hace falta crearlo |
| Autorizado + `AutorizacionCliente` privados de DN-01 (`REMITENTE_DN01`, autorizado SOLO por cliente, cero `AutorizacionPlan`) | `sets/set_c.py::_preparar_autorizacion_por_cliente()` (función local, no está en `preparacion_comun.py` — ver "Funciones para consolidar" al final) |

**Corré este set con**:

```bash
python3 herramientas/pruebas/correr.py C
```

---

## C-01 · Camino feliz — correo nuevo, remitente autorizado, Excel válido

**Qué prueba.** La cadena entera: clasificación (`ES_CORREO_NUEVO`, `REMITENTE_RECONOCIDO`) →
validación → referencia de formato 11.

**Necesita.** El `REMITENTE` de este set, autorizado sobre `PR11` (`preparar()`); el adjunto
`E01-feliz-formato11.xlsx`.

**Cómo.** Correo nuevo, asunto `Prueba C-01 inclusiones`, adjuntando **solo**
`E01-feliz-formato11.xlsx`.

**Qué tiene que pasar.**

| Qué | Valor |
|---|---|
| clasificación / procesar | `nuevo` / `true` |
| Estado | **En proceso** |
| filas t/v/r | `3 / 3 / 0` |
| Reglas de Correo y de Solicitud (las 7) | todas **Cumplida** |
| Las 3 filas | **Validada**, sin mensaje |
| Referencia de cada fila | `007` + cuenta rellena a 17 = **20 caracteres** (`00700010203040506070`, `00700011223344556677`, `00700099887766554433`) |
| Acuse | presente |

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py C` (caso `C-01`); a mano,
`ver_solicitud.py <MPPP-########>`.

**Cómo se sabe que falló.** Si alguna referencia mide distinto de 20 o no empieza en `007`, es
**FALLA grave**: esa referencia va a AS400 y un dígito corrido manda la plata a otra cuenta — no es
lo mismo que una regla mal evaluada (eso se ve en `ES_CORREO_NUEVO`/`REMITENTE_RECONOCIDO` en
Omitida o No cumplida) que un cálculo de referencia mal armado con las reglas en verde.

**Automatizable.** API (`correr.py C`, caso `C-01`, vía `simulador.py`, sin correo real). Con
correo real es manual: enviar y esperar 1-2 minutos.

---

## C-02 · Una respuesta NO se procesa

**Qué prueba.** `ES_CORREO_NUEVO` con una respuesta (DF-08): lo que impide un bucle de correos con
un "fuera de oficina".

**Necesita.** El `REMITENTE` de este set; el adjunto `E01-feliz-formato11.xlsx`; un
`In-Reply-To` simulado (`simulador.py` arma un `.eml` con esa cabecera).

**Cómo.** Correo con asunto `RE: algo anterior`, `In-Reply-To` apuntando a un mensaje anterior,
adjuntando `E01-feliz-formato11.xlsx`.

**Qué tiene que pasar.**

| Qué | Valor |
|---|---|
| clasificación / procesar | `respuesta` / `false` |
| Estado | **No es correo nuevo** |
| filas | **0** — no se abrió el Excel |
| `ES_CORREO_NUEVO` | **NO CUMPLIDA** |
| `REMITENTE_RECONOCIDO` | **Omitida** (la bloqueó la anterior) |
| Comunicación al cliente | **NINGUNA** |

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py C` (caso `C-02`).

**Cómo se sabe que falló.** Si `ES_CORREO_NUEVO` da Cumplida con `In-Reply-To` presente, la regla
no está mirando la cabecera. Si da correctamente No cumplida pero `REMITENTE_RECONOCIDO` NO queda
en Omitida (Cumplida o No cumplida), el motor de dependencias (DD-13) está evaluando una regla que
debería saltear — y si además hay acuse, hay un bucle de correo automático real, el peor de los dos
síntomas.

**Automatizable.** API (`correr.py C`, caso `C-02`, simula la respuesta sin correo real).

---

## C-03 · Un reenvío SÍ se procesa

**Qué prueba.** Que `ES_CORREO_NUEVO` distinga reenvío de respuesta por el **prefijo del asunto**
(`FW:`, `FWD:`, `RV:`, `REENV:`, de `correo.prefijos.reenvio`), no por la presencia de
`In-Reply-To`/`References` (un reenvío también las trae).

**Necesita.** El `REMITENTE` de este set; el adjunto `E01-feliz-formato11.xlsx`; un
`In-Reply-To` simulado (igual que C-02, a propósito: para aislar que lo que decide es el prefijo).

**Cómo.** Correo con asunto `FW: Prueba C-03 reenvio`, mismas cabeceras de "respuesta" que C-02,
adjuntando `E01-feliz-formato11.xlsx`.

**Qué tiene que pasar.** Igual que C-01: `nuevo`/`true`, **En proceso**, 3 filas Validadas,
`ES_CORREO_NUEVO` **Cumplida**.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py C` (caso `C-03`).

**Cómo se sabe que falló.** Si este caso queda en **No es correo nuevo** con las mismas cabeceras
que C-02 salvo el prefijo, la regla no está mirando el asunto — está copiando el criterio de
`In-Reply-To` de una respuesta común.

**Automatizable.** API (`correr.py C`, caso `C-03`).

---

## C-04 · Remitente no reconocido

**Qué prueba.** `REMITENTE_RECONOCIDO` (RF-02) en su forma de siempre: modo `plan`, sin ninguna
autorización.

**Necesita.** Un correo que **nunca** se registra como Autorizado (`REMITENTE_NO_AUTORIZADO`, no
hace falta ninguna preparación: la ausencia de la fila es justamente la condición del caso); el
adjunto `E01-feliz-formato11.xlsx`.

**Cómo.** Correo desde `REMITENTE_NO_AUTORIZADO`, con `E01-feliz-formato11.xlsx` adjunto.

**Qué tiene que pasar.**

| Qué | Valor |
|---|---|
| clasificación / procesar | `remitente_no_reconocido` / `false` |
| Estado | **No reconocida** |
| filas | **0** |
| `ES_CORREO_NUEVO` | Cumplida |
| `REMITENTE_RECONOCIDO` | **NO CUMPLIDA** |
| Comunicación | **NINGUNA** |

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py C` (caso `C-04`).

**Cómo se sabe que falló.** Si `REMITENTE_RECONOCIDO` da Cumplida para un correo que nunca se
autorizó, alguien está reconociendo por defecto (fail-open) — el peor tipo de defecto de esta
regla: cualquiera podría mandar filas a digitar sin autorización.

**Automatizable.** API (`correr.py C`, caso `C-04`, con un remitente propio no autorizado).

---

## C-05 · El correo se mueve y cambia de identificador

**Qué prueba.** El paso 7 de `MPPP-ING` y DF-04: el identificador de Outlook **cambia al mover**, y
por eso hay una acción aparte que lo vuelve a guardar.

**Necesita.** Un correo real, procesado de punta a punta por `MPPP-REC`/`MPPP-ING` contra el buzón
compartido `mppp@55xljh.onmicrosoft.com`, y acceso a ese buzón para ver la carpeta `Procesados`.

**Cómo.** Mandar un correo real a `mppp@55xljh.onmicrosoft.com` y, con acceso al buzón, mirar dónde
queda y qué campo trae la Solicitud resultante.

**Qué tiene que pasar.**

- El correo queda en **`Procesados`**, no en la bandeja de entrada.
- `outlook message id` dice **sí**.

**Cómo se verifica.** Acceso manual al buzón compartido, más `ver_solicitud.py <MPPP-########>`
para el campo del identificador.

**Cómo se sabe que falló.** Si el correo queda en la bandeja, `MPPP-VIG` lo va a reingerir a los 15
minutos — vas a ver reintentos en la Bitácora (protegidos por C-07), no una segunda Solicitud.

**Automatizable.** `N-A` — **investigado de nuevo hoy (2026-09-27), con una herramienta nueva que
antes no existía.** `herramientas/graph_api.py` ya manda y lee correo real contra Graph (probado
hoy: HTTP 202 al mandar, `Mail.Read` disponible), así que en principio podría inspeccionar carpetas
de un buzón real — lo que decía la versión anterior de este caso ("no hay Web API para inspeccionar
carpetas de Outlook") ya no es del todo cierto. Pero se comprobó, programáticamente y sin imprimir
ninguna dirección, que la Application Access Policy del service principal de Graph lo limita a 4
buzones de prueba (`PP_MAIL_BUZON_1`..`PP_MAIL_BUZON_4`) y **ninguno de los 4 es
`mppp@55xljh.onmicrosoft.com`**: Graph puede *mandar* un correo hacia ese buzón (el destino no
necesita permiso), pero no puede *leer sus carpetas* — un 403 esperado, no un defecto. No es un
caso de navegador (no hay pantalla de la app que mirar, es un tema de Exchange): `pruebas/gui/` no
aplica. Reproducción manual, con los dos flujos activos (verificado hoy, `statecode=1` los dos):
mandar un correo real a `mppp@55xljh.onmicrosoft.com`, esperar 1-2 minutos, y confirmar a mano, con
acceso al buzón compartido, que el correo pasó a `Procesados`.

---

## C-06 · El `.eml` queda guardado con su nombre real

**Qué prueba.** RF-10 y el insumo del plugin liviano: sin el `.eml` no hay clasificación; y que el
archivo se guarda con metadatos reales (nombre original), no un nombre de relleno.

**Necesita.** La misma Solicitud real de C-05 (un correo real ya procesado por `MPPP-ING`).

**Cómo.** Con esa Solicitud, leer `sanic_correocrudo_name` y `sanic_exceloriginal_name`.

**Qué tiene que pasar.** `sanic_correocrudo_name` trae el nombre real que Outlook le dio al
mensaje; `sanic_exceloriginal_name` trae el nombre original del adjunto `.xlsx` tal como lo mandó
quien escribió el correo.

**Cómo se verifica.** `python3 herramientas/pruebas/ver_solicitud.py <MPPP-########>` (línea
`archivos`), o un GET directo:
`sanic_mppp_tbl_solicituds(<id>)?$select=sanic_correocrudo_name,sanic_exceloriginal_name`.

**Cómo se sabe que falló.** Si alguno de los dos nombres viene vacío o con un nombre inventado por
el propio flujo (en vez del real), `MPPP-ING` no está preservando el nombre original del archivo
que capturó de Outlook.

**Automatizable.** `N-A` — mismo bloqueo de fondo que C-05 (hace falta el correo real). **Novedad
de hoy**: la VERIFICACIÓN en sí ya no exige la app ni una sesión de navegador — antes esta hoja
decía "abrí la Solicitud en la app, pestaña con los archivos" (ver "Corrección al MD" abajo);
`sanic_correocrudo_name`/`sanic_exceloriginal_name` son campos de Web API comunes, y
`ver_solicitud.py` ya los usa para su resumen. `simulador.py` sube el `.eml` con un nombre FIJO
(`correo.eml`), así que corriendo el caso simulado se probaría al simulador, no al sistema — por
eso sigue en `N-A` y no se "resuelve" con datos simulados. Falta el correo real de C-05, no una
herramienta de verificación.

> **Corrección al MD (2026-09-27).** La versión anterior de este caso pedía "sesión con acceso de
> lectura a Solicitud (Ejecutivo o Supervisor)" para abrir la app y descargar el archivo a mano. Es
> un error de alcance: los dos campos que este caso necesita (`_name`) se leen por Web API sin
> ninguna sesión de aplicación — la verificación es más simple de lo que decía la hoja anterior, el
> obstáculo real siempre fue (y sigue siendo) el correo real, no la falta de acceso a la app.

---

## C-07 · El mismo correo dos veces no duplica

**Qué prueba.** La clave alternativa sobre `sanic_messageid` (DD-03) y el paso 4b de `MPPP-ING`.

**Necesita.** El `REMITENTE` de este set; el adjunto `E01-feliz-formato11.xlsx`.

**Cómo.** Se simula la reingesta: un segundo `Create` con el **mismo** `sanic_messageid` que la
Solicitud original — exactamente lo que hace `Crear_la_solicitud` de `mppp-ing.json` cuando
`MPPP-VIG` reingiere el mismo correo.

**Qué tiene que pasar.**

- El segundo `Create` es **rechazado** por la clave alternativa (DD-03).
- Solo existe **UNA** Solicitud con ese `sanic_messageid`.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py C` (caso `C-07`). Con correo real:
pedirle a quien tenga acceso al buzón que mueva el correo de `Procesados` de vuelta a la bandeja;
`MPPP-VIG` lo reingiere en los próximos 15 minutos.

**Cómo se sabe que falló.** Si el segundo `Create` se acepta, la clave alternativa no está
realmente aplicada en el entorno — el peor escenario del sistema: procesar la misma plantilla dos
veces y mandarle al cliente dos acuses distintos.

**Automatizable.** API (`correr.py C`, caso `C-07`, simulado sin mover correo real). Con correo
real es manual: requiere acceso al buzón compartido y esperar a `MPPP-VIG`.

---

## C-08 · Sin adjunto

**Qué prueba.** `TRAE_ADJUNTO` y, sobre todo, que las reglas que dependen de ella (todas las de
nivel Solicitud) queden **Omitidas** y no "No cumplida" (DD-13).

**Necesita.** El `REMITENTE` de este set; un correo sin ningún archivo adjunto.

**Cómo.** Correo nuevo, asunto `Prueba C-08 sin adjunto`, **sin ningún archivo adjunto**.

**Qué tiene que pasar.**

| Qué | Valor |
|---|---|
| clasificación / procesar | `nuevo` / `true` (el remitente SÍ está reconocido: esto es nivel Solicitud, no Correo) |
| Estado | **Rechazada** |
| filas | 0 |
| `TRAE_ADJUNTO` | **NO CUMPLIDA** |
| `ADJUNTO_ES_EXCEL`, `UN_SOLO_EXCEL`, `ESTRUCTURA_PLANTILLA`, `TIENE_FILAS` | **Omitida** |
| Acuse | **sí**, y no nombra la regla técnica `TRAE_ADJUNTO` |

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py C` (caso `C-08`).

**Cómo se sabe que falló.** Ojo con la firma con logo: una imagen incrustada llega como adjunto
`isInline = true` y **no cuenta** (DF-07) — si `cantidadadjuntos` viniera contando esa imagen, la
regla no estaría filtrando los inline; acá se simula directo con `cantidad_adjuntos=0`, así que
esa causa puntual no aplica a este caso simulado (si el correo real la mostrara, sería un defecto
de `MPPP-ING`, fuera de lo que `TRAE_ADJUNTO` evalúa).

**Automatizable.** API (`correr.py C`, caso `C-08`).

---

## C-09 · DN-01, modo `cliente`: autorizado SOLO por `AutorizacionCliente` SÍ entra

**Qué prueba.** Que `REMITENTE_RECONOCIDO` (nivel Correo) resuelve `autorizacion.modo` — el bug
real que corrigió el aprobador el 2026-09-26 ("el portero también tiene que preguntar lo mismo"):
antes de esa corrección, un correo autorizado solo por cliente pasaba `AUTORIZACION_CORREO_PLAN`
(nivel Registro) pero quedaba igual **No reconocida** en nivel Correo, y una No reconocida no
recibe respuesta — el cliente mandaba su solicitud y no se enteraba de nada.

**Necesita.** `autorizacion.modo = cliente` (lo fija el propio caso); un Autorizado
(`REMITENTE_DN01`, privado de este set) con **una única vía**: `AutorizacionCliente` hacia el
Cliente `PR - Pruebas MPPP SA` (dueño de `PR11`) y **cero** `AutorizacionPlan` (`preparar()`); el
adjunto `E01-feliz-formato11.xlsx`.

**Cómo.** Con `autorizacion.modo` en `cliente`, correo desde `REMITENTE_DN01` con
`E01-feliz-formato11.xlsx` adjunto.

**Qué tiene que pasar.**

| Qué | Valor |
|---|---|
| clasificación / procesar | `nuevo` / `true` |
| `REMITENTE_RECONOCIDO` | **Cumplida** |
| Estado | **En proceso** (la cadena sigue funcionando después del portero) |

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py C` (caso `C-09`).

**Cómo se sabe que falló.** Si la Solicitud queda **No reconocida**, el nivel Correo no está
resolviendo el modo `cliente` — es exactamente el bug que este caso fija. Si en cambio queda
reconocida pero el resto de la cadena se rompe (no llega a **En proceso** con las reglas de
Solicitud en verde), el defecto ya no es de `REMITENTE_RECONOCIDO`: es de otro nivel, fuera del
alcance de este archivo (ver `03-reglas-de-registro.md`, letra R).

**Automatizable.** API (`correr.py C`, caso `C-09`).

---

## C-10 · DN-01, modo `plan`: el MISMO correo (autorizado solo por cliente) YA NO entra

**Qué prueba.** El complemento de C-09, con el MISMO remitente: prueba que es el **modo** el que
cambia el resultado, no una diferencia entre remitentes. En modo `plan`, `AutorizacionPlan` es la
única vía que se mira — `AutorizacionCliente` "se conserva y no se mira" (DN-01, punto 4).

**Necesita.** `autorizacion.modo = plan` (lo fija el propio caso); el mismo `REMITENTE_DN01` de
C-09 (sigue teniendo su `AutorizacionCliente`, y sigue sin ninguna `AutorizacionPlan`); el adjunto
`E01-feliz-formato11.xlsx`.

**Cómo.** Con `autorizacion.modo` en `plan`, correo desde `REMITENTE_DN01` con
`E01-feliz-formato11.xlsx` adjunto.

**Qué tiene que pasar.**

| Qué | Valor |
|---|---|
| clasificación / procesar | `remitente_no_reconocido` / `false` |
| `REMITENTE_RECONOCIDO` | **NO CUMPLIDA** |
| Estado | **No reconocida** |
| Comunicación | **NINGUNA** |

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py C` (caso `C-10`).

**Cómo se sabe que falló.** Si este correo SÍ entra en modo `plan`, `AutorizacionCliente` está
funcionando como respaldo silencioso en modo `plan` — en contra del diseño explícito ("se
conservan y no se miran"). Comparado con C-09 (mismo correo, resultado opuesto), esto aísla que la
causa es el parámetro, no el dato.

**Automatizable.** API (`correr.py C`, caso `C-10`).

---

## C-11 · DN-01, valor basura en `autorizacion.modo`: degrada a `plan`, sin tumbar nada

**Qué prueba.** `ModoDeAutorizacionParametro.Resolver`: cualquier valor que no sea exactamente
`cliente` (sin distinguir mayúsculas) degrada al fallback quemado `plan`, **nunca lanza ni tumba la
validación** — mismo criterio que `servicio.cuenta` y `rpa.horas.tolerancia`.

**Necesita.** `autorizacion.modo = "clienteX"` (basura, lo fija el propio caso); `REMITENTE_DN01`
(autorizado solo por cliente, de C-09/C-10) y el `REMITENTE` de siempre (autorizado por plan); el
adjunto `E01-feliz-formato11.xlsx`.

**Cómo.**
1. Con `autorizacion.modo = "clienteX"`, correo desde `REMITENTE_DN01` (autorizado solo por
   cliente).
2. Con el mismo valor basura, correo desde `REMITENTE` (autorizado por plan, el camino de
   siempre).

**Qué tiene que pasar.**

| Correo | clasificación | Por qué |
|---|---|---|
| (a) `REMITENTE_DN01` (solo cliente) | `remitente_no_reconocido` | la basura NO se interpreta como `cliente`: se comporta como `plan`, y por `plan` este correo no tiene autorización |
| (b) `REMITENTE` (por plan) | `nuevo`, llega a **En proceso** | la operación **no se tumba**: ni excepción, ni HTTP roto, ni un falso "no reconocido" para el camino de siempre |

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py C` (caso `C-11`).

**Cómo se sabe que falló.** Dos causas con el mismo síntoma superficial ("algo salió mal con el
parámetro basura"): (a) si el correo (a) queda reconocido, el valor basura se está interpretando
como `cliente` — el fallback no es realmente quemado en `plan`; (b) si el correo (b) revienta con
una excepción, un HTTP distinto de 200/204, o queda "no reconocido" siendo el camino de siempre, el
valor basura SÍ está tumbando la operación normal — exactamente lo que el diseño prohíbe.

**Automatizable.** API (`correr.py C`, caso `C-11`).

---

## Fase 2 — investigación y conclusión

Se investigó qué de `diseno/DIRECTRICES-NUEVAS.md` (DN-01, DN-02, DN-03) agrega comportamiento
nuevo que se resuelva **a nivel Correo** (el nivel que cubre este set) y no a nivel Solicitud,
Registro o Fila.

### DN-01 SÍ aplica a este nivel — y ya está cubierto (C-09, C-10, C-11)

`Datos/Catalogos.cs` traza, con archivo y línea, que `PlanesAutorizadosDe` (el ÚNICO punto donde se
resuelve `autorizacion.modo`) tiene EXACTAMENTE dos llamadores:

```
ClasificarCorreo.cs:242   REMITENTE_RECONOCIDO   (nivel Correo — ESTE archivo)
ValidarSolicitud.cs:246   AUTORIZACION_CORREO_PLAN (nivel Registro — 03-reglas-de-registro.md)
```

El propio diseño dice *"una función, dos llamadores, ningún tercero"*, y remarca que **el modo
cambia TODO, no una parte**: el bug real que motivó la corrección del aprobador el 2026-09-26 fue
justamente que el nivel Correo se había quedado afuera del cambio. C-09/C-10/C-11 fijan exactamente
eso — con el mismo correo, cambiando solo el parámetro.

**Por qué no se agregó el resto de `pruebas/10-autorizacion-cliente.md` (letra `U`) acá.** Ese
archivo (todavía no migrado a `sets/`) ya tiene una cobertura mucho más amplia de DN-01: el nombre
calculado de `AutorizacionCliente` (U-07), la evidencia firmada opcional (U-10), las subgrillas de
la app (U-08/U-09), la clave alternativa (U-11), que el mensaje al cliente es idéntico en los dos
modos (U-06), y que cambiar de modo no borra nada (U-05) — todo eso es nivel Registro, tabla o app,
no nivel Correo. `CONVENCIONES.md` §4 fija un ID por archivo, sin colisiones: agregar esos casos acá
con prefijo `C` los duplicaría bajo dos letras distintas. Este archivo agrega SOLO lo que le
corresponde por nivel: que el "portero" también mire el modo. `U-02` (el caso que ya describe "los
DOS niveles, en la misma prueba") queda con la mitad de nivel Correo ahora reforzada acá, y la mitad
de nivel Registro sigue siendo trabajo de la letra `U` cuando se migre.

### DN-02 (exportar a Excel) — no aplica

Su Custom API (`sanic_mppp_capi_exportarparadigitar`) recibe `filaid`, no `solicitudid` ni nada de
nivel Correo; el parámetro nuevo (`exportacion.columnas`) describe columnas de **Fila**. No hay
ninguna superficie de este candidato en `ClasificarCorreo`.

### DN-03 (obligatoriedad por gestión) — no aplica

`plantilla.obligatoriedad` lo consulta la regla `OBLIGATORIEDAD`, de nivel **Registro** (evalúa
campo por campo de una Fila ya creada). Nivel Correo nunca llega a mirar el contenido del Excel —
eso es, por definición, lo que separa "Correo" de "Solicitud"/"Registro" en este diseño.

**Conclusión**: el set C queda con 8 casos de fase 1 (C-01..C-08, migrados sin reescribir la lógica
que ya estaba probada) más 3 casos nuevos de fase 2 (C-09..C-11, DN-01). DN-02 y DN-03 no tienen
lugar en este nivel.

---

## Funciones para consolidar

`sets/set_c.py::_preparar_autorizacion_por_cliente()` es local a este set (CONVENCIONES §8 permite
escribir local lo que `preparacion_comun.py` todavía no tiene, y reportarlo para que el coordinador
lo consolide). Candidata a subir a `preparacion_comun.py` si otro set necesita lo mismo: crea (o
reutiliza) un Autorizado con una `AutorizacionCliente` hacia un Cliente dado, sin ninguna
`AutorizacionPlan`. El caso más probable que la necesitaría es la migración futura de la letra `U`
(`10-autorizacion-cliente.md`), que hoy arma esto mismo a mano con `dataverse_api.py POST` según su
propio README.

---

## Resultados

Última corrida: **2026-09-27**, `python3 herramientas/pruebas/correr.py C` contra Dev. 5 OK · 4
FALLA · 2 N-A.

| Caso | Solicitud | Resultado | Notas |
|---|---|---|---|
| C-01 camino feliz | (borrada al cerrar) | **OK** | |
| C-02 respuesta no se procesa | (borrada al cerrar) | **FALLA** | `sanic_mppp_capi_clasificarcorreo` responde HTTP 400 antes de llegar a ningún `check()` propio — ver "Defecto encontrado" abajo |
| C-03 reenvío sí se procesa | (borrada al cerrar) | **OK** | |
| C-04 remitente no reconocido | (borrada al cerrar) | **FALLA** | mismo defecto que C-02 (mismo camino `!procesar`) |
| C-05 el correo se mueve | — | **N-A** | ver el caso arriba |
| C-06 el `.eml`/excel con nombre real | — | **N-A** | ver el caso arriba |
| C-07 no duplica | (borrada al cerrar) | **OK** | |
| C-08 sin adjunto | (borrada al cerrar) | **OK** | |
| C-09 DN-01 modo cliente entra | (borrada al cerrar) | **OK** | |
| C-10 DN-01 modo plan no entra (mismo correo) | (borrada al cerrar) | **FALLA** | mismo defecto (camino `!procesar`, `remitente_no_reconocido`) |
| C-11 DN-01 valor basura degrada | (borrada al cerrar) | **FALLA** | la parte (a) (`remitente_no_reconocido`) pega el mismo defecto ANTES de poder ejercitar la parte (b); la parte (b) (autorizado por plan, camino `nuevo`) no llegó a correr |

### Defecto encontrado (no es de este set, es de `ClasificarCorreo`)

Las cuatro FALLA de arriba **no son un problema de las reglas de nivel Correo ni de DN-01**: la
Custom API revienta ANTES de terminar, con un error genérico
(`"No se pudo clasificar el correo. El equipo tecnico ya tiene el detalle."`, HTTP 400) cada vez
que la clasificación NO es `nuevo`/`reenvio` (es decir, cada vez que `!procesar`, que es exactamente
cuando el diseño manda avisar a los ejecutivos en vez de comunicarse con el cliente).

**Causa raíz, confirmada en `plugintracelogs` (2026-09-27, reproducido 4/4 veces, aislado sin
ningún otro proceso corriendo):**

```
System.InvalidCastException: Unable to cast object of type 'System.Guid' to type 'Microsoft.Xrm.Sdk.EntityReference'.
   at Microsoft.Xrm.Sdk.Entity.GetAttributeValue[T](String attributeLogicalName)
   at Sanic.Mppp.Plugins.Datos.AvisosDataverse.<>c.<AvisarAEjecutivos>b__4_1(Entity e)
   ...
   at Sanic.Mppp.Plugins.Datos.AvisosDataverse.AvisarAEjecutivos(Guid solicitudId, String titulo, String cuerpo)
   at Sanic.Mppp.Plugins.Api.ClasificarCorreo.Ejecutar(Guid solicitudId, DateTime ahoraUtc)
```

`src/Sanic.Mppp.Plugins/Datos/Avisos.cs:106-111` consulta `systemuserroles` (la tabla nativa de
intersección N:N de roles) y hace `e.GetAttributeValue<EntityReference>("systemuserid")`. En una
entidad de intersección, Dataverse devuelve las columnas del vínculo como `Guid` **plano**, no como
`EntityReference` (a diferencia de un lookup en una tabla normal) — el cast revienta siempre que la
consulta trae al menos una fila, o sea, siempre que exista al menos un usuario con el rol
`SR - MPPP - Ejecutivo` asignado (que es el estado normal de Dev: las cuatro sesiones de
`CONVENCIONES.md` §6 ya existen).

**Alcance real, más allá de este set.** Como el plugin corre dentro de la transacción de la Custom
API, la excepción revierte TODO lo que `Ejecutar` ya había hecho (`GuardarResultados`, `Clasificar`,
el evento de Bitácora "Correo clasificado" — se confirmó con `leer_bitacora()` sobre una Solicitud
de diagnóstico: quedó vacía). En un entorno real, **cualquier correo real que sea una respuesta o
que venga de un remitente no reconocido queda atascado en Ingresada para siempre**, sin acuse (es
lo esperado, DF-08), pero TAMPOCO con el aviso interno a los ejecutivos que el diseño prevé como
compensación — falla exactamente el mecanismo que se supone que avisa que algo no se procesó.

**No se toca `src/` desde este set** (este agente es dueño de exactamente dos archivos). Queda
reportado acá con la evidencia completa para que quien mantiene `diseno/PENDIENTES.md` lo registre
con su propio ID. Arreglo sugerido, una vez que se decida: en
`src/Sanic.Mppp.Plugins/Datos/Avisos.cs:109`, `e.GetAttributeValue<Guid>(ColumnaIdUsuario)` en vez
de `e.GetAttributeValue<EntityReference>(ColumnaIdUsuario).Id`.
