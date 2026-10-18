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
/// <param name="Balance">
/// Apertura + aportaciones − retiros + traspasos recibidos − traspasos enviados + ventas + dividendos −
/// comisiones − compras.
/// </param>
/// <param name="Since">El día de la apertura. Nulo si no la hay.</param>
/// <param name="BalanceInUsd">
/// El saldo llevado a dolares estadounidenses al cambio de hoy. **Solo informativo**: no se guarda ni interviene
/// en ningun calculo. Nulo si la cartera ya esta en dolares o si no hay tipo de cambio.
/// </param>
/// <param name="UsdRate">
/// El cambio usado para esa vista, 1 USD = X de la moneda de la cartera. Se ensena al lado para que la cifra
/// no parezca magia. Nulo si no se pudo calcular.
/// </param>
/// <param name="Unconverted">
/// Cuántos movimientos no se han podido convertir porque no se guardó su tipo. Si es mayor que cero, el saldo
/// <b>no está completo</b> y se dice: darlo como bueno sería peor que no darlo.
/// </param>
/// <param name="TransfersIn">
/// Lo que ha entrado por traspaso desde otra cuenta del mismo titular. <b>No está dentro de <paramref name="Deposits"/></b>
/// porque no es una aportación: en una cuenta de jubilación una aportación consume espacio de cotización y un
/// traspaso no.
/// </param>
/// <param name="TransfersOut">Lo que ha salido hacia otra cuenta del mismo titular. No es un retiro.</param>
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
    int Unconverted,
    decimal Dividends = 0m,
    decimal Commissions = 0m,
    decimal? BalanceInUsd = null,
    decimal? UsdRate = null,
    decimal TransfersIn = 0m,
    decimal TransfersOut = 0m)
{
    /// <summary>Dice si el saldo se puede dar como bueno.</summary>
    public bool IsKnown => Since is not null && Unconverted == 0;
}
