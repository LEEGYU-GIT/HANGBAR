namespace Hangbar.Core.World.Gen;

/// <summary>
/// Caves, groundwater and burial spots (v2 design: underground). Caves are worms: 14–20 tunnels of 60–140 steps with a
/// radius of 2–4 blocks between y 6 and the ground − 8; at least two start at the ground so the caves open to the sky.
/// Protected columns, the edge band and bedrock are never dug. The lowest stretch of some tunnels holds 1–3 blocks of
/// water. Exactly 12 burial spots sit in solid ground 3–20 blocks deep, at least 8 columns apart.
/// </summary>
public static class Underground
{
    const int Burials = 12;

    public static void Carve(SceneContext ctx)
    {
        var w = ctx.World;
        var rng = new Rng(Rng.Mix(ctx.Seed ^ 0xCA7E5UL));
        int worms = rng.NextInt(14, 21);
        for (int k = 0; k < worms; k++) Worm(ctx, w, rng, entrance: k < 2 + (rng.Next() < 0.5 ? 1 : 0));
        PlaceBurials(ctx, w, rng);
    }

    static bool Protected(SceneContext ctx, int x, int z)
    {
        int n = SceneContext.Size;
        if (x < 0 || z < 0 || x >= n || z >= n) return true;
        int d = Math.Min(Math.Min(x, z), Math.Min(n - 1 - x, n - 1 - z));
        return d < SceneContext.EdgeBand || ctx.NoCaves[ctx.Index(x, z)];
    }

    static void Worm(SceneContext ctx, VoxelWorld w, Rng rng, bool entrance)
    {
        int n = SceneContext.Size, band = SceneContext.EdgeBand + 6;
        double x = 0, y = 0, z = 0;
        bool found = false;
        for (int tries = 0; tries < 200 && !found; tries++)
        {
            x = rng.NextInt(band, n - band);
            z = rng.NextInt(band, n - band);
            int i = ctx.Index((int)x, (int)z);
            if (Protected(ctx, (int)x, (int)z) || ctx.Path[i] || ctx.WaterTop[i] > 0) continue;
            int surface = ctx.Surface[i];
            if (surface - 8 <= 6) continue;
            y = entrance ? surface - 1 : rng.NextInt(6, surface - 8);
            found = true;
        }
        if (!found) return;

        double yaw = rng.Next() * Math.Tau, pitch = entrance ? -0.9 : (rng.Next() - 0.5) * 0.5;
        int length = rng.NextInt(60, 141);
        double radius = rng.NextInt(2, 5);
        var carved = new List<BlockPos>();
        for (int step = 0; step < length; step++)
        {
            int ci = ctx.Index(Math.Clamp((int)x, 0, n - 1), Math.Clamp((int)z, 0, n - 1));
            int floor = 6, ceiling = ctx.Surface[ci] - 8;
            bool nearTop = entrance && step < 14;
            Sphere(ctx, w, x, y, z, radius, nearTop, carved);

            yaw += (rng.Next() - 0.5) * 0.5;
            pitch = nearTop ? -0.9 : Math.Clamp(pitch * 0.9 + (rng.Next() - 0.5) * 0.3, -0.5, 0.5);
            x += Math.Cos(yaw) * Math.Cos(pitch);
            z += Math.Sin(yaw) * Math.Cos(pitch);
            y += Math.Sin(pitch);
            if (!nearTop) y = Math.Clamp(y, floor, Math.Max(floor, ceiling));
            radius = Math.Clamp(radius + (rng.Next() - 0.5) * 0.6, 2, 4);
            if (x < band || z < band || x >= n - band || z >= n - band) yaw += Math.PI / 2; // turn back from the edge band
        }
        Groundwater(w, rng, carved);
    }

    static void Sphere(SceneContext ctx, VoxelWorld w, double cx, double cy, double cz, double r, bool nearTop, List<BlockPos> carved)
    {
        int ri = (int)Math.Ceiling(r);
        for (int dz = -ri; dz <= ri; dz++)
            for (int dx = -ri; dx <= ri; dx++)
            {
                int x = (int)Math.Round(cx) + dx, z = (int)Math.Round(cz) + dz;
                if (Protected(ctx, x, z)) continue;
                int i = ctx.Index(x, z);
                // an entrance may break the ground; everywhere else keep a roof of 8 blocks, and never dig under water or paths near the top
                int top = nearTop && !ctx.Path[i] && ctx.WaterTop[i] == 0 ? ctx.Surface[i] + 2 : ctx.Surface[i] - 8;
                for (int dy = -ri; dy <= ri; dy++)
                {
                    int y = (int)Math.Round(cy) + dy;
                    if (y < 2 || y >= top || dx * dx + dy * dy + dz * dz > r * r) continue;
                    uint b = w.Get(x, y, z);
                    if (!Block.IsBreakable(b)) continue;
                    w.Set(x, y, z, Block.Air);
                    carved.Add(new BlockPos(x, y, z));
                }
            }
    }

    /// <summary>Some tunnels hold 1–3 blocks of water in their lowest stretch.</summary>
    static void Groundwater(VoxelWorld w, Rng rng, List<BlockPos> carved)
    {
        if (carved.Count == 0 || rng.Next() < 0.35) return;
        int low = carved.Min(p => p.Y), depth = rng.NextInt(1, 4);
        foreach (var p in carved)
            if (p.Y < low + depth && w.Get(p.X, p.Y, p.Z) == Block.Air)
                w.Set(p.X, p.Y, p.Z, Block.Water);
    }

    static void PlaceBurials(SceneContext ctx, VoxelWorld w, Rng rng)
    {
        int n = SceneContext.Size;
        for (int tries = 0; tries < 20000 && ctx.BurialSpots.Count < Burials; tries++)
        {
            int x = rng.NextInt(0, n), z = rng.NextInt(0, n);
            if (Protected(ctx, x, z)) continue;
            int y = ctx.Surface[ctx.Index(x, z)] - rng.NextInt(3, 21);
            if (y < 2 || !Block.IsSolid(w.Get(x, y, z))) continue;
            if (ctx.BurialSpots.Any(p => (p.X - x) * (p.X - x) + (p.Z - z) * (p.Z - z) < 64)) continue;
            ctx.BurialSpots.Add(new BlockPos(x, y, z));
        }
    }
}
