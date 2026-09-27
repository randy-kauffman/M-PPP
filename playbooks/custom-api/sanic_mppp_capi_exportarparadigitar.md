# Playbook: custom-api · sanic_mppp_capi_exportarparadigitar

DN-02 (`diseno/DIRECTRICES-NUEVAS.md`, "Diseño resuelto — Exportación"). Orden explícita del
aprobador de construir en Dev, 2026-09-26.

Precondición: el paquete `sanic_mppp_pkg_plugins` tiene que estar registrado
(`playbooks/paquete-plugins/sanic_mppp_pkg_plugins.md`) con
`Sanic.Mppp.Plugins.Api.ExportarParaDigitarApi` entre sus `tipos`.

**A quién llama, y por qué NO lleva la puerta del RPA.** A diferencia de las dos Custom API de fase
2 (`sanic_mppp_capi_obtenerfilaspendientes`, `sanic_mppp_capi_registrarresultadorpa`), a ÉSTA la
llama una PERSONA desde la app (Ejecutivo o Supervisor), no un bot: el control es
`ExecutePrivilegeName`, igual que `validarsolicitud`/`clasificarcorreo`, no una puerta en el código.
**Confirmado por el aprobador, 2026-09-26**: `prvReadsanic_mppp_tbl_fila` — Ejecutivo y Supervisor lo
tienen (`04-matriz-privilegios.md` §2: `R W Ap:O` sobre Fila); Administrador de planes y
Administrador técnico no tienen nada sobre Fila y quedan afuera solos, sin tocar ningún rol.

Ver el XML doc de `Sanic.Mppp.Plugins.Api.ExportarParaDigitarApi` para el contrato completo
(ya construido y en verde, 1080 pruebas — no se toca desde este playbook).

## 1. Identidad

```json
{
  "tipo_playbook": "custom-api",
  "version_skill": "0.1.0",
  "proyecto": "2026-001-referencias-planes-pago",
  "inventario": "DN-02",
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
  "nombre": "sanic_mppp_capi_exportarparadigitar",
  "displayname": "CAPI - MPPP - Exportar para digitar",
  "descripcion": "Arma en Excel las filas seleccionadas, en el orden de columnas de exportacion.columnas, para que el ejecutivo las digite en AS400.",
  "plugintype": "Sanic.Mppp.Plugins.Api.ExportarParaDigitarApi",
  "esfuncion": true,
  "esprivada": false,
  "binding": "Global",
  "pasosdeterceros": "None",
  "habilitadaenflujos": false,
  "privilegio": "prvReadsanic_mppp_tbl_fila",
  "entrada": [
    {
      "nombre": "filaids",
      "displayname": "CAPI_IP - MPPP - Fila ids",
      "descripcion": "Los filaid seleccionados en la grilla. Calza con SelectedControlSelectedItemIds del ribbon. Vacio: archivo valido con cero filas.",
      "tipo": "StringArray",
      "opcional": false
    }
  ],
  "salida": [
    {
      "nombre": "archivo",
      "displayname": "CAPI_OP - MPPP - Archivo",
      "descripcion": "El .xlsx armado, en base64.",
      "tipo": "String"
    },
    {
      "nombre": "exportadas",
      "displayname": "CAPI_OP - MPPP - Exportadas",
      "descripcion": "Cuantas filas quedaron en el archivo.",
      "tipo": "Integer"
    }
  ]
}
```

## 3. Precondiciones

- El paquete de plugins registrado, con `Sanic.Mppp.Plugins.Api.ExportarParaDigitarApi` entre sus `plugintype`.
- El privilegio `prvReadsanic_mppp_tbl_fila` existe (lo crea la tabla Fila, ya construida).
- Los parámetros `exportacion.columnas` y `exportacion.maximo` sembrados (`playbooks/datos/parametros.md`).

## 4. Qué es inmutable

`uniquename`, `binding`, `esfuncion`, `pasosdeterceros`, `habilitadaenflujos`; y de cada parámetro,
`nombre`, `tipo` y `opcional`. Un error en cualquiera de esos obliga a borrar la API entera y rehacerla.
