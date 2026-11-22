using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Hangbar.Core;

namespace Hangbar.App.Net;

/// <summary>
/// WebSocket connection to one room on the relay (design §7-3). Pings every 2s to measure round trips,
/// reconnects with exponential backoff, and stops retrying on 401 (token rejected).
/// Received messages are queued; the game thread drains them with TryDequeue.
/// </summary>
public sealed class RoomClient : IAsyncDisposable
{
    readonly ConcurrentQueue<string> _inbox = new();
    readonly Stopwatch _clock = Stopwatch.StartNew();
    readonly CancellationTokenSource _life = new();
    ClientWebSocket? _ws;
    int _pingId;
    double _lastPong;
    const int ConnectTimeoutMs = 10_000, PongTimeoutMs = 6_000;

    public LatencyStats Latency { get; } = new();
    public string Status { get; private set; } = "연결 전";
    public bool Unauthorized { get; private set; }

    public bool TryDequeue(out string json) => _inbox.TryDequeue(out json!);

    /// <summary>Connects and keeps the connection alive in the background until disposed.</summary>
    public Task ConnectAsync(Uri baseUri, string roomId, string accessToken)
    {
        var uri = new Uri(baseUri, $"room/{Uri.EscapeDataString(roomId)}");
        _ = Task.Run(() => RunAsync(uri, accessToken, _life.Token));
        return Task.CompletedTask;
    }

    async Task RunAsync(Uri uri, string token, CancellationToken ct)
    {
        for (int attempt = 0; !ct.IsCancellationRequested; attempt++)
        {
            var ws = new ClientWebSocket();
            ws.Options.SetRequestHeader("Authorization", $"Bearer {token}");
            ws.Options.CollectHttpResponseDetails = true;
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
                Status = "연결됨";
                _lastPong = _clock.Elapsed.TotalMilliseconds;
                using var loop = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var pinger = PingLoop(ws, loop.Token);
                await ReceiveLoop(ws, ct);
                loop.Cancel();
                await pinger.ContinueWith(_ => { });
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (OperationCanceledException) { /* connect timed out: back off and retry */ }
            catch (WebSocketException) when (ws.HttpStatusCode == HttpStatusCode.Unauthorized)
            {
                Unauthorized = true;
                Status = "다시 로그인 필요";
                return;
            }
            catch (Exception) { /* network error: fall through to backoff */ }
            finally { _ws = null; ws.Dispose(); }

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
            if (TryReadPong(text, out double sentAt))
            {
                _lastPong = _clock.Elapsed.TotalMilliseconds;
                Latency.Add(_lastPong - sentAt);
            }
            else _inbox.Enqueue(text);
        }
    }

    async Task PingLoop(ClientWebSocket ws, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            if (_clock.Elapsed.TotalMilliseconds - _lastPong > PongTimeoutMs)
            {
                ws.Abort(); // silent drop: no pong for too long; ReceiveLoop ends and we reconnect
                return;
            }
            var ping = JsonSerializer.Serialize(new { t = "ping", id = ++_pingId, sentAt = _clock.Elapsed.TotalMilliseconds });
            await SendRaw(ws, ping, ct);
            await Task.Delay(2000, ct);
        }
    }

    static bool TryReadPong(string text, out double sentAt)
    {
        sentAt = 0;
        if (!text.Contains("\"pong\"")) return false;
        using var doc = JsonDocument.Parse(text);
        if (doc.RootElement.GetProperty("t").GetString() != "pong") return false;
        sentAt = doc.RootElement.GetProperty("sentAt").GetDouble();
        return true;
    }

    public Task SendAsync(string json) => _ws is { State: WebSocketState.Open } ws ? SendRaw(ws, json, _life.Token) : Task.CompletedTask;

    readonly SemaphoreSlim _sendLock = new(1, 1);

    async Task SendRaw(ClientWebSocket ws, string json, CancellationToken ct)
    {
        await _sendLock.WaitAsync(ct);
        try { await ws.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, ct); }
        finally { _sendLock.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        _life.Cancel();
        if (_ws is { State: WebSocketState.Open } ws)
            try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None); } catch { }
    }
}
