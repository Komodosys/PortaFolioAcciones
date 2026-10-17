using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Domain.Tests.Entities;

public class PortfolioTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Dueno = Guid.NewGuid();

    [Fact]
    public void CreatesAPortfolioForAPerson()
    {
        var cartera = Portfolio.Create(Dueno, "Cartera principal", Ahora);

        Assert.Equal(Dueno, cartera.OwnerUserId);
        Assert.Equal("Cartera principal", cartera.Name);
        Assert.False(cartera.IsArchived);
        Assert.Null(cartera.BaseCurrency);
    }

    [Fact]
    public void RejectsAPortfolioWithoutAnOwner()
    {
        // Es la base del aislamiento: una cartera sin dueno no se podria proteger.
        Assert.Throws<ArgumentException>(() => Portfolio.Create(Guid.Empty, "Sin dueno", Ahora));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsAnEmptyName(string nombre) =>
        Assert.ThrowsAny<ArgumentException>(() => Portfolio.Create(Dueno, nombre, Ahora));

    [Fact]
    public void RejectsANameThatIsTooLong() =>
        Assert.Throws<ArgumentException>(() =>
            Portfolio.Create(Dueno, new string('A', Portfolio.MaxNameLength + 1), Ahora));

    [Fact]
    public void InheritsTheBaseCurrencyFromThePersonByDefault()
    {
        var cartera = Portfolio.Create(Dueno, "Cartera principal", Ahora);

        // Sin moneda propia, manda la de la persona. Es el caso normal.
        Assert.Equal(Currency.Cad, cartera.ResolveBaseCurrency(Currency.Cad));
    }

    [Fact]
    public void CanOverrideTheBaseCurrency()
    {
        var cartera = Portfolio.Create(Dueno, "Cartera en dolares", Ahora);
        cartera.SetBaseCurrency(Currency.Usd, Ahora);

        Assert.Equal(Currency.Usd, cartera.ResolveBaseCurrency(Currency.Cad));

        cartera.InheritBaseCurrency(Ahora);

        Assert.Equal(Currency.Cad, cartera.ResolveBaseCurrency(Currency.Cad));
    }

    [Fact]
    public void ResolvingAlwaysNeedsThePersonsCurrency()
    {
        var cartera = Portfolio.Create(Dueno, "Cartera", Ahora);
        Assert.Throws<ArgumentNullException>(() => cartera.ResolveBaseCurrency(null!));
    }

    [Fact]
    public void ArchiveDoesNotDelete()
    {
        var cartera = Portfolio.Create(Dueno, "Cartera antigua", Ahora);

        cartera.Archive(Ahora);

        Assert.True(cartera.IsArchived);
        Assert.Equal("Cartera antigua", cartera.Name);

        cartera.Unarchive(Ahora);

        Assert.False(cartera.IsArchived);
    }

    [Fact]
    public void Reorders()
    {
        var cartera = Portfolio.Create(Dueno, "Cartera", Ahora);
        cartera.Reorder(3, Ahora);

        Assert.Equal(3, cartera.SortOrder);
    }

    [Fact]
    public void UpdatesTheModificationDateButNotTheCreationDate()
    {
        var cartera = Portfolio.Create(Dueno, "Cartera", Ahora);
        var despues = Ahora.AddDays(1);

        cartera.Rename("Cartera renombrada", despues);

        Assert.Equal(Ahora, cartera.CreatedAtUtc);
        Assert.Equal(despues, cartera.UpdatedAtUtc);
    }
}
