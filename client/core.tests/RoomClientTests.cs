using Hangbar.Core.Net;

namespace Hangbar.Core.Tests;

public class RoomClientTests
{
    // Nothing listens here; the tests never get far enough to connect anyway.
    static readonly Uri Relay = new("ws://127.0.0.1:9/");

    static async Task WaitUntil(Func<bool> done, int ms = 5000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(ms);
        while (!done() && DateTime.UtcNow < until) await Task.Delay(20);
    }

    [Fact]
    public async Task Provider_exception_is_retried_not_fatal()
    {
        // Review (ECC): a refresh that throws (HttpClient timeout, odd JSON) used to kill the reconnect loop silently.
        int calls = 0;
        await using var c = new RoomClient(backoffMs: _ => 10);
        await c.ConnectAsync(Relay, "r", _ =>
        {
            calls++;
            if (calls == 1) throw new TaskCanceledException("refresh timed out");
            return Task.FromResult(TokenResult.SignInAgain);
        });
        await WaitUntil(() => c.Unauthorized);
        Assert.Equal(2, calls);
        Assert.Equal("다시 로그인 필요", c.Status);
    }

    [Fact]
    public async Task Transient_token_failure_backs_off_and_asks_again()
    {
        // Review (ECC): waking from sleep with Wi-Fi still down must not end in "sign in again" for good.
        int calls = 0;
        await using var c = new RoomClient(backoffMs: _ => 10);
        await c.ConnectAsync(Relay, "r", _ => Task.FromResult(++calls < 3 ? TokenResult.TryLater : TokenResult.SignInAgain));
        await WaitUntil(() => c.Unauthorized);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task Send_while_not_connected_reports_false()
    {
        await using var c = new RoomClient();
        Assert.False(await c.SendAsync("{\"t\":\"jump\"}"));
    }

    [Theory]
    [InlineData(400, true)]
    [InlineData(401, true)]
    [InlineData(403, true)]
    [InlineData(429, false)]
    [InlineData(500, false)]
    [InlineData(503, false)]
    public void Refresh_rejection_is_only_400_401_403(int status, bool rejected) =>
        Assert.Equal(rejected, RefreshResponse.IsRejection(status));
}
