using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.Services;
using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Domain.Tests.Services;

public class CurrencyConverterTests
{
    private static readonly DateOnly Hoy = new(2026, 10, 12);

    private static CurrencyConverter ConCadComBase(params FxRate[] tipos) =>
        new(Currency.Cad, tipos);

    [Fact]
    public void ConvertsUsingTheRateOfTheDay()
    {
        var conversor = ConCadComBase(FxRate.Of(Currency.Usd, Currency.Cad, 1.4245m, Hoy));

        var enCad = conversor.ToBase(Money.Of(1780.00m, Currency.Usd));

        Assert.Equal(2535.61m, enCad.Amount);
        Assert.Equal(Currency.Cad, enCad.Currency);
    }

    [Fact]
    public void AnAmountAlreadyInTheBaseCurrencyIsLeftAlone()
    {
        var conversor = ConCadComBase();

        Assert.Equal(500m, conversor.ToBase(Money.Of(500m, Currency.Cad)).Amount);
        Assert.True(conversor.CanConvert(Currency.Cad));
    }

    [Fact]
    public void UsesTheInverseRateWhenThereIsNoDirectOne()
    {
        // La fuente publica CAD/USD; para pasar de USD a CAD hay que darle la vuelta.
        var conversor = ConCadComBase(FxRate.Of(Currency.Cad, Currency.Usd, 0.70m, Hoy));

        var enCad = conversor.ToBase(Money.Of(100m, Currency.Usd));

        // 100 / 0,70 = 142,857...
        Assert.Equal(142.85714285714285714285714286m, enCad.Amount);
    }

    [Fact]
    public void PrefersTheDirectRateOverTheInverse()
    {
        var conversor = ConCadComBase(
            FxRate.Of(Currency.Usd, Currency.Cad, 1.4245m, Hoy),
            FxRate.Of(Currency.Cad, Currency.Usd, 0.70m, Hoy));

        // Con los dos disponibles manda el directo: no se invierte por gusto, porque invertir pierde
        // precision y ademas deja de coincidir con lo que publico la fuente.
        Assert.Equal(142.45m, conversor.ToBase(Money.Of(100m, Currency.Usd)).Amount);
    }

    [Fact]
    public void NeverInventsARateOfOne()
    {
        // Es la regla que gobierna todo esto: antes un error que un numero falso.
        var conversor = ConCadComBase();

        Assert.False(conversor.CanConvert(Currency.Usd));
        Assert.False(conversor.TryToBase(Money.Of(100m, Currency.Usd), out _));

        var error = Assert.Throws<InvalidOperationException>(
            () => conversor.ToBase(Money.Of(100m, Currency.Usd)));

        Assert.Contains("USD", error.Message, StringComparison.Ordinal);
        Assert.Contains("1:1", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsTheDateOfTheRateItWouldUse()
    {
        // Sin la fecha, un tipo de cambio es un numero sin origen.
        var ayer = Hoy.AddDays(-1);
        var conversor = ConCadComBase(FxRate.Of(Currency.Usd, Currency.Cad, 1.4245m, ayer));

        Assert.Equal(ayer, conversor.RateDateFor(Currency.Usd));

        // Y para la moneda base no hay tipo que dar: no hace falta ninguna conversion.
        Assert.Null(conversor.RateDateFor(Currency.Cad));
    }

    [Fact]
    public void ReturnsNullDateWhenThePairIsMissing()
    {
        var conversor = ConCadComBase();
        Assert.Null(conversor.RateDateFor(Currency.Eur));
    }
}

public class PositionValuationTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoy = new(2026, 10, 12);

    private static Position Posicion(Quantity cantidad, decimal costeUnitario, Currency moneda) =>
        Position.Create(Guid.NewGuid(), Guid.NewGuid(), Ahora) is var p
        && AddLot(p, cantidad, costeUnitario, moneda) is var _
            ? p : p;

    private static Lot AddLot(Position p, Quantity cantidad, decimal costeUnitario, Currency moneda) =>
        p.AddLot(cantidad, Money.Of(costeUnitario, moneda), new DateOnly(2026, 1, 20), DateAccuracy.Exact, null, Ahora);

    [Fact]
    public void ValuesTheRealNflxPurchase()
    {
        // NFLX: 20 acciones compradas el 2026-01-20 a 89,00 USD. El 2026-10-12 cotiza a 70,61.
        var posicion = Posicion(Quantity.Of(20m), 89.00m, Currency.Usd);
        var conversor = new CurrencyConverter(Currency.Cad, [FxRate.Of(Currency.Usd, Currency.Cad, 1.4245m, Hoy)]);

        var valoracion = PositionValuation.Of(posicion, Money.Of(70.61m, Currency.Usd), conversor);

        Assert.Equal(1412.20m, valoracion.MarketValue.Amount);
        Assert.Equal(1780.00m, valoracion.Cost.Amount);

        // En dolares: -20,66 %.
        Assert.Equal(-367.80m, valoracion.LocalGain!.Amount);
        Assert.Equal(-20.66m, decimal.Round(valoracion.LocalReturn!.AsPercent, 2));

        // Y en dolares canadienses, al cambio de hoy.
        Assert.Equal(2011.68m, valoracion.BaseMarketValue.Round().Amount);
        Assert.Equal(2535.61m, valoracion.BaseCost.Round().Amount);
        Assert.Equal(-523.93m, valoracion.BaseGain.Round().Amount);
    }

    [Fact]
    public void TheBaseReturnAndTheLocalReturnAreTheSameNumber()
    {
        // Es LA consecuencia del ADR-011: al convertir coste y valor con el mismo tipo de cambio, el
        // tipo se cancela al dividir. El rendimiento que se muestra es el del activo, se lea en la
        // moneda que se lea.
        var posicion = Posicion(Quantity.Of(20m), 89.00m, Currency.Usd);
        var conversor = new CurrencyConverter(Currency.Cad, [FxRate.Of(Currency.Usd, Currency.Cad, 1.4245m, Hoy)]);

        var valoracion = PositionValuation.Of(posicion, Money.Of(70.61m, Currency.Usd), conversor);

        Assert.Equal(valoracion.LocalReturn!.Ratio, valoracion.BaseReturn.Ratio);
    }

    [Fact]
    public void TheSameHappensWithAGain()
    {
        // VEEV: 23 acciones con un coste de 165,5424 USD, cotizando a 285,51.
        var posicion = Posicion(Quantity.Of(23m), 165.5424m, Currency.Usd);
        var conversor = new CurrencyConverter(Currency.Cad, [FxRate.Of(Currency.Usd, Currency.Cad, 1.4245m, Hoy)]);

        var valoracion = PositionValuation.Of(posicion, Money.Of(285.51m, Currency.Usd), conversor);

        Assert.True(valoracion.LocalGain!.Amount > 0m);
        Assert.Equal(valoracion.LocalReturn!.Ratio, valoracion.BaseReturn.Ratio);
    }

    [Fact]
    public void ConvertingAtTheRateOfTheDayIsWhatMakesThemMatch()
    {
        // Y la comprobacion que cierra el circulo: el MISMO porcentaje sale con cualquier tipo de
        // cambio, porque no depende de el.
        var posicion = Posicion(Quantity.Of(20m), 89.00m, Currency.Usd);

        var conCambioBajo = PositionValuation.Of(
            posicion, Money.Of(70.61m, Currency.Usd),
            new CurrencyConverter(Currency.Cad, [FxRate.Of(Currency.Usd, Currency.Cad, 1.30m, Hoy)]));

        var conCambioAlto = PositionValuation.Of(
            posicion, Money.Of(70.61m, Currency.Usd),
            new CurrencyConverter(Currency.Cad, [FxRate.Of(Currency.Usd, Currency.Cad, 1.50m, Hoy)]));

        Assert.Equal(conCambioBajo.BaseReturn.Ratio, conCambioAlto.BaseReturn.Ratio);

        // Lo que cambia es el importe en dolares canadienses, que si depende del cambio.
        Assert.NotEqual(conCambioBajo.BaseGain.Amount, conCambioAlto.BaseGain.Amount);
    }

    [Fact]
    public void WhenCurrenciesDifferThereIsNoLocalReturn()
    {
        // Un instrumento cotizado en USD comprado en CAD no tiene un rendimiento «local» que signifique
        // nada: se dice que no lo hay, en vez de inventarse uno.
        var posicion = Posicion(Quantity.Of(10m), 100m, Currency.Cad);
        var conPrecioEnUsd = Position.Create(Guid.NewGuid(), posicion.InstrumentId, Ahora);

        // Se construye una posicion cuyo coste esta en CAD y se valora con un precio en USD.
        var conversor = new CurrencyConverter(
            Currency.Cad, [FxRate.Of(Currency.Usd, Currency.Cad, 1.4245m, Hoy)]);

        var valoracion = PositionValuation.Of(posicion, Money.Of(150m, Currency.Usd), conversor);

        Assert.Null(valoracion.LocalGain);
        Assert.Null(valoracion.LocalReturn);

        // Pero en moneda base si hay cifra: el precio se convierte y el coste ya estaba en CAD.
        // 10 x 150 USD = 1500 USD, por 1,4245 son 2136,75 CAD.
        Assert.Equal(2136.75m, valoracion.BaseMarketValue.Amount);
        Assert.Equal(1000m, valoracion.BaseCost.Amount);
        Assert.Equal(1.13675m, valoracion.BaseReturn.Ratio);
    }

    [Fact]
    public void RefusesToValueAClosedPosition()
    {
        var posicion = Posicion(Quantity.Of(10m), 100m, Currency.Usd);
        posicion.Sell(Quantity.Of(10m), Ahora);

        var conversor = new CurrencyConverter(Currency.Cad, [FxRate.Of(Currency.Usd, Currency.Cad, 1.4m, Hoy)]);

        var error = Assert.Throws<InvalidOperationException>(() =>
            PositionValuation.Of(posicion, Money.Of(150m, Currency.Usd), conversor));

        Assert.Contains("cerrada", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
