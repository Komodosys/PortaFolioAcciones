namespace PortaFolioAcciones.Domain.ValueObjects;

/// <summary>
/// Cuanto de fiable es la fecha de compra de un lote.
///
/// Existe porque la fecha casi nunca se conoce con exactitud. En la carga inicial, el broker publica
/// el coste medio pero no siempre el detalle de cada compra, asi que hay que declarar una fecha y decir
/// <b>con que confianza</b>. Marcarlo es lo que evita que una suposicion acabe pareciendo un dato.
///
/// Hoy esta marca <b>no cambia ninguna cifra</b> que se muestre: el coste se convierte al cambio del
/// dia, asi que no depende de la fecha. Se guarda porque es lo unico que permitira calcular el efecto
/// divisa cuando se quiera, y entonces si importara.
/// </summary>
public enum DateAccuracy
{
    /// <summary>La fecha sale de un documento: un estado de cuenta o una operacion registrada.</summary>
    Exact = 0,

    /// <summary>La fecha se recuerda con margen, por ejemplo «en la primavera de 2024».</summary>
    Approximate = 1,

    /// <summary>
    /// Fecha puesta como referencia porque no se conoce. Sirve para que el campo no quede vacio, y
    /// significa exactamente eso: no es un dato.
    /// </summary>
    Reference = 2,
}
