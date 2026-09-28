// Lectura minima del .xlsx descargado, para verificar que DN-02 bajo un
// archivo valido con las columnas y filas esperadas (no solo "algo se bajo").
//
// POR QUE UN PARSER PROPIO Y NO `exceljs` (decision tomada en vivo,
// 2026-09-27, con evidencia, no de entrada): el primer intento uso
// `exceljs`, y fallo con un .xlsx real descargado desde la app ("Cannot read
// properties of undefined (reading 'sheets')"). Investigando la causa (no
// asumiendola): el `xl/workbook.xml` que arma nuestro backend (C# + Open XML
// SDK 3.1.0, `ArmadorExportacionOpenXml`) declara el namespace de
// spreadsheetml con el PREFIJO `x:` (`<x:workbook>`, `<x:sheets>`...) en vez
// de dejarlo como namespace por defecto. Es OOXML igual de VALIDO (un prefijo
// de namespace es arbitrario por spec), pero el parser SAX de `exceljs`
// matchea nombres de tag a secas ("workbook", "sheets") y con el prefijo no
// los reconoce. Es una limitacion de esa libreria, no un defecto del archivo
// — pero el arnes necesita leerlo igual, y agregar `xlsx` (SheetJS) no fue
// opcion: la version publicada en npm (0.18.5, la ultima) carga con un CVE
// de prototype pollution sin parche en npm (el parche solo existe en su CDN
// propio). Por eso: unzip con `jszip` (la MISMA libreria que usa `exceljs`
// por dentro, ya vetada) y un parseo de XML a mano, TOLERANTE a cualquier
// prefijo de namespace porque busca por nombre de tag local con regex, no
// por namespace exacto.

import JSZip from "jszip";
import { readFile } from "node:fs/promises";

/** Decodifica las entidades XML basicas que pueden aparecer en texto de celda. */
function decodificarEntidades(texto) {
  return texto
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'")
    .replace(/&amp;/g, "&");
}

/** `sharedStrings.xml` -> arreglo de strings, en orden (el indice ES la posicion). */
function parsearSharedStrings(xml) {
  if (!xml) return [];
  // Cada <si>...</si> puede tener uno o más <t>texto</t> (runs con formato);
  // se concatenan todos los <t> de un mismo <si>.
  const bloques = xml.match(/<(?:[A-Za-z0-9]+:)?si\b[^>]*>[\s\S]*?<\/(?:[A-Za-z0-9]+:)?si>/g) || [];
  return bloques.map((bloque) => {
    const textos = [...bloque.matchAll(/<(?:[A-Za-z0-9]+:)?t\b[^>]*>([\s\S]*?)<\/(?:[A-Za-z0-9]+:)?t>/g)];
    return decodificarEntidades(textos.map((m) => m[1]).join(""));
  });
}

/** Letras de columna ("A", "B", ..., "AA") -> índice base 0, desde una referencia tipo "C7". */
function columnaDesdeReferencia(referencia) {
  const letras = (referencia.match(/^[A-Z]+/) || [""])[0];
  let indice = 0;
  for (const letra of letras) indice = indice * 26 + (letra.charCodeAt(0) - 64);
  return indice - 1;
}

/** Parsea `xl/worksheets/sheet1.xml` a una matriz de filas (arreglos de strings). */
function parsearHoja(xml, sharedStrings) {
  const filasXml = xml.match(/<(?:[A-Za-z0-9]+:)?row\b[^>]*>[\s\S]*?<\/(?:[A-Za-z0-9]+:)?row>/g) || [];
  return filasXml.map((filaXml) => {
    const celdas = [
      ...filaXml.matchAll(/<(?:[A-Za-z0-9]+:)?c\b([^>]*)>([\s\S]*?)<\/(?:[A-Za-z0-9]+:)?c>|<(?:[A-Za-z0-9]+:)?c\b([^>]*)\/>/g),
    ];
    const fila = [];
    for (const celda of celdas) {
      const atributos = celda[1] ?? celda[3] ?? "";
      const contenido = celda[2] ?? "";
      const refMatch = atributos.match(/\br="([A-Z]+\d+)"/);
      const columna = refMatch ? columnaDesdeReferencia(refMatch[1]) : fila.length;
      const tipo = (atributos.match(/\bt="([^"]+)"/) || [])[1];

      let valor = "";
      if (tipo === "inlineStr") {
        const t = contenido.match(/<(?:[A-Za-z0-9]+:)?t\b[^>]*>([\s\S]*?)<\/(?:[A-Za-z0-9]+:)?t>/);
        valor = t ? decodificarEntidades(t[1]) : "";
      } else {
        const v = contenido.match(/<(?:[A-Za-z0-9]+:)?v\b[^>]*>([\s\S]*?)<\/(?:[A-Za-z0-9]+:)?v>/);
        const crudo = v ? v[1] : "";
        valor = tipo === "s" ? sharedStrings[Number(crudo)] ?? "" : decodificarEntidades(crudo);
      }

      while (fila.length < columna) fila.push("");
      fila[columna] = valor;
    }
    return fila;
  });
}

/**
 * Lee la primera hoja de un .xlsx (formato producido por
 * `ArmadorExportacionOpenXml`) y devuelve encabezados + filas de datos.
 * Si el archivo no es un .zip valido, JSZip tira, y ESO es la señal correcta
 * de "no bajó un Excel válido" (no se atrapa el error acá).
 */
export async function leerXlsx(rutaArchivo) {
  const buffer = await readFile(rutaArchivo);
  const zip = await JSZip.loadAsync(buffer);

  const hojaEntry = zip.file("xl/worksheets/sheet1.xml");
  if (!hojaEntry) {
    throw new Error("El .xlsx no tiene 'xl/worksheets/sheet1.xml' — no es la estructura esperada.");
  }
  const hojaXml = await hojaEntry.async("string");

  const sharedEntry = zip.file("xl/sharedStrings.xml");
  const sharedStrings = sharedEntry ? parsearSharedStrings(await sharedEntry.async("string")) : [];

  const filas = parsearHoja(hojaXml, sharedStrings);
  const [encabezados, ...datos] = filas;
  return { encabezados: encabezados ?? [], filas: datos };
}
