using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortaFolioAcciones.Domain.Entities;

namespace PortaFolioAcciones.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo de un lote: una compra.
/// </summary>
internal sealed class LotConfiguration : IEntityTypeConfiguration<Lot>
{
    public void Configure(EntityTypeBuilder<Lot> builder)
    {
        builder.ToTable("Lots");
        builder.HasKey(lot => lot.Id);

        builder.Property(lot => lot.PositionId).IsRequired();

        builder.Property(lot => lot.Quantity)
            .HasConversion(ValueObjectMapping.QuantityConverter)
            .HasPrecision(ColumnPrecision.NumericTotal, ColumnPrecision.Quantity)
            .IsRequired();

        // El coste por unidad, en la moneda en que se compro: dos columnas, el numero y el codigo.
        builder.ComplexProperty(lot => lot.CostPerUnit,
            money => money.ConfigureMoney(ColumnPrecision.Price));

        builder.Property(lot => lot.PurchaseDate).IsRequired();
        builder.Property(lot => lot.DateAccuracy).StoreAsText().IsRequired();
        builder.Property(lot => lot.Notes).HasMaxLength(ColumnPrecision.Notes);

        // Los lotes se consultan siempre por su posicion.
        builder.HasIndex(lot => lot.PositionId);
    }
}
