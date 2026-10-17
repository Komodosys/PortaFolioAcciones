namespace PortaFolioAcciones.Application.Users;

/// <summary>
/// Datos para crear una cuenta.
/// </summary>
/// <param name="UserName">Nombre de usuario para entrar.</param>
/// <param name="DisplayName">Como se llama, para mostrarlo.</param>
/// <param name="Password">Contrasena.</param>
/// <param name="BaseCurrency">Moneda en la que quiere ver sus totales.</param>
/// <param name="Language">Idioma de la interfaz.</param>
/// <param name="IsAdmin">Si tendra permisos de administracion.</param>
public sealed record CreateAccountCommand(
    string UserName,
    string DisplayName,
    string Password,
    string BaseCurrency,
    string Language,
    bool IsAdmin);

/// <summary>
/// Resultado de crear una cuenta.
/// </summary>
/// <param name="Succeeded">Si se creo.</param>
/// <param name="UserId">Identificador, si se creo.</param>
/// <param name="Errors">
/// Codigos de los problemas, no textos: el texto lo pone el frontend, que es quien sabe el idioma
/// ([ADR-013](../../../DOCUMENTACION/decisiones/ADR-013-interfaz-multi-idioma.md)).
/// </param>
public sealed record AccountCreationResult(bool Succeeded, Guid? UserId, IReadOnlyList<string> Errors)
{
    /// <summary>Cuenta creada.</summary>
    public static AccountCreationResult Ok(Guid userId) => new(true, userId, []);

    /// <summary>No se creo, por los motivos indicados.</summary>
    public static AccountCreationResult Failed(params string[] errors) => new(false, null, errors);
}
