# 12 — DN-04: carga masiva y activación masiva de catálogos por Excel, y Comentarios

Set **L** (`CONVENCIONES.md` §4: **L**ote — cargar y cambiar el estado de los catálogos *en lote*, desde
un Excel). Cubre los dos menús de las grillas de catálogos ("Carga masiva" y "Activar/Desactivar
masivo"), las plantillas, las dos Custom API (`sanic_mppp_capi_cargarcatalogos` y
`sanic_mppp_capi_cambiarestadocatalogos`), el catálogo de códigos de archivo `MPPP-CAR-001` a `011`,
las reglas por fila de los dos Excel, el todo o nada, la idempotencia, el dueño del cliente nuevo
(D-73) y la columna **Comentarios** de la autorización por cliente. Fuente de verdad del
comportamiento: `diseno/DIRECTRICES-NUEVAS.md`, sección DN-04 completa, y `diseno/PENDIENTES.md`,
filas D-73, D-74 y D-75. Código: `src/Sanic.Mppp.Plugins/Api/CatalogosMasivosApi.cs` y
`src/Sanic.Mppp.Plugins/Catalogos/`.

Convención de IDs: prefijo `L-`.

## Cómo leer el estado de cada caso

Cada caso de abajo es **el diseño escrito como prueba**. Su **estado** no está en el caso sino en la
tabla `## Resultados` del final, con la evidencia citada. Hay tres estados, y **no se mezclan**:

| Estado | Qué significa | Qué NO significa |
|---|---|---|
| **EJECUTADO — OK** | Se corrió contra Dev (o con el navegador real) y pasó. La evidencia está citada: qué se corrió, cuándo, con qué ensamblado | — |
| **CUBIERTO POR PRUEBAS AUTOMÁTICAS** | Lo ejercita la suite de C# (`tests/Sanic.Mppp.Plugins.Tests`, 1580 en verde al 2026-10-01), con el archivo de aceptación citado. **No se corrió en Dev** | **No es un OK.** `CONVENCIONES.md` §3 solo define OK / FALLA / N-A para una corrida real; este estado dice "hay una prueba que lo cubre", no "se vio funcionar en el entorno". Un doble de pruebas no reproduce la plataforma (D-73 lo dejó escrito) |
| **PENDIENTE** | Nadie lo corrió. Se corre en Dev | — |

**Todavía no existe `herramientas/pruebas/sets/set_l.py`**: `correr.py` no conoce el set `L`. Los casos
de abajo están escritos para que el set se pueda escribir sin adivinar; hasta entonces, lo que está
EJECUTADO salió del arnés de Playwright (`pruebas/gui/casos/dn-04-catalogos-masivos.spec.mjs`) y de
las mediciones con sesión real de D-73/D-75, y todo lo demás es manual o está pendiente.

## Preparativos comunes a este archivo

Este set es autocontenido (`CONVENCIONES.md` §8): no supone ningún cliente, plan, correo ni
autorización que haya dejado otro set. Lo que `set_l.py` tiene que dejar listo en su `preparar()`:

- **Sesiones y roles verificados, no supuestos**: `local/sesion-admin_planes.json` (`# User 3`,
  `SR - MPPP - Administrador de planes`), `local/sesion-ejecutivo.json` (`# User 1`,
  `SR - MPPP - Ejecutivo`), `local/sesion-supervisor.json` (`# User 2`) y
  `local/sesion-admin_tecnico.json` (`# User 4`), confirmadas con
  `python3 herramientas/pruebas/estado_sesiones.py` antes de correr un caso GUI y con
  `systemuserroles_association` para los casos de API (`CONVENCIONES.md` §6 y §8).
- **Quién llama la Custom API.** Los casos de API se corren **como el Administrador de planes**, no
  con el service principal: la Custom API escribe con el usuario que llama (DN-04, "Mecanismo") y
  tiene como puerta el privilegio `prvCreatesanic_mppp_tbl_cliente`, que el service principal no
  garantiza. O la sesión real de `# User 3`, o `MSCRMCallerID` de `# User 3` (mismo mecanismo que
  `comportamiento_roles.py` y `E-09`).
- **Parámetros globales** (`CONVENCIONES.md` §8.2): `carga.filas.maximo` en **100** (versión 2, activa,
  D-75) y `lectura.limites` vigente. El set los verifica al arrancar y no los toca, salvo el caso que
  siembra su propia versión de prueba (versión = máximo sembrado + 1, desactivada y luego borrada
  por GUID en `cerrar()`).
- **El ejecutivo dueño.** El correo de `# User 1` se **lee de `systemusers`** por alias, nunca se
  escribe a mano en el archivo. Un usuario sin el rol Ejecutivo: `# User 2` (Supervisor).
- **Los archivos.** Lo único que existe hoy en el repositorio son los dos del arnés GUI:
  `pruebas/gui/datos/dn04/dn04-carga-con-error.xlsx` y `dn04-carga-hoja-equivocada.xlsx`. Los demás se
  arman al escribir `set_l.py` (hoja `CargaCatalogos`, 12 columnas A a L; hoja `ActivarDesactivar`,
  7 columnas A a G; encabezados en la fila 1, datos desde la fila 2).
- **Marca de agua y limpieza** (`CONVENCIONES.md` §5): todo lo que cree un caso lleva `ZZP-` en la razón
  social del cliente y en el nombre del contacto del correo; los CIF y los códigos de plan se
  eligen en rangos reservados del set. La limpieza es por GUID, en orden inverso a la creación
  (autorizaciones → planes → correos → clientes), releyendo la marca antes de cada `DELETE` con
  `marca_agua.borrar_marcado()`. **Un caso que carga por la Custom API no deja nada sin borrar.**
- **Cómo se lee un `resultado`**: la respuesta de las dos Custom API trae `resultado` en base64; se
  decodifica a `.xlsx` y se lee la columna **Resultado** (**L** en `CargaCatalogos`, **G** en
  `ActivarDesactivar`) de la fila que el caso nombra, con `openpyxl`. El número de fila del archivo de
  resultado es el mismo que el del archivo subido.
- **Tablas, para las verificaciones**: `sanic_mppp_tbl_clientes` (`sanic_cifbac`, `sanic_cifcom`,
  `sanic_nombre` = razón social, `_ownerid_value`, `_createdby_value`, `statecode`),
  `sanic_mppp_tbl_plans` (`sanic_codigo`, `sanic_tipoformato`, `sanic_moneda`, `_sanic_clienteid_value`),
  `sanic_mppp_tbl_autorizados` (`sanic_nombre` = el correo, `sanic_nombrecontacto`) y
  `sanic_mppp_tbl_autorizacionclientes` (`_sanic_clienteid_value`, `_sanic_autorizadoid_value`,
  `sanic_comentarios`, `sanic_documentofirmado`, `sanic_fechadocumento`).

---

## Grupo 1 — Menús, plantillas y flujo del navegador

### L-01 — El Administrador de planes ve los dos menús, con sus dos opciones, en las cuatro grillas

**Qué prueba.** DN-04, "Interfaz": "Carga masiva" y "Activar/Desactivar masivo" son menús
desplegables (`FlyoutAnchor`) que viven en la grilla principal de Clientes, Planes, Correos
Autorizados y Autorizaciones - Clientes, y cada uno trae *Cargar archivo* y *Descargar plantilla*.

**Necesita.** `local/sesion-admin_planes.json` (`# User 3`, `SR - MPPP - Administrador de planes`),
viva y con el rol correcto según `estado_sesiones.py`. No necesita datos: las grillas pueden estar
vacías.

**Cómo.** Desde `pruebas/gui/`: `MPPP_USUARIO=admin_planes npx playwright test dn-04`. Por cada una de
las cuatro grillas (entradas de sitemap "Clientes", "Planes", "Correos Autorizados" y
"Autorizaciones - Clientes", área Catálogos): abrir la grilla, abrir cada menú y confirmar que
trae las dos opciones.

**Qué tiene que pasar.** En cada grilla, el menú "Carga masiva" y el menú "Activar/Desactivar masivo"
están presentes; cada uno, al abrirse, lista "Cargar archivo" y "Descargar plantilla".

**Cómo se verifica.** El resultado de `MPPP_USUARIO=admin_planes npx playwright test dn-04`: las
cuatro pruebas `<grilla>: los dos menús están y cada uno trae sus dos opciones`.

**Cómo se sabe que falló.** Dos causas opuestas con el mismo síntoma (`CONVENCIONES.md` §1, la lección
del botón fantasma, `05-app-y-comandos.md` A-15). **Si el ancla falta de la barra**: metadata
compilada del ribbon sin regenerar (se confirma con el Command Checker, `&ribbondebug=true`) — no es
un defecto de definición. **Si el ancla está pero la regla la oculta**: `esAdministradorDePlanes` dio
`false`, y lo primero es `estado_sesiones.py` (¿la sesión sigue siendo `# User 3` con su rol?). Si el
menú se ve pero dice "LabelText", las `$LocLabels` volvieron a un `FlyoutAnchor` y el texto fijo se
perdió (D-74).

**Automatizable.** GUI — sesión `admin_planes`. Ya construido en
`pruebas/gui/casos/dn-04-catalogos-masivos.spec.mjs`.

---

### L-02 — El Ejecutivo no ve ninguno de los dos menús, en ninguna de las cuatro grillas

**Qué prueba.** DN-04, "Interfaz": los menús los ve **solo** el Administrador de planes; el Ejecutivo
solo lee catálogos. La regla es `Sanic.Mppp.Comandos.esAdministradorDePlanes`.

**Necesita.** `local/sesion-ejecutivo.json` (`# User 1`, `SR - MPPP - Ejecutivo`), viva y con el rol
correcto. Esto es seguridad: **una prueba que pasa porque el usuario no ve algo es indistinguible de
una que pasa porque la sesión está muerta** (`CONVENCIONES.md` §6). Por eso la guarda previa
`estado_sesiones.py` es obligatoria, y el caso se acompaña de L-01, que demuestra que en la misma
pantalla el Administrador de planes SÍ los ve.

**Cómo.** Desde `pruebas/gui/`: `MPPP_USUARIO=ejecutivo npx playwright test dn-04`. En cada una de las
cuatro grillas, buscar los dos menús en la barra de la página.

**Qué tiene que pasar.** Ni "Carga masiva" ni "Activar/Desactivar masivo" aparecen en ninguna de las
cuatro grillas.

**Cómo se verifica.** El resultado de `MPPP_USUARIO=ejecutivo npx playwright test dn-04`: las cuatro
pruebas `<grilla>: un Ejecutivo no ve los menús de carga ni de activación masiva`.

**Cómo se sabe que falló.** Si el Ejecutivo ve un menú: `esAdministradorDePlanes` devolvió `true` para
un usuario que no tiene el rol exacto `SR - MPPP - Administrador de planes` (revisar
`Xrm.Utility.getGlobalContext().userSettings.roles` en la consola). Aunque eso pase, la puerta real
es el privilegio de la Custom API (L-51): el menú visible no le daría acceso, pero es un defecto de
interfaz. Si la prueba pasa pero la sesión estaba vencida: es un falso verde; por eso la guarda.

**Automatizable.** GUI — sesión `ejecutivo`. Ya construido en
`pruebas/gui/casos/dn-04-catalogos-masivos.spec.mjs`.

---

### L-03 — El Supervisor y el Administrador técnico tampoco ven los menús

**Qué prueba.** El mismo diseño que L-02, para los otros dos roles que no administran catálogos. La
regla dice que solo el rol exacto `SR - MPPP - Administrador de planes` los ve; el set del arnés GUI
solo cubre al Ejecutivo.

**Necesita.** `local/sesion-supervisor.json` (`# User 2`, `SR - MPPP - Supervisor`) y
`local/sesion-admin_tecnico.json` (`# User 4`, `SR - MPPP - Administrador tecnico`), vivas y con su
rol. El Administrador técnico **no tiene ningún privilegio sobre las cinco tablas de catálogo** (`diseno/04`,
matriz: "—" en Cliente, Plan, Autorizado, AutorizacionPlan y AutorizacionCliente) y su sitemap no tiene
el área Catalogos: el caso se resuelve por la ausencia de las grillas, y se anota cuáles no se pudieron
abrir en vez de darlas por verdes. *(Corregido el 2026-10-02: decía solo "no ve Clientes ni
Autorizados"; la corrida mostró que no ve ninguna, y es lo que dice la matriz.)*

**Cómo.** Con cada sesión, abrir cada grilla de catálogo que el rol pueda abrir y buscar los dos menús
(mismos selectores que `dn-04-catalogos-masivos.spec.mjs`, que hoy filtra por `MPPP_USUARIO`).

**Qué tiene que pasar.** Ningún menú en ninguna grilla que el rol pueda abrir.

**Cómo se verifica.** Una corrida del mismo spec con `MPPP_USUARIO=supervisor` y
`MPPP_USUARIO=admin_tecnico` una vez que el spec incluya esos dos roles (hoy `test.skip` los saltea a
los dos: el arnés no los cubre).

**Cómo se sabe que falló.** Un menú visible = `esAdministradorDePlanes` mal escrita. Una grilla que no
abre = es el recorte de sitemap del rol (`G-07`), no un defecto de este caso: se anota, no se da por
verde.

**Automatizable.** GUI — sesiones `supervisor` y `admin_tecnico`. Falta extender el spec.

---

### L-04 — Los menús llevan texto fijo en español, también con la interfaz en inglés

**Qué prueba.** DN-04, "Los menús llevan texto fijo en español" (decisión del aprobador,
2026-10-01; excepción declarada a la regla de multiidioma solo para menús desplegables; D-74 sigue
abierta para ver si `$LocLabels` funciona en un `FlyoutAnchor`). El texto está en `LabelText`, no
depende de los idiomas instalados.

**Necesita.** `local/sesion-admin_planes.json` con el idioma de la interfaz del usuario en inglés (1033)
y una segunda corrida en español (3082). Cambiar el idioma de `# User 3` es tocar la configuración
de un usuario de prueba: el set lo lleva a español al terminar (canon, no restauración).

**Cómo.**
1. Con la interfaz en español, abrir la grilla Clientes y leer el texto del ancla de cada menú y de
   sus dos opciones.
2. Repetir con la interfaz en inglés.

**Qué tiene que pasar.** En los dos idiomas el texto es exactamente "Carga masiva",
"Activar/Desactivar masivo", "Cargar archivo" y "Descargar plantilla". **Los botones simples de la
barra, en cambio, sí cambian de idioma** (siguen con `$LocLabels`, `A-13`).

**Cómo se verifica.** El texto de los elementos del menú en el DOM, en cada idioma. El spec de hoy
(`dn-04-catalogos-masivos.spec.mjs`) acepta **las dos** formas ("Carga masiva" o "Bulk load"): no
sirve para este caso, porque un menú localizado también lo pasaría. Hay que fijar el texto en español.

**Cómo se sabe que falló.** El menú dice "LabelText": las etiquetas localizadas volvieron al
`FlyoutAnchor` y la metadata no las manda (el síntoma original de D-74). Dice "Bulk load" en inglés:
alguien volvió a `$LocLabels` sin cerrar D-74, y este caso tiene que actualizarse junto con la
decisión, no ignorarse.

**Automatizable.** GUI — sesión `admin_planes`, en dos idiomas. Falta un caso del arnés que fije el
texto en español (el spec actual tolera inglés).

---

### L-05 — "Descargar plantilla" de Carga masiva baja `CargaCatalogos.xlsx` vacía, con sus encabezados

**Qué prueba.** DN-04, "Interfaz" y "Abierto B": la opción llama a la Custom API con
`plantilla = true` y descarga la plantilla vacía, con el indicador "Generando la plantilla…". La
plantilla sale de la **misma definición de columnas que valida el archivo**.

**Necesita.** `local/sesion-admin_planes.json`, viva y con el rol correcto.

**Cómo.** `MPPP_USUARIO=admin_planes npx playwright test dn-04`: en la grilla Clientes, "Carga masiva"
→ "Descargar plantilla", capturar la descarga y leer el `.xlsx`.

**Qué tiene que pasar.** El archivo se llama `CargaCatalogos.xlsx`; la hoja `CargaCatalogos` trae en la
fila 1, en este orden: CIF BAC, CIF COM, Razon social, Correo ejecutivo, Codigo plan, Tipo formato,
Moneda, Correo, Nombre contacto, Fecha documento, Comentarios, **Resultado** (columna L); no trae
ninguna fila de datos.

**Cómo se verifica.** La prueba `'Carga masiva' → 'Descargar plantilla' baja la plantilla vacía con sus
encabezados` del spec (`suggestedFilename()`, encabezados contra la lista de 12, filas vacías).

**Cómo se sabe que falló.** No hay descarga: el indicador quedó abierto o la Custom API falló con
`plantilla = true` (se ve el código `MPPP-CAR-0xx` en el diálogo). Encabezados distintos: la
plantilla y el validador dejaron de compartir definición, y eso rompe la garantía de "Abierto B".

**Automatizable.** GUI — sesión `admin_planes`. Ya construido en
`pruebas/gui/casos/dn-04-catalogos-masivos.spec.mjs`.

---

### L-06 — "Descargar plantilla" de Activar/Desactivar baja `ActivarDesactivar.xlsx` vacía, con sus encabezados

**Qué prueba.** Lo mismo que L-05 para la segunda Custom API
(`sanic_mppp_capi_cambiarestadocatalogos`, `plantilla = true`).

**Necesita.** `local/sesion-admin_planes.json`, viva y con el rol correcto.

**Cómo.** `MPPP_USUARIO=admin_planes npx playwright test dn-04`: en la grilla Clientes,
"Activar/Desactivar masivo" → "Descargar plantilla".

**Qué tiene que pasar.** El archivo se llama `ActivarDesactivar.xlsx`; la hoja `ActivarDesactivar`
trae en la fila 1: Tipo, Accion, CIF BAC, CIF COM, Codigo plan, Correo, **Resultado** (columna G);
sin filas de datos.

**Cómo se verifica.** La prueba `'Activar/Desactivar masivo' → 'Descargar plantilla' baja la plantilla
vacía con sus encabezados` del spec.

**Cómo se sabe que falló.** Igual que L-05. Además: si baja `CargaCatalogos.xlsx` desde este menú, los
dos comandos del ribbon quedaron apuntando a la misma función del JS.

**Automatizable.** GUI — sesión `admin_planes`. Ya construido en
`pruebas/gui/casos/dn-04-catalogos-masivos.spec.mjs`.

---

### L-07 — Plantilla, archivo de resultado y archivo corregido tienen la misma forma: se baja, se corrige y se vuelve a subir tal cual

**Qué prueba.** DN-04, "Excel de resultado" (precisado el 2026-10-01): la columna **Resultado** es parte
de la plantilla (L en `CargaCatalogos`, G en `ActivarDesactivar`), se exige como encabezado y **nunca
se lee**. Sin esto, el diálogo diría "corregí y volvé a subirlo" y el modo estricto rechazaría el
archivo corregido por traer una columna de más. También: una plantilla subida vacía falla solo por
`MPPP-CAR-007` (sin filas), nunca por encabezados ("Abierto B").

**Necesita.** Un archivo de carga con un error de fila (por ejemplo, la fila 2 sin CIF) y su plantilla
vacía. Sesión de Administrador de planes.

**Cómo.**
1. Subir la plantilla vacía de `CargaCatalogos` tal cual se bajó.
2. Subir el archivo con el error; guardar el `resultado`.
3. Sin tocar la columna Resultado, corregir la fila y volver a subir **el archivo de resultado**
   corregido (con la columna L llena de texto viejo).
4. Repetir el paso 1 y el 3 con `ActivarDesactivar` (columna G).

**Qué tiene que pasar.** Paso 1: rechazo `MPPP-CAR-007`, nunca `MPPP-CAR-005`. Paso 3: el archivo
corregido se lee (el contenido de la columna Resultado se ignora) y la carga sigue su curso (se
graba, o da errores de fila reales, nunca un rechazo de encabezados). Paso 4: lo mismo con la
columna G.

**Cómo se verifica.** El código del rechazo en la respuesta (`MPPP-CAR-007`), y que `grabado` sea `true`
en el paso 3 si el archivo corregido ya no tiene errores.

**Cómo se sabe que falló.** `MPPP-CAR-005` en el paso 1 o 3: la columna Resultado dejó de ser parte de
la plantilla. Un error de fila que cita el contenido viejo de la columna Resultado: el lector la está
leyendo.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`tests/Sanic.Mppp.Plugins.Tests/Aceptacion/ExcelDeCatalogosAceptacion.cs`:
`La_plantilla_vacia_trae_solo_los_encabezados_y_el_lector_estricto_la_acepta_sin_filas`,
`El_resultado_va_en_la_misma_fila_con_los_valores_leidos_y_se_puede_volver_a_subir`,
`El_resultado_del_cambio_de_estado_tambien_se_puede_volver_a_subir`).

---

### L-08 — Cancelar el selector no hace nada; un archivo que no es `.xlsx` avisa sin llamar a la API

**Qué prueba.** Contrato del JS (`cargarCatalogos` / `cambiarEstadoCatalogos`): el selector de archivo
se abre **sincrónicamente dentro del clic** (si no, el navegador lo bloquea); cancelar no hace nada; un
archivo que no termina en `.xlsx` da un aviso y no llama a la Custom API.

**Necesita.** `local/sesion-admin_planes.json`; un archivo `.txt` cualquiera.

**Cómo.**
1. En Clientes, "Carga masiva" → "Cargar archivo"; cuando se abre el selector, cancelarlo.
2. Repetir con "Activar/Desactivar masivo".
3. Repetir "Cargar archivo" y elegir el `.txt`.

**Qué tiene que pasar.** Paso 1 y 2: no aparece indicador de progreso, no hay llamada de red a ninguna
Custom API, la grilla queda como estaba. Paso 3: un aviso dice que el archivo tiene que ser `.xlsx`;
no hay llamada a la Custom API.

**Cómo se verifica.** Con Playwright (`page.on("request")`, mismo mecanismo de evidencia de red que
`a-04-cancelar-no-cambia-nada.spec.mjs`): cero peticiones a `sanic_mppp_capi_cargarcatalogos` y
`sanic_mppp_capi_cambiarestadocatalogos` en los tres pasos.

**Cómo se sabe que falló.** Una petición a la Custom API con un `.txt`: el JS no valida la extensión y
el usuario recibiría `MPPP-CAR-002`, un mensaje que no le dice "elegí un Excel". El selector no
abre: se perdió el gesto del usuario (la apertura dejó de ser sincrónica con el clic).

**Automatizable.** GUI — sesión `admin_planes`. Falta el caso del arnés.

---

### L-09 — El indicador por etapas, el resumen de una carga grabada y el refresco de la grilla

**Qué prueba.** DN-04, "Flujo del JS": tres mensajes de progreso ("Leyendo el archivo…", "Validando y
cargando el archivo… puede tardar hasta un minuto", "Preparando el resultado…"), la descarga de
`<nombre original sin extensión>-resultado.xlsx`, el diálogo de resumen y el refresco de la grilla.
El indicador es modal: mientras está abierto no se puede volver a hacer clic.

> **Evidencia parcial (2026-10-01).** El mensaje del medio ya lo comprueba el caso de Playwright de la carga con error de
> fila (`pruebas/gui/casos/dn-04-catalogos-masivos.spec.mjs`, dos corridas en verde): el indicador muestra "puede tardar
> hasta un minuto" (antes decía "hasta 2 minutos"; se corrigió por D-75). El resto del caso sigue pendiente.

**Necesita.** `local/sesion-admin_planes.json`; un archivo de carga **sin errores** que cree un cliente
(con su ejecutivo `# User 1`), un plan, un correo y su autorización, con una advertencia buscada (un
cliente existente inactivo, o bien dos filas que repiten una acción en el caso del cambio de
estado); la grilla Clientes abierta **antes** de cargar.

**Cómo.**
1. En Clientes, "Carga masiva" → "Cargar archivo", elegir el archivo, y mirar el indicador durante la
   llamada. Intentar hacer clic en otro lado de la pantalla.
2. Al terminar, leer el diálogo y la descarga.
3. Cerrar el diálogo y mirar la grilla.
4. Repetir con un archivo de Activar/Desactivar sin errores.

**Qué tiene que pasar.** El JS **pide** los tres mensajes en ese orden (espía sobre
`Xrm.Utility.showProgressIndicator`) y el de la llamada a la API ("Validando…") **llega a verse**. *(Corregido
el 2026-10-02: decía "los tres aparecen". La plataforma pinta el indicador ~400 ms después de pedirlo, así que
"Leyendo" y "Preparando", que duran milisegundos, se cierran antes de verse: exigirlos en pantalla era pedir
algo no observable. El defecto era del caso, no del producto.)* El indicador bloquea la pantalla; se
descarga `<nombre>-resultado.xlsx`; el diálogo dice "Carga terminada: 1 clientes, 1 planes, 1 correos
y 1 autorizaciones creados." (en 3082; en 1033 "Load finished: ... created."), más
" N filas con advertencias: revisá el archivo de resultado." si `advertencias` > 0; el cliente nuevo
aparece en la grilla sin recargar a mano. Con Activar/Desactivar: "Cambio de estado terminado: N
activados, N desactivados y N sin cambios.", más el mismo sufijo de advertencias si las hay (desactivar un
cliente con autorizaciones activas siempre advierte, regla 6).

**Cómo se verifica.** La captura del diálogo y de la descarga con Playwright, y `GET
sanic_mppp_tbl_clientes?$filter=sanic_cifbac eq '<CIF del archivo>'` para el cliente creado.

**Cómo se sabe que falló.** La grilla no refresca: el JS no llama al refresco. El diálogo dice algo
distinto del texto del contrato: `TEXTOS` quedó desalineado (ese mensaje lo lee el Administrador de
planes). Si no se descarga el resultado pero el diálogo sí aparece: falló `Xrm.Navigation.openFile`.

**Automatizable.** GUI — sesión `admin_planes`. El spec de hoy cubre solo los dos desenlaces de error
(L-13 y L-43); falta el desenlace grabado.

---

## Grupo 2 — Los once códigos de archivo `MPPP-CAR-001` a `011`

Un error de **archivo** (no de fila) hace que la llamada falle con un código y un texto propio, y **no
devuelve Excel de resultado**: no hay filas confiables a las que asignarle un motivo. Los once códigos
son los de las **dos** Custom API; los casos de abajo los disparan sobre `cargarcatalogos` y repiten el
disparo sobre `cambiarestadocatalogos` cuando el código depende de la hoja.

### L-10 — `MPPP-CAR-001`: archivo vacío o que no es base64

**Qué prueba.** DN-04, tabla de códigos: `archivo` vacío o que no es base64 válido es `MPPP-CAR-001`.

**Necesita.** Sesión (o `MSCRMCallerID`) del Administrador de planes.

**Cómo.**
1. `dataverse_api.py POST sanic_mppp_capi_cargarcatalogos '{"archivo":""}'`.
2. Lo mismo con `{"archivo":"esto no es base64 !!"}`.
3. Lo mismo contra `sanic_mppp_capi_cambiarestadocatalogos`.

**Qué tiene que pasar.** Las tres llamadas se rechazan con un mensaje que empieza con `MPPP-CAR-001` y
pide elegir un Excel (`.xlsx`); ninguna devuelve `resultado`.

**Cómo se verifica.** El mensaje de error de cada llamada.

**Cómo se sabe que falló.** Un error genérico de la plataforma en vez del código: el plugin no
atrapó el base64 inválido. `MPPP-CAR-010` en vez de `001`: se mandó también `plantilla = true`
(no es este caso).

**Automatizable.** API — cubierto por pruebas automáticas C#
(`tests/Sanic.Mppp.Plugins.Tests/Aceptacion/CatalogosMasivosApiAceptacion.cs`:
`Archivo_vacio_o_base64_invalido_es_MPPP_CAR_001`).

---

### L-11 — `MPPP-CAR-002`: no es un `.xlsx` legible

**Qué prueba.** Un archivo que no se puede leer como Excel (dañado, o de otro formato) da
`MPPP-CAR-002`, y **el mensaje de la librería de lectura nunca llega al usuario** (LP-01): va a la
traza.

**Necesita.** Sesión del Administrador de planes; cuatro bytes cualesquiera en base64, y un `.xlsx`
cortado a la mitad.

**Cómo.**
1. Llamar a `cargarcatalogos` con los cuatro bytes en base64.
2. Llamar con el `.xlsx` truncado.

**Qué tiene que pasar.** Rechazo `MPPP-CAR-002` con un texto en español que manda a bajar la plantilla;
sin jerga de la librería ni nombres de clases.

**Cómo se verifica.** El mensaje de error; y que no contenga texto de la librería (`OpenXml`,
`System.`, un rastro de pila).

**Cómo se sabe que falló.** El usuario recibe un texto técnico: la excepción de la librería se mostró
en lugar de ir a la traza (violación de LP-01).

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CatalogosMasivosApiAceptacion.cs`: `Los_rechazos_del_lector_se_traducen_a_su_codigo_sin_escribir_nada`).

---

### L-12 — `MPPP-CAR-003`: el archivo supera el peso de `lectura.limites`

**Qué prueba.** El tope de bytes (comprimidos y descomprimidos) de `lectura.limites` se aplica, y el
peso del texto base64 se mira **antes de decodificar**: un texto enorme no se convierte en bytes para
después rechazarlo.

**Necesita.** `lectura.limites` vigente (se lee, no se asume); un archivo que lo supere (para el tope de
entrada) y uno chico comprimido que se expanda por encima del tope descomprimido.

**Cómo.** Llamar a `cargarcatalogos` con cada uno de los dos archivos.

**Qué tiene que pasar.** Rechazo `MPPP-CAR-003` que pide quitar filas o partir el archivo.

**Cómo se verifica.** El mensaje de error y el tiempo de la llamada (el rechazo por texto enorme no
tiene que tardar: no decodificó).

**Cómo se sabe que falló.** La llamada tarda mucho antes de rechazar (decodificó el texto entero) o
revienta con un error de memoria de la plataforma.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CatalogosMasivosApiAceptacion.cs`: `Un_archivo_que_supera_lectura_limites_es_MPPP_CAR_003`,
`El_tope_de_peso_se_mira_antes_de_decodificar_el_base64`).

---

### L-13 — `MPPP-CAR-004`: no existe la hoja esperada, y no se descarga nada

**Qué prueba.** Un archivo sin la hoja `CargaCatalogos` (o `ActivarDesactivar`) da `MPPP-CAR-004`, nombra
la hoja esperada y **no descarga ningún Excel de resultado**. Subir a un botón el archivo del otro (el
de `ActivarDesactivar` en Carga masiva) cae en este caso.

**Necesita.** `local/sesion-admin_planes.json`; `pruebas/gui/datos/dn04/dn04-carga-hoja-equivocada.xlsx`.

**Cómo.** `MPPP_USUARIO=admin_planes npx playwright test dn-04`: en Clientes, "Carga masiva" →
"Cargar archivo", elegir `dn04-carga-hoja-equivocada.xlsx`, escuchar el evento `download`.

**Qué tiene que pasar.** El diálogo muestra el texto con `MPPP-CAR-004`; no hubo ninguna descarga.

**Cómo se verifica.** La prueba `'Cargar archivo' con la hoja equivocada muestra MPPP-CAR-004 y no
descarga nada` del spec (`texto` contiene `MPPP-CAR-004`, `huboDescarga === false`).

**Cómo se sabe que falló.** Se descarga un resultado: el plugin trató un archivo sin hoja como un archivo
con filas. Aparece `MPPP-CAR-002` en lugar de `004`: el lector clasificó "no hay hoja" como "archivo
ilegible".

**Automatizable.** GUI — sesión `admin_planes`. Ya construido en
`pruebas/gui/casos/dn-04-catalogos-masivos.spec.mjs`; además lo cubre
`CatalogosMasivosApiAceptacion.cs` (`Los_rechazos_del_lector_se_traducen_a_su_codigo_sin_escribir_nada`).

---

### L-14 — `MPPP-CAR-005`: encabezados que faltan, sobran o están fuera de orden (los nombra)

**Qué prueba.** Los encabezados se comparan en la fila 1, en el orden exacto de la tabla del diseño,
sin distinguir mayúsculas, tildes ni espacios a los lados. Si falta uno, sobra un encabezado no
vacío después de la última columna, o hay uno fuera de orden, se rechaza el archivo y el mensaje
nombra **cuáles**.

**Necesita.** Sesión del Administrador de planes; la plantilla vacía de `CargaCatalogos` en cuatro
variantes: (a) un encabezado con otro nombre ("Divisa" en vez de "Moneda"), (b) una columna `Extra` al
final, (c) dos columnas intercambiadas, (d) los mismos encabezados con otras mayúsculas y un espacio
al final (no debe ser rechazo).

**Cómo.** Llamar a `cargarcatalogos` con cada variante, con **una fila de datos** para que no caiga en
`MPPP-CAR-007`.

**Qué tiene que pasar.** (a), (b) y (c): `MPPP-CAR-005` nombrando los encabezados que no
coinciden. (d): no hay rechazo de encabezados.

**Cómo se verifica.** El mensaje de error de cada llamada.

**Cómo se sabe que falló.** (c) pasa sin rechazo: el lector está comparando por nombre y no por
posición, y una carga con las columnas cruzadas grabaría datos en el campo equivocado. (d) se
rechaza: la comparación distingue mayúsculas o espacios.

**Automatizable.** API — cubierto por pruebas automáticas C# en (a) y (b)
(`CatalogosMasivosApiAceptacion.cs`: `Los_rechazos_del_lector_se_traducen_a_su_codigo_sin_escribir_nada`).
**(c) y (d) no tienen una prueba de aceptación propia nombrada** en esa clase; se corren en Dev
la primera vez.

---

### L-15 — `MPPP-CAR-006`: más de 100 filas, rechazado entero y sin escribir

**Qué prueba.** DN-04, "Las filas de más se rechazan, no se cortan": con más filas no vacías que
`carga.filas.maximo` (100, D-75) se rechaza **el archivo entero**; no se carga la ventana. El mensaje
dice cuál es el máximo, **no cuántas filas trae** (el lector corta en la primera de más para no
recorrer un archivo hostil).

**Necesita.** Sesión del Administrador de planes; `carga.filas.maximo` en 100 verificado; un archivo de
**101 filas** no vacías (cada una un cliente distinto, rangos de CIF reservados del set); el
conteo previo de clientes de ese rango (cero).

**Cómo.** Llamar a `cargarcatalogos` con el archivo de 101 filas. Después contar los clientes del rango.

**Qué tiene que pasar.** Rechazo `MPPP-CAR-006` que dice que el máximo es 100 y pide partir el archivo;
cero clientes nuevos en el rango; ningún `resultado`.

**Cómo se verifica.** El mensaje de error; `dataverse_api.py GET
"sanic_mppp_tbl_clientes?$filter=startswith(sanic_cifbac,'<prefijo del set>')&$count=true"`.

**Cómo se sabe que falló.** Se cargan las primeras 100 (el archivo se cortó en vez de rechazarse: es
exactamente lo que DN-04 prohíbe). El mensaje dice "101": el lector recorrió todo el archivo. Con 100
filas exactas, la llamada tiene que **aceptarlo** (ver L-48).

**Automatizable.** API — sesión real del Administrador de planes. También cubierto por pruebas
automáticas C# (`CatalogosMasivosApiAceptacion.cs`:
`Los_rechazos_del_lector_se_traducen_a_su_codigo_sin_escribir_nada`).

---

### L-16 — `MPPP-CAR-007`: ninguna fila de datos

**Qué prueba.** Un archivo con hoja y encabezados correctos pero sin ninguna fila de datos (la plantilla
vacía) da `MPPP-CAR-007`, nunca un rechazo de encabezados.

**Necesita.** Sesión del Administrador de planes; la plantilla vacía; un archivo con solo filas
totalmente vacías (se saltean).

**Cómo.** Llamar a las dos Custom API con la plantilla vacía correspondiente y con el archivo de filas
vacías.

**Qué tiene que pasar.** Rechazo `MPPP-CAR-007` que pide completar al menos una fila.

**Cómo se verifica.** El mensaje de error.

**Cómo se sabe que falló.** `MPPP-CAR-005`: la plantilla generada por la propia Custom API no pasa su
propio validador de encabezados (se rompió "Abierto B").

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CatalogosMasivosApiAceptacion.cs`: `Los_rechazos_del_lector_se_traducen_a_su_codigo_sin_escribir_nada`).

---

### L-17 — `MPPP-CAR-008`: se agotó el presupuesto de 30 s; no se grabó nada

**Qué prueba.** El presupuesto de escritura es de **30 s** (bajado de 100 s el 2026-10-01, D-75: en Dev la
base corta una transacción a los ~40 s). Al alcanzarlo, la Custom API corta con una excepción
(que deshace lo grabado) y un mensaje que pide partir el archivo.

**Necesita.** Un volumen que tarde más de 30 s en escribir. **Con el tope de 100 filas, el peor caso
medido tarda 25,1 s (D-75): hoy no hay un archivo permitido que dispare este caso en Dev sin ayuda.**
Para dispararlo de verdad hay que sembrar una versión de prueba de `carga.filas.maximo` más alta
(máximo sembrado + 1, desactivada y borrada después, `CONVENCIONES.md` §8.2) y subir 130 a 150 filas
peor caso, y a esos volúmenes la base ya cortó a los ~40 s (D-75: 150 filas = 39,0 s): el
mismo riesgo y la misma regla que `E-08` — **no se corre de rutina**.

**Cómo (solo si se decide la confirmación de punta a punta).** Con el parámetro de prueba sembrado,
subir 140 filas peor caso (cada una crea cliente, plan, correo y autorización) y mirar la respuesta.

**Qué tiene que pasar.** Rechazo `MPPP-CAR-008` que dice "30 segundos", "No se grabó nada" y pide partir
el archivo; cero registros nuevos.

**Cómo se verifica.** El mensaje de error y el conteo de los clientes del rango (cero).

**Cómo se sabe que falló.** Aparece `MPPP-CAR-011` o un error de plataforma (código 10928) en lugar
de `008`: la base cortó antes que el presupuesto propio, que es justo lo que D-75 quiso evitar bajando
a 30 s.

**Automatizable.** API en `set_l.py`, **siempre N-A sin bandera de alto riesgo** (mismo criterio que
`E-07`/`E-08`); la cobertura confiable y repetible es la automática C#
(`CatalogosMasivosApiAceptacion.cs`: `Presupuesto_agotado_es_MPPP_CAR_008`;
`CatalogosMasivosAceptacion.cs`: `Con_el_presupuesto_agotado_no_escribe_mas_y_dice_cuantas_alcanzo`,
`CatalogosMasivosApiAceptacion.cs`: `Si_no_hay_nada_que_escribir_el_presupuesto_no_tira_el_resultado`).

---

### L-18 — `MPPP-CAR-009`: falla un error de dato al grabar (pasada 2), nombra la fila y no graba nada

**Qué prueba.** Si una escritura de la pasada 2 falla por un **dato** (por ejemplo, alguien creó el
mismo CIF entre las dos pasadas), se atrapa solo para envolverla en `MPPP-CAR-009` con la fila y el
motivo de Dataverse (recortado a ~300 caracteres), y se relanza **sin ninguna otra llamada al
servicio**: la transacción deshace todo.

**Necesita.** Un archivo válido de 2 filas (cliente nuevo cada una) y la forma de colisionar una
clave **entre las dos pasadas**: crear con otro usuario el CIF de la fila 3 mientras corre la pasada 1.
Eso es una carrera y no se puede forzar de manera determinista contra Dev desde afuera.

**Cómo.** No hay forma segura de forzar el cambio entre las dos pasadas en Dev. En Dev, la variante
determinista es una violación de clave alternativa que la pasada 1 no ve (por ejemplo, un correo ya
existente con otras mayúsculas que Dataverse normaliza distinto): solo si aparece un caso real se
corre y se anota. El resto de la cobertura es la suite de C# con un servicio que falla a propósito.

**Qué tiene que pasar.** Rechazo `MPPP-CAR-009`: "No se pudo grabar la fila N: <motivo>. No se grabó
nada. Corregí esa fila y volvé a subir el archivo."; cero registros nuevos, incluidos los de las
filas anteriores.

**Cómo se verifica.** El mensaje de error, el `ErrorCode` en la traza (siempre va, en hexadecimal) y
el conteo de los registros del archivo.

**Cómo se sabe que falló.** Se grabaron las filas anteriores a la que falló: se perdió el todo o nada.
El usuario recibe `MPPP-CAR-009` por una falla de **plataforma**: el código está fuera de la lista de
`FallasDePlataforma` y debería ser `011` (ver L-20).

**Automatizable.** API en Dev solo cuando aparezca una colisión real; la cobertura repetible es la
automática C# (`CatalogosMasivosApiAceptacion.cs`:
`Una_escritura_que_falla_es_MPPP_CAR_009_con_su_fila_y_sube_para_que_la_transaccion_deshaga_todo`,
`Un_error_de_dato_sigue_siendo_MPPP_CAR_009_y_pide_corregir_su_fila`,
`Un_fault_sin_codigo_conocido_ni_detalle_sigue_siendo_MPPP_CAR_009`,
`El_motivo_de_Dataverse_en_el_009_va_recortado`).

---

### L-19 — `MPPP-CAR-010`: `plantilla = true` y además vino `archivo`

**Qué prueba.** No se adivina cuál de las dos cosas se quiso: con `plantilla = true` y un `archivo` a la
vez, la llamada se rechaza con `MPPP-CAR-010`, **no** con `MPPP-CAR-001`. Con `plantilla = true` sola
se devuelve la plantilla vacía sin escribir ni leer nada.

**Necesita.** Sesión del Administrador de planes; un `.xlsx` válido en base64.

**Cómo.**
1. `POST sanic_mppp_capi_cargarcatalogos '{"plantilla":true,"archivo":"<base64>"}'`.
2. `POST sanic_mppp_capi_cargarcatalogos '{"plantilla":true}'`.
3. Ambos contra `sanic_mppp_capi_cambiarestadocatalogos`.

**Qué tiene que pasar.** Paso 1 y 3 (con archivo): `MPPP-CAR-010`. Paso 2: `grabado = false`, un
`resultado` que es la plantilla vacía, ningún cambio en Dataverse.

**Cómo se verifica.** El mensaje de error y el `resultado` decodificado.

**Cómo se sabe que falló.** `MPPP-CAR-001` en el paso 1: el plugin ignoró la bandera y trató el archivo
como carga. Con la plantilla, que cree cosas: `plantilla = true` no puede escribir.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CatalogosMasivosApiAceptacion.cs`: `Con_plantilla_y_sin_archivo_devuelve_la_plantilla_vacia_sin_escribir`,
`Plantilla_y_archivo_juntos_es_MPPP_CAR_010_y_ninguno_es_MPPP_CAR_001`).

---

### L-20 — `MPPP-CAR-011`: falla de plataforma al leer o al escribir, sin nombrar ninguna fila

**Qué prueba.** DN-04 / D-75: una falla de **plataforma** (sobrecarga, límites; clasificada por el
`ErrorCode` del `OrganizationServiceFault`, nunca por el texto) sale como `MPPP-CAR-011`, pide
reintentar en unos minutos o partir el archivo, **no culpa a ninguna fila** y no graba nada. Antes,
una carga de 300 filas fallaba con el error 10928 de Azure SQL y el 009 le decía al usuario "Corregí
esa fila" cuando la fila no tenía nada malo. El código de Dataverse va **siempre** a la traza, en
hexadecimal.

**Necesita.** Una falla de plataforma real: con el tope en 100 filas **hoy no se reproduce por
volumen** (200 filas ya dan `MPPP-CAR-006`). Reproducirla exige una versión de prueba de
`carga.filas.maximo` más alta y un archivo de 200 filas peor caso: mismo riesgo y misma regla que
`E-08`, **no se corre de rutina**. Sesión real del Administrador de planes.

**Cómo (solo si se decide repetir la medición).** Sembrar la versión de prueba de
`carga.filas.maximo` con 300 (máximo sembrado + 1); subir un archivo de 200 filas peor caso; leer la
respuesta y la traza del plugin; desactivar y borrar la versión de prueba.

**Qué tiene que pasar.** Rechazo `MPPP-CAR-011` ("Dataverse estaba sobrecargado y no pudo procesar el
archivo. No se grabó nada. Esperá unos minutos y volvé a subir el mismo archivo; si vuelve a pasar,
partilo en archivos más chicos."), sin número de fila; en la traza, el código `0x80072324`.

**Cómo se verifica.** El mensaje de error (que **no** contenga "fila" seguido de un número) y la traza
del plugin, con el código en hexadecimal.

**Cómo se sabe que falló.** El usuario recibe `MPPP-CAR-009` con "Corregí esa fila": el código no está en
la lista de plataforma. La traza no trae el código: sin él no se puede confirmar que la lista lo
cubre (el 10928 de la primera medición no lo dejó). Una falla de **lectura** de plataforma que sale
como error genérico en vez de `011`.

**Automatizable.** API — cubierto además por pruebas automáticas C#
(`CatalogosMasivosApiAceptacion.cs`: `Una_falla_de_plataforma_es_MPPP_CAR_011_y_no_culpa_a_la_fila`,
`El_cambio_de_estado_tambien_distingue_la_falla_de_plataforma`,
`El_codigo_de_Dataverse_queda_en_la_traza_en_hexadecimal_y_el_usuario_recibe_el_011`,
`Una_falla_de_plataforma_al_leer_los_catalogos_es_MPPP_CAR_011`,
`Una_falla_de_plataforma_al_leer_con_SYSTEM_tambien_es_MPPP_CAR_011`,
`El_cambio_de_estado_tambien_da_MPPP_CAR_011_si_la_plataforma_falla_al_leer`,
`El_texto_del_011_es_el_mismo_al_leer_y_al_escribir`).

---

### L-21 — Ningún error de archivo muestra el texto de la librería, y todos empiezan con su código

**Qué prueba.** DN-04, "Errores de ARCHIVO": la llamada falla con un código y un texto propio; **nunca llega
al usuario el mensaje de la librería** (LP-01); ese va a la traza. Un error inesperado sale con el
mensaje genérico y su traza, no con la excepción cruda.

**Necesita.** Los archivos de L-10 a L-16.

**Cómo.** Para cada una de las llamadas rechazadas en L-10 a L-16, leer el mensaje.

**Qué tiene que pasar.** Cada mensaje empieza con `MPPP-CAR-0xx: ` y está redactado en español para
quien carga el archivo; ninguno trae nombres de clases, rastros de pila ni texto de la librería de
lectura.

**Cómo se verifica.** Una expresión regular sobre cada mensaje: `^MPPP-CAR-0(0[1-9]|1[01]): ` y que
no contenga `Exception`, `System.` ni `OpenXml`.

**Cómo se sabe que falló.** Un mensaje que muestra una excepción interna: el envoltorio dejó pasar el
texto técnico. Un código fuera del rango 001-011: se agregó un código sin documentarlo en el diseño.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CatalogosMasivosApiAceptacion.cs`: `Los_mensajes_de_rechazo_empiezan_con_su_codigo_y_no_traen_jerga_de_la_libreria`,
`Un_rechazo_sale_con_su_mensaje_y_un_error_inesperado_con_el_generico_y_la_traza`).

---

## Grupo 3 — Excel 1, Carga masiva: reglas por fila

Cada caso arma un archivo de pocas filas, lo sube a `cargarcatalogos` como el Administrador de planes y
lee la columna **Resultado** (L) de la fila nombrada. Los motivos tienen el formato
`<Columna>: <qué pasa>. <Qué hacer>.`. Un error de una fila **no graba nada** del archivo
(L-43): para ver el resultado "Creado: ..." de una fila hay que subirla en un archivo sin errores.

### L-22 — Reglas 1 y 2: la fila necesita su cliente, y los dos CIF tienen que ser coherentes con lo grabado

**Qué prueba.** Regla 1: sin CIF BAC ni CIF COM, error. Regla 2: se busca por A y por B, normalizados
(`123` encuentra a `000000123`); si vienen los dos y encuentran **clientes distintos**, o uno
encuentra un cliente y el otro no existe, error.

**Necesita.** Dos clientes existentes del set (`ZZP-Cliente A`, CIF BAC `000900001`, CIF COM
`ZZP900001001`; `ZZP-Cliente B`, CIF BAC `000900002`, CIF COM `ZZP900002001`), creados por el propio
set.

**Cómo.** Subir un archivo con cuatro filas: (1) sin A ni B; (2) A de `Cliente A` y B de `Cliente B`;
(3) A de `Cliente A` y B inexistente; (4) A = `900001` (sin ceros) y B de `Cliente A` (debe ser válido).

**Qué tiene que pasar.** Fila 1: error "toda fila necesita su cliente". Fila 2: error de `CIF BAC / CIF
COM`: "apuntan a dos clientes distintos (ZZP-Cliente A y ZZP-Cliente B)". Fila 3: error: no
coinciden con lo grabado. Fila 4: sin error (el CIF se normalizó antes de buscar). El archivo no
se graba por las filas 1 a 3.

**Cómo se verifica.** La columna L de las filas 2 a 5 del `resultado`.

**Cómo se sabe que falló.** La fila 4 da error: la búsqueda no normaliza (es el motivo de "Normalizar
antes de buscar" del diseño). La fila 2 no nombra a los dos clientes.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`tests/Sanic.Mppp.Plugins.Tests/Aceptacion/CargaDeCatalogosAceptacion.cs`:
`Sin_CIF_BAC_ni_CIF_COM_la_fila_es_error`,
`El_cliente_se_encuentra_por_CIF_BAC_normalizado_y_se_autoriza_el_correo_existente`,
`El_cliente_se_encuentra_por_CIF_COM_en_minuscula`,
`CIF_BAC_y_CIF_COM_de_dos_clientes_distintos_es_error_que_nombra_a_los_dos`,
`Un_CIF_encuentra_un_cliente_y_el_otro_no_existe_es_error`,
`CIF_BAC_con_formato_invalido_es_error_de_esa_columna`,
`CIF_COM_con_formato_invalido_es_error_de_esa_columna`).

---

### L-23 — Regla 3: un cliente existente no se modifica; si el archivo lo contradice, error; si está inactivo, advertencia

**Qué prueba.** Para un cliente que **ya existe**: si Razón social (C) o Correo ejecutivo (D) vienen y no
coinciden con lo grabado (razón social sin distinguir mayúsculas ni espacios a los lados; ejecutivo =
el dueño actual), error: la carga **no modifica**. Si existe **inactivo**, advertencia: lo que se le
cargue no va a estar vigente hasta activarlo.

**Necesita.** Un cliente existente activo `ZZP-Cliente A` con dueño `# User 1`, y un cliente
existente **inactivo** (`statecode = 1`) `ZZP-Cliente I`.

**Cómo.** Subir un archivo con: (1) A de `Cliente A` con razón social `ZZP-OTRA`; (2) A de `Cliente A`
con un Correo ejecutivo de `# User 2`; (3) A de `Cliente A` con la razón social en minúscula y con
espacios al final (debe pasar); (4) A de `Cliente I`, sin más datos. Después, sin errores, repetir
solo (3) y (4) y comparar el cliente en Dataverse antes y después.

**Qué tiene que pasar.** (1) y (2): error que dice que no coincide con lo grabado; (3): sin error; (4):
"Advertencia: ... no va a estar vigente hasta activarlo". El cliente `Cliente A` sigue con la misma
razón social y el mismo dueño (`modifiedon` sin cambios).

**Cómo se verifica.** La columna L del `resultado` y `dataverse_api.py GET
"sanic_mppp_tbl_clientes(<id>)?$select=sanic_nombre,_ownerid_value,modifiedon"` antes y después.

**Cómo se sabe que falló.** La razón social o el dueño cambiaron: la carga está pisando datos (la
razón del diseño es que "una carga masiva que de paso pisa razones sociales o dueños es un cambio
que nadie revisó").

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CargaDeCatalogosAceptacion.cs`: `Cliente_existente_con_otra_razon_social_es_error_y_no_se_pisa`,
`Cliente_existente_con_otro_ejecutivo_es_error_y_con_el_mismo_no`,
`Cliente_existente_inactivo_es_advertencia_no_error`,
`Razon_social_de_mas_de_200_es_error_y_de_200_no`).

---

### L-24 — Regla 4: un cliente nuevo exige A, B, C y D, y el ejecutivo tiene que ser exactamente un usuario habilitado con el rol Ejecutivo

**Qué prueba.** Para un cliente que **no existe**: hacen falta CIF BAC, CIF COM, Razón social y Correo
ejecutivo; si falta alguno, error nombrando cuál. D tiene que ser **exactamente un** usuario
**habilitado**, con ese correo y con el rol `SR - MPPP - Ejecutivo`. Cero usuarios, varios usuarios,
deshabilitado o sin el rol: error, nombrando cuál de esos casos es.

**Necesita.** El correo de `# User 1` (Ejecutivo, habilitado); el de `# User 2` (Supervisor, sin el rol
Ejecutivo); un correo inventado (cero usuarios). **Los motivos "deshabilitado" y "varios usuarios con
ese correo" necesitan un usuario deshabilitado y dos usuarios con el mismo correo, que el entorno de
prueba hoy no garantiza**: si `set_l.py` no los puede preparar, esos dos motivos se anotan como
cubiertos solo por la suite automática, no como OK.

**Cómo.** Subir un archivo con cinco filas de cliente nuevo: (1) sin CIF COM; (2) sin Razón social; (3)
Correo ejecutivo = el de `# User 2`; (4) Correo ejecutivo inventado; (5) todo correcto con el
correo de `# User 1`.

**Qué tiene que pasar.** (1) y (2): error que nombra la columna que falta. (3): error "no tiene el rol
Ejecutivo. Pedí que se lo asignen o usá otro correo." (4): error que dice que no hay ningún usuario
con ese correo. (5): sin error. Para "deshabilitado" y "varios", cada uno con su propio motivo, y los
dos distintos entre sí.

**Cómo se verifica.** La columna L del `resultado`; y la consulta que respalda cada motivo:
`dataverse_api.py GET "systemusers?$filter=internalemailaddress eq '<correo>'&$select=isdisabled"` y
los roles de `# User 2` en `systemuserroles_association`.

**Cómo se sabe que falló.** Un cliente a nombre de alguien que no es ejecutivo se acepta: ese cliente no
aparece en "Mis clientes" de nadie y nadie lo nota (el motivo de la regla). Dos motivos con el mismo
texto: el usuario no puede distinguir qué corregir.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CargaDeCatalogosAceptacion.cs`: `Cliente_nuevo_incompleto_nombra_cada_columna_que_falta`,
`El_ejecutivo_tiene_que_ser_exactamente_un_usuario_habilitado_con_el_rol_y_cada_caso_tiene_su_motivo`,
`El_correo_del_ejecutivo_se_compara_sin_mayusculas_ni_espacios_a_los_lados`,
`Cliente_nuevo_completo_se_crea_normalizado_con_el_ejecutivo_como_dueno`;
`CatalogosMasivosAceptacion.cs`: `Los_usuarios_se_leen_con_SYSTEM_y_dicen_si_estan_habilitados_y_si_son_ejecutivos`,
`Si_el_rol_de_ejecutivo_no_existe_en_el_entorno_es_un_error_de_configuracion_que_lo_nombra`).

---

### L-25 — Reglas 5 y 7: los datos de un plan sin código son error, y un plan nuevo exige tipo de formato y moneda

**Qué prueba.** Regla 5: Tipo formato (F) o Moneda (G) sin Código de plan (E) es error: son datos de un
plan que no se nombró. Regla 7: un plan que **no existe** necesita F y G; si falta alguno, error. El
Plan **no tiene nombre que se cargue**: `sanic_nombre` es calculado (`<código> - <cliente>`).
Tipo formato acepta `06`, `10` u `11` (y `6` como `06`); Moneda `COR` o `USD`.

**Necesita.** Un cliente existente `ZZP-Cliente A`.

**Cómo.** Subir un archivo con: (1) A de `Cliente A`, F = `10`, sin E; (2) A de `Cliente A`, E = `ZP91`,
sin F ni G; (3) E = `ZP92`, F = `7`, G = `COR`; (4) E = `ZP93`, F = `6`, G = `usd`; (5) E = `ZP94`,
F = `11`, G = `EUR`.

**Qué tiene que pasar.** (1): error, datos de un plan sin código. (2): error que nombra Tipo formato y
Moneda. (3): error de Tipo formato. (4): sin error (el `6` se acepta como `06`; la moneda se normaliza).
(5): error de Moneda.

**Cómo se verifica.** La columna L del `resultado`. Para (4), en un archivo sin errores:
`GET sanic_mppp_tbl_plans?$filter=sanic_codigo eq 'ZP93'&$select=sanic_nombre,sanic_tipoformato,sanic_moneda`
con el nombre armado como `ZP93 - <razón social del cliente>`.

**Cómo se sabe que falló.** El plan se crea con un nombre distinto del calculado (`NombreCalculadoStep`
no corrió o el archivo pisó el nombre). `6` rechazado: no se aplicó la normalización de `06`.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CargaDeCatalogosAceptacion.cs`: `Tipo_formato_o_moneda_sin_codigo_de_plan_es_error`,
`Tipo_formato_acepta_06_10_11_y_6`, `Tipo_formato_invalido_es_error`, `Moneda_invalida_es_error`,
`Plan_nuevo_incompleto_nombra_lo_que_falta`, `Codigo_de_plan_invalido_es_error`,
`Plan_nuevo_de_cliente_existente_se_crea_normalizado`,
`Formato_11_no_tiene_regla_de_moneda_ni_de_clasificacion_al_crear_el_plan`).

---

### L-26 — Regla 6: un plan existente tiene que ser del cliente de la fila; si el archivo lo contradice, error; si está inactivo, advertencia

**Qué prueba.** Para un plan que **ya existe**: su cliente tiene que ser el de la fila; si no, error
nombrando el cliente real (el código de plan es único en todo el banco). Si F o G vienen y no
coinciden con lo grabado, error. Inactivo, advertencia.

**Necesita.** Un plan existente `ZP81` de `ZZP-Cliente A` (formato `10`, moneda `COR`); un plan
existente inactivo `ZP82` de `Cliente A`; un cliente `ZZP-Cliente B`.

**Cómo.** Subir un archivo con: (1) `Cliente B` con E = `ZP81`; (2) `Cliente A` con E = `ZP81` y G =
`USD`; (3) `Cliente A` con E = `ZP81`, F = `10`, G = `COR` (debe pasar); (4) `Cliente A` con E =
`ZP82`; (5) un cliente **nuevo** con E = `ZP81`.

**Qué tiene que pasar.** (1): error "el plan ZP81 ya existe y es de ZZP-Cliente A, no de este cliente."
(2): error: moneda no coincide con lo grabado. (3): sin error. (4): "Advertencia: ... inactivo ...". (5):
error (un plan existente con un cliente nuevo en la fila).

**Cómo se verifica.** La columna L del `resultado`.

**Cómo se sabe que falló.** (1) pasa o crea un segundo plan con el mismo código: se rompe la unicidad
del código de plan en todo el banco. El texto de (1) no nombra al cliente real.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CargaDeCatalogosAceptacion.cs`: `Plan_existente_del_mismo_cliente_no_se_crea_y_sus_datos_tienen_que_coincidir`,
`Plan_existente_de_otro_cliente_es_error_que_nombra_al_cliente_real`,
`Plan_existente_con_un_cliente_nuevo_en_la_fila_es_error`, `Plan_existente_inactivo_es_advertencia`,
`Plan_nuevo_de_un_cliente_nuevo_apunta_al_cliente_por_su_CIF_BAC`).

---

### L-27 — Reglas 8 y 9: los datos de una autorización sin correo son error, y el correo tiene que tener formato válido

**Qué prueba.** Regla 8: Nombre contacto (I), Fecha documento (J) o Comentarios (K) sin Correo (H) es
error: son datos de una autorización sin correo. Regla 9: H con formato de correo inválido es error. El
correo se guarda en minúscula y sin espacios.

**Necesita.** Un cliente existente `ZZP-Cliente A`.

**Cómo.** Subir un archivo con: (1) `Cliente A`, I = `ZZP-Persona`, sin H; (2) `Cliente A`, K =
`un comentario`, sin H; (3) `Cliente A`, H = `no-es-un-correo`, I = `ZZP-Persona`; (4) `Cliente A`,
H = ` ZZP.Persona@Empresa.com ` (mayúsculas y espacios), I = `ZZP-Persona`.

**Qué tiene que pasar.** (1) y (2): error que dice que los datos son de una autorización sin correo.
(3): error de formato de correo. (4): sin error; el correo guardado es `zzp.persona@empresa.com`.

**Cómo se verifica.** La columna L del `resultado`; y en el archivo sin errores,
`GET sanic_mppp_tbl_autorizados?$filter=sanic_nombre eq 'zzp.persona@empresa.com'`.

**Cómo se sabe que falló.** El correo queda guardado con mayúsculas o espacios: la normalización no
corrió y la búsqueda de correos existentes se rompería.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CargaDeCatalogosAceptacion.cs`: `Nombre_fecha_o_comentarios_sin_correo_es_error`,
`Correo_con_formato_invalido_es_error`, `Correo_nuevo_crea_el_correo_y_la_autorizacion_normalizados`,
`Largos_maximos_de_correo_nombre_y_comentarios`).

---

### L-28 — Reglas 10 y 11: un correo existente se reutiliza (sin pisar su nombre); uno nuevo exige Nombre contacto

**Qué prueba.** Regla 10: el correo **existe**: si Nombre contacto (I) viene y no coincide, error;
inactivo, advertencia. Regla 11: el correo **no existe**: hace falta I. Un correo existente se puede
autorizar sobre otro cliente reutilizando su `id`.

**Necesita.** Un correo existente `zzp.uno@empresa.com` (contacto `ZZP-Uno`, activo) y uno inactivo
`zzp.dos@empresa.com`; dos clientes `ZZP-Cliente A` y `ZZP-Cliente B`; el correo `zzp.uno@...` ya
autorizado sobre `Cliente A`.

**Cómo.** Subir un archivo con: (1) `Cliente A`, H = `zzp.uno@empresa.com`, I = `OTRO NOMBRE`; (2)
`Cliente B`, H = `zzp.uno@empresa.com`, sin I (debe pasar: se autoriza el correo existente sobre otro
cliente); (3) `Cliente A`, H = `zzp.nuevo@empresa.com`, sin I; (4) `Cliente A`, H =
`zzp.dos@empresa.com`.

**Qué tiene que pasar.** (1): error: el nombre no coincide con lo grabado. (2): sin error. (3): error:
hace falta Nombre contacto. (4): advertencia de correo inactivo.

**Cómo se verifica.** La columna L del `resultado`; y, tras grabar (2), `GET
sanic_mppp_tbl_autorizados?$filter=sanic_nombre eq 'zzp.uno@empresa.com'&$count=true` devuelve **uno**
solo (no se duplicó el correo).

**Cómo se sabe que falló.** Se crea un segundo `Autorizado` con el mismo correo: choca con su clave
alterna, y en Dev el síntoma es un `HTTP 412` o un `MPPP-CAR-009` en la pasada 2 en vez de un error
de fila en la pasada 1.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CargaDeCatalogosAceptacion.cs`: `Correo_nuevo_sin_nombre_de_contacto_es_error`,
`Correo_existente_con_otro_nombre_es_error_y_con_el_mismo_no`,
`Correo_existente_se_autoriza_sobre_otro_cliente_usando_su_id`, `Correo_existente_inactivo_es_advertencia`).

---

### L-29 — Reglas 12, 13 y 14: una autorización existente activa queda "ya existía", una inactiva es error y una nueva nace sin evidencia firmada

**Qué prueba.** Regla 12: la autorización (correo, cliente) existe **activa**: queda "ya existía"; si J o
K vienen y no coinciden con lo grabado, error. Regla 13: existe **inactiva**: error, se reactiva con el
botón de Activar/Desactivar, no desde la carga. Regla 14: **no existe**: se crea con J y K, y nace
**sin evidencia firmada** (D-42: un archivo no puede viajar en una celda); aparece en la vista
"Autorizaciones sin evidencia".

**Necesita.** `ZZP-Cliente A`; el correo `zzp.uno@empresa.com`; una autorización activa (`Cliente A`,
`zzp.uno`) con comentario `viejo`; una autorización **inactiva** (`Cliente A`, `zzp.dos`).

**Cómo.** Subir un archivo con: (1) `Cliente A` + `zzp.uno` + K = `viejo` (coincide); (2) `Cliente A` +
`zzp.uno` + K = `nuevo` (no coincide); (3) `Cliente A` + `zzp.dos`; (4) `Cliente A` + `zzp.tres` (correo
nuevo con I) + J = `01/09/2026` + K = `alta masiva`. Para (4), en un archivo sin errores.

**Qué tiene que pasar.** (1): sin error y "Sin cambios: todo ya existía...". (2): error: el comentario no
coincide con lo grabado. (3): error: se reactiva con el botón de Activar/Desactivar. (4): "Creado:
correo, autorización" (con el cliente existente); la autorización tiene `sanic_documentofirmado`
vacío, `sanic_fechadocumento = 2026-09-01` y `sanic_comentarios = 'alta masiva'`, y aparece en
la vista "Autorizaciones sin evidencia".

**Cómo se verifica.** La columna L del `resultado`; `GET
sanic_mppp_tbl_autorizacionclientes?$filter=_sanic_clienteid_value eq <id>&$select=sanic_comentarios,sanic_fechadocumento,sanic_documentofirmado`;
y la vista "Autorizaciones sin evidencia" en la app.

**Cómo se sabe que falló.** (3) pasa y reactiva sola: se mezcló la carga con el cambio de estado. (4)
nace con evidencia: algo cargó un archivo que el diseño dice que no puede viajar.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CargaDeCatalogosAceptacion.cs`: `Autorizacion_existente_activa_queda_ya_existia_y_sus_datos_tienen_que_coincidir`,
`Autorizacion_existente_inactiva_es_error_se_reactiva_con_el_otro_boton`,
`Correo_nuevo_crea_el_correo_y_la_autorizacion_normalizados`,
`Fecha_documento_acepta_celda_de_fecha_y_texto_dd_mm_aaaa_sin_hora`).

---

### L-30 — Regla 15: la misma clave tiene que tener los mismos datos en todas sus filas

**Qué prueba.** Para lo que el archivo **crea**: un mismo cliente, plan, correo o autorización repetido en
varias filas (una por plan, por la desnormalización) tiene que traer los mismos datos; las celdas
vacías no contradicen. Una contradicción es error **en todas las filas involucradas**, nombrándolas
("filas 3 y 9"). Para lo que **ya existe**, cada fila se compara contra lo grabado, así que solo
lleva error la que lo contradice. Un cliente nuevo puede repartir sus datos entre filas si no se
contradicen.

**Necesita.** Ningún cliente existente con los CIF del archivo.

**Cómo.** Subir un archivo con: filas 2 y 3: mismo CIF BAC `000900101` con Razón social `ZZP-Uno` y
`ZZP-Dos`; filas 4 y 5: mismo CIF BAC `000900102`, la fila 4 con razón social y ejecutivo, la fila 5
con CIF BAC, CIF COM y otro plan (sin razón social: debe pasar); fila 6: mismo plan nuevo `ZP71` bajo
`000900101` y `000900102` (dos clientes).

**Qué tiene que pasar.** Filas 2 y 3: error "...aparece con otra razón social en la fila N" en **las dos**.
Filas 4 y 5: sin error. Fila 6 (junto a su par): error en las dos filas del mismo plan bajo dos clientes.

**Cómo se verifica.** La columna L del `resultado`, filas 2 a 7.

**Cómo se sabe que falló.** Solo una de las dos filas lleva error: el usuario corrige la que no es. O
dos filas con datos contradictorios se aceptan y la que "gana" es la de menor número, en silencio.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CargaDeCatalogosAceptacion.cs`: `La_misma_clave_con_datos_distintos_es_error_en_todas_las_filas_y_nombra_a_las_otras`,
`Los_datos_de_un_cliente_nuevo_pueden_repartirse_entre_filas_si_no_se_contradicen`,
`Si_a_un_cliente_nuevo_le_falta_un_dato_en_todo_el_archivo_el_error_va_en_todas_sus_filas`,
`El_mismo_plan_nuevo_bajo_dos_clientes_es_error_en_las_dos_filas`,
`Sobre_algo_que_ya_existe_lleva_error_solo_la_fila_que_contradice_lo_grabado`,
`La_misma_autorizacion_en_varias_filas_se_crea_una_vez_y_las_celdas_vacias_no_contradicen`).

---

### L-31 — Regla 16: un mismo CIF BAC con dos CIF COM distintos en el archivo (o al revés) es error

**Qué prueba.** Un CIF BAC con dos CIF COM distintos en el archivo, o al revés, es error en esas filas, aun
cuando ninguno de los clientes exista. Un CIF BAC solo y un CIF COM solo son el mismo cliente nuevo si
otra fila trae los dos.

**Necesita.** Ningún cliente existente con esos CIF.

**Cómo.** Subir un archivo con: (1) filas 2 y 3, CIF BAC `000900201` con CIF COM `ZZP900201001` y
`ZZP900201002`; (2) filas 4 y 5, CIF COM `ZZP900202001` con CIF BAC `000900202` y `000900203`; (3)
fila 6 con solo CIF BAC `000900204` y fila 7 con solo CIF COM `ZZP900204001` y fila 8 con los dos
(debe pasar: es un solo cliente).

**Qué tiene que pasar.** (1) y (2): error en las dos filas de cada par. (3): sin error; se crea un cliente.

**Cómo se verifica.** La columna L del `resultado`.

**Cómo se sabe que falló.** Se crean dos clientes con el mismo CIF BAC o el mismo CIF COM (queda
violada la unicidad del cliente).

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CargaDeCatalogosAceptacion.cs`: `Un_CIF_BAC_con_dos_CIF_COM_en_el_archivo_o_al_reves_es_error`,
`Un_CIF_BAC_solo_y_un_CIF_COM_solo_son_el_mismo_cliente_nuevo_si_otra_fila_trae_los_dos`).

---

### L-32 — Lectura de celdas: números sin ceros, notación científica, filas vacías y número de fila de Excel

**Qué prueba.** DN-04, "Lectura del archivo": un CIF o un código de plan que Excel guardó como número
(`123`) se lee como texto y se rellena con ceros; un número en notación científica (`1.23E+08`)
se rechaza en esa celda: no se puede reconstruir con certeza. Una fila totalmente vacía se saltea;
el número de fila de los mensajes es **el de Excel**. Las demás hojas del archivo se ignoran.

**Necesita.** Un archivo armado con la celda A2 numérica `123`, A3 con `1.23E+08`, una fila 4 totalmente
vacía, una fila 5 con error, y una segunda hoja `Instrucciones` con texto.

**Cómo.** Subir el archivo y leer la columna L de las filas 2, 3 y 5.

**Qué tiene que pasar.** La fila 2 se interpreta como CIF `000000123`. La fila 3 lleva error en CIF BAC
por la notación científica. La fila 4 no genera resultado. El error de la fila 5 dice "fila 5", la
que se ve a la izquierda en Excel. La hoja `Instrucciones` no molesta.

**Cómo se verifica.** La columna L de las filas 2, 3 y 5 y la cantidad de filas del contador `filas`
(no vacías).

**Cómo se sabe que falló.** La fila 3 se acepta (un cliente mal creado con un CIF inventado) o el número
de fila no coincide con el de Excel (el usuario corrige otra fila).

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CatalogosMasivosApiAceptacion.cs`: `Una_celda_con_error_de_Excel_no_se_graba_en_silencio`;
`CargaDeCatalogosAceptacion.cs`: `Un_aviso_del_lector_es_error_de_su_fila_con_el_encabezado_de_su_columna`).

---

### L-33 — Fecha documento: celda de fecha o `dd/mm/aaaa`; futura, ilegible o anterior al 01/01/1900 es error

**Qué prueba.** Se acepta una celda de fecha de Excel o texto `dd/mm/aaaa`; cualquier otra forma es
error. Una fecha futura es error (un documento firmado no puede tener fecha de mañana). Una anterior
al 01/01/1900 también (Dataverse no admite fechas anteriores a 1753 y reventaría la pasada 2 sin
decir qué fila). "Hoy" es **la fecha de Nicaragua** (UTC menos 6 horas).

**Necesita.** Un cliente existente y un correo existente; el archivo de cada variante. **La fecha de
"mañana" se calcula como UTC menos 6 horas**, no con la fecha local de la máquina: entre las 18:00 y las
24:00 de Nicaragua la fecha UTC ya es la de mañana, y una prueba armada con la fecha UTC daría un
resultado equivocado.

**Cómo.** Subir un archivo con la autorización repetida en varias filas, cada una con una Fecha
documento distinta: (1) celda de fecha de ayer; (2) texto `15/09/2026`; (3) `2026-09-15` (otro
formato); (4) mañana de Nicaragua; (5) `31/12/1899`; (6) `01/01/1900`.

**Qué tiene que pasar.** (1), (2) y (6): sin error. (3): error de formato en Fecha documento. (4) y (5):
error.

**Cómo se verifica.** La columna L del `resultado`; la fecha guardada de la autorización creada.

**Cómo se sabe que falló.** (4) pasa: la fecha de hoy se calculó en UTC. (5) pasa la pasada 1 y revienta
la 2 (aparece `MPPP-CAR-009` sin que la fila 5 tuviera un error de pasada 1): se perdió el tope de 1900.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CargaDeCatalogosAceptacion.cs`: `Fecha_documento_acepta_celda_de_fecha_y_texto_dd_mm_aaaa_sin_hora`,
`Fecha_documento_futura_o_ilegible_es_error`, `Fecha_documento_anterior_a_1900_es_error_en_la_pasada_1`,
`Fecha_documento_desde_el_1_de_enero_de_1900_vale`;
`CatalogosMasivosApiAceptacion.cs`: `La_fecha_de_hoy_la_decide_quien_construye_el_nucleo`).

---

### L-34 — Una fila reporta todos sus errores, y ningún mensaje cita un valor entero

**Qué prueba.** DN-04, "Formato de los motivos" y "Lectura": **se reportan todos los errores de la fila**,
no solo el primero (para corregir de una vez), separados por ` | `; el texto va en el orden errores,
la acción y las advertencias; y una celda enorme se cita acortada (60 caracteres) o no se cita, para
que Resultado nunca se acerque al tope de 32.767 caracteres de una celda de Excel.

**Necesita.** Un cliente existente `ZZP-Cliente A`.

**Cómo.** Subir una fila con cinco errores a la vez (CIF COM inválido, Moneda inválida sin Tipo
formato, correo mal formado con Nombre contacto, fecha futura, comentario de 51 caracteres) y otra
fila con una Razón social de 40.000 caracteres.

**Qué tiene que pasar.** La columna L de la primera fila lista los cinco errores, cada uno con el
formato `<Columna>: <qué pasa>. <Qué hacer>.`, separados por ` | `; la de la segunda, un error que
no reproduce los 40.000 caracteres (a lo sumo 60, acortado con `…`) y la celda no pasa de 32.767.

**Cómo se verifica.** La columna L del `resultado` y su largo.

**Cómo se sabe que falló.** Solo aparece el primer error: el usuario corrige, sube de nuevo, ve el
segundo, y así cinco vueltas. O el resultado no se puede abrir en Excel por exceder el tope de una
celda.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CargaDeCatalogosAceptacion.cs`: `Una_fila_reporta_todos_sus_errores_no_solo_el_primero`,
`El_texto_lleva_errores_luego_la_accion_y_al_final_las_advertencias`,
`Ningun_valor_enorme_en_ninguna_columna_produce_un_Resultado_enorme`,
`Correo_del_ejecutivo_de_mas_de_320_es_error_corto_y_no_entra_a_las_claves`;
`ExcelDeCatalogosAceptacion.cs`: `Un_texto_de_mas_de_32767_caracteres_es_error_y_uno_de_32767_no`,
`Un_valor_del_usuario_de_mas_de_32767_caracteres_se_escribe_acortado_en_el_eco_sin_fallar`,
`Un_caracter_que_XML_no_admite_se_reemplaza_y_nunca_tira_el_armado`).

---

## Grupo 4 — Excel 2, Activar / Desactivar: reglas por fila

Cada caso arma un archivo de `ActivarDesactivar` (columnas Tipo, Accion, CIF BAC, CIF COM, Codigo plan,
Correo), lo sube a `cambiarestadocatalogos` como el Administrador de planes y lee la columna **Resultado**
(G). El cambio de estado es **todo o nada** igual que la carga.

### L-35 — Regla 1: Tipo o Accion fuera de su lista es error (sin distinguir mayúsculas ni espacios)

**Qué prueba.** Tipo es uno de `CLIENTE`, `PLAN`, `CORREO`, `AUTORIZACION_CLIENTE`; Accion es `ACTIVAR` o
`DESACTIVAR`. Se comparan sin distinguir mayúsculas ni espacios a los lados.

**Necesita.** Un cliente existente `ZZP-Cliente A`.

**Cómo.** Subir un archivo con: (1) Tipo = `PRODUCTO`; (2) Accion = `BORRAR`; (3) Tipo = ` cliente `, Accion =
`desactivar` (A de `Cliente A`).

**Qué tiene que pasar.** (1) y (2): error que nombra Tipo o Accion. (3): sin error.

**Cómo se verifica.** La columna G del `resultado`.

**Cómo se sabe que falló.** (3) se rechaza: la comparación distingue mayúsculas o espacios. (1) se acepta
y la fila se ignora en silencio.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`tests/Sanic.Mppp.Plugins.Tests/Aceptacion/CambioDeEstadoAceptacion.cs`: `Tipo_fuera_de_su_lista_es_error`,
`Accion_fuera_de_su_lista_es_error`, `Tipo_y_accion_se_comparan_sin_mayusculas_ni_espacios_a_los_lados`).

---

### L-36 — Regla 2: cada Tipo exige sus claves y ninguna otra

**Qué prueba.** `CLIENTE` y `AUTORIZACION_CLIENTE` llevan CIF BAC y/o CIF COM (si vienen los dos, tienen
que apuntar al mismo cliente); `PLAN` lleva Codigo plan; `CORREO` lleva Correo; `AUTORIZACION_CLIENTE`
lleva además Correo. Si falta una clave, error; si **sobra** una (por ejemplo, un código de plan en una
fila `CORREO`), error: "una celda que no se usa es una intención que no se cumple".

**Necesita.** Un cliente, un plan, un correo y su autorización existentes del set.

**Cómo.** Subir un archivo con: (1) `CLIENTE` sin CIF; (2) `CLIENTE` con CIF y un Correo de más; (3) `PLAN`
sin Codigo plan; (4) `PLAN` con Codigo plan y CIF BAC de más; (5) `CORREO` con Correo y Codigo plan de
más; (6) `AUTORIZACION_CLIENTE` con CIF pero sin Correo; (7) `CLIENTE` con los dos CIF de clientes
distintos; (8) `CLIENTE` con solo CIF COM (debe pasar).

**Qué tiene que pasar.** (1) a (7): error que nombra la clave que falta o la que sobra; (8): sin error.

**Cómo se verifica.** La columna G del `resultado`.

**Cómo se sabe que falló.** (2), (4) o (5) pasan: la celda de más se ignora y quien cargó cree que algo
se aplicó.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CambioDeEstadoAceptacion.cs`: `Cada_tipo_exige_sus_claves_y_ninguna_otra`,
`Cliente_por_CIF_BAC_por_CIF_COM_o_por_los_dos_si_coinciden`,
`Cliente_con_CIF_que_no_coinciden_inexistente_o_sin_CIF_es_error`).

---

### L-37 — Regla 3: el registro tiene que existir; en `AUTORIZACION_CLIENTE`, el par (correo, cliente) tiene que existir

**Qué prueba.** Si el cliente, el plan, el correo o la autorización no existe, error. Que el correo y el
cliente existan **por separado** no alcanza para `AUTORIZACION_CLIENTE`: tiene que existir la
autorización de ese correo sobre ese cliente.

**Necesita.** `ZZP-Cliente A` y `ZZP-Cliente B`; el correo `zzp.uno@empresa.com` autorizado solo sobre
`Cliente A`.

**Cómo.** Subir un archivo con: (1) `CLIENTE` con un CIF que no existe; (2) `PLAN` con un código que no
existe; (3) `CORREO` con un correo que no existe; (4) `AUTORIZACION_CLIENTE` de `Cliente B` + `zzp.uno`
(los dos existen, el par no).

**Qué tiene que pasar.** Las cuatro filas con error: no existe.

**Cómo se verifica.** La columna G del `resultado`.

**Cómo se sabe que falló.** (4) pasa y crea una autorización: el cambio de estado se está usando como
alta.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CambioDeEstadoAceptacion.cs`: `Cliente_con_CIF_que_no_coinciden_inexistente_o_sin_CIF_es_error`,
`Plan_por_codigo_normalizado_inexistente_o_invalido`, `Correo_por_correo_normalizado_inexistente_o_invalido`,
`Autorizacion_por_cliente_es_el_par_correo_cliente_y_tiene_que_existir_como_par`).

---

### L-38 — Regla 4: lo que ya está en el estado pedido es "Sin cambios", no error

**Qué prueba.** Pedir DESACTIVAR de algo ya inactivo, o ACTIVAR de algo ya activo, da "Sin cambios: ya
estaba inactivo" / "ya estaba activo". No es error, y los contadores `sincambios` lo cuentan.

**Necesita.** Un cliente activo y un plan inactivo del set.

**Cómo.** Subir un archivo con: (1) `CLIENTE` ACTIVAR (ya activo); (2) `PLAN` DESACTIVAR (ya inactivo).

**Qué tiene que pasar.** Las dos filas: "Sin cambios: ya estaba activo/inactivo"; `grabado = true`;
`sincambios = 2`, `activados = 0`, `desactivados = 0`; nada cambió en Dataverse.

**Cómo se verifica.** Las columnas del `resultado`, los contadores de la respuesta y `GET
sanic_mppp_tbl_clientes(<id>)?$select=statecode,modifiedon`.

**Cómo se sabe que falló.** Una de las dos filas da error: el archivo no se puede volver a subir
(pierde la idempotencia, L-46).

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CambioDeEstadoAceptacion.cs`: `Lo_que_ya_esta_en_el_estado_pedido_no_cambia_y_no_es_error`,
`Los_contadores_cuentan_cambios_reales`).

---

### L-39 — Regla 5: el mismo registro dos veces con acciones opuestas es error; con la misma acción, se aplica una vez

**Qué prueba.** Dos filas que nombran el mismo registro con acciones **opuestas**: error en las dos,
nombrando a la otra. Con la **misma acción**: se aplica una vez y la segunda lleva advertencia de
repetida. Se detecta aunque el registro se nombre de formas distintas (por CIF BAC en una fila y por
CIF COM en otra) y aunque alguna de las filas tenga otro error.

**Necesita.** Un cliente activo `ZZP-Cliente A` y su plan `ZP81`.

**Cómo.** Subir un archivo con: filas 2 y 3 `CLIENTE` `Cliente A` DESACTIVAR y ACTIVAR (opuestas); filas
4, 5 y 6 `PLAN` `ZP81` DESACTIVAR (por código, y otras dos repeticiones) (repetida).

**Qué tiene que pasar.** Filas 2 y 3: error cada una, que nombra a la otra fila. Filas 4 a 6: sin error;
la fila 4 aplica; la 5 y la 6 llevan advertencia "ya lo pide la fila 4 con la misma acción. Se aplica una
sola vez.". Como el archivo tiene errores, no se graba nada.

**Cómo se verifica.** La columna G del `resultado`; y el segundo envío, sin las filas opuestas: el plan
queda inactivo una sola vez y `desactivados = 1`.

**Cómo se sabe que falló.** Las opuestas se aplican en el orden de las filas (queda el estado de la
última): se pierde la intención de quien cargó el archivo.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CambioDeEstadoAceptacion.cs`: `El_mismo_registro_con_la_misma_accion_se_aplica_una_vez_y_la_segunda_fila_lo_advierte`,
`El_mismo_registro_con_acciones_opuestas_es_error_en_las_dos_filas_nombrando_a_la_otra`,
`Opuestas_y_repetidas_se_detectan_aunque_una_de_las_filas_tenga_otro_error`,
`Una_repetida_de_tres_filas_que_nombran_el_registro_de_formas_distintas_se_aplica_una_vez`).

---

### L-40 — Regla 6: advertencias de efecto, calculadas sobre el estado final del archivo

**Qué prueba.** Se calculan **después de aplicar el archivo entero**. Desactivar un `CLIENTE` o un
`CORREO` advierte "deja sin efecto N autorizaciones por cliente activas" (solo si la fila realmente
desactiva algo). Activar una `AUTORIZACION_CLIENTE` cuyo cliente o correo queda inactivo advierte "no
va a estar vigente: el cliente / el correo está inactivo", **aunque la autorización ya estuviera
activa**.

**Necesita.** Un cliente `ZZP-Cliente A` con dos autorizaciones activas; un correo con autorizaciones
sobre dos clientes; un cliente `ZZP-Cliente I` inactivo con una autorización inactiva; una
autorización ya activa cuyo cliente está inactivo.

**Cómo.** Subir un archivo con: (1) `CLIENTE` `Cliente A` DESACTIVAR; (2) `CORREO` del correo compartido
DESACTIVAR; (3) `AUTORIZACION_CLIENTE` de `Cliente I` ACTIVAR (el cliente queda inactivo); (4)
`AUTORIZACION_CLIENTE` ya activa sobre un cliente inactivo, ACTIVAR (no cambia nada); (5) un archivo
con `CLIENTE` ACTIVAR de `Cliente I` **y** `AUTORIZACION_CLIENTE` ACTIVAR de `Cliente I` (el estado final
tiene al cliente activo: no debe advertir).

**Qué tiene que pasar.** (1) "deja sin efecto 2 autorizaciones por cliente activas"; (2) "deja sin efecto
2" (las de los dos clientes); (3) y (4) "no va a estar vigente: el cliente está inactivo", también la
(4) aunque no cambie nada; (5) sin advertencia de vigencia.

**Cómo se verifica.** La columna G del `resultado`.

**Cómo se sabe que falló.** (5) advierte: el efecto se calculó contra el estado de **antes** del
archivo, no contra el final. (4) no advierte: quien pidió ACTIVAR cree que opera.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CambioDeEstadoAceptacion.cs`: `Desactivar_un_cliente_o_un_correo_advierte_cuantas_autorizaciones_activas_deja_sin_efecto`,
`El_efecto_se_cuenta_sobre_el_estado_final_del_archivo`,
`Activar_una_autorizacion_cuyo_cliente_o_correo_queda_inactivo_advierte_que_no_va_a_estar_vigente`,
`Pedir_activar_una_autorizacion_que_ya_esta_activa_pero_no_vigente_tambien_advierte`).

---

### L-41 — Regla 7 y sin cascada: el cambio de estado es el nativo, y desactivar un cliente o un correo no toca lo que depende de él

**Qué prueba.** El cambio es el nativo (`statecode`/`statuscode`, Activo/Inactivo). **Sin cascada**
(decisión 4): desactivar un Cliente o un Correo **no** desactiva sus planes ni sus autorizaciones;
dejan de valer solas porque la vigencia exige que todo esté activo (`02` §2.4). Al escribir con el
usuario que llama, queda en la auditoría quién desactivó cada cosa (DD-18).

**Necesita.** `ZZP-Cliente A` activo con un plan activo y dos autorizaciones activas.

**Cómo.**
1. Subir un archivo con `CLIENTE` `Cliente A` DESACTIVAR.
2. Consultar el plan y las autorizaciones del cliente.
3. Consultar la auditoría del cliente.

**Qué tiene que pasar.** El cliente queda `statecode = 1`; el plan y las dos autorizaciones siguen en
`statecode = 0`; la auditoría del cliente registra el cambio hecho por `# User 3`.

**Cómo se verifica.** `GET sanic_mppp_tbl_clientes(<id>)?$select=statecode`; `GET
sanic_mppp_tbl_plans?$filter=_sanic_clienteid_value eq <id>&$select=statecode`; `GET
sanic_mppp_tbl_autorizacionclientes?$filter=_sanic_clienteid_value eq <id>&$select=statecode`; `GET
audits?$filter=_objectid_value eq <id>` (con el privilegio de auditoría del service principal).

**Cómo se sabe que falló.** El plan o las autorizaciones pasaron a inactivos: alguien agregó una
cascada que el aprobador descartó. La auditoría dice que lo hizo `SYSTEM`: el plugin escribió con el
servicio equivocado.

**Automatizable.** API — cubierto por pruebas automáticas C# en el cambio nativo y la regla de
estado (`tests/Sanic.Mppp.Plugins.Tests/Aceptacion/CatalogosMasivosAceptacion.cs`:
`El_escritor_cambia_el_estado_nativo_en_la_tabla_de_cada_tipo`,
`Si_un_cambio_de_estado_falla_lanza_con_su_fila_y_no_sigue`; `CatalogosMasivosApiAceptacion.cs`:
`El_cambio_de_estado_escribe_con_el_usuario_y_cuenta_lo_que_cambio`). La ausencia de cascada y la
auditoría real contra Dev no tienen prueba automática: se verifican en Dev.

---

### L-42 — El ejemplo del aprobador, de punta a punta en Dev

**Qué prueba.** Las seis filas del ejemplo de DN-04 (CLIENTE DESACTIVAR por CIF BAC, CLIENTE ACTIVAR por
CIF COM, PLAN DESACTIVAR, CORREO DESACTIVAR, AUTORIZACION_CLIENTE DESACTIVAR por CIF BAC y
AUTORIZACION_CLIENTE ACTIVAR por CIF COM) contra una Custom API real, con un registro real de cada
tipo, y que cada fila hace exactamente lo que dice el diseño: la 2 apaga el cliente; la 4 apaga **solo**
ese plan; la 5 apaga el correo en **todos** los clientes donde está autorizado; la 6 apaga **solo** la
autorización de María sobre ese cliente y María sigue autorizada sobre otros.

**Necesita.** Los datos del ejemplo armados por el propio set con la marca `ZZP-`: dos clientes, un plan,
dos correos, y autorizaciones cruzadas (el correo 2 autorizado sobre dos clientes).

**Cómo.** Subir el archivo con las seis filas a `cambiarestadocatalogos` como `# User 3` y consultar el
estado de cada registro antes y después.

**Qué tiene que pasar.** `grabado = true`; `desactivados`, `activados` y `sincambios` suman 6 filas
con su resultado "Activado" / "Desactivado" en la columna G; el estado de cada registro es el del diseño; el
texto de resumen del diálogo en la interfaz dice "Cambio de estado terminado: N activados, N
desactivados y N sin cambios.".

**Cómo se verifica.** La columna G y los `statecode` de los registros tras la llamada.

**Cómo se sabe que falló.** La fila 5 apaga solo un cliente (se interpretó el correo por el cliente de la
fila) o la fila 6 apaga la autorización de María en todos los clientes.

**Automatizable.** API — sesión real del Administrador de planes. Pendiente: no hay evidencia de Dev
para la Custom API de cambio de estado.

---

## Grupo 5 — Todo o nada

### L-43 — Con un error de fila no se graba nada: se descarga el resultado y el diálogo dice "No se grabó nada"

**Qué prueba.** Decisión 5 y "Mecanismo": con un solo error de fila, la Pasada 1 devuelve el Excel de
resultado y **no escribe**. El navegador descarga `<nombre original>-resultado.xlsx`, la fila con error
dice `Error: ...` en la columna Resultado y el diálogo dice "No se grabó nada: {errores} filas con
errores. Revisá la columna Resultado del archivo que se descargó, corregí y volvé a subirlo.".

**Necesita.** `local/sesion-admin_planes.json`; `pruebas/gui/datos/dn04/dn04-carga-con-error.xlsx` (una
fila con error).

**Cómo.** `MPPP_USUARIO=admin_planes npx playwright test dn-04`: en Clientes, "Carga masiva" →
"Cargar archivo", elegir `dn04-carga-con-error.xlsx`; capturar la descarga y el diálogo.

**Qué tiene que pasar.** Se descarga `dn04-carga-con-error-resultado.xlsx` con 1 fila de datos, cuya columna
L empieza con `Error: `; el diálogo contiene "No se grabó nada" (o "Nothing was saved").

**Cómo se verifica.** La prueba `'Cargar archivo' con un error de fila descarga el resultado y avisa que no
se grabó nada` del spec. **Límite de este caso**: el spec no consulta Dataverse, así que "no se grabó
nada" lo afirma el texto del diálogo, no un conteo de registros; el conteo en Dataverse lo da L-45
(rollback) y, para este archivo en concreto, hay que consultar `sanic_mppp_tbl_clientes` por el CIF
del archivo.

**Cómo se sabe que falló.** No hay descarga y el diálogo muestra un error técnico: la Custom API reventó
en vez de devolver el resultado. Hay descarga pero el diálogo dice "Carga terminada": se grabó con un
error en el archivo.

**Automatizable.** GUI — sesión `admin_planes`. Ya construido en
`pruebas/gui/casos/dn-04-catalogos-masivos.spec.mjs`.

---

### L-44 — Las filas sin error dicen "Sin errores — no se grabó: el archivo tiene errores en otras filas"

**Qué prueba.** Cuando el archivo tiene errores, las filas **sin** error no dicen "Creado" (no se creó
nada), sino `Sin errores — no se grabó: el archivo tiene errores en otras filas`. Es el texto que
evita que el usuario crea que sus filas buenas ya quedaron.

**Necesita.** Un archivo de cuatro filas, tres válidas (que crearían un cliente, un plan y un correo
cada una) y una con error. Sesión del Administrador de planes.

**Cómo.** Subir el archivo a `cargarcatalogos`. Repetir con un archivo de `ActivarDesactivar` de cuatro
filas con una con error.

**Qué tiene que pasar.** La fila con error: `Error: ...`; las tres sanas: `Sin errores — no se grabó: el
archivo tiene errores en otras filas`; `grabado = false`; los contadores de creados en 0. Lo mismo para
el cambio de estado, con `activados`, `desactivados` y `sincambios` en 0.

**Cómo se verifica.** La columna L (o G) del `resultado`; los contadores; y `GET
sanic_mppp_tbl_clientes?$filter=startswith(sanic_cifbac,'<prefijo>')&$count=true` = 0.

**Cómo se sabe que falló.** Las filas sanas dicen "Creado": el usuario cree que se grabaron y no se grabó
nada. O dicen "Sin cambios": se confunden con una fila que ya existía.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CargaDeCatalogosAceptacion.cs`: `Con_un_error_en_cualquier_fila_no_se_crea_nada_y_las_filas_sanas_lo_dicen`;
`CambioDeEstadoAceptacion.cs`: `Con_un_error_en_cualquier_fila_no_cambia_nada`,
`Una_fila_reporta_todos_sus_errores_y_el_texto_respeta_el_orden`;
`CatalogosMasivosApiAceptacion.cs`: `Con_errores_de_fila_devuelve_el_resultado_sin_escribir_nada`).

---

### L-45 — Si una escritura falla en la pasada 2, la transacción deja cero registros

**Qué prueba.** DN-04, "Todo o nada en dos pasadas": la pasada 2 corre dentro de la transacción de la
Custom API; si una escritura falla, la excepción deshace todo. Verificado en Dev el 2026-10-01
(D-72): `IsInTransaction = true` en la etapa 30, y una excepción deja cero registros, con el
servicio del usuario y con SYSTEM.

**Necesita.** Una carga que falle **a mitad de la pasada 2**: hoy, la forma comprobada es el corte de la
base a los ~40 s con un archivo grande (200 o 300 filas, con un tope de prueba más alto: ver el riesgo de
L-17 y L-20). Sesión real del Administrador de planes.

**Cómo (solo si se decide repetir).** Con una versión de prueba de `carga.filas.maximo`, subir un
archivo de 300 filas peor caso; después contar clientes, planes, correos y autorizaciones del rango.

**Qué tiene que pasar.** La llamada falla, y el conteo de los cuatro tipos de registro es cero.

**Cómo se verifica.** `dataverse_api.py GET
"sanic_mppp_tbl_clientes?$filter=startswith(sanic_cifbac,'<prefijo>')&$count=true"`, igual para los otros tres.

**Cómo se sabe que falló.** Queda alguno de los registros de las filas que ya se habían grabado: se
perdió la transacción (se atrapó un error y se siguió, que es el camino que Learn desaconseja y que
el spike D-72 no usó).

**Automatizable.** API. Es el caso que mide D-75; no se repite de rutina por el riesgo de L-17.

---

## Grupo 6 — Idempotencia, volumen y orden de escritura

### L-46 — Re-subir el mismo archivo no crea nada ("Sin cambios")

**Qué prueba.** DN-04, "Lo que NO es error": una fila donde todo ya existía queda "Sin cambios: todo ya
existía"; el archivo se puede volver a subir sin efectos (carga idempotente). Si una carga se cortó, se
repite entera.

**Necesita.** Un archivo de carga sin errores que cree un cliente, un plan, un correo y una autorización
por fila, ya cargado una vez por el propio caso (con los registros creados bajo la marca `ZZP-`).

**Cómo.**
1. Subir el archivo la primera vez y anotar los contadores.
2. Subir **el mismo archivo** otra vez.
3. Subirlo una tercera vez con una fila más (la nueva sí se crea).

**Qué tiene que pasar.** Paso 2: `grabado = true`, `clientescreados = 0`, `planescreados = 0`,
`correoscreados = 0`, `autorizacionescreadas = 0`, y la columna L de cada fila dice "Sin cambios: todo
ya existía...". Paso 3: se crea solo lo de la fila nueva.

**Cómo se verifica.** Los contadores de la respuesta y la columna L del `resultado`; `GET
sanic_mppp_tbl_clientes?$filter=startswith(sanic_cifbac,'<prefijo>')&$count=true` igual en los pasos 1 y 2.

**Cómo se sabe que falló.** Aparecen registros duplicados (la clave alterna del cliente o del correo
rechaza, y el usuario ve `MPPP-CAR-009`); o el segundo envío da errores de fila contra los registros
que acaba de crear él mismo.

**Automatizable.** API — sesión real del Administrador de planes.

---

### L-47 — Un archivo desnormalizado crea cada cosa una sola vez, y escribe en orden de dependencias

**Qué prueba.** El mismo cliente y el mismo correo se repiten en varias filas (una por plan) y se crean
**una sola vez**; la fila de menor número es la que "crea" lo que aparece en varias. El orden de
escritura es Clientes → Planes → Correos → Autorizaciones, sin importar el orden de las filas del
archivo.

**Necesita.** Un archivo de 6 filas: un cliente nuevo con 3 planes nuevos y 2 correos (cada correo
autorizado sobre ese cliente), repartidos en filas en desorden (la autorización antes que el plan del
mismo cliente en el archivo).

**Cómo.** Subir el archivo y contar.

**Qué tiene que pasar.** `clientescreados = 1`, `planescreados = 3`, `correoscreados = 2`,
`autorizacionescreadas = 2`; la fila 2 dice "Creado: cliente, plan, correo, autorización" y las demás
solo lo que ellas crean; en Dataverse, un cliente, tres planes, dos correos y dos autorizaciones.

**Cómo se verifica.** Los contadores y las consultas por el CIF del cliente.

**Cómo se sabe que falló.** Se crean dos clientes con el mismo CIF o el segundo plan apunta a un cliente
que todavía no existía (el orden de dependencias se rompió).

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CargaDeCatalogosAceptacion.cs`: `Archivo_desnormalizado_tipico_crea_cada_cosa_una_sola_vez_y_la_atribuye_a_la_primera_fila`,
`Los_resultados_salen_en_el_orden_recibido_y_lo_crea_la_fila_de_menor_numero`;
`CatalogosMasivosAceptacion.cs`: `El_escritor_crea_en_orden_con_las_columnas_exactas_y_resuelve_las_referencias_a_lo_nuevo`).

---

### L-48 — 100 filas, el peor caso, se graban dentro del presupuesto de 30 s

**Qué prueba.** El tope de 100 filas (D-75) se sostiene en Dev: un archivo de 100 filas en el **peor caso**
(cada fila crea cliente, plan, correo y autorización: 4 escrituras) se graba completo, y por debajo
del presupuesto de escritura de 30 s.

**Necesita.** `carga.filas.maximo = 100`; sesión real del Administrador de planes; un archivo de 100
filas peor caso con CIF, códigos y correos del rango reservado del set.

**Cómo.** Subir el archivo a `cargarcatalogos` y medir el tiempo de la llamada.

**Qué tiene que pasar.** `grabado = true`; 100 clientes, 100 planes, 100 correos y 100 autorizaciones
creados; tiempo por debajo de 30 s.

**Cómo se verifica.** Los contadores y la medición del tiempo de la llamada; el conteo por rango de CIF.

**Cómo se sabe que falló.** `MPPP-CAR-008`: el tiempo pasó de 30 s (con 25,1 s medidos, el margen contra
el presupuesto propio es de unos 5 s, y una plataforma más lenta lo puede cruzar: se anota como
riesgo, no como defecto). `MPPP-CAR-011`: la base cortó la transacción (10928).

**Automatizable.** API — sesión real del Administrador de planes. Tarda unos 25 s y escribe 400
registros: es un caso caro; se corre cuando cambia el código de la carga.

---

## Grupo 7 — Dueño del cliente nuevo y permisos (D-73)

### L-49 — El cliente nuevo queda con el ejecutivo como dueño; el que cargó queda como creador

**Qué prueba.** DN-04 "Decisiones" 3 y "Mecanismo": un cliente nuevo no lleva una columna nueva de
ejecutivo; se resuelve a `ownerid` (RF-19, D-23), que **ya es** el ejecutivo asignado. La Custom API
escribe con el usuario que llama (no con SYSTEM), así que `createdby` es el Administrador de planes y
queda quién dio de alta cada cosa (DD-18). D-73 pedía comprobar que el Administrador de planes puede
crear un Cliente con `ownerid` de OTRO usuario (privilegio Assign).

**Necesita.** Sesión del Administrador de planes (`# User 3`) y el correo de `# User 1` (Ejecutivo); un
archivo con clientes nuevos con el Correo ejecutivo de `# User 1`.

**Cómo.** Subir el archivo y consultar cada cliente creado.

**Qué tiene que pasar.** Cada cliente nuevo tiene `_ownerid_value` = `# User 1` y `_createdby_value` =
`# User 3`.

**Cómo se verifica.** `dataverse_api.py GET
"sanic_mppp_tbl_clientes?$filter=startswith(sanic_cifbac,'<prefijo>')&$select=sanic_nombre,_ownerid_value,_createdby_value"`.

**Cómo se sabe que falló.** `_ownerid_value` es `# User 3`: el cliente quedó a nombre de quien cargó y
no aparece en "Mis clientes" del ejecutivo. `_createdby_value` es `SYSTEM`: la API escribió con el
servicio equivocado y se perdió la auditoría de quién lo dio de alta.

**Automatizable.** API — sesión real del Administrador de planes (el sistema de permisos de Dataverse es
el que se prueba: un doble no sirve).

---

### L-50 — El Ejecutivo dueño ve el cliente nuevo y sus autorizaciones

**Qué prueba.** D-73: que el Ejecutivo dueño ve el cliente cargado por el Administrador de planes, y las
autorizaciones de ese cliente.

**Necesita.** Los clientes creados por L-49 con dueño `# User 1`, y `local/sesion-ejecutivo.json`.
Verificar con L-02 que la sesión del Ejecutivo está viva.

**Cómo.** Como `# User 1` (sesión real o `MSCRMCallerID`): consultar los clientes del rango y sus
autorizaciones por cliente.

**Qué tiene que pasar.** El Ejecutivo ve cada cliente nuevo y las autorizaciones que cuelgan de él.

**Cómo se verifica.** `GET sanic_mppp_tbl_clientes?$filter=startswith(sanic_cifbac,'<prefijo>')&$count=true`
y `GET sanic_mppp_tbl_autorizacionclientes?$filter=_sanic_clienteid_value eq <id>` con
`MSCRMCallerID` de `# User 1`.

**Cómo se sabe que falló.** El conteo del Ejecutivo da cero mientras el del Administrador de planes da
cien: un cliente a nombre de alguien que no lo ve (el defecto que la regla 4 de la carga quiere evitar).
Cuidado con el falso verde de la sesión muerta: la guarda de L-02 aplica acá también.

**Automatizable.** API — impersonación de `# User 1`.

---

### L-51 — La puerta de las dos Custom API: solo el Administrador de planes entra

**Qué prueba.** DN-04, "Contratos": `ExecutePrivilegeName` es `prvCreatesanic_mppp_tbl_cliente` para la
carga y `prvWritesanic_mppp_tbl_cliente` para el cambio de estado, y solo los tiene el Administrador de
planes (`04` §2). **El privilegio es solo la puerta**: cada `Create` y cada `Update` de la pasada 2
vuelve a pasar por la seguridad de la plataforma con el usuario que llama.

**Necesita.** Ejecutivo (`# User 1`), Supervisor (`# User 2`) y Administrador técnico (`# User 4`), cada
uno resuelto por alias y con su rol **verificado** en `systemuserroles_association` (no supuesto); un
archivo válido.

**Cómo.** Con `MSCRMCallerID` de cada uno de los tres usuarios, llamar a `cargarcatalogos` y a
`cambiarestadocatalogos` con `{"plantilla": true}` (la llamada más barata que igual pasa por la
puerta), y una vez con un archivo válido.

**Qué tiene que pasar.** Las seis llamadas se rechazan por falta de privilegio, con error de plataforma
(no un `MPPP-CAR-0xx`), **antes** de leer el archivo.

**Cómo se verifica.** El código HTTP (403) y el mensaje de la plataforma.

**Cómo se sabe que falló.** Alguno de los tres entra: el registro real de la Custom API quedó con otro
privilegio, o el rol del usuario cambió (la lección del 2026-09-24: verificar el rol primero). Un
rechazo con `MPPP-CAR-0xx` quiere decir que la llamada pasó la puerta y el rechazo lo hizo el plugin.

**Automatizable.** API — impersonación por `MSCRMCallerID`, mismo mecanismo que `E-09`.

---

## Grupo 8 — Comentarios en la autorización por cliente

### L-52 — La columna Comentarios existe, en el formulario y en las dos vistas

**Qué prueba.** DN-04, "Columna nueva": `sanic_mppp_tbl_autorizacioncliente.sanic_comentarios` es `T(50)`,
opcional, con nombre visible "Comentarios" en 1033 (`01` §0: las etiquetas de metadatos van en 1033 y la plataforma las usa si al destino le falta su idioma; *corregido el 2026-10-02*, decía "y en 3082").
Va al formulario **Autorizacion por cliente**, en la sección de la evidencia, **después de la fecha del
documento**, y a las vistas "Autorizaciones por cliente activas" y "Correos autorizados sobre este
cliente".

**Necesita.** `local/sesion-admin_planes.json`; una autorización por cliente con un comentario
(`ZZP-comentario`) creada por el set.

**Cómo.**
1. `GET EntityDefinitions(LogicalName='sanic_mppp_tbl_autorizacioncliente')/Attributes(LogicalName='sanic_comentarios')?$select=MaxLength,RequiredLevel,DisplayName`.
2. Abrir el formulario de la autorización y ubicar el campo.
3. Abrir las dos vistas y buscar la columna.

**Qué tiene que pasar.** `MaxLength = 50`, `RequiredLevel = None`, etiqueta "Comentarios" en 1033; el campo está en la sección de la evidencia, debajo de la fecha del documento; la columna aparece
en las dos vistas.

**Cómo se verifica.** La consulta de metadata del paso 1 y la inspección del formulario y de las vistas
(`FormXml` / `layoutxml`, o con Playwright como el resto de los casos de `06-seguridad-vistas-formularios.md`).
La muestra exportada de la columna se verificó estructuralmente el 2026-10-01 (`diseno/06`, 3.13),
pero eso comprueba que la columna existe, no que se vea donde el diseño dice.

**Cómo se sabe que falló.** La columna existe pero no aparece en el formulario (falta publicar) o aparece
en otra sección: cambió el `formxml` sin pasar por el diseño.

**Automatizable.** API (metadata) y GUI (formulario y vistas) — sesión `admin_planes`.

---

### L-53 — Comentarios solo en `AutorizacionCliente`, no en `AutorizacionPlan`; y auditada

**Qué prueba.** Decisión 6: los comentarios van solo en la autorización por cliente, no en la de plan. La
columna es **auditada** (DD-18).

**Necesita.** El service principal para consultar metadata.

**Cómo.**
1. `GET EntityDefinitions(LogicalName='sanic_mppp_tbl_autorizacionplan')/Attributes?$filter=LogicalName eq 'sanic_comentarios'`.
2. `GET EntityDefinitions(LogicalName='sanic_mppp_tbl_autorizacioncliente')/Attributes(LogicalName='sanic_comentarios')?$select=IsAuditEnabled`.

**Qué tiene que pasar.** Paso 1: ninguna columna. Paso 2: `IsAuditEnabled` con valor `true`.

**Cómo se verifica.** Las dos consultas de metadata.

**Cómo se sabe que falló.** Paso 1 devuelve la columna: se agregó a `AutorizacionPlan` sin decisión del
aprobador. Paso 2 en `false`: un cambio de comentario no deja huella de quién lo hizo.

**Automatizable.** API.

---

### L-54 — Comentarios: hasta 50 caracteres por Excel; 51 es error de fila

**Qué prueba.** Con la columna K del Excel de carga: 50 caracteres valen; 51 es error de esa fila (en la
pasada 1, nombrando la columna).

**Necesita.** Un cliente existente y un correo nuevo con Nombre contacto; dos filas con un comentario de
50 y de 51 caracteres.

**Cómo.** Subir el archivo con las dos filas a `cargarcatalogos`.

**Qué tiene que pasar.** La fila de 50: sin error; la de 51: error en Comentarios.

**Cómo se verifica.** La columna L del `resultado`; y, tras una carga sin errores con la de 50, `GET
sanic_mppp_tbl_autorizacionclientes(<id>)?$select=sanic_comentarios`.

**Cómo se sabe que falló.** La de 51 pasa la pasada 1 y revienta en la 2 con `MPPP-CAR-009`: el validador
de la carga y el largo real de la columna no coinciden.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CargaDeCatalogosAceptacion.cs`: `Largos_maximos_de_correo_nombre_y_comentarios`).

---

### L-55 — Comentarios: la plataforma rechaza 51 caracteres también por el formulario y por Web API

**Qué prueba.** Que el límite de 50 no lo aplica solo la carga masiva: `sanic_comentarios` es `T(50)` en la
plataforma, y un comentario de 51 caracteres desde el formulario o por Web API se rechaza. ("El largo se
puede ampliar después; achicar, no.")

**Necesita.** `local/sesion-admin_planes.json`; una autorización por cliente del set.

**Cómo.**
1. En el formulario de la autorización, pegar un texto de 51 caracteres en Comentarios y guardar.
2. `PATCH sanic_mppp_tbl_autorizacionclientes(<id>)` con `{"sanic_comentarios": "<51 caracteres>"}`.
3. Lo mismo con exactamente 50.

**Qué tiene que pasar.** Con 51: el campo no admite más de 50 o el guardado falla con un error de
longitud; con 50: se guarda.

**Cómo se verifica.** El código HTTP del PATCH y el valor guardado.

**Cómo se sabe que falló.** Se guardan 51 caracteres: la columna no es `T(50)` en el entorno.

**Automatizable.** API (PATCH) y GUI (formulario).

---

### L-56 — Los comentarios de la carga quedan guardados en la autorización nueva, y no se pisan en una existente

**Qué prueba.** Regla 14: la autorización nueva se crea con J (fecha) y K (comentarios). Regla 12: si ya
existe activa y el archivo trae otro comentario, error; la carga no lo pisa.

**Necesita.** Un cliente existente; dos correos: uno nuevo y uno ya autorizado sobre ese cliente con
comentario `viejo`.

**Cómo.** Subir dos archivos: (a) el correo nuevo con K = `alta masiva`; (b) el correo ya autorizado con
K = `nuevo`.

**Qué tiene que pasar.** (a): "Creado: correo, autorización"; la autorización tiene `sanic_comentarios =
'alta masiva'`. (b): error: el comentario no coincide con lo grabado; sigue `viejo`.

**Cómo se verifica.** La columna L del `resultado` y `GET
sanic_mppp_tbl_autorizacionclientes?$filter=_sanic_clienteid_value eq <id>&$select=sanic_comentarios`.

**Cómo se sabe que falló.** (b) cambia el comentario a `nuevo`: la carga está modificando lo existente.

**Automatizable.** API — cubierto por pruebas automáticas C#
(`CargaDeCatalogosAceptacion.cs`: `Autorizacion_existente_activa_queda_ya_existia_y_sus_datos_tienen_que_coincidir`,
`Largos_maximos_de_correo_nombre_y_comentarios`; `tests/Sanic.Mppp.Plugins.Tests/Unitarias/Catalogos/PlanificadorDeCargaTests.cs`,
`tests/Sanic.Mppp.Plugins.Tests/Unitarias/Datos/CatalogosMasivosTests.cs`).

---

## Grupo 9 — El parámetro `carga.filas.maximo`

### L-57 — `carga.filas.maximo` vale 100, y sin un valor usable el tope es 100

**Qué prueba.** DN-04, "Parámetro nuevo": `carga.filas.maximo` es un número, **100** (era 300; medido en
Dev el 2026-10-01, D-75), y lo usan las dos Custom API. Si falta, no es un entero o es menor que 1,
se usa un valor quemado de **100** (mismo criterio que `rpa.horas.tolerancia`). El peso del archivo
reutiliza `lectura.limites`. Los parámetros se leen con SYSTEM: el Administrador de planes no tiene
ningún privilegio sobre Parámetro.

**Necesita.** El service principal; el parámetro `carga.filas.maximo` sembrado en Dev con
`sanic_version = 2` y valor `100`.

**Cómo.**
1. `GET sanic_mppp_tbl_parametros?$filter=sanic_nombre eq 'carga.filas.maximo' and statecode eq 0&$select=sanic_version,sanic_valor&$orderby=sanic_version desc&$top=1`.
2. Confirmar que L-15 (101 filas → rechazo) y L-48 (100 filas → grabadas) coinciden con el valor.

**Qué tiene que pasar.** La versión activa más alta vale `100`. Sin versión activa, con un valor `abc` o
con `0`, la Custom API usa 100.

**Cómo se verifica.** La consulta del paso 1. Para el valor por defecto, la suite automática.

**Cómo se sabe que falló.** El parámetro vale 300 (la versión 1 sigue siendo la activa más alta): el tope
de la base de Dev se vuelve a cruzar (D-75). Un valor mal escrito que revienta la llamada en vez de
degradar a 100.

**Automatizable.** API — el valor en Dev se prueba con L-15 y L-48; el valor por defecto está cubierto por
pruebas automáticas C#
(`CatalogosMasivosApiAceptacion.cs`: `Sin_un_carga_filas_maximo_usable_el_tope_es_100`,
`Los_parametros_se_leen_con_SYSTEM_nunca_con_el_usuario`).

---

## Resultados

Estado al **2026-10-02** (los 10 pendientes se corrieron ese día). Los tres estados y lo que significa cada uno están en "Cómo leer el estado de
cada caso", arriba. **Nada de esta tabla es una corrida de `correr.py`**: el set `L` todavía no tiene
`set_l.py`. Ni un solo caso está marcado como EJECUTADO sin evidencia citada.

Evidencia citada en la tabla:

- **[GUI]** = `pruebas/gui/casos/dn-04-catalogos-masivos.spec.mjs`, Playwright, 2026-10-01: tres corridas
  seguidas en verde como Administrador de planes (`MPPP_USUARIO=admin_planes`) y una como Ejecutivo
  (`MPPP_USUARIO=ejecutivo`).
- **[Dev 1.0.22.0]** = pruebas con la sesión real del Administrador de planes (`# User 3`) contra la
  Custom API en Dev, 2026-10-01, ensamblado 1.0.22.0 (`diseno/PENDIENTES.md`, D-73 y D-75).
- **[GUI 2026-10-02]** = el mismo spec, extendido con L-03, L-04, L-08, L-09 y el formulario de L-55: dos
  corridas seguidas en verde como Administrador de planes (14 de 14), una como Supervisor (4 de 4), una
  como Ejecutivo (4 de 4) y una como Administrador técnico (4 saltados por motivo verificado: su
  sitemap no tiene el área Catalogos). Datos propios (`pruebas/gui/datos_dn04_l09.py`, `--sembrar` y
  `--limpiar`), limpios al terminar.
- **[Dev API 2026-10-02]** = llamadas con las sesiones reales de los cuatro usuarios (verificados con
  `WhoAmI` y su rol en `systemuserroles`) y consultas de metadatos con el service principal, ensamblado
  1.0.22.0. Datos `ZZP-` sembrados con la propia carga masiva y borrados al terminar.
- **[Dev 1.0.21.0]** = la misma sesión, ensamblado 1.0.21.0, con el presupuesto todavía en 100 s
  (D-75).
- **[C#]** = suite de pruebas automáticas de C# en `tests/Sanic.Mppp.Plugins.Tests` (1580 en verde al
  2026-10-01): cubre, **sin correr en Dev**, lo que cada caso cita.

| Caso | Estado | Evidencia / qué falta |
|---|---|---|
| L-01 los dos menús en las cuatro grillas (Administrador de planes) | EJECUTADO — OK | [GUI]: las cuatro pruebas `<grilla>: los dos menús están...`, 3 corridas seguidas en verde |
| L-02 el Ejecutivo no ve los menús | EJECUTADO — OK | [GUI]: las cuatro pruebas `un Ejecutivo no ve los menús...`, 1 corrida en verde |
| L-03 Supervisor y Administrador técnico no los ven | EJECUTADO — OK | [GUI 2026-10-02]: Supervisor, sin menús en las 4 grillas. Administrador técnico: las 4 saltadas porque su sitemap no tiene el área Catalogos (coincide con la matriz de `04`) |
| L-04 texto fijo en español, también con la interfaz en inglés | EJECUTADO — OK (1033); 3082 N-A | [GUI 2026-10-02]: con la interfaz en inglés (el botón nativo dice "New"), ancla y opciones dicen EXACTAMENTE "Carga masiva", "Activar/Desactivar masivo", "Cargar archivo" y "Descargar plantilla". La mitad en 3082 es N-A: Dev solo tiene 1033 instalado (`RetrieveProvisionedLanguages`, 2026-10-02) |
| L-05 plantilla `CargaCatalogos.xlsx` | EJECUTADO — OK | [GUI]: nombre del archivo, 12 encabezados con Resultado en L, sin filas |
| L-06 plantilla `ActivarDesactivar.xlsx` | EJECUTADO — OK | [GUI]: nombre del archivo, 7 encabezados con Resultado en G, sin filas |
| L-07 plantilla = resultado = archivo corregido | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `ExcelDeCatalogosAceptacion.cs`. No se re-subió un resultado corregido en Dev |
| L-08 cancelar el selector y extensión distinta de `.xlsx` | EJECUTADO — OK | [GUI 2026-10-02]: el selector se abre con el clic; sin archivo (`setFiles([])`, que dispara `change` y no `cancel`) no hay indicador, diálogo ni llamada; con un `.txt`, el aviso exacto del JS y cero llamadas a las dos Custom API |
| L-09 indicador por etapas, resumen de carga grabada y refresco de la grilla | EJECUTADO — OK | [GUI 2026-10-02]: los tres `show` en orden (espía) y "Validando…" visible; descarga del resultado; "Load finished: 1 clients, 1 plans, 1 emails and 1 authorizations created. 1 rows with warnings: check the result file."; el cliente nuevo aparece en la grilla sin recargar; cambio de estado: "Status change finished: 0 activated, 1 deactivated and 1 unchanged. 1 rows with warnings: check the result file." |
| L-10 `MPPP-CAR-001` | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CatalogosMasivosApiAceptacion.cs` |
| L-11 `MPPP-CAR-002` | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CatalogosMasivosApiAceptacion.cs` |
| L-12 `MPPP-CAR-003` | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CatalogosMasivosApiAceptacion.cs` |
| L-13 `MPPP-CAR-004`, sin descarga | EJECUTADO — OK | [GUI]: `'Cargar archivo' con la hoja equivocada muestra MPPP-CAR-004 y no descarga nada` |
| L-14 `MPPP-CAR-005` | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] solo (a) encabezado distinto y (b) columna de más; **(c) columnas intercambiadas y (d) mayúsculas/espacios no tienen prueba de aceptación propia**: se corren en Dev |
| L-15 `MPPP-CAR-006`, 101 filas | EJECUTADO — OK | [Dev 1.0.22.0]: 101 filas → `MPPP-CAR-006` sin escribir (D-75) |
| L-16 `MPPP-CAR-007` | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CatalogosMasivosApiAceptacion.cs` |
| L-17 `MPPP-CAR-008` por presupuesto de 30 s | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CatalogosMasivosApiAceptacion.cs` y `CatalogosMasivosAceptacion.cs`. En Dev nunca se disparó: el peor caso de 100 filas tarda 25,1 s |
| L-18 `MPPP-CAR-009` por error de dato al grabar | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CatalogosMasivosApiAceptacion.cs`. No se provocó en Dev |
| L-19 `MPPP-CAR-010` | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CatalogosMasivosApiAceptacion.cs` |
| L-20 `MPPP-CAR-011` por falla de plataforma, sin nombrar fila | EJECUTADO — OK | [Dev 1.0.21.0]: 200 filas fallaron a los 42 s, el usuario recibió `MPPP-CAR-011` y la traza dijo `Fila 66 (código 0x80072324)`; además [C#]. No se repitió con 1.0.22.0: con el tope en 100 ya no se alcanza por volumen |
| L-21 ningún error de archivo muestra el texto de la librería | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CatalogosMasivosApiAceptacion.cs` |
| L-22 reglas 1 y 2 (cliente y coherencia de CIF) | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CargaDeCatalogosAceptacion.cs` |
| L-23 regla 3 (cliente existente) | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CargaDeCatalogosAceptacion.cs` |
| L-24 regla 4 (cliente nuevo y ejecutivo) | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CargaDeCatalogosAceptacion.cs`, `CatalogosMasivosAceptacion.cs`. En Dev, solo se probó el ejecutivo válido (L-49); los motivos de error de usuario no se corrieron |
| L-25 reglas 5 y 7 (plan: datos sin código, plan nuevo) | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CargaDeCatalogosAceptacion.cs` |
| L-26 regla 6 (plan existente) | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CargaDeCatalogosAceptacion.cs` |
| L-27 reglas 8 y 9 (correo sin datos, formato) | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CargaDeCatalogosAceptacion.cs` |
| L-28 reglas 10 y 11 (correo existente o nuevo) | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CargaDeCatalogosAceptacion.cs` |
| L-29 reglas 12, 13 y 14 (autorización) | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CargaDeCatalogosAceptacion.cs`. La vista "sin evidencia" no se miró en Dev |
| L-30 regla 15 (coherencia dentro del archivo) | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CargaDeCatalogosAceptacion.cs` |
| L-31 regla 16 (un CIF con dos CIF) | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CargaDeCatalogosAceptacion.cs` |
| L-32 lectura de celdas | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CatalogosMasivosApiAceptacion.cs`, `CargaDeCatalogosAceptacion.cs` |
| L-33 Fecha documento | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CargaDeCatalogosAceptacion.cs`, `CatalogosMasivosApiAceptacion.cs` |
| L-34 todos los errores de la fila y valores acortados | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CargaDeCatalogosAceptacion.cs`, `ExcelDeCatalogosAceptacion.cs` |
| L-35 Excel 2, regla 1 (listas) | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CambioDeEstadoAceptacion.cs` |
| L-36 Excel 2, regla 2 (claves exigidas) | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CambioDeEstadoAceptacion.cs` |
| L-37 Excel 2, regla 3 (existir) | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CambioDeEstadoAceptacion.cs` |
| L-38 Excel 2, regla 4 (sin cambios) | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CambioDeEstadoAceptacion.cs` |
| L-39 Excel 2, regla 5 (opuestas y repetidas) | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CambioDeEstadoAceptacion.cs` |
| L-40 Excel 2, regla 6 (advertencias de efecto) | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CambioDeEstadoAceptacion.cs` |
| L-41 Excel 2, regla 7 y sin cascada | EJECUTADO — OK | [Dev API 2026-10-02]: desactivar `ZZP-Cliente 1` lo dejó inactivo y su plan y sus dos autorizaciones siguieron activos, con la advertencia "deja sin efecto 2 autorizaciones"; `modifiedby` y la auditoría (un `Update` de `# User 3`, no de SYSTEM) registran al usuario real. Ojo: la auditoría apareció con unos minutos de demora (Learn: los logs viven en el almacenamiento de logs) |
| L-42 el ejemplo del aprobador de punta a punta | EJECUTADO — OK | [Dev API 2026-10-02]: las seis filas sobre 3 clientes, 1 plan y 2 correos con autorizaciones cruzadas: 2 activados, 4 desactivados, 0 sin cambios; el plan solo, el correo de Juan inactivo con sus dos autorizaciones intactas ("deja sin efecto 2"), solo maria@1 desactivada (María sigue sobre el Cliente 2), juan@3 activada con la advertencia de correo inactivo. El texto del resumen en la interfaz lo cubre L-09 |
| L-43 error de fila: no se graba, se descarga el resultado, "No se grabó nada" | EJECUTADO — OK | [GUI]: `'Cargar archivo' con un error de fila descarga el resultado y avisa que no se grabó nada`. Límite: el spec no cuenta registros en Dataverse |
| L-44 filas sanas: "Sin errores — no se grabó" | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CargaDeCatalogosAceptacion.cs`, `CambioDeEstadoAceptacion.cs`. El archivo del GUI tiene una sola fila: no ejercita este texto |
| L-45 rollback: cero registros tras una falla de la pasada 2 | EJECUTADO — OK | D-72 (`spikes/d72-transaccion-capi/CONCLUSIONES.md`: `IsInTransaction = true` y una excepción deja cero registros) y la medición de D-75 ("El todo o nada funcionó en las cuatro (cero registros)", 200 y 300 filas) |
| L-46 re-subir el mismo archivo: "Sin cambios" | EJECUTADO — OK | [Dev 1.0.22.0]: re-subir las 100 filas → 0 creados en 0,6 s. El texto por fila ("Sin cambios: todo ya existía...") lo cubre [C#] `CargaDeCatalogosAceptacion.cs`; no se capturó la columna Resultado de esa corrida |
| L-47 desnormalizado: una sola vez, en orden de dependencias | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CargaDeCatalogosAceptacion.cs`, `CatalogosMasivosAceptacion.cs`. La corrida de Dev de 100 filas no era desnormalizada (cada fila creaba todo) |
| L-48 100 filas peor caso dentro del presupuesto | EJECUTADO — OK | [Dev 1.0.22.0]: 100 filas que crean cliente, plan, correo y autorización, grabadas en 25,1 s. Margen contra los 30 s: unos 5 s |
| L-49 dueño = el ejecutivo; creador = el Administrador de planes | EJECUTADO — OK | [Dev 1.0.22.0]: 100 de 100 clientes con dueño "# User 1" (Ejecutiva) y creador "# User 3" (Administrador de planes) (D-73) |
| L-50 el Ejecutivo dueño ve cliente y autorizaciones | EJECUTADO — OK | [Dev 1.0.22.0]: la Ejecutiva los ve, a ellos y a sus autorizaciones (D-73) |
| L-51 la puerta de las dos Custom API | EJECUTADO — OK | [Dev API 2026-10-02]: 12 de 12 llamadas (3 usuarios × 2 API × plantilla y archivo) rechazadas con 403 `0x80040220` de la plataforma, sin `MPPP-CAR`; el privilegio que falta es exactamente `prvCreatesanic_mppp_tbl_cliente` (carga) y `prvWritesanic_mppp_tbl_cliente` (cambio de estado). Los archivos traían un error de fila: aunque la puerta fallara, no podían escribir |
| L-52 la columna Comentarios en formulario y vistas | EJECUTADO — OK | [Dev API 2026-10-02]: `MaxLength` 50, `RequiredLevel` None, "Comentarios" en 1033; en el `formxml` de "Autorizacion por cliente", sección "Evidencia de la autorizacion", justo después de `sanic_fechadocumento`; en el `layoutxml` de las dos vistas |
| L-53 solo en `AutorizacionCliente`, auditada | EJECUTADO — OK | [Dev API 2026-10-02]: `AutorizacionPlan` no tiene la columna; en `AutorizacionCliente` es auditada, con la auditoría encendida en la tabla y en la organización |
| L-54 Comentarios: 50 por Excel | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CargaDeCatalogosAceptacion.cs` |
| L-55 Comentarios: 51 rechazado por formulario y Web API | EJECUTADO — OK | [Dev API 2026-10-02]: PATCH con 51 → 400 `0x80044331` ("exceeded the maximum allowed length of '50'"); con 50 → 204 y se guardan 50. [GUI 2026-10-02]: en el formulario, tipear 51 deja 50 en el campo y Ctrl+S guarda 50 |
| L-56 los comentarios de la carga se guardan y no se pisan | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CargaDeCatalogosAceptacion.cs`, `PlanificadorDeCargaTests.cs`, `CatalogosMasivosTests.cs` |
| L-57 `carga.filas.maximo` = 100 y valor por defecto 100 | CUBIERTO POR PRUEBAS AUTOMÁTICAS | [C#] `CatalogosMasivosApiAceptacion.cs` (valor por defecto). El valor 100 en Dev es la base de L-15 y L-48, que sí están ejecutados |

### Cuenta por grupo

| Grupo | Casos | EJECUTADO — OK | CUBIERTO POR PRUEBAS AUTOMÁTICAS | PENDIENTE |
|---|---|---|---|---|
| 1 — Menús, plantillas y flujo del navegador (L-01 a L-09) | 9 | 8 | 1 | 0 |
| 2 — Códigos de archivo `MPPP-CAR-001` a `011` (L-10 a L-21) | 12 | 3 | 9 | 0 |
| 3 — Excel 1: reglas por fila (L-22 a L-34) | 13 | 0 | 13 | 0 |
| 4 — Excel 2: reglas por fila (L-35 a L-42) | 8 | 2 | 6 | 0 |
| 5 — Todo o nada (L-43 a L-45) | 3 | 2 | 1 | 0 |
| 6 — Idempotencia, volumen y orden (L-46 a L-48) | 3 | 2 | 1 | 0 |
| 7 — Dueño y permisos (L-49 a L-51) | 3 | 3 | 0 | 0 |
| 8 — Comentarios (L-52 a L-56) | 5 | 3 | 2 | 0 |
| 9 — Parámetro (L-57) | 1 | 0 | 1 | 0 |
| **Total** | **57** | **23** | **34** | **0** |
