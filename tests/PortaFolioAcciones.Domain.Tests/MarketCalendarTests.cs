using PortaFolioAcciones.Domain.Services;

namespace PortaFolioAcciones.Domain.Tests;

/// <summary>
/// El calendario de bolsa.
///
/// Lo que se prueba aqui no es «una funcion devuelve un valor», sino las <b>reglas que hacen que un
/// precio signifique algo</b>: si el mercado esta abierto, cuando cerro, y que dias no se negocia. Un
/// precio con la etiqueta de «de ahora» un sabado por la mañana es un precio con el que alguien tomara una
/// decision creyendo que esta fresco.
/// </summary>
public sealed class MarketCalendarTests
{
    private static DateTimeOffset EnUtc(int anio, int mes, int dia, int hora, int minuto = 0) =>
        new(anio, mes, dia, hora, minuto, 0, TimeSpan.Zero);

    [Fact]
    public void LosHusosHorariosSeResuelven()
    {
        // Si esto falla, el resto de la clase no prueba nada: sin huso no hay sesion, y `SessionFor`
        // devolveria nulo en todas partes. Se comprueba primero y por separado para que el fallo diga
        // exactamente cual es el problema.
        Assert.NotEmpty(MarketCalendar.KnownExchanges);

        Assert.NotNull(MarketCalendar.SessionFor("XNAS"));
        Assert.NotNull(MarketCalendar.SessionFor("XTSE"));
        Assert.NotNull(MarketCalendar.SessionFor("xnas"));
    }

    [Fact]
    public void UnaBolsaDesconocidaNoSeInventa()
    {
        Assert.Null(MarketCalendar.SessionFor("XXXX"));

        // Y ante la duda se supone que SI se negocia: suponer lo contrario dejaria los instrumentos de esa
        // bolsa sin actualizar nunca, y el fallo seria silencioso.
        Assert.True(MarketCalendar.IsTradingDay("XXXX", new DateOnly(2026, 7, 15)));
        Assert.Null(MarketCalendar.NextClose("XXXX", EnUtc(2026, 7, 15, 12, 0)));
    }

    [Fact]
    public void EnHorarioDeBolsaEstaAbierto()
    {
        // 15 de julio de 2026, 14:00 en Nueva York (EDT, UTC-4) = 18:00 UTC.
        Assert.Equal(MarketState.Open, MarketCalendar.StateOf("XNAS", EnUtc(2026, 7, 15, 18, 0)));

        // Y a las 9:30 en punto ya esta abierto: los limites cuentan.
        Assert.Equal(MarketState.Open, MarketCalendar.StateOf("XNAS", EnUtc(2026, 7, 15, 13, 30)));
    }

    [Fact]
    public void AntesDeAbrirYDespuesDeCerrarEstaCerrado()
    {
        // 12:00 UTC son las 8:00 en Nueva York: antes de abrir.
        Assert.Equal(MarketState.Closed, MarketCalendar.StateOf("XNAS", EnUtc(2026, 7, 15, 12, 0)));

        // 21:00 UTC son las 17:00: ya cerrado.
        Assert.Equal(MarketState.Closed, MarketCalendar.StateOf("XNAS", EnUtc(2026, 7, 15, 21, 0)));

        // A las 16:00 en punto ya NO se negocia: el cierre es el final del horario, no su ultimo minuto.
        Assert.Equal(MarketState.Closed, MarketCalendar.StateOf("XNAS", EnUtc(2026, 7, 15, 20, 0)));
    }

    [Fact]
    public void ElFinDeSemanaNoEsDiaDeBolsa()
    {
        // Sabado 18 de julio de 2026 y domingo 19.
        Assert.Equal(MarketState.NotATradingDay, MarketCalendar.StateOf("XNAS", EnUtc(2026, 7, 18, 18, 0)));
        Assert.Equal(MarketState.NotATradingDay, MarketCalendar.StateOf("XNAS", EnUtc(2026, 7, 19, 18, 0)));

        Assert.False(MarketCalendar.IsTradingDay("XNAS", new DateOnly(2026, 7, 18)));
    }

    [Fact]
    public void ElCambioDeHoraSeAplicaSolo()
    {
        // Esta es la razon de guardar los horarios en local y con su huso. Si se guardaran en UTC, el
        // cierre de Nueva York seria el mismo instante en enero y en julio... y no lo es.
        var enero = MarketCalendar.NextClose("XNAS", EnUtc(2026, 1, 15, 14, 0));
        var julio = MarketCalendar.NextClose("XNAS", EnUtc(2026, 7, 15, 14, 0));

        // En enero Nueva York esta en UTC-5: las 16:00 son las 21:00 UTC.
        Assert.Equal(EnUtc(2026, 1, 15, 21, 0), enero);

        // En julio esta en UTC-4: las 16:00 son las 20:00 UTC.
        Assert.Equal(EnUtc(2026, 7, 15, 20, 0), julio);
    }

    [Fact]
    public void ElProximoCierreEsElDeHoySiAunNoHaCerrado()
    {
        // 14:00 UTC del 15 de julio son las 10:00 en Nueva York: aun no ha cerrado.
        Assert.Equal(
            EnUtc(2026, 7, 15, 20, 0),
            MarketCalendar.NextClose("XNAS", EnUtc(2026, 7, 15, 14, 0)));
    }

    [Fact]
    public void YSiYaHaCerradoEsElDelSiguienteDiaDeBolsa()
    {
        // Viernes 17 de julio a las 22:00 UTC: ya cerro. El siguiente cierre es el lunes 20, porque el
        // sabado y el domingo no se negocia.
        Assert.Equal(
            EnUtc(2026, 7, 20, 20, 0),
            MarketCalendar.NextClose("XNAS", EnUtc(2026, 7, 17, 22, 0)));
    }

    [Fact]
    public void AnoNuevoNoEsDiaDeBolsa()
    {
        // El 1 de enero de 2026 cae en jueves.
        Assert.Equal(DayOfWeek.Thursday, new DateOnly(2026, 1, 1).DayOfWeek);
        Assert.False(MarketCalendar.IsTradingDay("XNAS", new DateOnly(2026, 1, 1)));
        Assert.False(MarketCalendar.IsTradingDay("XTSE", new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void AccionDeGraciasNoEsDiaDeBolsa()
    {
        // El cuarto jueves de noviembre de 2026 es el dia 26. Se calcula, no se lista, y por eso sigue
        // siendo correcto dentro de diez años.
        Assert.False(MarketCalendar.IsTradingDay("XNAS", new DateOnly(2026, 11, 26)));

        // El mismo dia en Canada es un dia de bolsa normal: su Accion de Gracias es en octubre.
        Assert.True(MarketCalendar.IsTradingDay("XTSE", new DateOnly(2026, 11, 26)));
    }

    [Fact]
    public void AccionDeGraciasCanadienseTampoco()
    {
        // El segundo lunes de octubre de 2026 es el dia 12.
        Assert.False(MarketCalendar.IsTradingDay("XTSE", new DateOnly(2026, 10, 12)));

        // Y ese dia en Estados Unidos es de bolsa: alli no se celebra.
        Assert.True(MarketCalendar.IsTradingDay("XNAS", new DateOnly(2026, 10, 12)));
    }

    [Fact]
    public void ViernesSantoNoEsDiaDeBolsa()
    {
        // La Pascua de 2026 es el 5 de abril, asi que el Viernes Santo es el 3.
        Assert.False(MarketCalendar.IsTradingDay("XNAS", new DateOnly(2026, 4, 3)));
        Assert.False(MarketCalendar.IsTradingDay("XTSE", new DateOnly(2026, 4, 3)));
    }

    [Fact]
    public void VictoriaDayNoEsDiaDeBolsaEnCanada()
    {
        // El 25 de mayo de 2026 cae en lunes, asi que el Victoria Day es el lunes ANTERIOR: el 18. Si la
        // regla estuviera escrita como «el lunes mas cercano al 25» saldria el 25, y ese dia el mercado
        // esta cerrado por otra cosa —no lo esta— y la aplicacion diria que si abre.
        Assert.Equal(DayOfWeek.Monday, new DateOnly(2026, 5, 25).DayOfWeek);
        Assert.False(MarketCalendar.IsTradingDay("XTSE", new DateOnly(2026, 5, 18)));

        // Y en Estados Unidos ese dia es de bolsa.
        Assert.True(MarketCalendar.IsTradingDay("XNAS", new DateOnly(2026, 5, 18)));
    }

    [Fact]
    public void ElUltimoAperturaEsLaDeHoySiYaAbrio()
    {
        var apertura = MarketCalendar.LastOpen("XNAS", EnUtc(2026, 7, 15, 18, 0));

        // Las 9:30 de Nueva York en julio (UTC-4) son las 13:30 UTC.
        Assert.Equal(EnUtc(2026, 7, 15, 13, 30), apertura);
    }

    [Fact]
    public void LosFestivosDeLosDosPaisesEstanCalculadosParaVariosAnios()
    {
        // Se comprueba en varios años y no solo en uno: una regla mal escrita puede acertar por casualidad
        // en el año que se probó.
        foreach (var anio in new[] { 2026, 2027, 2028, 2029, 2030 })
        {
            var primeroDeEnero = new DateOnly(anio, 1, 1);
            var navidad = new DateOnly(anio, 12, 25);

            Assert.False(MarketCalendar.IsTradingDay("XNAS", primeroDeEnero), $"año {anio}");
            Assert.False(MarketCalendar.IsTradingDay("XNAS", navidad), $"año {anio}");

            // El Dia de Accion de Gracias cae siempre en jueves.
            var accionDeGracias = EnesimoDiaDeLaSemana(anio, 11, DayOfWeek.Thursday, 4);

            Assert.Equal(DayOfWeek.Thursday, accionDeGracias.DayOfWeek);
            Assert.False(MarketCalendar.IsTradingDay("XNAS", accionDeGracias), $"año {anio}");
        }
    }

    /// <summary>La misma regla que usa el calendario, escrita aqui para no depender de lo que se prueba.</summary>
    private static DateOnly EnesimoDiaDeLaSemana(int anio, int mes, DayOfWeek dia, int n)
    {
        var primero = new DateOnly(anio, mes, 1);
        var salto = ((int)dia - (int)primero.DayOfWeek + 7) % 7;

        return primero.AddDays(salto + ((n - 1) * 7));
    }
}
