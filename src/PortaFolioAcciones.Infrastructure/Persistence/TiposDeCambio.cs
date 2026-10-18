using Microsoft.EntityFrameworkCore;
using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Infrastructure.Persistence;

/// <summary>
/// Búsqueda de tipos de cambio para llevar un importe a la moneda de una cartera.
///
/// Vivía copiada en tres sitios y se quedó corta en uno de ellos: el alta de movimientos no guardaba el tipo, así
/// que una aportación en dólares se quedaba sin poder convertir y el saldo decía «no se sabe». Teniendo la
/// búsqueda en un solo sitio, añadir un uso nuevo es una línea y no una copia más.
/// </summary>
internal static class TiposDeCambio
{
    /// <summary>
    /// El tipo para llevar un importe a la moneda base de la cartera, <b>el del día del movimiento</b>.
    /// </summary>
    /// <returns>
    /// Nulo si no hace falta convertir —el importe ya está en la moneda base— y también si no hay tipo para ese
    /// día. Los dos casos son distintos, pero se comportan igual: el importe se guarda sin tipo y quien calcule
    /// el saldo dirá que no se sabe, en vez de inventar una conversión.
    /// </returns>
    /// <remarks>
    /// Se busca el <b>último publicado en o antes</b> de esa fecha y no exactamente el de esa fecha: los tipos no
    /// se publican fines de semana ni festivos, y una compra hecha un sábado se liquidó al del viernes. Lo que
    /// <b>no</b> se hace es cambiarle la fecha al tipo para que coincida: es un dato con fecha, y falsearla es
    /// peor que no tenerlo.
    /// </remarks>
    public static async Task<decimal?> DelDiaAsync(
        this PortafolioDbContext context,
        Guid portfolioId,
        Currency divisa,
        DateOnly dia,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(divisa);

        var baseDeLaCartera = await BaseDeLaCarteraAsync(context, portfolioId, cancellationToken);

        if (baseDeLaCartera is null || divisa == baseDeLaCartera)
        {
            // Sin moneda base no hay a qué convertir, y si coinciden no hace falta.
            return null;
        }

        var tasa = await context.DailyFxRates
            .AsNoTracking()
            .Where(tipo => tipo.From == divisa && tipo.To == baseDeLaCartera && tipo.RateDate <= dia)
            .OrderByDescending(tipo => tipo.RateDate)
            .FirstOrDefaultAsync(cancellationToken);

        return tasa?.Rate;
    }

    /// <summary>La moneda en la que se presentan los totales de una cartera: la suya, o la de su dueño.</summary>
    public static async Task<Currency?> BaseDeLaCarteraAsync(
        this PortafolioDbContext context,
        Guid portfolioId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var cartera = await context.Portfolios
            .AsNoTracking()
            .Where(portfolio => portfolio.Id == portfolioId)
            .Select(portfolio => new { portfolio.BaseCurrency, portfolio.OwnerUserId })
            .FirstOrDefaultAsync(cancellationToken);

        if (cartera is null)
        {
            return null;
        }

        if (cartera.BaseCurrency is { } propia)
        {
            return propia;
        }

        return await context.UserProfiles
            .AsNoTracking()
            .Where(perfil => perfil.Id == cartera.OwnerUserId)
            .Select(perfil => (Currency?)perfil.BaseCurrency)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
