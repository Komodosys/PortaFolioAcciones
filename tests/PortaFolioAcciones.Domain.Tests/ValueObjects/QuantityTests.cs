using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Domain.Tests.ValueObjects;

public class QuantityTests
{
    [Fact]
    public void AcceptsFractionalFundUnits()
    {
        // Un fondo de TD tiene 2393,827 participaciones en el estado de cuenta real.
        var quantity = Quantity.Of(2393.827m);
        Assert.Equal(2393.827m, quantity.Value);
    }

    [Fact]
    public void AcceptsUpToEightDecimals() =>
        Assert.Equal(0.12345678m, Quantity.Of(0.12345678m).Value);

    [Fact]
    public void RejectsMoreThanEightDecimals() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Quantity.Of(0.123456789m));

    [Fact]
    public void MultipliedByPriceGivesAnAmount()
    {
        // NFLX: 20 acciones a 89,00 USD. Es la compra real del 2026-01-20.
        var quantity = Quantity.Of(20m);
        var precio = Money.Of(89.00m, Currency.Usd);

        var amount = quantity * precio;

        Assert.Equal(1780.00m, amount.Amount);
        Assert.Equal(Currency.Usd, amount.Currency);
    }

    [Fact]
    public void MultipliesWithFullPrecision()
    {
        // VEEV: 10 participaciones al precio real de la operacion del 2026-06-08.
        var amount = Quantity.Of(7m) * Money.Of(168.859m, Currency.Usd);
        Assert.Equal(1182.013m, amount.Amount);
    }

    [Fact]
    public void SellsAreNegativeQuantities()
    {
        var sale = Quantity.Of(-11m);

        Assert.True(sale.IsNegative);
        Assert.Equal(11m, sale.Negate().Value);
    }

    [Fact]
    public void ZeroIsAClosedPosition()
    {
        Assert.True(Quantity.Zero.IsZero);
        Assert.False(Quantity.Of(0.00000001m).IsZero);
    }

    [Fact]
    public void SumsQuantities() =>
        Assert.Equal(34m, Quantity.Of(10m).Add(Quantity.Of(24m)).Value);

    [Fact]
    public void ProportionAndRemainingFactorAreComplements()
    {
        // VEEV: se venden 11 de 34.
        var total = Quantity.Of(34m);
        var sold = Quantity.Of(11m);

        // La proporcion que representa lo vendido sobre el total: 11/34.
        Assert.Equal(11m / 34m, total.ProportionOf(sold));

        // Y el factor por el que hay que multiplicar TODOS los lotes para que la cantidad que queda sea
        // la correcta: 23/34. Son complementarios, y confundirlos repartiria la venta al reves.
        Assert.Equal(23m / 34m, total.RemainingFactorAfterSelling(sold));
    }

    [Fact]
    public void SellingEverythingLeavesNothing() =>
        Assert.Equal(0m, Quantity.Of(34m).RemainingFactorAfterSelling(Quantity.Of(34m)));

    [Fact]
    public void SellingNothingChangesNothing() =>
        Assert.Equal(1m, Quantity.Of(34m).RemainingFactorAfterSelling(Quantity.Zero));

    [Fact]
    public void ProportionOfSomethingOverZeroIsZero()
    {
        // Sin cantidad no hay proporcion posible. Se devuelve cero en vez de fallar: una posicion
        // cerrada no tiene proporcion que repartir.
        Assert.Equal(0m, Quantity.Zero.ProportionOf(Quantity.Of(5m)));
    }

    [Fact]
    public void EqualityIsByValue()
    {
        Assert.Equal(Quantity.Of(10m), Quantity.Of(10m));
        Assert.NotEqual(Quantity.Of(10m), Quantity.Of(10.00000001m));
    }
}
