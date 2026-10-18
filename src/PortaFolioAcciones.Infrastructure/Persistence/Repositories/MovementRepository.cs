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

    public async Task<MovimientoBorrado> DeleteAsync(
        Guid portfolioId,
        Guid movementId,
        CancellationToken cancellationToken = default)
    {
        var movimiento = await _context.Movements
            .SingleOrDefaultAsync(
                fila => fila.Id == movementId && fila.PortfolioId == portfolioId,
                cancellationToken);

        if (movimiento is null)
        {
            return MovimientoBorrado.NoExiste();
        }

        // Una venta no se borra: es el registro de que una posicion se redujo. Borrarla dejaria la posicion
        // vendida y sin venta, y la ganancia realizada saldria mal para siempre. Si la venta fue un error, lo
        // que hay que deshacer es la venta entera, no su apunte.
        if (movimiento.Kind is MovementKind.Sale)
        {
            return MovimientoBorrado.No("movement.sale_cannot_be_deleted");
        }

        _context.Movements.Remove(movimiento);
        await _context.SaveChangesAsync(cancellationToken);

        return MovimientoBorrado.Si();
    }
    public async Task<MovementSummary?> CorrectOpeningAsync(
        Guid portfolioId,
        decimal amount,
        string currency,
        DateOnly occurredOn,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0m)
        {
            return null;
        }

        var importe = Money.Of(amount, Currency.FromCode(currency));
        var ocurridoEl = occurredOn;

        var apertura = await _context.Movements
            .Where(movimiento => movimiento.PortfolioId == portfolioId
                                 && movimiento.Kind == MovementKind.Opening)
            .OrderByDescending(movimiento => movimiento.OccurredOn)
            .ThenByDescending(movimiento => movimiento.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (apertura is null)
        {
            // No habia ninguna: se crea, y ya esta.
            return await AddCashAsync(
                portfolioId, "Opening", amount, currency, occurredOn, notes, cancellationToken);
        }

        apertura.CorregirApertura(importe, ocurridoEl, notes);

        apertura.SetRateUsed(await _context.DelDiaAsync(
            portfolioId, importe.Currency, ocurridoEl, cancellationToken));

        // Las demas aperturas que hubiera se retiran: vale la ultima, y dejar las viejas haria dudar de cual manda.
        var sobrantes = await _context.Movements
            .Where(movimiento => movimiento.PortfolioId == portfolioId
                                 && movimiento.Kind == MovementKind.Opening
                                 && movimiento.Id != apertura.Id)
            .ToListAsync(cancellationToken);

        _context.Movements.RemoveRange(sobrantes);

        await _context.SaveChangesAsync(cancellationToken);

        return AResumen(apertura, symbol: null);
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
            // La apertura es el punto de partida del saldo. Se registra como un movimiento mas para que la
            // suma cuadre, pero no cuenta como aportacion: no es dinero que entrara, es el dinero que ya habia.
            "Opening" => Movement.Opening(portfolioId, importe, occurredOn, notes, ahora),

            // Un dividendo entra y una comision sale, pero ninguno es dinero aportado: el dividendo es ganancia y
            // la comision es un coste.
            "Dividend" => Movement.Dividend(portfolioId, importe, occurredOn, notes, ahora),
            "Commission" => Movement.Commission(portfolioId, importe, occurredOn, notes, ahora),

            // Un traspaso mueve dinero como una aportacion, pero NO es dinero puesto: el dinero ya era del
            // titular y solo cambio de cuenta. Se guarda con su propio tipo para que «lo aportado» no lo cuente.
            "TransferIn" => Movement.TransferIn(portfolioId, importe, occurredOn, notes, ahora),
            "TransferOut" => Movement.TransferOut(portfolioId, importe, occurredOn, notes, ahora),

            // Una venta NO se registra aquí: se registra al vender, porque además reduce la posición. Dejar
            // crear una venta suelta permitiría tener una venta sin su reducción, y la posición y el registro
            // dirían cosas distintas.
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind), kind, "Solo se pueden registrar aportaciones, retiros, traspasos, dividendos y comisiones."),
        };

        // El tipo del dia del movimiento, para poder llevar el importe a la moneda de la cartera. Sin el, una
        // apertura en dolares no se puede convertir y el saldo entero se queda en «no se sabe» — que es
        // exactamente lo que paso.
        movimiento.SetRateUsed(await _context.DelDiaAsync(
            portfolioId, importe.Currency, occurredOn, cancellationToken));

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
