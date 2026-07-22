namespace OrientExpress.UnitTests;

/// <summary>Proves the test harness runs against the inner-layer assemblies (WP-001).</summary>
public sealed class BaselineSmokeTests
{
    [Fact]
    public void Inner_layer_assemblies_load()
    {
        Assert.NotNull(typeof(Domain.AssemblyMarker).Assembly);
        Assert.NotNull(typeof(Application.AssemblyMarker).Assembly);
        Assert.NotNull(typeof(Contracts.AssemblyMarker).Assembly);
    }
}
