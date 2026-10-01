# 06. Inventario de componentes

Proyecto: 2026-001-referencias-planes-pago · Etapa 3 · Estado: **borrador**. Completo. La app y los flujos ya están diseñados (`05-app-model-driven.md`, `07-flujos.md`), **en borrador y pendientes de la revisión del aprobador**; su parte de este inventario puede cambiar con esa revisión.

Todo lo que hay que crear, en el orden en que se puede crear. Cada componente se construye a partir de un **playbook**: una especificación en Markdown, cerrada y sin ambigüedad, que ejecuta un agente constructor y audita un agente revisor. La columna **Tipo de playbook** agrupa los componentes que comparten formato de especificación y receta de construcción: son **14 tipos**, y esa es la medida real del trabajo de escribir las skills.

Mecanismo de creación: **Web API de Dataverse**, con una herramienta versionada por tipo de playbook (`herramientas/construir/`). Esas herramientas construyen **solo en Dev**; a Test y a Prod llega la solución managed (BP-PP-121, BP-PP-125), no una re-ejecución. La excepción son los datos semilla, que se cargan en cada entorno. Estado: `hecho` · `pendiente` · `bloqueado por …`.

## Resumen

| Tipo de playbook | Cantidad fase 1 | Fase 2 | Fase 3 | Irreversible |
|---|---|---|---|---|
| P-01 Solución | 1 (`hecho`) | | | nombre |
| P-02 Choice global | 14 | | 1 | nombre; los valores no se renumeran |
| P-03 Tabla con sus columnas | 10 tablas, 84 columnas propias (sin lookups: esos nacen con las relaciones) | **+2 columnas en Fila**: `sanic_claimhasta`, `sanic_requiereatencionmanual` | 1 tabla | nombre, propiedad, tipo de cada columna |
| P-04 Relación | 11 | | | nombre, tipo |
| P-05 Clave alternativa | 10 | | 1 | nombre |
| P-06 Column security profile | 1 (2 columnas) | | | |
| P-07 Security role | 5 | 1 | 1 | |
| P-08 Código de plugins (C#, TDD) | 1 paquete, ~10 clases | +2 clases | | namespace y nombre de clase |
| P-09 Registro de Custom API y steps | 2 API (11 parámetros), **20** steps | **3 API** (`obtenerfilaspendientes`, `registrarresultadorpa`, y `exportarparadigitar` — DN-02, agregada el 2026-09-28 a este inventario) | | nombre de API y de parámetros |
| P-10 Datos semilla | 13 parámetros, 15 reglas | **4 parámetros**: `rpa.codigos`, `rpa.horas.tolerancia`, `rpa.minutos.claimmaximo`, `rpa.lote.maximo` (`rpa.puedeaprobar` ya se siembra en fase 1) | 2 parámetros | |
| P-11 Environment variable y connection reference | 2 + 2 | | +2 | nombre |
| P-12 Cloud flow | 5 (3 con disparador, 2 hijos) | | | |
| P-13 App model-driven: app, sitemap, **17** vistas, **8** formularios, 8 comandos (clásicos, por `ImportSolution`), **22** íconos | ver §12 | **+2 vistas** (*Para atención manual*, *Tomadas por el bot*), **+1 comando** (*Atendida*), la columna del claim en la grilla de Filas, y **+3 vistas y +1 formulario** (2026-09-27: *Correos autorizados sobre este cliente* DN-01; y los tres pedidos textuales del aprobador — *Clientes que puede modificar*, *Autorizaciones por cliente activas* y el formulario `Autorizacion por cliente` —, quedaron **20** vistas y **9** formularios), **+2 entradas de sitemap, el mismo día 2026-09-27** (`subarea_paraatencionmanual`, hallazgo del aprobador, §12.7; y `subarea_autorizacionescliente`, DN-01, la entrada "Autorizaciones - Clientes"): **15** entradas de sitemap en total (verificado contra Dev el 2026-09-28), corregido el 2026-09-28 — antes esta fila no traía el número. Y **+1 comando y +1 ícono** (*Descargar para digitar*, DN-02, 2026-09-26: **10** comandos y **24** íconos en total, corregido el 2026-09-28 — §12.1, §12.5) | página custom de la bandeja (v1.1) | |
| P-15 Página custom (canvas, **no la construye un agente solo**) | 1: diálogo de motivo | | | |
| P-14 Identidades y puesta en marcha | cuenta de servicio, buzón | usuario de aplicación del RPA | usuario de aplicación e infraestructura Azure | |

## 1. Base

| # | Componente | Nombre | Tipo | Depende de | Estado |
|---|---|---|---|---|---|
| 1.1 | Solución | `sanic_mppp_sol_mantenimientoppp` | P-01 | publisher `Sistemas_Abiertos_Nicaragua` | **hecho** (2026-09-20) |

## 2. Choices globales — P-02

Sin dependencias entre sí; van primero porque las columnas los usan. Valores = `15946` + correlativo de 4 dígitos (`02` §1).

| # | Nombre lógico | Opciones | Fase |
|---|---|---|---|
| 2.1 | `sanic_mppp_ch_moneda` | 2 | 1 |
| 2.2 | `sanic_mppp_ch_gestion` | 3 | 1 |
| 2.3 | `sanic_mppp_ch_clasificacion` | 3 | 1 |
| 2.4 | `sanic_mppp_ch_tipoformatoplan` | 3 | 1 |
| 2.5 | `sanic_mppp_ch_tipoidentificacion` | 5 | 1 |
| 2.6 | `sanic_mppp_ch_banco` | 8 | 1 |
| 2.7 | `sanic_mppp_ch_estadosolicitud` | 9 | 1 |
| 2.8 | `sanic_mppp_ch_estadofila` | 7 | 1 |
| 2.9 | `sanic_mppp_ch_efectoregla` | 3 | 1 |
| 2.10 | `sanic_mppp_ch_nivelregla` | 3 | 1 |
| 2.11 | `sanic_mppp_ch_resultadoregla` | 3 | 1 |
| 2.12 | `sanic_mppp_ch_origenevento` | 8 | 1 (+2 opciones en fases 2 y 3) |
| 2.13 | `sanic_mppp_ch_eventobitacora` | 20 | 1 |
| 2.14 | `sanic_mppp_ch_tipoparametro` | 3 | 1 |
| 2.15 | estado de histórico | por definir | 3 |

## 3. Tablas con sus columnas — P-03

Todas user-owned. El orden respeta los lookups: una tabla se crea después de las que referencia. Las columnas lookup **no** se crean acá: nacen con la relación (sección 4).

| # | Tabla | Columnas propias | Archivo | Protegidas | Depende de | Fase |
|---|---|---|---|---|---|---|
| 3.1 | `sanic_mppp_tbl_cliente` | 3 | | | | 1 |
| 3.2 | `sanic_mppp_tbl_plan` | 4 | | | choices 2.1, 2.4 | 1 |
| 3.3 | `sanic_mppp_tbl_autorizado` | 1 | | | | 1 |
| 3.4 | `sanic_mppp_tbl_autorizacionplan` | 3 | 1 | | | 1 |
| 3.5 | `sanic_mppp_tbl_parametro` | 5 | | | choice 2.14 | 1 |
| 3.6 | `sanic_mppp_tbl_regla` | 7 | | | choices 2.9, 2.10 | 1 |
| 3.7 | `sanic_mppp_tbl_solicitud` | 29 | 2 | | choice 2.7 | 1 |
| 3.8 | `sanic_mppp_tbl_fila` | 18 (+2 en fase 2: `sanic_claimhasta`, `sanic_requiereatencionmanual`) | | 2 | choices 2.1–2.3, 2.5, 2.6, 2.8 | 1 |
| 3.9 | `sanic_mppp_tbl_resultadoregla` | 7 | | | choices 2.9, 2.11 | 1 |
| 3.10 | `sanic_mppp_tbl_bitacora` | 7 | | | choices 2.12, 2.13 | 1 |
| 3.11 | `sanic_mppp_tbl_corridahistorico` | por definir | | | | 3 |
| 3.12 | `sanic_mppp_tbl_motivoaccion` | 2 | | | | 1 |
| 3.13 | `sanic_mppp_tbl_autorizacioncliente` | 3 (`sanic_documentofirmado` y `sanic_fechadocumento` agregadas el 2026-09-27, decisión del aprobador) **+1 el 2026-10-01: `sanic_comentarios` T(50), DN-04, construida y verificada en Dev ese día** (muestra exportada `OK`) | 1 | | | 2 (DN-01, `DIRECTRICES-NUEVAS.md`) |

## 4. Relaciones — P-04

Cada una crea su columna lookup. `P` = parental (borrado en cascada), `R` = referencial con borrado restringido.

| # | Relación | Lookup que crea | Tipo |
|---|---|---|---|
| 4.1 | cliente → plan | `plan.sanic_clienteid` | R |
| 4.2 | ~~cliente → autorizado~~ | **ELIMINADA el 2026-09-24** | — |
| 4.3 | autorizado → autorizacionplan | `autorizacionplan.sanic_autorizadoid` | R |
| 4.4 | plan → autorizacionplan | `autorizacionplan.sanic_planid` | R |
| 4.5 | solicitud → fila | `fila.sanic_solicitudid` | **P** |
| 4.6 | solicitud → resultadoregla | `resultadoregla.sanic_solicitudid` | **P** |
| 4.7 | solicitud → bitacora | `bitacora.sanic_solicitudid` | **P** |
| 4.8 | plan → fila | `fila.sanic_planid` | R |
| 4.9 | regla → resultadoregla | `resultadoregla.sanic_reglaid` | R |
| 4.10 | systemuser → fila | `fila.sanic_digitadapor` | R |
| 4.11 | systemuser → fila | `fila.sanic_aprobadapor` | R |
| 4.12 | autorizado → autorizacioncliente | `autorizacioncliente.sanic_autorizadoid` | R (fase 2, DN-01) |
| 4.13 | cliente → autorizacioncliente | `autorizacioncliente.sanic_clienteid` | R (fase 2, DN-01) |

## 5. Claves alternativas — P-05

Después de las relaciones, porque varias incluyen un lookup. La activación del índice es asíncrona: el playbook tiene que esperar y verificar.

| # | Clave | Columnas |
|---|---|---|
| 5.1 | `sanic_mppp_key_cliente_cifbac` | cifbac |
| 5.2 | `sanic_mppp_key_cliente_cifcom` | cifcom |
| 5.3 | `sanic_mppp_key_plan_codigo` | codigo |
| 5.4 | `sanic_mppp_key_autorizado_correo` | nombre (el correo) — **solo, desde el 2026-09-24** |
| 5.5 | `sanic_mppp_key_autorizacionplan_autorizado_plan` | autorizadoid + planid |
| 5.6 | `sanic_mppp_key_parametro_nombre_version` | nombre + version |
| 5.7 | `sanic_mppp_key_regla_codigo` | codigo |
| 5.8 | `sanic_mppp_key_solicitud_messageid` | messageid |
| 5.9 | `sanic_mppp_key_fila_solicitud_numerofila` | solicitudid + numerofila |
| 5.10 | `sanic_mppp_key_resultadoregla_solicitud_reglacodigo` | solicitudid + reglacodigo |
| 5.11 | `sanic_mppp_key_autorizacioncliente_autorizado_cliente` | autorizadoid + clienteid (fase 2, DN-01) |

## 6. Seguridad — P-06 y P-07

| # | Componente | Nombre | Depende de | Fase |
|---|---|---|---|---|
| 6.1 | Column security profile | `CSP - MPPP - Datos sensibles` (`fila.sanic_numeroidentificacion`, `fila.sanic_numerocuenta`) | tabla 3.8 | 1 |
| 6.2 | Security role | `SR - MPPP - Ejecutivo` | todas las tablas | 1 |
| 6.3 | Security role | `SR - MPPP - Supervisor` | ídem | 1 |
| 6.4 | Security role | `SR - MPPP - Administrador de planes` | ídem | 1 |
| 6.5 | Security role | `SR - MPPP - Administrador tecnico` | ídem | 1 |
| 6.6 | Security role | `SR - MPPP - Servicio de ingesta` | ídem. Las Custom API 8.0 y 8.1 se ejecutan con un privilegio que este rol ya tiene (D-9): el rol no depende de ellas, ellas dependen del rol | 1 |
| 6.7 | Security role | `SR - MPPP - RPA` | Custom API de fase 2 | 2 |
| 6.8 | Security role | `SR - MPPP - Historico` | | 3 |

## 7. Código de plugins — P-08 (C#, TDD estricto, sin FakeXrmEasy)

Un solo paquete, `sanic_mppp_pkg_plugins` (`Sanic.Mppp.Plugins`). Se construye por capas, de adentro hacia afuera (`03` §8); las cuatro primeras no tocan Dataverse.

| # | Pieza | Qué hace | Fase |
|---|---|---|---|
| 7.1 | `Dominio/` | Estados, transiciones permitidas, actor | 1 |
| 7.2 | `Plantilla/` | Lector de la plantilla, reglas LP-01 a LP-08 | 1 |
| 7.3 | `Validacion/` | Motor de reglas con orden y dependencias; 5 reglas de solicitud y 8 de registro (eran 6 de solicitud hasta que E-16 movió `REMITENTE_RECONOCIDO` al nivel Correo, que vive en 7.6b); el motor es uno solo y genérico, y lo reutiliza el nivel Correo | 1 |
| 7.4 | `Respuesta/` | Arma el acuse y la respuesta final, con enmascarado | 1 |
| 7.5 | Doble de prueba `OrganizationServiceEnMemoria` | En el proyecto de tests | 1 |
| 7.6 | `Datos/` | Repositorios sobre `IOrganizationService` | 1 |
| 7.6b | `Correo/` y `Api/ClasificarCorreoApi` | Lector de cabeceras del `.eml` (entrada no confiable) y el **plugin liviano** que decide si el correo se procesa | 1 |
| 7.7 | `Api/ValidarSolicitudApi` | Plugin de la Custom API de validación | 1 |
| 7.8 | `Steps/` lista blanca de columnas (Fila y Solicitud) | | 1 |
| 7.9 | `Steps/` transición de Fila (Pre) y post-transición (Post) | | 1 |
| 7.10 | `Steps/` normalizar y validar (Cliente, Plan, Autorizado, Parametro, Regla) | En Regla: impide desactivar una regla de la que dependen otras activas (D-13); impide que `AUTORIZACION_CORREO_PLAN` tenga otro efecto que Rechaza (D-14); impide el efecto Envía a revisión fuera de las reglas del sobre —niveles Correo y Solicitud—, nunca en Registro (D-20) | 1 |
| 7.11 | `Steps/` nombre calculado (Plan, AutorizacionPlan) e integridad de AutorizacionPlan | | 1 |
| 7.12 | `Steps/` atender no reconocida | | 1 |
| 7.13 | `Api/ObtenerFilasPendientesApi`, `Api/RegistrarResultadoRpaApi` | | 2 |
| 7.14 | `Api/ExportarParaDigitar`, `Api/ExportarParaDigitarApi`, `Exportacion/` (`ArmadorExportacionOpenXml`, Open XML SDK 3.1.0) | **Agregado el 2026-09-28 a este inventario** (DN-02, construido el 2026-09-26/27) | 2 |
| 7.15 | `Api/CargarCatalogos(Api)`, `Api/CambiarEstadoCatalogos(Api)`, y el dominio de la lectura y validación de los dos Excel de catálogos | **Construido el 2026-10-01, DN-04** (paquete 1.0.20.0). Reutiliza la normalización de `Steps/ReglasDeSteps.cs` y las reglas LP-01 a LP-07. D-72 cerrada el 2026-10-01 | DN-04 |

## 8. Registro de Custom API y steps — P-09

| # | Componente | Nombre | Depende de | Fase |
|---|---|---|---|---|
| 8.0 | Custom API | `sanic_mppp_capi_clasificarcorreo` (1 parámetro de entrada, 3 de salida) | 7.6b | 1 |
| 8.1 | Custom API | `sanic_mppp_capi_validarsolicitud` (1 parámetro de entrada, 6 de salida) | 7.7 | 1 |
| 8.2 | Steps | Uno por cada mensaje y tabla de 7.8 a 7.12: **18**, con su etapa, orden, atributos de filtro e imágenes. Lista cerrada en `03-contratos-custom-api.md` §5.1 (antes decía "~17"; la expansión real contra el código da 18, y sube a 20 si Dev admite registrar sobre `Upsert`) | paquete registrado | 1 |
| 8.3 | Custom API | `sanic_mppp_capi_obtenerfilaspendientes` | 7.13 | 2 |
| 8.4 | Custom API | `sanic_mppp_capi_registrarresultadorpa` | 7.13 | 2 |
| 8.5 | Custom API | `sanic_mppp_capi_exportarparadigitar` (1 parámetro de entrada, 2 de salida) — **agregada el 2026-09-28 a este inventario; construida en Dev el 2026-09-26/27** (DN-02, `DIRECTRICES-NUEVAS.md`, contrato completo en `03-contratos-custom-api.md` §1b) | 7.14 | 2 |
| 8.6 | Custom API | `sanic_mppp_capi_cargarcatalogos` (2 parámetros de entrada, 9 de salida) — **construida en Dev el 2026-10-01**, DN-04, contrato en `03` §1c | 7.15 | DN-04 |
| 8.7 | Custom API | `sanic_mppp_capi_cambiarestadocatalogos` (2 parámetros de entrada, 8 de salida) — **construida en Dev el 2026-10-01**, DN-04, contrato en `03` §1d | 7.15 | DN-04 |

## 9. Datos semilla — P-10

| # | Qué | Cantidad | Depende de |
|---|---|---|---|
| 9.1 | Parámetros: `correo.prefijos.reenvio`, `clasificacion.dias.vencimiento`, `plantilla.estructura`, `plantilla.listas`, `plantilla.obligatoriedad`, `lectura.limites`, `retencion.dias.*` (2), `vigilancia.*` (3), `rpa.puedeaprobar` | 12 | tabla 3.5 |
| 9.2 | Reglas de nivel Correo (2) y de nivel Solicitud (5) | 7 | tabla 3.6 |
| 9.3 | Reglas de nivel Registro | 8 | tabla 3.6 |
| 9.5 | Parámetro `carga.filas.maximo` = 300 (DN-04, sembrado en Dev el 2026-10-01, versión 1) | 1 | tabla 3.5 |
| 9.4 | Migración única desde SharePoint: clientes, planes, autorizados, autorizaciones (RF-16) | datos reales | tablas 3.1–3.4, y los datos revisados por Banca Privada |

## 10. Configuración entre entornos — P-11

| # | Componente | Nombre | Fase |
|---|---|---|---|
| 10.1 | Environment variable | `sanic_mppp_ev_buzoningesta` | 1 |
| 10.2 | Environment variable | `sanic_mppp_ev_carpetaprocesados` | 1 |
| 10.3 | Connection reference | `sanic_mppp_conr_outlook` | 1 |
| 10.4 | Connection reference | `sanic_mppp_conr_dataverse` | 1 |
| 10.5 | Environment variables del almacenamiento del histórico | por definir | 3 |

## 11. Flujos — P-12 (`07-flujos.md`)

| # | Flujo | Disparador | Depende de |
|---|---|---|---|
| 11.1 | `Cloud Flow - MPPP - ING - Ingerir y validar correo` | hijo | tabla 3.7, Custom API 8.0 y 8.1, 10.1–10.4 |
| 11.2 | `Cloud Flow - MPPP - REC - Recibir correo nuevo` | correo nuevo en el buzón | 11.1 |
| 11.3 | `Cloud Flow - MPPP - ENV - Enviar comunicación al cliente` | hijo | tabla 3.7 |
| 11.4 | `Cloud Flow - MPPP - COM - Detectar comunicación pendiente` | cambio de estado de la Solicitud | 11.3 |
| 11.5 | `Cloud Flow - MPPP - VIG - Vigilar pendientes` | cada 10 minutos | 11.1, 11.3, parámetros 9.1 |

Desaparece el Flow B de la definición (`07` DF-01). Los hijos se construyen antes que quienes los llaman.

## 12. App model-driven — P-13 y P-15 (`05-app-model-driven.md`)

| # | Componente | Cantidad | Depende de |
|---|---|---|---|
| 12.1 | Íconos SVG (web resources) | **24** | **Lista cerrada el 2026-09-22** (antes "~10", una estimación); **recontada el 2026-09-28** contra `recursos/svg/` (antes decía "22"). 13 para las entradas del sitemap (incluida Devueltas, agregada el 2026-09-22; dos entradas de fase 2 — *Para atención manual* y *Autorizaciones - Clientes* — reusan un ícono existente en vez de sumar uno nuevo), **10 para los comandos** (8 al cerrar la lista + *Atendida*, fase 2, + *Descargar para digitar*, DN-02, 2026-09-26: `cmd_atendida.svg`, `cmd_descargarparadigitar.svg`) y 1 para la app. Los de comandos no son adorno: son botones de transición que aparecen juntos sobre datos de pago de un banco, y equivocarse de botón es irreversible. Dibujados a mano en `recursos/svg/`, monocromos con `currentColor`, sin depender de descargas ni de rutas internas de Microsoft. **+2 diseñados el 2026-10-01, DN-04**: uno por cada comando de catálogos (los 8 botones comparten esos 2 íconos). Total al construirlos: **26** |
| 12.2 | Vistas | **20** | tablas y relaciones. Incluye las dos de AutorizacionPlan para subgrillas: "correos autorizados sobre un plan" y "planes que puede modificar un correo", más "Autorizaciones sin evidencia" (`05` §formularios) **Lista cerrada el 2026-09-22 en `05` §vistas** (antes "~19", un numero que no se podia reconstruir): cada catalogo lleva UNA vista "Activos"; la de busqueda rapida no se cuenta. **+1 el 2026-09-27** (DN-01, orden del aprobador): "Correos autorizados sobre este cliente", subgrilla de Cliente sobre `AutorizacionCliente`, que se había creado (DN-01, 3.13) y nunca se mostró en ningún formulario. **+2 más, ese mismo día** (los tres pedidos textuales del aprobador): "Clientes que puede modificar" (subgrilla de Autorizado sobre `AutorizacionCliente`) y "Autorizaciones por cliente activas" (por defecto de `AutorizacionCliente`, abre la entrada nueva del sitemap "Autorizaciones - Clientes"). Total: 18 + 2 = **20**. **Sin cambio de cantidad, pero sí de contenido, el 2026-09-27 (hallazgo del aprobador):** se agregó la columna `sanic_requiereatencionmanual` a cuatro vistas de trabajo ya existentes de Fila — "Mis clientes — por digitar", "Todos — por digitar", "Por aprobar" y "Mis clientes — devueltas" (`05` §"Fase 2", nota de corrección) —, para que el ejecutivo o el supervisor vean de un vistazo si el bot marcó la fila. No se agregó a Filas, Terminadas ni Tomadas por el bot (no son vistas de trabajo) |
| 12.3 | Formularios principales | **9** | tablas, vistas (subgrillas). **+1 el 2026-09-27**: `Autorizacion por cliente` (`AutorizacionCliente`), pedido textual del aprobador — antes solo tenía el formulario "Information" de fábrica sin lookups |
| 12.4 | Formulario de creación rápida de `sanic_mppp_tbl_motivoaccion` (el diálogo de motivo) | 1 | tabla 3.12 · **DA-03 corregida el 2026-09-22: ya no es una página custom.** Una página custom abierta como diálogo no devuelve ningún valor al comando y no admite recibir varias filas, así que era incompatible con la acción masiva de DA-02. Se construye por API como cualquier otro formulario (`formulario.py`, `type` 7) |
| 12.5 | Comandos propios | **10** | 12.4, tabla 3.12, plugin de transición 7.9, 12.1 (íconos). **Corregido el 2026-09-28** (decía "8", contado al cerrar fase 1: *Digitada*, *Aprobar*, *Devolver*, *Rechazada en AS400*, *Anular* en Fila + *Atendido*, *Descartar*, *Revisado* en Solicitud). Fase 2 sumó **2 más**, sin actualizar esta fila: *Atendida* (Fila, botón que apaga `sanic_requiereatencionmanual`) y **Descargar para digitar** (Fila, DN-02, 2026-09-26, `sanic_mppp_capi_exportarparadigitar`). Verificado contra `playbooks/importacion/comandos.md`: 10 bloques `"id"` en total. **Clásicos con web resource JavaScript** (DA-02 corregida el 2026-09-22): un comando moderno no se puede crear por API. Se construyen editando el `RibbonDiffXml` del `customizations.xml` e importando con la acción `ImportSolution`. **+2 diseñados el 2026-10-01, DN-04, sin construir**: *Carga masiva* y *Activar/Desactivar masivo*, cada uno en la grilla principal de **4 tablas** (Cliente, Plan, Autorizado, AutorizacionCliente). Son **2 comandos y 8 botones**, cada botón un **menú desplegable** (`FlyoutAnchor`, el primero del proyecto) con *Cargar archivo* y *Descargar plantilla*: cada tabla lleva su propio bloque en el `RibbonDiffXml`, y los dos comparten función de JS. Total al construirlos: **12** comandos |
| 12.6 | Ocultamiento de comandos genéricos | 10 tablas | Mismo mecanismo que 12.5 (`HideCustomAction` dentro del `RibbonDiffXml`). Son **6 comandos** (Eliminar, Asignar, Compartir, Flujo, Enviar vínculo, Combinar) en las 10 tablas, más "Nuevo" solo en Solicitud, Fila, ResultadoRegla y Bitácora (DA-06). El 10 cuenta TABLAS, no comandos |
| 12.7 | Sitemap | 1 | 12.1, 12.2. **Corregido el 2026-09-27 (hallazgo del aprobador):** el área Trabajo le faltaba la entrada `subarea_paraatencionmanual` (tabla Fila, vista "Para atencion manual") — un olvido de fase 2, no una decisión, porque su hermana conceptual del lado Solicitud, `subarea_pararevisar`, sí la tenía. Se agregó entre `subarea_poraprobar` y `subarea_porclasificar`, reusando el ícono `sanic_mppp_wr_svg_pararevisar` (`05` §"Fase 2", nota de corrección). **El mismo día se agregó `subarea_autorizacionescliente`** ("Autorizaciones - Clientes", DN-01, §12.2). **Total, verificado contra Dev el 2026-09-28** (`sitemaps` → `sitemapxml`, conteo de nodos `SubArea`): **15 entradas**, número que esta fila no traía hasta esta auditoría |
| 12.8 | App `sanic_mppp_mda_mantenimientoppp` | 1 | 12.2, 12.3, 12.7, roles 6.2–6.5 |
| 12.9 | Página custom "bandeja de trabajo" | 1 | **versión 1.1**, fuera de la fase 1 |

## 13. Identidades y puesta en marcha — P-14

| # | Qué | Quién | Fase |
|---|---|---|---|
| 13.1 | Buzón dedicado, con carpeta de procesados | Administrador de Exchange | 1 |
| 13.2 | Cuenta de servicio licenciada, con su rol 6.6 y acceso exclusivo al buzón (C-04) | Administrador de Power Platform | 1 |
| 13.3 | Asignación de roles a ejecutivos, supervisores y administradores | Administrador de Power Platform | 1 |
| 13.4 | Auditoría sobre asignación de roles activada (D-38) | Administrador de Power Platform | 1 |
| 13.7 | Grupo de seguridad de Entra ID para quienes leen datos sensibles, su equipo de grupo en el entorno, y la asociación de ese equipo al perfil 6.1 (D-8, `04` §4). **Ver 13.7.1: la prueba del mecanismo es parte de la tarea, no un extra** | Administrador de Azure y de Power Platform | 1 |
| 13.5 | Usuario de aplicación del RPA | | 2 |
| 13.6 | Usuario de aplicación del histórico e infraestructura Azure | Administrador de Azure | 3 |

### 13.7.1 Prerrequisito de migración: ejercitar el mecanismo de D-8 — **HECHO EN DEV el 2026-09-26**

**El mecanismo existe y está verificado en Dev**: equipo `MPPP - Ejecutivos` (`teamtype = 2`, equipo de grupo de Entra ID) asociado al perfil `CSP - MPPP - Datos sensibles`. Se comprueba así, **desde el lado del EQUIPO**:

```
GET teams({teamid})/teamprofiles_association?$select=name
  -> ['CSP - MPPP - Datos sensibles']
```

> **Ojo con cómo se verifica, porque miente en silencio.** La MISMA navegación `teamprofiles_association` consultada **desde el lado del PERFIL** devuelve **200 con lista vacía**, que se lee igual que "no hay ningún equipo". El 2026-09-26 esa lectura llevó a afirmar que D-8 no estaba hecho cuando sí lo estaba. **Una consulta que devuelve vacío se verifica desde el otro lado antes de convertirla en una afirmación.** Es la misma familia que el `$filter` que devolvía 200 vacío sobre la app (2026-09-23).

Lo que queda para cada entorno nuevo es **ejecutar la cadena**, no diseñarla:

En **Desarrollo, 13.7 no hace falta**: nadie necesita leer `sanic_numeroidentificacion` ni
`sanic_numerocuenta` ahí, y durante la fase 1 se trabajó con `User1` y `User2` asignados **directo**
al perfil, que es lo correcto para un entorno de pruebas (decisión del aprobador, 2026-09-23 y
2026-09-25). Donde **sí** hace falta es en Producción.

El riesgo no es la asignación: es que **la cadena Entra → equipo de grupo → perfil nunca se ejecutó
ni una sola vez**, en ningún entorno. Si su estreno es el día de la migración, un fallo se descubre
ese día, con la gente esperando. Por eso la prueba de punta a punta es parte de 13.7:

1. Crear el grupo de seguridad en Entra ID y su equipo de grupo en el entorno.
2. Asociar ese equipo al perfil `CSP - MPPP - Datos sensibles`.
3. **Meter a una persona en el grupo de Entra** → entra a la app y **lee** el número de cuenta.
4. **Sacarla del grupo** → deja de leerlo.
5. Si en ese entorno quedaron usuarios asignados directo al perfil, **quitarlos**: mientras estén,
   tapan si el mecanismo real funciona o no.

Los pasos 3 y 4 son la tarea. Los dos primeros son clics. **En Dev los cuatro ya se hicieron**; en Test y Producción hay que repetirlos.

### 13.8 Instalación en un entorno nuevo — el procedimiento completo

**La solución lleva las TABLAS, no sus filas.** Instalarla y no hacer el resto deja un entorno que
importa sin un solo error y no funciona. Cada paso de abajo se agregó porque su ausencia rompió algo
real en el entorno de prueba el 2026-09-24; el §13 anterior no nombraba ninguno de los cuatro
primeros.

| Paso | Qué | Por qué está acá |
|---|---|---|
| 1 | **Verificar la capacidad de almacenamiento del entorno** antes de importar | Una importación —y sobre todo la publicación del paso 4— falla, a veces en silencio, cuando el entorno está lleno |
| 2 | Importar la solución (`herramientas/construir/importar_solucion.py`, o a mano el zip de `solution/`) | **Único camino real para que las tablas nuevas entren a la app model-driven.** Las TABLAS de una app no se pueden enganchar por Web API: `AddAppComponents` (con `@odata.type: Microsoft.Dynamics.CRM.entity`) devuelve HTTP **204** (éxito aparente) y **no agrega nada** — deja una referencia a la tabla de metadatos `entity`, ignorando el GUID que se le pasa; `RemoveAppComponents` tampoco borra, `DELETE appmodulecomponents(id)` responde que el método no existe para ese tipo, y `ValidateApp` pasa en verde con la app vacía. **Bug de plataforma, reproducido limpio el 2026-09-22** en una solución nueva, sin nada propio de esta app (`playbooks/importacion/mantenimientoppp.md`). El único camino real es exportar la solución, editar `customizations.xml` a mano y reimportarla — eso es exactamente lo que hace `importar_solucion.py` |
| 3 | **Desplegar el paquete de plugins ANTES de probar cualquier flujo o Custom API**: `dotnet pack src/Sanic.Mppp.Plugins.Paquete/Sanic.Mppp.Plugins.Paquete.csproj -c Release` y después `herramientas/construir/paquete_plugins.py` contra el entorno destino | Sin esto no existe ningún `plugintype`: los steps y las dos Custom API (8.0/8.1/8.2) no tienen a qué apuntar. **Ojo, D-57(b) (2026-09-25/26): si se empaqueta sobre un `obj/`/`bin/` ya construidos, MSBuild puede dar `GenerateNuspec` por "up-to-date" contra esa caché vieja y `dotnet pack` termina con éxito SIN regenerar el `.nupkg` de verdad — `dotnet clean` no lo arregla porque `Pack` no es parte de ese target.** Hay que **borrar `obj/` y `bin/` a mano** antes de empaquetar de nuevo. `paquete_plugins.py` verifica el contenido del `.dll` de adentro contra los tipos declarados y queda `bloqueado` si no coinciden, así que un `.nupkg` viejo no llega a subirse solo, pero el paso manual de borrar `obj/`/`bin/` sigue siendo necesario para que el `dotnet pack` de al lado produzca uno bueno |
| 4 | **Publicar todas las personalizaciones** (Soluciones → *Publicar todas las personalizaciones*, `PublishAllXml`) | **Obligatorio.** Learn: *"After you successfully import your solution, you must publish customizations before you can see the changes"*. Sin esto no se aplican **Application Ribbon**, **Form**, **Sitemap** ni **Web Resource**: no se ven formularios, vistas ni sitemap; los comandos propios (12.5) no aparecen, los 1364 `HideCustomAction` (12.6) no ocultan nada, los formularios fallan al abrir y el web resource HTML de 12.x da "Name not found". La publicación puede tardar; no cerrar la pestaña |
| 5 | **Regenerar la metadata del ribbon** desde el Command Checker (`&ribbondebug=true` en la URL de la app, botón **"Regenerate ribbon metadata"**) | Publicar (paso 4) **no alcanza** para los comandos custom: sin este paso **NO APARECE NINGÚN BOTÓN CUSTOM**, aunque esté definido y publicado en el servidor. Esto costó **dos días de diagnóstico el 2026-09-27** (`pruebas/13-construido-hoy.md`, caso H-06): el botón "Descargar para digitar" existía verificado por **tres caminos de API** (`RetrieveEntityRibbon`, los `ribbondiff` con sus `LocLabel` en 1033/3082, y el verificador del proyecto) y no se dibujaba, porque la causa era la metadata **COMPILADA** del ribbon, no una regla de habilitación. **Criterio de diagnóstico** (mismo síntoma superficial, causas opuestas): en el Command Checker, un botón marcado **`(hidden)`** es problema de **REGLAS** (`EnableRule`/`DisplayRule` — revisar contra la selección actual, la metadata está bien); un botón **AUSENTE de la lista entera** es problema de **METADATA COMPILADA** — ningún ajuste de reglas lo arregla, hay que regenerar |
| 6 | **Sembrar los catálogos**: `datos.py playbooks/datos/parametros.md` (13 parámetros, 9.1) y `datos.py playbooks/datos/reglas.md` (15 reglas, 9.2/9.3) | Son filas, no metadatos: no viajan en la solución. Sin las reglas no se clasifica ni se valida nada; sin los parámetros el motor no arranca. **El bloque de identidad de cada playbook trae `entorno_url` apuntando a Dev: hay que cambiarlo al entorno destino** |
| 7 | **Ajustar `servicio.cuenta` al UPN real del entorno** | El valor que viaja en el playbook es el de Dev. Ese parámetro es lo único que exime a la cuenta de servicio de la lista blanca de columnas (`04` §1, `BaseDeStep.EsCodigoDeServidor`): con el UPN equivocado, los cinco flujos son tratados como personas y no pueden escribir ni sus propias marcas de envío. **El rol 6.6 no alcanza: son dos puertas distintas** |
| 8 | Cargar los valores de las 2 environment variables y crear las 2 connections de las connection references (11) | Las **definiciones** viajan; los **valores** y las **conexiones**, no |
| 9 | Hacer las tareas 13.1–13.4 y 13.7 de la tabla de arriba | El grupo de Entra, su equipo y la asociación equipo ↔ perfil 6.1 nunca viajan |
| 10 | **Subir la retención de auditoría de la organización a 365 días** (`organization.auditretentionperiodv2`) | **No viaja con la solución**: no es un componente (no hay `customizations.xml` que lo lleve) y no vive en ningún playbook. Un entorno nuevo nace siempre en el valor por defecto de Dataverse (**30 días**) — si nadie repite este paso a mano, el entorno nuevo pierde la retención acordada (DD-21). Procedimiento exacto (lectura, `PATCH`, relectura de verificación) en `02-diccionario-datos.md` §2.5-bis |
| 11 | Cargar los catálogos de negocio (Cliente, Plan, Autorizado, AutorizacionPlan) | 9.4. Los playbooks `humo_*.md` son datos de prueba, **no** datos del cliente |

**Cómo se verifica que quedó bien**, en este orden: (a) abrir un formulario de cada tabla sin error;
(b) ver los comandos propios en la grilla de Filas y que los genéricos sigan ocultos; (c) correr
`herramientas/pruebas/verificar_accesos.py`; (d) mandar un correo de prueba al buzón y seguirlo hasta
que la Solicitud quede Procesada.

## Orden de construcción de la fase 1

`2` choices → `3` tablas → `4` relaciones → `5` claves → `6.1` perfil de columna → `7.1–7.5` código sin Dataverse (**puede ir en paralelo con todo lo anterior**) → `7.6–7.12` → registro del paquete → `8.1–8.2` → `6.2–6.6` roles → `9.1–9.3` semillas → `10` configuración → `11` flujos → `12` app → `9.4` migración → `13` puesta en marcha.
