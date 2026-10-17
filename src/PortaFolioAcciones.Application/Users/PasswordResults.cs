namespace PortaFolioAcciones.Application.Users;

/// <summary>
/// Resultado de restablecer una contrasena.
/// </summary>
/// <param name="Succeeded">Si se restablecio.</param>
/// <param name="TemporaryPassword">
/// La contrasena temporal, que se enseña <b>una sola vez</b>. No se guarda en ningun sitio en claro: si
/// se pierde, hay que generar otra.
/// </param>
/// <param name="ExpiresAtUtc">Hasta cuando vale.</param>
/// <param name="Errors">Codigos de los problemas, no textos.</param>
public sealed record PasswordResetResult(
    bool Succeeded,
    string? TemporaryPassword,
    DateTimeOffset? ExpiresAtUtc,
    IReadOnlyList<string> Errors)
{
    /// <summary>Restablecida.</summary>
    public static PasswordResetResult Ok(string temporaryPassword, DateTimeOffset expiresAtUtc) =>
        new(true, temporaryPassword, expiresAtUtc, []);

    /// <summary>No se pudo, por los motivos indicados.</summary>
    public static PasswordResetResult Failed(params string[] errors) => new(false, null, null, errors);
}

/// <summary>
/// Resultado de cambiar la propia contrasena.
/// </summary>
/// <param name="Succeeded">Si se cambio.</param>
/// <param name="Errors">Codigos de los problemas, no textos.</param>
public sealed record PasswordChangeResult(bool Succeeded, IReadOnlyList<string> Errors)
{
    /// <summary>Cambiada.</summary>
    public static PasswordChangeResult Ok() => new(true, []);

    /// <summary>No se pudo, por los motivos indicados.</summary>
    public static PasswordChangeResult Failed(params string[] errors) => new(false, errors);
}
