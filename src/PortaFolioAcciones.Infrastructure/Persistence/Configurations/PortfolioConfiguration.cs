using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortaFolioAcciones.Domain.Entities;

namespace PortaFolioAcciones.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo de la cartera. La moneda base es <b>opcional</b>: nula significa heredar la de la persona.
/// </summary>
internal sealed class PortfolioConfiguration : IEntityTypeConfiguration<Portfolio>
{
    public void Configure(EntityTypeBuilder<Portfolio> builder)
    {
        builder.ToTable("Portfolios");
        builder.HasKey(portfolio => portfolio.Id);

        builder.Property(portfolio => portfolio.Name)
            .HasMaxLength(Portfolio.MaxNameLength)
            .IsRequired();

        // Nula = hereda la moneda de la persona. No es un dato que falte: es una decision.
        builder.Property(portfolio => portfolio.BaseCurrency)
            .HasMaxLength(ColumnPrecision.CurrencyCode)
            .HasConversion(ValueObjectMapping.NullableCurrencyConverter);

        builder.Property(portfolio => portfolio.CreatedAtUtc).IsRequired();
        builder.Property(portfolio => portfolio.UpdatedAtUtc).IsRequired();

        // Lo que se consulta de verdad: las carteras de una persona.
        builder.HasIndex(portfolio => portfolio.OwnerUserId);
    }
}
