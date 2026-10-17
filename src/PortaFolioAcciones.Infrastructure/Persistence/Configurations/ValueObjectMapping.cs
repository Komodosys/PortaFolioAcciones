using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Infrastructure.Persistence.Configurations;

/// <summary>
/// Piezas de mapeo compartidas por las entidades, para no repetir la misma configuracion en cada una.
///
/// Los tipos del dominio <b>no se modifican para que encajen en la base de datos</b>: no llevan
/// atributos ni propiedades de conveniencia para el mapeo. Toda la traduccion vive aqui, en la capa de
/// infraestructura. Es lo que permite que el dominio siga siendo independiente de framework, y esta
/// vigilado por una prueba de arquitectura.
/// </summary>
internal static class ValueObjectMapping
{
    /// <summary>
    /// Una moneda se guarda como su codigo ISO de tres letras, no como un numero. Un numero obligaria a
    /// mantener una tabla de traduccion y a consultarla para poder leer la base de datos a mano.
    /// </summary>
    public static readonly ValueConverter<Currency, string> CurrencyConverter = new(
        currency => currency.Code,
        code => Currency.FromCode(code));

    /// <summary>
    /// Lo mismo, para una moneda <b>opcional</b>. En la cartera, una moneda nula no es un dato que
    /// falte: significa heredar la de la persona, y hay que distinguirlo de tener una moneda.
    /// </summary>
    public static readonly ValueConverter<Currency?, string?> NullableCurrencyConverter = new(
        currency => currency == null ? null : currency.Code,
        code => string.IsNullOrEmpty(code) ? null : Currency.FromCode(code));

    /// <summary>Una cantidad se guarda como numero, con la precision de la columna.</summary>
    public static readonly ValueConverter<Quantity, decimal> QuantityConverter = new(
        quantity => quantity.Value,
        value => Quantity.Of(value));

    /// <summary>Un simbolo se guarda como texto.</summary>
    public static readonly ValueConverter<Ticker, string> TickerConverter = new(
        ticker => ticker.Value,
        value => Ticker.From(value));

    /// <summary>
    /// El idioma se guarda como su codigo de dos letras. Al leerlo se valida: si en la base hubiera un
    /// idioma sin traducciones, falla al leerlo y no al pintar la pantalla en blanco.
    /// </summary>
    public static readonly ValueConverter<AppLanguage, string> LanguageConverter = new(
        language => language.Code,
        code => AppLanguage.FromCode(code));

    /// <summary>
    /// Configura un importe con su moneda. No se fijan nombres de columna a proposito: EF Core los
    /// deriva del nombre de la propiedad compleja, asi que dos importes en la misma entidad no chocan.
    /// </summary>
    /// <param name="complex">El importe a configurar.</param>
    /// <param name="decimals">
    /// Decimales del numero: <see cref="ColumnPrecision.Price"/> para un precio o un coste por unidad,
    /// <see cref="ColumnPrecision.Amount"/> para un importe.
    /// </param>
    public static void ConfigureMoney(this ComplexPropertyBuilder<Money> complex, int decimals)
    {
        complex.Property(money => money.Amount)
            .HasPrecision(ColumnPrecision.NumericTotal, decimals);

        complex.Property(money => money.Currency)
            .HasMaxLength(ColumnPrecision.CurrencyCode)
            .HasConversion(CurrencyConverter);
    }

    /// <summary>
    /// Los enumerados se guardan por su <b>nombre</b>, no por su numero.
    ///
    /// Cuesta unos bytes mas y a cambio la base de datos se puede leer a mano, y reordenar un enumerado
    /// en el codigo no cambia en silencio el significado de los datos ya guardados.
    /// </summary>
    public static PropertyBuilder<TEnum> StoreAsText<TEnum>(this PropertyBuilder<TEnum> property)
        where TEnum : struct, Enum =>
        property.HasConversion<string>().HasMaxLength(32);
}
