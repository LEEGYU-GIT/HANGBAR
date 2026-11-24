using Hangbar.Core.World;

namespace Hangbar.Core.Tests.World.Gen.SceneChecks;

/// <summary>Timing runs alone: other tests generating worlds in parallel would slow it down and make it flaky.</summary>
[CollectionDefinition("Timing", DisableParallelization = true)]
public class TimingCollection { }

[Collection("Timing")]
public class SceneTimingTests
{
    [Theory]
    [InlineData(Scene.Mountain)]
    [InlineData(Scene.Beach)]
    [InlineData(Scene.Pool)]
    public void Generates_within_three_seconds(Scene s)
    {
        long ms = SceneKit.GenerationMilliseconds(s);
        Assert.True(ms <= 3000, $"{s}: {ms} ms");
    }
}
