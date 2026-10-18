using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Domain.Entities;

/// <summary>
/// Un movimiento de una cartera: dinero que entra o sale, o una venta.
///
/// <b>Existe porque la aplicación no guardaba esto.</b> Una posición cerrada sabe cuándo se cerró y cuánto
/// había costado, pero no por cuánto se vendió, y el dinero que entra y sale de una cuenta no se registraba
/// en ningún sitio. Sin ello no hay forma de responder «cuánto he puesto y cuánto tengo»: solo se puede decir
/// cuánto vale hoy lo que hay.
///
/// **Las compras no están aquí**: son los <see cref="Lot"/>, que ya tienen cantidad, coste y fecha. El
/// registro completo de una cartera es la unión de sus lotes y sus movimientos, y esa unión es una consulta,
/// no una tabla duplicada.
///
/// **Es un hecho, no un saldo.** Un movimiento no se «ajusta» para que cuadre: si se registró mal, se
/// corrige el movimiento o se añade el contrario, y queda el rastro de los dos. Es la misma idea que el libro
/// de auditoría, y por la misma razón: un saldo que se puede tocar no sirve para explicar nada.
/// </summary>
public sealed class Movement
{
    private Movement(
        Guid id,
        Guid portfolioId,
        MovementKind kind,
        DateOnly occurredOn,
        Money amount,
        Guid? positionId,
        Quantity? quantity,
        Money? pricePerUnit,
        string? notes,
        DateTimeOffset now)
    {
        Id = id;
        PortfolioId = portfolioId;
        Kind = kind;
        OccurredOn = occurredOn;
        Amount = amount;
        PositionId = positionId;
        Quantity = quantity;
        PricePerUnit = pricePerUnit;
        Notes = notes;
        CreatedAtUtc = now;
    }

    /// <summary>Constructor sin parámetros para el mapeo de la base de datos.</summary>
    private Movement()
    {
        Amount = Money.Of(0m, Currency.Cad);
    }

    public Guid Id { get; private set; }

    /// <summary>Cartera a la que afecta.</summary>
    public Guid PortfolioId { get; private set; }

    public MovementKind Kind { get; private set; }

    /// <summary>
    /// El día en que ocurrió, <b>en el calendario de quien lo registra</b>.
    ///
    /// Es una fecha y no un instante a propósito: «vendi el 3 de marzo» es lo que se sabe, y fingir una hora
    /// daría una precisión que nadie tiene.
    /// </summary>
    public DateOnly OccurredOn { get; private set; }

    /// <summary>
    /// El importe del movimiento, en su moneda.
    ///
    /// En una aportación o un retiro es <b>el dinero</b>. En una venta es <b>lo que se cobró</b>, y tiene que
    /// coincidir con cantidad × precio: se calcula al crear y no se acepta de fuera, para que no puedan
    /// decir cosas distintas.
    /// </summary>
    public Money Amount { get; private set; }

    /// <summary>La posición vendida. Nula en las aportaciones y los retiros.</summary>
    public Guid? PositionId { get; private set; }

    /// <summary>Cuántas unidades se vendieron. Nula en las aportaciones y los retiros.</summary>
    public Quantity? Quantity { get; private set; }

    /// <summary>A qué precio se vendió cada unidad. Nula en las aportaciones y los retiros.</summary>
    public Money? PricePerUnit { get; private set; }

    public string? Notes { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>Dinero que entra desde fuera.</summary>
    public static Movement Deposit(
        Guid portfolioId,
        Money amount,
        DateOnly occurredOn,
        string? notes,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(amount);
        ExigirCartera(portfolioId);
        ExigirPositivo(amount, nameof(amount));

        return new Movement(
            Guid.NewGuid(), portfolioId, MovementKind.Deposit, occurredOn, amount,
            positionId: null, quantity: null, pricePerUnit: null, notes, now);
    }

    /// <summary>Dinero que sale hacia fuera.</summary>
    public static Movement Withdrawal(
        Guid portfolioId,
        Money amount,
        DateOnly occurredOn,
        string? notes,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(amount);
        ExigirCartera(portfolioId);
        ExigirPositivo(amount, nameof(amount));

        return new Movement(
            Guid.NewGuid(), portfolioId, MovementKind.Withdrawal, occurredOn, amount,
            positionId: null, quantity: null, pricePerUnit: null, notes, now);
    }

    /// <summary>
    /// Una venta: cuántas unidades y a qué precio.
    ///
    /// El importe <b>se calcula</b> y no se recibe. Si se pudiera pasar, alguien podría registrar «10 unidades
    /// a 50» con un importe de 900, y la ganancia realizada saldría mal sin que nada lo delatara.
    /// </summary>
    public static Movement Sale(
        Guid portfolioId,
        Guid positionId,
        Quantity quantity,
        Money pricePerUnit,
        DateOnly occurredOn,
        string? notes,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(quantity);
        ArgumentNullException.ThrowIfNull(pricePerUnit);

        ExigirCartera(portfolioId);

        if (positionId == Guid.Empty)
        {
            throw new ArgumentException("Una venta tiene que decir de qué posición.", nameof(positionId));
        }

        if (quantity.Value <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity), quantity.Value, "La cantidad vendida tiene que ser mayor que cero.");
        }

        ExigirPositivo(pricePerUnit, nameof(pricePerUnit));

        return new Movement(
            Guid.NewGuid(), portfolioId, MovementKind.Sale, occurredOn,
            quantity * pricePerUnit,
            positionId, quantity, pricePerUnit, notes, now);
    }

    /// <summary>Dice si es dinero que entra o sale, en vez de una operación sobre un valor.</summary>
    public bool IsCash => Kind is MovementKind.Deposit or MovementKind.Withdrawal;

    /// <summary>
    /// El efecto sobre el dinero <b>aportado</b>: positivo si entró y negativo si salió.
    ///
    /// Una venta <b>no</b> cambia lo aportado: el dinero ya estaba dentro, solo cambió de forma. Confundir eso
    /// es lo que hace que un informe de rentabilidad salga mal.
    /// </summary>
    public Money CashFlow() => Kind switch
    {
        MovementKind.Deposit => Amount,
        MovementKind.Withdrawal => Amount.Negate(),
        _ => Money.Of(0m, Amount.Currency),
    };

    /// <summary>
    /// Todo movimiento pertenece a una cartera. Se comprueba en un solo sitio para que no se olvide en uno de
    /// los tres: la primera versión la tenía escrita solo en la venta.
    /// </summary>
    private static void ExigirCartera(Guid portfolioId)
    {
        if (portfolioId == Guid.Empty)
        {
            throw new ArgumentException("Un movimiento tiene que pertenecer a una cartera.", nameof(portfolioId));
        }
    }

    private static void ExigirPositivo(Money importe, string parametro)
    {
        if (importe.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(parametro, importe.Amount, "El importe tiene que ser mayor que cero.");
        }
    }
}
