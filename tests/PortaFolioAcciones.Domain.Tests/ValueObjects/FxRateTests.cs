using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Domain.Tests.ValueObjects;

public class FxRateTests
{
    private static readonly DateOnly UnDia = new(2026, 1, 20);

    [Fact]
    public void ConvertsAnAmountToTheTargetCurrency()
    {
        // NFLX se compro el 2026-01-20: 1780,00 USD con el USD/CAD a 1,3851.
        var rate = FxRate.Of(Currency.Usd, Currency.Cad, 1.3851m, UnDia);

        var inCad = rate.Convert(Money.Of(1780.00m, Currency.Usd));

        Assert.Equal(2465.478m, inCad.Amount);
        Assert.Equal(2465.48m, inCad.Round().Amount);
        Assert.Equal(Currency.Cad, inCad.Currency);
    }

    [Fact]
    public void RefusesToConvertAnAmountInAnotherCurrency()
    {
        var rate = FxRate.Of(Currency.Usd, Currency.Cad, 1.3851m, UnDia);
        var inEuros = Money.Of(100m, Currency.Eur);

        var error = Assert.Throws<InvalidOperationException>(() => rate.Convert(inEuros));
        Assert.Contains("EUR", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InvertingReturnsTheReciprocal()
    {
        var forward = FxRate.Of(Currency.Usd, Currency.Cad, 1.25m, UnDia);
        var backward = forward.Invert();

        Assert.Equal(Currency.Cad, backward.From);
        Assert.Equal(Currency.Usd, backward.To);
        Assert.Equal(0.8m, backward.Rate);
    }

    [Fact]
    public void ConvertingBackAndForthReturnsTheOriginalAmount()
    {
        var forward = FxRate.Of(Currency.Usd, Currency.Cad, 1.3851m, UnDia);
        var backward = forward.Invert();

        var original = Money.Of(1780.00m, Currency.Usd);
        var result = backward.Convert(forward.Convert(original));

        Assert.Equal(original.Amount, result.Amount);
        Assert.Equal(original.Currency, result.Currency);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1.5)]
    public void RejectsNonPositiveRates(decimal rate) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FxRate.Of(Currency.Usd, Currency.Cad, rate, UnDia));

    [Fact]
    public void SameCurrencyRateMustBeOne()
    {
        Assert.Throws<ArgumentException>(() =>
            FxRate.Of(Currency.Cad, Currency.Cad, 1.5m, UnDia));

        // Y con 1 es valido, que es el caso de una posicion ya en moneda base.
        var identity = FxRate.Of(Currency.Cad, Currency.Cad, 1m, UnDia);
        Assert.Equal(500m, identity.Convert(Money.Of(500m, Currency.Cad)).Amount);
    }

    [Fact]
    public void IdentityIsAlwaysOne()
    {
        var identity = FxRate.Identity(Currency.Cad, UnDia);

        Assert.Equal(1m, identity.Rate);
        Assert.Equal(100m, identity.Convert(Money.Of(100m, Currency.Cad)).Amount);
    }

    [Fact]
    public void KnowsWhichCurrencyItConvertsFrom()
    {
        var rate = FxRate.Of(Currency.Usd, Currency.Cad, 1.3851m, UnDia);

        Assert.True(rate.ConvertsFrom(Currency.Usd));
        Assert.False(rate.ConvertsFrom(Currency.Cad));
    }

    [Fact]
    public void KeepsTheDateSoTheNumberCanBeAudited()
    {
        // Sin la fecha, un tipo de cambio es un numero sin origen.
        var rate = FxRate.Of(Currency.Usd, Currency.Cad, 1.3851m, UnDia);
        Assert.Equal(UnDia, rate.RateDate);
    }
}
