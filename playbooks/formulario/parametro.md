# Playbook: formulario · Parametro

Inventario 12.3 (`05-app-model-driven.md` §formularios). El playbook no declara etiquetas
ni classid: la herramienta los resuelve contra la metadata de la tabla.

## 1. Identidad

```json
{
  "tipo_playbook": "formulario",
  "version_skill": "0.1.0",
  "proyecto": "2026-001-referencias-planes-pago",
  "inventario": "12.3",
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
  "tipo": "formulario",
  "nombre": "Parametro",
  "tabla": "sanic_mppp_tbl_parametro",
  "solo_lectura": false,
  "descripcion": "Formulario principal de Parametro. Lo edita el Administrador tecnico.",
  "pestana": "General",
  "encabezado": [
    "sanic_nombre",
    "sanic_version",
    "sanic_tipo"
  ],
  "secciones": [
    {
      "titulo": "Descripcion",
      "campos": [
        "sanic_descripcion"
      ]
    },
    {
      "titulo": "Valor",
      "campos": [
        "sanic_valor"
      ],
      "expandir": 20
    }
  ]
}
```

DN-06 (2026-11-24): la descripción va arriba, y `sanic_valor` va solo en la última sección con `expandir: 20`. La celda sale con `rowspan="20" auto="true"`: 20 filas como mínimo, y el resto del alto disponible. Es la misma marca que llevan los memo de los formularios de sistema del entorno. Antes, `sanic_valor` era una celda de una fila, y el JSON del parámetro no se podía leer.

## 3. Precondiciones

- La tabla, sus columnas y las vistas de las subgrillas existen.
