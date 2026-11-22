namespace Hangbar.Core;

/// <summary>A desktop item as read from the shell: position only, no name (names never leave the reader).</summary>
public readonly record struct RawIcon(PointI ItemPos, bool IsFolder);

/// <summary>Result of reading desktop icons. Error holds only an HRESULT code.</summary>
public sealed record IconReadResult(bool Ok, IReadOnlyList<RectI> FolderRects, IReadOnlyList<RectI> OtherRects, string? Error);

/// <summary>Turns shell item positions into screen rectangles (design §6-4).</summary>
public static class DesktopIconMapper
{
    public static IconReadResult Map(IReadOnlyList<RawIcon> raw, PointI listViewScreenOrigin, int spacingX, int spacingY)
    {
        var folders = new List<RectI>();
        var others = new List<RectI>();
        foreach (var icon in raw)
        {
            var rect = new RectI(listViewScreenOrigin.X + icon.ItemPos.X, listViewScreenOrigin.Y + icon.ItemPos.Y, spacingX, spacingY);
            (icon.IsFolder ? folders : others).Add(rect);
        }
        return new IconReadResult(true, folders, others, null);
    }

    public static IconReadResult Failed(string hresult) =>
        new(false, Array.Empty<RectI>(), Array.Empty<RectI>(), hresult);
}
