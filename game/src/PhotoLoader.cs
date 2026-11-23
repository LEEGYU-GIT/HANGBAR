using Godot;
using Hangbar.Core.World;

namespace Inpc.Game;

/// <summary>
/// Reads a jpg/png/webp photo into an engine-independent image. Reads the bytes with .NET so a missing or
/// unreadable file never makes Godot print the path; the path is never logged (design §3).
/// </summary>
public static class PhotoLoader
{
    public static PixelImage? Load(string path)
    {
        byte[] bytes;
        try { bytes = System.IO.File.ReadAllBytes(path); }
        catch (Exception) { return null; }

        var img = new Image();
        Error err = Sniff(bytes) switch
        {
            "png" => img.LoadPngFromBuffer(bytes),
            "jpg" => img.LoadJpgFromBuffer(bytes),
            "webp" => img.LoadWebpFromBuffer(bytes),
            _ => Error.FileUnrecognized,
        };
        if (err != Error.Ok || img.GetWidth() == 0) return null;

        img.Convert(Image.Format.Rgba8);
        byte[] data = img.GetData();
        var px = new uint[img.GetWidth() * img.GetHeight()];
        for (int i = 0; i < px.Length; i++)
            px[i] = (uint)(data[i * 4] << 24 | data[i * 4 + 1] << 16 | data[i * 4 + 2] << 8 | data[i * 4 + 3]);
        return new PixelImage(img.GetWidth(), img.GetHeight(), px);
    }

    static string Sniff(byte[] b)
    {
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return "png";
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "jpg";
        if (b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P') return "webp";
        return "";
    }
}
