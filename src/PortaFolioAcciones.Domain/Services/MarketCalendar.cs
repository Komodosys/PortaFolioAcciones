namespace PortaFolioAcciones.Domain.Services;

/// <summary>El pais de la bolsa, que es lo que decide que festivos cierra.</summary>
public enum MarketCountry
{
    /// <summary>Estados Unidos.</summary>
    UnitedStates,

    /// <summary>Canada.</summary>
    Canada,
}

/// <summary>
/// Una sesion de bolsa: en que franja horaria abre y en que huso.
/// </summary>
/// <param name="Exchange">Codigo MIC de la bolsa: <c>XNAS</c>, <c>XTSE</c>…</param>
/// <param name="Zone">El huso horario de la bolsa. Los horarios son <b>locales</b>.</param>
/// <param name="Open">Hora local de apertura.</param>
/// <param name="Close">Hora local de cierre.</param>
/// <param name="Country">El pais, que decide los festivos.</param>
public sealed record MarketSession(
    string Exchange,
    TimeZoneInfo Zone,
    TimeOnly Open,
    TimeOnly Close,
    MarketCountry Country);

/// <summary>Como esta el mercado en un momento dado.</summary>
public enum MarketState
{
    /// <summary>Abierto.</summary>
    Open,

    /// <summary>Cerrado, pero hoy es dia de bolsa: esta fuera de horario.</summary>
    Closed,

    /// <summary>Hoy no se negocia: fin de semana o festivo.</summary>
    NotATradingDay,
}

/// <summary>
/// El calendario de las bolsas: cuando abren, cuando cierran y que dias no se negocia.
///
/// Existe porque <b>hay preguntas que no se pueden responder mirando el reloj</b>: si el mercado esta
/// abierto, cuando cerro, o si un precio de hoy es de hoy. Sin esto, la aplicacion enseñaria precios con
/// la etiqueta de «de ahora» un sabado por la mañana, y quien los mire tomara decisiones con ellos.
///
/// Dos decisiones que hacen que sirva de algo:
///
/// <list type="number">
///   <item><description><b>Los horarios son locales y con su huso, no en UTC.</b> El horario de Nueva York
///   es 9:30–16:00 <b>de Nueva York</b>, y esa ciudad cambia de hora dos veces al año. Guardarlo en UTC
///   daria una hora de error la mitad del año.</description></item>
///   <item><description><b>Los festivos se calculan, no se listan.</b> Una lista hay que rehacerla cada
///   año, y el dia que se olvide la aplicacion dira que el mercado esta abierto el dia de Accion de
///   Gracias. Las reglas —«el tercer lunes de enero»— no caducan.</description></item>
/// </list>
/// </summary>
public static class MarketCalendar
{
    private static readonly Dictionary<string, MarketSession> Sesiones = CrearSesiones();

    /// <summary>Las bolsas que se conocen.</summary>
    public static IReadOnlyCollection<string> KnownExchanges => Sesiones.Keys;

    /// <summary>La sesion de una bolsa, o nula si no se conoce.</summary>
    /// <param name="exchange">Codigo MIC.</param>
    public static MarketSession? SessionFor(string? exchange) =>
        exchange is not null && Sesiones.TryGetValue(exchange.Trim().ToUpperInvariant(), out var sesion)
            ? sesion
            : null;

    /// <summary>
    /// Si ese dia se negocia en esa bolsa.
    ///
    /// Un dia que no se conoce devuelve <c>true</c>: ante la duda, se supone que si se negocia. Suponer lo
    /// contrario haria que un instrumento de una bolsa desconocida no se actualizara <b>nunca</b>, y el
    /// fallo seria silencioso.
    /// </summary>
    /// <param name="exchange">Codigo MIC.</param>
    /// <param name="dia">El dia, en la fecha local de la bolsa.</param>
    public static bool IsTradingDay(string? exchange, DateOnly dia)
    {
        if (SessionFor(exchange) is not { } sesion)
        {
            return true;
        }

        // Sabado y domingo no se negocia en ninguna de las bolsas que se usan aqui.
        if (dia.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return false;
        }

        return !EsFestivo(sesion, dia);
    }

    /// <summary>Como esta el mercado en ese instante.</summary>
    /// <param name="exchange">Codigo MIC.</param>
    /// <param name="ahora">El instante.</param>
    public static MarketState StateOf(string? exchange, DateTimeOffset ahora)
    {
        if (SessionFor(exchange) is not { } sesion)
        {
            // Una bolsa desconocida: no se puede decir nada, asi que se dice lo menos rotundo. Y no se
            // dice «abierto» a la ligera: se dice que esta abierto si es un dia laborable en su huso.
            return MarketState.Closed;
        }

        var local = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(ahora, sesion.Zone).DateTime);
        var dia = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(ahora, sesion.Zone).DateTime);

        if (!IsTradingDay(sesion.Exchange, dia))
        {
            return MarketState.NotATradingDay;
        }

        return local >= sesion.Open && local < sesion.Close ? MarketState.Open : MarketState.Closed;
    }

    /// <summary>
    /// El proximo cierre a partir de ese instante, o nulo si no se conoce la bolsa.
    ///
    /// Es lo que necesita el job para saber <b>cuando</b> hay que recoger los precios del dia.
    /// </summary>
    /// <param name="exchange">Codigo MIC.</param>
    /// <param name="ahora">El instante.</param>
    public static DateTimeOffset? NextClose(string? exchange, DateTimeOffset ahora)
    {
        if (SessionFor(exchange) is not { } sesion)
        {
            return null;
        }

        // Se mira hacia adelante como mucho una semana: siempre hay un dia de bolsa en ese plazo, porque
        // ningun festivo dura siete dias seguidos.
        for (var salto = 0; salto <= 7; salto++)
        {
            var diaLocal = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(ahora, sesion.Zone).DateTime).AddDays(salto);

            if (!IsTradingDay(sesion.Exchange, diaLocal))
            {
                continue;
            }

            var cierre = CerrarEn(sesion, diaLocal);

            if (cierre > ahora)
            {
                return cierre;
            }
        }

        return null;
    }

    /// <summary>Cuando abrio el mercado por ultima vez, antes de ese instante.</summary>
    /// <param name="exchange">Codigo MIC.</param>
    /// <param name="ahora">El instante.</param>
    public static DateTimeOffset? LastOpen(string? exchange, DateTimeOffset ahora)
    {
        if (SessionFor(exchange) is not { } sesion)
        {
            return null;
        }

        for (var salto = 0; salto <= 7; salto++)
        {
            var diaLocal = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(ahora, sesion.Zone).DateTime).AddDays(-salto);

            if (!IsTradingDay(sesion.Exchange, diaLocal))
            {
                continue;
            }

            var apertura = EnHuso(sesion, diaLocal, sesion.Open);

            if (apertura <= ahora)
            {
                return apertura;
            }
        }

        return null;
    }

    /// <summary>
    /// Cuando cerro el mercado por ultima vez, antes de ese instante.
    ///
    /// Es lo que necesita el trabajo diario para saber si la sesion de hoy <b>ya ha terminado</b>. No vale
    /// con preguntar si esta cerrado: a las siete de la mañana tambien esta cerrado, y recoger precios
    /// entonces daria los de ayer como si fueran los de hoy.
    /// </summary>
    /// <param name="exchange">Codigo MIC.</param>
    /// <param name="ahora">El instante.</param>
    public static DateTimeOffset? LastClose(string? exchange, DateTimeOffset ahora)
    {
        if (SessionFor(exchange) is not { } sesion)
        {
            return null;
        }

        for (var salto = 0; salto <= 7; salto++)
        {
            var diaLocal = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(ahora, sesion.Zone).DateTime).AddDays(-salto);

            if (!IsTradingDay(sesion.Exchange, diaLocal))
            {
                continue;
            }

            var cierre = EnHuso(sesion, diaLocal, sesion.Close);

            if (cierre <= ahora)
            {
                return cierre;
            }
        }

        return null;
    }

    /// <summary>
    /// La fecha local, en esa bolsa, del ultimo cierre.
    ///
    /// Es la referencia contra la que se compara un precio para decir si esta viejo: un precio deberia ser
    /// del dia de la ultima sesion, y si es anterior, algo ha fallado al recogerlo.
    /// </summary>
    /// <param name="exchange">Codigo MIC.</param>
    /// <param name="ahora">El instante.</param>
    public static DateOnly? LastCloseDate(string? exchange, DateTimeOffset ahora)
    {
        if (SessionFor(exchange) is not { } sesion ||
            LastClose(exchange, ahora) is not { } cierre)
        {
            return null;
        }

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(cierre, sesion.Zone).DateTime);
    }

    /// <summary>
    /// Si la sesion de HOY ya ha terminado en esa bolsa.
    ///
    /// Un dia que no se negocia cuenta como terminado: no hay nada que esperar.
    /// </summary>
    /// <param name="exchange">Codigo MIC.</param>
    /// <param name="ahora">El instante.</param>
    public static bool HasClosedToday(string? exchange, DateTimeOffset ahora)
    {
        if (SessionFor(exchange) is not { } sesion)
        {
            // Una bolsa desconocida no puede bloquear el trabajo diario para siempre. Se da por terminada:
            // es preferible recoger de mas que no recoger nunca.
            return true;
        }

        var local = TimeZoneInfo.ConvertTime(ahora, sesion.Zone);
        var diaLocal = DateOnly.FromDateTime(local.DateTime);

        if (!IsTradingDay(sesion.Exchange, diaLocal))
        {
            return true;
        }

        return LastClose(sesion.Exchange, ahora) is { } cierre
            && DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(cierre, sesion.Zone).DateTime) == diaLocal;
    }

    /// <summary>
    /// El cierre de ese dia, pasado a un instante absoluto.
    ///
    /// Se construye con `DateTimeOffset` y el huso de la bolsa, asi que <b>el cambio de hora se aplica
    /// solo</b>: en marzo el cierre cae a una hora distinta en UTC que en febrero, y eso es correcto.
    /// </summary>
    private static DateTimeOffset CerrarEn(MarketSession sesion, DateOnly dia) =>
        EnHuso(sesion, dia, sesion.Close);

    private static DateTimeOffset EnHuso(MarketSession sesion, DateOnly dia, TimeOnly hora)
    {
        var local = dia.ToDateTime(hora);

        // `IsInvalidTime` es la hora que no existe el dia que se adelanta el reloj, e `IsAmbiguousTime` la
        // que ocurre dos veces el dia que se atrasa. Ninguna de las dos toca a los horarios de bolsa —que
        // caen de dia—, pero si alguien cambia un horario a las 2:30 de la madrugada, esto evita que
        // reviente.
        var desplazamiento = sesion.Zone.IsInvalidTime(local) || sesion.Zone.IsAmbiguousTime(local)
            ? sesion.Zone.BaseUtcOffset
            : sesion.Zone.GetUtcOffset(local);

        return new DateTimeOffset(local, desplazamiento);
    }

    private static Dictionary<string, MarketSession> CrearSesiones()
    {
        var sesiones = new Dictionary<string, MarketSession>(StringComparer.OrdinalIgnoreCase);

        // Se busca el huso por su nombre **IANA** (`America/New_York`) y no por el de Windows
        // (`Eastern Standard Time`). A partir de .NET 6 los nombres IANA funcionan en los dos sistemas, y
        // son los que no cambian al desplegar: los de Windows no existen en el contenedor Linux.
        Anadir(sesiones, "XNAS", "America/New_York", new TimeOnly(9, 30), new TimeOnly(16, 0), MarketCountry.UnitedStates);
        Anadir(sesiones, "XNYS", "America/New_York", new TimeOnly(9, 30), new TimeOnly(16, 0), MarketCountry.UnitedStates);
        Anadir(sesiones, "ARCX", "America/New_York", new TimeOnly(9, 30), new TimeOnly(16, 0), MarketCountry.UnitedStates);
        Anadir(sesiones, "BATS", "America/New_York", new TimeOnly(9, 30), new TimeOnly(16, 0), MarketCountry.UnitedStates);
        Anadir(sesiones, "XTSE", "America/Toronto", new TimeOnly(9, 30), new TimeOnly(16, 0), MarketCountry.Canada);
        Anadir(sesiones, "XTSX", "America/Toronto", new TimeOnly(9, 30), new TimeOnly(16, 0), MarketCountry.Canada);

        return sesiones;
    }

    private static void Anadir(
        Dictionary<string, MarketSession> sesiones,
        string exchange,
        string huso,
        TimeOnly apertura,
        TimeOnly cierre,
        MarketCountry pais)
    {
        if (TimeZoneInfo.TryFindSystemTimeZoneById(huso, out var zona))
        {
            sesiones[exchange] = new MarketSession(exchange, zona, apertura, cierre, pais);
        }

        // Si el huso no se encuentra, la bolsa NO se añade en vez de añadirse con un huso inventado. Una
        // sesion con el huso equivocado diria que el mercado esta abierto a las tres de la mañana, y eso
        // es peor que no saberlo.
    }

    /// <summary>
    /// Si ese dia es festivo de bolsa en ese pais.
    ///
    /// <b>Se calcula, no se lista.</b> Las reglas de «el tercer lunes de enero» no caducan; una lista de
    /// fechas hay que rehacerla cada año, y el dia que se olvide la aplicacion dara por bueno un precio de
    /// un dia en el que no se negocio.
    ///
    /// <b>Lo que no cubre</b>, y conviene saberlo: los cierres por luto nacional o por una tormenta, que
    /// son imprevisibles y se anuncian con poco tiempo. Cuando ocurren, el precio que se recoge es el del
    /// dia anterior y la aplicacion lo dice, porque lleva su fecha.
    /// </summary>
    /// <param name="sesion">La bolsa.</param>
    /// <param name="dia">El dia, en fecha local de la bolsa.</param>
    private static bool EsFestivo(MarketSession sesion, DateOnly dia)
    {
        var anio = dia.Year;

        // Año Nuevo. Si cae en domingo, se traslada al lunes; si cae en sabado, no se traslada a un dia de
        // bolsa y el mercado ya esta cerrado por fin de semana.
        if (EsEl(dia, 1, 1) || (dia.DayOfWeek == DayOfWeek.Monday && EsEl(dia, 1, 2)))
        {
            return true;
        }

        // Viernes Santo: dos dias antes del Domingo de Resurreccion. Cierra en los dos paises.
        if (dia == ViernesSanto(anio))
        {
            return true;
        }

        // Navidad, con el mismo criterio que Año Nuevo.
        if (EsEl(dia, 12, 25) || (dia.DayOfWeek == DayOfWeek.Monday && EsEl(dia, 12, 26)))
        {
            return true;
        }

        return sesion.Country == MarketCountry.UnitedStates
            ? EsFestivoDeEstadosUnidos(dia, anio)
            : EsFestivoDeCanada(dia);
    }

    private static bool EsFestivoDeEstadosUnidos(DateOnly dia, int anio)
    {
        return dia == EnesimoDiaDeLaSemana(anio, 1, DayOfWeek.Monday, 3)   // Martin Luther King
            || dia == EnesimoDiaDeLaSemana(anio, 2, DayOfWeek.Monday, 3)   // Presidentes
            || dia == UltimoDiaDeLaSemana(anio, 5, DayOfWeek.Monday)       // Memorial
            || EsEl(dia, 6, 19)                                            // Juneteenth
            || (dia.DayOfWeek == DayOfWeek.Friday && EsEl(dia, 7, 3))      // 4 de julio en sabado
            || EsEl(dia, 7, 4)
            || dia == EnesimoDiaDeLaSemana(anio, 9, DayOfWeek.Monday, 1)   // Trabajo
            || dia == EnesimoDiaDeLaSemana(anio, 11, DayOfWeek.Thursday, 4); // Accion de Gracias
    }

    private static bool EsFestivoDeCanada(DateOnly dia)
    {
        return dia == LunesAntesDel(dia.Year, 24, 5)                       // Victoria Day
            || EsEl(dia, 7, 1)                                             // Canada Day
            || (dia.DayOfWeek == DayOfWeek.Monday && EsEl(dia, 7, 2))
            || dia == EnesimoDiaDeLaSemana(dia.Year, 9, DayOfWeek.Monday, 1)   // Trabajo
            || dia == EnesimoDiaDeLaSemana(dia.Year, 10, DayOfWeek.Monday, 2)  // Accion de Gracias
            || EsEl(dia, 12, 26);                                          // Boxing Day
    }

    private static bool EsEl(DateOnly dia, int mes, int numeroDeDia) =>
        dia.Month == mes && dia.Day == numeroDeDia;

    /// <summary>El n-esimo dia de la semana de un mes. El 3.º lunes de enero, por ejemplo.</summary>
    private static DateOnly EnesimoDiaDeLaSemana(int anio, int mes, DayOfWeek dia, int n)
    {
        var primero = new DateOnly(anio, mes, 1);
        var salto = ((int)dia - (int)primero.DayOfWeek + 7) % 7;

        return primero.AddDays(salto + ((n - 1) * 7));
    }

    /// <summary>El ultimo dia de la semana de un mes.</summary>
    private static DateOnly UltimoDiaDeLaSemana(int anio, int mes, DayOfWeek dia)
    {
        var ultimo = new DateOnly(anio, mes, DateTime.DaysInMonth(anio, mes));
        var salto = ((int)ultimo.DayOfWeek - (int)dia + 7) % 7;

        return ultimo.AddDays(-salto);
    }

    /// <summary>
    /// El lunes anterior a una fecha, o ese mismo dia si ya es lunes.
    ///
    /// Para el Victoria Day, que es **el lunes anterior al 25 de mayo**.
    /// </summary>
    private static DateOnly LunesAntesDel(int anio, int dia, int mes)
    {
        var fecha = new DateOnly(anio, mes, dia);

        return fecha.AddDays(-(((int)fecha.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7));
    }

    /// <summary>El Viernes Santo, por el algoritmo gregoriano de Gauss.</summary>
    private static DateOnly ViernesSanto(int anio)
    {
        var a = anio % 19;
        var b = anio / 100;
        var c = anio % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = ((19 * a) + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + (2 * e) + (2 * i) - h - k) % 7;
        var m = (a + (11 * h) + (22 * l)) / 451;
        var mes = (h + l - (7 * m) + 114) / 31;
        var dia = ((h + l - (7 * m) + 114) % 31) + 1;

        return new DateOnly(anio, mes, dia).AddDays(-2);
    }
}
