using PortaFolioAcciones.Application.MarketData;
using PortaFolioAcciones.Domain.Entities;

namespace PortaFolioAcciones.Application.Abstractions;

/// <summary>
/// El último precio que se guardó de un instrumento.
///
/// Es el <b>último escalón de la cadena de respaldo</b>, y existe porque las alternativas son peores:
/// dejar la posición sin valorar rompe el total, y poner un cero es mentir. Un precio viejo, <b>dicho
/// como viejo</b>, al menos permite seguir leyendo la cartera mientras la fuente no responde.
///
/// Lo que devuelve va marcado con <see cref="MarketQuote.IsFallback"/>, para que la pantalla pueda
/// avisar. Un precio guardado presentado como recién pedido sería peor que no mostrarlo.
/// </summary>
public interface ILastKnownPrice
{
    /// <summary>
    /// El último precio guardado del instrumento, o <c>null</c> si nunca se guardó ninguno.
    /// </summary>
    /// <param name="instrument">El instrumento.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    public Task<MarketQuote?> GetAsync(
        Instrument instrument,
        CancellationToken cancellationToken = default);
}
