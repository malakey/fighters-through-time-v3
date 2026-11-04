using System.Collections.Generic;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Characters.Abilities;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;
using FlightAbility = FTT.Tests.ContentValidation.TraversalContentKit.FlightAbility;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// GR-PILOT (rulings 7c, 7c+; plan §1.15): every hero reaches every route's reward, and crosses
/// every stretch, on the base kit, measured with that hero's own jump through
/// <see cref="StoryAirPhysics"/>; the shortcuts really work in real frames; and the four kit
/// facts dossier C left unmeasured (Einstein's and Tesla's walls, Joan's and Leonardo's rise)
/// are measured here, because the shortcut tables rest on them.
///
/// <para>The shortcuts gate nobody out and are not exclusive: a hero whose double jump is
/// already long enough simply takes the hop. <c>Report</c> prints who can.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class OptionalRouteReachTests {
    /// <summary>The bursts that work in the air. Rail Charge is a ground move (see the handoff).</summary>
    private static readonly string[] BurstHeroes = { "cleopatra", "einstein", "shakespeare", "tesla", "tubman" };
    /// <summary>The rise and glide kits.</summary>
    private static readonly string[] RiseHeroes = { "joan", "leonardo", "mozart", "shakespeare" };

    [TestCase("cleopatra")]
    [TestCase("einstein")]
    [TestCase("joan")]
    [TestCase("leonardo")]
    [TestCase("lincoln")]
    [TestCase("mozart")]
    [TestCase("shakespeare")]
    [TestCase("tesla")]
    [TestCase("tubman")]
    public void TheHeroCrossesEveryStretchAndReachesEveryRewardOnTheBaseKit(string heroID) {
        TraversalContentKit.HeroJump hero = TraversalContentKit.LoadHero(heroID);
        var issues = new List<string>();
        foreach (TraversalContentKit.LevelFact level in TraversalContentKit.Levels()) {
            if (level.LoadError != null) { issues.Add($"{level.Label}: {level.LoadError}"); continue; }
            foreach (TraversalContentKit.StretchFact stretch in level.Stretches) CheckLine($"{level.Label} '{stretch.ID}'", stretch.Line, hero, issues);
            foreach (TraversalContentKit.RouteFact route in level.Routes) CheckLine($"{level.Label} '{route.ID}'", route.BaseLine, hero, issues);
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    private static void CheckLine(string label, IReadOnlyList<TraversalSurface> line, TraversalContentKit.HeroJump hero, List<string> issues) {
        for (int index = 1; index < line.Count; index++) {
            float gap = TraversalRules.HopGap(line[index - 1], line[index]);
            float rise = TraversalRules.HopRise(line[index - 1], line[index]);
            if (!hero.ClearsOnBaseKit(gap, rise)) {
                issues.Add($"{label}: {hero.ID} cannot hop {line[index - 1]} to {line[index]} (gap {gap:0}, rise {rise:0}; "
                    + $"reach {hero.ReachAt(System.Math.Max(rise, 0f)):0}, double rise {hero.DoubleRise:0})");
            }
        }
    }

    /// <summary>Real frames: every air burst crosses each route's burst shortcut, and it is longer than the slowest base kit's reach.</summary>
    [TestCase(Timeout = 120000)]
    public async Task EveryAirBurstClearsTheBurstShortcutInRealFrames() {
        var issues = new List<string>();
        foreach (TraversalContentKit.LevelFact level in TraversalContentKit.Levels()) {
            foreach (TraversalContentKit.RouteFact route in level.Routes) {
                if (!route.HasBurst) continue;
                Report($"{level.Label} '{route.ID}' burst", route.BurstFrom, route.BurstTo);
                float gap = TraversalRules.HopGap(route.BurstFrom, route.BurstTo);
                float rise = TraversalRules.HopRise(route.BurstFrom, route.BurstTo);
                if (TraversalContentKit.LoadHero("lincoln").ClearsOnBaseKit(gap, rise)) {
                    issues.Add($"{level.Label}: the burst shortcut (gap {gap:0}, rise {rise:0}) is no shortcut: the slowest base kit clears it");
                }
                foreach (string heroID in BurstHeroes) {
                    (bool landed, string report) = await TraversalContentKit.Fly(heroID, route.BurstFrom, route.BurstTo, FlightAbility.Forward);
                    GD.Print($"[reach] {level.Label} burst: {report}");
                    if (!landed) issues.Add(report);
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    /// <summary>Real frames: every rise and glide kit makes each route's rise shortcut, which is higher than most double jumps.</summary>
    [TestCase(Timeout = 120000)]
    public async Task EveryRiseKitClearsTheRiseShortcutInRealFrames() {
        var issues = new List<string>();
        foreach (TraversalContentKit.LevelFact level in TraversalContentKit.Levels()) {
            foreach (TraversalContentKit.RouteFact route in level.Routes) {
                if (!route.HasRise) continue;
                Report($"{level.Label} '{route.ID}' rise", route.RiseFrom, route.RiseTo);
                float rise = TraversalRules.HopRise(route.RiseFrom, route.RiseTo);
                if (rise + TraversalContentKit.RiseMarginPixels <= TraversalContentKit.LoadHero("joan").DoubleRise) {
                    issues.Add($"{level.Label}: the rise shortcut ({rise:0} px) is no shortcut: Joan's double jump clears it");
                }
                foreach (string heroID in RiseHeroes) {
                    (bool landed, string report) = await TraversalContentKit.Fly(heroID, route.RiseFrom, route.RiseTo, FlightAbility.Up);
                    GD.Print($"[reach] {level.Label} rise: {report}");
                    if (!landed) issues.Add(report);
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    // === What dossier C left unmeasured ====================================

    /// <summary>Relativity Warp stops at an Environment wall: a shortcut can never rely on folding through one.</summary>
    [TestCase]
    public async Task EinsteinsWarpStopsShortOfAWallAndPassesAOneWay() {
        var tree = (SceneTree)Engine.GetMainLoop();
        Node2D world = Floor(tree, wallAtX: 120f);
        PlayerController hero = await Spawn(tree, "einstein");
        try {
            BufferedInputSource source = Source(hero);
            await Press(tree, source, 1f, 0f, GameplayButtons.MovementAbility);
            var warp = hero.GetNode<EinsteinRelativityWarp>("MovementAbility");
            for (int frame = 0; frame < 30 && warp.IsExecuting; frame++) await Press(tree, source, 0f, 0f, GameplayButtons.None);
            GD.Print($"[reach] Einstein's warp at a wall 120 px ahead ended at x {hero.GlobalPosition.X:0.0} (authored reach 240)");
            AssertThat(hero.GlobalPosition.X < 120f)
                .OverrideFailureMessage($"The warp passed the wall: x {hero.GlobalPosition.X:0.0}.").IsTrue();
            AssertThat(hero.GlobalPosition.X > 60f)
                .OverrideFailureMessage($"The warp stopped far short: x {hero.GlobalPosition.X:0.0}.").IsTrue();
        } finally {
            Free(hero, world);
        }
    }

    /// <summary>Lightning Blink is an ordinary body move: it stops at an Environment wall too.</summary>
    [TestCase]
    public async Task TeslasBlinkStopsAtAWall() {
        var tree = (SceneTree)Engine.GetMainLoop();
        Node2D world = Floor(tree, wallAtX: 100f);
        PlayerController hero = await Spawn(tree, "tesla");
        try {
            BufferedInputSource source = Source(hero);
            await Press(tree, source, 1f, 0f, GameplayButtons.MovementAbility);
            var blink = hero.GetNode<TeslaLightningBlink>("MovementAbility");
            for (int frame = 0; frame < 60 && blink.IsExecuting; frame++) await Press(tree, source, 0f, 0f, GameplayButtons.None);
            GD.Print($"[reach] Tesla's blink at a wall 100 px ahead ended at x {hero.GlobalPosition.X:0.0} (authored reach 180)");
            AssertThat(hero.GlobalPosition.X < 100f)
                .OverrideFailureMessage($"The blink passed the wall: x {hero.GlobalPosition.X:0.0}.").IsTrue();
        } finally {
            Free(hero, world);
        }
    }

    /// <summary>Ascendant Wings rises 252 px from a standstill (420 px/s for the cast's 36 frames, gravity off).</summary>
    [TestCase]
    public async Task JoansWingsRiseIsMeasured() {
        var tree = (SceneTree)Engine.GetMainLoop();
        Node2D world = Floor(tree);
        PlayerController hero = await Spawn(tree, "joan");
        try {
            BufferedInputSource source = Source(hero);
            float startY = hero.GlobalPosition.Y;
            float peak = startY;
            await Press(tree, source, 0f, 0f, GameplayButtons.MovementAbility);
            for (int frame = 0; frame < 120; frame++) {
                await Press(tree, source, 0f, 0f, GameplayButtons.None);
                peak = Mathf.Min(peak, hero.GlobalPosition.Y);
            }
            float rise = startY - peak;
            GD.Print($"[reach] Joan's Ascendant Wings rises {rise:0.0} px");
            AssertThat(rise >= 200f && rise <= 320f).OverrideFailureMessage($"Joan's wings rose {rise:0.0} px.").IsTrue();
        } finally {
            Free(hero, world);
        }
    }

    /// <summary>Daedalus boosts about 61 px and then glides level enough to cross far more than a jump.</summary>
    [TestCase]
    public async Task LeonardosBoostAndGlideAreMeasured() {
        var tree = (SceneTree)Engine.GetMainLoop();
        Node2D world = Floor(tree);
        PlayerController hero = await Spawn(tree, "leonardo");
        try {
            BufferedInputSource source = Source(hero);
            Vector2 start = hero.GlobalPosition;
            float peak = start.Y;
            await Press(tree, source, 1f, 0f, GameplayButtons.MovementAbility);
            int airFrames = 0;
            for (int frame = 0; frame < 300; frame++) {
                await Press(tree, source, 1f, 0f, GameplayButtons.None);
                peak = Mathf.Min(peak, hero.GlobalPosition.Y);
                if (!hero.IsOnFloor()) airFrames++;
                else if (airFrames > 0) break;
            }
            float rise = start.Y - peak;
            float travelled = hero.GlobalPosition.X - start.X;
            GD.Print($"[reach] Leonardo's boost rises {rise:0.0} px; flying from the floor he travels {travelled:0} px in {airFrames} air frames");
            AssertThat(rise >= 45f && rise <= 80f).OverrideFailureMessage($"Leonardo's boost rose {rise:0.0} px.").IsTrue();
            AssertThat(airFrames > 60).OverrideFailureMessage($"Leonardo glided only {airFrames} frames.").IsTrue();
        } finally {
            Free(hero, world);
        }
    }

    // === Helpers ===========================================================

    private static void Report(string label, TraversalSurface from, TraversalSurface to) {
        float gap = TraversalRules.HopGap(from, to);
        float rise = TraversalRules.HopRise(from, to);
        var clears = new List<string>();
        foreach (string heroID in CharacterRoster.IDs) {
            if (TraversalContentKit.LoadHero(heroID).ClearsOnBaseKit(gap, rise)) clears.Add(heroID);
        }
        GD.Print($"[reach] {label}: gap {gap:0}, rise {rise:0}; on the base kit alone: {string.Join(",", clears)}");
    }

    private static Node2D Floor(SceneTree tree, float? wallAtX = null) {
        var world = new Node2D { Name = "ReachWorld" };
        tree.Root.AddChild(world);
        world.AddChild(Solid(new Rect2(-2000f, 0f, 4000f, 40f)));
        if (wallAtX is float x) world.AddChild(Solid(new Rect2(x, -400f, 40f, 400f)));
        return world;
    }

    private static StaticBody2D Solid(Rect2 rect) {
        var body = new StaticBody2D { Position = rect.GetCenter(), CollisionLayer = CollisionLayers.Environment, CollisionMask = 0 };
        body.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = rect.Size } });
        return body;
    }

    private static readonly Dictionary<ulong, BufferedInputSource> Sources = new();

    private static BufferedInputSource Source(PlayerController hero) => Sources[hero.GetInstanceId()];

    private static async Task<PlayerController> Spawn(SceneTree tree, string heroID) {
        PlayerController hero = CharacterFactory.CreateCharacter(heroID, 0, applyStoryProgression: false);
        tree.Root.AddChild(hero);
        hero.GlobalPosition = new Vector2(0f, -2f);
        hero.IsFacingRight = true;
        var source = new BufferedInputSource();
        Sources[hero.GetInstanceId()] = source;
        InputManager.Instance.SetInputSource(hero.PlayerIndex, source);
        for (int frame = 0; frame < 30 && !(hero.IsOnFloor() && hero.CurrentState == CharacterState.Idle); frame++) {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        }
        return hero;
    }

    private static async Task Press(SceneTree tree, BufferedInputSource source, float h, float v, GameplayButtons held) {
        source.SetNextFrame(PlayerInputFrame.Create(0, h, v, held, GameplayButtons.None));
        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
    }

    private static void Free(PlayerController hero, Node2D world) {
        if (hero != null && GodotObject.IsInstanceValid(hero)) {
            InputManager.Instance?.ClearInputSource(hero.PlayerIndex);
            Sources.Remove(hero.GetInstanceId());
            hero.GetParent()?.RemoveChild(hero);
            hero.Free();
        }
        if (world != null && GodotObject.IsInstanceValid(world)) {
            world.GetParent()?.RemoveChild(world);
            world.Free();
        }
    }
}
