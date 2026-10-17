using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Domain.Entities;

/// <summary>
/// El cambio de un campo: que habia antes y que hay despues.
///
/// Se guardan <b>los dos valores</b> y no solo el nuevo. Un registro que dijera «se modifico la
/// posicion» no sirve para nada; lo que sirve es «cantidad: 100 a 120».
/// </summary>
/// <param name="Field">Nombre del campo.</param>
/// <param name="OldValue">Valor anterior, en texto. Nulo si no habia.</param>
/// <param name="NewValue">Valor nuevo, en texto. Nulo si se dejo vacio.</param>
public sealed record FieldChange(string Field, string? OldValue, string? NewValue);

/// <summary>
/// El libro de auditoria: quien cambio que, cuando y de quien era.
///
/// Existe porque el administrador puede <b>modificar datos de otras personas</b>
/// ([ADR-014](../../../DOCUMENTACION/decisiones/ADR-014-administrador-escritura-con-auditoria.md)).
/// La auditoria es la contrapartida de ese poder, no un adorno.
///
/// Dos decisiones que hacen que este libro sirva de algo:
///
/// <list type="number">
///   <item><description><b>Es de solo anadir.</b> No hay forma de editarlo ni de borrarlo desde la
///   aplicacion, y la base de datos <b>rechaza</b> un <c>UPDATE</c> o un <c>DELETE</c>. Si la tabla
///   permitiera borrarse, el registro no valdria como registro.</description></item>
///   <item><description><b>No tiene clave ajena hacia la entidad auditada.</b> Guarda el tipo y el
///   identificador como referencia logica. Si tuviera cascada, borrar una posicion se llevaria por
///   delante justo la prueba de que existio.</description></item>
/// </list>
/// </summary>
public sealed class AuditLog
{
    private AuditLog(
        Guid id,
        DateTimeOffset occurredAtUtc,
        Guid actorUserId,
        AuditAction action,
        string entityType,
        Guid entityId,
        Guid ownerUserId,
        IReadOnlyList<FieldChange> changes,
        string? reason)
    {
        Id = id;
        OccurredAtUtc = occurredAtUtc;
        ActorUserId = actorUserId;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        OwnerUserId = ownerUserId;
        Changes = changes;
        Reason = reason;
    }

    /// <summary>Constructor sin parametros para el mapeo de la base de datos.</summary>
    private AuditLog()
    {
        EntityType = string.Empty;
        Changes = [];
    }

    public Guid Id { get; private set; }

    /// <summary>Cuando ocurrio, en UTC.</summary>
    public DateTimeOffset OccurredAtUtc { get; private set; }

    /// <summary>Quien lo hizo.</summary>
    public Guid ActorUserId { get; private set; }

    /// <summary>Que hizo.</summary>
    public AuditAction Action { get; private set; }

    /// <summary>Tipo de la entidad tocada, como texto. Es una referencia logica, no una clave ajena.</summary>
    public string EntityType { get; private set; }

    /// <summary>Identificador de la entidad tocada. Tampoco es clave ajena, a proposito.</summary>
    public Guid EntityId { get; private set; }

    /// <summary>De quien era el dato. Es lo que permite responder «que le han tocado a Titular B».</summary>
    public Guid OwnerUserId { get; private set; }

    /// <summary>Los campos que cambiaron, con su valor anterior y el nuevo.</summary>
    public IReadOnlyList<FieldChange> Changes { get; private set; }

    /// <summary>Motivo que escribio quien lo hizo, si lo escribio.</summary>
    public string? Reason { get; private set; }

    /// <summary>Dice si el cambio fue sobre datos de otra persona, que es cuando la ley va aparte.</summary>
    public bool IsChangeToSomeoneElseData => ActorUserId != OwnerUserId;

    /// <summary>Anota un cambio.</summary>
    /// <exception cref="ArgumentException">Si falta el tipo de entidad o el actor.</exception>
    public static AuditLog Record(
        DateTimeOffset occurredAtUtc,
        Guid actorUserId,
        AuditAction action,
        string entityType,
        Guid entityId,
        Guid ownerUserId,
        IReadOnlyList<FieldChange>? changes = null,
        string? reason = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);

        if (actorUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "Un registro de auditoria tiene que decir quien lo hizo. Sin autor no audita nada.",
                nameof(actorUserId));
        }

        if (entityId == Guid.Empty)
        {
            throw new ArgumentException("Un registro de auditoria tiene que decir sobre que.", nameof(entityId));
        }

        return new AuditLog(
            Guid.NewGuid(),
            occurredAtUtc,
            actorUserId,
            action,
            entityType.Trim(),
            entityId,
            ownerUserId,
            changes ?? [],
            reason);
    }

    public override string ToString() =>
        string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{OccurredAtUtc:u} {Action} {EntityType} {EntityId} por {ActorUserId}");
}
