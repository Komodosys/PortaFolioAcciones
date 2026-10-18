using PortaFolioAcciones.Application.MarketData;

namespace PortaFolioAcciones.Application.Abstractions;

/// <summary>
/// Recoge los precios del dia y toma la foto de las carteras.
///
/// Es un puerto porque <b>quien decide cuándo</b> es una cosa y <b>qué hay que hacer</b> es otra: el
/// servicio en segundo plano se ocupa del calendario —esperar a que cierren las bolsas— y esto se ocupa del
/// trabajo. Separados, el trabajo se puede probar sin levantar el servicio ni esperar a ninguna hora.
/// </summary>
public interface IMarketDataCollector
{
    /// <summary>
    /// Recoge el precio de todos los instrumentos que hacen falta y toma la foto de las carteras.
    /// </summary>
    /// <param name="dia">El dia que se esta recogiendo, en fecha de la bolsa.</param>
    /// <param name="cancellationToken">Cancelacion.</param>
    /// <remarks>
    /// <b>Es idempotente</b>: lo que ya esta recogido no se vuelve a pedir. Eso es lo que permite
    /// ejecutarlo sin miedo —al arrancar, tras una caida, o dos veces seguidas— sin gastar cuota ni
    /// duplicar filas. Y es lo que hace que no haga falta guardar en ningun sitio «hasta aqui he llegado»:
    /// el propio dato dice si esta.
    /// </remarks>
    public Task<DailyCollectionResult> CollectAsync(
        DateOnly dia,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Vuelve a pedir la cotización de cada instrumento y actualiza <b>solo</b> «lo último que se sabe».
    ///
    /// <b>No escribe el cierre del día, ni los tipos de cambio, ni la foto de las carteras.</b> Esa es toda
    /// la diferencia con <see cref="CollectAsync"/>, y es lo que la hace segura a cualquier hora: antes del
    /// cierre, la fuente devuelve el precio del momento, y guardarlo como cierre de hoy sería mentir sobre
    /// el histórico. Después del cierre, la recogida diaria vuelve a pasar y escribe el de verdad.
    ///
    /// El llamante es quien decide si antes hay que tirar la caché. Aquí no se decide: esta operación
    /// pregunta a la fuente lo que le digan, y quien quiera saltarse la caché la invalida antes.
    /// </summary>
    /// <param name="cancellationToken">Cancelación.</param>
    public Task<QuoteRefreshResult> RefreshQuotesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rellena el histórico de precios hacia atrás, <b>en una petición por instrumento</b>.
    ///
    /// La recogida diaria guarda un cierre al día, así que el histórico de una instalación nueva empieza el
    /// día que se instaló: los informes de periodo no se podrían calcular hasta pasado un año. La fuente,
    /// en cambio, ya tiene los cierres publicados, y pedirlos de una vez los trae todos.
    ///
    /// <b>No sobrescribe.</b> Lo que ya está guardado se queda como está: se comprobó cuando se guardó, y
    /// rellenar dos veces no debe cambiar el pasado.
    /// </summary>
    /// <param name="desde">El día más antiguo que interesa.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    public Task<BackfillResult> BackfillAsync(
        DateOnly desde,
        CancellationToken cancellationToken = default);
}
