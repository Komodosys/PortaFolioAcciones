namespace PortaFolioAcciones.Infrastructure.MarketData;

/// <summary>
/// El estado de los refrescos de precios que se piden a mano.
///
/// Hace dos cosas que van juntas porque las dos nacen del mismo botón:
///
/// <list type="number">
///   <item><description>
///     <b>Tirar la caché.</b> Un precio intradía se guarda quince minutos. Sin esto, pulsar «actualizar»
///     devolvería el mismo precio que ya había, y la persona concluiría que el botón no hace nada. La
///     generación va dentro del nombre de cada entrada, así que subirla deja toda la caché vieja
///     inalcanzable sin recorrerla ni borrarla a mano.
///   </description></item>
///   <item><description>
///     <b>Frenar.</b> La fuente de precios es gratuita y no le gusta que la machaquen. Dos pulsaciones
///     seguidas no dan información nueva —el precio no cambia en dos segundos— y sí pueden costar un
///     bloqueo temporal.
///   </description></item>
/// </list>
///
/// Es un único objeto para toda la aplicación (<c>Singleton</c>) porque el estado es justo eso: uno.
/// </summary>
public sealed class RefrescoDePrecios
{
    /// <summary>Cuánto hay que esperar entre dos refrescos pedidos a mano.</summary>
    public static readonly TimeSpan EsperaMinima = TimeSpan.FromMinutes(2);

    private readonly TimeProvider _reloj;
    private int _generacion;
    private long _ultimoTick;

    public RefrescoDePrecios(TimeProvider reloj)
    {
        _reloj = reloj;
    }

    /// <summary>La generación actual de la caché. Cambia cada vez que se invalida.</summary>
    public int Generacion => Volatile.Read(ref _generacion);

    /// <summary>
    /// Tira la caché de precios: lo que se pida a partir de ahora se vuelve a consultar a la fuente.
    /// </summary>
    public void InvalidarCache() => Interlocked.Increment(ref _generacion);

    /// <summary>
    /// Intenta reservar un refresco.
    /// </summary>
    /// <returns>
    /// <c>null</c> si se puede hacer; si no, cuánto falta para poder. Se devuelve el tiempo y no un simple
    /// «no» porque la pantalla puede decir «espera 40 segundos» en vez de «no se puede», que no explica nada.
    /// </returns>
    public TimeSpan? EsperarPara()
    {
        var ahora = _reloj.GetUtcNow().UtcTicks;
        var ultimo = Interlocked.Read(ref _ultimoTick);

        if (ultimo != 0)
        {
            var pasado = TimeSpan.FromTicks(ahora - ultimo);

            if (pasado < EsperaMinima)
            {
                return EsperaMinima - pasado;
            }
        }

        // La reserva se hace con un intercambio condicional porque dos pulsaciones simultáneas verían el
        // mismo «último» y las dos pasarían. Solo una gana; la otra recibe la espera.
        return Interlocked.CompareExchange(ref _ultimoTick, ahora, ultimo) == ultimo
            ? null
            : EsperaMinima;
    }
}
