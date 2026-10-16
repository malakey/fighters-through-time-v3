using System.Collections.Generic;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Characters.Abilities;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// CM-STORY (combat and movement update 2026-10-16, decision 1.1(1), finding
/// MOV-13; the per-kit dispositions are <c>PROVISIONAL-MOVEMENT-ABILITY-GRAVITY-SCOPE</c>):
/// Story movement abilities fall under Story gravity. Before this,
/// <c>ProcessUsingMovementAbility</c> applied none, so Leonardo's ornithopter
/// boost rose through the whole three-second glide and four recoveries hovered.
/// Now gravity runs unless the ability holds its owner off it exactly where the
/// Fighter sim does (<c>FighterKitMotion.SuspendsGravity</c>) or where the kit
/// rewrites velocity every tick; Joan's Wings rise is the intended exception
/// (<c>VERIFY-WING-DIVE-LEAP</c>). Also Q7 (ruled 2026-10-16, "stop it like the
/// sim"): Lincoln's Rail Charge stops at the start of its recovery, so it travels
/// the authored 300 px. The trajectory cases run in real physics frames.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryMovementAbilityGravityTests {
    private const double Tick = 1.0 / 60.0;
    private const GameplayButtons Movement = GameplayButtons.MovementAbility;
    private const GameplayButtons None = GameplayButtons.None;

    /// <summary>
    /// The wing boost (−380 px/s) decays under gravity, so the flight peaks low
    /// (about 61 px for Leonardo), and the glide caps the descent at exactly its
    /// 40 px/s sink speed instead of carrying the boost for the full glide.
    /// </summary>
    [TestCase]
    public async Task LeonardosBoostPeaksLowAndHisGlideSinksAtExactlyItsSinkSpeed() {
        var tree = (SceneTree)Engine.GetMainLoop();
        StaticBody2D floor = CreateFloor(new Vector2(0f, 0f), 4000f);
        PlayerController hero = null;
        try {
            hero = await SpawnGroundedHero(tree, "leonardo", new Vector2(0f, -2f));
            var flight = hero.GetNode<LeonardoOrnithopterFlight>("MovementAbility");
            BufferedInputSource source = Source(hero);
            float startY = hero.GlobalPosition.Y;
            float minY = startY;

            await Frame(tree, source, Movement, None);
            AssertThat(hero.CurrentState).IsEqual(CharacterState.UsingMovementAbility);
            AssertThat(flight.IsExecuting).IsTrue();
            minY = Mathf.Min(minY, hero.GlobalPosition.Y);

            var glideDescents = new List<float>();
            int glideTicks = 0;
            for (int frame = 0; frame < 400 && flight.IsExecuting; frame++) {
                bool gliding = flight.CurrentPhase == AbilityPhase.Recovery;
                float before = hero.GlobalPosition.Y;
                await Frame(tree, source, None, frame == 0 ? Movement : None);
                minY = Mathf.Min(minY, hero.GlobalPosition.Y);
                if (!gliding) continue;
                glideTicks++;
                // The first two glide ticks turn the dying boost into the capped
                // sink; every airborne tick after that is the cap exactly.
                if (glideTicks > 2 && !hero.IsOnFloor()) glideDescents.Add(hero.GlobalPosition.Y - before);
            }

            float peak = startY - minY;
            GD.Print($"[CM-STORY] Leonardo ornithopter peak {peak:0.00} px, glide ticks {glideTicks}");
            AssertThat(peak <= 70f)
                .OverrideFailureMessage($"The ornithopter must peak at 70 px or less; it rose {peak:0.0} px.")
                .IsTrue();
            AssertThat(peak > 20f).OverrideFailureMessage($"Harness: the boost barely lifted him ({peak:0.0} px).").IsTrue();
            AssertThat(glideDescents.Count >= 30)
                .OverrideFailureMessage($"Harness: only {glideDescents.Count} capped glide ticks were measured.")
                .IsTrue();
            float sinkPerTick = LeonardoOrnithopterFlight.GlideSinkSpeed / 60f;
            foreach (float descent in glideDescents) {
                AssertThat(Mathf.Abs(descent - sinkPerTick) < 0.01f)
                    .OverrideFailureMessage($"A glide tick fell {descent:0.000} px; the sink is {sinkPerTick:0.000} px a tick.")
                    .IsTrue();
            }
        } finally {
            Release(hero, floor);
        }
    }

    /// <summary>
    /// Joan's Ascendant Wings rise is intended: gravity stays off for every
    /// phase, so the cast still climbs 36 frames at her authored 420 px/s
    /// (7 px a frame, 252 px), exactly as before.
    /// </summary>
    [TestCase]
    public async Task JoansWingsRiseIsUnchanged() {
        var tree = (SceneTree)Engine.GetMainLoop();
        StaticBody2D floor = CreateFloor(new Vector2(0f, 0f), 4000f);
        PlayerController hero = null;
        try {
            hero = await SpawnGroundedHero(tree, "joan", new Vector2(0f, -2f));
            var wings = hero.GetNode<JoanAscendantWings>("MovementAbility");
            BufferedInputSource source = Source(hero);
            int castFrames = wings.Data.StartupFrames + wings.Data.ActiveFrames + wings.Data.RecoveryFrames;
            float perFrame = ((MovementAbilityData)wings.Data).MovementSpeed / 60f;
            AssertThat(castFrames).IsEqual(36);
            AssertThat(perFrame).IsEqualApprox(7f, 0.0001f);

            float startY = hero.GlobalPosition.Y;
            await Frame(tree, source, Movement, None);
            AssertThat(wings.IsExecuting).IsTrue();
            int ticks = 1;
            for (int frame = 0; frame < 80 && wings.IsExecuting; frame++) {
                await Frame(tree, source, None, frame == 0 ? Movement : None);
                ticks++;
            }
            AssertThat(wings.IsExecuting).OverrideFailureMessage("The cast never ended.").IsFalse();
            AssertThat(ticks).IsEqual(castFrames);
            AssertThat(startY - hero.GlobalPosition.Y).IsEqualApprox(castFrames * perFrame, 0.5f);
        } finally {
            Release(hero, floor);
        }
    }

    /// <summary>
    /// The four recoveries that used to hover now fall: Tesla's blink, Tubman's
    /// leap (after its one settle frame) and Cleopatra's sand rush drop under
    /// gravity, and Mozart's glissando settles onto the staff it just placed —
    /// its 12 px body is centred 6 px below his feet, so its top is flush with
    /// them and he used to hover on it, airborne, through the whole recovery.
    /// Cast in open air.
    /// </summary>
    [TestCase]
    public async Task TeslaTubmanCleopatraAndMozartFallDuringRecovery() {
        var tree = (SceneTree)Engine.GetMainLoop();
        foreach (string heroID in new[] { "tesla", "tubman", "cleopatra", "mozart" }) {
            PlayerController hero = null;
            try {
                hero = await SpawnAirborneHero(tree, heroID, new Vector2(0f, -40000f));
                var ability = hero.GetNode<BaseSpecial>("MovementAbility");
                BufferedInputSource source = Source(hero);
                await Frame(tree, source, Movement, None);
                AssertThat(ability.IsExecuting).OverrideFailureMessage($"{heroID}: the cast did not start.").IsTrue();

                float recoveryStartY = float.NaN;
                float recoveryEndY = float.NaN;
                int recoveryTicks = 0;
                int settleTicks = 0;
                bool landedInRecovery = false;
                for (int frame = 0; frame < 120 && ability.IsExecuting; frame++) {
                    bool recovery = ability.CurrentPhase == AbilityPhase.Recovery;
                    bool settle = recovery && ability.RecoveryFramesElapsed == 0;
                    float before = hero.GlobalPosition.Y;
                    if (recovery && float.IsNaN(recoveryStartY)) recoveryStartY = before;
                    await Frame(tree, source, None, frame == 0 ? Movement : None);
                    if (!recovery) continue;
                    recoveryTicks++;
                    recoveryEndY = hero.GlobalPosition.Y;
                    landedInRecovery |= hero.IsOnFloor();
                    if (heroID == "tubman" && settle) {
                        settleTicks++;
                        AssertThat(Mathf.Abs(hero.GlobalPosition.Y - before) < 0.001f)
                            .OverrideFailureMessage("Tubman's settle frame (the sim's LeapEnd) holds still.")
                            .IsTrue();
                    }
                }
                AssertThat(recoveryTicks > 0).OverrideFailureMessage($"{heroID}: no recovery frame was seen.").IsTrue();
                float fell = recoveryEndY - recoveryStartY;
                GD.Print($"[CM-STORY] {heroID} recovery: {recoveryTicks} ticks, fell {fell:0.00} px, landed {landedInRecovery}");
                if (heroID == "mozart") {
                    AssertThat(landedInRecovery)
                        .OverrideFailureMessage("Mozart must settle onto his own staff during the recovery, not hover.")
                        .IsTrue();
                    AssertThat(fell >= -0.01f).OverrideFailureMessage($"Mozart rose {-fell:0.00} px in recovery.").IsTrue();
                } else {
                    AssertThat(fell > 0.5f)
                        .OverrideFailureMessage($"{heroID} must fall during its recovery; it moved {fell:0.00} px down.")
                        .IsTrue();
                    AssertThat(landedInRecovery).OverrideFailureMessage($"Harness: {heroID} landed on nothing.").IsFalse();
                }
                if (heroID == "tubman") AssertThat(settleTicks).IsEqual(1);
            } finally {
                Release(hero, null);
            }
        }
    }

    /// <summary>
    /// Lincoln's Rail Charge writes its horizontal speed only, so a charge that
    /// runs off a ledge falls with Story gravity (as the sim's <c>RailCharge</c>
    /// phase does) instead of flying level, and its travel along the charge is
    /// still the authored 300 px.
    /// </summary>
    [TestCase]
    public async Task LincolnsChargeFallsOnceItLeavesTheFloorAndStillTravelsItsAuthoredDistance() {
        var tree = (SceneTree)Engine.GetMainLoop();
        // A floor whose right edge is 100 px ahead of him.
        StaticBody2D floor = CreateFloor(new Vector2(-1900f, 0f), 4000f);
        PlayerController hero = null;
        try {
            hero = await SpawnGroundedHero(tree, "lincoln", new Vector2(0f, -2f));
            hero.IsFacingRight = true;
            var charge = hero.GetNode<LincolnRailCharge>("MovementAbility");
            BufferedInputSource source = Source(hero);
            float authored = ((MovementAbilityData)charge.Data).DistanceMoved;
            AssertThat(authored).IsEqualApprox(300f, 0.001f);
            Vector2 start = hero.GlobalPosition;

            await Frame(tree, source, Movement, None);
            AssertThat(charge.IsExecuting).IsTrue();
            float leftFloorAtY = float.NaN;
            float activeEndY = float.NaN;
            float lastActiveVy = 0f;
            for (int frame = 0; frame < 120 && charge.IsExecuting; frame++) {
                bool active = charge.CurrentPhase == AbilityPhase.Active;
                await Frame(tree, source, None, frame == 0 ? Movement : None);
                if (!active) continue;
                if (!hero.IsOnFloor() && float.IsNaN(leftFloorAtY)) leftFloorAtY = hero.GlobalPosition.Y;
                activeEndY = hero.GlobalPosition.Y;
                lastActiveVy = hero.Velocity.Y;
            }
            AssertThat(float.IsNaN(leftFloorAtY)).OverrideFailureMessage("Harness: the charge never left the floor.").IsFalse();
            float fall = activeEndY - leftFloorAtY;
            float travel = hero.GlobalPosition.X - start.X;
            GD.Print($"[CM-STORY] Lincoln charge off a ledge: fell {fall:0.0} px during the charge, travel {travel:0.0} px, end vy {lastActiveVy:0.0}");
            AssertThat(fall > 40f)
                .OverrideFailureMessage($"A charge off a ledge must fall; it dropped {fall:0.0} px.")
                .IsTrue();
            AssertThat(lastActiveVy > 200f)
                .OverrideFailureMessage($"Gravity must accumulate through the charge; vy {lastActiveVy:0.0} px/s.")
                .IsTrue();
            AssertThat(Mathf.Abs(travel - authored) <= 2f)
                .OverrideFailureMessage($"The charge travelled {travel:0.0} px; the authored distance is {authored} px.")
                .IsTrue();
        } finally {
            Release(hero, floor);
        }
    }

    /// <summary>
    /// The Story half of each kit-phase movement ability suspends gravity in
    /// exactly the phases the Fighter sim's <c>FighterKitMotion.SuspendsGravity</c>
    /// does, phase by phase, for the five abilities whose Story path changed:
    /// Tesla, Tubman, Cleopatra, Mozart and Lincoln. A Story phase the sim has no
    /// counterpart for (it starts these moves on the press tick) is checked
    /// against the plan's disposition instead.
    /// </summary>
    [TestCase]
    public void SuspendsOwnerGravityNowAgreesWithTheSimPhaseByPhase() {
        // (hero, Story phase, recovery frame or -1 for "any", sim state, Story-only expectation)
        var expectations = new (string Hero, AbilityPhase Phase, int RecoveryFrame, int SimState, bool? StoryOnly)[] {
            ("tesla", AbilityPhase.Startup, -1, FighterKitMotion.BlinkStartup, null),
            ("tesla", AbilityPhase.Active, -1, FighterKitMotion.BlinkTravel, null),
            ("tesla", AbilityPhase.Recovery, -1, FighterKitMotion.BlinkRecovery, null),
            // The sim leap starts travelling on the press; Story holds one frame.
            ("tubman", AbilityPhase.Startup, -1, 0, true),
            ("tubman", AbilityPhase.Active, -1, FighterKitMotion.LeapTravel, null),
            ("tubman", AbilityPhase.Recovery, 0, FighterKitMotion.LeapEnd, null),
            ("tubman", AbilityPhase.Recovery, 1, 0, null),
            ("cleopatra", AbilityPhase.Startup, -1, 0, null),
            ("cleopatra", AbilityPhase.Active, -1, FighterKitMotion.SandRush, null),
            ("cleopatra", AbilityPhase.Recovery, -1, 0, null),
            // Story keeps the glissando's opening beat; the sim has none.
            ("mozart", AbilityPhase.Startup, -1, 0, true),
            ("mozart", AbilityPhase.Active, -1, FighterKitMotion.Glissando, null),
            ("mozart", AbilityPhase.Recovery, -1, 0, null),
            ("lincoln", AbilityPhase.Startup, -1, 0, null),
            ("lincoln", AbilityPhase.Active, -1, FighterKitMotion.RailCharge, null),
            ("lincoln", AbilityPhase.Recovery, -1, 0, null),
        };

        var observed = new Dictionary<(string, AbilityPhase, int), bool>();
        foreach (string heroID in new[] { "tesla", "tubman", "cleopatra", "mozart", "lincoln" }) {
            PlayerController hero = CharacterFactory.CreateCharacter(heroID, 0, applyStoryProgression: false);
            var host = new Node { Name = "GravityAgreementHost" };
            ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
            host.AddChild(hero);
            try {
                hero.GlobalPosition = new Vector2(0f, -40000f);
                hero.TransitionTo(CharacterState.Airborne);
                var ability = hero.GetNode<BaseSpecial>("MovementAbility");
                var source = new BufferedInputSource();
                source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, Movement));
                InputManager.Instance.SetInputSource(hero.PlayerIndex, source);
                hero._PhysicsProcess(Tick);
                source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, None, Movement));
                AssertThat(ability.IsExecuting).OverrideFailureMessage($"{heroID}: the cast did not start.").IsTrue();
                for (int frame = 0; frame < 200 && ability.IsExecuting
                     && ability.CurrentPhase != AbilityPhase.Cleanup; frame++) {
                    AbilityPhase phase = ability.CurrentPhase;
                    int recovery = phase == AbilityPhase.Recovery ? ability.RecoveryFramesElapsed : -1;
                    bool suspends = ability.SuspendsOwnerGravityNow;
                    // Recovery is keyed per frame (Tubman's settle frame differs);
                    // every other phase is one answer for all of its frames.
                    var key = (heroID, phase, recovery);
                    if (observed.TryGetValue(key, out bool seen) && seen != suspends) {
                        AssertThat(suspends)
                            .OverrideFailureMessage($"{heroID} {phase}: the answer changed between frames of one phase.")
                            .IsEqual(seen);
                    }
                    observed[key] = suspends;
                    ability._PhysicsProcess(Tick);
                }
            } finally {
                InputManager.Instance?.ClearInputSource(hero.PlayerIndex);
                foreach (Node2D node in new List<Node2D>(hero.ActivePersistentObjects)) {
                    if (node is SonataPlatformNode platform && GodotObject.IsInstanceValid(platform)) platform.ReturnToPool();
                }
                host.GetParent()?.RemoveChild(host);
                host.Free();
            }
        }

        var issues = new List<string>();
        foreach (var expected in expectations) {
            var matches = new List<bool>();
            foreach (var entry in observed) {
                (string hero, AbilityPhase phase, int frame) = entry.Key;
                if (hero != expected.Hero || phase != expected.Phase) continue;
                if (expected.RecoveryFrame == 0 && frame != 0) continue;
                if (expected.RecoveryFrame > 0 && frame < expected.RecoveryFrame) continue;
                matches.Add(entry.Value);
            }
            if (matches.Count == 0) {
                issues.Add($"{expected.Hero} {expected.Phase}: never observed");
                continue;
            }
            bool want = expected.StoryOnly
                ?? FighterKitMotion.SuspendsGravity(new FighterRuntimeComponent { UniversalMovementState = expected.SimState });
            foreach (bool storySuspends in matches) {
                if (storySuspends != want) {
                    issues.Add($"{expected.Hero} {expected.Phase}" +
                        (expected.RecoveryFrame >= 0 ? $" (recovery frame {expected.RecoveryFrame})" : "") +
                        $": Story suspends={storySuspends}, expected {want}");
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    /// <summary>
    /// Q7 (ruled 2026-10-16, "stop it like the sim"): the Rail Charge's
    /// horizontal velocity is zero from its first recovery frame, so it no longer
    /// slides about 180 px past the authored distance, and the whole cast travels
    /// 300 px ± 2.
    /// </summary>
    [TestCase]
    public async Task LincolnsChargeStopsAtTheStartOfItsRecovery() {
        var tree = (SceneTree)Engine.GetMainLoop();
        StaticBody2D floor = CreateFloor(new Vector2(0f, 0f), 4000f);
        PlayerController hero = null;
        try {
            hero = await SpawnGroundedHero(tree, "lincoln", new Vector2(-600f, -2f));
            hero.IsFacingRight = true;
            var charge = hero.GetNode<LincolnRailCharge>("MovementAbility");
            BufferedInputSource source = Source(hero);
            float startX = hero.GlobalPosition.X;

            await Frame(tree, source, Movement, None);
            AssertThat(charge.IsExecuting).IsTrue();
            bool sawRecoveryStart = false;
            float recoveryStartX = 0f;
            for (int frame = 0; frame < 120 && charge.IsExecuting; frame++) {
                await Frame(tree, source, None, frame == 0 ? Movement : None);
                if (!sawRecoveryStart && charge.CurrentPhase == AbilityPhase.Recovery) {
                    sawRecoveryStart = true;
                    recoveryStartX = hero.GlobalPosition.X;
                    AssertThat(hero.Velocity.X)
                        .OverrideFailureMessage("The charge must stop as its recovery starts.")
                        .IsEqualApprox(0f, 0.0001f);
                }
            }
            AssertThat(sawRecoveryStart).OverrideFailureMessage("Harness: no recovery was seen.").IsTrue();
            AssertThat(charge.IsExecuting).IsFalse();
            float travel = hero.GlobalPosition.X - startX;
            GD.Print($"[CM-STORY] Lincoln charge on flat ground: travel {travel:0.0} px (recovery slid {hero.GlobalPosition.X - recoveryStartX:0.0} px)");
            AssertThat(Mathf.Abs(travel - 300f) <= 2f)
                .OverrideFailureMessage($"The charge travelled {travel:0.0} px, not the authored 300.")
                .IsTrue();
            AssertThat(Mathf.Abs(hero.GlobalPosition.X - recoveryStartX) < 0.01f)
                .OverrideFailureMessage("Nothing slides through the recovery.")
                .IsTrue();
        } finally {
            Release(hero, floor);
        }
    }

    // === Harness ===

    /// <summary>The scripted input source each spawned hero reads (one hero at a time).</summary>
    private static readonly Dictionary<ulong, BufferedInputSource> Sources = new();

    private static BufferedInputSource Source(PlayerController hero) => Sources[hero.GetInstanceId()];

    private static BufferedInputSource Install(PlayerController hero) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, None));
        InputManager.Instance.SetInputSource(hero.PlayerIndex, source);
        Sources[hero.GetInstanceId()] = source;
        return source;
    }

    /// <summary>
    /// Sets the input the current physics frame consumes and waits for the next
    /// frame to begin. <c>ToSignal(PhysicsFrame)</c> resumes inline when the
    /// signal fires, at the start of a frame and before its nodes process, so on
    /// return the state reflects every frame this call waited through.
    /// </summary>
    private static async Task Frame(SceneTree tree, BufferedInputSource source, GameplayButtons held, GameplayButtons previous) {
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, held, previous));
        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
    }

    private static async Task<PlayerController> SpawnGroundedHero(SceneTree tree, string heroID, Vector2 position) {
        PlayerController hero = CharacterFactory.CreateCharacter(heroID, 0, applyStoryProgression: false);
        tree.Root.AddChild(hero);
        try {
            hero.GlobalPosition = position;
            hero.IsFacingRight = true;
            Install(hero);
            for (int frame = 0; frame < 30 && !(hero.IsOnFloor() && hero.CurrentState == CharacterState.Idle); frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            }
            for (int frame = 0; frame < 4; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(hero.IsOnFloor() && hero.CurrentState == CharacterState.Idle)
                .OverrideFailureMessage($"Harness: {heroID} must stand idle on the floor.")
                .IsTrue();
            return hero;
        } catch {
            Release(hero, null);
            throw;
        }
    }

    private static async Task<PlayerController> SpawnAirborneHero(SceneTree tree, string heroID, Vector2 position) {
        PlayerController hero = CharacterFactory.CreateCharacter(heroID, 0, applyStoryProgression: false);
        tree.Root.AddChild(hero);
        try {
            hero.GlobalPosition = position;
            hero.IsFacingRight = true;
            hero.TransitionTo(CharacterState.Airborne);
            Install(hero);
            // The awaiter resumes at the start of a physics frame, before the
            // frame's nodes process; one wait puts every later Frame() call there,
            // so the input it sets is the input that frame consumes.
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            return hero;
        } catch {
            Release(hero, null);
            throw;
        }
    }

    private static StaticBody2D CreateFloor(Vector2 topCentre, float width) {
        var floor = new StaticBody2D {
            Name = "MovementAbilityGravityFloor",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(width, 40f) },
            Position = topCentre + new Vector2(0f, 20f)
        });
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(floor);
        return floor;
    }

    /// <summary>Frees whatever a case created; null-safe, and hands deployed staffs back to their pool.</summary>
    private static void Release(PlayerController hero, StaticBody2D floor) {
        if (hero != null && GodotObject.IsInstanceValid(hero)) {
            InputManager.Instance?.ClearInputSource(hero.PlayerIndex);
            Sources.Remove(hero.GetInstanceId());
            foreach (Node2D node in new List<Node2D>(hero.ActivePersistentObjects)) {
                if (node is SonataPlatformNode platform && GodotObject.IsInstanceValid(platform)) platform.ReturnToPool();
            }
            hero.GetParent()?.RemoveChild(hero);
            hero.Free();
        }
        if (floor != null && GodotObject.IsInstanceValid(floor)) floor.Free();
    }
}
