using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Infrastructure.Identity;
using PortaFolioAcciones.Infrastructure.MarketData;
using PortaFolioAcciones.Infrastructure.MarketData.Resilience;
using PortaFolioAcciones.Infrastructure.Persistence;
using PortaFolioAcciones.Infrastructure.Persistence.Interceptors;
using PortaFolioAcciones.Infrastructure.Persistence.Repositories;

namespace PortaFolioAcciones.Infrastructure;

/// <summary>
/// Registra los servicios de esta capa.
///
/// La capa de presentacion llama a esto y <b>no necesita conocer ninguna de las clases de aqui</b>: ni
/// el contexto de datos, ni los repositorios. Son detalles de implementacion, y si estuvieran a la
/// vista acabarian usandose desde sitios que no deben.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Anade el acceso a datos y los repositorios.</summary>
    /// <param name="services">Coleccion de servicios.</param>
    /// <param name="connectionString">Cadena de conexion a PostgreSQL.</param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddScoped<AuditInterceptor>();

        services.AddDbContext<PortafolioDbContext>((proveedor, opciones) =>
        {
            opciones.UseNpgsql(connectionString);

            // El libro de auditoria lo escribe un interceptor, no cada caso de uso. Asi el registro va
            // en el MISMO SaveChanges que el cambio: si el cambio se guarda, el registro se guarda.
            opciones.AddInterceptors(proveedor.GetRequiredService<AuditInterceptor>());
        });

        services.AddScoped<IPortfolioRepository, PortfolioRepository>();
        services.AddScoped<IProfileRepository, ProfileRepository>();
        services.AddScoped<IAuditLogReader, AuditLogReader>();        services.AddScoped<IAccountCreator, AccountCreator>();
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<IPasswordManager, PasswordManager>();
        services.AddScoped<IInstrumentRepository, InstrumentRepository>();
        services.AddScoped<IPositionRepository, PositionRepository>();
        services.AddScoped<IMovementRepository, MovementRepository>();
        services.AddScoped<IPortfolioViewReader, PortfolioViewReader>();
        services.AddScoped<ILastKnownPrice, LastKnownPriceReader>();

        // -------------------------------------------------------------------------------------
        // Precios y tipos de cambio
        // -------------------------------------------------------------------------------------
        // La cache en memoria y el reloj. El reloj se inyecta para que la caducidad se pueda probar sin
        // esperar quince minutos de verdad.
        services.AddMemoryCache();
        services.AddSingleton(TimeProvider.System);

        // --- Resiliencia de red ------------------------------------------------------------------
        //
        // Los valores son constantes y no salen de la configuracion, y es una decision consciente: son
        // ajustes que se tocan una vez y no por instalacion. El dia que haga falta cambiarlos sin
        // recompilar, se pasan a configuracion y nada mas cambia.
        //
        // Lo que SI conviene saber: `LlamadasAlDia` es el numero que hay que subir si algun dia se
        // contrata una fuente que permita mas. Y bajarlo es la forma de no pasarse con una gratuita.
        services.AddSingleton(new OpcionesDeResiliencia());
        services.AddSingleton<GuardasDeFuentes>();

        // El estado de los refrescos pedidos a mano: la generación que tira la caché y el freno que impide
        // machacar la fuente. Es Singleton porque el estado es uno solo para toda la aplicación: si cada
        // petición tuviera el suyo, el freno no frenaría nada y la caché no se tiraría nunca.
        services.AddSingleton<RefrescoDePrecios>();

        // El buscador de instrumentos, como cliente HTTP tipado: asi el marco gestiona el ciclo de vida
        // del HttpClient, que es lo que evita el problema clasico de agotar los puertos por crear uno
        // nuevo en cada peticion.
        //
        // Los tres clientes llevan el MISMO manejador de resiliencia, y cada uno con su guarda. Se pone
        // aqui y no dentro de cada adaptador porque es una decision sobre la red, no sobre los precios:
        // dentro habria que escribirla tres veces y la tercera quedaria distinta.
        services.AddHttpClient<IInstrumentSearch, YahooInstrumentSearch>(cliente =>
        {
            cliente.BaseAddress = new Uri("https://query1.finance.yahoo.com/");
            cliente.Timeout = TimeSpan.FromSeconds(10);

            // La fuente rechaza peticiones sin identificarse. Es su forma de distinguir un programa de
            // un navegador, y respetarla es parte de usar un servicio ajeno.
            cliente.DefaultRequestHeaders.UserAgent.ParseAdd(
                "PortaFolioAcciones/1.0 (aplicacion familiar de seguimiento de cartera)");
        })
        .ConResiliencia("query1.finance.yahoo.com");

        // El adaptador de la fuente de mercado, sin cache ni enrutado: solo sabe preguntar.
        services.AddHttpClient<YahooChartPriceProvider>(cliente =>
        {
            cliente.BaseAddress = new Uri("https://query1.finance.yahoo.com/");
            cliente.Timeout = TimeSpan.FromSeconds(10);
            cliente.DefaultRequestHeaders.UserAgent.ParseAdd(
                "PortaFolioAcciones/1.0 (aplicacion familiar de seguimiento de cartera)");
        })
        .ConResiliencia("query1.finance.yahoo.com");

        // El histórico se pide por el MISMO objeto que cotiza, pero expuesto por su otro puerto: una
        // petición por instrumento y una serie de vuelta. Sin caché ni respaldo, porque se usa una vez y lo
        // que devuelve no es «el precio de ahora» sino varios cientos de cierres.
        services.AddScoped<IPriceHistoryProvider>(
            proveedor => proveedor.GetRequiredService<YahooChartPriceProvider>());
        // El adaptador de tipos de cambio, sin cache: solo sabe preguntar.
        services.AddHttpClient<FrankfurterFxRateProvider>(cliente =>
        {
            cliente.BaseAddress = new Uri("https://api.frankfurter.dev/");
            cliente.Timeout = TimeSpan.FromSeconds(10);
        })
        .ConResiliencia("api.frankfurter.dev");

        // La cadena, de fuera hacia dentro:
        //
        //     cache  ->  enrutado por modo de valoracion  ->  cadena de respaldo  ->  fuentes
        //                                                                         ->  ultimo precio conocido
        //
        // Se monta en este orden y no en otro a proposito: la cache va POR FUERA para que el enrutado y
        // la fuente no se ejecuten siquiera cuando el precio ya se conoce. Al reves, cada consulta
        // preguntaria igualmente y la cache no ahorraria nada.
        services.AddScoped<IPriceProvider>(proveedor =>
        {
            var mercado = proveedor.GetRequiredService<YahooChartPriceProvider>();

            // Las fuentes, EN ORDEN. Hoy solo hay una viva: el dia que se anada una secundaria, entra
            // aqui delante o detras de esta, y nada mas cambia.
            IReadOnlyList<IPriceProvider> fuentes = [mercado];

            var cadena = new FallbackPriceProvider(
                fuentes,
                proveedor.GetRequiredService<ILastKnownPrice>(),
                proveedor.GetRequiredService<ILogger<FallbackPriceProvider>>());

            var compuesto = new CompositePriceProvider(cadena);

            return new CachedPriceProvider(
                compuesto,
                proveedor.GetRequiredService<IMemoryCache>(),
                proveedor.GetRequiredService<TimeProvider>(),
                proveedor.GetRequiredService<ILogger<CachedPriceProvider>>(),
                proveedor.GetRequiredService<RefrescoDePrecios>());
        });

        services.AddScoped<IFxRateProvider>(proveedor =>
        {
            var fuente = proveedor.GetRequiredService<FrankfurterFxRateProvider>();

            return new CachedFxRateProvider(
                fuente,
                proveedor.GetRequiredService<IMemoryCache>(),
                proveedor.GetRequiredService<TimeProvider>(),
                proveedor.GetRequiredService<ILogger<CachedFxRateProvider>>());
        });

        // --- El trabajo diario ---------------------------------------------------------------------
        //
        // El colector es quien sabe QUE hay que hacer. El servicio en segundo plano, que decide CUANDO, vive
        // en la API: es ella la que hospeda, y esta biblioteca no tiene por que saber de servicios de fondo.
        // Separados, ademas, el trabajo se puede probar sin levantar nada ni esperar a ninguna hora.
        services.AddScoped<IMarketDataCollector, CollectorDeMercado>();

        return services;
    }
}
