using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Domain.Entities;

/// <summary>
/// El precio mas reciente de un instrumento, con su fecha, su fuente y su retraso.
///
/// Es la tabla que lee la pantalla de posiciones, para no tener que agregar la serie historica entera
/// cada vez que alguien la abre. Se puede reconstruir desde <see cref="Price"/>, asi que no es un
/// riesgo grave de consistencia.
/// </summary>
public sealed class LatestQuote
{
    private LatestQuote(
        Guid instrumentId,
        Money price,
        DateTimeOffset asOfUtc,
        string source,
        DateTimeOffset now,
        int? delayMinutes,
        string? marketState)
    {
        InstrumentId = instrumentId;
        Price = price;
        AsOfUtc = asOfUtc;
        Source = source;
        DelayMinutes = delayMinutes;
        MarketState = marketState;
        RetrievedAtUtc = now;
    }

    /// <summary>Constructor sin parametros para el mapeo de la base de datos.</summary>
    private LatestQuote()
    {
        Price = Money.Zero(Currency.Cad);
        Source = string.Empty;
    }

    /// <summary>Instrumento. Es la clave: una fila por instrumento.</summary>
    public Guid InstrumentId { get; private set; }

    /// <summary>Precio mas reciente, con su moneda.</summary>
    public Money Price { get; private set; }

    /// <summary>
    /// A que momento corresponde el precio. <b>No es cuando se recogio</b>: un fondo puede publicar el
    /// valor liquidativo de ayer, y esa es la fecha que hay que enseñar.
    /// </summary>
    public DateTimeOffset AsOfUtc { get; private set; }

    /// <summary>Fuente del dato.</summary>
    public string Source { get; private set; }

    /// <summary>Retraso en minutos, si la fuente lo declara. Quince es lo habitual sin pagar.</summary>
    public int? DelayMinutes { get; private set; }

    /// <summary>Estado del mercado segun la fuente, si lo informa.</summary>
    public string? MarketState { get; private set; }

    /// <summary>Cuando se recogio el dato.</summary>
    public DateTimeOffset RetrievedAtUtc { get; private set; }

    /// <summary>Moneda del precio.</summary>
    public Currency Currency => Price.Currency;

    /// <summary>Dice si el precio llega con retraso.</summary>
    public bool IsDelayed => DelayMinutes is > 0;

    /// <summary>Crea el precio mas reciente de un instrumento.</summary>
    public static LatestQuote Create(
        Guid instrumentId,
        Money price,
        DateTimeOffset asOfUtc,
        string source,
        DateTimeOffset now,
        int? delayMinutes = null,
        string? marketState = null)
    {
        ArgumentNullException.ThrowIfNull(price);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        if (instrumentId == Guid.Empty)
        {
            throw new ArgumentException("Una cotizacion tiene que ser de algun instrumento.", nameof(instrumentId));
        }

        if (price.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(price), price.Amount,
                "Un precio tiene que ser mayor que cero. Un cero en un instrumento que se posee es casi " +
                "siempre un dato que falta disfrazado de precio.");
        }

        if (delayMinutes is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(delayMinutes), delayMinutes, "Un retraso no puede ser negativo.");
        }

        return new LatestQuote(
            instrumentId, price, asOfUtc, source.Trim(), now, delayMinutes, marketState);
    }

    /// <summary>
    /// Sustituye la cotizacion.
    ///
    /// <b>No se acepta un precio mas antiguo que el que ya hay</b>: si dos peticiones se cruzan, la que
    /// llega tarde con un dato viejo no puede pisar uno mas reciente. Es el unico error que esta tabla
    /// puede cometer de verdad.
    /// </summary>
    /// <returns><c>false</c> si se descarto por ser mas antiguo.</returns>
    public bool Update(
        Money price,
        DateTimeOffset asOfUtc,
        string source,
        DateTimeOffset now,
        int? delayMinutes = null,
        string? marketState = null)
    {
        ArgumentNullException.ThrowIfNull(price);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        if (price.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(price), price.Amount, "Un precio tiene que ser mayor que cero.");
        }

        if (price.Currency != Price.Currency)
        {
            throw new InvalidOperationException(
                $"La cotizacion esta en {Price.Currency} y se intenta actualizar con un precio en " +
                $"{price.Currency}.");
        }

        if (asOfUtc < AsOfUtc)
        {
            return false;
        }

        Price = price;
        AsOfUtc = asOfUtc;
        Source = source.Trim();
        DelayMinutes = delayMinutes;
        MarketState = marketState;
        RetrievedAtUtc = now;
        return true;
    }

    /// <summary>
    /// Dice si el precio se ha quedado viejo. Sirve para avisar en la pantalla en vez de mostrar un
    /// numero obsoleto como si fuera de ahora.
    /// </summary>
    public bool IsStale(DateTimeOffset now, TimeSpan maxAge) => now - AsOfUtc > maxAge;
}
