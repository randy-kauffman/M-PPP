# Playbook: formulario · Autorizacion por cliente

Inventario 12.3 (`05-app-model-driven.md` §formularios). El playbook no declara etiquetas
ni classid: la herramienta los resuelve contra la metadata de la tabla.

Formulario nuevo, agregado el 2026-09-27: pedido textual del aprobador ("Quiero agregar un nuevo
correo autorizado al cliente y solo me sale 'Nombre' y 'Owner'"). Hoy `AutorizacionCliente` solo
tiene el formulario "Information" de fábrica, sin ningún lookup visible; este playbook lo reemplaza.
Calcado de `autorizacionplan.md`, con dos diferencias de fondo:

1. Esta tabla **no tiene** `sanic_documentofirmado` ni `sanic_fechadocumento` (a diferencia de
   `AutorizacionPlan`), así que **no lleva** la sección "Evidencia de la autorizacion".
2. `sanic_nombre` es, igual que en `AutorizacionPlan`, un nombre **calculado por el step del
   servidor** (correo → cliente): nadie lo digita. Por eso va en el encabezado como los demás tres
   campos, en solo lectura de hecho (lo calcula el servidor), aunque el formulario entero no está
   marcado `solo_lectura` porque el Administrador de planes sí edita el lookup de Cliente y el de
   Autorizado al crear el registro.

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
  "nombre": "Autorizacion por cliente",
  "tabla": "sanic_mppp_tbl_autorizacioncliente",
  "solo_lectura": false,
  "descripcion": "Formulario principal de Autorizacion por cliente (DN-01): un correo autorizado sobre TODOS los planes de un cliente. Convive con Autorizacion (AutorizacionPlan) por plan; ninguna reemplaza a la otra. Esta tabla NO lleva evidencia: no tiene sanic_documentofirmado ni sanic_fechadocumento.",
  "pestana": "General",
  "encabezado": [
    "sanic_nombre",
    "sanic_autorizadoid",
    "sanic_clienteid"
  ],
  "secciones": [
    {
      "titulo": "Datos",
      "campos": [
        "ownerid"
      ]
    }
  ]
}
```

Decisión de este playbook, no del diseño: la sección **"Datos" con `ownerid`**. `AutorizacionCliente`
no tiene columnas propias además de las tres del encabezado (`sanic_nombre`, `sanic_autorizadoid`,
`sanic_clienteid`; verificado contra la metadata el 2026-09-27) y no es el lado "uno" de ninguna
relación, así que no admite ninguna subgrilla. `formulario.py` exige al menos una sección con
`campos`, `subgrilla` o `webresource` (la lista no puede ir vacía): con las tres columnas de negocio
ya puestas en el encabezado, no queda ningún campo propio para una sección. Se resuelve con el mismo
patrón que ya usa `playbooks/formulario/cliente.md` §2 (sección "Datos" con `ownerid`): un campo
nativo de toda tabla `usuario`, no inventado para este cambio.

## 3. Precondiciones

- La tabla, sus columnas y los dos lookups existen (inventario 3.13, 4.12, 4.13).
