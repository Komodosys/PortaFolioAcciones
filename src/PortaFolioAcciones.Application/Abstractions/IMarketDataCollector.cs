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
}
