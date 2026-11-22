using Hangbar.Core;

namespace Hangbar.Core.Tests;

public class GeometryTests
{
    static Monitor M(string id, RectI bounds, double scale = 1.0, bool primary = false) =>
        new(id, bounds, bounds with { H = bounds.H - 40 }, scale, primary);

    [Fact]
    public void Overlay_is_one_pixel_shorter_than_monitor() =>
        Assert.Equal(new RectI(0, 0, 1920, 1079),
            OverlayGeometry.OverlayRectFor(new Monitor("A", new RectI(0, 0, 1920, 1080), new RectI(0, 0, 1920, 1040), 1.0, true)));

    [Fact]
    public void Overlay_keeps_negative_origin_of_left_monitor() =>
        Assert.Equal(new RectI(-2560, -200, 2560, 1439),
            OverlayGeometry.OverlayRectFor(new Monitor("B", new RectI(-2560, -200, 2560, 1440), new RectI(-2560, -200, 2560, 1400), 1.5, false)));

    [Fact]
    public void Diff_reports_added_removed_and_scale_change()
    {
        var before = new MonitorLayout(new[] { M("A", new RectI(0, 0, 1920, 1080), 1.0, true), M("B", new RectI(1920, 0, 1920, 1080)) });
        var after = new MonitorLayout(new[] { M("A", new RectI(0, 0, 1920, 1080), 1.5, true), M("C", new RectI(-1920, 0, 1920, 1080)) });

        var d = MonitorLayout.Diff(before, after);

        Assert.Equal(new[] { "C" }, d.Added.Select(m => m.Id));
        Assert.Equal(new[] { "B" }, d.Removed.Select(m => m.Id));
        Assert.Equal(new[] { "A" }, d.Changed.Select(m => m.Id));
        Assert.Equal(1.5, d.Changed[0].Scale);
    }

    [Fact]
    public void Diff_of_identical_layouts_is_empty()
    {
        var a = new MonitorLayout(new[] { M("A", new RectI(0, 0, 1920, 1080), 1.0, true) });
        var b = new MonitorLayout(new[] { M("A", new RectI(0, 0, 1920, 1080), 1.0, true) });
        Assert.True(MonitorLayout.Diff(a, b).IsEmpty);
    }

    [Fact]
    public void Contains_excludes_right_and_bottom_edges()
    {
        var r = new RectI(0, 0, 10, 10);
        Assert.True(r.Contains(new PointI(9, 9)));
        Assert.False(r.Contains(new PointI(10, 5)));
        Assert.False(r.Contains(new PointI(5, 10)));
    }

    [Fact]
    public void DistanceTo_is_zero_inside_and_chebyshev_outside()
    {
        var r = new RectI(0, 0, 10, 10);
        Assert.Equal(0, r.DistanceTo(new PointI(5, 5)));
        Assert.Equal(5, r.DistanceTo(new PointI(15, 12)));
        Assert.Equal(3, r.DistanceTo(new PointI(-3, 4)));
    }
}
