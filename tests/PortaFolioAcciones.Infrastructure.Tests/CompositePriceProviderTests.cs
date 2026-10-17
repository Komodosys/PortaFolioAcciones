using PortaFolioAcciones.Domain.ValueObjects;
using PortaFolioAcciones.Infrastructure.MarketData;
using PortaFolioAcciones.Infrastructure.Tests.Ayudas;

namespace PortaFolioAcciones.Infrastructure.Tests;

/// <summary>
/// El enrutado por modo de valoración.
///
/// Hoy solo hay una fuente detrás, así que lo que se prueba es sobre todo la <b>excepción</b>: que un
/// instrumento de precio manual no consulte a nadie. Esa es la regla que impide que un precio de mercado
/// pise el que puso una persona.
/// </summary>
public sealed class CompositePriceProviderTests
{
    /// <summary>El token de la prueba, para que una prueba colgada se pueda cancelar.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Un_instrumento_de_precio_manual_no_consulta_ninguna_fuente()
    {
        var fuente = new ProveedorFalso { Respuesta = Datos.Cotizacion() };
        var compuesto = new CompositePriceProvider(fuente);

        var cotizacion = await compuesto.GetQuoteAsync(
            Datos.Instrumento(modo: ValuationMode.Manual, frecuencia: PricingFrequency.Manual),
            Ct);

        // Devolver el precio de mercado seria peor que no devolver nada: sobrescribiria una decision de
        // la persona con un dato ajeno, y ademas sin que se enterara.
        Assert.Null(cotizacion);
        Assert.Equal(0, fuente.Llamadas);
    }

    [Fact]
    public async Task Un_instrumento_de_mercado_si_consulta_la_fuente()
    {
        var fuente = new ProveedorFalso { Respuesta = Datos.Cotizacion() };
        var compuesto = new CompositePriceProvider(fuente);

        var cotizacion = await compuesto.GetQuoteAsync(Datos.Instrumento(modo: ValuationMode.Market), Ct);

        Assert.NotNull(cotizacion);
        Assert.Equal(1, fuente.Llamadas);
    }

    [Fact]
    public async Task Un_fondo_se_valora_consultando_la_misma_fuente()
    {
        var fuente = new ProveedorFalso { Respuesta = Datos.Cotizacion(moneda: Currency.Cad) };
        var compuesto = new CompositePriceProvider(fuente);

        var cotizacion = await compuesto.GetQuoteAsync(
            Datos.Instrumento(clave: "0P0001K2XD.TO", modo: ValuationMode.Nav, frecuencia: PricingFrequency.DailyNav),
            Ct);

        // La diferencia entre una accion y un fondo no esta en donde se pregunta, sino en que se lee de
        // la respuesta, y de eso se encarga el adaptador con la frecuencia.
        Assert.NotNull(cotizacion);
        Assert.Equal(1, fuente.Llamadas);
    }
}
