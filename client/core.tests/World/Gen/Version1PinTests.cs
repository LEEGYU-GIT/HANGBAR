using Hangbar.Core.World;

namespace Hangbar.Core.Tests.World.Gen;

/// <summary>Pins the v1 generator's output so moving it onto the shared tools (plan Task 4) changes nothing.</summary>
public class Version1PinTests
{
    static readonly uint[] MeadowPalette = Palette.EnsureContrast(Palette.Extract(ImageFit.Prepare(SamplePhotos.Meadow(), 192)));
    static readonly Mood Calm = new(0.6f, 0.4f, 0.5f);

    static ulong Fnv(IEnumerable<ulong> values)
    {
        ulong h = 14695981039346656037UL;
        foreach (ulong v in values)
            for (int s = 0; s < 64; s += 8) { h ^= (v >> s) & 0xFF; h *= 1099511628211UL; }
        return h;
    }

    public static ulong HashOf(Biome b)
    {
        var m = BiomeGenerator.Generate(b, MeadowPalette, Calm, 12345);
        var t = m.Terrain;
        IEnumerable<ulong> All()
        {
            foreach (int h in t.Height) yield return (ulong)h;
            foreach (uint c in t.Color) yield return c;
            foreach (bool w in t.Water) yield return w ? 1UL : 0UL;
            foreach (var bd in m.Buildings) { yield return (ulong)bd.X; yield return (ulong)bd.Z; yield return (ulong)bd.W; yield return (ulong)bd.D; yield return (ulong)bd.Height; yield return bd.Color; yield return (ulong)bd.Lift; }
            foreach (var p in m.Props) { yield return (ulong)p.Kind; yield return (ulong)p.X; yield return (ulong)p.Z; yield return (ulong)BitConverter.SingleToInt32Bits(p.Scale); yield return p.Color; }
        }
        return Fnv(All());
    }

    [Theory]
    [InlineData(Biome.Field, 2585572984850108670UL)]
    [InlineData(Biome.Water, 13190288056368839631UL)]
    [InlineData(Biome.Road, 18070723841017623455UL)]
    public void Version1_maps_are_unchanged_by_the_refactor(Biome b, ulong expected)
    {
        ulong actual = HashOf(b);
        Assert.True(expected == actual, $"{b}: {actual}");
    }
}
