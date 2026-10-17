using Microsoft.AspNetCore.Identity;
using PortaFolioAcciones.Infrastructure.Identity;
using PortaFolioAcciones.Infrastructure.Persistence;

namespace PortaFolioAcciones.Api.Authentication;

/// <summary>
/// Configuracion de la autenticacion: Identity local con cookie, sin registro publico
/// ([ADR-006](../../../DOCUMENTACION/decisiones/ADR-006-autenticacion-local-identity-cookie.md)).
///
/// Es una aplicacion <b>privada</b>: no hay usuarios por captar, y el registro abierto solo añadiria
/// superficie de ataque. Las cuentas las crea el administrador.
/// </summary>
internal static class AuthenticationSetup
{
    /// <summary>
    /// Longitud minima de la contrasena. Doce caracteres, y no se exige simbolo raro: una frase larga
    /// es mas segura y mucho mas facil de recordar que ocho caracteres con un signo de puntacion.
    /// </summary>
    public const int MinPasswordLength = 12;

    /// <summary>Intentos fallidos antes de bloquear la cuenta.</summary>
    public const int MaxFailedAttempts = 5;

    /// <summary>Cuanto dura el bloqueo.</summary>
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    /// <summary>Cuanto dura la sesion sin actividad.</summary>
    public static readonly TimeSpan SessionDuration = TimeSpan.FromDays(14);

    public static void AddPortafolioAuthentication(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = MinPasswordLength;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;

                // El correo es opcional: no hay servidor de correo y no hace falta para entrar.
                options.User.RequireUniqueEmail = false;

                // Y por lo mismo, no se exige confirmar la cuenta.
                options.SignIn.RequireConfirmedAccount = false;

                options.Lockout.MaxFailedAccessAttempts = MaxFailedAttempts;
                options.Lockout.DefaultLockoutTimeSpan = LockoutDuration;
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<PortafolioDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        builder.Services
            .AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();

        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "portafolio.sesion";

            // Que el JavaScript de la pagina NO pueda leer la cookie: es la defensa principal contra el
            // robo de sesion por un script inyectado.
            options.Cookie.HttpOnly = true;

            // Lax y no Strict: con Strict, llegar a la aplicacion desde un enlace de otra pagina no
            // mandaria la cookie y pareceria que la sesion se ha perdido.
            options.Cookie.SameSite = SameSiteMode.Lax;

            // SameAsRequest, y no Always, porque en desarrollo local se entra por HTTP: forzar Secure
            // ahi haria que el navegador descartara la cookie y no se podria ni entrar.
            // CONSECUENCIA: el despliegue TIENE que ir por HTTPS, y ahi la cookie sale Secure sola.
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

            options.ExpireTimeSpan = SessionDuration;
            options.SlidingExpiration = true;

            // Una API no redirige a una pagina de entrada: devuelve 401. Sin esto, una peticion sin
            // sesion recibiria un 302 hacia una pagina que no existe.
            options.Events.OnRedirectToLogin = contexto =>
            {
                contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };

            options.Events.OnRedirectToAccessDenied = contexto =>
            {
                contexto.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        builder.Services.AddAuthorization();

        // Se comprueba el testigo de seguridad en CADA peticion, y no cada media hora, que es lo que
        // viene por defecto.
        //
        // Cuesta una consulta por peticion, y merece la pena: es lo que hace que restablecer una
        // contrasena eche de verdad a quien estuviera dentro. Con el valor por defecto, una sesion
        // abierta seguiria funcionando hasta media hora DESPUES de haber cambiado la contrasena, que es
        // justo el momento en el que uno cree haber arreglado el problema.
        builder.Services.Configure<SecurityStampValidatorOptions>(opciones =>
        {
            opciones.ValidationInterval = TimeSpan.Zero;
        });
    }
}
