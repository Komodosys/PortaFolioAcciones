namespace PortaFolioAcciones.Domain.ValueObjects;

/// <summary>
/// Como se obtiene el precio de un instrumento. Es el eje que de verdad distingue unos activos de
/// otros, y lo que decide que proveedor de precios se usa.
/// </summary>
public enum ValuationMode
{
    /// <summary>
    /// Cotizacion de mercado: acciones y ETF. Hay precio durante la sesion y un cierre al terminar.
    /// </summary>
    Market = 0,

    /// <summary>
    /// Valor liquidativo: fondos mutuos. Se publica <b>una vez al dia, despues del cierre</b>, y no
    /// existe intradia. Un fondo puede tardar horas en publicar el valor del dia, asi que lo normal es
    /// leer el <b>ultimo cierre disponible</b> y decir de cuando es, en vez de esperar.
    /// </summary>
    Nav = 1,

    /// <summary>
    /// Precio introducido a mano: bonos individuales y fondos que no publican nada consultable. Lleva
    /// fecha y moneda obligatorias, para que nunca sea un numero sin origen.
    /// </summary>
    Manual = 2,
}
