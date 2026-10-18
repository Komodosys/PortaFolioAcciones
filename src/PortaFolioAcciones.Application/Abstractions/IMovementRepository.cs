using PortaFolioAcciones.Application.Portfolios;

namespace PortaFolioAcciones.Application.Abstractions;

/// <summary>
/// Los movimientos de una cartera: dinero que entra, dinero que sale y ventas.
///
/// <b>Las compras no están aquí.</b> Una compra es un <c>Lot</c>, con su cantidad, su coste y su fecha, y el
/// registro completo de una cartera es la unión de los dos. Duplicarla habría sido tener el mismo hecho en dos
/// sitios, y el día que uno se corrigiera sin el otro dejarían de cuadrar sin que nada fallara.
/// </summary>
/// <remarks>
/// <b>El aislamiento no lo pone este repositorio.</b> Un movimiento no tiene dueño propio: lo hereda de su
/// cartera. Por eso quien llama tiene que haber comprobado antes que la cartera es accesible, y ese es el
/// único motivo de que estas operaciones reciban el identificador de la cartera y no el del movimiento.
/// </remarks>
public interface IMovementRepository
{
    /// <summary>Los movimientos de una cartera, del más reciente al más antiguo.</summary>
    /// <param name="portfolioId">La cartera.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    public Task<IReadOnlyList<MovementSummary>> ListAsync(
        Guid portfolioId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra una aportación o un retiro.
    /// </summary>
    /// <param name="portfolioId">La cartera.</param>
    /// <param name="kind">`Deposit` o `Withdrawal`.</param>
    /// <param name="amount">El importe, positivo en los dos casos: el signo lo pone el tipo.</param>
    /// <param name="currency">La moneda del importe.</param>
    /// <param name="occurredOn">El día.</param>
    /// <param name="notes">Notas.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    /// <exception cref="ArgumentException">Si la moneda no tiene forma de código.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si el importe no es positivo.</exception>
    public Task<MovementSummary> AddCashAsync(
        Guid portfolioId,
        string kind,
        decimal amount,
        string currency,
        DateOnly occurredOn,
        string? notes,
        CancellationToken cancellationToken = default);
}
