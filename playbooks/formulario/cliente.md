# Playbook: formulario · Cliente

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
  "nombre": "Cliente",
  "tabla": "sanic_mppp_tbl_cliente",
  "solo_lectura": false,
  "descripcion": "Formulario principal de Cliente. Lo edita el Administrador de planes.",
  "pestana": "General",
  "encabezado": [
    "sanic_nombre",
    "sanic_cifbac",
    "sanic_cifcom"
  ],
  "secciones": [
    {
      "titulo": "Datos",
      "campos": [
        "ownerid"
      ]
    },
    {
      "titulo": "Planes del cliente",
      "subgrilla": {
        "tabla": "sanic_mppp_tbl_plan",
        "lookup": "sanic_clienteid",
        "vista": "Planes activos"
      }
    },
    {
      "titulo": "Correos autorizados sobre este cliente",
      "subgrilla": {
        "tabla": "sanic_mppp_tbl_autorizacioncliente",
        "lookup": "sanic_clienteid",
        "vista": "Correos autorizados sobre este cliente"
      }
    }
  ]
}
```

Sección agregada el 2026-09-27 (DN-01, orden explícita del aprobador): `AutorizacionCliente` se creó
y nunca se mostró en ningún formulario. Es la tabla de UNIÓN correo↔cliente (un correo autoriza
TODOS los planes de ese cliente a la vez), **no** el lookup directo `Autorizado.Cliente` que `05`
§4 dice que se quitó el 2026-09-24 ("un correo ya no pertenece a un cliente"): esa fue una relación
distinta, de cardinalidad 1:1 desde Autorizado, y sigue sin existir. Mismo patrón que la subgrilla
"Correos autorizados sobre este plan" que ya tiene el formulario de Plan sobre `AutorizacionPlan`.

## 3. Precondiciones

- La tabla, sus columnas y las vistas de las subgrillas existen.
- La relación `sanic_mppp_cliente_autorizacioncliente` (lookup `sanic_clienteid` en
  `sanic_mppp_tbl_autorizacioncliente`, inventario 4.13) existe.
