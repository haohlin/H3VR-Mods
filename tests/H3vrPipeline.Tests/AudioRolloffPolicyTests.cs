using ThePing;
using Xunit;

namespace H3vrPipeline.Tests;

public sealed class AudioRolloffPolicyTests
{
    [Fact]
    public void Policy_preserves_the_4500_meter_audio_range_and_expected_curve()
    {
        Assert.Equal(4500f, AudioRolloffPolicy.MaxDistance);

        var points = AudioRolloffPolicy.Points;

        Assert.Collection(
            points,
            point => Assert.Equal((0f, 1f), (point.Distance, point.Volume)),
            point => Assert.Equal((50f, 0.5f), (point.Distance, point.Volume)),
            point => Assert.Equal((100f, 0.3f), (point.Distance, point.Volume)),
            point => Assert.Equal((1000f, 0.125f), (point.Distance, point.Volume)),
            point => Assert.Equal((2500f, 0.05f), (point.Distance, point.Volume)));
    }
}
