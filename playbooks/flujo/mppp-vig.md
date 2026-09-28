# Playbook: flujo · MPPP-VIG

Inventario 11.5. Disenado en `07-flujos.md` seccion 6.

Es la red de seguridad de todo el sistema: hace lo mismo que `MPPP-REC` y `MPPP-COM` cuando algo
quedo a medias (DF-02). Por eso ninguno de esos dos tiene logica propia.

Los cinco bloques de la seccion 6, cada uno en su ambito y **encadenados con
`runAfter: [Succeeded, Failed]`**: que un bloque falle no puede dejar sin correr a los otros
cuatro. La mayoria de las ejecuciones termina en segundos sin nada que hacer.

Los umbrales salen de la tabla de Parametros, leidos al empezar cada ejecucion, con un valor de
respaldo por si el parametro no esta: 15 minutos sin validar, 30 sin responder, 3 reintentos,
30 dias de vencimiento.

## D-62 — blindaje de la etapa de arranque (corregido 2026-09-28)

La etapa de arranque (`Parametro_sin_validar -> Parametro_sin_responder -> Parametro_reintentos
-> Parametro_vencimiento -> Umbrales -> Correos_que_quedaron_en_la_bandeja`) quedo encadenada
**tambien con `runAfter: [Succeeded, Failed]`**, igual que los cinco bloques entre si. Antes solo
tenia `[Succeeded]`: si CUALQUIERA de esos pasos fallaba, los cinco bloques de recuperacion
quedaban `Skipped` esa corrida entera, no solo el paso que fallo (detectado por el set V, caso
V-01, `pruebas/07-vigilancia.md`).

**Alcance decidido: los cinco eslabones, sin excepcion, y sin distinguir ninguno para "cortar".**
Motivo: los cuatro `Parametro_*` ya tienen, DESDE ANTES de este arreglo, un valor de respaldo
para cuando la lectura no esta (ver arriba: 15/30/3/30 dias-minutos), resuelto con
`coalesce(first(outputs('Parametro_*')?['body/value'])?['sanic_valor'], '<respaldo>')` dentro de
`Umbrales`. Eso significa que un `Parametro_*` que falla NO deja al flujo con un umbral vacio: lo
deja con el umbral de respaldo ya documentado y ya probado. No hacia falta agregar un valor por
defecto nuevo -- ya existia, y es justo lo que hace seguro blindar con `Failed` en los cuatro
lugares. Si `Umbrales` mismo llega a fallar (el unico caso no cubierto por el `coalesce`, porque
implicaria que `sanic_valor` trae algo no numerico: un problema de datos, no de disponibilidad),
dejar que `Correos_que_quedaron_en_la_bandeja` igual lo intente es preferible a saltearlo entero:
el bloque falla rapido y de forma RUIDOSA (queda `Failed`, visible en el historial de
ejecuciones) en vez de `Skipped` en silencio -- y de paso no le impide a `Leer_la_bandeja`
(la lectura de Outlook, que no depende de `Umbrales`) intentar de todos modos. No se encontro
ningun eslabon de la etapa de arranque cuyo fallo justifique CORTAR la cadena.

Se blindo con `[Succeeded, Failed]`, igual que la cadena entre los cinco bloques -- no se agrego
`TimedOut` ni `Skipped` para no romper la convencion ya establecida en el resto del archivo (y
porque ninguna de las llamadas de esta etapa tiene un `timeout` configurado que pueda producir
`TimedOut` distinto de `Failed`).

**Concurrency Control en el trigger, tambien aplicado.** El trigger `Cada_10_minutos` quedo con
`runtimeConfiguration.concurrency.runs = 1`: evita que dos corridas de `MPPP-VIG` se superpongan
(el `recurrence` de 10 minutos no lo impedia por si solo). Evidencia que lo justifico: decenas de
eventos Bitacora `Error` ("Fallo la ingesta del correo", origen MPPP-VIG) agrupados en ventanas de
2-3 minutos -- mas seguido de lo que una sola corrida secuencial cada 10 minutos explica. Ver
`pruebas/07-vigilancia.md` para la evidencia completa y el hallazgo de la causa de fondo del
error "Fallo la ingesta del correo" -- NO corregida desde este playbook: es de otro componente,
pendiente de que el coordinador le asigne su propio ID en `diseno/PENDIENTES.md`.

**Correccion 2026-09-28 (segunda vuelta): esto quedo en el ARCHIVO pero no viajo al entorno.**
`recursos/flujos/mppp-vig.json` ya traia `runtimeConfiguration.concurrency.runs = 1` en el trigger
desde que se escribio este parrafo, pero nadie corrio
`herramientas/construir/flujo.py playbooks/flujo/mppp-vig.md --completar` para subirlo: el
`clientdata` DESPLEGADO en Dev seguia sin el nodo (confirmado por el coordinador leyendo
`properties.definition.triggers.Cada_10_minutos.runtimeConfiguration`, ausente -- no buscando la
palabra "concurrency" en el texto plano, que aparece igual en los `Foreach` por `repetitions`, ya
presentes desde antes y sin relacion con esto). El caso V-01 del set V daba FALLA con razon. Se
corrigio corriendo `--completar`, y se releyo el `clientdata` desplegado navegando hasta el nodo
del trigger para confirmar `runtimeConfiguration.concurrency.runs = 1`, `recurrence` de 10 minutos
intacto, y el flujo ACTIVO (`statecode=1, statuscode=2`). Leccion: un cambio en el `.json` del
repositorio no es un cambio en el entorno hasta que `--completar` corre y alguien relee el
`clientdata` desplegado -- no alcanza con mirar el archivo ni con la memoria de "ya lo aplique".

## Divergencias declaradas respecto del diseno

- El bloque 5 (vencer los que nadie clasifico) corre en **todas** las ejecuciones, no "solo en la
  primera de cada dia". Es idempotente —lo que ya esta Vencida sale del filtro— y determinar "la
  primera del dia" exige estado que el flujo no tiene. El costo es una consulta mas cada 10
  minutos, que no devuelve nada casi nunca.
- Cada bloque toma como mucho **25** registros por ejecucion. Con unos cientos de correos por mes
  sobra, y evita una ejecucion eterna si algo se acumula.

## 1. Identidad

```json
{
  "tipo_playbook": "flujo",
  "version_skill": "0.1.0",
  "proyecto": "2026-001-referencias-planes-pago",
  "inventario": "11.5",
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
  "tipo": "flujo",
  "nombre": "Cloud Flow - MPPP - VIG - Vigilar pendientes",
  "descripcion": "Cada 10 minutos: recupera lo que quedo a medias llamando a MPPP-ING y MPPP-ENV, levanta para revision lo que no se puede reintentar, y vence lo que nadie clasifico.",
  "archivo": "recursos/flujos/mppp-vig.json",
  "conexiones": {
    "dataverse": {
      "referencia": "sanic_mppp_conr_dataverse",
      "api": "shared_commondataserviceforapps"
    },
    "outlook": {
      "referencia": "sanic_mppp_conr_outlook",
      "api": "shared_office365"
    }
  },
  "variables": [
    "sanic_mppp_ev_buzoningesta"
  ],
  "hijos": [
    "Cloud Flow - MPPP - ING - Ingerir y validar correo",
    "Cloud Flow - MPPP - ENV - Enviar comunicacion al cliente"
  ],
  "activar": true
}
```

## 3. Precondiciones

- `MPPP-ING` (11.1) y `MPPP-ENV` (11.3) existen y estan ACTIVADOS: un padre activo no puede llamar
  a un hijo que nunca se publico.
- Las dos connection references existen y estan conectadas.
- La tabla de Parametros tiene cargados los cuatro umbrales (9.1).
