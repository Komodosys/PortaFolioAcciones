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
    /// <summary>
    /// Corrige la apertura de una cartera, o la crea si no habia.
    ///
    /// **Una sola apertura.** Declarar otra no se suma a la anterior: la reemplaza. Dos aperturas del mismo
    /// dinero lo contarian dos veces, y el saldo saldria al doble. Las que sobraran se retiran.
    /// </summary>
    /// <returns>La apertura corregida, o <c>null</c> si el importe no vale.</returns>
    public Task<MovementSummary?> CorrectOpeningAsync(
        Guid portfolioId,
        decimal amount,
        string currency,
        DateOnly occurredOn,
        string? notes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Borra un movimiento anotado por error.
    ///
    /// **Una venta no se borra.** Una venta es el registro de la reduccion de una posicion: borrarla dejaria la
    /// posicion vendida y sin venta, y la ganancia realizada saldria mal para siempre.
    /// </summary>
    /// <returns>
    /// <c>null</c> si no existe, y <see cref="MovementDeleteResult"/> con el motivo cuando no se puede borrar.
    /// </returns>
    public Task<MovimientoBorrado> DeleteAsync(
        Guid portfolioId,
        Guid movementId,
        CancellationToken cancellationToken = default);

    public Task<MovementSummary> AddCashAsync(
        Guid portfolioId,
        string kind,
        decimal amount,
        string currency,
        DateOnly occurredOn,
        string? notes,
        CancellationToken cancellationToken = default);
}
