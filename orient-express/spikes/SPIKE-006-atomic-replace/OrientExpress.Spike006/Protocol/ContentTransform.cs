namespace OrientExpress.Spike006.Protocol;

/// <summary>
/// Stand-in for the real lossless rotation: deterministic, size-preserving,
/// and guaranteed to differ from its input for any content longer than one
/// distinct byte. Reversal is an involution, which the tests exploit.
/// </summary>
public static class ContentTransform
{
    public static byte[] Apply(ReadOnlySpan<byte> content)
    {
        var transformed = content.ToArray();
        Array.Reverse(transformed);
        return transformed;
    }
}
