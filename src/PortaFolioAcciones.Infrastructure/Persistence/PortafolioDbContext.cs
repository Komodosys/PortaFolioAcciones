using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Infrastructure.Identity;

namespace PortaFolioAcciones.Infrastructure.Persistence;

/// <summary>
/// Contexto de datos del proyecto.
///
/// Hereda de <see cref="IdentityDbContext{TUser, TRole, TKey}"/> para que las tablas de Identity
/// (credenciales, intentos fallidos, doble factor) vivan en la misma base de datos y se creen con las
/// mismas migraciones. La clave es <see cref="Guid"/> para que coincida con la del dominio.
///
/// El mapeo de las entidades del proyecto no esta aqui: cada una tiene su fichero en
/// <c>Persistence/Configurations</c>. Con el modelo creciendo, un unico <c>OnModelCreating</c> se
/// vuelve imposible de leer.
/// </summary>
public sealed class PortafolioDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    private readonly ICurrentUser? _currentUser;

    /// <param name="options">Opciones del contexto.</param>
    /// <param name="currentUser">
    /// Quien hace la peticion. Es <b>opcional</b> para que las herramientas de linea de comandos puedan
    /// construir el contexto sin una sesion; en ese caso no hay usuario y el aislamiento <b>niega</b> en
    /// vez de permitir. El valor seguro es el que no deja ver nada.
    /// </param>
    public PortafolioDbContext(DbContextOptions<PortafolioDbContext> options, ICurrentUser? currentUser = null)
        : base(options)
    {
        _currentUser = currentUser;
    }

    /// <summary>Identificador de quien consulta, o vacio si no hay sesion.</summary>
    private Guid CurrentUserId => _currentUser?.Id ?? Guid.Empty;

    /// <summary>Dice si quien consulta es administrador.</summary>
    private bool IsCurrentUserAdmin => _currentUser?.IsAdmin ?? false;

    /// <summary>
    /// Los perfiles del dominio: nombre, moneda, idioma y si es administrador.
    ///
    /// Se llama <c>UserProfiles</c> y no <c>Users</c> porque el contexto ya hereda un <c>Users</c> de
    /// Identity, que son las <b>credenciales</b>. Son dos cosas distintas que comparten identificador,
    /// y conviene que se distingan tambien en el nombre.
    /// </summary>
    public DbSet<User> UserProfiles => Set<User>();

    public DbSet<Portfolio> Portfolios => Set<Portfolio>();

    public DbSet<Instrument> Instruments => Set<Instrument>();

    public DbSet<Position> Positions => Set<Position>();

    public DbSet<Lot> Lots => Set<Lot>();

    public DbSet<Price> Prices => Set<Price>();

    public DbSet<LatestQuote> LatestQuotes => Set<LatestQuote>();

    public DbSet<PortfolioSnapshot> PortfolioSnapshots => Set<PortfolioSnapshot>();

    /// <summary>
    /// Los tipos de cambio que se han usado, con su fecha.
    ///
    /// Se guardan para poder <b>descomponer</b> una variación del patrimonio: sin ellos se puede decir que la
    /// cartera subió un 3 %, pero no si fue porque subieron las acciones o porque se movió el dólar.
    /// </summary>
    public DbSet<DailyFxRate> DailyFxRates => Set<DailyFxRate>();

    /// <summary>
    /// El libro de auditoria. Es de <b>solo anadir</b>: la base de datos rechaza modificarlo o borrarlo.
    /// </summary>
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PortafolioDbContext).Assembly);

        // ---------------------------------------------------------------------------------------
        // PRIMERA BARRERA DEL AISLAMIENTO: el filtro global de consulta.
        //
        // Se aplica a TODA consulta de estas entidades, sin que nadie tenga que acordarse de pedirlo.
        // Es lo que evita el olvido de escribir «y ademas solo las mias» en cada sitio.
        //
        // El administrador lo ve todo (ADR-014). Y sin sesion no se ve nada: el filtro falla cerrado,
        // que es la unica forma segura de fallar.
        //
        // La SEGUNDA barrera esta en PortfolioRepository: alli se vuelve a comprobar a mano. Con una
        // sola, bastaria un filtro mal puesto para convertirlo en una fuga silenciosa.
        // ---------------------------------------------------------------------------------------
        modelBuilder.Entity<Portfolio>()
            .HasQueryFilter(cartera => IsCurrentUserAdmin || cartera.OwnerUserId == CurrentUserId);

        // Las posiciones no tienen dueno propio: pertenecen a una cartera. Se filtran por la cartera a
        // la que pertenecen, y de paso por el filtro de arriba.
        modelBuilder.Entity<Position>()
            .HasQueryFilter(posicion =>
                IsCurrentUserAdmin || Portfolios.Any(cartera => cartera.Id == posicion.PortfolioId));
    }
}
