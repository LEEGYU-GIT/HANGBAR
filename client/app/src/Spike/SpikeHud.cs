using System.Diagnostics;
using Godot;

namespace Hangbar.App.Spike;

/// <summary>Spike-only readout: fps, process CPU %, working set MB. Updates once a second.</summary>
public partial class SpikeHud : Label
{
    readonly Process _proc = Process.GetCurrentProcess();
    TimeSpan _lastCpu;
    DateTime _lastWall;
    double _acc;

    public double CpuPercent { get; private set; }
    public double WorkingSetMb { get; private set; }
    public string Extra { get; set; } = "";

    public override void _Ready()
    {
        _lastCpu = _proc.TotalProcessorTime;
        _lastWall = DateTime.UtcNow;
        AddThemeColorOverride("font_color", new Color(1, 1, 1));
        AddThemeColorOverride("font_outline_color", new Color(0, 0, 0));
        AddThemeConstantOverride("outline_size", 4);
    }

    public override void _Process(double delta)
    {
        _acc += delta;
        if (_acc < 1.0) return;
        _acc = 0;
        _proc.Refresh();
        var now = DateTime.UtcNow;
        var cpu = _proc.TotalProcessorTime;
        CpuPercent = (cpu - _lastCpu).TotalMilliseconds / (now - _lastWall).TotalMilliseconds / System.Environment.ProcessorCount * 100.0;
        WorkingSetMb = _proc.WorkingSet64 / (1024.0 * 1024.0);
        _lastCpu = cpu;
        _lastWall = now;
        Text = $"fps {Engine.GetFramesPerSecond():0}  cpu {CpuPercent:0.0}%  mem {WorkingSetMb:0}MB{Extra}";
    }
}
