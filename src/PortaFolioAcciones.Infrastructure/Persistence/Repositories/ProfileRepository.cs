using Microsoft.EntityFrameworkCore;
using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Application.Users;
using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.ValueObjects;
using PortaFolioAcciones.Infrastructure.Persistence;

namespace PortaFolioAcciones.Infrastructure.Persistence.Repositories;

/// <summary>
/// Acceso al perfil de quien tiene la sesión abierta.
///
/// <b>No hay forma de pedir el perfil de otra persona</b>: el identificador sale de
/// <see cref="ICurrentUser"/> y no de un parámetro. Es la misma regla que en las carteras, y aquí pesa
/// más: si se pudiera pasar un identificador, cualquiera podría cambiarse el idioma, la moneda…
/// <b>o los permisos</b> de otra.
/// </summary>
internal sealed class ProfileRepository : IProfileRepository
{
    private readonly PortafolioDbContext _context;
    private readonly ICurrentUser _currentUser;

    public ProfileRepository(PortafolioDbContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ProfileSummary?> GetAsync(CancellationToken cancellationToken = default)
    {
        var perfil = await CargarAsync(cancellationToken);

        return perfil is null ? null : AResumen(perfil);
    }

    public async Task<ProfileSummary?> UpdatePreferencesAsync(
        string baseCurrency,
        string language,
        CancellationToken cancellationToken = default)
    {
        var perfil = await CargarAsync(cancellationToken);

        if (perfil is null)
        {
            return null;
        }

        // Los códigos se validan ANTES de tocar nada. Si se cambiara primero la moneda y el idioma
        // fallara, el perfil quedaría a medias, y quien lo pidió vería un error creyendo que no se cambió
        // nada.
        var moneda = Currency.FromCode(baseCurrency);
        var idioma = AppLanguage.FromCode(language);

        var ahora = DateTimeOffset.UtcNow;

        perfil.ChangeBaseCurrency(moneda, ahora);
        perfil.ChangePreferredLanguage(idioma, ahora);

        await _context.SaveChangesAsync(cancellationToken);

        return AResumen(perfil);
    }

    private async Task<User?> CargarAsync(CancellationToken cancellationToken)
    {
        if (_currentUser.Id is not { } id)
        {
            return null;
        }

        // Sin `AsNoTracking`: esto se va a modificar.
        return await _context.UserProfiles.SingleOrDefaultAsync(perfil => perfil.Id == id, cancellationToken);
    }

    private static ProfileSummary AResumen(User perfil) =>
        new(
            perfil.Id,
            perfil.DisplayName,
            perfil.BaseCurrency.Code,
            perfil.PreferredLanguage.Code,
            perfil.IsAdmin);
}
