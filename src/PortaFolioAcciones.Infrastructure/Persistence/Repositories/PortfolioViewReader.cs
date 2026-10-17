using Microsoft.EntityFrameworkCore;
using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Application.Portfolios;
using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.Services;
using PortaFolioAcciones.Domain.ValueObjects;
using PortaFolioAcciones.Infrastructure.Persistence;

namespace PortaFolioAcciones.Infrastructure.Persistence.Repositories;

/// <summary>
/// Calcula la vista de una cartera.
///
/// <b>El cálculo se hace en el dominio</b>, no aquí: este lector solo junta lo que hace falta —la
/// cartera, sus posiciones, un precio para cada instrumento y los tipos de cambio— y luego traduce el
/// resultado a lo que se enseña. La aritmética vive en <see cref="PortfolioValuation"/>, que está
/// probada aparte y no depende de dónde vengan los datos.
/// </summary>
internal sealed class PortfolioViewReader : IPortfolioViewReader
{
    private readonly PortafolioDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IFxRateProvider _tiposDeCambio;

    public PortfolioViewReader(
        PortafolioDbContext context,
        ICurrentUser currentUser,
        IFxRateProvider tiposDeCambio)
    {
        _context = context;
        _currentUser = currentUser;
        _tiposDeCambio = tiposDeCambio;
    }

    public async Task<PortfolioView?> GetAsync(Guid portfolioId, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return null;
        }

        var cartera = await _context.Portfolios
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == portfolioId, cancellationToken);

        if (cartera is null)
        {
            return null;
        }

        // SEGUNDA BARRERA: el filtro global ya la esconde, y esto lo vuelve a comprobar.
        if (!_currentUser.IsAdmin && cartera.OwnerUserId != _currentUser.Id)
        {
            return null;
        }

        var posiciones = await _context.Positions
            .AsNoTracking()
            .Include(p => p.Lots)
            .Where(p => p.PortfolioId == portfolioId)
            .ToListAsync(cancellationToken);

        // LAS CERRADAS SE SEPARAN AQUI, y esto era un fallo de verdad: `Position.Close` pone la marca y
        // **conserva las compras** —para poder reabrir—, asi que la cantidad NO baja a cero. Si se pasaran
        // a la valoracion, una posicion cerrada **seguiria contando en los totales**, que es justo lo
        // contrario de lo que significa cerrarla.
        //
        // `PositionRepository` ya filtraba por `!IsClosed`, pero este lector **no pasa por el
        // repositorio**: consulta la base directamente. Por eso el fallo no lo cazaba ninguna prueba del
        // repositorio, y solo aparecio al probar el cierre de punta a punta.
        var cerradas = posiciones.Where(p => p.IsClosed).ToList();
        var abiertas = posiciones.Where(p => !p.IsClosed).ToList();

        var instrumentos = await _context.Instruments
            .AsNoTracking()
            .ToDictionaryAsync(i => i.Id, cancellationToken);

        // Los precios se resuelven solo para las abiertas: pedir el precio de lo que ya no se tiene seria
        // gastar cuota de la fuente para nada.
        var precios = await ResolverPreciosAsync(abiertas, cancellationToken);

        var monedaBase = cartera.BaseCurrency ?? await MonedaDeLaPersonaAsync(cartera.OwnerUserId, cancellationToken);

        // Las monedas que hay que convertir: la de cada precio y la de cada coste. `OfType` descarta los
        // nulos, que son las posiciones que todavia no tienen ningun lote.
        var monedas = precios.Precios.Values.Select(precio => precio.Currency)
            .Concat(posiciones.Select(p => p.CostCurrency).OfType<Currency>())
            .Distinct();

        var tasas = await _tiposDeCambio.GetRatesAsync(monedaBase, monedas, cancellationToken);
        var conversor = new CurrencyConverter(monedaBase, tasas);

        // Se separa lo que se puede valorar de lo que no, para poder decirlo en vez de dar un total
        // incompleto como si fuera el total.
        var valorables = new List<Position>();
        var sinValorar = new List<(Position Posicion, string Motivo)>();
        var sinTipoDeCambio = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var posicion in abiertas)
        {
            // Vender todo deja la posicion en cero y la marca como cerrada, asi que esto es un cinturon:
            // si alguna vez quedara una abierta sin unidades, tampoco debe contar.
            if (posicion.Quantity.IsZero)
            {
                continue;
            }

            if (!precios.Precios.TryGetValue(posicion.InstrumentId, out var precio))
            {
                // Sin precio se queda FUERA DEL CALCULO, y por eso hay que devolverla aparte: si no
                // apareciera en ningun sitio, quien acaba de darla de alta pensaria que no se guardo.
                sinValorar.Add((posicion, "sin-precio"));
                continue;
            }

            var monedaCoste = posicion.CostCurrency ?? monedaBase;

            if (conversor.CanConvert(precio.Currency) && conversor.CanConvert(monedaCoste))
            {
                valorables.Add(posicion);
                continue;
            }

            var culpable = conversor.CanConvert(precio.Currency) ? monedaCoste : precio.Currency;
            sinTipoDeCambio[culpable.Code] = sinTipoDeCambio.GetValueOrDefault(culpable.Code) + 1;
            sinValorar.Add((posicion, "sin-tipo-de-cambio"));
        }

        var valoracion = PortfolioValuation.Of(
            monedaBase,
            valorables,
            precios.Precios,
            conversor,
            precios.CierresAnteriores);

        var porId = abiertas.ToDictionary(posicion => posicion.Id);

        var vistas = valoracion.Positions
            .Select(posicion => AVista(posicion, valoracion, instrumentos, precios, porId))
            .ToList();

        var sinPrecio = sinValorar
            .Where(par => par.Motivo == "sin-precio")
            .Select(par => par.Posicion.Id)
            .ToList();

        // Las que no se han podido valorar, con sus datos, para poder enseñarlas en la tabla con guiones.
        var noValoradas = sinValorar
            .Select(par => new UnpricedPosition(
                par.Posicion.Id,
                instrumentos.GetValueOrDefault(par.Posicion.InstrumentId)?.Symbol.Value ?? "?",
                instrumentos.GetValueOrDefault(par.Posicion.InstrumentId)?.Name ?? "Instrumento desconocido",
                par.Posicion.Quantity.Value,
                par.Posicion.TotalCost.Amount,
                par.Posicion.TotalCost.Currency.Code,
                par.Motivo))
            .ToList();

        return new PortfolioView(
            cartera.Id,
            cartera.Name,
            monedaBase.Code,
            cartera.OwnerUserId,
            await NombreDeAsync(cartera.OwnerUserId, cancellationToken),
            valoracion.TotalValue.Amount,
            valoracion.TotalCost.Amount,
            valoracion.TotalGain.Amount,
            valoracion.TotalCost.IsZero ? null : valoracion.TotalReturn.Ratio,
            valoracion.TotalDailyChange.Amount,
            valoracion.TotalDailyReturn?.Ratio,
            valoracion.IsDailyComplete,

            // OJO: NO se usa `valoracion.IsComplete`. Al dominio solo se le pasan las posiciones
            // valorables —las que no se pueden convertir hay que dejarlas fuera o revienta—, asi que el
            // dominio no puede saber cuantas se quedaron por el camino. Su `IsComplete` seria cierto
            // siempre. La respuesta honesta es la de esta linea: completa solo si no falta ninguna, ni
            // por precio ni por tipo de cambio.
            sinValorar.Count == 0,

            // Se cuentan las cerradas a mano MAS las que el dominio descarta por quedarse sin unidades. Son
            // dos caminos distintos para el mismo estado —cerrada por decision, o cerrada por venderlo
            // todo— y quien mira la cartera quiere saber cuantas hay, no por que via.
            cerradas.Count + valoracion.ClosedPositions,
            vistas,
            sinPrecio,
            noValoradas,
            [.. sinTipoDeCambio.Select(par => new MissingCurrency(par.Key, par.Value))],
            DesdoblamientosPendientes(abiertas, instrumentos),
            tasas.Count == 0 ? null : tasas.Min(tasa => tasa.RateDate));
    }

    /// <summary>
    /// Los desdoblamientos que alguna posicion no ha aplicado.
    ///
    /// <b>Un desdoblamiento solo afecta a quien ya tenia titulos ese dia</b>, y por eso se mira la fecha de
    /// compra de cada lote: si se compro despues, el desdoblamiento no le toca y avisar seria alarmar sin
    /// motivo.
    ///
    /// Y se descartan los que ya se aplicaron, que es lo que hace util el aviso: sin ese registro, la
    /// aplicacion avisaria **siempre** de lo mismo hasta que alguien se acostumbrara a ignorarlo.
    /// </summary>
    private static IReadOnlyList<PendingSplit> DesdoblamientosPendientes(
        IReadOnlyList<Position> posiciones,
        IReadOnlyDictionary<Guid, Instrument> instrumentos)
    {
        var pendientes = new List<PendingSplit>();

        foreach (var posicion in posiciones)
        {
            if (posicion.Lots.Count == 0 || !instrumentos.TryGetValue(posicion.InstrumentId, out var instrumento))
            {
                continue;
            }

            // La compra mas antigua: a partir de ahi, cualquier desdoblamiento le afecta.
            var primeraCompra = posicion.Lots.Min(lote => lote.PurchaseDate);

            foreach (var split in instrumento.Splits)
            {
                if (split.Date < primeraCompra || posicion.HasAppliedSplit(split.Date))
                {
                    continue;
                }

                pendientes.Add(new PendingSplit(
                    posicion.Id,
                    instrumento.Symbol.Value,
                    split.Date,
                    split.Ratio,
                    posicion.Quantity.Value));
            }
        }

        return pendientes;
    }

    /// <summary>
    /// Como se llama una persona, para poder decir de quien es la cartera que se esta viendo.
    ///
    /// Devuelve <c>"?"</c> si no aparece, que es un estado roto pero no debe impedir ver la cartera: es
    /// mejor un nombre que falta que una pantalla que no carga.
    /// </summary>
    private async Task<string> NombreDeAsync(Guid userId, CancellationToken cancellationToken)
    {
        var nombre = await _context.UserProfiles
            .AsNoTracking()
            .Where(perfil => perfil.Id == userId)
            .Select(perfil => perfil.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);

        return nombre ?? "?";
    }

    /// <summary>
    /// Busca un precio para cada instrumento, por orden de preferencia:
    /// <list type="number">
    ///   <item><description>El precio <b>puesto a mano</b> en la posición, que es una decisión de quien
    ///   la tiene y manda sobre cualquier fuente.</description></item>
    ///   <item><description>La <b>última cotización</b> guardada.</description></item>
    ///   <item><description>El <b>último cierre</b> guardado, para instrumentos que solo publican valor
    ///   liquidativo una vez al día.</description></item>
    /// </list>
    /// </summary>
    /// <summary>Los precios resueltos de una cartera, con todo lo que hace falta para presentarlos.</summary>
    /// <param name="Precios">El precio actual de cada instrumento.</param>
    /// <param name="Origenes">De donde salio cada precio: <c>manual</c>, <c>quote</c> o <c>close</c>.</param>
    /// <param name="Fechas">De que dia es cada precio.</param>
    /// <param name="CierresAnteriores">
    /// El cierre de la sesion anterior a cada precio, <b>solo de los que se conocen</b>. Es lo que permite
    /// calcular lo que ha cambiado hoy, y falta cuando no hay: una compra de hoy no tiene «ayer».
    /// </param>
    private sealed record PreciosResueltos(
        Dictionary<Guid, Money> Precios,
        Dictionary<Guid, string> Origenes,
        Dictionary<Guid, DateOnly> Fechas,
        Dictionary<Guid, Money> CierresAnteriores);

    private async Task<PreciosResueltos> ResolverPreciosAsync(
        IReadOnlyList<Position> posiciones,
        CancellationToken cancellationToken)
    {
        var precios = new Dictionary<Guid, Money>();
        var origenes = new Dictionary<Guid, string>();

        // La fecha se guarda junto al precio y no se deduce despues: para saber si un precio esta viejo hay
        // que saber DE CUANDO es, y cada origen lo dice de una forma distinta.
        var fechas = new Dictionary<Guid, DateOnly>();

        // 1. El precio a mano.
        foreach (var posicion in posiciones.Where(p => p.ManualPrice is not null))
        {
            precios[posicion.InstrumentId] = posicion.ManualPrice!;
            origenes[posicion.InstrumentId] = "manual";

            if (posicion.ManualPriceDate is { } fechaAMano)
            {
                fechas[posicion.InstrumentId] = fechaAMano;
            }
        }

        var ids = posiciones.Select(p => p.InstrumentId).Distinct().ToList();

        // 2. La ultima cotizacion.
        var cotizaciones = await _context.LatestQuotes
            .AsNoTracking()
            .Where(c => ids.Contains(c.InstrumentId))
            .ToListAsync(cancellationToken);

        foreach (var cotizacion in cotizaciones.Where(c => !precios.ContainsKey(c.InstrumentId)))
        {
            precios[cotizacion.InstrumentId] = cotizacion.Price;
            origenes[cotizacion.InstrumentId] = "quote";
            fechas[cotizacion.InstrumentId] = DateOnly.FromDateTime(cotizacion.AsOfUtc.UtcDateTime);
        }

        // 3. El ultimo cierre. Se pide primero la fecha mas reciente de cada instrumento y luego esas
        //    filas: traer todo el historico para quedarse con la ultima seria tirar datos a la basura.
        var ultimasFechas = await _context.Prices
            .AsNoTracking()
            .Where(p => ids.Contains(p.InstrumentId))
            .GroupBy(p => p.InstrumentId)
            .Select(grupo => new { InstrumentId = grupo.Key, Fecha = grupo.Max(p => p.PriceDate) })
            .ToListAsync(cancellationToken);

        var claves = ultimasFechas.Select(f => new { f.InstrumentId, f.Fecha }).ToList();

        var cierres = await _context.Prices
            .AsNoTracking()
            .Where(p => ids.Contains(p.InstrumentId))
            .ToListAsync(cancellationToken);

        foreach (var cierre in cierres
            .Where(c => claves.Any(k => k.InstrumentId == c.InstrumentId && k.Fecha == c.PriceDate))
            .Where(c => !precios.ContainsKey(c.InstrumentId)))
        {
            precios[cierre.InstrumentId] = cierre.Close;
            origenes[cierre.InstrumentId] = "close";
            fechas[cierre.InstrumentId] = cierre.PriceDate;
        }

        return new PreciosResueltos(precios, origenes, fechas, CierresAnteriores(cierres, fechas));
    }

    /// <summary>
    /// El cierre de la sesion anterior a cada precio.
    ///
    /// <b>Sale del historico que ya esta guardado</b>, y por eso el trabajo diario importa mas de lo que
    /// parece: sin cierres guardados no hay con que comparar, y lo que ha cambiado hoy no se puede saber.
    ///
    /// Se busca el cierre <b>estrictamente anterior</b> a la fecha del precio actual. Si el precio de hoy
    /// fuera el de ayer —pasa en un festivo—, compararlo consigo mismo daria un cambio de cero, que es
    /// mentira: no es que no haya cambiado, es que hoy no se ha negociado.
    /// </summary>
    private static Dictionary<Guid, Money> CierresAnteriores(
        IReadOnlyList<Price> cierres,
        IReadOnlyDictionary<Guid, DateOnly> fechas)
    {
        var anteriores = new Dictionary<Guid, Money>();

        foreach (var (instrumentId, fecha) in fechas)
        {
            var anterior = cierres
                .Where(cierre => cierre.InstrumentId == instrumentId && cierre.PriceDate < fecha)
                .OrderByDescending(cierre => cierre.PriceDate)
                .FirstOrDefault();

            if (anterior is not null)
            {
                anteriores[instrumentId] = anterior.Close;
            }
        }

        return anteriores;
    }

    private async Task<Currency> MonedaDeLaPersonaAsync(Guid userId, CancellationToken cancellationToken)
    {
        var moneda = await _context.UserProfiles
            .Where(perfil => perfil.Id == userId)
            .Select(perfil => perfil.BaseCurrency.Code)
            .FirstOrDefaultAsync(cancellationToken);

        return moneda is null ? Currency.Cad : Currency.FromCode(moneda);
    }

    private static PositionView AVista(
        PositionValuation posicion,
        PortfolioValuation valoracion,
        IReadOnlyDictionary<Guid, Instrument> instrumentos,
        PreciosResueltos precios,
        IReadOnlyDictionary<Guid, Position> posiciones)
    {
        var instrumento = instrumentos.GetValueOrDefault(posicion.InstrumentId);
        var entidad = posiciones.GetValueOrDefault(posicion.PositionId);

        // El peso se calcula sobre el total de la cartera valorada. Si el total es cero no hay peso que
        // calcular, y devolver cero seria decir que esa posicion no pinta nada, que no es lo mismo.
        var peso = valoracion.TotalValue.IsZero ? (decimal?)null : valoracion.WeightOf(posicion).Ratio;

        var fechaDelPrecio = precios.Fechas.GetValueOrDefault(posicion.InstrumentId);
        var ahora = DateTimeOffset.UtcNow;

        return new PositionView(
            posicion.PositionId,
            instrumento?.Symbol.Value ?? "?",
            instrumento?.Name ?? "Instrumento desconocido",
            posicion.Quantity.Value,
            posicion.Price.Amount,
            posicion.Price.Currency.Code,
            precios.Origenes.GetValueOrDefault(posicion.InstrumentId, "quote"),

            // La fecha del precio, sea cual sea su origen. Antes solo se rellenaba con los precios puestos
            // a mano, asi que las posiciones con precio de mercado no decian de cuando era — que es
            // justamente cuando mas hace falta saberlo.
            fechaDelPrecio,

            // Si el mercado donde cotiza esta abierto, cerrado o de fiesta. Sin esto, un precio de hace dos
            // dias y uno de hace dos minutos se leen igual.
            instrumento is null
                ? "Closed"
                : MarketCalendar.StateOf(instrumento.Exchange, ahora).ToString(),

            // Y si el precio es viejo. Se dice aparte del estado del mercado porque son dos cosas
            // distintas: el mercado puede estar cerrado y el precio ser el de hoy, que es lo normal.
            instrumento is not null && PriceFreshness.IsStale(instrumento, fechaDelPrecio, ahora),

            posicion.PreviousClose?.Amount,
            posicion.DailyChange?.Amount,
            posicion.BaseDailyChange?.Amount,
            posicion.DailyReturn?.Ratio,

            posicion.MarketValue.Amount,
            posicion.Cost.Amount,
            posicion.BaseMarketValue.Amount,
            posicion.BaseCost.Amount,
            posicion.BaseGain.Amount,
            posicion.BaseReturn.Ratio,
            peso,
            posicion.LocalGain?.Amount,
            posicion.LocalReturn?.Ratio);
    }
}
