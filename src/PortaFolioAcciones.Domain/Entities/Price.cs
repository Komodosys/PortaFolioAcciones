using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Domain.Entities;

/// <summary>
/// El cierre de un instrumento en una fecha. Es la <b>fuente de verdad</b> de los precios y tambien el
/// historico que alimentara los graficos.
///
/// La clave es <b>(instrumento, fecha)</b>: un cierre por instrumento y dia. Si dos fuentes dan el
/// mismo dia, no se anade una fila: se corrige la que hay y se anota quien manda.
///
/// Cada precio lleva <b>su fecha y su fuente</b>. La pantalla no debe mostrar nunca un numero sin decir
/// de cuando es: con fondos (valor liquidativo de ayer) y acciones (cierre de hoy) mezclados, "de
/// cuando es" no es un detalle estetico.
/// </summary>
public sealed class Price
{
    private Price(Guid instrumentId, DateOnly priceDate, Money close, string source, DateTimeOffset now)
    {
        InstrumentId = instrumentId;
        PriceDate = priceDate;
        Close = close;
        Source = source;
        RetrievedAtUtc = now;
    }

    /// <summary>Constructor sin parametros para el mapeo de la base de datos.</summary>
    private Price()
    {
        Close = Money.Zero(Currency.Cad);
        Source = string.Empty;
    }

    /// <summary>Instrumento al que corresponde el cierre.</summary>
    public Guid InstrumentId { get; private set; }

    /// <summary>Dia del cierre.</summary>
    public DateOnly PriceDate { get; private set; }

    /// <summary>Valor de cierre, con su moneda.</summary>
    public Money Close { get; private set; }

    /// <summary>De donde salio el dato. Sin esto no se puede auditar un numero.</summary>
    public string Source { get; private set; }

    /// <summary>Cuando se recogio.</summary>
    public DateTimeOffset RetrievedAtUtc { get; private set; }

    /// <summary>Moneda del cierre.</summary>
    public Currency Currency => Close.Currency;

    /// <summary>Crea un cierre.</summary>
    public static Price Create(
        Guid instrumentId,
        DateOnly priceDate,
        Money close,
        string source,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(close);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        if (instrumentId == Guid.Empty)
        {
            throw new ArgumentException("Un precio tiene que ser de algun instrumento.", nameof(instrumentId));
        }

        if (close.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(close), close.Amount,
                "Un precio tiene que ser mayor que cero. Un cero en un instrumento que se posee es casi " +
                "siempre un dato que falta disfrazado de precio, y aceptarlo valoraria la posicion entera " +
                "a cero sin que nadie se entere.");
        }

        return new Price(instrumentId, priceDate, close, source.Trim(), now);
    }

    /// <summary>
    /// Corrige el cierre del mismo dia, por ejemplo porque lo publico otra fuente.
    /// La fecha y el instrumento no cambian: la fila es la misma.
    /// </summary>
    public void Revise(Money close, string source, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(close);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        if (close.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(close), close.Amount,
                "Un precio tiene que ser mayor que cero.");
        }

        if (close.Currency != Close.Currency)
        {
            throw new InvalidOperationException(
                $"El cierre de {PriceDate:yyyy-MM-dd} esta en {Close.Currency} y se intenta corregir " +
                $"con un importe en {close.Currency}. La moneda de un instrumento no cambia de un dia " +
                "para otro.");
        }

        Close = close;
        Source = source.Trim();
        RetrievedAtUtc = now;
    }

    public override string ToString() =>
        string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{PriceDate:yyyy-MM-dd} {Close} ({Source})");
}
