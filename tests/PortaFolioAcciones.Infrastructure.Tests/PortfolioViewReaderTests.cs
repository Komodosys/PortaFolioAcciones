using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.ValueObjects;
using PortaFolioAcciones.Infrastructure.Persistence.Repositories;
using PortaFolioAcciones.Infrastructure.Tests.Ayudas;

namespace PortaFolioAcciones.Infrastructure.Tests;

/// <summary>
/// Las consultas del lector de la vista de cartera, <b>contra una base de datos de verdad</b>.
///
/// Estas pruebas existen por un fallo concreto: **cerrar una posicion no la quitaba de los totales**. El
/// lector se traia todas las posiciones sin mirar la marca de cerrada, y la valoracion solo descarta las
/// de cantidad cero. Cada pieza por separado estaba bien; el fallo estaba en la consulta.
///
/// Y una prueba que no consulta nada no puede ver un fallo en una consulta. Por eso esta clase levanta
/// SQLite: para que la consulta se ejecute de verdad.
/// </summary>
public sealed class PortfolioViewReaderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset Ahora = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task UnaPosicionCerradaNoCuentaEnLosTotales()
    {
        using var bd = BaseDeDatosDePrueba.Nueva();

        var (carteraId, esperado, cerradaId) = await SembrarAsync(bd);

        using var contexto = bd.CrearContexto();
        var lector = new PortfolioViewReader(contexto, bd.Usuario, SinTiposDeCambio());

        var vista = await lector.GetAsync(carteraId, Ct);

        Assert.NotNull(vista);

        // Solo la abierta: 10 unidades a 120 = 1200. La cerrada tiene 5 a 50 y NO puede sumar.
        Assert.Equal(esperado, vista.TotalValue);
        Assert.Single(vista.Positions);

        // La cerrada sigue existiendo, y se cuenta aparte. Cerrar no borra: el historico tiene que seguir
        // cuadrando, y quien mira la cartera quiere saber cuantas cerradas hay.
        Assert.Equal(1, vista.ClosedPositions);
        Assert.DoesNotContain(vista.Positions, p => p.Id == cerradaId);
        Assert.True(vista.IsComplete);
    }

    [Fact]
    public async Task LaCarteraDeOtraPersonaNoSeVe()
    {
        using var bd = BaseDeDatosDePrueba.Nueva();

        var (carteraId, _, _) = await SembrarAsync(bd);

        // Otra persona, sin ser administradora. El aislamiento se prueba cambiando QUIEN consulta, no
        // cambiando los datos: es la unica forma de saber que la barrera funciona.
        bd.Usuario.Id = Guid.NewGuid();

        using var contexto = bd.CrearContexto();
        var lector = new PortfolioViewReader(contexto, bd.Usuario, SinTiposDeCambio());

        var vista = await lector.GetAsync(carteraId, Ct);

        // Nulo, y no «prohibido»: quien llama no puede distinguir «no existe» de «no es tuya», asi que no
        // puede deducir que la cartera de otra persona existe.
        Assert.Null(vista);

        // Y el filtro global hace que ni siquiera pueda VERLA en un listado. Se comprueba aqui porque es
        // la otra mitad del aislamiento: la barrera explicita corta el acceso directo, y el filtro evita
        // que aparezca sin buscarla.
        Assert.Empty(await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .ToListAsync(contexto.Portfolios, Ct));
    }

    [Fact]
    public async Task ElAdministradorSiVeLaCarteraDeOtraPersona()
    {
        using var bd = BaseDeDatosDePrueba.Nueva(esAdministrador: true);

        var (carteraId, esperado, _) = await SembrarAsync(bd);

        // El administrador es OTRA persona distinta del dueño: si fuera el dueño, la prueba no diria nada
        // sobre el aislamiento.
        bd.Usuario.Id = Guid.NewGuid();

        using var contexto = bd.CrearContexto();
        var lector = new PortfolioViewReader(contexto, bd.Usuario, SinTiposDeCambio());

        var vista = await lector.GetAsync(carteraId, Ct);

        Assert.NotNull(vista);
        Assert.Equal(esperado, vista.TotalValue);
    }

    [Fact]
    public async Task LaVistaDiceDeQuienEsLaCartera()
    {
        using var bd = BaseDeDatosDePrueba.Nueva();

        var (carteraId, _, _) = await SembrarAsync(bd);

        using var contexto = bd.CrearContexto();
        var lector = new PortfolioViewReader(contexto, bd.Usuario, SinTiposDeCambio());

        var vista = await lector.GetAsync(carteraId, Ct);

        // Sin esto, el administrador puede abrir y modificar la cartera de otra persona SIN SABERLO.
        Assert.Equal(bd.Usuario.Id, vista!.OwnerUserId);
        Assert.Equal("Persona de prueba", vista.OwnerDisplayName);
    }

    [Fact]
    public async Task UnaPosicionSinPrecioSeDevuelveAparteYConSusDatos()
    {
        using var bd = BaseDeDatosDePrueba.Nueva();

        var (carteraId, _, _) = await SembrarAsync(bd);

        Guid sinPrecioId;

        using (var escritura = bd.CrearContexto())
        {
            var instrumento = Instrument.Create(
                Ticker.From("SINPRECIO"),
                "Sin precio",
                AssetType.Equity,
                Currency.Cad,
                "XTSE",
                ValuationMode.Market,
                PricingFrequency.EndOfDay,
                Ahora);

            var posicion = Position.Create(carteraId, instrumento.Id, Ahora);
            posicion.AddLot(Quantity.Of(4), Money.Of(25, Currency.Cad), new DateOnly(2026, 3, 1), DateAccuracy.Exact, null, Ahora);

            escritura.Instruments.Add(instrumento);
            escritura.Positions.Add(posicion);

            sinPrecioId = posicion.Id;

            await escritura.SaveChangesAsync(Ct);
        }

        using var contexto = bd.CrearContexto();
        var lector = new PortfolioViewReader(contexto, bd.Usuario, SinTiposDeCambio());

        var vista = await lector.GetAsync(carteraId, Ct);

        // NO se puede valorar, asi que no entra en la tabla de valoradas.
        Assert.DoesNotContain(vista!.Positions, p => p.Id == sinPrecioId);

        // Pero SE DEVUELVE con sus datos. Si no, quien acaba de darla de alta no la ve en ningun sitio y
        // piensa que no se ha guardado.
        var sinValorar = Assert.Single(vista.UnpricedPositions);
        Assert.Equal(sinPrecioId, sinValorar.Id);
        Assert.Equal("SINPRECIO", sinValorar.Symbol);
        Assert.Equal(4m, sinValorar.Quantity);
        Assert.Equal(100m, sinValorar.Cost);
        Assert.Equal("sin-precio", sinValorar.Reason);

        // Y el total se declara PARCIAL. Un total incompleto presentado como total es una cifra falsa.
        Assert.False(vista.IsComplete);
    }

    /// <summary>Un proveedor de tipos de cambio que no tiene ninguno: todo esta en la moneda base.</summary>
    private static ProveedorFxFalso SinTiposDeCambio() => new();

    /// <summary>
    /// Deja una cartera con una posicion abierta y otra cerrada.
    /// </summary>
    /// <returns>La cartera, lo que debe valer y el identificador de la cerrada.</returns>
    private static async Task<(Guid CarteraId, decimal Esperado, Guid CerradaId)> SembrarAsync(
        BaseDeDatosDePrueba bd)
    {
        using var contexto = bd.CrearContexto();

        var persona = User.Create("Persona de prueba", Currency.Cad, AppLanguage.Spanish, Ahora);
        bd.Usuario.Id = persona.Id;

        var cartera = Portfolio.Create(persona.Id, "Cartera de prueba", Ahora);

        var abierto = Instrument.Create(
            Ticker.From("ABIERTA"), "Abierta", AssetType.Equity, Currency.Cad, "XTSE",
            ValuationMode.Market, PricingFrequency.EndOfDay, Ahora);

        var cerrado = Instrument.Create(
            Ticker.From("CERRADA"), "Cerrada", AssetType.Equity, Currency.Cad, "XTSE",
            ValuationMode.Market, PricingFrequency.EndOfDay, Ahora);

        var abierta = Position.Create(cartera.Id, abierto.Id, Ahora);
        abierta.AddLot(Quantity.Of(10), Money.Of(100, Currency.Cad), new DateOnly(2026, 1, 15), DateAccuracy.Exact, null, Ahora);

        var cerrada = Position.Create(cartera.Id, cerrado.Id, Ahora);
        cerrada.AddLot(Quantity.Of(5), Money.Of(50, Currency.Cad), new DateOnly(2026, 2, 10), DateAccuracy.Exact, null, Ahora);

        // Cerrar NO pone la cantidad a cero: conserva las compras para poder reabrir. Ese es justo el
        // detalle que hacia que contar por cantidad no bastara.
        cerrada.Close(Ahora);

        contexto.UserProfiles.Add(persona);
        contexto.Portfolios.Add(cartera);
        contexto.Instruments.AddRange(abierto, cerrado);
        contexto.Positions.AddRange(abierta, cerrada);

        contexto.LatestQuotes.AddRange(
            LatestQuote.Create(abierto.Id, Money.Of(120, Currency.Cad), Ahora, "prueba", Ahora),
            LatestQuote.Create(cerrado.Id, Money.Of(70, Currency.Cad), Ahora, "prueba", Ahora));

        await contexto.SaveChangesAsync(Ct);

        return (cartera.Id, 1200m, cerrada.Id);
    }
}
