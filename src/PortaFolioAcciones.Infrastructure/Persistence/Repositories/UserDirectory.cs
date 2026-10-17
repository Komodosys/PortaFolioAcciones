using Microsoft.EntityFrameworkCore;
using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Application.Users;
using PortaFolioAcciones.Infrastructure.Persistence;

namespace PortaFolioAcciones.Infrastructure.Persistence.Repositories;

/// <summary>
/// Las cuentas que hay.
///
/// <b>Hay que cruzar dos tablas</b>, y conviene tenerlo claro: `UserProfiles` guarda el perfil del dominio
/// —el nombre, la moneda, el idioma— y `AspNetUsers` guarda las credenciales —el nombre de entrada y el
/// estado de la contraseña—. Comparten el identificador, pero son dos cosas distintas, y por eso el
/// nombre de entrada no está en el perfil.
/// </summary>
internal sealed class UserDirectory : IUserDirectory
{
    private readonly PortafolioDbContext _context;

    public UserDirectory(PortafolioDbContext context) => _context = context;

    public async Task<IReadOnlyList<UserSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        var cuentas = await _context.UserProfiles
            .AsNoTracking()
            .Join(
                _context.Users.AsNoTracking(),
                perfil => perfil.Id,
                cuenta => cuenta.Id,
                (perfil, cuenta) => new
                {
                    perfil.Id,
                    cuenta.UserName,
                    perfil.DisplayName,
                    perfil.BaseCurrency,
                    perfil.PreferredLanguage,
                    perfil.IsAdmin,
                    cuenta.MustChangePassword,
                    perfil.IsActive,
                })
            .OrderBy(fila => fila.DisplayName)
            .ToListAsync(cancellationToken);

        return
        [
            .. cuentas.Select(cuenta => new UserSummary(
                cuenta.Id,
                cuenta.UserName ?? "?",
                cuenta.DisplayName,
                cuenta.BaseCurrency.Code,
                cuenta.PreferredLanguage.Code,
                cuenta.IsAdmin,
                cuenta.MustChangePassword,
                cuenta.IsActive))
        ];
    }
}
