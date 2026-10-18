using PortaFolioAcciones.Application.Abstractions;

namespace PortaFolioAcciones.Api.Endpoints;

/// <summary>
/// Los informes.
///
/// <b>No hace falta ser administrador para leer el propio</b>, y no es un descuido: el filtro global de las
/// carteras limita lo que se ve, así que quien no administra recibe solo sus filas y el administrador recibe
/// todas. Pedir «el informe de otra persona» no es un parámetro: es algo que se obtiene por poder verlas.
/// </summary>
internal static class ReportEndpoints
{
    public static void MapReportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/reports")
            .RequireAuthorization()
            .WithTags("Informes");

        // El nombre del atajo es honesto: «coste contra valor» y no «aportado contra ganado». La aplicación no
        // tiene cuenta de efectivo, así que el valor son las posiciones y nada más; llamarlo «aportado contra
        // ganado» haría creer que incluye el dinero sin invertir, y no lo incluye.
        group.MapGet("/performance", async (
            IPerformanceReader lector,
            CancellationToken cancelacion) =>
        {
            return Results.Ok(await lector.ReadAsync(cancelacion));
        })
        .WithName("reports.performance")
        .WithSummary("Coste contra valor, por cartera: lo que vale, lo que costó, lo aportado y lo vendido.");
    }
}
