using System.Linq;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// GR-LV4: Level 12's carve and its stretch on the real scene, with the reach measured on the
/// Moon. The shared rules are <c>TraversalStretchContentTests</c> and read normal gravity;
/// these are this level's own facts.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level12TraversalStretchTests {
    private const string ScenePath = "res://scenes/campaign/Level_12_Lunar.tscn";
    private static readonly string[] Heroes = {
        "cleopatra", "einstein", "joan", "leonardo", "lincoln", "mozart", "shakespeare", "tesla", "tubman"
    };

    /// <summary>A carve, not a shift, with no trench and no swarm: the Moon's floor carries on, and the lifts and the gravity tiles did not move.</summary>
    [TestCase]
    public void TranquilityWasCarvedWithNoTrenchNoSwarmAndTheLiftsUnmoved() {
        using var fixture = new GrLevelFixture<Level12Controller>(ScenePath, "einstein");
        Level12Controller level = fixture.Level;
        AssertThat(level.TranquilityLock.Area.End.X).IsEqual(2000f);
        AssertThat(level.VentFieldLock.Area.Position.X).IsEqual(3020f);

        TraversalStretch crossing = level.GetChildren().OfType<TraversalStretch>().Single();
        AssertThat(crossing.MinX).IsEqual(2020f);
        AssertThat(crossing.MaxX).IsEqual(3000f);
        AssertThat(crossing.HasTrench).IsFalse();
        AssertThat(level.ParSeconds).IsEqual(570f);
        AssertThat(level.AuthoredSecretCachePosition).IsEqual(new Vector2(2140f, 1285f));

        // Ruling 1b++: no swarm row here.
        LevelRewardManifest manifest = LevelRewardManifest.LoadFor(12);
        AssertThat(manifest.ParseWaves().Any(wave => wave.Item1.Contains("swarm"))).IsFalse();

        // Unmoved: the lifts, the gravity tiles, both later anchors and the font. The spire lift is still held.
        AssertThat(level.SpireLift.Enabled).IsFalse();
        AssertThat(level.GravityFields.Count).IsEqual(2);
        AssertThat(Level12Controller.GravityFieldSpans[0].EndX).IsEqual(Level12Controller.ArenaStartX);
        AssertThat(Level12Controller.SpireLiftBottomY).IsEqual(Level12Controller.SurfaceY + 16f);
        AssertThat(level.AuthoredRestorationFontPosition?.X).IsEqual(9400f);
    }

    /// <summary>
    /// The reach, on the Moon. Every hero clears every hop of the line and the route with its own
    /// jump at the regolith's gravity, and also both shortcuts: on this level the shortcuts gate
    /// nobody and only save the climb. Lincoln's double jump really makes the rise, in real frames.
    /// </summary>
    [TestCase(Timeout = 60000)]
    public async Task EveryHeroClearsTheHopsAndBothShortcutsOnTheMoon() {
        TraversalContentKit.LevelFact fact = TraversalContentKit.Levels().Single(level => level.Index == 12);
        TraversalContentKit.RouteFact route = fact.Routes.Single();
        float moon = Level12Controller.RegolithGravityScale;
        foreach (string heroID in Heroes) {
            TraversalContentKit.HeroJump hero = TraversalContentKit.LoadHero(heroID);
            float rise = StoryAirPhysics.DoubleJumpRise(hero.LaunchSpeed, hero.Weight, moon);
            float burstRise = TraversalRules.HopRise(route.BurstFrom, route.BurstTo);
            float reach = StoryAirPhysics.AirtimeFrames(hero.LaunchSpeed, hero.Weight, 2, moon, -burstRise) * hero.RunSpeed / 60f;
            GD.Print($"[moon] {heroID}: double rise {rise:0} px, reach at {burstRise:0} up {reach:0} px");
            float riseShortcut = TraversalRules.HopRise(route.RiseFrom, route.RiseTo);
            AssertThat(rise >= riseShortcut + TraversalContentKit.RiseMarginPixels)
                .OverrideFailureMessage($"{heroID} rises {rise:0} px on the Moon, under the rise shortcut's {riseShortcut:0}.").IsTrue();
            float burstGap = TraversalRules.HopGap(route.BurstFrom, route.BurstTo);
            AssertThat(reach >= burstGap * TraversalContentKit.GapMargin)
                .OverrideFailureMessage($"{heroID} reaches {reach:0} px on the Moon, under the burst shortcut's {burstGap:0}.").IsTrue();
        }

        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new GrLevelFixture<Level12Controller>(ScenePath, "lincoln");
        PlayerController player = fixture.Level.Player;
        var source = new BufferedInputSource();
        try {
            await GrLevelSteps.Settle(tree);
            InputManager.Instance.SetInputSource(player.PlayerIndex, source);
            player.GlobalPosition = new Vector2(2140f, route.RiseFrom.Top - 2f);
            player.Velocity = Vector2.Zero;
            fixture.Level.SyncGravityFieldToPlayer();
            await GrLevelSteps.Frames(tree, 4);
            AssertThat(Mathf.IsEqualApprox(player.EnvironmentGravityScale, moon)).IsTrue();
            float start = player.GlobalPosition.Y;
            float peak = start;
            var samples = new System.Collections.Generic.List<string>();
            await GrLevelSteps.DoubleJump(tree, player, source, 0f, () => {
                peak = Mathf.Min(peak, player.GlobalPosition.Y);
                if (samples.Count < 6) samples.Add($"{player.GlobalPosition} v={player.Velocity} floor={player.IsOnFloor()} {player.CurrentState}");
                return start - peak > 410f;
            }, 240);
            AssertThat(start - peak > 410f)
                .OverrideFailureMessage($"Lincoln rose only {start - peak:0} px on the Moon in real frames: {string.Join(" | ", samples)}.").IsTrue();
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
        }
    }

    /// <summary>The line stands above both vents' strips: a hero on a step is safe through an active window, and on the floor is not.</summary>
    [TestCase(Timeout = 60000)]
    public async Task TheLineClearsTheVentsAndTheFloorDoesNot() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new GrLevelFixture<Level12Controller>(ScenePath, "einstein");
        Level12Controller level = fixture.Level;
        PlayerController hero = level.Player;
        await GrLevelSteps.Settle(tree);
        var vent = level.GetChildren().OfType<StoryCyclicHazard>().Single(h => h.HazardID == "level_12.vent_mare_crossing");
        var regolith = level.GetChildren().OfType<StoryCyclicHazard>().Single(h => h.HazardID == "level_12.vent_regolith");

        // On the one-way over the second vent, and on the crumbling step over the first.
        foreach ((StoryCyclicHazard lane, float x, float top) in new[] { (vent, 2700f, 1660f), (regolith, 2400f, 1660f) }) {
            hero.GlobalPosition = new Vector2(x, top - 2f);
            hero.Velocity = Vector2.Zero;
            // Wherever the step really is, the hero has come to rest on it.
            for (int frame = 0; frame < 90; frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                if (frame > 2 && hero.IsOnFloor()) break;
            }
            int health = hero.CurrentHP;
            lane.ForcePhase(HazardPhase.Active, 0.2f);
            await GrLevelSteps.Frames(tree, 6);
            AssertThat(hero.CurrentHP)
                .OverrideFailureMessage($"'{lane.HazardID}' reached a hero on the line at x {x:0}.").IsEqual(health);
        }

        hero.GlobalPosition = new Vector2(2720f, Level12Controller.SurfaceY - 2f);
        hero.Velocity = Vector2.Zero;
        await GrLevelSteps.Frames(tree, 3);
        int before = hero.CurrentHP;
        vent.ForcePhase(HazardPhase.Active, 0.2f);
        await GrLevelSteps.Frames(tree, 6);
        AssertThat(hero.CurrentHP < before).OverrideFailureMessage("The vent missed a hero on the floor.").IsTrue();
    }
}
