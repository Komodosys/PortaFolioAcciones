using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Domain.Tests.Entities;

public class UserTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);

    private static User Nueva() =>
        User.Create("Persona De Ejemplo", Currency.Cad, AppLanguage.Spanish, Ahora);

    [Fact]
    public void CreatesAPersonWithTheirPreferences()
    {
        var persona = Nueva();

        Assert.Equal("Persona De Ejemplo", persona.DisplayName);
        Assert.Equal(Currency.Cad, persona.BaseCurrency);
        Assert.Equal(AppLanguage.Spanish, persona.PreferredLanguage);
        Assert.True(persona.IsActive);

        // Nadie es administrador por el hecho de existir: es un permiso que se concede.
        Assert.False(persona.IsAdmin);
    }

    [Fact]
    public void EveryoneStartsWithTheirOwnIdentifier()
    {
        // El identificador se comparte con Identity, que guarda las credenciales: no hay dos
        // identidades que mantener en sincronia.
        Assert.NotEqual(Nueva().Id, Nueva().Id);
        Assert.NotEqual(Guid.Empty, Nueva().Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsAnEmptyName(string nombre) =>
        Assert.ThrowsAny<ArgumentException>(() =>
            User.Create(nombre, Currency.Cad, AppLanguage.Spanish, Ahora));

    [Fact]
    public void ChangesCurrencyAndLanguage()
    {
        var persona = Nueva();

        persona.ChangeBaseCurrency(Currency.Usd, Ahora);
        persona.ChangePreferredLanguage(AppLanguage.French, Ahora);

        Assert.Equal(Currency.Usd, persona.BaseCurrency);
        Assert.Equal(AppLanguage.French, persona.PreferredLanguage);
    }

    [Fact]
    public void GrantsAndRevokesAdministration()
    {
        var persona = Nueva();

        persona.GrantAdmin(Ahora);
        Assert.True(persona.IsAdmin);

        persona.RevokeAdmin(Ahora);
        Assert.False(persona.IsAdmin);
    }

    [Fact]
    public void DeactivatingDoesNotDelete()
    {
        var persona = Nueva();

        persona.Deactivate(Ahora);

        Assert.False(persona.IsActive);
        Assert.Equal("Persona De Ejemplo", persona.DisplayName);

        persona.Activate(Ahora);

        Assert.True(persona.IsActive);
    }

    [Fact]
    public void NormalisesTheEmail()
    {
        var persona = Nueva();
        persona.SetEmail("  Persona.De.Ejemplo@Example.COM ", Ahora);

        Assert.Equal("persona.de.ejemplo@example.com", persona.Email);
    }

    [Fact]
    public void UpdatesTheModificationDateButNotTheCreationDate()
    {
        var persona = Nueva();
        var despues = Ahora.AddDays(2);

        // Se renombra a algo DISTINTO del nombre inicial: renombrar al mismo valor no probaria que el
        // cambio se aplica, solo que el metodo no revienta.
        persona.Rename("Nombre Cambiado", despues);

        Assert.Equal("Nombre Cambiado", persona.DisplayName);
        Assert.Equal(Ahora, persona.CreatedAtUtc);
        Assert.Equal(despues, persona.UpdatedAtUtc);
    }
}

public class AppLanguageTests
{
    [Theory]
    [InlineData("es", "es")]
    [InlineData("FR", "fr")]
    [InlineData("En", "en")]
    [InlineData("  fr  ", "fr")]
    public void ParsesTheSupportedLanguages(string entrada, string esperado) =>
        Assert.Equal(esperado, AppLanguage.FromCode(entrada).Code);

    [Theory]
    [InlineData("de")]
    [InlineData("pt")]
    [InlineData("")]
    public void RejectsALanguageThereAreNoTranslationsFor(string codigo)
    {
        // Si se aceptara, alguien podria guardar un idioma que no existe y la pantalla saldria vacia.
        // Falla al guardar, no al usar.
        Assert.ThrowsAny<ArgumentException>(() => AppLanguage.FromCode(codigo));
    }

    [Theory]
    [InlineData("es-CA", "es")]          // El navegador anuncia region; solo importa el idioma.
    [InlineData("fr-CA,fr;q=0.9", "fr")]
    [InlineData("en-US,en;q=0.9", "en")]
    [InlineData("de-DE,de;q=0.9,en;q=0.5", "en")]  // Aleman no: se cae al respaldo.
    [InlineData("", "en")]
    [InlineData(null, "en")]
    public void FallsBackToEnglishWhenTheBrowserLanguageIsNotSupported(string? etiqueta, string esperado)
    {
        // El idioma del navegador es una sugerencia, no un dato que deba impedir entrar.
        Assert.Equal(esperado, AppLanguage.FromBrowserTag(etiqueta).Code);
    }

    [Fact]
    public void TheFallbackIsEnglishAndItIsSupported()
    {
        Assert.Equal(AppLanguage.English, AppLanguage.Fallback);
        Assert.Contains(AppLanguage.Fallback, AppLanguage.Supported);
    }

    [Fact]
    public void OffersThreeLanguages()
    {
        Assert.Equal(3, AppLanguage.Supported.Count);
        Assert.Contains(AppLanguage.Spanish, AppLanguage.Supported);
        Assert.Contains(AppLanguage.French, AppLanguage.Supported);
        Assert.Contains(AppLanguage.English, AppLanguage.Supported);
    }

    [Fact]
    public void EqualityIsByCode()
    {
        Assert.Equal(AppLanguage.Spanish, AppLanguage.FromCode("es"));
        Assert.NotEqual(AppLanguage.Spanish, AppLanguage.French);
    }
}
