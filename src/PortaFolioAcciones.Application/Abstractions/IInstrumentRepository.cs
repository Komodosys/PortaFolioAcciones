using PortaFolioAcciones.Application.Instruments;

namespace PortaFolioAcciones.Application.Abstractions;

/// <summary>
/// Da acceso al catálogo de instrumentos.
///
/// El catálogo es <b>compartido</b>: un instrumento no es de nadie, lo usan las posiciones de
/// cualquiera. Por eso aquí <b>no</b> hay filtro por persona; lo que sí hay es una regla distinta:
/// <b>cualquiera puede añadir</b> —hace falta para dar de alta la posición que acaba de comprar— pero
/// <b>solo el administrador puede modificar</b> lo que ya está, porque cambiarlo afecta a las carteras
/// de todos.
/// </summary>
public interface IInstrumentRepository
{
    /// <summary>Busca en el catálogo, por símbolo o por nombre.</summary>
    /// <param name="query">Texto a buscar. Vacío devuelve los primeros.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    public Task<IReadOnlyList<InstrumentSummary>> SearchAsync(
        string? query,
        CancellationToken cancellationToken = default);

    /// <summary>Un instrumento concreto, o nulo.</summary>
    /// <param name="id">Identificador.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    public Task<InstrumentSummary?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Añade un instrumento al catálogo, o devuelve el que ya existía con esa identidad.
    ///
    /// No falla si ya existe: dar de alta dos veces el mismo instrumento es lo que pasa cuando dos
    /// personas de la casa compran el mismo valor, y lo correcto es que las dos posiciones apunten al
    /// mismo sitio y no a dos copias con precios distintos.
    /// </summary>
    /// <param name="command">Los datos.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    public Task<InstrumentResult> AddAsync(
        CreateInstrumentCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>Modifica un instrumento. Solo el administrador.</summary>
    /// <param name="id">Identificador.</param>
    /// <param name="command">Los datos nuevos.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    public Task<InstrumentResult> UpdateAsync(
        Guid id,
        CreateInstrumentCommand command,
        CancellationToken cancellationToken = default);
}
