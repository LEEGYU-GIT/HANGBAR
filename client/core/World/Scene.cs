using Hangbar.Core.World.Gen;

namespace Hangbar.Core.World;

/// <summary>
/// Scene variants (v2 design §6-1): the kind of place inside a biome. Part 1 builds the first four (their numbers stay
/// fixed); the rest are recognized from photos now and built in part 2.
/// </summary>
public enum Scene
{
    Beach, Pool, House, Mountain,
    Meadow, Farm,                  // Field
    CountryRoad, Boulevard, Alley, // Road
    RiverLake,                     // Water (with Beach, Pool)
    Forest, Park,                  // Forest (with Mountain)
    Downtown, Residential,         // City
    Cafe, Shop, Office,            // Indoor (with House)
    SubwayStation, TrainStation,   // Transit
}

public static class Scenes
{
    public static Biome BiomeOf(Scene s) => s switch
    {
        Scene.Meadow or Scene.Farm => Biome.Field,
        Scene.CountryRoad or Scene.Boulevard or Scene.Alley => Biome.Road,
        Scene.Beach or Scene.Pool or Scene.RiverLake => Biome.Water,
        Scene.Forest or Scene.Mountain or Scene.Park => Biome.Forest,
        Scene.Downtown or Scene.Residential => Biome.City,
        Scene.House or Scene.Cafe or Scene.Shop or Scene.Office => Biome.Indoor,
        _ => Biome.Transit,
    };

    /// <summary>The scene a biome becomes when only the biome is known, e.g. from the color guess (v2 design §6-1).</summary>
    public static Scene DefaultFor(Biome b) => b switch
    {
        Biome.Road => Scene.CountryRoad,
        Biome.Water => Scene.Beach,
        Biome.Forest => Scene.Forest,
        Biome.City => Scene.Downtown,
        Biome.Indoor => Scene.House,
        Biome.Transit => Scene.SubwayStation,
        _ => Scene.Meadow,
    };

    /// <summary>Whether <see cref="Create"/> can build this scene yet (part 1: four of the nineteen).</summary>
    public static bool IsBuilt(Scene s) => s is Scene.Beach or Scene.Pool or Scene.House or Scene.Mountain or Scene.Meadow or Scene.Farm or Scene.CountryRoad;

    /// <summary>"beach", "Pool", … (case ignored); null when unknown.</summary>
    public static Scene? FromName(string name) => Enum.TryParse<Scene>(name, true, out var s) && Enum.IsDefined(s) ? s : null;

    public static IScene Create(Scene s) => s switch
    {
        Scene.Beach => new BeachScene(),
        Scene.Pool => new PoolScene(),
        Scene.House => new HouseScene(),
        Scene.Mountain => new MountainScene(),
        Scene.Meadow => new MeadowScene(),
        Scene.Farm => new FarmScene(),
        Scene.CountryRoad => new CountryRoadScene(),
        _ => throw new NotSupportedException($"scene {s} is not built yet"),
    };
}
