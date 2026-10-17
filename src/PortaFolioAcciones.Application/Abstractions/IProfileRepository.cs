using PortaFolioAcciones.Application.Users;

namespace PortaFolioAcciones.Application.Abstractions;

/// <summary>
/// Da acceso al perfil de quien tiene la sesión abierta.
///
/// Es un <b>puerto</b>, igual que los demás. Y como los demás, <b>ningún método recibe el usuario</b>:
/// quien llama no puede elegir de quién lee o escribe el perfil. El usuario sale de la sesión.
///
/// Esto importa más aquí que en ningún otro sitio: si se pudiera pasar un identificador, cualquier
/// persona podría cambiarse la moneda, el idioma… <b>o los permisos</b> de otra.
/// </summary>
public interface IProfileRepository
{
    /// <summary>
    /// El perfil de quien llama, o <c>null</c> si no hay sesión o el perfil no existe.
    ///
    /// Los dos casos devuelven lo mismo: una credencial sin perfil es un estado roto, no una sesión.
    /// </summary>
    /// <param name="cancellationToken">Cancelación.</param>
    public Task<ProfileSummary?> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Cambia las preferencias: moneda base e idioma.
    /// </summary>
    /// <param name="baseCurrency">Código de la moneda en la que quiere ver sus totales.</param>
    /// <param name="language">Código del idioma de la interfaz.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    /// <exception cref="ArgumentException">
    /// Si la moneda o el idioma no tienen una forma válida. Se lanza en vez de devolver nulo porque
    /// <b>no es lo mismo</b>: un código mal escrito es un error de quien llama, y confundirlo con «no
    /// existe» haría creer que el perfil se ha perdido.
    /// </exception>
    public Task<ProfileSummary?> UpdatePreferencesAsync(
        string baseCurrency,
        string language,
        CancellationToken cancellationToken = default);
}
