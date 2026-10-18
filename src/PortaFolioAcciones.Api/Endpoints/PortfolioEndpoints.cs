using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Application.Portfolios;
using PortaFolioAcciones.Domain.ValueObjects;

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

            // El tipo es obligatorio, y **un valor desconocido no se convierte en el primero del enumerado**,
            // que es lo que pasaria con un enumerado no anulable en el record: etiquetaria una cuenta de
            // jubilacion como no registrada sin decir nada.
            if (!TryParseRegistrationType(peticion.RegistrationType, out var tipo))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest, title: "portfolio.registration_required");
            }

            try
            {
                var cartera = await repositorio.CreateAsync(
                    peticion.Name,
                    tipo,
                    peticion.BaseCurrency,
                    cancelacion);

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

        // La edición completa, en UNA llamada. Con tres (nombre, tipo, moneda) una podría fallar y las otras
        // no, y además cada una dejaría su propia entrada en el libro de auditoría: editar una cartera
        // aparecería como tres cambios distintos.
        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdatePortfolioRequest peticion,
            IPortfolioRepository repositorio,
            CancellationToken cancelacion) =>
        {
            if (string.IsNullOrWhiteSpace(peticion.Name))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest, title: "portfolio.name_required");
            }

            if (!TryParseRegistrationType(peticion.RegistrationType, out var tipo))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest, title: "portfolio.registration_required");
            }

            try
            {
                var cartera = await repositorio.UpdateAsync(
                    id,
                    peticion.Name,
                    tipo,
                    peticion.BaseCurrency,
                    cancelacion);

                return cartera is null ? NoEncontrada() : Results.Ok(cartera);
            }
            catch (ArgumentException)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest, title: "portfolio.invalid_currency");
            }
        })
        .WithName("portfolios.update")
        .WithSummary("Cambia el nombre, el tipo de registro y la moneda base de una cartera, todo a la vez.");

        // Borrar solo si esta vacia. Con posiciones devuelve 409 y no 400: la peticion es correcta, es el
        // estado de la cartera el que no lo permite — y el 409 es justo lo que significa eso.
        group.MapDelete("/{id:guid}", async (
            Guid id,
            IPortfolioRepository repositorio,
            CancellationToken cancelacion) =>
        {
            var resultado = await repositorio.DeleteAsync(id, cancelacion);

            return resultado switch
            {
                DeleteOutcome.Deleted => Results.NoContent(),
                DeleteOutcome.HasPositions => Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "portfolio.has_positions"),
                _ => NoEncontrada(),
            };
        })
        .WithName("portfolios.delete")
        .WithSummary("Borra una cartera, SOLO si no tiene posiciones. Si las tiene, se archiva.");

        // --- Los movimientos de la cartera ---------------------------------------------------------
        //
        // Solo aportaciones y retiros. Una venta NO se registra por aquí: se registra al vender, porque además
        // reduce la posición, y las dos cosas tienen que ir juntas o el registro y la posición dirían cosas
        // distintas.

        group.MapGet("/{id:guid}/movements", async (
            Guid id,
            IPortfolioRepository carteras,
            IMovementRepository movimientos,
            CancellationToken cancelacion) =>
        {
            // El filtro global actúa sobre las carteras, y un movimiento no tiene dueño propio: hereda el de
            // su cartera. Por eso el acceso se comprueba aquí. Sin esto, cualquiera podría leer los
            // movimientos de la cartera de otra persona.
            var cartera = await carteras.FindAsync(id, cancelacion);

            return cartera is null
                ? NoEncontrada()
                : Results.Ok(await movimientos.ListAsync(id, cancelacion));
        })
        .WithName("portfolios.movements")
        .WithSummary("Los movimientos de una cartera, del más reciente al más antiguo.");

        group.MapPost("/{id:guid}/movements", async Task<IResult> (
            Guid id,
            CashMovementRequest peticion,
            IPortfolioRepository carteras,
            IMovementRepository movimientos,
            TimeProvider reloj,
            CancellationToken cancelacion) =>
        {
            var cartera = await carteras.FindAsync(id, cancelacion);

            if (cartera is null)
            {
                return NoEncontrada();
            }

            if (peticion.Amount <= 0m)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest, title: "movement.invalid_amount");
            }

            try
            {
                var dia = peticion.OccurredOn ?? DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime);

                var movimiento = await movimientos.AddCashAsync(
                    id,
                    peticion.Kind ?? string.Empty,
                    peticion.Amount,
                    peticion.Currency ?? Currency.Cad.Code,
                    dia,
                    peticion.Notes,
                    cancelacion);

                return Results.Created($"/api/portfolios/{id}/movements/{movimiento.Id}", movimiento);
            }
            catch (ArgumentException)
            {
                // Aquí caen la moneda malformada y el tipo que no es aportación ni retiro. Se devuelve 400 y no
                // 500: es la petición la que está mal, no el programa.
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest, title: "movement.invalid");
            }
        })
        .WithName("portfolios.addMovement")
        .WithSummary("Registra una aportación o un retiro. Las ventas se registran al vender.");
    }

    private static IResult NoEncontrada() =>
        Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "portfolio.not_found");

    /// <summary>
    /// Traduce el tipo de registro que llega como texto.
    ///
    /// <c>Enum.IsDefined</c> hace falta además de <c>TryParse</c> porque <c>TryParse</c> acepta números fuera
    /// de rango: «99» se convierte en un valor que no existe y llegaría hasta la base de datos.
    /// </summary>
    private static bool TryParseRegistrationType(string? texto, out RegistrationType tipo) =>
        Enum.TryParse(texto, ignoreCase: true, out tipo) && Enum.IsDefined(tipo);

    /// <summary>Lo que manda el formulario de una aportación o un retiro.</summary>
    /// <param name="Kind">`Deposit` o `Withdrawal`. Cualquier otra cosa da 400.</param>
    /// <param name="Amount">El importe, siempre positivo: el signo lo pone el tipo.</param>
    /// <param name="Currency">La moneda. Sin ella se usa la base de la aplicación.</param>
    /// <param name="OccurredOn">El día. Nulo usa el de hoy.</param>
    /// <param name="Notes">Notas.</param>
    internal sealed record CashMovementRequest(
        string? Kind,
        decimal Amount,
        string? Currency,
        DateOnly? OccurredOn,
        string? Notes);

    /// <summary>Lo que manda el formulario de edición de una cartera.</summary>
    internal sealed record UpdatePortfolioRequest(
        string Name,
        string? RegistrationType,
        string? BaseCurrency);

    /// <summary>Lo que manda el formulario de nueva cartera.</summary>
    internal sealed record CreatePortfolioRequest(
        string Name,
        string? RegistrationType,
        string? BaseCurrency);

    /// <summary>Lo que manda el formulario de cambio de nombre.</summary>
    internal sealed record RenamePortfolioRequest(string Name);

    /// <summary>Lo que manda el cambio de moneda base.</summary>
    internal sealed record BaseCurrencyRequest(string? Currency);

    /// <summary>Lo que manda el cambio de orden.</summary>
    internal sealed record OrderRequest(int SortOrder);
}
