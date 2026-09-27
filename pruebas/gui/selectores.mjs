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
// ESTRATEGIA DE SELECTOR (por que texto/rol y no un `data-id` inventado):
//
// Se investigo en la documentacion oficial de Microsoft (Microsoft Learn,
// "Power Platform Playwright samples > Locators", 2026-09) como el propio
// toolkit oficial de Microsoft ubica botones del command bar en Playwright.
// La recomendacion documentada es usar locators semanticos:
//
//     page.getByRole('button', { name: '<etiqueta visible>' })
//
// El toolkit oficial (`power-platform-playwright-toolkit`, `CommandingComponent`)
// hace lo mismo por debajo de `clickButton(nombre)`. En NINGUN lugar de esa
// documentacion se recomienda un CSS selector por `data-id` crudo.
//
// Es CIERTO que en el DOM real de Unified Interface los botones del command bar
// suelen llevar un atributo `data-id` (viene del Id interno del comando), pero
// su valor exacto NO esta documentado publicamente y varia con la version de
// la plataforma. Inventarlo aca seria exactamente el error que este arnes existe
// para evitar (una prueba que "pasa" contra un selector que en realidad no
// corresponde a nada).
//
// Por eso la estrategia es:
//   1) PRIMARIO, ya verificable hoy: `getByRole('button', { name: etiqueta })`
//      dentro del contenedor del command bar (toolbar/menubar), con la etiqueta
//      en el idioma de la sesion activa. Es exactamente lo que Microsoft
//      documenta y lo que ya funciona en A-13 (multiidioma) por construccion:
//      cambia el idioma, cambia el locator, sin tocar el resto del arnes.
//   2) SECUNDARIO, opcional, a completar SOLO con sesion real: `dataId`.
//      Se deja en `null` a proposito. `descubrir-selectores.mjs` (este mismo
//      directorio) abre la vista con una sesion real, ubica cada boton por su
//      etiqueta (estrategia 1, que no depende de `data-id`) y vuelca a consola
//      el `data-id` que encuentre en el DOM, para pegarlo aca a mano una vez
//      confirmado. Mientras `dataId` sea `null`, el arnes usa solo la
//      estrategia 1 y funciona igual.
//
// Si el dia de manana un boton no aparece y la causa es la metadata compilada
// del ribbon (el defecto que motivo este arnes), la estrategia 1 lo va a
// detectar igual: no hay ningun `data-id` escondido que lo tape.
// ---------------------------------------------------------------------------------

export const IDIOMA_POR_DEFECTO = 3082; // Espanol (convencion del proyecto)

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
    dataId: null, // PENDIENTE de descubrir con sesion real (ver nota arriba)
  },
  {
    id: "aprobar",
    entidad: "sanic_mppp_tbl_fila",
    funcion: "Sanic.Mppp.Comandos.aprobar",
    ubicaciones: ["HomepageGrid", "SubGrid"],
    secuencia: 20,
    etiqueta: { 1033: "Approve", 3082: "Aprobar" },
    dataId: null,
  },
  {
    id: "rechazadaas400",
    entidad: "sanic_mppp_tbl_fila",
    funcion: "Sanic.Mppp.Comandos.rechazadaAs400",
    ubicaciones: ["HomepageGrid", "SubGrid"],
    secuencia: 30,
    etiqueta: { 1033: "Rejected in AS400", 3082: "Rechazada en AS400" },
    dataId: null,
  },
  {
    id: "anular",
    entidad: "sanic_mppp_tbl_fila",
    funcion: "Sanic.Mppp.Comandos.anular",
    ubicaciones: ["HomepageGrid", "SubGrid"],
    secuencia: 40,
    etiqueta: { 1033: "Annul", 3082: "Anular" },
    dataId: null,
  },
  {
    id: "devolver",
    entidad: "sanic_mppp_tbl_fila",
    funcion: "Sanic.Mppp.Comandos.devolver",
    ubicaciones: ["HomepageGrid", "SubGrid"],
    secuencia: 50,
    etiqueta: { 1033: "Send back", 3082: "Devolver" },
    dataId: null,
  },
  {
    id: "atendida",
    entidad: "sanic_mppp_tbl_fila",
    funcion: "Sanic.Mppp.Comandos.atendida",
    ubicaciones: ["HomepageGrid", "SubGrid"],
    secuencia: 60,
    etiqueta: { 1033: "Mark attended", 3082: "Atendida" },
    dataId: null,
  },
  {
    id: "descargarparadigitar",
    entidad: "sanic_mppp_tbl_fila",
    funcion: "Sanic.Mppp.Comandos.descargarParaDigitar",
    ubicaciones: ["HomepageGrid", "SubGrid"],
    secuencia: 70,
    etiqueta: { 1033: "Download for entry", 3082: "Descargar para digitar" },
    dataId: null,
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
