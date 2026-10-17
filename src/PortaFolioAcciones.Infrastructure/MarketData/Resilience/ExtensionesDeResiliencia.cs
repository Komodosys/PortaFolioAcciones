using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PortaFolioAcciones.Infrastructure.MarketData.Resilience;

/// <summary>
/// Poner la resiliencia en un cliente HTTP, en una linea.
///
/// Existe para que los tres clientes que salen a internet la lleven <b>igual</b>. Escrito a mano en cada
/// uno, el dia que se añada un cuarto se olvidaria, y ese olvido no se nota: el cliente funcionaria, solo
/// que sin reintentos y sin cuota, y nadie lo sabria hasta que la fuente empezara a rechazar.
/// </summary>
internal static class ExtensionesDeResiliencia
{
    /// <summary>
    /// Añade reintentos, interruptor de circuito y cuota a un cliente HTTP.
    /// </summary>
    /// <param name="constructor">El cliente.</param>
    /// <param name="fuente">
    /// Como se llama la fuente. <b>Lo que se le pase es lo que separa una guarda de otra</b>: dos clientes
    /// con el mismo nombre comparten interruptor y cuota, y con nombres distintos no.
    /// </param>
    public static IHttpClientBuilder ConResiliencia(this IHttpClientBuilder constructor, string fuente) =>
        constructor.AddHttpMessageHandler(proveedor => new ManejadorDeResiliencia(
            proveedor.GetRequiredService<GuardasDeFuentes>().Para(fuente),
            proveedor.GetRequiredService<OpcionesDeResiliencia>(),
            proveedor.GetRequiredService<TimeProvider>(),
            proveedor.GetRequiredService<ILogger<ManejadorDeResiliencia>>()));
}
