using Microsoft.Extensions.Logging.Abstractions;
using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Infrastructure.MarketData;
using PortaFolioAcciones.Infrastructure.Tests.Ayudas;

namespace PortaFolioAcciones.Infrastructure.Tests;

/// <summary>
/// La cadena de respaldo.
///
/// Lo que se prueba es que <b>nunca hay un hueco silencioso</b>: si una fuente falla se prueba la
/// siguiente, y si fallan todas se usa el último precio conocido <b>dicho como tal</b>. Dejar la posición
/// sin valorar rompe el total; poner un cero es mentir.
/// </summary>
public sealed class FallbackPriceProviderTests
{
    /// <summary>El token de la prueba, para que una prueba colgada se pueda cancelar.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static FallbackPriceProvider Montar(
        IReadOnlyList<ProveedorFalso> fuentes,
        UltimoPrecioFalso ultimo)
    {
        IReadOnlyList<IPriceProvider> soloLectura = [.. fuentes];

        return new FallbackPriceProvider(
            soloLectura,
            ultimo,
            NullLogger<FallbackPriceProvider>.Instance);
    }

    [Fact]
    public async Task Si_la_primera_fuente_responde_no_se_toca_la_segunda()
    {
        var primera = new ProveedorFalso { Respuesta = Datos.Cotizacion(100m) };
        var segunda = new ProveedorFalso { Respuesta = Datos.Cotizacion(999m) };
        var ultimo = new UltimoPrecioFalso();

        var cadena = Montar([primera, segunda], ultimo);
        var cotizacion = await cadena.GetQuoteAsync(Datos.Instrumento(), Ct);

        Assert.Equal(100m, cotizacion!.Price.Amount);
        Assert.Equal(1, primera.Llamadas);
        Assert.Equal(0, segunda.Llamadas);
        Assert.Equal(0, ultimo.Llamadas);
    }

    [Fact]
    public async Task Si_la_primera_falla_se_usa_la_segunda()
    {
        var primera = new ProveedorFalso { Respuesta = null };
        var segunda = new ProveedorFalso { Respuesta = Datos.Cotizacion(200m) };
        var ultimo = new UltimoPrecioFalso();

        var cadena = Montar([primera, segunda], ultimo);
        var cotizacion = await cadena.GetQuoteAsync(Datos.Instrumento(), Ct);

        Assert.Equal(200m, cotizacion!.Price.Amount);
        Assert.False(cotizacion.IsFallback);
        Assert.Equal(0, ultimo.Llamadas);
    }

    [Fact]
    public async Task Una_fuente_que_devuelve_un_respaldo_NO_cuenta_como_exito()
    {
        var primera = new ProveedorFalso { Respuesta = Datos.Cotizacion(100m, esRespaldo: true) };
        var segunda = new ProveedorFalso { Respuesta = Datos.Cotizacion(200m) };
        var ultimo = new UltimoPrecioFalso();

        var cadena = Montar([primera, segunda], ultimo);
        var cotizacion = await cadena.GetQuoteAsync(Datos.Instrumento(), Ct);

        // Si una fuente que arrastra su propio ultimo precio contara como exito, taparia a la siguiente
        // de la cadena y nadie se enteraria de que la primera lleva caida.
        Assert.Equal(200m, cotizacion!.Price.Amount);
        Assert.Equal(1, segunda.Llamadas);
    }

    [Fact]
    public async Task Si_todas_fallan_se_usa_el_ultimo_precio_conocido()
    {
        var primera = new ProveedorFalso { Respuesta = null };
        var segunda = new ProveedorFalso { Respuesta = null };
        var ultimo = new UltimoPrecioFalso { Respuesta = Datos.Cotizacion(150m, esRespaldo: true, origen: "ultimo-conocido") };

        var cadena = Montar([primera, segunda], ultimo);
        var cotizacion = await cadena.GetQuoteAsync(Datos.Instrumento(), Ct);

        Assert.Equal(150m, cotizacion!.Price.Amount);
        Assert.Equal(1, ultimo.Llamadas);
    }

    [Fact]
    public async Task Y_el_ultimo_precio_va_MARCADO_como_respaldo()
    {
        var fuente = new ProveedorFalso { Respuesta = null };
        var ultimo = new UltimoPrecioFalso { Respuesta = Datos.Cotizacion(150m, esRespaldo: true) };

        var cadena = Montar([fuente], ultimo);
        var cotizacion = await cadena.GetQuoteAsync(Datos.Instrumento(), Ct);

        // Es lo que permite a la pantalla decir «este precio es de hace tres dias». Presentado como
        // recien pedido seria peor que no mostrarlo.
        Assert.True(cotizacion!.IsFallback);
    }

    [Fact]
    public async Task Sin_fuente_ni_precio_guardado_devuelve_nulo_y_no_inventa()
    {
        var fuente = new ProveedorFalso { Respuesta = null };
        var ultimo = new UltimoPrecioFalso { Respuesta = null };

        var cadena = Montar([fuente], ultimo);
        var cotizacion = await cadena.GetQuoteAsync(Datos.Instrumento(), Ct);

        Assert.Null(cotizacion);
    }

    [Fact]
    public async Task Sin_fuentes_configuradas_se_va_directo_al_ultimo_precio()
    {
        var ultimo = new UltimoPrecioFalso { Respuesta = Datos.Cotizacion(150m, esRespaldo: true) };

        var cadena = Montar([], ultimo);
        var cotizacion = await cadena.GetQuoteAsync(Datos.Instrumento(), Ct);

        Assert.NotNull(cotizacion);
        Assert.Equal(1, ultimo.Llamadas);
    }
}
