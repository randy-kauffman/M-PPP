Cierre de DN-05 del proyecto `2026-001-referencias-planes-pago`.

Solución `sanic_mppp_sol_mantenimientoppp` **1.3.0.0** — managed y unmanaged. Ensamblado de plugins **1.0.23.0**.
La versión sube el *minor* por decisión del aprobador, **aunque DN-05 endurece reglas**: hay archivos que antes pasaban
la validación y ahora se rechazan (ver la sección 4). Quien consuma la solución tiene que leer esa sección antes de importar.

Se adjuntan además los **parámetros** y las **reglas** activos en un `.xlsx` cada uno. El valor de `servicio.cuenta` va
enmascarado a propósito: es un UPN de cuenta de servicio y este repositorio es público.

---

## 1. Qué cambió desde v1.2.0

### DN-05 · Escenarios de prueba de Mercadeo BAC y matriz de obligatoriedad
Mercadeo BAC mandó sus escenarios de prueba (0 a 21) y la matriz Gestión × formato del plan. Lo que se construyó:

**Reglas nuevas de fila (nivel Registro, efecto Rechaza):**
- **`CLASIFICACION_DEL_FORMATO`** — **reemplaza a `FORMATO_11_SOLO_ACH`**, que era el caso particular del formato 11.
  El formato 06 admite solo BAC; el 10, BAC o CK; el 11, solo ACH. Si la clasificación viene vacía y el formato admite
  una sola, la fila se guarda con esa; si viene con valor, tiene que estar entre las permitidas.
- **`GESTION_DEL_FORMATO`** — el formato 11 no admite Modificación.
- **`BANCO_DEL_FORMATO`** — los formatos 06 y 10 admiten solo cuentas BAC; con el banco vacío se toma BAC.
- **`CARACTERES_PERMITIDOS`** (escenario 21) — configurable por campo en `plantilla.estructura`. El mensaje muestra
  **solo los caracteres prohibidos, nunca el valor** (la cuenta y la identificación no se exponen).
- **`CUENTA_DEL_BANCO`** (escenario 20) — largo de la cuenta por banco, con rechazo. Tabla en `cuenta.largos`; los
  ceros a la izquierda cuentan.

**Regla cambiada:** `REFERENCIA_FORMATO_11` arma la referencia (banco + cuenta) **solo en Inclusión**; en Exclusión rige
la que manda el cliente. Ahora depende de `CLASIFICACION_DEL_FORMATO`.

**Parámetros:**
- `plantilla.obligatoriedad` **v3** = la matriz del negocio; reemplaza el criterio de exclusión de la v2. En la
  exclusión, cuenta y referencia son obligatorias; la identificación y el banco, opcionales.
- `plan.formatos` (nuevo): qué clasificaciones, gestiones y bancos admite cada formato.
- `cuenta.largos` (nuevo): largo mínimo y máximo por banco; por defecto 6-17.
- `plantilla.estructura` **v2**: define los `caracteres` de cada campo y lleva la cuenta a un máximo de **17**.

**Escenarios cubiertos:** 9 y 10 por la regla de clasificación; 20 por `CUENTA_DEL_BANCO`; 21 por
`CARACTERES_PERMITIDOS`; el 13 queda descartado. **Quedan pendientes del área de operaciones el escenario 9 (canal de
pago por plan) y el 12 (requiere preregistro).**

**Ensamblado:** `1.0.22.0` pasó a **`1.0.23.0`**.

### Orden de despliegue en otro entorno (importante)
Regla: **todo lo que el ensamblado nuevo exige se siembra antes que él, y nada de lo que se siembra antes puede aflojar
al ensamblado viejo.** Este release (managed o unmanaged) lleva el ensamblado y la solución; **los datos (parámetros y
reglas) no viajan en la solución: se cargan con los playbooks, en este orden** (detalle en `diseno/DIRECTRICES-NUEVAS.md`,
DN-05, "Orden de despliegue"):

1. **Parámetros**, con el ensamblado viejo todavía arriba: `plan.formatos`, `cuenta.largos`, `plantilla.estructura` v2 y
   `plantilla.obligatoriedad` v3. Antes de este paso, comprobar con el ensamblado viejo que lee la `plantilla.estructura`
   nueva (validar un correo de prueba).
2. **Ensamblado** `1.0.23.0` (la importación de esta solución).
3. **Catálogo de reglas**, en una sola pasada: las cinco reglas nuevas activas, `REFERENCIA_FORMATO_11` dependiendo de
   `CLASIFICACION_DEL_FORMATO` y `FORMATO_11_SOLO_ACH` inactiva. Una regla activa que depende de una inactiva tumba la
   validación (D-13), por eso va todo junto.

Importar el ensamblado nuevo sin `plan.formatos` y `cuenta.largos` tumba toda la validación: el código falla cerrado.

### Arnés de pruebas
- **Set R reescrito**, con R-29 a R-38 (DN-05).
- E01 pasó a 3 inclusiones.
- El set C ya no doble-codifica la URL de Graph (C-05 y C-06 en verde).
- I-09 espera 15 tipos de plugin (se sumaron `CargarCatalogosApi` y `CambiarEstadoCatalogosApi`).
- Suite de C#: **1895** pruebas en verde (`dotnet test tests/Sanic.Mppp.Plugins.Tests`: 0 fallan, 0 saltadas).
- **Resultados contra Dev, 2026-10-06:**

| Set | Resultado |
|---|---|
| R | 38 OK |
| A | 14 OK · 1 FALLA (A-13: el supervisor de prueba está en inglés; aceptado por el aprobador) |
| B | 19 OK · 1 N-A |
| C | 11 OK |
| E | 8 OK · 2 N-A |
| G | 17 OK |
| I | 13 OK |
| M | 7 OK |
| S | 7 OK |
| U | 11 OK |
| V | 0 OK · 1 FALLA (V-01, D-71 aceptada) · 6 N-A |
| L | **No se volvió a correr** (DN-05 no lo toca). Último resultado: 57 OK. |

---

## 2. Pendientes cerrados (`diseno/PENDIENTES.md`)

**Ninguno de la lista numerada.** Cambios de estado de este release:

| # | Qué | Cómo quedó |
|---|---|---|
| D-71 | Los 4 flujos vivos no tienen desplegado el control de concurrencia. | Pasa a **ACEPTADA tal cual** por el aprobador (2026-10-06): "si le pongo concurrencia 1 el flujo no corre". V-01 sigue en FALLA a sabiendas hasta que se investigue por qué. |
| D-76 | `parametro.py` no desactiva la versión anterior. | **Nueva, abierta** (ver sección 3). |

---

## 3. Lo que queda ABIERTO (`diseno/PENDIENTES.md`)

| # | Qué | Estado |
|---|---|---|
| D-68 | La RED 2 de V-02 (detectar al arrancar un flujo que quedó apagado por una corrida que murió de golpe) nunca se ejercitó. | **Riesgo residual ACEPTADO** por el aprobador (2026-09-28). La RED 1 sí se probó. |
| D-71 | Los 4 flujos vivos (REC, COM, ING, VIG) no tienen desplegado el control de concurrencia que declaran sus `recursos/flujos/*.json`. | **ACEPTADA tal cual** (2026-10-06). Sin él, dos corridas del mismo flujo pueden solaparse. V-01 queda en FALLA conocida. |
| D-76 | `parametro.py` agrega la versión nueva sin desactivar la anterior. | **Abierta.** DD-02 pide una sola versión activa por parámetro. Pasó con `carga.filas.maximo` (1.2.0.0) y con `plantilla.obligatoriedad` y `plantilla.estructura` en DN-05. Mientras no se arregle la herramienta, cada versionado de un parámetro exige desactivar la anterior a mano. |

Además, los límites que ya traía 1.2.0.0 siguen vigentes: el tope de la base de Producción no está medido (el usuario
recibe `MPPP-CAR-008` o `011`, con mensaje claro y sin grabar nada) y la mitad en español de L-04 es N/A porque Dev solo
tiene el idioma 1033.

**Resumen de estado**: **0 pendientes cerrados** en este release; **3 abiertos** (D-68 aceptado, D-71 aceptado, D-76 abierto).

---

## 4. Versión de la solución: `1.3.0.0`

Decisión del aprobador: **1.3.0.0**.

**DN-05 endurece reglas: no es retrocompatible para los archivos que se reciben.** Lo que antes pasaba y ahora se rechaza:

- Exclusión **sin referencia** o **sin cuenta**.
- **Modificación en formato 11.**
- **Caracteres no permitidos** (Ñ, tildes y símbolos fuera de lo configurado por campo).
- **Cuenta fuera del largo de su banco**, y cuenta con caracteres que no son dígitos.
- **Banco que no es BAC** en los formatos 06 y 10.
- **Clasificación no permitida** para el formato del plan.

Lo que cambia es qué filas valida el motor. Quien tenga clientes que mandan archivos tiene que avisarles antes de pasar a producción.

El *ensamblado* de plugins es otro número: pasó de `1.0.22.0` (v1.2.0) a **`1.0.23.0`**.

### Corrección de datos de este release
**DD-02 (una sola versión activa por parámetro).** `plantilla.obligatoriedad` v1 y v2, y `plantilla.estructura` v1,
habían quedado **activas** junto a las versiones nuevas, porque `parametro.py` no desactiva la anterior (D-76). Se
desactivaron. Quedan **22 parámetros activos, 22 nombres distintos, cero duplicados**. (El motor lee la versión activa
más alta, así que el comportamiento no cambiaba, pero el catálogo estaba fuera de la regla y R-26 fallaba.)

**Error propio, reconocido:** el 2026-10-06 la v1 de `plantilla.obligatoriedad` se **reactivó por error**, por una
lectura equivocada de R-26, y se volvió a desactivar el mismo día. R-26 se reescribió contra DD-02: una sola versión
activa por parámetro, en toda la tabla.

---

## 5. Artefactos de este release, en `solution/`

| Archivo | Qué es | Verificado |
|---|---|---|
| `sanic_mppp_sol_mantenimientoppp_1_3_0_0_dn05_20261006_unmanaged.zip` | Export unmanaged (`ExportSolution`, `Managed: false`), publicado antes de exportar (`PublishAllXml`). | `solution.xml`: `Version=1.3.0.0`, `Managed=0`. El DLL de plugins dentro del `.nupkg` trae `1.0.23.0` y ninguna otra versión del proyecto. |
| `sanic_mppp_sol_mantenimientoppp_1_3_0_0_dn05_20261006_managed.zip` | Mismo export, `Managed: true`. | `solution.xml`: `Version=1.3.0.0`, `Managed=1`. Mismo DLL `1.0.23.0`. |
| `sanic_mppp_tbl_parametros_activos_2026-10-06.xlsx` | Los **22** parámetros activos. Columnas: Nombre, Version, Tipo, Valor, Descripcion. | Contra el de 2026-10-02: dos filas nuevas (`plan.formatos` v1 y `cuenta.largos` v1, JSON); `plantilla.estructura` pasa de v1 a v2; `plantilla.obligatoriedad` de v2 a v3. Ningún parámetro quitado. `servicio.cuenta` enmascarado; ningún otro valor ni descripción trae cuenta, dominio, tenant id ni URL. |
| `sanic_mppp_tbl_reglas_activas_2026-10-06.xlsx` | Las **19** reglas activas (2 Correo, 5 Solicitud, 12 Registro). Columnas: Codigo, Nivel, Orden, Efecto, Dependencias, Estado. | Contra el de 2026-10-02: cinco reglas nuevas (`CARACTERES_PERMITIDOS` 25, `GESTION_DEL_FORMATO` 45, `CLASIFICACION_DEL_FORMATO` 50, `BANCO_DEL_FORMATO` 55, `CUENTA_DEL_BANCO` 90); sale `FORMATO_11_SOLO_ACH` (inactiva, no aparece); `REFERENCIA_FORMATO_11` ahora depende de `CLASIFICACION_DEL_FORMATO,OBLIGATORIEDAD`. El resto, idéntico. |

El `.nupkg` también lleva `Sanic.Mppp.Plugins.Paquete.dll` (versión 1.0.0.0): es el ensamblado vacío que genera el
proyecto del paquete, ya presente en v1.1.0; no tiene código.

### Después de importar esta solución en otro entorno
1. Cargar los datos en el orden de la sección 1 (parámetros, ensamblado, reglas). Sin eso, la validación falla cerrada.
2. Si el entorno tenía la solución anterior, regenerar la metadata del ribbon (`&ribbondebug=true` > Command Checker >
   Regenerate ribbon metadata), DESPUÉS de la importación, por si algún menú de DN-04 aparece con "LabelText". DN-05 no
   toca el ribbon.

---

## 6. Cómo verificar lo de arriba de nuevo

```
python3 - <<'PY'
import zipfile, io, re
for n in ["unmanaged", "managed"]:
    z = zipfile.ZipFile(f"solution/sanic_mppp_sol_mantenimientoppp_1_3_0_0_dn05_20261006_{n}.zip")
    sol = z.read("solution.xml").decode()
    nupkg = [x for x in z.namelist() if x.endswith(".nupkg")][0]
    dll = zipfile.ZipFile(io.BytesIO(z.read(nupkg))).read("lib/net462/Sanic.Mppp.Plugins.dll")
    print(n, re.search(r"<Version>([^<]+)</Version>", sol).group(1),
          re.search(r"<Managed>([^<]+)</Managed>", sol).group(1),
          sorted(set(re.findall(r"1\.0\.\d{1,2}\.0", dll.decode("utf-16-le", "ignore")))))
PY
```

La suite de C# y los sets de este release contra Dev:

```
dotnet test tests/Sanic.Mppp.Plugins.Tests
python3 herramientas/pruebas/correr.py R
```
