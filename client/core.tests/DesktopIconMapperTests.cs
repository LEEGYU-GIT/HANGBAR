using Hangbar.Core;

namespace Hangbar.Core.Tests;

public class DesktopIconMapperTests
{
    [Fact]
    public void Maps_item_positions_relative_to_listview_origin()
    {
        var r = DesktopIconMapper.Map(
            new[] { new RawIcon(new PointI(10, 20), true), new RawIcon(new PointI(10, 120), false) },
            new PointI(-2560, 0), 76, 100);

        Assert.True(r.Ok);
        Assert.Equal(new[] { new RectI(-2550, 20, 76, 100) }, r.FolderRects);
        Assert.Equal(new[] { new RectI(-2550, 120, 76, 100) }, r.OtherRects);
        Assert.Null(r.Error);
    }

    [Fact]
    public void Failure_result_is_empty_and_not_ok()
    {
        var r = DesktopIconMapper.Failed("0x80004005");
        Assert.False(r.Ok);
        Assert.Empty(r.FolderRects);
        Assert.Empty(r.OtherRects);
        Assert.Equal("0x80004005", r.Error);
    }

    [Fact]
    public void Empty_desktop_is_ok_with_no_rects()
    {
        var r = DesktopIconMapper.Map(Array.Empty<RawIcon>(), new PointI(0, 0), 76, 100);
        Assert.True(r.Ok);
        Assert.Empty(r.FolderRects);
    }
}
