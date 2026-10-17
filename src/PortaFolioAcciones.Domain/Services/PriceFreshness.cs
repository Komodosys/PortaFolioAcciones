using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Domain.Services;

/// <summary>
/// Si un precio se puede seguir considerando de ahora.
///
/// Existe porque <b>un precio viejo presentado como actual es peor que un hueco</b>: el hueco se ve, y el
/// precio viejo se cree. La aplicacion entera esta construida sobre esa idea —el aviso de total parcial,
/// la fecha del precio, el origen— y esto es la ultima pieza.
/// </summary>
public static class PriceFreshness
{
    /// <summary>
    /// Cuantos dias de mas se le permiten a un precio antes de llamarlo viejo.
    ///
    /// <b>No todos los precios cambian al mismo ritmo</b>, y usar la misma tolerancia para todos daria
    /// avisos falsos todos los dias: un fondo publica su valor liquidativo con uno o dos dias de retraso
    /// —es como funciona, no un fallo— y una accion no.
    /// </summary>
    /// <param name="frequency">Cada cuanto cambia el precio de ese instrumento.</param>
    public static int ToleranceInDays(PricingFrequency frequency) => frequency switch
    {
        // Los fondos publican el valor liquidativo con retraso, y es normal. Avisar de un dia de retraso
        // seria avisar de que el mundo funciona.
        PricingFrequency.DailyNav => 3,

        // Todo lo demas deberia ser del dia de la ultima sesion. Un precio a mano tambien: que sea una
        // decision de quien lo puso no lo hace menos viejo, y saberlo es justo lo que hace falta para
        // revisarlo.
        _ => 0,
    };

    /// <summary>
    /// Si el precio de un instrumento esta viejo.
    /// </summary>
    /// <param name="instrument">El instrumento.</param>
    /// <param name="priceDate">De que dia es el precio. Nulo si no se sabe.</param>
    /// <param name="now">El instante actual.</param>
    /// <returns>
    /// <c>false</c> cuando no hay datos para juzgar: un precio sin fecha, o una bolsa que no se conoce.
    /// <b>Ante la duda no se avisa</b>, porque un aviso que sale cuando no toca enseña a ignorarlos.
    /// </returns>
    public static bool IsStale(Instrument instrument, DateOnly? priceDate, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(instrument);

        if (priceDate is not { } fecha)
        {
            return false;
        }

        if (MarketCalendar.LastCloseDate(instrument.Exchange, now) is not { } ultimaSesion)
        {
            return false;
        }

        return fecha.AddDays(ToleranceInDays(instrument.PricingFrequency)) < ultimaSesion;
    }
}
