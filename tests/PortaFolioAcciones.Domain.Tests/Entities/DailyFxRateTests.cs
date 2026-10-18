using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Domain.Tests.Entities;

/// <summary>
/// El tipo de cambio guardado.
///
/// Lo que se prueba es lo que hace falta para que sirva de algo: que conserva <b>la fecha</b> —sin ella no
/// se puede auditar de dónde salió un número— y que <b>su clave es el par y el día</b>, que es lo que impide
/// que el mismo par quede dos veces el mismo día con valores distintos.
/// </summary>
public sealed class DailyFxRateTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private static FxRate Tipo(decimal valor = 1.4161m, int dia = 16) =>
        FxRate.Of(Currency.Usd, Currency.Cad, valor, new DateOnly(2026, 10, dia));

    [Fact]
    public void Guarda_el_par_el_valor_y_la_fecha()
    {
        var guardado = DailyFxRate.FromValue(Tipo(), Ahora);

        Assert.Equal("USD", guardado.From.Code);
        Assert.Equal("CAD", guardado.To.Code);
        Assert.Equal(1.4161m, guardado.Rate);
        Assert.Equal(new DateOnly(2026, 10, 16), guardado.RateDate);
    }

    [Fact]
    public void La_clave_es_el_par_y_el_dia()
    {
        var guardado = DailyFxRate.FromValue(Tipo(), Ahora);

        Assert.Equal(("USD", "CAD", new DateOnly(2026, 10, 16)), guardado.Key);
    }

    [Fact]
    public void Dos_dias_distintos_son_claves_distintas()
    {
        // Es lo que permite guardar la serie: el mismo par, un valor por día.
        var uno = DailyFxRate.FromValue(Tipo(dia: 16), Ahora);
        var otro = DailyFxRate.FromValue(Tipo(dia: 17), Ahora);

        Assert.NotEqual(uno.Key, otro.Key);
    }

    [Fact]
    public void Actualizar_cambia_el_valor_y_no_la_fecha()
    {
        // El día al que corresponde el tipo es un hecho de la fuente y no se toca; lo que se corrige es el
        // número. Si se pudiera cambiar la fecha, se podría «arreglar» un histórico moviéndolo de día.
        var guardado = DailyFxRate.FromValue(Tipo(), Ahora);

        guardado.Update(1.4200m, Ahora.AddHours(1));

        Assert.Equal(1.4200m, guardado.Rate);
        Assert.Equal(new DateOnly(2026, 10, 16), guardado.RateDate);
    }
}
