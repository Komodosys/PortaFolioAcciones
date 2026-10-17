using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Application.MarketData;
using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.ValueObjects;
namespace PortaFolioAcciones.Infrastructure.Tests.Ayudas;

/// <summary>
/// Una fuente de precios de mentira que cuenta cuántas veces se la ha llamado.
///
/// El recuento es lo que hace útil la prueba: la pregunta «¿funciona la caché?» se responde contando
/// llamadas, no mirando si el precio es el mismo. Un precio idéntico también saldría si se preguntara
/// cien veces al mismo sitio.
/// </summary>
internal sealed class ProveedorFalso : IPriceProvider
{
    public int Llamadas { get; private set; }

    public MarketQuote? Respuesta { get; set; }

    public Task<MarketQuote?> GetQuoteAsync(Instrument instrument, CancellationToken cancellationToken = default)
    {
        Llamadas++;
        return Task.FromResult(Respuesta);
    }
}

/// <summary>Instrumentos y cotizaciones de mentira, para no repetir la construcción en cada prueba.</summary>
internal static class Datos
{
    public static Instrument Instrumento(
        string clave = "AAPL",
        ValuationMode modo = ValuationMode.Market,
        PricingFrequency frecuencia = PricingFrequency.IntradayDelayed,
        Currency? moneda = null)
    {
        var ahora = new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

        var instrumento = Instrument.Create(
            Ticker.From(clave),
            "Instrumento de prueba",
            AssetType.Equity,
            moneda ?? Currency.Usd,
            "XNYS",
            modo,
            frecuencia,
            ahora);

        instrumento.SetProviderKey(clave, ahora);
        return instrumento;
    }

    public static MarketQuote Cotizacion(
        decimal precio = 100m,
        Currency? moneda = null,
        bool esRespaldo = false,
        string origen = "prueba")
    {
        var ahora = new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

        return new MarketQuote(
            Money.Of(precio, moneda ?? Currency.Usd),
            ahora,
            DateOnly.FromDateTime(ahora.UtcDateTime),
            [],
            [],
            origen,
            esRespaldo);
    }
}

/// <summary>
/// Un almacén de últimos precios conocidos de mentira, que cuenta las veces que se le consulta.
/// </summary>
internal sealed class UltimoPrecioFalso : ILastKnownPrice
{
    public int Llamadas { get; private set; }

    public MarketQuote? Respuesta { get; set; }

    public Task<MarketQuote?> GetAsync(Instrument instrument, CancellationToken cancellationToken = default)
    {
        Llamadas++;
        return Task.FromResult(Respuesta);
    }
}
