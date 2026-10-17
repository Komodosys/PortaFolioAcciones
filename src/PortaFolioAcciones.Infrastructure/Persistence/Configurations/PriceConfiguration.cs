using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortaFolioAcciones.Domain.Entities;

namespace PortaFolioAcciones.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo del cierre diario.
///
/// La clave es <b>(instrumento, fecha)</b>, que es la clave natural: un cierre por instrumento y dia.
/// Ponerla como clave primaria es lo que impide que la misma fila entre dos veces, en vez de confiar
/// en que el codigo se acuerde de comprobarlo.
/// </summary>
internal sealed class PriceConfiguration : IEntityTypeConfiguration<Price>
{
    public void Configure(EntityTypeBuilder<Price> builder)
    {
        builder.ToTable("Prices");

        builder.HasKey(price => new { price.InstrumentId, price.PriceDate });

        builder.ComplexProperty(price => price.Close,
            money => money.ConfigureMoney(ColumnPrecision.Price));

        builder.Property(price => price.Source).HasMaxLength(64).IsRequired();
        builder.Property(price => price.RetrievedAtUtc).IsRequired();

        // Lo que se consulta de verdad: la serie de un instrumento, lo mas reciente primero.
        builder.HasIndex(price => new { price.InstrumentId, price.PriceDate })
            .HasDatabaseName("IX_Prices_InstrumentId_PriceDate");
    }
}

/// <summary>
/// Mapeo del precio mas reciente. Es una fila por instrumento, asi que la clave es el instrumento.
/// </summary>
internal sealed class LatestQuoteConfiguration : IEntityTypeConfiguration<LatestQuote>
{
    public void Configure(EntityTypeBuilder<LatestQuote> builder)
    {
        builder.ToTable("LatestQuotes");

        builder.HasKey(quote => quote.InstrumentId);

        // La clave la pone el dominio, no la base de datos: es el identificador del instrumento, no uno
        // nuevo que haya que generar.
        builder.Property(quote => quote.InstrumentId).ValueGeneratedNever();

        builder.ComplexProperty(quote => quote.Price,
            money => money.ConfigureMoney(ColumnPrecision.Price));

        builder.Property(quote => quote.AsOfUtc).IsRequired();
        builder.Property(quote => quote.Source).HasMaxLength(64).IsRequired();
        builder.Property(quote => quote.MarketState).HasMaxLength(32);
        builder.Property(quote => quote.RetrievedAtUtc).IsRequired();
    }
}
