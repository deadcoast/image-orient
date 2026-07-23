using System.Globalization;

namespace OrientExpress.Spike009.Evidence;

public sealed record ScenarioResult(
    string Id,
    string Category,
    string Description,
    string Outcome,
    IReadOnlyDictionary<string, string> Measurements,
    string? Error)
{
    public const string Pass = "pass";
    public const string Fail = "fail";
}

/// <summary>Collects invariant-culture measurements with deterministic key order.</summary>
public sealed class ScenarioRecorder(string id, string category, string description)
{
    private readonly SortedDictionary<string, string> _measurements = new(StringComparer.Ordinal);
    private bool _expectationsHeld = true;

    public void Measure(string key, string value) => _measurements[key] = value;

    public void Measure(string key, bool value) => Measure(key, value ? "true" : "false");

    public void Measure(string key, long value) => Measure(key, value.ToString(CultureInfo.InvariantCulture));

    public void Expect(string key, bool held)
    {
        Measure(key, held);
        _expectationsHeld &= held;
    }

    public ScenarioResult Complete()
        => new(
            id,
            category,
            description,
            _expectationsHeld ? ScenarioResult.Pass : ScenarioResult.Fail,
            _measurements,
            Error: null);

    public ScenarioResult Failed(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new(id, category, description, ScenarioResult.Fail, _measurements, exception.ToString());
    }
}
