using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Hangbar.Core;

namespace Hangbar.App.Net;

public sealed record Session(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt, string UserId);

/// <summary>
/// Google sign-in through Supabase with PKCE and a loopback redirect (design §8-1), plus token refresh.
/// The callback listener is a tiny TCP server on 127.0.0.1 (HttpListener can need a URL ACL for 127.0.0.1).
/// Each connection is handled on its own with a 5s read limit and an 8KB request-line cap, so an idle or
/// junk connection (another process, a browser pre-connect) cannot block the real callback.
/// The browser is told "done" only after the token exchange succeeded. Session is in memory only during M0.
/// </summary>
public sealed class AuthLoopback
{
    public static readonly int[] Ports = { 53682, 53683, 53684 };
    static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);
    const int ReadTimeoutMs = 5_000, MaxRequestLine = 8 * 1024;

    const string DonePage = "<html><meta charset=utf-8><body>로그인 완료. 이 창을 닫고 HANGBAR로 돌아가세요.</body></html>";
    const string FailPage = "<html><meta charset=utf-8><body>로그인에 실패했습니다. HANGBAR에서 다시 시도해 주세요.</body></html>";

    readonly string _supabaseUrl;
    readonly string _publishableKey;
    readonly Action<string> _openBrowser;
    readonly HttpClient _http = new();

    public int? ListeningPort { get; private set; }
    public event Action<string>? Log;

    public AuthLoopback(string supabaseUrl, string publishableKey, Action<string> openBrowser)
    {
        _supabaseUrl = supabaseUrl.TrimEnd('/');
        _publishableKey = publishableKey;
        _openBrowser = openBrowser;
    }

    public async Task<Session?> SignInAsync(CancellationToken ct)
    {
        var (listener, port) = Listen();
        ListeningPort = port;
        Log?.Invoke($"[auth] listening on 127.0.0.1:{port}");
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        stop.CancelAfter(Timeout);
        var callbacks = Channel.CreateUnbounded<(LoopbackCallback Callback, NetworkStream Stream, TcpClient Client)>();
        var acceptor = AcceptLoop(listener, callbacks.Writer, stop.Token);
        try
        {
            var verifier = Pkce.CreateVerifier();
            var redirect = $"http://127.0.0.1:{port}/callback";
            _openBrowser($"{_supabaseUrl}/auth/v1/authorize?provider=google" +
                         $"&redirect_to={Uri.EscapeDataString(redirect)}" +
                         $"&code_challenge={Pkce.ChallengeS256(verifier)}&code_challenge_method=s256");

            var (cb, stream, client) = await callbacks.Reader.ReadAsync(stop.Token);
            using (client)
            {
                Session? session = null;
                if (cb.Code is { } code) session = await ExchangeAsync(code, verifier, stop.Token);
                else Log?.Invoke($"[auth] sign-in failed: {cb.Error}");
                await Respond(stream, 200, session is null ? FailPage : DonePage, stop.Token);
                return session;
            }
        }
        catch (OperationCanceledException)
        {
            Log?.Invoke("[auth] sign-in timed out");
            return null;
        }
        finally
        {
            stop.Cancel();
            listener.Stop();
            await acceptor.ContinueWith(_ => { });
            ListeningPort = null;
        }
    }

    /// <summary>Exchanges a refresh token for a new session. Null when Supabase rejects it or the network fails.</summary>
    public async Task<Session?> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_supabaseUrl}/auth/v1/token?grant_type=refresh_token")
        {
            Content = JsonContent.Create(new { refresh_token = refreshToken }),
        };
        return await SendForSession(req, "refresh", ct);
    }

    static (TcpListener, int) Listen()
    {
        foreach (var port in Ports)
        {
            var l = new TcpListener(IPAddress.Loopback, port) { ExclusiveAddressUse = true };
            try { l.Start(); return (l, port); }
            catch (SocketException) { /* port in use: try the next one */ }
        }
        throw new InvalidOperationException("로그인용 포트 53682~53684가 모두 사용 중입니다.");
    }

    static async Task AcceptLoop(TcpListener listener, ChannelWriter<(LoopbackCallback, NetworkStream, TcpClient)> found, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(ct);
                _ = HandleConnection(client, found, ct); // each connection on its own
            }
        }
        catch (Exception) when (ct.IsCancellationRequested) { }
        catch (ObjectDisposedException) { }
    }

    static async Task HandleConnection(TcpClient client, ChannelWriter<(LoopbackCallback, NetworkStream, TcpClient)> found, CancellationToken ct)
    {
        var stream = client.GetStream();
        try
        {
            using var read = CancellationTokenSource.CreateLinkedTokenSource(ct);
            read.CancelAfter(ReadTimeoutMs);
            var line = await ReadRequestLine(stream, read.Token);
            var cb = line is null ? null : LoopbackCallback.Parse(line);
            if (cb is null)
            {
                await Respond(stream, 404, "", ct);
                client.Dispose();
                return;
            }
            // The callback's connection stays open until the exchange finishes; SignInAsync answers it.
            if (!found.TryWrite((cb, stream, client))) client.Dispose();
        }
        catch (Exception)
        {
            client.Dispose();
        }
    }

    /// <summary>Reads up to the first CRLF; null if the line exceeds 8KB or the client stops sending.</summary>
    static async Task<string?> ReadRequestLine(NetworkStream stream, CancellationToken ct)
    {
        var buf = new byte[1024];
        using var line = new MemoryStream();
        while (line.Length < MaxRequestLine)
        {
            int n = await stream.ReadAsync(buf, ct);
            if (n == 0) return null;
            int nl = Array.IndexOf(buf, (byte)'\n', 0, n);
            line.Write(buf, 0, nl < 0 ? n : nl);
            if (nl >= 0) return Encoding.ASCII.GetString(line.ToArray()).TrimEnd('\r');
        }
        return null;
    }

    static async Task Respond(NetworkStream stream, int status, string html, CancellationToken ct)
    {
        try
        {
            var body = Encoding.UTF8.GetBytes(html);
            var head = Encoding.ASCII.GetBytes($"HTTP/1.1 {(status == 200 ? "200 OK" : "404 Not Found")}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(head, ct);
            await stream.WriteAsync(body, ct);
        }
        catch (Exception) { /* browser went away */ }
    }

    async Task<Session?> ExchangeAsync(string code, string verifier, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_supabaseUrl}/auth/v1/token?grant_type=pkce")
        {
            Content = JsonContent.Create(new { auth_code = code, code_verifier = verifier }),
        };
        var s = await SendForSession(req, "token exchange", ct);
        if (s is not null) Log?.Invoke($"[auth] signed in user={s.UserId[..Math.Min(8, s.UserId.Length)]}");
        return s;
    }

    async Task<Session?> SendForSession(HttpRequestMessage req, string what, CancellationToken ct)
    {
        req.Headers.Add("apikey", _publishableKey);
        HttpResponseMessage res;
        try { res = await _http.SendAsync(req, ct); }
        catch (HttpRequestException e)
        {
            Log?.Invoke($"[auth] {what} failed: network ({e.HttpRequestError})");
            return null;
        }
        using var _ = res;
        if (!res.IsSuccessStatusCode)
        {
            Log?.Invoke($"[auth] {what} failed: HTTP {(int)res.StatusCode}");
            return null;
        }
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;
        return new Session(
            root.GetProperty("access_token").GetString()!,
            root.GetProperty("refresh_token").GetString()!,
            DateTimeOffset.FromUnixTimeSeconds(root.GetProperty("expires_at").GetInt64()),
            root.GetProperty("user").GetProperty("id").GetString()!);
    }
}
