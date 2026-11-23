using Hangbar.Core.World;

namespace Hangbar.Core.Tests.World;

public class ShotOutputTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "inpc-shot-test-" + Guid.NewGuid().ToString("N"));

    public ShotOutputTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void Refuses_the_photo_folder()
    {
        var photo = Path.Combine(_root, "photo.png");
        File.WriteAllBytes(photo, new byte[] { 1 });
        Assert.NotNull(ShotOutput.Problem(_root, photo));
        Assert.NotNull(ShotOutput.Problem(_root + Path.DirectorySeparatorChar, photo)); // same folder, different spelling
    }

    [Fact]
    public void Refuses_to_overwrite_existing_files()
    {
        var outDir = Path.Combine(_root, "out");
        Directory.CreateDirectory(outDir);
        File.WriteAllBytes(Path.Combine(outDir, "top.png"), new byte[] { 1 });
        Assert.NotNull(ShotOutput.Problem(outDir, null));
    }

    [Fact]
    public void Accepts_a_new_or_empty_folder_away_from_the_photo()
    {
        var photo = Path.Combine(_root, "pics", "a.jpg");
        Assert.Null(ShotOutput.Problem(Path.Combine(_root, "shots"), photo));
    }

    [Fact]
    public void Problem_text_never_contains_the_path() =>
        Assert.DoesNotContain(_root, ShotOutput.Problem(_root, Path.Combine(_root, "x.png")) ?? "");
}
