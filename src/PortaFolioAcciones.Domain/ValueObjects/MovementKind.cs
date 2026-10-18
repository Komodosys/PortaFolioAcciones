namespace PortaFolioAcciones.Domain.ValueObjects;

/// <summary>
/// Qué clase de movimiento es.
///
/// <b>No hay «compra».</b> Una compra ya existe en el modelo: es un <c>Lot</c>, con su cantidad, su coste
/// por unidad y su fecha. Duplicarla aquí sería tener el mismo hecho en dos sitios, y el día que uno se
/// corrigiera sin el otro dejarían de cuadrar sin que nada fallara.
///
/// Lo que falta —y es lo que esto registra— son los hechos que hoy no se guardan en ningún sitio: el dinero
/// que entra y sale de la cuenta, y las ventas.
/// </summary>
public enum MovementKind
{
    /// <summary>Dinero que entra a la cuenta desde fuera.</summary>
    Deposit = 0,

    /// <summary>Dinero que sale de la cuenta hacia fuera.</summary>
    Withdrawal = 1,

    /// <summary>Una venta de una posición, con su precio.</summary>
    Sale = 2,
}
