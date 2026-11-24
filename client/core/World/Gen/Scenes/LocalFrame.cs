namespace Hangbar.Core.World.Gen;

/// <summary>
/// A scene's own coordinates turned onto the world. a and b are local column axes; <see cref="Side"/> 0–3 picks one
/// of the four turns (no mirroring), so a scene can be laid out once and land facing any way the seed chooses.
/// Side 0 is the identity: a = x, b = z.
/// </summary>
public readonly record struct LocalFrame(int Side)
{
    const int N = SceneContext.Size;

    public (int X, int Z) W(int a, int b) => Side switch
    {
        0 => (a, b),
        1 => (N - 1 - b, a),
        2 => (N - 1 - a, N - 1 - b),
        _ => (b, N - 1 - a),
    };

    /// <summary>World column index (z·Size + x) of local (a, b).</summary>
    public int I(int a, int b)
    {
        var (x, z) = W(a, b);
        return z * N + x;
    }

    /// <summary>A local step (da, db) as a world step.</summary>
    public (int X, int Z) Dir(int da, int db) => Side switch
    {
        0 => (da, db),
        1 => (-db, da),
        2 => (-da, -db),
        _ => (db, -da),
    };

    /// <summary>The facing of the local unit step (da, db).</summary>
    public Facing FacingOf(int da, int db) => ToFacing(Dir(da, db));

    public static Facing ToFacing((int X, int Z) d) => d switch
    {
        (0, -1) => Facing.North,
        (0, 1) => Facing.South,
        (1, 0) => Facing.East,
        _ => Facing.West,
    };

    /// <summary>Turn that points a part's +x along <paramref name="f"/>.</summary>
    public static Rotation Along(Facing f) => f switch
    {
        Facing.East => Rotation.R0, Facing.South => Rotation.R90, Facing.West => Rotation.R180, _ => Rotation.R270,
    };

    /// <summary>Turn that makes a part's front (its z = 0 row) face <paramref name="f"/>.</summary>
    public static Rotation FrontTo(Facing f) => f switch
    {
        Facing.North => Rotation.R0, Facing.East => Rotation.R90, Facing.South => Rotation.R180, _ => Rotation.R270,
    };

    /// <summary>World min corner of the local box a0..a0+wa−1 × b0..b0+wb−1 at height y (a part's placing origin).</summary>
    public BlockPos Origin(int a0, int b0, int wa, int wb, int y)
    {
        var (x1, z1) = W(a0, b0);
        var (x2, z2) = W(a0 + wa - 1, b0 + wb - 1);
        return new BlockPos(Math.Min(x1, x2), y, Math.Min(z1, z2));
    }

    /// <summary>The world box (inclusive) of a local box, as a structure record.</summary>
    public PlacedPart Box(string name, int a0, int b0, int wa, int wb, int y0, int y1)
    {
        var (x1, z1) = W(a0, b0);
        var (x2, z2) = W(a0 + wa - 1, b0 + wb - 1);
        return new PlacedPart(name, new BlockPos(Math.Min(x1, x2), y0, Math.Min(z1, z2)), new BlockPos(Math.Max(x1, x2), y1, Math.Max(z1, z2)),
            new Dictionary<string, BlockPos>());
    }

    /// <summary>A mural between two world columns of one wall row, its origin at the viewer's left end.</summary>
    public static MuralSpot Spot((int X, int Z) p, (int X, int Z) q, int y, Facing f, int height, bool framed = false)
    {
        int width = Math.Max(Math.Abs(q.X - p.X), Math.Abs(q.Z - p.Z)) + 1;
        var (rx, rz) = HiddenMural.RightOf(f);
        var origin = p.X + rx * (width - 1) == q.X && p.Z + rz * (width - 1) == q.Z ? p : q;
        return new MuralSpot(new BlockPos(origin.X, y, origin.Z), f, width, height, framed);
    }
}
