using Hangbar.Core;

namespace Hangbar.Core.Tests;

public class LoopbackCallbackTests
{
    [Fact]
    public void Reads_code_from_callback_request_line() =>
        Assert.Equal(new LoopbackCallback("abc-123", null),
            LoopbackCallback.Parse("GET /callback?code=abc-123&state=x HTTP/1.1"));

    [Fact]
    public void Reads_error_description_when_sign_in_failed() =>
        Assert.Equal(new LoopbackCallback(null, "access denied"),
            LoopbackCallback.Parse("GET /callback?error=access_denied&error_description=access%20denied HTTP/1.1"));

    [Fact]
    public void Other_paths_are_not_callbacks() =>
        Assert.Null(LoopbackCallback.Parse("GET /favicon.ico HTTP/1.1"));

    [Fact]
    public void Garbage_is_not_a_callback() =>
        Assert.Null(LoopbackCallback.Parse("hello"));
}
