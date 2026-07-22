using System.Xml.Linq;

namespace OrientExpress.ArchitectureTests;

/// <summary>
/// Enforces DOC-ARCH dependency rules and NFR-MAINT-001 (TEST-NFR-025): domain
/// policy must not depend on CLI, storage, model runtime, or native codec code.
/// Rules are checked against project files so unused references still fail.
/// </summary>
public sealed class ProjectDependencyRuleTests
{
    private static readonly string[] InnerLayerProjects =
    [
        "OrientExpress.Domain",
        "OrientExpress.Contracts",
        "OrientExpress.Application",
    ];

    private static readonly string[] AllowedApplicationReferences =
    [
        "OrientExpress.Domain",
        "OrientExpress.Contracts",
    ];

    private static readonly string[] CliProjectOnly = ["OrientExpress.Cli"];

    private static readonly string[] ForbiddenInDomain =
    [
        "OrientExpress.Cli",
        "OrientExpress.Infrastructure",
        "System.CommandLine",
        "Microsoft.Data.Sqlite",
        "SQLitePCL",
        "Microsoft.ML.OnnxRuntime",
        "Magick.NET",
        "Spectre.Console",
    ];

    [Theory]
    [InlineData("OrientExpress.Domain")]
    [InlineData("OrientExpress.Contracts")]
    public void Policy_projects_reference_no_other_projects(string project)
        => Assert.Empty(ProjectReferencesOf(project));

    [Fact]
    public void Application_references_only_domain_and_contracts()
        => Assert.All(
            ProjectReferencesOf("OrientExpress.Application"),
            reference => Assert.Contains(reference, AllowedApplicationReferences));

    [Fact]
    public void Inner_layers_reference_no_packages()
    {
        foreach (var project in InnerLayerProjects)
        {
            Assert.Empty(PackageReferencesOf(project));
        }
    }

    [Fact]
    public void Infrastructure_adapters_do_not_reference_presentation_or_each_other()
    {
        foreach (var project in SourceProjects().Where(name => name.StartsWith("OrientExpress.Infrastructure.", StringComparison.Ordinal)))
        {
            Assert.All(
                ProjectReferencesOf(project),
                reference => Assert.Contains(reference, InnerLayerProjects));
        }
    }

    [Fact]
    public void Only_the_cli_project_is_an_executable()
    {
        var executables = SourceProjects()
            .Where(project => LoadProject(project).Descendants("OutputType").Any(o => o.Value == "Exe"))
            .ToList();
        Assert.Equal(CliProjectOnly, executables);
    }

    [Fact]
    public void Domain_assembly_metadata_references_no_forbidden_assembly()
    {
        var referenced = typeof(Domain.AssemblyMarker).Assembly.GetReferencedAssemblies();
        Assert.All(
            referenced,
            assembly => Assert.DoesNotContain(
                ForbiddenInDomain,
                forbidden => (assembly.Name ?? string.Empty).StartsWith(forbidden, StringComparison.OrdinalIgnoreCase)));
    }

    private static string SolutionRoot { get; } = FindSolutionRoot();

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OrientExpress.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("OrientExpress.slnx was not found above the test base directory.");
    }

    private static List<string> SourceProjects()
        => Directory.GetDirectories(Path.Combine(SolutionRoot, "src"), "OrientExpress.*")
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order(StringComparer.Ordinal)
            .ToList();

    private static XDocument LoadProject(string project)
        => XDocument.Load(Path.Combine(SolutionRoot, "src", project, project + ".csproj"));

    private static List<string> ProjectReferencesOf(string project)
        => LoadProject(project)
            .Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension((string?)reference.Attribute("Include") ?? string.Empty))
            .ToList();

    private static List<string> PackageReferencesOf(string project)
        => LoadProject(project)
            .Descendants("PackageReference")
            .Select(reference => (string?)reference.Attribute("Include") ?? string.Empty)
            .ToList();
}
