namespace PortaFolioAcciones.Infrastructure.Tests.Ayudas;

/// <summary>
/// Un reloj que se puede mover a mano.
///
/// Existe para poder probar la caducidad de la caché <b>sin esperar de verdad</b>: comprobar que un
/// precio de intradía caduca a los quince minutos esperando quince minutos no es una prueba que nadie
/// ejecute. Con este reloj, la prueba tarda microsegundos y comprueba exactamente lo mismo.
/// </summary>
internal sealed class RelojFalso : TimeProvider
{
    private DateTimeOffset _ahora;

    public RelojFalso(DateTimeOffset? inicio = null) =>
        _ahora = inicio ?? new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _ahora;

    /// <summary>Adelanta el reloj.</summary>
    public void Avanzar(TimeSpan cuanto) => _ahora = _ahora.Add(cuanto);
}
