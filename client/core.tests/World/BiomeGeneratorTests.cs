using Hangbar.Core.World;

namespace Hangbar.Core.Tests.World;

public class BiomeGeneratorTests
{
    const int N = 192, C = N / 2;
    static readonly uint[] MeadowPalette = Palette.EnsureContrast(Palette.Extract(ImageFit.Prepare(SamplePhotos.Meadow(), N)));
    static readonly Mood Calm = new(0.6f, 0.4f, 0.5f);

    public static IEnumerable<object[]> AllBiomes => Enum.GetValues<Biome>().Select(b => new object[] { b });

    static BiomeMap Make(Biome b, ulong seed = 12345, uint[]? palette = null) =>
        BiomeGenerator.Generate(b, palette ?? MeadowPalette, Calm, seed);

    static PixelImage Photo => ImageFit.Prepare(SamplePhotos.Meadow(), N);

    // ---- walking: a surface is an open cell with a solid block under it and 7 free blocks (1.75 m) of headroom

    static List<int>[] Surfaces(VoxelWorld w)
    {
        var s = new List<int>[w.SizeX * w.SizeZ];
        for (int z = 0; z < w.SizeZ; z++)
            for (int x = 0; x < w.SizeX; x++)
            {
                var list = s[z * w.SizeX + x] = new List<int>();
                for (int y = 1; y <= w.SizeY; y++)
                {
                    if (!Block.IsSolid(w.Get(x, y - 1, z))) continue;
                    bool free = true;
                    for (int k = 0; k < 7 && free; k++) free = !Block.IsSolid(w.Get(x, y + k, z));
                    if (free) list.Add(y);
                }
            }
        return s;
    }

    /// <summary>Columns reached from (sx, sy, sz) by stepping to a side neighbour at most 2 blocks up or down.</summary>
    static bool[] Walk(VoxelWorld w, int sx, int sy, int sz)
    {
        var surfaces = Surfaces(w);
        Assert.Contains(sy, surfaces[sz * w.SizeX + sx]); // the spawn is open ground, not inside a building
        var reached = new bool[w.SizeX * w.SizeZ];
        var seen = new HashSet<(int, int, int)> { (sx, sy, sz) };
        var queue = new Queue<(int X, int Y, int Z)>();
        queue.Enqueue((sx, sy, sz));
        while (queue.Count > 0)
        {
            var (x, y, z) = queue.Dequeue();
            reached[z * w.SizeX + x] = true;
            foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nx = x + dx, nz = z + dz;
                if (nx < 0 || nz < 0 || nx >= w.SizeX || nz >= w.SizeZ) continue;
                foreach (int ny in surfaces[nz * w.SizeX + nx])
                    if (Math.Abs(ny - y) <= 2 && seen.Add((nx, ny, nz))) queue.Enqueue((nx, ny, nz));
            }
        }
        return reached;
    }

    static bool[] WalkFromSpawn(BiomeMap m, out VoxelWorld w)
    {
        w = VoxelWorld.FromBiomeMap(m, Photo);
        return Walk(w, C, m.Terrain.StandHeight(C, C), C);
    }

    // ---- every biome

    [Theory, MemberData(nameof(AllBiomes))]
    public void Same_input_gives_same_map(Biome b)
    {
        BiomeMap m1 = Make(b), m2 = Make(b);
        Assert.Equal(m1.Terrain.Height, m2.Terrain.Height);
        Assert.Equal(m1.Terrain.Color, m2.Terrain.Color);
        Assert.Equal(m1.Terrain.Water, m2.Terrain.Water);
        Assert.Equal(m1.Buildings, m2.Buildings);
        Assert.Equal(m1.Props, m2.Props);
        Assert.Equal(b, m1.Biome);
        Assert.Equal(N, m1.Terrain.Size);
    }

    [Theory, MemberData(nameof(AllBiomes))]
    public void Every_biome_is_mostly_walkable(Biome b)
    {
        var reached = WalkFromSpawn(Make(b), out _);
        double share = reached.Count(r => r) / (double)reached.Length;
        Assert.True(share >= 0.7, $"{b}: {share:P0} of the map is reachable on foot");
    }

    [Theory, MemberData(nameof(AllBiomes))]
    public void Neighbour_columns_differ_by_at_most_two_blocks(Biome b)
    {
        var t = Make(b).Terrain;
        for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
            {
                if (x + 1 < N) Assert.InRange(Math.Abs(t.Height[t.Index(x, z)] - t.Height[t.Index(x + 1, z)]), 0, 2);
                if (z + 1 < N) Assert.InRange(Math.Abs(t.Height[t.Index(x, z)] - t.Height[t.Index(x, z + 1)]), 0, 2);
            }
    }

    [Theory, MemberData(nameof(AllBiomes))]
    public void Props_stay_off_the_spawn_and_the_water(Biome b)
    {
        var m = Make(b);
        Assert.All(m.Props, p =>
        {
            Assert.False(Math.Abs(p.X - C) <= 6 && Math.Abs(p.Z - C) <= 6, $"{p.Kind} at the spawn");
            Assert.False(m.Terrain.Water[m.Terrain.Index(p.X, p.Z)], $"{p.Kind} on water");
        });
    }

    [Theory, MemberData(nameof(AllBiomes))]
    public void Nothing_stands_between_the_spawn_and_the_mural(Biome b)
    {
        var m = Make(b);
        int x0 = C - 28, x1 = C + 28, z0 = C - 34, z1 = C - 7; // mural plaza through to the spawn clearing
        Assert.DoesNotContain(m.Buildings, k => k.X <= x1 && k.X + k.W - 1 >= x0 && k.Z <= z1 && k.Z + k.D - 1 >= z0);
        Assert.DoesNotContain(m.Props, p => p.X >= x0 && p.X <= x1 && p.Z >= z0 && p.Z <= z1);
    }

    // ---- dark and gray photos (plan Review Focus 5): the world, not only the palette, must stay readable

    static double Luma(uint rgb) => (0.299 * (rgb >> 16 & 0xFF) + 0.587 * (rgb >> 8 & 0xFF) + 0.114 * (rgb & 0xFF)) / 255.0;

    static PixelImage DarkPhoto(string kind)
    {
        if (kind == "gray") return BiomeTests.Solid(N, 120, 120, 125);
        if (kind == "night") return BiomeTests.Solid(N, 20, 20, 30);
        var px = new uint[N * N]; // night street: dark, with about one seventh lamp-lit
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
                px[y * N + x] = (x / 12 + y / 16) % 7 == 0 ? PixelImage.Pack(250, 215, 140) : PixelImage.Pack(18 + (x * 7 + y * 3) % 9, 20, 28);
        return new PixelImage(N, N, px);
    }

    /// <summary>Raw palette on purpose: the generator must apply the contrast guard itself.</summary>
    static BiomeMap FromDarkPhoto(string kind, Biome b, out PixelImage prepared)
    {
        prepared = ImageFit.Prepare(DarkPhoto(kind), N);
        return BiomeGenerator.Generate(b, Palette.Extract(prepared), MoodReader.Read(prepared), 7);
    }

    [Theory]
    [InlineData("gray", Biome.Field)] [InlineData("gray", Biome.Forest)] [InlineData("gray", Biome.City)]
    [InlineData("night", Biome.Field)] [InlineData("night", Biome.Forest)] [InlineData("night", Biome.City)]
    [InlineData("street", Biome.Field)] [InlineData("street", Biome.Forest)] [InlineData("street", Biome.City)]
    public void Dark_or_gray_photos_still_give_a_readable_world(string kind, Biome b)
    {
        var m = FromDarkPhoto(kind, b, out var prepared);
        var w = VoxelWorld.FromBiomeMap(m, prepared);
        var tops = new List<double>();
        for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
            {
                int y = w.SizeY - 1;
                while (y > 0 && !Block.IsSolid(w.Get(x, y, z))) y--;
                tops.Add(Luma(Block.ColorOf(w.Get(x, y, z))));
            }
        tops.Sort();
        double p10 = tops[tops.Count / 10], p90 = tops[tops.Count * 9 / 10];
        Assert.True(p90 - p10 >= 0.15, $"{kind} {b}: top faces only span {p10:0.00}..{p90:0.00}");
    }

    [Theory]
    [InlineData("gray", Biome.Field)] [InlineData("gray", Biome.Forest)]
    [InlineData("night", Biome.Field)] [InlineData("night", Biome.Forest)]
    [InlineData("street", Biome.Field)] [InlineData("street", Biome.Forest)]
    public void Trees_stand_out_from_the_ground(string kind, Biome b)
    {
        var m = FromDarkPhoto(kind, b, out _);
        double Median(IEnumerable<double> v) { var s = v.OrderBy(x => x).ToList(); return s[s.Count / 2]; }
        double ground = Median(m.Terrain.Color.Where((_, i) => !m.Terrain.Water[i]).Select(Luma));
        double trees = Median(m.Props.Where(p => p.Kind == PropKind.Tree).Select(p => Luma(p.Color)));
        Assert.True(Math.Abs(trees - ground) >= 0.1, $"{kind} {b}: trees {trees:0.00} on ground {ground:0.00}");
    }

    [Fact]
    public void Different_seeds_give_different_worlds() =>
        Assert.NotEqual(Make(Biome.Field, 1).Terrain.Height, Make(Biome.Field, 2).Terrain.Height);

    [Fact]
    public void World_has_the_photo_mural_north_of_the_spawn()
    {
        var w = VoxelWorld.FromBiomeMap(Make(Biome.Field), Photo);
        int z0 = C - 30;
        Assert.True(Block.IsSolid(w.Get(C, 30, z0)), "mural wall 30 blocks north of the spawn");
        Assert.False(Block.IsSolid(w.Get(C, 30, z0 + 1)), "one block thick");
        Assert.False(Block.IsSolid(w.Get(C, 30, z0 - 1)), "one block thick");
    }

    // ---- per biome

    [Fact]
    public void Road_crosses_the_map()
    {
        uint asphalt = 0x262626;
        uint[] palette = { asphalt, 0x5A4632, 0x3C7832, 0x78A050, 0xB4C8DC, 0xF0F0E6 };
        var t = Make(Biome.Road, palette: palette).Terrain;
        var road = t.Color.Select(c => c == asphalt).ToArray();

        // connected from one edge to the opposite one
        bool Crosses(Func<int, int, bool> start, Func<int, int, bool> end)
        {
            var seen = new bool[N * N];
            var q = new Queue<(int X, int Z)>();
            for (int z = 0; z < N; z++)
                for (int x = 0; x < N; x++)
                    if (road[t.Index(x, z)] && start(x, z)) { seen[t.Index(x, z)] = true; q.Enqueue((x, z)); }
            while (q.Count > 0)
            {
                var (x, z) = q.Dequeue();
                if (end(x, z)) return true;
                foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, nz = z + dz;
                    if (nx < 0 || nz < 0 || nx >= N || nz >= N || !road[t.Index(nx, nz)] || seen[t.Index(nx, nz)]) continue;
                    seen[t.Index(nx, nz)] = true;
                    q.Enqueue((nx, nz));
                }
            }
            return false;
        }
        bool alongX = Crosses((x, _) => x == 0, (x, _) => x == N - 1);
        bool alongZ = Crosses((_, z) => z == 0, (_, z) => z == N - 1);
        Assert.True(alongX || alongZ, "the road runs from one map edge to the opposite edge");

        // 12 cells wide and level across its width
        for (int i = 0; i < N; i++)
        {
            var cross = Enumerable.Range(0, N).Select(j => alongX ? t.Index(i, j) : t.Index(j, i)).Where(k => road[k]).ToList();
            Assert.Equal(12, cross.Count);
            Assert.Single(cross.Select(k => t.Height[k]).Distinct());
        }
    }

    [Fact]
    public void Water_biome_is_40_to_60_percent_water()
    {
        foreach (ulong seed in new ulong[] { 1, 2, 3, 12345 })
        {
            double share = Make(Biome.Water, seed).Terrain.Water.Count(w => w) / (double)(N * N);
            Assert.InRange(share, 0.40, 0.60);
        }
    }

    [Fact]
    public void City_has_buildings_and_streets()
    {
        var m = Make(Biome.City);
        Assert.True(m.Buildings.Count >= 8, $"{m.Buildings.Count} buildings");
        Assert.All(m.Buildings, b => Assert.InRange(b.Height, 12, 36));

        var reached = WalkFromSpawn(m, out _);
        bool Edge(Func<int, bool> onEdge) => Enumerable.Range(0, N * N).Any(i => reached[i] && onEdge(i));
        Assert.True(Edge(i => i % N == 0) && Edge(i => i % N == N - 1) && Edge(i => i / N == 0) && Edge(i => i / N == N - 1),
            "streets join the spawn to all four map edges");
    }

    [Fact]
    public void Indoor_has_walls_and_climbable_furniture()
    {
        var m = Make(Biome.Indoor);
        var reached = WalkFromSpawn(m, out var w);
        for (int i = 0; i < N; i++)
            foreach (var (x, z) in new[] { (i, 0), (i, N - 1), (0, i), (N - 1, i) })
                Assert.True(Block.IsSolid(w.Get(x, 47, z)), $"wall at ({x},{z}) is 40 blocks over the floor");

        var furniture = m.Buildings.Where(b => b.Height is >= 16 and <= 24 && b.W >= 10 && b.D >= 10).ToList(); // not the steps
        Assert.True(furniture.Count >= 3, $"{furniture.Count} pieces of furniture");
        Assert.All(furniture, f =>
        {
            bool topReached = false;
            for (int z = f.Z; z < f.Z + f.D; z++)
                for (int x = f.X; x < f.X + f.W; x++)
                    topReached |= reached[z * N + x];
            Assert.True(topReached, $"furniture at ({f.X},{f.Z}) can be climbed by its steps");
        });
    }

    [Fact]
    public void Transit_has_rails_across_the_map()
    {
        var m = Make(Biome.Transit);
        var w = VoxelWorld.FromBiomeMap(m, Photo);
        var railRows = Enumerable.Range(0, N).Where(z => Enumerable.Range(0, N).All(x => Block.IsSolid(w.Get(x, 8, z)))).ToList();
        Assert.True(railRows.Count >= 4, $"{railRows.Count} rails cross the map (two tracks)");
        Assert.Contains(m.Buildings, b => b.Height == 4); // platform
        Assert.Contains(m.Buildings, b => b.Lift >= 7 && b.Z <= railRows.Min() && b.Z + b.D > railRows.Max()); // tunnel arch over every rail
    }

    [Fact]
    public void Forest_has_many_trees() =>
        Assert.True(Make(Biome.Forest).Props.Count(p => p.Kind == PropKind.Tree) >= N * N / 120);

    [Fact]
    public void Mural_shows_the_photo()
    {
        var px = new uint[64 * 64];
        for (int i = 0; i < px.Length; i++) px[i] = i % 64 < 32 ? PixelImage.Pack(220, 40, 40) : PixelImage.Pack(40, 60, 200);
        var w = new VoxelWorld(128, 64, 128);
        for (int z = 0; z < 128; z++)
            for (int x = 0; x < 128; x++)
                for (int y = 0; y < 8; y++) w.Set(x, y, z, Block.Solid(0x507030));

        Mural.Stamp(w, new PixelImage(64, 64, px), 10, 20, 64, 48);

        uint topLeft = Block.ColorOf(w.Get(10, 8 + 47, 20)), topRight = Block.ColorOf(w.Get(73, 8 + 47, 20));
        Assert.True((topLeft >> 16 & 0xFF) > 150 && (topLeft & 0xFF) < 90, $"top left {topLeft:x6} is red");
        Assert.True((topRight & 0xFF) > 150 && (topRight >> 16 & 0xFF) < 90, $"top right {topRight:x6} is blue");
        Assert.True(Block.IsSolid(w.Get(10, 8, 20)), "the bottom photo row stands on the ground");
        Assert.False(Block.IsSolid(w.Get(10, 8 + 48, 20)), "48 blocks tall");
        Assert.False(Block.IsSolid(w.Get(74, 20, 20)), "64 blocks wide");
    }
}
