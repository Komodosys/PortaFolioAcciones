using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Domain.Tests.ValueObjects;

/// <summary>
/// Los simbolos de estas pruebas son los que aparecen de verdad en los estados de cuenta de Questrade y
/// TD. Si el tipo no los admite, no sirve.
/// </summary>
public class TickerTests
{
    [Theory]
    [InlineData("VEEV")]          // Accion de EE. UU.
    [InlineData("MGK")]           // ETF de EE. UU.
    [InlineData("GIB.A")]         // Clase de accion canadiense, con punto.
    [InlineData("GIB-A.TO")]      // El mismo, con el guion que usa la fuente de precios.
    [InlineData("0P0001K312.TO")] // Fondo canadiense: codigo interno opaco.
    [InlineData("TDB2624C")]      // Codigo de fondo del broker, que no existe en la fuente de precios.
    [InlineData("VFV.TO")]
    [InlineData("XQQ")]
    public void AcceptsEverySymbolThatAppearsInTheRealStatements(string symbol)
    {
        var ticker = Ticker.From(symbol);
        Assert.Equal(symbol.ToUpperInvariant(), ticker.Value);
    }

    [Fact]
    public void NormalisesToUpperCase()
    {
        Assert.Equal("VEEV", Ticker.From("veev").Value);
        Assert.Equal("VEEV", Ticker.From("  Veev  ").Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".VFV")]        // Un simbolo no empieza por punto.
    [InlineData("VFV TO")]      // Ni lleva espacios.
    [InlineData("VFV/TO")]      // Ni barras.
    [InlineData("VFV$")]
    public void RejectsMalformedSymbols(string symbol) =>
        Assert.ThrowsAny<ArgumentException>(() => Ticker.From(symbol));

    [Fact]
    public void RejectsSymbolsThatAreTooLong()
    {
        var tooLong = new string('A', Ticker.MaxLength + 1);
        Assert.Throws<ArgumentException>(() => Ticker.From(tooLong));
    }

    [Fact]
    public void EqualityIsByValue()
    {
        Assert.Equal(Ticker.From("VFV.TO"), Ticker.From("vfv.to"));
        Assert.NotEqual(Ticker.From("VFV.TO"), Ticker.From("VFV"));
    }
}
