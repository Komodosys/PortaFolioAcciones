using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Domain.Tests.Entities;

public class PriceTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 12, 22, 0, 0, TimeSpan.Zero);
    private static readonly Guid Instrumento = Guid.NewGuid();
    private static readonly DateOnly UnDia = new(2026, 10, 9);

    [Fact]
    public void CreatesADailyClose()
    {
        // El cierre real de VEEV el 2026-10-09.
        var precio = Price.Create(Instrumento, UnDia, Money.Of(295.85m, Currency.Usd), "yahoo", Ahora);

        Assert.Equal(UnDia, precio.PriceDate);
        Assert.Equal(295.85m, precio.Close.Amount);
        Assert.Equal(Currency.Usd, precio.Currency);
        Assert.Equal("yahoo", precio.Source);
    }

    [Fact]
    public void KeepsTheDateAndTheSourceSoTheNumberCanBeAudited()
    {
        // Con fondos (valor liquidativo de ayer) y acciones (cierre de hoy) mezclados, decir de cuando
        // es cada numero no es un detalle estetico.
        var precio = Price.Create(Instrumento, UnDia, Money.Of(19.84m, Currency.Cad), "yahoo", Ahora);

        Assert.Equal(UnDia, precio.PriceDate);
        Assert.Equal("yahoo", precio.Source);
        Assert.Equal(Ahora, precio.RetrievedAtUtc);
    }

    [Fact]
    public void RevisesTheSameDayWithoutChangingTheRow()
    {
        var precio = Price.Create(Instrumento, UnDia, Money.Of(295.85m, Currency.Usd), "fuente-a", Ahora);
        var despues = Ahora.AddHours(2);

        precio.Revise(Money.Of(296.10m, Currency.Usd), "fuente-b", despues);

        Assert.Equal(296.10m, precio.Close.Amount);
        Assert.Equal("fuente-b", precio.Source);
        Assert.Equal(despues, precio.RetrievedAtUtc);

        // La fecha y el instrumento no cambian: sigue siendo la misma fila.
        Assert.Equal(UnDia, precio.PriceDate);
        Assert.Equal(Instrumento, precio.InstrumentId);
    }

    [Fact]
    public void RefusesToReviseWithAnotherCurrency()
    {
        var precio = Price.Create(Instrumento, UnDia, Money.Of(295.85m, Currency.Usd), "yahoo", Ahora);

        var error = Assert.Throws<InvalidOperationException>(() =>
            precio.Revise(Money.Of(400m, Currency.Cad), "otra", Ahora));

        Assert.Contains("USD", error.Message, StringComparison.Ordinal);
        Assert.Contains("CAD", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1.5)]
    public void RejectsANegativePrice(decimal importe)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Price.Create(Instrumento, UnDia, Money.Of(importe, Currency.Usd), "yahoo", Ahora));
    }

    [Fact]
    public void RejectsAPriceWithoutAnInstrument()
    {
        Assert.Throws<ArgumentException>(() =>
            Price.Create(Guid.Empty, UnDia, Money.Of(1m, Currency.Usd), "yahoo", Ahora));
    }

    [Fact]
    public void RejectsAPriceWithoutASource()
    {
        // Un precio sin origen es un numero que no se puede auditar.
        Assert.ThrowsAny<ArgumentException>(() =>
            Price.Create(Instrumento, UnDia, Money.Of(1m, Currency.Usd), "  ", Ahora));
    }
}

public class LatestQuoteTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 12, 22, 0, 0, TimeSpan.Zero);
    private static readonly Guid Instrumento = Guid.NewGuid();

    private static LatestQuote Nueva() => LatestQuote.Create(
        Instrumento, Money.Of(295.85m, Currency.Usd), Ahora.AddMinutes(-15), "yahoo", Ahora, delayMinutes: 15);

    [Fact]
    public void CreatesAQuoteWithItsDelay()
    {
        var cotizacion = Nueva();

        Assert.Equal(295.85m, cotizacion.Price.Amount);
        Assert.Equal(Currency.Usd, cotizacion.Currency);
        Assert.Equal(15, cotizacion.DelayMinutes);
        Assert.True(cotizacion.IsDelayed);
    }

    [Fact]
    public void SeparatesWhenThePriceIsFromAndWhenItWasFetched()
    {
        // Un fondo puede publicar hoy el valor liquidativo de ayer. La fecha que importa es la del dato.
        var cotizacion = Nueva();

        Assert.Equal(Ahora.AddMinutes(-15), cotizacion.AsOfUtc);
        Assert.Equal(Ahora, cotizacion.RetrievedAtUtc);
        Assert.NotEqual(cotizacion.AsOfUtc, cotizacion.RetrievedAtUtc);
    }

    [Fact]
    public void AcceptsANewerPrice()
    {
        var cotizacion = Nueva();
        cotizacion.Update(
            Money.Of(296.10m, Currency.Usd), Ahora, "yahoo", Ahora.AddMinutes(1), 15);

        Assert.Equal(296.10m, cotizacion.Price.Amount);
        Assert.Equal(Ahora, cotizacion.AsOfUtc);
    }

    [Fact]
    public void RefusesAnOlderPrice()
    {
        // Es el unico error que esta tabla puede cometer de verdad: si dos peticiones se cruzan, la que
        // llega tarde con un dato viejo no puede pisar uno mas reciente.
        var cotizacion = Nueva();

        var aceptado = cotizacion.Update(
            Money.Of(100m, Currency.Usd), Ahora.AddHours(-2), "yahoo", Ahora.AddMinutes(1), 15);

        Assert.False(aceptado);
        Assert.Equal(295.85m, cotizacion.Price.Amount);
        Assert.Equal(Ahora.AddMinutes(-15), cotizacion.AsOfUtc);
    }

    [Fact]
    public void AcceptsAPriceFromTheSameInstant()
    {
        // A igualdad de momento, si se acepta: puede ser la misma fuente corrigiendo el dato.
        var cotizacion = Nueva();

        var aceptado = cotizacion.Update(
            Money.Of(295.90m, Currency.Usd), Ahora.AddMinutes(-15), "otra", Ahora.AddMinutes(1), 15);

        Assert.True(aceptado);
        Assert.Equal(295.90m, cotizacion.Price.Amount);
    }

    [Fact]
    public void RefusesToUpdateWithAnotherCurrency()
    {
        var cotizacion = Nueva();

        Assert.Throws<InvalidOperationException>(() =>
            cotizacion.Update(Money.Of(400m, Currency.Cad), Ahora, "otra", Ahora));
    }

    [Fact]
    public void DetectsStalePrices()
    {
        var cotizacion = Nueva();

        // Un cierre de hace dos dias esta viejo para un precio intradia.
        Assert.True(cotizacion.IsStale(Ahora.AddDays(2), TimeSpan.FromHours(1)));
        Assert.False(cotizacion.IsStale(Ahora, TimeSpan.FromHours(1)));
    }

    [Fact]
    public void RejectsANegativeDelay()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LatestQuote.Create(
            Instrumento, Money.Of(1m, Currency.Usd), Ahora, "yahoo", Ahora, delayMinutes: -5));
    }

    [Fact]
    public void AQuoteWithoutDelayIsNotDelayed()
    {
        var cotizacion = LatestQuote.Create(
            Instrumento, Money.Of(1m, Currency.Usd), Ahora, "yahoo", Ahora);

        Assert.Null(cotizacion.DelayMinutes);
        Assert.False(cotizacion.IsDelayed);
    }
}
