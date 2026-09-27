# Playbook: datos · parametros (los 20)

**`autorizacion.modo` se agregó el 2026-09-26 (DN-01, `diseno/DIRECTRICES-NUEVAS.md`, "Diseño resuelto").**
Declara qué vía consulta `AUTORIZACION_CORREO_PLAN`: `plan` (la de hoy, `AutorizacionPlan`) o `cliente`
(la nueva, `AutorizacionCliente`). Se siembra en `plan`: **el comportamiento de hoy no cambia solo por
agregar el parámetro.** Tiene el mismo criterio de fallback quemado que `servicio.cuenta` y
`rpa.horas.tolerancia`: un valor que falta o viene con basura degrada a `plan`, nunca tumba la
validación (`Validacion/ReglasDeRegistro.cs`, `ModoDeAutorizacionParametro.Resolver`). Cambiar este
parámetro a `cliente` en un entorno activa la vía por cliente sin recargar nada: las filas de
`AutorizacionPlan` se conservan, solo dejan de mirarse.

**`servicio.cuenta` se agrego el 2026-09-23 y hoy tiene un valor PROVISORIO.** Declara el UPN de la
cuenta con la que los cinco flujos escriben en Dataverse; `BaseDeStep.EsCodigoDeServidor` lo usa
para no tratarlos como si fueran una persona (ver `diseno/07-flujos.md` §33, que declara la
excepcion a BP-PP-122: en BAC la cuenta de servicio es un usuario, no un service principal, y un
usuario no tiene `applicationid`). Mientras el componente 13.2 (cuenta de servicio licenciada) no
exista, apunta a la cuenta personal del administrador. **Eso significa que esa persona tampoco pasa
por la lista blanca cuando trabaja a mano.** Decidido asi el 2026-09-23 para no frenar las pruebas.

Inventario 9.1. **COMPLETO desde el 2026-09-22.** `plantilla.estructura` y `plantilla.listas` se
armaron leyendo `datos/plantilla/Inclusiones_Exclusiones en PPP.xlsx`, la plantilla
real del cliente: los encabezados salen de la fila 12 de la hoja `Datos` y los
codigos de banco de la hoja `Listas`. Los valores canonicos son los nombres de los
enums del dominio (`Dominio/Choices.g.cs`), no los textos del Excel: la comparacion
de listas ya ignora mayusculas, tildes y ñ, asi que INCLUSION matchea Inclusion sin
necesidad de declarar variantes.

**`rpa.puedeaprobar` DIVERGE POR ENTORNO A PROPOSITO.** El playbook siembra `no`, que es el valor seguro: sin el
aval de C-03 el bot no puede aprobar lo que el mismo digito. **C-03 se cerro el 2026-09-25 (el aval existe; su
contenido es confidencial y no vive en este repositorio), y en DEV el parametro se paso a `si` a mano ese mismo
dia, por orden del aprobador, para poder ejercitar el ciclo completo del bot.** Por eso `datos.py --solo-verificar`
va a reportar `difiere` en esta fila contra Dev, y eso es lo ESPERADO, no una deriva: que se prenda o no es una
decision operativa de cada entorno (D-53), no del playbook. **No se corrige con `--completar`**: eso lo volveria a
apagar. Si alguna vez el `difiere` de esta fila desaparece contra un entorno donde deberia estar prendido, ahi si
hay que mirar.

**`exportacion.columnas` se agregó el 2026-09-26 (DN-02, `diseno/DIRECTRICES-NUEVAS.md`)**, orden
explícita del aprobador de construir en Dev. Trae `numeroSolicitud` y `numeroFila` (trazabilidad,
sin eso el archivo no sirve para volver a marcar lo digitado) más los diez campos de
`plantilla.estructura` que el ejecutivo necesita para digitar en AS400 — cuenta e identificación
incluidas: DN-02 dejó escrito que ese archivo sale de la plataforma SIN la protección de columna de
RNF-04/BP-PP-004, decisión tomada a conciencia por el aprobador. **El CONJUNTO de columnas es una
decisión del negocio, no del código**: agregar o sacar un campo es cambiar este parámetro, nunca
tocar una línea de código (el plugin no tiene una lista propia de campos permitidos ni prohibidos).

**`exportacion.maximo` existió entre el 2026-09-26 y el 2026-09-26 y se ELIMINÓ el mismo día.**
Nació sembrado en 500 como tope PROVISIONAL de filas por llamada, mientras
`sanic_mppp_capi_exportarparadigitar` era una Function (GET, ids en la URL): la medición real
(BP-PP-053) encontró un HTTP 414 a las ~700 filas, techo de plataforma que ningún parámetro mueve,
y el tope de 500 abrió un agujero (si lo que se baja no es exactamente lo seleccionado, el
ejecutivo puede marcar como digitadas filas que nunca tipeó). Decisión del aprobador: la Custom API
pasa a Action (POST, ids en el cuerpo), se elimina este parámetro por completo, y el único límite
que queda es el presupuesto síncrono de 2 minutos, que el propio plugin vigila y por el que corta
solo con un mensaje claro (`src/Sanic.Mppp.Plugins/Api/ExportarParaDigitar.cs`,
`PresupuestoDeExportacionAgotandoseException`). Se borró la fila real de Dev
(`sanic_mppp_tbl_parametroid b88833e3-21ba-f111-aaad-000d3a301e02`) y se sacó de este playbook: no
queda ningún rastro de `exportacion.maximo` ni en el entorno ni en el inventario.

Valores con fuente:
- `autorizacion.modo`: `DIRECTRICES-NUEVAS.md` DN-01 "Diseño resuelto", documentado en `02-diccionario-datos.md` §2.5 (agregado el 2026-09-26).
- `correo.prefijos.reenvio`, `clasificacion.dias.vencimiento`, `rpa.puedeaprobar`: `02-diccionario-datos.md` §134-136.
- `lectura.limites`: `03-contratos-custom-api.md` §249 (2 MB / 20 MB); los nombres de las claves salen de `Plantilla/LimitesLectura.cs`.
- `plantilla.obligatoriedad`: DD-16 ("todo obligatorio salvo Referencia") con la forma de D-18.
- Los tres de `vigilancia.*` y los dos de `retencion.dias.*`: **decididos por el aprobador el 2026-09-22** (D-45); no estaban en ningun archivo.

Ojo: los fixtures de prueba (`ParametrosDePlantillaAceptacion`) y el mockup
NO son fuente de datos. Sus codigos de banco y sus columnas son inventados y no
coinciden con la plantilla real del cliente.

## 1. Identidad

```json
{
  "tipo_playbook": "datos",
  "version_skill": "0.1.0",
  "proyecto": "2026-001-referencias-planes-pago",
  "inventario": "9.1",
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
  "tipo": "datos",
  "tabla": "sanic_mppp_tbl_parametro",
  "clave": [
    "sanic_nombre",
    "sanic_version"
  ],
  "filas": [
    {
      "sanic_nombre": "autorizacion.modo",
      "sanic_version": 1,
      "sanic_tipo": {
        "choice": "sanic_mppp_ch_tipoparametro",
        "etiqueta": "Texto"
      },
      "sanic_valor": "plan",
      "sanic_descripcion": "DN-01 (2026-09-26): 'plan' o 'cliente', que via consulta AUTORIZACION_CORREO_PLAN. Sembrado en 'plan' (el comportamiento de hoy). Fallback quemado en el codigo: falta o basura degrada a 'plan', nunca tumba la validacion."
    },
    {
      "sanic_nombre": "correo.prefijos.reenvio",
      "sanic_version": 1,
      "sanic_tipo": {
        "choice": "sanic_mppp_ch_tipoparametro",
        "etiqueta": "JSON"
      },
      "sanic_valor": "[\"FW:\",\"FWD:\",\"RV:\",\"REENV:\"]",
      "sanic_descripcion": "Prefijos de asunto que identifican un reenvio. 02 §134."
    },
    {
      "sanic_nombre": "clasificacion.dias.vencimiento",
      "sanic_version": 1,
      "sanic_tipo": {
        "choice": "sanic_mppp_ch_tipoparametro",
        "etiqueta": "Numero"
      },
      "sanic_valor": "30",
      "sanic_descripcion": "Dias que un correo puede quedar en Por clasificar antes de pasar a Vencida. 02 §135."
    },
    {
      "sanic_nombre": "lectura.limites",
      "sanic_version": 1,
      "sanic_tipo": {
        "choice": "sanic_mppp_ch_tipoparametro",
        "etiqueta": "JSON"
      },
      "sanic_valor": "{\"maximoBytesComprimido\":2097152,\"maximoBytesDescomprimido\":20971520}",
      "sanic_descripcion": "Peso maximo del archivo comprimido (2 MB) y del contenido descomprimido (20 MB). 03 §249."
    },
    {
      "sanic_nombre": "plantilla.obligatoriedad",
      "sanic_version": 1,
      "sanic_tipo": {
        "choice": "sanic_mppp_ch_tipoparametro",
        "etiqueta": "JSON"
      },
      "sanic_valor": "{\"porDefecto\":\"obligatorio\",\"opcionales\":[{\"campo\":\"referencia\"}]}",
      "sanic_descripcion": "Campos obligatorios. Arranca con todo obligatorio salvo Referencia (DD-16); crece con reglas cuando el negocio escriba la matriz."
    },
    {
      "sanic_nombre": "rpa.puedeaprobar",
      "sanic_version": 1,
      "sanic_tipo": {
        "choice": "sanic_mppp_ch_tipoparametro",
        "etiqueta": "Texto"
      },
      "sanic_valor": "no",
      "sanic_descripcion": "En 'si', el bot puede aprobar lo que el mismo digito. Queda en 'no' hasta contar con el aval de C-03."
    },
    {
      "sanic_nombre": "servicio.cuenta",
      "sanic_version": 1,
      "sanic_tipo": {
        "choice": "sanic_mppp_ch_tipoparametro",
        "etiqueta": "Texto"
      },
      "sanic_valor": "admin@55xljh.onmicrosoft.com",
      "sanic_descripcion": "PROVISORIO. UPN de la cuenta con la que los cinco flujos escriben; sin el, la lista blanca los trata como personas y no pueden escribir sus marcas de envio. Hoy apunta a la cuenta personal del administrador porque el componente 13.2 no existe todavia: cambiar al UPN de la cuenta de servicio de BAC antes de produccion."
    },
    {
      "sanic_nombre": "vigilancia.minutos.sinvalidar",
      "sanic_version": 1,
      "sanic_tipo": {
        "choice": "sanic_mppp_ch_tipoparametro",
        "etiqueta": "Numero"
      },
      "sanic_valor": "15",
      "sanic_descripcion": "Umbral de MPPP-VIG para una Solicitud que sigue en Ingresada. Aprobado 2026-09-22: VIG corre cada 10 min."
    },
    {
      "sanic_nombre": "vigilancia.minutos.sinresponder",
      "sanic_version": 1,
      "sanic_tipo": {
        "choice": "sanic_mppp_ch_tipoparametro",
        "etiqueta": "Numero"
      },
      "sanic_valor": "30",
      "sanic_descripcion": "Umbral de MPPP-VIG para una comunicacion sin enviar o sin confirmar. Aprobado 2026-09-22."
    },
    {
      "sanic_nombre": "vigilancia.reintentos.maximo",
      "sanic_version": 1,
      "sanic_tipo": {
        "choice": "sanic_mppp_ch_tipoparametro",
        "etiqueta": "Numero"
      },
      "sanic_valor": "3",
      "sanic_descripcion": "Reintentos de validacion antes de marcar sanic_requiererevision. Aprobado 2026-09-22."
    },
    {
      "sanic_nombre": "retencion.dias.general",
      "sanic_version": 1,
      "sanic_tipo": {
        "choice": "sanic_mppp_ch_tipoparametro",
        "etiqueta": "Numero"
      },
      "sanic_valor": "30",
      "sanic_descripcion": "Plazo del historico desde que la Solicitud queda Cerrada. Lo consume el job de purga de fase 3 (D-35). Aprobado 2026-09-22."
    },
    {
      "sanic_nombre": "retencion.dias.noreconocidas",
      "sanic_version": 1,
      "sanic_tipo": {
        "choice": "sanic_mppp_ch_tipoparametro",
        "etiqueta": "Numero"
      },
      "sanic_valor": "30",
      "sanic_descripcion": "Plazo corto para las No reconocidas y Descartadas (DD-06). Lo consume el job de purga de fase 3. Aprobado 2026-09-22."
    },
    {
      "sanic_nombre": "plantilla.estructura",
      "sanic_version": 1,
      "sanic_tipo": {
        "choice": "sanic_mppp_ch_tipoparametro",
        "etiqueta": "JSON"
      },
      "sanic_valor": "{\"hoja\": \"Datos\", \"filaEncabezado\": 12, \"primeraFila\": 13, \"cantidadFilas\": 100, \"campos\": [{\"nombre\": \"gestion\", \"columna\": \"B\", \"encabezado\": \"Gestión\", \"formato\": \"texto\", \"largoMinimo\": 1, \"largoMaximo\": 20}, {\"nombre\": \"clasificacion\", \"columna\": \"C\", \"encabezado\": \"Clasificación\", \"formato\": \"texto\", \"largoMinimo\": 1, \"largoMaximo\": 10}, {\"nombre\": \"numeroPlan\", \"columna\": \"D\", \"encabezado\": \"No. Plan\", \"formato\": \"alfanumerico\", \"largoMinimo\": 1, \"largoMaximo\": 4}, {\"nombre\": \"nombreBeneficiario\", \"columna\": \"E\", \"encabezado\": \"Nombre Colaborador / Proveedor\", \"formato\": \"texto\", \"largoMinimo\": 1, \"largoMaximo\": 44}, {\"nombre\": \"tipoIdentificacion\", \"columna\": \"F\", \"encabezado\": \"Tipo ID\", \"formato\": \"texto\", \"largoMinimo\": 1, \"largoMaximo\": 10}, {\"nombre\": \"numeroIdentificacion\", \"columna\": \"G\", \"encabezado\": \"No. Identificación\", \"formato\": \"texto\", \"largoMinimo\": 1, \"largoMaximo\": 16}, {\"nombre\": \"referencia\", \"columna\": \"H\", \"encabezado\": \"Referencia\", \"formato\": \"texto\", \"largoMinimo\": 1, \"largoMaximo\": 20}, {\"nombre\": \"numeroCuenta\", \"columna\": \"I\", \"encabezado\": \"No. Cuenta\", \"formato\": \"texto\", \"largoMinimo\": 1, \"largoMaximo\": 16}, {\"nombre\": \"moneda\", \"columna\": \"J\", \"encabezado\": \"Moneda\", \"formato\": \"texto\", \"largoMinimo\": 1, \"largoMaximo\": 10}, {\"nombre\": \"banco\", \"columna\": \"K\", \"encabezado\": \"Banco\", \"formato\": \"texto\", \"largoMinimo\": 1, \"largoMaximo\": 20}]}",
      "sanic_descripcion": "Hoja, ventana de filas y las 10 columnas B-K con su encabezado, formato y largos. Encabezados leidos de la plantilla real del cliente."
    },
    {
      "sanic_nombre": "plantilla.listas",
      "sanic_version": 1,
      "sanic_tipo": {
        "choice": "sanic_mppp_ch_tipoparametro",
        "etiqueta": "JSON"
      },
      "sanic_valor": "{\"gestion\": [{\"valor\": \"Inclusion\", \"variantes\": []}, {\"valor\": \"Exclusion\", \"variantes\": []}, {\"valor\": \"Modificacion\", \"variantes\": []}], \"clasificacion\": [{\"valor\": \"BAC\", \"variantes\": []}, {\"valor\": \"ACH\", \"variantes\": []}, {\"valor\": \"CK\", \"variantes\": []}], \"tipoIdentificacion\": [{\"valor\": \"CNA\", \"variantes\": []}, {\"valor\": \"CRE\", \"variantes\": []}, {\"valor\": \"PAS\", \"variantes\": []}, {\"valor\": \"PEX\", \"variantes\": []}, {\"valor\": \"RUC\", \"variantes\": []}], \"moneda\": [{\"valor\": \"COR\", \"variantes\": []}, {\"valor\": \"USD\", \"variantes\": []}], \"banco\": [{\"valor\": \"LAFISE\", \"variantes\": [], \"codigo\": \"006\"}, {\"valor\": \"BAC\", \"variantes\": [], \"codigo\": \"007\"}, {\"valor\": \"BANPRO\", \"variantes\": [], \"codigo\": \"008\"}, {\"valor\": \"FICOHSA\", \"variantes\": [], \"codigo\": \"009\"}, {\"valor\": \"BDF\", \"variantes\": [], \"codigo\": \"012\"}, {\"valor\": \"AVANZ\", \"variantes\": [], \"codigo\": \"019\"}, {\"valor\": \"PRODUZCAMOS\", \"variantes\": [], \"codigo\": \"023\"}, {\"valor\": \"ATLANTIDA\", \"variantes\": [], \"codigo\": \"037\"}]}",
      "sanic_descripcion": "Valores aceptados de cada lista, con el codigo de 3 digitos del banco (DD-10). Codigos leidos de la plantilla real del cliente."
    },
    {
      "sanic_nombre": "rpa.codigos",
      "sanic_version": 1,
      "sanic_tipo": {
        "choice": "sanic_mppp_ch_tipoparametro",
        "etiqueta": "JSON"
      },
      "sanic_valor": "{\"OK\": {\"cliente\": null, \"nota\": \"AS400 acepto la operacion. No genera texto al cliente.\"}, \"RECH-GEN\": {\"cliente\": \"Su solicitud no pudo ser aplicada. Por favor comuniquese con su ejecutivo de cuenta.\", \"nota\": \"Rechazo de AS400 sin clasificar todavia. Texto PROVISIONAL: lo aprueba el negocio (D-54).\"}}",
      "sanic_descripcion": "Traduce el codigo que manda el bot al texto que lee el cliente (03 \u00a73). Arranca con OK y un rechazo generico y crece de a poco. Un codigo que no esta aca es MPPP-RPA-005, fallo NO reintentable: la fila no se mueve. El texto de RECH-GEN es PROVISIONAL, lo aprueba el negocio (D-54)."
    },
    {
      "sanic_nombre": "rpa.horas.tolerancia",
      "sanic_version": 1,
      "sanic_tipo": {
        "choice": "sanic_mppp_ch_tipoparametro",
        "etiqueta": "Numero"
      },
      "sanic_valor": "24",
      "sanic_descripcion": "Ventana dentro de la cual se acepta fechaproceso (03 \u00a73). Fuera de ella, o en el futuro, se rechaza. Valor PROVISIONAL elegido por el constructor (D-54). El codigo lleva su propio fallback quemado: que falte este parametro degrada el control, no tumba al bot."
    },
    {
      "sanic_nombre": "rpa.minutos.claimmaximo",
      "sanic_version": 1,
      "sanic_tipo": {
        "choice": "sanic_mppp_ch_tipoparametro",
        "etiqueta": "Numero"
      },
      "sanic_valor": "60",
      "sanic_descripcion": "Tope de minutos que puede durar una reserva del bot sobre una fila (03 \u00a72). El bot pide claimminutos; si pide mas, se recorta a este tope. Es lo que acota el dano de un bot colgado, y por eso no hay boton Liberar. Valor PROVISIONAL (D-54)."
    },
    {
      "sanic_nombre": "rpa.lote.maximo",
      "sanic_version": 1,
      "sanic_tipo": {
        "choice": "sanic_mppp_ch_tipoparametro",
        "etiqueta": "Numero"
      },
      "sanic_valor": "100",
      "sanic_descripcion": "Cuantos resultados acepta registrarresultadorpa en una llamada (03 \u00a73). Un lote mas grande se rechaza ENTERO (MPPP-RPA-008), nunca se recorta. Lo manda el limite de 2 minutos de la operacion (BP-PP-053), no la cuota. **MEDIDO en Dev el 2026-09-26**: 0,33 s por item, lineal de 10 a 50 (10->3,9s · 25->7,9s · 50->16,7s). Con 100 son ~33 s, el 28% del presupuesto, con mas de 3x de margen para degradacion. **Hay que volver a medirlo en Produccion**: Dev vacio no es Produccion con carga y con tablas grandes."
    },
    {
      "sanic_nombre": "exportacion.columnas",
      "sanic_version": 1,
      "sanic_tipo": {
        "choice": "sanic_mppp_ch_tipoparametro",
        "etiqueta": "JSON"
      },
      "sanic_valor": "{\"columnas\": [{\"campo\": \"numeroSolicitud\", \"encabezado\": \"No. Solicitud\"}, {\"campo\": \"numeroFila\", \"encabezado\": \"No. Fila\"}, {\"campo\": \"gestion\", \"encabezado\": \"Gestión\"}, {\"campo\": \"clasificacion\", \"encabezado\": \"Clasificación\"}, {\"campo\": \"numeroPlan\", \"encabezado\": \"No. Plan\"}, {\"campo\": \"nombreBeneficiario\", \"encabezado\": \"Nombre Colaborador / Proveedor\"}, {\"campo\": \"tipoIdentificacion\", \"encabezado\": \"Tipo ID\"}, {\"campo\": \"numeroIdentificacion\", \"encabezado\": \"No. Identificación\"}, {\"campo\": \"referencia\", \"encabezado\": \"Referencia\"}, {\"campo\": \"numeroCuenta\", \"encabezado\": \"No. Cuenta\"}, {\"campo\": \"moneda\", \"encabezado\": \"Moneda\"}, {\"campo\": \"banco\", \"encabezado\": \"Banco\"}]}",
      "sanic_descripcion": "DN-02 (2026-09-26): orden de columnas del Excel de 'Descargar para digitar' (sanic_mppp_capi_exportarparadigitar); el orden del arreglo ES el orden de columnas (A = primer elemento). numeroSolicitud/numeroFila dan trazabilidad; el resto es lo que el ejecutivo necesita para digitar en AS400. DECISION DEL NEGOCIO: el conjunto de columnas se cambia editando este parametro, nunca tocando codigo."
    }
  ]
}
```

## 3. Precondiciones

- La tabla `sanic_mppp_tbl_parametro` y su clave `sanic_mppp_key_parametro_nombre_version` existen.
- El step `MPPP - Normalizar y validar - Create de Parametro` esta registrado: normaliza `sanic_nombre` a minusculas y exige el formato separado por puntos. Si un nombre no pasa, el alta se rechaza y se ve el motivo.
