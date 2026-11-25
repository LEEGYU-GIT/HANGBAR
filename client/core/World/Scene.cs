using Hangbar.Core.World.Gen;

namespace Hangbar.Core.World;

/// <summary>Scene variants of the v2 generator (v2 design §6). Part 1 builds these four.</summary>
public enum Scene { Beach, Pool, House, Mountain }

public static class Scenes
{
    public static Biome BiomeOf(Scene s) => s switch
    {
        Scene.Beach or Scene.Pool => Biome.Water,
        Scene.House => Biome.Indoor,
        _ => Biome.Forest,
    };

    /// <summary>"beach", "Pool", … (case ignored); null when unknown.</summary>
    public static Scene? FromName(string name) => Enum.TryParse<Scene>(name, true, out var s) && Enum.IsDefined(s) ? s : null;

    public static IScene Create(Scene s) => s switch
    {
        Scene.Beach => new BeachScene(),
        Scene.Pool => new PoolScene(),
        Scene.House => new HouseScene(),
        Scene.Mountain => new MountainScene(),
        _ => throw new NotSupportedException($"scene {s} is not built yet"),
    };
}
