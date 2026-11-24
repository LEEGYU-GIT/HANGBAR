using System.Globalization;

namespace Hangbar.Core.World.Gen;

/// <summary>One cell of a part. Material.Air carves the world to air; a null cell (not this type) keeps the world.</summary>
public readonly record struct PartCell(Material Material, ColorRole? Role, uint FixedColor);

/// <summary>
/// A building part written as a text file (v2 design: parts are data, not code).
/// <code>
/// part: fence_segment
/// size: 4 5 1                  # width(x) height(y) depth(z)
/// legend:
///   M = Metal dark             # letter = material role
///   G = Glass #C8E6F0          # or a fixed colour
/// stretch: x 1 2               # optional: repeat this inclusive range to stretch along the axis
/// anchor: door 2 0 0           # optional: name x y z (turns with the part)
/// layer 0:                     # from y=0 up; each layer has depth rows of width letters (row = z, letter = x)
/// M..M
/// </code>
/// Reserved letters: '.' carves air, '_' keeps whatever is there. '#' starts a comment unless it begins a #RRGGBB colour.
/// </summary>
public sealed class Part
{
    public string Name { get; }
    public int Width { get; }
    public int Height { get; }
    public int Depth { get; }
    public IReadOnlyDictionary<string, BlockPos> Anchors { get; }
    public (char Axis, int From, int To)? Stretch { get; }

    readonly PartCell?[] _cells;

    Part(string name, int w, int h, int d, PartCell?[] cells, Dictionary<string, BlockPos> anchors, (char, int, int)? stretch)
    {
        Name = name; Width = w; Height = h; Depth = d; _cells = cells; Anchors = anchors; Stretch = stretch;
    }

    /// <summary>The cell at (x, y, z); null keeps the world.</summary>
    public PartCell? CellAt(int x, int y, int z) => _cells[(y * Depth + z) * Width + x];

    public static Part Parse(string text, string name)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        string partName = name;
        int w = 0, h = 0, d = 0;
        var legend = new Dictionary<char, PartCell>();
        var anchors = new Dictionary<string, BlockPos>();
        (char, int, int)? stretch = null;
        PartCell?[]? cells = null;
        int layer = -1, row = 0;
        bool inLegend = false;

        FormatException Error(int line, string what) => new($"part '{name}' line {line}: {what}");

        for (int i = 0; i < lines.Length; i++)
        {
            int lineNo = i + 1;
            string raw = StripComment(lines[i]);
            string line = raw.Trim();
            if (line.Length == 0) continue;

            if (layer >= 0 && row < d && !line.Contains(':'))
            {
                if (line.Length != w) throw Error(lineNo, $"row has {line.Length} letters, expected {w}");
                for (int x = 0; x < w; x++)
                {
                    char ch = line[x];
                    PartCell? cell = ch switch
                    {
                        '_' => null,
                        '.' => new PartCell(Material.Air, null, 0),
                        _ => legend.TryGetValue(ch, out var c) ? c : throw Error(lineNo, $"letter '{ch}' is not in the legend"),
                    };
                    cells![(layer * d + row) * w + x] = cell;
                }
                row++;
                continue;
            }

            int colon = line.IndexOf(':');
            if (inLegend && colon < 0 && line.Contains('='))
            {
                var parts = line.Split('=', 2);
                string key = parts[0].Trim();
                if (key.Length != 1 || key[0] is '.' or '_' or '#') throw Error(lineNo, "legend key must be one letter (not . _ #)");
                var words = parts[1].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (words.Length != 2) throw Error(lineNo, "legend entry is 'X = Material role' or 'X = Material #RRGGBB'");
                if (!Enum.TryParse<Material>(words[0], true, out var mat)) throw Error(lineNo, $"unknown material '{words[0]}'");
                if (words[1].StartsWith('#'))
                {
                    if (words[1].Length != 7 || !uint.TryParse(words[1][1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
                        throw Error(lineNo, $"bad colour '{words[1]}'");
                    legend[key[0]] = new PartCell(mat, null, rgb);
                }
                else if (Enum.TryParse<ColorRole>(words[1], true, out var role)) legend[key[0]] = new PartCell(mat, role, 0);
                else throw Error(lineNo, $"unknown colour role '{words[1]}'");
                continue;
            }
            inLegend = false;
            if (colon < 0) throw Error(lineNo, "expected 'key: value'");
            string head = line[..colon].Trim(), value = line[(colon + 1)..].Trim();
            var nums = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            switch (head)
            {
                case "part": partName = value; break;
                case "size":
                    if (nums.Length != 3 || !int.TryParse(nums[0], out w) || !int.TryParse(nums[1], out h) || !int.TryParse(nums[2], out d) || w <= 0 || h <= 0 || d <= 0)
                        throw Error(lineNo, "size is 'width height depth', all positive");
                    cells = new PartCell?[w * h * d];
                    break;
                case "legend": inLegend = true; break;
                case "stretch":
                    if (nums.Length != 3 || nums[0].Length != 1 || "xz".IndexOf(nums[0][0]) < 0 || !int.TryParse(nums[1], out int from) || !int.TryParse(nums[2], out int to) || from > to)
                        throw Error(lineNo, "stretch is 'x|z from to'");
                    stretch = (nums[0][0], from, to);
                    break;
                case "anchor":
                    if (nums.Length != 4 || !int.TryParse(nums[1], out int ax) || !int.TryParse(nums[2], out int ay) || !int.TryParse(nums[3], out int az))
                        throw Error(lineNo, "anchor is 'name x y z'");
                    anchors[nums[0]] = new BlockPos(ax, ay, az);
                    break;
                default:
                    if (head.StartsWith("layer ") && int.TryParse(head[6..], out int l))
                    {
                        if (cells is null) throw Error(lineNo, "size must come before layers");
                        if (layer >= 0 && row != d) throw Error(lineNo, $"layer {layer} has {row} rows, expected {d}");
                        if (l < 0 || l >= h) throw Error(lineNo, $"layer {l} is outside height {h}");
                        layer = l;
                        row = 0;
                        break;
                    }
                    throw Error(lineNo, $"unknown key '{head}'");
            }
        }
        if (cells is null) throw new FormatException($"part '{name}': no size");
        if (layer >= 0 && row != d) throw new FormatException($"part '{name}' line {lines.Length}: layer {layer} has {row} rows, expected {d}");
        if (stretch is { } s && s.Item3 >= (s.Item1 == 'x' ? w : d)) throw new FormatException($"part '{name}': stretch range is outside the part");
        return new Part(partName, w, h, d, cells, anchors, stretch);
    }

    /// <summary>'#' starts a comment unless it is followed by six hex digits (a colour).</summary>
    static string StripComment(string line)
    {
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] != '#') continue;
            bool colour = i + 7 <= line.Length && line.Substring(i + 1, 6).All(Uri.IsHexDigit)
                          && (i + 7 == line.Length || char.IsWhiteSpace(line[i + 7]));
            if (!colour) return line[..i];
        }
        return line;
    }
}
