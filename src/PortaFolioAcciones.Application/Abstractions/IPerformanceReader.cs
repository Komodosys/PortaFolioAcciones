namespace PortaFolioAcciones.Application.Abstractions;

using PortaFolioAcciones.Application.Portfolios;

/// <summary>
/// El informe de rendimiento: lo que vale cada cartera, lo que costó, lo que se puso y lo que se vendió.
///
/// Es la base del informe «aportado contra ganado» que se pidió. Se lee de dos sitios que ya existen: la foto
/// diaria —que trae el valor y el coste ya calculados y en la moneda base— y los movimientos.
///
/// <b>El aislamiento lo pone el filtro global de las carteras</b>, igual que en el listado: quien no administra
/// solo ve las suyas, y el administrador las ve todas. Por eso este método no recibe ningún filtro de persona:
/// pedir el informe «de otra persona» no es un parámetro, es algo que el administrador obtiene porque puede
/// verlas.
/// </summary>
public interface IPerformanceReader
{
    /// <summary>El informe de las carteras que puede ver quien llama.</summary>
    /// <param name="cancellationToken">Cancelación.</param>
    public Task<PerformanceReport> ReadAsync(CancellationToken cancellationToken = default);
}
