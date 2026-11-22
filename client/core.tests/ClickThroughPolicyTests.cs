using Hangbar.Core;

namespace Hangbar.Core.Tests;

public class ClickThroughPolicyTests
{
    static readonly RectI[] Figure = { new(100, 100, 40, 60) };

    [Fact]
    public void Inside_a_hit_rect_disables_passthrough() =>
        Assert.Equal(new ClickThroughDecision(false, 33), ClickThroughPolicy.Decide(new PointI(105, 105), Figure));

    [Fact]
    public void Near_a_hit_rect_keeps_passthrough_and_polls_fast() =>
        Assert.Equal(new ClickThroughDecision(true, 33), ClickThroughPolicy.Decide(new PointI(300, 100), Figure));

    [Fact]
    public void Far_from_all_rects_polls_slowly() =>
        Assert.Equal(new ClickThroughDecision(true, 100), ClickThroughPolicy.Decide(new PointI(900, 900), Figure));

    [Fact]
    public void No_rects_means_passthrough_and_slow_poll() =>
        Assert.Equal(new ClickThroughDecision(true, 100), ClickThroughPolicy.Decide(new PointI(0, 0), Array.Empty<RectI>()));

    [Fact]
    public void Nearest_of_several_rects_decides() =>
        Assert.Equal(new ClickThroughDecision(false, 33),
            ClickThroughPolicy.Decide(new PointI(1005, 505), new[] { new RectI(0, 0, 10, 10), new RectI(1000, 500, 20, 20) }));
}
