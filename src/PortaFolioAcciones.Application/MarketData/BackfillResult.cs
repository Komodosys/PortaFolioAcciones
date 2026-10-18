namespace PortaFolioAcciones.Application.MarketData;

/// <summary>
/// Lo que ha pasado al rellenar el histórico de precios.
/// </summary>
/// <param name="Instrumentos">Cuántos instrumentos se han intentado.</param>
/// <param name="Nuevos">Cuántos cierres se han añadido.</param>
/// <param name="YaEstaban">
/// Cuántos ya estaban guardados. **No es un fallo**: rellenar dos veces es normal y el relleno no
/// sobrescribe, porque un cierre que ya está se comprobó cuando se guardó.
/// </param>
/// <param name="SinSerie">
/// De cuántos no se ha obtenido ninguna serie. La fuente puede no conocer el valor —los fondos canadienses
/// de Fundserv no tienen código de mercado— y en ese caso se dice, en vez de contarlo como cero.
/// </param>
public sealed record BackfillResult(int Instrumentos, int Nuevos, int YaEstaban, int SinSerie);
