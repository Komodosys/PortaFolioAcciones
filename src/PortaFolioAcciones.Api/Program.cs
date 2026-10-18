using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using PortaFolioAcciones.Api.Authentication;
using PortaFolioAcciones.Api.Endpoints;
using PortaFolioAcciones.Api.Health;
using PortaFolioAcciones.Api.MarketData;
using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Infrastructure;
using PortaFolioAcciones.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------------------------
// Registro de sucesos
// ---------------------------------------------------------------------------------------------
// Serilog sustituye al registro por defecto. Se configura desde appsettings.json para poder cambiar
// el nivel sin recompilar.
builder.Host.UseSerilog((contexto, configuracion) => configuracion
    .ReadFrom.Configuration(contexto.Configuration)
    .Enrich.FromLogContext());

// ---------------------------------------------------------------------------------------------
// Servicios
// ---------------------------------------------------------------------------------------------

// Respuestas de error con formato estandar (RFC 9457), en vez de texto suelto.
builder.Services.AddProblemDetails(opciones =>
{
    opciones.CustomizeProblemDetails = contexto =>
    {
        // Una peticion mal formada es culpa de quien la manda, no del servidor: le toca un 400, no un
        // 500. Sin esto, enviar un JSON invalido devolvia un error interno, que ademas de ser falso
        // hace pensar que la aplicacion esta rota.
        if (contexto.Exception is BadHttpRequestException peticionInvalida)
        {
            contexto.ProblemDetails.Status = peticionInvalida.StatusCode;
            contexto.ProblemDetails.Title = "request.malformed";
        }
    };
});

// Documento OpenAPI. Solo se expone en desarrollo.
builder.Services.AddOpenApi();

// La cadena de conexion NO se guarda en el repositorio: llega por variable de entorno.
// En Docker la pone docker-compose.yml; en local, el script arrancar-api.cmd desde el fichero .env.
var cadenaConexion = builder.Configuration.GetConnectionString("Portafolio")
    ?? throw new InvalidOperationException(
        "Falta la cadena de conexion 'Portafolio'. Se pasa en la variable de entorno " +
        "'ConnectionStrings__Portafolio'. En Docker la pone docker-compose.yml; para ejecutar en " +
        "local, usa arrancar-api.cmd.");

builder.Services.AddInfrastructure(cadenaConexion);

// ---------------------------------------------------------------------------------------------
// El trabajo diario de precios
// ---------------------------------------------------------------------------------------------
// Va aqui y no en la capa de infraestructura porque es la API la que hospeda. Decide CUANDO recoger
// —esperando a que cierren las bolsas donde hay posiciones—; QUE hay que hacer lo sabe el colector, que si
// vive en infraestructura.
builder.Services.AddHostedService<JobDiarioDePrecios>();

builder.Services
    .AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("base-de-datos");

// Autenticacion local con cookie, sin registro publico (ADR-006).
builder.AddPortafolioAuthentication();

// ---------------------------------------------------------------------------------------------
// Aislamiento entre personas
// ---------------------------------------------------------------------------------------------
// Quien hace la peticion, leido del contexto autenticado y NUNCA de un parametro de la direccion: si
// pudiera venir de fuera, bastaria con cambiar un numero para leer los datos de otra persona.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

// Acceso a las carteras, con las dos barreras: el filtro global declarado en el contexto y la
// comprobacion explicita dentro del repositorio. El registro lo hace la propia capa de infraestructura.

// Proteccion CSRF. Hace falta porque la cookie de sesion viaja SOLA en cada peticion: sin comprobar
// que la peticion la manda de verdad la aplicacion, otra pagina podria provocar acciones en nombre de
// quien tiene la sesion abierta.
builder.Services.AddAntiforgery(opciones =>
{
    opciones.HeaderName = "X-CSRF-TOKEN";
    opciones.Cookie.Name = "portafolio.csrf";

    // Esta cookie NO es HttpOnly, y es a proposito: el JavaScript tiene que poder leerla para
    // devolverla en la cabecera. Es el patron de doble envio, y no un descuido.
    opciones.Cookie.HttpOnly = false;

    // Strict aqui si vale: la cookie solo la necesita el propio JavaScript de la pagina, no se manda
    // al llegar desde un enlace de fuera.
    opciones.Cookie.SameSite = SameSiteMode.Strict;
});

var app = builder.Build();

// ---------------------------------------------------------------------------------------------
// Politica de migraciones
// ---------------------------------------------------------------------------------------------
// La aplicacion NUNCA cambia la estructura de la base de datos por su cuenta: eso lo hace
// `actualizar.cmd`, despues de una copia de seguridad y con la aplicacion parada. Pero si se niega a
// arrancar cuando la base esta desactualizada, porque arrancar contra un esquema viejo da errores
// confusos mas adelante. Es un cinturon, no un mecanismo.
using (var ambito = app.Services.CreateScope())
{
    var contexto = ambito.ServiceProvider.GetRequiredService<PortafolioDbContext>();
    var pendientes = (await contexto.Database.GetPendingMigrationsAsync()).ToArray();

    if (pendientes.Length > 0)
    {
        throw new InvalidOperationException(
            $"La base de datos tiene {pendientes.Length} migracion(es) pendiente(s): " +
            $"{string.Join(", ", pendientes)}. Ejecuta actualizar.cmd antes de arrancar.");
    }
}

// ---------------------------------------------------------------------------------------------
// Primera cuenta de administrador
// ---------------------------------------------------------------------------------------------
// No hay registro publico, asi que la primera cuenta tiene que salir de algun sitio. Se crea sola y
// solo la primera vez; despues, las demas las crea el administrador.
await app.EnsureFirstAdminAsync();

// ---------------------------------------------------------------------------------------------
// Tuberia de peticiones
// ---------------------------------------------------------------------------------------------

app.UseExceptionHandler(new ExceptionHandlerOptions
{
    // El codigo HTTP sale de la excepcion, no del manejador por defecto. Sin esto, un cuerpo mal
    // formado devolvia un 500 con el cuerpo diciendo 400: el contenido era correcto y el codigo
    // mentia, que es peor que las dos cosas mal.
    StatusCodeSelector = excepcion => excepcion switch
    {
        BadHttpRequestException peticionInvalida => peticionInvalida.StatusCode,
        _ => StatusCodes.Status500InternalServerError,
    },
});
app.UseSerilogRequestLogging();

// El orden importa: primero se identifica a quien llama, y despues se comprueba si puede pasar.
app.UseAuthentication();
app.UseAuthorization();

// Y antes de ejecutar nada que cambie datos, se comprueba que la peticion la manda la aplicacion.
//
// El middleware del marco solo valida las peticiones de FORMULARIO, asi que aqui no protege: esta API
// recibe JSON. Se comprobo enviando un inicio de sesion sin testigo y recibiendo un 200.
app.UseAntiforgery();

// Y este es el que si protege: exige el testigo en toda peticion que cambie algo bajo /api/.
app.UseMiddleware<AntiforgeryMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// ---------------------------------------------------------------------------------------------
// Endpoints
// ---------------------------------------------------------------------------------------------

// Los nombres de los campos van en ingles, como el resto de identificadores: son parte del contrato
// de la API, no texto para el usuario.
app.MapGet("/", () => Results.Ok(new
{
    application = "PortaFolioAcciones",
    status = "running",
}))
.WithTags("System")
.WithSummary("Identifica la aplicacion y confirma que esta en pie.");

// Comprobacion de salud en JSON, para poder leerla a mano y para el healthcheck de Docker.
app.MapHealthChecks("/salud", new HealthCheckOptions
{
    ResponseWriter = async (contexto, informe) =>
    {
        contexto.Response.ContentType = "application/json; charset=utf-8";
        await contexto.Response.WriteAsJsonAsync(new
        {
            status = informe.Status.ToString(),
            checks = informe.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                detail = e.Value.Description,
            }),
        });
    },
})
.WithTags("System");

app.MapAuthEndpoints();

app.MapPortfolioEndpoints();

app.MapProfileEndpoints();

app.MapInstrumentEndpoints();

app.MapPositionEndpoints();

app.MapAdminEndpoints();

// Los informes. No requieren ser administrador: el aislamiento de las carteras decide qué se ve.
app.MapReportEndpoints();

// Los refrescos de precios pedidos a mano desde la pantalla. Solo administrador: llaman a una fuente
// externa gratuita y escriben precios.
app.MapMarketEndpoints();

// Solo en desarrollo: son andamiaje para verificar la Fase 5 mientras se construye.
app.MapDevEndpoints();

app.Run();
