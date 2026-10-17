using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Application.Positions;

namespace PortaFolioAcciones.Api.Endpoints;

/// <summary>
/// Las posiciones y las operaciones que se hacen sobre ellas.
///
/// Las rutas van <b>colgando de la cartera o de la posición</b>, nunca de la persona: el dueño sale del
/// contexto autenticado. Si la dirección llevara el identificador de la persona, bastaría con cambiarlo
/// para operar en la cartera de otro.
/// </summary>
internal static class PositionEndpoints
{
    public static void MapPositionEndpoints(this WebApplication app)
    {
        var porCartera = app.MapGroup("/api/portfolios/{portfolioId:guid}/positions")
            .RequireAuthorization()
            .WithTags("Positions");

        porCartera.MapGet("/", async (
            Guid portfolioId,
            bool? includeClosed,
            IPositionRepository repositorio,
            CancellationToken cancelacion) =>
        {
            var posiciones = await repositorio.ListAsync(portfolioId, includeClosed ?? false, cancelacion);

            // Una cartera ajena devuelve lista vacia y no 403: quien pregunta no tiene por que saber si
            // la cartera existe.
            return Results.Ok(posiciones);
        })
        .WithName("positions.list")
        .WithSummary("Lista las posiciones de una cartera.");

        porCartera.MapPost("/", async (
            Guid portfolioId,
            AddPositionRequest peticion,
            IPositionRepository repositorio,
            CancellationToken cancelacion) =>
        {
            var comando = new AddPositionCommand(
                peticion.InstrumentId,
                [.. (peticion.Lots ?? []).Select(ALote)],
                peticion.Notes);

            var resultado = await repositorio.AddAsync(portfolioId, comando, cancelacion);

            return resultado.Succeeded
                ? Results.Created($"/api/positions/{resultado.Position!.Id}", resultado.Position)
                : Problem(resultado);
        })
        .WithName("positions.create")
        .WithSummary("Da de alta una posicion en una cartera, con sus primeras compras.");

        var porPosicion = app.MapGroup("/api/positions")
            .RequireAuthorization()
            .WithTags("Positions");

        porPosicion.MapGet("/{id:guid}", async (
            Guid id,
            IPositionRepository repositorio,
            CancellationToken cancelacion) =>
        {
            var posicion = await repositorio.FindAsync(id, cancelacion);

            return posicion is null ? NoEncontrada() : Results.Ok(posicion);
        })
        .WithName("positions.get")
        .WithSummary("Devuelve una posicion con sus lotes.");

        // La cuenta a la que pertenece. Va aparte del detalle porque es contexto de la CUENTA, no de la
        // posicion: la pantalla lo necesita para poder decir EN QUE CUENTA Y DE QUIEN se esta trabajando,
        // que es un requisito de ADR-014 para el administrador.
        porPosicion.MapGet("/{id:guid}/account", async (
            Guid id,
            IPositionRepository repositorio,
            CancellationToken cancelacion) =>
        {
            var cuenta = await repositorio.AccountOfAsync(id, cancelacion);

            return cuenta is null ? NoEncontrada() : Results.Ok(cuenta);
        })
        .WithName("positions.account")
        .WithSummary("La cuenta a la que pertenece la posicion: como se llama y de quien es.");

        porPosicion.MapPost("/{id:guid}/lots", async (
            Guid id,
            LotRequest peticion,
            IPositionRepository repositorio,
            CancellationToken cancelacion) =>
        {
            var resultado = await repositorio.AddLotAsync(id, ALote(peticion), cancelacion);

            return resultado.Succeeded ? Results.Ok(resultado.Position) : Problem(resultado);
        })
        .WithName("positions.addLot")
        .WithSummary("Anade una compra a una posicion y recalcula el coste medio.");

        porPosicion.MapPost("/{id:guid}/sell", async (
            Guid id,
            SellRequest peticion,
            IPositionRepository repositorio,
            CancellationToken cancelacion) =>
        {
            var resultado = await repositorio.SellAsync(id, new SellCommand(peticion.Quantity), cancelacion);

            return resultado.Succeeded ? Results.Ok(resultado.Position) : Problem(resultado);
        })
        .WithName("positions.sell")
        .WithSummary("Vende una cantidad, repartida proporcionalmente entre los lotes para conservar el coste medio.");

        porPosicion.MapPut("/{id:guid}/manual-price", async (
            Guid id,
            ManualPriceRequest peticion,
            IPositionRepository repositorio,
            CancellationToken cancelacion) =>
        {
            var comando = new ManualPriceCommand(peticion.Amount, peticion.Currency, peticion.Date);
            var resultado = await repositorio.SetManualPriceAsync(id, comando, cancelacion);

            return resultado.Succeeded ? Results.Ok(resultado.Position) : Problem(resultado);
        })
        .WithName("positions.manualPrice")
        .WithSummary("Pone un precio a mano, con su fecha y su moneda, que manda sobre cualquier fuente.");

        porPosicion.MapPost("/{id:guid}/split", async (
            Guid id,
            SplitRequest peticion,
            IPositionRepository repositorio,
            CancellationToken cancelacion) =>
        {
            var comando = new SplitCommand(peticion.Ratio, peticion.Date, peticion.Apply ?? true);

            var resultado = await repositorio.ApplySplitAsync(id, comando, cancelacion);

            return resultado.Succeeded ? Results.Ok(resultado.Position) : Problem(resultado);
        })
        .WithName("positions.split")
        .WithSummary(
            "Aplica un desdoblamiento: la cantidad se multiplica y el coste por unidad se divide. " +
            "Con `apply` en falso solo se anota que ese dia ya esta reflejado, sin tocar las cantidades.");

        porPosicion.MapPost("/{id:guid}/close", async (
            Guid id,
            IPositionRepository repositorio,
            CancellationToken cancelacion) =>
        {
            var resultado = await repositorio.CloseAsync(id, cancelacion);

            return resultado.Succeeded ? Results.Ok(resultado.Position) : Problem(resultado);
        })
        .WithName("positions.close")
        .WithSummary("Cierra la posicion, conservando lo que habia costado.");

        porPosicion.MapPost("/{id:guid}/reopen", async (
            Guid id,
            IPositionRepository repositorio,
            CancellationToken cancelacion) =>
        {
            var resultado = await repositorio.ReopenAsync(id, cancelacion);

            return resultado.Succeeded ? Results.Ok(resultado.Position) : Problem(resultado);
        })
        .WithName("positions.reopen")
        .WithSummary("Vuelve a abrir una posicion cerrada.");

        porPosicion.MapPut("/{id:guid}/notes", async (
            Guid id,
            NotesRequest peticion,
            IPositionRepository repositorio,
            CancellationToken cancelacion) =>
        {
            var resultado = await repositorio.SetNotesAsync(id, peticion.Notes, cancelacion);

            return resultado.Succeeded ? Results.Ok(resultado.Position) : Problem(resultado);
        })
        .WithName("positions.notes")
        .WithSummary("Cambia las notas de una posicion.");
    }

    private static LotInput ALote(LotRequest lote) =>
        new(lote.Quantity, lote.CostPerUnit, lote.PurchaseDate, lote.DateAccuracy, lote.Notes);

    private static IResult NoEncontrada() =>
        Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "position.not_found");

    private static IResult Problem(PositionResult resultado)
    {
        var codigo = resultado.Errors.FirstOrDefault() ?? "position.failed";

        // El 404 se distingue del 400 porque son cosas distintas: una dice «no es tuya o no existe» y la
        // otra «los datos estan mal», y solo una se arregla corrigiendo lo que se mando.
        var estado = codigo switch
        {
            "position.not_found" => StatusCodes.Status404NotFound,
            "position.not_allowed" => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest,
        };

        return Results.Problem(
            statusCode: estado,
            title: codigo,
            extensions: new Dictionary<string, object?> { ["errors"] = resultado.Errors });
    }

    /// <summary>Un lote, tal como llega.</summary>
    internal sealed record LotRequest(
        decimal Quantity,
        decimal CostPerUnit,
        DateOnly PurchaseDate,
        string? DateAccuracy,
        string? Notes);

    /// <summary>Una posición nueva con sus compras.</summary>
    internal sealed record AddPositionRequest(
        Guid InstrumentId,
        IReadOnlyList<LotRequest>? Lots,
        string? Notes);

    /// <summary>Una venta.</summary>
    internal sealed record SellRequest(decimal Quantity);

    /// <summary>Un precio a mano, con su fecha y su moneda.</summary>
    internal sealed record ManualPriceRequest(decimal Amount, string Currency, DateOnly Date);

    /// <summary>Un desdoblamiento.</summary>
    internal sealed record SplitRequest(decimal Ratio, DateOnly? Date, bool? Apply);

    /// <summary>Las notas.</summary>
    internal sealed record NotesRequest(string? Notes);
}
