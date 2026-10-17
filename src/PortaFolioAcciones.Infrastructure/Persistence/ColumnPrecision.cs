namespace PortaFolioAcciones.Infrastructure.Persistence;

/// <summary>
/// Precisiones de las columnas numericas en PostgreSQL.
///
/// Estan aqui, y no en el dominio, porque son una decision de <b>almacenamiento</b>, no de calculo: el
/// dominio trabaja con <c>decimal</c> sin redondear y solo se redondea al presentar. Lo que se fija
/// aqui es cuanto se guarda.
///
/// Son deliberadamente mas anchas que las que publica el broker (Questrade da 4 decimales en las
/// cantidades): apretarlas solo serviria para perder informacion el dia que aparezca un instrumento
/// que use mas.
/// </summary>
internal static class ColumnPrecision
{
    /// <summary>Precios y costes por unidad. Seis decimales: 168,859 o 165,5421.</summary>
    public const int Price = 6;

    /// <summary>Cantidades. Ocho decimales: los fondos usan fracciones largas (2393,827).</summary>
    public const int Quantity = 8;

    /// <summary>Tipos de cambio. Ocho decimales.</summary>
    public const int FxRate = 8;

    /// <summary>Importes. Cuatro decimales.</summary>
    public const int Amount = 4;

    /// <summary>Longitud de un codigo ISO de moneda.</summary>
    public const int CurrencyCode = 3;

    /// <summary>Longitud de un simbolo.</summary>
    public const int Ticker = 24;

    /// <summary>Longitud de los textos cortos, como un nombre.</summary>
    public const int Name = 200;

    /// <summary>Longitud de las notas libres.</summary>
    public const int Notes = 1000;

    /// <summary>Precision total de una columna numerica. Con 28 hay de sobra para cualquier importe.</summary>
    public const int NumericTotal = 28;
}
