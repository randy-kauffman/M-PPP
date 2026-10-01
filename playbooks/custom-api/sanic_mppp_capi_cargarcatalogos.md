# Playbook: custom-api · sanic_mppp_capi_cargarcatalogos

DN-04 (`diseno/DIRECTRICES-NUEVAS.md`, Excel 1 y "Contratos de las dos Custom API"); contrato en
`diseno/03-contratos-custom-api.md` §1c. Orden del aprobador de construir DN-04: 2026-10-01.

Precondición: el paquete `sanic_mppp_pkg_plugins` registrado con `Sanic.Mppp.Plugins.Api.CargarCatalogosApi` entre sus `tipos`
(`playbooks/paquete-plugins/sanic_mppp_pkg_plugins.md`).

**A quién llama.** Una PERSONA desde la app: el Administrador de planes, desde el menú desplegable de las grillas de
Cliente, Plan, Autorizado y AutorizacionCliente (hoja `CargaCatalogos`). El control es `ExecutePrivilegeName`, como
`exportarparadigitar`: `prvCreatesanic_mppp_tbl_cliente` solo lo tiene el Administrador de planes (`04` §2). Es solo la puerta: cada
escritura vuelve a pasar por la seguridad con el usuario que llama (los catálogos son auditados, DD-18).

**Por qué los dos parámetros de entrada son opcionales.** `archivo` y `plantilla` se excluyen entre sí (con
`plantilla = true` no se manda archivo; con los dos → MPPP-CAR-010; sin ninguno → MPPP-CAR-001). Si `archivo` fuera
requerido en la plataforma, pedir la plantilla vacía obligaría a mandar un archivo de mentira. La regla la aplica el
código, con su código de error propio, no la plataforma.

## 1. Identidad

```json
{
  "tipo_playbook": "custom-api",
  "version_skill": "0.1.0",
  "proyecto": "2026-001-referencias-planes-pago",
  "inventario": "DN-04",
  "fase": 2,
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
  "tipo": "custom-api",
  "nombre": "sanic_mppp_capi_cargarcatalogos",
  "displayname": "CAPI - MPPP - Cargar catalogos",
  "descripcion": "Carga masiva desde Excel de Clientes, Planes, Correos y Autorizaciones por cliente (DN-04). Todo o nada.",
  "plugintype": "Sanic.Mppp.Plugins.Api.CargarCatalogosApi",
  "esfuncion": false,
  "esprivada": false,
  "binding": "Global",
  "pasosdeterceros": "None",
  "habilitadaenflujos": false,
  "privilegio": "prvCreatesanic_mppp_tbl_cliente",
  "entrada": [
    {
      "nombre": "archivo",
      "displayname": "CAPI_IP - MPPP - Archivo",
      "descripcion": "El .xlsx subido, en base64. Requerido salvo con plantilla = true.",
      "tipo": "String",
      "opcional": true
    },
    {
      "nombre": "plantilla",
      "displayname": "CAPI_IP - MPPP - Plantilla",
      "descripcion": "true: no lee nada, devuelve la plantilla vacia en resultado. Con archivo ademas: MPPP-CAR-010.",
      "tipo": "Boolean",
      "opcional": true
    }
  ],
  "salida": [
    {
      "nombre": "grabado",
      "displayname": "CAPI_OP - MPPP - Grabado",
      "descripcion": "true solo si la pasada 2 termino (todo o nada).",
      "tipo": "Boolean"
    },
    {
      "nombre": "resultado",
      "displayname": "CAPI_OP - MPPP - Resultado",
      "descripcion": "El Excel de resultado (o la plantilla vacia), en base64.",
      "tipo": "String"
    },
    {
      "nombre": "filas",
      "displayname": "CAPI_OP - MPPP - Filas",
      "descripcion": "Filas no vacias leidas.",
      "tipo": "Integer"
    },
    {
      "nombre": "errores",
      "displayname": "CAPI_OP - MPPP - Errores",
      "descripcion": "Filas con al menos un error.",
      "tipo": "Integer"
    },
    {
      "nombre": "advertencias",
      "displayname": "CAPI_OP - MPPP - Advertencias",
      "descripcion": "Filas con al menos una advertencia.",
      "tipo": "Integer"
    },
    {
      "nombre": "clientescreados",
      "displayname": "CAPI_OP - MPPP - Clientes creados",
      "descripcion": "Clientes creados.",
      "tipo": "Integer"
    },
    {
      "nombre": "planescreados",
      "displayname": "CAPI_OP - MPPP - Planes creados",
      "descripcion": "Planes creados.",
      "tipo": "Integer"
    },
    {
      "nombre": "correoscreados",
      "displayname": "CAPI_OP - MPPP - Correos creados",
      "descripcion": "Correos autorizados creados.",
      "tipo": "Integer"
    },
    {
      "nombre": "autorizacionescreadas",
      "displayname": "CAPI_OP - MPPP - Autorizaciones creadas",
      "descripcion": "Autorizaciones por cliente creadas.",
      "tipo": "Integer"
    }
  ]
}
```

## 3. Precondiciones

- El paquete de plugins registrado, con `Sanic.Mppp.Plugins.Api.CargarCatalogosApi` entre sus `plugintype`.
- El privilegio `prvCreatesanic_mppp_tbl_cliente` existe (verificado en Dev el 2026-10-01).
- Los parámetros `carga.filas.maximo` y `lectura.limites` sembrados (`playbooks/datos/parametros.md`). Sin ellos la API
  igual funciona con sus valores por defecto (300 filas; los topes de `LimitesLectura`), pero el parámetro es el que se mide y ajusta.

## 4. Qué es inmutable

`uniquename`, `binding`, `esfuncion`, `pasosdeterceros`, `habilitadaenflujos`; y de cada parámetro, `nombre`, `tipo` y
`opcional`. Un error en cualquiera de esos obliga a borrar la API entera y rehacerla.

## 5. Cómo se construye y se verifica

```
python3 herramientas/construir/custom_api.py playbooks/custom-api/sanic_mppp_capi_cargarcatalogos.md --solo-verificar
python3 herramientas/construir/custom_api.py playbooks/custom-api/sanic_mppp_capi_cargarcatalogos.md
python3 herramientas/construir/custom_api.py playbooks/custom-api/sanic_mppp_capi_cargarcatalogos.md --solo-verificar
```
