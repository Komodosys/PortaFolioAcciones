using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using PortaFolioAcciones.Domain.ValueObjects;
using PortaFolioAcciones.Infrastructure.MarketData;
using PortaFolioAcciones.Infrastructure.Tests.Ayudas;

namespace PortaFolioAcciones.Infrastructure.Tests;

/// <summary>
/// La caché de tipos de cambio.
///
/// Lo que se prueba, además de no preguntar de más, es que se guarda <b>par a par</b>: una consulta que
/// pide dos monedas de las que una ya estaba solo pregunta por la que falta. Guardar la consulta entera
/// obligaría a volver a pedir lo que ya se sabía en cuanto apareciera una moneda nueva.
/// </summary>
public sealed class CachedFxRateProviderTests
{
    private static readonly DateOnly Hoy = new(2026, 10, 14);

    /// <summary>El token de la prueba, para que una prueba colgada se pueda cancelar.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (CachedFxRateProvider Cache, ProveedorFxFalso Fuente, RelojFalso Reloj) Montar()
    {
        var fuente = new ProveedorFxFalso();
        var reloj = new RelojFalso();
        var cache = new MemoryCache(new MemoryCacheOptions());

        var proveedor = new CachedFxRateProvider(
            fuente,
            cache,
            reloj,
            NullLogger<CachedFxRateProvider>.Instance);

        return (proveedor, fuente, reloj);
    }

    [Fact]
    public async Task La_segunda_consulta_dentro_del_dia_no_pregunta()
    {
        var (cache, fuente, _) = Montar();
        fuente.Respuesta.Add(FxRate.Of(Currency.Usd, Currency.Cad, 1.39m, Hoy));

        await cache.GetRatesAsync(Currency.Cad, [Currency.Usd], Ct);
        await cache.GetRatesAsync(Currency.Cad, [Currency.Usd], Ct);

        Assert.Equal(1, fuente.Llamadas);
    }

    [Fact]
    public async Task Pasado_el_dia_vuelve_a_preguntar()
    {
        var (cache, fuente, reloj) = Montar();
        fuente.Respuesta.Add(FxRate.Of(Currency.Usd, Currency.Cad, 1.39m, Hoy));

        await cache.GetRatesAsync(Currency.Cad, [Currency.Usd], Ct);

        reloj.Avanzar(CachedFxRateProvider.Ttl + TimeSpan.FromSeconds(1));
        await cache.GetRatesAsync(Currency.Cad, [Currency.Usd], Ct);

        Assert.Equal(2, fuente.Llamadas);
    }

    [Fact]
    public async Task Solo_se_pide_la_moneda_que_falta()
    {
        var (cache, fuente, _) = Montar();
        fuente.Respuesta.Add(FxRate.Of(Currency.Usd, Currency.Cad, 1.39m, Hoy));

        // Primero solo el dolar: se guarda.
        await cache.GetRatesAsync(Currency.Cad, [Currency.Usd], Ct);

        // Y ahora el dolar Y el euro. Al dolar no hay que volver a preguntar.
        fuente.Respuesta.Add(FxRate.Of(Currency.Eur, Currency.Cad, 1.47m, Hoy));
        var tasas = await cache.GetRatesAsync(Currency.Cad, [Currency.Usd, Currency.Eur], Ct);

        Assert.Equal(2, fuente.Llamadas);
        Assert.Equal([Currency.Eur], fuente.Peticiones[1]);
        Assert.Equal(2, tasas.Count);
    }

    [Fact]
    public async Task La_moneda_base_consigo_misma_no_se_pide()
    {
        var (cache, fuente, _) = Montar();

        var tasas = await cache.GetRatesAsync(Currency.Cad, [Currency.Cad], Ct);

        // Un CAD a CAD es uno, y eso no es inventarse nada: es la identidad. Pedirlo a la fuente seria
        // una peticion para que nos diga lo que ya sabemos.
        Assert.Empty(tasas);
        Assert.Equal(0, fuente.Llamadas);
    }

    [Fact]
    public async Task Lo_que_no_se_pudo_conseguir_no_se_guarda()
    {
        var (cache, fuente, _) = Montar();
        // La fuente no devuelve nada: no hay tipo de cambio para esa moneda.

        await cache.GetRatesAsync(Currency.Cad, [Currency.Usd], Ct);
        await cache.GetRatesAsync(Currency.Cad, [Currency.Usd], Ct);

        // Si se guardara el hueco, un corte momentaneo dejaria la moneda sin convertir todo un dia.
        Assert.Equal(2, fuente.Llamadas);
    }

    [Fact]
    public async Task Los_pares_repetidos_se_piden_una_sola_vez()
    {
        var (cache, fuente, _) = Montar();
        fuente.Respuesta.Add(FxRate.Of(Currency.Usd, Currency.Cad, 1.39m, Hoy));

        await cache.GetRatesAsync(Currency.Cad, [Currency.Usd, Currency.Usd, Currency.Usd], Ct);

        Assert.Equal(1, fuente.Llamadas);
        Assert.Equal([Currency.Usd], fuente.Peticiones[0]);
    }
}
