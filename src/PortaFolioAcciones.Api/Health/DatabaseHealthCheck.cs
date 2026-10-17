using Microsoft.Extensions.Diagnostics.HealthChecks;
using PortaFolioAcciones.Infrastructure.Persistence;

namespace PortaFolioAcciones.Api.Health;

/// <summary>
/// Comprueba que la API alcanza PostgreSQL de verdad, abriendo una conexion.
///
/// No basta con que el contenedor de la base de datos este "arriba": puede estarlo y no aceptar
/// conexiones todavia, o no ser alcanzable desde la red de Docker. Esto lo detecta.
/// </summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly PortafolioDbContext _contexto;

    public DatabaseHealthCheck(PortafolioDbContext contexto) => _contexto = contexto;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext contexto,
        CancellationToken cancellationToken = default)
    {
        var alcanzable = await _contexto.Database.CanConnectAsync(cancellationToken);

        return alcanzable
            ? HealthCheckResult.Healthy("PostgreSQL responde.")
            : HealthCheckResult.Unhealthy("No se puede alcanzar PostgreSQL.");
    }
}
