using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Application.Abstractions;

/// <summary>
/// Consigue tipos de cambio para poder sumar importes de monedas distintas.
///
/// Es un <b>puerto</b>: la aplicación pide «¿cuánto vale un dólar en dólares canadienses hoy?» y no sabe
/// de dónde sale la respuesta.
///
/// <b>Nunca se inventa un tipo de cambio.</b> Si no se puede conseguir, se devuelve una lista sin ese
/// par, y quien pregunte tiene que decidir qué hacer: aquí no existe el «uno a uno» por comodidad,
/// porque un 1:1 falso convierte una cartera de 77 000 en una de 55 000 sin que salte nada.
/// </summary>
public interface IFxRateProvider
{
    /// <summary>
    /// Tipos de cambio desde las monedas indicadas hasta la moneda base.
    /// </summary>
    /// <param name="baseCurrency">La moneda a la que se convierte.</param>
    /// <param name="currencies">Las monedas que hay que convertir. Se ignoran la base y los repetidos.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    /// <returns>
    /// Los pares que se han podido conseguir. Puede faltar alguno, y faltar es una respuesta válida.
    /// </returns>
    public Task<IReadOnlyList<FxRate>> GetRatesAsync(
        Currency baseCurrency,
        IEnumerable<Currency> currencies,
        CancellationToken cancellationToken = default);
}
