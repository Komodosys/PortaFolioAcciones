namespace PortaFolioAcciones.Application.Portfolios;

/// <summary>
/// El saldo de efectivo de una cartera y de dónde sale cada parte.
/// </summary>
/// <param name="PortfolioId">La cartera.</param>
/// <param name="Currency">La moneda de todas las cifras: la base de la cartera.</param>
/// <param name="Opening">Lo declarado como punto de partida.</param>
/// <param name="Deposits">Lo que ha entrado desde la apertura.</param>
/// <param name="Withdrawals">Lo que ha salido desde la apertura.</param>
/// <param name="Sales">Lo cobrado en las ventas desde la apertura.</param>
/// <param name="Purchases">Lo pagado en las compras desde la apertura.</param>
/// <param name="Balance">Apertura + aportaciones − retiros + ventas − compras.</param>
/// <param name="Since">El día de la apertura. Nulo si no la hay.</param>
/// <param name="Unconverted">
/// Cuántos movimientos no se han podido convertir porque no se guardó su tipo. Si es mayor que cero, el saldo
/// <b>no está completo</b> y se dice: darlo como bueno sería peor que no darlo.
/// </param>
/// <remarks>
/// <b>Lo anterior a la apertura no se resta.</b> Los lotes y las ventas de antes están incorporados en lo que
/// se declaró, no recalculados: de las compras antiguas no se sabe cuánto salió de la cuenta, y recalcularlas
/// con una estimación daría un saldo con aspecto de exacto y sin serlo.
/// </remarks>
public sealed record CashBalanceSummary(
    Guid PortfolioId,
    string Currency,
    decimal Opening,
    decimal Deposits,
    decimal Withdrawals,
    decimal Sales,
    decimal Purchases,
    decimal Balance,
    DateOnly? Since,
    int Unconverted)
{
    /// <summary>Dice si el saldo se puede dar como bueno.</summary>
    public bool IsKnown => Since is not null && Unconverted == 0;
}
