using Microsoft.EntityFrameworkCore;
using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Application.Positions;
using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.ValueObjects;
using PortaFolioAcciones.Infrastructure.Persistence;

namespace PortaFolioAcciones.Infrastructure.Persistence.Repositories;

/// <summary>
/// Las posiciones y las operaciones que se hacen sobre ellas.
///
/// El <b>aislamiento</b> aquí no se puede comprobar contra la posición, porque una posición no tiene
/// dueño: se sube hasta su cartera, que sí lo tiene. Es la misma idea que el interceptor de auditoría
/// usa para saber de quién es un lote.
///
/// Las operaciones <b>no reciben el usuario</b>: sale del contexto autenticado. Si se pudiera pasar,
/// bastaría con cambiar un número para vender en la cartera de otra persona.
/// </summary>
internal sealed class PositionRepository : IPositionRepository
{
    private readonly PortafolioDbContext _context;
    private readonly ICurrentUser _currentUser;

    public PositionRepository(PortafolioDbContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<PositionDetail>> ListAsync(
        Guid portfolioId,
        bool includeClosed = false,
        CancellationToken cancellationToken = default)
    {
        if (!await PuedeVerCarteraAsync(portfolioId, cancellationToken))
        {
            return [];
        }

        var posiciones = await _context.Positions
            .AsNoTracking()
            .Include(posicion => posicion.Lots)
            .Where(posicion => posicion.PortfolioId == portfolioId)
            .Where(posicion => includeClosed || !posicion.IsClosed)
            .ToListAsync(cancellationToken);

        var instrumentos = await _context.Instruments
            .AsNoTracking()
            .ToDictionaryAsync(instrumento => instrumento.Id, cancellationToken);

        return
        [
            .. posiciones
                .OrderBy(posicion => instrumentos.GetValueOrDefault(posicion.InstrumentId)?.Symbol.Value)
                .Select(posicion => ADetalle(posicion, instrumentos.GetValueOrDefault(posicion.InstrumentId)))
        ];
    }

    public async Task<PositionDetail?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var posicion = await CargarParaOperarAsync(id, cancellationToken);

        return posicion is null ? null : ADetalle(posicion, await InstrumentoDeAsync(posicion, cancellationToken));
    }

    /// <summary>
    /// La cuenta a la que pertenece una posicion.
    ///
    /// Pasa por <see cref="CargarParaOperarAsync"/>, asi que respeta <b>las dos barreras de aislamiento</b>:
    /// quien no puede tocar la posicion no puede leer de quien es.
    /// </summary>
    public async Task<PositionAccount?> AccountOfAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var posicion = await CargarParaOperarAsync(id, cancellationToken);

        if (posicion is null)
        {
            return null;
        }

        var cartera = await _context.Portfolios
            .AsNoTracking()
            .Where(c => c.Id == posicion.PortfolioId)
            .Select(c => new { c.Id, c.Name, c.OwnerUserId })
            .FirstOrDefaultAsync(cancellationToken);

        if (cartera is null)
        {
            return null;
        }

        var dueno = await _context.UserProfiles
            .AsNoTracking()
            .Where(perfil => perfil.Id == cartera.OwnerUserId)
            .Select(perfil => perfil.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);

        return new PositionAccount(cartera.Id, cartera.Name, cartera.OwnerUserId, dueno ?? "?");
    }

    public async Task<PositionResult> AddAsync(
        Guid portfolioId,
        AddPositionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!await PuedeVerCarteraAsync(portfolioId, cancellationToken))
        {
            return PositionResult.Failed("position.not_found");
        }

        if (command.InstrumentId == Guid.Empty)
        {
            return PositionResult.Failed("position.instrument_required");
        }

        var instrumento = await _context.Instruments
            .SingleOrDefaultAsync(i => i.Id == command.InstrumentId, cancellationToken);

        if (instrumento is null)
        {
            return PositionResult.Failed("position.instrument_not_found");
        }

        // Una posicion por instrumento y cartera: si ya existe, se anaden compras a la que hay. Dos
        // posiciones del mismo instrumento en la misma cartera se sumarian mal y nadie sabria por que.
        var existente = await _context.Positions
            .Include(posicion => posicion.Lots)
            .SingleOrDefaultAsync(
                posicion => posicion.PortfolioId == portfolioId
                            && posicion.InstrumentId == command.InstrumentId,
                cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var posicion = existente ?? Position.Create(portfolioId, command.InstrumentId, now);

        if (existente is null)
        {
            _context.Positions.Add(posicion);
        }

        if (!string.IsNullOrWhiteSpace(command.Notes))
        {
            posicion.SetNotes(command.Notes, now);
        }

        foreach (var lote in command.Lots)
        {
            if (!TryCrearLote(lote, instrumento.QuoteCurrency, out var cantidad, out var coste, out var precision, out var fallo))
            {
                return PositionResult.Failed(fallo);
            }

            if (!TryAnadirLote(posicion, cantidad!, coste!, lote.PurchaseDate, precision, lote.Notes, now, out var nuevo, out fallo))
            {
                return PositionResult.Failed(fallo);
            }

            DeclararLoteNuevo(nuevo!);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return PositionResult.Ok(ADetalle(posicion, instrumento));
    }

    public async Task<PositionResult> AddLotAsync(
        Guid id,
        LotInput lot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lot);

        var posicion = await CargarParaOperarAsync(id, cancellationToken);

        if (posicion is null)
        {
            return PositionResult.Failed("position.not_found");
        }

        var instrumento = await InstrumentoDeAsync(posicion, cancellationToken);

        if (instrumento is null)
        {
            return PositionResult.Failed("position.instrument_not_found");
        }

        if (!TryCrearLote(lot, instrumento.QuoteCurrency, out var cantidad, out var coste, out var precision, out var fallo))
        {
            return PositionResult.Failed(fallo);
        }

        if (!TryAnadirLote(posicion, cantidad!, coste!, lot.PurchaseDate, precision, lot.Notes, DateTimeOffset.UtcNow, out var nuevo, out fallo))
        {
            return PositionResult.Failed(fallo);
        }

        DeclararLoteNuevo(nuevo!);

        await _context.SaveChangesAsync(cancellationToken);

        return PositionResult.Ok(ADetalle(posicion, instrumento));
    }

    public async Task<PositionResult> SellAsync(
        Guid id,
        SellCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var posicion = await CargarParaOperarAsync(id, cancellationToken);

        if (posicion is null)
        {
            return PositionResult.Failed("position.not_found");
        }

        if (command.Quantity <= 0m)
        {
            return PositionResult.Failed("position.invalid_quantity");
        }

        if (command.PricePerUnit <= 0m)
        {
            return PositionResult.Failed("position.invalid_price");
        }

        var vendida = Quantity.Of(command.Quantity);

        // Se comprueba ANTES de llamar al dominio para poder decir «no tienes tantas» en vez de un error
        // generico. El dominio tambien lo comprueba: esto es para poder dar un codigo util.
        if (vendida.Value > posicion.Quantity.Value)
        {
            return PositionResult.Failed("position.insufficient_quantity");
        }

        var ahora = DateTimeOffset.UtcNow;
        var instrumento = await InstrumentoDeAsync(posicion, cancellationToken);

        if (instrumento is null)
        {
            // Sin el instrumento no se sabe en qué moneda se vendió, y registrar la venta con una moneda
            // supuesta daría una ganancia realizada falsa. Mejor no vender que vender y apuntar mal.
            return PositionResult.Failed("position.instrument_missing");
        }

        // El coste medio se toma ANTES de vender: es lo que cuesta lo que se vende, y despues de vender ya
        // no se puede saber, porque los lotes se han reducido.
        var costeMedio = posicion.AverageCostPerUnit;

        posicion.Sell(vendida, ahora);

        // La venta se registra como movimiento, y en el MISMO guardado que la reducción de la posición: si se
        // guardara por separado, un fallo entre las dos dejaría la posición vendida y la venta sin registrar
        // —o al revés—, y la ganancia realizada saldría mal para siempre.
        _context.Movements.Add(Movement.Sale(
            posicion.PortfolioId,
            posicion.Id,
            vendida,
            Money.Of(command.PricePerUnit, instrumento.QuoteCurrency),
            costeMedio,
            command.OccurredOn ?? DateOnly.FromDateTime(ahora.UtcDateTime),
            command.Notes,
            ahora));

        await _context.SaveChangesAsync(cancellationToken);

        return PositionResult.Ok(ADetalle(posicion, instrumento));
    }

    public async Task<PositionResult> SetManualPriceAsync(
        Guid id,
        ManualPriceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var posicion = await CargarParaOperarAsync(id, cancellationToken);

        if (posicion is null)
        {
            return PositionResult.Failed("position.not_found");
        }

        // El precio tiene que ser POSITIVO. Un cero no es un precio: es un dato que falta disfrazado, y
        // si se aceptara, la posicion valdria cero y el rendimiento saldria como una perdida total.
        if (command.Amount <= 0m)
        {
            return PositionResult.Failed("position.invalid_price");
        }

        Currency moneda;

        try
        {
            moneda = Currency.FromCode(command.Currency);
        }
        catch (ArgumentException)
        {
            return PositionResult.Failed("position.invalid_currency");
        }

        posicion.SetManualPrice(Money.Of(command.Amount, moneda), command.Date, DateTimeOffset.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);

        return PositionResult.Ok(ADetalle(posicion, await InstrumentoDeAsync(posicion, cancellationToken)));
    }

    public async Task<PositionResult> ApplySplitAsync(
        Guid id,
        SplitCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var posicion = await CargarParaOperarAsync(id, cancellationToken);

        if (posicion is null)
        {
            return PositionResult.Failed("position.not_found");
        }

        if (command.Ratio <= 0m)
        {
            return PositionResult.Failed("position.invalid_split_ratio");
        }

        var ahora = DateTimeOffset.UtcNow;
        var dia = command.Date ?? DateOnly.FromDateTime(ahora.UtcDateTime);

        if (command.Apply)
        {
            // La fecha del desdoblamiento se guarda con el: es lo que permite saber, mas adelante, que ese
            // ya se atendio y no volver a avisar.
            posicion.ApplySplit(command.Ratio, dia, ahora);
        }
        else
        {
            // No se toca la cantidad: quien lo pide ya tiene las cifras del estado de cuenta posterior al
            // desdoblamiento, y multiplicarlas otra vez las dejaria mal. Solo se anota que ya esta visto.
            posicion.RecordSplitAsReflected(dia, ahora);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return PositionResult.Ok(ADetalle(posicion, await InstrumentoDeAsync(posicion, cancellationToken)));
    }

    public async Task<PositionResult> CloseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var posicion = await CargarParaOperarAsync(id, cancellationToken);

        if (posicion is null)
        {
            return PositionResult.Failed("position.not_found");
        }

        posicion.Close(DateTimeOffset.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);

        return PositionResult.Ok(ADetalle(posicion, await InstrumentoDeAsync(posicion, cancellationToken)));
    }

    public async Task<PositionResult> ReopenAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var posicion = await CargarParaOperarAsync(id, cancellationToken);

        if (posicion is null)
        {
            return PositionResult.Failed("position.not_found");
        }

        posicion.Reopen();

        await _context.SaveChangesAsync(cancellationToken);

        return PositionResult.Ok(ADetalle(posicion, await InstrumentoDeAsync(posicion, cancellationToken)));
    }

    public async Task<PositionResult> SetNotesAsync(
        Guid id,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var posicion = await CargarParaOperarAsync(id, cancellationToken);

        if (posicion is null)
        {
            return PositionResult.Failed("position.not_found");
        }

        posicion.SetNotes(notes, DateTimeOffset.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);

        return PositionResult.Ok(ADetalle(posicion, await InstrumentoDeAsync(posicion, cancellationToken)));
    }

    /// <summary>
    /// Carga una posición para operar con ella, comprobando <b>las dos barreras</b>.
    ///
    /// Aquí la comprobación no puede ser contra la posición, porque no tiene dueño: se sube hasta la
    /// cartera. El filtro global ya esconde las posiciones ajenas, y esta línea vuelve a comprobarlo
    /// contra la cartera por si el filtro fallara.
    /// </summary>
    private async Task<Position?> CargarParaOperarAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return null;
        }

        var posicion = await _context.Positions
            .Include(p => p.Lots)
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (posicion is null)
        {
            return null;
        }

        return await PuedeVerCarteraAsync(posicion.PortfolioId, cancellationToken) ? posicion : null;
    }

    /// <summary>Comprueba si quien llama puede ver —y por tanto tocar— una cartera.</summary>
    private async Task<bool> PuedeVerCarteraAsync(Guid portfolioId, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || portfolioId == Guid.Empty)
        {
            return false;
        }

        var propietario = await _context.Portfolios
            .Where(cartera => cartera.Id == portfolioId)
            .Select(cartera => (Guid?)cartera.OwnerUserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (propietario is null)
        {
            return false;
        }

        // SEGUNDA BARRERA.
        return _currentUser.IsAdmin || propietario == _currentUser.Id;
    }

    private async Task<Instrument?> InstrumentoDeAsync(Position posicion, CancellationToken cancellationToken) =>
        await _context.Instruments
            .AsNoTracking()
            .SingleOrDefaultAsync(i => i.Id == posicion.InstrumentId, cancellationToken);

    /// <summary>
    /// Convierte la cantidad y el coste que llegan de fuera, y dice qué falla si algo falla.
    /// </summary>
    /// <param name="lote">Lo que llega.</param>
    /// <param name="currency">
    /// La moneda en la que <b>cotiza el instrumento</b>, que es en la que se pagó. No se adivina ni se
    /// fija en dólares: un fondo canadiense comprado en dólares canadienses tiene que quedar en dólares
    /// canadienses, o el coste sería el de otra cosa.
    /// </param>
    /// <param name="cantidad">Cantidad convertida.</param>
    /// <param name="coste">Coste convertido.</param>
    /// <param name="precision">Exactitud de la fecha.</param>
    /// <param name="fallo">Código del problema, si lo hay.</param>
    private static bool TryCrearLote(
        LotInput lote,
        Currency currency,
        out Quantity? cantidad,
        out Money? coste,
        out DateAccuracy precision,
        out string fallo)
    {
        cantidad = null;
        coste = null;
        precision = DateAccuracy.Exact;
        fallo = string.Empty;

        if (lote.Quantity <= 0m)
        {
            fallo = "position.invalid_quantity";
            return false;
        }

        if (lote.CostPerUnit < 0m)
        {
            fallo = "position.invalid_cost";
            return false;
        }

        if (!Enum.TryParse(lote.DateAccuracy ?? nameof(DateAccuracy.Exact), ignoreCase: true, out precision))
        {
            fallo = "position.invalid_date_accuracy";
            return false;
        }

        cantidad = Quantity.Of(lote.Quantity);
        coste = Money.Of(lote.CostPerUnit, currency);

        return true;
    }

    private static bool TryAnadirLote(
        Position posicion,
        Quantity cantidad,
        Money coste,
        DateOnly fecha,
        DateAccuracy precision,
        string? notas,
        DateTimeOffset now,
        out Lot? lote,
        out string fallo)
    {
        fallo = string.Empty;
        lote = null;

        // La moneda del coste tiene que ser la misma que la de los lotes que ya hay. El dominio lo
        // comprueba lanzando; aqui se comprueba antes para poder dar un codigo en vez de un error.
        if (posicion.CostCurrency is not null && posicion.CostCurrency != coste.Currency)
        {
            fallo = "position.cost_currency_mismatch";
            return false;
        }

        lote = posicion.AddLot(cantidad, coste, fecha, precision, notas, now);
        return true;
    }

    /// <summary>
    /// Declara el lote nuevo como tal.
    ///
    /// <b>Hace falta aunque parezca redundante.</b> EF, cuando encuentra una entidad con la clave
    /// <b>ya puesta</b>, da por hecho que existe en la base de datos y genera un <c>UPDATE</c> en lugar
    /// de un <c>INSERT</c>. Y el dominio asigna el identificador al crear el lote, que es lo correcto
    /// —una entidad tiene identidad desde que nace—, pero deja a EF con esa suposicion equivocada.
    ///
    /// Se veia al anadir una compra a una posicion que ya existia: el <c>UPDATE</c> afectaba a cero
    /// filas y la peticion fallaba con un error de concurrencia que no tenia nada que ver. Los lotes del
    /// alta inicial si se guardaban, porque entonces el padre entero era nuevo y EF los daba por
    /// anadidos junto a el.
    /// </summary>
    private void DeclararLoteNuevo(Lot lote) => _context.Lots.Add(lote);

    private static PositionDetail ADetalle(Position posicion, Instrument? instrumento) =>
        new(
            posicion.Id,
            posicion.PortfolioId,
            posicion.InstrumentId,
            instrumento?.Symbol.Value ?? "?",
            instrumento?.Name ?? "Instrumento desconocido",
            posicion.Quantity.Value,
            posicion.AverageCostPerUnit.Amount,
            posicion.AverageCostPerUnit.Currency.Code,
            posicion.TotalCost.Amount,
            posicion.IsClosed,
            posicion.ClosedAtUtc,
            posicion.ManualPrice?.Amount,
            posicion.ManualPrice?.Currency.Code,
            posicion.ManualPriceDate,
            posicion.Notes,
            posicion.HasApproximateDates,
            [
                .. posicion.Lots.Select(lote => new LotSummary(
                    lote.Id,
                    lote.Quantity.Value,
                    lote.CostPerUnit.Amount,
                    lote.PurchaseDate,
                    lote.DateAccuracy.ToString(),
                    lote.Notes))
            ]);
}
