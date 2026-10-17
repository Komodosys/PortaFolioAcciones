using PortaFolioAcciones.Application.MarketData;
using PortaFolioAcciones.Domain.Entities;

namespace PortaFolioAcciones.Application.Abstractions;

/// <summary>
/// Consigue el precio actual de un instrumento.
///
/// Es un <b>puerto</b>: la aplicación pide «¿cuánto vale esto?» y no sabe de dónde sale la respuesta, ni
/// si hubo que llamar a un servicio, leer una caché o devolver el último precio conocido.
///
/// Vive en la capa de aplicación y no en el dominio <b>a propósito</b>: el dominio consume el
/// <b>resultado</b> —un <see cref="Money"/> con su fecha—, no al proveedor. Quién trae el dato es un
/// detalle de infraestructura, y los detalles de infraestructura no entran en el dominio.
/// </summary>
public interface IPriceProvider
{
    /// <summary>
    /// El precio del instrumento, o <c>null</c> si no se puede conseguir.
    ///
    /// Devolver <c>null</c> es una respuesta válida, y es justo lo que distingue este puerto de uno que
    /// lanza: si la fuente no responde, el que llama decide si usa el último precio conocido, si marca
    /// la posición como obsoleta, o si avisa. Aquí no se inventa un precio.
    /// </summary>
    /// <param name="instrument">El instrumento. Su <c>ProviderKey</c> es el nombre que usa la fuente.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    public Task<MarketQuote?> GetQuoteAsync(
        Instrument instrument,
        CancellationToken cancellationToken = default);
}
