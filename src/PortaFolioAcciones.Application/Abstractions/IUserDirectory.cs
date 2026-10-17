using PortaFolioAcciones.Application.Users;

namespace PortaFolioAcciones.Application.Abstractions;

/// <summary>
/// Las cuentas que hay, para el administrador.
///
/// Es un puerto <b>de solo lectura</b>: crear cuentas es de <see cref="IAccountCreator"/>, y cambiarles la
/// contraseña, de <see cref="IPasswordManager"/>. Aquí solo se miran.
///
/// Existe porque un administrador tiene que poder responder <b>quién tiene acceso</b> a los datos de la
/// casa, y hasta ahora eso solo se podía averiguar entrando en la base de datos.
/// </summary>
public interface IUserDirectory
{
    /// <summary>Todas las cuentas, ordenadas por nombre.</summary>
    /// <param name="cancellationToken">Cancelación.</param>
    public Task<IReadOnlyList<UserSummary>> ListAsync(CancellationToken cancellationToken = default);
}
