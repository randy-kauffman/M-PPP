#!/usr/bin/env python3
"""Arma los .xlsx que sube el caso `casos/dn-04-catalogos-masivos.spec.mjs` (DN-04). Ninguno escribe en Dataverse:
uno trae la hoja equivocada (MPPP-CAR-004) y otro un cliente nuevo incompleto (error de fila → "No se grabó nada").
Se regeneran con `python3 pruebas/gui/armar_xlsx_dn04.py`; el resultado se commitea junto al caso."""
import io, os, zipfile

AQUI = os.path.dirname(os.path.abspath(__file__))
DESTINO = os.path.join(AQUI, "datos", "dn04")
ENCABEZADOS = ["CIF BAC", "CIF COM", "Razon social", "Correo ejecutivo", "Codigo plan", "Tipo formato", "Moneda",
               "Correo", "Nombre contacto", "Fecha documento", "Comentarios", "Resultado"]


def xlsx(hoja, filas):
    """filas: [(numero, [(referencia, texto)])], como texto en línea. Zip con fecha fija: el archivo es reproducible."""
    ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main"
    rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships"
    esc = lambda t: t.replace("&", "&amp;").replace("<", "&lt;")
    datos = "".join(f'<row r="{n}">' + "".join(f'<c r="{r}" t="inlineStr"><is><t>{esc(t)}</t></is></c>' for r, t in cs)
                    + "</row>" for n, cs in filas)
    partes = [
        ("[Content_Types].xml", '<?xml version="1.0" encoding="UTF-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>'),
        ("_rels/.rels", f'<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="{rel}/officeDocument" Target="xl/workbook.xml"/></Relationships>'),
        ("xl/workbook.xml", f'<?xml version="1.0" encoding="UTF-8"?><workbook xmlns="{ns}" xmlns:r="{rel}"><sheets><sheet name="{hoja}" sheetId="1" r:id="rId1"/></sheets></workbook>'),
        ("xl/_rels/workbook.xml.rels", f'<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="{rel}/worksheet" Target="worksheets/sheet1.xml"/></Relationships>'),
        ("xl/worksheets/sheet1.xml", f'<?xml version="1.0" encoding="UTF-8"?><worksheet xmlns="{ns}"><sheetData>{datos}</sheetData></worksheet>'),
    ]
    b = io.BytesIO()
    with zipfile.ZipFile(b, "w", zipfile.ZIP_DEFLATED) as z:
        for nombre, contenido in partes:
            z.writestr(zipfile.ZipInfo(nombre, date_time=(2026, 10, 1, 0, 0, 0)), contenido)
    return b.getvalue()


def main():
    os.makedirs(DESTINO, exist_ok=True)
    encabezado = (1, [(f"{chr(65 + i)}1", t) for i, t in enumerate(ENCABEZADOS)])
    archivos = {
        "dn04-carga-hoja-equivocada.xlsx": xlsx("OtraHoja", [encabezado]),
        # Cliente nuevo (CIF que no existe) sin razón social ni ejecutivo: error de fila, nada se graba.
        "dn04-carga-con-error.xlsx": xlsx("CargaCatalogos", [encabezado, (2, [("A2", "999999901"), ("B2", "ZZH000000901")])]),
    }
    for nombre, contenido in archivos.items():
        with open(os.path.join(DESTINO, nombre), "wb") as f:
            f.write(contenido)
        print("escrito", nombre, len(contenido), "bytes")


if __name__ == "__main__":
    main()
