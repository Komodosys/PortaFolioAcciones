using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Domain.Tests.ValueObjects;

/// <summary>
/// El dinero es lo que mas facil se estropea, asi que estas pruebas cubren sobre todo lo que NO se
/// puede hacer: mezclar monedas y redondear sin querer.
/// </summary>
public class MoneyTests
{
    [Fact]
    public void CannotAddAmountsInDifferentCurrencies()
    {
        // 1.700 CAD y 1.700 USD no son la misma cantidad de dinero, y sumarlos daria un numero que no
        // significa nada. La conversion tiene que ser un paso explicito.
        var inCad = Money.Of(1700m, Currency.Cad);
        var inUsd = Money.Of(1700m, Currency.Usd);

        var error = Assert.Throws<InvalidOperationException>(() => inCad.Add(inUsd));

        Assert.Contains("CAD", error.Message, StringComparison.Ordinal);
        Assert.Contains("USD", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CannotSubtractOrCompareAmountsInDifferentCurrencies()
    {
        var inCad = Money.Of(100m, Currency.Cad);
        var inUsd = Money.Of(100m, Currency.Usd);

        Assert.Throws<InvalidOperationException>(() => inCad.Subtract(inUsd));
        Assert.Throws<InvalidOperationException>(() => inCad.CompareTo(inUsd));
        Assert.Throws<InvalidOperationException>(() => inCad < inUsd);
    }

    [Fact]
    public void AddsAndSubtractsWithinTheSameCurrency()
    {
        var a = Money.Of(1250.55m, Currency.Cad);
        var b = Money.Of(2550.10m, Currency.Cad);

        Assert.Equal(3800.65m, (a + b).Amount);
        Assert.Equal(-1299.55m, (a - b).Amount);
        Assert.Equal(Currency.Cad, (a + b).Currency);
    }

    [Fact]
    public void DoesNotRoundOnItsOwn()
    {
        // El redondeo solo ocurre cuando se pide. Si ocurriera solo, los totales dejarian de cuadrar
        // con la suma de las filas.
        var result = Money.Of(10m, Currency.Cad).DivideBy(3m);

        Assert.Equal(3.3333333333333333333333333333m, result.Amount);
    }

    [Fact]
    public void RoundsOnlyWhenAsked()
    {
        var amount = Money.Of(3.3333333333333333333333333333m, Currency.Cad);

        Assert.Equal(3.33m, amount.Round().Amount);
        Assert.Equal(3.3333m, amount.Round(4).Amount);
    }

    [Theory]
    // Redondeo al par (el que usa .NET para decimal y el habitual en finanzas): en el punto medio
    // exacto, va al digito par. Evita el sesgo de redondear siempre hacia arriba.
    // Se pide explicitamente 0 decimales: con el valor por defecto se usan los de la moneda (2 en CAD)
    // y 2,5 ya tiene menos decimales de los que se piden, asi que se quedaria igual.
    [InlineData(2.5, 2)]
    [InlineData(3.5, 4)]
    [InlineData(0.5, 0)]
    [InlineData(-2.5, -2)]
    public void RoundsHalfToEvenWhenAskedForWholeUnits(decimal input, decimal expected) =>
        Assert.Equal(expected, Money.Of(input, Currency.Cad).Round(0).Amount);

    [Theory]
    [InlineData(2.345, 2.34)]
    [InlineData(2.355, 2.36)]
    public void RoundsToTheCurrencyDecimalsByDefault(decimal input, decimal expected) =>
        Assert.Equal(expected, Money.Of(input, Currency.Cad).Round().Amount);

    [Theory]
    [InlineData("CAD", 2)]
    [InlineData("USD", 2)]
    [InlineData("EUR", 2)]
    [InlineData("JPY", 0)]   // El yen no tiene subunidad en circulacion.
    [InlineData("KRW", 0)]   // El won tampoco.
    [InlineData("KWD", 3)]   // El dinar kuwaiti usa tres decimales.
    public void KnowsHowManyDecimalsEachCurrencyUses(string code, int expectedDecimals) =>
        Assert.Equal(expectedDecimals, Money.DecimalsFor(Currency.FromCode(code)));

    [Fact]
    public void JpyShowsNoDecimalsWhenRounded()
    {
        // 1.500 yenes no son 1.500,00 yenes: no existe la centesima parte del yen.
        Assert.Equal(1500m, Money.Of(1500.4m, Currency.FromCode("JPY")).Round().Amount);
        Assert.Equal(1500m, Money.Of(1499.6m, Currency.FromCode("JPY")).Round().Amount);
    }

    [Fact]
    public void EqualityNeedsBothAmountAndCurrency()
    {
        Assert.Equal(Money.Of(100m, Currency.Cad), Money.Of(100m, Currency.Cad));
        Assert.NotEqual(Money.Of(100m, Currency.Cad), Money.Of(100m, Currency.Usd));
        Assert.NotEqual(Money.Of(100m, Currency.Cad), Money.Of(100.01m, Currency.Cad));
    }

    [Fact]
    public void CannotDivideByZero()
    {
        var amount = Money.Of(100m, Currency.Cad);
        Assert.Throws<DivideByZeroException>(() => amount.DivideBy(0m));
    }

    [Fact]
    public void ReportsZeroAndNegative()
    {
        Assert.True(Money.Zero(Currency.Cad).IsZero);
        Assert.True(Money.Of(-1m, Currency.Cad).IsNegative);
        Assert.False(Money.Of(0.0001m, Currency.Cad).IsZero);
    }
}
