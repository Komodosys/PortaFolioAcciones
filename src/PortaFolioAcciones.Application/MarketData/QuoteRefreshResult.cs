namespace PortaFolioAcciones.Application.MarketData;

/// <summary>
/// Lo que ha pasado al refrescar las cotizaciones a mano.
/// </summary>
/// <param name="Instrumentos">Cuántos instrumentos se han intentado.</param>
/// <param name="Actualizados">De cuántos se ha obtenido precio.</param>
/// <param name="SinPrecio">
/// De cuántos no. <b>No es un fallo del programa</b>: la fuente puede no responder, o el mercado puede
/// estar cerrado y no haber nada nuevo que dar. Se dice el número porque callarlo dejaría a la persona sin
/// saber si el botón funcionó.
/// </param>
/// <remarks>
/// <b>No escribe el cierre del día.</b> Es la diferencia con <see cref="DailyCollectionResult"/> y el motivo
/// de que sean dos cosas: refrescar la cotización cambia «lo último que se sabe» y nada más. Guardar eso
/// como cierre de hoy sería apuntar como cierre el precio de las once de la mañana.
/// </remarks>
public sealed record QuoteRefreshResult(int Instrumentos, int Actualizados, int SinPrecio);
