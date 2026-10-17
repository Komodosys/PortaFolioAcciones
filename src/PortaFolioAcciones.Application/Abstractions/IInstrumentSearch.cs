using PortaFolioAcciones.Application.Instruments;

namespace PortaFolioAcciones.Application.Abstractions;

/// <summary>
/// Busca instrumentos <b>fuera</b>, en una fuente de mercado.
///
/// Es un <b>puerto</b>, y esa es la decisión importante: la aplicación pide «búscame 'Apple'» y no sabe
/// si detrás hay una llamada HTTP, una caché o un fichero. La Fase 5 (resiliencia, reintentos, cuota)
/// podrá cambiar el adaptador sin tocar nada de aquí.
/// </summary>
public interface IInstrumentSearch
{
    /// <summary>Busca por nombre o por símbolo.</summary>
    /// <param name="query">Lo que escribe la persona.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    public Task<IReadOnlyList<InstrumentMatch>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default);
}
