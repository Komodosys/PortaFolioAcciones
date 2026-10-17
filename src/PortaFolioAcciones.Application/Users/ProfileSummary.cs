namespace PortaFolioAcciones.Application.Users;

/// <summary>
/// El perfil de una persona, tal como lo ve la interfaz.
/// </summary>
/// <param name="Id">Identificador.</param>
/// <param name="DisplayName">Como se llama, para mostrarlo.</param>
/// <param name="BaseCurrency">Moneda en la que quiere ver sus totales.</param>
/// <param name="Language">Idioma de la interfaz.</param>
/// <param name="IsAdmin">Si puede ver y modificar los datos de las demas personas.</param>
public sealed record ProfileSummary(
    Guid Id,
    string DisplayName,
    string BaseCurrency,
    string Language,
    bool IsAdmin);
