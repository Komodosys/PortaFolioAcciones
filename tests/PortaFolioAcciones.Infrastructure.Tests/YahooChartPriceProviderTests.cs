using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using PortaFolioAcciones.Domain.ValueObjects;
using PortaFolioAcciones.Infrastructure.MarketData;
using PortaFolioAcciones.Infrastructure.Tests.Ayudas;

namespace PortaFolioAcciones.Infrastructure.Tests;

/// <summary>
/// El analisis de la respuesta de la fuente de mercado.
///
/// Aqui estan las dos reglas que, si se rompen, dan un precio <b>que parece bueno y no lo es</b> —el peor
/// tipo de fallo, porque nadie lo mira—:
///
/// <list type="number">
///   <item><description>Se lee <c>close</c> y <b>nunca</b> <c>adjclose</c>. El cierre ajustado ya descuenta
///   los desdoblamientos; usarlo en una posicion que ya aplico el split seria contar el efecto dos
///   veces.</description></item>
///   <item><description>Se lee el <b>ultimo cierre no nulo</b>. Los fondos no publican todos los dias y en
///   un festivo la casilla viene vacia.</description></item>
/// </list>
///
/// Se prueba con una respuesta preparada y no contra la fuente real: la real cambia cada dia, y una prueba
/// que dependa de ella fallaria sola un martes cualquiera.
/// </summary>
public sealed class YahooChartPriceProviderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class FuenteFalsa(string json, HttpStatusCode codigo = HttpStatusCode.OK) : HttpMessageHandler
    {
        public int Llamadas { get; private set; }

        public string? UltimaRuta { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Llamadas++;
            UltimaRuta = request.RequestUri?.ToString();

            return Task.FromResult(new HttpResponseMessage(codigo)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }

    private static (YahooChartPriceProvider Proveedor, FuenteFalsa Fuente) Montar(
        string json,
        HttpStatusCode codigo = HttpStatusCode.OK)
    {
        var fuente = new FuenteFalsa(json, codigo);
        var http = new HttpClient(fuente) { BaseAddress = new Uri("https://ejemplo/") };

        return (new YahooChartPriceProvider(http, NullLogger<YahooChartPriceProvider>.Instance), fuente);
    }

    /// <summary>Una respuesta con cierres y, aparte, un ajustado deliberadamente distinto.</summary>
    private static string Respuesta(string cierres, string ajustados, string eventos = "")
    {
        var marcas = string.Join(',', Enumerable.Range(0, cierres.Split(',').Length).Select(i => 1760000000 + (i * 86400)));

        return $$"""
            {
              "chart": {
                "result": [
                  {
                    "meta": { "currency": "USD", "symbol": "PRUEBA" },
                    "timestamp": [{{marcas}}],
                    "indicators": {
                      "quote": [ { "close": [{{cierres}}] } ],
                      "adjclose": [ { "adjclose": [{{ajustados}}] } ]
                    }{{eventos}}
                  }
                ],
                "error": null
              }
            }
            """;
    }

    [Fact]
    public async Task LeeElCierreYNoElAjustado()
    {
        // Los dos valores son distintos a proposito: el ajustado vale 1 y el cierre vale 12,75. Si alguien
        // leyera el campo equivocado, la prueba lo dice con un numero, no con una opinión.
        var (proveedor, _) = Montar(Respuesta("10.5, 11.0, 12.75", "1.0, 1.0, 1.0"));

        var cotizacion = await proveedor.GetQuoteAsync(Datos.Instrumento(), Ct);

        Assert.NotNull(cotizacion);
        Assert.Equal(12.75m, cotizacion.Price.Amount);
    }

    [Fact]
    public async Task TomaElUltimoCierreNoNulo()
    {
        // La ultima casilla viene vacia, como en un festivo o en un fondo que no publica todos los dias.
        // Quedarse con la ultima casilla sin mirar daria un precio nulo justo cuando mas importa leerlo.
        var (proveedor, _) = Montar(Respuesta("10.5, 12.75, null", "1.0, 1.0, 1.0"));

        var cotizacion = await proveedor.GetQuoteAsync(Datos.Instrumento(), Ct);

        Assert.NotNull(cotizacion);
        Assert.Equal(12.75m, cotizacion.Price.Amount);
    }

    [Fact]
    public async Task LaFechaEsLaDelCierreQueSeUsaYNoLaDeHoy()
    {
        var (proveedor, _) = Montar(Respuesta("10.5, 12.75, null", "1.0, 1.0, 1.0"));

        var cotizacion = await proveedor.GetQuoteAsync(Datos.Instrumento(), Ct);

        // El 12,75 es el SEGUNDO cierre, asi que la fecha tiene que ser la del segundo instante. Poner la
        // de hoy seria fechar un precio en un dia en el que no se negocio.
        Assert.NotNull(cotizacion);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1760000000 + 86400), cotizacion.AsOfUtc);
        Assert.Equal(DateOnly.FromDateTime(cotizacion.AsOfUtc.UtcDateTime), cotizacion.PriceDate);
    }

    [Fact]
    public async Task SinNingunCierreDevuelveNulo()
    {
        // Un hueco, dicho como hueco. Inventarse un cero seria valorar la posicion entera a cero.
        var (proveedor, _) = Montar(Respuesta("null, null", "1.0, 1.0"));

        Assert.Null(await proveedor.GetQuoteAsync(Datos.Instrumento(), Ct));
    }

    [Fact]
    public async Task UnSimboloQueNoExisteDevuelveNulo()
    {
        var (proveedor, _) = Montar("{}", HttpStatusCode.NotFound);

        // Nunca un precio inventado.
        Assert.Null(await proveedor.GetQuoteAsync(Datos.Instrumento(), Ct));
    }

    [Fact]
    public async Task UnInstrumentoSinClaveDeFuenteNoPideNada()
    {
        var (proveedor, fuente) = Montar(Respuesta("10.5", "1.0"));

        var instrumento = Datos.Instrumento();

        // Se quita la clave de fuente: sin ella no se sabe a quien preguntar.
        typeof(PortaFolioAcciones.Domain.Entities.Instrument)
            .GetProperty(nameof(PortaFolioAcciones.Domain.Entities.Instrument.ProviderKey))!
            .SetValue(instrumento, null);

        Assert.Null(await proveedor.GetQuoteAsync(instrumento, Ct));

        // Y NO se llama a la fuente. Pedir un precio sin saber de que seria preguntar al azar.
        Assert.Equal(0, fuente.Llamadas);
    }

    [Fact]
    public async Task LeeLosDesdoblamientosQueDeclaraLaFuente()
    {
        // Un 10:1 como el de NFLX. Sin leerlo, la posicion se leeria como una caida del 90 % de un dia
        // para otro.
        const string Eventos = """
            ,"events": {
              "splits": {
                "1760000000": { "date": 1760000000, "numerator": 10, "denominator": 1, "splitRatio": "10:1" }
              }
            }
            """;

        var (proveedor, _) = Montar(Respuesta("10.5, 12.75", "1.0, 1.0", Eventos));

        var cotizacion = await proveedor.GetQuoteAsync(Datos.Instrumento(), Ct);

        Assert.NotNull(cotizacion);

        var division = Assert.Single(cotizacion.Splits);
        Assert.Equal(10m, division.Ratio);
        Assert.Equal("10:1", division.Label);
    }

    [Fact]
    public async Task PideLosEventosYElRangoDiario()
    {
        var (proveedor, fuente) = Montar(Respuesta("10.5", "1.0"));

        await proveedor.GetQuoteAsync(Datos.Instrumento(), Ct);

        // Los eventos hay que PEDIRLOS: si no se piden, la fuente no los manda y no hay nada que detectar.
        Assert.Contains("events=div%2Csplits", fuente.UltimaRuta, StringComparison.Ordinal);
        Assert.Contains("interval=1d", fuente.UltimaRuta, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SiLaFuenteNoDiceLaMonedaSeUsaLaDelInstrumento()
    {
        var (proveedor, _) = Montar("""
            {
              "chart": {
                "result": [
                  {
                    "meta": { "symbol": "PRUEBA" },
                    "timestamp": [1760000000],
                    "indicators": { "quote": [ { "close": [10.5] } ] }
                  }
                ]
              }
            }
            """);

        var cotizacion = await proveedor.GetQuoteAsync(Datos.Instrumento(moneda: Currency.Cad), Ct);

        Assert.NotNull(cotizacion);
        Assert.Equal(Currency.Cad, cotizacion.Price.Currency);
    }
}
