using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Application.MarketData;
using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Infrastructure.MarketData;

/// <summary>
/// Guarda los precios ya pedidos para no volver a preguntar por ellos en cada consulta.
///
/// <b>Cuánto dura cada precio depende de cada cuánto cambia</b>, y ahí está todo el asunto: un precio
/// intradía caduca en minutos, y un valor liquidativo de un fondo, que se publica una vez al día,
/// aguantaría perfectamente todo el día. Con una sola duración para todo, o se pregunta de más a la
/// fuente (gastando cuota) o se sirve un precio viejo como si fuera de ahora.
///
/// <b>La decisión de frescura se toma con un reloj inyectable</b>, no con el del sistema. No es un
/// capricho de diseño: si la caducidad la decidiera la caché por su cuenta, probar que un precio de
/// intradía caduca a los quince minutos exigiría esperar quince minutos de verdad, y esa prueba no se
/// escribiría nunca.
///
/// <b>Un fallo no se guarda.</b> Si la fuente no responde, no se cachea el hueco: hacerlo dejaría el
/// precio en blanco durante toda la ventana aunque la fuente se recuperara al minuto siguiente.
/// </summary>
internal sealed class CachedPriceProvider : IPriceProvider
{
    /// <summary>
    /// Cuánto aguanta un precio según cada cuánto cambia.
    /// </summary>
    /// <remarks>
    /// El de cierre diario dura doce horas y no veinticuatro, como aproximación a «hasta el próximo
    /// cierre». La duración exacta la dará el calendario de mercado, que todavía no está: mientras tanto
    /// se prefiere preguntar una vez de más a servir un cierre viejo como si fuera el de hoy.
    /// </remarks>
    public static readonly TimeSpan TtlIntradia = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan TtlCierreDiario = TimeSpan.FromHours(12);
    public static readonly TimeSpan TtlValorLiquidativo = TimeSpan.FromHours(24);

    /// <summary>
    /// Caducidad de respaldo dentro de la caché, muy por encima de cualquier TTL real.
    ///
    /// Es una cuestión de <b>memoria</b>, no de frescura: solo evita que una entrada viva para siempre si
    /// nadie vuelve a pedirla. Quien decide si un precio sigue valiendo es <see cref="TtlFor"/>, con el
    /// reloj inyectado. Por eso este respaldo se mide con el reloj de verdad —la caché no admite otro— y
    /// la frescura con el inyectado: son dos preguntas distintas.
    /// </summary>
    private static readonly TimeSpan Respaldo = TimeSpan.FromDays(2);

    private readonly IPriceProvider _interior;
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _reloj;
    private readonly ILogger<CachedPriceProvider> _registro;

    public CachedPriceProvider(
        IPriceProvider interior,
        IMemoryCache cache,
        TimeProvider reloj,
        ILogger<CachedPriceProvider> registro)
    {
        _interior = interior;
        _cache = cache;
        _reloj = reloj;
        _registro = registro;
    }

    public async Task<MarketQuote?> GetQuoteAsync(
        Instrument instrument,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instrument);

        var ttl = TtlFor(instrument.PricingFrequency);

        // Sin duración no hay caché: se pregunta siempre. Es el caso del precio manual, que no se pide a
        // ninguna fuente, así que no hay nada que guardar.
        if (ttl <= TimeSpan.Zero)
        {
            return await _interior.GetQuoteAsync(instrument, cancellationToken);
        }

        var clave = Clave(instrument);

        if (_cache.TryGetValue(clave, out Entrada? entrada) && entrada is not null
            && _reloj.GetUtcNow() - entrada.GuardadoEnUtc < ttl)
        {
            _registro.LogDebug("Precio de {Clave} servido de la cache.", clave);
            return entrada.Cotizacion;
        }

        var cotizacion = await _interior.GetQuoteAsync(instrument, cancellationToken);

        if (cotizacion is null)
        {
            return null;
        }

        // Un respaldo NO se guarda. Guardarlo prolongaria la averia: si la fuente se recupera al minuto
        // siguiente, el ultimo precio conocido seguiria sirviendose hasta que caducara la entrada, y en un
        // cierre diario eso son doce horas. Se prefiere volver a preguntar; de no machacar una fuente
        // caida se encarga el interruptor de circuito, no la cache.
        if (cotizacion.IsFallback)
        {
            return cotizacion;
        }

        _cache.Set(
            clave,
            new Entrada(cotizacion, _reloj.GetUtcNow()),
            new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Respaldo });

        return cotizacion;
    }

    /// <summary>Cuánto dura un precio según cada cuánto cambia.</summary>
    public static TimeSpan TtlFor(PricingFrequency frecuencia) => frecuencia switch
    {
        PricingFrequency.IntradayDelayed => TtlIntradia,
        PricingFrequency.EndOfDay => TtlCierreDiario,
        PricingFrequency.DailyNav => TtlValorLiquidativo,

        // El precio manual no viene de ninguna fuente, así que no hay nada que cachear.
        _ => TimeSpan.Zero,
    };

    /// <summary>
    /// Con qué nombre se guarda. Se usa la clave de la fuente y no el identificador del instrumento
    /// porque un mismo instrumento apuntado dos veces —o uno que todavía no está guardado— comparte
    /// precio, y sería absurdo preguntarlo dos veces.
    /// </summary>
    private static string Clave(Instrument instrument) =>
        $"{instrument.ProviderKey}|{instrument.QuoteCurrency.Code}|{instrument.PricingFrequency}";

    /// <summary>Lo que se guarda: el precio y cuándo se guardó.</summary>
    private sealed record Entrada(MarketQuote Cotizacion, DateTimeOffset GuardadoEnUtc);
}
