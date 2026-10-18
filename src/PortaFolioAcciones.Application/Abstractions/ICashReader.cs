using PortaFolioAcciones.Application.Portfolios;

namespace PortaFolioAcciones.Application.Abstractions;

/// <summary>
/// El saldo de efectivo de una cartera.
///
/// <b>Se calcula, no se guarda.</b> Un saldo guardado es un número que puede dejar de coincidir con los
/// movimientos que lo produjeron, y entonces hay dos verdades. Calculándolo, siempre cuadra por construcción.
///
/// Lo que sí se guarda es el <b>punto de partida</b>, porque eso no se puede deducir: de las compras antiguas
/// no se sabe cuánto salió de la cuenta.
/// </summary>
public interface ICashReader
{
    /// <summary>El saldo de una cartera y de dónde sale cada parte.</summary>
    /// <param name="portfolioId">La cartera.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    /// <returns>El saldo, o <c>null</c> si la cartera no existe o no es accesible.</returns>
    public Task<CashBalanceSummary?> GetAsync(Guid portfolioId, CancellationToken cancellationToken = default);
}
