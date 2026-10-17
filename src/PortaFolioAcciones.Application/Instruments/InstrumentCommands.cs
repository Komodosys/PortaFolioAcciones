namespace PortaFolioAcciones.Application.Instruments;

/// <summary>
/// Datos para dar de alta un instrumento en el catálogo.
/// </summary>
/// <param name="Symbol">Símbolo de pantalla.</param>
/// <param name="Name">Nombre legible.</param>
/// <param name="AssetType">Clase de instrumento.</param>
/// <param name="QuoteCurrency">Moneda en la que cotiza.</param>
/// <param name="Exchange">Bolsa, en código MIC.</param>
/// <param name="ValuationMode">Cómo se obtiene su precio.</param>
/// <param name="PricingFrequency">Cada cuánto cambia.</param>
/// <param name="Isin">ISIN, si tiene.</param>
/// <param name="Country">País, si se conoce.</param>
/// <param name="ProviderKey">Con qué nombre lo conoce la fuente de precios.</param>
/// <param name="BrokerCode">Con qué código lo conoce el bróker.</param>
public sealed record CreateInstrumentCommand(
    string Symbol,
    string Name,
    string AssetType,
    string QuoteCurrency,
    string Exchange,
    string ValuationMode,
    string PricingFrequency,
    string? Isin = null,
    string? Country = null,
    string? ProviderKey = null,
    string? BrokerCode = null);

/// <summary>
/// Resultado de dar de alta o modificar un instrumento.
/// </summary>
/// <param name="Succeeded">Si salió bien.</param>
/// <param name="Instrument">El instrumento, si salió bien.</param>
/// <param name="Errors">Códigos de los problemas, no textos.</param>
public sealed record InstrumentResult(
    bool Succeeded,
    InstrumentSummary? Instrument,
    IReadOnlyList<string> Errors)
{
    /// <summary>Salió bien.</summary>
    public static InstrumentResult Ok(InstrumentSummary instrument) => new(true, instrument, []);

    /// <summary>No salió, por los motivos indicados.</summary>
    public static InstrumentResult Failed(params string[] errors) => new(false, null, errors);
}
