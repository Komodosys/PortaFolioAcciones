namespace PortaFolioAcciones.Application.Positions;

/// <summary>
/// Lo que ha pasado al intentar borrar una posición.
/// </summary>
/// <param name="Existe">Si la posición estaba, o no se ve desde aquí.</param>
/// <param name="Borrado">Si se ha borrado.</param>
/// <param name="Motivo">
/// Por qué no se ha borrado, cuando no se ha borrado. El caso que importa es <c>position.has_sales</c>: una
/// posición con ventas <b>no se borra</b>.
/// </param>
/// <remarks>
/// <b>Borrar una posición mueve el efectivo, y no es un efecto colateral: es lo correcto.</b> El saldo se calcula
/// restando el coste de los lotes, así que al desaparecer la posición el saldo sube exactamente lo que costó. Si
/// la posición se dio de alta por error, ese dinero nunca salió de la cuenta y el saldo debe volver a donde
/// estaba. Si la posición existió de verdad, borrarla sería falsear la contabilidad: para eso está cerrarla.
/// </remarks>
public sealed record PosicionBorrada(bool Existe, bool Borrado, string? Motivo)
{
    /// <summary>No se borra, y se dice por qué.</summary>
    public static PosicionBorrada No(string motivo) => new(true, false, motivo);

    /// <summary>Borrada.</summary>
    public static PosicionBorrada Si() => new(true, true, null);

    /// <summary>No está, o no es de quien pregunta: las dos cosas se responden igual, a propósito.</summary>
    public static PosicionBorrada NoExiste() => new(false, false, null);
}
