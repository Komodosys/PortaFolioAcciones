using PortaFolioAcciones.Application.Abstractions;

namespace PortaFolioAcciones.Api.Endpoints;

/// <summary>
/// Endpoints de carteras.
///
/// Existen ya porque la <b>prueba del aislamiento</b> necesita algo que leer: sin un endpoint que pida
/// una cartera concreta, no hay forma de comprobar que quien no es su dueno recibe un 404.
/// </summary>
internal static class PortfolioEndpoints
{
    public static void MapPortfolioEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/portfolios")
            .RequireAuthorization()
            .WithTags("Portfolios");

        group.MapGet("/", async (
            bool? includeArchived,
            IPortfolioRepository repositorio,
            CancellationToken cancelacion) =>
        {
            var carteras = await repositorio.ListAsync(includeArchived ?? false, cancelacion);
            return Results.Ok(carteras);
        })
        .WithName("portfolios.list")
        .WithSummary("Lista las carteras que puede ver quien llama.");

        group.MapGet("/{id:guid}", async (
            Guid id,
            IPortfolioRepository repositorio,
            CancellationToken cancelacion) =>
        {
            var cartera = await repositorio.FindAsync(id, cancelacion);

            // 404 y NO 403. Un 403 diria «existe, pero no puedes verla», que ya es informacion sobre la
            // cartera de otra persona. El 404 no distingue «no existe» de «no es tuya», y por eso es el
            // unico codigo honesto aqui.
            return cartera is null
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "portfolio.not_found")
                : Results.Ok(cartera);
        })
        .WithName("portfolios.get")
        .WithSummary("Devuelve una cartera.");

        // La vista con los totales y el rendimiento, calculada EN EL SERVIDOR.
        //
        // Se calcula aqui y no en la pantalla a proposito: los numeros tienen que salir iguales en el
        // movil, en el ordenador y en un informe, y eso solo se consigue si hay un unico sitio que sume.
        group.MapGet("/{id:guid}/view", async (
            Guid id,
            IPortfolioViewReader lector,
            CancellationToken cancelacion) =>
        {
            var vista = await lector.GetAsync(id, cancelacion);

            return vista is null
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "portfolio.not_found")
                : Results.Ok(vista);
        })
        .WithName("portfolios.view")
        .WithSummary("La vista de la cartera con los totales y el rendimiento, calculados en el servidor.");

        group.MapPost("/", async (
            CreatePortfolioRequest peticion,
            IPortfolioRepository repositorio,
            CancellationToken cancelacion) =>
        {
            if (string.IsNullOrWhiteSpace(peticion.Name))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest, title: "portfolio.name_required");
            }

            try
            {
                var cartera = await repositorio.CreateAsync(peticion.Name, peticion.BaseCurrency, cancelacion);
                return Results.Created($"/api/portfolios/{cartera.Id}", cartera);
            }
            catch (ArgumentException)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest, title: "portfolio.invalid_currency");
            }
        })
        .WithName("portfolios.create")
        .WithSummary("Crea una cartera. El dueno es quien llama, siempre.");

        group.MapPatch("/{id:guid}", async (
            Guid id,
            RenamePortfolioRequest peticion,
            IPortfolioRepository repositorio,
            CancellationToken cancelacion) =>
        {
            if (string.IsNullOrWhiteSpace(peticion.Name))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest, title: "portfolio.name_required");
            }

            var cartera = await repositorio.RenameAsync(id, peticion.Name, cancelacion);

            return cartera is null
                ? NoEncontrada()
                : Results.Ok(cartera);
        })
        .WithName("portfolios.rename")
        .WithSummary("Cambia el nombre de una cartera.");

        // Archivar, y no borrar. Una cartera tiene posiciones e historico, y borrarla se llevaria por
        // delante justo lo que hace falta para entender que paso.
        group.MapPost("/{id:guid}/archive", async (
            Guid id,
            IPortfolioRepository repositorio,
            CancellationToken cancelacion) =>
        {
            var cartera = await repositorio.ArchiveAsync(id, cancelacion);
            return cartera is null ? NoEncontrada() : Results.Ok(cartera);
        })
        .WithName("portfolios.archive")
        .WithSummary("Archiva una cartera. No se borra: se esconde del listado normal.");

        group.MapPost("/{id:guid}/unarchive", async (
            Guid id,
            IPortfolioRepository repositorio,
            CancellationToken cancelacion) =>
        {
            var cartera = await repositorio.UnarchiveAsync(id, cancelacion);
            return cartera is null ? NoEncontrada() : Results.Ok(cartera);
        })
        .WithName("portfolios.unarchive")
        .WithSummary("Vuelve a activar una cartera archivada.");

        // La moneda base. Mandarla nula es volver a heredar la del perfil: poder deshacer la eleccion
        // importa, o una decision de una vez se quedaria fija para siempre.
        group.MapPut("/{id:guid}/base-currency", async (
            Guid id,
            BaseCurrencyRequest peticion,
            IPortfolioRepository repositorio,
            CancellationToken cancelacion) =>
        {
            try
            {
                var cartera = await repositorio.SetBaseCurrencyAsync(id, peticion.Currency, cancelacion);

                return cartera is null
                    ? NoEncontrada()
                    : Results.Ok(cartera);
            }
            catch (ArgumentException)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest, title: "portfolio.invalid_currency");
            }
        })
        .WithName("portfolios.baseCurrency")
        .WithSummary("Fija la moneda base de la cartera. Nula para volver a heredar la del perfil.");

        group.MapPut("/{id:guid}/order", async (
            Guid id,
            OrderRequest peticion,
            IPortfolioRepository repositorio,
            CancellationToken cancelacion) =>
        {
            var cartera = await repositorio.ReorderAsync(id, peticion.SortOrder, cancelacion);
            return cartera is null ? NoEncontrada() : Results.Ok(cartera);
        })
        .WithName("portfolios.order")
        .WithSummary("Cambia el orden en el que se quiere ver la cartera.");
    }

    private static IResult NoEncontrada() =>
        Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "portfolio.not_found");

    /// <summary>Lo que manda el formulario de nueva cartera.</summary>
    internal sealed record CreatePortfolioRequest(string Name, string? BaseCurrency);

    /// <summary>Lo que manda el formulario de cambio de nombre.</summary>
    internal sealed record RenamePortfolioRequest(string Name);

    /// <summary>Lo que manda el cambio de moneda base.</summary>
    internal sealed record BaseCurrencyRequest(string? Currency);

    /// <summary>Lo que manda el cambio de orden.</summary>
    internal sealed record OrderRequest(int SortOrder);
}
