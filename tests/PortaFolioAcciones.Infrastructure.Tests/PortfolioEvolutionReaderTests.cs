using PortaFolioAcciones.Application.Portfolios;
using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.ValueObjects;
using PortaFolioAcciones.Infrastructure.Persistence.Repositories;
using PortaFolioAcciones.Infrastructure.Tests.Ayudas;

namespace PortaFolioAcciones.Infrastructure.Tests;

/// <summary>
/// El gráfico de evolución, contra una base de datos de verdad.
///
/// **Estas pruebas existen por la parte difícil, que no es dibujar**: los lotes guardan lo que QUEDA de cada
/// compra, no lo que se compró. Una venta reparte la reducción entre todos los lotes y la cantidad original se
/// pierde. Para saber cuántas acciones había el 1 de marzo hay que invertir las ventas —y los desdoblamientos,
/// que cambian la unidad—, y equivocarse ahí da un gráfico con forma de dato y cifras falsas.
///
/// Por eso la prueba fija las cantidades que hubo en fechas concretas, no el dibujo.
/// </summary>
public sealed class PortfolioEvolutionReaderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset Ahora = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task LasCantidadesDeCadaDiaSeRecuperanInvirtiendoLaVenta()
    {
        using var bd = BaseDeDatosDePrueba.Nueva();

        // 10 acciones compradas en enero, 4 vendidas en junio: quedan 6, y el lote de la base dice 6.
        var carteraId = await SembrarAsync(bd, compradas: 10m, vendidas: 4m, desdoblamiento: null);

        using var contexto = bd.CrearContexto();
        var lector = new PortfolioEvolutionReader(contexto, new RelojFalso(Ahora), new CashReader(contexto));

        var serie = await lector.GetAsync(carteraId, new DateOnly(2024, 1, 10), new DateOnly(2024, 12, 31), 13, Ct);

        Assert.NotNull(serie);
        Assert.Equal("Reconstructed", serie.Source);

        // Los extremos de la serie son fijos: el primer punto es el día pedido y la venta fue en junio, así que
        // el valor de partida es 10 acciones por 100, y el del final 6 por 100.
        Assert.Equal(new DateOnly(2024, 1, 10), serie.Points[0].Date);
        Assert.Equal(1_000m, serie.Points[0].MarketValue);
        Assert.Equal(new DateOnly(2024, 12, 31), serie.Points[^1].Date);
        Assert.Equal(600m, serie.Points[^1].MarketValue);

        // Y ningún punto intermedio puede pasar de 10 acciones: si la inversión estuviera mal, el primero de
        // marzo —el lote de hoy dice 6— valdría 600 en vez de 1.000.
        Assert.All(serie.Points, punto => Assert.InRange(punto.MarketValue, 600m, 1_000m));
    }

    [Fact]
    public async Task ElDesdoblamientoSeDeshaceParaLosDiasAnteriores()
    {
        using var bd = BaseDeDatosDePrueba.Nueva();

        // 10 acciones compradas en enero y un 2:1 en septiembre: el lote de hoy dice 20, pero en marzo eran 10.
        // Y el precio de marzo está en las unidades de marzo, así que la cuenta tiene que volver a 10.
        var carteraId = await SembrarAsync(
            bd, compradas: 10m, vendidas: 0m, desdoblamiento: new DateOnly(2024, 9, 1));

        using var contexto = bd.CrearContexto();
        var lector = new PortfolioEvolutionReader(contexto, new RelojFalso(Ahora), new CashReader(contexto));

        var serie = await lector.GetAsync(carteraId, new DateOnly(2024, 1, 10), new DateOnly(2024, 12, 31), 13, Ct);

        Assert.NotNull(serie);

        // En enero eran 10 acciones: el lote de hoy dice 20 porque el 2:1 de septiembre lo multiplicó, y el
        // precio de enero está en las unidades de enero. Si no se deshiciera el desdoblamiento, el valor de
        // partida saldria al doble: 2.000 donde había 1.000.
        Assert.Equal(1_000m, serie.Points[0].MarketValue);

        // Y en diciembre son 20.
        Assert.Equal(2_000m, serie.Points[^1].MarketValue);
    }

    [Fact]
    public async Task SinMovimientosLaSerieSonLasFotosYLoPuestoEsNulo()
    {
        using var bd = BaseDeDatosDePrueba.Nueva();

        using var contexto = bd.CrearContexto();

        var persona = User.Create("Persona de prueba", Currency.Cad, AppLanguage.Spanish, Ahora);
        bd.Usuario.Id = persona.Id;

        var cartera = Portfolio.Create(persona.Id, "Foto", RegistrationType.NotRegistered, Ahora);

        contexto.UserProfiles.Add(persona);
        contexto.Portfolios.Add(cartera);
        contexto.PortfolioSnapshots.Add(PortfolioSnapshot.Take(
            cartera.Id,
            new DateOnly(2026, 10, 17),
            Currency.Cad,
            Money.Of(1_000m, Currency.Cad),
            Money.Of(800m, Currency.Cad),
            Money.Of(50m, Currency.Cad)));

        await contexto.SaveChangesAsync(Ct);

        var lector = new PortfolioEvolutionReader(contexto, new RelojFalso(Ahora), new CashReader(contexto));
        var serie = await lector.GetAsync(cartera.Id, null, null, 60, Ct);

        Assert.NotNull(serie);

        // Sin un solo movimiento no hay historia que reconstruir: se devuelven las fotos, y se dice.
        Assert.Equal("Snapshots", serie.Source);
        var punto = Assert.Single(serie.Points);

        // Lo que se puso antes de que hubiera registro no se sabe. **Nulo, no cero**: cero diría que no se puso
        // nada, y es mentira.
        Assert.Null(punto.Contributed);
    }

    private static EvolutionPoint Punto(PortfolioEvolution serie, DateOnly fecha)
        => Assert.Single(serie.Points, punto => punto.Date == fecha);

    /// <summary>
    /// Deja una cartera con una compra, una venta y —si se pide— un desdoblamiento, con precios planos de 100.
    /// </summary>
    private static async Task<Guid> SembrarAsync(
        BaseDeDatosDePrueba bd,
        decimal compradas,
        decimal vendidas,
        DateOnly? desdoblamiento)
    {
        using var contexto = bd.CrearContexto();

        var persona = User.Create("Persona de prueba", Currency.Cad, AppLanguage.Spanish, Ahora);
        bd.Usuario.Id = persona.Id;

        var cartera = Portfolio.Create(persona.Id, "Cartera", RegistrationType.NotRegistered, Ahora);

        var instrumento = Instrument.Create(
            Ticker.From("PRUEBA"), "Prueba", AssetType.Equity, Currency.Cad, "XTSE",
            ValuationMode.Market, PricingFrequency.EndOfDay, Ahora);

        var posicion = Position.Create(cartera.Id, instrumento.Id, Ahora);
        posicion.AddLot(
            Quantity.Of(compradas), Money.Of(100m, Currency.Cad), new DateOnly(2024, 1, 10), DateAccuracy.Exact, null, Ahora);

        contexto.UserProfiles.Add(persona);
        contexto.Portfolios.Add(cartera);
        contexto.Instruments.Add(instrumento);
        contexto.Positions.Add(posicion);

        // La apertura es lo que marca que esta cuenta tiene historia: sin ella, el lector devuelve las fotos
        // guardadas en vez de reconstruir, porque no hay punto de partida del que tirar.
        contexto.Movements.Add(Movement.Opening(
            cartera.Id, Money.Of(compradas * 100m, Currency.Cad), new DateOnly(2024, 1, 10), null, Ahora));

        // Precio plano de 100 todos los días: así el valor es la cantidad por cien y la prueba puede leer la
        // cantidad dividiendo. Un precio que cambiara mezclaría las dos cosas que se quieren separar.
        for (var dia = new DateOnly(2024, 1, 10); dia <= new DateOnly(2024, 12, 31); dia = dia.AddDays(1))
        {
            contexto.Prices.Add(Price.Create(instrumento.Id, dia, Money.Of(100m, Currency.Cad), "prueba", Ahora));
        }

        if (vendidas > 0m)
        {
            posicion.Sell(Quantity.Of(vendidas), Ahora);
            contexto.Movements.Add(Movement.Sale(
                cartera.Id, posicion.Id, Quantity.Of(vendidas),
                Money.Of(100m, Currency.Cad), Money.Of(100m, Currency.Cad),
                new DateOnly(2024, 6, 10), null, Ahora));
        }

        if (desdoblamiento is { } diaDelSplit)
        {
            posicion.ApplySplit(2m, diaDelSplit, Ahora);
            instrumento.RecordSplits([new StockSplit(diaDelSplit, 2m)], Ahora);
        }

        await contexto.SaveChangesAsync(Ct);

        return cartera.Id;
    }
}

