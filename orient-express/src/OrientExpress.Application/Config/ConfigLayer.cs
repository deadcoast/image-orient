using OrientExpress.Contracts.Config;

namespace OrientExpress.Application.Config;

/// <summary>
/// One precedence layer (DOC-CONFIG). Layer names are part of the support
/// contract: they appear verbatim in `config show` provenance.
/// </summary>
public sealed record ConfigLayer(string Name, UprightConfig? Values)
{
    public const string Cli = "cli";
    public const string Environment = "environment";
    public const string ExplicitFile = "explicit-file";
    public const string UserFile = "user-file";
    public const string Defaults = "defaults";
}
