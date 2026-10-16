using System.Collections.Generic;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Characters.Abilities;
using FTT.Combat;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// CM-FIXA-STORY (combat and movement update 2026-10-16, Phase A fix round): a
/// Story kit motion travels its authored distance on the ticks the Fighter sim
/// travels it — every active tick, the first one included.
///
/// <para>The controller moves (<c>MoveAndSlide</c>) before its ability nodes
/// process, and the cast's phase advances inside the kit's own tick. A kit that
/// wrote its travel velocity only while its phase already read Active therefore
/// moved its first Active tick at the startup's velocity (zero), and
/// <c>OnRecovery</c> zeroed its last write: Divine Piercing lunged 165 of 180 px,
/// Tesla's blink 165 of 180, Tubman's leap 226.7 of 240, Cleopatra's rush 224 of
/// 240 and Mozart's glissando 170 of 180. Each now drives its travel from
/// <c>OnActive</c>, as Lincoln's Rail Charge already did. Lincoln's Rail Charge
/// also no longer carries his run speed through its startup (the sim's charge
/// overwrites X from the press), so a running charge travels the authored 300 px
/// too.</para>
///
/// <para>Every case runs in real physics frames through the controller. The
/// measurement reads the cast's phase at the start of each frame (the phase the
/// controller's move sees) and the displacement that frame's move made.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryKitMotionTravelTests {
    private const GameplayButtons None = GameplayButtons.None;
    private const float Tolerance = 1f;

    [TestCase]
    public async Task DivinePiercingLungesThreeUnitsOnEveryActiveTickFromAStandstillAndFromARun() {
        var tree = (SceneTree)Engine.GetMainLoop();
        StaticBody2D floor = CreateFloor(new Vector2(0f, 0f), 4000f);
        PlayerController hero = null;
        try {
            hero = await SpawnGroundedHero(tree, "joan", new Vector2(-1200f, -2f));
            var piercing = hero.GetNode<JoanDivinePiercing>("Special2");
            float authored = (float)KitReachRules.DivinePiercingLungeUnits * KitMotionRules.StoryPixelsPerUnit;
            AssertThat(authored).IsEqualApprox(180f, 0.001f);
            float perTick = authored / piercing.Data.ActiveFrames;

            Travel still = await Cast(tree, hero, piercing, GameplayButtons.Special2, 0f, 0f);
            Report("joan divine piercing (standstill)", still);
            AssertActiveTravel("Divine Piercing (standstill)", still, piercing.Data.ActiveFrames, perTick, Vector2.Right);
            AssertThat(Mathf.Abs(still.Total.X - authored) <= Tolerance)
                .OverrideFailureMessage($"A standing Divine Piercing must lunge {authored} px; it moved {still.Total.X:0.00}.")
                .IsTrue();
            AssertThat(still.Startup.Length() < 0.01f)
                .OverrideFailureMessage($"A standing Joan must not move in the startup; she moved {still.Startup.X:0.00} px.")
                .IsTrue();
            AssertThat(still.Recovery.Length() < 0.01f)
                .OverrideFailureMessage($"The lunge must stop at its recovery; she slid {still.Recovery.X:0.00} px.")
                .IsTrue();

            // From a full run: the Q1 brake stops her in the startup, and the
            // lunge itself is still the authored distance on every active tick.
            await Settle(tree, hero, piercing);
            await RunRight(tree, hero, 24);
            Travel running = await Cast(tree, hero, piercing, GameplayButtons.Special2, 1f, 0f);
            Report("joan divine piercing (running)", running);
            AssertActiveTravel("Divine Piercing (running)", running, piercing.Data.ActiveFrames, perTick, Vector2.Right);
        } finally {
            Release(hero, floor);
        }
    }

    /// <summary>
    /// The Grand Crusade (a Story Ultimate, no cross-mode distance) charges on
    /// every one of its active ticks too, for consistency with the lunge.
    /// </summary>
    [TestCase]
    public async Task TheGrandCrusadeChargesOnEveryActiveTick() {
        var tree = (SceneTree)Engine.GetMainLoop();
        StaticBody2D floor = CreateFloor(new Vector2(0f, 0f), 4000f);
        PlayerController hero = null;
        try {
            hero = await SpawnGroundedHero(tree, "joan", new Vector2(-1500f, -2f));
            var crusade = hero.GetNode<JoanGrandCrusade>("Ultimate");
            hero.SetUltimateMeter(InfluenceMeterRules.MaxValue);
            float perTick = crusade.Data.ProjectileSpeed / 60f;
            AssertThat(perTick).IsEqualApprox(15f, 0.001f);

            Travel charge = await Cast(tree, hero, crusade, GameplayButtons.Ultimate, 0f, 0f);
            Report("joan grand crusade", charge);
            AssertActiveTravel("The Grand Crusade", charge, crusade.Data.ActiveFrames, perTick, Vector2.Right);
            AssertThat(charge.Startup.Length() < 0.01f).IsTrue();
        } finally {
            Release(hero, floor);
        }
    }

    [TestCase]
    public async Task TeslasBlinkTravelsItsAuthoredDistanceOnEveryTranslationTick() {
        var tree = (SceneTree)Engine.GetMainLoop();
        StaticBody2D floor = CreateFloor(new Vector2(0f, 0f), 4000f);
        PlayerController hero = null;
        try {
            hero = await SpawnGroundedHero(tree, "tesla", new Vector2(-1200f, -2f));
            var blink = hero.GetNode<TeslaLightningBlink>("MovementAbility");
            float authored = ((MovementAbilityData)blink.Data).DistanceMoved;
            AssertThat(authored).IsEqualApprox(180f, 0.001f);

            Travel cast = await Cast(tree, hero, blink, GameplayButtons.MovementAbility, 0f, 0f);
            Report("tesla lightning blink", cast);
            AssertActiveTravel("Lightning Blink", cast, blink.Data.ActiveFrames, authored / blink.Data.ActiveFrames, Vector2.Right);
            AssertWholeCast("Lightning Blink", cast, authored);
        } finally {
            Release(hero, floor);
        }
    }

    /// <summary>
    /// The leap travels 4 units, and because it now ends where the sim's ends,
    /// a ledge 55 px beyond that end — outside the ordinary overlap capture and
    /// inside the 0.5-unit extended snap (14 + 18 + 30 = 62 px) — is caught. It
    /// used to stop 13.3 px short and miss it (68.3 px &gt; 62).
    /// </summary>
    [TestCase]
    public async Task TubmansLeapTravelsFourUnitsAndCatchesALedgeAtTheEndOfItsExtendedSnap() {
        var tree = (SceneTree)Engine.GetMainLoop();
        StaticBody2D floor = CreateFloor(new Vector2(0f, 0f), 4000f);
        PlayerController hero = null;
        try {
            hero = await SpawnGroundedHero(tree, "tubman", new Vector2(-1200f, -2f));
            var leap = hero.GetNode<TubmanNorthStarLeap>("MovementAbility");
            float authored = leap.LeapDistancePixels;
            AssertThat(authored).IsEqualApprox(240f, 0.001f);

            Travel cast = await Cast(tree, hero, leap, GameplayButtons.MovementAbility, 0f, 0f);
            Report("tubman north star leap", cast);
            AssertActiveTravel("North Star Leap", cast, leap.Data.ActiveFrames, authored / leap.Data.ActiveFrames, Vector2.Right);
            AssertWholeCast("North Star Leap", cast, authored);
        } finally {
            Release(hero, floor);
        }

        LedgeGrabPoint ledge = null;
        hero = null;
        try {
            hero = await SpawnAirborneHero(tree, "tubman", new Vector2(0f, -40000f));
            var leap = hero.GetNode<TubmanNorthStarLeap>("MovementAbility");
            Vector2 start = hero.GlobalPosition;
            ledge = GD.Load<PackedScene>("res://scenes/templates/LedgeGrabPointTemplate.tscn").Instantiate<LedgeGrabPoint>();
            // Level with the body point FindLedgeWithinSnapReach measures from.
            ledge.Position = start + new Vector2(leap.LeapDistancePixels + 55f, -30f);
            tree.Root.AddChild(ledge);

            BufferedInputSource source = Source(hero);
            await Frame(tree, source, 0f, 0f, GameplayButtons.MovementAbility, None);
            AssertThat(leap.IsExecuting).IsTrue();
            for (int frame = 0; frame < 120 && leap.IsExecuting; frame++) {
                await Frame(tree, source, 0f, 0f, None, frame == 0 ? GameplayButtons.MovementAbility : None);
            }
            AssertThat(leap.IsExecuting).IsFalse();
            AssertThat(hero.CurrentState)
                .OverrideFailureMessage("A ledge 55 px past the leap's authored end must be caught by the extended snap.")
                .IsEqual(CharacterState.LedgeHanging);
        } finally {
            if (ledge != null && GodotObject.IsInstanceValid(ledge)) {
                ledge.GetParent()?.RemoveChild(ledge);
                ledge.Free();
            }
            Release(hero, null);
        }
    }

    /// <summary>
    /// The sand rush is 16 px on each of its 15 active ticks from a standstill,
    /// and from a run too: her startup keeps her entry velocity (CM-STORY's
    /// disposition), but the rush no longer starts with that stale velocity.
    /// </summary>
    [TestCase]
    public async Task CleopatrasRushTravelsItsAuthoredDistanceFromAStandstillAndFromARun() {
        var tree = (SceneTree)Engine.GetMainLoop();
        StaticBody2D floor = CreateFloor(new Vector2(0f, 0f), 4000f);
        PlayerController hero = null;
        try {
            hero = await SpawnGroundedHero(tree, "cleopatra", new Vector2(-1500f, -2f));
            var mirage = hero.GetNode<CleopatraDesertMirage>("MovementAbility");
            var data = (MovementAbilityData)mirage.Data;
            float authored = data.DistanceMoved;
            int rushTicks = Mathf.RoundToInt(data.MovementDuration * 60f);
            AssertThat(authored).IsEqualApprox(240f, 0.001f);
            AssertThat(rushTicks).IsEqual(15);

            Travel still = await Cast(tree, hero, mirage, GameplayButtons.MovementAbility, 0f, 0f);
            Report("cleopatra desert mirage (standstill)", still);
            AssertActiveTravel("Desert Mirage (standstill)", still, rushTicks, authored / rushTicks, Vector2.Right);
            AssertWholeCast("Desert Mirage (standstill)", still, authored);

            await Settle(tree, hero, mirage);
            await RunRight(tree, hero, 24);
            Travel running = await Cast(tree, hero, mirage, GameplayButtons.MovementAbility, 1f, 0f);
            Report("cleopatra desert mirage (running)", running);
            AssertActiveTravel("Desert Mirage (running)", running, rushTicks, authored / rushTicks, Vector2.Right);
        } finally {
            Release(hero, floor);
        }
    }

    [TestCase]
    public async Task MozartsGlissandoTravelsItsAuthoredDistanceSidewaysAndStraightUp() {
        var tree = (SceneTree)Engine.GetMainLoop();
        StaticBody2D floor = CreateFloor(new Vector2(0f, 0f), 4000f);
        PlayerController hero = null;
        try {
            hero = await SpawnGroundedHero(tree, "mozart", new Vector2(-1200f, -2f));
            var drift = hero.GetNode<MozartSonataDrift>("MovementAbility");
            var data = (MovementAbilityData)drift.Data;
            float authored = data.DistanceMoved;
            int travelTicks = Mathf.RoundToInt(data.MovementDuration * 60f);
            AssertThat(authored).IsEqualApprox(180f, 0.001f);

            // Held right: a sideways glissando along the floor.
            Travel sideways = await Cast(tree, hero, drift, GameplayButtons.MovementAbility, 1f, 0f, holdAfterPress: false);
            Report("mozart sonata drift (sideways)", sideways);
            AssertActiveTravel("Sonata Drift (sideways)", sideways, travelTicks, authored / travelTicks, Vector2.Right);
            AssertThat(Mathf.Abs(sideways.Total.X - authored) <= Tolerance)
                .OverrideFailureMessage($"The sideways glissando must travel {authored} px; it moved {sideways.Total.X:0.00}.")
                .IsTrue();
        } finally {
            Release(hero, floor);
        }

        hero = null;
        try {
            // Neutral in open air: the glissando rises straight up.
            hero = await SpawnAirborneHero(tree, "mozart", new Vector2(0f, -40000f));
            var drift = hero.GetNode<MozartSonataDrift>("MovementAbility");
            var data = (MovementAbilityData)drift.Data;
            int travelTicks = Mathf.RoundToInt(data.MovementDuration * 60f);
            Travel rise = await Cast(tree, hero, drift, GameplayButtons.MovementAbility, 0f, 0f);
            Report("mozart sonata drift (rise)", rise);
            AssertActiveTravel("Sonata Drift (rise)", rise, travelTicks, data.DistanceMoved / travelTicks, Vector2.Up);
        } finally {
            Release(hero, null);
        }
    }

    /// <summary>
    /// The sim's Rail Charge overwrites X from the press, so a running Lincoln
    /// charges exactly the authored 300 px. Story's 12-frame startup now holds
    /// him instead of sliding at his run speed (about 45 px).
    /// </summary>
    [TestCase]
    public async Task LincolnsRunningRailChargeTravelsThreeHundredFromThePress() {
        var tree = (SceneTree)Engine.GetMainLoop();
        StaticBody2D floor = CreateFloor(new Vector2(0f, 0f), 4000f);
        PlayerController hero = null;
        try {
            hero = await SpawnGroundedHero(tree, "lincoln", new Vector2(-1500f, -2f));
            var charge = hero.GetNode<LincolnRailCharge>("MovementAbility");
            float authored = ((MovementAbilityData)charge.Data).DistanceMoved;
            AssertThat(authored).IsEqualApprox(300f, 0.001f);

            await RunRight(tree, hero, 24);
            AssertThat(hero.CurrentState).IsEqual(CharacterState.Running);
            AssertThat(hero.Velocity.X > 200f)
                .OverrideFailureMessage($"Harness: Lincoln must be running at speed; vx {hero.Velocity.X:0.0}.")
                .IsTrue();
            Travel running = await Cast(tree, hero, charge, GameplayButtons.MovementAbility, 1f, 0f);
            Report("lincoln rail charge (running)", running);
            AssertThat(running.Startup.Length() < 0.01f)
                .OverrideFailureMessage($"The startup must hold him; he slid {running.Startup.X:0.00} px.")
                .IsTrue();
            AssertWholeCast("Rail Charge (running)", running, authored);
        } finally {
            Release(hero, floor);
        }
    }

    // === Measurement ===

    private sealed class Travel {
        /// <summary>The press tick plus every Startup tick.</summary>
        public Vector2 Startup;
        public Vector2 Active;
        public Vector2 Recovery;
        public Vector2 Total;
        public int ActiveTicks;
        public readonly List<Vector2> ActiveSteps = new();
    }

    /// <summary>
    /// Presses <paramref name="button"/> with the stick at (<paramref name="h"/>,
    /// <paramref name="v"/>) and plays the cast out in real frames. The stick is
    /// released after the press unless <paramref name="holdAfterPress"/>.
    /// </summary>
    private static async Task<Travel> Cast(
        SceneTree tree, PlayerController hero, BaseSpecial ability, GameplayButtons button,
        float h, float v, bool holdAfterPress = false) {
        BufferedInputSource source = Source(hero);
        var travel = new Travel();
        Vector2 start = hero.GlobalPosition;
        await Frame(tree, source, h, v, button, None);
        AssertThat(ability.IsExecuting)
            .OverrideFailureMessage($"{ability.Name}: the cast did not start (state {hero.CurrentState}).")
            .IsTrue();
        travel.Startup += hero.GlobalPosition - start;
        for (int frame = 0; frame < 240 && ability.IsExecuting; frame++) {
            AbilityPhase phase = ability.CurrentPhase;
            Vector2 before = hero.GlobalPosition;
            float stickH = holdAfterPress ? h : 0f;
            float stickV = holdAfterPress ? v : 0f;
            await Frame(tree, source, stickH, stickV, None, frame == 0 ? button : None);
            Vector2 step = hero.GlobalPosition - before;
            switch (phase) {
                case AbilityPhase.Startup:
                    travel.Startup += step;
                    break;
                case AbilityPhase.Active:
                    travel.Active += step;
                    travel.ActiveTicks++;
                    travel.ActiveSteps.Add(step);
                    break;
                default:
                    travel.Recovery += step;
                    break;
            }
        }
        AssertThat(ability.IsExecuting).OverrideFailureMessage($"{ability.Name}: the cast never ended.").IsFalse();
        travel.Total = hero.GlobalPosition - start;
        return travel;
    }

    /// <summary>
    /// Every active tick moves one authored step along <paramref name="axis"/>
    /// (the first one included), so the active phase covers exactly
    /// <paramref name="ticks"/> × <paramref name="perTick"/>.
    /// </summary>
    private static void AssertActiveTravel(string label, Travel travel, int ticks, float perTick, Vector2 axis) {
        AssertThat(travel.ActiveTicks)
            .OverrideFailureMessage($"{label}: {travel.ActiveTicks} active ticks, authored {ticks}.")
            .IsEqual(ticks);
        var issues = new List<string>();
        for (int index = 0; index < travel.ActiveSteps.Count; index++) {
            float along = travel.ActiveSteps[index].Dot(axis);
            if (Mathf.Abs(along - perTick) > 0.05f) issues.Add($"active tick {index + 1} moved {along:0.00} px (step {perTick:0.00})");
        }
        if (issues.Count > 0) AssertThat($"{label}: " + string.Join(" | ", issues)).IsEqual("");
        float expected = ticks * perTick;
        float covered = travel.Active.Dot(axis);
        AssertThat(Mathf.Abs(covered - expected) <= Tolerance)
            .OverrideFailureMessage($"{label}: the active phase covered {covered:0.00} px, authored {expected:0.00}.")
            .IsTrue();
    }

    /// <summary>The whole cast, from the press, covers exactly the authored distance along X.</summary>
    private static void AssertWholeCast(string label, Travel travel, float authored) {
        AssertThat(Mathf.Abs(travel.Total.X - authored) <= Tolerance)
            .OverrideFailureMessage($"{label}: the cast travelled {travel.Total.X:0.00} px from the press, authored {authored}.")
            .IsTrue();
    }

    private static void Report(string label, Travel travel) {
        GD.Print($"[CM-FIXA-STORY] {label}: startup {travel.Startup.X:0.00}/{travel.Startup.Y:0.00}, " +
            $"active {travel.Active.X:0.00}/{travel.Active.Y:0.00} over {travel.ActiveTicks} ticks, " +
            $"recovery {travel.Recovery.X:0.00}/{travel.Recovery.Y:0.00}, total {travel.Total.X:0.00}/{travel.Total.Y:0.00}");
    }

    /// <summary>Holds right for <paramref name="frames"/> frames so the hero reaches full run speed.</summary>
    private static async Task RunRight(SceneTree tree, PlayerController hero, int frames) {
        BufferedInputSource source = Source(hero);
        for (int frame = 0; frame < frames; frame++) await Frame(tree, source, 1f, 0f, None, None);
        AssertThat(hero.CurrentState)
            .OverrideFailureMessage($"Harness: the hero must be running, not {hero.CurrentState}.")
            .IsEqual(CharacterState.Running);
    }

    /// <summary>
    /// Readies the next cast from a clean, standing hero: the cooldowns are
    /// cleared (the measurement is about travel, not recharge) and the hero is
    /// stepped until it stands idle with the ability lockout run out.
    /// </summary>
    private static async Task Settle(SceneTree tree, PlayerController hero, BaseSpecial ability) {
        BufferedInputSource source = Source(hero);
        for (int frame = 0; frame < 120; frame++) {
            hero.MovementAbilityCooldownTimer = 0f;
            hero.SpecialTwoCooldownTimer = 0f;
            bool ready = hero.IsOnFloor() && hero.CurrentState == CharacterState.Idle && !ability.IsExecuting
                && hero.AbilityLockoutFramesRemaining <= 0 && hero.Velocity.X == 0f;
            if (ready) break;
            await Frame(tree, source, 0f, 0f, None, None);
        }
        AssertThat(hero.IsOnFloor() && hero.CurrentState == CharacterState.Idle)
            .OverrideFailureMessage($"Harness: the hero did not settle (state {hero.CurrentState}).")
            .IsTrue();
    }

    // === Harness (the StoryMovementAbilityGravityTests pattern) ===

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
    /// frame to begin (the awaiter resumes at the start of a frame, before its
    /// nodes process).
    /// </summary>
    private static async Task Frame(
        SceneTree tree, BufferedInputSource source, float h, float v, GameplayButtons held, GameplayButtons previous) {
        source.SetNextFrame(PlayerInputFrame.Create(0, h, v, held, previous));
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
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            return hero;
        } catch {
            Release(hero, null);
            throw;
        }
    }

    private static StaticBody2D CreateFloor(Vector2 topCentre, float width) {
        var floor = new StaticBody2D {
            Name = "KitMotionTravelFloor",
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
