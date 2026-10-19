namespace PortaFolioAcciones.Application.Portfolios;

/// <summary>
/// El saldo de <b>una</b> de las bolsas de moneda de una cartera.
/// </summary>
/// <param name="Currency">La moneda de esa bolsa.</param>
/// <param name="Amount">Lo que hay en ella, en su moneda.</param>
/// <param name="Rate">El cambio usado para llevarla a la moneda de la cartera. Nulo si no se pudo.</param>
/// <param name="AmountInBase">
/// Lo que vale esa bolsa en la moneda de la cartera **al cambio de hoy**. Nulo si no hay tipo.
/// </param>
/// <remarks>
/// <b>Una cuenta puede tener dos bolsas, y el saldo no es un número: son dos.</b> El total se obtiene llevando
/// cada una al cambio de hoy, que es lo que hace el bróker cuando informa de un saldo combinado. Sumarlas al tipo
/// de cada movimiento —que es lo que hacía antes el lector— mezcla dos monedas y da un total que no cuadra con
/// ninguno de los dos saldos reales.
/// </remarks>
public sealed record CashBalanceByCurrency(
    string Currency,
    decimal Amount,
    decimal? Rate,
    decimal? AmountInBase);
