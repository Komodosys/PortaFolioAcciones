namespace PortaFolioAcciones.Application.Portfolios;

/// <summary>
/// Una posición dentro de la vista de cartera.
///
/// Se dan <b>los dos importes</b>, el de la moneda del instrumento y el convertido a la moneda base, y
/// no solo el convertido. El convertido depende de un tipo de cambio que cambia a diario, así que el de
/// la moneda original es el único que sigue significando lo mismo mañana.
/// </summary>
/// <param name="Id">Identificador de la posición.</param>
/// <param name="Symbol">Símbolo del instrumento.</param>
/// <param name="InstrumentName">Nombre del instrumento.</param>
/// <param name="Quantity">Cantidad.</param>
/// <param name="PriceAmount">Precio usado para valorar.</param>
/// <param name="PriceCurrency">Moneda de ese precio.</param>
/// <param name="PriceSource">
/// De dónde sale el precio: <c>manual</c>, <c>quote</c> (última cotización) o <c>close</c> (último
/// cierre). Se dice porque no es lo mismo un precio puesto a mano que uno de mercado.
/// </param>
/// <param name="PriceDate">Fecha del precio, si se conoce.</param>
/// <param name="MarketState">
/// Cómo está el mercado donde cotiza: <c>Open</c>, <c>Closed</c> o <c>NotATradingDay</c>. Sin esto, un
/// precio de hace dos días y uno de hace dos minutos se leen igual.
/// </param>
/// <param name="IsStale">
/// Si el precio es viejo: anterior a la última sesión de su bolsa, con la tolerancia que merezca su
/// frecuencia. Va aparte del estado del mercado porque son cosas distintas: el mercado puede estar cerrado
/// y el precio ser el de hoy, que es lo normal.
/// </param>
/// <param name="MarketValue">Valor de mercado, en la moneda del instrumento.</param>
/// <param name="Cost">Coste, en la moneda en la que se compró.</param>
/// <param name="BaseMarketValue">Valor de mercado convertido a la moneda base.</param>
/// <param name="BaseCost">Coste convertido a la moneda base.</param>
/// <param name="BaseGain">Ganancia en la moneda base.</param>
/// <param name="BaseReturn">Rentabilidad sobre el coste.</param>
/// <param name="Weight">Peso sobre el total de la cartera.</param>
/// <param name="LocalGain">Ganancia en la moneda del instrumento, o nula si no se compró en ella.</param>
/// <param name="LocalReturn">Rentabilidad en la moneda del instrumento.</param>
public sealed record PositionView(
    Guid Id,
    string Symbol,
    string InstrumentName,
    decimal Quantity,
    decimal PriceAmount,
    string PriceCurrency,
    string PriceSource,
    DateOnly? PriceDate,
    string MarketState,
    bool IsStale,

    /// <summary>
    /// El cierre de la sesión anterior, si se conoce. Nulo cuando no: <b>no se supone</b> que el precio de
    /// ayer fuera el de hoy.
    /// </summary>
    decimal? PreviousClose,

    /// <summary>Lo que ha cambiado hoy, en la moneda del precio. <b>Nulo no es cero</b>: es que no se sabe.</summary>
    decimal? DailyChange,

    /// <summary>Lo mismo, en la moneda base.</summary>
    decimal? BaseDailyChange,

    /// <summary>Lo que ha cambiado hoy, en tanto por ciento sobre el valor de ayer.</summary>
    decimal? DailyReturn,
    decimal MarketValue,
    decimal Cost,
    decimal BaseMarketValue,
    decimal BaseCost,
    decimal BaseGain,
    decimal? BaseReturn,
    decimal? Weight,
    decimal? LocalGain,
    decimal? LocalReturn);

/// <summary>
/// Una moneda para la que <b>no</b> se ha podido conseguir tipo de cambio.
/// </summary>
/// <param name="Currency">La moneda.</param>
/// <param name="Positions">Cuántas posiciones se han quedado sin valorar por su culpa.</param>
public sealed record MissingCurrency(string Currency, int Positions);

/// <summary>
/// Una posición que <b>no se ha podido valorar</b>, con lo que sí se sabe de ella.
///
/// Se devuelve aparte de <see cref="PositionView"/> y no con campos nulos dentro, y es a propósito:
/// <see cref="PositionView"/> es una posición <b>valorada</b>, y sus importes siempre tienen sentido. Si
/// se mezclaran, habría que comprobar en cada cuenta si el valor es nulo, y ese olvido es una resta que
/// sale mal.
///
/// Existe porque <b>no enseñarlas sería peor</b>: quien acaba de dar de alta una posición y no la ve en la
/// tabla piensa que no se ha guardado.
/// </summary>
/// <param name="Id">Identificador de la posición.</param>
/// <param name="Symbol">Símbolo del instrumento.</param>
/// <param name="InstrumentName">Nombre del instrumento.</param>
/// <param name="Quantity">Cantidad.</param>
/// <param name="Cost">Lo que costó, en la moneda en la que se compró.</param>
/// <param name="CostCurrency">Esa moneda.</param>
/// <param name="Reason">
/// Por qué no se ha valorado: <c>sin-precio</c> o <c>sin-tipo-de-cambio</c>. Se dice porque son dos
/// problemas distintos y se arreglan de forma distinta.
/// </param>
public sealed record UnpricedPosition(
    Guid Id,
    string Symbol,
    string InstrumentName,
    decimal Quantity,
    decimal Cost,
    string CostCurrency,
    string Reason);

/// <summary>
/// Un desdoblamiento que una posición <b>todavía no ha aplicado</b>.
/// </summary>
/// <param name="PositionId">La posición afectada.</param>
/// <param name="Symbol">Su símbolo, para poder decirlo sin otra consulta.</param>
/// <param name="Date">El día en que ocurrió.</param>
/// <param name="Ratio">Por cuánto se multiplica la cantidad.</param>
/// <param name="Quantity">La cantidad que tiene ahora, para poder enseñar en cuánto quedaría.</param>
/// <remarks>
/// Existe porque <b>un desdoblamiento sin aplicar da una cifra falsa</b>, y de las que asustan: con un
/// 5:1 la cantidad se queda como estaba y el precio se divide, así que la cartera se lee como si hubiera
/// perdido el 80 % de un día para otro. La aplicación no lo aplica sola —cambiar las cantidades de alguien
/// sin que lo pida es peor que avisar— pero <b>tampoco lo deja pasar en silencio</b>.
/// </remarks>
public sealed record PendingSplit(
    Guid PositionId,
    string Symbol,
    DateOnly Date,
    decimal Ratio,
    decimal Quantity);

/// <summary>
/// La vista de una cartera, <b>calculada en el servidor</b>.
///
/// Se calcula aquí y no en la pantalla a propósito: los totales tienen que salir iguales en el móvil,
/// en el ordenador y en un informe, y eso solo se consigue si hay un único sitio que sume.
/// </summary>
/// <param name="PortfolioId">Identificador.</param>
/// <param name="Name">Nombre.</param>
/// <param name="BaseCurrency">Moneda en la que están los totales.</param>
/// <param name="OwnerUserId">
/// De quién es. Va aquí, y no solo en el listado, porque el aviso de «estás tocando datos ajenos» tiene
/// que estar <b>en la pantalla donde se tocan</b>, no en la anterior: quien entra directo por un enlace no
/// pasa por el listado.
/// </param>
/// <param name="OwnerDisplayName">Cómo se llama esa persona, para poder decirlo.</param>
/// <param name="TotalValue">Valor total.</param>
/// <param name="TotalCost">Coste total.</param>
/// <param name="TotalGain">Ganancia total.</param>
/// <param name="TotalReturn">Rentabilidad total.</param>
/// <param name="TotalDailyChange">
/// Lo que ha cambiado la cartera <b>hoy</b>, en moneda base. Si <paramref name="IsDailyComplete"/> es
/// falso, es solo la parte que se sabe.
/// </param>
/// <param name="TotalDailyReturn">
/// Lo que ha cambiado hoy, en tanto por ciento. Nulo si falta algún cambio del día, porque el porcentaje
/// se calcula sobre el valor de ayer y ese valor también estaría mal.
/// </param>
/// <param name="IsDailyComplete">
/// Si se conoce el cambio del día de <b>todas</b> las posiciones valoradas. Es falso en cuanto a una sola
/// le falte el cierre anterior —una compra de hoy no tiene «ayer»—, y entonces el total del día queda a
/// medias y hay que decirlo.
/// </param>
/// <param name="IsComplete">
/// Si <b>todas</b> las posiciones se han podido valorar. Cuando es falso, los totales son
/// <b>parciales</b> y hay que decirlo: un total incompleto presentado como total es una cifra falsa.
/// </param>
/// <param name="ClosedPositions">Posiciones cerradas, que no entran en los totales.</param>
/// <param name="Positions">Las posiciones valoradas.</param>
/// <param name="PositionsWithoutPrice">Posiciones sin ningún precio utilizable.</param>
/// <param name="UnpricedPositions">
/// Las que no se han podido valorar, <b>con sus datos</b>, para poder enseñarlas en la tabla aunque sea
/// con guiones. No enseñarlas haría pensar que no se guardaron.
/// </param>
/// <param name="MissingCurrencies">Monedas sin tipo de cambio.</param>
/// <param name="PendingSplits">
/// Desdoblamientos que alguna posición no ha aplicado. Van aquí y no dentro de la posición valorada
/// porque no son un dato de la valoración: son un <b>aviso de que la valoración puede estar mal</b>.
/// </param>
/// <param name="RatesDate">Fecha de los tipos de cambio usados.</param>
public sealed record PortfolioView(
    Guid PortfolioId,
    string Name,
    string BaseCurrency,
    Guid OwnerUserId,
    string OwnerDisplayName,
    decimal TotalValue,
    decimal TotalCost,
    decimal TotalGain,
    decimal? TotalReturn,
    decimal TotalDailyChange,
    decimal? TotalDailyReturn,
    bool IsDailyComplete,
    bool IsComplete,
    int ClosedPositions,
    IReadOnlyList<PositionView> Positions,
    IReadOnlyList<Guid> PositionsWithoutPrice,
    IReadOnlyList<UnpricedPosition> UnpricedPositions,
    IReadOnlyList<MissingCurrency> MissingCurrencies,
    IReadOnlyList<PendingSplit> PendingSplits,
    DateOnly? RatesDate);
