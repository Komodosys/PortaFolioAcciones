namespace PortaFolioAcciones.Application.Instruments;

/// <summary>
/// Lo que se enseña de un instrumento.
/// </summary>
/// <param name="Id">Identificador.</param>
/// <param name="Symbol">Símbolo de pantalla.</param>
/// <param name="Name">Nombre legible.</param>
/// <param name="AssetType">Clase de instrumento.</param>
/// <param name="QuoteCurrency">Moneda en la que cotiza.</param>
/// <param name="Exchange">Bolsa, en código MIC.</param>
/// <param name="Isin">ISIN, si tiene. Los fondos canadienses de Fundserv <b>no tienen</b>.</param>
/// <param name="ValuationMode">Cómo se obtiene su precio.</param>
/// <param name="PricingFrequency">Cada cuánto cambia.</param>
/// <param name="ProviderKey">Con qué nombre lo conoce la fuente de precios.</param>
/// <param name="BrokerCode">Con qué código lo conoce el bróker.</param>
/// <param name="IsActive">Si sigue vivo.</param>
/// <param name="Splits">
/// Los desdoblamientos que se le conocen. Se enseñan porque <b>explican un salto del precio</b>: sin
/// saber que hubo un 5:1, una caída del 80 % de un día para otro parece una ruina, y con él se entiende
/// que no pasó nada.
/// </param>
public sealed record InstrumentSummary(
    Guid Id,
    string Symbol,
    string Name,
    string AssetType,
    string QuoteCurrency,
    string Exchange,
    string? Isin,
    string ValuationMode,
    string PricingFrequency,
    string? ProviderKey,
    string? BrokerCode,
    bool IsActive,
    IReadOnlyList<SplitSummary> Splits);

/// <summary>Un desdoblamiento, tal como se enseña.</summary>
/// <param name="Date">El día en que ocurrió.</param>
/// <param name="Ratio">Por cuánto se multiplicó la cantidad.</param>
public sealed record SplitSummary(DateOnly Date, decimal Ratio);

/// <summary>
/// Un resultado de buscar un instrumento <b>fuera</b>, en la fuente de mercado.
///
/// Fíjate en que <see cref="ProviderKey"/> y <see cref="Symbol"/> son cosas distintas: para muchos
/// fondos canadienses el nombre en la fuente es un código interno (<c>0P0001K2XD.TO</c>) que no se
/// parece en nada a lo que se enseña.
/// </summary>
/// <param name="ProviderKey">El nombre que usa la fuente para pedirle el precio.</param>
/// <param name="Symbol">El símbolo que se enseña.</param>
/// <param name="Name">Nombre legible.</param>
/// <param name="Exchange">Bolsa según la fuente, que <b>no</b> es un código MIC.</param>
/// <param name="Mic">La bolsa traducida a MIC, si se ha podido reconocer.</param>
/// <param name="AssetType">Clase de instrumento, si se ha podido deducir.</param>
/// <param name="Currency">Moneda, si la fuente la dice.</param>
/// <param name="Source">De dónde sale el resultado.</param>
public sealed record InstrumentMatch(
    string ProviderKey,
    string Symbol,
    string Name,
    string? Exchange,
    string? Mic,
    string? AssetType,
    string? Currency,
    string Source);
