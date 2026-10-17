using PortaFolioAcciones.Application.Positions;

namespace PortaFolioAcciones.Application.Abstractions;

/// <summary>
/// Da acceso a las posiciones y a las operaciones que se hacen sobre ellas.
///
/// Una posición <b>no tiene dueño propio</b>: pertenece a una cartera, y la cartera sí es de alguien. Por
/// eso aquí no se pide el usuario en ningún método: quien llama no puede elegir en nombre de quién
/// opera, y la comprobación se hace subiendo hasta la cartera.
/// </summary>
public interface IPositionRepository
{
    /// <summary>Las posiciones de una cartera.</summary>
    /// <param name="portfolioId">La cartera.</param>
    /// <param name="includeClosed">Si se quieren también las cerradas.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    public Task<IReadOnlyList<PositionDetail>> ListAsync(
        Guid portfolioId,
        bool includeClosed = false,
        CancellationToken cancellationToken = default);

    /// <summary>Una posición concreta, o nulo si no existe o no se puede ver.</summary>
    /// <param name="id">Identificador.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    public Task<PositionDetail?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// La cuenta a la que pertenece una posición, o nulo si no existe o no se puede ver.
    ///
    /// Sirve para que la pantalla pueda decir <b>en qué cuenta y de quién</b> se está trabajando, que es un
    /// requisito de [ADR-014]: el administrador puede modificar datos ajenos y tiene que saberlo donde los
    /// modifica.
    /// </summary>
    /// <param name="id">Identificador de la posición.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    public Task<PositionAccount?> AccountOfAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Da de alta una posición en una cartera.</summary>
    /// <param name="portfolioId">La cartera.</param>
    /// <param name="command">Los datos.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    public Task<PositionResult> AddAsync(
        Guid portfolioId,
        AddPositionCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>Añade una compra a una posición que ya existe.</summary>
    /// <param name="id">La posición.</param>
    /// <param name="lot">La compra.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    public Task<PositionResult> AddLotAsync(
        Guid id,
        LotInput lot,
        CancellationToken cancellationToken = default);

    /// <summary>Vende una cantidad.</summary>
    /// <param name="id">La posición.</param>
    /// <param name="command">Cuánto se vende.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    public Task<PositionResult> SellAsync(
        Guid id,
        SellCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>Pone un precio a mano, con su fecha y su moneda.</summary>
    /// <param name="id">La posición.</param>
    /// <param name="command">El precio.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    public Task<PositionResult> SetManualPriceAsync(
        Guid id,
        ManualPriceCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>Aplica un desdoblamiento.</summary>
    /// <param name="id">La posición.</param>
    /// <param name="command">La proporción.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    public Task<PositionResult> ApplySplitAsync(
        Guid id,
        SplitCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>Cierra la posición, conservando lo que costó.</summary>
    /// <param name="id">La posición.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    public Task<PositionResult> CloseAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Vuelve a abrir una posición cerrada.</summary>
    /// <param name="id">La posición.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    public Task<PositionResult> ReopenAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Cambia las notas.</summary>
    /// <param name="id">La posición.</param>
    /// <param name="notes">Las notas, o nulo para quitarlas.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    public Task<PositionResult> SetNotesAsync(
        Guid id,
        string? notes,
        CancellationToken cancellationToken = default);
}
