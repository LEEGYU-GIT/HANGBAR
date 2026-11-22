using Hangbar.Core;

namespace Hangbar.Core.Tests;

public class TopEdgeClipperTests
{
    [Fact]
    public void Single_window_gives_full_top_edge() =>
        Assert.Equal(new[] { new EdgeSegment(0, 100, 50, 450) }, TopEdgeClipper.Clip(new[] { new RectI(50, 100, 400, 300) }));

    [Fact]
    public void Window_in_front_cuts_the_middle_of_edge_behind() =>
        Assert.Equal(
            new[] { new EdgeSegment(0, 50, 200, 300), new EdgeSegment(1, 100, 0, 200), new EdgeSegment(1, 100, 300, 600) },
            TopEdgeClipper.Clip(new[] { new RectI(200, 50, 100, 400), new RectI(0, 100, 600, 300) }));

    [Fact]
    public void Front_window_below_the_edge_does_not_cut() =>
        Assert.Contains(new EdgeSegment(1, 100, 0, 600),
            TopEdgeClipper.Clip(new[] { new RectI(200, 300, 100, 100), new RectI(0, 100, 600, 300) }));

    [Fact]
    public void Fully_covered_edge_disappears() =>
        Assert.DoesNotContain(TopEdgeClipper.Clip(new[] { new RectI(0, 0, 800, 600), new RectI(100, 100, 200, 200) }), s => s.WindowIndex == 1);

    // Back edge [0,620) is covered on [10,610) by the front window: two 10px pieces remain, both dropped.
    [Fact]
    public void Pieces_shorter_than_24px_are_dropped() =>
        Assert.DoesNotContain(TopEdgeClipper.Clip(new[] { new RectI(10, 0, 600, 400), new RectI(0, 100, 620, 300) }), s => s.WindowIndex == 1);

    [Fact]
    public void Two_front_windows_cut_separately_and_overlapping_cuts_merge() =>
        Assert.Equal(
            new[] { new EdgeSegment(2, 100, 0, 100), new EdgeSegment(2, 100, 400, 1000) },
            TopEdgeClipper.Clip(new[] { new RectI(100, 0, 200, 300), new RectI(250, 50, 150, 300), new RectI(0, 100, 1000, 300) })
                .Where(s => s.WindowIndex == 2));
}

public class TaskbarFloorTests
{
    [Theory]
    [InlineData(TaskbarEdge.Bottom, false, 1040)]
    [InlineData(TaskbarEdge.Bottom, true, 1079)]
    [InlineData(TaskbarEdge.Left, false, 1079)]
    [InlineData(TaskbarEdge.Top, false, 1079)]
    public void Floor_follows_taskbar_position(TaskbarEdge edge, bool autoHide, int expected)
    {
        var m = new Monitor("A", new RectI(0, 0, 1920, 1080), new RectI(0, 0, 1920, 1040), 1.0, true);
        var tb = edge switch
        {
            TaskbarEdge.Bottom => new RectI(0, 1040, 1920, 40),
            TaskbarEdge.Top => new RectI(0, 0, 1920, 40),
            _ => new RectI(0, 0, 48, 1080),
        };
        Assert.Equal(expected, TaskbarFloor.FloorY(m, tb, edge, autoHide));
    }

    [Fact]
    public void No_taskbar_uses_monitor_bottom()
    {
        var m = new Monitor("B", new RectI(1920, 0, 2560, 1440), new RectI(1920, 0, 2560, 1440), 1.5, false);
        Assert.Equal(1439, TaskbarFloor.FloorY(m, null, TaskbarEdge.Bottom, false));
    }
}
