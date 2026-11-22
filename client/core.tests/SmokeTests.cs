using Hangbar.Core;

namespace Hangbar.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void Core_assembly_loads() => Assert.NotNull(typeof(RectI).Assembly);
}
