using Microsoft.EntityFrameworkCore;
using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Application.Portfolios;
using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Infrastructure.Persistence.Repositories;

/// <summary>
/// Los movimientos de las carteras.
///
/// <b>No comprueba el acceso a la cartera</b> y no es un olvido: un movimiento no tiene dueño propio, lo
/// hereda de su cartera, así que el filtro global —que actúa sobre las carteras— no se le puede aplicar. Quien
/// llama comprueba la cartera primero, con el repositorio de carteras, que sí lleva las dos barreras.
/// </summary>
internal sealed class MovementRepository : IMovementRepository
{
    private readonly PortafolioDbContext _context;

    public MovementRepository(PortafolioDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<MovementSummary>> ListAsync(
        Guid portfolioId,
        CancellationToken cancellationToken = default)
    {
        var movimientos = await _context.Movements
            .AsNoTracking()
            .Where(movimiento => movimiento.PortfolioId == portfolioId)
            .OrderByDescending(movimiento => movimiento.OccurredOn)
            .ThenByDescending(movimiento => movimiento.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        // Los símbolos de las ventas, en UNA consulta. Pedirlos movimiento a movimiento serían decenas de
        // viajes para pintar una lista.
        var posiciones = movimientos
            .Where(movimiento => movimiento.PositionId is not null)
            .Select(movimiento => movimiento.PositionId!.Value)
            .Distinct()
            .ToList();

        var simbolos = posiciones.Count == 0
            ? new Dictionary<Guid, string>()
            : await _context.Positions
                .AsNoTracking()
                .Where(posicion => posiciones.Contains(posicion.Id))
                .Join(
                    _context.Instruments.AsNoTracking(),
                    posicion => posicion.InstrumentId,
                    instrumento => instrumento.Id,
                    (posicion, instrumento) => new { posicion.Id, Simbolo = instrumento.Symbol.Value })
                .ToDictionaryAsync(fila => fila.Id, fila => fila.Simbolo, cancellationToken);

        return
        [
            .. movimientos.Select(movimiento => AResumen(
                movimiento,
                movimiento.PositionId is { } posicion ? simbolos.GetValueOrDefault(posicion) : null)),
        ];
    }

    public async Task<MovementSummary> AddCashAsync(
        Guid portfolioId,
        string kind,
        decimal amount,
        string currency,
        DateOnly occurredOn,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var ahora = DateTimeOffset.UtcNow;
        var importe = Money.Of(amount, Currency.FromCode(currency));

        var movimiento = kind switch
        {
            "Deposit" => Movement.Deposit(portfolioId, importe, occurredOn, notes, ahora),
            "Withdrawal" => Movement.Withdrawal(portfolioId, importe, occurredOn, notes, ahora),

            // Una venta NO se registra aquí: se registra al vender, porque además reduce la posición. Dejar
            // crear una venta suelta permitiría tener una venta sin su reducción, y la posición y el registro
            // dirían cosas distintas.
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind), kind, "Solo se pueden registrar aportaciones y retiros."),
        };

        _context.Movements.Add(movimiento);
        await _context.SaveChangesAsync(cancellationToken);

        return AResumen(movimiento, symbol: null);
    }

    private static MovementSummary AResumen(Movement movimiento, string? symbol) =>
        new(
            movimiento.Id,
            movimiento.Kind.ToString(),
            movimiento.OccurredOn,
            movimiento.Amount.Amount,
            movimiento.Amount.Currency.Code,
            symbol,
            movimiento.Quantity?.Value,
            movimiento.PricePerUnit?.Amount,
            movimiento.Notes);
}
