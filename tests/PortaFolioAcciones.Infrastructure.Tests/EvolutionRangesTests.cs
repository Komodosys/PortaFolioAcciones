using PortaFolioAcciones.Application.Portfolios;

namespace PortaFolioAcciones.Infrastructure.Tests;

/// <summary>
/// Los periodos del gráfico de evolución.
///
/// Se prueban aparte porque son <b>reglas de calendario</b>, y un calendario tiene esquinas: el 1 de enero, el 29
/// de febrero, el día en que se pide. Equivocarse aquí no rompe nada: dibuja un periodo que no es el que se pidió.
/// </summary>
public sealed class EvolutionRangesTests
{
    [Fact]
    public void LoQueVaDeAnoEmpiezaElUnoDeEnero()
    {
        // Da igual el dia en que se mire: el periodo es el mismo desde el 1 de enero de ese ano.
        Assert.Equal(new DateOnly(2026, 1, 1), EvolutionRanges.Desde(EvolutionRange.YearToDate, new DateOnly(2026, 10, 18)));
        Assert.Equal(new DateOnly(2026, 1, 1), EvolutionRanges.Desde(EvolutionRange.YearToDate, new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void UnAnoAtrasSeCuentaDesdeElMismoDia()
    {
        Assert.Equal(new DateOnly(2025, 10, 18), EvolutionRanges.Desde(EvolutionRange.OneYear, new DateOnly(2026, 10, 18)));

        // Y en un 29 de febrero, que es el caso que rompe las cuentas de dias a mano.
        Assert.Equal(new DateOnly(2023, 2, 28), EvolutionRanges.Desde(EvolutionRange.OneYear, new DateOnly(2024, 2, 29)));
    }

    [Fact]
    public void TodoEsNuloYNoUnaFechaMuyAntigua()
    {
        // Nulo significa «desde donde haya datos». Pedir 2019 cuando los precios empiezan en 2021 dejaria un tramo
        // vacio al principio y la linea empezaria plana a cero, que no es un dato.
        Assert.Null(EvolutionRanges.Desde(EvolutionRange.All, new DateOnly(2026, 10, 18)));
    }

    [Theory]
    [InlineData("5d", EvolutionRange.FiveDays)]
    [InlineData("15d", EvolutionRange.FifteenDays)]
    [InlineData("1m", EvolutionRange.OneMonth)]
    [InlineData("1a", EvolutionRange.OneYear)]
    [InlineData("ytd", EvolutionRange.YearToDate)]
    [InlineData("todo", EvolutionRange.All)]
    [InlineData("YTD", EvolutionRange.YearToDate)]
    public void LosNombresSeEntiendenSinDistinguirMayusculas(string nombre, EvolutionRange esperado)
    {
        Assert.Equal(esperado, EvolutionRanges.Parse(nombre));
    }

    [Fact]
    public void UnPeriodoQueNoSeConoceEsTodoYNoUnError()
    {
        // Un nombre raro en la direccion no es un fallo del programa ni de quien lo escribio a mano: se dibuja
        // todo, que es lo que se ve por defecto.
        Assert.Equal(EvolutionRange.All, EvolutionRanges.Parse("2semanas"));
        Assert.Equal(EvolutionRange.All, EvolutionRanges.Parse(null));
    }

    [Fact]
    public void LosPeriodosCortosPidenMenosPuntosQueLosLargos()
    {
        // Sesenta puntos sobre cinco dias son mas puntos que dias: el mismo dia repetido. Y sesenta sobre un ano
        // deja dias sin mirar.
        Assert.True(EvolutionRanges.Puntos(EvolutionRange.FiveDays) <= 6);
        Assert.True(EvolutionRanges.Puntos(EvolutionRange.OneYear) >= 50);
    }
}
