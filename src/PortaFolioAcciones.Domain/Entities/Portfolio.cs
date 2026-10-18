using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Domain.Entities;

/// <summary>
/// Una cartera: el conjunto de posiciones de una persona, con su moneda de visualizacion.
///
/// La moneda base es <b>opcional</b>: si es nula, hereda la del usuario. Asi se puede tener una moneda
/// por defecto para toda la cuenta y, si algun dia hace falta, una distinta para una cartera concreta,
/// sin cargar con esa complejidad desde el principio.
/// </summary>
public sealed class Portfolio
{
    /// <summary>Longitud maxima del nombre.</summary>
    public const int MaxNameLength = 100;

    private Portfolio(
        Guid id,
        Guid ownerUserId,
        string name,
        RegistrationType registrationType,
        DateTimeOffset now)
    {
        Id = id;
        OwnerUserId = ownerUserId;
        Name = name;
        RegistrationType = registrationType;
        SortOrder = 0;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
    }

    /// <summary>Constructor sin parametros para el mapeo de la base de datos.</summary>
    private Portfolio()
    {
        Name = string.Empty;
    }

    public Guid Id { get; private set; }

    /// <summary>Persona a la que pertenece. Es la base del aislamiento entre usuarios.</summary>
    public Guid OwnerUserId { get; private set; }

    public string Name { get; private set; }

    /// <summary>
    /// Como esta registrada a efectos fiscales: REER, CELI o no registrada.
    ///
    /// Es <b>obligatoria y sin valor por defecto</b>, y no es rigidez: un valor por defecto comodo —«no
    /// registrada»— etiquetaria en silencio una cuenta de jubilacion como no registrada, y una etiqueta
    /// equivocada es peor que una que falta, porque nadie la revisa.
    /// </summary>
    public RegistrationType RegistrationType { get; private set; }

    /// <summary>Moneda de visualizacion propia, o nula para heredar la del usuario.</summary>
    public Currency? BaseCurrency { get; private set; }

    /// <summary>Orden en que se muestran las carteras.</summary>
    public int SortOrder { get; private set; }

    /// <summary>Dice si la cartera esta archivada. No se borra: sus posiciones son historia.</summary>
    public bool IsArchived { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>Crea una cartera vacia.</summary>
    /// <param name="ownerUserId">Persona a la que pertenece.</param>
    /// <param name="name">Nombre visible.</param>
    /// <param name="registrationType">Como esta registrada a efectos fiscales.</param>
    /// <param name="now">El instante.</param>
    public static Portfolio Create(
        Guid ownerUserId,
        string name,
        RegistrationType registrationType,
        DateTimeOffset now)
    {
        if (ownerUserId == Guid.Empty)
        {
            throw new ArgumentException("Una cartera tiene que pertenecer a alguien.", nameof(ownerUserId));
        }

        ValidarNombre(name);
        return new Portfolio(Guid.NewGuid(), ownerUserId, name.Trim(), registrationType, now);
    }

    /// <summary>
    /// Moneda en la que se presentan los totales: la propia si la tiene, y si no, la de la persona.
    ///
    /// Se resuelve aqui, en el dominio, y no en cada pantalla: si cada sitio decidiera por su cuenta,
    /// dos pantallas podrian mostrar la misma cartera en monedas distintas.
    /// </summary>
    public Currency ResolveBaseCurrency(Currency userBaseCurrency)
    {
        ArgumentNullException.ThrowIfNull(userBaseCurrency);
        return BaseCurrency ?? userBaseCurrency;
    }

    /// <summary>Fija una moneda propia para esta cartera.</summary>
    public void SetBaseCurrency(Currency currency, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(currency);
        BaseCurrency = currency;
        Touch(now);
    }

    /// <summary>Vuelve a heredar la moneda de la persona.</summary>
    public void InheritBaseCurrency(DateTimeOffset now)
    {
        BaseCurrency = null;
        Touch(now);
    }

    /// <summary>Cambia el nombre.</summary>
    public void Rename(string name, DateTimeOffset now)
    {
        ValidarNombre(name);
        Name = name.Trim();
        Touch(now);
    }

    /// <summary>Cambia como esta registrada a efectos fiscales.</summary>
    public void SetRegistrationType(RegistrationType registrationType, DateTimeOffset now)
    {
        RegistrationType = registrationType;
        Touch(now);
    }

    /// <summary>Cambia el orden en que se muestra.</summary>
    public void Reorder(int sortOrder, DateTimeOffset now)
    {
        SortOrder = sortOrder;
        Touch(now);
    }

    /// <summary>Archiva la cartera.</summary>
    public void Archive(DateTimeOffset now)
    {
        IsArchived = true;
        Touch(now);
    }

    /// <summary>Desarchiva la cartera.</summary>
    public void Unarchive(DateTimeOffset now)
    {
        IsArchived = false;
        Touch(now);
    }

    private static void ValidarNombre(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (name.Trim().Length > MaxNameLength)
        {
            throw new ArgumentException(
                $"El nombre de una cartera no puede pasar de {MaxNameLength} caracteres.", nameof(name));
        }
    }

    private void Touch(DateTimeOffset now) => UpdatedAtUtc = now;
}
