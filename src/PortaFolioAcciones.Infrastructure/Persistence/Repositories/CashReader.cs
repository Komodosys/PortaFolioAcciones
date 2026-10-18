using Microsoft.EntityFrameworkCore;
using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Application.Portfolios;
using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Infrastructure.Persistence.Repositories;

/// <summary>
/// Calcula el saldo de efectivo de una cartera.
///
/// **No lo guarda.** Se suma cada vez: un saldo almacenado puede dejar de coincidir con los movimientos que lo
/// produjeron, y entonces hay dos verdades. Calculado, cuadra por construcción.
/// </summary>
internal sealed class CashReader : ICashReader
{
    private readonly PortafolioDbContext _context;

    public CashReader(PortafolioDbContext context)
    {
        _context = context;
    }

    public async Task<CashBalanceSummary?> GetAsync(
        Guid portfolioId,
        CancellationToken cancellationToken = default)
    {
        // La cartera, con su filtro: si no es accesible, no hay saldo que dar.
        var cartera = await _context.Portfolios
            .AsNoTracking()
            .SingleOrDefaultAsync(portfolio => portfolio.Id == portfolioId, cancellationToken);

        if (cartera is null)
        {
            return null;
        }

        var moneda = cartera.BaseCurrency ?? await BaseDelDuenoAsync(cartera.OwnerUserId, cancellationToken);

        var movimientos = await _context.Movements
            .AsNoTracking()
            .Where(movimiento => movimiento.PortfolioId == portfolioId)
            .ToListAsync(cancellationToken);

        // La apertura: la última declarada, porque si se corrige se declara otra vez y vale la nueva. El saldo
        // cuenta desde ahí y lo anterior no se recalcula.
        var apertura = movimientos
            .Where(movimiento => movimiento.Kind is MovementKind.Opening)
            .OrderByDescending(movimiento => movimiento.OccurredOn)
            .ThenByDescending(movimiento => movimiento.CreatedAtUtc)
            .FirstOrDefault();

        var desde = apertura?.OccurredOn;
        var sinConvertir = 0;

        decimal Suma(MovementKind tipo)
        {
            var total = 0m;

            foreach (var movimiento in movimientos.Where(m => m.Kind == tipo))
            {
                // Lo anterior a la apertura está incorporado en lo declarado, no se resta otra vez.
                if (desde is { } dia && movimiento.OccurredOn < dia)
                {
                    continue;
                }

                if (movimiento.AmountIn(moneda) is { } importe)
                {
                    total += importe.Amount;
                }
                else
                {
                    sinConvertir++;
                }
            }

            return total;
        }

        var declarado = Suma(MovementKind.Opening);
        var aportado = Suma(MovementKind.Deposit);
        var retirado = Suma(MovementKind.Withdrawal);
        var vendido = Suma(MovementKind.Sale);

        // Las compras: los lotes de las posiciones de esta cartera, posteriores a la apertura.
        var compras = 0m;
        var lotes = await (
                from lote in _context.Lots.AsNoTracking()
                join posicion in _context.Positions.AsNoTracking() on lote.PositionId equals posicion.Id
                where posicion.PortfolioId == portfolioId
                      && (desde == null || lote.PurchaseDate >= desde)
                select lote)
            .ToListAsync(cancellationToken);

        foreach (var lote in lotes)
        {
            if (lote.CostIn(moneda) is { } coste)
            {
                compras += coste.Amount;
            }
            else
            {
                sinConvertir++;
            }
        }

        return new CashBalanceSummary(
            portfolioId,
            moneda.Code,
            declarado,
            aportado,
            retirado,
            vendido,
            compras,
            declarado + aportado - retirado + vendido - compras,
            desde,
            sinConvertir);
    }

    private async Task<Currency> BaseDelDuenoAsync(Guid dueno, CancellationToken cancellationToken)
    {
        var baseDelPerfil = await _context.UserProfiles
            .AsNoTracking()
            .Where(perfil => perfil.Id == dueno)
            .Select(perfil => (Currency?)perfil.BaseCurrency)
            .FirstOrDefaultAsync(cancellationToken);

        return baseDelPerfil ?? Currency.Cad;
    }
}
