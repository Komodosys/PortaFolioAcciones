using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using PortaFolioAcciones.Application.Users;
using PortaFolioAcciones.Application.Abstractions;

namespace PortaFolioAcciones.Api.Authentication;

/// <summary>
/// Implementa <see cref="ICurrentUser"/> a partir de la sesion de ASP.NET Core.
///
/// El identificador sale del <b>contexto autenticado</b>, nunca de un parametro de la URL ni de un valor
/// que mande el cliente. Es la base del aislamiento: si el identificador pudiera venir de fuera,
/// cualquiera podria pedir los datos de cualquiera con solo cambiar un numero.
/// </summary>
internal sealed class HttpContextCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accesor;
    private readonly Lazy<bool> _esAdministrador;

    public HttpContextCurrentUser(IHttpContextAccessor accesor)
    {
        _accesor = accesor;

        // El perfil se consulta una sola vez por peticion, y solo si alguien lo pregunta.
        _esAdministrador = new Lazy<bool>(ConsultarSiEsAdministrador);
    }

    public Guid? Id
    {
        get
        {
            var usuario = _accesor.HttpContext?.User;

            if (usuario?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var valor = usuario.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(valor, out var id) ? id : null;
        }
    }

    public bool IsAuthenticated => Id is not null;

    public bool IsAdmin => _esAdministrador.Value;

    /// <summary>
    /// Lee si es administrador del propio testigo de identidad, que es lo que se firmo al entrar.
    /// No se consulta la base de datos en cada peticion por algo que ya viaja en la cookie.
    /// </summary>
    private bool ConsultarSiEsAdministrador()
    {
        var usuario = _accesor.HttpContext?.User;

        if (usuario?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        return usuario.IsInRole(AccountRoles.Administrator);
    }
}
