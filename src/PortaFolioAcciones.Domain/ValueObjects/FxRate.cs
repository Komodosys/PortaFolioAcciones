namespace PortaFolioAcciones.Domain.ValueObjects;

/// <summary>
/// Un tipo de cambio entre dos monedas, <b>con la fecha a la que corresponde</b>.
///
/// La fecha no es un adorno: sin ella no se puede auditar de donde salio un numero. Cada conversion
/// que se haga en el sistema debe poder responder «con que tipo de cambio y de que dia».
///
/// Se guardan <b>pares directos</b> (USD/CAD tal como lo publica la fuente) y no todo cruzado contra
/// una moneda comun, porque un proveedor puede dar mejor un par concreto y guardarlo tal cual permite
/// comprobar de donde vino el numero.
/// </summary>
public sealed class FxRate : IEquatable<FxRate>
{
    private FxRate(Currency from, Currency to, decimal rate, DateOnly rateDate)
    {
        From = from;
        To = to;
        Rate = rate;
        RateDate = rateDate;
    }

    /// <summary>Moneda de origen.</summary>
    public Currency From { get; }

    /// <summary>Moneda de destino.</summary>
    public Currency To { get; }

    /// <summary>Cuanto vale una unidad de <see cref="From"/> en <see cref="To"/>.</summary>
    public decimal Rate { get; }

    /// <summary>Fecha a la que corresponde el tipo de cambio.</summary>
    public DateOnly RateDate { get; }

    /// <summary>Crea un tipo de cambio.</summary>
    /// <exception cref="ArgumentException">Si el tipo no es positivo.</exception>
    public static FxRate Of(Currency from, Currency to, decimal rate, DateOnly rateDate)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        if (rate <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rate), rate,
                "Un tipo de cambio tiene que ser mayor que cero. Un cero o un valor negativo no " +
                "convierten nada.");
        }

        if (from == to && rate != 1m)
        {
            throw new ArgumentException(
                $"Un tipo de cambio de {from} a {from} solo puede valer 1, y vale {rate}.",
                nameof(rate));
        }

        return new FxRate(from, to, rate, rateDate);
    }

    /// <summary>El tipo de cambio de una moneda consigo misma, que es siempre 1.</summary>
    public static FxRate Identity(Currency currency, DateOnly rateDate)
    {
        ArgumentNullException.ThrowIfNull(currency);
        return new FxRate(currency, currency, 1m, rateDate);
    }

    /// <summary>Dice si este tipo de cambio sirve para convertir desde una moneda dada.</summary>
    public bool ConvertsFrom(Currency currency)
    {
        ArgumentNullException.ThrowIfNull(currency);
        return From == currency;
    }

    /// <summary>Convierte un importe a la moneda de destino.</summary>
    /// <exception cref="InvalidOperationException">Si el importe no esta en la moneda de origen.</exception>
    public Money Convert(Money source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Currency != From)
        {
            throw new InvalidOperationException(
                $"Este tipo de cambio convierte de {From} a {To}, y se intento convertir un importe " +
                $"en {source.Currency}.");
        }

        return Money.Of(source.Amount * Rate, To);
    }

    /// <summary>Devuelve el tipo de cambio inverso, redondeando lo menos posible.</summary>
    public FxRate Invert() => new(To, From, 1m / Rate, RateDate);

    public bool Equals(FxRate? other) =>
        other is not null
        && From == other.From
        && To == other.To
        && Rate == other.Rate
        && RateDate == other.RateDate;

    public override bool Equals(object? obj) => Equals(obj as FxRate);

    public override int GetHashCode() => HashCode.Combine(From, To, Rate, RateDate);

    public override string ToString() =>
        string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{From}/{To} = {Rate} ({RateDate:yyyy-MM-dd})");
}
