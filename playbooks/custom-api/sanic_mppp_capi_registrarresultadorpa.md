# Playbook: custom-api · sanic_mppp_capi_registrarresultadorpa

Inventario 8.4. Contrato en `diseno/03-contratos-custom-api.md` §3, y su encabezado (D-9, D-14, D-44).

Precondición: el paquete `sanic_mppp_pkg_plugins` tiene que estar registrado
(`playbooks/paquete-plugins/sanic_mppp_pkg_plugins.md`) con
`Sanic.Mppp.Plugins.Api.RegistrarResultadoRpaApi` entre sus `tipos`.

## 1. Identidad

```json
{
  "tipo_playbook": "custom-api",
  "version_skill": "0.1.0",
  "proyecto": "2026-001-referencias-planes-pago",
  "inventario": "8.4",
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
  "nombre": "sanic_mppp_capi_registrarresultadorpa",
  "displayname": "CAPI - MPPP - Registrar resultado RPA",
  "descripcion": "El bot RPA informa, uno por uno y por lote, lo que ya ocurrio en AS400 (digitada, aprobada, rechazada o atencion manual) para filas que tomo con obtenerfilaspendientes. Nunca todo o nada: falla parcial por item.",
  "plugintype": "Sanic.Mppp.Plugins.Api.RegistrarResultadoRpaApi",
  "esfuncion": false,
  "esprivada": false,
  "binding": "Global",
  "pasosdeterceros": "None",
  "habilitadaenflujos": false,
  "privilegio": "",
  "entrada": [
    {
      "nombre": "resultados",
      "displayname": "CAPI_IP - MPPP - Resultados",
      "descripcion": "Arreglo JSON del lote (de 1 a rpa.lote.maximo items), cada uno con filaid, resultado, mensaje (si rechazada o atencionmanual), codigo y fechaproceso. Ausente se trata como lote vacio (MPPP-RPA-009).",
      "tipo": "String",
      "opcional": true
    }
  ],
  "salida": [
    {
      "nombre": "resultados",
      "displayname": "CAPI_OP - MPPP - Resultados",
      "descripcion": "Arreglo JSON con el resultado por item: filaid, estadofila, yaregistrado y error si fallo.",
      "tipo": "String"
    },
    {
      "nombre": "procesadas",
      "displayname": "CAPI_OP - MPPP - Procesadas",
      "descripcion": "Cuantos items del lote se procesaron sin error.",
      "tipo": "Integer"
    },
    {
      "nombre": "conerror",
      "displayname": "CAPI_OP - MPPP - Con error",
      "descripcion": "Cuantos items del lote quedaron con error (por item, no de la llamada entera).",
      "tipo": "Integer"
    }
  ]
}
```

## 3. Precondiciones

- El paquete de plugins registrado, con `Sanic.Mppp.Plugins.Api.RegistrarResultadoRpaApi` entre sus `plugintype`.
- El rol `SR - MPPP - RPA` existe en el entorno (inventario 6.7); si no existe, la puerta del plugin (`PuertaDelRpa.Exigir`, D-44) lo dice explícitamente al llamar, no esta herramienta.
- `ExecutePrivilegeName` va **vacío a propósito** (D-9), mismo motivo que en `obtenerfilaspendientes`.
- El nombre `resultados` se repite en `entrada` y `salida` **a propósito** (`03` §3, "La forma del parámetro de ENTRADA del lote": el bot manda `resultados` y recibe `resultados`; son dos tablas distintas de Dataverse, `customapirequestparameters` y `customapiresponseproperties`, y no chocan).

## 4. Qué es inmutable

`uniquename`, `binding`, `esfuncion`, `pasosdeterceros`, `habilitadaenflujos`; y de cada parámetro,
`nombre`, `tipo` y `opcional`. Un error en cualquiera de esos obliga a borrar la API entera y rehacerla.
