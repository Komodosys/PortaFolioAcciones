using Microsoft.AspNetCore.Identity;
using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Application.Users;

namespace PortaFolioAcciones.Infrastructure.Identity;

/// <summary>
/// Restablece y cambia contrasenas con ASP.NET Core Identity.
/// </summary>
internal sealed class PasswordManager : IPasswordManager
{
    /// <summary>
    /// Cuanto vale una contrasena temporal. Dos dias: tiempo de sobra para entregarla en mano a alguien
    /// de la familia, y lo bastante corto para que no se quede por ahi.
    /// </summary>
    public static readonly TimeSpan TemporaryPasswordValidity = TimeSpan.FromHours(48);

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUser _currentUser;

    public PasswordManager(UserManager<ApplicationUser> userManager, ICurrentUser currentUser)
    {
        _userManager = userManager;
        _currentUser = currentUser;
    }

    public async Task<PasswordResetResult> ResetAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // Solo un administrador restablece contrasenas ajenas.
        if (_currentUser.IsAdmin is false)
        {
            return PasswordResetResult.Failed("password.not_allowed");
        }

        var usuario = await _userManager.FindByIdAsync(userId.ToString());

        if (usuario is null)
        {
            return PasswordResetResult.Failed("password.user_not_found");
        }

        var temporal = PasswordGenerator.Generate();
        var expira = DateTimeOffset.UtcNow.Add(TemporaryPasswordValidity);

        var testigo = await _userManager.GeneratePasswordResetTokenAsync(usuario);
        var resultado = await _userManager.ResetPasswordAsync(usuario, testigo, temporal);

        if (!resultado.Succeeded)
        {
            return PasswordResetResult.Failed([.. resultado.Errors.Select(e => $"identity.{e.Code}")]);
        }

        usuario.MustChangePassword = true;
        usuario.TemporaryPasswordExpiresAtUtc = expira;

        await _userManager.UpdateAsync(usuario);

        // Se invalida el testigo de seguridad, y con el TODAS las sesiones abiertas de esa persona. Es lo
        // que hace que restablecer una contrasena sirva de algo cuando se sospecha que alguien ha entrado
        // donde no debia: si la sesion siguiera viva, cambiar la contrasena no lo echara.
        await _userManager.UpdateSecurityStampAsync(usuario);

        return PasswordResetResult.Ok(temporal, expira);
    }

    public async Task<PasswordChangeResult> ChangeAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var usuario = await _userManager.FindByIdAsync(userId.ToString());

        if (usuario is null)
        {
            return PasswordChangeResult.Failed("password.user_not_found");
        }

        // ChangePasswordAsync comprueba la actual y aplica la politica a la nueva. No se comprueba a
        // mano: seria reimplementar la regla y poder equivocarse en la copia.
        var resultado = await _userManager.ChangePasswordAsync(usuario, currentPassword, newPassword);

        if (!resultado.Succeeded)
        {
            return PasswordChangeResult.Failed([.. resultado.Errors.Select(e => $"identity.{e.Code}")]);
        }

        // Ya no es temporal. Si no se quitara la marca, la aplicacion seguiria pidiendo cambiarla.
        usuario.MustChangePassword = false;
        usuario.TemporaryPasswordExpiresAtUtc = null;

        await _userManager.UpdateAsync(usuario);

        return PasswordChangeResult.Ok();
    }
}
