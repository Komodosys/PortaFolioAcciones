using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Domain.Entities;

/// <summary>
/// Una persona.
///
/// <b>Aqui no hay contrasena.</b> Las credenciales las guarda ASP.NET Core Identity en sus propias
/// tablas ([ADR-006](../../../DOCUMENTACION/decisiones/ADR-006-autenticacion-local-identity-cookie.md)),
/// y esta entidad guarda el perfil: como se llama, en que moneda quiere ver sus totales, en que idioma
/// y si es administrador. Separarlo es lo que permite que el dominio no sepa nada de como se
/// autentica la gente.
///
/// La relacion entre las dos es que <b>comparten identificador</b>: no hay dos identidades que
/// mantener en sincronia.
/// </summary>
public sealed class User
{
    /// <summary>Longitud maxima del nombre visible.</summary>
    public const int MaxDisplayNameLength = 100;

    private User(
        Guid id,
        string displayName,
        Currency baseCurrency,
        AppLanguage preferredLanguage,
        DateTimeOffset now)
    {
        Id = id;
        DisplayName = displayName;
        BaseCurrency = baseCurrency;
        PreferredLanguage = preferredLanguage;
        IsAdmin = false;
        IsActive = true;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
    }

    /// <summary>Constructor sin parametros para el mapeo de la base de datos.</summary>
    private User()
    {
        DisplayName = string.Empty;
        BaseCurrency = Currency.Cad;
        PreferredLanguage = AppLanguage.Fallback;
    }

    /// <summary>
    /// Identificador de la persona. Es el mismo que usa Identity para las credenciales.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>Como se llama, para mostrarlo.</summary>
    public string DisplayName { get; private set; }

    /// <summary>Correo electronico. Sirve para identificar la cuenta, no para enviar nada: no hay servidor de correo.</summary>
    public string? Email { get; private set; }

    /// <summary>Moneda en la que quiere ver sus totales.</summary>
    public Currency BaseCurrency { get; private set; }

    /// <summary>Idioma de la interfaz para esta persona.</summary>
    public AppLanguage PreferredLanguage { get; private set; }

    /// <summary>
    /// Dice si es administrador. Puede <b>ver y modificar</b> los datos de las demas personas, y cada
    /// cambio suyo sobre datos ajenos queda auditado
    /// ([ADR-014](../../../DOCUMENTACION/decisiones/ADR-014-administrador-escritura-con-auditoria.md)).
    /// </summary>
    public bool IsAdmin { get; private set; }

    /// <summary>Dice si la cuenta puede entrar. Se desactiva en vez de borrar: sus carteras son historia.</summary>
    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>Crea una persona.</summary>
    public static User Create(
        string displayName,
        Currency baseCurrency,
        AppLanguage preferredLanguage,
        DateTimeOffset now,
        string? email = null)
    {
        ArgumentNullException.ThrowIfNull(baseCurrency);
        ArgumentNullException.ThrowIfNull(preferredLanguage);

        ValidarNombre(displayName);

        var user = new User(Guid.NewGuid(), displayName.Trim(), baseCurrency, preferredLanguage, now);

        if (email is not null)
        {
            user.SetEmail(email, now);
        }

        return user;
    }

    /// <summary>Cambia el nombre visible.</summary>
    public void Rename(string displayName, DateTimeOffset now)
    {
        ValidarNombre(displayName);
        DisplayName = displayName.Trim();
        Touch(now);
    }

    /// <summary>Cambia el correo.</summary>
    public void SetEmail(string email, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        Email = email.Trim().ToLowerInvariant();
        Touch(now);
    }

    /// <summary>Cambia la moneda en la que ve sus totales.</summary>
    public void ChangeBaseCurrency(Currency currency, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(currency);
        BaseCurrency = currency;
        Touch(now);
    }

    /// <summary>Cambia el idioma de la interfaz.</summary>
    public void ChangePreferredLanguage(AppLanguage language, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(language);
        PreferredLanguage = language;
        Touch(now);
    }

    /// <summary>
    /// Da permisos de administracion.
    /// </summary>
    /// <remarks>
    /// Que <b>el ultimo administrador no pueda perder ese papel</b> no se comprueba aqui: hace falta
    /// saber cuantos administradores activos hay, y eso es informacion de mas de una persona. Es una
    /// regla de la capa de aplicacion, que si puede consultarlas todas.
    /// </remarks>
    public void GrantAdmin(DateTimeOffset now)
    {
        IsAdmin = true;
        Touch(now);
    }

    /// <summary>Retira los permisos de administracion.</summary>
    public void RevokeAdmin(DateTimeOffset now)
    {
        IsAdmin = false;
        Touch(now);
    }

    /// <summary>Desactiva la cuenta.</summary>
    public void Deactivate(DateTimeOffset now)
    {
        IsActive = false;
        Touch(now);
    }

    /// <summary>Vuelve a activar la cuenta.</summary>
    public void Activate(DateTimeOffset now)
    {
        IsActive = true;
        Touch(now);
    }

    private static void ValidarNombre(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        if (displayName.Trim().Length > MaxDisplayNameLength)
        {
            throw new ArgumentException(
                $"El nombre no puede pasar de {MaxDisplayNameLength} caracteres.", nameof(displayName));
        }
    }

    private void Touch(DateTimeOffset now) => UpdatedAtUtc = now;
}
