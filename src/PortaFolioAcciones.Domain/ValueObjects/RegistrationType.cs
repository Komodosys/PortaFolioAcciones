namespace PortaFolioAcciones.Domain.ValueObjects;

/// <summary>
/// Como esta registrada una cartera a efectos fiscales.
///
/// Los tres valores son los que existen en Canada, y **son los mismos con dos nombres**: lo que en un
/// comprobante de Revenu Quebec se llama REER y CELI, en uno de la CRA se llama RRSP y TFSA. No son dos
/// cosas parecidas: son la misma, y por eso este enumerado tiene tres valores y no seis. Las etiquetas
/// cambian con el idioma —REER y CELI en espanol y frances, RRSP y TFSA en ingles— porque quien lee la
/// pantalla es quien usa esa palabra al hablar con su banco.
///
/// **Es informativo y no cambia como se calcula nada.** Igual que <see cref="AssetType"/>, sirve para
/// presentar y agrupar. En particular **no** sirve para vigilar los limites de aportacion: esos dependen de
/// anos, ingresos y retiros, y calcularlos mal seria peor que no calcularlos.
/// </summary>
public enum RegistrationType
{
    /// <summary>
    /// Plan de jubilacion registrado. REER (Quebec) o RRSP, que es lo mismo. Aportaciones deducibles y el
    /// retiro tributa.
    /// </summary>
    RegisteredRetirement = 0,

    /// <summary>
    /// Cuenta de ahorro libre de impuestos. CELI (Quebec) o TFSA, que es lo mismo. Se aporta con dinero ya
    /// tributado y el retiro no tributa.
    /// </summary>
    RegisteredSavings = 1,

    /// <summary>
    /// Cuenta no registrada, tambien llamada de margen o al contado. No tiene ventajas fiscales y todo lo
    /// que se gana tributa.
    /// </summary>
    NotRegistered = 2,
}
