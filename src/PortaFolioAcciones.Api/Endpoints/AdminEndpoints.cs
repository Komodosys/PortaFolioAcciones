using PortaFolioAcciones.Api.Authentication;
using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Application.Audit;
using PortaFolioAcciones.Application.Users;

namespace PortaFolioAcciones.Api.Endpoints;

/// <summary>
/// Administracion. Todo lo de aqui exige el papel de administrador.
///
/// La restriccion se pone en el <b>grupo entero</b> y no endpoint a endpoint: asi no hay forma de
/// añadir uno nuevo y olvidarse de protegerlo.
/// </summary>
internal static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin")
            .RequireAuthorization(politica => politica.RequireRole(AccountRoles.Administrator))
            .WithTags("Administration");

        // Quien tiene acceso a los datos de la casa. Hasta ahora eso solo se podia averiguar entrando en la
        // base de datos, y un administrador tiene que poder responderlo.
        group.MapGet("/users", async (IUserDirectory directorio, CancellationToken cancelacion) =>
        {
            return Results.Ok(await directorio.ListAsync(cancelacion));
        })
        .WithName("admin.users")
        .WithSummary("Las cuentas que hay. Solo el administrador.");

        group.MapPost("/users", async (
            CreateAccountRequest peticion,
            IAccountCreator creador,
            CancellationToken cancelacion) =>
        {
            // Una contrasena que falta NO puede llegar hasta Identity: alli revienta con
            // `ArgumentNullException` y el cliente recibe un 500 con una traza, cuando lo que ha hecho es
            // mandar una peticion incompleta. Se corta aqui, con un 400 que dice que falta.
            if (string.IsNullOrWhiteSpace(peticion.Password))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "account.password.missing");
            }

            var resultado = await creador.CreateAsync(
                new CreateAccountCommand(
                    peticion.UserName,
                    peticion.DisplayName,
                    peticion.Password,
                    peticion.BaseCurrency ?? "CAD",
                    peticion.Language ?? "es",
                    peticion.IsAdmin),
                cancelacion);

            if (!resultado.Succeeded)
            {
                // Los codigos, no los textos: el frontend es quien sabe en que idioma esta la persona.
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: resultado.Errors.FirstOrDefault() ?? "account.failed",
                    extensions: new Dictionary<string, object?> { ["errors"] = resultado.Errors });
            }

            return Results.Created($"/api/admin/users/{resultado.UserId}", new { id = resultado.UserId });
        })
        .WithName("admin.createUser")
        .WithSummary("Crea una cuenta, con su perfil y su credencial. Solo el administrador.");

        group.MapPost("/users/{id:guid}/reset-password", async (
            Guid id,
            IPasswordManager contrasenas,
            CancellationToken cancelacion) =>
        {
            var resultado = await contrasenas.ResetAsync(id, cancelacion);

            if (!resultado.Succeeded)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: resultado.Errors.FirstOrDefault() ?? "password.failed",
                    extensions: new Dictionary<string, object?> { ["errors"] = resultado.Errors });
            }

            // La temporal se devuelve UNA SOLA VEZ y no se guarda en claro en ningun sitio. Quien la
            // reciba tiene que entregarla en mano; si se pierde, se genera otra.
            return Results.Ok(new
            {
                temporaryPassword = resultado.TemporaryPassword,
                expiresAtUtc = resultado.ExpiresAtUtc,
                mustChangePassword = true,
            });
        })
        .WithName("admin.resetPassword")
        .WithSummary("Genera una contrasena temporal para otra persona. Se devuelve una sola vez. Solo el administrador.");

        // El libro de auditoria. Es la contrapartida del poder de modificar datos ajenos (ADR-014), asi que
        // leerlo es de administrador: es quien puede hacer lo que se audita.
        group.MapGet("/audit", async (
            Guid? ownerUserId,
            Guid? actorUserId,
            string? entityType,
            DateTimeOffset? from,
            DateTimeOffset? to,
            bool? onlySomeoneElseData,
            int? limit,
            IAuditLogReader libro,
            CancellationToken cancelacion) =>
        {
            var filtro = new AuditFilter(
                ownerUserId,
                actorUserId,
                entityType,
                from,
                to,
                onlySomeoneElseData ?? false,
                limit ?? 200);

            return Results.Ok(await libro.ReadAsync(filtro, cancelacion));
        })
        .WithName("admin.audit")
        .WithSummary("Lee el libro de auditoria, de lo mas reciente a lo mas antiguo. Solo el administrador.");

        group.MapGet("/audit/people", async (
            IAuditLogReader libro,
            CancellationToken cancelacion) =>
        {
            return Results.Ok(await libro.PeopleAsync(cancelacion));
        })
        .WithName("admin.auditPeople")
        .WithSummary("Las personas que aparecen en el libro, para poder filtrar por ellas. Solo el administrador.");
    }

    /// <summary>Lo que manda el formulario de nueva cuenta.</summary>
    internal sealed record CreateAccountRequest(
        string UserName,
        string DisplayName,
        string Password,
        string? BaseCurrency,
        string? Language,
        bool IsAdmin);
}
