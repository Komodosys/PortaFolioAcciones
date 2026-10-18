namespace PortaFolioAcciones.Application.Portfolios;

/// <summary>
/// El rendimiento de una cartera: lo que vale, lo que costó, lo que se puso y lo que ya se vendió.
/// </summary>
/// <param name="PortfolioId">La cartera.</param>
/// <param name="Name">Su nombre.</param>
/// <param name="OwnerDisplayName">De quién es, para poder agrupar.</param>
/// <param name="RegistrationType">REER, CELI o no registrada.</param>
/// <param name="Currency">La moneda en la que están todas las cifras de esta fila.</param>
/// <param name="MarketValue">Lo que valen hoy las posiciones abiertas.</param>
/// <param name="CashValue">El dinero que hay en la cuenta.</param>
/// <param name="CostBasis">Lo que costaron esas mismas posiciones.</param>
/// <param name="UnrealizedGain"><paramref name="MarketValue"/> menos <paramref name="CostBasis"/>.</param>
/// <param name="Contributed">
/// El dinero que ha entrado desde fuera, <b>en la moneda de la fila</b>. Nulo si algún movimiento está en otra
/// moneda y no se guardó su tipo: una suma a la que le falta una parte se leería como el total.
/// </param>
/// <param name="Withdrawn">El dinero que ha salido hacia fuera, en la moneda de la fila. Nulo, igual que
/// <paramref name="Contributed"/>.</param>
/// <param name="RealizedGain">
/// Lo ganado en lo ya vendido, en la moneda de la fila. <b>Nulo</b> si alguna venta no tiene el coste guardado
/// —las anteriores a que ese dato existiera—: devolver cero diría que no se ganó nada, y no es lo mismo que no
/// saberlo.
/// </param>
/// <param name="SnapshotDate">
/// El día al que corresponden <paramref name="MarketValue"/> y <paramref name="CostBasis"/>. Se dice porque
/// un valor de hace tres días presentado como de hoy es una cifra que engaña.
/// </param>
/// <remarks>
/// <b>Lo que este informe NO incluye es el efectivo.</b> La aplicación no tiene cuenta de efectivo, así que
/// «lo que vale» son las posiciones y nada más. Si algún día hay dinero sin invertir en una cuenta, esta fila
/// diría que se perdió, y por eso el informe se llama «coste contra valor» y no «aportado contra ganado».
/// </remarks>
public sealed record PerformanceRow(
    Guid PortfolioId,
    string Name,
    string OwnerDisplayName,
    string RegistrationType,
    string Currency,
    decimal MarketValue,
    decimal CashValue,
    decimal CostBasis,
    decimal UnrealizedGain,
    decimal? Contributed,
    decimal? Withdrawn,
    decimal? RealizedGain,
    DateOnly? SnapshotDate,
    bool HasOpening,
    decimal? TransfersIn = null,
    decimal? TransfersOut = null)
{
    /// <summary>
    /// Lo ganado de verdad: el total menos el dinero que entró <b>sin ser ganancia</b>. **Nulo si no hay
    /// apertura declarada**, si falta la foto, o si alguna de las partes no se pudo sumar entera.
    ///
    /// Sin apertura, «lo aportado» son solo las aportaciones que se hayan anotado, y las posiciones que ya
    /// estaban no aparecerian como puestas: la ganancia saldria enorme y falsa. Nulo dice «no se sabe», que es
    /// la verdad; un numero diria que se gano eso.
    ///
    /// **Y sin foto tampoco**, por el mismo motivo: los importes de la fila salen de la foto del dia, y una
    /// cartera creada despues de la ultima recogida los tiene a cero. Restarle lo aportado a cero no es una
    /// perdida: es que todavia no se sabe.
    ///
    /// **Y los traspasos se restan.** Un traspaso desde otra cuenta del titular no es ganancia, igual que no es
    /// una aportacion: sin restarlo, esta cifra diria que se gano todo lo que se movio de sitio. Se vio con la
    /// cuenta de siete años, donde los traspasos son 13.205,66 CAD de los 55.354,30 que salian como ganados.
    /// </summary>
    public decimal? Gain => HasOpening && SnapshotDate is not null && NetContributed is { } neto && NetTransfers is { } traspasos
        ? TotalValue - neto - traspasos
        : null;

    /// <summary>Lo que se puso y no se ha retirado. Nulo si falta una de las dos partes.</summary>
    public decimal? NetContributed => Contributed is { } puesto && Withdrawn is { } sacado
        ? puesto - sacado
        : null;

    /// <summary>Lo que entró y salió por traspaso, neto. Nulo si falta una de las dos partes.</summary>
    public decimal? NetTransfers => TransfersIn is { } entrado && TransfersOut is { } salido
        ? entrado - salido
        : null;

    /// <summary>
    /// Lo que vale la cartera entera: las posiciones **mas el efectivo**.
    ///
    /// Es la cifra que responde «cuanto tengo», y es la que hay que comparar con lo aportado. Comparar solo el
    /// valor de las posiciones haria que el dinero sin invertir pareciera perdido.
    /// </summary>
    public decimal TotalValue => MarketValue + CashValue;
}

/// <summary>
/// El informe completo, con los totales ya agrupados.
/// </summary>
/// <param name="Rows">Una fila por cartera.</param>
/// <param name="Totals">Los totales, solo si todas las filas comparten moneda.</param>
/// <param name="Currencies">
/// Las monedas que aparecen. Si hay más de una, <paramref name="Totals"/> es nulo y la pantalla tiene que
/// decir que no se pueden sumar: sumar dólares y euros en una sola cifra daría un número que no significa
/// nada.
/// </param>
public sealed record PerformanceReport(
    IReadOnlyList<PerformanceRow> Rows,
    PerformanceRow? Totals,
    IReadOnlyList<string> Currencies);
