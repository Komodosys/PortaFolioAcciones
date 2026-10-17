namespace PortaFolioAcciones.Domain.ValueObjects;

/// <summary>
/// Una cantidad de unidades de un instrumento (acciones, participaciones de un fondo, titulos).
///
/// Se guarda con <see cref="decimal"/> y no con <c>double</c>: los fondos usan fracciones largas
/// (2393,827 participaciones) y la aritmetica binaria de coma flotante no representa esos valores con
/// exactitud. Con dinero, un error de representacion es un error de saldo.
///
/// Admite valores negativos, porque una venta se expresa como cantidad negativa.
/// </summary>
public sealed class Quantity : IEquatable<Quantity>
{
    /// <summary>Decimales que admite una cantidad. Los fondos canadienses usan hasta siete.</summary>
    public const int MaxDecimals = 8;

    private Quantity(decimal value) => Value = value;

    /// <summary>Valor de la cantidad, sin redondear.</summary>
    public decimal Value { get; }

    /// <summary>Crea una cantidad.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Si tiene mas decimales de los permitidos.</exception>
    public static Quantity Of(decimal value)
    {
        if (ScaleOf(value) > MaxDecimals)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value), value,
                $"Una cantidad admite como maximo {MaxDecimals} decimales.");
        }

        return new Quantity(value);
    }

    /// <summary>Cantidad cero, que es una posicion cerrada.</summary>
    public static Quantity Zero => new(0m);

    /// <summary>Dice si la cantidad es exactamente cero (posicion cerrada).</summary>
    public bool IsZero => Value == 0m;

    /// <summary>Dice si la cantidad es negativa (una venta).</summary>
    public bool IsNegative => Value < 0m;

    /// <summary>Devuelve la cantidad con el signo cambiado.</summary>
    public Quantity Negate() => new(-Value);

    /// <summary>Suma dos cantidades.</summary>
    public Quantity Add(Quantity other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new Quantity(Value + other.Value);
    }

    /// <summary>
    /// Calcula que parte de esta cantidad representa otra. Con 34 y 11 devuelve 11/34.
    /// <b>Ojo:</b> para repartir una venta parcial entre lotes lo que hace falta es el complementario,
    /// que es <see cref="RemainingFactorAfterSelling"/>. Confundirlos reparte la venta al reves.
    /// </summary>
    /// <returns>Una proporcion entre 0 y 1, o 0 si esta cantidad es cero.</returns>
    public decimal ProportionOf(Quantity part)
    {
        ArgumentNullException.ThrowIfNull(part);
        return Value == 0m ? 0m : part.Value / Value;
    }

    /// <summary>
    /// Factor por el que hay que multiplicar <b>todos</b> los lotes cuando se vende una parte, para que
    /// la cantidad que queda sea la correcta. Si se venden 11 de 34, devuelve 23/34.
    ///
    /// Se llama asi, y no «proporcion», porque la proporcion de 11 sobre 34 es 11/34: justo lo
    /// contrario. Es la operacion que reparte una venta parcial conservando el coste medio, que es lo
    /// que hace el broker.
    /// </summary>
    public decimal RemainingFactorAfterSelling(Quantity sold)
    {
        ArgumentNullException.ThrowIfNull(sold);
        return Value == 0m ? 0m : (Value - sold.Value) / Value;
    }

    /// <summary>Escala (numero de decimales significativos) de un valor decimal.</summary>
    internal static int ScaleOf(decimal value) => (decimal.GetBits(value)[3] >> 16) & 0xFF;

    public bool Equals(Quantity? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as Quantity);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static Quantity operator +(Quantity left, Quantity right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return left.Add(right);
    }

    /// <summary>Multiplica una cantidad por un precio y devuelve un importe.</summary>
    public static Money operator *(Quantity quantity, Money precio)
    {
        ArgumentNullException.ThrowIfNull(quantity);
        ArgumentNullException.ThrowIfNull(precio);
        return Money.Of(quantity.Value * precio.Amount, precio.Currency);
    }
}
