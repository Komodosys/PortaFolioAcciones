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
        var cartera = Portfolio.Create(Dueno, "Cartera principal", RegistrationType.NotRegistered, Ahora);

        Assert.Equal(Dueno, cartera.OwnerUserId);
        Assert.Equal("Cartera principal", cartera.Name);
        Assert.False(cartera.IsArchived);
        Assert.Null(cartera.BaseCurrency);
    }

    [Fact]
    public void RejectsAPortfolioWithoutAnOwner()
    {
        // Es la base del aislamiento: una cartera sin dueno no se podria proteger.
        Assert.Throws<ArgumentException>(() => Portfolio.Create(Guid.Empty, "Sin dueno", RegistrationType.NotRegistered, Ahora));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsAnEmptyName(string nombre) =>
        Assert.ThrowsAny<ArgumentException>(() => Portfolio.Create(Dueno, nombre, RegistrationType.NotRegistered, Ahora));

    [Fact]
    public void RejectsANameThatIsTooLong() =>
        Assert.Throws<ArgumentException>(() =>
            Portfolio.Create(
                Dueno,
                new string('A', Portfolio.MaxNameLength + 1),
                RegistrationType.NotRegistered,
                Ahora));

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(27, false)]
    public void SoloSeBorraUnaCarteraVacia(int posiciones, bool sePuede)
    {
        // La regla vive en el dominio y no en la pantalla: esconder un boton no impide llamar a la API. Una
        // cartera con posiciones tiene lotes, y los lotes son historia.
        Assert.Equal(sePuede, Portfolio.CanBeDeleted(posiciones));
    }

    [Fact]
    public void KeepsTheRegistrationTypeItWasCreatedWith()
    {
        // El tipo es obligatorio al crear y no tiene valor por defecto: se guarda el que se pide, no otro.
        var cartera = Portfolio.Create(Dueno, "REER del trabajo", RegistrationType.RegisteredRetirement, Ahora);

        Assert.Equal(RegistrationType.RegisteredRetirement, cartera.RegistrationType);
    }

    [Fact]
    public void ChangesTheRegistrationType()
    {
        var cartera = Portfolio.Create(Dueno, "Cartera", RegistrationType.RegisteredRetirement, Ahora);

        cartera.SetRegistrationType(RegistrationType.NotRegistered, Ahora.AddMinutes(1));

        Assert.Equal(RegistrationType.NotRegistered, cartera.RegistrationType);
        Assert.Equal(Ahora.AddMinutes(1), cartera.UpdatedAtUtc);
    }

    [Fact]
    public void InheritsTheBaseCurrencyFromThePersonByDefault()
    {
        var cartera = Portfolio.Create(Dueno, "Cartera principal", RegistrationType.NotRegistered, Ahora);

        // Sin moneda propia, manda la de la persona. Es el caso normal.
        Assert.Equal(Currency.Cad, cartera.ResolveBaseCurrency(Currency.Cad));
    }

    [Fact]
    public void CanOverrideTheBaseCurrency()
    {
        var cartera = Portfolio.Create(Dueno, "Cartera en dolares", RegistrationType.NotRegistered, Ahora);
        cartera.SetBaseCurrency(Currency.Usd, Ahora);

        Assert.Equal(Currency.Usd, cartera.ResolveBaseCurrency(Currency.Cad));

        cartera.InheritBaseCurrency(Ahora);

        Assert.Equal(Currency.Cad, cartera.ResolveBaseCurrency(Currency.Cad));
    }

    [Fact]
    public void ResolvingAlwaysNeedsThePersonsCurrency()
    {
        var cartera = Portfolio.Create(Dueno, "Cartera", RegistrationType.NotRegistered, Ahora);
        Assert.Throws<ArgumentNullException>(() => cartera.ResolveBaseCurrency(null!));
    }

    [Fact]
    public void ArchiveDoesNotDelete()
    {
        var cartera = Portfolio.Create(Dueno, "Cartera antigua", RegistrationType.NotRegistered, Ahora);

        cartera.Archive(Ahora);

        Assert.True(cartera.IsArchived);
        Assert.Equal("Cartera antigua", cartera.Name);

        cartera.Unarchive(Ahora);

        Assert.False(cartera.IsArchived);
    }

    [Fact]
    public void Reorders()
    {
        var cartera = Portfolio.Create(Dueno, "Cartera", RegistrationType.NotRegistered, Ahora);
        cartera.Reorder(3, Ahora);

        Assert.Equal(3, cartera.SortOrder);
    }

    [Fact]
    public void UpdatesTheModificationDateButNotTheCreationDate()
    {
        var cartera = Portfolio.Create(Dueno, "Cartera", RegistrationType.NotRegistered, Ahora);
        var despues = Ahora.AddDays(1);

        cartera.Rename("Cartera renombrada", despues);

        Assert.Equal(Ahora, cartera.CreatedAtUtc);
        Assert.Equal(despues, cartera.UpdatedAtUtc);
    }
}
