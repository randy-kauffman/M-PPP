namespace Sanic.Mppp.Plugins.Tests.Apoyo
{
    /// <summary>
    /// Colecciones de xUnit: las clases de una misma colección NO corren en paralelo entre sí. `RelojDeApi` agrupa a toda
    /// clase que use o dependa de un `RelojParaPruebas` estático de una Custom API (revisión 4c, 2026-10-01): una prueba que
    /// lo pone en 100 s no puede pisar a otra que corre al mismo tiempo y espera el reloj real.
    /// </summary>
    public static class ColeccionesDePrueba
    {
        public const string RelojDeApi = "RelojDeApi";
    }
}
