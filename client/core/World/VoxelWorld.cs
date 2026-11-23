namespace Hangbar.Core.World;

/// <summary>Block store split into 16³ chunks; chunks that were never written stay unallocated (all air).</summary>
public sealed class VoxelWorld
{
    public const int ChunkSize = 16;

    public int SizeX { get; }
    public int SizeY { get; }
    public int SizeZ { get; }
    public int ChunksX { get; }
    public int ChunksY { get; }
    public int ChunksZ { get; }
    public int AllocatedChunks { get; private set; }

    readonly ushort[]?[] _chunks;

    public VoxelWorld(int sizeX, int sizeY, int sizeZ)
    {
        SizeX = sizeX; SizeY = sizeY; SizeZ = sizeZ;
        ChunksX = (sizeX + ChunkSize - 1) / ChunkSize;
        ChunksY = (sizeY + ChunkSize - 1) / ChunkSize;
        ChunksZ = (sizeZ + ChunkSize - 1) / ChunkSize;
        _chunks = new ushort[]?[ChunksX * ChunksY * ChunksZ];
    }

    bool InBounds(int x, int y, int z) => x >= 0 && y >= 0 && z >= 0 && x < SizeX && y < SizeY && z < SizeZ;

    static int Local(int x, int y, int z) => ((y % ChunkSize) * ChunkSize + z % ChunkSize) * ChunkSize + x % ChunkSize;

    int ChunkIndex(int x, int y, int z) => ((y / ChunkSize) * ChunksZ + z / ChunkSize) * ChunksX + x / ChunkSize;

    public ushort Get(int x, int y, int z)
    {
        if (!InBounds(x, y, z)) return Block.Air;
        var c = _chunks[ChunkIndex(x, y, z)];
        return c is null ? Block.Air : c[Local(x, y, z)];
    }

    public void Set(int x, int y, int z, ushort b)
    {
        if (!InBounds(x, y, z)) return;
        int ci = ChunkIndex(x, y, z);
        var c = _chunks[ci];
        if (c is null)
        {
            if (b == Block.Air) return;
            c = _chunks[ci] = new ushort[ChunkSize * ChunkSize * ChunkSize];
            AllocatedChunks++;
        }
        c[Local(x, y, z)] = b;
    }

    /// <summary>Surface = photo color, 3 blocks of dirt (×0.6), rock below (×0.35 + gray 0x50 ×0.65); water columns filled to the water level.</summary>
    public static VoxelWorld FromFileMap(FileMap map, int sizeY = 48)
    {
        var w = new VoxelWorld(map.Size, sizeY, map.Size);
        for (int z = 0; z < map.Size; z++)
            for (int x = 0; x < map.Size; x++)
            {
                int i = map.Index(x, z), h = Math.Min(map.Height[i], sizeY);
                uint c = map.Color[i];
                ushort surface = Block.Solid(c), dirt = Block.Solid(Mix(c, 0.6, 0)), rock = Block.Solid(Mix(c, 0.35, 0x50 * 0.65));
                for (int y = 0; y < h; y++)
                    w.Set(x, y, z, y == h - 1 ? surface : y >= h - 4 ? dirt : rock);
                if (map.Water[i])
                    for (int y = h; y < Math.Min(map.WaterLevel, sizeY); y++) w.Set(x, y, z, Block.Water);
            }
        return w;
    }

    static uint Mix(uint rgb, double k, double add)
    {
        uint Ch(int s) => (uint)Math.Clamp((int)Math.Round(((rgb >> s) & 0xFF) * k + add), 0, 255);
        return Ch(16) << 16 | Ch(8) << 8 | Ch(0);
    }
}
