using PortaFolioAcciones.Application.Abstractions;

namespace PortaFolioAcciones.Infrastructure.MarketData;

/// <summary>
/// Quien es «el sistema»: quien ejecuta el trabajo diario.
///
/// <b>No es una persona</b>, y por eso no tiene identificador. Y es administrador por una razon concreta:
/// el job mantiene los datos de <b>todas</b> las carteras, no las de nadie en particular. Sin eso, el
/// filtro de aislamiento —que falla cerrado— le esconderia todas y no recogeria ningun precio.
///
/// Que no tenga identificador tiene una consecuencia que conviene saber: <b>el libro de auditoria no
/// registra lo que escribe el job</b>. Y es lo correcto: la auditoria existe para el poder de una persona
/// sobre los datos de otra, y aqui no hay ninguna persona decidiendo. El precio de un dia no es una
/// decision, es un dato.
/// </summary>
internal sealed class UsuarioDelSistema : ICurrentUser
{
    /// <summary>La unica instancia: no tiene estado.</summary>
    public static readonly UsuarioDelSistema Instancia = new();

    private UsuarioDelSistema()
    {
    }

    public Guid? Id => null;

    public bool IsAuthenticated => true;

    public bool IsAdmin => true;
}
