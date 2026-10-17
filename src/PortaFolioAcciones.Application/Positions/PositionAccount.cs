namespace PortaFolioAcciones.Application.Positions;

/// <summary>
/// La cuenta a la que pertenece una posición: cómo se llama y de quién es.
/// </summary>
/// <param name="PortfolioId">Identificador de la cartera.</param>
/// <param name="PortfolioName">Cómo se llama.</param>
/// <param name="OwnerUserId">De quién es.</param>
/// <param name="OwnerDisplayName">Cómo se llama esa persona.</param>
/// <remarks>
/// Existe para que la pantalla de una posición pueda decir <b>en qué cuenta y de quién</b> se está
/// trabajando. Es un requisito de [ADR-014], no un adorno: el administrador puede modificar datos ajenos,
/// y tiene que saberlo <b>en la pantalla donde los modifica</b>.
///
/// Va aparte de <see cref="PositionDetail"/> y no dentro porque es contexto de la <b>cuenta</b>, no de la
/// posición: el detalle se construye en once sitios —uno de ellos, una proyección de una consulta que no
/// puede ser asíncrona— y arrastrar el dueño hasta todos ellos sería pagar once veces por un dato que la
/// pantalla necesita una vez.
/// </remarks>
public sealed record PositionAccount(
    Guid PortfolioId,
    string PortfolioName,
    Guid OwnerUserId,
    string OwnerDisplayName);
