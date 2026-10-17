using Microsoft.AspNetCore.Antiforgery;

namespace PortaFolioAcciones.Api.Authentication;

/// <summary>
/// Comprueba el testigo CSRF en las peticiones que <b>cambian datos</b>.
///
/// Hace falta porque la cookie de sesion viaja sola: el navegador la manda en cualquier peticion a este
/// sitio, la haya hecho la aplicacion o no. Sin esta comprobacion, otra pagina podria provocar acciones
/// en nombre de quien tiene la sesion abierta.
///
/// <b>Por que no basta con <c>app.UseAntiforgery()</c>.</b> Se comprobo ejecutandolo: el middleware del
/// marco solo valida por defecto las peticiones de <b>formulario</b>, y esta API recibe JSON. Enviar el
/// inicio de sesion sin testigo devolvia 200, es decir, no protegia nada. La criptografia y la
/// validacion siguen siendo del marco (<see cref="IAntiforgery.ValidateRequestAsync"/>); lo unico que se
/// decide aqui es <b>a que peticiones se les exige</b>, que es una decision de politica y no de
/// implementacion.
///
/// Se validan las peticiones que cambian algo y van a la API. Las de lectura no: exigir un testigo para
/// leer solo añadiria ruido.
/// </summary>
internal sealed class AntiforgeryMiddleware
{
    private static readonly string[] MetodosQueCambian =
        [HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete];

    /// <summary>
    /// Prefijo de la API. <b>Sin barra final</b>, y esto no es un detalle de estilo:
    /// <c>PathString.StartsWithSegments("/api/")</c> —con barra— <b>no</b> casa con
    /// <c>/api/auth/login</c>, porque la barra final crea un segmento vacio que no existe. El
    /// middleware no se ejecutaba y las peticiones pasaban sin testigo. Se descubrio mirando el
    /// registro, no leyendo el codigo.
    /// </summary>
    private const string PrefijoApi = "/api";

    private readonly RequestDelegate _siguiente;
    private readonly ILogger<AntiforgeryMiddleware> _registro;

    public AntiforgeryMiddleware(RequestDelegate siguiente, ILogger<AntiforgeryMiddleware> registro)
    {
        _siguiente = siguiente;
        _registro = registro;
    }

    public async Task InvokeAsync(HttpContext contexto, IAntiforgery antiforgery)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(antiforgery);

        if (RequiereValidacion(contexto))
        {
            try
            {
                await antiforgery.ValidateRequestAsync(contexto);
            }
            catch (AntiforgeryValidationException)
            {
                _registro.LogWarning(
                    "Peticion rechazada por falta de testigo CSRF: {Metodo} {Ruta}",
                    contexto.Request.Method,
                    contexto.Request.Path);

                contexto.Response.StatusCode = StatusCodes.Status400BadRequest;
                await contexto.Response.WriteAsJsonAsync(new
                {
                    // Un codigo, no un texto: el texto lo pone el frontend, que es quien sabe el idioma.
                    type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                    title = "csrf.invalid",
                    status = StatusCodes.Status400BadRequest,
                });

                return;
            }
        }

        await _siguiente(contexto);
    }

    private static bool RequiereValidacion(HttpContext contexto) =>
        MetodosQueCambian.Contains(contexto.Request.Method, StringComparer.OrdinalIgnoreCase)
        && contexto.Request.Path.StartsWithSegments(PrefijoApi, StringComparison.OrdinalIgnoreCase);
}
