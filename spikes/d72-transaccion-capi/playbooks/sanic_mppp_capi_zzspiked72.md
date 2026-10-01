# Playbook: custom-api · sanic_mppp_capi_zzspiked72

SPIKE D-72 (descartable). Action unbound, mismo camino que `Xrm.WebApi.online.execute`. Ver `spikes/d72-transaccion-capi/ORDEN.md`.

## 1. Identidad

```json
{
  "tipo_playbook": "custom-api",
  "version_skill": "0.1.0",
  "proyecto": "2026-001-referencias-planes-pago",
  "inventario": "D-72",
  "fase": 0,
  "entorno_url": "https://org36e60d9d.crm.dynamics.com/",
  "solucion": "sanic_mppp_sol_zzspiked72",
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
  "nombre": "sanic_mppp_capi_zzspiked72",
  "displayname": "CAPI - MPPP - ZZ spike D72",
  "descripcion": "SPIKE D-72, descartable: mide si la operacion principal de una Custom API se deshace entera ante una excepcion.",
  "plugintype": "Sanic.Mppp.Spike.D72.TransaccionCapiApi",
  "esfuncion": false,
  "esprivada": false,
  "binding": "Global",
  "pasosdeterceros": "None",
  "habilitadaenflujos": false,
  "privilegio": "",
  "entrada": [
    {
      "nombre": "modo",
      "displayname": "CAPI_IP - MPPP - ZZ modo",
      "descripcion": "exito | excepcion | fallo_escritura | sistema | mixto.",
      "tipo": "String",
      "opcional": false
    },
    {
      "nombre": "marca",
      "displayname": "CAPI_IP - MPPP - ZZ marca",
      "descripcion": "Prefijo distinto en cada llamada, para contar despues los registros creados.",
      "tipo": "String",
      "opcional": false
    }
  ],
  "salida": [
    {
      "nombre": "entransaccion",
      "displayname": "CAPI_OP - MPPP - ZZ en transaccion",
      "descripcion": "context.IsInTransaction.",
      "tipo": "Boolean"
    },
    {
      "nombre": "etapa",
      "displayname": "CAPI_OP - MPPP - ZZ etapa",
      "descripcion": "context.Stage.",
      "tipo": "Integer"
    },
    {
      "nombre": "profundidad",
      "displayname": "CAPI_OP - MPPP - ZZ profundidad",
      "descripcion": "context.Depth.",
      "tipo": "Integer"
    },
    {
      "nombre": "creados",
      "displayname": "CAPI_OP - MPPP - ZZ creados",
      "descripcion": "Cuantos Create hizo el plugin.",
      "tipo": "Integer"
    }
  ]
}
```

## 3. Precondiciones

- El paquete `sanic_mppp_pkg_zzspiked72` registrado, con `Sanic.Mppp.Spike.D72.TransaccionCapiApi` entre sus tipos.

## 4. Qué es inmutable

`uniquename`, `binding`, `esfuncion`, `pasosdeterceros`, `habilitadaenflujos`; y de cada parámetro, `nombre`, `tipo` y `opcional`.
