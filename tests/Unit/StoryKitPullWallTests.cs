using System;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Characters.Abilities;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Review fix FIX-E (2026-10-13) for the gameplay update's wall-aware kit pulls
/// (plan §2.3, GU-E): Cleopatra's Sandstorm Vortex, Einstein's Cosmological
/// Constant singularity and Tesla's Lorentz Attraction move a caught body
/// through its own collision (<c>BaseSpecial.PullThroughCollision</c>), so a
/// closed encounter-lock barrier between the body and the pull point stops it
/// on its own side. In the open each pull moves its target toward the pull
/// point, and a target standing on footing above the pull point is still drawn
/// in horizontally — one diagonal <c>MoveAndCollide</c> used to stop dead on
/// that footing. The casters and the target never tick (their AI, gravity and
/// motion are off), so only the pull moves the target; the casts are driven
/// frame by frame through the abilities' own <c>_PhysicsProcess</c>. Every
/// arena is built far from the origin and freed in <c>finally</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryKitPullWallTests {
    private const float Step = 1f / 60f;
    private static readonly Vector2 Origin = new(-91000f, 0f);
    private const float FloorTopY = 900f;

    private static SceneTree Tree => (SceneTree)Engine.GetMainLoop();

    [TestCase(Timeout = 60000)]
    public async Task TheSandstormVortexDrawsATargetInButNeverThroughABarrier() {
        // Open: three units per second toward the vortex 300 px ahead.
        using (var arena = new PullArena("PullVortexOpen")) {
            EnemyController target = arena.AddTarget(380f, FloorTopY);
            CleopatraSandstormVortex vortex = await CastVortex(arena);
            AssertFloat(vortex.ActiveVortexCenter.X).IsEqualApprox(Origin.X + 300f, 1f);
            float start = target.GlobalPosition.X;
            for (int tick = 0; tick < 20; tick++) vortex._PhysicsProcess(Step);
            AssertThat(start - target.GlobalPosition.X)
                .OverrideFailureMessage($"The vortex moved its target {start - target.GlobalPosition.X:0.0} px in 20 ticks.")
                .IsGreater(30f);
        }
        // A closed barrier between the target and the vortex holds it on its side.
        using (var arena = new PullArena("PullVortexBarrier")) {
            EncounterLockBarrier barrier = arena.AddBarrier(340f);
            EnemyController target = arena.AddTarget(390f, FloorTopY);
            CleopatraSandstormVortex vortex = await CastVortex(arena);
            float start = target.GlobalPosition.X;
            for (int tick = 0; tick < 40; tick++) vortex._PhysicsProcess(Step);
            AssertBlockedAt(target, barrier, start);
        }
    }

    [TestCase(Timeout = 60000)]
    public async Task TheSingularityDrawsATargetOnAStepAboveItButNeverThroughABarrier() {
        // Open, on the singularity's own floor.
        using (var arena = new PullArena("PullSingularityOpen")) {
            EnemyController target = arena.AddTarget(300f, FloorTopY);
            EinsteinUltimate ultimate = await CastSingularity(arena);
            float start = target.GlobalPosition.X;
            for (int tick = 0; tick < 10; tick++) ultimate._PhysicsProcess(Step);
            AssertThat(start - target.GlobalPosition.X).IsGreater(30f);
        }
        // On a step 100 px above Einstein's floor: the pull points down into the
        // target's own footing, which used to cancel it entirely.
        using (var arena = new PullArena("PullSingularityStep")) {
            arena.AddStep(250f, 450f, 100f);
            EnemyController target = arena.AddTarget(320f, FloorTopY - 100f);
            EinsteinUltimate ultimate = await CastSingularity(arena);
            float start = target.GlobalPosition.X;
            for (int tick = 0; tick < 10; tick++) ultimate._PhysicsProcess(Step);
            AssertThat(start - target.GlobalPosition.X)
                .OverrideFailureMessage($"A target on a step above the singularity moved {start - target.GlobalPosition.X:0.0} px in 10 ticks.")
                .IsGreater(30f);
        }
        using (var arena = new PullArena("PullSingularityBarrier")) {
            EncounterLockBarrier barrier = arena.AddBarrier(220f);
            EnemyController target = arena.AddTarget(300f, FloorTopY);
            EinsteinUltimate ultimate = await CastSingularity(arena);
            float start = target.GlobalPosition.X;
            for (int tick = 0; tick < 30; tick++) ultimate._PhysicsProcess(Step);
            AssertBlockedAt(target, barrier, start);
        }
    }

    [TestCase(Timeout = 60000)]
    public async Task LorentzAttractionDrawsATargetOnAStepAboveTeslaButNeverThroughABarrier() {
        // Open: the target ends 70 px from Tesla.
        using (var arena = new PullArena("PullLorentzOpen")) {
            EnemyController target = arena.AddTarget(130f, FloorTopY);
            await PulseWithAttraction(arena);
            AssertFloat(target.GlobalPosition.X - Origin.X).IsEqualApprox(70f, 2f);
        }
        // On a step 60 px above Tesla's floor: the pull points down into it.
        using (var arena = new PullArena("PullLorentzStep")) {
            arena.AddStep(90f, 250f, 60f);
            EnemyController target = arena.AddTarget(120f, FloorTopY - 60f);
            float start = target.GlobalPosition.X;
            await PulseWithAttraction(arena);
            AssertThat(start - target.GlobalPosition.X)
                .OverrideFailureMessage($"A target on a step above Tesla moved {start - target.GlobalPosition.X:0.0} px.")
                .IsGreater(40f);
        }
        using (var arena = new PullArena("PullLorentzBarrier")) {
            EncounterLockBarrier barrier = arena.AddBarrier(80f);
            EnemyController target = arena.AddTarget(135f, FloorTopY);
            float start = target.GlobalPosition.X;
            await PulseWithAttraction(arena);
            AssertBlockedAt(target, barrier, start);
        }
    }

    // === Casts ===============================================================

    private static async Task<CleopatraSandstormVortex> CastVortex(PullArena arena) {
        PlayerController cleopatra = arena.AddCaster("cleopatra");
        await arena.Settle();
        var vortex = cleopatra.GetNode<CleopatraSandstormVortex>("Special2");
        AssertThat(vortex.TryExecute()).IsTrue();
        vortex.AdvanceToActive();
        AssertThat(vortex.VortexActive).IsTrue();
        return vortex;
    }

    private static async Task<EinsteinUltimate> CastSingularity(PullArena arena) {
        PlayerController einstein = arena.AddCaster("einstein");
        await arena.Settle();
        einstein.GetNode<UltimateMeter>("UltimateMeter").SetValue(UltimateMeter.MaxValue);
        var ultimate = einstein.GetNode<EinsteinUltimate>("Ultimate");
        AssertThat(ultimate.TryExecute()).IsTrue();
        ultimate.AdvanceToActive();
        return ultimate;
    }

    private static async Task PulseWithAttraction(PullArena arena) {
        PlayerController tesla = arena.AddCaster("tesla");
        tesla.StoryAbilityPerks.Add(TeslaLorentzPulse.LorentzAttractionPerkKey);
        await arena.Settle();
        var lorentz = tesla.GetNode<TeslaLorentzPulse>("Special2");
        AssertThat(lorentz.TryExecute()).IsTrue();
        for (int frame = 0; frame < 120 && lorentz.IsExecuting; frame++) lorentz._PhysicsProcess(Step);
    }

    /// <summary>The target was pulled toward the barrier and ended against its far face, not through it.</summary>
    private static void AssertBlockedAt(EnemyController target, EncounterLockBarrier barrier, float startX) {
        float halfWidth = SafeRelocation.BodyOf(target, needsFloor: true, passesWalls: false)?.HalfWidth ?? 23f;
        float face = barrier.Footprint.End.X;
        AssertThat(startX - target.GlobalPosition.X)
            .OverrideFailureMessage("The pull never moved the target toward the barrier.")
            .IsGreater(5f);
        AssertThat(target.GlobalPosition.X - halfWidth >= face - 1f)
            .OverrideFailureMessage($"The target's body reached x {target.GlobalPosition.X - halfWidth:0.0}, past the barrier's face at {face:0.0}.")
            .IsTrue();
    }

    // === The arena ===========================================================

    /// <summary>
    /// A flat Environment floor (top at y 900) far from the origin. Casters stand
    /// at x 0 facing east; targets are fresh <c>chrono_slasher</c>s with their
    /// physics processing off. Freed on dispose.
    /// </summary>
    private sealed class PullArena : IDisposable {
        private readonly Node2D _host;

        public PullArena(string name) {
            _host = new Node2D { Name = name };
            Tree.Root.AddChild(_host);
            AddSolid(new Rect2(Origin.X - 800f, FloorTopY, 2400f, 40f));
        }

        public void AddStep(float minX, float maxX, float height) =>
            AddSolid(new Rect2(Origin.X + minX, FloorTopY - height, maxX - minX, height));

        public EncounterLockBarrier AddBarrier(float x) {
            EncounterLockBarrier barrier = EncounterLockBarrier.Create("kit_pull_test", isEntry: false, areaSide: -1f,
                Origin.X + x, FloorTopY - 600f, FloorTopY, EncounterLockRules.DefaultBarrierWidthPixels);
            _host.AddChild(barrier);
            barrier.SetClosed(true);
            return barrier;
        }

        public PlayerController AddCaster(string characterID) {
            PlayerController caster = CharacterFactory.CreateCharacter(characterID, 0,
                applyStoryProgression: false, applyLegacyUnlockLocks: false);
            caster.Position = new Vector2(Origin.X, FloorTopY);
            _host.AddChild(caster);
            caster.GlobalPosition = new Vector2(Origin.X, FloorTopY);
            caster.IsFacingRight = true;
            caster.Velocity = Vector2.Zero;
            caster.SetPhysicsProcess(false);
            return caster;
        }

        public EnemyController AddTarget(float x, float feetY) {
            EnemyData canonical = AuthoredResources.Load<EnemyData>("res://resources/Enemies/chrono_slasher.tres");
            var data = (EnemyData)canonical.Duplicate();
            PackedScene scene = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
            var enemy = scene.Instantiate<EnemyController>();
            enemy.Data = data;
            // Positioned before entering the tree, so the physics server
            // registers its hurtbox where the shape queries look. A hair above
            // its footing, so a horizontal cast never grazes it.
            Vector2 position = new(Origin.X + x, feetY - 0.5f);
            enemy.Position = position;
            _host.AddChild(enemy);
            enemy.OnSpawn();
            enemy.GlobalPosition = position;
            enemy.CurrentHP = 9999;
            enemy.Velocity = Vector2.Zero;
            enemy.SetPhysicsProcess(false);
            return enemy;
        }

        /// <summary>Two physics frames so every shape is in the space before the cast.</summary>
        public async Task Settle() {
            for (int frame = 0; frame < 2; frame++) await Tree.ToSignal(Tree, SceneTree.SignalName.PhysicsFrame);
        }

        private void AddSolid(Rect2 rect) {
            var body = new StaticBody2D {
                Name = "Solid",
                Position = rect.GetCenter(),
                CollisionLayer = CollisionLayers.Environment,
                CollisionMask = 0
            };
            body.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = rect.Size } });
            _host.AddChild(body);
        }

        public void Dispose() {
            PoolManager.Instance?.ReleaseActiveUnder(_host);
            if (GodotObject.IsInstanceValid(_host)) {
                _host.GetParent()?.RemoveChild(_host);
                _host.Free();
            }
        }
    }
}
