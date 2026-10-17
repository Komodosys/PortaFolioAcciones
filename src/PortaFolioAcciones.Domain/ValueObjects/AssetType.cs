namespace PortaFolioAcciones.Domain.ValueObjects;

/// <summary>
/// Que clase de instrumento es.
///
/// Es informativo y sirve para presentar y agrupar. <b>No</b> cambia como se calcula nada: la
/// diversidad real entre una accion, un ETF y un fondo no esta en el comportamiento (todos son
/// cantidad por precio), sino en <b>como se consigue su precio</b>. Ese eje se modela aparte, en
/// <see cref="ValuationMode"/>.
/// </summary>
public enum AssetType
{
    /// <summary>Accion cotizada.</summary>
    Equity = 0,

    /// <summary>Fondo cotizado en bolsa.</summary>
    Etf = 1,

    /// <summary>Fondo mutuo, que no cotiza en bolsa.</summary>
    MutualFund = 2,

    /// <summary>Bono o pagare individual.</summary>
    Bond = 3,

    /// <summary>Fondo de bonos.</summary>
    BondFund = 4,

    /// <summary>Indice, que sirve de referencia y no se posee.</summary>
    Index = 5,

    /// <summary>Efectivo.</summary>
    Cash = 6,
}
