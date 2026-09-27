# 12 — DN-03: la obligatoriedad se define por gestión

Cubre `plantilla.obligatoriedad` con `porGestion` (`Inclusion`/`Exclusion`/`Modificacion`), la
compatibilidad con lo sembrado antes de DN-03, y el versionado de parámetros (agregar fila, nunca
editar). Fuente: `diseno/DIRECTRICES-NUEVAS.md` DN-03,
`src/Sanic.Mppp.Plugins/Validacion/ParametrosDePlantilla.cs` (`ObligatoriedadPlantilla`),
`playbooks/datos/parametros.md`.

Convención de IDs: prefijo `O-` (**O**bligatoriedad), ver el encabezado de `09-rpa-fase2.md`.

**Advertencia general de este archivo.** `plantilla.obligatoriedad` decide, en TODA Solicitud que
se valide mientras esté activa esa versión, qué campos son obligatorios. Sembrar una versión nueva
en Dev la vuelve la ACTIVA de inmediato (la más alta gana) para absolutamente todo el entorno. Los
casos que necesitan una versión nueva lo dicen explícitamente en "Necesita", con la recomendación de
NO hacerlo si alcanza con la cobertura unitaria (`ParametrosDePlantillaAceptacion`, ya en verde).

## Preparativos comunes a este archivo

- Confirmar con `ver_solicitud.py --parametros` qué versión de `plantilla.obligatoriedad` está
  activa HOY (al momento de escribir esto: `sanic_version=2`, con `porGestion.Exclusion` sembrado y
  marcado **PROVISIONAL** — pendiente de confirmación del aprobador).
- Un Excel de prueba por escenario (`generar_excel.py` genera los de fase 1; los de gestión
  `Exclusion` necesitan un caso nuevo si no existe uno con cuenta/banco/moneda vacíos — puede
  armarse a mano siguiendo el mismo formato que `generar_excel.py` usa, sin tocar ese script).

---

### O-01 — Compatibilidad: un parámetro SIN `porGestion` se comporta igual que antes de DN-03

**Qué prueba.** La garantía explícita del diseño: "un parámetro sin `porGestion` se comporta
EXACTAMENTE como hoy". Es la condición bajo la que lo ya sembrado en los tres entornos sigue siendo
válido sin migrar nada.

**Necesita.** Confirmar que la versión 1 de `plantilla.obligatoriedad` (`{"porDefecto":
"obligatorio","opcionales":[{"campo":"referencia"}]}`, sin `porGestion`) sigue existiendo intacta
(no hace falta que sea la ACTIVA para este caso: alcanza con que la fila exista — ver O-08 para el
chequeo de que ninguna versión se edita).

**Cómo.** Correr los casos ya existentes de `03-reglas-de-registro.md` que ejercitan
`OBLIGATORIEDAD` (sin cambiar `plantilla.obligatoriedad` de lo que esté activo hoy).

**Qué tiene que pasar.** Exactamente lo que `03-reglas-de-registro.md` ya documenta.

**Cómo se verifica.** Los criterios ya existentes de esos casos.

**Cómo se sabe que falló.** Cualquier diferencia contra lo ya establecido, sin haber tocado
`plantilla.obligatoriedad`: sería DN-03 rompiendo compatibilidad hacia atrás, exactamente lo que el
diseño prohíbe.

**Automatizable.** API (reutiliza casos existentes).

---

### O-02 — `Exclusion` afloja `numeroCuenta`, `banco` y `moneda` (marcado PROVISIONAL)

**Qué prueba.** La versión 2 sembrada el 2026-09-27: una fila de gestión `Exclusion` puede omitir
cuenta, banco y moneda sin que `OBLIGATORIEDAD` la rechace — el criterio es que una exclusión solo
necesita identificar a quién dar de baja (plan, identificación, referencia).

**Necesita.** `plantilla.obligatoriedad` en su versión 2 activa (la sembrada, con
`porGestion.Exclusion`). **Este criterio está marcado PROVISIONAL en el propio playbook**
(`playbooks/datos/parametros.md`): pendiente de confirmación de negocio del aprobador. Si el
aprobador todavía no confirmó, este caso documenta el comportamiento TAL COMO ESTÁ HOY, no un
comportamiento ya cerrado — anotar el resultado con esa salvedad.

**Cómo.** Un Excel con una fila `gestion=Exclusion`, con `numeroCuenta`, `banco` y `moneda` en
blanco, y `numeroPlan`/`nombreBeneficiario`(o identificación)/`referencia` completos.

**Qué tiene que pasar.** La fila NO se rechaza por `OBLIGATORIEDAD` a causa de esos tres campos
vacíos.

**Cómo se verifica.** `ver_solicitud.py --ultima` (resultado de `OBLIGATORIEDAD` para esa fila).

**Cómo se sabe que falló.** La fila se rechaza citando `numeroCuenta`, `banco` o `moneda`: la
versión activa no trae el `porGestion.Exclusion` esperado, o el motor no lo está aplicando.

**Automatizable.** API.

---

### O-03 — `Inclusion` y `Modificacion` NO tienen entrada en `porGestion` todavía

**Qué prueba.** Solo `Exclusion` está sembrada en `porGestion` hoy; `Inclusion` y `Modificacion`
siguen bajo el bloque global sin ningún ajuste.

**Necesita.** La misma versión 2 activa que O-02.

**Cómo.** El mismo Excel de O-02 pero con `gestion=Inclusion` (o `Modificacion`), y los mismos tres
campos en blanco.

**Qué tiene que pasar.** La fila SÍ se rechaza por `OBLIGATORIEDAD` (esos campos siguen siendo
obligatorios para estas dos gestiones): comportamiento idéntico al de antes de DN-03.

**Cómo se verifica.** `ver_solicitud.py --ultima`.

**Cómo se sabe que falló.** La fila NO se rechaza: `porGestion` se estaría aplicando a una gestión
que no tiene entrada sembrada (contagio entre gestiones que el diseño no pide).

**Automatizable.** API.

---

### O-04 — Precedencia `obligatorios` sobre `opcionales` (cobertura unitaria, no sembrar en Dev)

**Qué prueba.** "`opcionales` afloja, `obligatorios` endurece: si un campo queda en las dos listas
de la MISMA entrada, gana `obligatorios`" — la mitad "endurece" de esta regla no está sembrada hoy
en ningún entorno (el ejemplo de `Modificacion` con `obligatorios` en `DIRECTRICES-NUEVAS.md` es
ilustrativo, no está en Dev).

**Necesita.** **No sembrar una versión nueva en Dev solo para este caso**: crear una entrada con
`obligatorios` cambiaría la versión ACTIVA para todo el entorno de forma permanente (agregar
versiones es fácil; "deshacer" es sembrar otra versión más, nunca vuelve sola a la anterior). La
cobertura confiable es `ParametrosDePlantillaAceptacion` (unitaria, ya construida con TDD según
`DIRECTRICES-NUEVAS.md`).

**Cómo.** Revisar que existan casos unitarios para: (a) un campo en `opcionales` de `porGestion`
sin estar en `obligatorios`, (b) un campo en `obligatorios` sin estar en `opcionales`, (c) el mismo
campo en las dos listas de la misma entrada (gana `obligatorios`).

**Qué tiene que pasar.** Los tres escenarios ya están cubiertos en la suite de plugins.

**Cómo se verifica.** `dotnet test` sobre `ParametrosDePlantillaAceptacion` (correr pruebas, no
compilar — filtrar por ese nombre de clase).

**Cómo se sabe que falló.** Falta alguno de los tres escenarios en la suite: hay que pedirlo antes
de confiar en la precedencia si algún día se siembra una gestión con `obligatorios` real.

**Automatizable.** Unit (no Dev).

---

### O-05 — Gestión inválida o vacía en la FILA: rige solo el bloque global

**Qué prueba.** "Una fila cuyo valor de gestión no es válido (o el plan no existe) SOLO calza con
`*` en `reglas`, y NUNCA mira `porGestion`" — no se le suma un motivo de rechazo derivado, porque
esa fila ya va a fallar `LISTAS_VALIDAS` por su cuenta.

**Necesita.** Un Excel con una fila con `gestion` en blanco, o con un valor que no es
`Inclusion`/`Exclusion`/`Modificacion` (p. ej. `"Baja"`), y ADEMÁS con `numeroCuenta`/`banco` en
blanco (para poder distinguir el motivo del rechazo).

**Cómo.** Mandar ese Excel y revisar TODOS los resultados de regla de esa fila, no solo el primero.

**Qué tiene que pasar.** `LISTAS_VALIDAS` (u otra regla de valor canónico) rechaza por la gestión
inválida; `OBLIGATORIEDAD` para esa fila usa el bloque GLOBAL (no aplica ningún aflojamiento de
`porGestion.Exclusion`, aunque el valor escrito se pareciera a "exclusión") — no aparece un segundo
motivo de rechazo derivado de `porGestion`.

**Cómo se verifica.** `ver_solicitud.py --ultima`, mirando el resultado y el motivo de CADA regla
para esa fila.

**Cómo se sabe que falló.** La fila trae un motivo de `OBLIGATORIEDAD` que menciona `porGestion` o
un aflojamiento que no le correspondía (ruido sobre una fila que ya iba a fallar de todos modos).

**Automatizable.** API.

---

### O-06 — Nombres de gestión sin distinguir caja ni tildes

**Qué prueba.** La comparación de la gestión de la fila contra las claves de `porGestion` usa el
mismo criterio que ya usa la validación de listas: sin distinguir mayúsculas ni tildes.

**Necesita.** La versión 2 activa (con `porGestion.Exclusion`).

**Cómo.** Un Excel con `gestion` escrita como `"exclusión"` (minúscula, con tilde) o `"EXCLUSION"`
en vez de `"Exclusion"` (el valor canónico exacto).

**Qué tiene que pasar.** Sigue aflojando `numeroCuenta`/`banco`/`moneda` igual que con el valor
canónico exacto.

**Cómo se verifica.** `ver_solicitud.py --ultima`.

**Cómo se sabe que falló.** La fila se rechaza por esos campos (la comparación está siendo
sensible a mayúsculas/tildes, en contra del criterio ya establecido para todo el resto de listas).

**Automatizable.** API.

---

### O-07 — Gestión desconocida DENTRO de `porGestion` es error de configuración (alto riesgo, no sembrar en Dev)

**Qué prueba.** Un typo en la CLAVE del parámetro (p. ej. `"Exclusio"` en vez de `"Exclusion"`) es
un `FormatException` al leer el parámetro — error ruidoso de configuración, distinto de O-05 (que es
sobre el VALOR de la fila, no sobre el parámetro).

**Necesita.** **NO sembrar esto en Dev de rutina**: un `plantilla.obligatoriedad` con esta clave
mal escrita, si se convierte en la versión activa, rompe la validación de TODA Solicitud del
entorno hasta que alguien lo note y siembre una versión corregida (más alta). Es exactamente el
"falla cerrado a propósito" del diseño, pero contra un entorno compartido el costo es alto.

**Cómo (si se decide correrlo, en una ventana coordinada, con una versión corregida lista para
sembrar de inmediato después).** Sembrar una versión con `"porGestion":{"Exclusioon": {...}}` (clave
mal escrita) y mandar cualquier Solicitud.

**Qué tiene que pasar.** La Custom API de validación falla con un error de configuración que nombra
la clave inválida (no un rechazo silencioso de una sola fila: es el parámetro entero el que no
carga).

**Cómo se verifica.** El mensaje de error de la llamada/Bitácora.

**Cómo se sabe que falló.** El parámetro se carga igual, ignorando la clave mal escrita en
silencio: un typo que alguien cree que configuró y no configuró, sin ninguna señal.

**Automatizable.** Unit (preferido) / manual de alto riesgo si se necesita la confirmación contra
Dev.

---

### O-08 — Versionar es AGREGAR una fila, nunca editar la vigente

**Qué prueba.** DD-02/la nota de `playbooks/datos/parametros.md`: cambiar una regla de negocio
siembra `sanic_version` más alto como fila NUEVA; la anterior queda intacta, de respaldo/histórico,
y el motor resuelve la activa por `sanic_version` descendente con `statecode` activo.

**Necesita.** Nada que crear: alcanza con leer lo ya sembrado.

**Cómo.** `dataverse_api.py GET "sanic_mppp_tbl_parametros?$filter=sanic_nombre eq
'plantilla.obligatoriedad'&$select=sanic_version,sanic_valor,statecode"`.

**Qué tiene que pasar.** Aparecen AL MENOS dos filas (`sanic_version=1` y `sanic_version=2`), las
dos activas, con `sanic_valor` distinto entre sí, y la versión 1 sigue exactamente como la
documenta `DD-16` (sin `porGestion`).

**Cómo se verifica.** La respuesta de esa consulta.

**Cómo se sabe que falló.** Solo aparece una fila (la versión 1 se editó en vez de agregar la 2:
rompería `sanic_versionparametros` de Solicitudes ya procesadas con la versión anterior, que
dejaría de decir la verdad), o la versión 1 tiene un `sanic_valor` distinto al original.

**Automatizable.** API.

---

### O-09 — El mensaje al cliente no cambia con DN-03

**Qué prueba.** El texto de rechazo de `OBLIGATORIEDAD` sigue nombrando el CAMPO, sin citar su
valor (D-41) — que la obligatoriedad ahora dependa de la gestión no puede filtrarse al texto que
lee el cliente (no tiene por qué deducir la configuración interna del banco).

**Necesita.** Una fila de gestión `Inclusion` (o `Modificacion`) con `numeroCuenta` vacío (sigue
siendo obligatoria para esas gestiones — O-03).

**Cómo.** Mandar ese Excel y leer `sanic_mensaje` de la fila rechazada.

**Qué tiene que pasar.** El mensaje nombra el campo (`numeroCuenta`) exactamente como antes de
DN-03, sin mencionar "gestión", "Exclusion" ni ninguna palabra de configuración interna.

**Cómo se verifica.** `ver_solicitud.py --ultima`.

**Cómo se sabe que falló.** El mensaje cambió de forma, o menciona la gestión o el hecho de que
otra gestión no exigiría este campo.

**Automatizable.** API.

---

### O-10 — La regla `OBLIGATORIEDAD` no cambia de código, orden ni nivel

**Qué prueba.** DN-03 es explícito: "No cambia de código, de orden, de nivel ni de efecto. Solo
cambia qué considera obligatorio" — mismo principio que DN-01 sobre `AUTORIZACION_CORREO_PLAN`.

**Necesita.** Cualquier Solicitud procesada con la versión 2 activa.

**Cómo.** `ver_solicitud.py --ultima` y comparar el nombre, nivel (Registro) y posición en el orden
de `OBLIGATORIEDAD` contra lo ya documentado en `03-reglas-de-registro.md`.

**Qué tiene que pasar.** Mismo código de regla, mismo nivel, misma posición relativa en la
Bitácora que antes de DN-03.

**Cómo se verifica.** La Bitácora de la Solicitud (orden de las reglas evaluadas).

**Cómo se sabe que falló.** La regla aparece con otro nombre, en otro nivel, o en otra posición:
señal de que se reimplementó en vez de ajustarse, contra la instrucción explícita del diseño.

**Automatizable.** API.

---

## Resultados

| Caso | Resultado | Notas |
|---|---|---|
| O-01 compatibilidad sin `porGestion` | | |
| O-02 `Exclusion` afloja los tres campos | | PROVISIONAL, confirmar con aprobador |
| O-03 `Inclusion`/`Modificacion` sin cambios | | |
| O-04 precedencia `obligatorios` | | unitario, no sembrar |
| O-05 gestión inválida en la fila | | |
| O-06 sin distinguir caja/tildes | | |
| O-07 gestión desconocida en el parámetro | | alto riesgo, preferir unitario |
| O-08 versionar agrega, no edita | | |
| O-09 mensaje al cliente sin cambios | | |
| O-10 regla sin cambio de código/orden/nivel | | |
