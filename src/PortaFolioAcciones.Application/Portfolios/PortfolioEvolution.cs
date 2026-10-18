namespace PortaFolioAcciones.Application.Portfolios;

/// <summary>
/// De dónde sale la serie de evolución de una cartera.
/// </summary>
/// <remarks>
/// <b>La diferencia no es un detalle técnico: es lo que separa un dato de una suposición.</b> Una cartera que se
/// dio de alta como una foto de lo que había no tiene historia de movimientos que reconstruir; dibujarle una
/// línea hacia atrás sería inventarse el pasado. La pantalla tiene que poder decir cuál de las dos está mirando.
/// </remarks>
public enum EvolutionSource
{
    /// <summary>De las fotos diarias guardadas. Es lo que de verdad se registró, día a día.</summary>
    Snapshots = 0,

    /// <summary>
    /// Reconstruida desde los movimientos, los cierres y los tipos de cambio. Es exacta en cantidad y precio,
    /// pero <b>es una reconstrucción</b>: no estaba registrada día a día.
    /// </summary>
    Reconstructed = 1,
}

/// <summary>
/// Un punto de la evolución de una cartera, en su moneda base.
/// </summary>
/// <param name="Date">El día del punto.</param>
/// <param name="MarketValue">Lo que valían las posiciones ese día.</param>
/// <param name="CashValue">El efectivo que había ese día.</param>
/// <param name="Contributed">
/// Lo que se había puesto hasta ese día y no se había sacado, en la moneda base. <b>No es una aportación
/// acumulada sin más</b>: incluye los traspasos y descuenta los retiros, porque la línea contra la que se
/// compara el valor tiene que ser todo el dinero que entró sin ser ganancia. <b>Nulo</b> cuando la serie sale de
/// las fotografías: lo que se puso antes de que hubiera registro no se sabe, y un cero diría que no se puso nada.
/// </param>
public sealed record EvolutionPoint(
    DateOnly Date,
    decimal MarketValue,
    decimal CashValue,
    decimal? Contributed)
{
    /// <summary>Lo que valía la cartera entera: las posiciones más el efectivo.</summary>
    public decimal TotalValue => MarketValue + CashValue;
}

/// <summary>
/// La evolución de una cartera en el tiempo.
/// </summary>
/// <param name="PortfolioId">La cartera.</param>
/// <param name="Currency">La moneda de todas las cifras.</param>
/// <param name="Source">
/// De dónde sale la serie, por su nombre: <c>Snapshots</c> o <c>Reconstructed</c>. Se manda como texto y no como
/// número, igual que el tipo de registro en el informe: un 1 en el JSON no dice nada a quien lo lee, y la pantalla
/// tiene que poder decir cuál de las dos está mirando.
/// </param>
/// <param name="Points">Los puntos, del más antiguo al más reciente.</param>
/// <param name="IsComplete">
/// Falso si algún punto se calculó sin el precio de algo que se tenía ese día, o sin su tipo de cambio. Un
/// gráfico con un tramo a la baja por un precio que falta es una cifra falsa con forma de dato.
/// </param>
/// <param name="MissingPrices">
/// Qué instrumentos se quedaron sin precio, por nombre. Sin esto, «parcial» no dice qué mirar.
/// </param>
public sealed record PortfolioEvolution(
    Guid PortfolioId,
    string Currency,
    string Source,
    IReadOnlyList<EvolutionPoint> Points,
    bool IsComplete,
    IReadOnlyList<string> MissingPrices);
