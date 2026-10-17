namespace PortaFolioAcciones.Domain.ValueObjects;

/// <summary>
/// Un porcentaje.
///
/// Se guarda internamente como <b>proporcion</b> (0,15 significa 15 %), no como 15. Es la unica forma
/// de que la aritmetica sea evidente: multiplicar un importe por un porcentaje no obliga a acordarse de
/// dividir entre cien. Para evitar dudas, se construye siempre con un metodo que dice cual de las dos
/// cosas se esta pasando.
/// </summary>
public sealed class Percentage : IEquatable<Percentage>, IComparable<Percentage>
{
    private Percentage(decimal ratio) => Ratio = ratio;

    /// <summary>Proporcion: 0,15 es 15 %, 1 es 100 %, -0,05 es -5 %.</summary>
    public decimal Ratio { get; }

    /// <summary>El mismo valor expresado en tanto por ciento: 15 para 0,15.</summary>
    public decimal AsPercent => Ratio * 100m;

    /// <summary>Crea un porcentaje a partir de una proporción (0,15 = 15 %).</summary>
    public static Percentage FromRatio(decimal ratio) => new(ratio);

    /// <summary>Crea un porcentaje a partir de un valor en tanto por ciento (15 = 15 %).</summary>
    public static Percentage FromPercent(decimal porCiento) => new(porCiento / 100m);

    /// <summary>Porcentaje cero.</summary>
    public static Percentage Zero => new(0m);

    /// <summary>
    /// Calcula que porcentaje representa una parte sobre un total.
    /// Si el total es cero, devuelve cero en vez de fallar: una posicion sin coste no tiene porcentaje.
    /// </summary>
    public static Percentage FromPartOverTotal(decimal part, decimal total) =>
        total == 0m ? Zero : new Percentage(part / total);

    /// <summary>Aplica el porcentaje a un importe.</summary>
    public Money ApplyTo(Money amount)
    {
        ArgumentNullException.ThrowIfNull(amount);
        return amount.MultiplyBy(Ratio);
    }

    public bool Equals(Percentage? other) => other is not null && Ratio == other.Ratio;

    public override bool Equals(object? obj) => Equals(obj as Percentage);

    public override int GetHashCode() => Ratio.GetHashCode();

    public int CompareTo(Percentage? other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Ratio.CompareTo(other.Ratio);
    }

    public override string ToString() =>
        string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{AsPercent} %");
}
