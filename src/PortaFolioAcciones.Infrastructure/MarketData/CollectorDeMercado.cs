using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Application.MarketData;
using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.ValueObjects;
using PortaFolioAcciones.Infrastructure.Persistence;
using PortaFolioAcciones.Infrastructure.Persistence.Repositories;

namespace PortaFolioAcciones.Infrastructure.MarketData;

/// <summary>
/// Recoge los precios del dia, calienta los tipos de cambio y toma la foto de las carteras.
///
/// <b>Es idempotente</b>, y eso es lo que lo hace seguro de ejecutar: lo que ya esta recogido no se vuelve
/// a pedir, asi que da igual ejecutarlo al arrancar, despues de una caida o dos veces seguidas. Y es lo que
/// evita tener que guardar «hasta aqui he llegado» en ningun sitio: <b>el propio dato dice si esta</b>.
///
/// <b>El cierre se guarda con la fecha que dice la fuente, no con la de hoy.</b> Es la decision que hace
/// que el historico sirva: en un festivo la fuente devuelve el cierre del dia anterior, y guardarlo con la
/// fecha de hoy pondria un precio donde no hubo negociacion. Con la fecha de la fuente, ese dia
/// simplemente no tiene fila, que es la verdad.
/// </summary>
internal sealed class CollectorDeMercado : IMarketDataCollector
{
    /// <summary>
    /// Cuantos dias atras se miran para saber que cierres ya estan guardados.
    ///
    /// No se mira todo el historico: seria traer años de filas para decidir sobre un dia. Y una fuente que
    /// devuelva un cierre de hace mas de un mes no esta dando «el precio de hoy», esta dando un problema, y
    /// se avisa en vez de guardarlo.
    /// </summary>
    private static readonly TimeSpan Ventana = TimeSpan.FromDays(31);

    private readonly DbContextOptions<PortafolioDbContext> _opciones;
    private readonly IPriceProvider _precios;
    private readonly IPriceHistoryProvider _historico;
    private readonly IFxRateProvider _tiposDeCambio;
    private readonly IFxHistoryProvider _historicoDeCambio;
    private readonly ILogger<CollectorDeMercado> _registro;

    public CollectorDeMercado(
        DbContextOptions<PortafolioDbContext> opciones,
        IPriceProvider precios,
        IPriceHistoryProvider historico,
        IFxRateProvider tiposDeCambio,
        IFxHistoryProvider historicoDeCambio,
        ILogger<CollectorDeMercado> registro)
    {
        _opciones = opciones;
        _precios = precios;
        _historico = historico;
        _tiposDeCambio = tiposDeCambio;
        _historicoDeCambio = historicoDeCambio;
        _registro = registro;
    }

    public async Task<DailyCollectionResult> CollectAsync(
        DateOnly dia,
        CancellationToken cancellationToken = default)
    {
        // El contexto se construye AQUI y con el usuario del sistema, en vez de pedirlo al contenedor.
        //
        // No es un capricho: el contexto que reparte el contenedor lleva el usuario de la peticion, y el
        // job no tiene peticion. Y hacer que «sin peticion» signifique «administrador» en todo el programa
        // seria convertir cualquier descuido futuro en un acceso a los datos de todo el mundo. Aqui el
        // privilegio esta escrito, se ve, y solo vale para este trabajo.
        await using var contexto = new PortafolioDbContext(_opciones, UsuarioDelSistema.Instancia);

        var instrumentos = await InstrumentosQueHacenFaltaAsync(contexto, cancellationToken);

        if (instrumentos.Count == 0)
        {
            _registro.LogInformation("No hay ningun instrumento que necesite precio: no se consulta a nadie.");

            return new DailyCollectionResult(0, 0, 0, 0, [], 0, 0);
        }

        var desde = dia.AddDays(-(int)Ventana.TotalDays);

        // Lo que ya esta guardado, en UNA consulta. Preguntarlo instrumento a instrumento serian decenas
        // de consultas para decidir lo mismo.
        var guardados = await contexto.Prices
            .Where(precio => precio.PriceDate >= desde)
            .Select(precio => new { precio.InstrumentId, precio.PriceDate })
            .ToListAsync(cancellationToken);

        var yaEstan = guardados.Select(p => (p.InstrumentId, p.PriceDate)).ToHashSet();

        var ultimas = await contexto.LatestQuotes
            .ToDictionaryAsync(cotizacion => cotizacion.InstrumentId, cancellationToken);

        var ahora = DateTimeOffset.UtcNow;
        var monedas = new HashSet<string>(StringComparer.Ordinal);
        var nuevos = 0;
        var repetidos = 0;
        var sinPrecio = 0;

        foreach (var instrumento in instrumentos)
        {
            var cotizacion = await PedirAsync(instrumento, cancellationToken);

            if (cotizacion is null)
            {
                sinPrecio++;
                continue;
            }

            monedas.Add(cotizacion.Price.Currency.Code);

            AnotarDesdoblamientos(instrumento, cotizacion, ahora);

            // La ultima cotizacion se actualiza SIEMPRE: es «lo ultimo que se sabe», aunque el cierre del
            // dia no se pueda guardar.
            if (ultimas.TryGetValue(instrumento.Id, out var ultima))
            {
                ultima.Update(cotizacion.Price, cotizacion.AsOfUtc, cotizacion.Source, ahora);
            }
            else
            {
                contexto.LatestQuotes.Add(LatestQuote.Create(
                    instrumento.Id,
                    cotizacion.Price,
                    cotizacion.AsOfUtc,
                    cotizacion.Source,
                    ahora));
            }

            if (cotizacion.PriceDate < desde)
            {
                // Un cierre de hace mas de un mes no es el de hoy. Se avisa y NO se guarda: ponerlo con la
                // fecha que trae ensuciaria el historico, y ponerlo con la de hoy seria mentir.
                _registro.LogWarning(
                    "{Simbolo}: la fuente devolvio un cierre del {Fecha}, demasiado antiguo para ser el de hoy.",
                    instrumento.Symbol.Value,
                    cotizacion.PriceDate);

                sinPrecio++;
                continue;
            }

            if (!yaEstan.Add((instrumento.Id, cotizacion.PriceDate)))
            {
                repetidos++;
                continue;
            }

            contexto.Prices.Add(Price.Create(
                instrumento.Id,
                cotizacion.PriceDate,
                cotizacion.Price,
                cotizacion.Source,
                ahora));

            nuevos++;
        }

        await contexto.SaveChangesAsync(cancellationToken);

        await CalentarTiposDeCambioAsync(contexto, monedas, cancellationToken);

        var (carteras, fotos) = await TomarFotosAsync(contexto, dia, cancellationToken);

        var resumen = new DailyCollectionResult(
            instrumentos.Count,
            nuevos,
            repetidos,
            sinPrecio,
            [.. monedas.Order(StringComparer.Ordinal)],
            carteras,
            fotos);

        _registro.LogInformation(
            "Recoleccion del {Dia}: {Instrumentos} instrumentos, {Nuevos} cierres nuevos, {Repetidos} ya estaban, {SinPrecio} sin precio, {Fotos} fotos de {Carteras} carteras.",
            dia,
            resumen.Instrumentos,
            resumen.Guardados,
            resumen.YaEstaban,
            resumen.SinPrecio,
            resumen.Fotos,
            resumen.Carteras);

        return resumen;
    }

    public async Task<QuoteRefreshResult> RefreshQuotesAsync(CancellationToken cancellationToken = default)
    {
        await using var contexto = new PortafolioDbContext(_opciones, UsuarioDelSistema.Instancia);

        var instrumentos = await InstrumentosQueHacenFaltaAsync(contexto, cancellationToken);

        if (instrumentos.Count == 0)
        {
            return new QuoteRefreshResult(0, 0, 0);
        }

        var ultimas = await contexto.LatestQuotes
            .ToDictionaryAsync(cotizacion => cotizacion.InstrumentId, cancellationToken);

        var ahora = DateTimeOffset.UtcNow;
        var actualizados = 0;
        var sinPrecio = 0;

        foreach (var instrumento in instrumentos)
        {
            var cotizacion = await PedirAsync(instrumento, cancellationToken);

            if (cotizacion is null)
            {
                sinPrecio++;
                continue;
            }

            // Un desdoblamiento se detecta con cualquier lectura del precio, no solo con la del cierre: si la
            // fuente ya lo refleja hoy, esperar a mañana para avisar retrasa un aviso que asusta.
            AnotarDesdoblamientos(instrumento, cotizacion, ahora);

            if (ultimas.TryGetValue(instrumento.Id, out var ultima))
            {
                ultima.Update(cotizacion.Price, cotizacion.AsOfUtc, cotizacion.Source, ahora);
            }
            else
            {
                contexto.LatestQuotes.Add(LatestQuote.Create(
                    instrumento.Id,
                    cotizacion.Price,
                    cotizacion.AsOfUtc,
                    cotizacion.Source,
                    ahora));
            }

            actualizados++;
        }

        // Aquí NO se escriben `Prices`, ni los tipos de cambio, ni la foto de las carteras. Es la diferencia
        // con `CollectAsync` y el motivo de que esta operación sea segura antes del cierre: guardar como
        // cierre de hoy el precio de las once de la mañana ensuciaría el histórico.
        await contexto.SaveChangesAsync(cancellationToken);

        _registro.LogInformation(
            "Cotizaciones refrescadas a mano: {Actualizados} de {Instrumentos}, {SinPrecio} sin precio.",
            actualizados,
            instrumentos.Count,
            sinPrecio);

        return new QuoteRefreshResult(instrumentos.Count, actualizados, sinPrecio);
    }

    public async Task<BackfillResult> BackfillAsync(
        DateOnly desde,
        CancellationToken cancellationToken = default)
    {
        await using var contexto = new PortafolioDbContext(_opciones, UsuarioDelSistema.Instancia);

        var instrumentos = await InstrumentosQueHacenFaltaAsync(contexto, cancellationToken);

        if (instrumentos.Count == 0)
        {
            return new BackfillResult(0, 0, 0, 0);
        }

        // Lo que ya está guardado en la ventana, en UNA consulta: preguntarlo instrumento a instrumento
        // serían decenas de consultas para decidir lo mismo.
        var yaEstan = (await contexto.Prices
                .Where(precio => precio.PriceDate >= desde)
                .Select(precio => new { precio.InstrumentId, precio.PriceDate })
                .ToListAsync(cancellationToken))
            .Select(fila => (fila.InstrumentId, fila.PriceDate))
            .ToHashSet();

        var ahora = DateTimeOffset.UtcNow;
        var nuevos = 0;
        var repetidos = 0;
        var sinSerie = 0;

        foreach (var instrumento in instrumentos)
        {
            var serie = await _historico.GetHistoryAsync(instrumento, desde, cancellationToken);

            if (serie.Count == 0)
            {
                // La fuente no lo conoce: los fondos canadienses de Fundserv no tienen código de mercado. Se
                // cuenta y se sigue; no es un fallo del relleno.
                sinSerie++;
                continue;
            }

            foreach (var cierre in serie)
            {
                if (!yaEstan.Add((instrumento.Id, cierre.Date)))
                {
                    repetidos++;
                    continue;
                }

                contexto.Prices.Add(Price.Create(
                    instrumento.Id,
                    cierre.Date,
                    cierre.Price,
                    "yahoo",
                    ahora));

                nuevos++;
            }
        }

        // Un solo guardado para todo: el relleno son cientos de filas por instrumento, y guardar una a una
        // serían cientos de viajes a la base por cada uno.
        await contexto.SaveChangesAsync(cancellationToken);

        _registro.LogInformation(
            "Relleno desde {Desde}: {Instrumentos} instrumentos, {Nuevos} cierres nuevos, {Repetidos} ya estaban, {SinSerie} sin serie.",
            desde,
            instrumentos.Count,
            nuevos,
            repetidos,
            sinSerie);

        return new BackfillResult(instrumentos.Count, nuevos, repetidos, sinSerie);
    }

    public async Task<FxBackfillResult> BackfillFxAsync(
        DateOnly desde,
        CancellationToken cancellationToken = default)
    {
        await using var contexto = new PortafolioDbContext(_opciones, UsuarioDelSistema.Instancia);

        var divisas = await DivisasQueHacenFaltaAsync(contexto, cancellationToken);
        var bases = await BasesQueHacenFaltaAsync(contexto, cancellationToken);

        if (divisas.Count == 0 || bases.Count == 0)
        {
            return new FxBackfillResult(0, 0, 0, 0);
        }

        // Lo ya guardado en la ventana, en una consulta. Las claves son el par y el día.
        var yaEstan = (await contexto.DailyFxRates
                .Where(tipo => tipo.RateDate >= desde)
                .Select(tipo => new { tipo.From, tipo.To, tipo.RateDate })
                .ToListAsync(cancellationToken))
            .Select(fila => (fila.From.Code, fila.To.Code, fila.RateDate))
            .ToHashSet();

        var pares = 0;
        var nuevos = 0;
        var repetidos = 0;
        var sinSerie = 0;

        foreach (var baseMoneda in bases)
        {
            foreach (var divisa in divisas.Where(divisa => divisa != baseMoneda))
            {
                pares++;

                var serie = await _historicoDeCambio.GetHistoryAsync(
                    divisa, baseMoneda, desde, cancellationToken);

                if (serie.Count == 0)
                {
                    sinSerie++;
                    continue;
                }

                foreach (var tipo in serie)
                {
                    if (!yaEstan.Add((tipo.From.Code, tipo.To.Code, tipo.RateDate)))
                    {
                        repetidos++;
                        continue;
                    }

                    contexto.DailyFxRates.Add(DailyFxRate.FromValue(tipo, DateTimeOffset.UtcNow));
                    nuevos++;
                }
            }
        }

        await contexto.SaveChangesAsync(cancellationToken);

        // Y con los tipos ya guardados, se les pone el suyo a los lotes que no lo tenían.
        await AnotarTiposDeLosLotesAsync(contexto, cancellationToken);

        // Y a los movimientos, que es lo que deja el saldo en «no se sabe» cuando la apertura está en dólares.
        await AnotarTiposDeLosMovimientosAsync(contexto, cancellationToken);

        _registro.LogInformation(
            "Relleno de divisa desde {Desde}: {Pares} pares, {Nuevos} tipos nuevos, {Repetidos} ya estaban, {SinSerie} sin serie.",
            desde, pares, nuevos, repetidos, sinSerie);

        return new FxBackfillResult(pares, nuevos, repetidos, sinSerie);
    }

    /// <summary>Las monedas de los instrumentos que tienen posición abierta.</summary>
    private static async Task<IReadOnlyList<Currency>> DivisasQueHacenFaltaAsync(
        PortafolioDbContext contexto,
        CancellationToken cancellationToken)
    {
        var conPosicion = await contexto.Positions
            .Where(posicion => !posicion.IsClosed)
            .Select(posicion => posicion.InstrumentId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (conPosicion.Count == 0)
        {
            return [];
        }

        return await contexto.Instruments
            .Where(instrumento => conPosicion.Contains(instrumento.Id))
            .Select(instrumento => instrumento.QuoteCurrency)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    /// <summary>Las monedas base: las de las carteras que la tienen propia, y las de los perfiles.</summary>
    private static async Task<IReadOnlyList<Currency>> BasesQueHacenFaltaAsync(
        PortafolioDbContext contexto,
        CancellationToken cancellationToken)
    {
        var deCarteras = await contexto.Portfolios
            .Where(cartera => cartera.BaseCurrency != null)
            .Select(cartera => cartera.BaseCurrency!)
            .ToListAsync(cancellationToken);

        var dePerfiles = await contexto.UserProfiles
            .Select(perfil => perfil.BaseCurrency)
            .Distinct()
            .ToListAsync(cancellationToken);

        return [.. deCarteras.Concat(dePerfiles).Distinct()];
    }
    /// <summary>
    /// Anota el tipo de cambio a los lotes que no lo tienen, con el histórico ya cargado.
    ///
    /// **Hace falta para el efectivo.** Un lote sin tipo no puede decir cuánto salió de la cuenta, y el saldo
    /// incluye todo lo comprado: sin estos tipos, las cuarenta compras anteriores a este dato dejarían el saldo
    /// en «no se sabe» para siempre.
    ///
    /// Se usa el último tipo publicado <b>en o antes</b> de la compra, con el mismo criterio que al registrar
    /// una compra nueva. Y no se toca ninguno que ya lo tenga: el tipo de una compra es un hecho de su día.
    /// </summary>
    private async Task<int> AnotarTiposDeLosLotesAsync(
        PortafolioDbContext contexto,
        CancellationToken cancellationToken)
    {
        var lotes = await contexto.Lots
            .Where(lote => lote.RateUsed == null)
            .ToListAsync(cancellationToken);

        if (lotes.Count == 0)
        {
            return 0;
        }

        // Las carteras y sus monedas base, en una consulta.
        var posiciones = await contexto.Positions
            .AsNoTracking()
            .Select(posicion => new { posicion.Id, posicion.PortfolioId })
            .ToDictionaryAsync(fila => fila.Id, fila => fila.PortfolioId, cancellationToken);

        var carteras = await contexto.Portfolios
            .AsNoTracking()
            .Select(cartera => new { cartera.Id, cartera.BaseCurrency, cartera.OwnerUserId })
            .ToDictionaryAsync(fila => fila.Id, fila => fila, cancellationToken);

        var perfiles = await contexto.UserProfiles
            .AsNoTracking()
            .Select(perfil => new { perfil.Id, perfil.BaseCurrency })
            .ToDictionaryAsync(fila => fila.Id, fila => fila.BaseCurrency, cancellationToken);

        // Los tipos, de golpe: son unos miles y se consultan en memoria.
        var tipos = await contexto.DailyFxRates
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var porPar = tipos
            .GroupBy(tipo => (tipo.From.Code, tipo.To.Code))
            .ToDictionary(
                grupo => grupo.Key,
                grupo => grupo.OrderBy(tipo => tipo.RateDate).ToList());

        var anotados = 0;

        foreach (var lote in lotes)
        {
            if (!posiciones.TryGetValue(lote.PositionId, out var carteraId)
                || !carteras.TryGetValue(carteraId, out var cartera))
            {
                continue;
            }

            var moneda = cartera.BaseCurrency
                         ?? (cartera.OwnerUserId is { } dueno ? perfiles.GetValueOrDefault(dueno) : null);

            if (moneda is null || lote.CostCurrency == moneda)
            {
                continue;
            }

            if (!porPar.TryGetValue((lote.CostCurrency.Code, moneda.Code), out var serie))
            {
                continue;
            }

            // El último publicado en o antes de la compra.
            var tipo = serie.LastOrDefault(fila => fila.RateDate <= lote.PurchaseDate);

            if (tipo is not null)
            {
                lote.SetRateUsed(tipo.Rate);
                anotados++;
            }
        }

        if (anotados > 0)
        {
            await contexto.SaveChangesAsync(cancellationToken);

            _registro.LogInformation("Tipos anotados a {Anotados} lotes con el histórico de divisa.", anotados);
        }

        return anotados;
    }
    /// <summary>
    /// Anota el tipo de cambio a los movimientos que no lo tienen, con el histórico ya cargado.
    ///
    /// Sirve para reparar los que se registraron antes de que el alta guardara el tipo: una apertura en dólares
    /// sin tipo deja el saldo entero en «no se sabe», porque no hay forma de llevarla a la moneda de la cartera.
    /// </summary>
    private async Task<int> AnotarTiposDeLosMovimientosAsync(
        PortafolioDbContext contexto,
        CancellationToken cancellationToken)
    {
        var movimientos = await contexto.Movements
            .Where(movimiento => movimiento.RateUsed == null)
            .ToListAsync(cancellationToken);

        if (movimientos.Count == 0)
        {
            return 0;
        }

        var anotados = 0;

        foreach (var movimiento in movimientos)
        {
            var tasa = await contexto.DelDiaAsync(
                movimiento.PortfolioId, movimiento.Amount.Currency, movimiento.OccurredOn, cancellationToken);

            if (tasa is > 0m)
            {
                movimiento.SetRateUsed(tasa);
                anotados++;
            }
        }

        if (anotados > 0)
        {
            await contexto.SaveChangesAsync(cancellationToken);

            _registro.LogInformation("Tipos anotados a {Anotados} movimientos con el historico de divisa.", anotados);
        }

        return anotados;
    }
    /// <summary>
    /// Los instrumentos que necesitan precio: los que tiene alguien en una posicion abierta.
    ///
    /// Se excluyen los de <b>precio manual</b>. Preguntarlos seria sobrescribir la decision de quien los
    /// tiene con un dato ajeno, y ademas sin avisar.
    /// </summary>
    private static async Task<IReadOnlyList<Instrument>> InstrumentosQueHacenFaltaAsync(
        PortafolioDbContext contexto,
        CancellationToken cancellationToken)
    {
        var conPosicion = await contexto.Positions
            .Where(posicion => !posicion.IsClosed)
            .Select(posicion => posicion.InstrumentId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (conPosicion.Count == 0)
        {
            return [];
        }

        var instrumentos = await contexto.Instruments
            .Where(instrumento => conPosicion.Contains(instrumento.Id))
            .ToListAsync(cancellationToken);

        // El filtro por modo de valoracion se hace en memoria: `ValuationMode` es un objeto de valor con
        // conversion, y compararlo dentro de la consulta no se traduce a SQL.
        return [.. instrumentos.Where(instrumento => instrumento.ValuationMode != ValuationMode.Manual)];
    }

    /// <summary>
    /// Guarda en el instrumento los desdoblamientos que declara la fuente.
    ///
    /// <b>Hay que hacerlo en el momento, y por eso esta aqui.</b> La fuente solo declara los eventos del
    /// rango que se le pide, que es un mes: un desdoblamiento que ocurrio hace dos meses ya no lo cuenta.
    /// Si no se guarda cuando sale, <b>no hay forma de recuperarlo</b>, y sin el la posicion se lee como
    /// una caida que nunca ocurrio.
    ///
    /// Se guardan en el instrumento y no en la posicion porque un desdoblamiento es un hecho del
    /// instrumento: le pasa a todo el mundo que lo tenga, y calculo una vez sirve para todos.
    /// </summary>
    private void AnotarDesdoblamientos(Instrument instrumento, MarketQuote cotizacion, DateTimeOffset ahora)
    {
        if (cotizacion.Splits.Count == 0)
        {
            return;
        }

        var splits = new List<StockSplit>();

        foreach (var evento in cotizacion.Splits)
        {
            try
            {
                splits.Add(StockSplit.Of(DateOnly.FromDateTime(evento.Date.UtcDateTime), evento.Ratio));
            }
            catch (ArgumentOutOfRangeException)
            {
                // Una fuente puede declarar una agrupacion —lo contrario de un desdoblamiento— o algo sin
                // sentido. No se guarda y se dice: aplicar en silencio un cambio distinto del que se cree
                // seria peor que no aplicarlo.
                _registro.LogWarning(
                    "{Simbolo}: la fuente declara un evento con proporcion {Ratio}, que no es un desdoblamiento. Se ignora.",
                    instrumento.Symbol.Value,
                    evento.Ratio);
            }
        }

        var nuevos = instrumento.RecordSplits(splits, ahora);

        if (nuevos > 0)
        {
            _registro.LogInformation(
                "{Simbolo}: {Nuevos} desdoblamiento(s) anotado(s). No se pierden aunque la fuente deje de contarlos.",
                instrumento.Symbol.Value,
                nuevos);
        }
    }

    /// <summary>
    /// Pide el precio, y si la fuente falla <b>no tumba la recoleccion entera</b>.
    ///
    /// Un instrumento que hoy no da precio no puede impedir que se guarden los otros cincuenta. Es la
    /// diferencia entre perder un dato y perder el dia.
    /// </summary>
    private async Task<MarketQuote?> PedirAsync(Instrument instrumento, CancellationToken cancellationToken)
    {
        try
        {
            return await _precios.GetQuoteAsync(instrumento, cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            _registro.LogWarning(
                error,
                "No se pudo obtener el precio de {Simbolo}: se sigue con los demas.",
                instrumento.Symbol.Value);

            return null;
        }
    }

    /// <summary>
    /// Pide los tipos de cambio del día y <b>los guarda</b>.
    ///
    /// Guardarlos no es un extra: sin ellos se puede decir que la cartera subió un 3 %, pero **no se puede
    /// descomponer** —si fue porque subieron las acciones o porque se movió el dólar—. Y no se puede
    /// reconstruir después: el tipo que se usó ese día no está en ningún otro sitio.
    ///
    /// Pedirlos aquí tiene además una segunda razón: hace que la primera consulta de la mañana y la foto del
    /// día usen <b>los mismos</b>. Sin esto, cada una preguntaría por su cuenta y podrían salir números
    /// distintos del mismo día.
    /// </summary>
    private async Task CalentarTiposDeCambioAsync(
        PortafolioDbContext contexto,
        HashSet<string> monedas,
        CancellationToken cancellationToken)
    {
        if (monedas.Count == 0)
        {
            return;
        }

        var divisas = monedas.Select(Currency.FromCode).ToList();

        var bases = await contexto.Portfolios
            .Select(cartera => cartera.BaseCurrency)
            .ToListAsync(cancellationToken);

        var monedasBase = await contexto.UserProfiles
            .Select(perfil => perfil.BaseCurrency)
            .ToListAsync(cancellationToken);

        var todasLasBases = bases
            .Where(moneda => moneda is not null)
            .Select(moneda => moneda!)
            .Concat(monedasBase)
            .Distinct()
            .ToList();

        foreach (var divisa in todasLasBases)
        {
            try
            {
                var tipos = await _tiposDeCambio.GetRatesAsync(divisa, divisas, cancellationToken);

                await GuardarTiposAsync(contexto, tipos, cancellationToken);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                // Que falten tipos de cambio no impide guardar los precios, que es lo que no se puede
                // recuperar. Se avisa y se sigue.
                _registro.LogWarning(
                    error,
                    "No se pudieron refrescar los tipos de cambio con base {Moneda}.",
                    divisa);
            }
        }
    }

    /// <summary>
    /// Guarda los tipos de cambio que se acaban de usar, <b>sin duplicar</b>.
    ///
    /// El par y el día son la clave: pedir el mismo par dos veces el mismo día actualiza el valor en vez de
    /// dejar dos filas con números distintos, que sería peor que no guardar nada. Y no se piden todos de
    /// golpe a la base: se piden solo los del día que se está recogiendo.
    /// </summary>
    private async Task GuardarTiposAsync(
        PortafolioDbContext contexto,
        IReadOnlyList<FxRate> tipos,
        CancellationToken cancellationToken)
    {
        if (tipos.Count == 0)
        {
            return;
        }

        var fechas = tipos.Select(tipo => tipo.RateDate).Distinct().ToList();

        var guardados = await contexto.DailyFxRates
            .Where(tipo => fechas.Contains(tipo.RateDate))
            .ToListAsync(cancellationToken);

        var porClave = guardados.ToDictionary(tipo => tipo.Key);
        var ahora = DateTimeOffset.UtcNow;
        var nuevos = 0;
        var actualizados = 0;

        foreach (var tipo in tipos)
        {
            if (porClave.TryGetValue((tipo.From.Code, tipo.To.Code, tipo.RateDate), out var guardado))
            {
                guardado.Update(tipo.Rate, ahora);
                actualizados++;
            }
            else
            {
                contexto.DailyFxRates.Add(DailyFxRate.FromValue(tipo, ahora));
                nuevos++;
            }
        }

        await contexto.SaveChangesAsync(cancellationToken);

        _registro.LogInformation(
            "Tipos de cambio guardados: {Nuevos} nuevos, {Actualizados} actualizados.",
            nuevos,
            actualizados);
    }

    /// <summary>
    /// Toma la foto del dia de cada cartera que no la tenga ya.
    /// </summary>
    /// <returns>Cuantas carteras habia y cuantas se fotografiaron.</returns>
    private async Task<(int Carteras, int Fotos)> TomarFotosAsync(
        PortafolioDbContext contexto,
        DateOnly dia,
        CancellationToken cancellationToken)
    {
        var carteras = await contexto.Portfolios
            .Where(cartera => !cartera.IsArchived)
            .Select(cartera => cartera.Id)
            .ToListAsync(cancellationToken);

        if (carteras.Count == 0)
        {
            return (0, 0);
        }

        var yaFotografiadas = await contexto.PortfolioSnapshots
            .Where(foto => foto.SnapshotDate == dia)
            .Select(foto => foto.PortfolioId)
            .ToListAsync(cancellationToken);

        var pendientes = carteras.Except(yaFotografiadas).ToList();

        if (pendientes.Count == 0)
        {
            return (carteras.Count, 0);
        }

        // Se valora con el MISMO lector que usa la pantalla. Valorar aqui por otra via seria tener dos
        // cuentas distintas de lo mismo, y el dia que discreparan no habria forma de saber cual es la buena.
        var lector = new PortfolioViewReader(contexto, UsuarioDelSistema.Instancia, _tiposDeCambio);
        var fotos = 0;

        foreach (var carteraId in pendientes)
        {
            var vista = await lector.GetAsync(carteraId, cancellationToken);

            if (vista is null)
            {
                continue;
            }

            if (!vista.IsComplete)
            {
                // NO se guarda una foto con el total incompleto, y es la decision mas importante de este
                // metodo: una foto es un registro historico, y dentro de un año nadie sabra que aquel
                // numero estaba a medias. Un hueco se ve; un numero bajo, no.
                _registro.LogWarning(
                    "No se toma la foto del {Dia} de la cartera {Cartera}: faltan precios y el total seria parcial.",
                    dia,
                    vista.Name);

                continue;
            }

            var moneda = Currency.FromCode(vista.BaseCurrency);

            // El efectivo entra en la foto, y se calcula con el MISMO contexto y en el mismo guardado: si se
            // calculara aparte, la foto y el saldo podrian decir cosas distintas del mismo dia.
            var saldo = await new CashReader(contexto).GetAsync(vista.PortfolioId, cancellationToken);

            contexto.PortfolioSnapshots.Add(PortfolioSnapshot.Take(
                vista.PortfolioId,
                dia,
                moneda,
                Money.Of(vista.TotalValue, moneda),
                Money.Of(vista.TotalCost, moneda),

                // Si el saldo no se puede dar como bueno —sin apertura o con algo sin convertir— se guarda
                // cero y no una cifra inventada. La foto dice lo que se sabe.
                Money.Of(saldo?.IsKnown == true ? saldo.Balance : 0m, moneda)));

            fotos++;
        }

        await contexto.SaveChangesAsync(cancellationToken);

        return (carteras.Count, fotos);
    }
}
