namespace Hangbar.Core.World.Gen;

/// <summary>
/// The v2 generator pipeline (v2 design §5): palette contrast → Shape → edge band → strata → underground → Build →
/// weathering → View → hidden mural → surface map.
/// </summary>
public static class WorldGenerator
{
    public static GeneratedWorld Generate(Scene scene, uint[] palette, Mood mood, PixelImage prepared, ulong seed, float horizon = 0.5f) =>
        Generate(Scenes.Create(scene), palette, mood, prepared, seed, horizon);

    public static GeneratedWorld Generate(IScene scene, uint[] palette, Mood mood, PixelImage prepared, ulong seed, float horizon = 0.5f)
    {
        var ctx = new SceneContext(Palette.EnsureContrast(palette), mood, prepared, seed);
        scene.Shape(ctx);
        EdgeBand.Apply(ctx);
        Strata.Fill(ctx);
        scene.Build(ctx);
        var view = scene.View(ctx);
        var landmark = ctx.Landmark ?? throw new InvalidOperationException($"scene {scene.Kind} placed no landmark");
        var mural = view.MuralCandidates.Count > 0 ? view.MuralCandidates[0] : new MuralSpot(view.Spawn, Facing.North, 0, 0, false);

        double dx = view.Target.X - view.Spawn.X, dz = view.Target.Z - view.Spawn.Z;
        float facing = (float)(Math.Atan2(-dx, -dz) * 180 / Math.PI);
        return new GeneratedWorld(ctx.World, SurfaceMap.Of(ctx.World, seed), scene.Kind, mood, seed, view.Spawn, facing,
            Horizon.PitchDegrees(horizon), ctx.Props, ctx.Structures, ctx.BurialSpots, landmark, mural);
    }
}
