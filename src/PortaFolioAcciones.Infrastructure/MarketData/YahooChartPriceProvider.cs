using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Application.MarketData;
using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Infrastructure.MarketData;

/// <summary>
/// Consigue el precio de la fuente de mercado, pidiendo el grafico diario con sus eventos.
///
/// <b>Se pide `events=div,splits` y no solo el precio</b>, y eso no es un adorno: es lo que permite
/// detectar un desdoblamiento. Sin eso, un 5:1 como el de MGK llegaría como una caída del 80 % de un día
/// para otro, y la cartera se leería como si se hubiera perdido casi todo.
///
/// <b>Dos reglas, y las dos se comprobaron contra datos reales:</b>
/// <list type="bullet">
///   <item><description>Se lee <c>close</c> y <b>nunca</b> <c>adjclose</c>. El cierre ajustado ya descuenta
///   los desdoblamientos y los dividendos: usarlo para valorar una posición que ya aplicó el split sería
///   contar el efecto dos veces, y el precio saldría mal sin que nadie se diera cuenta.</description></item>
///   <item><description>Se lee el <b>último cierre no nulo</b>. Los fondos no publican todos los días, y en
///   un festivo la casilla viene vacía: saltarse a la última que tenga valor es lo único que da un precio
///   sin inventarse uno.</description></item>
/// </list>
///
/// Esto es la <b>versión básica</b>. La caché, los reintentos y la cadena de respaldo son de puntos
/// posteriores de la Fase 5.
/// </summary>
internal sealed partial class YahooChartPriceProvider : IPriceProvider
{
    private const string Ruta = "v8/finance/chart";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<YahooChartPriceProvider> _logger;

    public YahooChartPriceProvider(HttpClient http, ILogger<YahooChartPriceProvider> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<MarketQuote?> GetQuoteAsync(
        Instrument instrument,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instrument);

        if (string.IsNullOrWhiteSpace(instrument.ProviderKey))
        {
            _logger.LogDebug("El instrumento {Simbolo} no tiene clave de fuente: no se puede pedir su precio.",
                instrument.Symbol.Value);
            return null;
        }

        var ruta = $"{Ruta}/{Uri.EscapeDataString(instrument.ProviderKey)}" +
                   "?range=1mo&interval=1d&events=div%2Csplits";

        try
        {
            using var respuesta = await _http.GetAsync(new Uri(ruta, UriKind.Relative), cancellationToken);

            if (!respuesta.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "La fuente devolvio {Codigo} al pedir el precio de {Clave}.",
                    (int)respuesta.StatusCode, instrument.ProviderKey);
                return null;
            }

            var datos = await respuesta.Content.ReadFromJsonAsync<YahooChartResponse>(JsonOptions, cancellationToken);

            var resultado = datos?.Chart?.Result?.FirstOrDefault();

            if (resultado is null || resultado.Timestamp is not { Count: > 0 })
            {
                return null;
            }

            var cierres = resultado.Indicators?.Quote?.FirstOrDefault()?.Close;

            if (cierres is null || cierres.Count != resultado.Timestamp.Count)
            {
                return null;
            }

            // El ultimo cierre NO nulo. Los festivos y los fondos dejan casillas vacias, y la posicion
            // correcta es la ultima que de verdad tiene un valor.
            var indice = EncontrarUltimoCierre(cierres);

            if (indice < 0 || cierres[indice] is not { } valor)
            {
                return null;
            }

            var moneda = ResolverMoneda(resultado.Meta?.Currency, instrument.QuoteCurrency);
            var instante = DateTimeOffset.FromUnixTimeSeconds(resultado.Timestamp[indice]);

            return new MarketQuote(
                Money.Of(valor, moneda),
                instante,
                DateOnly.FromDateTime(instante.UtcDateTime),
                [.. DividirEventos(resultado.Events?.Splits)],
                [.. DividirDividendos(resultado.Events?.Dividends)],
                "yahoo");
        }
        catch (HttpRequestException excepcion)
        {
            _logger.LogWarning(excepcion, "No se pudo pedir el precio de {Clave}.", instrument.ProviderKey);
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("El precio de {Clave} agoto el tiempo de espera.", instrument.ProviderKey);
            return null;
        }
    }

    /// <summary>
    /// Busca de atrás hacia delante el último cierre con valor. Devolver la última casilla sin mirar si
    /// está vacía daría un precio nulo justo en los días que más importa leer con cuidado.
    /// </summary>
    private static int EncontrarUltimoCierre(IReadOnlyList<decimal?> cierres)
    {
        for (var i = cierres.Count - 1; i >= 0; i--)
        {
            if (cierres[i] is not null)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// La moneda que declara la fuente, o la del instrumento si la fuente no la dice. Se usa la del
    /// instrumento como respaldo y no al revés, porque la fuente es quien cotiza el precio.
    /// </summary>
    private static Currency ResolverMoneda(string? codigo, Currency respaldo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return respaldo;
        }

        try
        {
            return Currency.FromCode(codigo);
        }
        catch (ArgumentException)
        {
            return respaldo;
        }
    }

    private static IEnumerable<SplitEvent> DividirEventos(IReadOnlyDictionary<string, YahooSplit>? eventos)
    {
        if (eventos is null)
        {
            return [];
        }

        return eventos.Values
            .Where(e => e.Numerator > 0m && e.Denominator > 0m)
            .Select(e => new SplitEvent(
                DateTimeOffset.FromUnixTimeSeconds(e.Date),
                e.Numerator / e.Denominator,
                e.SplitRatio))
            .ToList();
    }

    private static IEnumerable<DividendEvent> DividirDividendos(IReadOnlyDictionary<string, YahooDividend>? eventos)
    {
        if (eventos is null)
        {
            return [];
        }

        return eventos.Values
            .Where(e => e.Amount > 0m)
            .Select(e => new DividendEvent(
                DateTimeOffset.FromUnixTimeSeconds(e.Date),
                e.Amount))
            .ToList();
    }

    /// <summary>La respuesta del grafico. Solo se declara lo que se usa.</summary>
    private sealed record YahooChartResponse(
        [property: JsonPropertyName("chart")] YahooChart? Chart);

    private sealed record YahooChart(
        [property: JsonPropertyName("result")] IReadOnlyList<YahooResult?>? Result,
        [property: JsonPropertyName("error")] object? Error);

    private sealed record YahooResult(
        [property: JsonPropertyName("meta")] YahooMeta? Meta,
        [property: JsonPropertyName("timestamp")] IReadOnlyList<long>? Timestamp,
        [property: JsonPropertyName("indicators")] YahooIndicators? Indicators,
        [property: JsonPropertyName("events")] YahooEvents? Events);

    private sealed record YahooMeta(
        [property: JsonPropertyName("currency")] string? Currency,
        [property: JsonPropertyName("symbol")] string? Symbol,
        [property: JsonPropertyName("regularMarketPrice")] decimal? RegularMarketPrice);

    private sealed record YahooIndicators(
        [property: JsonPropertyName("quote")] IReadOnlyList<YahooQuoteSeries>? Quote);

    private sealed record YahooQuoteSeries(
        [property: JsonPropertyName("close")] IReadOnlyList<decimal?>? Close);

    private sealed record YahooEvents(
        [property: JsonPropertyName("splits")] IReadOnlyDictionary<string, YahooSplit>? Splits,
        [property: JsonPropertyName("dividends")] IReadOnlyDictionary<string, YahooDividend>? Dividends);

    private sealed record YahooSplit(
        [property: JsonPropertyName("date")] long Date,
        [property: JsonPropertyName("numerator")] decimal Numerator,
        [property: JsonPropertyName("denominator")] decimal Denominator,
        [property: JsonPropertyName("splitRatio")] string? SplitRatio);

    private sealed record YahooDividend(
        [property: JsonPropertyName("date")] long Date,
        [property: JsonPropertyName("amount")] decimal Amount);
}
