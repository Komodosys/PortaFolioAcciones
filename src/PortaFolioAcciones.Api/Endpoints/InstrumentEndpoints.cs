using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Application.Instruments;

namespace PortaFolioAcciones.Api.Endpoints;

/// <summary>
/// El catálogo de instrumentos y la búsqueda en la fuente de mercado.
///
/// Hay <b>dos búsquedas distintas</b> y conviene no confundirlas:
/// <list type="bullet">
///   <item><description><c>GET /api/instruments?q=</c> busca <b>en el catálogo</b>, que es lo que ya
///   existe en esta casa. Es instantánea y no depende de nadie.</description></item>
///   <item><description><c>GET /api/instruments/lookup?q=</c> busca <b>fuera</b>, en la fuente de
///   mercado, para dar de alta algo nuevo. Depende de un servicio ajeno y puede tardar o no
///   responder.</description></item>
/// </list>
/// Se separan porque lo que el usuario quiere saber es distinto en cada caso: «¿ya tengo esto?» frente a
/// «¿cómo se llama esto ahí fuera?».
/// </summary>
internal static class InstrumentEndpoints
{
    public static void MapInstrumentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/instruments")
            .RequireAuthorization()
            .WithTags("Instruments");

        group.MapGet("/", async (
            string? q,
            IInstrumentRepository repositorio,
            CancellationToken cancelacion) =>
        {
            var instrumentos = await repositorio.SearchAsync(q, cancelacion);
            return Results.Ok(instrumentos);
        })
        .WithName("instruments.search")
        .WithSummary("Busca en el catalogo de instrumentos de la casa, por simbolo, nombre o codigo de broker.");

        // OJO con el orden: esta ruta tiene que ir ANTES que '/{id:guid}' solo si pudieran coincidir.
        // No coinciden, porque 'lookup' no es un Guid, pero se deja arriba para que se lea primero.
        group.MapGet("/lookup", async (
            string? q,
            IInstrumentSearch busqueda,
            CancellationToken cancelacion) =>
        {
            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest, title: "instrument.query_too_short");
            }

            var encontrados = await busqueda.SearchAsync(q, cancelacion);

            // Devolver una lista vacia NO es un error: «no he encontrado nada» es una respuesta legitima
            // a una busqueda, y tratarla como fallo obligaria a la pantalla a distinguir dos casos que
            // para el usuario son el mismo.
            return Results.Ok(encontrados);
        })
        .WithName("instruments.lookup")
        .WithSummary("Busca en la fuente de mercado, para dar de alta un instrumento que no esta en el catalogo.");

        group.MapGet("/{id:guid}", async (
            Guid id,
            IInstrumentRepository repositorio,
            CancellationToken cancelacion) =>
        {
            var instrumento = await repositorio.FindAsync(id, cancelacion);

            return instrumento is null
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "instrument.not_found")
                : Results.Ok(instrumento);
        })
        .WithName("instruments.get")
        .WithSummary("Devuelve un instrumento del catalogo.");

        group.MapPost("/", async (
            InstrumentRequest peticion,
            IInstrumentRepository repositorio,
            CancellationToken cancelacion) =>
        {
            var resultado = await repositorio.AddAsync(ADominio(peticion), cancelacion);

            return resultado.Succeeded
                ? Results.Created($"/api/instruments/{resultado.Instrument!.Id}", resultado.Instrument)
                : Problem(resultado);
        })
        .WithName("instruments.create")
        .WithSummary("Da de alta un instrumento, o devuelve el que ya existia con esa identidad.");

        group.MapPut("/{id:guid}", async (
            Guid id,
            InstrumentRequest peticion,
            IInstrumentRepository repositorio,
            CancellationToken cancelacion) =>
        {
            var resultado = await repositorio.UpdateAsync(id, ADominio(peticion), cancelacion);

            return resultado.Succeeded ? Results.Ok(resultado.Instrument) : Problem(resultado);
        })
        .WithName("instruments.update")
        .WithSummary("Modifica un instrumento del catalogo compartido. Solo el administrador.");
    }

    private static CreateInstrumentCommand ADominio(InstrumentRequest peticion) =>
        new(
            peticion.Symbol,
            peticion.Name,
            peticion.AssetType,
            peticion.QuoteCurrency,
            peticion.Exchange,
            peticion.ValuationMode,
            peticion.PricingFrequency,
            peticion.Isin,
            peticion.Country,
            peticion.ProviderKey,
            peticion.BrokerCode);

    private static IResult Problem(InstrumentResult resultado)
    {
        var codigo = resultado.Errors.FirstOrDefault() ?? "instrument.failed";

        // No se pudo por permisos: 403 y no 400. La diferencia importa: una dice «no puedes» y la otra
        // «está mal», y solo una de las dos se arregla corrigiendo los datos.
        var estado = codigo == "instrument.not_allowed"
            ? StatusCodes.Status403Forbidden
            : StatusCodes.Status400BadRequest;

        return Results.Problem(
            statusCode: estado,
            title: codigo,
            extensions: new Dictionary<string, object?> { ["errors"] = resultado.Errors });
    }

    /// <summary>Lo que manda el formulario de instrumento.</summary>
    internal sealed record InstrumentRequest(
        string Symbol,
        string Name,
        string AssetType,
        string QuoteCurrency,
        string Exchange,
        string ValuationMode,
        string PricingFrequency,
        string? Isin,
        string? Country,
        string? ProviderKey,
        string? BrokerCode);
}
