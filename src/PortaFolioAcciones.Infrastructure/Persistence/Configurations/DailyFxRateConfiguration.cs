using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortaFolioAcciones.Domain.Entities;

namespace PortaFolioAcciones.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo de los tipos de cambio guardados.
///
/// La clave es el par de monedas y el día, y no un identificador: <b>un tipo de cambio no es algo que uno
/// crea</b>, es lo que publicó la fuente ese día. Con un identificador propio, pedir el mismo par dos veces
/// el mismo día dejaría dos filas con valores distintos y no habría forma de saber cuál vale.
/// </summary>
internal sealed class DailyFxRateConfiguration : IEntityTypeConfiguration<DailyFxRate>
{
    public void Configure(EntityTypeBuilder<DailyFxRate> builder)
    {
        builder.ToTable("DailyFxRates");
        builder.HasKey(tipo => tipo.Id);

        builder.Property(tipo => tipo.From)
            .HasMaxLength(ColumnPrecision.CurrencyCode)
            .HasConversion(ValueObjectMapping.CurrencyConverter)
            .IsRequired();

        builder.Property(tipo => tipo.To)
            .HasMaxLength(ColumnPrecision.CurrencyCode)
            .HasConversion(ValueObjectMapping.CurrencyConverter)
            .IsRequired();

        // Precisión de sobra para un tipo de cambio: las divisas se publican con cuatro o seis decimales, y
        // el producto necesita multiplicar por cantidades que llegan a millones.
        builder.Property(tipo => tipo.Rate).HasPrecision(18, 10).IsRequired();

        builder.Property(tipo => tipo.RateDate).IsRequired();
        builder.Property(tipo => tipo.CreatedAtUtc).IsRequired();

        // El par y el día son únicos: es la clave natural, y la que evita duplicados.
        builder.HasIndex(tipo => new { tipo.From, tipo.To, tipo.RateDate }).IsUnique();
    }
}
