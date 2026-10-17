using System.Reflection;
using System.Xml.Linq;

namespace PortaFolioAcciones.Architecture.Tests;

/// <summary>
/// Comprueba la regla de dependencias de la arquitectura limpia: las dependencias apuntan al dominio,
/// nunca al contrario. Y que la logica de negocio es independiente de framework.
///
/// Se comprueba en DOS capas, y las dos hacen falta:
///
///   1. Los ficheros de proyecto (.csproj): que nadie DECLARE una referencia prohibida. Esto se
///      descubrio falsificando la prueba: si solo se mira el ensamblado compilado, anadir el paquete
///      no la hace fallar, porque el compilador omite la referencia mientras no se use ningun tipo.
///      Es decir, el proyecto quedaba acoplado y la prueba seguia en verde.
///   2. Los ensamblados compilados: que el codigo no acabe USANDO nada prohibido, incluido lo que
///      llegue de forma transitiva a traves de otra dependencia.
///
/// Se hace con reflexion y lectura de XML, SIN librerias externas. La opcion habitual
/// (NetArchTest.Rules) no publica una version desde mayo de 2021 y su metadato de licencia viene
/// vacio; para estas reglas no merece la pena arrastrar una dependencia abandonada y su Mono.Cecil.
///
/// Si alguna de estas pruebas falla, NO se arregla la prueba: se arregla el codigo.
/// </summary>
public class LayerDependencyTests
{
    private const string Slnx = "PortaFolioAcciones.slnx";
    private const string Domain = "Domain";
    private const string Application = "Application";
    private const string Infrastructure = "Infrastructure";
    private const string Api = "Api";

    private static readonly string[] ForbiddenInDomain =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "Microsoft.Extensions.Http",
        "Microsoft.Extensions.DependencyInjection",
        "Npgsql",
        "Serilog",
        "MediatR",
        "AutoMapper",
    ];

    // ---- Localizacion de la raiz del repositorio -----------------------------------------------

    /// <summary>Sube desde la carpeta de las pruebas hasta encontrar el fichero de solucion.</summary>
    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, Slnx)))
            dir = dir.Parent;

        Assert.True(dir is not null,
            $"No se encontro '{Slnx}' subiendo desde '{AppContext.BaseDirectory}'.");
        return dir!.FullName;
    }

    private static string ProjectFile(string layer) =>
        Path.Combine(RepositoryRoot(), "src", $"PortaFolioAcciones.{layer}", $"PortaFolioAcciones.{layer}.csproj");

    private static XDocument Project(string layer) => XDocument.Load(ProjectFile(layer));

    /// <summary>Nombres de capa de las referencias entre proyectos declaradas en el .csproj.</summary>
    private static string[] DeclaredProjectReferences(string layer) =>
        Project(layer).Descendants("ProjectReference")
            .Select(e => (string?)e.Attribute("Include"))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => Path.GetFileNameWithoutExtension(v!.Replace('\\', '/')))
            .Select(n => n.Replace("PortaFolioAcciones.", string.Empty))
            .ToArray();

    /// <summary>Paquetes NuGet declarados en el .csproj.</summary>
    private static string[] DeclaredPackageReferences(string layer) =>
        Project(layer).Descendants("PackageReference")
            .Select(e => (string?)e.Attribute("Include"))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!)
            .ToArray();

    private static string[] CompiledReferences(string layer) =>
        Assembly.Load($"PortaFolioAcciones.{layer}")
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToArray();

    private static void AssertNoViolation(IEnumerable<string> actual, IEnumerable<string> forbidden, string message)
    {
        var violations = actual
            .Where(a => forbidden.Contains(a, StringComparer.OrdinalIgnoreCase))
            .Distinct()
            .ToArray();

        Assert.True(violations.Length == 0, $"{message} Encontrado: {string.Join(", ", violations)}.");
    }

    // ---- Capa 1: lo que cada proyecto DECLARA --------------------------------------------------

    [Fact]
    public void Domain_ShouldNotDeclareAnyProjectReference() =>
        Assert.True(DeclaredProjectReferences(Domain).Length == 0,
            "El dominio no puede depender de ninguna otra capa, y declara: " +
            $"{string.Join(", ", DeclaredProjectReferences(Domain))}.");

    [Fact]
    public void Domain_ShouldNotDeclareAnyNuGetPackage() =>
        Assert.True(DeclaredPackageReferences(Domain).Length == 0,
            "El dominio no puede depender de ningun paquete: la logica de negocio es independiente de " +
            $"framework. Declara: {string.Join(", ", DeclaredPackageReferences(Domain))}.");

    [Fact]
    public void Application_ShouldOnlyDeclareDomain()
    {
        var declared = DeclaredProjectReferences(Application);
        AssertNoViolation(declared, [Infrastructure, Api],
            "La capa de aplicacion solo puede depender del dominio.");
        Assert.True(declared.Contains(Domain),
            "La capa de aplicacion debe declarar su dependencia del dominio.");
    }

    [Fact]
    public void Infrastructure_ShouldNotDeclareApi() =>
        AssertNoViolation(DeclaredProjectReferences(Infrastructure), [Api],
            "La infraestructura no puede depender de la capa de presentacion.");

    // ---- Capa 2: lo que el codigo COMPILADO acaba usando ---------------------------------------

    [Fact]
    public void Domain_ShouldNotDependOnAnyOtherLayer() =>
        AssertNoViolation(CompiledReferences(Domain), [Application, Infrastructure, Api],
            "El dominio referencia ensamblados de otras capas.");

    [Fact]
    public void Application_ShouldNotDependOnInfrastructureOrApi() =>
        AssertNoViolation(CompiledReferences(Application), [Infrastructure, Api],
            "La capa de aplicacion referencia infraestructura o presentacion.");

    [Fact]
    public void Infrastructure_ShouldNotDependOnApi() =>
        AssertNoViolation(CompiledReferences(Infrastructure), [Api],
            "La infraestructura referencia la capa de presentacion.");

    [Fact]
    public void Domain_ShouldNotDependOnAnyFramework()
    {
        var violations = CompiledReferences(Domain)
            .Where(r => ForbiddenInDomain.Any(f => r.StartsWith(f, StringComparison.OrdinalIgnoreCase)))
            .Distinct()
            .ToArray();

        Assert.True(violations.Length == 0,
            "El dominio debe ser independiente de framework y usa: " + string.Join(", ", violations) +
            ". Los detalles de infraestructura se esconden detras de puertos definidos en el dominio.");
    }
}
