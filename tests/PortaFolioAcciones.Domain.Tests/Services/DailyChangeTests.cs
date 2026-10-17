using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.Services;
using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Domain.Tests.Services;

/// <summary>
/// Lo que ha cambiado la cartera <b>hoy</b>.
///
/// Es la cifra que más se mira y la que más fácil es de dar mal, porque <b>falta el dato casi siempre</b>:
/// una compra de hoy no tiene «ayer», y un fondo que publica cada dos días tampoco. Lo que se prueba aquí
/// es que cuando falta <b>se diga que falta</b> en vez de contar un cero, que es lo que convertiría una
/// cifra incompleta en una cifra falsa.
/// </summary>
public sealed class DailyChangeTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 16, 20, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoy = new(2026, 10, 16);
    private static readonly Guid Cartera = Guid.NewGuid();

    private static Position Posicion(decimal cantidad, Currency moneda, decimal coste = 100m)
    {
        var posicion = Position.Create(Cartera, Guid.NewGuid(), Ahora);

        posicion.AddLot(
            Quantity.Of(cantidad),
            Money.Of(coste, moneda),
            new DateOnly(2026, 1, 20),
            DateAccuracy.Reference,
            null,
            Ahora);

        return posicion;
    }

    private static CurrencyConverter EnCad(decimal tipo = 1.3908m) =>
        new(Currency.Cad, [FxRate.Of(Currency.Usd, Currency.Cad, tipo, Hoy)]);

    private static CurrencyConverter SinCambio() => new(Currency.Cad, []);

    [Fact]
    public void LaGananciaDelDiaEsLaDiferenciaConElCierreAnterior()
    {
        var posicion = Posicion(10m, Currency.Cad);

        var valorada = PositionValuation.Of(
            posicion,
            Money.Of(110m, Currency.Cad),
            SinCambio(),
            Money.Of(100m, Currency.Cad));

        // 10 unidades que han pasado de 100 a 110: cien de ganancia.
        Assert.Equal(100m, valorada.DailyChange!.Amount);
        Assert.Equal(100m, valorada.BaseDailyChange!.Amount);

        // Y el porcentaje va sobre lo que valian AYER, no sobre el precio de hoy: 100 sobre 1000.
        Assert.Equal(0.1m, valorada.DailyReturn!.Ratio);
    }

    [Fact]
    public void UnaPerdidaSaleEnNegativo()
    {
        var posicion = Posicion(10m, Currency.Cad);

        var valorada = PositionValuation.Of(
            posicion,
            Money.Of(90m, Currency.Cad),
            SinCambio(),
            Money.Of(100m, Currency.Cad));

        Assert.Equal(-100m, valorada.DailyChange!.Amount);
        Assert.Equal(-0.1m, valorada.DailyReturn!.Ratio);
    }

    [Fact]
    public void SinCierreAnteriorNoSeSabeYNoEsCero()
    {
        // Es el caso de una compra de hoy: no hay «ayer» con el que comparar.
        var posicion = Posicion(10m, Currency.Cad);

        var valorada = PositionValuation.Of(posicion, Money.Of(110m, Currency.Cad), SinCambio());

        // NULO, no cero. Un cero diria que no ha cambiado nada, y lo que pasa es que no se sabe — y esa
        // diferencia es la que evita que un total incompleto se lea como completo.
        Assert.Null(valorada.DailyChange);
        Assert.Null(valorada.BaseDailyChange);
        Assert.Null(valorada.DailyReturn);
    }

    [Fact]
    public void UnCierreAnteriorEnOtraMonedaSeIgnora()
    {
        // Seria un dato mal puesto. Restar monedas distintas daria un numero sin significado, y es mejor
        // no dar ninguno.
        var posicion = Posicion(10m, Currency.Usd);

        var valorada = PositionValuation.Of(
            posicion,
            Money.Of(110m, Currency.Usd),
            EnCad(),
            Money.Of(100m, Currency.Cad));

        Assert.Null(valorada.DailyChange);
    }

    [Fact]
    public void EnMonedaBaseSeConvierteAlCambioDeHoy()
    {
        var posicion = Posicion(10m, Currency.Usd);

        var valorada = PositionValuation.Of(
            posicion,
            Money.Of(110m, Currency.Usd),
            EnCad(),
            Money.Of(100m, Currency.Usd));

        Assert.Equal(100m, valorada.DailyChange!.Amount);

        // 100 USD por 1,3908 son 139,08 CAD. El cambio del dia es del ACTIVO; lo que se convierte es el
        // importe, que es lo unico que significa algo en otra moneda.
        Assert.Equal(139.08m, valorada.BaseDailyChange!.Amount);

        // Y el porcentaje es el mismo en las dos monedas: el tipo de cambio se cancela al dividir.
        Assert.Equal(0.1m, valorada.DailyReturn!.Ratio);
    }

    [Fact]
    public void LaCarteraSumaLosCambiosQueConoce()
    {
        var primera = Posicion(10m, Currency.Cad);
        var segunda = Posicion(5m, Currency.Cad);

        var precios = new Dictionary<Guid, Money>
        {
            [primera.InstrumentId] = Money.Of(110m, Currency.Cad),
            [segunda.InstrumentId] = Money.Of(80m, Currency.Cad),
        };

        var cierres = new Dictionary<Guid, Money>
        {
            [primera.InstrumentId] = Money.Of(100m, Currency.Cad),
            [segunda.InstrumentId] = Money.Of(100m, Currency.Cad),
        };

        var cartera = PortfolioValuation.Of(Currency.Cad, [primera, segunda], precios, SinCambio(), cierres);

        // 10 × 10 de ganancia y 5 × −20 de perdida: 100 − 100 = 0.
        Assert.Equal(0m, cartera.TotalDailyChange.Amount);
        Assert.True(cartera.IsDailyComplete);
    }

    [Fact]
    public void SiFaltaUnCierreElTotalDelDiaEsParcialYElPorcentajeNoSeDa()
    {
        var conCierre = Posicion(10m, Currency.Cad);
        var sinCierre = Posicion(10m, Currency.Cad);

        var precios = new Dictionary<Guid, Money>
        {
            [conCierre.InstrumentId] = Money.Of(110m, Currency.Cad),
            [sinCierre.InstrumentId] = Money.Of(110m, Currency.Cad),
        };

        var cierres = new Dictionary<Guid, Money>
        {
            [conCierre.InstrumentId] = Money.Of(100m, Currency.Cad),
        };

        var cartera = PortfolioValuation.Of(Currency.Cad, [conCierre, sinCierre], precios, SinCambio(), cierres);

        // El total es SOLO la parte que se sabe.
        Assert.Equal(100m, cartera.TotalDailyChange.Amount);

        // Y se dice que esta a medias: una cifra del dia incompleta presentada como el total es tan
        // engañosa como un total incompleto, y ademas invita a sacar conclusiones de la jornada.
        Assert.False(cartera.IsDailyComplete);

        // El porcentaje NO se da: se calcula sobre el valor de ayer, y ese valor tambien esta mal.
        Assert.Null(cartera.TotalDailyReturn);
    }

    [Fact]
    public void SinCierresAnterioresElTotalDelDiaNoSeInventa()
    {
        var posicion = Posicion(10m, Currency.Cad);

        var precios = new Dictionary<Guid, Money>
        {
            [posicion.InstrumentId] = Money.Of(110m, Currency.Cad),
        };

        var cartera = PortfolioValuation.Of(Currency.Cad, [posicion], precios, SinCambio());

        // Sin el parametro opcional, no hay ningun cierre anterior: ni se supone ni se inventa.
        Assert.Equal(0m, cartera.TotalDailyChange.Amount);
        Assert.False(cartera.IsDailyComplete);
        Assert.Null(cartera.TotalDailyReturn);
    }

    [Fact]
    public void ElValorDeAyerSeDeduceDelDeHoyMenosElCambio()
    {
        var posicion = Posicion(10m, Currency.Cad);

        var precios = new Dictionary<Guid, Money>
        {
            [posicion.InstrumentId] = Money.Of(110m, Currency.Cad),
        };

        var cierres = new Dictionary<Guid, Money>
        {
            [posicion.InstrumentId] = Money.Of(100m, Currency.Cad),
        };

        var cartera = PortfolioValuation.Of(Currency.Cad, [posicion], precios, SinCambio(), cierres);

        // Hoy vale 1100 y ha subido 100, asi que ayer valia 1000. El 10 % sale de ahi, y se calcula asi
        // —restando— en vez de volver a valorar con los precios de ayer, que no se tienen todos.
        Assert.Equal(1100m, cartera.TotalValue.Amount);
        Assert.Equal(0.1m, cartera.TotalDailyReturn!.Ratio);
    }
}
