using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PortaFolioAcciones.Domain.Entities;

namespace PortaFolioAcciones.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo de la posicion.
///
/// Los lotes se mapean como entidad con clave ajena hacia la posicion, no como coleccion poseida: EF
/// Core no deja configurar tipos complejos dentro de una coleccion poseida, y hacia falta mapear el
/// coste por unidad como importe con su moneda.
///
/// La frontera del agregado se respeta igualmente: los lotes <b>solo se modifican a traves de los
/// metodos de la posicion</b>, y eso lo garantiza el propio dominio, no la base de datos.
/// </summary>
internal sealed class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General);

    /// <summary>Las fechas de los desdoblamientos ya aplicados, como lista de dias en JSON.</summary>
    private static readonly ValueConverter<IReadOnlyList<DateOnly>, string> SplitsConverter = new(
        fechas => JsonSerializer.Serialize(fechas, JsonOptions),
        json => (IReadOnlyList<DateOnly>?)JsonSerializer.Deserialize<List<DateOnly>>(json, JsonOptions)
                ?? new List<DateOnly>());

    public void Configure(EntityTypeBuilder<Position> builder)
    {
        builder.ToTable("Positions");
        builder.HasKey(position => position.Id);

        builder.Property(position => position.Notes).HasMaxLength(ColumnPrecision.Notes);
        builder.Property(position => position.ClosedAtUtc);
        builder.Property(position => position.CreatedAtUtc).IsRequired();
        builder.Property(position => position.UpdatedAtUtc).IsRequired();

        // Los desdoblamientos ya aplicados. Van en una columna `jsonb` y no en una tabla aparte: son una
        // lista corta de dias, no se consultan por si mismos —se leen con la posicion— y una tabla solo
        // añadiria una clave ajena que mantener.
        builder.Property(position => position.AppliedSplitDates)
            .HasColumnType("jsonb")
            .HasConversion(SplitsConverter, new ValueComparer<IReadOnlyList<DateOnly>>(
                (izquierda, derecha) => izquierda!.SequenceEqual(derecha!),
                fechas => fechas.Aggregate(0, (suma, fecha) => HashCode.Combine(suma, fecha.GetHashCode())),
                fechas => fechas.ToList()))
            // El campo se dice a proposito: se llama `_splitsApplied` y la convencion de EF esperaria
            // `_appliedSplitDates`. Sin esta linea, EF no lo encuentra y falla al construir el modelo —cosa
            // que solo se ve al generar una migracion, no al compilar.
            .HasField("_splitsApplied")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .IsRequired();

        // Lo que habia costado la posicion al cerrarse. Es un importe: 4 decimales.
        builder.ComplexProperty(position => position.CostBasisAtClose,
            money => money.ConfigureMoney(ColumnPrecision.Amount));

        // Precio introducido a mano, para instrumentos que no publican cotizacion. Es un precio:
        // 6 decimales.
        builder.ComplexProperty(position => position.ManualPrice,
            money => money.ConfigureMoney(ColumnPrecision.Price));

        builder.Property(position => position.ManualPriceDate);

        builder.HasMany(position => position.Lots)
            .WithOne()
            .HasForeignKey(lot => lot.PositionId)
            // Si desaparece la posicion, sus lotes no significan nada sueltos.
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(position => position.Lots).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Lo que se consulta de verdad: las posiciones de una cartera.
        builder.HasIndex(position => position.PortfolioId);
        builder.HasIndex(position => new { position.PortfolioId, position.InstrumentId });
    }
}
