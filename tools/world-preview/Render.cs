using Hangbar.Core.World;
using Hangbar.Core.World.Gen;

namespace WorldPreview;

/// <summary>The four preview pictures of a generated world (v2 design §6-9). Rough on purpose: the game shows the real look.</summary>
public static class Render
{
    const uint Sky = 0xBFD9EE, WaterColor = 0x3A7BD5;

    public static Canvas Iso(GeneratedWorld g)
    {
        var w = g.World;
        int n = w.SizeX, h = w.SizeY;
        var c = new Canvas(n * 4 + 8, n * 2 + h * 2 + 8, Sky);
        int offX = n * 2 + 4, offY = h * 2 + 4;
        var tops = Tops(w);
        for (int s = 0; s < 2 * n - 1; s++)
            for (int x = Math.Max(0, s - n + 1); x <= Math.Min(n - 1, s); x++)
            {
                int z = s - x, top = tops[z * n + x];
                if (top < 0) continue;
                for (int y = 0; y <= top; y++)
                {
                    uint b = w.Get(x, y, z);
                    if (b == Block.Air) continue;
                    // only blocks with a face open to the air in front or above show (overhangs no longer hide walls)
                    bool open = x + 1 >= n || z + 1 >= n || w.Get(x + 1, y, z) == Block.Air || w.Get(x, y, z + 1) == Block.Air ||
                                y == top || w.Get(x, y + 1, z) == Block.Air;
                    if (!open) continue;
                    uint col = Block.IsWater(b) ? WaterColor : Block.ColorOf(b);
                    int sx = (x - z) * 2 + offX, sy = (x + z) - y * 2 + offY;
                    c.Fill(sx - 2, sy, 2, 2, Shade(col, 0.78));   // left side
                    c.Fill(sx, sy, 2, 2, Shade(col, 0.62));       // right side
                    if (y == top || w.Get(x, y + 1, z) == Block.Air) c.Fill(sx - 2, sy - 1, 4, 1, col); // top
                }
            }
        return c;
    }

    public static Canvas Top(GeneratedWorld g, int scale = 3)
    {
        var w = g.World;
        int n = w.SizeX;
        var c = new Canvas(n * scale, n * scale, Sky);
        var m = g.Surface;
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int i = m.Index(x, z);
                uint col = m.Color[i];
                int hNw = m.Height[m.Index(Math.Max(0, x - 1), Math.Max(0, z - 1))];
                col = Shade(col, Math.Clamp(1 + (m.Height[i] - hNw) * 0.06, 0.6, 1.3));
                if (m.Water[i]) col = Mix(col, WaterColor, 0.65);
                c.Fill(x * scale, z * scale, scale, scale, col);
            }
        Mark(c, g.Spawn.X, g.Spawn.Z, scale, 0xFF2020);
        Mark(c, g.Landmark.Center.X, g.Landmark.Center.Z, scale, 0xFFD000);
        Mark(c, g.Mural.Origin.X, g.Mural.Origin.Z, scale, 0xFF00FF);
        return c;
    }

    public static Canvas Slice(GeneratedWorld g, int scale = 3)
    {
        var w = g.World;
        int n = w.SizeX, h = w.SizeY, z = g.Spawn.Z;
        var c = new Canvas(n * scale, h * scale, Sky);
        for (int x = 0; x < n; x++)
            for (int y = 0; y < h; y++)
            {
                uint b = w.Get(x, y, z);
                if (b == Block.Air) continue;
                uint col = Block.IsWater(b) ? WaterColor : Block.ColorOf(b);
                c.Fill(x * scale, (h - 1 - y) * scale, scale, scale, col);
            }
        return c;
    }

    /// <summary>48×48 columns around the spawn, each top face drawn with its 8×8 BlockPattern texture.</summary>
    public static Canvas Close(GeneratedWorld g)
    {
        var w = g.World;
        const int span = 48, px = BlockPattern.Pixels;
        int x0 = Math.Clamp(g.Spawn.X - span / 2, 0, w.SizeX - span), z0 = Math.Clamp(g.Spawn.Z - span / 2, 0, w.SizeZ - span);
        var c = new Canvas(span * px, span * px, Sky);
        var tops = Tops(w);
        for (int dz = 0; dz < span; dz++)
            for (int dx = 0; dx < span; dx++)
            {
                int x = x0 + dx, z = z0 + dz, y = tops[z * w.SizeX + x];
                if (y < 0) continue;
                uint b = w.Get(x, y, z);
                if (Block.IsWater(b))
                {
                    c.Fill(dx * px, dz * px, px, px, WaterColor);
                    continue;
                }
                uint hash = BlockPattern.Hash(x, y, z, 2);
                for (int ty = 0; ty < px; ty++)
                    for (int tx = 0; tx < px; tx++)
                    {
                        var t = BlockPattern.Sample(Block.MaterialOf(b), Block.StateOf(b), tx, ty, hash);
                        c.Set(dx * px + tx, dz * px + ty, Mix(Shade(Block.ColorOf(b), t.Shade), BlockPattern.MossColor, t.Moss));
                    }
            }
        return c;
    }

    static int[] Tops(VoxelWorld w)
    {
        var t = new int[w.SizeX * w.SizeZ];
        for (int z = 0; z < w.SizeZ; z++)
            for (int x = 0; x < w.SizeX; x++)
            {
                int y = w.SizeY - 1;
                while (y >= 0 && w.Get(x, y, z) == Block.Air) y--;
                t[z * w.SizeX + x] = y;
            }
        return t;
    }

    static void Mark(Canvas c, int x, int z, int scale, uint rgb) => c.Fill(x * scale - 3, z * scale - 3, scale + 6, scale + 6, rgb);

    public static uint Shade(uint c, double k)
    {
        uint Ch(int s) => (uint)Math.Clamp(Math.Round((c >> s & 0xFF) * k), 0, 255);
        return Ch(16) << 16 | Ch(8) << 8 | Ch(0);
    }

    public static uint Mix(uint a, uint b, double t)
    {
        uint Ch(int s) => (uint)Math.Round((a >> s & 0xFF) + ((double)(b >> s & 0xFF) - (a >> s & 0xFF)) * t);
        return Ch(16) << 16 | Ch(8) << 8 | Ch(0);
    }
}
