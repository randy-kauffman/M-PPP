Cierre de DN-04 del proyecto `2026-001-referencias-planes-pago`.

Solución `sanic_mppp_sol_mantenimientoppp` **1.2.0.0** — managed y unmanaged. Ensamblado de plugins **1.0.22.0**.
La versión sube el *minor*: todo lo nuevo es **aditivo y retrocompatible** (dos Custom API nuevas, una columna opcional,
un parámetro nuevo y dos menús que solo ve el Administrador de planes; ningún camino existente cambia).

Se adjuntan además los **parámetros** y las **reglas** activos en un `.xlsx` cada uno. El valor de `servicio.cuenta` va
enmascarado a propósito: es un UPN de cuenta de servicio y este repositorio es público.

---

## 1. Qué cambió desde v1.1.0

### DN-04 · Carga masiva y activación masiva de catálogos por Excel
El Administrador de planes deja de cargar clientes, planes, correos y autorizaciones de a uno en formularios. Dos
menús desplegables nuevos en la grilla de las cuatro tablas de catálogos (Clientes, Planes, Correos Autorizados,
Autorizaciones - Clientes), cada uno con *Cargar archivo* y *Descargar plantilla*:

- **Carga masiva** (`sanic_mppp_capi_cargarcatalogos`): un Excel desnormalizado (hoja `CargaCatalogos`) que crea lo
  que falte —correo, cliente (con su ejecutivo como dueño), plan y autorización por cliente— en orden de dependencias,
  y cada cosa una sola vez aunque se repita en varias filas.
- **Activar/Desactivar masivo** (`sanic_mppp_capi_cambiarestadocatalogos`): un Excel (hoja `ActivarDesactivar`) que
  activa o desactiva clientes, planes, correos y autorizaciones por cliente, registro por registro, sin cascada.

Las dos son **todo o nada**: la pasada 1 valida el archivo entero sin escribir; con un solo error de fila no se graba
nada y se devuelve el Excel con una columna *Resultado* que dice qué corregir en cada fila. La pasada 2 corre dentro de
la transacción de la Custom API (verificado en Dev, D-72): si algo falla, se deshace todo. Escriben **con el usuario
que llama**, así la auditoría dice quién dio de alta o desactivó cada cosa. La plantilla la genera la misma API, con la
misma definición de columnas que valida. Los errores de archivo tienen código propio, de `MPPP-CAR-001` a `011`.

**Columna nueva**: `Comentarios` (texto de 50, opcional, auditada) en la autorización por cliente, en el formulario y en
las dos vistas.

**Parámetro nuevo**: `carga.filas.maximo` = **100**. Se diseñó con 300 y se **midió** en Dev (D-75): el peor caso (cada
fila crea cliente, plan, correo y autorización) graba 100 filas en ~25 s; desde 200 filas la base de Dev corta la
transacción a los ~40 s (error 10928 de Azure SQL). El presupuesto de escritura del plugin bajó de 100 s a **30 s**,
para que corte nuestro mensaje (`MPPP-CAR-008`, "partí el archivo") antes que la base. Una falla de la plataforma —no
de un dato— tiene su propio código, **`MPPP-CAR-011`**, clasificado por el `ErrorCode` de Dataverse y verificado en Dev
contra el error real (`0x80072324`): ya no se le dice al usuario "corregí esa fila" cuando la fila no tiene nada malo.

**Menús en el idioma del usuario.** Los menús llevan etiquetas localizadas (1033 y 3082), como el resto del proyecto.
Al construirlos, un menú con etiquetas localizadas mostró "LabelText" y se concluyó que la plataforma no las soportaba
dentro de un menú desplegable; el aprobador lo dudó y un experimento controlado le dio la razón (D-74): era la metadata
compilada del ribbon sin regenerar.

### Herramientas
- `importar_solucion.py` genera y verifica menús desplegables (`FlyoutAnchor`), con etiquetas localizadas o texto fijo,
  y al importar un ribbon **avisa que hay que regenerar la metadata compilada** (Command Checker > Regenerate ribbon
  metadata): publicar no alcanza, y la regeneración tiene que ir DESPUÉS de la última importación.
- Las diez herramientas que escriben distinguen `actualizado` de `creado`.
- El playbook del paquete de plugins borra `bin/` y `obj/` antes de empaquetar (`dotnet pack` dejaba el `.nupkg` viejo).

### Arnés de pruebas
- **Set L** nuevo (`pruebas/12-catalogos-masivos.md`, 57 casos), con su arnés: `python3 herramientas/pruebas/correr.py L`
  junta la suite de C#, el spec de Playwright en los cuatro roles y la Web API con las sesiones reales de cada usuario.
  Un test saltado nunca cuenta como aprobado. **Resultado: 57 OK · 0 FALLA · 0 N-A**, en las dos corridas reales.
- **206 casos en 12 sets** en total. Suite de C#: **1581** pruebas en verde.
- Navegación del arnés GUI más robusta: espera la entrada del sitemap y reintenta el cambio de área (cerró D-70).

---

## 2. Pendientes cerrados (`diseno/PENDIENTES.md`)

| # | Qué era | Cómo quedó |
|---|---|---|
| D-69 | `AutorizacionCliente` mostraba comandos genéricos aunque el ribbon estaba bien importado. | Era la metadata compilada sin regenerar. Regenerada; `A-02 — AutorizacionCliente` en verde en Dev. |
| D-70 | El arnés GUI no encontraba "Correos Autorizados" en el sitemap. | Carrera en la navegación (`count()` sin esperar tras cambiar de área). Arreglado; `A-02 — Autorizado` en verde. |
| D-72 | ¿Hay transacción en la Custom API? Prerrequisito del todo o nada. | Sí (`IsInTransaction = true`, etapa 30); una excepción deshace todo, con el servicio del usuario y con SYSTEM. |
| D-73 | Prueba con escrituras reales y la sesión del Administrador de planes. | El cliente nuevo queda con el ejecutivo como dueño y el ejecutivo lo ve; el creador es el Administrador de planes. |
| D-74 | ¿Etiquetas multiidioma en menús desplegables? | **Sí funcionan.** Experimento con una tabla de prueba y tres de control; las cuatro tablas pasaron a etiquetas localizadas y desapareció la excepción al multiidioma. |
| D-75 | El tope de 300 filas no se sostenía en Dev. | Tope 100, presupuesto 30 s y `MPPP-CAR-011`, medido y verificado en Dev. El tope de la base de Producción no se mide, por decisión del aprobador. |

---

## 3. Lo que queda ABIERTO (`diseno/PENDIENTES.md`)

| # | Qué | Estado |
|---|---|---|
| D-68 | La RED 2 de V-02 (detectar al arrancar un flujo que quedó apagado por una corrida que murió de golpe) nunca se ejercitó. | **Riesgo residual ACEPTADO** por el aprobador (2026-09-28). La RED 1 sí se probó. |
| D-71 | Los 4 flujos vivos (REC, COM, ING, VIG) **no tienen desplegado** el control de concurrencia que declaran sus `recursos/flujos/*.json`. | **Abierto.** Sin él, dos corridas del mismo flujo pueden solaparse. No lo toca este release. |

Además, dos límites conocidos de este release, sin número de pendiente:

- **El tope de la base de Producción no está medido** (descartado por el aprobador). Si Producción corta antes que Dev,
  el usuario recibe `MPPP-CAR-008` o `011`, los dos con mensaje claro y sin grabar nada.
- **La mitad en español de L-04 es N/A**: Dev solo tiene el idioma 1033 instalado. Los menús se probaron en inglés
  (muestran "Bulk load"); en un entorno con 3082 muestran "Carga masiva".

**Resumen de estado**: **6 pendientes cerrados** (D-69, D-70, D-72 a D-75) y **2 abiertos** (D-68 aceptado, D-71).

---

## 4. Versión de la solución: `1.2.0.0`

Decisión del aprobador (2026-10-01): **1.2.0.0**. Todo lo nuevo es aditivo: dos Custom API nuevas, una columna opcional,
un parámetro nuevo y dos menús visibles solo para el Administrador de planes. Ningún consumidor existente se rompe.

El *ensamblado* de plugins es otro número: pasó de `1.0.19.0` (v1.1.0) a **`1.0.22.0`**. La 1.0.20.0 trajo las dos Custom
API, la 1.0.21.0 el `MPPP-CAR-011` (desplegada a propósito con el presupuesto todavía en 100 s, para reproducir el
error real de la base y confirmar su código) y la 1.0.22.0 el presupuesto de 30 s y el tope de 100.

### Corrección de datos de este release
`carga.filas.maximo` tenía **dos versiones activas** (v1 = 300 y v2 = 100), contra DD-02 ("cambiar de versión desactiva
la anterior"): la herramienta que versiona el parámetro agregó la v2 sin desactivar la v1. Se desactivó la v1. Quedan
**20 parámetros activos, 20 nombres distintos, cero duplicados**. (El plugin ya leía la v2, la versión activa más alta,
así que el comportamiento no cambió.)

---

## 5. Artefactos de este release, en `solution/`

| Archivo | Qué es | Verificado |
|---|---|---|
| `sanic_mppp_sol_mantenimientoppp_1_2_0_0_dn04_20261002_unmanaged.zip` | Export unmanaged (`ExportSolution`, `Managed: false`), publicado antes de exportar (`PublishAllXml`). | `solution.xml`: `Version=1.2.0.0`, `Managed=0`. El DLL de plugins dentro del `.nupkg` trae `1.0.22.0` y ninguna otra versión del proyecto. |
| `sanic_mppp_sol_mantenimientoppp_1_2_0_0_dn04_20261002_managed.zip` | Mismo export, `Managed: true`. | `solution.xml`: `Version=1.2.0.0`, `Managed=1`. Mismo DLL `1.0.22.0`. |
| `sanic_mppp_tbl_parametros_activos_2026-10-02.xlsx` | Los **20** parámetros activos. Columnas: Nombre, Version, Tipo, Valor, Descripcion. | Contra el de 2026-09-28: la única diferencia es la fila nueva `carga.filas.maximo` (v2, Numero, 100). `servicio.cuenta` enmascarado; ningún otro valor ni descripción trae cuenta, dominio, tenant id ni URL. |
| `sanic_mppp_tbl_reglas_activas_2026-10-02.xlsx` | Las **15** reglas activas. Columnas: Codigo, Nivel, Orden, Efecto, Dependencias, Estado. | Idénticas fila por fila a las de 2026-09-28 (DN-04 no tocó reglas). |

El `.nupkg` también lleva `Sanic.Mppp.Plugins.Paquete.dll` (versión 1.0.0.0): es el ensamblado vacío que genera el
proyecto del paquete, ya presente en v1.1.0; no tiene código.

### Después de importar esta solución en otro entorno
Regenerar la metadata del ribbon (`&ribbondebug=true` > Command Checker > Regenerate ribbon metadata), DESPUÉS de la
importación. Sin eso, los menús nuevos no aparecen o aparecen con "LabelText".

---

## 6. Cómo verificar lo de arriba de nuevo

```
python3 - <<'PY'
import zipfile, io, re
for n in ["unmanaged", "managed"]:
    z = zipfile.ZipFile(f"solution/sanic_mppp_sol_mantenimientoppp_1_2_0_0_dn04_20261002_{n}.zip")
    sol = z.read("solution.xml").decode()
    nupkg = [x for x in z.namelist() if x.endswith(".nupkg")][0]
    dll = zipfile.ZipFile(io.BytesIO(z.read(nupkg))).read("lib/net462/Sanic.Mppp.Plugins.dll")
    print(n, re.search(r"<Version>([^<]+)</Version>", sol).group(1),
          re.search(r"<Managed>([^<]+)</Managed>", sol).group(1),
          sorted(set(re.findall(r"1\.0\.\d{1,2}\.0", dll.decode("utf-16-le", "ignore")))))
PY
```

Y el set de este release contra Dev:

```
python3 herramientas/pruebas/correr.py L
```
