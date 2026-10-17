namespace PortaFolioAcciones.Infrastructure.MarketData.Resilience;

/// <summary>
/// Como se comporta la aplicacion con una fuente que falla.
/// </summary>
/// <remarks>
/// Los valores por omision estan pensados para las fuentes gratuitas que se usan: aguantan sin quejarse,
/// pero <b>no son infinitas y no avisan antes de cerrar el grifo</b>. De ahi que haya cuota diaria.
/// </remarks>
internal sealed record OpcionesDeResiliencia
{
    /// <summary>Cuantas veces se intenta una peticion antes de darla por perdida. La primera cuenta.</summary>
    public int IntentosMaximos { get; init; } = 3;

    /// <summary>
    /// Cuanto se espera antes del primer reintento.
    ///
    /// El segundo reintento espera el doble y el tercero el cuadruple. Esperar siempre lo mismo sirve de
    /// poco: si la fuente esta saturada, insistir cada medio segundo la satura mas.
    /// </summary>
    public TimeSpan EsperaBase { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Fallos seguidos que hacen saltar el interruptor.</summary>
    public int FallosParaAbrir { get; init; } = 5;

    /// <summary>Cuanto tiempo se queda abierto antes de dejar pasar una prueba.</summary>
    public TimeSpan DuracionAbierto { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Cuantas peticiones se permiten al dia y por fuente.
    ///
    /// Existe porque pasarse de cuota no da un error: la fuente <b>empieza a rechazar</b>, y el dia que eso
    /// pasa no hay precios para nadie. Es mejor dejar de pedir a tiempo y decirlo.
    /// </summary>
    public int LlamadasAlDia { get; init; } = 2000;

    /// <summary>Los valores pensados para las pruebas: sin esperas de verdad.</summary>
    public static OpcionesDeResiliencia ParaProbar(TimeSpan? esperaBase = null) =>
        new() { EsperaBase = esperaBase ?? TimeSpan.Zero };
}
