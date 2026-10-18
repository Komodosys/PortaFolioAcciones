using Microsoft.EntityFrameworkCore;
using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Application.Portfolios;
using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Infrastructure.Persistence.Repositories;

/// <summary>
/// El informe de rendimiento, leído de la foto diaria y de los movimientos.
///
/// <b>Tres consultas y no una por cartera.</b> Con la lista creciendo, una por cartera serían decenas de
/// viajes para pintar una pantalla; y el administrador ve todas las de la casa.
/// </summary>
internal sealed class PerformanceReader : IPerformanceReader
{
    private readonly PortafolioDbContext _context;

    public PerformanceReader(PortafolioDbContext context)
    {
        _context = context;
    }

    public async Task<PerformanceReport> ReadAsync(CancellationToken cancellationToken = default)
    {
        // 1. Las carteras que puede ver quien llama. El filtro global hace el aislamiento: no hay ningún
        //    parámetro de persona, porque «ver las de otro» es un permiso y no una petición.
        var carteras = await _context.Portfolios
            .AsNoTracking()
            .OrderBy(cartera => cartera.Name)
            .ToListAsync(cancellationToken);

        if (carteras.Count == 0)
        {
            return new PerformanceReport([], null, []);
        }

        var ids = carteras.Select(cartera => cartera.Id).ToList();

        // 2. La ÚLTIMA foto de cada cartera, en una consulta. Se agrupa por cartera y se queda la fecha mayor:
        //    pedir «la última» cartera a cartera sería una consulta por cada una.
        var fotos = await _context.PortfolioSnapshots
            .AsNoTracking()
            .Where(foto => ids.Contains(foto.PortfolioId))
            .GroupBy(foto => foto.PortfolioId)
            .Select(grupo => grupo.OrderByDescending(foto => foto.SnapshotDate).First())
            .ToListAsync(cancellationToken);

        var porCartera = fotos.ToDictionary(foto => foto.PortfolioId);

        // 3. Los movimientos, todos de golpe. Son pocos y se agregan en memoria: calcular la ganancia realizada
        //    en SQL exigiría restar dos importes opcionales y quedarse con los que tienen coste, y eso se lee
        //    mucho peor que un bucle de tres líneas.
        var movimientos = await _context.Movements
            .AsNoTracking()
            .Where(movimiento => ids.Contains(movimiento.PortfolioId))
            .ToListAsync(cancellationToken);

        var propietarios = await PropietariosDeAsync(carteras, cancellationToken);

        var filas = carteras
            .Select(cartera => AFila(
                cartera,
                porCartera.GetValueOrDefault(cartera.Id),
                movimientos.Where(movimiento => movimiento.PortfolioId == cartera.Id).ToList(),
                propietarios.GetValueOrDefault(cartera.OwnerUserId)))
            .ToList();

        var monedas = filas.Select(fila => fila.Currency).Distinct().Order(StringComparer.Ordinal).ToList();

        // Los totales SOLO si todas las filas comparten moneda. Sumar dólares y euros daría un número que no
        // significa nada, y es peor que no dar ninguno.
        var totales = monedas.Count == 1 ? ATotales(filas, monedas[0]) : null;

        return new PerformanceReport(filas, totales, monedas);
    }

    private static PerformanceRow AFila(
        Portfolio cartera,
        PortfolioSnapshot? foto,
        IReadOnlyList<Movement> movimientos,
        DatosDelPropietario? propietario)
    {
        // Lo aportado es el dinero que vino de fuera: las aperturas —lo que ya habia cuando se empezo a
        // contar— y las aportaciones. El retiro va aparte y resta.
        var aportado = movimientos
            .Where(movimiento => movimiento.Kind is MovementKind.Deposit or MovementKind.Opening)
            .Sum(movimiento => movimiento.Amount.Amount);

        var retirado = movimientos
            .Where(movimiento => movimiento.Kind is MovementKind.Withdrawal)
            .Sum(movimiento => movimiento.Amount.Amount);

        var ventas = movimientos.Where(movimiento => movimiento.Kind is MovementKind.Sale).ToList();
        var ganancias = ventas.Select(venta => venta.RealizedGain()).ToList();

        // Si UNA sola venta no tiene coste, el total realizado no se puede dar: se devuelve nulo en vez de
        // sumar las que sí lo tienen, porque esa suma se leería como «lo ganado» y le faltaría una parte.
        var realizado = ganancias.Count == 0 || ganancias.Any(ganancia => ganancia is null)
            ? (decimal?)null
            : ganancias.Sum(ganancia => ganancia!.Amount);

        var valor = foto?.MarketValue.Amount ?? 0m;
        var efectivo = foto?.CashValue.Amount ?? 0m;
        var coste = foto?.CostBasis.Amount ?? 0m;

        return new PerformanceRow(
            cartera.Id,
            cartera.Name,
            propietario?.DisplayName ?? "?",
            cartera.RegistrationType.ToString(),

            // La moneda de la foto es la base de la cartera. Sin foto se usa la de la persona, que es la que se
            // usaría al calcularla.
            foto?.BaseCurrency.Code ?? propietario?.BaseCurrency.Code ?? Currency.Cad.Code,

            valor,
            efectivo,
            coste,
            valor - coste,
            aportado,
            retirado,
            realizado,
            foto?.SnapshotDate,
            movimientos.Any(movimiento => movimiento.Kind is MovementKind.Opening));
    }

    private static PerformanceRow ATotales(IReadOnlyList<PerformanceRow> filas, string moneda)
    {
        // Los totales llevan el identificador vacío y un nombre que la pantalla sustituye por «Total»: así una
        // fila de totales no se puede confundir con una cartera de verdad.
        var realizado = filas.Any(fila => fila.RealizedGain is null) && filas.Any(fila => fila.RealizedGain is not null)
            ? (decimal?)null
            : filas.All(fila => fila.RealizedGain is null)
                ? null
                : filas.Sum(fila => fila.RealizedGain!.Value);

        return new PerformanceRow(
            Guid.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            moneda,
            filas.Sum(fila => fila.MarketValue),
            filas.Sum(fila => fila.CashValue),
            filas.Sum(fila => fila.CostBasis),
            filas.Sum(fila => fila.UnrealizedGain),
            filas.Sum(fila => fila.Contributed),
            filas.Sum(fila => fila.Withdrawn),
            realizado,
            filas.Max(fila => fila.SnapshotDate),

            // TODAS, no alguna: si una sola cartera no tiene apertura, su parte de «aportado» esta incompleta y
            // el total de ganancia saldria inflado por esa parte.
            filas.All(fila => fila.HasOpening));
    }

    private async Task<IReadOnlyDictionary<Guid, DatosDelPropietario>> PropietariosDeAsync(
        IReadOnlyList<Portfolio> carteras,
        CancellationToken cancellationToken)
    {
        var duenos = carteras.Select(cartera => cartera.OwnerUserId).Distinct().ToList();

        return await _context.UserProfiles
            .AsNoTracking()
            .Where(perfil => duenos.Contains(perfil.Id))
            .Select(perfil => new DatosDelPropietario(perfil.Id, perfil.DisplayName, perfil.BaseCurrency))
            .ToDictionaryAsync(datos => datos.Id, datos => datos, cancellationToken);
    }

    /// <summary>Lo que hace falta del dueño para poder agrupar por persona.</summary>
    internal sealed record DatosDelPropietario(Guid Id, string DisplayName, Currency BaseCurrency);
}
