using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Mppp.Plugins.Datos;
using Sanic.Mppp.Plugins.Dominio;
using Sanic.Mppp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Aceptacion
{
    /// <summary>Lectura y escritura de Fila para las dos Custom API del RPA (diseno/03 §2 y §3). Los nombres de columna son los de diseno/02.</summary>
    public class FilasDataverseAceptacion
    {
        private static readonly DateTime Base = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
        private static readonly Guid Usuario = Guid.NewGuid();

        private static Entity Fila(EstadoDeLaFila estado, DateTime fechaValidada, int numeroFila, Guid? digitadaPor = null, bool atencionManual = false, Guid? planId = null, Guid? solicitudId = null)
        {
            var e = new Entity(TablasHistorico.Fila)
            {
                ["sanic_estado"] = new OptionSetValue((int)estado),
                ["sanic_fechavalidada"] = fechaValidada,
                ["sanic_numerofila"] = numeroFila,
                ["sanic_requiereatencionmanual"] = atencionManual,
                ["sanic_solicitudid"] = new EntityReference(TablasHistorico.Solicitud, solicitudId ?? Guid.NewGuid()),
                ["sanic_numeroplan"] = "0001",
                ["sanic_gestion"] = new OptionSetValue((int)Gestion.Inclusion),
                ["sanic_clasificacion"] = new OptionSetValue((int)Clasificacion.ACH),
                ["sanic_moneda"] = new OptionSetValue((int)Moneda.COR),
                ["sanic_banco"] = new OptionSetValue((int)Banco.BANPRO),
                ["sanic_referencia"] = "REF-1",
                ["sanic_referenciarecibida"] = "REF-1",
            };
            if (planId.HasValue)
            {
                e["sanic_planid"] = new EntityReference(Tablas.Plan, planId.Value);
            }

            if (digitadaPor.HasValue)
            {
                e["sanic_digitadapor"] = new EntityReference(TablasNativas.Usuario, digitadaPor.Value);
            }

            return e;
        }

        // ---------- Candidatas ----------

        [Fact]
        public void Candidatas_solo_trae_Validada_y_Digitada_en_orden_FIFO()
        {
            var svc = new OrganizationServiceEnMemoria();
            var tercera = svc.Sembrar(Fila(EstadoDeLaFila.Validada, Base.AddMinutes(30), 3));
            var primera = svc.Sembrar(Fila(EstadoDeLaFila.Validada, Base, 1));
            var segunda = svc.Sembrar(Fila(EstadoDeLaFila.Digitada, Base, 2)); // misma fecha, desempata por numerofila
            svc.Sembrar(Fila(EstadoDeLaFila.Aprobada, Base.AddMinutes(-10), 0)); // no es candidata
            svc.Sembrar(Fila(EstadoDeLaFila.RechazadaEnAS400, Base.AddMinutes(-10), 0)); // no es candidata

            var candidatas = new FilasDataverse(svc).Candidatas();

            Assert.Equal(new[] { primera, segunda, tercera }, candidatas.Select(f => f.FilaId));
        }

        [Fact]
        public void Candidatas_no_filtra_por_atencion_manual_ni_por_quien_digito_eso_lo_decide_el_dominio()
        {
            var svc = new OrganizationServiceEnMemoria();
            var conAtencion = svc.Sembrar(Fila(EstadoDeLaFila.Validada, Base, 1, atencionManual: true));

            var candidatas = new FilasDataverse(svc).Candidatas();

            Assert.Contains(candidatas, f => f.FilaId == conAtencion && f.RequiereAtencionManual);
        }

        [Fact]
        public void Candidatas_es_UNA_sola_consulta()
        {
            var svc = new OrganizationServiceEnMemoria();
            svc.Sembrar(Fila(EstadoDeLaFila.Validada, Base, 1));
            svc.Sembrar(Fila(EstadoDeLaFila.Validada, Base, 2));

            new FilasDataverse(svc).Candidatas();

            Assert.Single(svc.Llamadas);
        }

        // ---------- TiposDeFormato / NumerosDeSolicitud ----------

        [Fact]
        public void TiposDeFormato_lee_planes_por_id_SIN_filtrar_por_activo()
        {
            var svc = new OrganizationServiceEnMemoria();
            var planActivo = svc.Sembrar(new Entity(Tablas.Plan) { ["statecode"] = new OptionSetValue(Tablas.Activo), ["sanic_tipoformato"] = new OptionSetValue((int)TipoDeFormatoDelPlan._11) });
            var planInactivo = svc.Sembrar(new Entity(Tablas.Plan) { ["statecode"] = new OptionSetValue(1), ["sanic_tipoformato"] = new OptionSetValue((int)TipoDeFormatoDelPlan._06) });

            var tipos = new FilasDataverse(svc).TiposDeFormato(new[] { planActivo, planInactivo });

            Assert.Equal(TipoDeFormatoDelPlan._11, tipos[planActivo]);
            Assert.Equal(TipoDeFormatoDelPlan._06, tipos[planInactivo]); // aunque esté desactivado: la fila ya se validó con ese plan
            Assert.Single(svc.Llamadas);
        }

        [Fact]
        public void TiposDeFormato_vacio_no_consulta_nada()
        {
            var svc = new OrganizationServiceEnMemoria();
            var tipos = new FilasDataverse(svc).TiposDeFormato(Array.Empty<Guid>());
            Assert.Empty(tipos);
            Assert.Empty(svc.Llamadas);
        }

        [Fact]
        public void NumerosDeSolicitud_lee_el_nombre_por_id_en_una_consulta()
        {
            var svc = new OrganizationServiceEnMemoria();
            var solicitudId = svc.Sembrar(new Entity(TablasHistorico.Solicitud) { ["sanic_nombre"] = "MPPP-00000123" });

            var numeros = new FilasDataverse(svc).NumerosDeSolicitud(new[] { solicitudId });

            Assert.Equal("MPPP-00000123", numeros[solicitudId]);
            Assert.Single(svc.Llamadas);
        }

        // ---------- PorIds (DN-02, exportacion) ----------

        [Fact]
        public void PorIds_trae_las_filas_pedidas_sin_filtrar_por_estado()
        {
            var svc = new OrganizationServiceEnMemoria();
            var validada = svc.Sembrar(Fila(EstadoDeLaFila.Validada, Base, 1));
            var aprobada = svc.Sembrar(Fila(EstadoDeLaFila.Aprobada, Base, 2));

            var filas = new FilasDataverse(svc).PorIds(new[] { validada, aprobada });

            Assert.Equal(2, filas.Count);
            Assert.Contains(filas, f => f.FilaId == validada);
            Assert.Contains(filas, f => f.FilaId == aprobada);
        }

        [Fact]
        public void PorIds_un_id_inexistente_no_esta_en_el_resultado_y_no_revienta()
        {
            var svc = new OrganizationServiceEnMemoria();
            var existe = svc.Sembrar(Fila(EstadoDeLaFila.Validada, Base, 1));

            var filas = new FilasDataverse(svc).PorIds(new[] { existe, Guid.NewGuid() });

            Assert.Single(filas);
            Assert.Equal(existe, filas[0].FilaId);
        }

        [Fact]
        public void PorIds_vacio_no_consulta_nada()
        {
            var svc = new OrganizationServiceEnMemoria();
            var filas = new FilasDataverse(svc).PorIds(Array.Empty<Guid>());
            Assert.Empty(filas);
            Assert.Empty(svc.Llamadas);
        }

        [Fact]
        public void PorIds_nulo_es_un_error_de_programacion()
        {
            var svc = new OrganizationServiceEnMemoria();
            Assert.Throws<ArgumentNullException>(() => new FilasDataverse(svc).PorIds(null));
        }

        [Fact]
        public void PorIds_es_UNA_sola_consulta()
        {
            var svc = new OrganizationServiceEnMemoria();
            var primera = svc.Sembrar(Fila(EstadoDeLaFila.Validada, Base, 1));
            var segunda = svc.Sembrar(Fila(EstadoDeLaFila.Digitada, Base, 2));

            new FilasDataverse(svc).PorIds(new[] { primera, segunda });

            Assert.Single(svc.Llamadas);
        }

        [Fact]
        public void PorIds_trae_los_valores_de_los_diez_campos_de_CamposDeFila()
        {
            var svc = new OrganizationServiceEnMemoria();
            var filaId = svc.Sembrar(Fila(EstadoDeLaFila.Validada, Base, 1));

            var fila = new FilasDataverse(svc).PorIds(new[] { filaId }).Single();

            Assert.Equal("0001", fila.NumeroPlan);
            Assert.Equal(Gestion.Inclusion, fila.Gestion);
            Assert.Equal(Clasificacion.ACH, fila.Clasificacion);
            Assert.Equal(Moneda.COR, fila.Moneda);
            Assert.Equal(Banco.BANPRO, fila.Banco);
            Assert.Equal("REF-1", fila.Referencia);
        }

        // ---------- Escrituras ----------

        [Fact]
        public void RefrescarClaim_pone_sanic_claimhasta()
        {
            var svc = new OrganizationServiceEnMemoria();
            var filaId = svc.Sembrar(Fila(EstadoDeLaFila.Validada, Base, 1));
            var claimHasta = Base.AddMinutes(30);

            new FilasDataverse(svc).RefrescarClaim(filaId, claimHasta);

            var registro = svc.Registros(TablasHistorico.Fila).Single(f => f.Id == filaId);
            Assert.Equal(claimHasta, registro["sanic_claimhasta"]);
        }

        [Fact]
        public void RefrescarClaim_exige_UTC()
        {
            var svc = new OrganizationServiceEnMemoria();
            var filaId = svc.Sembrar(Fila(EstadoDeLaFila.Validada, Base, 1));
            Assert.Throws<ArgumentException>(() => new FilasDataverse(svc).RefrescarClaim(filaId, DateTime.Now));
        }

        [Fact]
        public void LeerParaRegistro_trae_estado_digitadapor_y_atencion_manual()
        {
            var svc = new OrganizationServiceEnMemoria();
            var filaId = svc.Sembrar(Fila(EstadoDeLaFila.Digitada, Base, 1, digitadaPor: Usuario, atencionManual: true));

            var leida = new FilasDataverse(svc).LeerParaRegistro(filaId);

            Assert.Equal(EstadoDeLaFila.Digitada, leida.Estado);
            Assert.Equal(Usuario, leida.DigitadaPor);
            Assert.True(leida.RequiereAtencionManual);
        }

        /// <summary>
        /// Semántica cambiada el 2026-09-25 — NO la revirtás pensando que "antes andaba". Hasta esta fecha esta
        /// prueba fijaba lo contrario (`Assert.Throws&lt;FaultException&lt;OrganizationServiceFault&gt;&gt;`), porque
        /// `LeerParaRegistro` usaba `Retrieve` por id. Eso nunca fue una decisión de diseño: era un ACCIDENTE de la
        /// API de `Retrieve` (tira si no encuentra el id), que la prueba se limitó a describir.
        ///
        /// Ese mismo día, `herramientas/pruebas/probar_rpa.py --confirmar` contra el ENTORNO REAL (no las pruebas)
        /// mandó un lote de 3 ítems con uno de `filaid` inexistente, y la plataforma tumbó LA LLAMADA ENTERA con
        /// "ISV code reduced the open transaction count. Custom plug-ins should not catch exceptions from
        /// OrganizationService calls and continue processing.": `RegistrarResultadoRpa` atrapaba la `FaultException`
        /// de este método para seguir con el resto del lote, y ese `try/catch` alrededor de una llamada a
        /// `OrganizationService` es justo lo que corrompe el contador de transacción de Dataverse.
        ///
        /// Un `filaid` que no existe es un DATO MALO del bot — no una condición excepcional de plataforma — y se
        /// decide con una lectura que puede no encontrar nada, nunca con una excepción atrapada. Por eso
        /// `LeerParaRegistro` pasó a `RetrieveMultiple` y esta prueba fija la semántica nueva.
        /// </summary>
        [Fact]
        public void LeerParaRegistro_de_una_fila_inexistente_devuelve_null_no_propaga_ninguna_falla_del_servicio()
        {
            var svc = new OrganizationServiceEnMemoria();
            Assert.Null(new FilasDataverse(svc).LeerParaRegistro(Guid.NewGuid()));
        }

        [Fact]
        public void RegistrarDigitacion_pone_estado_digitadapor_y_fecha_y_NO_libera_el_claim()
        {
            var svc = new OrganizationServiceEnMemoria();
            var filaId = svc.Sembrar(Fila(EstadoDeLaFila.Validada, Base, 1));

            new FilasDataverse(svc).RegistrarDigitacion(filaId, Usuario, Base);

            var registro = svc.Registros(TablasHistorico.Fila).Single(f => f.Id == filaId);
            Assert.Equal((int)EstadoDeLaFila.Digitada, ((OptionSetValue)registro["sanic_estado"]).Value);
            Assert.Equal(Usuario, ((EntityReference)registro["sanic_digitadapor"]).Id);
            Assert.Equal(Base, registro["sanic_fechadigitada"]);
            Assert.False(registro.Contains("sanic_claimhasta")); // no lo toca: sigue vigente
        }

        [Fact]
        public void RegistrarAprobacion_libera_el_claim()
        {
            var svc = new OrganizationServiceEnMemoria();
            var filaId = svc.Sembrar(Fila(EstadoDeLaFila.Digitada, Base, 1, digitadaPor: Usuario));
            svc.Update(new Entity(TablasHistorico.Fila, filaId) { ["sanic_claimhasta"] = Base.AddMinutes(10) });

            new FilasDataverse(svc).RegistrarAprobacion(filaId, Usuario, Base);

            var registro = svc.Registros(TablasHistorico.Fila).Single(f => f.Id == filaId);
            Assert.Equal((int)EstadoDeLaFila.Aprobada, ((OptionSetValue)registro["sanic_estado"]).Value);
            Assert.Null(registro["sanic_claimhasta"]);
        }

        [Fact]
        public void RegistrarRechazoEnAS400_escribe_mensaje_al_cliente_y_notainterna_por_separado_y_libera_el_claim()
        {
            var svc = new OrganizationServiceEnMemoria();
            var filaId = svc.Sembrar(Fila(EstadoDeLaFila.Validada, Base, 1));
            svc.Update(new Entity(TablasHistorico.Fila, filaId) { ["sanic_claimhasta"] = Base.AddMinutes(10) });

            new FilasDataverse(svc).RegistrarRechazoEnAS400(filaId, "Texto para el cliente", "CUENTA INACTIVA (texto crudo de AS400)");

            var registro = svc.Registros(TablasHistorico.Fila).Single(f => f.Id == filaId);
            Assert.Equal((int)EstadoDeLaFila.RechazadaEnAS400, ((OptionSetValue)registro["sanic_estado"]).Value);
            Assert.Equal("Texto para el cliente", registro["sanic_mensaje"]);
            Assert.Equal("CUENTA INACTIVA (texto crudo de AS400)", registro["sanic_notainterna"]);
            Assert.Null(registro["sanic_claimhasta"]);
        }

        [Fact]
        public void MarcarAtencionManual_no_toca_el_estado_pero_libera_el_claim()
        {
            var svc = new OrganizationServiceEnMemoria();
            var filaId = svc.Sembrar(Fila(EstadoDeLaFila.Digitada, Base, 1, digitadaPor: Usuario));
            svc.Update(new Entity(TablasHistorico.Fila, filaId) { ["sanic_claimhasta"] = Base.AddMinutes(10) });

            new FilasDataverse(svc).MarcarAtencionManual(filaId, "El bot no reconoce esta condicion de AS400.");

            var registro = svc.Registros(TablasHistorico.Fila).Single(f => f.Id == filaId);
            Assert.Equal((int)EstadoDeLaFila.Digitada, ((OptionSetValue)registro["sanic_estado"]).Value); // no cambia
            Assert.True((bool)registro["sanic_requiereatencionmanual"]);
            Assert.Equal("El bot no reconoce esta condicion de AS400.", registro["sanic_notainterna"]);
            Assert.Null(registro["sanic_claimhasta"]);
        }
    }
}
