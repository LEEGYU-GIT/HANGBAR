namespace Hangbar.Core;

/// <summary>What the browser brought back to the loopback listener after Google sign-in.</summary>
public sealed record LoopbackCallback(string? Code, string? Error)
{
    /// <summary>Parses an HTTP request line like "GET /callback?code=... HTTP/1.1". Null if it isn't the callback.</summary>
    public static LoopbackCallback? Parse(string requestLine)
    {
        var parts = requestLine.Split(' ');
        if (parts.Length < 2 || parts[0] != "GET") return null;
        var target = parts[1];
        int q = target.IndexOf('?');
        var path = q < 0 ? target : target[..q];
        if (path != "/callback" && path != "/callback/") return null;

        string? code = null, error = null, description = null;
        if (q >= 0)
        {
            foreach (var pair in target[(q + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = pair.IndexOf('=');
                var key = eq < 0 ? pair : pair[..eq];
                var value = eq < 0 ? "" : Uri.UnescapeDataString(pair[(eq + 1)..].Replace('+', ' '));
                switch (key)
                {
                    case "code": code = value; break;
                    case "error": error = value; break;
                    case "error_description": description = value; break;
                }
            }
        }
        if (code is { Length: > 0 }) return new LoopbackCallback(code, null);
        return new LoopbackCallback(null, description ?? error ?? "no code");
    }
}
