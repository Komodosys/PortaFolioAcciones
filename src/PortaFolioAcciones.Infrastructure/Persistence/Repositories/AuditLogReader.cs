using Microsoft.EntityFrameworkCore;
using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Application.Audit;
using PortaFolioAcciones.Infrastructure.Persistence;

namespace PortaFolioAcciones.Infrastructure.Persistence.Repositories;

/// <summary>
/// Lee el libro de auditoría.
///
/// <b>No escribe nada, y no puede.</b> El libro es de solo añadir: lo escribe el interceptor, en la misma
/// transacción que el cambio. Si desde aquí se pudiera tocar, el registro dejaría de valer como registro.
/// </summary>
internal sealed class AuditLogReader : IAuditLogReader
{
    private readonly PortafolioDbContext _context;

    public AuditLogReader(PortafolioDbContext context) => _context = context;

    public async Task<IReadOnlyList<AuditEntry>> ReadAsync(
        AuditFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var consulta = _context.AuditLogs.AsNoTracking();

        if (filter.OwnerUserId is { } dueno)
        {
            consulta = consulta.Where(log => log.OwnerUserId == dueno);
        }

        if (filter.ActorUserId is { } actor)
        {
            consulta = consulta.Where(log => log.ActorUserId == actor);
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityType))
        {
            var entidad = filter.EntityType.Trim();
            consulta = consulta.Where(log => log.EntityType == entidad);
        }

        if (filter.From is { } desde)
        {
            consulta = consulta.Where(log => log.OccurredAtUtc >= desde);
        }

        if (filter.To is { } hasta)
        {
            consulta = consulta.Where(log => log.OccurredAtUtc <= hasta);
        }

        if (filter.OnlySomeoneElseData)
        {
            // La comparacion se hace en la base y no despues: si se filtrara en memoria, habria que traerse
            // el libro entero para quedarse con una parte, y el libro solo crece.
            consulta = consulta.Where(log => log.ActorUserId != log.OwnerUserId);
        }

        var anotaciones = await consulta
            // Lo mas reciente primero: en un libro de auditoria se busca lo ultimo que ha pasado.
            .OrderByDescending(log => log.OccurredAtUtc)
            .ThenByDescending(log => log.Id)
            .Take(Math.Clamp(filter.Limit, 1, 1000))
            .ToListAsync(cancellationToken);

        if (anotaciones.Count == 0)
        {
            return [];
        }

        var nombres = await NombresDeAsync(anotaciones, cancellationToken);

        return
        [
            .. anotaciones.Select(log => new AuditEntry(
                log.Id,
                log.OccurredAtUtc,
                nombres.GetValueOrDefault(log.ActorUserId) ?? "?",
                nombres.GetValueOrDefault(log.OwnerUserId) ?? "?",
                log.Action.ToString(),
                log.EntityType,
                log.EntityId,
                log.Changes,
                log.Reason,
                log.IsChangeToSomeoneElseData))
        ];
    }

    public async Task<IReadOnlyList<PersonaDelLibro>> PeopleAsync(CancellationToken cancellationToken = default)
    {
        // Se sacan del propio libro y no de la lista de cuentas, para que solo salgan personas que de
        // verdad tienen algo anotado.
        //
        // Y se sacan los DUENOS, no los autores: el filtro de la pantalla pregunta «¿que le han tocado a
        // esta persona?». Ofrecer a alguien que solo aparece como autor daria una lista que no devuelve
        // nada al elegirla, o sea un filtro que parece roto.
        var ids = await _context.AuditLogs
            .AsNoTracking()
            .Select(log => log.OwnerUserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return await _context.UserProfiles
            .AsNoTracking()
            .Where(perfil => ids.Contains(perfil.Id))
            .OrderBy(perfil => perfil.DisplayName)
            .Select(perfil => new PersonaDelLibro(perfil.Id, perfil.DisplayName))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Los nombres de todos los que aparecen en estas anotaciones, en una sola consulta.</summary>
    private async Task<IReadOnlyDictionary<Guid, string>> NombresDeAsync(
        IReadOnlyList<Domain.Entities.AuditLog> anotaciones,
        CancellationToken cancellationToken)
    {
        var ids = anotaciones
            .SelectMany(log => new[] { log.ActorUserId, log.OwnerUserId })
            .Distinct()
            .ToList();

        return await _context.UserProfiles
            .AsNoTracking()
            .Where(perfil => ids.Contains(perfil.Id))
            .ToDictionaryAsync(perfil => perfil.Id, perfil => perfil.DisplayName, cancellationToken);
    }
}
