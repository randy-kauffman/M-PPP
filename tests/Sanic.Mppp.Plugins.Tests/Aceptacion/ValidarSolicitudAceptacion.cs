using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Mppp.Plugins.Api;
using Sanic.Mppp.Plugins.Datos;
using Sanic.Mppp.Plugins.Dominio;
using Sanic.Mppp.Plugins.Plantilla;
using Sanic.Mppp.Plugins.Tests.Apoyo;
using Sanic.Mppp.Plugins.Tests.Dobles;
using Sanic.Mppp.Plugins.Validacion;
using Xunit;

namespace Sanic.Mppp.Plugins.Tests.Aceptacion
{
    /// <summary>
    /// 7.7: la Custom API de validación (diseno/03 §1), de punta a punta sobre el doble. Es la pieza que junta todo: parámetros,
    /// reglas del sobre, lector, reglas de registro, acuse y cierre.
    /// </summary>
    public class ValidarSolicitudAceptacion
    {
        private static readonly DateTime Ahora = new DateTime(2026, 9, 21, 17, 0, 0, DateTimeKind.Utc);
        private const string Hoja = "Datos";

        // La ventana: encabezado en la fila 12, datos desde la 13, 100 filas. Columnas C..L, en el orden de CamposDeFila.
        private static readonly string[] Columnas = { "C", "D", "E", "F", "G", "H", "I", "J", "K", "L" };

        private static readonly Dictionary<string, string> Encabezados = new Dictionary<string, string>
        {
            ["gestion"] = "Gestion", ["clasificacion"] = "Clasificacion", ["tipoIdentificacion"] = "Tipo de identificacion", ["moneda"] = "Moneda",
            ["banco"] = "Banco", ["numeroPlan"] = "No. Plan", ["nombreBeneficiario"] = "Nombre del beneficiario",
            ["numeroIdentificacion"] = "No. Identificacion", ["numeroCuenta"] = "No. Cuenta", ["referencia"] = "Referencia",
        };

        private static string Estructura()
        {
            var campos = CamposDeFila.Todos().Select((c, i) =>
            {
                var limites = c == "numeroCuenta" ? @",""largoMinimo"":1,""largoMaximo"":16,""formato"":""digitos"""
                    : c == "numeroPlan" ? @",""largoMinimo"":1,""largoMaximo"":4,""formato"":""alfanumerico"""
                    : c == "nombreBeneficiario" ? @",""largoMaximo"":44,""formato"":""texto"""
                    : string.Empty;
                return $@"{{""nombre"":""{c}"",""columna"":""{Columnas[i]}"",""encabezado"":""{Encabezados[c]}""{limites}}}";
            });
            return @"{""hoja"":""" + Hoja + @""",""filaEncabezado"":12,""primeraFila"":13,""cantidadFilas"":100,""campos"":[" + string.Join(",", campos) + "]}";
        }

        /// <summary>Una plantilla con las filas que se le pidan: cada fila es el valor de cada campo, en el orden de `CamposDeFila.Todos()`.</summary>
        private static byte[] Excel(params string[][] filas)
        {
            var b = new ConstructorXlsxDePrueba(Hoja);
            foreach (var campo in CamposDeFila.Todos().Select((c, i) => (c, i)))
            {
                b.ConTexto($"{Columnas[campo.i]}12", Encabezados[campo.c]);
            }

            for (var f = 0; f < filas.Length; f++)
            {
                for (var c = 0; c < filas[f].Length; c++)
                {
                    if (!string.IsNullOrEmpty(filas[f][c]))
                    {
                        b.ConTexto($"{Columnas[c]}{13 + f}", filas[f][c]);
                    }
                }
            }

            return b.Bytes();
        }

        private static string[] Fila(string plan = "0042", string cuenta = "123456789", string nombre = "Juan Perez", string moneda = "USD", string gestion = "Inclusion",
            string clasificacion = "ACH", string banco = "BAC")
        {
            return new[] { gestion, clasificacion, "CNA", moneda, banco, plan, nombre, "0011234567890", cuenta, string.Empty };
        }

        private sealed class Mundo
        {
            public readonly OrganizationServiceEnMemoria Svc = new OrganizationServiceEnMemoria();
            public readonly ArchivosDeMemoria Archivos = new ArchivosDeMemoria();
            public readonly AvisosSimulados Avisos = new AvisosSimulados();
            /// <summary>El último lector que la fábrica armó. Nunca nulo, aunque Ejecutar reviente antes de pedirlo.</summary>
            public LectorQueCuenta Lector { get; private set; } = new LectorQueCuenta(new LimitesLectura());

            /// <summary>Lo que la pieza recibe de verdad: una fábrica, porque los topes salen del parámetro `lectura.limites`.</summary>
            public Func<LimitesLectura, ILectorPlantilla> FabricaDeLector => limites => Lector = new LectorQueCuenta(limites);

            public Guid SolicitudId;

            public Mundo(byte[] excel, int adjuntos = 1, int excels = 1, EstadoDeLaSolicitud estado = EstadoDeLaSolicitud.Ingresada, bool conParametros = true, string obligatoriedad = null)
            {
                Entity Activo(string tabla, params (string, object)[] atributos)
                {
                    var e = new Entity(tabla) { ["statecode"] = new OptionSetValue(0) };
                    foreach (var (a, v) in atributos)
                    {
                        e[a] = v;
                    }

                    return e;
                }

                EjecutivoDelCliente = Svc.Sembrar(new Entity(TablasNativas.Usuario) { ["fullname"] = "Eje Cutivo", ["isdisabled"] = false });
                ClienteId = Svc.Sembrar(Activo(Tablas.Cliente, ("sanic_nombre", "ACME"), ("ownerid", new EntityReference(TablasNativas.Usuario, EjecutivoDelCliente))));
                PlanId = Svc.Sembrar(Activo(Tablas.Plan, ("sanic_codigo", "0042"), ("sanic_tipoformato", new OptionSetValue((int)TipoDeFormatoDelPlan._11)), ("sanic_moneda", new OptionSetValue((int)Moneda.USD)), ("sanic_clienteid", new EntityReference(Tablas.Cliente, ClienteId))));
                var autorizado = Svc.Sembrar(Activo(Tablas.Autorizado, ("sanic_nombre", "ana@acme.com"), ("sanic_clienteid", new EntityReference(Tablas.Cliente, ClienteId))));
                Svc.Sembrar(Activo(Tablas.AutorizacionPlan, ("sanic_autorizadoid", new EntityReference(Tablas.Autorizado, autorizado)), ("sanic_planid", new EntityReference(Tablas.Plan, PlanId))));
                // DN-05: un plan formato 06 (solo BAC) del mismo cliente y autorizado, para lo que se completa al guardar.
                var plan06 = Svc.Sembrar(Activo(Tablas.Plan, ("sanic_codigo", "0006"), ("sanic_tipoformato", new OptionSetValue((int)TipoDeFormatoDelPlan._06)), ("sanic_moneda", new OptionSetValue((int)Moneda.COR)), ("sanic_clienteid", new EntityReference(Tablas.Cliente, ClienteId))));
                Svc.Sembrar(Activo(Tablas.AutorizacionPlan, ("sanic_autorizadoid", new EntityReference(Tablas.Autorizado, autorizado)), ("sanic_planid", new EntityReference(Tablas.Plan, plan06))));

                if (conParametros)
                {
                    Parametro(ValidarSolicitud.ParametroEstructura, Estructura());
                    Parametro(ValidarSolicitud.ParametroListas, ParametrosDePlantillaAceptacion.ListasValidas);
                    Parametro(ValidarSolicitud.ParametroObligatoriedad, obligatoriedad ?? ParametrosDePlantillaAceptacion.ObligatoriedadInicial);
                    Parametro(ValidarSolicitud.ParametroLimites, @"{""maximoBytesComprimido"":2097152,""maximoBytesDescomprimido"":20971520}");
                    Parametro(ValidarSolicitud.ParametroFormatos, Dn05Aceptacion.FormatosDn05);
                    Parametro(ValidarSolicitud.ParametroLargosDeCuenta, Dn05Aceptacion.LargosDn05);
                }

                var sobre = new[] { ReglasDelSobre.TraeAdjunto, ReglasDelSobre.AdjuntoEsExcel, ReglasDelSobre.UnSoloExcel, ReglasDelSobre.EstructuraPlantilla, ReglasDelSobre.TieneFilas };
                for (var i = 0; i < sobre.Length; i++)
                {
                    Svc.Sembrar(Activo(Tablas.Regla, ("sanic_codigo", sobre[i]), ("sanic_nivel", new OptionSetValue((int)NivelDeLaRegla.Solicitud)), ("sanic_orden", (i + 1) * 10),
                        ("sanic_dependede", i == 0 ? null : sobre[i - 1]), ("sanic_efecto", new OptionSetValue((int)EfectoDeLaRegla.Rechaza))));
                }

                // DN-06 (2026-11-24): el catálogo de registro DESTINO (el de DN-05 más PLAN_REQUIERE_PREREGISTRO, con las cinco
                // reglas que dependían de la autorización colgando de ella). Antes, DN-05 (FORMATO_11_SOLO_ACH inactiva). El catálogo de la transición, con FORMATO_11_SOLO_ACH todavía activa, lo cubre
                // ModoDeAutorizacionWiringAceptacion.
                var registro = Dn06Aceptacion.SemillaRegistro
                    .Select(r => (r.codigo, dependeDe: r.dependeDe.Length == 0 ? null : string.Join(",", r.dependeDe)))
                    .ToArray();
                for (var i = 0; i < registro.Length; i++)
                {
                    Svc.Sembrar(Activo(Tablas.Regla, ("sanic_codigo", registro[i].codigo), ("sanic_nivel", new OptionSetValue((int)NivelDeLaRegla.Registro)), ("sanic_orden", (i + 1) * 10),
                        ("sanic_dependede", registro[i].dependeDe), ("sanic_efecto", new OptionSetValue((int)EfectoDeLaRegla.Rechaza))));
                }

                SolicitudId = Svc.Sembrar(new Entity(TablasHistorico.Solicitud)
                {
                    ["sanic_nombre"] = "MPPP-00000123", ["sanic_estadoprocesamiento"] = new OptionSetValue((int)estado), ["sanic_remitente"] = "ana@acme.com",
                    ["sanic_asunto"] = "Inclusiones", ["sanic_fecharecibido"] = Ahora, ["sanic_messageid"] = "<m@x>",
                    ["sanic_cantidadadjuntos"] = adjuntos, ["sanic_cantidadexcel"] = excels,
                    ["sanic_filastotales"] = 7, ["sanic_filasvalidas"] = 5, ["sanic_filasrechazadas"] = 2,
                });
                if (excel != null)
                {
                    Archivos.Contenido[ValidarSolicitud.ColumnaExcel] = excel;
                }
            }

            public Guid PlanId { get; }

            public Guid ClienteId { get; private set; }

            /// <summary>El dueño de <see cref="ClienteId"/> (RF-19, D-23): a quien tiene que llegar el aviso de "filas por digitar".</summary>
            public Guid EjecutivoDelCliente { get; private set; }

            public void Parametro(string nombre, string valor, int version = 1)
            {
                Svc.Sembrar(new Entity(Tablas.Parametro) { ["statecode"] = new OptionSetValue(0), ["sanic_nombre"] = nombre, ["sanic_version"] = version, ["sanic_valor"] = valor });
            }

            public ResultadoDeValidacion Correr()
            {
                return new ValidarSolicitud(new SolicitudesDataverse(Svc), new CatalogosDataverse(Svc), Archivos, FabricaDeLector, Avisos).Ejecutar(SolicitudId, Ahora);
            }

            public Entity Solicitud() => Svc.Retrieve(TablasHistorico.Solicitud, SolicitudId, new ColumnSet(true));

            public EstadoDeLaSolicitud Estado() => (EstadoDeLaSolicitud)Solicitud().GetAttributeValue<OptionSetValue>("sanic_estadoprocesamiento").Value;

            public IList<Entity> Filas() => Svc.Registros(TablasHistorico.Fila).OrderBy(f => (int)f["sanic_numerofila"]).ToList();

            public IList<Entity> Resultados() => Svc.Registros(TablasHistorico.ResultadoRegla).OrderBy(r => (int)r["sanic_orden"]).ToList();

            public IList<Entity> Bitacora() => Svc.Registros(TablasHistorico.Bitacora);
        }

        private sealed class AvisosSimulados : IAvisos
        {
            public readonly List<(Guid solicitudId, string titulo, string cuerpo)> DeEjecutivos = new List<(Guid, string, string)>();
            public readonly List<(Guid solicitudId, Guid clienteId, string titulo, string cuerpo)> PorDigitar = new List<(Guid, Guid, string, string)>();
            public readonly List<(Guid solicitudId, string titulo, string cuerpo)> PorAprobar = new List<(Guid, string, string)>();

            public void AvisarAEjecutivos(Guid solicitudId, string titulo, string cuerpo) => DeEjecutivos.Add((solicitudId, titulo, cuerpo));

            public void AvisarFilasPorDigitar(Guid solicitudId, Guid clienteId, string titulo, string cuerpo) => PorDigitar.Add((solicitudId, clienteId, titulo, cuerpo));

            public void AvisarFilasPorAprobar(Guid solicitudId, string titulo, string cuerpo) => PorAprobar.Add((solicitudId, titulo, cuerpo));
        }

        private sealed class ArchivosDeMemoria : IArchivos
        {
            public readonly Dictionary<string, byte[]> Contenido = new Dictionary<string, byte[]>();
            public readonly List<(string columna, long maximo)> Descargas = new List<(string, long)>();

            public byte[] Descargar(string tabla, Guid id, string columna, long maximoBytes)
            {
                Descargas.Add((columna, maximoBytes));
                if (!Contenido.TryGetValue(columna, out var bytes))
                {
                    throw new InvalidOperationException("no hay archivo en esa columna");
                }

                if (bytes.LongLength > maximoBytes)
                {
                    throw new ArchivoExcedeElMaximoException(bytes.LongLength, maximoBytes);
                }

                return bytes;
            }

            public byte[] DescargarInicio(string tabla, Guid id, string columna, int maximoBytes) => throw new NotSupportedException("la validación baja el Excel completo");
        }

        /// <summary>El lector real, envuelto para contar cuántas veces se abre el Excel.</summary>
        private sealed class LectorQueCuenta : ILectorPlantilla
        {
            private readonly LectorOpenXml _real;

            public LectorQueCuenta(LimitesLectura limites)
            {
                Limites = limites;
                _real = new LectorOpenXml(limites);
            }

            /// <summary>Con qué topes se armó: así se comprueba que `lectura.limites` llega hasta acá.</summary>
            public LimitesLectura Limites { get; }

            public int Lecturas { get; private set; }

            public ResultadoLecturaPlantilla Leer(System.IO.Stream excel, ConfiguracionPlantilla configuracion)
            {
                Lecturas++;
                return _real.Leer(excel, configuracion);
            }

            public ResultadoLecturaPlantilla Leer(byte[] excel, ConfiguracionPlantilla configuracion)
            {
                Lecturas++;
                return _real.Leer(excel, configuracion);
            }
        }

        // ------------------------------------------------------------------ el camino completo
        [Fact]
        public void Una_plantilla_con_filas_buenas_y_malas_deja_la_solicitud_en_proceso_con_todo_guardado()
        {
            var m = new Mundo(Excel(
                Fila(),
                Fila(plan: "9999"), // el plan no existe
                Fila(moneda: "COR"), // la moneda no es la del plan
                Fila(cuenta: "12A")));

            var r = m.Correr();

            Assert.Equal((EstadoDeLaSolicitud.EnProceso, false, 4, 1, 3), (r.Estado, r.YaProcesada, r.FilasTotales, r.FilasValidas, r.FilasRechazadas));
            Assert.Equal(EstadoDeLaSolicitud.EnProceso, m.Estado());

            var solicitud = m.Solicitud();
            Assert.Equal((4, 1, 3), (solicitud["sanic_filastotales"], solicitud["sanic_filasvalidas"], solicitud["sanic_filasrechazadas"]));
            Assert.Equal(Ahora, solicitud["sanic_fechavalidada"]);
            Assert.Equal("estructura=1;listas=1;obligatoriedad=1;formatos=1;largos=1", solicitud["sanic_versionparametros"]);
            var acuse = (string)solicitud["sanic_acusecontenido"];
            Assert.Contains("MPPP-00000123", acuse);
            Assert.Contains("<table", acuse);
            Assert.DoesNotContain("123456789", acuse); // la cuenta va enmascarada (DD-08)

            var filas = m.Filas();
            Assert.Equal(new[] { 1, 2, 3, 4 }, filas.Select(f => (int)f["sanic_numerofila"]));
            Assert.Equal((int)EstadoDeLaFila.Validada, filas[0].GetAttributeValue<OptionSetValue>("sanic_estado").Value);
            Assert.Equal(m.PlanId, filas[0].GetAttributeValue<EntityReference>("sanic_planid").Id);
            Assert.Equal("10200000000123456789", filas[0]["sanic_referencia"]); // D-17: la construida
            Assert.Equal("MPPP-00000123-F01", filas[0]["sanic_nombre"]); // ya calculado: el step no vuelve a leer la Solicitud
            Assert.All(filas.Skip(1), f => Assert.Equal((int)EstadoDeLaFila.RechazadaEnValidacion, f.GetAttributeValue<OptionSetValue>("sanic_estado").Value));
            Assert.All(filas.Skip(1), f => Assert.False(string.IsNullOrWhiteSpace((string)f["sanic_mensaje"])));
            Assert.False(filas[0].Contains("sanic_mensaje"));

            // El historial regla por regla es del SOBRE (02 §3.3): las cinco, ninguna de registro.
            Assert.Equal(5, m.Resultados().Count);
            Assert.All(m.Resultados(), x => Assert.Equal((int)ResultadoDeLaRegla.Cumplida, x.GetAttributeValue<OptionSetValue>("sanic_resultado").Value));
            var evento = Assert.Single(m.Bitacora());
            Assert.Equal((int)EventoDeBitacora.ValidacionTerminada, evento.GetAttributeValue<OptionSetValue>("sanic_evento").Value);
            Assert.Equal(1, m.Lector.Lecturas); // el Excel se abre UNA sola vez

            // D-64: la solicitud dejó una fila por digitar (Validada) -> UN aviso al ejecutivo dueño del cliente,
            // nunca uno por fila.
            var aviso = Assert.Single(m.Avisos.PorDigitar);
            Assert.Equal((m.SolicitudId, m.ClienteId), (aviso.solicitudId, aviso.clienteId));
            Assert.Contains("MPPP-00000123", aviso.titulo);
            Assert.Empty(m.Avisos.PorAprobar);
            Assert.Empty(m.Avisos.DeEjecutivos);
        }

        [Fact]
        public void Los_catalogos_se_cargan_una_vez_por_ejecucion_nunca_una_consulta_por_fila()
        {
            var filas = Enumerable.Range(0, 30).Select(i => Fila(cuenta: (100000000 + i).ToString()) /* 9 dígitos: el mínimo de BAC (DN-05) */).ToArray();
            var m = new Mundo(Excel(filas));

            var r = m.Correr();

            Assert.Equal(30, r.FilasValidas);
            var consultas = m.Svc.Llamadas.Count(l => l.Operacion == "RetrieveMultiple");
            Assert.InRange(consultas, 1, 16); // 8 parámetros (DN-05 sumó 2, D-82 la zona horaria, DN-06 la obligatoriedad de conectividad) + 2 niveles de reglas + planes + autorizaciones (hasta 4)
            Assert.Equal(30, m.Svc.Llamadas.Count(l => l.Operacion == "Create" && l.Entidad == TablasHistorico.Fila));
            Assert.All(m.Filas(), f => Assert.StartsWith("MPPP-00000123-F", (string)f["sanic_nombre"]));
        }

        // ------------------------------------------------------------------ el sobre corta antes de leer filas
        [Fact]
        public void Sin_adjunto_la_solicitud_queda_rechazada_y_el_excel_nunca_se_abre()
        {
            var m = new Mundo(Excel(Fila()), adjuntos: 0, excels: 0);

            var r = m.Correr();

            Assert.Equal((EstadoDeLaSolicitud.Rechazada, 0, 0, 0), (r.Estado, r.FilasTotales, r.FilasValidas, r.FilasRechazadas));
            Assert.Equal(EstadoDeLaSolicitud.Rechazada, m.Estado());
            Assert.Empty(m.Filas());
            Assert.Equal(0, m.Lector.Lecturas);
            Assert.Empty(m.Archivos.Descargas);
            var acuse = (string)m.Solicitud()["sanic_acusecontenido"];
            Assert.Contains("nada que procesar", acuse, StringComparison.OrdinalIgnoreCase);
            var resultados = m.Resultados();
            Assert.Equal(5, resultados.Count);
            Assert.Equal((int)ResultadoDeLaRegla.NoCumplida, resultados[0].GetAttributeValue<OptionSetValue>("sanic_resultado").Value);
            Assert.All(resultados.Skip(1), x => Assert.Equal((int)ResultadoDeLaRegla.Omitida, x.GetAttributeValue<OptionSetValue>("sanic_resultado").Value));
            Assert.Single(m.Bitacora());
        }

        [Fact]
        public void Una_plantilla_que_no_es_la_vigente_rechaza_con_los_motivos_del_lector_y_sin_detalle_tecnico()
        {
            var otra = new ConstructorXlsxDePrueba("OtraHoja").ConTexto("C12", "Cualquier cosa").Bytes();
            var m = new Mundo(otra);

            var r = m.Correr();

            Assert.Equal(EstadoDeLaSolicitud.Rechazada, r.Estado);
            Assert.Equal(1, m.Lector.Lecturas);
            Assert.Empty(m.Filas());
            var acuse = (string)m.Solicitud()["sanic_acusecontenido"];
            Assert.DoesNotContain("OpenXml", acuse);
            Assert.DoesNotContain("Exception", acuse);
            var estructura = m.Resultados().Single(x => (string)x["sanic_reglacodigo"] == ReglasDelSobre.EstructuraPlantilla);
            Assert.Equal((int)ResultadoDeLaRegla.NoCumplida, estructura.GetAttributeValue<OptionSetValue>("sanic_resultado").Value);
        }

        [Fact]
        public void Una_plantilla_vigente_pero_sin_filas_rechaza_por_tiene_filas()
        {
            var m = new Mundo(Excel());
            var r = m.Correr();
            Assert.Equal((EstadoDeLaSolicitud.Rechazada, 0), (r.Estado, r.FilasTotales));
            var tieneFilas = m.Resultados().Single(x => (string)x["sanic_reglacodigo"] == ReglasDelSobre.TieneFilas);
            Assert.Equal((int)ResultadoDeLaRegla.NoCumplida, tieneFilas.GetAttributeValue<OptionSetValue>("sanic_resultado").Value);
        }

        [Fact]
        public void Si_ninguna_fila_queda_validada_la_solicitud_se_rechaza_igual_con_sus_filas_guardadas()
        {
            var m = new Mundo(Excel(Fila(plan: "9999"), Fila(cuenta: "12A")));

            var r = m.Correr();

            Assert.Equal((EstadoDeLaSolicitud.Rechazada, 2, 0, 2), (r.Estado, r.FilasTotales, r.FilasValidas, r.FilasRechazadas));
            Assert.Equal(2, m.Filas().Count);
            var acuse = (string)m.Solicitud()["sanic_acusecontenido"];
            Assert.Contains("nada que procesar", acuse, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("<table", acuse);

            // D-64: sin ninguna fila Validada no hay "filas por digitar": no se avisa a nadie.
            Assert.Empty(m.Avisos.PorDigitar);
        }

        [Fact]
        public void Un_excel_mas_pesado_que_el_limite_rechaza_por_estructura_no_revienta()
        {
            var m = new Mundo(Excel(Fila()));
            m.Parametro(ValidarSolicitud.ParametroLimites, @"{""maximoBytesComprimido"":10,""maximoBytesDescomprimido"":20}", version: 2);

            var r = m.Correr();

            Assert.Equal(EstadoDeLaSolicitud.Rechazada, r.Estado);
            Assert.Empty(m.Filas());
            Assert.Equal(0, m.Lector.Lecturas); // ni se abre: el tamaño se controla antes (LP-02)
        }

        // Aprobador, 2026-11-24: un Excel pesado se rechaza SOLO por el peso, con este texto exacto. Antes el cliente recibía
        // además "no tiene la estructura de la plantilla esperada... solicite la plantilla vigente", que era falso.
        [Theory]
        [InlineData(2L * 1024 * 1024, "El archivo de Excel pesa más de lo permitido (máximo 2 MB). Por favor, envíe un archivo más liviano.")]
        [InlineData(10L * 1024 * 1024, "El archivo de Excel pesa más de lo permitido (máximo 10 MB). Por favor, envíe un archivo más liviano.")]
        [InlineData(2621440L, "El archivo de Excel pesa más de lo permitido (máximo 2,5 MB). Por favor, envíe un archivo más liviano.")]
        public void El_mensaje_de_peso_excedido_cita_el_maximo_configurado_en_megas(long maximoBytes, string esperado)
        {
            Assert.Equal(esperado, ValidarSolicitud.MensajeDePesoExcedido(maximoBytes));
        }

        [Fact]
        public void Un_excel_mas_pesado_que_el_limite_le_dice_al_cliente_solo_que_pesa_demasiado()
        {
            var m = new Mundo(Excel(Fila()));
            m.Parametro(ValidarSolicitud.ParametroLimites, @"{""maximoBytesComprimido"":10,""maximoBytesDescomprimido"":20}", version: 2);

            m.Correr();

            var estructura = m.Resultados().Single(x => (string)x["sanic_reglacodigo"] == ReglasDelSobre.EstructuraPlantilla);
            Assert.Equal((int)ResultadoDeLaRegla.NoCumplida, estructura.GetAttributeValue<OptionSetValue>("sanic_resultado").Value);
            Assert.Equal(ValidarSolicitud.MensajeDePesoExcedido(10), (string)estructura["sanic_razon"]);
            Assert.DoesNotContain("estructura", (string)m.Solicitud()["sanic_acusecontenido"], StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Un_excel_que_pasa_el_tope_comprimido_pero_no_el_descomprimido_recibe_el_mismo_mensaje_de_peso_sin_jerga()
        {
            // LP-03: el lector lo rechaza por el tamaño declarado en el zip. Al cliente no le llega "bytes descomprimidos" ni
            // "zip": recibe el mismo texto del peso, con el máximo del archivo que él manda (el comprimido).
            var m = new Mundo(Excel(Fila()));
            m.Parametro(ValidarSolicitud.ParametroLimites, @"{""maximoBytesComprimido"":10485760,""maximoBytesDescomprimido"":20}", version: 2);

            var r = m.Correr();

            Assert.Equal(EstadoDeLaSolicitud.Rechazada, r.Estado);
            Assert.Equal(1, m.Lector.Lecturas);
            var estructura = m.Resultados().Single(x => (string)x["sanic_reglacodigo"] == ReglasDelSobre.EstructuraPlantilla);
            var razon = (string)estructura["sanic_razon"];
            Assert.Equal(ValidarSolicitud.MensajeDePesoExcedido(10485760), razon);
            Assert.DoesNotContain("zip", razon, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("bytes", razon, StringComparison.OrdinalIgnoreCase);
        }

        // ------------------------------------------------------------------ idempotencia y errores
        [Theory]
        [InlineData(EstadoDeLaSolicitud.EnProceso)]
        [InlineData(EstadoDeLaSolicitud.Rechazada)]
        [InlineData(EstadoDeLaSolicitud.Cerrada)]
        public void Una_solicitud_que_ya_no_esta_en_ingresada_devuelve_lo_guardado_sin_tocar_nada(EstadoDeLaSolicitud estado)
        {
            var m = new Mundo(Excel(Fila()), estado: estado);
            var llamadasAntes = m.Svc.Llamadas.Count;

            var r = m.Correr();
            var llamadasDelPlugin = m.Svc.Llamadas.Count - llamadasAntes;

            Assert.Equal((estado, true, 7, 5, 2), (r.Estado, r.YaProcesada, r.FilasTotales, r.FilasValidas, r.FilasRechazadas));
            Assert.Empty(m.Filas());
            Assert.Empty(m.Resultados());
            Assert.Empty(m.Bitacora());
            Assert.Equal(0, m.Lector.Lecturas);
            Assert.Equal(1, llamadasDelPlugin);
        }

        [Theory]
        [InlineData(ValidarSolicitud.ParametroEstructura)]
        [InlineData(ValidarSolicitud.ParametroListas)]
        [InlineData(ValidarSolicitud.ParametroObligatoriedad)]
        [InlineData(ValidarSolicitud.ParametroLimites)]
        [InlineData(ValidarSolicitud.ParametroFormatos)]
        [InlineData(ValidarSolicitud.ParametroLargosDeCuenta)]
        public void Un_parametro_que_falta_es_un_error_real_y_nada_queda_a_medias(string cual)
        {
            var m = new Mundo(Excel(Fila()), conParametros: false);
            var todos = new[]
            {
                ValidarSolicitud.ParametroEstructura, ValidarSolicitud.ParametroListas, ValidarSolicitud.ParametroObligatoriedad, ValidarSolicitud.ParametroLimites,
                ValidarSolicitud.ParametroFormatos, ValidarSolicitud.ParametroLargosDeCuenta,
            };
            foreach (var otro in todos.Where(p => p != cual))
            {
                m.Parametro(otro, otro == ValidarSolicitud.ParametroEstructura ? Estructura()
                    : otro == ValidarSolicitud.ParametroListas ? ParametrosDePlantillaAceptacion.ListasValidas
                    : otro == ValidarSolicitud.ParametroObligatoriedad ? ParametrosDePlantillaAceptacion.ObligatoriedadInicial
                    : otro == ValidarSolicitud.ParametroFormatos ? Dn05Aceptacion.FormatosDn05
                    : otro == ValidarSolicitud.ParametroLargosDeCuenta ? Dn05Aceptacion.LargosDn05
                    : @"{""maximoBytesComprimido"":2097152,""maximoBytesDescomprimido"":20971520}");
            }

            var ex = Record.Exception(() => m.Correr());

            Assert.NotNull(ex);
            Assert.IsNotType<NullReferenceException>(ex);
            Assert.Contains(cual, ex.Message);
            Assert.Equal(EstadoDeLaSolicitud.Ingresada, m.Estado());
            Assert.Empty(m.Filas());
            Assert.Empty(m.Resultados());
        }

        // ------------------------------------------------------------------ D-82: la hora del acuse (aprobador, 2026-11-24)
        // Antes el acuse decía la hora UTC ("la recibimos el 24/11/2026 06:12" cuando en Nicaragua eran las 00:12).
        // Parámetro OPCIONAL `respuesta.zonahoraria` ("+HH:mm" / "-HH:mm"); si no existe, -06:00. Mal escrito: error real.
        // Ahora = 21/09/2026 17:00 UTC.
        private static string Acuse(Mundo m) => (string)m.Solicitud()["sanic_acusecontenido"];

        [Fact]
        public void Sin_el_parametro_de_zona_horaria_el_acuse_dice_la_hora_de_nicaragua()
        {
            var m = new Mundo(Excel(Fila()));

            m.Correr();

            Assert.Contains("y la recibimos el 21/09/2026 11:00.", Acuse(m));
            Assert.DoesNotContain("17:00", Acuse(m));
        }

        [Theory]
        [InlineData("-06:00", "21/09/2026 11:00")]
        [InlineData("+00:00", "21/09/2026 17:00")]
        [InlineData("-05:30", "21/09/2026 11:30")]
        [InlineData("+08:00", "22/09/2026 01:00")] // cambia el día
        [InlineData("-14:00", "21/09/2026 03:00")]
        [InlineData("+14:00", "22/09/2026 07:00")]
        [InlineData(" -06:00 ", "21/09/2026 11:00")] // espacios a los lados: se recortan
        public void Con_el_parametro_de_zona_horaria_el_acuse_dice_la_hora_de_esa_zona(string valor, string esperado)
        {
            var m = new Mundo(Excel(Fila()));
            m.Parametro(ValidarSolicitud.ParametroZonaHoraria, valor);

            m.Correr();

            Assert.Contains("y la recibimos el " + esperado + ".", Acuse(m));
        }

        [Theory]
        [InlineData("")]
        [InlineData("6")]
        [InlineData("-6")]
        [InlineData("-06")]
        [InlineData("-6:00")]
        [InlineData("06:00")] // sin signo: no se adivina
        [InlineData("-06:60")]
        [InlineData("+14:30")] // fuera de -14:00..+14:00
        [InlineData("-15:00")]
        [InlineData("UTC-6")]
        [InlineData("-6 horas")]
        [InlineData("−06:00")] // signo menos tipográfico (U+2212), no el guion
        public void Una_zona_horaria_mal_escrita_es_un_error_real_que_nombra_el_parametro_y_nada_queda_a_medias(string valor)
        {
            var m = new Mundo(Excel(Fila()));
            m.Parametro(ValidarSolicitud.ParametroZonaHoraria, valor);

            var ex = Record.Exception(() => m.Correr());

            Assert.NotNull(ex);
            Assert.IsNotType<NullReferenceException>(ex);
            Assert.Contains(ValidarSolicitud.ParametroZonaHoraria, ex.Message);
            Assert.Equal(EstadoDeLaSolicitud.Ingresada, m.Estado());
            Assert.Empty(m.Filas());
            Assert.Empty(m.Resultados());
        }

        [Fact]
        public void El_nombre_del_parametro_de_zona_horaria_es_el_aprobado()
        {
            Assert.Equal("respuesta.zonahoraria", ValidarSolicitud.ParametroZonaHoraria);
        }

        [Fact]
        public void Un_parametro_mal_cargado_tambien_es_un_error_real()
        {
            var m = new Mundo(Excel(Fila()));
            m.Parametro(ValidarSolicitud.ParametroListas, "{no es json", version: 2);
            Assert.ThrowsAny<Exception>(() => m.Correr());
            Assert.Equal(EstadoDeLaSolicitud.Ingresada, m.Estado());
        }

        [Fact]
        public void La_version_de_los_parametros_es_la_activa_mas_alta_de_cada_uno()
        {
            var m = new Mundo(Excel(Fila()));
            m.Parametro(ValidarSolicitud.ParametroListas, ParametrosDePlantillaAceptacion.ListasValidas, version: 7);
            m.Parametro(ValidarSolicitud.ParametroEstructura, Estructura(), version: 3);
            m.Parametro(ValidarSolicitud.ParametroFormatos, Dn05Aceptacion.FormatosDn05, version: 4);
            m.Parametro(ValidarSolicitud.ParametroLargosDeCuenta, Dn05Aceptacion.LargosDn05, version: 2);

            m.Correr();

            Assert.Equal("estructura=3;listas=7;obligatoriedad=1;formatos=4;largos=2", m.Solicitud()["sanic_versionparametros"]);
        }

        [Fact]
        public void El_resumen_es_una_linea_para_el_run_history_sin_datos_del_cliente()
        {
            var m = new Mundo(Excel(Fila(nombre: "Juan Secreto Perez"), Fila(plan: "9999")));
            var r = m.Correr();
            Assert.False(string.IsNullOrWhiteSpace(r.Resumen));
            Assert.DoesNotContain("\n", r.Resumen);
            Assert.DoesNotContain("Secreto", r.Resumen);
            Assert.DoesNotContain("123456789", r.Resumen);
            Assert.Contains("2", r.Resumen);
        }

        [Fact]
        public void Los_argumentos_se_validan_y_la_fecha_tiene_que_ser_utc()
        {
            var m = new Mundo(Excel(Fila()));
            var api = new ValidarSolicitud(new SolicitudesDataverse(m.Svc), new CatalogosDataverse(m.Svc), m.Archivos, m.FabricaDeLector, m.Avisos);
            Assert.Throws<ArgumentException>(() => api.Ejecutar(Guid.Empty, Ahora));
            Assert.Throws<ArgumentException>(() => api.Ejecutar(m.SolicitudId, DateTime.SpecifyKind(Ahora, DateTimeKind.Local)));
            Assert.Throws<ArgumentNullException>(() => new ValidarSolicitud(null, new CatalogosDataverse(m.Svc), m.Archivos, m.FabricaDeLector, m.Avisos));
            Assert.Throws<ArgumentNullException>(() => new ValidarSolicitud(new SolicitudesDataverse(m.Svc), null, m.Archivos, m.FabricaDeLector, m.Avisos));
            Assert.Throws<ArgumentNullException>(() => new ValidarSolicitud(new SolicitudesDataverse(m.Svc), new CatalogosDataverse(m.Svc), null, m.FabricaDeLector, m.Avisos));
            Assert.Throws<ArgumentNullException>(() => new ValidarSolicitud(new SolicitudesDataverse(m.Svc), new CatalogosDataverse(m.Svc), m.Archivos, null, m.Avisos));
            Assert.Throws<ArgumentNullException>(() => new ValidarSolicitud(new SolicitudesDataverse(m.Svc), new CatalogosDataverse(m.Svc), m.Archivos, m.FabricaDeLector, null));
        }

        /// <summary>
        /// Los topes de `lectura.limites` tienen que llegar al LECTOR, no quedarse en la descarga: el de bytes
        /// descomprimidos es la defensa contra un zip bomba (LP-02) y vive dentro del lector. Por eso la pieza recibe
        /// una fábrica y no un lector ya armado.
        /// </summary>
        [Fact]
        public void Los_topes_del_parametro_llegan_al_lector_y_no_se_quedan_en_la_descarga()
        {
            var m = new Mundo(Excel(Fila()), conParametros: false);
            m.Parametro(ValidarSolicitud.ParametroEstructura, Estructura());
            m.Parametro(ValidarSolicitud.ParametroListas, ParametrosDePlantillaAceptacion.ListasValidas);
            m.Parametro(ValidarSolicitud.ParametroObligatoriedad, ParametrosDePlantillaAceptacion.ObligatoriedadInicial);
            m.Parametro(ValidarSolicitud.ParametroLimites, @"{""maximoBytesComprimido"":1048576,""maximoBytesDescomprimido"":3145728}");
            m.Parametro(ValidarSolicitud.ParametroFormatos, Dn05Aceptacion.FormatosDn05);
            m.Parametro(ValidarSolicitud.ParametroLargosDeCuenta, Dn05Aceptacion.LargosDn05);

            m.Correr();

            Assert.Equal(1, m.Lector.Lecturas);
            Assert.Equal(1048576L, m.Lector.Limites.TamanoMaximoBytesEntrada);
            Assert.Equal(3145728L, m.Lector.Limites.TamanoMaximoBytesDescomprimidos);
        }

        [Fact]
        public void Un_catalogo_de_reglas_mal_armado_es_un_error_real_antes_de_escribir_nada()
        {
            var m = new Mundo(Excel(Fila()));
            m.Svc.Sembrar(new Entity(Tablas.Regla)
            {
                ["statecode"] = new OptionSetValue(0), ["sanic_codigo"] = "SIN_EVALUADOR", ["sanic_nivel"] = new OptionSetValue((int)NivelDeLaRegla.Solicitud),
                ["sanic_orden"] = 99, ["sanic_efecto"] = new OptionSetValue((int)EfectoDeLaRegla.Rechaza),
            });

            Assert.Throws<ConfiguracionDeReglasInvalidaException>(() => m.Correr());
            Assert.Equal(EstadoDeLaSolicitud.Ingresada, m.Estado());
            Assert.Empty(m.Resultados());
        }

        // ------------------------------------------------------------------ DN-01 (wiring, punto 4): el modo de autorizacion llega hasta acá
        // Prueba que ValidarSolicitud (1) lee `autorizacion.modo` con CatalogosDataverse.ResolverModoDeAutorizacion(),
        // y (2) pasa el modo y los clientes autorizados del remitente al constructor de CatalogosDeValidacion — no
        // alcanza con que el dominio (AutorizacionPorClienteAceptacion) ya sepa mirar la vía correcta si nadie
        // adentro de ValidarSolicitud se la pasa.
        [Fact]
        public void En_modo_cliente_una_fila_de_un_plan_del_mismo_cliente_se_valida_sin_autorizacionplan_propia()
        {
            var m = new Mundo(Excel(Fila(plan: "0099")));
            m.Parametro(ModoDeAutorizacionParametro.Nombre, "cliente");

            // El plan 0099 es del MISMO cliente que el 0042 (ACME), pero NO tiene su propia AutorizacionPlan:
            // en modo plan esta fila quedaría Sin autorización. Solo se valida si el modo cliente autoriza por
            // TODOS los planes del cliente, sin enumerarlos (DN-01).
            var planActual = m.Svc.Retrieve(Tablas.Plan, m.PlanId, new ColumnSet("sanic_clienteid"));
            var clienteId = planActual.GetAttributeValue<EntityReference>("sanic_clienteid").Id;
            m.Svc.Sembrar(new Entity(Tablas.Plan)
            {
                ["statecode"] = new OptionSetValue(0), ["sanic_codigo"] = "0099",
                ["sanic_tipoformato"] = new OptionSetValue((int)TipoDeFormatoDelPlan._11), ["sanic_moneda"] = new OptionSetValue((int)Moneda.USD),
                ["sanic_clienteid"] = new EntityReference(Tablas.Cliente, clienteId),
            });

            // Un autorizado "hermano" (mismo correo, otra fila de catálogo), autorizado por CLIENTE, sin ninguna
            // AutorizacionPlan propia — así la única vía posible para validar esta fila es la de cliente.
            var autorizadoHermano = m.Svc.Sembrar(new Entity(Tablas.Autorizado) { ["statecode"] = new OptionSetValue(0), ["sanic_nombre"] = "ana@acme.com" });
            m.Svc.Sembrar(new Entity(Tablas.AutorizacionCliente)
            {
                ["statecode"] = new OptionSetValue(0),
                ["sanic_autorizadoid"] = new EntityReference(Tablas.Autorizado, autorizadoHermano),
                ["sanic_clienteid"] = new EntityReference(Tablas.Cliente, clienteId),
            });

            var r = m.Correr();

            Assert.Equal((EstadoDeLaSolicitud.EnProceso, 1, 1, 0), (r.Estado, r.FilasTotales, r.FilasValidas, r.FilasRechazadas));
            var fila = Assert.Single(m.Filas());
            Assert.Equal((int)EstadoDeLaFila.Validada, fila.GetAttributeValue<OptionSetValue>("sanic_estado").Value);
        }

        [Fact]
        public void En_modo_cliente_una_autorizacionplan_existente_deja_de_alcanzar_sin_autorizacioncliente()
        {
            // Mismo remitente y mismo plan 0042 de siempre: Mundo YA sembró una AutorizacionPlan(ana@acme.com, 0042)
            // válida. En modo PLAN esta fila se valida (lo prueba el resto de esta clase). En modo CLIENTE, sin
            // ninguna AutorizacionCliente, tiene que dejar de alcanzar: si esto da Validada, ValidarSolicitud no
            // está mirando el modo — sigue usando la vía vieja de memoria.
            var m = new Mundo(Excel(Fila()));
            m.Parametro(ModoDeAutorizacionParametro.Nombre, "cliente");

            // El texto "no negociable" (DN-01 punto 5) sale del catálogo (`sanic_mensajecliente` de la Regla, que
            // MotorDeReglas.cs compone vía MensajeAlCliente.Componer), NO de un default en el código: en Dataverse,
            // PLAN_EXISTE y AUTORIZACION_CORREO_PLAN ya traen ese mismo texto configurado (playbooks/datos/reglas.md).
            // Este Mundo compartido no lo siembra para ninguna regla (las demás pruebas del archivo solo miran que
            // el mensaje no esté vacío); esta prueba SÍ mira el texto exacto, así que lo configura acá — sin tocar
            // el resto del fixture ni ninguna otra prueba.
            const string plantillaUnificada = "El plan {plan} no existe o usted no está autorizado sobre él.";
            foreach (var codigo in new[] { ReglasDeRegistro.PlanExiste, ReglasDeRegistro.AutorizacionCorreoPlan })
            {
                var regla = m.Svc.Registros(Tablas.Regla).Single(r => (string)r["sanic_codigo"] == codigo);
                m.Svc.Update(new Entity(Tablas.Regla, regla.Id) { ["sanic_mensajecliente"] = plantillaUnificada });
            }

            var r = m.Correr();

            Assert.Equal((EstadoDeLaSolicitud.Rechazada, 1, 0, 1), (r.Estado, r.FilasTotales, r.FilasValidas, r.FilasRechazadas));
            var fila = Assert.Single(m.Filas());
            Assert.Equal((int)EstadoDeLaFila.SinAutorizacion, fila.GetAttributeValue<OptionSetValue>("sanic_estado").Value);
            // No negociable (DN-01 punto 5): el mismo texto unificado que en modo plan, byte a byte.
            Assert.Equal("El plan 0042 no existe o usted no está autorizado sobre él.", (string)fila["sanic_mensaje"]);
        }

        [Fact]
        public void Sin_el_parametro_autorizacion_modo_el_comportamiento_es_el_de_siempre_modo_plan()
        {
            // El parámetro no existe en el entorno todavía (playbook recién sembrado en algunos entornos, no en
            // todos): tiene que degradar a plan y seguir validando por AutorizacionPlan, sin romper nada.
            var m = new Mundo(Excel(Fila()));

            var r = m.Correr();

            Assert.Equal((EstadoDeLaSolicitud.EnProceso, 1, 1, 0), (r.Estado, r.FilasTotales, r.FilasValidas, r.FilasRechazadas));
        }

        // ------------------------------------------------------------------ DN-05: lo completado se GUARDA
        [Fact]
        public void Dn05_la_clasificacion_vacia_en_un_plan_de_una_sola_clasificacion_se_guarda_completada()
        {
            // 0042 es formato 11: solo ACH (plan.formatos). La celda vacía no es un error ni queda vacía en la Fila.
            var m = new Mundo(Excel(Fila(clasificacion: string.Empty), Fila(clasificacion: "BAC")));

            m.Correr();

            var filas = m.Filas();
            Assert.Equal((int)EstadoDeLaFila.Validada, filas[0].GetAttributeValue<OptionSetValue>("sanic_estado").Value);
            Assert.Equal((int)Clasificacion.ACH, filas[0].GetAttributeValue<OptionSetValue>("sanic_clasificacion").Value);

            // Un valor recibido que el formato no admite NUNCA se reemplaza: se rechaza con el mensaje de la regla.
            Assert.Equal((int)EstadoDeLaFila.RechazadaEnValidacion, filas[1].GetAttributeValue<OptionSetValue>("sanic_estado").Value);
            Assert.Equal((int)Clasificacion.BAC, filas[1].GetAttributeValue<OptionSetValue>("sanic_clasificacion").Value);
            Assert.Contains("El plan 0042 solo admite la clasificación ACH.", filas[1].GetAttributeValue<string>("sanic_mensaje"));
        }

        [Fact]
        public void Dn05_el_banco_y_la_clasificacion_vacios_en_un_plan_06_se_guardan_como_BAC()
        {
            // Revisión DN-05, hallazgo 3 (mutación M23 sobrevivía): lo que se GUARDA es el banco efectivo, no el recibido.
            var m = new Mundo(Excel(Fila(plan: "0006", moneda: "COR", clasificacion: string.Empty, banco: string.Empty)));

            m.Correr();

            var fila = m.Filas().Single();
            Assert.Equal((int)EstadoDeLaFila.Validada, fila.GetAttributeValue<OptionSetValue>("sanic_estado").Value);
            Assert.Equal((int)Banco.BAC, fila.GetAttributeValue<OptionSetValue>("sanic_banco").Value);
            Assert.Equal((int)Clasificacion.BAC, fila.GetAttributeValue<OptionSetValue>("sanic_clasificacion").Value);
        }

        [Fact]
        public void Dn05_plan_formatos_con_un_valor_que_plantilla_listas_no_acepta_es_un_error_real()
        {
            // Revisión DN-05, hallazgo 4: completar con un valor que la lista no acepta guardaría algo que ninguna fila podría
            // escribir. Se cruza al cargar, antes de escribir nada. Los bancos de `cuenta.largos` NO se cruzan: uno sin
            // entrada en la lista simplemente nunca se alcanza.
            var m = new Mundo(Excel(Fila()));
            m.Parametro(ValidarSolicitud.ParametroListas, ParametrosDePlantillaAceptacion.ListasValidas.Replace(@",{""valor"":""CK"",""variantes"":[""cheque""]}", string.Empty), version: 2);

            var ex = Assert.Throws<InvalidOperationException>(() => m.Correr());

            Assert.Contains(ValidarSolicitud.ParametroFormatos, ex.Message);
            Assert.Contains("CK", ex.Message);
            Assert.Equal(EstadoDeLaSolicitud.Ingresada, m.Estado());
            Assert.Empty(m.Filas());
        }

        [Fact]
        public void Dn05_una_modificacion_en_un_plan_formato_11_se_rechaza()
        {
            var m = new Mundo(Excel(Fila(gestion: "Modificacion")));

            m.Correr();

            var fila = m.Filas().Single();
            Assert.Equal((int)EstadoDeLaFila.RechazadaEnValidacion, fila.GetAttributeValue<OptionSetValue>("sanic_estado").Value);
            Assert.Contains("El plan 0042 no admite la gestión Modificacion.", fila.GetAttributeValue<string>("sanic_mensaje"));
        }

        // ------------------------------------------------------------------ DN-06 (aprobador, 2026-11-24): canal y pre-registro del plan
        private void CambiarPlan(Mundo m, params (string atributo, object valor)[] cambios)
        {
            var e = new Entity(Tablas.Plan, m.PlanId);
            foreach (var (a, v) in cambios)
            {
                e[a] = v;
            }

            m.Svc.Update(e);
        }

        [Fact]
        public void Dn06_las_filas_de_un_plan_que_no_requiere_preregistro_se_rechazan_con_el_texto_del_aprobador()
        {
            var m = new Mundo(Excel(Fila(), Fila(cuenta: "987654321")));
            CambiarPlan(m, ("sanic_requierepreregistro", false));

            var r = m.Correr();

            Assert.Equal((EstadoDeLaSolicitud.Rechazada, 2, 0, 2), (r.Estado, r.FilasTotales, r.FilasValidas, r.FilasRechazadas));
            Assert.All(m.Filas(), f =>
            {
                Assert.Equal((int)EstadoDeLaFila.RechazadaEnValidacion, f.GetAttributeValue<OptionSetValue>("sanic_estado").Value);
                Assert.Equal("Este plan no requiere pre-registro.", f["sanic_mensaje"]);
            });
        }

        [Fact]
        public void Dn06_un_plan_grabado_sin_canal_ni_preregistro_se_valida_como_antes()
        {
            // El Mundo siembra los planes SIN las columnas nuevas: es el ambiente recién parchado, antes del paso de datos.
            var m = new Mundo(Excel(Fila()));
            var r = m.Correr();
            Assert.Equal((EstadoDeLaSolicitud.EnProceso, 1, 1, 0), (r.Estado, r.FilasTotales, r.FilasValidas, r.FilasRechazadas));
            Assert.Equal("estructura=1;listas=1;obligatoriedad=1;formatos=1;largos=1", m.Solicitud()["sanic_versionparametros"]);
        }

        [Fact]
        public void Dn06_una_fila_de_conectividad_sin_el_parametro_es_un_error_real_que_lo_nombra_antes_de_escribir()
        {
            var m = new Mundo(Excel(Fila()));
            CambiarPlan(m, ("sanic_canal", new OptionSetValue((int)Canal.Conectividad)));

            var ex = Assert.ThrowsAny<InvalidOperationException>(() => m.Correr());

            Assert.Contains("plantilla.obligatoriedad.conectividad", ex.Message);
            Assert.Equal(EstadoDeLaSolicitud.Ingresada, m.Estado());
            Assert.Empty(m.Filas());
        }

        [Fact]
        public void Dn06_una_fila_de_conectividad_usa_su_matriz_y_la_version_queda_registrada()
        {
            // La matriz de Conectividad de esta prueba exige la referencia, que Fila() deja vacía; la de siempre no.
            var m = new Mundo(Excel(Fila()));
            m.Parametro(ValidarSolicitud.ParametroObligatoriedadConectividad, @"{""porDefecto"":""obligatorio"",""opcionales"":[{""campo"":""moneda""}]}", version: 2);
            CambiarPlan(m, ("sanic_canal", new OptionSetValue((int)Canal.Conectividad)));

            var r = m.Correr();

            Assert.Equal((EstadoDeLaSolicitud.Rechazada, 1, 0, 1), (r.Estado, r.FilasTotales, r.FilasValidas, r.FilasRechazadas));
            Assert.StartsWith("Faltan datos obligatorios:", (string)Assert.Single(m.Filas())["sanic_mensaje"]);
            Assert.Equal("estructura=1;listas=1;obligatoriedad=1;formatos=1;largos=1;obligatoriedadconectividad=2", m.Solicitud()["sanic_versionparametros"]);
        }

        [Fact]
        public void Dn06_con_el_parametro_cargado_una_fila_de_banca_en_linea_sigue_usando_la_matriz_de_siempre()
        {
            var m = new Mundo(Excel(Fila()));
            m.Parametro(ValidarSolicitud.ParametroObligatoriedadConectividad, @"{""porDefecto"":""obligatorio"",""opcionales"":[{""campo"":""moneda""}]}");

            var r = m.Correr();

            Assert.Equal((EstadoDeLaSolicitud.EnProceso, 1, 1, 0), (r.Estado, r.FilasTotales, r.FilasValidas, r.FilasRechazadas));
            Assert.Equal("estructura=1;listas=1;obligatoriedad=1;formatos=1;largos=1;obligatoriedadconectividad=1", m.Solicitud()["sanic_versionparametros"]);
        }

        [Fact]
        public void Dn06_el_parametro_de_conectividad_mal_cargado_es_un_error_real_aunque_ninguna_fila_lo_use()
        {
            var m = new Mundo(Excel(Fila()));
            m.Parametro(ValidarSolicitud.ParametroObligatoriedadConectividad, "{no es json");

            Assert.ThrowsAny<Exception>(() => m.Correr());
            Assert.Equal(EstadoDeLaSolicitud.Ingresada, m.Estado());
            Assert.Empty(m.Filas());
        }
    }
}
