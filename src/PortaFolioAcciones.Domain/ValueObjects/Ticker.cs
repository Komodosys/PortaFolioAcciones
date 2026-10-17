namespace PortaFolioAcciones.Domain.ValueObjects;

/// <summary>
/// El simbolo con el que se identifica un instrumento.
///
/// Se acepta deliberadamente un abanico amplio, porque en la cartera real conviven formas muy
/// distintas y todas son validas:
///
/// <list type="bullet">
///   <item><description><c>VEEV</c>, <c>MGK</c>: acciones y ETF de Estados Unidos.</description></item>
///   <item><description><c>GIB.A</c>: clases de acciones canadienses con punto.</description></item>
///   <item><description><c>0P0001K312.TO</c>: fondos canadienses, con un codigo interno opaco.</description></item>
///   <item><description><c>TDB2624C</c>: el codigo del fondo en el broker, que <b>no</b> existe en la
///   fuente de precios y hay que resolver a mano una vez.</description></item>
/// </list>
///
/// Este tipo representa el simbolo <b>de pantalla</b>. El identificador en cada fuente de datos se
/// guarda aparte, porque el mismo instrumento puede llamarse distinto en cada sitio.
/// </summary>
public sealed class Ticker : IEquatable<Ticker>
{
    /// <summary>Longitud maxima admitida.</summary>
    public const int MaxLength = 24;

    private Ticker(string value) => Value = value;

    /// <summary>Simbolo normalizado a mayusculas.</summary>
    public string Value { get; }

    /// <summary>Crea un simbolo.</summary>
    /// <exception cref="ArgumentException">Si esta vacio o contiene caracteres no admitidos.</exception>
    public static Ticker From(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var trimmed = value.Trim();

        if (trimmed.Length > MaxLength)
        {
            throw new ArgumentException(
                $"Un simbolo no puede pasar de {MaxLength} caracteres. Se recibio '{value}'.",
                nameof(value));
        }

        if (!char.IsAsciiLetterOrDigit(trimmed[0]))
        {
            throw new ArgumentException(
                $"Un simbolo debe empezar por letra o cifra. Se recibio '{value}'.",
                nameof(value));
        }

        if (!trimmed.All(IsAllowedCharacter))
        {
            throw new ArgumentException(
                $"Un simbolo solo admite letras, cifras, punto y guion. Se recibio '{value}'.",
                nameof(value));
        }

        return new Ticker(trimmed.ToUpperInvariant());
    }

    private static bool IsAllowedCharacter(char c) =>
        char.IsAsciiLetterOrDigit(c) || c is '.' or '-';

    public bool Equals(Ticker? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as Ticker);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;

    public static bool operator ==(Ticker? left, Ticker? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(Ticker? left, Ticker? right) => !(left == right);
}
