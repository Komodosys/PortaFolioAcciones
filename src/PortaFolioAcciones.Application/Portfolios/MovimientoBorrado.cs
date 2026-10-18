namespace PortaFolioAcciones.Application.Portfolios;

/// <summary>Lo que ha pasado al intentar borrar un movimiento.</summary>
/// <param name="Existe">Si el movimiento estaba.</param>
/// <param name="Borrado">Si se ha borrado.</param>
/// <param name="Motivo">Por que no se ha borrado, si no se ha borrado.</param>
public sealed record MovimientoBorrado(bool Existe, bool Borrado, string? Motivo)
{
    public static MovimientoBorrado No(string motivo) => new(true, false, motivo);

    public static MovimientoBorrado Si() => new(true, true, null);

    public static MovimientoBorrado NoExiste() => new(false, false, null);
}