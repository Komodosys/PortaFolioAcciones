using Microsoft.AspNetCore.Identity;

namespace PortaFolioAcciones.Infrastructure.Identity;

/// <summary>
/// La cuenta de una persona <b>para autenticarse</b>. Identity guarda aqui el nombre de usuario, el
/// hash de la contrasena, los intentos fallidos y los datos de doble factor.
///
/// El perfil —como se llama, su moneda, su idioma, si es administrador— vive en la entidad <c>User</c>
/// del dominio, en su propia tabla. Lo unico que las une es que <b>comparten identificador</b>: no hay
/// dos identidades que mantener en sincronia, y el dominio no sabe nada de como se autentica la gente.
///
/// El identificador es <see cref="Guid"/> y no el texto por defecto de Identity, para que coincida con
/// el del dominio sin conversiones.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>
    /// Dice si la contrasena es <b>temporal</b> y hay que cambiarla al entrar.
    ///
    /// Lo pone el administrador al restablecer una contrasena, porque en esta aplicacion no hay correo:
    /// la temporal se entrega <b>en mano</b> y hay que obligar a cambiarla, o se quedaria puesta para
    /// siempre y el administrador seguiria conociendola.
    /// </summary>
    public bool MustChangePassword { get; set; }

    /// <summary>
    /// Hasta cuando vale la contrasena temporal. Pasado ese plazo ya no sirve para entrar.
    ///
    /// Una contrasena temporal que no caduca es una contrasena permanente que ademas conocen dos
    /// personas.
    /// </summary>
    public DateTimeOffset? TemporaryPasswordExpiresAtUtc { get; set; }
}
