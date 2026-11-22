using System.Text.Json;
using Godot;
using Hangbar.App.Net;
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

    public void Connect(Uri relay, string accessToken)
    {
        _client = new RoomClient();
        _ = _client.ConnectAsync(relay, RoomId, accessToken);
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
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var t = root.TryGetProperty("t", out var tp) ? tp.GetString() : null;
        var from = root.TryGetProperty("from", out var fp) ? fp.GetString() ?? "?" : "?";
        var who = from[..Math.Min(8, from.Length)];
        var figure = RemoteFigure(from);
        switch (t)
        {
            case "jump":
                figure?.Jump();
                GD.Print($"[room] jump from {who}");
                break;
            case "chat":
                var text = root.TryGetProperty("text", out var tx) ? tx.GetString() ?? "" : "";
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
