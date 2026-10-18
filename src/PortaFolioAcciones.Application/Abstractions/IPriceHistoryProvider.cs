using PortaFolioAcciones.Application.MarketData;
using PortaFolioAcciones.Domain.Entities;

namespace PortaFolioAcciones.Application.Abstractions;

/// <summary>
/// Pide a la fuente <b>la serie de cierres</b> de un instrumento, no el último precio.
///
/// Es un puerto aparte de <see cref="IPriceProvider"/> y no un método más, y la razón es que se usan de
/// forma distinta:
///
/// <list type="bullet">
///   <item><description>
///     <b>Cotizar</b> ocurre todo el tiempo, instrumento a instrumento, y por eso lleva caché, reintentos y
///     cadena de respaldo.
///   </description></item>
///   <item><description>
///     <b>Rellenar el histórico</b> ocurre <b>una vez</b>, y lo que devuelve son cientos de filas de una sola
///     petición. Meterlo en la cadena de cotizar habría obligado a que la caché, el respaldo y los
///     interruptores supieran de series, y ninguno de los tres tiene nada que hacer con ellas.
///   </description></item>
/// </list>
///
/// Sin relleno, el histórico de precios empieza el día que se instala la aplicación y los informes de
/// periodo no se pueden calcular hasta pasado un año.
/// </summary>
public interface IPriceHistoryProvider
{
    /// <summary>
    /// Los cierres publicados desde una fecha.
    /// </summary>
    /// <param name="instrument">El instrumento. Sin clave de fuente no se puede pedir nada.</param>
    /// <param name="desde">El día más antiguo que interesa.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    /// <returns>
    /// Los cierres en orden de fecha, sin huecos rellenados: <b>los días sin publicación no aparecen</b>. Un
    /// festivo o un fondo que publica cada dos días no tienen cierre, y inventarlo sería peor que faltar.
    /// </returns>
    public Task<IReadOnlyList<DailyClose>> GetHistoryAsync(
        Instrument instrument,
        DateOnly desde,
        CancellationToken cancellationToken = default);
}
