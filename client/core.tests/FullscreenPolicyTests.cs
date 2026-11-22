using Hangbar.Core;

namespace Hangbar.Core.Tests;

public class FullscreenPolicyTests
{
    static readonly RectI Screen = new(0, 0, 1920, 1080);

    [Theory]
    [InlineData(QunsState.Busy, true)]
    [InlineData(QunsState.RunningD3DFullScreen, true)]
    [InlineData(QunsState.PresentationMode, true)]
    [InlineData(QunsState.AcceptsNotifications, false)]
    [InlineData(QunsState.QuietTime, false)]
    public void Quns_state_alone_decides_when_no_foreground(QunsState s, bool hide) =>
        Assert.Equal(hide, FullscreenPolicy.ShouldHide(s, null, Screen, false));

    [Fact]
    public void Borderless_window_covering_monitor_hides() =>
        Assert.True(FullscreenPolicy.ShouldHide(QunsState.AcceptsNotifications, new RectI(0, 0, 1920, 1080), Screen, false));

    [Fact]
    public void Window_larger_than_monitor_hides() =>
        Assert.True(FullscreenPolicy.ShouldHide(QunsState.AcceptsNotifications, new RectI(-8, -8, 1936, 1096), Screen, false));

    [Fact]
    public void Maximized_window_not_covering_taskbar_does_not_hide() =>
        Assert.False(FullscreenPolicy.ShouldHide(QunsState.AcceptsNotifications, new RectI(0, 0, 1920, 1040), Screen, false));

    // Review I2: with an auto-hide taskbar a maximized ordinary window covers the whole monitor too.
    [Fact]
    public void Maximized_or_captioned_window_covering_monitor_does_not_hide() =>
        Assert.False(FullscreenPolicy.ShouldHide(QunsState.AcceptsNotifications, new RectI(0, 0, 1920, 1080), Screen, false, foregroundIsOrdinaryWindow: true));

    [Fact]
    public void Quns_busy_still_hides_even_for_ordinary_window() =>
        Assert.True(FullscreenPolicy.ShouldHide(QunsState.Busy, new RectI(0, 0, 1920, 1080), Screen, false, foregroundIsOrdinaryWindow: true));

    [Fact]
    public void Desktop_or_own_window_never_hides() =>
        Assert.False(FullscreenPolicy.ShouldHide(QunsState.AcceptsNotifications, new RectI(0, 0, 1920, 1080), Screen, true));
}
