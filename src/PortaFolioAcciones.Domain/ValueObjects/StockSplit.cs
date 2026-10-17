namespace PortaFolioAcciones.Domain.ValueObjects;

/// <summary>
/// Un desdoblamiento: tal dia, cada titulo se convirtio en <paramref name="Ratio"/>.
///
/// Un 10 significa un 10:1 —como el de NFLX—: quien tenia una accion paso a tener diez, y cada una valia
/// la decima parte. <b>El coste total no cambia</b>: un desdoblamiento reparte lo mismo entre mas titulos.
/// Lo que cambia es la cantidad y el precio por unidad, y por eso hay que aplicarlo: sin aplicarlo, la
/// posicion se lee como si el precio se hubiera hundido.
/// </summary>
/// <param name="Date">El dia en que la fuente dice que ocurrio.</param>
/// <param name="Ratio">Por cuanto se multiplica la cantidad.</param>
public sealed record StockSplit(DateOnly Date, decimal Ratio)
{
    /// <summary>Crea un desdoblamiento.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Si la proporcion no es mayor que uno. Un 1:1 no es nada, y una proporcion menor que uno seria una
    /// agrupacion —lo contrario— y aqui no se ha dado el caso: aceptarla en silencio aplicaria un cambio
    /// distinto del que dice la fuente.
    /// </exception>
    public static StockSplit Of(DateOnly date, decimal ratio)
    {
        if (ratio <= 1m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ratio),
                ratio,
                "Un desdoblamiento tiene que repartir en MAS titulos. Una proporcion de uno o menos seria " +
                "una agrupacion, que es lo contrario y no se ha dado.");
        }

        return new StockSplit(date, ratio);
    }

    public override string ToString() =>
        string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{Date:yyyy-MM-dd} {Ratio}:1");
}
