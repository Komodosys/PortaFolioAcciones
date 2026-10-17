using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Infrastructure.Identity;
using PortaFolioAcciones.Infrastructure.Persistence;

namespace PortaFolioAcciones.Api.Authentication;

/// <summary>
/// Entrar, salir y saber quien soy.
///
/// <b>La API nunca devuelve texto para el usuario: devuelve codigos.</b> Es el
/// [ADR-013](../../../DOCUMENTACION/decisiones/ADR-013-interfaz-multi-idioma.md): el texto lo pone el
/// frontend, que es el unico que sabe en que idioma esta la persona. Por eso un fallo de credenciales
/// devuelve <c>auth.invalid_credentials</c> y no «usuario o contraseña incorrectos».
/// </summary>
internal static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Authentication");

        // Devuelve el testigo CSRF y siembra su cookie. El frontend lo pide al arrancar y lo manda en
        // la cabecera en cada peticion que cambie algo.
        group.MapGet("/csrf", (IAntiforgery antiforgery, HttpContext contexto) =>
        {
            var testigo = antiforgery.GetAndStoreTokens(contexto);

            return Results.Ok(new { token = testigo.RequestToken });
        })
        .WithName("auth.csrf")
        .WithSummary("Pide el testigo CSRF, que la interfaz manda en la cabecera al cambiar datos.");

        group.MapPost("/login", async (
            LoginRequest request,
            SignInManager<ApplicationUser> signIn,
            UserManager<ApplicationUser> userManager,
            IAntiforgery antiforgery,
            HttpContext contexto) =>
        {
            if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "auth.missing_fields");
            }

            var cuenta = await userManager.FindByNameAsync(request.UserName);

            // Una contrasena temporal CADUCADA no vale, aunque sea la correcta. Sin esto, una temporal que
            // se entrego en mano y nadie cambio seguiria sirviendo meses despues, y la caducidad no
            // serviria de nada.
            if (cuenta?.MustChangePassword == true
                && cuenta.TemporaryPasswordExpiresAtUtc is { } caduca
                && caduca <= DateTimeOffset.UtcNow)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "auth.temporary_password_expired");
            }

            // lockoutOnFailure: cada intento fallido cuenta. Al llegar al limite la cuenta se bloquea
            // un rato, que es lo que frena que alguien pruebe contrasenas a mano.
            var result = await signIn.PasswordSignInAsync(
                request.UserName,
                request.Password,
                isPersistent: true,
                lockoutOnFailure: true);

            if (result.Succeeded)
            {
                // El testigo CSRF esta ligado a QUIEN lo pide: el que se pidio estando anonimo deja de
                // valer en cuanto se entra. Si no se devolviera uno nuevo aqui, la primera peticion que
                // cambiara algo despues de entrar fallaria con «csrf.invalid», que es un error
                // desconcertante porque la sesion si funciona.
                //
                // Se devuelve en la propia respuesta para que el cliente no tenga que acordarse de
                // pedirlo otra vez.
                var testigo = antiforgery.GetAndStoreTokens(contexto);

                return Results.Ok(new
                {
                    status = "ok",
                    csrfToken = testigo.RequestToken,

                    // Si la contrasena es temporal, el frontend tiene que llevar a cambiarla antes de
                    // dejar hacer nada mas. Si no se dijera, la persona entraria y no sabria por que se
                    // le pide algo a cada paso.
                    mustChangePassword = cuenta?.MustChangePassword ?? false,
                });
            }

            if (result.IsLockedOut)
            {
                // 423 Bloqueado. Se dice, porque quien lo intenta con su propia cuenta merece saber que
                // no es que se haya equivocado, sino que esta bloqueada.
                return Results.Problem(statusCode: StatusCodes.Status423Locked, title: "auth.locked_out");
            }

            // El mismo codigo tanto si el usuario no existe como si la contrasena esta mal: decir cual
            // de las dos cosas falla sirve para adivinar usuarios.
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "auth.invalid_credentials");
        })
        .WithName("auth.login")
        .WithSummary("Entra con usuario y contrasena, y devuelve el testigo CSRF renovado para la sesion.");

        group.MapPost("/logout", async (SignInManager<ApplicationUser> signIn) =>
        {
            await signIn.SignOutAsync();
            return Results.Ok(new { status = "ok" });
        })
        .WithName("auth.logout")
        .WithSummary("Cierra la sesion actual.");

        group.MapPost("/change-password", async (
            ChangePasswordRequest peticion,
            ClaimsPrincipal principal,
            IPasswordManager contrasenas,
            SignInManager<ApplicationUser> signIn,
            CancellationToken cancelacion) =>
        {
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            {
                return Results.Unauthorized();
            }

            var resultado = await contrasenas.ChangeAsync(id, peticion.CurrentPassword, peticion.NewPassword, cancelacion);

            if (!resultado.Succeeded)
            {
                // `password.mismatch` y los codigos de la politica son cosas distintas y el frontend
                // tiene que poder distinguirlas: una dice «te equivocaste al escribir la actual» y la
                // otra «la nueva no cumple los requisitos».
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: resultado.Errors.FirstOrDefault() ?? "password.failed",
                    extensions: new Dictionary<string, object?> { ["errors"] = resultado.Errors });
            }

            // Al cambiar la contrasena se invalida el testigo de seguridad, asi que la sesion que se uso
            // para cambiarla deja de valer. Se vuelve a entrar con la nueva para no dejar a la persona
            // fuera justo despues de que haya hecho lo correcto.
            await signIn.SignOutAsync();

            return Results.Ok(new { status = "ok", message = "auth.password_changed" });
        })
        .WithName("auth.changePassword")
        .WithSummary("Cambia la contrasena propia, comprobando antes la actual.")
        .RequireAuthorization();

        // El perfil se lee por el mismo sitio por el que se escribe, y no consultando la tabla aqui. Antes
        // se consultaba en esta linea; con la pantalla de preferencias habria quedado **la misma lectura
        // escrita dos veces**, y el dia que el perfil gane un dato habria que acordarse de los dos sitios.
        group.MapGet("/me", async (IProfileRepository perfiles, CancellationToken cancelacion) =>
        {
            var perfil = await perfiles.GetAsync(cancelacion);

            return perfil is null
                // Hay credencial pero no perfil: es un estado roto, no una sesion valida.
                ? Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "auth.profile_missing")
                : Results.Ok(new
                {
                    id = perfil.Id,
                    displayName = perfil.DisplayName,
                    baseCurrency = perfil.BaseCurrency,
                    language = perfil.Language,
                    isAdmin = perfil.IsAdmin,
                });
        })
        .WithName("auth.me")
        .WithSummary("Devuelve el perfil de quien tiene la sesion abierta.")
        .RequireAuthorization();
    }

    /// <summary>Lo que manda el formulario de entrada.</summary>
    internal sealed record LoginRequest(string UserName, string Password);

    /// <summary>Lo que manda el formulario de cambio de contrasena.</summary>
    internal sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
}
