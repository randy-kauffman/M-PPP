# 12 — DN-03: la obligatoriedad se define por gestión

Cubre `plantilla.obligatoriedad` con `porGestion` (`Inclusion`/`Exclusion`/`Modificacion`), la
compatibilidad con lo sembrado antes de DN-03, y el versionado de parámetros (agregar fila, nunca
editar). Fuente: `diseno/DIRECTRICES-NUEVAS.md` DN-03,
`src/Sanic.Mppp.Plugins/Validacion/ParametrosDePlantilla.cs` (`ObligatoriedadPlantilla`),
`playbooks/datos/parametros.md`.

Convención de IDs: prefijo `O-` (**O**bligatoriedad), ver el encabezado de `09-rpa-fase2.md`.

**Advertencia general de este archivo.** `plantilla.obligatoriedad` decide, en TODA Solicitud que
se valide mientras esté activa esa versión, qué campos son obligatorios. Sembrar una versión nueva
en Dev la vuelve la ACTIVA de inmediato (la más alta gana) para absolutamente todo el entorno. **Este
archivo NUNCA siembra una versión nueva de `plantilla.obligatoriedad` en Dev**: ningún caso de
`sets/set_o.py` escribe sobre esa tabla. Lo que hoy está sembrado (`sanic_version=1` y `=2`) se lee,
nunca se edita ni se versiona de nuevo; O-04, O-06 y O-07 (los de mayor riesgo: precedencia, y dos
formas de gestión mal escrita) se resuelven contra la cobertura unitaria ya construida con TDD
(`tests/Sanic.Mppp.Plugins.Tests/Aceptacion/ParametrosDePlantillaAceptacion.cs`, clase
`ParametrosDePlantillaAceptacion`), corrida en vivo con `dotnet test --filter`. El riesgo que este
archivo describía ("sembrar una versión con precedencia real, o con un typo, la vuelve la activa
para todo el entorno") queda en **cero** porque el código de este set jamás ejecuta ese `POST`.

## Frontera con el set R (R-18)

`03-reglas-de-registro.md` (set `R`) ya tiene, desde fase 2, el caso **R-18**: manda un Excel real
con una fila `gestion=Exclusion` y `numeroCuenta`/`banco`/`moneda` en blanco contra las Custom API
en vivo, y confirma que la fila queda Validada — es la prueba EN VIVO, dentro del motor de las ocho
reglas, de que `porGestion.Exclusion` funciona. Ese caso no se repite acá (CONVENCIONES §8 dice "un
set no supone nada" de otro set, pero tampoco pide volver a probar en vivo lo que otro set YA
prueba en vivo con el mismo arnés): repetirlo sería el mismo error que
`03-reglas-de-registro.md` §"Fase 2" ya evitó frente a `10-autorizacion-cliente.md`.

Este archivo (`O`) es el dueño de la cobertura **exhaustiva** de DN-03, y cubre exactamente lo que
R-18 NO cubre:

- **O-01/O-02** — de solo lectura: que el CONTENIDO sembrado en Dev (versión 1 intacta, versión 2
  con `porGestion.Exclusion` trayendo exactamente los tres campos pedidos) es el correcto. Es la
  prueba de que la configuración que R-18 ejercita en vivo es la que el aprobador pidió, no solo que
  "algo" pasó al mandar un Excel.
- **O-03, O-05, O-09, O-10** — escenarios en vivo que R-18 no ejercita: `Inclusion`/`Modificacion`
  sin aflojar, una gestión inválida en la FILA (no en el parámetro), el texto exacto del mensaje al
  cliente y la metadata de la regla en el catálogo.
- **O-04, O-06, O-07** — alto riesgo o de precedencia: nunca se siembran en Dev (cambiarían la
  versión activa para todo el entorno); se verifican corriendo `dotnet test` contra
  `ParametrosDePlantillaAceptacion`, que ya cubre estos tres escenarios con TDD.
- **O-08** — de solo lectura: la evidencia de que versionar agrega una fila y no edita la anterior.

## Preparativos comunes a este archivo

- El fixture compartido `PR*` (`preparar_datos.py`) y el `REMITENTE` de siempre, autorizado sobre
  `PR11` — igual que `sets/set_r.py` y `sets/set_s.py`.
- Los 15 Excel de fase 1 (`generar_excel.py`; ya trae `E06-obligatoriedad.xlsx`, que O-09 reutiliza
  tal cual). Los Excel de gestión `Exclusion`/`Inclusion`/`Modificacion` con `numeroCuenta`/`banco`/
  `moneda` en blanco que hacen falta para O-03 y O-05 se arman con `ctx.excel_ad_hoc()` (el mismo
  mecanismo que ya usan R-09/R-14/R-18), NO a mano: quedan en `pruebas/adjuntos/` como insumo
  reutilizable.
- Confirmar que exista una versión ACTIVA de `plantilla.obligatoriedad` (§8.2: su canon es "la
  versión activa más alta", no un valor fijo — este archivo solo lo COMPRUEBA, nunca lo siembra).
  Al momento de escribir esto (`ver_solicitud.py --parametros`, 2026-09-27): `sanic_version=2`, con
  `porGestion.Exclusion` sembrado y marcado **PROVISIONAL** — pendiente de confirmación del
  aprobador (O-02 reporta `N-A` si esto cambiara y `porGestion.Exclusion` dejara de estar).
- `dotnet` disponible en el PATH, para los casos unitarios (O-04, O-06, O-07): `dotnet test
  tests/Sanic.Mppp.Plugins.Tests --filter <FullyQualifiedName>`.

---

### O-01 — Compatibilidad: un parámetro SIN `porGestion` se comporta igual que antes de DN-03

**Qué prueba.** La garantía explícita del diseño: "un parámetro sin `porGestion` se comporta
EXACTAMENTE como hoy". Es la condición bajo la que lo ya sembrado en los tres entornos sigue siendo
válido sin migrar nada. La prueba EN VIVO de una gestión sin ajuste ya existe en el propio motor de
las ocho reglas (R-06, `03-reglas-de-registro.md`, gestión `Inclusion`, estable v1/v2); este caso no
la repite (ver "Frontera con el set R") — verifica la garantía general a nivel unitario más la
integridad de lo sembrado.

**Necesita.** Confirmar que la versión 1 de `plantilla.obligatoriedad` (`{"porDefecto":
"obligatorio","opcionales":[{"campo":"referencia"}]}`, sin `porGestion`) sigue existiendo intacta
(no hace falta que sea la ACTIVA para este caso: alcanza con que la fila exista — ver O-08 para el
chequeo de que ninguna versión se edita). `dotnet` en el PATH (ver "Preparativos comunes").

**Cómo.** (1) `dotnet test` con `--filter` exacto sobre
`ParametrosDePlantillaAceptacion.DN03_un_parametro_sin_porGestion_se_comporta_exactamente_como_hoy`
(la garantía general: con y sin `reglas`, ninguna combinación cambia). (2) `GET` de solo lectura
sobre `sanic_mppp_tbl_parametros` filtrando `sanic_nombre eq 'plantilla.obligatoriedad' and
sanic_version eq 1`.

**Qué tiene que pasar.** (1) La prueba unitaria pasa, y el filtro encontró exactamente 1 prueba (si
encontrara 0, el filtro está mal escrito y el caso mentiría "OK" sin haber corrido nada). (2) La
fila de la versión 1 existe y su `sanic_valor`, interpretado como JSON, es idéntico al de DD-16.

**Cómo se verifica.** La salida de `dotnet test` (recuento Failed/Passed/Total) y la respuesta de
esa consulta `GET`.

**Cómo se sabe que falló.** La prueba unitaria falla (una regresión real en el dominio), el filtro
encontró 0 o más de 1 prueba (el nombre del `[Fact]` cambió sin actualizar este caso), o la versión
1 no existe o su valor cambió (alguien la editó en vez de agregar una versión nueva — ver O-08).

**Automatizable.** Unit (`dotnet test`) + API de solo lectura.

---

### O-02 — El contenido sembrado de `porGestion.Exclusion` es exactamente el pedido (marcado PROVISIONAL)

**Qué prueba.** Que la versión ACTIVA hoy de `plantilla.obligatoriedad` trae, dentro de
`porGestion.Exclusion`, exactamente los tres campos que pidió el aprobador (`numeroCuenta`, `banco`,
`moneda`) como `opcionales`, y ninguno como `obligatorios` — ni de más ni de menos. **La prueba EN
VIVO de que ese aflojamiento funciona contra una Solicitud real ya la hace R-18**
(`03-reglas-de-registro.md`): este caso es sobre el CONTENIDO del parámetro sembrado, de solo
lectura, no vuelve a mandar un Excel (ver "Frontera con el set R").

**Necesita.** `plantilla.obligatoriedad` en su versión activa más alta (§8.2: no se siembra nada
acá). **Este criterio está marcado PROVISIONAL en el propio playbook**
(`playbooks/datos/parametros.md`): pendiente de confirmación de negocio del aprobador. Si el
aprobador todavía no confirmó o `porGestion.Exclusion` dejó de estar sembrado, este caso reporta
`N-A` en vez de asumir un valor que ya no existe.

**Cómo.** `GET` de solo lectura sobre `sanic_mppp_tbl_parametros` (versión activa más alta de
`plantilla.obligatoriedad`, la misma consulta que ya hace `preparacion_comun.verificar_parametro_activo`),
parseando su `sanic_valor` como JSON y mirando `porGestion.Exclusion`.

**Qué tiene que pasar.** `porGestion.Exclusion.opcionales` es EXACTAMENTE el conjunto
`{numeroCuenta, banco, moneda}` (nombrados, no contados: CONVENCIONES §7), sin `obligatorios`, y
ninguna otra gestión (`Inclusion`, `Modificacion`) tiene entrada todavía (ver O-03).

**Cómo se verifica.** La respuesta de esa consulta `GET`, ya parseada.

**Cómo se sabe que falló.** Falta alguno de los tres campos, sobra alguno, o `Exclusion` trae
`obligatorios`: el parámetro sembrado no es el que el aprobador pidió, y R-18 (que sí manda un
Excel real) podría estar pasando por una razón distinta a la documentada.

**Automatizable.** API de solo lectura.

---

### O-03 — `Inclusion` y `Modificacion` NO tienen entrada en `porGestion` todavía

**Qué prueba.** Solo `Exclusion` está sembrada en `porGestion` hoy; `Inclusion` y `Modificacion`
siguen bajo el bloque global sin ningún ajuste.

**Necesita.** La misma versión activa que O-02 (se relee, no se asume el resultado de O-02: si mientras
tanto `Inclusion`/`Modificacion` ganaron una entrada en `porGestion`, este caso lo detecta y reporta
`N-A` en vez de mentir un `FALLA` contra una forma del parámetro que ya cambió).

**Cómo.** Un Excel ad hoc (`ctx.excel_ad_hoc`, no a mano) con dos filas: `gestion=Inclusion` y
`gestion=Modificacion`, las dos con `numeroCuenta`/`banco`/`moneda` en blanco.

**Qué tiene que pasar.** Las dos filas se rechazan por `OBLIGATORIEDAD` (esos campos siguen siendo
obligatorios para estas dos gestiones): comportamiento idéntico al de antes de DN-03.

**Cómo se verifica.** `sanic_estado` y `sanic_mensaje` de las dos filas (cita al menos uno de
`numeroCuenta`/`banco`/`moneda`).

**Cómo se sabe que falló.** Alguna fila NO se rechaza, o se rechaza sin citar ninguno de esos tres
campos: `porGestion` se estaría aplicando a una gestión que no tiene entrada sembrada (contagio
entre gestiones que el diseño no pide).

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
campo en las dos listas de la misma entrada (gana `obligatorios`). Los tres viven en
`ParametrosDePlantillaAceptacion.cs`: (a)+(b) en
`DN03_porGestion_afloja_con_opcionales_y_endurece_con_obligatorios_la_gestion_gana_sobre_lo_global`
(gestión `Exclusion` = solo opcionales, gestión `Modificacion` = solo obligatorios) y (c) en
`DN03_si_un_campo_esta_en_opcionales_y_en_obligatorios_de_la_misma_gestion_gana_obligatorios`.

**Qué tiene que pasar.** Los tres escenarios ya están cubiertos en la suite de plugins, y las dos
pruebas pasan.

**Cómo se verifica.** (1) Que el código fuente de `ParametrosDePlantillaAceptacion.cs` siga
declarando esos dos nombres de `[Fact]` (si se renombraron, este caso lo detecta en vez de correr un
filtro que no encuentra nada y "pasa" en falso). (2) `dotnet test
tests/Sanic.Mppp.Plugins.Tests --filter "FullyQualifiedName=<FQN1>|FullyQualifiedName=<FQN2>"`
sobre esos dos nombres exactos — el filtro tiene que encontrar EXACTAMENTE 2 pruebas.

**Cómo se sabe que falló.** Falta alguno de los dos `[Fact]` en el archivo, el filtro encuentra un
número de pruebas distinto de 2 (nombre cambiado sin avisar), o alguna de las dos falla.

**Automatizable.** Unit (no Dev).

---

### O-05 — Gestión inválida en la FILA: `OBLIGATORIEDAD` ni siquiera se evalúa

**Qué prueba.** "Una fila cuyo valor de gestión no es válido SOLO calza con `*` en `reglas`, y NUNCA
mira `porGestion`" — y, más preciso todavía que la redacción original de este caso:
`OBLIGATORIEDAD` **depende de `LISTAS_VALIDAS`** (`playbooks/datos/reglas.md`, `sanic_dependede` de
`OBLIGATORIEDAD` = `"AUTORIZACION_CORREO_PLAN,LISTAS_VALIDAS"`; DD-13, "las reglas de registro
guardan los motivos de lo que falló, sin historial por regla"). Con una gestión inválida,
`LISTAS_VALIDAS` YA rechaza esa fila, así que `OBLIGATORIEDAD` **no se evalúa en absoluto** — no es
que se evalúe "con el bloque global": directamente no corre, y por eso no puede sumar ningún motivo
derivado de `porGestion` (ni aflojado ni endurecido).

**Necesita.** Un Excel con una fila con un valor de `gestion` que no es
`Inclusion`/`Exclusion`/`Modificacion` (p. ej. `"Baja"`), y ADEMÁS con `numeroCuenta`/`banco` en
blanco (para poder distinguir, por el mensaje, si `OBLIGATORIEDAD` corrió o no).

**Cómo.** Un Excel ad hoc (`ctx.excel_ad_hoc`) con esa única fila, mandado contra las Custom API.

**Qué tiene que pasar.** La fila se rechaza citando el campo `gestión` (el único motivo:
`LISTAS_VALIDAS`); el mensaje NO cita `numeroCuenta` ni `banco` — la prueba de que `OBLIGATORIEDAD`
nunca llegó a correr, con o sin `porGestion`.

**Cómo se verifica.** `sanic_estado` y `sanic_mensaje` de esa fila.

**Cómo se sabe que falló.** El mensaje cita `numeroCuenta` o `banco` (señal de que `OBLIGATORIEDAD`
sí corrió pese a la dependencia declarada — un cambio de comportamiento no documentado), o no cita
`gestión` en absoluto.

**Automatizable.** API.

---

### O-06 — Las CLAVES de `porGestion` (dentro del parámetro) no distinguen caja ni tildes (unitario, no Dev)

**Qué prueba.** Cuidado con una trampa de redacción que tenía la versión anterior de este caso: esto
**no** es sobre lo que un cliente escribe en la columna `Gestión` del Excel — eso ya lo normaliza
`ListasPlantilla` (D-18a) ANTES de llegar a `ObligatoriedadPlantilla`, así que un Excel con
`"exclusión"` en la celda se comporta igual con o sin DN-03 y no ejercitaría nada nuevo. Lo que
DN-03 agrega es la comparación sin distinguir caja ni tildes de las **claves del propio JSON**
sembrado en `plantilla.obligatoriedad` (p. ej. que `"porGestion":{"EXCLUSION":{...}}` se reconozca
como la gestión `Exclusion`) — y eso solo se puede ejercitar con una forma de parámetro que hoy NO
está sembrada en ningún entorno.

**Necesita.** **No sembrar una versión nueva en Dev para esto**: cambiar la CLAVE con la que está
sembrado `porGestion` en Dev no aporta nada que un Excel no pueda ya probar con la forma actual, y sí
suma el mismo riesgo de O-04/O-07 (tocar la versión activa de todo el entorno) sin necesidad. `dotnet`
en el PATH.

**Cómo.** `dotnet test` con `--filter` sobre
`ParametrosDePlantillaAceptacion.DN03_los_nombres_de_gestion_de_porGestion_se_comparan_sin_distinguir_caja_ni_tildes`
(un `[Theory]` con las variantes `"  exclusión  "`, `"EXCLUSION"` y `"ExclusioN"` como CLAVE del
JSON, no como valor de fila).

**Qué tiene que pasar.** El filtro encuentra al menos una prueba y todas pasan: las tres variantes
de clave resuelven a la misma gestión (`Exclusion`) y aplican el mismo aflojamiento.

**Cómo se verifica.** La salida de `dotnet test` (recuento Failed/Passed/Total).

**Cómo se sabe que falló.** El filtro no encuentra ninguna prueba (el nombre cambió y este caso
quedó "verde" sin haber corrido nada), o alguna variante falla.

**Automatizable.** Unit (`dotnet test`), no Dev.

---

### O-07 — Gestión desconocida DENTRO de `porGestion` es error de configuración (alto riesgo, no sembrar en Dev)

**Qué prueba.** Un typo en la CLAVE del parámetro (p. ej. `"Exclusio"` en vez de `"Exclusion"`) es
un `FormatException` al leer el parámetro — error ruidoso de configuración, distinto de O-05 (que es
sobre el VALOR de la fila, no sobre el parámetro).

**Necesita.** **NO sembrar esto en Dev de rutina**: un `plantilla.obligatoriedad` con esta clave
mal escrita, si se convierte en la versión activa, rompe la validación de TODA Solicitud del
entorno hasta que alguien lo note y siembre una versión corregida (más alta). Es exactamente el
"falla cerrado a propósito" del diseño, pero contra un entorno compartido el costo es alto.

**Cómo.** Lo que corre `herramientas/pruebas/sets/set_o.py` (arnés API, sin navegador) es SOLO la
mitad unitaria, a propósito: (1) confirma que el archivo de pruebas sigue trayendo un caso con una
gestión desconocida DENTRO de `porGestion` (distinto de O-05, que es sobre el valor de la FILA), y
(2) corre con `dotnet test --filter` el `[Theory]`
`ParametrosDePlantillaAceptacion.Una_obligatoriedad_mal_cargada_revienta_con_el_motivo` completo (la
clase entera de "parámetro mal cargado", que incluye esa fila de datos). **La mitad de alto riesgo
contra Dev de verdad (sembrar `"porGestion":{"Exclusioon":{...}}` y mandar una Solicitud) NO se
automatiza acá**: si algún día hace falta la confirmación contra un entorno real, se hace a mano, en
una ventana coordinada, con una versión corregida lista para sembrar de inmediato después — y se
documenta el resultado en la tabla de abajo con la fecha y quién la corrió.

**Qué tiene que pasar.** El `[Theory]` completo pasa (incluida la fila de la gestión desconocida): un
parámetro con una clave de `porGestion` que no es ninguna gestión válida revienta con
`FormatException` y un motivo que nombra la clave, nunca una lista "vacía pero válida".

**Cómo se verifica.** La salida de `dotnet test` (recuento Failed/Passed/Total).

**Cómo se sabe que falló.** El caso de la gestión desconocida desapareció del archivo de pruebas
(la búsqueda del literal no lo encuentra), o el `[Theory]` completo (o esa fila en particular) falla.

**Automatizable.** Unit (`dotnet test`, lo que corre este archivo) / manual de alto riesgo contra
Dev (fuera de este arnés, ver "Cómo").

---

### O-08 — Versionar es AGREGAR una fila, nunca editar la vigente

**Qué prueba.** DD-02/la nota de `playbooks/datos/parametros.md`: cambiar una regla de negocio
siembra `sanic_version` más alto como fila NUEVA; la anterior queda intacta, de respaldo/histórico,
y el motor resuelve la activa por `sanic_version` descendente con `statecode` activo.

**Necesita.** Nada que crear: alcanza con leer lo ya sembrado.

**Cómo.** `GET sanic_mppp_tbl_parametros?$select=sanic_version,sanic_valor,statecode&$filter=sanic_nombre
eq 'plantilla.obligatoriedad'&$orderby=sanic_version asc` (mismo patrón de lectura que usa
`preparacion_comun.py`, sin ningún `POST`/`PATCH`).

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

**Necesita.** Una fila de gestión `Inclusion` con `numeroCuenta` vacío (sigue siendo obligatoria
para esa gestión — O-03): la fila 2 ("Sin Cuenta") de `E06-obligatoriedad.xlsx`, que
`generar_excel.py` YA genera para R-06 — se reutiliza tal cual, sin armar un Excel nuevo
(CONVENCIONES §9).

**Cómo.** Mandar ese Excel (`E06-obligatoriedad.xlsx`) y leer `sanic_mensaje` de la fila 2.

**Qué tiene que pasar.** El mensaje nombra el campo (`numeroCuenta`) exactamente como antes de
DN-03, sin mencionar "gestión", "Exclusion" ni ninguna palabra de configuración interna.

**Cómo se verifica.** `ver_solicitud.py --ultima`.

**Cómo se sabe que falló.** El mensaje cambió de forma, o menciona la gestión o el hecho de que
otra gestión no exigiría este campo.

**Automatizable.** API.

---

### O-10 — La regla `OBLIGATORIEDAD` no cambia de código, orden, nivel ni dependencias

**Qué prueba.** DN-03 es explícito: "No cambia de código, de orden, de nivel ni de efecto. Solo
cambia qué considera obligatorio" — mismo principio que DN-01 sobre `AUTORIZACION_CORREO_PLAN`.
**Corrección sobre la redacción original de este caso**: DD-13 (`diseno/02-diccionario-datos.md`)
dice que las reglas de REGISTRO (las ocho de `ReglasDeRegistro.cs`, `OBLIGATORIEDAD` entre ellas) NO
dejan un `ResultadoRegla` propio ni un evento de Bitácora por regla — "la fila guarda los motivos de
lo que falló, sin historial por regla". No hay ninguna Bitácora de la que leer "el orden en que se
evaluaron las reglas de una fila": eso solo existe para las cinco reglas de nivel Solicitud (set
`S`). Lo que SÍ tiene código, nivel, orden y dependencias declarados, en vivo, es el catálogo
`sanic_mppp_tbl_reglas` — la misma tabla que ya lee `sets/set_i.py` (I-01..I-04) — y es contra eso
que se verifica este caso, no contra una Solicitud procesada.

**Necesita.** Nada que crear: el catálogo de Reglas ya existe (`preparar_datos.py`/playbooks).

**Cómo.** `GET sanic_mppp_tbl_reglas?$select=sanic_codigo,sanic_nivel,sanic_efecto,sanic_orden,
sanic_dependede,statecode&$filter=sanic_codigo eq 'OBLIGATORIEDAD'`.

**Qué tiene que pasar.** Una única fila, activa, con `sanic_nivel=Registro`, `sanic_efecto=Rechaza`,
`sanic_orden=60` y `sanic_dependede='AUTORIZACION_CORREO_PLAN,LISTAS_VALIDAS'` — exactamente lo que
ya trae `playbooks/datos/reglas.md` desde antes de DN-03.

**Cómo se verifica.** La respuesta de esa consulta `GET`.

**Cómo se sabe que falló.** Cualquiera de esos cinco valores cambió, o hay más de una fila con ese
código: señal de que se reimplementó (otro código, otro orden) en vez de ajustarse, contra la
instrucción explícita del diseño.

**Automatizable.** API de solo lectura.

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
