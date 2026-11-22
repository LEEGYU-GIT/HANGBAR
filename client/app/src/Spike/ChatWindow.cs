using Godot;

namespace Hangbar.App.Spike;

/// <summary>
/// Ordinary (non-transparent) window for chat. Korean input is taken only here, never on the
/// transparent overlays, to avoid IME trouble there (design §6-6).
/// </summary>
public partial class ChatWindow : Window
{
    public const int MaxChars = 500;

    RichTextLabel _log = null!;
    LineEdit _input = null!;

    public event Action<string>? Submitted;

    public override void _Ready()
    {
        Title = "HANGBAR 채팅 (시험)";
        Size = new Vector2I(380, 480);
        Unresizable = false;
        CloseRequested += Hide;

        var font = new SystemFont { FontNames = new[] { "Malgun Gothic", "Segoe UI" } };
        var theme = new Theme { DefaultFont = font, DefaultFontSize = 16 };

        var root = new VBoxContainer { Theme = theme };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 6);

        _log = new RichTextLabel { ScrollFollowing = true, SizeFlagsVertical = Control.SizeFlags.ExpandFill, SelectionEnabled = true };
        _input = new LineEdit { PlaceholderText = $"메시지 입력 (Enter로 보내기, {MaxChars}자까지)", MaxLength = MaxChars };
        _input.TextSubmitted += OnSubmit;

        root.AddChild(_log);
        root.AddChild(_input);
        AddChild(root);
    }

    void OnSubmit(string text)
    {
        text = text.Trim();
        if (text.Length == 0) return;
        _input.Clear();
        Submitted?.Invoke(text);
    }

    public void Append(string who, string text) => _log.AppendText($"[b]{EscapeBbcode(who)}[/b]  {EscapeBbcode(text)}\n");

    public void FocusInput() => _input.GrabFocus();

    static string EscapeBbcode(string s) => s.Replace("[", "[lb]");
}
