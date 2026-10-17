using PortaFolioAcciones.Application.Audit;

namespace PortaFolioAcciones.Application.Abstractions;

/// <summary>
/// Lee el libro de auditoría.
///
/// Es un puerto <b>de solo lectura</b>, y no por casualidad: el libro es de solo añadir. No existe —ni
/// debe existir— un método para editarlo o borrarlo desde la aplicación. Un registro que se puede
/// modificar no es un registro.
/// </summary>
public interface IAuditLogReader
{
    /// <summary>
    /// Las anotaciones que cumplen el filtro, <b>de la más reciente a la más antigua</b>.
    ///
    /// El orden no es un detalle de presentación: lo que se busca en un libro de auditoría es lo último
    /// que ha pasado.
    /// </summary>
    /// <param name="filter">Qué se quiere ver.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    public Task<IReadOnlyList<AuditEntry>> ReadAsync(
        AuditFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Las personas <b>cuyos datos</b> aparecen en el libro, para poder ofrecer la lista de filtros.
    /// </summary>
    /// <remarks>
    /// Son los <b>dueños</b>, no los autores. Y no es un detalle: el filtro de la pantalla pregunta «¿qué
    /// le han tocado a esta persona?», así que ofrecer a alguien que solo aparece como autor daría una
    /// lista que no devuelve nada al elegirla — un filtro que parece roto.
    ///
    /// Se saca del propio libro y no de la lista de cuentas: así solo salen personas que <b>de verdad</b>
    /// tienen algo anotado.
    /// </remarks>
    /// <param name="cancellationToken">Cancelación.</param>
    public Task<IReadOnlyList<PersonaDelLibro>> PeopleAsync(CancellationToken cancellationToken = default);
}

/// <summary>Una persona que aparece en el libro de auditoría.</summary>
/// <param name="Id">Identificador.</param>
/// <param name="DisplayName">Cómo se llama.</param>
public sealed record PersonaDelLibro(Guid Id, string DisplayName);
