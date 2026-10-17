using PortaFolioAcciones.Application.Users;

namespace PortaFolioAcciones.Application.Abstractions;

/// <summary>
/// Crea cuentas. Es un <b>puerto</b>: la aplicacion no tiene por que saber que detras esta ASP.NET Core
/// Identity ni como se guarda una contrasena.
///
/// Crear una cuenta son <b>dos cosas a la vez</b>: el perfil (nombre, moneda, idioma) y la credencial.
/// Quien implemente este puerto tiene que hacerlas juntas: media cuenta es una persona que no puede
/// entrar.
/// </summary>
public interface IAccountCreator
{
    /// <summary>Crea una cuenta con su perfil y su credencial.</summary>
    /// <param name="command">Los datos.</param>
    /// <param name="cancellationToken">Cancelacion.</param>
    public Task<AccountCreationResult> CreateAsync(
        CreateAccountCommand command,
        CancellationToken cancellationToken = default);
}
