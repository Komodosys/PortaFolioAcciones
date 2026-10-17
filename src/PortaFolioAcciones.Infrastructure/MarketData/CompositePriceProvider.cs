using Microsoft.Extensions.Logging;
using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Application.MarketData;
using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Infrastructure.MarketData;

/// <summary>
/// Enruta la petición de precio según <b>cómo se valora</b> el instrumento.
///
/// Es el sitio donde se decide que un fondo y una acción no se piden igual, y donde se dirá que un
/// instrumento de precio manual <b>no tiene fuente que consultar</b>.
///
/// Existe aunque hoy solo haya una fuente detrás, y no es ceremonia: es el punto por el que entrará la
/// <b>cadena de respaldo</b> (fuente primaria, secundaria, último precio conocido). Sin un sitio único
/// donde se decide de dónde sale el precio, esa cadena acabaría repartida por los sitios que piden
/// precios, y el día que fallara una fuente el comportamiento dependería de quién preguntara.
/// </summary>
internal sealed class CompositePriceProvider : IPriceProvider
{
    private readonly IPriceProvider _mercado;

    public CompositePriceProvider(IPriceProvider mercado) => _mercado = mercado;

    public async Task<MarketQuote?> GetQuoteAsync(
        Instrument instrument,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instrument);

        // Precio puesto a mano: NO hay fuente que consultar, y devolver el de mercado seria peor que no
        // devolver nada, porque sobrescribiria una decisión de la persona con un dato ajeno.
        if (instrument.ValuationMode == ValuationMode.Manual)
        {
            return null;
        }

        // `Market` y `Nav` los sirve la misma fuente: la diferencia entre una acción y un fondo no está
        // en dónde se pregunta, sino en qué se lee de la respuesta, y de eso se encarga el adaptador
        // mirando la frecuencia de cotización.
        return await _mercado.GetQuoteAsync(instrument, cancellationToken);
    }
}
