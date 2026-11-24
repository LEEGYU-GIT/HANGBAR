namespace Hangbar.Core.World.Gen;

/// <summary>Smooth value noise on a grid of <c>cell</c>-sized squares (the v1 formula), plus a 3D version for caves.</summary>
public sealed class ValueNoise(ulong seed)
{
    readonly ulong _seed = seed;

    /// <summary>Uniform in [0, 1) for a lattice point.</summary>
    public double Hash01(int x, int z, uint salt) =>
        (Rng.Mix(_seed ^ (ulong)(uint)x * 0x9E3779B1UL ^ (ulong)(uint)z << 32 ^ (ulong)salt * 0x85EBCA77C2B2AE63UL) >> 11) * (1.0 / (1UL << 53));

    double Hash01(int x, int y, int z, uint salt) =>
        (Rng.Mix(_seed ^ (ulong)(uint)x * 0x9E3779B1UL ^ (ulong)(uint)z << 32 ^ (ulong)(uint)y * 0xC2B2AE3D27D4EB4FUL
                 ^ (ulong)salt * 0x85EBCA77C2B2AE63UL) >> 11) * (1.0 / (1UL << 53));

    public double Sample(double x, double z, int cell, uint salt)
    {
        double fx = x / cell, fz = z / cell;
        int ix = (int)Math.Floor(fx), iz = (int)Math.Floor(fz);
        double tx = fx - ix, tz = fz - iz;
        tx = tx * tx * (3 - 2 * tx);
        tz = tz * tz * (3 - 2 * tz);
        double a = Hash01(ix, iz, salt), b = Hash01(ix + 1, iz, salt), c = Hash01(ix, iz + 1, salt), d = Hash01(ix + 1, iz + 1, salt);
        return a + (b - a) * tx + (c - a) * tz + (a - b - c + d) * tx * tz;
    }

    public double Sample3(double x, double y, double z, int cell, uint salt)
    {
        double fx = x / cell, fy = y / cell, fz = z / cell;
        int ix = (int)Math.Floor(fx), iy = (int)Math.Floor(fy), iz = (int)Math.Floor(fz);
        double tx = Smooth(fx - ix), ty = Smooth(fy - iy), tz = Smooth(fz - iz);
        double Plane(int yy) => Lerp(
            Lerp(Hash01(ix, yy, iz, salt), Hash01(ix + 1, yy, iz, salt), tx),
            Lerp(Hash01(ix, yy, iz + 1, salt), Hash01(ix + 1, yy, iz + 1, salt), tx), tz);
        return Lerp(Plane(iy), Plane(iy + 1), ty);
    }

    static double Lerp(double p, double q, double t) => p + (q - p) * t;
    static double Smooth(double t) => t * t * (3 - 2 * t);
}
