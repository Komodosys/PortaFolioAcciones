using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PortaFolioAcciones.Domain.Entities;

namespace PortaFolioAcciones.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo del libro de auditoria.
///
/// El detalle de los cambios va en una columna <b><c>jsonb</c></b>: no tiene una forma fija —una
/// posicion cambia de cantidad y un instrumento cambia de nombre— y PostgreSQL lo guarda, lo indexa y
/// lo consulta sin obligar a inventar una tabla de «valores de campos».
///
/// La <b>regla que impide modificar o borrar</b> no se pone aqui: va en la migracion, como disparador
/// de PostgreSQL. Es deliberado. Si la proteccion viviera en el codigo de la aplicacion, cualquiera
/// con acceso a la base de datos podria reescribir el libro y no quedaria rastro.
/// </summary>
internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General);

    /// <summary>Convierte la lista de cambios a JSON y de vuelta.</summary>
    private static readonly ValueConverter<IReadOnlyList<FieldChange>, string> ChangesConverter = new(
        changes => JsonSerializer.Serialize(changes, JsonOptions),
        json => (IReadOnlyList<FieldChange>?)JsonSerializer.Deserialize<List<FieldChange>>(json, JsonOptions)
                ?? new List<FieldChange>());

    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(log => log.Id);

        builder.Property(log => log.OccurredAtUtc).IsRequired();
        builder.Property(log => log.ActorUserId).IsRequired();
        builder.Property(log => log.Action).StoreAsText().IsRequired();
        builder.Property(log => log.EntityType).HasMaxLength(ColumnPrecision.Name).IsRequired();
        builder.Property(log => log.EntityId).IsRequired();

        // El dueno del dato. No es clave ajena: si lo fuera, borrar a una persona se llevaria por
        // delante el registro de lo que se hizo con sus datos.
        builder.Property(log => log.OwnerUserId).IsRequired();

        builder.Property(log => log.Changes)
            .HasColumnType("jsonb")
            // El comparador hace falta porque la propiedad es una coleccion con conversor: sin el, EF no
            // sabe comparar dos listas de cambios y avisa de que podria no detectar modificaciones.
            .HasConversion(ChangesConverter, new ValueComparer<IReadOnlyList<FieldChange>>(
                (izquierda, derecha) => izquierda!.SequenceEqual(derecha!),
                cambios => cambios.Aggregate(0, (suma, cambio) => HashCode.Combine(suma, cambio.GetHashCode())),
                cambios => cambios.ToList()))
            .IsRequired();

        builder.Property(log => log.Reason).HasMaxLength(ColumnPrecision.Notes);

        // Las dos preguntas que se le hacen al libro: que le han hecho a esta persona, y que ha hecho
        // esta persona.
        builder.HasIndex(log => new { log.OwnerUserId, log.OccurredAtUtc });
        builder.HasIndex(log => new { log.ActorUserId, log.OccurredAtUtc });

        // Buscar todo lo que le ha pasado a una entidad concreta.
        builder.HasIndex(log => new { log.EntityType, log.EntityId });
    }
}
