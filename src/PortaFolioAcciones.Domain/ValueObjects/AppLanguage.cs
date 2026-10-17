namespace PortaFolioAcciones.Domain.ValueObjects;

/// <summary>
/// El idioma de la interfaz para una persona.
///
/// Es un objeto de valor y no un texto suelto porque <b>la lista esta cerrada</b>: si se aceptara
/// cualquier cadena, alguien podria guardar un idioma para el que no hay traducciones y la pantalla
/// saldria vacia. Aqui, un idioma que no exista falla al guardarlo, no al usarlo.
///
/// La aplicacion esta en espanol, frances e ingles ([ADR-013](../../../DOCUMENTACION/decisiones/ADR-013-interfaz-multi-idioma.md)),
/// y el ingles es el <b>respaldo</b>: si a una traduccion le falta una clave, se muestra en ingles en
/// vez de dejar la clave a la vista.
/// </summary>
public sealed class AppLanguage : IEquatable<AppLanguage>
{
    /// <summary>Espanol.</summary>
    public static readonly AppLanguage Spanish = new("es");

    /// <summary>Frances. La familia vive en Quebec, asi que no es un adorno.</summary>
    public static readonly AppLanguage French = new("fr");

    /// <summary>Ingles. Es tambien el idioma de respaldo.</summary>
    public static readonly AppLanguage English = new("en");

    /// <summary>Idiomas disponibles, en el orden en que se ofrecen.</summary>
    public static readonly IReadOnlyList<AppLanguage> Supported = [Spanish, French, English];

    private AppLanguage(string code) => Code = code;

    /// <summary>Codigo de dos letras: <c>es</c>, <c>fr</c>, <c>en</c>.</summary>
    public string Code { get; }

    /// <summary>Idioma que se usa cuando falta una traduccion.</summary>
    public static AppLanguage Fallback => English;

    /// <summary>Crea un idioma a partir de su codigo.</summary>
    /// <exception cref="ArgumentException">Si el idioma no esta entre los disponibles.</exception>
    public static AppLanguage FromCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var normalized = code.Trim().ToLowerInvariant();

        return normalized switch
        {
            "es" => Spanish,
            "fr" => French,
            "en" => English,
            _ => throw new ArgumentException(
                $"'{code}' no es un idioma disponible. Los hay: " +
                $"{string.Join(", ", Supported.Select(l => l.Code))}.", nameof(code)),
        };
    }

    /// <summary>
    /// Interpreta el idioma preferido de un navegador, del tipo <c>es-CA</c> o <c>fr</c>.
    /// Si no se reconoce ninguno, devuelve el de respaldo en vez de fallar: el idioma del navegador es
    /// una sugerencia, no un dato que deba impedir entrar.
    /// </summary>
    public static AppLanguage FromBrowserTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return Fallback;
        }

        // Se miran todas las variantes que anuncia el navegador, en su orden de preferencia.
        foreach (var candidate in tag.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var language = candidate.Split(';')[0].Trim();
            var prefix = language.Split('-')[0].ToLowerInvariant();

            if (prefix is "es" or "fr" or "en")
            {
                return FromCode(prefix);
            }
        }

        return Fallback;
    }

    public bool Equals(AppLanguage? other) =>
        other is not null && string.Equals(Code, other.Code, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as AppLanguage);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Code);

    public override string ToString() => Code;

    public static bool operator ==(AppLanguage? left, AppLanguage? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(AppLanguage? left, AppLanguage? right) => !(left == right);
}
