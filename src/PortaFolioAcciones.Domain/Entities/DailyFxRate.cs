using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Domain.Entities;

/// <summary>
/// El tipo de cambio de un par de monedas en una fecha concreta, guardado.
///
/// Existe porque <see cref="FxRate"/> —el valor— <b>se usaba y se tiraba</b>: se pedía para convertir los
/// totales y se quedaba en memoria. Con eso se puede decir «la cartera subió un 3 %», pero **no se puede
/// descomponer**: si subió porque subieron las acciones o porque el dólar se movió. Y esa pregunta no se
/// puede responder después: el dato que no se guarda hoy no existe mañana.
///
/// La clave es <b>el par y la fecha</b>, y no un identificador: un tipo de cambio no es algo que uno cree,
/// es lo que publicó la fuente ese día. Pedirlo dos veces el mismo día <b>actualiza</b> en vez de duplicar.
/// </summary>
public sealed class DailyFxRate
{
    private DailyFxRate(Guid id, Currency from, Currency to, decimal rate, DateOnly rateDate, DateTimeOffset now)
    {
        Id = id;
        From = from;
        To = to;
        Rate = rate;
        RateDate = rateDate;
        CreatedAtUtc = now;
    }

    /// <summary>Constructor sin parametros para el mapeo de la base de datos.</summary>
    private DailyFxRate()
    {
        From = Currency.Cad;
        To = Currency.Cad;
    }

    public Guid Id { get; private set; }

    /// <summary>Moneda de origen.</summary>
    public Currency From { get; private set; }

    /// <summary>Moneda de destino.</summary>
    public Currency To { get; private set; }

    /// <summary>Cuanto vale una unidad de <see cref="From"/> en <see cref="To"/>.</summary>
    public decimal Rate { get; private set; }

    /// <summary>El dia al que corresponde el tipo, en fecha de la fuente.</summary>
    public DateOnly RateDate { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>Guarda un tipo de cambio que ya se ha usado para convertir.</summary>
    /// <param name="tipo">El valor que devolvio la fuente.</param>
    /// <param name="now">El instante en que se guarda.</param>
    public static DailyFxRate FromValue(FxRate tipo, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(tipo);

        return new DailyFxRate(Guid.NewGuid(), tipo.From, tipo.To, tipo.Rate, tipo.RateDate, now);
    }

    /// <summary>Actualiza el valor de uno que ya estaba guardado.</summary>
    public void Update(decimal rate, DateTimeOffset now)
    {
        Rate = rate;
        CreatedAtUtc = now;
    }

    /// <summary>La clave natural: el par y el dia.</summary>
    public (string From, string To, DateOnly Date) Key => (From.Code, To.Code, RateDate);
}
