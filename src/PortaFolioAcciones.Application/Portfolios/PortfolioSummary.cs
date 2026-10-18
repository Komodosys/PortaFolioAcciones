namespace PortaFolioAcciones.Application.Portfolios;

/// <summary>
/// Lo que se enseña de una cartera en un listado.
/// </summary>
/// <param name="Id">Identificador.</param>
/// <param name="Name">Nombre.</param>
/// <param name="BaseCurrency">Moneda en la que se presentan sus totales, <b>ya resuelta</b>.</param>
/// <param name="InheritsBaseCurrency">
/// Si esa moneda la hereda de la persona en vez de tenerla propia. Se dice porque no es lo mismo: si la
/// hereda, cambiarla en el perfil cambia también la de esta cartera, y quien la mire tiene derecho a
/// saberlo sin abrir la configuración.
/// </param>
/// <param name="RegistrationType">
/// Cómo está registrada a efectos fiscales: <c>RegisteredRetirement</c> (REER/RRSP),
/// <c>RegisteredSavings</c> (CELI/TFSA) o <c>NotRegistered</c>.
///
/// Viaja como el <b>nombre del valor</b> y no como un número, y el idioma lo pone el frontend: si viajara
/// como número habría que traducirlo dos veces, y si viajara traducido el servidor tendría que saber en qué
/// idioma está mirando cada persona.
/// </param>
/// <param name="IsArchived">Si está archivada.</param>
/// <param name="SortOrder">Orden en el que se quiere ver.</param>
/// <param name="PositionCount">
/// Cuántas posiciones tiene.
///
/// Existe a la vista porque es lo que decide si la cartera <b>se puede borrar</b>: una con posiciones se
/// archiva, no se borra. Sin este dato, la pantalla tendría que enseñar un botón que va a fallar, o pedir
/// el detalle de cada cartera solo para saberlo.
/// </param>
/// <param name="OwnerUserId">De quién es.</param>
/// <param name="OwnerDisplayName">Cómo se llama esa persona, para poder enseñarlo.</param>
/// <remarks>
/// <paramref name="OwnerUserId"/> y <paramref name="OwnerDisplayName"/> existen por un motivo concreto:
/// <b>el administrador ve las carteras de todas las personas en la misma lista</b>. Sin decir de quién es
/// cada una, no solo es confuso —«¿cuáles son las mías?»—, sino que puede <b>abrir y modificar la cartera
/// de otra persona sin saberlo</b>. El aviso de «estás tocando datos ajenos» no se puede construir sin
/// este dato.
/// </remarks>
public sealed record PortfolioSummary(
    Guid Id,
    string Name,
    string BaseCurrency,
    bool InheritsBaseCurrency,
    string RegistrationType,
    bool IsArchived,
    int SortOrder,
    int PositionCount,
    Guid OwnerUserId,
    string OwnerDisplayName);
