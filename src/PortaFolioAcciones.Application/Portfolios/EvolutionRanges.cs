namespace PortaFolioAcciones.Application.Portfolios;

/// <summary>
/// Los periodos que se pueden pedir en el gráfico de evolución.
/// </summary>
/// <remarks>
/// <b>Los traduce el servidor y no la pantalla</b>, y no es por gusto: «lo que va de año» y «desde el principio»
/// son reglas de calendario, no un detalle de presentación. En el cliente acabarían repartidas entre la pantalla
/// y su prueba, cada una calculando el 1 de enero a su manera, y un 31 de diciembre se vería distinto de un 1 de
/// enero sin que nadie lo hubiera decidido.
/// </remarks>
public enum EvolutionRange
{
    /// <summary>Los últimos cinco días.</summary>
    FiveDays = 0,

    /// <summary>Los últimos quince días.</summary>
    FifteenDays = 1,

    /// <summary>El último mes.</summary>
    OneMonth = 2,

    /// <summary>El último año.</summary>
    OneYear = 3,

    /// <summary>Lo que va de año: desde el 1 de enero.</summary>
    YearToDate = 4,

    /// <summary>Todo lo que hay.</summary>
    All = 5,
}

/// <summary>
/// Convierte un periodo en una fecha de comienzo y un número de puntos razonable.
/// </summary>
public static class EvolutionRanges
{
    /// <summary>El nombre con el que viaja cada periodo en la dirección.</summary>
    public static readonly IReadOnlyDictionary<string, EvolutionRange> Nombres =
        new Dictionary<string, EvolutionRange>(StringComparer.OrdinalIgnoreCase)
        {
            ["5d"] = EvolutionRange.FiveDays,
            ["15d"] = EvolutionRange.FifteenDays,
            ["1m"] = EvolutionRange.OneMonth,
            ["1a"] = EvolutionRange.OneYear,
            ["ytd"] = EvolutionRange.YearToDate,
            ["todo"] = EvolutionRange.All,
        };

    /// <summary>Traduce el nombre que llega en la petición. Nulo o desconocido: todo.</summary>
    public static EvolutionRange Parse(string? nombre) =>
        nombre is not null && Nombres.TryGetValue(nombre, out var rango) ? rango : EvolutionRange.All;

    /// <summary>
    /// Desde cuándo se dibuja. <b>Nulo significa «desde donde haya datos»</b>, que no es lo mismo que una fecha
    /// muy antigua: pedir 2019 cuando los precios empiezan en 2021 daría un tramo vacío al principio.
    /// </summary>
    public static DateOnly? Desde(EvolutionRange rango, DateOnly hoy) => rango switch
    {
        EvolutionRange.FiveDays => hoy.AddDays(-5),
        EvolutionRange.FifteenDays => hoy.AddDays(-15),
        EvolutionRange.OneMonth => hoy.AddMonths(-1),
        EvolutionRange.OneYear => hoy.AddYears(-1),
        EvolutionRange.YearToDate => new DateOnly(hoy.Year, 1, 1),
        _ => null,
    };

    /// <summary>
    /// Cuántos puntos se piden para cada periodo.
    /// </summary>
    /// <remarks>
    /// <b>No se piden siempre los mismos.</b> Sesenta puntos sobre cinco días son más puntos que días —el mismo
    /// día repetido— y sesenta sobre un año se deja días sin mirar. La cuenta va a lo que se quiere ver: en los
    /// periodos cortos, día a día; en los largos, con semanas de por medio.
    /// </remarks>
    public static int Puntos(EvolutionRange rango) => rango switch
    {
        EvolutionRange.FiveDays => 6,
        EvolutionRange.FifteenDays => 16,
        EvolutionRange.OneMonth => 31,
        EvolutionRange.OneYear => 53,
        EvolutionRange.YearToDate => 60,
        _ => 60,
    };
}
