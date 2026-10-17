namespace PortaFolioAcciones.Application.MarketData;

/// <summary>
/// Lo que hizo la recoleccion de precios de un dia.
/// </summary>
/// <param name="Instrumentos">Cuantos instrumentos hacian falta.</param>
/// <param name="Guardados">Cuantos se guardaron.</param>
/// <param name="YaEstaban">Cuantos ya estaban recogidos: el job es idempotente.</param>
/// <param name="SinPrecio">Cuantos no tenian precio en la fuente.</param>
/// <param name="Monedas">Las monedas para las que se pidieron tipos de cambio.</param>
/// <param name="Fotos">Cuantas fotos de cartera se tomaron.</param>
/// <param name="Carteras">Cuantas carteras habia que fotografiar.</param>
/// <remarks>
/// Se devuelve un resumen y no un «vale» porque quien ejecuta el job necesita saber <b>que paso</b>: si no
/// se guardo nada porque no hacia falta es una cosa, y si no se guardo nada porque la fuente no responde
/// es otra muy distinta. Un job que solo dice «termine» no permite distinguirlas.
/// </remarks>
public sealed record DailyCollectionResult(
    int Instrumentos,
    int Guardados,
    int YaEstaban,
    int SinPrecio,
    IReadOnlyList<string> Monedas,
    int Carteras,
    int Fotos)
{
    /// <summary>Si no habia nada que hacer.</summary>
    public bool NoHabiaNada => Instrumentos == 0;
}
