using PortaFolioAcciones.Application.Portfolios;
using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Application.Abstractions;

/// <summary>
/// Da acceso a las carteras.
///
/// Es un <b>puerto</b>: la aplicacion dice que necesita, y la infraestructura decide como lo consigue.
///
/// <b>Ningun metodo recibe el usuario como parametro.</b> Quien llama no puede elegir en nombre de quien
/// consulta: el usuario sale del contexto autenticado. Si se pudiera pasar, bastaria con cambiar un
/// numero para leer los datos de otra persona, y el aislamiento dependeria de que nadie se equivoque.
/// </summary>
public interface IPortfolioRepository
{
    /// <summary>
    /// Carteras que puede ver quien llama: las suyas y, si es administrador, todas.
    /// </summary>
    /// <param name="includeArchived">Si se quieren también las archivadas.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    public Task<IReadOnlyList<PortfolioSummary>> ListAsync(
        bool includeArchived = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Una cartera concreta, o <c>null</c> si no existe <b>o si no se puede ver</b>.
    ///
    /// Los dos casos devuelven lo mismo a proposito: quien llama no tiene forma de distinguir «no
    /// existe» de «no es tuya», y por tanto no puede deducir que la cartera de otra persona existe.
    /// </summary>
    public Task<PortfolioSummary?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea una cartera. El dueno es <b>siempre quien llama</b>: no se puede crear una cartera a nombre
    /// de otra persona ni pasando un identificador.
    /// </summary>
    /// <param name="name">Nombre de la cartera.</param>
    /// <param name="registrationType">
    /// Cómo está registrada a efectos fiscales. <b>Es obligatorio</b> y no tiene valor por defecto: un valor
    /// por defecto cómodo etiquetaría en silencio una cuenta de jubilación como no registrada.
    /// </param>
    /// <param name="baseCurrency">Moneda propia, o nulo para heredar la de la persona.</param>
    /// <param name="cancellationToken">Cancelacion.</param>
    /// <exception cref="InvalidOperationException">Si no hay sesion.</exception>
    public Task<PortfolioSummary> CreateAsync(
        string name,
        RegistrationType registrationType,
        string? baseCurrency,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cambia el nombre de una cartera.
    /// </summary>
    /// <returns>
    /// La cartera, o <c>null</c> si no existe o no se puede tocar. Igual que al leer, los dos casos
    /// devuelven lo mismo.
    /// </returns>
    public Task<PortfolioSummary?> RenameAsync(
        Guid id,
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Archiva una cartera.
    ///
    /// Se archiva y no se borra: una cartera tiene posiciones e histórico, y borrarla se llevaría por
    /// delante justo lo que hace falta para entender qué pasó.
    /// </summary>
    /// <param name="id">Identificador.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    public Task<PortfolioSummary?> ArchiveAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Vuelve a activar una cartera archivada.</summary>
    /// <param name="id">Identificador.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    public Task<PortfolioSummary?> UnarchiveAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fija la moneda base de la cartera, o la hace <b>heredar</b> la de la persona.
    /// </summary>
    /// <param name="id">Identificador.</param>
    /// <param name="baseCurrency">
    /// El código de la moneda, o <c>null</c> para volver a heredar la del perfil. Poder volver a heredar
    /// importa: si no, una elección de una vez se quedaría fija para siempre y cambiar la moneda del
    /// perfil dejaría de afectarle.
    /// </param>
    /// <param name="cancellationToken">Cancelación.</param>
    public Task<PortfolioSummary?> SetBaseCurrencyAsync(
        Guid id,
        string? baseCurrency,
        CancellationToken cancellationToken = default);

    /// <summary>Cambia el orden en el que se quiere ver la cartera.</summary>
    /// <param name="id">Identificador.</param>
    /// <param name="sortOrder">Posición en la lista.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    public Task<PortfolioSummary?> ReorderAsync(
        Guid id,
        int sortOrder,
        CancellationToken cancellationToken = default);
}
