using Microsoft.EntityFrameworkCore;
using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Application.Portfolios;
using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.ValueObjects;
using PortaFolioAcciones.Infrastructure.Persistence;

namespace PortaFolioAcciones.Infrastructure.Persistence.Repositories;

/// <summary>
/// Acceso a las carteras, con <b>las dos barreras de aislamiento</b>.
///
/// <list type="number">
///   <item><description><b>El filtro global de consulta</b>, declarado en
///   <see cref="PortafolioDbContext"/>: hace que una consulta ni siquiera vea las carteras de otra
///   persona. Es la barrera que evita <b>olvidos</b>.</description></item>
///   <item><description><b>La comprobación explícita</b>, aquí: aunque el filtro estuviera mal
///   configurado o alguien lo desactivara con <c>IgnoreQueryFilters</c>, esta línea corta el paso. Es la
///   barrera que evita que un filtro mal puesto se convierta en una <b>fuga silenciosa</b>.</description></item>
/// </list>
///
/// Las dos son obligatorias. Una sola significa confiar en que nada cambie.
/// </summary>
internal sealed class PortfolioRepository : IPortfolioRepository
{
    private readonly PortafolioDbContext _context;
    private readonly ICurrentUser _currentUser;

    public PortfolioRepository(PortafolioDbContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<PortfolioSummary>> ListAsync(
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return [];
        }

        var carteras = await _context.Portfolios
            .AsNoTracking()
            .Where(cartera => includeArchived || !cartera.IsArchived)
            .OrderBy(cartera => cartera.SortOrder)
            .ThenBy(cartera => cartera.Name)
            .ToListAsync(cancellationToken);

        var propietarios = await PropietariosDeAsync(carteras, cancellationToken);
        var posiciones = await ContarPosicionesAsync(carteras, cancellationToken);

        return [.. carteras.Select(cartera => AResumen(cartera, propietarios, posiciones))];
    }

    public async Task<PortfolioSummary?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cartera = await CargarParaOperarAsync(id, cancellationToken);

        return cartera is null ? null : await AResumenAsync(cartera, cancellationToken);
    }

    public async Task<PortfolioSummary> CreateAsync(
        string name,
        RegistrationType registrationType,
        string? baseCurrency,
        CancellationToken cancellationToken = default)
    {
        if (_currentUser.Id is not { } dueno)
        {
            throw new InvalidOperationException(
                "Sin sesion no se crea nada: una cartera tiene que tener dueno.");
        }

        var ahora = DateTimeOffset.UtcNow;

        // El dueno sale del contexto autenticado, NUNCA de un parametro. Es lo que impide crear una
        // cartera a nombre de otra persona.
        var cartera = Portfolio.Create(dueno, name, registrationType, ahora);

        if (!string.IsNullOrWhiteSpace(baseCurrency))
        {
            cartera.SetBaseCurrency(Currency.FromCode(baseCurrency), ahora);
        }

        _context.Portfolios.Add(cartera);
        await _context.SaveChangesAsync(cancellationToken);

        return await AResumenAsync(cartera, cancellationToken);
    }

    public Task<PortfolioSummary?> RenameAsync(
        Guid id,
        string name,
        CancellationToken cancellationToken = default) =>
        ModificarAsync(id, (cartera, ahora) => cartera.Rename(name, ahora), cancellationToken);

    public Task<PortfolioSummary?> ArchiveAsync(Guid id, CancellationToken cancellationToken = default) =>
        ModificarAsync(id, (cartera, ahora) => cartera.Archive(ahora), cancellationToken);

    public Task<PortfolioSummary?> UnarchiveAsync(Guid id, CancellationToken cancellationToken = default) =>
        ModificarAsync(id, (cartera, ahora) => cartera.Unarchive(ahora), cancellationToken);

    public Task<PortfolioSummary?> SetBaseCurrencyAsync(
        Guid id,
        string? baseCurrency,
        CancellationToken cancellationToken = default) =>
        ModificarAsync(
            id,
            (cartera, ahora) =>
            {
                // Sin moneda se vuelve a heredar la del perfil. Poder deshacer la eleccion importa: si no,
                // una decision de una vez se quedaria fija para siempre y cambiar la moneda del perfil
                // dejaria de afectarle.
                if (string.IsNullOrWhiteSpace(baseCurrency))
                {
                    cartera.InheritBaseCurrency(ahora);
                }
                else
                {
                    cartera.SetBaseCurrency(Currency.FromCode(baseCurrency), ahora);
                }
            },
            cancellationToken);

    public Task<PortfolioSummary?> UpdateAsync(
        Guid id,
        string name,
        RegistrationType registrationType,
        string? baseCurrency,
        CancellationToken cancellationToken = default) =>
        ModificarAsync(
            id,
            (cartera, ahora) =>
            {
                cartera.Rename(name, ahora);
                cartera.SetRegistrationType(registrationType, ahora);

                if (string.IsNullOrWhiteSpace(baseCurrency))
                {
                    cartera.InheritBaseCurrency(ahora);
                }
                else
                {
                    cartera.SetBaseCurrency(Currency.FromCode(baseCurrency), ahora);
                }
            },
            cancellationToken);

    public async Task<DeleteOutcome> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cartera = await CargarParaOperarAsync(id, cancellationToken);

        if (cartera is null)
        {
            return DeleteOutcome.NotFound;
        }

        // La regla la decide el dominio; aqui solo se cuenta lo que hace falta para aplicarla.
        var posiciones = await _context.Positions
            .CountAsync(posicion => posicion.PortfolioId == cartera.Id, cancellationToken);

        if (!Portfolio.CanBeDeleted(posiciones))
        {
            return DeleteOutcome.HasPositions;
        }

        _context.Portfolios.Remove(cartera);
        await _context.SaveChangesAsync(cancellationToken);

        return DeleteOutcome.Deleted;
    }

    /// <summary>
    /// Cuenta las posiciones de cada cartera, <b>en una sola consulta</b>.
    ///
    /// Hace falta para que la pantalla pueda decir si una cartera se puede borrar antes de intentarlo. Con
    /// una consulta por cartera, listar las siete cuentas de la casa serían siete viajes más a la base; y
    /// en la lista del administrador, que ve todas, muchas más.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, int>> ContarPosicionesAsync(
        IReadOnlyList<Portfolio> carteras,
        CancellationToken cancellationToken)
    {
        var ids = carteras.Select(cartera => cartera.Id).ToList();

        var cuentas = await _context.Positions
            .AsNoTracking()
            .Where(posicion => ids.Contains(posicion.PortfolioId))
            .GroupBy(posicion => posicion.PortfolioId)
            .Select(grupo => new { PortfolioId = grupo.Key, Cuantas = grupo.Count() })
            .ToDictionaryAsync(fila => fila.PortfolioId, fila => fila.Cuantas, cancellationToken);

        return cuentas;
    }

    public Task<PortfolioSummary?> ReorderAsync(
        Guid id,
        int sortOrder,
        CancellationToken cancellationToken = default) =>
        ModificarAsync(id, (cartera, ahora) => cartera.Reorder(sortOrder, ahora), cancellationToken);

    /// <summary>
    /// Carga una cartera, comprueba que se puede tocar, aplica el cambio y lo guarda.
    ///
    /// Todas las modificaciones pasan por aquí a propósito: así la comprobación de acceso está escrita
    /// <b>una vez</b>, y no hay forma de añadir una operación nueva y olvidarse de protegerla.
    /// </summary>
    private async Task<PortfolioSummary?> ModificarAsync(
        Guid id,
        Action<Portfolio, DateTimeOffset> cambio,
        CancellationToken cancellationToken)
    {
        var cartera = await CargarParaOperarAsync(id, cancellationToken);

        if (cartera is null)
        {
            return null;
        }

        cambio(cartera, DateTimeOffset.UtcNow);

        // El cambio y su registro en el libro de auditoría se guardan en el MISMO SaveChanges: el
        // interceptor los escribe juntos, así que no puede quedar un cambio sin registrar.
        await _context.SaveChangesAsync(cancellationToken);

        return await AResumenAsync(cartera, cancellationToken);
    }

    /// <summary>Lo mismo que <see cref="AResumen"/>, para una sola cartera: carga su dueño y la resume.</summary>
    private async Task<PortfolioSummary> AResumenAsync(Portfolio cartera, CancellationToken cancellationToken)
    {
        var propietarios = await PropietariosDeAsync([cartera], cancellationToken);
        var posiciones = await ContarPosicionesAsync([cartera], cancellationToken);

        return AResumen(cartera, propietarios, posiciones);
    }

    /// <summary>
    /// Carga una cartera <b>para modificarla</b>, comprobando las dos barreras. Devuelve nulo si no
    /// existe o si quien llama no puede tocarla, sin distinguir los dos casos.
    /// </summary>
    private async Task<Portfolio?> CargarParaOperarAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return null;
        }

        // Aqui NO se usa AsNoTracking: se va a modificar, hay que seguirla.
        var cartera = await _context.Portfolios
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (cartera is null)
        {
            return null;
        }

        // SEGUNDA BARRERA. Esta linea es la que convierte un fallo del filtro en un 404 en vez de en una
        // fuga. Se escribe aunque parezca redundante: precisamente por eso esta.
        return !_currentUser.IsAdmin && cartera.OwnerUserId != _currentUser.Id ? null : cartera;
    }

    /// <summary>
    /// Pasa la cartera a lo que se enseña. La moneda base se <b>resuelve</b> aqui: la cartera puede
    /// heredarla de la persona, y quien la mire no tiene por que saber esa regla.
    /// </summary>
    private static PortfolioSummary AResumen(
        Portfolio cartera,
        IReadOnlyDictionary<Guid, DatosDelPropietario> propietarios,
        IReadOnlyDictionary<Guid, int> posiciones)
    {
        var propietario = propietarios.GetValueOrDefault(cartera.OwnerUserId);

        return new PortfolioSummary(
            cartera.Id,
            cartera.Name,
            cartera.BaseCurrency?.Code ?? propietario?.BaseCurrency.Code ?? Currency.Cad.Code,
            cartera.BaseCurrency is null,
            cartera.RegistrationType.ToString(),
            cartera.IsArchived,
            cartera.SortOrder,
            posiciones.GetValueOrDefault(cartera.Id),
            cartera.OwnerUserId,
            propietario?.DisplayName ?? "?");
    }

    /// <summary>
    /// Los dueños de esas carteras, en <b>una sola consulta</b>.
    ///
    /// Antes cada cartera resolvía su moneda con su propia consulta, así que listar las 86 carteras que ve
    /// el administrador eran 86 consultas. Ahora es una. No es una micro-optimización: con la lista
    /// creciendo, esto es la diferencia entre una pantalla instantánea y una que se arrastra.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, DatosDelPropietario>> PropietariosDeAsync(
        IReadOnlyList<Portfolio> carteras,
        CancellationToken cancellationToken)
    {
        var ids = carteras.Select(cartera => cartera.OwnerUserId).Distinct().ToList();

        var perfiles = await _context.UserProfiles
            .AsNoTracking()
            .Where(perfil => ids.Contains(perfil.Id))
            .Select(perfil => new DatosDelPropietario(perfil.Id, perfil.DisplayName, perfil.BaseCurrency))
            .ToListAsync(cancellationToken);

        return perfiles.ToDictionary(perfil => perfil.Id);
    }

    /// <summary>Lo que hace falta saber del dueño de una cartera para poder enseñarla bien.</summary>
    private sealed record DatosDelPropietario(Guid Id, string DisplayName, Currency BaseCurrency);
}
