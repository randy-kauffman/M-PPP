# Fase 2 — dónde quedó, y qué falta exactamente

Última actualización: **2026-09-25**. Rama `cierre/fase-1`. Suite: **858** pruebas de plugins y
**587** de herramientas, todas en verde.

## Decisiones: ninguna abierta

D-9, D-43, D-44 cerradas. C-03 no era nuestra (dependencia externa ya resuelta en la definición).
D-53 a D-56 anotadas en `PENDIENTES.md`; ninguna frena.

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

## Lo que FALTA, y es todo plomería

**Ninguna decisión de diseño pendiente**: todo está contratado en `03` §2 y §3.

1. **Plugin de `sanic_mppp_capi_obtenerfilaspendientes`**. Orquesta lo que ya existe:
   consulta **FIFO** por `sanic_fechavalidada` + `sanic_numerofila`, filtra con
   `SeleccionParaElRpa.EntraEnElLote`, recorta con `maximo` (tope 500), **pone o refresca
   `sanic_claimhasta`** con `ReglasDelRpa.MinutosDeClaim`, serializa con `ArmadorJsonDelRpa`, y
   devuelve `filas`, `entregadas` y `pendientes`. Todo **como SYSTEM** (`04` §1). Primera línea:
   `PuertaDelRpa.Exigir`.
2. **Plugin de `sanic_mppp_capi_registrarresultadorpa`**. Lote de 1 a `rpa.lote.maximo`
   (`ReglasDelRpa.ExigirLoteAceptable`), **resultado por ítem y HTTP 200** — nunca todo o nada —,
   `ExigirFechaCreible`, traducción del `codigo` con `rpa.codigos`, `mensaje` a
   `sanic_notainterna`. **El plugin no puede tirar excepción por un ítem**: corre en transacción.
3. **Registro de las dos Custom API** con sus parámetros, `ExecutePrivilegeName` **vacío**
   (D-9), e `IsFunction` solo en la primera.
4. **El step de transición tiene que respetar el claim**: mientras `sanic_claimhasta` esté
   vigente, una **persona** no puede transicionar esa fila. El claim **no** frena al bot cuando
   reporta (`03` §2, y el porqué está escrito ahí: divergencia con AS400).
5. **La marca de atención manual se limpia con cualquier transición exitosa** de la fila.
6. App (`05`): vistas *Para atención manual* y *Tomadas por el bot*, comando *Atendida*, y la
   columna del claim en la grilla de Filas.

## Cómo se sigue trabajando

Las pruebas y las firmas las escribe quien dirige; el constructor implementa contra ellas y
**no toca el archivo de pruebas**. Si ve algo que ninguna prueba cubre, **lo reporta y no lo
implementa**. Ese freno encontró cuatro defectos reales el 2026-09-25 —un `catch` que escondía
bugs detrás de un mensaje de seguridad, un tope que apagaba el claim en silencio, una rama sin
autor, y un campo nulo sin contrato— y **ninguno lo encontró una prueba corriendo**.
