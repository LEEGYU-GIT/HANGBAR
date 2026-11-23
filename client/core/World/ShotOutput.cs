namespace Hangbar.Core.World;

/// <summary>
/// Guards the screenshot folder (final review): never the photo's own folder (nothing is written next to the
/// user's photos, design §3), and never over existing files (a user photo named photo.png would be lost).
/// </summary>
public static class ShotOutput
{
    public static readonly string[] FileNames = { "overview.png", "ground.png", "top.png", "photo.png" };

    /// <summary>Null when the folder is safe to write; otherwise a message that never contains the path.</summary>
    public static string? Problem(string outDir, string? photoPath)
    {
        string dir = Normalize(outDir);
        if (photoPath is not null && Path.GetDirectoryName(Path.GetFullPath(photoPath)) is { } photoDir
            && string.Equals(dir, Normalize(photoDir), StringComparison.OrdinalIgnoreCase))
            return "the screenshot folder is the photo's folder; choose another folder";
        if (FileNames.Any(n => File.Exists(Path.Combine(dir, n))))
            return "the screenshot folder already has screenshots; choose an empty folder";
        return null;
    }

    static string Normalize(string p) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(p));
}
