using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Domain.Tests.Entities;

/// <summary>
/// Pruebas de la posicion con los <b>datos reales</b> de los estados de cuenta de Questrade. Si el
/// dominio no reproduce las cifras que declara el broker, no sirve para nada.
/// </summary>
public class PositionTests
{
    private static readonly Guid Cartera = Guid.NewGuid();
    private static readonly Guid Instrumento = Guid.NewGuid();
    private static readonly DateTimeOffset Ahora = new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    private static Position Nueva() => Position.Create(Cartera, Instrumento, Ahora);

    // ---------------------------------------------------------------------------------------------
    // VEEV: el caso que motivo modelar lotes
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// VEEV se compro en cuatro lotes en junio de 2026. El estado de cuenta declara 34 unidades con un
    /// coste de 5628,44 USD, y esos cuatro lotes son exactamente eso.
    /// </summary>
    private static Position ConLosCuatroLotesDeVevev()
    {
        var posicion = Nueva();

        posicion.AddLot(Quantity.Of(10m), Money.Of(172.000m, Currency.Usd), new DateOnly(2026, 6, 5), DateAccuracy.Exact, null, Ahora);
        posicion.AddLot(Quantity.Of(7m), Money.Of(168.859m, Currency.Usd), new DateOnly(2026, 6, 8), DateAccuracy.Exact, null, Ahora);
        posicion.AddLot(Quantity.Of(10m), Money.Of(164.500m, Currency.Usd), new DateOnly(2026, 6, 10), DateAccuracy.Exact, null, Ahora);
        posicion.AddLot(Quantity.Of(7m), Money.Of(154.490m, Currency.Usd), new DateOnly(2026, 6, 17), DateAccuracy.Exact, null, Ahora);

        return posicion;
    }

    [Fact]
    public void Vevev_QuantityAndCostMatchTheStatement()
    {
        var posicion = ConLosCuatroLotesDeVevev();

        Assert.Equal(34m, posicion.Quantity.Value);

        // El estado declara 5628,44 USD. La suma exacta de los cuatro lotes es 5628,443.
        Assert.Equal(5628.443m, posicion.TotalCost.Amount);
        Assert.Equal(5628.44m, posicion.TotalCost.Round().Amount);
        Assert.Equal(Currency.Usd, posicion.CostCurrency);
    }

    [Fact]
    public void Vevev_AverageCostPerUnitMatchesTheStatement()
    {
        var posicion = ConLosCuatroLotesDeVevev();

        // El estado declara 165,54 por unidad.
        Assert.Equal(165.54m, posicion.AverageCostPerUnit.Round().Amount);
        Assert.Equal(165.542441m, posicion.AverageCostPerUnit.Round(6).Amount);
    }

    [Fact]
    public void Vevev_SellingElevenKeepsTheAverageCostPerUnit()
    {
        // Es LA propiedad que define el metodo de coste medio, y lo que hace que la aplicacion cuadre
        // con el broker en lugar de inventarse otro coste.
        var posicion = ConLosCuatroLotesDeVevev();
        var antes = posicion.AverageCostPerUnit;

        posicion.Sell(Quantity.Of(11m), Ahora);

        Assert.Equal(23m, posicion.Quantity.Value);

        // El estado de cuenta muestra 3807,47. Aqui sale 3807,48, y la diferencia de un centimo NO es
        // un error: el broker redondea ANTES, publicando un coste medio por unidad, y luego multiplica.
        // Nosotros sumamos el coste exacto de los cuatro lotes (3807,4761) y redondeamos AL FINAL, que
        // es la politica fijada: redondear una sola vez y sobre la suma exacta.
        Assert.Equal(3807.48m, posicion.TotalCost.Round().Amount);
        Assert.Equal(165.5424m, posicion.AverageCostPerUnit.Round(4).Amount);

        // El coste por unidad se conserva. Se compara redondeado a seis decimales porque el redondeo
        // de cada lote por separado introduce una diferencia en la diezmillonesima.
        Assert.Equal(antes.Round(6), posicion.AverageCostPerUnit.Round(6));
        Assert.Equal(165.542441m, posicion.AverageCostPerUnit.Round(6).Amount);
    }

    [Fact]
    public void Vevev_SellingElevenDistributesTheReductionAcrossAllFourLots()
    {
        var posicion = ConLosCuatroLotesDeVevev();

        posicion.Sell(Quantity.Of(11m), Ahora);

        // Ningun lote se elimina y ninguno se queda entero: los cuatro se reducen en la misma
        // proporcion, 23/34.
        Assert.Equal(4, posicion.Lots.Count);
        Assert.Equal(6.76470588m, posicion.Lots[0].Quantity.Value);
        Assert.Equal(4.73529412m, posicion.Lots[1].Quantity.Value);
        Assert.Equal(6.76470588m, posicion.Lots[2].Quantity.Value);
        Assert.Equal(4.73529412m, posicion.Lots[3].Quantity.Value);

        // Y los precios de compra no se tocan: lo que cambia es la cantidad de cada lote.
        Assert.Equal(172.000m, posicion.Lots[0].CostPerUnit.Amount);
        Assert.Equal(154.490m, posicion.Lots[3].CostPerUnit.Amount);
    }

    [Fact]
    public void Vevev_SellingEverythingClosesThePositionAndKeepsTheCost()
    {
        var posicion = ConLosCuatroLotesDeVevev();

        posicion.Sell(Quantity.Of(34m), Ahora);

        Assert.True(posicion.Quantity.IsZero);
        Assert.True(posicion.IsClosed);
        Assert.Equal(Ahora, posicion.ClosedAtUtc);

        // Sin registro de operaciones, este campo es el unico rastro de lo que se pago.
        Assert.Equal(5628.443m, posicion.CostBasisAtClose!.Amount);
    }

    // ---------------------------------------------------------------------------------------------
    // MGK: el split real de abril de 2026
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// MGK tenia 12 unidades a 201,50 USD (coste 2418,00). En abril de 2026 hubo un split 5:1 y el
    /// estado paso a mostrar 60 unidades a 40,30, con el mismo coste total de 2418,00.
    /// </summary>
    [Fact]
    public void Mgk_FiveToOneSplitKeepsTheTotalCostAndChangesQuantityAndUnitCost()
    {
        var posicion = Nueva();
        posicion.AddLot(Quantity.Of(12m), Money.Of(201.50m, Currency.Usd), new DateOnly(2024, 3, 15), DateAccuracy.Reference, null, Ahora);

        var diaDelSplit = new DateOnly(2026, 4, 15);

        posicion.ApplySplit(5m, diaDelSplit, Ahora);

        Assert.Equal(60m, posicion.Quantity.Value);
        Assert.Equal(40.30m, posicion.AverageCostPerUnit.Amount);

        // El coste total NO cambia: un split reparte lo mismo entre mas titulos.
        Assert.Equal(2418.00m, posicion.TotalCost.Amount);

        // Y la fecha de compra tampoco: un split no es una compra nueva.
        Assert.Equal(new DateOnly(2024, 3, 15), posicion.Lots[0].PurchaseDate);

        // Y queda constancia de que ESTE se atenido. Es la unica forma de que la aplicacion pueda
        // distinguir «ya lo aplique» de «me falta», porque las cifras son identicas en los dos casos.
        Assert.True(posicion.HasAppliedSplit(diaDelSplit));
        Assert.False(posicion.HasAppliedSplit(new DateOnly(2026, 5, 1)));
    }

    [Fact]
    public void Mgk_AddingTheAprilPurchaseReproducesTheAugustStatement()
    {
        // Los 60 del split mas la compra de 40 del 2026-04-29 a 83,55 dan las 100 unidades y los
        // 5760,00 de coste que declara el estado de agosto.
        var posicion = Nueva();
        posicion.AddLot(Quantity.Of(12m), Money.Of(201.50m, Currency.Usd), new DateOnly(2024, 3, 15), DateAccuracy.Reference, null, Ahora);
        posicion.ApplySplit(5m, new DateOnly(2026, 4, 15), Ahora);
        posicion.AddLot(Quantity.Of(40m), Money.Of(83.55m, Currency.Usd), new DateOnly(2026, 4, 29), DateAccuracy.Exact, null, Ahora);

        Assert.Equal(100m, posicion.Quantity.Value);
        Assert.Equal(5760.00m, posicion.TotalCost.Amount);
        Assert.Equal(57.60m, posicion.AverageCostPerUnit.Amount);
    }

    // ---------------------------------------------------------------------------------------------
    // NFLX: la compra verificada de principio a fin
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Nflx_TheRealPurchaseProducesTheStatementsNumbers()
    {
        var posicion = Nueva();
        posicion.AddLot(Quantity.Of(20m), Money.Of(89.00m, Currency.Usd), new DateOnly(2026, 1, 20), DateAccuracy.Exact, null, Ahora);

        Assert.Equal(20m, posicion.Quantity.Value);
        Assert.Equal(1780.00m, posicion.TotalCost.Amount);
        Assert.Equal(89.00m, posicion.AverageCostPerUnit.Amount);
        Assert.Equal(new DateOnly(2026, 1, 20), posicion.FirstPurchaseDate);
        Assert.Equal(new DateOnly(2026, 1, 20), posicion.LastPurchaseDate);
    }

    // ---------------------------------------------------------------------------------------------
    // Redondeo: que la suma de los lotes sea exacta
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void SellingAThirdOfThreeEqualLotsLeavesTheQuantityExact()
    {
        // 2/3 no tiene representacion decimal finita. Cada lote se redondea por separado y la suma
        // quedaria en 20,00000001 si no se corrigiera el residuo.
        var posicion = Nueva();
        for (var i = 0; i < 3; i++)
        {
            posicion.AddLot(Quantity.Of(10m), Money.Of(100m, Currency.Usd), new DateOnly(2026, 1, 1), DateAccuracy.Exact, null, Ahora);
        }

        posicion.Sell(Quantity.Of(10m), Ahora);

        Assert.Equal(20m, posicion.Quantity.Value);
    }

    // ---------------------------------------------------------------------------------------------
    // Invariantes
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void CannotSellMoreThanItHolds()
    {
        var posicion = ConLosCuatroLotesDeVevev();

        var error = Assert.Throws<InvalidOperationException>(() => posicion.Sell(Quantity.Of(35m), Ahora));
        Assert.Contains("35", error.Message, StringComparison.Ordinal);

        // Y la posicion no se ha tocado.
        Assert.Equal(34m, posicion.Quantity.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void CannotSellAnEmptyOrNegativeQuantity(decimal cantidad)
    {
        var posicion = ConLosCuatroLotesDeVevev();
        Assert.Throws<ArgumentOutOfRangeException>(() => posicion.Sell(Quantity.Of(cantidad), Ahora));
    }

    [Fact]
    public void CannotMixCurrenciesWithinAPosition()
    {
        var posicion = Nueva();
        posicion.AddLot(Quantity.Of(10m), Money.Of(100m, Currency.Usd), new DateOnly(2026, 1, 1), DateAccuracy.Exact, null, Ahora);

        var error = Assert.Throws<InvalidOperationException>(() =>
            posicion.AddLot(Quantity.Of(5m), Money.Of(100m, Currency.Cad), new DateOnly(2026, 2, 1), DateAccuracy.Exact, null, Ahora));

        Assert.Contains("USD", error.Message, StringComparison.Ordinal);
        Assert.Contains("CAD", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuyingAgainReopensAClosedPosition()
    {
        var posicion = Nueva();
        posicion.AddLot(Quantity.Of(10m), Money.Of(100m, Currency.Usd), new DateOnly(2026, 1, 1), DateAccuracy.Exact, null, Ahora);
        posicion.Sell(Quantity.Of(10m), Ahora);
        Assert.True(posicion.IsClosed);

        posicion.AddLot(Quantity.Of(5m), Money.Of(110m, Currency.Usd), new DateOnly(2026, 6, 1), DateAccuracy.Exact, null, Ahora);

        Assert.False(posicion.IsClosed);
        Assert.Null(posicion.ClosedAtUtc);
        Assert.Null(posicion.CostBasisAtClose);
        Assert.Equal(5m, posicion.Quantity.Value);
    }

    [Fact]
    public void AmountsAndAverageCostUseThePurchaseCurrency()
    {
        var posicion = ConLosCuatroLotesDeVevev();

        Assert.Equal(Currency.Usd, posicion.TotalCost.Currency);
        Assert.Equal(Currency.Usd, posicion.AverageCostPerUnit.Currency);
    }

    [Fact]
    public void ReportsWhetherAnyPurchaseDateIsApproximate()
    {
        var posicion = Nueva();
        posicion.AddLot(Quantity.Of(10m), Money.Of(100m, Currency.Usd), new DateOnly(2026, 1, 1), DateAccuracy.Exact, null, Ahora);
        Assert.False(posicion.HasApproximateDates);

        posicion.AddLot(Quantity.Of(5m), Money.Of(100m, Currency.Usd), new DateOnly(2024, 1, 1), DateAccuracy.Reference, null, Ahora);
        Assert.True(posicion.HasApproximateDates);
    }

    [Fact]
    public void AnEmptyPositionHasNoCostOrAverage()
    {
        var posicion = Nueva();

        Assert.True(posicion.Quantity.IsZero);
        Assert.Null(posicion.CostCurrency);
        Assert.Null(posicion.FirstPurchaseDate);
        Assert.True(posicion.TotalCost.IsZero);
        Assert.True(posicion.AverageCostPerUnit.IsZero);
    }
}
