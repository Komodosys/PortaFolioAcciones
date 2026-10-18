using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.ValueObjects;
using PortaFolioAcciones.Infrastructure.Persistence.Repositories;
using PortaFolioAcciones.Infrastructure.Tests.Ayudas;

namespace PortaFolioAcciones.Infrastructure.Tests;

/// <summary>
/// El informe de rendimiento contra una base de datos de verdad.
///
/// **Estas pruebas existen por un fallo que nadie veía**: «lo aportado» se sumaba sin convertir la moneda, así
/// que en una cartera con movimientos en dólares y en dólares canadienses el número era una suma de peras y
/// manzanas. No se notó durante meses porque todas las carteras eran de una sola moneda; apareció el día que se
/// cargó una cuenta con siete años en dos monedas.
///
/// Y no lo podía cazar ninguna prueba que no consultara: el fallo estaba en cómo se agregaban los movimientos.
/// </summary>
public sealed class PerformanceReaderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset Ahora = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task LoAportadoSeConvierteALaMonedaDeLaCartera()
    {
        using var bd = BaseDeDatosDePrueba.Nueva();

        var carteraId = await SembrarAsync(bd, tipoDeCambio: 1.40m);

        using var contexto = bd.CrearContexto();
        var lector = new PerformanceReader(contexto);

        var informe = await lector.ReadAsync(Ct);
        var fila = Assert.Single(informe.Rows, f => f.PortfolioId == carteraId);

        // La cartera es en CAD. Lo puesto es la apertura de 1.000 CAD mas una aportacion de 1.000 USD al 1,40
        // del dia: 2.400. Sumar los importes tal cual daria 2.000, que es una cifra sin significado; la prueba
        // fija el numero bueno para que el fallo, si vuelve, salga como un numero y no como una opinion.
        Assert.Equal(2_400m, fila.Contributed);
        Assert.Equal(0m, fila.Withdrawn);
        Assert.Equal("CAD", fila.Currency);
    }

    [Fact]
    public async Task SinTipoDeCambioLoAportadoEsNuloYNoUnaSumaAMedias()
    {
        using var bd = BaseDeDatosDePrueba.Nueva();

        // Sin tipo para el dolar: el movimiento en USD no se puede convertir.
        var carteraId = await SembrarAsync(bd, tipoDeCambio: null);

        using var contexto = bd.CrearContexto();
        var lector = new PerformanceReader(contexto);

        var informe = await lector.ReadAsync(Ct);
        var fila = Assert.Single(informe.Rows, f => f.PortfolioId == carteraId);

        // Nulo, y no los 1.000 CAD que si se podian sumar: esa suma se leeria como el total y le faltaria una
        // parte. Es la misma regla que la ganancia realizada, y por eso «lo ganado» tampoco se da.
        Assert.Null(fila.Contributed);
        Assert.Null(fila.NetContributed);
        Assert.Null(fila.Gain);
    }

    [Fact]
    public async Task LosTraspasosNoCuentanComoAportadoPeroSiMuevenElEfectivo()
    {
        using var bd = BaseDeDatosDePrueba.Nueva();

        var carteraId = await SembrarAsync(bd, tipoDeCambio: 1.40m, conTraspasos: true);

        using var contexto = bd.CrearContexto();
        var lector = new PerformanceReader(contexto);
        var efectivo = new CashReader(contexto);

        var informe = await lector.ReadAsync(Ct);
        var fila = Assert.Single(informe.Rows, f => f.PortfolioId == carteraId);
        var saldo = await efectivo.GetAsync(carteraId, Ct);

        // Lo aportado son la apertura y la aportacion: el traspaso no lo puso nadie, ya era del titular. Si
        // contara, el informe diria que se puso dinero que no se puso.
        Assert.Equal(2_400m, fila.Contributed);

        // Pero el traspaso SI esta en el efectivo, y ademas en su propia linea: la apertura y la aportacion no
        // lo incluyen. El saldo es 1.000 + 1.400 + 500.
        Assert.NotNull(saldo);
        Assert.Equal(1_000m, saldo.Opening);
        Assert.Equal(1_400m, saldo.Deposits);
        Assert.Equal(500m, saldo.TransfersIn);
        Assert.Equal(2_900m, saldo.Balance);
    }

    /// <summary>Deja una cartera con una apertura en CAD y una aportación en USD, más lo que se pida.</summary>
    private static async Task<Guid> SembrarAsync(
        BaseDeDatosDePrueba bd,
        decimal? tipoDeCambio,
        bool conTraspasos = false)
    {
        using var contexto = bd.CrearContexto();

        var persona = User.Create("Persona de prueba", Currency.Cad, AppLanguage.Spanish, Ahora);
        bd.Usuario.Id = persona.Id;

        var cartera = Portfolio.Create(persona.Id, "Cartera de prueba", RegistrationType.RegisteredRetirement, Ahora);

        var apertura = Movement.Opening(cartera.Id, Money.Of(1_000m, Currency.Cad), new DateOnly(2026, 1, 10), null, Ahora);

        var aportacion = Movement.Deposit(cartera.Id, Money.Of(1_000m, Currency.Usd), new DateOnly(2026, 2, 10), null, Ahora);
        aportacion.SetRateUsed(tipoDeCambio);

        contexto.UserProfiles.Add(persona);
        contexto.Portfolios.Add(cartera);
        contexto.Movements.Add(apertura);
        contexto.Movements.Add(aportacion);

        if (conTraspasos)
        {
            var traspaso = Movement.TransferIn(cartera.Id, Money.Of(500m, Currency.Cad), new DateOnly(2026, 3, 10), "traspaso", Ahora);
            contexto.Movements.Add(traspaso);
        }

        await contexto.SaveChangesAsync(Ct);

        return cartera.Id;
    }
}
