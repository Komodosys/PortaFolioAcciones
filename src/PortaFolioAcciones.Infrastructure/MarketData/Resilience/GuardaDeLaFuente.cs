namespace PortaFolioAcciones.Infrastructure.MarketData.Resilience;

/// <summary>
/// La guarda de <b>una</b> fuente: su interruptor de circuito y su cuota del dia.
///
/// Es por fuente y no una sola para todas, y eso importa: si el interruptor fuera compartido, que Yahoo
/// estuviera caido dejaria de pedir tambien los tipos de cambio, que son otro servicio y no tienen nada
/// que ver. Un fallo se convertiria en dos.
/// </summary>
/// <remarks>
/// <para>
/// El interruptor tiene <b>tres estados</b> y no dos, que es lo que lo hace util:
/// </para>
/// <list type="bullet">
///   <item><description><b>Cerrado</b>: se llama con normalidad.</description></item>
///   <item><description><b>Abierto</b>: se corta sin salir a la red, hasta que pasa el tiempo de espera.
///   </description></item>
///   <item><description><b>Medio abierto</b>: pasado ese tiempo se deja pasar <b>una sola</b> llamada de
///   prueba. Si sale bien, se cierra; si falla, se vuelve a abrir. Sin esta fase, el interruptor no tendria
///   forma de enterarse de que la fuente se ha recuperado y se quedaria abierto para siempre.</description></item>
/// </list>
/// <para>
/// Los metodos estan protegidos con un cerrojo porque el job puede pedir precios de varias posiciones a la
/// vez, y los contadores se llevan de uno en uno.
/// </para>
/// </remarks>
internal sealed class GuardaDeLaFuente
{
    private readonly OpcionesDeResiliencia _opciones;
    private readonly TimeProvider _reloj;
    private readonly Lock _cerrojo = new();

    private int _fallosSeguidos;
    private DateTimeOffset? _reabrirEn;
    private bool _pruebaEnCurso;

    private DateOnly _diaDelContador;
    private int _llamadasHoy;

    public GuardaDeLaFuente(string nombre, OpcionesDeResiliencia opciones, TimeProvider reloj)
    {
        Nombre = nombre;
        _opciones = opciones;
        _reloj = reloj;
        _diaDelContador = DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime);
    }

    /// <summary>Como se llama la fuente, para los mensajes.</summary>
    public string Nombre { get; }

    /// <summary>
    /// Si el interruptor <b>no</b> esta cerrado: o abierto del todo, o esperando el resultado de la prueba.
    /// </summary>
    public bool EstaAbierto
    {
        get
        {
            lock (_cerrojo)
            {
                return _reabrirEn is not null;
            }
        }
    }

    /// <summary>Cuantas llamadas se han hecho hoy.</summary>
    public int LlamadasHoy
    {
        get
        {
            lock (_cerrojo)
            {
                RenovarElDia();
                return _llamadasHoy;
            }
        }
    }

    /// <summary>
    /// Pide permiso para llamar.
    /// </summary>
    /// <param name="motivo">Por que NO se deja pasar, si no se deja.</param>
    /// <returns>Si se puede llamar.</returns>
    public bool PermiteLlamar(out string? motivo)
    {
        lock (_cerrojo)
        {
            RenovarElDia();

            if (_llamadasHoy >= _opciones.LlamadasAlDia)
            {
                motivo = $"cuota diaria agotada ({_opciones.LlamadasAlDia} llamadas a {Nombre})";
                return false;
            }

            if (_reabrirEn is not { } reabrirEn)
            {
                motivo = null;
                return true;
            }

            if (_reloj.GetUtcNow() < reabrirEn)
            {
                motivo = $"interruptor abierto por fallos seguidos en {Nombre}";
                return false;
            }

            // El tiempo de espera ha pasado: se deja pasar UNA prueba.
            if (_pruebaEnCurso)
            {
                motivo = $"ya hay una llamada de prueba en curso contra {Nombre}";
                return false;
            }

            _pruebaEnCurso = true;
            motivo = null;
            return true;
        }
    }

    /// <summary>Anota que la llamada salio bien. Cierra el interruptor.</summary>
    public void AnotarExito()
    {
        lock (_cerrojo)
        {
            _llamadasHoy++;
            _fallosSeguidos = 0;
            _reabrirEn = null;
            _pruebaEnCurso = false;
        }
    }

    /// <summary>Anota que la llamada fallo.</summary>
    /// <returns>Si con este fallo se acaba de abrir el interruptor.</returns>
    public bool AnotarFallo()
    {
        lock (_cerrojo)
        {
            _llamadasHoy++;
            _fallosSeguidos++;
            _pruebaEnCurso = false;

            if (_fallosSeguidos < _opciones.FallosParaAbrir)
            {
                return false;
            }

            var seAbreAhora = _reabrirEn is null;

            // Se empuja la reapertura hacia adelante en CADA fallo, y eso incluye el fallo de una llamada
            // de prueba: si la prueba falla, el interruptor vuelve a estar abierto del todo y hay que
            // esperar otra vez. Sin esto, se probaria en cada peticion y el interruptor no cortaria nada.
            _reabrirEn = _reloj.GetUtcNow() + _opciones.DuracionAbierto;

            return seAbreAhora;
        }
    }

    private void RenovarElDia()
    {
        var hoy = DateOnly.FromDateTime(_reloj.GetUtcNow().UtcDateTime);

        // El contador se renueva al cambiar el dia. Si el reloj retrocediera —un ajuste de hora del
        // sistema— tambien se renueva, y eso es lo seguro: contar de menos solo gasta cuota, y contar de
        // mas dejaria la aplicacion sin precios sin motivo.
        if (hoy != _diaDelContador)
        {
            _diaDelContador = hoy;
            _llamadasHoy = 0;
        }
    }
}
