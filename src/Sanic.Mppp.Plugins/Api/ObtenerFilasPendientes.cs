using System;
using System.Collections.Generic;
using System.Linq;
using Sanic.Mppp.Plugins.Datos;
using Sanic.Mppp.Plugins.Dominio;
using Sanic.Mppp.Plugins.Validacion;

namespace Sanic.Mppp.Plugins.Api
{
    /// <summary>Las salidas de `sanic_mppp_capi_obtenerfilaspendientes` (diseno/03 §2).</summary>
    public sealed class ResultadoDeFilasPendientes
    {
        public ResultadoDeFilasPendientes(string filasJson, int entregadas, int pendientes)
        {
            FilasJson = filasJson;
            Entregadas = entregadas;
            Pendientes = pendientes;
        }

        /// <summary>El JSON armado por <see cref="ArmadorJsonDelRpa"/>. `"[]"` si no hay nada que entregar.</summary>
        public string FilasJson { get; }

        public int Entregadas { get; }

        public int Pendientes { get; }
    }

    /// <summary>
    /// El plugin de `sanic_mppp_capi_obtenerfilaspendientes` (diseno/03 §2), sin el envoltorio `IPlugin` (que es
    /// <see cref="ObtenerFilasPendientesApi"/>). Orquesta piezas que YA EXISTEN, no decide nada nuevo. Contrato (lo
    /// fijan las pruebas `ObtenerFilasPendientesAceptacion`):
    ///  1. lee las Filas candidatas (<see cref="FilasDataverse.Candidatas"/>), YA en orden FIFO
    ///     (`sanic_fechavalidada`, `sanic_numerofila`);
    ///  2. filtra la PERTENENCIA con <see cref="SeleccionParaElRpa.EntraEnElLote"/> (atención manual, quién digitó,
    ///     `rpa.puedeaprobar`) sobre TODAS las candidatas, para poder contar cuántas quedan elegibles en total: el
    ///     claim nunca excluye (diseno/03 §2, "El claim NO excluye filas de futuras entregas al bot");
    ///  3. recorta a `maximo` (<see cref="ReglasDelRpa.CantidadAEntregar"/>: defecto 100, tope 500, 0 o negativo es
    ///     un `ArgumentException` que valida quien llama, no esta pieza);
    ///  4. sobre las que se van a entregar, y SOLO sobre esas (nunca sobre todas las candidatas): refresca el claim
    ///     (<see cref="FilasDataverse.RefrescarClaim"/> con <see cref="ReglasDelRpa.MinutosDeClaim"/>), resuelve el
    ///     tipo de formato del plan y el número de la Solicitud con UNA consulta cada una (nunca por fila), y el
    ///     código de banco de 3 dígitos con `plantilla.listas` (ya cargado para la validación, DD-10: "el bot no
    ///     tiene por qué saber que BANPRO es 008");
    ///  5. serializa con <see cref="ArmadorJsonDelRpa"/>;
    ///  6. `entregadas` = cuántas se entregaron; `pendientes` = elegibles totales menos entregadas (diseno/03 §2:
    ///     "cuántas quedan sin entregar DESPUÉS de este lote").
    /// Nada lee el reloj: la fecha llega por parámetro y tiene que ser UTC.
    /// </summary>
    public sealed class ObtenerFilasPendientes
    {
        /// <summary>Mismo parámetro que usa la validación (diseno/03 §1): acá solo hace falta para el código de banco (DD-10).</summary>
        public const string ParametroListas = "plantilla.listas";

        private readonly FilasDataverse _filas;
        private readonly CatalogosDataverse _catalogos;

        public ObtenerFilasPendientes(FilasDataverse filas, CatalogosDataverse catalogos)
        {
            _filas = filas ?? throw new ArgumentNullException(nameof(filas));
            _catalogos = catalogos ?? throw new ArgumentNullException(nameof(catalogos));
        }

        /// <param name="maximoPedido">`maximo` del contrato; ya resuelto a su defecto (100) por quien llama si el bot no lo mandó.</param>
        /// <param name="claimMinutosPedido">`claimminutos` del contrato: entero, mayor que 0 (lo exige <see cref="ReglasDelRpa.MinutosDeClaim"/>).</param>
        /// <param name="claimMinutosTope">`rpa.minutos.claimmaximo`, ya leído por quien llama (con su fallback si falta).</param>
        /// <param name="rpaPuedeAprobar">`rpa.puedeaprobar`, ya leído por quien llama.</param>
        public ResultadoDeFilasPendientes Ejecutar(
            int maximoPedido, int claimMinutosPedido, int claimMinutosTope, bool rpaPuedeAprobar, Guid quienLlama, DateTime ahoraUtc)
        {
            if (quienLlama == Guid.Empty)
            {
                throw new ArgumentException("El id de quien llama no puede estar vacío.", nameof(quienLlama));
            }

            if (ahoraUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("La fecha tiene que venir en UTC (Kind = DateTimeKind.Utc).", nameof(ahoraUtc));
            }

            var cantidadAEntregar = ReglasDelRpa.CantidadAEntregar(maximoPedido);
            var minutosDeClaim = ReglasDelRpa.MinutosDeClaim(claimMinutosPedido, claimMinutosTope);

            // Paso 1 y 2: candidatas FIFO, filtradas por PERTENENCIA (nunca por el claim: diseno/03 §2).
            var candidatas = _filas.Candidatas();
            var elegibles = candidatas
                .Where(f => SeleccionParaElRpa.EntraEnElLote(ConvertirACandidata(f), quienLlama, rpaPuedeAprobar, ahoraUtc))
                .ToList();

            // Paso 3: recorte a `maximo`.
            var aEntregar = elegibles.Take(cantidadAEntregar).ToList();

            if (aEntregar.Count == 0)
            {
                return new ResultadoDeFilasPendientes(ArmadorJsonDelRpa.Serializar(new List<FilaParaElRpa>()), 0, elegibles.Count);
            }

            // Paso 4: refresca el claim SOLO sobre lo que se entrega.
            var claimHasta = ahoraUtc.AddMinutes(minutosDeClaim);
            foreach (var fila in aEntregar)
            {
                _filas.RefrescarClaim(fila.FilaId, claimHasta);
            }

            var tiposDeFormato = _filas.TiposDeFormato(aEntregar.Where(f => f.PlanId.HasValue).Select(f => f.PlanId.Value));
            var numerosDeSolicitud = _filas.NumerosDeSolicitud(aEntregar.Select(f => f.SolicitudId));

            var listas = ListasPlantilla.DesdeJson(ParametroObligatorio(ParametroListas).Valor);

            var filasParaElRpa = aEntregar.Select(f => ArmarFilaParaElRpa(f, tiposDeFormato, numerosDeSolicitud, listas)).ToList();
            var json = ArmadorJsonDelRpa.Serializar(filasParaElRpa);

            return new ResultadoDeFilasPendientes(json, aEntregar.Count, elegibles.Count - aEntregar.Count);
        }

        private static FilaCandidata ConvertirACandidata(FilaPendienteLeida fila) => new FilaCandidata
        {
            Estado = fila.Estado,
            DigitadaPor = fila.DigitadaPor,
            RequiereAtencionManual = fila.RequiereAtencionManual,
        };

        private FilaParaElRpa ArmarFilaParaElRpa(
            FilaPendienteLeida fila, IDictionary<Guid, TipoDeFormatoDelPlan> tiposDeFormato, IDictionary<Guid, string> numerosDeSolicitud, ListasPlantilla listas)
        {
            if (!fila.PlanId.HasValue || !tiposDeFormato.TryGetValue(fila.PlanId.Value, out var tipoFormato))
            {
                throw new InvalidOperationException($"La fila {fila.FilaId} no tiene un plan resoluble: no se le puede armar el JSON al bot.");
            }

            if (!numerosDeSolicitud.TryGetValue(fila.SolicitudId, out var numeroDeSolicitud))
            {
                throw new InvalidOperationException($"La fila {fila.FilaId} no tiene una solicitud resoluble: no se le puede armar el JSON al bot.");
            }

            var textoBanco = fila.Banco?.ToString();

            return new FilaParaElRpa
            {
                FilaId = fila.FilaId,
                Solicitud = numeroDeSolicitud,
                NumeroFila = fila.NumeroFila,
                Estado = fila.Estado,
                Plan = fila.NumeroPlan,
                TipoFormato = TextoDeFormato(tipoFormato),
                Gestion = fila.Gestion?.ToString(),
                Clasificacion = fila.Clasificacion?.ToString(),
                NombreBeneficiario = fila.NombreBeneficiario,
                TipoIdentificacion = fila.TipoIdentificacion?.ToString(),
                NumeroIdentificacion = fila.NumeroIdentificacion,
                NumeroCuenta = fila.NumeroCuenta,
                Moneda = fila.Moneda?.ToString(),
                Banco = textoBanco,
                BancoCodigo = listas.Resolver(CamposDeFila.Banco, textoBanco)?.CodigoDeBanco,
                Referencia = fila.Referencia,
                ReferenciaRecibida = fila.ReferenciaRecibida,
                FechaValidada = fila.FechaValidada,
            };
        }

        /// <summary>`06`/`10`/`11`, como lo espera el bot (mismo texto que usa `plantilla.obligatoriedad`).</summary>
        private static string TextoDeFormato(TipoDeFormatoDelPlan formato)
        {
            switch (formato)
            {
                case TipoDeFormatoDelPlan._06: return "06";
                case TipoDeFormatoDelPlan._10: return "10";
                case TipoDeFormatoDelPlan._11: return "11";
                default: throw new ArgumentOutOfRangeException(nameof(formato), formato, "Tipo de formato de plan desconocido.");
            }
        }

        private ParametroLeido ParametroObligatorio(string nombre)
        {
            var parametro = _catalogos.Parametro(nombre);
            if (parametro == null)
            {
                throw new InvalidOperationException($"Falta el parámetro '{nombre}', necesario para entregar filas al bot.");
            }

            return parametro;
        }
    }
}
