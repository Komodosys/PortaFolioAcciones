using Microsoft.EntityFrameworkCore;
using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Application.Portfolios;
using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Infrastructure.Persistence.Repositories;

/// <summary>
/// La evolución de una cartera en el tiempo: lo que valía y lo que se había puesto.
///
/// <b>Se calcula, no se guarda.</b> Guardar una serie histórica sería guardar algo que se puede reconstruir, y
/// entonces habría dos verdades: la de las fotos y la de los movimientos.
///
/// <b>De dónde sale cada cosa, y dónde está la dificultad:</b>
///
///   - <b>Las cantidades de cada día</b> salen de los lotes y de las ventas. Y aquí está el problema: los lotes
///     guardan lo que <b>queda</b>, no lo que se compró. Una venta reparte la reducción entre todos los lotes,
///     así que la cantidad original se perdió. Se recupera <b>invirtiendo</b>: hacia atrás, la cantidad que había
///     antes de una venta es la de ahora más lo vendido, y ese factor se aplica a los lotes que ya existían.
///   - <b>Los desdoblamientos</b> entran en la misma cuenta, porque <b>cambian la unidad</b>: doce acciones de
///     2021 son sesenta de hoy, y el precio de 2021 está expresado en las de entonces.
///   - <b>Los precios y los tipos</b>, el último publicado en o antes de cada día: los mercados cierran los fines
///     de semana y los festivos, y pedir «el de ese día» dejaría huecos donde no hay nada que buscar.
///   - <b>El efectivo</b>, con el mismo criterio que el lector de efectivo: se resta lo comprado y se suma lo
///     cobrado.
/// </summary>
internal sealed class PortfolioEvolutionReader : IPortfolioEvolutionReader
{
    private readonly PortafolioDbContext _context;
    private readonly TimeProvider _reloj;

    public PortfolioEvolutionReader(PortafolioDbContext context, TimeProvider reloj)
    {
        _context = context;
        _reloj = reloj;
    }

    public async Task<PortfolioEvolution?> GetAsync(
        Guid portfolioId,
        DateOnly? desde,
        DateOnly? hasta,
        int puntos,
        CancellationToken cancellationToken = default)
    {
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

        // Sin un solo movimiento no hay historia que reconstruir: la cartera se dio de alta como una fotografía
        // de lo que había. Se devuelven las fotos guardadas, y se dice que son fotos.
        return movimientos.Count == 0
            ? await DeLasFotosAsync(portfolioId, moneda, desde, hasta, cancellationToken)
            : await ReconstruidaAsync(portfolioId, moneda, movimientos, desde, hasta, puntos, cancellationToken);
    }

    // ------------------------------------------------------------------ de las fotografías

    private async Task<PortfolioEvolution> DeLasFotosAsync(
        Guid portfolioId,
        Currency moneda,
        DateOnly? desde,
        DateOnly? hasta,
        CancellationToken cancellationToken)
    {
        var fotos = await _context.PortfolioSnapshots
            .AsNoTracking()
            .Where(foto => foto.PortfolioId == portfolioId)
            .OrderBy(foto => foto.SnapshotDate)
            .ToListAsync(cancellationToken);

        var puntos = fotos
            .Where(foto => (desde is null || foto.SnapshotDate >= desde) && (hasta is null || foto.SnapshotDate <= hasta))
            .Select(foto => new EvolutionPoint(
                foto.SnapshotDate,
                foto.MarketValue.Amount,
                foto.CashValue.Amount,

                // Lo que se puso antes de que hubiera registro no se sabe. Nulo, no cero.
                Contributed: null))
            .ToList();

        return new PortfolioEvolution(portfolioId, moneda.Code, nameof(EvolutionSource.Snapshots), puntos, IsComplete: true, []);
    }

    // ------------------------------------------------------------------ reconstruida

    private async Task<PortfolioEvolution> ReconstruidaAsync(
        Guid portfolioId,
        Currency moneda,
        IReadOnlyList<Movement> movimientos,
        DateOnly? desde,
        DateOnly? hasta,
        int puntos,
        CancellationToken cancellationToken)
    {
        var posiciones = await _context.Positions
            .AsNoTracking()
            .Where(posicion => posicion.PortfolioId == portfolioId)
            .ToListAsync(cancellationToken);

        var idsDePosicion = posiciones.Select(posicion => posicion.Id).ToList();
        var idsDeInstrumento = posiciones.Select(posicion => posicion.InstrumentId).Distinct().ToList();

        var instrumentos = await _context.Instruments
            .AsNoTracking()
            .Where(instrumento => idsDeInstrumento.Contains(instrumento.Id))
            .ToDictionaryAsync(instrumento => instrumento.Id, cancellationToken);

        var lotes = await _context.Lots
            .AsNoTracking()
            .Where(lote => idsDePosicion.Contains(lote.PositionId))
            .ToListAsync(cancellationToken);

        var ventas = movimientos.Where(movimiento => movimiento.Kind is MovementKind.Sale).ToList();
        var hoy = DateOnly.FromDateTime(_reloj.GetUtcNow().UtcDateTime);

        // 1. Las cantidades, invertidas.
        var series = new List<SerieDeInstrumento>();

        foreach (var posicion in posiciones)
        {
            if (!instrumentos.TryGetValue(posicion.InstrumentId, out var instrumento))
            {
                continue;
            }

            // La posición guarda LAS FECHAS de los desdoblamientos que aplicó; la proporción vive en el
            // instrumento, que es donde está el hecho. Se cruzan aquí.
            var splits = posicion.AppliedSplitDates
                .Select(dia => instrumento.Splits.FirstOrDefault(split => split.Date == dia))
                .OfType<StockSplit>()
                .OrderBy(split => split.Date)
                .ToList();

            series.Add(Invertir(
                instrumento,
                lotes.Where(lote => lote.PositionId == posicion.Id).ToList(),
                ventas.Where(venta => venta.PositionId == posicion.Id).ToList(),
                splits,
                hoy));
        }

        // 2. Las fechas que se van a dibujar.
        var primera = desde
            ?? series.SelectMany(serie => serie.Lotes).Select(lote => lote.Fecha)
                .Concat(movimientos.Select(movimiento => movimiento.OccurredOn))
                .DefaultIfEmpty(hoy)
                .Min();

        var ultima = hasta ?? hoy;

        // 3. Precios y tipos, de una vez y en memoria. Son miles de filas, no millones: preguntar por cada fecha
        //    sería una consulta por punto, y con sesenta puntos serían ciento veinte viajes.
        var cierres = (await _context.Prices
                .AsNoTracking()
                .Where(precio => idsDeInstrumento.Contains(precio.InstrumentId) && precio.PriceDate <= ultima)
                .ToListAsync(cancellationToken))
            .GroupBy(precio => precio.InstrumentId)
            .ToDictionary(
                grupo => grupo.Key,
                grupo => grupo.OrderBy(precio => precio.PriceDate)
                    .Select(precio => new Cotizacion(precio.PriceDate, precio.Close.Amount))
                    .ToList());

        var tipos = (await _context.DailyFxRates
                .AsNoTracking()
                .Where(tipo => tipo.To == moneda && tipo.RateDate <= ultima)
                .ToListAsync(cancellationToken))
            .GroupBy(tipo => tipo.From.Code)
            .ToDictionary(
                grupo => grupo.Key,
                grupo => grupo.OrderBy(tipo => tipo.RateDate)
                    .Select(tipo => new Cotizacion(tipo.RateDate, tipo.Rate))
                    .ToList());

        // La serie empieza cuando hay precios, no cuando hay movimientos. Antes del primer cierre no se puede
        // valorar nada, y un tramo plano a cero no es un dato: es una casilla vacía dibujada como si fuera un
        // valor. Si se pide más atrás, se empieza donde se puede.
        var primerCierre = cierres.Values
            .SelectMany(serie => serie)
            .Select(cotizacion => cotizacion.Fecha)
            .DefaultIfEmpty(primera)
            .Min();

        if (primerCierre > primera && desde is null)
        {
            primera = primerCierre;
        }

        if (ultima < primera)
        {
            ultima = primera;
        }

        var fechas = Repartir(primera, ultima, Math.Max(2, puntos));

        var faltan = new SortedSet<string>(StringComparer.Ordinal);
        var completo = true;
        var ordenados = movimientos.OrderBy(movimiento => movimiento.OccurredOn).ToList();
        var resultado = new List<EvolutionPoint>(fechas.Count);

        // 4. Un punto por fecha.
        foreach (var fecha in fechas)
        {
            var valor = 0m;

            foreach (var serie in series)
            {
                var unidades = serie.UnidadesEn(fecha);

                if (unidades == 0m)
                {
                    continue;
                }

                if (UltimoEn(cierres.GetValueOrDefault(serie.InstrumentId), fecha) is not { } cierre)
                {
                    // Se tenía algo y no hay precio: ese punto no vale. Se cuenta y se dice de qué.
                    completo = false;
                    faltan.Add(serie.Simbolo);
                    continue;
                }

                var enMonedaBase = cierre;

                if (serie.MonedaDelPrecio != moneda)
                {
                    if (UltimoEn(tipos.GetValueOrDefault(serie.MonedaDelPrecio.Code), fecha) is not { } tipo)
                    {
                        completo = false;
                        faltan.Add(serie.Simbolo);
                        continue;
                    }

                    enMonedaBase *= tipo;
                }

                valor += unidades * enMonedaBase;
            }

            var (efectivo, aportado) = Acumulado(ordenados, series, fecha, moneda);

            resultado.Add(new EvolutionPoint(fecha, valor, efectivo, aportado));
        }

        return new PortfolioEvolution(
            portfolioId,
            moneda.Code,
            nameof(EvolutionSource.Reconstructed),
            resultado,
            completo,
            [.. faltan]);
    }

    // ------------------------------------------------------------------ la inversión

    /// <summary>
    /// Recupera la cantidad comprada de cada lote <b>en unidades de hoy</b>, que es la única escala común entre
    /// un lote de 2021 y un precio de 2021.
    /// </summary>
    /// <remarks>
    /// Hacia atrás: la cantidad que había antes de una venta es la que hay ahora más lo vendido, y como una venta
    /// reduce todos los lotes en la misma proporción, ese factor se aplica a los que ya existían. Lo vendido se
    /// lleva antes a unidades de hoy, multiplicando por los desdoblamientos posteriores.
    ///
    /// En el mismo día, las compras van <b>antes</b> que las ventas, que es el orden en que ocurrieron: al revés,
    /// la venta se llevaría un factor que no le toca.
    /// </remarks>
    private static SerieDeInstrumento Invertir(
        Instrument instrumento,
        IReadOnlyList<Lot> lotes,
        IReadOnlyList<Movement> ventas,
        IReadOnlyList<StockSplit> splits,
        DateOnly hoy)
    {
        decimal FactorDeSplit(DateOnly desde, DateOnly hasta) =>
            splits.Where(split => split.Date > desde && split.Date <= hasta)
                .Aggregate(1m, (factor, split) => factor * split.Ratio);

        var eventos = new List<Evento>();

        foreach (var lote in lotes)
        {
            eventos.Add(new Evento(lote.PurchaseDate, EsVenta: false, lote.Quantity.Value, lote));
        }

        foreach (var venta in ventas)
        {
            var unidades = (venta.Quantity?.Value ?? 0m) * FactorDeSplit(venta.OccurredOn, hoy);
            eventos.Add(new Evento(venta.OccurredOn, EsVenta: true, unidades, Lote: null));
        }

        eventos = [.. eventos
            .OrderBy(evento => evento.Fecha)
            .ThenBy(evento => evento.EsVenta ? 1 : 0)
            .ThenBy(evento => evento.Lote?.PurchaseDate)
            .ThenBy(evento => evento.Lote?.Id)];

        var cantidad = lotes.Sum(lote => lote.Quantity.Value);
        var factor = 1m;
        var invertidos = new List<LoteInvertido>(lotes.Count);

        for (var i = eventos.Count - 1; i >= 0; i--)
        {
            var evento = eventos[i];

            if (evento.EsVenta)
            {
                if (cantidad <= 0m)
                {
                    continue;
                }

                var antes = cantidad + evento.Cantidad;
                factor *= antes / cantidad;
                cantidad = antes;

                continue;
            }

            var deHoy = evento.Cantidad * factor;
            cantidad -= deHoy;

            invertidos.Add(new LoteInvertido(
                evento.Fecha,
                deHoy,
                evento.Lote!.CostPerUnit.Amount,
                evento.Lote.RateUsed));
        }

        var vendidas = ventas
            .Select(venta => new VentaInvertida(
                venta.OccurredOn,
                (venta.Quantity?.Value ?? 0m) * FactorDeSplit(venta.OccurredOn, hoy),

                // Lo que costó lo vendido. Va aquí porque en una posición cerrada del todo es **lo único** que
                // queda de lo que se pagó: los lotes se quedaron a cero y su cantidad original no se puede
                // recuperar de ellos.
                (venta.Quantity?.Value ?? 0m) * (venta.CostPerUnit?.Amount ?? 0m) * (venta.RateUsed ?? 1m)))
            .ToList();

        return new SerieDeInstrumento(
            instrumento.Id,
            instrumento.Symbol.Value,
            instrumento.QuoteCurrency,
            [.. invertidos.OrderBy(lote => lote.Fecha)],
            vendidas,
            FactorDeSplit,
            hoy);
    }

    // ------------------------------------------------------------------ el acumulado

    /// <summary>
    /// El efectivo y lo puesto hasta una fecha, con el mismo criterio que el lector de efectivo.
    /// </summary>
    private static (decimal Efectivo, decimal? Aportado) Acumulado(
        IReadOnlyList<Movement> movimientos,
        IReadOnlyList<SerieDeInstrumento> series,
        DateOnly fecha,
        Currency moneda)
    {
        var efectivo = 0m;
        var aportado = 0m;
        var conocido = true;

        foreach (var movimiento in movimientos.Where(movimiento => movimiento.OccurredOn <= fecha))
        {
            if (movimiento.AmountIn(moneda) is not { } importe)
            {
                if (movimiento.Kind is MovementKind.Opening or MovementKind.Deposit or MovementKind.Withdrawal
                    or MovementKind.TransferIn or MovementKind.TransferOut)
                {
                    conocido = false;
                }

                continue;
            }

            efectivo += movimiento.Kind switch
            {
                MovementKind.Opening or MovementKind.Deposit => importe.Amount,
                MovementKind.Withdrawal => -importe.Amount,
                MovementKind.TransferIn => importe.Amount,
                MovementKind.TransferOut => -importe.Amount,
                MovementKind.Sale => importe.Amount,
                MovementKind.Dividend => importe.Amount,
                MovementKind.Commission => -importe.Amount,
                _ => 0m,
            };

            // Lo puesto es lo que entró sin ser ganancia: apertura, aportaciones y traspasos, menos lo que salió.
            // Es la línea contra la que se compara el valor, y tiene que ser la misma definición que la del
            // informe, o las dos cifras dirían cosas distintas de lo mismo.
            aportado += movimiento.Kind switch
            {
                MovementKind.Opening or MovementKind.Deposit => importe.Amount,
                MovementKind.Withdrawal => -importe.Amount,
                MovementKind.TransferIn => importe.Amount,
                MovementKind.TransferOut => -importe.Amount,
                _ => 0m,
            };
        }

        foreach (var serie in series)
        {
            efectivo -= serie.CosteDe(fecha, moneda);
        }

        return (efectivo, conocido ? aportado : null);
    }

    // ------------------------------------------------------------------ utilidades

    /// <summary>Reparte el rango en puntos regulares, <b>incluyendo siempre el último día</b>.</summary>
    private static List<DateOnly> Repartir(DateOnly desde, DateOnly hasta, int cuantos)
    {
        var dias = hasta.DayNumber - desde.DayNumber;

        if (dias <= 0 || cuantos <= 1)
        {
            return [hasta];
        }

        // Hacia arriba, para que el número pedido sea un MÁXIMO de verdad: truncando, el paso sale menor y el
        // gráfico acaba con más puntos de los pedidos —setenta y cuatro donde se pedían sesenta—, que no es
        // ningún drama pero incumple lo que la interfaz promete.
        var paso = Math.Max(1, (int)Math.Ceiling((double)dias / (cuantos - 1)));
        var fechas = new List<DateOnly>();

        for (var dia = 0; dia <= dias; dia += paso)
        {
            fechas.Add(desde.AddDays(dia));
        }

        // Cortar el gráfico en el penúltimo punto dejaría fuera justo la cifra que se quiere ver.
        if (fechas[^1] != hasta)
        {
            fechas.Add(hasta);
        }

        return fechas;
    }

    /// <summary>El último valor publicado en o antes de la fecha. Nulo si no hay ninguno.</summary>
    private static decimal? UltimoEn(List<Cotizacion>? serie, DateOnly fecha)
    {
        if (serie is null || serie.Count == 0)
        {
            return null;
        }

        var bajo = 0;
        var alto = serie.Count - 1;
        decimal? hallado = null;

        while (bajo <= alto)
        {
            var medio = (bajo + alto) / 2;

            if (serie[medio].Fecha <= fecha)
            {
                hallado = serie[medio].Valor;
                bajo = medio + 1;
            }
            else
            {
                alto = medio - 1;
            }
        }

        return hallado;
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

    private sealed record Cotizacion(DateOnly Fecha, decimal Valor);

    private sealed record Evento(DateOnly Fecha, bool EsVenta, decimal Cantidad, Lot? Lote);

    private sealed record VentaInvertida(DateOnly Fecha, decimal Cantidad, decimal Coste);

    private sealed record LoteInvertido(DateOnly Fecha, decimal CantidadDeHoy, decimal CostePorUnidad, decimal? Tipo);

    private sealed record SerieDeInstrumento(
        Guid InstrumentId,
        string Simbolo,
        Currency MonedaDelPrecio,
        List<LoteInvertido> Lotes,
        List<VentaInvertida> Ventas,
        Func<DateOnly, DateOnly, decimal> FactorDeSplit,
        DateOnly Hoy)
    {
        /// <summary>
        /// Cuántas unidades había en una fecha. La cuenta se hace en unidades de hoy y se deshace el
        /// desdoblamiento al final, porque <b>el precio de cada día está en las unidades de ese día</b>.
        /// </summary>
        public decimal UnidadesEn(DateOnly fecha)
        {
            var enUnidadesDeHoy = Lotes.Where(lote => lote.Fecha <= fecha).Sum(lote => lote.CantidadDeHoy)
                - Ventas.Where(venta => venta.Fecha <= fecha).Sum(venta => venta.Cantidad);

            var factor = FactorDeSplit(fecha, Hoy);

            return factor == 0m ? 0m : enUnidadesDeHoy / factor;
        }

        /// <summary>
        /// Lo que había costado, en la moneda de la cartera, todo lo comprado hasta esa fecha.
        ///
        /// En una posición <b>cerrada del todo</b> los lotes están a cero y su cantidad original no se puede
        /// recuperar de ellos —cero por lo que sea sigue siendo cero—, así que se usa el coste de lo vendido, que
        /// la venta sí guarda. Sin esto, el efectivo salía <b>más alto de lo que es</b> en todo lo que valían las
        /// posiciones ya vendidas: en esta cuenta, unos 5.900 CAD.
        /// </summary>
        public decimal CosteDe(DateOnly fecha, Currency moneda)
        {
            var total = 0m;

            if (Cerrada)
            {
                foreach (var venta in Ventas.Where(venta => venta.Fecha <= fecha))
                {
                    total += venta.Coste;
                }

                return total;
            }

            foreach (var lote in Lotes.Where(lote => lote.Fecha <= fecha))
            {
                var coste = lote.CantidadDeHoy * lote.CostePorUnidad;

                total += MonedaDelPrecio == moneda ? coste : coste * (lote.Tipo ?? 1m);
            }

            return total;
        }

        /// <summary>Si hoy no queda nada: todo lo que había se vendió.</summary>
        public bool Cerrada => Lotes.Sum(lote => lote.CantidadDeHoy) <= 0m;
    }
}
