using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Domain.Tests.Entities;

public class PortfolioSnapshotTests
{
    private static readonly Guid Cartera = Guid.NewGuid();
    private static readonly DateOnly UnDia = new(2026, 8, 31);

    [Fact]
    public void TakesThePictureOfAPortfolio()
    {
        var foto = PortfolioSnapshot.Take(
            Cartera, UnDia, Currency.Cad, Money.Of(100000m, Currency.Cad), Money.Of(80000m, Currency.Cad), Money.Zero(Currency.Cad));

        Assert.Equal(UnDia, foto.SnapshotDate);
        Assert.Equal(Currency.Cad, foto.BaseCurrency);
        Assert.Equal(100000m, foto.MarketValue.Amount);
        Assert.Equal(80000m, foto.CostBasis.Amount);
    }

    [Fact]
    public void ComputesTheGainInsteadOfStoringIt()
    {
        // Guardarla seria tener dos verdades: si alguien corrige el valor o el coste, la ganancia
        // guardada quedaria desfasada y nadie sabria cual manda.
        var foto = PortfolioSnapshot.Take(
            Cartera, UnDia, Currency.Cad, Money.Of(100000m, Currency.Cad), Money.Of(80000m, Currency.Cad), Money.Zero(Currency.Cad));

        Assert.Equal(20000m, foto.UnrealizedPnl.Amount);
        Assert.Equal(25m, foto.Return.AsPercent);
    }

    [Fact]
    public void HandlesALoss()
    {
        var foto = PortfolioSnapshot.Take(
            Cartera, UnDia, Currency.Cad, Money.Of(75000m, Currency.Cad), Money.Of(100000m, Currency.Cad), Money.Zero(Currency.Cad));

        Assert.Equal(-25000m, foto.UnrealizedPnl.Amount);
        Assert.Equal(-25m, foto.Return.AsPercent);
    }

    [Fact]
    public void APortfolioWithoutCostHasNoPercentage()
    {
        // Devolver cero evita una division entre cero, que es lo que pasaria al calcular el porcentaje.
        var foto = PortfolioSnapshot.Take(
            Cartera, UnDia, Currency.Cad, Money.Of(1000m, Currency.Cad), Money.Zero(Currency.Cad), Money.Zero(Currency.Cad));

        Assert.True(foto.Return.Ratio == 0m);
    }

    [Fact]
    public void RefusesAmountsInAnotherCurrency()
    {
        // Guardar el numero sin que su moneda coincida con la moneda base declarada haria la foto
        // ininterpretable dentro de un año.
        var enUsd = Assert.Throws<InvalidOperationException>(() => PortfolioSnapshot.Take(
            Cartera, UnDia, Currency.Cad, Money.Of(100000m, Currency.Usd), Money.Of(80000m, Currency.Cad), Money.Zero(Currency.Cad)));

        Assert.Contains("USD", enUsd.Message, StringComparison.Ordinal);

        var costeEnUsd = Assert.Throws<InvalidOperationException>(() => PortfolioSnapshot.Take(
            Cartera, UnDia, Currency.Cad, Money.Of(100000m, Currency.Cad), Money.Of(80000m, Currency.Usd), Money.Zero(Currency.Cad)));

        Assert.Contains("USD", costeEnUsd.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsASnapshotWithoutAPortfolio()
    {
        Assert.Throws<ArgumentException>(() => PortfolioSnapshot.Take(
            Guid.Empty, UnDia, Currency.Cad, Money.Zero(Currency.Cad), Money.Zero(Currency.Cad), Money.Zero(Currency.Cad)));
    }

    [Fact]
    public void KeepsTheDateSoTheHistoryIsReadable()
    {
        var foto = PortfolioSnapshot.Take(
            Cartera, UnDia, Currency.Cad, Money.Of(1m, Currency.Cad), Money.Of(1m, Currency.Cad), Money.Zero(Currency.Cad));

        Assert.Equal(UnDia, foto.SnapshotDate);
    }
}
