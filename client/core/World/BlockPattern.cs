namespace Hangbar.Core.World;

/// <summary>One texture pixel: final colour = mix(blockColour × Shade, MossColor, Moss).</summary>
public readonly record struct Texel(float Shade, float Moss);

/// <summary>
/// The pixel texture function of the v2 design: an 8×8 pattern per block face, shaded around the block's own colour.
/// The preview tool's close-up picture uses it. The game no longer does: since the soft style (user decision
/// 2026-11-25) game/shaders/block.gdshader draws blocks without a pattern and shows wear more gently, keeping
/// <see cref="MossColor"/>. Integer and float arithmetic only, so it can be ported to a shader again if needed.
/// Motif: compression blotches — stone-like materials are made of 2×2 blocks, the "pixels" of a JPEG gone wrong.
/// Photo mural blocks (Pixel) stay flat.
/// </summary>
public static class BlockPattern
{
    public const int Pixels = 8;
    public const uint MossColor = 0x4E7A2F;

    /// <summary>Per-face variation seed. The formula is fixed: the shader repeats it.</summary>
    public static uint Hash(int bx, int by, int bz, int face)
    {
        unchecked
        {
            uint h = (uint)bx * 73856093u ^ (uint)by * 19349663u ^ (uint)bz * 83492791u ^ (uint)face * 2654435761u;
            h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
            return h;
        }
    }

    /// <summary>0..1 pseudo-random value for (hash, a, b, salt).</summary>
    static float Rand(uint hash, int a, int b, uint salt)
    {
        unchecked
        {
            uint h = hash ^ (uint)a * 374761393u ^ (uint)b * 668265263u ^ salt * 2246822519u;
            h ^= h >> 13; h *= 1274126177u; h ^= h >> 16;
            return (h & 0xFFFFu) / 65535f;
        }
    }

    static float Signed(uint hash, int a, int b, uint salt) => Rand(hash, a, b, salt) * 2f - 1f;

    public static Texel Sample(Material m, BlockState s, int px, int py, uint hash)
    {
        if (m is Material.Pixel or Material.Air or Material.Water) return new Texel(1f, 0f);
        uint salt = (uint)m * 0x9E3779B9u;
        float shade = Base(m, px, py, hash, salt);
        float moss = 0f;
        if (s == BlockState.Cracked && OnCrack(px, py, hash)) shade *= 0.62f;
        else if (s == BlockState.Mossy) moss = MossAt(px, py, hash);
        else if (s == BlockState.Worn) shade = 1f + (shade - 1f) * 0.4f + 0.02f;
        return new Texel(shade, moss);
    }

    static float Base(Material m, int px, int py, uint hash, uint salt)
    {
        switch (m)
        {
            case Material.Stone: return Blotch(px, py, hash, salt, 0.12f, 0.88f);
            case Material.DeepStone: return Blotch(px, py, hash, salt, 0.14f, 0.85f);
            case Material.Bedrock: return Blotch(px, py, hash, salt, 0.2f, 0.9f);
            case Material.Concrete: return Blotch(px, py, hash, salt, 0.05f, 0.95f);
            case Material.Soil:
                return Rand(hash, px, py, salt + 1u) > 0.86f ? 0.8f : 1f + 0.14f * Signed(hash, px, py, salt);
            case Material.Grass:
            {
                float blade = Rand(hash, px, 0, salt + 2u) > 0.6f && py < 3 ? 0.08f : 0f;
                return 1f + 0.1f * Signed(hash, px, py, salt) + blade;
            }
            case Material.Sand: return 1f + 0.05f * Signed(hash, px, py, salt);
            case Material.Snow: return Rand(hash, px, py, salt + 3u) > 0.94f ? 1.12f : 1f + 0.03f * Signed(hash, px, py, salt);
            case Material.Wood:
            {
                float row = 1f + 0.08f * Signed(hash, py, 0, salt);
                int offset = (int)(Rand(hash, 0, 0, salt + 4u) * 4f);
                if ((py + offset) % 4 == 0) row *= 0.9f;
                return row + 0.03f * Signed(hash, px, py, salt + 5u);
            }
            case Material.Brick:
            {
                int course = py / 4;
                bool mortar = py % 4 == 3 || px == (course % 2 == 0 ? 0 : 4);
                if (mortar) return 0.76f;
                int brick = (px + (course % 2 == 0 ? 0 : 4)) / 4;
                return 1.06f + 0.06f * Signed(hash, course, brick, salt);
            }
            case Material.Tile:
                return px % 4 == 0 || py % 4 == 0 ? 0.84f : 1.06f + 0.03f * Signed(hash, px / 4, py / 4, salt);
            case Material.Metal:
            {
                bool rivet = (px == 1 || px == 6) && (py == 1 || py == 6);
                return rivet ? 0.78f : 1f + 0.05f * Signed(hash, py, 0, salt) + 0.02f * Signed(hash, px, py, salt + 6u);
            }
            case Material.Glass:
            {
                bool frame = px == 0 || py == 0 || px == 7 || py == 7;
                return frame ? 0.82f : px == py ? 1.12f : 1f;
            }
            case Material.DarkGlass:
            {
                bool frame = px == 0 || py == 0 || px == 7 || py == 7;
                return frame ? 0.85f : px + py == 7 ? 1.1f : 1f;
            }
            case Material.Cloth:
                return ((px + py) % 2 == 0 ? 0.95f : 1.05f) + 0.02f * Signed(hash, px, py, salt);
            default:
                return 1f;
        }
    }

    /// <summary>Compression blotches: one value per 2×2 cell, some cells noticeably darker.</summary>
    static float Blotch(int px, int py, uint hash, uint salt, float amp, float dark)
    {
        int cx = px / 2, cy = py / 2;
        float v = 1f + amp * Signed(hash, cx, cy, salt);
        return Rand(hash, cx, cy, salt + 7u) > 0.85f ? v * dark : v;
    }

    /// <summary>A crack runs from the top row to the bottom, drifting one pixel sideways at most per row.</summary>
    static bool OnCrack(int px, int py, uint hash)
    {
        int x = 1 + (int)(Rand(hash, 11, 0, 99u) * 6f);
        for (int r = 1; r <= py; r++)
        {
            float d = Rand(hash, 11, r, 98u);
            x += d < 0.33f ? -1 : d > 0.66f ? 1 : 0;
            x = Math.Clamp(x, 0, Pixels - 1);
        }
        return px == x;
    }

    /// <summary>Moss grows from the top edge: full top row, most of the second, drips below.</summary>
    static float MossAt(int px, int py, uint hash)
    {
        if (py == 0) return 0.7f;
        if (py == 1) return 0.45f;
        int drip = 1 + (int)(Rand(hash, px, 21, 97u) * 3f);
        return Rand(hash, px, 22, 96u) > 0.65f && py <= drip ? 0.3f : 0f;
    }
}
