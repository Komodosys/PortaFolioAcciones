using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Domain.Tests.ValueObjects;

public class CurrencyTests
{
    [Theory]
    [InlineData("CAD", "CAD")]
    [InlineData("cad", "CAD")]
    [InlineData("Usd", "USD")]
    [InlineData("eur", "EUR")]
    [InlineData("HKD", "HKD")]
    [InlineData("MXN", "MXN")]
    public void NormalisesTheCodeToUpperCase(string input, string expected) =>
        Assert.Equal(expected, Currency.FromCode(input).Code);

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("CA")]      // Dos letras.
    [InlineData("CADA")]    // Cuatro.
    [InlineData("C4D")]     // Con cifras.
    public void RejectsMalformedCodes(string code) =>
        Assert.ThrowsAny<ArgumentException>(() => Currency.FromCode(code));

    [Fact]
    public void WellKnownCurrenciesAreTheSameInstance()
    {
        // Si no lo fueran, comparar por referencia fallaria en sitios inesperados.
        Assert.Same(Currency.Cad, Currency.FromCode("CAD"));
        Assert.Same(Currency.Usd, Currency.FromCode("usd"));
        Assert.Same(Currency.Eur, Currency.FromCode("EUR"));
    }

    [Fact]
    public void AcceptsCurrenciesThatAreNotInTheShortList()
    {
        // La fuente de tipos de cambio cubre 223 divisas: no se puede cerrar la lista.
        var uncommon = Currency.FromCode("ISK");
        Assert.Equal("ISK", uncommon.Code);
    }

    [Fact]
    public void EqualityIsByCode()
    {
        Assert.Equal(Currency.Cad, Currency.FromCode("CAD"));
        Assert.NotEqual(Currency.Cad, Currency.Usd);
        Assert.True(Currency.Cad == Currency.FromCode("cad"));
        Assert.True(Currency.Cad != Currency.Usd);
    }

    [Fact]
    public void ToStringGivesTheCode() => Assert.Equal("CAD", Currency.Cad.ToString());
}
