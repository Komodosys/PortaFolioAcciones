using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo del instrumento.
///
/// La identidad la forman <b>simbolo, bolsa y moneda</b>, y por eso los tres van en el indice unico:
/// el mismo ETF en dos mercados, o en dos monedas, son dos instrumentos distintos. Fusionarlos por
/// simbolo provoca colisiones reales entre bolsas.
/// </summary>
internal sealed class InstrumentConfiguration : IEntityTypeConfiguration<Instrument>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General);

    /// <summary>Los desdoblamientos conocidos, como lista en JSON.</summary>
    private static readonly ValueConverter<IReadOnlyList<StockSplit>, string> SplitsConverter = new(
        splits => JsonSerializer.Serialize(splits, JsonOptions),
        json => (IReadOnlyList<StockSplit>?)JsonSerializer.Deserialize<List<StockSplit>>(json, JsonOptions)
                ?? new List<StockSplit>());

    public void Configure(EntityTypeBuilder<Instrument> builder)
    {
        builder.ToTable("Instruments");
        builder.HasKey(instrument => instrument.Id);

        builder.Property(instrument => instrument.Symbol)
            .HasMaxLength(ColumnPrecision.Ticker)
            .HasConversion(ValueObjectMapping.TickerConverter)
            .IsRequired();

        builder.Property(instrument => instrument.Name)
            .HasMaxLength(Instrument.MaxNameLength)
            .IsRequired();

        builder.Property(instrument => instrument.QuoteCurrency)
            .HasMaxLength(ColumnPrecision.CurrencyCode)
            .HasConversion(ValueObjectMapping.CurrencyConverter)
            .IsRequired();

        builder.Property(instrument => instrument.Exchange).HasMaxLength(16).IsRequired();
        builder.Property(instrument => instrument.Country).HasMaxLength(2);
        builder.Property(instrument => instrument.Isin).HasMaxLength(Instrument.IsinLength);

        builder.Property(instrument => instrument.AssetType).StoreAsText().IsRequired();
        builder.Property(instrument => instrument.ValuationMode).StoreAsText().IsRequired();
        builder.Property(instrument => instrument.PricingFrequency).StoreAsText().IsRequired();

        // Con que nombre lo conoce la fuente de precios. Nulo mientras no se haya resuelto: un fondo de
        // Fundserv no se puede consultar hasta que alguien confirme la equivalencia.
        builder.Property(instrument => instrument.ProviderKey).HasMaxLength(64);

        // Con que codigo lo conoce el broker.
        builder.Property(instrument => instrument.BrokerCode).HasMaxLength(64);

        builder.Property(instrument => instrument.CreatedAtUtc).IsRequired();
        builder.Property(instrument => instrument.UpdatedAtUtc).IsRequired();

        // Los desdoblamientos que se le conocen. Se acumulan aqui porque la fuente solo declara los del
        // ultimo mes: lo que no se guarde cuando sale, se pierde.
        builder.Property(instrument => instrument.Splits)
            .HasColumnType("jsonb")
            .HasConversion(SplitsConverter, new ValueComparer<IReadOnlyList<StockSplit>>(
                (izquierda, derecha) => izquierda!.SequenceEqual(derecha!),
                splits => splits.Aggregate(0, (suma, split) => HashCode.Combine(suma, split.GetHashCode())),
                splits => splits.ToList()))
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .IsRequired();

        builder.HasIndex(instrument => new { instrument.Symbol, instrument.Exchange, instrument.QuoteCurrency })
            .IsUnique()
            .HasDatabaseName("IX_Instruments_Identity");

        // Se busca por la clave del proveedor al pedir precios, y por el codigo del broker al volver a
        // cargar un extracto. Ninguno de los dos es unico por si solo.
        builder.HasIndex(instrument => instrument.ProviderKey);
        builder.HasIndex(instrument => instrument.BrokerCode);
    }
}
