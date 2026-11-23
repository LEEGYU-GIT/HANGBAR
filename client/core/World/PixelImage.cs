namespace Hangbar.Core.World;

/// <summary>Engine-independent RGBA image. Pixels are row-major, each 0xRRGGBBAA.</summary>
public sealed class PixelImage
{
    public int Width { get; }
    public int Height { get; }
    public uint[] Rgba { get; }

    public PixelImage(int width, int height, uint[] rgba)
    {
        if (width <= 0 || height <= 0) throw new ArgumentException("Image must be at least 1×1.");
        if (rgba.Length != width * height) throw new ArgumentException($"Expected {width * height} pixels, got {rgba.Length}.");
        Width = width;
        Height = height;
        Rgba = rgba;
    }

    public uint this[int x, int y] => Rgba[y * Width + x];

    public static uint Pack(int r, int g, int b, int a = 255) =>
        (uint)(Math.Clamp(r, 0, 255) << 24 | Math.Clamp(g, 0, 255) << 16 | Math.Clamp(b, 0, 255) << 8 | Math.Clamp(a, 0, 255));

    public static (int R, int G, int B, int A) Unpack(uint p) =>
        ((int)(p >> 24) & 0xFF, (int)(p >> 16) & 0xFF, (int)(p >> 8) & 0xFF, (int)p & 0xFF);
}
