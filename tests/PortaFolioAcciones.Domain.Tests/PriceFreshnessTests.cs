using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.Services;
using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Domain.Tests;

/// <summary>
/// Si un precio se puede seguir considerando de ahora.
///
/// Lo que se prueba aqui es una idea que recorre todo el proyecto: <b>un precio viejo presentado como
/// actual es peor que un hueco</b>, porque el hueco se ve y el precio viejo se cree. Y tambien lo
/// contrario: un aviso que sale cuando no toca <b>enseña a ignorarlos</b>, y entonces deja de servir el
/// dia que importa.
/// </summary>
public sealed class PriceFreshnessTests
{
    /// <summary>Viernes 16 de octubre de 2026, despues del cierre de Nueva York (20:00 UTC en octubre).</summary>
    private static readonly DateTimeOffset DespuesDelCierre = new(2026, 10, 16, 22, 0, 0, TimeSpan.Zero);

    private static Instrument Instrumento(
        PricingFrequency frecuencia = PricingFrequency.EndOfDay,
        string bolsa = "XNAS")
    {
        return Instrument.Create(
            Ticker.From("PRUEBA"),
            "Instrumento de prueba",
            AssetType.Equity,
            Currency.Usd,
            bolsa,
            ValuationMode.Market,
            frecuencia,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void UnPrecioDeLaUltimaSesionNoEsViejo()
    {
        // El caso normal: el cierre de hoy. Si esto diera «viejo», el aviso saldria todos los dias y
        // dejaria de mirarse.
        Assert.False(PriceFreshness.IsStale(Instrumento(), new DateOnly(2026, 10, 16), DespuesDelCierre));
    }

    [Fact]
    public void UnPrecioAnteriorALaUltimaSesionSiEsViejo()
    {
        // El mercado cerro el dia 16 y el precio es del 15: falta la ultima sesion, asi que algo ha fallado
        // al recogerlo. Esto es lo que hay que enseñar.
        Assert.True(PriceFreshness.IsStale(Instrumento(), new DateOnly(2026, 10, 15), DespuesDelCierre));
        Assert.True(PriceFreshness.IsStale(Instrumento(), new DateOnly(2026, 10, 9), DespuesDelCierre));
    }

    [Fact]
    public void UnFondoTieneMargenPorqueSuValorLiquidativoLlegaConRetraso()
    {
        var fondo = Instrumento(PricingFrequency.DailyNav);

        // Un fondo publica su valor liquidativo con uno o dos dias de retraso, y eso es como funciona, no
        // un fallo. Avisar de un dia de retraso seria avisar de que el mundo funciona.
        Assert.False(PriceFreshness.IsStale(fondo, new DateOnly(2026, 10, 15), DespuesDelCierre));
        Assert.False(PriceFreshness.IsStale(fondo, new DateOnly(2026, 10, 13), DespuesDelCierre));

        // Pero cuatro dias ya no son un retraso normal.
        Assert.True(PriceFreshness.IsStale(fondo, new DateOnly(2026, 10, 12), DespuesDelCierre));
    }

    [Fact]
    public void UnPrecioAManoTambienEnvejece()
    {
        // Que sea una decision de quien lo puso no lo hace menos viejo. Saberlo es justo lo que hace falta
        // para acordarse de revisarlo.
        Assert.True(PriceFreshness.IsStale(Instrumento(PricingFrequency.Manual), new DateOnly(2026, 10, 15), DespuesDelCierre));
    }

    [Fact]
    public void SinFechaNoSeAvisa()
    {
        // Ante la duda, no se avisa: un aviso que sale cuando no toca enseña a ignorarlos.
        Assert.False(PriceFreshness.IsStale(Instrumento(), null, DespuesDelCierre));
    }

    [Fact]
    public void ConUnaBolsaDesconocidaNoSeAvisa()
    {
        // De una bolsa que no se conoce no se sabe cuando cerro, asi que no se puede decir que el precio
        // sea viejo. Suponerlo daria avisos permanentes en instrumentos que quiza esten perfectos.
        Assert.False(PriceFreshness.IsStale(
            Instrumento(bolsa: "XXXX"),
            new DateOnly(2020, 1, 1),
            DespuesDelCierre));
    }

    [Fact]
    public void LaToleranciaEsLaQueSeDice()
    {
        Assert.Equal(0, PriceFreshness.ToleranceInDays(PricingFrequency.IntradayDelayed));
        Assert.Equal(0, PriceFreshness.ToleranceInDays(PricingFrequency.EndOfDay));
        Assert.Equal(3, PriceFreshness.ToleranceInDays(PricingFrequency.DailyNav));
    }
}
