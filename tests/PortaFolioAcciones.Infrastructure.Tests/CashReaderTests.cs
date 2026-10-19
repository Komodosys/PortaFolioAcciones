using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.ValueObjects;
using PortaFolioAcciones.Infrastructure.Persistence.Repositories;
using PortaFolioAcciones.Infrastructure.Tests.Ayudas;

namespace PortaFolioAcciones.Infrastructure.Tests;

/// <summary>
/// El efectivo de una cuenta con dos monedas.
///
/// **Lo que se fija aquí es que el saldo son dos bolsas, no una cifra.** La cuenta real tenía el dinero entrando
/// en coronas y saliendo en dólares, y al fundir las dos monedas en una sola suma la aplicación daba unos
/// 21.500 CAD de dinero que ya no estaba. Estas pruebas existen para que esa suma no vuelva.
/// </summary>
public sealed class CashReaderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset Ahora = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task UnaCuentaConDosMonedasTieneDosBolsasYElTotalVaAlCambioDeHoy()
    {
        using var bd = BaseDeDatosDePrueba.Nueva();

        // 1.000 CAD de apertura, 1.000 USD de aportación, y 500 CAD convertidos a 357,14 USD al 1,40.
        // Hoy el dólar está a 1,50: la bolsa de dólares vale más de lo que valía el día que se convirtió.
        var carteraId = await SembrarAsync(bd, tipoDeLaConversion: 1.40m, tipoDeHoy: 1.50m);

        using var contexto = bd.CrearContexto();
        var saldo = await new CashReader(contexto).GetAsync(carteraId, Ct);

        Assert.NotNull(saldo);
        Assert.Equal(2, saldo.Pockets.Count);

        var coronas = Assert.Single(saldo.Pockets, bolsa => bolsa.Currency == "CAD");
        var dolares = Assert.Single(saldo.Pockets, bolsa => bolsa.Currency == "USD");

        // La bolsa de coronas: entraron 1.000 y salieron 500 al convertir. **No conserva el dinero convertido.**
        Assert.Equal(500m, coronas.Amount);

        // La de dólares: la aportación más lo que entró al convertir.
        Assert.Equal(1_357.14m, Math.Round(dolares.Amount, 2));

        // Y el total es cada bolsa al cambio de hoy: 500 + 1.357,14 × 1,50.
        Assert.Equal(2_535.71m, Math.Round(saldo.Balance, 2));

        // **Esta es la prueba de que no se ha vuelto al modelo de una sola bolsa.** Las dos cifras que daba
        // aquel modelo están aquí para que se vea que no coinciden con la buena: 2.400 era llevar el dinero
        // convertido con el tipo del día de la conversión, y 2.900 sumar el CAD que ya se había convertido.
        Assert.NotEqual(2_400m, Math.Round(saldo.Balance, 2));
        Assert.NotEqual(2_900m, Math.Round(saldo.Balance, 2));
    }

    [Fact]
    public async Task SinElCambioDeHoyElSaldoSeDeclaraIncompletoYNoSeInventa()
    {
        using var bd = BaseDeDatosDePrueba.Nueva();

        // Sin tipo de cambio de hoy, la bolsa de dólares no se puede valorar. Se cuenta como no convertida y el
        // saldo queda incompleto: darlo como bueno sería peor que decir que no se sabe.
        var carteraId = await SembrarAsync(bd, tipoDeLaConversion: 1.40m, tipoDeHoy: null);

        using var contexto = bd.CrearContexto();
        var saldo = await new CashReader(contexto).GetAsync(carteraId, Ct);

        Assert.NotNull(saldo);
        Assert.True(saldo.Unconverted > 0);
        Assert.False(saldo.IsKnown);

        // La bolsa de dólares aparece, con su importe y sin valor en coronas.
        var dolares = Assert.Single(saldo.Pockets, bolsa => bolsa.Currency == "USD");
        Assert.Equal(1_357.14m, Math.Round(dolares.Amount, 2));
        Assert.Null(dolares.AmountInBase);
    }

    /// <summary>
    /// Una cartera con dos bolsas: apertura en CAD, aportación en USD y una conversión de CAD a USD.
    /// </summary>
    /// <param name="tipoDeLaConversion">El tipo de la conversión, que es un hecho del día.</param>
    /// <param name="tipoDeHoy">El tipo de hoy, o nulo para no guardar ninguno.</param>
    private static async Task<Guid> SembrarAsync(
        BaseDeDatosDePrueba bd,
        decimal tipoDeLaConversion,
        decimal? tipoDeHoy)
    {
        using var contexto = bd.CrearContexto();

        var persona = User.Create("Persona de prueba", Currency.Cad, AppLanguage.Spanish, Ahora);
        bd.Usuario.Id = persona.Id;

        var cartera = Portfolio.Create(persona.Id, "Cartera de dos monedas", RegistrationType.RegisteredRetirement, Ahora);

        contexto.UserProfiles.Add(persona);
        contexto.Portfolios.Add(cartera);

        contexto.Movements.Add(Movement.Opening(
            cartera.Id, Money.Of(1_000m, Currency.Cad), new DateOnly(2026, 1, 10), null, Ahora));

        contexto.Movements.Add(Movement.Deposit(
            cartera.Id, Money.Of(1_000m, Currency.Usd), new DateOnly(2026, 2, 10), null, Ahora));

        // 500 CAD se convierten en 357,142857 USD (500 / 1,40). Las dos patas son el hecho.
        contexto.Movements.Add(Movement.Conversion(
            cartera.Id,
            Money.Of(500m, Currency.Cad),
            Money.Of(500m / tipoDeLaConversion, Currency.Usd),
            new DateOnly(2026, 3, 10),
            "CONVERSION",
            Ahora));

        if (tipoDeHoy is { } tipo)
        {
            contexto.DailyFxRates.Add(DailyFxRate.FromValue(
                FxRate.Of(Currency.Usd, Currency.Cad, tipo, new DateOnly(2026, 10, 1)), Ahora));
        }

        await contexto.SaveChangesAsync(Ct);

        return cartera.Id;
    }
}
