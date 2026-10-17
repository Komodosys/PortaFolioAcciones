using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Domain.Entities;

/// <summary>
/// Una compra: una cantidad de unidades a un precio, en una fecha.
///
/// El coste se guarda <b>por unidad y en su moneda original</b>, no como total ya convertido. Es lo
/// que permite recalcular si algún día cambia la moneda base y lo que hace posible separar el
/// rendimiento del activo del efecto divisa.
///
/// Los lotes <b>no se modifican por su cuenta</b>: solo el <see cref="Position"/> que los contiene
/// puede reducirlos (una venta parcial) o ajustarlos (un split). Es lo que garantiza que la suma de
/// los lotes siga coincidiendo siempre con la posición.
/// </summary>
public sealed class Lot
{
    private Lot(
        Guid id,
        Quantity quantity,
        Money costPerUnit,
        DateOnly purchaseDate,
        DateAccuracy dateAccuracy,
        string? notes)
    {
        Id = id;
        Quantity = quantity;
        CostPerUnit = costPerUnit;
        PurchaseDate = purchaseDate;
        DateAccuracy = dateAccuracy;
        Notes = notes;
    }

    /// <summary>Constructor sin parametros para el mapeo de la base de datos.</summary>
    private Lot()
    {
        Quantity = ValueObjects.Quantity.Zero;
        CostPerUnit = Money.Zero(Currency.Cad);
    }

    public Guid Id { get; private set; }

    /// <summary>Posicion a la que pertenece el lote. Un lote no existe sin su posicion.</summary>
    public Guid PositionId { get; private set; }

    /// <summary>Cantidad comprada, o la que queda de ella tras ventas y splits.</summary>
    public Quantity Quantity { get; private set; }

    /// <summary>Coste por unidad, en la moneda en que se compro.</summary>
    public Money CostPerUnit { get; private set; }

    /// <summary>Fecha de la compra.</summary>
    public DateOnly PurchaseDate { get; private set; }

    /// <summary>Cuanto de fiable es <see cref="PurchaseDate"/>.</summary>
    public DateAccuracy DateAccuracy { get; private set; }

    /// <summary>Notas libres.</summary>
    public string? Notes { get; private set; }

    /// <summary>Moneda en la que se compro.</summary>
    public Currency CostCurrency => CostPerUnit.Currency;

    /// <summary>Coste total del lote: cantidad por coste unitario.</summary>
    public Money TotalCost => Quantity * CostPerUnit;

    /// <summary>Dice si al lote no le queda nada.</summary>
    public bool IsEmpty => Quantity.IsZero;

    /// <summary>Crea un lote.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Si la cantidad no es positiva o el coste es negativo.</exception>
    public static Lot Create(
        Quantity quantity,
        Money costPerUnit,
        DateOnly purchaseDate,
        DateAccuracy dateAccuracy = DateAccuracy.Exact,
        string? notes = null)
    {
        ArgumentNullException.ThrowIfNull(quantity);
        ArgumentNullException.ThrowIfNull(costPerUnit);

        if (quantity.Value <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity), quantity.Value,
                "Un lote es una compra, asi que su cantidad tiene que ser mayor que cero.");
        }

        if (costPerUnit.Amount < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(costPerUnit), costPerUnit.Amount,
                "El coste de un lote no puede ser negativo.");
        }

        return new Lot(Guid.NewGuid(), quantity, costPerUnit, purchaseDate, dateAccuracy, notes);
    }

    /// <summary>
    /// Vincula el lote a su posicion. Lo llama <see cref="Position"/> al anadirlo: un lote suelto no
    /// tiene sentido, asi que no se puede crear ya vinculado desde fuera.
    /// </summary>
    internal void AttachTo(Guid positionId) => PositionId = positionId;

    /// <summary>
    /// Reduce el lote proporcionalmente, conservando el coste por unidad. Lo usa una venta parcial.
    /// La cantidad resultante se redondea a la precision que admite una cantidad; el residuo de ese
    /// redondeo lo reparte el <see cref="Position"/>, para que la suma de los lotes siga siendo exacta.
    /// </summary>
    internal void ReduceBy(decimal factor)
    {
        var reduced = decimal.Round(Quantity.Value * factor, ValueObjects.Quantity.MaxDecimals, MidpointRounding.ToEven);
        Quantity = ValueObjects.Quantity.Of(reduced);
    }

    /// <summary>
    /// Suma una cantidad minima al lote. Sirve para absorber el residuo del redondeo de una venta.
    /// </summary>
    internal void AdjustBy(decimal delta)
    {
        var adjusted = decimal.Round(
            Quantity.Value + delta, ValueObjects.Quantity.MaxDecimals, MidpointRounding.ToEven);
        Quantity = ValueObjects.Quantity.Of(adjusted);
    }

    /// <summary>
    /// Aplica un split: la cantidad se multiplica y el coste por unidad se divide, de modo que el
    /// <b>coste total no cambia</b>. Un split reparte lo mismo entre mas titulos.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Si la proporcion no es positiva.</exception>
    internal void ApplySplit(decimal ratio)
    {
        if (ratio <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ratio), ratio, "La proporcion de un split tiene que ser mayor que cero.");
        }

        var split = decimal.Round(Quantity.Value * ratio, ValueObjects.Quantity.MaxDecimals, MidpointRounding.ToEven);
        Quantity = ValueObjects.Quantity.Of(split);
        CostPerUnit = CostPerUnit.DivideBy(ratio);
    }
}
