using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Application.Instruments;

namespace PortaFolioAcciones.Infrastructure.MarketData;

/// <summary>
/// Busca instrumentos en la fuente de mercado, por nombre o por símbolo.
///
/// <b>Traduce la bolsa a código MIC.</b> No es un adorno: la fuente devuelve códigos suyos
/// (<c>NMS</c>, <c>NYQ</c>, <c>TOR</c>) y el <see cref="Domain.Entities.Instrument"/> identifica cada
/// instrumento por su bolsa <b>en MIC</b>. Guardar el código de la fuente mezclaría dos vocabularios en
/// la misma columna, y el mismo valor acabaría con dos instrumentos distintos o al revés.
///
/// Cuando la bolsa no se reconoce, <see cref="InstrumentMatch.Mic"/> queda nulo <b>a propósito</b>: es
/// preferible que la pantalla pregunte a que invente un código que no significa nada.
///
/// Los reintentos y el control de cuota son de la Fase 5. Aquí, si la fuente falla, se devuelve una
/// lista vacía y se anota: una búsqueda no es motivo para dejar la aplicación sin responder.
/// </summary>
internal sealed partial class YahooInstrumentSearch : IInstrumentSearch
{
    private const int MaxResults = 15;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Traducción de los códigos de bolsa de la fuente a MIC.
    /// </summary>
    private static readonly Dictionary<string, string> Bolsas = new(StringComparer.OrdinalIgnoreCase)
    {
        // Estados Unidos
        ["NMS"] = "XNAS", ["NGM"] = "XNAS", ["NCM"] = "XNAS", ["NAS"] = "XNAS",
        ["NYQ"] = "XNYS", ["NYS"] = "XNYS",
        ["PCX"] = "ARCX", ["ASE"] = "XASE", ["AMX"] = "XASE", ["BTS"] = "BATS",

        // Canada
        ["TOR"] = "XTSE", ["CVE"] = "XTSX", ["CNQ"] = "XCNQ", ["NEO"] = "NEOE",

        // Europa y resto
        ["LSE"] = "XLON", ["GER"] = "XETR", ["FRA"] = "XFRA", ["PAR"] = "XPAR",
        ["AMS"] = "XAMS", ["MCE"] = "XMAD", ["MIL"] = "XMIL", ["STO"] = "XSTO",
        ["SWX"] = "XSWX", ["VIE"] = "XWBO", ["IST"] = "XIST",
        ["HKG"] = "XHKG", ["TYO"] = "XTKS", ["ASX"] = "XASX", ["NSE"] = "XNSE", ["BSE"] = "XBOM",
    };

    /// <summary>
    /// Traducción del tipo de instrumento de la fuente al del dominio.
    ///
    /// Solo se traduce lo que el dominio <b>sabe representar</b>. Una divisa o una criptomoneda se
    /// quedan fuera a propósito: no son cosas que se posean en una cartera de acciones y fondos, y
    /// forzarlas a un tipo que no les corresponde sería peor que decir que no se sabe.
    /// </summary>
    private static readonly Dictionary<string, string> Tipos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["EQUITY"] = "Equity",
        ["ETF"] = "Etf",
        ["MUTUALFUND"] = "MutualFund",
        ["BOND"] = "Bond",
        ["INDEX"] = "Index",
    };

    private readonly HttpClient _http;
    private readonly ILogger<YahooInstrumentSearch> _logger;

    public YahooInstrumentSearch(HttpClient http, ILogger<YahooInstrumentSearch> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<IReadOnlyList<InstrumentMatch>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var ruta = $"v1/finance/search?q={Uri.EscapeDataString(query.Trim())}" +
                   $"&quotesCount={MaxResults}&newsCount=0&listsCount=0";

        try
        {
            using var respuesta = await _http.GetAsync(new Uri(ruta, UriKind.Relative), cancellationToken);

            if (!respuesta.IsSuccessStatusCode)
            {
                // No se lanza: quien busca prefiere «no he encontrado nada» a un error de servidor.
                _logger.LogWarning(
                    "La busqueda de instrumentos devolvio {Codigo}. Se devuelve vacio.",
                    (int)respuesta.StatusCode);

                return [];
            }

            var datos = await respuesta.Content.ReadFromJsonAsync<YahooSearchResponse>(
                JsonOptions, cancellationToken);

            return
            [
                .. (datos?.Quotes ?? [])
                    .Where(c => !string.IsNullOrWhiteSpace(c.Symbol))
                    .Select(Convertir)
            ];
        }
        catch (HttpRequestException excepcion)
        {
            _logger.LogWarning(excepcion, "No se pudo buscar instrumentos: la fuente no respondio.");
            return [];
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("La busqueda de instrumentos agoto el tiempo de espera.");
            return [];
        }
    }

    private static InstrumentMatch Convertir(YahooQuote cita)
    {
        var symbol = cita.Symbol!;
        var nombre = cita.LongName ?? cita.ShortName ?? symbol;

        // El simbolo que se enseña quita el sufijo de bolsa ('.TO'), porque la bolsa ya va aparte. Si se
        // dejara, el mismo instrumento tendria dos nombres segun de donde salga el dato.
        var punto = symbol.LastIndexOf('.');
        var simboloVisible = punto > 0 ? symbol[..punto] : symbol;

        return new InstrumentMatch(
            ProviderKey: symbol,
            Symbol: simboloVisible,
            Name: nombre,
            Exchange: cita.Exchange,
            Mic: cita.Exchange is not null && Bolsas.TryGetValue(cita.Exchange, out var mic) ? mic : null,
            AssetType: cita.QuoteType is not null && Tipos.TryGetValue(cita.QuoteType, out var tipo) ? tipo : null,
            Currency: cita.Currency,
            Source: "yahoo");
    }

    /// <summary>Lo que devuelve la búsqueda. Solo se declara lo que se usa.</summary>
    private sealed record YahooSearchResponse(
        [property: JsonPropertyName("quotes")] IReadOnlyList<YahooQuote>? Quotes);

    /// <summary>Una cita de la búsqueda.</summary>
    private sealed record YahooQuote(
        [property: JsonPropertyName("symbol")] string? Symbol,
        [property: JsonPropertyName("shortname")] string? ShortName,
        [property: JsonPropertyName("longname")] string? LongName,
        [property: JsonPropertyName("exchange")] string? Exchange,
        [property: JsonPropertyName("exchDisp")] string? ExchangeDisplay,
        [property: JsonPropertyName("quoteType")] string? QuoteType,
        [property: JsonPropertyName("typeDisp")] string? TypeDisplay,
        [property: JsonPropertyName("currency")] string? Currency);
}
