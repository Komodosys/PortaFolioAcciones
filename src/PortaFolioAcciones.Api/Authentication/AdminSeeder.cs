using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PortaFolioAcciones.Application.Users;
using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.ValueObjects;
using PortaFolioAcciones.Infrastructure.Identity;
using PortaFolioAcciones.Infrastructure.Persistence;

namespace PortaFolioAcciones.Api.Authentication;

/// <summary>
/// Crea la primera cuenta de administrador si no existe ninguna.
///
/// Hace falta porque <b>no hay registro publico</b>: las cuentas las crea el administrador, asi que la
/// primera tiene que salir de algun sitio. Se crea sola al arrancar y solo la primera vez.
///
/// La contrasena se toma de la configuracion (variable de entorno <c>Admin__InitialPassword</c>). Si no
/// esta puesta, se <b>genera una</b> y se escribe una sola vez en el registro, para no obligar a
/// guardar una contrasena en un fichero de configuracion.
/// </summary>
internal static class AdminSeeder
{
    private const string DefaultUserName = "admin";

    /// <summary>Genera una contrasena larga y aleatoria, compartida con el resto de la aplicacion.</summary>

    public static async Task EnsureFirstAdminAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PortafolioDbContext>();

        if (await context.UserProfiles.AnyAsync(perfil => perfil.IsAdmin))
        {
            return;
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Arranque");

        // El papel tiene que existir antes de asignarlo. Se crea aqui y no en una migracion porque es
        // un dato, no estructura: la tabla de papeles la crea la migracion, su contenido no.
        if (!await roleManager.RoleExistsAsync(AccountRoles.Administrator))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid> { Name = AccountRoles.Administrator });
        }

        var userName = app.Configuration["Admin:UserName"] ?? DefaultUserName;

        // Si no hay contrasena puesta en la configuracion, se genera una y se enseña una sola vez.
        var configuredPassword = app.Configuration["Admin:InitialPassword"];
        var generated = string.IsNullOrWhiteSpace(configuredPassword);
        var password = generated ? GeneratePassword() : configuredPassword!;

        var now = DateTimeOffset.UtcNow;

        // El perfil lo crea el dominio y genera el identificador; la credencial usa ESE MISMO
        // identificador. Asi no hay dos identidades que mantener en sincronia.
        var profile = User.Create(
            app.Configuration["Admin:DisplayName"] ?? "Administrador",
            Currency.Cad,
            AppLanguage.Spanish,
            now);

        profile.GrantAdmin(now);

        // Las dos escrituras van en la misma transaccion: si falla la credencial, no queda un perfil
        // huerfano sin poder entrar.
        await using var transaction = await context.Database.BeginTransactionAsync();

        context.UserProfiles.Add(profile);
        await context.SaveChangesAsync();

        var credential = new ApplicationUser
        {
            Id = profile.Id,
            UserName = userName,
        };

        var result = await userManager.CreateAsync(credential, password);

        if (!result.Succeeded)
        {
            await transaction.RollbackAsync();

            var reasons = string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
            throw new InvalidOperationException(
                $"No se pudo crear la cuenta de administrador '{userName}': {reasons}");
        }

        await transaction.CommitAsync();

        // El papel se asigna DESPUES de confirmar la transaccion, y se comprueba: es lo que hace que
        // «es administrador» viaje dentro del testigo firmado y no haya que consultar la base en cada
        // peticion.
        await userManager.AddToRoleAsync(credential, AccountRoles.Administrator);

        logger.LogWarning(
            "Creada la cuenta de administrador '{Usuario}'. {Aviso}",
            userName,
            generated
                ? $"CONTRASENA GENERADA: {password}  ->  apuntala y cambiala al entrar. No se volvera a mostrar."
                : "Contrasena tomada de la configuracion.");
    }

    /// <summary>
    /// Genera una contrasena larga y aleatoria. Se usan letras y numeros, sin simbolos, porque hay que
    /// poder copiarla o dictarla a mano sin equivocarse.
    /// </summary>
    private static string GeneratePassword() => PasswordGenerator.Generate();
}
