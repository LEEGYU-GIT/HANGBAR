namespace Hangbar.Core.World.Gen;

/// <summary>A block position in world block coordinates.</summary>
public readonly record struct BlockPos(int X, int Y, int Z);

/// <summary>SplitMix64: the same numbers on every machine and .NET version (moved unchanged from the v1 generator).</summary>
public sealed class Rng(ulong seed)
{
    ulong _s = seed;

    public static ulong Mix(ulong z)
    {
        z += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>Uniform in [0, 1).</summary>
    public double Next()
    {
        _s += 0x9E3779B97F4A7C15UL;
        return (Mix(_s) >> 11) * (1.0 / (1UL << 53));
    }

    /// <summary>Uniform integer in [min, maxExclusive); min when the range is empty.</summary>
    public int NextInt(int min, int maxExclusive) => maxExclusive <= min ? min : min + (int)(Next() * (maxExclusive - min));
}
