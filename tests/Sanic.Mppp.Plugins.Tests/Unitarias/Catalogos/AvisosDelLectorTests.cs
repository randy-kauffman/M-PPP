using System;
using System.Linq;
using Sanic.Mppp.Plugins.Catalogos;
using Sanic.Mppp.Plugins.Plantilla;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Unitarias.Catalogos
{
    /// <summary>
    /// Los avisos del lector (revisión 4c, LP-07 "nada se pierde callado") como error de su fila, en los dos planificadores:
    /// el texto exacto, varios avisos en una fila, la fila que no tiene error propio y los argumentos inválidos del Excel 2.
    /// </summary>
    public class AvisosDelLectorTests
    {
        private static readonly DateTime Hoy = new DateTime(2026, 10, 1);

        private static EstadoDeCatalogos Foto() => new EstadoDeCatalogos(
            new ClienteExistente[0], new PlanExistente[0], new AutorizadoExistente[0], new AutorizacionClienteExistente[0], new UsuarioCandidato[0]);

        private static FilaPlantilla Fila(int numero, params (string letra, string valor)[] valores)
            => new FilaPlantilla { NumeroFilaExcel = numero, NumeroOrden = numero - 1, Valores = valores.ToDictionary(v => v.letra, v => v.valor) };

        private static AdvertenciaLectura Aviso(int fila, string columna, string motivo)
            => new AdvertenciaLectura { NumeroFilaExcel = fila, Columna = columna, Motivo = motivo };

        [Fact]
        public void El_error_lleva_el_encabezado_de_la_columna_el_motivo_del_lector_y_que_hacer()
        {
            var plan = PlanificadorDeCarga.Planificar(new[] { Fila(2, ("A", "123")) }, Foto(), Hoy,
                new[] { Aviso(2, "K", "celda K2: tiene un valor de error de Excel ('#REF!')") });
            var error = Assert.Single(plan.Filas[0].Errores, e => e.StartsWith("Comentarios: ", StringComparison.Ordinal));
            Assert.StartsWith("Comentarios: celda K2: tiene un valor de error de Excel ('#REF!').", error);
            Assert.Contains("Corregí o borrá esa celda", error);
            Assert.StartsWith("Error: Comentarios: ", plan.Filas[0].Texto);
        }

        [Fact]
        public void Dos_avisos_de_una_fila_son_dos_errores_y_los_de_otras_filas_no_se_mezclan()
        {
            var filas = new[] { Fila(2, ("A", "123")), Fila(3, ("A", "456")), Fila(4, ("A", "789")) };
            var plan = PlanificadorDeCarga.Planificar(filas, Foto(), Hoy, new[]
            {
                Aviso(4, "J", "celda J4: x"), Aviso(2, "K", "celda K2: y"), Aviso(2, "B", "celda B2: z"),
            });
            Assert.Equal(2, plan.Filas[0].Errores.Count(e => e.Contains("celda B2: z") || e.Contains("celda K2: y")));
            Assert.Contains(plan.Filas[0].Errores, e => e.StartsWith("CIF COM: ", StringComparison.Ordinal) && e.Contains("celda B2: z"));
            Assert.DoesNotContain(plan.Filas[1].Errores, e => e.Contains("celda"));
            Assert.Contains(plan.Filas[2].Errores, e => e.StartsWith("Fecha documento: ", StringComparison.Ordinal) && e.Contains("celda J4: x"));
        }

        [Fact]
        public void Un_aviso_sin_motivo_igual_marca_el_error()
        {
            var plan = PlanificadorDeCarga.Planificar(new[] { Fila(2, ("A", "123")) }, Foto(), Hoy, new[] { Aviso(2, "K", null) });
            Assert.True(plan.TieneErrores);
            Assert.Contains(plan.Filas[0].Errores, e => e.StartsWith("Comentarios: ", StringComparison.Ordinal));
        }

        [Fact]
        public void En_el_cambio_de_estado_el_encabezado_es_el_de_su_columna()
        {
            var plan = PlanificadorDeCambioDeEstado.Planificar(new[] { Fila(2, ("A", "CLIENTE"), ("B", "ACTIVAR"), ("C", "123")) }, Foto(),
                new[] { Aviso(2, "C", "celda C2: tiene un valor de error de Excel ('#N/A')") });
            Assert.True(plan.TieneErrores);
            Assert.Contains(plan.Filas[0].Errores, e => e.StartsWith("CIF BAC: celda C2", StringComparison.Ordinal));
            Assert.Empty(plan.Cambios);
        }

        [Fact]
        public void En_el_cambio_de_estado_un_aviso_mal_armado_es_error_de_quien_llama()
        {
            var filas = new[] { Fila(2, ("A", "CLIENTE")) };
            Assert.ThrowsAny<ArgumentException>(() => PlanificadorDeCambioDeEstado.Planificar(filas, Foto(), new AdvertenciaLectura[] { null }));
            Assert.ThrowsAny<ArgumentException>(() => PlanificadorDeCambioDeEstado.Planificar(filas, Foto(), new[] { Aviso(9, "A", "x") }));
            Assert.ThrowsAny<ArgumentException>(() => PlanificadorDeCambioDeEstado.Planificar(filas, Foto(), new[] { Aviso(2, "G", "x") }));   // G es Resultado: no es de la estructura de dominio
            Assert.Throws<ArgumentNullException>(() => PlanificadorDeCambioDeEstado.Planificar(null, Foto(), new AdvertenciaLectura[0]));
            Assert.Throws<ArgumentNullException>(() => PlanificadorDeCambioDeEstado.Planificar(filas, null, new AdvertenciaLectura[0]));
        }

        [Fact]
        public void En_la_carga_filas_o_foto_nulas_siguen_siendo_ArgumentNullException_con_la_sobrecarga_nueva()
        {
            Assert.Throws<ArgumentNullException>(() => PlanificadorDeCarga.Planificar(null, Foto(), Hoy, new AdvertenciaLectura[0]));
            Assert.Throws<ArgumentNullException>(() => PlanificadorDeCarga.Planificar(new FilaPlantilla[0], null, Hoy, new AdvertenciaLectura[0]));
        }

        [Fact]
        public void Un_aviso_de_la_columna_Resultado_no_es_de_la_estructura_de_dominio()
        {
            Assert.ThrowsAny<ArgumentException>(() => PlanificadorDeCarga.Planificar(new[] { Fila(2, ("A", "123")) }, Foto(), Hoy, new[] { Aviso(2, "L", "x") }));
        }
    }
}
