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

        [Fact]
        public void LeerParaRegistro_de_una_fila_inexistente_propaga_la_falla_del_servicio()
        {
            var svc = new OrganizationServiceEnMemoria();
            Assert.Throws<System.ServiceModel.FaultException<OrganizationServiceFault>>(
                () => new FilasDataverse(svc).LeerParaRegistro(Guid.NewGuid()));
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
