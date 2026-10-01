# Playbook: web-resource · WR - MPPP - SVG - Comando Carga masiva

DN-04 (`diseno/DIRECTRICES-NUEVAS.md`, orden del aprobador de construir DN-04,
2026-10-01). El contenido NO va en este playbook: vive en
`recursos/svg/cmd_cargamasiva.svg`, se edita y se revisa como un archivo, y la
herramienta lo sube.

## 1. Identidad

```json
{
  "tipo_playbook": "web-resource",
  "version_skill": "0.1.0",
  "proyecto": "2026-001-referencias-planes-pago",
  "inventario": "DN-04",
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
  "tipo": "web-resource",
  "nombre": "sanic_mppp_wr_svg_cmdcargamasiva",
  "displayname": "WR - MPPP - SVG - Comando Carga masiva",
  "descripcion": "Menu desplegable Carga masiva de las grillas de catalogos (DN-04).",
  "recurso": "SVG",
  "archivo": "recursos/svg/cmd_cargamasiva.svg"
}
```

## 3. Precondiciones

- El archivo existe en el repositorio y es un SVG válido.
