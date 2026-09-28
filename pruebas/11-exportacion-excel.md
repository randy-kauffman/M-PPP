# 11 — DN-02: descargar a Excel lo que hay que digitar

Cubre el comando *Descargar para digitar*, la Custom API `sanic_mppp_capi_exportarparadigitar`, el
orden de columnas por `exportacion.columnas`, el corte por presupuesto de tiempo, y el riesgo
conocido de memoria a volumen extremo. Fuente: `diseno/DIRECTRICES-NUEVAS.md` DN-02,
`src/Sanic.Mppp.Plugins/Api/ExportarParaDigitar.cs` y `ExportarParaDigitarApi.cs`.

Convención de IDs: prefijo `E-` (**E**xportación), ver el encabezado de `09-rpa-fase2.md`.

## Preparativos comunes a este archivo

Este set es autocontenido (CONVENCIONES §8): no supone ninguna Fila, Solicitud ni configuración que
haya dejado otro set. `herramientas/pruebas/sets/set_e.py` deja listo, en su propio `preparar()`:

- El fixture `PR*` compartido (create-if-missing) y su propio remitente de prueba autorizado sobre
  `PR11` (mismo criterio que `set_s.py`/`set_i.py`).
- Los 15 Excel + el adjunto que no es Excel, en `pruebas/adjuntos/`.
- Que `exportacion.columnas` tenga al menos una versión activa (si no existe ninguna, el set no
  arranca: sembrar el CONJUNTO de columnas es una decisión de negocio, no algo que un set de pruebas
  deba inventar — ver `playbooks/datos/parametros.md`).
- **Su propio fixture de Filas reales**: una Solicitud simulada con `E01-feliz-formato11.xlsx` (tres
  filas válidas — Inclusión, Exclusión, Modificación — que pasan todas las reglas y quedan en
  Validada), creada con `ctx.procesar()`. Todos los casos de API que necesitan `filaid`s reales usan
  ESTE fixture, nunca datos de otro set ni una fila cualquiera del entorno.

- `local/sesion-ejecutivo.json` (rol `SR - MPPP - Ejecutivo`) o `local/sesion-supervisor.json`
  (rol `SR - MPPP - Supervisor`) para el caso de GUI — generadas por el aprobador, ver
  `pruebas/gui/README.md`; confirmarlas con `estado_sesiones.py`. El arnés de Playwright de
  `pruebas/gui/` **ya existe y ya cubre la descarga de punta a punta**
  (`casos/dn-02-descargar-para-digitar.spec.mjs`): los casos de acá que dependen de sesión real
  APUNTAN a ese arnés en vez de duplicarlo.
- Ninguno de los casos de acá toca `pruebas/gui/`.

---

### E-01 — El comando "Descargar para digitar" se dibuja en el ribbon

**Qué prueba.** Que el botón exista de verdad en el DOM, no solo en el servidor — la lección cara
del 2026-09-27 (ver `05-app-y-comandos.md` A-15): un comando puede estar publicado y no dibujarse.

**Necesita.** `local/sesion-ejecutivo.json` (rol `SR - MPPP - Ejecutivo`) o
`local/sesion-supervisor.json` (rol `SR - MPPP - Supervisor`) — cualquiera de las dos sirve, ambas
tienen el privilegio de la Custom API (ver E-09); confirmarla viva y con el rol correcto con
`estado_sesiones.py` antes de correr Playwright.

**Cómo.** Ya está automatizado: `MPPP_USUARIO=ejecutivo npx playwright test dn-02` (o
`ribbon-fila`) desde `pruebas/gui/`.

**Qué tiene que pasar.** El botón "Descargar para digitar" presente y habilitado con filas
seleccionadas.

**Cómo se verifica.** El resultado de esa corrida de Playwright.

**Cómo se sabe que falló.** Ver A-15 (`05-app-y-comandos.md`): distinguir AUSENTE (metadata de
ribbon) de DESHABILITADO (regla de habilitación) — son diagnósticos opuestos.

**Automatizable.** GUI — ya construido en `pruebas/gui/casos/`, no se reescribe acá.

---

### E-02 — Exportación básica: `archivo` y `exportadas`

**Qué prueba.** El contrato mínimo: la Custom API devuelve un `.xlsx` en base64 y cuántas filas
quedaron en el archivo.

**Necesita.** 2-3 `filaid` reales conocidos.

**Cómo.** `dataverse_api.py POST sanic_mppp_capi_exportarparadigitar
'{"filaids":["<id1>","<id2>"]}'` (con sesión de Ejecutivo/Supervisor o el service principal, según
qué identidad tenga el privilegio — ver E-09).

**Qué tiene que pasar.** `exportadas: 2`, `archivo` no vacío; decodificado de base64, un `.xlsx`
válido (`unzip -l` o `herramientas/pruebas/verificar` si existe una utilidad, o abrirlo con
`openpyxl`/Excel).

**Cómo se verifica.** El campo `exportadas` de la respuesta, y abrir el archivo decodificado.

**Cómo se sabe que falló.** `exportadas` no coincide con la cantidad pedida, o el base64 no decodifica
a un `.xlsx` válido.

**Automatizable.** API.

---

### E-03 — El orden de columnas sigue `exportacion.columnas`, sin tocar código

**Qué prueba.** El parámetro decide el orden; cambiarlo cambia el Excel sin ningún despliegue.

**Necesita.** El valor de la versión ACTIVA actual de `exportacion.columnas` (lo lee el propio caso).
El caso siembra su propia versión de prueba, con las dos primeras columnas intercambiadas, en
`sanic_version` = la MÁXIMA versión ya sembrada (activa o no) + 1 — nunca edita ni borra ninguna fila
que no sembró él, mismo criterio de versionado que `03-reglas-de-registro.md` R-26 y que
CONVENCIONES §8.2. Usar la máxima sembrada (no solo la activa) importa: si una corrida murió antes de
limpiar y dejó una versión de prueba colgando, "activa + 1" volvería a chocar con esa fila y la clave
alterna `KEY - MPPP - Parámetro - Nombre y versión` rechazaría el alta con HTTP 412.

**Limpieza en DOS pasos (CONVENCIONES §8.2: "desactivar no es limpiar").** El 2026-09-27, tres
corridas de este caso solo desactivaban la versión de prueba y nunca la borraban: quedaron tres
filas colgando en Dev, y la cuarta corrida chocó con la clave alterna (HTTP 412) porque calculaba
la versión nueva contra la activa en vez de contra el máximo sembrado. Basura que se acumula no es
inofensiva. Por eso la limpieza real son dos pasos, en dos momentos distintos:

1. **Desactivarla de inmediato**, en el `finally` de `e03()` — devuelve la precedencia al canon sin
   esperar a que el set entero termine, pase lo que pase con el resto del caso.
2. **Borrarla por GUID**, en `cerrar()` — nunca por nombre ni por versión (así fue como se borraron
   datos ajenos el 2026-09-27, CONVENCIONES §5). `cerrar()` relee la fila antes de cada DELETE y
   confirma que sigue desactivada y que trae la marca `ZZP -` de este caso; si no, NO la borra y
   reporta el motivo en vez de arriesgar borrar algo que no es de este set.

**Cómo (lo hace el caso, `e03()` + `cerrar()` de `set_e.py`).**
1. Exportar 1 fila del fixture propio con el orden actual, anotar los encabezados del `.xlsx`.
2. Sembrar una versión NUEVA de `exportacion.columnas` (máxima sembrada + 1) con las dos primeras
   columnas intercambiadas — nunca tocar ninguna fila existente.
3. Exportar la misma fila de nuevo.
4. En el `finally` de `e03()`, sea cual sea el resultado: DESACTIVAR (`statecode=1`) la versión de
   prueba que sembró en el paso 2 (paso 1 de la limpieza). La versión anterior queda intacta y
   vuelve a ser la activa más alta — ese es el canon real de este parámetro, y por eso no hace falta
   "restaurar" ningún valor.
5. En `cerrar()` (corre siempre, al final del set): releer esa misma fila por GUID, confirmar que
   sigue desactivada y marcada, y BORRARLA (paso 2 de la limpieza).

**Qué tiene que pasar.** El orden de columnas del paso 3 refleja el cambio del paso 2, sin haber
tocado el ensamblado. Al terminar el SET (no el caso: la fila vive hasta `cerrar()`), no queda
ninguna versión de prueba de `exportacion.columnas` en el entorno — ni activa ni desactivada.

**Cómo se verifica.** Comparar los encabezados del `.xlsx` de los pasos 1 y 3; y, después de correr
el set completo (`correr.py`, no solo este caso), que `sanic_mppp_tbl_parametros` filtrado por
`sanic_nombre eq 'exportacion.columnas'` solo devuelve la versión canónica.

**Cómo se sabe que falló.** El orden no cambia (el código está usando un orden fijo en vez de leer
el parámetro). Aparte, si después de una corrida completa del set queda una fila de PRUEBA (activa o
desactivada) de `exportacion.columnas`: la limpieza de dos pasos no funcionó — ver `cerrar()` en
`set_e.py`, que reporta el motivo en vez de fallar en silencio.

**Automatizable.** API.

---

### E-04 — `filaids` vacío: archivo válido con cero filas de datos

**Qué prueba.** Un arreglo vacío no es un error: se exporta un archivo con solo el encabezado.

**Necesita.** Nada más que la Custom API sembrada.

**Cómo.** `dataverse_api.py POST sanic_mppp_capi_exportarparadigitar '{"filaids":[]}'`.

**Qué tiene que pasar.** HTTP 200, `exportadas: 0`, `archivo` es un `.xlsx` válido (con encabezado,
sin filas de datos).

**Cómo se verifica.** El código HTTP y el archivo decodificado.

**Cómo se sabe que falló.** Un error 4xx sobre un arreglo vacío (se estaría tratando como caso
inválido cuando el contrato dice que es válido).

**Automatizable.** API.

---

### E-05 — Un `filaid` inexistente revienta LA LLAMADA ENTERA, nombrando cuáles

**Qué prueba.** Decisión del aprobador (2026-09-26): nunca se omite en silencio una fila pedida que
no existe — un archivo "completo" con una fila de menos es peor que un rechazo ruidoso.

**Necesita.** Un `filaid` real y un GUID inventado.

**Cómo.** `dataverse_api.py POST sanic_mppp_capi_exportarparadigitar
'{"filaids":["<id real>","00000000-0000-0000-0000-000000000000"]}'`.

**Qué tiene que pasar.** Rechazo (no HTTP 200) con un mensaje que cita el GUID inventado y dice
"volvé a seleccionar y descargá de nuevo".

**Cómo se verifica.** El mensaje de error de la llamada.

**Cómo se sabe que falló.** La llamada devuelve 200 con `exportadas: 1` (la fila inexistente se
omitió en silencio): es exactamente el riesgo de integridad que el aprobador detectó y que motivó
sacar el tope de 500 filas.

**Automatizable.** API.

---

### E-06 — Columnas protegidas (identificación, cuenta) salen igual, sin la protección

**Qué prueba.** DN-02 punto 3-4: el Excel decide qué sale por el PARÁMETRO, no por el perfil de
columna — el archivo descargado NO lleva encima la protección de `CSP - MPPP - Datos sensibles`.

**Necesita.** Una Fila con `numeroidentificacion` y `numerocuenta` reales (ficticios); confirmar
que esos campos están en `exportacion.columnas`.

**Cómo.** Exportar esa fila y abrir el `.xlsx`.

**Qué tiene que pasar.** Los valores de `numeroidentificacion` y `numerocuenta` aparecen en CLARO
en el archivo, sin enmascarar.

**Cómo se verifica.** Inspección del archivo.

**Cómo se sabe que falló.** Los valores vienen enmascarados o vacíos (alguien agregó una protección
que el diseño explícitamente NO pide — y que dejaría al ejecutivo sin poder digitar en AS400), o al
revés, si el negocio decide sacar esas columnas del parámetro y siguen apareciendo (el parámetro
dejó de mandar).

**Automatizable.** API (con inspección del archivo descargado).

---

### E-07 — Corte por presupuesto de tiempo (antes de que la plataforma reviente)

**Qué prueba.** `ExportarParaDigitar.Ejecutar` corta él mismo a los ~100 s (100 s de margen sobre
el presupuesto síncrono de 120 s), con un mensaje que dice qué hacer — no un error genérico de
plataforma.

**Necesita.** Un volumen de `filaids` lo bastante grande. **Importante, según la medición real ya
hecha (`DIRECTRICES-NUEVAS.md`, tabla de 2026-09-26): con datos reales de Dev, ni 250.000 posiciones
de `filaids` llegaron a acercarse al umbral de 100 s (tardó 40,6 s)** — el corte propio del plugin
NO se ha observado disparar contra un volumen real en este entorno. Repetirlo hoy con más volumen
tiene el mismo riesgo que E-08 (ver abajo). La cobertura confiable y repetible de ESTE mensaje
puntual es la unitaria, con el reloj inyectado (`ExportarParaDigitarApiAceptacion`,
`RelojParaPruebas`), que si fuerza el corte sin esperar casi 2 minutos de verdad.

**Cómo (solo si hace falta la confirmación end-to-end, y con las mismas prevenciones de E-08).**
Repetir `filaids` reales hasta un volumen entre 250.000 y 300.000 posiciones (recordar: el costo se
paga por POSICIÓN, no por fila distinta — `FilasDataverse.PorIds` deduplica antes de leer).

**Qué tiene que pasar.** Rechazo con `PresupuestoDeExportacionAgotandoseException`: menciona cuántas
filas alcanzó a preparar y pide volver a seleccionar menos.

**Cómo se verifica.** El mensaje de la llamada.

**Cómo se sabe que falló.** El mensaje no aparece y en cambio la plataforma corta con un error
técnico crudo (ver E-08: a partir de cierto volumen, es justamente lo que pasa, y es un riesgo
aceptado, no una regresión de este caso).

**Automatizable.** Unit (confiable, ya existe: `tests/Sanic.Mppp.Plugins.Tests/Aceptacion/ExportarParaDigitarApiAceptacion.cs`,
seam `RelojParaPruebas`) / API — pero en `set_e.py` este caso queda **SIEMPRE N-A, sin excepción ni
bandera**: no hay hoy un volumen conocido que dispare el mensaje elegante sin correr el mismo riesgo
de E-08 (arriba de ~250k, antes de llegar a los 100s del corte propio, el sandbox puede morir por
memoria — ver E-08). Forzarlo por API no agrega cobertura real sobre la unitaria, solo riesgo.

---

### E-08 — Riesgo conocido: ~300.000 filas mueren por memoria/sandbox ANTES del mensaje elegante

**Qué prueba.** Documenta el límite real medido el 2026-09-26 (`DIRECTRICES-NUEVAS.md`): entre
250.000 (sano, 40,6 s) y 300.000 `filaids` (falla), el proceso sandbox de la plataforma se cae por
agotamiento de memoria/hosts ANTES de que el chequeo de tiempo del plugin tenga otra vuelta de loop
para cortar con su propio mensaje. A 300.000 el error real fue
`SandboxFabricHostCommunicationException`; a 500.000, `System.OutOfMemoryException`. **Esto es un
riesgo ACEPTADO, no un defecto a corregir en esta fase**: ninguna selección real de este negocio se
acerca a ese volumen (7.500 filas/mes en producción). Resumen del borde medido: **100.000 filas OK
· 250.000 OK en 40,6s · 300.000 revienta por memoria del sandbox**, y revienta ANTES de que el
plugin llegue a su propio corte de 100s — por eso el mensaje elegante de E-07 no sale a ese
volumen. E-08 documenta ese borde, no lo descubre.

**Corrección 2026-09-28.** Decisión del aprobador: *"no importa que degrade el ambiente, en el modo
FULL debería correr esta prueba"*. `correr.py --full` prende `MPPP_E08_ALTO_RIESGO` junto con las
banderas de V — ver `herramientas/pruebas/correr.py --listar` y su docstring. La bandera suelta
sigue funcionando igual: `--full` no la rompe, solo la reemplaza como forma recomendada.

**Necesita.** **NO CORRER DE RUTINA.** Esta llamada tarda ~150-170 s y puede degradar el sandbox de
Dataverse para otros usuarios de Dev mientras corre. Solo se repite si `ExportarParaDigitar.cs`
cambia de forma que pudiera afectar este límite (por ejemplo, si se agrega el chequeo de memoria
que quedó como riesgo abierto). Si se corre: avisar a todo el equipo, y NO en horario de otras
pruebas.

En `set_e.py`, `e08()` está detrás de una bandera explícita: por default da **N-A** con el motivo y
el comando exacto para correrlo a propósito. Solo ejecuta la llamada real con la variable de entorno
puesta ANTES de invocar al motor:

```bash
MPPP_E08_ALTO_RIESGO=si python3 herramientas/pruebas/correr.py E
python3 herramientas/pruebas/correr.py E --full   # equivalente, y la forma recomendada desde 2026-09-28
```

Sin esa variable (una corrida normal, `--todos` incluido), el caso NUNCA hace la llamada de 300.000
`filaids`: un set que degrada el entorno cada vez que alguien lo corre es un set que nadie va a
correr.

**Cómo (solo si se decide repetir la medición, con la bandera de arriba).** Repite un `filaid` real
(del fixture propio de este set) 300.000 veces en el arreglo `filaids` de una sola llamada a
`exportarparadigitar` (el costo se paga por POSICIÓN, no por fila distinta: `FilasDataverse.PorIds`
deduplica antes de leer).

**Qué tiene que pasar hoy.** Falla con un error de plataforma (no el mensaje elegante de E-07): eso
es lo ESPERADO y documentado, no un hallazgo nuevo.

**Cómo se verifica.** El tipo de excepción en la respuesta/log.

**Cómo se sabe que "falló" (en el sentido de que empeoró).** Si el volumen al que revienta bajó
significativamente respecto de la medición de referencia (250k sano / 300k+ revienta): indicaría
una regresión de memoria en el código nuevo, y ahí sí amerita investigar.

**Automatizable.** API, pero detrás de la bandera `MPPP_E08_ALTO_RIESGO=si` (o `--full`, ver
"Necesita" arriba) — alto costo, no forma parte de una corrida de regresión habitual ni de
`--todos` (`--todos` solo, sin `--full`, tampoco la prende).

---

### E-09 — Privilegio de la Custom API: solo Ejecutivo y Supervisor

**Qué prueba.** A diferencia de las Custom API del RPA (puerta en código, D-9), esta usa
`ExecutePrivilegeName = prvReadsanic_mppp_tbl_fila` — un privilegio de plataforma que YA tienen
Ejecutivo y Supervisor, y que Administrador de planes/técnico NO tienen.

**Necesita.** No hace falta abrir un navegador: se resuelve cada usuario de prueba por su alias
(`admin_planes` = `User3`, rol `SR - MPPP - Administrador de planes`; `admin_tecnico` = `User4`, rol
`SR - MPPP - Administrador tecnico`), se verifica el rol contra `systemuserroles_association` (nunca
supuesto — CONVENCIONES §8) y se impersona por `MSCRMCallerID`, el mismo mecanismo de
`comportamiento_roles.py` y de `set_i.py` (`_usuario_por_alias` + `pc.verificar_roles`, igual que
I-13). **Este caso NO es GUI**: no usa `local/sesion-*.json` (esos son `storageState` de Playwright,
para el arnés de `pruebas/gui/`), sino impersonación por API.

**Importante:** la llamada impersonada usa un `filaid` REAL, pero del fixture propio de este set (el
mismo que crea `preparar()` con `E01-feliz-formato11.xlsx`) — nunca una fila cualquiera del entorno.
El privilegio se evalúa a nivel de MENSAJE antes de tocar ningún registro (`ExecutePrivilegeName`),
así que usar un id real y propio prueba lo mismo que un arreglo vacío, pero además confirma que el
rechazo llega ANTES de leer la fila, sin depender de datos de otro set.

**Cómo (`e09()` de `set_e.py`).** Por cada papel (`admin_planes`, `admin_tecnico`): resolver su
`systemuserid` por alias, verificar su rol, y llamar
`sanic_mppp_capi_exportarparadigitar` con `MSCRMCallerID` de ese usuario y
`{"filaids": ["<filaid propio del fixture>"]}`.

**Qué tiene que pasar.** Las dos llamadas rechazadas por falta de privilegio (error de plataforma,
no del plugin).

**Cómo se verifica.** El código HTTP (403) y el mensaje de la plataforma.

**Cómo se sabe que falló.** Alguno de los dos roles SÍ puede exportar: el registro real de la
Custom API (tarea de `pac`/Dev, fuera de este plugin) no quedó con el privilegio correcto.

**Automatizable.** API.

---

### E-10 — `filaids` es un StringArray, no un JSON serializado

**Qué prueba.** El parámetro calza con `SelectedControlSelectedItemIds` del ribbon SIN
transformación: es un arreglo de texto nativo de la Custom API, no un string con JSON adentro. Un
elemento que no es un GUID válido nombra la POSICIÓN exacta.

**Necesita.** Nada especial.

**Cómo.**
1. `dataverse_api.py POST sanic_mppp_capi_exportarparadigitar '{"filaids": "no-es-un-arreglo"}'`
   (mandado como string en vez de arreglo).
2. `dataverse_api.py POST sanic_mppp_capi_exportarparadigitar '{"filaids": ["<id real>", "no-es-un-guid"]}'`.

**Qué tiene que pasar.** Paso 1: rechazo diciendo que tiene que ser un StringArray. Paso 2: rechazo
que nombra la posición 2 como inválida.

**Cómo se verifica.** El mensaje de error de cada llamada.

**Cómo se sabe que falló.** Un mensaje genérico que no dice CUÁL posición está mal: con un arreglo
de cientos de ids seleccionados en la grilla, encontrar el culpable a mano sería inviable.

**Automatizable.** API.

---

## Resultados

| Caso | Resultado | Notas |
|---|---|---|
| E-01 el botón se dibuja | | GUI, ya cubierto en `pruebas/gui/` |
| E-02 exportación básica | | |
| E-03 orden de columnas por parámetro | | |
| E-04 `filaids` vacío | | |
| E-05 `filaid` inexistente revienta entero | | |
| E-06 columnas protegidas salen en claro | | |
| E-07 corte por presupuesto de tiempo | | siempre N-A en `set_e.py`; cobertura real es la unitaria |
| E-08 riesgo de memoria a ~300k | | N-A por default; real solo con `MPPP_E08_ALTO_RIESGO=si` (o `--full`) |
| E-09 privilegio de la Custom API | | |
| E-10 `filaids` StringArray, posición del error | | |
