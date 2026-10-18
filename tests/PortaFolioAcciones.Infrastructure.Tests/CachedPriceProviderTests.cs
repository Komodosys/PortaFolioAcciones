using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using PortaFolioAcciones.Domain.ValueObjects;
using PortaFolioAcciones.Infrastructure.MarketData;
using PortaFolioAcciones.Infrastructure.Tests.Ayudas;

namespace PortaFolioAcciones.Infrastructure.Tests;

/// <summary>
/// La caché de precios. Lo que se prueba es que <b>no se pregunta de más</b> y que <b>no se sirve un
/// precio viejo como si fuera de ahora</b>, que son las dos formas de equivocarse que tiene una caché.
/// </summary>
public sealed class CachedPriceProviderTests
{
    /// <summary>El token de la prueba, para que una prueba colgada se pueda cancelar.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (CachedPriceProvider Cache, ProveedorFalso Fuente, RelojFalso Reloj, RefrescoDePrecios Refrescos) Montar()
    {
        var fuente = new ProveedorFalso { Respuesta = Datos.Cotizacion() };
        var reloj = new RelojFalso();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var refrescos = new RefrescoDePrecios(reloj);

        var proveedor = new CachedPriceProvider(
            fuente,
            cache,
            reloj,
            NullLogger<CachedPriceProvider>.Instance,
            refrescos);

        return (proveedor, fuente, reloj, refrescos);
    }

    [Fact]
    public async Task Invalidar_la_cache_hace_que_se_vuelva_a_preguntar()
    {
        // Es lo que hace que el boton de «actualizar precios» signifique algo: sin esto, pulsarlo devolveria
        // el mismo precio guardado y pareceria que no hace nada.
        var (cache, fuente, _, refrescos) = Montar();
        var instrumento = Datos.Instrumento();

        await cache.GetQuoteAsync(instrumento, Ct);
        Assert.Equal(1, fuente.Llamadas);

        refrescos.InvalidarCache();

        await cache.GetQuoteAsync(instrumento, Ct);
        Assert.Equal(2, fuente.Llamadas);
    }

    [Fact]
    public async Task La_segunda_llamada_dentro_del_plazo_no_pregunta_a_la_fuente()
    {
        var (cache, fuente, _, _) = Montar();
        var instrumento = Datos.Instrumento();

        await cache.GetQuoteAsync(instrumento, Ct);
        await cache.GetQuoteAsync(instrumento, Ct);

        // Si esto fallara, cada consulta de la pantalla gastaria una peticion a la fuente: con una
        // cartera de veinte posiciones, veinte peticiones por refresco.
        Assert.Equal(1, fuente.Llamadas);
    }

    [Fact]
    public async Task Lo_que_devuelve_es_la_cotizacion_de_la_fuente()
    {
        var (cache, _, _, _) = Montar();
        var instrumento = Datos.Instrumento();

        var primera = await cache.GetQuoteAsync(instrumento, Ct);
        var segunda = await cache.GetQuoteAsync(instrumento, Ct);

        Assert.Same(primera, segunda);
        Assert.Equal(100m, segunda!.Price.Amount);
    }

    [Fact]
    public async Task Pasado_el_plazo_de_intradia_vuelve_a_preguntar()
    {
        var (cache, fuente, reloj, _) = Montar();
        var instrumento = Datos.Instrumento(frecuencia: PricingFrequency.IntradayDelayed);

        await cache.GetQuoteAsync(instrumento, Ct);

        reloj.Avanzar(CachedPriceProvider.TtlIntradia + TimeSpan.FromSeconds(1));
        await cache.GetQuoteAsync(instrumento, Ct);

        Assert.Equal(2, fuente.Llamadas);
    }

    [Fact]
    public async Task Un_precio_de_cierre_aguanta_mas_que_uno_de_intradia()
    {
        var (cache, fuente, reloj, _) = Montar();
        var instrumento = Datos.Instrumento(frecuencia: PricingFrequency.EndOfDay);

        await cache.GetQuoteAsync(instrumento, Ct);

        // Quince minutos: suficiente para que un precio de intradía haya caducado, y no para un cierre
        // diario, que no cambia hasta el día siguiente.
        reloj.Avanzar(CachedPriceProvider.TtlIntradia + TimeSpan.FromMinutes(1));
        await cache.GetQuoteAsync(instrumento, Ct);

        Assert.Equal(1, fuente.Llamadas);
    }

    [Fact]
    public async Task Un_valor_liquidativo_aguanta_un_dia()
    {
        var (cache, fuente, reloj, _) = Montar();
        var instrumento = Datos.Instrumento(frecuencia: PricingFrequency.DailyNav);

        await cache.GetQuoteAsync(instrumento, Ct);

        reloj.Avanzar(TimeSpan.FromHours(23));
        await cache.GetQuoteAsync(instrumento, Ct);
        Assert.Equal(1, fuente.Llamadas);

        reloj.Avanzar(TimeSpan.FromHours(2));
        await cache.GetQuoteAsync(instrumento, Ct);
        Assert.Equal(2, fuente.Llamadas);
    }

    [Fact]
    public async Task Un_fallo_de_la_fuente_NO_se_guarda()
    {
        var (cache, fuente, _, _) = Montar();
        fuente.Respuesta = null;
        var instrumento = Datos.Instrumento();

        var primera = await cache.GetQuoteAsync(instrumento, Ct);
        var segunda = await cache.GetQuoteAsync(instrumento, Ct);

        // Guardar el hueco dejaria el precio en blanco toda la ventana aunque la fuente se recuperara al
        // minuto siguiente. Se vuelve a preguntar.
        Assert.Null(primera);
        Assert.Null(segunda);
        Assert.Equal(2, fuente.Llamadas);
    }

    [Fact]
    public void El_precio_manual_no_tiene_plazo_porque_no_se_pide_a_nadie()
    {
        Assert.Equal(TimeSpan.Zero, CachedPriceProvider.TtlFor(PricingFrequency.Manual));
    }

    [Theory]
    [InlineData(PricingFrequency.IntradayDelayed, 15)]
    [InlineData(PricingFrequency.EndOfDay, 720)]
    [InlineData(PricingFrequency.DailyNav, 1440)]
    public void Cada_frecuencia_tiene_su_plazo(PricingFrequency frecuencia, int minutos)
    {
        Assert.Equal(TimeSpan.FromMinutes(minutos), CachedPriceProvider.TtlFor(frecuencia));
    }

    [Fact]
    public async Task Un_respaldo_NO_se_guarda_en_la_cache()
    {
        var (cache, fuente, _, _) = Montar();
        fuente.Respuesta = Datos.Cotizacion(150m, esRespaldo: true);
        var instrumento = Datos.Instrumento();

        await cache.GetQuoteAsync(instrumento, Ct);
        await cache.GetQuoteAsync(instrumento, Ct);

        // Guardarlo prolongaria la averia: si la fuente se recuperara al minuto siguiente, el ultimo
        // precio conocido seguiria sirviendose hasta que caducara la entrada, y en un cierre diario eso
        // son doce horas. De no machacar una fuente caida se encarga el interruptor de circuito.
        Assert.Equal(2, fuente.Llamadas);
    }

    [Fact]
    public async Task Dos_instrumentos_distintos_no_comparten_entrada()
    {
        var (cache, fuente, _, _) = Montar();

        await cache.GetQuoteAsync(Datos.Instrumento("AAPL"), Ct);
        await cache.GetQuoteAsync(Datos.Instrumento("MSFT"), Ct);

        Assert.Equal(2, fuente.Llamadas);
    }
}
