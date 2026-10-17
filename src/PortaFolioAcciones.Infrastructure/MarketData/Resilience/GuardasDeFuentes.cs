using System.Collections.Concurrent;

namespace PortaFolioAcciones.Infrastructure.MarketData.Resilience;

/// <summary>
/// Las guardas de todas las fuentes, una por fuente.
///
/// Existe para que la guarda <b>sobreviva a la peticion</b>. El manejador de resiliencia se crea por
/// cliente HTTP y el marco los recicla cada dos minutos: si el interruptor viviera dentro del manejador, se
/// perderia al reciclarlo y no cortaria nada. La guarda es un unico objeto que dura toda la vida de la
/// aplicacion.
/// </summary>
internal sealed class GuardasDeFuentes
{
    private readonly ConcurrentDictionary<string, GuardaDeLaFuente> _guardas = new(StringComparer.OrdinalIgnoreCase);
    private readonly OpcionesDeResiliencia _opciones;
    private readonly TimeProvider _reloj;

    public GuardasDeFuentes(OpcionesDeResiliencia opciones, TimeProvider reloj)
    {
        _opciones = opciones;
        _reloj = reloj;
    }

    /// <summary>La guarda de una fuente, creandola la primera vez.</summary>
    /// <param name="nombre">Como se llama la fuente. Se usa el nombre del servidor al que se llama.</param>
    public GuardaDeLaFuente Para(string nombre) =>
        _guardas.GetOrAdd(nombre, clave => new GuardaDeLaFuente(clave, _opciones, _reloj));

    /// <summary>Todas las guardas, para poder informar de su estado.</summary>
    public IReadOnlyCollection<GuardaDeLaFuente> Todas => [.. _guardas.Values];
}
