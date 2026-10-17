using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Domain.Entities;

/// <summary>
/// Un instrumento: lo que se puede poseer.
///
/// Una sola entidad con un tipo, en vez de una jerarquia de clases. La diversidad real entre una
/// accion, un ETF y un fondo **no esta en el comportamiento** —todos son cantidad por precio—, sino en
/// <b>como se consigue su precio</b>, y eso se modela con <see cref="ValuationMode"/>.
///
/// <b>Tres identificadores distintos, y ninguno se deduce de los otros:</b>
/// <list type="bullet">
///   <item><description><see cref="Symbol"/>: el que se muestra.</description></item>
///   <item><description><see cref="ProviderKey"/>: con el que lo conoce la fuente de precios. El mismo
///   fondo puede llamarse distinto en cada fuente.</description></item>
///   <item><description><see cref="BrokerCode"/>: el codigo del broker. En Canada, los fondos de
///   Fundserv se identifican asi, <b>no tienen ISIN</b>, y ese codigo <b>no existe en la fuente de
///   precios</b>: buscarlo devuelve cero resultados. La equivalencia se resuelve a mano una vez por
///   instrumento, confirmando que el valor liquidativo coincide con el del estado de cuenta.</description></item>
/// </list>
/// </summary>
public sealed class Instrument
{
    /// <summary>Longitud maxima de un nombre.</summary>
    public const int MaxNameLength = 200;

    /// <summary>Longitud de un ISIN: dos letras de pais, nueve caracteres y un digito de control.</summary>
    public const int IsinLength = 12;

    private Instrument(
        Guid id,
        Ticker symbol,
        string name,
        AssetType assetType,
        Currency quoteCurrency,
        string exchange,
        ValuationMode valuationMode,
        PricingFrequency pricingFrequency,
        DateTimeOffset now)
    {
        Id = id;
        Symbol = symbol;
        Name = name;
        AssetType = assetType;
        QuoteCurrency = quoteCurrency;
        Exchange = exchange;
        ValuationMode = valuationMode;
        PricingFrequency = pricingFrequency;
        IsActive = true;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
    }

    /// <summary>Constructor sin parametros para el mapeo de la base de datos.</summary>
    private Instrument()
    {
        Symbol = Ticker.From("X");
        Name = string.Empty;
        QuoteCurrency = Currency.Cad;
        Exchange = string.Empty;
    }

    public Guid Id { get; private set; }

    /// <summary>Simbolo de pantalla.</summary>
    public Ticker Symbol { get; private set; }

    /// <summary>Nombre legible.</summary>
    public string Name { get; private set; }

    /// <summary>Que clase de instrumento es. Informativo: no cambia ningun calculo.</summary>
    public AssetType AssetType { get; private set; }

    /// <summary>Moneda en la que cotiza.</summary>
    public Currency QuoteCurrency { get; private set; }

    /// <summary>Bolsa donde cotiza, en codigo MIC (XNYS, XTSE, XETR). Forma parte de su identidad.</summary>
    public string Exchange { get; private set; }

    /// <summary>Pais, si se conoce.</summary>
    public string? Country { get; private set; }

    /// <summary>ISIN, si existe. Los fondos canadienses de Fundserv <b>no tienen</b>.</summary>
    public string? Isin { get; private set; }

    /// <summary>Como se obtiene su precio. Es el eje que distingue unos activos de otros.</summary>
    public ValuationMode ValuationMode { get; private set; }

    /// <summary>Cada cuanto cambia su precio.</summary>
    public PricingFrequency PricingFrequency { get; private set; }

    /// <summary>Con que nombre lo conoce la fuente de precios. Nulo si aun no se ha resuelto.</summary>
    public string? ProviderKey { get; private set; }

    /// <summary>Con que codigo lo conoce el broker. Nulo si no se ha anotado.</summary>
    public string? BrokerCode { get; private set; }

    /// <summary>Dice si el instrumento sigue vivo. No se borra: se desactiva.</summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Los desdoblamientos que se le conocen, del mas antiguo al mas reciente.
    ///
    /// <b>Se guardan aqui y no se leen cada vez de la fuente</b>, y eso es lo que hace que no se pierdan:
    /// la fuente solo declara los eventos del rango que se le pide, que es un mes. Un desdoblamiento que
    /// ocurrio hace dos meses <b>ya no lo cuenta</b>, asi que si no se guardo cuando salio, no hay forma de
    /// recuperarlo. Y sin el, la posicion se lee como una caida que nunca ocurrio.
    /// </summary>
    public IReadOnlyList<StockSplit> Splits => _splits;

    private readonly List<StockSplit> _splits = [];

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>Dice si el precio lo tiene que introducir una persona.</summary>
    public bool RequiresManualPrice => ValuationMode == ValuationMode.Manual;

    /// <summary>Dice si se puede pedir su precio a una fuente automatica.</summary>
    public bool CanBeQuotedFromProvider =>
        ValuationMode != ValuationMode.Manual && !string.IsNullOrWhiteSpace(ProviderKey);

    /// <summary>Dice si es un fondo, que no cotiza en bolsa y publica valor liquidativo.</summary>
    public bool IsFund => AssetType is AssetType.MutualFund or AssetType.BondFund;

    /// <summary>Crea un instrumento.</summary>
    public static Instrument Create(
        Ticker symbol,
        string name,
        AssetType assetType,
        Currency quoteCurrency,
        string exchange,
        ValuationMode valuationMode,
        PricingFrequency pricingFrequency,
        DateTimeOffset now,
        string? country = null,
        string? isin = null)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(quoteCurrency);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);

        if (name.Length > MaxNameLength)
        {
            throw new ArgumentException(
                $"El nombre de un instrumento no puede pasar de {MaxNameLength} caracteres.", nameof(name));
        }

        var instrument = new Instrument(
            Guid.NewGuid(), symbol, name.Trim(), assetType, quoteCurrency,
            exchange.Trim().ToUpperInvariant(), valuationMode, pricingFrequency, now);

        if (country is not null)
        {
            instrument.SetCountry(country, now);
        }

        if (isin is not null)
        {
            instrument.SetIsin(isin, now);
        }

        return instrument;
    }

    /// <summary>Anota con que nombre lo conoce la fuente de precios.</summary>
    public void SetProviderKey(string providerKey, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerKey);
        ProviderKey = providerKey.Trim();
        Touch(now);
    }

    /// <summary>Olvida el nombre que usa la fuente de precios.</summary>
    public void ClearProviderKey(DateTimeOffset now)
    {
        ProviderKey = null;
        Touch(now);
    }

    /// <summary>
    /// Anota los desdoblamientos que declara la fuente, <b>sin repetir los que ya se sabian</b>.
    ///
    /// Se añaden y no se sustituyen, porque lo que se busca es justo lo contrario de lo que hace la fuente:
    /// ella solo cuenta los del ultimo mes, y aqui se acumulan. Si se sustituyera, un desdoblamiento de hace
    /// dos meses desapareceria al dia siguiente deja de venir en la respuesta.
    /// </summary>
    /// <param name="splits">Los que declara la fuente ahora mismo.</param>
    /// <param name="now">Momento del cambio.</param>
    /// <returns>Cuantos eran nuevos.</returns>
    public int RecordSplits(IEnumerable<StockSplit> splits, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(splits);

        var nuevos = 0;

        foreach (var split in splits)
        {
            // Se compara por fecha Y proporcion: la misma fecha con otra proporcion seria otro
            // desdoblamiento, y quedarse solo con la fecha lo perderia.
            if (_splits.Any(conocido => conocido.Date == split.Date && conocido.Ratio == split.Ratio))
            {
                continue;
            }

            _splits.Add(split);
            nuevos++;
        }

        if (nuevos > 0)
        {
            // Se guardan ordenados para que la lista se lea igual siempre y no dependa del orden en que la
            // fuente los mando, que no promete ninguno.
            _splits.Sort((a, b) => a.Date.CompareTo(b.Date));
            Touch(now);
        }

        return nuevos;
    }

    /// <summary>Anota el codigo con el que lo identifica el broker.</summary>
    public void SetBrokerCode(string brokerCode, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(brokerCode);
        BrokerCode = brokerCode.Trim().ToUpperInvariant();
        Touch(now);
    }

    /// <summary>Anota el ISIN, si el instrumento tiene.</summary>
    /// <exception cref="ArgumentException">Si no tiene la forma de un ISIN o su digito de control no cuadra.</exception>
    public void SetIsin(string isin, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(isin);
        var normalized = isin.Trim().ToUpperInvariant();

        if (normalized.Length != IsinLength || !normalized.All(char.IsAsciiLetterOrDigit))
        {
            throw new ArgumentException(
                $"Un ISIN son {IsinLength} caracteres alfanumericos, por ejemplo 'US0378331005'. " +
                $"Se recibio '{isin}'.", nameof(isin));
        }

        // El ultimo caracter de un ISIN es un digito de control. Comprobarlo es lo unico que atrapa un
        // tecleo equivocado: la longitud y los caracteres no distinguen 'US0378331005' de una errata.
        if (!HasValidCheckDigit(normalized))
        {
            throw new ArgumentException(
                $"El digito de control de '{isin}' no cuadra: puede ser un tecleo equivocado.",
                nameof(isin));
        }

        Isin = normalized;
        Touch(now);
    }

    /// <summary>
    /// Comprueba el digito de control del ISIN, que sigue el algoritmo de Luhn sobre el numero una vez
    /// convertidas las letras (A=10, B=11... Z=35).
    /// </summary>
    private static bool HasValidCheckDigit(string isin)
    {
        var digits = new System.Text.StringBuilder(isin.Length * 2);

        foreach (var character in isin)
        {
            if (char.IsAsciiDigit(character))
            {
                digits.Append(character);
            }
            else if (char.IsAsciiLetter(character))
            {
                // A vale 10, B vale 11, y asi hasta Z, que vale 35.
                digits.Append(character - 'A' + 10);
            }
            else
            {
                return false;
            }
        }

        var sum = 0;
        var doubleThis = false;

        // Se recorre de derecha a izquierda, doblando uno de cada dos digitos.
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var digit = digits[i] - '0';

            if (doubleThis)
            {
                digit *= 2;

                if (digit > 9)
                {
                    digit -= 9;
                }
            }

            sum += digit;
            doubleThis = !doubleThis;
        }

        return sum % 10 == 0;
    }

    /// <summary>Anota el pais.</summary>
    public void SetCountry(string country, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(country);
        Country = country.Trim().ToUpperInvariant();
        Touch(now);
    }

    /// <summary>Cambia el nombre legible.</summary>
    public void Rename(string name, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (name.Length > MaxNameLength)
        {
            throw new ArgumentException(
                $"El nombre de un instrumento no puede pasar de {MaxNameLength} caracteres.", nameof(name));
        }

        Name = name.Trim();
        Touch(now);
    }

    /// <summary>Cambia como se obtiene el precio.</summary>
    public void ChangeValuation(ValuationMode valuationMode, PricingFrequency pricingFrequency, DateTimeOffset now)
    {
        ValuationMode = valuationMode;
        PricingFrequency = pricingFrequency;
        Touch(now);
    }

    /// <summary>Desactiva el instrumento. No se borra nunca: las posiciones que lo usan quedarian huerfanas.</summary>
    public void Deactivate(DateTimeOffset now)
    {
        IsActive = false;
        Touch(now);
    }

    /// <summary>Vuelve a activar el instrumento.</summary>
    public void Activate(DateTimeOffset now)
    {
        IsActive = true;
        Touch(now);
    }

    private void Touch(DateTimeOffset now) => UpdatedAtUtc = now;
}
