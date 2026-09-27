# Playbook: web-resource · WR - MPPP - SVG - Comando Descargar para digitar

DN-02 (`diseno/DIRECTRICES-NUEVAS.md`, orden explícita del aprobador de construir en Dev,
2026-09-26). El contenido NO va en este playbook: vive en
`recursos/svg/cmd_descargarparadigitar.svg`, se edita y se revisa como un archivo, y la
herramienta lo sube.

## 1. Identidad

```json
{
  "tipo_playbook": "web-resource",
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
  "tipo": "web-resource",
  "nombre": "sanic_mppp_wr_svg_cmddescargarparadigitar",
  "displayname": "WR - MPPP - SVG - Comando Descargar para digitar",
  "descripcion": "Boton Descargar para digitar de la barra de comandos de Fila (DN-02).",
  "recurso": "SVG",
  "archivo": "recursos/svg/cmd_descargarparadigitar.svg"
}
```

## 3. Precondiciones

- El archivo existe en el repositorio y es un SVG válido.
