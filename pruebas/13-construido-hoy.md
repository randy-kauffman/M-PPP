# 13 — Lo construido el 2026-09-27: sitemap, vistas, y la lección del ribbon

Cubre los hallazgos y correcciones del propio 2026-09-27 que todavía no tenían un caso de prueba:
la entrada de sitemap "Para atención manual", la columna `sanic_requiereatencionmanual` en las
vistas de trabajo, la diferenciación de "Autorizaciones - Planes"/"Autorizaciones - Clientes", y el
diagnóstico correcto de un botón que existe en el servidor pero no se dibuja. Fuente:
`diseno/05-app-model-driven.md` §2-§3, `diseno/06-inventario-componentes.md` 12.2/12.7,
`pruebas/gui/README.md` y `pruebas/gui/ayudantes/ribbon.mjs` (solo lectura, no se tocan).

Convención de IDs: prefijo `H-` (**H**oy), ver el encabezado de `09-rpa-fase2.md`.

## Preparativos comunes a este archivo

- Sesión real de Ejecutivo (`local/sesion-ejecutivo.json`) para los casos de sitemap/vistas.
- Al menos una Fila con `sanic_requiereatencionmanual = sí` (seguir `09-rpa-fase2.md` B-19 para
  dejarla en ese estado) para ver la vista con contenido, no solo vacía.

---

### H-01 — Entrada de sitemap "Para atención manual" en el área Trabajo

**Qué prueba.** El olvido corregido el 2026-09-27: la vista existía desde fase 2 pero no tenía
entrada propia en el sitemap. Ahora `subarea_paraatencionmanual` está entre "Por aprobar" y "Por
clasificar".

**Necesita.** Sesión de Ejecutivo.

**Cómo.** Abrir la app y mirar el área Trabajo del sitemap.

**Qué tiene que pasar.** La entrada "Para atención manual" aparece en esa posición exacta (entre
"Por aprobar" y "Por clasificar"), y al abrirla muestra Filas con
`sanic_requiereatencionmanual = sí`.

**Cómo se verifica.** Inspección visual del sitemap y de la vista.

**Cómo se sabe que falló.** La entrada no aparece (regresión del propio arreglo del 2026-09-27), o
aparece en otra posición del área, o abre una vista distinta (filtro mal aplicado).

**Automatizable.** GUI (sesión `ejecutivo`).

---

### H-02 — Columna "Requiere atención manual" en las 4 vistas de trabajo (y en ninguna otra)

**Qué prueba.** El hallazgo del aprobador: sin esta columna, un ejecutivo no podía ver de un
vistazo si el bot marcó la fila en las vistas donde ya trabaja todos los días. Se agregó a **Mis
clientes — por digitar**, **Todos — por digitar**, **Por aprobar** y **Mis clientes —
devueltas** — y a NINGUNA otra (ni Filas, ni Terminadas, ni Tomadas por el bot: no son vistas de
trabajo).

**Necesita.** Sesión de Ejecutivo y de Supervisor (para "Por aprobar"); al menos una fila con la
marca prendida visible en alguna de las cuatro vistas.

**Cómo.** Abrir cada una de las 7 vistas de Fila (las 4 de trabajo + Filas + Terminadas + Tomadas
por el bot) y revisar sus columnas.

**Qué tiene que pasar.** La columna está presente, como segunda columna (pegada al n.º de fila),
en las 4 vistas de trabajo; AUSENTE en Filas, Terminadas y Tomadas por el bot.

**Cómo se verifica.** Inspección visual de cada vista.

**Cómo se sabe que falló.** Falta en alguna de las 4 vistas de trabajo (el ejecutivo sigue sin ver
la marca donde más la necesita), o SOBRA en Filas/Terminadas/Tomadas por el bot (ruido en vistas que
no son de trabajo, contra el criterio explícito del diseño).

**Automatizable.** GUI (sesiones `ejecutivo` y `supervisor`).

---

### H-03 — "Autorizaciones - Clientes" diferenciada de "Autorizaciones - Planes" en el sitemap

**Qué prueba.** El pedido textual del aprobador de poder distinguir las dos vías de DN-01 desde el
sitemap, cada una con su propia vista por defecto.

**Necesita.** Sesión de Administrador de planes.

**Cómo.** Abrir el grupo Catálogos del sitemap.

**Qué tiene que pasar.** Dos entradas separadas: "Autorizaciones - Planes" (abre "Autorizaciones
activas", sobre `AutorizacionPlan`) y "Autorizaciones - Clientes" (abre "Autorizaciones por cliente
activas", sobre `AutorizacionCliente`).

**Cómo se verifica.** Inspección visual; confirmar con qué tabla trabaja cada vista (columnas
mostradas).

**Cómo se sabe que falló.** Una sola entrada para las dos tablas (no se diferenciaron), o las dos
entradas abren la misma vista.

**Automatizable.** GUI (sesión `admin_planes`).

---

### H-04 — Formulario "Autorizacion por cliente": encabezado + evidencia

**Qué prueba.** El formulario nuevo (DN-01, 2026-09-27) con su encabezado (`sanic_nombre`,
`sanic_autorizadoid`, `sanic_clienteid`) y su sección "Evidencia de la autorización" (ver también
`10-autorizacion-cliente.md` U-10, que prueba el guardado; este caso es solo de estructura visual).

**Necesita.** Sesión de Administrador de planes.

**Cómo.** Abrir (o crear) un registro de `AutorizacionCliente`.

**Qué tiene que pasar.** El formulario muestra los 3 campos del encabezado y una sección separada
"Evidencia de la autorización" con `sanic_documentofirmado` y `sanic_fechadocumento`.

**Cómo se verifica.** Inspección visual del formulario.

**Cómo se sabe que falló.** Falta la sección de evidencia (el hueco que el aprobador señaló seguiría
sin cerrarse en la interfaz, aunque las columnas ya existan en la tabla), o los campos del
encabezado no están.

**Automatizable.** GUI (sesión `admin_planes`).

---

### H-05 — El ícono reutilizado, no uno nuevo

**Qué prueba.** "Para atención manual" reusa el SVG de "Para revisar" (mismo patrón de negocio);
"Autorizaciones - Clientes" reusa el ícono de "Autorizaciones - Planes". Ninguno de los dos generó
un web resource nuevo.

**Necesita.** Sesión de Ejecutivo y de Administrador de planes; acceso de lectura a los web
resources de íconos (`sanic_mppp_wr_svg_*`) para confirmar el nombre exacto que usa cada entrada.

**Cómo.** Comparar visualmente el ícono de "Para atención manual" contra "Para revisar", y el de
"Autorizaciones - Clientes" contra "Autorizaciones - Planes"; si hace falta desambiguar a simple
vista, confirmar el nombre del web resource asignado a cada entrada del sitemap.

**Qué tiene que pasar.** Los pares de íconos son visualmente IDÉNTICOS (mismo archivo SVG).

**Cómo se verifica.** Inspección visual, o el nombre del web resource si hay que confirmarlo.

**Cómo se sabe que falló.** Los íconos se ven distintos: se habría creado un web resource nuevo sin
necesidad (contradice el inventario, que cuenta 22 íconos sin sumar ninguno por estas dos
entradas).

**Automatizable.** GUI (sesiones `ejecutivo` y `admin_planes`).

---

### H-06 — La lección del botón fantasma: distinguir REGLAS de METADATA COMPILADA

**Qué prueba.** El hallazgo real del 2026-09-27: el botón "Descargar para digitar" existía en el
servidor, verificado por API, y NO se dibujaba en el navegador — la causa era metadata de ribbon
compilada, no una regla de habilitación. Este caso documenta el PROCEDIMIENTO DE DIAGNÓSTICO para
la próxima vez que un botón nuevo "no aparezca", sin importar cuál sea.

**Qué prueba, en una frase.** Que quien encuentre un botón nuevo ausente sepa, en minutos y no en
días, si el problema es de REGLAS o de METADATA COMPILADA — son diagnósticos opuestos con el mismo
síntoma superficial ("no lo veo").

**Necesita.** Acceso de Administrador técnico al maker portal (`make.powerapps.com`) sobre la
solución `sanic_mppp_sol_mantenimientoppp`; sesión de Ejecutivo o Supervisor para reproducir en la
app; el parámetro de URL `&ribbondebug=true` agregado a la URL de la app model-driven.

**Cómo.**
1. Abrir la vista de Filas en la app con `&ribbondebug=true` en la URL, y usar el **Command
   Checker** (la extensión/herramienta que expone esa bandera) sobre el comando en cuestión.
2. Leer la lista de comandos que devuelve el Command Checker:
   - Si el botón aparece en la lista marcado **`(hidden)`**: es un problema de **REGLAS**
     (`EnableRule`/`DisplayRule`) — revisar contra la selección actual, la metadata está bien.
   - Si el botón está **AUSENTE de la lista entera** (ni siquiera aparece como oculto): es un
     problema de **METADATA COMPILADA** del ribbon — ningún ajuste de reglas lo va a arreglar.
3. Para el segundo caso: en el maker portal, sobre la tabla/app correspondiente, usar la acción
   **"Regenerate ribbon metadata"** (o volver a publicar la personalización completa) y repetir el
   paso 1.

**Qué tiene que pasar.** Después de "Regenerate ribbon metadata", un botón que estaba AUSENTE de la
lista del Command Checker pasa a aparecer (visible o `(hidden)`, según corresponda a la selección
actual).

**Cómo se verifica.** La lista del Command Checker antes y después, y — para la confirmación final
— el arnés ya existente `pruebas/gui/casos/ribbon-fila.spec.mjs` (ver `ayudantes/ribbon.mjs`, que
codifica esta misma distinción: **AUSENTE** → "Sospechar metadata de ribbon (regenerar desde
Command Checker) antes que el EnableRule"; **presente pero DESHABILITADO** → "revisar el EnableRule
contra la selección actual, la metadata está bien").

**Cómo se sabe que falló (es decir, que se erró el diagnóstico).** Se pasaron minutos u horas
ajustando `EnableRule`/`DisplayRule` sobre un botón que en realidad estaba **ausente de la lista del
Command Checker** (metadata compilada) — o al revés, se regeneró metadata sobre un botón que SÍ
estaba en la lista pero `(hidden)` por una regla. Los dos síntomas se VEN igual desde la app ("no
lo veo"), pero tienen remedios opuestos; confundirlos es exactamente lo que costó tiempo real este
mismo día.

**Automatizable.** Manual (el diagnóstico en sí, con el maker portal) — el síntoma de "presente vs.
ausente" en el DOM real ya lo automatiza `pruebas/gui/ayudantes/ribbon.mjs`, pero la ACCIÓN de
"Regenerate ribbon metadata" es manual, en el maker portal.

---

## Resultados

| Caso | Resultado | Notas |
|---|---|---|
| H-01 sitemap "Para atención manual" | | sesión ejecutivo |
| H-02 columna en las 4 vistas de trabajo (y en ninguna más) | | sesiones ejecutivo/supervisor |
| H-03 "Autorizaciones - Clientes" diferenciada | | sesión admin_planes |
| H-04 formulario AutorizacionCliente: estructura | | sesión admin_planes |
| H-05 íconos reutilizados, no nuevos | | sesiones ejecutivo/admin_planes |
| H-06 diagnóstico REGLAS vs. METADATA COMPILADA | | maker portal, manual |
