using Microsoft.EntityFrameworkCore;
using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Application.MarketData;
using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Infrastructure.Persistence;

namespace PortaFolioAcciones.Infrastructure.Persistence.Repositories;

/// <summary>
/// Lee el último precio guardado de un instrumento: la última cotización y, si no la hay, el último
/// cierre.
///
/// Se consultan los dos porque sirven para cosas distintas: la <b>cotización</b> la guarda el
/// instrumento que se sigue a diario, y el <b>cierre</b> es lo único que existe para un fondo, que
/// publica un valor liquidativo al día.
/// </summary>
internal sealed class LastKnownPriceReader : ILastKnownPrice
{
    private readonly PortafolioDbContext _context;

    public LastKnownPriceReader(PortafolioDbContext context) => _context = context;

    public async Task<MarketQuote?> GetAsync(
        Instrument instrument,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instrument);

        var cotizacion = await _context.LatestQuotes
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.InstrumentId == instrument.Id, cancellationToken);

        if (cotizacion is not null)
        {
            return new MarketQuote(
                cotizacion.Price,

                // La fecha del DATO, no la de la consulta. Es lo que permite decir «este precio es de
                // hace tres días» en vez de presentarlo como de ahora.
                cotizacion.AsOfUtc,
                DateOnly.FromDateTime(cotizacion.AsOfUtc.UtcDateTime),
                [],
                [],
                "ultimo-conocido",
                IsFallback: true);
        }

        var cierre = await _context.Prices
            .AsNoTracking()
            .Where(p => p.InstrumentId == instrument.Id)
            .OrderByDescending(p => p.PriceDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (cierre is null)
        {
            return null;
        }

        return new MarketQuote(
            cierre.Close,
            new DateTimeOffset(cierre.PriceDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            cierre.PriceDate,
            [],
            [],
            "ultimo-conocido",
            IsFallback: true);
    }
}
