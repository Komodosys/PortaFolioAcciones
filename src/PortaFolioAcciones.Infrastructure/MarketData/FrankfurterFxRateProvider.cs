using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Infrastructure.MarketData;

/// <summary>
/// Consigue tipos de cambio de Frankfurter, que publica los del Banco Central Europeo y es de uso libre
/// sin registro ni cuota ([ADR-004](../../../DOCUMENTACION/decisiones/ADR-004-fuentes-gratuitas.md)).
///
/// Se pide <b>una vez por moneda</b>, porque el servicio solo admite una moneda base por consulta. Para
/// una cartera familiar eso son una o dos consultas.
///
/// <b>Esto es la versión básica.</b> La caché por frecuencia, los reintentos y la cadena de respaldo son
/// de la Fase 5. Aquí, si la fuente no responde, se devuelve <b>sin ese par</b> y quien pregunte decide:
/// lo que no se hace nunca es inventarse un uno a uno para poder seguir sumando.
/// </summary>
internal sealed partial class FrankfurterFxRateProvider : IFxRateProvider
{
    private const string Ruta = "v1/latest";

    private readonly HttpClient _http;
    private readonly ILogger<FrankfurterFxRateProvider> _logger;

    public FrankfurterFxRateProvider(HttpClient http, ILogger<FrankfurterFxRateProvider> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<IReadOnlyList<FxRate>> GetRatesAsync(
        Currency baseCurrency,
        IEnumerable<Currency> currencies,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseCurrency);
        ArgumentNullException.ThrowIfNull(currencies);

        // La moneda base consigo misma no necesita consulta: es la identidad, y eso no es inventarse
        // nada.
        var pendientes = currencies
            .Where(moneda => moneda != baseCurrency)
            .Distinct()
            .ToList();

        if (pendientes.Count == 0)
        {
            return [];
        }

        var tasas = new List<FxRate>();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var moneda in pendientes)
        {
            var tasa = await PedirAsync(moneda, baseCurrency, hoy, cancellationToken);

            if (tasa is not null)
            {
                tasas.Add(tasa);
            }
        }

        return tasas;
    }

    private async Task<FxRate?> PedirAsync(
        Currency from,
        Currency to,
        DateOnly hoy,
        CancellationToken cancellationToken)
    {
        var ruta = $"{Ruta}?base={Uri.EscapeDataString(from.Code)}&symbols={Uri.EscapeDataString(to.Code)}";

        try
        {
            using var respuesta = await _http.GetAsync(new Uri(ruta, UriKind.Relative), cancellationToken);

            if (!respuesta.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "No se pudo conseguir el tipo de cambio de {Desde} a {Hasta}: respondio {Codigo}.",
                    from.Code, to.Code, (int)respuesta.StatusCode);

                return null;
            }

            var datos = await respuesta.Content.ReadFromJsonAsync<FrankfurterResponse>(cancellationToken);

            if (datos?.Rates is null || !datos.Rates.TryGetValue(to.Code, out var valor) || valor <= 0m)
            {
                _logger.LogWarning(
                    "La respuesta del tipo de cambio de {Desde} a {Hasta} no traia un valor utilizable.",
                    from.Code, to.Code);

                return null;
            }

            // Se usa la fecha que declara la fuente, y no la de hoy: los tipos no se publican los fines
            // de semana ni los festivos, y decir que es de hoy cuando es del viernes seria mentir sobre
            // la frescura del dato.
            var fecha = DateOnly.TryParse(datos.Date, out var publicada) ? publicada : hoy;

            return FxRate.Of(from, to, valor, fecha);
        }
        catch (HttpRequestException excepcion)
        {
            _logger.LogWarning(
                excepcion, "No se pudo conseguir el tipo de cambio de {Desde} a {Hasta}.", from.Code, to.Code);

            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("El tipo de cambio de {Desde} a {Hasta} agoto el tiempo de espera.", from.Code, to.Code);
            return null;
        }
    }

    /// <summary>Lo que devuelve el servicio. Solo se declara lo que se usa.</summary>
    private sealed record FrankfurterResponse(
        [property: JsonPropertyName("base")] string? Base,
        [property: JsonPropertyName("date")] string? Date,
        [property: JsonPropertyName("rates")] Dictionary<string, decimal>? Rates);
}
