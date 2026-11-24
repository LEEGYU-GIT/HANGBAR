using System.IO.Compression;

namespace WorldPreview;

/// <summary>An RGB canvas that saves as PNG with nothing but the base library (zlib stream + CRC-32).</summary>
public sealed class Canvas(int width, int height, uint background)
{
    public int Width { get; } = width;
    public int Height { get; } = height;
    readonly uint[] _px = Enumerable.Repeat(background, width * height).ToArray();

    public void Set(int x, int y, uint rgb)
    {
        if (x >= 0 && y >= 0 && x < Width && y < Height) _px[y * Width + x] = rgb;
    }

    public void Fill(int x0, int y0, int w, int h, uint rgb)
    {
        for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++) Set(x, y, rgb);
    }

    public void Save(string path)
    {
        using var file = File.Create(path);
        file.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        var ihdr = new byte[13];
        BigEndian(ihdr, 0, (uint)Width);
        BigEndian(ihdr, 4, (uint)Height);
        ihdr[8] = 8;  // bit depth
        ihdr[9] = 2;  // RGB
        Chunk(file, "IHDR", ihdr);
        using (var raw = new MemoryStream())
        {
            using (var z = new ZLibStream(raw, CompressionLevel.Optimal, leaveOpen: true))
            {
                var row = new byte[1 + Width * 3];
                for (int y = 0; y < Height; y++)
                {
                    row[0] = 0;
                    for (int x = 0; x < Width; x++)
                    {
                        uint c = _px[y * Width + x];
                        row[1 + x * 3] = (byte)(c >> 16);
                        row[2 + x * 3] = (byte)(c >> 8);
                        row[3 + x * 3] = (byte)c;
                    }
                    z.Write(row);
                }
            }
            Chunk(file, "IDAT", raw.ToArray());
        }
        Chunk(file, "IEND", Array.Empty<byte>());
    }

    static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4];
        BigEndian(len, 0, (uint)data.Length);
        s.Write(len);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(data);
        var crc = new byte[4];
        BigEndian(crc, 0, Crc(typeBytes.Concat(data)));
        s.Write(crc);
    }

    static void BigEndian(byte[] b, int at, uint v)
    {
        b[at] = (byte)(v >> 24); b[at + 1] = (byte)(v >> 16); b[at + 2] = (byte)(v >> 8); b[at + 3] = (byte)v;
    }

    static readonly uint[] Table = Enumerable.Range(0, 256).Select(n =>
    {
        uint c = (uint)n;
        for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    static uint Crc(IEnumerable<byte> bytes)
    {
        uint c = 0xFFFFFFFFu;
        foreach (byte b in bytes) c = Table[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }
}
