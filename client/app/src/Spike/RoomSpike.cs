using System.Text.Json;
using Godot;
using Hangbar.Core.Net;
using Hangbar.App.Overlay;

namespace Hangbar.App.Spike;

/// <summary>
/// Spike for risk 4: joins room "m0-test" on the relay. Poking my figure sends {"t":"jump"}; chat sends
/// {"t":"chat"}. Each remote user gets a figure on the primary overlay that jumps and shows a 4s bubble.
/// HUD shows round-trip P50/P95.
/// </summary>
public partial class RoomSpike : Node
{
    public const string RoomId = "m0-test";

    OverlayManager _overlays = null!;
    ChatWindow? _chat;
    RoomClient? _client;
    readonly Dictionary<string, StickFigure> _remote = new();
    double _hudTick;

    public void Init(OverlayManager overlays, ChatWindow? chat)
    {
        _overlays = overlays;
        _chat = chat;
        _overlays.Rebuilt += HookFigures;
        HookFigures();
        if (_chat is not null) _chat.Submitted += text => Send(new { t = "chat", text });
    }

    public void Connect(Uri relay, TokenProvider tokens)
    {
        if (_client is not null) _ = _client.DisposeAsync(); // one connection at a time
        // Spike: HANGBAR_PING_MS lowers the keepalive interval to sample latency (default 20s, review I3).
        int ping = int.TryParse(System.Environment.GetEnvironmentVariable("HANGBAR_PING_MS"), out var p) ? p : 20_000;
        _client = new RoomClient(ping);
        _ = _client.ConnectAsync(relay, RoomId, tokens);
        GD.Print($"[room] connecting to {RoomId}");
    }

    void HookFigures()
    {
        _remote.Clear();
        foreach (var w in _overlays.Windows)
            if (w.GetNodeOrNull<StickFigure>("Figure") is { } f)
                f.Poked += () => Send(new { t = "jump" });
    }

    void Send(object msg)
    {
        if (_client is null) return;
        _ = _client.SendAsync(JsonSerializer.Serialize(msg));
    }

    public override void _Process(double delta)
    {
        if (_client is null) return;
        while (_client.TryDequeue(out var json)) Handle(json);

        _hudTick += delta;
        if (_hudTick >= 1.0 && _overlays.Hud is { } hud)
        {
            _hudTick = 0;
            var l = _client.Latency;
            hud.Extra = $"\nroom {_client.Status}  rtt p50 {l.P50:0}ms p95 {l.P95:0}ms (n={l.Count})";
        }
    }

    void Handle(string json)
    {
        // Room members control this input: one odd message must not stop the rest of the frame's queue (ECC review).
        try { HandleUnsafe(json); }
        catch (Exception e) when (e is JsonException or InvalidOperationException) { GD.Print($"[room] dropped malformed message ({e.GetType().Name})"); }
    }

    static string? Str(JsonElement root, string name) =>
        root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    void HandleUnsafe(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return;
        var t = Str(root, "t");
        var from = Str(root, "from") ?? "?";
        var who = from[..Math.Min(8, from.Length)];
        var figure = RemoteFigure(from);
        switch (t)
        {
            case "jump":
                figure?.Jump();
                GD.Print($"[room] jump from {who}");
                break;
            case "chat":
                var text = Str(root, "text") ?? "";
                if (text.Length > 500) text = text[..500]; // same cap as our own chat box
                _chat?.Append(who, text);
                figure?.Say(text);
                GD.Print($"[room] chat from {who} len={text.Length}");
                break;
        }
    }

    StickFigure? RemoteFigure(string from)
    {
        if (_remote.TryGetValue(from, out var f) && IsInstanceValid(f)) return f;
        var w = _overlays.Windows.FirstOrDefault(o => o.Monitor.Primary) ?? _overlays.Windows.FirstOrDefault();
        if (w is null) return null;
        var mine = w.GetNodeOrNull<StickFigure>("Figure");
        f = new StickFigure { Name = $"Remote_{_remote.Count}", FloorY = mine?.FloorY ?? w.Size.Y - 80, MaxX = w.Size.X };
        f.Position = new Vector2(w.Size.X * (0.5f + 0.1f * _remote.Count), f.FloorY);
        w.AddChild(f);
        _remote[from] = f;
        return f;
    }

    public override void _ExitTree()
    {
        if (_client is not null) _ = _client.DisposeAsync();
    }
}
