# Playbook: web-resource · WR - MPPP - SVG - Comando Atendida

Inventario 12.1 (`+1` icono, fase 2: `05-app-model-driven.md` §"Fase 2: dos vistas y un comando
más"). El contenido NO va en este playbook: vive en `recursos/svg/cmd_atendida.svg`, se edita y
se revisa como un archivo, y la herramienta lo sube.

No confundir con `sanic_mppp_wr_svg_cmdatendido` (12.1, fase 1): ese es el botón **Atendido**
sobre **Solicitud** (cierra la solicitud). Este es **Atendida**, sobre **Fila** (apaga
`sanic_requiereatencionmanual`); son dos comandos distintos, en dos tablas distintas, y llevan
íconos distintos a propósito (12.1: "equivocarse de botón es irreversible").

## 1. Identidad

```json
{
  "tipo_playbook": "web-resource",
  "version_skill": "0.1.0",
  "proyecto": "2026-001-referencias-planes-pago",
  "inventario": "12.1",
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
  "nombre": "sanic_mppp_wr_svg_cmdatendida",
  "displayname": "WR - MPPP - SVG - Comando Atendida",
  "descripcion": "Boton Atendida de la barra de comandos de Fila (apaga requiereatencionmanual).",
  "recurso": "SVG",
  "archivo": "recursos/svg/cmd_atendida.svg"
}
```

## 3. Precondiciones

- El archivo existe en el repositorio y es un SVG válido.
