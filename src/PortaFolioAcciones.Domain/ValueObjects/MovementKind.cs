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
    /// <summary>
    /// El saldo con el que se empieza. <b>No es una aportación.</b>
    ///
    /// Existe porque el pasado no se puede reconstruir: de las compras que se dieron de alta antes de que
    /// hubiera registro de efectivo no se sabe cuántos dólares salieron de la cuenta. Fingir que se puede es
    /// peor que declarar un punto de partida y decirlo.
    /// </summary>
    Opening = 0,

    /// <summary>Dinero que entra a la cuenta desde fuera.</summary>
    Deposit = 1,

    /// <summary>Dinero que sale de la cuenta hacia fuera.</summary>
    Withdrawal = 2,

    /// <summary>Una venta de una posición, con su precio.</summary>
    Sale = 3,

    /// <summary>
    /// Un dividendo cobrado. **Es ganancia**, no dinero aportado: entra en la cuenta pero no viene de fuera, sale
    /// de que la empresa reparte lo que gano. Por eso no cuenta como aportacion y si suma al efectivo.
    /// </summary>
    Dividend = 4,

    /// <summary>
    /// Una comision cobrada por el broker. Sale de la cuenta y **no es un retiro**: no te llevas el dinero, se lo
    /// queda el intermediario.
    /// </summary>
    Commission = 5,
}
