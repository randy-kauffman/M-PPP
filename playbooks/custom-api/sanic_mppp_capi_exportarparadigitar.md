# Playbook: custom-api · sanic_mppp_capi_exportarparadigitar

DN-02 (`diseno/DIRECTRICES-NUEVAS.md`, "Diseño resuelto — Exportación"). Orden explícita del
aprobador de construir en Dev, 2026-09-26.

**Reconstruida como Action el 2026-09-26 (misma fecha, orden explícita del aprobador).** Nació como
Function (`esfuncion: true`) y, con `exportacion.maximo` en 500, la medición real mostró un HTTP 414
a las ~700 filas: como es Function, TODA llamada es un GET con los `filaid` en la query string, y ese
techo de plataforma es independiente de la velocidad de escritura del Excel y no lo mueve ningún
parámetro. El tope de 500 filas por llamada abrió un agujero que detectó el aprobador: si lo que se
baja no es exactamente lo que se seleccionó, el ejecutivo puede marcar como digitadas filas que nunca
tipeó en AS400. Decisión: la API pasa a **Action** (`esfuncion: false`, POST, los ids viajan en el
cuerpo), se elimina el tope de filas, y el único límite pasa a ser el presupuesto de 2 minutos
(BP-PP-053, ver `src/Sanic.Mppp.Plugins/Api/ExportarParaDigitar.cs`). Verificado en Microsoft Learn
("CustomAPI tables", "Create a custom API using the plug-in registration tool"): `isfunction`
**"Cannot be changed after it is saved"**, así que la única forma de aplicar esto fue borrar la
Custom API entera (con sus parámetros de entrada y salida, en cascada) y volver a crearla — es un
componente propio, en Dev, que todavía no usa nadie. `filaids`, `archivo` y `exportadas` NO cambiaron:
mismo contrato de entrada/salida, solo cambió cómo se invoca.

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
  "esfuncion": false,
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
- El parámetro `exportacion.columnas` sembrado (`playbooks/datos/parametros.md`). `exportacion.maximo` existió y se eliminó el 2026-09-26 (ver ese playbook): ya no es una precondición.

## 4. Qué es inmutable

`uniquename`, `binding`, `esfuncion`, `pasosdeterceros`, `habilitadaenflujos`; y de cada parámetro,
`nombre`, `tipo` y `opcional`. Un error en cualquiera de esos obliga a borrar la API entera y rehacerla.
