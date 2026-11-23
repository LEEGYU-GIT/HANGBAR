namespace Hangbar.Core;

/// <summary>Round-trip times of the latest 200 pings; percentiles by nearest rank.</summary>
public sealed class LatencyStats
{
    const int Capacity = 200;
    readonly Queue<double> _samples = new();

    // Add runs on the socket receive thread, reads on the game thread (ECC review), so every access locks.
    public int Count { get { lock (_samples) return _samples.Count; } }
    public double P50 => Percentile(50);
    public double P95 => Percentile(95);

    public void Add(double ms)
    {
        lock (_samples)
        {
            _samples.Enqueue(ms);
            while (_samples.Count > Capacity) _samples.Dequeue();
        }
    }

    double Percentile(int p)
    {
        double[] sorted;
        lock (_samples) sorted = _samples.ToArray();
        if (sorted.Length == 0) return 0;
        Array.Sort(sorted);
        int rank = (int)Math.Ceiling(p / 100.0 * sorted.Length);
        return sorted[Math.Clamp(rank, 1, sorted.Length) - 1];
    }
}

/// <summary>Reconnect delay: 500ms doubling per attempt, capped at 30s.</summary>
public static class ReconnectBackoff
{
    public const int BaseMs = 500, MaxMs = 30000;

    public static int DelayMs(int attempt)
    {
        if (attempt <= 0) return BaseMs;
        if (attempt >= 6) return MaxMs;
        return Math.Min(MaxMs, BaseMs << attempt);
    }
}
