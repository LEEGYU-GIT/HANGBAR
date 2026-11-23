using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Text;

namespace Hangbar.Core.Net;

/// <summary>Answer of a <see cref="TokenProvider"/>.</summary>
public readonly record struct TokenResult(string? Token, bool Rejected)
{
    public static TokenResult Ok(string token) => new(token, false);
    /// <summary>No session, or the refresh token is no longer accepted: the user must sign in again.</summary>
    public static readonly TokenResult SignInAgain = new(null, true);
    /// <summary>No token right now (network down, Supabase hiccup): back off and ask again.</summary>
    public static readonly TokenResult TryLater = new(null, false);
}

/// <summary>Supplies an access token for (re)connecting. forceRefresh=true after the relay rejected the last one.</summary>
public delegate Task<TokenResult> TokenProvider(bool forceRefresh);

/// <summary>How a failed Supabase refresh response is read.</summary>
public static class RefreshResponse
{
    /// <summary>400/401/403 mean the refresh token (or key) is refused; 429, 5xx and everything else are worth retrying.</summary>
    public static bool IsRejection(int status) => status is 400 or 401 or 403;
}

/// <summary>
/// WebSocket connection to one room on the relay (design §7-3).
/// Keepalive is the plain text "ping"/"pong" pair, answered by the relay's auto-response so it never wakes
/// the Durable Object; only one ping is outstanding at a time and its round trip is timed locally.
/// Reconnects with exponential backoff; on 401 it asks for a refreshed token once, then gives up.
/// Received messages are queued; the game thread drains them with TryDequeue.
/// </summary>
public sealed class RoomClient : IAsyncDisposable
{
    const int ConnectTimeoutMs = 10_000;
    readonly int _pingIntervalMs;
    readonly int _pongTimeoutMs;

    readonly ConcurrentQueue<string> _inbox = new();
    readonly Stopwatch _clock = Stopwatch.StartNew();
    readonly CancellationTokenSource _life = new();
    readonly SemaphoreSlim _sendLock = new(1, 1);
    ClientWebSocket? _ws;
    double _pingSentAt = -1; // -1: no ping outstanding

    public LatencyStats Latency { get; } = new();
    public string Status { get; private set; } = "연결 전";
    public bool Unauthorized { get; private set; }
    /// <summary>Why the last attempt failed (exception type, HTTP status), so a wrong relay URL does not look like a flaky network.</summary>
    public string? LastError { get; private set; }

    readonly Func<int, int> _backoffMs;

    /// <param name="pingIntervalMs">Keepalive interval. 20s by default; the spike may lower it to sample latency.</param>
    /// <param name="backoffMs">Reconnect delay per attempt; ReconnectBackoff by default (tests pass a short one).</param>
    public RoomClient(int pingIntervalMs = 20_000, Func<int, int>? backoffMs = null)
    {
        _pingIntervalMs = pingIntervalMs;
        _pongTimeoutMs = Math.Max(6_000, pingIntervalMs / 2);
        _backoffMs = backoffMs ?? ReconnectBackoff.DelayMs;
    }

    public bool TryDequeue(out string json) => _inbox.TryDequeue(out json!);

    /// <summary>Connects and keeps the connection alive in the background until disposed.</summary>
    public Task ConnectAsync(Uri baseUri, string roomId, TokenProvider tokens)
    {
        var uri = new Uri(baseUri, $"room/{Uri.EscapeDataString(roomId)}");
        _ = Task.Run(() => RunAsync(uri, tokens, _life.Token));
        return Task.CompletedTask;
    }

    async Task RunAsync(Uri uri, TokenProvider tokens, CancellationToken ct)
    {
        bool forceRefresh = false;
        for (int attempt = 0; !ct.IsCancellationRequested; attempt++)
        {
            TokenResult answer;
            try { answer = await tokens(forceRefresh); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception e)
            {
                // A provider that throws (refresh timeout, odd response) is a transient failure, not the end of the loop.
                LastError = $"token {e.GetType().Name}";
                answer = TokenResult.TryLater;
            }
            if (answer.Rejected)
            {
                Unauthorized = true;
                Status = "다시 로그인 필요";
                return;
            }
            if (answer.Token is not { } token)
            {
                Status = "재연결 대기";
                try { await Task.Delay(_backoffMs(attempt), ct); }
                catch (OperationCanceledException) { return; }
                continue; // keep forceRefresh: the relay's rejection still stands
            }

            var ws = new ClientWebSocket();
            ws.Options.SetRequestHeader("Authorization", $"Bearer {token}");
            ws.Options.CollectHttpResponseDetails = true;
            bool unauthorized = false;
            try
            {
                Status = "연결 중";
                using (var connect = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    connect.CancelAfter(ConnectTimeoutMs);  // an unresponsive server must not hang us forever
                    await ws.ConnectAsync(uri, connect.Token);
                }
                _ws = ws;
                attempt = 0;
                forceRefresh = false;
                Status = "연결됨";
                _pingSentAt = -1;
                using var loop = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var pinger = PingLoop(ws, loop.Token);
                await ReceiveLoop(ws, ct);
                loop.Cancel();
                await pinger.ContinueWith(_ => { });
                // 4001: the relay closed us because the token expired while connected.
                if (ws.CloseStatus == (WebSocketCloseStatus)4001) forceRefresh = true;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (OperationCanceledException) { LastError = "connect timeout"; /* back off and retry */ }
            catch (WebSocketException) when (ws.HttpStatusCode == HttpStatusCode.Unauthorized) { unauthorized = true; }
            catch (Exception e)
            {
                // Network error, or an HTTP answer such as 503 (relay could not fetch auth keys) or 404 (wrong URL).
                LastError = ws.HttpStatusCode != 0 ? $"HTTP {(int)ws.HttpStatusCode}" : e.GetType().Name;
            }
            finally { _ws = null; ws.Dispose(); }

            if (unauthorized)
            {
                if (forceRefresh)
                {
                    // Already refreshed once and still rejected: stop.
                    Unauthorized = true;
                    Status = "다시 로그인 필요";
                    return;
                }
                forceRefresh = true;
                continue; // retry immediately with a refreshed token
            }

            Status = "재연결 대기";
            try { await Task.Delay(_backoffMs(attempt), ct); }
            catch (OperationCanceledException) { return; }
        }
    }

    async Task ReceiveLoop(ClientWebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[64 * 1024];
        while (ws.State == WebSocketState.Open)
        {
            using var ms = new MemoryStream();
            WebSocketReceiveResult r;
            do
            {
                r = await ws.ReceiveAsync(buffer, ct);
                if (r.MessageType == WebSocketMessageType.Close) return;
                ms.Write(buffer, 0, r.Count);
            } while (!r.EndOfMessage);

            var text = Encoding.UTF8.GetString(ms.ToArray());
            // Only the relay's exact auto-response counts as pong; JSON from members never does.
            if (text == "pong")
            {
                if (_pingSentAt >= 0) Latency.Add(_clock.Elapsed.TotalMilliseconds - _pingSentAt);
                _pingSentAt = -1;
            }
            else _inbox.Enqueue(text);
        }
    }

    async Task PingLoop(ClientWebSocket ws, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            double now = _clock.Elapsed.TotalMilliseconds;
            if (_pingSentAt >= 0)
            {
                if (now - _pingSentAt > _pongTimeoutMs)
                {
                    ws.Abort(); // silent drop: no pong for too long; ReceiveLoop ends and we reconnect
                    return;
                }
            }
            else
            {
                _pingSentAt = now;
                await SendRaw(ws, "ping", ct);
            }
            await Task.Delay(Math.Min(_pingIntervalMs, 1000), ct);
            // Wait out the rest of the interval only once the ping was answered.
            while (_pingSentAt < 0 && _clock.Elapsed.TotalMilliseconds - now < _pingIntervalMs && !ct.IsCancellationRequested)
                await Task.Delay(250, ct);
        }
    }

    /// <summary>
    /// Sends one message. False when not connected or the socket died mid-send (e.g. during a reconnect);
    /// never throws, so fire-and-forget callers cannot lose an exception.
    /// </summary>
    public async Task<bool> SendAsync(string json)
    {
        if (_ws is not { State: WebSocketState.Open } ws) return false;
        try { await SendRaw(ws, json, _life.Token); return true; }
        catch (Exception e) when (e is WebSocketException or ObjectDisposedException or OperationCanceledException or InvalidOperationException)
        {
            LastError = $"send {e.GetType().Name}";
            return false;
        }
    }

    async Task SendRaw(ClientWebSocket ws, string text, CancellationToken ct)
    {
        await _sendLock.WaitAsync(ct);
        try { await ws.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, ct); }
        finally { _sendLock.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        _life.Cancel();
        if (_ws is { State: WebSocketState.Open } ws)
            try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None); } catch { }
    }
}
