using OrientExpress.Spike005;

namespace OrientExpress.Spike005.Tests;

/// <summary>The reference rotation must be beyond suspicion before it judges jpegtran.</summary>
public sealed class PpmTests
{
    [Fact]
    public void Serialize_then_parse_round_trips()
    {
        var fixture = Ppm.CreateFixture(37, 21);
        var parsed = Ppm.Parse(fixture.Serialize());
        Assert.Equal(fixture.Width, parsed.Width);
        Assert.Equal(fixture.Height, parsed.Height);
        Assert.Equal(fixture.Pixels, parsed.Pixels);
    }

    [Fact]
    public void Four_quarter_turns_return_the_original()
    {
        var fixture = Ppm.CreateFixture(20, 12);
        var rotated = fixture.Rotate(4);
        Assert.Equal(fixture.Pixels, rotated.Pixels);
    }

    [Fact]
    public void Rotation_swaps_dimensions_for_odd_quarter_turns()
    {
        var fixture = Ppm.CreateFixture(30, 10);
        Assert.Equal((10, 30), (fixture.Rotate(1).Width, fixture.Rotate(1).Height));
        Assert.Equal((30, 10), (fixture.Rotate(2).Width, fixture.Rotate(2).Height));
    }

    [Fact]
    public void Rotate90_moves_the_top_left_pixel_to_the_top_right()
    {
        var fixture = Ppm.CreateFixture(3, 2);
        var rotated = fixture.Rotate(1);
        // Source (0,0) lands at (width-1, 0) after a clockwise quarter turn.
        var source = fixture.Pixels.AsSpan(0, 3).ToArray();
        var destinationOffset = ((0 * rotated.Width) + (rotated.Width - 1)) * 3;
        Assert.Equal(source, rotated.Pixels.AsSpan(destinationOffset, 3).ToArray());
    }

    [Fact]
    public void Compare_counts_differences_and_dimension_mismatches()
    {
        var fixture = Ppm.CreateFixture(8, 8);
        Assert.Equal(0, Ppm.Compare(fixture, fixture).DiffBytes);

        var tampered = fixture with { Pixels = (byte[])fixture.Pixels.Clone() };
        tampered.Pixels[0] ^= 0xFF;
        Assert.Equal(1, Ppm.Compare(fixture, tampered).DiffBytes);

        var resized = Ppm.CreateFixture(8, 9);
        Assert.True(Ppm.Compare(fixture, resized).DiffBytes > 0);
    }
}
