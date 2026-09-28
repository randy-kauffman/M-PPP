# Fase 2 — CERRADA

Última actualización: **2026-09-28**. Rama `cierre/fase-1`.

> **Decisión de este documento, y por qué (corrección del 2026-09-28).** Este archivo se escribió el
> 2026-09-25 como una lista de "qué falta" — en ese momento, real. Fase 2 se construyó entera después,
> se probó con el set automatizado **B** (`pruebas/09-rpa-fase2.md`, casos B-01 a B-20, contra la
> plataforma), y se le repararon dos defectos reales encontrados por esas pruebas (D-60, D-61,
> `PENDIENTES.md`). Una lista de pendientes que ya no lo son es peor que no tener el documento: el
> próximo que la lea puede creer que falta trabajo hecho, o peor, rehacerlo. Se decidió **convertir
> este archivo en un cierre**, no borrarlo ni archivarlo aparte: otros documentos lo citan por número de
> fila (`pruebas/06-seguridad-vistas-formularios.md` cita "6.7"; `pruebas/09-rpa-fase2.md` cita las 8
> pruebas de `PuertaDelRpaAceptacion`; `pruebas/04-comunicaciones.md` lo cita para descartar que fase 2
> le agregue alcance a `MPPP-COM`/`MPPP-ENV`), así que romper la ruta o vaciar el contenido rompería esas
> citas. Las tablas "Construido y verificado" y "Dominio construido con TDD" de abajo **no cambiaron**
> — se re-verificaron hoy y siguen siendo ciertas. Lo que cambió es la sección de qué faltaba: se
> reemplaza por el cierre. **El contrato vivo de las dos Custom API sigue viviendo en
> `03-contratos-custom-api.md` §2 y §3** (no acá) y **la evidencia de prueba caso por caso vive en
> `pruebas/09-rpa-fase2.md`** (no acá) — este documento es el cierre y el punto de entrada, no la fuente
> de la verdad de ninguno de los dos.

## Decisiones: ninguna abierta

D-9, D-43, D-44 cerradas. C-03 no era nuestra (dependencia externa ya resuelta en la definición).
D-53 a D-56 — **las cuatro CERRADAS el 2026-09-28** (`PENDIENTES.md`; el 2026-09-25 estaban
"anotadas, ninguna frena": ya no son un riesgo anotado, son historia cerrada con commit). D-60 y D-61
— defectos reales encontrados por el set B el 2026-09-27 y **cerrados el 2026-09-28**, ver más abajo.

## Construido y verificado contra la plataforma

| # | Qué | Cómo se verificó |
|---|---|---|
| 6.7 | Rol `SR - MPPP - RPA` | 81 privilegios, **0 sobre tablas MPPP** |
| 13.5 | App user `# MPP-RPA` + rol | `herramientas/pruebas/como_rpa.py` — se autentica y pasa la puerta de D-9 **desde afuera** |
| 3.8 | `sanic_claimhasta`, `sanic_requiereatencionmanual` | DateTime y Boolean, ninguna protegida |
| 9.1 | `rpa.codigos`, `rpa.horas.tolerancia`, `rpa.minutos.claimmaximo`, `rpa.lote.maximo` | 17 parámetros en el entorno |

## Dominio construido con TDD (código, no configuración)

| Clase | Qué resuelve | Pruebas |
|---|---|---|
| `Steps/PuertaDelRpa` | Las dos condiciones de D-9 + el rol no instalado (D-44) | 8, **7 de ellas de rechazo** |
| `Dominio/ReglasDelRpa` | Claim, fechas, tamaño de lote, y los fallbacks | 18 |
| `Dominio/SeleccionParaElRpa` | Qué fila entra en el lote | 16 |
| `Dominio/FilaParaElRpa` (`ArmadorJsonDelRpa`) | El JSON que recibe el bot | 12 |

## Cierre: lo que faltaba el 2026-09-25, y cómo se resolvió

Los seis puntos de abajo eran la lista de pendientes original. Los seis están construidos, registrados
y probados; se listan con dónde vive cada uno para no tener que rehacer la búsqueda.

1. **Plugin de `sanic_mppp_capi_obtenerfilaspendientes`.** Construido: `src/Sanic.Mppp.Plugins/Api/ObtenerFilasPendientes.cs`
   y `Api/ObtenerFilasPendientesApi.cs`. Orquesta FIFO por `sanic_fechavalidada` + `sanic_numerofila`,
   filtra con `SeleccionParaElRpa.EntraEnElLote`, recorta con `maximo`, pone/refresca `sanic_claimhasta`,
   serializa con `ArmadorJsonDelRpa`, corre como SYSTEM, primera línea `PuertaDelRpa.Exigir` — todo tal
   como contrataba `03-contratos-custom-api.md` §2. Probado por el set B: B-04 (FIFO estricto), B-05 y
   B-06 (`maximo`/`claimminutos`), B-07 a B-10 (claim), B-11 (`rpa.puedeaprobar`). **Defecto encontrado
   y cerrado en el camino**: D-61 — el contrato pedía "ausente → 100" y "0 o negativo → error" a la vez,
   imposible en una Function (Dataverse manda `0` cuando el llamador omite la clave); el aprobador
   decidió que `0` significa el defecto. Corregidos el contrato (`03` §2) y el caso B-06.
2. **Plugin de `sanic_mppp_capi_registrarresultadorpa`.** Construido: `Api/RegistrarResultadoRpa.cs` y
   `Api/RegistrarResultadoRpaApi.cs`. Lote de 1 a `rpa.lote.maximo`, resultado por ítem y HTTP 200
   siempre, `ExigirFechaCreible`, traduce `codigo` con `rpa.codigos`, corre en transacción. Probado por
   el set B: B-13 (tolerancia de fecha), B-14 (catálogo 001/002/004/005), B-15 (lote grande rechazado
   entero), B-16 (lote vacío rechazado), B-17 (falla parcial no arrastra a los buenos), B-18
   (idempotencia). **Defecto encontrado y cerrado en el camino**: D-60 — un rechazo (`RechazadaEnAS400`)
   sin paso previo por Digitada leía `sanic_digitadapor` como proxy de identidad, columna NULL en ese
   camino, y la transacción se rechazaba SIEMPRE aunque el bot ya hubiera pasado la puerta de D-9.
   Arreglado en `Steps/StepsDeFila.cs` (`ResolverIdentidadDelActor`); el mismo fix resolvió B-17
   (la excepción escapaba sin atrapar y tumbaba el lote entero).
3. **Registro de las dos Custom API.** `diseno/06-inventario-componentes.md` filas 8.3 y 8.4, con
   `ExecutePrivilegeName` vacío (D-9) e `IsFunction` solo en `obtenerfilaspendientes`.
4. **El step de transición respeta el claim.** `Steps/StepsDeFila.cs`. Mientras `sanic_claimhasta` esté
   vigente, una persona no puede transicionar la fila; el bot sí puede al reportar (el claim no lo frena
   a él — `03` §2). Probado: B-07 (frena a una persona), B-08 (no frena al bot), B-09 (no excluye).
5. **La marca de atención manual se limpia con cualquier transición exitosa.** Confirmado en
   `05-app-model-driven.md` §3 ("la marca además se apaga sola con cualquier transición exitosa de la
   fila"). Probado: B-20.
6. **App (`05`): vistas y comando.** `05-app-model-driven.md` §2-3: vistas **Para atención manual** y
   **Tomadas por el bot**, comando **Atendida**, columna del claim (`Tomada por el bot hasta`) agregada
   a la grilla de Filas. Probado: B-19 (marca + cierra la puerta al bot), B-20 (se apaga sola).

## Cómo se siguió trabajando (histórico, vale para la próxima fase)

Las pruebas y las firmas las escribe quien dirige; el constructor implementa contra ellas y **no toca
el archivo de pruebas**. Si ve algo que ninguna prueba cubre, **lo reporta y no lo implementa**. Ese
freno encontró cuatro defectos reales el 2026-09-25 —un `catch` que escondía bugs detrás de un mensaje
de seguridad, un tope que apagaba el claim en silencio, una rama sin autor, y un campo nulo sin
contrato— y **ninguno lo encontró una prueba corriendo**. El mismo patrón (una prueba que ejercita el
control real, no un doble que lo simula bien) encontró D-60 el 2026-09-28: las pruebas viejas nunca
corrían el step real con `Depth=2` (código de servidor anidado), que es exactamente donde vivía el bug.
