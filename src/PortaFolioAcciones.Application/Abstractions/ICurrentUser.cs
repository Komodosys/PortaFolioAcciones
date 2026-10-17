namespace PortaFolioAcciones.Application.Abstractions;

/// <summary>
/// Quien esta haciendo la peticion.
///
/// Es un <b>puerto</b>: la capa de aplicacion necesita saber quien llama y que puede hacer, pero no
/// tiene por que saber que eso viene de una cookie de ASP.NET Core. La implementacion vive fuera, en la
/// capa de presentacion.
///
/// Todos los miembros admiten el caso de «nadie»: si no hay usuario, el identificador es nulo y el
/// administrador es falso. Es el valor seguro, porque hace que el aislamiento <b>niegue</b> en vez de
/// permitir cuando algo va mal.
/// </summary>
public interface ICurrentUser
{
    /// <summary>Identificador de la persona que hace la peticion, o nulo si no hay sesion.</summary>
    public Guid? Id { get; }

    /// <summary>Dice si hay una sesion iniciada.</summary>
    public bool IsAuthenticated { get; }

    /// <summary>
    /// Dice si quien llama es administrador. Puede <b>ver y modificar</b> los datos de las demas
    /// personas, y cada cambio suyo queda auditado.
    /// </summary>
    public bool IsAdmin { get; }
}
