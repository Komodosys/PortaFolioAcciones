namespace PortaFolioAcciones.Application.MarketData;

/// <summary>
/// Lo que ha pasado al rellenar el histórico de tipos de cambio.
/// </summary>
/// <param name="Pares">Cuántos pares de monedas se han intentado.</param>
/// <param name="Nuevos">Cuántos días se han añadido.</param>
/// <param name="YaEstaban">Cuántos ya estaban guardados.</param>
/// <param name="SinSerie">
/// De cuántos pares no se ha obtenido nada. Puede ser que la fuente no publique ese par, o que el rango no
/// tenga datos; se dice, en vez de contarlo como cero.
/// </param>
public sealed record FxBackfillResult(int Pares, int Nuevos, int YaEstaban, int SinSerie);