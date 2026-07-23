using System.Collections;

namespace OrientExpress.Cli.Hosting;

/// <summary>
/// Ambient-state seam for the host: environment variables and well-known
/// paths are injected so golden tests stay hermetic (no process-global
/// mutation) while production uses the real process state.
/// </summary>
public sealed record HostEnvironment(
    IReadOnlyDictionary<string, string> EnvironmentVariables,
    string UserConfigPath,
    string DataRoot)
{
    public static HostEnvironment CreateDefault()
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string key && entry.Value is string value)
            {
                variables[key] = value;
            }
        }

        return new HostEnvironment(
            variables,
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "orient-express", "config.json"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "orient-express"));
    }
}
