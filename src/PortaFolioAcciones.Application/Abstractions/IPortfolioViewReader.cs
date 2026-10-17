namespace PortaFolioAcciones.Application.Abstractions;

using PortaFolioAcciones.Application.Portfolios;

/// <summary>
/// Lee la vista de una cartera ya calculada.
///
/// Es un puerto aparte del de <see cref="IPortfolioRepository"/> porque es otra cosa: aquel trabaja con
/// la cartera como entidad —crearla, renombrarla—, y este solo <b>lee y calcula</b>. Separarlos deja que
/// cada uno cambie por su motivo.
/// </summary>
public interface IPortfolioViewReader
{
    /// <summary>
    /// La vista de una cartera, o <c>null</c> si no existe o no se puede ver.
    /// </summary>
    /// <param name="portfolioId">La cartera.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    public Task<PortfolioView?> GetAsync(
        Guid portfolioId,
        CancellationToken cancellationToken = default);
}
