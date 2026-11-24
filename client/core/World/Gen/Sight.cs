namespace Hangbar.Core.World.Gen;

/// <summary>Line of sight through the block grid; only opaque blocks block it (glass and water do not).</summary>
public static class Sight
{
    /// <summary>
    /// True when no opaque block lies between the two points (block coordinates). The cells holding the two end points
    /// are not checked, so a point on a block's surface (a mural) can be the target.
    /// </summary>
    public static bool Clear(VoxelWorld w, (double X, double Y, double Z) from, (double X, double Y, double Z) to)
    {
        double dx = to.X - from.X, dy = to.Y - from.Y, dz = to.Z - from.Z;
        int x = (int)Math.Floor(from.X), y = (int)Math.Floor(from.Y), z = (int)Math.Floor(from.Z);
        int ex = (int)Math.Floor(to.X), ey = (int)Math.Floor(to.Y), ez = (int)Math.Floor(to.Z);
        int sx = Math.Sign(dx), sy = Math.Sign(dy), sz = Math.Sign(dz);
        double tdx = sx == 0 ? double.MaxValue : 1 / Math.Abs(dx);
        double tdy = sy == 0 ? double.MaxValue : 1 / Math.Abs(dy);
        double tdz = sz == 0 ? double.MaxValue : 1 / Math.Abs(dz);
        double tx = sx == 0 ? double.MaxValue : ToBoundary(from.X, sx) * tdx;
        double ty = sy == 0 ? double.MaxValue : ToBoundary(from.Y, sy) * tdy;
        double tz = sz == 0 ? double.MaxValue : ToBoundary(from.Z, sz) * tdz;
        while (true)
        {
            if (tx <= ty && tx <= tz) { if (tx > 1) break; x += sx; tx += tdx; }
            else if (ty <= tz) { if (ty > 1) break; y += sy; ty += tdy; }
            else { if (tz > 1) break; z += sz; tz += tdz; }
            if (x == ex && y == ey && z == ez) break;
            if (Block.IsOpaque(w.Get(x, y, z))) return false;
        }
        return true;
    }

    static double ToBoundary(double p, int s) => s > 0 ? Math.Floor(p) + 1 - p : p - Math.Floor(p);
}
