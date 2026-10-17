using PortaFolioAcciones.Application.Abstractions;

namespace PortaFolioAcciones.Api.Endpoints;

/// <summary>
/// Las preferencias de quien tiene la sesión abierta.
///
/// <b>Ninguna dirección lleva el identificador de la persona.</b> Quién es se sabe por la sesión, en el
/// servidor. Si se pudiera pasar, cualquiera podría cambiarse las preferencias —o los permisos— de otra
/// persona cambiando un número.
/// </summary>
internal static class ProfileEndpoints
{
    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/profile")
            .WithTags("Perfil")
            .RequireAuthorization();

        grupo.MapGet("/", async (IProfileRepository perfiles, CancellationToken cancelacion) =>
        {
            var perfil = await perfiles.GetAsync(cancelacion);

            return perfil is null
                ? Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "auth.profile_missing")
                : Results.Ok(perfil);
        })
        .WithName("profile.get")
        .WithSummary("Devuelve el perfil de quien tiene la sesión abierta.");

        // Los dos códigos van juntos y no en dos direcciones: son la misma pantalla y se guardan a la vez.
        // Con dos, un fallo en la segunda dejaría las preferencias a medias.
        grupo.MapPut("/preferences", async (
            PreferencesRequest peticion,
            IProfileRepository perfiles,
            CancellationToken cancelacion) =>
        {
            try
            {
                var perfil = await perfiles.UpdatePreferencesAsync(
                    peticion.BaseCurrency,
                    peticion.Language,
                    cancelacion);

                return perfil is null
                    ? Results.Problem(
                        statusCode: StatusCodes.Status500InternalServerError,
                        title: "auth.profile_missing")
                    : Results.Ok(perfil);
            }
            catch (ArgumentException)
            {
                // Un código mal escrito no es «no existe»: es un error de quien llama, y decirle 404 le
                // haría creer que su perfil se ha perdido.
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "profile.invalid_preferences");
            }
        })
        .WithName("profile.updatePreferences")
        .WithSummary("Cambia la moneda base y el idioma de quien tiene la sesión abierta.");

        return app;
    }

    /// <summary>Lo que manda la pantalla de preferencias.</summary>
    internal sealed record PreferencesRequest(string BaseCurrency, string Language);
}
