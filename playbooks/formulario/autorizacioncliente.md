# Playbook: formulario · Autorizacion por cliente

Inventario 12.3 (`05-app-model-driven.md` §formularios). El playbook no declara etiquetas
ni classid: la herramienta los resuelve contra la metadata de la tabla.

Formulario nuevo, agregado el 2026-09-27: pedido textual del aprobador ("Quiero agregar un nuevo
correo autorizado al cliente y solo me sale 'Nombre' y 'Owner'"). Hoy `AutorizacionCliente` solo
tiene el formulario "Information" de fábrica, sin ningún lookup visible; este playbook lo reemplaza.
Calcado de `autorizacionplan.md`, con una diferencia de fondo:

1. `sanic_nombre` es, igual que en `AutorizacionPlan`, un nombre **calculado por el step del
   servidor** (correo → cliente): nadie lo digita. Por eso va en el encabezado como los demás tres
   campos, en solo lectura de hecho (lo calcula el servidor), aunque el formulario entero no está
   marcado `solo_lectura` porque el Administrador de planes sí edita el lookup de Cliente y el de
   Autorizado al crear el registro.

Actualizado el 2026-09-27 por decisión del aprobador (ver `playbooks/tabla/sanic_mppp_tbl_autorizacioncliente.md`
§2, "Decisión del aprobador"): `AutorizacionCliente` ahora tiene `sanic_documentofirmado` y
`sanic_fechadocumento`, igual que `AutorizacionPlan`, así que este formulario **agrega** la sección
"Evidencia de la autorizacion" con esos dos campos. Motivo de negocio: autorizar por cliente habilita
TODOS los planes del cliente a la vez, más amplio que autorizar por plan; no puede exigir menos
respaldo documental.

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
  "descripcion": "Formulario principal de Autorizacion por cliente (DN-01): un correo autorizado sobre TODOS los planes de un cliente. Convive con Autorizacion (AutorizacionPlan) por plan; ninguna reemplaza a la otra. La evidencia firmada se agregó el 2026-09-27 por decisión del aprobador.",
  "pestana": "General",
  "encabezado": [
    "sanic_nombre",
    "sanic_autorizadoid",
    "sanic_clienteid"
  ],
  "secciones": [
    {
      "titulo": "Evidencia de la autorizacion",
      "campos": [
        "sanic_documentofirmado",
        "sanic_fechadocumento"
      ]
    }
  ]
}
```

Decisión de este playbook, no del diseño: **se quita la sección "Datos" con `ownerid`** que llevaba
la versión anterior de este playbook. Esa sección no venía del diseño: se había agregado solo porque
`formulario.py` exige al menos una sección con `campos`, `subgrilla` o `webresource`, y hasta el
2026-09-27 esta tabla no tenía ninguna columna propia fuera de las tres del encabezado. Ahora que
`sanic_documentofirmado` y `sanic_fechadocumento` existen, la sección "Evidencia de la autorizacion"
cumple ese mínimo por sí sola con campos que sí vienen del negocio, así que el placeholder de
`ownerid` ya no aporta nada y se retira. `ownerid` sigue estando disponible en el formulario por
las vistas y la seguridad estándar de Dataverse; no hace falta declararlo a mano.

## 3. Precondiciones

- La tabla, sus columnas y los dos lookups existen (inventario 3.13, 4.12, 4.13).
