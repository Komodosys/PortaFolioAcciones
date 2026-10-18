using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortaFolioAcciones.Domain.Entities;

namespace PortaFolioAcciones.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo de los movimientos de una cartera.
///
/// Se guarda como texto y no como número, como el resto de enumerados del proyecto: un 2 en la base no dice
/// nada a quien la mire, y reordenar el enumerado cambiaría el significado de todas las filas sin que nada
/// fallara.
///
/// Los índices son los dos que se consultan: <b>los movimientos de una cartera</b> —para el registro— y
/// <b>los de una posición</b> —para la ganancia realizada—, ambos por fecha.
/// </summary>
internal sealed class MovementConfiguration : IEntityTypeConfiguration<Movement>
{
    public void Configure(EntityTypeBuilder<Movement> builder)
    {
        builder.ToTable("Movements");
        builder.HasKey(movimiento => movimiento.Id);

        builder.Property(movimiento => movimiento.Kind)
            .StoreAsText()
            .HasMaxLength(ColumnPrecision.Name)
            .IsRequired();

        builder.Property(movimiento => movimiento.OccurredOn).IsRequired();
        builder.Property(movimiento => movimiento.CreatedAtUtc).IsRequired();
        builder.Property(movimiento => movimiento.Notes).HasMaxLength(1000);

        // Los importes en la moneda del movimiento: no se convierten al guardar. Una venta en dólares es una
        // venta en dólares, y convertirla con el tipo de un día que no es el suyo daría una cifra que no
        // cuadra con el estado de cuenta.
        builder.ComplexProperty(movimiento => movimiento.Amount,
            dinero => dinero.ConfigureMoney(ColumnPrecision.Amount));

        builder.ComplexProperty(movimiento => movimiento.PricePerUnit,
            dinero => dinero.ConfigureMoney(ColumnPrecision.Price));

        builder.Property(movimiento => movimiento.Quantity)
            .HasConversion(ValueObjectMapping.NullableQuantityConverter)
            .HasPrecision(ColumnPrecision.NumericTotal, ColumnPrecision.Quantity);

        builder.HasIndex(movimiento => new { movimiento.PortfolioId, movimiento.OccurredOn })
            .HasDatabaseName("IX_Movements_PortfolioId_OccurredOn");

        builder.HasIndex(movimiento => new { movimiento.PositionId, movimiento.OccurredOn })
            .HasDatabaseName("IX_Movements_PositionId_OccurredOn");
    }
}
