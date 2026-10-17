using Microsoft.AspNetCore.Identity;
using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Application.Users;
using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.ValueObjects;
using PortaFolioAcciones.Infrastructure.Persistence;

namespace PortaFolioAcciones.Infrastructure.Identity;

/// <summary>
/// Crea cuentas: el perfil en la tabla del dominio y la credencial en la de Identity, <b>con el mismo
/// identificador</b> y en la misma transaccion.
///
/// Lo de la transaccion no es un adorno: si se guardara el perfil y fallara la credencial, quedaria una
/// persona que no puede entrar y que nadie sabria como arreglar.
/// </summary>
internal sealed class AccountCreator : IAccountCreator
{
    private readonly PortafolioDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUser _currentUser;

    public AccountCreator(
        PortafolioDbContext context,
        UserManager<ApplicationUser> userManager,
        ICurrentUser currentUser)
    {
        _context = context;
        _userManager = userManager;
        _currentUser = currentUser;
    }

    public async Task<AccountCreationResult> CreateAsync(
        CreateAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Solo un administrador crea cuentas. No hay registro publico.
        if (_currentUser.IsAdmin is false)
        {
            return AccountCreationResult.Failed("account.not_allowed");
        }

        if (!TryParse(command, out var currency, out var language, out var error))
        {
            return AccountCreationResult.Failed(error);
        }

        var now = DateTimeOffset.UtcNow;

        var profile = User.Create(
            command.DisplayName,
            currency!,
            language!,
            now);

        if (command.IsAdmin)
        {
            profile.GrantAdmin(now);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        _context.UserProfiles.Add(profile);
        await _context.SaveChangesAsync(cancellationToken);

        var credential = new ApplicationUser
        {
            Id = profile.Id,
            UserName = command.UserName,
        };

        var result = await _userManager.CreateAsync(credential, command.Password);

        if (!result.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);

            return AccountCreationResult.Failed(
                [.. result.Errors.Select(e => $"identity.{e.Code}")]);
        }

        if (command.IsAdmin)
        {
            await _userManager.AddToRoleAsync(credential, AccountRoles.Administrator);
        }

        await transaction.CommitAsync(cancellationToken);

        return AccountCreationResult.Ok(profile.Id);
    }

    /// <summary>
    /// Convierte los textos que llegan de fuera a los objetos de valor del dominio. Si algo no encaja,
    /// se dice <b>que</b> no encaja, con un codigo, y no se crea nada a medias.
    /// </summary>
    private static bool TryParse(
        CreateAccountCommand command,
        out Currency? currency,
        out AppLanguage? language,
        out string error)
    {
        currency = null;
        language = null;
        error = string.Empty;

        try
        {
            currency = Currency.FromCode(command.BaseCurrency);
        }
        catch (ArgumentException)
        {
            error = "account.invalid_currency";
            return false;
        }

        try
        {
            language = AppLanguage.FromCode(command.Language);
        }
        catch (ArgumentException)
        {
            error = "account.invalid_language";
            return false;
        }

        return true;
    }
}
