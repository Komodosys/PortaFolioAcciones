using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Domain.Tests.ValueObjects;

/// <summary>
/// El porcentaje se guarda como proporcion. Estas pruebas existen sobre todo para dejar claro cual de
/// las dos formas se esta usando en cada construccion: confundirlas es un error de un factor 100, que
/// es de los que no se ven a simple vista.
/// </summary>
public class PercentageTests
{
    [Fact]
    public void BuildsFromRatioOrFromPercent()
    {
        var fromRatio = Percentage.FromRatio(0.1575m);
        var fromPercent = Percentage.FromPercent(15.75m);

        Assert.Equal(fromRatio, fromPercent);
        Assert.Equal(0.1575m, fromRatio.Ratio);
        Assert.Equal(15.75m, fromRatio.AsPercent);
    }

    [Fact]
    public void AppliesToAnAmount()
    {
        // VEEV tiene un rendimiento real del 78,72 % sobre un coste de 3807,47 USD.
        var cost = Money.Of(3807.47m, Currency.Usd);
        var performance = Percentage.FromPercent(78.72m);

        Assert.Equal(2997.240384m, performance.ApplyTo(cost).Amount);
    }

    [Fact]
    public void PartOverTotalGivesTheWeightOfAPosition()
    {
        // VEEV pesa 16,45 % de su cartera segun el estado de agosto de 2026.
        var weight = Percentage.FromPartOverTotal(9707.34m, 59013.29m);

        Assert.Equal(16.45m, decimal.Round(weight.AsPercent, 2));
    }

    [Fact]
    public void PartOverZeroTotalIsZero()
    {
        // Una posicion sin coste no tiene porcentaje. Devolver cero evita una division entre cero.
        Assert.Equal(Percentage.Zero, Percentage.FromPartOverTotal(100m, 0m));
    }

    [Fact]
    public void LossesAreNegative()
    {
        // NFLX tiene un rendimiento real del -20,66 %.
        var performance = Percentage.FromPercent(-20.66m);

        Assert.Equal(-0.2066m, performance.Ratio);
        Assert.True(performance.Ratio < 0m);
    }

    [Fact]
    public void ZeroIsZeroInBothForms()
    {
        Assert.Equal(0m, Percentage.Zero.Ratio);
        Assert.Equal(0m, Percentage.Zero.AsPercent);
    }

    [Fact]
    public void EqualityIsByValue()
    {
        Assert.Equal(Percentage.FromPercent(15m), Percentage.FromRatio(0.15m));
        Assert.NotEqual(Percentage.FromPercent(15m), Percentage.FromRatio(15m));
    }
}
