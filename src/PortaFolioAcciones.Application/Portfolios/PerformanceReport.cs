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
/// <param name="CostBasis">Lo que costaron esas mismas posiciones.</param>
/// <param name="UnrealizedGain"><paramref name="MarketValue"/> menos <paramref name="CostBasis"/>.</param>
/// <param name="Contributed">El dinero que ha entrado desde fuera.</param>
/// <param name="Withdrawn">El dinero que ha salido hacia fuera.</param>
/// <param name="RealizedGain">
/// Lo ganado en lo ya vendido. <b>Nulo</b> si alguna venta no tiene el coste guardado —las anteriores a que
/// ese dato existiera—: devolver cero diría que no se ganó nada, y no es lo mismo que no saberlo.
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
    decimal CostBasis,
    decimal UnrealizedGain,
    decimal Contributed,
    decimal Withdrawn,
    decimal? RealizedGain,
    DateOnly? SnapshotDate)
{
    /// <summary>Lo que se puso y no se ha retirado.</summary>
    public decimal NetContributed => Contributed - Withdrawn;
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
