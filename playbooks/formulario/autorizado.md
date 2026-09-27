# Playbook: formulario · Autorizado

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
  "nombre": "Autorizado",
  "tabla": "sanic_mppp_tbl_autorizado",
  "solo_lectura": false,
  "descripcion": "El autorizado: su correo y el nombre de la persona. El correo NO pertenece a un cliente (2026-09-24); a que puede tocar lo dicen las dos subgrillas: DN-01 permite que un correo esté autorizado por plan (AutorizacionPlan, uno por uno) Y por cliente (AutorizacionCliente, todos los planes de ese cliente a la vez) a la vez; ninguna vía reemplaza a la otra.",
  "pestana": "General",
  "encabezado": [
    "sanic_nombre",
    "sanic_nombrecontacto"
  ],
  "secciones": [
    {
      "titulo": "Planes que puede modificar",
      "subgrilla": {
        "tabla": "sanic_mppp_tbl_autorizacionplan",
        "lookup": "sanic_autorizadoid",
        "vista": "Planes que puede modificar"
      }
    },
    {
      "titulo": "Clientes que puede modificar",
      "subgrilla": {
        "tabla": "sanic_mppp_tbl_autorizacioncliente",
        "lookup": "sanic_autorizadoid",
        "vista": "Clientes que puede modificar"
      }
    }
  ]
}
```

Sección "Clientes que puede modificar" agregada el 2026-09-27, pedido textual del aprobador ("si
quiero entrar por correo y ver con cuántos clientes está relacionado ese correo, ¿cómo lo hago? Hoy
por hoy sí puedo ver los planes, pero no los clientes"). La sección "Planes que puede modificar"
queda intacta, sin tocar.

## 3. Precondiciones

- La tabla, sus columnas y las vistas de las subgrillas existen (incluida "Clientes que puede
  modificar", `playbooks/vista/clientes_que_puede_modificar.md`).
