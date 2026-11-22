using Hangbar.Core;

namespace Hangbar.Core.Tests;

// Review I4: the access token must be refreshed before reconnecting instead of reusing an expired one.
public class TokenRefreshTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 23, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Fresh_token_is_reused() => Assert.False(TokenRefresh.ShouldRefresh(Now.AddMinutes(30), Now));

    [Fact]
    public void Token_expiring_within_a_minute_is_refreshed() => Assert.True(TokenRefresh.ShouldRefresh(Now.AddSeconds(59), Now));

    [Fact]
    public void Expired_token_is_refreshed() => Assert.True(TokenRefresh.ShouldRefresh(Now.AddHours(-1), Now));
}
