namespace PortaFolioAcciones.Application.Positions;

/// <summary>
/// Una compra concreta, tal como llega de fuera.
/// </summary>
/// <param name="Quantity">Cuántas unidades.</param>
/// <param name="CostPerUnit">A cuánto cada una.</param>
/// <param name="PurchaseDate">Cuándo.</param>
/// <param name="DateAccuracy">
/// Si la fecha es exacta o estimada. Importa: los estados de cuenta de un año entero no siempre dan la
/// fecha de cada compra, y fingir que se sabe cuando no se sabe es peor que decirlo.
/// </param>
/// <param name="Notes">Notas.</param>
public sealed record LotInput(
    decimal Quantity,
    decimal CostPerUnit,
    DateOnly PurchaseDate,
    string? DateAccuracy = null,
    string? Notes = null);

/// <summary>Datos para dar de alta una posición, con sus compras.</summary>
/// <param name="InstrumentId">Sobre qué instrumento.</param>
/// <param name="Lots">Las compras. Puede estar vacío: se puede crear la posición y añadirlas después.</param>
/// <param name="Notes">Notas.</param>
public sealed record AddPositionCommand(
    Guid InstrumentId,
    IReadOnlyList<LotInput> Lots,
    string? Notes = null);

/// <summary>Una venta.</summary>
/// <param name="Quantity">Cuántas unidades se venden.</param>
public sealed record SellCommand(decimal Quantity);

/// <summary>Un precio puesto a mano.</summary>
/// <param name="Amount">El precio.</param>
/// <param name="Currency">En qué moneda.</param>
/// <param name="Date">De qué fecha es el precio.</param>
public sealed record ManualPriceCommand(decimal Amount, string Currency, DateOnly Date);

/// <summary>
/// Un desdoblamiento: por cada unidad, cuántas resultan.
/// </summary>
/// <param name="Ratio">Por ejemplo, 5 para un 5:1.</param>
/// <param name="Date">
/// El día en que ocurrió. Va en el comando y no se deduce, porque es lo que queda registrado como
/// «este ya se atendió»: sin la fecha, la aplicación no puede saber si le falta aplicar un
/// desdoblamiento y tendría que avisar siempre. Una fecha nula usa la de hoy.
/// </param>
/// <param name="Apply">
/// Si hay que multiplicar las cantidades. En <c>false</c> solo se anota que ese día ya está reflejado,
/// que es lo que hace falta cuando las cifras se dieron de alta copiándolas de un estado de cuenta
/// <b>posterior</b> al desdoblamiento: aplicarlo entonces las multiplicaría por segunda vez.
/// </param>
public sealed record SplitCommand(decimal Ratio, DateOnly? Date = null, bool Apply = true);

/// <summary>
/// Resultado de una operación sobre una posición.
/// </summary>
/// <param name="Succeeded">Si salió bien.</param>
/// <param name="Position">La posición resultante.</param>
/// <param name="Errors">Códigos de los problemas, no textos.</param>
public sealed record PositionResult(
    bool Succeeded,
    PositionDetail? Position,
    IReadOnlyList<string> Errors)
{
    /// <summary>Salió bien.</summary>
    public static PositionResult Ok(PositionDetail position) => new(true, position, []);

    /// <summary>No salió, por los motivos indicados.</summary>
    public static PositionResult Failed(params string[] errors) => new(false, null, errors);
}
