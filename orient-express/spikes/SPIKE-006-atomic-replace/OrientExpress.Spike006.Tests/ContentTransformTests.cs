using OrientExpress.Spike006.Protocol;

namespace OrientExpress.Spike006.Tests;

public sealed class ContentTransformTests
{
    [Fact]
    public void Transform_is_deterministic_and_size_preserving()
    {
        byte[] content = [1, 2, 3, 4, 5];

        Assert.Equal(ContentTransform.Apply(content), ContentTransform.Apply(content));
        Assert.Equal(content.Length, ContentTransform.Apply(content).Length);
    }

    [Fact]
    public void Transform_changes_non_palindromic_content()
    {
        byte[] content = [1, 2, 3];

        Assert.NotEqual(content, ContentTransform.Apply(content));
    }

    [Fact]
    public void Transform_is_an_involution()
    {
        byte[] content = [9, 8, 7, 6];

        Assert.Equal(content, ContentTransform.Apply(ContentTransform.Apply(content)));
    }
}
