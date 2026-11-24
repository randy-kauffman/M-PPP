# Playbook: paquete-plugins · sanic_mppp_pkg_plugins

Inventario 8.0/8.1/8.2 dependen de este paquete: sin él no existen los `plugintype`
a los que apuntan las Custom API y los steps.

**`Sanic.Mppp.Plugins.Api.ExportarParaDigitarApi` se agregó el 2026-09-26 (DN-02,
`diseno/DIRECTRICES-NUEVAS.md`)**, para la Custom API `sanic_mppp_capi_exportarparadigitar`
(`playbooks/custom-api/sanic_mppp_capi_exportarparadigitar.md`). El código ya está en verde
(1080 pruebas) y no se toca desde acá.

**`CargarCatalogosApi` y `CambiarEstadoCatalogosApi` se agregaron el 2026-10-01 (DN-04)**, para
`sanic_mppp_capi_cargarcatalogos` y `sanic_mppp_capi_cambiarestadocatalogos`. Ensamblado **1.0.20.0**
(Dev tenía 1.0.19.0, verificado el 2026-10-01): sube solo el build, como pide el comentario del csproj, para que el
sandbox no siga ejecutando el binario viejo. Se actualiza con `--actualizar-contenido` (la `version` del PAQUETE,
1.0.0, es inmutable y no cambia).

**1.0.21.0 (2026-10-01, D-75)**: MPPP-CAR-011 para fallas de plataforma al leer y al escribir, y el código de Dataverse
en la traza. Mismos tipos, sin steps nuevos. Se sube con el presupuesto TODAVÍA en 100 s a propósito: hay que reproducir el
10928 de Azure SQL con 200 filas para confirmar qué `ErrorCode` trae (`DIRECTRICES-NUEVAS.md` DN-04, párrafo del 011).
Verificado ese mismo día: llegó como `0x80072324` y salió como MPPP-CAR-011.

**1.0.22.0 (2026-10-01, D-75)**: presupuesto de escritura de 30 s y tope por defecto de 100 filas. Mismos tipos.

**1.0.23.0 (2026-10-06, DN-05)**: cinco reglas de registro nuevas (`CLASIFICACION_DEL_FORMATO`, `GESTION_DEL_FORMATO`,
`BANCO_DEL_FORMATO`, `CARACTERES_PERMITIDOS`, `CUENTA_DEL_BANCO`), y `ValidarSolicitud` exige los parámetros
`plan.formatos` y `cuenta.largos`. Mismos tipos, sin steps nuevos. **Se sube DESPUÉS de sembrar esos dos parámetros**
(con el ensamblado viejo arriba, que no los lee): si sube antes, toda validación de correos falla con "Falta el
parámetro". Y ANTES de `playbooks/datos/reglas.md` (el plugin de integridad rechaza una regla sin evaluador).

**1.0.24.0 (2026-11-23, D-77)**: `ClasificarCorreo` es idempotente con la Solicitud en Ingresada (no reinserta
`ResultadoRegla` ni repite la Bitácora en el reintento). Mismos tipos, sin steps nuevos, sin parámetros nuevos. Viaja
también en la solución parche `sanic_mppp_sol_parche_1_3_1` junto con D-79 (`MPPP-ING`).

**1.0.25.0 (2026-11-24)**: Excel más pesado que el tope: el cliente recibe solo "El archivo de Excel pesa más de lo permitido (máximo N MB). Por favor, envíe un archivo más liviano." (`ESTRUCTURA_PLANTILLA` con rechazo `PesoExcedido`, también el tope descomprimido LP-03). Mismos tipos, sin steps nuevos.

**1.0.26.0 (2026-11-24)**: D-81: plan vacío → 'Falta el número de plan.' (PLAN_EXISTE sin marcadores). D-82: la fecha del acuse sale en la zona de `respuesta.zonahoraria` (opcional, -06:00 por defecto). Mismos tipos, sin steps nuevos.

Antes de correr la herramienta hay que armar el .nupkg, **borrando antes `bin/` y `obj/` de los DOS proyectos**
(receta `references/dataverse/alm.md` línea 118: `dotnet clean` no alcanza). Sin eso, `dotnet pack` sale con código 0
y **no regenera el .nupkg**: queda el viejo, sin los tipos nuevos (pasó el 2026-10-01 con DN-04; lo detectó el
constructor comparando la fecha del .nupkg y buscando los tipos dentro del .dll empaquetado). Son artefactos de
compilación ignorados por git: borrarlos no pierde nada.

```
rm -rf src/Sanic.Mppp.Plugins/bin src/Sanic.Mppp.Plugins/obj src/Sanic.Mppp.Plugins.Paquete/bin src/Sanic.Mppp.Plugins.Paquete/obj
dotnet pack src/Sanic.Mppp.Plugins.Paquete/Sanic.Mppp.Plugins.Paquete.csproj -c Release
```

Comprobación antes de subir: el .nupkg tiene la fecha de hoy y su `Sanic.Mppp.Plugins.dll` contiene cada uno de los
`tipos` de abajo (buscar el nombre de la clase en los bytes del .dll dentro del zip). Los nombres de clase están en
ASCII; un literal de texto del código (por ejemplo un código de regla) está en **UTF-16**: buscarlo en ASCII da falso
(lo encontró el constructor el 2026-10-06, DN-05).

## 1. Identidad

```json
{
  "tipo_playbook": "paquete-plugins",
  "version_skill": "0.1.0",
  "proyecto": "2026-001-referencias-planes-pago",
  "inventario": "8.0",
  "fase": 1,
  "entorno_url": "https://org36e60d9d.crm.dynamics.com/",
  "solucion": "sanic_mppp_sol_mantenimientoppp",
  "publisher": "Sistemas_Abiertos_Nicaragua",
  "prefijo": "sanic",
  "abrev": "mppp",
  "prefijo_opciones": 15946,
  "lcid": 1033
}
```

## 2. Qué se crea

```json
{
  "tipo": "paquete-plugins",
  "nombre": "sanic_mppp_pkg_plugins",
  "uniquename": "sanic_mppp_pkg_plugins",
  "version": "1.0.0",
  "nupkg": "src/Sanic.Mppp.Plugins.Paquete/bin/Release/SanicMpppPlugins.1.0.0.nupkg",
  "tipos": [
    "Sanic.Mppp.Plugins.Api.ClasificarCorreoApi",
    "Sanic.Mppp.Plugins.Api.ValidarSolicitudApi",
    "Sanic.Mppp.Plugins.Steps.ListaBlancaStep",
    "Sanic.Mppp.Plugins.Steps.NormalizarYValidarStep",
    "Sanic.Mppp.Plugins.Steps.NombreCalculadoStep",
    "Sanic.Mppp.Plugins.Steps.IntegridadDeReglaStep",
    "Sanic.Mppp.Plugins.Steps.TransicionDeFilaStep",
    "Sanic.Mppp.Plugins.Steps.PostTransicionDeFilaStep",
    "Sanic.Mppp.Plugins.Steps.AtenderPorClasificarStep",
    "Sanic.Mppp.Plugins.Api.ObtenerFilasPendientesApi",
    "Sanic.Mppp.Plugins.Api.RegistrarResultadoRpaApi",
    "Sanic.Mppp.Plugins.Steps.RevisionAtendidaStep",
    "Sanic.Mppp.Plugins.Api.ExportarParaDigitarApi",
    "Sanic.Mppp.Plugins.Api.CargarCatalogosApi",
    "Sanic.Mppp.Plugins.Api.CambiarEstadoCatalogosApi"
  ]
}
```

## 3. Precondiciones

- La solución `sanic_mppp_sol_mantenimientoppp` existe, es unmanaged y su publisher es `Sistemas_Abiertos_Nicaragua` (lo comprueba la herramienta).
- El .nupkg está armado y pesa menos de 12 MB. Lleva `Sanic.Mppp.Plugins.dll`, los dos de `DocumentFormat.OpenXml` y `Sanic.Mppp.Plugins.Paquete.dll`, el ensamblado vacío (~4,6 KB) que genera el propio proyecto del paquete (observado al desplegar 1.0.21.0, 2026-10-01; el csproj del paquete no cambió desde que se creó). Nada más: el SDK de Dataverse ya vive en el sandbox y no viaja (`ExcludeAssets="runtime"` en el csproj del paquete).
- Hay steps registrados (27 al 2026-10-06) apuntando a estos tipos: por eso solo sube el build. Un cambio de `major`/`minor` del ensamblado NO los re-apunta solo (Learn, "Assembly versioning").

## 4. Qué NO hace esta herramienta

- No borra el paquete. Borrarlo se lleva puestos los ensamblados, los tipos y **todos** sus steps.
- No corrige `nombre` ni `version`: son inmutables en el servidor. Si están mal, hay que borrar a mano y rehacer.
- No actualiza el contenido salvo que se lo pidan con `--actualizar-contenido`.
