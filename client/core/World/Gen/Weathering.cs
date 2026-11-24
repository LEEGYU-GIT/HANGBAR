namespace Hangbar.Core.World.Gen;

/// <summary>
/// Wear on the finished world (v2 design: block variety). Exact shares, chosen by a seeded hash of the position:
/// 30% of old structures' blocks crack, 5% of natural stone touching air cracks, 40% of the ground within 2 columns
/// of water grows moss (paths excluded), 70% of path tops are worn. Photo, glass and bedrock blocks never change.
/// </summary>
public static class Weathering
{
    public static void Apply(SceneContext ctx)
    {
        var w = ctx.World;
        int n = SceneContext.Size;
        bool Keeps(uint b) => !Block.IsSolid(b) || Block.MaterialOf(b) is Material.Pixel or Material.Glass or Material.Bedrock;

        var old = new List<BlockPos>();
        foreach (var p in ctx.OldParts)
            for (int y = p.Min.Y; y <= p.Max.Y; y++)
                for (int z = p.Min.Z; z <= p.Max.Z; z++)
                    for (int x = p.Min.X; x <= p.Max.X; x++)
                        if (!Keeps(w.Get(x, y, z))) old.Add(new BlockPos(x, y, z));
        Mark(ctx, old.Distinct().ToList(), 0.30, BlockState.Cracked, 1);

        bool InStructure(int x, int y, int z) => ctx.Structures.Any(s =>
            x >= s.Min.X && x <= s.Max.X && y >= s.Min.Y && y <= s.Max.Y && z >= s.Min.Z && z <= s.Max.Z);
        var bare = new List<BlockPos>();
        for (int y = 2; y < w.SizeY; y++)
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    uint b = w.Get(x, y, z);
                    if (Block.MaterialOf(b) is not (Material.Stone or Material.DeepStone)) continue;
                    if (!TouchesAir(w, x, y, z) || InStructure(x, y, z)) continue;
                    bare.Add(new BlockPos(x, y, z));
                }
        Mark(ctx, bare, 0.05, BlockState.Cracked, 2);

        var top = new int[n * n];
        var wet = new bool[n * n];
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int y = w.SizeY - 1;
                while (y > 0 && w.Get(x, y, z) == Block.Air) y--;
                wet[z * n + x] = Block.IsWater(w.Get(x, y, z));
                while (y > 0 && !Block.IsSolid(w.Get(x, y, z))) y--;
                top[z * n + x] = y;
            }
        var shore = new List<BlockPos>();
        var path = new List<BlockPos>();
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int i = z * n + x;
                var pos = new BlockPos(x, top[i], z);
                if (Keeps(w.Get(x, top[i], z))) continue;
                if (ctx.Path[i]) { path.Add(pos); continue; }
                if (wet[i]) continue;
                bool near = false;
                for (int dz = -2; dz <= 2 && !near; dz++)
                    for (int dx = -2; dx <= 2 && !near; dx++)
                    {
                        int nx = x + dx, nz = z + dz;
                        near = nx >= 0 && nz >= 0 && nx < n && nz < n && wet[nz * n + nx];
                    }
                if (near) shore.Add(pos);
            }
        Mark(ctx, shore, 0.40, BlockState.Mossy, 3);
        Mark(ctx, path, 0.70, BlockState.Worn, 4);
    }

    static bool TouchesAir(VoxelWorld w, int x, int y, int z) =>
        w.Get(x + 1, y, z) == Block.Air || w.Get(x - 1, y, z) == Block.Air || w.Get(x, y + 1, z) == Block.Air ||
        w.Get(x, y - 1, z) == Block.Air || w.Get(x, y, z + 1) == Block.Air || w.Get(x, y, z - 1) == Block.Air;

    /// <summary>Gives exactly round(share × count) of the cells the state; which ones follows a seeded hash.</summary>
    static void Mark(SceneContext ctx, List<BlockPos> cells, double share, BlockState state, ulong salt)
    {
        int take = (int)Math.Round(share * cells.Count);
        ulong Key(BlockPos p) => Rng.Mix(ctx.Seed ^ salt * 0x9E3779B97F4A7C15UL ^ (ulong)(uint)p.X ^ (ulong)(uint)p.Y << 20 ^ (ulong)(uint)p.Z << 40);
        foreach (var p in cells.OrderBy(Key).ThenBy(p => p.X).ThenBy(p => p.Y).ThenBy(p => p.Z).Take(take))
            ctx.World.Set(p.X, p.Y, p.Z, Block.WithState(ctx.World.Get(p.X, p.Y, p.Z), state));
    }
}
