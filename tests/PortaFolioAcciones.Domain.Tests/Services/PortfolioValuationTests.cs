using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.Services;
using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Domain.Tests.Services;

/// <summary>
/// La verificacion que mas importa de todo el dominio: <b>la cartera real</b>, con los numeros que
/// declara el estado de cuenta de Questrade de agosto de 2026 para la cuenta RRSP-2.
///
/// Si la agregacion no reproduce esas cifras, no sirve para nada.
/// </summary>
public class PortfolioValuationTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoy = new(2026, 8, 31);
    private static readonly Guid Cartera = Guid.NewGuid();

    /// <summary>Una posicion con un lote agregado, que es como se carga el estado de cuenta (ADR-009).</summary>
    private static Position Posicion(decimal cantidad, decimal costeUnitario, Currency moneda)
    {
        var posicion = Position.Create(Cartera, Guid.NewGuid(), Ahora);
        posicion.AddLot(
            Quantity.Of(cantidad),
            Money.Of(costeUnitario, moneda),
            new DateOnly(2026, 1, 20),
            DateAccuracy.Reference,
            null,
            Ahora);
        return posicion;
    }

    /// <summary>
    /// Las ocho posiciones en dolares de la cuenta RRSP-2, con el coste medio y el precio que
    /// declara el estado de cuenta del 2026-08-31.
    /// </summary>
    private static (List<Position> Posiciones, Dictionary<Guid, Money> Precios) CarteraReal()
    {
        var posiciones = new List<Position>();
        var precios = new Dictionary<Guid, Money>();

        void Anadir(decimal cantidad, decimal costeMedio, decimal precio)
        {
            var posicion = Posicion(cantidad, costeMedio, Currency.Usd);
            posiciones.Add(posicion);
            precios[posicion.InstrumentId] = Money.Of(precio, Currency.Usd);
        }

        // AMZN, META, MSFT, NFLX, TSLA, VEEV, MGK y VOO, con sus cifras reales.
        Anadir(31m, 176.72064516m, 259.93m);
        Anadir(8m, 648.76625m, 572.34m);
        Anadir(17m, 456.00294118m, 507.32m);
        Anadir(20m, 89.00000000m, 81.04m);
        Anadir(34m, 270.58029412m, 367.84m);
        Anadir(23m, 165.54217391m, 285.51m);
        Anadir(100m, 57.60000000m, 89.91m);
        Anadir(7m, 386.18714286m, 704.99m);

        return (posiciones, precios);
    }

    private static CurrencyConverter EnCad() =>
        new(Currency.Cad, [FxRate.Of(Currency.Usd, Currency.Cad, 1.3908m, Hoy)]);

    private static CurrencyConverter EnUsd() => new(Currency.Usd, []);

    [Fact]
    public void ReproducesTheTotalsOfTheStatement()
    {
        var (posiciones, precios) = CarteraReal();

        // La cuenta RRSP-2 esta en dolares, asi que se valora con el dolar como moneda base: es la
        // unica forma de comparar directamente con lo que declara el estado de cuenta.
        var cartera = PortfolioValuation.Of(Currency.Usd, posiciones, precios, EnUsd());

        // Las cifras que declara el estado de cuenta, en dolares.
        Assert.Equal(55881.01m, cartera.TotalValue.Round().Amount);
        Assert.Equal(41671.03m, cartera.TotalCost.Round().Amount);
        Assert.Equal(14209.98m, cartera.TotalGain.Round().Amount);
        Assert.Equal(34.10m, decimal.Round(cartera.TotalReturn.AsPercent, 2));

        Assert.Equal(8, cartera.Positions.Count);
        Assert.True(cartera.IsComplete);
        Assert.Equal(0, cartera.ClosedPositions);
    }

    [Fact]
    public void InCanadianDollarsTheSamePortfolioIsConvertedAtTheRateOfTheDay()
    {
        var (posiciones, precios) = CarteraReal();

        var cartera = PortfolioValuation.Of(Currency.Cad, posiciones, precios, EnCad());

        // 55881,01 x 1,3908
        Assert.Equal(77719.31m, cartera.TotalValue.Round().Amount);

        // Y el porcentaje no cambia, porque el tipo se cancela.
        Assert.Equal(34.10m, decimal.Round(cartera.TotalReturn.AsPercent, 2));
    }

    [Fact]
    public void OrdersPositionsByValue()
    {
        var (posiciones, precios) = CarteraReal();

        var cartera = PortfolioValuation.Of(Currency.Cad, posiciones, precios, EnCad());

        // TSLA es la mayor con diferencia: 34 x 367,84 = 12506,56.
        Assert.Equal(12506.56m, cartera.Positions[0].MarketValue.Round().Amount);

        // Y estan de mayor a menor.
        for (var i = 1; i < cartera.Positions.Count; i++)
        {
            Assert.True(cartera.Positions[i - 1].BaseMarketValue.Amount >= cartera.Positions[i].BaseMarketValue.Amount);
        }
    }

    [Fact]
    public void TheTotalReturnIsTheSameInBothCurrencies()
    {
        // Toda la cartera esta en dolares, asi que el tipo de cambio se cancela en el total igual que
        // se cancela en cada posicion. El rendimiento no depende de a como este el dolar.
        var (posiciones, precios) = CarteraReal();

        var enCad = PortfolioValuation.Of(Currency.Cad, posiciones, precios,
            new CurrencyConverter(Currency.Cad, [FxRate.Of(Currency.Usd, Currency.Cad, 1.3908m, Hoy)]));

        var enUsd = PortfolioValuation.Of(Currency.Usd, posiciones, precios,
            new CurrencyConverter(Currency.Usd, []));

        Assert.Equal(enUsd.TotalReturn.Ratio, enCad.TotalReturn.Ratio);

        // Lo que cambia es el importe, que si depende del cambio.
        Assert.NotEqual(enUsd.TotalGain.Amount, enCad.TotalGain.Amount);
    }

    [Fact]
    public void WeightsAddUpToTheWholePortfolio()
    {
        var (posiciones, precios) = CarteraReal();

        var cartera = PortfolioValuation.Of(Currency.Cad, posiciones, precios, EnCad());
        var sumaDePesos = cartera.Positions.Sum(p => cartera.WeightOf(p).Ratio);

        Assert.Equal(1m, decimal.Round(sumaDePesos, 10));
    }

    [Fact]
    public void APositionWithoutPriceStaysOutOfTheTotalAndIsReported()
    {
        // Sumarla a cero daria un total mas bajo sin que nadie lo note. Se excluye y se avisa.
        var (posiciones, precios) = CarteraReal();
        var sinPrecio = posiciones[0];
        precios.Remove(sinPrecio.InstrumentId);

        var cartera = PortfolioValuation.Of(Currency.Usd, posiciones, precios, EnUsd());

        Assert.False(cartera.IsComplete);
        Assert.Single(cartera.PositionsWithoutPrice);
        Assert.Equal(sinPrecio.Id, cartera.PositionsWithoutPrice[0]);
        Assert.Equal(7, cartera.Positions.Count);

        // Y el total baja exactamente en lo que valia esa posicion (AMZN: 31 x 259,93 = 8057,83).
        Assert.Equal(55881.01m - 8057.83m, cartera.TotalValue.Round().Amount);
    }

    [Fact]
    public void AClosedPositionIsNotAnErrorItIsJustClosed()
    {
        var (posiciones, precios) = CarteraReal();
        var vendida = posiciones[0];
        vendida.Sell(vendida.Quantity, Ahora);
        precios.Remove(vendida.InstrumentId);

        var cartera = PortfolioValuation.Of(Currency.Cad, posiciones, precios, EnCad());

        // No aparece como un problema: vender una posicion entera es lo normal.
        Assert.True(cartera.IsComplete);
        Assert.Equal(1, cartera.ClosedPositions);
        Assert.Equal(7, cartera.Positions.Count);
    }

    [Fact]
    public void WithoutAnyRateTheValuationFailsInsteadOfInventingOne()
    {
        var (posiciones, precios) = CarteraReal();
        var sinTipos = new CurrencyConverter(Currency.Cad, []);

        var error = Assert.Throws<InvalidOperationException>(
            () => PortfolioValuation.Of(Currency.Cad, posiciones, precios, sinTipos));

        Assert.Contains("USD", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptyPortfolioIsZeroAndNotAnError()
    {
        var cartera = PortfolioValuation.Of(Currency.Cad, [], new Dictionary<Guid, Money>(), EnCad());

        Assert.True(cartera.TotalValue.IsZero);
        Assert.True(cartera.TotalCost.IsZero);
        Assert.True(cartera.TotalReturn.Ratio == 0m);
        Assert.True(cartera.IsComplete);
    }
}
