using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortaFolioAcciones.Domain.Entities;

namespace PortaFolioAcciones.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo de la foto diaria de una cartera.
///
/// La clave es <b>(cartera, fecha)</b>: una foto por cartera y dia. Guardar dos veces el mismo dia seria
/// tener dos versiones de lo mismo sin saber cual vale.
/// </summary>
internal sealed class PortfolioSnapshotConfiguration : IEntityTypeConfiguration<PortfolioSnapshot>
{
    public void Configure(EntityTypeBuilder<PortfolioSnapshot> builder)
    {
        builder.ToTable("PortfolioSnapshots");

        builder.HasKey(snapshot => new { snapshot.PortfolioId, snapshot.SnapshotDate });

        // La moneda con la que se tomaron los importes. Sin ella, la foto es ininterpretable dentro de
        // un año: un numero sin moneda no dice nada.
        builder.Property(snapshot => snapshot.BaseCurrency)
            .HasMaxLength(ColumnPrecision.CurrencyCode)
            .HasConversion(ValueObjectMapping.CurrencyConverter)
            .IsRequired();

        // Son importes, no precios: cuatro decimales.
        builder.ComplexProperty(snapshot => snapshot.MarketValue,
            money => money.ConfigureMoney(ColumnPrecision.Amount));

        builder.ComplexProperty(snapshot => snapshot.CostBasis,
            money => money.ConfigureMoney(ColumnPrecision.Amount));

        // El efectivo del día. Va aparte del valor de las posiciones a propósito: son dos cosas distintas, y
        // las fotos anteriores a este campo lo tendrán en cero, que es la verdad —no había registro de él—.
        builder.ComplexProperty(snapshot => snapshot.CashValue,
            money => money.ConfigureMoney(ColumnPrecision.Amount));

        // La ganancia NO se mapea: se calcula a partir de los otros dos. Guardarla seria tener dos
        // verdades que pueden divergir si alguien corrige uno de los importes.

        builder.HasIndex(snapshot => new { snapshot.PortfolioId, snapshot.SnapshotDate })
            .HasDatabaseName("IX_PortfolioSnapshots_PortfolioId_SnapshotDate");
    }
}
