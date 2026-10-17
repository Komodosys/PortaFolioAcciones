using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Infrastructure.Tests.Ayudas;

/// <summary>
/// Una fuente de tipos de cambio de mentira que cuenta llamadas y <b>guarda qué monedas se le pidieron</b>.
///
/// Guardar las monedas es lo que permite comprobar lo importante: que una consulta con dos monedas, de
/// las cuales una ya estaba guardada, solo pregunta por la que falta. Con solo el recuento no se podría
/// distinguir «pidió las dos» de «pidió la que faltaba».
/// </summary>
internal sealed class ProveedorFxFalso : IFxRateProvider
{
    public int Llamadas { get; private set; }

    public List<FxRate> Respuesta { get; } = [];

    public List<IReadOnlyList<Currency>> Peticiones { get; } = [];

    public Task<IReadOnlyList<FxRate>> GetRatesAsync(
        Currency baseCurrency,
        IEnumerable<Currency> currencies,
        CancellationToken cancellationToken = default)
    {
        Llamadas++;

        var pedidas = currencies.ToList();
        Peticiones.Add(pedidas);

        // Se devuelven SOLO las monedas que se pidieron, como hace la fuente de verdad. Devolver todo lo
        // que se tenga guardado no seria un doble fiel: escondería justo el fallo que esta prueba busca,
        // porque una consulta por una moneda parecería haber traido dos.
        return Task.FromResult<IReadOnlyList<FxRate>>(
            [.. Respuesta.Where(tasa => pedidas.Contains(tasa.From))]);
    }
}
