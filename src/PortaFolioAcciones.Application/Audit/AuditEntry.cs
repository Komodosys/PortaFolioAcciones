using PortaFolioAcciones.Domain.Entities;

namespace PortaFolioAcciones.Application.Audit;

/// <summary>
/// Una anotación del libro de auditoría, tal como se enseña.
/// </summary>
/// <param name="Id">Identificador.</param>
/// <param name="OccurredAtUtc">Cuándo ocurrió, en UTC.</param>
/// <param name="ActorName">Cómo se llama quien lo hizo.</param>
/// <param name="OwnerName">Cómo se llama la persona a la que le tocaron los datos.</param>
/// <param name="Action">Qué hizo: <c>Created</c>, <c>Updated</c> o <c>Deleted</c>.</param>
/// <param name="EntityType">Sobre qué entidad.</param>
/// <param name="EntityId">Identificador de esa entidad.</param>
/// <param name="Changes">Qué campos cambiaron, <b>con el valor anterior y el nuevo</b>.</param>
/// <param name="Reason">El motivo, si quien lo hizo lo escribió.</param>
/// <param name="IsSomeoneElseData">
/// Si el cambio fue sobre datos de otra persona. Se devuelve ya resuelto, y no es un adorno: es
/// <b>lo único que separa una anotación rutinaria de una que hay que mirar</b>, y quien lee el libro no
/// debería tener que comparar dos identificadores para averiguarlo.
/// </param>
/// <remarks>
/// Los nombres del actor y del dueño vienen <b>ya resueltos</b> y no como identificadores. El
/// identificador no le dice nada a nadie, y el libro de auditoría existe precisamente para que una persona
/// lo lea y entienda qué pasó.
/// </remarks>
public sealed record AuditEntry(
    Guid Id,
    DateTimeOffset OccurredAtUtc,
    string ActorName,
    string OwnerName,
    string Action,
    string EntityType,
    Guid EntityId,
    IReadOnlyList<FieldChange> Changes,
    string? Reason,
    bool IsSomeoneElseData);

/// <summary>
/// Qué anotaciones se quieren.
/// </summary>
/// <param name="OwnerUserId">Solo las de esta persona, o nulo para todas.</param>
/// <param name="ActorUserId">Solo las hechas por esta persona, o nulo para todas.</param>
/// <param name="EntityType">Solo las de esta entidad, o nulo para todas.</param>
/// <param name="From">Desde cuándo, inclusive.</param>
/// <param name="To">Hasta cuándo, inclusive.</param>
/// <param name="OnlySomeoneElseData">
/// Si solo se quieren las que tocan datos ajenos. Es el filtro que de verdad se usa: de todo el libro, lo
/// que hay que revisar son <b>las que no deberían ser rutina</b>.
/// </param>
/// <param name="Limit">Cuántas como mucho.</param>
public sealed record AuditFilter(
    Guid? OwnerUserId = null,
    Guid? ActorUserId = null,
    string? EntityType = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    bool OnlySomeoneElseData = false,
    int Limit = 200);
