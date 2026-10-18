namespace PortaFolioAcciones.Application.Portfolios;

/// <summary>
/// Que ha pasado al intentar borrar una cartera.
///
/// Existe en vez de un <c>bool</c> porque los tres casos <b>no son lo mismo</b> y la pantalla tiene que decir
/// cual es: «no existe» y «tiene posiciones» se arreglan de formas distintas, y confundirlos llevaria a
/// enseñar «no se ha encontrado» a quien acaba de crear una cartera y le ha metido una posicion.
/// </summary>
public enum DeleteOutcome
{
    /// <summary>
    /// No existe, o no es de quien la pide. <b>Los dos casos son el mismo a proposito</b>: distinguirlos
    /// confirmaria que la cartera de otra persona existe, y con eso ya se puede empezar a preguntar.
    /// </summary>
    NotFound = 0,

    /// <summary>Tiene posiciones, asi que no se borra. Se archiva.</summary>
    HasPositions = 1,

    /// <summary>Se borro.</summary>
    Deleted = 2,
}
