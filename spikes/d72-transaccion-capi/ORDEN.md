# Orden: spike D-72, transacción de la operación principal de una Custom API

## Qué es
Un **spike**: el código se descarta y quedan las conclusiones. Responde UNA pregunta que bloquea
construir DN-04 (`diseno/DIRECTRICES-NUEVAS.md`, DN-04; `diseno/PENDIENTES.md`, D-72). No es parte de
la solución y **no puede dejar nada en ella**.

## La pregunta
En Dataverse, cuando el plugin de la **operación principal** de una **Custom API (Action, unbound)**
llamada por el **Web API** (el mismo camino que `Xrm.WebApi.online.execute` desde la app) hace
varios `Create` y después lanza una excepción, **¿se deshacen todos esos `Create`?**

Microsoft Learn lo dice para los steps síncronos (PreOperation y PostOperation, "Handle exceptions in
plug-ins") y para las Custom Actions clásicas con "Enable Rollback" ("Scalable Customization Design:
Database transactions"), pero **no dice nada de la operación principal de una Custom API**. No se da
por cierto: se mide.

## Casos (uno por valor de la entrada `modo`)
Cada caso crea registros en `sanic_mppp_tbl_autorizado` (la tabla que DN-04 va a cargar de verdad):
`sanic_nombre` = `zz-d72-<marca>-<n>@spike.invalid`, `sanic_nombrecontacto` = `ZZ spike D-72`. `marca`
llega como entrada y es distinta en cada llamada, para poder contar después por prefijo.

| `modo` | Qué hace el plugin | Resultado esperado si hay transacción |
|---|---|---|
| `exito` | 3 `Create` con el servicio del usuario que llama (`CreateOrganizationService(context.UserId)`); devuelve las salidas | 3 registros |
| `excepcion` | 3 `Create` con el servicio del usuario y después `throw new InvalidPluginExecutionException("D72-EXCEPCION")` | 0 registros; el cliente recibe `D72-EXCEPCION` |
| `fallo_escritura` | 2 `Create` con el servicio del usuario; un 3.º con el **mismo correo** que el 1.º (choca con la clave `sanic_mppp_key_autorizado_correo`). Atrapa la `FaultException<OrganizationServiceFault>` y lanza `InvalidPluginExecutionException("D72-FALLO: " + mensaje)` **sin hacer ninguna otra llamada al servicio** | 0 registros; el cliente recibe `D72-FALLO: …` (no "ISV code reduced the open transaction count") |
| `sistema` | 3 `Create` con el servicio de SYSTEM (`CreateOrganizationService(null)`) y después la misma excepción que `excepcion` | 0 registros |
| `mixto` | 2 `Create` con el servicio del usuario y 2 con SYSTEM, después la excepción | 0 registros |

**Salidas** (las devuelve el plugin cuando no lanza; cuando lanza, escribir los mismos valores en la
traza ANTES de lanzar): `entransaccion` (Boolean, `context.IsInTransaction`), `etapa` (Integer,
`context.Stage`), `profundidad` (Integer, `context.Depth`), `creados` (Integer).

**Entradas**: `modo` (String, requerido) y `marca` (String, requerido).

Cada caso se corre **2 veces** con marcas distintas. Después de cada llamada se cuentan los
registros por prefijo `zz-d72-<marca>-` con el Web API (con `dataverse_api.py`, fuera del plugin). La
traza se lee de `plugintracelogs` (filtrar por `typename` del tipo del spike).

## Restricciones (no negociables)
1. **Nada en `sanic_mppp_sol_mantenimientoppp`.** Crear una solución temporal
   `sanic_mppp_sol_zzspiked72`, **del mismo publisher** que la nuestra (lo pide el prefijo `sanic_`),
   y construir todo ahí. Al empezar y al terminar, listar los `solutioncomponents` de
   `sanic_mppp_sol_mantenimientoppp` y comprobar que **no cambiaron**: guardar las dos cuentas en las
   conclusiones.
2. **Nombres**: paquete `sanic_mppp_pkg_zzspiked72`, ensamblado `Sanic.Mppp.Spike.D72`, Custom API
   `sanic_mppp_capi_zzspiked72`. **No se toca el paquete `sanic_mppp_pkg_plugins` ni su código.**
3. **Se construye con las herramientas del proyecto**: `herramientas/construir/paquete_plugins.py` y
   `herramientas/construir/custom_api.py`, con playbooks propios del spike guardados en la carpeta del
   spike (tomar como modelo `playbooks/paquete-plugins/sanic_mppp_pkg_plugins.md` y
   `playbooks/custom-api/sanic_mppp_capi_exportarparadigitar.md`). Si una herramienta no admite algo que
   el spike necesita, **no la modifiques**: reportalo.
4. **Limpieza al terminar, en este orden**: borrar los registros `zz-d72-*` que hayan quedado, la
   Custom API (con sus parámetros), el paquete y la solución temporal. Verificar con una consulta que no
   quedó ninguno. En Dev, y solo para el spike, borrar está autorizado.
5. **Solo Dev.** Ningún otro entorno.
6. **Si el sistema de permisos bloquea un comando, parás y lo reportás.** No lo reintentes, ni con
   otra forma ni con otra herramienta.
7. No compilar la solución principal ni correr sus pruebas. El proyecto del spike sí se compila: es
   necesario para subirlo. Basarse en `src/Sanic.Mppp.Plugins/Sanic.Mppp.Plugins.csproj` (net462,
   `Microsoft.CrmSdk.CoreAssemblies` 9.0.2.60, `Microsoft.NETFramework.ReferenceAssemblies` 1.0.3), **sin
   Open XML** (no hace falta).

## Destino
Solo dentro de `spikes/d72-transaccion-capi/`: `src/` (el proyecto), `playbooks/`, `CONCLUSIONES.md`.

## modo_tdd
No aplica: la prueba ES la corrida contra Dev. Cada número de las conclusiones tiene que salir de
una consulta que se pueda repetir. Poné el comando exacto al lado de cada número.

## Qué tiene que decir `CONCLUSIONES.md`
1. **Respuesta en una línea**: ¿la operación principal de una Custom API corre en transacción y se
   deshace entera ante una excepción? Sí / No / Depende (de qué).
2. Tabla de los 5 casos × 2 corridas: `entransaccion`, `etapa`, `profundidad`, registros contados
   después, y el mensaje que recibió el cliente.
3. Si `fallo_escritura` deja pasar nuestro mensaje o lo tapa uno de la plataforma (texto exacto).
4. Cuentas de componentes de `sanic_mppp_sol_mantenimientoppp` antes y después.
5. Evidencia de la limpieza (consultas que devuelven 0).
6. Tropiezos con las herramientas, si los hubo (qué pasó, sin arreglarlo).
