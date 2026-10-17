namespace PortaFolioAcciones.Domain.ValueObjects;

/// <summary>
/// Que le ha pasado a una entidad, segun el libro de auditoria.
/// </summary>
public enum AuditAction
{
    /// <summary>Se creo.</summary>
    Created = 0,

    /// <summary>Se modifico. Los campos que cambiaron van en el detalle.</summary>
    Updated = 1,

    /// <summary>Se borro.</summary>
    Deleted = 2,
}
