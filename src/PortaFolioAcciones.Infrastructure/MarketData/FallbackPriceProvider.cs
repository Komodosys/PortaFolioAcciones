using Microsoft.Extensions.Logging;
using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Application.MarketData;
using PortaFolioAcciones.Domain.Entities;

namespace PortaFolioAcciones.Infrastructure.MarketData;

/// <summary>
/// La cadena de respaldo: prueba las fuentes en orden y, si ninguna responde, usa el último precio
/// conocido.
///
/// El último escalón es el que de verdad importa hoy, y existe porque las alternativas son peores:
/// <b>dejar la posición sin valorar rompe el total</b>, y <b>poner un cero es mentir</b>. Un precio viejo,
/// dicho como viejo, al menos permite seguir leyendo la cartera mientras la fuente no responde.
///
/// Lo que <b>no</b> hace nunca es inventar: si no hay ni fuente ni precio guardado, devuelve
/// <c>null</c>, y quien llama decide qué decir. Un hueco silencioso sería peor que un hueco.
/// </summary>
internal sealed class FallbackPriceProvider : IPriceProvider
{
    private readonly IReadOnlyList<IPriceProvider> _fuentes;
    private readonly ILastKnownPrice _ultimoConocido;
    private readonly ILogger<FallbackPriceProvider> _registro;

    public FallbackPriceProvider(
        IReadOnlyList<IPriceProvider> fuentes,
        ILastKnownPrice ultimoConocido,
        ILogger<FallbackPriceProvider> registro)
    {
        _fuentes = fuentes;
        _ultimoConocido = ultimoConocido;
        _registro = registro;
    }

    public async Task<MarketQuote?> GetQuoteAsync(
        Instrument instrument,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instrument);

        foreach (var fuente in _fuentes)
        {
            var cotizacion = await fuente.GetQuoteAsync(instrument, cancellationToken);

            // Una fuente que devuelve un respaldo no cuenta como exito: si no se comprobara, una fuente
            // que arrastra su propio ultimo precio taparia a la siguiente de la cadena sin que nadie se
            // enterara de que la primera esta caida.
            if (cotizacion is not null && !cotizacion.IsFallback)
            {
                return cotizacion;
            }
        }

        var ultimo = await _ultimoConocido.GetAsync(instrument, cancellationToken);

        if (ultimo is null)
        {
            _registro.LogWarning(
                "Ninguna fuente respondio y no hay precio guardado para {Clave}.",
                instrument.ProviderKey);

            return null;
        }

        // Se avisa en el registro porque esto es una degradacion: la aplicacion sigue funcionando, pero
        // con un dato que puede tener dias. Que quede rastro es lo que permite enterarse de que una
        // fuente lleva caida una semana sin que nadie lo haya notado.
        _registro.LogWarning(
            "Ninguna fuente respondio para {Clave}: se usa el ultimo precio conocido, del {Fecha}.",
            instrument.ProviderKey,
            ultimo.PriceDate);

        return ultimo;
    }
}
