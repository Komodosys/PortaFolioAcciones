using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Api.Endpoints;

/// <summary>
/// Endpoints de desarrollo, para verificar a mano las piezas mientras se construyen.
///
/// <b>No se montan si la aplicación no está en desarrollo.</b> Son andamiaje de la Fase 5: cuando exista
/// el caso de uso de refrescar precios y el trabajo diario, se quitan y se verifica por ahí.
/// </summary>
internal static class DevEndpoints
{
    public static void MapDevEndpoints(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return;
        }

        var group = app.MapGroup("/api/dev")
            .RequireAuthorization()
            .WithTags("Development");

        group.MapGet("/quote", async (
            string symbol,
            string? currency,
            IPriceProvider proveedor,
            CancellationToken cancelacion) =>
        {
            var instrumento = Instrument.Create(
                Ticker.From(symbol),
                symbol,
                AssetType.Equity,
                Currency.FromCode(currency ?? "USD"),
                "XNYS",
                ValuationMode.Market,
                PricingFrequency.IntradayDelayed,
                DateTimeOffset.UtcNow);

            instrumento.SetProviderKey(symbol, DateTimeOffset.UtcNow);

            var cotizacion = await proveedor.GetQuoteAsync(instrumento, cancelacion);

            return cotizacion is null ? Results.NotFound() : Results.Ok(cotizacion);
        })
        .WithName("dev.quote")
        .WithSummary("(Solo desarrollo) Pide el precio actual de un simbolo a la fuente.");

        // Ejecuta el trabajo diario A MANO. Existe porque el trabajo de verdad espera a que cierren las
        // bolsas, y esperar a las diez de la noche para comprobar que funciona no es forma de trabajar.
        // Es idempotente, asi que ejecutarlo de mas no rompe nada.
        group.MapPost("/recolectar", async (
            DateOnly? dia,
            IMarketDataCollector colector,
            CancellationToken cancelacion) =>
        {
            var objetivo = dia ?? DateOnly.FromDateTime(DateTime.UtcNow);

            return Results.Ok(await colector.CollectAsync(objetivo, cancelacion));
        })
        .WithName("dev.collect")
        .WithSummary("(Solo desarrollo) Ejecuta la recoleccion de precios y la foto del dia ahora mismo.");
    }
}
