namespace PortaFolioAcciones.Application.Positions;

/// <summary>
/// Un lote tal como se enseña.
/// </summary>
/// <param name="Id">Identificador.</param>
/// <param name="Quantity">Cantidad que queda del lote.</param>
/// <param name="CostPerUnit">Lo que costó cada unidad.</param>
/// <param name="PurchaseDate">Fecha de compra.</param>
/// <param name="DateAccuracy">Si la fecha es exacta o estimada.</param>
/// <param name="Notes">Notas.</param>
public sealed record LotSummary(
    Guid Id,
    decimal Quantity,
    decimal CostPerUnit,
    DateOnly PurchaseDate,
    string DateAccuracy,
    string? Notes);

/// <summary>
/// Una posición con todo lo que hace falta para pintarla.
///
/// Los importes van <b>en la moneda en la que se compró</b>, no convertidos: la conversión a la moneda
/// base depende de un tipo de cambio que cambia a diario, y mezclarla aquí haría que el mismo dato
/// significara cosas distintas según cuándo se pidiera.
/// </summary>
/// <param name="Id">Identificador.</param>
/// <param name="PortfolioId">Cartera a la que pertenece.</param>
/// <param name="InstrumentId">Instrumento.</param>
/// <param name="Symbol">Símbolo del instrumento.</param>
/// <param name="InstrumentName">Nombre del instrumento.</param>
/// <param name="Quantity">Cantidad total, suma de los lotes.</param>
/// <param name="AverageCostPerUnit">Coste medio por unidad.</param>
/// <param name="CostCurrency">Moneda en la que se compró.</param>
/// <param name="TotalCost">Coste total.</param>
/// <param name="IsClosed">Si está cerrada.</param>
/// <param name="ClosedAtUtc">Cuándo se cerró.</param>
/// <param name="ManualPriceAmount">Precio manual, si lo tiene.</param>
/// <param name="ManualPriceCurrency">Moneda de ese precio.</param>
/// <param name="ManualPriceDate">Fecha de ese precio.</param>
/// <param name="Notes">Notas.</param>
/// <param name="HasApproximateDates">Si algún lote tiene la fecha estimada.</param>
/// <param name="Lots">Los lotes, del más antiguo al más reciente.</param>
/// <remarks>
/// <b>No lleva de quién es la cartera, y es a propósito.</b> Eso es contexto de la <b>cuenta</b>, no de la
/// posición, y la pantalla lo pide aparte. Meterlo aquí obligaría a cargar el dueño en las once llamadas
/// que construyen este detalle —incluida una que es una proyección de una consulta, que no puede ser
/// asíncrona— para un dato que la pantalla necesita una sola vez.
/// </remarks>
public sealed record PositionDetail(
    Guid Id,
    Guid PortfolioId,
    Guid InstrumentId,
    string Symbol,
    string InstrumentName,
    decimal Quantity,
    decimal AverageCostPerUnit,
    string CostCurrency,
    decimal TotalCost,
    bool IsClosed,
    DateTimeOffset? ClosedAtUtc,
    decimal? ManualPriceAmount,
    string? ManualPriceCurrency,
    DateOnly? ManualPriceDate,
    string? Notes,
    bool HasApproximateDates,
    IReadOnlyList<LotSummary> Lots);
