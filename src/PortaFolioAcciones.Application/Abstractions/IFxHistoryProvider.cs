using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Application.Abstractions;

/// <summary>
/// Pide a la fuente <b>la serie histórica</b> de un par de monedas, no el tipo de hoy.
///
/// Es un puerto aparte de <see cref="IFxRateProvider"/> por el mismo motivo que el de precios: cotizar ocurre
/// todo el tiempo y lleva caché; rellenar el histórico ocurre **una vez** y devuelve años de filas de una sola
/// petición.
///
/// <b>Hace falta para el efectivo.</b> Una compra de enero se pagó al tipo de enero; convertirla con el de hoy
/// daría un saldo que sube y baja solo cuando se mueve el dólar, y el saldo de una cuenta no hace eso.
/// </summary>
public interface IFxHistoryProvider
{
    /// <summary>
    /// Los tipos publicados para un par desde una fecha.
    /// </summary>
    /// <param name="from">La moneda que se convierte.</param>
    /// <param name="to">La moneda a la que se convierte.</param>
    /// <param name="desde">El día más antiguo que interesa.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    /// <returns>
    /// Un tipo por día publicado, en orden de fecha. <b>Los días sin publicación no aparecen</b>: los tipos no
    /// se publican los fines de semana ni los festivos, y rellenar esos huecos con el del día anterior sería
    /// inventar una fecha.
    /// </returns>
    public Task<IReadOnlyList<FxRate>> GetHistoryAsync(
        Currency from,
        Currency to,
        DateOnly desde,
        CancellationToken cancellationToken = default);
}
