namespace PortaFolioAcciones.Application.Abstractions;

using PortaFolioAcciones.Application.Portfolios;

/// <summary>
/// La evolución de una cartera en el tiempo: lo que valía y lo que se había puesto.
/// </summary>
public interface IPortfolioEvolutionReader
{
    /// <summary>
    /// Devuelve la serie de una cartera, o nulo si no existe o no es de quien pregunta.
    /// </summary>
    /// <param name="portfolioId">La cartera.</param>
    /// <param name="desde">Primer día. Nulo: desde el primer movimiento o la primera foto.</param>
    /// <param name="hasta">Último día. Nulo: hoy, o el último día con algo.</param>
    /// <param name="puntos">
    /// Cuántos puntos como máximo. Se reparten a intervalos regulares, y **siempre se incluye el último día**:
    /// cortar el gráfico en el penúltimo punto dejaría fuera justo la cifra que se quiere ver.
    /// </param>
    /// <param name="cancellationToken">Cancelación.</param>
    public Task<PortfolioEvolution?> GetAsync(
        Guid portfolioId,
        DateOnly? desde,
        DateOnly? hasta,
        int puntos,
        CancellationToken cancellationToken = default);
}
