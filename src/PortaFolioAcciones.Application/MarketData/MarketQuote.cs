using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Application.MarketData;

/// <summary>
/// Un desdoblamiento declarado por la fuente de precios.
/// </summary>
/// <param name="Date">Cuándo ocurrió.</param>
/// <param name="Ratio">Por cada unidad, cuántas resultan. Un 5:1 da 5.</param>
/// <param name="Label">Como lo declara la fuente, por ejemplo «5:1».</param>
public sealed record SplitEvent(DateTimeOffset Date, decimal Ratio, string? Label);

/// <summary>
/// Un dividendo declarado por la fuente.
/// </summary>
/// <param name="Date">Cuándo se pagó.</param>
/// <param name="Amount">Cuánto por unidad, en la moneda del instrumento.</param>
public sealed record DividendEvent(DateTimeOffset Date, decimal Amount);

/// <summary>
/// Un precio conseguido de una fuente de mercado.
///
/// Lleva <b>el precio y cuándo es de</b>, porque las dos cosas importan: un precio de hace tres días
/// presentado sin decir cuándo es, parece el de hoy, y eso es mentir sobre la frescura del dato.
/// </summary>
/// <param name="Price">El precio, en la moneda del instrumento.</param>
/// <param name="AsOfUtc">El instante al que corresponde el precio.</param>
/// <param name="PriceDate">La fecha (sin hora) del precio.</param>
/// <param name="Splits">Desdoblamientos detectados en el periodo.</param>
/// <param name="Dividends">Dividendos detectados en el periodo.</param>
/// <param name="Source">De qué fuente salió, para poder decirlo.</param>
/// <param name="IsFallback">
/// Dice si es el <b>último precio conocido</b> que se guardó, y no uno recién pedido. Se dice porque la
/// diferencia importa: un precio guardado puede tener días, y quien lo mire tiene derecho a saber que la
/// fuente no respondió.
/// </param>
public sealed record MarketQuote(
    Money Price,
    DateTimeOffset AsOfUtc,
    DateOnly PriceDate,
    IReadOnlyList<SplitEvent> Splits,
    IReadOnlyList<DividendEvent> Dividends,
    string Source,
    bool IsFallback = false);
