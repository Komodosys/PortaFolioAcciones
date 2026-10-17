namespace PortaFolioAcciones.Domain.ValueObjects;

/// <summary>
/// Cada cuanto cambia el precio de un instrumento. Decide la politica de cache y si tiene sentido
/// pedir una cotizacion en un momento dado.
/// </summary>
public enum PricingFrequency
{
    /// <summary>
    /// Cotizacion con retraso durante la sesion (unos 15 minutos). Es lo que se puede conseguir sin
    /// pagar, y solo tiene sentido durante el horario del mercado.
    /// </summary>
    IntradayDelayed = 0,

    /// <summary>Cierre diario. Es la base del sistema: aunque exista el intradia, el cierre manda.</summary>
    EndOfDay = 1,

    /// <summary>Valor liquidativo diario, publicado despues del cierre y sin version intradia.</summary>
    DailyNav = 2,

    /// <summary>Lo introduce una persona. No se pide a nadie.</summary>
    Manual = 3,
}
