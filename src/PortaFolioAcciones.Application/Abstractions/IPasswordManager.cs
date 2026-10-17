using PortaFolioAcciones.Application.Users;

namespace PortaFolioAcciones.Application.Abstractions;

/// <summary>
/// Gestiona las contrasenas: restablecerlas (el administrador) y cambiarlas (cada persona).
///
/// Es un <b>puerto</b>: la aplicacion no tiene por que saber como se guarda un hash ni que detras esta
/// ASP.NET Core Identity.
/// </summary>
public interface IPasswordManager
{
    /// <summary>
    /// Genera una contrasena <b>temporal</b> para otra persona.
    ///
    /// Tres cosas van juntas y no se pueden separar: la temporal se enseña una sola vez, <b>caduca</b>, y
    /// obliga a cambiarla al entrar. Sin lo primero, el administrador se queda con una contrasena que
    /// conoce para siempre; sin lo segundo, una temporal se convierte en permanente; sin lo tercero, se
    /// queda puesta y la conoce alguien mas que su dueño.
    /// </summary>
    /// <param name="userId">A quien se le restablece.</param>
    /// <param name="cancellationToken">Cancelacion.</param>
    public Task<PasswordResetResult> ResetAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cambia la contrasena de alguien, comprobando antes la que tiene.
    /// </summary>
    /// <param name="userId">Quien la cambia.</param>
    /// <param name="currentPassword">La que tiene ahora.</param>
    /// <param name="newPassword">La nueva.</param>
    /// <param name="cancellationToken">Cancelacion.</param>
    public Task<PasswordChangeResult> ChangeAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);
}
