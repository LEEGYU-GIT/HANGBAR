using Hangbar.Core.World;
using Hangbar.Core.World.Gen;

namespace Hangbar.Core.Tests.World.Gen;

public class GenToolsTests
{
    static readonly uint Stone = Block.Solid(0x808080, Material.Stone);

    static VoxelWorld Flat(int size = 32, int height = 20, int ground = 8)
    {
        var w = new VoxelWorld(size, height, size);
        for (int z = 0; z < size; z++)
            for (int x = 0; x < size; x++)
                for (int y = 0; y < ground; y++) w.Set(x, y, z, Stone);
        return w;
    }

    // ---------------------------------------------------------------- slope

    [Fact]
    public void Limit_keeps_neighbours_within_step()
    {
        int n = 16;
        var h = new int[n * n];
        var rng = new Rng(7);
        for (int i = 0; i < h.Length; i++) h[i] = rng.NextInt(0, 40);
        var pins = Enumerable.Repeat(Slope.NoPin, n * n).ToArray();
        var r = Slope.Limit(h, pins, n, 2);
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                if (x > 0) Assert.InRange(Math.Abs(r[z * n + x] - r[z * n + x - 1]), 0, 2);
                if (z > 0) Assert.InRange(Math.Abs(r[z * n + x] - r[(z - 1) * n + x]), 0, 2);
            }
    }

    [Fact]
    public void Limit_holds_pinned_columns()
    {
        int n = 16;
        var h = Enumerable.Repeat(30, n * n).ToArray();
        var pins = Enumerable.Repeat(Slope.NoPin, n * n).ToArray();
        pins[5 * n + 5] = 10;
        var r = Slope.Limit(h, pins, n, 2);
        Assert.Equal(10, r[5 * n + 5]);
        Assert.Equal(12, r[5 * n + 6]);
    }

    [Fact]
    public void Limit_throws_when_pins_conflict()
    {
        int n = 8;
        var pins = Enumerable.Repeat(Slope.NoPin, n * n).ToArray();
        pins[0] = 0;
        pins[1] = 10;
        Assert.Throws<InvalidOperationException>(() => Slope.Limit(new int[n * n], pins, n, 2));
    }

    // ---------------------------------------------------------------- walking

    [Fact]
    public void Flat_ground_is_all_reachable() =>
        Assert.All(Walkability.ReachColumns(Flat(), new BlockPos(16, 8, 16)), Assert.True);

    [Fact]
    public void Three_block_wall_stops_walking()
    {
        var w = Flat();
        for (int z = 0; z < 32; z++)
            for (int y = 8; y < 11; y++) w.Set(16, y, z, Stone);
        var r = Walkability.ReachColumns(w, new BlockPos(4, 8, 16));
        Assert.False(r[16 * 32 + 28]);
    }

    [Fact]
    public void Two_block_steps_are_climbed()
    {
        var w = Flat();
        for (int z = 0; z < 32; z++)
            for (int x = 16; x < 32; x++)
                for (int y = 8; y < 10; y++) w.Set(x, y, z, Stone);
        Assert.True(Walkability.ReachColumns(w, new BlockPos(4, 8, 16))[16 * 32 + 28]);
    }

    [Fact]
    public void Tunnel_lower_than_seven_blocks_is_not_walkable()
    {
        var w = Flat(height: 30);
        // a solid hill from x 12..31 with a tunnel 6 blocks high along x, entrance at x=12
        for (int z = 0; z < 32; z++)
            for (int x = 12; x < 32; x++)
                for (int y = 8; y < 25; y++)
                    if (!(z == 16 && y < 14)) w.Set(x, y, z, Stone);
        var r = Walkability.ReachColumns(w, new BlockPos(4, 8, 16));
        Assert.False(r[16 * 32 + 20]);
        Assert.False(Walkability.IsStandable(w, 20, 8, 16));
    }

    [Fact]
    public void Standing_needs_solid_ground_and_seven_free_blocks()
    {
        var w = Flat();
        Assert.True(Walkability.IsStandable(w, 5, 8, 5));
        Assert.False(Walkability.IsStandable(w, 5, 7, 5)); // inside the ground
        Assert.False(Walkability.IsStandable(w, 5, 9, 5)); // floating
    }

    // ---------------------------------------------------------------- sight

    [Fact]
    public void Open_air_is_clear() => Assert.True(Sight.Clear(Flat(), (2.5, 12.5, 2.5), (28.5, 10.5, 28.5)));

    [Fact]
    public void Opaque_wall_blocks_sight()
    {
        var w = Flat();
        for (int z = 0; z < 32; z++)
            for (int y = 8; y < 20; y++) w.Set(16, y, z, Stone);
        Assert.False(Sight.Clear(w, (4.5, 12.5, 16.5), (28.5, 12.5, 16.5)));
    }

    [Fact]
    public void Glass_and_water_do_not_block_sight()
    {
        var w = Flat();
        for (int z = 0; z < 32; z++)
        {
            for (int y = 8; y < 14; y++) w.Set(16, y, z, Block.Solid(0xC8E6F0, Material.Glass));
            for (int y = 8; y < 14; y++) w.Set(20, y, z, Block.Water);
        }
        Assert.True(Sight.Clear(w, (4.5, 12.5, 16.5), (28.5, 12.5, 16.5)));
    }

    // ---------------------------------------------------------------- random and noise

    [Fact]
    public void Rng_and_noise_are_deterministic()
    {
        var a = new Rng(42);
        var b = new Rng(42);
        for (int i = 0; i < 10; i++) Assert.Equal(a.Next(), b.Next());
        var n = new ValueNoise(9);
        Assert.Equal(n.Sample(13.2, 7.7, 16, 3), new ValueNoise(9).Sample(13.2, 7.7, 16, 3));
        double s = n.Sample3(3.5, 9.25, 1.75, 8, 5);
        Assert.InRange(s, 0, 1);
        Assert.Equal(s, new ValueNoise(9).Sample3(3.5, 9.25, 1.75, 8, 5));
    }
}
