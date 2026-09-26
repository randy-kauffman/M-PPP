# Playbook: custom-api · sanic_mppp_capi_obtenerfilaspendientes

Inventario 8.3. Contrato en `diseno/03-contratos-custom-api.md` §2, y su encabezado (D-9, D-44).

Precondición: el paquete `sanic_mppp_pkg_plugins` tiene que estar registrado
(`playbooks/paquete-plugins/sanic_mppp_pkg_plugins.md`) con
`Sanic.Mppp.Plugins.Api.ObtenerFilasPendientesApi` entre sus `tipos`.

## 1. Identidad

```json
{
  "tipo_playbook": "custom-api",
  "version_skill": "0.1.0",
  "proyecto": "2026-001-referencias-planes-pago",
  "inventario": "8.3",
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
  "nombre": "sanic_mppp_capi_obtenerfilaspendientes",
  "displayname": "CAPI - MPPP - Obtener filas pendientes",
  "descripcion": "El bot RPA consulta el lote de filas pendientes de digitar o aprobar, en orden FIFO estricto, y reserva (o refresca) el claim sobre cada una que entrega.",
  "plugintype": "Sanic.Mppp.Plugins.Api.ObtenerFilasPendientesApi",
  "esfuncion": true,
  "esprivada": false,
  "binding": "Global",
  "pasosdeterceros": "None",
  "habilitadaenflujos": false,
  "privilegio": "",
  "entrada": [
    {
      "nombre": "maximo",
      "displayname": "CAPI_IP - MPPP - Maximo",
      "descripcion": "Cuantas filas entregar como maximo en esta llamada. Ausente: 100. Tope 500 (un valor mayor se recorta, sin error). 0 o negativo se rechaza.",
      "tipo": "Integer",
      "opcional": true
    },
    {
      "nombre": "claimminutos",
      "displayname": "CAPI_IP - MPPP - Claim minutos",
      "descripcion": "Por cuanto tiempo el bot reserva las filas que se lleva. Entero mayor que 0; mayor que rpa.minutos.claimmaximo se recorta al tope.",
      "tipo": "Integer",
      "opcional": false
    }
  ],
  "salida": [
    {
      "nombre": "filas",
      "displayname": "CAPI_OP - MPPP - Filas",
      "descripcion": "Arreglo JSON de filas Validada y Digitada (del mismo usuario que llama), en orden FIFO estricto.",
      "tipo": "String"
    },
    {
      "nombre": "entregadas",
      "displayname": "CAPI_OP - MPPP - Entregadas",
      "descripcion": "Cuantas filas trae este lote.",
      "tipo": "Integer"
    },
    {
      "nombre": "pendientes",
      "displayname": "CAPI_OP - MPPP - Pendientes",
      "descripcion": "Cuantas quedan sin entregar despues de este lote.",
      "tipo": "Integer"
    }
  ]
}
```

## 3. Precondiciones

- El paquete de plugins registrado, con `Sanic.Mppp.Plugins.Api.ObtenerFilasPendientesApi` entre sus `plugintype`.
- El rol `SR - MPPP - RPA` existe en el entorno (inventario 6.7); si no existe, la puerta del plugin (`PuertaDelRpa.Exigir`, D-44) lo dice explícitamente al llamar, no esta herramienta.
- `ExecutePrivilegeName` va **vacío a propósito** (D-9): la puerta es el plugin (identidad de aplicación + rol `SR - MPPP - RPA`), no un privilegio de tabla. Ningún privilegio existente sirve sin abrirle la API a otro rol (ver el porqué completo en el encabezado de `03`).

## 4. Qué es inmutable

`uniquename`, `binding`, `esfuncion`, `pasosdeterceros`, `habilitadaenflujos`; y de cada parámetro,
`nombre`, `tipo` y `opcional`. Un error en cualquiera de esos obliga a borrar la API entera y rehacerla.
