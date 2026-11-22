using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Hangbar.Core;

namespace Hangbar.App.Net;

public sealed record Session(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt, string UserId);

/// <summary>
/// Google sign-in through Supabase with PKCE and a loopback redirect (design §8-1).
/// The callback listener is a tiny TCP server on 127.0.0.1 (HttpListener can need a URL ACL for 127.0.0.1).
/// Session is kept in memory only during M0.
/// </summary>
public sealed class AuthLoopback
{
    public static readonly int[] Ports = { 53682, 53683, 53684 };
    static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

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
        try
        {
            var verifier = Pkce.CreateVerifier();
            var redirect = $"http://127.0.0.1:{port}/callback";
            _openBrowser($"{_supabaseUrl}/auth/v1/authorize?provider=google" +
                         $"&redirect_to={Uri.EscapeDataString(redirect)}" +
                         $"&code_challenge={Pkce.ChallengeS256(verifier)}&code_challenge_method=s256");

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(Timeout);
            var callback = await WaitForCallbackAsync(listener, timeout.Token);
            if (callback?.Code is not { } code)
            {
                Log?.Invoke($"[auth] sign-in failed: {callback?.Error ?? "timeout"}");
                return null;
            }
            return await ExchangeAsync(code, verifier, ct);
        }
        catch (OperationCanceledException)
        {
            Log?.Invoke("[auth] sign-in timed out");
            return null;
        }
        finally
        {
            listener.Stop();
            ListeningPort = null;
        }
    }

    static (TcpListener, int) Listen()
    {
        foreach (var port in Ports)
        {
            var l = new TcpListener(IPAddress.Loopback, port);
            try { l.Start(); return (l, port); }
            catch (SocketException) { /* port in use: try the next one */ }
        }
        throw new InvalidOperationException("로그인용 포트 53682~53684가 모두 사용 중입니다.");
    }

    static async Task<LoopbackCallback?> WaitForCallbackAsync(TcpListener listener, CancellationToken ct)
    {
        while (true)
        {
            using var client = await listener.AcceptTcpClientAsync(ct);
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            var line = await reader.ReadLineAsync(ct) ?? "";
            var cb = LoopbackCallback.Parse(line);
            var body = cb is null ? "" : cb.Code is not null
                ? "<html><meta charset=utf-8><body>로그인 완료. 이 창을 닫고 HANGBAR로 돌아가세요.</body></html>"
                : "<html><meta charset=utf-8><body>로그인에 실패했습니다. HANGBAR에서 다시 시도해 주세요.</body></html>";
            var bytes = Encoding.UTF8.GetBytes(body);
            var head = Encoding.ASCII.GetBytes($"HTTP/1.1 {(cb is null ? "404 Not Found" : "200 OK")}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(head, ct);
            await stream.WriteAsync(bytes, ct);
            if (cb is not null) return cb;
        }
    }

    async Task<Session?> ExchangeAsync(string code, string verifier, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_supabaseUrl}/auth/v1/token?grant_type=pkce")
        {
            Content = JsonContent.Create(new { auth_code = code, code_verifier = verifier }),
        };
        req.Headers.Add("apikey", _publishableKey);
        HttpResponseMessage res;
        try { res = await _http.SendAsync(req, ct); }
        catch (HttpRequestException e)
        {
            Log?.Invoke($"[auth] token exchange failed: network ({e.HttpRequestError})");
            return null;
        }
        using var _ = res;
        if (!res.IsSuccessStatusCode)
        {
            Log?.Invoke($"[auth] token exchange failed: HTTP {(int)res.StatusCode}");
            return null;
        }
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;
        var session = new Session(
            root.GetProperty("access_token").GetString()!,
            root.GetProperty("refresh_token").GetString()!,
            DateTimeOffset.FromUnixTimeSeconds(root.GetProperty("expires_at").GetInt64()),
            root.GetProperty("user").GetProperty("id").GetString()!);
        Log?.Invoke($"[auth] signed in user={session.UserId[..Math.Min(8, session.UserId.Length)]}");
        return session;
    }
}
