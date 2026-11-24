namespace Hangbar.Core.World;

/// <summary>
/// A photo's biome, palette and seed → a file map with buildings and props (design §6-3, plan G0.5 Task 5).
/// Heights are in blocks over a base ground of 8; neighbouring columns never differ by more than 2 blocks, so
/// the 0.55 m step-up always works. The spawn (map centre) and the mural plaza north of it are kept flat and free.
/// </summary>
public static class BiomeGenerator
{
    public const int Ground = 8, WaterLevel = 8, SeaFloor = 5;
    /// <summary>The photo mural: this wide and tall, this many blocks north (−z) of the spawn (see VoxelWorld.FromBiomeMap).</summary>
    public const int MuralWidth = 48, MuralHeight = 36, MuralDistance = 30;

    public static BiomeMap Generate(Biome biome, uint[] palette, Mood mood, ulong seed, int size = 192)
    {
        if (palette.Length == 0) throw new ArgumentException("the palette is empty", nameof(palette));
        if (size < 128) throw new ArgumentOutOfRangeException(nameof(size), "a file map is at least 128 columns");
        palette = Palette.EnsureContrast(palette); // a gray or night photo still gives a readable world (Review Focus 5)
        var g = new Gen(palette, seed, seed ^ (ulong)(biome + 1) * 0x9E3779B97F4A7C15UL, size);
        switch (biome)
        {
            case Biome.Field: g.Field(); break;
            case Biome.Road: g.Road(); break;
            case Biome.Water: g.Water(); break;
            case Biome.Forest: g.Forest(); break;
            case Biome.City: g.City(); break;
            case Biome.Indoor: g.Indoor(); break;
            case Biome.Transit: g.Transit(); break;
            default: throw new ArgumentOutOfRangeException(nameof(biome));
        }
        return new BiomeMap(g.Terrain(), g.Buildings, g.Props, biome, mood);
    }

    sealed class Gen
    {
        const int NoPin = int.MinValue, Far = 1 << 20;

        readonly uint[] _pal;
        readonly ulong _worldSeed, _seed;
        readonly Rng _rng;
        readonly int _n, _c;
        readonly int[] _height, _pin;
        readonly uint[] _color;
        readonly bool[] _water, _blocked;
        /// <summary>Inclusive column rectangles: spawn clearing, mural plaza, and the open view between them.</summary>
        readonly (int X0, int X1, int Z0, int Z1) _spawn, _plaza, _view;

        public readonly List<Building> Buildings = new();
        public readonly List<Prop> Props = new();

        /// <summary>worldSeed identifies the world (kept in the file map); noiseSeed also depends on the biome.</summary>
        public Gen(uint[] palette, ulong worldSeed, ulong noiseSeed, int n)
        {
            _pal = palette;
            _worldSeed = worldSeed;
            _seed = noiseSeed;
            _rng = new Rng(noiseSeed);
            _n = n;
            _c = n / 2;
            _height = new int[n * n];
            _pin = Enumerable.Repeat(NoPin, n * n).ToArray();
            _color = new uint[n * n];
            _water = new bool[n * n];
            _blocked = new bool[n * n];
            Array.Fill(_height, Ground);
            // nothing is built or placed on the spawn, the plaza or between them, so the mural is seen from the
            // spawn; natural biomes also flatten the spawn and the plaza (PinFlat)
            _spawn = (_c - 6, _c + 6, _c - 6, _c + 6);
            _plaza = (_c - MuralWidth / 2 - 4, _c + MuralWidth / 2 + 4, _c - MuralDistance - 4, _c - MuralDistance + 10);
            _view = (_plaza.X0, _plaza.X1, _plaza.Z1 + 1, _spawn.Z0 - 1);
            foreach (var r in new[] { _spawn, _plaza, _view })
                MarkBlocked(r.X0, r.Z0, r.X1 - r.X0 + 1, r.Z1 - r.Z0 + 1);
        }

        public FileMap Terrain() => new(_n, WaterLevel, _worldSeed, _height, _color, _water);

        int I(int x, int z) => z * _n + x;
        bool In(int x, int z) => x >= 0 && z >= 0 && x < _n && z < _n;

        static bool Inside((int X0, int X1, int Z0, int Z1) r, int x, int z) => x >= r.X0 && x <= r.X1 && z >= r.Z0 && z <= r.Z1;

        bool OnReserved(int x, int z) => Inside(_spawn, x, z) || Inside(_plaza, x, z) || Inside(_view, x, z);

        /// <summary>
        /// Flattens the spawn clearing and the mural plaza at their own average height, so they sit in the land
        /// instead of in a pit. The plaza stays low enough for the mural to fit the 48-block world.
        /// </summary>
        void PinFlat()
        {
            Pin(_spawn, Ground, 20);
            Pin(_plaza, Ground, 48 - MuralHeight);
        }

        void Pin((int X0, int X1, int Z0, int Z1) r, int min, int max)
        {
            double sum = 0;
            int count = 0;
            for (int z = r.Z0; z <= r.Z1; z++)
                for (int x = r.X0; x <= r.X1; x++)
                    if (In(x, z)) { sum += _height[I(x, z)]; count++; }
            int level = Math.Clamp((int)Math.Round(sum / count), min, max);
            for (int z = r.Z0; z <= r.Z1; z++)
                for (int x = r.X0; x <= r.X1; x++)
                    if (In(x, z)) _pin[I(x, z)] = level;
        }

        // ------------------------------------------------------------ biomes

        public void Field()
        {
            Hills(6, 1);
            PinFlat();
            Shape();
            PaintGround(0.25, 0.85, 2);
            var (leaf, bush, rock) = Greenery(0.45);
            Scatter(PropKind.Tree, _n * _n / 600, 4, 0.8, 1.4, leaf);
            Scatter(PropKind.Bush, _n * _n / 350, 3, 0.6, 1.1, bush);
            Scatter(PropKind.Rock, _n * _n / 2500, 3, 0.5, 1.2, rock);
        }

        public void Road()
        {
            Hills(5, 3);
            PinFlat();
            double phase = _rng.Next() * Math.Tau;
            var centre = new int[_n];
            var level = new int[_n];
            for (int x = 0; x < _n; x++)
            {
                centre[x] = _c + 27 + (int)Math.Round(8 * Math.Sin(x * Math.Tau / 160 + phase) + 3 * Math.Sin(x * Math.Tau / 57 + 2 * phase));
                level[x] = Ground + (int)Math.Round(2 * Noise(x, 0, 64, 4));
            }
            for (int x = 1; x < _n; x++) level[x] = Math.Clamp(level[x], level[x - 1] - 1, level[x - 1] + 1);

            var road = new bool[_n * _n];
            for (int x = 0; x < _n; x++)
                for (int z = centre[x] - 6; z < centre[x] + 6; z++)
                {
                    road[I(x, z)] = true;
                    _pin[I(x, z)] = level[x];
                }
            for (int x = 0; x < _n; x++) // keep props two cells off the road
                for (int z = centre[x] - 8; z < centre[x] + 8; z++)
                    if (In(x, z)) _blocked[I(x, z)] = true;
            Shape();
            PaintGround(0.25, 0.8, 5);
            uint asphalt = Asphalt();
            for (int i = 0; i < road.Length; i++)
                if (road[i]) _color[i] = asphalt;

            for (int x = 6, k = 0; x < _n; x += 14, k++)
            {
                int z = k % 2 == 0 ? centre[x] - 8 : centre[x] + 7;
                if (In(x, z) && !OnReserved(x, z)) Props.Add(new Prop(PropKind.Lamp, x, z, 1f, P(1)));
            }
            var (leaf, bush, _) = Greenery(0.45);
            Scatter(PropKind.Tree, _n * _n / 700, 4, 0.8, 1.3, leaf);
            Scatter(PropKind.Bush, _n * _n / 450, 3, 0.6, 1.0, bush);
        }

        public void Water()
        {
            double angle = _rng.Next() * Math.Tau, dx = Math.Cos(angle), dz = Math.Sin(angle);
            int plazaX = (_plaza.X0 + _plaza.X1) / 2, plazaZ = (_plaza.Z0 + _plaza.Z1) / 2;
            // land rises around the spawn and the plaza, so the coast bends around them instead of cutting them off
            double LandBonus(int x, int z, int cx, int cz)
            {
                double t = Math.Max(0, 1 - Math.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz)) / 36);
                return t * t * (3 - 2 * t);
            }
            var score = new double[_n * _n];
            for (int z = 0; z < _n; z++)
                for (int x = 0; x < _n; x++)
                    score[I(x, z)] = Inside(_spawn, x, z) || Inside(_plaza, x, z) ? double.NegativeInfinity // always land
                        : 0.55 * Noise(x, z, 56, 6) + 0.45 * (((x - _c) * dx + (z - _c) * dz) / _n + 0.5)
                          - 0.6 * Math.Max(LandBonus(x, z, _c, _c), LandBonus(x, z, plazaX, plazaZ));
            double share = 0.44 + 0.08 * _rng.Next(); // the shore settles a little more water in below
            var sorted = (double[])score.Clone();
            Array.Sort(sorted);
            double threshold = sorted[(int)((1 - share) * sorted.Length)];
            for (int z = 0; z < _n; z++)
                for (int x = 0; x < _n; x++)
                    _height[I(x, z)] = score[I(x, z)] >= threshold ? SeaFloor : 9 + (int)Math.Round(4 * Noise(x, z, 40, 7));
            PinFlat();
            Shape();

            uint seabed = Shade(P(0.3), 0.9), sand = Warm(P(0.85)), grassLow = P(0.35), grassHigh = P(0.7);
            int minLand = int.MaxValue, maxLand = int.MinValue;
            for (int i = 0; i < _height.Length; i++)
            {
                _water[i] = _height[i] < WaterLevel;
                if (!_water[i]) (minLand, maxLand) = (Math.Min(minLand, _height[i]), Math.Max(maxLand, _height[i]));
            }
            for (int z = 0; z < _n; z++)
                for (int x = 0; x < _n; x++)
                {
                    int i = I(x, z);
                    if (_water[i]) { _color[i] = Jitter(seabed, x, z); _blocked[i] = true; continue; }
                    bool shore = _height[i] <= WaterLevel + 1 || NearWater(x, z, 3);
                    double t = (_height[i] - minLand) / (double)Math.Max(1, maxLand - minLand);
                    _color[i] = Jitter(shore ? sand : Lerp(grassLow, grassHigh, t), x, z);
                }
            var (leaf, bush, rock) = Greenery(0.5);
            Scatter(PropKind.Rock, _n * _n / 900, 3, 0.5, 1.3, rock);
            Scatter(PropKind.Tree, _n * _n / 900, 4, 0.8, 1.3, leaf);
            Scatter(PropKind.Bush, _n * _n / 600, 3, 0.6, 1.0, bush);
        }

        public void Forest()
        {
            Hills(10, 8);
            PinFlat();
            Shape();
            PaintGround(0.1, 0.65, 9);
            var (leaf, bush, rock) = Greenery(0.4);
            Scatter(PropKind.Tree, _n * _n / 60, 3, 0.8, 1.7, leaf);
            Scatter(PropKind.Bush, _n * _n / 300, 2, 0.6, 1.1, bush);
            Scatter(PropKind.Rock, _n * _n / 1500, 3, 0.6, 1.4, rock);
        }

        public void City()
        {
            uint pavement = Gray(P(0.65), 0.5);
            double walk = Palette.Luma(pavement);
            uint asphalt = Apart(Asphalt(), walk, 0.1);
            var street = new bool[_n * _n];
            var lines = Enumerable.Range(-_c / 40, _n / 40 + 1).Select(k => _c + 40 * k).Where(s => s - 4 >= 0 && s + 4 <= _n).ToList();
            for (int z = 0; z < _n; z++)
                for (int x = 0; x < _n; x++)
                {
                    bool onStreet = lines.Any(s => x >= s - 4 && x < s + 4) || lines.Any(s => z >= s - 4 && z < s + 4);
                    street[I(x, z)] = onStreet;
                    _color[I(x, z)] = onStreet ? asphalt : Jitter(pavement, x, z);
                    if (onStreet) _blocked[I(x, z)] = true;
                }

            for (int a = 0; a + 1 < lines.Count; a++)
                for (int b = 0; b + 1 < lines.Count; b++)
                {
                    int lotX = lines[a] + 4, lotZ = lines[b] + 4, lot = lines[a + 1] - 4 - lotX, half = lot / 2;
                    for (int q = 0; q < 4; q++)
                    {
                        if (_rng.Next() < 0.15) continue; // an empty corner: a small square
                        int sx = lotX + q % 2 * half, sz = lotZ + q / 2 * half;
                        int w = _rng.NextInt(8, half - 3), d = _rng.NextInt(8, half - 3);
                        int x0 = sx + 2 + _rng.NextInt(0, half - 3 - w), z0 = sz + 2 + _rng.NextInt(0, half - 3 - d);
                        if (Overlaps(x0, z0, w, d, 1)) continue;
                        uint color = Apart(Jitter(P(0.15 + 0.6 * _rng.Next()), x0, z0), walk, 0.1);
                        AddBox(x0, z0, w, d, 12 + _rng.NextInt(0, 25), color);
                    }
                }

            for (int s = 0; s < lines.Count; s++)
                for (int t = 10; t < _n; t += 12)
                {
                    int x = lines[s] + 5, z = t;
                    if (In(x, z) && !_blocked[I(x, z)]) Props.Add(new Prop(PropKind.Lamp, x, z, 1f, P(1)));
                }
            Scatter(PropKind.Tree, _n * _n / 1200, 4, 0.7, 1.1, Apart(LeafColor(), walk, 0.12));
            Scatter(PropKind.Bench, _n * _n / 2500, 4, 1f, 1f, Apart(P(0.3), walk, 0.1));
        }

        public void Indoor()
        {
            uint floor = P(0.55);
            double floorLuma = Palette.Luma(floor);
            uint wall = Apart(P(0.92), floorLuma, 0.1);
            for (int z = 0; z < _n; z++)
                for (int x = 0; x < _n; x++)
                    _color[I(x, z)] = x / 6 % 2 == 0 ? floor : Shade(floor, 0.93); // floor boards
            AddBox(0, 0, _n, 2, 40, wall);
            AddBox(0, _n - 2, _n, 2, 40, wall);
            AddBox(0, 2, 2, _n - 4, 40, wall);
            AddBox(_n - 2, 2, 2, _n - 4, 40, wall);

            int want = 4 + _rng.NextInt(0, 3), placed = 0;
            for (int attempt = 0; attempt < 400 && placed < want; attempt++)
            {
                int w = _rng.NextInt(18, 35), d = _rng.NextInt(18, 35), h = 2 * _rng.NextInt(8, 13);
                int x0 = _rng.NextInt(6, _n - 6 - w), z0 = _rng.NextInt(6, _n - 6 - d);
                int steps = h / 2 - 1;
                // stairs on the side facing the room centre, 4 wide, centred on that side
                int cx = x0 + w / 2, cz = z0 + d / 2;
                (int Dx, int Dz) dir = Math.Abs(cx - _c) > Math.Abs(cz - _c) ? (Math.Sign(_c - cx), 0) : (0, Math.Sign(_c - cz));
                if (dir == (0, 0)) dir = (0, 1);
                var stairs = new List<(int X, int Z, int W, int D, int H)>();
                for (int k = 0; k < steps; k++)
                {
                    int height = h - 2 - 2 * k; // the step next to the table is 2 below its top
                    stairs.Add(dir switch
                    {
                        (-1, 0) => (x0 - 2 * (k + 1), cz - 2, 2, 4, height),
                        (1, 0) => (x0 + w + 2 * k, cz - 2, 2, 4, height),
                        (0, -1) => (cx - 2, z0 - 2 * (k + 1), 4, 2, height),
                        _ => (cx - 2, z0 + d + 2 * k, 4, 2, height),
                    });
                }
                int ax = Math.Min(x0, stairs.Min(s => s.X)), az = Math.Min(z0, stairs.Min(s => s.Z));
                int bx = Math.Max(x0 + w, stairs.Max(s => s.X + s.W)), bz = Math.Max(z0 + d, stairs.Max(s => s.Z + s.D));
                if (ax < 5 || az < 5 || bx > _n - 5 || bz > _n - 5 || Overlaps(ax, az, bx - ax, bz - az, 5)) continue;

                uint wood = Apart(Jitter(P(0.15 + 0.2 * _rng.Next()), x0, z0), floorLuma, 0.12);
                AddBox(x0, z0, w, d, h, wood);
                foreach (var s in stairs) AddBox(s.X, s.Z, s.W, s.D, s.H, Shade(wood, 1.12));
                MarkBlocked(ax - 3, az - 3, bx - ax + 6, bz - az + 6);
                int cups = 1 + _rng.NextInt(0, 2);
                for (int k = 0; k < cups; k++)
                    Props.Add(new Prop(PropKind.Cup, x0 + 3 + _rng.NextInt(0, w - 6), z0 + 3 + _rng.NextInt(0, d - 6),
                        (float)(4 + 2 * _rng.Next()), Apart(P(0.75 + 0.25 * _rng.Next()), Palette.Luma(wood), 0.1)));
                placed++;
            }
            Scatter(PropKind.Plant, 8, 12, 5, 8, Apart(LeafColor(), floorLuma, 0.12));
        }

        public void Transit()
        {
            uint ground = P(0.5);
            double groundLuma = Palette.Luma(ground);
            uint gravel = Apart(Gray(P(0.35), 0.75), groundLuma, 0.08), platform = Apart(P(0.8), groundLuma, 0.12);
            uint rail = Apart(Shade(P(0), 0.9), Palette.Luma(gravel), 0.1);
            for (int z = 0; z < _n; z++)
                for (int x = 0; x < _n; x++)
                    _color[I(x, z)] = Jitter(ground, x, z);
            int[] tracks = { _c + 22, _c + 38 };
            foreach (int t in tracks)
            {
                for (int z = t - 5; z <= t + 5; z++)
                    for (int x = 0; x < _n; x++)
                    {
                        _color[I(x, z)] = Math.Abs(z - t) < 3 && x % 3 == 0 ? Shade(gravel, 0.7) : Jitter(gravel, x, z); // sleepers
                        _blocked[I(x, z)] = true;
                    }
                AddBox(0, t - 3, _n, 1, 1, rail);
                AddBox(0, t + 3, _n, 1, 1, rail);
            }
            int px0 = _c - 60, len = 120;
            foreach (var (z0, d) in new[] { (tracks[0] - 12, 6), (tracks[0] + 6, tracks[1] - 6 - (tracks[0] + 6)) })
            {
                AddBox(px0, z0, len, d, 4, platform);
                AddBox(px0 - 3, z0, 3, d, 2, Shade(platform, 0.9)); // a step up at each end
                AddBox(px0 + len, z0, 3, d, 2, Shade(platform, 0.9));
                for (int x = px0 + 8; x < px0 + len - 4; x += 16)
                {
                    Props.Add(new Prop(PropKind.Lamp, x, z0 + d / 2, 1f, P(1)));
                    if (d >= 6) Props.Add(new Prop(PropKind.Bench, x + 8, z0 + 1, 1f, P(0.3)));
                }
            }
            // a tunnel arch over both tracks: two pillars and a beam 9 blocks over the ground
            int ax = _c - 84, az = tracks[0] - 8, ad = tracks[1] + 8 - az;
            uint stone = Apart(Gray(P(0.4), 0.6), groundLuma, 0.1);
            AddBox(ax, az, 6, 2, 12, stone);
            AddBox(ax, az + ad - 2, 6, 2, 12, stone);
            AddBox(ax, az, 6, ad, 3, stone, lift: 9);
            uint leaf = Apart(LeafColor(), groundLuma, 0.12);
            Scatter(PropKind.Bush, _n * _n / 800, 3, 0.6, 1.0, leaf);
            Scatter(PropKind.Tree, _n * _n / 1500, 4, 0.8, 1.2, leaf);
        }

        // ------------------------------------------------------------ terrain

        /// <summary>Two layers of value noise (big hills + ripples), stretched to Ground..Ground+amp.</summary>
        void Hills(int amp, uint salt)
        {
            var n = new double[_n * _n];
            for (int z = 0; z < _n; z++)
                for (int x = 0; x < _n; x++)
                    n[I(x, z)] = 0.75 * Noise(x, z, 48, salt) + 0.25 * Noise(x, z, 12, salt + 100);
            double lo = n.Min(), hi = n.Max();
            for (int i = 0; i < n.Length; i++)
                _height[i] = Ground + (int)Math.Round(amp * (n[i] - lo) / Math.Max(1e-9, hi - lo));
        }

        /// <summary>
        /// Limits every side step to 2 blocks and holds pinned columns (spawn, plaza, road) at their heights:
        /// the lower envelope of the hills, clamped between the lowest and highest slope-2 cones the pins allow.
        /// All three are 2-Lipschitz on the grid, so the result is too.
        /// </summary>
        void Shape()
        {
            var env = (int[])_height.Clone();
            MinPlus(env);
            var up = _pin.Select(p => p == NoPin ? Far : p).ToArray();
            var lo = _pin.Select(p => p == NoPin ? -Far : p).ToArray();
            MinPlus(up);
            for (int i = 0; i < lo.Length; i++) lo[i] = -lo[i];
            MinPlus(lo);
            for (int i = 0; i < lo.Length; i++)
            {
                lo[i] = -lo[i];
                if (lo[i] > up[i]) throw new InvalidOperationException("pinned heights are too steep for each other");
                _height[i] = Math.Min(Math.Max(env[i], lo[i]), up[i]);
            }
        }

        /// <summary>a[p] = min over q of a[q] + 2·(grid distance p..q), in two sweeps.</summary>
        void MinPlus(int[] a)
        {
            for (int z = 0; z < _n; z++)
                for (int x = 0; x < _n; x++)
                {
                    int i = I(x, z);
                    if (x > 0) a[i] = Math.Min(a[i], a[i - 1] + 2);
                    if (z > 0) a[i] = Math.Min(a[i], a[i - _n] + 2);
                }
            for (int z = _n - 1; z >= 0; z--)
                for (int x = _n - 1; x >= 0; x--)
                {
                    int i = I(x, z);
                    if (x < _n - 1) a[i] = Math.Min(a[i], a[i + 1] + 2);
                    if (z < _n - 1) a[i] = Math.Min(a[i], a[i + _n] + 2);
                }
        }

        /// <summary>
        /// Low ground takes the darker end of the palette range, high ground the lighter end. Position in the range
        /// is the column's height rank (share of columns lower than it), not its raw height, so every world uses its
        /// whole range even when most hills sit at middle height (plan Review Focus 5).
        /// </summary>
        void PaintGround(double from, double to, uint salt)
        {
            var count = new int[_height.Max() + 1];
            foreach (int h in _height) count[h]++;
            var rank = new double[count.Length];
            for (int h = 0, below = 0; h < count.Length; below += count[h], h++)
                rank[h] = (below + count[h] / 2.0) / _height.Length;
            for (int z = 0; z < _n; z++)
                for (int x = 0; x < _n; x++)
                {
                    int i = I(x, z);
                    double t = rank[_height[i]] + 0.15 * (Noise(x, z, 9, salt) - 0.5);
                    _color[i] = Jitter(P(from + (to - from) * Math.Clamp(t, 0, 1)), x, z);
                }
        }

        bool NearWater(int x, int z, int r)
        {
            for (int dz = -r; dz <= r; dz++)
                for (int dx = -r; dx <= r; dx++)
                    if (In(x + dx, z + dz) && _water[I(x + dx, z + dz)]) return true;
            return false;
        }

        // ------------------------------------------------------------ buildings and props

        void AddBox(int x, int z, int w, int d, int height, uint color, int lift = 0)
        {
            Buildings.Add(new Building(x, z, w, d, height, color, lift));
            MarkBlocked(x, z, w, d);
        }

        void MarkBlocked(int x0, int z0, int w, int d)
        {
            for (int z = Math.Max(0, z0); z < Math.Min(_n, z0 + d); z++)
                for (int x = Math.Max(0, x0); x < Math.Min(_n, x0 + w); x++)
                    _blocked[I(x, z)] = true;
        }

        /// <summary>True when the box, grown by `margin`, touches anything blocked (streets, buildings, spawn, plaza).</summary>
        bool Overlaps(int x0, int z0, int w, int d, int margin)
        {
            for (int z = z0 - margin; z < z0 + d + margin; z++)
                for (int x = x0 - margin; x < x0 + w + margin; x++)
                    if (In(x, z) && _blocked[I(x, z)]) return true;
            return false;
        }

        /// <summary>Up to `count` props on free ground, at least `spacing` columns apart.</summary>
        void Scatter(PropKind kind, int count, int spacing, double minScale, double maxScale, uint color)
        {
            var near = new bool[_n * _n];
            foreach (var p in Props) Mark(near, p.X, p.Z, spacing);
            for (int attempt = 0, placed = 0; attempt < count * 20 && placed < count; attempt++)
            {
                int x = _rng.NextInt(1, _n - 1), z = _rng.NextInt(1, _n - 1);
                if (_blocked[I(x, z)] || near[I(x, z)] || _water[I(x, z)]) continue;
                float scale = (float)(minScale + (maxScale - minScale) * _rng.Next());
                Props.Add(new Prop(kind, x, z, scale, Jitter(color, x, z)));
                Mark(near, x, z, spacing);
                placed++;
            }
        }

        void Mark(bool[] near, int x, int z, int r)
        {
            for (int dz = -r + 1; dz < r; dz++)
                for (int dx = -r + 1; dx < r; dx++)
                    if (In(x + dx, z + dz)) near[I(x + dx, z + dz)] = true;
        }

        // ------------------------------------------------------------ colors

        /// <summary>Palette color at t (0 = darkest, 1 = lightest), blended between neighbours.</summary>
        uint P(double t)
        {
            double pos = Math.Clamp(t, 0, 1) * (_pal.Length - 1);
            int i = (int)Math.Floor(pos);
            return Lerp(_pal[i], _pal[Math.Min(i + 1, _pal.Length - 1)], pos - i);
        }

        /// <summary>Road and street color: the least saturated of the three darkest colors, pushed toward gray.</summary>
        uint Asphalt() => Gray(_pal.Take(Math.Min(3, _pal.Length)).OrderBy(Saturation).ThenBy(c => c).First(), 0.6);

        /// <summary>The most saturated mid color: leaves read as the photo's liveliest color.</summary>
        uint LeafColor() =>
            _pal.Skip(1).Take(Math.Max(1, _pal.Length - 2)).OrderByDescending(Saturation).ThenBy(c => c).First();

        /// <summary>Leaf, bush and rock colors for the natural biomes, apart from the ground already painted.</summary>
        (uint Leaf, uint Bush, uint Rock) Greenery(double rockAt)
        {
            var dry = Enumerable.Range(0, _color.Length).Where(i => !_water[i]).Select(i => Palette.Luma(_color[i])).OrderBy(l => l).ToList();
            double ground = dry.Count == 0 ? 0.5 : dry[dry.Count / 2];
            uint leaf = Apart(LeafColor(), ground, 0.12);
            return (leaf, Apart(Shade(leaf, 0.85), ground, 0.1), Apart(Gray(P(rockAt), 0.65), ground, 0.1));
        }

        /// <summary>
        /// `c` with its brightness at least `gap` away from `from`: darker when there is room (leaves in shade read
        /// naturally), otherwise lighter. Props and buildings never melt into the ground (plan Review Focus 5).
        /// </summary>
        static uint Apart(uint c, double from, double gap)
        {
            double l = Palette.Luma(c);
            if (Math.Abs(l - from) >= gap) return c;
            return Palette.WithLuma(c, from - gap >= 0.03 ? from - gap : Math.Min(1, from + gap));
        }

        static double Saturation(uint c)
        {
            int r = (int)(c >> 16 & 0xFF), g = (int)(c >> 8 & 0xFF), b = (int)(c & 0xFF), max = Math.Max(r, Math.Max(g, b));
            return max == 0 ? 0 : (max - Math.Min(r, Math.Min(g, b))) / (double)max;
        }

        uint Jitter(uint c, int x, int z) => Shade(c, 0.94 + 0.12 * Hash01(x, z, 77));

        static uint Lerp(uint a, uint b, double t)
        {
            uint Ch(int s) => (uint)Math.Round((a >> s & 0xFF) + ((double)(b >> s & 0xFF) - (a >> s & 0xFF)) * t);
            return Ch(16) << 16 | Ch(8) << 8 | Ch(0);
        }

        static uint Shade(uint c, double k)
        {
            uint Ch(int s) => (uint)Math.Clamp((int)Math.Round((c >> s & 0xFF) * k), 0, 255);
            return Ch(16) << 16 | Ch(8) << 8 | Ch(0);
        }

        /// <summary>Mixes `amount` of the color's own gray in.</summary>
        static uint Gray(uint c, double amount)
        {
            uint y = (uint)Math.Round(Palette.Luma(c) * 255);
            return Lerp(c, y << 16 | y << 8 | y, amount);
        }

        static uint Warm(uint c) => Lerp(c, 0xE8D2A0, 0.35);

        // ------------------------------------------------------------ randomness

        double Noise(double x, double z, int cell, uint salt)
        {
            double fx = x / cell, fz = z / cell;
            int ix = (int)Math.Floor(fx), iz = (int)Math.Floor(fz);
            double tx = fx - ix, tz = fz - iz;
            tx = tx * tx * (3 - 2 * tx);
            tz = tz * tz * (3 - 2 * tz);
            double a = Hash01(ix, iz, salt), b = Hash01(ix + 1, iz, salt), c = Hash01(ix, iz + 1, salt), d = Hash01(ix + 1, iz + 1, salt);
            return a + (b - a) * tx + (c - a) * tz + (a - b - c + d) * tx * tz;
        }

        double Hash01(int x, int z, uint salt) =>
            (Rng.Mix(_seed ^ (ulong)(uint)x * 0x9E3779B1UL ^ (ulong)(uint)z << 32 ^ (ulong)salt * 0x85EBCA77C2B2AE63UL) >> 11) * (1.0 / (1UL << 53));
    }

    /// <summary>SplitMix64: the same numbers on every machine and .NET version.</summary>
    sealed class Rng(ulong state)
    {
        ulong _s = state;

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
}

/// <summary>The photo itself, standing in the world as a pixel wall (design §6-3: a landmark of your own photo).</summary>
public static class Mural
{
    static readonly uint Frame = Block.Solid(0x3A3A3A);

    /// <summary>
    /// The photo, cropped to width:height around its centre and shrunk to width×height blocks, as a 1-block-thick
    /// wall along x0..x0+width-1 at z0. Its bottom row rests on the highest ground under the wall; lower columns
    /// get a dark plinth up to it, so nothing floats. Seen from the south (+z) the photo reads left to right.
    /// </summary>
    public static void Stamp(VoxelWorld w, PixelImage prepared, int x0, int z0, int width = 64, int height = 48)
    {
        var art = Fit(prepared, width, height);
        var top = new int[width];
        int baseY = 0;
        for (int c = 0; c < width; c++)
        {
            int y = w.SizeY - 1;
            while (y >= 0 && !Block.IsSolid(w.Get(x0 + c, y, z0))) y--;
            top[c] = y + 1;
            baseY = Math.Max(baseY, top[c]);
        }
        for (int c = 0; c < width; c++)
        {
            for (int y = top[c]; y < baseY; y++) w.Set(x0 + c, y, z0, Frame);
            for (int r = 0; r < height; r++)
            {
                var (red, green, blue, _) = PixelImage.Unpack(art[c, r]);
                w.Set(x0 + c, baseY + height - 1 - r, z0, Block.Solid((uint)(red << 16 | green << 8 | blue)));
            }
        }
    }

    /// <summary>Centre crop to the target aspect, then area average (or nearest, when growing) to width×height.</summary>
    static PixelImage Fit(PixelImage src, int width, int height)
    {
        double aspect = (double)width / height;
        int cw = src.Width, ch = src.Height;
        if ((double)cw / ch > aspect) cw = Math.Max(1, (int)Math.Round(ch * aspect));
        else ch = Math.Max(1, (int)Math.Round(cw / aspect));
        int ox = (src.Width - cw) / 2, oy = (src.Height - ch) / 2;
        var px = new uint[width * height];
        for (int r = 0; r < height; r++)
            for (int c = 0; c < width; c++)
            {
                int sx0 = ox + c * cw / width, sx1 = Math.Max(sx0 + 1, ox + (c + 1) * cw / width);
                int sy0 = oy + r * ch / height, sy1 = Math.Max(sy0 + 1, oy + (r + 1) * ch / height);
                long rs = 0, gs = 0, bs = 0, n = 0;
                for (int y = sy0; y < sy1; y++)
                    for (int x = sx0; x < sx1; x++)
                    {
                        var (pr, pg, pb, _) = PixelImage.Unpack(src[x, y]);
                        rs += pr; gs += pg; bs += pb; n++;
                    }
                px[r * width + c] = PixelImage.Pack((int)(rs / n), (int)(gs / n), (int)(bs / n));
            }
        return new PixelImage(width, height, px);
    }
}
