using Microsoft.EntityFrameworkCore;
using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.ValueObjects;
using PortaFolioAcciones.Infrastructure.Persistence.Repositories;
using PortaFolioAcciones.Infrastructure.Tests.Ayudas;

namespace PortaFolioAcciones.Infrastructure.Tests;

/// <summary>
/// Borrar una posición, contra una base de datos de verdad.
///
/// **La decisión que hay detrás no es técnica**: el efectivo se calcula restando el coste de los lotes, así que
/// borrar una posición **sube el saldo** exactamente lo que costó. Está bien si la posición se dio de alta por
/// error —ese dinero nunca salió de la cuenta— y estaría muy mal si existió de verdad, que es para lo que está
/// cerrarla. Por eso estas pruebas fijan las tres cosas: que con ventas no se borra, que el efectivo sube el
/// coste, y que el borrado **queda en el libro**.
/// </summary>
public sealed class PositionDeleteTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset Ahora = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task UnaPosicionConVentasNoSeBorra()
    {
        using var bd = BaseDeDatosDePrueba.Nueva();

        var (posicionId, _) = await SembrarAsync(bd, conVenta: true);

        using var contexto = bd.CrearContexto();
        var repositorio = new PositionRepository(contexto, bd.Usuario);

        var resultado = await repositorio.DeleteAsync(posicionId, Ct);

        // Con ventas no: la ganancia realizada es un hecho y se calcula con esta posición. Borrarla dejaría la
        // venta apuntando a algo que no existe. Es la misma regla que ya protege a la venta.
        Assert.True(resultado.Existe);
        Assert.False(resultado.Borrado);
        Assert.Equal("position.has_sales", resultado.Motivo);

        // Y sigue ahí, con sus lotes: no se ha borrado a medias.
        Assert.True(await contexto.Positions.AnyAsync(p => p.Id == posicionId, Ct));
    }

    [Fact]
    public async Task BorrarlaSubeElEfectivoExactamenteLoQueCostaba()
    {
        using var bd = BaseDeDatosDePrueba.Nueva();

        var (posicionId, coste) = await SembrarAsync(bd, conVenta: false);

        using var contexto = bd.CrearContexto();
        var repositorio = new PositionRepository(contexto, bd.Usuario);
        var efectivo = new CashReader(contexto);

        var carteraId = await contexto.Positions
            .Where(p => p.Id == posicionId)
            .Select(p => p.PortfolioId)
            .SingleAsync(Ct);

        var antes = await efectivo.GetAsync(carteraId, Ct);
        Assert.NotNull(antes);

        var resultado = await repositorio.DeleteAsync(posicionId, Ct);

        Assert.True(resultado.Borrado);

        var despues = await efectivo.GetAsync(carteraId, Ct);
        Assert.NotNull(despues);

        // Ni más ni menos: exactamente el coste. Si subiera otra cosa, borrar una posición estaría moviendo el
        // saldo por un importe que nadie ha decidido.
        Assert.Equal(coste, despues.Balance - antes.Balance);

        // Y la posición ya no está, ni sus lotes.
        Assert.False(await contexto.Positions.AnyAsync(p => p.Id == posicionId, Ct));
    }

    [Fact]
    public async Task ElBorradoQuedaEnElLibroDeAuditoria()
    {
        // Quien borra es el administrador y la posición es de otra persona: es justo el caso que el libro tiene
        // que registrar. Sin esto, borrar por la API dejaría menos rastro que el `DELETE` por SQL que se hizo a
        // mano, que es lo que se venía a arreglar.
        using var bd = BaseDeDatosDePrueba.Nueva(esAdministrador: true);

        var (posicionId, _) = await SembrarAsync(bd, conVenta: false);

        using var contexto = bd.CrearContexto(auditar: true);

        // Quien actúa pasa a ser el administrador; el dueño de los datos es otra persona.
        bd.Usuario.Id = Guid.NewGuid();

        var repositorio = new PositionRepository(contexto, bd.Usuario);
        var resultado = await repositorio.DeleteAsync(posicionId, Ct);

        Assert.True(resultado.Borrado);

        var registro = await contexto.Set<AuditLog>()
            .Where(fila => fila.Action == AuditAction.Deleted && fila.EntityType == nameof(Position))
            .SingleOrDefaultAsync(Ct);

        Assert.NotNull(registro);
        Assert.Equal(posicionId, registro.EntityId);
        Assert.Equal(bd.Usuario.Id, registro.ActorUserId);
    }

    /// <summary>
    /// Deja una cartera con una apertura y una posición de 10 unidades a 100 (coste 1.000), con o sin una venta.
    /// </summary>
    /// <returns>La posición y lo que costaba.</returns>
    private static async Task<(Guid PosicionId, decimal Coste)> SembrarAsync(
        BaseDeDatosDePrueba bd,
        bool conVenta)
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
            Quantity.Of(10m), Money.Of(100m, Currency.Cad), new DateOnly(2026, 1, 10), DateAccuracy.Exact, null, Ahora);

        // La apertura es lo que permite calcular el saldo: sin ella, el efectivo no se puede dar.
        contexto.Movements.Add(Movement.Opening(
            cartera.Id, Money.Of(10_000m, Currency.Cad), new DateOnly(2026, 1, 10), null, Ahora));

        if (conVenta)
        {
            posicion.Sell(Quantity.Of(4m), Ahora);
            contexto.Movements.Add(Movement.Sale(
                cartera.Id, posicion.Id, Quantity.Of(4m),
                Money.Of(120m, Currency.Cad), Money.Of(100m, Currency.Cad),
                new DateOnly(2026, 6, 10), null, Ahora));
        }

        contexto.UserProfiles.Add(persona);
        contexto.Portfolios.Add(cartera);
        contexto.Instruments.Add(instrumento);
        contexto.Positions.Add(posicion);

        await contexto.SaveChangesAsync(Ct);

        // Lo que queda del lote: con una venta de 4 de 10, la posición guarda 6 unidades a 100 = 600.
        var coste = conVenta ? 600m : 1_000m;

        return (posicion.Id, coste);
    }
}
