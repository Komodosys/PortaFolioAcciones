namespace PortaFolioAcciones.Application.Portfolios;

/// <summary>
/// Un movimiento tal como se enseña y se devuelve por la API.
/// </summary>
/// <param name="Id">Identificador.</param>
/// <param name="Kind">`Deposit`, `Withdrawal` o `Sale`.</param>
/// <param name="OccurredOn">El día del movimiento.</param>
/// <param name="Amount">El importe, ya en su moneda.</param>
/// <param name="Currency">La moneda del importe.</param>
/// <param name="Symbol">
/// El símbolo del instrumento vendido, o nulo en una aportación o un retiro. Se devuelve el símbolo y no el
/// identificador porque quien lo lee quiere ver «VEEV», no un `Guid`.
/// </param>
/// <param name="Quantity">Las unidades vendidas, o nulo.</param>
/// <param name="PricePerUnit">El precio de venta por unidad, o nulo.</param>
/// <param name="Notes">Notas.</param>
public sealed record MovementSummary(
    Guid Id,
    string Kind,
    DateOnly OccurredOn,
    decimal Amount,
    string Currency,
    string? Symbol,
    decimal? Quantity,
    decimal? PricePerUnit,
    string? Notes);
