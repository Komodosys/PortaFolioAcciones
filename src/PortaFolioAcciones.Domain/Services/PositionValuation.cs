using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Domain.Services;

/// <summary>
/// El resultado de valorar una posicion: cuanto vale, cuanto costo y cuanto se ha ganado.
///
/// Se calcula <b>una sola vez y en un solo sitio</b>. La pantalla no recalcula nada: si cada sitio
/// hiciera su cuenta, dos pantallas podrian dar cifras distintas de lo mismo.
/// </summary>
public sealed record PositionValuation
{
    private PositionValuation(
        Guid positionId,
        Guid instrumentId,
        Quantity quantity,
        Money price,
        Money marketValue,
        Money cost,
        Money baseMarketValue,
        Money baseCost,
        Money? previousClose,
        Money? dailyChange,
        Money? baseDailyChange)
    {
        PositionId = positionId;
        InstrumentId = instrumentId;
        Quantity = quantity;
        Price = price;
        MarketValue = marketValue;
        Cost = cost;
        BaseMarketValue = baseMarketValue;
        BaseCost = baseCost;
        PreviousClose = previousClose;
        DailyChange = dailyChange;
        BaseDailyChange = baseDailyChange;
    }

    public Guid PositionId { get; }

    public Guid InstrumentId { get; }

    public Quantity Quantity { get; }

    /// <summary>Precio actual, en la moneda en que cotiza el instrumento.</summary>
    public Money Price { get; }

    /// <summary>Valor de mercado en la moneda del precio.</summary>
    public Money MarketValue { get; }

    /// <summary>Coste en la moneda en que se compro.</summary>
    public Money Cost { get; }

    /// <summary>Valor de mercado convertido a la moneda base, al cambio de hoy.</summary>
    public Money BaseMarketValue { get; }

    /// <summary>Coste convertido a la moneda base, al cambio de hoy.</summary>
    public Money BaseCost { get; }

    /// <summary>Ganancia no realizada en moneda base.</summary>
    public Money BaseGain => BaseMarketValue.Subtract(BaseCost);

    /// <summary>
    /// Rendimiento en moneda base. Es <b>el mismo numero</b> que el rendimiento del activo en su
    /// moneda, porque el tipo de cambio se cancela al dividir
    /// ([ADR-011](../../../DOCUMENTACION/decisiones/ADR-011-coste-al-cambio-de-hoy.md)).
    /// </summary>
    public Percentage BaseReturn =>
        Percentage.FromPartOverTotal(BaseGain.Amount, BaseCost.Amount);

    /// <summary>
    /// Ganancia en la moneda del activo. Es exacta y no depende de ningun tipo de cambio.
    /// Nula cuando el precio y el coste estan en monedas distintas: ahi no hay un rendimiento «local»
    /// que signifique nada.
    /// </summary>
    public Money? LocalGain =>
        Price.Currency == Cost.Currency ? MarketValue.Subtract(Cost) : null;

    /// <summary>Rendimiento en la moneda del activo.</summary>
    public Percentage? LocalReturn =>
        LocalGain is { } gain ? Percentage.FromPartOverTotal(gain.Amount, Cost.Amount) : null;

    /// <summary>
    /// El cierre anterior, si se conoce. Nulo cuando no hay: <b>no se supone</b> que el precio de ayer fuera
    /// el de hoy.
    /// </summary>
    public Money? PreviousClose { get; }

    /// <summary>
    /// Lo que ha cambiado hoy, en la moneda del precio. Nulo si no se sabe el cierre anterior.
    /// </summary>
    /// <remarks>
    /// Es el precio de hoy menos el de ayer, multiplicado por las unidades. <b>Nulo no es cero</b>: una
    /// posición cuyo cierre anterior no se conoce no ha cambiado cero, es que no se sabe, y contarla como
    /// cero haría que el total del día pareciera completo cuando le falta un trozo.
    /// </remarks>
    public Money? DailyChange { get; }

    /// <summary>Lo mismo, en la moneda base.</summary>
    public Money? BaseDailyChange { get; }

    /// <summary>
    /// Lo que ha cambiado hoy, en tanto por ciento sobre el valor de ayer.
    ///
    /// Es <b>el mismo número</b> en la moneda del activo y en la base, porque el tipo de cambio se cancela
    /// al dividir — igual que pasa con <see cref="BaseReturn"/> y
    /// [ADR-011](../../../DOCUMENTACION/decisiones/ADR-011-coste-al-cambio-de-hoy.md).
    /// </summary>
    public Percentage? DailyReturn =>
        DailyChange is { } cambio && PreviousClose is not null
            ? Percentage.FromPartOverTotal(cambio.Amount, Quantity.Value * PreviousClose.Amount)
            : null;

    /// <summary>
    /// Valora una posicion.
    /// </summary>
    /// <param name="position">La posicion.</param>
    /// <param name="price">Precio actual, en la moneda en que cotiza el instrumento.</param>
    /// <param name="converter">Conversor a la moneda base, con los tipos de cambio de hoy.</param>
    /// <param name="previousClose">
    /// El cierre anterior, si se conoce. Va <b>aparte del precio</b> y no se deduce de él: para una posición
    /// recién comprada no hay cierre anterior que valga, y para un fondo que publica cada dos días tampoco.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Si el precio no esta en la moneda del instrumento, o si falta el tipo de cambio.
    /// </exception>
    public static PositionValuation Of(
        Position position,
        Money price,
        CurrencyConverter converter,
        Money? previousClose = null)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(price);
        ArgumentNullException.ThrowIfNull(converter);

        var quantity = position.Quantity;

        if (quantity.IsZero)
        {
            throw new InvalidOperationException(
                "No se valora una posicion sin unidades: eso es una posicion cerrada, y no tiene " +
                "valor de mercado.");
        }

        var marketValue = quantity * price;

        // El cierre anterior solo sirve si esta en la misma moneda que el precio. Si no, es un dato mal
        // puesto, y restar monedas distintas daria un numero sin significado.
        var cierreUtil = previousClose is { } cierre && cierre.Currency == price.Currency ? cierre : null;

        var valorDeAyer = cierreUtil is null ? null : quantity * cierreUtil;
        var cambio = valorDeAyer is null ? null : marketValue.Subtract(valorDeAyer);

        return new PositionValuation(
            position.Id,
            position.InstrumentId,
            quantity,
            price,
            marketValue,
            position.TotalCost,
            converter.ToBase(marketValue),
            converter.ToBase(position.TotalCost),
            cierreUtil,
            cambio,
            cambio is null ? null : converter.ToBase(cambio));
    }
}
