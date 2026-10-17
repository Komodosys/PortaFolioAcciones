using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Domain.Tests.Entities;

/// <summary>
/// Los instrumentos de estas pruebas son los que aparecen de verdad en los estados de cuenta.
/// </summary>
public class InstrumentTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreatesAnAmericanEquity()
    {
        var instrument = Instrument.Create(
            Ticker.From("VEEV"), "VEEVA SYSTEMS INC CL A COM", AssetType.Equity,
            Currency.Usd, "XNYS", ValuationMode.Market, PricingFrequency.EndOfDay, Ahora,
            country: "US");

        Assert.Equal("VEEV", instrument.Symbol.Value);
        Assert.Equal(AssetType.Equity, instrument.AssetType);
        Assert.Equal("USD", instrument.QuoteCurrency.Code);
        Assert.True(instrument.IsActive);
        Assert.False(instrument.IsFund);
        Assert.False(instrument.RequiresManualPrice);
    }

    /// <summary>
    /// El caso que obligo a separar los tres identificadores. Un fondo de TD distribuido por Fundserv:
    /// el broker lo llama TDB2624C, la fuente de precios lo conoce como 0P0001K312.TO, y <b>no tiene
    /// ISIN</b>.
    /// </summary>
    [Fact]
    public void KeepsBrokerCodeAndProviderKeyApart()
    {
        var fondo = Instrument.Create(
            Ticker.From("TDB2624C"), "TD ACT CDN POOL-W/SF", AssetType.MutualFund,
            Currency.Cad, "FUNDSERV", ValuationMode.Nav, PricingFrequency.DailyNav, Ahora);

        // Al principio no se sabe con que nombre lo conoce la fuente, asi que no se le puede pedir precio.
        Assert.Null(fondo.ProviderKey);
        Assert.False(fondo.CanBeQuotedFromProvider);
        Assert.Null(fondo.Isin);

        fondo.SetBrokerCode("TDB2624C", Ahora);
        fondo.SetProviderKey("0P0001K312.TO", Ahora);

        // Sigue sin poder pedirse hasta que se resuelve la equivalencia... y ahora si.
        Assert.Equal("TDB2624C", fondo.BrokerCode);
        Assert.Equal("0P0001K312.TO", fondo.ProviderKey);
        Assert.True(fondo.CanBeQuotedFromProvider);
        Assert.True(fondo.IsFund);

        // Y no se le exige un ISIN, porque no existe para este tipo de fondo.
        Assert.Null(fondo.Isin);
    }

    [Fact]
    public void AFundWithoutProviderKeyCannotBeQuoted()
    {
        var fondo = Instrument.Create(
            Ticker.From("TDB2560C"), "TD PL REV FIX-W /SF", AssetType.MutualFund,
            Currency.Cad, "FUNDSERV", ValuationMode.Nav, PricingFrequency.DailyNav, Ahora);

        // Este es el estado en el que hay que avisar al usuario: sin equivalencia resuelta, no hay precio.
        Assert.False(fondo.CanBeQuotedFromProvider);

        fondo.SetProviderKey("0P0001K2XD.TO", Ahora);

        Assert.True(fondo.CanBeQuotedFromProvider);
    }

    [Fact]
    public void ABondWithManualValuationIsNeverQuoted()
    {
        var bono = Instrument.Create(
            Ticker.From("BONO2028"), "BONO CORPORATIVO 2028", AssetType.Bond,
            Currency.Cad, "OTC", ValuationMode.Manual, PricingFrequency.Manual, Ahora);

        Assert.True(bono.RequiresManualPrice);

        // Aunque alguien le ponga una clave de proveedor por error, un instrumento manual no se consulta.
        bono.SetProviderKey("lo-que-sea", Ahora);
        Assert.False(bono.CanBeQuotedFromProvider);
    }

    [Fact]
    public void TheSameEtfInTwoMarketsAreTwoInstruments()
    {
        // Es una decision explicita del modelo: bolsa y moneda forman parte de la identidad. Fusionarlos
        // por simbolo provoca colisiones reales entre bolsas.
        var enToronto = Instrument.Create(
            Ticker.From("VFV"), "VANGUARD S&P 500 INDEX ETF", AssetType.Etf,
            Currency.Cad, "XTSE", ValuationMode.Market, PricingFrequency.EndOfDay, Ahora);

        var enNuevaYork = Instrument.Create(
            Ticker.From("VOO"), "VANGUARD S&P 500 ETF", AssetType.Etf,
            Currency.Usd, "PCX", ValuationMode.Market, PricingFrequency.EndOfDay, Ahora);

        Assert.NotEqual(enToronto.Id, enNuevaYork.Id);
        Assert.NotEqual(enToronto.Exchange, enNuevaYork.Exchange);
        Assert.NotEqual(enToronto.QuoteCurrency, enNuevaYork.QuoteCurrency);
    }

    [Theory]
    [InlineData("US0378331005")]  // Apple: el ejemplo canonico de ISIN valido.
    [InlineData("us0378331005")]  // Se normaliza a mayusculas.
    [InlineData("US0378331005 ")] // Y se recortan los espacios.
    public void AcceptsAValidIsin(string isin)
    {
        var instrument = Instrument.Create(
            Ticker.From("AAPL"), "APPLE INC", AssetType.Equity,
            Currency.Usd, "XNAS", ValuationMode.Market, PricingFrequency.EndOfDay, Ahora);

        instrument.SetIsin(isin, Ahora);

        Assert.Equal("US0378331005", instrument.Isin);
    }

    [Theory]
    [InlineData("US0378331004")]   // Digito de control cambiado: es el error que se comete al teclear.
    [InlineData("US037833100X")]   // Duodecimo caracter que no cierra la cuenta.
    [InlineData("US037833100")]    // Once caracteres.
    [InlineData("US03783310055")]  // Trece.
    public void RejectsAnIsinThatIsMalformedOrWhoseCheckDigitDoesNotAddUp(string isin)
    {
        var instrument = Instrument.Create(
            Ticker.From("AAPL"), "APPLE INC", AssetType.Equity,
            Currency.Usd, "XNAS", ValuationMode.Market, PricingFrequency.EndOfDay, Ahora);

        Assert.Throws<ArgumentException>(() => instrument.SetIsin(isin, Ahora));

        // Y el instrumento se queda como estaba: no se guarda un dato a medias.
        Assert.Null(instrument.Isin);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsAnEmptyName(string nombre) =>
        Assert.ThrowsAny<ArgumentException>(() => Instrument.Create(
            Ticker.From("X"), nombre, AssetType.Equity, Currency.Usd, "XNYS",
            ValuationMode.Market, PricingFrequency.EndOfDay, Ahora));

    [Fact]
    public void RejectsAnEmptyExchange()
    {
        // La bolsa forma parte de la identidad del instrumento, asi que no puede faltar.
        Assert.ThrowsAny<ArgumentException>(() => Instrument.Create(
            Ticker.From("X"), "Algo", AssetType.Equity, Currency.Usd, "  ",
            ValuationMode.Market, PricingFrequency.EndOfDay, Ahora));
    }

    [Fact]
    public void DeactivatingDoesNotDelete()
    {
        var instrument = Instrument.Create(
            Ticker.From("MGK"), "VANGUARD WORLD FUNDS VANGUARD", AssetType.Etf,
            Currency.Usd, "PCX", ValuationMode.Market, PricingFrequency.EndOfDay, Ahora);

        instrument.Deactivate(Ahora);

        Assert.False(instrument.IsActive);
        Assert.Equal(AssetType.Etf, instrument.AssetType);
        Assert.Equal("MGK", instrument.Symbol.Value);

        instrument.Activate(Ahora);

        Assert.True(instrument.IsActive);
    }

    [Fact]
    public void UpdatesTheModificationDateButNotTheCreationDate()
    {
        var instrument = Instrument.Create(
            Ticker.From("VFV"), "VANGUARD S&P 500 INDEX ETF TR", AssetType.Etf,
            Currency.Cad, "XTSE", ValuationMode.Market, PricingFrequency.EndOfDay, Ahora);

        var despues = Ahora.AddDays(3);
        instrument.Rename("VANGUARD S&P 500 INDEX ETF", despues);

        Assert.Equal(Ahora, instrument.CreatedAtUtc);
        Assert.Equal(despues, instrument.UpdatedAtUtc);
    }
}
