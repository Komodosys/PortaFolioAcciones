using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Domain.Services;

/// <summary>
/// El resultado de valorar una cartera entera.
///
/// <b>Lo que no se puede valorar queda fuera del total, y se dice.</b> Una posicion sin precio, o con
/// una moneda cuyo tipo de cambio falta, no se suma: sumarla a cero daria un total mas bajo sin que
/// nadie lo note, y sumarla a ciegas daria uno mas alto. Se excluye y se avisa, que es lo unico
/// honesto.
///
/// Los totales se suman <b>exactos</b>. El redondeo es cosa de quien presenta, y se hace una sola vez
/// sobre la suma, no fila a fila: si se redondeara cada fila, el total no cuadraria con su propia suma.
/// </summary>
public sealed record PortfolioValuation
{
    private PortfolioValuation(
        Currency baseCurrency,
        IReadOnlyList<PositionValuation> positions,
        IReadOnlyList<Guid> positionsWithoutPrice,
        int closedPositions,
        bool isDailyComplete)
    {
        BaseCurrency = baseCurrency;
        Positions = positions;
        PositionsWithoutPrice = positionsWithoutPrice;
        ClosedPositions = closedPositions;
        IsDailyComplete = isDailyComplete;
    }

    /// <summary>Moneda en la que estan los totales.</summary>
    public Currency BaseCurrency { get; }

    /// <summary>Posiciones valoradas, ordenadas por valor de mayor a menor.</summary>
    public IReadOnlyList<PositionValuation> Positions { get; }

    /// <summary>Posiciones con unidades de las que no se pudo conseguir precio. Quedan fuera del total.</summary>
    public IReadOnlyList<Guid> PositionsWithoutPrice { get; }

    /// <summary>Cuantas posiciones estan cerradas, que no es un error: es una posicion vendida.</summary>
    public int ClosedPositions { get; }

    /// <summary>Valor de mercado total, en moneda base.</summary>
    public Money TotalValue => Sum(Positions.Select(p => p.BaseMarketValue));

    /// <summary>Coste total, en moneda base.</summary>
    public Money TotalCost => Sum(Positions.Select(p => p.BaseCost));

    /// <summary>Ganancia no realizada total, en moneda base.</summary>
    public Money TotalGain => TotalValue.Subtract(TotalCost);

    /// <summary>Rendimiento total sobre el coste.</summary>
    public Percentage TotalReturn =>
        Percentage.FromPartOverTotal(TotalGain.Amount, TotalCost.Amount);

    /// <summary>
    /// Lo que ha cambiado la cartera <b>hoy</b>, en moneda base.
    /// </summary>
    /// <remarks>
    /// Si <see cref="IsDailyComplete"/> es falso, esto es <b>solo la parte que se sabe</b> y hay que decirlo:
    /// una cifra del día incompleta presentada como el total es tan engañosa como un total incompleto, y
    /// además invita a sacar conclusiones sobre cómo ha ido la jornada.
    /// </remarks>
    public Money TotalDailyChange =>
        Sum(Positions.Select(posicion => posicion.BaseDailyChange ?? Money.Zero(BaseCurrency)));

    /// <summary>
    /// Si se conoce el cambio del día de <b>todas</b> las posiciones valoradas.
    ///
    /// Es falso en cuanto a una sola le falte el cierre anterior —una compra de hoy, un fondo que no
    /// publicó— y entonces el total del día queda a medias.
    /// </summary>
    public bool IsDailyComplete { get; }

    /// <summary>
    /// Lo que ha cambiado hoy, en tanto por ciento sobre lo que valía ayer.
    /// </summary>
    /// <remarks>
    /// <b>Nulo si el cambio del día está incompleto</b>, y no por pereza: el porcentaje se calcula sobre el
    /// valor de ayer, y si falta un trozo del cambio, ese valor de ayer también está mal. Un porcentaje
    /// calculado sobre una base equivocada es peor que no darlo.
    /// </remarks>
    public Percentage? TotalDailyReturn =>
        IsDailyComplete && !TotalDailyChange.IsZero
            ? Percentage.FromPartOverTotal(
                TotalDailyChange.Amount,
                TotalValue.Subtract(TotalDailyChange).Amount)
            : null;

    /// <summary>Dice si se pudo valorar todo. Si es falso, hay que enseñar el aviso.</summary>
    public bool IsComplete => PositionsWithoutPrice.Count == 0;

    /// <summary>Peso de cada posicion sobre el total.</summary>
    public Percentage WeightOf(PositionValuation position)
    {
        ArgumentNullException.ThrowIfNull(position);
        return Percentage.FromPartOverTotal(position.BaseMarketValue.Amount, TotalValue.Amount);
    }

    /// <summary>Valora una cartera.</summary>
    /// <param name="baseCurrency">Moneda en la que se expresan los totales.</param>
    /// <param name="positions">Posiciones de la cartera.</param>
    /// <param name="prices">
    /// Precio actual de cada instrumento, en su moneda de cotizacion. La clave es el instrumento.
    /// </param>
    /// <param name="converter">Conversor a la moneda base, con los tipos de cambio de hoy.</param>
    /// <param name="previousCloses">
    /// El cierre anterior de cada instrumento, si se conoce. Es <b>opcional</b> y no un adorno: hay
    /// posiciones cuyo cambio del día no se puede saber —una compra de hoy no tiene «ayer»— y tratarlas
    /// como si no hubieran cambiado nada daría un total del día que parece completo y no lo está.
    /// </param>
    public static PortfolioValuation Of(
        Currency baseCurrency,
        IEnumerable<Position> positions,
        IReadOnlyDictionary<Guid, Money> prices,
        CurrencyConverter converter,
        IReadOnlyDictionary<Guid, Money>? previousCloses = null)
    {
        ArgumentNullException.ThrowIfNull(baseCurrency);
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(prices);
        ArgumentNullException.ThrowIfNull(converter);

        var valued = new List<PositionValuation>();
        var withoutPrice = new List<Guid>();
        var closed = 0;
        var sinCambioDelDia = 0;

        foreach (var position in positions)
        {
            if (position.Quantity.IsZero)
            {
                closed++;
                continue;
            }

            if (!prices.TryGetValue(position.InstrumentId, out var price))
            {
                // Sin precio no se inventa nada: la posicion se queda fuera y se avisa.
                withoutPrice.Add(position.Id);
                continue;
            }

            var cierre = previousCloses?.GetValueOrDefault(position.InstrumentId);

            var valorada = PositionValuation.Of(position, price, converter, cierre);
            valued.Add(valorada);

            if (valorada.DailyChange is null)
            {
                sinCambioDelDia++;
            }
        }

        return new PortfolioValuation(
            baseCurrency,
            [.. valued.OrderByDescending(p => p.BaseMarketValue.Amount)],
            withoutPrice,
            closed,
            // Completo solo si NINGUNA se ha quedado sin su cambio del dia. Una sola que falte ya deja el
            // total del dia a medias, y decirlo es lo unico honesto.
            isDailyComplete: sinCambioDelDia == 0);
    }

    private Money Sum(IEnumerable<Money> amounts)
    {
        var total = Money.Zero(BaseCurrency);

        foreach (var amount in amounts)
        {
            total = total.Add(amount);
        }

        return total;
    }
}
