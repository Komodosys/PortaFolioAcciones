namespace PortaFolioAcciones.Domain.ValueObjects;

/// <summary>
/// Una moneda, identificada por su codigo ISO 4217 de tres letras (CAD, USD, EUR).
///
/// No es una lista cerrada: el proveedor de tipos de cambio cubre 223 divisas, asi que se acepta
/// cualquier codigo bien formado. Las conocidas se ofrecen como constantes por comodidad.
/// </summary>
public sealed class Currency : IEquatable<Currency>
{
    /// <summary>Dolar canadiense. Es la moneda base del proyecto.</summary>
    public static readonly Currency Cad = new("CAD");

    /// <summary>Dolar estadounidense.</summary>
    public static readonly Currency Usd = new("USD");

    /// <summary>Euro.</summary>
    public static readonly Currency Eur = new("EUR");

    /// <summary>Dolar de Hong Kong, presente en carteras canadienses.</summary>
    public static readonly Currency Hkd = new("HKD");

    private Currency(string code) => Code = code;

    /// <summary>Codigo ISO de tres letras, siempre en mayusculas.</summary>
    public string Code { get; }

    /// <summary>Crea una moneda a partir de su codigo ISO.</summary>
    /// <exception cref="ArgumentNullException">Si el codigo es nulo o esta en blanco.</exception>
    /// <exception cref="ArgumentException">Si el codigo no son tres letras ASCII.</exception>
    public static Currency FromCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        if (code.Length != 3 || !code.All(IsAsciiLetter))
        {
            throw new ArgumentException(
                $"Un codigo de moneda debe ser tres letras, por ejemplo 'CAD'. Se recibio '{code}'.",
                nameof(code));
        }

        var normalized = code.ToUpperInvariant();
        return normalized switch
        {
            "CAD" => Cad,
            "USD" => Usd,
            "EUR" => Eur,
            "HKD" => Hkd,
            _ => new Currency(normalized),
        };
    }

    private static bool IsAsciiLetter(char c) => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    public bool Equals(Currency? other) =>
        other is not null && string.Equals(Code, other.Code, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as Currency);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Code);

    public override string ToString() => Code;

    public static bool operator ==(Currency? left, Currency? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(Currency? left, Currency? right) => !(left == right);
}
