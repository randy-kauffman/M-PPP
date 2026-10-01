# Conclusiones: spike D-72, transacción de la operación principal de una Custom API

Medido en Dev (`https://org36e60d9d.crm.dynamics.com/`) el 2026-10-01. El código del spike se descarta; quedan estas conclusiones, `resultados.json` (salida cruda de las 10 llamadas) y los scripts de `scripts/`.

Todos los comandos se corren desde la raíz del repositorio (`/home/gmaker/projects/M-PPP`).

## 1. Respuesta en una línea

**Sí.** La operación principal (Stage 30) de una Custom API Action unbound llamada por el Web API corre dentro de una transacción (`IsInTransaction = true`) y, ante una excepción, se deshacen TODOS los `Create` que hizo, tanto los hechos con el servicio del usuario como los hechos con el servicio de SYSTEM, y también cuando la excepción nace de una violación de clave que el plugin atrapó.

Cómo se obtuvo: `python3 spikes/d72-transaccion-capi/scripts/correr_casos.py` (llama a la Custom API, cuenta por prefijo y lee la traza; deja todo en `spikes/d72-transaccion-capi/resultados.json`).

Salvedad de alcance: el llamador fue el application user de `local/pp_secrets.env` (credenciales de cliente), no un usuario interactivo de la app. El camino es el mismo (`POST /api/data/v9.2/sanic_mppp_capi_zzspiked72`, igual que `Xrm.WebApi.online.execute`), pero no se midió con un usuario con sesión.

## 2. Tabla: 5 casos × 2 corridas

Origen de cada fila: `resultados.json` (campos `respuesta`, `registros_despues`, `trazas[].messageblock`). `entransaccion`, `etapa`, `profundidad` salen de la traza (`plugintracelogs`, escrita ANTES de lanzar) y, en `exito`, además de las salidas de la API. "Registros" es la cuenta con `GET sanic_mppp_tbl_autorizados?$filter=startswith(sanic_nombre,'zz-d72-<marca>-')&$count=true`, hecha justo después de cada llamada.

| modo | corrida | marca | entransaccion | etapa | profundidad | creados (traza) | registros después | HTTP | mensaje que recibió el cliente |
|---|---|---|---|---|---|---|---|---|---|
| exito | 1 | exito1x141245 | true | 30 | 1 | 3 | **3** | 200 | (sin error; salidas `entransaccion=true, etapa=30, profundidad=1, creados=3`) |
| exito | 2 | exito2x141245 | true | 30 | 1 | 3 | **3** | 200 | (ídem) |
| excepcion | 1 | excepcion1x141245 | true | 30 | 1 | 3 | **0** | 400 | `D72-EXCEPCION` |
| excepcion | 2 | excepcion2x141245 | true | 30 | 1 | 3 | **0** | 400 | `D72-EXCEPCION` |
| fallo_escritura | 1 | falloescritura1x141245 | true | 30 | 1 | 2 | **0** | 400 | `D72-FALLO: Entity Key KEY - MPPP - Autorizado - Correo violated. A record with the same value for Correo already exists. A duplicate record cannot be created. Select one or more unique values and try again.` |
| fallo_escritura | 2 | falloescritura2x141245 | true | 30 | 1 | 2 | **0** | 400 | (idéntico al anterior) |
| sistema | 1 | sistema1x141245 | true | 30 | 1 | 3 | **0** | 400 | `D72-EXCEPCION` |
| sistema | 2 | sistema2x141245 | true | 30 | 1 | 3 | **0** | 400 | `D72-EXCEPCION` |
| mixto | 1 | mixto1x141245 | true | 30 | 1 | 4 | **0** | 400 | `D72-EXCEPCION` |
| mixto | 2 | mixto2x141245 | true | 30 | 1 | 4 | **0** | 400 | `D72-EXCEPCION` |

Lecturas que importan:

- Los `Create` SÍ se ejecutaron antes de la excepción (la traza dice `creados=3`, `2` o `4` en el momento de lanzar). Que después haya 0 registros es rollback, no que nunca se escribieran.
- `exito` demuestra que la tabla, la clave y el step existente aceptan estos registros: los 3 se ven después, así que el 0 de los demás casos no se debe a un rechazo previo.
- `mixto` y `sistema`: el servicio de SYSTEM (`CreateOrganizationService(null)`) NO abre una transacción aparte; sus `Create` también se deshacen.
- `etapa` 30 es MainOperation; `profundidad` 1 es la llamada directa del Web API.

Nota de método: la primera tanda de `exito` (marcas `exito1x141108` y `exito2x141108`, 2 × 3 registros, también correctas: HTTP 200, 3 registros) quedó sin traza porque mi script filtraba `plugintracelogs` con `typename eq`; ver tropiezo 2. Se repitió `exito` en la tanda `…x141245` y es la que está en la tabla. Los 12 registros de las dos tandas se borraron en la limpieza.

## 3. ¿`fallo_escritura` deja pasar nuestro mensaje?

**Sí, lo deja pasar.** Con el patrón de la orden (atrapar `FaultException<OrganizationServiceFault>` y lanzar `InvalidPluginExecutionException("D72-FALLO: " + mensaje)` sin hacer ninguna otra llamada al servicio) el cliente recibe, en las dos corridas, HTTP 400 con este texto exacto:

```
D72-FALLO: Entity Key KEY - MPPP - Autorizado - Correo violated. A record with the same value for Correo already exists. A duplicate record cannot be created. Select one or more unique values and try again.
```

No aparece "ISV code reduced the open transaction count" en ninguna corrida. Comando: `python3 spikes/d72-transaccion-capi/scripts/correr_casos.py fallo_escritura` (o `rg -n "D72-FALLO" spikes/d72-transaccion-capi/resultados.json`).

Qué NO se midió (fuera de la orden): el caso en que, después de atrapar el fault, el plugin hace OTRA llamada al servicio. Ese es justamente el que Learn asocia con el error de "open transaction count"; el spike solo confirma que sin esa llamada no ocurre.

## 4. Componentes de `sanic_mppp_sol_mantenimientoppp`, antes y después

Comando (el mismo antes y después): `python3 spikes/d72-transaccion-capi/scripts/cuenta_componentes.py sanic_mppp_sol_mantenimientoppp`. Salidas guardadas en `ANTES_componentes.txt` y `DESPUES_componentes.txt`.

| | total | sha256 del conjunto `(componenttype:objectid)` | momento (UTC) |
|---|---|---|---|
| Antes | **126** | `91898bdb6b890aea1ed87273e856e8e6b01024ccaf94e8f94346cb9780ac7bc4` | 2026-10-01 20:09:15 (antes de crear la solución temporal) |
| Después | **126** | `91898bdb6b890aea1ed87273e856e8e6b01024ccaf94e8f94346cb9780ac7bc4` | 2026-10-01 20:18:39 (después de la limpieza) |

Por tipo, idéntico antes y después: `[[1,13],[9,14],[20,6],[29,5],[61,26],[62,1],[70,1],[80,1],[91,1],[92,20],[380,2],[10036,5],[10037,6],[10038,17],[10039,1],[10091,5],[10150,2]]`. No cambió ni la cuenta ni un solo `objectid`.

## 5. Evidencia de la limpieza

Orden ejecutado con `python3 spikes/d72-transaccion-capi/scripts/limpiar.py` (todo HTTP 204): 12 registros `zz-d72-*`, luego las 2 entradas y las 4 salidas de la Custom API, la Custom API, el paquete (se lleva el ensamblado y el tipo) y la solución temporal.

Verificación: `python3 spikes/d72-transaccion-capi/scripts/verificar_limpieza.py` (salida guardada en `DESPUES_limpieza.txt`):

```
registros zz-d72-*: HTTP 200 count=0
customapis zzspiked72: HTTP 200 count=0
customapirequestparameters del spike: HTTP 200 count=0
pluginpackages zzspiked72: HTTP 200 count=0
pluginassemblies Sanic.Mppp.Spike.D72: HTTP 200 count=0
plugintypes del spike: HTTP 200 count=0
solutions zzspiked72: HTTP 200 count=0
```

**Lo único que quedó en Dev:** 12 filas de `plugintracelogs` del tipo del spike (2 de la tanda sin filtro correcto + 10 de la tanda de la tabla). La orden no las incluía en la limpieza y son registros de sistema, no componentes. Las demuestra:
`python3 herramientas/dataverse_api.py GET "plugintracelogs?\$select=plugintracelogid,createdon&\$filter=startswith(typename,'Sanic.Mppp.Spike.D72')&\$count=true"` (devuelve `@odata.count: 12`). Se pueden borrar con `DELETE plugintracelogs(<id>)` si se quiere.

## 6. Tropiezos con las herramientas

1. **Las herramientas no crean la solución ni borran.** `paquete_plugins.py` y `custom_api.py` exigen que la solución ya exista (`comprobar_solucion_e_idioma`) y nunca borran (a propósito). Para crear `sanic_mppp_sol_zzspiked72` y para toda la limpieza usé el Web API directo con `dataverse_api.py` (`POST solutions` con el `publisherid` de `Sistemas_Abiertos_Nicaragua`; `DELETE` de cada componente). No se modificó ninguna herramienta.
2. **Error mío en el script del spike, no de las herramientas:** `plugintracelogs.typename` guarda el nombre calificado con ensamblado (`Sanic.Mppp.Spike.D72.TransaccionCapiApi, Sanic.Mppp.Spike.D72, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null`). Mi primer filtro `typename eq '<tipo>'` no encontró nada, y la primera tanda de `exito` quedó sin traza. Se corrigió con `startswith(typename,'<tipo>,')` y se repitió todo. La orden dice "filtrar por `typename` del tipo del spike": conviene que diga que es por prefijo.
3. **`sd` no está instalado** (la regla de usuario pide `sd` en lugar de `sed`). No lo instalé; edité con la herramienta Edit.
4. **Los campos `inventario` y `fase` de la Identidad de los playbooks** no tienen valor natural en un spike; puse `"D-72"` y `0` y las herramientas los aceptaron sin objeción (no validan su contenido).
5. **El `.nupkg` lleva dos DLL**: `Sanic.Mppp.Spike.D72.dll` y `Sanic.Mppp.Spike.D72.Paquete.dll` (el proyecto vacío del paquete), igual que en el paquete real. La plataforma registró solo un ensamblado y un tipo (el `IPlugin`); no molestó.
6. **Contexto que podría confundir a quien repita el spike:** existe un step activo `MPPP - Normalizar y validar - Create de Autorizado` (Stage 20) del paquete real, que corre sobre los `Create` anidados del plugin del spike. No alteró los resultados (`exito` dejó los 3 registros con los nombres tal cual) y no se tocó.

## 7. Qué se construyó (todo descartable)

- Solución temporal `sanic_mppp_sol_zzspiked72` (mismo publisher), paquete `sanic_mppp_pkg_zzspiked72`, ensamblado `Sanic.Mppp.Spike.D72`, Custom API `sanic_mppp_capi_zzspiked72`: creados con las herramientas del proyecto y borrados.
- En la carpeta del spike quedan: `src/` (proyecto y paquete, con `bin/` y `obj/` locales), `playbooks/` (los dos playbooks del spike), `scripts/`, `resultados.json`, `ANTES_componentes.txt`, `DESPUES_componentes.txt`, `DESPUES_limpieza.txt` y este archivo. No se tocó `sanic_mppp_pkg_plugins` ni su código, ni se compiló la solución principal.
