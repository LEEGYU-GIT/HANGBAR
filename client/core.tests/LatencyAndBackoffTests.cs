using Hangbar.Core;

namespace Hangbar.Core.Tests;

public class LatencyStatsTests
{
    [Fact]
    public void P50_and_p95_use_nearest_rank()
    {
        var s = new LatencyStats();
        foreach (var v in Enumerable.Range(1, 100)) s.Add(v);
        Assert.Equal(50, s.P50);
        Assert.Equal(95, s.P95);
    }

    [Fact]
    public void Keeps_only_latest_200()
    {
        var s = new LatencyStats();
        for (var i = 0; i < 250; i++) s.Add(i);
        Assert.Equal(200, s.Count);
        Assert.Equal(149, s.P50); // samples 50..249 remain; nearest-rank 100th of 200 = 149
    }

    [Fact]
    public void Empty_stats_report_zero()
    {
        var s = new LatencyStats();
        Assert.Equal(0, s.Count);
        Assert.Equal(0, s.P50);
        Assert.Equal(0, s.P95);
    }
}

public class ReconnectBackoffTests
{
    [Theory]
    [InlineData(0, 500)]
    [InlineData(1, 1000)]
    [InlineData(5, 16000)]
    [InlineData(6, 30000)]
    [InlineData(20, 30000)]
    [InlineData(100, 30000)]
    public void Backoff_doubles_and_caps(int attempt, int ms) => Assert.Equal(ms, ReconnectBackoff.DelayMs(attempt));
}
