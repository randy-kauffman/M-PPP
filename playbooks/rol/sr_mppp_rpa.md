# Playbook: rol · SR - MPPP - RPA

## 1. Identidad

```json
{
  "tipo_playbook": "rol",
  "version_skill": "0.1.0",
  "proyecto": "2026-001-referencias-planes-pago",
  "inventario": "6.7",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 y §7. El rol está definido en
`diseno/04-matriz-privilegios.md` §3 y figura en `diseno/06-inventario-componentes.md` §6,
renglón 6.7.

Advertencias: en Dev existe otro publisher con el mismo prefijo de texto, "Sanic Corp", con otro
unique name; no es el de este proyecto.

## 2. Qué se crea

```json
{
  "tipo": "rol",
  "nombre": "SR - MPPP - RPA",
  "descripcion": "Usuario de aplicación del bot que digita y aprueba en AS400. No escribe ninguna tabla: todo pasa por sus dos Custom API, que graban como SYSTEM. Tener este rol es lo que habilita a llamarlas.",
  "base": "App Opener",
  "tablas": {},
  "otros_privilegios": {},
  "sin_privilegios_propios": "Es un rol de IDENTIDAD, no de acceso: la puerta de las dos Custom API de fase 2 (D-9) es TENER este rol, no lo que el rol permite. El bot no lee ni escribe ninguna tabla; sus API graban como SYSTEM."
}
```

Un security role en la unidad de negocio raíz con **exactamente los privilegios de `App Opener` y
ni uno más**. Es el rol más chico de todo el proyecto, y esa es toda su gracia.

### Por qué no tiene NINGÚN privilegio sobre las tablas de MPPP

**Corrige `04` §3** (decisión del aprobador, 2026-09-25), que le daba lectura en organización sobre
Solicitud, Fila, Cliente y Plan. El bot **no usa ninguno de los cuatro**:

- solo llama a sus dos Custom API (D-14, RNF-07);
- esas API leen y graban **con SYSTEM** (`04` §1), no con los privilegios de quien llama;
- `ExecutePrivilegeName` quedó **vacío** (D-9), así que tampoco hace falta un privilegio para
  invocarlas: la puerta es el plugin, que exige identidad de aplicación **y** este rol.

Es el mismo razonamiento que sacó al RPA del column security profile (`04` §4): **no se otorga un
privilegio para algo que nunca se va a hacer por esa vía.** Si la credencial del bot se filtrara,
quien la tenga no puede leer un solo número de cuenta, ni una fila, ni una solicitud: solo puede
llamar a dos API que validan qué le corresponde hacer.

**Este rol no da acceso: da identidad.** Tenerlo es lo que prueba que quien llama es el bot.

### El nombre de este rol NO es decorativo

**`SR - MPPP - RPA`, exacto, con esas mayúsculas y esos espacios.** Desde D-9 (2026-09-25) el
nombre de este rol es **la puerta de las dos Custom API de fase 2**: el plugin verifica que quien
llama sea una identidad de aplicación **y** tenga este rol, resuelto por `name` (`03` §2 y §3).
El código ya trae la constante: `TablasNativas.RolRpa = "SR - MPPP - RPA"`
(`src/Sanic.Mppp.Plugins/Datos/Avisos.cs`). Un nombre distinto no es un detalle cosmético:
**el bot deja de existir.**

Se identifica por `name` y no por `roleid` a propósito: el nombre viaja con la solución, el id es de
cada entorno (verificado el 2026-09-25: los cinco roles de MPPP tienen `roletemplateid = None`).

Decisiones que este playbook toma y el diseño no traía:

| Decisión | Valor | Por qué |
|---|---|---|
| Descripción del rol | «Usuario de aplicación del bot que digita y aprueba en AS400. No escribe ninguna tabla: todo pasa por sus dos Custom API, que graban como SYSTEM. Tener este rol es lo que habilita a llamarlas.» | La matriz trae el nombre y los privilegios; el texto lo fija el playbook. Se nombra la puerta de D-9 en la descripción porque es lo primero que lee quien audite este rol |

Viene del diseño, no lo decide este playbook:

- Que el RPA no escriba ninguna tabla: `04` §3 y D-14. Que tampoco lea ninguna: decisión del aprobador del 2026-09-25, que corrige `04` §3 (ver arriba).
- **Ningún privilegio de escritura, en ninguna tabla** (D-14, RNF-07): el RPA no escribe nada; sus
  dos Custom API graban como SYSTEM. Rol mínimo de verdad.
- **Ningún `ExecutePrivilegeName`** (D-9): las dos API de fase 2 lo llevan vacío y la puerta es el
  plugin. Por eso este rol no necesita ningún privilegio de ejecución.
- **No entra al column security profile** `CSP - MPPP - Datos sensibles` (decisión del aprobador,
  2026-09-25, `04` §4): el bot nunca lee `sanic_numeroidentificacion` ni `sanic_numerocuenta`
  directamente — recibe un JSON que arma el plugin con SYSTEM.
- Se parte de "App Opener", no de "Basic User" (`04` §2).

## 3. Precondiciones

Las de la receta (`seguridad/patrones.md` §2.1), con estos valores esperados:

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es managed, y su publisher coincide | los de la sección 1 |
| 2 | Hay exactamente una unidad de negocio raíz | una |
| 3 | El rol base existe una vez en la unidad raíz | `App Opener` |
| 4 | **No se pide ningún privilegio sobre tablas**: no hay nada que verificar acá | la sección 2 declara `"tablas": {}` |
| 5 | **No existe ya un rol con este nombre** en la unidad raíz | ninguno, o exactamente uno que cumpla la sección 5 |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, `references/seguridad/patrones.md` §2.1 "Security role".
Herramienta del proyecto: `herramientas/construir/rol.py`.

```
python3 herramientas/construir/rol.py playbooks/rol/sr_mppp_rpa.md
```

## 5. Verificación

```
python3 herramientas/construir/rol.py playbooks/rol/sr_mppp_rpa.md --solo-verificar
python3 herramientas/construir/muestra_rol.py playbooks/rol/sr_mppp_rpa.md --guardar
```

**La verificación central de este rol es NEGATIVA**: lo que importa no es lo que tiene, sino lo que
no tiene.

> **Ni un solo privilegio sobre ninguna tabla `sanic_mppp_tbl_*`** — ni leer, ni escribir, ni crear,
> ni borrar, en ningún alcance. Si aparece uno, es `difiere` y **hay que entender de dónde salió
> antes de seguir**: un bot que puede tocar tablas directo es exactamente lo que D-14 prohíbe, y un
> privilegio que nadie recuerda haber puesto es la forma en que estas cosas se aflojan.

Los únicos privilegios admitidos son los que trae `App Opener` en el entorno al momento de
construir.

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET roles` por nombre en la unidad raíz | un rol · `ismanaged = false` · **`name` exactamente `SR - MPPP - RPA`** · la descripción de la sección 2 |
| 2 | `RetrieveRolePrivilegesRole` | **exactamente** los privilegios de `App Opener`; ninguno sobre tablas de MPPP |
| 3 | Pertenencia a la solución | el rol figura una vez en la solución de la sección 1 (`solutioncomponents`, tipo 20) |
| 4 | Segunda ejecución sin `--solo-verificar` | estado `ya_existia`, código de salida 0 |
| 5 | Comprobación independiente contra el XML exportado (`muestra_rol.py`) | `OK`, y la muestra queda en `playbooks/rol/muestras/` |
| 6 | **El código lo reconoce** | `DeterminarRoles` (`StepsDeFila`) resuelve este rol por `name`; se verifica con la prueba de aceptación de la puerta de D-9, no a ojo |

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5. Si todas pasan: `ya_existia`. Si alguna no:
`difiere`, con la diferencia exacta, **sin tocar nada**. La herramienta nunca quita un privilegio ni
cambia un alcance. Si lo único que pasa es que **faltan** privilegios, se completa con
`--completar`; eso lo decide quien dirige la construcción, no el constructor.

## 7. Reversa

`DELETE roles(<roleid>)`. No es posible mientras el rol esté asignado a un usuario o a un equipo.

**El nombre es reversible en la plataforma pero NO en la práctica**: mientras el rol se llame
distinto de `SR - MPPP - RPA`, las dos Custom API rechazan toda llamada del bot con
`MPPP-RPA-006`, y el step de transición no reconoce al RPA como actor. Renombrarlo apaga el bot.

## 8. Fuera de alcance

- **Crear el usuario de aplicación del RPA en Entra y en el entorno, y asignarle este rol**: tarea
  de administración 13.5. Este playbook crea el rol, no la identidad.
- El registro de las dos Custom API de fase 2: inventario 8, y depende de este rol, no al revés.
- Las pruebas de aceptación de la puerta de D-9 (llamador sin `applicationid`, app user sin el rol,
  rol inexistente en el entorno): van con el código del plugin, no con este playbook.
- `rpa.puedeaprobar` y los demás parámetros de fase 2: inventario 9.1.
