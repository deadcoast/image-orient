using System.Globalization;

namespace OrientExpress.Spike006.Scenarios;

public sealed record ScenarioResult(
    string Id,
    string Category,
    string Description,
    string Outcome,
    bool SourceSafetyHeld,
    IReadOnlyDictionary<string, string> Measurements,
    string? Error)
{
    public const string Pass = "pass";
    public const string Fail = "fail";
    public const string Skipped = "skipped";
}

/// <summary>Collects invariant-culture measurements with deterministic key order.</summary>
public sealed class ScenarioRecorder(string id, string category, string description)
{
    private readonly SortedDictionary<string, string> _measurements = new(StringComparer.Ordinal);
    private bool _sourceSafetyHeld = true;
    private bool _expectationsHeld = true;

    public void Measure(string key, string value) => _measurements[key] = value;

    public void Measure(string key, bool value) => Measure(key, value ? "true" : "false");

    public void Measure(string key, long value) => Measure(key, value.ToString(CultureInfo.InvariantCulture));

    /// <summary>Records a hard expectation; a miss fails the scenario but keeps measuring.</summary>
    public void Expect(string key, bool held)
    {
        Measure(key, held);
        _expectationsHeld &= held;
    }

    /// <summary>Records a user-data-safety invariant; a miss is a source-loss case.</summary>
    public void ExpectSafety(string key, bool held)
    {
        Measure(key, held);
        _sourceSafetyHeld &= held;
        _expectationsHeld &= held;
    }

    public ScenarioResult Complete()
        => new(
            id,
            category,
            description,
            _expectationsHeld ? ScenarioResult.Pass : ScenarioResult.Fail,
            _sourceSafetyHeld,
            _measurements,
            Error: null);

    public ScenarioResult Skip(string reason)
        => new(id, category, description, ScenarioResult.Skipped, _sourceSafetyHeld, _measurements, reason);

    public ScenarioResult Failed(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new(
            id,
            category,
            description,
            ScenarioResult.Fail,
            SourceSafetyHeld: false,
            _measurements,
            exception.ToString());
    }
}
