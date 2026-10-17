using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Domain.Services;

/// <summary>
/// Convierte importes a la moneda base usando <b>el tipo de cambio del dia</b>.
///
/// Es la pieza donde aterriza el [ADR-011](../../../DOCUMENTACION/decisiones/ADR-011-coste-al-cambio-de-hoy.md):
/// el coste y el valor se convierten con <b>el mismo</b> tipo, asi que el tipo se cancela al calcular
/// el porcentaje y lo que se muestra es el rendimiento del activo, no el del activo mas la divisa.
///
/// <b>Nunca inventa un tipo de cambio.</b> Si no tiene el par, lo dice: no usa 1:1 ni el de otra fecha
/// en silencio. Toda conversion lleva su fecha, y quien la use debe poder decir cual es.
/// </summary>
public sealed class CurrencyConverter
{
    private readonly Dictionary<string, FxRate> _rates;

    /// <summary>Crea el conversor con los tipos de cambio disponibles.</summary>
    /// <param name="baseCurrency">Moneda a la que se convierte todo.</param>
    /// <param name="rates">Tipos de cambio. Los pares repetidos: gana el ultimo.</param>
    public CurrencyConverter(Currency baseCurrency, IEnumerable<FxRate> rates)
    {
        ArgumentNullException.ThrowIfNull(baseCurrency);
        ArgumentNullException.ThrowIfNull(rates);

        BaseCurrency = baseCurrency;
        _rates = [];

        foreach (var rate in rates)
        {
            _rates[$"{rate.From}>{rate.To}"] = rate;
        }
    }

    /// <summary>Moneda a la que se convierte todo.</summary>
    public Currency BaseCurrency { get; }

    /// <summary>Dice si un importe en esta moneda se puede convertir.</summary>
    public bool CanConvert(Currency currency)
    {
        ArgumentNullException.ThrowIfNull(currency);

        return currency == BaseCurrency
            || _rates.ContainsKey($"{currency}>{BaseCurrency}")
            || _rates.ContainsKey($"{BaseCurrency}>{currency}");
    }

    /// <summary>Convierte un importe, si se puede.</summary>
    /// <returns><c>false</c> si falta el tipo de cambio. El importe de salida queda sin usar.</returns>
    public bool TryToBase(Money amount, out Money converted)
    {
        ArgumentNullException.ThrowIfNull(amount);

        if (amount.Currency == BaseCurrency)
        {
            converted = amount;
            return true;
        }

        // Se prefiere el par directo que publica la fuente; el inverso es un recurso, no lo normal.
        if (_rates.TryGetValue($"{amount.Currency}>{BaseCurrency}", out var direct))
        {
            converted = direct.Convert(amount);
            return true;
        }

        if (_rates.TryGetValue($"{BaseCurrency}>{amount.Currency}", out var inverse))
        {
            converted = inverse.Invert().Convert(amount);
            return true;
        }

        converted = Money.Zero(BaseCurrency);
        return false;
    }

    /// <summary>Convierte un importe a la moneda base.</summary>
    /// <exception cref="InvalidOperationException">Si falta el tipo de cambio.</exception>
    public Money ToBase(Money amount)
    {
        ArgumentNullException.ThrowIfNull(amount);

        return TryToBase(amount, out var converted)
            ? converted
            : throw new InvalidOperationException(
                $"No hay tipo de cambio de {amount.Currency} a {BaseCurrency}. No se convierte a 1:1 ni " +
                "con el de otra fecha: eso daria un numero falso. Hay que conseguir el par antes.");
    }

    /// <summary>Lee de que fecha es el tipo de cambio que se usaria para una moneda.</summary>
    public DateOnly? RateDateFor(Currency currency)
    {
        ArgumentNullException.ThrowIfNull(currency);

        if (currency == BaseCurrency)
        {
            return null;
        }

        if (_rates.TryGetValue($"{currency}>{BaseCurrency}", out var direct))
        {
            return direct.RateDate;
        }

        return _rates.TryGetValue($"{BaseCurrency}>{currency}", out var inverse)
            ? inverse.RateDate
            : null;
    }
}
