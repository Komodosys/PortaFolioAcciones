using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Domain.Entities;

/// <summary>
/// Una posicion en un instrumento dentro de una cartera. Es el <b>agregado</b>: la unica puerta de
/// entrada a sus lotes.
///
/// <b>No guarda cantidad ni coste propios.</b> La cantidad es la suma de sus lotes y el coste, la suma
/// del coste de cada uno. Guardarlos aparte seria tener dos verdades que pueden divergir, y la que se
/// muestra podria no ser la que se calcula.
/// </summary>
public sealed class Position
{
    private readonly List<Lot> _lots = [];

    private Position(Guid id, Guid portfolioId, Guid instrumentId, DateTimeOffset now)
    {
        Id = id;
        PortfolioId = portfolioId;
        InstrumentId = instrumentId;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
    }

    /// <summary>Constructor sin parametros para el mapeo de la base de datos.</summary>
    private Position()
    {
    }

    public Guid Id { get; private set; }

    public Guid PortfolioId { get; private set; }

    public Guid InstrumentId { get; private set; }

    /// <summary>Dice si la posicion se quedo sin unidades y se cerro.</summary>
    public bool IsClosed { get; private set; }

    /// <summary>Cuando se cerro, si esta cerrada.</summary>
    public DateTimeOffset? ClosedAtUtc { get; private set; }

    /// <summary>
    /// Lo que habia costado la posicion en el momento de cerrarla.
    ///
    /// Existe porque al venderlo todo los lotes quedan a cero y su coste se pierde, y sin registro de
    /// operaciones esta fila es <b>el unico rastro</b> de lo que se pago. Guardarlo aqui es lo que hace
    /// cierta esa promesa.
    /// </summary>
    public Money? CostBasisAtClose { get; private set; }

    /// <summary>Precio introducido a mano, para instrumentos que no publican cotizacion.</summary>
    public Money? ManualPrice { get; private set; }

    /// <summary>Fecha del precio introducido a mano.</summary>
    public DateOnly? ManualPriceDate { get; private set; }

    public string? Notes { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>
    /// Lotes de la posicion, <b>ordenados por fecha de compra</b> y, a igualdad de fecha, por
    /// identificador.
    ///
    /// El orden importa para quien lo mira —la interfaz, las pruebas—, pero la base de datos no
    /// garantiza ninguno si no se pide. Se ordena aqui, en el dominio, para que el resultado sea el
    /// mismo venga como venga de la consulta. Los calculos del agregado no dependen del orden, porque
    /// son sumas; lo que se evita es que dos consultas den listados distintos.
    /// </summary>
    public IReadOnlyList<Lot> Lots =>
        [.. _lots.OrderBy(lot => lot.PurchaseDate).ThenBy(lot => lot.Id)];

    /// <summary>Cantidad de la posicion: la suma de sus lotes.</summary>
    public Quantity Quantity =>
        Quantity.Of(_lots.Sum(l => l.Quantity.Value));

    /// <summary>Coste total: la suma del coste de cada lote, en la moneda en que se compraron.</summary>
    public Money TotalCost =>
        _lots.Count == 0 ? Money.Zero(Currency.Cad) : _lots.Aggregate(Money.Zero(CostCurrency!), (t, l) => t.Add(l.TotalCost));

    /// <summary>
    /// Coste medio por unidad, que es lo que publica el broker. Una venta parcial <b>lo conserva</b>,
    /// porque reduce todos los lotes en la misma proporcion.
    /// </summary>
    public Money AverageCostPerUnit =>
        Quantity.IsZero ? Money.Zero(CostCurrency ?? Currency.Cad) : TotalCost.DivideBy(Quantity.Value);

    /// <summary>
    /// Moneda en la que se compro la posicion. Todos sus lotes comparten una, porque un instrumento se
    /// compra en una sola moneda; si hicieran falta dos, son dos instrumentos distintos.
    /// </summary>
    public Currency? CostCurrency => _lots.Count == 0 ? null : _lots[0].CostCurrency;

    /// <summary>Fecha de la compra mas antigua.</summary>
    public DateOnly? FirstPurchaseDate => _lots.Count == 0 ? null : _lots.Min(l => l.PurchaseDate);

    /// <summary>Fecha de la compra mas reciente.</summary>
    public DateOnly? LastPurchaseDate => _lots.Count == 0 ? null : _lots.Max(l => l.PurchaseDate);

    /// <summary>Dice si algun lote tiene una fecha que no es exacta.</summary>
    public bool HasApproximateDates => _lots.Any(l => l.DateAccuracy != DateAccuracy.Exact);

    /// <summary>Crea una posicion vacia.</summary>
    public static Position Create(Guid portfolioId, Guid instrumentId, DateTimeOffset now)
    {
        if (portfolioId == Guid.Empty)
        {
            throw new ArgumentException("Una posicion necesita saber a que cartera pertenece.", nameof(portfolioId));
        }

        if (instrumentId == Guid.Empty)
        {
            throw new ArgumentException("Una posicion necesita saber sobre que instrumento es.", nameof(instrumentId));
        }

        return new Position(Guid.NewGuid(), portfolioId, instrumentId, now);
    }

    /// <summary>
    /// Anade un lote. Si la posicion estaba cerrada, la reabre: volver a comprar significa volver a
    /// tener posicion.
    /// </summary>
    /// <exception cref="InvalidOperationException">Si la moneda del coste no es la de los demas lotes.</exception>
    public Lot AddLot(
        Quantity quantity,
        Money costPerUnit,
        DateOnly purchaseDate,
        DateAccuracy dateAccuracy,
        string? notes,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(costPerUnit);

        // Todos los lotes de una posicion se compran en la misma moneda. Mezclarlas haria que el coste
        // total fuera la suma de cosas distintas.
        if (CostCurrency is not null && CostCurrency != costPerUnit.Currency)
        {
            throw new InvalidOperationException(
                $"Esta posicion se compro en {CostCurrency} y se intenta anadir un lote en " +
                $"{costPerUnit.Currency}. Si de verdad son dos monedas, son dos instrumentos distintos.");
        }

        if (IsClosed)
        {
            Reopen();
        }

        var lot = Lot.Create(quantity, costPerUnit, purchaseDate, dateAccuracy, notes);
        lot.AttachTo(Id);
        _lots.Add(lot);
        Touch(now);
        return lot;
    }

    /// <summary>
    /// Vende una cantidad. La reduccion se reparte <b>proporcionalmente entre todos los lotes</b>, que
    /// es lo que hace el metodo de coste medio del broker y lo que conserva el coste por unidad.
    ///
    /// No se elige «que lote se vende»: elegirlo daria un coste por unidad distinto del que declara el
    /// estado de cuenta, y la aplicacion dejaria de cuadrar con el broker.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Si la cantidad no es positiva.</exception>
    /// <exception cref="InvalidOperationException">Si se vende mas de lo que se tiene.</exception>
    public void Sell(Quantity sold, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(sold);

        if (sold.Value <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sold), sold.Value, "La cantidad vendida tiene que ser mayor que cero.");
        }

        var current = Quantity;

        if (sold.Value > current.Value)
        {
            throw new InvalidOperationException(
                $"No se pueden vender {sold.Value} unidades cuando solo hay {current.Value}.");
        }

        var costBasisBefore = TotalCost;
        var expected = current.Value - sold.Value;

        var factor = current.RemainingFactorAfterSelling(sold);
        foreach (var lot in _lots)
        {
            lot.ReduceBy(factor);
        }

        // Cada lote se redondea por separado, asi que la suma puede quedar a unas centesimas de
        // millonésima de la cantidad esperada. Ese residuo se ajusta en el lote mayor: asi la suma
        // vuelve a ser exacta y la cantidad que se muestra es la que debe ser.
        var residual = expected - Quantity.Value;
        if (residual != 0m && _lots.Count > 0)
        {
            var largest = _lots.OrderByDescending(l => l.Quantity.Value).First();
            largest.AdjustBy(residual);
        }

        Touch(now);

        if (Quantity.IsZero)
        {
            IsClosed = true;
            ClosedAtUtc = now;
            CostBasisAtClose = costBasisBefore;
        }
    }

    /// <summary>
    /// Los desdoblamientos que ya se aplicaron a esta posicion.
    ///
    /// <b>Hace falta registrarlos, y no se pueden deducir de los datos.</b> Aplicar un 5:1 deja la cantidad
    /// multiplicada y el coste por unidad dividido, pero <b>nada distingue ese estado del de no haberlo
    /// aplicado</b>: no hay ninguna cifra que cuadre o deje de cuadrar. Sin este registro, la aplicacion no
    /// puede saber si le falta aplicar un desdoblamiento y tendria que avisar siempre —o no avisar nunca,
    /// que es peor.
    /// </summary>
    public IReadOnlyList<DateOnly> AppliedSplitDates => _splitsApplied;

    private readonly List<DateOnly> _splitsApplied = [];

    /// <summary>Dice si ya se aplico el desdoblamiento de ese dia.</summary>
    /// <param name="splitDate">El dia del desdoblamiento.</param>
    public bool HasAppliedSplit(DateOnly splitDate) => _splitsApplied.Contains(splitDate);

    /// <summary>
    /// Aplica un split: la cantidad se multiplica y el coste por unidad se divide. El coste total
    /// <b>no cambia</b>, porque un split reparte lo mismo entre mas titulos.
    /// </summary>
    /// <param name="ratio">Por cuanto se multiplica la cantidad.</param>
    /// <param name="splitDate">El dia en que ocurrio, para poder recordar que ya se aplico.</param>
    /// <param name="now">Momento del cambio.</param>
    public void ApplySplit(decimal ratio, DateOnly splitDate, DateTimeOffset now)
    {
        if (_lots.Count == 0)
        {
            return;
        }

        foreach (var lot in _lots)
        {
            lot.ApplySplit(ratio);
        }

        // Se anota aunque se repita: si el mismo dia con la misma proporcion se aplica dos veces, eso es
        // cosa de quien llama. Aqui lo que importa es que queda constancia de que ESE dia ya se atendio,
        // que es lo que evita avisar de un desdoblamiento ya resuelto.
        if (!_splitsApplied.Contains(splitDate))
        {
            _splitsApplied.Add(splitDate);
            _splitsApplied.Sort();
        }

        // La fecha de compra NO cambia: un split no es una compra nueva.
        Touch(now);
    }

    /// <summary>
    /// Anota que el desdoblamiento de ese dia <b>ya esta reflejado</b>, sin tocar las cantidades.
    ///
    /// Hace falta, y es tan importante como aplicarlo: quien dio de alta sus posiciones a mano, copiando
    /// del estado de cuenta <b>posterior</b> al desdoblamiento, ya tiene las cifras buenas. Aplicarlo
    /// entonces las multiplicaria por segunda vez y las dejaria mal.
    ///
    /// Sin esta via, esa persona solo podria elegir entre aplicar —y estropear sus datos— o convivir con un
    /// aviso que no puede quitar. Un aviso que no se puede resolver se aprende a ignorar, y entonces deja de
    /// servir para el dia que importa.
    /// </summary>
    /// <param name="splitDate">El dia del desdoblamiento que ya esta reflejado.</param>
    /// <param name="now">Momento del cambio.</param>
    public void RecordSplitAsReflected(DateOnly splitDate, DateTimeOffset now)
    {
        if (_splitsApplied.Contains(splitDate))
        {
            return;
        }

        _splitsApplied.Add(splitDate);
        _splitsApplied.Sort();
        Touch(now);
    }

    /// <summary>Fija el precio a mano, para instrumentos que no publican cotizacion.</summary>
    public void SetManualPrice(Money price, DateOnly date, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(price);

        if (price.Amount < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(price), price.Amount, "Un precio no puede ser negativo.");
        }

        ManualPrice = price;
        ManualPriceDate = date;
        Touch(now);
    }

    /// <summary>Quita el precio manual.</summary>
    public void ClearManualPrice(DateTimeOffset now)
    {
        ManualPrice = null;
        ManualPriceDate = null;
        Touch(now);
    }

    /// <summary>Cierra la posicion a mano, sin vender.</summary>
    public void Close(DateTimeOffset now)
    {
        if (IsClosed)
        {
            return;
        }

        CostBasisAtClose = TotalCost;
        IsClosed = true;
        ClosedAtUtc = now;
        Touch(now);
    }

    /// <summary>Reabre una posicion cerrada.</summary>
    public void Reopen()
    {
        IsClosed = false;
        ClosedAtUtc = null;
        CostBasisAtClose = null;
    }

    /// <summary>Cambia las notas.</summary>
    public void SetNotes(string? notes, DateTimeOffset now)
    {
        Notes = notes;
        Touch(now);
    }

    private void Touch(DateTimeOffset now) => UpdatedAtUtc = now;
}
