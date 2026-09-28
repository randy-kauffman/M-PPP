# 13 — Lo construido el 2026-09-27: sitemap, vistas, formulario y la lección del ribbon

Cubre los hallazgos y correcciones del propio 2026-09-27 que todavía no tenían un caso de prueba:
la entrada de sitemap nueva `subarea_paraatencionmanual`, los renombres y la entrada nueva del grupo
Catálogos, la columna `sanic_requiereatencionmanual` en las vistas de trabajo, el formulario nuevo de
`AutorizacionCliente`, la tabla `sanic_mppp_tbl_autorizacioncliente` como componente real de la app
(no solo del sitemap), y el diagnóstico correcto de un botón que existe en el servidor pero no se
dibuja. Fuente: `diseno/05-app-model-driven.md` §2-§3, `diseno/06-inventario-componentes.md`
12.2/12.7/12.8, `playbooks/app/mantenimientoppp.md`, los playbooks de `playbooks/vista/` y
`playbooks/formulario/autorizacioncliente.md`, y `pruebas/gui/README.md` +
`pruebas/gui/ayudantes/ribbon.mjs` (solo lectura, no se tocan).

Convención de IDs: prefijo `H-` (**H**oy), ver `CONVENCIONES.md` §4.

**Corrección de este set contra `CONVENCIONES.md`** (§1, §2, §7, §8, versión anterior de este
archivo): H-01 a H-05 estaban marcadas `Automatizable: GUI` (inspección visual con sesión de
navegador). Eso estaba mal — el sitemap, el `layoutxml`/`fetchxml` de una vista y el `formxml` de un
formulario son metadata **publicada**, legible por Web API con el mismo service principal que usa
cualquier otro set (mismo criterio que ya aplicó el set U para lo que el diseño original dejaba como
"GUI, sesión admin_planes" en `10-autorizacion-cliente.md`). Se corrigen acá a `API`, contra la
plataforma en vivo, no contra el ojo de quien mira ni contra la salida de ninguna herramienta
(§2: "la verificación va contra la plataforma, no contra la salida del constructor"). La única
excepción real es **H-06**: es la lección del día, y su punto central es que ninguna prueba de API la
detecta — se deja `N-A` (Manual) a propósito, con el comando exacto del arnés que sí mira el DOM real.

Se agrega **H-07**, caso nuevo: la tabla `sanic_mppp_tbl_autorizacioncliente` como componente de la
app se resolvió con un **import manual de la solución** (no con la herramienta del proyecto en modo
escritura), así que merece su propio caso verificado en vivo contra `RetrieveAppComponents` —
**nombrada, no contada** (§7): no se afirma un total de tablas (ese número puede cambiar sin que nada
esté roto, es el mismo error que ya costó caro con "12 parámetros activos" y "13 entradas de
sitemap"), se afirma que ESTA tabla puntual aparece.

Este set **no crea ningún dato**: es lectura de metadata ya publicada. No hay ninguna Solicitud, Fila,
Cliente ni Autorizado que sembrar, y por lo tanto nada que `cerrar()` tenga que borrar con marca
`ZZP-`.

## Preparativos comunes a este archivo

- Ninguna sesión de navegador para H-01 a H-05 y H-07: se verifican contra Web API directo, con el
  service principal `# PowerPlatform-Dev` (System Administrator), igual que cualquier otro set de
  `herramientas/pruebas/sets/`.
- Solo **H-06** necesita sesión real y acceso de plataforma — ver su campo `Necesita`.

---

### H-01 — Sitemap: "Para atención manual" en el área Trabajo, en su posición exacta

**Qué prueba.** El olvido corregido el 2026-09-27: la vista "Para atencion manual" existía desde
fase 2 pero no tenía entrada propia en el sitemap. Ahora `subarea_paraatencionmanual` está,
EXACTAMENTE, entre `subarea_poraprobar` y `subarea_porclasificar` dentro del área con título 1033
"Trabajo", con sus títulos en 1033 y 3082, y abre la vista real "Para atención manual" (no una vista
distinta ni una `Url` inventada).

**Necesita.** Nada del navegador: conexión a Dataverse por Web API (el mismo service principal que
usan los demás sets). Ningún dato previo — es lectura de metadata ya publicada.

**Cómo.** `GET sitemaps?$select=sitemapxml&$filter=sitemapnameunique eq 'sanic_mppp_sm_mantenimientoppp'`;
parsear el XML devuelto; ubicar el `Area` cuyo `Titles/Title[@LCID='1033']` sea "Trabajo"; leer, en
orden de documento, los `Id` de sus `SubArea`; ubicar `subarea_paraatencionmanual` y confirmar que su
`Url` apunta al `savedqueryid` real de la vista "Para atencion manual" sobre `sanic_mppp_tbl_fila`
(`GET savedqueries?$select=savedqueryid&$filter=name eq 'Para atencion manual' and returnedtypecode eq 'sanic_mppp_tbl_fila'`).

**Qué tiene que pasar.** `subarea_paraatencionmanual` aparece consecutiva entre `subarea_poraprobar` y
`subarea_porclasificar` (sin ninguna otra entrada en el medio), con título 1033 y 3082 = "Para
atencion manual", y su `Url` contiene `etn=sanic_mppp_tbl_fila` y el `savedqueryid` real de esa vista.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py H` (caso H-01) — las dos consultas
`GET` de arriba, comparadas en código, no a mano.

**Cómo se sabe que falló.** Alguna de estas, todas objetivas y distinguibles entre sí: la entrada no
existe (regresión del arreglo); existe pero no está pegada entre `poraprobar` y `porclasificar`
(alguien la movió); le falta el título en 1033 o en 3082 (`CONVENCIONES` sobre etiquetas embebidas,
`playbooks/app/mantenimientoppp.md` §"Cada titulo lleva etiqueta por cada idioma"); o su `Url` apunta
a un `savedqueryid` que no es el de "Para atencion manual" (abre otra vista sin que se note a simple
vista).

**Automatizable.** API (Web API directo, sin sesión de navegador; corregido desde `GUI` — ver la nota
de corrección al inicio del archivo).

---

### H-02 — Columna "Requiere atención manual" en las 4 vistas de trabajo, y en ninguna otra

**Qué prueba.** El hallazgo del aprobador: sin esta columna, un ejecutivo no podía ver de un vistazo
si el bot marcó la fila en las vistas donde ya trabaja todos los días. Se agregó a **Mis clientes —
por digitar**, **Todos — por digitar**, **Por aprobar** y **Mis clientes — devueltas** — y a NINGUNA
otra vista de Fila (ni **Filas**, ni **Terminadas**, ni **Tomadas por el bot**).

**Necesita.** Nada del navegador: Web API directo. Ningún dato previo.

**Cómo.** Para cada una de las 7 vistas nombradas arriba:
`GET savedqueries?$select=fetchxml,layoutxml&$filter=name eq '<nombre>' and returnedtypecode eq 'sanic_mppp_tbl_fila'`;
parsear el `layoutxml` (`<row>/<cell name=...>`) y el `fetchxml` (`<entity>/<attribute name=...>`).

**Qué tiene que pasar.** En **Mis clientes - por digitar**, **Todos - por digitar**, **Por aprobar** y
**Mis clientes - devueltas**: el `layoutxml` trae una `<cell name="sanic_requiereatencionmanual">`
inmediatamente después de `sanic_numerofila`, y el `fetchxml` trae su `<attribute>`. En **Filas**,
**Terminadas** y **Tomadas por el bot**: la celda NO está.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py H` (caso H-02).

**Cómo se sabe que falló.** Falta la celda o el atributo en alguna de las 4 vistas de trabajo (el
ejecutivo sigue sin ver la marca donde más la necesita); está pero no pegada a `sanic_numerofila`
(queda perdida entre las demás columnas, contra el motivo de diseño de cada playbook de vista); o
SOBRA en Filas/Terminadas/Tomadas por el bot (ruido en vistas que el diseño dejó explícitamente
afuera).

**Automatizable.** API (`layoutxml`/`fetchxml` reales vía Web API; corregido desde `GUI`).

---

### H-03 — Catálogos: "Correos Autorizados", "Autorizaciones - Planes" y "Autorizaciones - Clientes"

**Qué prueba.** Los dos renombres del 2026-09-27 ("Autorizados" → "Correos Autorizados",
"Autorizaciones" → "Autorizaciones - Planes") y la entrada nueva de DN-01 ("Autorizaciones -
Clientes"), las tres en el sitemap en vivo, cada una abriendo la vista por defecto de su propia
tabla.

**Necesita.** Nada del navegador: Web API directo. Ningún dato previo.

**Cómo.** Sobre el mismo `sitemapxml` de H-01, ubicar el `Area` con título 1033 "Catalogos" y sus
`SubArea` `subarea_autorizados`, `subarea_autorizaciones` y `subarea_autorizacionescliente`; leer
título 1033/3082 y el atributo `Entity` de cada una; y confirmar la vista por defecto real de cada
tabla con
`GET savedqueries?$select=name&$filter=returnedtypecode eq '<tabla>' and isdefault eq true and querytype eq 0`.

**Qué tiene que pasar.** `subarea_autorizados`: título "Correos Autorizados", `Entity` =
`sanic_mppp_tbl_autorizado`. `subarea_autorizaciones`: título "Autorizaciones - Planes", `Entity` =
`sanic_mppp_tbl_autorizacionplan`, cuya vista por defecto es "Autorizaciones activas".
`subarea_autorizacionescliente`: título "Autorizaciones - Clientes", `Entity` =
`sanic_mppp_tbl_autorizacioncliente`, cuya vista por defecto es "Autorizaciones por cliente activas".
Las dos últimas son entradas DISTINTAS que abren tablas DISTINTAS.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py H` (caso H-03).

**Cómo se sabe que falló.** Falta alguna de las tres entradas; algún título quedó con el nombre viejo
("Autorizados"/"Autorizaciones" a secas); las dos entradas de autorización abren la MISMA tabla (no
se diferenciaron); o la vista por defecto de alguna de las dos tablas no es la esperada (el sitemap
"apunta bien" pero la vista que se abre no es la correcta).

**Automatizable.** API (corregido desde `GUI`).

---

### H-04 — Formulario "Autorizacion por cliente": encabezado (2 lookups) + evidencia

**Qué prueba.** El formulario nuevo (DN-01, 2026-09-27), leyendo su `formxml` real de
`systemforms`: el encabezado con los 3 campos (`sanic_nombre`, y los lookups
`sanic_autorizadoid`/`sanic_clienteid`) y la sección separada "Evidencia de la autorizacion" con
`sanic_documentofirmado` y `sanic_fechadocumento` (ver también `10-autorizacion-cliente.md` U-10, que
prueba el guardado; este caso es solo de estructura del formulario).

**Necesita.** Nada del navegador: Web API directo. Ningún dato previo.

**Cómo.**
`GET systemforms?$select=formxml&$filter=objecttypecode eq 'sanic_mppp_tbl_autorizacioncliente' and type eq 2`
(2 = Main); parsear el `formxml`: los `<control datafieldname="...">` del `<header>`, y los de la
`<section>` cuya `<label description="Evidencia de la autorizacion">` coincide.

**Qué tiene que pasar.** El encabezado trae `sanic_nombre`, `sanic_autorizadoid` y `sanic_clienteid`,
y los dos últimos son controles de tipo Lookup (`classid` `{270BD3DB-D9AF-4782-9025-509E298DEC0A}`).
Existe una sección separada "Evidencia de la autorizacion" con `sanic_documentofirmado` y
`sanic_fechadocumento`.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py H` (caso H-04).

**Cómo se sabe que falló.** Falta algún campo del encabezado, o alguno de los dos lookups no es un
control Lookup real (`classid` distinto); o falta la sección de evidencia, o sus campos están
mezclados con el encabezado en vez de en su propia sección (el hueco que señaló el aprobador seguiría
sin cerrarse en la interfaz, aunque las columnas ya existan en la tabla).

**Automatizable.** API (`formxml` real vía Web API; corregido desde `GUI`).

---

### H-05 — El ícono reutilizado, no uno nuevo

**Qué prueba.** "Para atención manual" reusa el SVG de "Para revisar" (mismo patrón de negocio);
"Autorizaciones - Clientes" reusa el ícono de "Autorizaciones - Planes". Ninguna de las dos generó un
web resource nuevo.

**Necesita.** Nada del navegador: reusa el mismo `sitemapxml` de H-01/H-03. Ningún dato previo.

**Cómo.** Sobre el `sitemapxml` en vivo, comparar el atributo `Icon` de `subarea_paraatencionmanual`
contra el de `subarea_pararevisar`, y el de `subarea_autorizacionescliente` contra el de
`subarea_autorizaciones`.

**Qué tiene que pasar.** Los dos pares de `Icon` son, cada uno, el MISMO valor
(`/WebResources/sanic_mppp_wr_svg_pararevisar` y `/WebResources/sanic_mppp_wr_svg_autorizaciones`
respectivamente) — nada de un web resource nuevo.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py H` (caso H-05).

**Cómo se sabe que falló.** El `Icon` de alguna de las dos entradas nuevas difiere del de su par (se
habría creado o asignado un web resource distinto sin necesidad).

**Automatizable.** API (mismo `sitemapxml` de H-01/H-03, sin navegador; corregido desde `GUI`).

---

### H-06 — La lección del botón fantasma: distinguir REGLAS de METADATA COMPILADA

**Qué prueba.** El hallazgo real del 2026-09-27: el botón "Descargar para digitar" existía en el
servidor —verificado por API por TRES caminos distintos (`RetrieveEntityRibbon`, los `ribbondiff` con
sus LocLabels en 1033/3082, y el verificador del proyecto)— y NO se dibujaba en el navegador: la causa
era la metadata de ribbon **compilada**, no una regla de habilitación. Este caso documenta el
PROCEDIMIENTO DE DIAGNÓSTICO para la próxima vez que un botón nuevo "no aparezca", sea cual sea, y que
quien lo encuentre sepa en minutos —no en dos días, como esta vez— si el problema es de REGLAS o de
METADATA COMPILADA: son diagnósticos opuestos con el mismo síntoma superficial ("no lo veo").

**Necesita.** Acceso de **customización de la solución** en `make.powerapps.com` sobre
`sanic_mppp_sol_mantenimientoppp` — privilegio de PLATAFORMA (System Administrator/System Customizer
de Dataverse), **no** el rol de negocio `SR - MPPP - Administrador tecnico` que prueban
`local/sesion-admin_tecnico.json` y `estado_sesiones.py`: ese rol está acotado a las tablas Parámetro
y Reglas dentro de la app (`04-matriz-privilegios.md`) y no da acceso al maker portal para regenerar
metadata. Además: `local/sesion-ejecutivo.json` (rol `SR - MPPP - Ejecutivo`), confirmada viva y con
ese rol por `python3 herramientas/pruebas/estado_sesiones.py` ANTES de correr nada; el parámetro de
URL `&ribbondebug=true` agregado a la URL de la app model-driven.

**Cómo.**
1. Abrir la vista de Filas en la app con `&ribbondebug=true` en la URL, y usar el **Command
   Checker** sobre el comando en cuestión.
2. Leer la lista de comandos que devuelve el Command Checker:
   - Si el botón aparece marcado **`(hidden)`**: es un problema de **REGLAS** (`EnableRule`/
     `DisplayRule`) — revisar contra la selección actual, la metadata está bien.
   - Si el botón está **AUSENTE de la lista entera**: es un problema de **METADATA COMPILADA** del
     ribbon — ningún ajuste de reglas lo va a arreglar.
3. Para el segundo caso: en el maker portal, sobre la tabla/app correspondiente, usar **"Regenerate
   ribbon metadata"** (o volver a publicar la personalización completa) y repetir el paso 1.

**Qué tiene que pasar.** Después de "Regenerate ribbon metadata", un botón que estaba AUSENTE de la
lista del Command Checker pasa a aparecer (visible o `(hidden)`, según la selección actual).

**Cómo se verifica.** La lista del Command Checker antes y después. Para la confirmación en el DOM
real (con sesión y sin depender del maker portal): `cd pruebas/gui && MPPP_USUARIO=ejecutivo npx
playwright test ribbon-fila` (ver `pruebas/gui/ayudantes/ribbon.mjs`, que codifica esta misma
distinción: **AUSENTE** → "Sospechar metadata de ribbon (regenerar desde Command Checker) antes que el
EnableRule"; **presente pero DESHABILITADO** → "revisar el EnableRule contra la selección actual, la
metadata está bien"). Este set nunca duplica ese arnés (`CONVENCIONES` §9).

**Cómo se sabe que falló (es decir, que se erró el diagnóstico).** Se pasaron minutos u horas
ajustando `EnableRule`/`DisplayRule` sobre un botón que en realidad estaba AUSENTE de la lista del
Command Checker (metadata compilada) — o al revés, se regeneró metadata sobre un botón que SÍ estaba
en la lista pero `(hidden)` por una regla. Confundirlos es exactamente lo que costó dos días este
mismo día.

**Automatizable.** Manual (el diagnóstico y la reparación en el maker portal). El síntoma
"presente vs. ausente" en el DOM real ya lo automatiza `pruebas/gui/ayudantes/ribbon.mjs`
(GUI, con sesión), pero la ACCIÓN de "Regenerate ribbon metadata" es manual, en el maker portal — por
eso el caso queda `N-A` en este set de API y no se corre desde acá.

> **Nota para quien mantenga el documento de instalación en otro entorno** (fuera del alcance de este
> archivo de pruebas, pero registrado acá porque de acá sale la lección): después de importar la
> solución y publicar (`PublishAllXml`), hay que **regenerar la metadata del ribbon** desde el Command
> Checker, o los botones custom no aparecen — aunque la publicación haya terminado sin ningún error.

---

### H-07 — `sanic_mppp_tbl_autorizacioncliente` como componente real de la app

**Qué prueba.** El hueco que dejó resolver esta tabla con un **import manual de la solución** en vez
de con la herramienta del proyecto en modo escritura: que la tabla sea un componente REAL de la app
`MDA - MPPP - Mantenimiento PPP` (`RetrieveAppComponents` en vivo), no solo algo que el sitemap
menciona (H-03) sin que la tabla misma viaje con la app.

**Necesita.** Nada del navegador: Web API directo, más una corrida de solo lectura de
`herramientas/construir/app.py` (con `--solo-verificar` nunca escribe). Ningún dato previo.

**Cómo.**
1. `python3 herramientas/construir/app.py playbooks/app/mantenimientoppp.md --solo-verificar`.
2. Releer, en el propio caso (no confiar solo en la salida del paso 1 — `CONVENCIONES` §2):
   `GET appmodules/Microsoft.Dynamics.CRM.RetrieveUnpublishedMultiple()?$select=appmoduleid&$filter=uniquename eq 'sanic_mppp_mda_mantenimientoppp'`,
   después `GET RetrieveAppComponents(AppModuleId=<id>)` y, para cada componente de tabla
   (`componenttype eq 1`), `GET EntityDefinitions(<objectid>)?$select=LogicalName`.

**Qué tiene que pasar.** El paso 1 termina en `ya_existia` (nunca `difiere`/`error`/`bloqueado`), y
entre los nombres lógicos que trae el paso 2 aparece, POR NOMBRE, `sanic_mppp_tbl_autorizacioncliente`
— no se afirma un total de tablas (`CONVENCIONES` §7): ese número puede cambiar sin que nada esté
roto.

**Cómo se verifica.** `python3 herramientas/pruebas/correr.py H` (caso H-07).

**Cómo se sabe que falló.** El paso 1 no da `ya_existia` (el sitemap o los componentes en vivo se
despegaron del playbook); o `sanic_mppp_tbl_autorizacioncliente` no aparece por nombre en
`RetrieveAppComponents` (la tabla no viaja con la app, aunque el sitemap la mencione — exactamente el
síntoma que motivó este caso: un import manual que quedó a medias).

**Automatizable.** API.

---

## Resultados

Corrida real, `python3 herramientas/pruebas/correr.py H`, 2026-09-27: **6 OK · 0 FALLA · 1 N-A**
(código de salida 0).

| Caso | Resultado | Notas |
|---|---|---|
| H-01 sitemap "Para atención manual", en su posición | OK | API, sin sesión |
| H-02 columna en las 4 vistas de trabajo, y en ninguna más | OK | API, sin sesión |
| H-03 "Correos Autorizados" / "Autorizaciones - Planes" / "Autorizaciones - Clientes" | OK | API, sin sesión |
| H-04 formulario AutorizacionCliente: encabezado + evidencia | OK | API, sin sesión |
| H-05 íconos reutilizados, no nuevos | OK | API, sin sesión |
| H-06 diagnóstico REGLAS vs. METADATA COMPILADA | N-A | maker portal (System Administrator/Customizer de plataforma, NO `admin_tecnico`), manual — motivo completo en el caso |
| H-07 la tabla AutorizacionCliente es componente real de la app | OK | API, sin sesión |
