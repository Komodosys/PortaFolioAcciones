using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Domain.Entities;

/// <summary>
/// La foto de una cartera en un dia: cuanto valia, cuanto habia costado y cuanto se ha ganado.
///
/// Se escribe <b>desde el primer dia</b>, aunque el grafico historico llegue despues. Es lo unico del
/// modelo cuyo valor depende del momento en que se empieza: la historia no se puede recuperar hacia
/// atras, porque no hay registro de operaciones del que reconstruirla. Cuesta una fila al dia por
/// cartera.
///
/// Los tres importes estan en la moneda base de la cartera, que se guarda con ellos. Guardar el numero
/// sin decir en que moneda esta lo haria ininterpretable dentro de un año.
/// </summary>
public sealed class PortfolioSnapshot
{
    private PortfolioSnapshot(
        Guid portfolioId,
        DateOnly snapshotDate,
        Currency baseCurrency,
        Money marketValue,
        Money costBasis)
    {
        PortfolioId = portfolioId;
        SnapshotDate = snapshotDate;
        BaseCurrency = baseCurrency;
        MarketValue = marketValue;
        CostBasis = costBasis;
    }

    /// <summary>Constructor sin parametros para el mapeo de la base de datos.</summary>
    private PortfolioSnapshot()
    {
        BaseCurrency = Currency.Cad;
        MarketValue = Money.Zero(Currency.Cad);
        CostBasis = Money.Zero(Currency.Cad);
    }

    /// <summary>Cartera fotografiada.</summary>
    public Guid PortfolioId { get; private set; }

    /// <summary>Dia de la foto.</summary>
    public DateOnly SnapshotDate { get; private set; }

    /// <summary>Moneda en la que estan los tres importes.</summary>
    public Currency BaseCurrency { get; private set; }

    /// <summary>Lo que valia la cartera ese dia.</summary>
    public Money MarketValue { get; private set; }

    /// <summary>Lo que habia costado.</summary>
    public Money CostBasis { get; private set; }

    /// <summary>
    /// Ganancia no realizada: valor menos coste. <b>No se guarda</b>, se calcula: guardarla seria tener
    /// dos verdades que pueden divergir si alguien corrige uno de los dos importes.
    /// </summary>
    public Money UnrealizedPnl => MarketValue.Subtract(CostBasis);

    /// <summary>
    /// Rendimiento sobre el coste. Cero si no habia coste, en vez de fallar: una cartera sin coste no
    /// tiene porcentaje.
    /// </summary>
    public Percentage Return =>
        Percentage.FromPartOverTotal(UnrealizedPnl.Amount, CostBasis.Amount);

    /// <summary>Toma la foto de una cartera.</summary>
    /// <exception cref="InvalidOperationException">Si los importes no estan en la moneda base.</exception>
    public static PortfolioSnapshot Take(
        Guid portfolioId,
        DateOnly snapshotDate,
        Currency baseCurrency,
        Money marketValue,
        Money costBasis)
    {
        ArgumentNullException.ThrowIfNull(baseCurrency);
        ArgumentNullException.ThrowIfNull(marketValue);
        ArgumentNullException.ThrowIfNull(costBasis);

        if (portfolioId == Guid.Empty)
        {
            throw new ArgumentException("Una foto tiene que ser de alguna cartera.", nameof(portfolioId));
        }

        if (marketValue.Currency != baseCurrency)
        {
            throw new InvalidOperationException(
                $"El valor de la cartera esta en {marketValue.Currency} y la moneda base de la foto es " +
                $"{baseCurrency}. Los tres importes tienen que estar en la misma moneda.");
        }

        if (costBasis.Currency != baseCurrency)
        {
            throw new InvalidOperationException(
                $"El coste esta en {costBasis.Currency} y la moneda base de la foto es {baseCurrency}.");
        }

        return new PortfolioSnapshot(portfolioId, snapshotDate, baseCurrency, marketValue, costBasis);
    }

    public override string ToString() =>
        string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{SnapshotDate:yyyy-MM-dd} valor {MarketValue}, coste {CostBasis}");
}
