using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Application.MarketData;

/// <summary>
/// El cierre de un instrumento en un día concreto.
///
/// Es lo que devuelve una petición de histórico: <b>una serie, no un precio</b>. No lleva desdoblamientos ni
/// dividendos porque no hacen falta para rellenar: el precio que se guarda es el de cierre tal como lo
/// publicó la fuente, y los desdoblamientos se detectan con la lectura del día, no con el relleno.
/// </summary>
/// <param name="Date">El día al que corresponde el cierre, en fecha de la bolsa.</param>
/// <param name="Price">El cierre, en la moneda en la que cotiza.</param>
public sealed record DailyClose(DateOnly Date, Money Price);
