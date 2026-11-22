using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using Hangbar.Core;

namespace Hangbar.App.Net;

/// <summary>
/// Supplies an access token for (re)connecting. forceRefresh=true after the relay rejected the last one.
/// Returns null when no valid token can be obtained (user must sign in again).
/// </summary>
public delegate Task<string?> TokenProvider(bool forceRefresh);

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

    /// <param name="pingIntervalMs">Keepalive interval. 20s by default; the spike may lower it to sample latency.</param>
    public RoomClient(int pingIntervalMs = 20_000)
    {
        _pingIntervalMs = pingIntervalMs;
        _pongTimeoutMs = Math.Max(6_000, pingIntervalMs / 2);
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
            var token = await tokens(forceRefresh);
            if (token is null)
            {
                Unauthorized = true;
                Status = "다시 로그인 필요";
                return;
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
            catch (OperationCanceledException) { /* connect timed out: back off and retry */ }
            catch (WebSocketException) when (ws.HttpStatusCode == HttpStatusCode.Unauthorized) { unauthorized = true; }
            catch (Exception) { /* network error: fall through to backoff */ }
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
            try { await Task.Delay(ReconnectBackoff.DelayMs(attempt), ct); }
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

    public Task SendAsync(string json) => _ws is { State: WebSocketState.Open } ws ? SendRaw(ws, json, _life.Token) : Task.CompletedTask;

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
