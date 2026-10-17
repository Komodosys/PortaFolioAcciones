using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.ValueObjects;
using PortaFolioAcciones.Infrastructure.Identity;

namespace PortaFolioAcciones.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Escribe en el libro de auditoria cada cambio que un administrador hace <b>sobre datos de otra
/// persona</b> ([ADR-014](../../../../DOCUMENTACION/decisiones/ADR-014-administrador-escritura-con-auditoria.md)).
///
/// Va en un <b>interceptor</b> y no en una llamada dentro de cada caso de uso, y eso es parte de la
/// decision: una auditoria que depende de que alguien se acuerde de escribirla no es una auditoria,
/// porque el dia que se olvide no habra forma de saberlo. Aqui se registra en el mismo
/// <c>SaveChanges</c> que el propio cambio: <b>si el cambio se guarda, el registro se guarda</b>.
///
/// Se auditan los datos que <b>tienen dueno</b>: carteras, posiciones, lotes, fotos y perfiles. El
/// catalogo compartido (instrumentos, precios) no es de nadie en concreto, asi que no se le atribuye
/// un dueno; si algun dia hace falta auditarlo, tendra su propia regla.
/// </summary>
internal sealed class AuditInterceptor : SaveChangesInterceptor
{
    /// <summary>
    /// Campos que <b>nunca</b> se copian al libro, aunque cambien.
    ///
    /// El libro de auditoria lo lee el administrador, y estos son secretos: guardar el hash de una
    /// contrasena o el testigo de seguridad en un sitio pensado para consultarse seria trasladar el
    /// secreto de la tabla que lo protege a otra que no lo hace. El cambio se registra igual —se ve que
    /// se restablecio— pero sin el valor.
    /// </summary>
    private static readonly HashSet<string> CamposSecretos = new(StringComparer.Ordinal)
    {
        "PasswordHash",
        "SecurityStamp",
        "ConcurrencyStamp",
        "AuthenticatorKey",
    };

    private readonly ICurrentUser _currentUser;

    public AuditInterceptor(ICurrentUser currentUser) => _currentUser = currentUser;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is { } context)
        {
            await RecordAsync(context, cancellationToken);
        }

        return result;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is { } context)
        {
            RecordAsync(context, CancellationToken.None).GetAwaiter().GetResult();
        }

        return result;
    }

    private async Task RecordAsync(DbContext context, CancellationToken cancellationToken)
    {
        // Sin sesion no hay a quien atribuir el cambio. No se inventa un autor: un registro sin autor no
        // audita nada.
        if (_currentUser.Id is not { } actorId)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var records = new List<AuditLog>();

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            // El libro no se audita a si mismo: seria un registro por cada registro.
            if (entry.Entity is AuditLog)
            {
                continue;
            }

            var ownerId = await OwnerOfAsync(context, entry, cancellationToken);

            // Sin dueno son datos compartidos, y si el dueno es quien actua, cada uno con lo suyo.
            // La auditoria es para el poder sobre los datos AJENOS.
            if (ownerId is null || ownerId == actorId)
            {
                continue;
            }

            records.Add(AuditLog.Record(
                now,
                actorId,
                ActionOf(entry.State),
                entry.Metadata.ClrType.Name,
                IdentifierOf(entry),
                ownerId.Value,
                ChangesOf(entry)));
        }

        if (records.Count > 0)
        {
            context.Set<AuditLog>().AddRange(records);
        }
    }

    private static AuditAction ActionOf(EntityState state) => state switch
    {
        EntityState.Added => AuditAction.Created,
        EntityState.Deleted => AuditAction.Deleted,
        _ => AuditAction.Updated,
    };

    /// <summary>
    /// Identificador de la entidad tocada. Se lee de la clave primaria, que puede ser compuesta.
    /// </summary>
    private static Guid IdentifierOf(EntityEntry entry)
    {
        var clave = entry.Metadata.FindPrimaryKey();

        if (clave is null)
        {
            return Guid.Empty;
        }

        foreach (var propiedad in clave.Properties)
        {
            var valor = entry.Property(propiedad.Name).CurrentValue;

            if (valor is Guid guid)
            {
                return guid;
            }
        }

        return Guid.Empty;
    }

    /// <summary>
    /// De quien es el dato. Se resuelve subiendo por la cadena de propiedad: un lote es de quien es su
    /// posicion, y una posicion es de quien es su cartera.
    /// </summary>
    private static async Task<Guid?> OwnerOfAsync(
        DbContext context,
        EntityEntry entry,
        CancellationToken cancellationToken) => entry.Entity switch
    {
        Portfolio cartera => cartera.OwnerUserId,

        // Una persona es «suya»: el perfil se audita a si mismo.
        User persona => persona.Id,

        // Y la cuenta de Identity tambien. Es lo que hace que restablecer la contrasena de otra persona
        // quede registrado sin tener que acordarse de escribirlo a mano: lo ve el interceptor porque la
        // tabla esta en el mismo contexto.
        ApplicationUser cuenta => cuenta.Id,

        Position posicion => await OwnerOfPortfolioAsync(context, posicion.PortfolioId, cancellationToken),

        PortfolioSnapshot foto => await OwnerOfPortfolioAsync(context, foto.PortfolioId, cancellationToken),

        Lot lote => await OwnerOfPortfolioAsync(
            context,
            await PortfolioOfPositionAsync(context, lote.PositionId, cancellationToken),
            cancellationToken),

        _ => null,
    };

    private static async Task<Guid> PortfolioOfPositionAsync(
        DbContext context,
        Guid positionId,
        CancellationToken cancellationToken) =>
        await context.Set<Position>()
            .Where(posicion => posicion.Id == positionId)
            .Select(posicion => posicion.PortfolioId)
            .FirstOrDefaultAsync(cancellationToken);

    private static async Task<Guid?> OwnerOfPortfolioAsync(
        DbContext context,
        Guid portfolioId,
        CancellationToken cancellationToken)
    {
        if (portfolioId == Guid.Empty)
        {
            return null;
        }

        return await context.Set<Portfolio>()
            .Where(cartera => cartera.Id == portfolioId)
            .Select(cartera => (Guid?)cartera.OwnerUserId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Los campos que cambiaron, con el valor anterior y el nuevo.
    ///
    /// Se guardan los dos y no solo el nuevo: un registro que dijera «se modifico la posicion» no sirve
    /// para nada; lo que sirve es «cantidad: 100 a 120».
    /// </summary>
    private static IReadOnlyList<FieldChange> ChangesOf(EntityEntry entry)
    {
        var cambios = new List<FieldChange>();

        switch (entry.State)
        {
            case EntityState.Added:
                foreach (var propiedad in entry.Properties.Where(p => p.CurrentValue is not null))
                {
                    Add(cambios, propiedad.Metadata.Name, null, Format(propiedad.CurrentValue));
                }

                break;

            case EntityState.Deleted:
                foreach (var propiedad in entry.Properties)
                {
                    Add(cambios, propiedad.Metadata.Name, Format(propiedad.OriginalValue), null);
                }

                break;

            default:
                foreach (var propiedad in entry.Properties.Where(EsCambioReal))
                {
                    Add(
                        cambios,
                        propiedad.Metadata.Name,
                        Format(propiedad.OriginalValue),
                        Format(propiedad.CurrentValue));
                }

                break;
        }

        return cambios;
    }

    /// <summary>
    /// Dice si una propiedad marcada como modificada <b>cambio de verdad</b>.
    ///
    /// Hace falta comprobarlo: Identity marca <b>todas</b> las columnas de la cuenta como modificadas
    /// cada vez que se guarda, aunque solo haya cambiado una. Sin esto, restablecer una contrasena
    /// dejaba en el libro dieciseis campos —`Email: null → null`, `AccessFailedCount: 0 → 0`— y los dos
    /// que importaban quedaban enterrados entre el ruido. Un registro lleno de nada es un registro que
    /// nadie lee.
    /// </summary>
    private static bool EsCambioReal(PropertyEntry propiedad)
    {
        if (!propiedad.IsModified)
        {
            return false;
        }

        return !Equals(propiedad.OriginalValue, propiedad.CurrentValue);
    }

    /// <summary>
    /// Anade un cambio, salvo que sea un secreto: en ese caso se registra <b>que cambio</b>, pero no a
    /// que valor. Un libro que guarde el hash de la contrasena nueva es un problema nuevo, no una
    /// solucion.
    /// </summary>
    private static void Add(List<FieldChange> cambios, string campo, string? anterior, string? nuevo)
    {
        if (CamposSecretos.Contains(campo))
        {
            cambios.Add(new FieldChange(campo, "(oculto)", nuevo is null ? null : "(oculto)"));
            return;
        }

        cambios.Add(new FieldChange(campo, anterior, nuevo));
    }

    /// <summary>
    /// Pasa un valor a texto para guardarlo. Se usa la cultura invariante a proposito: el registro no
    /// debe cambiar de forma segun el idioma de quien lo provoco.
    /// </summary>
    private static string? Format(object? value) => value switch
    {
        null => null,
        DateTimeOffset momento => momento.ToString("O", CultureInfo.InvariantCulture),
        DateTime momento => momento.ToString("O", CultureInfo.InvariantCulture),
        IFormattable formateable => formateable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };
}
