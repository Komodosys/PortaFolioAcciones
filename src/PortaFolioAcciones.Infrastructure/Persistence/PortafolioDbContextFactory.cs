using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PortaFolioAcciones.Infrastructure.Persistence;

/// <summary>
/// Permite que las herramientas de linea de comandos de EF Core (dotnet-ef) construyan el contexto
/// sin arrancar la aplicacion web. Solo se usa en tiempo de diseno: no afecta a la ejecucion.
///
/// La cadena de conexion se lee de la variable de entorno, para no guardar contrasenas en el
/// repositorio. El script `actualizar.cmd` la rellena a partir del fichero .env.
/// </summary>
public sealed class PortafolioDbContextFactory : IDesignTimeDbContextFactory<PortafolioDbContext>
{
    private const string VariableEntorno = "ConnectionStrings__Portafolio";

    public PortafolioDbContext CreateDbContext(string[] args)
    {
        var cadena = Environment.GetEnvironmentVariable(VariableEntorno);

        if (string.IsNullOrWhiteSpace(cadena))
        {
            throw new InvalidOperationException(
                $"Falta la variable de entorno '{VariableEntorno}'. " +
                "Usa `actualizar.cmd`, que la rellena desde el fichero .env.");
        }

        var opciones = new DbContextOptionsBuilder<PortafolioDbContext>()
            .UseNpgsql(cadena)
            .Options;

        return new PortafolioDbContext(opciones);
    }
}
