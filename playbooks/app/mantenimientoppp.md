# Playbook: app · MDA - MPPP - Mantenimiento PPP

Inventario 12.7 (sitemap) y 12.8 (app). Estructura de `05-app-model-driven.md` §sitemap.

Las entradas de Trabajo y Consulta nombran su VISTA: con `Entity=` las cuatro entradas sobre
Fila abririan la misma vista por defecto. Las de Catalogos y Configuracion van con `Entity=`
porque cada una de esas tablas tiene una sola vista por defecto, la de activos.

Cada titulo lleva una etiqueta por cada idioma (`01-convenciones.md`): en el XML del sitemap
la plataforma NO sustituye.

## 1. Identidad

```json
{
  "tipo_playbook": "app",
  "version_skill": "0.1.0",
  "proyecto": "2026-001-referencias-planes-pago",
  "inventario": "12.7+12.8",
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
  "tipo": "app",
  "nombre": "MDA - MPPP - Mantenimiento PPP",
  "uniquename": "sanic_mppp_mda_mantenimientoppp",
  "descripcion": "Una sola app para los cuatro perfiles; cada uno ve solo las entradas cuyos datos puede leer (D-12).",
  "icono": "sanic_mppp_wr_svg_app",
  "sitemap": "sanic_mppp_sm_mantenimientoppp",
  "idiomas": [
    1033,
    3082
  ],
  "roles": [
    "SR - MPPP - Ejecutivo",
    "SR - MPPP - Supervisor",
    "SR - MPPP - Administrador de planes",
    "SR - MPPP - Administrador tecnico",
    "SR - MPPP - Auditor"
  ],
  "tablas": [
    "sanic_mppp_tbl_fila",
    "sanic_mppp_tbl_solicitud",
    "sanic_mppp_tbl_cliente",
    "sanic_mppp_tbl_plan",
    "sanic_mppp_tbl_autorizado",
    "sanic_mppp_tbl_autorizacionplan",
    "sanic_mppp_tbl_autorizacioncliente",
    "sanic_mppp_tbl_parametro",
    "sanic_mppp_tbl_regla",
    "sanic_mppp_tbl_resultadoregla",
    "sanic_mppp_tbl_bitacora",
    "sanic_mppp_tbl_motivoaccion"
  ],
  "areas": [
    {
      "titulo": {
        "1033": "Trabajo",
        "3082": "Trabajo"
      },
      "grupos": [
        {
          "titulo": {
            "1033": "Trabajo",
            "3082": "Trabajo"
          },
          "entradas": [
            {
              "id": "subarea_pordigitar",
              "titulo": {
                "1033": "Por digitar",
                "3082": "Por digitar"
              },
              "tabla": "sanic_mppp_tbl_fila",
              "icono": "sanic_mppp_wr_svg_pordigitar",
              "vista": "Mis clientes - por digitar"
            },
            {
              "id": "subarea_devueltas",
              "titulo": {
                "1033": "Devueltas",
                "3082": "Devueltas"
              },
              "tabla": "sanic_mppp_tbl_fila",
              "icono": "sanic_mppp_wr_svg_devueltas",
              "vista": "Mis clientes - devueltas"
            },
            {
              "id": "subarea_poraprobar",
              "titulo": {
                "1033": "Por aprobar",
                "3082": "Por aprobar"
              },
              "tabla": "sanic_mppp_tbl_fila",
              "icono": "sanic_mppp_wr_svg_poraprobar",
              "vista": "Por aprobar"
            },
            {
              "id": "subarea_paraatencionmanual",
              "titulo": {
                "1033": "Para atencion manual",
                "3082": "Para atencion manual"
              },
              "tabla": "sanic_mppp_tbl_fila",
              "icono": "sanic_mppp_wr_svg_pararevisar",
              "vista": "Para atencion manual"
            },
            {
              "id": "subarea_porclasificar",
              "titulo": {
                "1033": "Por clasificar",
                "3082": "Por clasificar"
              },
              "tabla": "sanic_mppp_tbl_solicitud",
              "icono": "sanic_mppp_wr_svg_porclasificar",
              "vista": "Por clasificar"
            },
            {
              "id": "subarea_pararevisar",
              "titulo": {
                "1033": "Para revisar",
                "3082": "Para revisar"
              },
              "tabla": "sanic_mppp_tbl_solicitud",
              "icono": "sanic_mppp_wr_svg_pararevisar",
              "vista": "Para revisar"
            }
          ]
        }
      ]
    },
    {
      "titulo": {
        "1033": "Consulta",
        "3082": "Consulta"
      },
      "grupos": [
        {
          "titulo": {
            "1033": "Consulta",
            "3082": "Consulta"
          },
          "entradas": [
            {
              "id": "subarea_solicitudes",
              "titulo": {
                "1033": "Solicitudes",
                "3082": "Solicitudes"
              },
              "tabla": "sanic_mppp_tbl_solicitud",
              "icono": "sanic_mppp_wr_svg_solicitudes",
              "vista": "Solicitudes"
            },
            {
              "id": "subarea_filas",
              "titulo": {
                "1033": "Filas",
                "3082": "Filas"
              },
              "tabla": "sanic_mppp_tbl_fila",
              "icono": "sanic_mppp_wr_svg_filas",
              "vista": "Filas"
            }
          ]
        }
      ]
    },
    {
      "titulo": {
        "1033": "Audit",
        "3082": "Auditoria"
      },
      "grupos": [
        {
          "titulo": {
            "1033": "Audit",
            "3082": "Auditoria"
          },
          "entradas": [
            {
              "id": "subarea_auditoriasolicitudes",
              "titulo": {
                "1033": "Requests",
                "3082": "Solicitudes"
              },
              "tabla": "sanic_mppp_tbl_solicitud",
              "icono": "sanic_mppp_wr_svg_solicitudes",
              "vista": "Solicitudes - Auditoria"
            },
            {
              "id": "subarea_auditoriabitacora",
              "titulo": {
                "1033": "Log",
                "3082": "Bitacora"
              },
              "tabla": "sanic_mppp_tbl_bitacora",
              "icono": "sanic_mppp_wr_svg_filas",
              "vista": "Bitacora - Auditoria"
            }
          ]
        }
      ]
    },
    {
      "titulo": {
        "1033": "Catalogos",
        "3082": "Catalogos"
      },
      "grupos": [
        {
          "titulo": {
            "1033": "Catalogos",
            "3082": "Catalogos"
          },
          "entradas": [
            {
              "id": "subarea_clientes",
              "titulo": {
                "1033": "Clientes",
                "3082": "Clientes"
              },
              "tabla": "sanic_mppp_tbl_cliente",
              "icono": "sanic_mppp_wr_svg_clientes"
            },
            {
              "id": "subarea_planes",
              "titulo": {
                "1033": "Planes",
                "3082": "Planes"
              },
              "tabla": "sanic_mppp_tbl_plan",
              "icono": "sanic_mppp_wr_svg_planes"
            },
            {
              "id": "subarea_autorizados",
              "titulo": {
                "1033": "Correos Autorizados",
                "3082": "Correos Autorizados"
              },
              "tabla": "sanic_mppp_tbl_autorizado",
              "icono": "sanic_mppp_wr_svg_autorizados"
            },
            {
              "id": "subarea_autorizaciones",
              "titulo": {
                "1033": "Autorizaciones - Planes",
                "3082": "Autorizaciones - Planes"
              },
              "tabla": "sanic_mppp_tbl_autorizacionplan",
              "icono": "sanic_mppp_wr_svg_autorizaciones"
            },
            {
              "id": "subarea_autorizacionescliente",
              "titulo": {
                "1033": "Autorizaciones - Clientes",
                "3082": "Autorizaciones - Clientes"
              },
              "tabla": "sanic_mppp_tbl_autorizacioncliente",
              "icono": "sanic_mppp_wr_svg_autorizaciones"
            }
          ]
        }
      ]
    },
    {
      "titulo": {
        "1033": "Configuracion",
        "3082": "Configuracion"
      },
      "grupos": [
        {
          "titulo": {
            "1033": "Configuracion",
            "3082": "Configuracion"
          },
          "entradas": [
            {
              "id": "subarea_parametros",
              "titulo": {
                "1033": "Parametros",
                "3082": "Parametros"
              },
              "tabla": "sanic_mppp_tbl_parametro",
              "icono": "sanic_mppp_wr_svg_parametros"
            },
            {
              "id": "subarea_reglas",
              "titulo": {
                "1033": "Reglas",
                "3082": "Reglas"
              },
              "tabla": "sanic_mppp_tbl_regla",
              "icono": "sanic_mppp_wr_svg_reglas"
            }
          ]
        }
      ]
    }
  ]
}
```

## 3. Precondiciones

- Los 22 iconos, las 20 vistas, los 9 formularios y los 4 roles existen. El ícono de la entrada
  nueva `subarea_autorizacionescliente` **no es nuevo**: reúsa `sanic_mppp_wr_svg_autorizaciones`
  (el mismo de `subarea_autorizaciones`), porque las dos entradas son la misma idea de negocio —una
  autorización de correo—, solo que una es por plan y la otra por cliente; no se crea un web
  resource nuevo para esto.
- El ícono de la entrada nueva `subarea_paraatencionmanual` **tampoco es nuevo**: reúsa
  `sanic_mppp_wr_svg_pararevisar` (el mismo de `subarea_pararevisar`, Solicitud), porque las dos
  entradas son el mismo patrón de negocio —"esto quedó marcado, alguien tiene que mirarlo"—, solo
  que una es sobre Fila (el bot la marcó) y la otra sobre Solicitud (la máquina la marcó); no se
  crea un web resource nuevo para esto. Hallazgo del aprobador (2026-09-27): la vista "Para atencion
  manual" existía en Dev desde la fase 2 pero nunca tuvo entrada propia en el sitemap — un olvido,
  no una decisión —, a diferencia de su hermana "Para revisar" que sí la tenía. Se agrega DESPUÉS de
  `subarea_poraprobar` y ANTES de `subarea_porclasificar`: las entradas de Fila van juntas, las de
  Solicitud después.

- **Área «Auditoría» agregada el 2026-11-27 (DN-09, aprobador).** Va entre Consulta y Catálogos y
  tiene dos entradas, ambas sobre su **vista propia** (no la por defecto de la tabla, por la misma
  razón que Trabajo y Consulta: con `Entity=` se abriría la vista de operación):
  `subarea_auditoriasolicitudes` → Solicitud, vista «Solicitudes - Auditoria»
  (`playbooks/vista/solicitudes_auditoria.md`), y `subarea_auditoriabitacora` → Bitácora, vista
  «Bitacora - Auditoria» (`playbooks/vista/bitacora_auditoria.md`). Los títulos del área y del
  grupo son «Audit» (1033) y «Auditoria» (3082); los de las entradas, «Requests»/«Solicitudes» y
  «Log»/«Bitacora». **Ojo:** las demás entradas del sitemap llevan el mismo texto en español en
  los dos idiomas; aquí el 1033 va traducido porque así lo pidió la tarea (área «Audit»). Si el
  aprobador prefiere la regla de las demás entradas, se cambia el texto 1033 de las dos entradas.
  La app **no cambia de tablas**: Solicitud y Bitácora ya estaban en la lista.
- **Íconos:** sin web resource nuevo. `subarea_auditoriasolicitudes` reúsa
  `sanic_mppp_wr_svg_solicitudes` (la misma idea de negocio que la entrada de Consulta) y
  `subarea_auditoriabitacora` reúsa `sanic_mppp_wr_svg_filas`, el ícono de «lista de registros»;
  no hay uno propio de bitácora y la lista de íconos está cerrada (12.1). Si el aprobador quiere uno
  propio, es un web resource nuevo y no es de este cambio.
- **Rol nuevo `SR - MPPP - Auditor`** (6.9, `playbooks/rol/sr_mppp_auditor.md`) en la lista `roles`:
  `app.py` lo asocia a la app (`appmoduleroles_association`). Tiene que existir **antes** de
  correr la app, o `app.py` se detiene. Como el sitemap es uno solo, el auditor ve **todas** las
  áreas (decisión 3 de DN-09) y los demás roles ven también «Auditoría» (Solicitud y Bitácora las
  leen todos), solo que **con** los formularios de siempre.
- **Formularios y vistas como componentes:** este playbook no los lista. `app.py` agrega a la app
  **las tablas** (`appmodulecomponent` tipo 1), no formularios ni vistas sueltos, y las vistas
  entran al sitemap por su `viewid`. Los formularios nuevos «Auditoria» (Solicitud y Fila,
  `playbooks/formulario/`) y el web resource `sanic_mppp_wr_html_auditoria` **no necesitan línea
  aquí**. Se comprueba en vivo (no se supone) que la app los ofrece: si al construir la app se
  hubiera marcado «solo estos formularios», habría que agregarlos; es la verificación 5 y 6 de los
  playbooks de formulario.
- **Orden de construcción:** rol → vistas (las tres) → web resource → formularios → esta app
  (`app.py` vuelve a calcular el sitemap y, como ya existe, lo actualiza; las vistas del sitemap se
  resuelven por nombre y tienen que existir).
- Conteos de arriba, al 2026-11-27: **+1 rol (5)**, **+3 vistas (23)** (`Solicitudes - Auditoria`,
  `Bitacora - Auditoria`, `Filas - Auditoria`; esta última es de la subgrilla, no del sitemap) y
  **+2 formularios (11)**; íconos sin cambio.
