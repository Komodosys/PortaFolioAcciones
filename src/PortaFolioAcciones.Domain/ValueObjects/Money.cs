namespace PortaFolioAcciones.Domain.ValueObjects;

/// <summary>
/// Un importe de dinero: una cantidad <b>con su moneda</b>.
///
/// Dos reglas que definen este tipo:
///
/// 1. <b>No se puede operar entre monedas distintas.</b> Add CAD con USD lanza una excepcion en vez
///    de dar un numero sin sentido. La conversion es un paso explicito, con su tipo de cambio.
/// 2. <b>No redondea nunca por su cuenta.</b> La aritmetica conserva toda la precision y el redondeo
///    es una operacion aparte que se pide a proposito. Si se redondeara en cada paso intermedio, los
///    totales dejarian de cuadrar con la suma de las filas y quien lo mira lo nota.
/// </summary>
public sealed class Money : IEquatable<Money>, IComparable<Money>
{
    private Money(decimal amount, Currency currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>Importe, con toda la precision disponible. No esta redondeado.</summary>
    public decimal Amount { get; }

    /// <summary>Moneda del importe.</summary>
    public Currency Currency { get; }

    /// <summary>Crea un importe.</summary>
    public static Money Of(decimal amount, Currency currency)
    {
        ArgumentNullException.ThrowIfNull(currency);
        return new Money(amount, currency);
    }

    /// <summary>Importe cero en una moneda dada.</summary>
    public static Money Zero(Currency currency) => Of(0m, currency);

    /// <summary>Dice si el importe es exactamente cero.</summary>
    public bool IsZero => Amount == 0m;

    /// <summary>Dice si el importe es negativo (una perdida o un coste).</summary>
    public bool IsNegative => Amount < 0m;

    /// <summary>Suma dos importes de la misma moneda.</summary>
    /// <exception cref="InvalidOperationException">Si las monedas no coinciden.</exception>
    public Money Add(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);
        RequireSameCurrency(other, "sumar");
        return new Money(Amount + other.Amount, Currency);
    }

    /// <summary>Resta dos importes de la misma moneda.</summary>
    /// <exception cref="InvalidOperationException">Si las monedas no coinciden.</exception>
    public Money Subtract(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);
        RequireSameCurrency(other, "restar");
        return new Money(Amount - other.Amount, Currency);
    }

    /// <summary>Cambia el signo del importe.</summary>
    public Money Negate() => new(-Amount, Currency);

    /// <summary>Valor absoluto del importe.</summary>
    public Money Abs() => new(Math.Abs(Amount), Currency);

    /// <summary>Multiplica el importe por un factor adimensional (un tipo de cambio, una proporcion).</summary>
    public Money MultiplyBy(decimal factor) => new(Amount * factor, Currency);

    /// <summary>Divide el importe por un divisor adimensional.</summary>
    /// <exception cref="DivideByZeroException">Si el divisor es cero.</exception>
    public Money DivideBy(decimal divisor) =>
        divisor == 0m
            ? throw new DivideByZeroException("No se puede dividir un importe entre cero.")
            : new Money(Amount / divisor, Currency);

    /// <summary>
    /// Redondea el importe a un numero de decimales. <b>Es una operacion de presentacion</b>: se pide
    /// a proposito, nunca ocurre sola.
    /// </summary>
    /// <param name="decimales">Decimales a conservar. Por defecto, los de la moneda.</param>
    public Money Round(int? decimales = null)
    {
        var n = decimales ?? DecimalsFor(Currency);
        return new Money(decimal.Round(Amount, n, MidpointRounding.ToEven), Currency);
    }

    /// <summary>
    /// Decimales con los que se presenta una moneda. Son dos en la mayoria; las monedas sin subunidad,
    /// como el yen o el won, no llevan ninguno.
    /// </summary>
    public static int DecimalsFor(Currency currency)
    {
        ArgumentNullException.ThrowIfNull(currency);

        return currency.Code switch
        {
            // Monedas sin subunidad en circulacion (ISO 4217, exponente 0).
            "BIF" or "CLP" or "DJF" or "GNF" or "ISK" or "JPY" or "KMF" or "KRW" or "PYG"
                or "RWF" or "UGX" or "UYI" or "VND" or "VUV" or "XAF" or "XOF" or "XPF" => 0,

            // Monedas con tres decimales (ISO 4217, exponente 3).
            "BHD" or "IQD" or "JOD" or "KWD" or "LYD" or "OMR" or "TND" => 3,

            _ => 2,
        };
    }

    private void RequireSameCurrency(Money other, string operation)
    {
        if (Currency != other.Currency)
        {
            throw new InvalidOperationException(
                $"No se pueden {operation} importes de monedas distintas: {Currency} y {other.Currency}. " +
                "Convierte primero, con un tipo de cambio explicito.");
        }
    }

    public bool Equals(Money? other) =>
        other is not null && Amount == other.Amount && Currency == other.Currency;

    public override bool Equals(object? obj) => Equals(obj as Money);

    public override int GetHashCode() => HashCode.Combine(Amount, Currency);

    /// <summary>Compara con otro importe de la misma moneda.</summary>
    /// <exception cref="InvalidOperationException">Si las monedas no coinciden.</exception>
    public int CompareTo(Money? other)
    {
        ArgumentNullException.ThrowIfNull(other);
        RequireSameCurrency(other, "comparar");
        return Amount.CompareTo(other.Amount);
    }

    public override string ToString() =>
        string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{Amount} {Currency}");

    public static Money operator +(Money left, Money right)
    {
        ArgumentNullException.ThrowIfNull(left);
        return left.Add(right);
    }

    public static Money operator -(Money left, Money right)
    {
        ArgumentNullException.ThrowIfNull(left);
        return left.Subtract(right);
    }

    public static Money operator -(Money value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Negate();
    }

    public static bool operator <(Money left, Money right)
    {
        ArgumentNullException.ThrowIfNull(left);
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(Money left, Money right)
    {
        ArgumentNullException.ThrowIfNull(left);
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(Money left, Money right)
    {
        ArgumentNullException.ThrowIfNull(left);
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(Money left, Money right)
    {
        ArgumentNullException.ThrowIfNull(left);
        return left.CompareTo(right) >= 0;
    }
}
