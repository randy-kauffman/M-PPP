// Selectores CENTRALIZADOS de los botones de la barra de comandos (ribbon) de Fila
// (`sanic_mppp_tbl_fila`). Un solo archivo: si Microsoft cambia el DOM del command bar,
// o el proyecto cambia una etiqueta, se toca ACA y nada mas.
//
// ---------------------------------------------------------------------------------
// COMO SE OBTUVO ESTA LISTA (verificable, no inventada):
//
// Los 7 botones, sus ids de comando, entidad, ubicaciones y etiquetas en los dos
// idiomas (1033 EN-US, 3082 ES) salen TAL CUAL del playbook que generó el
// `RibbonDiffXml` real de `sanic_mppp_tbl_fila`:
//   playbooks/importacion/comandos.md (seccion 2, tabla "sanic_mppp_tbl_fila")
// Ese playbook es la fuente de verdad que YA se aplico al entorno Dev (es la
// orden que uso la herramienta de importacion), asi que sus ids y etiquetas
// son un hecho, no una suposicion.
//
// ---------------------------------------------------------------------------------
// ESTRATEGIA DE SELECTOR (por que texto/rol es el PRIMARIO, y el `data-id` un respaldo):
//
// Se investigo en la documentacion oficial de Microsoft (Microsoft Learn,
// "Power Platform Playwright samples > Locators", 2026-09) como el propio
// toolkit oficial de Microsoft ubica botones del command bar en Playwright.
// La recomendacion documentada es usar locators semanticos por nombre
// accesible, no CSS por `data-id` crudo.
//
// CORREGIDO EN VIVO (2026-09-27, con evidencia real, no de entrada): el rol
// de accesibilidad real de estos comandos es **"menuitem"**, no "button"
// (confirmado leyendo el `error-context.md` que Playwright adjunta a una
// corrida fallida: `region "Fila Commands" > menubar > menuitem "Mark as
// entered..."`). Ademas, el NOMBRE accesible de cada item es la etiqueta
// SEGUIDA del tooltip completo, pegados sin separador (ej.: `"Mark as
// entered Marks the selected rows as entered in AS400."`) — por eso
// `ayudantes/ribbon.mjs` matchea por PREFIJO (`^etiqueta`), no por texto
// exacto. Los dos hallazgos salieron de mirar el DOM real, no de la
// documentacion general de Microsoft (que no baja a ese nivel de detalle).
//
// Por eso la estrategia final es:
//   1) PRIMARIO, verificado en vivo: `getByRole('menuitem', { name: /^(1033|3082)/ })`
//      dentro del "Fila Commands" menubar, probando las DOS etiquetas del
//      boton (no se puede asumir el idioma de la sesion: la sesion de prueba
//      disponible tenia la UI en ingles). Ver `ayudantes/ribbon.mjs`.
//   2) SECUNDARIO, opcional: `dataId`. Se completó corriendo
//      `descubrir-selectores.mjs` con sesión real (2026-09-27) para 5 de los
//      7 botones — los que se pueden seleccionar desde "Mis clientes - por
//      digitar" (Estado = Validada). Sigue el patrón
//      `<tabla>|NoRelationship|HomePageGrid|sanic.mppp.fila.<id>.HomepageGrid.Button`.
//      `aprobar` y `devolver` quedan en `null`: solo se dibujan con una fila
//      en Digitada seleccionada (vista "Por aprobar"), fuera del alcance del
//      descubrimiento hecho hasta ahora — casi seguro siguen el mismo
//      patrón, pero no se escribe un valor sin haberlo visto en el DOM.
//
// Si el dia de manana un boton no aparece y la causa es la metadata compilada
// del ribbon (el defecto que motivo este arnes), la estrategia 1 lo va a
// detectar igual: no hay ningun `data-id` escondido que lo tape.
// ---------------------------------------------------------------------------------

export const IDIOMA_POR_DEFECTO = 3082; // Espanol (convencion del proyecto)

// Mensajes del JS (A-13), copiados TAL CUAL de `recursos/js/comandos.js` (objeto
// `TEXTOS`, líneas 98-127) — la plantilla del diálogo de confirmación que arma
// `openConfirmDialog` antes de aplicar un comando. `{accion}` y `{n}` se sustituyen
// en tiempo real (`armar()` en comandos.js); acá solo se arma la MISMA plantilla
// para comparar contra el texto real del diálogo.
export const TEXTOS_JS = {
  1033: { confirmar: 'Apply "{accion}" to {n} selected row(s)?' },
  3082: { confirmar: '¿Aplicar "{accion}" a {n} fila(s) seleccionada(s)?' },
};

export function armarTexto(plantilla, valores) {
  let texto = plantilla;
  for (const [clave, valor] of Object.entries(valores)) {
    texto = texto.split(`{${clave}}`).join(valor);
  }
  return texto;
}

/**
 * Los 7 botones propios de la barra de Fila, secuencia 10 a 70
 * (playbooks/importacion/comandos.md).
 */
export const BOTONES_FILA = [
  {
    id: "digitada",
    entidad: "sanic_mppp_tbl_fila",
    funcion: "Sanic.Mppp.Comandos.digitada",
    ubicaciones: ["HomepageGrid", "SubGrid"],
    secuencia: 10,
    etiqueta: { 1033: "Mark as entered", 3082: "Digitada" },
    dataId: "sanic_mppp_tbl_fila|NoRelationship|HomePageGrid|sanic.mppp.fila.digitada.HomepageGrid.Button",
  },
  {
    id: "aprobar",
    entidad: "sanic_mppp_tbl_fila",
    funcion: "Sanic.Mppp.Comandos.aprobar",
    ubicaciones: ["HomepageGrid", "SubGrid"],
    secuencia: 20,
    etiqueta: { 1033: "Approve", 3082: "Aprobar" },
    // PENDIENTE: no se pudo descubrir. Este comando solo se dibuja con una
    // fila EN ESTADO DIGITADA seleccionada (TRANSICIONES_VALIDAS.aprobar), y
    // esa vive en la vista "Por aprobar", fuera del alcance de
    // descubrir-selectores.mjs por ahora. Sigue el mismo patrón que los
    // demás casi con certeza (ver dataId de 'digitada' más arriba):
    // "sanic_mppp_tbl_fila|NoRelationship|HomePageGrid|sanic.mppp.fila.aprobar.HomepageGrid.Button"
    // pero NO se escribe como valor real sin haberlo visto en el DOM: sería
    // inventarlo, exactamente lo que este archivo existe para evitar.
    dataId: null,
  },
  {
    id: "rechazadaas400",
    entidad: "sanic_mppp_tbl_fila",
    funcion: "Sanic.Mppp.Comandos.rechazadaAs400",
    ubicaciones: ["HomepageGrid", "SubGrid"],
    secuencia: 30,
    etiqueta: { 1033: "Rejected in AS400", 3082: "Rechazada en AS400" },
    dataId: "sanic_mppp_tbl_fila|NoRelationship|HomePageGrid|sanic.mppp.fila.rechazadaas400.HomepageGrid.Button",
  },
  {
    id: "anular",
    entidad: "sanic_mppp_tbl_fila",
    funcion: "Sanic.Mppp.Comandos.anular",
    ubicaciones: ["HomepageGrid", "SubGrid"],
    secuencia: 40,
    etiqueta: { 1033: "Annul", 3082: "Anular" },
    dataId: "sanic_mppp_tbl_fila|NoRelationship|HomePageGrid|sanic.mppp.fila.anular.HomepageGrid.Button",
  },
  {
    id: "devolver",
    entidad: "sanic_mppp_tbl_fila",
    funcion: "Sanic.Mppp.Comandos.devolver",
    ubicaciones: ["HomepageGrid", "SubGrid"],
    secuencia: 50,
    etiqueta: { 1033: "Send back", 3082: "Devolver" },
    // PENDIENTE, mismo motivo que 'aprobar': solo se dibuja con una fila en
    // Digitada seleccionada (vista "Por aprobar"), no descubierto todavía.
    dataId: null,
  },
  {
    id: "atendida",
    entidad: "sanic_mppp_tbl_fila",
    funcion: "Sanic.Mppp.Comandos.atendida",
    ubicaciones: ["HomepageGrid", "SubGrid"],
    secuencia: 60,
    etiqueta: { 1033: "Mark attended", 3082: "Atendida" },
    dataId: "sanic_mppp_tbl_fila|NoRelationship|HomePageGrid|sanic.mppp.fila.atendida.HomepageGrid.Button",
  },
  {
    id: "descargarparadigitar",
    entidad: "sanic_mppp_tbl_fila",
    funcion: "Sanic.Mppp.Comandos.descargarParaDigitar",
    ubicaciones: ["HomepageGrid", "SubGrid"],
    secuencia: 70,
    etiqueta: { 1033: "Download for entry", 3082: "Descargar para digitar" },
    dataId: "sanic_mppp_tbl_fila|NoRelationship|HomePageGrid|sanic.mppp.fila.descargarparadigitar.HomepageGrid.Button",
  },
];

export function botonPorId(id) {
  const boton = BOTONES_FILA.find((b) => b.id === id);
  if (!boton) {
    throw new Error(
      `No existe el boton '${id}' en selectores.mjs. IDs conocidos: ` +
        BOTONES_FILA.map((b) => b.id).join(", ")
    );
  }
  return boton;
}

export function etiquetaDe(boton, idioma = IDIOMA_POR_DEFECTO) {
  const etiqueta = boton.etiqueta[idioma];
  if (!etiqueta) {
    throw new Error(`El boton '${boton.id}' no tiene etiqueta para el idioma ${idioma}.`);
  }
  return etiqueta;
}

// Las dos etiquetas posibles de un boton, en el orden en que conviene
// probarlas. NO se puede asumir el idioma de la sesion: se comprobó en vivo
// (2026-09-27) que la sesión de prueba disponible (`local/sesion-ejecutivo.json`)
// tiene la UI en ingles (1033), aunque el sitemap y la convención del
// proyecto son en español — el idioma de UI es una preferencia POR USUARIO
// (Configuración personal → Idiomas), no algo que este arnés controle. Por
// eso `ayudantes/ribbon.mjs` prueba las dos etiquetas de cada botón en vez
// de asumir una.
export function etiquetas(boton) {
  return [boton.etiqueta[1033], boton.etiqueta[3082]].filter(Boolean);
}

// Estados de Fila (`sanic_mppp_ch_estadofila`, playbooks/choice-global/) que
// habilitan cada comando con EnableRule propio — copiado TAL CUAL de
// `TRANSICIONES_VALIDAS` en `recursos/js/comandos.js` (función `algunaAdmite`:
// el botón se habilita si AL MENOS UNA fila seleccionada está en uno de estos
// estados). Los comandos que NO aparecen acá (`atendida`,
// `descargarparadigitar`) no tienen EnableRule de estado: alcanza con
// seleccionar algo (`Mscrm.SelectionCountAtLeastOne`).
//
// Confirmado EN VIVO (2026-09-27) que esto importa de verdad para el DOM, no
// solo en teoría: con una fila en Validada seleccionada, "Approve" y "Send
// back" NO se dibujan (desaparecen del todo, no quedan grises) porque
// ninguna fila seleccionada admite esas dos acciones — es el EnableRule
// funcionando bien, NO el defecto de metadata que motivó este arnés. Un caso
// de ribbon que no tenga esto en cuenta reporta un falso positivo.
export const ESTADOFILA = {
  RECHAZADA_EN_VALIDACION: 159460001,
  SIN_AUTORIZACION: 159460002,
  VALIDADA: 159460003,
  DIGITADA: 159460004,
  APROBADA: 159460005,
  RECHAZADA_EN_AS400: 159460006,
  ANULADA: 159460007,
};

export const TRANSICIONES_VALIDAS = {
  digitada: [ESTADOFILA.VALIDADA],
  aprobar: [ESTADOFILA.DIGITADA],
  rechazadaas400: [ESTADOFILA.VALIDADA, ESTADOFILA.DIGITADA],
  anular: [ESTADOFILA.VALIDADA, ESTADOFILA.DIGITADA],
  devolver: [ESTADOFILA.DIGITADA],
};

/**
 * Dado el conjunto de estados de Fila seleccionados (puede repetirse), qué
 * botones de `BOTONES_FILA` debería mostrar la barra, presentes y
 * habilitados. Espejo exacto de `algunaAdmite` en `recursos/js/comandos.js`.
 */
export function botonesEsperados(estadosSeleccionados) {
  if (!estadosSeleccionados || estadosSeleccionados.length === 0) {
    // Confirmado en vivo: sin selección, la plataforma no dibuja NINGUNO de
    // los 7 botones propios (no es "presentes pero deshabilitados" — es
    // ausencia total del grupo). Distinto de lo que asumía una versión
    // anterior de este archivo, corregido tras verificar contra la app real.
    return [];
  }
  return BOTONES_FILA.filter((boton) => {
    const requeridos = TRANSICIONES_VALIDAS[boton.id];
    if (!requeridos) return true; // sin EnableRule de estado: siempre que haya selección
    return estadosSeleccionados.some((estado) => requeridos.includes(estado));
  }).map((boton) => boton.id);
}

// Columnas esperadas del .xlsx de "Descargar para digitar" (DN-02), en orden.
// Fuente: parametro `exportacion.columnas` sembrado en
// playbooks/datos/parametros.md — el orden del arreglo ES el orden de columnas
// del Excel (A = primer elemento). Si el parametro cambia en el entorno, esta
// lista deja de coincidir y el caso DN-02 lo va a marcar como FALLA: es la
// senal correcta de que hay que actualizar este archivo o de que el parametro
// se toco sin avisar.
export const COLUMNAS_EXPORTACION_ESPERADAS = [
  "No. Solicitud",
  "No. Fila",
  "Gestión",
  "Clasificación",
  "No. Plan",
  "Nombre Colaborador / Proveedor",
  "Tipo ID",
  "No. Identificación",
  "Referencia",
  "No. Cuenta",
  "Moneda",
  "Banco",
];

// Patron del nombre del archivo que arma `comandos.js` (nombreDeArchivoExportacion):
// "MPPP-para-digitar-YYYYMMDD-HHMMSS.xlsx"
export const PATRON_NOMBRE_ARCHIVO_EXPORTACION = /^MPPP-para-digitar-\d{8}-\d{6}\.xlsx$/;

// ---------------------------------------------------------------------------------
// A-02 (12.6) -- los 6 comandos GENERICOS que DA-06 oculta en TODAS las tablas, mas
// "Nuevo" (oculto solo en las 4 tablas donde el alta no es de una persona). Son
// comandos ESTANDAR de la plataforma (no un CustomAction del proyecto, a diferencia
// de BOTONES_FILA de arriba): las etiquetas 1033/3082 son las que Microsoft publica
// para Unified Interface.
//
// CONFIRMADO EN VIVO (2026-09-28, primera corrida real, tabla `AutorizacionCliente`
// -- el único lugar donde DA-06 resultó NO estar aplicado, ver `05-app-y-comandos.md`
// A-02): "Email a Link" es el nombre accesible TAL CUAL (sin prefijo). "Flow" en
// cambio NO aparece solo: el nombre accesible real es "Run Flow ..." (corregido
// abajo). `eliminar`/`asignar`/`compartir`/`combinar` NO se pudieron confirmar
// contra el DOM real todavía porque en NINGUNA tabla de este entorno aparecieron
// (DA-06 los tiene bien ocultos en todos lados) -- sin un lugar donde estén
// presentes no hay como leer su nombre accesible real; quedan con la etiqueta
// estándar de Microsoft Learn hasta que alguna corrida los encuentre.
export const COMANDOS_GENERICOS = [
  { id: "eliminar", etiqueta: { 1033: "Delete", 3082: "Eliminar" } },
  { id: "asignar", etiqueta: { 1033: "Assign", 3082: "Asignar" } },
  { id: "compartir", etiqueta: { 1033: "Share", 3082: "Compartir" } },
  // "Run Flow"/"Ejecutar flujo" confirmado en vivo para 1033; 3082 sigue sin
  // confirmar (esta sesión corrió en inglés) -- se sigue el mismo patrón
  // "Ejecutar X" que usa la plataforma para el resto de comandos con acción.
  { id: "flujo", etiqueta: { 1033: "Run Flow", 3082: "Ejecutar flujo" } },
  { id: "enviarvinculo", etiqueta: { 1033: "Email a Link", 3082: "Enviar vínculo por correo electrónico" } },
  { id: "combinar", etiqueta: { 1033: "Merge", 3082: "Combinar" } },
];

export const COMANDO_NUEVO = { id: "nuevo", etiqueta: { 1033: "New", 3082: "Nuevo" } };

// Las tablas de A-02 (`pruebas/05-app-y-comandos.md`, tabla del caso): en las 4
// primeras (donde el alta es de un proceso, no de una persona) "Nuevo" también tiene
// que estar oculto; en las demás, de catálogo, "Nuevo" SÍ tiene que aparecer (una
// persona da de alta ahí). `sitemap` referencia una clave de `SITEMAP` (config.mjs)
// cuando la tabla tiene entrada propia; `subgrid: true` marca las 2 que solo se ven
// dentro del formulario de Solicitud (ResultadoRegla, Bitácora —
// diseno/05-app-model-driven.md, línea 208: "ResultadoRegla y Bitácora solo se ven
// en subgrilla"). `roles` son los papeles de `local/sesion-<papel>.json` que pueden
// ver la tabla (05-app-model-driven.md §2): HideCustomAction es una personalización
// de TABLA, no de rol, así que un papel por tabla alcanza para probarlo — no hace
// falta repetir el mismo chequeo con las 4 sesiones (ver el caso a02 de `set_a.py`).
//
// CORRECCIÓN 2026-09-28: la versión original de A-02 (`pruebas/05-app-y-comandos.md`)
// contaba "10 tablas", cerrado antes de que `AutorizacionCliente` tuviera su propia
// entrada de sitemap (agregada el 2026-09-27, `diseno/05-app-model-driven.md` §2).
// Son 11 tablas hoy — CONVENCIONES.md §7 ("nombrar, no contar"): se nombran las 11,
// no se repite el número viejo.
// `area` es la ÁREA del sitemap (`diseno/05-app-model-driven.md` §2: Trabajo,
// Consulta, Catálogos, Configuración NO son un árbol plano, son áreas separadas
// con su propio selector "Change area" — confirmado en vivo 2026-09-28, ver
// `ayudantes/navegacion.mjs::cambiarArea`). `undefined` en Trabajo porque es el
// área por defecto de toda sesión, no hace falta cambiar.
export const TABLAS_A02 = [
  { tabla: "Solicitud", sitemap: "solicitudes", area: "Consulta", ocultarNuevo: true, roles: ["ejecutivo", "supervisor"] },
  { tabla: "Fila", sitemap: "filas", area: "Consulta", ocultarNuevo: true, roles: ["ejecutivo", "supervisor"] },
  { tabla: "ResultadoRegla", subgrid: true, ocultarNuevo: true, roles: ["ejecutivo", "supervisor"] },
  { tabla: "Bitácora", subgrid: true, ocultarNuevo: true, roles: ["ejecutivo", "supervisor"] },
  { tabla: "Cliente", sitemap: "clientes", area: "Catalogos", ocultarNuevo: false, roles: ["ejecutivo", "supervisor", "admin_planes"] },
  { tabla: "Plan", sitemap: "planes", area: "Catalogos", ocultarNuevo: false, roles: ["ejecutivo", "supervisor", "admin_planes"] },
  { tabla: "Autorizado", sitemap: "autorizados", area: "Catalogos", ocultarNuevo: false, roles: ["ejecutivo", "supervisor", "admin_planes"] },
  { tabla: "AutorizacionPlan", sitemap: "autorizacionesPlanes", area: "Catalogos", ocultarNuevo: false, roles: ["ejecutivo", "supervisor", "admin_planes"] },
  { tabla: "AutorizacionCliente", sitemap: "autorizacionesClientes", area: "Catalogos", ocultarNuevo: false, roles: ["ejecutivo", "supervisor", "admin_planes"] },
  { tabla: "Parámetro", sitemap: "parametros", area: "Configuracion", ocultarNuevo: false, roles: ["admin_tecnico"] },
  { tabla: "Regla", sitemap: "reglas", area: "Configuracion", ocultarNuevo: false, roles: ["admin_tecnico"] },
];
