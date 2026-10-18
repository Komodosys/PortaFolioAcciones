using System.Globalization;
using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Application.Users;
using PortaFolioAcciones.Infrastructure.MarketData;

namespace PortaFolioAcciones.Api.Endpoints;

/// <summary>
/// Los refrescos de precios pedidos a mano, desde la pantalla.
///
/// <b>Solo el administrador.</b> No es desconfianza de la familia: las dos operaciones llaman a una fuente
/// externa gratuita y escriben precios, y en una aplicación que se deja abierta en una pestaña, un botón
/// que cualquiera puede pulsar en bucle acaba en un bloqueo de la fuente que deja sin precios a todos.
///
/// Hay dos, y la diferencia entre ellas es lo que las hace seguras:
///
/// <list type="bullet">
///   <item><description>
///     <b>Refrescar cotizaciones</b>: actualiza «lo último que se sabe» y nada más. Se puede pulsar
///     cualquier día, a cualquier hora.
///   </description></item>
///   <item><description>
///     <b>Recoger el cierre del día</b>: escribe el cierre, los tipos de cambio y la foto de las carteras.
///     <b>Antes del cierre de las bolsas, ese «cierre» es en realidad el precio del momento.</b> Se corrige
///     solo cuando la recogida diaria vuelve a pasar después del cierre, pero mientras tanto el histórico
///     dice algo que no es. Por eso la pantalla avisa antes de lanzarla.
///   </description></item>
/// </list>
/// </summary>
internal static class MarketEndpoints
{
    public static void MapMarketEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/market")
            .RequireAuthorization(politica => politica.RequireRole(AccountRoles.Administrator))
            .WithTags("Mercado");

        group.MapPost("/refresh-quotes", async Task<IResult> (
            RefrescoDePrecios refrescos,
            IMarketDataCollector colector,
            HttpContext contexto,
            CancellationToken cancelacion) =>
        {
            if (refrescos.EsperarPara() is { } espera)
            {
                return DemasiadoPronto(contexto, espera);
            }

            // Se tira la caché ANTES: si no, la fuente no se consultaría y el botón devolvería lo mismo que
            // ya había, que es justo lo que la persona está intentando evitar.
            refrescos.InvalidarCache();

            return Results.Ok(await colector.RefreshQuotesAsync(cancelacion));
        })
        .WithName("market.refreshQuotes")
        .WithSummary("Vuelve a pedir la cotización de cada instrumento. No escribe el cierre del día.");

        group.MapPost("/collect", async Task<IResult> (
            RefrescoDePrecios refrescos,
            IMarketDataCollector colector,
            TimeProvider reloj,
            HttpContext contexto,
            CancellationToken cancelacion) =>
        {
            if (refrescos.EsperarPara() is { } espera)
            {
                return DemasiadoPronto(contexto, espera);
            }

            refrescos.InvalidarCache();

            var dia = DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime);

            return Results.Ok(await colector.CollectAsync(dia, cancelacion));
        })
        .WithName("market.collect")
        .WithSummary("Ejecuta la recogida diaria ahora: cierre, tipos de cambio y foto de las carteras.");

        // El relleno del histórico. Es una operación de una vez y pesada: una petición por instrumento que
        // devuelve años de cierres. Por eso lleva el mismo freno que las demás.
        group.MapPost("/backfill", async Task<IResult> (
            RefrescoDePrecios refrescos,
            IMarketDataCollector colector,
            TimeProvider reloj,
            HttpContext contexto,
            CancellationToken cancelacion) =>
        {
            if (refrescos.EsperarPara() is { } espera)
            {
                return DemasiadoPronto(contexto, espera);
            }

            // Cinco años: cubre cualquier informe razonable y no multiplica las filas por gusto. Si algún día
            // hace falta más atrás, es un parámetro y no un rediseño.
            var desde = DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime).AddYears(-5);

            return Results.Ok(await colector.BackfillAsync(desde, cancelacion));
        })
        .WithName("market.backfill")
        .WithSummary("Rellena el histórico de precios hacia atrás. Una petición por instrumento, sin sobrescribir.");

        // El de divisa es otro relleno y va aparte: son fuentes distintas y se pueden querer por separado. Sin
        // él, el efectivo de una compra vieja se convertiría con el tipo de hoy, y el saldo de una cuenta no
        // cambia cuando se mueve el dólar.
        group.MapPost("/backfill-fx", async Task<IResult> (
            RefrescoDePrecios refrescos,
            IMarketDataCollector colector,
            TimeProvider reloj,
            HttpContext contexto,
            CancellationToken cancelacion) =>
        {
            if (refrescos.EsperarPara() is { } espera)
            {
                return DemasiadoPronto(contexto, espera);
            }

            // Los mismos cinco años que los precios: no tiene sentido tener cierres de 2021 y tipos desde 2025.
            var desde = DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime).AddYears(-5);

            return Results.Ok(await colector.BackfillFxAsync(desde, cancelacion));
        })
        .WithName("market.backfillFx")
        .WithSummary("Rellena el histórico de tipos de cambio, que es lo que permite convertir el efectivo de una compra antigua.");
    }

    /// <summary>
    /// 429 con <c>Retry-After</c>, que es lo que dice un servidor cuando la petición es correcta pero llega
    /// demasiado seguida — y que un cliente entiende sin leer el cuerpo.
    ///
    /// La cabecera se pone en la respuesta en vez de pasarla a <c>Results.Problem</c> porque ese método no
    /// admite cabeceras, y la fecha de reintento es parte del contrato de un 429.
    /// </summary>
    private static IResult DemasiadoPronto(HttpContext contexto, TimeSpan espera)
    {
        var segundos = (int)Math.Ceiling(espera.TotalSeconds);

        contexto.Response.Headers.RetryAfter = segundos.ToString(CultureInfo.InvariantCulture);

        return Results.Problem(
            statusCode: StatusCodes.Status429TooManyRequests,
            title: "mercado.demasiadoPronto",
            extensions: new Dictionary<string, object?> { ["retryAfterSeconds"] = segundos });
    }
}
