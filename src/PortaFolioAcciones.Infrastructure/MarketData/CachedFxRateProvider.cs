using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Infrastructure.MarketData;

/// <summary>
/// Guarda los tipos de cambio ya conseguidos.
///
/// Duran <b>veinticuatro horas</b> porque los tipos de referencia se publican una vez al día y no cambian
/// dentro de la jornada. Pedirlos en cada consulta sería gastar peticiones para recibir siempre el mismo
/// número.
///
/// Se guarda <b>par a par</b> y no la consulta entera: si mañana aparece una posición en otra moneda, se
/// pide solo esa, en vez de tirar todo lo que ya se sabía.
///
/// Igual que la caché de precios, la frescura la decide un <b>reloj inyectable</b>, para poder probar la
/// caducidad sin esperar un día entero.
/// </summary>
internal sealed class CachedFxRateProvider : IFxRateProvider
{
    public static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    /// <summary>
    /// Caducidad de respaldo: es una cuestión de memoria, no de frescura. Solo evita que una entrada viva
    /// para siempre sin que nadie la pida. La frescura la decide <see cref="Ttl"/> con el reloj inyectado.
    /// </summary>
    private static readonly TimeSpan Respaldo = TimeSpan.FromDays(3);

    private readonly IFxRateProvider _interior;
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _reloj;
    private readonly ILogger<CachedFxRateProvider> _registro;

    public CachedFxRateProvider(
        IFxRateProvider interior,
        IMemoryCache cache,
        TimeProvider reloj,
        ILogger<CachedFxRateProvider> registro)
    {
        _interior = interior;
        _cache = cache;
        _reloj = reloj;
        _registro = registro;
    }

    public async Task<IReadOnlyList<FxRate>> GetRatesAsync(
        Currency baseCurrency,
        IEnumerable<Currency> currencies,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseCurrency);
        ArgumentNullException.ThrowIfNull(currencies);

        var pedidas = currencies
            .Where(moneda => moneda != baseCurrency)
            .Distinct()
            .ToList();

        if (pedidas.Count == 0)
        {
            return [];
        }

        var tasas = new List<FxRate>();
        var faltan = new List<Currency>();

        foreach (var moneda in pedidas)
        {
            if (_cache.TryGetValue(Clave(baseCurrency, moneda), out Entrada? entrada) && entrada is not null
                && _reloj.GetUtcNow() - entrada.GuardadoEnUtc < Ttl)
            {
                tasas.Add(entrada.Tasa);
            }
            else
            {
                faltan.Add(moneda);
            }
        }

        if (faltan.Count == 0)
        {
            _registro.LogDebug("Tipos de cambio servidos de la cache para {Monedas}.", string.Join(", ", pedidas));
            return tasas;
        }

        var conseguidas = await _interior.GetRatesAsync(baseCurrency, faltan, cancellationToken);

        foreach (var tasa in conseguidas)
        {
            tasas.Add(tasa);

            _cache.Set(
                Clave(tasa.To, tasa.From),
                new Entrada(tasa, _reloj.GetUtcNow()),
                new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Respaldo });
        }

        // Lo que no se pudo conseguir NO se guarda: si se guardara el hueco, un corte momentaneo dejaria
        // la moneda sin tipo de cambio durante un dia entero.
        return tasas;
    }

    /// <summary>Con qué nombre se guarda cada par. El sentido importa: no es lo mismo CAD→USD que USD→CAD.</summary>
    private static string Clave(Currency baseCurrency, Currency moneda) =>
        $"{moneda.Code}->{baseCurrency.Code}";

    private sealed record Entrada(FxRate Tasa, DateTimeOffset GuardadoEnUtc);
}
