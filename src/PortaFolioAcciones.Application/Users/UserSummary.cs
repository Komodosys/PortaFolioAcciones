namespace PortaFolioAcciones.Application.Users;

/// <summary>
/// Una cuenta, tal como la ve el administrador.
/// </summary>
/// <param name="Id">Identificador.</param>
/// <param name="UserName">Con qué nombre entra.</param>
/// <param name="DisplayName">Cómo se llama, para mostrarlo.</param>
/// <param name="BaseCurrency">Moneda en la que ve sus totales.</param>
/// <param name="Language">Idioma de su interfaz.</param>
/// <param name="IsAdmin">Si puede ver y modificar los datos de las demás personas.</param>
/// <param name="MustChangePassword">
/// Si tiene una contraseña temporal pendiente de cambiar. Se enseña porque una cuenta con la contraseña
/// temporal sin usar <b>no ha entrado todavía</b>, y eso es distinto de una cuenta que se usa.
/// </param>
/// <param name="IsActive">Si la cuenta está en uso.</param>
/// <remarks>
/// **No lleva nada de la contraseña**, y no hace falta decirlo: lo que no se guarda en claro no se puede
/// devolver. El administrador puede <b>generar una temporal</b>, pero no leer la que alguien tiene.
/// </remarks>
public sealed record UserSummary(
    Guid Id,
    string UserName,
    string DisplayName,
    string BaseCurrency,
    string Language,
    bool IsAdmin,
    bool MustChangePassword,
    bool IsActive);
